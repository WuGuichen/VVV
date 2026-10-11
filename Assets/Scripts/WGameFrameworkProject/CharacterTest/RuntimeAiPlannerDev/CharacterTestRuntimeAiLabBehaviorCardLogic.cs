using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabLogicDetectorScope
    {
        Card = 0,
        Stage = 1
    }

    internal enum CharacterTestRuntimeAiLabLogicDetectorTrigger
    {
        ConditionEdge = 0,
        ActionCompleted = 1,
        ActionFailed = 2,
        LocalFrame = 3,
        ActionRejected = 4
    }

    internal enum CharacterTestRuntimeAiLabLogicTransitionPolicy
    {
        Interrupt = 0,
        AfterActionEnd = 1,
        PreserveAction = 2
    }

    internal enum CharacterTestRuntimeAiLabLogicDurationMode
    {
        Fixed = 0,
        UntilEvent = 1,
        Infinite = 2
    }

    internal enum CharacterTestRuntimeAiLabLogicDetectorWindowEndMode
    {
        Fixed = 0,
        StageEnd = 1,
        UntilEvent = 2,
        Infinite = 3
    }

    internal enum CharacterTestRuntimeAiLabLogicActionState
    {
        None = 0,
        Running = 1,
        Completed = 2,
        Failed = 3
    }

    internal enum CharacterTestRuntimeAiLabLogicRuntimeState
    {
        Inactive = 0,
        Running = 1,
        Transitioning = 2,
        Completed = 3,
        Failed = 4
    }

    internal enum CharacterTestRuntimeAiLabMovementStrategy
    {
        ApproachTarget = 0,
        MaintainDistance = 1,
        AvoidHazard = 2,
        StrafeWhenBlocked = 3,
        RetreatWhenClose = 4,
        OrbitTarget = 5
    }

    internal enum CharacterTestRuntimeAiLabMovementTargetKind
    {
        CurrentCombatTarget = 0,
        WorldPosition = 1,
        Entity = 2
    }

    internal enum CharacterTestRuntimeAiLabLogicEventKind
    {
        StageEntered = 0,
        DetectorTriggered = 1,
        TransitionPending = 2,
        TransitionCommitted = 3,
        ActionInterruptRequested = 4,
        CardEnded = 5,
        ActionStartRejected = 6
    }

    internal static class CharacterTestRuntimeAiLabDetectorActivity
    {
        /// <summary>NoAlloc: shared evaluator and latest-frame presentation predicate.</summary>
        internal static bool IsActive(
            int frame,
            CharacterTestRuntimeAiLabCompiledLogicDetector detector,
            int cardStartedFrame,
            int stageEnteredFrame,
            int currentStageIndex)
        {
            if (detector == null
                || (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    && detector.StageIndex != currentStageIndex))
            {
                return false;
            }
            int origin = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                ? stageEnteredFrame
                : cardStartedFrame;
            if (origin < 0)
                return false;
            int elapsed = frame - origin;
            if (elapsed < detector.ActivationStartFrame)
                return false;
            if (detector.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
                return true;
            long endExclusive = (long)detector.ActivationStartFrame
                + detector.ActivationDurationFrames;
            return elapsed < endExclusive;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardLogicPayload
    {
        public const string Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v11";
        public const string LegacyV10Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v10";
        public const string LegacyV9Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v9";
        public const string LegacyV8Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v8";
        public const string LegacyV7Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v7";
        public const string LegacyV6Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v6";
        public const string LegacyV5Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v5";
        public const string LegacyV4Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v4";
        public const string LegacyV3Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v3";
        public const string LegacyV2Schema = "mx.character-test.runtime-ai-lab.behavior-card-logic.v2";
        public const int CurrentBehaviorRecipeVersion = 11;

        public string EntryTimelineId { get; set; } = string.Empty;
        public int BehaviorRecipeVersion { get; set; }
        public string EntryStageId { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public string RiskSummary { get; set; } = string.Empty;
        public List<CharacterTestRuntimeAiLabCardCondition> ActivationConditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ActivationConditionRoot { get; set; }
        public List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline> Timelines { get; } =
            new List<CharacterTestRuntimeAiLabBehaviorRecipeTimeline>();
        public List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent> LegacySharedTriggerEvents { get; } =
            new List<CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent>();
        public List<CharacterTestRuntimeAiLabBehaviorRecipeEvent> LegacySharedEvents { get; } =
            new List<CharacterTestRuntimeAiLabBehaviorRecipeEvent>();

        // These collections remain available to the existing authoring surface as a compatibility
        // projection. ShouldSerialize* keeps them out of native v8 JSON while still allowing v4/v5 input.
        public List<CharacterTestRuntimeAiLabLogicStage> Stages { get; } =
            new List<CharacterTestRuntimeAiLabLogicStage>();
        public List<CharacterTestRuntimeAiLabLogicDetector> Detectors { get; } =
            new List<CharacterTestRuntimeAiLabLogicDetector>();
        public List<CharacterTestRuntimeAiLabLogicTransition> Transitions { get; } =
            new List<CharacterTestRuntimeAiLabLogicTransition>();
        public CharacterTestRuntimeAiLabLogicLayout Layout { get; set; } =
            new CharacterTestRuntimeAiLabLogicLayout();

        internal bool LegacyProjectionFingerprintInitialized { get; private set; }
        internal long LegacyProjectionFingerprint { get; private set; }
        internal List<CharacterTestRuntimeAiLabCardDiagnostic> MigrationDiagnostics { get; } =
            new List<CharacterTestRuntimeAiLabCardDiagnostic>();

        public bool ShouldSerializeEntryStageId() => false;
        public bool ShouldSerializeActivationConditions() => false;
        public bool ShouldSerializeLegacySharedTriggerEvents() => false;
        public bool ShouldSerializeStages() => false;
        public bool ShouldSerializeDetectors() => false;
        public bool ShouldSerializeTransitions() => false;
        public bool ShouldSerializeLayout() => false;

        internal void CaptureLegacyProjectionFingerprint(long fingerprint)
        {
            LegacyProjectionFingerprint = fingerprint;
            LegacyProjectionFingerprintInitialized = true;
        }

        public CharacterTestRuntimeAiLabBehaviorCardLogicPayload Clone()
        {
            var copy = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryTimelineId = EntryTimelineId,
                BehaviorRecipeVersion = BehaviorRecipeVersion,
                EntryStageId = EntryStageId,
                Intent = Intent,
                RiskSummary = RiskSummary,
                ActivationConditionRoot = ActivationConditionRoot?.Clone(),
                Layout = Layout?.Clone() ?? new CharacterTestRuntimeAiLabLogicLayout()
            };
            for (int i = 0; i < ActivationConditions.Count; i++)
                copy.ActivationConditions.Add(ActivationConditions[i].Clone());
            for (int i = 0; i < Timelines.Count; i++)
                copy.Timelines.Add(Timelines[i]?.Clone());
            for (int i = 0; i < LegacySharedTriggerEvents.Count; i++)
                copy.LegacySharedTriggerEvents.Add(LegacySharedTriggerEvents[i]?.Clone());
            for (int i = 0; i < LegacySharedEvents.Count; i++)
                copy.LegacySharedEvents.Add(LegacySharedEvents[i]?.Clone());
            for (int i = 0; i < Stages.Count; i++)
                copy.Stages.Add(Stages[i].Clone());
            for (int i = 0; i < Detectors.Count; i++)
                copy.Detectors.Add(Detectors[i].Clone());
            for (int i = 0; i < Transitions.Count; i++)
                copy.Transitions.Add(Transitions[i].Clone());
            for (int i = 0; i < MigrationDiagnostics.Count; i++)
                copy.MigrationDiagnostics.Add(MigrationDiagnostics[i]);
            if (LegacyProjectionFingerprintInitialized)
                copy.CaptureLegacyProjectionFingerprint(LegacyProjectionFingerprint);
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicStage
    {
        public string StageId { get; set; } = string.Empty;
        public int Order { get; set; }
        public CharacterTestRuntimeAiLabCardStepKind Kind { get; set; }
        public string Title { get; set; } = string.Empty;
        public int AbilityId { get; set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; set; }
        public CharacterTestRuntimeAiPlannerActionKind LegacyActionKind { get; set; }
        public CharacterTestRuntimeAiLabMovementStrategy MovementStrategy { get; set; }
        public CharacterTestRuntimeAiLabMovementTargetKind MovementTargetKind { get; set; }
        public float MoveTargetPositionX { get; set; }
        public float MoveTargetPositionZ { get; set; }
        public int MoveTargetEntityId { get; set; }
        public float MoveMinDistance { get; set; }
        public float MoveMaxDistance { get; set; }
        public float MoveSpeedMultiplier { get; set; } =
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier;
        public int WaitFrames { get; set; }
        public int PreviewDurationFrames { get; set; }
        public int PlannedStartFrame { get; set; }
        public int PlannedDurationFrames { get; set; }
        public CharacterTestRuntimeAiLabLogicDurationMode DurationMode { get; set; } =
            CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent;
        public string Note { get; set; } = string.Empty;
        public string EntryEventId { get; set; } = string.Empty;
        public string PrimaryActionEventId { get; set; } = string.Empty;
        public bool IsBehaviorRecipeV5Timeline { get; set; }
        public List<CharacterTestRuntimeAiLabCardCondition> EntryConditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();

        public bool OwnsPrimaryAction => Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
            || Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
            || Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;

        public CharacterTestRuntimeAiLabLogicStage Clone()
        {
            var copy = new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = StageId,
                Order = Order,
                Kind = Kind,
                Title = Title,
                AbilityId = AbilityId,
                CharacterActionId = CharacterActionId,
                LegacyActionKind = LegacyActionKind,
                MovementStrategy = MovementStrategy,
                MovementTargetKind = MovementTargetKind,
                MoveTargetPositionX = MoveTargetPositionX,
                MoveTargetPositionZ = MoveTargetPositionZ,
                MoveTargetEntityId = MoveTargetEntityId,
                MoveMinDistance = MoveMinDistance,
                MoveMaxDistance = MoveMaxDistance,
                MoveSpeedMultiplier = MoveSpeedMultiplier,
                WaitFrames = WaitFrames,
                PreviewDurationFrames = PreviewDurationFrames,
                PlannedStartFrame = PlannedStartFrame,
                PlannedDurationFrames = PlannedDurationFrames,
                DurationMode = DurationMode,
                Note = Note,
                EntryEventId = EntryEventId,
                PrimaryActionEventId = PrimaryActionEventId,
                IsBehaviorRecipeV5Timeline = IsBehaviorRecipeV5Timeline
            };
            for (int i = 0; i < EntryConditions.Count; i++)
                copy.EntryConditions.Add(EntryConditions[i].Clone());
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicDetector
    {
        public string DetectorId { get; set; } = string.Empty;
        public string LegacySharedTriggerId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabLogicDetectorScope Scope { get; set; }
        public string StageId { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabLogicDetectorTrigger Trigger { get; set; }
        public int Priority { get; set; }
        public int ActivationStartFrame { get; set; }
        public int ActivationDurationFrames { get; set; }
        public CharacterTestRuntimeAiLabLogicDetectorWindowEndMode WindowEndMode { get; set; } =
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
        public bool ExpectedResult { get; set; } = true;
        public CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor LocalFrameAnchor { get; set; }
        public int LocalFrame { get; set; }
        public int ActionSignalReasonCode { get; set; } = 0;
        public bool LegacyUnfilteredSignal { get; set; }
        public bool IsBehaviorRecipeV5Event { get; set; }
        public List<CharacterTestRuntimeAiLabCardCondition> Conditions { get; } =
            new List<CharacterTestRuntimeAiLabCardCondition>();

        public CharacterTestRuntimeAiLabLogicDetector Clone()
        {
            var copy = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = DetectorId,
                LegacySharedTriggerId = LegacySharedTriggerId,
                Title = Title,
                Scope = Scope,
                StageId = StageId,
                Trigger = Trigger,
                Priority = Priority,
                ActivationStartFrame = ActivationStartFrame,
                ActivationDurationFrames = ActivationDurationFrames,
                WindowEndMode = WindowEndMode,
                ExpectedResult = ExpectedResult,
                LocalFrameAnchor = LocalFrameAnchor,
                LocalFrame = LocalFrame,
                ActionSignalReasonCode = ActionSignalReasonCode,
                LegacyUnfilteredSignal = LegacyUnfilteredSignal,
                IsBehaviorRecipeV5Event = IsBehaviorRecipeV5Event
            };
            for (int i = 0; i < Conditions.Count; i++)
                copy.Conditions.Add(Conditions[i].Clone());
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicTransition
    {
        public const string EndTarget = "End";

        public string TransitionId { get; set; } = string.Empty;
        public string DetectorId { get; set; } = string.Empty;
        public string SourceStageId { get; set; } = string.Empty;
        public string TargetStageId { get; set; } = EndTarget;
        public int Priority { get; set; }
        public CharacterTestRuntimeAiLabLogicTransitionPolicy Policy { get; set; }
        public string Label { get; set; } = string.Empty;
        public string EndReason { get; set; } = string.Empty;
        public bool IsBehaviorRecipeV5Event { get; set; }

        public CharacterTestRuntimeAiLabLogicTransition Clone()
        {
            return (CharacterTestRuntimeAiLabLogicTransition)MemberwiseClone();
        }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicLayout
    {
        public List<CharacterTestRuntimeAiLabLogicStageLayout> Stages { get; } =
            new List<CharacterTestRuntimeAiLabLogicStageLayout>();

        public CharacterTestRuntimeAiLabLogicLayout Clone()
        {
            var copy = new CharacterTestRuntimeAiLabLogicLayout();
            for (int i = 0; i < Stages.Count; i++)
                copy.Stages.Add(Stages[i].Clone());
            return copy;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicStageLayout
    {
        public string StageId { get; set; } = string.Empty;
        public int Column { get; set; }
        public float Width { get; set; } = 1f;
        public float Offset { get; set; }

        public CharacterTestRuntimeAiLabLogicStageLayout Clone()
        {
            return (CharacterTestRuntimeAiLabLogicStageLayout)MemberwiseClone();
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardV1Migration
    {
        /// <remarks>LowFreqAlloc: explicit draft migration only.</remarks>
        public static CharacterTestRuntimeAiLabBehaviorCardLogicPayload Convert(
            CharacterTestRuntimeAiLabBehaviorCardPayload source)
        {
            var target = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload();
            if (source == null)
                return target;

            target.EntryStageId = source.EntryStepId;
            target.Intent = source.Intent;
            target.RiskSummary = source.RiskSummary;
            for (int i = 0; i < source.TriggerConditions.Count; i++)
                target.ActivationConditions.Add(source.TriggerConditions[i].Clone());

            for (int i = 0; i < source.Steps.Count; i++)
            {
                CharacterTestRuntimeAiLabCardStep step = source.Steps[i];
                int plannedDuration = ResolveMigratedDurationFrames(step);
                var stage = new CharacterTestRuntimeAiLabLogicStage
                {
                    StageId = step.StepId,
                    Order = step.Order,
                    Kind = step.Kind,
                    Title = step.Title,
                    AbilityId = step.AbilityId,
                    CharacterActionId = step.CharacterActionId,
                    LegacyActionKind = step.LegacyActionKind,
                    MoveMinDistance = step.MoveMinDistance,
                    MoveMaxDistance = step.MoveMaxDistance,
                    WaitFrames = step.WaitFrames,
                    PreviewDurationFrames = step.EstimatedDurationFrames,
                    PlannedStartFrame = ResolveMigratedStartFrame(target.Stages),
                    PlannedDurationFrames = plannedDuration,
                    DurationMode = plannedDuration > 0
                        ? CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                        : CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent,
                    Note = step.Note
                };
                for (int c = 0; c < step.EntryConditions.Count; c++)
                    stage.EntryConditions.Add(step.EntryConditions[c].Clone());
                target.Stages.Add(stage);
                target.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
                {
                    StageId = stage.StageId,
                    Column = i,
                    Width = 1f
                });

                for (int branchIndex = 0; branchIndex < step.Branches.Count; branchIndex++)
                {
                    CharacterTestRuntimeAiLabCardBranch branch = step.Branches[branchIndex];
                    string detectorId = "detector." + branch.BranchId;
                    var detector = new CharacterTestRuntimeAiLabLogicDetector
                    {
                        DetectorId = detectorId,
                        Title = string.IsNullOrWhiteSpace(branch.Label) ? branch.BranchId : branch.Label,
                        Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                        StageId = stage.StageId,
                        Trigger = ToDetectorTrigger(branch.Trigger),
                        Priority = branch.Priority,
                        ActivationStartFrame = 0,
                        // StageEnd is an open listener window. Keeping the legacy planned duration
                        // here creates a dormant fixed-window value and unnecessarily forces the
                        // one-time authoring canonicalization gate for newly generated v3 cards.
                        ActivationDurationFrames = 0,
                        WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd
                    };
                    IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions =
                        branch.Conditions.Count > 0 ? branch.Conditions : step.MonitorConditions;
                    for (int c = 0; c < conditions.Count; c++)
                        detector.Conditions.Add(conditions[c].Clone());
                    target.Detectors.Add(detector);
                    target.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
                    {
                        TransitionId = branch.BranchId,
                        DetectorId = detectorId,
                        SourceStageId = stage.StageId,
                        TargetStageId = branch.TargetStepId,
                        Priority = branch.Priority,
                        Policy = branch.Trigger == CharacterTestRuntimeAiLabCardBranchTrigger.Condition
                            ? CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt
                            : CharacterTestRuntimeAiLabLogicTransitionPolicy.AfterActionEnd,
                        Label = branch.Label,
                        EndReason = branch.EndReason
                    });
                }
            }

            if (source.Steps.Count == 1)
            {
                CharacterTestRuntimeAiLabBehaviorCardFactory.ApplyLegacyFallbackHoldMigration(
                    target,
                    source.Steps[0]?.Title);
            }

            CharacterTestRuntimeAiLabBehaviorCardLogicJson.MigrateV3MovementStrategies(target);

            return target;
        }

        private static int ResolveMigratedDurationFrames(CharacterTestRuntimeAiLabCardStep step)
        {
            if (step == null)
                return 0;
            if (step.EstimatedDurationFrames > 0)
                return step.EstimatedDurationFrames;
            if (step.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait && step.WaitFrames > 0)
                return step.WaitFrames;
            return 0;
        }

        private static int ResolveMigratedStartFrame(
            IReadOnlyList<CharacterTestRuntimeAiLabLogicStage> stages)
        {
            int start = 0;
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = stages[i];
                int duration = stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                    ? Math.Max(0, stage.PlannedDurationFrames)
                    : Math.Max(0, stage.PreviewDurationFrames);
                start = Math.Max(start, stage.PlannedStartFrame + duration);
            }
            return start;
        }

        private static CharacterTestRuntimeAiLabLogicDetectorTrigger ToDetectorTrigger(
            CharacterTestRuntimeAiLabCardBranchTrigger trigger)
        {
            switch (trigger)
            {
                case CharacterTestRuntimeAiLabCardBranchTrigger.Failure:
                    return CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed;
                case CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess:
                    return CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted;
                default:
                    return CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
            }
        }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardLogicValidator
    {
        /// <remarks>LowFreqAlloc: explicit authoring validation only.</remarks>
        public static IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Validate(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            if (payload == null)
            {
                Error(diagnostics, "CARD_V2_NULL", "$", "Behavior card logic payload is null.");
                return diagnostics;
            }
            if (CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.HasV8Authority(payload))
            {
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                    CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(payload, null);
                return compile.Diagnostics;
            }
            if (payload.Timelines.Count > 0)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForWrite(payload);
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
                return CharacterTestRuntimeAiLabBehaviorRecipeV5Validator.Validate(payload);
            }

            var stages = new Dictionary<string, CharacterTestRuntimeAiLabLogicStage>(StringComparer.Ordinal);
            var detectors = new HashSet<string>(StringComparer.Ordinal);
            var transitions = new HashSet<string>(StringComparer.Ordinal);
            var conditionIds = new HashSet<string>(StringComparer.Ordinal);
            ValidateConditions(payload.ActivationConditions, "$.activationConditions", conditionIds, diagnostics);
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                string path = "$.stages[" + i + "]";
                if (stage == null || string.IsNullOrWhiteSpace(stage.StageId))
                {
                    Error(diagnostics, "CARD_V2_STAGE_ID", path + ".stageId", "Stage id is required.");
                    continue;
                }
                if (!stages.TryAdd(stage.StageId, stage))
                    Error(diagnostics, "CARD_V2_STAGE_DUPLICATE", path + ".stageId", "Stage id must be unique.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardStepKind), stage.Kind))
                    Error(diagnostics, "CARD_V2_STAGE_KIND", path + ".kind", "Stage kind is invalid.");
                bool usesAbility = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility;
                if (usesAbility && stage.AbilityId <= 0)
                    Error(diagnostics, "CARD_V2_ABILITY_ID", path + ".abilityId", "Ability stage requires a positive ability id.");
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    && !stage.CharacterActionId.IsValid)
                    Error(diagnostics, "CARD_V3_ACTION_ID", path + ".characterActionId", "CharacterAction requires a stable CharacterAction id.");
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
                    Error(diagnostics, "CARD_V3_LEGACY_ACTION_UNMIGRATED", path + ".kind", "Legacy runtime action must be migrated before compilation.");
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    && (stage.MoveMinDistance < 0f || stage.MoveMaxDistance < stage.MoveMinDistance))
                    Error(diagnostics, "CARD_V2_MOVE_BAND", path, "Move distance band is invalid.");
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    && !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementStrategy), stage.MovementStrategy))
                {
                    Error(
                        diagnostics,
                        "CARD_V4_MOVEMENT_STRATEGY",
                        path + ".movementStrategy",
                        "Move stage requires a supported editable movement strategy.");
                }
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    && !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementTargetKind), stage.MovementTargetKind))
                {
                    Error(
                        diagnostics,
                        "CARD_V4_MOVEMENT_TARGET_KIND",
                        path + ".movementTargetKind",
                        "Move stage requires a supported movement target kind.");
                }
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    && stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                    && (!IsFinite(stage.MoveTargetPositionX) || !IsFinite(stage.MoveTargetPositionZ)))
                {
                    Error(
                        diagnostics,
                        "CARD_V4_MOVEMENT_TARGET_POSITION",
                        path + ".moveTargetPosition",
                        "World-position movement target coordinates must be finite.");
                }
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    && stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                    && stage.MoveTargetEntityId < 0)
                {
                    Error(
                        diagnostics,
                        "CARD_V4_MOVEMENT_TARGET_ENTITY",
                        path + ".moveTargetEntityId",
                        "Entity movement target id must be zero (automatic ally) or positive.");
                }
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait && stage.WaitFrames <= 0)
                    Error(diagnostics, "CARD_V2_WAIT_FRAMES", path + ".waitFrames", "Wait requires a positive frame count.");
                if (stage.PlannedStartFrame < 0)
                    Error(diagnostics, "CARD_V2_STAGE_START_FRAME", path + ".plannedStartFrame", "Stage planned start frame cannot be negative.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDurationMode), stage.DurationMode))
                    Error(diagnostics, "CARD_V2_STAGE_DURATION_MODE", path + ".durationMode", "Stage duration mode is invalid.");
                if (stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                    && stage.PlannedDurationFrames <= 0)
                {
                    Error(diagnostics, "CARD_V2_STAGE_DURATION", path + ".plannedDurationFrames", "Fixed stage duration requires a positive frame count.");
                }
                else if (stage.PlannedDurationFrames < 0)
                {
                    Error(diagnostics, "CARD_V2_STAGE_DURATION", path + ".plannedDurationFrames", "Stage planned duration cannot be negative.");
                }
                ValidateConditions(stage.EntryConditions, path + ".entryConditions", conditionIds, diagnostics);
            }
            if (payload.Stages.Count == 0)
                Error(diagnostics, "CARD_V2_STAGES_EMPTY", "$.stages", "Behavior card requires at least one stage.");
            if (!stages.ContainsKey(payload.EntryStageId ?? string.Empty))
                Error(diagnostics, "CARD_V2_ENTRY_UNKNOWN", "$.entryStageId", "Entry stage does not exist.");

            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                string path = "$.detectors[" + i + "]";
                if (detector == null || string.IsNullOrWhiteSpace(detector.DetectorId))
                {
                    Error(diagnostics, "CARD_V2_DETECTOR_ID", path + ".detectorId", "Detector id is required.");
                    continue;
                }
                if (!detectors.Add(detector.DetectorId))
                    Error(diagnostics, "CARD_V2_DETECTOR_DUPLICATE", path + ".detectorId", "Detector id must be unique.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorScope), detector.Scope))
                    Error(diagnostics, "CARD_V2_DETECTOR_SCOPE", path + ".scope", "Detector scope is invalid.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorTrigger), detector.Trigger))
                    Error(diagnostics, "CARD_V2_DETECTOR_TRIGGER", path + ".trigger", "Detector trigger is invalid.");
                if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    && !stages.ContainsKey(detector.StageId ?? string.Empty))
                {
                    Error(diagnostics, "CARD_V2_DETECTOR_STAGE", path + ".stageId", "Stage detector references an unknown stage.");
                }
                if (detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                    && detector.Conditions.Count == 0)
                {
                    Error(diagnostics, "CARD_V2_DETECTOR_CONDITIONS", path + ".conditions", "Condition-edge detector requires conditions.");
                }
                if (detector.ActivationStartFrame < 0)
                    Error(diagnostics, "CARD_V2_DETECTOR_START_FRAME", path + ".activationStartFrame", "Detector activation start frame cannot be negative.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode), detector.WindowEndMode))
                    Error(diagnostics, "CARD_V2_DETECTOR_WINDOW_MODE", path + ".windowEndMode", "Detector window end mode is invalid.");
                if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                    && detector.ActivationDurationFrames <= 0)
                {
                    Error(diagnostics, "CARD_V2_DETECTOR_DURATION", path + ".activationDurationFrames", "Fixed detector window requires a positive frame count.");
                }
                else if (detector.ActivationDurationFrames < 0)
                {
                    Error(diagnostics, "CARD_V2_DETECTOR_DURATION", path + ".activationDurationFrames", "Detector activation duration cannot be negative.");
                }
                if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card
                    && detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd)
                {
                    Error(diagnostics, "CARD_V2_CARD_DETECTOR_STAGE_END", path + ".windowEndMode", "Card detector cannot use StageEnd window mode.");
                }
                ValidateConditions(detector.Conditions, path + ".conditions", conditionIds, diagnostics);
            }

            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                string path = "$.transitions[" + i + "]";
                if (transition == null || string.IsNullOrWhiteSpace(transition.TransitionId))
                {
                    Error(diagnostics, "CARD_V2_TRANSITION_ID", path + ".transitionId", "Transition id is required.");
                    continue;
                }
                if (!transitions.Add(transition.TransitionId))
                    Error(diagnostics, "CARD_V2_TRANSITION_DUPLICATE", path + ".transitionId", "Transition id must be unique.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicTransitionPolicy), transition.Policy))
                    Error(diagnostics, "CARD_V2_TRANSITION_POLICY", path + ".policy", "Transition policy is invalid.");
                if (!detectors.Contains(transition.DetectorId ?? string.Empty))
                    Error(diagnostics, "CARD_V2_TRANSITION_DETECTOR", path + ".detectorId", "Transition references an unknown detector.");
                if (!string.IsNullOrWhiteSpace(transition.SourceStageId)
                    && !stages.ContainsKey(transition.SourceStageId))
                {
                    Error(diagnostics, "CARD_V2_TRANSITION_SOURCE", path + ".sourceStageId", "Transition source stage does not exist.");
                }
                bool ends = string.Equals(transition.TargetStageId, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal);
                CharacterTestRuntimeAiLabLogicStage target = null;
                if (!ends && !stages.TryGetValue(transition.TargetStageId ?? string.Empty, out target))
                {
                    Error(diagnostics, "CARD_V2_TRANSITION_TARGET", path + ".targetStageId", "Transition target stage does not exist.");
                }
                else if (!ends
                    && transition.Policy == CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction
                    && target.OwnsPrimaryAction)
                {
                    Error(diagnostics, "CARD_V2_PRESERVE_ACTION_CONFLICT", path + ".policy", "PreserveAction cannot enter another primary-action stage.");
                }
            }

            var layoutStages = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; payload.Layout != null && i < payload.Layout.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStageLayout layout = payload.Layout.Stages[i];
                string path = "$.layout.stages[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (layout == null || !stages.ContainsKey(layout.StageId ?? string.Empty))
                    Error(diagnostics, "CARD_V2_LAYOUT_STAGE", path + ".stageId", "Layout references an unknown stage.");
                else if (!layoutStages.Add(layout.StageId))
                    Error(diagnostics, "CARD_V2_LAYOUT_DUPLICATE", path + ".stageId", "Stage layout must be unique.");
                if (layout != null && (layout.Column < 0 || layout.Width <= 0f))
                    Error(diagnostics, "CARD_V2_LAYOUT_BOUNDS", path, "Layout column and width are invalid.");
            }

            ValidateReachability(payload, stages, diagnostics);
            ValidatePrimaryActionTiming(payload, diagnostics);

            return diagnostics;
        }

        private static void ValidatePrimaryActionTiming(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int leftIndex = 0; leftIndex < payload.Stages.Count; leftIndex++)
            {
                CharacterTestRuntimeAiLabLogicStage left = payload.Stages[leftIndex];
                if (left == null
                    || !left.OwnsPrimaryAction
                    || left.DurationMode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                    || left.PlannedDurationFrames <= 0)
                {
                    continue;
                }

                int leftEnd = SaturatingAdd(left.PlannedStartFrame, left.PlannedDurationFrames);
                for (int rightIndex = leftIndex + 1; rightIndex < payload.Stages.Count; rightIndex++)
                {
                    CharacterTestRuntimeAiLabLogicStage right = payload.Stages[rightIndex];
                    if (right == null
                        || !right.OwnsPrimaryAction
                        || right.DurationMode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                        || right.PlannedDurationFrames <= 0)
                    {
                        continue;
                    }

                    int rightEnd = SaturatingAdd(right.PlannedStartFrame, right.PlannedDurationFrames);
                    if (left.PlannedStartFrame < rightEnd && right.PlannedStartFrame < leftEnd)
                    {
                        Error(
                            diagnostics,
                            "CARD_V2_PRIMARY_ACTION_TIME_CONFLICT",
                            "$.stages[" + rightIndex + "].plannedStartFrame",
                            "Primary-action stage overlaps another primary-action stage on the authored timeline.");
                    }
                }
            }
        }

        private static int SaturatingAdd(int left, int right)
        {
            long result = (long)left + right;
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        private static void ValidateConditions(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string path,
            HashSet<string> conditionIds,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                string itemPath = path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (condition == null)
                {
                    Error(diagnostics, "CARD_V2_CONDITION_NULL", itemPath, "Condition is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(condition.ConditionId))
                    Error(diagnostics, "CARD_V2_CONDITION_ID", itemPath + ".conditionId", "Condition id is required.");
                else if (!conditionIds.Add(condition.ConditionId))
                    Error(diagnostics, "CARD_V2_CONDITION_DUPLICATE", itemPath + ".conditionId", "Condition id must be unique across the behavior card.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionFact), condition.Fact))
                    Error(diagnostics, "CARD_V2_CONDITION_FACT", itemPath + ".fact", "Condition fact is invalid.");
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionOperator), condition.Operator))
                    Error(diagnostics, "CARD_V2_CONDITION_OPERATOR_ENUM", itemPath + ".operator", "Condition operator is invalid.");
                bool numeric = condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance
                    || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames;
                if (numeric
                    && (float.IsNaN(condition.Value)
                        || float.IsInfinity(condition.Value)
                        || float.IsNaN(condition.MaxValue)
                        || float.IsInfinity(condition.MaxValue)))
                {
                    Error(
                        diagnostics,
                        "CARD_V2_CONDITION_NON_FINITE",
                        itemPath,
                        "Numeric condition values must be finite.");
                }
                if (!numeric
                    && condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && condition.Operator != CharacterTestRuntimeAiLabCardConditionOperator.NotEquals)
                    Error(diagnostics, "CARD_V2_CONDITION_OPERATOR", itemPath + ".operator", "Boolean fact only supports Equals or NotEquals.");
                if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between
                    && condition.MaxValue < condition.Value)
                    Error(diagnostics, "CARD_V2_CONDITION_RANGE", itemPath, "Between maximum must be greater than or equal to minimum.");
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                    && condition.AbilityId <= 0)
                    Error(diagnostics, "CARD_V2_CONDITION_ABILITY", itemPath + ".abilityId", "AbilityAvailable requires an ability id.");
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    && !condition.CharacterActionId.IsValid)
                    Error(diagnostics, "CARD_V2_CONDITION_ACTION", itemPath + ".characterActionId", "SelfCanTransitionToAction requires a CharacterAction id.");
            }
        }

        private static void ValidateReachability(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabLogicStage> stages,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (!stages.ContainsKey(payload.EntryStageId ?? string.Empty))
                return;

            var reachable = new HashSet<string>(StringComparer.Ordinal) { payload.EntryStageId };
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < payload.Transitions.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                    if (transition == null
                        || string.Equals(transition.TargetStageId, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal)
                        || !stages.ContainsKey(transition.TargetStageId ?? string.Empty))
                    {
                        continue;
                    }
                    bool sourceReachable = string.IsNullOrWhiteSpace(transition.SourceStageId)
                        || reachable.Contains(transition.SourceStageId);
                    if (sourceReachable && reachable.Add(transition.TargetStageId))
                        changed = true;
                }
            }
            while (changed);

            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage != null && !string.IsNullOrWhiteSpace(stage.StageId) && !reachable.Contains(stage.StageId))
                {
                    Error(
                        diagnostics,
                        "CARD_V2_STAGE_UNREACHABLE",
                        "$.stages[" + i + "].stageId",
                        "Stage is unreachable from the entry stage.");
                }
            }
        }

        public static bool HasErrors(IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void Error(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledLogicStage
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledCondition> _entryConditions;

        public CharacterTestRuntimeAiLabCompiledLogicStage(
            CharacterTestRuntimeAiLabLogicStage source,
            CharacterTestRuntimeAiLabCompiledCondition[] entryConditions,
            bool frameScopedControlOperation = false)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            StageId = source.StageId ?? string.Empty;
            Order = source.Order;
            Kind = source.Kind;
            Title = source.Title ?? string.Empty;
            AbilityId = source.AbilityId;
            CharacterActionId = source.CharacterActionId;
            LegacyActionKind = source.LegacyActionKind;
            MovementStrategy = source.MovementStrategy;
            MovementTargetKind = source.MovementTargetKind;
            MoveTargetPositionX = source.MoveTargetPositionX;
            MoveTargetPositionZ = source.MoveTargetPositionZ;
            MoveTargetEntityId = source.MoveTargetEntityId;
            MoveMinDistance = source.MoveMinDistance;
            MoveMaxDistance = source.MoveMaxDistance;
            MoveSpeedMultiplier = source.MoveSpeedMultiplier > 0f
                ? source.MoveSpeedMultiplier
                : CharacterTestRuntimeAiLabBehaviorRecipeEventOperation.DefaultMoveSpeedMultiplier;
            WaitFrames = source.WaitFrames;
            PreviewDurationFrames = source.PreviewDurationFrames;
            PlannedStartFrame = source.PlannedStartFrame;
            PlannedDurationFrames = source.PlannedDurationFrames;
            DurationMode = source.DurationMode;
            Note = source.Note ?? string.Empty;
            EntryEventId = source.EntryEventId ?? string.Empty;
            PrimaryActionEventId = source.PrimaryActionEventId ?? string.Empty;
            IsBehaviorRecipeV5Timeline = source.IsBehaviorRecipeV5Timeline;
            FrameScopedControlOperation = frameScopedControlOperation;
            _entryConditions = Array.AsReadOnly(
                entryConditions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
        }

        public string StageId { get; }
        public int Order { get; }
        public CharacterTestRuntimeAiLabCardStepKind Kind { get; }
        public string Title { get; }
        public int AbilityId { get; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; }
        public CharacterTestRuntimeAiPlannerActionKind LegacyActionKind { get; }
        public CharacterTestRuntimeAiLabMovementStrategy MovementStrategy { get; }
        public CharacterTestRuntimeAiLabMovementTargetKind MovementTargetKind { get; }
        public float MoveTargetPositionX { get; }
        public float MoveTargetPositionZ { get; }
        public int MoveTargetEntityId { get; }
        public float MoveMinDistance { get; }
        public float MoveMaxDistance { get; }
        public float MoveSpeedMultiplier { get; }
        public int WaitFrames { get; }
        public int PreviewDurationFrames { get; }
        public int PlannedStartFrame { get; }
        public int PlannedDurationFrames { get; }
        public CharacterTestRuntimeAiLabLogicDurationMode DurationMode { get; }
        public string Note { get; }
        public string EntryEventId { get; }
        public string PrimaryActionEventId { get; }
        public bool IsBehaviorRecipeV5Timeline { get; }
        public bool FrameScopedControlOperation { get; }
        public string TimelineId => StageId;
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> EntryConditions => _entryConditions;
        public bool OwnsPrimaryAction => Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
            || Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
            || Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledLogicDetector
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledCondition> _conditions;

        public CharacterTestRuntimeAiLabCompiledLogicDetector(
            string detectorId,
            string eventId,
            string legacySharedTriggerId,
            string title,
            CharacterTestRuntimeAiLabLogicDetectorScope scope,
            int stageIndex,
            CharacterTestRuntimeAiLabLogicDetectorTrigger trigger,
            int priority,
            int activationStartFrame,
            int activationDurationFrames,
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode windowEndMode,
            bool expectedResult,
            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor localFrameAnchor,
            int localFrame,
            int actionSignalReasonCode,
            bool legacyUnfilteredSignal,
            bool isBehaviorRecipeV5Event,
            CharacterTestRuntimeAiLabCompiledCondition[] conditions)
        {
            DetectorId = detectorId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            LegacySharedTriggerId = legacySharedTriggerId ?? string.Empty;
            Title = title ?? string.Empty;
            Scope = scope;
            StageIndex = stageIndex;
            Trigger = trigger;
            Priority = priority;
            ActivationStartFrame = activationStartFrame;
            ActivationDurationFrames = activationDurationFrames;
            WindowEndMode = windowEndMode;
            ExpectedResult = expectedResult;
            LocalFrameAnchor = localFrameAnchor;
            LocalFrame = localFrame;
            ActionSignalReasonCode = actionSignalReasonCode;
            LegacyUnfilteredSignal = legacyUnfilteredSignal;
            IsBehaviorRecipeV5Event = isBehaviorRecipeV5Event;
            _conditions = Array.AsReadOnly(conditions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
        }

        public string DetectorId { get; }
        public string EventId { get; }
        public string LegacySharedTriggerId { get; }
        public string Title { get; }
        public CharacterTestRuntimeAiLabLogicDetectorScope Scope { get; }
        public int StageIndex { get; }
        public CharacterTestRuntimeAiLabLogicDetectorTrigger Trigger { get; }
        public int Priority { get; }
        public int ActivationStartFrame { get; }
        public int ActivationDurationFrames { get; }
        public CharacterTestRuntimeAiLabLogicDetectorWindowEndMode WindowEndMode { get; }
        public bool ExpectedResult { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor LocalFrameAnchor { get; }
        public int LocalFrame { get; }
        public int ActionSignalReasonCode { get; }
        public bool LegacyUnfilteredSignal { get; }
        public bool IsBehaviorRecipeV5Event { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> Conditions => _conditions;
    }

    internal readonly struct CharacterTestRuntimeAiLabCompiledLogicTransition
    {
        public CharacterTestRuntimeAiLabCompiledLogicTransition(
            string transitionId,
            int detectorIndex,
            int sourceStageIndex,
            int targetStageIndex,
            int priority,
            CharacterTestRuntimeAiLabLogicTransitionPolicy policy,
            string label,
            string endReason,
            bool isBehaviorRecipeV5Event)
        {
            TransitionId = transitionId ?? string.Empty;
            DetectorIndex = detectorIndex;
            SourceStageIndex = sourceStageIndex;
            TargetStageIndex = targetStageIndex;
            Priority = priority;
            Policy = policy;
            Label = label ?? string.Empty;
            EndReason = endReason ?? string.Empty;
            IsBehaviorRecipeV5Event = isBehaviorRecipeV5Event;
        }

        public string TransitionId { get; }
        public int DetectorIndex { get; }
        public int SourceStageIndex { get; }
        public int TargetStageIndex { get; }
        public int Priority { get; }
        public CharacterTestRuntimeAiLabLogicTransitionPolicy Policy { get; }
        public string Label { get; }
        public string EndReason { get; }
        public bool IsBehaviorRecipeV5Event { get; }
        public bool EndsCard => TargetStageIndex < 0;
    }

    internal sealed class CharacterTestRuntimeAiLabCompiledLogicProgram
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabCompiledCondition> _activationConditions;

        public CharacterTestRuntimeAiLabCompiledLogicProgram(
            long semanticHash,
            int entryStageIndex,
            CharacterTestRuntimeAiLabCompiledCondition[] activationConditions,
            CharacterTestRuntimeAiLabCompiledLogicStage[] stages,
            CharacterTestRuntimeAiLabCompiledLogicDetector[] detectors,
            CharacterTestRuntimeAiLabCompiledLogicTransition[] transitions,
            bool isBehaviorRecipeV5,
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipeV6 = null,
            CharacterTestBehaviorExecutionProgram execution = null,
            CharacterTestBehaviorExecutionDebugMap executionDebugMap = null,
            CharacterTestTacticalFrozenSource tacticalSource = null,
            CharacterTestBehaviorCardSelectionDefinition selectionDefinition = null)
        {
            SemanticHash = semanticHash;
            EntryStageIndex = entryStageIndex;
            _activationConditions = Array.AsReadOnly(
                activationConditions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
            Stages = Array.AsReadOnly(stages ?? Array.Empty<CharacterTestRuntimeAiLabCompiledLogicStage>());
            Detectors = Array.AsReadOnly(detectors ?? Array.Empty<CharacterTestRuntimeAiLabCompiledLogicDetector>());
            Transitions = Array.AsReadOnly(transitions ?? Array.Empty<CharacterTestRuntimeAiLabCompiledLogicTransition>());
            IsBehaviorRecipeV5 = isBehaviorRecipeV5;
            RecipeV6 = recipeV6;
            Execution = execution;
            ExecutionDebugMap = executionDebugMap;
            TacticalSource = tacticalSource;
            SelectionDefinition = selectionDefinition;
            if (execution == null) BackgroundPerceptionRequirements = CharacterTestRuntimeAiPerceptionRequirements.All;
            else
            {
                if (execution.Movements.Length > 0) BackgroundPerceptionRequirements |= CharacterTestRuntimeAiPerceptionRequirements.Navigation;
                foreach (var predicate in execution.Predicates)
                    if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.Fact)
                        BackgroundPerceptionRequirements |= RequirementsFor(predicate.Fact) & ~CharacterTestRuntimeAiPerceptionRequirements.PrimaryAttack;
                // Legacy activation guards can still be polled while execution owns the card.
                foreach (var condition in _activationConditions) BackgroundPerceptionRequirements |= RequirementsFor(condition.Fact);
            }
        }

        internal CharacterTestRuntimeAiPerceptionRequirements BackgroundPerceptionRequirements { get; }
        private static CharacterTestRuntimeAiPerceptionRequirements RequirementsFor(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange: return CharacterTestRuntimeAiPerceptionRequirements.PrimaryAttack;
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable: return CharacterTestRuntimeAiPerceptionRequirements.Navigation;
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard: return CharacterTestRuntimeAiPerceptionRequirements.Environment;
                default: return CharacterTestRuntimeAiPerceptionRequirements.None;
            }
        }

        public long SemanticHash { get; }
        public int EntryStageIndex { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> ActivationConditions =>
            _activationConditions;
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicStage> Stages { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicDetector> Detectors { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicTransition> Transitions { get; }
        public bool IsBehaviorRecipeV5 { get; }
        public CharacterTestRuntimeAiLabCompiledRecipeV6 RecipeV6 { get; }
        public CharacterTestBehaviorExecutionProgram Execution { get; }
        public CharacterTestBehaviorExecutionDebugMap ExecutionDebugMap { get; }
        internal CharacterTestTacticalFrozenSource TacticalSource { get; }
        internal CharacterTestBehaviorCardSelectionDefinition SelectionDefinition { get; }
        internal CharacterTestRuntimeAiLabCompiledLogicProgram WithTacticalSource(CharacterTestTacticalFrozenSource source)
            => new CharacterTestRuntimeAiLabCompiledLogicProgram(SemanticHash, EntryStageIndex,
                new List<CharacterTestRuntimeAiLabCompiledCondition>(ActivationConditions).ToArray(),
                new List<CharacterTestRuntimeAiLabCompiledLogicStage>(Stages).ToArray(),
                new List<CharacterTestRuntimeAiLabCompiledLogicDetector>(Detectors).ToArray(),
                new List<CharacterTestRuntimeAiLabCompiledLogicTransition>(Transitions).ToArray(), IsBehaviorRecipeV5,
                RecipeV6, Execution, ExecutionDebugMap, source, SelectionDefinition);
        internal CharacterTestRuntimeAiLabCompiledLogicProgram WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition definition)
            => new CharacterTestRuntimeAiLabCompiledLogicProgram(SemanticHash, EntryStageIndex,
                new List<CharacterTestRuntimeAiLabCompiledCondition>(ActivationConditions).ToArray(),
                new List<CharacterTestRuntimeAiLabCompiledLogicStage>(Stages).ToArray(),
                new List<CharacterTestRuntimeAiLabCompiledLogicDetector>(Detectors).ToArray(),
                new List<CharacterTestRuntimeAiLabCompiledLogicTransition>(Transitions).ToArray(), IsBehaviorRecipeV5,
                RecipeV6, Execution, ExecutionDebugMap, TacticalSource, definition);
        public bool IsBehaviorRecipeV6 => RecipeV6 != null || Execution != null;
        public bool IsBehaviorRecipeV7 => RecipeV6 != null || Execution != null;
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult
    {
        public CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestRuntimeAiLabCardDiagnostic[] diagnostics)
        {
            Program = program;
            Diagnostics = Array.AsReadOnly(diagnostics ?? Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        public CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Diagnostics { get; }
        public bool Success => Program != null && !CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(Diagnostics);
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardLogicCompiler
    {
        /// <remarks>LowFreqAlloc: explicit authoring compile only.</remarks>
        public static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Compile(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = null)
        {
            if (payload != null
                && (CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.HasV8Authority(payload)
                    || payload.Timelines.Count > 0))
            {
                return CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(payload, references, executionOnly: true);
            }

            // Raw legacy payloads keep the v4 evaluator until their authenticated load boundary.
            // Payloads that already carry timelines use the v5 orchestration handshake. Keeping this
            // distinction until the legacy payload has crossed a load/authoring migration boundary
            // preserves the internal v4 test/tool facade without making it a persisted authority.
            bool isBehaviorRecipeV5 = payload?.Timelines != null && payload.Timelines.Count > 0;
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> validation;
            if (isBehaviorRecipeV5)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForWrite(payload);
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
                CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
                validation = CharacterTestRuntimeAiLabBehaviorRecipeV5Validator.Validate(payload, references);
            }
            else
            {
                // A raw v4 object must remain intact until its original schema/hash have been
                // authenticated by the load boundary. Compile-time migration would erase legacy
                // transition policy semantics before that authentication can occur.
                validation = CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);
            }
            var diagnosticList = new List<CharacterTestRuntimeAiLabCardDiagnostic>(validation.Count + 4);
            for (int i = 0; i < validation.Count; i++)
                diagnosticList.Add(validation[i]);
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(validation))
                return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(null, diagnosticList.ToArray());

            if (references != null)
            {
                ValidateConditionReferences(
                    payload.ActivationConditions,
                    "$.activationConditions",
                    references,
                    diagnosticList);
                for (int i = 0; i < payload.Stages.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                    bool usesAbility = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility;
                    if (usesAbility
                        && !references.HasAbility(stage.AbilityId))
                    {
                        diagnosticList.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                            CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                            "CARD_V2_ABILITY_UNKNOWN",
                            "$.stages[" + i + "].abilityId",
                            "Ability reference is not available in the current GameSlice."));
                    }
                    if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                        && !references.HasCharacterAction(stage.CharacterActionId))
                    {
                        diagnosticList.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                            CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                            "CARD_V2_ACTION_UNKNOWN",
                            "$.stages[" + i + "].characterActionId",
                            "CharacterAction reference is not available in the current GameSlice."));
                    }
                    ValidateConditionReferences(
                        stage.EntryConditions,
                        "$.stages[" + i + "].entryConditions",
                        references,
                        diagnosticList);
                }
                for (int i = 0; i < payload.Detectors.Count; i++)
                {
                    ValidateConditionReferences(
                        payload.Detectors[i].Conditions,
                        "$.detectors[" + i + "].conditions",
                        references,
                        diagnosticList);
                }
                if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnosticList))
                {
                    return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                        null,
                        diagnosticList.ToArray());
                }
            }

            var stageIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var stages = new CharacterTestRuntimeAiLabCompiledLogicStage[payload.Stages.Count];
            for (int i = 0; i < stages.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicStage source = payload.Stages[i];
                stages[i] = new CharacterTestRuntimeAiLabCompiledLogicStage(
                    source,
                    CompileConditions(source.EntryConditions));
                stageIndices.Add(stages[i].StageId, i);
            }
            var detectorIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var detectors = new CharacterTestRuntimeAiLabCompiledLogicDetector[payload.Detectors.Count];
            for (int i = 0; i < detectors.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector source = payload.Detectors[i];
                int stageIndex = source.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    ? stageIndices[source.StageId]
                    : -1;
                detectors[i] = new CharacterTestRuntimeAiLabCompiledLogicDetector(
                    source.DetectorId,
                    ResolveProjectedEventId(source),
                    source.LegacySharedTriggerId,
                    source.Title,
                    source.Scope,
                    stageIndex,
                    source.Trigger,
                    source.Priority,
                    source.ActivationStartFrame,
                    source.ActivationDurationFrames,
                    source.WindowEndMode,
                    source.ExpectedResult,
                    source.LocalFrameAnchor,
                    ResolveCompiledLocalFrame(payload, source, references),
                    source.ActionSignalReasonCode,
                    source.LegacyUnfilteredSignal,
                    source.IsBehaviorRecipeV5Event,
                    CompileConditions(source.Conditions));
                detectorIndices.Add(source.DetectorId, i);
            }
            var transitions = new CharacterTestRuntimeAiLabCompiledLogicTransition[payload.Transitions.Count];
            for (int i = 0; i < transitions.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition source = payload.Transitions[i];
                transitions[i] = new CharacterTestRuntimeAiLabCompiledLogicTransition(
                    source.TransitionId,
                    detectorIndices[source.DetectorId],
                    string.IsNullOrWhiteSpace(source.SourceStageId) ? -1 : stageIndices[source.SourceStageId],
                    string.Equals(source.TargetStageId, CharacterTestRuntimeAiLabLogicTransition.EndTarget, StringComparison.Ordinal)
                        ? -1
                        : stageIndices[source.TargetStageId],
                    source.Priority,
                    source.Policy,
                    source.Label,
                    source.EndReason,
                    source.IsBehaviorRecipeV5Event);
            }

            return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                new CharacterTestRuntimeAiLabCompiledLogicProgram(
                    isBehaviorRecipeV5
                        ? CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(payload)
                        : ComputeLegacyV4SemanticHash(payload),
                    stageIndices[payload.EntryStageId],
                    CompileConditions(payload.ActivationConditions),
                    stages,
                    detectors,
                    transitions,
                    isBehaviorRecipeV5),
                diagnosticList.ToArray());
        }

        private static string ResolveProjectedEventId(CharacterTestRuntimeAiLabLogicDetector detector)
        {
            const string prefix = "event.";
            string detectorId = detector?.DetectorId ?? string.Empty;
            return detector != null
                && detector.IsBehaviorRecipeV5Event
                && detectorId.StartsWith(prefix, StringComparison.Ordinal)
                    ? detectorId.Substring(prefix.Length)
                    : detectorId;
        }

        private static int ResolveCompiledLocalFrame(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicDetector detector,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            if (detector == null
                || detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                || detector.LocalFrameAnchor == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame)
            {
                return detector?.LocalFrame ?? 0;
            }
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (timeline != null
                    && string.Equals(timeline.TimelineId, detector.StageId, StringComparison.Ordinal)
                    && CharacterTestRuntimeAiLabBehaviorRecipeV5Validator.TryResolvePrimaryActionEnd(
                        timeline.PrimaryActionEvent,
                        references,
                        out int frame))
                {
                    return frame;
                }
            }
            return 0;
        }

        private static CharacterTestRuntimeAiLabCompiledCondition[] CompileConditions(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            var result = new CharacterTestRuntimeAiLabCompiledCondition[conditions.Count];
            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                result[i] = new CharacterTestRuntimeAiLabCompiledCondition(
                    condition.ConditionId,
                    condition.Fact,
                    condition.Operator,
                    condition.BoolValue,
                    condition.Value,
                    condition.MaxValue,
                    condition.AbilityId,
                    condition.CharacterActionId,
                    condition.AllowedTargetPhases,
                    condition.RequiredTargetFlags,
                    condition.ForbiddenTargetFlags);
            }
            return result;
        }

        private static void ValidateConditionReferences(
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions,
            string path,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                    && !references.HasAbility(condition.AbilityId))
                {
                    diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                        CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                        "CARD_V2_CONDITION_ABILITY_UNKNOWN",
                        path + "[" + i + "].abilityId",
                        "Condition Ability reference is not available in the current GameSlice."));
                }
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    && !references.HasCharacterAction(condition.CharacterActionId))
                {
                    diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                        CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                        "CARD_V2_CONDITION_ACTION_UNKNOWN",
                        path + "[" + i + "].characterActionId",
                        "Condition CharacterAction reference is not available in the current GameSlice."));
                }
            }
        }

        internal static long ComputeSemanticHash(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload != null && CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.HasV11Authority(payload))
                return CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV11(payload);
            if (payload != null && CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.HasV10Authority(payload))
                return CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV10(payload);
            if (payload != null && CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.HasV8Authority(payload))
                return CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV9(payload);
            if (payload != null && CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.HasV7Authority(payload))
                return CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV7(payload);
            if (payload != null && CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
                return CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.Compute(payload);
            return payload?.Timelines != null && payload.Timelines.Count > 0
                ? CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(payload)
                : ComputeLegacyV4SemanticHash(payload);
        }

        internal static long ComputeLegacyV4SemanticHash(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return ComputeLegacySemanticHash(
                payload,
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV4Schema);
        }

        internal static long ComputeLegacyV2SemanticHash(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return ComputeLegacySemanticHash(payload, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV2Schema);
        }

        internal static long ComputeLegacyV3SemanticHash(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return ComputeLegacySemanticHash(payload, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV3Schema);
        }

        private static long ComputeLegacySemanticHash(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string schema)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("cardV2.schema", schema);
            hash.AddStringStable("cardV2.entry", payload.EntryStageId);
            hash.AddStringStable("cardV2.intent", payload.Intent);
            hash.AddStringStable("cardV2.risk", payload.RiskSummary);
            AddConditionsHash(ref hash, payload.ActivationConditions);
            hash.AddInt("cardV2.stageCount", payload.Stages.Count);
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                hash.AddStringStable("cardV2.stage.id", stage.StageId);
                hash.AddInt("cardV2.stage.order", stage.Order);
                hash.AddInt("cardV2.stage.kind", (int)stage.Kind);
                hash.AddInt("cardV2.stage.ability", stage.AbilityId);
                hash.AddInt("cardV2.stage.action", stage.CharacterActionId.Value);
                hash.AddInt("cardV2.stage.legacyAction", (int)stage.LegacyActionKind);
                if (string.Equals(schema, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV4Schema, StringComparison.Ordinal))
                    hash.AddInt("cardV2.stage.movementStrategy", (int)stage.MovementStrategy);
                if (string.Equals(schema, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV4Schema, StringComparison.Ordinal)
                    && (stage.MovementTargetKind != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                        || stage.MoveTargetPositionX != 0f
                        || stage.MoveTargetPositionZ != 0f
                        || stage.MoveTargetEntityId != 0))
                {
                    hash.AddInt("cardV2.stage.movementTargetKind", (int)stage.MovementTargetKind);
                    hash.AddInt("cardV2.stage.moveTargetPositionX", (int)Math.Round(stage.MoveTargetPositionX * 1000f));
                    hash.AddInt("cardV2.stage.moveTargetPositionZ", (int)Math.Round(stage.MoveTargetPositionZ * 1000f));
                    hash.AddInt("cardV2.stage.moveTargetEntityId", stage.MoveTargetEntityId);
                }
                hash.AddInt("cardV2.stage.moveMin", (int)Math.Round(stage.MoveMinDistance * 1000f));
                hash.AddInt("cardV2.stage.moveMax", (int)Math.Round(stage.MoveMaxDistance * 1000f));
                hash.AddInt("cardV2.stage.wait", stage.WaitFrames);
                hash.AddInt("cardV2.stage.plannedStart", stage.PlannedStartFrame);
                hash.AddInt("cardV2.stage.plannedDuration", stage.PlannedDurationFrames);
                hash.AddInt("cardV2.stage.durationMode", (int)stage.DurationMode);
                AddConditionsHash(ref hash, stage.EntryConditions);
            }
            hash.AddInt("cardV2.detectorCount", payload.Detectors.Count);
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                hash.AddStringStable("cardV2.detector.id", detector.DetectorId);
                hash.AddInt("cardV2.detector.scope", (int)detector.Scope);
                hash.AddStringStable("cardV2.detector.stage", detector.StageId);
                hash.AddInt("cardV2.detector.trigger", (int)detector.Trigger);
                hash.AddInt("cardV2.detector.priority", detector.Priority);
                hash.AddInt("cardV2.detector.activationStart", detector.ActivationStartFrame);
                hash.AddInt("cardV2.detector.activationDuration", detector.ActivationDurationFrames);
                hash.AddInt("cardV2.detector.windowEndMode", (int)detector.WindowEndMode);
                AddConditionsHash(ref hash, detector.Conditions);
            }
            hash.AddInt("cardV2.transitionCount", payload.Transitions.Count);
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                hash.AddStringStable("cardV2.transition.id", transition.TransitionId);
                hash.AddStringStable("cardV2.transition.detector", transition.DetectorId);
                hash.AddStringStable("cardV2.transition.source", transition.SourceStageId);
                hash.AddStringStable("cardV2.transition.target", transition.TargetStageId);
                hash.AddInt("cardV2.transition.priority", transition.Priority);
                hash.AddInt("cardV2.transition.policy", (int)transition.Policy);
                hash.AddStringStable("cardV2.transition.endReason", transition.EndReason);
            }
            return hash.ToHash();
        }

        private static void AddConditionsHash(
            ref RuntimeContentHashAccumulator hash,
            IReadOnlyList<CharacterTestRuntimeAiLabCardCondition> conditions)
        {
            hash.AddInt("cardV2.conditions.count", conditions.Count);
            for (int i = 0; i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = conditions[i];
                hash.AddStringStable("cardV2.condition.id", condition.ConditionId);
                hash.AddInt("cardV2.condition.fact", (int)condition.Fact);
                hash.AddInt("cardV2.condition.op", (int)condition.Operator);
                hash.AddInt("cardV2.condition.bool", condition.BoolValue ? 1 : 0);
                hash.AddInt("cardV2.condition.value", (int)Math.Round(condition.Value * 1000f));
                hash.AddInt("cardV2.condition.max", (int)Math.Round(condition.MaxValue * 1000f));
                hash.AddInt("cardV2.condition.ability", condition.AbilityId);
                hash.AddInt("cardV2.condition.action", condition.CharacterActionId.Value);
            }
        }
    }

    internal interface ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime
    {
        bool Evaluate(in CharacterTestRuntimeAiLabCompiledCondition condition);
        CharacterTestRuntimeAiLabLogicActionState PrimaryActionState { get; }
        bool TryEnterStage(CharacterTestRuntimeAiLabCompiledLogicStage stage);
        bool TryInterruptPrimaryAction();
    }

    internal interface ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime
    {
        long PrimaryActionInstanceId { get; }
        int PrimaryActionSignalReasonCode { get; }
        bool PrimaryActionRejected { get; }
        CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState OrchestrationState { get; }
        int OrchestrationFailureReasonCode { get; }
        bool TryBeginTimelineJump(CharacterTestRuntimeAiLabCompiledLogicStage target);
        bool TryBeginReturnToPlanner();
        void CompleteOrchestration();
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState
    {
        None = 0,
        Pending = 1,
        Succeeded = 2,
        Rejected = 3
    }

    internal enum CharacterTestRuntimeAiLabLogicCandidateKind { Detector = 1, Transition = 2 }

    internal interface ICharacterTestRuntimeAiLabBehaviorCardLogicTraceSink
    {
        void BeginLogicCandidate(CharacterTestRuntimeAiLabLogicCandidateKind kind, int index, int stableRank);
        void EndLogicCandidate(
            CharacterTestRuntimeAiLabDecisionTraceCandidateState state,
            int priority,
            int reasonCode);
        void MarkLogicCandidateSelected(CharacterTestRuntimeAiLabLogicCandidateKind kind, int index, int reasonCode);
        void RecordStageActionCandidate(int stageIndex, bool selected, int reasonCode);
    }

    internal readonly struct CharacterTestRuntimeAiLabLogicEvent
    {
        public CharacterTestRuntimeAiLabLogicEvent(
            int frame,
            CharacterTestRuntimeAiLabLogicEventKind kind,
            string stageId,
            string detectorId,
            string transitionId,
            int entityId = 0,
            string behaviorEntryId = "",
            long programSemanticHash = 0L,
            int profileSlot = 0,
            int cardInstanceStartFrame = -1,
            string targetTimelineId = "",
            int localFrame = 0,
            long primaryActionInstanceId = 0L,
            int reasonCode = 0,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind? triggerSourceKind = null,
            CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind? outcomeKind = null,
            int behaviorEntryNumericId = 0, int stageIndex = -1, int detectorIndex = -1, int transitionIndex = -1, int targetTimelineIndex = -1)
        {
            BehaviorEntryNumericId = behaviorEntryNumericId;
            StageIndex = stageIndex; DetectorIndex = detectorIndex; TransitionIndex = transitionIndex; TargetTimelineIndex = targetTimelineIndex;
            Frame = frame;
            Kind = kind;
            StageId = stageId ?? string.Empty;
            DetectorId = detectorId ?? string.Empty;
            TransitionId = transitionId ?? string.Empty;
            EntityId = Math.Max(0, entityId);
            BehaviorEntryId = behaviorEntryId ?? string.Empty;
            ProgramSemanticHash = programSemanticHash;
            ProfileSlot = Math.Max(0, profileSlot);
            CardInstanceStartFrame = cardInstanceStartFrame;
            TargetTimelineId = targetTimelineId ?? string.Empty;
            LocalFrame = Math.Max(0, localFrame);
            PrimaryActionInstanceId = Math.Max(0L, primaryActionInstanceId);
            ReasonCode = reasonCode;
            TriggerSourceKind = triggerSourceKind;
            OutcomeKind = outcomeKind;
        }

        public int BehaviorEntryNumericId { get; }
        public int StageIndex { get; }
        public int DetectorIndex { get; }
        public int TransitionIndex { get; }
        public int TargetTimelineIndex { get; }
        public MxFramework.Runtime.RuntimeTraceIdentity SourceIdentity => CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.TransitionKind, TransitionIndex, BehaviorEntryNumericId);
        public MxFramework.Runtime.RuntimeTraceIdentity StageIdentity => CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.StageKind, StageIndex, BehaviorEntryNumericId);
        public int Frame { get; }
        public CharacterTestRuntimeAiLabLogicEventKind Kind { get; }
        public string StageId { get; }
        public string DetectorId { get; }
        public string TransitionId { get; }
        public int EntityId { get; }
        public string BehaviorEntryId { get; }
        public long ProgramSemanticHash { get; }
        public int ProfileSlot { get; }
        public int CardInstanceStartFrame { get; }
        public string TimelineId => StageId;
        public string EventId => DetectorId;
        public string TargetTimelineId { get; }
        public int LocalFrame { get; }
        public long PrimaryActionInstanceId { get; }
        public int ReasonCode { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind? TriggerSourceKind { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind? OutcomeKind { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicEventBuffer
    {
        private readonly CharacterTestRuntimeAiLabLogicEvent[] _events;

        public CharacterTestRuntimeAiLabLogicEventBuffer(int capacity = 16)
        {
            _events = new CharacterTestRuntimeAiLabLogicEvent[Math.Max(1, capacity)];
        }

        public int Count { get; private set; }
        public int OverflowCount { get; private set; }
        public CharacterTestRuntimeAiLabLogicEvent this[int index] => _events[index];

        public void Clear()
        {
            Count = 0;
            OverflowCount = 0;
        }

        public void Add(in CharacterTestRuntimeAiLabLogicEvent evt)
        {
            if (Count < _events.Length)
            {
                _events[Count++] = evt;
                return;
            }
            OverflowCount++;
        }

        public void AddOverflow(int count)
        {
            if (count > 0)
                OverflowCount += count;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator
    {
        private readonly CharacterTestRuntimeAiLabCompiledLogicProgram _program;
        private readonly ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime _runtime;
        private readonly ICharacterTestRuntimeAiLabBehaviorCardLogicTraceSink _traceSink;
        private readonly bool[] _detectorArmed;
        private readonly bool[] _detectorFired;
        private readonly int[] _sharedEvaluationFrame;
        private readonly bool[] _sharedEvaluationResult;
        private readonly CharacterTestBehaviorExecutionEvaluator _execution;
        private int _currentStageIndex = -1;
        private int _cardStartedFrame = -1;
        private int _stageEnteredFrame = -1;
        private int _pendingTransitionIndex = -1;
        private int _committedTransitionIndexThisFrame = -1;
        private int _lastEvaluatedFrame = -1;
        private long _ownedPrimaryActionInstanceId;
        private string _lastTriggeredEventId = string.Empty;
        private int _lastTriggeredEventIndex = -1;

        public CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime runtime)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (_program.Execution != null)
                _execution = new CharacterTestBehaviorExecutionEvaluator(_program.Execution,
                    runtime as ICharacterTestBehaviorExecutionRuntime ?? throw new ArgumentException("The runtime must support execution tables.", nameof(runtime)), _program.ExecutionDebugMap);
            else if (_program.RecipeV6 != null)
                throw new ArgumentException("Legacy timeline data is a reference format; compile an execution program.", nameof(program));
            _traceSink = runtime as ICharacterTestRuntimeAiLabBehaviorCardLogicTraceSink;
            _detectorArmed = new bool[_program.Detectors.Count];
            _detectorFired = new bool[_program.Detectors.Count];
            _sharedEvaluationFrame = new int[_program.Detectors.Count];
            _sharedEvaluationResult = new bool[_program.Detectors.Count];
            for (int i = 0; i < _detectorArmed.Length; i++)
            {
                _detectorArmed[i] = true;
                _sharedEvaluationFrame[i] = int.MinValue;
            }
        }

        private CharacterTestRuntimeAiLabLogicRuntimeState _legacyState = CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
        public CharacterTestRuntimeAiLabLogicRuntimeState State { get => _execution?.State ?? _legacyState; private set => _legacyState = value; }
        public int CurrentStageIndex => _execution?.CurrentTimelineIndex ?? _currentStageIndex;
        public int PendingTransitionIndex => _pendingTransitionIndex;
        public int CommittedTransitionIndexThisFrame => _committedTransitionIndexThisFrame;
        public int CardStartedFrame => _execution?.CardStartedFrame ?? _cardStartedFrame;
        public int CurrentTimelineIndex => _execution?.CurrentTimelineIndex ?? _currentStageIndex;
        internal CharacterTestBehaviorExecutionEndReason ExecutionEndReason => _execution?.EndReason ?? CharacterTestBehaviorExecutionEndReason.None;
        internal bool NeedsPrimaryAttackFact(int frame) => _execution?.NeedsPrimaryAttackFact(frame) ?? true;
        public int TimelineEnteredFrame => _execution?.TimelineEnteredFrame ?? _stageEnteredFrame;
        public int LocalFrame => _execution != null ? _execution.LocalFrame : _stageEnteredFrame < 0 || _lastEvaluatedFrame < _stageEnteredFrame
            ? 0
            : _lastEvaluatedFrame - _stageEnteredFrame;
        public string LastTriggeredEventId => _execution?.LastTriggeredEventId ?? _lastTriggeredEventId;
        public long OwnedPrimaryActionInstanceId => _execution?.OwnedActionInstanceId ?? _ownedPrimaryActionInstanceId;

        /// <remarks>NoAlloc. Used when a repeatable card instance returns to the inactive pool.</remarks>
        public void Reset()
        {
            if (_execution != null) { _execution.Reset(); return; }
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
            _currentStageIndex = -1;
            _cardStartedFrame = -1;
            _stageEnteredFrame = -1;
            _pendingTransitionIndex = -1;
            _committedTransitionIndexThisFrame = -1;
            _lastEvaluatedFrame = -1;
            _ownedPrimaryActionInstanceId = 0L;
            _lastTriggeredEventId = string.Empty;
            _lastTriggeredEventIndex = -1;
            for (int i = 0; i < _detectorArmed.Length; i++)
            {
                _detectorArmed[i] = true;
                _detectorFired[i] = false;
                _sharedEvaluationFrame[i] = int.MinValue;
                _sharedEvaluationResult[i] = false;
            }
        }

        public CharacterTestRuntimeAiLabLogicEvaluatorSnapshot CreateSnapshot(CharacterTestRuntimeAiLabLogicEvaluatorSnapshot target = null)
        {
            if (_execution != null) return CreateExecutionSnapshot(target);

            target ??= new CharacterTestRuntimeAiLabLogicEvaluatorSnapshot();
            var armed = target.DetectorArmed ?? new bool[_detectorArmed.Length];
            var fired = target.DetectorFired ?? new bool[_detectorFired.Length];
            Array.Copy(_detectorArmed, armed, armed.Length);
            Array.Copy(_detectorFired, fired, fired.Length);
            target.Initialize(
                State,
                _currentStageIndex,
                _cardStartedFrame,
                _stageEnteredFrame,
                _pendingTransitionIndex,
                armed,
                _lastEvaluatedFrame,
                _ownedPrimaryActionInstanceId,
                fired,
                _lastTriggeredEventIndex);
            return target;
        }

        public bool TryRestoreSnapshot(CharacterTestRuntimeAiLabLogicEvaluatorSnapshot snapshot)
        {
            if (_execution != null) return _execution.TryRestoreSnapshot(snapshot?.ExecutionState);
            if (snapshot == null || snapshot.DetectorArmed.Length != _detectorArmed.Length)
                return false;
            if (snapshot.LastTriggeredEventIndex < -1 || snapshot.LastTriggeredEventIndex >= _program.Detectors.Count) return false;
            if (snapshot.CurrentStageIndex < -1 || snapshot.CurrentStageIndex >= _program.Stages.Count)
                return false;
            if (snapshot.PendingTransitionIndex < -1 || snapshot.PendingTransitionIndex >= _program.Transitions.Count)
                return false;

            State = snapshot.State;
            _currentStageIndex = snapshot.CurrentStageIndex;
            _cardStartedFrame = snapshot.CardStartedFrame;
            _stageEnteredFrame = snapshot.StageEnteredFrame;
            _pendingTransitionIndex = snapshot.PendingTransitionIndex;
            _lastEvaluatedFrame = snapshot.LastEvaluatedFrame;
            _ownedPrimaryActionInstanceId = snapshot.OwnedPrimaryActionInstanceId;
            _lastTriggeredEventIndex = snapshot.LastTriggeredEventIndex;
            _lastTriggeredEventId = _lastTriggeredEventIndex < 0 ? string.Empty : _program.Detectors[_lastTriggeredEventIndex].EventId;
            Array.Copy(snapshot.DetectorArmed, _detectorArmed, _detectorArmed.Length);
            if (snapshot.DetectorFired.Length == _detectorFired.Length)
                Array.Copy(snapshot.DetectorFired, _detectorFired, _detectorFired.Length);
            else
                Array.Clear(_detectorFired, 0, _detectorFired.Length);
            NormalizeLegacySharedState();
            for (int i = 0; i < _sharedEvaluationFrame.Length; i++)
            {
                _sharedEvaluationFrame[i] = int.MinValue;
                _sharedEvaluationResult[i] = false;
            }
            return true;
        }

        /// <remarks>NoAlloc after construction.</remarks>
        public bool Start(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (_execution != null) return _execution.Start(frame);
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Inactive)
                return false;
            for (int i = 0; i < _program.ActivationConditions.Count; i++)
            {
                if (!_runtime.Evaluate(_program.ActivationConditions[i]))
                    return false;
            }
            return StartCommitted(frame, events);
        }

        /// <summary>
        /// Starts a binding whose entry action has already been accepted by the authoritative
        /// CharacterAction runtime. Activation was validated before the request and is not
        /// evaluated again after that action has changed the live facts.
        /// </summary>
        /// <remarks>NoAlloc after construction.</remarks>
        public bool StartCommitted(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (_execution != null) return _execution.StartCommitted(frame);
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Inactive)
                return false;
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            _cardStartedFrame = frame;
            return EnterStage(_program.EntryStageIndex, frame, events);
        }

        /// <remarks>NoAlloc after construction.</remarks>
        public void Tick(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            if (_execution != null) { _execution.Tick(frame, events); return; }
            events?.Clear();
            _committedTransitionIndexThisFrame = -1;
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Running
                && State != CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning)
            {
                return;
            }

            int previousEvaluatedFrame = _lastEvaluatedFrame;
            _lastEvaluatedFrame = frame;

            if (_currentStageIndex >= 0 && _currentStageIndex < _program.Stages.Count)
            {
                _traceSink?.RecordStageActionCandidate(
                    _currentStageIndex,
                    selected: true,
                    CharacterTestAiTraceReason.LOGIC_STAGE_HELD);
            }

            if (_pendingTransitionIndex >= 0)
            {
                if (_program.IsBehaviorRecipeV5)
                {
                    AdvancePendingOrchestration(frame, events);
                    return;
                }
                if (_runtime.PrimaryActionState == CharacterTestRuntimeAiLabLogicActionState.Running)
                    return;
                int pending = _pendingTransitionIndex;
                _pendingTransitionIndex = -1;
                CommitTransition(pending, frame, events);
                return;
            }

            if (frame <= _stageEnteredFrame)
                return;

            int bestTransition = -1;
            for (int detectorIndex = 0; detectorIndex < _program.Detectors.Count; detectorIndex++)
            {
                CharacterTestRuntimeAiLabCompiledLogicDetector detector = _program.Detectors[detectorIndex];
                if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    && detector.StageIndex != _currentStageIndex)
                {
                    _detectorArmed[detectorIndex] = true;
                    continue;
                }
                if (!CharacterTestRuntimeAiLabDetectorActivity.IsActive(
                        frame,
                        detector,
                        _cardStartedFrame,
                        _stageEnteredFrame,
                        _currentStageIndex))
                {
                    _detectorArmed[detectorIndex] = true;
                    continue;
                }
                _traceSink?.BeginLogicCandidate(CharacterTestRuntimeAiLabLogicCandidateKind.Detector, detectorIndex, detectorIndex);
                bool firedDetector = string.IsNullOrEmpty(detector.LegacySharedTriggerId)
                    ? DidDetectorFire(detectorIndex, detector, previousEvaluatedFrame, frame)
                    : DidLegacySharedDetectorFire(
                        detectorIndex,
                        previousEvaluatedFrame,
                        frame);
                int transition = firedDetector ? FindBestTransitionAndTrace(detectorIndex) : -1;
                int priority = transition >= 0 ? _program.Transitions[transition].Priority : 0;
                _traceSink?.EndLogicCandidate(
                    firedDetector && transition >= 0
                        ? CharacterTestRuntimeAiLabDecisionTraceCandidateState.Evaluated
                        : CharacterTestRuntimeAiLabDecisionTraceCandidateState.Ineligible,
                    priority,
                    firedDetector
                        ? transition >= 0 ? CharacterTestAiTraceReason.LOGIC_DETECTOR_FIRED : CharacterTestAiTraceReason.LOGIC_TRANSITION_UNAVAILABLE
                        : CharacterTestAiTraceReason.LOGIC_DETECTOR_NOT_FIRED);
                if (!firedDetector)
                    continue;
                SetDetectorFired(detectorIndex, true);
                if (transition < 0)
                    continue;
                CharacterTestRuntimeAiLabCompiledLogicTransition fired = _program.Transitions[transition];
                AddEvent(
                    events,
                    frame,
                    CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                    CurrentStageId(),
                    detector.EventId,
                    fired.TransitionId,
                    fired.EndsCard ? string.Empty : _program.Stages[fired.TargetStageIndex].TimelineId,
                    Math.Max(0, frame - _stageEnteredFrame),
                    _ownedPrimaryActionInstanceId,
                    detector.ActionSignalReasonCode,
                    ResolveTriggerSourceKind(detector),
                    ResolveOutcomeKind(fired), stageIndex: _currentStageIndex, detectorIndex: detectorIndex, transitionIndex: transition, targetTimelineIndex: fired.EndsCard ? -1 : fired.TargetStageIndex);
                if (bestTransition < 0 || CompareTransitions(transition, bestTransition) < 0)
                    bestTransition = transition;
            }

            if (bestTransition < 0)
                return;
            CharacterTestRuntimeAiLabCompiledLogicTransition selectedTransition =
                _program.Transitions[bestTransition];
            _lastTriggeredEventIndex = selectedTransition.DetectorIndex;
            _lastTriggeredEventId =
                _program.Detectors[selectedTransition.DetectorIndex].EventId;
            _traceSink?.MarkLogicCandidateSelected(
                CharacterTestRuntimeAiLabLogicCandidateKind.Detector, selectedTransition.DetectorIndex,
                CharacterTestAiTraceReason.LOGIC_TRANSITION_SELECTED);
            _traceSink?.MarkLogicCandidateSelected(
                CharacterTestRuntimeAiLabLogicCandidateKind.Transition, bestTransition,
                CharacterTestAiTraceReason.LOGIC_TRANSITION_SELECTED);
            BeginTransition(bestTransition, frame, events);
        }

        private bool DidDetectorFire(
            int detectorIndex,
            CharacterTestRuntimeAiLabCompiledLogicDetector detector,
            int previousFrame,
            int frame)
        {
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    return MatchesPrimaryActionSignal(
                        detector,
                        CharacterTestRuntimeAiLabLogicActionState.Completed,
                        rejected: false);
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    return MatchesPrimaryActionSignal(
                        detector,
                        CharacterTestRuntimeAiLabLogicActionState.Failed,
                        rejected: false);
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected:
                    return MatchesPrimaryActionSignal(
                        detector,
                        CharacterTestRuntimeAiLabLogicActionState.Failed,
                        rejected: true);
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame:
                    int origin = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card
                        ? _cardStartedFrame
                        : _stageEnteredFrame;
                    int previousLocalFrame = previousFrame < origin ? -1 : previousFrame - origin;
                    int currentLocalFrame = frame - origin;
                    if (!_detectorArmed[detectorIndex]
                        || detector.LocalFrame < 1
                        || previousLocalFrame >= detector.LocalFrame
                        || currentLocalFrame < detector.LocalFrame)
                    {
                        return false;
                    }
                    SetDetectorArmed(detectorIndex, false);
                    return true;
                default:
                    bool matches = true;
                    for (int i = 0; i < detector.Conditions.Count; i++)
                    {
                        if (_runtime.Evaluate(detector.Conditions[i]))
                            continue;
                        matches = false;
                        break;
                    }
                    bool matchesExpected = matches == detector.ExpectedResult;
                    if (!matchesExpected)
                    {
                        SetDetectorArmed(detectorIndex, true);
                        return false;
                    }
                    if (!_detectorArmed[detectorIndex])
                        return false;
                    SetDetectorArmed(detectorIndex, false);
                    return true;
            }
        }

        private bool DidLegacySharedDetectorFire(
            int detectorIndex,
            int previousFrame,
            int frame)
        {
            int leaderIndex = FindLegacySharedLeader(detectorIndex);
            if (_sharedEvaluationFrame[leaderIndex] != frame)
            {
                _sharedEvaluationFrame[leaderIndex] = frame;
                _sharedEvaluationResult[leaderIndex] = DidDetectorFire(
                    leaderIndex,
                    _program.Detectors[leaderIndex],
                    previousFrame,
                    frame);
            }
            return _sharedEvaluationResult[leaderIndex];
        }

        private int FindLegacySharedLeader(int detectorIndex)
        {
            string sharedId = _program.Detectors[detectorIndex].LegacySharedTriggerId;
            if (string.IsNullOrEmpty(sharedId))
                return detectorIndex;
            for (int i = 0; i < detectorIndex; i++)
            {
                if (string.Equals(
                        _program.Detectors[i].LegacySharedTriggerId,
                        sharedId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return detectorIndex;
        }

        private void SetDetectorArmed(int detectorIndex, bool value)
        {
            string sharedId = _program.Detectors[detectorIndex].LegacySharedTriggerId;
            if (string.IsNullOrEmpty(sharedId))
            {
                _detectorArmed[detectorIndex] = value;
                return;
            }
            for (int i = 0; i < _program.Detectors.Count; i++)
            {
                if (string.Equals(
                        _program.Detectors[i].LegacySharedTriggerId,
                        sharedId,
                        StringComparison.Ordinal))
                {
                    _detectorArmed[i] = value;
                }
            }
        }

        private void SetDetectorFired(int detectorIndex, bool value)
        {
            string sharedId = _program.Detectors[detectorIndex].LegacySharedTriggerId;
            if (string.IsNullOrEmpty(sharedId))
            {
                _detectorFired[detectorIndex] = value;
                return;
            }
            for (int i = 0; i < _program.Detectors.Count; i++)
            {
                if (string.Equals(
                        _program.Detectors[i].LegacySharedTriggerId,
                        sharedId,
                        StringComparison.Ordinal))
                {
                    _detectorFired[i] = value;
                }
            }
        }

        private void NormalizeLegacySharedState()
        {
            for (int detectorIndex = 0; detectorIndex < _program.Detectors.Count; detectorIndex++)
            {
                int leaderIndex = FindLegacySharedLeader(detectorIndex);
                if (leaderIndex == detectorIndex)
                    continue;
                _detectorArmed[detectorIndex] = _detectorArmed[leaderIndex];
                _detectorFired[detectorIndex] = _detectorFired[leaderIndex];
            }
        }

        private bool MatchesPrimaryActionSignal(
            CharacterTestRuntimeAiLabCompiledLogicDetector detector,
            CharacterTestRuntimeAiLabLogicActionState requiredState,
            bool rejected)
        {
            var v5Runtime = _runtime as ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime;
            if (rejected)
            {
                if (v5Runtime == null || !v5Runtime.PrimaryActionRejected)
                    return false;
            }
            else if (_runtime.PrimaryActionState != requiredState)
            {
                return false;
            }
            if (_program.IsBehaviorRecipeV5 && !detector.LegacyUnfilteredSignal)
            {
                if (v5Runtime == null
                    || _ownedPrimaryActionInstanceId <= 0L
                    || v5Runtime.PrimaryActionInstanceId != _ownedPrimaryActionInstanceId)
                {
                    return false;
                }
                if (detector.ActionSignalReasonCode != 0
                    && detector.ActionSignalReasonCode != v5Runtime.PrimaryActionSignalReasonCode)
                {
                    return false;
                }
            }
            return true;
        }

        private int FindBestTransitionAndTrace(int detectorIndex)
        {
            int best = -1;
            for (int i = 0; i < _program.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledLogicTransition transition = _program.Transitions[i];
                if (transition.DetectorIndex != detectorIndex
                    || (transition.SourceStageIndex >= 0 && transition.SourceStageIndex != _currentStageIndex))
                {
                    continue;
                }
                _traceSink?.BeginLogicCandidate(CharacterTestRuntimeAiLabLogicCandidateKind.Transition, i, i);
                _traceSink?.EndLogicCandidate(
                    CharacterTestRuntimeAiLabDecisionTraceCandidateState.Evaluated,
                    transition.Priority,
                    CharacterTestAiTraceReason.LOGIC_TRANSITION_EVALUATED);
                if (best < 0 || CompareTransitions(i, best) < 0)
                    best = i;
            }
            return best;
        }

        private int CompareTransitions(int leftIndex, int rightIndex)
        {
            CharacterTestRuntimeAiLabCompiledLogicTransition left = _program.Transitions[leftIndex];
            CharacterTestRuntimeAiLabCompiledLogicTransition right = _program.Transitions[rightIndex];
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0
                ? priority
                : string.CompareOrdinal(left.TransitionId, right.TransitionId);
        }

        private void BeginTransition(
            int transitionIndex,
            int frame,
            CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            CharacterTestRuntimeAiLabCompiledLogicTransition transition = _program.Transitions[transitionIndex];
            if (_program.IsBehaviorRecipeV5)
            {
                SetPending(transitionIndex, frame, events);
                AdvancePendingOrchestration(frame, events);
                return;
            }
            bool actionRunning = _runtime.PrimaryActionState == CharacterTestRuntimeAiLabLogicActionState.Running;
            if (transition.Policy == CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt && actionRunning)
            {
                if (!_runtime.TryInterruptPrimaryAction())
                {
                    State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                    return;
                }
                AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.ActionInterruptRequested,
                    CurrentStageId(), _program.Detectors[transition.DetectorIndex].DetectorId, transition.TransitionId, stageIndex: _currentStageIndex, detectorIndex: transition.DetectorIndex, transitionIndex: transitionIndex);
                SetPending(transitionIndex, frame, events);
                return;
            }
            if (transition.Policy == CharacterTestRuntimeAiLabLogicTransitionPolicy.AfterActionEnd && actionRunning)
            {
                SetPending(transitionIndex, frame, events);
                return;
            }
            CommitTransition(transitionIndex, frame, events);
        }

        private void SetPending(int transitionIndex, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            _pendingTransitionIndex = transitionIndex;
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning;
            CharacterTestRuntimeAiLabCompiledLogicTransition transition = _program.Transitions[transitionIndex];
            CharacterTestRuntimeAiLabCompiledLogicDetector detector =
                _program.Detectors[transition.DetectorIndex];
            AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending,
                CurrentStageId(), detector.EventId, transition.TransitionId,
                transition.EndsCard ? string.Empty : _program.Stages[transition.TargetStageIndex].TimelineId,
                Math.Max(0, frame - _stageEnteredFrame),
                _ownedPrimaryActionInstanceId,
                detector.ActionSignalReasonCode,
                ResolveTriggerSourceKind(detector),
                ResolveOutcomeKind(transition), stageIndex: _currentStageIndex, detectorIndex: transition.DetectorIndex, transitionIndex: transitionIndex, targetTimelineIndex: transition.EndsCard ? -1 : transition.TargetStageIndex);
        }

        private void AdvancePendingOrchestration(
            int frame,
            CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            var runtime = _runtime as ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime;
            if (runtime == null || _pendingTransitionIndex < 0)
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                return;
            }
            CharacterTestRuntimeAiLabCompiledLogicTransition transition =
                _program.Transitions[_pendingTransitionIndex];
            if (runtime.OrchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None)
            {
                bool began = transition.EndsCard
                    ? runtime.TryBeginReturnToPlanner()
                    : runtime.TryBeginTimelineJump(_program.Stages[transition.TargetStageIndex]);
                if (!began
                    && runtime.OrchestrationState ==
                        CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None)
                {
                    State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                }
                return;
            }
            if (runtime.OrchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending)
                return;
            if (runtime.OrchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected)
            {
                CharacterTestRuntimeAiLabCompiledLogicDetector detector =
                    _program.Detectors[transition.DetectorIndex];
                AddEvent(
                    events,
                    frame,
                    CharacterTestRuntimeAiLabLogicEventKind.ActionStartRejected,
                    CurrentStageId(),
                    detector.EventId,
                    transition.TransitionId,
                    transition.EndsCard
                        ? string.Empty
                        : _program.Stages[transition.TargetStageIndex].TimelineId,
                    Math.Max(0, frame - _stageEnteredFrame),
                    _ownedPrimaryActionInstanceId,
                    runtime.OrchestrationFailureReasonCode,
                    ResolveTriggerSourceKind(detector),
                    ResolveOutcomeKind(transition), stageIndex: _currentStageIndex, detectorIndex: transition.DetectorIndex, transitionIndex: _pendingTransitionIndex, targetTimelineIndex: transition.EndsCard ? -1 : transition.TargetStageIndex);
                _pendingTransitionIndex = -1;
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                runtime.CompleteOrchestration();
                return;
            }

            int pending = _pendingTransitionIndex;
            _pendingTransitionIndex = -1;
            CommitTransition(pending, frame, events);
            runtime.CompleteOrchestration();
        }

        private void CommitTransition(int transitionIndex, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            CharacterTestRuntimeAiLabCompiledLogicTransition transition = _program.Transitions[transitionIndex];
            CharacterTestRuntimeAiLabCompiledLogicDetector detector =
                _program.Detectors[transition.DetectorIndex];
            _committedTransitionIndexThisFrame = transitionIndex;
            AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                CurrentStageId(), detector.EventId, transition.TransitionId,
                transition.EndsCard ? string.Empty : _program.Stages[transition.TargetStageIndex].TimelineId,
                Math.Max(0, frame - _stageEnteredFrame),
                _ownedPrimaryActionInstanceId,
                detector.ActionSignalReasonCode,
                ResolveTriggerSourceKind(detector),
                ResolveOutcomeKind(transition), stageIndex: _currentStageIndex, detectorIndex: transition.DetectorIndex, transitionIndex: transitionIndex, targetTimelineIndex: transition.EndsCard ? -1 : transition.TargetStageIndex);
            if (transition.EndsCard)
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Completed;
                AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.CardEnded,
                    CurrentStageId(), detector.EventId, transition.TransitionId,
                    string.Empty,
                    Math.Max(0, frame - _stageEnteredFrame),
                    _ownedPrimaryActionInstanceId,
                    detector.ActionSignalReasonCode,
                    ResolveTriggerSourceKind(detector),
                    ResolveOutcomeKind(transition), stageIndex: _currentStageIndex, detectorIndex: transition.DetectorIndex, transitionIndex: transitionIndex, targetTimelineIndex: transition.EndsCard ? -1 : transition.TargetStageIndex);
                return;
            }
            State = CharacterTestRuntimeAiLabLogicRuntimeState.Running;
            EnterStage(transition.TargetStageIndex, frame, events);
        }

        private bool EnterStage(int stageIndex, int frame, CharacterTestRuntimeAiLabLogicEventBuffer events)
        {
            CharacterTestRuntimeAiLabCompiledLogicStage stage = _program.Stages[stageIndex];
            if (stage.OwnsPrimaryAction
                && !_program.IsBehaviorRecipeV5
                && _runtime.PrimaryActionState == CharacterTestRuntimeAiLabLogicActionState.Running)
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.ActionStartRejected,
                    stage.StageId, string.Empty, string.Empty, stageIndex: stageIndex);
                return false;
            }
            if (!_runtime.TryEnterStage(stage))
            {
                State = CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
                AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.ActionStartRejected,
                    stage.StageId, string.Empty, string.Empty, stageIndex: stageIndex);
                return false;
            }
            _currentStageIndex = stageIndex;
            _stageEnteredFrame = frame;
            _lastEvaluatedFrame = frame;
            var v5Runtime = _runtime as ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime;
            _ownedPrimaryActionInstanceId = v5Runtime?.PrimaryActionInstanceId ?? 0L;
            if (_program.IsBehaviorRecipeV5)
            {
                for (int i = 0; i < _program.Detectors.Count; i++)
                {
                    CharacterTestRuntimeAiLabCompiledLogicDetector detector = _program.Detectors[i];
                    if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                        && detector.StageIndex == stageIndex)
                    {
                        _detectorArmed[i] = true;
                        _detectorFired[i] = false;
                    }
                }
            }
            _traceSink?.RecordStageActionCandidate(
                stageIndex,
                selected: true,
                CharacterTestAiTraceReason.LOGIC_STAGE_ENTERED);
            AddEvent(events, frame, CharacterTestRuntimeAiLabLogicEventKind.StageEntered,
                stage.StageId, stage.EntryEventId, string.Empty,
                stage.TimelineId,
                0,
                _ownedPrimaryActionInstanceId, stageIndex: stageIndex, targetTimelineIndex: stageIndex);
            return true;
        }


        private CharacterTestRuntimeAiLabLogicEvaluatorSnapshot CreateExecutionSnapshot(CharacterTestRuntimeAiLabLogicEvaluatorSnapshot target = null)
        {
            target ??= new CharacterTestRuntimeAiLabLogicEvaluatorSnapshot();
            var state = target.ExecutionState;
            if (!_execution.CanCopySnapshotTo(state)) state = _execution.CreateSnapshot();
            else _execution.CopySnapshotTo(state);
            target.Initialize(_execution.State, state.Timeline, state.CardStartedFrame,
                state.TimelineEnteredFrame, -1, Array.Empty<bool>(), state.LastWorldFrame, state.ActionInstanceId,
                lastTriggeredEventIndex: state.LastTriggeredEvent, eventExecutionCounts: state.Counts,
                eventLastExecutionFrames: state.LastExecutions, eventResults: state.Results,
                actorOwnerEventIndex: state.ActionOwner, pendingEventIndex: state.PendingAction >= 0 ? state.PendingAction : state.FlowEvent,
                pendingTimelineIndex: state.JumpTarget, localPlayheadFrame: state.LocalFrame, timelinePassGeneration: state.PassGeneration,
                loopRepeatCounts: state.LoopRepeats, activeWaitEventIndex: state.WaitEvent,
                lastSampledPlayheadFrame: state.LastSampledFrame, executionState: state);
            return target;
        }
        private string CurrentStageId()
        {
            return _currentStageIndex >= 0 ? _program.Stages[_currentStageIndex].StageId : string.Empty;
        }

        private static void AddEvent(
            CharacterTestRuntimeAiLabLogicEventBuffer events,
            int frame,
            CharacterTestRuntimeAiLabLogicEventKind kind,
            string stageId,
            string detectorId,
            string transitionId,
            string targetTimelineId = "",
            int localFrame = 0,
            long primaryActionInstanceId = 0L,
            int reasonCode = 0,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind? triggerSourceKind = null,
            CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind? outcomeKind = null,
            int stageIndex = -1, int detectorIndex = -1, int transitionIndex = -1, int targetTimelineIndex = -1)
        {
            if (events == null)
                return;
            var evt = new CharacterTestRuntimeAiLabLogicEvent(
                frame,
                kind,
                stageId,
                detectorId,
                transitionId,
                targetTimelineId: targetTimelineId,
                localFrame: localFrame,
                primaryActionInstanceId: primaryActionInstanceId,
                reasonCode: reasonCode,
                triggerSourceKind: triggerSourceKind,
                outcomeKind: outcomeKind, stageIndex: stageIndex, detectorIndex: detectorIndex, transitionIndex: transitionIndex, targetTimelineIndex: targetTimelineIndex);
            events.Add(evt);
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind?
            ResolveTriggerSourceKind(CharacterTestRuntimeAiLabCompiledLogicDetector detector)
        {
            if (detector == null || !detector.IsBehaviorRecipeV5Event)
                return null;

            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge:
                    return CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame:
                    return CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected:
                    return CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal;
                default:
                    return null;
            }
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind?
            ResolveOutcomeKind(CharacterTestRuntimeAiLabCompiledLogicTransition transition)
        {
            if (!transition.IsBehaviorRecipeV5Event)
                return null;
            return transition.EndsCard
                ? CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                : CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline;
        }
    }

    /// <summary>Warm Replay state for one compiled behavior-card logic instance.</summary>
    /// <remarks>AllocByDesign: captured only at retained-frame snapshot boundaries.</remarks>
    internal sealed class CharacterTestRuntimeAiLabLogicEvaluatorSnapshot
    {
        public CharacterTestRuntimeAiLabLogicEvaluatorSnapshot(
            CharacterTestRuntimeAiLabLogicRuntimeState state,
            int currentStageIndex,
            int cardStartedFrame,
            int stageEnteredFrame,
            int pendingTransitionIndex,
            bool[] detectorArmed,
            int lastEvaluatedFrame = -1,
            long ownedPrimaryActionInstanceId = 0L,
            bool[] detectorFired = null,
            int lastTriggeredEventIndex = -1,
            int[] eventExecutionCounts = null,
            int[] eventLastExecutionFrames = null,
            int[] eventResults = null,
            int[] previousEventResults = null,
            int actorOwnerEventIndex = -1,
            int pendingEventIndex = -1,
            int pendingTimelineIndex = -1,
            int pendingV6Operation = 0,
            int localPlayheadFrame = -1,
            int timelinePassGeneration = 0,
            int[] loopRepeatCounts = null,
            int activeWaitEventIndex = -1,
            int pendingPlayheadTargetFrame = -1,
            int pendingTimeControlEventIndex = -1,
            bool waitingForInstantOwnerAtTimelineEnd = false,
            int lastSampledPlayheadFrame = -1, CharacterTestBehaviorExecutionState executionState = null)
        {
            Initialize(state, currentStageIndex, cardStartedFrame, stageEnteredFrame, pendingTransitionIndex, detectorArmed, lastEvaluatedFrame, ownedPrimaryActionInstanceId, detectorFired, lastTriggeredEventIndex, eventExecutionCounts, eventLastExecutionFrames, eventResults, previousEventResults, actorOwnerEventIndex, pendingEventIndex, pendingTimelineIndex, pendingV6Operation, localPlayheadFrame, timelinePassGeneration, loopRepeatCounts, activeWaitEventIndex, pendingPlayheadTargetFrame, pendingTimeControlEventIndex, waitingForInstantOwnerAtTimelineEnd, lastSampledPlayheadFrame, executionState);
        }

        internal CharacterTestRuntimeAiLabLogicEvaluatorSnapshot() { }

        internal void Initialize(
            CharacterTestRuntimeAiLabLogicRuntimeState state,
            int currentStageIndex,
            int cardStartedFrame,
            int stageEnteredFrame,
            int pendingTransitionIndex,
            bool[] detectorArmed,
            int lastEvaluatedFrame = -1,
            long ownedPrimaryActionInstanceId = 0L,
            bool[] detectorFired = null,
            int lastTriggeredEventIndex = -1,
            int[] eventExecutionCounts = null,
            int[] eventLastExecutionFrames = null,
            int[] eventResults = null,
            int[] previousEventResults = null,
            int actorOwnerEventIndex = -1,
            int pendingEventIndex = -1,
            int pendingTimelineIndex = -1,
            int pendingV6Operation = 0,
            int localPlayheadFrame = -1,
            int timelinePassGeneration = 0,
            int[] loopRepeatCounts = null,
            int activeWaitEventIndex = -1,
            int pendingPlayheadTargetFrame = -1,
            int pendingTimeControlEventIndex = -1,
            bool waitingForInstantOwnerAtTimelineEnd = false,
            int lastSampledPlayheadFrame = -1, CharacterTestBehaviorExecutionState executionState = null)
        {
            State = state;
            CurrentStageIndex = currentStageIndex;
            CardStartedFrame = cardStartedFrame;
            StageEnteredFrame = stageEnteredFrame;
            PendingTransitionIndex = pendingTransitionIndex;
            DetectorArmed = detectorArmed ?? Array.Empty<bool>();
            LastEvaluatedFrame = lastEvaluatedFrame;
            OwnedPrimaryActionInstanceId = ownedPrimaryActionInstanceId;
            DetectorFired = detectorFired ?? Array.Empty<bool>();
            LastTriggeredEventIndex = lastTriggeredEventIndex;
            EventExecutionCounts = eventExecutionCounts ?? Array.Empty<int>();
            EventLastExecutionFrames = eventLastExecutionFrames ?? Array.Empty<int>();
            EventResults = eventResults ?? Array.Empty<int>();
            PreviousEventResults = previousEventResults ?? Array.Empty<int>();
            ActorOwnerEventIndex = actorOwnerEventIndex;
            PendingEventIndex = pendingEventIndex;
            PendingTimelineIndex = pendingTimelineIndex;
            PendingV6Operation = pendingV6Operation;
            LocalPlayheadFrame = localPlayheadFrame >= 0
                ? localPlayheadFrame
                : StageEnteredFrame < 0 || LastEvaluatedFrame < StageEnteredFrame
                    ? 0
                    : LastEvaluatedFrame - StageEnteredFrame;
            TimelinePassGeneration = timelinePassGeneration;
            LoopRepeatCounts = loopRepeatCounts ?? Array.Empty<int>();
            ActiveWaitEventIndex = activeWaitEventIndex;
            PendingPlayheadTargetFrame = pendingPlayheadTargetFrame;
            PendingTimeControlEventIndex = pendingTimeControlEventIndex;
            WaitingForInstantOwnerAtTimelineEnd = waitingForInstantOwnerAtTimelineEnd;
            LastSampledPlayheadFrame = lastSampledPlayheadFrame;
            ExecutionState = executionState;
        }

        public CharacterTestBehaviorExecutionState ExecutionState { get; private set; }
        public CharacterTestRuntimeAiLabLogicRuntimeState State { get; private set; }
        public int CurrentStageIndex { get; private set; }
        public int CurrentTimelineIndex => CurrentStageIndex;
        public int CardStartedFrame { get; private set; }
        public int StageEnteredFrame { get; private set; }
        public int TimelineEnteredFrame => StageEnteredFrame;
        public int LocalFrame => LocalPlayheadFrame;
        public int LocalPlayheadFrame { get; private set; }
        public int TimelinePassGeneration { get; private set; }
        public int[] LoopRepeatCounts { get; private set; }
        public int ActiveWaitEventIndex { get; private set; }
        public int PendingPlayheadTargetFrame { get; private set; }
        public int PendingTimeControlEventIndex { get; private set; }
        public bool WaitingForInstantOwnerAtTimelineEnd { get; private set; }
        public int LastSampledPlayheadFrame { get; private set; }
        public int PendingTransitionIndex { get; private set; }
        public bool[] DetectorArmed { get; private set; }
        public int LastEvaluatedFrame { get; private set; }
        public long OwnedPrimaryActionInstanceId { get; private set; }
        public bool[] DetectorFired { get; private set; }
        public int LastTriggeredEventIndex { get; private set; }
        public int[] EventExecutionCounts { get; private set; }
        public int[] EventLastExecutionFrames { get; private set; }
        public int[] EventResults { get; private set; }
        public int[] PreviousEventResults { get; private set; }
        public int ActorOwnerEventIndex { get; private set; }
        public int PendingEventIndex { get; private set; }
        public int PendingTimelineIndex { get; private set; }
        public int PendingV6Operation { get; private set; }
    }
}
