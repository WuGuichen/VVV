using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void RestoreTimelineTransientVisuals()
        {
            for (int i = 0; i < _stagePool.Count && i < _stageGestureBaseX.Length; i++)
            {
                _stagePool[i].Visual.x = _stageGestureBaseX[i];
                if (_stageGestureBaseWidth[i] > 0f)
                    _stagePool[i].Visual.SetSize(_stageGestureBaseWidth[i], _stagePool[i].Visual.height);
                SetStageConflictVisible(_stagePool[i], _stageGestureBaseConflict[i]);
            }
            for (int i = 0; i < _detectorPool.Count && i < _detectorGestureBaseX.Length; i++)
                _detectorPool[i].Visual.x = _detectorGestureBaseX[i];
            for (int i = 0; i < _transitionPool.Count && i < _transitionGestureBaseX.Length; i++)
                _transitionPool[i].Visual.x = _transitionGestureBaseX[i];
            for (int i = 0; i < _connectorPool.Count && i < _connectorGestureBaseX.Length; i++)
                _connectorPool[i].x = _connectorGestureBaseX[i];
        }

        private void ResetTimelineTransientViewport()
        {
            _stageTransientViewportBeginFrame = 0;
            _stageTransientViewportSpan = 0;
            _stageTransientViewportFrameOffset = 0;
            SetObjectVisible("timelineEdgeScrollLeft", false);
            SetObjectVisible("timelineEdgeScrollRight", false);
        }

        /// <remarks>LowFreqAlloc once during Binder construction; pointer preview is NoAlloc.</remarks>
        private void InitializeStageGesturePreviewTextCache()
        {
            for (int delta = -StageGestureTooltipDeltaLimit;
                 delta <= StageGestureTooltipDeltaLimit;
                 delta++)
            {
                int index = delta + StageGestureTooltipDeltaLimit;
                string prefix = delta >= 0 ? "+" : string.Empty;
                _stageGesturePreviewTextCache[index] = prefix
                    + delta.ToString(CultureInfo.InvariantCulture)
                    + "f / "
                    + prefix
                    + (delta / 60f).ToString("0.00", CultureInfo.InvariantCulture)
                    + "s";
                _stageGestureConflictPreviewTextCache[index] = _stageGesturePreviewTextCache[index]
                    + " · 动作冲突";
            }
            _stageGestureNegativeOverflowText = "≤ -600f / -10.00s";
            _stageGesturePositiveOverflowText = "≥ +600f / +10.00s";
            _stageGestureNegativeConflictOverflowText = _stageGestureNegativeOverflowText + " · 动作冲突";
            _stageGesturePositiveConflictOverflowText = _stageGesturePositiveOverflowText + " · 动作冲突";
        }

        /// <remarks>NoAlloc after Binder construction.</remarks>
        private string ResolveStageGesturePreviewText(int frameDelta, bool hasActionConflict)
        {
            if (frameDelta < -StageGestureTooltipDeltaLimit)
                return hasActionConflict
                    ? _stageGestureNegativeConflictOverflowText
                    : _stageGestureNegativeOverflowText;
            if (frameDelta > StageGestureTooltipDeltaLimit)
                return hasActionConflict
                    ? _stageGesturePositiveConflictOverflowText
                    : _stageGesturePositiveOverflowText;
            int index = frameDelta + StageGestureTooltipDeltaLimit;
            return hasActionConflict
                ? _stageGestureConflictPreviewTextCache[index] ?? string.Empty
                : _stageGesturePreviewTextCache[index] ?? string.Empty;
        }

        private static CharacterTestRuntimeAiLabStageSelectionModifier ResolveStageSelectionModifier(
            Fgui.EventContext context)
        {
            if (context?.inputEvent == null)
                return CharacterTestRuntimeAiLabStageSelectionModifier.None;
            if (context.inputEvent.ctrl || context.inputEvent.command)
                return CharacterTestRuntimeAiLabStageSelectionModifier.Toggle;
            return context.inputEvent.shift
                ? CharacterTestRuntimeAiLabStageSelectionModifier.AddRange
                : CharacterTestRuntimeAiLabStageSelectionModifier.None;
        }

        private void ConfigureTimelineBackgroundSelection()
        {
            if (_contentLayer == null)
                return;
            _contentLayer.onTouchBegin.Set(context =>
            {
                HideTransitionResultMenu();
                if (context?.inputEvent == null)
                    return;
                if (!TryCommitActiveStageFieldBeforeNavigation())
                    return;
                Vector2 local = _contentLayer.GlobalToLocal(new Vector2(
                    context.inputEvent.x,
                    context.inputEvent.y));
                if (!(_commands?.BeginStageMarquee(out _stageMarqueeEpoch) ?? false))
                    return;
                _stageMarqueeCaptured = true;
                _stageMarqueeMoved = false;
                _stageMarqueeStartX = local.x;
                _stageMarqueeStartY = local.y;
                _stageMarqueeModifier = ResolveStageSelectionModifier(context);
                EnsureStageMarqueeScratch();
                CaptureStageMarqueeSources();
                EnsureStageGestureScratch();
                CaptureTimelineTransientVisuals();
                _stageGesturePixelsPerFrame = (CanvasRight - CanvasLeft)
                    / Math.Max(1f, _lastRulerEndFrame - _lastRulerStartFrame);
                BeginTimelineTransientViewport();
                context.CaptureTouch();
            });
            _contentLayer.onTouchMove.Set(context =>
            {
                if (!_stageMarqueeCaptured || context?.inputEvent == null)
                    return;
                Vector2 local = _contentLayer.GlobalToLocal(new Vector2(
                    context.inputEvent.x,
                    context.inputEvent.y));
                if (!_stageMarqueeMoved
                    && Math.Abs(local.x - _stageMarqueeStartX) < 4f
                    && Math.Abs(local.y - _stageMarqueeStartY) < 4f)
                {
                    return;
                }
                _stageMarqueeMoved = true;
                UpdateTimelineEdgeAutoScroll(context.inputEvent.x, context.inputEvent.y);
                float viewportShiftX = _stageTransientViewportFrameOffset * _stageGesturePixelsPerFrame;
                ApplyTimelineTransientViewportVisuals(viewportShiftX);
                UpdateStageMarquee(local.x, local.y);
            });
            _contentLayer.onTouchEnd.Set(context =>
            {
                if (!_stageMarqueeCaptured)
                    return;
                bool moved = _stageMarqueeMoved;
                CharacterTestRuntimeAiLabStageSelectionModifier modifier = _stageMarqueeModifier;
                int viewportFrameDelta = _stageTransientViewportFrameOffset;
                long epoch = _stageMarqueeEpoch;
                int hitCount = moved ? CaptureStageMarqueeHits() : 0;
                ClearStageMarqueeState(restoreVisuals: true);
                if (moved)
                    _commands?.CommitStageMarquee(
                        epoch,
                        _stageMarqueeHitIds,
                        hitCount,
                        modifier,
                        viewportFrameDelta);
                else if (modifier == CharacterTestRuntimeAiLabStageSelectionModifier.None)
                {
                    _commands?.CancelStageMarquee(epoch);
                    _commands?.ClearStageSelection();
                }
                else
                {
                    _commands?.CancelStageMarquee(epoch);
                }
            });
        }

        private void UpdateStageMarquee(float currentX, float currentY)
        {
            float viewportShiftX = _stageTransientViewportFrameOffset * _stageGesturePixelsPerFrame;
            float startX = _stageMarqueeStartX - viewportShiftX;
            float left = Math.Min(startX, currentX);
            float right = Math.Max(startX, currentX);
            float top = Math.Min(_stageMarqueeStartY, currentY);
            float bottom = Math.Max(_stageMarqueeStartY, currentY);
            for (int i = 0; i < _activeStageCount; i++)
            {
                float stageLeft = _stageX[i] - CanvasLeft - viewportShiftX;
                float stageTop = _stageY[i] - ContentTop;
                _stageMarqueeMask[i] = right > stageLeft
                    && left < stageLeft + _stageWidth[i]
                    && bottom > stageTop
                    && top < stageTop + _stageHeight[i];
            }
            Fgui.GObject visual = _root?.GetChild("timelineMarquee");
            if (visual == null)
                return;
            visual.visible = true;
            visual.sortingOrder = 210;
            visual.SetXY(CanvasLeft + left, ContentTop + top);
            visual.SetSize(Math.Max(1f, right - left), Math.Max(1f, bottom - top));
        }

        private void EnsureStageMarqueeScratch()
        {
            if (_stageMarqueeMask.Length >= _activeStageCount
                && _stageMarqueeSourceIds.Length >= _activeStageCount
                && _stageMarqueeHitIds.Length >= _activeStageCount)
            {
                Array.Clear(_stageMarqueeMask, 0, _activeStageCount);
                return;
            }
            int capacity = Math.Max(_activeStageCount, Math.Max(8, _stageMarqueeMask.Length * 2));
            Array.Resize(ref _stageMarqueeMask, capacity);
            Array.Resize(ref _stageMarqueeSourceIds, capacity);
            Array.Resize(ref _stageMarqueeHitIds, capacity);
        }

        private void CaptureStageMarqueeSources()
        {
            _stageMarqueeSourceCount = _activeStageCount;
            for (int i = 0; i < _stageMarqueeSourceCount; i++)
                _stageMarqueeSourceIds[i] = _stagePool[i].StableId;
        }

        private int CaptureStageMarqueeHits()
        {
            int count = 0;
            for (int i = 0; i < _stageMarqueeSourceCount; i++)
            {
                if (_stageMarqueeMask[i])
                    _stageMarqueeHitIds[count++] = _stageMarqueeSourceIds[i];
            }
            return count;
        }

        private void ApplyTimelineTransientViewportVisuals(float viewportShiftX)
        {
            for (int i = 0; i < _stagePool.Count; i++)
            {
                if (_stagePool[i].Visual.visible)
                    _stagePool[i].Visual.x = _stageGestureBaseX[i] - viewportShiftX;
            }
            ApplyTimelineTransientViewportToAuxiliaryVisuals(viewportShiftX);
        }

        private void CancelStageMarquee()
        {
            if (!_stageMarqueeCaptured)
                return;
            _commands?.CancelStageMarquee(_stageMarqueeEpoch);
            ClearStageMarqueeState(restoreVisuals: true);
        }

        private void ClearStageMarqueeState(bool restoreVisuals)
        {
            if (restoreVisuals)
                RestoreTimelineTransientVisuals();
            _stageMarqueeCaptured = false;
            _stageMarqueeMoved = false;
            _stageMarqueeEpoch = 0L;
            for (int i = 0; i < _stageMarqueeSourceCount; i++)
            {
                _stageMarqueeSourceIds[i] = string.Empty;
                _stageMarqueeHitIds[i] = string.Empty;
                _stageMarqueeMask[i] = false;
            }
            _stageMarqueeSourceCount = 0;
            _stageMarqueeStartX = 0f;
            _stageMarqueeStartY = 0f;
            _stageMarqueeModifier = default;
            _stageGesturePixelsPerFrame = 0f;
            ResetTimelineTransientViewport();
            SetObjectVisible("timelineMarquee", false);
        }
    }
}
