using UnityEngine;
using System.Collections;


[DefaultExecutionOrder(1000)] // run after most Awakes/Starts
public class RunSceneBootstrap : MonoBehaviour
{
    IEnumerator Start()
    {
        // give RoomManager/Player/Camera one frame to come alive
        yield return null;

        if (RunManager.I == null)
        {
            Debug.LogError("[RunBootstrap] No RunManager in memory. Ensure a singleton RunManager exists in a bootstrap/menu scene and is DontDestroyOnLoad.");
            yield break;
        }

        // Start a fresh run every time Level 1 loads
        RunManager.I.StartNewRun();
        Debug.Log("[RunBootstrap] StartNewRun issued.");
    }
}
