using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class TacticalCardHud
    {
        internal TacticalDeployment EnemyCardDeployment => GetComponent<TacticalCardSliceCoordinator>()?.BattleDeployment;

        internal bool ReadEnemyCard(CharacterTestCharacterEntity entity, CharacterTestEnemyCardPresentation destination)
            => entity != null && entity.CombatState.IsAlive
                && _preparationManager?.RuntimeContext?.Slice?.Services.RuntimeAiPlannerCommands
                    ?.ReadEnemyCard(entity.Id.Value, entity.Control.RuntimeAiPlannerProfileSlot, destination) == true;

        internal CharacterTestCardUiPreviewData GetEnemyCardData(TacticalCardDefinition card, TacticalDeploymentParticipant participant)
        {
            var context = _preparationManager?.RuntimeContext;
            double seconds = context?.SessionBuilder?.ResolveSimulationDeltaTime()
                ?? 1d / System.Math.Max(1, context?.Profile?.TargetFrameRate ?? CharacterTestDeterministicUnits.DefaultTickRate);
            return CharacterTestTacticalCardPresentationProjection.FromDeployment(card, participant,
                CharacterTacticalServices.Player?.Catalog, context?.Slice?.CharacterRuntime?.ActionRuntime,
                BattleTextProvider, seconds);
        }
    }
}
