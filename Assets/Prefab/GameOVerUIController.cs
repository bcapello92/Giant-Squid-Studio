using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System;

[RequireComponent(typeof(UIDocument))]
public class GameOverUIControllerUITK : MonoBehaviour
{
    [Header("Scenes (exact names in Build Settings)")]
    public string level1SceneName = "Level 1";
    public string mainMenuSceneName = "Title Scene";

    [Header("Behavior")]
    public float fadeInTime = 0.35f;

    UIDocument doc;
    VisualElement root;
    Button restartBtn, menuBtn;

    bool prevCursorVisible;
    CursorLockMode prevLock;

    void Awake() { doc = GetComponent<UIDocument>(); }

    void OnEnable()
    {
        root = doc.rootVisualElement;
        restartBtn = root.Q<Button>("RestartBtn");
        menuBtn = root.Q<Button>("MainMenuBtn");

        // Button-only interaction
        if (restartBtn != null) restartBtn.clicked += OnRestart;
        if (menuBtn != null) menuBtn.clicked += OnMenu;

        // Fade + pause + cursor unlock
        root.style.opacity = 0f;
        StartCoroutine(FadeIn());
        Time.timeScale = 0f;
        prevCursorVisible = UnityEngine.Cursor.visible;
        prevLock = UnityEngine.Cursor.lockState;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        // Don’t accidentally trigger Restart with Enter; focus nothing or Menu:
        menuBtn?.Focus();
    }

    void OnDisable()
    {
        if (restartBtn != null) restartBtn.clicked -= OnRestart;
        if (menuBtn != null) menuBtn.clicked -= OnMenu;

        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;
        UnityEngine.Cursor.visible = prevCursorVisible;
        UnityEngine.Cursor.lockState = prevLock;
    }

    System.Collections.IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            root.style.opacity = new StyleFloat(Mathf.SmoothStep(0f, 1f, t / fadeInTime));
            yield return null;
        }
        root.style.opacity = 1f;
        root.pickingMode = PickingMode.Position;
    }

    void OnRestart()
    {
        LoadSceneThen(level1SceneName, () =>
        {
            // re-enable gameplay and restart the run AFTER Level1 is loaded
            RunManager.I?.StartNewRun();
        });
    }

    void OnMenu()
    {
        LoadSceneThen(mainMenuSceneName, () =>
        {
            // stop gameplay input while on menu
            if (RunManager.I != null)
            {
                // ensure your RunManager won’t advance rooms on menu
                // (if you added IsRunActive, flip it off; otherwise, you can add a StopRun() API)
               RunManager.I?.StopRun(); // make this 'internal set' or expose a StopRun()
            }
        });
    }

    void LoadSceneThen(string sceneName, Action after)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameOverUI] Scene name not set.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[GameOverUI] Scene '{sceneName}' is not in Build Settings.");
            return;
        }

        // restore timescale BEFORE scene load
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;

        // run callback when the requested scene finishes loading
        void Handler(Scene s, LoadSceneMode m)
        {
            if (s.name == sceneName)
            {
                SceneManager.sceneLoaded -= Handler;
                after?.Invoke();
            }
        }
        SceneManager.sceneLoaded += Handler;

        SceneManager.LoadScene(sceneName);
        Destroy(gameObject); // remove the overlay
    }
}
