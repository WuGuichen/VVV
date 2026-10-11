using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    /// <summary>One primitive leaf in a character-owned compound hurtbox. Shape coordinates are body-local.</summary>
    public readonly struct CombatPhysicsCompoundLeaf : IEquatable<CombatPhysicsCompoundLeaf>
    {
        public CombatPhysicsCompoundLeaf(CombatColliderId colliderId, int layer, CombatPhysicsShape localShape, CombatSurfaceMaterial surfaceMaterial = default, int priority = 0)
        {
            if (colliderId.IsNone) throw new ArgumentException("Compound collider id cannot be none.", nameof(colliderId));
            if (localShape.Kind != CombatPhysicsShapeKind.Sphere && localShape.Kind != CombatPhysicsShapeKind.Capsule && localShape.Kind != CombatPhysicsShapeKind.Obb)
                throw new ArgumentException("Compound leaves only support Sphere, Capsule, and OBB.", nameof(localShape));
            CombatPhysicsLayerMask.FromLayer(layer);
            ColliderId = colliderId;
            Layer = layer;
            LocalShape = localShape;
            SurfaceMaterial = surfaceMaterial;
            Priority = priority;
        }

        public CombatColliderId ColliderId { get; }
        public int Layer { get; }
        public CombatPhysicsShape LocalShape { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int Priority { get; }
        public bool Equals(CombatPhysicsCompoundLeaf other) => ColliderId.Equals(other.ColliderId) && Layer == other.Layer && LocalShape.Equals(other.LocalShape) && SurfaceMaterial.Equals(other.SurfaceMaterial) && Priority == other.Priority;
        public override bool Equals(object obj) => obj is CombatPhysicsCompoundLeaf other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ColliderId, Layer, LocalShape, SurfaceMaterial, Priority);
    }

    public sealed partial class CombatPhysicsWorld
    {
        private readonly List<CombatPhysicsCompound> _compounds = new List<CombatPhysicsCompound>();
        private readonly Dictionary<CombatBodyId, int> _compoundIndices = new Dictionary<CombatBodyId, int>();
        private CombatPhysicsCompound _activeCompoundUpdate;

        public int CompoundCount => _compounds.Count;
        public int CompoundLeafCount
        {
            get { int count = 0; for (int i = 0; i < _compounds.Count; i++) count += _compounds[i].LeafCount; return count; }
        }

        /// <summary>Begins a no-allocation pose update after the compound has been warmed once.</summary>
        public void BeginCompoundColliderUpdate(CombatBodyId bodyId, int expectedLeafCount)
        {
            if (_activeCompoundUpdate != null) throw new InvalidOperationException("A compound collider update is already active.");
            if (FindBodyIndex(bodyId) < 0) throw new InvalidOperationException("Cannot register a compound before its body is registered.");
            if (expectedLeafCount <= 0 || expectedLeafCount > 32) throw new ArgumentOutOfRangeException(nameof(expectedLeafCount));
            if (!_compoundIndices.TryGetValue(bodyId, out int index))
            {
                var created = new CombatPhysicsCompound(bodyId, expectedLeafCount);
                _compoundIndices.Add(bodyId, _compounds.Count);
                _compounds.Add(created);
                _activeCompoundUpdate = created;
            }
            else
            {
                _activeCompoundUpdate = _compounds[index];
            }
            _activeCompoundUpdate.BeginUpdate(expectedLeafCount);
        }

        public void UpsertCompoundCollider(CombatPhysicsCompoundLeaf leaf)
        {
            if (_activeCompoundUpdate == null) throw new InvalidOperationException("No compound collider update is active.");
            _activeCompoundUpdate.Append(leaf);
        }

        public void EndCompoundColliderUpdate()
        {
            if (_activeCompoundUpdate == null) throw new InvalidOperationException("No compound collider update is active.");
            bool topologyChanged = _activeCompoundUpdate.EndUpdate();
            if (TryGetBody(_activeCompoundUpdate.BodyId, out CombatPhysicsBody body))
                _activeCompoundUpdate.SetBody(body);
            _activeCompoundUpdate = null;
            if (topologyChanged) IncrementColliderTopologyRevision();
            else { unchecked { _revision++; } }
        }

        public void CancelCompoundColliderUpdate()
        {
            if (_activeCompoundUpdate == null) return;
            _activeCompoundUpdate = null;
        }

        public bool RemoveCompound(CombatBodyId bodyId)
        {
            if (!_compoundIndices.TryGetValue(bodyId, out int index)) return false;
            _compounds.RemoveAt(index);
            RebuildCompoundIndices();
            IncrementColliderTopologyRevision();
            return true;
        }

        private void ClearCompounds()
        {
            _compounds.Clear();
            _compoundIndices.Clear();
            _activeCompoundUpdate = null;
        }

        private void RebuildCompoundIndices()
        {
            _compoundIndices.Clear();
            for (int i = 0; i < _compounds.Count; i++) _compoundIndices[_compounds[i].BodyId] = i;
        }

        private bool RemoveCompoundLeaf(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            if (!_compoundIndices.TryGetValue(bodyId, out int index)) return false;
            return _compounds[index].Remove(colliderId);
        }

        private int CopyCompoundSphereCollidersTo(List<CombatPhysicsSphereCollider> colliders)
        {
            int startCount = colliders.Count;
            for (int compoundIndex = 0; compoundIndex < _compounds.Count; compoundIndex++)
            {
                CombatPhysicsCompound compound = _compounds[compoundIndex];
                for (int leafIndex = 0; leafIndex < compound.LeafCount; leafIndex++)
                {
                    CombatPhysicsCompoundLeaf leaf = compound.Leaves[leafIndex];
                    if (leaf.LocalShape.Kind != CombatPhysicsShapeKind.Sphere) continue;
                    colliders.Add(new CombatPhysicsSphereCollider(compound.BodyId, leaf.ColliderId, leaf.Layer,
                        leaf.LocalShape.Center, leaf.LocalShape.Radius, leaf.SurfaceMaterial, leaf.Priority));
                }
            }
            return colliders.Count - startCount;
        }

        private int CopyCompoundCapsuleCollidersTo(List<CombatPhysicsCapsuleCollider> colliders)
        {
            int startCount = colliders.Count;
            for (int compoundIndex = 0; compoundIndex < _compounds.Count; compoundIndex++)
            {
                CombatPhysicsCompound compound = _compounds[compoundIndex];
                for (int leafIndex = 0; leafIndex < compound.LeafCount; leafIndex++)
                {
                    CombatPhysicsCompoundLeaf leaf = compound.Leaves[leafIndex];
                    if (leaf.LocalShape.Kind != CombatPhysicsShapeKind.Capsule) continue;
                    colliders.Add(new CombatPhysicsCapsuleCollider(compound.BodyId, leaf.ColliderId, leaf.Layer,
                        leaf.LocalShape.PointA, leaf.LocalShape.PointB, leaf.LocalShape.Radius, leaf.SurfaceMaterial, leaf.Priority));
                }
            }
            return colliders.Count - startCount;
        }

        private int CopyCompoundObbCollidersTo(List<CombatPhysicsObbCollider> colliders)
        {
            int startCount = colliders.Count;
            for (int compoundIndex = 0; compoundIndex < _compounds.Count; compoundIndex++)
            {
                CombatPhysicsCompound compound = _compounds[compoundIndex];
                for (int leafIndex = 0; leafIndex < compound.LeafCount; leafIndex++)
                {
                    CombatPhysicsCompoundLeaf leaf = compound.Leaves[leafIndex];
                    if (leaf.LocalShape.Kind != CombatPhysicsShapeKind.Obb) continue;
                    colliders.Add(new CombatPhysicsObbCollider(compound.BodyId, leaf.ColliderId, leaf.Layer,
                        leaf.LocalShape.Center, leaf.LocalShape.HalfExtents, leaf.LocalShape.AxisX,
                        leaf.LocalShape.AxisY, leaf.LocalShape.AxisZ, leaf.SurfaceMaterial, leaf.Priority));
                }
            }
            return colliders.Count - startCount;
        }

        private int QueryCompounds(in CombatPhysicsQuery query, List<CombatQueryResult> results)
        {
            GetCompoundShapeAabb(query.Shape, out FixVector3 queryMin, out FixVector3 queryMax);
            int startCount = results.Count;
            for (int compoundIndex = 0; compoundIndex < _compounds.Count; compoundIndex++)
            {
                CombatPhysicsCompound compound = _compounds[compoundIndex];
                if (!query.Filter.IncludeSourceEntity && query.Header.SourceEntityId.Equals(compound.EntityId)) continue;
                if (!AabbOverlaps(queryMin, queryMax, compound.WorldMin, compound.WorldMax)) continue;
                QueryCompound(
                    compound,
                    query,
                    TranslateQueryShape(query.Shape, -compound.Position),
                    queryMin - compound.Position,
                    queryMax - compound.Position,
                    results);
            }
            return results.Count - startCount;
        }

        private static void QueryCompound(
            CombatPhysicsCompound compound,
            in CombatPhysicsQuery query,
            CombatPhysicsShape localQueryShape,
            FixVector3 localQueryMin,
            FixVector3 localQueryMax,
            List<CombatQueryResult> results)
        {
            if (compound.NodeCount == 0) return;
            int stackCount = 1;
            compound.TraversalStack[0] = compound.RootNode;
            while (stackCount > 0)
            {
                CombatPhysicsCompoundNode node = compound.Nodes[compound.TraversalStack[--stackCount]];
                if (!AabbOverlaps(localQueryMin, localQueryMax, node.Min, node.Max)) continue;
                if (node.LeafIndex >= 0)
                {
                    CombatPhysicsCompoundLeaf leaf = compound.Leaves[node.LeafIndex];
                    if (!query.Header.LayerMask.ContainsLayer(leaf.Layer)) continue;
                    if (!TryShapeHitTarget(localQueryShape, leaf.LocalShape, out Fix64 distance, out FixVector3 point, out FixVector3 normal)) continue;
                    results.Add(new CombatQueryResult(query.Header, compound.EntityId, compound.BodyId, leaf.ColliderId, leaf.LocalShape.Kind, distance, point + compound.Position, normal, leaf.Priority));
                    continue;
                }
                compound.TraversalStack[stackCount++] = node.Right;
                compound.TraversalStack[stackCount++] = node.Left;
            }
        }

        private static CombatPhysicsShape TranslateQueryShape(CombatPhysicsShape shape, FixVector3 translation)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray: return CombatPhysicsShape.Ray(shape.Origin + translation, shape.Direction, shape.Length);
                case CombatPhysicsShapeKind.Sphere: return CombatPhysicsShape.Sphere(shape.Center + translation, shape.Radius);
                case CombatPhysicsShapeKind.Capsule: return CombatPhysicsShape.Capsule(shape.PointA + translation, shape.PointB + translation, shape.Radius);
                case CombatPhysicsShapeKind.Aabb: return CombatPhysicsShape.Aabb(shape.Min + translation, shape.Max + translation);
                case CombatPhysicsShapeKind.Sector: return CombatPhysicsShape.Sector(shape.Origin + translation, shape.Direction, shape.Radius, shape.MinDot);
                case CombatPhysicsShapeKind.Obb: return CombatPhysicsShape.Obb(shape.Center + translation, shape.HalfExtents, shape.AxisX, shape.AxisY, shape.AxisZ);
                default: throw new NotSupportedException("Compound query shape is unsupported.");
            }
        }

        private static void GetCompoundShapeAabb(CombatPhysicsShape shape, out FixVector3 min, out FixVector3 max)
        {
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                    FixVector3 end = shape.Origin + shape.Direction * shape.Length; min = VectorMin(shape.Origin, end); max = VectorMax(shape.Origin, end); return;
                case CombatPhysicsShapeKind.Sphere:
                    var sphere = new FixVector3(shape.Radius, shape.Radius, shape.Radius); min = shape.Center - sphere; max = shape.Center + sphere; return;
                case CombatPhysicsShapeKind.Capsule:
                    var capsule = new FixVector3(shape.Radius, shape.Radius, shape.Radius); min = VectorMin(shape.PointA, shape.PointB) - capsule; max = VectorMax(shape.PointA, shape.PointB) + capsule; return;
                case CombatPhysicsShapeKind.Aabb: min = shape.Min; max = shape.Max; return;
                case CombatPhysicsShapeKind.Sector:
                    var sector = new FixVector3(shape.Radius, shape.Radius, shape.Radius); min = shape.Origin - sector; max = shape.Origin + sector; return;
                case CombatPhysicsShapeKind.Obb: CombatPhysicsObbMath.GetAabb(shape.Center, shape.HalfExtents, shape.AxisX, shape.AxisY, shape.AxisZ, out min, out max); return;
                default: throw new NotSupportedException("Compound query shape is unsupported.");
            }
        }

        private static void GetLocalShapeAabb(CombatPhysicsShape shape, out FixVector3 min, out FixVector3 max)
        {
            GetCompoundShapeAabb(shape, out min, out max);
        }

        private static FixVector3 VectorMin(FixVector3 a, FixVector3 b) => new FixVector3(Fix64.Min(a.X, b.X), Fix64.Min(a.Y, b.Y), Fix64.Min(a.Z, b.Z));
        private static FixVector3 VectorMax(FixVector3 a, FixVector3 b) => new FixVector3(Fix64.Max(a.X, b.X), Fix64.Max(a.Y, b.Y), Fix64.Max(a.Z, b.Z));
        private static bool AabbOverlaps(FixVector3 aMin, FixVector3 aMax, FixVector3 bMin, FixVector3 bMax) => aMin.X <= bMax.X && aMax.X >= bMin.X && aMin.Y <= bMax.Y && aMax.Y >= bMin.Y && aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;

        private sealed class CombatPhysicsCompound
        {
            private int _writeIndex;
            private bool _topologyChanged;
            public CombatPhysicsCompound(CombatBodyId bodyId, int capacity) { BodyId = bodyId; Leaves = new CombatPhysicsCompoundLeaf[capacity]; Nodes = new CombatPhysicsCompoundNode[capacity * 2 - 1]; TraversalStack = new int[capacity * 2]; LeafOrder = new int[capacity]; }
            public CombatBodyId BodyId { get; }
            public CombatPhysicsCompoundLeaf[] Leaves { get; private set; }
            public CombatPhysicsCompoundNode[] Nodes { get; private set; }
            public int[] TraversalStack { get; private set; }
            private int[] LeafOrder { get; set; }
            public int LeafCount { get; private set; }
            public int NodeCount { get; private set; }
            public int RootNode { get; private set; }
            public CombatEntityId EntityId { get; private set; }
            public FixVector3 Position { get; private set; }
            public FixVector3 WorldMin { get; private set; }
            public FixVector3 WorldMax { get; private set; }
            public FixVector3 Min => NodeCount == 0 ? FixVector3.Zero : Nodes[RootNode].Min;
            public FixVector3 Max => NodeCount == 0 ? FixVector3.Zero : Nodes[RootNode].Max;

            public void SetBody(CombatPhysicsBody body)
            {
                EntityId = body.EntityId;
                Position = body.Position;
                WorldMin = Min + Position;
                WorldMax = Max + Position;
            }

            public void BeginUpdate(int expected)
            {
                if (Leaves.Length < expected)
                {
                    Leaves = new CombatPhysicsCompoundLeaf[expected]; Nodes = new CombatPhysicsCompoundNode[expected * 2 - 1]; TraversalStack = new int[expected * 2]; LeafOrder = new int[expected]; _topologyChanged = true;
                }
                _writeIndex = 0;
            }
            public void Append(CombatPhysicsCompoundLeaf leaf)
            {
                if (_writeIndex >= Leaves.Length) throw new InvalidOperationException("Compound update exceeds its declared capacity.");
                if (_writeIndex >= LeafCount || !Leaves[_writeIndex].ColliderId.Equals(leaf.ColliderId)) _topologyChanged = true;
                Leaves[_writeIndex++] = leaf;
            }
            public bool EndUpdate()
            {
                if (LeafCount != _writeIndex) _topologyChanged = true;
                LeafCount = _writeIndex;
                if (_topologyChanged) BuildTopology(); else Refit();
                bool changed = _topologyChanged; _topologyChanged = false; return changed;
            }
            public bool Remove(CombatColliderId id)
            {
                for (int i = 0; i < LeafCount; i++) if (Leaves[i].ColliderId.Equals(id)) { for (int j = i + 1; j < LeafCount; j++) Leaves[j - 1] = Leaves[j]; LeafCount--; BuildTopology(); return true; }
                return false;
            }
            private void BuildTopology()
            {
                NodeCount = 0;
                for (int i = 0; i < LeafCount; i++) LeafOrder[i] = i;
                RootNode = LeafCount == 0 ? -1 : BuildRange(0, LeafCount);
                Refit();
                WorldMin = Min + Position;
                WorldMax = Max + Position;
            }
            private int BuildRange(int start, int count)
            {
                int nodeIndex = NodeCount++;
                if (count == 1) { Nodes[nodeIndex] = new CombatPhysicsCompoundNode(LeafOrder[start], -1, -1, FixVector3.Zero, FixVector3.Zero); return nodeIndex; }
                int leftCount = SelectSahSplit(start, count, out int axis);
                SortLeafRange(start, count, axis);
                int left = BuildRange(start, leftCount);
                int right = BuildRange(start + leftCount, count - leftCount);
                Nodes[nodeIndex] = new CombatPhysicsCompoundNode(-1, left, right, FixVector3.Zero, FixVector3.Zero); return nodeIndex;
            }
            private int SelectSahSplit(int start, int count, out int bestAxis)
            {
                bestAxis = 0;
                int bestSplit = count / 2;
                Fix64 bestCost = Fix64.MaxValue;
                for (int axis = 0; axis < 3; axis++)
                {
                    SortLeafRange(start, count, axis);
                    for (int split = 1; split < count; split++)
                    {
                        ComputeLeafRangeBounds(start, split, out FixVector3 leftMin, out FixVector3 leftMax);
                        ComputeLeafRangeBounds(start + split, count - split, out FixVector3 rightMin, out FixVector3 rightMax);
                        Fix64 cost = SaturatingAddNonNegative(
                            SaturatingMultiplyNonNegative(SurfaceArea(leftMin, leftMax), Fix64.FromInt(split)),
                            SaturatingMultiplyNonNegative(SurfaceArea(rightMin, rightMax), Fix64.FromInt(count - split)));
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestAxis = axis;
                            bestSplit = split;
                        }
                    }
                }
                return bestSplit;
            }
            private void SortLeafRange(int start, int count, int axis)
            {
                for (int i = start + 1; i < start + count; i++)
                {
                    int value = LeafOrder[i];
                    int j = i - 1;
                    while (j >= start && CompareLeaf(value, LeafOrder[j], axis) < 0)
                    {
                        LeafOrder[j + 1] = LeafOrder[j];
                        j--;
                    }
                    LeafOrder[j + 1] = value;
                }
            }
            private int CompareLeaf(int left, int right, int axis)
            {
                FixVector3 a = LeafCenter(left), b = LeafCenter(right);
                Fix64 av = axis == 0 ? a.X : axis == 1 ? a.Y : a.Z;
                Fix64 bv = axis == 0 ? b.X : axis == 1 ? b.Y : b.Z;
                int order = av.CompareTo(bv);
                return order != 0 ? order : Leaves[left].ColliderId.Value.CompareTo(Leaves[right].ColliderId.Value);
            }
            private FixVector3 LeafCenter(int leafIndex)
            {
                GetLocalShapeAabb(Leaves[leafIndex].LocalShape, out FixVector3 min, out FixVector3 max);
                return (min + max) * Fix64.Half;
            }
            private void ComputeLeafRangeBounds(int start, int count, out FixVector3 min, out FixVector3 max)
            {
                GetLocalShapeAabb(Leaves[LeafOrder[start]].LocalShape, out min, out max);
                for (int i = 1; i < count; i++)
                {
                    GetLocalShapeAabb(Leaves[LeafOrder[start + i]].LocalShape, out FixVector3 leafMin, out FixVector3 leafMax);
                    min = VectorMin(min, leafMin); max = VectorMax(max, leafMax);
                }
            }
            private static Fix64 SurfaceArea(FixVector3 min, FixVector3 max)
            {
                FixVector3 size = max - min;
                Fix64 pairs = SaturatingAddNonNegative(
                    SaturatingAddNonNegative(
                        SaturatingMultiplyNonNegative(size.X, size.Y),
                        SaturatingMultiplyNonNegative(size.Y, size.Z)),
                    SaturatingMultiplyNonNegative(size.Z, size.X));
                return SaturatingMultiplyNonNegative(pairs, Fix64.FromInt(2));
            }

            private static Fix64 SaturatingAddNonNegative(Fix64 left, Fix64 right)
            {
                long leftRaw = left.RawValue;
                long rightRaw = right.RawValue;
                if (leftRaw <= 0) return rightRaw <= 0 ? Fix64.Zero : right;
                if (rightRaw <= 0) return left;
                return leftRaw > long.MaxValue - rightRaw
                    ? Fix64.MaxValue
                    : Fix64.FromRaw(leftRaw + rightRaw);
            }

            private static Fix64 SaturatingMultiplyNonNegative(Fix64 left, Fix64 right)
            {
                long leftRaw = left.RawValue;
                long rightRaw = right.RawValue;
                if (leftRaw <= 0 || rightRaw <= 0) return Fix64.Zero;

                long leftWhole = leftRaw / Fix64.Scale;
                long leftFraction = leftRaw % Fix64.Scale;
                long rightWhole = rightRaw / Fix64.Scale;
                long rightFraction = rightRaw % Fix64.Scale;
                long result = 0;
                if (!TryAddNonNegativeProduct(ref result, leftWhole, rightRaw)
                    || !TryAddNonNegativeProduct(ref result, leftFraction, rightWhole))
                {
                    return Fix64.MaxValue;
                }

                long fractionalProduct = leftFraction * rightFraction / Fix64.Scale;
                return result > long.MaxValue - fractionalProduct
                    ? Fix64.MaxValue
                    : Fix64.FromRaw(result + fractionalProduct);
            }

            private static bool TryAddNonNegativeProduct(ref long total, long left, long right)
            {
                if (left <= 0 || right <= 0) return true;
                if (left > (long.MaxValue - total) / right) return false;
                total += left * right;
                return true;
            }
            private void Refit()
            {
                for (int i = NodeCount - 1; i >= 0; i--)
                {
                    CombatPhysicsCompoundNode node = Nodes[i];
                    if (node.LeafIndex >= 0) GetLocalShapeAabb(Leaves[node.LeafIndex].LocalShape, out node.Min, out node.Max);
                    else { node.Min = VectorMin(Nodes[node.Left].Min, Nodes[node.Right].Min); node.Max = VectorMax(Nodes[node.Left].Max, Nodes[node.Right].Max); }
                    Nodes[i] = node;
                }
            }
        }

        private struct CombatPhysicsCompoundNode
        {
            public CombatPhysicsCompoundNode(int leafIndex, int left, int right, FixVector3 min, FixVector3 max) { LeafIndex = leafIndex; Left = left; Right = right; Min = min; Max = max; }
            public int LeafIndex; public int Left; public int Right; public FixVector3 Min; public FixVector3 Max;
        }
    }
}
