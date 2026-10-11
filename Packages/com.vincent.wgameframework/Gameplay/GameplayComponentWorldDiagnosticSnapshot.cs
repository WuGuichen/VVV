using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentWorldDiagnosticSnapshot
    {
        private readonly GameplayEntityId[] _entities;
        private readonly GameplayComponentStoreDiagnosticSnapshot[] _stores;

        public GameplayComponentWorldDiagnosticSnapshot(
            IReadOnlyList<GameplayEntityId> entities,
            IReadOnlyList<GameplayComponentStoreDiagnosticSnapshot> stores,
            RuntimeEventQueueSnapshot eventQueue)
        {
            _entities = CopyEntities(entities);
            _stores = CopyStores(stores);
            EventQueue = eventQueue;
        }

        public IReadOnlyList<GameplayEntityId> Entities => _entities;
        public IReadOnlyList<GameplayComponentStoreDiagnosticSnapshot> Stores => _stores;
        public RuntimeEventQueueSnapshot EventQueue { get; }
        public int AliveEntityCount => _entities.Length;
        public int ComponentStoreCount => _stores.Length;
        public int PendingEventCount => EventQueue.PendingCount;

        private static GameplayEntityId[] CopyEntities(IReadOnlyList<GameplayEntityId> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<GameplayEntityId>();

            var copy = new GameplayEntityId[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                copy[i] = entities[i];

            return copy;
        }

        private static GameplayComponentStoreDiagnosticSnapshot[] CopyStores(
            IReadOnlyList<GameplayComponentStoreDiagnosticSnapshot> stores)
        {
            if (stores == null || stores.Count == 0)
                return Array.Empty<GameplayComponentStoreDiagnosticSnapshot>();

            var copy = new GameplayComponentStoreDiagnosticSnapshot[stores.Count];
            for (int i = 0; i < stores.Count; i++)
                copy[i] = stores[i];

            return copy;
        }
    }
}
