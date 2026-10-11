using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Player-readable projection for one Runtime AI Lab timeline item.</summary>
    /// <remarks>
    /// LowFreqAlloc: created only while rebuilding the RuntimeFrame UI workspace. Runtime
    /// identity remains on the source item and is intentionally excluded from these strings.
    /// </remarks>
    internal sealed class CharacterTestRuntimeAiLabTimelineDetailViewModel
    {
        internal CharacterTestRuntimeAiLabTimelineItem SourceItem { get; set; }
        internal string Title { get; set; } = string.Empty;
        internal string MetaText { get; set; } = string.Empty;
        internal string FrameText { get; set; } = string.Empty;
        internal string DetailLine1 { get; set; } = string.Empty;
        internal string DetailLine2 { get; set; } = string.Empty;
        internal string DetailLine3 { get; set; } = string.Empty;
        internal string ParticipantsText { get; set; } = string.Empty;
        internal string TooltipText { get; set; } = string.Empty;

        internal IEnumerable<string> EnumerateVisibleText()
        {
            yield return Title;
            yield return MetaText;
            yield return FrameText;
            yield return DetailLine1;
            yield return DetailLine2;
            yield return DetailLine3;
            yield return ParticipantsText;
            yield return TooltipText;
        }
    }

    /// <summary>One selectable runtime agent in the single-surface test workspace.</summary>
    /// <remarks>LowFreqAlloc: rebuilt only when the RuntimeFrame UI projection changes.</remarks>
    internal sealed class CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel
    {
        internal int AgentIndex { get; set; }
        internal int EntityId { get; set; }
        internal int TeamId { get; set; }
        internal int SlotIndex { get; set; }
        internal string SlotLabel { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal bool Selected { get; set; }
    }

    /// <summary>Read-only live RuntimeFrame workspace projection.</summary>
    /// <remarks>LowFreqAlloc: rebuilt only for the active RuntimeFrame workspace.</remarks>
    internal sealed class CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel
    {
        private readonly Dictionary<string, CharacterTestRuntimeAiLabTimelineDetailViewModel>
            _timelineDetails =
                new Dictionary<string, CharacterTestRuntimeAiLabTimelineDetailViewModel>(
                    StringComparer.Ordinal);

        internal bool Available { get; set; }
        internal int AgentEntityId { get; set; }
        internal string AgentSlotLabel { get; set; } = string.Empty;
        internal string AgentDisplayName { get; set; } = string.Empty;
        internal CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel[] AgentOptions { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel>();
        internal int SessionGeneration { get; set; }
        internal long CommitGeneration { get; set; }
        internal long RecordGeneration { get; set; }
        internal long AuthorityHash { get; set; }
        internal int SessionStartFrame { get; set; }
        internal int RetainedStartFrame { get; set; }
        internal int SelectedFrame { get; set; }
        internal int LatestFrame { get; set; }
        internal int FrameLimit { get; set; }
        internal CharacterTestRuntimeAiLabTimelineViewport Viewport { get; set; }
        internal CharacterTestRuntimeAiLabTimelineProjection Projection { get; set; }
        internal CharacterTestRuntimeAiLabTimelineItem SelectedItem { get; set; }
        internal CharacterTestRuntimeAiLabTimelineDetailViewModel SelectedDetail { get; set; }
        internal CharacterTestRuntimeAiLabFrameSelectionPhase SelectionPhase { get; set; }
        internal bool ReplayInProgress { get; set; }
        internal bool HistoryPlaybackActive { get; set; }
        internal bool FutureAdvanceActive { get; set; }
        internal int FutureAdvanceTargetFrame { get; set; }
        internal bool BranchRecomputeArmed { get; set; }
        internal bool BranchConfirmationPending { get; set; }
        internal bool LoopEnabled { get; set; }
        internal int LoopStartFrame { get; set; }
        internal int LoopEndFrame { get; set; }
        internal bool CanSelectFrame { get; set; }
        internal bool CanStepBackward { get; set; }
        internal bool CanStepForward { get; set; }
        internal bool CanPauseResume { get; set; }
        internal bool CanJumpFrame { get; set; }
        internal bool CanPan { get; set; }
        internal bool CanZoom { get; set; }
        internal bool CanSetLoop { get; set; }
        internal bool CanSetLoopRange { get; set; }
        internal bool CanToggleBranchRecompute { get; set; }
        internal string PauseResumeLabel { get; set; } = string.Empty;
        internal string BranchRecomputeLabel { get; set; } = string.Empty;
        internal string BranchRecomputeRiskText { get; set; } = string.Empty;
        internal string JumpFrameInputText { get; set; } = string.Empty;
        internal string ContextText { get; set; } = string.Empty;
        internal string RangeText { get; set; } = string.Empty;
        internal string StatusText { get; set; } = string.Empty;

        internal void ClearTimelineDetails()
        {
            _timelineDetails.Clear();
            SelectedDetail = null;
        }

        internal void AddTimelineDetail(CharacterTestRuntimeAiLabTimelineDetailViewModel detail)
        {
            string stableKey = detail?.SourceItem?.StableKey;
            if (!string.IsNullOrEmpty(stableKey))
                _timelineDetails[stableKey] = detail;
        }

        internal bool TryGetTimelineDetail(
            CharacterTestRuntimeAiLabTimelineItem item,
            out CharacterTestRuntimeAiLabTimelineDetailViewModel detail)
        {
            detail = null;
            return item != null
                && !string.IsNullOrEmpty(item.StableKey)
                && _timelineDetails.TryGetValue(item.StableKey, out detail);
        }
    }

    /// <summary>Builds the shared semantic event text used by timeline chips, tooltips and details.</summary>
    /// <remarks>LowFreqAlloc: UI projection only; never called from Runtime AI Planner Tick.</remarks>
    internal static class CharacterTestRuntimeAiLabTimelineDetailProjector
    {
        internal static CharacterTestRuntimeAiLabTimelineDetailViewModel Project(
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            if (item == null)
                return null;

            ResolveBehaviorContext(
                item,
                controller,
                out string behaviorName,
                out string stageName);
            var detail = new CharacterTestRuntimeAiLabTimelineDetailViewModel
            {
                SourceItem = item,
                Title = ResolveTitle(item),
                MetaText = BuildMetaText(item),
                FrameText = BuildFrameText(item),
                ParticipantsText = BuildParticipantsText(item)
            };
            FillSemanticLines(detail, item, behaviorName, stageName);
            detail.TooltipText = BuildTooltip(detail);
            return detail;
        }

        internal static string BuildMetaText(CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (item == null)
                return string.Empty;
            string value = TrackLabel(item.TrackKind) + " · " + ItemKindLabel(item.ItemKind);
            switch (item.Severity)
            {
                case CharacterTestRuntimeAiLabTimelineSeverity.Success:
                    return value + " · 已确认";
                case CharacterTestRuntimeAiLabTimelineSeverity.Warning:
                    return value + " · 需确认";
                case CharacterTestRuntimeAiLabTimelineSeverity.Error:
                    return value + " · 异常";
                default:
                    return value;
            }
        }

        internal static string BuildFrameText(CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (item == null)
                return string.Empty;
            if (item.StartFrame == item.EndFrame)
                return "第 " + item.StartFrame.ToString(CultureInfo.InvariantCulture) + " 帧";
            return "观测区间  "
                + item.StartFrame.ToString(CultureInfo.InvariantCulture)
                + "  →  "
                + item.EndFrame.ToString(CultureInfo.InvariantCulture)
                + "    ·    共 "
                + (item.EndFrame - item.StartFrame + 1).ToString(CultureInfo.InvariantCulture)
                + " 帧";
        }

        internal static string BuildParticipantsText(
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (item == null)
                return string.Empty;
            string text = item.AgentEntityId > 0
                ? "涉及对象  智能体 "
                    + item.AgentEntityId.ToString(CultureInfo.InvariantCulture)
                : "涉及对象  系统";
            if (item.RelatedEntityId > 0)
            {
                text += "  →  目标 "
                    + item.RelatedEntityId.ToString(CultureInfo.InvariantCulture);
            }
            return text;
        }

        internal static bool ContainsOpaqueIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            for (int start = 0; start + 36 <= value.Length; start++)
            {
                if (Guid.TryParse(value.Substring(start, 36), out _))
                    return true;
            }

            int tokenStart = 0;
            for (int i = 0; i <= value.Length; i++)
            {
                bool boundary = i == value.Length || IsTokenBoundary(value[i]);
                if (!boundary)
                    continue;
                int length = i - tokenStart;
                if (length >= 16 && IsHexadecimalToken(value, tokenStart, length))
                    return true;
                tokenStart = i + 1;
            }
            return false;
        }

        private static void FillSemanticLines(
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail,
            CharacterTestRuntimeAiLabTimelineItem item,
            string behaviorName,
            string stageName)
        {
            switch (item.TrackKind)
            {
                case CharacterTestRuntimeAiLabTimelineTrackKind.Perception:
                    FillPerceptionLines(detail, item);
                    return;
                case CharacterTestRuntimeAiLabTimelineTrackKind.Decision:
                    detail.DetailLine1 = "行为  " + behaviorName;
                    detail.DetailLine2 = "阶段  " + stageName;
                    detail.DetailLine3 = "决策  " + ResolveExecutionStatus(item);
                    return;
                case CharacterTestRuntimeAiLabTimelineTrackKind.Action:
                    detail.DetailLine1 = "行为  " + behaviorName;
                    detail.DetailLine2 = "动作  " + stageName;
                    detail.DetailLine3 = "执行  " + ResolveExecutionStatus(item);
                    return;
                case CharacterTestRuntimeAiLabTimelineTrackKind.Result:
                    detail.DetailLine1 = "行为  " + behaviorName;
                    detail.DetailLine2 = "阶段  " + stageName;
                    detail.DetailLine3 = "结果  " + ResolveExecutionStatus(item);
                    return;
                default:
                    FillWarningLines(detail, item);
                    return;
            }
        }

        private static void FillPerceptionLines(
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail,
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (!item.HasPerceptionSnapshot)
            {
                detail.DetailLine1 = "感知  " + ResolveTitle(item);
                detail.DetailLine2 = "目标  " + (item.RelatedEntityId > 0
                    ? "目标 " + item.RelatedEntityId.ToString(CultureInfo.InvariantCulture)
                    : "未发现目标");
                detail.DetailLine3 = "结果  已记录本次感知变化";
                return;
            }

            detail.DetailLine1 = item.PerceptionTargetExists
                ? "目标  目标 " + item.RelatedEntityId.ToString(CultureInfo.InvariantCulture)
                : "目标  未发现目标";
            detail.DetailLine2 = item.PerceptionTargetExists
                ? "距离  "
                    + item.PerceptionStartDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + " → "
                    + item.PerceptionEndDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + " m"
                : "距离  无距离数据";
            detail.DetailLine3 = item.PerceptionTargetExists
                ? "状态  "
                    + (item.PerceptionTargetVisible ? "可见" : "不可见")
                    + " · "
                    + (item.PerceptionTargetDead ? "已死亡" : "未死亡")
                    + " · "
                    + (item.PerceptionLineOfSightBlocked ? "视线受阻" : "视线畅通")
                    + " · "
                    + (item.PerceptionInAttackRange ? "攻击范围内" : "攻击范围外")
                : "状态  等待发现有效目标";
        }

        private static void FillWarningLines(
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail,
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            string reason = HumanizeReason(item.ReasonCode);
            detail.DetailLine1 = "问题  " + ResolveTitle(item);
            detail.DetailLine2 = "影响  " + (item.Severity == CharacterTestRuntimeAiLabTimelineSeverity.Error
                ? "本次决策或执行没有按预期完成"
                : "当前记录需要进一步确认");
            detail.DetailLine3 = "建议  " + (reason == "未提供具体原因"
                ? "检查目标、距离和行为切换条件"
                : reason);
        }

        internal static string ResolveExecutionStatus(
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            string reason = HumanizeReason(item.ReasonCode);
            if (item.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                || item.PlannerLogicKind == CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected)
            {
                return "未执行：" + reason;
            }

            if (item.RuntimeEffectKind != CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved)
            {
                switch (item.RuntimeEffectKind)
                {
                    case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started:
                        return "动作已开始";
                    case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running:
                        return "动作执行中";
                    case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed:
                        return "动作已完成";
                    case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Cancelled:
                        return "动作已取消";
                    case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed:
                        return "未执行：" + reason;
                }
            }

            switch (item.PlannerLogicKind)
            {
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed:
                    return item.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued
                        ? "已入队 · 尚未执行"
                        : "已提交";
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Held:
                    return "保持当前行为";
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Pending:
                    return "等待切换条件";
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Fallback:
                    return "已使用兜底行为";
            }

            if (item.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued)
                return "已入队 · 尚未执行";
            switch (item.EventKind)
            {
                case CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted:
                    return "动作已开始";
                case CharacterTestRuntimeAiLabTimelineEventKind.ActionEnded:
                    return "动作已结束";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered:
                    return "已进入该阶段";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionPending:
                    return "等待切换条件";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted:
                    return "阶段切换已完成";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorCardEnded:
                    return "行为已结束";
            }
            if (item.Severity == CharacterTestRuntimeAiLabTimelineSeverity.Error)
                return "未执行：" + reason;
            return "已记录本次状态";
        }

        private static void ResolveBehaviorContext(
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            out string behaviorName,
            out string stageName)
        {
            behaviorName = string.Empty;
            stageName = string.Empty;
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = null;
            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            CharacterTestRuntimeAiLabBehaviorGroupDraft group = null;
            int entryIndex = -1;
            if (controller != null
                && controller.TryResolveBehaviorEntry(
                    item.BehaviorLoadoutId,
                    item.BehaviorEntryId,
                    out group,
                    out entryIndex)
                && entryIndex >= 0
                && entryIndex < group.Entries.Count)
            {
                entry = group.Entries[entryIndex];
            }
            else if (controller != null)
            {
                FindBehaviorEntry(
                    controller.DraftWorkspace,
                    item.BehaviorEntryId,
                    out group,
                    out entry);
            }

            CharacterTestRuntimeAiLabDraftWorkspace workspace = controller?.DraftWorkspace;
            if (entry != null
                && workspace != null
                && workspace.Cards.TryGetValue(entry.CardDraftId, out card))
            {
                behaviorName = SafeDisplayText(card.DisplayName, string.Empty);
            }

            string behaviorId = entry?.BehaviorId;
            if (string.IsNullOrEmpty(behaviorName)
                && !string.IsNullOrEmpty(behaviorId)
                && CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    behaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
            {
                behaviorName = SafeDisplayText(definition.DisplayName, string.Empty);
            }
            if (string.IsNullOrEmpty(behaviorName)
                && !string.IsNullOrEmpty(item.BehaviorEntryId)
                && CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    item.BehaviorEntryId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition directDefinition))
            {
                behaviorName = SafeDisplayText(directDefinition.DisplayName, string.Empty);
            }
            if (string.IsNullOrEmpty(behaviorName))
                behaviorName = ActionKindLabel(item.FallbackActionKind, "未识别行为");

            CharacterTestRuntimeAiLabLogicStage stage = FindStage(card, item.BehaviorStepId);
            if (stage != null)
            {
                stageName = SafeDisplayText(stage.Title, string.Empty);
                if (string.IsNullOrEmpty(stageName))
                    stageName = StageKindLabel(stage.Kind, stage.LegacyActionKind, stage.MovementStrategy);
            }
            if (string.IsNullOrEmpty(stageName))
                stageName = SemanticToken(item.BehaviorStepId, string.Empty);
            if (string.IsNullOrEmpty(stageName))
                stageName = ActionKindLabel(item.FallbackActionKind, "未命名阶段");
        }

        private static void FindBehaviorEntry(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string runtimeIdentity,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out CharacterTestRuntimeAiLabBehaviorEntryDraft entry)
        {
            group = null;
            entry = null;
            if (workspace == null || string.IsNullOrEmpty(runtimeIdentity))
                return;
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> pair
                in workspace.Groups)
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft candidateGroup = pair.Value;
                for (int index = 0;
                    candidateGroup != null && index < candidateGroup.Entries.Count;
                    index++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft candidate =
                        candidateGroup.Entries[index];
                    if (candidate == null)
                        continue;
                    bool matches = string.Equals(
                            candidate.RuntimeEntry?.EntryId,
                            runtimeIdentity,
                            StringComparison.Ordinal)
                        || string.Equals(candidate.EntryId, runtimeIdentity, StringComparison.Ordinal)
                        || string.Equals(candidate.CardDraftId, runtimeIdentity, StringComparison.Ordinal)
                        || string.Equals(candidate.BehaviorId, runtimeIdentity, StringComparison.Ordinal);
                    if (!matches
                        && workspace.Cards.TryGetValue(
                            candidate.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft candidateCard))
                    {
                        matches = string.Equals(
                                candidateCard.CardDraftId,
                                runtimeIdentity,
                                StringComparison.Ordinal)
                            || string.Equals(
                                candidateCard.PublishedStableId,
                                runtimeIdentity,
                                StringComparison.Ordinal);
                    }
                    if (!matches)
                        continue;
                    group = candidateGroup;
                    entry = candidate;
                    return;
                }
            }
        }

        private static CharacterTestRuntimeAiLabLogicStage FindStage(
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            string stageIdentity)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabLogicStage> stages =
                card?.LogicPayload?.Stages;
            if (stages == null || stages.Count == 0)
                return null;
            for (int i = 0; i < stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = stages[i];
                if (stage != null
                    && (string.Equals(stage.StageId, stageIdentity, StringComparison.Ordinal)
                        || (CharacterActionId.TryParse(stageIdentity, out CharacterActionId actionId)
                            && stage.CharacterActionId == actionId)))
                {
                    return stage;
                }
            }
            return null;
        }

        private static string ResolveTitle(CharacterTestRuntimeAiLabTimelineItem item)
        {
            string fallback = item.TrackKind == CharacterTestRuntimeAiLabTimelineTrackKind.Warnings
                ? "运行警告"
                : TrackLabel(item.TrackKind) + "事件";
            string title = SafeDisplayText(item.Label, string.Empty);
            if (!string.IsNullOrEmpty(title))
                return title;
            switch (item.EventKind)
            {
                case CharacterTestRuntimeAiLabTimelineEventKind.Command:
                    return "命令已提交";
                case CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted:
                    return "动作已开始";
                case CharacterTestRuntimeAiLabTimelineEventKind.ActionEnded:
                    return "动作已结束";
                case CharacterTestRuntimeAiLabTimelineEventKind.CombatHit:
                    return "攻击命中";
                case CharacterTestRuntimeAiLabTimelineEventKind.Damage:
                    return "造成伤害";
                case CharacterTestRuntimeAiLabTimelineEventKind.Warning:
                    return "运行警告";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered:
                    return "进入行为阶段";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionPending:
                    return "等待阶段切换";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted:
                    return "阶段切换完成";
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorCardEnded:
                    return "行为已结束";
                default:
                    return fallback;
            }
        }

        private static string HumanizeReason(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "未提供具体原因";
            string safe = SafeDisplayText(value, string.Empty);
            if (!string.IsNullOrEmpty(safe))
                return safe;
            string lower = value.ToLowerInvariant();
            if (lower.Contains("no-executable") || lower.Contains("no_executable"))
                return "没有满足条件的可执行行为";
            if (lower.Contains("target") && (lower.Contains("missing") || lower.Contains("not")))
                return "当前没有有效目标";
            if (lower.Contains("range"))
                return "目标不在有效范围内";
            if (lower.Contains("blocked") || lower.Contains("path"))
                return "移动路径或视线受阻";
            if (lower.Contains("transition"))
                return "尚未满足切换条件";
            if (lower.Contains("queue"))
                return "命令队列暂时无法接收新命令";
            if (lower.Contains("reject") || lower.Contains("invalid"))
                return "运行时拒绝了本次执行";
            return "具体原因暂不可读";
        }

        private static string SafeDisplayText(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || ContainsOpaqueIdentifier(value))
                return fallback;
            string trimmed = value.Trim();
            bool containsCjk = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] >= '\u4e00' && trimmed[i] <= '\u9fff')
                {
                    containsCjk = true;
                    break;
                }
            }
            if (containsCjk)
                return trimmed.Replace(" | ", " · ");
            return SemanticToken(trimmed, fallback);
        }

        private static string SemanticToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || ContainsOpaqueIdentifier(value))
                return fallback;
            string lower = value.ToLowerInvariant();
            if (lower.Contains("nearest") && lower.Contains("enemy"))
                return "最近敌人";
            if (lower.Contains("observe") || lower.Contains("perception"))
                return "观察目标";
            if (lower.Contains("wait") || lower.Contains("hold"))
                return "等待/保持";
            if (lower.Contains("approach") || lower.Contains("pursuit") || lower.Contains("engage"))
                return "接近目标";
            if (lower.Contains("retreat"))
                return "撤退";
            if (lower.Contains("strafe"))
                return "侧移";
            if (lower.Contains("attack") || lower.Contains("damage"))
                return "攻击";
            if (lower.Contains("dodge") || lower.Contains("evade"))
                return "闪避";
            if (lower.Contains("move"))
                return "移动";
            if (lower.Contains("complete") || lower.Contains("success"))
                return "已完成";
            if (lower.Contains("cancel"))
                return "已取消";
            if (lower.Contains("fail") || lower.Contains("reject"))
                return "执行失败";
            return fallback;
        }

        private static string StageKindLabel(
            CharacterTestRuntimeAiLabCardStepKind kind,
            CharacterTestRuntimeAiPlannerActionKind legacyActionKind,
            CharacterTestRuntimeAiLabMovementStrategy movementStrategy)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return "使用能力";
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return "执行动作";
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return movementStrategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
                        ? "保持距离"
                        : movementStrategy == CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard
                            ? "环境避险"
                            : movementStrategy == CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked
                                ? "受阻横移"
                                : "接近目标";
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    return "等待/保持";
                case CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly:
                    return ActionKindLabel(legacyActionKind, "执行动作");
                case CharacterTestRuntimeAiLabCardStepKind.Logic:
                    return "逻辑阶段";
                default:
                    return "未命名阶段";
            }
        }

        private static string ActionKindLabel(
            CharacterTestRuntimeAiPlannerActionKind kind,
            string fallback)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return "接近目标";
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return "侧移";
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return "撤退";
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return "基础攻击";
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return "使用能力";
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return "闪避";
                default:
                    return fallback;
            }
        }

        private static string BuildTooltip(
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail)
        {
            return detail.Title
                + "\n" + detail.FrameText
                + "\n" + detail.DetailLine1
                + "\n" + detail.DetailLine2
                + "\n" + detail.DetailLine3
                + "\n" + detail.ParticipantsText;
        }

        private static string TrackLabel(CharacterTestRuntimeAiLabTimelineTrackKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineTrackKind.Perception:
                    return "感知";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Decision:
                    return "决策";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Action:
                    return "动作";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Result:
                    return "结果";
                default:
                    return "警告";
            }
        }

        private static string ItemKindLabel(CharacterTestRuntimeAiLabTimelineItemKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineItemKind.Marker:
                    return "单帧";
                case CharacterTestRuntimeAiLabTimelineItemKind.Span:
                    return "区间";
                case CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack:
                    return "同帧集合";
                default:
                    return "密度聚合";
            }
        }

        private static bool IsTokenBoundary(char character)
        {
            return char.IsWhiteSpace(character)
                || character == '|'
                || character == '·'
                || character == ':'
                || character == '/'
                || character == '\\'
                || character == ','
                || character == ';'
                || character == '('
                || character == ')'
                || character == '['
                || character == ']';
        }

        private static bool IsHexadecimalToken(string value, int start, int length)
        {
            int hexadecimalCount = 0;
            for (int i = start; i < start + length; i++)
            {
                char character = value[i];
                if ((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F'))
                {
                    hexadecimalCount++;
                    continue;
                }
                if (character != '-' && character != '_')
                    return false;
            }
            return hexadecimalCount >= 12;
        }
    }

    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
    {
        internal CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel RuntimeFrame { get; set; } =
            new CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel();
    }
}
