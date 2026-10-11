using System;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.Unity
{
    /// <summary>Fail-closed composition root for one Unity occupancy ingress.</summary>
    public static class GameplayTriggerUnityOccupancyComposition
    {
        /// <summary>Validates exact catalog, consumer, buffer and source authorization identity.</summary>
        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public static bool TryCreate(
            int commandSourceId,
            RuntimeCommandSourceRegistry sourceRegistry,
            GameplayRuntimeModule gameplayModule,
            GameplayTriggerCommandSystem triggerCommandSystem,
            GameplayTriggerRuntime triggerRuntime,
            GameplayTriggerCompiledCatalogPlan compiledPlan,
            GameplayTriggerUnityOccupancyCatalog unityCatalog,
            GameplayTriggerUnitySubmissionMode submissionMode,
            in GameplayTriggerUnityOccupancyOptions options,
            out GameplayTriggerUnityOccupancyAdapter adapter,
            out GameplayTriggerUnityOccupancyFlushModule flushModule,
            out GameplayTriggerUnityInitializationResult result)
        {
            adapter = null;
            flushModule = null;
            if (sourceRegistry == null || gameplayModule == null ||
                triggerCommandSystem == null || triggerRuntime == null ||
                compiledPlan == null || unityCatalog == null ||
                (submissionMode != GameplayTriggerUnitySubmissionMode.LiveExternal &&
                 submissionMode != GameplayTriggerUnitySubmissionMode.ReplayDisabled))
            {
                result = Failure(GameplayTriggerUnityInitializationErrorCode.InvalidArgument);
                return false;
            }
            if (options.MaxSubjectBindings <= 0 ||
                options.MaxSubjectColliders < options.MaxSubjectBindings ||
                options.MaxActiveContactPairs <= 0 ||
                options.MaxSubjectBindings < unityCatalog.TotalMaxSubjects ||
                options.MaxActiveContactPairs < unityCatalog.TotalMaxSubjects)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.InvalidOptions,
                    unityCatalog.TotalMaxSubjects,
                    options.MaxSubjectBindings);
                return false;
            }
            if (unityCatalog.GameplayTriggerCatalogHash !=
                compiledPlan.GameplayTriggerCatalogHash)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.CatalogPlanMismatch,
                    compiledPlan.GameplayTriggerCatalogHash,
                    unityCatalog.GameplayTriggerCatalogHash);
                return false;
            }
            if (triggerRuntime.GameplayTriggerCatalogHash !=
                compiledPlan.GameplayTriggerCatalogHash)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.RuntimeCatalogMismatch,
                    compiledPlan.GameplayTriggerCatalogHash,
                    triggerRuntime.GameplayTriggerCatalogHash);
                return false;
            }
            if (gameplayModule.ModuleId != GameplayRuntimeModule.DefaultModuleId ||
                gameplayModule.TickStage != RuntimeTickStage.Simulation ||
                gameplayModule.Priority != 100)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.GameplayModuleScheduleMismatch);
                return false;
            }
            if (!gameplayModule.CommandBuffer.IsPendingCapacityBounded ||
                gameplayModule.CommandBuffer.PendingCapacityLimit < 1)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.CommandBufferUnbounded);
                return false;
            }
            if (triggerCommandSystem.SystemId != GameplayTriggerCommandSystem.DefaultSystemId ||
                triggerCommandSystem.Phase != GameplaySystemPhase.Command ||
                triggerCommandSystem.Priority != GameplayTriggerCommandSystem.DefaultPriority)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.CommandSystemScheduleMismatch);
                return false;
            }
            if (!triggerCommandSystem.IsEnabled)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.CommandSystemDisabled);
                return false;
            }
            if (!ReferenceEquals(triggerCommandSystem.RuntimeIdentity, triggerRuntime))
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.CommandSystemRuntimeMismatch);
                return false;
            }
            if (!gameplayModule.SystemPipeline.ContainsSystemInstance(triggerCommandSystem))
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.CommandSystemNotRegistered);
                return false;
            }
            if (triggerCommandSystem.MaxOccupancyCommandItems < unityCatalog.MaxSubjects)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.ReconstructionCapacityInsufficient,
                    unityCatalog.MaxSubjects,
                    triggerCommandSystem.MaxOccupancyCommandItems);
                return false;
            }
            if (commandSourceId <= RuntimeCommandSourceRegistry.LegacyUnspecifiedSourceId)
            {
                result = Failure(
                    GameplayTriggerUnityInitializationErrorCode.InvalidCommandSourceId,
                    expected: 1L,
                    actual: commandSourceId);
                return false;
            }

            var allowlist = new[]
            {
                GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch
            };
            if (!sourceRegistry.TryRegister(
                    commandSourceId,
                    RuntimeCommandSourceKind.Exogenous,
                    allowlist,
                    out RuntimeCommandSourceRegistrationResult registration))
            {
                result = new GameplayTriggerUnityInitializationResult(
                    false,
                    GameplayTriggerUnityInitializationErrorCode.SourceRegistrationFailed,
                    sourceErrorCode: registration.ErrorCode);
                return false;
            }

            var gate = new RuntimeCommandSubmissionGate(
                sourceRegistry,
                gameplayModule.CommandBuffer,
                RuntimeCommandIngressKind.External);
            adapter = new GameplayTriggerUnityOccupancyAdapter(
                commandSourceId,
                submissionMode,
                unityCatalog,
                triggerRuntime,
                gameplayModule.ComponentWorld,
                gate,
                in options);
            flushModule = new GameplayTriggerUnityOccupancyFlushModule(adapter);
            result = new GameplayTriggerUnityInitializationResult(
                true,
                GameplayTriggerUnityInitializationErrorCode.None);
            return true;
        }

        private static GameplayTriggerUnityInitializationResult Failure(
            GameplayTriggerUnityInitializationErrorCode errorCode,
            long expected = 0L,
            long actual = 0L) =>
            new GameplayTriggerUnityInitializationResult(
                false,
                errorCode,
                expected,
                actual);
    }
}
