using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.CombatAnimation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Combat Animation HUD Controller")]
    public sealed class CombatAnimationHudController : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;

        private Label _playerAction;
        private Label _playerPhase;
        private Label _playerFrame;
        private Label _playerHp;
        private Label _dummyHp;
        private Label _weaponTrace;
        private Label _instructions;
        private VisualElement _hudRoot;
        private VisualElement _eventList;
        private VisualElement _diagnosticPanel;
        private VisualElement _actionStateList;
        private VisualElement _hitApplicationList;
        private VisualElement _gameplayAttributeList;
        private VisualElement _bridgeMapList;
        private VisualElement _runtimeHashList;
        private VisualElement _eventQueueList;
        private bool _built;

        public void ConfigureAssets(UIDocument document, VisualTreeAsset visualTree, StyleSheet styleSheet)
        {
            _document = document;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
        }

        public void Refresh(CombatAnimationHudModel model)
        {
            EnsureBuilt();
            if (!_built)
            {
                return;
            }

            SetText(_playerAction, model.PlayerAction);
            SetText(_playerPhase, model.PlayerPhase);
            SetText(_playerFrame, model.PlayerLocalFrame);
            SetText(_playerHp, model.PlayerHp);
            SetText(_dummyHp, model.DummyHp);
            SetText(_weaponTrace, model.WeaponTrace);
            SetText(_instructions, model.Instructions);
            RefreshEvents(model.RecentEvents);
            RefreshDiagnostics(model.Diagnostics);
            ApplyRuntimeStyleFallback();
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _document = _document != null ? _document : GetComponent<UIDocument>();
            if (_document == null)
            {
                return;
            }

            if (_visualTree != null)
            {
                _document.visualTreeAsset = _visualTree;
            }

            VisualElement root = _document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
            {
                root.styleSheets.Add(_styleSheet);
            }

            _hudRoot = root.Q<VisualElement>("combat-animation-hud") ?? root;
            _playerAction = root.Q<Label>("player-action");
            _playerPhase = root.Q<Label>("player-phase");
            _playerFrame = root.Q<Label>("player-frame");
            _playerHp = root.Q<Label>("player-hp");
            _dummyHp = root.Q<Label>("dummy-hp");
            _weaponTrace = root.Q<Label>("weapon-trace");
            _instructions = root.Q<Label>("instructions");
            _eventList = root.Q<VisualElement>("event-list");
            _diagnosticPanel = root.Q<VisualElement>("runtime-diagnostic-panel");
            _actionStateList = root.Q<VisualElement>("diagnostic-action-state-list");
            _hitApplicationList = root.Q<VisualElement>("diagnostic-hit-application-list");
            _gameplayAttributeList = root.Q<VisualElement>("diagnostic-gameplay-attribute-list");
            _bridgeMapList = root.Q<VisualElement>("diagnostic-bridge-map-list");
            _runtimeHashList = root.Q<VisualElement>("diagnostic-runtime-hash-list");
            _eventQueueList = root.Q<VisualElement>("diagnostic-event-queue-list");
            _built = _playerAction != null && _eventList != null;
            if (_built)
            {
                ApplyRuntimeStyleFallback();
            }
        }

        private void RefreshEvents(IReadOnlyList<string> events)
        {
            _eventList.Clear();
            if (events == null || events.Count == 0)
            {
                Label empty = new Label("No hit events yet.") { name = "event-empty" };
                ApplyEventLabelFallback(empty);
                _eventList.Add(empty);
                return;
            }

            int start = Mathf.Max(0, events.Count - 5);
            for (int i = start; i < events.Count; i++)
            {
                Label row = new Label(events[i]) { name = "event-row" };
                ApplyEventLabelFallback(row);
                _eventList.Add(row);
            }
        }

        private void RefreshDiagnostics(CombatRuntimeDiagnosticHudModel diagnostics)
        {
            if (_diagnosticPanel == null)
            {
                return;
            }

            RefreshDiagnosticRows(_actionStateList, diagnostics == null ? null : diagnostics.ActionStateRows);
            RefreshDiagnosticRows(_hitApplicationList, diagnostics == null ? null : diagnostics.HitApplicationRows);
            RefreshDiagnosticRows(_gameplayAttributeList, diagnostics == null ? null : diagnostics.GameplayAttributeRows);
            RefreshDiagnosticRows(_bridgeMapList, diagnostics == null ? null : diagnostics.BridgeMapRows);
            RefreshDiagnosticRows(_runtimeHashList, diagnostics == null ? null : diagnostics.RuntimeHashRows);
            RefreshDiagnosticRows(_eventQueueList, diagnostics == null ? null : diagnostics.EventQueueRows);
        }

        private static void RefreshDiagnosticRows(VisualElement list, IReadOnlyList<string> rows)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();
            if (rows == null || rows.Count == 0)
            {
                Label empty = new Label("-") { name = "diagnostic-row" };
                ApplyDiagnosticLabelFallback(empty);
                list.Add(empty);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                Label row = new Label(rows[i]) { name = "diagnostic-row" };
                ApplyDiagnosticLabelFallback(row);
                list.Add(row);
            }
        }

        private void ApplyRuntimeStyleFallback()
        {
            if (_hudRoot == null)
            {
                return;
            }

            _hudRoot.style.position = Position.Absolute;
            _hudRoot.style.left = 18f;
            _hudRoot.style.top = 18f;
            _hudRoot.style.width = 560f;
            _hudRoot.style.paddingLeft = 14f;
            _hudRoot.style.paddingRight = 14f;
            _hudRoot.style.paddingTop = 14f;
            _hudRoot.style.paddingBottom = 14f;
            _hudRoot.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.94f);
            _hudRoot.style.borderLeftWidth = 4f;
            _hudRoot.style.borderLeftColor = new Color(0.95f, 0.24f, 0.20f, 1f);

            VisualElement grid = _hudRoot.Q<VisualElement>(className: "hud-grid");
            if (grid != null)
            {
                grid.style.flexDirection = FlexDirection.Row;
            }

            List<VisualElement> panels = _hudRoot.Query<VisualElement>(className: "hud-panel").ToList();
            for (int i = 0; i < panels.Count; i++)
            {
                ApplyPanelFallback(panels[i]);
            }

            ApplyLabelFallback(_hudRoot.Q<Label>("title"), 22f, new Color(1f, 1f, 1f, 1f), FontStyle.Bold);
            ApplyLabelFallback(_instructions, 14f, new Color(0.82f, 0.90f, 0.96f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_playerAction, 15f, new Color(0.96f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_playerPhase, 15f, new Color(0.96f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_playerFrame, 15f, new Color(0.96f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_playerHp, 16f, new Color(1f, 0.96f, 0.74f, 1f), FontStyle.Bold);
            ApplyLabelFallback(_dummyHp, 16f, new Color(1f, 0.96f, 0.74f, 1f), FontStyle.Bold);
            ApplyLabelFallback(_weaponTrace, 14f, new Color(0.86f, 0.94f, 1f, 1f), FontStyle.Normal);

            List<Label> panelTitles = _hudRoot.Query<Label>(className: "panel-title").ToList();
            for (int i = 0; i < panelTitles.Count; i++)
            {
                ApplyLabelFallback(panelTitles[i], 14f, new Color(1f, 0.82f, 0.36f, 1f), FontStyle.Bold);
            }

            if (_eventList != null)
            {
                for (int i = 0; i < _eventList.childCount; i++)
                {
                    ApplyEventLabelFallback(_eventList[i] as Label);
                }
            }

            ApplyDiagnosticListFallback(_actionStateList);
            ApplyDiagnosticListFallback(_hitApplicationList);
            ApplyDiagnosticListFallback(_gameplayAttributeList);
            ApplyDiagnosticListFallback(_bridgeMapList);
            ApplyDiagnosticListFallback(_runtimeHashList);
            ApplyDiagnosticListFallback(_eventQueueList);
        }

        private static void ApplyPanelFallback(VisualElement panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.style.flexGrow = 1f;
            panel.style.marginRight = 8f;
            panel.style.marginBottom = 8f;
            panel.style.paddingLeft = 10f;
            panel.style.paddingRight = 10f;
            panel.style.paddingTop = 10f;
            panel.style.paddingBottom = 10f;
            panel.style.backgroundColor = new Color(0.14f, 0.17f, 0.22f, 0.96f);
            panel.style.borderTopLeftRadius = 4f;
            panel.style.borderTopRightRadius = 4f;
            panel.style.borderBottomLeftRadius = 4f;
            panel.style.borderBottomRightRadius = 4f;
        }

        private static void ApplyLabelFallback(Label label, float fontSize, Color color, FontStyle fontStyle)
        {
            if (label == null)
            {
                return;
            }

            label.style.color = color;
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginBottom = 3f;
        }

        private static void ApplyEventLabelFallback(Label label)
        {
            ApplyLabelFallback(label, 13f, new Color(0.88f, 0.94f, 0.98f, 1f), FontStyle.Normal);
            if (label != null)
            {
                label.style.marginBottom = 2f;
            }
        }

        private static void ApplyDiagnosticListFallback(VisualElement list)
        {
            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.childCount; i++)
            {
                ApplyDiagnosticLabelFallback(list[i] as Label);
            }
        }

        private static void ApplyDiagnosticLabelFallback(Label label)
        {
            ApplyLabelFallback(label, 13f, new Color(0.84f, 0.91f, 0.95f, 1f), FontStyle.Normal);
            if (label != null)
            {
                label.style.marginBottom = 1f;
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(text) ? "-" : text;
            }
        }
    }

    public sealed class CombatAnimationHudModel
    {
        public string PlayerAction { get; set; }
        public string PlayerPhase { get; set; }
        public string PlayerLocalFrame { get; set; }
        public string PlayerHp { get; set; }
        public string DummyHp { get; set; }
        public string WeaponTrace { get; set; }
        public string Instructions { get; set; }
        public IReadOnlyList<string> RecentEvents { get; set; }
        public CombatRuntimeDiagnosticHudModel Diagnostics { get; set; }
    }
}
