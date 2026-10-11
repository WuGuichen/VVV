using System;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        /// <summary>
        /// Builds the one-glance behavior explanation shown beside the gameplay plan.
        /// </summary>
        /// <remarks>
        /// LowFreqAlloc: rebuilt only when the behavior-card source revision changes. Stable
        /// FairyGUI refreshes consume the cached strings without formatting or collection work.
        /// </remarks>
        internal static void FillCurrentBehaviorSummary(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            in CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            bool hasSelectedAgent)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel?.BehaviorCardWorkspace?.Timeline;
            if (timeline == null)
                return;

            timeline.CurrentBehaviorAvailable = false;
            timeline.CurrentBehaviorWarningVisible = false;
            timeline.CurrentBehaviorTitleText = "尚未运行行为";
            timeline.CurrentBehaviorFactText = "开始测试后显示当前行为";
            timeline.CurrentBehaviorCommandText = "无命令";
            timeline.CurrentBehaviorWarningText = string.Empty;
            timeline.CurrentBehaviorTooltipText = string.Empty;
            timeline.CurrentBehaviorRepairDetectorStableId = string.Empty;
            FillBehaviorRelationship(viewModel, timeline, snapshot, agent, hasSelectedAgent);
            if (!hasSelectedAgent || !timeline.Available || timeline.StageCount <= 0)
                return;

            int stageIndex = FindCurrentBehaviorStage(timeline);
            if (stageIndex < 0)
            {
                FillDifferentViewedCardSummary(viewModel, timeline, agent);
                return;
            }
            CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[stageIndex];
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                FindCurrentBehaviorDetector(timeline, stageIndex);
            string currentAction = CharacterTestRuntimeAiPlannerDevPresentationText
                .BuildBehaviorGameplayActionLabel(stage);
            string targetAction = ResolveDetectorTargetAction(timeline, detector, currentAction);

            timeline.CurrentBehaviorAvailable = true;
            timeline.CurrentBehaviorCommandText = currentAction;
            timeline.CurrentBehaviorTitleText = detector != null
                ? BuildCurrentBehaviorWaitingTitle(targetAction)
                : "正在" + currentAction;
            timeline.CurrentBehaviorFactText = detector == null
                ? "当前阶段没有活动判断"
                : "正在监听“" + NonEmpty(detector.FactText, detector.Title) + "”";
            timeline.CurrentBehaviorTooltipText = "阶段：" + stage.StageId
                + (detector == null ? string.Empty : "\n判断：" + detector.DetectorId);

            if (detector == null
                || detector.ConditionCount <= 0
                || detector.Conditions == null)
            {
                return;
            }

            int conditionCount = Math.Min(detector.ConditionCount, detector.Conditions.Length);
            for (int i = 0; i < conditionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicConditionViewModel condition = detector.Conditions[i];
                if (!TryResolveCurrentBooleanFact(condition, agent, out bool actual))
                    continue;
                bool expected = ResolveExpectedBoolean(condition);
                string actualText = BuildCurrentFactText(condition.Fact, actual);
                string expectedText = BuildExpectedFactText(condition.Fact, expected);
                if (actual == expected)
                {
                    timeline.CurrentBehaviorFactText = actualText + " · 条件已满足";
                    continue;
                }

                timeline.CurrentBehaviorFactText = actualText
                    + " · 条件却要求“" + expectedText + "”";
                timeline.CurrentBehaviorWarningVisible = true;
                timeline.CurrentBehaviorWarningText =
                    BuildCurrentBehaviorConditionLabel(targetAction) + "与当前事实矛盾";
                timeline.CurrentBehaviorRepairDetectorStableId = detector.DetectorId;
                timeline.CurrentBehaviorTooltipText += "\n当前事实与判断条件冲突；修复按钮只定位配置，不自动改写。";
                break;
            }
        }

        /// <summary>
        /// Projects the previous, committed and pending behavior identities into the compact
        /// relationship strip. This is presentation-only and never guesses a future behavior.
        /// </summary>
        /// <remarks>LowFreqAlloc: rebuilt with the behavior-card presentation view model.</remarks>
        private static void FillBehaviorRelationship(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            in CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            bool hasSelectedAgent)
        {
            timeline.BehaviorRelationshipAvailable = false;
            timeline.PreviousBehaviorText = "上一行为\n—";
            timeline.PreviousBehaviorRelationText = "→";
            timeline.PreviousBehaviorTooltipText = "本帧没有可用的上一提交行为";
            timeline.PreviousBehaviorEntryIndex = -1;
            timeline.CommittedBehaviorText = "当前行为\n尚无行为";
            timeline.CommittedBehaviorTooltipText = "当前没有已提交的行为卡";
            timeline.CommittedBehaviorEntryIndex = -1;
            timeline.NextBehaviorText = "下一 · 待决策";
            timeline.NextBehaviorRelationText = "→";
            timeline.NextBehaviorTooltipText = "尚未产生真实的下一行为候选";
            timeline.NextBehaviorEntryIndex = -1;
            timeline.NextBehaviorPending = false;
            timeline.BehaviorCandidateCount = 0;
            timeline.BehaviorSuccessExitText = "完成";
            timeline.BehaviorFailureExitText = "失败";

            if (!hasSelectedAgent || viewModel?.BehaviorCardIdentity?.Runtime == null)
                return;

            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime =
                viewModel.BehaviorCardIdentity.Runtime;
            CharacterTestRuntimeAiLabDecisionTraceAgentView trace = default;
            bool hasTrace = snapshot.CommittedFrameReceipt.IsValid
                && snapshot.Timeline != null
                && snapshot.Timeline.TryGetDecisionTrace(
                    snapshot.CommittedFrameReceipt.Bundle.Record,
                    agent.EntityId,
                    out trace);

            CharacterTestRuntimeAiLabRevisionCardIdentity previous = hasTrace
                ? FindBehaviorIdentity(runtime, trace.PreviousCommitted.CardStableId)
                : null;
            CharacterTestRuntimeAiLabRevisionCardIdentity committed = runtime.CommittedCard;
            CharacterTestRuntimeAiLabRevisionCardIdentity pending = runtime.PendingCard;

            FillBehaviorCandidates(timeline, runtime, trace, hasTrace, agent);
            FillBehaviorExitLabels(timeline, runtime.CommittedProgram);

            if (previous != null)
            {
                timeline.PreviousBehaviorText = "上一行为\n"
                    + BuildBehaviorCandidateTitle(previous, null);
                timeline.PreviousBehaviorRelationText = committed != null
                    && previous.BindingIndex == committed.BindingIndex
                        ? "重入 →"
                        : "切换 →";
                timeline.PreviousBehaviorTooltipText = "上一提交行为：" + previous.DisplayName;
                timeline.PreviousBehaviorEntryIndex = previous.BindingIndex;
            }

            if (committed != null)
            {
                timeline.CommittedBehaviorText = "当前行为\n"
                    + BuildBehaviorCandidateTitle(committed, runtime.CommittedProgram);
                timeline.CommittedBehaviorTooltipText = "当前已提交并拥有执行权的行为：" + committed.DisplayName;
                timeline.CommittedBehaviorEntryIndex = committed.BindingIndex;
            }

            if (pending != null)
            {
                timeline.NextBehaviorText = "下一 · "
                    + NonEmpty(pending.DisplayName, "未命名行为");
                timeline.NextBehaviorRelationText = "→ 候选";
                timeline.NextBehaviorTooltipText = "候选行为尚未取得执行权：" + pending.DisplayName;
                timeline.NextBehaviorEntryIndex = pending.BindingIndex;
                timeline.NextBehaviorPending = true;
            }

            timeline.BehaviorRelationshipAvailable = previous != null
                || committed != null
                || pending != null
                || timeline.BehaviorCandidateCount > 0;
        }

        private static void FillBehaviorCandidates(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime,
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            bool hasTrace,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiLabCandidateRevision revision = runtime?.Revision;
            int count = Math.Min(
                revision?.CardIdentities?.Count ?? 0,
                revision?.LogicPrograms?.Count ?? 0);
            if (count <= 0)
                return;
            EnsureBehaviorCandidateCapacity(timeline, count);
            int previousVisibleCount = timeline.BehaviorCandidateCount;
            int visibleIndex = 0;
            for (int index = 0; index < count; index++)
            {
                CharacterTestRuntimeAiLabRevisionCardIdentity identity =
                    revision.CardIdentities[index];
                CharacterTestRuntimeAiLabCompiledLogicProgram program =
                    revision.LogicPrograms[index];
                bool committed = runtime.CommittedCard != null
                    && identity != null
                    && identity.BindingIndex == runtime.CommittedCard.BindingIndex;
                if (identity?.CoreSlotKind
                        == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting
                    || (identity?.CoreSlotKind
                            == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback
                        && !committed))
                {
                    continue;
                }
                CharacterTestRuntimeAiLabBehaviorCandidateViewModel candidate =
                    timeline.BehaviorCandidates[visibleIndex++];
                ResetBehaviorCandidate(candidate);
                candidate.EntryIndex = identity?.BindingIndex ?? index;
                candidate.StableId = identity?.RuntimeEntryId ?? string.Empty;
                candidate.Title = BuildBehaviorCandidateTitle(identity, program);

                CharacterTestRuntimeAiLabCandidateEvaluationTrace evaluation = default;
                bool hasEvaluation = hasTrace && TryFindCardCandidate(
                    trace,
                    candidate.StableId,
                    out evaluation);
                candidate.Evaluated = hasEvaluation
                    && evaluation.State != CharacterTestRuntimeAiLabDecisionTraceCandidateState.NotEvaluated;
                candidate.Selected = hasEvaluation
                    && evaluation.State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected;
                candidate.Committed = committed;
                candidate.Pending = runtime.PendingCard != null
                    && candidate.EntryIndex == runtime.PendingCard.BindingIndex
                    && !candidate.Committed;
                candidate.Eligible = hasEvaluation
                    && evaluation.State != CharacterTestRuntimeAiLabDecisionTraceCandidateState.Ineligible
                    && evaluation.State != CharacterTestRuntimeAiLabDecisionTraceCandidateState.NotEvaluated;
                candidate.ScoreAvailable = hasEvaluation && evaluation.ScoreAvailable;
                candidate.Score = candidate.ScoreAvailable ? evaluation.Score : 0f;
                candidate.ScoreText = BuildBehaviorCandidateScoreText(
                    evaluation,
                    hasEvaluation,
                    candidate.Committed,
                    candidate.Pending,
                    candidate.Selected);

                int conditionCount = Math.Min(
                    program?.ActivationConditions?.Count ?? 0,
                    CharacterTestRuntimeAiLabBehaviorCandidateViewModel.MaxVisibleConditions);
                if (conditionCount <= 0)
                {
                    candidate.ConditionCount = 0;
                }
                else
                {
                    candidate.ConditionCount = conditionCount;
                    for (int conditionIndex = 0; conditionIndex < conditionCount; conditionIndex++)
                    {
                        CharacterTestRuntimeAiLabCompiledCondition condition =
                            program.ActivationConditions[conditionIndex];
                        bool known = TryEvaluateBehaviorCondition(
                            condition,
                            trace,
                            hasTrace,
                            agent,
                            timeline.ActiveActionProgress.Available,
                            out bool satisfied,
                            out float observedNumber,
                            out bool observedBoolean);
                        candidate.ConditionKnown[conditionIndex] = known;
                        candidate.ConditionSatisfied[conditionIndex] = known && satisfied;
                        candidate.ConditionTexts[conditionIndex] = BuildBehaviorConditionText(
                            condition,
                            known,
                            observedNumber,
                            observedBoolean);
                    }
                }
                candidate.TooltipText = BuildBehaviorCandidateTooltip(identity, evaluation, hasEvaluation);
            }
            int staleEnd = Math.Min(previousVisibleCount, timeline.BehaviorCandidates.Length);
            for (int index = visibleIndex; index < staleEnd; index++)
                ResetBehaviorCandidate(timeline.BehaviorCandidates[index]);
            timeline.BehaviorCandidateCount = visibleIndex;
        }

        private static void EnsureBehaviorCandidateCapacity(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            int count)
        {
            CharacterTestRuntimeAiLabBehaviorCandidateViewModel[] candidates =
                timeline.BehaviorCandidates ?? Array.Empty<CharacterTestRuntimeAiLabBehaviorCandidateViewModel>();
            if (candidates.Length < count)
                Array.Resize(ref candidates, Math.Max(count, candidates.Length * 2 + 4));
            for (int i = 0; i < count; i++)
                candidates[i] = candidates[i] ?? new CharacterTestRuntimeAiLabBehaviorCandidateViewModel();
            timeline.BehaviorCandidates = candidates;
        }

        private static void ResetBehaviorCandidate(
            CharacterTestRuntimeAiLabBehaviorCandidateViewModel candidate)
        {
            candidate.EntryIndex = -1;
            candidate.StableId = string.Empty;
            candidate.Title = string.Empty;
            candidate.ScoreText = string.Empty;
            candidate.TooltipText = string.Empty;
            candidate.Selected = false;
            candidate.Committed = false;
            candidate.Pending = false;
            candidate.Eligible = false;
            candidate.Evaluated = false;
            candidate.ScoreAvailable = false;
            candidate.Score = 0f;
            candidate.ConditionCount = 0;
            for (int i = 0; i < candidate.ConditionTexts.Length; i++)
            {
                candidate.ConditionTexts[i] = string.Empty;
                candidate.ConditionSatisfied[i] = false;
                candidate.ConditionKnown[i] = false;
            }
        }

        private static bool TryFindCardCandidate(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string stableId,
            out CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate)
        {
            for (int i = 0; i < trace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace current = trace.Candidates[i];
                if (current.Layer == CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration
                    && string.Equals(current.StableId, stableId, StringComparison.Ordinal))
                {
                    candidate = current;
                    return true;
                }
            }
            candidate = default;
            return false;
        }

        private static string BuildBehaviorCandidateScoreText(
            in CharacterTestRuntimeAiLabCandidateEvaluationTrace evaluation,
            bool available,
            bool committed,
            bool pending,
            bool selected)
        {
            if (committed)
                return "执行中";
            if (pending || selected)
                return "等待执行";
            if (!available
                || evaluation.State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.NotEvaluated)
                return "未评估";
            if (evaluation.State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.Ineligible)
                return "条件不满足";
            if (!evaluation.ScoreAvailable)
                return "候选";
            string score = evaluation.Score.ToString(
                "0.##",
                System.Globalization.CultureInfo.InvariantCulture);
            return "得分 " + score;
        }

        private static string BuildBehaviorCandidateTitle(
            CharacterTestRuntimeAiLabRevisionCardIdentity identity,
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            string displayName = identity?.DisplayName ?? string.Empty;
            if (ContainsOrdinalIgnoreCase(displayName, "NearestEnemy")) return "选择最近敌人";
            if (ContainsOrdinalIgnoreCase(displayName, "Approach target")) return "靠近目标";
            if (ContainsOrdinalIgnoreCase(displayName, "Primary attack")) return "主要攻击";
            if (ContainsOrdinalIgnoreCase(displayName, "Wait or hold")) return "等待或保持";
            if (ContainsOrdinalIgnoreCase(displayName, "Avoid hazard")) return "避开危险";
            if (ContainsOrdinalIgnoreCase(displayName, "Strafe when blocked")) return "受阻横移";
            for (int i = 0; i < displayName.Length; i++)
            {
                if (displayName[i] >= '\u3400' && displayName[i] <= '\u9fff')
                    return displayName;
            }
            string key = identity?.LegacySourceKey;
            if (string.IsNullOrEmpty(key))
                key = identity?.PublishedStableId;
            if (string.IsNullOrEmpty(key))
                key = identity?.DisplayName;
            string normalized = key ?? string.Empty;
            if (ContainsOrdinalIgnoreCase(normalized, "NearestEnemy")) return "选择最近敌人";
            if (ContainsOrdinalIgnoreCase(normalized, "ApproachTarget")
                || ContainsOrdinalIgnoreCase(normalized, "Approach target")) return "靠近目标";
            if (ContainsOrdinalIgnoreCase(normalized, "PrimaryAttack")
                || ContainsOrdinalIgnoreCase(normalized, "Primary attack")) return "主要攻击";
            if (ContainsOrdinalIgnoreCase(normalized, "WaitOrHold")
                || ContainsOrdinalIgnoreCase(normalized, "Wait or hold")) return "等待或保持";
            if (ContainsOrdinalIgnoreCase(normalized, "AvoidHazard")
                || ContainsOrdinalIgnoreCase(normalized, "Avoid hazard")) return "避开危险";
            if (ContainsOrdinalIgnoreCase(normalized, "StrafeWhenBlocked")
                || ContainsOrdinalIgnoreCase(normalized, "Strafe when blocked")) return "受阻横移";
            return NonEmpty(displayName, "未命名行为");
        }

        private static bool ContainsOrdinalIgnoreCase(string value, string token) =>
            value?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string BuildBehaviorCandidateTooltip(
            CharacterTestRuntimeAiLabRevisionCardIdentity identity,
            in CharacterTestRuntimeAiLabCandidateEvaluationTrace evaluation,
            bool hasEvaluation)
        {
            return "行为：" + NonEmpty(identity?.DisplayName, "未命名行为")
                + "\n绑定：" + NonEmpty(identity?.RuntimeEntryId, "无")
                + "\n状态：" + (hasEvaluation ? evaluation.State.ToString() : "无本帧证据")
                + "\n原因：" + (hasEvaluation ? NonEmpty(evaluation.ReasonCode, "无") : "无");
        }

        private static void FillBehaviorExitLabels(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            if (program?.Transitions == null)
                return;
            for (int i = 0; i < program.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledLogicTransition transition = program.Transitions[i];
                if (!transition.EndsCard)
                    continue;
                string reason = transition.EndReason ?? string.Empty;
                string label = NonEmpty(transition.Label, reason);
                if (ContainsOrdinalIgnoreCase(reason, "fail"))
                    timeline.BehaviorFailureExitText = LocalizeBehaviorCardEndLabel(label, "失败");
                else if (ContainsOrdinalIgnoreCase(reason, "complete")
                    || ContainsOrdinalIgnoreCase(reason, "success"))
                    timeline.BehaviorSuccessExitText = LocalizeBehaviorCardEndLabel(label, "完成");
            }
        }

        private static string LocalizeBehaviorCardEndLabel(string label, string fallback)
        {
            if (string.IsNullOrWhiteSpace(label))
                return fallback;
            if (string.Equals(label, "Complete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, "Success", StringComparison.OrdinalIgnoreCase))
                return "完成";
            if (string.Equals(label, "Fail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, "Failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, "Failure", StringComparison.OrdinalIgnoreCase))
                return "失败";
            return label;
        }

        private static CharacterTestRuntimeAiLabRevisionCardIdentity FindBehaviorIdentity(
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime,
            string runtimeEntryId)
        {
            if (runtime?.Revision?.CardIdentities == null || string.IsNullOrEmpty(runtimeEntryId))
                return null;
            for (int i = 0; i < runtime.Revision.CardIdentities.Count; i++)
            {
                CharacterTestRuntimeAiLabRevisionCardIdentity identity =
                    runtime.Revision.CardIdentities[i];
                if (identity != null && string.Equals(
                        identity.RuntimeEntryId,
                        runtimeEntryId,
                        StringComparison.Ordinal))
                {
                    return identity;
                }
            }
            return null;
        }

        private static string BuildCurrentBehaviorWaitingTitle(string targetAction)
        {
            string subject = NonEmpty(targetAction, "行为");
            if (subject.StartsWith("等待", StringComparison.Ordinal))
                subject = subject.Substring(2);
            if (subject.EndsWith("条件", StringComparison.Ordinal))
                subject = subject.Substring(0, subject.Length - 2);
            return string.IsNullOrEmpty(subject)
                ? "等待条件"
                : "等待" + subject + "条件";
        }

        private static string BuildCurrentBehaviorConditionLabel(string targetAction)
        {
            string subject = NonEmpty(targetAction, "行为");
            return subject.EndsWith("条件", StringComparison.Ordinal)
                ? subject
                : subject + "条件";
        }

        private static int FindCurrentBehaviorStage(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            int count = Math.Min(timeline.StageCount, timeline.Stages?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                if (timeline.Stages[i]?.Current == true)
                    return i;
            }
            return -1;
        }

        private static void FillDifferentViewedCardSummary(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection identity = viewModel.BehaviorCardIdentity;
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime = identity?.Runtime;
            if (runtime == null || (runtime.CommittedCard == null && runtime.PendingCard == null))
                return;

            CharacterTestRuntimeAiLabRevisionCardIdentity currentCard = runtime.CommittedCard;
            CharacterTestRuntimeAiLabRevisionCardIdentity pendingCard = runtime.PendingCard;
            string viewedName = NonEmpty(identity.EditCard?.DisplayName, "未命名行为");
            CharacterTestRuntimeAiLabPrimaryActionIdentity action = runtime.PrimaryAction;
            timeline.CurrentBehaviorAvailable = true;
            timeline.CurrentBehaviorTitleText = currentCard != null
                ? "当前：" + NonEmpty(currentCard.DisplayName, "未命名行为") + "｜查看：" + viewedName
                : "候选" + NonEmpty(pendingCard?.DisplayName, "行为") + " · 未执行";
            timeline.CurrentBehaviorCommandText = action?.Available == true
                ? NonEmpty(action.DisplayName, runtime.CurrentStageTitle)
                : NonEmpty(runtime.CurrentStageTitle, "等待下一次决策");

            if (pendingCard != null)
            {
                timeline.CurrentBehaviorFactText = "候选"
                    + NonEmpty(pendingCard.DisplayName, "行为")
                    + " · 未执行";
            }
            else
            {
                timeline.CurrentBehaviorFactText = "查看卡不是当前执行卡";
            }

            bool rejected = action?.CommandRejected == true || !string.IsNullOrEmpty(agent.RejectReason);
            timeline.CurrentBehaviorWarningVisible = rejected;
            timeline.CurrentBehaviorWarningText = rejected
                ? "动作未执行 · " + NonEmpty(agent.RejectReason, "运行时拒绝")
                : string.Empty;
            timeline.CurrentBehaviorTooltipText = "当前绑定：" + NonEmpty(currentCard?.RuntimeEntryId, "无")
                + "\n当前阶段：" + runtime.CurrentStageId
                + "\n查看绑定：" + NonEmpty(identity.EditCard?.DraftEntryId, "无");
        }

        private static CharacterTestRuntimeAiLabLogicDetectorViewModel FindCurrentBehaviorDetector(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            int stageIndex)
        {
            int count = Math.Min(timeline.DetectorCount, timeline.Detectors?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[i];
                if (detector == null || (!detector.CardScope && detector.StageIndex != stageIndex))
                    continue;
                if (detector.Active || detector.Armed)
                    return detector;
            }
            return null;
        }

        private static string ResolveDetectorTargetAction(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            string fallback)
        {
            if (detector == null)
                return fallback;
            int count = Math.Min(timeline.TransitionCount, timeline.Transitions?.Length ?? 0);
            int stageCount = Math.Min(timeline.StageCount, timeline.Stages?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = timeline.Transitions[i];
                if (transition == null
                    || transition.DetectorIndex != detector.Index
                    || transition.TargetStageIndex < 0
                    || transition.TargetStageIndex >= stageCount)
                {
                    continue;
                }
                return CharacterTestRuntimeAiPlannerDevPresentationText
                    .BuildBehaviorGameplayActionLabel(timeline.Stages[transition.TargetStageIndex]);
            }
            return fallback;
        }

        private static bool TryResolveCurrentBooleanFact(
            CharacterTestRuntimeAiLabLogicConditionViewModel condition,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            out bool value)
        {
            value = false;
            if (condition == null)
                return false;
            switch (condition.Fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    value = agent.TargetExists;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    value = agent.TargetVisible;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    value = agent.TargetDead;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    value = agent.MovePathAvailable;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    value = agent.TargetAttackWillHitSelf;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    value = agent.TargetAttackInstanceResponded;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    value = agent.SelfDodgeCooldownReady;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    value = agent.TargetInAttackRange;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    value = agent.TargetLineOfSightBlocked;
                    return true;
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    value = agent.EnvironmentNearHazard;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryEvaluateBehaviorCondition(
            in CharacterTestRuntimeAiLabCompiledCondition condition,
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            bool hasTrace,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            bool activeActionAvailable,
            out bool satisfied,
            out float observedNumber,
            out bool observedBoolean)
        {
            satisfied = false;
            observedNumber = 0f;
            observedBoolean = false;
            if (hasTrace && TryReadBehaviorConditionTraceValue(condition, trace, out var value))
            {
                if (value.Kind == CharacterTestRuntimeAiLabDecisionTraceValueKind.Boolean)
                {
                    observedBoolean = value.Boolean;
                    satisfied = CompareBehaviorBoolean(observedBoolean, condition);
                    return true;
                }
                if (value.Kind == CharacterTestRuntimeAiLabDecisionTraceValueKind.Integer)
                {
                    observedNumber = value.Integer;
                    satisfied = CompareBehaviorNumber(observedNumber, condition);
                    return true;
                }
                if (value.Kind == CharacterTestRuntimeAiLabDecisionTraceValueKind.Float)
                {
                    observedNumber = value.Number;
                    satisfied = CompareBehaviorNumber(observedNumber, condition);
                    return true;
                }
            }

            bool hasBoolean = true;
            switch (condition.Fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    observedBoolean = agent.TargetExists;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    observedBoolean = agent.TargetVisible;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    observedBoolean = agent.TargetDead;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    observedBoolean = activeActionAvailable;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    observedBoolean = agent.SelfActionInterruptible;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    observedBoolean = agent.MovePathAvailable;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    observedBoolean = agent.TargetAttackWillHitSelf;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    observedBoolean = agent.TargetAttackInstanceResponded;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    observedBoolean = agent.SelfDodgeCooldownReady;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    observedBoolean = agent.TargetInAttackRange;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    observedBoolean = agent.TargetLineOfSightBlocked;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    observedBoolean = agent.EnvironmentNearHazard;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                    observedNumber = agent.TargetDistance;
                    hasBoolean = false;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                    observedBoolean = agent.TargetDistanceAvailable;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    observedNumber = agent.TargetRootDistance;
                    hasBoolean = false;
                    break;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                    observedNumber = agent.TargetAttackImpactFrames;
                    hasBoolean = false;
                    break;
                default:
                    return false;
            }
            satisfied = hasBoolean
                ? CompareBehaviorBoolean(observedBoolean, condition)
                : CompareBehaviorNumber(observedNumber, condition);
            return true;
        }

        private static bool TryReadBehaviorConditionTraceValue(
            in CharacterTestRuntimeAiLabCompiledCondition condition,
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            out CharacterTestRuntimeAiLabDecisionTraceValue value)
        {
            for (int i = 0; i < trace.Facts.Count; i++)
            {
                CharacterTestRuntimeAiLabFactObservationTrace fact = trace.Facts[i];
                if (fact.Fact != condition.Fact)
                    continue;
                if ((condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                        || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction)
                    && (string.IsNullOrEmpty(condition.ConditionId)
                        || fact.StableId?.IndexOf(
                            "/fact/" + condition.ConditionId + "/",
                            StringComparison.Ordinal) < 0))
                {
                    continue;
                }
                value = fact.Value;
                return value.Kind != CharacterTestRuntimeAiLabDecisionTraceValueKind.None;
            }
            value = default;
            return false;
        }

        private static bool CompareBehaviorBoolean(
            bool actual,
            in CharacterTestRuntimeAiLabCompiledCondition condition)
        {
            return condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                ? actual != condition.BoolValue
                : actual == condition.BoolValue;
        }

        private static bool CompareBehaviorNumber(
            float actual,
            in CharacterTestRuntimeAiLabCompiledCondition condition)
        {
            switch (condition.Operator)
            {
                case CharacterTestRuntimeAiLabCardConditionOperator.Equals:
                    return Math.Abs(actual - condition.Value) <= 0.0001f;
                case CharacterTestRuntimeAiLabCardConditionOperator.NotEquals:
                    return Math.Abs(actual - condition.Value) > 0.0001f;
                case CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual:
                    return actual <= condition.Value;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual:
                    return actual >= condition.Value;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan:
                    return actual > condition.Value;
                case CharacterTestRuntimeAiLabCardConditionOperator.Between:
                    return actual >= condition.Value && actual <= condition.MaxValue;
                default:
                    return false;
            }
        }

        private static string BuildBehaviorConditionText(
            in CharacterTestRuntimeAiLabCompiledCondition condition,
            bool known,
            float observedNumber,
            bool observedBoolean)
        {
            if (!known)
                return "条件数据不可用";
            bool expected = condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                ? !condition.BoolValue
                : condition.BoolValue;
            switch (condition.Fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    return expected ? "目标存在" : "没有目标";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    return expected ? "目标可见" : "目标不可见";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    return expected ? "目标死亡" : "目标存活";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                    return BuildBehaviorNumericConditionText("受击框净空", "m", condition, observedNumber);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                    return expected ? "受击框距离可用" : "受击框距离不可用";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    return BuildBehaviorNumericConditionText("根距离", "m", condition, observedNumber);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    return expected ? "角色动作运行中" : "无角色动作运行";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return expected ? "自身动作可打断" : "自身动作不可打断";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                    return expected ? "可切换到目标动作" : "不可切换到目标动作";
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    return expected ? "能力可用" : "能力不可用";
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    return expected ? "路径可用" : "路径不可用";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    return expected ? "敌方攻击将命中" : "敌方攻击不会命中";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                    return BuildBehaviorNumericConditionText("命中剩余", "帧", condition, observedNumber);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    return expected ? "本次攻击已响应" : "本次攻击未响应";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    return expected ? "闪避冷却就绪" : "闪避冷却中";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    return expected ? "在攻击范围" : "不在攻击范围";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    return expected ? "视线受阻" : "视线畅通";
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    return expected ? "附近存在危险" : "附近没有危险";
                default:
                    return observedBoolean ? "条件为真" : "条件为假";
            }
        }

        private static string BuildBehaviorNumericConditionText(
            string subject,
            string unit,
            in CharacterTestRuntimeAiLabCompiledCondition condition,
            float observed)
        {
            string expected;
            string value = condition.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            switch (condition.Operator)
            {
                case CharacterTestRuntimeAiLabCardConditionOperator.Between:
                    expected = value + "–" + condition.MaxValue.ToString(
                        "0.##",
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual:
                    expected = "≤ " + value;
                    break;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual:
                    expected = "≥ " + value;
                    break;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan:
                    expected = "> " + value;
                    break;
                case CharacterTestRuntimeAiLabCardConditionOperator.NotEquals:
                    expected = "≠ " + value;
                    break;
                default:
                    expected = value;
                    break;
            }
            return subject + " " + expected + unit + "（当前 "
                + observed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + unit + "）";
        }

        private static bool ResolveExpectedBoolean(
            CharacterTestRuntimeAiLabLogicConditionViewModel condition)
        {
            return condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                ? !condition.BoolValue
                : condition.BoolValue;
        }

        private static string BuildCurrentFactText(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            bool value)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    return value ? "已存在目标" : "当前没有目标";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    return value ? "目标当前可见" : "目标当前不可见";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    return value ? "目标已经死亡" : "目标尚未死亡";
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    return value ? "当前存在路径" : "当前没有路径";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    return value ? "敌方攻击将命中" : "敌方攻击不会命中";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    return value ? "本次攻击已响应" : "本次攻击尚未响应";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    return value ? "目标已在攻击范围内" : "目标仍在攻击范围外";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    return value ? "目标视线受阻" : "目标视线畅通";
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    return value ? "附近存在危险" : "附近没有危险";
                default:
                    return value ? "当前事实为是" : "当前事实为否";
            }
        }

        private static string BuildExpectedFactText(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            bool value)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    return value ? "存在目标" : "没有目标";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    return value ? "目标可见" : "目标不可见";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    return value ? "目标死亡" : "目标存活";
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    return value ? "存在路径" : "没有路径";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    return value ? "攻击将命中" : "攻击不会命中";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    return value ? "已经响应" : "尚未响应";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    return value ? "进入攻击范围" : "处于攻击范围外";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    return value ? "视线受阻" : "视线畅通";
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    return value ? "附近有危险" : "附近无危险";
                default:
                    return value ? "是" : "否";
            }
        }
    }
}
