using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.Rendering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Rendering Demo Slices HUD")]
    public sealed class RenderingDemoSlicesHudController : MonoBehaviour
    {
        [SerializeField] private UIDocument _document = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private RenderingDemoSlicesShowcaseRoot _root = null;

        private VisualElement _hudRoot;
        private Label _contextValue;
        private Label _sharedRtValue;
        private Label _materialValue;
        private Label _publisherValue;
        private Label _volumeValue;
        private Label _controlsValue;
        private VisualElement _eventList;
        private bool _built;
        private VisualTreeAsset VisualTree => _visualTree;
        private StyleSheet StyleSheet => _styleSheet;

        public void Configure(
            RenderingDemoSlicesShowcaseRoot root,
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet)
        {
            _root = root;
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

        public void Refresh(RenderingDemoSlicesSnapshot snapshot)
        {
            EnsureBuilt();
            if (!_built)
                return;

            SetText(_contextValue, "wind " + FormatVector(snapshot.Globals.WindDirection) + " strength " + snapshot.Globals.WindStrength.ToString("0.00"));
            SetText(_sharedRtValue, "passes " + snapshot.Topology.Passes.Count + " sharedRT " + snapshot.SharedRT.Entries.Count + " conflicts " + snapshot.SharedRT.RecentConflicts.Count);
            SetText(_materialValue, "bindings " + snapshot.MaterialBindings.BindingCount + " targets " + snapshot.MaterialBindings.TargetCount + " applied " + snapshot.MaterialBindings.LastAppliedTargetCount + " events " + snapshot.Events.Count);
            SetText(_publisherValue, "current " + snapshot.Publisher.CurrentFrameEventCount + " recent " + snapshot.Publisher.RecentEventCount + " total " + snapshot.Publisher.TotalEventCount);
            SetText(_volumeValue, "active " + snapshot.VolumeDiagnostics.ActiveRequests.Count + " applied " + snapshot.VolumeBlendState.AppliedProfiles.Count + " suppressed " + snapshot.VolumeBlendState.SuppressedRequests.Count);
            SetText(_controlsValue, "1 Wind  2 Material  3 Publisher  4 Volume  R Reset");
            RefreshEvents(snapshot.Events);
            ApplyRuntimeStyleFallback();
        }

        public bool InvokeButtonForValidation(string buttonName)
        {
            EnsureBuilt();
            if (!_built || _hudRoot == null)
                return false;

            Button button = _hudRoot.Q<Button>(buttonName);
            if (button == null)
                return false;

            switch (buttonName)
            {
                case "wind-button":
                    DispatchCommand(RenderingDemoCommand.ToggleWind);
                    return true;
                case "material-button":
                    DispatchCommand(RenderingDemoCommand.PulseMaterial);
                    return true;
                case "publisher-button":
                    DispatchCommand(RenderingDemoCommand.PublishEventBurst);
                    return true;
                case "volume-button":
                    DispatchCommand(RenderingDemoCommand.CycleVolumePriority);
                    return true;
                case "reset-button":
                    DispatchCommand(RenderingDemoCommand.Reset);
                    return true;
                default:
                    return false;
            }
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            _document = _document != null ? _document : GetComponent<UIDocument>();
            if (_document == null)
                return;

            if (VisualTree != null)
                _document.visualTreeAsset = VisualTree;

            VisualElement root = _document.rootVisualElement;
            if (root == null)
                return;

            if (StyleSheet != null && !root.styleSheets.Contains(StyleSheet))
                root.styleSheets.Add(StyleSheet);

            _hudRoot = root.Q<VisualElement>("rendering-demo-hud") ?? root;
            _contextValue = root.Q<Label>("context-value");
            _sharedRtValue = root.Q<Label>("sharedrt-value");
            _materialValue = root.Q<Label>("material-value");
            _publisherValue = root.Q<Label>("publisher-value");
            _volumeValue = root.Q<Label>("volume-value");
            _controlsValue = root.Q<Label>("controls-value");
            _eventList = root.Q<VisualElement>("event-list");
            BindButton(root, "wind-button", RenderingDemoCommand.ToggleWind);
            BindButton(root, "material-button", RenderingDemoCommand.PulseMaterial);
            BindButton(root, "publisher-button", RenderingDemoCommand.PublishEventBurst);
            BindButton(root, "volume-button", RenderingDemoCommand.CycleVolumePriority);
            BindButton(root, "reset-button", RenderingDemoCommand.Reset);
            _built = _contextValue != null && _sharedRtValue != null && _eventList != null;
            if (_built)
                ApplyRuntimeStyleFallback();
        }

        private void BindButton(VisualElement root, string name, RenderingDemoCommand command)
        {
            Button button = root.Q<Button>(name);
            if (button == null)
                return;

            button.clicked += () => DispatchCommand(command);
        }

        private void DispatchCommand(RenderingDemoCommand command)
        {
            _root?.EnqueueCommand(command);
        }

        private void RefreshEvents(IReadOnlyList<string> events)
        {
            _eventList.Clear();
            if (events == null || events.Count == 0)
            {
                Label empty = new Label("No rendering events yet.") { name = "event-empty" };
                ApplyEventLabelFallback(empty);
                _eventList.Add(empty);
                return;
            }

            int start = Mathf.Max(0, events.Count - 6);
            for (int i = start; i < events.Count; i++)
            {
                Label row = new Label(events[i]) { name = "event-row" };
                ApplyEventLabelFallback(row);
                _eventList.Add(row);
            }
        }

        private void ApplyRuntimeStyleFallback()
        {
            if (_hudRoot == null)
                return;

            _hudRoot.style.position = Position.Absolute;
            _hudRoot.style.left = 18f;
            _hudRoot.style.top = 18f;
            _hudRoot.style.width = 610f;
            _hudRoot.style.paddingLeft = 14f;
            _hudRoot.style.paddingRight = 14f;
            _hudRoot.style.paddingTop = 14f;
            _hudRoot.style.paddingBottom = 14f;
            _hudRoot.style.backgroundColor = new Color(0.06f, 0.075f, 0.08f, 0.94f);
            _hudRoot.style.borderLeftWidth = 4f;
            _hudRoot.style.borderLeftColor = new Color(0.2f, 0.95f, 0.78f, 1f);

            ApplyLabelFallback(_hudRoot.Q<Label>("title"), 22f, Color.white, FontStyle.Bold);
            ApplyLabelFallback(_contextValue, 14f, new Color(0.9f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_sharedRtValue, 14f, new Color(0.9f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_materialValue, 14f, new Color(0.9f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_publisherValue, 14f, new Color(0.9f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_volumeValue, 14f, new Color(0.9f, 0.98f, 1f, 1f), FontStyle.Normal);
            ApplyLabelFallback(_controlsValue, 13f, new Color(0.76f, 0.86f, 0.9f, 1f), FontStyle.Normal);

            List<Label> labels = _hudRoot.Query<Label>(className: "slice-title").ToList();
            for (int i = 0; i < labels.Count; i++)
                ApplyLabelFallback(labels[i], 13f, new Color(1f, 0.83f, 0.42f, 1f), FontStyle.Bold);

            List<Button> buttons = _hudRoot.Query<Button>().ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].style.height = 28f;
                buttons[i].style.marginRight = 6f;
                buttons[i].style.color = Color.white;
                buttons[i].style.backgroundColor = new Color(0.14f, 0.22f, 0.26f, 1f);
                buttons[i].style.unityFontStyleAndWeight = FontStyle.Bold;
                buttons[i].style.fontSize = 12f;
            }

            for (int i = 0; i < _eventList.childCount; i++)
                ApplyEventLabelFallback(_eventList[i] as Label);
        }

        private static void ApplyLabelFallback(Label label, float size, Color color, FontStyle style)
        {
            if (label == null)
                return;

            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = style;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        private static void ApplyEventLabelFallback(Label label)
        {
            ApplyLabelFallback(label, 12f, new Color(0.82f, 0.91f, 0.94f, 1f), FontStyle.Normal);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
                label.text = text ?? string.Empty;
        }

        private static string FormatVector(Vector3 value)
        {
            return value.x.ToString("0.00") + "," + value.y.ToString("0.00") + "," + value.z.ToString("0.00");
        }
    }
}
