using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
        private static bool TryShapeHitTarget(
            CombatPhysicsShape queryShape,
            CombatPhysicsShape targetShape,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            switch (targetShape.Kind)
            {
                case CombatPhysicsShapeKind.Aabb:
                    return TryShapeHitAabb(queryShape, targetShape.Min, targetShape.Max, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Sphere:
                    return TryShapeHitSphere(queryShape, targetShape.Center, targetShape.Radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Capsule:
                    return TryShapeHitCapsule(queryShape, targetShape.PointA, targetShape.PointB, targetShape.Radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Obb:
                    return TryShapeHitObb(queryShape, targetShape, out distance, out point, out normal);
                default:
                    throw new NotSupportedException("Combat physics target shape kind is not supported.");
            }
        }

        private static bool TryShapeHitObb(
            CombatPhysicsShape shape,
            CombatPhysicsShape target,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                {
                    FixVector3 localOrigin = ToObbLocal(shape.Origin, target);
                    FixVector3 localDirection = DirectionToObbLocal(shape.Direction, target);
                    if (!TryRaycastAabb(localOrigin, localDirection, shape.Length, -target.HalfExtents, target.HalfExtents, out distance, out FixVector3 localPoint, out FixVector3 localNormal))
                    { point = FixVector3.Zero; normal = FixVector3.Zero; return false; }
                    point = FromObbLocal(localPoint, target);
                    normal = DirectionFromObbLocal(localNormal, target);
                    return true;
                }
                case CombatPhysicsShapeKind.Sphere:
                {
                    point = CombatPhysicsObbMath.ClosestPoint(shape.Center, target.Center, target.HalfExtents, target.AxisX, target.AxisY, target.AxisZ);
                    FixVector3 delta = shape.Center - point;
                    Fix64 squared = delta.LengthSquared();
                    if (squared > shape.Radius * shape.Radius) { distance = Fix64.Zero; point = FixVector3.Zero; normal = FixVector3.Zero; return false; }
                    distance = DistanceFromSquared(squared);
                    normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                    return true;
                }
                case CombatPhysicsShapeKind.Capsule:
                {
                    FixVector3 localA = ToObbLocal(shape.PointA, target);
                    FixVector3 localB = ToObbLocal(shape.PointB, target);
                    FixVector3 segment = localB - localA;
                    bool hit = TryCapsuleAabb(localA, localB, segment, segment.LengthSquared(), shape.Radius, shape.Radius * shape.Radius, -target.HalfExtents, target.HalfExtents, out distance, out FixVector3 localPoint, out FixVector3 localNormal);
                    if (!hit) { point = FixVector3.Zero; normal = FixVector3.Zero; return false; }
                    point = FromObbLocal(localPoint, target);
                    normal = DirectionFromObbLocal(localNormal, target);
                    return true;
                }
                case CombatPhysicsShapeKind.Aabb:
                {
                    if (!CombatPhysicsObbMath.OverlapsAabb(target.Center, target.HalfExtents, target.AxisX, target.AxisY, target.AxisZ, shape.Min, shape.Max))
                    { distance = Fix64.Zero; point = FixVector3.Zero; normal = FixVector3.Zero; return false; }
                    FixVector3 queryCenter = (shape.Min + shape.Max) / Fix64.FromInt(2);
                    point = CombatPhysicsObbMath.ClosestPoint(queryCenter, target.Center, target.HalfExtents, target.AxisX, target.AxisY, target.AxisZ);
                    normal = (queryCenter - point).TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                    distance = Fix64.Zero;
                    return true;
                }
                case CombatPhysicsShapeKind.Sector:
                {
                    point = CombatPhysicsObbMath.ClosestPoint(shape.Origin, target.Center, target.HalfExtents, target.AxisX, target.AxisY, target.AxisZ);
                    FixVector3 toPoint = point - shape.Origin;
                    Fix64 squared = toPoint.LengthSquared();
                    if (squared > shape.Radius * shape.Radius) { distance = Fix64.Zero; point = FixVector3.Zero; normal = FixVector3.Zero; return false; }
                    if (!squared.IsZero && shape.Direction.Dot(toPoint / squared.Sqrt()) < shape.MinDot) { distance = Fix64.Zero; point = FixVector3.Zero; normal = FixVector3.Zero; return false; }
                    distance = DistanceFromSquared(squared);
                    normal = toPoint.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                    return true;
                }
                case CombatPhysicsShapeKind.Obb:
                {
                    if (!CombatPhysicsObbMath.OverlapsObb(shape.Center, shape.HalfExtents, shape.AxisX, shape.AxisY, shape.AxisZ, target.Center, target.HalfExtents, target.AxisX, target.AxisY, target.AxisZ))
                    { distance = Fix64.Zero; point = FixVector3.Zero; normal = FixVector3.Zero; return false; }
                    point = CombatPhysicsObbMath.ClosestPoint(shape.Center, target.Center, target.HalfExtents, target.AxisX, target.AxisY, target.AxisZ);
                    normal = (shape.Center - point).TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                    distance = Fix64.Zero;
                    return true;
                }
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }
        }

        private static bool TryShapeHitAabb(
            CombatPhysicsShape shape,
            FixVector3 min,
            FixVector3 max,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                {
                    return TryRaycastAabb(
                        shape.Origin,
                        shape.Direction,
                        shape.Length,
                        min,
                        max,
                        out distance,
                        out point,
                        out normal);
                }
                case CombatPhysicsShapeKind.Sphere:
                {
                    Fix64 radiusSquared = shape.Radius * shape.Radius;
                    point = ClosestPointOnAabb(shape.Center, min, max);
                    FixVector3 delta = shape.Center - point;
                    Fix64 distanceSquared = delta.LengthSquared();
                    if (distanceSquared <= radiusSquared)
                    {
                        distance = DistanceFromSquared(distanceSquared);
                        normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                        return true;
                    }

                    break;
                }
                case CombatPhysicsShapeKind.Capsule:
                {
                    Fix64 radiusSquared = shape.Radius * shape.Radius;
                    FixVector3 segment = shape.PointB - shape.PointA;
                    Fix64 segmentLengthSquared = segment.LengthSquared();
                    return TryCapsuleAabb(
                        shape.PointA,
                        shape.PointB,
                        segment,
                        segmentLengthSquared,
                        shape.Radius,
                        radiusSquared,
                        min,
                        max,
                        out distance,
                        out point,
                        out normal);
                }
                case CombatPhysicsShapeKind.Aabb:
                {
                    if (Overlaps(shape.Min, shape.Max, min, max))
                    {
                        distance = Fix64.Zero;
                        point = GetOverlapCenter(shape.Min, shape.Max, min, max);
                        normal = FixVector3.Zero;
                        return true;
                    }

                    break;
                }
                case CombatPhysicsShapeKind.Sector:
                {
                    point = ClosestPointOnAabb(shape.Origin, min, max);
                    FixVector3 toPoint = point - shape.Origin;
                    Fix64 distanceSquared = toPoint.LengthSquared();
                    if (distanceSquared > shape.Radius * shape.Radius)
                    {
                        break;
                    }

                    if (!distanceSquared.IsZero)
                    {
                        FixVector3 toPointDirection = toPoint / distanceSquared.Sqrt();
                        if (shape.Direction.Dot(toPointDirection) < shape.MinDot)
                        {
                            break;
                        }
                    }

                    distance = DistanceFromSquared(distanceSquared);
                    normal = toPoint.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                    return true;
                }
                case CombatPhysicsShapeKind.Obb:
                {
                    if (CombatPhysicsObbMath.OverlapsAabb(
                        shape.Center,
                        shape.HalfExtents,
                        shape.AxisX,
                        shape.AxisY,
                        shape.AxisZ,
                        min,
                        max))
                    {
                        distance = Fix64.Zero;
                        point = ClosestPointOnAabb(shape.Center, min, max);
                        FixVector3 delta = point - shape.Center;
                        normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                        return true;
                    }

                    break;
                }
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }

            distance = Fix64.Zero;
            point = FixVector3.Zero;
            normal = FixVector3.Zero;
            return false;
        }

        private static bool TryShapeHitSphere(
            CombatPhysicsShape shape,
            FixVector3 center,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                {
                    return TryRaycastSphere(shape.Origin, shape.Direction, shape.Length, center, radius, out distance, out point, out normal);
                }
                case CombatPhysicsShapeKind.Sphere:
                    return TrySphereSphere(shape.Center, shape.Radius, center, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Capsule:
                    return TryCapsuleSphere(shape.PointA, shape.PointB, shape.Radius, center, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Aabb:
                    return TryAabbSphere(shape.Min, shape.Max, center, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Sector:
                    return TrySectorSphere(shape, center, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Obb:
                    return TryObbSphere(shape, center, radius, out distance, out point, out normal);
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }
        }

        private static bool TryShapeHitCapsule(
            CombatPhysicsShape shape,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                {
                    return TryRaycastCapsule(shape.Origin, shape.Direction, shape.Length, pointA, pointB, radius, out distance, out point, out normal);
                }
                case CombatPhysicsShapeKind.Sphere:
                    return TrySphereCapsule(shape.Center, shape.Radius, pointA, pointB, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Capsule:
                    return TryCapsuleCapsule(shape.PointA, shape.PointB, shape.Radius, pointA, pointB, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Aabb:
                    return TryAabbCapsule(shape.Min, shape.Max, pointA, pointB, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Sector:
                    return TrySectorCapsule(shape, pointA, pointB, radius, out distance, out point, out normal);
                case CombatPhysicsShapeKind.Obb:
                    return TryObbCapsule(shape, pointA, pointB, radius, out distance, out point, out normal);
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }
        }

        private static bool TryRaycastSphere(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 length,
            FixVector3 center,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 relative = origin - center;
            Fix64 radiusSquared = radius * radius;
            Fix64 c = relative.Dot(relative) - radiusSquared;
            if (c <= Fix64.Zero)
            {
                distance = Fix64.Zero;
                point = origin;
                normal = relative.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                return true;
            }

            Fix64 b = relative.Dot(direction);
            if (b > Fix64.Zero)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            Fix64 discriminant = (b * b) - c;
            if (discriminant < Fix64.Zero)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            Fix64 t = -b - discriminant.Sqrt();
            if (t < Fix64.Zero)
            {
                t = Fix64.Zero;
            }

            if (t > length)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            distance = t;
            point = origin + direction * t;
            FixVector3 delta = point - center;
            normal = delta.TryNormalize(out FixVector3 hitNormal) ? hitNormal : FixVector3.Zero;
            return true;
        }

        private static bool TryRaycastCapsule(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 length,
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 rayEnd = origin + direction * length;
            ClosestSegmentPoints(origin, rayEnd, capsuleA, capsuleB, out Fix64 rayT, out Fix64 _, out FixVector3 rayPoint, out FixVector3 capsulePoint);
            FixVector3 delta = rayPoint - capsulePoint;
            if (delta.LengthSquared() > radius * radius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            distance = rayT * length;
            point = rayPoint;
            normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            return true;
        }

        private static bool TrySphereSphere(
            FixVector3 queryCenter,
            Fix64 queryRadius,
            FixVector3 targetCenter,
            Fix64 targetRadius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 delta = queryCenter - targetCenter;
            Fix64 centerDistanceSquared = delta.LengthSquared();
            Fix64 combinedRadius = queryRadius + targetRadius;
            if (centerDistanceSquared > combinedRadius * combinedRadius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            Fix64 centerDistance = DistanceFromSquared(centerDistanceSquared);
            normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            distance = MaxZero(centerDistance - targetRadius);
            point = targetCenter + normal * targetRadius;
            return true;
        }

        private static bool TrySphereCapsule(
            FixVector3 sphereCenter,
            Fix64 sphereRadius,
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 capsuleRadius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 segmentPoint = ClosestPointOnSegment(sphereCenter, capsuleA, capsuleB, out Fix64 _);
            FixVector3 delta = sphereCenter - segmentPoint;
            Fix64 distanceSquared = delta.LengthSquared();
            Fix64 combinedRadius = sphereRadius + capsuleRadius;
            if (distanceSquared > combinedRadius * combinedRadius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            Fix64 centerDistance = DistanceFromSquared(distanceSquared);
            normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            distance = MaxZero(centerDistance - capsuleRadius);
            point = segmentPoint + normal * capsuleRadius;
            return true;
        }

        private static bool TryCapsuleSphere(
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 capsuleRadius,
            FixVector3 sphereCenter,
            Fix64 sphereRadius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            return TrySphereCapsule(sphereCenter, sphereRadius, capsuleA, capsuleB, capsuleRadius, out distance, out point, out normal);
        }

        private static bool TryCapsuleCapsule(
            FixVector3 queryA,
            FixVector3 queryB,
            Fix64 queryRadius,
            FixVector3 targetA,
            FixVector3 targetB,
            Fix64 targetRadius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            ClosestSegmentPoints(queryA, queryB, targetA, targetB, out Fix64 queryT, out Fix64 _, out FixVector3 queryPoint, out FixVector3 targetPoint);
            FixVector3 delta = queryPoint - targetPoint;
            Fix64 distanceSquared = delta.LengthSquared();
            Fix64 combinedRadius = queryRadius + targetRadius;
            if (distanceSquared > combinedRadius * combinedRadius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            distance = (queryB - queryA).LengthSquared().IsZero
                ? Fix64.Zero
                : queryT * (queryB - queryA).LengthSquared().Sqrt();
            point = targetPoint + normal * targetRadius;
            return true;
        }

        private static bool TryAabbSphere(
            FixVector3 min,
            FixVector3 max,
            FixVector3 center,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 closest = ClosestPointOnAabb(center, min, max);
            FixVector3 delta = center - closest;
            Fix64 distanceSquared = delta.LengthSquared();
            if (distanceSquared > radius * radius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            distance = Fix64.Zero;
            normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            point = normal.IsZero ? center : center - normal * radius;
            return true;
        }

        private static bool TryAabbCapsule(
            FixVector3 min,
            FixVector3 max,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 segment = pointB - pointA;
            Fix64 segmentLengthSquared = segment.LengthSquared();
            return TryCapsuleAabb(
                pointA,
                pointB,
                segment,
                segmentLengthSquared,
                radius,
                radius * radius,
                min,
                max,
                out distance,
                out point,
                out normal);
        }

        private static bool TrySectorSphere(
            CombatPhysicsShape sector,
            FixVector3 center,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 toCenter = center - sector.Origin;
            Fix64 centerDistanceSquared = toCenter.LengthSquared();
            Fix64 combinedRadius = sector.Radius + radius;
            if (centerDistanceSquared > combinedRadius * combinedRadius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            if (!centerDistanceSquared.IsZero)
            {
                FixVector3 direction = toCenter / centerDistanceSquared.Sqrt();
                if (sector.Direction.Dot(direction) < sector.MinDot)
                {
                    distance = Fix64.Zero;
                    point = FixVector3.Zero;
                    normal = FixVector3.Zero;
                    return false;
                }
            }

            Fix64 centerDistance = DistanceFromSquared(centerDistanceSquared);
            normal = toCenter.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            distance = MaxZero(centerDistance - radius);
            point = center - normal * radius;
            return true;
        }

        private static bool TrySectorCapsule(
            CombatPhysicsShape sector,
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 capsuleRadius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 segmentPoint = ClosestPointOnSegment(sector.Origin, capsuleA, capsuleB, out Fix64 _);
            FixVector3 toPoint = segmentPoint - sector.Origin;
            Fix64 distanceSquared = toPoint.LengthSquared();
            Fix64 combinedRadius = sector.Radius + capsuleRadius;
            if (distanceSquared > combinedRadius * combinedRadius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            if (!distanceSquared.IsZero)
            {
                FixVector3 direction = toPoint / distanceSquared.Sqrt();
                if (sector.Direction.Dot(direction) < sector.MinDot)
                {
                    distance = Fix64.Zero;
                    point = FixVector3.Zero;
                    normal = FixVector3.Zero;
                    return false;
                }
            }

            Fix64 centerDistance = DistanceFromSquared(distanceSquared);
            normal = toPoint.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            distance = MaxZero(centerDistance - capsuleRadius);
            point = segmentPoint - normal * capsuleRadius;
            return true;
        }

        private static bool TryObbSphere(
            CombatPhysicsShape obb,
            FixVector3 center,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 localCenter = ToObbLocal(center, obb);
            FixVector3 localMin = -obb.HalfExtents;
            FixVector3 localMax = obb.HalfExtents;
            FixVector3 localClosest = ClosestPointOnAabb(localCenter, localMin, localMax);
            FixVector3 localDelta = localCenter - localClosest;
            Fix64 distanceSquared = localDelta.LengthSquared();
            if (distanceSquared > radius * radius)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            distance = Fix64.Zero;
            point = FromObbLocal(localClosest, obb);
            normal = DirectionFromObbLocal(localDelta, obb).TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            return true;
        }

        private static bool TryObbCapsule(
            CombatPhysicsShape obb,
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 capsuleRadius,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 localA = ToObbLocal(capsuleA, obb);
            FixVector3 localB = ToObbLocal(capsuleB, obb);
            FixVector3 localSegment = localB - localA;
            Fix64 localSegmentLengthSquared = localSegment.LengthSquared();
            bool hit = TryCapsuleAabb(
                localA,
                localB,
                localSegment,
                localSegmentLengthSquared,
                capsuleRadius,
                capsuleRadius * capsuleRadius,
                -obb.HalfExtents,
                obb.HalfExtents,
                out distance,
                out FixVector3 localPoint,
                out FixVector3 localNormal);
            if (!hit)
            {
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            point = FromObbLocal(localPoint, obb);
            normal = DirectionFromObbLocal(localNormal, obb).TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
            return true;
        }

        private static FixVector3 ClosestPointOnSegment(
            FixVector3 point,
            FixVector3 segmentA,
            FixVector3 segmentB,
            out Fix64 t)
        {
            FixVector3 segment = segmentB - segmentA;
            Fix64 lengthSquared = segment.LengthSquared();
            if (lengthSquared.IsZero)
            {
                t = Fix64.Zero;
                return segmentA;
            }

            t = Fix64.Clamp((point - segmentA).Dot(segment) / lengthSquared, Fix64.Zero, Fix64.One);
            return segmentA + segment * t;
        }

        private static FixVector3 ToObbLocal(FixVector3 point, CombatPhysicsShape obb)
        {
            FixVector3 relative = point - obb.Center;
            return new FixVector3(
                relative.Dot(obb.AxisX),
                relative.Dot(obb.AxisY),
                relative.Dot(obb.AxisZ));
        }

        private static FixVector3 FromObbLocal(FixVector3 localPoint, CombatPhysicsShape obb)
        {
            return obb.Center
                + obb.AxisX * localPoint.X
                + obb.AxisY * localPoint.Y
                + obb.AxisZ * localPoint.Z;
        }

        private static FixVector3 DirectionToObbLocal(FixVector3 direction, CombatPhysicsShape obb)
        {
            return new FixVector3(direction.Dot(obb.AxisX), direction.Dot(obb.AxisY), direction.Dot(obb.AxisZ));
        }

        private static FixVector3 DirectionFromObbLocal(FixVector3 localDirection, CombatPhysicsShape obb)
        {
            return obb.AxisX * localDirection.X
                + obb.AxisY * localDirection.Y
                + obb.AxisZ * localDirection.Z;
        }

        private static Fix64 DistanceFromSquared(Fix64 value)
        {
            return value.IsZero ? Fix64.Zero : value.Sqrt();
        }

        private static Fix64 MaxZero(Fix64 value)
        {
            return value < Fix64.Zero ? Fix64.Zero : value;
        }

        private static bool Overlaps(FixVector3 leftMin, FixVector3 leftMax, FixVector3 rightMin, FixVector3 rightMax)
        {
            return leftMin.X <= rightMax.X
                && leftMax.X >= rightMin.X
                && leftMin.Y <= rightMax.Y
                && leftMax.Y >= rightMin.Y
                && leftMin.Z <= rightMax.Z
                && leftMax.Z >= rightMin.Z;
        }

        private static FixVector3 GetOverlapCenter(FixVector3 leftMin, FixVector3 leftMax, FixVector3 rightMin, FixVector3 rightMax)
        {
            Fix64 minX = Fix64.Max(leftMin.X, rightMin.X);
            Fix64 minY = Fix64.Max(leftMin.Y, rightMin.Y);
            Fix64 minZ = Fix64.Max(leftMin.Z, rightMin.Z);
            Fix64 maxX = Fix64.Min(leftMax.X, rightMax.X);
            Fix64 maxY = Fix64.Min(leftMax.Y, rightMax.Y);
            Fix64 maxZ = Fix64.Min(leftMax.Z, rightMax.Z);
            return new FixVector3(
                (minX + maxX) / Fix64.FromInt(2),
                (minY + maxY) / Fix64.FromInt(2),
                (minZ + maxZ) / Fix64.FromInt(2));
        }

        private static FixVector3 ClosestPointOnAabb(FixVector3 point, FixVector3 min, FixVector3 max)
        {
            return new FixVector3(
                Fix64.Clamp(point.X, min.X, max.X),
                Fix64.Clamp(point.Y, min.Y, max.Y),
                Fix64.Clamp(point.Z, min.Z, max.Z));
        }

        private static bool TryCapsuleAabb(
            FixVector3 pointA,
            FixVector3 pointB,
            FixVector3 segment,
            Fix64 segmentLengthSquared,
            Fix64 radius,
            Fix64 radiusSquared,
            FixVector3 min,
            FixVector3 max,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            bool hasExpandedHit = false;
            Fix64 expandedDistance = Fix64.Zero;
            FixVector3 expandedPoint = FixVector3.Zero;
            FixVector3 expandedNormal = FixVector3.Zero;
            if (!segmentLengthSquared.IsZero && segment.TryNormalize(out FixVector3 direction))
            {
                Fix64 length = segmentLengthSquared.Sqrt();
                FixVector3 expandedMin = new FixVector3(min.X - radius, min.Y - radius, min.Z - radius);
                FixVector3 expandedMax = new FixVector3(max.X + radius, max.Y + radius, max.Z + radius);
                hasExpandedHit = TryRaycastAabb(pointA, direction, length, expandedMin, expandedMax, out expandedDistance, out expandedPoint, out expandedNormal);
                if (TryRaycastAabb(pointA, direction, length, min, max, out distance, out point, out normal))
                {
                    if (hasExpandedHit)
                    {
                        distance = expandedDistance;
                        point = expandedPoint;
                        normal = expandedNormal;
                    }

                    return true;
                }
            }

            FixVector3 bestSegmentPoint = pointA;
            FixVector3 bestBoxPoint = ClosestPointOnAabb(pointA, min, max);
            Fix64 bestDistanceSquared = (bestSegmentPoint - bestBoxPoint).LengthSquared();
            Fix64 bestT = Fix64.Zero;

            CheckPointAabb(pointB, min, max, Fix64.One, ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentAabbEdges(pointA, pointB, min, max, ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);

            if (bestDistanceSquared > radiusSquared)
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            distance = hasExpandedHit
                ? expandedDistance
                : segmentLengthSquared.IsZero ? Fix64.Zero : bestT * segmentLengthSquared.Sqrt();
            point = bestBoxPoint;
            FixVector3 delta = bestSegmentPoint - bestBoxPoint;
            normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : expandedNormal;
            return true;
        }

        private static void CheckPointAabb(
            FixVector3 point,
            FixVector3 min,
            FixVector3 max,
            Fix64 t,
            ref Fix64 bestDistanceSquared,
            ref Fix64 bestT,
            ref FixVector3 bestSegmentPoint,
            ref FixVector3 bestBoxPoint)
        {
            FixVector3 boxPoint = ClosestPointOnAabb(point, min, max);
            Fix64 distanceSquared = (point - boxPoint).LengthSquared();
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestT = t;
                bestSegmentPoint = point;
                bestBoxPoint = boxPoint;
            }
        }

        private static void CheckSegmentAabbEdges(
            FixVector3 pointA,
            FixVector3 pointB,
            FixVector3 min,
            FixVector3 max,
            ref Fix64 bestDistanceSquared,
            ref Fix64 bestT,
            ref FixVector3 bestSegmentPoint,
            ref FixVector3 bestBoxPoint)
        {
            CheckSegmentPair(pointA, pointB, Corner(min.X, min.Y, min.Z), Corner(max.X, min.Y, min.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(min.X, max.Y, min.Z), Corner(max.X, max.Y, min.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(min.X, min.Y, max.Z), Corner(max.X, min.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(min.X, max.Y, max.Z), Corner(max.X, max.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);

            CheckSegmentPair(pointA, pointB, Corner(min.X, min.Y, min.Z), Corner(min.X, max.Y, min.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(max.X, min.Y, min.Z), Corner(max.X, max.Y, min.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(min.X, min.Y, max.Z), Corner(min.X, max.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(max.X, min.Y, max.Z), Corner(max.X, max.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);

            CheckSegmentPair(pointA, pointB, Corner(min.X, min.Y, min.Z), Corner(min.X, min.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(max.X, min.Y, min.Z), Corner(max.X, min.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(min.X, max.Y, min.Z), Corner(min.X, max.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
            CheckSegmentPair(pointA, pointB, Corner(max.X, max.Y, min.Z), Corner(max.X, max.Y, max.Z), ref bestDistanceSquared, ref bestT, ref bestSegmentPoint, ref bestBoxPoint);
        }

        private static FixVector3 Corner(Fix64 x, Fix64 y, Fix64 z)
        {
            return new FixVector3(x, y, z);
        }

        private static void CheckSegmentPair(
            FixVector3 leftA,
            FixVector3 leftB,
            FixVector3 rightA,
            FixVector3 rightB,
            ref Fix64 bestDistanceSquared,
            ref Fix64 bestT,
            ref FixVector3 bestSegmentPoint,
            ref FixVector3 bestBoxPoint)
        {
            ClosestSegmentPoints(leftA, leftB, rightA, rightB, out Fix64 t, out Fix64 _, out FixVector3 leftPoint, out FixVector3 rightPoint);
            Fix64 distanceSquared = (leftPoint - rightPoint).LengthSquared();
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestT = t;
                bestSegmentPoint = leftPoint;
                bestBoxPoint = rightPoint;
            }
        }

        private static void ClosestSegmentPoints(
            FixVector3 leftA,
            FixVector3 leftB,
            FixVector3 rightA,
            FixVector3 rightB,
            out Fix64 leftT,
            out Fix64 rightT,
            out FixVector3 leftPoint,
            out FixVector3 rightPoint)
        {
            FixVector3 leftDirection = leftB - leftA;
            FixVector3 rightDirection = rightB - rightA;
            FixVector3 relative = leftA - rightA;
            Fix64 leftLengthSquared = leftDirection.Dot(leftDirection);
            Fix64 rightLengthSquared = rightDirection.Dot(rightDirection);

            if (leftLengthSquared.IsZero && rightLengthSquared.IsZero)
            {
                leftT = Fix64.Zero;
                rightT = Fix64.Zero;
                leftPoint = leftA;
                rightPoint = rightA;
                return;
            }

            if (leftLengthSquared.IsZero)
            {
                leftT = Fix64.Zero;
                Fix64 rightLength = rightLengthSquared.Sqrt();
                FixVector3 rightUnit = rightDirection / rightLength;
                Fix64 rightDistance = Fix64.Clamp(
                    rightUnit.Dot(relative),
                    Fix64.Zero,
                    rightLength);
                rightT = rightDistance / rightLength;
            }
            else
            {
                Fix64 leftLength = leftLengthSquared.Sqrt();
                FixVector3 leftUnit = leftDirection / leftLength;
                if (rightLengthSquared.IsZero)
                {
                    rightT = Fix64.Zero;
                    Fix64 leftDistance = Fix64.Clamp(
                        -leftUnit.Dot(relative),
                        Fix64.Zero,
                        leftLength);
                    leftT = leftDistance / leftLength;
                }
                else
                {
                    Fix64 rightLength = rightLengthSquared.Sqrt();
                    FixVector3 rightUnit = rightDirection / rightLength;
                    Fix64 leftDotRight = leftUnit.Dot(rightUnit);
                    Fix64 leftDotRelative = leftUnit.Dot(relative);
                    Fix64 rightDotRelative = rightUnit.Dot(relative);
                    Fix64 denominator = Fix64.One - leftDotRight * leftDotRight;
                    Fix64 leftDistance = denominator <= Fix64.FromRatio(1, 100000)
                        ? Fix64.Zero
                        : Fix64.Clamp(
                            (leftDotRight * rightDotRelative - leftDotRelative) / denominator,
                            Fix64.Zero,
                            leftLength);
                    Fix64 rightDistance = leftDotRight * leftDistance + rightDotRelative;
                    if (rightDistance < Fix64.Zero)
                    {
                        rightDistance = Fix64.Zero;
                        leftDistance = Fix64.Clamp(-leftDotRelative, Fix64.Zero, leftLength);
                    }
                    else if (rightDistance > rightLength)
                    {
                        rightDistance = rightLength;
                        leftDistance = Fix64.Clamp(
                            leftDotRight * rightLength - leftDotRelative,
                            Fix64.Zero,
                            leftLength);
                    }

                    leftT = leftDistance / leftLength;
                    rightT = rightDistance / rightLength;
                }
            }

            leftPoint = leftA + leftDirection * leftT;
            rightPoint = rightA + rightDirection * rightT;
        }

        private static bool TryRaycastAabb(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 length,
            FixVector3 min,
            FixVector3 max,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            Fix64 tMin = Fix64.Zero;
            Fix64 tMax = length;
            normal = FixVector3.Zero;

            if (!ClipAxis(origin.X, direction.X, min.X, max.X, -AxisX, AxisX, ref tMin, ref tMax, ref normal)
                || !ClipAxis(origin.Y, direction.Y, min.Y, max.Y, -AxisY, AxisY, ref tMin, ref tMax, ref normal)
                || !ClipAxis(origin.Z, direction.Z, min.Z, max.Z, -AxisZ, AxisZ, ref tMin, ref tMax, ref normal))
            {
                distance = Fix64.Zero;
                point = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            distance = tMin;
            point = origin + (direction * tMin);
            return true;
        }

        private static bool ClipAxis(
            Fix64 origin,
            Fix64 direction,
            Fix64 min,
            Fix64 max,
            FixVector3 minNormal,
            FixVector3 maxNormal,
            ref Fix64 tMin,
            ref Fix64 tMax,
            ref FixVector3 normal)
        {
            if (direction.IsZero)
            {
                return origin >= min && origin <= max;
            }

            Fix64 t1 = (min - origin) / direction;
            Fix64 t2 = (max - origin) / direction;
            FixVector3 nearNormal = minNormal;
            if (t1 > t2)
            {
                Fix64 temp = t1;
                t1 = t2;
                t2 = temp;
                nearNormal = maxNormal;
            }

            if (t1 > tMin)
            {
                tMin = t1;
                normal = nearNormal;
            }

            if (t2 < tMax)
            {
                tMax = t2;
            }

            return tMin <= tMax;
        }
    }
}
