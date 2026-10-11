using System;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class TacticalCardHud
    {
        private bool _cardTrialPresentation;
        private TacticalDeployment _cardTrialDeployment;
        private CharacterTacticalContentCatalog _cardTrialCatalog;
        internal bool IsCardTrialPresentation => _cardTrialPresentation;
        internal int CardTrialSlotCount => _slots.Length;
        internal TacticalStageFlowCoordinator CardTrialStage => _coordinator;

        internal void ConfigureCardTrialPresentation(TacticalDeployment deployment, CharacterTacticalContentCatalog catalog)
        {
            if (deployment?.CardTrial == null || catalog == null) throw new ArgumentException("试验界面需要冻结的部署与正式卡目录。");
            _cardTrialPresentation = true; _cardTrialDeployment = deployment; _cardTrialCatalog = catalog;
            _slots = new TacticalCardInstance[deployment.CardTrial.HandCapacity];
            _root?.AddToClassList("tactical-hidden");
            _fairyBattleView?.Sync(false);
            _preparationState = TacticalPreparationState.Battle; _dirty = true;
        }
    }
}
