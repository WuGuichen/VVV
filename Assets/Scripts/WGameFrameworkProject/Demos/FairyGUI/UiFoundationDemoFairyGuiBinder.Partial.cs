using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Demo.FairyGui
{
    public sealed partial class UiFoundationDemoFairyGuiBinder
    {
        partial void OnAfterGeneratedBind(Fgui.GComponent root, UiFoundationDemoViewModel viewModel)
        {
            float width = MxFairyGuiStageLayout.ResolveStageWidth(
                root,
                MxFairyGuiStageLayout.DefaultDesignWidth);
            float height = MxFairyGuiStageLayout.ResolveStageHeight(
                root,
                MxFairyGuiStageLayout.DefaultDesignHeight);
            root.SetSize(width, height);
        }
    }
}
