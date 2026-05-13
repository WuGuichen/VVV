using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentBuffCleanupSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.buff.cleanup";

        private readonly List<GameplayComponentSnapshot<GameplayComponentBuffSetComponent>> _snapshot =
            new List<GameplayComponentSnapshot<GameplayComponentBuffSetComponent>>();

        public GameplayComponentBuffCleanupSystem(string systemId = DefaultSystemId, int priority = 80)
        {
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Resolution;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void Tick(GameplaySystemContext context)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueMissingComponentWorld(context);
                return;
            }

            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore))
                return;

            _snapshot.Clear();
            buffStore.CopyTo(_snapshot);
            componentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentModifierSetComponent> modifierStore);

            for (int i = 0; i < _snapshot.Count; i++)
            {
                GameplayEntityId entityId = _snapshot[i].EntityId;
                GameplayComponentBuffSetComponent current = _snapshot[i].Component;
                GameplayComponentBuffSetComponent updated = current.RemoveExpired(context.Frame, out int[] removedBuffIds);
                if (removedBuffIds.Length == 0)
                    continue;

                if (updated.Count == 0)
                    buffStore.Remove(entityId);
                else
                    buffStore.Set(entityId, updated);

                if (modifierStore != null &&
                    modifierStore.TryGet(entityId, out GameplayComponentModifierSetComponent modifiers))
                {
                    GameplayComponentModifierSetComponent updatedModifiers = modifiers.RemoveBySourceBuffIds(removedBuffIds);
                    if (updatedModifiers.Count == 0)
                        modifierStore.Remove(entityId);
                    else
                        modifierStore.Set(entityId, updatedModifiers);
                }
            }

            _snapshot.Clear();
        }

        private static void EnqueueMissingComponentWorld(GameplaySystemContext context)
        {
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.CommandRejected,
                commandId: 0,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: 0,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: GameplayComponentBuffEvents.MissingComponentWorldReason,
                traceId: string.Empty));
        }
    }
}
