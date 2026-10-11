using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void ConfigureRecipeInputHandlers()
        {
            _recipeViewport.displayObject.onMouseWheel.Set(context =>
            {
                float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
                if (Mathf.Approximately(delta, 0f))
                    return;
                if (context.inputEvent.ctrl || context.inputEvent.command)
                    ZoomRecipeAroundPointer(delta, context.inputEvent.x);
                else if (context.inputEvent.shift)
                    PanRecipeHorizontally(delta);
                else
                    ScrollRecipeVertically(delta < 0f ? RecipeVerticalWheelStep : -RecipeVerticalWheelStep);
                context.StopPropagation();
            });
            _recipeViewport.onRightClick.Set(context =>
            {
                Vector2 point = _recipeViewport.GlobalToLocal(Fgui.Stage.inst.touchPosition);
                BehaviorRecipeTimelineViewModel timeline = FindRecipeTimelineAtViewportY(point.y);
                int frame = ResolveRecipePointerFrame(_recipeViewport, point.x - RecipeContentInset);
                if (timeline != null)
                    ShowRecipeFrameContextMenu(timeline.StageId, frame);
                else
                    ShowRecipeBackgroundContextMenu(timeline);
                context.StopPropagation();
            });
            _recipeViewport.onClick.Set(context =>
            {
                Vector2 point = _recipeViewport.GlobalToLocal(Fgui.Stage.inst.touchPosition);
                BehaviorRecipeTimelineViewModel timeline = FindRecipeTimelineAtViewportY(point.y);
                if (timeline == null && _recipeSource?.TacticalPreview?.FocusBlockId != null)
                    timeline = FindRecipeTimeline(_recipeSource.TacticalPreview.Editor?.Composition?.FocusTimelineId);
                if (timeline != null)
                    SelectRecipeTimelineConditions(timeline);
                else
                    CancelRecipeContextMenu();
                context.StopPropagation();
            });
            _recipeLayer.onClick.Set(CancelRecipeContextMenu);
            _recipeHorizontalScrollThumb.draggable = true;
            _recipeHorizontalScrollThumb.onDragMove.Set(UpdateRecipeHorizontalThumbDrag);
            _recipeVerticalScrollThumb.draggable = true;
            _recipeVerticalScrollThumb.onDragMove.Set(UpdateRecipeVerticalThumbDrag);
        }

        private void BeginRecipeGesture(
            RecipeTrackSlot slot,
            bool resize,
            Fgui.EventContext context)
        {
            context?.StopPropagation();
            BehaviorRecipeEventViewModel recipeEvent = slot?.Event;
            if (!_canEdit || recipeEvent == null || context?.inputEvent == null)
                return;
            RecipeGestureKind kind = RecipeGestureKind.None;
            if (resize && recipeEvent.CanResize)
                kind = recipeEvent.Kind == BehaviorRecipeEventKind.PrimaryAction
                    ? RecipeGestureKind.ResizeWait
                    : RecipeGestureKind.ResizeBranch;
            else if (!resize
                && recipeEvent.Kind is BehaviorRecipeEventKind.Event or BehaviorRecipeEventKind.Branch
                && recipeEvent.CanMove)
                kind = RecipeGestureKind.MoveBranch;
            if (kind == RecipeGestureKind.None)
                return;
            if (_recipeSource?.TacticalPreview?.Editor != null && context.inputEvent.ctrlOrCmd) return;
            SelectRecipeEvent(recipeEvent, preserveSelection: true);
            BeginTacticalGesture(recipeEvent, resize, context);
            if (_recipeSource?.TacticalPreview != null && _tacticalGesture == null) return;
            _recipeGestureSlot = slot;
            _recipeGestureKind = kind;
            _recipeGestureStartX = ResolveRecipeGesturePointerLocalX(slot.Content);
            _recipeGestureBaseX = recipeEvent.Primitive == BehaviorRecipeEventPrimitive.Marker
                ? slot.Marker.x
                : slot.Span.x;
            _recipeGestureGrabOffsetX = _recipeGestureStartX - _recipeGestureBaseX;
            _recipeGestureBaseWidth = slot.Span.width;
            _recipeGestureBaseMarkerCardX = slot.MarkerCardBackground.x;
            _recipeGestureBaseMarkerLabelX = slot.MarkerLabel.x;
            _recipeGestureBaseResizeHandleX = slot.ResizeHandle.x;
            _recipeGestureBaseSpanHitAreaX = slot.SpanHitArea.x;
            _recipeGestureBaseSpanHitAreaWidth = slot.SpanHitArea.width;
            _recipeGestureBaseStartFrame = Math.Max(0, recipeEvent.StartFrame);
            _recipeGestureBaseDurationFrames = Math.Max(1, recipeEvent.DurationFrames);
            _recipeGesturePreviewStartFrame = _recipeGestureBaseStartFrame;
            _recipeGesturePreviewDurationFrames = _recipeGestureBaseDurationFrames;
            _recipeGestureMoved = false;
            context.CaptureTouch();
        }

        private void UpdateRecipeGesture(Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (_recipeGestureSlot == null || context?.inputEvent == null)
                return;
            float deltaX = ResolveRecipeGesturePointerLocalX(_recipeGestureSlot.Content)
                - _recipeGestureStartX;
            _recipeGestureMoved |= Math.Abs(deltaX) >= 3f;
            if (!_recipeGestureMoved)
                return;
            int viewportStartFrame = Math.Max(0, _recipeSource?.HorizontalStartFrame ?? 0);
            int viewportEndFrame = Math.Max(
                viewportStartFrame + 1,
                _recipeSource?.HorizontalEndFrame ?? viewportStartFrame + 1);
            var viewport = new BehaviorTimelineViewport(
                viewportStartFrame,
                viewportEndFrame,
                _recipeSource?.ZoomLevel ?? 1f,
                _recipeVerticalOffset,
                false,
                RecipeFrameSnap);
            var transform = new BehaviorTimelineTransform(
                viewport,
                0f,
                Math.Max(1f, _recipeGestureSlot.Content.width));
            if (_tacticalGesture?.StartResize == true)
            {
                _recipeGesturePreviewStartFrame = Mathf.Clamp(ResolveRecipeGestureFrame(ResolveRecipeGesturePointerLocalX(_recipeGestureSlot.Content), transform),
                    0, _recipeGestureBaseStartFrame + _recipeGestureBaseDurationFrames - 1);
                UpdateTacticalGesture(); return;
            }
            if (_recipeGestureKind == RecipeGestureKind.MoveBranch)
            {
                bool marker = _recipeGestureSlot.Event.Primitive
                    == BehaviorRecipeEventPrimitive.Marker;
                float eventWidth = marker
                    ? Math.Max(18f, _recipeGestureSlot.Marker.width)
                    : _recipeGestureSlot.Span.width;
                int maximumStartFrame = Math.Max(
                    viewportStartFrame,
                    viewportEndFrame - _recipeGestureBaseDurationFrames);
                float candidateEventX = ResolveRecipeGesturePointerLocalX(_recipeGestureSlot.Content)
                    - _recipeGestureGrabOffsetX;
                _recipeGesturePreviewStartFrame = Mathf.Clamp(
                    ResolveRecipeGestureFrame(candidateEventX, transform),
                    viewportStartFrame,
                    maximumStartFrame);
                float x = ClampRecipeHorizontalEventX(
                    transform.FrameToX(_recipeGesturePreviewStartFrame),
                    _recipeGestureSlot.Content.width,
                    eventWidth);
                if (marker)
                {
                    _recipeGestureSlot.Marker.x = x;
                    float cardWidth = _recipeGestureSlot.MarkerCardBackground.width;
                    float cardX = x + 8f + cardWidth <= _recipeGestureSlot.Content.width
                        ? x + 8f
                        : Math.Max(0f, x - cardWidth - 8f);
                    _recipeGestureSlot.MarkerCardBackground.x = cardX;
                    _recipeGestureSlot.MarkerLabel.x = cardX + 10f;
                }
                else
                {
                    _recipeGestureSlot.Span.x = x;
                    PositionRecipeSpanHitArea(
                        _recipeGestureSlot,
                        x,
                        _recipeGestureSlot.Span.width);
                    _recipeGestureSlot.ResizeHandle.x = x
                        + _recipeGestureSlot.Span.width - RecipeResizeHandleWidth;
                }
            }
            else
            {
                int minimumEndFrame = _recipeGestureBaseStartFrame + 1;
                int maximumEndFrame = Math.Max(minimumEndFrame, viewportEndFrame);
                float pointerLocalX = ResolveRecipeGesturePointerLocalX(_recipeGestureSlot.Content);
                int endFrame = Mathf.Clamp(
                    ResolveRecipeGestureFrame(pointerLocalX, transform),
                    minimumEndFrame,
                    maximumEndFrame);
                _recipeGesturePreviewDurationFrames = Math.Max(
                    1,
                    endFrame - _recipeGestureBaseStartFrame);
                float previewStartX = transform.FrameToX(_recipeGestureBaseStartFrame);
                float previewEndX = transform.FrameToX(endFrame);
                _recipeGestureSlot.Span.SetSize(
                    Math.Max(1f, previewEndX - previewStartX),
                    RecipeEventHeight);
                PositionRecipeSpanHitArea(
                    _recipeGestureSlot,
                    _recipeGestureSlot.Span.x,
                    _recipeGestureSlot.Span.width);
                _recipeGestureSlot.ResizeHandle.x = ResolveRecipeResizeHandleX(
                    _recipeGestureSlot.Span.x,
                    _recipeGestureSlot.Span.width,
                    _recipeGestureSlot.Content.width,
                    RecipeResizeHandleWidth);
            }
            bool isMarker = _recipeGestureSlot?.Event?.Primitive == BehaviorRecipeEventPrimitive.Marker;
            int guideStartFrame = _recipeGestureKind == RecipeGestureKind.MoveBranch
                ? _recipeGesturePreviewStartFrame
                : _recipeGestureBaseStartFrame;
            int guideEndFrame = _recipeGestureKind == RecipeGestureKind.MoveBranch
                ? _recipeGesturePreviewStartFrame + _recipeGestureBaseDurationFrames
                : _recipeGestureBaseStartFrame + _recipeGesturePreviewDurationFrames;
            DrawRecipeDragGuideLines(transform, guideStartFrame, guideEndFrame, isMarker);
            UpdateTacticalGesture();
        }

        private static float ResolveRecipeGesturePointerLocalX(Fgui.GComponent content) =>
            content?.GlobalToLocal(Fgui.Stage.inst.touchPosition).x ?? 0f;

        internal static int ResolveRecipeGestureFrame(
            float pointerLocalX,
            in BehaviorTimelineTransform transform) =>
            transform.SnapRelativeFrame(transform.XToFrame(pointerLocalX));

        internal static int ResolveRecipeGestureDeltaFrames(
            float localDeltaX,
            float trackWidth,
            int viewportStartFrame,
            int viewportEndFrame,
            int snapFrames)
        {
            int frameSpan = Math.Max(1, viewportEndFrame - viewportStartFrame);
            float pixelsPerFrame = Math.Max(0.001f, trackWidth) / frameSpan;
            int snap = Math.Max(1, snapFrames);
            return (int)Math.Round(
                localDeltaX / pixelsPerFrame / snap,
                MidpointRounding.AwayFromZero) * snap;
        }

        internal static float ResolveRecipeResizeHandleX(
            float eventX,
            float eventWidth,
            float trackWidth,
            float handleWidth)
        {
            float safeHandleWidth = Math.Max(1f, handleWidth);
            return Mathf.Clamp(
                eventX + Math.Max(1f, eventWidth) - safeHandleWidth * 0.5f,
                0f,
                Math.Max(0f, trackWidth - safeHandleWidth));
        }

        private static void PositionRecipeSpanHitArea(
            RecipeTrackSlot slot,
            float eventX,
            float temporalWidth)
        {
            if (slot?.SpanHitArea == null)
                return;
            float hitWidth = Math.Max(RecipeResizeHandleWidth, temporalWidth);
            float hitX = eventX - Math.Max(0f, (hitWidth - temporalWidth) * 0.5f);
            float maximumX = Math.Max(0f, (slot.Content?.width ?? hitWidth) - hitWidth);
            hitX = Mathf.Clamp(hitX, 0f, maximumX);
            slot.SpanHitArea.SetXY(hitX, RecipeEventY);
            slot.SpanHitArea.SetSize(hitWidth, RecipeEventHeight);
            // Keep a real FairyGUI hit shape while the visible Event body remains
            // exactly as wide as its frame interval.
            slot.SpanHitArea.DrawRect(
                hitWidth,
                RecipeEventHeight,
                0,
                Color.clear,
                new Color32(0, 0, 0, 1));
        }

        internal static float ClampRecipeHorizontalEventX(
            float candidateX,
            float trackWidth,
            float eventWidth)
        {
            return Mathf.Clamp(
                candidateX,
                0f,
                Math.Max(0f, trackWidth - eventWidth));
        }

        internal static float ClampRecipeEventWidth(
            float candidateWidth,
            float trackWidth,
            float eventX,
            float minimumWidth)
        {
            float maximumWidth = Math.Max(minimumWidth, trackWidth - eventX);
            return Mathf.Clamp(candidateWidth, minimumWidth, maximumWidth);
        }

        private void EndRecipeGesture(Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (_recipeGestureSlot == null)
                return;
            if (_tacticalGesture != null) { EndTacticalGesture(); return; }
            RecipeTrackSlot slot = _recipeGestureSlot;
            RecipeGestureKind kind = _recipeGestureKind;
            bool moved = _recipeGestureMoved;
            int previewStartFrame = _recipeGesturePreviewStartFrame;
            int previewDurationFrames = _recipeGesturePreviewDurationFrames;
            CancelRecipeGesture();
            if (!moved || slot.Event == null)
                return;
            if (kind == RecipeGestureKind.MoveBranch)
            {
                CommitRecipeTriggerEventFrame(
                    slot.Event,
                    slot.Event.Kind == BehaviorRecipeEventKind.Event
                        ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationStartFrame
                        : slot.Event.Primitive == BehaviorRecipeEventPrimitive.Marker
                        ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrame
                        : CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationStartFrame,
                    previewStartFrame);
            }
            else if (kind == RecipeGestureKind.ResizeBranch)
            {
                CommitRecipeTriggerEventFrame(
                    slot.Event,
                    slot.Event.Kind == BehaviorRecipeEventKind.Event
                        ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.DurationFrames
                        : CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationDurationFrames,
                    previewDurationFrames);
            }
            else if (kind == RecipeGestureKind.ResizeWait)
            {
                CommitRecipeWaitFrames(slot.Event.StageId, previewDurationFrames);
            }
        }

        private void CommitRecipeTriggerEventFrame(
            BehaviorRecipeEventViewModel recipeEvent,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
            int value)
        {
            _commands?.ApplyRecipeTriggerEventField(
                recipeEvent?.StageId,
                recipeEvent?.EventId,
                field,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: value));
        }

        private void CommitRecipeWaitFrames(string stageId, int frames)
        {
            if (_commands?.BeginStageFieldEdit(
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.WaitFrames,
                    out long epoch) == true)
            {
                _commands.CommitStageFieldEdit(
                    epoch,
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.WaitFrames,
                    new CharacterTestRuntimeAiLabStageFieldValue(intValue: frames));
            }
        }

        private void CancelRecipeGesture()
            => ClearRecipeGesture(restorePreview: true);

        private void ClearRecipeGesture(bool restorePreview)
        {
            bool tactical = _tacticalGesture != null;
            RestoreTacticalGesture(restorePreview);
            HideRecipeDragGuideLines();
            if (restorePreview && _recipeGestureSlot != null)
            {
                _recipeGestureSlot.Span.x = _recipeGestureBaseX;
                _recipeGestureSlot.Span.SetSize(_recipeGestureBaseWidth, RecipeEventHeight);
                _recipeGestureSlot.Marker.x = _recipeGestureBaseX;
                _recipeGestureSlot.MarkerCardBackground.x = _recipeGestureBaseMarkerCardX;
                _recipeGestureSlot.MarkerLabel.x = _recipeGestureBaseMarkerLabelX;
                _recipeGestureSlot.ResizeHandle.x = _recipeGestureBaseResizeHandleX;
                _recipeGestureSlot.SpanHitArea.x = _recipeGestureBaseSpanHitAreaX;
                _recipeGestureSlot.SpanHitArea.SetSize(
                    _recipeGestureBaseSpanHitAreaWidth,
                    RecipeEventHeight);
            }
            _recipeGestureSlot = null;
            _recipeGestureKind = RecipeGestureKind.None;
            _recipeGestureGrabOffsetX = 0f;
            _recipeGestureBaseStartFrame = 0;
            _recipeGestureBaseDurationFrames = 1;
            _recipeGesturePreviewStartFrame = 0;
            _recipeGesturePreviewDurationFrames = 1;
            _recipeGestureMoved = false;
            if (restorePreview && tactical) RedrawRecipeTimeline();
        }

        private void DrawRecipeDragGuideLines(
            in BehaviorTimelineTransform transform,
            int startFrame,
            int endFrame,
            bool isMarker)
        {
            if (_recipeDragStartGuideLine == null || _recipeDragEndGuideLine == null || _recipeLayer == null)
                return;

            float contentWidth = Math.Max(1f, _recipeLayer.width - RecipeContentInset - 20f);
            float viewportHeight = _recipeViewport?.height ?? 300f;

            // Start Frame Line (Bright amber / gold)
            float startLocalX = transform.FrameToX(startFrame);
            bool startVisible = startLocalX >= 0f && startLocalX <= contentWidth;
            _recipeDragStartGuideLine.visible = startVisible;
            if (_recipeDragStartGuideLabel != null)
                _recipeDragStartGuideLabel.visible = startVisible;

            if (startVisible)
            {
                float lineX = RecipeContentInset + startLocalX;
                _recipeDragStartGuideLine.SetXY(lineX, 0f);
                _recipeDragStartGuideLine.DrawRect(1.5f, viewportHeight, 0, Color.clear,
                    new Color32(255, 215, 75, 235));

                if (_recipeDragStartGuideLabel != null)
                {
                    float labelX = Mathf.Clamp(lineX - 24f, RecipeContentInset, _recipeLayer.width - 52f);
                    _recipeDragStartGuideLabel.SetXY(labelX, RecipeHeaderHeight + 2f);
                    _recipeDragStartGuideLabel.SetSize(48f, RecipeRulerHeight - 10f);
                    _recipeDragStartGuideLabel.text = startFrame.ToString(CultureInfo.InvariantCulture) + "f";
                }
            }

            // End Frame Line (Cyan / Teal, for span events)
            bool showEndLine = !isMarker && endFrame > startFrame;
            float endLocalX = transform.FrameToX(endFrame);
            bool endVisible = showEndLine && endLocalX >= 0f && endLocalX <= contentWidth;
            _recipeDragEndGuideLine.visible = endVisible;
            if (_recipeDragEndGuideLabel != null)
                _recipeDragEndGuideLabel.visible = endVisible;

            if (endVisible)
            {
                float lineX = RecipeContentInset + endLocalX;
                _recipeDragEndGuideLine.SetXY(lineX, 0f);
                _recipeDragEndGuideLine.DrawRect(1.5f, viewportHeight, 0, Color.clear,
                    new Color32(80, 235, 225, 235));

                if (_recipeDragEndGuideLabel != null)
                {
                    float labelX = Mathf.Clamp(lineX - 24f, RecipeContentInset, _recipeLayer.width - 52f);
                    _recipeDragEndGuideLabel.SetXY(labelX, RecipeHeaderHeight + 2f);
                    _recipeDragEndGuideLabel.SetSize(48f, RecipeRulerHeight - 10f);
                    _recipeDragEndGuideLabel.text = endFrame.ToString(CultureInfo.InvariantCulture) + "f";
                }
            }
        }

        private void HideRecipeDragGuideLines()
        {
            if (_recipeDragStartGuideLine != null)
                _recipeDragStartGuideLine.visible = false;
            if (_recipeDragStartGuideLabel != null)
                _recipeDragStartGuideLabel.visible = false;
            if (_recipeDragEndGuideLine != null)
                _recipeDragEndGuideLine.visible = false;
            if (_recipeDragEndGuideLabel != null)
                _recipeDragEndGuideLabel.visible = false;
        }

        private void SelectRecipeEvent(BehaviorRecipeEventViewModel recipeEvent, bool toggle = false, bool preserveSelection = false)
        {
            if (recipeEvent == null || _recipeSource == null)
                return;
            if (!_recipeSource.InspectCompiledExecution && _recipeSource.TacticalPreview?.WholeCard is CharacterTestTacticalWholeCardTimeline whole)
            {
                whole.Select(recipeEvent.EventId);
                return;
            }
            var editor = _recipeSource.InspectCompiledExecution ? null : _recipeSource.TacticalPreview?.Editor;
            if (editor != null)
            {
                if (toggle && editor.Selection.Remove(recipeEvent.EventId))
                {
                    if (editor.Selection.Count == 0) _commands?.SelectRecipeInspector(BehaviorRecipeInspectorTargetKind.Recipe);
                    else foreach (var selected in editor.Selection) { _commands?.SelectRecipeInspector(BehaviorRecipeInspectorTargetKind.Event, recipeEvent.StageId, selected); break; }
                    RedrawRecipeTimeline(); return;
                }
                if (!toggle && (!preserveSelection || !editor.Selection.Contains(recipeEvent.EventId))) editor.Selection.Clear();
                editor.Selection.Add(recipeEvent.EventId);
            }
            _recipeSource.Presentation.SelectedRecipeEventKind = recipeEvent.Kind;
            _recipeSource.Presentation.SelectedRecipeEventStableKey = recipeEvent.StableKey;
            if (recipeEvent.Kind == BehaviorRecipeEventKind.Event
                && !string.IsNullOrEmpty(recipeEvent.EventId))
            {
                _commands?.SelectRecipeInspector(
                    BehaviorRecipeInspectorTargetKind.Event,
                    recipeEvent.StageId,
                    recipeEvent.EventId);
                RedrawRecipeTimeline();
                return;
            }
            if (recipeEvent.SelectionKind == CharacterTestRuntimeAiLabLogicElementKind.Stage)
            {
                _commands?.SelectStage(
                    recipeEvent.StageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None);
            }
            else if (recipeEvent.SelectionKind == CharacterTestRuntimeAiLabLogicElementKind.Detector)
            {
                Enqueue(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
                    recipeEvent.DetectorId,
                    kind: CharacterTestRuntimeAiLabLogicElementKind.Detector);
            }
        }

        private void SelectRecipeStage(
            BehaviorRecipeTimelineViewModel timeline,
            BehaviorRecipeEventKind eventKind)
        {
            if (timeline == null || _recipeSource == null)
                return;
            _recipeSource.Presentation.SelectedRecipeEventKind = eventKind;
            _recipeSource.Presentation.SelectedRecipeEventStableKey =
                "event:" + timeline.StageId + (eventKind == BehaviorRecipeEventKind.StageEntry ? ":entry" : ":primary");
            _commands?.SelectStage(timeline.StageId, CharacterTestRuntimeAiLabStageSelectionModifier.None);
        }

        private void SelectRecipeActivationConditions()
        {
            if (_recipeSource == null)
                return;
            _recipeSource.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
            _recipeSource.Presentation.SelectedRecipeEventStableKey = "condition-owner:activation";
            _commands?.SelectRecipeInspector(BehaviorRecipeInspectorTargetKind.Recipe);
            RedrawRecipeTimeline();
        }

        private void SelectRecipeTimelineConditions(BehaviorRecipeTimelineViewModel timeline)
        {
            if (timeline == null || _recipeSource == null)
                return;
            _recipeSource.TacticalPreview?.Editor?.Selection.Clear();
            _recipeSource.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
            _recipeSource.Presentation.SelectedRecipeEventStableKey =
                "condition-owner:timeline:" + timeline.StageId;
            _commands?.SelectRecipeInspector(
                BehaviorRecipeInspectorTargetKind.Timeline,
                timeline.StageId);
            RedrawRecipeTimeline();
        }

        private bool ScrollToRecipeTarget(BehaviorRecipeEventViewModel recipeEvent)
        {
            if (recipeEvent == null
                || recipeEvent.EffectiveTransitionIndex < 0
                || recipeEvent.EffectiveTransitionIndex >= _boundTransitionCount)
                return false;
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                _boundTransitions[recipeEvent.EffectiveTransitionIndex];
            int targetIndex = transition?.TargetStageIndex ?? -1;
            if (targetIndex < 0)
                return false;
            float offset = 0f;
            for (int i = 0; i < _recipeProjection.TimelineCount; i++)
            {
                BehaviorRecipeTimelineViewModel timeline = _recipeProjection.GetTimeline(i);
                if (timeline.StageIndex == targetIndex)
                {
                    _recipeVerticalOffset = Mathf.Max(0f, offset);
                    _recipeSource.VerticalScrollOffset = _recipeVerticalOffset;
                    SelectRecipeStage(timeline, BehaviorRecipeEventKind.PrimaryAction);
                    RedrawRecipeTimeline();
                    return true;
                }
                offset += RecipeTimelineVisualHeight(timeline);
            }
            return false;
        }

        internal bool TryLocateRecipeTimeline(string timelineId)
        {
            if (string.IsNullOrEmpty(timelineId) || _recipeSource == null || _recipeProjection.TimelineCount == 0)
                return false;
            float offset = 0f;
            for (int i = 0; i < _recipeProjection.TimelineCount; i++)
            {
                BehaviorRecipeTimelineViewModel timeline = _recipeProjection.GetTimeline(i);
                if (string.Equals(timeline.StageId, timelineId, StringComparison.Ordinal))
                {
                    float maximum = Math.Max(0f, _recipeContentExtent - _recipeViewport.height);
                    _recipeVerticalOffset = Mathf.Clamp(offset, 0f, maximum);
                    _recipeSource.VerticalScrollOffset = _recipeVerticalOffset;
                    RedrawRecipeTimeline();
                    return true;
                }
                offset += RecipeTimelineVisualHeight(timeline);
            }
            return false;
        }

        internal bool TryClearRecipeEventSelection()
        {
            if (_recipeSource == null)
                return false;
            string timelineId = _recipeSource.Presentation.SelectedRecipeTimelineId;
            if (string.IsNullOrEmpty(timelineId))
            {
                _commands?.SelectRecipeInspector(BehaviorRecipeInspectorTargetKind.Recipe);
                RedrawRecipeTimeline();
                return true;
            }
            _recipeSource.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.None;
            _recipeSource.Presentation.SelectedRecipeEventStableKey =
                "condition-owner:timeline:" + timelineId;
            _commands?.SelectRecipeInspector(
                BehaviorRecipeInspectorTargetKind.Timeline,
                timelineId);
            RedrawRecipeTimeline();
            return true;
        }

        private void ScrollRecipeVertically(float delta)
        {
            DisableRecipeAutoFollow();
            float maximum = Math.Max(0f, _recipeContentExtent - _recipeViewport.height);
            _recipeVerticalOffset = Mathf.Clamp(_recipeVerticalOffset + delta, 0f, maximum);
            if (_recipeSource != null)
                _recipeSource.VerticalScrollOffset = _recipeVerticalOffset;
            CommitRecipeViewport();
            RedrawRecipeTimeline();
        }

        private void PanRecipeHorizontally(float wheelDelta)
        {
            if (_recipeSource == null)
                return;
            DisableRecipeAutoFollow();
            int span = Math.Max(1, _recipeSource.HorizontalEndFrame - _recipeSource.HorizontalStartFrame);
            int delta = Math.Max(1, Mathf.RoundToInt(span * 0.12f)) * (wheelDelta < 0f ? 1 : -1);
            int total = Math.Max(span, _recipeProjection.MaximumLocalFrame + 30);
            int start = Mathf.Clamp(_recipeSource.HorizontalStartFrame + delta, 0, Math.Max(0, total - span));
            _recipeSource.HorizontalStartFrame = start;
            _recipeSource.HorizontalEndFrame = start + span;
            CommitRecipeViewport();
            RedrawRecipeTimeline();
        }

        private void ZoomRecipeAroundPointer(float wheelDelta, float globalPointerX)
        {
            if (_recipeSource == null)
                return;
            DisableRecipeAutoFollow();
            int oldStart = Math.Max(0, _recipeSource.HorizontalStartFrame);
            int oldEnd = Math.Max(oldStart + 1, _recipeSource.HorizontalEndFrame);
            int oldSpan = oldEnd - oldStart;
            int newSpan = Mathf.Clamp(
                Mathf.RoundToInt(oldSpan * (wheelDelta < 0f ? 0.8f : 1.25f)),
                15,
                Math.Max(60, _recipeProjection.MaximumLocalFrame + 60));
            Vector2 pointer = _recipeLayer.GlobalToLocal(new Vector2(globalPointerX, Fgui.Stage.inst.touchPosition.y));
            float fraction = Mathf.Clamp01(
                (pointer.x - RecipeContentInset)
                / Math.Max(1f, _recipeLayer.width - RecipeContentInset - 20f));
            int anchor = oldStart + Mathf.RoundToInt(oldSpan * fraction);
            int newStart = Math.Max(0, anchor - Mathf.RoundToInt(newSpan * fraction));
            _recipeSource.HorizontalStartFrame = newStart;
            _recipeSource.HorizontalEndFrame = newStart + newSpan;
            _recipeSource.ZoomLevel = oldSpan / (float)newSpan;
            CommitRecipeViewport();
            RedrawRecipeTimeline();
        }

        private void UpdateRecipeHorizontalThumbDrag()
        {
            if (_recipeSource == null)
                return;
            DisableRecipeAutoFollow();
            float trackX = RecipeContentInset;
            float trackWidth = Math.Max(1f, _recipeLayer.width - RecipeContentInset - 18f);
            float travel = Math.Max(1f, trackWidth - _recipeHorizontalScrollThumb.width);
            _recipeHorizontalScrollThumb.x = Mathf.Clamp(
                _recipeHorizontalScrollThumb.x,
                trackX,
                trackX + travel);
            int span = Math.Max(1, _recipeSource.HorizontalEndFrame - _recipeSource.HorizontalStartFrame);
            int total = Math.Max(span, _recipeProjection.MaximumLocalFrame + 30);
            int maximumStart = Math.Max(0, total - span);
            int start = Mathf.RoundToInt(maximumStart * ((_recipeHorizontalScrollThumb.x - trackX) / travel));
            _recipeSource.HorizontalStartFrame = start;
            _recipeSource.HorizontalEndFrame = start + span;
            CommitRecipeViewport();
            RedrawRecipeTimeline();
        }

        private void UpdateRecipeVerticalThumbDrag()
        {
            DisableRecipeAutoFollow();
            float trackY = RecipeHeaderHeight + RecipeRulerHeight;
            float trackHeight = _recipeViewport.height;
            float travel = Math.Max(1f, trackHeight - _recipeVerticalScrollThumb.height);
            _recipeVerticalScrollThumb.y = Mathf.Clamp(
                _recipeVerticalScrollThumb.y,
                trackY,
                trackY + travel);
            float maximum = Math.Max(0f, _recipeContentExtent - _recipeViewport.height);
            _recipeVerticalOffset = maximum * ((_recipeVerticalScrollThumb.y - trackY) / travel);
            if (_recipeSource != null)
                _recipeSource.VerticalScrollOffset = _recipeVerticalOffset;
            CommitRecipeViewport();
            RedrawRecipeTimeline();
        }

    }
}
