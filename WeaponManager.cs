using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponManager : MonoBehaviour
{
    [Header("Gun Configuration")]
    public MonoBehaviour gunScript; // Your F16GunFire script

    [Header("Missile Pylons")]
    public MissilePylon[] missilePylons = new MissilePylon[3]; // 3 pairs of pylons

    [Header("Crosshairs")]
    public GameObject irCrosshair; // Infrared missiles
    public GameObject semiRadarCrosshair; // Semi-Active Radar
    public GameObject activeRadarCrosshair; // Active Radar

    [Header("UI References")]
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI ammoCountText;
    public TextMeshProUGUI targetingTypeText;
    public Transform weaponSelectionIndicator; // Parent for weapon selection UI
    public GameObject[] weaponSelectionButtons = new GameObject[4]; // UI buttons for weapon selection

    [Header("Crosshair Management")]
    public AimAssist aimAssist; // Reference to AimAssist for gun crosshair control

    [Header("Missile Prefabs")]
    public GameObject[] missilePrefabs = new GameObject[3]; // IR, Semi-Radar, Active-Radar

    // Current weapon state
    private WeaponType currentWeapon = WeaponType.Gun;
    private int currentMissilePylon = 0;

    // UI auto-hide
    private Coroutine uiHideCoroutine;

    // Weapon types
    public enum WeaponType
    {
        Gun = 0,
        MissilePylon1 = 1,
        MissilePylon2 = 2,
        MissilePylon3 = 3
    }

    void Start()
    {
        // Initialize weapon selection
        SwitchToWeapon(WeaponType.Gun);
        UpdateUI();
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        // Weapon switching only - firing is handled by individual weapon scripts
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SwitchToWeapon(WeaponType.Gun);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            SwitchToWeapon(WeaponType.MissilePylon1);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            SwitchToWeapon(WeaponType.MissilePylon2);
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            SwitchToWeapon(WeaponType.MissilePylon3);

        // Missile firing only (gun firing handled by F16GunFire script)
        if (Input.GetMouseButtonDown(0) && currentWeapon != WeaponType.Gun)
        {
            FireCurrentWeapon();
        }
    }

    public void SwitchToWeapon(WeaponType weaponType)
    {
        currentWeapon = weaponType;

        // Deactivate all weapon systems first
        DeactivateAllWeapons();

        // Activate current weapon
        switch (currentWeapon)
        {
            case WeaponType.Gun:
                ActivateGun();
                break;
            case WeaponType.MissilePylon1:
                currentMissilePylon = 0;
                ActivateMissilePylon(0);
                break;
            case WeaponType.MissilePylon2:
                currentMissilePylon = 1;
                ActivateMissilePylon(1);
                break;
            case WeaponType.MissilePylon3:
                currentMissilePylon = 2;
                ActivateMissilePylon(2);
                break;
        }

        UpdateUI();
        UpdateWeaponSelectionIndicator();
        StartUIAutoHide();
    }

    void DeactivateAllWeapons()
    {
        // Deactivate gun
        if (gunScript != null)
            gunScript.enabled = false;

        // Hide gun crosshair via AimAssist
        if (aimAssist != null)
            aimAssist.SetGunCrosshairVisible(false);

        // Hide all missile crosshairs
        if (irCrosshair) irCrosshair.SetActive(false);
        if (semiRadarCrosshair) semiRadarCrosshair.SetActive(false);
        if (activeRadarCrosshair) activeRadarCrosshair.SetActive(false);
    }

    void ActivateGun()
    {
        if (gunScript != null)
            gunScript.enabled = true;

        // Show gun crosshair via AimAssist
        if (aimAssist != null)
            aimAssist.SetGunCrosshairVisible(true);
    }

    void ActivateMissilePylon(int pylonIndex)
    {
        if (pylonIndex >= 0 && pylonIndex < missilePylons.Length)
        {
            MissilePylon pylon = missilePylons[pylonIndex];
            if (pylon != null && pylon.HasMissiles())
            {
                // Activate appropriate crosshair based on missile type
                switch (pylon.missileType)
                {
                    case MissileType.Infrared:
                        if (irCrosshair) irCrosshair.SetActive(true);
                        break;
                    case MissileType.SemiActiveRadar:
                        if (semiRadarCrosshair) semiRadarCrosshair.SetActive(true);
                        break;
                    case MissileType.ActiveRadar:
                        if (activeRadarCrosshair) activeRadarCrosshair.SetActive(true);
                        break;
                }
            }
        }
    }

    void FireCurrentWeapon()
    {
        // Only handle missile firing - gun firing is handled by F16GunFire script
        switch (currentWeapon)
        {
            case WeaponType.MissilePylon1:
            case WeaponType.MissilePylon2:
            case WeaponType.MissilePylon3:
                FireMissile(currentMissilePylon);
                break;
        }
    }

    void FireMissile(int pylonIndex)
    {
        if (pylonIndex >= 0 && pylonIndex < missilePylons.Length)
        {
            MissilePylon pylon = missilePylons[pylonIndex];
            if (pylon != null && pylon.HasMissiles())
            {
                if (pylon.IsOnCooldown())
                {
                    Debug.Log($"Pylon {pylonIndex + 1} on cooldown! {pylon.GetCooldownRemaining():F1}s remaining");
                    return;
                }

                Transform missileTransform = pylon.GetNextMissileToFire();
                if (missileTransform != null)
                {
                    Vector3 spawnPosition = missileTransform.position;
                    Quaternion spawnRotation = missileTransform.rotation;

                    Debug.Log($"[WeaponManager] Firing missile from position: {spawnPosition}"); // ADD THIS

                    Destroy(missileTransform.gameObject);

                    if (missilePrefabs != null)
                    {
                        int prefabIndex = (int)pylon.missileType;
                        if (prefabIndex >= 0 && prefabIndex < missilePrefabs.Length && missilePrefabs[prefabIndex] != null)
                        {
                            Debug.Log($"[WeaponManager] Instantiating missile prefab: {missilePrefabs[prefabIndex].name}"); // ADD THIS

                            GameObject newMissile = Instantiate(missilePrefabs[prefabIndex], spawnPosition, spawnRotation);

                            Debug.Log($"[WeaponManager] Missile instantiated: {newMissile.name} at {newMissile.transform.position}"); // ADD THIS

                            AIM9Missile missileController = newMissile.GetComponent<AIM9Missile>();
                            if (missileController != null)
                            {
                                missileController.SetLauncher(gameObject);

                                if (aimAssist != null && aimAssist.HasLockedTarget())
                                {
                                    missileController.SetTarget(aimAssist.GetLockedTarget());
                                    Debug.Log($"[WeaponManager] Target set to: {aimAssist.GetLockedTarget().name}"); // ADD THIS
                                }
                                else
                                {
                                    Debug.LogWarning("[WeaponManager] No locked target found!"); // ADD THIS
                                }
                            }
                            else
                            {
                                Debug.LogError("[WeaponManager] AIM9Missile script not found on prefab!"); // ADD THIS
                            }
                        }
                        else
                        {
                            Debug.LogError($"[WeaponManager] Missile prefab not assigned or invalid index: {prefabIndex}"); // ADD THIS
                        }
                    }

                    pylon.FireMissile();
                    UpdateUI();

                    if (!pylon.HasMissiles())
                    {
                        Debug.Log($"Pylon {pylonIndex + 1} is empty!");
                    }
                }
                else
                {
                    Debug.LogError("[WeaponManager] No missile transform found to fire!"); // ADD THIS
                }
            }
        }
    }



    void UpdateUI()
    {
        string weaponName = "";
        string ammoCount = "";
        string targetingType = "";

        switch (currentWeapon)
        {
            case WeaponType.Gun:
                weaponName = "Aircraft Gun";
                ammoCount = "Ready"; // Gun ammo is handled by F16GunFire script
                targetingType = "Manual";
                break;
            case WeaponType.MissilePylon1:
            case WeaponType.MissilePylon2:
            case WeaponType.MissilePylon3:
                MissilePylon pylon = missilePylons[currentMissilePylon];
                if (pylon != null)
                {
                    weaponName = GetMissileName(pylon.missileType);
                    ammoCount = pylon.GetTotalMissiles().ToString();
                    targetingType = GetTargetingTypeName(pylon.missileType);
                }
                break;
        }

        if (weaponNameText) weaponNameText.text = weaponName;
        if (ammoCountText) ammoCountText.text = ammoCount;
        if (targetingTypeText) targetingTypeText.text = targetingType;
    }

    void UpdateWeaponSelectionIndicator()
    {
        for (int i = 0; i < weaponSelectionButtons.Length; i++)
        {
            if (weaponSelectionButtons[i] != null)
            {
                // Highlight current weapon selection
                weaponSelectionButtons[i].GetComponent<Image>().color =
                    (i == (int)currentWeapon) ? Color.yellow : Color.white;
            }
        }
    }

    string GetMissileName(MissileType type)
    {
        switch (type)
        {
            case MissileType.Infrared: return "IR Missile";
            case MissileType.SemiActiveRadar: return "Semi-Active Radar";
            case MissileType.ActiveRadar: return "Active Radar";
            default: return "Unknown";
        }
    }

    string GetTargetingTypeName(MissileType type)
    {
        switch (type)
        {
            case MissileType.Infrared: return "Heat-Seeking";
            case MissileType.SemiActiveRadar: return "Semi-Active";
            case MissileType.ActiveRadar: return "Fire & Forget";
            default: return "Unknown";
        }
    }

    void StartUIAutoHide()
    {
        // Stop any existing coroutine
        if (uiHideCoroutine != null)
        {
            StopCoroutine(uiHideCoroutine);
        }

        // Show UI elements
        SetUIVisible(true);

        // Start new hide timer
        uiHideCoroutine = StartCoroutine(HideUIAfterDelay(2f));
    }

    IEnumerator HideUIAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetUIVisible(false);
        uiHideCoroutine = null;
    }

    void SetUIVisible(bool visible)
    {
        if (weaponNameText) weaponNameText.gameObject.SetActive(visible);
        if (ammoCountText) ammoCountText.gameObject.SetActive(visible);
        if (targetingTypeText) targetingTypeText.gameObject.SetActive(visible);
    }

    // Public methods for external access
    public bool CanFireCurrentWeapon()
    {
        switch (currentWeapon)
        {
            case WeaponType.Gun:
                return true; // Gun availability is handled by F16GunFire script
            case WeaponType.MissilePylon1:
            case WeaponType.MissilePylon2:
            case WeaponType.MissilePylon3:
                return missilePylons[currentMissilePylon] != null &&
                       missilePylons[currentMissilePylon].HasMissiles();
            default:
                return false;
        }
    }

    public WeaponType GetCurrentWeapon()
    {
        return currentWeapon;
    }
}

