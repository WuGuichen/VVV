using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiMainMenuBinder
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        partial void OnAfterGeneratedBind(Fgui.GComponent root, CharacterTestFairyGuiMainMenuViewModel viewModel)
        {
            float width = ResolveStageWidth(root);
            float height = ResolveStageHeight(root);
            root.SetSize(width, height);
            bool homepage = BindTacticalHomepage(root, viewModel, width, height);
            // Composer materializes this export; its independent modal host owns visibility.
            var loadingAuthority = root.GetChild("loadingAuthority");
            if (loadingAuthority != null) loadingAuthority.visible = false;
            if (homepage) return;
            if (viewModel?.Status?.IsTacticalSlice == true)
            {
                var phase = (TacticalCard.TacticalPreparationState)viewModel.Status.TacticalPhase;
                var network = root.GetChild(CharacterTestFairyGuiMainMenuIds.Network); if (network != null) network.visible = false;
                var abilities = root.GetChild(CharacterTestFairyGuiMainMenuIds.Abilities); if (abilities != null) abilities.visible = false;
                var decks = root.GetChild(CharacterTestFairyGuiMainMenuIds.BehaviorDeck); if (decks != null) decks.visible = false;
                var restart = root.GetChild(CharacterTestFairyGuiMainMenuIds.Restart)?.asButton;
                if (restart != null) restart.text = phase == TacticalCard.TacticalPreparationState.Home ? "新建进度（保留旧进度）"
                    : phase == TacticalCard.TacticalPreparationState.Completed ? (viewModel.Status.TacticalSettlementSaved ? "相同配置重试" : "重试保存结算") : "返回整备";
                if (restart != null && viewModel.Status.TacticalRewardState == (int)TacticalCard.CharacterTacticalRewardState.Pending)
                    restart.text = "请先领取奖励";
                var close = root.GetChild(CharacterTestFairyGuiMainMenuIds.Close)?.asButton;
                if (close != null) close.text = phase == TacticalCard.TacticalPreparationState.Home
                    || viewModel.Status.TacticalRewardState == (int)TacticalCard.CharacterTacticalRewardState.Pending ? "保存并退出" : "保存并返回首页";
            }
        }

        private static float ResolveStageWidth(Fgui.GComponent root)
        {
            return MxFairyGuiStageLayout.ResolveStageWidth(root, DesignWidth);
        }

        private static float ResolveStageHeight(Fgui.GComponent root)
        {
            return MxFairyGuiStageLayout.ResolveStageHeight(root, DesignHeight);
        }

    }
}
