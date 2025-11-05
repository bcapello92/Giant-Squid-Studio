using UnityEngine;

public abstract class RoomEnemy : MonoBehaviour
{
    protected RoomManager roomManager;
    bool reportedDead = false;

    public void SetRoomManager(RoomManager rm)
    {
        roomManager = rm;
    }

    /// <summary>
    /// Call this exactly once when the enemy is actually dead.
    /// It will notify the room and prevent double reporting.
    /// </summary>
    protected void DieInRoom()
    {
        if (reportedDead) return;
        reportedDead = true;

        if (roomManager != null)
            roomManager.OnEnemyDied(this);
    }
}
