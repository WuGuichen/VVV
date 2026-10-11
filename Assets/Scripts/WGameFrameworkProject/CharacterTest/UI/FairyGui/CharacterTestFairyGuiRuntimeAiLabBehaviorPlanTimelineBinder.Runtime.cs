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
        private void UpdateGameplayPlanRuntimeStyles(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            GameplayReadablePlanStageProjection currentStage =
                _gameplayPlanProjection.FindCurrentStage();
            int currentStartFrame = currentStage?.StartFrame ?? int.MinValue;
            for (int i = 0; i < _gameplayPlanStageCount; i++)
            {
                GameplayPlanBarSlot slot = _gameplayPlanStageBars[i];
                HideGameplayPlanProgress(slot);
                GameplayReadablePlanStageProjection stage =
                    _gameplayPlanProjection.FindStage(slot.StableId);
                if (stage != null)
                {
                    slot.Selected = stage.Selected;
                    slot.Current = stage.Current;
                    slot.Conflict = stage.ActionConflict;
                    slot.Visual.alpha = ResolveGameplayPlanItemAlpha(
                        stage,
                        currentStartFrame,
                        completed: stage.StartFrame < currentStartFrame);
                    RedrawGameplayPlanBar(slot);
                }
            }
            for (int i = 0; i < _gameplayPlanActionCount; i++)
            {
                GameplayPlanBarSlot slot = _gameplayPlanActionBars[i];
                HideGameplayPlanProgress(slot);
                GameplayReadablePlanStageProjection stage =
                    _gameplayPlanProjection.FindStage(slot.StableId);
                if (stage != null)
                {
                    slot.Selected = stage.Selected;
                    slot.Current = stage.ActionRunning || stage.Current;
                    slot.Conflict = stage.ActionConflict;
                    slot.Visual.alpha = ResolveGameplayPlanItemAlpha(
                        stage,
                        currentStartFrame,
                        completed: stage.ActionProgressMode == GameplayReadablePlanProgressMode.Completed
                            || stage.StartFrame < currentStartFrame);
                    RedrawGameplayPlanBar(slot);
                    if (stage.MetricKind == GameplayReadablePlanMetricKind.Distance)
                    {
                        DrawGameplayPlanDistanceMetric(slot, stage);
                    }
                    else
                    {
                        DrawGameplayPlanProgress(
                            slot.ProgressFill,
                            slot.IndeterminateSweep,
                            slot.StateBadge,
                            slot.Visual.width,
                            slot.Visual.height,
                            stage.ActionProgressMode,
                            stage.ActionProgressFrames,
                            stage.ActionDurationFrames);
                        LayoutGameplayPlanActionContent(
                            slot,
                            stage.ActionProgressMode != GameplayReadablePlanProgressMode.None);
                    }
                    UpdateGameplayPlanActionRestartBadge(slot, stage);
                }
            }
            for (int i = 0; i < _gameplayPlanExitCount; i++)
            {
                GameplayPlanExitSlot slot = _gameplayPlanExitCards[i];
                GameplayReadablePlanExitProjection exit = slot.Projection;
                slot.Label.text = ResolveGameplayPlanExitRuntimeLabel(slot, exit);
                RedrawGameplayPlanExit(slot);
                GameplayReadablePlanProgressMode exitProgressMode =
                    exit?.ProgressDurationFrames > 0
                        ? exit.ProgressMode
                        : GameplayReadablePlanProgressMode.None;
                DrawGameplayPlanProgress(
                    slot.ProgressFill,
                    slot.IndeterminateSweep,
                    slot.StateBadge,
                    slot.Visual.width,
                    slot.Visual.height,
                    exitProgressMode,
                    exit?.ProgressFrames ?? -1,
                    exit?.ProgressDurationFrames ?? 0);
                LayoutGameplayPlanExitContent(
                    slot,
                    exitProgressMode != GameplayReadablePlanProgressMode.None);
            }

            bool visible = _gameplayPlanProjection.SnapshotVisible;
            _gameplayPlanExecutionStatus.visible = false;
            _gameplayPlanExecutionStatus.text = _gameplayPlanProjection.ExecutionStatusText;
            HideGameplayPlanRelationshipChrome();
        }

        private static float ResolveGameplayPlanItemAlpha(
            GameplayReadablePlanStageProjection stage,
            int currentStartFrame,
            bool completed)
        {
            if (stage == null)
                return 0.32f;
            if (stage.Selected || stage.Current || stage.ActionRunning)
                return 1f;
            if (currentStartFrame == int.MinValue)
                return 0.78f;
            return completed ? 0.58f : 0.36f;
        }

        private static string ResolveGameplayPlanExitRuntimeLabel(
            GameplayPlanExitSlot slot,
            GameplayReadablePlanExitProjection exit)
        {
            if (exit == null)
                return string.Empty;
            switch (exit.RuntimeState)
            {
                case GameplayReadablePlanExitRuntimeState.Committed:
                    return slot.CommittedRuntimeLabel;
                case GameplayReadablePlanExitRuntimeState.Pending:
                case GameplayReadablePlanExitRuntimeState.Triggered:
                    return slot.TriggeredRuntimeLabel;
                case GameplayReadablePlanExitRuntimeState.Listening:
                    return slot.RuntimeLabel;
                default:
                    return slot.RuntimeLabel;
            }
        }

        /// <remarks>NoAlloc. Dynamic frame text comes from the prebuilt progress-label caches.</remarks>
        private void UpdateGameplayPlanSummary(
            GameplayReadablePlanStageProjection currentStage,
            bool snapshotVisible,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            bool relationshipVisible = viewModel?.BehaviorRelationshipAvailable == true;
            bool showCurrentProgress = snapshotVisible && currentStage != null;

            _gameplayPlanSummaryPrefix.SetXY(194f, 110f);
            _gameplayPlanSummaryPrefix.SetSize(146f, 82f);
            _gameplayPlanSummaryCurrent.SetXY(432f, 94f);
            _gameplayPlanSummaryCurrent.SetSize(172f, 78f);
            _gameplayPlanSummaryFrame.SetXY(470f, 184f);
            _gameplayPlanSummaryFrame.SetSize(98f, 36f);
            _gameplayPlanSummaryNext.SetXY(792f, 130f);
            _gameplayPlanSummaryNext.SetSize(108f, 60f);

            _gameplayPlanSummaryPrefix.visible = true;
            _gameplayPlanSummaryCurrent.visible = true;
            _gameplayPlanSummaryFramePrefix.visible = false;
            _gameplayPlanSummaryFrame.visible = true;
            _gameplayPlanSummaryNextPrefix.visible = false;
            _gameplayPlanSummaryNext.visible = true;
            _gameplayPlanSummaryPrefix.text = viewModel?.PreviousBehaviorText ?? "上一行为\n—";
            _gameplayPlanSummaryPrefix.align = Fgui.AlignType.Center;
            _gameplayPlanSummaryPrefix.color = new Color32(184, 201, 209, 255);
            BindTooltip(_gameplayPlanSummaryPrefix, viewModel?.PreviousBehaviorTooltipText);
            _gameplayPlanSummaryCurrent.text = relationshipVisible
                ? viewModel.CommittedBehaviorText
                : "当前行为\n尚无行为";
            _gameplayPlanSummaryCurrent.align = Fgui.AlignType.Center;
            _gameplayPlanSummaryCurrent.color = relationshipVisible
                ? new Color32(78, 229, 219, 255)
                : new Color32(137, 158, 169, 255);
            BindTooltip(
                _gameplayPlanSummaryCurrent,
                viewModel?.CommittedBehaviorTooltipText ?? "当前没有已提交的行为卡");
            _gameplayPlanSummaryFrame.text = viewModel?.Historical == true
                ? _gameplayPlanProjection.ExecutionStatusText
                : showCurrentProgress
                    ? "执行中"
                    : _gameplayPlanProjection.ExecutionStatusText;
            _gameplayPlanSummaryFrame.color = showCurrentProgress
                ? new Color32(29, 224, 217, 255)
                : new Color32(137, 158, 169, 255);
            _gameplayPlanSummaryNext.text = "重新决策";
            _gameplayPlanSummaryNext.align = Fgui.AlignType.Center;
            _gameplayPlanSummaryNext.color = new Color32(205, 224, 232, 255);
            BindTooltip(
                _gameplayPlanSummaryNext,
                "当前行为结束后，Runtime AI Planner 重新评估全部候选行为。");

            int candidateCount = Math.Min(
                _gameplayPlanCandidateCount,
                Math.Min(
                    viewModel?.BehaviorCandidateCount ?? 0,
                    viewModel?.BehaviorCandidates?.Length ?? 0));
            for (int i = 0; i < candidateCount; i++)
            {
                GameplayPlanCandidateSlot slot = _gameplayPlanCandidateCards[i];
                BindGameplayPlanCandidate(
                    slot,
                    viewModel.BehaviorCandidates[i],
                    slot.Visual.x,
                    slot.Visual.y,
                    slot.Visual.width,
                    slot.Visual.height);
            }

            _gameplayPlanPreviousBehaviorEntryIndex = viewModel?.PreviousBehaviorEntryIndex ?? -1;
            _gameplayPlanCommittedBehaviorEntryIndex = viewModel?.CommittedBehaviorEntryIndex ?? -1;
            _gameplayPlanNextBehaviorEntryIndex = viewModel?.NextBehaviorEntryIndex ?? -1;
        }

        private void DrawGameplayPlanBehaviorNode(
            int index,
            float x,
            float y,
            float width,
            float height,
            bool committed,
            bool pending)
        {
            Fgui.GGraph background = _gameplayPlanBehaviorNodeBackgrounds[index];
            background.SetXY(x, y);
            background.SetSize(width, height);
            background.alpha = pending ? 0.72f : 1f;
            Color32 border = committed
                ? new Color32(54, 205, 196, 255)
                : pending
                    ? new Color32(211, 139, 38, 255)
                    : new Color32(87, 111, 122, 255);
            Color32 fill = committed
                ? new Color32(8, 49, 55, 245)
                : pending
                    ? new Color32(48, 35, 12, 224)
                    : new Color32(15, 29, 36, 224);
            background.DrawRect(width, height, committed ? 2 : 1, border, fill);
            background.visible = true;
        }

        private void SelectGameplayPlanBehaviorNode(int nodeIndex)
        {
            int entryIndex = nodeIndex == 0
                ? _gameplayPlanPreviousBehaviorEntryIndex
                : nodeIndex == 1
                    ? _gameplayPlanCommittedBehaviorEntryIndex
                    : _gameplayPlanNextBehaviorEntryIndex;
            if (entryIndex < 0)
                return;
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(entryIndex),
                null));
        }

        private static string ResolveGameplayPlanSummaryFrame(
            GameplayReadablePlanStageProjection stage)
        {
            if (stage.ActionProgressMode == GameplayReadablePlanProgressMode.Indeterminate
                || stage.ActionDurationFrames <= 0)
            {
                int frame = Mathf.Clamp(
                    stage.ActionProgressFrames,
                    0,
                    GameplayPlanOpenFrameLabels.Length - 1);
                return GameplayPlanCompactOpenFrameLabels[frame];
            }

            GameplayPlanDeterminateFrameLabels labels =
                GetGameplayPlanDeterminateFrameLabels(stage.ActionDurationFrames);
            int progress = Mathf.Clamp(stage.ActionProgressFrames, 0, labels.Running.Length - 1);
            return labels.Running[progress];
        }

        private static void HideGameplayPlanProgress(GameplayPlanBarSlot slot)
        {
            slot.ProgressFill.visible = false;
            slot.IndeterminateSweep.visible = false;
            slot.StateBadge.visible = false;
            slot.MetricTargetBadge.visible = false;
            slot.RestartBadge.visible = false;
        }

        /// <remarks>NoAlloc. Both strings are cached or structural projection values.</remarks>
        private static void DrawGameplayPlanDistanceMetric(
            GameplayPlanBarSlot slot,
            GameplayReadablePlanStageProjection stage)
        {
            slot.ProgressFill.visible = false;
            slot.IndeterminateSweep.visible = false;
            slot.Label.align = Fgui.AlignType.Left;
            slot.Label.SetXY(12f, 0f);
            slot.Label.SetSize(118f, slot.Visual.height);
            slot.StateBadge.align = Fgui.AlignType.Left;
            slot.StateBadge.SetXY(132f, 0f);
            slot.StateBadge.SetSize(132f, slot.Visual.height);
            slot.StateBadge.text = stage.MetricCurrentText;
            slot.StateBadge.visible = true;
            slot.MetricTargetBadge.align = Fgui.AlignType.Left;
            slot.MetricTargetBadge.SetXY(264f, 0f);
            slot.MetricTargetBadge.SetSize(Mathf.Max(1f, slot.Visual.width - 276f), slot.Visual.height);
            slot.MetricTargetBadge.text = stage.MetricTargetText;
            slot.MetricTargetBadge.visible = true;
        }

        private static void LayoutGameplayPlanActionContent(
            GameplayPlanBarSlot slot,
            bool showsProgress)
        {
            if (!showsProgress)
            {
                slot.Label.align = Fgui.AlignType.Center;
                slot.Label.SetXY(8f, 0f);
                slot.Label.SetSize(Mathf.Max(1f, slot.Visual.width - 16f), slot.Visual.height);
                return;
            }

            slot.Label.align = Fgui.AlignType.Left;
            slot.Label.SetXY(12f, 2f);
            slot.Label.SetSize(Mathf.Max(1f, slot.Visual.width - 24f), 30f);
            slot.StateBadge.align = Fgui.AlignType.Right;
            slot.StateBadge.SetXY(12f, 34f);
            slot.StateBadge.SetSize(Mathf.Max(1f, slot.Visual.width - 24f), 28f);
        }

        private static void LayoutGameplayPlanExitContent(
            GameplayPlanExitSlot slot,
            bool showsProgress)
        {
            if (!showsProgress)
            {
                slot.Label.SetXY(18f, 0f);
                slot.Label.SetSize(
                    Mathf.Max(1f, slot.Visual.width - 30f),
                    slot.Visual.height);
                return;
            }
            slot.Label.SetXY(18f, 2f);
            slot.Label.SetSize(
                Mathf.Max(1f, slot.Visual.width - 30f),
                28f);
            slot.StateBadge.align = Fgui.AlignType.Right;
            slot.StateBadge.SetXY(18f, 32f);
            slot.StateBadge.SetSize(
                Mathf.Max(1f, slot.Visual.width - 30f),
                24f);
        }

        /// <remarks>NoAlloc. The marker remains visible for the restarted action instance.</remarks>
        private void UpdateGameplayPlanActionRestartBadge(
            GameplayPlanBarSlot slot,
            GameplayReadablePlanStageProjection stage)
        {
            long instanceId = stage.ActionInstanceId;
            if (instanceId > 0L
                && slot.ObservedActionInstanceId > 0L
                && instanceId != slot.ObservedActionInstanceId)
            {
                slot.RestartActionInstanceId = instanceId;
            }
            if (instanceId > 0L)
                slot.ObservedActionInstanceId = instanceId;

            bool visible = instanceId > 0L
                && slot.RestartActionInstanceId == instanceId;
            slot.RestartBadge.visible = visible;
            if (!visible)
                return;

            slot.RestartBadge.SetXY(8f, 0f);
            slot.RestartBadge.SetSize(36f, slot.Visual.height);
            BindTooltip(slot.RestartBadge, "同一阶段已重新启动一个新的角色动作实例");
        }

        /// <remarks>NoAlloc.</remarks>
        private static void DrawGameplayPlanProgress(
            Fgui.GGraph progressFill,
            Fgui.GGraph indeterminateSweep,
            Fgui.GTextField stateBadge,
            float width,
            float height,
            GameplayReadablePlanProgressMode mode,
            int progressFrames,
            int durationFrames)
        {
            progressFill.visible = false;
            indeterminateSweep.visible = false;
            stateBadge.visible = false;
            if (mode == GameplayReadablePlanProgressMode.None)
                return;

            if (mode == GameplayReadablePlanProgressMode.Indeterminate
                || durationFrames <= 0)
            {
                string[] openLabels = mode == GameplayReadablePlanProgressMode.Completed
                    ? GameplayPlanCompletedOpenFrameLabels
                    : mode == GameplayReadablePlanProgressMode.Failed
                        ? GameplayPlanFailedOpenFrameLabels
                        : GameplayPlanOpenFrameLabels;
                BindGameplayPlanFrameBadge(
                    stateBadge,
                    width,
                    height,
                    openLabels,
                    progressFrames);
                return;
            }

            float ratio = mode == GameplayReadablePlanProgressMode.Completed
                ? 1f
                : durationFrames > 0
                    ? Mathf.Clamp01(Math.Max(0, progressFrames) / (float)durationFrames)
                    : 1f;
            Color32 fill = mode == GameplayReadablePlanProgressMode.Failed
                ? new Color32(210, 54, 66, 190)
                : mode == GameplayReadablePlanProgressMode.Overrun
                    ? new Color32(226, 144, 32, 196)
                    : mode == GameplayReadablePlanProgressMode.Completed
                        ? new Color32(43, 184, 112, 196)
                        : new Color32(35, 205, 194, 184);
            if (ratio > 0f)
            {
                progressFill.SetXY(0f, 0f);
                progressFill.DrawRect(
                    Mathf.Max(2f, width * ratio),
                    height,
                    0,
                    Color.clear,
                    fill);
                progressFill.visible = true;
            }
            GameplayPlanDeterminateFrameLabels labels =
                GetGameplayPlanDeterminateFrameLabels(durationFrames);
            if (mode == GameplayReadablePlanProgressMode.Completed)
            {
                BindGameplayPlanFrameBadge(stateBadge, width, height, labels.Completed);
                return;
            }
            if (mode == GameplayReadablePlanProgressMode.Overrun)
            {
                BindGameplayPlanFrameBadge(stateBadge, width, height, labels.Overrun);
                return;
            }
            string[] frameLabels = labels.Running;
            if (mode == GameplayReadablePlanProgressMode.Failed)
            {
                frameLabels = durationFrames > 0
                    ? labels.Failed
                    : GameplayPlanFailedOpenFrameLabels;
            }
            BindGameplayPlanFrameBadge(
                stateBadge,
                width,
                height,
                frameLabels,
                progressFrames);
        }

        private static void BindGameplayPlanFrameBadge(
            Fgui.GTextField badge,
            float width,
            float height,
            string[] labels,
            int frames)
        {
            int index = Mathf.Clamp(frames, 0, labels.Length - 1);
            BindGameplayPlanFrameBadge(badge, width, height, labels[index]);
        }

        private static void BindGameplayPlanFrameBadge(
            Fgui.GTextField badge,
            float width,
            float height,
            string label)
        {
            float badgeWidth = Mathf.Min(144f, width);
            badge.SetXY(Mathf.Max(0f, width - badgeWidth), 0f);
            badge.SetSize(badgeWidth, height);
            badge.text = label;
            badge.visible = true;
        }

        private static string[] CreateGameplayPlanOpenFrameLabels(string prefix)
        {
            var labels = new string[GameplayPlanFrameLabelLimit + 1];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = prefix + i.ToString(CultureInfo.InvariantCulture) + " 帧 / ∞";
            return labels;
        }

        private static string[] CreateGameplayPlanCompactOpenFrameLabels()
        {
            var labels = new string[GameplayPlanFrameLabelLimit + 1];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = i.ToString(CultureInfo.InvariantCulture) + "/∞";
            return labels;
        }

        /// <remarks>
        /// LowFreqAlloc when a previously unseen authored duration is first rendered;
        /// subsequent stable refreshes reuse the cached strings and allocate nothing.
        /// </remarks>
        private static GameplayPlanDeterminateFrameLabels GetGameplayPlanDeterminateFrameLabels(
            int durationFrames)
        {
            int total = Mathf.Clamp(durationFrames, 1, GameplayPlanFrameLabelLimit);
            if (GameplayPlanDeterminateFrameLabelCache.TryGetValue(total, out var cached))
                return cached;

            var labels = new GameplayPlanDeterminateFrameLabels
            {
                Running = new string[total + 1],
                Failed = new string[total + 1]
            };
            string totalText = total.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i <= total; i++)
            {
                string progressText = i.ToString(CultureInfo.InvariantCulture);
                labels.Running[i] = progressText + "/" + totalText;
                labels.Failed[i] = "× " + progressText + "/" + totalText;
            }
            labels.Completed = "✓ " + totalText + "/" + totalText;
            labels.Overrun = "! " + totalText + "+/" + totalText;
            GameplayPlanDeterminateFrameLabelCache.Add(total, labels);
            return labels;
        }

        private void OnGameplayPlanElementClicked(
            GameplayReadablePlanDetailKind detailKind,
            string stableId)
        {
            string normalized = stableId ?? string.Empty;
            if (normalized.Length == 0)
                return;
            if (_gameplayPlanDetailKind == detailKind
                && string.Equals(_gameplayPlanDetailStableId, normalized, StringComparison.Ordinal))
            {
                ShowGameplayPlanOverview();
                return;
            }
            _gameplayPlanDetailKind = detailKind;
            _gameplayPlanDetailStableId = normalized;
            _gameplayPlanDetailChanged?.Invoke();

            if (detailKind == GameplayReadablePlanDetailKind.Stage
                || detailKind == GameplayReadablePlanDetailKind.Action)
            {
                _planInteractions.SelectStage(normalized);
                return;
            }
            GameplayReadablePlanExitProjection exit = _gameplayPlanProjection.FindExit(normalized);
            CharacterTestRuntimeAiLabLogicElementKind kind = exit?.ElementKind
                ?? CharacterTestRuntimeAiLabLogicElementKind.Transition;
            if (kind == CharacterTestRuntimeAiLabLogicElementKind.Detector)
                _planInteractions.SelectDetector(normalized);
            else
                _planInteractions.SelectTransition(normalized);
        }

        private void ShowGameplayPlanOverview()
        {
            if (_gameplayPlanDetailKind == GameplayReadablePlanDetailKind.None)
                return;
            _gameplayPlanDetailKind = GameplayReadablePlanDetailKind.None;
            _gameplayPlanDetailStableId = string.Empty;
            _gameplayPlanDetailChanged?.Invoke();
        }

        private static void ResolveGameplayPlanExitPalette(
            GameplayReadablePlanExitTone tone,
            out Color32 fill,
            out Color32 border)
        {
            switch (tone)
            {
                case GameplayReadablePlanExitTone.Success:
                    fill = new Color32(8, 35, 28, 244);
                    border = new Color32(53, 175, 109, 255);
                    return;
                case GameplayReadablePlanExitTone.Failure:
                    fill = new Color32(40, 20, 27, 244);
                    border = new Color32(205, 76, 86, 255);
                    return;
                case GameplayReadablePlanExitTone.Condition:
                    fill = new Color32(42, 32, 13, 244);
                    border = new Color32(201, 128, 31, 255);
                    return;
                default:
                    fill = new Color32(39, 24, 24, 244);
                    border = new Color32(202, 96, 68, 255);
                    return;
            }
        }

        private static Fgui.GGraph NewGraph(string name, bool touchable)
        {
            return new Fgui.GGraph { name = name, touchable = touchable };
        }

        private static Fgui.GTextField NewGameplayPlanText(
            string name,
            string text,
            int size,
            Color32 color,
            Fgui.AlignType align)
        {
            var label = new Fgui.GTextField
            {
                name = name,
                touchable = false,
                text = text,
                color = color,
                align = align,
                verticalAlign = Fgui.VertAlignType.Middle,
                autoSize = Fgui.AutoSizeType.None
            };
            Fgui.TextFormat format = label.textFormat;
            format.size = size;
            format.color = color;
            label.textFormat = format;
            return label;
        }

    }
}
