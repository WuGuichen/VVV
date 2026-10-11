using System;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerSourceKind
    {
        Signal = 0,
        Occupancy = 1,
        Threshold = 2
    }

    [Flags]
    public enum GameplayTriggerOccurrenceKind
    {
        None = 0,
        Entered = 1,
        Stayed = 2,
        Exited = 4,
        Pulsed = 8
    }

    public enum GameplayTriggerSignalOrdering
    {
        Ordered = 0,
        UnorderedMultiset = 1
    }

    public enum GameplayTriggerThresholdDirection
    {
        High = 0,
        Low = 1
    }

    public enum GameplayTriggerInitialSamplePolicy
    {
        PrimeSilently = 0,
        EmitEnteredIfActive = 1
    }

    public enum GameplayTriggerSubjectDeparturePolicy
    {
        RemoveSilently = 0,
        EmitExited = 1
    }

    public enum GameplayTriggerStateScope
    {
        PerInstance = 0,
        PerSubject = 1,
        PerInstigator = 2,
        PerPair = 3
    }

    public enum GameplayTriggerPresenceLimit
    {
        Ignore = 0,
        OncePerPresence = 1
    }

    public enum GameplayTriggerArbitrationScope
    {
        Global = 0,
        PerSubject = 1,
        PerInstigator = 2,
        PerPair = 3
    }

    public enum GameplayTriggerTargetSelector
    {
        Owner = 0,
        Subject = 1,
        Instigator = 2,
        ExplicitBindingTarget = 3
    }

    public enum GameplayTriggerDeliveryContract
    {
        SameFrameReserved = 0,
        ReliableDeferred = 1,
        PresentationOnly = 2,
        InternalDeferred = 3
    }

    public enum GameplayTriggerInternalDeferredOperationKind
    {
        None = 0,
        SetEnabled = 1,
        Reset = 2,
        Signal = 3
    }

    public enum GameplayTriggerErrorCode
    {
        None = 0,
        InvalidArgument = 1,
        InvalidFrame = 2,
        UnknownSourceBinding = 3,
        SourceKindMismatch = 4,
        DuplicateSourceBatch = 5,
        InputCapacityExceeded = 6,
        BatchIncomplete = 7,
        DuplicateScalarSubject = 8,
        OccurrenceCapacityExceeded = 9,
        CandidateCapacityExceeded = 10,
        RouteStateCapacityExceeded = 11,
        IntentCapacityExceeded = 12,
        ReservationRejected = 13,
        ConditionRejected = 14,
        PolicyRejected = 15,
        ArbitrationSuppressed = 16,
        Disabled = 17,
        InvalidCatalog = 18,
        UnknownActionKind = 19,
        MissingScopeEntity = 20,
        MissingActionTarget = 21,
        DeferredQueueCapacityExceeded = 22,
        DeferredFrameOverflow = 23,
        DeferredInvalidTarget = 24,
        DeferredStaleTarget = 25,
        DeferredInputCapacityExceeded = 26,
        DeferredSignalCollision = 27,
        DeferredSequenceExhausted = 28,
        DeferredSourceKindMismatch = 29,
        ReliableOutboxCapacityExceeded = 30,
        ReliableOutboxFrameOverflow = 31,
        ReliableOutboxSequenceExhausted = 32
    }

    /// <summary>Fixed capacities for a trigger runtime session.</summary>
    public readonly struct GameplayTriggerRuntimeOptions
    {
        public GameplayTriggerRuntimeOptions(
            int maxInputBatches,
            int maxInputItems,
            int maxRelations,
            int maxOccurrences,
            int maxCandidates,
            int maxRouteStates,
            int maxActivations,
            int maxIntents,
            int maxPreparedActions,
            int maxConditionStackDepth = 32)
            : this(
                maxInputBatches,
                maxInputItems,
                maxRelations,
                maxOccurrences,
                maxCandidates,
                maxRouteStates,
                maxActivations,
                maxIntents,
                maxPreparedActions,
                maxConditionStackDepth,
                0,
                0)
        {
        }

        public GameplayTriggerRuntimeOptions(
            int maxInputBatches,
            int maxInputItems,
            int maxRelations,
            int maxOccurrences,
            int maxCandidates,
            int maxRouteStates,
            int maxActivations,
            int maxIntents,
            int maxPreparedActions,
            int maxConditionStackDepth,
            int maxDeferredRecords)
            : this(
                maxInputBatches,
                maxInputItems,
                maxRelations,
                maxOccurrences,
                maxCandidates,
                maxRouteStates,
                maxActivations,
                maxIntents,
                maxPreparedActions,
                maxConditionStackDepth,
                maxDeferredRecords,
                0)
        {
        }

        public GameplayTriggerRuntimeOptions(
            int maxInputBatches,
            int maxInputItems,
            int maxRelations,
            int maxOccurrences,
            int maxCandidates,
            int maxRouteStates,
            int maxActivations,
            int maxIntents,
            int maxPreparedActions,
            int maxConditionStackDepth,
            int maxDeferredRecords,
            int maxReliableOutboxRecords)
        {
            if (maxInputBatches <= 0) throw new ArgumentOutOfRangeException(nameof(maxInputBatches));
            if (maxInputItems <= 0) throw new ArgumentOutOfRangeException(nameof(maxInputItems));
            if (maxRelations <= 0) throw new ArgumentOutOfRangeException(nameof(maxRelations));
            if (maxOccurrences <= 0) throw new ArgumentOutOfRangeException(nameof(maxOccurrences));
            if (maxCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maxCandidates));
            if (maxRouteStates <= 0) throw new ArgumentOutOfRangeException(nameof(maxRouteStates));
            if (maxActivations <= 0) throw new ArgumentOutOfRangeException(nameof(maxActivations));
            if (maxIntents <= 0) throw new ArgumentOutOfRangeException(nameof(maxIntents));
            if (maxPreparedActions <= 0) throw new ArgumentOutOfRangeException(nameof(maxPreparedActions));
            if (maxConditionStackDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxConditionStackDepth));
            if (maxDeferredRecords < 0) throw new ArgumentOutOfRangeException(nameof(maxDeferredRecords));
            if (maxReliableOutboxRecords < 0) throw new ArgumentOutOfRangeException(nameof(maxReliableOutboxRecords));
            MaxInputBatches = maxInputBatches;
            MaxInputItems = maxInputItems;
            MaxRelations = maxRelations;
            MaxOccurrences = maxOccurrences;
            MaxCandidates = maxCandidates;
            MaxRouteStates = maxRouteStates;
            MaxActivations = maxActivations;
            MaxIntents = maxIntents;
            MaxPreparedActions = maxPreparedActions;
            MaxConditionStackDepth = maxConditionStackDepth;
            MaxDeferredRecords = maxDeferredRecords;
            MaxReliableOutboxRecords = maxReliableOutboxRecords;
        }

        public int MaxInputBatches { get; }
        public int MaxInputItems { get; }
        public int MaxRelations { get; }
        public int MaxOccurrences { get; }
        public int MaxCandidates { get; }
        public int MaxRouteStates { get; }
        public int MaxActivations { get; }
        public int MaxIntents { get; }
        public int MaxPreparedActions { get; }
        public int MaxConditionStackDepth { get; }
        public int MaxDeferredRecords { get; }
        public int MaxReliableOutboxRecords { get; }
    }

    /// <summary>Result of a no-allocation input or evaluation operation.</summary>
    public readonly struct GameplayTriggerResult
    {
        public GameplayTriggerResult(bool success, GameplayTriggerErrorCode errorCode, int detail0 = 0, int detail1 = 0)
        {
            Success = success;
            ErrorCode = errorCode;
            Detail0 = detail0;
            Detail1 = detail1;
        }

        public bool Success { get; }
        public GameplayTriggerErrorCode ErrorCode { get; }
        public int Detail0 { get; }
        public int Detail1 { get; }
        public static GameplayTriggerResult Succeeded => new GameplayTriggerResult(true, GameplayTriggerErrorCode.None);
    }

    /// <summary>Complete threshold sample for one optional subject.</summary>
    public readonly struct GameplayTriggerScalarSample
    {
        public GameplayTriggerScalarSample(GameplayEntityId subject, Fix64 value) { Subject = subject; Value = value; }
        public GameplayEntityId Subject { get; }
        public Fix64 Value { get; }
    }

    /// <summary>Replayable numeric signal fact.</summary>
    public readonly struct GameplayTriggerSignal
    {
        public GameplayTriggerSignal(int signalId, GameplayEntityId subject = default, GameplayEntityId instigator = default, long payload0 = 0L, long payload1 = 0L, long payload2 = 0L, long sequence = 0L)
        {
            if (signalId <= 0) throw new ArgumentOutOfRangeException(nameof(signalId));
            if (sequence < 0L) throw new ArgumentOutOfRangeException(nameof(sequence));
            SignalId = signalId;
            Subject = subject;
            Instigator = instigator;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
            Sequence = sequence;
        }

        public int SignalId { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public long Payload0 { get; }
        public long Payload1 { get; }
        public long Payload2 { get; }
        public long Sequence { get; }
    }

    /// <summary>Canonical source occurrence consumed by route evaluation.</summary>
    public readonly struct GameplayTriggerOccurrence
    {
        internal GameplayTriggerOccurrence(
            RuntimeFrame frame,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject,
            GameplayEntityId instigator,
            int signalId,
            Fix64 previousScalar,
            Fix64 currentScalar,
            long payload0,
            long payload1,
            long payload2,
            long orderingValue,
            long presenceEpoch,
            long presenceStartFrame)
        {
            Frame = frame;
            InstanceId = instanceId;
            Kind = kind;
            Subject = subject;
            Instigator = instigator;
            SignalId = signalId;
            PreviousScalar = previousScalar;
            CurrentScalar = currentScalar;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
            OrderingValue = orderingValue;
            PresenceEpoch = presenceEpoch;
            PresenceStartFrame = presenceStartFrame;
        }

        public RuntimeFrame Frame { get; }
        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerOccurrenceKind Kind { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public int SignalId { get; }
        public Fix64 PreviousScalar { get; }
        public Fix64 CurrentScalar { get; }
        public long Payload0 { get; }
        public long Payload1 { get; }
        public long Payload2 { get; }
        public long OrderingValue { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }

        internal GameplayTriggerOccurrence WithOrderingValue(long orderingValue) =>
            new GameplayTriggerOccurrence(
                Frame,
                InstanceId,
                Kind,
                Subject,
                Instigator,
                SignalId,
                PreviousScalar,
                CurrentScalar,
                Payload0,
                Payload1,
                Payload2,
                orderingValue,
                PresenceEpoch,
                PresenceStartFrame);
    }

    public readonly struct GameplayTriggerActivationPolicy
    {
        public GameplayTriggerActivationPolicy(
            GameplayTriggerStateScope stateScope = GameplayTriggerStateScope.PerInstance,
            int maxActivations = 0,
            long cooldownFrames = 0L,
            GameplayTriggerPresenceLimit presenceLimit = GameplayTriggerPresenceLimit.Ignore,
            long stayIntervalFrames = 0L)
        {
            if (maxActivations < 0) throw new ArgumentOutOfRangeException(nameof(maxActivations));
            if (cooldownFrames < 0L) throw new ArgumentOutOfRangeException(nameof(cooldownFrames));
            if (stayIntervalFrames < 0L) throw new ArgumentOutOfRangeException(nameof(stayIntervalFrames));
            StateScope = stateScope;
            MaxActivations = maxActivations;
            CooldownFrames = cooldownFrames;
            PresenceLimit = presenceLimit;
            StayIntervalFrames = stayIntervalFrames;
        }

        public GameplayTriggerStateScope StateScope { get; }
        public int MaxActivations { get; }
        public long CooldownFrames { get; }
        public GameplayTriggerPresenceLimit PresenceLimit { get; }
        public long StayIntervalFrames { get; }
    }

    public readonly struct GameplayTriggerActionInstruction
    {
        public GameplayTriggerActionInstruction(GameplayTriggerActionKindId actionKindId, GameplayTriggerTargetSelector targetSelector, int targetSlotId = 0, long payload0 = 0L, long payload1 = 0L, long payload2 = 0L)
        {
            if (targetSelector == GameplayTriggerTargetSelector.ExplicitBindingTarget && targetSlotId <= 0) throw new ArgumentOutOfRangeException(nameof(targetSlotId));
            ActionKindId = actionKindId;
            TargetSelector = targetSelector;
            TargetSlotId = targetSlotId;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
        }

        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerTargetSelector TargetSelector { get; }
        public int TargetSlotId { get; }
        public long Payload0 { get; }
        public long Payload1 { get; }
        public long Payload2 { get; }
    }

    /// <summary>One explicit, binding-owned action target slot.</summary>
    public readonly struct GameplayTriggerBindingTarget
    {
        public GameplayTriggerBindingTarget(int slotId, GameplayTriggerTargetRef target)
        {
            if (slotId <= 0) throw new ArgumentOutOfRangeException(nameof(slotId));
            if (!target.IsValid) throw new ArgumentOutOfRangeException(nameof(target));
            SlotId = slotId;
            Target = target;
        }

        public int SlotId { get; }
        public GameplayTriggerTargetRef Target { get; }
    }

    /// <summary>Domain-qualified numeric target identity used by action sinks.</summary>
    public readonly struct GameplayTriggerTargetRef : IEquatable<GameplayTriggerTargetRef>
    {
        public const int GameplayEntityDomainId = 1;
        public const int GameplayTriggerSourceBindingDomainId = 2;

        public GameplayTriggerTargetRef(int domainId, long value, int generation = 0)
        {
            if (domainId <= 0) throw new ArgumentOutOfRangeException(nameof(domainId));
            if (value <= 0L) throw new ArgumentOutOfRangeException(nameof(value));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            DomainId = domainId;
            Value = value;
            Generation = generation;
        }

        public int DomainId { get; }
        public long Value { get; }
        public int Generation { get; }
        public bool IsValid => DomainId > 0 && Value > 0L && Generation >= 0;

        public static GameplayTriggerTargetRef FromGameplayEntity(GameplayEntityId entityId) =>
            entityId.IsValid
                ? new GameplayTriggerTargetRef(GameplayEntityDomainId, entityId.Index, entityId.Generation)
                : default;

        public static GameplayTriggerTargetRef FromTriggerSourceBinding(
            GameplayTriggerSourceBindingId sourceBindingId) =>
            sourceBindingId.IsValid
                ? new GameplayTriggerTargetRef(
                    GameplayTriggerSourceBindingDomainId,
                    sourceBindingId.Value,
                    0)
                : default;

        public bool Equals(GameplayTriggerTargetRef other) =>
            DomainId == other.DomainId && Value == other.Value && Generation == other.Generation;

        public override bool Equals(object obj) => obj is GameplayTriggerTargetRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(DomainId, Value, Generation);
        public static bool operator ==(GameplayTriggerTargetRef left, GameplayTriggerTargetRef right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerTargetRef left, GameplayTriggerTargetRef right) => !left.Equals(right);
    }

    /// <summary>Prepared action data before an activation id is committed.</summary>
    public readonly struct GameplayTriggerActionPreparation
    {
        internal GameplayTriggerActionPreparation(
            RuntimeFrame frame,
            GameplayTriggerBindingId bindingId,
            GameplayEntityId owner,
            GameplayTriggerTargetRef target,
            GameplayTriggerRouteId routeId,
            int actionIndex,
            GameplayTriggerOccurrence occurrence,
            GameplayTriggerActionInstruction instruction)
        {
            Frame = frame;
            BindingId = bindingId;
            Owner = owner;
            Target = target;
            RouteId = routeId;
            ActionIndex = actionIndex;
            Occurrence = occurrence;
            Instruction = instruction;
        }

        public RuntimeFrame Frame { get; }
        public GameplayTriggerBindingId BindingId { get; }
        public GameplayEntityId Owner { get; }
        public GameplayTriggerTargetRef Target { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerOccurrence Occurrence { get; }
        public GameplayTriggerActionInstruction Instruction { get; }
    }

    /// <summary>Committed action intent emitted by an accepted activation.</summary>
    public readonly struct GameplayTriggerActionIntent
    {
        internal GameplayTriggerActionIntent(long activationId, GameplayTriggerActionPreparation preparation)
        {
            ActivationId = activationId;
            Frame = preparation.Frame;
            BindingId = preparation.BindingId;
            Owner = preparation.Owner;
            Target = preparation.Target;
            RouteId = preparation.RouteId;
            ActionIndex = preparation.ActionIndex;
            Occurrence = preparation.Occurrence;
            Instruction = preparation.Instruction;
        }

        public long ActivationId { get; }
        public RuntimeFrame Frame { get; }
        public GameplayTriggerBindingId BindingId { get; }
        public GameplayEntityId Owner { get; }
        public GameplayTriggerTargetRef Target { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerOccurrence Occurrence { get; }
        public GameplayTriggerActionInstruction Instruction { get; }
    }

    public readonly struct GameplayTriggerActivation
    {
        internal GameplayTriggerActivation(long activationId, GameplayTriggerDefinitionId definitionId, GameplayTriggerBindingId bindingId, GameplayTriggerRouteId routeId, GameplayTriggerOccurrence occurrence)
        {
            ActivationId = activationId;
            DefinitionId = definitionId;
            BindingId = bindingId;
            RouteId = routeId;
            Occurrence = occurrence;
        }

        public long ActivationId { get; }
        public GameplayTriggerDefinitionId DefinitionId { get; }
        public GameplayTriggerBindingId BindingId { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public GameplayTriggerOccurrence Occurrence { get; }
    }

    /// <summary>Reservation-aware action sink. Implementations must not allocate after initialization.</summary>
    public interface IGameplayTriggerActionSink
    {
        GameplayTriggerActionKindId ActionKindId { get; }
        GameplayTriggerDeliveryContract DeliveryContract { get; }
        bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token);
        void Abort(int token);
        void Commit(int token, in GameplayTriggerActionIntent intent);
    }

    /// <summary>Optional structural preflight for ReliableDeferred actions before Trigger reserves outbox authority.</summary>
    public interface IGameplayTriggerReliableActionPreparationValidator
    {
        /// <remarks>NoAlloc. Failure must not mutate target, activation, policy, or outbox authority.</remarks>
        bool TryValidateReliableAction(in GameplayTriggerActionPreparation preparation);
    }
}
