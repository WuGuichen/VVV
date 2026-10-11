using System;

namespace MxFramework.Gameplay
{
    /// <summary>Pure C# gameplay world root for entity lifecycle and stable ticking.</summary>
    public sealed class GameplayWorld
    {
        private readonly RuntimeEntityRegistry _entities;

        public GameplayWorld()
        {
            _entities = new RuntimeEntityRegistry();
        }

        public RuntimeEntityRegistry Entities => _entities;
        public long TickCount { get; private set; }

        public void Register(IRuntimeEntity entity)
        {
            _entities.Register(entity);
        }

        public bool Remove(int entityId)
        {
            return _entities.Remove(entityId);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(double deltaTime)
        {
            if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0d || deltaTime > float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime must be finite, non-negative, and fit in a float.");

            TickCount++;
        }

        public GameplayWorldSnapshot CreateSnapshot()
        {
            return new GameplayWorldSnapshot(TickCount, _entities.CreateSnapshot());
        }
    }
}
