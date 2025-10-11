using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// CENTRALIZED loadout manager - Works with BOTH WeaponManager (player) and BotWeaponManager (bots)
/// ONE instance in scene - Define all aircraft types and their loadouts here
/// </summary>
public class AircraftLoadoutManager : MonoBehaviour
{
    [Header("Aircraft Type Definitions")]
    public AircraftType[] aircraftTypes;

    [Header("Scene Aircraft Assignments")]
    public AircraftAssignment[] sceneAircraft;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private static AircraftLoadoutManager instance;

    [System.Serializable]
    public class AircraftType
    {
        public string typeName = "F-16";
        public MissileLoadout[] pylons = new MissileLoadout[3];
    }

    [System.Serializable]
    public class MissileLoadout
    {
        public GameObject missilePrefab;
        public string leftMissileName = "missile";
        public string rightMissileName = "missile (1)";
        public int missileCount = 2;
    }

    [System.Serializable]
    public class AircraftAssignment
    {
        public GameObject aircraftObject;  // The actual aircraft in scene
        public string aircraftTypeName;    // Which type it uses (e.g., "F-16", "F-15")
        public bool isBot = false;         // Is this a bot or player?

        [Header("Runtime Info (Auto-filled)")]
        public WeaponManager playerWeaponManager;
        public BotWeaponManager botWeaponManager;
        public int assignedTypeIndex = -1;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("[LoadoutManager] Multiple instances detected! Using first one.");
            Destroy(this);
            return;
        }

