using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        internal static float ResolveScrolledRouteBaseY(
            float unscrolledRouteBaseY,
            float verticalOffset)
        {
            return unscrolledRouteBaseY - Math.Max(0f, verticalOffset);
        }

        private void FillStableTransitionOrder(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            for (int i = 0; i < transitionCount; i++)
                _transitionOrder[i] = i;
            for (int i = 1; i < transitionCount; i++)
            {
                int candidate = _transitionOrder[i];
                int insert = i;
                while (insert > 0
                    && CompareTransitionRouteOrder(
                        transitions[candidate],
                        transitions[_transitionOrder[insert - 1]]) < 0)
                {
                    _transitionOrder[insert] = _transitionOrder[insert - 1];
                    insert--;
                }
                _transitionOrder[insert] = candidate;
            }
        }

        internal enum TransitionRouteClass
        {
            Forward = 0,
            BackEdge = 1,
            SelfLoop = 2,
            End = 3,
            CardDetector = 4
        }

        internal static TransitionRouteClass ClassifyTransitionRoute(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition.SourceStageIndex < 0)
                return TransitionRouteClass.CardDetector;
            if (transition.EndsCard)
                return TransitionRouteClass.End;
            if (transition.SourceStageIndex == transition.TargetStageIndex)
                return TransitionRouteClass.SelfLoop;
            return transition.SourceStageIndex < transition.TargetStageIndex
                ? TransitionRouteClass.Forward
                : TransitionRouteClass.BackEdge;
        }

        internal static int CompareTransitionRouteOrder(
            CharacterTestRuntimeAiLabLogicTransitionViewModel left,
            CharacterTestRuntimeAiLabLogicTransitionViewModel right)
        {
            int result = ClassifyTransitionRoute(left).CompareTo(ClassifyTransitionRoute(right));
            if (result != 0)
                return result;
            int leftTarget = left.EndsCard ? int.MaxValue : left.TargetStageIndex;
            int rightTarget = right.EndsCard ? int.MaxValue : right.TargetStageIndex;
            int leftMinimum = Math.Min(left.SourceStageIndex, leftTarget);
            int rightMinimum = Math.Min(right.SourceStageIndex, rightTarget);
            result = leftMinimum.CompareTo(rightMinimum);
            if (result != 0)
                return result;
            int leftMaximum = Math.Max(left.SourceStageIndex, leftTarget);
            int rightMaximum = Math.Max(right.SourceStageIndex, rightTarget);
            result = leftMaximum.CompareTo(rightMaximum);
            if (result != 0)
                return result;
            result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
                return result;
            return string.Compare(left.TransitionId, right.TransitionId, StringComparison.Ordinal);
        }

        private float DetectorPortX(int detectorIndex, int fallbackStageIndex)
        {
            if (detectorIndex >= 0 && detectorIndex < _detectorX.Length)
                return _detectorX[detectorIndex] + _detectorWidth[detectorIndex];
            return fallbackStageIndex >= 0 && fallbackStageIndex < _activeStageCount
                ? _stageX[fallbackStageIndex] + _stageWidth[fallbackStageIndex] + 8f
                : CanvasLeft + 8f;
        }

        private float DetectorPortY(int detectorIndex, int fallbackStageIndex)
        {
            if (detectorIndex >= 0 && detectorIndex < _detectorY.Length)
                return _detectorY[detectorIndex] + Mathf.Min(_detectorHeight[detectorIndex], DetectorBandHeight) * 0.5f;
            return fallbackStageIndex >= 0 && fallbackStageIndex < _activeStageCount
                ? _stageY[fallbackStageIndex] + 72f
                : ContentTop + 24f;
        }

        private float StageInputPortX(int stageIndex)
        {
            return stageIndex >= 0 && stageIndex < _activeStageCount
                // Terminate inside the 12px input-port hit target. Ending at
                // its left edge made a sequential predecessor whose right
                // edge shares the same time coordinate an unavoidable route
                // obstacle.
                ? _stageX[stageIndex] + 2f
                : CanvasRight - 12f;
        }

        private float StageInputPortY(int stageIndex)
        {
            return stageIndex >= 0 && stageIndex < _activeStageCount
                ? _stageY[stageIndex] + 30f
                : _routeBaseY + 24f;
        }

        private int ResolveTransitionRouteLane(
            int transitionIndex,
            int transitionCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            float routeBaseY,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float chipX,
            float chipWidth,
            float chipHeight,
            float routeStartX,
            float routeEndX,
            int laneSpan,
            out float routeEscapeX)
        {
            int maximumRouteLaneCount = ResolveTransitionRouteLaneBudget(
                transitionCount,
                _activeStageCount + _activeDetectorCount);
            routeEscapeX = sourceX;
            int lastRejectionCode = 0;
            for (int lane = 0; lane < maximumRouteLaneCount; lane++)
            {
                float routeTop = routeBaseY + lane * TransitionRouteLaneHeight;
                bool overlaps = RouteLaneIntervalsOverlap(
                    transitionIndex,
                    routeTop,
                    laneSpan,
                    routeStartX,
                    routeEndX);
                if (!overlaps)
                {
                    float routeY = routeTop + chipHeight + TransitionRouteChipClearance;
                    Rect chipObstacle = new Rect(chipX, routeTop, chipWidth, chipHeight);
                    for (int escapeAttempt = 0; escapeAttempt < 4; escapeAttempt++)
                    {
                        float candidateEscapeX = ResolveTransitionEscapeX(
                            transition,
                            sourceX,
                            targetX,
                            escapeAttempt);
                        if (TransitionRouteLaneAvoidsObstacles(
                            transitionIndex,
                            transition,
                            sourceX,
                            sourceY,
                            targetX,
                            targetY,
                            routeY,
                            candidateEscapeX,
                            chipObstacle,
                            out lastRejectionCode))
                        {
                            routeEscapeX = candidateEscapeX;
                            return lane;
                        }
                    }
                }
            }
            int rejectionIndex = lastRejectionCode % 100000;
            string rejectionGeometry = lastRejectionCode >= 100000
                && lastRejectionCode < 300000
                && rejectionIndex >= 0
                && rejectionIndex < _activeStageCount
                    ? ", obstacle="
                        + _stageX[rejectionIndex]
                        + "/"
                        + _stageY[rejectionIndex]
                        + "/"
                        + _stageWidth[rejectionIndex]
                        + "/"
                        + _stageHeight[rejectionIndex]
                    : lastRejectionCode >= 300000
                        && lastRejectionCode < 500000
                        && rejectionIndex >= 0
                        && rejectionIndex < _activeDetectorCount
                            ? ", obstacle="
                                + _detectorX[rejectionIndex]
                                + "/"
                                + _detectorY[rejectionIndex]
                                + "/"
                                + _detectorWidth[rejectionIndex]
                                + "/"
                                + _detectorHeight[rejectionIndex]
                            : string.Empty;
            throw new InvalidOperationException(
                "Runtime AI Lab route solver exhausted its deterministic outer lanes for transition '"
                + (transition.TransitionId ?? string.Empty)
                + "' ("
                + ClassifyTransitionRoute(transition)
                + ", rejection "
                + lastRejectionCode
                + ", endpoints="
                + sourceX
                + "/"
                + sourceY
                + "->"
                + targetX
                + "/"
                + targetY
                + rejectionGeometry
                + ") without finding a collision-free route.");
        }

        internal static int ResolveTransitionRouteLaneBudget(
            int activeTransitionCount,
            int obstacleBandCount)
        {
            return Math.Max(1, activeTransitionCount + obstacleBandCount + 1);
        }

        private static float ResolveTransitionEscapeX(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            float sourceX,
            float targetX,
            int attempt)
        {
            TransitionRouteClass routeClass = ClassifyTransitionRoute(transition);
            bool prefersRight = routeClass == TransitionRouteClass.Forward
                || routeClass == TransitionRouteClass.End
                || routeClass == TransitionRouteClass.CardDetector;
            switch (attempt)
            {
                case 0:
                    return sourceX;
                case 1:
                    return prefersRight ? CanvasRight - 4f : CanvasLeft + 4f;
                case 2:
                    return prefersRight ? CanvasLeft + 4f : CanvasRight - 4f;
                default:
                    return targetX;
            }
        }

        private bool RouteLaneIntervalsOverlap(
            int transitionIndex,
            float routeTop,
            int laneSpan,
            float routeStartX,
            float routeEndX)
        {
            for (int previous = 0; previous < transitionIndex; previous++)
            {
                float routeBottom = routeTop + laneSpan * TransitionRouteLaneHeight;
                float previousTop = _transitionRouteTopY[previous];
                float previousBottom = previousTop
                    + _transitionLaneSpan[previous] * TransitionRouteLaneHeight;
                if (routeBottom <= previousTop || routeTop >= previousBottom)
                    continue;
                if (routeStartX < _transitionRouteEndX[previous]
                    && routeEndX > _transitionRouteStartX[previous])
                {
                    return true;
                }
            }
            return false;
        }

        private bool TransitionRouteLaneAvoidsObstacles(
            int transitionIndex,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            float escapeX,
            Rect chipObstacle,
            out int rejectionCode)
        {
            rejectionCode = 0;
            bool lateralSourceEscape = IsCardDetectorRoute(transition);
            const float ObstacleExpansion = 12f;
            Rect expandedCandidateChip = ExpandRouteObstacle(chipObstacle, ObstacleExpansion);
            for (int stageIndex = 0; stageIndex < _activeStageCount; stageIndex++)
            {
                if (stageIndex == transition.SourceStageIndex
                    || stageIndex == transition.TargetStageIndex)
                {
                    continue;
                }
                Rect routeObstacle = new Rect(
                    _stageX[stageIndex],
                    _stageY[stageIndex],
                    _stageWidth[stageIndex],
                    _stageHeight[stageIndex]);
                if (!IsVisibleRouteObstacle(routeObstacle))
                    continue;
                Rect stageClearance = ExpandRouteObstacle(routeObstacle, ObstacleExpansion);
                if (expandedCandidateChip.Overlaps(stageClearance))
                {
                    rejectionCode = 100000 + stageIndex;
                    return false;
                }
                if (TransitionRouteIntersectsObstacle(
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    routeY,
                    escapeX,
                    lateralSourceEscape,
                    routeObstacle))
                {
                    rejectionCode = 200000 + stageIndex;
                    return false;
                }
            }
            for (int detectorIndex = 0; detectorIndex < _activeDetectorCount; detectorIndex++)
            {
                if (detectorIndex == transition.DetectorIndex)
                    continue;
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                    _boundDetectors[detectorIndex];
                if (detector != null
                    && (detector.StageIndex == transition.SourceStageIndex
                        || detector.StageIndex == transition.TargetStageIndex))
                {
                    // Endpoint columns own a narrow port corridor. Sibling
                    // detectors in those columns must not make every possible
                    // outer lane collide while the curve exits or enters.
                    continue;
                }
                Rect routeObstacle = new Rect(
                    _detectorX[detectorIndex],
                    _detectorY[detectorIndex],
                    _detectorWidth[detectorIndex],
                    _detectorHeight[detectorIndex]);
                if (!IsVisibleRouteObstacle(routeObstacle))
                    continue;
                Rect detectorClearance = ExpandRouteObstacle(routeObstacle, ObstacleExpansion);
                if (expandedCandidateChip.Overlaps(detectorClearance))
                {
                    rejectionCode = 300000 + detectorIndex;
                    return false;
                }
                Rect detectorCurveObstacle = routeObstacle;
                if (lateralSourceEscape
                    && detector != null
                    && detector.CardScope)
                {
                    // Card-scoped Detector bands may share the exact output
                    // edge. Open only the two-pixel port strip; the remaining
                    // card body stays a hard obstacle, including when another
                    // escape attempt bends back through the sibling card.
                    detectorCurveObstacle = TrimSharedDetectorPortCorridor(
                        routeObstacle,
                        sourceX,
                        2f);
                }
                if (TransitionRouteIntersectsObstacle(
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    routeY,
                    escapeX,
                    lateralSourceEscape,
                    detectorCurveObstacle))
                {
                    rejectionCode = 400000 + detectorIndex;
                    return false;
                }
            }
            for (int previous = 0; previous < transitionIndex; previous++)
            {
                Rect previousChip = ExpandRouteObstacle(
                    new Rect(
                        _transitionChipX[previous],
                        _transitionRouteTopY[previous],
                        _transitionChipWidth[previous],
                        _transitionChipHeight[previous]),
                    ObstacleExpansion);
                if (!IsVisibleRouteObstacle(previousChip))
                    continue;
                if (expandedCandidateChip.Overlaps(previousChip))
                {
                    rejectionCode = 500000 + previous;
                    return false;
                }
                // Transition chips are opaque labels rendered above connector
                // geometry. Chip-to-chip overlap remains forbidden, while a
                // connector may pass underneath an unrelated label. Treating
                // labels as hard curve obstacles makes dense graphs
                // unsatisfiable because every route shares a small set of
                // Stage/Detector ports.
            }
            return true;
        }

        private float ResolveTransitionRouteBaseY(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            float outerRouteBaseY)
        {
            if (ClassifyTransitionRoute(transition) != TransitionRouteClass.SelfLoop
                || transition.SourceStageIndex < 0
                || transition.SourceStageIndex >= _activeStageCount)
            {
                return outerRouteBaseY;
            }
            int stageIndex = transition.SourceStageIndex;
            float localBottom = _stageY[stageIndex] + _stageHeight[stageIndex] + 12f;
            for (int detectorIndex = 0; detectorIndex < _activeDetectorCount; detectorIndex++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = _boundDetectors[detectorIndex];
                if (detector != null && detector.StageIndex == stageIndex)
                {
                    localBottom = Mathf.Max(
                        localBottom,
                        _detectorY[detectorIndex] + _detectorHeight[detectorIndex] + 12f);
                }
            }
            return ResolveTransitionRouteBaseYForClass(
                TransitionRouteClass.SelfLoop,
                outerRouteBaseY,
                _stageY[stageIndex] + _stageHeight[stageIndex],
                localBottom - 12f);
        }

        internal static float ResolveTransitionRouteBaseYForClass(
            TransitionRouteClass routeClass,
            float outerRouteBaseY,
            float sourceStageBottom,
            float sourceDetectorBottom)
        {
            return routeClass == TransitionRouteClass.SelfLoop
                ? Mathf.Max(sourceStageBottom, sourceDetectorBottom) + 12f
                : outerRouteBaseY;
        }

        private static Rect ExpandRouteObstacle(Rect obstacle, float expansion)
        {
            return Rect.MinMaxRect(
                obstacle.xMin - expansion,
                obstacle.yMin - expansion,
                obstacle.xMax + expansion,
                obstacle.yMax + expansion);
        }

        internal static Rect TrimSharedDetectorPortCorridor(
            Rect obstacle,
            float sourceX,
            float corridorWidth)
        {
            float clampedWidth = Mathf.Clamp(
                corridorWidth,
                0f,
                obstacle.width * 0.5f);
            if (Mathf.Abs(sourceX - obstacle.xMin) <= clampedWidth)
            {
                return Rect.MinMaxRect(
                    obstacle.xMin + clampedWidth,
                    obstacle.yMin,
                    obstacle.xMax,
                    obstacle.yMax);
            }
            if (Mathf.Abs(sourceX - obstacle.xMax) <= clampedWidth)
            {
                return Rect.MinMaxRect(
                    obstacle.xMin,
                    obstacle.yMin,
                    obstacle.xMax - clampedWidth,
                    obstacle.yMax);
            }
            return obstacle;
        }

        private static bool IsVisibleRouteObstacle(Rect obstacle)
        {
            return obstacle.xMax > CanvasLeft
                && obstacle.xMin < CanvasRight
                && obstacle.yMax > ContentTop
                && obstacle.yMin < ContentBottom;
        }

    }
}
