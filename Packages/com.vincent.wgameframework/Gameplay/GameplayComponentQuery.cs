using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public static class GameplayComponentQuery
    {
        public static int CopyEntities<T>(
            GameplayComponentStore<T> store,
            List<GameplayEntityId> output)
            where T : struct, IGameplayComponent
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int countBefore = output.Count;
            GameplayComponentSnapshot<T>[] snapshot = store.CreateSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
                output.Add(snapshot[i].EntityId);

            return output.Count - countBefore;
        }

        public static int CopyComponents<T>(
            GameplayComponentStore<T> store,
            List<T> output)
            where T : struct, IGameplayComponent
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int countBefore = output.Count;
            GameplayComponentSnapshot<T>[] snapshot = store.CreateSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
                output.Add(snapshot[i].Component);

            return output.Count - countBefore;
        }

        public static int CopyEntries<T>(
            GameplayComponentStore<T> store,
            List<GameplayComponentSnapshot<T>> output)
            where T : struct, IGameplayComponent
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            return store.CopyTo(output);
        }

        public static int CopyPairs<TPrimary, TSecondary>(
            GameplayComponentStore<TPrimary> primaryStore,
            GameplayComponentStore<TSecondary> secondaryStore,
            List<GameplayComponentPair<TPrimary, TSecondary>> output)
            where TPrimary : struct, IGameplayComponent
            where TSecondary : struct, IGameplayComponent
        {
            if (primaryStore == null)
                throw new ArgumentNullException(nameof(primaryStore));
            if (secondaryStore == null)
                throw new ArgumentNullException(nameof(secondaryStore));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int countBefore = output.Count;
            GameplayComponentSnapshot<TPrimary>[] snapshot = primaryStore.CreateSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
            {
                GameplayComponentSnapshot<TPrimary> primary = snapshot[i];
                if (!secondaryStore.TryGet(primary.EntityId, out TSecondary secondary))
                    continue;

                output.Add(new GameplayComponentPair<TPrimary, TSecondary>(
                    primary.EntityId,
                    primary.Component,
                    secondary));
            }

            return output.Count - countBefore;
        }
    }
}
