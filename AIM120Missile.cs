using UnityEngine;

public class AIM120Missile : MonoBehaviour
{
    [Header("AIM-120 AMRAAM Specifications")]
    [SerializeField] private float speed = 1200f; // Much faster than AIM-9
    [SerializeField] private float lifetime = 35f; // Longer range/time
    [SerializeField] private float damage = 65f; // Higher damage than AIM-9
    [SerializeField] private float explosionRadius = 10f; // Larger blast radius

    [Header("Guidance System")]
    [SerializeField] private float lockOnDelay = 3f; // Slightly longer lock time
    [SerializeField] private float seekerConeAngle = 45f; // Wider cone, but less agile
    [SerializeField] private float totalTurnBudget = 480f; // Less maneuverable than AIM-9
    [SerializeField] private float maxTurnRatePerSecond = 15f; // Degrees per second - less agile
    [SerializeField] private float targetSearchInterval = 0.3f; // How often to search for targets

    [Header("Effects")]
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private AudioClip explosionSound;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private GameObject target;
    private Rigidbody rb;
    private float launchTime;
    private bool isLocked = false;
    private bool isTracking = false;
    private float turnBudgetRemaining;
    private GameObject launcher;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        launchTime = Time.time;
        turnBudgetRemaining = totalTurnBudget;

