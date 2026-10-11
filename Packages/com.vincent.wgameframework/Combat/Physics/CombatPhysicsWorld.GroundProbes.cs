using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
        private const long GroundProbeMillimeterRaw = Fix64.Scale / 1000L;
        private const long GroundProbeNormalScale = 32767L;
        private static readonly Fix64 GroundProbeMaximumAuthoredDistance = Fix64.FromInt(100);
        private static readonly Fix64 GroundProbeUnitLengthTolerance =
            Fix64.FromRatio(2, GroundProbeNormalScale);

        /// <summary>Selects one stable static surface using the existing Combat broadphase and narrowphase.</summary>
        /// <remarks>NoAlloc after the world's existing broadphase and compound capacities are initialized.</remarks>
        public CombatGroundProbeResult QueryGroundProbe(in CombatGroundProbeQuery query)
        {
            CombatGroundProbeStatus validation = ValidateGroundProbe(
                query,
                out FixVector3 normalizedDirection,
                out FixVector3 endpoint);
            if (validation != CombatGroundProbeStatus.Success)
            {
                return CombatGroundProbeResult.Failure(validation);
            }

            CombatQueryKind queryKind = query.Radius.IsZero
                ? CombatQueryKind.Ray
                : CombatQueryKind.Capsule;
            var header = new CombatQueryHeader(
                query.ProbeId,
                queryKind,
                query.SourceEntityId,
                0,
                0,
                query.SourceOrder,
                query.StaticLayerMask);
            CombatPhysicsShape shape = query.Radius.IsZero
                ? CombatPhysicsShape.Ray(query.Origin, normalizedDirection, query.Distance)
                : CombatPhysicsShape.Capsule(
                    query.Origin,
                    endpoint,
                    query.Radius);
            var physicsQuery = new CombatPhysicsQuery(
                header,
                shape,
                new CombatPhysicsQueryFilter(includeSourceEntity: false));

            bool hasBest = false;
            GroundProbeCandidate best = default;
            if (_aabbColliders.Count != 0
                || _sphereColliders.Count != 0
                || _capsuleColliders.Count != 0
                || _obbColliders.Count != 0)
            {
                CombatPhysicsBroadphaseResult broadphase = CollectBroadphaseCandidates(
                    physicsQuery,
                    CombatPhysicsBroadphaseCandidateOrdering.Unsorted);
                for (int i = 0; i < broadphase.Candidates.Count; i++)
                {
                    CombatPhysicsBroadphaseCandidate candidate = broadphase.Candidates[i];
                    if (!CanHitBroadphaseCandidate(physicsQuery, candidate)
                        || !TryGroundProbeHit(
                            query.Origin,
                            normalizedDirection,
                            query.Distance,
                            query.Radius,
                            candidate.Shape,
                            out Fix64 distance,
                            out FixVector3 point,
                            out FixVector3 normal))
                    {
                        continue;
                    }

                    var hit = new CombatQueryResult(
                        header,
                        candidate.Body.EntityId,
                        candidate.Body.BodyId,
                        candidate.ColliderId,
                        candidate.ShapeKind,
                        distance,
                        point,
                        normal,
                        candidate.Priority);
                    ConsiderGroundProbeCandidate(
                        new GroundProbeCandidate(hit, isCompoundLeaf: false),
                        ref hasBest,
                        ref best);
                }
            }

            QueryGroundProbeCompounds(
                physicsQuery,
                shape,
                normalizedDirection,
                query.Distance,
                query.Radius,
                ref hasBest,
                ref best);
            if (!hasBest)
            {
                return CombatGroundProbeResult.Failure(CombatGroundProbeStatus.NoHit);
            }

            CombatQueryResult selected = best.Hit;
            if (selected.Distance > query.Distance)
            {
                return CombatGroundProbeResult.Failure(CombatGroundProbeStatus.OutOfRange);
            }

            if (selected.TargetEntityId.IsNone
                || selected.TargetBodyId.IsNone
                || selected.TargetColliderId.IsNone
                || !TryGetBody(selected.TargetBodyId, out CombatPhysicsBody surfaceBody)
                || !surfaceBody.EntityId.Equals(selected.TargetEntityId))
            {
                return CombatGroundProbeResult.Failure(
                    CombatGroundProbeStatus.SurfaceIdentityUnavailable);
            }

            if (!TryGetSurfaceMaterial(selected, out CombatSurfaceMaterial surfaceMaterial))
            {
                return CombatGroundProbeResult.Failure(
                    CombatGroundProbeStatus.SurfaceMaterialUnavailable);
            }

            if (!selected.Normal.TryNormalize(out FixVector3 normalizedNormal)
                || !TryQuantizeNormal(normalizedNormal, out FixVector3 quantizedNormal))
            {
                return CombatGroundProbeResult.Failure(CombatGroundProbeStatus.UnsupportedShape);
            }

            Fix64 slopeCosine = QuantizeSignedNormalComponent(quantizedNormal.Y);
            if (slopeCosine < query.MinimumSlopeCosine)
            {
                return CombatGroundProbeResult.Failure(CombatGroundProbeStatus.TooSteep);
            }

            var hitResult = new CombatGroundProbeHit(
                selected.TargetEntityId,
                selected.TargetBodyId,
                selected.TargetColliderId,
                best.IsCompoundLeaf ? selected.TargetColliderId : CombatColliderId.None,
                selected.TargetShapeKind,
                QuantizeMillimeters(selected.Distance),
                QuantizeMillimeters(selected.Point),
                quantizedNormal,
                slopeCosine,
                surfaceMaterial,
                selected.TargetPriority);
            return CombatGroundProbeResult.Success(hitResult);
        }

        /// <summary>Resolves material from an exact query-hit identity without a fallback.</summary>
        /// <remarks>NoAlloc. Ambiguous primitive/compound identities fail closed.</remarks>
        public bool TryGetSurfaceMaterial(
            in CombatQueryResult hit,
            out CombatSurfaceMaterial surfaceMaterial)
        {
            if (hit.TargetEntityId.IsNone
                || hit.TargetBodyId.IsNone
                || hit.TargetColliderId.IsNone
                || !TryGetBody(hit.TargetBodyId, out CombatPhysicsBody body)
                || !body.EntityId.Equals(hit.TargetEntityId))
            {
                surfaceMaterial = default;
                return false;
            }

            bool found = false;
            CombatSurfaceMaterial resolved = default;
            switch (hit.TargetShapeKind)
            {
                case CombatPhysicsShapeKind.Aabb:
                    if (TryGetAabbCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsAabbCollider aabb))
                    {
                        found = true;
                        resolved = aabb.SurfaceMaterial;
                    }

                    break;
                case CombatPhysicsShapeKind.Sphere:
                    if (TryGetSphereCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsSphereCollider sphere))
                    {
                        found = true;
                        resolved = sphere.SurfaceMaterial;
                    }

                    break;
                case CombatPhysicsShapeKind.Capsule:
                    if (TryGetCapsuleCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsCapsuleCollider capsule))
                    {
                        found = true;
                        resolved = capsule.SurfaceMaterial;
                    }

                    break;
                case CombatPhysicsShapeKind.Obb:
                    if (TryGetObbCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsObbCollider obb))
                    {
                        found = true;
                        resolved = obb.SurfaceMaterial;
                    }

                    break;
                default:
                    surfaceMaterial = default;
                    return false;
            }

            int compoundMatches = 0;
            CombatSurfaceMaterial compoundMaterial = default;
            if (_compoundIndices.TryGetValue(hit.TargetBodyId, out int compoundIndex))
            {
                CombatPhysicsCompound compound = _compounds[compoundIndex];
                for (int i = 0; i < compound.LeafCount; i++)
                {
                    CombatPhysicsCompoundLeaf leaf = compound.Leaves[i];
                    if (!leaf.ColliderId.Equals(hit.TargetColliderId)
                        || leaf.LocalShape.Kind != hit.TargetShapeKind)
                    {
                        continue;
                    }

                    compoundMatches++;
                    compoundMaterial = leaf.SurfaceMaterial;
                }
            }

            if ((found && compoundMatches != 0) || compoundMatches > 1)
            {
                surfaceMaterial = default;
                return false;
            }

            if (compoundMatches == 1)
            {
                surfaceMaterial = compoundMaterial;
                return true;
            }

            surfaceMaterial = resolved;
            return found;
        }

        private static CombatGroundProbeStatus ValidateGroundProbe(
            in CombatGroundProbeQuery query,
            out FixVector3 normalizedDirection,
            out FixVector3 endpoint)
        {
            normalizedDirection = FixVector3.Zero;
            endpoint = FixVector3.Zero;
            if (query.ProbeId < 0
                || query.SourceOrder < 0
                || query.StaticLayerMask.Value == 0
                || query.Distance <= Fix64.Zero
                || query.Radius < Fix64.Zero
                || query.MinimumSlopeCosine < Fix64.Zero
                || query.MinimumSlopeCosine > Fix64.One
                || !HasUnitBoundedComponents(query.Direction)
                || (query.Direction.LengthSquared() - Fix64.One).Abs()
                    > GroundProbeUnitLengthTolerance
                || !query.Direction.TryNormalize(out normalizedDirection)
                || normalizedDirection.Y >= Fix64.Zero)
            {
                return CombatGroundProbeStatus.InvalidInput;
            }

            if (query.Distance > GroundProbeMaximumAuthoredDistance
                || query.Radius > GroundProbeMaximumAuthoredDistance
                || !TryBuildGroundProbeEndpoint(
                    query.Origin,
                    normalizedDirection,
                    query.Distance,
                    query.Radius,
                    out endpoint))
            {
                return CombatGroundProbeStatus.OutOfRange;
            }

            return CombatGroundProbeStatus.Success;
        }

        private static bool HasUnitBoundedComponents(FixVector3 value)
        {
            return value.X >= -Fix64.One && value.X <= Fix64.One
                && value.Y >= -Fix64.One && value.Y <= Fix64.One
                && value.Z >= -Fix64.One && value.Z <= Fix64.One;
        }

        private static bool TryBuildGroundProbeEndpoint(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distance,
            Fix64 radius,
            out FixVector3 endpoint)
        {
            if (!TryBuildGroundProbeEndpointComponent(
                    origin.X,
                    direction.X,
                    distance,
                    radius,
                    out Fix64 endpointX)
                || !TryBuildGroundProbeEndpointComponent(
                    origin.Y,
                    direction.Y,
                    distance,
                    radius,
                    out Fix64 endpointY)
                || !TryBuildGroundProbeEndpointComponent(
                    origin.Z,
                    direction.Z,
                    distance,
                    radius,
                    out Fix64 endpointZ))
            {
                endpoint = FixVector3.Zero;
                return false;
            }

            endpoint = new FixVector3(endpointX, endpointY, endpointZ);
            return true;
        }

        private static bool TryBuildGroundProbeEndpointComponent(
            Fix64 origin,
            Fix64 direction,
            Fix64 distance,
            Fix64 radius,
            out Fix64 endpoint)
        {
            // Direction components are unit-bounded and authored distance is <= 100 m,
            // so this product cannot overflow Int64 before fixed-point scaling.
            long deltaRaw = direction.RawValue * distance.RawValue / Fix64.Scale;
            if (!TryAddRaw(origin.RawValue, deltaRaw, out long endpointRaw)
                || !CanExpandRaw(origin.RawValue, radius.RawValue)
                || !CanExpandRaw(endpointRaw, radius.RawValue))
            {
                endpoint = Fix64.Zero;
                return false;
            }

            endpoint = Fix64.FromRaw(endpointRaw);
            return true;
        }

        private static bool CanExpandRaw(long value, long nonNegativeAmount)
        {
            return value >= long.MinValue + nonNegativeAmount
                && value <= long.MaxValue - nonNegativeAmount;
        }

        private static bool TryAddRaw(long left, long right, out long result)
        {
            if ((right > 0 && left > long.MaxValue - right)
                || (right < 0 && left < long.MinValue - right))
            {
                result = 0;
                return false;
            }

            result = left + right;
            return true;
        }

        private static bool TryGroundProbeHit(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distanceLimit,
            Fix64 probeRadius,
            CombatPhysicsShape target,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            if (probeRadius.IsZero)
            {
                return TryShapeHitTarget(
                    CombatPhysicsShape.Ray(origin, direction, distanceLimit),
                    target,
                    out distance,
                    out point,
                    out normal);
            }

            switch (target.Kind)
            {
                case CombatPhysicsShapeKind.Aabb:
                    return TrySphereCastGroundAabb(
                        origin,
                        direction,
                        distanceLimit,
                        probeRadius,
                        target.Min,
                        target.Max,
                        out distance,
                        out point,
                        out normal);
                case CombatPhysicsShapeKind.Sphere:
                    if (!TryAddNonNegative(
                            probeRadius,
                            target.Radius,
                            out Fix64 combinedSphereRadius)
                        || !TryRaycastGroundSphere(
                            origin,
                            direction,
                            distanceLimit,
                            target.Center,
                            combinedSphereRadius,
                            out distance,
                            out FixVector3 _,
                            out normal))
                    {
                        distance = Fix64.Zero;
                        point = FixVector3.Zero;
                        normal = FixVector3.Zero;
                        return false;
                    }

                    point = target.Center + normal * target.Radius;
                    return true;
                case CombatPhysicsShapeKind.Capsule:
                    if (!TryAddNonNegative(
                            probeRadius,
                            target.Radius,
                            out Fix64 combinedCapsuleRadius)
                        || !TryRaycastGroundCapsule(
                            origin,
                            direction,
                            distanceLimit,
                            target.PointA,
                            target.PointB,
                            combinedCapsuleRadius,
                            out distance,
                            out FixVector3 axisPoint,
                            out normal))
                    {
                        distance = Fix64.Zero;
                        point = FixVector3.Zero;
                        normal = FixVector3.Zero;
                        return false;
                    }

                    point = axisPoint + normal * target.Radius;
                    return true;
                case CombatPhysicsShapeKind.Obb:
                    FixVector3 localOrigin = ToObbLocal(origin, target);
                    FixVector3 localDirection = DirectionToObbLocal(direction, target);
                    if (!TrySphereCastGroundAabb(
                        localOrigin,
                        localDirection,
                        distanceLimit,
                        probeRadius,
                        -target.HalfExtents,
                        target.HalfExtents,
                        out distance,
                        out FixVector3 localPoint,
                        out FixVector3 localNormal))
                    {
                        point = FixVector3.Zero;
                        normal = FixVector3.Zero;
                        return false;
                    }

                    point = FromObbLocal(localPoint, target);
                    normal = DirectionFromObbLocal(localNormal, target);
                    return true;
                default:
                    distance = Fix64.Zero;
                    point = FixVector3.Zero;
                    normal = FixVector3.Zero;
                    return false;
            }
        }

        private static bool TryAddNonNegative(Fix64 left, Fix64 right, out Fix64 result)
        {
            if (left < Fix64.Zero
                || right < Fix64.Zero
                || !TryAddRaw(left.RawValue, right.RawValue, out long raw))
            {
                result = Fix64.Zero;
                return false;
            }

            result = Fix64.FromRaw(raw);
            return true;
        }

        private static bool TryRaycastGroundSphere(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distanceLimit,
            FixVector3 center,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 hitCenter,
            out FixVector3 normal)
        {
            FixVector3 relative = origin - center;
            Fix64 radiusSquared = radius * radius;
            Fix64 c = relative.Dot(relative) - radiusSquared;
            if (c <= Fix64.Zero)
            {
                distance = Fix64.Zero;
                hitCenter = origin;
                normal = relative.TryNormalize(out FixVector3 initialNormal)
                    ? initialNormal
                    : FixVector3.Zero;
                return true;
            }

            Fix64 directionSquared = direction.Dot(direction);
            if (directionSquared.IsZero)
            {
                distance = Fix64.Zero;
                hitCenter = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            Fix64 projected = relative.Dot(direction);
            if (projected > Fix64.Zero)
            {
                distance = Fix64.Zero;
                hitCenter = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            Fix64 discriminant = projected * projected - directionSquared * c;
            if (discriminant < Fix64.Zero)
            {
                distance = Fix64.Zero;
                hitCenter = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            Fix64 candidate = (-projected - discriminant.Sqrt()) / directionSquared;
            if (candidate < Fix64.Zero || candidate > distanceLimit)
            {
                distance = Fix64.Zero;
                hitCenter = FixVector3.Zero;
                normal = FixVector3.Zero;
                return false;
            }

            distance = candidate;
            hitCenter = origin + direction * candidate;
            FixVector3 delta = hitCenter - center;
            normal = delta.TryNormalize(out FixVector3 hitNormal)
                ? hitNormal
                : FixVector3.Zero;
            return true;
        }

        private static bool TryRaycastGroundCapsule(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distanceLimit,
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 radius,
            out Fix64 distance,
            out FixVector3 axisPoint,
            out FixVector3 normal)
        {
            FixVector3 initialAxisPoint = ClosestPointOnSegment(
                origin,
                capsuleA,
                capsuleB,
                out Fix64 _);
            FixVector3 initialDelta = origin - initialAxisPoint;
            if (initialDelta.LengthSquared() <= radius * radius)
            {
                distance = Fix64.Zero;
                axisPoint = initialAxisPoint;
                normal = initialDelta.TryNormalize(out FixVector3 initialNormal)
                    ? initialNormal
                    : FixVector3.Zero;
                return true;
            }

            bool hasBest = false;
            Fix64 bestDistance = Fix64.Zero;
            FixVector3 bestAxisPoint = FixVector3.Zero;
            FixVector3 bestNormal = FixVector3.Zero;
            FixVector3 capsuleAxis = capsuleB - capsuleA;
            FixVector3 relative = origin - capsuleA;
            Fix64 axisSquared = capsuleAxis.Dot(capsuleAxis);
            Fix64 directionSquared = direction.Dot(direction);
            if (!axisSquared.IsZero
                && !directionSquared.IsZero
                && capsuleAxis.TryNormalize(out FixVector3 axisDirection))
            {
                Fix64 axisLength = axisSquared.Sqrt();
                Fix64 directionAlongAxis = direction.Dot(axisDirection);
                Fix64 relativeAlongAxis = relative.Dot(axisDirection);
                FixVector3 perpendicularDirection =
                    direction - axisDirection * directionAlongAxis;
                FixVector3 perpendicularRelative =
                    relative - axisDirection * relativeAlongAxis;
                Fix64 coefficientA = perpendicularDirection.Dot(perpendicularDirection);
                Fix64 coefficientB = perpendicularRelative.Dot(perpendicularDirection);
                Fix64 coefficientC = perpendicularRelative.Dot(perpendicularRelative)
                    - radius * radius;
                Fix64 discriminant = coefficientB * coefficientB
                    - coefficientA * coefficientC;
                if (coefficientA > Fix64.Zero && discriminant >= Fix64.Zero)
                {
                    Fix64 candidate = (-coefficientB - discriminant.Sqrt()) / coefficientA;
                    Fix64 axisPosition = relativeAlongAxis + candidate * directionAlongAxis;
                    if (candidate >= Fix64.Zero
                        && candidate <= distanceLimit
                        && axisPosition >= Fix64.Zero
                        && axisPosition <= axisLength)
                    {
                        ConsiderGroundCapsuleHit(
                            origin,
                            direction,
                            capsuleA,
                            capsuleB,
                            candidate,
                            ref hasBest,
                            ref bestDistance,
                            ref bestAxisPoint,
                            ref bestNormal);
                    }
                }
            }

            ConsiderGroundCapsuleCap(
                origin,
                direction,
                distanceLimit,
                capsuleA,
                capsuleA,
                capsuleB,
                radius,
                ref hasBest,
                ref bestDistance,
                ref bestAxisPoint,
                ref bestNormal);
            ConsiderGroundCapsuleCap(
                origin,
                direction,
                distanceLimit,
                capsuleB,
                capsuleA,
                capsuleB,
                radius,
                ref hasBest,
                ref bestDistance,
                ref bestAxisPoint,
                ref bestNormal);

            distance = bestDistance;
            axisPoint = bestAxisPoint;
            normal = bestNormal;
            return hasBest;
        }

        private static void ConsiderGroundCapsuleCap(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distanceLimit,
            FixVector3 capCenter,
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 radius,
            ref bool hasBest,
            ref Fix64 bestDistance,
            ref FixVector3 bestAxisPoint,
            ref FixVector3 bestNormal)
        {
            if (TryRaycastGroundSphere(
                origin,
                direction,
                distanceLimit,
                capCenter,
                radius,
                out Fix64 candidate,
                out FixVector3 _,
                out FixVector3 _))
            {
                ConsiderGroundCapsuleHit(
                    origin,
                    direction,
                    capsuleA,
                    capsuleB,
                    candidate,
                    ref hasBest,
                    ref bestDistance,
                    ref bestAxisPoint,
                    ref bestNormal);
            }
        }

        private static void ConsiderGroundCapsuleHit(
            FixVector3 origin,
            FixVector3 direction,
            FixVector3 capsuleA,
            FixVector3 capsuleB,
            Fix64 candidateDistance,
            ref bool hasBest,
            ref Fix64 bestDistance,
            ref FixVector3 bestAxisPoint,
            ref FixVector3 bestNormal)
        {
            FixVector3 candidateCenter = origin + direction * candidateDistance;
            FixVector3 candidateAxisPoint = ClosestPointOnSegment(
                candidateCenter,
                capsuleA,
                capsuleB,
                out Fix64 _);
            FixVector3 delta = candidateCenter - candidateAxisPoint;
            FixVector3 candidateNormal = delta.TryNormalize(out FixVector3 normalized)
                ? normalized
                : FixVector3.Zero;
            if (!hasBest
                || CompareGroundShapeHits(
                    candidateDistance,
                    candidateAxisPoint,
                    candidateNormal,
                    bestDistance,
                    bestAxisPoint,
                    bestNormal) < 0)
            {
                hasBest = true;
                bestDistance = candidateDistance;
                bestAxisPoint = candidateAxisPoint;
                bestNormal = candidateNormal;
            }
        }

        private static bool TrySphereCastGroundAabb(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distanceLimit,
            Fix64 radius,
            FixVector3 min,
            FixVector3 max,
            out Fix64 distance,
            out FixVector3 point,
            out FixVector3 normal)
        {
            FixVector3 initialPoint = ClosestPointOnAabb(origin, min, max);
            FixVector3 initialDelta = origin - initialPoint;
            if (initialDelta.LengthSquared() <= radius * radius)
            {
                distance = Fix64.Zero;
                point = initialPoint;
                normal = initialDelta.TryNormalize(out FixVector3 initialNormal)
                    ? initialNormal
                    : FixVector3.Zero;
                return true;
            }

            bool hasBest = false;
            Fix64 bestDistance = Fix64.Zero;
            FixVector3 bestPoint = FixVector3.Zero;
            FixVector3 bestNormal = FixVector3.Zero;
            for (int axis = 0; axis < 3; axis++)
            {
                ConsiderGroundAabbFace(
                    origin,
                    direction,
                    distanceLimit,
                    radius,
                    min,
                    max,
                    axis,
                    useMaximumFace: false,
                    ref hasBest,
                    ref bestDistance,
                    ref bestPoint,
                    ref bestNormal);
                ConsiderGroundAabbFace(
                    origin,
                    direction,
                    distanceLimit,
                    radius,
                    min,
                    max,
                    axis,
                    useMaximumFace: true,
                    ref hasBest,
                    ref bestDistance,
                    ref bestPoint,
                    ref bestNormal);

                for (int edge = 0; edge < 4; edge++)
                {
                    BuildGroundAabbEdge(min, max, axis, edge, out FixVector3 edgeA, out FixVector3 edgeB);
                    if (!TryRaycastGroundCapsule(
                        origin,
                        direction,
                        distanceLimit,
                        edgeA,
                        edgeB,
                        radius,
                        out Fix64 candidateDistance,
                        out FixVector3 candidatePoint,
                        out FixVector3 candidateNormal))
                    {
                        continue;
                    }

                    ConsiderGroundShapeHit(
                        candidateDistance,
                        candidatePoint,
                        candidateNormal,
                        ref hasBest,
                        ref bestDistance,
                        ref bestPoint,
                        ref bestNormal);
                }
            }

            distance = bestDistance;
            point = bestPoint;
            normal = bestNormal;
            return hasBest;
        }

        private static void ConsiderGroundAabbFace(
            FixVector3 origin,
            FixVector3 direction,
            Fix64 distanceLimit,
            Fix64 radius,
            FixVector3 min,
            FixVector3 max,
            int axis,
            bool useMaximumFace,
            ref bool hasBest,
            ref Fix64 bestDistance,
            ref FixVector3 bestPoint,
            ref FixVector3 bestNormal)
        {
            Fix64 directionComponent = GetGroundVectorComponent(direction, axis);
            Fix64 normalSign = useMaximumFace ? Fix64.One : -Fix64.One;
            if ((directionComponent * normalSign) >= Fix64.Zero)
            {
                return;
            }

            Fix64 face = GetGroundVectorComponent(useMaximumFace ? max : min, axis);
            if (!TryAddRaw(
                face.RawValue,
                normalSign.RawValue * radius.RawValue / Fix64.Scale,
                out long expandedFaceRaw))
            {
                return;
            }

            Fix64 candidateDistance =
                (Fix64.FromRaw(expandedFaceRaw) - GetGroundVectorComponent(origin, axis))
                / directionComponent;
            if (candidateDistance < Fix64.Zero || candidateDistance > distanceLimit)
            {
                return;
            }

            FixVector3 center = origin + direction * candidateDistance;
            for (int otherAxis = 0; otherAxis < 3; otherAxis++)
            {
                if (otherAxis == axis)
                {
                    continue;
                }

                Fix64 component = GetGroundVectorComponent(center, otherAxis);
                if (component < GetGroundVectorComponent(min, otherAxis)
                    || component > GetGroundVectorComponent(max, otherAxis))
                {
                    return;
                }
            }

            FixVector3 candidateNormal = GetGroundAxis(axis) * normalSign;
            FixVector3 candidatePoint = center - candidateNormal * radius;
            ConsiderGroundShapeHit(
                candidateDistance,
                candidatePoint,
                candidateNormal,
                ref hasBest,
                ref bestDistance,
                ref bestPoint,
                ref bestNormal);
        }

        private static void BuildGroundAabbEdge(
            FixVector3 min,
            FixVector3 max,
            int edgeAxis,
            int edgeIndex,
            out FixVector3 edgeA,
            out FixVector3 edgeB)
        {
            edgeA = min;
            edgeB = min;
            edgeB = SetGroundVectorComponent(
                edgeB,
                edgeAxis,
                GetGroundVectorComponent(max, edgeAxis));
            int bit = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == edgeAxis)
                {
                    continue;
                }

                if ((edgeIndex & (1 << bit)) != 0)
                {
                    Fix64 component = GetGroundVectorComponent(max, axis);
                    edgeA = SetGroundVectorComponent(edgeA, axis, component);
                    edgeB = SetGroundVectorComponent(edgeB, axis, component);
                }

                bit++;
            }
        }

        private static void ConsiderGroundShapeHit(
            Fix64 candidateDistance,
            FixVector3 candidatePoint,
            FixVector3 candidateNormal,
            ref bool hasBest,
            ref Fix64 bestDistance,
            ref FixVector3 bestPoint,
            ref FixVector3 bestNormal)
        {
            if (!hasBest
                || CompareGroundShapeHits(
                    candidateDistance,
                    candidatePoint,
                    candidateNormal,
                    bestDistance,
                    bestPoint,
                    bestNormal) < 0)
            {
                hasBest = true;
                bestDistance = candidateDistance;
                bestPoint = candidatePoint;
                bestNormal = candidateNormal;
            }
        }

        private static int CompareGroundShapeHits(
            Fix64 leftDistance,
            FixVector3 leftPoint,
            FixVector3 leftNormal,
            Fix64 rightDistance,
            FixVector3 rightPoint,
            FixVector3 rightNormal)
        {
            int compare = leftDistance.RawValue.CompareTo(rightDistance.RawValue);
            if (compare != 0) return compare;
            compare = CompareVectorRaw(leftPoint, rightPoint);
            return compare != 0 ? compare : CompareVectorRaw(leftNormal, rightNormal);
        }

        private static Fix64 GetGroundVectorComponent(FixVector3 value, int axis)
        {
            return axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
        }

        private static FixVector3 SetGroundVectorComponent(
            FixVector3 value,
            int axis,
            Fix64 component)
        {
            return axis == 0
                ? new FixVector3(component, value.Y, value.Z)
                : axis == 1
                    ? new FixVector3(value.X, component, value.Z)
                    : new FixVector3(value.X, value.Y, component);
        }

        private static FixVector3 GetGroundAxis(int axis)
        {
            return axis == 0 ? AxisX : axis == 1 ? AxisY : AxisZ;
        }

        private void QueryGroundProbeCompounds(
            in CombatPhysicsQuery query,
            CombatPhysicsShape shape,
            FixVector3 direction,
            Fix64 distanceLimit,
            Fix64 radius,
            ref bool hasBest,
            ref GroundProbeCandidate best)
        {
            GetCompoundShapeAabb(shape, out FixVector3 queryMin, out FixVector3 queryMax);
            for (int compoundIndex = 0; compoundIndex < _compounds.Count; compoundIndex++)
            {
                CombatPhysicsCompound compound = _compounds[compoundIndex];
                if (!query.Filter.IncludeSourceEntity
                    && query.Header.SourceEntityId.Equals(compound.EntityId))
                {
                    continue;
                }

                if (!AabbOverlaps(queryMin, queryMax, compound.WorldMin, compound.WorldMax)
                    || compound.NodeCount == 0)
                {
                    continue;
                }

                CombatPhysicsShape localShape = TranslateQueryShape(shape, -compound.Position);
                FixVector3 localMin = queryMin - compound.Position;
                FixVector3 localMax = queryMax - compound.Position;
                int stackCount = 1;
                compound.TraversalStack[0] = compound.RootNode;
                while (stackCount > 0)
                {
                    CombatPhysicsCompoundNode node =
                        compound.Nodes[compound.TraversalStack[--stackCount]];
                    if (!AabbOverlaps(localMin, localMax, node.Min, node.Max))
                    {
                        continue;
                    }

                    if (node.LeafIndex >= 0)
                    {
                        CombatPhysicsCompoundLeaf leaf = compound.Leaves[node.LeafIndex];
                        if (!query.Header.LayerMask.ContainsLayer(leaf.Layer)
                            || !TryGroundProbeHit(
                                shape.Kind == CombatPhysicsShapeKind.Ray
                                    ? localShape.Origin
                                    : localShape.PointA,
                                direction,
                                distanceLimit,
                                radius,
                                leaf.LocalShape,
                                out Fix64 distance,
                                out FixVector3 point,
                                out FixVector3 normal))
                        {
                            continue;
                        }

                        var hit = new CombatQueryResult(
                            query.Header,
                            compound.EntityId,
                            compound.BodyId,
                            leaf.ColliderId,
                            leaf.LocalShape.Kind,
                            distance,
                            point + compound.Position,
                            normal,
                            leaf.Priority);
                        ConsiderGroundProbeCandidate(
                            new GroundProbeCandidate(hit, isCompoundLeaf: true),
                            ref hasBest,
                            ref best);
                        continue;
                    }

                    compound.TraversalStack[stackCount++] = node.Right;
                    compound.TraversalStack[stackCount++] = node.Left;
                }
            }
        }

        private static void ConsiderGroundProbeCandidate(
            in GroundProbeCandidate candidate,
            ref bool hasBest,
            ref GroundProbeCandidate best)
        {
            if (!hasBest || CompareGroundProbeCandidates(candidate, best) < 0)
            {
                best = candidate;
                hasBest = true;
            }
        }

        private static int CompareGroundProbeCandidates(
            in GroundProbeCandidate left,
            in GroundProbeCandidate right)
        {
            int compare = CombatQueryResultComparer.Instance.Compare(left.Hit, right.Hit);
            if (compare != 0) return compare;

            compare = left.IsCompoundLeaf.CompareTo(right.IsCompoundLeaf);
            if (compare != 0) return compare;

            compare = CompareVectorRaw(left.Hit.Point, right.Hit.Point);
            return compare != 0
                ? compare
                : CompareVectorRaw(left.Hit.Normal, right.Hit.Normal);
        }

        private static int CompareVectorRaw(FixVector3 left, FixVector3 right)
        {
            int compare = left.X.RawValue.CompareTo(right.X.RawValue);
            if (compare != 0) return compare;
            compare = left.Y.RawValue.CompareTo(right.Y.RawValue);
            return compare != 0 ? compare : left.Z.RawValue.CompareTo(right.Z.RawValue);
        }

        private static FixVector3 QuantizeMillimeters(FixVector3 value)
        {
            return new FixVector3(
                QuantizeMillimeters(value.X),
                QuantizeMillimeters(value.Y),
                QuantizeMillimeters(value.Z));
        }

        private static Fix64 QuantizeMillimeters(Fix64 value)
        {
            long raw = value.RawValue;
            long half = GroundProbeMillimeterRaw / 2L;
            long rounded = raw >= 0
                ? ((raw + half) / GroundProbeMillimeterRaw) * GroundProbeMillimeterRaw
                : ((raw - half) / GroundProbeMillimeterRaw) * GroundProbeMillimeterRaw;
            return Fix64.FromRaw(rounded);
        }

        private static bool TryQuantizeNormal(
            FixVector3 normal,
            out FixVector3 quantizedNormal)
        {
            var quantized = new FixVector3(
                QuantizeSignedNormalComponent(normal.X),
                QuantizeSignedNormalComponent(normal.Y),
                QuantizeSignedNormalComponent(normal.Z));
            return quantized.TryNormalize(out quantizedNormal);
        }

        private static Fix64 QuantizeSignedNormalComponent(Fix64 value)
        {
            Fix64 clamped = Fix64.Clamp(value, -Fix64.One, Fix64.One);
            long scaled = clamped.RawValue * GroundProbeNormalScale;
            long half = Fix64.Scale / 2L;
            long signed = scaled >= 0
                ? (scaled + half) / Fix64.Scale
                : (scaled - half) / Fix64.Scale;
            return Fix64.FromRatio(signed, GroundProbeNormalScale);
        }

        private readonly struct GroundProbeCandidate
        {
            public GroundProbeCandidate(CombatQueryResult hit, bool isCompoundLeaf)
            {
                Hit = hit;
                IsCompoundLeaf = isCompoundLeaf;
            }

            public CombatQueryResult Hit { get; }

            public bool IsCompoundLeaf { get; }
        }
    }
}
