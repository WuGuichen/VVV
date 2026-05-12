using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Stable component store keyed only by generation-based gameplay entity ids.</summary>
    public sealed class GameplayComponentStore<T> : IGameplayComponentStore where T : struct, IGameplayComponent
    {
        private readonly SortedDictionary<GameplayEntityId, T> _components;

        public GameplayComponentStore()
        {
            _components = new SortedDictionary<GameplayEntityId, T>();
        }

        public int Count => _components.Count;
        public Type ComponentType => typeof(T);

        public bool TryAdd(GameplayEntityId entityId, T component)
        {
            ValidateEntityId(entityId);
            if (_components.ContainsKey(entityId))
                return false;

            _components.Add(entityId, component);
            return true;
        }

        public void Set(GameplayEntityId entityId, T component)
        {
            ValidateEntityId(entityId);
            _components[entityId] = component;
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
            return entityId.IsValid && _components.Remove(entityId);
        }

        public int CopyTo(List<GameplayComponentSnapshot<T>> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int count = 0;
            foreach (KeyValuePair<GameplayEntityId, T> pair in _components)
            {
                output.Add(new GameplayComponentSnapshot<T>(pair.Key, pair.Value));
                count++;
            }

            return count;
        }

        public GameplayComponentSnapshot<T>[] CreateSnapshot()
        {
            if (_components.Count == 0)
                return Array.Empty<GameplayComponentSnapshot<T>>();

            var snapshot = new GameplayComponentSnapshot<T>[_components.Count];
            int index = 0;
            foreach (KeyValuePair<GameplayEntityId, T> pair in _components)
                snapshot[index++] = new GameplayComponentSnapshot<T>(pair.Key, pair.Value);

            return snapshot;
        }

        public void Clear()
        {
            _components.Clear();
        }

        private static void ValidateEntityId(GameplayEntityId entityId)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Gameplay component store requires a valid generation-based GameplayEntityId.", nameof(entityId));
        }
    }
}
