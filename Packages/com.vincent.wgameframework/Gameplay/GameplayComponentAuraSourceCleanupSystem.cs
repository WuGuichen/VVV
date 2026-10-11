using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Removes expired component-native aura sources during resolution.
    /// </summary>
    public sealed class GameplayComponentAuraSourceCleanupSystem : IGameplaySystem
    {
        /// <summary>
        /// Default stable system id used by the aura source cleanup system.
        /// </summary>
        public const string DefaultSystemId = "mxframework.gameplay.aura_source.cleanup";

        private readonly List<GameplayComponentSnapshot<GameplayComponentAuraSourceSetComponent>> _snapshot =
            new List<GameplayComponentSnapshot<GameplayComponentAuraSourceSetComponent>>();

        /// <summary>
        /// Creates an aura source cleanup system.
        /// </summary>
        /// <param name="systemId">Stable system id used by the gameplay system pipeline.</param>
        /// <param name="priority">Resolution phase priority.</param>
        public GameplayComponentAuraSourceCleanupSystem(string systemId = DefaultSystemId, int priority = 40)
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
        /// Executes one cleanup tick for expired aura sources.
        /// </summary>
        /// <param name="context">The gameplay system context for the current frame.</param>
        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueMissingComponentWorld(context);
                return;
            }

            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentAuraSourceSetComponent> auraStore))
                return;

            _snapshot.Clear();
            auraStore.CopyTo(_snapshot);

            for (int i = 0; i < _snapshot.Count; i++)
            {
                GameplayEntityId entityId = _snapshot[i].EntityId;
                GameplayComponentAuraSourceSetComponent current = _snapshot[i].Component;
                GameplayComponentAuraSourceSetComponent updated = current.RemoveExpired(context.Frame, out int[] removedAuraIds);
                if (removedAuraIds.Length == 0)
                    continue;

                if (updated.Count == 0)
                    auraStore.Remove(entityId);
                else
                    auraStore.Set(entityId, updated);
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
                traceId: default));
        }
    }
}
