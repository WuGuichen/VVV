using System.Collections.Generic;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Story.Editor
{
    public sealed class StoryRuntimeDebugWindow : EditorWindow
    {
        private const string MenuPath = "MxFramework/Story/Runtime Debug";

        private readonly DebugUiSnapshotAggregator _aggregator = new DebugUiSnapshotAggregator();
        private ScrollView _contentRoot;
        private PopupField<string> _targetPopup;
        private int _selectedTargetIndex;
        private string _lastReport = string.Empty;

        [MenuItem(MenuPath, priority = 180)]
        public static void Open()
        {
            StoryRuntimeDebugWindow window = GetWindow<StoryRuntimeDebugWindow>();
            window.titleContent = new GUIContent("Story Debug");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            rootVisualElement.Add(CreateToolbar());

            _contentRoot = new ScrollView(ScrollViewMode.Vertical);
            _contentRoot.style.flexGrow = 1;
            _contentRoot.style.minHeight = 0;
            rootVisualElement.Add(_contentRoot);

            RefreshView();
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.marginBottom = 8;

            IReadOnlyList<StoryEditorDebugTarget> targets = StoryEditorDebugRegistry.Targets;
            var names = new List<string>();
            for (int i = 0; i < targets.Count; i++)
            {
                names.Add(targets[i].Name);
            }

            if (names.Count == 0)
            {
                names.Add("无已注册目标");
                _selectedTargetIndex = 0;
            }
            else if (_selectedTargetIndex < 0 || _selectedTargetIndex >= names.Count)
            {
                _selectedTargetIndex = 0;
            }

            _targetPopup = new PopupField<string>("调试目标", names, _selectedTargetIndex);
            _targetPopup.RegisterValueChangedCallback(evt =>
            {
                _selectedTargetIndex = names.IndexOf(evt.newValue);
                RefreshView();
            });
            _targetPopup.style.minWidth = 240;
            _targetPopup.style.marginRight = 8;
            toolbar.Add(_targetPopup);

            toolbar.Add(CreateButton("刷新", RefreshView));
            toolbar.Add(CreateButton("复制报告", CopyReport));
            return toolbar;
        }

        private void RefreshView()
        {
            if (_contentRoot == null)
            {
                return;
            }

            _contentRoot.Clear();
            IReadOnlyList<StoryEditorDebugTarget> targets = StoryEditorDebugRegistry.Targets;
            if (targets.Count == 0)
            {
                _lastReport = "Story Runtime Debug\nstatus: no registered target\n";
                StoryEditorDebugWindowView.BuildReadonlyTree(_contentRoot, DebugUiDashboardViewModel.Empty, _lastReport);
                return;
            }

            if (_selectedTargetIndex < 0 || _selectedTargetIndex >= targets.Count)
            {
                _selectedTargetIndex = 0;
            }

            StoryRuntimeDebugSource source = targets[_selectedTargetIndex].CreateSource();
            var registry = new FrameworkDebugSourceRegistry();
            registry.Register(source);
            DebugUiDashboardViewModel dashboard = _aggregator.Refresh(registry);
            _lastReport = FrameworkDebugReportExporter.ExportText(source.CreateSnapshot());
            StoryEditorDebugWindowView.BuildReadonlyTree(_contentRoot, dashboard, _lastReport);
        }

        private static Button CreateButton(string text, System.Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 6;
            button.style.marginBottom = 4;
            return button;
        }

        private void CopyReport()
        {
            if (string.IsNullOrEmpty(_lastReport))
            {
                RefreshView();
            }

            EditorGUIUtility.systemCopyBuffer = _lastReport ?? string.Empty;
            ShowNotification(new GUIContent("报告已复制"));
        }
    }
}
