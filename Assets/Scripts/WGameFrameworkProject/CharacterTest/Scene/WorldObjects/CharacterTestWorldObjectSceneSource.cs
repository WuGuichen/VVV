using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/World Object Scene Source")]
    public sealed class CharacterTestWorldObjectSceneSource : MonoBehaviour
    {
        private static readonly CharacterTestWorldObjectDefinitionCatalog BuiltInDefinitionCatalog =
            CharacterTestWorldObjectDefinitionCatalogSource.Catalog;

        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _authoringJsonPath = "";
        [SerializeField] private string _authoringContentHash = "";
        [SerializeField] private string _authoringLastAppliedUtc = "";
        [SerializeField] private MovableInstance[] _movableInstances = Array.Empty<MovableInstance>();
        [SerializeField] private StaticColliderInstance[] _staticColliderInstances =
            Array.Empty<StaticColliderInstance>();
        [SerializeField] private LevelPointInstance[] _pointInstances = Array.Empty<LevelPointInstance>();
        [SerializeField] private LevelRegionInstance[] _regionInstances = Array.Empty<LevelRegionInstance>();

        private string[] _lastDiagnostics = Array.Empty<string>();

        public bool Enabled => _enabled;

        public string AuthoringJsonPath => _authoringJsonPath ?? string.Empty;

        public string AuthoringContentHash => _authoringContentHash ?? string.Empty;

        public string AuthoringLastAppliedUtc => _authoringLastAppliedUtc ?? string.Empty;

        public IReadOnlyList<MovableInstance> MovableInstances =>
            _movableInstances ?? Array.Empty<MovableInstance>();

        public IReadOnlyList<StaticColliderInstance> StaticColliderInstances =>
            _staticColliderInstances ?? Array.Empty<StaticColliderInstance>();

        public IReadOnlyList<LevelPointInstance> PointInstances =>
            _pointInstances ?? Array.Empty<LevelPointInstance>();

        public IReadOnlyList<LevelRegionInstance> RegionInstances =>
            _regionInstances ?? Array.Empty<LevelRegionInstance>();

        public int MovableInstanceCount => _movableInstances == null ? 0 : _movableInstances.Length;

        public int StaticColliderInstanceCount =>
            _staticColliderInstances == null ? 0 : _staticColliderInstances.Length;

        public int PointInstanceCount => _pointInstances == null ? 0 : _pointInstances.Length;

        public int RegionInstanceCount => _regionInstances == null ? 0 : _regionInstances.Length;

        public IReadOnlyList<string> LastDiagnostics => _lastDiagnostics;

        public static CharacterTestWorldObjectDefinitionCatalog BuiltInCatalog => BuiltInDefinitionCatalog;

        public void Configure(
            IEnumerable<MovableInstance> movableInstances,
            IEnumerable<StaticColliderInstance> staticColliderInstances)
        {
            Configure(
                movableInstances,
                staticColliderInstances,
                Array.Empty<LevelPointInstance>(),
                Array.Empty<LevelRegionInstance>());
        }

        public void Configure(
            IEnumerable<MovableInstance> movableInstances,
            IEnumerable<StaticColliderInstance> staticColliderInstances,
            IEnumerable<LevelPointInstance> pointInstances,
            IEnumerable<LevelRegionInstance> regionInstances)
        {
            _movableInstances = ToArray(movableInstances);
            _staticColliderInstances = ToArray(staticColliderInstances);
            _pointInstances = ToArray(pointInstances);
            _regionInstances = ToArray(regionInstances);
        }

        public void ConfigureFromAuthoring(
            IEnumerable<MovableInstance> movableInstances,
            IEnumerable<StaticColliderInstance> staticColliderInstances,
            string authoringJsonPath,
            string authoringContentHash,
            string authoringLastAppliedUtc)
        {
            ConfigureFromAuthoring(
                movableInstances,
                staticColliderInstances,
                Array.Empty<LevelPointInstance>(),
                Array.Empty<LevelRegionInstance>(),
                authoringJsonPath,
                authoringContentHash,
                authoringLastAppliedUtc);
        }

        public void ConfigureFromAuthoring(
            IEnumerable<MovableInstance> movableInstances,
            IEnumerable<StaticColliderInstance> staticColliderInstances,
            IEnumerable<LevelPointInstance> pointInstances,
            IEnumerable<LevelRegionInstance> regionInstances,
            string authoringJsonPath,
            string authoringContentHash,
            string authoringLastAppliedUtc)
        {
            Configure(movableInstances, staticColliderInstances, pointInstances, regionInstances);
            _authoringJsonPath = NormalizeAuthoringString(authoringJsonPath);
            _authoringContentHash = NormalizeAuthoringString(authoringContentHash);
            _authoringLastAppliedUtc = NormalizeAuthoringString(authoringLastAppliedUtc);
        }

        public CharacterTestMovableNaturalObjectSpawnSpec[] CollectMovableSpawnSpecs(
            out string[] diagnostics)
        {
            return CollectMovableSpawnSpecs(BuiltInDefinitionCatalog, out diagnostics);
        }

        public CharacterTestMovableNaturalObjectSpawnSpec[] CollectMovableSpawnSpecs(
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out string[] diagnostics)
        {
            var localDiagnostics = new List<string>();
            if (!_enabled || _movableInstances == null || _movableInstances.Length == 0)
            {
                diagnostics = Array.Empty<string>();
                return Array.Empty<CharacterTestMovableNaturalObjectSpawnSpec>();
            }

            var instances = new List<MovableInstance>(_movableInstances);
            instances.Sort((left, right) =>
            {
                int numeric = GetNumericInstanceId(left).CompareTo(GetNumericInstanceId(right));
                return numeric != 0
                    ? numeric
                    : GetDefinitionId(left).CompareTo(GetDefinitionId(right));
            });

            var specs = new List<CharacterTestMovableNaturalObjectSpawnSpec>();
            var numericIds = new HashSet<int>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < instances.Count; i++)
            {
                MovableInstance instance = instances[i];
                if (instance == null || !instance.IsEnabled)
                    continue;

                if (!instance.TryCreateSpawnSpec(
                        definitionProvider,
                        out CharacterTestMovableNaturalObjectSpawnSpec spec,
                        out string diagnostic))
                {
                    if (!string.IsNullOrEmpty(diagnostic))
                        localDiagnostics.Add(diagnostic);
                    continue;
                }

                AddMovableSpecIfUnique(spec, specs, numericIds, stableIds, localDiagnostics, "world-object-source");
            }

            diagnostics = localDiagnostics.ToArray();
            _lastDiagnostics = diagnostics;
            return specs.ToArray();
        }

        public CharacterTestCharacterSpawnSpec[] CollectCharacterSpawnSpecs(
            out string[] diagnostics)
        {
            return CollectCharacterSpawnSpecs(BuiltInDefinitionCatalog, out diagnostics);
        }

        public CharacterTestCharacterSpawnSpec[] CollectCharacterSpawnSpecs(
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out string[] diagnostics)
        {
            var localDiagnostics = new List<string>();
            if (!_enabled || _pointInstances == null || _pointInstances.Length == 0)
            {
                localDiagnostics.Add("world-object-source.points.missing-player-spawn");
                localDiagnostics.Add("world-object-source.points.missing-ai-spawn");
                diagnostics = localDiagnostics.ToArray();
                _lastDiagnostics = diagnostics;
                return Array.Empty<CharacterTestCharacterSpawnSpec>();
            }

            var instances = new List<LevelPointInstance>(_pointInstances);
            instances.Sort((left, right) =>
            {
                int definition = GetDefinitionId(left).CompareTo(GetDefinitionId(right));
                return definition != 0
                    ? definition
                    : string.CompareOrdinal(GetStableId(left), GetStableId(right));
            });

            var specs = new List<CharacterTestCharacterSpawnSpec>();
            bool hasPlayerSpawn = false;
            int aiSpawnCount = 0;
            for (int i = 0; i < instances.Count; i++)
            {
                LevelPointInstance instance = instances[i];
                if (instance == null || !instance.IsEnabled)
                    continue;

                if (!instance.TryCreateCharacterSpawnSpec(
                        definitionProvider,
                        out CharacterTestCharacterSpawnSpec spec,
                        out LevelWorldObjectPointKind pointKind,
                        out string diagnostic))
                {
                    if (!string.IsNullOrEmpty(diagnostic))
                        localDiagnostics.Add(diagnostic);
                    continue;
                }

                if (pointKind == LevelWorldObjectPointKind.PlayerSpawn)
                    hasPlayerSpawn = true;
                else if (pointKind == LevelWorldObjectPointKind.AiSpawn)
                    aiSpawnCount++;
                specs.Add(spec);
            }

            if (!hasPlayerSpawn)
                localDiagnostics.Add("world-object-source.points.missing-player-spawn");
            if (aiSpawnCount == 0)
                localDiagnostics.Add("world-object-source.points.missing-ai-spawn");

            diagnostics = localDiagnostics.ToArray();
            _lastDiagnostics = diagnostics;
            return specs.ToArray();
        }

        public CharacterTestWorldObjectStaticColliderSpec[] CollectStaticColliderSpecs(
            out string[] diagnostics)
        {
            return CollectStaticColliderSpecs(BuiltInDefinitionCatalog, out diagnostics);
        }

        public CharacterTestWorldObjectStaticColliderSpec[] CollectStaticColliderSpecs(
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out string[] diagnostics)
        {
            var localDiagnostics = new List<string>();
            if (!_enabled || _staticColliderInstances == null || _staticColliderInstances.Length == 0)
            {
                diagnostics = Array.Empty<string>();
                return Array.Empty<CharacterTestWorldObjectStaticColliderSpec>();
            }

            var specs = new List<CharacterTestWorldObjectStaticColliderSpec>();
            for (int i = 0; i < _staticColliderInstances.Length; i++)
            {
                StaticColliderInstance instance = _staticColliderInstances[i];
                if (instance == null || !instance.IsEnabled)
                    continue;

                if (!instance.TryCreateSpec(
                        definitionProvider,
                        out CharacterTestWorldObjectStaticColliderSpec spec,
                        out string diagnostic))
                {
                    if (!string.IsNullOrEmpty(diagnostic))
                        localDiagnostics.Add(diagnostic);
                    continue;
                }

                specs.Add(spec);
            }

            specs.Sort((left, right) => string.CompareOrdinal(left.SortKey, right.SortKey));
            diagnostics = localDiagnostics.ToArray();
            _lastDiagnostics = diagnostics;
            return specs.ToArray();
        }

        public static CharacterTestWorldObjectSceneSource FindFor(Transform root)
        {
            if (root != null)
            {
                CharacterTestWorldObjectSceneSource local = root.GetComponentInChildren<CharacterTestWorldObjectSceneSource>(
                    includeInactive: true);
                if (local != null && local.Enabled)
                    return local;
            }

            CharacterTestWorldObjectSceneSource[] sources =
                UnityEngine.Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            for (int i = 0; i < sources.Length; i++)
            {
                CharacterTestWorldObjectSceneSource source = sources[i];
                if (source != null && source.Enabled)
                    return source;
            }

            return null;
        }

        public static string ResolveTransformPath(Transform root, Transform target)
        {
            if (target == null)
                return "missing";

            var segments = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                segments.Add(current.name + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }

            if (segments.Count == 0)
                return target.name + "[" + target.GetSiblingIndex() + "]";

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private static bool AddMovableSpecIfUnique(
            CharacterTestMovableNaturalObjectSpawnSpec spec,
            List<CharacterTestMovableNaturalObjectSpawnSpec> specs,
            HashSet<int> numericIds,
            HashSet<string> stableIds,
            List<string> diagnostics,
            string source)
        {
            if (spec.NumericId <= 0 || string.IsNullOrWhiteSpace(spec.StableId))
                return false;
            if (!numericIds.Add(spec.NumericId))
            {
                diagnostics?.Add(source + ".duplicate-numeric-id:" + spec.NumericId);
                return false;
            }
            if (!stableIds.Add(spec.StableId))
            {
                diagnostics?.Add(source + ".duplicate-stable-id:" + spec.StableId);
                return false;
            }

            specs.Add(spec);
            return true;
        }

        private static int GetNumericInstanceId(MovableInstance instance)
        {
            return instance == null ? 0 : instance.NumericInstanceId;
        }

        private static int GetDefinitionId(MovableInstance instance)
        {
            return instance == null ? 0 : instance.DefinitionId;
        }

        private static int GetDefinitionId(LevelPointInstance instance)
        {
            return instance == null ? 0 : instance.DefinitionId;
        }

        private static string GetStableId(LevelPointInstance instance)
        {
            return instance == null ? string.Empty : instance.StableId;
        }

        private static T[] ToArray<T>(IEnumerable<T> values)
        {
            if (values == null)
                return Array.Empty<T>();

            var list = new List<T>(values);
            return list.ToArray();
        }

        private static string NormalizeAuthoringString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        [Serializable]
        public sealed class MovableInstance
        {
            [SerializeField] private bool _enabled = true;
            [SerializeField] private int _definitionId;
            [SerializeField] private int _numericInstanceId;
            [SerializeField] private string _instanceStableId = "";
            [SerializeField] private Transform _transform;
            [SerializeField] private CharacterTestWorldObjectInstanceScaleSource _scaleSource =
                CharacterTestWorldObjectInstanceScaleSource.SceneTransform;

            public MovableInstance()
            {
            }

            public MovableInstance(
                bool enabled,
                int definitionId,
                int numericInstanceId,
                string instanceStableId,
                Transform transform,
                CharacterTestWorldObjectInstanceScaleSource scaleSource)
            {
                _enabled = enabled;
                _definitionId = Mathf.Max(0, definitionId);
                _numericInstanceId = Mathf.Max(0, numericInstanceId);
                _instanceStableId = instanceStableId ?? string.Empty;
                _transform = transform;
                _scaleSource = NormalizeScaleSource(scaleSource);
            }

            public bool IsEnabled => _enabled;

            public int DefinitionId => _definitionId;

            public int NumericInstanceId => _numericInstanceId;

            public string InstanceStableId => string.IsNullOrWhiteSpace(_instanceStableId)
                ? string.Empty
                : _instanceStableId.Trim();

            public Transform Transform => _transform;

            public CharacterTestWorldObjectInstanceScaleSource ScaleSource => _scaleSource;

            public bool TryCreateSpawnSpec(
                ICharacterTestWorldObjectDefinitionProvider definitionProvider,
                out CharacterTestMovableNaturalObjectSpawnSpec spec,
                out string diagnostic)
            {
                spec = default;
                diagnostic = string.Empty;
                if (!_enabled)
                    return false;
                if (_transform == null)
                {
                    diagnostic = "world-object-source.movable.missing-transform:" + _numericInstanceId;
                    return false;
                }
                if (definitionProvider == null)
                {
                    diagnostic = "world-object-source.movable.missing-definition-provider";
                    return false;
                }
                if (_definitionId <= 0)
                {
                    diagnostic = "world-object-source.movable.invalid-definition-id";
                    return false;
                }
                if (_numericInstanceId <= 0)
                {
                    diagnostic = "world-object-source.movable.invalid-numeric-id";
                    return false;
                }
                if (!definitionProvider.TryGetDefinition(
                        _definitionId,
                        out CharacterTestWorldObjectDefinition definition))
                {
                    diagnostic = "world-object-source.movable.definition-not-found:" + _definitionId;
                    return false;
                }
                if (!definition.CanSpawnMovableNaturalObject)
                {
                    diagnostic = "world-object-source.movable.definition-not-movable:" + _definitionId;
                    return false;
                }

                Vector3 halfExtents = ResolveHalfExtents(definition, _transform, _scaleSource);
                return definition.TryCreateMovableNaturalObjectSpawnSpec(
                    _numericInstanceId,
                    InstanceStableId,
                    CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.x),
                    CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.y),
                    CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.z),
                    CharacterTestDeterministicUnits.MetersToMillimeters(_transform.position.x),
                    CharacterTestDeterministicUnits.MetersToMillimeters(_transform.position.y),
                    CharacterTestDeterministicUnits.MetersToMillimeters(_transform.position.z),
                    out spec);
            }
        }

        [Serializable]
        public sealed class StaticColliderInstance
        {
            [SerializeField] private bool _enabled = true;
            [SerializeField] private int _definitionId;
            [SerializeField] private Transform _transform;
            [SerializeField] private int _bodyId;
            [SerializeField] private int _colliderId;
            [SerializeField] private CharacterTestWorldObjectInstanceScaleSource _scaleSource =
                CharacterTestWorldObjectInstanceScaleSource.SceneTransform;

            public StaticColliderInstance()
            {
            }

            public StaticColliderInstance(
                bool enabled,
                int definitionId,
                Transform transform,
                int bodyId,
                int colliderId,
                CharacterTestWorldObjectInstanceScaleSource scaleSource)
            {
                _enabled = enabled;
                _definitionId = Mathf.Max(0, definitionId);
                _transform = transform;
                _bodyId = Mathf.Max(0, bodyId);
                _colliderId = Mathf.Max(0, colliderId);
                _scaleSource = NormalizeScaleSource(scaleSource);
            }

            public bool IsEnabled => _enabled;

            public int DefinitionId => _definitionId;

            public Transform Transform => _transform;

            public int BodyId => _bodyId;

            public int ColliderId => _colliderId;

            public CharacterTestWorldObjectInstanceScaleSource ScaleSource => _scaleSource;

            public bool TryCreateSpec(
                ICharacterTestWorldObjectDefinitionProvider definitionProvider,
                out CharacterTestWorldObjectStaticColliderSpec spec,
                out string diagnostic)
            {
                spec = default;
                diagnostic = string.Empty;
                if (!_enabled)
                    return false;
                if (_transform == null)
                {
                    diagnostic = "world-object-source.static.missing-transform:" + _definitionId;
                    return false;
                }
                if (definitionProvider == null)
                {
                    diagnostic = "world-object-source.static.missing-definition-provider";
                    return false;
                }
                if (_definitionId <= 0)
                {
                    diagnostic = "world-object-source.static.invalid-definition-id";
                    return false;
                }
                if (!definitionProvider.TryGetDefinition(
                        _definitionId,
                        out CharacterTestWorldObjectDefinition definition))
                {
                    diagnostic = "world-object-source.static.definition-not-found:" + _definitionId;
                    return false;
                }
                if (!definition.CanSpawnStaticCollider)
                {
                    diagnostic = "world-object-source.static.definition-not-static:" + _definitionId;
                    return false;
                }

                Vector3 halfExtents = definition.Shape.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb
                    ? new Vector3(
                        CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentXMillimeters),
                        CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentYMillimeters),
                        CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentZMillimeters))
                    : ResolveHalfExtents(definition, _transform, _scaleSource);
                spec = CharacterTestWorldObjectStaticColliderSpec.FromDefinition(
                    definition,
                    _transform,
                    _bodyId,
                    _colliderId > 0 ? _colliderId : definition.StaticCollider.ColliderId,
                    definition.StaticCollider.CombatLayer,
                    _scaleSource,
                    halfExtents);
                return true;
            }
        }

        [Serializable]
        public sealed class LevelPointInstance
        {
            [SerializeField] private bool _enabled = true;
            [SerializeField] private int _definitionId;
            [SerializeField] private string _stableId = "";
            [SerializeField] private Transform _transform;

            public LevelPointInstance()
            {
            }

            public LevelPointInstance(
                bool enabled,
                int definitionId,
                string stableId,
                Transform transform)
            {
                _enabled = enabled;
                _definitionId = Mathf.Max(0, definitionId);
                _stableId = stableId ?? string.Empty;
                _transform = transform;
            }

            public bool IsEnabled => _enabled;

            public int DefinitionId => _definitionId;

            public string StableId => string.IsNullOrWhiteSpace(_stableId)
                ? string.Empty
                : _stableId.Trim();

            public Transform Transform => _transform;

            public bool TryCreateCharacterSpawnSpec(
                ICharacterTestWorldObjectDefinitionProvider definitionProvider,
                out CharacterTestCharacterSpawnSpec spec,
                out LevelWorldObjectPointKind pointKind,
                out string diagnostic)
            {
                spec = default;
                pointKind = LevelWorldObjectPointKind.Unknown;
                diagnostic = string.Empty;
                if (!_enabled)
                    return false;
                if (_transform == null)
                {
                    diagnostic = "world-object-source.point.missing-transform:" + _definitionId;
                    return false;
                }
                if (definitionProvider == null)
                {
                    diagnostic = "world-object-source.point.missing-definition-provider";
                    return false;
                }
                if (!definitionProvider.TryGetDefinition(
                        _definitionId,
                        out CharacterTestWorldObjectDefinition definition))
                {
                    diagnostic = "world-object-source.point.definition-not-found:" + _definitionId;
                    return false;
                }
                if (!definition.CanSpawnPoint)
                {
                    diagnostic = "world-object-source.point.definition-not-point:" + _definitionId;
                    return false;
                }

                pointKind = definition.Point.PointKind;
                // Checkpoints and markers share the world-object point owner, but they are
                // not character spawn requests. Skipping them is expected and must not turn
                // a valid mixed-purpose point catalog into a runtime diagnostic failure.
                if (pointKind == LevelWorldObjectPointKind.Checkpoint
                    || pointKind == LevelWorldObjectPointKind.Marker)
                {
                    return false;
                }

                if (pointKind != LevelWorldObjectPointKind.PlayerSpawn
                    && pointKind != LevelWorldObjectPointKind.AiSpawn)
                {
                    diagnostic = "world-object-source.point.not-character-spawn:" + _definitionId;
                    return false;
                }

                Vector3 position = _transform.position;
                spec = new CharacterTestCharacterSpawnSpec(
                    definition.Point.CharacterId,
                    definition.Point.TeamId,
                    position.x,
                    position.y,
                    position.z,
                    definition.Point.ControlOwner);
                return true;
            }
        }

        [Serializable]
        public sealed class LevelRegionInstance
        {
            [SerializeField] private bool _enabled = true;
            [SerializeField] private int _definitionId;
            [SerializeField] private string _stableId = "";
            [SerializeField] private Transform _transform;
            [SerializeField] private CharacterTestWorldObjectInstanceScaleSource _scaleSource =
                CharacterTestWorldObjectInstanceScaleSource.SceneTransform;

            public LevelRegionInstance()
            {
            }

            public LevelRegionInstance(
                bool enabled,
                int definitionId,
                string stableId,
                Transform transform,
                CharacterTestWorldObjectInstanceScaleSource scaleSource)
            {
                _enabled = enabled;
                _definitionId = Mathf.Max(0, definitionId);
                _stableId = stableId ?? string.Empty;
                _transform = transform;
                _scaleSource = NormalizeScaleSource(scaleSource);
            }

            public bool IsEnabled => _enabled;

            public int DefinitionId => _definitionId;

            public string StableId => string.IsNullOrWhiteSpace(_stableId)
                ? string.Empty
                : _stableId.Trim();

            public Transform Transform => _transform;

            public CharacterTestWorldObjectInstanceScaleSource ScaleSource => _scaleSource;
        }

        private static Vector3 ResolveHalfExtents(
            CharacterTestWorldObjectDefinition definition,
            Transform transform,
            CharacterTestWorldObjectInstanceScaleSource scaleSource)
        {
            if (scaleSource == CharacterTestWorldObjectInstanceScaleSource.Definition)
            {
                return new Vector3(
                    CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentXMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentYMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentZMillimeters));
            }

            Vector3 scale = transform == null ? Vector3.one : transform.lossyScale;
            return new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(scale.x) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.y) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.z) * 0.5f));
        }

        private static CharacterTestWorldObjectInstanceScaleSource NormalizeScaleSource(
            CharacterTestWorldObjectInstanceScaleSource source)
        {
            return source == CharacterTestWorldObjectInstanceScaleSource.Definition
                ? CharacterTestWorldObjectInstanceScaleSource.Definition
                : CharacterTestWorldObjectInstanceScaleSource.SceneTransform;
        }
    }

    public readonly struct CharacterTestWorldObjectStaticColliderSpec
    {
        private CharacterTestWorldObjectStaticColliderSpec(
            int definitionId,
            string definitionStableId,
            Transform transform,
            CharacterTestMovableNaturalObjectShapeKind shapeKind,
            int bodyId,
            int colliderId,
            int layer,
            CombatSurfaceMaterial surfaceMaterial,
            CharacterTestWorldObjectInstanceScaleSource scaleSource,
            int definitionHalfExtentXMillimeters,
            int definitionHalfExtentYMillimeters,
            int definitionHalfExtentZMillimeters,
            Vector3 halfExtents)
        {
            DefinitionId = definitionId;
            DefinitionStableId = definitionStableId ?? string.Empty;
            Transform = transform;
            ShapeKind = CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(shapeKind);
            BodyId = Math.Max(0, bodyId);
            ColliderId = colliderId > 0 ? colliderId : 1;
            Layer = Math.Max(0, Math.Min(31, layer));
            SurfaceMaterial = surfaceMaterial;
            ScaleSource = scaleSource;
            DefinitionHalfExtentXMillimeters = definitionHalfExtentXMillimeters;
            DefinitionHalfExtentYMillimeters = definitionHalfExtentYMillimeters;
            DefinitionHalfExtentZMillimeters = definitionHalfExtentZMillimeters;
            HalfExtents = MaxVector3(halfExtents, new Vector3(0.01f, 0.01f, 0.01f));
            SortKey = CharacterTestWorldObjectSceneSource.ResolveTransformPath(null, transform);
        }

        public int DefinitionId { get; }

        public string DefinitionStableId { get; }

        public Transform Transform { get; }

        public CharacterTestMovableNaturalObjectShapeKind ShapeKind { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int Layer { get; }

        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public CharacterTestWorldObjectInstanceScaleSource ScaleSource { get; }

        public int DefinitionHalfExtentXMillimeters { get; }

        public int DefinitionHalfExtentYMillimeters { get; }

        public int DefinitionHalfExtentZMillimeters { get; }

        public Vector3 HalfExtents { get; }

        public string SortKey { get; }

        public Vector3 Position => Transform == null ? Vector3.zero : Transform.position;

        public bool HasExplicitBodyId => BodyId > 0;

        public float SphereRadius =>
            Mathf.Max(0.01f, Mathf.Max(HalfExtents.x, Mathf.Max(HalfExtents.y, HalfExtents.z)));

        public float CapsuleRadius => Mathf.Max(0.01f, Mathf.Max(HalfExtents.x, HalfExtents.z));

        public float CapsuleHalfHeight
        {
            get
            {
                if (Transform != null)
                    return Mathf.Max(CapsuleRadius, Mathf.Abs(Transform.lossyScale.y));

                return Mathf.Max(CapsuleRadius, HalfExtents.y);
            }
        }

        public Vector3 CapsuleWorldPointA
        {
            get
            {
                Vector3 axis = Transform == null ? Vector3.up : Transform.rotation * Vector3.up;
                return Position - axis * CapsuleSegmentHalfLength;
            }
        }

        public Vector3 CapsuleWorldPointB
        {
            get
            {
                Vector3 axis = Transform == null ? Vector3.up : Transform.rotation * Vector3.up;
                return Position + axis * CapsuleSegmentHalfLength;
            }
        }

        private float CapsuleSegmentHalfLength => Mathf.Max(0f, CapsuleHalfHeight - CapsuleRadius);

        public static CharacterTestWorldObjectStaticColliderSpec FromDefinition(
            CharacterTestWorldObjectDefinition definition,
            Transform transform,
            int bodyId,
            int colliderId,
            int layer,
            CharacterTestWorldObjectInstanceScaleSource scaleSource,
            Vector3 halfExtents)
        {
            return new CharacterTestWorldObjectStaticColliderSpec(
                definition.DefinitionId,
                definition.StableId,
                transform,
                definition.Shape.ShapeKind,
                bodyId,
                colliderId,
                layer,
                definition.Material.Material,
                scaleSource,
                definition.Shape.HalfExtentXMillimeters,
                definition.Shape.HalfExtentYMillimeters,
                definition.Shape.HalfExtentZMillimeters,
                halfExtents);
        }

        private static Vector3 MaxVector3(Vector3 value, Vector3 min)
        {
            return new Vector3(
                Mathf.Max(value.x, min.x),
                Mathf.Max(value.y, min.y),
                Mathf.Max(value.z, min.z));
        }
    }
}
