using UnityEngine;
using System.Collections.Generic;

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
        // 1) clear previous room
        if (currentRoomInstance != null)
            Destroy(currentRoomInstance);

        liveEnemies.Clear();
        currentDoorOut = null;

        // 2) spawn new room under root
        currentRoomInstance = Instantiate(roomPrefab, roomRoot);
        currentRoomInstance.name = roomPrefab.name;

        // 3) look for entrance marker INSIDE this room
        DoorEntranceMarker entrance = currentRoomInstance.GetComponentInChildren<DoorEntranceMarker>(true);

        // 4) look for exit door to lock
        currentDoorOut = currentRoomInstance.GetComponentInChildren<DoorController>(true);
        if (currentDoorOut != null)
        {
            currentDoorOut.Lock();
            currentDoorOut.SetRoomManager(this);
        }

        // 5) register enemies
        var enemies = currentRoomInstance.GetComponentsInChildren<RoomEnemy>(true);
        foreach (var e in enemies)
        {
            liveEnemies.Add(e);
            e.SetRoomManager(this);
        }

        // 6) move player
        var player = FindObjectOfType<TestPlayerController>();
        if (player != null)
        {
            if (entrance != null)
            {
                player.transform.position = entrance.GetSpawnPosition();
            }
            else if (playerSpawnPoint != null)
            {
                player.transform.position = playerSpawnPoint.position;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null)
                    follow.SetTarget(player.transform);
            }
        }
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
        if (RunManager.I != null)
            RunManager.I.GoToNextRoom();
    }
}
