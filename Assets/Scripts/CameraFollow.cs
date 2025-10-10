using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player; // Assign your player GameObject in the Inspector
    private Vector3 offset;

    void Start()
    {
        if (player != null)
        {
            offset = transform.position - player.position;
        }
        else
        {
            Debug.LogWarning("Player not assigned to CameraFollow script!");
        }
    }

    void LateUpdate()
    {
        if (player != null)
        {
            transform.position = player.position + offset;
        }
    }
}