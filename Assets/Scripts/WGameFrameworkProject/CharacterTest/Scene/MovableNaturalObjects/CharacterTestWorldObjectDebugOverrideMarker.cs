using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/World Object Debug Override Marker")]
    public sealed class CharacterTestWorldObjectDebugOverrideMarker : MonoBehaviour
    {
        [SerializeField] private bool _enabled;
        [SerializeField] private bool _overrideMass;
        [SerializeField] private float _massKg = 40f;
        [SerializeField] private bool _overrideMaterial;
        [SerializeField] private CombatSurfaceMaterialKind _surfaceMaterialKind =
            CombatSurfaceMaterialKind.Default;
        [SerializeField] private float _surfaceFriction = 10f;
        [SerializeField][Range(0f, 1f)] private float _surfaceElasticity;
        [SerializeField] private float _surfaceFixedBounceHeight;
        [SerializeField] private bool _overrideShape;
        [SerializeField] private CharacterTestMovableNaturalObjectShapeKind _shapeKind =
            CharacterTestMovableNaturalObjectShapeKind.Aabb;
        [SerializeField] private bool _overridePotency;
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

        public CharacterTestWorldObjectDefinition ApplyOverrides(
            CharacterTestWorldObjectDefinition definition)
        {
            if (!_enabled)
                return definition;

            CharacterTestWorldObjectMaterialFacet material = _overrideMaterial
                ? new CharacterTestWorldObjectMaterialFacet(CreateSurfaceMaterial())
                : definition.Material;
            CharacterTestWorldObjectShapeFacet shape = _overrideShape
                ? new CharacterTestWorldObjectShapeFacet(
                    _shapeKind,
                    definition.Shape.HalfExtentXMillimeters,
                    definition.Shape.HalfExtentYMillimeters,
                    definition.Shape.HalfExtentZMillimeters)
                : definition.Shape;
            CharacterTestWorldObjectPotencyFacet potency = _overridePotency
                ? new CharacterTestWorldObjectPotencyFacet(
                    new CharacterTestGenesisPotencyTriplet(
                        _basePotencyMotion,
                        _basePotencyCohesion,
                        _basePotencyScatter),
                    CreatePotencyProfile())
                : definition.Potency;
            CharacterTestMovableNaturalObjectFacet movable =
                definition.MovableNaturalObject;
            if (_overrideMass && definition.MovableNaturalObject.IsEnabled)
            {
                movable = new CharacterTestMovableNaturalObjectFacet(
                    CharacterTestDeterministicUnits.KilogramsToGrams(Mathf.Max(0.001f, _massKg)));
            }

            return new CharacterTestWorldObjectDefinition(
                definition.DefinitionId,
                definition.StableId,
                definition.DisplayName,
                definition.ObjectKind,
                definition.Category,
                material,
                shape,
                potency,
                movable);
        }

        private CombatSurfaceMaterial CreateSurfaceMaterial()
        {
            return new CombatSurfaceMaterial(
                NormalizeMaterialKind(_surfaceMaterialKind),
                ToFix64(Mathf.Max(0f, _surfaceFriction)),
                ToFix64(Mathf.Clamp01(_surfaceElasticity)),
                ToFix64(Mathf.Max(0f, _surfaceFixedBounceHeight)));
        }

        private CharacterTestGenesisPotencyProfile CreatePotencyProfile()
        {
            return new CharacterTestGenesisPotencyProfile(
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
        }

        private void OnValidate()
        {
            _massKg = Mathf.Max(0.001f, _massKg);
            _surfaceMaterialKind = NormalizeMaterialKind(_surfaceMaterialKind);
            _surfaceFriction = Mathf.Max(0f, _surfaceFriction);
            _surfaceElasticity = Mathf.Clamp01(_surfaceElasticity);
            _surfaceFixedBounceHeight = Mathf.Max(0f, _surfaceFixedBounceHeight);
            _shapeKind = CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(_shapeKind);
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

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw(checked((long)(value * Fix64.Scale)));
        }
    }
}
