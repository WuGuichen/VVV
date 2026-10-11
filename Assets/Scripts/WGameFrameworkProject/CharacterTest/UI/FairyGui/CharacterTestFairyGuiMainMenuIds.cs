using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiMainMenuIds
    {
        public const string ViewIdValue = "ui.character_test.main.menu";
        public const string PackageId = "MxCharacterTestUi";
        public const string ComponentName = "MainMenuPanel";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.player_ui.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.player_ui.atlas0";

        public const string Overlay = "overlay";
        public const string PanelFrame = "panelFrame";
        public const string PanelTrim = "panelTrim";
        public const string Title = "title";
        public const string Status = "status";
        public const string Message = "message";
        public const string Start = "btnStart";
        public const string Network = "btnNetwork";
        public const string Abilities = "btnAbilities";
        public const string BehaviorDeck = "btnBehaviorDeck";
        public const string Settings = "btnSettings";
        public const string Restart = "btnRestart";
        public const string Close = "btnClose";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);
    }

    public static class CharacterTestFairyGuiMainMenuCommandIds
    {
        public const string Start = "character_test.main_menu.start";
        public const string Network = "character_test.main_menu.network";
        public const string AbilitiesToggle = "character_test.main_menu.abilities";
        public const string BehaviorDeckToggle = "character_test.main_menu.behavior_deck";
        public const string SettingsToggle = "character_test.main_menu.settings";
        public const string RestartOffline = "character_test.main_menu.restart_offline";
        public const string Close = "character_test.main_menu.close";
    }
}
