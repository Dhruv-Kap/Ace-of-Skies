using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AimAssist : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] float detectionRange = 650f;
    [SerializeField] float detectionInterval = 0.2f;
    [SerializeField] float detectionAngle = 60f;

    [Header("Crosshair Settings")]
    [SerializeField] RectTransform crosshairParent;
    [SerializeField] float crosshairSpeed = 5f;
    [SerializeField] GameObject gunCrosshair;

    [Header("IR Missile Settings")]
    [SerializeField] float irDetectionRange = 1300f;
    [SerializeField] float irLockOnTime = 2f;
    [SerializeField] Image infraredFillImage;
    [SerializeField] Color irNormalColor = Color.white;
    [SerializeField] Color irLockedColor = Color.red;

    [Header("SARH Missile Settings")]
    [SerializeField] float sarhDetectionRange = 2000f;
    [SerializeField] float sarhLockOnTime = 3f;
    [SerializeField] Image sarhFillImage;
    [SerializeField] Color sarhNormalColor = Color.cyan;
    [SerializeField] Color sarhLockedColor = Color.green;

    [Header("ARH Missile Settings")]
    [SerializeField] float arhDetectionRange = 2500f;
    [SerializeField] float arhLockOnTime = 4f;
    [SerializeField] Image arhFillImage;
    [SerializeField] Color arhNormalColor = Color.blue;
    [SerializeField] Color arhLockedColor = Color.magenta;

    [Header("References")]
    [SerializeField] public WeaponManager weaponManager;

    GameObject currentTarget;
    Camera playerCamera;
    Vector2 screenCenter;

    float lockProgress;
    bool isLocking;
    bool isLocked;
    MissileType? currentMissileType;

    void Start()
    {
        Reinitialize();
    }

    // Call this on respawn to reset ALL references and coroutines!
    public void Reinitialize(WeaponManager wm = null)
    {
        if (wm != null) weaponManager = wm;
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = Object.FindFirstObjectByType<Camera>();

        if (crosshairParent == null)
        {
            GameObject crosshairGO = GameObject.Find("Crosshair");
            if (crosshairGO != null)
                crosshairParent = crosshairGO.GetComponent<RectTransform>();
        }

        screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        ResetLock();

        StopAllCoroutines();
        StartCoroutine(DetectEnemiesCoroutine());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) SwitchToNextTarget();

        ValidateCurrentTarget();
        UpdateCrosshairPosition();
        UpdateMissileLockOn();
    }

    IEnumerator DetectEnemiesCoroutine()
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
        MissileType? missileType = GetCurrentMissileType();
        if (!missileType.HasValue) return detectionRange;

        switch (missileType.Value)
        {
            case MissileType.Infrared: return irDetectionRange;
            case MissileType.SemiActiveRadar: return sarhDetectionRange;
            case MissileType.ActiveRadar: return arhDetectionRange;
            default: return detectionRange;
        }
    }

    MissileType? GetCurrentMissileType()
    {
        if (weaponManager == null) return null;
        WeaponManager.WeaponType currentWeapon = weaponManager.GetCurrentWeapon();
        if (currentWeapon == WeaponManager.WeaponType.Gun) return null;
        int pylonIndex = GetPylonIndexFromWeapon(currentWeapon);
        if (pylonIndex >= 0 && pylonIndex < weaponManager.missilePylons.Length)
            return weaponManager.missilePylons[pylonIndex].missileType;
        return null;
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
        currentMissileType = GetCurrentMissileType();
        ResetLock();
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
            currentTarget = null;
            ResetLock();
        }
    }

    void SwitchToNextTarget()
    {
        GameObject previousTarget = currentTarget;
        currentTarget = null;
        ResetLock();
        CheckForEnemiesExcluding(previousTarget);
    }

    void UpdateCrosshairPosition()
    {
        if (crosshairParent == null) return;
        Vector2 targetScreenPos = screenCenter;
        if (currentTarget != null && playerCamera != null)
        {
            Vector3 worldPos = currentTarget.transform.position;
            Vector3 screenPos = playerCamera.WorldToScreenPoint(worldPos);
            targetScreenPos = new Vector2(screenPos.x, screenPos.y);
        }
        Vector2 currentPos = crosshairParent.position;
        Vector2 newPos = Vector2.Lerp(currentPos, targetScreenPos, crosshairSpeed * Time.deltaTime);
        crosshairParent.position = newPos;
    }

    void UpdateMissileLockOn()
    {
        MissileType? missileType = GetCurrentMissileType();
        bool shouldShowLock = missileType.HasValue && currentTarget != null;

        if (shouldShowLock)
        {
            if (!isLocking || currentMissileType != missileType)
            {
                isLocking = true;
                lockProgress = 0f;
                isLocked = false;
                currentMissileType = missileType;
            }

            float lockTime = GetLockOnTime(missileType.Value);
            lockProgress += Time.deltaTime / lockTime;
            lockProgress = Mathf.Clamp01(lockProgress);

            if (lockProgress >= 1f && !isLocked)
                isLocked = true;

            UpdateCrosshairVisuals(missileType.Value);
        }
        else
        {
            ResetLock();
            HideAllCrosshairVisuals();
        }
    }

    float GetLockOnTime(MissileType missileType)
    {
        switch (missileType)
        {
            case MissileType.Infrared: return irLockOnTime;
            case MissileType.SemiActiveRadar: return sarhLockOnTime;
            case MissileType.ActiveRadar: return arhLockOnTime;
            default: return 2f;
        }
    }

    void UpdateCrosshairVisuals(MissileType missileType)
    {
        HideAllCrosshairVisuals();
        Image targetImage = null;
        Color normalColor = Color.white;
        Color lockedColor = Color.red;

        switch (missileType)
        {
            case MissileType.Infrared: targetImage = infraredFillImage; normalColor = irNormalColor; lockedColor = irLockedColor; break;
            case MissileType.SemiActiveRadar: targetImage = sarhFillImage; normalColor = sarhNormalColor; lockedColor = sarhLockedColor; break;
            case MissileType.ActiveRadar: targetImage = arhFillImage; normalColor = arhNormalColor; lockedColor = arhLockedColor; break;
        }
        if (targetImage != null)
        {
            targetImage.gameObject.SetActive(true);
            targetImage.fillAmount = lockProgress;
            targetImage.color = isLocked ? lockedColor : normalColor;
        }
    }

    void HideAllCrosshairVisuals()
    {
        if (infraredFillImage != null) infraredFillImage.gameObject.SetActive(false);
        if (sarhFillImage != null) sarhFillImage.gameObject.SetActive(false);
        if (arhFillImage != null) arhFillImage.gameObject.SetActive(false);
    }

    void ResetLock()
    {
        isLocking = false;
        isLocked = false;
        lockProgress = 0f;
        currentMissileType = null;
    }

    // Public status methods
    public bool HasLockedTarget() => currentTarget != null;
    public GameObject GetLockedTarget() => currentTarget;
    public bool IsMissileLocked() => isLocked;
    public float GetLockProgress() => lockProgress;
    public MissileType? GetCurrentLockedMissileType() => currentMissileType;
    public void SetGunCrosshairVisible(bool visible)
    {
        if (gunCrosshair != null)
            gunCrosshair.SetActive(visible);
    }
}
