using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder
    {
        private static string BuildStageTimeText(
            int startFrame,
            int durationFrames,
            CharacterTestRuntimeAiLabLogicDurationMode durationMode)
        {
            string end = durationMode == CharacterTestRuntimeAiLabLogicDurationMode.Infinite
                ? "∞"
                : durationMode == CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent
                    ? "事件触发"
                    : FormatLogicFrame(startFrame + Math.Max(1, durationFrames));
            return FormatLogicFrame(startFrame) + " → " + end + " · " + FormatDurationMode(durationMode);
        }

        private static void FillStageCardSummaries(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                var detectorText = new StringBuilder(160);
                for (int detectorIndex = 0; detectorIndex < detectorCount; detectorIndex++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[detectorIndex];
                    if (detector.CardScope || detector.StageIndex != stageIndex)
                        continue;
                    if (detectorText.Length > 0)
                        detectorText.Append('\n');
                    detectorText.Append("P").Append(detector.Priority).Append(" · ")
                        .Append(detector.Title).Append(" · ").Append(detector.FactText);
                }

                var transitionText = new StringBuilder(160);
                for (int transitionIndex = 0; transitionIndex < transitionCount; transitionIndex++)
                {
                    CharacterTestRuntimeAiLabLogicTransitionViewModel transition = transitions[transitionIndex];
                    if (transition.SourceStageIndex >= 0 && transition.SourceStageIndex != stageIndex)
                        continue;
                    if (transitionText.Length > 0)
                        transitionText.Append('\n');
                    transitionText.Append("P").Append(transition.Priority).Append(" · ")
                        .Append(transition.Label).Append(" → ").Append(transition.TargetText)
                        .Append(" · ").Append(transition.PolicyText);
                }

                stages[stageIndex].DetectorSummaryText = detectorText.Length == 0
                    ? "无阶段级检测器"
                    : detectorText.ToString();
                stages[stageIndex].TransitionSummaryText = transitionText.Length == 0
                    ? "无离开路径 · 保持当前阶段"
                    : transitionText.ToString();
            }
        }

        private static int ResolveLogicTimelineDuration(
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            int duration = 60;
            for (int i = 0; program != null && i < program.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledLogicStage stage = program.Stages[i];
                duration = Math.Max(
                    duration,
                    stage.PlannedStartFrame + ResolveStageDisplayDuration(stage));
            }
            return duration;
        }

        private static int ResolveLogicTimelineDuration(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            int duration = 60;
            for (int i = 0; payload != null && i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage == null)
                    continue;
                duration = Math.Max(
                    duration,
                    stage.PlannedStartFrame + ResolveStageDisplayDuration(stage));
            }
            return duration;
        }

        private static int ResolveStageDisplayDuration(
            CharacterTestRuntimeAiLabCompiledLogicStage stage)
        {
            if (stage == null)
                return 1;
            if (stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed)
                return Math.Max(1, stage.PlannedDurationFrames);
            return Math.Max(60, stage.PreviewDurationFrames);
        }

        private static int ResolveStageDisplayDuration(CharacterTestRuntimeAiLabLogicStage stage)
        {
            if (stage == null)
                return 1;
            if (stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed)
                return Math.Max(1, stage.PlannedDurationFrames);
            return Math.Max(60, stage.PreviewDurationFrames);
        }

        private static int ResolveDetectorDisplayDuration(
            CharacterTestRuntimeAiLabCompiledLogicDetector detector,
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
                return Math.Max(1, detector.ActivationDurationFrames);
            if (detector.StageIndex >= 0 && detector.StageIndex < program.Stages.Count)
                return Math.Max(1, ResolveStageDisplayDuration(program.Stages[detector.StageIndex]) - detector.ActivationStartFrame);
            return Math.Max(60, ResolveLogicTimelineDuration(program) - detector.ActivationStartFrame);
        }

        private static int ResolveDetectorDisplayDuration(
            CharacterTestRuntimeAiLabLogicDetector detector,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (detector == null)
                return 1;
            if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
                return Math.Max(1, detector.ActivationDurationFrames);
            if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage)
            {
                for (int i = 0; payload != null && i < payload.Stages.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                    if (stage != null
                        && string.Equals(stage.StageId, detector.StageId, StringComparison.Ordinal))
                    {
                        return Math.Max(1, ResolveStageDisplayDuration(stage) - detector.ActivationStartFrame);
                    }
                }
            }
            return Math.Max(60, ResolveLogicTimelineDuration(payload) - detector.ActivationStartFrame);
        }

        private static string FormatDurationMode(CharacterTestRuntimeAiLabLogicDurationMode mode)
        {
            switch (mode)
            {
                case CharacterTestRuntimeAiLabLogicDurationMode.Fixed:
                    return "固定时长";
                case CharacterTestRuntimeAiLabLogicDurationMode.Infinite:
                    return "持续运行";
                default:
                    return "直到事件触发";
            }
        }

        private static void FillDraftDetectorDisplayOrder(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            int[] order)
        {
            int count = payload.Detectors.Count;
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                int insert = i - 1;
                while (insert >= 0)
                {
                    CharacterTestRuntimeAiLabLogicDetector left = payload.Detectors[order[insert]];
                    CharacterTestRuntimeAiLabLogicDetector right = payload.Detectors[order[insert + 1]];
                    int comparison;
                    if (left == null || right == null)
                        comparison = left == null ? (right == null ? 0 : 1) : -1;
                    else
                        comparison = right.Priority.CompareTo(left.Priority);
                    if (comparison == 0)
                    {
                        comparison = string.CompareOrdinal(
                            left?.DetectorId ?? string.Empty,
                            right?.DetectorId ?? string.Empty);
                    }
                    if (comparison <= 0)
                        break;
                    int swap = order[insert];
                    order[insert] = order[insert + 1];
                    order[insert + 1] = swap;
                    insert--;
                }
            }
        }

        private static int FindDraftStageIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage != null
                    && string.Equals(stage.StageId, stageId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int FindDraftDetectorIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector != null
                    && string.Equals(detector.DetectorId, detectorId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int[] BuildDetectorDisplayOrder(CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            var order = new int[program.Detectors.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
                int insert = i - 1;
                while (insert >= 0)
                {
                    CharacterTestRuntimeAiLabCompiledLogicDetector left = program.Detectors[order[insert]];
                    CharacterTestRuntimeAiLabCompiledLogicDetector right = program.Detectors[order[insert + 1]];
                    int comparison = right.Priority.CompareTo(left.Priority);
                    if (comparison == 0)
                        comparison = string.CompareOrdinal(left.DetectorId, right.DetectorId);
                    if (comparison <= 0)
                        break;
                    int swap = order[insert];
                    order[insert] = order[insert + 1];
                    order[insert + 1] = swap;
                    insert--;
                }
            }
            return order;
        }

        private static void FillDetectorDisplayOrder(
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            int[] order)
        {
            int count = program.Detectors.Count;
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                int insert = i - 1;
                while (insert >= 0)
                {
                    CharacterTestRuntimeAiLabCompiledLogicDetector left = program.Detectors[order[insert]];
                    CharacterTestRuntimeAiLabCompiledLogicDetector right = program.Detectors[order[insert + 1]];
                    int comparison = right.Priority.CompareTo(left.Priority);
                    if (comparison == 0)
                        comparison = string.CompareOrdinal(left.DetectorId, right.DetectorId);
                    if (comparison <= 0)
                        break;
                    int swap = order[insert];
                    order[insert] = order[insert + 1];
                    order[insert + 1] = swap;
                    insert--;
                }
            }
        }

        private static void EnsureTimelineCapacity(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            int stageCount,
            int detectorCount,
            int transitionCount)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline.Stages;
            if (stages.Length < stageCount)
            {
                Array.Resize(ref stages, Math.Max(stageCount, Math.Max(4, stages.Length * 2)));
                timeline.Stages = stages;
            }
            for (int i = 0; i < stageCount; i++)
            {
                if (stages[i] == null)
                    stages[i] = new CharacterTestRuntimeAiLabLogicStageViewModel();
            }

            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors = timeline.Detectors;
            if (detectors.Length < detectorCount)
            {
                Array.Resize(ref detectors, Math.Max(detectorCount, Math.Max(4, detectors.Length * 2)));
                timeline.Detectors = detectors;
            }
            for (int i = 0; i < detectorCount; i++)
            {
                if (detectors[i] == null)
                    detectors[i] = new CharacterTestRuntimeAiLabLogicDetectorViewModel();
            }

            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions = timeline.Transitions;
            if (transitions.Length < transitionCount)
            {
                Array.Resize(ref transitions, Math.Max(transitionCount, Math.Max(4, transitions.Length * 2)));
                timeline.Transitions = transitions;
            }
            for (int i = 0; i < transitionCount; i++)
            {
                if (transitions[i] == null)
                    transitions[i] = new CharacterTestRuntimeAiLabLogicTransitionViewModel();
            }

            int[] order = timeline.DetectorOrderScratch;
            if (order.Length < detectorCount)
            {
                Array.Resize(ref order, Math.Max(detectorCount, Math.Max(4, order.Length * 2)));
                timeline.DetectorOrderScratch = order;
            }
        }

        private static void FillConditionRows(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> source)
        {
            int count = source?.Count ?? 0;
            EnsureConditionCapacity(detector, count);
            detector.ConditionCount = count;
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabCompiledCondition condition = source[i];
                CharacterTestRuntimeAiLabLogicConditionViewModel row = detector.Conditions[i];
                row.ConditionId = condition.ConditionId;
                row.Fact = condition.Fact;
                row.Operator = condition.Operator;
                row.BoolValue = condition.BoolValue;
                row.Value = condition.Value;
                row.MaxValue = condition.MaxValue;
                row.AbilityId = condition.AbilityId;
                row.CharacterActionId = condition.CharacterActionId;
            }
        }

        private static void FillConditionRows(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> source)
        {
            int count = source?.Count ?? 0;
            EnsureConditionCapacity(detector, count);
            detector.ConditionCount = count;
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = source[i];
                CharacterTestRuntimeAiLabLogicConditionViewModel row = detector.Conditions[i];
                row.ConditionId = condition?.ConditionId ?? string.Empty;
                row.Fact = condition?.Fact ?? default;
                row.Operator = condition?.Operator ?? default;
                row.BoolValue = condition?.BoolValue ?? false;
                row.Value = condition?.Value ?? 0f;
                row.MaxValue = condition?.MaxValue ?? 0f;
                row.AbilityId = condition?.AbilityId ?? 0;
                row.CharacterActionId = condition?.CharacterActionId ?? default;
            }
        }

        private static void EnsureConditionCapacity(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            int count)
        {
            CharacterTestRuntimeAiLabLogicConditionViewModel[] conditions = detector.Conditions;
            if (conditions.Length < count)
            {
                Array.Resize(ref conditions, Math.Max(count, Math.Max(2, conditions.Length * 2)));
                detector.Conditions = conditions;
            }
            for (int i = 0; i < count; i++)
            {
                if (conditions[i] == null)
                    conditions[i] = new CharacterTestRuntimeAiLabLogicConditionViewModel();
            }
        }

        private static long ComputeCompiledStructureKey(
            string cardName,
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            unchecked
            {
                long hash = program.SemanticHash;
                hash = hash * 397L ^ StableWorkspaceTextHash(cardName);
                hash = hash * 397L ^ program.Stages.Count;
                hash = hash * 397L ^ program.Detectors.Count;
                hash = hash * 397L ^ program.Transitions.Count;
                IReadOnlyList<CharacterTestRuntimeAiLabLogicStageLayout> layouts = payload?.Layout?.Stages;
                for (int i = 0; layouts != null && i < layouts.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicStageLayout layout = layouts[i];
                    hash = hash * 397L ^ StableWorkspaceTextHash(layout.StageId);
                    hash = hash * 397L ^ layout.Column;
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(layout.Width);
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(layout.Offset);
                }
                return hash;
            }
        }

        private static long ComputeInvalidDraftStructureKey(
            string cardName,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            unchecked
            {
                long hash = -7046029254386353131L;
                hash = hash * 397L ^ StableWorkspaceTextHash(cardName);
                hash = hash * 397L ^ StableWorkspaceTextHash(payload.EntryStageId);
                hash = hash * 397L ^ StableWorkspaceTextHash(payload.Intent);
                hash = hash * 397L ^ StableWorkspaceTextHash(payload.RiskSummary);
                hash = HashConditions(hash, payload.ActivationConditions);
                hash = hash * 397L ^ payload.Stages.Count;
                for (int i = 0; i < payload.Stages.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                    if (stage == null)
                    {
                        hash = hash * 397L ^ -1;
                        continue;
                    }
                    hash = hash * 397L ^ StableWorkspaceTextHash(stage.StageId);
                    hash = hash * 397L ^ StableWorkspaceTextHash(stage.Title);
                    hash = hash * 397L ^ stage.Order;
                    hash = hash * 397L ^ (int)stage.Kind;
                    hash = hash * 397L ^ stage.AbilityId;
                    hash = hash * 397L ^ StableWorkspaceTextHash(stage.CharacterActionId);
                    hash = hash * 397L ^ (int)stage.LegacyActionKind;
                    hash = hash * 397L ^ (int)stage.MovementStrategy;
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(stage.MoveMinDistance);
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(stage.MoveMaxDistance);
                    hash = hash * 397L ^ stage.WaitFrames;
                    hash = hash * 397L ^ stage.PlannedStartFrame;
                    hash = hash * 397L ^ stage.PlannedDurationFrames;
                    hash = hash * 397L ^ stage.PreviewDurationFrames;
                    hash = hash * 397L ^ (int)stage.DurationMode;
                    hash = hash * 397L ^ StableWorkspaceTextHash(stage.Note);
                    hash = HashConditions(hash, stage.EntryConditions);
                }

                hash = hash * 397L ^ payload.Detectors.Count;
                for (int i = 0; i < payload.Detectors.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                    if (detector == null)
                    {
                        hash = hash * 397L ^ -1;
                        continue;
                    }
                    hash = hash * 397L ^ StableWorkspaceTextHash(detector.DetectorId);
                    hash = hash * 397L ^ StableWorkspaceTextHash(detector.Title);
                    hash = hash * 397L ^ StableWorkspaceTextHash(detector.StageId);
                    hash = hash * 397L ^ (int)detector.Scope;
                    hash = hash * 397L ^ (int)detector.Trigger;
                    hash = hash * 397L ^ detector.Priority;
                    hash = hash * 397L ^ detector.ActivationStartFrame;
                    hash = hash * 397L ^ detector.ActivationDurationFrames;
                    hash = hash * 397L ^ (int)detector.WindowEndMode;
                    hash = HashConditions(hash, detector.Conditions);
                }

                hash = hash * 397L ^ payload.Transitions.Count;
                for (int i = 0; i < payload.Transitions.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                    if (transition == null)
                    {
                        hash = hash * 397L ^ -1;
                        continue;
                    }
                    hash = hash * 397L ^ StableWorkspaceTextHash(transition.TransitionId);
                    hash = hash * 397L ^ StableWorkspaceTextHash(transition.DetectorId);
                    hash = hash * 397L ^ StableWorkspaceTextHash(transition.SourceStageId);
                    hash = hash * 397L ^ StableWorkspaceTextHash(transition.TargetStageId);
                    hash = hash * 397L ^ StableWorkspaceTextHash(transition.Label);
                    hash = hash * 397L ^ transition.Priority;
                    hash = hash * 397L ^ (int)transition.Policy;
                    hash = hash * 397L ^ StableWorkspaceTextHash(transition.EndReason);
                }

                IReadOnlyList<CharacterTestRuntimeAiLabLogicStageLayout> layouts = payload.Layout?.Stages;
                hash = hash * 397L ^ (layouts?.Count ?? 0);
                for (int i = 0; layouts != null && i < layouts.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicStageLayout layout = layouts[i];
                    if (layout == null)
                    {
                        hash = hash * 397L ^ -1;
                        continue;
                    }
                    hash = hash * 397L ^ StableWorkspaceTextHash(layout.StageId);
                    hash = hash * 397L ^ layout.Column;
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(layout.Width);
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(layout.Offset);
                }

                hash = hash * 397L ^ (diagnostics?.Count ?? 0);
                for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
                {
                    CharacterTestRuntimeAiLabCardDiagnostic diagnostic = diagnostics[i];
                    hash = hash * 397L ^ (int)diagnostic.Severity;
                    hash = hash * 397L ^ StableWorkspaceTextHash(diagnostic.Code);
                    hash = hash * 397L ^ StableWorkspaceTextHash(diagnostic.Path);
                    hash = hash * 397L ^ StableWorkspaceTextHash(diagnostic.Message);
                }
                return hash;
            }
        }

        /// <remarks>LowFreqAlloc. Runs only while projecting an invalid authoring draft.</remarks>
        private static bool TryResolvePrimaryBlockingDiagnostic(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            out CharacterTestRuntimeAiLabCardDiagnostic diagnostic,
            out CharacterTestRuntimeAiLabLogicElementKind kind,
            out int index)
        {
            diagnostic = default;
            kind = CharacterTestRuntimeAiLabLogicElementKind.None;
            index = -1;
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiLabCardDiagnostic candidate = diagnostics[i];
                if (candidate.Severity != CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error
                    || !TryResolveDiagnosticElement(payload, candidate.Path, out kind, out index))
                {
                    continue;
                }

                diagnostic = candidate;
                return true;
            }

            return false;
        }

        private static bool TryResolveDiagnosticElement(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string path,
            out CharacterTestRuntimeAiLabLogicElementKind kind,
            out int index)
        {
            kind = CharacterTestRuntimeAiLabLogicElementKind.None;
            index = -1;
            if (TryReadDiagnosticPathIndex(path, "$.stages[", payload?.Stages?.Count ?? 0, out index))
                kind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            else if (TryReadDiagnosticPathIndex(path, "$.detectors[", payload?.Detectors?.Count ?? 0, out index))
                kind = CharacterTestRuntimeAiLabLogicElementKind.Detector;
            else if (TryReadDiagnosticPathIndex(path, "$.transitions[", payload?.Transitions?.Count ?? 0, out index))
                kind = CharacterTestRuntimeAiLabLogicElementKind.Transition;
            return kind != CharacterTestRuntimeAiLabLogicElementKind.None;
        }

        private static bool TryReadDiagnosticPathIndex(
            string path,
            string prefix,
            int count,
            out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(path)
                || !path.StartsWith(prefix, StringComparison.Ordinal)
                || count <= 0)
            {
                return false;
            }

            int value = 0;
            int cursor = prefix.Length;
            int digitCount = 0;
            while (cursor < path.Length && path[cursor] >= '0' && path[cursor] <= '9')
            {
                value = value * 10 + path[cursor] - '0';
                cursor++;
                digitCount++;
            }

            if (digitCount == 0 || cursor >= path.Length || path[cursor] != ']' || value >= count)
                return false;
            index = value;
            return true;
        }

        private static void NormalizeInvalidDraftSelection(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            ref CharacterTestRuntimeAiLabLogicElementKind kind,
            ref int index)
        {
            int count;
            switch (kind)
            {
                case CharacterTestRuntimeAiLabLogicElementKind.Detector:
                    count = payload.Detectors.Count;
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Transition:
                    count = payload.Transitions.Count;
                    break;
                default:
                    kind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                    count = payload.Stages.Count;
                    break;
            }

            index = count > 0 ? Math.Max(0, Math.Min(index, count - 1)) : -1;
        }

        private static string BuildPrimaryRepairInstruction(
            CharacterTestRuntimeAiLabCardDiagnostic diagnostic,
            CharacterTestRuntimeAiLabLogicElementKind kind)
        {
            if (string.Equals(
                    diagnostic.Code,
                    "CARD_V2_DETECTOR_CONDITIONS",
                    StringComparison.Ordinal)
                && kind == CharacterTestRuntimeAiLabLogicElementKind.Detector)
            {
                return "修复：该检测器没有触发条件，请点击“添加条件”。";
            }

            if (kind == CharacterTestRuntimeAiLabLogicElementKind.Stage)
                return "修复：请检查当前展开阶段的动作与时长配置。";
            if (kind == CharacterTestRuntimeAiLabLogicElementKind.Detector)
                return "修复：请检查当前展开检测器的触发与监听配置。";
            if (kind == CharacterTestRuntimeAiLabLogicElementKind.Transition)
                return "修复：请检查当前展开出口的目标与执行策略。";
            return "草稿存在阻断问题；请查看诊断并修复后再应用到下一帧。";
        }

        private static long HashConditions(
            long hash,
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            unchecked
            {
                int count = conditions?.Count ?? 0;
                hash = hash * 397L ^ count;
                for (int i = 0; i < count; i++)
                {
                    CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                    if (condition == null)
                    {
                        hash = hash * 397L ^ -1;
                        continue;
                    }
                    hash = hash * 397L ^ StableWorkspaceTextHash(condition.ConditionId);
                    hash = hash * 397L ^ (int)condition.Fact;
                    hash = hash * 397L ^ (int)condition.Operator;
                    hash = hash * 397L ^ (condition.BoolValue ? 1 : 0);
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(condition.Value);
                    hash = hash * 397L ^ BitConverter.SingleToInt32Bits(condition.MaxValue);
                    hash = hash * 397L ^ condition.AbilityId;
                    hash = hash * 397L ^ StableWorkspaceTextHash(condition.CharacterActionId);
                }
                return hash;
            }
        }

        private static long StableWorkspaceTextHash(string value)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 1099511628211UL;
                }
                return (long)hash;
            }
        }

        private static long StableWorkspaceTextHash(CharacterActionId value)
        {
            return value.Value;
        }

        private static bool ContainsLogicEvent(
            CharacterTestRuntimeAiLabLogicEventBuffer events,
            CharacterTestRuntimeAiLabLogicEventKind kind,
            string detectorId,
            string transitionId,
            int entityId,
            string behaviorEntryId,
            long programSemanticHash,
            int profileSlot = -1,
            int cardInstanceStartFrame = -1)
        {
            for (int i = 0; events != null && i < events.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicEvent evt = events[i];
                if (evt.Kind == kind
                    && (entityId <= 0 || evt.EntityId == entityId)
                    && (string.IsNullOrEmpty(behaviorEntryId)
                        || string.Equals(evt.BehaviorEntryId, behaviorEntryId, StringComparison.Ordinal))
                    && (programSemanticHash == 0L || evt.ProgramSemanticHash == programSemanticHash)
                    && (profileSlot < 0 || evt.ProfileSlot == profileSlot)
                    && (cardInstanceStartFrame < 0 || evt.CardInstanceStartFrame == cardInstanceStartFrame)
                    && (string.IsNullOrEmpty(detectorId)
                        || string.Equals(evt.DetectorId, detectorId, StringComparison.Ordinal))
                    && (string.IsNullOrEmpty(transitionId)
                        || string.Equals(evt.TransitionId, transitionId, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
            return false;
        }

        private static int FindLogicEventFrame(
            CharacterTestRuntimeAiLabLogicEventBuffer events,
            CharacterTestRuntimeAiLabLogicEventKind kind,
            string transitionId,
            int entityId,
            string behaviorEntryId,
            long programSemanticHash,
            int profileSlot = -1,
            int cardInstanceStartFrame = -1)
        {
            for (int i = events != null ? events.Count - 1 : -1; i >= 0; i--)
            {
                CharacterTestRuntimeAiLabLogicEvent evt = events[i];
                if (evt.Kind == kind
                    && (entityId <= 0 || evt.EntityId == entityId)
                    && (string.IsNullOrEmpty(behaviorEntryId)
                        || string.Equals(evt.BehaviorEntryId, behaviorEntryId, StringComparison.Ordinal))
                    && (programSemanticHash == 0L || evt.ProgramSemanticHash == programSemanticHash)
                    && (profileSlot < 0 || evt.ProfileSlot == profileSlot)
                    && (cardInstanceStartFrame < 0 || evt.CardInstanceStartFrame == cardInstanceStartFrame)
                    && string.Equals(evt.TransitionId, transitionId, StringComparison.Ordinal))
                {
                    return evt.Frame;
                }
            }
            return -1;
        }

        private static string FormatLogicDetectorConditions(
            CharacterTestRuntimeAiLabCompiledLogicDetector detector)
        {
            if (detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge)
                return detector.Trigger.ToString();
            if (detector.Conditions.Count == 0)
                return "无条件";
            var builder = new StringBuilder(96);
            for (int i = 0; i < detector.Conditions.Count; i++)
            {
                if (i > 0)
                    builder.Append(" & ");
                builder.Append(FormatCompiledCondition(detector.Conditions[i]));
            }
            return builder.ToString();
        }

        private static string FormatCompiledCondition(
            CharacterTestRuntimeAiLabCompiledCondition condition)
        {
            var builder = new StringBuilder(64);
            builder.Append(FormatConditionFact(condition.Fact))
                .Append(' ')
                .Append(FormatConditionOperator(condition.Operator))
                .Append(' ');
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames)
            {
                builder.Append(condition.Value.ToString("0.##", CultureInfo.InvariantCulture));
                if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between)
                {
                    builder.Append("..").Append(
                        condition.MaxValue.ToString("0.##", CultureInfo.InvariantCulture));
                }
            }
            else
            {
                builder.Append(condition.BoolValue ? "是" : "否");
            }
            if (condition.AbilityId > 0)
                builder.Append(" 能力#").Append(condition.AbilityId.ToString(CultureInfo.InvariantCulture));
            if (condition.CharacterActionId.IsValid)
                builder.Append(" 动作:#").Append(condition.CharacterActionId.Value);
            return builder.ToString();
        }

        private static string FormatStepKind(CharacterTestRuntimeAiLabCardStepKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return "使用能力";
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return "使用角色动作";
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return "接近目标";
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    return "等待";
                case CharacterTestRuntimeAiLabCardStepKind.Logic:
                    return "纯逻辑阶段";
                default:
                    return "兼容运行时动作";
            }
        }

        private static string FormatConditionFact(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    return "目标存在";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    return "目标可见";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    return "目标死亡";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                    return "锁定受击框净空";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                    return "受击框距离可用";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    return "目标根距离";
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    return "路径可用";
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    return "能力就绪";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    return "自身动作运行中";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return "自身动作可打断";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    return "目标攻击预计命中自身";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                    return "预计命中剩余帧";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    return "本次攻击已响应";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    return "闪避冷却就绪";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    return "目标在攻击范围内";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    return "目标视线受阻";
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    return "附近存在危险";
                default:
                    return "角色动作状态";
            }
        }

        private static string FormatConditionOperator(CharacterTestRuntimeAiLabCardConditionOperator value)
        {
            switch (value)
            {
                case CharacterTestRuntimeAiLabCardConditionOperator.Equals:
                    return "等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.NotEquals:
                    return "不等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual:
                    return "小于等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual:
                    return "大于等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan:
                    return "大于";
                default:
                    return "介于";
            }
        }

        private static string BuildCompileDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return "等待编译，当前没有诊断信息。";
            var builder = new StringBuilder(512);
            builder.Append("编译失败——草稿已保留，当前求值器未改变");
            for (int i = 0; i < diagnostics.Count; i++)
            {
                builder.Append("\n\n")
                    .Append(diagnostics[i].Code)
                    .Append(" @ ")
                    .Append(diagnostics[i].Path)
                    .Append("\n")
                    .Append(diagnostics[i].Message);
            }
            return builder.ToString();
        }

        private static string FormatLogicFrame(int frame)
        {
            return frame.ToString(CultureInfo.InvariantCulture)
                + "f / "
                + (frame / 60f).ToString("0.00", CultureInfo.InvariantCulture)
                + "s";
        }

        private static string NonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static CharacterTestRuntimeAiLabLogicAgentStateSnapshot FindLogicAgentState(
            CharacterTestRuntimeAiPlannerStateSnapshot state,
            int entityId,
            int profileSlot)
        {
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] slots = state?.BehaviorLogicSlots;
            for (int slotIndex = 0; slots != null && slotIndex < slots.Length; slotIndex++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot slot = slots[slotIndex];
                if (slot == null || slot.Slot != profileSlot)
                    continue;
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents = slot.Agents;
                for (int agentIndex = 0; agents != null && agentIndex < agents.Length; agentIndex++)
                {
                    if (agents[agentIndex] != null && agents[agentIndex].EntityId == entityId)
                        return agents[agentIndex];
                }
            }
            return null;
        }

    }
}
