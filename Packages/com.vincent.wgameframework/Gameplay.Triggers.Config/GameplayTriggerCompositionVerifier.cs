using System;
using System.Collections.Generic;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>LowFreqAlloc pre-initialize verification result for actual composition facts.</summary>
    public sealed class GameplayTriggerCompositionVerificationResult
    {
        private readonly GameplayTriggerConfigDiagnostic[] _diagnostics;
        internal GameplayTriggerCompositionVerificationResult(List<GameplayTriggerConfigDiagnostic> diagnostics) { _diagnostics = GameplayTriggerConfigDiagnosticOrdering.Freeze(diagnostics); }
        /// <remarks>NoAlloc.</remarks>
        public bool Succeeded => _diagnostics.Length == 0;
        /// <remarks>NoAlloc.</remarks>
        public int DiagnosticCount => _diagnostics.Length;
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerConfigDiagnostic GetDiagnostic(int index) => _diagnostics[index];
    }

    /// <summary>Reads actual Host/runtime/outbox objects at the Created pre-initialize startup gate.</summary>
    public static class GameplayTriggerCompositionVerifier
    {
        /// <remarks>LowFreqAlloc. Reads actual Created-host composition and returns canonical diagnostics.</remarks>
        public static bool TryVerify(GameplayTriggerCompiledCatalogPlan plan, RuntimeHost actualHost, GameplayTriggerRuntime actualRuntime, GameplayTriggerReliableOutboxModule actualOutboxModule, out GameplayTriggerCompositionVerificationResult result)
        {
            var diagnostics = new List<GameplayTriggerConfigDiagnostic>();
            if (plan == null || actualHost == null || actualRuntime == null)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RegistryInvalid, "$.composition"));
                result = new GameplayTriggerCompositionVerificationResult(diagnostics);
                return false;
            }
            if (actualHost.State != RuntimeLifecycleState.Created)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch, "$.composition.host.state", 0, 0, (int)RuntimeLifecycleState.Created, (int)actualHost.State));
            GameplayTriggerCatalog actualCatalog = GameplayTriggerCompositionReadView.GetCatalog(actualRuntime);
            if (actualCatalog == null || actualCatalog.GameplayTriggerCatalogHash != plan.GameplayTriggerCatalogHash)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected, "$.composition.runtime.catalogHash", 0, 0, plan.GameplayTriggerCatalogHash, actualCatalog?.GameplayTriggerCatalogHash ?? 0L));

            GameplayTriggerActionKindId[] reliable = GetReliableActions(plan);
            if (reliable.Length == 0)
            {
                if (actualOutboxModule != null)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch, "$.composition.outbox", 0, 0, 0, 1));
                result = new GameplayTriggerCompositionVerificationResult(diagnostics);
                return result.Succeeded;
            }

            if (actualHost.ErrorPolicy != RuntimeHostErrorPolicy.FailFast)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ReliableRequiresFailFast, "$.composition.host.errorPolicy", 0, 0, (int)RuntimeHostErrorPolicy.FailFast, (int)actualHost.ErrorPolicy));
            if (actualOutboxModule == null)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ReliableOutboxCapabilityMissing, "$.composition.outbox"));
                result = new GameplayTriggerCompositionVerificationResult(diagnostics);
                return false;
            }
            if (!actualOutboxModule.IsEnabled || !ReferenceEquals(actualOutboxModule.RuntimeIdentity, actualRuntime) || !GameplayTriggerCompositionReadView.HostContainsModule(actualHost, actualOutboxModule))
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ReliableOutboxCapabilityMissing, "$.composition.outbox"));
            if (actualOutboxModule.FrozenTransferCount != reliable.Length)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch, "$.composition.transfers", 0, 0, reliable.Length, actualOutboxModule.FrozenTransferCount));

            RuntimeHostDiagnostics hostDiagnostics = actualHost.CaptureDiagnostics();
            for (int i = 0; i < reliable.Length; i++)
            {
                GameplayTriggerActionKindId actionKindId = reliable[i];
                string path = "$.composition.transfers[kind=" + actionKindId.Value + "]";
                int transferIndex = FindTransfer(actualOutboxModule, actionKindId);
                if (transferIndex < 0)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch, path, actionKindId.Value));
                    continue;
                }
                IGameplayTriggerReliableTargetTransfer transfer = actualOutboxModule.GetFrozenTransfer(transferIndex);
                string targetModuleId = actualOutboxModule.GetFrozenTransferTargetModuleId(transferIndex);
                if (transfer == null || transfer.ActionKindId != actionKindId || !transfer.IsEnabled || !string.Equals(transfer.TargetModuleId, targetModuleId, StringComparison.Ordinal))
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch, path, actionKindId.Value));
                    continue;
                }
                if (!TryFindModule(hostDiagnostics, targetModuleId, out RuntimeModuleDiagnostics target))
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ReliableScheduleInvalid, path + ".targetModule", actionKindId.Value));
                    continue;
                }
                if (CompareTuple(actualOutboxModule.TickStage, actualOutboxModule.Priority, actualOutboxModule.ModuleId, target.TickStage, target.Priority, target.ModuleId) >= 0)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ReliableScheduleInvalid, path + ".schedule", actionKindId.Value));
            }
            result = new GameplayTriggerCompositionVerificationResult(diagnostics);
            return result.Succeeded;
        }

        private static GameplayTriggerActionKindId[] GetReliableActions(GameplayTriggerCompiledCatalogPlan plan)
        {
            var result = new List<GameplayTriggerActionKindId>();
            for (int i = 0; i < plan.ActionRequirementCount; i++)
            {
                GameplayTriggerCompiledActionRequirement requirement = plan.GetActionRequirement(i);
                if (requirement.DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred) result.Add(requirement.ActionKindId);
            }
            return result.ToArray();
        }

        private static int FindTransfer(GameplayTriggerReliableOutboxModule module, GameplayTriggerActionKindId id)
        {
            for (int i = 0; i < module.FrozenTransferCount; i++) if (module.GetFrozenTransferActionKindId(i) == id) return i;
            return -1;
        }

        private static bool TryFindModule(RuntimeHostDiagnostics diagnostics, string moduleId, out RuntimeModuleDiagnostics module)
        {
            for (int i = 0; i < diagnostics.Modules.Count; i++) if (string.Equals(diagnostics.Modules[i].ModuleId, moduleId, StringComparison.Ordinal)) { module = diagnostics.Modules[i]; return true; }
            module = default;
            return false;
        }

        private static int CompareTuple(RuntimeTickStage leftStage, int leftPriority, string leftId, RuntimeTickStage rightStage, int rightPriority, string rightId)
        {
            int value = leftStage.CompareTo(rightStage);
            if (value != 0) return value;
            value = leftPriority.CompareTo(rightPriority);
            return value != 0 ? value : string.Compare(leftId, rightId, StringComparison.Ordinal);
        }
    }
}
