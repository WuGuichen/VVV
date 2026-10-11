using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
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
                _queryBatchHitsScratch.Clear();
                Query(query, _queryBatchHitsScratch);
                IReadOnlyList<CombatQueryResult> hits = SnapshotQueryBatchHits(_queryBatchHitsScratch);
                CombatPhysicsQueryDebugReport report = ExplainQuery(query);

                results.Add(new CombatPhysicsQueryBatchResult(query, i, hits, report));
            }

            SortBatchResultsRange(results, startCount, results.Count - startCount);
            _queryBatchHitsScratch.Clear();
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
            CombatPhysicsBroadphaseResult broadphase = _broadphase.CollectCandidates(
                query.Shape,
                _topologyRevision,
                _bodyRevision,
                _movedBodies,
                _bodies,
                _aabbColliders,
                _sphereColliders,
                _capsuleColliders,
                _obbColliders,
                CombatPhysicsBroadphaseCandidateOrdering.Sorted);
            ClearMovedBodies();
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates = broadphase.Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                CombatPhysicsBroadphaseCandidate candidate = candidates[i];
                CombatPhysicsBody body = candidate.Body;
                int candidateIndex = candidateCount;
                candidateCount++;
                if (!query.Header.LayerMask.ContainsLayer(candidate.Layer))
                {
                    filteredLayerCount++;
                    rows.Add(new CombatPhysicsQueryDebugRow(
                        candidateIndex,
                        body.EntityId,
                        body.BodyId,
                        candidate.ColliderId,
                        candidate.Layer,
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
                        candidate.ColliderId,
                        candidate.Layer,
                        CombatPhysicsQueryDebugRowStatus.FilteredSource));
                    continue;
                }

                postFilterCandidateCount++;
                CombatPhysicsQueryDebugRowStatus status = TryShapeHitTarget(
                        query.Shape,
                        candidate.Shape,
                        out Fix64 _,
                        out FixVector3 _,
                        out FixVector3 _)
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
                        candidate.ColliderId,
                        candidate.Layer,
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

        private int QueryBroadphase(in CombatPhysicsQuery query, List<CombatQueryResult> results)
        {
            if (query.Header.LayerMask.Value == 0)
            {
                return 0;
            }

            int startCount = results.Count;
            // Explicit character hurtboxes live entirely in compounds. Avoid rebuilding and
            // traversing the flat broadphase when a world contains no legacy primitive
            // colliders; the compound root AABBs are already the global coarse phase.
            if (_aabbColliders.Count == 0
                && _sphereColliders.Count == 0
                && _capsuleColliders.Count == 0
                && _obbColliders.Count == 0)
            {
                QueryCompounds(query, results);
                SortResultsRange(results, startCount, results.Count - startCount);
                return results.Count - startCount;
            }

            CombatPhysicsBroadphaseResult broadphase = CollectBroadphaseCandidates(
                query,
                CombatPhysicsBroadphaseCandidateOrdering.Unsorted);
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates = broadphase.Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                CombatPhysicsBroadphaseCandidate candidate = candidates[i];
                if (TryBuildQueryResult(query, candidate, out CombatQueryResult result))
                {
                    results.Add(result);
                }
            }

            QueryCompounds(query, results);

            SortResultsRange(results, startCount, results.Count - startCount);
            return results.Count - startCount;
        }

        internal CombatPhysicsBroadphaseResult CollectBroadphaseCandidates(
            in CombatPhysicsQuery query,
            CombatPhysicsBroadphaseCandidateOrdering ordering)
        {
            CombatPhysicsBroadphaseResult result = _broadphase.CollectCandidates(
                query.Shape,
                _topologyRevision,
                _bodyRevision,
                _movedBodies,
                _bodies,
                _aabbColliders,
                _sphereColliders,
                _capsuleColliders,
                _obbColliders,
                ordering,
                query.Header.SourceEntityId,
                query.Header.LayerMask,
                query.Filter.IncludeSourceEntity);
            ClearMovedBodies();
            return result;
        }

        internal CombatPhysicsBroadphaseResult CollectBroadphaseCandidatesAabb(
            FixVector3 queryMin,
            FixVector3 queryMax,
            CombatPhysicsBroadphaseCandidateOrdering ordering)
        {
            CombatPhysicsBroadphaseResult result = _broadphase.CollectCandidatesAabb(
                queryMin,
                queryMax,
                _topologyRevision,
                _bodyRevision,
                _movedBodies,
                _bodies,
                _aabbColliders,
                _sphereColliders,
                _capsuleColliders,
                _obbColliders,
                ordering);
            ClearMovedBodies();
            return result;
        }

        internal CombatPhysicsBroadphaseResult CollectBroadphaseCandidatesAabb(
            FixVector3 queryMin,
            FixVector3 queryMax,
            CombatPhysicsBroadphaseCandidateOrdering ordering,
            CombatEntityId sourceEntityId,
            CombatPhysicsLayerMask layerMask,
            bool includeSourceEntity)
        {
            CombatPhysicsBroadphaseResult result = _broadphase.CollectCandidatesAabb(
                queryMin,
                queryMax,
                _topologyRevision,
                _bodyRevision,
                _movedBodies,
                _bodies,
                _aabbColliders,
                _sphereColliders,
                _capsuleColliders,
                _obbColliders,
                ordering,
                sourceEntityId,
                layerMask,
                includeSourceEntity);
            ClearMovedBodies();
            return result;
        }

        internal static bool CanHitBroadphaseCandidate(
            in CombatPhysicsQuery query,
            in CombatPhysicsBroadphaseCandidate candidate)
        {
            return CanHit(
                query.Header,
                candidate.Body,
                candidate.Layer,
                query.Filter.IncludeSourceEntity);
        }

        internal static bool CanHitBroadphaseCandidate(
            CombatEntityId sourceEntityId,
            CombatPhysicsLayerMask layerMask,
            bool includeSourceEntity,
            in CombatPhysicsBroadphaseCandidate candidate)
        {
            if (!layerMask.ContainsLayer(candidate.Layer))
            {
                return false;
            }

            return includeSourceEntity || !sourceEntityId.Equals(candidate.Body.EntityId);
        }

        private static bool TryBuildQueryResult(
            in CombatPhysicsQuery query,
            in CombatPhysicsBroadphaseCandidate candidate,
            out CombatQueryResult result)
        {
            if (TryShapeHitTarget(
                    query.Shape,
                    candidate.Shape,
                    out Fix64 distance,
                    out FixVector3 point,
                    out FixVector3 normal))
            {
                result = new CombatQueryResult(
                    query.Header,
                    candidate.Body.EntityId,
                    candidate.Body.BodyId,
                    candidate.ColliderId,
                    candidate.ShapeKind,
                    distance,
                    point,
                    normal,
                    candidate.Priority);
                return true;
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

            return Query(CombatPhysicsQuery.From(query, new CombatPhysicsQueryFilter(includeSourceEntity)), results);
        }

        public int QuerySphere(CombatSphereQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            return Query(CombatPhysicsQuery.From(query, new CombatPhysicsQueryFilter(includeSourceEntity)), results);
        }

        public int QueryCapsule(CombatCapsuleQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            return Query(CombatPhysicsQuery.From(query, new CombatPhysicsQueryFilter(includeSourceEntity)), results);
        }

        public int QueryAabb(CombatAabbQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            return Query(CombatPhysicsQuery.From(query, new CombatPhysicsQueryFilter(includeSourceEntity)), results);
        }

        public int QuerySector(CombatSectorQuery query, List<CombatQueryResult> results, bool includeSourceEntity = false)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            return Query(CombatPhysicsQuery.From(query, new CombatPhysicsQueryFilter(includeSourceEntity)), results);
        }

        private static IReadOnlyList<CombatQueryResult> SnapshotQueryBatchHits(
            List<CombatQueryResult> hits)
        {
            if (hits == null || hits.Count == 0)
                return Array.Empty<CombatQueryResult>();

            var snapshot = new CombatQueryResult[hits.Count];
            for (int i = 0; i < hits.Count; i++)
                snapshot[i] = hits[i];
            return snapshot;
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
    }
}
