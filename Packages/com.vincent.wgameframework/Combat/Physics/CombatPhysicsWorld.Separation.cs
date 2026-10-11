using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
        private const long NormalToleranceRaw = 16L;

        /// <summary>
        /// Calculates one capsule-to-shape separation fact without changing world state.
        /// </summary>
        /// <remarks>NoAlloc. Arithmetic outside the supported fixed-point range fails closed.</remarks>
        public static CombatShapeSeparationResult CalculateCapsuleSeparation(
            in CombatCapsuleQuery source,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatPhysicsShape targetShape,
            bool targetIsCompoundLeaf = false,
            int targetPriority = 0)
        {
            CombatColliderId leafId = targetIsCompoundLeaf
                ? targetColliderId
                : CombatColliderId.None;

            if (targetEntityId.IsNone || targetBodyId.IsNone || targetColliderId.IsNone)
            {
                return CreateSeparationFailure(
                    CombatShapeSeparationStatus.InvalidShape,
                    source.Header,
                    targetEntityId,
                    targetBodyId,
                    targetColliderId,
                    leafId,
                    targetShape.Kind,
                    targetPriority);
            }

            try
            {
                SeparationGeometry geometry;
                switch (targetShape.Kind)
                {
                    case CombatPhysicsShapeKind.Aabb:
                        geometry = CalculateCapsuleAabbGeometry(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape.Min,
                            targetShape.Max);
                        break;
                    case CombatPhysicsShapeKind.Sphere:
                        geometry = CalculateCapsuleSphereGeometry(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape.Center,
                            targetShape.Radius);
                        break;
                    case CombatPhysicsShapeKind.Capsule:
                        geometry = CalculateCapsuleCapsuleGeometry(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape.PointA,
                            targetShape.PointB,
                            targetShape.Radius);
                        break;
                    case CombatPhysicsShapeKind.Obb:
                        geometry = CalculateCapsuleObbGeometry(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape);
                        break;
                    default:
                        return CreateSeparationFailure(
                            CombatShapeSeparationStatus.UnsupportedShape,
                            source.Header,
                            targetEntityId,
                            targetBodyId,
                            targetColliderId,
                            leafId,
                            targetShape.Kind,
                            targetPriority);
                }

                if (geometry.HasContact)
                {
                    if (geometry.Penetration < Fix64.Zero
                        || !geometry.Normal.TryNormalize(out FixVector3 normalized))
                    {
                        geometry = SeparationGeometry.Invalid;
                    }
                    else
                    {
                        geometry = new SeparationGeometry(
                            geometry.Status,
                            normalized,
                            geometry.Penetration,
                            geometry.ContactPoint);
                    }
                }

                return new CombatShapeSeparationResult(
                    geometry.Status,
                    source.Header,
                    targetEntityId,
                    targetBodyId,
                    targetColliderId,
                    leafId,
                    targetShape.Kind,
                    geometry.Normal,
                    geometry.Penetration,
                    geometry.ContactPoint,
                    targetPriority);
            }
            catch (ArithmeticException)
            {
                return CreateSeparationFailure(
                    CombatShapeSeparationStatus.InvalidShape,
                    source.Header,
                    targetEntityId,
                    targetBodyId,
                    targetColliderId,
                    leafId,
                    targetShape.Kind,
                    targetPriority);
            }
            catch (InvalidOperationException)
            {
                return CreateSeparationFailure(
                    CombatShapeSeparationStatus.InvalidShape,
                    source.Header,
                    targetEntityId,
                    targetBodyId,
                    targetColliderId,
                    leafId,
                    targetShape.Kind,
                    targetPriority);
            }
        }

        /// <summary>
        /// Appends one typed separation result for every layer/source-filtered collider.
        /// Separated and failed targets remain visible to the caller.
        /// </summary>
        /// <remarks>NoAlloc after the caller-owned result list has sufficient capacity.</remarks>
        public int QueryCapsuleSeparations(
            CombatCapsuleQuery source,
            List<CombatShapeSeparationResult> results,
            bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (source.Header.LayerMask.Value == 0)
            {
                return 0;
            }

            int startCount = results.Count;
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                if (TryGetSeparationBody(source, collider.BodyId, collider.Layer, includeSourceEntity, out CombatPhysicsBody body))
                    results.Add(CalculateWorldAabbSeparation(source, body, collider));
            }

            for (int i = 0; i < _sphereColliders.Count; i++)
            {
                CombatPhysicsSphereCollider collider = _sphereColliders[i];
                if (TryGetSeparationBody(source, collider.BodyId, collider.Layer, includeSourceEntity, out CombatPhysicsBody body))
                    results.Add(CalculateWorldSphereSeparation(source, body, collider));
            }

            for (int i = 0; i < _capsuleColliders.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = _capsuleColliders[i];
                if (TryGetSeparationBody(source, collider.BodyId, collider.Layer, includeSourceEntity, out CombatPhysicsBody body))
                    results.Add(CalculateWorldCapsuleSeparation(source, body, collider));
            }

            for (int i = 0; i < _obbColliders.Count; i++)
            {
                CombatPhysicsObbCollider collider = _obbColliders[i];
                if (TryGetSeparationBody(source, collider.BodyId, collider.Layer, includeSourceEntity, out CombatPhysicsBody body))
                    results.Add(CalculateWorldObbSeparation(source, body, collider));
            }

            for (int compoundIndex = 0; compoundIndex < _compounds.Count; compoundIndex++)
            {
                CombatPhysicsCompound compound = _compounds[compoundIndex];
                if (!TryGetBody(compound.BodyId, out CombatPhysicsBody body)
                    || (!includeSourceEntity && source.Header.SourceEntityId.Equals(body.EntityId)))
                {
                    continue;
                }

                for (int leafIndex = 0; leafIndex < compound.LeafCount; leafIndex++)
                {
                    CombatPhysicsCompoundLeaf leaf = compound.Leaves[leafIndex];
                    if (!source.Header.LayerMask.ContainsLayer(leaf.Layer)) continue;
                    results.Add(CalculateWorldCompoundSeparation(source, body, leaf));
                }
            }

            SortSeparationsRange(results, startCount, results.Count - startCount);
            return results.Count - startCount;
        }

        private bool TryGetSeparationBody(
            in CombatCapsuleQuery source,
            CombatBodyId bodyId,
            int layer,
            bool includeSourceEntity,
            out CombatPhysicsBody body)
        {
            body = default;
            if (!source.Header.LayerMask.ContainsLayer(layer) || !TryGetBody(bodyId, out body))
                return false;
            return includeSourceEntity || !source.Header.SourceEntityId.Equals(body.EntityId);
        }

        private static CombatShapeSeparationResult CalculateWorldAabbSeparation(
            in CombatCapsuleQuery source,
            CombatPhysicsBody body,
            CombatPhysicsAabbCollider collider)
        {
            try
            {
                return CalculateCapsuleSeparation(
                    source,
                    body.EntityId,
                    body.BodyId,
                    collider.ColliderId,
                    CombatPhysicsShape.Aabb(collider.GetWorldMin(body.Position), collider.GetWorldMax(body.Position)),
                    false,
                    collider.Priority);
            }
            catch (ArithmeticException)
            {
                return CreateWorldInvalid(source, body, collider.ColliderId, CombatPhysicsShapeKind.Aabb, false, collider.Priority);
            }
        }

        private static CombatShapeSeparationResult CalculateWorldSphereSeparation(
            in CombatCapsuleQuery source,
            CombatPhysicsBody body,
            CombatPhysicsSphereCollider collider)
        {
            try
            {
                return CalculateCapsuleSeparation(
                    source,
                    body.EntityId,
                    body.BodyId,
                    collider.ColliderId,
                    CombatPhysicsShape.Sphere(collider.GetWorldCenter(body.Position), collider.Radius),
                    false,
                    collider.Priority);
            }
            catch (ArithmeticException)
            {
                return CreateWorldInvalid(source, body, collider.ColliderId, CombatPhysicsShapeKind.Sphere, false, collider.Priority);
            }
        }

        private static CombatShapeSeparationResult CalculateWorldCapsuleSeparation(
            in CombatCapsuleQuery source,
            CombatPhysicsBody body,
            CombatPhysicsCapsuleCollider collider)
        {
            try
            {
                return CalculateCapsuleSeparation(
                    source,
                    body.EntityId,
                    body.BodyId,
                    collider.ColliderId,
                    CombatPhysicsShape.Capsule(
                        collider.GetWorldPointA(body.Position),
                        collider.GetWorldPointB(body.Position),
                        collider.Radius),
                    false,
                    collider.Priority);
            }
            catch (ArithmeticException)
            {
                return CreateWorldInvalid(source, body, collider.ColliderId, CombatPhysicsShapeKind.Capsule, false, collider.Priority);
            }
        }

        private static CombatShapeSeparationResult CalculateWorldObbSeparation(
            in CombatCapsuleQuery source,
            CombatPhysicsBody body,
            CombatPhysicsObbCollider collider)
        {
            try
            {
                return CalculateCapsuleSeparation(
                    source,
                    body.EntityId,
                    body.BodyId,
                    collider.ColliderId,
                    CombatPhysicsShape.Obb(
                        collider.GetWorldCenter(body.Position),
                        collider.HalfExtents,
                        collider.AxisX,
                        collider.AxisY,
                        collider.AxisZ),
                    false,
                    collider.Priority);
            }
            catch (ArithmeticException)
            {
                return CreateWorldInvalid(source, body, collider.ColliderId, CombatPhysicsShapeKind.Obb, false, collider.Priority);
            }
        }

        private static CombatShapeSeparationResult CalculateWorldCompoundSeparation(
            in CombatCapsuleQuery source,
            CombatPhysicsBody body,
            CombatPhysicsCompoundLeaf leaf)
        {
            try
            {
                CombatPhysicsShape worldShape = TranslateQueryShape(leaf.LocalShape, body.Position);
                return CalculateCapsuleSeparation(
                    source,
                    body.EntityId,
                    body.BodyId,
                    leaf.ColliderId,
                    worldShape,
                    true,
                    leaf.Priority);
            }
            catch (ArithmeticException)
            {
                return CreateWorldInvalid(source, body, leaf.ColliderId, leaf.LocalShape.Kind, true, leaf.Priority);
            }
        }

        private static CombatShapeSeparationResult CreateWorldInvalid(
            in CombatCapsuleQuery source,
            CombatPhysicsBody body,
            CombatColliderId colliderId,
            CombatPhysicsShapeKind shapeKind,
            bool isCompoundLeaf,
            int priority)
        {
            return CreateSeparationFailure(
                CombatShapeSeparationStatus.InvalidShape,
                source.Header,
                body.EntityId,
                body.BodyId,
                colliderId,
                isCompoundLeaf ? colliderId : CombatColliderId.None,
                shapeKind,
                priority);
        }

        private static CombatShapeSeparationResult CreateSeparationFailure(
            CombatShapeSeparationStatus status,
            CombatQueryHeader sourceQuery,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatColliderId targetCompoundLeafId,
            CombatPhysicsShapeKind targetShapeKind,
            int targetPriority)
        {
            return new CombatShapeSeparationResult(
                status,
                sourceQuery,
                targetEntityId,
                targetBodyId,
                targetColliderId,
                targetCompoundLeafId,
                targetShapeKind,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero,
                targetPriority);
        }

        private static SeparationGeometry CalculateCapsuleSphereGeometry(
            FixVector3 sourceA,
            FixVector3 sourceB,
            Fix64 sourceRadius,
            FixVector3 targetCenter,
            Fix64 targetRadius)
        {
            FixVector3 sourcePoint = ClosestPointOnSegment(targetCenter, sourceA, sourceB, out Fix64 _);
            FixVector3 delta = sourcePoint - targetCenter;
            Fix64 distanceSquared = delta.LengthSquared();
            Fix64 combinedRadius = sourceRadius + targetRadius;
            if (distanceSquared > combinedRadius * combinedRadius) return SeparationGeometry.Separated;

            FixVector3 normal = NormalizeOrFallback(delta);
            Fix64 distance = DistanceFromSquared(distanceSquared);
            FixVector3 contact = targetCenter + normal * targetRadius;
            if (distanceSquared == combinedRadius * combinedRadius)
                return SeparationGeometry.Touching(normal, contact);
            return SeparationGeometry.Overlapping(normal, combinedRadius - distance, contact);
        }

        private static SeparationGeometry CalculateCapsuleCapsuleGeometry(
            FixVector3 sourceA,
            FixVector3 sourceB,
            Fix64 sourceRadius,
            FixVector3 targetA,
            FixVector3 targetB,
            Fix64 targetRadius)
        {
            ClosestSeparationSegmentPoints(
                sourceA,
                sourceB,
                targetA,
                targetB,
                out FixVector3 sourcePoint,
                out FixVector3 targetPoint);
            FixVector3 delta = sourcePoint - targetPoint;
            Fix64 distanceSquared = delta.LengthSquared();
            Fix64 combinedRadius = sourceRadius + targetRadius;
            Fix64 combinedRadiusSquared = combinedRadius * combinedRadius;
            if (distanceSquared > combinedRadiusSquared) return SeparationGeometry.Separated;

            FixVector3 midpointDelta = ((sourceA + sourceB) * Fix64.Half)
                - ((targetA + targetB) * Fix64.Half);
            FixVector3 normal;
            if (!delta.TryNormalize(out normal))
            {
                FixVector3 sourceAxis = sourceB - sourceA;
                FixVector3 targetAxis = targetB - targetA;
                normal = TryGetCommonSegmentAxis(sourceAxis, targetAxis, out FixVector3 commonAxis)
                    ? GetDeterministicPerpendicular(commonAxis)
                    : NormalizeOrFallback(midpointDelta);
            }
            FixVector3 contact = targetPoint + normal * targetRadius;
            if (distanceSquared == combinedRadiusSquared)
                return SeparationGeometry.Touching(normal, contact);
            return SeparationGeometry.Overlapping(
                normal,
                combinedRadius - DistanceFromSquared(distanceSquared),
                contact);
        }

        private static void ClosestSeparationSegmentPoints(
            FixVector3 sourceA,
            FixVector3 sourceB,
            FixVector3 targetA,
            FixVector3 targetB,
            out FixVector3 sourcePoint,
            out FixVector3 targetPoint)
        {
            sourcePoint = sourceA;
            targetPoint = ClosestPointOnSegment(sourceA, targetA, targetB, out Fix64 _);
            Fix64 bestDistanceSquared = (sourcePoint - targetPoint).LengthSquared();

            ConsiderSegmentPair(
                sourceB,
                ClosestPointOnSegment(sourceB, targetA, targetB, out Fix64 _),
                ref bestDistanceSquared,
                ref sourcePoint,
                ref targetPoint);
            ConsiderSegmentPair(
                ClosestPointOnSegment(targetA, sourceA, sourceB, out Fix64 _),
                targetA,
                ref bestDistanceSquared,
                ref sourcePoint,
                ref targetPoint);
            ConsiderSegmentPair(
                ClosestPointOnSegment(targetB, sourceA, sourceB, out Fix64 _),
                targetB,
                ref bestDistanceSquared,
                ref sourcePoint,
                ref targetPoint);

            FixVector3 sourceDirection = sourceB - sourceA;
            FixVector3 targetDirection = targetB - targetA;
            FixVector3 relative = sourceA - targetA;
            Fix64 sourceLengthSquared = sourceDirection.Dot(sourceDirection);
            Fix64 targetLengthSquared = targetDirection.Dot(targetDirection);
            if (sourceLengthSquared.IsZero || targetLengthSquared.IsZero) return;

            Fix64 sourceDotTarget = sourceDirection.Dot(targetDirection);
            Fix64 sourceDotRelative = sourceDirection.Dot(relative);
            Fix64 targetDotRelative = targetDirection.Dot(relative);
            Fix64 denominator = (sourceLengthSquared * targetLengthSquared)
                - (sourceDotTarget * sourceDotTarget);
            if (denominator <= Fix64.Zero) return;

            Fix64 sourceT = ((sourceDotTarget * targetDotRelative)
                - (sourceDotRelative * targetLengthSquared)) / denominator;
            if (sourceT < Fix64.Zero || sourceT > Fix64.One) return;
            Fix64 targetT = ((sourceDotTarget * sourceT) + targetDotRelative)
                / targetLengthSquared;
            if (targetT < Fix64.Zero || targetT > Fix64.One) return;

            ConsiderSegmentPair(
                sourceA + sourceDirection * sourceT,
                targetA + targetDirection * targetT,
                ref bestDistanceSquared,
                ref sourcePoint,
                ref targetPoint);
        }

        private static void ConsiderSegmentPair(
            FixVector3 candidateSource,
            FixVector3 candidateTarget,
            ref Fix64 bestDistanceSquared,
            ref FixVector3 sourcePoint,
            ref FixVector3 targetPoint)
        {
            Fix64 candidateDistanceSquared = (candidateSource - candidateTarget).LengthSquared();
            if (candidateDistanceSquared >= bestDistanceSquared) return;
            bestDistanceSquared = candidateDistanceSquared;
            sourcePoint = candidateSource;
            targetPoint = candidateTarget;
        }

        private static bool TryGetCommonSegmentAxis(
            FixVector3 sourceAxis,
            FixVector3 targetAxis,
            out FixVector3 commonAxis)
        {
            bool hasSource = sourceAxis.TryNormalize(out FixVector3 normalizedSource);
            bool hasTarget = targetAxis.TryNormalize(out FixVector3 normalizedTarget);
            if (!hasSource && !hasTarget)
            {
                commonAxis = FixVector3.Zero;
                return false;
            }

            if (!hasSource)
            {
                commonAxis = normalizedTarget;
                return true;
            }

            if (!hasTarget)
            {
                commonAxis = normalizedSource;
                return true;
            }

            Fix64 alignment = normalizedSource.Dot(normalizedTarget).Abs();
            if (alignment < Fix64.FromRaw(Fix64.Scale - NormalToleranceRaw))
            {
                commonAxis = FixVector3.Zero;
                return false;
            }

            commonAxis = normalizedSource;
            return true;
        }

        private static FixVector3 GetDeterministicPerpendicular(FixVector3 axis)
        {
            Fix64 absX = axis.X.Abs();
            Fix64 absY = axis.Y.Abs();
            Fix64 absZ = axis.Z.Abs();
            FixVector3 basis = AxisX;
            Fix64 leastAlignment = absX;
            if (absY < leastAlignment)
            {
                basis = AxisY;
                leastAlignment = absY;
            }
            if (absZ < leastAlignment) basis = AxisZ;

            FixVector3 perpendicular = basis - axis * axis.Dot(basis);
            return perpendicular.TryNormalize(out FixVector3 normalized)
                ? normalized
                : AxisX;
        }

        private static SeparationGeometry CalculateCapsuleAabbGeometry(
            FixVector3 sourceA,
            FixVector3 sourceB,
            Fix64 sourceRadius,
            FixVector3 targetMin,
            FixVector3 targetMax)
        {
            GetClosestSegmentAabbPoints(
                sourceA,
                sourceB,
                targetMin,
                targetMax,
                out FixVector3 sourcePoint,
                out FixVector3 targetPoint);
            FixVector3 delta = sourcePoint - targetPoint;
            Fix64 distanceSquared = delta.LengthSquared();
            Fix64 radiusSquared = sourceRadius * sourceRadius;
            if (distanceSquared > radiusSquared) return SeparationGeometry.Separated;

            if (!distanceSquared.IsZero)
            {
                FixVector3 normal = NormalizeOrFallback(delta);
                if (distanceSquared == radiusSquared)
                    return SeparationGeometry.Touching(normal, targetPoint);
                return SeparationGeometry.Overlapping(
                    normal,
                    sourceRadius - DistanceFromSquared(distanceSquared),
                    targetPoint);
            }

            SelectCapsuleAabbExit(
                sourceA,
                sourceB,
                sourceRadius,
                targetMin,
                targetMax,
                out FixVector3 insideNormal,
                out Fix64 penetration,
                out FixVector3 contactPoint);
            return penetration.IsZero
                ? SeparationGeometry.Touching(insideNormal, contactPoint)
                : SeparationGeometry.Overlapping(insideNormal, penetration, contactPoint);
        }

        private static SeparationGeometry CalculateCapsuleObbGeometry(
            FixVector3 sourceA,
            FixVector3 sourceB,
            Fix64 sourceRadius,
            CombatPhysicsShape target)
        {
            if (!IsValidObbBasis(target)) return SeparationGeometry.Invalid;
            SeparationGeometry local = CalculateCapsuleAabbGeometry(
                ToObbLocal(sourceA, target),
                ToObbLocal(sourceB, target),
                sourceRadius,
                -target.HalfExtents,
                target.HalfExtents);
            if (!local.HasContact) return local;
            return new SeparationGeometry(
                local.Status,
                DirectionFromObbLocal(local.Normal, target),
                local.Penetration,
                FromObbLocal(local.ContactPoint, target));
        }

        private static bool IsValidObbBasis(CombatPhysicsShape target)
        {
            return IsUnit(target.AxisX)
                && IsUnit(target.AxisY)
                && IsUnit(target.AxisZ)
                && AbsRaw(target.AxisX.Dot(target.AxisY).RawValue) <= NormalToleranceRaw
                && AbsRaw(target.AxisX.Dot(target.AxisZ).RawValue) <= NormalToleranceRaw
                && AbsRaw(target.AxisY.Dot(target.AxisZ).RawValue) <= NormalToleranceRaw;
        }

        private static bool IsUnit(FixVector3 value)
        {
            return AbsRaw(value.LengthSquared().RawValue - Fix64.One.RawValue) <= NormalToleranceRaw;
        }

        private static long AbsRaw(long value)
        {
            return value < 0 ? -value : value;
        }

        private static FixVector3 NormalizeOrFallback(FixVector3 primary)
        {
            return NormalizeOrFallback(primary, FixVector3.Zero);
        }

        private static FixVector3 NormalizeOrFallback(FixVector3 primary, FixVector3 secondary)
        {
            if (primary.TryNormalize(out FixVector3 normalized)) return normalized;
            return secondary.TryNormalize(out normalized) ? normalized : AxisX;
        }

        private static void GetClosestSegmentAabbPoints(
            FixVector3 pointA,
            FixVector3 pointB,
            FixVector3 min,
            FixVector3 max,
            out FixVector3 segmentPoint,
            out FixVector3 boxPoint)
        {
            FixVector3 segment = pointB - pointA;
            Fix64 lengthSquared = segment.LengthSquared();
            if (lengthSquared.IsZero)
            {
                segmentPoint = pointA;
                boxPoint = ClosestPointOnAabb(pointA, min, max);
                return;
            }

            if (segment.TryNormalize(out FixVector3 direction)
                && TryRaycastAabb(
                    pointA,
                    direction,
                    lengthSquared.Sqrt(),
                    min,
                    max,
                    out Fix64 _,
                    out FixVector3 intersection,
                    out FixVector3 _))
            {
                segmentPoint = intersection;
                boxPoint = intersection;
                return;
            }

            segmentPoint = pointA;
            boxPoint = ClosestPointOnAabb(pointA, min, max);
            Fix64 bestDistanceSquared = (segmentPoint - boxPoint).LengthSquared();
            Fix64 bestT = Fix64.Zero;
            CheckPointAabb(pointB, min, max, Fix64.One, ref bestDistanceSquared, ref bestT, ref segmentPoint, ref boxPoint);
            CheckSegmentAabbEdges(pointA, pointB, min, max, ref bestDistanceSquared, ref bestT, ref segmentPoint, ref boxPoint);
        }

        private static void SelectCapsuleAabbExit(
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            FixVector3 min,
            FixVector3 max,
            out FixVector3 normal,
            out Fix64 penetration,
            out FixVector3 contactPoint)
        {
            FixVector3 representative = pointA.X <= pointB.X ? pointA : pointB;
            penetration = (max.X - Fix64.Min(pointA.X, pointB.X)) + radius;
            normal = AxisX;

            ConsiderAabbExit((Fix64.Max(pointA.X, pointB.X) - min.X) + radius, -AxisX,
                pointA.X >= pointB.X ? pointA : pointB, ref penetration, ref normal, ref representative);
            ConsiderAabbExit((max.Y - Fix64.Min(pointA.Y, pointB.Y)) + radius, AxisY,
                pointA.Y <= pointB.Y ? pointA : pointB, ref penetration, ref normal, ref representative);
            ConsiderAabbExit((Fix64.Max(pointA.Y, pointB.Y) - min.Y) + radius, -AxisY,
                pointA.Y >= pointB.Y ? pointA : pointB, ref penetration, ref normal, ref representative);
            ConsiderAabbExit((max.Z - Fix64.Min(pointA.Z, pointB.Z)) + radius, AxisZ,
                pointA.Z <= pointB.Z ? pointA : pointB, ref penetration, ref normal, ref representative);
            ConsiderAabbExit((Fix64.Max(pointA.Z, pointB.Z) - min.Z) + radius, -AxisZ,
                pointA.Z >= pointB.Z ? pointA : pointB, ref penetration, ref normal, ref representative);

            penetration = MaxZero(penetration);
            if (normal.X > Fix64.Zero)
                contactPoint = new FixVector3(max.X, Fix64.Clamp(representative.Y, min.Y, max.Y), Fix64.Clamp(representative.Z, min.Z, max.Z));
            else if (normal.X < Fix64.Zero)
                contactPoint = new FixVector3(min.X, Fix64.Clamp(representative.Y, min.Y, max.Y), Fix64.Clamp(representative.Z, min.Z, max.Z));
            else if (normal.Y > Fix64.Zero)
                contactPoint = new FixVector3(Fix64.Clamp(representative.X, min.X, max.X), max.Y, Fix64.Clamp(representative.Z, min.Z, max.Z));
            else if (normal.Y < Fix64.Zero)
                contactPoint = new FixVector3(Fix64.Clamp(representative.X, min.X, max.X), min.Y, Fix64.Clamp(representative.Z, min.Z, max.Z));
            else if (normal.Z > Fix64.Zero)
                contactPoint = new FixVector3(Fix64.Clamp(representative.X, min.X, max.X), Fix64.Clamp(representative.Y, min.Y, max.Y), max.Z);
            else
                contactPoint = new FixVector3(Fix64.Clamp(representative.X, min.X, max.X), Fix64.Clamp(representative.Y, min.Y, max.Y), min.Z);
        }

        private static void ConsiderAabbExit(
            Fix64 candidatePenetration,
            FixVector3 candidateNormal,
            FixVector3 candidateRepresentative,
            ref Fix64 penetration,
            ref FixVector3 normal,
            ref FixVector3 representative)
        {
            if (candidatePenetration >= penetration) return;
            penetration = candidatePenetration;
            normal = candidateNormal;
            representative = candidateRepresentative;
        }

        private static void SortSeparationsRange(
            List<CombatShapeSeparationResult> results,
            int startIndex,
            int count)
        {
            if (count <= 1) return;
            QuickSortSeparations(results, startIndex, startIndex + count - 1);
        }

        private static void QuickSortSeparations(
            List<CombatShapeSeparationResult> results,
            int left,
            int right)
        {
            while (left < right)
            {
                int i = left;
                int j = right;
                CombatShapeSeparationResult pivot = results[left + ((right - left) >> 1)];
                while (i <= j)
                {
                    while (CombatShapeSeparationResultComparer.CompareValues(results[i], pivot) < 0) i++;
                    while (CombatShapeSeparationResultComparer.CompareValues(results[j], pivot) > 0) j--;
                    if (i <= j)
                    {
                        if (i != j)
                        {
                            CombatShapeSeparationResult value = results[i];
                            results[i] = results[j];
                            results[j] = value;
                        }
                        i++;
                        j--;
                    }
                }

                if (j - left < right - i)
                {
                    if (left < j) QuickSortSeparations(results, left, j);
                    left = i;
                }
                else
                {
                    if (i < right) QuickSortSeparations(results, i, right);
                    right = j;
                }
            }
        }

        private readonly struct SeparationGeometry
        {
            public static readonly SeparationGeometry Invalid = new SeparationGeometry(
                CombatShapeSeparationStatus.InvalidShape,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero);

            public static readonly SeparationGeometry Separated = new SeparationGeometry(
                CombatShapeSeparationStatus.Separated,
                FixVector3.Zero,
                Fix64.Zero,
                FixVector3.Zero);

            public SeparationGeometry(
                CombatShapeSeparationStatus status,
                FixVector3 normal,
                Fix64 penetration,
                FixVector3 contactPoint)
            {
                Status = status;
                Normal = normal;
                Penetration = penetration;
                ContactPoint = contactPoint;
            }

            public CombatShapeSeparationStatus Status { get; }
            public FixVector3 Normal { get; }
            public Fix64 Penetration { get; }
            public FixVector3 ContactPoint { get; }
            public bool HasContact => Status == CombatShapeSeparationStatus.Touching
                || Status == CombatShapeSeparationStatus.Overlapping;

            public static SeparationGeometry Touching(FixVector3 normal, FixVector3 contactPoint)
            {
                return new SeparationGeometry(
                    CombatShapeSeparationStatus.Touching,
                    normal,
                    Fix64.Zero,
                    contactPoint);
            }

            public static SeparationGeometry Overlapping(
                FixVector3 normal,
                Fix64 penetration,
                FixVector3 contactPoint)
            {
                return new SeparationGeometry(
                    CombatShapeSeparationStatus.Overlapping,
                    normal,
                    penetration,
                    contactPoint);
            }
        }
    }
}
