using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiCombatHudIds
    {
        public const string ViewIdValue = "ui.character_test.combat.hud";
        public const string PackageId = "MxCharacterTestCombatHud";
        public const string ComponentName = "CombatHudPanel";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.combat_hud.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.combat_hud.atlas0";

        public const string PlayerPlate = "playerPlate";
        public const string PlayerName = "playerName";
        public const string PlayerHealthIcon = "playerHealthIcon";
        public const string PlayerHealthTrack = "playerHpTrack";
        public const string PlayerHealthFill = "playerHpFill";
        public const string PlayerHealthText = "playerHpText";
        public const string PlayerScatterPressureIcon = "playerPostureIcon";
        public const string PlayerScatterPressureTrack = "playerPostureTrack";
        public const string PlayerScatterPressureFill = "playerPostureFill";
        public const string PlayerCohesionBreakMarker = "playerPostureBreakMarker";
        public const string PlayerScatterPressureText = "playerPostureText";
        public const string PlayerPostureText = PlayerScatterPressureText;
        public const string PlayerActionText = "playerActionText";

        public const string TargetPlate = "targetPlate";
        public const string TargetName = "targetName";
        public const string TargetStatusText = "targetStatusText";
        public const string TargetHealthTrack = "targetHpTrack";
        public const string TargetHealthFill = "targetHpFill";
        public const string TargetHealthText = "targetHpText";
        public const string TargetScatterPressureTrack = "targetPostureTrack";
        public const string TargetScatterPressureFill = "targetPostureFill";
        public const string TargetCohesionBreakMarker = "targetPostureBreakMarker";
        public const string TargetScatterPressureText = "targetPostureText";
        public const string TargetPostureText = TargetScatterPressureText;
        public const string LockCrosshair = "lockCrosshair";
        public const string FeedbackPanel = "feedbackPanel";
        public const string FeedbackTrim = "feedbackTrim";
        public const string FeedbackText = "feedbackText";
        public const string NetworkLatencyBadgeBackground = "networkLatencyBadgeBg";
        public const string NetworkLatencyText = "networkLatencyText";
        public const string BehaviorCardPanel = "behaviorCardPanel";
        public const string BehaviorCardIcon = "behaviorCardIcon";
        public const string BehaviorCardIconText = "behaviorCardIconText";
        public const string BehaviorCardName = "behaviorCardName";
        public const string BehaviorCardReason = "behaviorCardReason";
        public const int AbilitySlotCount = CharacterTestAbilityHotbarRuntime.SlotCount;
        public const int BuffSlotCount = 8;

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);

        public static string AbilitySlotBackground(int slotNumber)
        {
            return "abilitySlot" + slotNumber + "Bg";
        }

        public static string AbilitySlotSelected(int slotNumber)
        {
            return "abilitySlot" + slotNumber + "Selected";
        }

        public static string AbilitySlotKey(int slotNumber)
        {
            return "abilitySlot" + slotNumber + "Key";
        }

        public static string AbilitySlotName(int slotNumber)
        {
            return "abilitySlot" + slotNumber + "Name";
        }

        public static string AbilitySlotCooldownFill(int slotNumber)
        {
            return "abilitySlot" + slotNumber + "CooldownFill";
        }

        public static string AbilitySlotCooldownText(int slotNumber)
        {
            return "abilitySlot" + slotNumber + "CooldownText";
        }

        public static string PlayerBuffSlotBackground(int slotNumber)
        {
            return "playerBuffSlot" + slotNumber + "Bg";
        }

        public static string PlayerBuffSlotBorder(int slotNumber)
        {
            return "playerBuffSlot" + slotNumber + "Border";
        }

        public static string PlayerBuffSlotLabel(int slotNumber)
        {
            return "playerBuffSlot" + slotNumber + "Label";
        }

        public static string PlayerBuffSlotCooldownFill(int slotNumber)
        {
            return "playerBuffSlot" + slotNumber + "CooldownFill";
        }

        public static string PlayerBuffSlotStack(int slotNumber)
        {
            return "playerBuffSlot" + slotNumber + "Stack";
        }

        public static string PlayerBuffSlotTime(int slotNumber)
        {
            return "playerBuffSlot" + slotNumber + "Time";
        }

        public static string TargetBuffSlotBackground(int slotNumber)
        {
            return "targetBuffSlot" + slotNumber + "Bg";
        }

        public static string TargetBuffSlotBorder(int slotNumber)
        {
            return "targetBuffSlot" + slotNumber + "Border";
        }

        public static string TargetBuffSlotLabel(int slotNumber)
        {
            return "targetBuffSlot" + slotNumber + "Label";
        }

        public static string TargetBuffSlotCooldownFill(int slotNumber)
        {
            return "targetBuffSlot" + slotNumber + "CooldownFill";
        }

        public static string TargetBuffSlotStack(int slotNumber)
        {
            return "targetBuffSlot" + slotNumber + "Stack";
        }

        public static string TargetBuffSlotTime(int slotNumber)
        {
            return "targetBuffSlot" + slotNumber + "Time";
        }
    }
}
