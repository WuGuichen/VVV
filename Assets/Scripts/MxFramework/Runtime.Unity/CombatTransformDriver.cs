using MxFramework.Combat.Core;
using UnityEngine;

namespace MxFramework.Runtime.Unity
{
    [AddComponentMenu("MxFramework/Combat/Combat Transform Driver")]
    public sealed class CombatTransformDriver : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _entityId;
        [SerializeField, Min(0f)] private float _interpolationSpeed = 20f;

        private ICombatEntityPoseSource _poseSource;

        public CombatEntityId EntityId
        {
            get => new CombatEntityId(_entityId);
            set => _entityId = value.Value;
        }

        public float InterpolationSpeed
        {
            get => _interpolationSpeed;
            set => _interpolationSpeed = Mathf.Max(0f, value);
        }

        public ICombatEntityPoseSource PoseSource => _poseSource;

        public void SetPoseSource(ICombatEntityPoseSource poseSource)
        {
            _poseSource = poseSource;
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (_poseSource == null
                || !_poseSource.TryGetPose(EntityId, out Vector3 targetPosition, out Quaternion targetRotation))
            {
                return;
            }

            float speed = Mathf.Max(0f, _interpolationSpeed);
            if (speed <= 0f || deltaTime <= 0f)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            float t = Mathf.Clamp01(speed * deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, targetPosition, t),
                Quaternion.Slerp(transform.rotation, targetRotation, t));
        }
    }
}
