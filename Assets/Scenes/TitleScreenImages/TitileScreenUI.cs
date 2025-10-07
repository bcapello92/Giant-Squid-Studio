using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class TitleScreenUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private UIDocument uiDocument;

    [Tooltip("Optional: assign your SVG/Texture here if not set via USS/UI Builder")]
    [SerializeField] private Texture2D backgroundTexture;  // or Sprite if you prefer

    [Tooltip("Scene name to load when Start Game is pressed")]
    [SerializeField] private string firstSceneName = "GameScene";

    // (Optional) panels you might open later
    [SerializeField] private VisualTreeAsset levelSelectUxml;
    [SerializeField] private VisualTreeAsset optionsUxml;

    VisualElement root;
    VisualElement bg;
    Label title;
    Button startBtn, levelBtn, optionsBtn;

    void OnEnable()
    {
        if (!uiDocument) uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        bg = root.Q<VisualElement>("BG");
        title = root.Q<Label>("Title");
        startBtn = root.Q<Button>("StartBtn");
        levelBtn = root.Q<Button>("LevelBtn");
        optionsBtn = root.Q<Button>("OptionsBtn");

        // Optional: set background image from code if not using USS/UI Builder
        if (bg != null && backgroundTexture != null)
        {
            bg.style.backgroundImage = new StyleBackground(backgroundTexture);
            bg.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        // Button events
        if (startBtn != null) startBtn.clicked += OnStartClicked;
        if (levelBtn != null) levelBtn.clicked += OnLevelSelectClicked;
        if (optionsBtn != null) optionsBtn.clicked += OnOptionsClicked;

        // Set keyboard/gamepad focus to first button
        startBtn?.Focus();
    }

    void OnDisable()
    {
        if (startBtn != null) startBtn.clicked -= OnStartClicked;
        if (levelBtn != null) levelBtn.clicked -= OnLevelSelectClicked;
        if (optionsBtn != null) optionsBtn.clicked -= OnOptionsClicked;
    }

    void OnStartClicked()
    {
        if (!string.IsNullOrEmpty(firstSceneName))
            SceneManager.LoadScene(firstSceneName);
        else
            Debug.LogWarning("[Title] firstSceneName not set.");
    }

    void OnLevelSelectClicked()
    {
        // Minimal placeholder – swap content with a new visual tree (or open a modal)
        if (levelSelectUxml != null)
        {
            var panel = levelSelectUxml.CloneTree();
            OpenOverlay(panel);
        }
        else
        {
            Debug.Log("[Title] Level Select clicked (hook your level UI here).");
        }
    }

    void OnOptionsClicked()
    {
        if (optionsUxml != null)
        {
            var panel = optionsUxml.CloneTree();
            OpenOverlay(panel);
        }
        else
        {
            Debug.Log("[Title] Options clicked (hook your options UI here).");
        }
    }

    // Simple modal overlay helper
    void OpenOverlay(VisualElement panel)
    {
        if (panel == null || root == null) return;

        panel.style.position = Position.Absolute;
        panel.style.top = 0; panel.style.left = 0; panel.style.right = 0; panel.style.bottom = 0;
        panel.style.backgroundColor = new Color(0, 0, 0, 0.35f);

        // Add a close button if you want
        var closeBtn = new Button(() => { root.Remove(panel); startBtn?.Focus(); }) { text = "Back" };
        closeBtn.AddToClassList("menu-btn");
        closeBtn.style.position = Position.Absolute;
        closeBtn.style.top = 16; closeBtn.style.left = 16;
        panel.Add(closeBtn);

        root.Add(panel);
    }

    // Optional convenience for changing title text at runtime
    public void SetTitle(string newTitle) => title.text = newTitle;
}
