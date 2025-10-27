using UnityEngine;
using System.Collections.Generic;

public class AmmoReloadSystem : MonoBehaviour
{
    [Header("Reload Times (seconds)")]
    [SerializeField] private float bulletReloadTimePerUnit = 0.1f;
    [SerializeField] private float aim9ReloadTime = 8f;
    [SerializeField] private float aim7ReloadTime = 10f;
    [SerializeField] private float aim120ReloadTime = 12f;

    [Header("References (Auto-detected)")]
    private WeaponManager playerWeaponManager;
    private BotWeaponManager botWeaponManager;
    private F16GunFire playerGunScript;
    private F16BotGunFire botGunScript;
    private bool isBot = false;

    [Header("Reload State")]
    private bool isReloading = false;
    private Dictionary<int, MissileReloadData> missileReloadData = new Dictionary<int, MissileReloadData>();
    private BulletReloadData bulletReloadData;

    private bool gunsReloading = false;
    private HashSet<int> pylonsReloading = new HashSet<int>();

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private class MissileReloadData
    {
        public int pylonIndex;
        public int missilesNeeded;
        public float reloadTimePerMissile;
        public float reloadProgress;
        public int missilesReloaded;
        public MissileType missileType;
    }

    private class BulletReloadData
    {
        public int bulletsNeeded;
        public float reloadProgress;
        public int bulletsReloaded;
        public int maxAmmo;
    }

    void Start()
    {
        DetectWeaponSystems();
    }

    void DetectWeaponSystems()
    {
        playerWeaponManager = GetComponent<WeaponManager>();
        if (playerWeaponManager == null)
            playerWeaponManager = GetComponentInChildren<WeaponManager>();

        playerGunScript = GetComponent<F16GunFire>();
        if (playerGunScript == null)
            playerGunScript = GetComponentInChildren<F16GunFire>();

        botWeaponManager = GetComponent<BotWeaponManager>();
        if (botWeaponManager == null)
            botWeaponManager = GetComponentInChildren<BotWeaponManager>();

        botGunScript = GetComponent<F16BotGunFire>();
        if (botGunScript == null)
            botGunScript = GetComponentInChildren<F16BotGunFire>();

        if (botWeaponManager != null || botGunScript != null)
        {
            isBot = true;
            if (showDebugInfo)
                Debug.Log($"[AmmoReloadSystem] Detected BOT aircraft: {gameObject.name}");
        }
        else if (playerWeaponManager != null || playerGunScript != null)
        {
            isBot = false;
            if (showDebugInfo)
                Debug.Log($"[AmmoReloadSystem] Detected PLAYER aircraft: {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[AmmoReloadSystem] No weapon systems found on {gameObject.name}!");
        }
    }

    void Update()
    {
        if (isReloading)
        {
            ProcessReloading();
        }
    }

    public bool NeedsResupply()
    {
        bool needsAmmo = false;

        if (!isBot && playerGunScript != null)
        {
            int currentAmmo = playerGunScript.GetCurrentAmmo();
            int maxAmmo = playerGunScript.GetMaxAmmo();
            if (currentAmmo < maxAmmo)
                needsAmmo = true;
        }

        MissilePylon[] pylons = isBot ?
            (botWeaponManager != null ? botWeaponManager.missilePylons : null) :
            (playerWeaponManager != null ? playerWeaponManager.missilePylons : null);

        if (pylons != null)
        {
            foreach (var pylon in pylons)
            {
                if (pylon != null)
                {
                    int currentCount = pylon.GetTotalMissiles();
                    int maxCount = 2;
                    if (currentCount < maxCount)
                    {
                        needsAmmo = true;
                        break;
                    }
                }
            }
        }

        return needsAmmo;
    }

