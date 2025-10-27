using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles player and bot respawning - Works with centralized AircraftLoadoutManager
/// Attach to a GameObject in your scene (can be same as other managers)
/// </summary>
public class RespawnManager : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Respawn Settings")]
    [SerializeField] private float playerRespawnDelay = 3f;
    [SerializeField] private bool respawnAtSameLocation = true;
    [SerializeField] private Transform customSpawnPoint;

    [Header("Bot Management")]
    [SerializeField] private F16BotGunFire[] allBots;
    [SerializeField] private bool stopBotsOnPlayerDeath = true;

    [Header("Bot Respawn Settings")]
    [SerializeField] private bool enableBotRespawn = true;
    [SerializeField] private float botRespawnDelay = 5f;
    [SerializeField] private Transform[] botSpawnPoints; // Assign respawn locations for bots
    [SerializeField] private bool useBotInitialPositions = true; // If true, bots respawn at their starting position

    [Header("Player Controls")]
    [SerializeField] private F16Controller playerControllerScript;
    [SerializeField] private bool disableControlsOnDeath = true;

    [Header("Death Effects")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Collision Death Settings")]
    [SerializeField] private bool enableCollisionDeath = true;
    [SerializeField] private LayerMask collisionDeathLayers = -1; // Which layers cause death (default: all)

    [Header("UI Elements - Assign These!")]
    [SerializeField] private Transform crosshairImage;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI deathText;
    [SerializeField] private TextMeshProUGUI respawnTimerText;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private Vector3 lastPlayerPosition;
    private Quaternion lastPlayerRotation;
    private bool playerIsDead = false;
    private bool isRespawning = false;
    private ScoreManager scoreManager;
    private WeaponManager playerWeaponManager;
    private float respawnTimeRemaining = 0f;

    // Death loop protection
    private float lastRespawnTime = -999f;
    private int consecutiveDeaths = 0;
    private const float MIN_TIME_BETWEEN_DEATHS = 2f;

    // Bot respawn tracking
    private Dictionary<F16BotGunFire, Vector3> botInitialPositions = new Dictionary<F16BotGunFire, Vector3>();
    private Dictionary<F16BotGunFire, Quaternion> botInitialRotations = new Dictionary<F16BotGunFire, Quaternion>();
    private Dictionary<F16BotGunFire, bool> botRespawnInProgress = new Dictionary<F16BotGunFire, bool>();

    private int nextBotSpawnIndex = 0;


    void Start()
    {
        InitializeRespawnManager();

        // Hide death UI initially
        if (deathText != null)
            deathText.gameObject.SetActive(false);
        if (respawnTimerText != null)
            respawnTimerText.gameObject.SetActive(false);

        // Setup collision detection for player and bots
        SetupCollisionDetection();

        // Store initial bot positions
        StoreBotInitialPositions();
    }

    void InitializeRespawnManager()
    {
        scoreManager = FindFirstObjectByType<ScoreManager>();

        // Auto-find player
        if (playerObject == null)
        {
            playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
                playerObject = GameObject.Find("F16 pivot");
        }

        // Auto-find components
        if (playerObject != null)
        {
            if (playerHealth == null)
                playerHealth = playerObject.GetComponent<PlayerHealth>();

            if (playerWeaponManager == null)
                playerWeaponManager = playerObject.GetComponentInChildren<WeaponManager>();

            if (playerControllerScript == null)
                playerControllerScript = playerObject.GetComponent<F16Controller>();
        }

        // Validate setup
        if (AircraftLoadoutManager.Instance == null)
        {
            Debug.LogError("[RespawnManager]  AircraftLoadoutManager NOT FOUND! Create empty GameObject and add AircraftLoadoutManager script!");
        }
        else if (showDebugInfo)
        {
            Debug.Log("[RespawnManager]  Connected to AircraftLoadoutManager");
        }

        // Auto-find bots
        if (allBots == null || allBots.Length == 0)
        {
            allBots = FindObjectsByType<F16BotGunFire>(FindObjectsSortMode.None);
            if (showDebugInfo)
            {
                Debug.Log($"[RespawnManager] Found {allBots.Length} bots");
            }
        }

        // Subscribe to game over
        if (scoreManager != null)
        {
            scoreManager.OnGameOver += OnGameOver;
        }

        UpdatePlayerPosition();

        if (showDebugInfo)
        {
            Debug.Log("[RespawnManager]  Initialized successfully");
        }
    }

    void StoreBotInitialPositions()
    {
        if (allBots == null) return;

        foreach (F16BotGunFire bot in allBots)
        {
            if (bot != null)
            {
                botInitialPositions[bot] = bot.transform.position;
                botInitialRotations[bot] = bot.transform.rotation;
                botRespawnInProgress[bot] = false;

                // Subscribe to bot death events
                Health botHealth = bot.GetComponent<Health>();
                if (botHealth != null)
                {
                    botHealth.OnKilled += (deadBot, killer) => HandleBotDeath(bot);
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Stored initial positions for {botInitialPositions.Count} bots");
        }
    }

    void SetupCollisionDetection()
    {
        if (!enableCollisionDeath) return;

        // Add collision detector to player
        if (playerObject != null)
        {
            // Find the GameObject with Rigidbody and add detector there
            Rigidbody playerRb = playerObject.GetComponentInChildren<Rigidbody>();
            if (playerRb != null)
            {
                CollisionDeathDetector detector = playerRb.gameObject.GetComponent<CollisionDeathDetector>();
                if (detector == null)
                {
                    detector = playerRb.gameObject.AddComponent<CollisionDeathDetector>();
                }
                detector.Initialize(this, true, collisionDeathLayers);

                if (showDebugInfo)
                {
                    Debug.Log($"[RespawnManager] Player collision detector added to {playerRb.gameObject.name} (has Rigidbody)");
                }
            }
            else
            {
                Debug.LogError("[RespawnManager] Player has no Rigidbody! Collision detection won't work!");
            }
        }

        // Add collision detector to all bots
        if (allBots != null)
        {
            foreach (F16BotGunFire bot in allBots)
            {
                if (bot != null)
                {
                    // Find the GameObject with Rigidbody and add detector there
                    Rigidbody botRb = bot.GetComponentInChildren<Rigidbody>();
                    if (botRb != null)
                    {
                        CollisionDeathDetector detector = botRb.gameObject.GetComponent<CollisionDeathDetector>();
                        if (detector == null)
                        {
                            detector = botRb.gameObject.AddComponent<CollisionDeathDetector>();
                        }
                        detector.Initialize(this, false, collisionDeathLayers);

                        if (showDebugInfo)
                        {
                            Debug.Log($"[RespawnManager] Bot {bot.name} collision detector added to {botRb.gameObject.name} (has Rigidbody)");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[RespawnManager] Bot {bot.name} has no Rigidbody! Collision detection won't work!");
                    }
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log("[RespawnManager] Collision death detection setup complete");
        }
    }

    void AddCollisionDetectorRecursive(GameObject obj, bool isPlayer)
    {
        // This method is no longer used, but keeping for backward compatibility
        // Find the Rigidbody and add detector to that GameObject instead
        Rigidbody rb = obj.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            CollisionDeathDetector detector = rb.gameObject.GetComponent<CollisionDeathDetector>();
            if (detector == null)
            {
                detector = rb.gameObject.AddComponent<CollisionDeathDetector>();
            }
            detector.Initialize(this, isPlayer, collisionDeathLayers);

            if (showDebugInfo)
            {
                Debug.Log($"[RespawnManager] Added detector to {rb.gameObject.name} (has Rigidbody)");
            }
        }
    }

    void Update()
    {
        // Don't respawn if game is over
        if (scoreManager != null && scoreManager.IsGameOver())
            return;

        // Check for player death
        if (!playerIsDead && playerHealth != null && playerHealth.IsDead)
        {
            // Death loop protection
            float timeSinceLastDeath = Time.time - lastRespawnTime;

            if (timeSinceLastDeath > MIN_TIME_BETWEEN_DEATHS)
            {
                HandlePlayerDeath();
            }
            else
            {
                consecutiveDeaths++;

                if (showDebugInfo)
                {
                    Debug.LogWarning($"[RespawnManager]  Death loop detected (#{consecutiveDeaths})! Force-resetting health.");
                }

                // Emergency: Force health reset to break death loop
                playerHealth.ResetHealth();
                playerIsDead = false;

                // If loops continue, teleport to safety
                if (consecutiveDeaths > 3)
                {
                    if (customSpawnPoint != null)
                    {
                        playerObject.transform.position = customSpawnPoint.position;
                        playerObject.transform.rotation = customSpawnPoint.rotation;
                    }
                    else
                    {
                        // Emergency safe position
                        playerObject.transform.position = new Vector3(0, 500, 0);
                        playerObject.transform.rotation = Quaternion.identity;
                    }
                    consecutiveDeaths = 0;

                    if (showDebugInfo)
                    {
                        Debug.Log("[RespawnManager] Emergency teleport to break death loop!");
                    }
                }
            }
        }

        // Update respawn timer display
        if (playerIsDead && respawnTimeRemaining > 0)
        {
            respawnTimeRemaining -= Time.deltaTime;
            UpdateRespawnTimerDisplay();
        }

        // Update player position when alive
        if (!playerIsDead && playerObject != null)
        {
            UpdatePlayerPosition();
        }
    }

    void UpdateRespawnTimerDisplay()
    {
        if (respawnTimerText != null && respawnTimeRemaining > 0)
        {
            respawnTimerText.text = respawnTimeRemaining.ToString("F1");
        }
    }

    void UpdatePlayerPosition()
    {
        if (playerObject != null)
        {
            lastPlayerPosition = playerObject.transform.position;
            lastPlayerRotation = playerObject.transform.rotation;
        }
    }

    void HandlePlayerDeath()
    {
        if (isRespawning) return;

        playerIsDead = true;
        isRespawning = true;
        respawnTimeRemaining = playerRespawnDelay;

        if (showDebugInfo)
        {
            Debug.Log("[RespawnManager]  Player died - starting respawn sequence");
        }

        // Hide crosshair and ammo
        if (crosshairImage != null)
            crosshairImage.gameObject.SetActive(false);
        if (ammoText != null)
            ammoText.gameObject.SetActive(false);

        // Show death UI
        if (deathText != null)
            deathText.gameObject.SetActive(true);
        if (respawnTimerText != null)
        {
            respawnTimerText.gameObject.SetActive(true);
            UpdateRespawnTimerDisplay();
        }

        // Disable controls
        if (disableControlsOnDeath && playerControllerScript != null)
        {
            playerControllerScript.enabled = false;
        }

        // Stop bots from firing
        if (stopBotsOnPlayerDeath)
        {
            SetBotsCanFire(false);
        }

        // Spawn death effect
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, playerObject.transform.position, playerObject.transform.rotation);
        }

        // Start respawn countdown
        StartCoroutine(RespawnPlayerAfterDelay());
    }

    IEnumerator RespawnPlayerAfterDelay()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager]  Respawning in {playerRespawnDelay} seconds...");
        }

        yield return new WaitForSeconds(playerRespawnDelay);

        // Check if game ended during respawn delay
        if (scoreManager != null && scoreManager.IsGameOver())
        {
            if (showDebugInfo)
            {
                Debug.Log("[RespawnManager] Game over - respawn cancelled");
            }
            yield break;
        }

        RespawnPlayer();
    }

    void RespawnPlayer()
    {
        if (playerObject == null || playerHealth == null)
        {
            Debug.LogError("[RespawnManager] Cannot respawn - missing player or health component!");
            return;
        }

        if (showDebugInfo)
            Debug.Log("[RespawnManager] Respawning player...");

        // Show crosshair and ammo
        if (crosshairImage != null)
            crosshairImage.gameObject.SetActive(true);
        if (ammoText != null)
            ammoText.gameObject.SetActive(true);

        // Hide death UI
        if (deathText != null)
            deathText.gameObject.SetActive(false);
        if (respawnTimerText != null)
            respawnTimerText.gameObject.SetActive(false);

        // Determine respawn position
        Vector3 respawnPosition;
        Quaternion respawnRotation;

        if (customSpawnPoint != null)
        {
            respawnPosition = customSpawnPoint.position;
            respawnRotation = customSpawnPoint.rotation;
        }
        else if (respawnAtSameLocation)
        {
            respawnPosition = lastPlayerPosition;
            respawnRotation = lastPlayerRotation;
        }
        else
        {
            respawnPosition = new Vector3(0, 500, 0); // Safe default altitude
            respawnRotation = Quaternion.identity;
        }

        // Move player to respawn position
        playerObject.transform.position = respawnPosition;
        playerObject.transform.rotation = respawnRotation;

        // STEP 1: Reset health FIRST
        playerHealth.ResetHealth();

        // STEP 2: Restore weapons using centralized F-16 loadout manager
        if (AircraftLoadoutManager.Instance != null)
        {
            AircraftLoadoutManager.Instance.RestoreLoadout(playerObject);
            if (showDebugInfo)
            {
                Debug.Log("[RespawnManager] Missiles restored via loadout manager");
            }
        }
        else
        {
            Debug.LogError("[RespawnManager] AircraftLoadoutManager not found - weapons NOT restored!");
        }

        // STEP 3: Re-enable player controls
        if (disableControlsOnDeath && playerControllerScript != null)
            playerControllerScript.enabled = true;

        // STEP 4: Allow bots to fire again
        if (stopBotsOnPlayerDeath)
            SetBotsCanFire(true);

        // STEP 5: Refresh weapon manager UI
        if (playerWeaponManager != null)
            playerWeaponManager.SwitchToWeapon(WeaponManager.WeaponType.Gun);

        // Reset death tracking
        playerIsDead = false;
        isRespawning = false;
        lastRespawnTime = Time.time;
        consecutiveDeaths = 0;
        respawnTimeRemaining = 0f;

        if (showDebugInfo)
            Debug.Log("[RespawnManager] Respawn complete!");
    }

    void SetBotsCanFire(bool canFire)
    {
        if (allBots == null) return;

        int botCount = 0;
        foreach (F16BotGunFire bot in allBots)
        {
            if (bot != null)
            {
                bot.SetCanFire(canFire);
                botCount++;
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Bots firing: {canFire} ({botCount} bots)");
        }
    }

    void OnGameOver(string winner)
    {
        StopAllCoroutines();
        SetBotsCanFire(false);

        // Hide death UI on game over
        if (deathText != null)
            deathText.gameObject.SetActive(false);
        if (respawnTimerText != null)
            respawnTimerText.gameObject.SetActive(false);

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager]  Game over - {winner} won. Respawning stopped.");
        }
    }

    // ==================== COLLISION DEATH HANDLING ====================

    /// <summary>
    /// Called by CollisionDeathDetector when player/bot collides with mesh
    /// </summary>
    public void HandleCollisionDeath(GameObject entity, bool isPlayer)
    {
        if (isPlayer)
        {
            // Kill player
            if (playerHealth != null && !playerHealth.IsDead)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[RespawnManager] Player died from collision!");
                }

                // Set the killer to null for collision deaths
                playerHealth.OnKilled?.Invoke(playerObject, null);
                playerHealth.TakeDamage(9999f); // Instant death with massive damage
            }
        }
        else
        {
            // Kill bot
            Health botHealth = entity.GetComponent<Health>();
            if (botHealth != null && !botHealth.IsDead)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[RespawnManager] Bot {entity.name} died from collision!");
                }

                // Trigger the OnKilled event properly for bots
                botHealth.TakeDamage(9999f); // Instant death with massive damage
            }
        }
    }

    // ==================== BOT RESPAWN HANDLING ====================

    /// <summary>
    /// Called when a bot dies - initiates respawn sequence
    /// </summary>
    void HandleBotDeath(F16BotGunFire bot)
    {
        if (!enableBotRespawn) return;
        if (bot == null) return;
        if (scoreManager != null && scoreManager.IsGameOver()) return;

        // Check if already respawning
        if (botRespawnInProgress.ContainsKey(bot) && botRespawnInProgress[bot])
        {
            return;
        }

        botRespawnInProgress[bot] = true;

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Bot {bot.name} died - starting respawn sequence");
        }

        // Spawn death effect
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, bot.transform.position, bot.transform.rotation);
        }

        // Start respawn countdown
        StartCoroutine(RespawnBotAfterDelay(bot));
    }

    IEnumerator RespawnBotAfterDelay(F16BotGunFire bot)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Bot {bot.name} respawning in {botRespawnDelay} seconds...");
        }

        yield return new WaitForSeconds(botRespawnDelay);

        // Check if game ended during respawn delay
        if (scoreManager != null && scoreManager.IsGameOver())
        {
            if (showDebugInfo)
            {
                Debug.Log("[RespawnManager] Game over - bot respawn cancelled");
            }
            yield break;
        }

        // Check if bot still exists
        if (bot == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[RespawnManager] Bot was destroyed, cannot respawn");
            }
            yield break;
        }

        RespawnBot(bot);
    }

    void RespawnBot(F16BotGunFire bot)
    {
        if (bot == null) return;

        Health botHealth = bot.GetComponent<Health>();
        if (botHealth == null) return;

        Vector3 respawnPosition = Vector3.zero;
        Quaternion respawnRotation = Quaternion.identity;

        if (botSpawnPoints != null && botSpawnPoints.Length > 0)
        {
            List<Transform> freeSpawnPoints = new List<Transform>();

            foreach (Transform spawn in botSpawnPoints)
            {
                bool occupied = false;
                foreach (F16BotGunFire otherBot in allBots)
                {
                    if (otherBot != null && otherBot != bot)
                    {
                        // Consider spawn "occupied" if another bot is within 10 units
                        if (Vector3.Distance(otherBot.transform.position, spawn.position) < 10f)
                        {
                            occupied = true;
                            break;
                        }
                    }
                }
                if (!occupied) freeSpawnPoints.Add(spawn);
            }

            if (freeSpawnPoints.Count > 0)
            {
                // Pick a random free spawn point
                Transform chosen = freeSpawnPoints[Random.Range(0, freeSpawnPoints.Count)];
                respawnPosition = chosen.position;
                respawnRotation = chosen.rotation;
            }
            else
            {
                // If all spawn points occupied, pick the **least crowded** one
                Transform leastCrowded = botSpawnPoints[0];
                float minDistanceSum = float.MaxValue;

                foreach (Transform spawn in botSpawnPoints)
                {
                    float distanceSum = 0f;
                    foreach (F16BotGunFire otherBot in allBots)
                    {
                        if (otherBot != null && otherBot != bot)
                        {
                            distanceSum += Vector3.Distance(otherBot.transform.position, spawn.position);
                        }
                    }
                    if (distanceSum < minDistanceSum)
                    {
                        minDistanceSum = distanceSum;
                        leastCrowded = spawn;
                    }
                }

                respawnPosition = leastCrowded.position;
                respawnRotation = leastCrowded.rotation;
            }
        }
        else if (useBotInitialPositions && botInitialPositions.ContainsKey(bot))
        {
            respawnPosition = botInitialPositions[bot];
            respawnRotation = botInitialRotations[bot];
        }
        else
        {
            Debug.LogWarning($"[RespawnManager] Bot {bot.name} has no valid spawn point!");
            return;
        }

        // Move bot
        bot.transform.position = respawnPosition;
        bot.transform.rotation = respawnRotation;

        // Reset Rigidbody
        Rigidbody botRb = bot.GetComponentInChildren<Rigidbody>();
        if (botRb != null)
        {
            botRb.linearVelocity = Vector3.zero;
            botRb.angularVelocity = Vector3.zero;
        }

        // Reset health & weapons
        botHealth.ResetHealth();
        if (AircraftLoadoutManager.Instance != null)
            AircraftLoadoutManager.Instance.RestoreLoadout(bot.gameObject);

        // Enable firing
        bot.SetCanFire(true);

        // Reset respawn flag
        botRespawnInProgress[bot] = false;

        if (showDebugInfo)
            Debug.Log($"[RespawnManager] Bot {bot.name} respawned at {respawnPosition}");
    }




    // ==================== PUBLIC API ====================

    /// <summary>
    /// Manually trigger respawn (useful for testing or forced respawn)
    /// </summary>
    public void ForceRespawn()
    {
        if (isRespawning)
        {
            Debug.LogWarning("[RespawnManager] Already respawning!");
            return;
        }

        if (scoreManager != null && scoreManager.IsGameOver())
        {
            Debug.LogWarning("[RespawnManager] Cannot respawn - game is over!");
            return;
        }

        HandlePlayerDeath();
    }

    /// <summary>
    /// Check if player is currently alive
    /// </summary>
    public bool IsPlayerAlive()
    {
        return !playerIsDead;
    }

    /// <summary>
    /// Check if respawn is in progress
    /// </summary>
    public bool IsRespawning()
    {
        return isRespawning;
    }

    /// <summary>
    /// Set a custom spawn point for respawning
    /// </summary>
    public void SetSpawnPoint(Transform spawnPoint)
    {
        customSpawnPoint = spawnPoint;
        respawnAtSameLocation = false;

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Custom spawn point set: {spawnPoint.name}");
        }
    }

    /// <summary>
    /// Register a new bot to the managed list
    /// </summary>
    public void RegisterBot(F16BotGunFire bot)
    {
        // Auto-find bots
        if (allBots == null || allBots.Length == 0)
        {
            allBots = FindObjectsByType<F16BotGunFire>(FindObjectsSortMode.None);
            if (showDebugInfo)
            {
                Debug.Log($"[RespawnManager] Found {allBots.Length} bots");
            }
        }

        // Setup collision detection for each bot
        foreach (F16BotGunFire b in allBots)
        {
            if (b == null) continue;

            Rigidbody botRb = b.GetComponent<Rigidbody>(); // Use parent Rigidbody
            if (botRb == null)
            {
                botRb = b.GetComponentInChildren<Rigidbody>();
            }

            if (botRb != null)
            {
                CollisionDeathDetector detector = botRb.GetComponent<CollisionDeathDetector>();
                if (detector == null)
                {
                    detector = botRb.gameObject.AddComponent<CollisionDeathDetector>();
                }

                detector.Initialize(this, false, collisionDeathLayers);

                if (showDebugInfo)
                    Debug.Log($"[RespawnManager] Collision detector added to Bot Rigidbody: {botRb.gameObject.name}");
            }
            else
            {
                Debug.LogError($"[RespawnManager] Bot {b.name} has no Rigidbody! Collision detection won't work!");
            }
        }


    }

    /// <summary>
    /// Get a random respawn position (for bots or fallback)
    /// Returns a safe altitude position
    /// </summary>
    public Vector3 GetRandomBotRespawnPosition()
    {
        // Return a random safe position at altitude
        float randomX = Random.Range(-1000f, 1000f);
        float randomZ = Random.Range(-1000f, 1000f);
        return new Vector3(randomX, 500f, randomZ);
    }

    /// <summary>
    /// Add a bot spawn point dynamically
    /// </summary>
    public void AddBotSpawnPoint(Transform spawnPoint)
    {
        if (spawnPoint == null) return;

        if (botSpawnPoints == null)
        {
            botSpawnPoints = new Transform[] { spawnPoint };
        }
        else
        {
            Transform[] newArray = new Transform[botSpawnPoints.Length + 1];
            botSpawnPoints.CopyTo(newArray, 0);
            newArray[botSpawnPoints.Length] = spawnPoint;
            botSpawnPoints = newArray;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Added bot spawn point: {spawnPoint.name}");
        }
    }

    /// <summary>
    /// Set whether bots should respawn at their initial positions
    /// </summary>
    public void SetUseBotInitialPositions(bool useInitial)
    {
        useBotInitialPositions = useInitial;

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Bot initial positions: {(useInitial ? "Enabled" : "Disabled")}");
        }
    }

    /// <summary>
    /// Enable or disable bot respawning at runtime
    /// </summary>
    public void SetBotRespawnEnabled(bool enabled)
    {
        enableBotRespawn = enabled;

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Bot respawn: {(enabled ? "Enabled" : "Disabled")}");
        }
    }

}