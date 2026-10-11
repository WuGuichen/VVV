using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    /// <summary>Executes legacy CombatAction timelines strictly as children of a formal CharacterAction.</summary>
    public sealed class CharacterActionCombatTrackBridge :
        ICharacterActionCombatRequestSink,
        ICharacterActionDownstreamLifecycleSink,
        IDisposable
    {
        private readonly CombatActionRunner _runner;
        private readonly Dictionary<CombatEntityId, long> _outerInstances =
            new Dictionary<CombatEntityId, long>();
        private bool _disposed;

        public CharacterActionCombatTrackBridge(CombatActionRegistry registry)
        {
            _runner = new CombatActionRunner(registry ?? throw new ArgumentNullException(nameof(registry)));
            _runner.ActionFinished += OnChildFinished;
            _runner.ActionCanceled += OnChildCanceled;
        }

        /// <remarks>NoAlloc after the bridge dictionaries have reached their high-water mark.</remarks>
        public CharacterActionAdapterSinkResult SubmitCombatRequest(CharacterActionCombatRequest request)
        {
            if (_disposed)
                return Reject(CharacterActionDiagnosticCode.AdapterSinkFailure, "Combat track bridge is disposed.");
            if (request.EventKind != CharacterActionTrackEventKind.StartCombatAction)
            {
                return Reject(
                    CharacterActionDiagnosticCode.AdapterPayloadMissing,
                    "The CombatAction child bridge only accepts StartCombatAction events.");
            }
            if (request.CombatEntityId.IsNone)
            {
                return Reject(
                    CharacterActionDiagnosticCode.AdapterPayloadMissing,
                    "Combat track request requires a valid combat entity.");
            }
            int actionId = request.CombatActionId;
            if (actionId <= 0)
            {
                return Reject(
                    CharacterActionDiagnosticCode.CombatActionMissing,
                    "CombatAction child id must be a positive integer.");
            }

            ActionResult result = _runner.StartAction(
                request.CombatEntityId,
                actionId,
                ToCombatFrame(request.Metadata.Frame));
            if (!result.Success)
            {
                return Reject(
                    CharacterActionDiagnosticCode.CombatCancelRejected,
                    result.Reason);
            }

            _outerInstances[request.CombatEntityId] = request.Metadata.InstanceId;
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public CharacterActionAdapterSinkResult TerminateDownstream(CharacterActionDownstreamTermination termination)
        {
            if (_disposed || termination.InstanceId <= 0L)
                return CharacterActionAdapterSinkResult.AcceptedResult();

            CombatEntityId matchedEntity = default;
            bool found = false;
            foreach (KeyValuePair<CombatEntityId, long> pair in _outerInstances)
            {
                if (pair.Value != termination.InstanceId)
                    continue;
                matchedEntity = pair.Key;
                found = true;
                break;
            }

            if (!found)
                return CharacterActionAdapterSinkResult.AcceptedResult();

            _runner.ForceCancel(matchedEntity);
            _outerInstances.Remove(matchedEntity);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Tick(RuntimeFrame frame)
        {
            if (!_disposed)
                _runner.TickActions(ToCombatFrame(frame));
        }

        public CombatActionPhase GetCurrentPhase(CombatEntityId entityId)
        {
            return _disposed ? CombatActionPhase.None : _runner.GetCurrentPhase(entityId);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _runner.ActionFinished -= OnChildFinished;
            _runner.ActionCanceled -= OnChildCanceled;
            _outerInstances.Clear();
            _disposed = true;
        }

        private void OnChildFinished(ActionFinishedEvent evt)
        {
            _outerInstances.Remove(evt.EntityId);
        }

        private void OnChildCanceled(ActionCanceledEvent evt)
        {
            _outerInstances.Remove(evt.EntityId);
        }

        private static CombatFrame ToCombatFrame(RuntimeFrame frame)
        {
            return new CombatFrame(frame.Value > int.MaxValue ? int.MaxValue : (int)frame.Value);
        }

        private static CharacterActionAdapterSinkResult Reject(CharacterActionDiagnosticCode code, string message)
        {
            return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(code, message));
        }
    }

    /// <summary>Translates CharacterAction gameplay tracks into the shared Runtime command stream.</summary>
    public sealed class CharacterActionGameplayTrackBridge : ICharacterActionGameplayRequestSink
    {
        private readonly RuntimeCommandBuffer _commands;

        public CharacterActionGameplayTrackBridge(RuntimeCommandBuffer commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <remarks>NoAlloc after RuntimeCommandBuffer initialization on accepted submissions.</remarks>
        public CharacterActionAdapterSinkResult SubmitGameplayRequest(CharacterActionGameplayRequest request)
        {
            if (request.EventKind != CharacterActionTrackEventKind.CastAbility
                && request.EventKind != CharacterActionTrackEventKind.SendGameplayRequest)
            {
                return Reject(
                    CharacterActionDiagnosticCode.AdapterPayloadMissing,
                    "Gameplay track bridge currently accepts CastAbility and SendGameplayRequest events.");
            }
            if (!request.GameplayEntityId.IsValid)
            {
                return Reject(
                    CharacterActionDiagnosticCode.AdapterPayloadMissing,
                    "Gameplay track request requires a valid gameplay entity.");
            }
            if (!TryResolveAbilityId(request, out int abilityId))
            {
                return Reject(
                    CharacterActionDiagnosticCode.AdapterPayloadMissing,
                    "Gameplay track ability id must be a positive integer in requestId or abilityStableId.");
            }

            RuntimeCommand command = GameplayRuntimeCommandFactory.CastComponentAbility(
                request.Metadata.Frame,
                request.GameplayEntityId,
                abilityId,
                request.Metadata.SourceId,
                request.Metadata.TraceId);
            RuntimeCommandValidationResult result = _commands.Enqueue(command);
            return result.Success
                ? CharacterActionAdapterSinkResult.AcceptedResult()
                : Reject(CharacterActionDiagnosticCode.AdapterSinkFailure, result.Error.Message);
        }

        private static bool TryResolveAbilityId(CharacterActionGameplayRequest request, out int abilityId)
        {
            abilityId = request.RequestId > 0 ? request.RequestId : request.AbilityStableId;
            return abilityId > 0;
        }

        private static CharacterActionAdapterSinkResult Reject(CharacterActionDiagnosticCode code, string message)
        {
            return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(code, message));
        }
    }
}
