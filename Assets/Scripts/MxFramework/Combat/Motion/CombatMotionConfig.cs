using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public readonly struct CombatMotionConfig : IEquatable<CombatMotionConfig>
    {
        public const int DefaultTicksPerSecond = CombatStepConfig.DefaultTicksPerSecond;
        public const int DefaultMaxCollisionIterations = 3;

        public static readonly CombatMotionConfig Default = FromStepConfig(CombatStepConfig.Default);

        public CombatMotionConfig(
            int ticksPerSecond,
            Fix64 moveSpeed,
            Fix64 gravityPerSecond,
            Fix64 jumpSpeed,
            Fix64 maxFallSpeed,
            Fix64 skinWidth,
            Fix64 groundMinNormalY,
            Fix64 ceilingMinNormalY,
            int maxCollisionIterations,
            FixVector3 characterHalfExtents,
            CombatPhysicsLayerMask obstacleLayerMask)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "Motion ticks per second must be positive.");
            }

            if (moveSpeed < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed), "Move speed cannot be negative.");
            }

            if (jumpSpeed < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(jumpSpeed), "Jump speed cannot be negative.");
            }

            if (maxFallSpeed > Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFallSpeed), "Max fall speed must be zero or negative.");
            }

            if (skinWidth < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth), "Skin width cannot be negative.");
            }

            if (groundMinNormalY < Fix64.Zero || groundMinNormalY > Fix64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(groundMinNormalY), "Ground normal threshold must be in range 0..1.");
            }

            if (ceilingMinNormalY < Fix64.Zero || ceilingMinNormalY > Fix64.One)
            {
                throw new ArgumentOutOfRangeException(nameof(ceilingMinNormalY), "Ceiling normal threshold must be in range 0..1.");
            }

            if (maxCollisionIterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCollisionIterations), "Max collision iterations must be positive.");
            }

            if (characterHalfExtents.X <= Fix64.Zero || characterHalfExtents.Y <= Fix64.Zero || characterHalfExtents.Z <= Fix64.Zero)
            {
                throw new ArgumentException("Character half extents must be positive on all axes.", nameof(characterHalfExtents));
            }

            TicksPerSecond = ticksPerSecond;
            MoveSpeed = moveSpeed;
            GravityPerSecond = gravityPerSecond;
            JumpSpeed = jumpSpeed;
            MaxFallSpeed = maxFallSpeed;
            SkinWidth = skinWidth;
            GroundMinNormalY = groundMinNormalY;
            CeilingMinNormalY = ceilingMinNormalY;
            MaxCollisionIterations = maxCollisionIterations;
            CharacterHalfExtents = characterHalfExtents;
            ObstacleLayerMask = obstacleLayerMask;
            CapsuleProxy = CombatMotionCapsuleProxy.FromHalfExtents(characterHalfExtents, skinWidth, layer: 0, obstacleLayerMask);
        }

        public CombatMotionConfig(
            CombatStepConfig stepConfig,
            FixVector3 characterHalfExtents,
            Fix64 moveSpeed,
            Fix64 gravityPerSecond,
            Fix64 jumpSpeed,
            Fix64 maxFallSpeed,
            Fix64 skinWidth,
            Fix64 groundMinNormalY,
            Fix64 ceilingMinNormalY,
            int collisionLayerMask,
            int maxSlideIterations)
            : this(
                stepConfig.TicksPerSecond,
                moveSpeed,
                gravityPerSecond,
                jumpSpeed,
                NormalizeMaxFallSpeed(maxFallSpeed),
                skinWidth,
                groundMinNormalY,
                ceilingMinNormalY,
                maxSlideIterations,
                characterHalfExtents,
                new CombatPhysicsLayerMask(collisionLayerMask))
        {
        }

        public int TicksPerSecond { get; }

        public Fix64 MoveSpeed { get; }

        public Fix64 GravityPerSecond { get; }

        public Fix64 GravityPerStep => GravityPerSecond / Fix64.FromInt(TicksPerSecond);

        public Fix64 JumpSpeed { get; }

        public Fix64 MaxFallSpeed { get; }

        public Fix64 SkinWidth { get; }

        public Fix64 GroundMinNormalY { get; }

        public Fix64 CeilingMinNormalY { get; }

        public int MaxCollisionIterations { get; }

        public FixVector3 CharacterHalfExtents { get; }

        public CombatPhysicsLayerMask ObstacleLayerMask { get; }

        public CombatMotionCapsuleProxy CapsuleProxy { get; }

        public static CombatMotionConfig FromStepConfig(CombatStepConfig stepConfig)
        {
            return new CombatMotionConfig(
                stepConfig.TicksPerSecond,
                Fix64.FromInt(5),
                Fix64.FromInt(-30),
                Fix64.FromInt(10),
                Fix64.FromInt(-50),
                Fix64.FromRatio(1, 1000),
                Fix64.Half,
                Fix64.Half,
                DefaultMaxCollisionIterations,
                new FixVector3(Fix64.FromRatio(4, 10), Fix64.FromRatio(9, 10), Fix64.FromRatio(4, 10)),
                CombatPhysicsLayerMask.All);
        }

        private static Fix64 NormalizeMaxFallSpeed(Fix64 maxFallSpeed)
        {
            return maxFallSpeed > Fix64.Zero ? -maxFallSpeed : maxFallSpeed;
        }

        public bool Equals(CombatMotionConfig other)
        {
            return TicksPerSecond == other.TicksPerSecond
                && MoveSpeed.Equals(other.MoveSpeed)
                && GravityPerSecond.Equals(other.GravityPerSecond)
                && JumpSpeed.Equals(other.JumpSpeed)
                && MaxFallSpeed.Equals(other.MaxFallSpeed)
                && SkinWidth.Equals(other.SkinWidth)
                && GroundMinNormalY.Equals(other.GroundMinNormalY)
                && CeilingMinNormalY.Equals(other.CeilingMinNormalY)
                && MaxCollisionIterations == other.MaxCollisionIterations
                && CharacterHalfExtents.Equals(other.CharacterHalfExtents)
                && ObstacleLayerMask.Equals(other.ObstacleLayerMask)
                && CapsuleProxy.Equals(other.CapsuleProxy);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatMotionConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TicksPerSecond;
                hash = (hash * 397) ^ MoveSpeed.GetHashCode();
                hash = (hash * 397) ^ GravityPerSecond.GetHashCode();
                hash = (hash * 397) ^ JumpSpeed.GetHashCode();
                hash = (hash * 397) ^ MaxFallSpeed.GetHashCode();
                hash = (hash * 397) ^ SkinWidth.GetHashCode();
                hash = (hash * 397) ^ GroundMinNormalY.GetHashCode();
                hash = (hash * 397) ^ CeilingMinNormalY.GetHashCode();
                hash = (hash * 397) ^ MaxCollisionIterations;
                hash = (hash * 397) ^ CharacterHalfExtents.GetHashCode();
                hash = (hash * 397) ^ ObstacleLayerMask.GetHashCode();
                hash = (hash * 397) ^ CapsuleProxy.GetHashCode();
                return hash;
            }
        }
    }
}
