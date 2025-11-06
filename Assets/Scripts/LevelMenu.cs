using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;


public class LevelMenu : MonoBehaviour
{

    public Button[] buttons;
    private void Awake()
    {
        int unlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 1);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].interactable = false;
        }
        for (int i = 0; i < unlockedLevel; i++)
        {
            buttons[i].interactable = true;
        }   
    }
    public void OpenLevel(int levelId)
    {
        string levelName = "Level " + levelId;
        StartCoroutine(LoadAndStart(levelName));
    }

    IEnumerator LoadAndStart(string levelName)
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;//check for paused ingame menu

        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Single);
        if(op == null)
        {
            Debug.LogError($"[LevelMenu] Failed to start loading '{levelName}'");
            yield break;
        }

        while(!op.isDone) yield return null;

        yield return null;

        if (RunManager.I != null)
        {
            RunManager.I.StartNewRun(); // will re-find RoomManager and load the first room
        }
        else
        {
            Debug.LogWarning("[LevelMenu] No RunManager in memory. Make sure a RunManager exists in a bootstrap scene and is DontDestroyOnLoad.");
        }
    }
}
