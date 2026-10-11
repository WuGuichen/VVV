using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Combat Physics AABB Marker")]
    public sealed class CharacterTestCombatPhysicsAabbMarker : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _combatLayer = 1;
        [SerializeField] private int _bodyId = 0;
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
                ToFix64(SurfaceFriction),
                ToFix64(SurfaceElasticity),
                ToFix64(SurfaceFixedBounceHeight));

        public Vector3 HalfExtents
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                return new Vector3(
                    Mathf.Max(0.01f, Mathf.Abs(scale.x) * 0.5f),
                    Mathf.Max(0.01f, Mathf.Abs(scale.y) * 0.5f),
                    Mathf.Max(0.01f, Mathf.Abs(scale.z) * 0.5f));
            }
        }

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

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)System.Math.Round(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }
}
