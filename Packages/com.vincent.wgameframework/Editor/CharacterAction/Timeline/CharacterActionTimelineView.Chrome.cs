using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Manipulators;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        private void BuildChrome()
        {
            Add(BuildContextBar());

            _readOnlyBannerHost = new VisualElement();
            _readOnlyBannerHost.name = "timeline-readonly-banner-host";
            _readOnlyBannerHost.AddToClassList("ca-tl-readonly-banner-host");
            _readOnlyBannerHost.style.display = DisplayStyle.None;
            Add(_readOnlyBannerHost);

            VisualElement viewportControl = BuildViewportControl();
            viewportControl.style.flexShrink = 0f;
            Add(viewportControl);

            _bodyScroll = new ScrollView(ScrollViewMode.Vertical);
            _bodyScroll.AddToClassList("ca-tl-body-scroll");
            _bodyScroll.style.flexGrow = 1f;
            _bodyScroll.style.minHeight = 0f;

            var body = new VisualElement();
            body.AddToClassList("ca-tl-body");
            body.style.flexGrow = 1f;
            _bodyScroll.Add(body);

            _labelColumn = new VisualElement();
            _labelColumn.AddToClassList("ca-tl-label-column");
            _labelColumn.style.width = CharacterActionTimelineLayout.LabelColumnWidth;
            body.Add(_labelColumn);

            _scrollView = new ScrollView(ScrollViewMode.Horizontal);
            _scrollView.AddToClassList("ca-tl-scroll");
            _scrollView.style.flexGrow = 1f;
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scrollView.RegisterCallback<GeometryChangedEvent>(_ => UpdateViewportControl());
            body.Add(_scrollView);

            _canvasRoot = new VisualElement();
            _canvasRoot.AddToClassList("ca-tl-canvas-root");
            _scrollView.Add(_canvasRoot);

            Add(_bodyScroll);

            this.AddManipulator(new TimelinePanZoomManipulator(this, _scrollView));
            BuildItemPickerOverlay();
            RefreshCommandState();
        }

        private VisualElement BuildContextBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("ca-tl-context-bar");

            _contextSelector = new ToolbarMenu { text = "（未选择）" };
            _contextSelector.name = "timeline-context-selector";
            _contextSelector.tooltip = "选择要编辑的动作或效果时间轴（Ctrl+K 搜索）";
            _contextSelector.AddToClassList("ca-tl-context-selector");
            bar.Add(_contextSelector);

            var spacer = new VisualElement();
            spacer.AddToClassList("ca-tl-transport-spacer");
            bar.Add(spacer);

            var durationGroup = new VisualElement();
            durationGroup.AddToClassList("ca-tl-duration-group");
            var durationLabel = new Label("时长");
            durationLabel.AddToClassList("ca-tl-duration-label");
            durationGroup.Add(durationLabel);

            _durationField = new IntegerField();
            _durationField.AddToClassList("ca-tl-duration-field");
            _durationField.isDelayed = true;
            _durationField.RegisterValueChangedCallback(evt =>
            {
                if (_snapshot == null || _snapshot.ReadOnly)
                    return;
                if (_host?.DurationFramesChanged != null)
                {
                    int value = Math.Max(1, evt.newValue);
                    _host.DurationFramesChanged(value);
                    return;
                }
                if (_snapshot.Action == null)
                    return;
                ApplyDurationFramesFromInput(evt.newValue);
            });
            durationGroup.Add(_durationField);
            bar.Add(durationGroup);

            _saveButton = MakeButton("💾", () => _host?.SaveRequested?.Invoke());
            _saveButton.name = "timeline-save-button";
            _saveButton.tooltip = "保存当前时间轴模式涉及的所有未保存源文件。";
            _saveButton.AddToClassList("ca-tl-save-btn");
            _saveButton.AddToClassList("ca-tl-icon-btn");
            bar.Add(_saveButton);

            _dirtyLabel = new Label("已保存");
            _dirtyLabel.name = "timeline-dirty-label";
            _dirtyLabel.AddToClassList("ca-tl-dirty-label");
            bar.Add(_dirtyLabel);

            _addMenu = new ToolbarMenu { text = "+" };
            _addMenu.name = "timeline-add-menu";
            _addMenu.tooltip = "在当前播放头帧添加事件。";
            _addMenu.AddToClassList("ca-tl-toolbar-btn");
            _addMenu.AddToClassList("ca-tl-toolbar-menu");
            _addMenu.AddToClassList("ca-tl-icon-menu");
            bar.Add(_addMenu);

            _moreMenu = new ToolbarMenu { text = "⋯" };
            _moreMenu.name = "timeline-more-menu";
            _moreMenu.tooltip = "时间轴辅助功能。";
            _moreMenu.AddToClassList("ca-tl-toolbar-btn");
            _moreMenu.AddToClassList("ca-tl-toolbar-menu");
            _moreMenu.AddToClassList("ca-tl-icon-menu");
            bar.Add(_moreMenu);

            return bar;
        }

        private void UpdateReadOnlyBanner()
        {
            if (_readOnlyBannerHost == null)
                return;

            _readOnlyBannerHost.Clear();
            if (_snapshot != null && _snapshot.ReadOnly)
            {
                var banner = new HelpBox(
                    "当前动作为 CombatAnchored：时长由战斗导出决定，时间轴为只读展示。",
                    HelpBoxMessageType.Info);
                banner.AddToClassList("ca-tl-readonly-banner");
                _readOnlyBannerHost.Add(banner);
                _readOnlyBannerHost.style.display = DisplayStyle.Flex;
            }
            else
            {
                _readOnlyBannerHost.style.display = DisplayStyle.None;
            }
        }

        public void RefreshCommandState()
        {
            bool canSave = _host?.SaveRequested != null
                && (_host.CanSaveProvider?.Invoke() ?? false);
            if (_saveButton != null)
                _saveButton.SetEnabled(canSave);

            string dirtyText = _host?.DirtyStateProvider?.Invoke();
            if (string.IsNullOrWhiteSpace(dirtyText))
                dirtyText = "已保存";
            if (_dirtyLabel != null)
            {
                _dirtyLabel.text = dirtyText;
                _dirtyLabel.EnableInClassList("ca-tl-dirty-label--dirty", canSave);
                _dirtyLabel.EnableInClassList("ca-tl-dirty-label--clean", !canSave);
            }

            if (_durationField != null)
                _durationField.SetEnabled(_snapshot != null && !_snapshot.ReadOnly);

            RefreshContextSelector();
            RebuildAddMenu();
            RebuildMoreMenu();
            UpdatePlaybackChrome();
            UpdateReadOnlyBanner();
        }

        private void RebuildAddMenu()
        {
            if (_addMenu == null)
                return;

            _addMenu.menu.MenuItems().Clear();
            int frame = CurrentTimelineFrame();
            if (_host?.AddMenuProvider != null)
                _host.AddMenuProvider(_addMenu.menu, frame);
            else
                PopulateDefaultAddMenu(_addMenu.menu, frame);

            bool canAdd = _snapshot != null
                && !_snapshot.ReadOnly
                && (_host?.AddMenuProvider != null || _host?.AddEventAtTrack != null || _snapshot.Action != null);
            _addMenu.SetEnabled(canAdd);
        }

        private void RebuildMoreMenu()
        {
            if (_moreMenu == null)
                return;

            _moreMenu.menu.MenuItems().Clear();
            _moreMenu.menu.AppendAction("适应宽度", _ => FitToWidth(), DropdownMenuAction.AlwaysEnabled);
            _moreMenu.menu.AppendAction("缩小", _ => Zoom(-1.5f), DropdownMenuAction.AlwaysEnabled);
            _moreMenu.menu.AppendAction("放大", _ => Zoom(1.5f), DropdownMenuAction.AlwaysEnabled);
            _moreMenu.menu.AppendSeparator();
            _moreMenu.menu.AppendAction(
                "显示事件描述",
                _ =>
                {
                    _state.ShowMarkerTooltipText = !_state.ShowMarkerTooltipText;
                    RefreshMarkerTooltipMode();
                },
                _ => _state.ShowMarkerTooltipText
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);

            if (_host?.MoreMenuProvider == null)
                return;

            _moreMenu.menu.AppendSeparator();
            _host.MoreMenuProvider(_moreMenu.menu);
        }

        private void ApplyDurationFramesFromInput(int rawValue)
        {
            if (_snapshot?.Action == null || _snapshot.ReadOnly)
                return;

            int value = Math.Max(1, rawValue);
            if (_snapshot.ActionDurationFrames == value
                && _snapshot.Action.DurationFrames.HasValue
                && _snapshot.Action.DurationFrames.Value == value)
            {
                _durationField?.SetValueWithoutNotify(value);
                return;
            }

            CharacterActionJsonDto action = _snapshot.Action;
            _host?.CommitField(() =>
            {
                action.DurationFrames = value;
                CharacterActionPhaseTimelineUtility.NormalizePhases(action, value);
            }, true);
            RebindSnapshotForDuration(action, value);
        }

        private void RebindSnapshotForDuration(CharacterActionJsonDto action, int durationFrames)
        {
            int fallback = _host?.DefaultDurationFrames ?? 8;
            _snapshot = CharacterActionTimelineSnapshot.FromAction(action, fallback);
            if (_state.SelectedItemId != null && _snapshot.FindItem(_state.SelectedItemId) == null)
                _state.SelectedItemId = string.Empty;

            _durationField?.SetValueWithoutNotify(_snapshot.ActionDurationFrames);
            _playbackController?.SetDuration(durationFrames);
            if (_playbackController != null)
            {
                int maxFrame = Math.Max(0, durationFrames - 1);
                _playbackController.Seek(Math.Min(_playbackController.CurrentFrame, maxFrame));
            }
            else
            {
                _state.PlayheadFrame = CharacterActionTimelineLayout.ClampFrame(_state.PlayheadFrame, durationFrames);
            }

            RebuildCanvas(preserveScroll: true);
            UpdateFrameLabel();
        }

        private static Button MakeButton(string text, Action onClick)
        {
            return MakeButton(onClick, text);
        }

        private static Button MakeButton(string text, Action onClick, string tooltip)
        {
            return MakeButton(onClick, text, tooltip);
        }

        private static Button MakeButton(Action onClick, string text = "", string tooltip = null)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ca-tl-toolbar-btn");
            if (!string.IsNullOrEmpty(tooltip))
                button.tooltip = tooltip;
            return button;
        }
    }
}
