using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void BindTransitions(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            if (_behaviorLogicMode)
            {
                BindBehaviorLogicTransitions(transitions, transitionCount);
                return;
            }

            HideTransitionFlowSlots(0);

            int connectorUsed = 0;
            // Resolve the route baseline in unscrolled content space, then apply
            // the viewport offset once. Keeping the old visible-space minimum
            // pinned selected transition editors to the bottom edge after the
            // stages had scrolled away, so the editor could never be brought
            // fully into view.
            float verticalOffset = Math.Max(0f, _currentVerticalOffset);
            float unscrolledRouteBaseY = ContentTop + 42f;
            for (int i = 0; i < _activeStageCount; i++)
            {
                unscrolledRouteBaseY = Mathf.Max(
                    unscrolledRouteBaseY,
                    _stageY[i] + verticalOffset + _stageHeight[i] + 38f);
            }
            for (int i = 0; i < _detectorPool.Count; i++)
            {
                DetectorSlot detector = _detectorPool[i];
                if (detector?.Visual?.visible == true)
                {
                    unscrolledRouteBaseY = Mathf.Max(
                        unscrolledRouteBaseY,
                        detector.Visual.y
                        + ContentTop
                        + verticalOffset
                        + detector.Visual.height
                        + 38f);
                }
            }
            float routeBaseY = ResolveScrolledRouteBaseY(
                unscrolledRouteBaseY,
                verticalOffset);
            _routeBaseY = routeBaseY;
            FillStableTransitionOrder(transitions, transitionCount);
            float maximumRouteBottom = routeBaseY;
            for (int orderIndex = 0; orderIndex < transitionCount; orderIndex++)
            {
                int transitionIndex = _transitionOrder[orderIndex];
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = transitions[transitionIndex];
                float sourceX = DetectorPortX(transition.DetectorIndex, transition.SourceStageIndex);
                float sourceY = DetectorPortY(transition.DetectorIndex, transition.SourceStageIndex);
                float targetX = transition.EndsCard
                    ? CanvasRight - 12f
                    : StageInputPortX(transition.TargetStageIndex);
                float targetY = transition.EndsCard
                    ? routeBaseY + 24f
                    : StageInputPortY(transition.TargetStageIndex);
                TransitionSlot slot = GetTransitionSlot(orderIndex);
                slot.StableId = transition.TransitionId ?? string.Empty;
                slot.RuntimeFrame = transition.RuntimeFrame;
                slot.ViewModel = transition;
                Fgui.GComponent chip = slot.Visual;
                chip.visible = true;
                chip.sortingOrder = TransitionChipSortingOrder;
                float chipWidth = transition.Selected
                    ? 1000f
                    : transition.RuntimeFrame >= 0
                        ? 360f
                        : 260f;
                float chipHeight = ResolveTransitionChipHeight(transition, chipWidth);
                float chipX = Mathf.Clamp(
                    (sourceX + targetX) * 0.5f - chipWidth * 0.5f,
                    CanvasLeft,
                    CanvasRight - chipWidth);
                chipX = ResolveTransitionChipX(
                    transitions,
                    transitionCount,
                    chipX,
                    chipWidth);
                float routeStartX = Mathf.Min(sourceX, targetX);
                float routeEndX = Mathf.Max(sourceX, targetX);
                if (Mathf.Abs(routeEndX - routeStartX) < 96f)
                {
                    routeStartX = sourceX - 96f;
                    routeEndX = sourceX + 96f;
                }
                routeStartX = Mathf.Min(routeStartX, chipX) - TransitionRouteHorizontalPadding;
                routeEndX = Mathf.Max(routeEndX, chipX + chipWidth) + TransitionRouteHorizontalPadding;
                routeStartX = Mathf.Max(CanvasLeft, routeStartX);
                routeEndX = Mathf.Min(CanvasRight, routeEndX);
                if (ClassifyTransitionRoute(transition) != TransitionRouteClass.SelfLoop)
                {
                    // A route may escape through either outer side gutter when its direct
                    // descent is blocked. Reserve the full horizontal interval so two
                    // routes that choose the same gutter cannot be assigned the same lane.
                    routeStartX = CanvasLeft;
                    routeEndX = CanvasRight;
                }
                int laneSpan = Math.Max(
                    1,
                    Mathf.CeilToInt(
                        (chipHeight + TransitionRouteChipClearance + 6f)
                        / TransitionRouteLaneHeight));
                float transitionRouteBaseY = ResolveTransitionRouteBaseY(transition, routeBaseY);
                int routeLane = ResolveTransitionRouteLane(
                    orderIndex,
                    transitionCount,
                    transition,
                    transitionRouteBaseY,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    chipX,
                    chipWidth,
                    chipHeight,
                    routeStartX,
                    routeEndX,
                    laneSpan,
                    out float routeEscapeX);
                float routeTop = transitionRouteBaseY + routeLane * TransitionRouteLaneHeight;
                float routeY = routeTop + chipHeight + TransitionRouteChipClearance;
                maximumRouteBottom = Mathf.Max(
                    maximumRouteBottom,
                    routeTop + laneSpan * TransitionRouteLaneHeight);
                _transitionChipX[orderIndex] = chipX;
                _transitionChipWidth[orderIndex] = chipWidth;
                _transitionChipHeight[orderIndex] = chipHeight;
                _transitionRouteTopY[orderIndex] = routeTop;
                _transitionRouteY[orderIndex] = routeY;
                _transitionRouteStartX[orderIndex] = routeStartX;
                _transitionRouteEndX[orderIndex] = routeEndX;
                _transitionRouteLane[orderIndex] = routeLane;
                _transitionLaneSpan[orderIndex] = laneSpan;
                float chipLocalY = routeTop - ContentTop;
                float chipDisplayTop = ContentTop + chipLocalY;
                _transitionConnectorStart[orderIndex] = connectorUsed;
                connectorUsed = DrawTransitionCurve(
                    connectorUsed,
                    sourceX - CanvasLeft,
                    sourceY - ContentTop,
                    targetX - CanvasLeft,
                    targetY - ContentTop,
                    routeY - ContentTop,
                    routeEscapeX - CanvasLeft,
                    transition);
                float chipCenterX = chipX + chipWidth * 0.5f;
                DrawConnector(
                    GetConnector(connectorUsed++),
                    chipCenterX - CanvasLeft,
                    chipDisplayTop + chipHeight - ContentTop,
                    chipCenterX - CanvasLeft,
                    routeY - ContentTop,
                    transition);
                _transitionConnectorCount[orderIndex] = connectorUsed
                    - _transitionConnectorStart[orderIndex];
                chip.SetSize(chipWidth, chipHeight);
                chip.SetXY(chipX - CanvasLeft, chipLocalY);
                chip.rootContainer.clipRect = new Rect(0f, 0f, chipWidth, chipHeight);
                SetText(chip, "title", transition.DisplayTitleText);
                SetText(
                    chip,
                    "detail",
                    transition.Selected
                        ? "名称 · 优先级 · 来源 Stage · 目标 Stage · 策略与操作"
                        : transition.DetailText);
                SetText(chip, "status", transition.StatusText);
                LayoutTransitionChip(chip, transition, chipWidth, chipHeight);
                ColorTransition(chip, transition);
                ConfigureTransitionButtons(chip, transition);
                BindTransitionEditorFields(slot, transition);
            }
            if (transitionCount > 0)
            {
                float unscrolledBottom = maximumRouteBottom
                    + TransitionRouteChipClearance
                    + Math.Max(0f, _currentVerticalOffset);
                _contentExtentHeight = Mathf.Max(_contentExtentHeight, unscrolledBottom - ContentTop);
            }
            HideTransitionSlots(transitionCount);
            HideConnectors(connectorUsed);
            LayoutTransitionPorts();
        }

        private void BindBehaviorLogicTransitions(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            if (_compactBehaviorLogicCard)
            {
                BindCompactBehaviorLogicTransitions(transitions, transitionCount);
                return;
            }

            HideTransitionFlowSlots(0);

            FillStableTransitionOrder(transitions, transitionCount);
            int connectorUsed = 0;
            int previousSourceStage = int.MinValue;
            int sourceRow = 0;
            float maximumBottom = ContentTop;
            for (int orderIndex = 0; orderIndex < transitionCount; orderIndex++)
            {
                int transitionIndex = _transitionOrder[orderIndex];
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                    transitions[transitionIndex];
                int sourceStage = transition.SourceStageIndex >= 0
                    && transition.SourceStageIndex < _activeStageCount
                        ? transition.SourceStageIndex
                        : 0;
                if (sourceStage != previousSourceStage)
                {
                    previousSourceStage = sourceStage;
                    sourceRow = 0;
                }
                else
                {
                    sourceRow++;
                }

                float sourceCardX = sourceStage < _activeStageCount
                    ? _stageX[sourceStage]
                    : CanvasLeft + 52f;
                float sourceCardWidth = sourceStage < _activeStageCount
                    ? _stageWidth[sourceStage]
                    : 400f;
                float sourceBottom = sourceStage < _activeStageCount
                    ? _stageY[sourceStage] + _stageHeight[sourceStage]
                    : ContentTop + 288f;
                const float chipWidth = 220f;
                const float chipHeight = 52f;
                float chipX = sourceCardX + 24f + sourceRow * (chipWidth + 16f);
                float chipY = sourceBottom + 28f;
                if (chipX + chipWidth > sourceCardX + sourceCardWidth - 24f)
                {
                    chipX = sourceCardX + 24f;
                    chipY += sourceRow * (chipHeight + 12f);
                }
                chipX = Mathf.Clamp(chipX, CanvasLeft + 12f, CanvasRight - chipWidth - 12f);

                TransitionSlot slot = GetTransitionSlot(orderIndex);
                slot.StableId = transition.TransitionId ?? string.Empty;
                slot.RuntimeFrame = transition.RuntimeFrame;
                slot.ViewModel = transition;
                Fgui.GComponent chip = slot.Visual;
                chip.visible = true;
                chip.sortingOrder = TransitionChipSortingOrder;
                chip.SetXY(chipX - CanvasLeft, chipY - ContentTop);
                chip.SetSize(chipWidth, chipHeight);
                chip.rootContainer.clipRect = new Rect(0f, 0f, chipWidth, chipHeight);
                SetText(chip, "title", ResolveBehaviorLogicTransitionTitle(transition));
                SetText(chip, "detail", ResolveBehaviorLogicTransitionTarget(transition));
                SetText(chip, "status", transition.Invalid ? "需要修复" : string.Empty);
                ConfigureBehaviorLogicTransitionChip(chip, chipWidth, chipHeight);
                ColorTransition(chip, transition);
                ConfigureTransitionButtons(chip, transition);
                BindTransitionEditorFields(slot, transition);

                float sourceX = sourceCardX + sourceCardWidth * 0.5f;
                float sourceY = sourceBottom;
                float chipCenterX = chipX + chipWidth * 0.5f;
                _transitionConnectorStart[orderIndex] = connectorUsed;
                DrawConnector(
                    GetConnector(connectorUsed++),
                    sourceX - CanvasLeft,
                    sourceY - ContentTop,
                    chipCenterX - CanvasLeft,
                    chipY - ContentTop,
                    transition);
                if (!transition.EndsCard
                    && transition.TargetStageIndex >= 0
                    && transition.TargetStageIndex < _activeStageCount)
                {
                    DrawConnector(
                        GetConnector(connectorUsed++),
                        chipX + chipWidth - CanvasLeft,
                        chipY + chipHeight * 0.5f - ContentTop,
                        _stageX[transition.TargetStageIndex] - CanvasLeft,
                        _stageY[transition.TargetStageIndex] + 30f - ContentTop,
                        transition);
                }
                _transitionConnectorCount[orderIndex] = connectorUsed
                    - _transitionConnectorStart[orderIndex];
                _transitionChipX[orderIndex] = chipX;
                _transitionChipWidth[orderIndex] = chipWidth;
                _transitionChipHeight[orderIndex] = chipHeight;
                maximumBottom = Mathf.Max(maximumBottom, chipY + chipHeight);
            }
            _contentExtentHeight = Mathf.Max(ContentHeight, maximumBottom - ContentTop + 16f);
            HideTransitionSlots(transitionCount);
            HideConnectors(connectorUsed);
            LayoutTransitionPorts();
        }

        private void BindCompactBehaviorLogicTransitions(
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            FillStableTransitionOrder(transitions, transitionCount);
            int connectorUsed = 0;
            float maximumBottom = ContentTop + BehaviorLogicBottomRouteTop;
            for (int orderIndex = 0; orderIndex < transitionCount; orderIndex++)
            {
                int transitionIndex = _transitionOrder[orderIndex];
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                    transitions[transitionIndex];
                int detectorIndex = transition.DetectorIndex;
                bool detectorAvailable = detectorIndex >= 0
                    && detectorIndex < _activeDetectorCount
                    && detectorIndex < _detectorPool.Count
                    && _detectorPool[detectorIndex].Visual?.visible == true;

                TransitionSlot slot = GetTransitionSlot(orderIndex);
                slot.StableId = transition.TransitionId ?? string.Empty;
                slot.RuntimeFrame = transition.RuntimeFrame;
                slot.ViewModel = transition;
                Fgui.GComponent chip = slot.Visual;
                if (!detectorAvailable)
                {
                    chip.visible = false;
                    _transitionConnectorStart[orderIndex] = connectorUsed;
                    _transitionConnectorCount[orderIndex] = 0;
                    HideTransitionFlowSlot(orderIndex);
                    continue;
                }

                float detectorX = _detectorX[detectorIndex];
                float detectorY = _detectorY[detectorIndex];
                float detectorWidth = _detectorWidth[detectorIndex];
                float detectorHeight = _detectorHeight[detectorIndex];
                int detectorTransitionCount = CountBehaviorLogicTransitionsForDetector(
                    transitions,
                    transitionCount,
                    detectorIndex);
                int detectorTransitionOrdinal = BehaviorLogicTransitionOrdinalForDetector(
                    transitions,
                    transitionIndex,
                    detectorIndex);
                float chipX = detectorX + Math.Min(142f, detectorWidth * 0.48f);
                float chipWidth = Math.Max(54f, detectorWidth - (chipX - detectorX) - 40f);
                float chipHeight = detectorHeight;
                chip.visible = true;
                chip.sortingOrder = TransitionChipSortingOrder;
                chip.SetXY(chipX - CanvasLeft, detectorY - ContentTop);
                chip.SetSize(chipWidth, chipHeight);
                chip.rootContainer.clipRect = new Rect(0f, 0f, chipWidth, chipHeight);
                string transitionTitle = ResolveCompactBehaviorLogicTransitionTitle(transition);
                if (detectorTransitionCount > 1)
                {
                    transitionTitle += " · +"
                        + (detectorTransitionCount - 1).ToString();
                }
                SetText(chip, "title", transitionTitle);
                SetText(chip, "detail", "→");
                SetText(chip, "status", string.Empty);
                ColorTransition(chip, transition);
                ConfigureTransitionButtons(chip, transition);
                BindTransitionEditorFields(slot, transition);
                SetTransitionEditorVisibility(chip, false, transition.EndsCard);
                ConfigureCompactBehaviorLogicTransitionChip(
                    chip,
                    chipWidth,
                    chipHeight,
                    true);
                ApplyCompactBehaviorLogicTransitionTone(chip, transition);
                chip.visible = detectorTransitionOrdinal == 0;

                float sourceX = detectorX + detectorWidth;
                float sourceY = detectorY + detectorHeight * 0.5f;
                TransitionRouteClass routeClass = ClassifyTransitionRoute(transition);
                float targetX;
                float targetY;
                if (transition.EndsCard)
                {
                    string endpointName = IsBehaviorLogicReplan(transition)
                        ? "logicReplanEndpoint"
                        : "logicEndEndpoint";
                    Fgui.GObject endpoint = _root?.GetChild(endpointName);
                    targetX = endpoint != null
                        ? endpoint.x + endpoint.width * 0.5f
                        : CanvasLeft + (_contentLayer?.width ?? 1f) - 72f;
                    float endpointBottom = endpoint != null
                        ? endpoint.y + endpoint.height
                        : BehaviorLogicGlobalDetectorTop + 38f;
                    targetY = ContentTop
                        + endpointBottom
                        + BehaviorLogicEndpointDockGap;
                }
                else if (transition.TargetStageIndex >= 0
                    && transition.TargetStageIndex < _activeStageCount)
                {
                    int targetStage = transition.TargetStageIndex;
                    targetX = routeClass == TransitionRouteClass.BackEdge
                        ? _stageX[targetStage] + _stageWidth[targetStage] * 0.5f
                        : _stageX[targetStage] + 2f;
                    targetY = routeClass == TransitionRouteClass.BackEdge
                        ? _stageY[targetStage] + _stageHeight[targetStage]
                        : _stageY[targetStage] + BehaviorLogicDetectorTop + DetectorBandHeight * 0.5f;
                }
                else
                {
                    targetX = sourceX + 24f;
                    targetY = sourceY;
                }

                _transitionConnectorStart[orderIndex] = connectorUsed;
                switch (routeClass)
                {
                    case TransitionRouteClass.SelfLoop:
                        connectorUsed = DrawBehaviorLogicSelfLoop(
                            connectorUsed,
                            orderIndex,
                            sourceX,
                            sourceY,
                            transition);
                        break;
                    case TransitionRouteClass.BackEdge:
                        float backRouteY = ContentTop
                            + BehaviorLogicBottomRouteTop
                            + detectorTransitionOrdinal * 10f;
                        connectorUsed = DrawBehaviorLogicOrthogonalArrow(
                            connectorUsed,
                            orderIndex,
                            sourceX,
                            sourceY,
                            targetX,
                            targetY,
                            backRouteY,
                            transition);
                        maximumBottom = Math.Max(maximumBottom, backRouteY + 8f);
                        break;
                    case TransitionRouteClass.End:
                        int endRouteLane = ResolveBehaviorLogicEndRouteLane(
                            transitions,
                            transitionCount,
                            transitionIndex,
                            targetX);
                        float endRouteY = ContentTop
                            + BehaviorLogicEndRouteTop
                            + endRouteLane * BehaviorLogicEndRouteLaneGap;
                        float endRouteEscapeX = sourceX
                            + BehaviorLogicEndRouteEscape
                            + endRouteLane * BehaviorLogicEndRouteEscapeGap;
                        connectorUsed = DrawBehaviorLogicRoundedOrthogonalArrow(
                            connectorUsed,
                            orderIndex,
                            sourceX,
                            sourceY,
                            targetX,
                            targetY,
                            endRouteEscapeX,
                            endRouteY,
                            transition);
                        break;
                    case TransitionRouteClass.CardDetector:
                        connectorUsed = DrawBehaviorLogicCardDetectorArrow(
                            connectorUsed,
                            orderIndex,
                            sourceX,
                            sourceY,
                            targetX,
                            targetY,
                            transition);
                        break;
                    default:
                        connectorUsed = DrawBehaviorLogicForwardArrow(
                            connectorUsed,
                            orderIndex,
                            sourceX,
                            sourceY,
                            targetX,
                            targetY,
                            transition);
                        break;
                }
                _transitionConnectorCount[orderIndex] = connectorUsed
                    - _transitionConnectorStart[orderIndex];
                _transitionChipX[orderIndex] = chipX;
                _transitionChipWidth[orderIndex] = chipWidth;
                _transitionChipHeight[orderIndex] = chipHeight;
                _transitionRouteTopY[orderIndex] = detectorY;
                _transitionRouteY[orderIndex] = detectorY + chipHeight;
                _transitionRouteStartX[orderIndex] = Math.Min(sourceX, targetX);
                _transitionRouteEndX[orderIndex] = Math.Max(sourceX, targetX);
                _transitionRouteLane[orderIndex] = 0;
                _transitionLaneSpan[orderIndex] = 1;
            }

            _routeBaseY = maximumBottom + 16f;
            _contentExtentHeight = _contentLayer != null
                ? Math.Max(1f, _contentLayer.height)
                : ContentHeight;
            HideTransitionSlots(transitionCount);
            HideConnectors(connectorUsed);
            HideTransitionFlowSlots(transitionCount);
            RefreshBehaviorLogicFlowAnimation();
            if (_endPort != null)
                _endPort.visible = false;
        }

    }
}
