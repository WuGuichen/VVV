using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void DrawRuler(int startFrame, int endFrame, float width)
        {
            const int divisions = 10;
            bool refreshLabels = !_hasRulerLabels
                || _lastRulerStartFrame != startFrame
                || _lastRulerEndFrame != endFrame;
            for (int i = 0; i <= divisions; i++)
            {
                float fraction = i / (float)divisions;
                int frame = Mathf.RoundToInt(Mathf.Lerp(startFrame, endFrame, fraction));
                float x = CanvasLeft + width * fraction;
                Fgui.GGraph tick = GetRulerTick(i);
                tick.SetXY(x - CanvasLeft, 0f);
                tick.DrawRect(1f, ContentHeight, 0, Color.clear, new Color(0.19f, 0.31f, 0.36f, 0.42f));
                Fgui.GTextField label = GetRulerLabel(i);
                label.SetXY(Mathf.Clamp(x - 40f, CanvasLeft, CanvasRight - 80f), RulerY);
                label.SetSize(80f, 22f);
                if (refreshLabels)
                    label.text = (frame / 60f).ToString("0.0") + "s · " + frame + "f";
            }
            _lastRulerStartFrame = startFrame;
            _lastRulerEndFrame = endFrame;
            _hasRulerLabels = true;
            for (int i = divisions + 1; i < _rulerTickPool.Count; i++)
                _rulerTickPool[i].visible = false;
            for (int i = divisions + 1; i < _rulerLabelPool.Count; i++)
                _rulerLabelPool[i].visible = false;
        }

        private Fgui.GGraph GetRulerTick(int index)
        {
            while (_rulerTickPool.Count <= index)
            {
                var tick = new Fgui.GGraph { touchable = false };
                _rulerTickPool.Add(tick);
                _contentLayer.AddChild(tick);
            }
            Fgui.GGraph result = _rulerTickPool[index];
            result.visible = true;
            return result;
        }

        private Fgui.GTextField GetRulerLabel(int index)
        {
            while (_rulerLabelPool.Count <= index)
            {
                var label = new Fgui.GTextField
                {
                    touchable = false,
                    color = new Color32(143, 174, 184, 255),
                    align = Fgui.AlignType.Center
                };
                Fgui.TextFormat format = label.textFormat;
                format.size = 11;
                format.color = label.color;
                label.textFormat = format;
                _rulerLabelPool.Add(label);
                _root.AddChild(label);
            }
            Fgui.GTextField result = _rulerLabelPool[index];
            result.visible = true;
            return result;
        }

        private Fgui.GGraph GetConnector(int index)
        {
            while (_connectorPool.Count <= index)
            {
                var graph = new Fgui.GGraph { touchable = true };
                graph.onClick.Set(HandleTransitionConnectorClick);
                _connectorPool.Add(graph);
                _contentLayer.AddChild(graph);
            }
            Fgui.GGraph result = _connectorPool[index];
            result.visible = true;
            result.sortingOrder = ConnectorSortingOrder;
            return result;
        }

        private void DrawConnector(
            Fgui.GGraph graph,
            float x1,
            float y1,
            float x2,
            float y2,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = Mathf.Max(2f, Mathf.Sqrt(dx * dx + dy * dy));
            graph.SetXY(x1, y1);
            graph.rotation = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            float width = TransitionLineWidth(transition);
            graph.DrawRect(length, width, 0, Color.clear, Color.white);
            graph.data = transition.TransitionId;
            graph.alpha = 1f;
            graph.color = TransitionColor(transition);
        }

        private int DrawTransitionCurve(
            int connectorIndex,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            float escapeX,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            const int SegmentCount = 24;
            // The endpoint tangents stay inside narrow, port-aligned corridors.
            // Horizontal movement happens only after the curve reaches the
            // obstacle-free route gutter below the card matrix.
            float secondControlX = targetX;
            float secondControlY = routeY;
            bool lateralSourceEscape = IsCardDetectorRoute(transition);
            Vector2 previous = new Vector2(sourceX, sourceY);
            for (int segment = 1; segment <= SegmentCount; segment++)
            {
                float t = segment / (float)SegmentCount;
                Vector2 point = EvaluateTransitionRoutePoint(
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    routeY,
                    escapeX,
                    lateralSourceEscape,
                    t);
                DrawConnector(
                    GetConnector(connectorIndex++),
                    previous.x,
                    previous.y,
                    point.x,
                    point.y,
                    transition);
                if (transition.Pending && (segment & 1) == 0)
                    _connectorPool[connectorIndex - 1].alpha = 0.45f;
                else if (transition.PreviousCommitted)
                    _connectorPool[connectorIndex - 1].alpha = 0.58f;
                else
                    _connectorPool[connectorIndex - 1].alpha = 1f;
                previous = point;
            }

            float tangentX = targetX - secondControlX;
            float tangentY = targetY - secondControlY;
            float tangentLength = Mathf.Max(0.001f, Mathf.Sqrt(tangentX * tangentX + tangentY * tangentY));
            float unitX = tangentX / tangentLength;
            float unitY = tangentY / tangentLength;
            const float ArrowLength = 12f;
            const float ArrowWidth = 6f;
            DrawConnector(
                GetConnector(connectorIndex++),
                targetX,
                targetY,
                targetX - unitX * ArrowLength - unitY * ArrowWidth,
                targetY - unitY * ArrowLength + unitX * ArrowWidth,
                transition);
            DrawConnector(
                GetConnector(connectorIndex++),
                targetX,
                targetY,
                targetX - unitX * ArrowLength + unitY * ArrowWidth,
                targetY - unitY * ArrowLength - unitX * ArrowWidth,
                transition);
            return connectorIndex;
        }

        internal static Vector2 EvaluateTransitionRoutePoint(
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            float t)
        {
            return EvaluateTransitionRoutePoint(
                sourceX,
                sourceY,
                targetX,
                targetY,
                routeY,
                sourceX,
                false,
                t);
        }

        internal static Vector2 EvaluateTransitionRoutePoint(
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            float escapeX,
            float t)
        {
            return EvaluateTransitionRoutePoint(
                sourceX,
                sourceY,
                targetX,
                targetY,
                routeY,
                escapeX,
                true,
                t);
        }

        internal static Vector2 EvaluateTransitionRoutePoint(
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            float escapeX,
            bool lateralSourceEscape,
            float t)
        {
            float clamped = Mathf.Clamp01(t);
            Vector2 escapeAnchor = new Vector2(escapeX, routeY);
            if (clamped <= 0.5f)
            {
                float local = clamped * 2f;
                if (lateralSourceEscape)
                {
                    return EvaluateCubicBezier(
                        new Vector2(sourceX, sourceY),
                        new Vector2(Mathf.Lerp(sourceX, escapeX, 0.58f), sourceY),
                        new Vector2(escapeX, sourceY),
                        escapeAnchor,
                        local);
                }
                return EvaluateCubicBezier(
                    new Vector2(sourceX, sourceY),
                    new Vector2(sourceX, Mathf.Lerp(sourceY, routeY, 0.58f)),
                    new Vector2(sourceX, routeY),
                    escapeAnchor,
                    local);
            }
            return EvaluateCubicBezier(
                escapeAnchor,
                escapeAnchor,
                new Vector2(targetX, routeY),
                new Vector2(targetX, targetY),
                (clamped - 0.5f) * 2f);
        }

        private static Vector2 EvaluateCubicBezier(
            Vector2 start,
            Vector2 control1,
            Vector2 control2,
            Vector2 end,
            float t)
        {
            float inverse = 1f - t;
            float inverseSquared = inverse * inverse;
            float tSquared = t * t;
            return start * (inverseSquared * inverse)
                + control1 * (3f * inverseSquared * t)
                + control2 * (3f * inverse * tSquared)
                + end * (tSquared * t);
        }

        private Color TransitionColor(CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition.Invalid)
                return new Color32(226, 82, 70, 255);
            if (_behaviorLogicMode)
            {
                if (IsBehaviorLogicReplan(transition))
                    return new Color32(190, 126, 236, 255);
                if (transition.EndsCard)
                    return new Color32(82, 211, 145, 255);
                if (ClassifyTransitionRoute(transition) == TransitionRouteClass.BackEdge
                    || ClassifyTransitionRoute(transition) == TransitionRouteClass.SelfLoop)
                {
                    return new Color32(190, 126, 236, 255);
                }
                int detectorIndex = transition.DetectorIndex;
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                    detectorIndex >= 0 && detectorIndex < _boundDetectorCount
                        ? _boundDetectors[detectorIndex]
                        : null;
                switch (detector?.Trigger)
                {
                    case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                        return new Color32(255, 107, 115, 255);
                    case CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge:
                        return new Color32(245, 167, 68, 255);
                    default:
                        return new Color32(54, 197, 230, 255);
                }
            }
            if (transition.Committed)
                return new Color32(180, 119, 242, 255);
            if (transition.PreviousCommitted)
                return new Color32(122, 93, 153, 255);
            if (transition.Pending)
                return new Color32(235, 154, 71, 255);
            if (transition.Selected)
                return new Color32(76, 213, 193, 255);
            return new Color32(77, 112, 128, 220);
        }

        private static float TransitionLineWidth(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition.Invalid || transition.Committed)
                return 4f;
            if (transition.PreviousCommitted)
                return 2f;
            if (transition.Pending || transition.Selected)
                return 3f;
            return 2f;
        }

        private void HandleTransitionConnectorClick(Fgui.EventContext context)
        {
            context?.StopPropagation();
            if (!(context?.sender is Fgui.GGraph graph) || !(graph.data is string transitionId))
                return;
            if (!TryCommitActiveGraphFieldBeforeNavigation())
                return;
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectTransition,
                transitionId,
                0,
                CharacterTestRuntimeAiLabLogicElementKind.Transition);
        }

        private static void ColorCard(Fgui.GComponent card, CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            SetGraphColor(card, "background", stage.Current
                ? new Color32(10, 48, 62, 255)
                : stage.Selected ? new Color32(12, 40, 52, 255) : new Color32(13, 27, 36, 255));
            SetGraphColor(card, "stripe", stage.ActionConflict
                ? new Color32(226, 82, 70, 255)
                : stage.Current ? new Color32(63, 226, 199, 255)
                : stage.Selected ? new Color32(48, 182, 222, 255)
                : new Color32(65, 107, 121, 255));
        }

        private static void ColorDetector(Fgui.GComponent band, CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            SetGraphColor(band, "background", detector.Triggered
                ? new Color32(82, 60, 18, 255)
                : detector.Active ? new Color32(13, 55, 54, 255)
                : new Color32(28, 31, 33, 255));
            SetGraphColor(band, "stripe", detector.Triggered
                ? new Color32(255, 196, 67, 255)
                : detector.Active ? new Color32(65, 220, 194, 255)
                : detector.Selected ? new Color32(48, 182, 222, 255)
                : new Color32(169, 133, 63, 255));
        }

        private void ColorTransition(Fgui.GComponent chip, CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            SetGraphColor(chip, "background", transition.Committed
                ? new Color32(55, 28, 76, 255)
                : transition.PreviousCommitted ? new Color32(35, 29, 45, 255)
                : transition.Pending ? new Color32(70, 43, 18, 255)
                : new Color32(27, 31, 43, 255));
            SetGraphColor(chip, "stripe", (Color32)TransitionColor(transition));
        }

        private float StageCenterX(int index)
        {
            if (index < 0 || index >= _stageX.Length)
                return CanvasLeft;
            return _stageX[index] + _stageWidth[index] * 0.5f;
        }

        private float StageBottomY(int index)
        {
            if (index < 0 || index >= _stageY.Length)
                return ContentTop;
            return _stageY[index] + _stageHeight[index];
        }

        private static int DetectorIndexWithinStage(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            int index)
        {
            int rank = 0;
            int stageIndex = detectors[index].StageIndex;
            int priorityRank = detectors[index].PriorityRank;
            for (int i = 0; i < detectorCount; i++)
            {
                if (i != index
                    && detectors[i] != null
                    && !detectors[i].CardScope
                    && detectors[i].StageIndex == stageIndex
                    && detectors[i].PriorityRank < priorityRank)
                    rank++;
            }
            return rank;
        }

        private static int CardScopeDisplayRank(
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            int index)
        {
            int rank = 0;
            int priorityRank = detectors[index].PriorityRank;
            for (int i = 0; i < detectorCount; i++)
            {
                if (i != index
                    && detectors[i] != null
                    && detectors[i].CardScope
                    && detectors[i].PriorityRank < priorityRank)
                {
                    rank++;
                }
            }
            return rank;
        }

        private void EnsureLayoutScratch(int stageCount, int detectorCount, int transitionCount)
        {
            if (_stageX.Length < stageCount)
            {
                int capacity = Math.Max(stageCount, Math.Max(8, _stageX.Length * 2));
                Array.Resize(ref _stageX, capacity);
                Array.Resize(ref _stageY, capacity);
                Array.Resize(ref _stageWidth, capacity);
                Array.Resize(ref _stageHeight, capacity);
                Array.Resize(ref _stageLane, capacity);
                Array.Resize(ref _stageDetectorCount, capacity);
                Array.Resize(ref _laneHeight, capacity);
                Array.Resize(ref _laneY, capacity);
            }
            if (_detectorX.Length < detectorCount)
            {
                int capacity = Math.Max(detectorCount, Math.Max(8, _detectorX.Length * 2));
                Array.Resize(ref _detectorX, capacity);
                Array.Resize(ref _detectorY, capacity);
                Array.Resize(ref _detectorWidth, capacity);
                Array.Resize(ref _detectorHeight, capacity);
            }
            if (_transitionChipX.Length < transitionCount)
            {
                int capacity = Math.Max(transitionCount, Math.Max(8, _transitionChipX.Length * 2));
                Array.Resize(ref _transitionChipX, capacity);
                Array.Resize(ref _transitionChipWidth, capacity);
                Array.Resize(ref _transitionChipHeight, capacity);
                Array.Resize(ref _transitionRouteTopY, capacity);
                Array.Resize(ref _transitionRouteY, capacity);
                Array.Resize(ref _transitionRouteStartX, capacity);
                Array.Resize(ref _transitionRouteEndX, capacity);
                Array.Resize(ref _transitionRouteLane, capacity);
                Array.Resize(ref _transitionLaneSpan, capacity);
                Array.Resize(ref _transitionOrder, capacity);
                Array.Resize(ref _transitionConnectorStart, capacity);
                Array.Resize(ref _transitionConnectorCount, capacity);
            }
        }

    }
}
