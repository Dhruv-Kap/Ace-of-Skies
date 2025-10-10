using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class F16GunFire : MonoBehaviour
{
    [Header("Gun Settings")]
    [SerializeField] private Transform gunFirePoint;
    [SerializeField] private float fireRange = 650f;
    [SerializeField] private float fireRate = 0.1f;
    [SerializeField] private int maxAmmo = 500;
    [SerializeField] private float damage = 5f; // Changed to 5 as you specified

    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private LineRenderer tracerLine;
    [SerializeField] private float tracerDuration = 0.05f;

    [Header("Overheat System")]
    [SerializeField] private float heatPerShot = 2f;
    [SerializeField] private float maxHeat = 100f;
    [SerializeField] private float coolRate = 10f;
    [SerializeField] private float overheatCoolRate = 20000000f;

    [Header("Aim Assist Integration")]
    [SerializeField] private AimAssist aimAssist; // Reference to AimAssist script

    [Header("Runtime State")]
    private int currentAmmo;
    private float currentHeat;
    private bool isOverheated;
    private float fireTimer;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Image overheatCircle;

    void Start()
    {
        // Initialize ammo and heat
        currentAmmo = maxAmmo;
        currentHeat = 0f;

        // Find AimAssist if not assigned
        if (aimAssist == null)
        {
            aimAssist = GetComponent<AimAssist>();
        }

        // Create LineRenderer for tracer if not assigned
        if (tracerLine == null)
        {
            GameObject tracerObj = new GameObject("TracerLine");
            tracerObj.transform.SetParent(gunFirePoint);
            tracerLine = tracerObj.AddComponent<LineRenderer>();
            tracerLine.startWidth = 0.05f;
            tracerLine.endWidth = 0.05f;
            tracerLine.material = new Material(Shader.Find("Sprites/Default"));
            tracerLine.startColor = Color.yellow;
            tracerLine.endColor = Color.yellow;
        }
        tracerLine.enabled = false;

        // Update UI at game start
        UpdateAmmoUI();
        UpdateOverheatUI();
    }

    void Update()
    {
        // Increment fire timer
        fireTimer += Time.deltaTime;

        // Handle all firing and cooling logic
        HandleFiringAndCooling();
    }

    void HandleFiringAndCooling()
    {
        // Cooling logic
        if (isOverheated)
        {
            // Slower cooling when overheated
            currentHeat -= overheatCoolRate * Time.deltaTime;
        }
        else
        {
            // Normal cooling rate
            currentHeat -= coolRate * Time.deltaTime;
        }

        // Clamp heat to be >= 0
        currentHeat = Mathf.Max(0f, currentHeat);

        // Check if cooled enough to reset overheat state
        if (isOverheated && currentHeat <= maxHeat * 0.75f)
        {
            isOverheated = false;
        }

        // Update heat UI after cooling
        UpdateOverheatUI();

        // Firing logic
        if (Input.GetMouseButton(0) &&
            fireTimer >= fireRate &&
            !isOverheated &&
            currentAmmo > 0)
        {
            FireOneShot();
        }
    }

    void FireOneShot()
    {
        // Reset fire timer
        fireTimer = 0f;

        // Check if we have a locked target from aim assist
        if (aimAssist != null && aimAssist.HasLockedTarget())
        {
            // AIM ASSIST HIT - Guaranteed hit on locked target
            GameObject lockedTarget = aimAssist.GetLockedTarget();
            HandleLockedTargetHit(lockedTarget);

            // Show tracer to locked target
            ShowTracer(gunFirePoint.position, lockedTarget.transform.position);

            Debug.Log($"Aim Assist Hit: {lockedTarget.name}");
        }
        else
        {
            // NORMAL RAYCAST - No locked target, use regular firing
            RaycastHit hit;
            Vector3 shootDirection = gunFirePoint.forward;

            if (Physics.Raycast(gunFirePoint.position, shootDirection, out hit, fireRange))
            {
                // Hit something normally
                HandleHit(hit);
                ShowTracer(gunFirePoint.position, hit.point);
            }
            else
            {
                // No hit - show tracer to max range
                Vector3 endPoint = gunFirePoint.position + (shootDirection * fireRange);
                ShowTracer(gunFirePoint.position, endPoint);
            }
        }

        // Spawn muzzle flash
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, gunFirePoint.position, gunFirePoint.rotation);
            Destroy(flash, 0.1f);
        }

        // Decrease ammo
        currentAmmo--;

        // Increase heat
        currentHeat += heatPerShot;

        // Check for overheat
        if (currentHeat >= maxHeat)
        {
            currentHeat = maxHeat;
            isOverheated = true;
        }

        // Update UI
        UpdateAmmoUI();
        UpdateOverheatUI();
    }

    void HandleLockedTargetHit(GameObject target)
    {
        // Apply damage to locked target (guaranteed hit)
        if (target.CompareTag("Enemy"))
        {
            // Apply damage to enemy health component
            Health enemyHealth = target.GetComponent<Health>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, gameObject);
                Debug.Log($"Dealt {damage} damage to {target.name}");
            }
        }

        // Spawn hit effect at target position
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // Apply impact force if rigidbody exists
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceDirection = (target.transform.position - gunFirePoint.position).normalized;
            rb.AddForce(forceDirection * 100f, ForceMode.Impulse);
        }
    }

    void HandleHit(RaycastHit hit)
    {
        // Apply damage if the hit object has a health component (normal raycast hit)
        if (hit.collider.CompareTag("Enemy"))
        {
            Health enemyHealth = hit.collider.GetComponent<Health>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, gameObject);
                Debug.Log($"Normal hit - Dealt {damage} damage to {hit.collider.name}");
            }
        }

        // Spawn hit effect
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(effect, 2f);
        }

        // Apply impact force if rigidbody exists
        Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForceAtPosition(gunFirePoint.forward * 100f, hit.point);
        }
    }

    void ShowTracer(Vector3 start, Vector3 end)
    {
        if (tracerLine != null)
        {
            tracerLine.enabled = true;
            tracerLine.SetPosition(0, start);
            tracerLine.SetPosition(1, end);

            // Disable tracer after duration
            CancelInvoke(nameof(HideTracer));
            Invoke(nameof(HideTracer), tracerDuration);
        }
    }

    void HideTracer()
    {
        if (tracerLine != null)
        {
            tracerLine.enabled = false;
        }
    }

    void UpdateAmmoUI()
    {
        if (ammoText != null)
        {
            ammoText.text = currentAmmo.ToString();
        }
    }

    void UpdateOverheatUI()
    {
        if (overheatCircle != null)
        {
            overheatCircle.fillAmount = currentHeat / maxHeat;
        }
    }
}