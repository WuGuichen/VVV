using MxFramework.Runtime;
using MxFramework.UI;

namespace MxFramework.Demo
{
    public enum RuntimeAbilitySliceHudManualCommand
    {
        Strike = 0,
        Reset = 1
    }

    public interface IRuntimeAbilitySliceHudCommandTarget
    {
        bool IsInitialized { get; }
        bool AutoSequenceEnabled { get; }
        void SetAutoSequenceEnabled(bool enabled);
        RuntimeCommandValidationResult EnqueueHudCommand(RuntimeAbilitySliceHudManualCommand command);
    }

    public sealed class RuntimeAbilitySliceUiCommandSink : IMxUiCommandSink
    {
        private readonly IRuntimeAbilitySliceHudCommandTarget _target;

        public RuntimeAbilitySliceUiCommandSink(IRuntimeAbilitySliceHudCommandTarget target)
        {
            _target = target;
            LastResult = Failed("RuntimeAbilitySlice UI command sink has not received a command.");
        }

        public MxUiCommand LastCommand { get; private set; }
        public RuntimeCommandValidationResult LastResult { get; private set; }
        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }

        public void Enqueue(MxUiCommand command)
        {
            LastCommand = command;

            if (_target == null || !_target.IsInitialized)
            {
                Reject("RuntimeAbilitySliceRunner is not initialized.");
                return;
            }

            RuntimeAbilitySliceHudManualCommand manualCommand;
            if (!TryMap(command.CommandId, out manualCommand))
            {
                Reject("RuntimeAbilitySlice HUD command is not mapped: " + command.CommandId);
                return;
            }

            if (manualCommand != RuntimeAbilitySliceHudManualCommand.Reset && _target.AutoSequenceEnabled)
                _target.SetAutoSequenceEnabled(false);

            LastResult = _target.EnqueueHudCommand(manualCommand);
            if (LastResult.Success)
                AcceptedCount++;
            else
                RejectedCount++;
        }

        public static bool TryMap(string commandId, out RuntimeAbilitySliceHudManualCommand command)
        {
            switch (commandId)
            {
                case RuntimeAbilitySliceHudCommandIds.Strike:
                    command = RuntimeAbilitySliceHudManualCommand.Strike;
                    return true;
                case RuntimeAbilitySliceHudCommandIds.Reset:
                    command = RuntimeAbilitySliceHudManualCommand.Reset;
                    return true;
                default:
                    command = default(RuntimeAbilitySliceHudManualCommand);
                    return false;
            }
        }

        private void Reject(string message)
        {
            LastResult = Failed(message);
            RejectedCount++;
        }

        private static RuntimeCommandValidationResult Failed(string message)
        {
            return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                default(RuntimeCommand),
                RuntimeFrame.Zero,
                message));
        }
    }
}
