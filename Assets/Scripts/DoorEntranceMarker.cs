using UnityEngine;

public class DoorEntranceMarker : MonoBehaviour
{
    [Header("Spawn inside the room")]
    [SerializeField] float forwardOffset = 1.0f;  // set a REAL default
    [SerializeField] Vector3 extraOffset = Vector3.zero;

    // pick world-forward direction: your door's Y points OUT, so go opposite
    public Vector3 GetSpawnPosition()
    {
        Vector3 forward = transform.up;  // into the room
        return transform.position + forward * forwardOffset + extraOffset;
    }
}
