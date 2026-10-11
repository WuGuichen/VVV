using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder
    {
        private void RenderGameplayPlanGeometry(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            int startFrame = Math.Max(0, _gameplayPlanProjection.PresentationStartFrame);
            int endFrame = Math.Max(startFrame + 1, _gameplayPlanProjection.PresentationEndFrame);
            float canvasLeft = GameplayPlanLabelWidth;
            float canvasRight = Mathf.Max(canvasLeft + 1f, _root.width - GameplayPlanCanvasRightPadding);
            float canvasWidth = canvasRight - canvasLeft;
            DrawGameplayPlanStaticTracks(startFrame, endFrame, canvasLeft, canvasWidth);

            _gameplayPlanStageCount = 0;
            _gameplayPlanActionCount = 0;
            _gameplayPlanExitCount = 0;
            _gameplayPlanBranchCount = 0;
            _gameplayPlanConnectorCount = 0;
            _gameplayPlanGlobalBandCount = 0;
            _gameplayPlanCandidateCount = 0;
            _gameplayPlanExitGroupBounds.Clear();

            HideGameplayPlanRelationshipChrome();
            bool singleStage = _gameplayPlanProjection.StageCount == 1;

            for (int i = 0; i < _gameplayPlanProjection.StageCount; i++)
            {
                GameplayReadablePlanStageProjection stage = _gameplayPlanProjection.GetStage(i);
                if (stage == null)
                    continue;
                float x = FrameToGameplayPlanX(
                    stage.StartFrame,
                    startFrame,
                    endFrame,
                    canvasLeft,
                    canvasWidth);
                float endX = FrameToGameplayPlanX(
                    stage.StartFrame + stage.DurationFrames,
                    startFrame,
                    endFrame,
                    canvasLeft,
                    canvasWidth);
                // Preserve the authored time geometry. A large visual minimum made short
                // stages appear to end later than their authored endpoint and disconnected
                // the local event progress from the plan it was meant to explain.
                float width = Mathf.Clamp(endX - x - 4f, 180f, 420f);
                if (!singleStage)
                {
                    BindGameplayPlanBar(
                        GetGameplayPlanBar(_gameplayPlanStageBars, _gameplayPlanStageCount++, "planStage"),
                        stage.SourceIndex,
                        stage.StableId,
                        GameplayReadablePlanDetailKind.Stage,
                        stage.StageLabel,
                        stage.TooltipText,
                        x + 2f,
                        GameplayPlanTrackY[0] + 4f,
                        width,
                        56f,
                        new Color32(10, 47, 55, 232),
                        new Color32(39, 211, 201, 210),
                        stage.Selected,
                        stage.Current,
                        stage.ActionConflict);
                }

                if (!stage.OwnsPrimaryAction)
                    continue;
                float actionX = FrameToGameplayPlanX(
                    stage.ActionStartFrame,
                    startFrame,
                    endFrame,
                    canvasLeft,
                    canvasWidth);
                float actionWidth;
                if (stage.ActionDurationFrames > 0)
                {
                    float actionEndX = FrameToGameplayPlanX(
                        stage.ActionStartFrame + stage.ActionDurationFrames,
                        startFrame,
                        endFrame,
                        canvasLeft,
                        canvasWidth);
                    actionWidth = Mathf.Clamp(actionEndX - actionX - 4f, 180f, 420f);
                }
                else if (stage.MetricKind == GameplayReadablePlanMetricKind.Distance)
                {
                    actionWidth = 420f;
                }
                else
                {
                    actionWidth = 320f;
                }
                BindGameplayPlanBar(
                    GetGameplayPlanBar(_gameplayPlanActionBars, _gameplayPlanActionCount++, "planAction"),
                    stage.SourceIndex,
                    stage.StableId,
                    GameplayReadablePlanDetailKind.Action,
                    singleStage ? stage.StageLabel : stage.ActionLabel,
                    stage.TooltipText,
                    actionX + 2f,
                    singleStage ? GameplayPlanTrackY[0] + 4f : GameplayPlanTrackY[1] + 4f,
                    actionWidth,
                    60f,
                    new Color32(10, 40, 58, 242),
                    new Color32(53, 153, 232, 220),
                    stage.Selected,
                    stage.ActionRunning || stage.Current,
                    stage.ActionConflict);
            }

            float conditionBaseY = singleStage
                ? GameplayPlanTrackY[1] + 4f
                : GameplayPlanTrackY[1] + GameplayPlanTrackHeight[1] + 8f;
            for (int exitIndex = 0; exitIndex < _gameplayPlanProjection.ExitCount; exitIndex++)
            {
                GameplayReadablePlanExitProjection exit = _gameplayPlanProjection.GetExit(exitIndex);
                if (!ShouldRenderCompactGameplayPlanExit(exit))
                    continue;
                int localRow = 0;
                for (int previousIndex = 0; previousIndex < exitIndex; previousIndex++)
                {
                    GameplayReadablePlanExitProjection previous =
                        _gameplayPlanProjection.GetExit(previousIndex);
                    if (ShouldRenderCompactGameplayPlanExit(previous)
                        && previous.SourceStageIndex == exit.SourceStageIndex)
                    {
                        localRow++;
                    }
                }
                float exitX = canvasLeft + 2f;
                GameplayReadablePlanStageProjection sourceStage = null;
                for (int stageIndex = 0;
                     stageIndex < _gameplayPlanProjection.StageCount;
                     stageIndex++)
                {
                    GameplayReadablePlanStageProjection candidate =
                        _gameplayPlanProjection.GetStage(stageIndex);
                    if (candidate?.SourceIndex == exit.SourceStageIndex)
                    {
                        sourceStage = candidate;
                        break;
                    }
                }
                if (sourceStage != null)
                {
                    exitX = FrameToGameplayPlanX(
                        sourceStage.StartFrame,
                        startFrame,
                        endFrame,
                        canvasLeft,
                        canvasWidth) + 2f;
                }
                BindGameplayPlanExit(
                    GetGameplayPlanExit(_gameplayPlanExitCount++),
                    exit,
                    exitX,
                    conditionBaseY + localRow * 64f,
                    320f,
                    56f);
                GameplayPlanExitSlot compactExit = _gameplayPlanExitCards[_gameplayPlanExitCount - 1];
                if (sourceStage != null
                    && exit.Tone == GameplayReadablePlanExitTone.Success
                    && !string.IsNullOrEmpty(sourceStage.MetricCompletionText))
                {
                    compactExit.RuntimeLabel = sourceStage.MetricCompletionText + " → 重新决策";
                    compactExit.TriggeredRuntimeLabel = "已满足 · " + compactExit.RuntimeLabel;
                    compactExit.CommittedRuntimeLabel = "已退出 · 重新决策";
                    compactExit.Label.text = compactExit.RuntimeLabel;
                }
                else if (sourceStage != null
                    && exit.Tone == GameplayReadablePlanExitTone.Failure)
                {
                    compactExit.RuntimeLabel = sourceStage.ActionLabel + "失败 → 重新决策";
                    compactExit.TriggeredRuntimeLabel = "已失败 · " + compactExit.RuntimeLabel;
                    compactExit.CommittedRuntimeLabel = "已退出 · 重新决策";
                    compactExit.Label.text = compactExit.RuntimeLabel;
                }
            }

            HideGameplayPlanBars(_gameplayPlanStageBars, _gameplayPlanStageCount);
            HideGameplayPlanBars(_gameplayPlanActionBars, _gameplayPlanActionCount);
            HideGameplayPlanExits(_gameplayPlanExitCount);
            HideGameplayPlanBranches(_gameplayPlanBranchCount);
            HideGameplayPlanConnectors(_gameplayPlanConnectorCount);
            HideGameplayPlanGlobalBands(_gameplayPlanGlobalBandCount);
            HideGameplayPlanCandidates(_gameplayPlanCandidateCount);
            _gameplayPlanExitEmpty.visible = false;
            BringGameplayPlanExecutionOverlayToFront();
        }

        private static bool ShouldRenderCompactGameplayPlanExit(
            GameplayReadablePlanExitProjection exit)
        {
            if (exit == null)
                return false;
            return exit.Tone == GameplayReadablePlanExitTone.Success
                || exit.Tone == GameplayReadablePlanExitTone.Failure
                || exit.Tone == GameplayReadablePlanExitTone.Condition
                || exit.Tone == GameplayReadablePlanExitTone.Invalid;
        }

        private void HideGameplayPlanRelationshipChrome()
        {
            _gameplayPlanRelationshipLabel.visible = false;
            _gameplayPlanSuccessExitLabel.visible = false;
            _gameplayPlanFailureExitLabel.visible = false;
            _gameplayPlanSummaryPrefix.visible = false;
            _gameplayPlanSummaryCurrent.visible = false;
            _gameplayPlanSummaryFramePrefix.visible = false;
            _gameplayPlanSummaryFrame.visible = false;
            _gameplayPlanSummaryNextPrefix.visible = false;
            _gameplayPlanSummaryNext.visible = false;
            for (int i = 0; i < _gameplayPlanBehaviorNodeBackgrounds.Length; i++)
                _gameplayPlanBehaviorNodeBackgrounds[i].visible = false;
            for (int i = 0; i < _gameplayPlanBehaviorExitBackgrounds.Length; i++)
                _gameplayPlanBehaviorExitBackgrounds[i].visible = false;
        }

        private void BringGameplayPlanExecutionOverlayToFront()
        {
            int topIndex = _gameplayPlanLayer.numChildren - 1;
            for (int index = 0; index < _gameplayPlanExitCount; index++)
            {
                Fgui.GComponent exitCard = _gameplayPlanExitCards[index].Visual;
                if (exitCard.visible)
                    _gameplayPlanLayer.SetChildIndex(exitCard, topIndex);
            }
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanExecutionStatus, topIndex);
            for (int i = 0; i < _gameplayPlanBehaviorNodeBackgrounds.Length; i++)
                _gameplayPlanLayer.SetChildIndex(_gameplayPlanBehaviorNodeBackgrounds[i], topIndex);
            for (int i = 0; i < _gameplayPlanBehaviorExitBackgrounds.Length; i++)
                _gameplayPlanLayer.SetChildIndex(_gameplayPlanBehaviorExitBackgrounds[i], topIndex);
            for (int i = 0; i < _gameplayPlanCandidateCount; i++)
                _gameplayPlanLayer.SetChildIndex(_gameplayPlanCandidateCards[i].Visual, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanRelationshipLabel, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanSuccessExitLabel, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanFailureExitLabel, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanSummaryPrefix, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanSummaryCurrent, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanSummaryFramePrefix, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanSummaryFrame, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanSummaryNextPrefix, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanSummaryNext, topIndex);
            _gameplayPlanLayer.SetChildIndex(_gameplayPlanExitEmpty, topIndex);
        }

        /// <remarks>LowFreqAlloc only when the candidate-card high-water mark grows.</remarks>
        private void RenderGameplayPlanBehaviorRelationship(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            const float previousX = 184f;
            const float previousY = 94f;
            const float previousWidth = 166f;
            const float previousHeight = 116f;
            const float currentX = 418f;
            const float currentY = 78f;
            const float currentWidth = 200f;
            const float currentHeight = 160f;
            const float exitX = 668f;
            const float exitWidth = 84f;
            const float exitHeight = 48f;
            const float successY = 92f;
            const float failureY = 188f;
            const float decisionX = 792f;
            const float decisionY = 130f;
            const float decisionWidth = 108f;
            const float decisionHeight = 60f;
            const float decisionCenterY = decisionY + decisionHeight * 0.5f;
            const float candidateLeft = 956f;
            const float candidateTop = 82f;
            const float candidateBottom = 392f;
            const float candidateGapX = 18f;
            const float candidateGapY = 16f;

            _gameplayPlanRelationshipLabel.SetXY(24f, 140f);
            _gameplayPlanRelationshipLabel.SetSize(132f, 56f);
            DrawGameplayPlanBehaviorNode(0, previousX, previousY, previousWidth, previousHeight, false, false);
            DrawGameplayPlanBehaviorNode(1, currentX, currentY, currentWidth, currentHeight, true, false);
            DrawGameplayPlanBehaviorDecisionNode(
                decisionX,
                decisionY,
                decisionWidth,
                decisionHeight);

            DrawGameplayPlanDashedHorizontal(
                previousX + previousWidth,
                currentX,
                previousY + previousHeight * 0.5f,
                2f);
            DrawGameplayPlanHorizontalLine(
                currentX + currentWidth,
                638f,
                decisionCenterY,
                2f);
            DrawGameplayPlanLine(638f, successY + exitHeight * 0.5f, 2f,
                failureY - successY);
            DrawGameplayPlanHorizontalLine(638f, exitX, successY + exitHeight * 0.5f, 2f);
            DrawGameplayPlanHorizontalLine(638f, exitX, failureY + exitHeight * 0.5f, 2f);

            BindGameplayPlanBehaviorExit(
                0,
                exitX,
                successY,
                exitWidth,
                exitHeight,
                true,
                viewModel?.BehaviorSuccessExitText);
            BindGameplayPlanBehaviorExit(
                1,
                exitX,
                failureY,
                exitWidth,
                exitHeight,
                false,
                viewModel?.BehaviorFailureExitText);

            const float mergeX = 774f;
            float successCenterY = successY + exitHeight * 0.5f;
            float failureCenterY = failureY + exitHeight * 0.5f;
            DrawGameplayPlanDashedHorizontal(exitX + exitWidth, mergeX, successCenterY, 2f);
            DrawGameplayPlanDashedHorizontal(exitX + exitWidth, mergeX, failureCenterY, 2f);
            DrawGameplayPlanDashedVertical(mergeX, successCenterY, failureCenterY, 2f);
            DrawGameplayPlanDashedHorizontal(mergeX, decisionX, decisionCenterY, 2f);

            int count = Math.Min(
                viewModel?.BehaviorCandidateCount ?? 0,
                viewModel?.BehaviorCandidates?.Length ?? 0);
            if (count <= 0)
                return;
            int columns = count <= 6 ? 3 : 4;
            int rows = Math.Max(1, (count + columns - 1) / columns);
            float availableWidth = Mathf.Max(400f, _root.width - candidateLeft - 24f);
            float cardWidth = (availableWidth - (columns - 1) * candidateGapX) / columns;
            float cardHeight = (candidateBottom - candidateTop - (rows - 1) * candidateGapY) / rows;
            float busX = 930f;
            float firstCenterY = candidateTop + cardHeight * 0.5f;
            float lastCenterY = candidateTop + (rows - 1) * (cardHeight + candidateGapY)
                + cardHeight * 0.5f;
            DrawGameplayPlanDashedHorizontal(decisionX + decisionWidth, busX, decisionCenterY, 2f);
            DrawGameplayPlanDashedVertical(busX, firstCenterY, lastCenterY, 2f);

            for (int index = 0; index < count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                float x = candidateLeft + column * (cardWidth + candidateGapX);
                float y = candidateTop + row * (cardHeight + candidateGapY);
                float centerY = y + cardHeight * 0.5f;
                DrawGameplayPlanDashedHorizontal(busX, x, centerY, 2f);
                BindGameplayPlanCandidate(
                    GetGameplayPlanCandidate(_gameplayPlanCandidateCount++),
                    viewModel.BehaviorCandidates[index],
                    x,
                    y,
                    cardWidth,
                    cardHeight);
            }
        }

        private void DrawGameplayPlanBehaviorDecisionNode(
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GGraph background = _gameplayPlanBehaviorNodeBackgrounds[2];
            background.SetXY(x, y);
            background.SetSize(width, height);
            background.rotation = 0f;
            background.DrawRect(
                width,
                height,
                2,
                new Color32(36, 221, 214, 255),
                new Color32(5, 38, 49, 250));
            background.visible = true;
        }

        private void BindGameplayPlanBehaviorExit(
            int index,
            float x,
            float y,
            float width,
            float height,
            bool success,
            string label)
        {
            Fgui.GGraph background = _gameplayPlanBehaviorExitBackgrounds[index];
            background.SetXY(x, y);
            background.DrawRect(
                width,
                height,
                1,
                success ? new Color32(77, 194, 85, 255) : new Color32(226, 70, 70, 255),
                success ? new Color32(9, 49, 30, 245) : new Color32(51, 13, 18, 245));
            background.visible = true;
            Fgui.GTextField text = success
                ? _gameplayPlanSuccessExitLabel
                : _gameplayPlanFailureExitLabel;
            text.SetXY(x, y);
            text.SetSize(width, height);
            text.text = string.IsNullOrEmpty(label) ? (success ? "完成" : "失败") : label;
            text.visible = true;
        }

        private GameplayPlanCandidateSlot GetGameplayPlanCandidate(int index)
        {
            while (_gameplayPlanCandidateCards.Count <= index)
            {
                var slot = new GameplayPlanCandidateSlot();
                var visual = new Fgui.GComponent
                {
                    name = "planBehaviorCandidate" + _gameplayPlanCandidateCards.Count
                        .ToString(CultureInfo.InvariantCulture),
                    touchable = true
                };
                slot.Background = NewGraph("background", false);
                slot.Title = NewGameplayPlanText(
                    "title", string.Empty, 22, new Color32(226, 237, 242, 255), Fgui.AlignType.Left);
                slot.Score = NewGameplayPlanText(
                    "score", string.Empty, 18, new Color32(117, 181, 229, 255), Fgui.AlignType.Right);
                slot.ConditionHeader = NewGameplayPlanText(
                    "conditionHeader", "条件:", 17, new Color32(205, 218, 224, 255), Fgui.AlignType.Left);
                visual.AddChild(slot.Background);
                visual.AddChild(slot.Title);
                visual.AddChild(slot.Score);
                visual.AddChild(slot.ConditionHeader);
                for (int i = 0; i < slot.Conditions.Length; i++)
                {
                    slot.Conditions[i] = NewGameplayPlanText(
                        "condition" + i.ToString(CultureInfo.InvariantCulture),
                        string.Empty,
                        17,
                        new Color32(196, 210, 217, 255),
                        Fgui.AlignType.Left);
                    visual.AddChild(slot.Conditions[i]);
                }
                slot.Visual = visual;
                visual.onClick.Set(context =>
                {
                    context.StopPropagation();
                    if (slot.EntryIndex >= 0)
                    {
                        _commandSink?.Enqueue(new MxUiCommand(
                            _viewId,
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                                .SelectWorkbenchEntry(slot.EntryIndex),
                            null));
                    }
                });
                _gameplayPlanCandidateCards.Add(slot);
                _gameplayPlanLayer.AddChild(visual);
            }
            GameplayPlanCandidateSlot result = _gameplayPlanCandidateCards[index];
            result.Visual.visible = true;
            return result;
        }

        private void BindGameplayPlanCandidate(
            GameplayPlanCandidateSlot slot,
            CharacterTestRuntimeAiLabBehaviorCandidateViewModel candidate,
            float x,
            float y,
            float width,
            float height)
        {
            slot.EntryIndex = candidate?.EntryIndex ?? -1;
            slot.Visual.SetXY(x, y);
            slot.Visual.SetSize(width, height);
            BindTooltip(slot.Visual, candidate?.TooltipText);
            slot.Background.SetXY(0f, 0f);
            Color32 border = candidate?.Committed == true
                ? new Color32(22, 226, 220, 255)
                : candidate?.Pending == true || candidate?.Selected == true
                    ? new Color32(238, 177, 55, 255)
                : candidate?.Eligible == true
                    ? new Color32(74, 177, 88, 255)
                    : new Color32(88, 110, 121, 255);
            Color32 fill = candidate?.Committed == true
                ? new Color32(5, 50, 59, 248)
                : candidate?.Pending == true || candidate?.Selected == true
                    ? new Color32(52, 37, 8, 246)
                : candidate?.Eligible == true
                    ? new Color32(8, 36, 27, 242)
                    : new Color32(12, 27, 35, 238);
            slot.Background.DrawRect(
                width,
                height,
                candidate?.Committed == true || candidate?.Pending == true || candidate?.Selected == true
                    ? 2
                    : 1,
                border,
                fill);
            slot.Title.SetXY(14f, 6f);
            slot.Title.SetSize(Mathf.Max(80f, width * 0.57f), 28f);
            slot.Title.text = candidate?.Title ?? "未命名行为";
            slot.Score.SetXY(width * 0.55f, 6f);
            slot.Score.SetSize(Mathf.Max(60f, width * 0.4f - 12f), 28f);
            slot.Score.text = candidate?.ScoreText ?? string.Empty;
            slot.Score.color = candidate?.Committed == true
                ? new Color32(30, 225, 218, 255)
                : candidate?.Pending == true || candidate?.Selected == true
                    ? new Color32(238, 190, 74, 255)
                : candidate?.Eligible == true
                    ? new Color32(91, 201, 105, 255)
                    : new Color32(117, 181, 229, 255);
            slot.ConditionHeader.SetXY(14f, 34f);
            slot.ConditionHeader.SetSize(width - 28f, 20f);
            int conditionCount = Math.Min(candidate?.ConditionCount ?? 0, slot.Conditions.Length);
            float stride = Mathf.Clamp((height - 54f) / Math.Max(1, conditionCount), 18f, 24f);
            for (int i = 0; i < slot.Conditions.Length; i++)
            {
                Fgui.GTextField text = slot.Conditions[i];
                bool visible = i < conditionCount;
                text.visible = visible;
                if (!visible)
                    continue;
                bool known = candidate.ConditionKnown[i];
                bool satisfied = candidate.ConditionSatisfied[i];
                text.SetXY(16f, 53f + i * stride);
                text.SetSize(width - 28f, stride);
                text.text = known
                    ? (satisfied ? "✓  " : "×  ") + candidate.ConditionTexts[i]
                    : candidate.ConditionTexts[i];
                text.color = !known
                    ? new Color32(145, 163, 172, 255)
                    : satisfied
                        ? new Color32(67, 213, 106, 255)
                        : new Color32(211, 219, 223, 255);
            }
        }

        private void DrawGameplayPlanDashedHorizontal(float fromX, float toX, float y, float height)
        {
            float start = Mathf.Min(fromX, toX);
            float end = Mathf.Max(fromX, toX);
            for (float x = start; x < end; x += 14f)
                DrawGameplayPlanHorizontalLine(x, Mathf.Min(x + 8f, end), y, height);
        }

        private void DrawGameplayPlanDashedVertical(float x, float fromY, float toY, float width)
        {
            float start = Mathf.Min(fromY, toY);
            float end = Mathf.Max(fromY, toY);
            for (float y = start; y < end; y += 14f)
                DrawGameplayPlanLine(x, y, width, Mathf.Min(8f, end - y));
        }

        private void RenderGameplayPlanBranchGroup(
            GameplayReadablePlanExitGroupProjection group,
            float anchorX,
            float canvasLeft,
            float canvasRight,
            int startFrame,
            int endFrame,
            float canvasWidth)
        {
            const float cardWidth = 382f;
            const float cardHeight = 60f;
            const float rowStride = 82f;
            const float columnGap = 18f;
            int visibleRowCount = Math.Min(3, Math.Max(1, group.ExitCount));
            float blockHeight = (visibleRowCount - 1) * rowStride + cardHeight;
            int columnCount = Math.Max(1, (group.ExitCount + 2) / 3);
            float requiredWidth = columnCount * cardWidth + (columnCount - 1) * columnGap;
            bool preferredRight = anchorX + 72f + requiredWidth <= canvasRight
                || anchorX - 72f - requiredWidth < canvasLeft;
            ResolveGameplayPlanBranchGroupPlacement(
                anchorX,
                canvasLeft,
                canvasRight,
                requiredWidth,
                blockHeight,
                preferredRight,
                out bool placeRight,
                out float firstCardX,
                out float rowStartY);
            float branchCenterY = rowStartY
                + (visibleRowCount - 1) * rowStride * 0.5f
                + cardHeight * 0.5f;
            float direction = placeRight ? 1f : -1f;

            GameplayPlanBranchSlot branch = GetGameplayPlanBranch(_gameplayPlanBranchCount++);
            BindGameplayPlanBranch(branch, group, anchorX, branchCenterY);

            float stageCenterY = GameplayPlanTrackY[0] + GameplayPlanTrackHeight[0] * 0.5f;
            float actionCenterY = GameplayPlanTrackY[1] + GameplayPlanTrackHeight[1] * 0.5f;
            DrawGameplayPlanLine(anchorX, stageCenterY, 2f, branchCenterY - stageCenterY);
            DrawGameplayPlanLine(anchorX, actionCenterY, 2f, branchCenterY - actionCenterY);

            if (group.CardScope)
            {
                float startX = FrameToGameplayPlanX(
                    group.WindowStartFrame,
                    startFrame,
                    endFrame,
                    canvasLeft,
                    canvasWidth);
                float endX = FrameToGameplayPlanX(
                    group.WindowStartFrame + group.WindowDurationFrames,
                    startFrame,
                    endFrame,
                    canvasLeft,
                    canvasWidth);
                BindGameplayPlanGlobalBand(
                    GetGameplayPlanGlobalBand(_gameplayPlanGlobalBandCount++),
                    group,
                    Mathf.Min(startX, endX),
                    Mathf.Max(180f, Mathf.Abs(endX - startX)));
            }

            if (group.ExitCount <= 0)
                return;
            float firstCenterY = rowStartY + cardHeight * 0.5f;
            float lastCenterY = rowStartY
                + Math.Min(2, group.ExitCount - 1) * rowStride
                + cardHeight * 0.5f;
            float trunkX = anchorX + direction * 42f;
            DrawGameplayPlanHorizontalLine(anchorX, trunkX, branchCenterY, 2f);
            DrawGameplayPlanLine(trunkX, firstCenterY, 2f, Mathf.Max(2f, lastCenterY - firstCenterY));

            for (int localIndex = 0; localIndex < group.ExitCount; localIndex++)
            {
                GameplayReadablePlanExitProjection exit =
                    _gameplayPlanProjection.GetExit(group.FirstExitIndex + localIndex);
                if (exit == null)
                    continue;
                int column = localIndex / 3;
                int row = localIndex % 3;
                float cardX = placeRight
                    ? firstCardX + column * (cardWidth + columnGap)
                    : firstCardX - column * (cardWidth + columnGap);
                cardX = Mathf.Clamp(cardX, canvasLeft + 6f, canvasRight - cardWidth - 6f);
                float cardY = rowStartY + row * rowStride;
                float cardEdgeX = placeRight ? cardX : cardX + cardWidth;
                DrawGameplayPlanHorizontalLine(trunkX, cardEdgeX, cardY + cardHeight * 0.5f, 2f);
                BindGameplayPlanExit(
                    GetGameplayPlanExit(_gameplayPlanExitCount++),
                    exit,
                    cardX,
                    cardY,
                    cardWidth,
                    cardHeight);
            }
        }

        /// <remarks>LowFreqAlloc. Called only when the projected card geometry changes.</remarks>
        private void ResolveGameplayPlanBranchGroupPlacement(
            float anchorX,
            float canvasLeft,
            float canvasRight,
            float requiredWidth,
            float blockHeight,
            bool preferredRight,
            out bool placeRight,
            out float firstCardX,
            out float rowStartY)
        {
            const float anchorGap = 72f;
            const float groupGap = 18f;
            const float collisionPadding = 10f;
            const float upperRowStart = 308f;
            float branchBottom = GameplayPlanTrackY[2] + GameplayPlanTrackHeight[2] - 2f;
            float lowerRowStart = Mathf.Max(upperRowStart, branchBottom - blockHeight);

            for (int sideAttempt = 0; sideAttempt < 2; sideAttempt++)
            {
                bool candidateRight = sideAttempt == 0 ? preferredRight : !preferredRight;
                for (int columnOffset = 0; columnOffset < 3; columnOffset++)
                {
                    float nearCardX = candidateRight
                        ? anchorX + anchorGap
                            + columnOffset * (requiredWidth + groupGap)
                        : anchorX - anchorGap - 382f
                            - columnOffset * (requiredWidth + groupGap);
                    float boundsX = candidateRight
                        ? nearCardX
                        : nearCardX - (requiredWidth - 382f);
                    if (boundsX < canvasLeft + 6f
                        || boundsX + requiredWidth > canvasRight - 6f)
                    {
                        continue;
                    }

                    for (int lane = 0; lane < 2; lane++)
                    {
                        float candidateY = lane == 0 ? upperRowStart : lowerRowStart;
                        var bounds = new Rect(boundsX, candidateY, requiredWidth, blockHeight);
                        if (OverlapsGameplayPlanExitGroup(bounds, collisionPadding))
                            continue;
                        _gameplayPlanExitGroupBounds.Add(bounds);
                        placeRight = candidateRight;
                        firstCardX = nearCardX;
                        rowStartY = candidateY;
                        return;
                    }
                }
            }

            placeRight = preferredRight;
            firstCardX = preferredRight
                ? Mathf.Clamp(anchorX + anchorGap, canvasLeft + 6f, canvasRight - requiredWidth - 6f)
                : Mathf.Clamp(anchorX - anchorGap - 382f, canvasLeft + requiredWidth - 376f, canvasRight - 388f);
            float fallbackBoundsX = preferredRight
                ? firstCardX
                : firstCardX - (requiredWidth - 382f);
            rowStartY = lowerRowStart;
            _gameplayPlanExitGroupBounds.Add(
                new Rect(fallbackBoundsX, rowStartY, requiredWidth, blockHeight));
        }

        private bool OverlapsGameplayPlanExitGroup(Rect candidate, float padding)
        {
            for (int index = 0; index < _gameplayPlanExitGroupBounds.Count; index++)
            {
                Rect occupied = _gameplayPlanExitGroupBounds[index];
                if (candidate.xMin < occupied.xMax + padding
                    && candidate.xMax > occupied.xMin - padding
                    && candidate.yMin < occupied.yMax + padding
                    && candidate.yMax > occupied.yMin - padding)
                {
                    return true;
                }
            }
            return false;
        }

    }
}
