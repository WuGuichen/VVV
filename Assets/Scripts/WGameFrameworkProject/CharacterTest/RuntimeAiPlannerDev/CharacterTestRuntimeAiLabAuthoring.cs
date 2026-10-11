using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabAuthoringMode
    {
        Gameplay = 0,
        Developer = 1
    }

    internal enum CharacterTestRuntimeAiLabCandidateState
    {
        None = 0,
        Compiling = 1,
        CompileFailed = 2,
        PreviewPending = 3,
        PreviewComplete = 4,
        ApplyPending = 5,
        Applied = 6,
        ApplyFailed = 7
    }

    /// <summary>
    /// Immutable, low-frequency projection of a compiled Runtime AI Lab draft.
    /// </summary>
    /// <remarks>LowFreqAlloc: created only after explicit authoring commands.</remarks>
    internal sealed class CharacterTestRuntimeAiLabCandidateRevision
    {
        private readonly CharacterTestRuntimeAiPlannerBehaviorCompositionProfile _profile;
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabRevisionCardIdentity> _cardIdentities;

        private CharacterTestRuntimeAiLabCandidateRevision(
            int revision,
            long authorityHash,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiLabCandidateComparisonReport comparison,
            CharacterTestRuntimeAiLabCompiledLogicProgram[] logicPrograms,
            CharacterTestRuntimeAiLabRevisionCardIdentity[] cardIdentities,
            string groupDraftId = "", int testTeamId = -1, int testSlotIndex = -1)
        {
            Revision = Math.Max(1, revision);
            AuthorityHash = authorityHash;
            _profile = profile.Clone();
            Comparison = comparison;
            GroupDraftId = groupDraftId ?? string.Empty;
            TestTeamId = testTeamId; TestSlotIndex = testSlotIndex;
            CharacterTestRuntimeAiLabCompiledLogicProgram[] programCopy =
                (CharacterTestRuntimeAiLabCompiledLogicProgram[])logicPrograms.Clone();
            LogicPrograms = Array.AsReadOnly(programCopy);
            CharacterTestRuntimeAiLabRevisionCardIdentity[] identityCopy =
                (CharacterTestRuntimeAiLabRevisionCardIdentity[])cardIdentities.Clone();
            _cardIdentities = Array.AsReadOnly(identityCopy);
        }

        public int Revision { get; }
        public long AuthorityHash { get; }
        /// <remarks>LowFreqAlloc: returns an isolated mutable copy for apply or diagnostics.</remarks>
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Profile => _profile.Clone();
        public CharacterTestRuntimeAiLabCandidateComparisonReport Comparison { get; }
        public string GroupDraftId { get; }
        internal int TestTeamId { get; }
        internal int TestSlotIndex { get; }
        internal bool IsSingleCardTest => TestTeamId >= 0 && TestSlotIndex >= 0;
        internal CharacterTestRuntimeAiLabCandidateRevision WithSingleCardTestTarget(int team, int slot)
        {
            if (LogicPrograms.Count != 1 || _cardIdentities.Count != 1)
                throw new InvalidOperationException("Single-card testing requires exactly one compiled card.");
            return new CharacterTestRuntimeAiLabCandidateRevision(Revision, AuthorityHash, _profile, Comparison,
                new List<CharacterTestRuntimeAiLabCompiledLogicProgram>(LogicPrograms).ToArray(),
                new List<CharacterTestRuntimeAiLabRevisionCardIdentity>(_cardIdentities).ToArray(), GroupDraftId, team, slot);
        }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicProgram> LogicPrograms { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabRevisionCardIdentity> CardIdentities => _cardIdentities;

        /// <remarks>LowFreqAlloc: validates and freezes an explicit authoring candidate.</remarks>
        public static bool TryCreate(
            int revision,
            long authorityHash,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiLabCandidateComparisonReport comparison,
            CharacterTestRuntimeAiLabCompiledLogicProgram[] logicPrograms,
            CharacterTestRuntimeAiLabRevisionCardIdentity[] cardIdentities,
            string groupDraftId,
            out CharacterTestRuntimeAiLabCandidateRevision candidate,
            out string diagnostic)
        {
            candidate = null;
            if (!ValidateBindings(profile, logicPrograms, cardIdentities, out diagnostic))
                return false;

            candidate = new CharacterTestRuntimeAiLabCandidateRevision(
                revision,
                authorityHash,
                profile,
                comparison,
                logicPrograms,
                cardIdentities,
                groupDraftId);
            diagnostic = string.Empty;
            return true;
        }

        private static bool ValidateBindings(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiLabCompiledLogicProgram[] programs,
            CharacterTestRuntimeAiLabRevisionCardIdentity[] identities,
            out string diagnostic)
        {
            if (profile == null || programs == null || identities == null
                || profile.Entries.Count != programs.Length
                || programs.Length != identities.Length)
            {
                diagnostic = "Candidate revision binding counts are inconsistent.";
                return false;
            }

            for (int i = 0; i < programs.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[i];
                CharacterTestRuntimeAiLabCompiledLogicProgram program = programs[i];
                CharacterTestRuntimeAiLabRevisionCardIdentity identity = identities[i];
                if (entry == null || program == null || identity == null
                    || identity.BindingIndex != i
                    || identity.CandidateSemanticHash != program.SemanticHash
                    || !string.Equals(identity.RuntimeEntryId, entry.EntryId, StringComparison.Ordinal))
                {
                    diagnostic = "Candidate revision binding identity is inconsistent at index " + i + ".";
                    return false;
                }
            }

            diagnostic = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Deterministic comparison of the active and candidate immutable authoring projections.
    /// </summary>
    /// <remarks>LowFreqAlloc: generated by candidate preflight, never from Tick.</remarks>
    internal sealed class CharacterTestRuntimeAiLabCandidateComparisonReport
    {
        public CharacterTestRuntimeAiLabCandidateComparisonReport(
            int changedCards,
            int changedSteps,
            int changedConditions,
            string firstDifference,
            long authorityHash,
            bool repeatable)
        {
            ChangedCards = Math.Max(0, changedCards);
            ChangedSteps = Math.Max(0, changedSteps);
            ChangedConditions = Math.Max(0, changedConditions);
            FirstDifference = firstDifference ?? string.Empty;
            AuthorityHash = authorityHash;
            Repeatable = repeatable;
        }

        public int ChangedCards { get; }
        public int ChangedSteps { get; }
        public int ChangedConditions { get; }
        public string FirstDifference { get; }
        public long AuthorityHash { get; }
        public bool Repeatable { get; }

        public string ToSummary()
        {
            string hash = unchecked((ulong)AuthorityHash).ToString("X16", CultureInfo.InvariantCulture);
            return "候选差异：行为卡 " + ChangedCards
                + "，步骤 " + ChangedSteps
                + "，条件 " + ChangedConditions
                + " | 首个差异：" + (string.IsNullOrEmpty(FirstDifference) ? "无" : FirstDifference)
                + " | authority " + hash
                + (Repeatable ? "（可重复）" : "（不稳定）");
        }
    }

    internal static class CharacterTestRuntimeAiLabGameplayAuthoring
    {
        public const float MinimumStopDistance = 0.1f;
        public const float MaximumStopDistance = 10f;
        public const int MinimumThreatImpactFrames = 0;
        public const int MaximumThreatImpactFrames = 240;
        public const int MinimumDodgeCooldownFrames = 0;
        public const int MaximumDodgeCooldownFrames = 600;

        private static readonly float[] DistancePresets = { 1f, 2f, 3f, 5f, 7f, 10f };
        private static readonly int[] ThreatWindowMinimumPresets = { 4, 6, 8 };
        private static readonly int[] ThreatWindowMaximumPresets = { 8, 12, 16 };

        public static CharacterTestRuntimeAiLabBehaviorCardPayload CreateApproachPreset()
        {
            return CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic();
        }

        public static CharacterTestRuntimeAiLabBehaviorCardPayload CycleTriggerDistance(
            CharacterTestRuntimeAiLabBehaviorCardPayload source)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload = source?.Clone()
                ?? CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic();
            CharacterTestRuntimeAiLabCardCondition distance = null;
            for (int i = 0; i < payload.TriggerConditions.Count; i++)
            {
                if (payload.TriggerConditions[i].Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetDistance)
                    continue;
                distance = payload.TriggerConditions[i];
                break;
            }

            if (distance == null)
            {
                distance = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = "trigger.distance",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual,
                    Value = DistancePresets[2]
                };
                payload.TriggerConditions.Add(distance);
                return payload;
            }

            float current = distance.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between
                ? distance.MaxValue
                : distance.Value;
            float next = DistancePresets[0];
            for (int i = 0; i < DistancePresets.Length; i++)
            {
                if (DistancePresets[i] <= current + 0.001f)
                    continue;
                next = DistancePresets[i];
                break;
            }
            if (distance.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between)
                distance.MaxValue = Math.Max(distance.Value, next);
            else
                distance.Value = next;
            return payload;
        }

        public static bool TrySetStopDistance(
            CharacterTestRuntimeAiLabBehaviorCardPayload source,
            float stopDistance,
            out CharacterTestRuntimeAiLabBehaviorCardPayload result)
        {
            result = null;
            if (source == null
                || float.IsNaN(stopDistance)
                || float.IsInfinity(stopDistance)
                || stopDistance < MinimumStopDistance
                || stopDistance > MaximumStopDistance)
            {
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardPayload payload = source.Clone();
            CharacterTestRuntimeAiLabCardCondition distance = FindTrigger(
                payload,
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance);
            if (distance == null)
                return false;

            float rounded = (float)Math.Round(stopDistance, 1, MidpointRounding.AwayFromZero);
            distance.Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan;
            distance.Value = rounded;
            distance.MaxValue = 0f;
            for (int i = 0; i < payload.Steps.Count; i++)
            {
                CharacterTestRuntimeAiLabCardStep step = payload.Steps[i];
                if (step.LegacyActionKind == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                    || step.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                {
                    step.MoveMinDistance = rounded;
                }
            }
            result = payload;
            return true;
        }

        public static CharacterTestRuntimeAiLabBehaviorCardPayload CycleFailureOutcome(
            CharacterTestRuntimeAiLabBehaviorCardPayload source,
            int stepIndex)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload = source?.Clone();
            if (payload == null || payload.Steps.Count == 0)
                return payload;
            int index = Math.Max(0, Math.Min(stepIndex, payload.Steps.Count - 1));
            CharacterTestRuntimeAiLabCardStep step = payload.Steps[index];
            CharacterTestRuntimeAiLabCardBranch failure = null;
            for (int i = 0; i < step.Branches.Count; i++)
            {
                if (step.Branches[i].Trigger == CharacterTestRuntimeAiLabCardBranchTrigger.Failure)
                {
                    failure = step.Branches[i];
                    break;
                }
            }
            if (failure == null)
            {
                failure = new CharacterTestRuntimeAiLabCardBranch
                {
                    BranchId = step.StepId + ".failure",
                    Trigger = CharacterTestRuntimeAiLabCardBranchTrigger.Failure,
                    Priority = 0,
                    TargetStepId = CharacterTestRuntimeAiLabCardBranch.EndTarget,
                    EndReason = "Replan"
                };
                step.Branches.Add(failure);
            }
            else if (index + 1 < payload.Steps.Count
                && string.Equals(failure.TargetStepId, CharacterTestRuntimeAiLabCardBranch.EndTarget, StringComparison.Ordinal))
            {
                failure.TargetStepId = payload.Steps[index + 1].StepId;
                failure.EndReason = string.Empty;
                failure.Label = "Fallback";
            }
            else
            {
                failure.TargetStepId = CharacterTestRuntimeAiLabCardBranch.EndTarget;
                failure.EndReason = "Replan";
                failure.Label = "Replan";
            }
            return payload;
        }

        public static CharacterTestRuntimeAiLabBehaviorCardPayload CycleThreatWindow(
            CharacterTestRuntimeAiLabBehaviorCardPayload source)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload = source?.Clone()
                ?? CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic();
            CharacterTestRuntimeAiLabCardCondition window = FindTrigger(
                payload,
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames);
            if (window == null)
                return CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic();

            int nextIndex = 0;
            for (int i = 0; i < ThreatWindowMinimumPresets.Length; i++)
            {
                if (Math.Abs(window.Value - ThreatWindowMinimumPresets[i]) < 0.001f
                    && Math.Abs(window.MaxValue - ThreatWindowMaximumPresets[i]) < 0.001f)
                {
                    nextIndex = (i + 1) % ThreatWindowMinimumPresets.Length;
                    break;
                }
            }
            window.Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between;
            window.Value = ThreatWindowMinimumPresets[nextIndex];
            window.MaxValue = ThreatWindowMaximumPresets[nextIndex];
            return payload;
        }

        public static bool TrySetThreatWindow(
            CharacterTestRuntimeAiLabBehaviorCardPayload source,
            int minimumImpactFrames,
            int maximumImpactFrames,
            out CharacterTestRuntimeAiLabBehaviorCardPayload result)
        {
            result = null;
            if (source == null
                || minimumImpactFrames < MinimumThreatImpactFrames
                || maximumImpactFrames > MaximumThreatImpactFrames
                || minimumImpactFrames > maximumImpactFrames)
            {
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardPayload payload = source.Clone();
            CharacterTestRuntimeAiLabCardCondition window = FindTrigger(
                payload,
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames);
            if (window == null)
                return false;
            window.Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between;
            window.Value = minimumImpactFrames;
            window.MaxValue = maximumImpactFrames;
            result = payload;
            return true;
        }

        public static bool TryGetThreatWindow(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            out int minimumImpactFrames,
            out int maximumImpactFrames)
        {
            CharacterTestRuntimeAiLabCardCondition window = FindTrigger(
                payload,
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames);
            if (window == null)
            {
                minimumImpactFrames = 0;
                maximumImpactFrames = 0;
                return false;
            }
            minimumImpactFrames = Math.Max(0, (int)Math.Round(window.Value));
            maximumImpactFrames = Math.Max(minimumImpactFrames, (int)Math.Round(window.MaxValue));
            return true;
        }

        public static bool TryGetStopDistance(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            out float stopDistance)
        {
            CharacterTestRuntimeAiLabCardCondition distance = FindTrigger(
                payload,
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance);
            if (distance == null)
            {
                stopDistance = 0f;
                return false;
            }
            stopDistance = Math.Max(0f, distance.Value);
            return true;
        }

        public static bool HasAdvancedSettings(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            if (payload == null)
                return false;
            for (int i = 0; i < payload.Steps.Count; i++)
            {
                CharacterTestRuntimeAiLabCardStep step = payload.Steps[i];
                if (step.MonitorConditions.Count > 0 || step.Branches.Count > 2)
                    return true;
                for (int branchIndex = 0; branchIndex < step.Branches.Count; branchIndex++)
                {
                    CharacterTestRuntimeAiLabCardBranch branch = step.Branches[branchIndex];
                    if (branch.Conditions.Count > 0 || branch.WindowStartFrame > 0 || branch.WindowEndFrame > 0)
                        return true;
                }
            }
            return false;
        }

        public static string BuildGameplaySummary(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            if (payload == null)
                return "行为卡不可用。";
            var actions = new List<string>(payload.Steps.Count);
            for (int i = 0; i < payload.Steps.Count; i++)
                actions.Add(FormatAction(payload.Steps[i]));
            string advanced = HasAdvancedSettings(payload) ? "\n包含高级设置（开发者模式可查看，玩法模式不会覆盖）。" : string.Empty;
            return "何时执行：" + FormatTriggers(payload.TriggerConditions)
                + "\n执行什么：" + string.Join(" → ", actions)
                + "\n成功后：按步骤连线继续或结束"
                + "\n失败后：备用动作 / 重新决策 / 结束"
                + advanced;
        }

        public static string BuildTriggerSummary(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            if (payload == null)
                return "行为卡不可用";
            if (string.Equals(payload.EntryStepId, "approach", StringComparison.Ordinal)
                && TryGetStopDistance(payload, out float stopDistance))
            {
                return "目标存在\n路径可达\n目标距离 > "
                    + stopDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + " m；完成 ≤ "
                    + stopDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + " m";
            }
            if (string.Equals(payload.EntryStepId, "dodge", StringComparison.Ordinal)
                && TryGetThreatWindow(payload, out int minimum, out int maximum))
            {
                return "目标攻击预计命中自己\n预计命中窗口 "
                    + minimum.ToString(CultureInfo.InvariantCulture)
                    + "–"
                    + maximum.ToString(CultureInfo.InvariantCulture)
                    + " 帧\n本次攻击尚未响应且冷却就绪";
            }
            return FormatTriggers(payload.TriggerConditions);
        }

        public static string BuildActionSummary(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            if (payload == null || payload.Steps.Count == 0)
                return "尚未配置动作";
            if (string.Equals(payload.EntryStepId, "approach", StringComparison.Ordinal))
                return "持续靠近目标（模板固定动作）";
            if (string.Equals(payload.EntryStepId, "dodge", StringComparison.Ordinal))
                return "播放侧向闪避动作（模板固定动作）";
            var actions = new List<string>(payload.Steps.Count);
            for (int i = 0; i < payload.Steps.Count; i++)
                actions.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + FormatAction(payload.Steps[i]));
            return string.Join("  →  ", actions);
        }

        public static string BuildSuccessSummary(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            if (payload != null
                && (string.Equals(payload.EntryStepId, "approach", StringComparison.Ordinal)
                    || string.Equals(payload.EntryStepId, "dodge", StringComparison.Ordinal)))
            {
                return "下一次决策继续评估";
            }
            return payload == null || payload.Steps.Count == 0
                ? "结束"
                : "继续下一步；最后一步完成后重新决策";
        }

        public static string BuildFailureSummary(CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            if (payload != null && string.Equals(payload.EntryStepId, "approach", StringComparison.Ordinal))
                return "路径不可用时重新决策";
            if (payload != null && string.Equals(payload.EntryStepId, "dodge", StringComparison.Ordinal))
                return "动作被拒绝时记录原因并重新决策";
            return payload == null || payload.Steps.Count == 0
                ? "重新决策"
                : "执行步骤配置的备用出口；未配置时重新决策";
        }

        internal static string FormatTriggers(IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return "始终可评估";
            var values = new List<string>(conditions.Count);
            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance)
                {
                    values.Add(FormatNumericCondition("目标距离", "m", condition));
                }
                else if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames)
                {
                    values.Add(FormatNumericCondition("预计命中", "帧", condition));
                }
                else if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf)
                {
                    values.Add(condition.BoolValue ? "目标攻击预计命中自己" : "目标攻击不会命中自己");
                }
                else if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded)
                {
                    values.Add(condition.BoolValue ? "本次攻击已响应" : "本次攻击尚未响应");
                }
                else if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady)
                {
                    values.Add(condition.BoolValue ? "闪避冷却就绪" : "闪避冷却中");
                }
                else if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable)
                {
                    values.Add(condition.BoolValue ? "路径可达" : "路径不可达");
                }
                else
                {
                    values.Add(condition.Fact.ToString());
                }
            }
            return string.Join("、", values);
        }

        private static string FormatNumericCondition(
            string label,
            string unit,
            CharacterTestRuntimeAiLabCardCondition condition)
        {
            string value = condition.Value.ToString("0.#", CultureInfo.InvariantCulture);
            string suffix = string.IsNullOrEmpty(unit) ? string.Empty : " " + unit;
            switch (condition.Operator)
            {
                case CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual:
                    return label + " ≤ " + value + suffix;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual:
                    return label + " ≥ " + value + suffix;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan:
                    return label + " > " + value + suffix;
                case CharacterTestRuntimeAiLabCardConditionOperator.Equals:
                    return label + " = " + value + suffix;
                case CharacterTestRuntimeAiLabCardConditionOperator.NotEquals:
                    return label + " ≠ " + value + suffix;
                case CharacterTestRuntimeAiLabCardConditionOperator.Between:
                    return label + " " + value + "–"
                        + condition.MaxValue.ToString("0.#", CultureInfo.InvariantCulture)
                        + suffix;
                default:
                    return label + " " + condition.Operator + " " + value + suffix;
            }
        }

        private static CharacterTestRuntimeAiLabCardCondition FindTrigger(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            if (payload == null)
                return null;
            for (int i = 0; i < payload.TriggerConditions.Count; i++)
            {
                if (payload.TriggerConditions[i].Fact == fact)
                    return payload.TriggerConditions[i];
            }
            return null;
        }

        private static string FormatAction(CharacterTestRuntimeAiLabCardStep step)
        {
            if (step == null)
                return "无效步骤";
            switch (step.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget: return "接近目标";
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility: return "使用能力";
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction: return "角色动作";
                case CharacterTestRuntimeAiLabCardStepKind.Wait: return "等待";
                default: return string.IsNullOrWhiteSpace(step.Title) ? "运行时行为" : step.Title;
            }
        }
    }
}
