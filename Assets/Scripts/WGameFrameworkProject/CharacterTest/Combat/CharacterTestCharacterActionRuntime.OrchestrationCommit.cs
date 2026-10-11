using System;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterActionRuntime
    {
        internal readonly struct OrchestrationStage
        {
            internal readonly long Serial;
            internal readonly CharacterTestCharacterEntityId Entity;
            internal readonly CharacterActionExecutionGateway Gateway;
            internal readonly CharacterActionExecutionState Before;
            internal readonly CharacterActionPlan BeforePlan;
            internal readonly CharacterActionRunnerActionDefinition BeforeDefinition;
            internal readonly CharacterActionExecutionResult Result;
            internal readonly bool HadGateway;
            internal OrchestrationStage(long serial, CharacterTestCharacterEntityId entity, CharacterActionExecutionGateway gateway,
                CharacterActionExecutionState before, CharacterActionPlan plan, CharacterActionRunnerActionDefinition definition,
                CharacterActionExecutionResult result, bool hadGateway)
            { Serial = serial; Entity = entity; Gateway = gateway; Before = before; BeforePlan = plan;
                BeforeDefinition = definition; Result = result; HadGateway = hadGateway; }
        }
        private bool _orchestrationStaged;
        private long _orchestrationStageSerial;

        /// <summary>Stages gateway state and its event buffer without publishing animation, gameplay or prior-action cleanup.</summary>
        /// <remarks>Single-thread command boundary only. The caller must commit or abort before returning; no world tick or observer dispatch is allowed in between.</remarks>
        internal bool TryStageOrchestration(CharacterTestCharacterEntityId entity, CharacterActionId action, RuntimeTraceIdentity traceId,
            out OrchestrationStage staged, out CharacterTestActionUseFailure issue)
        {
            staged = default; issue = default;
            if (_orchestrationStaged) { issue = new CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind.Reentered); return false; }
            var components = _projection.Components;
            if (components == null || !components.Contains(entity) || !action.IsValid
                || !_catalog.TryResolvePlanCache(components, entity, out var cache)
                || !cache.TryGetOrCreate(action, traceId, out var entry) || entry?.IsSuccess != true)
            { issue = new CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind.TargetUnavailable); return false; }
            bool existed = _executions.TryGet(entity, out var gateway);
            gateway = gateway ?? GetGateway(entity);
            var before = gateway.CaptureState();
            var previousPlan = gateway.ActiveInstance?.Plan;
            var previousDefinition = gateway.ActiveInstance?.Definition;
            _executions.EventBuffer.EnsureCapacity(Math.Max(16, entry.Definition.TrackEvents.Length + 8));
            var request = CharacterActionExecutionRequest.ReplaceForOrchestration(entry.ResolveResult, entry.Definition,
                CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump, traceId);
            var result = gateway.Execute(request, _executions.EventBuffer);
            _orchestrationStaged = true;
            staged = new OrchestrationStage(++_orchestrationStageSerial, entity, gateway, before, previousPlan, previousDefinition, result, existed);
            if (result.Accepted) return true;
            issue = result.Diagnostics.Length > 0
                ? new CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind.GatewayDiagnostic,
                    diagnosticCode: result.Diagnostics[0].CodeId)
                : new CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind.TargetRejected);
            AbortOrchestration(staged); staged = default;
            if (issue.Kind == CharacterTestActionCommitFailureKind.GatewayDiagnostic
                && issue.DiagnosticCode == CharacterActionDiagnosticCode.None)
                throw new InvalidOperationException("Runtime action gateway diagnostics require a predefined integer code.");
            return false;
        }

        internal void CommitOrchestration(in OrchestrationStage stage, RuntimeFrame frame)
        {
            RequireOrchestrationStage(stage);
            _projection.ApplyOperation(_projection.Components, stage.Entity, stage.Result, frame);
            _orchestrationStaged = false;
        }

        internal void AbortOrchestration(in OrchestrationStage stage)
        {
            RequireOrchestrationStage(stage);
            if (stage.HadGateway)
            {
                if (!stage.Gateway.TryRestore(stage.Before, stage.BeforePlan, stage.BeforeDefinition, out var diagnostic))
                    throw new InvalidOperationException("Staged action rollback could not restore its captured state: " + diagnostic.Code);
            }
            else _executions.Remove(stage.Entity);
            _executions.EventBuffer.Clear();
            _orchestrationStaged = false;
        }

        private void RequireOrchestrationStage(in OrchestrationStage stage)
        {
            if (!_orchestrationStaged || stage.Serial != _orchestrationStageSerial || stage.Gateway == null)
                throw new InvalidOperationException("The action staging capability is no longer current.");
        }
    }
}
