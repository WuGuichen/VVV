using System;
using System.Linq;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalAuthoringLayoutBinder
    {
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel _workbenchModel;
        private float _overviewConditionsOffset = 250f;

        // All objects below are Composer-published controls; this only arranges their projection.
        private void ApplySequential(Fgui.GComponent root, Fgui.GComponent header)
        {
            if (_applying || !_state.TryGet(root.width, root.height, out var layout)) return;
            if (MaximizeEventTimeline)
            {
                // Temporary projection only: retain the user's normal divider preference for exit.
                var fullTimeline = layout.Timeline;
                fullTimeline.height = layout.Preview.yMax - fullTimeline.y;
                layout = new CharacterTestRuntimeAiLabTacticalAuthoringLayout(layout.Overview,
                    layout.Preview, layout.Divider, fullTimeline, layout.Inspector);
            }
            _applying = true;
            try
            {
                foreach (string name in new[] { "workbenchSummaryText", "btnWorkbenchDuplicate", "workspaceTitle", "btnWorkspaceBack",
                    "btnReloadProjectDraft", "btnReapplyProjectDraft", "workspaceDivider", "timelinePanel", "timelineModeHost",
                    "btnTimelineModeLogic", "btnTimelineModePlan", "btnTimelineModeRuntime", "projectCardMetadataTitle",
                    "inputProjectCardCost", "inputProjectCardTags", "inputProjectCardUseSummary", "inputProjectCardRiskSummary",
                    "btnApplyDraft", "btnPublishDraft", "btnReset", "workbenchEntryList", "btnTacticalTemplate",
                    "btnTacticalBlank", "btnTacticalDuplicate", "btnCreateBehaviorCard", "btnWorkbenchEdit",
                    "btnWorkbenchToggle", "btnWorkbenchDelete" }) HideOwned(root.GetChild(name));
                foreach (string name in new[] { "responsibilityTabAll", "responsibilityTabTargeting", "responsibilityTabEngage",
                    "responsibilityTabDamage", "responsibilityTabFallback", "responsibilityTabOther" }) HideOwned(root.GetChild(name));
                var background = root.GetChild("behaviorCardWorkspaceBackground");
                background.visible = true;
                Geometry(background, new Rect(0, 0, root.width, root.height));
                background.asGraph?.DrawRect(root.width, root.height, 0, Color.clear, new Color32(8, 17, 25, 255));

                Geometry(root.GetChild("authoringContextBar"), new Rect(0, 0, root.width, 60));
                Geometry(root.GetChild("btnAuthoringWorkspaceRole"), new Rect(12, 10, 130, 38));
                Geometry(root.GetChild("authoringGroupText"), new Rect(156, 5, 280, 20));
                Geometry(root.GetChild("authoringCardText"), new Rect(156, 29, 280, 22));
                Geometry(root.GetChild("authoringDraftStateText"), new Rect(444, 20, 150, 22));
                HideOwned(root.GetChild("authoringProblemText"));
                HideOwned(root.GetChild("authoringPublishedStateText"));
                Geometry(root.GetChild("authoringTestStateText"), new Rect(root.width - 802, 19, 180, 24));
                Geometry(root.GetChild("btnTestCurrentChanges"), new Rect(root.width - 612, 10, 176, 40));
                Geometry(root.GetChild("btnPublishCurrentChanges"), new Rect(root.width - 426, 10, 166, 40));
                if (root.GetChild("btnPublishCurrentChanges") is Fgui.GButton publish)
                {
                    publish.title = "发布行为组";
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyCommandRailButtonVisual(publish, publish.enabled, false);
                }
                if (root.GetChild("btnTestCurrentChanges") is Fgui.GButton test)
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyCommandRailButtonVisual(test, test.enabled, true);
                Geometry(root.GetChild("btnClose"), new Rect(root.width - 52, 10, 40, 40));
                BindExecutionViewSwitch(root, layout);

                Geometry(root.GetChild("projectCardMetadataPanel"), layout.Overview);
                var title = root.GetChild("inputProjectCardDisplayName");
                title.visible = true;
                Geometry(title, new Rect(layout.Overview.x + 16, layout.Overview.y + 18, layout.Overview.width - 32, 42));
                if (title.asTextField != null) { title.asTextField.textFormat.size = 21; title.asTextField.textFormat.bold = true; }
                var description = root.GetChild("inputProjectCardDescription");
                description.visible = true;
                Geometry(description, new Rect(layout.Overview.x + 16, layout.Overview.y + 66, layout.Overview.width - 32, 84));
                // FairyGUI detects Latin word boundaries after whitespace, including inside Chinese quotes.
                description.text = BuildOverviewBrief()?.Replace("“", "“\u2009");
                description.tooltips = _compositionSession.DescriptionText + "\n\n" + _compositionSession.DescriptionDetails;
                if (description.asTextField != null)
                {
                    description.asTextField.singleLine = false;
                    description.asTextField.autoSize = Fgui.AutoSizeType.None;
                    description.asTextField.verticalAlign = Fgui.VertAlignType.Top;
                    description.asTextField.textFormat.size = 14;
                    description.asTextField.textFormat.color = new Color32(185, 205, 216, 255);
                    if (description.asTextInput != null) description.asTextInput.editable = false;
                }
                float descriptionHeight = Mathf.Clamp(description.asTextField?.textHeight ?? 44f, 36, 84);
                Geometry(description, new Rect(description.x, description.y, description.width, descriptionHeight));
                float roleY = 66 + descriptionHeight + 16;

                var controller = _compositionSession.Controller;
                var preview = _workbenchModel?.BehaviorCardWorkspace?.Timeline?.TacticalPreview;
                string role = preview?.IsFrozen == true ? "冻结角色 · " + preview.FrozenCharacterId : controller.TacticalPreviewRoleLabel;
                SetWorkbenchText(root, CharacterTestTacticalPreviewControlContract.BindingLabel, preview?.IsFrozen == true ? role : "测试角色 · " + role,
                    new Rect(layout.Overview.x + 16, layout.Overview.y + roleY, layout.Overview.width - 32, 26));
                root.GetChild(CharacterTestTacticalPreviewControlContract.BindingLabel).tooltips = role;
                if (root.GetChild(CharacterTestTacticalPreviewControlContract.BindingLabel).asTextField is Fgui.GTextField roleText)
                { roleText.textFormat.size = 13; roleText.color = new Color32(145, 175, 188, 255); }
                var actionLocations = preview?.IsFrozen == true ? Array.Empty<CharacterTestTacticalActionLocation>()
                    : controller.TacticalActionLocations(_compositionSession.CardId);
                bool hasActionSlots = preview?.IsFrozen == true
                    ? _compositionSession.Document?.Definitions?.Any(d => d.Source?.Slots?.Count > 0) == true
                    : actionLocations.Length > 0;
                float actionY = roleY + 32;
                SetWorkbenchText(root, CharacterTestTacticalPreviewControlContract.BindingValue,
                    preview?.IsFrozen == true ? "历史动作 · 只读" : actionLocations.Length == 1
                        ? "动作：" + actionLocations[0].ActionName + "  ▾" : "配置动作（" + actionLocations.Length + "）  ▾",
                    new Rect(layout.Overview.x + 16, layout.Overview.y + actionY, layout.Overview.width - 32, 34));
                if (root.GetChild(CharacterTestTacticalPreviewControlContract.BindingValue) is Fgui.GObject binding)
                {
                    var picker = root.GetChild("tacticalBindingPicker")?.asButton;
                    if (picker == null)
                    {
                        var template = root.GetChild("btnTestCurrentChanges");
                        if (!string.IsNullOrEmpty(template?.resourceURL))
                        {
                            picker = Fgui.UIPackage.CreateObjectFromURL(template.resourceURL)?.asButton;
                            if (picker != null) { picker.name = "tacticalBindingPicker"; root.AddChild(picker); }
                        }
                    }
                    if (picker != null)
                    {
                        binding.visible = false; binding.touchable = false; binding.onClick.Clear();
                        picker.visible = hasActionSlots; picker.sortingOrder = 85; picker.icon = string.Empty;
                        picker.title = binding.text; picker.titleFontSize = 15;
                        picker.SetXY(binding.x, binding.y); picker.SetSize(binding.width, binding.height);
                        picker.enabled = _compositionSession?.CanEdit == true;
                        picker.tooltips = preview?.IsFrozen == true ? "历史冻结动作，只读。"
                            : (actionLocations.Length == 1 ? actionLocations[0].Label + "\n" : "")
                            + "选择这里使用的具体动作；用于当前角色，下次出牌／测试生效。";
                        var anchor = picker;
                        picker.onClick.Set(() => _composition.ShowTestActionPicker(anchor));
                    }
                }

                string status = preview?.IsFrozen == true ? "历史冻结装配 · 只读"
                    : !controller.TacticalPreviewRoleEnabled ? "槽位未启用 · 无法测试"
                    : preview?.Ready == true ? _compositionSession?.EditingSourceTimeline == true
                        ? "正在编辑封装 · 修改后更新预览" : "装配已解析 · 整卡编译就绪"
                    : "未就绪 · " + (preview?.WholeCard?.Diagnostic ?? preview?.Diagnostic ?? "等待装配解析");
                float statusY = hasActionSlots ? actionY + 40 : actionY;
                bool ready = preview?.Ready == true && controller.TacticalPreviewRoleEnabled;
                SetWorkbenchText(root, "authoringProblemText", preview?.IsFrozen == true ? "历史版本 · 只读"
                    : ready ? "配置已就绪" : "未就绪 · 悬停查看原因",
                    new Rect(layout.Overview.x + 16, layout.Overview.y + statusY, layout.Overview.width - 32, 24));
                _overviewConditionsOffset = statusY + 42;
                var readiness = root.GetChild("authoringProblemText");
                readiness.tooltips = status + (string.IsNullOrEmpty(_compositionSession.DescriptionWarnings) ? "" : "\n" + _compositionSession.DescriptionWarnings);
                if (readiness.asTextField != null)
                {
                    readiness.asTextField.singleLine = true;
                    readiness.asTextField.autoSize = Fgui.AutoSizeType.None;
                    readiness.asTextField.textFormat.size = 13;
                    readiness.asTextField.textFormat.color = preview?.Ready == true && controller.TacticalPreviewRoleEnabled
                        ? new Color32(94, 213, 170, 255) : new Color32(255, 156, 103, 255);
                }

                const float railWidth = 72f, stripHeight = 142f;
                var composition = root.GetChild(CharacterTestTacticalPreviewControlContract.CompositionPanel)?.asCom;
                Geometry(composition, layout.Timeline);
                composition.visible = true;
                composition.sortingOrder = 35;
                var timeline = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline)?.asCom;
                Geometry(timeline, new Rect(layout.Timeline.x + railWidth, layout.Timeline.y + stripHeight,
                    layout.Timeline.width - railWidth, layout.Timeline.height - stripHeight));
                timeline.visible = _workbenchModel?.BehaviorCardWorkspace?.Timeline?.TacticalPreview?.WholeCard != null || _showInternal;
                timeline.sortingOrder = 36;

                var runtime = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)?.asCom;
                if (runtime != null)
                {
                    Geometry(runtime, new Rect(0, 0, root.width, root.height));
                    var inspector = runtime.GetChild("behaviorRecipeInspector")?.asCom;
                    Geometry(inspector, layout.Inspector);
                    if (inspector != null) inspector.SetScale(1, 1);
                }
                _composition.ArrangeSequential(layout.Timeline.width, layout.Timeline.height, root);
                if (_workbenchModel?.BehaviorCardWorkspace?.Timeline?.InspectCompiledExecution == true)
                {
                    composition.visible = false;
                    Geometry(timeline, layout.Timeline);
                    HideOwned(root.GetChild("btnConfirmShared")); HideOwned(root.GetChild("btnCopyShared"));
                }
                if (_workbenchModel?.BehaviorCardWorkspace?.Timeline?.TacticalPreview?.WholeCard?.TryGetSelectedControl(out _, out _) == true)
                {
                    HideOwned(root.GetChild("btnConfirmShared"));
                    HideOwned(root.GetChild("btnCopyShared"));
                }

                header.visible = !MaximizeEventTimeline;
                header.sortingOrder = 40;
                Geometry(header, layout.Preview);
                Geometry(header.GetChild("scenePreviewGuide"), new Rect(0, 0, layout.Preview.width, layout.Preview.height));
                foreach (string name in new[] { "runtimeControlBar", "runtimeHeaderText", "runtimeFrameSummaryText", "scenePreviewHint",
                    "runtimeStatePanel", "runtimeStateText", "executionPanel", "executionSummaryText", "recentEventsText",
                    "runtimeScrubTrack", "runtimeScrubEvicted", "runtimeScrubCaptured", "runtimeScrubFuture", "runtimeScrubThumb",
                    "runtimeScrubPending", "runtimeScrubQueued", "runtimeScrubHover" })
                    if (header.GetChild(name) is Fgui.GObject child) child.visible = false;
                string[] names = { "btnRuntimeFirst", "btnRuntimePlayPause", "btnRuntimeForward1", "btnRuntimeBack10" };
                string[] labels = { "|◀", "▶", "+1", "↻" };
                for (int i = 0; i < names.Length; i++)
                {
                    var button = header.GetChild(names[i])?.asButton;
                    if (button == null) continue;
                    button.visible = true;
                    Geometry(button, new Rect(10 + i * 52, layout.Preview.height - 48, 44, 36));
                    if (i != 1) button.title = labels[i];
                    else
                    {
                        bool playing = button.title.Contains("暂停") || button.title == "Ⅱ";
                        button.title = playing ? "Ⅱ" : "▶";
                        if (button.enabled)
                            button.tooltips = playing ? "暂停测试" : "播放测试";
                    }
                    if (button.GetChild("title") is Fgui.GTextField caption)
                    {
                        caption.SetXY(2, 0); caption.SetSize(40, 36);
                        caption.align = Fgui.AlignType.Center; caption.verticalAlign = Fgui.VertAlignType.Middle;
                        caption.textFormat.size = 15;
                    }
                }
                var fit = header.GetChild("btnRuntimeFit")?.asButton;
                if (fit != null) { fit.visible = true; fit.title = "⊙"; fit.tooltips = "适配视野"; Geometry(fit, new Rect(layout.Preview.width - 106, 10, 44, 36)); }
                var expand = header.GetChild(CharacterTestTacticalPreviewControlContract.Expand)?.asButton;
                if (expand != null) { expand.visible = true; expand.title = "↗"; expand.tooltips = "放大预览"; Geometry(expand, new Rect(layout.Preview.width - 54, 10, 44, 36)); }
                ArrangePreviewCameraMode(header, layout.Preview.width);
                if (header.GetChild("btnRuntimeOverlay") is Fgui.GButton hurtboxes)
                {
                    hurtboxes.visible = true;
                    Geometry(hurtboxes, new Rect(222, layout.Preview.height - 48, 116, 36));
                    hurtboxes.titleFontSize = 14;
                }
                if (_divider != null)
                {
                    _divider.visible = !MaximizeEventTimeline;
                    Geometry(_divider, new Rect(0, -layout.Divider.height, layout.Divider.width, layout.Divider.height));
                }
                BindActivationOverview(root, layout);
                ArrangeLibrary(root, layout);
            }
            finally { _applying = false; }
        }

        private void BindExecutionViewSwitch(Fgui.GComponent root, CharacterTestRuntimeAiLabTacticalAuthoringLayout layout)
        {
            var model = _workbenchModel?.BehaviorCardWorkspace?.Timeline;
            if (model?.CompositionEditor == null) return;
            Bind("btnTimelineModeLogic", "制作视图", false, 0);
            Bind("btnTimelineModePlan", "底层时间轴", true, 1);
            void Bind(string name, string label, bool native, int column)
            {
                var button = root.GetChild(name)?.asButton;
                if (button == null) return;
                button.visible = button.enabled = true; button.title = label; button.sortingOrder = 85;
                float width = (layout.Overview.width - 32) / 2;
                Geometry(button, new Rect(layout.Overview.x + 12 + column * (width + 8), layout.Overview.yMax - 48, width, 36));
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyCommandRailButtonVisual(button, true, model.InspectCompiledExecution == native);
                if (button.GetChild("title") is Fgui.GTextField text)
                { text.SetXY(4, 0); text.SetSize(width - 8, 36); text.align = Fgui.AlignType.Center; text.verticalAlign = Fgui.VertAlignType.Middle; }
                button.onClick.Set(() => {
                    if (model.InspectCompiledExecution == native) return;
                    model.Presentation.SelectRecipe();
                    model.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
                    model.Presentation.SelectedRecipeEventStableKey = string.Empty;
                    model.CompositionEditor.SetNativeExecutionView(native);
                });
            }
        }

        private void ArrangePreviewCameraMode(Fgui.GComponent header, float width)
        {
            var mode = header.GetChild("btnRuntimeFollow")?.asButton;
            if (mode == null) return;
            mode.visible = true;
            Geometry(mode, new Rect(width - 290, 10, 176, 36));
            mode.titleFontSize = 14;
            if (mode.GetChild("title") is Fgui.GTextField caption)
            {
                caption.SetXY(4, 0); caption.SetSize(168, 36);
                caption.align = Fgui.AlignType.Center; caption.verticalAlign = Fgui.VertAlignType.Middle;
            }
        }

        private void SetWorkbenchText(Fgui.GComponent root, string name, string value, Rect rect)
        {
            var field = root.GetChild(name);
            if (field == null) return;
            field.visible = true; field.text = value; Geometry(field, rect);
        }

        private string BuildOverviewBrief()
        {
            var document = _compositionSession.Document;
            if (document == null) return _compositionSession.DescriptionText;
            var references = CharacterTestTacticalCompositionEdits.Nodes(document.Root)
                .Where(n => n.Kind == "Reference").Select(n => n.DefinitionId).ToArray();
            var names = document.Definitions.Where(d => references.Contains(d.DefinitionId))
                .SelectMany(d => d.Source.Timelines).SelectMany(t => t.Events).Select(CharacterTestRuntimeAiPlannerDevSceneController.TacticalActionEventTitle)
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray();
            return names.Length == 0 ? _compositionSession.DescriptionText : "包含：" + string.Join(" · ", names);
        }
    }
}
