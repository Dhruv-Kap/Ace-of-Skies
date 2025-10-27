// ==================== COLLISION DETECTOR COMPONENT ====================

using UnityEngine;

/// <summary>
/// Automatically added to player/bots to detect mesh collisions
/// </summary>
public class CollisionDeathDetector : MonoBehaviour
{
    private RespawnManager respawnManager;
    private bool isPlayer;
    private LayerMask deathLayers;
    private bool isInitialized = false;

    public void Initialize(RespawnManager manager, bool playerFlag, LayerMask layers)
    {
        respawnManager = manager;
        isPlayer = playerFlag;
        deathLayers = layers;
        isInitialized = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isInitialized || respawnManager == null) return;

        Collider collider = collision.collider;
        bool isMeshCollider = collider is MeshCollider;
        bool isTerrainCollider = collider is TerrainCollider;

        bool layerCheck = ((1 << collision.gameObject.layer) & deathLayers) != 0;

        Debug.Log($"[CollisionDeathDetector] {gameObject.name} collided with {collision.gameObject.name} ({collision.gameObject.layer}) | Mesh: {isMeshCollider}, Terrain: {isTerrainCollider} | Layer check: {layerCheck} | isPlayer: {isPlayer}");

        if ((isMeshCollider || isTerrainCollider) && layerCheck)
        {
            Debug.Log("[CollisionDeathDetector] You should die now!");
            respawnManager.HandleCollisionDeath(gameObject, isPlayer);
        }
    }



}