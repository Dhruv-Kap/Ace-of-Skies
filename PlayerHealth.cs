using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("UI References")]
    [SerializeField] private Image healthBarFill; // Drag your health bar image here
    [SerializeField] private TextMeshProUGUI healthText; // Drag your TMP text here

    // Events for managers to listen to
    public System.Action<GameObject, GameObject> OnKilled; // (victim, killer)

    // Public properties for external access
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => currentHealth / maxHealth;
    public bool IsDead => currentHealth <= 0;

    void Start()
    {
        // Initialize health to maximum
        currentHealth = maxHealth;

        // Update UI on start
        UpdateHealthUI();

        Debug.Log($"[PlayerHealth] Player health initialized: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Take damage - called by enemy scripts (IDamageable interface)
    /// </summary>
    /// <param name="damageAmount">Amount of damage to take</param>
    public void TakeDamage(float damageAmount)
    {
        TakeDamage(damageAmount, null);
    }

    /// <summary>
    /// Take damage with killer tracking
    /// </summary>
    /// <param name="damageAmount">Amount of damage to take</param>
    /// <param name="damageDealer">Who caused the damage</param>
    public void TakeDamage(float damageAmount, GameObject damageDealer)
    {
        Debug.Log($"[PlayerHealth] TakeDamage called with {damageAmount} damage from {(damageDealer != null ? damageDealer.name : "unknown")}!");

        // Don't take damage if already dead
        if (IsDead) return;

        // Apply damage
        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // Update UI
        UpdateHealthUI();

        // Log damage taken
        Debug.Log($"[PlayerHealth] Player took {damageAmount} damage. Current health: {currentHealth}/{maxHealth}");

        // Check for death
        if (currentHealth <= 0)
        {
            HandleDeath(damageDealer);
        }
    }

    /// <summary>
    /// Update the health bar and text UI
    /// </summary>
    private void UpdateHealthUI()
    {
        // Update health bar fill amount (0-1 range)
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = HealthPercentage;
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] Health bar fill reference is missing!");
        }

        // Update health text
        if (healthText != null)
        {
            healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}";
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] Health text reference is missing!");
        }
    }

    /// <summary>
    /// Handle player death
    /// </summary>
    private void HandleDeath(GameObject killer = null)
    {
        Debug.Log($"[PlayerHealth] Player has died! Killed by: {(killer != null ? killer.name : "unknown")}");

        // Notify managers about the kill (ScoreManager and RespawnManager will both listen)
        OnKilled?.Invoke(gameObject, killer);

        // RespawnManager will handle the respawn process
        // No need to disable controls here as RespawnManager does it
    }

    /// <summary>
    /// Heal the player (useful for testing or power-ups later)
    /// </summary>
    /// <param name="healAmount">Amount to heal</param>
    public void Heal(float healAmount)
    {
        if (IsDead) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateHealthUI();

        Debug.Log($"[PlayerHealth] Player healed for {healAmount}. Current health: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Reset health to full (for respawn)
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();

        Debug.Log("[PlayerHealth] Player health reset to full");
    }

    /// <summary>
    /// Set health to a specific value
    /// </summary>
    /// <param name="newHealth">New health value</param>
    public void SetHealth(float newHealth)
    {
        currentHealth = Mathf.Clamp(newHealth, 0f, maxHealth);
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            HandleDeath();
        }

        Debug.Log($"[PlayerHealth] Player health set to: {currentHealth}/{maxHealth}");
    }
}