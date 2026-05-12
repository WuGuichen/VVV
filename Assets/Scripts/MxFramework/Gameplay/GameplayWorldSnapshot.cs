using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Copy of world-level registry state at one tick boundary.</summary>
    public sealed class GameplayWorldSnapshot
    {
        private readonly IRuntimeEntity[] _entities;

        public GameplayWorldSnapshot(long tickCount, IReadOnlyList<IRuntimeEntity> entities)
        {
            TickCount = tickCount;
            _entities = Copy(entities);
        }

        public long TickCount { get; }
        public IReadOnlyList<IRuntimeEntity> Entities => _entities;

        private static IRuntimeEntity[] Copy(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<IRuntimeEntity>();

            var copy = new IRuntimeEntity[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                copy[i] = entities[i];

            return copy;
        }
    }
}
