using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    /// <summary>
    /// Reusable broadphase candidate cache for batched kinematic motor steps against a shared physics world.
    /// </summary>
    /// <remarks>
    /// NoAlloc after initialization when capacities are warmed. Callers prepare once for a fixed tick, then
    /// pass the cache to each <see cref="CombatKinematicMotor"/> step. Static layers are assumed not to move
    /// while the cache is prepared; moving a static-layer body requires <see cref="Invalidate"/>.
    /// </remarks>
    public sealed class CombatKinematicMotorCandidateCache
    {
        private static readonly CombatPhysicsBody[] EmptyMovedBodies = Array.Empty<CombatPhysicsBody>();

        private readonly List<CombatPhysicsBody> _staticBodies = new List<CombatPhysicsBody>();
        private readonly List<CombatPhysicsBody> _dynamicBodies = new List<CombatPhysicsBody>();
        private readonly Dictionary<CombatBodyId, int> _dynamicBodyIndices =
            new Dictionary<CombatBodyId, int>();
        private readonly List<CombatPhysicsBody> _prepareMovedBodies = new List<CombatPhysicsBody>();
        private readonly List<CombatPhysicsBody> _dynamicMovedBodies = new List<CombatPhysicsBody>();
        private readonly Dictionary<CombatBodyId, int> _dynamicMovedBodyIndices =
            new Dictionary<CombatBodyId, int>();

        private readonly List<CombatPhysicsAabbCollider> _allAabbColliders =
            new List<CombatPhysicsAabbCollider>();
        private readonly List<CombatPhysicsSphereCollider> _allSphereColliders =
            new List<CombatPhysicsSphereCollider>();
        private readonly List<CombatPhysicsCapsuleCollider> _allCapsuleColliders =
            new List<CombatPhysicsCapsuleCollider>();
        private readonly List<CombatPhysicsObbCollider> _allObbColliders = new List<CombatPhysicsObbCollider>();
        private readonly List<CombatPhysicsAabbCollider> _staticAabbColliders =
            new List<CombatPhysicsAabbCollider>();
        private readonly List<CombatPhysicsSphereCollider> _staticSphereColliders =
            new List<CombatPhysicsSphereCollider>();
        private readonly List<CombatPhysicsCapsuleCollider> _staticCapsuleColliders =
            new List<CombatPhysicsCapsuleCollider>();
        private readonly List<CombatPhysicsObbCollider> _staticObbColliders = new List<CombatPhysicsObbCollider>();
        private readonly List<CombatPhysicsAabbCollider> _dynamicAabbColliders =
            new List<CombatPhysicsAabbCollider>();
        private readonly List<CombatPhysicsSphereCollider> _dynamicSphereColliders =
            new List<CombatPhysicsSphereCollider>();
        private readonly List<CombatPhysicsCapsuleCollider> _dynamicCapsuleColliders =
            new List<CombatPhysicsCapsuleCollider>();
        private readonly List<CombatPhysicsObbCollider> _dynamicObbColliders = new List<CombatPhysicsObbCollider>();

        private CombatPhysicsBroadphaseConfig _broadphaseConfig;
        private CombatPhysicsBroadphase _staticBroadphase;
        private CombatPhysicsBroadphase _dynamicBroadphase;
        private CombatPhysicsWorld _world;
        private CombatPhysicsLayerMask _staticLayerMask;
        private CombatPhysicsLayerMask _dynamicLayerMask;
        private int _staticTopologyRevision = -1;
        private int _staticBodyRevision = -1;
        private int _dynamicTopologyRevision = -1;
        private int _dynamicBodyRevision = -1;
        private int _staticRebuildCount;
        private int _dynamicRebuildCount;
        private int _dynamicBodyUpdateCount;
        private int _dynamicIncrementalUpdateCount;
        private int _lastDynamicMovedBodyCount;
        private int _queryCount;
        private int _lastQueryCellCount;
        private int _lastQueryCandidateCount;
        private bool _staticPrepared;
        private bool _dynamicPrepared;

        /// <summary>
        /// Creates a cache using the default broadphase configuration.
        /// </summary>
        /// <remarks>LowFreqAlloc; create once and reuse across ticks.</remarks>
        public CombatKinematicMotorCandidateCache()
            : this(CombatPhysicsBroadphaseConfig.Default)
        {
        }

        /// <summary>
        /// Creates a cache using an explicit broadphase configuration.
        /// </summary>
        /// <remarks>LowFreqAlloc; create once and reuse across ticks.</remarks>
        public CombatKinematicMotorCandidateCache(CombatPhysicsBroadphaseConfig broadphaseConfig)
        {
            _broadphaseConfig = broadphaseConfig;
            _staticBroadphase = new CombatPhysicsBroadphase(broadphaseConfig);
            _dynamicBroadphase = new CombatPhysicsBroadphase(broadphaseConfig);
        }

        /// <summary>
        /// Gets whether both static and dynamic candidate indexes are prepared for the current world.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public bool IsPrepared => _staticPrepared && _dynamicPrepared;

        /// <summary>
        /// Gets the layer mask cached as static obstacles.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public CombatPhysicsLayerMask StaticLayerMask => _staticLayerMask;

        /// <summary>
        /// Gets the layer mask cached as dynamic obstacles.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public CombatPhysicsLayerMask DynamicLayerMask => _dynamicLayerMask;

        /// <summary>
        /// Gets diagnostics from the most recent prepare/query operations.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public CombatKinematicMotorCandidateCacheStats LastStats =>
            new CombatKinematicMotorCandidateCacheStats(
                IsPrepared,
                _staticAabbColliders.Count + _staticSphereColliders.Count + _staticCapsuleColliders.Count + _staticObbColliders.Count,
                _dynamicAabbColliders.Count + _dynamicSphereColliders.Count + _dynamicCapsuleColliders.Count + _dynamicObbColliders.Count,
                _staticRebuildCount,
                _dynamicRebuildCount,
                _dynamicBodyUpdateCount,
                _dynamicIncrementalUpdateCount,
                _lastDynamicMovedBodyCount,
                _queryCount,
                _lastQueryCellCount,
                _lastQueryCandidateCount);

        /// <summary>
        /// Prepares static and dynamic broadphase indexes for a world and fixed-tick layer split.
        /// </summary>
        /// <remarks>
        /// NoAlloc after initialization when cached collection capacities are sufficient. Rebuilds on topology,
        /// world, mask, or broadphase config changes; body-only changes update the dynamic index incrementally
        /// when the world still exposes the moved-body snapshot.
        /// </remarks>
        public void Prepare(
            CombatPhysicsWorld world,
            CombatPhysicsLayerMask staticLayerMask,
            CombatPhysicsLayerMask dynamicLayerMask)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            EnsureBroadphaseConfig(world.BroadphaseConfig);
            bool worldChanged = !ReferenceEquals(_world, world);
            bool masksChanged = !_staticLayerMask.Equals(staticLayerMask)
                || !_dynamicLayerMask.Equals(dynamicLayerMask);
            _world = world;
            _staticLayerMask = staticLayerMask;
            _dynamicLayerMask = dynamicLayerMask;

            bool staticNeedsRebuild = worldChanged
                || masksChanged
                || !_staticPrepared
                || _staticTopologyRevision != world.TopologyRevision;
            bool dynamicNeedsRebuild = worldChanged
                || masksChanged
                || !_dynamicPrepared
                || _dynamicTopologyRevision != world.TopologyRevision;

            if (staticNeedsRebuild)
            {
                RebuildStatic(world);
            }

            if (dynamicNeedsRebuild)
            {
                RebuildDynamic(world);
                return;
            }

            if (_dynamicBodyRevision == world.BodyRevision)
            {
                _lastDynamicMovedBodyCount = 0;
                return;
            }

            if (!TryUpdateDynamicBodiesFromMovedBodies(world, out bool staticBodyMoved))
            {
                RebuildStatic(world);
                RebuildDynamic(world);
                return;
            }

            if (staticBodyMoved)
            {
                RebuildStatic(world);
            }
        }

        /// <summary>
        /// Returns whether the prepared indexes can serve a motor query requiring the supplied layer mask.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public bool CanServe(CombatPhysicsWorld world, CombatPhysicsLayerMask requiredLayerMask)
        {
            return IsPrepared
                && ReferenceEquals(_world, world)
                && Covers(requiredLayerMask)
                && _staticTopologyRevision == world.TopologyRevision
                && _dynamicTopologyRevision == world.TopologyRevision;
        }

        /// <summary>
        /// Clears snapshots and diagnostics when the cache owner changes worlds or leaves physics mode.
        /// </summary>
        /// <remarks>LowFreqAlloc; call outside fixed-step motion loops.</remarks>
        public void Clear()
        {
            ClearSnapshots();
            _world = null;
            _staticLayerMask = CombatPhysicsLayerMask.None;
            _dynamicLayerMask = CombatPhysicsLayerMask.None;
            _staticPrepared = false;
            _dynamicPrepared = false;
            _staticTopologyRevision = -1;
            _staticBodyRevision = -1;
            _dynamicTopologyRevision = -1;
            _dynamicBodyRevision = -1;
            _staticRebuildCount = 0;
            _dynamicRebuildCount = 0;
            _dynamicBodyUpdateCount = 0;
            _dynamicIncrementalUpdateCount = 0;
            _lastDynamicMovedBodyCount = 0;
            _queryCount = 0;
            _lastQueryCellCount = 0;
            _lastQueryCandidateCount = 0;
            _staticBroadphase = new CombatPhysicsBroadphase(_broadphaseConfig);
            _dynamicBroadphase = new CombatPhysicsBroadphase(_broadphaseConfig);
        }

        /// <summary>
        /// Marks the prepared indexes stale while preserving warmed collection capacities.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public void Invalidate()
        {
            _staticPrepared = false;
            _dynamicPrepared = false;
            _staticTopologyRevision = -1;
            _staticBodyRevision = -1;
            _dynamicTopologyRevision = -1;
            _dynamicBodyRevision = -1;
            _lastDynamicMovedBodyCount = 0;
            ClearDynamicMovedBodies();
        }

        internal bool TryCollectCandidatesAabb(
            CombatPhysicsWorld world,
            FixVector3 queryMin,
            FixVector3 queryMax,
            CombatPhysicsBroadphaseCandidateOrdering ordering,
            CombatEntityId sourceEntityId,
            CombatPhysicsLayerMask layerMask,
            bool includeSourceEntity,
            List<CombatPhysicsBroadphaseCandidate> results,
            out int cellCount)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            cellCount = 0;
            _lastQueryCellCount = 0;
            _lastQueryCandidateCount = 0;

            if (!CanServe(world, layerMask))
            {
                return false;
            }

            if (HasLayerOverlap(layerMask, _staticLayerMask))
            {
                CombatPhysicsBroadphaseResult staticResult = _staticBroadphase.CollectCandidatesAabb(
                    queryMin,
                    queryMax,
                    _staticTopologyRevision,
                    _staticBodyRevision,
                    EmptyMovedBodies,
                    _staticBodies,
                    _staticAabbColliders,
                    _staticSphereColliders,
                    _staticCapsuleColliders,
                    _staticObbColliders,
                    ordering,
                    sourceEntityId,
                    layerMask,
                    includeSourceEntity);
                cellCount += staticResult.CellCount;
                CopyCandidates(staticResult.Candidates, results);
            }

            if (HasLayerOverlap(layerMask, _dynamicLayerMask))
            {
                CombatPhysicsBroadphaseResult dynamicResult = _dynamicBroadphase.CollectCandidatesAabb(
                    queryMin,
                    queryMax,
                    _dynamicTopologyRevision,
                    _dynamicBodyRevision,
                    _dynamicMovedBodies,
                    _dynamicBodies,
                    _dynamicAabbColliders,
                    _dynamicSphereColliders,
                    _dynamicCapsuleColliders,
                    _dynamicObbColliders,
                    ordering,
                    sourceEntityId,
                    layerMask,
                    includeSourceEntity);
                cellCount += dynamicResult.CellCount;
                CopyCandidates(dynamicResult.Candidates, results);
                ClearDynamicMovedBodies();
            }

            _queryCount++;
            _lastQueryCellCount = cellCount;
            _lastQueryCandidateCount = results.Count;
            return true;
        }

        internal void UpdateDynamicBody(CombatPhysicsWorld world, CombatBodyId bodyId)
        {
            if (!ReferenceEquals(_world, world)
                || !_dynamicPrepared
                || _dynamicTopologyRevision != world.TopologyRevision)
            {
                _dynamicPrepared = false;
                return;
            }

            if (!world.TryGetBody(bodyId, out CombatPhysicsBody body))
            {
                _dynamicPrepared = false;
                return;
            }

            if (!_dynamicBodyIndices.TryGetValue(bodyId, out int bodyIndex))
            {
                _dynamicPrepared = false;
                return;
            }

            if (_dynamicBodies[bodyIndex].Equals(body))
            {
                _dynamicBodyRevision = world.BodyRevision;
                return;
            }

            _dynamicBodies[bodyIndex] = body;
            AddDynamicMovedBody(body);
            _dynamicBodyRevision = world.BodyRevision;
            _dynamicBodyUpdateCount++;
            _lastDynamicMovedBodyCount = _dynamicMovedBodies.Count;
        }

        private void EnsureBroadphaseConfig(CombatPhysicsBroadphaseConfig broadphaseConfig)
        {
            if (_broadphaseConfig.Equals(broadphaseConfig))
            {
                return;
            }

            _broadphaseConfig = broadphaseConfig;
            ClearSnapshots();
            _staticPrepared = false;
            _dynamicPrepared = false;
            _staticBroadphase = new CombatPhysicsBroadphase(broadphaseConfig);
            _dynamicBroadphase = new CombatPhysicsBroadphase(broadphaseConfig);
        }

        private void RebuildStatic(CombatPhysicsWorld world)
        {
            _staticBodies.Clear();
            _staticAabbColliders.Clear();
            _staticSphereColliders.Clear();
            _staticCapsuleColliders.Clear();
            _staticObbColliders.Clear();
            world.CopyBodiesUnsortedTo(_staticBodies);
            CopyStaticColliders(world);
            _staticTopologyRevision = world.TopologyRevision;
            _staticBodyRevision = world.BodyRevision;
            _staticBroadphase.PrepareIndex(
                _staticTopologyRevision,
                _staticBodyRevision,
                EmptyMovedBodies,
                _staticBodies,
                _staticAabbColliders,
                _staticSphereColliders,
                _staticCapsuleColliders,
                _staticObbColliders);
            _staticPrepared = true;
            _staticRebuildCount++;
        }

        private void RebuildDynamic(CombatPhysicsWorld world)
        {
            _dynamicBodies.Clear();
            _dynamicBodyIndices.Clear();
            _dynamicAabbColliders.Clear();
            _dynamicSphereColliders.Clear();
            _dynamicCapsuleColliders.Clear();
            _dynamicObbColliders.Clear();
            ClearDynamicMovedBodies();
            world.CopyBodiesUnsortedTo(_dynamicBodies);
            EnsureDictionaryCapacity(_dynamicBodyIndices, _dynamicBodies.Count);
            for (int i = 0; i < _dynamicBodies.Count; i++)
                _dynamicBodyIndices[_dynamicBodies[i].BodyId] = i;
            CopyDynamicColliders(world);
            _dynamicTopologyRevision = world.TopologyRevision;
            _dynamicBodyRevision = world.BodyRevision;
            _dynamicBroadphase.PrepareIndex(
                _dynamicTopologyRevision,
                _dynamicBodyRevision,
                EmptyMovedBodies,
                _dynamicBodies,
                _dynamicAabbColliders,
                _dynamicSphereColliders,
                _dynamicCapsuleColliders,
                _dynamicObbColliders);
            _dynamicPrepared = true;
            _dynamicRebuildCount++;
        }

        private bool TryUpdateDynamicBodiesFromMovedBodies(
            CombatPhysicsWorld world,
            out bool staticBodyMoved)
        {
            staticBodyMoved = false;
            _prepareMovedBodies.Clear();
            EnsureListCapacity(_prepareMovedBodies, world.BodyCount);
            int movedBodyCount = world.CopyMovedBodiesUnsortedTo(_prepareMovedBodies);
            if (movedBodyCount == 0)
            {
                return TryUpdateDynamicBodiesFromWorldSnapshot(world, out staticBodyMoved);
            }

            int updatedCount = 0;
            for (int i = 0; i < _prepareMovedBodies.Count; i++)
            {
                CombatPhysicsBody body = _prepareMovedBodies[i];
                if (!_dynamicBodyIndices.TryGetValue(body.BodyId, out int bodyIndex))
                {
                    _lastDynamicMovedBodyCount = 0;
                    return false;
                }

                if (HasStaticColliderForBody(body.BodyId))
                {
                    staticBodyMoved = true;
                }

                if (_dynamicBodies[bodyIndex].Equals(body))
                {
                    continue;
                }

                _dynamicBodies[bodyIndex] = body;
                AddDynamicMovedBody(body);
                updatedCount++;
            }

            _dynamicBodyRevision = world.BodyRevision;
            _dynamicIncrementalUpdateCount++;
            _dynamicBodyUpdateCount += updatedCount;
            _lastDynamicMovedBodyCount = movedBodyCount;
            return true;
        }

        private bool TryUpdateDynamicBodiesFromWorldSnapshot(
            CombatPhysicsWorld world,
            out bool staticBodyMoved)
        {
            staticBodyMoved = false;
            _prepareMovedBodies.Clear();
            EnsureListCapacity(_prepareMovedBodies, world.BodyCount);
            int bodyCount = world.CopyBodiesUnsortedTo(_prepareMovedBodies);
            if (bodyCount != _dynamicBodies.Count)
            {
                _lastDynamicMovedBodyCount = 0;
                return false;
            }

            int updatedCount = 0;
            for (int i = 0; i < _prepareMovedBodies.Count; i++)
            {
                CombatPhysicsBody body = _prepareMovedBodies[i];
                if (!_dynamicBodyIndices.TryGetValue(body.BodyId, out int bodyIndex))
                {
                    _lastDynamicMovedBodyCount = 0;
                    return false;
                }

                if (_dynamicBodies[bodyIndex].Equals(body))
                    continue;

                if (HasStaticColliderForBody(body.BodyId) && HasStaticBodyChanged(body))
                    staticBodyMoved = true;

                _dynamicBodies[bodyIndex] = body;
                AddDynamicMovedBody(body);
                updatedCount++;
            }

            _dynamicBodyRevision = world.BodyRevision;
            _dynamicIncrementalUpdateCount++;
            _dynamicBodyUpdateCount += updatedCount;
            _lastDynamicMovedBodyCount = updatedCount;
            return true;
        }

        private void CopyStaticColliders(CombatPhysicsWorld world)
        {
            _allAabbColliders.Clear();
            world.CopyAabbCollidersUnsortedTo(_allAabbColliders);
            EnsureListCapacity(_staticAabbColliders, _allAabbColliders.Count);
            for (int i = 0; i < _allAabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _allAabbColliders[i];
                if (IsStaticLayer(collider.Layer))
                    _staticAabbColliders.Add(collider);
            }

            _allSphereColliders.Clear();
            world.CopySphereCollidersUnsortedTo(_allSphereColliders);
            EnsureListCapacity(_staticSphereColliders, _allSphereColliders.Count);
            for (int i = 0; i < _allSphereColliders.Count; i++)
            {
                CombatPhysicsSphereCollider collider = _allSphereColliders[i];
                if (IsStaticLayer(collider.Layer))
                    _staticSphereColliders.Add(collider);
            }

            _allCapsuleColliders.Clear();
            world.CopyCapsuleCollidersUnsortedTo(_allCapsuleColliders);
            EnsureListCapacity(_staticCapsuleColliders, _allCapsuleColliders.Count);
            for (int i = 0; i < _allCapsuleColliders.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = _allCapsuleColliders[i];
                if (IsStaticLayer(collider.Layer))
                    _staticCapsuleColliders.Add(collider);
            }

            _allObbColliders.Clear();
            world.CopyObbCollidersUnsortedTo(_allObbColliders);
            EnsureListCapacity(_staticObbColliders, _allObbColliders.Count);
            for (int i = 0; i < _allObbColliders.Count; i++)
            {
                CombatPhysicsObbCollider collider = _allObbColliders[i];
                if (IsStaticLayer(collider.Layer)) _staticObbColliders.Add(collider);
            }
        }

        private void CopyDynamicColliders(CombatPhysicsWorld world)
        {
            _allAabbColliders.Clear();
            world.CopyAabbCollidersUnsortedTo(_allAabbColliders);
            EnsureListCapacity(_dynamicAabbColliders, _allAabbColliders.Count);
            for (int i = 0; i < _allAabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _allAabbColliders[i];
                if (IsDynamicLayer(collider.Layer))
                    _dynamicAabbColliders.Add(collider);
            }

            _allSphereColliders.Clear();
            world.CopySphereCollidersUnsortedTo(_allSphereColliders);
            EnsureListCapacity(_dynamicSphereColliders, _allSphereColliders.Count);
            for (int i = 0; i < _allSphereColliders.Count; i++)
            {
                CombatPhysicsSphereCollider collider = _allSphereColliders[i];
                if (IsDynamicLayer(collider.Layer))
                    _dynamicSphereColliders.Add(collider);
            }

            _allCapsuleColliders.Clear();
            world.CopyCapsuleCollidersUnsortedTo(_allCapsuleColliders);
            EnsureListCapacity(_dynamicCapsuleColliders, _allCapsuleColliders.Count);
            for (int i = 0; i < _allCapsuleColliders.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = _allCapsuleColliders[i];
                if (IsDynamicLayer(collider.Layer))
                    _dynamicCapsuleColliders.Add(collider);
            }

            _allObbColliders.Clear();
            world.CopyObbCollidersUnsortedTo(_allObbColliders);
            EnsureListCapacity(_dynamicObbColliders, _allObbColliders.Count);
            for (int i = 0; i < _allObbColliders.Count; i++)
            {
                CombatPhysicsObbCollider collider = _allObbColliders[i];
                if (IsDynamicLayer(collider.Layer)) _dynamicObbColliders.Add(collider);
            }
        }

        private bool Covers(CombatPhysicsLayerMask requiredLayerMask)
        {
            int covered = _staticLayerMask.Value | _dynamicLayerMask.Value;
            return (requiredLayerMask.Value & ~covered) == 0;
        }

        private bool IsStaticLayer(int layer)
        {
            return _staticLayerMask.ContainsLayer(layer)
                && !_dynamicLayerMask.ContainsLayer(layer);
        }

        private bool IsDynamicLayer(int layer)
        {
            return _dynamicLayerMask.ContainsLayer(layer);
        }

        private bool HasStaticColliderForBody(CombatBodyId bodyId)
        {
            for (int i = 0; i < _staticAabbColliders.Count; i++)
            {
                if (_staticAabbColliders[i].BodyId.Equals(bodyId))
                    return true;
            }

            for (int i = 0; i < _staticSphereColliders.Count; i++)
            {
                if (_staticSphereColliders[i].BodyId.Equals(bodyId))
                    return true;
            }

            for (int i = 0; i < _staticCapsuleColliders.Count; i++)
            {
                if (_staticCapsuleColliders[i].BodyId.Equals(bodyId))
                    return true;
            }

            for (int i = 0; i < _staticObbColliders.Count; i++)
            {
                if (_staticObbColliders[i].BodyId.Equals(bodyId)) return true;
            }

            return false;
        }

        private bool HasStaticBodyChanged(CombatPhysicsBody body)
        {
            for (int i = 0; i < _staticBodies.Count; i++)
            {
                CombatPhysicsBody candidate = _staticBodies[i];
                if (candidate.BodyId.Equals(body.BodyId))
                    return !candidate.Equals(body);
            }

            return true;
        }

        private void AddDynamicMovedBody(CombatPhysicsBody body)
        {
            EnsureDictionaryCapacity(_dynamicMovedBodyIndices, _dynamicBodies.Count);
            if (_dynamicMovedBodyIndices.TryGetValue(body.BodyId, out int index))
            {
                _dynamicMovedBodies[index] = body;
                return;
            }

            _dynamicMovedBodyIndices[body.BodyId] = _dynamicMovedBodies.Count;
            _dynamicMovedBodies.Add(body);
        }

        private void ClearDynamicMovedBodies()
        {
            _dynamicMovedBodies.Clear();
            _dynamicMovedBodyIndices.Clear();
        }

        private void ClearSnapshots()
        {
            _staticBodies.Clear();
            _dynamicBodies.Clear();
            _dynamicBodyIndices.Clear();
            _prepareMovedBodies.Clear();
            ClearDynamicMovedBodies();
            _allAabbColliders.Clear();
            _allSphereColliders.Clear();
            _allCapsuleColliders.Clear();
            _allObbColliders.Clear();
            _staticAabbColliders.Clear();
            _staticSphereColliders.Clear();
            _staticCapsuleColliders.Clear();
            _staticObbColliders.Clear();
            _dynamicAabbColliders.Clear();
            _dynamicSphereColliders.Clear();
            _dynamicCapsuleColliders.Clear();
            _dynamicObbColliders.Clear();
        }

        private static void CopyCandidates(
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> source,
            List<CombatPhysicsBroadphaseCandidate> destination)
        {
            if (destination.Capacity < destination.Count + source.Count)
            {
                destination.Capacity = destination.Count + source.Count;
            }

            for (int i = 0; i < source.Count; i++)
                destination.Add(source[i]);
        }

        private static bool HasLayerOverlap(
            CombatPhysicsLayerMask left,
            CombatPhysicsLayerMask right)
        {
            return (left.Value & right.Value) != 0;
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }
        }

        private static void EnsureDictionaryCapacity<TKey, TValue>(
            Dictionary<TKey, TValue> dictionary,
            int capacity)
        {
            if (dictionary.Count < capacity)
            {
                dictionary.EnsureCapacity(capacity);
            }
        }
    }

    /// <summary>
    /// Diagnostics snapshot for <see cref="CombatKinematicMotorCandidateCache"/>.
    /// </summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct CombatKinematicMotorCandidateCacheStats : IEquatable<CombatKinematicMotorCandidateCacheStats>
    {
        /// <summary>
        /// Creates a cache diagnostics snapshot.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public CombatKinematicMotorCandidateCacheStats(
            bool isPrepared,
            int staticColliderCount,
            int dynamicColliderCount,
            int staticRebuildCount,
            int dynamicRebuildCount,
            int dynamicBodyUpdateCount,
            int queryCount,
            int lastQueryCellCount,
            int lastQueryCandidateCount)
            : this(
                isPrepared,
                staticColliderCount,
                dynamicColliderCount,
                staticRebuildCount,
                dynamicRebuildCount,
                dynamicBodyUpdateCount,
                dynamicIncrementalUpdateCount: 0,
                dynamicMovedBodyCount: 0,
                queryCount,
                lastQueryCellCount,
                lastQueryCandidateCount)
        {
        }

        /// <summary>
        /// Creates a cache diagnostics snapshot.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public CombatKinematicMotorCandidateCacheStats(
            bool isPrepared,
            int staticColliderCount,
            int dynamicColliderCount,
            int staticRebuildCount,
            int dynamicRebuildCount,
            int dynamicBodyUpdateCount,
            int dynamicIncrementalUpdateCount,
            int dynamicMovedBodyCount,
            int queryCount,
            int lastQueryCellCount,
            int lastQueryCandidateCount)
        {
            IsPrepared = isPrepared;
            StaticColliderCount = staticColliderCount;
            DynamicColliderCount = dynamicColliderCount;
            StaticRebuildCount = staticRebuildCount;
            DynamicRebuildCount = dynamicRebuildCount;
            DynamicBodyUpdateCount = dynamicBodyUpdateCount;
            DynamicIncrementalUpdateCount = dynamicIncrementalUpdateCount;
            DynamicMovedBodyCount = dynamicMovedBodyCount;
            QueryCount = queryCount;
            LastQueryCellCount = lastQueryCellCount;
            LastQueryCandidateCount = lastQueryCandidateCount;
        }

        /// <summary>
        /// Gets whether both candidate indexes were prepared.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public bool IsPrepared { get; }

        /// <summary>
        /// Gets the number of colliders cached in the static index.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int StaticColliderCount { get; }

        /// <summary>
        /// Gets the number of colliders cached in the dynamic index.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int DynamicColliderCount { get; }

        /// <summary>
        /// Gets how many times the static index was rebuilt.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int StaticRebuildCount { get; }

        /// <summary>
        /// Gets how many times the dynamic index was rebuilt.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int DynamicRebuildCount { get; }

        /// <summary>
        /// Gets how many dynamic body snapshots were incrementally updated by prepare or motor world-sync.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int DynamicBodyUpdateCount { get; }

        /// <summary>
        /// Gets how many prepare calls applied dynamic body changes incrementally.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int DynamicIncrementalUpdateCount { get; }

        /// <summary>
        /// Gets the moved body count observed by the most recent dynamic incremental prepare.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int DynamicMovedBodyCount { get; }

        /// <summary>
        /// Gets how many candidate queries were served by the cache.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int QueryCount { get; }

        /// <summary>
        /// Gets the broadphase cell count from the most recent cached query.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int LastQueryCellCount { get; }

        /// <summary>
        /// Gets the candidate count from the most recent cached query.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public int LastQueryCandidateCount { get; }

        public bool Equals(CombatKinematicMotorCandidateCacheStats other)
        {
            return IsPrepared == other.IsPrepared
                && StaticColliderCount == other.StaticColliderCount
                && DynamicColliderCount == other.DynamicColliderCount
                && StaticRebuildCount == other.StaticRebuildCount
                && DynamicRebuildCount == other.DynamicRebuildCount
                && DynamicBodyUpdateCount == other.DynamicBodyUpdateCount
                && DynamicIncrementalUpdateCount == other.DynamicIncrementalUpdateCount
                && DynamicMovedBodyCount == other.DynamicMovedBodyCount
                && QueryCount == other.QueryCount
                && LastQueryCellCount == other.LastQueryCellCount
                && LastQueryCandidateCount == other.LastQueryCandidateCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatKinematicMotorCandidateCacheStats other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsPrepared ? 1 : 0;
                hash = (hash * 397) ^ StaticColliderCount;
                hash = (hash * 397) ^ DynamicColliderCount;
                hash = (hash * 397) ^ StaticRebuildCount;
                hash = (hash * 397) ^ DynamicRebuildCount;
                hash = (hash * 397) ^ DynamicBodyUpdateCount;
                hash = (hash * 397) ^ DynamicIncrementalUpdateCount;
                hash = (hash * 397) ^ DynamicMovedBodyCount;
                hash = (hash * 397) ^ QueryCount;
                hash = (hash * 397) ^ LastQueryCellCount;
                hash = (hash * 397) ^ LastQueryCandidateCount;
                return hash;
            }
        }
    }
}
