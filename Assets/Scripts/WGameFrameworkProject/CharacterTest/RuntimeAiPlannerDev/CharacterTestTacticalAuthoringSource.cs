using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Editable tactical authoring data; never consumed by the runtime evaluator.</summary>
    /// <remarks>LowFreqAlloc. Clone before compiling or freezing a candidate.</remarks>
    internal sealed class CharacterTestTacticalAuthoringSource
    {
        internal const string CurrentSchema = "mx.character-test.tactical-card-source.v2";
        internal const string BlockSchema = "mx.character-test.tactical-card-source.v3";
        internal const string TimingSchema = "mx.character-test.tactical-card-source.v4";
        internal const string PreparationSchema = "mx.character-test.tactical-card-source.v5";
        internal bool HasPreparationVersion => Schema == PreparationSchema && Version == 5;
        internal bool HasTimingVersion => Schema == TimingSchema && Version == 4 || HasPreparationVersion;
        internal bool HasBlocksVersion => Schema == BlockSchema && Version == 3 || HasTimingVersion;
        internal bool IsSupportedVersion => HasBlocksVersion || Schema == CurrentSchema && Version == 2;
        internal const string EventSetTrialSchema = "mx.character-test.tactical-event-set.trial.v1";
        internal const string EventSetSchema = "mx.character-test.tactical-event-set.v1";
        [JsonProperty(Required = Required.Always)] public string Schema { get; set; } = CurrentSchema;
        [JsonProperty(Required = Required.Always)] public int Version { get; set; } = 2;
        public MxFramework.Game.CharacterTest.TacticalCard.TacticalPointMoveAuthoring ExecutionInput { get; set; }
        public TacticalCard.TacticalPositionRecallAuthoring PositionRecall { get; set; }
        public TacticalCard.TacticalInteractionPreparationAuthoring InteractionPreparation { get; set; }
        public string Name { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public string EntryTimelineId { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public List<CharacterTestTacticalAuthoringSlot> Slots { get; set; } = new List<CharacterTestTacticalAuthoringSlot>();
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ActivationConditionRoot { get; set; }
        [JsonProperty(Required = Required.Always)] public List<CharacterTestTacticalPredicateBinding> PredicateBindings { get; set; } = new List<CharacterTestTacticalPredicateBinding>();
        [JsonProperty(Required = Required.Always)] public List<CharacterTestTacticalAuthoringTimeline> Timelines { get; set; } = new List<CharacterTestTacticalAuthoringTimeline>();

        internal CharacterTestTacticalAuthoringSource Clone()
            => CharacterTestTacticalAuthoringJson.ReadSharedTrial(CharacterTestTacticalAuthoringJson.Write(this));
    }

    internal sealed class CharacterTestTacticalAuthoringSlot
    {
        [JsonProperty(Required = Required.Always)] public string SlotId { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public string Resolver { get; set; } = "lightAttackOpener";
        internal bool IsFunctional => Resolver == "stepBack" || Resolver == "stepForward";
        public string Description { get; set; } = string.Empty;
    }

    internal sealed class CharacterTestTacticalPredicateBinding
    {
        [JsonProperty(Required = Required.Always)] public string NodeId { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public string Kind { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public string SourceActionEventId { get; set; } = string.Empty;
        public string SourceBlockId { get; set; }
        public string SourceMovementEventId { get; set; }
        public string SourceTurnEventId { get; set; }
    }

    internal sealed class CharacterTestTacticalAuthoringTimeline
    {
        [JsonProperty(Required = Required.Always)] public string TimelineId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode EntryConditionRoot { get; set; }
        [JsonProperty(Required = Required.Always)] public List<CharacterTestTacticalAuthoringEvent> Events { get; set; } = new List<CharacterTestTacticalAuthoringEvent>();
        public List<CharacterTestTacticalEventBlock> EventBlocks { get; set; } = new List<CharacterTestTacticalEventBlock>();
        public bool ShouldSerializeEventBlocks() => EventBlocks == null || EventBlocks.Count > 0;
    }

    internal sealed class CharacterTestTacticalEventBlock
    {
        [JsonProperty(Required = Required.Always)] public string BlockId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public CharacterTestTacticalBlockTiming Timing { get; set; }
        public CharacterTestTacticalDistanceData DistanceData { get; set; }
        public string Completion { get; set; } = "Timed";
        public string CompletionEventId { get; set; } = string.Empty;
        public int PreparationTimeoutFrames { get; set; }
        public int CompletionTimeoutFrames { get; set; }
        public int MaximumGuardFrames { get; set; }
        public bool ShouldSerializeMaximumGuardFrames() => MaximumGuardFrames != 0;
        public bool RequireLivingTarget { get; set; }
        public bool ContinueToActionOnPreparationTimeout { get; set; }
        public bool PassiveWait { get; set; }
        public bool ShouldSerializePassiveWait() => PassiveWait;
        public bool ShouldSerializePreparationTimeoutFrames() => PreparationTimeoutFrames != 0;
        public bool ShouldSerializeCompletionTimeoutFrames() => CompletionTimeoutFrames != 0;
        public bool ShouldSerializeRequireLivingTarget() => RequireLivingTarget;
        public bool ShouldSerializeContinueToActionOnPreparationTimeout() => ContinueToActionOnPreparationTimeout;
        public string SuccessConditionEventId { get; set; } = string.Empty;
        public bool ShouldSerializeCompletion() => Completion != "Timed";
        public bool ShouldSerializeCompletionEventId() => !string.IsNullOrEmpty(CompletionEventId);
        public bool ShouldSerializeSuccessConditionEventId() => !string.IsNullOrEmpty(SuccessConditionEventId);
        [JsonProperty(Required = Required.Always)] public CharacterTestTacticalFrameReference Start { get; set; } = new CharacterTestTacticalFrameReference();
        [JsonProperty(Required = Required.Always)] public List<string> EventIds { get; set; } = new List<string>();
    }

    /// <summary>Public configuration data of an intent block; never an execution result.</summary>
    internal sealed class CharacterTestTacticalDistanceData
    {
        [JsonProperty(Required = Required.Always)] public string Kind { get; set; } = "Fixed";
        public string SourceActionEventId { get; set; }
        [JsonProperty(Required = Required.Always)] public string Metric { get; set; } = "RootPlanarDistance";
        [JsonProperty(Required = Required.Always)] public float Minimum { get; set; }
        [JsonProperty(Required = Required.Always)] public float Maximum { get; set; }
    }

    internal sealed class CharacterTestTacticalAuthoringEvent
    {
        [JsonProperty(Required = Required.Always)] public string EventId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        [JsonProperty(Required = Required.Always)] public string TimingKind { get; set; } = "Duration";
        [JsonProperty(Required = Required.Always)] public CharacterTestTacticalFrameReference Start { get; set; } = new CharacterTestTacticalFrameReference();
        public CharacterTestTacticalFrameReference End { get; set; }
        public CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ConditionRoot { get; set; }
        [JsonProperty(Required = Required.Always)] public CharacterTestTacticalAuthoringOperation Operation { get; set; } = new CharacterTestTacticalAuthoringOperation();
        public int Priority { get; set; }
        public int MaxExecutionsPerPass { get; set; } = 1;
        public int CooldownFrames { get; set; }
    }

    internal sealed class CharacterTestTacticalFrameReference
    {
        [JsonProperty(Required = Required.Always)] public string Kind { get; set; } = "Absolute";
        public int Frame { get; set; }
        public string SourceEventId { get; set; } = string.Empty;
        public string Anchor { get; set; } = string.Empty;
        public int OffsetFrames { get; set; }
        public double Ratio { get; set; } = 1d;
        public bool ShouldSerializeRatio() => Kind == "Ratio";
        public string Space { get; set; } = "Timeline";
        public string TargetKind { get; set; } = "Event";
        public bool ShouldSerializeSpace() => Space != "Timeline";
        public bool ShouldSerializeTargetKind() => TargetKind != "Event";
        public bool ShouldSerializeFrame() => Kind == "Absolute" || Frame != 0;
        public bool ShouldSerializeSourceEventId() => Kind == "Anchor" || !string.IsNullOrEmpty(SourceEventId);
        public bool ShouldSerializeAnchor() => Kind == "Anchor" || !string.IsNullOrEmpty(Anchor);
        public bool ShouldSerializeOffsetFrames() => Kind == "Anchor" || OffsetFrames != 0;
    }

    internal sealed class CharacterTestTacticalAuthoringOperation
    {
        [JsonProperty(Required = Required.Always)] public string Kind { get; set; } = string.Empty;
        public string SlotId { get; set; } = string.Empty;
        public string SourceActionEventId { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public float Speed { get; set; } = 1f;
        public CharacterTestTacticalMovement Movement { get; set; }
        public string TargetTimelineId { get; set; }
        public string FacingSource { get; set; }
        public string FacingActionSlotId { get; set; }
        public float? FacingToleranceDegrees { get; set; }
        internal (float Center, float Tolerance) ResolveFacing(CharacterTestTacticalCardInputs inputs)
        {
            if (FacingSource == "Manual" && FacingToleranceDegrees.HasValue)
                return (0f, FacingToleranceDegrees.Value);
            if (FacingSource == "ActionFacing" && inputs != null)
            {
                var sector = inputs.SlotRange(FacingActionSlotId).Sector;
                return (sector.CenterMilliDegrees / 1000f, sector.ToleranceMilliDegrees / 1000f);
            }
            throw new InvalidOperationException("转向事件的面向参数尚未配置或装配数据未准备。");
        }
        public int MaxLoopRepeats { get; set; }
        public bool ShouldSerializeMaxLoopRepeats() => Kind == "LoopRegion";
    }

    internal readonly struct CharacterTestTacticalAuthoringDiagnostic
    {
        internal CharacterTestTacticalAuthoringDiagnostic(string code, string path, string message)
        { Code = code; Path = path; Message = message; }
        internal string Code { get; }
        internal string Path { get; }
        internal string Message { get; }
        public override string ToString() => Code + " at " + Path + ": " + Message;
    }
}
