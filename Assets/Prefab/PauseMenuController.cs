using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PauseMenuController : MonoBehaviour
{
    UIDocument doc;
    VisualElement root;          // "pause-root"
    VisualElement panel;         // "panel"
    Button resumeBtn, restartBtn, menuBtn, quitBtn;
    bool isPaused;
    bool prevCursorVisible;
    CursorLockMode prevLock;

    void Awake()
    {
        doc = GetComponent<UIDocument>();
        root = doc.rootVisualElement.Q<VisualElement>("pause-root");
        panel = doc.rootVisualElement.Q<VisualElement>("panel");

        resumeBtn = doc.rootVisualElement.Q<Button>("resume-btn");
        restartBtn = doc.rootVisualElement.Q<Button>("restart-btn");
        menuBtn = doc.rootVisualElement.Q<Button>("menu-btn");
        quitBtn = doc.rootVisualElement.Q<Button>("quit-btn");

        // Start hidden
        root.style.display = DisplayStyle.None;

        // CLICK handlers (explicit null checks)
        if (resumeBtn != null) resumeBtn.clicked += OnResume;
        if (restartBtn != null) restartBtn.clicked += OnRestart;
        if (menuBtn != null) menuBtn.clicked += OnMainMenu;
        if (quitBtn != null) quitBtn.clicked += OnQuit;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        // show UI and allow clicks
        root.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;   // accept pointer events
        if (panel != null) panel.pickingMode = PickingMode.Position;

        // cursor available for clicking
        prevCursorVisible = UnityEngine.Cursor.visible;
        prevLock = UnityEngine.Cursor.lockState;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;

        // optional: focus a button for keyboard/gamepad nav
        // resumeBtn?.Focus();
    }

    void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        root.style.display = DisplayStyle.None;

        // restore cursor state
        UnityEngine.Cursor.visible = prevCursorVisible;
        UnityEngine.Cursor.lockState = prevLock;
    }

    void OnResume() => ResumeGame();

    void OnRestart()
    {
        Time.timeScale = 1f;
        RunManager.I?.StartNewRun();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnMainMenu()
    {
        Time.timeScale = 1f;
        RunManager.I?.StopRun();
        SceneManager.LoadScene("MainMenu"); // set to your exact scene name
    }

    void OnQuit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
