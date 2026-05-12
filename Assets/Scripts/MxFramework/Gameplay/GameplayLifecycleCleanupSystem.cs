using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayLifecycleCleanupSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.lifecycle.cleanup";

        private readonly List<GameplayComponentSnapshot<GameplayLifecycleComponent>> _snapshot =
            new List<GameplayComponentSnapshot<GameplayLifecycleComponent>>();

        private readonly List<GameplayEntityId> _pendingDestroy = new List<GameplayEntityId>();

        public GameplayLifecycleCleanupSystem(
            string systemId = DefaultSystemId,
            int priority = 100)
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

            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> lifecycleStore))
                return;

            _snapshot.Clear();
            _pendingDestroy.Clear();
            lifecycleStore.CopyTo(_snapshot);
            for (int i = 0; i < _snapshot.Count; i++)
            {
                GameplayComponentSnapshot<GameplayLifecycleComponent> entry = _snapshot[i];
                if (entry.Component.State == GameplayLifecycleState.PendingDestroy)
                    _pendingDestroy.Add(entry.EntityId);
            }

            _pendingDestroy.Sort();
            for (int i = 0; i < _pendingDestroy.Count; i++)
            {
                GameplayEntityId entityId = _pendingDestroy[i];
                if (!componentWorld.DestroyEntity(entityId))
                    continue;

                context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                    context.Frame,
                    GameplayRuntimeEventType.ComponentEntityDestroyed,
                    commandId: 0,
                    casterEntityId: 0,
                    abilityId: 0,
                    targetEntityId: entityId.Index,
                    failureCode: GameplayAbilityRuntimeFailureCode.None,
                    reason: GameplayLifecycleEvents.PendingDestroyCleanupReason,
                    traceId: string.Empty,
                    componentEntityIndex: entityId.Index,
                    componentEntityGeneration: entityId.Generation));
            }

            _snapshot.Clear();
            _pendingDestroy.Clear();
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
                reason: GameplayLifecycleEvents.MissingComponentWorldReason,
                traceId: string.Empty));
        }
    }
}
