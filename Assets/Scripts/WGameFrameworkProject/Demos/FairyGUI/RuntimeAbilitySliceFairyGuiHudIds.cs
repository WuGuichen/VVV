using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public static class RuntimeAbilitySliceFairyGuiHudIds
    {
        public const string ViewIdValue = "ui.runtimehud.main";
        public const string PackageId = "MxRuntimeHud";
        public const string ComponentName = "RuntimeHudPanel";
        public const string PackageBytesResourceId = "ui.fairygui.runtimehud.fui";

        public const string Title = "title";
        public const string Mode = "mode";
        public const string PlayerName = "playerName";
        public const string PlayerHp = "playerHp";
        public const string EnemyName = "enemyName";
        public const string EnemyHp = "enemyHp";
        public const string RecentAction = "recentAction";
        public const string Strike = "btnStrike";
        public const string Reset = "btnReset";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);
    }
}
