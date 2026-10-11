using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [Serializable]
    public sealed class CharacterTestScenePhysicsSettings
    {
        public const float DefaultGroundSnapDistance = 0.10f;

        [SerializeField] private bool _enabled = true;
        [SerializeField] private Vector3 _characterHalfExtents = new Vector3(0.45f, 0.9f, 0.45f);
        [SerializeField] private float _skinWidth = 0.01f;
        [SerializeField] private float _maxStepHeight = 0.35f;
        [SerializeField] private float _groundSnapDistance = DefaultGroundSnapDistance;

        public bool Enabled => _enabled;

        public Vector3 CharacterHalfExtents =>
            MaxVector3(_characterHalfExtents, new Vector3(0.01f, 0.01f, 0.01f));

        public float SkinWidth => Mathf.Max(0f, _skinWidth);

        public float MaxStepHeight => Mathf.Max(0f, _maxStepHeight);

        public float GroundSnapDistance => Mathf.Max(0f, _groundSnapDistance);

        private static Vector3 MaxVector3(Vector3 value, Vector3 min)
        {
            return new Vector3(
                Mathf.Max(value.x, min.x),
                Mathf.Max(value.y, min.y),
                Mathf.Max(value.z, min.z));
        }
    }
}