        // Set initial velocity
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = transform.forward * speed;
        }

        // Reduce immediate collisions with the launcher
        if (launcher != null)
        {
            Collider missileCollider = GetComponent<Collider>();
            Collider[] launcherColliders = launcher.GetComponentsInChildren<Collider>();
            foreach (Collider col in launcherColliders)
            {
                Physics.IgnoreCollision(missileCollider, col, true);
            }
        }

        // Play launch sound
        if (audioSource != null && launchSound != null)
        {
            audioSource.PlayOneShot(launchSound);
        }

        // Try to acquire target from AimAssist
        if (target == null)
        {
            AimAssist aimAssist = FindFirstObjectByType<AimAssist>();
            if (aimAssist != null && aimAssist.HasLockedTarget())
            {
                target = aimAssist.GetLockedTarget();
                if (showDebug)
                    Debug.Log($"[AIM-120] Missile acquired target: {target.name}");
            }
            else if (showDebug)
            {
                Debug.Log("[AIM-120] Missile fired without target - going ballistic");
            }
        }
        else if (showDebug && target != null)
        {
            Debug.Log($"[AIM-120] Target set directly: {target.name}");
        }

        Destroy(gameObject, lifetime);
        Invoke(nameof(ActivateTracking), lockOnDelay);
        StartCoroutine(SearchForTargetsCoroutine());
    }

    void ActivateTracking()
    {
        if (target != null)
        {
            isLocked = true;
            isTracking = true;
            if (showDebug)
                Debug.Log("[AIM-120] Active radar lock-on complete - tracking active");
        }
    }

    System.Collections.IEnumerator SearchForTargetsCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(targetSearchInterval);

            // Only search if we don't have a target
            if (target == null)
            {
                SearchForTarget();
            }
        }
    }

    void SearchForTarget()
    {
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemy in allEnemies)
        {
            if (enemy != null)
            {
                Vector3 directionToEnemy = (enemy.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, directionToEnemy);

                if (angle <= seekerConeAngle)
                {
                    target = enemy;
                    isLocked = true;
                    isTracking = true;
                    if (showDebug)
                        Debug.Log($"[AIM-120] Target acquired in flight: {enemy.name}");
                    break;
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Debug missile movement
        if (showDebug && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[AIM-120] Pos: {transform.position}, Vel: {rb.linearVelocity.magnitude:F1} m/s, Target: {(target != null ? target.name : "NULL")}, Tracking: {isTracking}");
            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                Debug.Log($"[AIM-120] Distance to target: {dist:F1}m, Explosion radius: {explosionRadius}m");
            }
        }

        // Track target or go ballistic
        if (isTracking && target != null && turnBudgetRemaining > 0)
        {
            GuideToTarget();
        }
        else
        {
            rb.linearVelocity = transform.forward * speed;
        }

        // Maintain speed
        if (rb.linearVelocity.magnitude < speed * 0.9f)
            rb.linearVelocity = rb.linearVelocity.normalized * speed;

        // Check proximity for explosion
        if (target != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
            if (distanceToTarget <= explosionRadius)
            {
                Explode(target);
            }
        }
    }

    void GuideToTarget()
    {
        if (target == null || rb == null) return;

        Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

        // Check if target is within seeker cone
        if (angleToTarget > seekerConeAngle)
        {
            isTracking = false;
            if (showDebug)
            {
                Debug.Log($"[AIM-120] Target left seeker cone ({angleToTarget:F1}° > {seekerConeAngle}°) - going ballistic");
            }
            return;
        }

        // Calculate maximum turn this frame based on turn rate limit
        float maxTurnThisFrame = maxTurnRatePerSecond * Time.fixedDeltaTime;
        float desiredTurn = Mathf.Min(angleToTarget, maxTurnThisFrame);

        // Check turn budget
        if (desiredTurn > turnBudgetRemaining)
        {
            isTracking = false;
            if (showDebug)
            {
                Debug.Log("[AIM-120] Turn budget exhausted - going ballistic");
            }
            return;
        }

        // Apply turn
        Vector3 newDirection = Vector3.RotateTowards(
            transform.forward,
            directionToTarget,
            Mathf.Deg2Rad * desiredTurn,
            0f
        );

        turnBudgetRemaining -= desiredTurn;
        transform.rotation = Quaternion.LookRotation(newDirection);
        rb.linearVelocity = transform.forward * speed;
    }

    void OnTriggerEnter(Collider other)
    {
        // Ignore launcher
        if (launcher != null && other.GetComponentInParent<Transform>().root == launcher.transform.root)
            return;

        Explode(other.gameObject, other);
    }

    void Explode(GameObject hitObject = null, Collider directHitCollider = null)
    {
        if (showDebug)
            Debug.Log($"[AIM-120] Exploding! Hit: {(hitObject != null ? hitObject.name : "nothing")}");

        if (explosionEffect != null)
        {
            GameObject explosion = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(explosion, 3f);
        }

        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);

        Collider[] objectsInRange = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider obj in objectsInRange)
        {
            // Exclude launcher's whole root object
            if (launcher != null && obj.GetComponentInParent<Transform>().root == launcher.transform.root)
                continue;

            IDamageable damageable = obj.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                float finalDamage = 0f;

                // Direct hit: full damage
                if (directHitCollider != null && obj == directHitCollider)
                {
                    finalDamage = damage;
                    if (showDebug)
                        Debug.Log($"[AIM-120] Direct hit! Dealing FULL damage {damage} to {obj.name}");
                }
                else
                {
                    // Splash damage with falloff
                    Vector3 closest = obj.ClosestPoint(transform.position);
                    float distance = Vector3.Distance(transform.position, closest);
                    float damageFalloff = 1f - (distance / explosionRadius);
                    finalDamage = damage * Mathf.Clamp01(damageFalloff);
                    if (showDebug)
                        Debug.Log($"[AIM-120] Proximity splash: {finalDamage:F1} to {obj.name} (distance: {distance:F1})");
                }

                if (finalDamage > 0f)
                    damageable.TakeDamage(finalDamage, launcher);
            }

            // Apply explosion force
            Rigidbody targetRb = obj.GetComponent<Rigidbody>();
            if (targetRb != null)
                targetRb.AddExplosionForce(damage * 75f, transform.position, explosionRadius);
        }

        Destroy(gameObject);
    }

    public void SetTarget(GameObject targetObject) => target = targetObject;
    public void SetLauncher(GameObject launcherObject) => launcher = launcherObject;
}