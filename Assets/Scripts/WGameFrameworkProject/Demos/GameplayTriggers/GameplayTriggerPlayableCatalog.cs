using System;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Gameplay.Triggers.StoryBridge;

namespace MxFramework.Demo.GameplayTriggers
{
    internal sealed class GameplayTriggerPlayableCatalogComposition
    {
        internal GameplayTriggerPlayableCatalogComposition(
            GameplayTriggerCompiledCatalogPlan plan,
            GameplayTriggerCatalog catalog,
            GameplayTriggerRuntimeOptions runtimeOptions)
        {
            Plan = plan;
            Catalog = catalog;
            RuntimeOptions = runtimeOptions;
        }

        internal GameplayTriggerCompiledCatalogPlan Plan { get; }
        internal GameplayTriggerCatalog Catalog { get; }
        internal GameplayTriggerRuntimeOptions RuntimeOptions { get; }
    }

    internal static class GameplayTriggerPlayableCatalog
    {
        private static readonly GameplayTriggerRuntimeCapacityProfile RuntimeProfile =
            new GameplayTriggerRuntimeCapacityProfile(
                3, 3, 3, 3, 6, 6, 6, 6, 12, 12, 8, 4, 4);

        /// <remarks>AllocByDesign. Compiles and bootstraps the immutable playable catalog once.</remarks>
        internal static GameplayTriggerPlayableCatalogComposition Create(
            GameplayTriggerGameplayBuffActionSink buffSink,
            GameplayTriggerStoryReliableCommandActionSink storySink)
        {
            if (buffSink == null)
                throw new ArgumentNullException(nameof(buffSink));
            if (storySink == null)
                throw new ArgumentNullException(nameof(storySink));

            GameplayTriggerCompilerEnvironment environment = CreateEnvironment();
            GameplayTriggerCompileResult compile = GameplayTriggerConfigCompiler.Compile(
                CreateAuthoringDocument(),
                GameplayTriggerCompilerOptions.Default,
                environment);
            if (!compile.Succeeded)
                throw new InvalidOperationException("Gameplay Trigger playable catalog compilation failed.");
            ValidateFrozenPolicies(compile.CompiledCatalog);

            GameplayTriggerCatalogBootstrapResult bootstrap =
                GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                    compile.CompiledCatalog,
                    new[]
                    {
                        GameplayTriggerActionBootstrapBinding.FromInternal(
                            new GameplayTriggerInternalDeferredActionDescriptor(
                                GameplayTriggerPlayableIds.SetEnabledAction,
                                GameplayTriggerInternalDeferredOperationKind.SetEnabled)),
                        GameplayTriggerActionBootstrapBinding.FromSink(buffSink),
                        GameplayTriggerActionBootstrapBinding.FromSink(storySink)
                    },
                    RuntimeProfile);
            if (!bootstrap.Succeeded)
                throw new InvalidOperationException("Gameplay Trigger playable catalog bootstrap failed.");

            return new GameplayTriggerPlayableCatalogComposition(
                compile.CompiledCatalog,
                bootstrap.Catalog,
                bootstrap.RuntimeOptions.Value);
        }

        private static GameplayTriggerCompilerEnvironment CreateEnvironment()
        {
            Require(GameplayTriggerRetiredIdRegistry.TryCreate(
                null,
                out GameplayTriggerRetiredIdRegistry retired,
                out _));

            GameplayTriggerSourceEntityCapability occupancyCapabilities =
                GameplayTriggerSourceEntityCapability.SubjectPossible |
                GameplayTriggerSourceEntityCapability.SubjectGuaranteed |
                GameplayTriggerSourceEntityCapability.PresenceEpoch;
            var sourceContracts = new[]
            {
                SourceContract(
                    GameplayTriggerPlayableIds.CheckpointSourceBinding,
                    GameplayTriggerPlayableIds.CheckpointSourceDefinition,
                    occupancyCapabilities),
                SourceContract(
                    GameplayTriggerPlayableIds.DoorSourceBinding,
                    GameplayTriggerPlayableIds.DoorSourceDefinition,
                    occupancyCapabilities),
                SourceContract(
                    GameplayTriggerPlayableIds.TrapSourceBinding,
                    GameplayTriggerPlayableIds.TrapSourceDefinition,
                    occupancyCapabilities)
            };
            Require(GameplayTriggerSourceBindingContractRegistry.TryCreate(
                sourceContracts,
                out GameplayTriggerSourceBindingContractRegistry sources,
                out _));

            var actionRequirements = new[]
            {
                new GameplayTriggerActionRequirementDescriptor(
                    GameplayTriggerPlayableIds.SetEnabledAction,
                    GameplayTriggerDeliveryContract.InternalDeferred,
                    GameplayTriggerInternalDeferredOperationKind.SetEnabled),
                new GameplayTriggerActionRequirementDescriptor(
                    GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                    GameplayTriggerDeliveryContract.SameFrameReserved),
                new GameplayTriggerActionRequirementDescriptor(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    GameplayTriggerDeliveryContract.ReliableDeferred)
            };
            Require(GameplayTriggerActionRequirementRegistry.TryCreate(
                actionRequirements,
                out GameplayTriggerActionRequirementRegistry actions,
                out _));

            Require(GameplayTriggerCompilerEnvironment.TryCreate(
                retired,
                sources,
                actions,
                new GameplayTriggerExpectedCatalogIdentity(
                    GameplayTriggerConfigSchemas.RuntimeCatalogId,
                    GameplayTriggerConfigSchemas.RuntimeCatalogVersion),
                out GameplayTriggerCompilerEnvironment environment,
                out _));
            return environment;
        }

