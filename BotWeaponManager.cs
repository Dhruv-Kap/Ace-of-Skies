using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotWeaponManager : MonoBehaviour
{
    [Header("Targeting Configuration")]
    [SerializeField] private string[] hostileTags = { "Player", "Enemy" };
    [SerializeField] private float searchRange = 15000f;
    [SerializeField] private float searchInterval = 0.5f;

    [Header("Missile Employment Rules")]
    [SerializeField] private MissileEmploymentRule[] employmentRules;

    [Header("Missile Pylons - SAME AS PLAYER")]
    public MissilePylon[] missilePylons = new MissilePylon[3];

    [Header("Missile Prefabs (index == MissileType)")]
    public GameObject[] missilePrefabs = new GameObject[3]; // IR, SARH, ARH

    [Header("Firing Behavior")]
    [SerializeField] private bool autoFireEnabled = true;
    [SerializeField] private float minEngagementRange = 500f; // Don't fire if too close
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask losBlockingLayers; // Terrain/obstacles

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Internal state
    private GameObject currentTarget;
    private Dictionary<MissileType, MissileEmploymentRule> rulesByType;
    private int lastPylonFired = -1;

    void Awake()
    {
        // Build lookup dictionary for employment rules
        rulesByType = new Dictionary<MissileType, MissileEmploymentRule>();
        if (employmentRules != null)
        {
            foreach (var rule in employmentRules)
            {
                if (!rulesByType.ContainsKey(rule.missileType))
                {
                    rulesByType.Add(rule.missileType, rule);
                }
            }
        }
    }

    void OnEnable()
    {
        StartCoroutine(CombatLoop());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator CombatLoop()
    {
        var wait = new WaitForSeconds(searchInterval);

        while (true)
        {
            UpdateTarget();

            if (autoFireEnabled && currentTarget != null)
            {
                TryEngageTarget();
            }

            yield return wait;
        }
    }

    void UpdateTarget()
    {
        // Validate existing target
        if (currentTarget != null)
        {
            if (!currentTarget.activeInHierarchy)
            {
                if (showDebug) Debug.Log($"[BotWeapon] Lost target: {currentTarget.name} (inactive)");
                currentTarget = null;
                return;
            }

            float sqrDist = (currentTarget.transform.position - transform.position).sqrMagnitude;
            if (sqrDist > searchRange * searchRange)
            {
                if (showDebug) Debug.Log($"[BotWeapon] Lost target: {currentTarget.name} (out of range)");
                currentTarget = null;
            }
        }

        // If we still have a valid target, keep it
        if (currentTarget != null) return;

        // Search for new target
        GameObject bestTarget = null;
        float bestPriority = float.PositiveInfinity;

        foreach (var tag in hostileTags)
        {
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(tag);

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate == gameObject) continue;
                if (!candidate.activeInHierarchy) continue;

                float sqrDist = (candidate.transform.position - transform.position).sqrMagnitude;

                // Within search range?
                if (sqrDist > searchRange * searchRange) continue;

                // Prioritize closer targets
                if (sqrDist < bestPriority)
                {
                    bestTarget = candidate;
                    bestPriority = sqrDist;
                }
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            if (showDebug) Debug.Log($"[BotWeapon] Target acquired: {currentTarget.name} at {Mathf.Sqrt(bestPriority):F0}m");
        }
    }

    void TryEngageTarget()
    {
        if (currentTarget == null) return;

        // Calculate target parameters
        Vector3 toTarget = currentTarget.transform.position - transform.position;
        float distance = toTarget.magnitude;
        float angle = Vector3.Angle(transform.forward, toTarget);

        if (showDebug)
        {
            Debug.Log($"[BotWeapon] Engaging {currentTarget.name}: Dist={distance:F0}m, Angle={angle:F1}°");
        }

        // Try each pylon in rotation
        int attempts = missilePylons.Length;
        for (int i = 0; i < attempts; i++)
        {
            int pylonIndex = (lastPylonFired + 1 + i) % missilePylons.Length;

            if (TryFirePylon(pylonIndex, distance, angle))
            {
                lastPylonFired = pylonIndex;
                return; // Successfully fired
            }
        }

        if (showDebug)
        {
            Debug.Log($"[BotWeapon] No suitable weapon available for target at {distance:F0}m, {angle:F1}°");
        }
    }

    bool TryFirePylon(int pylonIndex, float targetDistance, float targetAngle)
    {
        // Validate pylon index
        if (pylonIndex < 0 || pylonIndex >= missilePylons.Length)
            return false;

        MissilePylon pylon = missilePylons[pylonIndex];

        // Check basic pylon state
        if (pylon == null)
        {
            if (showDebug) Debug.Log($"[BotWeapon] Pylon {pylonIndex} is null");
            return false;
        }

        if (!pylon.HasMissiles())
        {
            if (showDebug) Debug.Log($"[BotWeapon] Pylon {pylonIndex} empty");
            return false;
        }

        if (pylon.IsOnCooldown())
        {
            if (showDebug) Debug.Log($"[BotWeapon] Pylon {pylonIndex} on cooldown ({pylon.GetCooldownRemaining():F1}s)");
            return false;
        }

        // Check employment rules for this missile type
        if (!rulesByType.TryGetValue(pylon.missileType, out var rule))
        {
            if (showDebug) Debug.LogWarning($"[BotWeapon] No employment rule for {pylon.missileType}");
            return false;
        }

        // Check range envelope
        if (targetDistance < rule.minRange || targetDistance > rule.maxRange)
        {
            if (showDebug) Debug.Log($"[BotWeapon] Pylon {pylonIndex} ({pylon.missileType}) out of range: {targetDistance:F0}m (need {rule.minRange}-{rule.maxRange}m)");
            return false;
        }

        // Check angle envelope
        if (targetAngle > rule.maxOffBoresight)
        {
            if (showDebug) Debug.Log($"[BotWeapon] Pylon {pylonIndex} ({pylon.missileType}) off-boresight: {targetAngle:F1}° (max {rule.maxOffBoresight}°)");
            return false;
        }

        // Check line of sight if required
        if (requireLineOfSight && !CheckLineOfSight())
        {
            if (showDebug) Debug.Log($"[BotWeapon] Pylon {pylonIndex} blocked by terrain");
            return false;
        }

        // All checks passed - FIRE!
        return FireMissile(pylonIndex, pylon);
    }

    bool CheckLineOfSight()
    {
        if (currentTarget == null) return false;

        Vector3 origin = transform.position;
        Vector3 direction = (currentTarget.transform.position - origin).normalized;
        float distance = Vector3.Distance(origin, currentTarget.transform.position);

        // Check if terrain/obstacles block the shot
        return !Physics.Raycast(origin, direction, distance, losBlockingLayers);
    }

    bool FireMissile(int pylonIndex, MissilePylon pylon)
    {
        // Get the visual missile to fire
        Transform missileTransform = pylon.GetNextMissileToFire();
        if (missileTransform == null)
        {
            if (showDebug) Debug.LogError($"[BotWeapon] No missile transform on pylon {pylonIndex}");
            return false;
        }

        // Store spawn position/rotation before destroying visual
        Vector3 spawnPosition = missileTransform.position;
        Quaternion spawnRotation = missileTransform.rotation;

        // Remove visual missile from rail
        Destroy(missileTransform.gameObject);

        // Spawn live missile
        int prefabIndex = (int)pylon.missileType;
        if (prefabIndex < 0 || prefabIndex >= missilePrefabs.Length || missilePrefabs[prefabIndex] == null)
        {
            if (showDebug) Debug.LogError($"[BotWeapon] Missing prefab for {pylon.missileType}");
            return false;
        }

        GameObject liveMissile = Instantiate(missilePrefabs[prefabIndex], spawnPosition, spawnRotation);

        // Configure missile
        AIM9Missile missileScript = liveMissile.GetComponent<AIM9Missile>();
        if (missileScript != null)
        {
            missileScript.SetLauncher(gameObject);
            missileScript.SetTarget(currentTarget);
        }
        else
        {
            if (showDebug) Debug.LogWarning($"[BotWeapon] Missile prefab missing AIM9Missile component");
        }

        // Ignore collision with launcher
        Collider missileCollider = liveMissile.GetComponent<Collider>();
        if (missileCollider != null)
        {
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(missileCollider, col, true);
            }
        }

        // Update pylon state
        pylon.FireMissile();

        if (showDebug)
        {
            Debug.Log($"[BotWeapon] FIRED {pylon.missileType} from pylon {pylonIndex} at {currentTarget.name}");
        }

        return true;
    }

    // Public API
    public GameObject GetCurrentTarget() => currentTarget;

    public bool HasTarget() => currentTarget != null;

    public void SetAutoFire(bool enabled)
    {
        autoFireEnabled = enabled;
    }

    // Draw target info in scene view
    void OnDrawGizmosSelected()
    {
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            Gizmos.DrawWireSphere(currentTarget.transform.position, 50f);
        }

        // Draw search range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRange);
    }
}

[System.Serializable]
public class MissileEmploymentRule
{
    [Header("Missile Type")]
    public MissileType missileType;

    [Header("Employment Envelope")]
    public float minRange = 500f;
    public float maxRange = 8000f;
    public float maxOffBoresight = 30f; // Max angle off nose

    [Header("Notes")]
    [TextArea(2, 3)]
    public string notes = "Define when this missile can be fired";
}