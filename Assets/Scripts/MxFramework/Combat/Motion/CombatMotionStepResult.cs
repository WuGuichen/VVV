using System;
using System.Collections.Generic;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public readonly struct CombatMotionStepResult : IEquatable<CombatMotionStepResult>
    {
        public CombatMotionStepResult(
            CombatMotionState state,
            FixVector3 desiredDelta,
            FixVector3 appliedDelta,
            bool jumpStarted,
            CombatMotionCollisionFlags collisionFlags,
            IReadOnlyList<CombatMotionCollision> collisions)
        {
            State = state;
            DesiredDelta = desiredDelta;
            AppliedDelta = appliedDelta;
            JumpStarted = jumpStarted;
            CollisionFlags = collisionFlags;
            Collisions = collisions ?? Array.Empty<CombatMotionCollision>();
        }

        public CombatMotionState State { get; }

        public FixVector3 DesiredDelta { get; }

        public FixVector3 AppliedDelta { get; }

        public bool JumpStarted { get; }

        public CombatMotionCollisionFlags CollisionFlags { get; }

        public IReadOnlyList<CombatMotionCollision> Collisions { get; }

        public int CollisionCount => Collisions.Count;

        public CombatMotionCollision LastCollision => CollisionCount == 0
            ? default
            : Collisions[CollisionCount - 1];

        public bool Equals(CombatMotionStepResult other)
        {
            if (!State.Equals(other.State)
                || !DesiredDelta.Equals(other.DesiredDelta)
                || !AppliedDelta.Equals(other.AppliedDelta)
                || JumpStarted != other.JumpStarted
                || CollisionFlags != other.CollisionFlags
                || CollisionCount != other.CollisionCount)
            {
                return false;
            }

            for (int i = 0; i < CollisionCount; i++)
            {
                if (!Collisions[i].Equals(other.Collisions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatMotionStepResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = State.GetHashCode();
                hash = (hash * 397) ^ DesiredDelta.GetHashCode();
                hash = (hash * 397) ^ AppliedDelta.GetHashCode();
                hash = (hash * 397) ^ (JumpStarted ? 1 : 0);
                hash = (hash * 397) ^ (int)CollisionFlags;
                for (int i = 0; i < CollisionCount; i++)
                {
                    hash = (hash * 397) ^ Collisions[i].GetHashCode();
                }

                return hash;
            }
        }
    }
}