    public void StartResupply()
    {
        if (isReloading)
        {
            if (showDebugInfo)
                Debug.Log($"[AmmoReloadSystem] {gameObject.name} already reloading, ignoring new request");
            return;
        }

        missileReloadData.Clear();
        bulletReloadData = null;
        pylonsReloading.Clear();
        gunsReloading = false;

        if (!isBot && playerGunScript != null)
        {
            int currentAmmo = playerGunScript.GetCurrentAmmo();
            int maxAmmo = playerGunScript.GetMaxAmmo();
            int needed = maxAmmo - currentAmmo;

            if (needed > 0)
            {
                bulletReloadData = new BulletReloadData
                {
                    bulletsNeeded = needed,
                    reloadProgress = 0f,
                    bulletsReloaded = 0,
                    maxAmmo = maxAmmo
                };

                gunsReloading = true;

                if (showDebugInfo)
                    Debug.Log($"[AmmoReloadSystem] Reloading {needed} bullets");
            }
        }

        MissilePylon[] pylons = isBot ?
            (botWeaponManager != null ? botWeaponManager.missilePylons : null) :
            (playerWeaponManager != null ? playerWeaponManager.missilePylons : null);

        if (pylons != null)
        {
            for (int i = 0; i < pylons.Length; i++)
            {
                MissilePylon pylon = pylons[i];
                if (pylon != null)
                {
                    int currentCount = pylon.GetTotalMissiles();
                    int maxCount = 2;
                    int needed = maxCount - currentCount;

                    if (needed > 0)
                    {
                        float reloadTime = GetReloadTimeForMissileType(pylon.missileType);
                        missileReloadData[i] = new MissileReloadData
                        {
                            pylonIndex = i,
                            missilesNeeded = needed,
                            reloadTimePerMissile = reloadTime,
                            reloadProgress = 0f,
                            missilesReloaded = 0,
                            missileType = pylon.missileType
                        };

                        pylonsReloading.Add(i);

                        if (showDebugInfo)
                            Debug.Log($"[AmmoReloadSystem] Pylon {i + 1} ({pylon.missileType}): Reloading {needed} missiles");
                    }
                }
            }
        }

        if (bulletReloadData != null || missileReloadData.Count > 0)
        {
            isReloading = true;
            if (showDebugInfo)
                Debug.Log($"[AmmoReloadSystem] Resupply started!");
        }
    }

    void ProcessReloading()
    {
        bool stillReloading = false;

        if (!isBot && bulletReloadData != null)
        {
            bulletReloadData.reloadProgress += Time.deltaTime;

            int shouldHaveReloaded = Mathf.FloorToInt(bulletReloadData.reloadProgress / bulletReloadTimePerUnit);
            int toReload = Mathf.Min(shouldHaveReloaded - bulletReloadData.bulletsReloaded,
                                     bulletReloadData.bulletsNeeded - bulletReloadData.bulletsReloaded);

            if (toReload > 0)
            {
                AddPlayerAmmo(toReload);
                bulletReloadData.bulletsReloaded += toReload;

                if (showDebugInfo)
                    Debug.Log($"[AmmoReloadSystem] Reloaded {toReload} bullets ({bulletReloadData.bulletsReloaded}/{bulletReloadData.bulletsNeeded})");
            }

            if (bulletReloadData.bulletsReloaded < bulletReloadData.bulletsNeeded)
                stillReloading = true;
            else
            {
                bulletReloadData = null;
                gunsReloading = false;
                if (showDebugInfo)
                    Debug.Log($"[AmmoReloadSystem] Gun reload complete!");
            }
        }

        List<int> completedPylons = new List<int>();
        foreach (var kvp in missileReloadData)
        {
            MissileReloadData data = kvp.Value;
            data.reloadProgress += Time.deltaTime;

            int shouldHaveReloaded = Mathf.FloorToInt(data.reloadProgress / data.reloadTimePerMissile);
            int toReload = Mathf.Min(shouldHaveReloaded - data.missilesReloaded,
                                     data.missilesNeeded - data.missilesReloaded);

            if (toReload > 0)
            {
                RestoreMissilesToPylon(data.pylonIndex, toReload, data.missileType);
                data.missilesReloaded += toReload;

                if (showDebugInfo)
                    Debug.Log($"[AmmoReloadSystem] Pylon {data.pylonIndex + 1}: Reloaded {toReload} missiles");
            }

            if (data.missilesReloaded >= data.missilesNeeded)
            {
                completedPylons.Add(kvp.Key);
                pylonsReloading.Remove(kvp.Key);
                if (showDebugInfo)
                    Debug.Log($"[AmmoReloadSystem] Pylon {kvp.Key + 1} reload complete!");
            }
            else
            {
                stillReloading = true;
            }
        }

        foreach (int pylonIndex in completedPylons)
        {
            missileReloadData.Remove(pylonIndex);
        }

        if (!stillReloading)
        {
            isReloading = false;
            if (showDebugInfo)
                Debug.Log($"[AmmoReloadSystem] Resupply complete!");
        }
    }

