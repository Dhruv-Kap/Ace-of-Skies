using UnityEngine;

/// <summary>
/// Simplified Loadout Manager – handles only F-16 loadouts
/// Works for both Player and Bots
/// Restores missile prefabs and models correctly after respawn
/// </summary>
public class AircraftLoadoutManager : MonoBehaviour
{
    [Header("Missile Loadouts (F-16)")]
    public MissileLoadout[] pylons = new MissileLoadout[3]; // AIM-9, AIM-120, AIM-7

    [Header("Scene Aircraft")]
    public AircraftAssignment[] sceneAircraft;

    private static AircraftLoadoutManager instance;
    public static AircraftLoadoutManager Instance => instance;

    [System.Serializable]
    public class MissileLoadout
    {
        public GameObject missilePrefab;   // Used for firing
        public GameObject modelPrefab;     // Visual display
        public int missileCount = 2;
        public string leftMissileName = "missile";
        public string rightMissileName = "missile (1)";
    }

    [System.Serializable]
    public class AircraftAssignment
    {
        public GameObject aircraftObject;
        public bool isBot = false;

        [Header("Runtime Info")]
        public WeaponManager playerWeaponManager;
        public BotWeaponManager botWeaponManager;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else
        {
            Destroy(this);
            return;
        }

        InitializeAssignments();
    }

    void InitializeAssignments()
    {
        foreach (var a in sceneAircraft)
        {
            if (a.aircraftObject == null) continue;

            a.playerWeaponManager = a.aircraftObject.GetComponent<WeaponManager>();
            if (a.playerWeaponManager == null)
                a.playerWeaponManager = a.aircraftObject.GetComponentInChildren<WeaponManager>();

            a.botWeaponManager = a.aircraftObject.GetComponent<BotWeaponManager>();
            if (a.botWeaponManager == null)
                a.botWeaponManager = a.aircraftObject.GetComponentInChildren<BotWeaponManager>();

            a.isBot = a.botWeaponManager != null;
        }
    }

    // ==================================================
    //  RESTORE LOADOUTS
    // ==================================================
    public void RestoreLoadout(GameObject aircraft)
    {
        if (aircraft == null) return;

        AircraftAssignment assignment = System.Array.Find(sceneAircraft, a => a.aircraftObject == aircraft);
        if (assignment == null)
        {
            Debug.LogError($"[LoadoutManager] {aircraft.name} not registered!");
            return;
        }

        if (assignment.isBot)
            RestoreBotMissiles(assignment.botWeaponManager);
        else
            RestorePlayerMissiles(assignment.playerWeaponManager);
    }

    void RestorePlayerMissiles(WeaponManager wm)
    {
        if (wm == null || wm.missilePylons == null) return;

        ClearPylons(wm.missilePylons);

        for (int i = 0; i < pylons.Length; i++)
        {
            MissileLoadout loadout = pylons[i];
            MissilePylon pylon = wm.missilePylons[i];
            if (pylon == null || loadout == null) continue;

            if (loadout.missileCount >= 1 && loadout.missilePrefab && pylon.leftPylon)
                CreateMissile(loadout.missilePrefab, pylon.leftPylon, loadout.leftMissileName);
            if (loadout.missileCount >= 2 && loadout.missilePrefab && pylon.rightPylon)
                CreateMissile(loadout.missilePrefab, pylon.rightPylon, loadout.rightMissileName);

            if (loadout.modelPrefab)
            {
                if (pylon.leftPylon)
                    CreateMissile(loadout.modelPrefab, pylon.leftPylon, "Model_" + loadout.leftMissileName);
                if (pylon.rightPylon)
                    CreateMissile(loadout.modelPrefab, pylon.rightPylon, "Model_" + loadout.rightMissileName);
            }
        }
    }

    void RestoreBotMissiles(BotWeaponManager bwm)
    {
        if (bwm == null || bwm.missilePylons == null) return;

        ClearPylons(bwm.missilePylons);

        for (int i = 0; i < pylons.Length; i++)
        {
            MissileLoadout loadout = pylons[i];
            MissilePylon pylon = bwm.missilePylons[i];
            if (pylon == null || loadout == null) continue;

            if (loadout.missileCount >= 1 && loadout.missilePrefab && pylon.leftPylon)
                CreateMissile(loadout.missilePrefab, pylon.leftPylon, loadout.leftMissileName);
            if (loadout.missileCount >= 2 && loadout.missilePrefab && pylon.rightPylon)
                CreateMissile(loadout.missilePrefab, pylon.rightPylon, loadout.rightMissileName);

            if (loadout.modelPrefab)
            {
                if (pylon.leftPylon)
                    CreateMissile(loadout.modelPrefab, pylon.leftPylon, "Model_" + loadout.leftMissileName);
                if (pylon.rightPylon)
                    CreateMissile(loadout.modelPrefab, pylon.rightPylon, "Model_" + loadout.rightMissileName);
            }
        }
    }

    // ==================================================
    //  UTILITY
    // ==================================================
    void ClearPylons(MissilePylon[] pylons)
    {
        foreach (var p in pylons)
        {
            if (p == null) continue;
            ClearChildren(p.leftPylon);
            ClearChildren(p.rightPylon);
        }
    }

    void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        while (parent.childCount > 0)
            DestroyImmediate(parent.GetChild(0).gameObject);
    }

    void CreateMissile(GameObject prefab, Transform parent, string name)
    {
        if (!prefab || !parent) return;
        GameObject missile = Instantiate(prefab, parent);
        missile.name = name;
        missile.transform.localPosition = Vector3.zero;
        missile.transform.localRotation = Quaternion.identity;
        missile.transform.localScale = Vector3.one;
    }
}
