using UnityEngine;

public class AIM9Missile : MonoBehaviour
{
    [Header("AIM-9 Specifications")]
    [SerializeField] private float speed = 566f;
    [SerializeField] private float lifetime = 15f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float explosionRadius = 6f;

    [Header("Guidance System")]
    [SerializeField] private float lockOnDelay = 2f;
    [SerializeField] private float seekerConeAngle = 30f;
    [SerializeField] private float totalTurnBudget = 720f;

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

        // Reduce immediate collisions with the launcher (player or bot)
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

        // If the target wasn't set externally, try to grab from AimAssist
        if (target == null)
        {
            AimAssist aimAssist = FindFirstObjectByType<AimAssist>();
            if (aimAssist != null && aimAssist.HasLockedTarget())
            {
                target = aimAssist.GetLockedTarget();
                if (showDebug)
                    Debug.Log($"[AIM-9] Missile acquired target: {target.name}");
            }
            else if (showDebug)
            {
                Debug.Log("[AIM-9] Missile fired without target - going ballistic");
            }
        }
        else if (showDebug && target != null)
        {
            Debug.Log($"[AIM-9] Target set directly: {target.name}");
        }

        Destroy(gameObject, lifetime); // Self-destruct after lifetime
        Invoke(nameof(ActivateTracking), lockOnDelay); // Lock-on sequence
    }

    void ActivateTracking()
    {
        if (target != null)
        {
            isLocked = true;
            isTracking = true;
            if (showDebug)
                Debug.Log("[AIM-9] Lock-on complete - tracking active");
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Debug missile movement
        if (showDebug && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[AIM-9] Pos: {transform.position}, Vel: {rb.linearVelocity.magnitude:F1} m/s, Target: {(target != null ? target.name : "NULL")}, Tracking: {isTracking}");
            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                Debug.Log($"[AIM-9] Distance to target: {dist:F1}m, Explosion radius: {explosionRadius}m");
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

        if (angleToTarget > seekerConeAngle)
        {
            isTracking = false;
            if (showDebug)
            {
                Debug.Log($"[AIM-9] Target left seeker cone ({angleToTarget:F1}° > {seekerConeAngle}°) - going ballistic");
            }
            return;
        }

        Vector3 newDirection = Vector3.RotateTowards(
            transform.forward,
            directionToTarget,
            Mathf.Deg2Rad * angleToTarget,
            0f
        );
        float turnThisFrame = Vector3.Angle(transform.forward, newDirection);

        if (turnThisFrame > turnBudgetRemaining)
        {
            isTracking = false;
            if (showDebug)
            {
                Debug.Log("[AIM-9] Turn budget exhausted - going ballistic");
            }
            return;
        }

        turnBudgetRemaining -= turnThisFrame;
        transform.rotation = Quaternion.LookRotation(newDirection);
        rb.linearVelocity = transform.forward * speed;
    }

    void OnTriggerEnter(Collider other)
    {
        // Exclude launcher checks here as before...
        if (launcher != null && other.GetComponentInParent<Transform>().root == launcher.transform.root)
            return;

        // Remember which collider was hit directly
        Explode(other.gameObject, other);
    }

    void Explode(GameObject hitObject = null, Collider directHitCollider = null)
    {
        if (showDebug)
            Debug.Log($"[AIM-9] Exploding! Hit: {(hitObject != null ? hitObject.name : "nothing")}");

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

                // Direct hit: if this collider matches directHitCollider
                if (directHitCollider != null && obj == directHitCollider)
                {
                    finalDamage = damage; // Full damage on direct collision
                    if (showDebug)
                        Debug.Log($"[AIM-9] Direct hit! Dealing FULL damage {damage} to {obj.name}");
                }
                else
                {
                    Vector3 closest = obj.ClosestPoint(transform.position);
                    float distance = Vector3.Distance(transform.position, closest);
                    float damageFalloff = 1f - (distance / explosionRadius);
                    finalDamage = damage * Mathf.Clamp01(damageFalloff);
                    if (showDebug)
                        Debug.Log($"[AIM-9] Proximity splash: {finalDamage:F1} to {obj.name} (distance: {distance:F1})");
                }

                if (finalDamage > 0f)
                    damageable.TakeDamage(finalDamage, launcher);
            }

            Rigidbody targetRb = obj.GetComponent<Rigidbody>();
            if (targetRb != null)
                targetRb.AddExplosionForce(damage * 50f, transform.position, explosionRadius);
        }
        Destroy(gameObject);
    }


    public void SetTarget(GameObject targetObject) => target = targetObject;
    public void SetLauncher(GameObject launcherObject) => launcher = launcherObject;
}
