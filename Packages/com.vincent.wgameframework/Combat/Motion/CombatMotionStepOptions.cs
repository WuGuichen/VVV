using System;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public readonly struct CombatMotionStepOptions
    {
        public CombatMotionStepOptions(bool suppressGroundSnap)
            : this(suppressGroundSnap, Fix64.Zero)
        {
        }

        public CombatMotionStepOptions(
            bool suppressGroundSnap,
            Fix64 wallSlideMinimumHorizontalSpeed)
        {
            if (wallSlideMinimumHorizontalSpeed < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wallSlideMinimumHorizontalSpeed),
                    "Wall-slide minimum horizontal speed cannot be negative.");
            }

            SuppressGroundSnap = suppressGroundSnap;
            WallSlideMinimumHorizontalSpeed = wallSlideMinimumHorizontalSpeed;
        }

        public bool SuppressGroundSnap { get; }

        /// <summary>
        /// Optional locomotion-owned minimum horizontal speed retained while an
        /// otherwise legal tangent is resolved along a wall. Zero preserves the
        /// ordinary physical projection contract used by Root Motion and external motion.
        /// </summary>
        public Fix64 WallSlideMinimumHorizontalSpeed { get; }
    }
}
