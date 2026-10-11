using System;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Runtime;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal static class TriggerTestSignalFunctionFixtureFactory
    {
        internal const int SourceDefinitionId = 1;
        internal const int SourceBindingId = 1;

        internal static ITriggerTestFixture Create(
            TriggerTestFunctionActionSink functionSink,
            int[] functionCallIds)
        {
            if (functionSink == null)
                throw new ArgumentNullException(nameof(functionSink));

            GameplayTriggerAuthoringDocument document =
                CreateAuthoringDocument(functionCallIds);
            GameplayTriggerCompilerEnvironment environment =
                CreateEnvironment();
            GameplayTriggerCompileResult compile =
                GameplayTriggerConfigCompiler.Compile(
                    document,
                    GameplayTriggerCompilerOptions.Default,
                    environment);
            if (!compile.Succeeded)
            {
                throw new InvalidOperationException(
                    "Trigger test catalog compile failed: " +
                    Describe(compile));
            }

            GameplayTriggerRuntimeCapacityRequirements required =
                compile.CompiledCatalog.CapacityRequirements;
            var profile = new GameplayTriggerRuntimeCapacityProfile(
                required.RequiredInstances,
                required.MaxInputBatches,
                required.MaxInputItems,
                required.MaxRelations,
                required.MaxOccurrences,
                required.MaxCandidates,
                required.MaxRouteStates,
                required.MaxActivations,
                required.MaxIntents,
                required.MaxPreparedActions,
                required.MaxConditionStackDepth,
                required.MaxDeferredRecords,
                required.MaxReliableOutboxRecords);
            GameplayTriggerCatalogBootstrapResult bootstrap =
                GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                    compile.CompiledCatalog,
                    new[]
                    {
                        GameplayTriggerActionBootstrapBinding.FromSink(
                            functionSink)
                    },
                    profile);
            if (!bootstrap.Succeeded)
            {
                throw new InvalidOperationException(
                    "Trigger test catalog bootstrap failed: " +
                    Describe(bootstrap));
            }

            var runtime = new GameplayTriggerRuntime(
                bootstrap.Catalog,
                bootstrap.RuntimeOptions.Value);
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new GameplayTriggerEvaluationSystem(runtime));
            var commandBuffer = new RuntimeCommandBuffer(initialCapacity: 4);
            var gameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                commandBuffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline);
            var host = new RuntimeHost();
            host.RegisterModule(gameplayModule);
            return new TriggerTestRuntimeHostFixture(
                runtime,
                host,
                new TriggerTestServiceRegistry());
        }

        internal static GameplayTriggerAuthoringDocument
            CreateAuthoringDocument(
                int[] functionCallIds)
        {
            if (functionCallIds == null || functionCallIds.Length == 0)
            {
                throw new ArgumentException(
                    "At least one function call id is required.",
                    nameof(functionCallIds));
            }

            var source = new GameplayTriggerSourceAuthoring(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Signal);
            var condition = new GameplayTriggerConditionAuthoring(
                1,
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.PushTrue));
            var actions =
                new GameplayTriggerActionAuthoring[functionCallIds.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                if (functionCallIds[i] <= 0)
                    throw new ArgumentOutOfRangeException(nameof(functionCallIds));

                actions[i] = new GameplayTriggerActionAuthoring(
                    TriggerTestActionKindIds.FunctionCallValue,
                    GameplayTriggerTargetSelector.Owner,
                    payload0: functionCallIds[i]);
            }
            var route = new GameplayTriggerRouteAuthoring(
                1,
                GameplayTriggerOccurrenceKind.Pulsed,
                condition.ConditionId,
                actions);
            var definition = new GameplayTriggerDefinitionAuthoring(
                1,
                1,
                SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBindingAuthoring(
                1,
                definition.DefinitionId,
                SourceBindingId,
                ownerIndex: 1,
                ownerGeneration: 1);
            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { source },
                new[] { condition },
                new[] { definition },
                new[] { binding });
        }

        private static GameplayTriggerCompilerEnvironment CreateEnvironment()
        {
            Require(
                GameplayTriggerRetiredIdRegistry.TryCreate(
                    Array.Empty<GameplayTriggerRetiredIdDescriptor>(),
                    out GameplayTriggerRetiredIdRegistry retired,
                    out GameplayTriggerConfigRegistryResult retiredResult),
                retiredResult,
                "retired ids");
            var sourceContract =
                new GameplayTriggerSourceBindingContractDescriptor(
                    new GameplayTriggerSourceBindingId(SourceBindingId),
                    new GameplayTriggerSourceDefinitionId(SourceDefinitionId),
                    GameplayTriggerSourceKind.Signal,
                    new GameplayTriggerSourceProviderId(1),
                    GameplayTriggerSourceEntityCapability.None,
                    completeBatchPerEvaluation: true,
                    maxInputItemsPerEvaluation: 4,
                    maxActiveRelations: 0,
                    signalEnvelopeSchemaVersion: 1,
                    signalOrdering:
                    GameplayTriggerSignalOrdering.UnorderedMultiset);
            Require(
                GameplayTriggerSourceBindingContractRegistry.TryCreate(
                    new[] { sourceContract },
                    out GameplayTriggerSourceBindingContractRegistry sources,
                    out GameplayTriggerConfigRegistryResult sourcesResult),
                sourcesResult,
                "source contracts");
            var actionRequirement =
                new GameplayTriggerActionRequirementDescriptor(
                    TriggerTestActionKindIds.FunctionCall,
                    GameplayTriggerDeliveryContract.SameFrameReserved);
            Require(
                GameplayTriggerActionRequirementRegistry.TryCreate(
                    new[] { actionRequirement },
                    out GameplayTriggerActionRequirementRegistry actions,
                    out GameplayTriggerConfigRegistryResult actionsResult),
                actionsResult,
                "action requirements");
            var expected = new GameplayTriggerExpectedCatalogIdentity(
                GameplayTriggerConfigSchemas.RuntimeCatalogId,
                GameplayTriggerConfigSchemas.RuntimeCatalogVersion);
            Require(
                GameplayTriggerCompilerEnvironment.TryCreate(
                    retired,
                    sources,
                    actions,
                    expected,
                    out GameplayTriggerCompilerEnvironment environment,
                    out GameplayTriggerConfigRegistryResult environmentResult),
                environmentResult,
                "compiler environment");
            return environment;
        }

        private static void Require(
            bool success,
            GameplayTriggerConfigRegistryResult result,
            string label)
        {
            if (success)
                return;

            throw new InvalidOperationException(
                "Trigger test " + label + " is invalid: " +
                Describe(result));
        }

        private static string Describe(GameplayTriggerConfigRegistryResult result)
        {
            if (result == null || result.DiagnosticCount == 0)
                return "unknown";
            GameplayTriggerConfigDiagnostic diagnostic =
                result.GetDiagnostic(0);
            return diagnostic.Code + " at " + diagnostic.Path;
        }

        private static string Describe(GameplayTriggerCompileResult result)
        {
            if (result == null || result.DiagnosticCount == 0)
                return "unknown";
            GameplayTriggerConfigDiagnostic diagnostic =
                result.GetDiagnostic(0);
            return diagnostic.Code + " at " + diagnostic.Path;
        }

        private static string Describe(
            GameplayTriggerCatalogBootstrapResult result)
        {
            if (result == null || result.DiagnosticCount == 0)
                return "unknown";
            GameplayTriggerConfigDiagnostic diagnostic =
                result.GetDiagnostic(0);
            return diagnostic.Code + " at " + diagnostic.Path;
        }
    }
}
