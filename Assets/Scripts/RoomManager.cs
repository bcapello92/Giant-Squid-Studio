using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    [Header("Optional fallback (only used if room has no entrance marker)")]
    [Tooltip("If a room has no DoorEntranceMarker, we’ll spawn the player here. Can be left null.")]
    public Transform playerSpawnPoint;

    [Header("Room root (where room instances go)")]
    public Transform roomRoot;
    public GameObject powerupPrefab;

    // ----- Power-up spawn near exit door -----
    [Header("Power-up Spawn @ Exit Door")]
    [Tooltip("If a child with this name exists under the exit door, spawn exactly there.")]
    public string pickupMarkerName = "PickupSpawn";

    [Tooltip("Offset from the door position if no marker is found.")]
    public Vector2 powerupOffset = new Vector2(0f, 1.0f);

    [Tooltip("Layers to avoid when placing the pickup (e.g., Walls | Enemy | Player). 0 = skip check.")]
    public LayerMask avoidLayers;

    [Tooltip("Quick overlap check radius at the candidate spawn point.")]
    public float spawnClearRadius = 0.25f;

    [Tooltip("How far to nudge if the first candidate is blocked.")]
    public float nudgeStep = 0.15f;

    [Tooltip("How many nudges to try before giving up and using the base position.")]
    public int nudgeTries = 10;

    // ----- Runtime -----
    GameObject currentRoomInstance;
    readonly List<RoomEnemy> liveEnemies = new List<RoomEnemy>();
    DoorController currentExitDoor;                   // for pickup spawn
    DoorController[] allDoorsInRoom;

    void Awake()
    {
        if (!roomRoot) roomRoot = transform;
    }

    // =========================================================
    //                       Room Loading
    // =========================================================
    public void LoadRoom(GameObject roomPrefab)
    {
        // 1) Clear previous room
        if (currentRoomInstance) Destroy(currentRoomInstance);
        liveEnemies.Clear();
        currentExitDoor = null;
        allDoorsInRoom = null;

        // 2) Spawn new room under root
        currentRoomInstance = Instantiate(roomPrefab, roomRoot);
        currentRoomInstance.name = roomPrefab.name;

        // 3) Find entrance marker (if any)
        DoorEntranceMarker entrance = currentRoomInstance.GetComponentInChildren<DoorEntranceMarker>(true);

        // 4) Doors: lock & wire to this RoomManager
        allDoorsInRoom = currentRoomInstance.GetComponentsInChildren<DoorController>(true);
        foreach (var d in allDoorsInRoom)
        {
            if (!d) continue;
            d.SetRoomManager(this);
            d.Lock(); // locked until room is cleared
            // Pick the first door as the exit door for pickup spawn, unless you have a custom flag.
            if (!currentExitDoor) currentExitDoor = d;
        }

        // 5) Pre-existing enemies: register
        var enemies = currentRoomInstance.GetComponentsInChildren<RoomEnemy>(true);
        foreach (var e in enemies)
            RegisterEnemy(e);

        // 6) Wave spawners: configure & begin
        int difficulty = 1;
        if (RunManager.I != null) difficulty = RunManager.I.GetCurrentDifficulty();

        var spawners = currentRoomInstance.GetComponentsInChildren<EnemyWaveSpawnerPoisson>(true);
        foreach (var sp in spawners)
        {
            if (!sp) continue;
            sp.SetDifficulty(difficulty);
            sp.SetRoomManager(this);
            sp.BeginSpawning(); // make sure BeginSpawning() has its own "started" guard internally
        }

        // 7) Move player to entrance (or fallback)
        var player = FindObjectOfType<TestPlayerController>();
        if (player)
        {
            if (entrance) player.transform.position = entrance.GetSpawnPosition();
            else if (playerSpawnPoint) player.transform.position = playerSpawnPoint.position;
        }

        // 8) Camera follow target = player
        var cam = Camera.main;
        if (cam)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow && player) follow.SetTarget(player.transform);
        }
    }

    // =========================================================
    //                   Enemy Registration & Clear
    // =========================================================
    public void RegisterEnemy(RoomEnemy enemy)
    {
        if (!enemy) return;
        if (liveEnemies.Contains(enemy)) return;
        liveEnemies.Add(enemy);
        enemy.SetRoomManager(this);
    }

    public void OnEnemyDied(RoomEnemy enemy)
    {
        if (enemy && liveEnemies.Contains(enemy))
            liveEnemies.Remove(enemy);

        if (liveEnemies.Count == 0)
            OnRoomCleared();
    }

    // =========================================================
    //                  Room Clear: Heal + Unlock + Pickup
    // =========================================================
    void OnRoomCleared()
    {
        // 1) Unlock all doors
        if (allDoorsInRoom != null)
            foreach (var d in allDoorsInRoom) if (d) d.Unlock();

        // 2) Heal player (+ RunManager mirror)
        const int clearHeal = 10;
        var player = FindObjectOfType<TestPlayerController>();
        if (player) player.Heal(clearHeal);
        RunManager.I?.Heal(clearHeal);

        // 3) Spawn power-up at the exit door
        if (powerupPrefab && currentExitDoor)
        {
            Vector3 pos = GetPowerupSpawnPosition(currentExitDoor.transform);
            Instantiate(powerupPrefab, pos, Quaternion.identity);
        }
        else
        {
            // Optional logs to help diagnose config issues
            if (!powerupPrefab) Debug.LogWarning("[Room] powerupPrefab not assigned.");
            if (!currentExitDoor) Debug.LogWarning("[Room] No exit door available for pickup spawn.");
        }
    }

    // Called by DoorController when player uses the exit
    public void OnExitDoorUsed()
    {
        // Gate by run state if you use one
        if (RunManager.I != null && !RunManager.I.runActive) return;
        RunManager.I?.GoToNextRoom();
    }

    // =========================================================
    //                  Power-up Placement Helper
    // =========================================================
    Vector3 GetPowerupSpawnPosition(Transform door)
    {
        // Prefer a named child marker on the door
        Transform marker = door.Find(pickupMarkerName);
        Vector3 basePos = marker ? marker.position
                                 : (Vector3)((Vector2)door.position + powerupOffset);

        if (avoidLayers.value == 0)
            return basePos;

        // If blocked, nudge around in 90° steps (simple + predictable)
        bool blocked = Physics2D.OverlapCircle(basePos, spawnClearRadius, avoidLayers);
        if (!blocked) return basePos;

        float angle = 0f;
        for (int i = 0; i < nudgeTries; i++)
        {
            angle += Mathf.PI * 0.5f; // 90° step
            float radius = nudgeStep * (1 + i * 0.5f);
            Vector2 off = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector3 candidate = basePos + (Vector3)off;

            if (!Physics2D.OverlapCircle(candidate, spawnClearRadius, avoidLayers))
                return candidate;
        }

        // Fallback if all tries blocked
        return basePos;
    }

    // =========================================================
    //                         Gizmos
    // =========================================================
    void OnDrawGizmosSelected()
    {
        if (currentExitDoor)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.35f);
            Gizmos.DrawWireSphere(GetPowerupSpawnPosition(currentExitDoor.transform), spawnClearRadius);
        }
    }
}
