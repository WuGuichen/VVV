using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public readonly struct CombatMotionState : IEquatable<CombatMotionState>
    {
        public CombatMotionState(
            CombatFrame frame,
            FixVector3 position,
            FixVector3 velocity,
            bool grounded,
            FixVector3 lastCollisionNormal,
            CombatMotionCollisionFlags collisionFlags)
        {
            Frame = frame;
            Position = position;
            Velocity = velocity;
            Grounded = grounded;
            LastCollisionNormal = lastCollisionNormal;
            CollisionFlags = collisionFlags;
        }

        public CombatFrame Frame { get; }

        public FixVector3 Position { get; }

        public FixVector3 Velocity { get; }

        public bool Grounded { get; }

        public FixVector3 LastCollisionNormal { get; }

        public CombatMotionCollisionFlags CollisionFlags { get; }

        public CombatMotionState WithFrame(CombatFrame frame)
        {
            return new CombatMotionState(frame, Position, Velocity, Grounded, LastCollisionNormal, CollisionFlags);
        }

        public bool Equals(CombatMotionState other)
        {
            return Frame.Equals(other.Frame)
                && Position.Equals(other.Position)
                && Velocity.Equals(other.Velocity)
                && Grounded == other.Grounded
                && LastCollisionNormal.Equals(other.LastCollisionNormal)
                && CollisionFlags == other.CollisionFlags;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatMotionState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ Velocity.GetHashCode();
                hash = (hash * 397) ^ (Grounded ? 1 : 0);
                hash = (hash * 397) ^ LastCollisionNormal.GetHashCode();
                hash = (hash * 397) ^ (int)CollisionFlags;
                return hash;
            }
        }
    }
}
