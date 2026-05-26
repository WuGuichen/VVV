using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public sealed class RuntimeAbilitySliceFairyGuiHudController
    {
        private readonly MxFairyGuiNavigator _navigator;

        public RuntimeAbilitySliceFairyGuiHudController(MxFairyGuiNavigator navigator)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        }

        public MxUiViewId ViewId => RuntimeAbilitySliceFairyGuiHudIds.ViewId;

        public bool IsOpen => _navigator.IsOpen(ViewId);

        public MxUiOpenResult Open(RuntimeAbilitySliceHudViewModel viewModel)
        {
            return _navigator.Open(ViewId, viewModel ?? new RuntimeAbilitySliceHudViewModel());
        }

        public MxUiOpenOperation OpenAsync(RuntimeAbilitySliceHudViewModel viewModel)
        {
            return _navigator.OpenAsync(ViewId, viewModel ?? new RuntimeAbilitySliceHudViewModel());
        }

        public bool Close()
        {
            return _navigator.Close(ViewId);
        }
    }
}
