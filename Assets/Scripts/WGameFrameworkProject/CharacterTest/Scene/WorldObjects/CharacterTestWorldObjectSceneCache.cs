using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [Serializable]
    public sealed class CharacterTestWorldObjectSceneCache
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _authoringJsonPath = "";
        [SerializeField] private string _authoringContentHash = "";
        [SerializeField] private string _authoringLastAppliedUtc = "";
        [SerializeField] private CharacterTestWorldObjectSceneSource.MovableInstance[] _movableInstances =
            Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>();
        [SerializeField] private CharacterTestWorldObjectSceneSource.StaticColliderInstance[] _staticColliderInstances =
            Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>();
        [SerializeField] private CharacterTestWorldObjectSceneSource.LevelPointInstance[] _pointInstances =
            Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>();
        [SerializeField] private CharacterTestWorldObjectSceneSource.LevelRegionInstance[] _regionInstances =
            Array.Empty<CharacterTestWorldObjectSceneSource.LevelRegionInstance>();

        private string[] _lastDiagnostics = Array.Empty<string>();

        public bool Enabled => _enabled;

        public string AuthoringJsonPath => _authoringJsonPath ?? string.Empty;

        public string AuthoringContentHash => _authoringContentHash ?? string.Empty;

        public string AuthoringLastAppliedUtc => _authoringLastAppliedUtc ?? string.Empty;

        public IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> MovableInstances =>
            _movableInstances ?? Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>();

        public IReadOnlyList<CharacterTestWorldObjectSceneSource.StaticColliderInstance> StaticColliderInstances =>
            _staticColliderInstances ?? Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>();

        public IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance> PointInstances =>
            _pointInstances ?? Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>();

        public IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelRegionInstance> RegionInstances =>
            _regionInstances ?? Array.Empty<CharacterTestWorldObjectSceneSource.LevelRegionInstance>();

        public int MovableInstanceCount => _movableInstances == null ? 0 : _movableInstances.Length;

        public int StaticColliderInstanceCount =>
            _staticColliderInstances == null ? 0 : _staticColliderInstances.Length;

        public int PointInstanceCount => _pointInstances == null ? 0 : _pointInstances.Length;

        public int RegionInstanceCount => _regionInstances == null ? 0 : _regionInstances.Length;

        public IReadOnlyList<string> LastDiagnostics => _lastDiagnostics;

        public void Configure(
            IEnumerable<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticColliderInstances)
        {
            Configure(
                movableInstances,
                staticColliderInstances,
                Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                Array.Empty<CharacterTestWorldObjectSceneSource.LevelRegionInstance>());
        }

        public void Configure(
            IEnumerable<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticColliderInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances)
        {
            _movableInstances = ToArray(movableInstances);
            _staticColliderInstances = ToArray(staticColliderInstances);
            _pointInstances = ToArray(pointInstances);
            _regionInstances = ToArray(regionInstances);
        }

        public void ConfigureFromAuthoring(
            IEnumerable<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticColliderInstances,
            string authoringJsonPath,
            string authoringContentHash,
            string authoringLastAppliedUtc)
        {
            ConfigureFromAuthoring(
                movableInstances,
                staticColliderInstances,
                Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                Array.Empty<CharacterTestWorldObjectSceneSource.LevelRegionInstance>(),
                authoringJsonPath,
                authoringContentHash,
                authoringLastAppliedUtc);
        }

        public void ConfigureFromAuthoring(
            IEnumerable<CharacterTestWorldObjectSceneSource.MovableInstance> movableInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.StaticColliderInstance> staticColliderInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances,
            IEnumerable<CharacterTestWorldObjectSceneSource.LevelRegionInstance> regionInstances,
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
            return CollectMovableSpawnSpecs(CharacterTestWorldObjectSceneSource.BuiltInCatalog, out diagnostics);
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

            var instances = new List<CharacterTestWorldObjectSceneSource.MovableInstance>(_movableInstances);
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
                CharacterTestWorldObjectSceneSource.MovableInstance instance = instances[i];
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

                AddMovableSpecIfUnique(spec, specs, numericIds, stableIds, localDiagnostics, "world-object-cache");
            }

            diagnostics = localDiagnostics.ToArray();
            _lastDiagnostics = diagnostics;
            return specs.ToArray();
        }

        public CharacterTestCharacterSpawnSpec[] CollectCharacterSpawnSpecs(
            out string[] diagnostics)
        {
            return CollectCharacterSpawnSpecs(CharacterTestWorldObjectSceneSource.BuiltInCatalog, out diagnostics);
        }

        public CharacterTestCharacterSpawnSpec[] CollectCharacterSpawnSpecs(
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            out string[] diagnostics)
        {
            var localDiagnostics = new List<string>();
            if (!_enabled || _pointInstances == null || _pointInstances.Length == 0)
            {
                localDiagnostics.Add("world-object-cache.points.missing-player-spawn");
                localDiagnostics.Add("world-object-cache.points.missing-ai-spawn");
                diagnostics = localDiagnostics.ToArray();
                _lastDiagnostics = diagnostics;
                return Array.Empty<CharacterTestCharacterSpawnSpec>();
            }

            var instances = new List<CharacterTestWorldObjectSceneSource.LevelPointInstance>(_pointInstances);
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
                CharacterTestWorldObjectSceneSource.LevelPointInstance instance = instances[i];
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
                localDiagnostics.Add("world-object-cache.points.missing-player-spawn");
            if (aiSpawnCount == 0)
                localDiagnostics.Add("world-object-cache.points.missing-ai-spawn");

            diagnostics = localDiagnostics.ToArray();
            _lastDiagnostics = diagnostics;
            return specs.ToArray();
        }

        public CharacterTestWorldObjectStaticColliderSpec[] CollectStaticColliderSpecs(
            out string[] diagnostics)
        {
            return CollectStaticColliderSpecs(CharacterTestWorldObjectSceneSource.BuiltInCatalog, out diagnostics);
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
                CharacterTestWorldObjectSceneSource.StaticColliderInstance instance = _staticColliderInstances[i];
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

        private static int GetNumericInstanceId(CharacterTestWorldObjectSceneSource.MovableInstance instance)
        {
            return instance == null ? 0 : instance.NumericInstanceId;
        }

        private static int GetDefinitionId(CharacterTestWorldObjectSceneSource.MovableInstance instance)
        {
            return instance == null ? 0 : instance.DefinitionId;
        }

        private static int GetDefinitionId(CharacterTestWorldObjectSceneSource.LevelPointInstance instance)
        {
            return instance == null ? 0 : instance.DefinitionId;
        }

        private static string GetStableId(CharacterTestWorldObjectSceneSource.LevelPointInstance instance)
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
    }
}
