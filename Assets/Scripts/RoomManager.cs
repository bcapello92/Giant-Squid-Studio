using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    [Header("Optional fallback")]
    [Tooltip("If a room has no DoorEntranceMarker, we'll spawn the player here. Can be left null.")]
    public Transform playerSpawnPoint;   // <-- now truly optional

    [Header("Room root (where prefabs go)")]
    public Transform roomRoot;
    public GameObject powerupPrefab;

    GameObject currentRoomInstance;
    readonly List<RoomEnemy> liveEnemies = new List<RoomEnemy>();
    DoorController currentDoorOut;


    void Awake()
    {
        if (roomRoot == null)
            roomRoot = this.transform; // safe default
    }

    public void LoadRoom(GameObject roomPrefab)
    {
        // 1) Clear previous room
        if (currentRoomInstance != null)
            Destroy(currentRoomInstance);

        liveEnemies.Clear();
        currentDoorOut = null;

        // 2) Spawn new room under root
        currentRoomInstance = Instantiate(roomPrefab, roomRoot);
        currentRoomInstance.name = roomPrefab.name;

        // 3) Find entrance & exit
        var entrance = currentRoomInstance.GetComponentInChildren<DoorEntranceMarker>(true);

        currentDoorOut = currentRoomInstance.GetComponentInChildren<DoorController>(true);
        if (currentDoorOut != null)
        {
            currentDoorOut.Lock();
            currentDoorOut.SetRoomManager(this);
        }

        // 4) Register enemies (ONCE)
        var enemies = currentRoomInstance.GetComponentsInChildren<RoomEnemy>(true);
        foreach (var e in enemies)
        {
            if (e == null) continue;
            liveEnemies.Add(e);
            e.SetRoomManager(this);
        }

        // 5) Configure spawners (no BeginSpawning yet)
        int difficulty = RunManager.I ? RunManager.I.GetCurrentDifficulty() : 1;
        var spawners = currentRoomInstance.GetComponentsInChildren<EnemyWaveSpawnerPoisson>(true);
        foreach (var sp in spawners)
        {
            if (!sp) continue;
            sp.SetDifficulty(difficulty);
            sp.SetRoomManager(this); // lets them RegisterEnemy() back
                                     // DO NOT call BeginSpawning yet; wait one frame so their Awake/Start run.
        }

        // 6) Move player and hook camera
        var player = FindObjectOfType<TestPlayerController>();
        if (player != null)
        {
            if (entrance != null)
                player.transform.position = entrance.GetSpawnPosition();
            else if (playerSpawnPoint != null)
                player.transform.position = playerSpawnPoint.position;

            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null) follow.SetTarget(player.transform);
            }
        }

        // 7) Start spawners on the next frame (lets all Awake/Start complete safely)
        StartCoroutine(BeginSpawnersNextFrame(spawners));
    }

    IEnumerator BeginSpawnersNextFrame(EnemyWaveSpawnerPoisson[] spawners)
    {
        yield return null; // wait 1 frame
        foreach (var sp in spawners)
            if (sp) sp.BeginSpawning();
    }


    public void OnEnemyDied(RoomEnemy enemy)
    {
        if (enemy != null && liveEnemies.Contains(enemy))
            liveEnemies.Remove(enemy);

        if (liveEnemies.Count == 0)
            OnRoomCleared();
    }

    void OnRoomCleared()
    {
        if (currentDoorOut != null)
            currentDoorOut.Unlock();

        if (powerupPrefab != null)
        {
            var player = FindObjectOfType<TestPlayerController>();
            Vector3 pos = player ? player.transform.position : roomRoot.position;
            Instantiate(powerupPrefab, pos, Quaternion.identity);
        }
    }

    public void OnExitDoorUsed()
    {
        if (RunManager.I != null && !RunManager.I.runActive) return;
        RunManager.I?.GoToNextRoom();
    }
    public void RegisterEnemy(RoomEnemy enemy)
    {
        if (enemy == null) return;
        if (!liveEnemies.Contains(enemy))
        {
            liveEnemies.Add(enemy);
            enemy.SetRoomManager(this);
        }
    }


}
