using System;
using System.Collections.Generic;
using MxFramework.Input;

namespace MxFramework.DebugUI.Input
{
    public interface IDebugUiInputTarget
    {
        DebugUiVisibility Visibility { get; }
        void SetVisibility(DebugUiVisibility visibility);
        void RefreshNow();
        void RequestDebugStep();
    }

    public readonly struct DebugUiInputAdapterResult
    {
        public DebugUiInputAdapterResult(int handledCommandCount, string error)
        {
            HandledCommandCount = handledCommandCount;
            Error = error ?? string.Empty;
        }

        public int HandledCommandCount { get; }
        public string Error { get; }
        public bool Success => string.IsNullOrEmpty(Error);
    }

    public sealed class DebugUiInputAdapter : IDisposable
    {
        private readonly List<InputCommand> _commands = new List<InputCommand>(8);
        private IInputProvider _input;
        private IDisposable _debugContextScope;

        public DebugUiInputAdapter(IInputProvider input = null)
        {
            _input = input;
        }

        public bool Enabled { get; private set; }
        public IInputProvider Input => _input;

        public void Configure(IInputProvider input)
        {
            if (ReferenceEquals(_input, input))
                return;

            ReleaseDebugContext();
            _input = input;
            if (Enabled)
                AcquireDebugContext();
        }

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
                return;

            Enabled = enabled;
            if (enabled)
                AcquireDebugContext();
            else
                ReleaseDebugContext();
        }

        public DebugUiInputAdapterResult ProcessFrame(long frame, IDebugUiInputTarget target)
        {
            if (!Enabled || _input == null)
                return new DebugUiInputAdapterResult(0, string.Empty);
            if (target == null)
                return new DebugUiInputAdapterResult(0, "Debug UI input target is missing.");

            _commands.Clear();
            _input.Commands.DrainForFrame(frame, _commands);

            int handled = 0;
            try
            {
                for (int i = 0; i < _commands.Count; i++)
                {
                    InputCommand command = _commands[i];
                    if (command.Phase != InputCommandPhase.Pressed
                        && command.Phase != InputCommandPhase.Performed)
                    {
                        continue;
                    }

                    if (HandleCommand(command.Intent, target))
                        handled++;
                }
            }
            catch (Exception exception)
            {
                return new DebugUiInputAdapterResult(handled, exception.Message);
            }

            return new DebugUiInputAdapterResult(handled, string.Empty);
        }

        public void Dispose()
        {
            ReleaseDebugContext();
        }

        private static bool HandleCommand(InputIntent intent, IDebugUiInputTarget target)
        {
            switch (intent)
            {
                case InputIntent.ToggleHud:
                case InputIntent.DebugCycle:
                    target.SetVisibility(NextVisibility(target.Visibility));
                    return true;
                case InputIntent.ToggleConsole:
                    target.SetVisibility(target.Visibility == DebugUiVisibility.Expanded
                        ? DebugUiVisibility.Hidden
                        : DebugUiVisibility.Expanded);
                    return true;
                case InputIntent.DebugStep:
                    target.RequestDebugStep();
                    target.RefreshNow();
                    return true;
                default:
                    return false;
            }
        }

        private static DebugUiVisibility NextVisibility(DebugUiVisibility visibility)
        {
            switch (visibility)
            {
                case DebugUiVisibility.Hidden:
                    return DebugUiVisibility.Collapsed;
                case DebugUiVisibility.Collapsed:
                    return DebugUiVisibility.Expanded;
                default:
                    return DebugUiVisibility.Hidden;
            }
        }

        private void AcquireDebugContext()
        {
            ReleaseDebugContext();
            if (_input != null)
                _debugContextScope = _input.PushContext(InputContext.Debug, InputContextPolicy.Overlay);
        }

        private void ReleaseDebugContext()
        {
            if (_debugContextScope == null)
                return;

            _debugContextScope.Dispose();
            _debugContextScope = null;
        }
    }
}
