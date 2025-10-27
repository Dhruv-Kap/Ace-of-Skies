using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Aircraft Carrier Resupply Zone - Attach to a separate GameObject with trigger collider
/// Restores health instantly and gradually reloads ammo/missiles when aircraft enter
/// SETUP: Create empty GameObject -> Add Cylinder Collider (Is Trigger = true) -> Add this script
/// </summary>
public class CarrierResupplyZone : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // Store original pylon positions
    private Dictionary<Transform, Vector3> originalPylonPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalPylonRotations = new Dictionary<Transform, Quaternion>();

    void Start()
    {
        // Verify setup
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[ResupplyZone] No collider found! Add a Collider component and set 'Is Trigger' to true.");
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("[ResupplyZone] Collider is not a trigger! Set 'Is Trigger' to true in the collider component.");
        }
        else if (showDebugInfo)
        {
            Debug.Log("[ResupplyZone] Resupply zone active and ready!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if it's an aircraft (player or bot)
        // Try PlayerHealth first (for player), then Health (for bots)
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        }

        Health botHealth = other.GetComponent<Health>();
        if (botHealth == null)
        {
            botHealth = other.GetComponentInParent<Health>();
        }

        // If we found either health component
        if (playerHealth != null || botHealth != null)
        {
            // Get reload system
            AmmoReloadSystem reloadSystem = other.GetComponent<AmmoReloadSystem>();
            if (reloadSystem == null)
            {
                reloadSystem = other.GetComponentInParent<AmmoReloadSystem>();
            }

            bool needsResupply = false;

            // Check health (different methods for player vs bot)
            if (playerHealth != null)
            {
                if (playerHealth.CurrentHealth < playerHealth.MaxHealth)
                {
                    needsResupply = true;
                }
            }
            else if (botHealth != null)
            {
                if (botHealth.GetCurrentHealth() < botHealth.GetMaxHealth())
                {
                    needsResupply = true;
                }
            }

            // Check ammo
            if (reloadSystem != null && reloadSystem.NeedsResupply())
            {
                needsResupply = true;
            }

            if (needsResupply)
            {
                // STORE ORIGINAL PYLON POSITIONS BEFORE RESUPPLY
                StorePylonPositions(other.transform);

                // Restore health instantly (different methods for player vs bot)
                if (playerHealth != null)
                {
                    playerHealth.SetHealth(playerHealth.MaxHealth);
                }
                else if (botHealth != null)
                {
                    botHealth.ResetHealth();
                }

                // Start gradual ammo reload
                if (reloadSystem != null)
                {
                    reloadSystem.StartResupply();
                }

                if (showDebugInfo)
                {
                    string aircraftType = playerHealth != null ? "Player" : "Bot";
                    Debug.Log($"[ResupplyZone] {aircraftType} {other.name} entered resupply zone - Health restored, ammo reloading...");
                }
            }
            else if (showDebugInfo)
            {
                Debug.Log($"[ResupplyZone] {other.name} entered but doesn't need resupply");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // RESTORE ORIGINAL PYLON POSITIONS WHEN LEAVING
        RestorePylonPositions(other.transform);
    }

    void StorePylonPositions(Transform aircraft)
    {
        // Find all pylon transforms in the aircraft
        Transform[] allTransforms = aircraft.GetComponentsInChildren<Transform>();

        originalPylonPositions.Clear();
        originalPylonRotations.Clear();

        foreach (Transform t in allTransforms)
        {
            if (t.name.Contains("MissilePylon") || t.name.Contains("MPP"))
            {
                originalPylonPositions[t] = t.localPosition;
                originalPylonRotations[t] = t.localRotation;

                if (showDebugInfo)
                    Debug.Log($"[ResupplyZone] Stored position for {t.name}: {t.localPosition}");
            }
        }
    }

    void RestorePylonPositions(Transform aircraft)
    {
        // Restore all stored pylon positions
        foreach (var kvp in originalPylonPositions)
        {
            if (kvp.Key != null)
            {
                kvp.Key.localPosition = kvp.Value;

                if (showDebugInfo)
                    Debug.Log($"[ResupplyZone] Restored position for {kvp.Key.name}: {kvp.Value}");
            }
        }

        foreach (var kvp in originalPylonRotations)
        {
            if (kvp.Key != null)
            {
                kvp.Key.localRotation = kvp.Value;
            }
        }

        originalPylonPositions.Clear();
        originalPylonRotations.Clear();
    }

    void OnDrawGizmos()
    {
        // Draw the trigger zone in the editor
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Translucent green
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider)
            {
                BoxCollider box = col as BoxCollider;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider)
            {
                SphereCollider sphere = col as SphereCollider;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (col is CapsuleCollider)
            {
                CapsuleCollider capsule = col as CapsuleCollider;
                // Draw approximate capsule
                Gizmos.DrawSphere(capsule.center + Vector3.up * capsule.height * 0.5f, capsule.radius);
                Gizmos.DrawSphere(capsule.center - Vector3.up * capsule.height * 0.5f, capsule.radius);
            }
        }
    }
}
