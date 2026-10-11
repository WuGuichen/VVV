using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestDynamicFieldTargetIndex
    {
        private static readonly Fix64 CellSize = Fix64.FromInt(4);
        private const int InitialTargetCapacity = 128;
        private const int InitialCellCapacity = 512;
        private const int InitialCellListCapacity = 4;
        private static readonly IComparer<CombatQueryResult> ResultComparer = CombatQueryResultComparer.Instance;

        private readonly List<TargetEntry> _targets;
        private readonly Dictionary<FieldCell, List<int>> _cells;
        private readonly List<List<int>> _activeCellLists;
        private readonly List<List<int>> _cellListPool;
        private int _queryStamp;

        public CharacterTestDynamicFieldTargetIndex()
        {
            _targets = new List<TargetEntry>(InitialTargetCapacity);
            _cells = new Dictionary<FieldCell, List<int>>(InitialCellCapacity);
            _activeCellLists = new List<List<int>>(InitialCellCapacity);
            _cellListPool = new List<List<int>>(InitialCellCapacity);
            for (int i = 0; i < InitialCellCapacity; i++)
                _cellListPool.Add(new List<int>(InitialCellListCapacity));
            _cells.Add(new FieldCell(int.MinValue, int.MinValue), _cellListPool[0]);
            _cells.Clear();
        }

        public int TargetCount => _targets.Count;

        public void EnsureResultCapacity(List<CombatQueryResult> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            if (results.Capacity < _targets.Count)
                results.Capacity = _targets.Count;
        }

        public void Rebuild(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CombatPhysicsLayerMask layerMask)
        {
            ClearCells();
            _targets.Clear();
            if (world == null || physicsRuntime == null || physicsRuntime.World == null)
                return;

            if (layerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer))
                AddCharacterTargets(world, physicsRuntime, layerMask);
            if (layerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer))
                AddMovableTargets(world, physicsRuntime, layerMask);
        }

        public int QueryVerticalCapsule(
            CombatQueryHeader header,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            List<CombatQueryResult> results,
            bool includeSourceEntity)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            int startCount = results.Count;
            if (_targets.Count == 0 || radius < Fix64.Zero)
                return 0;

            int queryStamp = NextQueryStamp();
            Fix64 minY = Fix64.Min(pointA.Y, pointB.Y);
            Fix64 maxY = Fix64.Max(pointA.Y, pointB.Y);
            Fix64 queryMinX = pointA.X - radius;
            Fix64 queryMaxX = pointA.X + radius;
            Fix64 queryMinZ = pointA.Z - radius;
            Fix64 queryMaxZ = pointA.Z + radius;
            FieldCell minCell = ToCell(queryMinX, queryMinZ);
            FieldCell maxCell = ToCell(queryMaxX, queryMaxZ);

            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    if (!_cells.TryGetValue(new FieldCell(x, z), out List<int> indices))
                        continue;

                    for (int i = 0; i < indices.Count; i++)
                    {
                        int targetIndex = indices[i];
                        TargetEntry target = _targets[targetIndex];
                        if (target.QueryStamp == queryStamp)
                            continue;

                        target.QueryStamp = queryStamp;
                        _targets[targetIndex] = target;
                        if (!includeSourceEntity && header.SourceEntityId.Equals(target.EntityId))
                            continue;
                        if (target.MaxX < queryMinX
                            || target.MinX > queryMaxX
                            || target.MaxZ < queryMinZ
                            || target.MinZ > queryMaxZ)
                        {
                            continue;
                        }

                        if (!TryHitVerticalCapsule(
                                target,
                                pointA.X,
                                pointA.Z,
                                minY,
                                maxY,
                                radius,
                                out FixVector3 point))
                        {
                            continue;
                        }

                        results.Add(new CombatQueryResult(
                            header,
                            target.EntityId,
                            target.BodyId,
                            target.ColliderId,
                            target.ShapeKind,
                            Fix64.Zero,
                            point,
                            FixVector3.Zero));
                    }
                }
            }

            int added = results.Count - startCount;
            if (added > 1)
                SortResultRange(results, startCount, added);
            return added;
        }

        private static void SortResultRange(List<CombatQueryResult> results, int startIndex, int count)
        {
            int endIndex = startIndex + count;
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                CombatQueryResult current = results[i];
                int insertIndex = i - 1;
                while (insertIndex >= startIndex && ResultComparer.Compare(results[insertIndex], current) > 0)
                {
                    results[insertIndex + 1] = results[insertIndex];
                    insertIndex--;
                }

                results[insertIndex + 1] = current;
            }
        }

        private void AddCharacterTargets(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CombatPhysicsLayerMask layerMask)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            if (entities == null || entities.Count == 0)
                return;

            CombatColliderId colliderId =
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId);
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                CombatBodyId bodyId = physicsRuntime.GetBodyId(entity);
                if (!physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body)
                    || !physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out CombatPhysicsAabbCollider collider)
                    || !layerMask.ContainsLayer(collider.Layer))
                {
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                AddAabbTarget(body.EntityId, body.BodyId, collider.ColliderId, min, max);
            }
        }

        private void AddMovableTargets(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CombatPhysicsLayerMask layerMask)
        {
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects = world.MovableNaturalObjects;
            if (objects == null || objects.Count == 0)
                return;

            CombatColliderId colliderId =
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId);
            for (int i = 0; i < objects.Count; i++)
            {
                CharacterTestMovableNaturalObjectState movable = objects[i];
                if (movable == null)
                    continue;

                CombatBodyId bodyId = physicsRuntime.GetMovableObjectBodyId(movable.NumericId);
                if (!physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body))
                    continue;

                if (physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out CombatPhysicsAabbCollider aabb)
                    && layerMask.ContainsLayer(aabb.Layer))
                {
                    AddAabbTarget(body.EntityId, body.BodyId, aabb.ColliderId, aabb.GetWorldMin(body.Position), aabb.GetWorldMax(body.Position));
                    continue;
                }

                if (physicsRuntime.World.TryGetSphereCollider(bodyId, colliderId, out CombatPhysicsSphereCollider sphere)
                    && layerMask.ContainsLayer(sphere.Layer))
                {
                    FixVector3 center = sphere.GetWorldCenter(body.Position);
                    AddSphereTarget(body.EntityId, body.BodyId, sphere.ColliderId, center, sphere.Radius);
                    continue;
                }

                if (physicsRuntime.World.TryGetCapsuleCollider(bodyId, colliderId, out CombatPhysicsCapsuleCollider capsule)
                    && layerMask.ContainsLayer(capsule.Layer))
                {
                    AddCapsuleTarget(
                        body.EntityId,
                        body.BodyId,
                        capsule.ColliderId,
                        body.Position + capsule.LocalPointA,
                        body.Position + capsule.LocalPointB,
                        capsule.Radius);
                }
            }
        }

        private void AddAabbTarget(
            CombatEntityId entityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            FixVector3 min,
            FixVector3 max)
        {
            AddTarget(new TargetEntry(
                entityId,
                bodyId,
                colliderId,
                CombatPhysicsShapeKind.Aabb,
                min,
                max,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.Zero));
        }

        private void AddSphereTarget(
            CombatEntityId entityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            FixVector3 center,
            Fix64 radius)
        {
            FixVector3 extent = new FixVector3(radius, radius, radius);
            AddTarget(new TargetEntry(
                entityId,
                bodyId,
                colliderId,
                CombatPhysicsShapeKind.Sphere,
                center - extent,
                center + extent,
                center,
                center,
                radius));
        }

        private void AddCapsuleTarget(
            CombatEntityId entityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius)
        {
            Fix64 minX = Fix64.Min(pointA.X, pointB.X) - radius;
            Fix64 maxX = Fix64.Max(pointA.X, pointB.X) + radius;
            Fix64 minY = Fix64.Min(pointA.Y, pointB.Y) - radius;
            Fix64 maxY = Fix64.Max(pointA.Y, pointB.Y) + radius;
            Fix64 minZ = Fix64.Min(pointA.Z, pointB.Z) - radius;
            Fix64 maxZ = Fix64.Max(pointA.Z, pointB.Z) + radius;
            AddTarget(new TargetEntry(
                entityId,
                bodyId,
                colliderId,
                CombatPhysicsShapeKind.Capsule,
                new FixVector3(minX, minY, minZ),
                new FixVector3(maxX, maxY, maxZ),
                pointA,
                pointB,
                radius));
        }

        private void AddTarget(TargetEntry target)
        {
            int targetIndex = _targets.Count;
            _targets.Add(target);

            FieldCell minCell = ToCell(target.MinX, target.MinZ);
            FieldCell maxCell = ToCell(target.MaxX, target.MaxZ);
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                    GetCellList(new FieldCell(x, z)).Add(targetIndex);
            }
        }

        private List<int> GetCellList(FieldCell cell)
        {
            if (_cells.TryGetValue(cell, out List<int> indices))
                return indices;

            indices = RentCellList();
            _cells.Add(cell, indices);
            _activeCellLists.Add(indices);
            return indices;
        }

        private List<int> RentCellList()
        {
            int last = _cellListPool.Count - 1;
            if (last < 0)
                return new List<int>(4);

            List<int> indices = _cellListPool[last];
            _cellListPool.RemoveAt(last);
            return indices;
        }

        private void ClearCells()
        {
            for (int i = 0; i < _activeCellLists.Count; i++)
            {
                List<int> indices = _activeCellLists[i];
                indices.Clear();
                _cellListPool.Add(indices);
            }

            _activeCellLists.Clear();
            _cells.Clear();
        }

        private int NextQueryStamp()
        {
            if (_queryStamp != int.MaxValue)
                return ++_queryStamp;

            for (int i = 0; i < _targets.Count; i++)
            {
                TargetEntry target = _targets[i];
                target.QueryStamp = 0;
                _targets[i] = target;
            }

            _queryStamp = 1;
            return _queryStamp;
        }

        private static bool TryHitVerticalCapsule(
            TargetEntry target,
            Fix64 axisX,
            Fix64 axisZ,
            Fix64 minY,
            Fix64 maxY,
            Fix64 radius,
            out FixVector3 point)
        {
            switch (target.ShapeKind)
            {
                case CombatPhysicsShapeKind.Aabb:
                    return TryHitVerticalCapsuleAabb(
                        axisX,
                        axisZ,
                        minY,
                        maxY,
                        radius,
                        target.Min,
                        target.Max,
                        out point);
                case CombatPhysicsShapeKind.Sphere:
                    return TryHitVerticalCapsuleSphere(
                        axisX,
                        axisZ,
                        minY,
                        maxY,
                        radius,
                        target.PointA,
                        target.Radius,
                        out point);
                case CombatPhysicsShapeKind.Capsule:
                    return TryHitVerticalCapsuleCapsule(
                        axisX,
                        axisZ,
                        minY,
                        maxY,
                        radius,
                        target.PointA,
                        target.PointB,
                        target.Radius,
                        out point);
                default:
                    point = FixVector3.Zero;
                    return false;
            }
        }

        private static bool TryHitVerticalCapsuleAabb(
            Fix64 axisX,
            Fix64 axisZ,
            Fix64 minY,
            Fix64 maxY,
            Fix64 radius,
            FixVector3 min,
            FixVector3 max,
            out FixVector3 point)
        {
            Fix64 dx = DistanceToInterval(axisX, min.X, max.X);
            Fix64 dy = DistanceBetweenIntervals(minY, maxY, min.Y, max.Y);
            Fix64 dz = DistanceToInterval(axisZ, min.Z, max.Z);
            if (!IsWithinRadius(dx, dy, dz, radius))
            {
                point = FixVector3.Zero;
                return false;
            }

            point = new FixVector3(
                Fix64.Clamp(axisX, min.X, max.X),
                ClampIntervalToInterval(minY, maxY, min.Y, max.Y),
                Fix64.Clamp(axisZ, min.Z, max.Z));
            return true;
        }

        private static bool TryHitVerticalCapsuleSphere(
            Fix64 axisX,
            Fix64 axisZ,
            Fix64 minY,
            Fix64 maxY,
            Fix64 radius,
            FixVector3 center,
            Fix64 sphereRadius,
            out FixVector3 point)
        {
            Fix64 combinedRadius = radius + sphereRadius;
            Fix64 dx = axisX - center.X;
            Fix64 dz = axisZ - center.Z;
            Fix64 dy = DistanceToInterval(center.Y, minY, maxY);
            if (!IsWithinRadius(dx, dy, dz, combinedRadius))
            {
                point = FixVector3.Zero;
                return false;
            }

            point = center;
            return true;
        }

        private static bool TryHitVerticalCapsuleCapsule(
            Fix64 axisX,
            Fix64 axisZ,
            Fix64 minY,
            Fix64 maxY,
            Fix64 radius,
            FixVector3 targetPointA,
            FixVector3 targetPointB,
            Fix64 targetRadius,
            out FixVector3 point)
        {
            Fix64 combinedRadius = radius + targetRadius;
            Fix64 targetMinY = Fix64.Min(targetPointA.Y, targetPointB.Y);
            Fix64 targetMaxY = Fix64.Max(targetPointA.Y, targetPointB.Y);
            Fix64 targetAxisX = (targetPointA.X + targetPointB.X) / Fix64.FromInt(2);
            Fix64 targetAxisZ = (targetPointA.Z + targetPointB.Z) / Fix64.FromInt(2);
            Fix64 dx = axisX - targetAxisX;
            Fix64 dz = axisZ - targetAxisZ;
            Fix64 dy = DistanceBetweenIntervals(minY, maxY, targetMinY, targetMaxY);
            if (!IsWithinRadius(dx, dy, dz, combinedRadius))
            {
                point = FixVector3.Zero;
                return false;
            }

            point = new FixVector3(
                targetAxisX,
                ClampIntervalToInterval(minY, maxY, targetMinY, targetMaxY),
                targetAxisZ);
            return true;
        }

        private static bool IsWithinRadius(Fix64 dx, Fix64 dy, Fix64 dz, Fix64 radius)
        {
            Fix64 radiusSquared = radius * radius;
            return (dx * dx) + (dy * dy) + (dz * dz) <= radiusSquared;
        }

        private static Fix64 DistanceToInterval(Fix64 value, Fix64 min, Fix64 max)
        {
            if (value < min)
                return min - value;
            return value > max ? value - max : Fix64.Zero;
        }

        private static Fix64 DistanceBetweenIntervals(Fix64 firstMin, Fix64 firstMax, Fix64 secondMin, Fix64 secondMax)
        {
            if (firstMax < secondMin)
                return secondMin - firstMax;
            return secondMax < firstMin ? firstMin - secondMax : Fix64.Zero;
        }

        private static Fix64 ClampIntervalToInterval(Fix64 firstMin, Fix64 firstMax, Fix64 secondMin, Fix64 secondMax)
        {
            Fix64 overlapMin = Fix64.Max(firstMin, secondMin);
            Fix64 overlapMax = Fix64.Min(firstMax, secondMax);
            if (overlapMin <= overlapMax)
                return (overlapMin + overlapMax) / Fix64.FromInt(2);

            return firstMax < secondMin ? secondMin : secondMax;
        }

        private static FieldCell ToCell(Fix64 x, Fix64 z)
        {
            long cellRaw = CellSize.RawValue;
            return new FieldCell(
                FloorDiv(x.RawValue, cellRaw),
                FloorDiv(z.RawValue, cellRaw));
        }

        private static int FloorDiv(long value, long divisor)
        {
            long result = value / divisor;
            long remainder = value % divisor;
            if (remainder != 0 && ((remainder > 0) != (divisor > 0)))
                result--;
            return checked((int)result);
        }

        private readonly struct FieldCell : IEquatable<FieldCell>
        {
            public FieldCell(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }
            public int Z { get; }

            public bool Equals(FieldCell other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is FieldCell other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }

        private struct TargetEntry
        {
            public TargetEntry(
                CombatEntityId entityId,
                CombatBodyId bodyId,
                CombatColliderId colliderId,
                CombatPhysicsShapeKind shapeKind,
                FixVector3 min,
                FixVector3 max,
                FixVector3 pointA,
                FixVector3 pointB,
                Fix64 radius)
            {
                EntityId = entityId;
                BodyId = bodyId;
                ColliderId = colliderId;
                ShapeKind = shapeKind;
                Min = min;
                Max = max;
                PointA = pointA;
                PointB = pointB;
                Radius = radius;
                QueryStamp = 0;
            }

            public CombatEntityId EntityId { get; }
            public CombatBodyId BodyId { get; }
            public CombatColliderId ColliderId { get; }
            public CombatPhysicsShapeKind ShapeKind { get; }
            public FixVector3 Min { get; }
            public FixVector3 Max { get; }
            public Fix64 MinX => Min.X;
            public Fix64 MaxX => Max.X;
            public Fix64 MinZ => Min.Z;
            public Fix64 MaxZ => Max.Z;
            public FixVector3 PointA { get; }
            public FixVector3 PointB { get; }
            public Fix64 Radius { get; }
            public int QueryStamp { get; set; }
        }
    }
}
