using UnityEngine;

public class VoicelineManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private F16Controller playerController;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private AudioSource audioSource;

    [Header("Voiceline Clips")]
    [SerializeField] private AudioClip altitudeTooLowClip;
    [SerializeField] private AudioClip deployFlareClip;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip defeatClip;

    [Header("Altitude Warning Settings")]
    [SerializeField] private float lowAltitudeThreshold = 150f;
    [SerializeField] private float altitudeWarningCooldown = 5f;

    [Header("Flare Warning Settings")]
    [SerializeField] private float flareWarningCooldown = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // Cooldown tracking
    private float lastAltitudeWarningTime = -999f;
    private float lastFlareWarningTime = -999f;
    private bool hasPlayedGameOverVoiceline = false;

    void Start()
    {
        InitializeVoicelineManager();
    }

    void InitializeVoicelineManager()
    {
        // Auto-find player controller if not assigned
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<F16Controller>();
        }

        // Auto-find score manager if not assigned
        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }

        // Auto-find or create audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D sound
            }
        }

        // Subscribe to game over event
        if (scoreManager != null)
        {
            scoreManager.OnGameOver += OnGameOver;
        }
        else
        {
            Debug.LogError("[VoicelineManager] ScoreManager not found! Victory/Defeat voicelines won't work.");
        }

        if (showDebugInfo)
        {
            Debug.Log("[VoicelineManager] Initialized successfully");
        }
    }

    void Update()
    {
        // Don't play warnings if game is over
        if (scoreManager != null && scoreManager.IsGameOver())
            return;

        CheckAltitudeWarning();
        CheckFlareWarning();
    }

    // ==================== ALTITUDE WARNING ====================
    void CheckAltitudeWarning()
    {
        if (playerController == null || altitudeTooLowClip == null)
            return;

        float currentAltitude = playerController.GetCurrentAltitude();

        // Check if altitude is too low and cooldown has passed
        if (currentAltitude < lowAltitudeThreshold)
        {
            float timeSinceLastWarning = Time.time - lastAltitudeWarningTime;

            if (timeSinceLastWarning >= altitudeWarningCooldown)
            {
                PlayVoiceline(altitudeTooLowClip, "Altitude Too Low");
                lastAltitudeWarningTime = Time.time;
            }
        }
    }

    // ==================== FLARE WARNING ====================
    void CheckFlareWarning()
    {
        // This will be triggered externally when missile is detected
        // For now, check if player pressed flare key as a placeholder
        if (deployFlareClip == null)
            return;

        // Check if flare deployment key is pressed (you can modify this)
        if (Input.GetKeyDown(KeyCode.F))
        {
            float timeSinceLastWarning = Time.time - lastFlareWarningTime;

            if (timeSinceLastWarning >= flareWarningCooldown)
            {
                PlayVoiceline(deployFlareClip, "Deploy Flare");
                lastFlareWarningTime = Time.time;
            }
        }
    }

    /// <summary>
    /// Call this method externally when a missile is detected/locked onto player
    /// </summary>
    public void TriggerFlareWarning()
    {
        if (deployFlareClip == null)
            return;

        float timeSinceLastWarning = Time.time - lastFlareWarningTime;

        if (timeSinceLastWarning >= flareWarningCooldown)
        {
            PlayVoiceline(deployFlareClip, "Deploy Flare");
            lastFlareWarningTime = Time.time;
        }
    }

    // ==================== GAME OVER VOICELINES ====================
    void OnGameOver(string winner)
    {
        // Prevent playing multiple times
        if (hasPlayedGameOverVoiceline)
            return;

        hasPlayedGameOverVoiceline = true;

        // Check if player won or lost
        if (winner == "You") // Match the playerName in ScoreManager
        {
            PlayVoiceline(victoryClip, "Victory");
        }
        else
        {
            PlayVoiceline(defeatClip, "Defeat");
        }
    }

    // ==================== AUDIO PLAYBACK ====================
    void PlayVoiceline(AudioClip clip, string voicelineName)
    {
        if (clip == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[VoicelineManager] {voicelineName} clip not assigned!");
            }
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("[VoicelineManager] AudioSource is null!");
            return;
        }

        // Don't interrupt game over voicelines
        if (hasPlayedGameOverVoiceline && audioSource.isPlaying)
            return;

        audioSource.PlayOneShot(clip);

        if (showDebugInfo)
        {
            Debug.Log($"[VoicelineManager] Playing voiceline: {voicelineName}");
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Manually trigger altitude warning (for testing or external use)
    /// </summary>
    public void ForceAltitudeWarning()
    {
        if (altitudeTooLowClip != null)
        {
            PlayVoiceline(altitudeTooLowClip, "Altitude Too Low (Forced)");
            lastAltitudeWarningTime = Time.time;
        }
    }

    /// <summary>
    /// Manually trigger flare warning (for testing or external use)
    /// </summary>
    public void ForceFlareWarning()
    {
        if (deployFlareClip != null)
        {
            PlayVoiceline(deployFlareClip, "Deploy Flare (Forced)");
            lastFlareWarningTime = Time.time;
        }
    }

    /// <summary>
    /// Set custom altitude threshold
    /// </summary>
    public void SetAltitudeThreshold(float threshold)
    {
        lowAltitudeThreshold = threshold;

        if (showDebugInfo)
        {
            Debug.Log($"[VoicelineManager] Altitude threshold set to: {threshold}");
        }
    }

    /// <summary>
    /// Reset game over flag (useful for game restart)
    /// </summary>
    public void ResetGameOverFlag()
    {
        hasPlayedGameOverVoiceline = false;

        if (showDebugInfo)
        {
            Debug.Log("[VoicelineManager] Game over flag reset");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (scoreManager != null)
        {
            scoreManager.OnGameOver -= OnGameOver;
        }
    }
}