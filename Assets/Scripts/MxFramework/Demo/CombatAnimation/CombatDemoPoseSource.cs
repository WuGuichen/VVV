using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Runtime.Unity;
using UnityEngine;

namespace MxFramework.Demo.CombatAnimation
{
    public sealed class CombatDemoPoseSource : ICombatEntityPoseSource
    {
        private readonly Dictionary<CombatEntityId, PoseState> _poses = new Dictionary<CombatEntityId, PoseState>();

        public void SetPose(CombatEntityId entityId, Vector3 position, Quaternion rotation)
        {
            _poses[entityId] = new PoseState(position, rotation);
        }

        public void Move(CombatEntityId entityId, Vector3 delta)
        {
            if (!_poses.TryGetValue(entityId, out PoseState pose))
            {
                pose = new PoseState(Vector3.zero, Quaternion.identity);
            }

            Vector3 position = pose.Position + delta;
            Quaternion rotation = delta.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(delta.normalized, Vector3.up)
                : pose.Rotation;
            _poses[entityId] = new PoseState(position, rotation);
        }

        public bool TryGetPose(CombatEntityId entityId, out Vector3 position, out Quaternion rotation)
        {
            if (_poses.TryGetValue(entityId, out PoseState pose))
            {
                position = pose.Position;
                rotation = pose.Rotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }

        private readonly struct PoseState
        {
            public PoseState(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; }

            public Quaternion Rotation { get; }
        }
    }
}
