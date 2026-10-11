using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Stable component store keyed only by generation-based gameplay entity ids.</summary>
    public sealed class GameplayComponentStore<T> : IGameplayComponentStore where T : struct, IGameplayComponent
    {
        private readonly SortedDictionary<GameplayEntityId, T> _components;
        private readonly List<GameplayEntityId> _sortedEntityIds;

        public GameplayComponentStore()
        {
            _components = new SortedDictionary<GameplayEntityId, T>();
            _sortedEntityIds = new List<GameplayEntityId>();
        }

        public int Count => _components.Count;
        public Type ComponentType => typeof(T);

        public bool TryAdd(GameplayEntityId entityId, T component)
        {
            ValidateEntityId(entityId);
            if (_components.ContainsKey(entityId))
                return false;

            _components.Add(entityId, component);
            InsertSortedEntityId(entityId);
            return true;
        }

        public void Set(GameplayEntityId entityId, T component)
        {
            ValidateEntityId(entityId);
            bool exists = _components.ContainsKey(entityId);
            _components[entityId] = component;
            if (!exists)
                InsertSortedEntityId(entityId);
        }

        public bool Contains(GameplayEntityId entityId)
        {
            return entityId.IsValid && _components.ContainsKey(entityId);
        }

        public bool TryGet(GameplayEntityId entityId, out T component)
        {
            if (!entityId.IsValid)
            {
                component = default;
                return false;
            }

            return _components.TryGetValue(entityId, out component);
        }

        public bool Remove(GameplayEntityId entityId)
        {
            if (!entityId.IsValid || !_components.Remove(entityId))
                return false;

            RemoveSortedEntityId(entityId);
            return true;
        }

        public int CopyTo(List<GameplayComponentSnapshot<T>> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int count = 0;
            for (int i = 0; i < _sortedEntityIds.Count; i++)
            {
                GameplayEntityId entityId = _sortedEntityIds[i];
                output.Add(new GameplayComponentSnapshot<T>(entityId, _components[entityId]));
                count++;
            }

            return count;
        }

        public GameplayComponentSnapshot<T>[] CreateSnapshot()
        {
            if (_components.Count == 0)
                return Array.Empty<GameplayComponentSnapshot<T>>();

            var snapshot = new GameplayComponentSnapshot<T>[_components.Count];
            for (int i = 0; i < _sortedEntityIds.Count; i++)
            {
                GameplayEntityId entityId = _sortedEntityIds[i];
                snapshot[i] = new GameplayComponentSnapshot<T>(entityId, _components[entityId]);
            }

            return snapshot;
        }

        public void Clear()
        {
            _components.Clear();
            _sortedEntityIds.Clear();
        }

        private static void ValidateEntityId(GameplayEntityId entityId)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Gameplay component store requires a valid generation-based GameplayEntityId.", nameof(entityId));
        }

        private void InsertSortedEntityId(GameplayEntityId entityId)
        {
            int index = _sortedEntityIds.BinarySearch(entityId);
            if (index >= 0)
                return;

            _sortedEntityIds.Insert(~index, entityId);
        }

        private void RemoveSortedEntityId(GameplayEntityId entityId)
        {
            int index = _sortedEntityIds.BinarySearch(entityId);
            if (index >= 0)
                _sortedEntityIds.RemoveAt(index);
        }
    }
}
