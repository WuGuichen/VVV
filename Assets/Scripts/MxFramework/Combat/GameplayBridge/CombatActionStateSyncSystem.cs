using System;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    public sealed class CombatActionStateSyncSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.bridge.combat.action_sync";

        private readonly CombatEntityGameplayMap _entityMap;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly Func<CombatEntityId, CombatActionState?> _queryActionState;

        public CombatActionStateSyncSystem(
            CombatEntityGameplayMap entityMap,
            GameplayComponentWorld componentWorld,
            Func<CombatEntityId, CombatActionState?> queryActionState,
            string systemId = DefaultSystemId,
            GameplaySystemPhase phase = GameplaySystemPhase.Simulation,
            int priority = 60)
        {
            _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
            _componentWorld = componentWorld ?? throw new ArgumentNullException(nameof(componentWorld));
            _queryActionState = queryActionState ?? throw new ArgumentNullException(nameof(queryActionState));
            SystemId = systemId ?? string.Empty;
            Phase = phase;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase { get; }
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void Tick(GameplaySystemContext context)
        {
            if (!IsEnabled)
                return;

            CombatEntityId[] combatIds = _entityMap.CreateCombatIdSnapshot();
            if (combatIds.Length == 0)
                return;

            GameplayComponentStore<CombatActionStateComponent> store =
                _componentWorld.GetOrCreateStore<CombatActionStateComponent>();

            for (int i = 0; i < combatIds.Length; i++)
            {
                CombatEntityId combatId = combatIds[i];
                CombatActionState? state = _queryActionState(combatId);
                if (!_entityMap.TryGetGameplayId(combatId, out GameplayEntityId gameplayId))
                    continue;

                CombatActionStateComponent component = state.HasValue
                    ? CombatActionStateComponent.Active(state.Value)
                    : CombatActionStateComponent.Inactive();

                store.Set(gameplayId, component);
            }
        }
    }
}
