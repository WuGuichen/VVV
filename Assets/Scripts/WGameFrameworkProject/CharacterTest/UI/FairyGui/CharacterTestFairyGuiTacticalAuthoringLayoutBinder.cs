using System;
using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Arranges published controls only. Layout commands never enter the simulation command sink.</summary>
    internal sealed partial class CharacterTestFairyGuiTacticalAuthoringLayoutBinder
    {
        private readonly CharacterTestRuntimeAiLabTacticalAuthoringLayoutState _state = new CharacterTestRuntimeAiLabTacticalAuthoringLayoutState();
        private readonly Dictionary<Fgui.GObject, bool> _hidden = new Dictionary<Fgui.GObject, bool>();
        private readonly Dictionary<Fgui.GObject, (Rect bounds, Vector2 scale, int order)> _originalGeometry = new();
        private Fgui.GComponent _root;
        private Fgui.GObject _divider;
        private bool _dragging;
        private int _touchId;
        private bool _applying;
        private bool _wideInspector;
        private readonly CharacterTestFairyGuiTacticalCompositionBinder _composition = new CharacterTestFairyGuiTacticalCompositionBinder();
        private CharacterTestTacticalCompositionEditingSession _compositionSession;
        private bool _showInternal;
        private bool MaximizeEventTimeline => !_state.Expanded
            && _compositionSession?.EventFocusActive == true
            && !_compositionSession.NativeExecutionView;
        internal void PrepareInspectorBounds(Fgui.GComponent root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel model)
        {
            if (root == null || !AuthoringPageActive(model)) return;
            _state.Sequential = model?.BehaviorCardWorkspace?.Timeline?.CompositionEditor != null;
            var inspector = root.GetChild("runtimeFrameWorkspace")?.asCom?.GetChild("behaviorRecipeInspector")?.asCom;
            if (inspector == null) return;
            inspector.relations.ClearAll();
            float height = Mathf.Max(600, root.height - 104);
            bool wide = model?.BehaviorCardWorkspace?.Timeline?.InspectCompiledExecution == true
                || (model?.BehaviorCardWorkspace?.Timeline?.TacticalPreview?.IsConditionProjection == true
                    && model.BehaviorCardWorkspace.Timeline.TacticalPreview.Editor?.ConditionsOpen == true);
            _state.InspectorWidth = wide ? 520f : 420f;
            if (!_state.TryGet(root.width, root.height, out var layout)) return;
            if (_state.Sequential) height = layout.Inspector.height;
            float targetW = wide ? 520f : layout.Inspector.width;
            inspector.SetSize(targetW, height);
            if (model?.BehaviorCardWorkspace?.Timeline?.TacticalPreview?.Editor != null)
                model.BehaviorCardWorkspace.Timeline.TacticalPreview.Editor.InspectorHeight = height;
        }
        private Action _rebind;

        private static bool AuthoringPageActive(CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel model)
            => model != null && (model.ShowBehaviorLogic || model.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);

        internal void BeginBind(Fgui.GComponent root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel model)
        {
            _rebind = null;
            RestoreHidden();
            // Restore before the destination page applies its own visibility and geometry.
            if (!ReferenceEquals(_root, root) || !AuthoringPageActive(model))
            {
                EndDrag(); RestoreGeometry(); _composition.Deactivate();
                _compositionSession = null; _workbenchModel = null; _showInternal = false;
                _state.SetExpanded(false); _libraryOpen = false;
                HideAuthoringExtras(_root);
                HideAuthoringExtras(root);
            }
        }

        private static void HideAuthoringExtras(Fgui.GComponent root)
        {
            if (root == null || root.isDisposed) return;
            foreach (string name in new[] { CharacterTestTacticalPreviewControlContract.ActivationInspector,
                CharacterTestTacticalPreviewControlContract.BindingLabel, CharacterTestTacticalPreviewControlContract.BindingValue,
                CharacterTestTacticalPreviewControlContract.RequiredText, CharacterTestTacticalPreviewControlContract.CompositionPanel,
                CharacterTestTacticalPreviewControlContract.Blank, CharacterTestTacticalPreviewControlContract.Template,
                CharacterTestTacticalPreviewControlContract.Duplicate, "tacticalBindingPicker" })
                if (root.GetChild(name) is Fgui.GObject child) child.visible = false;
        }

        private void RestoreGeometry()
        {
            foreach (var entry in _originalGeometry)
            {
                var item = entry.Key; if (item == null || item.isDisposed) continue;
                var value = entry.Value;
                item.SetXY(value.bounds.x, value.bounds.y); item.SetSize(value.bounds.width, value.bounds.height);
                item.SetScale(value.scale.x, value.scale.y); item.sortingOrder = value.order;
            }
            _originalGeometry.Clear();
        }

        internal bool Expanded => _state.Expanded;
        internal float SplitRatio => _state.SplitRatio;

        internal void Bind(Fgui.GComponent root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel model,
            Action rebind, ICharacterTestRuntimeAiLabBehaviorCardPreviewSurface preview)
        {
            if (!AuthoringPageActive(model))
            {
                _rebind = null; EndDrag(); _composition.Deactivate();
                _compositionSession = null; _workbenchModel = null; _showInternal = false;
                HideAuthoringExtras(root);
                preview?.SetActive(false);
                return;
            }
            if (!ReferenceEquals(_root, root))
            {
                EndDrag();
                RestoreHidden();
                if (_root != null && !_root.isDisposed) _root.onSizeChanged.Remove(OnRootSizeChanged);
                _root = root;
                _root?.onSizeChanged.Add(OnRootSizeChanged);
            }
            _wideInspector = model?.BehaviorCardWorkspace?.Timeline?.InspectCompiledExecution == true
                || (model?.BehaviorCardWorkspace?.Timeline?.TacticalPreview?.IsConditionProjection == true
                    && model.BehaviorCardWorkspace.Timeline.TacticalPreview.Editor?.ConditionsOpen == true);
            _compositionSession = model?.BehaviorCardWorkspace?.Timeline?.CompositionEditor;
            _state.Sequential = _compositionSession != null;
            _state.InspectorWidth = _wideInspector ? 520f : 420f;
            _workbenchModel = model;
            if (_compositionSession == null)
                foreach (string name in new[] { CharacterTestTacticalPreviewControlContract.ActivationInspector,
                    CharacterTestTacticalPreviewControlContract.BindingLabel, CharacterTestTacticalPreviewControlContract.BindingValue,
                    CharacterTestTacticalPreviewControlContract.RequiredText, "tacticalBindingPicker" })
                    if (root.GetChild(name) is Fgui.GObject child) child.visible = false;
            _showInternal = _compositionSession != null
                ? model?.BehaviorCardWorkspace?.Timeline?.TacticalPreview?.IsConditionProjection == false
                : model?.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null;
            _composition.Bind(root?.GetChild(CharacterTestTacticalPreviewControlContract.CompositionPanel)?.asCom, _compositionSession);
            var header = root?.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader)?.asCom;
            var expand = header?.GetChild(CharacterTestTacticalPreviewControlContract.Expand)?.asButton;
            bool active = model != null && (model.ShowBehaviorLogic || model.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
            if (!active || expand == null)
            {
                if (root?.GetChild("tacticalBindingPicker") is Fgui.GObject picker) picker.visible = false;
                var compositionPanel = root?.GetChild(CharacterTestTacticalPreviewControlContract.CompositionPanel);
                if (compositionPanel != null) compositionPanel.visible = false;
                EndDrag();
                RestoreHidden();
                return;
            }
            _rebind = () =>
            {
                RestoreHidden();
                expand.title = _state.Expanded ? "返回编辑" : "放大预览";
                Apply(root, header);
                preview?.Bind(header, model, !MaximizeEventTimeline);
                header.visible = !MaximizeEventTimeline;
                if (_compositionSession == null && model.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null)
                    header.GetChild("runtimeHeaderText").text = model.HasTacticalTestFrame?.Invoke() == true
                        ? header.width < 720 ? "本轮测试" : "本轮测试画面 · 冻结版本"
                        : header.width < 720 ? "场景预览" : "场景预览 · 尚未开始测试";
                if (_compositionSession == null && model.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null)
                    header.GetChild("runtimeFrameSummaryText").text = model.TacticalFrameSummary?.Invoke() ?? "尚未测试";
                rebind?.Invoke();
            };
            _divider = header.GetChild(CharacterTestTacticalPreviewControlContract.Divider);
            expand.title = _state.Expanded ? "返回编辑" : "放大预览";
            expand.enabled = true;
            expand.visible = true;
            expand.onClick.Set(() =>
            {
                EndDrag();
                RestoreHidden();
                _state.SetExpanded(!_state.Expanded);
                _rebind?.Invoke();
            });
            if (_divider != null)
            {
                _divider.touchable = true;
                _divider.onTouchBegin.Set(OnDividerBegin);
                _divider.onTouchMove.Set(OnDividerMove);
                _divider.onTouchEnd.Set(OnDividerEnd);
            }
            var blank = header.GetChild(CharacterTestTacticalPreviewControlContract.Blank);
            var template = header.GetChild(CharacterTestTacticalPreviewControlContract.Template);
            var duplicate = header.GetChild(CharacterTestTacticalPreviewControlContract.Duplicate);
            if (blank != null && blank.parent != root) root.AddChild(blank);
            if (template != null && template.parent != root) root.AddChild(template);
            if (duplicate != null && duplicate.parent != root) root.AddChild(duplicate);

            BindTacticalButton(header, CharacterTestTacticalPreviewControlContract.Import, model.ImportTacticalSource,
                model.CanEditWorkbench, false);
            BindTacticalButton(header, CharacterTestTacticalPreviewControlContract.Prepare, model.PrepareTacticalSummary,
                model.PrepareTacticalSummary != null, false);
            BindTacticalButton(header, CharacterTestTacticalPreviewControlContract.Requery, model.RequeryTacticalSource,
                true, false);
            var editor = model.BehaviorCardWorkspace?.Timeline?.TacticalPreview?.Editor;
            Action duplicateAction = _compositionSession != null ? _compositionSession.CopyCard : (editor == null ? null : editor.Duplicate);
            BindTacticalButton(root, CharacterTestTacticalPreviewControlContract.Blank, model.CreateTacticalBlank, model.CanEditWorkbench, false);
            BindTacticalButton(root, CharacterTestTacticalPreviewControlContract.Template, model.CreateTacticalTemplate, model.CanEditWorkbench, true);
            BindTacticalButton(root, CharacterTestTacticalPreviewControlContract.Duplicate, duplicateAction, model.CanEditWorkbench, true);
            if (editor != null)
            {
                editor.Locate = (timelineId, eventId) => {
                    if (editor.Preview.FindEvent(timelineId, eventId) == null) { editor.Feedback("关联事件已不存在，请修改来源或撤销删除。"); return; }
                    var timelineModel = model.BehaviorCardWorkspace.Timeline;
                    editor.Selection.Clear(); editor.Selection.Add(eventId);
                    timelineModel.Presentation.SelectEvent(timelineId, eventId);
                    timelineModel.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
                    timelineModel.Presentation.SelectedRecipeEventStableKey = BehaviorRecipeTimelineProjection.BuildV6EventStableKey(timelineId, eventId);
                    if (editor.Preview.TryGetFrames(eventId, out int start, out int end)) {
                        timelineModel.HorizontalStartFrame = Mathf.Max(0, start - 10);
                        timelineModel.HorizontalEndFrame = Mathf.Max(timelineModel.HorizontalStartFrame + 120, end + 10);
                    }
                    editor.ShowConditions(false);
                };
                var save = root.GetChild("btnApplyDraft")?.asButton;
                if (save != null) { save.title = "保存源"; save.enabled = editor.CanEdit; save.onClick.Set(editor.Save); }
            }
            _rebind();
        }

        private static void BindTacticalButton(Fgui.GComponent parent, string name, Action action, bool enabled, bool shown)
        {
            var button = parent?.GetChild(name)?.asButton;
            if (button == null) return;
            button.visible = shown;
            button.enabled = enabled && action != null;
            button.onClick.Set(() => action?.Invoke());
        }

        private void Apply(Fgui.GComponent root, Fgui.GComponent header)
        {
            if (_compositionSession != null && !_state.Expanded) { ApplySequential(root, header); return; }
            if (_applying || !_state.TryGet(root.width, root.height, out var layout)) return;
            float compositionHeight = 0;
            if (_compositionSession != null && !_state.Expanded)
            {
                float width = layout.Timeline.width - (_wideInspector ? 520f - layout.Inspector.width : 0);
                compositionHeight = _composition.Arrange(width);
            }
            _state.MinimumTimelineHeight = _compositionSession == null ? 180 : compositionHeight + (_showInternal ? 180 : 0);
            if (!_state.TryGet(root.width, root.height, out layout)) return;
            if (_wideInspector && !_state.Expanded)
            {
                float extra = 520f - layout.Inspector.width;
                layout = new CharacterTestRuntimeAiLabTacticalAuthoringLayout(layout.Overview,
                    new Rect(layout.Preview.x, layout.Preview.y, layout.Preview.width - extra, layout.Preview.height),
                    new Rect(layout.Divider.x, layout.Divider.y, layout.Divider.width - extra, layout.Divider.height),
                    new Rect(layout.Timeline.x, layout.Timeline.y, layout.Timeline.width - extra, layout.Timeline.height),
                    new Rect(layout.Inspector.x - extra, layout.Inspector.y, 520f, layout.Inspector.height));
            }
            _applying = true;
            try
            {
                bool compactOverview = _compositionSession != null && layout.Overview.height < 800;
                if (_compositionSession != null)
                {
                    foreach (string name in new[] { "workbenchSummaryText", "btnWorkbenchDuplicate", "workspaceTitle", "btnWorkspaceBack" }) HideOwned(root.GetChild(name));
                    var bg = root.GetChild("behaviorCardWorkspaceBackground");
                    if (bg != null)
                    {
                        bg.visible = true;
                        bg.sortingOrder = 0;
                        Geometry(bg, new Rect(0, 0, root.width, root.height));
                        if (bg.asGraph != null)
                            bg.asGraph.DrawRect(root.width, root.height, 0, Color.black, new Color32(8, 17, 25, 255));
                    }
                    Geometry(root.GetChild("authoringContextBar"), new Rect(0, 0, root.width, 60));
                    if (root.width < 1600)
                    {
                        HideOwned(root.GetChild("authoringPublishedStateText")); HideOwned(root.GetChild("authoringTestStateText"));
                        Geometry(root.GetChild("authoringProblemText"), new Rect(590, 19, 170, 22));
                        Geometry(root.GetChild("btnTestCurrentChanges"), new Rect(root.width - 488, 10, 180, 40));
                        Geometry(root.GetChild("btnPublishCurrentChanges"), new Rect(root.width - 298, 10, 180, 40));
                        Geometry(root.GetChild("btnReset"), new Rect(root.width - 106, 8, 44, 44));
                        Geometry(root.GetChild("btnClose"), new Rect(root.width - 52, 10, 44, 40));
                    }
                }
                header.visible = true;
                header.sortingOrder = 40;
                Geometry(header, new Rect(layout.Preview.x, layout.Preview.y, layout.Preview.width,
                    layout.Preview.height + (_state.Expanded ? 0 : layout.Divider.height)));
                float controlsY = Mathf.Max(100f, layout.Preview.height - 48f);
                Geometry(header.GetChild("scenePreviewGuide"), new Rect(0, 0, layout.Preview.width, controlsY));
                Geometry(header.GetChild("runtimeControlBar"), new Rect(0, controlsY, layout.Preview.width, 48));
                string[] buttons = { "btnRuntimeFirst", "btnRuntimePlayPause", "btnRuntimeForward1", "btnRuntimeBack10", "btnRuntimeFit", "btnRuntimeFollow" };
                float[] widths = { 90, 88, 64, 44, 86, 86 };
                float x = 8;
                for (int i = 0; i < buttons.Length; i++)
                {
                    var button = header.GetChild(buttons[i])?.asButton;
                    if (button != null) { Geometry(button, new Rect(x, controlsY + 5, widths[i], 36)); button.titleFontSize = 13; }
                    x += widths[i] + 6;
                }
                Geometry(header.GetChild(CharacterTestTacticalPreviewControlContract.Expand),
                    new Rect(layout.Preview.width - 112, controlsY + 5, 104, 36));
                Geometry(header.GetChild("runtimeHeaderText"), new Rect(16, 12, Mathf.Max(180, layout.Preview.width - 32), 32));
                var hint = header.GetChild("scenePreviewHint");
                if (hint != null)
                {
                    hint.visible = true;
                    Geometry(hint, new Rect(16, controlsY - 28, Mathf.Max(180, layout.Preview.width - 32), 24));
                }
                foreach (var name in new[] { "runtimeStatePanel", "runtimeStateText", "executionPanel", "executionSummaryText", "recentEventsText", "runtimeScrubTrack", "runtimeScrubEvicted", "runtimeScrubCaptured", "runtimeScrubFuture", "runtimeScrubThumb", "runtimeScrubPending", "runtimeScrubQueued", "runtimeScrubHover" })
                { var child = header.GetChild(name); if (child != null) child.visible = false; }
                string[] tacticalButtons = { CharacterTestTacticalPreviewControlContract.Import,
                    CharacterTestTacticalPreviewControlContract.Prepare, CharacterTestTacticalPreviewControlContract.Requery,
                    CharacterTestTacticalPreviewControlContract.Blank, CharacterTestTacticalPreviewControlContract.Template, CharacterTestTacticalPreviewControlContract.Duplicate };
                for (int i = 0; i < tacticalButtons.Length; i++)
                {
                    var child = header.GetChild(tacticalButtons[i]);
                    if (child != null) child.visible = false;
                }
                Geometry(header.GetChild("runtimeFrameSummaryText"), new Rect(16, 16, Mathf.Max(80, Mathf.Min(140, layout.Preview.width - 356)), 24));
                if (header.GetChild("runtimeFrameSummaryText") is Fgui.GTextField frameText)
                { frameText.textFormat.size = 12; frameText.tooltips = "选中运行帧 / 最新运行帧"; }
                header.GetChild("runtimeHeaderText").visible = true;
                Geometry(header.GetChild("runtimeHeaderText"), new Rect(16, 44, Mathf.Max(80, layout.Preview.width - 360), 24));
                if (_divider != null)
                {
                    _divider.visible = !_state.Expanded;
                    Geometry(_divider, new Rect(0, layout.Preview.height, layout.Preview.width, 8));
                }
                if (_state.Expanded)
                {
                    if (_compositionSession != null)
                    {
                        HideOwned(root.GetChild("authoringProblemText"));
                        HideOwned(root.GetChild("authoringPublishedStateText"));
                        HideOwned(root.GetChild("btnReset"));
                        header.GetChild("runtimeHeaderText").visible = false;
                        header.GetChild("runtimeFrameSummaryText").visible = false;
                        ArrangePreviewCameraMode(header, layout.Preview.width);
                    }
                    foreach (var child in root.GetChildren())
                    {
                        if (ReferenceEquals(child, header) || child.name == "behaviorCardWorkspaceBackground" || (child.y < 80f && child.name != "runtimeFrameWorkspace")) continue;
                        if (!_hidden.ContainsKey(child)) _hidden.Add(child, child.visible);
                        child.visible = false;
                    }
                    return;
                }

                var timeline = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline);
                var timelineRect = layout.Timeline;
                if (_compositionSession != null)
                {
                    var panel = root.GetChild(CharacterTestTacticalPreviewControlContract.CompositionPanel);
                    float h = _showInternal ? compositionHeight : timelineRect.height;
                    Geometry(panel, new Rect(timelineRect.x, timelineRect.y, timelineRect.width, h));
                    panel.SetSize(timelineRect.width, h);
                    panel.asCom?.GetChild("background")?.SetSize(timelineRect.width, h);
                    panel.visible = true; panel.sortingOrder = 35;
                    if (_showInternal)
                    {
                        timelineRect.y += compositionHeight;
                        timelineRect.height -= compositionHeight;
                    }
                }
                Geometry(timeline, timelineRect);
                if (timeline != null) { timeline.visible = _compositionSession == null || _showInternal; timeline.sortingOrder = 30; }

                var runtime = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)?.asCom;
                if (runtime != null)
                {
                    runtime.SetXY(0, 0); runtime.SetSize(root.width, root.height);
                    var inspector = runtime.GetChild("behaviorRecipeInspector") ?? runtime.GetChild("behaviorTriggerEventInspector");
                    if (inspector != null)
                    {
                        inspector.relations.ClearAll();
                        inspector.SetXY(layout.Inspector.x, layout.Inspector.y);
                        inspector.SetScale(1, 1);
                        var panel = inspector.asCom;
                        if (panel != null)
                        {
                            panel.SetSize(layout.Inspector.width, layout.Inspector.height);
                            panel.SetXY(layout.Inspector.x, layout.Inspector.y);
                        }
                    }
                }

                Geometry(root.GetChild("projectCardMetadataPanel"), layout.Overview);
                Geometry(root.GetChild("projectCardMetadataTitle"), new Rect(layout.Overview.x + 12, layout.Overview.y + 10, layout.Overview.width - 24, 30));
                float y = layout.Overview.y + 50;
                foreach (string name in new[] { "inputProjectCardDisplayName", "inputProjectCardCost", "inputProjectCardTags",
                    "inputProjectCardDescription", "inputProjectCardUseSummary", "inputProjectCardRiskSummary" })
                {
                    if (_compositionSession != null && (name == "inputProjectCardCost" || name == "inputProjectCardTags"
                        || name == "inputProjectCardUseSummary" || name == "inputProjectCardRiskSummary"))
                    { var hidden = root.GetChild(name); if (hidden != null) hidden.visible = false; continue; }
                    float height = name.EndsWith("Summary", StringComparison.Ordinal) || name.EndsWith("Description", StringComparison.Ordinal) ? 64 : 34;
                    if (_compositionSession != null && name == "inputProjectCardDescription")
                    {
                        var description = root.GetChild(name);
                        description.SetSize(layout.Overview.width - 24, 180);
                        description.text = _compositionSession.DescriptionText;
                        float desiredHeight = Mathf.Max(180, (description.asTextField?.textHeight ?? 168) + 12);
                        height = Mathf.Min(desiredHeight, Mathf.Max(180, layout.Overview.height - (compactOverview ? 272 : 340)));
                        description.tooltips = desiredHeight > height ? "点击“说明详情”，滚动阅读完整说明。" : description.text;
                    }
                    Geometry(root.GetChild(name), new Rect(layout.Overview.x + 12, y, layout.Overview.width - 24, height));
                    y += height + 8;
                }
                foreach (string name in new[] { "workspaceDivider", "timelinePanel", "timelineModeHost" })
                { var child = root.GetChild(name); if (child != null) child.visible = false; }
                string[] modes = { "btnTimelineModeLogic", "btnTimelineModePlan", "btnTimelineModeRuntime" };
                for (int i = 0; i < modes.Length; i++) HideOwned(root.GetChild(modes[i]));
                string[] tabs = { "responsibilityTabAll", "responsibilityTabTargeting", "responsibilityTabEngage", "responsibilityTabDamage", "responsibilityTabFallback", "responsibilityTabOther" };
                for (int i = 0; i < tabs.Length; i++) HideOwned(root.GetChild(tabs[i]));
                Geometry(root.GetChild("btnApplyDraft"), new Rect(layout.Timeline.x + 11, layout.Timeline.y + 142, 98, 36));
                Geometry(root.GetChild("btnPublishDraft"), new Rect(layout.Timeline.x + 11, layout.Timeline.y + 182, 98, 36));
                if (_compositionSession != null)
                {
                    root.GetChild("btnApplyDraft").visible = false;
                    root.GetChild("btnPublishDraft").visible = false;
                    var deleteEntry = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry);
                    if (deleteEntry != null) deleteEntry.visible = false;
                    var toggleEntry = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry);
                    if (toggleEntry != null) toggleEntry.visible = false;
                }
                var list = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList);
                var library = root.GetChild("btnWorkbenchEdit");
                var create = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard);
                var templateBtn = root.GetChild(CharacterTestTacticalPreviewControlContract.Template);
                var duplicateBtn = root.GetChild(CharacterTestTacticalPreviewControlContract.Duplicate);

                float halfW = (layout.Overview.width - 20f) / 2f;
                float btnH = 30f;
                Geometry(library, new Rect(layout.Overview.x + 8, y, halfW, btnH));
                Geometry(create, new Rect(layout.Overview.x + 12 + halfW, y, halfW, btnH));
                Geometry(templateBtn, new Rect(layout.Overview.x + 8, y + btnH + 6f, halfW, btnH));
                Geometry(duplicateBtn, new Rect(layout.Overview.x + 12 + halfW, y + btnH + 6f, halfW, btnH));
                if (library is Fgui.GButton libBtn) libBtn.titleFontSize = 13;
                if (create is Fgui.GButton createBtn) createBtn.titleFontSize = 13;
                if (templateBtn != null) { templateBtn.visible = true; if (templateBtn is Fgui.GButton tb) tb.titleFontSize = 13; }
                if (duplicateBtn != null) { duplicateBtn.visible = true; if (duplicateBtn is Fgui.GButton db) db.titleFontSize = 13; }

                float listOffset = btnH * 2f + 14f;
                Geometry(list, new Rect(layout.Overview.x + 8, y + listOffset, layout.Overview.width - 16, Mathf.Max(80, layout.Overview.yMax - y - listOffset - 8)));
            }
            finally { _applying = false; }
        }

        private void OnDividerBegin(Fgui.EventContext context)
        {
            if (_state.Expanded || context.inputEvent == null || context.inputEvent.button != 0) return;
            _dragging = true; _touchId = context.inputEvent.touchId;
            context.CaptureTouch(); context.StopPropagation();
        }
        private void OnDividerMove(Fgui.EventContext context)
        {
            if (!_dragging || context.inputEvent == null || context.inputEvent.touchId != _touchId || _root == null) return;
            if (_state.SetDividerPosition(_root.GlobalToLocal(context.inputEvent.position), _root.width, _root.height))
                _rebind?.Invoke();
            context.StopPropagation();
        }
        private void OnDividerEnd(Fgui.EventContext context) { if (_dragging) { EndDrag(); context.StopPropagation(); } }
        private void EndDrag()
        {
            if (_dragging && Application.isPlaying && _divider != null) Fgui.Stage.inst.RemoveTouchMonitor(_divider);
            _dragging = false;
        }
        private void OnRootSizeChanged() { if (!_applying) _rebind?.Invoke(); }
        private void RestoreHidden()
        {
            foreach (var entry in _hidden) if (entry.Key != null && !entry.Key.isDisposed) entry.Key.visible = entry.Value;
            _hidden.Clear();
        }
        private void HideOwned(Fgui.GObject child)
        {
            if (child == null) return;
            if (!_hidden.ContainsKey(child)) _hidden.Add(child, child.visible);
            child.visible = false;
        }
        private void Geometry(Fgui.GObject value, Rect rect)
        {
            if (value == null) return;
            if (!_originalGeometry.ContainsKey(value)) _originalGeometry.Add(value,
                (new Rect(value.x, value.y, value.width, value.height), new Vector2(value.scaleX, value.scaleY), value.sortingOrder));
            value.relations.ClearAll();
            value.SetXY(rect.x, rect.y);
            value.SetSize(rect.width, rect.height);
            var com = value.asCom;
            if (com != null)
            {
                com.GetChild("segmentedSurface")?.SetSize(rect.width, rect.height);
                var lib = com.GetChild("logicLibrarySurface")?.asGraph;
                if (lib != null)
                {
                    lib.SetXY(0f, 0f);
                    lib.SetSize(rect.width, rect.height);
                    lib.DrawRoundRect(rect.width, rect.height, new Color32(32, 120, 104, 255), new float[] { 6f, 6f, 6f, 6f });
                }
                var cmd = com.GetChild("commandRailSurface")?.asGraph;
                if (cmd != null)
                {
                    cmd.SetXY(0f, 0f);
                    cmd.SetSize(rect.width, rect.height);
                    cmd.DrawRoundRect(rect.width, rect.height, new Color32(32, 120, 104, 255), new float[] { 6f, 6f, 6f, 6f });
                }
            }
        }
    }
}
