using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiAbilityBindingPanelIds
    {
        public const string ViewIdValue = "ui.character_test.ability.bindings";
        public const string PackageId = "MxCharacterTestAbilityUi";
        public const string ComponentName = "AbilityBindingPanel";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.ability_ui.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.ability_ui.atlas0";
        public const int AbilityChoiceCount = 22;

        public const string PanelFrame = "panelFrame";
        public const string Title = "title";
        public const string Status = "status";
        public const string Message = "message";
        public const string SelectedSlotLabel = "selectedSlotLabel";
        public const string SlotsHeader = "slotsHeader";
        public const string AbilitiesHeader = "abilitiesHeader";
        public const string ActionsHeader = "actionsHeader";
        public const string Clear = "btnClear";
        public const string ResetDefaults = "btnReset";
        public const string Save = "btnSave";
        public const string Cancel = "btnCancel";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);

        public static string SlotButton(int slotNumber)
        {
            return "btnSlot" + slotNumber;
        }

        public static string AbilityButton(int index)
        {
            return "btnAbility" + index;
        }
    }

    public static class CharacterTestFairyGuiAbilityBindingPanelCommandIds
    {
        public const string SelectSlot = "character_test.abilities.select_slot";
        public const string SelectAbility = "character_test.abilities.select_ability";
        public const string ClearSlot = "character_test.abilities.clear_slot";
        public const string ResetDefaults = "character_test.abilities.reset_defaults";
        public const string Save = "character_test.abilities.save";
        public const string Cancel = "character_test.abilities.cancel";
    }
}
