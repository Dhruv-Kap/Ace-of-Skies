using UnityEngine;
using TMPro;

/// <summary>
/// Flare countermeasure system for aircraft
/// Deploys flares to distract infrared-guided missiles
/// Attach to an empty GameObject on your aircraft
/// </summary>
public class FlareSystem : MonoBehaviour
{
    [Header("Flare Configuration")]
    [SerializeField] private int maxFlares = 5;
    [SerializeField] private float cooldownTime = 7.5f;
    [SerializeField] private float flareActiveDuration = 3f;
    [SerializeField] private float flareEffectiveRange = 5000f;

    [Header("Input")]
    [SerializeField] private KeyCode deployKey = KeyCode.F;

    [Header("Visual Effects")]
    [SerializeField] private GameObject flarePrefab;
    [SerializeField] private Transform flareSpawnPoint;
    [SerializeField] private AudioClip deploySound;
    [SerializeField] private AudioSource audioSource;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI flareCountText;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Runtime state
    private int currentFlares;
    private float lastDeployTime = -999f;
    private GameObject activeFlare;

    void Start()
    {
        currentFlares = maxFlares;
        UpdateUI();

        if (flareSpawnPoint == null)
        {
            flareSpawnPoint = transform;
            if (showDebug)
                Debug.LogWarning("[FlareSystem] No flare spawn point assigned, using self position");
        }

        if (showDebug)
            Debug.Log($"[FlareSystem] Initialized with {maxFlares} flares");
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(deployKey))
        {
            TryDeployFlare();
        }
    }

    void TryDeployFlare()
    {
        // Check if on cooldown
        if (Time.time - lastDeployTime < cooldownTime)
        {
            float remainingCooldown = cooldownTime - (Time.time - lastDeployTime);
            if (showDebug)
                Debug.Log($"[FlareSystem] On cooldown! {remainingCooldown:F1}s remaining");
            return;
        }

        // Check if out of flares
        if (currentFlares <= 0)
        {
            if (showDebug)
                Debug.Log("[FlareSystem] Out of flares!");
            return;
        }

        // Deploy flare
        DeployFlare();
    }

    void DeployFlare()
    {
        currentFlares--;
        lastDeployTime = Time.time;

        // Spawn flare visual effect
        if (flarePrefab != null && flareSpawnPoint != null)
        {
            activeFlare = Instantiate(flarePrefab, flareSpawnPoint.position, flareSpawnPoint.rotation);

            // Add Flare component to track it
            Flare flareComponent = activeFlare.AddComponent<Flare>();
            flareComponent.Initialize(flareActiveDuration, flareEffectiveRange);

            if (showDebug)
                Debug.Log($"[FlareSystem] Flare deployed at {flareSpawnPoint.position}");
        }
        else
        {
            // Create invisible flare if no prefab
            activeFlare = new GameObject("Flare");
            activeFlare.transform.position = flareSpawnPoint.position;

            Flare flareComponent = activeFlare.AddComponent<Flare>();
            flareComponent.Initialize(flareActiveDuration, flareEffectiveRange);

            if (showDebug)
                Debug.Log("[FlareSystem] Flare deployed (no visual prefab)");
        }

        // Play sound
        if (audioSource != null && deploySound != null)
        {
            audioSource.PlayOneShot(deploySound);
        }

        UpdateUI();

        if (showDebug)
            Debug.Log($"[FlareSystem] Flare deployed! Remaining: {currentFlares}/{maxFlares}");
    }

    void UpdateUI()
    {
        if (flareCountText != null)
        {
            flareCountText.text = currentFlares.ToString();
        }
    }

    public void Reload()
    {
        int flaresNeeded = maxFlares - currentFlares;
        if (flaresNeeded > 0)
        {
            currentFlares = maxFlares;
            UpdateUI();

            if (showDebug)
                Debug.Log($"[FlareSystem] Reloaded {flaresNeeded} flares");
        }
    }

    public bool NeedsReload()
    {
        return currentFlares < maxFlares;
    }

    public int GetCurrentFlares() => currentFlares;
    public int GetMaxFlares() => maxFlares;
    public bool IsOnCooldown() => Time.time - lastDeployTime < cooldownTime;
    public float GetCooldownRemaining() => Mathf.Max(0f, cooldownTime - (Time.time - lastDeployTime));
}

/// <summary>
/// Individual flare object that can distract missiles
/// Automatically added to spawned flare objects
/// </summary>
public class Flare : MonoBehaviour
{
    private float expiryTime;
    private float effectiveRange;
    private bool isActive = true;

    public void Initialize(float duration, float range)
    {
        effectiveRange = range;
        expiryTime = Time.time + duration;
    }

    void Update()
    {
        if (Time.time >= expiryTime)
        {
            isActive = false;
            Destroy(gameObject);
        }
    }

    public bool IsActive() => isActive;
    public float GetEffectiveRange() => effectiveRange;
}
