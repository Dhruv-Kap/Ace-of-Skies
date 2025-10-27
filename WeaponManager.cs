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
    public GameObject[] missilePrefabs = new GameObject[3]; // IR, Semi-Radar, Active-Radar (FIRING prefabs)

    [Header("Missile Models (Visual Only)")]
    public GameObject[] missileModelPrefabs = new GameObject[3]; // IR, Semi-Radar, Active-Radar (MODEL prefabs for display)

    // Current weapon state
    private WeaponType currentWeapon = WeaponType.Gun;
    private int currentMissilePylon = 0;

    // UI auto-hide
    private Coroutine uiHideCoroutine;

    // Reload system reference
    private AmmoReloadSystem reloadSystem;

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
        // Find reload system
        reloadSystem = GetComponent<AmmoReloadSystem>();
        if (reloadSystem == null)
            reloadSystem = GetComponentInChildren<AmmoReloadSystem>();

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
        // Check if gun is reloading
        if (reloadSystem != null && reloadSystem.IsGunReloading())
        {
            Debug.Log("[WeaponManager] Cannot use gun - currently reloading!");
            return;
        }

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
            // Check if this pylon is reloading
            if (reloadSystem != null && reloadSystem.IsPylonReloading(pylonIndex))
            {
                Debug.Log($"[WeaponManager] Cannot use pylon {pylonIndex + 1} - currently reloading!");
                return;
            }

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
        // Check if this pylon is reloading
        if (reloadSystem != null && reloadSystem.IsPylonReloading(pylonIndex))
        {
            Debug.Log($"[WeaponManager] Cannot fire pylon {pylonIndex + 1} - currently reloading!");
            return;
        }

        // CHECK FOR LOCKED TARGET BEFORE FIRING
        if (aimAssist == null || !aimAssist.HasLockedTarget())
        {
            Debug.Log("[WeaponManager] Cannot fire missile - No locked target!");
            return;
        }

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

                    Debug.Log($"[WeaponManager] Firing missile from position: {spawnPosition}");

                    Destroy(missileTransform.gameObject);

                    if (missilePrefabs != null)
                    {
                        int prefabIndex = (int)pylon.missileType;
                        if (prefabIndex >= 0 && prefabIndex < missilePrefabs.Length && missilePrefabs[prefabIndex] != null)
                        {
                            Debug.Log($"[WeaponManager] Instantiating missile prefab: {missilePrefabs[prefabIndex].name}");

                            GameObject newMissile = Instantiate(missilePrefabs[prefabIndex], spawnPosition, spawnRotation);

                            Debug.Log($"[WeaponManager] Missile instantiated: {newMissile.name} at {newMissile.transform.position}");

                            // Try to find AIM9, AIM7, or AIM120 missile component
                            AIM9Missile aim9Controller = newMissile.GetComponent<AIM9Missile>();
                            AIM7Missile aim7Controller = newMissile.GetComponent<AIM7Missile>();
                            AIM120Missile aim120Controller = newMissile.GetComponent<AIM120Missile>();

                            if (aim9Controller != null)
                            {
                                aim9Controller.SetLauncher(gameObject);
                                aim9Controller.SetTarget(aimAssist.GetLockedTarget());
                                Debug.Log($"[WeaponManager] AIM-9 Target set to: {aimAssist.GetLockedTarget().name}");
                            }
                            else if (aim7Controller != null)
                            {
                                aim7Controller.SetLauncher(gameObject);
                                aim7Controller.SetTarget(aimAssist.GetLockedTarget());
                                Debug.Log($"[WeaponManager] AIM-7 Target set to: {aimAssist.GetLockedTarget().name}");
                            }
                            else if (aim120Controller != null)
                            {
                                aim120Controller.SetLauncher(gameObject);
                                aim120Controller.SetTarget(aimAssist.GetLockedTarget());
                                Debug.Log($"[WeaponManager] AIM-120 Target set to: {aimAssist.GetLockedTarget().name}");
                            }
                            else
                            {
                                Debug.LogError("[WeaponManager] No recognized missile script found on prefab!");
                            }
                        }
                        else
                        {
                            Debug.LogError($"[WeaponManager] Missile prefab not assigned or invalid index: {prefabIndex}");
                        }
                    }

                    pylon.FireMissile();

                    // Update UI immediately after firing
                    UpdateUI();

                    if (!pylon.HasMissiles())
                    {
                        Debug.Log($"Pylon {pylonIndex + 1} is empty!");
                    }
                }
                else
                {
                    Debug.LogError("[WeaponManager] No missile transform found to fire!");
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

                // Show reloading status
                if (reloadSystem != null && reloadSystem.IsGunReloading())
                {
                    ammoCount = "RELOADING...";
                }
                else
                {
                    ammoCount = "Ready";
                }
                targetingType = "Manual";
                break;
            case WeaponType.MissilePylon1:
            case WeaponType.MissilePylon2:
            case WeaponType.MissilePylon3:
                MissilePylon pylon = missilePylons[currentMissilePylon];
                if (pylon != null)
                {
                    weaponName = GetMissileName(pylon.missileType);

                    // FIXED: Check the CORRECT pylon index for reload status
                    // currentMissilePylon corresponds to the selected pylon (0, 1, or 2)
                    if (reloadSystem != null && reloadSystem.IsPylonReloading(currentMissilePylon))
                    {
                        ammoCount = "RELOADING...";
                    }
                    else
                    {
                        ammoCount = pylon.GetTotalMissiles().ToString();
                    }
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
                weaponSelectionButtons[i].GetComponent<Image>().color =
                    (i == (int)currentWeapon) ? Color.yellow : Color.white;
            }
        }
    }

    string GetMissileName(MissileType type)
    {
        switch (type)
        {
            case MissileType.Infrared: return "AIM-9 Sidewinder";
            case MissileType.SemiActiveRadar: return "Semi-Active Radar";
            case MissileType.ActiveRadar: return "AIM-120 AMRAAM";
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
        if (uiHideCoroutine != null)
        {
            StopCoroutine(uiHideCoroutine);
        }

        SetUIVisible(true);
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
        if (ammoCountText) ammoCountText.gameObject.SetActive(true);
        if (targetingTypeText) targetingTypeText.gameObject.SetActive(visible);
    }

    public bool CanFireCurrentWeapon()
    {
        switch (currentWeapon)
        {
            case WeaponType.Gun:
                // Check if gun is reloading
                if (reloadSystem != null && reloadSystem.IsGunReloading())
                    return false;
                return true;
            case WeaponType.MissilePylon1:
            case WeaponType.MissilePylon2:
            case WeaponType.MissilePylon3:
                // Check if pylon is reloading
                if (reloadSystem != null && reloadSystem.IsPylonReloading(currentMissilePylon))
                    return false;
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

[System.Serializable]
public class MissilePylon
{
    [Header("Pylon Configuration")]
    public string pylonName;
    public MissileType missileType;
    public Transform leftPylon;
    public Transform rightPylon;

    [Header("Cooldown")]
    public float cooldownTime = 5f;

    private bool fireFromLeft = true;
    private float lastFireTime = -999f;

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

        if (targetPylon == null || targetPylon.childCount == 0)
        {
            targetPylon = fireFromLeft ? rightPylon : leftPylon;
        }

        if (targetPylon != null && targetPylon.childCount > 0)
        {
            return targetPylon.GetChild(0);
        }

        return null;
    }

    public void FireMissile()
    {
        lastFireTime = Time.time;
        fireFromLeft = !fireFromLeft;
    }
}

public enum MissileType
{
    Infrared,
    SemiActiveRadar,
    ActiveRadar
}
