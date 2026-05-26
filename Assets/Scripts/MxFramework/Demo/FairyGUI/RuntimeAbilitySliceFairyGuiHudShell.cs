using System;
using MxFramework.Runtime;
using MxFramework.UI;

namespace MxFramework.Demo.FairyGui
{
    public sealed class RuntimeAbilitySliceFairyGuiHudShell
    {
        private readonly RuntimeAbilitySliceFairyGuiHudController _controller;
        private readonly RuntimeAbilitySliceUiCommandSink _commandSink;

        public RuntimeAbilitySliceFairyGuiHudShell(
            RuntimeAbilitySliceFairyGuiHudController controller,
            RuntimeAbilitySliceUiCommandSink commandSink)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _commandSink = commandSink ?? throw new ArgumentNullException(nameof(commandSink));
        }

        public MxUiViewId ViewId => _controller.ViewId;
        public bool IsOpen => _controller.IsOpen;
        public MxUiCommand LastCommand => _commandSink.LastCommand;
        public RuntimeCommandValidationResult LastCommandResult => _commandSink.LastResult;
        public int AcceptedCommandCount => _commandSink.AcceptedCount;
        public int RejectedCommandCount => _commandSink.RejectedCount;

        public MxUiOpenResult Open(RuntimeAbilitySliceHudViewModel viewModel)
        {
            return _controller.Open(viewModel ?? new RuntimeAbilitySliceHudViewModel());
        }

        public MxUiOpenResult Refresh(RuntimeAbilitySliceHudViewModel viewModel)
        {
            return Open(viewModel);
        }

        public MxUiOpenResult OpenFrom(RuntimeAbilitySliceRunner runner)
        {
            return Open(RuntimeAbilitySliceHudViewModelBuilder.Build(runner));
        }

        public MxUiOpenOperation OpenAsync(RuntimeAbilitySliceHudViewModel viewModel)
        {
            return _controller.OpenAsync(viewModel ?? new RuntimeAbilitySliceHudViewModel());
        }

        public bool Close()
        {
            return _controller.Close();
        }
    }
}