        InitializeAircraft();
    }

    void InitializeAircraft()
    {
        if (showDebugInfo)
        {
            Debug.Log("[LoadoutManager] Initializing aircraft loadouts...");
        }

        foreach (var assignment in sceneAircraft)
        {
            if (assignment.aircraftObject == null) continue;

            // Find weapon manager (player or bot)
            assignment.playerWeaponManager = assignment.aircraftObject.GetComponent<WeaponManager>();
            if (assignment.playerWeaponManager == null)
            {
                assignment.playerWeaponManager = assignment.aircraftObject.GetComponentInChildren<WeaponManager>();
            }

            assignment.botWeaponManager = assignment.aircraftObject.GetComponent<BotWeaponManager>();
            if (assignment.botWeaponManager == null)
            {
                assignment.botWeaponManager = assignment.aircraftObject.GetComponentInChildren<BotWeaponManager>();
            }

            // Determine if bot or player
            if (assignment.botWeaponManager != null)
            {
                assignment.isBot = true;
            }
            else if (assignment.playerWeaponManager != null)
            {
                assignment.isBot = false;
            }
            else
            {
                Debug.LogWarning($"[LoadoutManager] No weapon manager found on {assignment.aircraftObject.name}");
                continue;
            }

            // Find matching aircraft type
            assignment.assignedTypeIndex = -1;
            for (int i = 0; i < aircraftTypes.Length; i++)
            {
                if (aircraftTypes[i].typeName == assignment.aircraftTypeName)
                {
                    assignment.assignedTypeIndex = i;
                    break;
                }
            }

            if (assignment.assignedTypeIndex == -1)
            {
                Debug.LogWarning($"[LoadoutManager] Aircraft type '{assignment.aircraftTypeName}' not found for {assignment.aircraftObject.name}");
            }
            else if (showDebugInfo)
            {
                string type = assignment.isBot ? "Bot" : "Player";
                Debug.Log($"[LoadoutManager] {assignment.aircraftObject.name} [{type}] assigned as {assignment.aircraftTypeName}");
            }
        }
    }

    /// <summary>
    /// Restore default loadout for a specific aircraft (works for both player and bots)
    /// </summary>
    public int RestoreLoadout(GameObject aircraft)
    {
        if (aircraft == null) return 0;

        // Find assignment
        AircraftAssignment assignment = null;
        foreach (var a in sceneAircraft)
        {
            if (a.aircraftObject == aircraft)
            {
                assignment = a;
                break;
            }
        }

        if (assignment == null)
        {
            Debug.LogError($"[LoadoutManager] Aircraft {aircraft.name} not registered in loadout manager!");
            return 0;
        }

        if (assignment.assignedTypeIndex < 0 || assignment.assignedTypeIndex >= aircraftTypes.Length)
        {
            Debug.LogError($"[LoadoutManager] Invalid aircraft type for {aircraft.name}");
            return 0;
        }

        // Get the aircraft type configuration
        AircraftType type = aircraftTypes[assignment.assignedTypeIndex];

        // Route to correct restore method based on manager type
        int restored = 0;
        if (assignment.isBot && assignment.botWeaponManager != null)
        {
            restored = RestoreBotMissiles(assignment.botWeaponManager, type);
        }
        else if (!assignment.isBot && assignment.playerWeaponManager != null)
        {
            restored = RestorePlayerMissiles(assignment.playerWeaponManager, type);
        }
        else
        {
            Debug.LogError($"[LoadoutManager] No valid weapon manager for {aircraft.name}");
            return 0;
        }

        if (showDebugInfo)
        {
            string aircraftType = assignment.isBot ? "Bot" : "Player";
            Debug.Log($"[LoadoutManager] Restored {restored} missiles for {aircraft.name} [{aircraftType}] ({type.typeName})");
        }

        return restored;
    }

    // ============ PLAYER WEAPON RESTORATION ============
    int RestorePlayerMissiles(WeaponManager wm, AircraftType type)
    {
        if (wm == null || wm.missilePylons == null) return 0;

        ClearPlayerMissiles(wm);

        int totalRestored = 0;
        int pylonCount = Mathf.Min(wm.missilePylons.Length, type.pylons.Length);

        for (int i = 0; i < pylonCount; i++)
        {
            MissilePylon pylon = wm.missilePylons[i];
            MissileLoadout loadout = type.pylons[i];

            if (pylon == null || loadout == null || loadout.missilePrefab == null)
                continue;

            // Create left missile
            if (loadout.missileCount >= 1 && pylon.leftPylon != null)
            {
                CreateMissile(loadout.missilePrefab, pylon.leftPylon, loadout.leftMissileName);
                totalRestored++;
            }

            // Create right missile
            if (loadout.missileCount >= 2 && pylon.rightPylon != null)
            {
                CreateMissile(loadout.missilePrefab, pylon.rightPylon, loadout.rightMissileName);
                totalRestored++;
            }
        }

        return totalRestored;
    }

    void ClearPlayerMissiles(WeaponManager wm)
    {
        foreach (MissilePylon pylon in wm.missilePylons)
        {
            if (pylon == null) continue;
            ClearPylon(pylon.leftPylon);
            ClearPylon(pylon.rightPylon);
        }
    }

    // ============ BOT WEAPON RESTORATION ============
    int RestoreBotMissiles(BotWeaponManager bwm, AircraftType type)
    {
        if (bwm == null || bwm.missilePylons == null) return 0;

        ClearBotMissiles(bwm);

        int totalRestored = 0;
        int pylonCount = Mathf.Min(bwm.missilePylons.Length, type.pylons.Length);

        for (int i = 0; i < pylonCount; i++)
        {
            MissilePylon pylon = bwm.missilePylons[i];
            MissileLoadout loadout = type.pylons[i];

            if (pylon == null || loadout == null || loadout.missilePrefab == null)
                continue;

            // Create left missile
            if (loadout.missileCount >= 1 && pylon.leftPylon != null)
            {
                CreateMissile(loadout.missilePrefab, pylon.leftPylon, loadout.leftMissileName);
                totalRestored++;
            }

            // Create right missile
            if (loadout.missileCount >= 2 && pylon.rightPylon != null)
            {
                CreateMissile(loadout.missilePrefab, pylon.rightPylon, loadout.rightMissileName);
                totalRestored++;
            }
        }

        return totalRestored;
    }

    void ClearBotMissiles(BotWeaponManager bwm)
    {
        foreach (MissilePylon pylon in bwm.missilePylons)
        {
            if (pylon == null) continue;
            ClearPylon(pylon.leftPylon);
            ClearPylon(pylon.rightPylon);
        }
    }

    // ============ SHARED UTILITY METHODS ============
    void ClearPylon(Transform pylon)
    {
        if (pylon == null) return;

        while (pylon.childCount > 0)
        {
            DestroyImmediate(pylon.GetChild(0).gameObject);
        }
    }

    void CreateMissile(GameObject prefab, Transform parent, string name)
    {
        if (prefab == null || parent == null) return;

        GameObject missile = Instantiate(prefab, parent);
        missile.name = name;
        missile.transform.localPosition = Vector3.zero;
        missile.transform.localRotation = Quaternion.identity;
        missile.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Get total missile count for an aircraft (works for both player and bots)
    /// </summary>
    public int GetMissileCount(GameObject aircraft)
    {
        foreach (var assignment in sceneAircraft)
        {
            if (assignment.aircraftObject != aircraft) continue;

            int count = 0;

            if (assignment.isBot && assignment.botWeaponManager != null)
            {
                foreach (var pylon in assignment.botWeaponManager.missilePylons)
                {
                    if (pylon != null)
                        count += pylon.GetTotalMissiles();
                }
            }
            else if (!assignment.isBot && assignment.playerWeaponManager != null)
            {
                foreach (var pylon in assignment.playerWeaponManager.missilePylons)
                {
                    if (pylon != null)
                        count += pylon.GetTotalMissiles();
                }
            }

            return count;
        }
        return 0;
    }

    // Static accessor
    public static AircraftLoadoutManager Instance => instance;

    // ============ HELPER METHODS TO ADD AIRCRAFT TYPES ============

    [ContextMenu("Add F-16 Standard Type")]
    public void AddF16StandardType()
    {
        AddAircraftType("F-16", new string[]
        {
            "us_aim-9x", "us_aim-9x (1)",
            "aim-120", "aim-120 (1)",
            "aim-7", "aim-7 (1)"
        });
    }

    [ContextMenu("Add F-15 Eagle Type")]
    public void AddF15StandardType()
    {
        AddAircraftType("F-15", new string[]
        {
            "aim-9", "aim-9 (1)",
            "aim-120", "aim-120 (1)",
            "aim-7", "aim-7 (1)"
        });
    }

    [ContextMenu("Add F-18 Hornet Type")]
    public void AddF18StandardType()
    {
        AddAircraftType("F-18", new string[]
        {
            "aim-9x", "aim-9x (1)",
            "aim-120", "aim-120 (1)",
            "aim-7", "aim-7 (1)"
        });
    }

    [ContextMenu("Add Su-27 Flanker Type")]
    public void AddSu27StandardType()
    {
        AddAircraftType("Su-27", new string[]
        {
            "r-73", "r-73 (1)",
            "r-77", "r-77 (1)",
            "r-27", "r-27 (1)"
        });
    }

    void AddAircraftType(string typeName, string[] missileNames)
    {
        // Expand array
        if (aircraftTypes == null)
        {
            aircraftTypes = new AircraftType[1];
        }
        else
        {
            AircraftType[] newArray = new AircraftType[aircraftTypes.Length + 1];
            for (int i = 0; i < aircraftTypes.Length; i++)
            {
                newArray[i] = aircraftTypes[i];
            }
            aircraftTypes = newArray;
        }

        int index = aircraftTypes.Length - 1;
        aircraftTypes[index] = new AircraftType
        {
            typeName = typeName,
            pylons = new MissileLoadout[3]
        };

        // Configure 3 pylons
        for (int i = 0; i < 3; i++)
        {
            aircraftTypes[index].pylons[i] = new MissileLoadout
            {
                missileCount = 2,
                leftMissileName = missileNames[i * 2],
                rightMissileName = missileNames[i * 2 + 1]
            };
        }

        Debug.Log($"[LoadoutManager] {typeName} type added! Assign missile prefabs in inspector.");
    }
}