using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    internal readonly struct CombatPhysicsBroadphaseCandidate
    {
        public CombatPhysicsBroadphaseCandidate(
            int colliderIndex,
            int sourceIndex,
            CombatPhysicsBody body,
            CombatPhysicsShapeKind shapeKind,
            CombatColliderId colliderId,
            int layer,
            CombatSurfaceMaterial surfaceMaterial,
            int priority,
            CombatPhysicsShape shape,
            FixVector3 min,
            FixVector3 max)
        {
            ColliderIndex = colliderIndex;
            SourceIndex = sourceIndex;
            Body = body;
            ShapeKind = shapeKind;
            ColliderId = colliderId;
            Layer = layer;
            SurfaceMaterial = surfaceMaterial;
            Priority = priority;
            Shape = shape;
            Min = min;
            Max = max;
        }

        public int ColliderIndex { get; }
        public int SourceIndex { get; }
        public CombatPhysicsBody Body { get; }
        public CombatPhysicsShapeKind ShapeKind { get; }
        public CombatColliderId ColliderId { get; }
        public int Layer { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int Priority { get; }
        public CombatPhysicsShape Shape { get; }
        public FixVector3 Min { get; }
        public FixVector3 Max { get; }
    }

    internal enum CombatPhysicsBroadphaseCandidateOrdering
    {
        Unsorted = 0,
        Sorted = 1,
    }

    internal readonly struct CombatPhysicsBroadphaseResult
    {
        public CombatPhysicsBroadphaseResult(
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates,
            int rawCandidateCount,
            int cellCount,
            FixVector3 queryMin,
            FixVector3 queryMax,
            bool candidatesSorted)
        {
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            RawCandidateCount = rawCandidateCount;
            CellCount = cellCount;
            QueryMin = queryMin;
            QueryMax = queryMax;
            CandidatesSorted = candidatesSorted;
        }

        // Backed by broadphase scratch storage; valid until the next CollectCandidates call on the same instance.
        public IReadOnlyList<CombatPhysicsBroadphaseCandidate> Candidates { get; }
        public int RawCandidateCount { get; }
        public int CellCount { get; }
        public FixVector3 QueryMin { get; }
        public FixVector3 QueryMax { get; }
        public bool CandidatesSorted { get; }
    }

    internal sealed class CombatPhysicsBroadphase
    {
        private readonly CombatPhysicsBroadphaseConfig _config;
        private readonly List<CombatPhysicsBroadphaseCandidate> _entries = new List<CombatPhysicsBroadphaseCandidate>();
        private readonly List<CombatPhysicsBroadphaseCell> _entryMinCells = new List<CombatPhysicsBroadphaseCell>();
        private readonly List<CombatPhysicsBroadphaseCell> _entryMaxCells = new List<CombatPhysicsBroadphaseCell>();
        private readonly List<CombatPhysicsBroadphaseCandidate> _candidates = new List<CombatPhysicsBroadphaseCandidate>();
        private readonly List<CombatPhysicsBody> _indexedBodies = new List<CombatPhysicsBody>();
        private readonly Dictionary<CombatBodyId, int> _indexedBodyIndices = new Dictionary<CombatBodyId, int>();
        private readonly List<CombatPhysicsAabbCollider> _indexedAabbColliders = new List<CombatPhysicsAabbCollider>();
        private readonly List<CombatPhysicsSphereCollider> _indexedSphereColliders = new List<CombatPhysicsSphereCollider>();
        private readonly List<CombatPhysicsCapsuleCollider> _indexedCapsuleColliders = new List<CombatPhysicsCapsuleCollider>();
        private readonly List<CombatPhysicsObbCollider> _indexedObbColliders = new List<CombatPhysicsObbCollider>();
        private readonly List<int> _candidateQueryStamps = new List<int>();
        private readonly Dictionary<CombatPhysicsBroadphaseCell, List<int>> _cells = new Dictionary<CombatPhysicsBroadphaseCell, List<int>>();
        private readonly Dictionary<CombatBodyId, List<int>> _entryIndicesByBody = new Dictionary<CombatBodyId, List<int>>();
        private readonly List<List<int>> _activeCellLists = new List<List<int>>();
        private readonly Dictionary<List<int>, int> _activeCellListIndices = new Dictionary<List<int>, int>();
        private readonly List<List<int>> _cellListPool = new List<List<int>>();
        private readonly List<List<int>> _activeEntryIndexLists = new List<List<int>>();
        private readonly List<List<int>> _entryIndexListPool = new List<List<int>>();
        private bool _hasIndex;
        private int _indexedTopologyRevision;
        private int _indexedBodyRevision;
        private int _candidateQueryStamp;

        public CombatPhysicsBroadphase(CombatPhysicsBroadphaseConfig config)
        {
            _config = config;
        }

        public void PrepareIndex(
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> movedBodies,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            if (aabbColliders == null)
            {
                throw new ArgumentNullException(nameof(aabbColliders));
            }

            if (sphereColliders == null)
            {
                throw new ArgumentNullException(nameof(sphereColliders));
            }

            if (capsuleColliders == null)
            {
                throw new ArgumentNullException(nameof(capsuleColliders));
            }
            if (obbColliders == null) throw new ArgumentNullException(nameof(obbColliders));

            int colliderCount = aabbColliders.Count + sphereColliders.Count + capsuleColliders.Count + obbColliders.Count;
            EnsureIndex(
                topologyRevision,
                bodyRevision,
                movedBodies,
                bodies,
                aabbColliders,
                sphereColliders,
                capsuleColliders,
                obbColliders,
                colliderCount);
        }

        public CombatPhysicsBroadphaseResult CollectCandidates(
            CombatPhysicsShape shape,
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> movedBodies,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders,
            CombatPhysicsBroadphaseCandidateOrdering ordering)
        {
            GetQueryAabb(shape, out FixVector3 queryMin, out FixVector3 queryMax);
            return CollectCandidatesAabb(
                queryMin,
                queryMax,
                topologyRevision,
                bodyRevision,
                movedBodies,
                bodies,
                aabbColliders,
                sphereColliders,
                capsuleColliders,
                obbColliders,
                ordering);
        }

        public CombatPhysicsBroadphaseResult CollectCandidates(
            CombatPhysicsShape shape,
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> movedBodies,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders,
            CombatPhysicsBroadphaseCandidateOrdering ordering,
            CombatEntityId sourceEntityId,
            CombatPhysicsLayerMask layerMask,
            bool includeSourceEntity)
        {
            GetQueryAabb(shape, out FixVector3 queryMin, out FixVector3 queryMax);
            return CollectCandidatesAabb(
                queryMin,
                queryMax,
                topologyRevision,
                bodyRevision,
                movedBodies,
                bodies,
                aabbColliders,
                sphereColliders,
                capsuleColliders,
                obbColliders,
                ordering,
                sourceEntityId,
                layerMask,
                includeSourceEntity);
        }

        public CombatPhysicsBroadphaseResult CollectCandidatesAabb(
            FixVector3 queryMin,
            FixVector3 queryMax,
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> movedBodies,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders,
            CombatPhysicsBroadphaseCandidateOrdering ordering)
        {
            return CollectCandidatesAabbCore(
                queryMin,
                queryMax,
                topologyRevision,
                bodyRevision,
                movedBodies,
                bodies,
                aabbColliders,
                sphereColliders,
                capsuleColliders,
                obbColliders,
                ordering,
                filterCandidates: false,
                CombatEntityId.None,
                CombatPhysicsLayerMask.All,
                includeSourceEntity: true);
        }

        public CombatPhysicsBroadphaseResult CollectCandidatesAabb(
            FixVector3 queryMin,
            FixVector3 queryMax,
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> movedBodies,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders,
            CombatPhysicsBroadphaseCandidateOrdering ordering,
            CombatEntityId sourceEntityId,
            CombatPhysicsLayerMask layerMask,
            bool includeSourceEntity)
        {
            return CollectCandidatesAabbCore(
                queryMin,
                queryMax,
                topologyRevision,
                bodyRevision,
                movedBodies,
                bodies,
                aabbColliders,
                sphereColliders,
                capsuleColliders,
                obbColliders,
                ordering,
                filterCandidates: true,
                sourceEntityId,
                layerMask,
                includeSourceEntity);
        }

        private CombatPhysicsBroadphaseResult CollectCandidatesAabbCore(
            FixVector3 queryMin,
            FixVector3 queryMax,
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> movedBodies,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders,
            CombatPhysicsBroadphaseCandidateOrdering ordering,
            bool filterCandidates,
            CombatEntityId sourceEntityId,
            CombatPhysicsLayerMask layerMask,
            bool includeSourceEntity)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            if (aabbColliders == null)
            {
                throw new ArgumentNullException(nameof(aabbColliders));
            }

            if (sphereColliders == null)
            {
                throw new ArgumentNullException(nameof(sphereColliders));
            }

            if (capsuleColliders == null)
            {
                throw new ArgumentNullException(nameof(capsuleColliders));
            }
            if (obbColliders == null) throw new ArgumentNullException(nameof(obbColliders));

            int colliderCount = aabbColliders.Count + sphereColliders.Count + capsuleColliders.Count + obbColliders.Count;
            EnsureIndex(
                topologyRevision,
                bodyRevision,
                movedBodies,
                bodies,
                aabbColliders,
                sphereColliders,
                capsuleColliders,
                obbColliders,
                colliderCount);
            PrepareQueryScratch(_entries.Count);

            CombatPhysicsBroadphaseCell queryCellMin = ToCell(queryMin);
            CombatPhysicsBroadphaseCell queryCellMax = ToCell(queryMax);
            int rawCandidateCount = 0;
            int cellCount = 0;
            for (int x = queryCellMin.X; x <= queryCellMax.X; x++)
            {
                for (int y = queryCellMin.Y; y <= queryCellMax.Y; y++)
                {
                    for (int z = queryCellMin.Z; z <= queryCellMax.Z; z++)
                    {
                        cellCount++;
                        var cell = new CombatPhysicsBroadphaseCell(x, y, z);
                        if (!_cells.TryGetValue(cell, out List<int> colliderIndices))
                        {
                            continue;
                        }

                        for (int i = 0; i < colliderIndices.Count; i++)
                        {
                            rawCandidateCount++;
                            int colliderIndex = colliderIndices[i];
                            if (_candidateQueryStamps[colliderIndex] == _candidateQueryStamp)
                            {
                                continue;
                            }

                            _candidateQueryStamps[colliderIndex] = _candidateQueryStamp;
                            CombatPhysicsBroadphaseCandidate candidate = _entries[colliderIndex];
                            if (filterCandidates
                                && !CanIncludeCandidate(sourceEntityId, layerMask, includeSourceEntity, candidate))
                            {
                                continue;
                            }

                            if (!Overlaps(queryMin, queryMax, candidate.Min, candidate.Max))
                            {
                                continue;
                            }

                            _candidates.Add(candidate);
                        }
                    }
                }
            }

            bool candidatesSorted = ordering == CombatPhysicsBroadphaseCandidateOrdering.Sorted;
            if (candidatesSorted)
            {
                _candidates.Sort(CombatPhysicsBroadphaseCandidateComparer.Instance);
            }

            return new CombatPhysicsBroadphaseResult(
                _candidates,
                rawCandidateCount,
                cellCount,
                queryMin,
                queryMax,
                candidatesSorted);
        }

        private static bool CanIncludeCandidate(
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

        private void PrepareQueryScratch(int entryCount)
        {
            _candidates.Clear();
            AdvanceCandidateQueryStamp();
            EnsureCandidateQueryStampCapacity(entryCount);

            if (_candidates.Capacity < entryCount)
            {
                _candidates.Capacity = entryCount;
            }
        }

        private void AdvanceCandidateQueryStamp()
        {
            if (_candidateQueryStamp < int.MaxValue)
            {
                _candidateQueryStamp++;
                return;
            }

            for (int i = 0; i < _candidateQueryStamps.Count; i++)
                _candidateQueryStamps[i] = 0;
            _candidateQueryStamp = 1;
        }

        private void EnsureCandidateQueryStampCapacity(int entryCount)
        {
            while (_candidateQueryStamps.Count < entryCount)
                _candidateQueryStamps.Add(0);
        }

        private void EnsureIndex(
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> movedBodies,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders,
            int colliderCount)
        {
            if (_hasIndex
                && _indexedTopologyRevision == topologyRevision
                && _indexedBodyRevision == bodyRevision)
            {
                return;
            }

            if (!_hasIndex
                || _indexedTopologyRevision != topologyRevision)
            {
                RebuildIndex(
                    topologyRevision,
                    bodyRevision,
                    bodies,
                    aabbColliders,
                    sphereColliders,
                    capsuleColliders,
                    obbColliders,
                    colliderCount);
                return;
            }

            if (movedBodies != null && movedBodies.Count > 0)
            {
                for (int i = 0; i < movedBodies.Count; i++)
                    UpdateIndexedBody(movedBodies[i]);

                _indexedBodyRevision = bodyRevision;
                return;
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                CombatPhysicsBody body = bodies[i];
                CombatPhysicsBody indexedBody = _indexedBodies[i];
                if (indexedBody.Position.Equals(body.Position))
                {
                    continue;
                }

                _indexedBodies[i] = body;
                UpdateBodyCandidates(body.BodyId, body);
            }

            _indexedBodyRevision = bodyRevision;
        }

        private void RebuildIndex(
            int topologyRevision,
            int bodyRevision,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> aabbColliders,
            IReadOnlyList<CombatPhysicsSphereCollider> sphereColliders,
            IReadOnlyList<CombatPhysicsCapsuleCollider> capsuleColliders,
            IReadOnlyList<CombatPhysicsObbCollider> obbColliders,
            int colliderCount)
        {
            ReleaseCells();
            ReleaseEntryIndexLists();
            _entries.Clear();
            _entryMinCells.Clear();
            _entryMaxCells.Clear();
            EnsureEntryCapacity(colliderCount);
            CopyIndexedBodies(bodies);
            CopyIndexedAabbColliders(aabbColliders);
            CopyIndexedSphereColliders(sphereColliders);
            CopyIndexedCapsuleColliders(capsuleColliders);
            CopyIndexedObbColliders(obbColliders);

            AddAabbCandidates(_indexedAabbColliders);
            AddSphereCandidates(_indexedSphereColliders);
            AddCapsuleCandidates(_indexedCapsuleColliders);
            AddObbCandidates(_indexedObbColliders);

            for (int i = 0; i < _entries.Count; i++)
            {
                CombatPhysicsBroadphaseCandidate entry = _entries[i];
                AddEntryIndex(entry.Body.BodyId, i);
                CombatPhysicsBroadphaseCell minCell = ToCell(entry.Min);
                CombatPhysicsBroadphaseCell maxCell = ToCell(entry.Max);
                _entryMinCells.Add(minCell);
                _entryMaxCells.Add(maxCell);
                AddColliderToCells(i, minCell, maxCell);
            }

            _hasIndex = true;
            _indexedTopologyRevision = topologyRevision;
            _indexedBodyRevision = bodyRevision;
        }

        private void AddEntryIndex(CombatBodyId bodyId, int entryIndex)
        {
            if (!_entryIndicesByBody.TryGetValue(bodyId, out List<int> entryIndices))
            {
                entryIndices = RentEntryIndexList();
                _entryIndicesByBody.Add(bodyId, entryIndices);
            }

            entryIndices.Add(entryIndex);
        }

        private void EnsureEntryCapacity(int colliderCount)
        {
            if (_entries.Capacity < colliderCount)
            {
                _entries.Capacity = colliderCount;
            }

            if (_entryMinCells.Capacity < colliderCount)
            {
                _entryMinCells.Capacity = colliderCount;
            }

            if (_entryMaxCells.Capacity < colliderCount)
            {
                _entryMaxCells.Capacity = colliderCount;
            }
        }

        private void CopyIndexedBodies(IReadOnlyList<CombatPhysicsBody> bodies)
        {
            _indexedBodies.Clear();
            _indexedBodyIndices.Clear();
            if (_indexedBodies.Capacity < bodies.Count)
            {
                _indexedBodies.Capacity = bodies.Count;
            }

            EnsureIndexedBodyIndexCapacity(bodies.Count);
            for (int i = 0; i < bodies.Count; i++)
            {
                CombatPhysicsBody body = bodies[i];
                _indexedBodies.Add(body);
                _indexedBodyIndices[body.BodyId] = i;
            }
        }

        private void EnsureIndexedBodyIndexCapacity(int bodyCount)
        {
            if (bodyCount <= _indexedBodyIndices.Count)
                return;

            _indexedBodyIndices.EnsureCapacity(bodyCount);
        }

        private void CopyIndexedAabbColliders(IReadOnlyList<CombatPhysicsAabbCollider> colliders)
        {
            _indexedAabbColliders.Clear();
            if (_indexedAabbColliders.Capacity < colliders.Count)
            {
                _indexedAabbColliders.Capacity = colliders.Count;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                _indexedAabbColliders.Add(colliders[i]);
            }
        }

        private void CopyIndexedSphereColliders(IReadOnlyList<CombatPhysicsSphereCollider> colliders)
        {
            _indexedSphereColliders.Clear();
            if (_indexedSphereColliders.Capacity < colliders.Count)
            {
                _indexedSphereColliders.Capacity = colliders.Count;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                _indexedSphereColliders.Add(colliders[i]);
            }
        }

        private void CopyIndexedCapsuleColliders(IReadOnlyList<CombatPhysicsCapsuleCollider> colliders)
        {
            _indexedCapsuleColliders.Clear();
            if (_indexedCapsuleColliders.Capacity < colliders.Count)
            {
                _indexedCapsuleColliders.Capacity = colliders.Count;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                _indexedCapsuleColliders.Add(colliders[i]);
            }
        }

        private void CopyIndexedObbColliders(IReadOnlyList<CombatPhysicsObbCollider> colliders)
        {
            _indexedObbColliders.Clear();
            if (_indexedObbColliders.Capacity < colliders.Count) _indexedObbColliders.Capacity = colliders.Count;
            for (int i = 0; i < colliders.Count; i++) _indexedObbColliders.Add(colliders[i]);
        }

        private void UpdateIndexedBody(CombatPhysicsBody body)
        {
            if (!_indexedBodyIndices.TryGetValue(body.BodyId, out int index))
                return;

            CombatPhysicsBody indexedBody = _indexedBodies[index];
            if (indexedBody.Position.Equals(body.Position))
            {
                return;
            }

            _indexedBodies[index] = body;
            UpdateBodyCandidates(body.BodyId, body);
        }

        private void UpdateBodyCandidates(CombatBodyId bodyId, CombatPhysicsBody body)
        {
            if (!_entryIndicesByBody.TryGetValue(bodyId, out List<int> entryIndices))
            {
                return;
            }

            for (int i = 0; i < entryIndices.Count; i++)
            {
                int entryIndex = entryIndices[i];
                CombatPhysicsBroadphaseCandidate entry = _entries[entryIndex];
                CombatPhysicsBroadphaseCandidate updated = RebuildCandidate(entry, body);
                CombatPhysicsBroadphaseCell oldMinCell = _entryMinCells[entryIndex];
                CombatPhysicsBroadphaseCell oldMaxCell = _entryMaxCells[entryIndex];
                CombatPhysicsBroadphaseCell newMinCell = ToCell(updated.Min);
                CombatPhysicsBroadphaseCell newMaxCell = ToCell(updated.Max);
                if (!oldMinCell.Equals(newMinCell) || !oldMaxCell.Equals(newMaxCell))
                {
                    RemoveColliderFromCells(entry.ColliderIndex, oldMinCell, oldMaxCell);
                    AddColliderToCells(entry.ColliderIndex, newMinCell, newMaxCell);
                }

                _entries[entryIndex] = updated;
                _entryMinCells[entryIndex] = newMinCell;
                _entryMaxCells[entryIndex] = newMaxCell;
            }
        }

        private CombatPhysicsBroadphaseCandidate RebuildCandidate(
            CombatPhysicsBroadphaseCandidate entry,
            CombatPhysicsBody body)
        {
            switch (entry.ShapeKind)
            {
                case CombatPhysicsShapeKind.Aabb:
                    return BuildAabbCandidate(entry.ColliderIndex, entry.SourceIndex, body, _indexedAabbColliders[entry.SourceIndex]);
                case CombatPhysicsShapeKind.Sphere:
                    return BuildSphereCandidate(entry.ColliderIndex, entry.SourceIndex, body, _indexedSphereColliders[entry.SourceIndex]);
                case CombatPhysicsShapeKind.Capsule:
                    return BuildCapsuleCandidate(entry.ColliderIndex, entry.SourceIndex, body, _indexedCapsuleColliders[entry.SourceIndex]);
                case CombatPhysicsShapeKind.Obb:
                    return BuildObbCandidate(entry.ColliderIndex, entry.SourceIndex, body, _indexedObbColliders[entry.SourceIndex]);
                default:
                    throw new NotSupportedException("Combat physics broadphase candidate shape kind is not supported.");
            }
        }

        private void ReleaseCells()
        {
            for (int i = _activeCellLists.Count - 1; i >= 0; i--)
            {
                List<int> list = _activeCellLists[i];
                list.Clear();
                _cellListPool.Add(list);
            }

            _activeCellLists.Clear();
            _activeCellListIndices.Clear();
            _cells.Clear();
        }

        private void ReleaseEntryIndexLists()
        {
            for (int i = _activeEntryIndexLists.Count - 1; i >= 0; i--)
            {
                List<int> list = _activeEntryIndexLists[i];
                list.Clear();
                _entryIndexListPool.Add(list);
            }

            _activeEntryIndexLists.Clear();
            _entryIndicesByBody.Clear();
        }

        private List<int> RentCellList()
        {
            int poolIndex = _cellListPool.Count - 1;
            List<int> list;
            if (poolIndex >= 0)
            {
                list = _cellListPool[poolIndex];
                _cellListPool.RemoveAt(poolIndex);
            }
            else
            {
                list = new List<int>();
            }

            AddActiveCellList(list);
            return list;
        }

        private void AddActiveCellList(List<int> list)
        {
            int activeIndex = _activeCellLists.Count;
            _activeCellLists.Add(list);
            _activeCellListIndices[list] = activeIndex;
        }

        private void ReleaseActiveCellList(List<int> list)
        {
            if (!_activeCellListIndices.TryGetValue(list, out int activeIndex))
            {
                return;
            }

            int lastIndex = _activeCellLists.Count - 1;
            List<int> last = _activeCellLists[lastIndex];
            _activeCellLists[activeIndex] = last;
            _activeCellLists.RemoveAt(lastIndex);
            if (activeIndex != lastIndex)
            {
                _activeCellListIndices[last] = activeIndex;
            }

            _activeCellListIndices.Remove(list);
            list.Clear();
            _cellListPool.Add(list);
        }

        private List<int> RentEntryIndexList()
        {
            int poolIndex = _entryIndexListPool.Count - 1;
            List<int> list;
            if (poolIndex >= 0)
            {
                list = _entryIndexListPool[poolIndex];
                _entryIndexListPool.RemoveAt(poolIndex);
            }
            else
            {
                list = new List<int>();
            }

            _activeEntryIndexLists.Add(list);
            return list;
        }

        private void AddAabbCandidates(IReadOnlyList<CombatPhysicsAabbCollider> colliders)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = colliders[i];
                if (!TryGetIndexedBody(collider.BodyId, out CombatPhysicsBody body))
                {
                    continue;
                }

                _entries.Add(BuildAabbCandidate(_entries.Count, i, body, collider));
            }
        }

        private void AddSphereCandidates(IReadOnlyList<CombatPhysicsSphereCollider> colliders)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                CombatPhysicsSphereCollider collider = colliders[i];
                if (!TryGetIndexedBody(collider.BodyId, out CombatPhysicsBody body))
                {
                    continue;
                }

                _entries.Add(BuildSphereCandidate(_entries.Count, i, body, collider));
            }
        }

        private void AddCapsuleCandidates(IReadOnlyList<CombatPhysicsCapsuleCollider> colliders)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = colliders[i];
                if (!TryGetIndexedBody(collider.BodyId, out CombatPhysicsBody body))
                {
                    continue;
                }

                _entries.Add(BuildCapsuleCandidate(_entries.Count, i, body, collider));
            }
        }

        private void AddObbCandidates(IReadOnlyList<CombatPhysicsObbCollider> colliders)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                CombatPhysicsObbCollider collider = colliders[i];
                if (TryGetIndexedBody(collider.BodyId, out CombatPhysicsBody body))
                    _entries.Add(BuildObbCandidate(_entries.Count, i, body, collider));
            }
        }

        private bool TryGetIndexedBody(
            CombatBodyId bodyId,
            out CombatPhysicsBody body)
        {
            if (_indexedBodyIndices.TryGetValue(bodyId, out int index))
            {
                body = _indexedBodies[index];
                return true;
            }

            body = default;
            return false;
        }

        private static CombatPhysicsBroadphaseCandidate BuildAabbCandidate(
            int colliderIndex,
            int sourceIndex,
            CombatPhysicsBody body,
            CombatPhysicsAabbCollider collider)
        {
            FixVector3 min = collider.GetWorldMin(body.Position);
            FixVector3 max = collider.GetWorldMax(body.Position);
            return new CombatPhysicsBroadphaseCandidate(
                colliderIndex,
                sourceIndex,
                body,
                CombatPhysicsShapeKind.Aabb,
                collider.ColliderId,
                collider.Layer,
                collider.SurfaceMaterial,
                collider.Priority,
                CombatPhysicsShape.Aabb(min, max),
                min,
                max);
        }

        private static CombatPhysicsBroadphaseCandidate BuildSphereCandidate(
            int colliderIndex,
            int sourceIndex,
            CombatPhysicsBody body,
            CombatPhysicsSphereCollider collider)
        {
            FixVector3 min = collider.GetWorldMin(body.Position);
            FixVector3 max = collider.GetWorldMax(body.Position);
            return new CombatPhysicsBroadphaseCandidate(
                colliderIndex,
                sourceIndex,
                body,
                CombatPhysicsShapeKind.Sphere,
                collider.ColliderId,
                collider.Layer,
                collider.SurfaceMaterial,
                collider.Priority,
                CombatPhysicsShape.Sphere(collider.GetWorldCenter(body.Position), collider.Radius),
                min,
                max);
        }

        private static CombatPhysicsBroadphaseCandidate BuildCapsuleCandidate(
            int colliderIndex,
            int sourceIndex,
            CombatPhysicsBody body,
            CombatPhysicsCapsuleCollider collider)
        {
            FixVector3 min = collider.GetWorldMin(body.Position);
            FixVector3 max = collider.GetWorldMax(body.Position);
            return new CombatPhysicsBroadphaseCandidate(
                colliderIndex,
                sourceIndex,
                body,
                CombatPhysicsShapeKind.Capsule,
                collider.ColliderId,
                collider.Layer,
                collider.SurfaceMaterial,
                collider.Priority,
                CombatPhysicsShape.Capsule(collider.GetWorldPointA(body.Position), collider.GetWorldPointB(body.Position), collider.Radius),
                min,
                max);
        }

        private static CombatPhysicsBroadphaseCandidate BuildObbCandidate(
            int colliderIndex,
            int sourceIndex,
            CombatPhysicsBody body,
            CombatPhysicsObbCollider collider)
        {
            collider.GetWorldAabb(body.Position, out FixVector3 min, out FixVector3 max);
            return new CombatPhysicsBroadphaseCandidate(
                colliderIndex,
                sourceIndex,
                body,
                CombatPhysicsShapeKind.Obb,
                collider.ColliderId,
                collider.Layer,
                collider.SurfaceMaterial,
                collider.Priority,
                CombatPhysicsShape.Obb(collider.GetWorldCenter(body.Position), collider.HalfExtents, collider.AxisX, collider.AxisY, collider.AxisZ),
                min,
                max);
        }

        private void AddColliderToCells(
            int colliderIndex,
            CombatPhysicsBroadphaseCell minCell,
            CombatPhysicsBroadphaseCell maxCell)
        {
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        var cell = new CombatPhysicsBroadphaseCell(x, y, z);
                        if (!_cells.TryGetValue(cell, out List<int> indices))
                        {
                            indices = RentCellList();
                            _cells.Add(cell, indices);
                        }

                        indices.Add(colliderIndex);
                    }
                }
            }
        }

        private void RemoveColliderFromCells(
            int colliderIndex,
            CombatPhysicsBroadphaseCell minCell,
            CombatPhysicsBroadphaseCell maxCell)
        {
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        var cell = new CombatPhysicsBroadphaseCell(x, y, z);
                        if (!_cells.TryGetValue(cell, out List<int> indices))
                        {
                            continue;
                        }

                        RemoveColliderIndex(indices, colliderIndex);
                        if (indices.Count == 0)
                        {
                            _cells.Remove(cell);
                            ReleaseActiveCellList(indices);
                        }
                    }
                }
            }
        }

        private static void RemoveColliderIndex(List<int> indices, int colliderIndex)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] != colliderIndex)
                {
                    continue;
                }

                int lastIndex = indices.Count - 1;
                indices[i] = indices[lastIndex];
                indices.RemoveAt(lastIndex);
                return;
            }
        }

        private CombatPhysicsBroadphaseCell ToCell(FixVector3 point)
        {
            long cellRaw = _config.CellSize.RawValue;
            return new CombatPhysicsBroadphaseCell(
                FloorDiv(point.X.RawValue, cellRaw),
                FloorDiv(point.Y.RawValue, cellRaw),
                FloorDiv(point.Z.RawValue, cellRaw));
        }

        private static int FloorDiv(long value, long divisor)
        {
            long result = value >= 0
                ? value / divisor
                : -((checked(-value) + divisor - 1) / divisor);
            return checked((int)result);
        }

        private static void GetQueryAabb(
            CombatPhysicsShape shape,
            out FixVector3 min,
            out FixVector3 max)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                    FixVector3 end = shape.Origin + shape.Direction * shape.Length;
                    min = Min(shape.Origin, end);
                    max = Max(shape.Origin, end);
                    return;
                case CombatPhysicsShapeKind.Sphere:
                    FixVector3 sphereExtent = new FixVector3(shape.Radius, shape.Radius, shape.Radius);
                    min = shape.Center - sphereExtent;
                    max = shape.Center + sphereExtent;
                    return;
                case CombatPhysicsShapeKind.Capsule:
                    FixVector3 capsuleExtent = new FixVector3(shape.Radius, shape.Radius, shape.Radius);
                    min = Min(shape.PointA, shape.PointB) - capsuleExtent;
                    max = Max(shape.PointA, shape.PointB) + capsuleExtent;
                    return;
                case CombatPhysicsShapeKind.Aabb:
                    min = shape.Min;
                    max = shape.Max;
                    return;
                case CombatPhysicsShapeKind.Sector:
                    FixVector3 sectorExtent = new FixVector3(shape.Radius, shape.Radius, shape.Radius);
                    min = shape.Origin - sectorExtent;
                    max = shape.Origin + sectorExtent;
                    return;
                case CombatPhysicsShapeKind.Obb:
                    CombatPhysicsObbMath.GetAabb(
                        shape.Center,
                        shape.HalfExtents,
                        shape.AxisX,
                        shape.AxisY,
                        shape.AxisZ,
                        out min,
                        out max);
                    return;
                default:
                    throw new NotSupportedException("Combat physics query shape kind is not supported.");
            }
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

        private static bool Overlaps(FixVector3 leftMin, FixVector3 leftMax, FixVector3 rightMin, FixVector3 rightMax)
        {
            return leftMin.X <= rightMax.X
                && leftMax.X >= rightMin.X
                && leftMin.Y <= rightMax.Y
                && leftMax.Y >= rightMin.Y
                && leftMin.Z <= rightMax.Z
                && leftMax.Z >= rightMin.Z;
        }

        private static int CompareCandidates(
            CombatPhysicsBroadphaseCandidate left,
            CombatPhysicsBroadphaseCandidate right)
        {
            int compare = left.Body.EntityId.CompareTo(right.Body.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Body.BodyId.CompareTo(right.Body.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.ColliderId.CompareTo(right.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ((int)left.ShapeKind).CompareTo((int)right.ShapeKind);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Layer.CompareTo(right.Layer);
            if (compare != 0)
            {
                return compare;
            }

            return left.ColliderIndex.CompareTo(right.ColliderIndex);
        }

        private sealed class CombatPhysicsBroadphaseCandidateComparer : IComparer<CombatPhysicsBroadphaseCandidate>
        {
            public static readonly CombatPhysicsBroadphaseCandidateComparer Instance = new CombatPhysicsBroadphaseCandidateComparer();

            private CombatPhysicsBroadphaseCandidateComparer()
            {
            }

            public int Compare(CombatPhysicsBroadphaseCandidate left, CombatPhysicsBroadphaseCandidate right)
            {
                return CompareCandidates(left, right);
            }
        }
    }
}
