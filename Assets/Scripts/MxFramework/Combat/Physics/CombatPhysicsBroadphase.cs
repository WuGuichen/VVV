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
            CombatPhysicsBody body,
            CombatPhysicsAabbCollider collider,
            FixVector3 min,
            FixVector3 max)
        {
            ColliderIndex = colliderIndex;
            Body = body;
            Collider = collider;
            Min = min;
            Max = max;
        }

        public int ColliderIndex { get; }
        public CombatPhysicsBody Body { get; }
        public CombatPhysicsAabbCollider Collider { get; }
        public FixVector3 Min { get; }
        public FixVector3 Max { get; }
    }

    internal sealed class CombatPhysicsBroadphaseResult
    {
        public CombatPhysicsBroadphaseResult(
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates,
            int rawCandidateCount,
            int cellCount,
            FixVector3 queryMin,
            FixVector3 queryMax)
        {
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            RawCandidateCount = rawCandidateCount;
            CellCount = cellCount;
            QueryMin = queryMin;
            QueryMax = queryMax;
        }

        public IReadOnlyList<CombatPhysicsBroadphaseCandidate> Candidates { get; }
        public int RawCandidateCount { get; }
        public int CellCount { get; }
        public FixVector3 QueryMin { get; }
        public FixVector3 QueryMax { get; }
    }

    internal sealed class CombatPhysicsBroadphase
    {
        private readonly CombatPhysicsBroadphaseConfig _config;

        public CombatPhysicsBroadphase(CombatPhysicsBroadphaseConfig config)
        {
            _config = config;
        }

        public CombatPhysicsBroadphaseResult CollectCandidates(
            CombatPhysicsShape shape,
            IReadOnlyList<CombatPhysicsBody> bodies,
            IReadOnlyList<CombatPhysicsAabbCollider> colliders)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            GetQueryAabb(shape, out FixVector3 queryMin, out FixVector3 queryMax);
            var cells = new Dictionary<CombatPhysicsBroadphaseCell, List<int>>();
            for (int i = 0; i < colliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = colliders[i];
                if (!TryGetBody(bodies, collider.BodyId, out CombatPhysicsBody body))
                {
                    continue;
                }

                FixVector3 min = collider.GetWorldMin(body.Position);
                FixVector3 max = collider.GetWorldMax(body.Position);
                AddColliderToCells(cells, i, min, max);
            }

            CombatPhysicsBroadphaseCell queryCellMin = ToCell(queryMin);
            CombatPhysicsBroadphaseCell queryCellMax = ToCell(queryMax);
            var unique = new HashSet<int>();
            var candidates = new List<CombatPhysicsBroadphaseCandidate>();
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
                        if (!cells.TryGetValue(cell, out List<int> colliderIndices))
                        {
                            continue;
                        }

                        for (int i = 0; i < colliderIndices.Count; i++)
                        {
                            rawCandidateCount++;
                            int colliderIndex = colliderIndices[i];
                            if (!unique.Add(colliderIndex))
                            {
                                continue;
                            }

                            CombatPhysicsAabbCollider collider = colliders[colliderIndex];
                            if (!TryGetBody(bodies, collider.BodyId, out CombatPhysicsBody body))
                            {
                                continue;
                            }

                            FixVector3 min = collider.GetWorldMin(body.Position);
                            FixVector3 max = collider.GetWorldMax(body.Position);
                            if (!Overlaps(queryMin, queryMax, min, max))
                            {
                                continue;
                            }

                            candidates.Add(new CombatPhysicsBroadphaseCandidate(
                                colliderIndex,
                                body,
                                collider,
                                min,
                                max));
                        }
                    }
                }
            }

            candidates.Sort(CompareCandidates);
            return new CombatPhysicsBroadphaseResult(candidates, rawCandidateCount, cellCount, queryMin, queryMax);
        }

        private void AddColliderToCells(
            Dictionary<CombatPhysicsBroadphaseCell, List<int>> cells,
            int colliderIndex,
            FixVector3 min,
            FixVector3 max)
        {
            CombatPhysicsBroadphaseCell minCell = ToCell(min);
            CombatPhysicsBroadphaseCell maxCell = ToCell(max);
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        var cell = new CombatPhysicsBroadphaseCell(x, y, z);
                        if (!cells.TryGetValue(cell, out List<int> indices))
                        {
                            indices = new List<int>();
                            cells.Add(cell, indices);
                        }

                        indices.Add(colliderIndex);
                    }
                }
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

        private static bool TryGetBody(
            IReadOnlyList<CombatPhysicsBody> bodies,
            CombatBodyId bodyId,
            out CombatPhysicsBody body)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].BodyId.Equals(bodyId))
                {
                    body = bodies[i];
                    return true;
                }
            }

            body = default;
            return false;
        }

        private static void GetQueryAabb(
            CombatPhysicsShape shape,
            out FixVector3 min,
            out FixVector3 max)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                    if (!shape.Direction.TryNormalize(out FixVector3 rayDirection))
                    {
                        throw new ArgumentException("Ray direction cannot be zero.", nameof(shape));
                    }

                    FixVector3 end = shape.Origin + rayDirection * shape.Length;
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
                    throw new NotSupportedException("OBB broadphase query AABB is not supported.");
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

            compare = left.Collider.ColliderId.CompareTo(right.Collider.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Collider.Layer.CompareTo(right.Collider.Layer);
            if (compare != 0)
            {
                return compare;
            }

            return left.ColliderIndex.CompareTo(right.ColliderIndex);
        }
    }
}
