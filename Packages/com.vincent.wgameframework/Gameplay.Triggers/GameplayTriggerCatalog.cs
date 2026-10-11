using System;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public readonly struct GameplayTriggerSourceDefinition
    {
        public GameplayTriggerSourceDefinition(
            GameplayTriggerSourceDefinitionId sourceDefinitionId,
            GameplayTriggerSourceKind sourceKind,
            GameplayTriggerSignalOrdering signalOrdering = GameplayTriggerSignalOrdering.UnorderedMultiset,
            GameplayTriggerThresholdDirection thresholdDirection = GameplayTriggerThresholdDirection.High,
            Fix64 enterThreshold = default,
            Fix64 exitThreshold = default,
            GameplayTriggerInitialSamplePolicy initialSamplePolicy = GameplayTriggerInitialSamplePolicy.PrimeSilently,
            GameplayTriggerSubjectDeparturePolicy departurePolicy = GameplayTriggerSubjectDeparturePolicy.RemoveSilently)
        {
            if (sourceKind == GameplayTriggerSourceKind.Threshold)
            {
                if (thresholdDirection == GameplayTriggerThresholdDirection.High && exitThreshold > enterThreshold)
                    throw new ArgumentException("High threshold exit must be less than or equal to enter threshold.");
                if (thresholdDirection == GameplayTriggerThresholdDirection.Low && exitThreshold < enterThreshold)
                    throw new ArgumentException("Low threshold exit must be greater than or equal to enter threshold.");
            }

            SourceDefinitionId = sourceDefinitionId;
            SourceKind = sourceKind;
            SignalOrdering = signalOrdering;
            ThresholdDirection = thresholdDirection;
            EnterThreshold = enterThreshold;
            ExitThreshold = exitThreshold;
            InitialSamplePolicy = initialSamplePolicy;
            DeparturePolicy = departurePolicy;
        }

        public GameplayTriggerSourceDefinitionId SourceDefinitionId { get; }
        public GameplayTriggerSourceKind SourceKind { get; }
        public GameplayTriggerSignalOrdering SignalOrdering { get; }
        public GameplayTriggerThresholdDirection ThresholdDirection { get; }
        public Fix64 EnterThreshold { get; }
        public Fix64 ExitThreshold { get; }
        public GameplayTriggerInitialSamplePolicy InitialSamplePolicy { get; }
        public GameplayTriggerSubjectDeparturePolicy DeparturePolicy { get; }
    }

    public sealed class GameplayTriggerRouteDefinition
    {
        private readonly GameplayTriggerActionInstruction[] _actions;

        public GameplayTriggerRouteDefinition(
            GameplayTriggerRouteId routeId,
            GameplayTriggerOccurrenceKind occurrenceMask,
            GameplayTriggerConditionProgram conditionProgram,
            GameplayTriggerActionInstruction[] actions,
            GameplayTriggerActivationPolicy activationPolicy = default,
            int executionOrder = 0,
            int exclusiveGroupId = 0,
            int winnerPriority = 0,
            GameplayTriggerArbitrationScope arbitrationScope = GameplayTriggerArbitrationScope.Global)
        {
            if (occurrenceMask == GameplayTriggerOccurrenceKind.None) throw new ArgumentOutOfRangeException(nameof(occurrenceMask));
            if (actions == null || actions.Length == 0) throw new ArgumentException("Route requires at least one action.", nameof(actions));
            if (exclusiveGroupId < 0) throw new ArgumentOutOfRangeException(nameof(exclusiveGroupId));
            RouteId = routeId;
            OccurrenceMask = occurrenceMask;
            ConditionProgram = conditionProgram ?? GameplayTriggerConditionProgram.Always;
            _actions = new GameplayTriggerActionInstruction[actions.Length];
            Array.Copy(actions, _actions, actions.Length);
            ActivationPolicy = activationPolicy;
            ExecutionOrder = executionOrder;
            ExclusiveGroupId = exclusiveGroupId;
            WinnerPriority = winnerPriority;
            ArbitrationScope = arbitrationScope;
        }

        public GameplayTriggerRouteId RouteId { get; }
        public GameplayTriggerOccurrenceKind OccurrenceMask { get; }
        public GameplayTriggerConditionProgram ConditionProgram { get; }
        public GameplayTriggerActivationPolicy ActivationPolicy { get; }
        public int ExecutionOrder { get; }
        public int ExclusiveGroupId { get; }
        public int WinnerPriority { get; }
        public GameplayTriggerArbitrationScope ArbitrationScope { get; }
        public int ActionCount => _actions.Length;
        public GameplayTriggerActionInstruction GetAction(int index) => _actions[index];
    }

    public sealed class GameplayTriggerDefinition
    {
        private readonly GameplayTriggerRouteDefinition[] _routes;

        public GameplayTriggerDefinition(GameplayTriggerDefinitionId definitionId, int definitionVersion, GameplayTriggerSourceDefinitionId sourceDefinitionId, GameplayTriggerRouteDefinition[] routes)
        {
            if (definitionVersion <= 0) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (routes == null || routes.Length == 0) throw new ArgumentException("Trigger definition requires at least one route.", nameof(routes));
            DefinitionId = definitionId;
            DefinitionVersion = definitionVersion;
            SourceDefinitionId = sourceDefinitionId;
            _routes = new GameplayTriggerRouteDefinition[routes.Length];
            Array.Copy(routes, _routes, routes.Length);
            Array.Sort(_routes, CompareRoutes);
            for (int i = 1; i < _routes.Length; i++)
            {
                if (_routes[i - 1].RouteId == _routes[i].RouteId) throw new ArgumentException("Trigger definition contains duplicate route ids.", nameof(routes));
            }
        }

        public GameplayTriggerDefinitionId DefinitionId { get; }
        public int DefinitionVersion { get; }
        public GameplayTriggerSourceDefinitionId SourceDefinitionId { get; }
        public int RouteCount => _routes.Length;
        public GameplayTriggerRouteDefinition GetRoute(int index) => _routes[index];
        private static int CompareRoutes(GameplayTriggerRouteDefinition left, GameplayTriggerRouteDefinition right) => left.RouteId.CompareTo(right.RouteId);
    }

    public readonly struct GameplayTriggerBinding
    {
        private readonly GameplayTriggerBindingTarget[] _explicitTargets;

        public GameplayTriggerBinding(
            GameplayTriggerBindingId bindingId,
            GameplayTriggerDefinitionId definitionId,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayEntityId owner = default,
            bool initiallyEnabled = true,
            GameplayTriggerBindingTarget[] explicitTargets = null)
        {
            BindingId = bindingId;
            DefinitionId = definitionId;
            SourceBindingId = sourceBindingId;
            Owner = owner;
            InitiallyEnabled = initiallyEnabled;
            if (explicitTargets == null || explicitTargets.Length == 0)
            {
                _explicitTargets = Array.Empty<GameplayTriggerBindingTarget>();
            }
            else
            {
                _explicitTargets = new GameplayTriggerBindingTarget[explicitTargets.Length];
                Array.Copy(explicitTargets, _explicitTargets, explicitTargets.Length);
                Array.Sort(_explicitTargets, CompareTargets);
                for (int i = 1; i < _explicitTargets.Length; i++)
                {
                    if (_explicitTargets[i - 1].SlotId == _explicitTargets[i].SlotId)
                        throw new ArgumentException("Trigger binding contains duplicate explicit target slots.", nameof(explicitTargets));
                }
            }
        }

        public GameplayTriggerBindingId BindingId { get; }
        public GameplayTriggerDefinitionId DefinitionId { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public GameplayEntityId Owner { get; }
        public bool InitiallyEnabled { get; }
        public int ExplicitTargetCount => _explicitTargets?.Length ?? 0;
        public GameplayTriggerBindingTarget GetExplicitTarget(int index) => _explicitTargets[index];

        internal bool TryGetExplicitTarget(int slotId, out GameplayTriggerTargetRef target)
        {
            if (_explicitTargets != null)
            {
                for (int i = 0; i < _explicitTargets.Length; i++)
                {
                    if (_explicitTargets[i].SlotId != slotId) continue;
                    target = _explicitTargets[i].Target;
                    return true;
                }
            }
            target = default;
            return false;
        }

        private static int CompareTargets(GameplayTriggerBindingTarget left, GameplayTriggerBindingTarget right) => left.SlotId.CompareTo(right.SlotId);
    }

    public readonly struct GameplayTriggerCatalogValidationResult
    {
        public GameplayTriggerCatalogValidationResult(bool success, GameplayTriggerErrorCode errorCode, int primaryId = 0, int secondaryId = 0)
        {
            Success = success;
            ErrorCode = errorCode;
            PrimaryId = primaryId;
            SecondaryId = secondaryId;
        }

        public bool Success { get; }
        public GameplayTriggerErrorCode ErrorCode { get; }
        public int PrimaryId { get; }
        public int SecondaryId { get; }
        public static GameplayTriggerCatalogValidationResult Succeeded => new GameplayTriggerCatalogValidationResult(true, GameplayTriggerErrorCode.None);
    }

    /// <summary>Immutable catalog registration for one Trigger-owned deferred action kind.</summary>
    public readonly struct GameplayTriggerInternalDeferredActionDescriptor
    {
        public GameplayTriggerInternalDeferredActionDescriptor(
            GameplayTriggerActionKindId actionKindId,
            GameplayTriggerInternalDeferredOperationKind operationKind,
            int signalId = 0)
        {
            if (operationKind != GameplayTriggerInternalDeferredOperationKind.SetEnabled &&
                operationKind != GameplayTriggerInternalDeferredOperationKind.Reset &&
                operationKind != GameplayTriggerInternalDeferredOperationKind.Signal)
                throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (operationKind == GameplayTriggerInternalDeferredOperationKind.Signal)
            {
                if (signalId <= 0) throw new ArgumentOutOfRangeException(nameof(signalId));
            }
            else if (signalId != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(signalId));
            }

            ActionKindId = actionKindId;
            OperationKind = operationKind;
            SignalId = signalId;
        }

        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerInternalDeferredOperationKind OperationKind { get; }
        public int SignalId { get; }
    }

    internal readonly struct GameplayTriggerActionSinkEntry
    {
        public GameplayTriggerActionSinkEntry(IGameplayTriggerActionSink sink)
        {
            Sink = sink;
            ActionKindId = sink == null ? default : sink.ActionKindId;
            DeliveryContract = sink == null ? default : sink.DeliveryContract;
            InternalOperationKind = GameplayTriggerInternalDeferredOperationKind.None;
            SignalId = 0;
        }

        public GameplayTriggerActionSinkEntry(
            GameplayTriggerInternalDeferredActionDescriptor descriptor)
        {
            Sink = null;
            ActionKindId = descriptor.ActionKindId;
            DeliveryContract = GameplayTriggerDeliveryContract.InternalDeferred;
            InternalOperationKind = descriptor.OperationKind;
            SignalId = descriptor.SignalId;
        }

        public IGameplayTriggerActionSink Sink { get; }
        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerDeliveryContract DeliveryContract { get; }
        public GameplayTriggerInternalDeferredOperationKind InternalOperationKind { get; }
        public int SignalId { get; }
    }

    /// <summary>Immutable, startup-built trigger catalog.</summary>
    public sealed class GameplayTriggerCatalog
    {
        /// <summary>Current schema for the three cached Gameplay Trigger catalog hashes.</summary>
        /// <remarks>NoAlloc compile-time constant. Additive public contract.</remarks>
        public const int CurrentCatalogHashSchemaVersion = 2;

        private readonly GameplayTriggerSourceDefinition[] _sources;
        private readonly GameplayTriggerDefinition[] _definitions;
        private readonly GameplayTriggerBinding[] _bindings;
        private readonly GameplayTriggerActionSinkEntry[] _sinkEntries;
        private readonly int _internalDeferredActionCount;
        private readonly int _reliableDeferredActionCount;

        private GameplayTriggerCatalog(
            GameplayTriggerSourceDefinition[] sources,
            GameplayTriggerDefinition[] definitions,
            GameplayTriggerBinding[] bindings,
            GameplayTriggerActionSinkEntry[] sinkEntries)
        {
            _sources = sources;
            _definitions = definitions;
            _bindings = bindings;
            _sinkEntries = sinkEntries;
            for (int i = 0; i < sinkEntries.Length; i++)
            {
                if (sinkEntries[i].DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                    _internalDeferredActionCount++;
                else if (sinkEntries[i].DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred)
                    _reliableDeferredActionCount++;
            }
            DefinitionCatalogHash = ComputeDefinitionCatalogHash(
                sources,
                definitions,
                sinkEntries,
                CurrentCatalogHashSchemaVersion,
                true);
            BindingCatalogHash = ComputeBindingCatalogHash(
                bindings,
                CurrentCatalogHashSchemaVersion);
            GameplayTriggerCatalogHash = ComputeCombinedCatalogHash(
                DefinitionCatalogHash,
                BindingCatalogHash,
                CurrentCatalogHashSchemaVersion);

            if (_internalDeferredActionCount == 0)
            {
                HasLegacyV1CatalogIdentity = true;
                LegacyV1DefinitionCatalogHash = ComputeDefinitionCatalogHash(
                    sources,
                    definitions,
                    sinkEntries,
                    1,
                    false);
                LegacyV1BindingCatalogHash = ComputeBindingCatalogHash(bindings, 1);
                LegacyV1GameplayTriggerCatalogHash = ComputeCombinedCatalogHash(
                    LegacyV1DefinitionCatalogHash,
                    LegacyV1BindingCatalogHash,
                    1);
            }
        }

        public int SourceDefinitionCount => _sources.Length;
        public int DefinitionCount => _definitions.Length;
        public int BindingCount => _bindings.Length;
        public int ActionSinkCount => _sinkEntries.Length;
        internal int InternalDeferredActionCount => _internalDeferredActionCount;
        internal int ReliableDeferredActionCount => _reliableDeferredActionCount;
        internal bool HasLegacyV1CatalogIdentity { get; }
        internal long LegacyV1DefinitionCatalogHash { get; }
        internal long LegacyV1BindingCatalogHash { get; }
        internal long LegacyV1GameplayTriggerCatalogHash { get; }

        /// <summary>Gets the schema-versioned hash of source, definition, route, condition, action, and sink contracts.</summary>
        /// <remarks>NoAlloc. Additive public contract; computed and cached during successful catalog creation.</remarks>
        public long DefinitionCatalogHash { get; }

        /// <summary>Gets the schema-versioned hash of canonical binding and explicit-target contracts.</summary>
        /// <remarks>NoAlloc. Additive public contract; computed and cached during successful catalog creation.</remarks>
        public long BindingCatalogHash { get; }

        /// <summary>Gets the schema-versioned composition of the definition and binding catalog hashes.</summary>
        /// <remarks>NoAlloc. Additive public contract; computed and cached during successful catalog creation.</remarks>
        public long GameplayTriggerCatalogHash { get; }

        /// <remarks>AllocByDesign on the low-frequency startup path. Copies and sorts inputs, validates contracts, and computes cached catalog hashes.</remarks>
        public static bool TryCreate(
            GameplayTriggerSourceDefinition[] sources,
            GameplayTriggerDefinition[] definitions,
            GameplayTriggerBinding[] bindings,
            IGameplayTriggerActionSink[] sinks,
            out GameplayTriggerCatalog catalog,
            out GameplayTriggerCatalogValidationResult result)
        {
            return TryCreate(
                sources,
                definitions,
                bindings,
                sinks,
                Array.Empty<GameplayTriggerInternalDeferredActionDescriptor>(),
                out catalog,
                out result);
        }

        /// <summary>Creates a catalog with external sinks and immutable Trigger-owned action descriptors.</summary>
        /// <remarks>AllocByDesign on the low-frequency startup path.</remarks>
        public static bool TryCreate(
            GameplayTriggerSourceDefinition[] sources,
            GameplayTriggerDefinition[] definitions,
            GameplayTriggerBinding[] bindings,
            IGameplayTriggerActionSink[] sinks,
            GameplayTriggerInternalDeferredActionDescriptor[] internalDeferredActions,
            out GameplayTriggerCatalog catalog,
            out GameplayTriggerCatalogValidationResult result)
        {
            catalog = null;
            if (sources == null || sources.Length == 0 ||
                definitions == null || definitions.Length == 0 ||
                bindings == null || bindings.Length == 0 ||
                sinks == null || internalDeferredActions == null ||
                sinks.Length + internalDeferredActions.Length == 0)
            {
                result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog);
                return false;
            }

            var sourceCopy = new GameplayTriggerSourceDefinition[sources.Length];
            var definitionCopy = new GameplayTriggerDefinition[definitions.Length];
            var bindingCopy = new GameplayTriggerBinding[bindings.Length];
            var sinkCopy = new GameplayTriggerActionSinkEntry[
                sinks.Length + internalDeferredActions.Length];
            Array.Copy(sources, sourceCopy, sources.Length);
            Array.Copy(definitions, definitionCopy, definitions.Length);
            Array.Copy(bindings, bindingCopy, bindings.Length);
            for (int i = 0; i < sinks.Length; i++)
                sinkCopy[i] = new GameplayTriggerActionSinkEntry(sinks[i]);
            for (int i = 0; i < internalDeferredActions.Length; i++)
                sinkCopy[sinks.Length + i] = new GameplayTriggerActionSinkEntry(
                    internalDeferredActions[i]);
            Array.Sort(sourceCopy, CompareSources);
            Array.Sort(definitionCopy, CompareDefinitions);
            Array.Sort(bindingCopy, CompareBindings);
            Array.Sort(sinkCopy, CompareSinks);

            for (int i = 0; i < sourceCopy.Length; i++)
            {
                if (i > 0 && sourceCopy[i - 1].SourceDefinitionId == sourceCopy[i].SourceDefinitionId)
                {
                    result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, sourceCopy[i].SourceDefinitionId.Value);
                    return false;
                }
            }

            for (int i = 0; i < sinkCopy.Length; i++)
            {
                GameplayTriggerActionSinkEntry sinkEntry = sinkCopy[i];
                bool internalDeferred = sinkEntry.DeliveryContract ==
                    GameplayTriggerDeliveryContract.InternalDeferred;
                bool validExternal = sinkEntry.Sink != null &&
                    !internalDeferred &&
                    sinkEntry.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.None &&
                    sinkEntry.SignalId == 0;
                bool validInternal = sinkEntry.Sink == null &&
                    internalDeferred &&
                    (sinkEntry.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.SetEnabled ||
                     sinkEntry.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Reset ||
                     sinkEntry.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Signal) &&
                    (sinkEntry.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Signal
                        ? sinkEntry.SignalId > 0
                        : sinkEntry.SignalId == 0);
                if ((!validExternal && !validInternal) ||
                    !sinkEntry.ActionKindId.IsValid ||
                    (i > 0 && sinkCopy[i - 1].ActionKindId == sinkEntry.ActionKindId))
                {
                    result = new GameplayTriggerCatalogValidationResult(
                        false,
                        GameplayTriggerErrorCode.UnknownActionKind,
                        sinkEntry.ActionKindId.Value);
                    return false;
                }
            }

            for (int i = 0; i < definitionCopy.Length; i++)
            {
                GameplayTriggerDefinition definition = definitionCopy[i];
                if (definition == null || (i > 0 && definitionCopy[i - 1].DefinitionId == definition.DefinitionId) || FindSource(sourceCopy, definition.SourceDefinitionId) < 0)
                {
                    result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, definition?.DefinitionId.Value ?? 0);
                    return false;
                }

                GameplayTriggerSourceDefinition source = sourceCopy[FindSource(sourceCopy, definition.SourceDefinitionId)];
                for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(routeIndex);
                    if (route.ConditionProgram.MaxStackDepth <= 0)
                    {
                        result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, definition.DefinitionId.Value, route.RouteId.Value);
                        return false;
                    }
                    if (route.ActivationPolicy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence && source.SourceKind == GameplayTriggerSourceKind.Signal)
                    {
                        result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, definition.DefinitionId.Value, route.RouteId.Value);
                        return false;
                    }
                    if (!HasConsistentExclusiveScope(definitionCopy, definition, route))
                    {
                        result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, route.ExclusiveGroupId, (int)route.ArbitrationScope);
                        return false;
                    }
                    for (int actionIndex = 0; actionIndex < route.ActionCount; actionIndex++)
                    {
                        GameplayTriggerActionInstruction action = route.GetAction(actionIndex);
                        int sinkIndex = FindSink(sinkCopy, action.ActionKindId);
                        if (sinkIndex < 0)
                        {
                            result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.UnknownActionKind, action.ActionKindId.Value);
                            return false;
                        }
                        GameplayTriggerActionSinkEntry sinkEntry = sinkCopy[sinkIndex];
                        if (sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred &&
                            !HasValidInternalDeferredPayload(sinkEntry, action))
                        {
                            result = new GameplayTriggerCatalogValidationResult(
                                false,
                                GameplayTriggerErrorCode.InvalidCatalog,
                                definition.DefinitionId.Value,
                                route.RouteId.Value);
                            return false;
                        }
                    }
                }
            }

            for (int i = 0; i < bindingCopy.Length; i++)
            {
                GameplayTriggerBinding binding = bindingCopy[i];
                if (i > 0 && bindingCopy[i - 1].BindingId == binding.BindingId)
                {
                    result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, binding.BindingId.Value);
                    return false;
                }
                for (int other = 0; other < i; other++)
                {
                    if (bindingCopy[other].SourceBindingId != binding.SourceBindingId) continue;
                    result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, binding.SourceBindingId.Value);
                    return false;
                }
                if (FindDefinition(definitionCopy, binding.DefinitionId) < 0)
                {
                    result = new GameplayTriggerCatalogValidationResult(false, GameplayTriggerErrorCode.InvalidCatalog, binding.DefinitionId.Value);
                    return false;
                }
                GameplayTriggerDefinition bindingDefinition = definitionCopy[FindDefinition(definitionCopy, binding.DefinitionId)];
                for (int routeIndex = 0; routeIndex < bindingDefinition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = bindingDefinition.GetRoute(routeIndex);
                    for (int actionIndex = 0; actionIndex < route.ActionCount; actionIndex++)
                    {
                        GameplayTriggerActionInstruction action = route.GetAction(actionIndex);
                        bool validTarget = action.TargetSelector != GameplayTriggerTargetSelector.Owner || binding.Owner.IsValid;
                        if (action.TargetSelector == GameplayTriggerTargetSelector.ExplicitBindingTarget)
                            validTarget = binding.TryGetExplicitTarget(action.TargetSlotId, out _);
                        int sinkIndex = FindSink(sinkCopy, action.ActionKindId);
                        if (validTarget && sinkIndex >= 0 &&
                            sinkCopy[sinkIndex].DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                        {
                            validTarget = action.TargetSelector == GameplayTriggerTargetSelector.ExplicitBindingTarget &&
                                binding.TryGetExplicitTarget(action.TargetSlotId, out GameplayTriggerTargetRef target) &&
                                target.DomainId == GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId &&
                                target.Generation == 0 &&
                                target.Value <= int.MaxValue &&
                                FindBindingBySourceBinding(
                                    bindingCopy,
                                    new GameplayTriggerSourceBindingId((int)target.Value)) >= 0;
                        }
                        if (validTarget) continue;
                        result = new GameplayTriggerCatalogValidationResult(
                            false,
                            GameplayTriggerErrorCode.MissingActionTarget,
                            binding.BindingId.Value,
                            action.TargetSlotId);
                        return false;
                    }
                }
            }

            catalog = new GameplayTriggerCatalog(sourceCopy, definitionCopy, bindingCopy, sinkCopy);
            result = GameplayTriggerCatalogValidationResult.Succeeded;
            return true;
        }

        internal GameplayTriggerSourceDefinition GetSource(int index) => _sources[index];
        internal GameplayTriggerDefinition GetDefinition(int index) => _definitions[index];
        internal GameplayTriggerBinding GetBinding(int index) => _bindings[index];
        internal IGameplayTriggerActionSink GetSink(int index) => _sinkEntries[index].Sink;
        internal GameplayTriggerActionSinkEntry GetSinkEntry(int index) => _sinkEntries[index];
        internal int FindSource(GameplayTriggerSourceDefinitionId id) => FindSource(_sources, id);
        internal int FindDefinition(GameplayTriggerDefinitionId id) => FindDefinition(_definitions, id);
        internal int FindSink(GameplayTriggerActionKindId id) => FindSink(_sinkEntries, id);
        internal int MaxConditionStackDepth
        {
            get
            {
                int maximum = 0;
                for (int definitionIndex = 0; definitionIndex < _definitions.Length; definitionIndex++)
                {
                    GameplayTriggerDefinition definition = _definitions[definitionIndex];
                    for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                    {
                        int depth = definition.GetRoute(routeIndex).ConditionProgram.MaxStackDepth;
                        if (depth > maximum) maximum = depth;
                    }
                }
                return maximum;
            }
        }

        private static int FindSource(GameplayTriggerSourceDefinition[] sources, GameplayTriggerSourceDefinitionId id)
        {
            for (int i = 0; i < sources.Length; i++) if (sources[i].SourceDefinitionId == id) return i;
            return -1;
        }

        private static int FindDefinition(GameplayTriggerDefinition[] definitions, GameplayTriggerDefinitionId id)
        {
            for (int i = 0; i < definitions.Length; i++) if (definitions[i].DefinitionId == id) return i;
            return -1;
        }

        private static int FindSink(
            GameplayTriggerActionSinkEntry[] sinkEntries,
            GameplayTriggerActionKindId id)
        {
            for (int i = 0; i < sinkEntries.Length; i++)
                if (sinkEntries[i].ActionKindId == id) return i;
            return -1;
        }

        private static int FindBindingBySourceBinding(
            GameplayTriggerBinding[] bindings,
            GameplayTriggerSourceBindingId sourceBindingId)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].SourceBindingId == sourceBindingId) return i;
            }
            return -1;
        }

        private static bool HasValidInternalDeferredPayload(
            GameplayTriggerActionSinkEntry sinkEntry,
            GameplayTriggerActionInstruction action)
        {
            switch (sinkEntry.InternalOperationKind)
            {
                case GameplayTriggerInternalDeferredOperationKind.SetEnabled:
                    return (action.Payload0 == 0L || action.Payload0 == 1L) &&
                        action.Payload1 == 0L && action.Payload2 == 0L;
                case GameplayTriggerInternalDeferredOperationKind.Reset:
                    return action.Payload0 == 0L && action.Payload1 == 0L &&
                        action.Payload2 == 0L;
                case GameplayTriggerInternalDeferredOperationKind.Signal:
                    return sinkEntry.SignalId > 0;
                default:
                    return false;
            }
        }

        private static bool HasConsistentExclusiveScope(
            GameplayTriggerDefinition[] definitions,
            GameplayTriggerDefinition currentDefinition,
            GameplayTriggerRouteDefinition currentRoute)
        {
            if (currentRoute.ExclusiveGroupId == 0) return true;
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
            {
                GameplayTriggerDefinition definition = definitions[definitionIndex];
                if (definition == null) continue;
                for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(routeIndex);
                    if (ReferenceEquals(definition, currentDefinition) && ReferenceEquals(route, currentRoute)) continue;
                    if (route.ExclusiveGroupId == currentRoute.ExclusiveGroupId &&
                        route.ArbitrationScope != currentRoute.ArbitrationScope)
                        return false;
                }
            }
            return true;
        }

        private static int CompareSources(GameplayTriggerSourceDefinition left, GameplayTriggerSourceDefinition right) => left.SourceDefinitionId.CompareTo(right.SourceDefinitionId);
        private static int CompareDefinitions(GameplayTriggerDefinition left, GameplayTriggerDefinition right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            return left.DefinitionId.CompareTo(right.DefinitionId);
        }
        private static int CompareBindings(GameplayTriggerBinding left, GameplayTriggerBinding right) => left.BindingId.CompareTo(right.BindingId);
        private static int CompareSinks(
            GameplayTriggerActionSinkEntry left,
            GameplayTriggerActionSinkEntry right) =>
            left.ActionKindId.CompareTo(right.ActionKindId);

        private static long ComputeDefinitionCatalogHash(
            GameplayTriggerSourceDefinition[] sources,
            GameplayTriggerDefinition[] definitions,
            GameplayTriggerActionSinkEntry[] sinkEntries,
            int schemaVersion,
            bool includeInternalDescriptors)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, 1);
            accumulator.AddInt(DefinitionCatalogHashFields.Schema, schemaVersion);

            accumulator.BeginArray(DefinitionCatalogHashFields.Sources, sources.Length);
            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                GameplayTriggerSourceDefinition source = sources[sourceIndex];
                accumulator.BeginElement(sourceIndex);
                accumulator.AddInt(SourceCatalogHashFields.SourceDefinitionId, source.SourceDefinitionId.Value);
                accumulator.AddInt(SourceCatalogHashFields.SourceKind, (int)source.SourceKind);
                accumulator.AddInt(SourceCatalogHashFields.SignalOrdering, (int)source.SignalOrdering);
                accumulator.AddInt(SourceCatalogHashFields.ThresholdDirection, (int)source.ThresholdDirection);
                accumulator.AddLong(SourceCatalogHashFields.EnterThresholdRaw, source.EnterThreshold.RawValue);
                accumulator.AddLong(SourceCatalogHashFields.ExitThresholdRaw, source.ExitThreshold.RawValue);
                accumulator.AddInt(SourceCatalogHashFields.InitialSamplePolicy, (int)source.InitialSamplePolicy);
                accumulator.AddInt(SourceCatalogHashFields.DeparturePolicy, (int)source.DeparturePolicy);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            accumulator.BeginArray(DefinitionCatalogHashFields.Definitions, definitions.Length);
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
            {
                GameplayTriggerDefinition definition = definitions[definitionIndex];
                accumulator.BeginElement(definitionIndex);
                accumulator.AddInt(DefinitionCatalogEntryHashFields.DefinitionId, definition.DefinitionId.Value);
                accumulator.AddInt(DefinitionCatalogEntryHashFields.DefinitionVersion, definition.DefinitionVersion);
                accumulator.AddInt(DefinitionCatalogEntryHashFields.SourceDefinitionId, definition.SourceDefinitionId.Value);
                accumulator.BeginArray(DefinitionCatalogEntryHashFields.Routes, definition.RouteCount);
                for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(routeIndex);
                    accumulator.BeginElement(routeIndex);
                    accumulator.AddInt(RouteCatalogHashFields.RouteId, route.RouteId.Value);
                    accumulator.AddInt(RouteCatalogHashFields.OccurrenceMask, (int)route.OccurrenceMask);
                    accumulator.BeginArray(RouteCatalogHashFields.Conditions, route.ConditionProgram.InstructionCount);
                    for (int instructionIndex = 0;
                         instructionIndex < route.ConditionProgram.InstructionCount;
                         instructionIndex++)
                    {
                        GameplayTriggerConditionInstruction instruction =
                            route.ConditionProgram.GetInstruction(instructionIndex);
                        accumulator.BeginElement(instructionIndex);
                        accumulator.AddInt(ConditionCatalogHashFields.Operation, (int)instruction.Operation);
                        accumulator.AddLong(ConditionCatalogHashFields.Operand, instruction.Operand);
                        accumulator.AddInt(ConditionCatalogHashFields.Comparison, (int)instruction.Comparison);
                        accumulator.EndElement();
                    }
                    accumulator.EndArray();

                    GameplayTriggerActivationPolicy policy = route.ActivationPolicy;
                    accumulator.AddInt(RouteCatalogHashFields.StateScope, (int)policy.StateScope);
                    accumulator.AddInt(RouteCatalogHashFields.MaxActivations, policy.MaxActivations);
                    accumulator.AddLong(RouteCatalogHashFields.CooldownFrames, policy.CooldownFrames);
                    accumulator.AddInt(RouteCatalogHashFields.PresenceLimit, (int)policy.PresenceLimit);
                    accumulator.AddLong(RouteCatalogHashFields.StayIntervalFrames, policy.StayIntervalFrames);
                    accumulator.AddInt(RouteCatalogHashFields.ExecutionOrder, route.ExecutionOrder);
                    accumulator.AddInt(RouteCatalogHashFields.ExclusiveGroupId, route.ExclusiveGroupId);
                    accumulator.AddInt(RouteCatalogHashFields.WinnerPriority, route.WinnerPriority);
                    accumulator.AddInt(RouteCatalogHashFields.ArbitrationScope, (int)route.ArbitrationScope);
                    accumulator.BeginArray(RouteCatalogHashFields.Actions, route.ActionCount);
                    for (int actionIndex = 0; actionIndex < route.ActionCount; actionIndex++)
                    {
                        GameplayTriggerActionInstruction action = route.GetAction(actionIndex);
                        accumulator.BeginElement(actionIndex);
                        accumulator.AddInt(ActionCatalogHashFields.ActionKindId, action.ActionKindId.Value);
                        accumulator.AddInt(ActionCatalogHashFields.TargetSelector, (int)action.TargetSelector);
                        accumulator.AddInt(ActionCatalogHashFields.TargetSlotId, action.TargetSlotId);
                        accumulator.AddLong(ActionCatalogHashFields.Payload0, action.Payload0);
                        accumulator.AddLong(ActionCatalogHashFields.Payload1, action.Payload1);
                        accumulator.AddLong(ActionCatalogHashFields.Payload2, action.Payload2);
                        accumulator.EndElement();
                    }
                    accumulator.EndArray();
                    accumulator.EndElement();
                }
                accumulator.EndArray();
                accumulator.EndElement();
            }
            accumulator.EndArray();

            int internalDescriptorCount = 0;
            for (int sinkIndex = 0; sinkIndex < sinkEntries.Length; sinkIndex++)
            {
                if (sinkEntries[sinkIndex].DeliveryContract ==
                    GameplayTriggerDeliveryContract.InternalDeferred)
                    internalDescriptorCount++;
            }
            if (includeInternalDescriptors)
                accumulator.AddInt(DefinitionCatalogHashFields.InternalDescriptorCount, internalDescriptorCount);

            accumulator.BeginArray(DefinitionCatalogHashFields.Sinks, sinkEntries.Length);
            for (int sinkIndex = 0; sinkIndex < sinkEntries.Length; sinkIndex++)
            {
                GameplayTriggerActionSinkEntry sinkEntry = sinkEntries[sinkIndex];
                accumulator.BeginElement(sinkIndex);
                accumulator.AddInt(SinkCatalogHashFields.ActionKindId, sinkEntry.ActionKindId.Value);
                accumulator.AddInt(SinkCatalogHashFields.DeliveryContract, (int)sinkEntry.DeliveryContract);
                if (includeInternalDescriptors &&
                    sinkEntry.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                {
                    accumulator.AddInt(SinkCatalogHashFields.InternalOperation, (int)sinkEntry.InternalOperationKind);
                    accumulator.AddInt(SinkCatalogHashFields.InternalSignalId, sinkEntry.SignalId);
                }
                accumulator.EndElement();
            }
            accumulator.EndArray();
            return accumulator.ToHash();
        }

        private static class DefinitionCatalogHashFields
        {
            public const int Schema = 1;
            public const int Sources = 2;
            public const int Definitions = 3;
            public const int InternalDescriptorCount = 4;
            public const int Sinks = 5;
        }

        private static class SourceCatalogHashFields
        {
            public const int SourceDefinitionId = 1;
            public const int SourceKind = 2;
            public const int SignalOrdering = 3;
            public const int ThresholdDirection = 4;
            public const int EnterThresholdRaw = 5;
            public const int ExitThresholdRaw = 6;
            public const int InitialSamplePolicy = 7;
            public const int DeparturePolicy = 8;
        }

        private static class DefinitionCatalogEntryHashFields
        {
            public const int DefinitionId = 1;
            public const int DefinitionVersion = 2;
            public const int SourceDefinitionId = 3;
            public const int Routes = 4;
        }

        private static class RouteCatalogHashFields
        {
            public const int RouteId = 1;
            public const int OccurrenceMask = 2;
            public const int Conditions = 3;
            public const int StateScope = 4;
            public const int MaxActivations = 5;
            public const int CooldownFrames = 6;
            public const int PresenceLimit = 7;
            public const int StayIntervalFrames = 8;
            public const int ExecutionOrder = 9;
            public const int ExclusiveGroupId = 10;
            public const int WinnerPriority = 11;
            public const int ArbitrationScope = 12;
            public const int Actions = 13;
        }

        private static class ConditionCatalogHashFields
        {
            public const int Operation = 1;
            public const int Operand = 2;
            public const int Comparison = 3;
        }

        private static class ActionCatalogHashFields
        {
            public const int ActionKindId = 1;
            public const int TargetSelector = 2;
            public const int TargetSlotId = 3;
            public const int Payload0 = 4;
            public const int Payload1 = 5;
            public const int Payload2 = 6;
        }

        private static class SinkCatalogHashFields
        {
            public const int ActionKindId = 1;
            public const int DeliveryContract = 2;
            public const int InternalOperation = 3;
            public const int InternalSignalId = 4;
        }

        private static long ComputeBindingCatalogHash(
            GameplayTriggerBinding[] bindings,
            int schemaVersion)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, 1);
            accumulator.AddInt(BindingCatalogHashFields.Schema, schemaVersion);
            accumulator.BeginArray(BindingCatalogHashFields.Bindings, bindings.Length);
            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                GameplayTriggerBinding binding = bindings[bindingIndex];
                accumulator.BeginElement(bindingIndex);
                accumulator.AddInt(BindingCatalogEntryHashFields.BindingId, binding.BindingId.Value);
                accumulator.AddInt(BindingCatalogEntryHashFields.DefinitionId, binding.DefinitionId.Value);
                accumulator.AddInt(BindingCatalogEntryHashFields.SourceBindingId, binding.SourceBindingId.Value);
                accumulator.AddInt(BindingCatalogEntryHashFields.OwnerIndex, binding.Owner.Index);
                accumulator.AddInt(BindingCatalogEntryHashFields.OwnerGeneration, binding.Owner.Generation);
                accumulator.AddInt(BindingCatalogEntryHashFields.InitiallyEnabled, binding.InitiallyEnabled ? 1 : 0);
                accumulator.BeginArray(BindingCatalogEntryHashFields.ExplicitTargets, binding.ExplicitTargetCount);
                for (int targetIndex = 0; targetIndex < binding.ExplicitTargetCount; targetIndex++)
                {
                    GameplayTriggerBindingTarget target = binding.GetExplicitTarget(targetIndex);
                    accumulator.BeginElement(targetIndex);
                    accumulator.AddInt(BindingTargetHashFields.SlotId, target.SlotId);
                    accumulator.AddInt(BindingTargetHashFields.DomainId, target.Target.DomainId);
                    accumulator.AddLong(BindingTargetHashFields.Value, target.Target.Value);
                    accumulator.AddInt(BindingTargetHashFields.Generation, target.Target.Generation);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
                accumulator.EndElement();
            }
            accumulator.EndArray();
            return accumulator.ToHash();
        }

        private static class BindingCatalogHashFields
        {
            public const int Schema = 1;
            public const int Bindings = 2;
        }

        private static class BindingCatalogEntryHashFields
        {
            public const int BindingId = 1;
            public const int DefinitionId = 2;
            public const int SourceBindingId = 3;
            public const int OwnerIndex = 4;
            public const int OwnerGeneration = 5;
            public const int InitiallyEnabled = 6;
            public const int ExplicitTargets = 7;
        }

        private static class BindingTargetHashFields
        {
            public const int SlotId = 1;
            public const int DomainId = 2;
            public const int Value = 3;
            public const int Generation = 4;
        }

        private static long ComputeCombinedCatalogHash(
            long definitionCatalogHash,
            long bindingCatalogHash,
            int schemaVersion)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, 1);
            accumulator.AddInt(CombinedCatalogHashFields.Schema, schemaVersion);
            accumulator.AddLong(CombinedCatalogHashFields.DefinitionHash, definitionCatalogHash);
            accumulator.AddLong(CombinedCatalogHashFields.BindingHash, bindingCatalogHash);
            return accumulator.ToHash();
        }

        private static class CombinedCatalogHashFields
        {
            public const int Schema = 1;
            public const int DefinitionHash = 2;
            public const int BindingHash = 3;
        }
    }
}
