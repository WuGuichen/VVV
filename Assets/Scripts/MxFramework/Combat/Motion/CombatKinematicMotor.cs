using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public sealed class CombatKinematicMotor
    {
        private static readonly FixVector3 AxisX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 AxisY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 AxisZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        private static readonly Fix64 LargeNegative = Fix64.FromRaw(long.MinValue / 4);
        private static readonly Fix64 LargePositive = Fix64.FromRaw(long.MaxValue / 4);

        private readonly List<CombatQueryResult> _candidateHits = new List<CombatQueryResult>();
        private readonly List<CombatMotionCollision> _collisions = new List<CombatMotionCollision>();

        public CombatKinematicMotor()
            : this(CombatMotionConfig.Default)
        {
        }

        public CombatKinematicMotor(CombatMotionConfig config)
        {
            Config = config;
        }

        public CombatMotionConfig Config { get; }

        public CombatMotionStepResult Step(CombatMotionState state, CombatMotionInput input)
        {
            return Step(state, input, null, CombatEntityId.None, CombatBodyId.None, false);
        }

        public CombatMotionStepResult Step(
            CombatPhysicsWorld physicsWorld,
            CombatBodyId syncBodyId,
            CombatMotionState state,
            CombatMotionInput input)
        {
            CombatEntityId sourceEntityId = CombatEntityId.None;
            if (physicsWorld != null && physicsWorld.TryGetBody(syncBodyId, out CombatPhysicsBody body))
            {
                sourceEntityId = body.EntityId;
            }

            return Step(state, input, physicsWorld, sourceEntityId, syncBodyId);
        }

        public CombatMotionStepResult Step(
            CombatMotionState state,
            CombatMotionInput input,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatBodyId syncBodyId,
            bool syncBodyPosition = true)
        {
            _collisions.Clear();

            bool jumpStarted = input.JumpPressed && state.Grounded;
            FixVector3 horizontalDirection = ClampHorizontalDirection(input.MoveDirection);
            Fix64 moveScale = input.MoveSpeedScale;
            Fix64 tick = Fix64.FromInt(Config.TicksPerSecond);
            FixVector3 horizontalVelocity = horizontalDirection * (Config.MoveSpeed * moveScale);

            Fix64 verticalVelocity = state.Velocity.Y;
            if (jumpStarted)
            {
                verticalVelocity = Config.JumpSpeed;
            }
            else if (state.Grounded && verticalVelocity < Fix64.Zero)
            {
                verticalVelocity = Fix64.Zero;
            }

            verticalVelocity += Config.GravityPerStep;
            if (verticalVelocity < Config.MaxFallSpeed)
            {
                verticalVelocity = Config.MaxFallSpeed;
            }

            FixVector3 desiredVelocity = new FixVector3(horizontalVelocity.X, verticalVelocity, horizontalVelocity.Z);
            FixVector3 desiredDelta = desiredVelocity / tick;
            FixVector3 startPosition = state.Position;
            FixVector3 resolvedPosition = ResolveCollisions(
                state.Frame,
                startPosition,
                desiredDelta,
                physicsWorld,
                sourceEntityId,
                out CombatMotionCollisionFlags collisionFlags);

            FixVector3 appliedDelta = resolvedPosition - startPosition;
            bool grounded = (collisionFlags & CombatMotionCollisionFlags.Grounded) != 0;
            FixVector3 resolvedVelocity = ResolveVelocity(desiredVelocity, collisionFlags);
            FixVector3 lastNormal = _collisions.Count == 0 ? FixVector3.Zero : _collisions[_collisions.Count - 1].Normal;
            CombatMotionState nextState = new CombatMotionState(
                state.Frame.Next(),
                resolvedPosition,
                resolvedVelocity,
                grounded,
                lastNormal,
                collisionFlags);

            if (syncBodyPosition && physicsWorld != null && !syncBodyId.IsNone)
            {
                physicsWorld.SetBodyPosition(syncBodyId, resolvedPosition);
            }

            CombatMotionCollision[] collisions = _collisions.Count == 0
                ? Array.Empty<CombatMotionCollision>()
                : _collisions.ToArray();
            return new CombatMotionStepResult(
                nextState,
                desiredDelta,
                appliedDelta,
                jumpStarted,
                collisionFlags,
                collisions);
        }

        public void GetCharacterAabb(FixVector3 position, out FixVector3 min, out FixVector3 max)
        {
            Config.CapsuleProxy.GetBounds(position, out min, out max);
        }

        private FixVector3 ResolveCollisions(
            CombatFrame frame,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            out CombatMotionCollisionFlags collisionFlags)
        {
            collisionFlags = CombatMotionCollisionFlags.None;
            if (physicsWorld == null || desiredDelta.IsZero)
            {
                return startPosition + desiredDelta;
            }

            FixVector3 position = startPosition;
            FixVector3 remaining = desiredDelta;
            for (int iteration = 0; iteration < Config.MaxCollisionIterations; iteration++)
            {
                if (remaining.IsZero)
                {
                    return position;
                }

                if (!TryFindFirstCollision(frame, iteration, position, remaining, physicsWorld, sourceEntityId, out CombatMotionCollision collision))
                {
                    return position + remaining;
                }

                CombatMotionCollisionFlags frameFlags = ClassifyCollision(collision.Normal);
                collisionFlags |= frameFlags;
                _collisions.Add(new CombatMotionCollision(
                    collision.TargetEntityId,
                    collision.TargetBodyId,
                    collision.TargetColliderId,
                    collision.Normal,
                    collision.Distance,
                    collision.Fraction,
                    frameFlags));

                Fix64 safeFraction = CalculateSafeFraction(collision, remaining);
                FixVector3 safeDelta = remaining * safeFraction;
                position += safeDelta;

                Fix64 remainingFraction = Fix64.One - collision.Fraction;
                if (remainingFraction <= Fix64.Zero)
                {
                    return position;
                }

                FixVector3 afterHit = remaining * remainingFraction;
                remaining = ProjectOnPlane(afterHit, collision.Normal);
            }

            collisionFlags |= CombatMotionCollisionFlags.IterationLimit;
            return position;
        }

        private bool TryFindFirstCollision(
            CombatFrame frame,
            int iteration,
            FixVector3 position,
            FixVector3 delta,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            out CombatMotionCollision collision)
        {
            Config.CapsuleProxy.GetBounds(position, out FixVector3 startMin, out FixVector3 startMax);
            FixVector3 endMin = startMin + delta;
            FixVector3 endMax = startMax + delta;
            FixVector3 queryMin = Min(startMin, endMin) - SkinVector();
            FixVector3 queryMax = Max(startMax, endMax) + SkinVector();
            var header = new CombatQueryHeader(
                CreateQueryId(frame, iteration),
                CombatQueryKind.Aabb,
                sourceEntityId,
                traceId: 0,
                actionId: 0,
                sourceOrder: iteration,
                Config.ObstacleLayerMask);
            var query = CombatPhysicsQuery.From(new CombatAabbQuery(header, queryMin, queryMax));

            _candidateHits.Clear();
            physicsWorld.Query(query, _candidateHits);

            bool found = false;
            CombatMotionCollision best = default;
            for (int i = 0; i < _candidateHits.Count; i++)
            {
                CombatQueryResult hit = _candidateHits[i];
                if (!physicsWorld.TryGetBody(hit.TargetBodyId, out CombatPhysicsBody targetBody)
                    || !physicsWorld.TryGetAabbCollider(hit.TargetBodyId, hit.TargetColliderId, out CombatPhysicsAabbCollider targetCollider))
                {
                    continue;
                }

                FixVector3 obstacleMin = targetCollider.GetWorldMin(targetBody.Position);
                FixVector3 obstacleMax = targetCollider.GetWorldMax(targetBody.Position);
                if (!TrySweepCapsule(
                    position,
                    delta,
                    obstacleMin,
                    obstacleMax,
                    out Fix64 fraction,
                    out Fix64 distance,
                    out FixVector3 normal))
                {
                    continue;
                }

                var candidate = new CombatMotionCollision(
                    targetBody.EntityId,
                    targetBody.BodyId,
                    targetCollider.ColliderId,
                    normal,
                    distance,
                    fraction,
                    ClassifyCollision(normal));
                if (!found || CompareCollision(candidate, best) < 0)
                {
                    best = candidate;
                    found = true;
                }
            }

            collision = best;
            return found;
        }

        private bool TrySweepCapsule(
            FixVector3 position,
            FixVector3 delta,
            FixVector3 obstacleMin,
            FixVector3 obstacleMax,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 normal)
        {
            CombatMotionCapsuleProxy capsule = Config.CapsuleProxy;
            capsule.GetSegment(position, out FixVector3 pointA, out FixVector3 pointB);
            FixVector3 movingMin = Min(pointA, pointB);
            FixVector3 movingMax = Max(pointA, pointB);
            FixVector3 radius = new FixVector3(capsule.Radius, capsule.Radius, capsule.Radius);
            return TrySweepAabb(
                movingMin,
                movingMax,
                delta,
                obstacleMin - radius,
                obstacleMax + radius,
                out fraction,
                out distance,
                out normal);
        }

        private bool TrySweepAabb(
            FixVector3 movingMin,
            FixVector3 movingMax,
            FixVector3 delta,
            FixVector3 obstacleMin,
            FixVector3 obstacleMax,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 normal)
        {
            if (StrictlyOverlaps(movingMin, movingMax, obstacleMin, obstacleMax))
            {
                FixVector3 initialNormal = GetInitialOverlapNormal(movingMin, movingMax, obstacleMin, obstacleMax);
                if (delta.Dot(initialNormal) > Fix64.Zero)
                {
                    return NoSweep(out fraction, out distance, out normal);
                }

                fraction = Fix64.Zero;
                distance = Fix64.Zero;
                normal = initialNormal;
                return true;
            }

            Fix64 entryX;
            Fix64 exitX;
            if (!CalculateSweepAxis(movingMin.X, movingMax.X, obstacleMin.X, obstacleMax.X, delta.X, out entryX, out exitX))
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            Fix64 entryY;
            Fix64 exitY;
            if (!CalculateSweepAxis(movingMin.Y, movingMax.Y, obstacleMin.Y, obstacleMax.Y, delta.Y, out entryY, out exitY))
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            Fix64 entryZ;
            Fix64 exitZ;
            if (!CalculateSweepAxis(movingMin.Z, movingMax.Z, obstacleMin.Z, obstacleMax.Z, delta.Z, out entryZ, out exitZ))
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            Fix64 entry = Fix64.Max(entryX, Fix64.Max(entryY, entryZ));
            Fix64 exit = Fix64.Min(exitX, Fix64.Min(exitY, exitZ));
            if (entry > exit || entry < Fix64.Zero || entry > Fix64.One)
            {
                return NoSweep(out fraction, out distance, out normal);
            }

            fraction = entry;
            distance = delta.LengthSquared().IsZero ? Fix64.Zero : delta.LengthSquared().Sqrt() * fraction;
            normal = GetSweepNormal(entry, entryX, entryY, entryZ, delta);
            return true;
        }

        private bool CalculateSweepAxis(
            Fix64 movingMin,
            Fix64 movingMax,
            Fix64 obstacleMin,
            Fix64 obstacleMax,
            Fix64 delta,
            out Fix64 entry,
            out Fix64 exit)
        {
            if (delta.IsZero)
            {
                if (movingMax < obstacleMin || movingMin > obstacleMax)
                {
                    entry = Fix64.Zero;
                    exit = Fix64.Zero;
                    return false;
                }

                entry = LargeNegative;
                exit = LargePositive;
                return true;
            }

            if (delta > Fix64.Zero)
            {
                entry = (obstacleMin - movingMax) / delta;
                exit = (obstacleMax - movingMin) / delta;
            }
            else
            {
                entry = (obstacleMax - movingMin) / delta;
                exit = (obstacleMin - movingMax) / delta;
            }

            return true;
        }

        private static bool NoSweep(out Fix64 fraction, out Fix64 distance, out FixVector3 normal)
        {
            fraction = Fix64.Zero;
            distance = Fix64.Zero;
            normal = FixVector3.Zero;
            return false;
        }

        private Fix64 CalculateSafeFraction(CombatMotionCollision collision, FixVector3 delta)
        {
            if (collision.Distance <= Config.SkinWidth)
            {
                return Fix64.Zero;
            }

            Fix64 totalDistance = delta.LengthSquared().Sqrt();
            if (totalDistance.IsZero)
            {
                return Fix64.Zero;
            }

            return Fix64.Clamp((collision.Distance - Config.SkinWidth) / totalDistance, Fix64.Zero, collision.Fraction);
        }

        private CombatMotionCollisionFlags ClassifyCollision(FixVector3 normal)
        {
            CombatMotionCollisionFlags flags = CombatMotionCollisionFlags.None;
            if (!normal.X.IsZero)
            {
                flags |= CombatMotionCollisionFlags.BlockedX | CombatMotionCollisionFlags.Wall;
            }

            if (!normal.Y.IsZero)
            {
                flags |= CombatMotionCollisionFlags.BlockedY;
                if (normal.Y >= Config.GroundMinNormalY)
                {
                    flags |= CombatMotionCollisionFlags.Grounded;
                }
                else if (normal.Y <= -Config.CeilingMinNormalY)
                {
                    flags |= CombatMotionCollisionFlags.Ceiling;
                }
            }

            if (!normal.Z.IsZero)
            {
                flags |= CombatMotionCollisionFlags.BlockedZ | CombatMotionCollisionFlags.Wall;
            }

            return flags;
        }

        private static FixVector3 ResolveVelocity(FixVector3 velocity, CombatMotionCollisionFlags flags)
        {
            Fix64 x = (flags & CombatMotionCollisionFlags.BlockedX) != 0 ? Fix64.Zero : velocity.X;
            Fix64 y = (flags & (CombatMotionCollisionFlags.Grounded | CombatMotionCollisionFlags.Ceiling)) != 0 ? Fix64.Zero : velocity.Y;
            Fix64 z = (flags & CombatMotionCollisionFlags.BlockedZ) != 0 ? Fix64.Zero : velocity.Z;
            return new FixVector3(x, y, z);
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

        private static FixVector3 GetSweepNormal(Fix64 entry, Fix64 entryX, Fix64 entryY, Fix64 entryZ, FixVector3 delta)
        {
            if (entry == entryX && !delta.X.IsZero)
            {
                return delta.X > Fix64.Zero ? -AxisX : AxisX;
            }

            if (entry == entryY && !delta.Y.IsZero)
            {
                return delta.Y > Fix64.Zero ? -AxisY : AxisY;
            }

            if (entry == entryZ && !delta.Z.IsZero)
            {
                return delta.Z > Fix64.Zero ? -AxisZ : AxisZ;
            }

            return FixVector3.Zero;
        }

        private static FixVector3 GetInitialOverlapNormal(
            FixVector3 movingMin,
            FixVector3 movingMax,
            FixVector3 obstacleMin,
            FixVector3 obstacleMax)
        {
            Fix64 movingCenterX = (movingMin.X + movingMax.X) / Fix64.FromInt(2);
            Fix64 movingCenterY = (movingMin.Y + movingMax.Y) / Fix64.FromInt(2);
            Fix64 movingCenterZ = (movingMin.Z + movingMax.Z) / Fix64.FromInt(2);
            Fix64 obstacleCenterX = (obstacleMin.X + obstacleMax.X) / Fix64.FromInt(2);
            Fix64 obstacleCenterY = (obstacleMin.Y + obstacleMax.Y) / Fix64.FromInt(2);
            Fix64 obstacleCenterZ = (obstacleMin.Z + obstacleMax.Z) / Fix64.FromInt(2);

            Fix64 overlapX = Fix64.Min(movingMax.X, obstacleMax.X) - Fix64.Max(movingMin.X, obstacleMin.X);
            Fix64 overlapY = Fix64.Min(movingMax.Y, obstacleMax.Y) - Fix64.Max(movingMin.Y, obstacleMin.Y);
            Fix64 overlapZ = Fix64.Min(movingMax.Z, obstacleMax.Z) - Fix64.Max(movingMin.Z, obstacleMin.Z);

            if (overlapY <= overlapX && overlapY <= overlapZ)
            {
                return movingCenterY >= obstacleCenterY ? AxisY : -AxisY;
            }

            if (overlapX <= overlapZ)
            {
                return movingCenterX >= obstacleCenterX ? AxisX : -AxisX;
            }

            return movingCenterZ >= obstacleCenterZ ? AxisZ : -AxisZ;
        }

        private static bool StrictlyOverlaps(FixVector3 leftMin, FixVector3 leftMax, FixVector3 rightMin, FixVector3 rightMax)
        {
            return leftMin.X < rightMax.X
                && leftMax.X > rightMin.X
                && leftMin.Y < rightMax.Y
                && leftMax.Y > rightMin.Y
                && leftMin.Z < rightMax.Z
                && leftMax.Z > rightMin.Z;
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

        private static int CreateQueryId(CombatFrame frame, int iteration)
        {
            return checked(((frame.Value & 0x0FFFFFFF) * 16) + iteration);
        }

        private FixVector3 SkinVector()
        {
            Fix64 queryPadding = Config.CapsuleProxy.Radius + Config.SkinWidth;
            return new FixVector3(queryPadding, queryPadding, queryPadding);
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Min(left.X, right.X),
                Fix64.Min(left.Y, right.Y),
                Fix64.Min(left.Z, right.Z));
        }

        private static FixVector3 Max(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Max(left.X, right.X),
                Fix64.Max(left.Y, right.Y),
                Fix64.Max(left.Z, right.Z));
        }
    }
}
