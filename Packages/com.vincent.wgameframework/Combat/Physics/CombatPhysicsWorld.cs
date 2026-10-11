using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
        private static readonly FixVector3 AxisX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 AxisY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 AxisZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        private readonly List<CombatPhysicsBody> _bodies = new List<CombatPhysicsBody>();
        private readonly List<CombatPhysicsAabbCollider> _aabbColliders = new List<CombatPhysicsAabbCollider>();
        private readonly List<CombatPhysicsSphereCollider> _sphereColliders = new List<CombatPhysicsSphereCollider>();
        private readonly List<CombatPhysicsCapsuleCollider> _capsuleColliders = new List<CombatPhysicsCapsuleCollider>();
        private readonly List<CombatPhysicsObbCollider> _obbColliders = new List<CombatPhysicsObbCollider>();
        private readonly Dictionary<CombatBodyId, int> _bodyIndices = new Dictionary<CombatBodyId, int>();
        private readonly Dictionary<CombatPhysicsColliderKey, int> _aabbColliderIndices =
            new Dictionary<CombatPhysicsColliderKey, int>();
        private readonly Dictionary<CombatPhysicsColliderKey, int> _sphereColliderIndices =
            new Dictionary<CombatPhysicsColliderKey, int>();
        private readonly Dictionary<CombatPhysicsColliderKey, int> _capsuleColliderIndices =
            new Dictionary<CombatPhysicsColliderKey, int>();
        private readonly Dictionary<CombatPhysicsColliderKey, int> _obbColliderIndices =
            new Dictionary<CombatPhysicsColliderKey, int>();
        private readonly List<CombatQueryResult> _queryBatchHitsScratch = new List<CombatQueryResult>();
        private readonly List<CombatPhysicsBody> _movedBodies = new List<CombatPhysicsBody>();
        private readonly Dictionary<CombatBodyId, int> _movedBodyIndices = new Dictionary<CombatBodyId, int>();
        private readonly CombatPhysicsBroadphaseConfig _broadphaseConfig;
        private readonly CombatPhysicsBroadphase _broadphase;
        private int _revision;
        private int _bodyRevision;
        private int _topologyRevision;
        private int _colliderBatchDepth;
        private bool _colliderBatchDirty;

        public CombatPhysicsWorld()
            : this(CombatPhysicsBroadphaseConfig.Default)
        {
        }

        public CombatPhysicsWorld(CombatPhysicsBroadphaseConfig broadphaseConfig)
        {
            _broadphaseConfig = broadphaseConfig;
            _broadphase = new CombatPhysicsBroadphase(broadphaseConfig);
        }

        public int BodyCount => _bodies.Count;

        public int ColliderCount => _aabbColliders.Count + _sphereColliders.Count + _capsuleColliders.Count + _obbColliders.Count + CompoundLeafCount;

        public int TotalColliderCount => ColliderCount;

        public int Revision => _revision;

        internal int BodyRevision => _bodyRevision;

        /// <summary>
        /// Gets the broadphase configuration used by this world.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public CombatPhysicsBroadphaseConfig BroadphaseConfig => _broadphaseConfig;

        public int TopologyRevision => _topologyRevision;

        public void Clear()
        {
            if (_bodies.Count == 0 && ColliderCount == 0)
            {
                return;
            }

            _bodies.Clear();
            _aabbColliders.Clear();
            _sphereColliders.Clear();
            _capsuleColliders.Clear();
            _obbColliders.Clear();
            _bodyIndices.Clear();
            _aabbColliderIndices.Clear();
            _sphereColliderIndices.Clear();
            _capsuleColliderIndices.Clear();
            _obbColliderIndices.Clear();
            ClearCompounds();
            ClearMovedBodies();
            IncrementTopologyRevision();
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

                CombatPhysicsBody existing = _bodies[index];
                _bodies[index] = body;
                if (!existing.EntityId.Equals(body.EntityId))
                {
                    ClearMovedBodies();
                    IncrementTopologyRevision();
                }
                else
                {
                    if (!existing.Position.Equals(body.Position))
                    {
                        MarkBodyMoved(body);
                    }

                    IncrementBodyRevision();
                }

                if (_compoundIndices.TryGetValue(body.BodyId, out int compoundIndex))
                {
                    _compounds[compoundIndex].SetBody(body);
                }

                return;
            }

            _bodies.Add(body);
            _bodyIndices[body.BodyId] = _bodies.Count - 1;
            EnsureMovedBodyCapacity();
            IncrementTopologyRevision();
        }

        public bool RemoveBody(CombatBodyId bodyId)
        {
            int index = FindBodyIndex(bodyId);
            if (index < 0)
            {
                return false;
            }

            _bodies.RemoveAt(index);
            RebuildBodyIndices();
            for (int i = _aabbColliders.Count - 1; i >= 0; i--)
            {
                if (_aabbColliders[i].BodyId.Equals(bodyId))
                {
                    _aabbColliders.RemoveAt(i);
                }
            }

            RebuildAabbColliderIndices();

            for (int i = _sphereColliders.Count - 1; i >= 0; i--)
            {
                if (_sphereColliders[i].BodyId.Equals(bodyId))
                {
                    _sphereColliders.RemoveAt(i);
                }
            }

            RebuildSphereColliderIndices();

            for (int i = _capsuleColliders.Count - 1; i >= 0; i--)
            {
                if (_capsuleColliders[i].BodyId.Equals(bodyId))
                {
                    _capsuleColliders.RemoveAt(i);
                }
            }

            RebuildCapsuleColliderIndices();

            for (int i = _obbColliders.Count - 1; i >= 0; i--)
            {
                if (_obbColliders[i].BodyId.Equals(bodyId)) _obbColliders.RemoveAt(i);
            }
            RebuildObbColliderIndices();

            RemoveCompound(bodyId);

            IncrementTopologyRevision();
            return true;
        }

        public void UpsertAabbCollider(CombatPhysicsAabbCollider collider)
        {
            if (FindBodyIndex(collider.BodyId) < 0)
            {
                throw new InvalidOperationException("Cannot register a collider before its body is registered.");
            }

            bool removedOtherShape = RemoveNonAabbColliderVersion(collider.BodyId, collider.ColliderId);
            int index = FindAabbColliderIndex(collider.BodyId, collider.ColliderId);
            if (index >= 0)
            {
                if (_aabbColliders[index].Equals(collider))
                {
                    if (removedOtherShape)
                    {
                        IncrementColliderTopologyRevision();
                    }

                    return;
                }

                _aabbColliders[index] = collider;
                IncrementColliderTopologyRevision();
                return;
            }

            _aabbColliders.Add(collider);
            _aabbColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] =
                _aabbColliders.Count - 1;
            IncrementColliderTopologyRevision();
        }

        public void UpsertSphereCollider(CombatPhysicsSphereCollider collider)
        {
            if (FindBodyIndex(collider.BodyId) < 0)
            {
                throw new InvalidOperationException("Cannot register a collider before its body is registered.");
            }

            bool removedOtherShape = RemoveNonSphereColliderVersion(collider.BodyId, collider.ColliderId);
            int index = FindSphereColliderIndex(collider.BodyId, collider.ColliderId);
            if (index >= 0)
            {
                if (_sphereColliders[index].Equals(collider))
                {
                    if (removedOtherShape)
                    {
                        IncrementColliderTopologyRevision();
                    }

                    return;
                }

                _sphereColliders[index] = collider;
                IncrementColliderTopologyRevision();
                return;
            }

            _sphereColliders.Add(collider);
            _sphereColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] =
                _sphereColliders.Count - 1;
            IncrementColliderTopologyRevision();
        }

        public void UpsertCapsuleCollider(CombatPhysicsCapsuleCollider collider)
        {
            if (FindBodyIndex(collider.BodyId) < 0)
            {
                throw new InvalidOperationException("Cannot register a collider before its body is registered.");
            }

            bool removedOtherShape = RemoveNonCapsuleColliderVersion(collider.BodyId, collider.ColliderId);
            int index = FindCapsuleColliderIndex(collider.BodyId, collider.ColliderId);
            if (index >= 0)
            {
                if (_capsuleColliders[index].Equals(collider))
                {
                    if (removedOtherShape)
                    {
                        IncrementColliderTopologyRevision();
                    }

                    return;
                }

                _capsuleColliders[index] = collider;
                IncrementColliderTopologyRevision();
                return;
            }

            _capsuleColliders.Add(collider);
            _capsuleColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] =
                _capsuleColliders.Count - 1;
            IncrementColliderTopologyRevision();
        }

        public void UpsertObbCollider(CombatPhysicsObbCollider collider)
        {
            if (FindBodyIndex(collider.BodyId) < 0) throw new InvalidOperationException("Cannot register a collider before its body is registered.");
            bool removedOtherShape = RemoveNonObbColliderVersion(collider.BodyId, collider.ColliderId);
            int index = FindObbColliderIndex(collider.BodyId, collider.ColliderId);
            if (index >= 0)
            {
                if (_obbColliders[index].Equals(collider))
                {
                    if (removedOtherShape) IncrementColliderTopologyRevision();
                    return;
                }
                _obbColliders[index] = collider;
                IncrementColliderTopologyRevision();
                return;
            }
            _obbColliders.Add(collider);
            _obbColliderIndices[CombatPhysicsColliderKey.From(collider.BodyId, collider.ColliderId)] = _obbColliders.Count - 1;
            IncrementColliderTopologyRevision();
        }

        /// <summary>Defers broadphase invalidation until all collider poses in the batch are updated. NoAlloc.</summary>
        public void BeginColliderBatchUpdate() => _colliderBatchDepth++;

        /// <summary>Completes a collider batch and invalidates the broadphase exactly once. NoAlloc.</summary>
        public void EndColliderBatchUpdate()
        {
            if (_colliderBatchDepth <= 0) throw new InvalidOperationException("No collider batch update is active.");
            _colliderBatchDepth--;
            if (_colliderBatchDepth == 0 && _colliderBatchDirty)
            {
                _colliderBatchDirty = false;
                IncrementTopologyRevision();
            }
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

        public bool TryGetSphereCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            out CombatPhysicsSphereCollider collider)
        {
            int index = FindSphereColliderIndex(bodyId, colliderId);
            if (index < 0)
            {
                collider = default;
                return false;
            }

            collider = _sphereColliders[index];
            return true;
        }

        public bool TryGetCapsuleCollider(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            out CombatPhysicsCapsuleCollider collider)
        {
            int index = FindCapsuleColliderIndex(bodyId, colliderId);
            if (index < 0)
            {
                collider = default;
                return false;
            }

            collider = _capsuleColliders[index];
            return true;
        }

        public bool TryGetObbCollider(CombatBodyId bodyId, CombatColliderId colliderId, out CombatPhysicsObbCollider collider)
        {
            int index = FindObbColliderIndex(bodyId, colliderId);
            if (index < 0) { collider = default; return false; }
            collider = _obbColliders[index];
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

            CombatPhysicsBody movedBody = new CombatPhysicsBody(body.EntityId, body.BodyId, position);
            _bodies[index] = movedBody;
            if (_compoundIndices.TryGetValue(bodyId, out int compoundIndex))
            {
                _compounds[compoundIndex].SetBody(movedBody);
            }
            MarkBodyMoved(movedBody);
            IncrementBodyRevision();
            return true;
        }

        private void MarkBodyMoved(CombatPhysicsBody body)
        {
            EnsureMovedBodyCapacity();
            if (_movedBodyIndices.TryGetValue(body.BodyId, out int index))
            {
                _movedBodies[index] = body;
                return;
            }

            _movedBodyIndices[body.BodyId] = _movedBodies.Count;
            _movedBodies.Add(body);
        }

        private void EnsureMovedBodyCapacity()
        {
            if (_movedBodies.Capacity < _bodies.Count)
            {
                _movedBodies.Capacity = _bodies.Count;
            }

            if (_movedBodyIndices.Count < _bodies.Count)
            {
                _movedBodyIndices.EnsureCapacity(_bodies.Count);
            }
        }

        private void ClearMovedBodies()
        {
            _movedBodies.Clear();
            _movedBodyIndices.Clear();
        }

        public CombatPhysicsWorldStats CreateStats()
        {
            return new CombatPhysicsWorldStats(_revision, _bodies.Count, ColliderCount);
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

        internal int CopyBodiesUnsortedTo(List<CombatPhysicsBody> bodies)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            for (int i = 0; i < _bodies.Count; i++)
                bodies.Add(_bodies[i]);
            return _bodies.Count;
        }

        internal int CopyMovedBodiesUnsortedTo(List<CombatPhysicsBody> bodies)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            int startCount = bodies.Count;
            for (int i = 0; i < _movedBodies.Count; i++)
                bodies.Add(_movedBodies[i]);
            return bodies.Count - startCount;
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

            SortCopiedColliders(colliders, startCount, _aabbColliders.Count, CombatPhysicsAabbColliderComparer.Instance, AabbCopyComparison);
            return _aabbColliders.Count;
        }

        internal int CopyAabbCollidersUnsortedTo(List<CombatPhysicsAabbCollider> colliders)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            for (int i = 0; i < _aabbColliders.Count; i++)
                colliders.Add(_aabbColliders[i]);
            return _aabbColliders.Count;
        }

        public int CopySphereCollidersTo(List<CombatPhysicsSphereCollider> colliders)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            int startCount = colliders.Count;
            for (int i = 0; i < _sphereColliders.Count; i++)
            {
                colliders.Add(_sphereColliders[i]);
            }

            int copiedCount = _sphereColliders.Count + CopyCompoundSphereCollidersTo(colliders);
            SortCopiedColliders(colliders, startCount, copiedCount, CombatPhysicsSphereColliderComparer.Instance, SphereCopyComparison);
            return copiedCount;
        }

        internal int CopySphereCollidersUnsortedTo(List<CombatPhysicsSphereCollider> colliders)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            for (int i = 0; i < _sphereColliders.Count; i++)
                colliders.Add(_sphereColliders[i]);
            return _sphereColliders.Count + CopyCompoundSphereCollidersTo(colliders);
        }

        public int CopyCapsuleCollidersTo(List<CombatPhysicsCapsuleCollider> colliders)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            int startCount = colliders.Count;
            for (int i = 0; i < _capsuleColliders.Count; i++)
            {
                colliders.Add(_capsuleColliders[i]);
            }

            int copiedCount = _capsuleColliders.Count + CopyCompoundCapsuleCollidersTo(colliders);
            SortCopiedColliders(colliders, startCount, copiedCount, CombatPhysicsCapsuleColliderComparer.Instance, CapsuleCopyComparison);
            return copiedCount;
        }

        internal int CopyCapsuleCollidersUnsortedTo(List<CombatPhysicsCapsuleCollider> colliders)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            for (int i = 0; i < _capsuleColliders.Count; i++)
                colliders.Add(_capsuleColliders[i]);
            return _capsuleColliders.Count + CopyCompoundCapsuleCollidersTo(colliders);
        }

        public int CopyObbCollidersTo(List<CombatPhysicsObbCollider> colliders)
        {
            if (colliders == null) throw new ArgumentNullException(nameof(colliders));
            int startCount = colliders.Count;
            for (int i = 0; i < _obbColliders.Count; i++) colliders.Add(_obbColliders[i]);
            int copiedCount = _obbColliders.Count + CopyCompoundObbCollidersTo(colliders);
            SortCopiedColliders(colliders, startCount, copiedCount, CombatPhysicsObbColliderComparer.Instance, ObbCopyComparison);
            return copiedCount;
        }

        internal int CopyObbCollidersUnsortedTo(List<CombatPhysicsObbCollider> colliders)
        {
            if (colliders == null) throw new ArgumentNullException(nameof(colliders));
            for (int i = 0; i < _obbColliders.Count; i++) colliders.Add(_obbColliders[i]);
            return _obbColliders.Count + CopyCompoundObbCollidersTo(colliders);
        }

        public bool RemoveCollider(CombatBodyId bodyId, CombatColliderId colliderId)
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

            index = FindCapsuleColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _capsuleColliders.RemoveAt(index);
                RebuildCapsuleColliderIndices();
                removed = true;
            }

            index = FindObbColliderIndex(bodyId, colliderId);
            if (index >= 0)
            {
                _obbColliders.RemoveAt(index);
                RebuildObbColliderIndices();
                removed = true;
            }

            removed |= RemoveCompoundLeaf(bodyId, colliderId);

            if (removed)
            {
                IncrementColliderTopologyRevision();
            }

            return removed;
        }
    }
}
