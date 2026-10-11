using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
        private int FindBodyIndex(CombatBodyId bodyId)
        {
            return _bodyIndices.TryGetValue(bodyId, out int index) ? index : -1;
        }

        private int FindAabbColliderIndex(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            return _aabbColliderIndices.TryGetValue(CombatPhysicsColliderKey.From(bodyId, colliderId), out int index)
                ? index
                : -1;
        }

        private int FindSphereColliderIndex(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            return _sphereColliderIndices.TryGetValue(CombatPhysicsColliderKey.From(bodyId, colliderId), out int index)
                ? index
                : -1;
        }

        private int FindCapsuleColliderIndex(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            return _capsuleColliderIndices.TryGetValue(CombatPhysicsColliderKey.From(bodyId, colliderId), out int index)
                ? index
                : -1;
        }

        private int FindObbColliderIndex(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            return _obbColliderIndices.TryGetValue(CombatPhysicsColliderKey.From(bodyId, colliderId), out int index) ? index : -1;
        }

        private bool RemoveNonAabbColliderVersion(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            bool removed = false;
            int index = FindSphereColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _sphereColliders.RemoveAt(index);
                RebuildSphereColliderIndices();
                removed = true;
            }

            index = FindCapsuleColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _capsuleColliders.RemoveAt(index);
                RebuildCapsuleColliderIndices();
                removed = true;
            }
            index = FindObbColliderIndex(bodyId, colliderId);
            if (index >= 0) { _obbColliders.RemoveAt(index); RebuildObbColliderIndices(); removed = true; }

            return removed;
        }

        private bool RemoveNonSphereColliderVersion(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            bool removed = false;
            int index = FindAabbColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _aabbColliders.RemoveAt(index);
                RebuildAabbColliderIndices();
                removed = true;
            }

            index = FindCapsuleColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _capsuleColliders.RemoveAt(index);
                RebuildCapsuleColliderIndices();
                removed = true;
            }
            index = FindObbColliderIndex(bodyId, colliderId);
            if (index >= 0) { _obbColliders.RemoveAt(index); RebuildObbColliderIndices(); removed = true; }

            return removed;
        }

        private bool RemoveNonCapsuleColliderVersion(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            bool removed = false;
            int index = FindAabbColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _aabbColliders.RemoveAt(index);
                RebuildAabbColliderIndices();
                removed = true;
            }

            index = FindSphereColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _sphereColliders.RemoveAt(index);
                RebuildSphereColliderIndices();
                removed = true;
            }
            index = FindObbColliderIndex(bodyId, colliderId);
            if (index >= 0) { _obbColliders.RemoveAt(index); RebuildObbColliderIndices(); removed = true; }

            return removed;
        }

        private bool RemoveNonObbColliderVersion(CombatBodyId bodyId, CombatColliderId colliderId)
        {
            bool removed = false;
            int index = FindAabbColliderIndex(bodyId, colliderId);
            if (index >= 0) { _aabbColliders.RemoveAt(index); RebuildAabbColliderIndices(); removed = true; }
            index = FindSphereColliderIndex(bodyId, colliderId);
            if (index >= 0) { _sphereColliders.RemoveAt(index); RebuildSphereColliderIndices(); removed = true; }
            index = FindCapsuleColliderIndex(bodyId, colliderId);
            if (index >= 0) { _capsuleColliders.RemoveAt(index); RebuildCapsuleColliderIndices(); removed = true; }
            return removed;
        }

        private readonly struct CombatPhysicsColliderKey : IEquatable<CombatPhysicsColliderKey>
        {
            private CombatPhysicsColliderKey(CombatBodyId bodyId, CombatColliderId colliderId)
            {
                BodyId = bodyId;
                ColliderId = colliderId;
            }

            public CombatBodyId BodyId { get; }
            public CombatColliderId ColliderId { get; }

            public static CombatPhysicsColliderKey From(CombatBodyId bodyId, CombatColliderId colliderId)
            {
                return new CombatPhysicsColliderKey(bodyId, colliderId);
            }

            public bool Equals(CombatPhysicsColliderKey other)
            {
                return BodyId.Equals(other.BodyId) && ColliderId.Equals(other.ColliderId);
            }

            public override bool Equals(object obj)
            {
                return obj is CombatPhysicsColliderKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (BodyId.Value * 397) ^ ColliderId.Value;
                }
            }
        }

        private void RebuildBodyIndices()
        {
            _bodyIndices.Clear();
            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodyIndices[_bodies[i].BodyId] = i;
            }
        }

        private void RebuildAabbColliderIndices()
        {
            _aabbColliderIndices.Clear();
            for (int i = 0; i < _aabbColliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = _aabbColliders[i];
                _aabbColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] = i;
            }
        }

        private void RebuildSphereColliderIndices()
        {
            _sphereColliderIndices.Clear();
            for (int i = 0; i < _sphereColliders.Count; i++)
            {
                CombatPhysicsSphereCollider collider = _sphereColliders[i];
                _sphereColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] = i;
            }
        }

        private void RebuildCapsuleColliderIndices()
        {
            _capsuleColliderIndices.Clear();
            for (int i = 0; i < _capsuleColliders.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = _capsuleColliders[i];
                _capsuleColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] = i;
            }
        }

        private void RebuildObbColliderIndices()
        {
            _obbColliderIndices.Clear();
            for (int i = 0; i < _obbColliders.Count; i++)
            {
                CombatPhysicsObbCollider collider = _obbColliders[i];
                _obbColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] = i;
            }
        }

        private void IncrementColliderTopologyRevision()
        {
            if (_colliderBatchDepth > 0) { _colliderBatchDirty = true; return; }
            IncrementTopologyRevision();
        }

        private void IncrementTopologyRevision()
        {
            ClearMovedBodies();
            unchecked
            {
                _revision++;
                _topologyRevision++;
            }
        }

        private void IncrementBodyRevision()
        {
            unchecked
            {
                _revision++;
                _bodyRevision++;
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

        private sealed class CombatPhysicsSphereColliderComparer : IComparer<CombatPhysicsSphereCollider>
        {
            public static readonly CombatPhysicsSphereColliderComparer Instance = new CombatPhysicsSphereColliderComparer();

            private CombatPhysicsSphereColliderComparer()
            {
            }

            public int Compare(CombatPhysicsSphereCollider left, CombatPhysicsSphereCollider right)
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

        private sealed class CombatPhysicsCapsuleColliderComparer : IComparer<CombatPhysicsCapsuleCollider>
        {
            public static readonly CombatPhysicsCapsuleColliderComparer Instance = new CombatPhysicsCapsuleColliderComparer();

            private CombatPhysicsCapsuleColliderComparer()
            {
            }

            public int Compare(CombatPhysicsCapsuleCollider left, CombatPhysicsCapsuleCollider right)
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

        private sealed class CombatPhysicsObbColliderComparer : IComparer<CombatPhysicsObbCollider>
        {
            public static readonly CombatPhysicsObbColliderComparer Instance = new CombatPhysicsObbColliderComparer();
            private CombatPhysicsObbColliderComparer() { }
            public int Compare(CombatPhysicsObbCollider left, CombatPhysicsObbCollider right)
            {
                int compare = left.BodyId.CompareTo(right.BodyId);
                if (compare != 0) return compare;
                compare = left.ColliderId.CompareTo(right.ColliderId);
                return compare != 0 ? compare : left.Layer.CompareTo(right.Layer);
            }
        }

    }
}
