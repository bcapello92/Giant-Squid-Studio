using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;
    [SerializeField] Texture2D backgroundTexture;

    VisualElement root, bg;
    Label title;
    Button restartBtn, menuBtn;

    Dictionary<Button, string> baseTexts = new Dictionary<Button, string>();

    void OnEnable()
    {
        if (!uiDocument) uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        bg = root.Q<VisualElement>("BG");
        title = root.Q<Label>("Title"); // adjust if your label is named "GameTitle"
        restartBtn = root.Q<Button>("RestartBtn");
        menuBtn = root.Q<Button>("MenuBtn");

        // Optional: background setup
        if (bg != null && backgroundTexture != null)
        {
            bg.style.backgroundImage = new StyleBackground(backgroundTexture);
            bg.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        // Click handlers
        if (restartBtn != null) restartBtn.clicked += OnRestartClicked;
        if (menuBtn != null) menuBtn.clicked += OnMenuClicked;

        // Focus + hover setup
        SetupBrackets(restartBtn);
        SetupBrackets(menuBtn);

        // initial focus
        restartBtn?.Focus();
        ApplyBrackets(restartBtn, true);
    }

    void OnDisable()
    {
        RemoveHandlers(restartBtn);
        RemoveHandlers(menuBtn);
    }

    // ---------------- Helper Functions ----------------

    void SetupBrackets(Button b)
    {
        if (b == null) return;
        if (!baseTexts.ContainsKey(b)) baseTexts[b] = b.text;

        b.RegisterCallback<FocusInEvent>(_ => ApplyBrackets(b, true));
        b.RegisterCallback<FocusOutEvent>(_ => ApplyBrackets(b, false));

        // add hover
        b.RegisterCallback<MouseEnterEvent>(_ =>
        {
            b.Focus(); // focus for keyboard/visual consistency
            ApplyBrackets(b, true);
        });
        b.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (!b.focusController.focusedElement.Equals(b))
                ApplyBrackets(b, false);
        });
    }

    void RemoveHandlers(Button b)
    {
        if (b == null) return;
        // no explicit unregister needed unless you’re using lambdas with stored refs
    }

    void ApplyBrackets(Button b, bool active)
    {
        if (b == null) return;
        if (!baseTexts.TryGetValue(b, out var baseText))
            baseText = b.text;

        b.text = active ? $"< {baseText} >" : baseText;
    }

    // ---------------- Button Clicks ----------------

    void OnRestartClicked() => SceneManager.LoadScene("Level 1");
    void OnMenuClicked() => SceneManager.LoadScene("Title Scene");
}
