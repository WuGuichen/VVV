using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public sealed class CombatPhysicsWorld
    {
        private static readonly FixVector3 AxisX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 AxisY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 AxisZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        private readonly List<CombatPhysicsBody> _bodies = new List<CombatPhysicsBody>();
        private readonly List<CombatPhysicsAabbCollider> _aabbColliders = new List<CombatPhysicsAabbCollider>();
        private readonly CombatPhysicsBroadphase _broadphase;
        private int _revision;

        public CombatPhysicsWorld()
            : this(CombatPhysicsBroadphaseConfig.Default)
        {
        }

        public CombatPhysicsWorld(CombatPhysicsBroadphaseConfig broadphaseConfig)
        {
            _broadphase = new CombatPhysicsBroadphase(broadphaseConfig);
        }

        public int BodyCount => _bodies.Count;

        public int ColliderCount => _aabbColliders.Count;

        public int Revision => _revision;

        public void Clear()
        {
            if (_bodies.Count == 0 && _aabbColliders.Count == 0)
            {
                return;
            }

            _bodies.Clear();
            _aabbColliders.Clear();
            IncrementRevision();
        }

        public void UpsertBody(CombatPhysicsBody body)
        {
            int index = FindBodyIndex(body.BodyId);
            if (index >= 0)
            {
                if (_bodies[index].Equals(body))
                {
                    return;
                }

                _bodies[index] = body;
                IncrementRevision();
                return;
            }

            _bodies.Add(body);
            IncrementRevision();
        }

        public bool RemoveBody(CombatBodyId bodyId)
        {
            int index = FindBodyIndex(bodyId);
            if (index < 0)
            {
                return false;
            }

            _bodies.RemoveAt(index);
            for (int i = _aabbColliders.Count - 1; i >= 0; i--)
            {
                if (_aabbColliders[i].BodyId.Equals(bodyId))
                {
                    _aabbColliders.RemoveAt(i);
                }
            }

            IncrementRevision();
            return true;
        }

        public void UpsertAabbCollider(CombatPhysicsAabbCollider collider)
        {
            if (FindBodyIndex(collider.BodyId) < 0)
            {
                throw new InvalidOperationException("Cannot register a collider before its body is registered.");
            }

            int index = FindAabbColliderIndex(collider.BodyId, collider.ColliderId);
            if (index >= 0)
            {
                if (_aabbColliders[index].Equals(collider))
                {
                    return;
                }

                _aabbColliders[index] = collider;
                IncrementRevision();
                return;
            }

            _aabbColliders.Add(collider);
            IncrementRevision();
        }

        public bool TryGetBody(CombatBodyId bodyId, out CombatPhysicsBody body)
        {
            int index = FindBodyIndex(bodyId);
            if (index < 0)
            {
                body = default;
                return false;
            }

            body = _bodies[index];
            return true;
        }

        public bool TryGetAabbCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            out CombatPhysicsAabbCollider collider)
        {
            int index = FindAabbColliderIndex(bodyId, colliderId);
            if (index < 0)
            {
                collider = default;
                return false;
            }

            collider = _aabbColliders[index];
            return true;
        }

        public bool MoveBody(CombatBodyId bodyId, FixVector3 delta)
        {
            if (!TryGetBody(bodyId, out CombatPhysicsBody body))
            {
                return false;
            }

            return SetBodyPosition(bodyId, body.Position + delta);
        }

        public bool SetBodyPosition(CombatBodyId bodyId, FixVector3 position)
        {
            int index = FindBodyIndex(bodyId);
            if (index < 0)
            {
                return false;
            }

            CombatPhysicsBody body = _bodies[index];
            if (body.Position.Equals(position))
            {
                return true;
            }

            _bodies[index] = new CombatPhysicsBody(body.EntityId, body.BodyId, position);
            IncrementRevision();
            return true;
        }

        public CombatPhysicsWorldStats CreateStats()
        {
            return new CombatPhysicsWorldStats(_revision, _bodies.Count, _aabbColliders.Count);
        }

        public int CopyBodiesTo(List<CombatPhysicsBody> bodies)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            int startCount = bodies.Count;
            for (int i = 0; i < _bodies.Count; i++)
            {
                bodies.Add(_bodies[i]);
            }

            bodies.Sort(startCount, _bodies.Count, CombatPhysicsBodyComparer.Instance);
            return _bodies.Count;
        }

        public int CopyAabbCollidersTo(List<CombatPhysicsAabbCollider> colliders)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            int startCount = colliders.Count;
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                colliders.Add(_aabbColliders[i]);
            }

            colliders.Sort(startCount, _aabbColliders.Count, CombatPhysicsAabbColliderComparer.Instance);
            return _aabbColliders.Count;
        }

        public int Query(in CombatPhysicsQuery query, List<CombatQueryResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            switch (query.Shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                case CombatPhysicsShapeKind.Sphere:
                case CombatPhysicsShapeKind.Capsule:
                case CombatPhysicsShapeKind.Aabb:
                case CombatPhysicsShapeKind.Sector:
                case CombatPhysicsShapeKind.Obb:
                    return QueryBroadphase(query, results);
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }
        }

        public int QueryBatch(in CombatPhysicsQueryBatch batch, List<CombatPhysicsQueryBatchResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int startCount = results.Count;
            for (int i = 0; i < batch.Count; i++)
            {
                CombatPhysicsQuery query = batch[i];
                var hits = new List<CombatQueryResult>();
                Query(query, hits);
                CombatPhysicsQueryDebugReport report = ExplainQuery(query);

                results.Add(new CombatPhysicsQueryBatchResult(query, i, hits, report));
            }

            SortBatchResults(results);
            return results.Count - startCount;
        }

        public CombatPhysicsQueryDebugReport ExplainQuery(in CombatPhysicsQuery query)
        {
            var rows = new List<CombatPhysicsQueryDebugRow>();
            int candidateCount = 0;
            int filteredSourceCount = 0;
            int filteredLayerCount = 0;
            int hitCount = 0;
            int postFilterCandidateCount = 0;
            CombatPhysicsBroadphaseResult broadphase = _broadphase.CollectCandidates(query.Shape, _bodies, _aabbColliders);
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates = broadphase.Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                CombatPhysicsBroadphaseCandidate candidate = candidates[i];
                CombatPhysicsAabbCollider collider = candidate.Collider;
                CombatPhysicsBody body = candidate.Body;
                int candidateIndex = candidateCount;
                candidateCount++;
                if (!query.Header.LayerMask.ContainsLayer(collider.Layer))
                {
                    filteredLayerCount++;
                    rows.Add(new CombatPhysicsQueryDebugRow(
                        candidateIndex,
                        body.EntityId,
                        body.BodyId,
                        collider.ColliderId,
                        collider.Layer,
                        CombatPhysicsQueryDebugRowStatus.FilteredLayer));
                    continue;
                }

                if (!query.Filter.IncludeSourceEntity && query.Header.SourceEntityId.Equals(body.EntityId))
                {
                    filteredSourceCount++;
                    rows.Add(new CombatPhysicsQueryDebugRow(
                        candidateIndex,
                        body.EntityId,
                        body.BodyId,
                        collider.ColliderId,
                        collider.Layer,
                        CombatPhysicsQueryDebugRowStatus.FilteredSource));
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                postFilterCandidateCount++;
                CombatPhysicsQueryDebugRowStatus status = TryShapeHitAabb(query.Shape, min, max)
                    ? CombatPhysicsQueryDebugRowStatus.Hit
                    : CombatPhysicsQueryDebugRowStatus.Miss;
                if (status == CombatPhysicsQueryDebugRowStatus.Hit)
                {
                    hitCount++;
                }

                rows.Add(new CombatPhysicsQueryDebugRow(
                    candidateIndex,
                    body.EntityId,
                    body.BodyId,
                    collider.ColliderId,
                    collider.Layer,
                    status));
            }

            return new CombatPhysicsQueryDebugReport(
                query,
                candidateCount,
                filteredSourceCount,
                filteredLayerCount,
                hitCount,
                string.Empty,
                rows,
                broadphase.RawCandidateCount,
                broadphase.Candidates.Count,
                postFilterCandidateCount,
                broadphase.CellCount);
        }

        public bool RemoveCollider(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            int index = FindAabbColliderIndex(bodyId, colliderId);
            if (index < 0)
            {
                return false;
            }

            _aabbColliders.RemoveAt(index);
            IncrementRevision();
            return true;
        }

        private int QueryBroadphase(in CombatPhysicsQuery query, List<CombatQueryResult> results)
        {
            int startCount = results.Count;
            CombatPhysicsBroadphaseResult broadphase = _broadphase.CollectCandidates(query.Shape, _bodies, _aabbColliders);
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates = broadphase.Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                CombatPhysicsBroadphaseCandidate candidate = candidates[i];
                if (!CanHit(query.Header, candidate.Body, candidate.Collider.Layer, query.Filter.IncludeSourceEntity))
                {
                    continue;
                }

                if (TryBuildQueryResult(query, candidate, out CombatQueryResult result))
                {
                    results.Add(result);
                }
            }

            SortResults(results);
            return results.Count - startCount;
        }

        private static bool TryBuildQueryResult(
            in CombatPhysicsQuery query,
            in CombatPhysicsBroadphaseCandidate candidate,
            out CombatQueryResult result)
        {
            switch (query.Shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                    if (!query.Shape.Direction.TryNormalize(out FixVector3 rayDirection))
                    {
                        throw new ArgumentException("Ray direction cannot be zero.", nameof(query));
                    }

                    if (TryRaycastAabb(
                        query.Shape.Origin,
                        rayDirection,
                        query.Shape.Length,
                        candidate.Min,
                        candidate.Max,
                        out Fix64 rayDistance,
                        out FixVector3 rayPoint,
                        out FixVector3 rayNormal))
                    {
                        result = new CombatQueryResult(
                            query.Header,
                            candidate.Body.EntityId,
                            candidate.Body.BodyId,
                            candidate.Collider.ColliderId,
                            rayDistance,
                            rayPoint,
                            rayNormal);
                        return true;
                    }

                    break;
                case CombatPhysicsShapeKind.Sphere:
                    Fix64 sphereRadiusSquared = query.Shape.Radius * query.Shape.Radius;
                    FixVector3 spherePoint = ClosestPointOnAabb(query.Shape.Center, candidate.Min, candidate.Max);
                    FixVector3 sphereDelta = query.Shape.Center - spherePoint;
                    Fix64 sphereDistanceSquared = sphereDelta.LengthSquared();
                    if (sphereDistanceSquared <= sphereRadiusSquared)
                    {
                        Fix64 distance = sphereDistanceSquared.IsZero ? Fix64.Zero : sphereDistanceSquared.Sqrt();
                        FixVector3 normal = sphereDelta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                        result = new CombatQueryResult(
                            query.Header,
                            candidate.Body.EntityId,
                            candidate.Body.BodyId,
                            candidate.Collider.ColliderId,
                            distance,
                            spherePoint,
                            normal);
                        return true;
                    }

                    break;
                case CombatPhysicsShapeKind.Capsule:
                    Fix64 capsuleRadiusSquared = query.Shape.Radius * query.Shape.Radius;
                    FixVector3 segment = query.Shape.PointB - query.Shape.PointA;
                    Fix64 segmentLengthSquared = segment.LengthSquared();
                    if (TryCapsuleAabb(
                        query.Shape.PointA,
                        query.Shape.PointB,
                        segment,
                        segmentLengthSquared,
                        query.Shape.Radius,
                        capsuleRadiusSquared,
                        candidate.Min,
                        candidate.Max,
                        out Fix64 capsuleDistance,
                        out FixVector3 capsulePoint,
                        out FixVector3 capsuleNormal))
                    {
                        result = new CombatQueryResult(
                            query.Header,
                            candidate.Body.EntityId,
                            candidate.Body.BodyId,
                            candidate.Collider.ColliderId,
                            capsuleDistance,
                            capsulePoint,
                            capsuleNormal);
                        return true;
                    }

                    break;
                case CombatPhysicsShapeKind.Aabb:
                    if (Overlaps(query.Shape.Min, query.Shape.Max, candidate.Min, candidate.Max))
                    {
                        result = new CombatQueryResult(
                            query.Header,
                            candidate.Body.EntityId,
                            candidate.Body.BodyId,
                            candidate.Collider.ColliderId,
                            Fix64.Zero,
                            GetOverlapCenter(query.Shape.Min, query.Shape.Max, candidate.Min, candidate.Max),
                            FixVector3.Zero);
                        return true;
                    }

                    break;
                case CombatPhysicsShapeKind.Sector:
                    if (!query.Shape.Direction.TryNormalize(out FixVector3 sectorDirection))
                    {
                        throw new ArgumentException("Sector direction cannot be zero.", nameof(query));
                    }

                    FixVector3 sectorPoint = ClosestPointOnAabb(query.Shape.Origin, candidate.Min, candidate.Max);
                    FixVector3 toPoint = sectorPoint - query.Shape.Origin;
                    Fix64 sectorDistanceSquared = toPoint.LengthSquared();
                    if (sectorDistanceSquared <= query.Shape.Radius * query.Shape.Radius)
                    {
                        if (!sectorDistanceSquared.IsZero)
                        {
                            FixVector3 toPointDirection = toPoint / sectorDistanceSquared.Sqrt();
                            if (sectorDirection.Dot(toPointDirection) < query.Shape.MinDot)
                            {
                                break;
                            }
                        }

                        Fix64 sectorDistance = sectorDistanceSquared.IsZero ? Fix64.Zero : sectorDistanceSquared.Sqrt();
                        FixVector3 sectorNormal = toPoint.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                        result = new CombatQueryResult(
                            query.Header,
                            candidate.Body.EntityId,
                            candidate.Body.BodyId,
                            candidate.Collider.ColliderId,
                            sectorDistance,
                            sectorPoint,
                            sectorNormal);
                        return true;
                    }

                    break;
                case CombatPhysicsShapeKind.Obb:
                    if (CombatPhysicsObbMath.OverlapsAabb(
                        query.Shape.Center,
                        query.Shape.HalfExtents,
                        query.Shape.AxisX,
                        query.Shape.AxisY,
                        query.Shape.AxisZ,
                        candidate.Min,
                        candidate.Max))
                    {
                        FixVector3 obbPoint = ClosestPointOnAabb(query.Shape.Center, candidate.Min, candidate.Max);
                        FixVector3 obbDelta = obbPoint - query.Shape.Center;
                        FixVector3 obbNormal = obbDelta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                        result = new CombatQueryResult(
                            query.Header,
                            candidate.Body.EntityId,
                            candidate.Body.BodyId,
                            candidate.Collider.ColliderId,
                            Fix64.Zero,
                            obbPoint,
                            obbNormal);
                        return true;
                    }

                    break;
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }

            result = default;
            return false;
        }

        public int QueryRay(CombatRayQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!query.Direction.TryNormalize(out FixVector3 direction))
            {
                throw new ArgumentException("Ray direction cannot be zero.", nameof(query));
            }

            int startCount = results.Count;
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                if (!TryGetBody(collider.BodyId, out CombatPhysicsBody body) || !CanHit(query.Header, body, collider.Layer, includeSourceEntity))
                {
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                if (TryRaycastAabb(query.Origin, direction, query.Length, min, max, out Fix64 distance, out FixVector3 point, out FixVector3 normal))
                {
                    results.Add(new CombatQueryResult(query.Header, body.EntityId, body.BodyId, collider.ColliderId, distance, point, normal));
                }
            }

            SortResults(results);
            return results.Count - startCount;
        }

        public int QuerySphere(CombatSphereQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int startCount = results.Count;
            Fix64 radiusSquared = query.Radius * query.Radius;
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                if (!TryGetBody(collider.BodyId, out CombatPhysicsBody body) || !CanHit(query.Header, body, collider.Layer, includeSourceEntity))
                {
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                FixVector3 point = ClosestPointOnAabb(query.Center, min, max);
                FixVector3 delta = query.Center - point;
                Fix64 distanceSquared = delta.LengthSquared();
                if (distanceSquared <= radiusSquared)
                {
                    Fix64 distance = distanceSquared.IsZero ? Fix64.Zero : distanceSquared.Sqrt();
                    FixVector3 normal = delta.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                    results.Add(new CombatQueryResult(query.Header, body.EntityId, body.BodyId, collider.ColliderId, distance, point, normal));
                }
            }

            SortResults(results);
            return results.Count - startCount;
        }

        public int QueryCapsule(CombatCapsuleQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int startCount = results.Count;
            Fix64 radiusSquared = query.Radius * query.Radius;
            FixVector3 segment = query.PointB - query.PointA;
            Fix64 segmentLengthSquared = segment.LengthSquared();
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                if (!TryGetBody(collider.BodyId, out CombatPhysicsBody body) || !CanHit(query.Header, body, collider.Layer, includeSourceEntity))
                {
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                if (TryCapsuleAabb(
                    query.PointA,
                    query.PointB,
                    segment,
                    segmentLengthSquared,
                    query.Radius,
                    radiusSquared,
                    min,
                    max,
                    out Fix64 distance,
                    out FixVector3 point,
                    out FixVector3 normal))
                {
                    results.Add(new CombatQueryResult(query.Header, body.EntityId, body.BodyId, collider.ColliderId, distance, point, normal));
                }
            }

            SortResults(results);
            return results.Count - startCount;
        }

        public int QueryAabb(CombatAabbQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int startCount = results.Count;
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                if (!TryGetBody(collider.BodyId, out CombatPhysicsBody body) || !CanHit(query.Header, body, collider.Layer, includeSourceEntity))
                {
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                if (Overlaps(query.Min, query.Max, min, max))
                {
                    FixVector3 point = GetOverlapCenter(query.Min, query.Max, min, max);
                    results.Add(new CombatQueryResult(query.Header, body.EntityId, body.BodyId, collider.ColliderId, Fix64.Zero, point, FixVector3.Zero));
                }
            }

            SortResults(results);
            return results.Count - startCount;
        }

        public int QuerySector(CombatSectorQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!query.Direction.TryNormalize(out FixVector3 direction))
            {
                throw new ArgumentException("Sector direction cannot be zero.", nameof(query));
            }

            int startCount = results.Count;
            Fix64 radiusSquared = query.Radius * query.Radius;
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                if (!TryGetBody(collider.BodyId, out CombatPhysicsBody body) || !CanHit(query.Header, body, collider.Layer, includeSourceEntity))
                {
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                FixVector3 point = ClosestPointOnAabb(query.Origin, min, max);
                FixVector3 toPoint = point - query.Origin;
                Fix64 distanceSquared = toPoint.LengthSquared();
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                if (!distanceSquared.IsZero)
                {
                    FixVector3 toPointDirection = toPoint / distanceSquared.Sqrt();
                    if (direction.Dot(toPointDirection) < query.MinDot)
                    {
                        continue;
                    }
                }

                Fix64 distance = distanceSquared.IsZero ? Fix64.Zero : distanceSquared.Sqrt();
                FixVector3 normal = toPoint.TryNormalize(out FixVector3 normalized) ? normalized : FixVector3.Zero;
                results.Add(new CombatQueryResult(query.Header, body.EntityId, body.BodyId, collider.ColliderId, distance, point, normal));
            }

            SortResults(results);
            return results.Count - startCount;
        }

        public void SortResults(List<CombatQueryResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Sort();
        }

        public void SortBatchResults(List<CombatPhysicsQueryBatchResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Sort(CompareBatchResults);
        }

        private static bool CanHit(
            CombatQueryHeader header,
            CombatPhysicsBody body,
            int colliderLayer,
            bool includeSourceEntity)
        {
            if (!header.LayerMask.ContainsLayer(colliderLayer))
            {
                return false;
            }

            return includeSourceEntity || !header.SourceEntityId.Equals(body.EntityId);
        }

        private static int CompareBatchResults(CombatPhysicsQueryBatchResult left, CombatPhysicsQueryBatchResult right)
        {
            int compare = CombatPhysicsQuery.CompareHeaders(left.Query.Header, right.Query.Header);
            if (compare != 0)
            {
                return compare;
            }

            return left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static bool TryShapeHitAabb(CombatPhysicsShape shape, FixVector3 min, FixVector3 max)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                    if (!shape.Direction.TryNormalize(out FixVector3 rayDirection))
                    {
                        throw new ArgumentException("Ray direction cannot be zero.", nameof(shape));
                    }

                    return TryRaycastAabb(
                        shape.Origin,
                        rayDirection,
                        shape.Length,
                        min,
                        max,
                        out Fix64 _,
                        out FixVector3 _,
                        out FixVector3 _);
                case CombatPhysicsShapeKind.Sphere:
                    FixVector3 spherePoint = ClosestPointOnAabb(shape.Center, min, max);
                    Fix64 sphereDistanceSquared = (shape.Center - spherePoint).LengthSquared();
                    return sphereDistanceSquared <= shape.Radius * shape.Radius;
                case CombatPhysicsShapeKind.Capsule:
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
                        out Fix64 _,
                        out FixVector3 _,
                        out FixVector3 _);
                case CombatPhysicsShapeKind.Aabb:
                    return Overlaps(shape.Min, shape.Max, min, max);
                case CombatPhysicsShapeKind.Sector:
                    if (!shape.Direction.TryNormalize(out FixVector3 sectorDirection))
                    {
                        throw new ArgumentException("Sector direction cannot be zero.", nameof(shape));
                    }

                    FixVector3 sectorPoint = ClosestPointOnAabb(shape.Origin, min, max);
                    FixVector3 toPoint = sectorPoint - shape.Origin;
                    Fix64 sectorDistanceSquared = toPoint.LengthSquared();
                    if (sectorDistanceSquared > shape.Radius * shape.Radius)
                    {
                        return false;
                    }

                    if (sectorDistanceSquared.IsZero)
                    {
                        return true;
                    }

                    FixVector3 toPointDirection = toPoint / sectorDistanceSquared.Sqrt();
                    return sectorDirection.Dot(toPointDirection) >= shape.MinDot;
                case CombatPhysicsShapeKind.Obb:
                    return CombatPhysicsObbMath.OverlapsAabb(
                        shape.Center,
                        shape.HalfExtents,
                        shape.AxisX,
                        shape.AxisY,
                        shape.AxisZ,
                        min,
                        max);
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }
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
            Fix64 rightDotRelative = rightDirection.Dot(relative);

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
                rightT = Fix64.Clamp(rightDotRelative / rightLengthSquared, Fix64.Zero, Fix64.One);
            }
            else
            {
                Fix64 leftDotRelative = leftDirection.Dot(relative);
                if (rightLengthSquared.IsZero)
                {
                    rightT = Fix64.Zero;
                    leftT = Fix64.Clamp(-leftDotRelative / leftLengthSquared, Fix64.Zero, Fix64.One);
                }
                else
                {
                    Fix64 leftDotRight = leftDirection.Dot(rightDirection);
                    Fix64 denominator = leftLengthSquared * rightLengthSquared - leftDotRight * leftDotRight;
                    leftT = denominator.IsZero
                        ? Fix64.Zero
                        : Fix64.Clamp((leftDotRight * rightDotRelative - leftDotRelative * rightLengthSquared) / denominator, Fix64.Zero, Fix64.One);
                    rightT = (leftDotRight * leftT + rightDotRelative) / rightLengthSquared;
                    if (rightT < Fix64.Zero)
                    {
                        rightT = Fix64.Zero;
                        leftT = Fix64.Clamp(-leftDotRelative / leftLengthSquared, Fix64.Zero, Fix64.One);
                    }
                    else if (rightT > Fix64.One)
                    {
                        rightT = Fix64.One;
                        leftT = Fix64.Clamp((leftDotRight - leftDotRelative) / leftLengthSquared, Fix64.Zero, Fix64.One);
                    }
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

        private int FindBodyIndex(CombatBodyId bodyId)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_bodies[i].BodyId.Equals(bodyId))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindAabbColliderIndex(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                if (_aabbColliders[i].BodyId.Equals(bodyId) && _aabbColliders[i].ColliderId.Equals(colliderId))
                {
                    return i;
                }
            }

            return -1;
        }

        private void IncrementRevision()
        {
            unchecked
            {
                _revision++;
            }
        }

        private sealed class CombatPhysicsBodyComparer : IComparer<CombatPhysicsBody>
        {
            public static readonly CombatPhysicsBodyComparer Instance = new CombatPhysicsBodyComparer();

            private CombatPhysicsBodyComparer()
            {
            }

            public int Compare(CombatPhysicsBody left, CombatPhysicsBody right)
            {
                int compare = left.EntityId.CompareTo(right.EntityId);
                if (compare != 0)
                {
                    return compare;
                }

                return left.BodyId.CompareTo(right.BodyId);
            }
        }

        private sealed class CombatPhysicsAabbColliderComparer : IComparer<CombatPhysicsAabbCollider>
        {
            public static readonly CombatPhysicsAabbColliderComparer Instance = new CombatPhysicsAabbColliderComparer();

            private CombatPhysicsAabbColliderComparer()
            {
            }

            public int Compare(CombatPhysicsAabbCollider left, CombatPhysicsAabbCollider right)
            {
                int compare = left.BodyId.CompareTo(right.BodyId);
                if (compare != 0)
                {
                    return compare;
                }

                compare = left.ColliderId.CompareTo(right.ColliderId);
                if (compare != 0)
                {
                    return compare;
                }

                return left.Layer.CompareTo(right.Layer);
            }
        }
    }

    public readonly struct CombatPhysicsWorldStats : IEquatable<CombatPhysicsWorldStats>
    {
        public CombatPhysicsWorldStats(int revision, int bodyCount, int colliderCount)
        {
            Revision = revision;
            BodyCount = bodyCount;
            ColliderCount = colliderCount;
        }

        public int Revision { get; }

        public int BodyCount { get; }

        public int ColliderCount { get; }

        public bool Equals(CombatPhysicsWorldStats other)
        {
            return Revision == other.Revision
                && BodyCount == other.BodyCount
                && ColliderCount == other.ColliderCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsWorldStats other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision;
                hash = (hash * 397) ^ BodyCount;
                hash = (hash * 397) ^ ColliderCount;
                return hash;
            }
        }
    }
}
