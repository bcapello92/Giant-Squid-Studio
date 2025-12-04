using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class GameOverUIControllerUITK : MonoBehaviour
{
    [Header("Scenes (exact names in Build Settings)")]
    public string mainMenuSceneName = "Title Scene";

    [Header("Behavior")]
    public float fadeInTime = 0.35f;

    UIDocument doc;
    VisualElement root;
    Button restartBtn, menuBtn;

    bool prevCursorVisible;
    CursorLockMode prevLock;

    void Awake()
    {
        doc = GetComponent<UIDocument>();
    }

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

        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;

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

    // -------------------------------------------------------
    // Buttons
    // -------------------------------------------------------

    void OnRestart()
    {
        // Unpause first
        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;

        if (RunManager.I != null)
        {
            // Restart a fresh run. If you want to always go back to Level 1:
            RunManager.I.StartRunAtLevel(1);

           
        }
        else
        {
            // Fallback: if somehow there's no RunManager, reload the current scene
            Debug.LogWarning("[GameOverUI] No RunManager found on restart. Reloading active scene.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        Destroy(gameObject);
    }

    void OnMenu()
    {
        // Stop gameplay and go to main menu
        if (RunManager.I != null)
            RunManager.I.StopRun();

        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;

        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("[GameOverUI] mainMenuSceneName not set.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"[GameOverUI] Scene '{mainMenuSceneName}' is not in Build Settings.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
        Destroy(gameObject);
    }
}
