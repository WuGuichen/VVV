using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Combat Physics Capsule Marker")]
    public sealed class CharacterTestCombatPhysicsCapsuleMarker : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _combatLayer = 1;
        [SerializeField] private int _bodyId;
        [SerializeField] private int _colliderId = 1;
        [SerializeField] private CombatSurfaceMaterialKind _surfaceMaterialKind =
            CombatSurfaceMaterialKind.Default;
        [SerializeField] private float _surfaceFriction = 10f;
        [SerializeField][Range(0f, 1f)] private float _surfaceElasticity;
        [SerializeField] private float _surfaceFixedBounceHeight;

        public bool IsEnabled => _enabled;
        public int CombatLayer => _combatLayer;
        public int BodyId => _bodyId;
        public int ColliderId => _colliderId;
        public CombatSurfaceMaterialKind SurfaceMaterialKind => NormalizeMaterialKind(_surfaceMaterialKind);
        public float SurfaceFriction => ResolveSurfaceFriction(SurfaceMaterialKind, _surfaceFriction);
        public float SurfaceElasticity => Mathf.Clamp01(_surfaceElasticity);
        public float SurfaceFixedBounceHeight => Mathf.Max(0f, _surfaceFixedBounceHeight);
        public CombatSurfaceMaterial SurfaceMaterial =>
            new CombatSurfaceMaterial(
                SurfaceMaterialKind,
                CharacterTestCombatPhysicsSceneBridge.ToFix64(SurfaceFriction),
                CharacterTestCombatPhysicsSceneBridge.ToFix64(SurfaceElasticity),
                CharacterTestCombatPhysicsSceneBridge.ToFix64(SurfaceFixedBounceHeight));

        public float Radius
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                float diameter = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                return Mathf.Max(0.01f, diameter * 0.5f);
            }
        }

        public float HalfHeight
        {
            get
            {
                return Mathf.Max(Radius, Mathf.Abs(transform.lossyScale.y));
            }
        }

        public Vector3 WorldPointA => transform.position - Axis * SegmentHalfLength;

        public Vector3 WorldPointB => transform.position + Axis * SegmentHalfLength;

        private Vector3 Axis => transform.rotation * Vector3.up;

        private float SegmentHalfLength => Mathf.Max(0f, HalfHeight - Radius);

        private void OnValidate()
        {
            _surfaceFriction = Mathf.Max(0f, _surfaceFriction);
            _surfaceElasticity = Mathf.Clamp01(_surfaceElasticity);
            _surfaceFixedBounceHeight = Mathf.Max(0f, _surfaceFixedBounceHeight);
        }

        private static CombatSurfaceMaterialKind NormalizeMaterialKind(CombatSurfaceMaterialKind kind)
        {
            switch (kind)
            {
                case CombatSurfaceMaterialKind.HardSlick:
                case CombatSurfaceMaterialKind.Rough:
                case CombatSurfaceMaterialKind.Elastic:
                case CombatSurfaceMaterialKind.BouncePad:
                    return kind;
                default:
                    return CombatSurfaceMaterialKind.Default;
            }
        }

        private static float ResolveSurfaceFriction(CombatSurfaceMaterialKind kind, float surfaceFriction)
        {
            float clamped = Mathf.Max(0f, surfaceFriction);
            if (kind == CombatSurfaceMaterialKind.HardSlick && clamped <= 0f)
                return ToFloat(CombatSurfaceMaterial.HardSlick.Friction);

            return clamped;
        }

        private static float ToFloat(MxFramework.Core.Math.Fix64 value)
        {
            return (float)((double)value.RawValue / MxFramework.Core.Math.Fix64.Scale);
        }
    }
}
