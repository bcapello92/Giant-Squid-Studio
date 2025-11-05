using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoorController : MonoBehaviour
{
    bool locked = true;
    RoomManager roomManager;

    public void SetRoomManager(RoomManager rm) => roomManager = rm;

    public void Lock()
    {
        locked = true;
        // TODO: swap sprite to locked
    }

    public void Unlock()
    {
        locked = false;
        // TODO: swap sprite to unlocked
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (locked) return;
        if (!other.CompareTag("Player")) return;

        // player uses door → tell room
        if (roomManager != null)
            roomManager.OnExitDoorUsed();
    }
}
