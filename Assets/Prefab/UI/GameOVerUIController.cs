using System;
using System.Collections;                 // for IEnumerator
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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

        if (restartBtn != null) restartBtn.clicked += OnRestart;
        if (menuBtn != null) menuBtn.clicked += OnMenu;

        // Fade in + pause + cursor unlock
        root.style.opacity = 0f;
        StartCoroutine(FadeIn());

        Time.timeScale = 0f;
        prevCursorVisible = UnityEngine.Cursor.visible;
        prevLock = UnityEngine.Cursor.lockState;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        // Focus menu by default so Enter doesn't accidentally restart
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

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            root.style.opacity = new StyleFloat(Mathf.SmoothStep(0f, 1f, t / fadeInTime));
            yield return null;
        }
        root.style.opacity = 1f;
        root.pickingMode = PickingMode.Position; // accept clicks immediately
    }

    void OnRestart()
    {
        // Stop any current run; Level 1's RunSceneBootstrap will StartNewRun()
        RunManager.I?.StopRun();
        LoadSceneThen(level1SceneName, after: null);
    }

    void OnMenu()
    {
        // Stop gameplay and go to main menu; no run starts here
        RunManager.I?.StopRun();
        LoadSceneThen(mainMenuSceneName, after: null);
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

        // Unpause BEFORE loading the new scene
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;

        void Handler(Scene s, LoadSceneMode m)
        {
            if (s.name != sceneName) return;
            SceneManager.sceneLoaded -= Handler;

            // Give one frame if you need to do anything after load
            if (after != null)
            {
                // run deferred
                StartCoroutine(Deferred(after));
            }
        }
        SceneManager.sceneLoaded += Handler;

        SceneManager.LoadScene(sceneName);
        Destroy(gameObject); // remove the overlay
    }

    IEnumerator Deferred(Action after)
    {
        yield return null;
        after?.Invoke();
    }
}
