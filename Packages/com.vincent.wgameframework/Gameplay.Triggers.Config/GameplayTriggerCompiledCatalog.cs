using System;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>AllocByDesign compiler limits for untrusted authoring trees and rows.</summary>
    public readonly struct GameplayTriggerCompilerOptions
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCompilerOptions(int maxConditionTreeDepth, int maxConditionInstructions, int maxConditionStackDepth, int maxRoutesPerDefinition, int maxActionsPerRoute, int maxTotalRows)
        {
            MaxConditionTreeDepth = maxConditionTreeDepth;
            MaxConditionInstructions = maxConditionInstructions;
            MaxConditionStackDepth = maxConditionStackDepth;
            MaxRoutesPerDefinition = maxRoutesPerDefinition;
            MaxActionsPerRoute = maxActionsPerRoute;
            MaxTotalRows = maxTotalRows;
        }

        /// <remarks>NoAlloc.</remarks>
        public int MaxConditionTreeDepth { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxConditionInstructions { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxConditionStackDepth { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxRoutesPerDefinition { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxActionsPerRoute { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxTotalRows { get; }
        /// <remarks>NoAlloc.</remarks>
        public static GameplayTriggerCompilerOptions Default => new GameplayTriggerCompilerOptions(32, 256, 64, 64, 32, 4096);
    }

    /// <summary>Unclamped deterministic structural workload.</summary>
    public readonly struct GameplayTriggerRuntimeCapacityShape
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRuntimeCapacityShape(int instances, int inputBatches, int inputItems, int relations, int occurrences, int candidates, int routeStates, int activations, int intents, int preparedActions, int conditionStackDepth, int deferredRecords, int reliableOutboxRecords)
        {
            Instances = instances;
            MaxInputBatches = inputBatches;
            MaxInputItems = inputItems;
            MaxRelations = relations;
            MaxOccurrences = occurrences;
            MaxCandidates = candidates;
            MaxRouteStates = routeStates;
            MaxActivations = activations;
            MaxIntents = intents;
            MaxPreparedActions = preparedActions;
            MaxConditionStackDepth = conditionStackDepth;
            MaxDeferredRecords = deferredRecords;
            MaxReliableOutboxRecords = reliableOutboxRecords;
        }

        /// <remarks>NoAlloc.</remarks>
        public int Instances { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputBatches { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputItems { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxRelations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxOccurrences { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxCandidates { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxRouteStates { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxActivations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxIntents { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxPreparedActions { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxConditionStackDepth { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxDeferredRecords { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxReliableOutboxRecords { get; }
    }

    /// <summary>Constructible minimum capacities for GameplayTriggerRuntimeOptions.</summary>
    public readonly struct GameplayTriggerRuntimeCapacityRequirements
    {
        internal GameplayTriggerRuntimeCapacityRequirements(GameplayTriggerRuntimeCapacityShape shape, bool hasDeferredActions, bool hasReliableActions)
        {
            RequiredInstances = Math.Max(1, shape.Instances);
            MaxInputBatches = Math.Max(1, shape.MaxInputBatches);
            MaxInputItems = Math.Max(1, shape.MaxInputItems);
            MaxRelations = Math.Max(1, shape.MaxRelations);
            MaxOccurrences = Math.Max(1, shape.MaxOccurrences);
            MaxCandidates = Math.Max(1, shape.MaxCandidates);
            MaxRouteStates = Math.Max(1, shape.MaxRouteStates);
            MaxActivations = Math.Max(1, shape.MaxActivations);
            MaxIntents = Math.Max(1, shape.MaxIntents);
            MaxPreparedActions = Math.Max(1, shape.MaxPreparedActions);
            MaxConditionStackDepth = Math.Max(1, shape.MaxConditionStackDepth);
            MaxDeferredRecords = hasDeferredActions ? Math.Max(1, shape.MaxDeferredRecords) : 0;
            MaxReliableOutboxRecords = hasReliableActions ? Math.Max(1, shape.MaxReliableOutboxRecords) : 0;
        }

        /// <remarks>NoAlloc.</remarks>
        public int RequiredInstances { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputBatches { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputItems { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxRelations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxOccurrences { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxCandidates { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxRouteStates { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxActivations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxIntents { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxPreparedActions { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxConditionStackDepth { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxDeferredRecords { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxReliableOutboxRecords { get; }
    }

    /// <summary>Caller-owned startup capacity profile. Validation is typed and AllocByDesign.</summary>
    public readonly struct GameplayTriggerRuntimeCapacityProfile
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRuntimeCapacityProfile(int maxInstances, int maxInputBatches, int maxInputItems, int maxRelations, int maxOccurrences, int maxCandidates, int maxRouteStates, int maxActivations, int maxIntents, int maxPreparedActions, int maxConditionStackDepth, int maxDeferredRecords, int maxReliableOutboxRecords)
        {
            MaxInstances = maxInstances;
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

        /// <remarks>NoAlloc.</remarks>
        public int MaxInstances { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputBatches { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputItems { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxRelations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxOccurrences { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxCandidates { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxRouteStates { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxActivations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxIntents { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxPreparedActions { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxConditionStackDepth { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxDeferredRecords { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxReliableOutboxRecords { get; }

        internal GameplayTriggerRuntimeOptions ToRuntimeOptions() => new GameplayTriggerRuntimeOptions(MaxInputBatches, MaxInputItems, MaxRelations, MaxOccurrences, MaxCandidates, MaxRouteStates, MaxActivations, MaxIntents, MaxPreparedActions, MaxConditionStackDepth, MaxDeferredRecords, MaxReliableOutboxRecords);
    }

    /// <summary>Frozen compile-time action requirement without composition objects.</summary>
    public readonly struct GameplayTriggerCompiledActionRequirement
    {
        internal GameplayTriggerCompiledActionRequirement(GameplayTriggerActionRequirementDescriptor descriptor)
        {
            ActionKindId = descriptor.ActionKindId;
            DeliveryContract = descriptor.DeliveryContract;
            InternalOperationKind = descriptor.InternalOperationKind;
            SignalId = descriptor.SignalId;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionKindId ActionKindId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerDeliveryContract DeliveryContract { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerInternalDeferredOperationKind InternalOperationKind { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SignalId { get; }
    }

    /// <summary>Frozen trusted provider requirement without bridge/provider object identity.</summary>
    public readonly struct GameplayTriggerCompiledSourceRequirement
    {
        internal GameplayTriggerCompiledSourceRequirement(GameplayTriggerSourceBindingContractDescriptor descriptor)
        {
            SourceBindingId = descriptor.SourceBindingId;
            SourceDefinitionId = descriptor.SourceDefinitionId;
            SourceKind = descriptor.SourceKind;
            ProviderId = descriptor.ProviderId;
            EntityCapabilities = descriptor.EntityCapabilities;
            CompleteBatchPerEvaluation = descriptor.CompleteBatchPerEvaluation;
            MaxInputItemsPerEvaluation = descriptor.MaxInputItemsPerEvaluation;
            MaxActiveRelations = descriptor.MaxActiveRelations;
            SignalEnvelopeSchemaVersion = descriptor.SignalEnvelopeSchemaVersion;
            SignalOrdering = descriptor.SignalOrdering;
            PreservesOrderedSequence = descriptor.PreservesOrderedSequence;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceDefinitionId SourceDefinitionId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceKind SourceKind { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceProviderId ProviderId { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceEntityCapability EntityCapabilities { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool CompleteBatchPerEvaluation { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxInputItemsPerEvaluation { get; }
        /// <remarks>NoAlloc.</remarks>
        public int MaxActiveRelations { get; }
        /// <remarks>NoAlloc.</remarks>
        public int SignalEnvelopeSchemaVersion { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSignalOrdering SignalOrdering { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool PreservesOrderedSequence { get; }
    }

    /// <summary>Immutable deterministic compiler output. It never stores sink, transfer or provider objects.</summary>
    public sealed class GameplayTriggerCompiledCatalogPlan
    {
        private readonly GameplayTriggerSourceDefinition[] _sources;
        private readonly GameplayTriggerDefinition[] _definitions;
        private readonly GameplayTriggerBinding[] _bindings;
        private readonly GameplayTriggerCompiledActionRequirement[] _actionRequirements;
        private readonly GameplayTriggerCompiledSourceRequirement[] _sourceRequirements;

        internal GameplayTriggerCompiledCatalogPlan(GameplayTriggerSourceDefinition[] sources, GameplayTriggerDefinition[] definitions, GameplayTriggerBinding[] bindings, GameplayTriggerCompiledActionRequirement[] actionRequirements, GameplayTriggerCompiledSourceRequirement[] sourceRequirements, GameplayTriggerRuntimeCapacityShape capacityShape, GameplayTriggerRuntimeCapacityRequirements capacityRequirements, GameplayTriggerCatalog validationCatalog)
        {
            _sources = sources;
            _definitions = definitions;
            _bindings = bindings;
            _actionRequirements = actionRequirements;
            _sourceRequirements = sourceRequirements;
            CapacityShape = capacityShape;
            CapacityRequirements = capacityRequirements;
            DefinitionCatalogHash = validationCatalog.DefinitionCatalogHash;
            BindingCatalogHash = validationCatalog.BindingCatalogHash;
            GameplayTriggerCatalogHash = validationCatalog.GameplayTriggerCatalogHash;
        }

        /// <remarks>NoAlloc.</remarks>
        public string RuntimeCatalogSchemaId => GameplayTriggerConfigSchemas.RuntimeCatalogId;
        /// <remarks>NoAlloc.</remarks>
        public int RuntimeCatalogSchemaVersion => GameplayTriggerConfigSchemas.RuntimeCatalogVersion;
        /// <remarks>NoAlloc.</remarks>
        public int SourceDefinitionCount => _sources.Length;
        /// <remarks>NoAlloc.</remarks>
        public int DefinitionCount => _definitions.Length;
        /// <remarks>NoAlloc.</remarks>
        public int BindingCount => _bindings.Length;
        /// <remarks>NoAlloc.</remarks>
        public int ActionRequirementCount => _actionRequirements.Length;
        /// <remarks>NoAlloc.</remarks>
        public int SourceRequirementCount => _sourceRequirements.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRuntimeCapacityShape CapacityShape { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRuntimeCapacityRequirements CapacityRequirements { get; }
        /// <remarks>NoAlloc.</remarks>
        public long DefinitionCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public long BindingCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public long GameplayTriggerCatalogHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerSourceDefinition GetSourceDefinition(int index) => _sources[index];
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerDefinition GetDefinition(int index) => _definitions[index];
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerBinding GetBinding(int index) => _bindings[index];
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCompiledActionRequirement GetActionRequirement(int index) => _actionRequirements[index];
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCompiledSourceRequirement GetSourceRequirement(int index) => _sourceRequirements[index];
        internal GameplayTriggerSourceDefinition[] CopySources() => (GameplayTriggerSourceDefinition[])_sources.Clone();
        internal GameplayTriggerDefinition[] CopyDefinitions() => (GameplayTriggerDefinition[])_definitions.Clone();
        internal GameplayTriggerBinding[] CopyBindings() => (GameplayTriggerBinding[])_bindings.Clone();
    }

    /// <summary>AllocByDesign compiler result. Failed results never expose a partial plan.</summary>
    public sealed class GameplayTriggerCompileResult
    {
        private readonly GameplayTriggerConfigDiagnostic[] _diagnostics;
        internal GameplayTriggerCompileResult(GameplayTriggerCompiledCatalogPlan compiledCatalog, GameplayTriggerConfigDiagnostic[] diagnostics)
        {
            CompiledCatalog = compiledCatalog;
            _diagnostics = diagnostics ?? Array.Empty<GameplayTriggerConfigDiagnostic>();
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCompiledCatalogPlan CompiledCatalog { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool Succeeded => CompiledCatalog != null && _diagnostics.Length == 0;
        /// <remarks>NoAlloc.</remarks>
        public int DiagnosticCount => _diagnostics.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConfigDiagnostic GetDiagnostic(int index) => _diagnostics[index];
    }
}
