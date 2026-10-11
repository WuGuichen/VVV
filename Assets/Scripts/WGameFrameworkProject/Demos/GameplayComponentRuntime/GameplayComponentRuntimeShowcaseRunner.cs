using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.GameplayComponentRuntime
{
    [AddComponentMenu("MxFramework/Demo/Gameplay Component Runtime Showcase")]
    public sealed class GameplayComponentRuntimeShowcaseRunner : MonoBehaviour
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private PanelSettings _panelSettings = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private bool _spawnOnEnable = true;

        private GameplayComponentRuntimeShowcase _showcase;
        private VisualElement _root;
        private Label _phaseLabel;
        private Label _frameLabel;
        private Label _hashLabel;
        private Label _heroLabel;
        private Label _enemyLabel;
        private Label _cooldownLabel;
        private Label _saveLabel;
        private Label _eventLogLabel;
        private Button _spawnButton;
        private Button _castButton;
        private Button _cleanupButton;
        private Button _saveButton;
        private Button _restoreButton;
        private Button _resetButton;
        private Button _scriptButton;

        private void OnEnable()
        {
            _showcase = new GameplayComponentRuntimeShowcase();
            if (_spawnOnEnable)
                _showcase.SpawnActors();

            EnsureDocument();
            RefreshUi();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            if (_showcase != null)
            {
                _showcase.Dispose();
                _showcase = null;
            }
        }

        private void Update()
        {
            EnsureDocument();
            RefreshUi();
        }

        public void ConfigureAssets(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            _document = document;
            _panelSettings = panelSettings;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.panelSettings == null)
                _document.panelSettings = CreateRuntimePanelSettings(_panelSettings);

            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
                _document.visualTreeAsset = _visualTree;

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
                return;

            if (_styleSheet != null && !documentRoot.styleSheets.Contains(_styleSheet))
                documentRoot.styleSheets.Add(_styleSheet);

            VisualElement nextRoot = documentRoot.Q<VisualElement>("component-runtime-root");
            if (nextRoot == null)
                nextRoot = BuildFallbackTree(documentRoot);

            if (_root == nextRoot)
                return;

            UnregisterCallbacks();
            _root = nextRoot;
            CacheElements(_root);
            ApplyRuntimeTextStyles();
            RegisterCallbacks();
        }

        private VisualElement BuildFallbackTree(VisualElement documentRoot)
        {
            documentRoot.Clear();
            var root = new VisualElement { name = "component-runtime-root" };
            root.AddToClassList("component-runtime-root");
            documentRoot.Add(root);

            root.Add(new Label("Component Runtime Showcase") { name = "title-label" });
            root.Add(new Label("Ready") { name = "phase-label" });
            root.Add(CreateMetric("Frame", "frame-label"));
            root.Add(CreateMetric("Hash", "hash-label"));
            root.Add(CreateMetric("Hero", "hero-label"));
            root.Add(CreateMetric("Enemy", "enemy-label"));
            root.Add(CreateMetric("Cooldown", "cooldown-label"));
            root.Add(CreateMetric("Save", "save-label"));

            var row = new VisualElement { name = "button-row" };
            row.AddToClassList("button-row");
            root.Add(row);
            row.Add(new Button { name = "spawn-button", text = "Spawn" });
            row.Add(new Button { name = "cast-button", text = "Cast" });
            row.Add(new Button { name = "cleanup-button", text = "Cleanup" });
            row.Add(new Button { name = "save-button", text = "Save" });
            row.Add(new Button { name = "restore-button", text = "Restore" });
            row.Add(new Button { name = "reset-button", text = "Reset" });
            row.Add(new Button { name = "script-button", text = "Run Script" });

            root.Add(new Label(string.Empty) { name = "event-log-label" });
            return root;
        }

        private static VisualElement CreateMetric(string title, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("metric-row");
            row.Add(new Label(title));
            row.Add(new Label("-") { name = valueName });
            return row;
        }

        private void CacheElements(VisualElement root)
        {
            _phaseLabel = root.Q<Label>("phase-label");
            _frameLabel = root.Q<Label>("frame-label");
            _hashLabel = root.Q<Label>("hash-label");
            _heroLabel = root.Q<Label>("hero-label");
            _enemyLabel = root.Q<Label>("enemy-label");
            _cooldownLabel = root.Q<Label>("cooldown-label");
            _saveLabel = root.Q<Label>("save-label");
            _eventLogLabel = root.Q<Label>("event-log-label");
            _spawnButton = root.Q<Button>("spawn-button");
            _castButton = root.Q<Button>("cast-button");
            _cleanupButton = root.Q<Button>("cleanup-button");
            _saveButton = root.Q<Button>("save-button");
            _restoreButton = root.Q<Button>("restore-button");
            _resetButton = root.Q<Button>("reset-button");
            _scriptButton = root.Q<Button>("script-button");
        }

        private void RegisterCallbacks()
        {
            if (_spawnButton != null) _spawnButton.clicked += OnSpawnClicked;
            if (_castButton != null) _castButton.clicked += OnCastClicked;
            if (_cleanupButton != null) _cleanupButton.clicked += OnCleanupClicked;
            if (_saveButton != null) _saveButton.clicked += OnSaveClicked;
            if (_restoreButton != null) _restoreButton.clicked += OnRestoreClicked;
            if (_resetButton != null) _resetButton.clicked += OnResetClicked;
            if (_scriptButton != null) _scriptButton.clicked += OnScriptClicked;
        }

        private void UnregisterCallbacks()
        {
            if (_spawnButton != null) _spawnButton.clicked -= OnSpawnClicked;
            if (_castButton != null) _castButton.clicked -= OnCastClicked;
            if (_cleanupButton != null) _cleanupButton.clicked -= OnCleanupClicked;
            if (_saveButton != null) _saveButton.clicked -= OnSaveClicked;
            if (_restoreButton != null) _restoreButton.clicked -= OnRestoreClicked;
            if (_resetButton != null) _resetButton.clicked -= OnResetClicked;
            if (_scriptButton != null) _scriptButton.clicked -= OnScriptClicked;
        }

        private void ApplyRuntimeTextStyles()
        {
            if (_root == null)
                return;

            _root.style.flexGrow = 1f;
            ApplyLabel(_root.Q<Label>("title-label"), 24, new Color(0.96f, 0.97f, 0.98f), FontStyle.Bold);
            ApplyLabel(_phaseLabel, 14, new Color(0.78f, 0.94f, 1f), FontStyle.Bold);
            ApplyLabel(_frameLabel, 14, Color.white, FontStyle.Bold);
            ApplyLabel(_hashLabel, 13, new Color(0.88f, 0.91f, 0.95f), FontStyle.Normal);
            ApplyLabel(_heroLabel, 14, new Color(0.84f, 1f, 0.88f), FontStyle.Bold);
            ApplyLabel(_enemyLabel, 14, new Color(1f, 0.86f, 0.82f), FontStyle.Bold);
            ApplyLabel(_cooldownLabel, 14, new Color(1f, 0.93f, 0.72f), FontStyle.Bold);
            ApplyLabel(_saveLabel, 13, new Color(0.86f, 0.91f, 1f), FontStyle.Normal);
            ApplyLabel(_eventLogLabel, 12, new Color(0.86f, 0.90f, 0.94f), FontStyle.Normal);
            ApplyButton(_spawnButton);
            ApplyButton(_castButton);
            ApplyButton(_cleanupButton);
            ApplyButton(_saveButton);
            ApplyButton(_restoreButton);
            ApplyButton(_resetButton);
            ApplyButton(_scriptButton);
        }

        private static void ApplyLabel(Label label, int fontSize, Color color, FontStyle fontStyle)
        {
            if (label == null)
                return;

            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
        }

        private static void ApplyButton(Button button)
        {
            if (button == null)
                return;

            button.style.height = 34;
            button.style.marginRight = 8;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private void RefreshUi()
        {
            if (_showcase == null || _root == null)
                return;

            GameplayComponentRuntimeShowcaseSnapshot snapshot = _showcase.CreateSnapshot();
            Set(_phaseLabel, ResolvePhase(snapshot));
            Set(_frameLabel, "Frame " + snapshot.Frame.Value + " / next command " + snapshot.NextFrame);
            Set(_hashLabel, snapshot.Hash.ToString());
            Set(_heroLabel, FormatActor(snapshot.HeroEntityId, snapshot.HeroAlive, snapshot.HeroLifecycle, snapshot.HeroHp, snapshot.HeroMana));
            Set(_enemyLabel, FormatActor(snapshot.EnemyEntityId, snapshot.EnemyAlive, snapshot.EnemyLifecycle, snapshot.EnemyHp, snapshot.EnemyMana));
            Set(_cooldownLabel, snapshot.StrikeCooldownRemainingFrames + " frames");
            Set(_saveLabel, snapshot.SaveStatus);
            Set(_eventLogLabel, string.Join("\n", snapshot.EventLog));
        }

        private static string ResolvePhase(GameplayComponentRuntimeShowcaseSnapshot snapshot)
        {
            if (!snapshot.HeroEntityId.IsValid)
                return "Ready to spawn";
            if (!snapshot.EnemyAlive && snapshot.EnemyEntityId.IsValid)
                return "Enemy cleaned";
            if (snapshot.EnemyHp <= 0)
                return "Enemy defeated, cleanup pending";
            if (snapshot.StrikeCooldownRemainingFrames > 0)
                return "Cooldown active";
            return "Ready";
        }

        private static string FormatActor(
            MxFramework.Gameplay.GameplayEntityId entityId,
            bool alive,
            MxFramework.Gameplay.GameplayLifecycleState lifecycle,
            int hp,
            int mana)
        {
            if (!entityId.IsValid)
                return "not spawned";

            return entityId.Index + ":" + entityId.Generation +
                " alive=" + alive +
                " lifecycle=" + lifecycle +
                " hp=" + hp +
                " mana=" + mana;
        }

        private static void Set(Label label, string text)
        {
            if (label != null)
                label.text = text ?? string.Empty;
        }

        private void OnSpawnClicked()
        {
            _showcase?.SpawnActors();
            RefreshUi();
        }

        private void OnCastClicked()
        {
            _showcase?.CastStrike();
            RefreshUi();
        }

        private void OnCleanupClicked()
        {
            _showcase?.MarkEnemyPendingDestroyAndTick();
            RefreshUi();
        }

        private void OnSaveClicked()
        {
            _showcase?.Save();
            RefreshUi();
        }

        private void OnRestoreClicked()
        {
            _showcase?.Restore();
            RefreshUi();
        }

        private void OnResetClicked()
        {
            _showcase?.Reset();
            if (_spawnOnEnable)
                _showcase?.SpawnActors();
            RefreshUi();
        }

        private void OnScriptClicked()
        {
            if (_showcase == null)
                return;

            _showcase.Reset();
            _showcase.SpawnActors();
            _showcase.CastStrike();
            _showcase.CastStrike();
            _showcase.CastStrike();
            _showcase.MarkEnemyPendingDestroyAndTick();
            RefreshUi();
        }

        private static PanelSettings CreateRuntimePanelSettings(PanelSettings template)
        {
            PanelSettings settings = template != null
                ? Instantiate(template)
                : ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "GameplayComponentRuntimeShowcasePanelSettingsInstance";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            DisableNoThemeWarningField?.SetValue(settings, true);
            return settings;
        }
    }
}
