using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Composes the Runtime AI Lab setup overlay binder.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder
    {
        private readonly CharacterTestFairyGuiRuntimeAiLabSetupSelectorBinder _setupSelectorBinder;

        internal CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            IMxFairyGuiPopupService popupService,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession rootSession)
        {
            _setupSelectorBinder = new CharacterTestFairyGuiRuntimeAiLabSetupSelectorBinder(
                commandSink,
                viewId,
                popupService,
                rootSession,
                () => false);
        }

        internal bool TooltipsSuppressed => false;

        internal void BindSetupBehaviorGroupPicker(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            _setupSelectorBinder.Bind(root, viewModel);
        }

        internal void ResetForRoot()
        {
            _setupSelectorBinder.ResetForRoot();
        }
    }
}
