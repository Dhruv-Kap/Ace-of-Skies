using UnityEngine;
using System.Collections;
using System.Linq;

public class F16BotGunFire : MonoBehaviour
{
    [Header("Gun Settings")]
    [SerializeField] private Transform gunFirePoint;
    [SerializeField] private float damage = 25f;

    [Header("Burst Fire Settings")]
    [SerializeField] private int burstSize = 3;
    [SerializeField] private float burstFireRate = 0.1f;
    [SerializeField] private float burstCooldown = 2f;

    [Header("Target Settings")]
    [SerializeField] private float maxEngagementRange = 800f;
    [SerializeField] private float minEngagementRange = 50f;
    [SerializeField] private float maxFiringAngle = 30f;

    [Header("Visual Effects (Optional)")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private LineRenderer tracerLine;
    [SerializeField] private float tracerDuration = 0.1f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private GameObject currentTarget;
    private float lastBurstTime;
    private bool canFire = true;
    private bool isBurstFiring = false;
    private int currentBurstCount = 0;

    void Start()
    {
        if (tracerLine != null) tracerLine.enabled = false;
        lastBurstTime = Time.time;

        // Start looking for targets
        InvokeRepeating(nameof(FindTarget), 0f, 0.5f);
    }

    void Update()
    {
        if (currentTarget != null && canFire && !isBurstFiring)
        {
            TryStartBurstFire();
        }
    }

    void FindTarget()
    {
        // Search both players and bots
        var potentialTargets = GameObject.FindGameObjectsWithTag("Player")
            .Concat(GameObject.FindGameObjectsWithTag("Enemy"))
            .ToArray();

        if (potentialTargets.Length == 0)
        {
            currentTarget = null;
            return;
        }

        // Pick closest valid target
        GameObject bestTarget = null;
        float closestDist = Mathf.Infinity;

        foreach (var t in potentialTargets)
        {
            float dist = Vector3.Distance(transform.position, t.transform.position);
            if (dist < minEngagementRange || dist > maxEngagementRange) continue;

            // Firing angle check
            Vector3 dir = (t.transform.position - gunFirePoint.position).normalized;
            float angle = Vector3.Angle(gunFirePoint.forward, dir);
            if (angle > maxFiringAngle) continue;

            if (dist < closestDist)
            {
                closestDist = dist;
                bestTarget = t;
            }
        }

        currentTarget = bestTarget;
    }

    void TryStartBurstFire()
    {
        if (Time.time - lastBurstTime < burstCooldown) return;
        if (currentTarget == null) return;

        StartCoroutine(BurstFire());
        lastBurstTime = Time.time;
    }

    IEnumerator BurstFire()
    {
        isBurstFiring = true;
        currentBurstCount = 0;

        for (int i = 0; i < burstSize; i++)
        {
            if (currentTarget == null) break;

            FireSingleShot();
            currentBurstCount++;

            if (i < burstSize - 1)
                yield return new WaitForSeconds(burstFireRate);
        }

        isBurstFiring = false;
    }

    void FireSingleShot()
    {
        if (gunFirePoint == null) return;

        // Apply damage via interface
        IDamageable dmg = currentTarget.GetComponent<IDamageable>();
        if (dmg != null && !dmg.IsDead)
        {
            dmg.TakeDamage(damage, transform.parent.gameObject); // This passes the bot
            if (showDebugInfo)
                Debug.Log($"[F16BotGun] Hit {currentTarget.name} for {damage} damage");
        }

        // Show visuals (optional)
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, gunFirePoint.position, gunFirePoint.rotation);
            Destroy(flash, 0.2f);
        }

        if (tracerLine != null)
        {
            tracerLine.enabled = true;
            tracerLine.SetPosition(0, gunFirePoint.position);
            tracerLine.SetPosition(1, currentTarget.transform.position);
            StartCoroutine(HideTracerAfterDelay());
        }
    }


    IEnumerator HideTracerAfterDelay()
    {
        yield return new WaitForSeconds(tracerDuration);
        if (tracerLine != null) tracerLine.enabled = false;
    }

    // External controls
    public void SetCanFire(bool value) => canFire = value;
    public bool HasTarget() => currentTarget != null;
    public GameObject GetCurrentTarget() => currentTarget;
    public bool IsBurstFiring() => isBurstFiring;
}
