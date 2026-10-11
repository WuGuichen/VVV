using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private float ResolveTransitionChipX(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount,
            float preferredX,
            float chipWidth)
        {
            const float PortClearance = 18f;
            float clampedPreferred = Mathf.Clamp(
                preferredX,
                CanvasLeft,
                CanvasRight - chipWidth);
            if (IsTransitionChipClearOfPorts(
                transitions,
                transitionCount,
                clampedPreferred,
                chipWidth,
                PortClearance))
            {
                return clampedPreferred;
            }

            float bestX = float.NaN;
            float bestDistance = float.MaxValue;
            ConsiderTransitionChipX(
                transitions,
                transitionCount,
                CanvasLeft,
                chipWidth,
                PortClearance,
                clampedPreferred,
                ref bestX,
                ref bestDistance);
            ConsiderTransitionChipX(
                transitions,
                transitionCount,
                CanvasRight - chipWidth,
                chipWidth,
                PortClearance,
                clampedPreferred,
                ref bestX,
                ref bestDistance);
            for (int transitionIndex = 0; transitionIndex < transitionCount; transitionIndex++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                    transitions[transitionIndex];
                float sourcePortX = DetectorPortX(
                    transition.DetectorIndex,
                    transition.SourceStageIndex);
                float targetPortX = transition.EndsCard
                    ? CanvasRight - 12f
                    : StageInputPortX(transition.TargetStageIndex);
                ConsiderTransitionChipX(
                    transitions,
                    transitionCount,
                    sourcePortX + PortClearance,
                    chipWidth,
                    PortClearance,
                    clampedPreferred,
                    ref bestX,
                    ref bestDistance);
                ConsiderTransitionChipX(
                    transitions,
                    transitionCount,
                    sourcePortX - PortClearance - chipWidth,
                    chipWidth,
                    PortClearance,
                    clampedPreferred,
                    ref bestX,
                    ref bestDistance);
                ConsiderTransitionChipX(
                    transitions,
                    transitionCount,
                    targetPortX + PortClearance,
                    chipWidth,
                    PortClearance,
                    clampedPreferred,
                    ref bestX,
                    ref bestDistance);
                ConsiderTransitionChipX(
                    transitions,
                    transitionCount,
                    targetPortX - PortClearance - chipWidth,
                    chipWidth,
                    PortClearance,
                    clampedPreferred,
                    ref bestX,
                    ref bestDistance);
            }
            return float.IsNaN(bestX) ? clampedPreferred : bestX;
        }

        private void ConsiderTransitionChipX(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount,
            float candidateX,
            float chipWidth,
            float portClearance,
            float preferredX,
            ref float bestX,
            ref float bestDistance)
        {
            if (candidateX < CanvasLeft || candidateX + chipWidth > CanvasRight)
                return;
            if (!IsTransitionChipClearOfPorts(
                transitions,
                transitionCount,
                candidateX,
                chipWidth,
                portClearance))
            {
                return;
            }
            float distance = Mathf.Abs(candidateX - preferredX);
            if (distance < bestDistance)
            {
                bestX = candidateX;
                bestDistance = distance;
            }
        }

        private bool IsTransitionChipClearOfPorts(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount,
            float chipX,
            float chipWidth,
            float portClearance)
        {
            float minimumX = chipX - portClearance;
            float maximumX = chipX + chipWidth + portClearance;
            for (int transitionIndex = 0; transitionIndex < transitionCount; transitionIndex++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                    transitions[transitionIndex];
                float sourcePortX = DetectorPortX(
                    transition.DetectorIndex,
                    transition.SourceStageIndex);
                if (sourcePortX >= minimumX && sourcePortX <= maximumX)
                    return false;
                float targetPortX = transition.EndsCard
                    ? CanvasRight - 12f
                    : StageInputPortX(transition.TargetStageIndex);
                if (targetPortX >= minimumX && targetPortX <= maximumX)
                    return false;
            }
            return true;
        }

        internal static bool TransitionRouteIntersectsObstacle(
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            Rect obstacle)
        {
            return TransitionRouteIntersectsObstacle(
                sourceX,
                sourceY,
                targetX,
                targetY,
                routeY,
                sourceX,
                false,
                obstacle);
        }

        internal static bool TransitionRouteIntersectsObstacle(
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            float escapeX,
            Rect obstacle)
        {
            return TransitionRouteIntersectsObstacle(
                sourceX,
                sourceY,
                targetX,
                targetY,
                routeY,
                escapeX,
                true,
                obstacle);
        }

        internal static bool TransitionRouteIntersectsObstacle(
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            float escapeX,
            bool lateralSourceEscape,
            Rect obstacle)
        {
            Vector2 previous = EvaluateTransitionRoutePoint(
                sourceX,
                sourceY,
                targetX,
                targetY,
                routeY,
                escapeX,
                lateralSourceEscape,
                0f);
            const int RouteSampleCount = 24;
            for (int sample = 1; sample <= RouteSampleCount; sample++)
            {
                Vector2 current = EvaluateTransitionRoutePoint(
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    routeY,
                    escapeX,
                    lateralSourceEscape,
                    sample / (float)RouteSampleCount);
                Rect segmentBounds = Rect.MinMaxRect(
                    Math.Min(previous.x, current.x) - 1f,
                    Math.Min(previous.y, current.y) - 1f,
                    Math.Max(previous.x, current.x) + 1f,
                    Math.Max(previous.y, current.y) + 1f);
                if (segmentBounds.Overlaps(obstacle))
                    return true;
                previous = current;
            }
            return false;
        }

        private bool IsCardDetectorRoute(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            int detectorIndex = transition?.DetectorIndex ?? -1;
            return detectorIndex >= 0
                && detectorIndex < _activeDetectorCount
                && _boundDetectors[detectorIndex] != null
                && _boundDetectors[detectorIndex].CardScope;
        }

        private static float ResolveTransitionChipHeight(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            float chipWidth)
        {
            if (!transition.Selected)
                return 36f;
            int charactersPerLine = Math.Max(16, Mathf.FloorToInt((chipWidth - 24f) / 11f));
            int titleLength = transition.DisplayTitleText?.Length ?? 0;
            int titleLines = Math.Max(1, (titleLength + charactersPerLine - 1) / charactersPerLine);
            return Math.Max(136f, titleLines * 16f + 96f);
        }

        private static void LayoutTransitionChip(
            Fgui.GComponent chip,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            float chipWidth,
            float chipHeight)
        {
            SetObjectVisible(chip, "background", true);
            SetObjectVisible(chip, "stripe", true);
            SetObjectVisible(chip, "title", true);
            Fgui.GTextField title = chip.GetChild("title")?.asTextField;
            Fgui.GTextField detail = chip.GetChild("detail")?.asTextField;
            Fgui.GTextField status = chip.GetChild("status")?.asTextField;
            if (title == null || detail == null || status == null)
                return;
            title.align = Fgui.AlignType.Left;
            title.color = new Color32(214, 183, 240, 255);
            title.alpha = 1f;
            if (!transition.Selected)
            {
                float statusWidth = Mathf.Clamp(
                    (transition.StatusText?.Length ?? 0) * 7f + 16f,
                    72f,
                    chipWidth * 0.68f);
                detail.visible = true;
                status.visible = true;
                title.singleLine = true;
                title.SetXY(12f, 3f);
                title.SetSize(Math.Max(72f, chipWidth - statusWidth - 32f), 16f);
                detail.singleLine = true;
                detail.SetXY(12f, 18f);
                detail.SetSize(Math.Max(120f, chipWidth - 24f), 15f);
                status.SetXY(chipWidth - statusWidth - 12f, 3f);
                status.SetSize(statusWidth, 15f);
                return;
            }

            float titleHeight = 24f;
            title.singleLine = false;
            title.SetXY(12f, 4f);
            title.SetSize(chipWidth - 24f, titleHeight);
            detail.visible = true;
            detail.singleLine = true;
            detail.SetXY(12f, 28f);
            detail.SetSize(chipWidth - 24f, 14f);
            status.visible = false;
            float buttonY = chipHeight - 34f;
            SetObjectSize(chip, "btnPolicy", 160f, 28f);
            SetObjectXY(chip, "btnPolicy", 12f, buttonY);
            SetObjectSize(chip, "btnTargetPrevious", 96f, 28f);
            SetObjectXY(chip, "btnTargetPrevious", 180f, buttonY);
            SetObjectSize(chip, "btnTargetNext", 96f, 28f);
            SetObjectXY(chip, "btnTargetNext", 284f, buttonY);
            SetObjectSize(chip, "btnDelete", 86f, 28f);
            SetObjectXY(chip, "btnDelete", 890f, buttonY);
        }

    }
}
