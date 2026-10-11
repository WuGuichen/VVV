using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabStageFieldKind
    {
        Title = 0,
        Kind = 1,
        PlannedStartFrame = 2,
        DurationMode = 3,
        PlannedDurationFrames = 4,
        AbilityId = 5,
        CharacterActionId = 6,
        LegacyActionKind = 7,
        MoveDistanceBand = 8,
        WaitFrames = 9,
        Note = 10,
        MovementStrategy = 11,
        MovementTargetKind = 12,
        MoveTargetPosition = 13,
        MoveTargetEntityId = 14
    }

    internal readonly struct CharacterTestRuntimeAiLabStageFieldValue
    {
        internal CharacterTestRuntimeAiLabStageFieldValue(
            int intValue = 0,
            float floatValue = 0f,
            float secondFloatValue = 0f,
            string stringValue = null,
            CharacterTestRuntimeAiLabCardStepKind stageKind = default,
            CharacterTestRuntimeAiLabLogicDurationMode durationMode = default,
            CharacterTestRuntimeAiPlannerActionKind legacyActionKind = default,
            CharacterTestRuntimeAiLabMovementStrategy movementStrategy = default,
            CharacterTestRuntimeAiLabMovementTargetKind movementTargetKind = default)
        {
            IntValue = intValue;
            FloatValue = floatValue;
            SecondFloatValue = secondFloatValue;
            StringValue = stringValue ?? string.Empty;
            StageKind = stageKind;
            DurationMode = durationMode;
            LegacyActionKind = legacyActionKind;
            MovementStrategy = movementStrategy;
            MovementTargetKind = movementTargetKind;
        }

        internal int IntValue { get; }
        internal float FloatValue { get; }
        internal float SecondFloatValue { get; }
        internal string StringValue { get; }
        internal CharacterTestRuntimeAiLabCardStepKind StageKind { get; }
        internal CharacterTestRuntimeAiLabLogicDurationMode DurationMode { get; }
        internal CharacterTestRuntimeAiPlannerActionKind LegacyActionKind { get; }
        internal CharacterTestRuntimeAiLabMovementStrategy MovementStrategy { get; }
        internal CharacterTestRuntimeAiLabMovementTargetKind MovementTargetKind { get; }
    }

    internal static partial class CharacterTestRuntimeAiLabBehaviorCardStageMutation
    {
        internal const int MaximumFrame = 36000;

        internal static bool TryMoveStages(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string[] selectedStageIds,
            int selectedCount,
            int requestedDelta,
            out int appliedDelta,
            out string diagnostic)
        {
            appliedDelta = 0;
            if (!TryValidateSelection(payload, selectedStageIds, selectedCount, out diagnostic))
                return false;
            int minimum = int.MinValue;
            int maximum = int.MaxValue;
            for (int i = 0; i < selectedCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = FindStage(payload, selectedStageIds[i]);
                minimum = Math.Max(minimum, -stage.PlannedStartFrame);
                long end = (long)stage.PlannedStartFrame + Math.Max(0, stage.PlannedDurationFrames);
                maximum = Math.Min(maximum, ClampToInt((long)MaximumFrame - end));
            }
            appliedDelta = Math.Max(minimum, Math.Min(maximum, requestedDelta));
            if (appliedDelta == 0)
            {
                diagnostic = "Stage 起始时间没有变化。";
                return false;
            }
            for (int i = 0; i < selectedCount; i++)
                FindStage(payload, selectedStageIds[i]).PlannedStartFrame += appliedDelta;
            diagnostic = "已移动 " + selectedCount + " 个 Stage。";
            return true;
        }

        internal static bool TryResizeStage(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId,
            int requestedDelta,
            out int appliedDelta,
            out string diagnostic)
        {
            appliedDelta = 0;
            CharacterTestRuntimeAiLabLogicStage stage = FindStage(payload, stageId);
            if (stage == null)
            {
                diagnostic = "Stage stable ID 已失效。";
                return false;
            }
            if (stage.DurationMode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed)
            {
                diagnostic = "只有 Fixed Stage 可以拖动持续时间。";
                return false;
            }
            int maximumDuration = MaximumFrame - stage.PlannedStartFrame;
            if (maximumDuration < 1)
            {
                diagnostic = "Stage 已位于时间轴末端，无法设置 Fixed duration。";
                return false;
            }
            int duration = Math.Max(1, Math.Min(maximumDuration,
                stage.PlannedDurationFrames + requestedDelta));
            appliedDelta = duration - stage.PlannedDurationFrames;
            if (appliedDelta == 0)
            {
                diagnostic = "Stage 持续时间没有变化。";
                return false;
            }
            stage.PlannedDurationFrames = duration;
            if (!CharacterTestRuntimeAiLabBehaviorRecipeTimelineCanonicalizer.TryCanonicalize(
                    payload,
                    out diagnostic))
                return false;
            diagnostic = "Stage duration 已更新为 " + duration + "f。";
            return true;
        }

        internal static bool TryApplyField(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            in CharacterTestRuntimeAiLabStageFieldValue value,
            out string diagnostic)
        {
            return TryApplyField(
                payload,
                stageId,
                field,
                value,
                out _,
                out diagnostic);
        }

        internal static bool TryApplyField(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            in CharacterTestRuntimeAiLabStageFieldValue value,
            out bool changed,
            out string diagnostic)
        {
            changed = false;
            CharacterTestRuntimeAiLabLogicStage stage = FindStage(payload, stageId);
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline v6Timeline =
                    payload.Timelines.Find(t => string.Equals(t.TimelineId, stageId, StringComparison.Ordinal));
                if (v6Timeline != null)
                {
                    if (field == CharacterTestRuntimeAiLabStageFieldKind.Title)
                    {
                        string title = value.StringValue.Trim();
                        if (title.Length == 0)
                        {
                            diagnostic = "时间轴名称不能为空。";
                            return false;
                        }
                        if (string.Equals(v6Timeline.Title, title, StringComparison.Ordinal))
                            return NoChange(out changed, out diagnostic);
                        v6Timeline.Title = title;
                        if (stage != null) stage.Title = title;
                        CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
                        changed = true;
                        diagnostic = "时间轴名称已更新。";
                        return true;
                    }
                    if (field == CharacterTestRuntimeAiLabStageFieldKind.Note)
                    {
                        if (string.Equals(v6Timeline.Note, value.StringValue, StringComparison.Ordinal))
                            return NoChange(out changed, out diagnostic);
                        v6Timeline.Note = value.StringValue;
                        if (stage != null) stage.Note = value.StringValue;
                        CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
                        changed = true;
                        diagnostic = "时间轴备注已更新。";
                        return true;
                    }
                }
            }

            if (stage == null)
            {
                diagnostic = "Stage stable ID 已失效。";
                return false;
            }
            switch (field)
            {
                case CharacterTestRuntimeAiLabStageFieldKind.Title:
                    string title = value.StringValue.Trim();
                    if (title.Length == 0)
                    {
                        diagnostic = "Stage 名称不能为空。";
                        return false;
                    }
                    if (string.Equals(stage.Title, title, StringComparison.Ordinal))
                        return NoChange(out changed, out diagnostic);
                    stage.Title = title;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.Kind:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardStepKind), value.StageKind))
                    {
                        diagnostic = "Stage Kind 无效。";
                        return false;
                    }
                    if (stage.Kind == value.StageKind)
                        return NoChange(out changed, out diagnostic);
                    NormalizeStageKind(stage, value.StageKind);
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.PlannedStartFrame:
                    if (value.IntValue < 0 || value.IntValue > MaximumFrame
                        || (long)value.IntValue + Math.Max(0, stage.PlannedDurationFrames) > MaximumFrame)
                    {
                        diagnostic = "Stage 起始帧超出 0..36000 或导致结束帧越界。";
                        return false;
                    }
                    if (stage.PlannedStartFrame == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    stage.PlannedStartFrame = value.IntValue;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.DurationMode:
                    if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDurationMode), value.DurationMode))
                    {
                        diagnostic = "DurationMode 无效。";
                        return false;
                    }
                    if (stage.DurationMode == value.DurationMode)
                        return NoChange(out changed, out diagnostic);
                    stage.DurationMode = value.DurationMode;
                    if (value.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                        && stage.PlannedDurationFrames <= 0)
                    {
                        stage.PlannedDurationFrames = Math.Max(1,
                            Math.Min(MaximumFrame - stage.PlannedStartFrame,
                                stage.PreviewDurationFrames > 0 ? stage.PreviewDurationFrames : 60));
                    }
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.PlannedDurationFrames:
                    if (stage.DurationMode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                        || value.IntValue < 1
                        || (long)stage.PlannedStartFrame + value.IntValue > MaximumFrame)
                    {
                        diagnostic = "只有 Fixed Stage 可设置 1..36000 内的持续帧。";
                        return false;
                    }
                    if (stage.PlannedDurationFrames == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    stage.PlannedDurationFrames = value.IntValue;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.AbilityId:
                    if (!UsesAbility(stage) || value.IntValue < 0)
                    {
                        diagnostic = "当前 Stage 不接受 AbilityId。";
                        return false;
                    }
                    if (stage.AbilityId == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    stage.AbilityId = value.IntValue;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.CharacterActionId:
                    if (stage.Kind != CharacterTestRuntimeAiLabCardStepKind.CharacterAction)
                    {
                        diagnostic = "当前 Stage 不接受 CharacterActionId。";
                        return false;
                    }
                    if (!CharacterActionId.TryParse(value.StringValue, out CharacterActionId actionId))
                    {
                        diagnostic = "CharacterActionId 不能为空。";
                        return false;
                    }
                    if (stage.CharacterActionId == actionId)
                        return NoChange(out changed, out diagnostic);
                    stage.CharacterActionId = actionId;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.LegacyActionKind:
                    if (stage.Kind != CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly
                        || !Enum.IsDefined(typeof(CharacterTestRuntimeAiPlannerActionKind), value.LegacyActionKind))
                    {
                        diagnostic = "LegacyActionKind 仅适用于旧草稿迁移。";
                        return false;
                    }
                    if (stage.LegacyActionKind == value.LegacyActionKind)
                        return NoChange(out changed, out diagnostic);
                    NormalizeLegacyActionKind(stage, value.LegacyActionKind);
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.MovementStrategy:
                    if (stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementStrategy), value.MovementStrategy))
                    {
                        diagnostic = "移动策略只适用于移动 Stage。";
                        return false;
                    }
                    if (stage.MovementStrategy == value.MovementStrategy)
                        return NoChange(out changed, out diagnostic);
                    stage.MovementStrategy = value.MovementStrategy;
                    stage.LegacyActionKind = default;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand:
                    if (!UsesMoveBand(stage)
                        || value.FloatValue < 0f
                        || value.SecondFloatValue < value.FloatValue)
                    {
                        diagnostic = "移动距离必须满足 0 <= min <= max。";
                        return false;
                    }
                    if (stage.MoveMinDistance.Equals(value.FloatValue)
                        && stage.MoveMaxDistance.Equals(value.SecondFloatValue))
                        return NoChange(out changed, out diagnostic);
                    stage.MoveMinDistance = value.FloatValue;
                    stage.MoveMaxDistance = value.SecondFloatValue;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.MovementTargetKind:
                    if (stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || !Enum.IsDefined(
                            typeof(CharacterTestRuntimeAiLabMovementTargetKind),
                            value.MovementTargetKind))
                    {
                        diagnostic = "Movement target kind is only valid for movement stages.";
                        return false;
                    }
                    if (stage.MovementTargetKind == value.MovementTargetKind)
                        return NoChange(out changed, out diagnostic);
                    stage.MovementTargetKind = value.MovementTargetKind;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.MoveTargetPosition:
                    if (stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || stage.MovementTargetKind != CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                        || !IsFinite(value.FloatValue)
                        || !IsFinite(value.SecondFloatValue))
                    {
                        diagnostic = "World-position target requires finite X and Z coordinates.";
                        return false;
                    }
                    if (stage.MoveTargetPositionX.Equals(value.FloatValue)
                        && stage.MoveTargetPositionZ.Equals(value.SecondFloatValue))
                        return NoChange(out changed, out diagnostic);
                    stage.MoveTargetPositionX = value.FloatValue;
                    stage.MoveTargetPositionZ = value.SecondFloatValue;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.MoveTargetEntityId:
                    if (stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || stage.MovementTargetKind != CharacterTestRuntimeAiLabMovementTargetKind.Entity
                        || value.IntValue < 0)
                    {
                        diagnostic = "Entity target id must be zero (automatic ally) or positive.";
                        return false;
                    }
                    if (stage.MoveTargetEntityId == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    stage.MoveTargetEntityId = value.IntValue;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.WaitFrames:
                    if (stage.Kind != CharacterTestRuntimeAiLabCardStepKind.Wait || value.IntValue < 1)
                    {
                        diagnostic = "Wait Stage 至少需要 1f。";
                        return false;
                    }
                    if (stage.WaitFrames == value.IntValue)
                        return NoChange(out changed, out diagnostic);
                    stage.WaitFrames = value.IntValue;
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.Note:
                    if (string.Equals(stage.Note, value.StringValue, StringComparison.Ordinal))
                        return NoChange(out changed, out diagnostic);
                    stage.Note = value.StringValue;
                    break;
                default:
                    diagnostic = "不支持的 Stage 字段。";
                    return false;
            }
            if (field == CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand)
                SynchronizeAtomicMovementActivationThreshold(payload, stage);
            if (field == CharacterTestRuntimeAiLabStageFieldKind.Kind
                || field == CharacterTestRuntimeAiLabStageFieldKind.DurationMode
                || field == CharacterTestRuntimeAiLabStageFieldKind.PlannedDurationFrames
                || field == CharacterTestRuntimeAiLabStageFieldKind.WaitFrames)
            {
                if (!CharacterTestRuntimeAiLabBehaviorRecipeTimelineCanonicalizer.TryCanonicalize(
                        payload,
                        out diagnostic))
                    return false;
            }
            changed = true;
            diagnostic = "Stage 字段已更新。";
            return true;
        }

        private static void SynchronizeAtomicMovementActivationThreshold(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicStage stage)
        {
            if (payload == null
                || stage == null
                || payload.Stages.Count != 1
                || !string.Equals(payload.EntryStageId, stage.StageId, StringComparison.Ordinal)
                || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                || stage.MovementTargetKind
                    != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget)
            {
                return;
            }

            CharacterTestRuntimeAiLabCardConditionOperator thresholdOperator;
            switch (stage.MovementStrategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                    thresholdOperator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan;
                    break;
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    thresholdOperator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual;
                    break;
                default:
                    return;
            }

            for (int i = 0; i < payload.ActivationConditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = payload.ActivationConditions[i];
                if (condition?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    && condition.Operator == thresholdOperator)
                {
                    condition.Value = stage.MoveMinDistance;
                }
            }
        }

        internal static bool TryDuplicate(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string[] selectedStageIds,
            int selectedCount,
            out string[] createdStageIds,
            out string diagnostic)
        {
            createdStageIds = Array.Empty<string>();
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                return TryDuplicateV6(payload, selectedStageIds, selectedCount, out createdStageIds, out diagnostic);
            }
            if (!TryValidateSelection(payload, selectedStageIds, selectedCount, out diagnostic))
                return false;
            if (!TryValidateUniqueGraphIds(payload, out diagnostic))
                return false;

            var stageMap = new Dictionary<string, string>(selectedCount, StringComparer.Ordinal);
            var detectorMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var transitionMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var sources = new List<CharacterTestRuntimeAiLabLogicStage>(selectedCount);
            int insertionIndex = -1;
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (!Contains(selectedStageIds, selectedCount, stage.StageId))
                    continue;
                sources.Add(stage);
                insertionIndex = i + 1;
                stageMap.Add(stage.StageId, AllocateStageId(payload, stageMap));
            }
            if (sources.Count != selectedCount || insertionIndex < 0)
            {
                diagnostic = "Stage selection 包含重复或失效 ID。";
                return false;
            }

            int commonDelta = 15;
            for (int i = 0; i < sources.Count; i++)
            {
                long end = (long)sources[i].PlannedStartFrame
                    + Math.Max(0, sources[i].PlannedDurationFrames);
                commonDelta = Math.Min(commonDelta, Math.Max(0, ClampToInt(MaximumFrame - end)));
            }
            createdStageIds = new string[sources.Count];
            int maxColumn = MaximumLayoutColumn(payload);
            for (int i = 0; i < sources.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage clone = sources[i].Clone();
                clone.StageId = stageMap[sources[i].StageId];
                clone.PlannedStartFrame += commonDelta;
                payload.Stages.Insert(insertionIndex + i, clone);
                createdStageIds[i] = clone.StageId;
                CharacterTestRuntimeAiLabLogicStageLayout sourceLayout = FindLayout(payload, sources[i].StageId);
                payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
                {
                    StageId = clone.StageId,
                    Column = maxColumn + i + 1,
                    Width = sourceLayout?.Width ?? 1f,
                    Offset = sourceLayout?.Offset ?? 0f
                });
            }
            NormalizeStageOrder(payload);

            int detectorSourceCount = payload.Detectors.Count;
            for (int i = 0; i < detectorSourceCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector.Scope != CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    || !stageMap.ContainsKey(detector.StageId))
                    continue;
                string newId = AllocateDetectorId(payload, detectorMap);
                detectorMap.Add(detector.DetectorId, newId);
                CharacterTestRuntimeAiLabLogicDetector clone = detector.Clone();
                clone.DetectorId = newId;
                clone.StageId = stageMap[detector.StageId];
                payload.Detectors.Add(clone);
            }

            int transitionSourceCount = payload.Transitions.Count;
            for (int i = 0; i < transitionSourceCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (!detectorMap.TryGetValue(transition.DetectorId, out string newDetectorId))
                    continue;
                if (!stageMap.TryGetValue(transition.SourceStageId, out string newSourceStageId))
                {
                    diagnostic = "Transition source Stage 无法映射。";
                    return false;
                }
                string newId = AllocateTransitionId(payload, transitionMap);
                transitionMap.Add(transition.TransitionId, newId);
                CharacterTestRuntimeAiLabLogicTransition clone = transition.Clone();
                clone.TransitionId = newId;
                clone.DetectorId = newDetectorId;
                clone.SourceStageId = newSourceStageId;
                if (stageMap.TryGetValue(transition.TargetStageId, out string newTargetStageId))
                    clone.TargetStageId = newTargetStageId;
                payload.Transitions.Add(clone);
            }
            if (!CharacterTestRuntimeAiLabBehaviorRecipeTimelineCanonicalizer.TryCanonicalize(
                    payload,
                    out diagnostic))
                return false;
            diagnostic = "已复制 " + createdStageIds.Length + " 个 Stage 及其内部子图。";
            return true;
        }

        private static bool TryDuplicateV6(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string[] selectedStageIds,
            int selectedCount,
            out string[] createdStageIds,
            out string diagnostic)
        {
            createdStageIds = Array.Empty<string>();
            if (payload == null || selectedStageIds == null || selectedCount <= 0 || selectedCount > selectedStageIds.Length)
            {
                diagnostic = "时间轴选择不可用。";
                return false;
            }

            var sources = new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>(selectedCount);
            int insertionIndex = -1;
            for (int i = 0; i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline == null || !Contains(selectedStageIds, selectedCount, timeline.TimelineId))
                    continue;
                sources.Add(timeline);
                insertionIndex = i + 1;
            }
            if (sources.Count != selectedCount || insertionIndex < 0)
            {
                diagnostic = "时间轴选择包含无效或重复 ID。";
                return false;
            }

            createdStageIds = new string[sources.Count];
            int suffix = 1;
            for (int i = 0; i < sources.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline source = sources[i];
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline clone = source.Clone();
                string newId;
                do newId = "timeline.copy." + suffix++;
                while (payload.Timelines.Exists(t => string.Equals(t.TimelineId, newId, StringComparison.Ordinal)));

                clone.TimelineId = newId;
                clone.Title = string.IsNullOrWhiteSpace(source.Title) ? "时间轴 副本" : source.Title.Trim() + " (副本)";

                int eventSuffix = 1;
                for (int evIdx = 0; evIdx < clone.Events.Count; evIdx++)
                {
                    if (clone.Events[evIdx] != null)
                    {
                        clone.Events[evIdx].TimelineId = newId;
                        clone.Events[evIdx].EventId = "event.copy." + (eventSuffix++);
                    }
                }

                payload.Timelines.Insert(insertionIndex + i, clone);
                createdStageIds[i] = newId;
            }
            for (int i = 0; i < payload.Timelines.Count; i++)
                payload.Timelines[i].Order = i;

            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
            diagnostic = "已复制 " + createdStageIds.Length + " 个时间轴。";
            return true;
        }

        internal static bool TryDelete(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string[] selectedStageIds,
            int selectedCount,
            out string focusStageId,
            out string diagnostic)
        {
            focusStageId = string.Empty;
            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                return TryDeleteV6(payload, selectedStageIds, selectedCount, out focusStageId, out diagnostic);
            }
            if (!TryValidateSelection(payload, selectedStageIds, selectedCount, out diagnostic))
                return false;
            if (selectedCount >= payload.Stages.Count)
            {
                diagnostic = "行为卡至少必须保留一个 Stage。";
                return false;
            }
            if (Contains(selectedStageIds, selectedCount, payload.EntryStageId))
            {
                diagnostic = "入口 Stage 不能直接删除。";
                return false;
            }

            int firstRemovedIndex = payload.Stages.Count;
            for (int i = payload.Stages.Count - 1; i >= 0; i--)
            {
                if (!Contains(selectedStageIds, selectedCount, payload.Stages[i].StageId))
                    continue;
                firstRemovedIndex = Math.Min(firstRemovedIndex, i);
                payload.Stages.RemoveAt(i);
            }
            for (int i = payload.Detectors.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    && Contains(selectedStageIds, selectedCount, detector.StageId))
                {
                    string detectorId = detector.DetectorId;
                    payload.Detectors.RemoveAt(i);
                    RemoveTransitionsForDetector(payload, detectorId);
                }
            }
            for (int i = payload.Transitions.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (Contains(selectedStageIds, selectedCount, transition.SourceStageId))
                {
                    payload.Transitions.RemoveAt(i);
                    continue;
                }
                if (Contains(selectedStageIds, selectedCount, transition.TargetStageId))
                    transition.TargetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget;
            }
            for (int i = payload.Layout.Stages.Count - 1; i >= 0; i--)
            {
                if (Contains(selectedStageIds, selectedCount, payload.Layout.Stages[i].StageId))
                    payload.Layout.Stages.RemoveAt(i);
            }
            NormalizeStageOrder(payload);
            NormalizeLayoutColumns(payload);
            if (!CharacterTestRuntimeAiLabBehaviorRecipeTimelineCanonicalizer.TryCanonicalize(
                    payload,
                    out diagnostic))
                return false;
            int focusIndex = Math.Max(0, Math.Min(firstRemovedIndex, payload.Stages.Count - 1));
            focusStageId = payload.Stages[focusIndex].StageId;
            diagnostic = "已删除 " + selectedCount + " 个 Stage。";
            return true;
        }

        private static bool TryDeleteV6(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string[] selectedStageIds,
            int selectedCount,
            out string focusStageId,
            out string diagnostic)
        {
            focusStageId = string.Empty;
            if (payload == null || selectedStageIds == null || selectedCount <= 0 || selectedCount > selectedStageIds.Length)
            {
                diagnostic = "时间轴选择不可用。";
                return false;
            }
            if (selectedCount >= payload.Timelines.Count)
            {
                diagnostic = "行为卡至少必须保留一条时间轴。";
                return false;
            }
            if (Contains(selectedStageIds, selectedCount, payload.EntryTimelineId))
            {
                diagnostic = "入口时间轴不能直接删除。";
                return false;
            }

            int firstRemovedIndex = payload.Timelines.Count;
            for (int i = payload.Timelines.Count - 1; i >= 0; i--)
            {
                if (!Contains(selectedStageIds, selectedCount, payload.Timelines[i].TimelineId))
                    continue;
                firstRemovedIndex = Math.Min(firstRemovedIndex, i);
                payload.Timelines.RemoveAt(i);
            }
            for (int i = 0; i < payload.Timelines.Count; i++)
                payload.Timelines[i].Order = i;

            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
            int focusIndex = Math.Max(0, Math.Min(firstRemovedIndex, payload.Timelines.Count - 1));
            focusStageId = payload.Timelines[focusIndex]?.TimelineId ?? string.Empty;
            diagnostic = "已删除 " + selectedCount + " 条时间轴。";
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

    }
}
