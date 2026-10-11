using System;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>Stable schema identities for Trigger authoring and compiled plans.</summary>
    public static class GameplayTriggerConfigSchemas
    {
        /// <remarks>NoAlloc.</remarks>
        public const string AuthoringId = "mx.gameplayTriggers.authoring.v1";
        /// <remarks>NoAlloc.</remarks>
        public const int AuthoringVersion = 1;
        /// <remarks>NoAlloc.</remarks>
        public const string RuntimeCatalogId = "mx.gameplayTriggers.runtimeCatalog.v1";
        /// <remarks>NoAlloc.</remarks>
        public const int RuntimeCatalogVersion = 1;
    }

    /// <summary>Deterministic fixed-point authoring representation.</summary>
    public enum GameplayTriggerFix64InputKind
    {
        Raw = 0,
        InvariantDecimal = 1
    }

    /// <summary>Raw Fix64 or invariant decimal input. Validation and quantization are AllocByDesign.</summary>
    public readonly struct GameplayTriggerFix64Authoring
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerFix64Authoring(GameplayTriggerFix64InputKind kind, long rawValue, string invariantDecimal = null)
        {
            Kind = kind;
            RawValue = rawValue;
            InvariantDecimal = invariantDecimal ?? string.Empty;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerFix64InputKind Kind { get; }
        /// <remarks>NoAlloc.</remarks>
        public long RawValue { get; }
        /// <remarks>NoAlloc.</remarks>
        public string InvariantDecimal { get; }
        /// <remarks>NoAlloc.</remarks>
        public static GameplayTriggerFix64Authoring FromRaw(long rawValue) =>
            new GameplayTriggerFix64Authoring(GameplayTriggerFix64InputKind.Raw, rawValue);
        /// <remarks>NoAlloc.</remarks>
        public static GameplayTriggerFix64Authoring FromInvariantDecimal(string value) =>
            new GameplayTriggerFix64Authoring(GameplayTriggerFix64InputKind.InvariantDecimal, 0L, value);
    }

    /// <summary>External authoring document. Trusted retirement and composition facts are deliberately excluded.</summary>
    public sealed class GameplayTriggerAuthoringDocument
    {
        /// <remarks>AllocByDesign. The document retains caller-owned authoring arrays.</remarks>
        public GameplayTriggerAuthoringDocument(
            string schemaId,
            int schemaVersion,
            GameplayTriggerSourceAuthoring[] sources,
            GameplayTriggerConditionAuthoring[] conditions,
            GameplayTriggerDefinitionAuthoring[] definitions,
            GameplayTriggerBindingAuthoring[] bindings)
        {
            SchemaId = schemaId ?? string.Empty;
            SchemaVersion = schemaVersion;
            Sources = sources;
            Conditions = conditions;
            Definitions = definitions;
            Bindings = bindings;
        }

        /// <remarks>NoAlloc.</remarks>
        public string SchemaId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SchemaVersion { get; }
        /// <remarks>NoAlloc. Returns the caller-owned authoring array; compilation copies before canonicalization.</remarks>
        public GameplayTriggerSourceAuthoring[] Sources { get; }
        /// <remarks>NoAlloc. Returns the caller-owned authoring array; compilation copies before canonicalization.</remarks>
        public GameplayTriggerConditionAuthoring[] Conditions { get; }
        /// <remarks>NoAlloc. Returns the caller-owned authoring array; compilation copies before canonicalization.</remarks>
        public GameplayTriggerDefinitionAuthoring[] Definitions { get; }
        /// <remarks>NoAlloc. Returns the caller-owned authoring array; compilation copies before canonicalization.</remarks>
        public GameplayTriggerBindingAuthoring[] Bindings { get; }
    }

    /// <summary>Authoring row for one source definition.</summary>
    public sealed class GameplayTriggerSourceAuthoring
    {
        /// <remarks>AllocByDesign.</remarks>
        public GameplayTriggerSourceAuthoring(
            int sourceDefinitionId,
            GameplayTriggerSourceKind sourceKind,
            GameplayTriggerSignalOrdering signalOrdering = GameplayTriggerSignalOrdering.UnorderedMultiset,
            GameplayTriggerThresholdDirection thresholdDirection = GameplayTriggerThresholdDirection.High,
            GameplayTriggerFix64Authoring enterThreshold = default,
            GameplayTriggerFix64Authoring exitThreshold = default,
            GameplayTriggerInitialSamplePolicy initialSamplePolicy = GameplayTriggerInitialSamplePolicy.PrimeSilently,
            GameplayTriggerSubjectDeparturePolicy departurePolicy = GameplayTriggerSubjectDeparturePolicy.RemoveSilently)
        {
            SourceDefinitionId = sourceDefinitionId;
            SourceKind = sourceKind;
            SignalOrdering = signalOrdering;
            ThresholdDirection = thresholdDirection;
            EnterThreshold = enterThreshold;
            ExitThreshold = exitThreshold;
            InitialSamplePolicy = initialSamplePolicy;
            DeparturePolicy = departurePolicy;
        }

        /// <remarks>NoAlloc.</remarks>
        public int SourceDefinitionId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceKind SourceKind { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSignalOrdering SignalOrdering { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerThresholdDirection ThresholdDirection { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerFix64Authoring EnterThreshold { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerFix64Authoring ExitThreshold { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerInitialSamplePolicy InitialSamplePolicy { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSubjectDeparturePolicy DeparturePolicy { get; }
    }

    /// <summary>Stable authoring identity and root for one reusable condition tree.</summary>
    public sealed class GameplayTriggerConditionAuthoring
    {
        /// <remarks>AllocByDesign.</remarks>
        public GameplayTriggerConditionAuthoring(int conditionId, GameplayTriggerConditionNodeAuthoring root)
        {
            ConditionId = conditionId;
            Root = root;
        }

        /// <remarks>NoAlloc.</remarks>
        public int ConditionId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConditionNodeAuthoring Root { get; }
    }

    /// <summary>One condition tree node. Child order is semantic and is never canonical-set sorted.</summary>
    public sealed class GameplayTriggerConditionNodeAuthoring
    {
        /// <remarks>AllocByDesign. Retains the caller-owned semantic child array.</remarks>
        public GameplayTriggerConditionNodeAuthoring(
            GameplayTriggerConditionOperation operation,
            long operand = 0L,
            GameplayTriggerComparison comparison = GameplayTriggerComparison.Equal,
            GameplayTriggerConditionNodeAuthoring[] children = null)
        {
            Operation = operation;
            Operand = operand;
            Comparison = comparison;
            Children = children ?? Array.Empty<GameplayTriggerConditionNodeAuthoring>();
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConditionOperation Operation { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Operand { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerComparison Comparison { get; }
        /// <remarks>NoAlloc. Returns the caller-owned semantic child array.</remarks>
        public GameplayTriggerConditionNodeAuthoring[] Children { get; }
    }

    /// <summary>Authoring row for one Trigger definition.</summary>
    public sealed class GameplayTriggerDefinitionAuthoring
    {
        /// <remarks>AllocByDesign. Retains the caller-owned route array.</remarks>
        public GameplayTriggerDefinitionAuthoring(int definitionId, int definitionVersion, int sourceDefinitionId, GameplayTriggerRouteAuthoring[] routes)
        {
            DefinitionId = definitionId;
            DefinitionVersion = definitionVersion;
            SourceDefinitionId = sourceDefinitionId;
            Routes = routes;
        }

        /// <remarks>NoAlloc.</remarks>
        public int DefinitionId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DefinitionVersion { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SourceDefinitionId { get; }
        /// <remarks>NoAlloc. Returns the caller-owned route array; compilation copies before canonicalization.</remarks>
        public GameplayTriggerRouteAuthoring[] Routes { get; }
    }

    /// <summary>Authoring row for one route. Action and condition order remain semantic.</summary>
    public sealed class GameplayTriggerRouteAuthoring
    {
        /// <remarks>AllocByDesign. Retains the caller-owned semantic action array.</remarks>
        public GameplayTriggerRouteAuthoring(
            int routeId,
            GameplayTriggerOccurrenceKind occurrenceMask,
            int conditionId,
            GameplayTriggerActionAuthoring[] actions,
            GameplayTriggerStateScope stateScope = GameplayTriggerStateScope.PerInstance,
            int maxActivations = 0,
            long cooldownFrames = 0L,
            GameplayTriggerPresenceLimit presenceLimit = GameplayTriggerPresenceLimit.Ignore,
            long stayIntervalFrames = 0L,
            int executionOrder = 0,
            int exclusiveGroupId = 0,
            int winnerPriority = 0,
            GameplayTriggerArbitrationScope arbitrationScope = GameplayTriggerArbitrationScope.Global,
            int maxStateKeys = 1,
            bool allowRepeatingCycle = false,
            long minIntervalFrames = 0L)
        {
            RouteId = routeId;
            OccurrenceMask = occurrenceMask;
            ConditionId = conditionId;
            Actions = actions;
            StateScope = stateScope;
            MaxActivations = maxActivations;
            CooldownFrames = cooldownFrames;
            PresenceLimit = presenceLimit;
            StayIntervalFrames = stayIntervalFrames;
            ExecutionOrder = executionOrder;
            ExclusiveGroupId = exclusiveGroupId;
            WinnerPriority = winnerPriority;
            ArbitrationScope = arbitrationScope;
            MaxStateKeys = maxStateKeys;
            AllowRepeatingCycle = allowRepeatingCycle;
            MinIntervalFrames = minIntervalFrames;
        }

        /// <remarks>NoAlloc.</remarks>
        public int RouteId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerOccurrenceKind OccurrenceMask { get; }
        /// <remarks>NoAlloc.</remarks>
        public int ConditionId { get; }
        /// <remarks>NoAlloc. Returns the caller-owned semantic action array.</remarks>
        public GameplayTriggerActionAuthoring[] Actions { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerStateScope StateScope { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxActivations { get; }
        /// <remarks>NoAlloc.</remarks>
        public long CooldownFrames { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerPresenceLimit PresenceLimit { get; }
        /// <remarks>NoAlloc.</remarks>
        public long StayIntervalFrames { get; }
        /// <remarks>NoAlloc.</remarks>
        public int ExecutionOrder { get; }
        /// <remarks>NoAlloc.</remarks>
        public int ExclusiveGroupId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int WinnerPriority { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerArbitrationScope ArbitrationScope { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxStateKeys { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool AllowRepeatingCycle { get; }
        /// <remarks>NoAlloc.</remarks>
        public long MinIntervalFrames { get; }
    }

    /// <summary>Authoring row for one ordered action instruction.</summary>
    public readonly struct GameplayTriggerActionAuthoring
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionAuthoring(int actionKindId, GameplayTriggerTargetSelector targetSelector, int targetSlotId = 0, long payload0 = 0L, long payload1 = 0L, long payload2 = 0L)
        {
            ActionKindId = actionKindId;
            TargetSelector = targetSelector;
            TargetSlotId = targetSlotId;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
        }

        /// <remarks>NoAlloc.</remarks>
        public int ActionKindId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerTargetSelector TargetSelector { get; }
        /// <remarks>NoAlloc.</remarks>
        public int TargetSlotId { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Payload0 { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Payload1 { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Payload2 { get; }
    }

    /// <summary>Authoring row for one static source binding.</summary>
    public sealed class GameplayTriggerBindingAuthoring
    {
        /// <remarks>AllocByDesign. Retains the caller-owned explicit target array.</remarks>
        public GameplayTriggerBindingAuthoring(
            int bindingId,
            int definitionId,
            int sourceBindingId,
            int ownerIndex = 0,
            int ownerGeneration = 0,
            bool initiallyEnabled = true,
            GameplayTriggerBindingTargetAuthoring[] explicitTargets = null)
        {
            BindingId = bindingId;
            DefinitionId = definitionId;
            SourceBindingId = sourceBindingId;
            OwnerIndex = ownerIndex;
            OwnerGeneration = ownerGeneration;
            InitiallyEnabled = initiallyEnabled;
            ExplicitTargets = explicitTargets ?? Array.Empty<GameplayTriggerBindingTargetAuthoring>();
        }

        /// <remarks>NoAlloc.</remarks>
        public int BindingId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DefinitionId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SourceBindingId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int OwnerIndex { get; }
        /// <remarks>NoAlloc.</remarks>
        public int OwnerGeneration { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool InitiallyEnabled { get; }
        /// <remarks>NoAlloc. Returns the caller-owned explicit target array.</remarks>
        public GameplayTriggerBindingTargetAuthoring[] ExplicitTargets { get; }
    }

    /// <summary>Authoring row for one binding-owned target slot.</summary>
    public readonly struct GameplayTriggerBindingTargetAuthoring
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerBindingTargetAuthoring(int slotId, int domainId, long value, int generation = 0)
        {
            SlotId = slotId;
            DomainId = domainId;
            Value = value;
            Generation = generation;
        }

        /// <remarks>NoAlloc.</remarks>
        public int SlotId { get; }
        /// <remarks>NoAlloc.</remarks>
        public int DomainId { get; }
        /// <remarks>NoAlloc.</remarks>
        public long Value { get; }
        /// <remarks>NoAlloc.</remarks>
        public int Generation { get; }
    }
}
