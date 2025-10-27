using UnityEngine;

public class DeathZoneTrigger : MonoBehaviour
{
    [Header("Respawn Manager Reference")]
    public RespawnManager respawnManager;

    [Header("Debug Settings")]
    public bool showDebug = true;

    private void OnTriggerEnter(Collider other)
    {
        if (respawnManager == null) return;

        GameObject root = other.transform.root.gameObject;

        if (root.CompareTag("Player"))
        {
            respawnManager.HandleCollisionDeath(root, true);
            if (showDebug)
                Debug.Log($"[DeathZoneTrigger] Player entered death zone: {root.name}");
        }
        else if (root.CompareTag("Bot"))
        {
            respawnManager.HandleCollisionDeath(root, false);
            if (showDebug)
                Debug.Log($"[DeathZoneTrigger] Bot entered death zone: {root.name}");
        }
    }
}
