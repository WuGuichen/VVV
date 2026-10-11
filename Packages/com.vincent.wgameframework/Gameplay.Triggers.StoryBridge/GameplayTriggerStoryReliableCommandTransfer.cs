using System;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;

namespace MxFramework.Gameplay.Triggers.StoryBridge
{
    /// <summary>Transfers Story.RaiseTrigger actions into an exact bounded Story command composition.</summary>
    public sealed class GameplayTriggerStoryReliableCommandTransfer :
        IGameplayTriggerReliableTargetTransfer
    {
        private readonly RuntimeHost _host;
        private readonly StoryRuntimeModule _module;
        private readonly RuntimeCommandSubmissionGate _gate;
        private bool _isEnabled = true;

        /// <remarks>AllocByDesign. Validates the immutable composition and authorization graph.</remarks>
        public GameplayTriggerStoryReliableCommandTransfer(
            GameplayTriggerActionKindId actionKindId,
            int sourceId,
            RuntimeHost host,
            StoryRuntimeModule module,
            RuntimeCommandSubmissionGate gate)
        {
            if (actionKindId != GameplayTriggerStoryCommandActionKinds.RaiseTrigger)
                throw new ArgumentOutOfRangeException(nameof(actionKindId));
            if (sourceId != StoryRuntimeCommandSources.GameplayBridge)
                throw new ArgumentOutOfRangeException(nameof(sourceId));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));

            if (_module.ModuleId != StoryRuntimeModule.DefaultModuleId ||
                _module.TickStage != RuntimeTickStage.Simulation ||
                _module.Priority != StoryRuntimeModule.DefaultPriority)
            {
                throw new ArgumentException(
                    "Story Trigger transfer requires the default Story module scheduling contract.",
                    nameof(module));
            }
            if (_gate.IngressKind != RuntimeCommandIngressKind.TrustedDerived)
                throw new ArgumentException("Story Trigger transfer requires TrustedDerived ingress.", nameof(gate));
            if (!_gate.IsTargetPendingCapacityBounded)
                throw new ArgumentException("Story Trigger transfer requires a hard-bounded command buffer.", nameof(gate));
            if (!ReferenceEquals(_gate.CommandBufferIdentity, _module.CommandBuffer))
                throw new ArgumentException("Submission gate must target the Story module command buffer.", nameof(gate));
            if (!ReferenceEquals(_gate.TargetValidatorIdentity, _module.Validator))
                throw new ArgumentException("Bounded admission and Story module drain must use the same validator instance.", nameof(gate));
            if (!ReferenceEquals(
                    _module.Validator.CommandContractIdentity,
                    StoryRuntimeCommandValidator.DefaultCommandContractIdentity))
            {
                throw new ArgumentException(
                    "Story Trigger transfer requires the default Story command validator contract.",
                    nameof(module));
            }
            ValidateSourceContract(sourceId, _gate.SourceRegistryIdentity);

            ActionKindId = actionKindId;
        }

        public GameplayTriggerActionKindId ActionKindId { get; }
        public string TargetModuleId => _module.ModuleId;
        public bool IsEnabled => _isEnabled;

        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled) => _isEnabled = enabled;

        /// <remarks>NoAlloc after initialization. Rejected transfer leaves both authorities unchanged.</remarks>
        public GameplayTriggerReliableTransferResult TryTransfer(
            RuntimeFrame currentFrame,
            in GameplayTriggerReliableOutboxRecord record)
        {
            if (!_isEnabled)
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetDisabled);
            }
            if (_host.State != RuntimeLifecycleState.Started)
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                    GameplayTriggerStoryCommandContracts.TargetHostNotStartedDetailCode);
            }
            if (!_host.ContainsModuleInstance(_module))
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                    GameplayTriggerStoryCommandContracts.TargetModuleNotRegisteredDetailCode);
            }
            if (!TryCreateCommand(currentFrame, in record, out RuntimeCommand command))
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TransientRejected,
                    (int)RuntimeCommandErrorCode.InvalidPayload);
            }

            RuntimeCommandValidationResult submission = _gate.Submit(command);
            if (submission.Success)
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.Accepted);
            }
            if (submission.Error.Code == RuntimeCommandErrorCode.PendingCapacityExceeded)
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetCapacityExceeded,
                    (int)submission.Error.Code);
            }
            return new GameplayTriggerReliableTransferResult(
                GameplayTriggerReliableTransferStatus.TransientRejected,
                (int)submission.Error.Code);
        }

        private static bool TryCreateCommand(
            RuntimeFrame currentFrame,
            in GameplayTriggerReliableOutboxRecord record,
            out RuntimeCommand command)
        {
            command = default;
            if (record.ActionKindId != GameplayTriggerStoryCommandActionKinds.RaiseTrigger ||
                record.Sequence <= 0L ||
                record.ActivationId <= 0L ||
                record.ActionIndex < 0 ||
                record.OriginalActivationFrame.Value < 0L ||
                record.OriginalActivationFrame.Value == long.MaxValue ||
                record.OriginalDueFrame.Value != record.OriginalActivationFrame.Value + 1L ||
                record.OriginalDueFrame > currentFrame ||
                record.ResolvedTarget.DomainId !=
                    GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId ||
                record.ResolvedTarget.Value <= 0L ||
                record.ResolvedTarget.Value > int.MaxValue ||
                record.ResolvedTarget.Generation != 0 ||
                record.ActionPayload0 <= 0L ||
                record.ActionPayload0 > int.MaxValue ||
                record.ActionPayload1 < int.MinValue ||
                record.ActionPayload1 > int.MaxValue ||
                record.ActionPayload2 < int.MinValue ||
                record.ActionPayload2 > int.MaxValue)
            {
                return false;
            }

            RuntimeCommand mapped = StoryRuntimeCommandFactory.RaiseTrigger(
                currentFrame,
                StoryRuntimeCommandSources.GameplayBridge,
                (int)record.ActionPayload0,
                (int)record.ActionPayload1,
                (int)record.ActionPayload2,
                (int)record.ResolvedTarget.Value,
                default);
            var envelope = new RuntimeCommandEnvelope(
                GameplayTriggerStoryCommandContracts.EnvelopeTypeId,
                GameplayTriggerStoryCommandContracts.EnvelopeSchemaVersion,
                RuntimeCommandProvenanceKind.Derived,
                record.ActivationId,
                record.ActionIndex,
                record.ActionKindId.Value,
                record.Sequence,
                record.OriginalActivationFrame.Value,
                record.OriginalDueFrame.Value,
                0L,
                0L);
            command = new RuntimeCommand(
                mapped.Frame,
                mapped.SourceId,
                mapped.CommandId,
                mapped.TargetId,
                mapped.Payload0,
                mapped.Payload1,
                mapped.Payload2,
                default,
                sequence: 0L,
                envelope: envelope);
            return true;
        }

        private static void ValidateSourceContract(
            int sourceId,
            RuntimeCommandSourceRegistry sourceRegistry)
        {
            if (!sourceRegistry.TryGet(sourceId, out RuntimeCommandSourceDefinition source) ||
                source.Kind != RuntimeCommandSourceKind.Derived ||
                source.AllowedCommandCount != 1 ||
                !source.AllowsCommand(StoryRuntimeCommandIds.RaiseTrigger))
            {
                throw new ArgumentException(
                    "Story Trigger source must be Derived with the exact RaiseTrigger-only allowlist.",
                    nameof(sourceId));
            }
        }
    }
}
