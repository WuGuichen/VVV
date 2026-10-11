using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public static class UiFoundationDemoFairyGuiIds
    {
        public const string ViewIdValue = "ui.foundation.demo";
        public const string PackageId = "MxUiFoundationDemo";
        public const string ComponentName = "UiFoundationDemoPanel";
        public const string PackageBytesResourceId = "ui.fairygui.foundationdemo.fui";
        public const string Atlas0ResourceId = "ui.fairygui.foundationdemo.atlas0";

        public const string Status = "status";
        public const string Primary = "btnPrimary";
        public const string Secondary = "btnSecondary";
        public const string Close = "btnClose";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, ResourceTypeIds.Texture2D, packageId: PackageId);
    }

    public static class UiFoundationDemoCommandIds
    {
        public const string Primary = "ui.foundation.demo.primary";
        public const string Secondary = "ui.foundation.demo.secondary";
        public const string Close = "ui.foundation.demo.close";
    }
}
