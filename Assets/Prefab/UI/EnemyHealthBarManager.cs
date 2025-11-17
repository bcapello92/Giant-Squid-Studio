using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class EnemyHealthBarManager : MonoBehaviour
{
    public static EnemyHealthBarManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private UIDocument uiDocument;           // HUD UIDocument
    [SerializeField] private VisualTreeAsset healthBarTemplate; // EnemyHealthBar.uxml

    [Header("Positioning")]
    [Tooltip("World-space offset added to the enemy position (e.g. above the head).")]
    public Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

    private VisualElement root;
    private VisualElement container;
    private Camera mainCam;

    // One entry per enemy
    private class Entry
    {
        public Transform targetTransform;   // enemy transform
        public VisualElement root;         // EnemyHealthRoot
        public ProgressBar bar;            // EnemyHealthBar
    }

    private readonly List<Entry> entries = new List<Entry>();

    // ------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------
    void Awake()
    {
        // Simple singleton so enemies can call Instance.RegisterX(...)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!uiDocument)
            uiDocument = GetComponent<UIDocument>();

        if (!uiDocument)
        {
            Debug.LogError("[EnemyHealthBarManager] No UIDocument assigned or found on GameObject.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[EnemyHealthBarManager] UIDocument has no rootVisualElement.");
            enabled = false;
            return;
        }

        // This must exist in your EnemyHUD.uxml
        container = root.Q<VisualElement>("EnemyHealthContainer");
        if (container == null)
        {
            Debug.LogError("[EnemyHealthBarManager] Could not find 'EnemyHealthContainer' in HUD UXML.");
            enabled = false;
            return;
        }

        if (!healthBarTemplate)
        {
            Debug.LogError("[EnemyHealthBarManager] No healthBarTemplate (VisualTreeAsset) assigned.");
            enabled = false;
            return;
        }

        mainCam = Camera.main;
        if (!mainCam)
        {
            Debug.LogWarning("[EnemyHealthBarManager] No main camera found. Trying Camera.main at runtime may be needed.");
        }
    }

    // ------------------------------------------------------
    // Public registration API
    // ------------------------------------------------------

    // For BlobMonsterController
    public void RegisterBlob(BlobMonsterController blob)
    {
        if (blob == null) return;
        CreateEntry(
            blob.transform,
            blob.maxHP,
            (cur, max, bar) =>
            {
                bar.highValue = max;
                bar.value = cur;
            },
            () =>
            {
                // On death
                // removal is handled by the closure in CreateEntry
            });

        // Hook up into events
        Entry entry = entries[entries.Count - 1]; // last created
        blob.OnHealthChanged += (cur, max) =>
        {
            if (entry.bar != null)
            {
                entry.bar.highValue = max;
                entry.bar.value = cur;
            }
        };
        blob.OnDied += _ =>
        {
            RemoveEntry(entry);
        };
    }

    // For EnemyController
    public void RegisterEnemy(EnemyController enemy)
    {
        if (enemy == null) return;
        CreateEntry(
            enemy.transform,
            enemy.maxHP,
            (cur, max, bar) =>
            {
                bar.highValue = max;
                bar.value = cur;
            },
            () =>
            {
                // removal handled below
            });

        Entry entry = entries[entries.Count - 1];
        enemy.OnHealthChanged += (cur, max) =>
        {
            if (entry.bar != null)
            {
                entry.bar.highValue = max;
                entry.bar.value = cur;
            }
        };
        enemy.OnDied += _ =>
        {
            RemoveEntry(entry);
        };
    }

    // ------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------
    private void CreateEntry(
        Transform target,
        int maxHp,
        System.Action<int, int, ProgressBar> onHealthInitOrChange,
        System.Action onDied)
    {
        if (healthBarTemplate == null || container == null || target == null)
            return;

        // Instantiate UI from template
        VisualElement ve = healthBarTemplate.Instantiate();
        ve.name = "EnemyHealthRoot";

        ProgressBar bar = ve.Q<ProgressBar>("EnemyHealthBar");
        if (bar == null)
        {
            Debug.LogError("[EnemyHealthBarManager] EnemyHealthBar not found in template.");
            return;
        }

        bar.lowValue = 0;
        bar.highValue = maxHp;
        bar.value = maxHp;

        container.Add(ve);

        Entry entry = new Entry
        {
            targetTransform = target,
            root = ve,
            bar = bar
        };
        entries.Add(entry);

        // Initial update
        onHealthInitOrChange(maxHp, maxHp, bar);
    }

    private void RemoveEntry(Entry entry)
    {
        if (entry == null) return;

        if (entry.root != null && entry.root.parent != null)
            entry.root.parent.Remove(entry.root);

        entries.Remove(entry);
    }

    // ------------------------------------------------------
    // Update positioning
    // ------------------------------------------------------
    void LateUpdate()
    {
        if (entries.Count == 0) return;
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return;
        }

        IPanel panel = root.panel;
        if (panel == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e == null || e.targetTransform == null || e.root == null)
                continue;

            Vector3 worldPos = e.targetTransform.position + worldOffset;

            // Hide if behind camera
            Vector3 toCam = mainCam.transform.InverseTransformPoint(worldPos);
            if (toCam.z < 0f)
            {
                e.root.style.display = DisplayStyle.None;
                continue;
            }

            e.root.style.display = DisplayStyle.Flex;

            // World → panel coordinates
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPos, mainCam);

            float barWidth = e.root.resolvedStyle.width > 0 ? e.root.resolvedStyle.width : 60f;
            float barHeight = e.root.resolvedStyle.height > 0 ? e.root.resolvedStyle.height : 8f;

            e.root.style.left = panelPos.x - barWidth * 0.5f;
            e.root.style.top = panelPos.y - barHeight * 1.5f;
        }
    }
}
