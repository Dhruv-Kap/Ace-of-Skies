using UnityEngine;
using System.Collections;

/// <summary>
/// Handles player respawning - Works with centralized AircraftLoadoutManager
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

    [Header("Player Controls")]
    [SerializeField] private F16Controller playerControllerScript;
    [SerializeField] private bool disableControlsOnDeath = true;

    [Header("Death Effects")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private Vector3 lastPlayerPosition;
    private Quaternion lastPlayerRotation;
    private bool playerIsDead = false;
    private bool isRespawning = false;
    private ScoreManager scoreManager;
    private WeaponManager playerWeaponManager;

    // Death loop protection
    private float lastRespawnTime = -999f;
    private int consecutiveDeaths = 0;
    private const float MIN_TIME_BETWEEN_DEATHS = 2f;

    void Start()
    {
        InitializeRespawnManager();
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

        // Update player position when alive
        if (!playerIsDead && playerObject != null)
        {
            UpdatePlayerPosition();
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

        if (showDebugInfo)
        {
            Debug.Log("[RespawnManager]  Player died - starting respawn sequence");
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
            Debug.LogError("[RespawnManager]  Cannot respawn - missing player or health component!");
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log("[RespawnManager]  Respawning player...");
        }

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

        // STEP 1: Reset health FIRST (critical!)
        playerHealth.ResetHealth();

        // STEP 2: Restore weapons using centralized loadout manager
        if (AircraftLoadoutManager.Instance != null)
        {
            int missilesRestored = AircraftLoadoutManager.Instance.RestoreLoadout(playerObject);

            if (showDebugInfo)
            {
                Debug.Log($"[RespawnManager]  Restored {missilesRestored} missiles via loadout manager");
            }
        }
        else
        {
            Debug.LogError("[RespawnManager]  AircraftLoadoutManager not found - weapons NOT restored!");
        }

        // STEP 3: Re-enable player controls
        if (disableControlsOnDeath && playerControllerScript != null)
        {
            playerControllerScript.enabled = true;
        }

        // STEP 4: Allow bots to fire again
        if (stopBotsOnPlayerDeath)
        {
            SetBotsCanFire(true);
        }

        // STEP 5: Refresh weapon manager UI
        if (playerWeaponManager != null)
        {
            playerWeaponManager.SwitchToWeapon(WeaponManager.WeaponType.Gun);
        }

        // Reset death tracking
        playerIsDead = false;
        isRespawning = false;
        lastRespawnTime = Time.time;
        consecutiveDeaths = 0;

        if (showDebugInfo)
        {
            Debug.Log("[RespawnManager]  Respawn complete!");
        }
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

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager]  Game over - {winner} won. Respawning stopped.");
        }
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
        if (bot == null) return;

        F16BotGunFire[] newArray = new F16BotGunFire[allBots.Length + 1];
        for (int i = 0; i < allBots.Length; i++)
        {
            newArray[i] = allBots[i];
        }
        newArray[allBots.Length] = bot;
        allBots = newArray;

        if (showDebugInfo)
        {
            Debug.Log($"[RespawnManager] Bot registered: {bot.name}");
        }
    }

    /// <summary>
    /// Get a random respawn position (for bots or fallback)
    /// Returns a safe altitude position
    /// </summary>
    public Vector3 GetRandomBotRespawnPosition()
    {
        // If custom spawn point exists, use it
        if (customSpawnPoint != null)
        {
            return customSpawnPoint.position;
        }

        // Otherwise return a random safe position at altitude
        float randomX = Random.Range(-1000f, 1000f);
        float randomZ = Random.Range(-1000f, 1000f);
        return new Vector3(randomX, 500f, randomZ);
    }
}