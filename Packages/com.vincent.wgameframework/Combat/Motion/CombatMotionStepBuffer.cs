using System;
using System.Collections.Generic;

namespace MxFramework.Combat.Motion
{
    /// <summary>
    /// Caller-owned scratch for no-GC motion steps. Results that reference this buffer remain valid until
    /// the next step that reuses the same buffer.
    /// </summary>
    public sealed class CombatMotionStepBuffer
    {
        private readonly List<CombatMotionCollision> _collisions;

        public CombatMotionStepBuffer()
            : this(0)
        {
        }

        public CombatMotionStepBuffer(int collisionCapacity)
        {
            _collisions = new List<CombatMotionCollision>(Math.Max(0, collisionCapacity));
        }

        internal IReadOnlyList<CombatMotionCollision> CopyCollisionsFrom(IReadOnlyList<CombatMotionCollision> collisions)
        {
            _collisions.Clear();
            if (collisions == null || collisions.Count == 0)
            {
                return Array.Empty<CombatMotionCollision>();
            }

            EnsureCollisionCapacity(collisions.Count);
            for (int i = 0; i < collisions.Count; i++)
            {
                _collisions.Add(collisions[i]);
            }

            return _collisions;
        }

        private void EnsureCollisionCapacity(int capacity)
        {
            if (_collisions.Capacity < capacity)
            {
                _collisions.Capacity = capacity;
            }
        }
    }
}
