using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public sealed partial class CombatKinematicMotor
    {
        private Fix64 CalculateSafeFraction(
            CombatMotionCollision collision,
            FixVector3 delta,
            bool hasDeltaLength,
            Fix64 deltaLength)
        {
            if (IsGroundOnlyCollision(collision.Flags))
            {
                return collision.Fraction;
            }

            if (collision.Distance <= Config.SkinWidth)
            {
                return Fix64.Zero;
            }

            Fix64 totalDistance = hasDeltaLength
                ? deltaLength
                : delta.LengthSquared().Sqrt();
            if (totalDistance.IsZero)
            {
                return Fix64.Zero;
            }

            return Fix64.Clamp((collision.Distance - Config.SkinWidth) / totalDistance, Fix64.Zero, collision.Fraction);
        }

        private static bool IsGroundOnlyCollision(CombatMotionCollisionFlags flags)
        {
            return (flags & CombatMotionCollisionFlags.Grounded) != 0
                && (flags & CombatMotionCollisionFlags.Wall) == 0
                && (flags & CombatMotionCollisionFlags.Ceiling) == 0;
        }

        private static FixVector3 ResolveVelocity(FixVector3 velocity, CombatMotionCollisionFlags flags)
        {
            Fix64 x = (flags & CombatMotionCollisionFlags.BlockedX) != 0 ? Fix64.Zero : velocity.X;
            Fix64 y = (flags & (CombatMotionCollisionFlags.Grounded | CombatMotionCollisionFlags.Ceiling)) != 0 ? Fix64.Zero : velocity.Y;
            Fix64 z = (flags & CombatMotionCollisionFlags.BlockedZ) != 0 ? Fix64.Zero : velocity.Z;
            return new FixVector3(x, y, z);
        }

        private static FixVector3 ResolveVelocityFromAppliedDelta(
            FixVector3 desiredVelocity,
            FixVector3 appliedDelta,
            CombatMotionCollisionFlags flags,
            Fix64 tick)
        {
            if (tick <= Fix64.Zero)
                return ResolveVelocity(desiredVelocity, flags);

            FixVector3 deltaVelocity = appliedDelta * tick;
            Fix64 y = (flags & (CombatMotionCollisionFlags.Grounded | CombatMotionCollisionFlags.Ceiling)) != 0
                ? Fix64.Zero
                : deltaVelocity.Y;
            return new FixVector3(deltaVelocity.X, y, deltaVelocity.Z);
        }

        private CombatSurfaceMaterial ResolveContactSurfaceMaterial()
        {
            if (_collisions.Count == 0)
                return CombatSurfaceMaterial.Default;

            bool foundGround = false;
            CombatSurfaceMaterial surfaceMaterial = CombatSurfaceMaterial.Default;
            for (int i = 0; i < _collisions.Count; i++)
            {
                CombatMotionCollision collision = _collisions[i];
                if ((collision.Flags & CombatMotionCollisionFlags.Grounded) != 0
                    && (collision.Flags & CombatMotionCollisionFlags.StepUp) == 0)
                {
                    if (!foundGround || CompareSurfaceMaterialPriority(collision.SurfaceMaterial, surfaceMaterial) < 0)
                    {
                        surfaceMaterial = collision.SurfaceMaterial;
                        foundGround = true;
                    }
                }
            }

            if (foundGround)
                return surfaceMaterial;

            bool foundContact = false;
            for (int i = 0; i < _collisions.Count; i++)
            {
                CombatSurfaceMaterial candidate = _collisions[i].SurfaceMaterial;
                if (!foundContact || IsBetterNonGroundMaterial(candidate, surfaceMaterial))
                {
                    surfaceMaterial = candidate;
                    foundContact = true;
                }
            }

            return foundContact ? surfaceMaterial : CombatSurfaceMaterial.Default;
        }

        private FixVector3 ApplySurfaceBounce(
            FixVector3 velocity,
            FixVector3 impactVelocity,
            Fix64 gravityMagnitude,
            Fix64 minElasticImpactSpeed,
            out FixVector3 reboundNormal)
        {
            reboundNormal = FixVector3.Zero;
            if (_collisions.Count == 0)
                return velocity;

            Fix64 bestReboundSpeed = Fix64.Zero;
            FixVector3 bestReboundNormal = FixVector3.Zero;
            for (int i = 0; i < _collisions.Count; i++)
            {
                CombatMotionCollision collision = _collisions[i];
                if (collision.Normal.IsZero)
                    continue;

                Fix64 speedIntoContact = -impactVelocity.Dot(collision.Normal);
                bool fixedBounce = collision.SurfaceMaterial.FixedBounceHeight > Fix64.Zero;
                if (fixedBounce)
                {
                    if (speedIntoContact < Fix64.Zero)
                        continue;
                }
                else if (speedIntoContact < minElasticImpactSpeed)
                {
                    continue;
                }

                Fix64 reboundSpeed = ResolveSurfaceReboundSpeed(
                    collision.SurfaceMaterial,
                    speedIntoContact,
                    gravityMagnitude);
                if (reboundSpeed > bestReboundSpeed)
                {
                    bestReboundSpeed = reboundSpeed;
                    bestReboundNormal = collision.Normal;
                }
            }

            if (bestReboundSpeed <= Fix64.Zero || bestReboundNormal.IsZero)
                return velocity;

            reboundNormal = bestReboundNormal;
            Fix64 currentNormalSpeed = velocity.Dot(bestReboundNormal);
            FixVector3 tangentialVelocity = velocity - (bestReboundNormal * currentNormalSpeed);
            return tangentialVelocity + (bestReboundNormal * bestReboundSpeed);
        }

        private static CombatMotionCollisionFlags ResolveCollisionFlagsAfterBounce(
            CombatMotionCollisionFlags flags,
            FixVector3 reboundNormal)
        {
            if (reboundNormal.X != Fix64.Zero)
                flags &= ~(CombatMotionCollisionFlags.BlockedX | CombatMotionCollisionFlags.Wall);

            if (reboundNormal.Y > Fix64.Zero)
                flags &= ~(CombatMotionCollisionFlags.BlockedY
                    | CombatMotionCollisionFlags.Grounded
                    | CombatMotionCollisionFlags.GroundSnap);
            else if (reboundNormal.Y < Fix64.Zero)
                flags &= ~(CombatMotionCollisionFlags.BlockedY | CombatMotionCollisionFlags.Ceiling);

            if (reboundNormal.Z != Fix64.Zero)
                flags &= ~(CombatMotionCollisionFlags.BlockedZ | CombatMotionCollisionFlags.Wall);

            return flags;
        }

        private static Fix64 ResolveSurfaceReboundSpeed(
            CombatSurfaceMaterial surfaceMaterial,
            Fix64 speedIntoContact,
            Fix64 gravityMagnitude)
        {
            if (surfaceMaterial.FixedBounceHeight > Fix64.Zero && gravityMagnitude > Fix64.Zero)
                return (Fix64.FromInt(2) * gravityMagnitude * surfaceMaterial.FixedBounceHeight).Sqrt();

            if (surfaceMaterial.Elasticity <= Fix64.Zero)
                return Fix64.Zero;

            return speedIntoContact * surfaceMaterial.Elasticity;
        }

        private static bool IsBetterNonGroundMaterial(
            CombatSurfaceMaterial candidate,
            CombatSurfaceMaterial current)
        {
            if (candidate.FixedBounceHeight != current.FixedBounceHeight)
                return candidate.FixedBounceHeight > current.FixedBounceHeight;

            if (candidate.Elasticity != current.Elasticity)
                return candidate.Elasticity > current.Elasticity;

            if (candidate.Friction != current.Friction)
                return candidate.Friction > current.Friction;

            return (int)candidate.Kind < (int)current.Kind;
        }

        private static FixVector3 ApplySurfaceFriction(
            FixVector3 velocity,
            CombatMotionCollisionFlags flags,
            Fix64 surfaceFriction,
            Fix64 tick,
            bool applyGroundSurfaceFriction)
        {
            if (surfaceFriction <= Fix64.Zero || tick <= Fix64.Zero)
                return velocity;

            Fix64 stepFriction = surfaceFriction / tick;
            if (stepFriction <= Fix64.Zero)
                return velocity;

            if ((flags & CombatMotionCollisionFlags.Grounded) == 0)
                return velocity;

            if (!applyGroundSurfaceFriction)
                return velocity;

            FixVector3 horizontal = new FixVector3(velocity.X, Fix64.Zero, velocity.Z);
            FixVector3 reduced = MoveTowardsZero(horizontal, stepFriction);
            return new FixVector3(reduced.X, velocity.Y, reduced.Z);
        }

        private static FixVector3 MoveTowardsZero(FixVector3 value, Fix64 maxDelta)
        {
            Fix64 lengthSquared = value.LengthSquared();
            if (lengthSquared.IsZero)
                return FixVector3.Zero;

            Fix64 length = lengthSquared.Sqrt();
            if (length <= maxDelta)
                return FixVector3.Zero;

            return value * ((length - maxDelta) / length);
        }

        private static FixVector3 ClampHorizontalDirection(FixVector3 direction)
        {
            var horizontal = new FixVector3(direction.X, Fix64.Zero, direction.Z);
            Fix64 lengthSquared = horizontal.LengthSquared();
            if (lengthSquared <= Fix64.One)
            {
                return horizontal;
            }

            return horizontal / lengthSquared.Sqrt();
        }

        private static FixVector3 ProjectOnPlane(FixVector3 value, FixVector3 normal)
        {
            if (normal.IsZero)
            {
                return FixVector3.Zero;
            }

            return value - normal * value.Dot(normal);
        }

        private static int CompareCollision(CombatMotionCollision left, CombatMotionCollision right)
        {
            int compare = left.Fraction.RawValue.CompareTo(right.Fraction.RawValue);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.TargetEntityId.CompareTo(right.TargetEntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.TargetBodyId.CompareTo(right.TargetBodyId);
            if (compare != 0)
            {
                return compare;
            }

            return left.TargetColliderId.CompareTo(right.TargetColliderId);
        }

        private static int CompareSurfaceMaterialPriority(
            CombatSurfaceMaterial left,
            CombatSurfaceMaterial right)
        {
            bool leftIsDefault = left.Kind == CombatSurfaceMaterialKind.Default;
            bool rightIsDefault = right.Kind == CombatSurfaceMaterialKind.Default;
            if (leftIsDefault != rightIsDefault)
            {
                return leftIsDefault ? 1 : -1;
            }

            int compare = right.FixedBounceHeight.RawValue.CompareTo(left.FixedBounceHeight.RawValue);
            if (compare != 0)
            {
                return compare;
            }

            compare = right.Elasticity.RawValue.CompareTo(left.Elasticity.RawValue);
            if (compare != 0)
            {
                return compare;
            }

            compare = right.Friction.RawValue.CompareTo(left.Friction.RawValue);
            if (compare != 0)
            {
                return compare;
            }

            return ((int)left.Kind).CompareTo((int)right.Kind);
        }

        private FixVector3 SkinVector()
        {
            Fix64 queryPadding = Config.SkinWidth;
            return new FixVector3(queryPadding, queryPadding, queryPadding);
        }
    }
}
