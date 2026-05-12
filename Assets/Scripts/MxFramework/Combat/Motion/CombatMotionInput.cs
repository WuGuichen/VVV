using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public readonly struct CombatMotionInput : IEquatable<CombatMotionInput>
    {
        public static readonly CombatMotionInput None = new CombatMotionInput(FixVector3.Zero, false, Fix64.One);

        public CombatMotionInput(FixVector3 moveDirection, bool jumpPressed)
            : this(moveDirection, jumpPressed, Fix64.One)
        {
        }

        public CombatMotionInput(FixVector3 moveDirection, bool jumpPressed, Fix64 moveSpeedScale)
        {
            if (moveSpeedScale < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeedScale), "Move speed scale cannot be negative.");
            }

            MoveDirection = moveDirection;
            JumpPressed = jumpPressed;
            MoveSpeedScale = moveSpeedScale;
        }

        public FixVector3 MoveDirection { get; }

        public bool JumpPressed { get; }

        public Fix64 MoveSpeedScale { get; }

        public bool Equals(CombatMotionInput other)
        {
            return MoveDirection.Equals(other.MoveDirection)
                && JumpPressed == other.JumpPressed
                && MoveSpeedScale.Equals(other.MoveSpeedScale);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatMotionInput other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MoveDirection.GetHashCode();
                hash = (hash * 397) ^ (JumpPressed ? 1 : 0);
                hash = (hash * 397) ^ MoveSpeedScale.GetHashCode();
                return hash;
            }
        }
    }
}
