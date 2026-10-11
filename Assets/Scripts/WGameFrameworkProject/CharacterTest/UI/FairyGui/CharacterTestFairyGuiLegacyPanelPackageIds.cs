using MxFramework.Resources;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFairyGuiLegacyPanelPackageIds
    {
        public const string PackageId = "MxCharacterTestHud";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.hud.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.hud.atlas0";

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);
    }
}
