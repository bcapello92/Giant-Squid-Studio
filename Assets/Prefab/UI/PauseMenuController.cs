using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PauseMenuController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }  

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
        IsPaused = true;           // <--- SET GLOBAL FLAG
        Time.timeScale = 0f;

        root.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;
        if (panel != null) panel.pickingMode = PickingMode.Position;

        prevCursorVisible = UnityEngine.Cursor.visible;
        prevLock = UnityEngine.Cursor.lockState;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
    }

    void ResumeGame()
    {
        isPaused = false;
        IsPaused = false;          // <--- CLEAR GLOBAL FLAG
        Time.timeScale = 1f;
        root.style.display = DisplayStyle.None;

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
        SceneManager.LoadScene("Title Scene");
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
