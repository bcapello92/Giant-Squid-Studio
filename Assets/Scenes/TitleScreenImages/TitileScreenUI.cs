using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameTitleUI : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;
    [SerializeField] Texture2D backgroundTexture;

    [Header("Run Manager")]
    [Tooltip("Prefab with RunManager on it (DontDestroyOnLoad).")]
    public RunManager runManagerPrefab;

    VisualElement root, bg;
    Label title;
    Button startBtn, levelBtn, optionsBtn;

    Dictionary<Button, string> baseTexts = new Dictionary<Button, string>();

    void OnEnable()
    {
        if (!uiDocument) uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        bg = root.Q<VisualElement>("BG");
        title = root.Q<Label>("Title");
        startBtn = root.Q<Button>("StartBtn");
        levelBtn = root.Q<Button>("LevelBtn");
        optionsBtn = root.Q<Button>("OptionsBtn");

        // Optional: background setup
        if (bg != null && backgroundTexture != null)
        {
            bg.style.backgroundImage = new StyleBackground(backgroundTexture);
            bg.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        // Click handlers
        if (startBtn != null) startBtn.clicked += OnStartClicked;
        if (levelBtn != null) levelBtn.clicked += OnLevelSelectClicked;
        if (optionsBtn != null) optionsBtn.clicked += OnOptionsClicked;

        // Focus + hover setup
        SetupBrackets(startBtn);
        SetupBrackets(levelBtn);
        SetupBrackets(optionsBtn);

        // initial focus
        startBtn?.Focus();
        ApplyBrackets(startBtn, true);
    }

    void OnDisable()
    {
        RemoveHandlers(startBtn);
        RemoveHandlers(levelBtn);
        RemoveHandlers(optionsBtn);
    }

    // ---------------- RunManager helper ----------------

    void EnsureRunManager()
    {
        if (RunManager.I == null)
        {
            if (runManagerPrefab == null)
            {
                Debug.LogError("[GameTitleUI] No RunManager prefab assigned.");
                return;
            }

            Instantiate(runManagerPrefab);
        }
    }

    // ---------------- Helper Functions ----------------

    void SetupBrackets(Button b)
    {
        if (b == null) return;
        if (!baseTexts.ContainsKey(b)) baseTexts[b] = b.text;

        b.RegisterCallback<FocusInEvent>(_ => ApplyBrackets(b, true));
        b.RegisterCallback<FocusOutEvent>(_ => ApplyBrackets(b, false));

        // hover
        b.RegisterCallback<MouseEnterEvent>(_ =>
        {
            b.Focus(); // focus for keyboard/visual consistency
            ApplyBrackets(b, true);
        });
        b.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            // be safe if focusController is null
            if (b.focusController == null || b.focusController.focusedElement != b)
                ApplyBrackets(b, false);
        });
    }

    void RemoveHandlers(Button b)
    {
        if (b == null) return;
        // UI Toolkit doesn't need explicit unregister here since callbacks are anonymous
    }

    void ApplyBrackets(Button b, bool active)
    {
        if (b == null) return;
        if (!baseTexts.TryGetValue(b, out var baseText))
            baseText = b.text;

        b.text = active ? $"< {baseText} >" : baseText;
    }

    // ---------------- Button Clicks ----------------

    void OnStartClicked()
    {
        // Start a run at Level 1 via RunManager
        EnsureRunManager();
        if (RunManager.I != null)
        {
            RunManager.I.StartRunAtLevel(1);
        }
    }

    void OnLevelSelectClicked()
    {
        // Level select is just another scene; LevelMenu will also EnsureRunManager
        SceneManager.LoadScene("Level Select");
    }

    void OnOptionsClicked()
    {
        Debug.Log("Options clicked");
        // open options menu / scene as needed
    }
}
