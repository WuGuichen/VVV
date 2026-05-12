using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public static class GameplayComponentTargetCandidates
    {
        public static void CopyFromWorld(
            GameplayComponentWorld world,
            IList<GameplayComponentTargetCandidate> output)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            GameplayEntityId[] entities = world.CreateEntitySnapshot();
            for (int i = 0; i < entities.Length; i++)
            {
                if (TryCreateFromWorld(world, entities[i], out GameplayComponentTargetCandidate candidate))
                    output.Add(candidate);
            }
        }

        public static bool TryCreateFromWorld(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            out GameplayComponentTargetCandidate candidate)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (!entityId.IsValid || !world.IsAlive(entityId))
            {
                candidate = default;
                return false;
            }

            int teamId = 0;
            if (world.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams) &&
                teams.TryGet(entityId, out GameplayTeamComponent team))
            {
                teamId = team.TeamId;
            }

            GameplayLifecycleState lifecycleState = GameplayLifecycleState.None;
            if (world.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> lifecycles) &&
                lifecycles.TryGet(entityId, out GameplayLifecycleComponent lifecycle))
            {
                lifecycleState = lifecycle.State;
            }

            int[] tags = Array.Empty<int>();
            if (world.TryGetStore(out GameplayComponentStore<GameplayTagComponent> tagStore) &&
                tagStore.TryGet(entityId, out GameplayTagComponent tagComponent))
            {
                tags = ToIntArray(tagComponent.ToArray());
            }

            int[] statuses = Array.Empty<int>();
            if (world.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statusStore) &&
                statusStore.TryGet(entityId, out GameplayStatusComponent statusComponent))
            {
                statuses = ToIntArray(statusComponent.ToArray());
            }

            candidate = new GameplayComponentTargetCandidate(
                entityId,
                teamId,
                lifecycleState,
                tags,
                statuses);
            return true;
        }

        private static int[] ToIntArray(GameplayTagId[] ids)
        {
            if (ids == null || ids.Length == 0)
                return Array.Empty<int>();

            var values = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                values[i] = ids[i].Value;
            return values;
        }

        private static int[] ToIntArray(GameplayStatusId[] ids)
        {
            if (ids == null || ids.Length == 0)
                return Array.Empty<int>();

            var values = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                values[i] = ids[i].Value;
            return values;
        }
    }
}
