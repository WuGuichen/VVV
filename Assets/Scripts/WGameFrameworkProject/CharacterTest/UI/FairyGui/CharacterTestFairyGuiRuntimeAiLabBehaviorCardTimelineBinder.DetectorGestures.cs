using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private enum DetectorPointerGestureKind
        {
            MoveWindow = 0,
            ResizeWindow = 1,
            ReorderPriority = 2
        }

        private DetectorPointerGestureKind _detectorPointerGestureKind;

        private void ConfigureDetectorPointerGestures(DetectorSlot slot)
        {
            Fgui.GObject title = slot.Visual.GetChild("title");
            if (title != null)
            {
                title.touchable = true;
                title.onTouchBegin.Set(context => BeginDetectorPointerGesture(
                    slot,
                    DetectorPointerGestureKind.MoveWindow,
                    context));
                title.onTouchMove.Set(UpdateDetectorPointerGesture);
                title.onTouchEnd.Set(EndDetectorPointerGesture);
            }
            Fgui.GObject stripe = slot.Visual.GetChild("stripe");
            if (stripe != null)
            {
                stripe.touchable = true;
                stripe.onTouchBegin.Set(context => BeginDetectorPointerGesture(
                    slot,
                    DetectorPointerGestureKind.ReorderPriority,
                    context));
                stripe.onTouchMove.Set(UpdateDetectorPointerGesture);
                stripe.onTouchEnd.Set(EndDetectorPointerGesture);
            }

            slot.ResizeHandle = GetRequiredGraph(slot.Visual, "windowResizeHandle");
            slot.ResizeHandle.touchable = true;
            slot.ResizeHandle.onTouchBegin.Set(context => BeginDetectorPointerGesture(
                slot,
                DetectorPointerGestureKind.ResizeWindow,
                context));
            slot.ResizeHandle.onTouchMove.Set(UpdateDetectorPointerGesture);
            slot.ResizeHandle.onTouchEnd.Set(EndDetectorPointerGesture);
            slot.OutputPort = GetRequiredGraph(slot.Visual, "outputPort");
            slot.OutputPort.touchable = true;
            slot.OutputPort.onTouchBegin.Set(context => BeginTransitionConnect(slot, context));
            slot.OutputPort.onTouchMove.Set(UpdateTransitionConnect);
            slot.OutputPort.onTouchEnd.Set(EndTransitionConnect);
        }

        private void LayoutDetectorGestureTargets(DetectorSlot slot, float width, float height, bool selected)
        {
            if (slot.ResizeHandle != null)
            {
                slot.ResizeHandle.visible = _canEdit
                    && !selected
                    && slot.ViewModel?.WindowEndMode
                        == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed;
                slot.ResizeHandle.SetXY(Math.Max(0f, width - 8f), 0f);
                slot.ResizeHandle.SetSize(8f, height);
            }
            if (slot.OutputPort != null)
            {
                slot.OutputPort.visible = !selected;
                slot.OutputPort.SetXY(Math.Max(0f, width - 7f), Math.Max(0f, height * 0.5f - 7f));
                slot.OutputPort.sortingOrder = 80;
            }
        }

        private void BeginDetectorPointerGesture(
            DetectorSlot slot,
            DetectorPointerGestureKind kind,
            Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (!_canEdit || slot?.ViewModel == null || context?.inputEvent == null)
                return;
            CharacterTestRuntimeAiLabDetectorFieldKind field = kind == DetectorPointerGestureKind.ResizeWindow
                ? CharacterTestRuntimeAiLabDetectorFieldKind.ActivationDurationFrames
                : kind == DetectorPointerGestureKind.ReorderPriority
                    ? CharacterTestRuntimeAiLabDetectorFieldKind.Priority
                    : CharacterTestRuntimeAiLabDetectorFieldKind.ActivationStartFrame;
            if (kind == DetectorPointerGestureKind.ResizeWindow
                && slot.ViewModel.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
                return;
            if (_commands?.BeginDetectorFieldEdit(slot.StableId, field, out long epoch) != true)
                return;
            _detectorGestureSlot = slot;
            _detectorGestureEpoch = epoch;
            _detectorGestureStartX = context.inputEvent.x;
            _detectorGestureStartY = context.inputEvent.y;
            _detectorPointerBaseX = slot.Visual.x;
            _detectorPointerBaseY = slot.Visual.y;
            _detectorPointerBaseWidth = slot.Visual.width;
            _detectorPointerBaseValue = field == CharacterTestRuntimeAiLabDetectorFieldKind.ActivationStartFrame
                ? slot.ViewModel.StartFrame
                : field == CharacterTestRuntimeAiLabDetectorFieldKind.ActivationDurationFrames
                    ? slot.ViewModel.DurationFrames
                    : slot.ViewModel.Priority;
            _detectorGestureField = field;
            _detectorPointerGestureKind = kind;
            _detectorGestureCaptured = true;
            _detectorGestureMoved = false;
            context.CaptureTouch();
            if (kind == DetectorPointerGestureKind.ReorderPriority)
                SetGraphFieldError(slot.Visual, "上下拖动将改变同帧裁决优先级；释放后提交一次语义变更。");
        }

        private void UpdateDetectorPointerGesture(Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (!_detectorGestureCaptured || _detectorGestureSlot == null || context?.inputEvent == null)
                return;
            float deltaX = context.inputEvent.x - _detectorGestureStartX;
            float deltaY = context.inputEvent.y - _detectorGestureStartY;
            _detectorGestureMoved |= Math.Abs(deltaX) >= 4f || Math.Abs(deltaY) >= 4f;
            if (!_detectorGestureMoved)
                return;
            if (_detectorPointerGestureKind == DetectorPointerGestureKind.ReorderPriority)
            {
                _detectorGestureSlot.Visual.y = _detectorPointerBaseY + Mathf.Clamp(deltaY, -64f, 64f);
                return;
            }
            float pixelsPerFrame = (CanvasRight - CanvasLeft)
                / Math.Max(1f, _lastRulerEndFrame - _lastRulerStartFrame);
            int deltaFrames = Mathf.RoundToInt(deltaX / Math.Max(0.001f, pixelsPerFrame));
            int snap = _commands?.StageSnapEnabled == false ? 1 : 15;
            deltaFrames = Mathf.RoundToInt(deltaFrames / (float)snap) * snap;
            if (_detectorPointerGestureKind == DetectorPointerGestureKind.MoveWindow)
                _detectorGestureSlot.Visual.x = _detectorPointerBaseX + deltaFrames * pixelsPerFrame;
            else
                _detectorGestureSlot.Visual.SetSize(
                    Math.Max(36f, _detectorPointerBaseWidth + deltaFrames * pixelsPerFrame),
                    _detectorGestureSlot.Visual.height);
        }

        private void EndDetectorPointerGesture(Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (!_detectorGestureCaptured || _detectorGestureSlot == null)
                return;
            DetectorSlot slot = _detectorGestureSlot;
            long epoch = _detectorGestureEpoch;
            bool moved = _detectorGestureMoved;
            float deltaX = context?.inputEvent == null ? 0f : context.inputEvent.x - _detectorGestureStartX;
            float deltaY = context?.inputEvent == null ? 0f : context.inputEvent.y - _detectorGestureStartY;
            ClearDetectorPointerGesture();
            if (!moved)
            {
                _commands?.CancelGraphFieldEdit(deactivating: false);
                return;
            }
            int requested;
            if (_detectorPointerGestureKind == DetectorPointerGestureKind.ReorderPriority)
            {
                int steps = Math.Max(1, Mathf.RoundToInt(Math.Abs(deltaY) / (DetectorBandHeight + 4f)));
                requested = ResolveDetectorPriorityForRankMove(slot.ViewModel, deltaY < 0f, steps);
            }
            else
            {
                float pixelsPerFrame = (CanvasRight - CanvasLeft)
                    / Math.Max(1f, _lastRulerEndFrame - _lastRulerStartFrame);
                int deltaFrames = Mathf.RoundToInt(deltaX / Math.Max(0.001f, pixelsPerFrame));
                int snap = _commands?.StageSnapEnabled == false ? 1 : 15;
                deltaFrames = Mathf.RoundToInt(deltaFrames / (float)snap) * snap;
                requested = Mathf.Clamp(
                    _detectorPointerBaseValue + deltaFrames,
                    _detectorGestureField == CharacterTestRuntimeAiLabDetectorFieldKind.ActivationDurationFrames ? 1 : 0,
                    StageTimelineFrameLimit);
            }
            _commands?.CommitDetectorFieldEdit(
                epoch,
                slot.StableId,
                _detectorGestureField,
                new CharacterTestRuntimeAiLabDetectorFieldValue(intValue: requested));
        }

        private void ClearDetectorPointerGesture()
        {
            if (_detectorGestureSlot != null)
            {
                _detectorGestureSlot.Visual.x = _detectorPointerBaseX;
                if (_detectorPointerGestureKind == DetectorPointerGestureKind.ReorderPriority)
                    _detectorGestureSlot.Visual.y = _detectorPointerBaseY;
                _detectorGestureSlot.Visual.SetSize(
                    _detectorPointerBaseWidth,
                    _detectorGestureSlot.Visual.height);
            }
            _detectorGestureSlot = null;
            _detectorGestureCaptured = false;
            _detectorGestureMoved = false;
            _detectorGestureEpoch = 0L;
        }

        private void BeginTransitionConnect(DetectorSlot slot, Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (!_canEdit || slot == null || context?.inputEvent == null
                || _commands?.BeginTransitionConnect(slot.StableId, out long epoch) != true)
                return;
            EnsureTransitionConnectPreview();
            _transitionConnectSource = slot;
            _transitionConnectEpoch = epoch;
            _transitionConnectCaptured = true;
            context.CaptureTouch();
            UpdateTransitionConnect(context);
        }

        private void UpdateTransitionConnect(Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (!_transitionConnectCaptured || _transitionConnectSource?.OutputPort == null
                || context?.inputEvent == null)
                return;
            Vector2 source = _contentLayer.GlobalToLocal(
                _transitionConnectSource.OutputPort.LocalToGlobal(new Vector2(7f, 7f)));
            Vector2 target = _contentLayer.GlobalToLocal(new Vector2(
                context.inputEvent.x,
                context.inputEvent.y));
            DrawTransitionConnectPreview(source, target);
        }

        private void EndTransitionConnect(Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (!_transitionConnectCaptured || _transitionConnectSource == null)
                return;
            string detectorId = _transitionConnectSource.StableId;
            long epoch = _transitionConnectEpoch;
            string targetId = context?.inputEvent == null
                ? string.Empty
                : HitTransitionTarget(context.inputEvent.x, context.inputEvent.y);
            ClearTransitionConnectPreview();
            if (string.IsNullOrEmpty(targetId))
            {
                _commands?.CancelTransitionConnect();
                return;
            }
            _commands?.CommitTransitionConnect(epoch, detectorId, targetId);
        }

        private string HitTransitionTarget(float globalX, float globalY)
        {
            Vector2 global = new Vector2(globalX, globalY);
            for (int i = 0; i < _activeStageCount && i < _stagePool.Count; i++)
            {
                StageSlot slot = _stagePool[i];
                if (slot.InputPort == null || !slot.Visual.visible)
                    continue;
                Vector2 local = slot.InputPort.GlobalToLocal(global);
                if (local.x >= -8f && local.x <= 20f && local.y >= -8f && local.y <= 20f)
                    return slot.StableId;
            }
            if (_endPort != null)
            {
                Vector2 local = _endPort.GlobalToLocal(global);
                if (local.x >= -10f && local.x <= 26f && local.y >= -10f && local.y <= 26f)
                    return CharacterTestRuntimeAiLabLogicTransition.EndTarget;
            }
            return string.Empty;
        }

        private void EnsureTransitionConnectPreview()
        {
            for (int i = 0; i < _transitionConnectPreview.Length; i++)
            {
                if (_transitionConnectPreview[i] != null)
                    continue;
                var graph = new Fgui.GGraph { touchable = false, sortingOrder = 90 };
                graph.DrawRect(1f, 2f, 0, Color.clear, Color.white);
                graph.color = new Color32(73, 218, 196, 220);
                _transitionConnectPreview[i] = graph;
                _contentLayer.AddChild(graph);
            }
            if (_endPort == null)
                throw new InvalidOperationException(
                    "Runtime AI Lab timeline is missing required Graph 'endPort'. Apply and publish UI Composer sources.");
        }

        private void LayoutTransitionPorts()
        {
            for (int i = 0; i < _activeStageCount && i < _stagePool.Count; i++)
            {
                StageSlot slot = _stagePool[i];
                if (slot.InputPort == null)
                    continue;
                slot.InputPort.visible = true;
                slot.InputPort.SetXY(-6f, 24f);
                slot.InputPort.sortingOrder = 75;
            }
            EnsureTransitionConnectPreview();
            _endPort.visible = true;
            _endPort.SetXY(CanvasRight - CanvasLeft - 18f, Math.Max(8f, _routeBaseY - ContentTop));
        }

        private void DrawTransitionConnectPreview(Vector2 source, Vector2 target)
        {
            float controlY = Math.Max(source.y, target.y) + 80f;
            float previousX = source.x;
            float previousY = source.y;
            for (int i = 1; i <= _transitionConnectPreview.Length; i++)
            {
                float t = i / (float)_transitionConnectPreview.Length;
                float inverse = 1f - t;
                float x = inverse * inverse * inverse * source.x
                    + 3f * inverse * inverse * t * source.x
                    + 3f * inverse * t * t * target.x
                    + t * t * t * target.x;
                float y = inverse * inverse * inverse * source.y
                    + 3f * inverse * inverse * t * controlY
                    + 3f * inverse * t * t * controlY
                    + t * t * t * target.y;
                DrawPreviewSegment(_transitionConnectPreview[i - 1], previousX, previousY, x, y);
                previousX = x;
                previousY = y;
            }
        }

        private static void DrawPreviewSegment(Fgui.GGraph graph, float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = Mathf.Max(2f, Mathf.Sqrt(dx * dx + dy * dy));
            graph.visible = true;
            graph.SetXY(x1, y1);
            graph.rotation = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            graph.SetSize(length, 2f);
        }

        private int ResolveDetectorPriorityForRankMove(
            CharacterTestRuntimeAiLabLogicDetectorViewModel source,
            bool moveUp,
            int steps)
        {
            return ResolveDetectorPriorityForCohortRankMove(
                _boundDetectors,
                _boundDetectorCount,
                source,
                moveUp,
                steps,
                _detectorPointerBaseValue);
        }

        internal static int ResolveDetectorPriorityForCohortRankMove(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel source,
            bool moveUp,
            int steps,
            int fallbackPriority)
        {
            if (source == null || steps <= 0 || detectors == null)
                return fallbackPriority;
            int safeCount = Math.Min(detectorCount, detectors.Length);
            int cohortCount = 0;
            int sourceRank = 0;
            for (int i = 0; i < safeCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel candidate = detectors[i];
                if (!SameDetectorPriorityCohort(source, candidate))
                    continue;
                cohortCount++;
                if (candidate.PriorityRank < source.PriorityRank)
                    sourceRank++;
            }
            int targetRank = Mathf.Clamp(
                sourceRank + (moveUp ? -steps : steps),
                0,
                Math.Max(0, cohortCount - 1));
            if (targetRank == sourceRank)
                return fallbackPriority;
            CharacterTestRuntimeAiLabLogicDetectorViewModel target = null;
            for (int i = 0; i < safeCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel candidate = detectors[i];
                if (!SameDetectorPriorityCohort(source, candidate))
                    continue;
                int candidateRank = 0;
                for (int otherIndex = 0; otherIndex < safeCount; otherIndex++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel other = detectors[otherIndex];
                    if (SameDetectorPriorityCohort(candidate, other)
                        && other.PriorityRank < candidate.PriorityRank)
                    {
                        candidateRank++;
                    }
                }
                if (candidateRank == targetRank)
                {
                    target = candidate;
                    break;
                }
            }
            if (target == null)
                return fallbackPriority;
            long requested = (long)target.Priority + (moveUp ? 1L : -1L);
            return requested < int.MinValue || requested > int.MaxValue
                ? fallbackPriority
                : (int)requested;
        }

        private static bool SameDetectorPriorityCohort(
            CharacterTestRuntimeAiLabLogicDetectorViewModel left,
            CharacterTestRuntimeAiLabLogicDetectorViewModel right)
        {
            if (left == null || right == null || left.CardScope != right.CardScope)
                return false;
            return left.CardScope || left.StageIndex == right.StageIndex;
        }

        private void ClearTransitionConnectPreview()
        {
            for (int i = 0; i < _transitionConnectPreview.Length; i++)
            {
                if (_transitionConnectPreview[i] != null)
                    _transitionConnectPreview[i].visible = false;
            }
            _transitionConnectCaptured = false;
            _transitionConnectSource = null;
            _transitionConnectEpoch = 0L;
        }
    }
}