        private static GameplayTriggerSourceBindingContractDescriptor SourceContract(
            int sourceBindingId,
            int sourceDefinitionId,
            GameplayTriggerSourceEntityCapability capabilities) =>
            new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(sourceBindingId),
                new GameplayTriggerSourceDefinitionId(sourceDefinitionId),
                GameplayTriggerSourceKind.Occupancy,
                GameplayTriggerPlayableIds.CombatSourceProvider,
                capabilities,
                completeBatchPerEvaluation: true,
                maxInputItemsPerEvaluation: 1,
                maxActiveRelations: 1);

        private static void ValidateFrozenPolicies(
            GameplayTriggerCompiledCatalogPlan plan)
        {
            if (plan == null || plan.DefinitionCount != 3)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable definitions do not match the frozen catalog.");

            for (int i = 0; i < plan.DefinitionCount; i++)
            {
                GameplayTriggerDefinition definition = plan.GetDefinition(i);
                if (definition.RouteCount != 1)
                    throw new InvalidOperationException(
                        "Each Gameplay Trigger playable definition must own exactly one route.");
                GameplayTriggerRouteDefinition route = definition.GetRoute(0);
                GameplayTriggerActivationPolicy policy = route.ActivationPolicy;
                bool trap = definition.DefinitionId.Value ==
                    GameplayTriggerPlayableIds.TrapDefinition;
                bool validPresence = trap
                    ? policy.PresenceLimit == GameplayTriggerPresenceLimit.Ignore &&
                      policy.StayIntervalFrames == 1L &&
                      policy.CooldownFrames == 30L &&
                      policy.MaxActivations == 8
                    : policy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence &&
                      policy.StayIntervalFrames == 0L &&
                      policy.CooldownFrames == 0L &&
                      policy.MaxActivations == 1;
                if (policy.StateScope != GameplayTriggerStateScope.PerSubject ||
                    !validPresence)
                {
                    throw new InvalidOperationException(
                        "Gameplay Trigger playable route policy differs from the approved catalog.");
                }
            }
        }

        private static GameplayTriggerAuthoringDocument CreateAuthoringDocument()
        {
            var trueCondition = new GameplayTriggerConditionNodeAuthoring(
                GameplayTriggerConditionOperation.PushTrue);
            var definitions = new[]
            {
                new GameplayTriggerDefinitionAuthoring(
                    GameplayTriggerPlayableIds.CheckpointDefinition,
                    1,
                    GameplayTriggerPlayableIds.CheckpointSourceDefinition,
                    new[]
                    {
                        new GameplayTriggerRouteAuthoring(
                            GameplayTriggerPlayableIds.CheckpointRoute,
                            GameplayTriggerOccurrenceKind.Entered,
                            GameplayTriggerPlayableIds.CheckpointCondition,
                            new[]
                            {
                                new GameplayTriggerActionAuthoring(
                                    GameplayTriggerPlayableIds.SetEnabledActionKind,
                                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                                    GameplayTriggerPlayableIds.ExplicitDoorTargetSlot,
                                    payload0: 1L)
                            },
                            stateScope: GameplayTriggerStateScope.PerSubject,
                            presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence,
                            maxActivations: 1)
                    }),
                new GameplayTriggerDefinitionAuthoring(
                    GameplayTriggerPlayableIds.DoorDefinition,
                    1,
                    GameplayTriggerPlayableIds.DoorSourceDefinition,
                    new[]
                    {
                        new GameplayTriggerRouteAuthoring(
                            GameplayTriggerPlayableIds.DoorRoute,
                            GameplayTriggerOccurrenceKind.Entered,
                            GameplayTriggerPlayableIds.DoorCondition,
                            new[]
                            {
                                new GameplayTriggerActionAuthoring(
                                    GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue,
                                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                                    GameplayTriggerPlayableIds.ExplicitDoorTargetSlot,
                                    payload0: GameplayTriggerPlayableIds.StoryOpenTrigger)
                            },
                            stateScope: GameplayTriggerStateScope.PerSubject,
                            presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence,
                            maxActivations: 1)
                    }),
                new GameplayTriggerDefinitionAuthoring(
                    GameplayTriggerPlayableIds.TrapDefinition,
                    1,
                    GameplayTriggerPlayableIds.TrapSourceDefinition,
                    new[]
                    {
                        new GameplayTriggerRouteAuthoring(
                            GameplayTriggerPlayableIds.TrapRoute,
                            GameplayTriggerOccurrenceKind.Entered |
                                GameplayTriggerOccurrenceKind.Stayed,
                            GameplayTriggerPlayableIds.TrapCondition,
                            new[]
                            {
                                new GameplayTriggerActionAuthoring(
                                    GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuffValue,
                                    GameplayTriggerTargetSelector.Subject,
                                    payload0: GameplayTriggerPlayableIds.TrapCounterBuff,
                                    payload1: GameplayTriggerPlayableIds.TrapBuffSource),
                                new GameplayTriggerActionAuthoring(
                                    GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuffValue,
                                    GameplayTriggerTargetSelector.Subject,
                                    payload0: GameplayTriggerPlayableIds.TrapSlowBuff,
                                    payload1: GameplayTriggerPlayableIds.TrapBuffSource)
                            },
                            stateScope: GameplayTriggerStateScope.PerSubject,
                            maxActivations: 8,
                            cooldownFrames: 30L,
                            presenceLimit: GameplayTriggerPresenceLimit.Ignore,
                            stayIntervalFrames: 1L,
                            maxStateKeys: 1)
                    })
            };

            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[]
                {
                    OccupancySource(GameplayTriggerPlayableIds.CheckpointSourceDefinition),
                    OccupancySource(GameplayTriggerPlayableIds.DoorSourceDefinition),
                    OccupancySource(GameplayTriggerPlayableIds.TrapSourceDefinition)
                },
                new[]
                {
                    new GameplayTriggerConditionAuthoring(
                        GameplayTriggerPlayableIds.CheckpointCondition,
                        trueCondition),
                    new GameplayTriggerConditionAuthoring(
                        GameplayTriggerPlayableIds.DoorCondition,
                        trueCondition),
                    new GameplayTriggerConditionAuthoring(
                        GameplayTriggerPlayableIds.TrapCondition,
                        trueCondition)
                },
                definitions,
                new[]
                {
                    new GameplayTriggerBindingAuthoring(
                        GameplayTriggerPlayableIds.CheckpointBinding,
                        GameplayTriggerPlayableIds.CheckpointDefinition,
                        GameplayTriggerPlayableIds.CheckpointSourceBinding,
                        explicitTargets: new[]
                        {
                            new GameplayTriggerBindingTargetAuthoring(
                                GameplayTriggerPlayableIds.ExplicitDoorTargetSlot,
                                GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                                GameplayTriggerPlayableIds.DoorSourceBinding)
                        }),
                    new GameplayTriggerBindingAuthoring(
                        GameplayTriggerPlayableIds.DoorBinding,
                        GameplayTriggerPlayableIds.DoorDefinition,
                        GameplayTriggerPlayableIds.DoorSourceBinding,
                        initiallyEnabled: false,
                        explicitTargets: new[]
                        {
                            new GameplayTriggerBindingTargetAuthoring(
                                GameplayTriggerPlayableIds.ExplicitDoorTargetSlot,
                                GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                                GameplayTriggerPlayableIds.DoorSourceBinding)
                        }),
                    new GameplayTriggerBindingAuthoring(
                        GameplayTriggerPlayableIds.TrapBinding,
                        GameplayTriggerPlayableIds.TrapDefinition,
                        GameplayTriggerPlayableIds.TrapSourceBinding)
                });
        }

        private static GameplayTriggerSourceAuthoring OccupancySource(int id) =>
            new GameplayTriggerSourceAuthoring(
                id,
                GameplayTriggerSourceKind.Occupancy,
                initialSamplePolicy: GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive,
                departurePolicy: GameplayTriggerSubjectDeparturePolicy.EmitExited);

        private static void Require(bool success)
        {
            if (!success)
                throw new InvalidOperationException(
                    "Gameplay Trigger playable compiler environment is invalid.");
        }
    }
}
