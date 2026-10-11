using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Movable Natural Object Marker")]
    public sealed class CharacterTestMovableNaturalObjectMarker : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _stableId = "";
        [SerializeField] private int _numericId;
        [SerializeField] private string _category = CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory;
        [SerializeField] private float _massKg = 40f;
        [SerializeField] private CharacterTestMovableNaturalObjectShapeKind _shapeKind =
            CharacterTestMovableNaturalObjectShapeKind.Aabb;
        [SerializeField] private CombatSurfaceMaterialKind _surfaceMaterialKind =
            CombatSurfaceMaterialKind.Default;
        [SerializeField] private float _surfaceFriction = 10f;
        [SerializeField][Range(0f, 1f)] private float _surfaceElasticity;
        [SerializeField] private float _surfaceFixedBounceHeight;
        [Header("Genesis Potency")]
        [SerializeField][Min(0)] private int _basePotencyMotion;
        [SerializeField][Min(0)] private int _basePotencyCohesion;
        [SerializeField][Min(0)] private int _basePotencyScatter;
        [SerializeField] private bool _potencyImmuneMotion;
        [SerializeField] private bool _potencyImmuneCohesion;
        [SerializeField] private bool _potencyImmuneScatter;
        [SerializeField] private bool _potencyBurningEnabled;
        [SerializeField] private bool _potencyCrushEnabled;
        [SerializeField] private bool _potencyCrackEnabled;
        [SerializeField] private bool _potencyCondensedLayerEnabled;
        [SerializeField][Min(0)] private int _potencyBurningMinMotion =
            CharacterTestGenesisPotencyProfile.DefaultBurningMinMotion;
        [SerializeField][Min(0)] private int _potencyBurningMinScatter =
            CharacterTestGenesisPotencyProfile.DefaultBurningMinScatter;
        [SerializeField][Min(0)] private int _potencyBurningMaxCohesion =
            CharacterTestGenesisPotencyProfile.DefaultBurningMaxCohesion;
        [SerializeField][Min(0)] private int _potencyCondensedLayerMinCohesion =
            CharacterTestGenesisPotencyProfile.DefaultCondensedLayerMinCohesion;
        [SerializeField][Min(0)] private int _potencyCondensedLayerMaxMotion =
            CharacterTestGenesisPotencyProfile.DefaultCondensedLayerMaxMotion;

        public bool IsEnabled => _enabled;
        public string StableId => string.IsNullOrWhiteSpace(_stableId)
            ? "movable.natural." + _numericId
            : _stableId.Trim();
        public int NumericId => _numericId;
        public string Category => string.IsNullOrWhiteSpace(_category)
            ? CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory
            : _category.Trim();
        public float MassKg => Mathf.Max(0.001f, _massKg);
        public CharacterTestMovableNaturalObjectShapeKind ShapeKind =>
            CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(_shapeKind);
        public CharacterTestGenesisPotencyTriplet BasePotency =>
            CharacterTestGenesisPotencyRuntime.ClampState(new CharacterTestGenesisPotencyTriplet(
                _basePotencyMotion,
                _basePotencyCohesion,
                _basePotencyScatter));
        public CharacterTestGenesisPotencyProfile PotencyProfile =>
            new CharacterTestGenesisPotencyProfile(
                _potencyImmuneMotion,
                _potencyImmuneCohesion,
                _potencyImmuneScatter,
                _potencyBurningEnabled,
                _potencyCrushEnabled,
                _potencyCrackEnabled,
                _potencyCondensedLayerEnabled,
                _potencyBurningMinMotion,
                _potencyBurningMinScatter,
                _potencyBurningMaxCohesion,
                _potencyCondensedLayerMinCohesion,
                _potencyCondensedLayerMaxMotion);
        public CombatSurfaceMaterial SurfaceMaterial => new CombatSurfaceMaterial(
            NormalizeMaterialKind(_surfaceMaterialKind),
            CharacterTestCombatPhysicsSceneBridge.ToFix64(ResolveSurfaceFriction()),
            CharacterTestCombatPhysicsSceneBridge.ToFix64(Mathf.Clamp01(_surfaceElasticity)),
            CharacterTestCombatPhysicsSceneBridge.ToFix64(Mathf.Max(0f, _surfaceFixedBounceHeight)));

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

        public CharacterTestMovableNaturalObjectSpawnSpec CreateSpawnSpec()
        {
            return CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                StableId,
                NumericId,
                Category,
                MassKg,
                SurfaceMaterial,
                HalfExtents,
                transform.position,
                ShapeKind,
                BasePotency,
                PotencyProfile);
        }

        public void ApplyRuntimeState(CharacterTestMovableNaturalObjectState state)
        {
            if (state == null)
                return;

            transform.position = state.Position;
        }

        public void Configure(
            string stableId,
            int numericId,
            string category,
            float massKg,
            CombatSurfaceMaterialKind materialKind,
            float surfaceFriction,
            float surfaceElasticity,
            float surfaceFixedBounceHeight,
            CharacterTestMovableNaturalObjectShapeKind shapeKind = CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestGenesisPotencyTriplet basePotency = default,
            CharacterTestGenesisPotencyProfile potencyProfile = default)
        {
            _stableId = stableId ?? string.Empty;
            _numericId = Mathf.Max(0, numericId);
            _category = string.IsNullOrWhiteSpace(category)
                ? CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory
                : category;
            _massKg = Mathf.Max(0.001f, massKg);
            _shapeKind = CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(shapeKind);
            _surfaceMaterialKind = NormalizeMaterialKind(materialKind);
            _surfaceFriction = Mathf.Max(0f, surfaceFriction);
            _surfaceElasticity = Mathf.Clamp01(surfaceElasticity);
            _surfaceFixedBounceHeight = Mathf.Max(0f, surfaceFixedBounceHeight);
            CharacterTestGenesisPotencyTriplet clampedBase =
                CharacterTestGenesisPotencyRuntime.ClampState(basePotency);
            CharacterTestGenesisPotencyProfile normalizedProfile = potencyProfile.Normalize();
            _basePotencyMotion = clampedBase.Motion;
            _basePotencyCohesion = clampedBase.Cohesion;
            _basePotencyScatter = clampedBase.Scatter;
            _potencyImmuneMotion = normalizedProfile.ImmuneMotion;
            _potencyImmuneCohesion = normalizedProfile.ImmuneCohesion;
            _potencyImmuneScatter = normalizedProfile.ImmuneScatter;
            _potencyBurningEnabled = normalizedProfile.BurningEnabled;
            _potencyCrushEnabled = normalizedProfile.CrushEnabled;
            _potencyCrackEnabled = normalizedProfile.CrackEnabled;
            _potencyCondensedLayerEnabled = normalizedProfile.CondensedLayerEnabled;
            _potencyBurningMinMotion = normalizedProfile.BurningMinMotion;
            _potencyBurningMinScatter = normalizedProfile.BurningMinScatter;
            _potencyBurningMaxCohesion = normalizedProfile.BurningMaxCohesion;
            _potencyCondensedLayerMinCohesion = normalizedProfile.CondensedLayerMinCohesion;
            _potencyCondensedLayerMaxMotion = normalizedProfile.CondensedLayerMaxMotion;
        }

        private void OnValidate()
        {
            _numericId = Mathf.Max(0, _numericId);
            _massKg = Mathf.Max(0.001f, _massKg);
            _shapeKind = CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(_shapeKind);
            _surfaceMaterialKind = NormalizeMaterialKind(_surfaceMaterialKind);
            _surfaceFriction = Mathf.Max(0f, _surfaceFriction);
            _surfaceElasticity = Mathf.Clamp01(_surfaceElasticity);
            _surfaceFixedBounceHeight = Mathf.Max(0f, _surfaceFixedBounceHeight);
            _basePotencyMotion = CharacterTestGenesisPotencyRuntime.ClampStateValue(_basePotencyMotion);
            _basePotencyCohesion = CharacterTestGenesisPotencyRuntime.ClampStateValue(_basePotencyCohesion);
            _basePotencyScatter = CharacterTestGenesisPotencyRuntime.ClampStateValue(_basePotencyScatter);
            _potencyBurningMinMotion =
                CharacterTestGenesisPotencyRuntime.ClampStateValue(_potencyBurningMinMotion);
            _potencyBurningMinScatter =
                CharacterTestGenesisPotencyRuntime.ClampStateValue(_potencyBurningMinScatter);
            _potencyBurningMaxCohesion =
                CharacterTestGenesisPotencyRuntime.ClampStateValue(_potencyBurningMaxCohesion);
            _potencyCondensedLayerMinCohesion =
                CharacterTestGenesisPotencyRuntime.ClampStateValue(_potencyCondensedLayerMinCohesion);
            _potencyCondensedLayerMaxMotion =
                CharacterTestGenesisPotencyRuntime.ClampStateValue(_potencyCondensedLayerMaxMotion);
        }

        private float ResolveSurfaceFriction()
        {
            float clamped = Mathf.Max(0f, _surfaceFriction);
            if (_surfaceMaterialKind == CombatSurfaceMaterialKind.HardSlick && clamped <= 0f)
                return ToFloat(CombatSurfaceMaterial.HardSlick.Friction);

            return clamped;
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

        private static float ToFloat(MxFramework.Core.Math.Fix64 value)
        {
            return (float)((double)value.RawValue / MxFramework.Core.Math.Fix64.Scale);
        }
    }
}
