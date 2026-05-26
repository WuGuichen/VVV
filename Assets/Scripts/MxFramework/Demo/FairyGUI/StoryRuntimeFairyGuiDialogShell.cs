using System;
using MxFramework.Demo.Story;
using MxFramework.Runtime;
using MxFramework.UI;

namespace MxFramework.Demo.FairyGui
{
    public sealed class StoryRuntimeFairyGuiDialogShell
    {
        private readonly StoryRuntimeFairyGuiDialogController _controller;
        private readonly StoryRuntimeVerticalSliceUiCommandSink _commandSink;

        public StoryRuntimeFairyGuiDialogShell(
            StoryRuntimeFairyGuiDialogController controller,
            StoryRuntimeVerticalSliceUiCommandSink commandSink)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _commandSink = commandSink ?? throw new ArgumentNullException(nameof(commandSink));
        }

        public MxUiViewId ViewId => _controller.ViewId;
        public bool IsOpen => _controller.IsOpen;
        public MxUiCommand LastCommand => _commandSink.LastCommand;
        public RuntimeCommand LastRuntimeCommand => _commandSink.LastRuntimeCommand;
        public RuntimeCommandValidationResult LastCommandResult => _commandSink.LastResult;
        public int AcceptedCommandCount => _commandSink.AcceptedCount;
        public int RejectedCommandCount => _commandSink.RejectedCount;

        public MxUiOpenResult Open(StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            return _controller.Open(viewModel ?? new StoryRuntimeVerticalSliceFairyGuiViewModel());
        }

        public MxUiOpenResult Refresh(StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            return Open(viewModel);
        }

        public MxUiOpenResult OpenFrom(StoryRuntimeVerticalSliceDemo demo)
        {
            if (demo == null)
                return Open(new StoryRuntimeVerticalSliceFairyGuiViewModel());

            return Open(StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(demo.CreateSnapshot()));
        }

        public MxUiOpenOperation OpenAsync(StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            return _controller.OpenAsync(viewModel ?? new StoryRuntimeVerticalSliceFairyGuiViewModel());
        }

        public bool Close()
        {
            return _controller.Close();
        }
    }
}
