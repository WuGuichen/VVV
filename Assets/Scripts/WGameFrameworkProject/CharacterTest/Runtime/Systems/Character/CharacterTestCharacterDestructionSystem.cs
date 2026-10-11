using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCharacterDestructionSystem
    {
        private const int DespawnTraceKind = 90874;
        private readonly List<CharacterTestCharacterEntityId> _pendingDestroy =
            new List<CharacterTestCharacterEntityId>(32);

        /// <remarks>NoAlloc after initialization for ordinary cleanup counts within the preallocated pending list.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterUnityViewBridge viewBridge,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            if (world == null)
                return;

            _pendingDestroy.Clear();
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Presence.IsPendingDestroy)
                    _pendingDestroy.Add(entity.Id);
            }

            for (int i = 0; i < _pendingDestroy.Count; i++)
                ApplyDestroy(world, viewBridge, actionRuntime, _pendingDestroy[i], frame, traceRecorder);
        }

        private static void ApplyDestroy(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterUnityViewBridge viewBridge,
            CharacterTestCharacterActionRuntime actionRuntime,
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            CharacterTestCharacterEntity entity = world.FindEntity(entityId.Value);
            if (entity == null)
                return;

            CharacterTestEntityPresenceComponent presence = entity.Presence;
            if (!presence.IsPendingDestroy)
                return;

            entity.SetPresence(presence.WithDestroyed(frame.Value));
            CharacterTestBattleStateSystem.RefreshEntity(world, entity, frame, null);
            traceRecorder?.Lifecycle(
                frame,
                entity.Id.Value,
                "destroy applied sourceEntity="
                + presence.SourceEntityId
                + " trace=" + presence.TraceId);

            actionRuntime?.TerminateAndReleaseEntityAction(
                entity,
                frame,
                CharacterActionLifecycleReason.EntityDespawn,
                new RuntimeTraceIdentity(DespawnTraceKind, entity.Id.Value, frame.Value));
            viewBridge?.ReleaseView(entity.Id);
            world.Remove(entity.Id);
        }
    }
}
