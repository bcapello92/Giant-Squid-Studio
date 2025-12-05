using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class RunSceneBootstrap_Level2 : MonoBehaviour
{
    IEnumerator Start()
    {
        // give RoomManager/Player/Camera one frame to come alive
        yield return null;

        if (RunManager.I == null)
        {
            Debug.LogError("[RunBootstrap_Level2] No RunManager in memory. " +
                           "Make sure RunManager lives in a bootstrap/menu scene and is DontDestroyOnLoad.");
            yield break;
        }

        // Start the *second* boss run using the Level 2 configs
        RunManager.I.StartSecondRun();
        Debug.Log("[RunBootstrap_Level2] StartSecondRun issued.");
    }
}
