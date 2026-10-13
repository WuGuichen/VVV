using System;
using System.Collections.Generic;
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
        private readonly List<CombatEntityId> _combatIds = new List<CombatEntityId>();

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

        /// <remarks>NoAlloc after ID buffer and component store capacity warm-up, provided the query delegate does not allocate. New high-water marks and exception paths may allocate.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            if (!IsEnabled)
                return;

            _combatIds.Clear();
            _entityMap.CopyCombatIds(_combatIds);
            if (_combatIds.Count == 0)
                return;

            GameplayComponentStore<CombatActionStateComponent> store =
                _componentWorld.GetOrCreateStore<CombatActionStateComponent>();

            for (int i = 0; i < _combatIds.Count; i++)
            {
                CombatEntityId combatId = _combatIds[i];
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


