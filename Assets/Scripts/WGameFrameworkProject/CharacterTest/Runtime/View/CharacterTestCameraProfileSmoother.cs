using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCameraProfileSmoother
    {
        private bool _hasValue;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private Vector3 _worldOffset;

        public void Reset()
        {
            _hasValue = false;
            _yaw = 0f;
            _pitch = 0f;
            _distance = 0f;
            _worldOffset = Vector3.zero;
        }

        public void Snap(
            float yaw,
            float pitch,
            float distance,
            Vector3 worldOffset)
        {
            _hasValue = true;
            _yaw = yaw;
            _pitch = pitch;
            _distance = distance;
            _worldOffset = worldOffset;
        }

        public void Smooth(
            float sharpness,
            float deltaTime,
            ref float yaw,
            ref float pitch,
            ref float distance,
            ref Vector3 worldOffset)
        {
            if (!_hasValue || sharpness <= 0f)
            {
                _hasValue = true;
                _yaw = yaw;
                _pitch = pitch;
                _distance = distance;
                _worldOffset = worldOffset;
                return;
            }

            float t = 1f - Mathf.Exp(-sharpness * Mathf.Max(0f, deltaTime));
            _yaw = Mathf.LerpAngle(_yaw, yaw, t);
            _pitch = Mathf.Lerp(_pitch, pitch, t);
            _distance = Mathf.Lerp(_distance, distance, t);
            _worldOffset = Vector3.Lerp(_worldOffset, worldOffset, t);

            yaw = _yaw;
            pitch = _pitch;
            distance = _distance;
            worldOffset = _worldOffset;
        }
    }
}
