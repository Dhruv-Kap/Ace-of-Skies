using UnityEngine;
using UnityEngine.UI;

public class AimAssist : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 650f;
    [SerializeField] private float detectionInterval = 0.2f;
    [SerializeField] private float detectionAngle = 60f;

    [Header("Crosshair Settings")]
    [SerializeField] private RectTransform crosshairParent;
    [SerializeField] private float crosshairSpeed = 5f;
    [SerializeField] private GameObject gunCrosshair; // The gun crosshair GameObject

    [Header("IR Missile Settings")]
    [SerializeField] private float irDetectionRange = 1300f;
    [SerializeField] private float irLockOnTime = 2f;
    [SerializeField] private Image infraredFillImage; // The "infrared" Image component
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lockedColor = Color.red;

    [Header("References")]
    [SerializeField] private WeaponManager weaponManager;

    private GameObject currentTarget;
    private Camera playerCamera;
    private Vector2 screenCenter;

    // IR missile lock-on variables
    private float irLockProgress = 0f;
    private bool isIRLocking = false;
    private bool isIRLocked = false;

    void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = Object.FindFirstObjectByType<Camera>();

        if (crosshairParent == null)
        {
            GameObject crosshairGO = GameObject.Find("Crosshair");
            if (crosshairGO != null)
                crosshairParent = crosshairGO.GetComponent<RectTransform>();
        }

        if (weaponManager == null)
            weaponManager = Object.FindFirstObjectByType<WeaponManager>();

        screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        StartCoroutine(DetectEnemiesCoroutine());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            SwitchToNextTarget();
        }

        ValidateCurrentTarget();
        UpdateCrosshairPosition();
        UpdateIRMissileLockOn();
    }

    System.Collections.IEnumerator DetectEnemiesCoroutine()
    {
        while (true)
        {
            CheckForEnemies();
            yield return new WaitForSeconds(detectionInterval);
        }
    }

    void CheckForEnemies()
    {
        if (currentTarget != null) return;

        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemy in allEnemies)
        {
            if (enemy != null)
            {
                Vector3 directionToEnemy = (enemy.transform.position - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                float angle = Vector3.Angle(transform.forward, directionToEnemy);

                float currentDetectionRange = GetCurrentDetectionRange();

                if (distance <= currentDetectionRange && angle <= detectionAngle)
                {
                    LockOntoTarget(enemy);
                    break;
                }
            }
        }
    }

    void CheckForEnemiesExcluding(GameObject excludeEnemy)
    {
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemy in allEnemies)
        {
            if (enemy != null && enemy != excludeEnemy)
            {
                Vector3 directionToEnemy = (enemy.transform.position - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                float angle = Vector3.Angle(transform.forward, directionToEnemy);

                float currentDetectionRange = GetCurrentDetectionRange();

                if (distance <= currentDetectionRange && angle <= detectionAngle)
                {
                    LockOntoTarget(enemy);
                    break;
                }
            }
        }
    }

    float GetCurrentDetectionRange()
    {
        if (IsCurrentlyIRMissile())
        {
            return irDetectionRange;
        }
        return detectionRange;
    }

    bool IsCurrentlyIRMissile()
    {
        if (weaponManager == null) return false;

        WeaponManager.WeaponType currentWeapon = weaponManager.GetCurrentWeapon();

        // If it's the gun, it's not IR missile
        if (currentWeapon == WeaponManager.WeaponType.Gun) return false;

        // For missile pylons, check if the pylon has IR missiles
        int pylonIndex = GetPylonIndexFromWeapon(currentWeapon);
        if (pylonIndex >= 0 && pylonIndex < weaponManager.missilePylons.Length)
        {
            return weaponManager.missilePylons[pylonIndex].missileType == MissileType.Infrared;
        }

        return false;
    }

    int GetPylonIndexFromWeapon(WeaponManager.WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponManager.WeaponType.MissilePylon1: return 0;
            case WeaponManager.WeaponType.MissilePylon2: return 1;
            case WeaponManager.WeaponType.MissilePylon3: return 2;
            default: return -1;
        }
    }

    void LockOntoTarget(GameObject target)
    {
        currentTarget = target;
        Debug.Log($"Locked onto target: {target.name}");

        // Reset IR lock progress when getting new target
        ResetIRLock();
    }

    void ValidateCurrentTarget()
    {
        if (currentTarget == null) return;

        Vector3 directionToTarget = (currentTarget.transform.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        float angle = Vector3.Angle(transform.forward, directionToTarget);

        float currentDetectionRange = GetCurrentDetectionRange();

        if (distance > currentDetectionRange || angle > detectionAngle)
        {
            Debug.Log($"Lost target: {currentTarget.name} - out of range/angle");
            currentTarget = null;
            ResetIRLock();
        }
    }

    void SwitchToNextTarget()
    {
        GameObject previousTarget = currentTarget;

        if (currentTarget != null)
        {
            Debug.Log($"Switching from target: {currentTarget.name}");
        }

        currentTarget = null;
        ResetIRLock();
        CheckForEnemiesExcluding(previousTarget);

        if (currentTarget == null)
        {
            Debug.Log("No other targets available");
        }
    }

    void UpdateCrosshairPosition()
    {
        if (crosshairParent == null) return;

        Vector2 targetScreenPos;

        if (currentTarget != null)
        {
            Vector3 worldPos = currentTarget.transform.position;
            Vector3 screenPos = playerCamera.WorldToScreenPoint(worldPos);
            targetScreenPos = new Vector2(screenPos.x, screenPos.y);
        }
        else
        {
            targetScreenPos = screenCenter;
        }

        Vector2 currentPos = crosshairParent.position;
        Vector2 newPos = Vector2.Lerp(currentPos, targetScreenPos, crosshairSpeed * Time.deltaTime);
        crosshairParent.position = newPos;
    }

    void UpdateIRMissileLockOn()
    {
        bool shouldShowIRLock = IsCurrentlyIRMissile() && currentTarget != null;

        if (shouldShowIRLock)
        {
            // Start/continue IR lock process
            if (!isIRLocking)
            {
                isIRLocking = true;
                irLockProgress = 0f;
                isIRLocked = false;
            }

            // Update lock progress
            irLockProgress += Time.deltaTime / irLockOnTime;
            irLockProgress = Mathf.Clamp01(irLockProgress);

            // Check if fully locked
            if (irLockProgress >= 1f && !isIRLocked)
            {
                isIRLocked = true;
                Debug.Log("IR Missile LOCKED ON!");
            }

            UpdateIRCrosshairVisuals();
        }
        else
        {
            // Reset when not using IR or no target
            ResetIRLock();
            HideIRCrosshairVisuals();
        }
    }

    void UpdateIRCrosshairVisuals()
    {
        if (infraredFillImage != null)
        {
            // Show the fill image
            if (!infraredFillImage.gameObject.activeInHierarchy)
                infraredFillImage.gameObject.SetActive(true);

            // Update fill amount
            infraredFillImage.fillAmount = irLockProgress;

            // Update color based on lock status
            infraredFillImage.color = isIRLocked ? lockedColor : normalColor;
        }
    }

    void HideIRCrosshairVisuals()
    {
        if (infraredFillImage != null && infraredFillImage.gameObject.activeInHierarchy)
        {
            infraredFillImage.gameObject.SetActive(false);
        }
    }

    void ResetIRLock()
    {
        isIRLocking = false;
        isIRLocked = false;
        irLockProgress = 0f;
    }

    void OnDrawGizmosSelected()
    {
        // Draw current detection range based on weapon
        float currentRange = GetCurrentDetectionRange();
        Gizmos.color = IsCurrentlyIRMissile() ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, currentRange);

        if (currentTarget != null)
        {
            Gizmos.color = isIRLocked ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }

    // Public methods for external access
    public bool HasLockedTarget()
    {
        return currentTarget != null;
    }

    public GameObject GetLockedTarget()
    {
        return currentTarget;
    }

    public bool IsIRMissileLocked()
    {
        return isIRLocked;
    }

    public float GetIRLockProgress()
    {
        return irLockProgress;
    }

    // Method for WeaponManager to control gun crosshair visibility
    public void SetGunCrosshairVisible(bool visible)
    {
        if (gunCrosshair != null)
        {
            gunCrosshair.SetActive(visible);
        }
    }
}