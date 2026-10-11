using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public sealed partial class CombatKinematicMotor
    {
        private bool IsGroundSnapEligible(
            CombatMotionState previousState,
            bool jumpStarted,
            FixVector3 desiredDelta,
            FixVector3? impactVelocityOverride,
            CombatMotionStepOptions options,
            CombatMotionCollisionFlags mainCollisionFlags)
        {
            return Config.GroundSnapDistance > Fix64.Zero
                && previousState.Grounded
                && previousState.Velocity.Y <= Fix64.Zero
                && !jumpStarted
                && !options.SuppressGroundSnap
                && desiredDelta.Y <= Fix64.Zero
                && (!impactVelocityOverride.HasValue || impactVelocityOverride.Value.Y <= Fix64.Zero)
                && (mainCollisionFlags & CombatMotionCollisionFlags.Grounded) == 0;
        }

        private bool TryApplyGroundSupport(
            FixVector3 startPosition,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatKinematicMotorCandidateCache candidateCache,
            out FixVector3 supportedPosition,
            out CombatMotionCollision supportCollision)
        {
            supportedPosition = startPosition;
            supportCollision = default;
            if (physicsWorld == null || Config.ObstacleLayerMask.Value == 0)
                return false;

            FixVector3 supportDelta = new FixVector3(
                Fix64.Zero,
                -Config.GroundSnapDistance,
                Fix64.Zero);
            PrepareStepBroadphaseCache(
                startPosition,
                supportDelta,
                allowStepUp: false,
                physicsWorld,
                sourceEntityId,
                candidateCache);
            try
            {
                if (!TryFindFirstCollision(
                        startPosition,
                        supportDelta,
                        physicsWorld,
                        sourceEntityId,
                        out CombatMotionCollision earliest,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    return false;
                }

                CombatMotionCollisionFlags classified = ClassifyCollision(earliest.Normal);
                if ((classified & CombatMotionCollisionFlags.Grounded) == 0
                    || !TryCalculateGroundSnapSafeDistance(
                        earliest.Distance,
                        Config.GroundSnapDistance,
                        Config.SkinWidth,
                        out Fix64 safeDistance))
                {
                    return false;
                }

                CombatMotionCollisionFlags supportFlags = CombatMotionCollisionFlags.Grounded
                    | CombatMotionCollisionFlags.BlockedY
                    | CombatMotionCollisionFlags.GroundSnap;
                supportedPosition = startPosition + new FixVector3(Fix64.Zero, -safeDistance, Fix64.Zero);
                supportCollision = new CombatMotionCollision(
                    earliest.TargetEntityId,
                    earliest.TargetBodyId,
                    earliest.TargetColliderId,
                    earliest.Normal,
                    earliest.Distance,
                    earliest.Fraction,
                    supportFlags,
                    earliest.SurfaceMaterial);
                return true;
            }
            finally
            {
                ClearStepBroadphaseCache();
            }
        }

        internal static bool TryCalculateGroundSnapSafeDistance(
            Fix64 contactDistance,
            Fix64 groundSnapDistance,
            Fix64 skinWidth,
            out Fix64 safeDistance)
        {
            safeDistance = Fix64.Zero;
            if (groundSnapDistance < Fix64.Zero
                || skinWidth < Fix64.Zero
                || contactDistance < Fix64.Zero
                || contactDistance > groundSnapDistance)
            {
                return false;
            }

            safeDistance = Fix64.Max(contactDistance - skinWidth, Fix64.Zero);
            return true;
        }
    }
}
