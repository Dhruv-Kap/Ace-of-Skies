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
    [SerializeField] private float damage = 5f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private LineRenderer tracerLine;
    [SerializeField] private float tracerDuration = 0.05f;

    [Header("Overheat System")]
    [SerializeField] private float heatPerShot = 2f;
    [SerializeField] private float maxHeat = 100f;
    [SerializeField] private float coolRate = 10f;
    [SerializeField] private float overheatCoolRate = 20f;

    [Header("Aim Assist Integration")]
    [SerializeField] private AimAssist aimAssist;

    [Header("Runtime State")]
    private int currentAmmo;
    private float currentHeat;
    private bool isOverheated;
    private float fireTimer;
    private bool canFire = true; // Controlled by WeaponManager
    private AmmoReloadSystem reloadSystem;

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

        // Find AmmoReloadSystem
        reloadSystem = GetComponent<AmmoReloadSystem>();
        if (reloadSystem == null)
        {
            reloadSystem = GetComponentInParent<AmmoReloadSystem>();
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
        // ALWAYS run cooling logic (even when gun is not selected)
        HandleCooling();

        // Only handle firing if gun is selected weapon
        if (canFire)
        {
            HandleFiring();
        }

        // Always increment fire timer
        fireTimer += Time.deltaTime;
    }

    void HandleCooling()
    {
        // Cooling logic runs in background
        if (isOverheated)
        {
            currentHeat -= overheatCoolRate * Time.deltaTime;
        }
        else
        {
            currentHeat -= coolRate * Time.deltaTime;
        }

        currentHeat = Mathf.Max(0f, currentHeat);

        if (isOverheated && currentHeat <= maxHeat * 0.75f)
        {
            isOverheated = false;
        }

        UpdateOverheatUI();
    }

    void HandleFiring()
    {
        // CHECK IF GUN IS RELOADING
        if (reloadSystem != null && reloadSystem.IsGunReloading())
        {
            // Don't fire while reloading
            return;
        }

        // Firing logic only when gun is active weapon
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
            GameObject lockedTarget = aimAssist.GetLockedTarget();

            // Calculate tracer endpoint
            Vector3 tracerEnd = lockedTarget.transform.position;

            // Deal damage to locked target
            HandleLockedTargetHit(lockedTarget);

            // Show tracer to target
            ShowTracer(gunFirePoint.position, tracerEnd);

            Debug.Log($"[F16GunFire] Aim Assist Hit: {lockedTarget.name} - Damage: {damage}");
        }
        else
        {
            // No locked target - shoot forward (miss)
            Vector3 endPoint = gunFirePoint.position + (gunFirePoint.forward * fireRange);
            ShowTracer(gunFirePoint.position, endPoint);
            Debug.Log("[F16GunFire] No target locked - shot missed");
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
            Debug.Log("[F16GunFire] Gun overheated!");
        }

        // Update UI
        UpdateAmmoUI();
        UpdateOverheatUI();
    }

    void HandleLockedTargetHit(GameObject target)
    {
        // Deal damage to enemy
        if (target.CompareTag("Enemy"))
        {
            Health enemyHealth = target.GetComponent<Health>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, gameObject);
                Debug.Log($"[F16GunFire] Dealt {damage} damage to {target.name}");
            }
            else
            {
                Debug.LogWarning($"[F16GunFire] Target {target.name} has no Health component!");
            }
        }

        // Spawn hit effect on target
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // Apply physics force to target
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceDirection = (target.transform.position - gunFirePoint.position).normalized;
            rb.AddForce(forceDirection * 100f, ForceMode.Impulse);
        }
    }

    void ShowTracer(Vector3 start, Vector3 end)
    {
        if (tracerLine != null)
        {
            tracerLine.enabled = true;
            tracerLine.SetPosition(0, start);
            tracerLine.SetPosition(1, end);

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
            // Show reloading status if gun is reloading
            if (reloadSystem != null && reloadSystem.IsGunReloading())
            {
                ammoText.text = "RELOADING";
            }
            else
            {
                ammoText.text = currentAmmo.ToString();
            }
        }
    }

    void UpdateOverheatUI()
    {
        if (overheatCircle != null)
        {
            // ONLY update fill amount - don't change color (keep your custom color)
            overheatCircle.fillAmount = currentHeat / maxHeat;
        }
    }

    // Public methods for WeaponManager
    public void SetCanFire(bool canFire)
    {
        this.canFire = canFire;
    }

    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }

    public int GetMaxAmmo()
    {
        return maxAmmo;
    }
}
