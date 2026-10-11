using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private int ResolveBehaviorLogicEndRouteLane(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount,
            int transitionIndex,
            float currentTargetX)
        {
            CharacterTestRuntimeAiLabLogicTransitionViewModel current =
                transitionIndex >= 0 && transitionIndex < transitions.Length
                    ? transitions[transitionIndex]
                    : null;
            string currentStableId = current?.TransitionId ?? string.Empty;
            int lane = 0;
            int count = Math.Min(transitionCount, transitions.Length);
            for (int index = 0; index < count; index++)
            {
                if (index == transitionIndex)
                    continue;
                CharacterTestRuntimeAiLabLogicTransitionViewModel other = transitions[index];
                if (other == null || ClassifyTransitionRoute(other) != TransitionRouteClass.End)
                    continue;

                float otherTargetX = ResolveBehaviorLogicEndTargetX(other);
                bool precedesCurrent = otherTargetX < currentTargetX - 0.5f;
                if (Mathf.Abs(otherTargetX - currentTargetX) <= 0.5f)
                {
                    int stableComparison = string.CompareOrdinal(
                        other.TransitionId ?? string.Empty,
                        currentStableId);
                    precedesCurrent = stableComparison < 0
                        || (stableComparison == 0 && index < transitionIndex);
                }
                if (precedesCurrent)
                    lane++;
            }
            return lane;
        }

        private float ResolveBehaviorLogicEndTargetX(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            bool replan = IsBehaviorLogicReplan(transition);
            Fgui.GObject endpoint = _root?.GetChild(
                replan ? "logicReplanEndpoint" : "logicEndEndpoint");
            if (endpoint != null)
                return endpoint.x + endpoint.width * 0.5f;

            float fallbackRight = CanvasLeft + (_contentLayer?.width ?? 1f) - 72f;
            return replan ? fallbackRight - 127f : fallbackRight;
        }

        private static int CountBehaviorLogicTransitionsForDetector(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount,
            int detectorIndex)
        {
            int count = 0;
            for (int i = 0; i < transitionCount; i++)
            {
                if (transitions[i]?.DetectorIndex == detectorIndex)
                    count++;
            }
            return count;
        }

        private static int BehaviorLogicTransitionOrdinalForDetector(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionIndex,
            int detectorIndex)
        {
            int ordinal = 0;
            for (int i = 0; i < transitionIndex && i < transitions.Length; i++)
            {
                if (transitions[i]?.DetectorIndex == detectorIndex)
                    ordinal++;
            }
            return ordinal;
        }

        private static bool IsBehaviorLogicReplan(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            return transition != null
                && (string.Equals(
                        transition.EndReason,
                        "Replan",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        transition.Label,
                        "Replan",
                        StringComparison.OrdinalIgnoreCase)
                    || ContainsCompactSemantic(transition.EndReason, "重新决策")
                    || ContainsCompactSemantic(transition.Label, "重新决策"));
        }

        private int DrawBehaviorLogicForwardArrow(
            int connectorIndex,
            int flowIndex,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            float middleX = (sourceX + targetX) * 0.5f;
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX,
                sourceY,
                middleX,
                sourceY,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                middleX,
                sourceY,
                middleX,
                targetY,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                middleX,
                targetY,
                targetX,
                targetY,
                transition);
            return DrawBehaviorLogicArrowHead(
                connectorIndex,
                flowIndex,
                targetX,
                targetY,
                1f,
                0f,
                transition);
        }

        private int DrawBehaviorLogicCardDetectorArrow(
            int connectorIndex,
            int flowIndex,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition.EndsCard)
            {
                return DrawBehaviorLogicOrthogonalArrow(
                    connectorIndex,
                    flowIndex,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    ContentTop + 104f,
                    transition);
            }
            float routeY = ContentTop + 104f;
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX,
                sourceY,
                sourceX,
                routeY,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX,
                routeY,
                targetX,
                routeY,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                targetX,
                routeY,
                targetX,
                targetY,
                transition);
            return DrawBehaviorLogicArrowHead(
                connectorIndex,
                flowIndex,
                targetX,
                targetY,
                0f,
                1f,
                transition);
        }

        private int DrawBehaviorLogicOrthogonalArrow(
            int connectorIndex,
            int flowIndex,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float routeY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX,
                sourceY,
                sourceX,
                routeY,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX,
                routeY,
                targetX,
                routeY,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                targetX,
                routeY,
                targetX,
                targetY,
                transition);
            float verticalDirection = targetY >= routeY ? 1f : -1f;
            return DrawBehaviorLogicArrowHead(
                connectorIndex,
                flowIndex,
                targetX,
                targetY,
                0f,
                verticalDirection,
                transition);
        }

        private int DrawBehaviorLogicRoundedOrthogonalArrow(
            int connectorIndex,
            int flowIndex,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            float escapeX,
            float routeY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            Vector2 source = new Vector2(sourceX, sourceY);
            Vector2 firstCorner = new Vector2(escapeX, sourceY);
            Vector2 secondCorner = new Vector2(escapeX, routeY);
            Vector2 thirdCorner = new Vector2(targetX, routeY);
            Vector2 target = new Vector2(targetX, targetY);

            float firstRadius = ResolveBehaviorLogicCornerRadius(
                Vector2.Distance(source, firstCorner),
                Vector2.Distance(firstCorner, secondCorner));
            float secondRadius = ResolveBehaviorLogicCornerRadius(
                Vector2.Distance(firstCorner, secondCorner),
                Vector2.Distance(secondCorner, thirdCorner));
            float thirdRadius = ResolveBehaviorLogicCornerRadius(
                Vector2.Distance(secondCorner, thirdCorner),
                Vector2.Distance(thirdCorner, target));

            Vector2 firstIncoming = MoveTowardsCorner(
                firstCorner,
                source,
                firstRadius);
            Vector2 firstOutgoing = MoveTowardsCorner(
                firstCorner,
                secondCorner,
                firstRadius);
            Vector2 secondIncoming = MoveTowardsCorner(
                secondCorner,
                firstCorner,
                secondRadius);
            Vector2 secondOutgoing = MoveTowardsCorner(
                secondCorner,
                thirdCorner,
                secondRadius);
            Vector2 thirdIncoming = MoveTowardsCorner(
                thirdCorner,
                secondCorner,
                thirdRadius);
            Vector2 thirdOutgoing = MoveTowardsCorner(
                thirdCorner,
                target,
                thirdRadius);

            int sourceLeadStart = connectorIndex;
            connectorIndex = DrawBehaviorLogicSegmentIfLong(
                connectorIndex,
                source,
                firstIncoming,
                transition);
            connectorIndex = DrawBehaviorLogicQuadraticCorner(
                connectorIndex,
                firstIncoming,
                firstCorner,
                firstOutgoing,
                transition);
            connectorIndex = DrawBehaviorLogicSegmentIfLong(
                connectorIndex,
                firstOutgoing,
                secondIncoming,
                transition);
            connectorIndex = DrawBehaviorLogicQuadraticCorner(
                connectorIndex,
                secondIncoming,
                secondCorner,
                secondOutgoing,
                transition);
            SetBehaviorLogicConnectorSortingOrder(
                sourceLeadStart,
                connectorIndex,
                BehaviorLogicSourceConnectorSortingOrder);
            connectorIndex = DrawBehaviorLogicSegmentIfLong(
                connectorIndex,
                secondOutgoing,
                thirdIncoming,
                transition);
            connectorIndex = DrawBehaviorLogicQuadraticCorner(
                connectorIndex,
                thirdIncoming,
                thirdCorner,
                thirdOutgoing,
                transition);
            connectorIndex = DrawBehaviorLogicSegmentIfLong(
                connectorIndex,
                thirdOutgoing,
                target,
                transition);

            float verticalDirection = targetY >= routeY ? 1f : -1f;
            return DrawBehaviorLogicArrowHead(
                connectorIndex,
                flowIndex,
                targetX,
                targetY,
                0f,
                verticalDirection,
                transition);
        }

        private void SetBehaviorLogicConnectorSortingOrder(
            int start,
            int endExclusive,
            int sortingOrder)
        {
            int clampedStart = Math.Max(0, start);
            int clampedEnd = Math.Min(_connectorPool.Count, endExclusive);
            for (int index = clampedStart; index < clampedEnd; index++)
                _connectorPool[index].sortingOrder = sortingOrder;
        }

        private int DrawBehaviorLogicQuadraticCorner(
            int connectorIndex,
            Vector2 start,
            Vector2 corner,
            Vector2 end,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            const int SegmentCount = 5;
            Vector2 previous = start;
            for (int segment = 1; segment <= SegmentCount; segment++)
            {
                float t = segment / (float)SegmentCount;
                float inverse = 1f - t;
                Vector2 point = inverse * inverse * start
                    + 2f * inverse * t * corner
                    + t * t * end;
                connectorIndex = DrawBehaviorLogicSegmentIfLong(
                    connectorIndex,
                    previous,
                    point,
                    transition);
                previous = point;
            }
            return connectorIndex;
        }

        private int DrawBehaviorLogicSegmentIfLong(
            int connectorIndex,
            Vector2 source,
            Vector2 target,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if ((target - source).sqrMagnitude <= 0.25f)
                return connectorIndex;
            return DrawBehaviorLogicSegment(
                connectorIndex,
                source.x,
                source.y,
                target.x,
                target.y,
                transition);
        }

        private static float ResolveBehaviorLogicCornerRadius(
            float incomingLength,
            float outgoingLength)
        {
            return Mathf.Min(
                BehaviorLogicEndRouteCornerRadius,
                incomingLength * 0.5f,
                outgoingLength * 0.5f);
        }

        private static Vector2 MoveTowardsCorner(
            Vector2 corner,
            Vector2 adjacent,
            float distance)
        {
            Vector2 direction = adjacent - corner;
            if (direction.sqrMagnitude <= 0.001f)
                return corner;
            return corner + direction.normalized * distance;
        }

        private int DrawBehaviorLogicSelfLoop(
            int connectorIndex,
            int flowIndex,
            float sourceX,
            float sourceY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            const float loopWidth = 26f;
            const float loopHeight = 28f;
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX,
                sourceY,
                sourceX + loopWidth,
                sourceY,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX + loopWidth,
                sourceY,
                sourceX + loopWidth,
                sourceY + loopHeight,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX + loopWidth,
                sourceY + loopHeight,
                sourceX - 16f,
                sourceY + loopHeight,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX - 16f,
                sourceY + loopHeight,
                sourceX - 16f,
                sourceY + 8f,
                transition);
            connectorIndex = DrawBehaviorLogicSegment(
                connectorIndex,
                sourceX - 16f,
                sourceY + 8f,
                sourceX - 2f,
                sourceY + 8f,
                transition);
            return DrawBehaviorLogicArrowHead(
                connectorIndex,
                flowIndex,
                sourceX - 2f,
                sourceY + 8f,
                1f,
                0f,
                transition);
        }

        private int DrawBehaviorLogicSegment(
            int connectorIndex,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            DrawConnector(
                GetConnector(connectorIndex++),
                sourceX - CanvasLeft,
                sourceY - ContentTop,
                targetX - CanvasLeft,
                targetY - ContentTop,
                transition);
            return connectorIndex;
        }

        private int DrawBehaviorLogicArrowHead(
            int connectorIndex,
            int flowIndex,
            float targetX,
            float targetY,
            float directionX,
            float directionY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            ConfigureBehaviorLogicFlowArrow(
                flowIndex,
                targetX - CanvasLeft,
                targetY - ContentTop,
                directionX,
                directionY,
                transition);
            return connectorIndex;
        }

        private string ResolveCompactBehaviorLogicTransitionTitle(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition == null || transition.Invalid)
                return "待修复";

            if (ContainsCompactSemantic(transition.EndReason, "重新决策")
                || ContainsCompactSemantic(transition.Label, "重新决策")
                || string.Equals(transition.EndReason, "Replan", StringComparison.OrdinalIgnoreCase)
                || string.Equals(transition.Label, "Replan", StringComparison.OrdinalIgnoreCase))
            {
                return "重新决策";
            }
            if (ContainsCompactSemantic(transition.EndReason, "继续")
                || ContainsCompactSemantic(transition.Label, "继续"))
            {
                return "下一行为";
            }
            if (transition.EndsCard
                && (string.Equals(
                        transition.EndReason,
                        "Completed",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        transition.Label,
                        "结束行为卡",
                        StringComparison.Ordinal)))
            {
                return "结束行为卡";
            }
            if (!transition.EndsCard)
            {
                string target = string.IsNullOrWhiteSpace(transition.TargetText)
                    ? "下一阶段"
                    : transition.TargetText;
                return target;
            }
            if (string.IsNullOrWhiteSpace(transition.EndReason))
                return "结束";
            return CharacterTestRuntimeAiPlannerDevPresentationText
                .BuildBehaviorTransitionLabel(transition.EndReason);
        }

        private static bool ContainsCompactSemantic(string value, string semantic)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(semantic, StringComparison.Ordinal) >= 0;
        }

        private static void ConfigureCompactBehaviorLogicTransitionChip(
            Fgui.GComponent chip,
            float width,
            float height,
            bool dense)
        {
            SetObjectVisible(chip, "background", false);
            SetObjectVisible(chip, "stripe", false);
            SetObjectVisible(chip, "title", true);
            float arrowWidth = dense ? 30f : 52f;
            float titleX = dense ? 34f : 64f;
            SetObjectSize(chip, "title", Math.Max(24f, width - titleX), height - 6f);
            SetObjectXY(chip, "title", titleX, 3f);
            SetObjectVisible(chip, "detail", true);
            SetObjectSize(chip, "detail", arrowWidth, height);
            SetObjectXY(chip, "detail", 0f, 0f);
            SetObjectVisible(chip, "status", false);
            Fgui.GTextField title = chip.GetChild("title")?.asTextField;
            if (title != null)
                title.align = Fgui.AlignType.Left;
            Fgui.GTextField arrow = chip.GetChild("detail")?.asTextField;
            if (arrow != null)
            {
                arrow.align = Fgui.AlignType.Center;
                arrow.color = new Color32(184, 220, 231, 255);
                arrow.alpha = 0.96f;
            }
            SetTimelineTextFontSize(
                title,
                dense ? width >= 140f ? 20 : width >= 90f ? 17 : 14 : 30);
            SetTimelineTextFontSize(arrow, dense ? 22 : 34);
        }

        private void ApplyCompactBehaviorLogicTransitionTone(
            Fgui.GComponent chip,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            Color32 tone = (Color32)TransitionColor(transition);
            Fgui.GTextField title = chip.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = tone;
                title.alpha = transition.Committed
                    || transition.Pending
                    || transition.Selected
                    || transition.Invalid
                        ? 1f
                        : 0.88f;
            }
            Fgui.GTextField arrow = chip.GetChild("detail")?.asTextField;
            if (arrow != null)
            {
                arrow.color = new Color32(184, 220, 231, 255);
                arrow.alpha = 0.96f;
            }
        }

        private void HideCompactBehaviorLogicPorts()
        {
            for (int i = 0; i < _activeStageCount && i < _stagePool.Count; i++)
            {
                if (_stagePool[i].InputPort != null)
                    _stagePool[i].InputPort.visible = false;
            }
            for (int i = 0; i < _activeDetectorCount && i < _detectorPool.Count; i++)
            {
                if (_detectorPool[i].OutputPort != null)
                    _detectorPool[i].OutputPort.visible = false;
            }
            if (_endPort != null)
                _endPort.visible = false;
        }

        private static string ResolveBehaviorLogicTransitionTitle(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition.Invalid)
                return "出口待修复";
            if (transition.EndsCard)
            {
                if (string.Equals(
                        transition.EndReason,
                        "Completed",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        transition.Label,
                        "结束行为卡",
                        StringComparison.Ordinal))
                {
                    return "结束行为卡";
                }
                return string.IsNullOrWhiteSpace(transition.EndReason)
                    ? "结束行为卡"
                    : CharacterTestRuntimeAiPlannerDevPresentationText
                        .BuildBehaviorTransitionLabel(transition.EndReason);
            }
            if (!string.IsNullOrWhiteSpace(transition.Label))
                return CharacterTestRuntimeAiPlannerDevPresentationText
                    .BuildBehaviorTransitionLabel(transition.Label);
            return "进入下一阶段";
        }

        private static string ResolveBehaviorLogicTransitionTarget(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition.EndsCard)
                return "行为卡结束";
            return string.IsNullOrWhiteSpace(transition.TargetText)
                ? "目标阶段"
                : transition.TargetText;
        }

        private static void ConfigureBehaviorLogicTransitionChip(
            Fgui.GComponent chip,
            float width,
            float height)
        {
            SetObjectVisible(chip, "background", true);
            SetObjectVisible(chip, "stripe", true);
            SetObjectVisible(chip, "title", true);
            SetObjectSize(chip, "background", width, height);
            SetObjectSize(chip, "stripe", 6f, height);
            SetObjectXY(chip, "title", 16f, 5f);
            SetObjectSize(chip, "title", width - 32f, 24f);
            SetObjectXY(chip, "detail", 16f, 29f);
            SetObjectSize(chip, "detail", width - 32f, 18f);
            SetObjectVisible(chip, "detail", true);
            SetObjectVisible(chip, "status", false);
            Fgui.GTextField title = chip.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.align = Fgui.AlignType.Left;
                title.color = new Color32(214, 183, 240, 255);
                title.alpha = 1f;
            }
            SetTimelineTextFontSize(title, 18);
            SetTimelineTextFontSize(chip.GetChild("detail")?.asTextField, 14);
        }

    }
}
