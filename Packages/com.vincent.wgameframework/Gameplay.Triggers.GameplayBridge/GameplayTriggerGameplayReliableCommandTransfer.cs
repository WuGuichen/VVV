using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.GameplayBridge
{
    /// <summary>Transfers one reliable Trigger action kind into a bounded trusted-derived Gameplay command gate.</summary>
    public sealed class GameplayTriggerGameplayReliableCommandTransfer :
        IGameplayTriggerReliableTargetTransfer
    {
        private readonly int _sourceId;
        private readonly RuntimeHost _host;
        private readonly GameplayRuntimeModule _module;
        private readonly IGameplaySystem _consumer;
        private readonly RuntimeCommandSubmissionGate _gate;
        private bool _isEnabled = true;

        /// <remarks>AllocByDesign. Validates the immutable composition and authorization graph.</remarks>
        public GameplayTriggerGameplayReliableCommandTransfer(
            GameplayTriggerActionKindId actionKindId,
            int sourceId,
            RuntimeHost host,
            GameplayRuntimeModule module,
            IGameplaySystem consumer,
            RuntimeCommandSubmissionGate gate)
        {
            if (!GameplayTriggerGameplayCommandContracts.IsSupportedActionKind(actionKindId))
                throw new ArgumentOutOfRangeException(nameof(actionKindId));
            if (sourceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            if (_gate.IngressKind != RuntimeCommandIngressKind.TrustedDerived)
                throw new ArgumentException("Gameplay Trigger command transfer requires TrustedDerived ingress.", nameof(gate));
            if (!_gate.IsTargetPendingCapacityBounded)
                throw new ArgumentException("Gameplay Trigger command transfer requires a hard-bounded command buffer.", nameof(gate));
            if (!ReferenceEquals(_gate.CommandBufferIdentity, _module.CommandBuffer))
                throw new ArgumentException("Submission gate must target the Gameplay module command buffer.", nameof(gate));
            ValidateSourceContract(sourceId, _gate.SourceRegistryIdentity, actionKindId);
            ValidateConsumer(actionKindId, _consumer);

            ActionKindId = actionKindId;
            _sourceId = sourceId;
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
            if (!_isEnabled || !_consumer.IsEnabled)
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetDisabled);
            }
            if (_host.State != RuntimeLifecycleState.Started)
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                    GameplayTriggerGameplayCommandContracts.TargetHostNotStartedDetailCode);
            }
            if (!_host.ContainsModuleInstance(_module))
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                    GameplayTriggerGameplayCommandContracts.TargetModuleNotRegisteredDetailCode);
            }
            if (!_module.SystemPipeline.ContainsSystemInstance(_consumer))
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                    GameplayTriggerGameplayCommandContracts.TargetConsumerNotRegisteredDetailCode);
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

        private bool TryCreateCommand(
            RuntimeFrame currentFrame,
            in GameplayTriggerReliableOutboxRecord record,
            out RuntimeCommand command)
        {
            command = default;
            if (record.ActionKindId != ActionKindId ||
                record.Sequence <= 0L ||
                record.ActivationId <= 0L ||
                record.ActionIndex < 0 ||
                record.OriginalActivationFrame.Value == long.MaxValue ||
                record.OriginalDueFrame.Value != record.OriginalActivationFrame.Value + 1L ||
                record.OriginalDueFrame > currentFrame ||
                record.ResolvedTarget.DomainId != GameplayTriggerTargetRef.GameplayEntityDomainId ||
                record.ResolvedTarget.Value <= 0L ||
                record.ResolvedTarget.Value > int.MaxValue ||
                record.ResolvedTarget.Generation <= 0)
            {
                return false;
            }

            int targetIndex = (int)record.ResolvedTarget.Value;
            var entityId = new GameplayEntityId(targetIndex, record.ResolvedTarget.Generation);
            RuntimeCommand mapped;
            if (ActionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity)
            {
                if (record.ActionPayload0 != 0L ||
                    record.ActionPayload1 != 0L ||
                    record.ActionPayload2 != 0L)
                    return false;
                mapped = GameplayRuntimeCommandFactory.DestroyComponentEntity(
                    currentFrame,
                    entityId,
                    _sourceId,
                    default);
            }
            else if (ActionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute ||
                     ActionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute)
            {
                if (record.ActionPayload0 <= 0L ||
                    record.ActionPayload0 > int.MaxValue ||
                    record.ActionPayload1 < int.MinValue ||
                    record.ActionPayload1 > int.MaxValue ||
                    record.ActionPayload2 != 0L)
                    return false;
                mapped = ActionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute
                    ? GameplayRuntimeCommandFactory.SetComponentAttribute(
                        currentFrame,
                        entityId,
                        (int)record.ActionPayload0,
                        (int)record.ActionPayload1,
                        _sourceId,
                        default)
                    : GameplayRuntimeCommandFactory.AddComponentAttribute(
                        currentFrame,
                        entityId,
                        (int)record.ActionPayload0,
                        (int)record.ActionPayload1,
                        _sourceId,
                        default);
            }
            else
            {
                if (record.ActionPayload0 <= 0L ||
                    record.ActionPayload0 > int.MaxValue ||
                    record.ActionPayload1 != 0L ||
                    record.ActionPayload2 != 0L)
                    return false;
                mapped = GameplayRuntimeCommandFactory.CastComponentAbility(
                    currentFrame,
                    entityId,
                    (int)record.ActionPayload0,
                    _sourceId,
                    default);
            }

            var envelope = new RuntimeCommandEnvelope(
                GameplayTriggerGameplayCommandContracts.EnvelopeTypeId,
                GameplayTriggerGameplayCommandContracts.EnvelopeSchemaVersion,
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
            RuntimeCommandSourceRegistry sourceRegistry,
            GameplayTriggerActionKindId actionKindId)
        {
            int commandId = GameplayTriggerGameplayCommandContracts.ResolveCommandId(actionKindId);
            if (!sourceRegistry.TryGet(sourceId, out RuntimeCommandSourceDefinition source) ||
                source.Kind != RuntimeCommandSourceKind.Derived ||
                !source.AllowsCommand(commandId) ||
                source.AllowedCommandCount != 4 ||
                !source.AllowsCommand(GameplayRuntimeCommandIds.DestroyComponentEntity) ||
                !source.AllowsCommand(GameplayRuntimeCommandIds.SetComponentAttribute) ||
                !source.AllowsCommand(GameplayRuntimeCommandIds.AddComponentAttribute) ||
                !source.AllowsCommand(GameplayRuntimeCommandIds.CastComponentAbility))
            {
                throw new ArgumentException(
                    "Gameplay Trigger command source must be Derived with the exact four-command allowlist.",
                    nameof(sourceId));
            }
        }

        private static void ValidateConsumer(
            GameplayTriggerActionKindId actionKindId,
            IGameplaySystem consumer)
        {
            Type expectedType;
            int expectedPriority;
            if (actionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity)
            {
                expectedType = typeof(GameplayComponentEntityCommandSystem);
                expectedPriority = 20;
            }
            else if (actionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute ||
                     actionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute)
            {
                expectedType = typeof(GameplayAttributeCommandSystem);
                expectedPriority = 40;
            }
            else
            {
                expectedType = typeof(GameplayComponentAbilityCommandSystem);
                expectedPriority = 50;
            }

            if (consumer.GetType() != expectedType ||
                consumer.Phase != GameplaySystemPhase.Command ||
                consumer.Priority != expectedPriority ||
                consumer.Priority >= int.MaxValue)
            {
                throw new ArgumentException(
                    "Gameplay Trigger command transfer consumer has the wrong concrete type or scheduling contract.",
                    nameof(consumer));
            }
        }
    }
}
