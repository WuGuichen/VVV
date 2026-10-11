using System;
using System.Collections.Generic;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>Actual action binding used only during startup catalog creation.</summary>
    public readonly struct GameplayTriggerActionBootstrapBinding
    {
        private GameplayTriggerActionBootstrapBinding(GameplayTriggerActionKindId actionKindId, IGameplayTriggerActionSink sink, GameplayTriggerInternalDeferredActionDescriptor internalDescriptor, bool isInternal)
        {
            ActionKindId = actionKindId;
            Sink = sink;
            InternalDescriptor = internalDescriptor;
            IsInternal = isInternal;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionKindId ActionKindId { get; }
        /// <remarks>NoAlloc.</remarks>
        public IGameplayTriggerActionSink Sink { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerInternalDeferredActionDescriptor InternalDescriptor { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool IsInternal { get; }
        /// <remarks>NoAlloc.</remarks>
        public static GameplayTriggerActionBootstrapBinding FromSink(IGameplayTriggerActionSink sink) => new GameplayTriggerActionBootstrapBinding(sink == null ? default : sink.ActionKindId, sink, default, false);
        /// <remarks>NoAlloc.</remarks>
        public static GameplayTriggerActionBootstrapBinding FromInternal(GameplayTriggerInternalDeferredActionDescriptor descriptor) => new GameplayTriggerActionBootstrapBinding(descriptor.ActionKindId, null, descriptor, true);
    }

    /// <summary>Immutable source/reliable requirements that remain for the actual composition gate.</summary>
    public sealed class GameplayTriggerCompositionRequirements
    {
        private readonly GameplayTriggerCompiledSourceRequirement[] _sources;
        private readonly GameplayTriggerActionKindId[] _reliableActions;
        internal GameplayTriggerCompositionRequirements(GameplayTriggerCompiledCatalogPlan plan)
        {
            _sources = new GameplayTriggerCompiledSourceRequirement[plan.SourceRequirementCount];
            for (int i = 0; i < _sources.Length; i++) _sources[i] = plan.GetSourceRequirement(i);
            var reliable = new List<GameplayTriggerActionKindId>();
            for (int i = 0; i < plan.ActionRequirementCount; i++)
            {
                GameplayTriggerCompiledActionRequirement requirement = plan.GetActionRequirement(i);
                if (requirement.DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred) reliable.Add(requirement.ActionKindId);
            }
            _reliableActions = reliable.ToArray();
        }

        /// <remarks>NoAlloc.</remarks>
        public int SourceRequirementCount => _sources.Length;
        /// <remarks>NoAlloc.</remarks>
        public int ReliableActionCount => _reliableActions.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCompiledSourceRequirement GetSourceRequirement(int index) => _sources[index];
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerActionKindId GetReliableActionKindId(int index) => _reliableActions[index];
    }

    /// <summary>AllocByDesign catalog bootstrap result.</summary>
    public sealed class GameplayTriggerCatalogBootstrapResult
    {
        private readonly GameplayTriggerConfigDiagnostic[] _diagnostics;
        internal GameplayTriggerCatalogBootstrapResult(GameplayTriggerCatalog catalog, GameplayTriggerRuntimeOptions? runtimeOptions, GameplayTriggerCompositionRequirements compositionRequirements, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            Catalog = catalog;
            RuntimeOptions = runtimeOptions;
            CompositionRequirements = compositionRequirements;
            _diagnostics = GameplayTriggerConfigDiagnosticOrdering.Freeze(diagnostics);
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCatalog Catalog { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerRuntimeOptions? RuntimeOptions { get; }
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerCompositionRequirements CompositionRequirements { get; }
        /// <remarks>NoAlloc.</remarks>
        public bool Succeeded => Catalog != null && RuntimeOptions.HasValue && _diagnostics.Length == 0;
        /// <remarks>NoAlloc.</remarks>
        public int DiagnosticCount => _diagnostics.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConfigDiagnostic GetDiagnostic(int index) => _diagnostics[index];
    }

    /// <summary>LowFreqAlloc startup bootstrap for an immutable compiled plan.</summary>
    public static class GameplayTriggerCatalogBootstrap
    {
        /// <remarks>LowFreqAlloc. Copies and validates actual bootstrap bindings before creating the runtime catalog.</remarks>
        public static GameplayTriggerCatalogBootstrapResult TryCreateCatalog(GameplayTriggerCompiledCatalogPlan plan, GameplayTriggerActionBootstrapBinding[] actualActions, GameplayTriggerRuntimeCapacityProfile runtimeProfile)
        {
            var diagnostics = new List<GameplayTriggerConfigDiagnostic>();
            if (plan == null)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RegistryInvalid, "$.compiledPlan"));
                return new GameplayTriggerCatalogBootstrapResult(null, null, null, diagnostics);
            }
            ValidateProfile(plan.CapacityRequirements, runtimeProfile, diagnostics);
            GameplayTriggerActionBootstrapBinding[] actions = actualActions == null ? Array.Empty<GameplayTriggerActionBootstrapBinding>() : (GameplayTriggerActionBootstrapBinding[])actualActions.Clone();
            Array.Sort(actions, (a, b) => a.ActionKindId.CompareTo(b.ActionKindId));
            for (int i = 1; i < actions.Length; i++) if (actions[i - 1].ActionKindId == actions[i].ActionKindId) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapActionDuplicate, "$.bootstrap.actions[kind=" + actions[i].ActionKindId.Value + "]", actions[i].ActionKindId.Value));
            for (int i = 0; i < plan.ActionRequirementCount; i++)
            {
                GameplayTriggerCompiledActionRequirement requirement = plan.GetActionRequirement(i);
                int index = Find(actions, requirement.ActionKindId);
                string path = "$.bootstrap.actions[kind=" + requirement.ActionKindId.Value + "]";
                if (index < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapActionMissing, path, requirement.ActionKindId.Value)); continue; }
                GameplayTriggerActionBootstrapBinding actual = actions[index];
                if (requirement.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                {
                    if (!actual.IsInternal || actual.Sink != null || actual.InternalDescriptor.ActionKindId != requirement.ActionKindId || actual.InternalDescriptor.OperationKind != requirement.InternalOperationKind || actual.InternalDescriptor.SignalId != requirement.SignalId)
                        diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InternalDescriptorMismatch, path + ".internalDescriptor", requirement.ActionKindId.Value));
                }
                else if (actual.IsInternal || actual.Sink == null || actual.Sink.ActionKindId != requirement.ActionKindId || actual.Sink.DeliveryContract != requirement.DeliveryContract)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapDeliveryMismatch, path, requirement.ActionKindId.Value, actual.Sink == null ? -1 : (int)actual.Sink.DeliveryContract, (int)requirement.DeliveryContract));
            }
            for (int i = 0; i < actions.Length; i++) if (!Contains(plan, actions[i].ActionKindId)) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch, "$.bootstrap.actions[kind=" + actions[i].ActionKindId.Value + "]", actions[i].ActionKindId.Value));
            if (diagnostics.Count > 0) return new GameplayTriggerCatalogBootstrapResult(null, null, null, diagnostics);

            var sinks = new List<IGameplayTriggerActionSink>();
            var internalDescriptors = new List<GameplayTriggerInternalDeferredActionDescriptor>();
            for (int i = 0; i < actions.Length; i++) if (actions[i].IsInternal) internalDescriptors.Add(actions[i].InternalDescriptor); else sinks.Add(actions[i].Sink);
            if (!GameplayTriggerCatalog.TryCreate(plan.CopySources(), plan.CopyDefinitions(), plan.CopyBindings(), sinks.ToArray(), internalDescriptors.ToArray(), out GameplayTriggerCatalog catalog, out GameplayTriggerCatalogValidationResult validation))
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected, "$.runtimeCatalog", validation.PrimaryId, validation.SecondaryId, (int)validation.ErrorCode));
                return new GameplayTriggerCatalogBootstrapResult(null, null, null, diagnostics);
            }
            if (catalog.DefinitionCatalogHash != plan.DefinitionCatalogHash || catalog.BindingCatalogHash != plan.BindingCatalogHash || catalog.GameplayTriggerCatalogHash != plan.GameplayTriggerCatalogHash)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected, "$.runtimeCatalog.hash", 0, 0, plan.GameplayTriggerCatalogHash, catalog.GameplayTriggerCatalogHash));
                return new GameplayTriggerCatalogBootstrapResult(null, null, null, diagnostics);
            }
            GameplayTriggerRuntimeOptions options = runtimeProfile.ToRuntimeOptions();
            return new GameplayTriggerCatalogBootstrapResult(catalog, options, new GameplayTriggerCompositionRequirements(plan), diagnostics);
        }

        private static void ValidateProfile(GameplayTriggerRuntimeCapacityRequirements required, GameplayTriggerRuntimeCapacityProfile actual, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            Check("maxInstances", required.RequiredInstances, actual.MaxInstances, diagnostics);
            Check("maxInputBatches", required.MaxInputBatches, actual.MaxInputBatches, diagnostics);
            Check("maxInputItems", required.MaxInputItems, actual.MaxInputItems, diagnostics);
            Check("maxRelations", required.MaxRelations, actual.MaxRelations, diagnostics);
            Check("maxOccurrences", required.MaxOccurrences, actual.MaxOccurrences, diagnostics);
            Check("maxCandidates", required.MaxCandidates, actual.MaxCandidates, diagnostics);
            Check("maxRouteStates", required.MaxRouteStates, actual.MaxRouteStates, diagnostics);
            Check("maxActivations", required.MaxActivations, actual.MaxActivations, diagnostics);
            Check("maxIntents", required.MaxIntents, actual.MaxIntents, diagnostics);
            Check("maxPreparedActions", required.MaxPreparedActions, actual.MaxPreparedActions, diagnostics);
            Check("maxConditionStackDepth", required.MaxConditionStackDepth, actual.MaxConditionStackDepth, diagnostics);
            Check("maxDeferredRecords", required.MaxDeferredRecords, actual.MaxDeferredRecords, diagnostics);
            Check("maxReliableOutboxRecords", required.MaxReliableOutboxRecords, actual.MaxReliableOutboxRecords, diagnostics);
            if (actual.MaxDeferredRecords < 0 || actual.MaxReliableOutboxRecords < 0)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeProfileShapeInvalid, "$.runtimeProfile"));
        }

        private static void Check(string field, int required, int actual, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            if (actual < required) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeProfileUndersized, "$.runtimeProfile." + field, 0, 0, required, actual));
        }

        private static int Find(GameplayTriggerActionBootstrapBinding[] rows, GameplayTriggerActionKindId id) { for (int i = 0; i < rows.Length; i++) if (rows[i].ActionKindId == id) return i; return -1; }
        private static bool Contains(GameplayTriggerCompiledCatalogPlan plan, GameplayTriggerActionKindId id) { for (int i = 0; i < plan.ActionRequirementCount; i++) if (plan.GetActionRequirement(i).ActionKindId == id) return true; return false; }
    }
}
