using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestWorldObjectInstanceScaleSource
    {
        SceneTransform = 0,
        Definition = 1
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/World Object Instance Marker")]
    public sealed class CharacterTestWorldObjectInstanceMarker : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _definitionId;
        [SerializeField] private int _numericInstanceId;
        [SerializeField] private string _instanceStableId = "";
        [SerializeField] private CharacterTestWorldObjectInstanceScaleSource _scaleSource =
            CharacterTestWorldObjectInstanceScaleSource.SceneTransform;

        public bool IsEnabled => _enabled;

        public int DefinitionId => _definitionId;

        public int NumericInstanceId => _numericInstanceId;

        public string InstanceStableId => string.IsNullOrWhiteSpace(_instanceStableId)
            ? string.Empty
            : _instanceStableId.Trim();

        public CharacterTestWorldObjectInstanceScaleSource ScaleSource => _scaleSource;

        public void Configure(
            int definitionId,
            int numericInstanceId,
            string instanceStableId,
            CharacterTestWorldObjectInstanceScaleSource scaleSource =
                CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
        {
            _definitionId = Mathf.Max(0, definitionId);
            _numericInstanceId = Mathf.Max(0, numericInstanceId);
            _instanceStableId = instanceStableId ?? string.Empty;
            _scaleSource = NormalizeScaleSource(scaleSource);
        }

        public bool TryCreateMovableNaturalObjectSpawnSpec(
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out CharacterTestMovableNaturalObjectSpawnSpec spec,
            out string diagnostic)
        {
            spec = default;
            diagnostic = string.Empty;
            if (!_enabled)
                return false;
            if (definitionProvider == null)
            {
                diagnostic = "world-object.instance.missing-definition-provider";
                return false;
            }
            if (_definitionId <= 0)
            {
                diagnostic = "world-object.instance.invalid-definition-id";
                return false;
            }
            if (_numericInstanceId <= 0)
            {
                diagnostic = "world-object.instance.invalid-numeric-id";
                return false;
            }
            if (!definitionProvider.TryGetDefinition(
                    _definitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                diagnostic = "world-object.instance.definition-not-found:" + _definitionId;
                return false;
            }

            CharacterTestWorldObjectDebugOverrideMarker overrideMarker =
                GetComponent<CharacterTestWorldObjectDebugOverrideMarker>();
            if (overrideMarker != null && overrideMarker.IsEnabled)
                definition = overrideMarker.ApplyOverrides(definition);

            Vector3 halfExtents = ResolveHalfExtents(definition);
            bool created = definition.TryCreateMovableNaturalObjectSpawnSpec(
                _numericInstanceId,
                InstanceStableId,
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.z),
                CharacterTestDeterministicUnits.MetersToMillimeters(transform.position.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(transform.position.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(transform.position.z),
                out spec);
            if (!created)
                diagnostic = "world-object.instance.definition-not-movable:" + _definitionId;

            return created;
        }

        private Vector3 ResolveHalfExtents(CharacterTestWorldObjectDefinition definition)
        {
            if (_scaleSource == CharacterTestWorldObjectInstanceScaleSource.Definition)
            {
                return new Vector3(
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentXMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentYMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentZMillimeters));
            }

            Vector3 scale = transform.lossyScale;
            return new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(scale.x) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.y) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.z) * 0.5f));
        }

        private void OnValidate()
        {
            _definitionId = Mathf.Max(0, _definitionId);
            _numericInstanceId = Mathf.Max(0, _numericInstanceId);
            _scaleSource = NormalizeScaleSource(_scaleSource);
        }

        private static CharacterTestWorldObjectInstanceScaleSource NormalizeScaleSource(
            CharacterTestWorldObjectInstanceScaleSource source)
        {
            return source == CharacterTestWorldObjectInstanceScaleSource.Definition
                ? CharacterTestWorldObjectInstanceScaleSource.Definition
                : CharacterTestWorldObjectInstanceScaleSource.SceneTransform;
        }
    }
}
