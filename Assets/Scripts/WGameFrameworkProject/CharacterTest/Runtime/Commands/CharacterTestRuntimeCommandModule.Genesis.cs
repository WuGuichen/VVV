using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeCommandModule
    {
        private void ApplyGenesisMovableObjectProbe(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectGenesisMovableObjectProbe(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestGenesisMovableObjectProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisMovableObjectProbe(command);
            if (!_characterRuntimeModule.TryRunGenesisMovableObjectProbe(request, command.Frame, out string message))
            {
                RejectGenesisMovableObjectProbe(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisMovableObjectProbeApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: string.IsNullOrWhiteSpace(message)
                    ? "genesis movable object probe applied"
                    : message));
        }

        private void ApplyGenesisPotencyMechanicsProbe(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectGenesisPotencyMechanicsProbe(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestGenesisPotencyMechanicsProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisPotencyMechanicsProbe(command);
            if (!_characterRuntimeModule.TryRunGenesisPotencyMechanicsProbe(request, command.Frame, out string message))
            {
                RejectGenesisPotencyMechanicsProbe(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisPotencyMechanicsProbeApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: string.IsNullOrWhiteSpace(message)
                    ? "genesis potency mechanics probe applied"
                    : message));
        }

        private void ApplyGenesisLivingBeingProbe(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectGenesisLivingBeingProbe(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestGenesisLivingBeingProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisLivingBeingProbe(command);
            if (!_characterRuntimeModule.TryRunGenesisLivingBeingProbe(request, command.Frame, out string message))
            {
                RejectGenesisLivingBeingProbe(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisLivingBeingProbeApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: string.IsNullOrWhiteSpace(message)
                    ? "genesis living being probe applied"
                    : message));
        }

        private void ApplyGenesisLivingNaturalAttributesProbe(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectGenesisLivingNaturalAttributesProbe(command, "Character runtime module is not available.");
                return;
            }

            CharacterTestGenesisLivingNaturalAttributesProbeRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisLivingNaturalAttributesProbe(command);
            if (!_characterRuntimeModule.TryRunGenesisLivingNaturalAttributesProbe(
                    request,
                    command.Frame,
                    out string message))
            {
                RejectGenesisLivingNaturalAttributesProbe(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisLivingNaturalAttributesProbeApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: string.IsNullOrWhiteSpace(message)
                    ? "genesis living natural attributes probe applied"
                    : message));
        }

        private void ApplyGenesisLivingNaturalAttributesRuntimeState(RuntimeCommand command)
        {
            if (_characterRuntimeModule == null)
            {
                RejectGenesisLivingNaturalAttributesRuntimeState(
                    command,
                    "Character runtime module is not available.");
                return;
            }

            CharacterTestGenesisLivingNaturalAttributesRuntimeStateRequest request =
                CharacterTestRuntimeCommandFactory.DecodeGenesisLivingNaturalAttributesRuntimeState(command);
            if (!_characterRuntimeModule.TryApplyGenesisLivingNaturalAttributesRuntimeState(
                    request,
                    command.Frame,
                    out string message))
            {
                RejectGenesisLivingNaturalAttributesRuntimeState(command, message);
                return;
            }

            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisLivingNaturalAttributesRuntimeStateApplied,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: request.TargetEntityId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: string.IsNullOrWhiteSpace(message)
                    ? "genesis living natural attributes runtime state applied"
                    : message));
        }

        private void RejectGenesisMovableObjectProbe(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisMovableObjectProbeRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectGenesisPotencyMechanicsProbe(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisPotencyMechanicsProbeRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectGenesisLivingBeingProbe(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisLivingBeingProbeRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectGenesisLivingNaturalAttributesProbe(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisLivingNaturalAttributesProbeRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }

        private void RejectGenesisLivingNaturalAttributesRuntimeState(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.GenesisLivingNaturalAttributesRuntimeStateRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: command.TargetId,
                payload0: command.Payload0,
                payload1: command.Payload1,
                payload2: command.Payload2,
                message: message));
        }
    }
}
