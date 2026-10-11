using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void ShowTacticalCreationMenu(string timeline, int frame, string afterEvent = null)
        {
            var preview = _recipeSource.TacticalPreview;
            if (preview.WholeCard != null || _recipeSource.InspectCompiledExecution)
            {
                BeginRecipeContextMenu();
                AddRecipeMenuItem("适配整卡可见帧范围", FitRecipeTimeline);
                EndRecipeContextMenu(); return;
            }
            var editor = preview.Editor;
            if (!editor.CanEdit) return;
            var token = editor.Token;
            BeginRecipeContextMenu();
            foreach (string kind in editor.Source.HasTimingVersion ? new[] { "CharacterAction", "Movement", "TurnToTarget", "WaitForCondition", "ReturnToPlanner", "LoopRegion", "EnterTimeline", "ReturnToPlanner:Instant", "EnterTimeline:Instant" }
                : new[] { "CharacterAction", "Movement", "TurnToTarget", "WaitForCondition", "ReturnToPlanner" })
            {
                string selectedKind = kind;
                string label = kind switch { "CharacterAction" => "角色动作", "Movement" => "移动（六种方式）",
                    "ApproachOptimalRange" => "接近最佳位置", "TurnToTarget" => "转向目标", "WaitForCondition" => "等待条件", "LoopRegion" => "循环区间", "EnterTimeline" => "进入时间轴", "ReturnToPlanner:Instant" => "单帧退出行为卡", "EnterTimeline:Instant" => "单帧进入时间轴", _ => "退出行为卡" };
                AddRecipeMenuItem("添加" + label, () => {
                    if (selectedKind != "CharacterAction") { editor.AddEvent(token, timeline, selectedKind, frame, afterEvent); return; }
                    BeginRecipeContextMenu();
                    foreach (var slot in editor.Source.Slots)
                    {
                        string slotId = slot.SlotId;
                        AddRecipeMenuItem("动作槽：" + slotId, () => editor.AddEvent(token, timeline, selectedKind, frame, afterEvent, slotId));
                    }
                    AddRecipeMenuItem("＋创建动作槽", () => editor.AddEvent(token, timeline, selectedKind, frame, afterEvent,
                        "slot." + Guid.NewGuid().ToString("N"), true));
                    EndRecipeContextMenu();
                });
            }
            EndRecipeContextMenu();
        }

        private void ShowRecipeBackgroundContextMenu(BehaviorRecipeTimelineViewModel timeline)
        {
            if (_recipeSource?.TacticalPreview?.Editor != null) { ShowTacticalCreationMenu(timeline?.StageId ?? _recipeSource.TacticalPreview.Source.EntryTimelineId, 0); return; }
            BeginRecipeContextMenu();
            AddRecipeMenuItem("➕  添加时间轴", () => AddRecipeTimeline(timeline));
            if (FindRecipeTimeline(_recipeCopiedTimelineStageId) != null)
            {
                AddRecipeMenuItem(
                    "⎘  粘贴时间轴 · " + _recipeCopiedTimelineTitle,
                    () => PasteRecipeTimeline(timeline));
            }
            AddRecipeMenuItem("⤢  适配可见时间", FitRecipeTimeline);
            EndRecipeContextMenu();
        }

        private void ShowRecipeTimelineContextMenu(BehaviorRecipeTimelineViewModel timeline)
        {
            if (_recipeSource?.TacticalPreview?.Editor != null) { ShowTacticalCreationMenu(timeline?.StageId ?? _recipeSource.TacticalPreview.Source.EntryTimelineId, 0); return; }
            BeginRecipeContextMenu();
            AddRecipeMenuItem("➕  在后面添加时间轴", () => AddRecipeTimeline(timeline));
            AddRecipeMenuItem("⎘  复制时间轴", () => CopyRecipeTimeline(timeline));
            AddRecipeMenuItem("⚡  添加单帧事件", () => AddRecipeEvent(
                timeline?.StageId,
                0,
                CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant));
            AddRecipeMenuItem("⏱  添加持续事件", () => AddRecipeEvent(
                timeline?.StageId,
                0,
                CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration));
            AddRecipeMenuItem("⚔  添加角色动作事件", () => AddRecipeCharacterActionEvent(
                timeline?.StageId,
                0));
            if (timeline?.Entry == false)
                AddRecipeMenuItem("⭐  设为行为入口", () => SetRecipeEntryTimeline(timeline));
            if (_recipeProjection.TimelineCount > 1)
                AddRecipeMenuItem("🗑  删除时间轴", () => DeleteRecipeTimeline(timeline), destructive: true);
            EndRecipeContextMenu();
        }

        private void ShowRecipeFrameContextMenu(string stageId, int frame)
        {
            if (_recipeSource?.TacticalPreview?.Editor != null) { ShowTacticalCreationMenu(stageId, frame); return; }
            BehaviorRecipeTimelineViewModel timeline = FindRecipeTimeline(stageId);
            BeginRecipeContextMenu();
            AddRecipeMenuItem("⚡  在 " + frame + "f 添加单帧事件", () => AddRecipeEvent(
                stageId,
                frame,
                CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant));
            AddRecipeMenuItem("⏱  在 " + frame + "f 添加持续事件", () => AddRecipeEvent(
                stageId,
                frame,
                CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration));
            AddRecipeMenuItem("⚔  在 " + frame + "f 添加角色动作事件", () =>
                AddRecipeCharacterActionEvent(stageId, frame));
            AddRecipeMenuItem("➕  在后面添加时间轴", () => AddRecipeTimeline(timeline));
            AddRecipeMenuItem("⎘  复制时间轴", () => CopyRecipeTimeline(timeline));
            if (timeline?.Entry == false)
                AddRecipeMenuItem("⭐  设为行为入口", () => SetRecipeEntryTimeline(timeline));
            if (_recipeProjection.TimelineCount > 1)
                AddRecipeMenuItem("🗑  删除时间轴", () => DeleteRecipeTimeline(timeline), destructive: true);
            EndRecipeContextMenu();
        }

        private void ShowRecipeEventContextMenu(BehaviorRecipeEventViewModel recipeEvent)
        {
            if (_recipeSource?.InspectCompiledExecution == true)
            {
                BeginRecipeContextMenu(); AddRecipeMenuItem("查看底层事件属性", () => SelectRecipeEvent(recipeEvent)); EndRecipeContextMenu(); return;
            }
            if (_recipeSource?.TacticalPreview?.Editor != null)
            {
                BeginRecipeContextMenu();
                AddRecipeMenuItem("在属性中编辑／确认删除", () => SelectRecipeEvent(recipeEvent, preserveSelection: true));
                var editor = _recipeSource.TacticalPreview.Editor;
                if (editor.Composition != null && _recipeSource.TacticalPreview.WholeCard == null)
                {
                    if (editor.Focused)
                    {
                        AddRecipeMenuItem("接在此事件之后添加…", () => ShowTacticalCreationMenu(recipeEvent.StageId, recipeEvent.StartFrame, recipeEvent.EventId));
                        var token = editor.Token; string[] ids = editor.Selected(recipeEvent.EventId);
                        AddRecipeMenuItem("复制所选底层事件", () => editor.CopyEvents(token, recipeEvent.StageId, ids));
                        AddRecipeMenuItem("删除所选底层事件…", () => {
                            string[] impact = CharacterTestTacticalAuthoringEdits.DeleteImpact(editor.Source, ids);
                            BeginRecipeContextMenu();
                            foreach (string item in impact) AddRecipeMenuItem(item, () => { });
                            AddRecipeMenuItem(impact.Length == 0 ? "确认删除" : "确认删除，保留断开的引用", () =>
                                editor.Commit(token, "删除底层事件", s => CharacterTestTacticalAuthoringEdits.Delete(s, recipeEvent.StageId, ids, true)), destructive: true);
                            EndRecipeContextMenu();
                        }, destructive: true);
                    }
                    else AddRecipeMenuItem("编辑编译结果", () => editor.OpenEventFocus(recipeEvent.StageId, recipeEvent.EventId));
                }
                EndRecipeContextMenu(); return;
            }
            BeginRecipeContextMenu();
            AddRecipeMenuItem("🔍  在 Inspector 中定位", () => SelectRecipeEvent(recipeEvent));
            if (recipeEvent?.Kind == BehaviorRecipeEventKind.Event)
            {
                if (recipeEvent.DurationReviewRequired)
                    AddRecipeMenuItem("✓  确认当前固定区间", () => ConfirmRecipeEventDuration(recipeEvent));
                AddRecipeMenuItem("🗑  删除事件", () => DeleteRecipeEvent(recipeEvent), destructive: true);
            }
            else if (recipeEvent?.Kind is BehaviorRecipeEventKind.Branch or BehaviorRecipeEventKind.LegacySharedBranch)
            {
                if (!recipeEvent.Shared
                    && recipeEvent.Primitive != BehaviorRecipeEventPrimitive.Marker)
                {
                    bool fixedWindow = recipeEvent.Primitive == BehaviorRecipeEventPrimitive.Span;
                    AddRecipeMenuItem(
                        fixedWindow ? "⏱  设为持续监听" : "⇥  设为固定监听区间",
                        () => SetRecipeBranchWindowMode(
                            recipeEvent,
                            fixedWindow
                                ? CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
                                : CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed));
                }
                AddRecipeMenuItem("⎘  复制分支事件", () => DuplicateRecipeBranch(recipeEvent));
                AddRecipeMenuItem("🗑  删除分支事件", () => DeleteRecipeEvent(recipeEvent), destructive: true);
            }
            else if (recipeEvent?.Kind == BehaviorRecipeEventKind.PrimaryAction)
            {
                AddRecipeMenuItem("✎  编辑主行为", () => SelectRecipeEvent(recipeEvent));
            }
            EndRecipeContextMenu();
        }

        private void ConfirmRecipeEventDuration(BehaviorRecipeEventViewModel recipeEvent)
        {
            if (!_canEdit || recipeEvent == null || string.IsNullOrEmpty(recipeEvent.EventId))
                return;
            _commands?.ApplyRecipeTriggerEventField(
                recipeEvent.StageId,
                recipeEvent.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConfirmDuration,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue());
            ShowRecipeFeedback("已确认固定区间。");
        }

        private void SetRecipeBranchWindowMode(
            BehaviorRecipeEventViewModel recipeEvent,
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode)
        {
            if (!_canEdit || recipeEvent == null || recipeEvent.Shared)
                return;
            _commands?.ApplyRecipeTriggerEventField(
                recipeEvent.StageId,
                recipeEvent.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.WindowEndMode,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)mode));
            ShowRecipeFeedback(_commands?.LastMessage);
        }

        private void BeginRecipeContextMenu()
        {
            _commands?.CancelCascadeConfirmation();
            for (int i = 0; i < _recipeMenuItems.Count; i++)
                _recipeMenuItems[i].Visual.visible = false;
            _recipeLayer.SetChildIndex(_recipeContextMenu, _recipeLayer.numChildren - 1);
            _recipeContextMenu.visible = true;
        }

        private void AddRecipeMenuItem(string label, Action action, bool destructive = false)
        {
            int index = 0;
            while (index < _recipeMenuItems.Count && _recipeMenuItems[index].Visual.visible)
                index++;
            RecipeMenuItemSlot slot = GetRecipeMenuItem(index);
            slot.Action = action;
            slot.Index = index;
            slot.Destructive = destructive;
            slot.Label.text = label;
            slot.Visual.SetXY(6f, 6f + index * 36f);
            slot.Visual.SetSize(236f, 32f);
            slot.Label.SetXY(12f, 0f);
            slot.Label.SetSize(212f, 32f);
            DrawRecipeMenuItem(slot, hovered: false);
            slot.Visual.visible = true;
        }

        private void EndRecipeContextMenu()
        {
            int count = 0;
            for (int i = 0; i < _recipeMenuItems.Count && _recipeMenuItems[i].Visual.visible; i++)
                count++;
            float height = 12f + count * 36f;
            _recipeContextMenu.SetSize(248f, height);
            _recipeContextMenuBackground.DrawRect(248f, height, 1,
                new Color32(36, 105, 120, 255), new Color32(8, 20, 28, 252));
            Vector2 point = _recipeLayer.GlobalToLocal(Fgui.Stage.inst.touchPosition);
            _recipeContextMenu.SetXY(
                Mathf.Clamp(point.x, 4f, Math.Max(4f, _recipeLayer.width - 252f)),
                Mathf.Clamp(point.y, 4f, Math.Max(4f, _recipeLayer.height - height - 4f)));
        }

        private RecipeMenuItemSlot GetRecipeMenuItem(int index)
        {
            while (_recipeMenuItems.Count <= index)
            {
                var slot = new RecipeMenuItemSlot();
                slot.Visual = new Fgui.GComponent
                {
                    name = "menuItem" + _recipeMenuItems.Count,
                    touchable = true,
                    opaque = true
                };
                slot.Background = new Fgui.GGraph { name = "background", touchable = false };
                slot.Label = NewRecipeText("label", string.Empty, 14,
                    new Color32(224, 237, 242, 255), Fgui.AlignType.Left, false);
                slot.Visual.AddChild(slot.Background);
                slot.Visual.AddChild(slot.Label);
                RecipeMenuItemSlot captured = slot;
                slot.Visual.onClick.Set(context =>
                {
                    Action callback = captured.Action;
                    HideRecipeContextMenu();
                    callback?.Invoke();
                    context.StopPropagation();
                });
                slot.Visual.onRollOver.Set(() => DrawRecipeMenuItem(captured, hovered: true));
                slot.Visual.onRollOut.Set(() => DrawRecipeMenuItem(captured, hovered: false));
                _recipeMenuItems.Add(slot);
                _recipeContextMenu.AddChild(slot.Visual);
            }
            return _recipeMenuItems[index];
        }

        private static void DrawRecipeMenuItem(RecipeMenuItemSlot slot, bool hovered)
        {
            if (slot == null || slot.Visual == null || slot.Background == null || slot.Label == null)
                return;
            Color32 fill = hovered
                ? slot.Destructive
                    ? new Color32(75, 24, 32, 255)
                    : new Color32(18, 64, 76, 255)
                : slot.Index % 2 == 0
                    ? new Color32(9, 32, 42, 180)
                    : new Color32(7, 26, 35, 180);
            Color32 border = hovered
                ? slot.Destructive
                    ? new Color32(244, 95, 105, 255)
                    : new Color32(65, 185, 195, 255)
                : Color.clear;
            slot.Background.DrawRect(slot.Visual.width, slot.Visual.height, hovered ? 1 : 0, border, fill);
            slot.Label.color = slot.Destructive
                ? new Color32(244, 102, 108, 255)
                : hovered
                    ? new Color32(235, 252, 255, 255)
                    : new Color32(195, 222, 230, 255);
        }

        private void HideRecipeContextMenu()
        {
            if (_recipeContextMenu != null)
                _recipeContextMenu.visible = false;
        }

        private void CancelRecipeContextMenu()
        {
            _commands?.CancelCascadeConfirmation();
            HideRecipeContextMenu();
        }

        private void AddRecipeTimeline(BehaviorRecipeTimelineViewModel anchor)
        {
            if (!_canEdit)
            {
                ShowRecipeFeedback("当前状态不可编辑，未添加时间轴。");
                return;
            }
            if (_recipeSource != null)
            {
                _recipeSource.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.PrimaryAction;
                _recipeSource.Presentation.SelectedRecipeEventStableKey = string.Empty;
            }
            ShowRecipeFeedback("正在添加时间轴…");
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStage,
                anchor?.StageId ?? string.Empty,
                kind: CharacterTestRuntimeAiLabLogicElementKind.Stage);
        }

        private void DuplicateRecipeTimeline(BehaviorRecipeTimelineViewModel timeline)
        {
            if (!_canEdit || timeline == null || _commands == null)
                return;
            _commands.SelectStage(timeline.StageId, CharacterTestRuntimeAiLabStageSelectionModifier.None);
            _commands.DuplicateSelectedStages();
            ShowRecipeFeedback(_commands.LastMessage);
        }

        private void CopyRecipeTimeline(BehaviorRecipeTimelineViewModel timeline)
        {
            if (!_canEdit || timeline == null)
            {
                ShowRecipeFeedback("当前状态不可复制时间轴。");
                return;
            }
            _recipeCopiedTimelineStageId = timeline.StageId ?? string.Empty;
            _recipeCopiedTimelineTitle = string.IsNullOrWhiteSpace(timeline.Title)
                ? "未命名时间轴"
                : timeline.Title.Trim();
            ShowRecipeFeedback("已复制“" + _recipeCopiedTimelineTitle + "”，右键空白处可粘贴。");
        }

        private void PasteRecipeTimeline(BehaviorRecipeTimelineViewModel anchor)
        {
            BehaviorRecipeTimelineViewModel copied = FindRecipeTimeline(
                _recipeCopiedTimelineStageId);
            if (!_canEdit || copied == null)
                return;
            DuplicateRecipeTimeline(copied);
        }

        private void DeleteRecipeTimeline(BehaviorRecipeTimelineViewModel timeline)
        {
            if (!_canEdit || timeline == null || _commands == null)
                return;
            _commands.SelectStage(timeline.StageId, CharacterTestRuntimeAiLabStageSelectionModifier.None);
            _commands.DeleteSelectedStages();
            ShowRecipeFeedback(_commands.LastMessage);
        }

        private void AddRecipeEvent(
            string stageId,
            int frame,
            CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind timingKind)
        {
            if (!_canEdit || string.IsNullOrEmpty(stageId))
            {
                ShowRecipeFeedback("当前状态不可创建事件。");
                return;
            }
            if (_recipeSource != null)
            {
                _recipeSource.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Event;
                _recipeSource.Presentation.SelectedRecipeEventStableKey = string.Empty;
            }
            ShowRecipeFeedback("正在 " + Math.Max(0, frame) + "f 创建"
                + (timingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                    ? "单帧事件"
                    : "持续事件")
                + "…");
            Enqueue(
                timingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                    ? CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddInstantEventAtFrame
                    : CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddDurationEventAtFrame,
                stageId,
                Math.Max(0, frame),
                CharacterTestRuntimeAiLabLogicElementKind.Stage);
        }

        private void AddRecipeCharacterActionEvent(string timelineId, int frame)
        {
            if (!_canEdit || string.IsNullOrEmpty(timelineId))
            {
                ShowRecipeFeedback("当前状态不可创建角色动作事件。");
                return;
            }
            if (_showCharacterActionPicker == null)
            {
                ShowRecipeFeedback("角色动作目录尚未就绪。");
                return;
            }
            Fgui.GObject anchor = _recipeLayer;
            int normalizedFrame = Math.Max(0, frame);
            _showCharacterActionPicker(anchor, actionId =>
            {
                if (!CharacterActionId.TryParse(actionId, out CharacterActionId parsedActionId))
                    return;
                ShowRecipeFeedback("正在 " + normalizedFrame + "f 创建角色动作事件…");
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddCharacterActionEventAtFrame,
                    new CharacterTestRuntimeAiLabBehaviorRecipeCharacterActionEventCommand(
                        timelineId,
                        normalizedFrame,
                        parsedActionId)));
            });
        }

        private void AddRecipePredicateBranch(string stageId)
        {
            if (!_canEdit || string.IsNullOrEmpty(stageId))
            {
                ShowRecipeFeedback("当前状态不可添加事实条件。");
                return;
            }
            if (_recipeSource != null)
            {
                _recipeSource.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Branch;
                _recipeSource.Presentation.SelectedRecipeEventStableKey = string.Empty;
            }
            ShowRecipeFeedback("正在添加事实条件…");
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageDetector,
                stageId,
                kind: CharacterTestRuntimeAiLabLogicElementKind.Stage);
        }

        private void DuplicateRecipeBranch(BehaviorRecipeEventViewModel recipeEvent)
        {
            if (!_canEdit || recipeEvent == null)
                return;
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateBranchEvent,
                recipeEvent.DetectorId,
                kind: CharacterTestRuntimeAiLabLogicElementKind.Detector);
        }

        private void DeleteRecipeEvent(BehaviorRecipeEventViewModel recipeEvent)
        {
            if (!_canEdit || recipeEvent == null || _commands == null)
            {
                ShowRecipeFeedback("当前状态不可删除事件。");
                return;
            }
            string detectorId = recipeEvent.Kind == BehaviorRecipeEventKind.Event
                ? "event." + recipeEvent.EventId
                : recipeEvent.DetectorId;
            // Recipe edits are undoable. Dispatch immediately so the context-menu click
            // cannot be lost behind a second transient confirmation menu.
            ConfirmDeleteRecipeEvent(recipeEvent, detectorId);
        }

        private void ConfirmDeleteRecipeEvent(
            BehaviorRecipeEventViewModel recipeEvent,
            string detectorId)
        {
            if (!_canEdit || string.IsNullOrEmpty(detectorId) || _commands == null)
            {
                ShowRecipeFeedback("当前状态不可删除事件。");
                return;
            }
            if (recipeEvent?.Kind == BehaviorRecipeEventKind.Event)
            {
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DeleteTriggerEvent,
                    new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventCommand(
                        recipeEvent.StageId,
                        recipeEvent.EventId,
                        CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title)));
                return;
            }
            if (!_commands.TryAuthorizeCascadeDeletion(detectorId, out string message))
            {
                ShowRecipeFeedback(message);
                return;
            }
            ShowRecipeFeedback(message);
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RemoveSelectedElement,
                detectorId,
                kind: CharacterTestRuntimeAiLabLogicElementKind.Detector);
        }

        private void SetRecipeEntryTimeline(BehaviorRecipeTimelineViewModel timeline)
        {
            if (!_canEdit || timeline == null)
                return;
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SetEntryStage,
                timeline.StageId,
                kind: CharacterTestRuntimeAiLabLogicElementKind.Stage);
        }

        private void FitRecipeTimeline()
        {
            if (_recipeSource == null)
                return;
            _recipeSource.HorizontalStartFrame = 0;
            _recipeSource.HorizontalEndFrame = Math.Max(60, _recipeProjection.MaximumLocalFrame + 15);
            _recipeSource.ZoomLevel = 1f;
            _recipeSource.FollowCurrent = true;
            ApplyRecipeAutoFollow();
            CommitRecipeViewport();
            RedrawRecipeTimeline();
            ShowRecipeFeedback("已适配可见时间。");
        }

        private void DisableRecipeAutoFollow()
        {
            if (_recipeSource != null)
                _recipeSource.FollowCurrent = false;
        }

        private void CommitRecipeViewport()
        {
            if (_recipeSource == null)
                return;
            _commands?.CommitTimelineViewport(
                _recipeSource.HorizontalStartFrame,
                _recipeSource.HorizontalEndFrame,
                _recipeSource.ZoomLevel,
                _recipeVerticalOffset,
                _recipeSource.FollowCurrent);
        }

        internal void ShowCommandFeedback(string message) => ShowRecipeFeedback(message);

        private void ShowRecipeFeedback(string message)
        {
            _recipeLocalFeedbackText = message ?? string.Empty;
            UpdateRecipeFeedbackGeometry(_recipeLayer?.width ?? 0f);
        }

        private void UpdateRecipeFeedbackGeometry(float width)
        {
            if (_recipeFeedbackBackground == null || _recipeFeedbackText == null)
                return;
            string message = !string.IsNullOrWhiteSpace(_recipeLocalFeedbackText)
                ? _recipeLocalFeedbackText
                : _recipeSource?.CommandFeedbackText ?? string.Empty;
            bool visible = !string.IsNullOrWhiteSpace(message)
                && (_recipeSource?.CompositionEditor == null || !string.IsNullOrWhiteSpace(_recipeLocalFeedbackText));
            _recipeFeedbackBackground.visible = visible;
            _recipeFeedbackText.visible = visible;
            if (!visible)
                return;
            float feedbackWidth = Math.Min(
                460f,
                Math.Max(180f, width - RecipeContentInset - 32f));
            float feedbackX = Math.Max(
                RecipeContentInset + 8f,
                width - feedbackWidth - 14f);
            _recipeFeedbackBackground.SetXY(feedbackX, RecipeHeaderHeight + 5f);
            _recipeFeedbackBackground.DrawRect(
                feedbackWidth,
                RecipeRulerHeight - 10f,
                1,
                new Color32(40, 130, 142, 220),
                new Color32(7, 34, 42, 245));
            _recipeFeedbackText.SetXY(feedbackX + 12f, RecipeHeaderHeight + 5f);
            _recipeFeedbackText.SetSize(feedbackWidth - 24f, RecipeRulerHeight - 10f);
            _recipeFeedbackText.text = "✓  " + message;
            _recipeFeedbackText.color = new Color32(110, 245, 235, 255);
            BindTooltip(_recipeFeedbackText, message);
        }

    }
}