// Missile Pylon Class
[System.Serializable]
public class MissilePylon
{
    [Header("Pylon Configuration")]
    public string pylonName;
    public MissileType missileType;
    public Transform leftPylon;  // The main pylon (e.g., MissilePylon1)
    public Transform rightPylon; // The paired pylon (e.g., MissilePylon1 (1))

    [Header("Cooldown")]
    public float cooldownTime = 5f;

    private bool fireFromLeft = true; // Alternating fire pattern
    private float lastFireTime = -999f; // Track last fire time

    public bool HasMissiles()
    {
        int leftCount = (leftPylon != null) ? leftPylon.childCount : 0;
        int rightCount = (rightPylon != null) ? rightPylon.childCount : 0;
        return leftCount > 0 || rightCount > 0;
    }

    public int GetTotalMissiles()
    {
        int leftCount = (leftPylon != null) ? leftPylon.childCount : 0;
        int rightCount = (rightPylon != null) ? rightPylon.childCount : 0;
        return leftCount + rightCount;
    }

    public bool IsOnCooldown()
    {
        return Time.time - lastFireTime < cooldownTime;
    }

    public float GetCooldownRemaining()
    {
        float remaining = cooldownTime - (Time.time - lastFireTime);
        return Mathf.Max(0f, remaining);
    }

    public Transform GetNextMissileToFire()
    {
        Transform targetPylon = fireFromLeft ? leftPylon : rightPylon;

        // If target pylon is empty or null, try the other one
        if (targetPylon == null || targetPylon.childCount == 0)
        {
            targetPylon = fireFromLeft ? rightPylon : leftPylon;
        }

        if (targetPylon != null && targetPylon.childCount > 0)
        {
            return targetPylon.GetChild(0); // Get first child (missile)
        }

        return null;
    }

    public void FireMissile()
    {
        // Record fire time for cooldown
        lastFireTime = Time.time;

        // Alternate between pylons for next shot
        fireFromLeft = !fireFromLeft;
    }
}

// Missile Types
public enum MissileType
{
    Infrared,
    SemiActiveRadar,
    ActiveRadar
}