using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Removes expired component-native buffs during resolution and clears modifiers linked to removed buffs.
    /// </summary>
    public sealed class GameplayComponentBuffCleanupSystem : IGameplaySystem
    {
        /// <summary>
        /// Default stable system id used by the buff cleanup system.
        /// </summary>
        public const string DefaultSystemId = "mxframework.gameplay.buff.cleanup";

        private readonly List<GameplayComponentSnapshot<GameplayComponentBuffSetComponent>> _snapshot =
            new List<GameplayComponentSnapshot<GameplayComponentBuffSetComponent>>();

        /// <summary>
        /// Creates a buff cleanup system.
        /// </summary>
        /// <param name="systemId">Stable system id used by the gameplay system pipeline.</param>
        /// <param name="priority">Resolution phase priority.</param>
        public GameplayComponentBuffCleanupSystem(string systemId = DefaultSystemId, int priority = 80)
        {
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        /// <summary>
        /// Gets the stable system id.
        /// </summary>
        public string SystemId { get; }

        /// <summary>
        /// Gets the pipeline phase where cleanup runs.
        /// </summary>
        public GameplaySystemPhase Phase => GameplaySystemPhase.Resolution;

        /// <summary>
        /// Gets the ordering priority within the resolution phase.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Gets whether the system should run when scheduled by the pipeline.
        /// </summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>
        /// Enables or disables the cleanup system.
        /// </summary>
        /// <param name="enabled">Whether the system should run.</param>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <summary>
        /// Executes one cleanup tick for expired buffs and linked modifiers.
        /// </summary>
        /// <param name="context">The gameplay system context for the current frame.</param>
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
                    var removedBuffIdSet = new HashSet<int>(removedBuffIds);
                    GameplayComponentModifierSetComponent updatedModifiers = modifiers.RemoveBySourceBuffIds(removedBuffIdSet);
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