    void RestoreMissilesToPylon(int pylonIndex, int count, MissileType missileType)
    {
        MissilePylon[] pylons = isBot ?
            (botWeaponManager != null ? botWeaponManager.missilePylons : null) :
            (playerWeaponManager != null ? playerWeaponManager.missilePylons : null);

        if (pylons == null || pylonIndex < 0 || pylonIndex >= pylons.Length)
            return;

        MissilePylon pylon = pylons[pylonIndex];
        if (pylon == null) return;

        GameObject[] modelPrefabs = isBot ?
            (botWeaponManager != null ? botWeaponManager.missileModelPrefabs : null) :
            (playerWeaponManager != null ? playerWeaponManager.missileModelPrefabs : null);

        if (modelPrefabs == null || modelPrefabs.Length == 0)
        {
            Debug.LogError("[AmmoReloadSystem] No missile MODEL prefabs assigned in WeaponManager!");
            return;
        }

        int prefabIndex = (int)missileType;
        if (prefabIndex < 0 || prefabIndex >= modelPrefabs.Length || modelPrefabs[prefabIndex] == null)
        {
            Debug.LogError($"[AmmoReloadSystem] Missing MODEL prefab for {missileType}");
            return;
        }

        GameObject missileModelPrefab = modelPrefabs[prefabIndex];

        for (int i = 0; i < count; i++)
        {
            Transform targetPylon = null;
            string missileName = "";

            int leftCount = (pylon.leftPylon != null) ? pylon.leftPylon.childCount : 0;
            int rightCount = (pylon.rightPylon != null) ? pylon.rightPylon.childCount : 0;

            if (leftCount <= rightCount && pylon.leftPylon != null)
            {
                targetPylon = pylon.leftPylon;
                missileName = GetMissileNameForPylon(missileType, true);
            }
            else if (pylon.rightPylon != null)
            {
                targetPylon = pylon.rightPylon;
                missileName = GetMissileNameForPylon(missileType, false);
            }

            if (targetPylon != null)
            {
                // Instantiate the model
                GameObject missile = Instantiate(missileModelPrefab, targetPylon);
                missile.name = missileName;

                // Set position and rotation FIRST
                missile.transform.localPosition = Vector3.zero;
                missile.transform.localRotation = Quaternion.identity;
                missile.transform.localScale = new Vector3(110f, 110f, 110f);

                // CRITICAL: Remove ALL physics components immediately to prevent position changes
                Rigidbody[] rigidbodies = missile.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody rb in rigidbodies)
                {
                    DestroyImmediate(rb);
                }

                Collider[] colliders = missile.GetComponentsInChildren<Collider>(true);
                foreach (Collider col in colliders)
                {
                    DestroyImmediate(col);
                }

                // Remove any missile scripts (we only want visual models)
                MonoBehaviour[] scripts = missile.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (MonoBehaviour script in scripts)
                {
                    if (script.GetType().Name.Contains("Missile") || script.GetType().Name.Contains("AIM"))
                    {
                        DestroyImmediate(script);
                    }
                }

                if (showDebugInfo)
                    Debug.Log($"[AmmoReloadSystem] Spawned {missileName} at scale (110, 110, 110)");
            }
        }
    }

    string GetMissileNameForPylon(MissileType type, bool isLeft)
    {
        switch (type)
        {
            case MissileType.Infrared:
                return isLeft ? "aim-9" : "aim-9 (1)";
            case MissileType.SemiActiveRadar:
                return isLeft ? "aim-7" : "aim-7 (1)";
            case MissileType.ActiveRadar:
                return isLeft ? "aim-120" : "aim-120 (1)";
            default:
                return isLeft ? "missile" : "missile (1)";
        }
    }

    float GetReloadTimeForMissileType(MissileType type)
    {
        switch (type)
        {
            case MissileType.Infrared:
                return aim9ReloadTime;
            case MissileType.SemiActiveRadar:
                return aim7ReloadTime;
            case MissileType.ActiveRadar:
                return aim120ReloadTime;
            default:
                return 10f;
        }
    }

    void AddPlayerAmmo(int amount)
    {
        if (playerGunScript == null) return;

        var field = typeof(F16GunFire).GetField("currentAmmo",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            int current = (int)field.GetValue(playerGunScript);
            int max = playerGunScript.GetMaxAmmo();
            int newAmount = Mathf.Min(current + amount, max);
            field.SetValue(playerGunScript, newAmount);

            var updateMethod = typeof(F16GunFire).GetMethod("UpdateAmmoUI",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (updateMethod != null)
                updateMethod.Invoke(playerGunScript, null);
        }
    }

    public bool IsReloading() => isReloading;
    public bool IsGunReloading() => gunsReloading;
    public bool IsPylonReloading(int pylonIndex) => pylonsReloading.Contains(pylonIndex);
}