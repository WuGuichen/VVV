using System;
using System.Collections;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Stable registry for runtime gameplay entities keyed by entity id.</summary>
    public sealed class RuntimeEntityRegistry : IEnumerable<IRuntimeEntity>
    {
        private readonly SortedDictionary<int, IRuntimeEntity> _entities;

        public RuntimeEntityRegistry()
        {
            _entities = new SortedDictionary<int, IRuntimeEntity>();
        }

        public int Count => _entities.Count;

        public void Register(IRuntimeEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (entity.EntityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(entity), "EntityId must be greater than zero.");
            if (_entities.ContainsKey(entity.EntityId))
                throw new InvalidOperationException($"Entity id {entity.EntityId} is already registered.");

            _entities.Add(entity.EntityId, entity);
        }

        public bool Remove(int entityId)
        {
            if (entityId <= 0)
                return false;

            return _entities.Remove(entityId);
        }

        public bool TryGet(int entityId, out IRuntimeEntity entity)
        {
            if (entityId <= 0)
            {
                entity = null;
                return false;
            }

            return _entities.TryGetValue(entityId, out entity);
        }

        public IRuntimeEntity[] CreateSnapshot()
        {
            if (_entities.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var snapshot = new IRuntimeEntity[_entities.Count];
            int index = 0;
            foreach (KeyValuePair<int, IRuntimeEntity> pair in _entities)
                snapshot[index++] = pair.Value;

            return snapshot;
        }

        public IEnumerator<IRuntimeEntity> GetEnumerator()
        {
            IRuntimeEntity[] snapshot = CreateSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
                yield return snapshot[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
