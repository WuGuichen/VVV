using System;
using MxFramework.Demo.Story;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public sealed class StoryRuntimeFairyGuiDialogController
    {
        private readonly MxFairyGuiNavigator _navigator;

        public StoryRuntimeFairyGuiDialogController(MxFairyGuiNavigator navigator)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        }

        public MxUiViewId ViewId => StoryRuntimeFairyGuiDialogIds.ViewId;
        public bool IsOpen => _navigator.IsOpen(ViewId);

        public MxUiOpenResult Open(StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            return _navigator.Open(ViewId, viewModel ?? new StoryRuntimeVerticalSliceFairyGuiViewModel());
        }

        public MxUiOpenOperation OpenAsync(StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            return _navigator.OpenAsync(ViewId, viewModel ?? new StoryRuntimeVerticalSliceFairyGuiViewModel());
        }

        public bool Close()
        {
            return _navigator.Close(ViewId);
        }
    }
}
