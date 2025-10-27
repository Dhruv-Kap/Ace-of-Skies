using UnityEngine;

public class AIM7Missile : MonoBehaviour
{
    [Header("AIM-7 Sparrow Specifications")]
    [SerializeField] private float speed = 850f; // Between AIM-9 and AIM-120
    [SerializeField] private float lifetime = 25f; // Medium range
    [SerializeField] private float damage = 45f; // Between AIM-9 and AIM-120
    [SerializeField] private float explosionRadius = 8f; // Medium blast radius

    [Header("Guidance System")]
    [SerializeField] private float lockOnDelay = 2.5f;
    [SerializeField] private float seekerConeAngle = 35f;
    [SerializeField] private float totalTurnBudget = 600f; // Between AIM-9 and AIM-120
    [SerializeField] private float maxTurnRatePerSecond = 20f; // Medium agility

    [Header("Semi-Active Radar")]
    [SerializeField] private float radarIlluminationConeAngle = 30f; // Plane must keep pointing at target
    [SerializeField] private float illuminationCheckInterval = 0.2f; // How often to check if plane is illuminating

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
    private bool isPlaneIlluminating = false;

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
                    Debug.Log($"[AIM-7] Missile acquired target: {target.name}");
            }
            else if (showDebug)
            {
                Debug.Log("[AIM-7] Missile fired without target - going ballistic");
            }
        }
        else if (showDebug && target != null)
        {
            Debug.Log($"[AIM-7] Target set directly: {target.name}");
        }

        Destroy(gameObject, lifetime);
        Invoke(nameof(ActivateTracking), lockOnDelay);
        StartCoroutine(CheckRadarIlluminationCoroutine());
    }

    void ActivateTracking()
    {
        if (target != null)
        {
            isLocked = true;
            if (showDebug)
                Debug.Log("[AIM-7] Semi-active radar lock-on complete");
        }
    }

    System.Collections.IEnumerator CheckRadarIlluminationCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(illuminationCheckInterval);
            CheckPlaneIllumination();
        }
    }

    void CheckPlaneIllumination()
    {
        if (launcher == null || target == null)
        {
            isPlaneIlluminating = false;
            return;
        }

        // Check if launcher is pointing at target
        Vector3 launcherToTarget = (target.transform.position - launcher.transform.position).normalized;
        float angle = Vector3.Angle(launcher.transform.forward, launcherToTarget);

        bool wasIlluminating = isPlaneIlluminating;
        isPlaneIlluminating = angle <= radarIlluminationConeAngle;

        // Debug status changes
        if (showDebug && wasIlluminating != isPlaneIlluminating)
        {
            if (isPlaneIlluminating)
                Debug.Log("[AIM-7] Radar illumination ACQUIRED - missile can track");
            else
                Debug.Log("[AIM-7] Radar illumination LOST - missile going ballistic");
        }

        // Update tracking status based on illumination
        if (isLocked)
        {
            if (isPlaneIlluminating && !isTracking)
            {
                // Reacquiring target - costs turn budget naturally through GuideToTarget
                isTracking = true;
                if (showDebug)
                    Debug.Log("[AIM-7] Reacquiring target");
            }
            else if (!isPlaneIlluminating && isTracking)
            {
                // Lost illumination - go ballistic
                isTracking = false;
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Debug missile movement
        if (showDebug && Time.frameCount % 30 == 0)
        {
            string status = isTracking ? "TRACKING" : "BALLISTIC";
            string illumination = isPlaneIlluminating ? "ILLUMINATED" : "NO ILLUMINATION";
            Debug.Log($"[AIM-7] Status: {status}, {illumination}, Vel: {rb.linearVelocity.magnitude:F1} m/s, Target: {(target != null ? target.name : "NULL")}");
            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                Debug.Log($"[AIM-7] Distance to target: {dist:F1}m, Turn budget: {turnBudgetRemaining:F1}°");
            }
        }

        // Track target only if plane is illuminating and we have turn budget
        if (isTracking && target != null && isPlaneIlluminating && turnBudgetRemaining > 0)
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
                Debug.Log($"[AIM-7] Target left seeker cone ({angleToTarget:F1}° > {seekerConeAngle}°) - going ballistic");
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
                Debug.Log("[AIM-7] Turn budget exhausted - going ballistic");
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
            Debug.Log($"[AIM-7] Exploding! Hit: {(hitObject != null ? hitObject.name : "nothing")}");

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
                        Debug.Log($"[AIM-7] Direct hit! Dealing FULL damage {damage} to {obj.name}");
                }
                else
                {
                    // Splash damage with falloff
                    Vector3 closest = obj.ClosestPoint(transform.position);
                    float distance = Vector3.Distance(transform.position, closest);
                    float damageFalloff = 1f - (distance / explosionRadius);
                    finalDamage = damage * Mathf.Clamp01(damageFalloff);
                    if (showDebug)
                        Debug.Log($"[AIM-7] Proximity splash: {finalDamage:F1} to {obj.name} (distance: {distance:F1})");
                }

                if (finalDamage > 0f)
                    damageable.TakeDamage(finalDamage, launcher);
            }

            // Apply explosion force
            Rigidbody targetRb = obj.GetComponent<Rigidbody>();
            if (targetRb != null)
                targetRb.AddExplosionForce(damage * 65f, transform.position, explosionRadius);
        }

        Destroy(gameObject);
    }

    public void SetTarget(GameObject targetObject) => target = targetObject;
    public void SetLauncher(GameObject launcherObject) => launcher = launcherObject;
}