using System.Collections;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelMenu : MonoBehaviour
{
    public Button[] buttons;

    [Header("Run Manager")]
    [Tooltip("Prefab with RunManager on it (DontDestroyOnLoad).")]
    public RunManager runManagerPrefab;

    private void Awake()
    {
        int unlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 2);

        for (int i = 0; i < buttons.Length; i++)
            buttons[i].interactable = false;

        for (int i = 0; i < unlockedLevel && i < buttons.Length; i++)
            buttons[i].interactable = true;
    }

    void EnsureRunManager()
    {
        if (RunManager.I == null)
        {
            // Spawn the persistent RunManager
            Instantiate(runManagerPrefab);
        }
    }

    public void OpenLevel(int levelId)
    {
        EnsureRunManager();
        RunManager.I.StartRunAtLevel(levelId);
    }
}
