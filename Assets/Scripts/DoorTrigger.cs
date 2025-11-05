using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [Tooltip("Optional: assign the next scene name, or the prefab to load.")]
    public string destinationScene;
    public Transform destinationPoint;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("[Door] Player entered door: " + name);
            // You can handle transitions here
            // Example:
            // SceneManager.LoadScene(destinationScene);
        }
    }
}
