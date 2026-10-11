using System;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
        /// <summary>Calculates deterministic capsule-to-shape surface distance.</summary>
        /// <remarks>NoAlloc. Arithmetic outside the supported fixed-point range fails closed.</remarks>
        public static CombatShapeDistanceResult CalculateCapsuleDistance(
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
                return DistanceFailure(
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
                CapsuleDistanceGeometry geometry;
                switch (targetShape.Kind)
                {
                    case CombatPhysicsShapeKind.Aabb:
                        geometry = CapsuleAabbDistance(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape.Min,
                            targetShape.Max);
                        break;
                    case CombatPhysicsShapeKind.Sphere:
                        geometry = CapsuleSphereDistance(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape.Center,
                            targetShape.Radius);
                        break;
                    case CombatPhysicsShapeKind.Capsule:
                        geometry = CapsuleCapsuleDistance(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape.PointA,
                            targetShape.PointB,
                            targetShape.Radius);
                        break;
                    case CombatPhysicsShapeKind.Obb:
                        geometry = CapsuleObbDistance(
                            source.PointA,
                            source.PointB,
                            source.Radius,
                            targetShape);
                        break;
                    default:
                        return DistanceFailure(
                            CombatShapeSeparationStatus.UnsupportedShape,
                            source.Header,
                            targetEntityId,
                            targetBodyId,
                            targetColliderId,
                            leafId,
                            targetShape.Kind,
                            targetPriority);
                }

                return new CombatShapeDistanceResult(
                    geometry.Status,
                    source.Header,
                    targetEntityId,
                    targetBodyId,
                    targetColliderId,
                    leafId,
                    targetShape.Kind,
                    geometry.SurfaceGap,
                    geometry.SourceWitness,
                    geometry.TargetWitness,
                    targetPriority);
            }
            catch (ArithmeticException)
            {
                return DistanceFailure(
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
                return DistanceFailure(
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

        private static CapsuleDistanceGeometry CapsuleSphereDistance(
            FixVector3 sourceA,
            FixVector3 sourceB,
            Fix64 sourceRadius,
            FixVector3 targetCenter,
            Fix64 targetRadius)
        {
            FixVector3 sourceAxisPoint = ClosestPointOnSegment(
                targetCenter,
                sourceA,
                sourceB,
                out Fix64 _);
            return RoundedPairDistance(
                sourceAxisPoint,
                sourceRadius,
                targetCenter,
                targetRadius);
        }

        private static CapsuleDistanceGeometry CapsuleCapsuleDistance(
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
                out FixVector3 sourceAxisPoint,
                out FixVector3 targetAxisPoint);
            return RoundedPairDistance(
                sourceAxisPoint,
                sourceRadius,
                targetAxisPoint,
                targetRadius);
        }

        private static CapsuleDistanceGeometry RoundedPairDistance(
            FixVector3 sourceAxisPoint,
            Fix64 sourceRadius,
            FixVector3 targetAxisPoint,
            Fix64 targetRadius)
        {
            FixVector3 delta = sourceAxisPoint - targetAxisPoint;
            Fix64 distance = DistanceFromSquared(delta.LengthSquared());
            Fix64 combinedRadius = sourceRadius + targetRadius;
            FixVector3 normal = NormalizeOrFallback(delta);
            FixVector3 sourceWitness = sourceAxisPoint - normal * sourceRadius;
            FixVector3 targetWitness = targetAxisPoint + normal * targetRadius;
            CombatShapeSeparationStatus status = distance > combinedRadius
                ? CombatShapeSeparationStatus.Separated
                : distance == combinedRadius
                    ? CombatShapeSeparationStatus.Touching
                    : CombatShapeSeparationStatus.Overlapping;
            Fix64 gap = status == CombatShapeSeparationStatus.Separated
                ? distance - combinedRadius
                : Fix64.Zero;
            return new CapsuleDistanceGeometry(
                status,
                gap,
                sourceWitness,
                targetWitness);
        }

        private static CapsuleDistanceGeometry CapsuleAabbDistance(
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
                out FixVector3 sourceAxisPoint,
                out FixVector3 targetPoint);
            FixVector3 delta = sourceAxisPoint - targetPoint;
            Fix64 distance = DistanceFromSquared(delta.LengthSquared());
            if (distance > sourceRadius)
            {
                FixVector3 normal = NormalizeOrFallback(delta);
                return new CapsuleDistanceGeometry(
                    CombatShapeSeparationStatus.Separated,
                    distance - sourceRadius,
                    sourceAxisPoint - normal * sourceRadius,
                    targetPoint);
            }
            if (distance == sourceRadius && !distance.IsZero)
            {
                FixVector3 normal = NormalizeOrFallback(delta);
                FixVector3 witness = sourceAxisPoint - normal * sourceRadius;
                return new CapsuleDistanceGeometry(
                    CombatShapeSeparationStatus.Touching,
                    Fix64.Zero,
                    witness,
                    targetPoint);
            }

            SeparationGeometry overlap = CalculateCapsuleAabbGeometry(
                sourceA,
                sourceB,
                sourceRadius,
                targetMin,
                targetMax);
            return new CapsuleDistanceGeometry(
                overlap.Status,
                Fix64.Zero,
                overlap.ContactPoint,
                overlap.ContactPoint);
        }

        private static CapsuleDistanceGeometry CapsuleObbDistance(
            FixVector3 sourceA,
            FixVector3 sourceB,
            Fix64 sourceRadius,
            CombatPhysicsShape target)
        {
            if (!IsValidObbBasis(target))
                return CapsuleDistanceGeometry.Invalid;
            CapsuleDistanceGeometry local = CapsuleAabbDistance(
                ToObbLocal(sourceA, target),
                ToObbLocal(sourceB, target),
                sourceRadius,
                -target.HalfExtents,
                target.HalfExtents);
            if (!local.HasDistance)
                return local;
            return new CapsuleDistanceGeometry(
                local.Status,
                local.SurfaceGap,
                FromObbLocal(local.SourceWitness, target),
                FromObbLocal(local.TargetWitness, target));
        }

        private static CombatShapeDistanceResult DistanceFailure(
            CombatShapeSeparationStatus status,
            CombatQueryHeader source,
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatColliderId targetCompoundLeafId,
            CombatPhysicsShapeKind targetShapeKind,
            int targetPriority)
        {
            return new CombatShapeDistanceResult(
                status,
                source,
                targetEntityId,
                targetBodyId,
                targetColliderId,
                targetCompoundLeafId,
                targetShapeKind,
                Fix64.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                targetPriority);
        }

        private readonly struct CapsuleDistanceGeometry
        {
            internal static readonly CapsuleDistanceGeometry Invalid =
                new CapsuleDistanceGeometry(
                    CombatShapeSeparationStatus.InvalidShape,
                    Fix64.Zero,
                    FixVector3.Zero,
                    FixVector3.Zero);

            internal CapsuleDistanceGeometry(
                CombatShapeSeparationStatus status,
                Fix64 surfaceGap,
                FixVector3 sourceWitness,
                FixVector3 targetWitness)
            {
                Status = status;
                SurfaceGap = surfaceGap;
                SourceWitness = sourceWitness;
                TargetWitness = targetWitness;
            }

            internal CombatShapeSeparationStatus Status { get; }
            internal Fix64 SurfaceGap { get; }
            internal FixVector3 SourceWitness { get; }
            internal FixVector3 TargetWitness { get; }
            internal bool HasDistance => Status == CombatShapeSeparationStatus.Separated
                || Status == CombatShapeSeparationStatus.Touching
                || Status == CombatShapeSeparationStatus.Overlapping;
        }
    }
}
