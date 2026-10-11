using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds
    {
        public const string ViewIdValue = "ui.character_test.runtime_ai.behavior_loadout";
        public const string PackageId = CharacterTestFairyGuiLegacyPanelPackageIds.PackageId;
        public const string ComponentName = "RuntimeAiBehaviorLoadoutPanel";
        public const string PackageBytesResourceId = CharacterTestFairyGuiLegacyPanelPackageIds.PackageBytesResourceId;
        public const string Atlas0ResourceId = CharacterTestFairyGuiLegacyPanelPackageIds.Atlas0ResourceId;
        public const int EntityRowCount = 8;
        public const int LoadoutCardCount = 12;

        public const string PanelFrame = "panelFrame";
        public const string Title = "title";
        public const string Status = "status";
        public const string Message = "message";
        public const string PhaseStatus = "phaseStatus";
        public const string EntitiesHeader = "entitiesHeader";
        public const string LoadoutsHeader = "loadoutsHeader";
        public const string DetailsHeader = "detailsHeader";
        public const string StrategyName = "strategyName";
        public const string StrategyDescription = "strategyDescription";
        public const string PrimaryActionSummary = "primaryActionSummary";
        public const string SuitableScenario = "suitableScenario";
        public const string RiskText = "riskText";
        public const string TagsText = "tagsText";
        public const string BehaviorOrderText = "behaviorOrderText";
        public const string ActionChainText = "actionChainText";
        public const string WarningText = "warningText";
        public const string Apply = "btnApply";
        public const string Refresh = "btnRefresh";
        public const string Close = "btnClose";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey => CharacterTestFairyGuiLegacyPanelPackageIds.PackageBytesKey;
        public static ResourceKey Atlas0Key => CharacterTestFairyGuiLegacyPanelPackageIds.Atlas0Key;

        public static string EntityButton(int index)
        {
            return "btnEntity" + index;
        }

        public static string LoadoutButton(int index)
        {
            return "btnLoadout" + index;
        }
    }

    public static class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds
    {
        public const string SelectEntity = "character_test.runtime_ai_behavior.select_entity";
        public const string SelectLoadout = "character_test.runtime_ai_behavior.select_loadout";
        public const string ApplySelection = "character_test.runtime_ai_behavior.apply_selection";
        public const string Refresh = "character_test.runtime_ai_behavior.refresh";
        public const string Close = "character_test.runtime_ai_behavior.close";
    }
}
