using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestMovableNaturalObjectContactScratch
    {
        private const int StaticObstacleGridCellSizeMillimeters = 4000;
        private const int QueryContactEpsilonMillimeters = 1;

        private readonly List<CombatPhysicsAabbCollider> _aabbColliders =
            new List<CombatPhysicsAabbCollider>();
        private readonly List<CharacterTestMovableNaturalObjectStaticContactObstacle> _staticObstacles =
            new List<CharacterTestMovableNaturalObjectStaticContactObstacle>();
        private readonly Dictionary<StaticObstacleCell, List<int>> _staticObstacleCells =
            new Dictionary<StaticObstacleCell, List<int>>();
        private readonly List<List<int>> _activeStaticObstacleCellLists =
            new List<List<int>>();
        private readonly List<List<int>> _staticObstacleCellListPool =
            new List<List<int>>();
        private readonly List<int> _staticObstacleQueryIndices =
            new List<int>();
        private readonly List<CharacterTestMovableNaturalObjectStaticContactObstacle> _staticObstacleQueryResults =
            new List<CharacterTestMovableNaturalObjectStaticContactObstacle>();
        private readonly Dictionary<int, StaticObstacleQueryCache> _staticObstacleQueryCaches =
            new Dictionary<int, StaticObstacleQueryCache>();
        private readonly List<CharacterTestMovableNaturalObjectState> _objectContactStates =
            new List<CharacterTestMovableNaturalObjectState>();
        private readonly List<ObjectContactStateBounds> _objectContactStateBounds =
            new List<ObjectContactStateBounds>();
        private readonly Dictionary<StaticObstacleCell, ObjectContactCellEntry> _objectContactCells =
            new Dictionary<StaticObstacleCell, ObjectContactCellEntry>();
        private readonly List<ObjectContactCellEntry> _activeObjectContactCellEntries =
            new List<ObjectContactCellEntry>();
        private readonly List<ObjectContactCellEntry> _objectContactCellEntryPool =
            new List<ObjectContactCellEntry>();
        private readonly List<int> _objectContactQueryIndices =
            new List<int>();
        private readonly List<int> _objectContactQueryStamps =
            new List<int>();
        private CharacterTestCharacterPhysicsRuntime _cachedPhysicsRuntime;
        private CombatPhysicsWorld _cachedWorld;
        private int _cachedTopologyRevision = -1;
        private int _cachedStaticObstacleLayerMaskValue;
        private int _cachedStaticColliderCount = -1;
        private int _staticObstacleRebuildCount;
        private int _staticObstacleCacheHitCount;
        private int _staticObstacleQueryStamp;
        private int _lastStaticObstacleRawCandidateCount;
        private int _lastStaticObstacleCandidateCount;
        private int _lastStaticObstacleQueryCellCount;
        private int _staticObstacleObjectQueryCacheHitCount;
        private int _staticObstacleObjectQueryCacheMissCount;
        private int _lastObjectContactActiveCount;
        private int _lastObjectContactRawCandidateCount;
        private int _lastObjectContactCandidateCount;
        private int _lastObjectContactQueryCellCount;
        private int _lastObjectContactSkippedCellCount;
        private int _lastObjectContactIndexRebuildCount;
        private int _objectContactQueryStamp;
        private bool _lastStaticObstacleQueryCacheHit;

        internal IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> StaticObstacles =>
            _staticObstacles;

        public int StaticObstacleCount => _staticObstacles.Count;
        public int StaticObstacleRebuildCount => _staticObstacleRebuildCount;
        public int StaticObstacleCacheHitCount => _staticObstacleCacheHitCount;
        public int CachedTopologyRevision => _cachedTopologyRevision;
        public int LastStaticObstacleRawCandidateCount => _lastStaticObstacleRawCandidateCount;
        public int LastStaticObstacleCandidateCount => _lastStaticObstacleCandidateCount;
        public int LastStaticObstacleQueryCellCount => _lastStaticObstacleQueryCellCount;
        internal int StaticObstacleObjectQueryCacheHitCount => _staticObstacleObjectQueryCacheHitCount;
        internal int StaticObstacleObjectQueryCacheMissCount => _staticObstacleObjectQueryCacheMissCount;
        internal bool LastStaticObstacleQueryCacheHit => _lastStaticObstacleQueryCacheHit;
        internal int LastObjectContactActiveCount => _lastObjectContactActiveCount;
        internal int LastObjectContactRawCandidateCount => _lastObjectContactRawCandidateCount;
        internal int LastObjectContactCandidateCount => _lastObjectContactCandidateCount;
        internal int LastObjectContactQueryCellCount => _lastObjectContactQueryCellCount;
        internal int LastObjectContactSkippedCellCount => _lastObjectContactSkippedCellCount;
        internal int LastObjectContactIndexRebuildCount => _lastObjectContactIndexRebuildCount;

        public void Clear()
        {
            _aabbColliders.Clear();
            _staticObstacles.Clear();
            ReleaseStaticObstacleCells();
            ClearStaticObstacleQueryCaches();
            _staticObstacleQueryIndices.Clear();
            _staticObstacleQueryResults.Clear();
            _objectContactStates.Clear();
            _objectContactStateBounds.Clear();
            ReleaseObjectContactCells();
            _objectContactQueryIndices.Clear();
            _objectContactQueryStamps.Clear();
            _cachedPhysicsRuntime = null;
            _cachedWorld = null;
            _cachedTopologyRevision = -1;
            _cachedStaticObstacleLayerMaskValue = 0;
            _cachedStaticColliderCount = -1;
            _staticObstacleRebuildCount = 0;
            _staticObstacleCacheHitCount = 0;
            _staticObstacleQueryStamp = 0;
            _staticObstacleObjectQueryCacheHitCount = 0;
            _staticObstacleObjectQueryCacheMissCount = 0;
            _objectContactQueryStamp = 0;
            ResetStaticObstacleQueryStats();
            ResetObjectContactStats();
        }

        internal void BuildStaticObstacles(CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (physicsRuntime == null || physicsRuntime.World == null)
            {
                Clear();
                return;
            }

            CombatPhysicsWorld world = physicsRuntime.World;
            if (IsStaticObstacleCacheValid(physicsRuntime, world))
            {
                _staticObstacleCacheHitCount++;
                return;
            }

            _aabbColliders.Clear();
            _staticObstacles.Clear();
            ReleaseStaticObstacleCells();
            world.CopyAabbCollidersTo(_aabbColliders);
            if (_staticObstacles.Capacity < _aabbColliders.Count)
                _staticObstacles.Capacity = _aabbColliders.Count;
            if (_staticObstacleQueryIndices.Capacity < _aabbColliders.Count)
                _staticObstacleQueryIndices.Capacity = _aabbColliders.Count;
            if (_staticObstacleQueryResults.Capacity < _aabbColliders.Count)
                _staticObstacleQueryResults.Capacity = _aabbColliders.Count;

            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                if (!physicsRuntime.StaticObstacleLayerMask.ContainsLayer(collider.Layer)
                    || !world.TryGetBody(collider.BodyId, out CombatPhysicsBody body))
                {
                    continue;
                }

                FixVector3 targetMin = collider.GetWorldMin(body.Position);
                FixVector3 targetMax = collider.GetWorldMax(body.Position);
                _staticObstacles.Add(new CharacterTestMovableNaturalObjectStaticContactObstacle(
                    collider,
                    ToMillimeters(targetMin.X),
                    ToMillimeters(targetMax.X),
                    ToMillimeters(targetMin.Y),
                    ToMillimeters(targetMax.Y),
                    ToMillimeters(targetMin.Z),
                    ToMillimeters(targetMax.Z)));
            }

            BuildStaticObstacleGrid();

            _cachedPhysicsRuntime = physicsRuntime;
            _cachedWorld = world;
            _cachedTopologyRevision = world.TopologyRevision;
            _cachedStaticObstacleLayerMaskValue = physicsRuntime.StaticObstacleLayerMask.Value;
            _cachedStaticColliderCount = physicsRuntime.StaticColliderCount;
            _staticObstacleRebuildCount++;
        }

        private bool IsStaticObstacleCacheValid(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CombatPhysicsWorld world)
        {
            return ReferenceEquals(_cachedPhysicsRuntime, physicsRuntime)
                && ReferenceEquals(_cachedWorld, world)
                && _cachedTopologyRevision == world.TopologyRevision
                && _cachedStaticObstacleLayerMaskValue == physicsRuntime.StaticObstacleLayerMask.Value
                && _cachedStaticColliderCount == physicsRuntime.StaticColliderCount;
        }

        internal IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> QueryStaticObstacles(
            CharacterTestMovableNaturalObjectState state)
        {
            ResetStaticObstacleQueryStats();
            if (state == null || _staticObstacles.Count == 0)
            {
                _staticObstacleQueryIndices.Clear();
                _staticObstacleQueryResults.Clear();
                return _staticObstacleQueryResults;
            }

            GetStateQueryBounds(
                state,
                out int queryMinX,
                out int queryMaxX,
                out int queryMinZ,
                out int queryMaxZ);
            StaticObstacleQueryCache queryCache = state.NumericId > 0
                ? GetStaticObstacleQueryCache(state.NumericId)
                : null;
            if (queryCache != null
                && queryCache.Matches(
                    _cachedTopologyRevision,
                    queryMinX,
                    queryMaxX,
                    queryMinZ,
                    queryMaxZ))
            {
                _staticObstacleObjectQueryCacheHitCount++;
                _lastStaticObstacleQueryCacheHit = true;
                _lastStaticObstacleCandidateCount = queryCache.Results.Count;
                return queryCache.Results;
            }

            _staticObstacleQueryIndices.Clear();
            _staticObstacleQueryResults.Clear();
            _staticObstacleObjectQueryCacheMissCount++;
            int queryStamp = NextStaticObstacleQueryStamp();
            StaticObstacleCell minCell = ToStaticObstacleCell(queryMinX, queryMinZ);
            StaticObstacleCell maxCell = ToStaticObstacleCell(queryMaxX, queryMaxZ);
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    _lastStaticObstacleQueryCellCount++;
                    if (!_staticObstacleCells.TryGetValue(new StaticObstacleCell(x, z), out List<int> indices))
                        continue;

                    _lastStaticObstacleRawCandidateCount += indices.Count;
                    for (int i = 0; i < indices.Count; i++)
                    {
                        int obstacleIndex = indices[i];
                        CharacterTestMovableNaturalObjectStaticContactObstacle obstacle =
                            _staticObstacles[obstacleIndex];
                        if (obstacle.QueryStamp == queryStamp)
                            continue;

                        obstacle.QueryStamp = queryStamp;
                        _staticObstacles[obstacleIndex] = obstacle;
                        if (obstacle.MaxX < queryMinX
                            || obstacle.MinX > queryMaxX
                            || obstacle.MaxZ < queryMinZ
                            || obstacle.MinZ > queryMaxZ)
                        {
                            continue;
                        }

                        _staticObstacleQueryIndices.Add(obstacleIndex);
                    }
                }
            }

            SortObstacleIndices(_staticObstacleQueryIndices);
            for (int i = 0; i < _staticObstacleQueryIndices.Count; i++)
                _staticObstacleQueryResults.Add(_staticObstacles[_staticObstacleQueryIndices[i]]);
            _lastStaticObstacleCandidateCount = _staticObstacleQueryResults.Count;
            if (queryCache != null)
            {
                queryCache.Update(
                    _cachedTopologyRevision,
                    queryMinX,
                    queryMaxX,
                    queryMinZ,
                    queryMaxZ,
                    _staticObstacleQueryResults);
                return queryCache.Results;
            }

            return _staticObstacleQueryResults;
        }

        internal IReadOnlyList<CharacterTestMovableNaturalObjectState> PrepareObjectContactCandidates(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects)
        {
            ResetObjectContactStats();
            _objectContactStates.Clear();
            _objectContactStateBounds.Clear();
            _objectContactQueryIndices.Clear();
            ReleaseObjectContactCells();
            if (objects == null || objects.Count == 0)
                return _objectContactStates;

            if (_objectContactStates.Capacity < objects.Count)
                _objectContactStates.Capacity = objects.Count;
            if (_objectContactStateBounds.Capacity < objects.Count)
                _objectContactStateBounds.Capacity = objects.Count;

            for (int i = 0; i < objects.Count; i++)
            {
                CharacterTestMovableNaturalObjectState state = objects[i];
                if (state != null)
                    _objectContactStates.Add(state);
            }

            EnsureObjectContactQueryStampCapacity(_objectContactStates.Count);
            _lastObjectContactActiveCount = _objectContactStates.Count;
            return _objectContactStates;
        }

        internal void RebuildObjectContactIndex()
        {
            ReleaseObjectContactCells();
            _objectContactStateBounds.Clear();
            for (int i = 0; i < _objectContactStates.Count; i++)
            {
                CharacterTestMovableNaturalObjectState state = _objectContactStates[i];
                ObjectContactStateBounds bounds = BuildObjectContactStateBounds(state);
                _objectContactStateBounds.Add(bounds);
                StaticObstacleCell minCell = ToStaticObstacleCell(bounds.MinX, bounds.MinZ);
                StaticObstacleCell maxCell = ToStaticObstacleCell(bounds.MaxX, bounds.MaxZ);
                for (int x = minCell.X; x <= maxCell.X; x++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                        GetObjectContactCellEntry(new StaticObstacleCell(x, z)).Add(
                            i,
                            bounds.MinX,
                            bounds.MaxX,
                            bounds.MinZ,
                            bounds.MaxZ);
                }
            }

            _lastObjectContactIndexRebuildCount++;
        }

        internal IReadOnlyList<int> QueryObjectContactCandidateIndices(
            int activeIndex,
            int minCandidateIndex)
        {
            _objectContactQueryIndices.Clear();
            if ((uint)activeIndex >= (uint)_objectContactStateBounds.Count)
                return _objectContactQueryIndices;

            ObjectContactStateBounds queryBounds = _objectContactStateBounds[activeIndex];
            int queryStamp = NextObjectContactQueryStamp();
            StaticObstacleCell minCell = ToStaticObstacleCell(queryBounds.MinX, queryBounds.MinZ);
            StaticObstacleCell maxCell = ToStaticObstacleCell(queryBounds.MaxX, queryBounds.MaxZ);
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    _lastObjectContactQueryCellCount++;
                    if (!_objectContactCells.TryGetValue(new StaticObstacleCell(x, z), out ObjectContactCellEntry entry))
                        continue;

                    if (!BoundsOverlap(
                        queryBounds.MinX,
                        queryBounds.MaxX,
                        queryBounds.MinZ,
                        queryBounds.MaxZ,
                        entry.MinX,
                        entry.MaxX,
                        entry.MinZ,
                        entry.MaxZ))
                    {
                        _lastObjectContactSkippedCellCount++;
                        continue;
                    }

                    List<int> indices = entry.Indices;
                    _lastObjectContactRawCandidateCount += indices.Count;
                    for (int i = 0; i < indices.Count; i++)
                    {
                        int candidateIndex = indices[i];
                        if (candidateIndex <= activeIndex || candidateIndex < minCandidateIndex)
                            continue;
                        if (_objectContactQueryStamps[candidateIndex] == queryStamp)
                            continue;

                        _objectContactQueryStamps[candidateIndex] = queryStamp;
                        ObjectContactStateBounds candidateBounds =
                            _objectContactStateBounds[candidateIndex];
                        if (!ObjectContactBoundsOverlap(
                            queryBounds,
                            candidateBounds))
                        {
                            continue;
                        }

                        _objectContactQueryIndices.Add(candidateIndex);
                    }
                }
            }

            SortObstacleIndices(_objectContactQueryIndices);
            _lastObjectContactCandidateCount += _objectContactQueryIndices.Count;
            return _objectContactQueryIndices;
        }

        private void ResetStaticObstacleQueryStats()
        {
            _lastStaticObstacleRawCandidateCount = 0;
            _lastStaticObstacleCandidateCount = 0;
            _lastStaticObstacleQueryCellCount = 0;
            _lastStaticObstacleQueryCacheHit = false;
        }

        private void ResetObjectContactStats()
        {
            _lastObjectContactActiveCount = 0;
            _lastObjectContactRawCandidateCount = 0;
            _lastObjectContactCandidateCount = 0;
            _lastObjectContactQueryCellCount = 0;
            _lastObjectContactSkippedCellCount = 0;
            _lastObjectContactIndexRebuildCount = 0;
        }

        private void BuildStaticObstacleGrid()
        {
            for (int i = 0; i < _staticObstacles.Count; i++)
            {
                CharacterTestMovableNaturalObjectStaticContactObstacle obstacle = _staticObstacles[i];
                StaticObstacleCell minCell = ToStaticObstacleCell(obstacle.MinX, obstacle.MinZ);
                StaticObstacleCell maxCell = ToStaticObstacleCell(obstacle.MaxX, obstacle.MaxZ);
                for (int x = minCell.X; x <= maxCell.X; x++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                        GetStaticObstacleCellList(new StaticObstacleCell(x, z)).Add(i);
                }
            }
        }

        private List<int> GetStaticObstacleCellList(StaticObstacleCell cell)
        {
            if (_staticObstacleCells.TryGetValue(cell, out List<int> indices))
                return indices;

            indices = RentStaticObstacleCellList();
            _staticObstacleCells.Add(cell, indices);
            _activeStaticObstacleCellLists.Add(indices);
            return indices;
        }

        private List<int> RentStaticObstacleCellList()
        {
            int last = _staticObstacleCellListPool.Count - 1;
            if (last < 0)
                return new List<int>(4);

            List<int> indices = _staticObstacleCellListPool[last];
            _staticObstacleCellListPool.RemoveAt(last);
            return indices;
        }

        private void ReleaseStaticObstacleCells()
        {
            for (int i = 0; i < _activeStaticObstacleCellLists.Count; i++)
            {
                List<int> indices = _activeStaticObstacleCellLists[i];
                indices.Clear();
                _staticObstacleCellListPool.Add(indices);
            }

            _activeStaticObstacleCellLists.Clear();
            _staticObstacleCells.Clear();
        }

        private ObjectContactCellEntry GetObjectContactCellEntry(StaticObstacleCell cell)
        {
            if (_objectContactCells.TryGetValue(cell, out ObjectContactCellEntry entry))
                return entry;

            entry = RentObjectContactCellEntry();
            _objectContactCells.Add(cell, entry);
            _activeObjectContactCellEntries.Add(entry);
            return entry;
        }

        private ObjectContactCellEntry RentObjectContactCellEntry()
        {
            int last = _objectContactCellEntryPool.Count - 1;
            if (last < 0)
                return new ObjectContactCellEntry();

            ObjectContactCellEntry entry = _objectContactCellEntryPool[last];
            _objectContactCellEntryPool.RemoveAt(last);
            return entry;
        }

        private void ReleaseObjectContactCells()
        {
            for (int i = 0; i < _activeObjectContactCellEntries.Count; i++)
            {
                ObjectContactCellEntry entry = _activeObjectContactCellEntries[i];
                entry.Reset();
                _objectContactCellEntryPool.Add(entry);
            }

            _activeObjectContactCellEntries.Clear();
            _objectContactCells.Clear();
        }

        private StaticObstacleQueryCache GetStaticObstacleQueryCache(int numericId)
        {
            if (_staticObstacleQueryCaches.TryGetValue(numericId, out StaticObstacleQueryCache cache))
                return cache;

            cache = new StaticObstacleQueryCache();
            _staticObstacleQueryCaches.Add(numericId, cache);
            return cache;
        }

        private void ClearStaticObstacleQueryCaches()
        {
            foreach (KeyValuePair<int, StaticObstacleQueryCache> entry in _staticObstacleQueryCaches)
                entry.Value.Clear();
            _staticObstacleQueryCaches.Clear();
        }

        private int NextStaticObstacleQueryStamp()
        {
            if (_staticObstacleQueryStamp != int.MaxValue)
                return ++_staticObstacleQueryStamp;

            for (int i = 0; i < _staticObstacles.Count; i++)
            {
                CharacterTestMovableNaturalObjectStaticContactObstacle obstacle = _staticObstacles[i];
                obstacle.QueryStamp = 0;
                _staticObstacles[i] = obstacle;
            }

            _staticObstacleQueryStamp = 1;
            return _staticObstacleQueryStamp;
        }

        private void EnsureObjectContactQueryStampCapacity(int count)
        {
            if (_objectContactQueryStamps.Capacity < count)
                _objectContactQueryStamps.Capacity = count;
            while (_objectContactQueryStamps.Count < count)
                _objectContactQueryStamps.Add(0);
        }

        private int NextObjectContactQueryStamp()
        {
            if (_objectContactQueryStamp != int.MaxValue)
                return ++_objectContactQueryStamp;

            for (int i = 0; i < _objectContactQueryStamps.Count; i++)
                _objectContactQueryStamps[i] = 0;
            _objectContactQueryStamp = 1;
            return _objectContactQueryStamp;
        }

        private static void GetStateQueryBounds(
            CharacterTestMovableNaturalObjectState state,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            if (state.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
            {
                minX = state.XMillimeters - state.HalfExtentXMillimeters;
                maxX = state.XMillimeters + state.HalfExtentXMillimeters;
                minZ = state.ZMillimeters - state.HalfExtentZMillimeters;
                maxZ = state.ZMillimeters + state.HalfExtentZMillimeters;
            }
            else
            {
                int radius = Math.Max(state.HalfExtentXMillimeters, state.HalfExtentZMillimeters);
                minX = state.XMillimeters - radius;
                maxX = state.XMillimeters + radius;
                minZ = state.ZMillimeters - radius;
                maxZ = state.ZMillimeters + radius;
            }

            minX -= QueryContactEpsilonMillimeters;
            maxX += QueryContactEpsilonMillimeters;
            minZ -= QueryContactEpsilonMillimeters;
            maxZ += QueryContactEpsilonMillimeters;
        }

        private static void GetStateQueryVerticalBounds(
            CharacterTestMovableNaturalObjectState state,
            out int minY,
            out int maxY)
        {
            minY = state.YMillimeters - state.HalfExtentYMillimeters - QueryContactEpsilonMillimeters;
            maxY = state.YMillimeters + state.HalfExtentYMillimeters + QueryContactEpsilonMillimeters;
        }

        private static ObjectContactStateBounds BuildObjectContactStateBounds(
            CharacterTestMovableNaturalObjectState state)
        {
            GetStateQueryBounds(
                state,
                out int minX,
                out int maxX,
                out int minZ,
                out int maxZ);
            GetStateQueryVerticalBounds(
                state,
                out int minY,
                out int maxY);
            return new ObjectContactStateBounds(minX, maxX, minZ, maxZ, minY, maxY);
        }

        private static void SortObstacleIndices(List<int> indices)
        {
            for (int i = 1; i < indices.Count; i++)
            {
                int current = indices[i];
                int insertIndex = i - 1;
                while (insertIndex >= 0 && indices[insertIndex] > current)
                {
                    indices[insertIndex + 1] = indices[insertIndex];
                    insertIndex--;
                }

                indices[insertIndex + 1] = current;
            }
        }

        private static bool ObjectContactBoundsOverlap(
            ObjectContactStateBounds query,
            ObjectContactStateBounds candidate)
        {
            if (!BoundsOverlap(
                query.MinX,
                query.MaxX,
                query.MinZ,
                query.MaxZ,
                candidate.MinX,
                candidate.MaxX,
                candidate.MinZ,
                candidate.MaxZ))
            {
                return false;
            }

            return RangesOverlap(
                query.MinY,
                query.MaxY,
                candidate.MinY,
                candidate.MaxY);
        }

        private static bool BoundsOverlap(
            int leftMinX,
            int leftMaxX,
            int leftMinZ,
            int leftMaxZ,
            int rightMinX,
            int rightMaxX,
            int rightMinZ,
            int rightMaxZ)
        {
            return leftMaxX >= rightMinX
                && rightMaxX >= leftMinX
                && leftMaxZ >= rightMinZ
                && rightMaxZ >= leftMinZ;
        }

        private static bool RangesOverlap(
            int leftMin,
            int leftMax,
            int rightMin,
            int rightMax)
        {
            return leftMax >= rightMin
                && rightMax >= leftMin;
        }

        private static int ToMillimeters(Fix64 meters)
        {
            return CharacterTestDeterministicUnits.Fix64MetersToMillimeters(meters);
        }

        private static StaticObstacleCell ToStaticObstacleCell(int xMillimeters, int zMillimeters)
        {
            return new StaticObstacleCell(
                FloorDiv(xMillimeters, StaticObstacleGridCellSizeMillimeters),
                FloorDiv(zMillimeters, StaticObstacleGridCellSizeMillimeters));
        }

        private static int FloorDiv(int value, int divisor)
        {
            int result = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && ((remainder > 0) != (divisor > 0)))
                result--;
            return result;
        }

        private readonly struct StaticObstacleCell : IEquatable<StaticObstacleCell>
        {
            public StaticObstacleCell(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }
            public int Z { get; }

            public bool Equals(StaticObstacleCell other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is StaticObstacleCell other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }

        private readonly struct ObjectContactStateBounds
        {
            public ObjectContactStateBounds(
                int minX,
                int maxX,
                int minZ,
                int maxZ,
                int minY,
                int maxY)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
                MinY = minY;
                MaxY = maxY;
            }

            public int MinX { get; }

            public int MaxX { get; }

            public int MinZ { get; }

            public int MaxZ { get; }

            public int MinY { get; }

            public int MaxY { get; }
        }

        private sealed class ObjectContactCellEntry
        {
            public ObjectContactCellEntry()
            {
                Indices = new List<int>(4);
            }

            public List<int> Indices { get; }

            public int MinX { get; private set; }

            public int MaxX { get; private set; }

            public int MinZ { get; private set; }

            public int MaxZ { get; private set; }

            public void Add(
                int index,
                int minX,
                int maxX,
                int minZ,
                int maxZ)
            {
                if (Indices.Count == 0)
                {
                    MinX = minX;
                    MaxX = maxX;
                    MinZ = minZ;
                    MaxZ = maxZ;
                }
                else
                {
                    MinX = Math.Min(MinX, minX);
                    MaxX = Math.Max(MaxX, maxX);
                    MinZ = Math.Min(MinZ, minZ);
                    MaxZ = Math.Max(MaxZ, maxZ);
                }

                Indices.Add(index);
            }

            public void Reset()
            {
                Indices.Clear();
                MinX = 0;
                MaxX = 0;
                MinZ = 0;
                MaxZ = 0;
            }
        }

        private sealed class StaticObstacleQueryCache
        {
            private readonly List<CharacterTestMovableNaturalObjectStaticContactObstacle> _results =
                new List<CharacterTestMovableNaturalObjectStaticContactObstacle>();
            private int _topologyRevision = -1;
            private int _queryMinX;
            private int _queryMaxX;
            private int _queryMinZ;
            private int _queryMaxZ;

            internal IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> Results =>
                _results;

            internal bool Matches(
                int topologyRevision,
                int queryMinX,
                int queryMaxX,
                int queryMinZ,
                int queryMaxZ)
            {
                return _topologyRevision == topologyRevision
                    && _queryMinX == queryMinX
                    && _queryMaxX == queryMaxX
                    && _queryMinZ == queryMinZ
                    && _queryMaxZ == queryMaxZ;
            }

            internal void Update(
                int topologyRevision,
                int queryMinX,
                int queryMaxX,
                int queryMinZ,
                int queryMaxZ,
                IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> results)
            {
                _topologyRevision = topologyRevision;
                _queryMinX = queryMinX;
                _queryMaxX = queryMaxX;
                _queryMinZ = queryMinZ;
                _queryMaxZ = queryMaxZ;
                if (_results.Capacity < results.Count)
                    _results.Capacity = results.Count;
                _results.Clear();
                for (int i = 0; i < results.Count; i++)
                    _results.Add(results[i]);
            }

            internal void Clear()
            {
                _topologyRevision = -1;
                _results.Clear();
            }
        }
    }

    internal struct CharacterTestMovableNaturalObjectStaticContactObstacle
    {
        public CharacterTestMovableNaturalObjectStaticContactObstacle(
            CombatPhysicsAabbCollider collider,
            int minX,
            int maxX,
            int minY,
            int maxY,
            int minZ,
            int maxZ)
        {
            Collider = collider;
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
            QueryStamp = 0;
        }

        public CombatPhysicsAabbCollider Collider { get; }
        public int MinX { get; }
        public int MaxX { get; }
        public int MinY { get; }
        public int MaxY { get; }
        public int MinZ { get; }
        public int MaxZ { get; }
        public int QueryStamp { get; set; }
    }

    public static class CharacterTestMovableNaturalObjectSystem
    {
        private const int ContactEpsilonMillimeters = 1;
        private const int VelocityEpsilonMillimetersPerSecond = 8;
        private const int ObjectContactSolverIterations = 2;
        private const int FrictionBaseMillimetersPerSecondSquared = 500;
        private const int FrictionScaleMillimetersPerSecondSquared = 350;
        private const int GravityMillimetersPerSecondSquared = -30000;

        public static void BeginFrame(IReadOnlyList<CharacterTestMovableNaturalObjectState> objects)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Count; i++)
                objects[i]?.BeginFrame();
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public static void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            int tickRate,
            RuntimeFrame frame = default,
            CharacterTestMovableNaturalObjectContactScratch contactScratch = null)
        {
            if (world == null)
                return;

            if (tickRate <= 0)
                tickRate = CharacterTestDeterministicUnits.DefaultTickRate;

            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects = world.MovableNaturalObjects;
            if (objects == null || objects.Count == 0)
                return;

            CharacterTestMovableNaturalObjectContactScratch activeContactScratch = contactScratch;
            if (physicsRuntime != null && physicsRuntime.World != null)
            {
                contactScratch ??= new CharacterTestMovableNaturalObjectContactScratch();
                contactScratch.BuildStaticObstacles(physicsRuntime);
                activeContactScratch = contactScratch;
            }
            else
            {
                contactScratch?.Clear();
            }

            bool simulateVerticalMotion = physicsRuntime != null && physicsRuntime.IsReady;
            for (int i = 0; i < objects.Count; i++)
                AdvanceObject(objects[i], activeContactScratch, tickRate, simulateVerticalMotion);

            ResolveObjectContacts(objects, activeContactScratch, tickRate);
            SyncAllCombatColliders(objects, physicsRuntime);
        }

        public static void SyncAllCombatColliders(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (objects == null || physicsRuntime == null || physicsRuntime.World == null)
                return;

            for (int i = 0; i < objects.Count; i++)
                SyncCombatCollider(objects[i], physicsRuntime);
        }

        public static void SyncCombatCollider(
            CharacterTestMovableNaturalObjectState state,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (state == null || physicsRuntime == null || physicsRuntime.World == null || state.NumericId <= 0)
                return;

            CombatBodyId bodyId = physicsRuntime.GetMovableObjectBodyId(state.NumericId);
            CombatEntityId entityId = physicsRuntime.GetMovableObjectEntityId(state.NumericId);
            CombatColliderId colliderId =
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId);
            if (physicsRuntime.IsMovableObjectColliderSynced(state.NumericId, state.CombatColliderSyncVersion)
                && physicsRuntime.World.TryGetBody(bodyId, out _)
                && HasCurrentMovableObjectCollider(state, physicsRuntime, bodyId, colliderId))
            {
                return;
            }

            FixVector3 center = CreateMovableObjectCenter(state);
            if (physicsRuntime.World.TryGetBody(bodyId, out _)
                && HasCurrentMovableObjectColliderShape(state, physicsRuntime, bodyId, colliderId))
            {
                physicsRuntime.World.SetBodyPosition(bodyId, center);
                physicsRuntime.MarkMovableObjectColliderSynced(state.NumericId, state.CombatColliderSyncVersion);
                return;
            }

            physicsRuntime.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, center));
            switch (state.ShapeKind)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                    physicsRuntime.World.UpsertSphereCollider(CreateMovableObjectSphereCollider(
                        state,
                        bodyId,
                        colliderId));
                    break;
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    physicsRuntime.World.UpsertCapsuleCollider(CreateMovableObjectCapsuleCollider(
                        state,
                        bodyId,
                        colliderId));
                    break;
                default:
                    physicsRuntime.World.UpsertAabbCollider(CreateMovableObjectAabbCollider(
                        state,
                        bodyId,
                        colliderId));
                    break;
            }

            physicsRuntime.MarkMovableObjectColliderSynced(state.NumericId, state.CombatColliderSyncVersion);
        }

        private static bool HasCurrentMovableObjectCollider(
            CharacterTestMovableNaturalObjectState state,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            switch (state.ShapeKind)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                    return physicsRuntime.World.TryGetSphereCollider(bodyId, colliderId, out _);
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    return physicsRuntime.World.TryGetCapsuleCollider(bodyId, colliderId, out _);
                default:
                    return physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out _);
            }
        }

        private static bool HasCurrentMovableObjectColliderShape(
            CharacterTestMovableNaturalObjectState state,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            switch (state.ShapeKind)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                    return physicsRuntime.World.TryGetSphereCollider(
                            bodyId,
                            colliderId,
                            out CombatPhysicsSphereCollider sphere)
                        && sphere.Equals(CreateMovableObjectSphereCollider(state, bodyId, colliderId));
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    return physicsRuntime.World.TryGetCapsuleCollider(
                            bodyId,
                            colliderId,
                            out CombatPhysicsCapsuleCollider capsule)
                        && capsule.Equals(CreateMovableObjectCapsuleCollider(state, bodyId, colliderId));
                default:
                    return physicsRuntime.World.TryGetAabbCollider(
                            bodyId,
                            colliderId,
                            out CombatPhysicsAabbCollider aabb)
                        && aabb.Equals(CreateMovableObjectAabbCollider(state, bodyId, colliderId));
            }
        }

        private static FixVector3 CreateMovableObjectCenter(
            CharacterTestMovableNaturalObjectState state)
        {
            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(state.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(state.YMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(state.ZMillimeters));
        }

        private static CombatPhysicsSphereCollider CreateMovableObjectSphereCollider(
            CharacterTestMovableNaturalObjectState state,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            return new CombatPhysicsSphereCollider(
                bodyId,
                colliderId,
                CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer,
                FixVector3.Zero,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(GetHorizontalRadiusMillimeters(state)),
                state.Material);
        }

        private static CombatPhysicsCapsuleCollider CreateMovableObjectCapsuleCollider(
            CharacterTestMovableNaturalObjectState state,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            Fix64 radius = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                GetHorizontalRadiusMillimeters(state));
            Fix64 halfHeight = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                state.HalfExtentYMillimeters);
            Fix64 segmentHalfLength = Fix64.Max(Fix64.Zero, halfHeight - radius);
            return new CombatPhysicsCapsuleCollider(
                bodyId,
                colliderId,
                CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer,
                new FixVector3(Fix64.Zero, -segmentHalfLength, Fix64.Zero),
                new FixVector3(Fix64.Zero, segmentHalfLength, Fix64.Zero),
                radius,
                state.Material);
        }

        private static CombatPhysicsAabbCollider CreateMovableObjectAabbCollider(
            CharacterTestMovableNaturalObjectState state,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            Fix64 halfExtentX = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                state.HalfExtentXMillimeters);
            Fix64 halfExtentY = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                state.HalfExtentYMillimeters);
            Fix64 halfExtentZ = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                state.HalfExtentZMillimeters);
            return new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer,
                new FixVector3(-halfExtentX, -halfExtentY, -halfExtentZ),
                new FixVector3(halfExtentX, halfExtentY, halfExtentZ),
                state.Material);
        }

        public static CharacterTestMovableNaturalObjectSummary CaptureSummary(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects)
        {
            if (objects == null || objects.Count == 0)
                return CharacterTestMovableNaturalObjectSummary.Empty;

            var snapshots = new CharacterTestMovableNaturalObjectSnapshot[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                snapshots[i] = objects[i]?.CaptureSnapshot();
            return new CharacterTestMovableNaturalObjectSummary(snapshots);
        }

        private static void AdvanceObject(
            CharacterTestMovableNaturalObjectState state,
            CharacterTestMovableNaturalObjectContactScratch contactScratch,
            int tickRate,
            bool simulateVerticalMotion)
        {
            if (state == null)
                return;

            state.ApplyPendingVelocity();
            int previousX = state.XMillimeters;
            int previousZ = state.ZMillimeters;
            int nextX = previousX + CharacterTestDeterministicMath.DivideRoundToInt(
                state.VelocityXMillimetersPerSecond,
                tickRate);
            int nextZ = previousZ + CharacterTestDeterministicMath.DivideRoundToInt(
                state.VelocityZMillimetersPerSecond,
                tickRate);
            state.SetHorizontalPosition(nextX, nextZ);

            IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> staticObstacles =
                contactScratch?.QueryStaticObstacles(state);
            if (simulateVerticalMotion)
                AdvanceVerticalMotion(state, staticObstacles, tickRate);
            ResolveStaticContacts(state, staticObstacles);
            ApplyFriction(state, tickRate);
            UpdateBehaviorFromVelocity(state);
        }

        private static void AdvanceVerticalMotion(
            CharacterTestMovableNaturalObjectState state,
            IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> staticObstacles,
            int tickRate)
        {
            int previousY = state.YMillimeters;
            int previousVelocityY = state.VelocityYMillimetersPerSecond;
            if (previousVelocityY == 0
                && TryFindRestingSupport(state, staticObstacles, out int restingTop))
            {
                state.SetVerticalPosition(restingTop + state.HalfExtentYMillimeters);
                return;
            }

            int nextVelocityY = previousVelocityY
                + CharacterTestDeterministicMath.DivideRoundToInt(
                    GravityMillimetersPerSecondSquared,
                    tickRate);
            int nextY = previousY + CharacterTestDeterministicMath.DivideRoundToInt(
                nextVelocityY,
                tickRate);
            state.SetVerticalPosition(nextY);
            state.SetVerticalVelocity(nextVelocityY);
            ResolveVerticalStaticContact(
                state,
                staticObstacles,
                previousY,
                nextY,
                nextVelocityY,
                tickRate);
        }

        private static bool TryFindRestingSupport(
            CharacterTestMovableNaturalObjectState state,
            IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> staticObstacles,
            out int supportTop)
        {
            supportTop = int.MinValue;
            if (state == null || staticObstacles == null)
                return false;

            int objectBottom = state.YMillimeters - state.HalfExtentYMillimeters;
            for (int i = 0; i < staticObstacles.Count; i++)
            {
                CharacterTestMovableNaturalObjectStaticContactObstacle obstacle = staticObstacles[i];
                if (!StaticFootprintOverlaps(state, obstacle)
                    || Math.Abs(objectBottom - obstacle.MaxY) > ContactEpsilonMillimeters)
                {
                    continue;
                }

                if (obstacle.MaxY > supportTop)
                    supportTop = obstacle.MaxY;
            }

            return supportTop != int.MinValue;
        }

        private static void ResolveVerticalStaticContact(
            CharacterTestMovableNaturalObjectState state,
            IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> staticObstacles,
            int previousY,
            int proposedY,
            int incomingVelocityY,
            int tickRate)
        {
            if (state == null || staticObstacles == null || staticObstacles.Count == 0
                || incomingVelocityY == 0)
            {
                return;
            }

            bool falling = incomingVelocityY < 0;
            int previousEdge = previousY
                + (falling ? -state.HalfExtentYMillimeters : state.HalfExtentYMillimeters);
            int proposedEdge = proposedY
                + (falling ? -state.HalfExtentYMillimeters : state.HalfExtentYMillimeters);
            int contactPlane = falling ? int.MinValue : int.MaxValue;
            int contactIndex = -1;
            for (int i = 0; i < staticObstacles.Count; i++)
            {
                CharacterTestMovableNaturalObjectStaticContactObstacle obstacle = staticObstacles[i];
                if (!StaticFootprintOverlaps(state, obstacle))
                    continue;

                int candidatePlane = falling ? obstacle.MaxY : obstacle.MinY;
                bool crossed = falling
                    ? previousEdge >= candidatePlane - ContactEpsilonMillimeters
                        && proposedEdge <= candidatePlane + ContactEpsilonMillimeters
                    : previousEdge <= candidatePlane + ContactEpsilonMillimeters
                        && proposedEdge >= candidatePlane - ContactEpsilonMillimeters;
                if (!crossed)
                    continue;

                if ((falling && candidatePlane > contactPlane)
                    || (!falling && candidatePlane < contactPlane))
                {
                    contactPlane = candidatePlane;
                    contactIndex = i;
                }
            }

            if (contactIndex < 0)
                return;

            CharacterTestMovableNaturalObjectStaticContactObstacle contact = staticObstacles[contactIndex];
            int resolvedY = contactPlane
                + (falling ? state.HalfExtentYMillimeters : -state.HalfExtentYMillimeters);
            int displacementY = resolvedY - proposedY;
            int elasticityMilli = Math.Max(
                CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(contact.Collider.SurfaceMaterial.Elasticity)),
                CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(state.Material.Elasticity)));
            int reflectedVelocityY = ReflectVelocity(
                incomingVelocityY,
                displacementY,
                elasticityMilli);
            int minimumBounceVelocity = Math.Max(
                VelocityEpsilonMillimetersPerSecond,
                Math.Abs(CharacterTestDeterministicMath.DivideRoundToInt(
                    GravityMillimetersPerSecondSquared,
                    tickRate)));
            if (Math.Abs(reflectedVelocityY) <= minimumBounceVelocity)
                reflectedVelocityY = 0;

            int pressure = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)Math.Abs(incomingVelocityY) * Math.Max(1, state.MassGrams),
                CharacterTestDeterministicUnits.PositionScale);
            state.SetVerticalPosition(resolvedY);
            state.SetVerticalVelocity(reflectedVelocityY);
            state.MarkBoundaryContact(
                (int)contact.Collider.SurfaceMaterial.Kind,
                0,
                0,
                0,
                0,
                pressure,
                reflectedVelocityY == 0 ? CharacterTestMovableObjectBehavior.PressureBlocked : CharacterTestMovableObjectBehavior.VerticalRebound);
        }

        private static bool StaticFootprintOverlaps(
            CharacterTestMovableNaturalObjectState state,
            CharacterTestMovableNaturalObjectStaticContactObstacle obstacle)
        {
            if (state.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
            {
                return state.XMillimeters + state.HalfExtentXMillimeters
                        > obstacle.MinX + ContactEpsilonMillimeters
                    && obstacle.MaxX
                        > state.XMillimeters - state.HalfExtentXMillimeters + ContactEpsilonMillimeters
                    && state.ZMillimeters + state.HalfExtentZMillimeters
                        > obstacle.MinZ + ContactEpsilonMillimeters
                    && obstacle.MaxZ
                        > state.ZMillimeters - state.HalfExtentZMillimeters + ContactEpsilonMillimeters;
            }

            int closestX = ClampInt(state.XMillimeters, obstacle.MinX, obstacle.MaxX);
            int closestZ = ClampInt(state.ZMillimeters, obstacle.MinZ, obstacle.MaxZ);
            int dx = state.XMillimeters - closestX;
            int dz = state.ZMillimeters - closestZ;
            int radius = GetHorizontalRadiusMillimeters(state);
            return (long)dx * dx + (long)dz * dz
                <= (long)(radius + ContactEpsilonMillimeters) * (radius + ContactEpsilonMillimeters);
        }

        private static void ResolveStaticContacts(
            CharacterTestMovableNaturalObjectState state,
            IReadOnlyList<CharacterTestMovableNaturalObjectStaticContactObstacle> staticObstacles)
        {
            if (state == null || staticObstacles == null || staticObstacles.Count == 0)
                return;

            for (int i = 0; i < staticObstacles.Count; i++)
            {
                CharacterTestMovableNaturalObjectStaticContactObstacle obstacle = staticObstacles[i];
                CombatPhysicsAabbCollider collider = obstacle.Collider;
                if (state.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
                {
                    if (!Overlaps(
                            state.XMillimeters - state.HalfExtentXMillimeters,
                            state.XMillimeters + state.HalfExtentXMillimeters,
                            state.YMillimeters - state.HalfExtentYMillimeters,
                            state.YMillimeters + state.HalfExtentYMillimeters,
                            state.ZMillimeters - state.HalfExtentZMillimeters,
                            state.ZMillimeters + state.HalfExtentZMillimeters,
                            obstacle.MinX,
                            obstacle.MaxX,
                            obstacle.MinY,
                            obstacle.MaxY,
                            obstacle.MinZ,
                            obstacle.MaxZ))
                    {
                        continue;
                    }

                    ResolveStaticOverlap(
                        state,
                        collider,
                        obstacle.MinX,
                        obstacle.MaxX,
                        obstacle.MinZ,
                        obstacle.MaxZ);
                    continue;
                }

                if (state.YMillimeters + state.HalfExtentYMillimeters <= obstacle.MinY + ContactEpsilonMillimeters
                    || obstacle.MaxY <= state.YMillimeters - state.HalfExtentYMillimeters + ContactEpsilonMillimeters)
                {
                    continue;
                }

                if (TryResolveStaticCircleOverlap(
                        state,
                        obstacle.MinX,
                        obstacle.MaxX,
                        obstacle.MinZ,
                        obstacle.MaxZ,
                        out int displacementX,
                        out int displacementZ))
                {
                    ApplyStaticContactResolution(
                        state,
                        collider,
                        displacementX,
                        displacementZ);
                }
            }
        }

        private static void ResolveStaticOverlap(
            CharacterTestMovableNaturalObjectState state,
            CombatPhysicsAabbCollider collider,
            int targetMinX,
            int targetMaxX,
            int targetMinZ,
            int targetMaxZ)
        {
            int objectMinX = state.XMillimeters - state.HalfExtentXMillimeters;
            int objectMaxX = state.XMillimeters + state.HalfExtentXMillimeters;
            int objectMinZ = state.ZMillimeters - state.HalfExtentZMillimeters;
            int objectMaxZ = state.ZMillimeters + state.HalfExtentZMillimeters;
            int moveLeft = -(objectMaxX - targetMinX);
            int moveRight = targetMaxX - objectMinX;
            int moveBack = -(objectMaxZ - targetMinZ);
            int moveForward = targetMaxZ - objectMinZ;
            int resolveX = Math.Abs(moveLeft) < Math.Abs(moveRight) ? moveLeft : moveRight;
            int resolveZ = Math.Abs(moveBack) < Math.Abs(moveForward) ? moveBack : moveForward;
            bool useX = Math.Abs(resolveX) <= Math.Abs(resolveZ);
            ApplyStaticContactResolution(
                state,
                collider,
                useX ? resolveX : 0,
                useX ? 0 : resolveZ);
        }

        private static bool TryResolveStaticCircleOverlap(
            CharacterTestMovableNaturalObjectState state,
            int targetMinX,
            int targetMaxX,
            int targetMinZ,
            int targetMaxZ,
            out int displacementX,
            out int displacementZ)
        {
            displacementX = 0;
            displacementZ = 0;
            int radius = GetHorizontalRadiusMillimeters(state);
            int closestX = ClampInt(state.XMillimeters, targetMinX, targetMaxX);
            int closestZ = ClampInt(state.ZMillimeters, targetMinZ, targetMaxZ);
            int dx = state.XMillimeters - closestX;
            int dz = state.ZMillimeters - closestZ;
            if (dx == 0 && dz == 0)
            {
                int left = Math.Abs(state.XMillimeters - targetMinX);
                int right = Math.Abs(targetMaxX - state.XMillimeters);
                int back = Math.Abs(state.ZMillimeters - targetMinZ);
                int forward = Math.Abs(targetMaxZ - state.ZMillimeters);
                int minDistance = Math.Min(Math.Min(left, right), Math.Min(back, forward));
                if (minDistance == left)
                    displacementX = -(radius + left);
                else if (minDistance == right)
                    displacementX = radius + right;
                else if (minDistance == back)
                    displacementZ = -(radius + back);
                else
                    displacementZ = radius + forward;
                return displacementX != 0 || displacementZ != 0;
            }

            int distance = CharacterTestDeterministicMath.Magnitude(dx, dz);
            int overlap = radius - distance;
            if (overlap <= ContactEpsilonMillimeters)
                return false;

            if (Math.Abs(dx) >= Math.Abs(dz))
                displacementX = dx >= 0 ? overlap : -overlap;
            else
                displacementZ = dz >= 0 ? overlap : -overlap;
            return true;
        }

        private static void ApplyStaticContactResolution(
            CharacterTestMovableNaturalObjectState state,
            CombatPhysicsAabbCollider collider,
            int displacementX,
            int displacementZ)
        {
            int incomingX = state.VelocityXMillimetersPerSecond;
            int incomingZ = state.VelocityZMillimetersPerSecond;
            int speed = CharacterTestDeterministicMath.Magnitude(incomingX, incomingZ);
            int pressure = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)Math.Max(0, speed) * Math.Max(1, state.MassGrams),
                CharacterTestDeterministicUnits.PositionScale);
            int elasticityMilli = Math.Max(
                CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(collider.SurfaceMaterial.Elasticity)),
                CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(state.Material.Elasticity)));
            int nextVelocityX = incomingX;
            int nextVelocityZ = incomingZ;
            if (displacementX != 0)
            {
                state.SetHorizontalPosition(state.XMillimeters + displacementX, state.ZMillimeters);
                nextVelocityX = ReflectVelocity(incomingX, displacementX, elasticityMilli);
            }
            else if (displacementZ != 0)
            {
                state.SetHorizontalPosition(state.XMillimeters, state.ZMillimeters + displacementZ);
                nextVelocityZ = ReflectVelocity(incomingZ, displacementZ, elasticityMilli);
            }

            state.SetVelocity(nextVelocityX, nextVelocityZ);
            state.MarkBoundaryContact(
                (int)collider.SurfaceMaterial.Kind,
                incomingX,
                incomingZ,
                displacementX,
                displacementZ,
                pressure,
                elasticityMilli > 0 && CharacterTestDeterministicMath.Magnitude(nextVelocityX, nextVelocityZ) > VelocityEpsilonMillimetersPerSecond
                    ? CharacterTestMovableObjectBehavior.ReboundReturnedForce
                    : CharacterTestMovableObjectBehavior.PressureBlocked);
        }

        private static void ResolveObjectContacts(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects,
            CharacterTestMovableNaturalObjectContactScratch contactScratch,
            int tickRate)
        {
            if (objects == null || objects.Count <= 1)
                return;
            if (contactScratch == null)
            {
                ResolveObjectContactsFullScan(objects, tickRate);
                return;
            }

            IReadOnlyList<CharacterTestMovableNaturalObjectState> contactObjects =
                contactScratch.PrepareObjectContactCandidates(objects);
            if (contactObjects.Count <= 1)
                return;

            for (int iteration = 0; iteration < ObjectContactSolverIterations; iteration++)
            {
                bool changed = false;
                contactScratch.RebuildObjectContactIndex();
                for (int i = 0; i < contactObjects.Count; i++)
                {
                    CharacterTestMovableNaturalObjectState a = contactObjects[i];
                    int minCandidateIndex = i + 1;
                    while (minCandidateIndex < contactObjects.Count)
                    {
                        bool restartQuery = false;
                        IReadOnlyList<int> candidateIndices =
                            contactScratch.QueryObjectContactCandidateIndices(i, minCandidateIndex);
                        for (int candidate = 0; candidate < candidateIndices.Count; candidate++)
                        {
                            int j = candidateIndices[candidate];
                            minCandidateIndex = j + 1;
                            CharacterTestMovableNaturalObjectState b = contactObjects[j];
                            if (!ObjectsOverlap(a, b))
                                continue;

                            if (!ResolveObjectPair(a, b))
                                continue;

                            changed = true;
                            restartQuery = minCandidateIndex < contactObjects.Count;
                            if (restartQuery)
                                contactScratch.RebuildObjectContactIndex();
                            break;
                        }

                        if (!restartQuery)
                            break;
                    }
                }

                if (!changed)
                    return;
            }
        }

        private static void ResolveObjectContactsFullScan(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects,
            int tickRate)
        {
            for (int iteration = 0; iteration < ObjectContactSolverIterations; iteration++)
            {
                bool changed = false;
                for (int i = 0; i < objects.Count; i++)
                {
                    CharacterTestMovableNaturalObjectState a = objects[i];
                    if (a == null)
                        continue;

                    for (int j = i + 1; j < objects.Count; j++)
                    {
                        CharacterTestMovableNaturalObjectState b = objects[j];
                        if (b == null || !ObjectsOverlap(a, b))
                            continue;

                        changed |= ResolveObjectPair(a, b);
                    }
                }

                if (!changed)
                    return;
            }
        }

        private static bool ResolveObjectPair(
            CharacterTestMovableNaturalObjectState a,
            CharacterTestMovableNaturalObjectState b)
        {
            if (!TryResolveObjectContactAxis(a, b, out int normalX, out int normalZ, out int overlap))
                return false;

            int moveA = -(overlap / 2);
            int moveB = overlap - overlap / 2;
            a.MoveByContactPair(normalX * moveA, normalZ * moveA, a.NumericId, b.NumericId);
            b.MoveByContactPair(normalX * moveB, normalZ * moveB, a.NumericId, b.NumericId);

            int relativeAlong =
                (a.VelocityXMillimetersPerSecond - b.VelocityXMillimetersPerSecond) * normalX
                + (a.VelocityZMillimetersPerSecond - b.VelocityZMillimetersPerSecond) * normalZ;
            if (relativeAlong > VelocityEpsilonMillimetersPerSecond)
            {
                TransferVelocity(a, b, normalX, normalZ, relativeAlong);
            }
            else if (relativeAlong < -VelocityEpsilonMillimetersPerSecond)
            {
                TransferVelocity(b, a, -normalX, -normalZ, -relativeAlong);
            }

            return true;
        }

        private static bool TryResolveObjectContactAxis(
            CharacterTestMovableNaturalObjectState a,
            CharacterTestMovableNaturalObjectState b,
            out int normalX,
            out int normalZ,
            out int overlap)
        {
            normalX = 0;
            normalZ = 0;
            overlap = 0;
            if (a == null || b == null || !VerticalOverlaps(a, b))
                return false;

            if (a.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb
                && b.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
            {
                int overlapX = Math.Min(
                    a.XMillimeters + a.HalfExtentXMillimeters,
                    b.XMillimeters + b.HalfExtentXMillimeters)
                    - Math.Max(
                        a.XMillimeters - a.HalfExtentXMillimeters,
                        b.XMillimeters - b.HalfExtentXMillimeters);
                int overlapZ = Math.Min(
                    a.ZMillimeters + a.HalfExtentZMillimeters,
                    b.ZMillimeters + b.HalfExtentZMillimeters)
                    - Math.Max(
                        a.ZMillimeters - a.HalfExtentZMillimeters,
                        b.ZMillimeters - b.HalfExtentZMillimeters);
                if (overlapX <= ContactEpsilonMillimeters || overlapZ <= ContactEpsilonMillimeters)
                    return false;

                bool useX = overlapX <= overlapZ;
                normalX = useX
                    ? b.XMillimeters >= a.XMillimeters ? 1 : -1
                    : 0;
                normalZ = useX
                    ? 0
                    : b.ZMillimeters >= a.ZMillimeters ? 1 : -1;
                overlap = useX ? overlapX : overlapZ;
                return true;
            }

            if (a.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
                return TryResolveAabbCircleAxis(a, b, out normalX, out normalZ, out overlap);
            if (b.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
            {
                if (!TryResolveAabbCircleAxis(b, a, out int fromBToA_X, out int fromBToA_Z, out overlap))
                    return false;

                normalX = -fromBToA_X;
                normalZ = -fromBToA_Z;
                return true;
            }

            int radius = GetHorizontalRadiusMillimeters(a) + GetHorizontalRadiusMillimeters(b);
            int dx = b.XMillimeters - a.XMillimeters;
            int dz = b.ZMillimeters - a.ZMillimeters;
            int distance = CharacterTestDeterministicMath.Magnitude(dx, dz);
            overlap = radius - distance;
            if (overlap <= ContactEpsilonMillimeters)
                return false;

            if (Math.Abs(dx) >= Math.Abs(dz))
                normalX = dx >= 0 ? 1 : -1;
            else
                normalZ = dz >= 0 ? 1 : -1;
            return true;
        }

        private static bool TryResolveAabbCircleAxis(
            CharacterTestMovableNaturalObjectState aabb,
            CharacterTestMovableNaturalObjectState circle,
            out int normalX,
            out int normalZ,
            out int overlap)
        {
            normalX = 0;
            normalZ = 0;
            overlap = 0;
            int minX = aabb.XMillimeters - aabb.HalfExtentXMillimeters;
            int maxX = aabb.XMillimeters + aabb.HalfExtentXMillimeters;
            int minZ = aabb.ZMillimeters - aabb.HalfExtentZMillimeters;
            int maxZ = aabb.ZMillimeters + aabb.HalfExtentZMillimeters;
            int closestX = ClampInt(circle.XMillimeters, minX, maxX);
            int closestZ = ClampInt(circle.ZMillimeters, minZ, maxZ);
            int dx = circle.XMillimeters - closestX;
            int dz = circle.ZMillimeters - closestZ;
            int radius = GetHorizontalRadiusMillimeters(circle);
            if (dx == 0 && dz == 0)
            {
                int left = Math.Abs(circle.XMillimeters - minX);
                int right = Math.Abs(maxX - circle.XMillimeters);
                int back = Math.Abs(circle.ZMillimeters - minZ);
                int forward = Math.Abs(maxZ - circle.ZMillimeters);
                int minDistance = Math.Min(Math.Min(left, right), Math.Min(back, forward));
                if (minDistance == left)
                {
                    normalX = -1;
                    overlap = radius + left;
                }
                else if (minDistance == right)
                {
                    normalX = 1;
                    overlap = radius + right;
                }
                else if (minDistance == back)
                {
                    normalZ = -1;
                    overlap = radius + back;
                }
                else
                {
                    normalZ = 1;
                    overlap = radius + forward;
                }

                return overlap > ContactEpsilonMillimeters;
            }

            int distance = CharacterTestDeterministicMath.Magnitude(dx, dz);
            overlap = radius - distance;
            if (overlap <= ContactEpsilonMillimeters)
                return false;

            if (Math.Abs(dx) >= Math.Abs(dz))
                normalX = dx >= 0 ? 1 : -1;
            else
                normalZ = dz >= 0 ? 1 : -1;
            return true;
        }

        private static void TransferVelocity(
            CharacterTestMovableNaturalObjectState source,
            CharacterTestMovableNaturalObjectState target,
            int normalX,
            int normalZ,
            int relativeAlong)
        {
            int sourceMass = Math.Max(1, source.MassGrams);
            int targetMass = Math.Max(1, target.MassGrams);
            int transfer = CharacterTestDeterministicMath.DivideRoundToInt(
                (long)relativeAlong * sourceMass,
                sourceMass + targetMass);
            if (transfer <= 0)
                return;

            int transferX = normalX * transfer;
            int transferZ = normalZ * transfer;
            source.SetVelocity(
                source.VelocityXMillimetersPerSecond - transferX,
                source.VelocityZMillimetersPerSecond - transferZ);
            target.AccumulateObjectContact(
                source,
                transferX,
                transferZ,
                CharacterTestDeterministicMath.DivideRoundToInt(
                    (long)transfer * sourceMass,
                    CharacterTestDeterministicUnits.PositionScale));
            target.ApplyPendingVelocity();
            source.MarkObjectContact(
                target,
                source.VelocityXMillimetersPerSecond,
                source.VelocityZMillimetersPerSecond,
                0,
                0,
                CharacterTestDeterministicMath.DivideRoundToInt(
                    (long)transfer * sourceMass,
                    CharacterTestDeterministicUnits.PositionScale),
                CharacterTestMovableObjectBehavior.ObjectContactSource);
            target.SetBehavior(CharacterTestMovableObjectBehavior.ObjectContactReceived);
        }

        private static void ApplyFriction(CharacterTestMovableNaturalObjectState state, int tickRate)
        {
            int speed = CharacterTestDeterministicMath.Magnitude(
                state.VelocityXMillimetersPerSecond,
                state.VelocityZMillimetersPerSecond);
            if (speed <= 0)
                return;

            int frictionMilli = CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(state.Material.Friction));
            int deceleration = FrictionBaseMillimetersPerSecondSquared
                + CharacterTestDeterministicMath.MulDivRoundToInt(
                    frictionMilli,
                    FrictionScaleMillimetersPerSecondSquared,
                    CharacterTestDeterministicUnits.ScaleFactorScale);
            int maxDelta = Math.Max(1, CharacterTestDeterministicMath.DivideRoundToInt(deceleration, tickRate));
            CharacterTestDeterministicMath.MoveTowards2D(
                state.VelocityXMillimetersPerSecond,
                state.VelocityZMillimetersPerSecond,
                0,
                0,
                maxDelta,
                out int nextX,
                out int nextZ);
            state.SetVelocity(nextX, nextZ);
        }

        private static void UpdateBehaviorFromVelocity(CharacterTestMovableNaturalObjectState state)
        {
            if (state == null || state.ContactCount > 0)
                return;

            if (state.VelocityYMillimetersPerSecond < -VelocityEpsilonMillimetersPerSecond)
            {
                state.SetBehavior(CharacterTestMovableObjectBehavior.Falling);
                return;
            }

            if (state.VelocityYMillimetersPerSecond > VelocityEpsilonMillimetersPerSecond)
            {
                state.SetBehavior(CharacterTestMovableObjectBehavior.VerticalRebound);
                return;
            }

            int speed = CharacterTestDeterministicMath.Magnitude(
                state.VelocityXMillimetersPerSecond,
                state.VelocityZMillimetersPerSecond);
            state.SetBehavior(speed > VelocityEpsilonMillimetersPerSecond ? CharacterTestMovableObjectBehavior.PushSliding : CharacterTestMovableObjectBehavior.Idle);
        }

        private static int ReflectVelocity(int velocity, int displacement, int elasticityMilli)
        {
            if (elasticityMilli <= 0)
                return 0;

            bool movingIntoSurface = (velocity > 0 && displacement < 0)
                || (velocity < 0 && displacement > 0);
            if (!movingIntoSurface)
                return velocity;

            return CharacterTestDeterministicMath.MulDivRoundToInt(
                -velocity,
                elasticityMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale);
        }

        private static bool ObjectsOverlap(
            CharacterTestMovableNaturalObjectState a,
            CharacterTestMovableNaturalObjectState b)
        {
            if (a.ShapeKind != CharacterTestMovableNaturalObjectShapeKind.Aabb
                || b.ShapeKind != CharacterTestMovableNaturalObjectShapeKind.Aabb)
            {
                return ShapeFootprintsOverlap(a, b);
            }

            return Overlaps(
                a.XMillimeters - a.HalfExtentXMillimeters,
                a.XMillimeters + a.HalfExtentXMillimeters,
                a.YMillimeters - a.HalfExtentYMillimeters,
                a.YMillimeters + a.HalfExtentYMillimeters,
                a.ZMillimeters - a.HalfExtentZMillimeters,
                a.ZMillimeters + a.HalfExtentZMillimeters,
                b.XMillimeters - b.HalfExtentXMillimeters,
                b.XMillimeters + b.HalfExtentXMillimeters,
                b.YMillimeters - b.HalfExtentYMillimeters,
                b.YMillimeters + b.HalfExtentYMillimeters,
                b.ZMillimeters - b.HalfExtentZMillimeters,
                b.ZMillimeters + b.HalfExtentZMillimeters);
        }

        private static bool ShapeFootprintsOverlap(
            CharacterTestMovableNaturalObjectState a,
            CharacterTestMovableNaturalObjectState b)
        {
            if (a == null || b == null || !VerticalOverlaps(a, b))
                return false;

            if (a.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
                return CircleOverlapsAabb(b, a);
            if (b.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
                return CircleOverlapsAabb(a, b);

            int radius = GetHorizontalRadiusMillimeters(a) + GetHorizontalRadiusMillimeters(b);
            int dx = a.XMillimeters - b.XMillimeters;
            int dz = a.ZMillimeters - b.ZMillimeters;
            return (long)dx * dx + (long)dz * dz
                <= (long)(radius + ContactEpsilonMillimeters) * (radius + ContactEpsilonMillimeters);
        }

        private static bool CircleOverlapsAabb(
            CharacterTestMovableNaturalObjectState circle,
            CharacterTestMovableNaturalObjectState aabb)
        {
            int closestX = ClampInt(
                circle.XMillimeters,
                aabb.XMillimeters - aabb.HalfExtentXMillimeters,
                aabb.XMillimeters + aabb.HalfExtentXMillimeters);
            int closestZ = ClampInt(
                circle.ZMillimeters,
                aabb.ZMillimeters - aabb.HalfExtentZMillimeters,
                aabb.ZMillimeters + aabb.HalfExtentZMillimeters);
            int dx = circle.XMillimeters - closestX;
            int dz = circle.ZMillimeters - closestZ;
            int radius = GetHorizontalRadiusMillimeters(circle);
            return (long)dx * dx + (long)dz * dz
                <= (long)(radius + ContactEpsilonMillimeters) * (radius + ContactEpsilonMillimeters);
        }

        private static bool VerticalOverlaps(
            CharacterTestMovableNaturalObjectState a,
            CharacterTestMovableNaturalObjectState b)
        {
            return a.YMillimeters + a.HalfExtentYMillimeters > b.YMillimeters - b.HalfExtentYMillimeters + ContactEpsilonMillimeters
                && b.YMillimeters + b.HalfExtentYMillimeters > a.YMillimeters - a.HalfExtentYMillimeters + ContactEpsilonMillimeters;
        }

        private static int GetHorizontalRadiusMillimeters(CharacterTestMovableNaturalObjectState state)
        {
            return Math.Max(1, Math.Max(state.HalfExtentXMillimeters, state.HalfExtentZMillimeters));
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static bool Overlaps(
            int aMinX,
            int aMaxX,
            int aMinY,
            int aMaxY,
            int aMinZ,
            int aMaxZ,
            int bMinX,
            int bMaxX,
            int bMinY,
            int bMaxY,
            int bMinZ,
            int bMaxZ)
        {
            return aMaxX > bMinX + ContactEpsilonMillimeters
                && bMaxX > aMinX + ContactEpsilonMillimeters
                && aMaxY > bMinY + ContactEpsilonMillimeters
                && bMaxY > aMinY + ContactEpsilonMillimeters
                && aMaxZ > bMinZ + ContactEpsilonMillimeters
                && bMaxZ > aMinZ + ContactEpsilonMillimeters;
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }
}


