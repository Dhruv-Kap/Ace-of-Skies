using UnityEngine;
using System.Collections;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Respawn Settings (for bots)")]
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private float respawnDelay = 5f;

    [Header("Death Effects (Optional)")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private float deathEffectDuration = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // Events for managers to listen to
    public System.Action<GameObject, GameObject> OnKilled; // (victim, killer)
    public System.Action<GameObject> OnRespawned; // (respawned object)

    private bool isDead = false;
    private RespawnManager respawnManager;

    void Start()
    {
        // Initialize health to maximum
        currentHealth = maxHealth;

        // Find RespawnManager
        respawnManager = FindFirstObjectByType<RespawnManager>();
        if (respawnManager == null)
        {
            Debug.LogError($"[Health] RespawnManager not found for {gameObject.name}! Bot respawning will not work!");
        }
        else if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} found RespawnManager successfully");
        }
    }

    // IDamageable interface implementation
    public void TakeDamage(float damageAmount)
    {
        TakeDamageInternal(damageAmount, null);
    }

    public void TakeDamage(float damageAmount, GameObject damageDealer)
    {
        TakeDamageInternal(damageAmount, damageDealer);
    }

    private void TakeDamageInternal(float damageAmount, GameObject damageDealer)
    {
        if (isDead) return; // Already dead, ignore damage

        // Reduce health
        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0f, currentHealth); // Don't go below 0

        if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} took {damageAmount} damage from {(damageDealer != null ? damageDealer.name : "unknown")}. Health: {currentHealth}/{maxHealth}");
        }

        // Check if dead
        if (currentHealth <= 0f)
        {
            Die(damageDealer);
        }
    }

    void Die(GameObject killer = null)
    {
        if (isDead) return; // Prevent multiple death calls

        isDead = true;

        if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} has been killed by {(killer != null ? killer.name : "unknown")}!");
        }

        // Notify managers about the kill
        OnKilled?.Invoke(gameObject, killer);

        // Spawn death effect if assigned
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, transform.rotation);
            Destroy(effect, deathEffectDuration);
        }

        // Check if this object can respawn (bots)
        if (canRespawn)
        {
            if (respawnManager != null)
            {
                // For bots - start respawn process
                if (showDebugInfo)
                {
                    Debug.Log($"[Health] {gameObject.name} starting respawn process...");
                }
                StartCoroutine(RespawnAfterDelay());
            }
            else
            {
                Debug.LogError($"[Health] {gameObject.name} can't respawn - no RespawnManager found!");
                Destroy(gameObject);
            }
        }
        else
        {
            // If can't respawn, destroy the object
            if (showDebugInfo)
            {
                Debug.Log($"[Health] {gameObject.name} set to not respawn - destroying object");
            }
            Destroy(gameObject);
        }
    }

    IEnumerator RespawnAfterDelay()
    {
        // Hide/disable the object during respawn delay
        SetObjectActive(false);

        if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} disabled, will respawn in {respawnDelay} seconds");
        }

        // Wait for respawn delay
        yield return new WaitForSeconds(respawnDelay);

        // Check if RespawnManager still exists
        if (respawnManager == null)
        {
            Debug.LogError($"[Health] {gameObject.name} - RespawnManager missing during respawn!");
            Destroy(gameObject);
            yield break;
        }

        // Respawn
        RespawnBot();
    }

    void RespawnBot()
    {
        if (respawnManager == null)
        {
            Debug.LogError($"[Health] {gameObject.name} - RespawnManager is null in RespawnBot()!");
            return;
        }

        // Get random respawn position from RespawnManager
        Vector3 respawnPosition = respawnManager.GetRandomBotRespawnPosition();

        if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} respawning at position: {respawnPosition}");
        }

        // Move to respawn position
        transform.position = respawnPosition;

        // Reset health
        currentHealth = maxHealth;
        isDead = false;

        // Reactivate object
        SetObjectActive(true);

        // Notify managers
        OnRespawned?.Invoke(gameObject);

        if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} successfully respawned at {respawnPosition}");
        }
    }

    void SetObjectActive(bool active)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} SetObjectActive({active})");
        }

        // Disable/enable renderer and collider instead of the whole gameObject
        // This way scripts keep running but object becomes invisible/non-collidable

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = active;
            if (showDebugInfo)
            {
                Debug.Log($"[Health] {gameObject.name} renderer {(active ? "enabled" : "disabled")}");
            }
        }

        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = active;
            if (showDebugInfo)
            {
                Debug.Log($"[Health] {gameObject.name} collider {(active ? "enabled" : "disabled")}");
            }
        }

        // Also disable any weapons/firing scripts when dead
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != this && (script.GetType().Name.Contains("Gun") || script.GetType().Name.Contains("Fire")))
            {
                script.enabled = active;
                if (showDebugInfo)
                {
                    Debug.Log($"[Health] {gameObject.name} script {script.GetType().Name} {(active ? "enabled" : "disabled")}");
                }
            }
        }
    }

    // Method to reset health (for external use)
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        SetObjectActive(true);

        if (showDebugInfo)
        {
            Debug.Log($"[Health] {gameObject.name} health reset to full");
        }
    }

    // Public getters for other scripts
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => currentHealth / maxHealth;
    public bool IsAlive() => !isDead;

    // Implement IDamageable
    public bool IsDead => isDead;
}