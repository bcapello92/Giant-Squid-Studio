using UnityEngine;
using UnityEngine.UIElements;

public class UIPlayerHealthToolkit : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;
    [SerializeField] TestPlayerController player;
    [SerializeField] Texture2D healthFrame;   // ← drag greenBar here

    ProgressBar bar;
    Label label;
    VisualElement frame;

    void OnEnable()
    {
        if (!uiDocument) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        frame = root.Q<VisualElement>("HealthBarBackground");
        bar = root.Q<ProgressBar>("HealthBar");
        label = root.Q<Label>("HealthLabel");

        // Apply background image in code (no XML URL)
        if (frame != null && healthFrame != null)
        {
            frame.style.backgroundImage = new StyleBackground(healthFrame);
            frame.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        if (!player) player = FindObjectOfType<TestPlayerController>();
        if (player != null)
        {
            player.HealthChanged += OnHealthChanged;
            OnHealthChanged(player.CurrentHP, player.maxHP);
        }
    }

    void OnDisable()
    {
        if (player != null) player.HealthChanged -= OnHealthChanged;
    }

    void OnHealthChanged(int current, int max)
    {
        if (bar != null)
        {
            bar.lowValue = 0;
            bar.highValue = max;
            bar.value = Mathf.Clamp(current, 0, max);
            bar.title = string.Empty;
        }
        if (label != null) label.text = $"{current}/{max}";
    }
}
