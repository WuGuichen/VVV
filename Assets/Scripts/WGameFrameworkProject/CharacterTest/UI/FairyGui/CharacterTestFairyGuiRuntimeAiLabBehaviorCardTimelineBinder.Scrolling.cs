using System;
using System.Collections.Generic;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void UpdateScrollBars(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            int stageCount,
            int transitionCount)
        {
            if (_behaviorLogicMode)
            {
                UpdateBehaviorLogicHorizontalScrollBar();
                return;
            }
            const float horizontalTrackX = 20f;
            const float horizontalTrackWidth = 1828f;
            const float horizontalTrackY = 662f;
            const float verticalTrackX = 1874f;
            const float verticalTrackY = 92f;
            const float verticalTrackHeight = 552f;
            const float visibleContentHeight = 552f;

            Fgui.GObject horizontalThumb = _root.GetChild("timelineHorizontalScrollThumb");
            if (horizontalThumb != null)
            {
                int start = Math.Max(0, viewModel.HorizontalStartFrame);
                int end = Math.Max(start + 1, viewModel.HorizontalEndFrame);
                int visibleFrames = end - start;
                int totalFrames = Math.Max(end, Math.Max(1, viewModel.DurationFrames));
                float thumbWidth = Mathf.Max(
                    180f,
                    horizontalTrackWidth * Mathf.Clamp01(visibleFrames / (float)totalFrames));
                float maxStart = Mathf.Max(0f, totalFrames - visibleFrames);
                float position = maxStart > 0f ? Mathf.Clamp01(start / maxStart) : 0f;
                horizontalThumb.SetXY(
                    horizontalTrackX + (horizontalTrackWidth - thumbWidth) * position,
                    horizontalTrackY);
                horizontalThumb.SetSize(thumbWidth, 6f);
            }

            Fgui.GObject verticalThumb = _root.GetChild("timelineVerticalScrollThumb");
            if (verticalThumb == null)
                return;

            float contentHeight = Mathf.Max(visibleContentHeight, _contentExtentHeight);
            float verticalThumbHeight = Mathf.Max(
                88f,
                verticalTrackHeight * Mathf.Clamp01(visibleContentHeight / contentHeight));
            float maxVerticalOffset = Mathf.Max(0f, contentHeight - visibleContentHeight);
            float verticalPosition = maxVerticalOffset > 0f
                ? Mathf.Clamp01(viewModel.VerticalScrollOffset / maxVerticalOffset)
                : 0f;
            verticalThumb.SetXY(
                verticalTrackX,
                verticalTrackY + (verticalTrackHeight - verticalThumbHeight) * verticalPosition);
            verticalThumb.SetSize(6f, verticalThumbHeight);
        }

        private void ConfigureBehaviorLogicScrollHandlers()
        {
            if (_root == null || _contentLayer == null)
                return;
            if (_contentLayer.displayObject != null)
            {
                _contentLayer.displayObject.onMouseWheel.Set(context =>
                {
                    if (!_behaviorLogicMode)
                        return;
                    float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
                    if (Mathf.Approximately(delta, 0f)
                        || _behaviorLogicContentWidth <= _contentLayer.width + 0.5f)
                    {
                        return;
                    }
                    SetBehaviorLogicHorizontalOffset(
                        _behaviorLogicHorizontalOffset
                            + (delta < 0f
                                ? BehaviorLogicHorizontalScrollStep
                                : -BehaviorLogicHorizontalScrollStep));
                    context.StopPropagation();
                });
            }

            Fgui.GObject track = _root.GetChild("timelineHorizontalScrollTrack");
            if (track != null)
            {
                track.onClick.Set(() =>
                {
                    if (!_behaviorLogicMode || _behaviorLogicContentWidth <= _contentLayer.width)
                        return;
                    Fgui.GObject thumb = _root.GetChild("timelineHorizontalScrollThumb");
                    Vector2 pointer = _root.GlobalToLocal(Fgui.Stage.inst.touchPosition);
                    float thumbWidth = thumb != null ? thumb.width : 160f;
                    float travel = Math.Max(1f, track.width - thumbWidth);
                    float normalized = Mathf.Clamp01(
                        (pointer.x - track.x - thumbWidth * 0.5f) / travel);
                    SetBehaviorLogicHorizontalOffset(
                        normalized * Math.Max(0f, _behaviorLogicContentWidth - _contentLayer.width));
                });
            }

            Fgui.GObject scrollThumb = _root.GetChild("timelineHorizontalScrollThumb");
            if (scrollThumb != null)
            {
                scrollThumb.draggable = true;
                scrollThumb.onDragMove.Set(() =>
                {
                    Fgui.GObject scrollTrack = _root.GetChild("timelineHorizontalScrollTrack");
                    if (!_behaviorLogicMode
                        || scrollTrack == null
                        || _behaviorLogicContentWidth <= _contentLayer.width)
                    {
                        return;
                    }
                    float travel = Math.Max(1f, scrollTrack.width - scrollThumb.width);
                    scrollThumb.x = Mathf.Clamp(
                        scrollThumb.x,
                        scrollTrack.x,
                        scrollTrack.x + travel);
                    scrollThumb.y = scrollTrack.y;
                    float normalized = (scrollThumb.x - scrollTrack.x) / travel;
                    SetBehaviorLogicHorizontalOffset(
                        normalized * Math.Max(0f, _behaviorLogicContentWidth - _contentLayer.width));
                });
            }
        }

        private void UpdateBehaviorLogicHorizontalScrollBar()
        {
            if (!_behaviorLogicMode || _root == null || _contentLayer == null)
                return;
            Fgui.GObject track = _root.GetChild("timelineHorizontalScrollTrack");
            Fgui.GObject thumb = _root.GetChild("timelineHorizontalScrollThumb");
            if (track == null || thumb == null)
                return;
            float trackX = CanvasLeft;
            float trackY = Math.Max(0f, _root.height - 12f);
            float trackWidth = Math.Max(1f, _contentLayer.width);
            float contentWidth = Math.Max(trackWidth, _behaviorLogicContentWidth);
            float maximumOffset = Math.Max(0f, contentWidth - trackWidth);
            _behaviorLogicHorizontalOffset = Mathf.Clamp(
                _behaviorLogicHorizontalOffset,
                0f,
                maximumOffset);
            float thumbWidth = maximumOffset > 0f
                ? Math.Max(160f, trackWidth * trackWidth / contentWidth)
                : trackWidth;
            float normalized = maximumOffset > 0f
                ? _behaviorLogicHorizontalOffset / maximumOffset
                : 0f;
            track.SetXY(trackX, trackY);
            track.SetSize(trackWidth, 6f);
            track.touchable = maximumOffset > 0f;
            thumb.SetXY(trackX + (trackWidth - thumbWidth) * normalized, trackY);
            thumb.SetSize(thumbWidth, 6f);
            thumb.touchable = maximumOffset > 0f;
            thumb.draggable = maximumOffset > 0f;
        }

        private void SetBehaviorLogicHorizontalOffset(float offset)
        {
            if (!_behaviorLogicMode || _contentLayer == null)
                return;
            float maximumOffset = Math.Max(
                0f,
                _behaviorLogicContentWidth - _contentLayer.width);
            float clamped = Mathf.Clamp(offset, 0f, maximumOffset);
            if (Mathf.Abs(clamped - _behaviorLogicHorizontalOffset) < 0.5f)
                return;
            _behaviorLogicHorizontalOffset = clamped;
            RebuildBehaviorLogicPresentation();
        }

        private void ScrollBehaviorLogicStageDetectors(
            StageSlot slot,
            Fgui.EventContext context)
        {
            if (!_behaviorLogicMode || slot == null)
                return;
            float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
            if (Mathf.Approximately(delta, 0f))
                return;
            int stageIndex = _stagePool.IndexOf(slot);
            if (stageIndex < 0 || stageIndex >= _activeStageCount)
                return;
            int maximumOffset = Math.Max(
                0,
                _stageDetectorCount[stageIndex] - BehaviorLogicVisibleDetectorRows);
            if (maximumOffset <= 0)
                return;
            int next = Mathf.Clamp(
                slot.DetectorScrollOffset + (delta < 0f ? 1 : -1),
                0,
                maximumOffset);
            if (next == slot.DetectorScrollOffset)
                return;
            slot.DetectorScrollOffset = next;
            RebuildBehaviorLogicPresentation();
            context.StopPropagation();
        }

        private void RebuildBehaviorLogicPresentation()
        {
            if (!_behaviorLogicMode || _root == null || _contentLayer == null)
                return;
            FillStageDetectorCounts(
                _boundDetectors,
                _boundDetectorCount,
                _activeStageCount);
            float canvasWidth = Math.Max(1f, _contentLayer.width);
            LayoutBehaviorLogicStages(_activeStageCount, canvasWidth);
            BindStages(_boundStages, _activeStageCount);
            BindDetectors(
                _boundDetectors,
                _boundDetectorCount,
                _activeStageCount,
                0,
                1f,
                canvasWidth,
                0f);
            BindTransitions(_boundTransitions, _boundTransitionCount);
            PositionBehaviorLogicStructureControls(_activeStageCount);
            RefreshTransitionResultMenuAnchor();
            UpdateBehaviorLogicHorizontalScrollBar();
            _hasGeometry = false;
        }

        private void EnsureStaticHandlers()
        {
            if (_handlersConfigured)
                return;
            _handlersConfigured = true;
            BindToolbar("btnTimelineAddStage", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStage);
            BindToolbar("btnTimelineAddCardDetector", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddCardDetector);
            BindToolbar("btnTimelineCanonicalize", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CanonicalizeDetectorWindows);
            BindToolbar("btnTimelineDelete", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DeleteStages);
            BindToolbar("btnTimelineDuplicate", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateStages);
            BindToolbar("btnTimelineSnap", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ToggleStageSnap);
            BindToolbar("btnTimelineUndo", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.UndoStageEdit);
            BindToolbar("btnTimelineRedo", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RedoStageEdit);
            BindToolbar("btnTimelineZoomOut", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineOut);
            BindToolbar("btnTimelineZoomIn", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineIn);
            BindToolbar("btnTimelineFit", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.FitTimeline);
            BindToolbar("btnTimelinePanLeft", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineLeft);
            BindToolbar("btnTimelinePanRight", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineRight);
            BindToolbar("btnTimelineScrollUp", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineUp);
            BindToolbar("btnTimelineScrollDown", CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineDown);
            _root.focusable = true;
            _root.onKeyDown.Set(HandleTimelineKeyDown);
        }

        private void BindToolbar(string name, string commandId)
        {
            Fgui.GButton button = _root.GetChild(name)?.asButton;
            if (button == null)
                return;
            button.onClick.Set(() =>
            {
                if (commandId == CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddCardDetector
                    && _behaviorLogicMode)
                {
                    TryAddDetectorToSelectedStage();
                    return;
                }
                string stableId;
                if (commandId == CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStage)
                {
                    stableId = _behaviorLogicMode
                        || _selectedKind != CharacterTestRuntimeAiLabLogicElementKind.Stage
                            ? string.Empty
                            : _selectedStableId;
                }
                else
                {
                    stableId = _selectedStableId;
                }
                Enqueue(commandId, stableId, 0, _selectedKind);
            });
        }

        private void HideAll()
        {
            CancelStageMarquee();
            HideStageSlots(0);
            HideDetectorSlots(0);
            HideTransitionSlots(0);
            HideConnectors(0);
            HideTransitionFlowSlots(0);
            if (_stageEditorPopover != null)
                _stageEditorPopover.visible = false;
            HideStageTypedOptions();
            HideTransitionResultMenu();
            SetObjectVisible("timelineMarquee", false);
            SetObjectVisible("timelineEdgeScrollLeft", false);
            SetObjectVisible("timelineEdgeScrollRight", false);
        }

    }
}
