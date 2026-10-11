using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Movable Natural Object Scene Source")]
    public sealed class CharacterTestMovableNaturalObjectSceneSource : MonoBehaviour
    {
        private static readonly CharacterTestWorldObjectDefinitionCatalog BuiltInDefinitionCatalog =
            CharacterTestWorldObjectDefinitionCatalogSource.Catalog;

        [SerializeField] private bool _enabled = true;
        private string[] _lastCollectionDiagnostics = Array.Empty<string>();

        public bool Enabled => _enabled;

        public IReadOnlyList<string> LastCollectionDiagnostics => _lastCollectionDiagnostics;

        public CharacterTestMovableNaturalObjectSpawnSpec[] CollectSpawnSpecs()
        {
            var diagnostics = new List<string>();
            if (!_enabled)
            {
                _lastCollectionDiagnostics = Array.Empty<string>();
                return Array.Empty<CharacterTestMovableNaturalObjectSpawnSpec>();
            }

            CharacterTestWorldObjectSceneSource worldObjectSource =
                CharacterTestWorldObjectSceneSource.FindFor(transform);
            if (worldObjectSource != null && worldObjectSource.MovableInstanceCount > 0)
            {
                CharacterTestMovableNaturalObjectSpawnSpec[] sourcedSpecs =
                    worldObjectSource.CollectMovableSpawnSpecs(BuiltInDefinitionCatalog, out string[] sourceDiagnostics);
                diagnostics.AddRange(sourceDiagnostics);
                _lastCollectionDiagnostics = diagnostics.ToArray();
                return sourcedSpecs;
            }

            var specs = new List<CharacterTestMovableNaturalObjectSpawnSpec>();
            var numericIds = new HashSet<int>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);

            var instanceMarkers = new List<CharacterTestWorldObjectInstanceMarker>();
            GetComponentsInChildren(includeInactive: true, instanceMarkers);
            instanceMarkers.Sort((left, right) =>
            {
                int numeric = left.NumericInstanceId.CompareTo(right.NumericInstanceId);
                return numeric != 0
                    ? numeric
                    : left.DefinitionId.CompareTo(right.DefinitionId);
            });

            for (int i = 0; i < instanceMarkers.Count; i++)
            {
                CharacterTestWorldObjectInstanceMarker marker = instanceMarkers[i];
                if (marker == null || !marker.IsEnabled)
                    continue;

                if (!marker.TryCreateMovableNaturalObjectSpawnSpec(
                        BuiltInDefinitionCatalog,
                        out CharacterTestMovableNaturalObjectSpawnSpec spec,
                        out string diagnostic))
                {
                    if (!string.IsNullOrEmpty(diagnostic))
                        diagnostics.Add(diagnostic);
                    continue;
                }

                AddSpecIfUnique(spec, specs, numericIds, stableIds, diagnostics, "instance-marker");
            }

            var markers = new List<CharacterTestMovableNaturalObjectMarker>();
            GetComponentsInChildren(includeInactive: true, markers);
            markers.Sort((left, right) =>
            {
                int numeric = left.NumericId.CompareTo(right.NumericId);
                return numeric != 0
                    ? numeric
                    : string.CompareOrdinal(left.StableId, right.StableId);
            });

            for (int i = 0; i < markers.Count; i++)
            {
                CharacterTestMovableNaturalObjectMarker marker = markers[i];
                if (marker == null || !marker.IsEnabled || marker.NumericId <= 0)
                    continue;

                diagnostics.Add("legacy-movable-marker:" + marker.StableId);
                AddSpecIfUnique(
                    marker.CreateSpawnSpec(),
                    specs,
                    numericIds,
                    stableIds,
                    diagnostics,
                    "legacy-marker");
            }

            var fallbackTransforms = new List<Transform>();
            CharacterTestMovableNaturalObjectSceneFallback.CollectNamedTransforms(transform, fallbackTransforms);
            fallbackTransforms.Sort((left, right) =>
            {
                CharacterTestMovableNaturalObjectSceneFallback.TryGetDefaults(
                    CharacterTestMovableNaturalObjectSceneFallback.ParseStableId(left.name),
                    out int leftNumeric,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _);
                CharacterTestMovableNaturalObjectSceneFallback.TryGetDefaults(
                    CharacterTestMovableNaturalObjectSceneFallback.ParseStableId(right.name),
                    out int rightNumeric,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _);
                int numeric = leftNumeric.CompareTo(rightNumeric);
                return numeric != 0
                    ? numeric
                    : string.CompareOrdinal(left.name, right.name);
            });

            for (int i = 0; i < fallbackTransforms.Count; i++)
            {
                Transform candidate = fallbackTransforms[i];
                if (!CharacterTestMovableNaturalObjectSceneFallback.TryCreateSpawnSpec(
                        BuiltInDefinitionCatalog,
                        candidate,
                        out CharacterTestMovableNaturalObjectSpawnSpec spec))
                    continue;

                diagnostics.Add("legacy-name-fallback:" + spec.StableId);
                AddSpecIfUnique(
                    spec,
                    specs,
                    numericIds,
                    stableIds,
                    diagnostics,
                    "legacy-name-fallback");
            }

            _lastCollectionDiagnostics = diagnostics.ToArray();
            return specs.ToArray();
        }

        private static bool AddSpecIfUnique(
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
    }

    public sealed class CharacterTestMovableNaturalObjectUnityViewBridge
    {
        private readonly Dictionary<int, Transform> _views;

        private CharacterTestMovableNaturalObjectUnityViewBridge(
            Dictionary<int, Transform> views)
        {
            _views = views ?? new Dictionary<int, Transform>();
        }

        public int MarkerCount => _views.Count;

        public static CharacterTestMovableNaturalObjectUnityViewBridge FromSceneSource(
            CharacterTestMovableNaturalObjectSceneSource source)
        {
            if (source == null || !source.Enabled)
                return new CharacterTestMovableNaturalObjectUnityViewBridge(
                    new Dictionary<int, Transform>());

            var map = new Dictionary<int, Transform>();
            CharacterTestWorldObjectSceneSource worldObjectSource =
                CharacterTestWorldObjectSceneSource.FindFor(source.transform);
            if (worldObjectSource != null && worldObjectSource.MovableInstanceCount > 0)
            {
                IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> instances =
                    worldObjectSource.MovableInstances;
                for (int i = 0; i < instances.Count; i++)
                {
                    CharacterTestWorldObjectSceneSource.MovableInstance instance = instances[i];
                    if (instance == null
                        || !instance.IsEnabled
                        || instance.NumericInstanceId <= 0
                        || instance.Transform == null
                        || map.ContainsKey(instance.NumericInstanceId))
                    {
                        continue;
                    }

                    map.Add(instance.NumericInstanceId, instance.Transform);
                }

                return new CharacterTestMovableNaturalObjectUnityViewBridge(map);
            }

            var instanceMarkers = new List<CharacterTestWorldObjectInstanceMarker>();
            source.GetComponentsInChildren(includeInactive: true, instanceMarkers);
            for (int i = 0; i < instanceMarkers.Count; i++)
            {
                CharacterTestWorldObjectInstanceMarker marker = instanceMarkers[i];
                if (marker == null
                    || !marker.IsEnabled
                    || marker.NumericInstanceId <= 0
                    || map.ContainsKey(marker.NumericInstanceId))
                    continue;

                map.Add(marker.NumericInstanceId, marker.transform);
            }

            var markers = new List<CharacterTestMovableNaturalObjectMarker>();
            source.GetComponentsInChildren(includeInactive: true, markers);
            for (int i = 0; i < markers.Count; i++)
            {
                CharacterTestMovableNaturalObjectMarker marker = markers[i];
                if (marker == null || !marker.IsEnabled || marker.NumericId <= 0 || map.ContainsKey(marker.NumericId))
                    continue;

                map.Add(marker.NumericId, marker.transform);
            }

            var fallbackTransforms = new List<Transform>();
            CharacterTestMovableNaturalObjectSceneFallback.CollectNamedTransforms(source.transform, fallbackTransforms);
            for (int i = 0; i < fallbackTransforms.Count; i++)
            {
                Transform candidate = fallbackTransforms[i];
                string stableId = CharacterTestMovableNaturalObjectSceneFallback.ParseStableId(candidate.name);
                if (!CharacterTestMovableNaturalObjectSceneFallback.TryGetDefaults(
                        stableId,
                        out int numericId,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _))
                    continue;
                if (numericId <= 0 || map.ContainsKey(numericId))
                    continue;

                map.Add(numericId, candidate);
            }

            return new CharacterTestMovableNaturalObjectUnityViewBridge(map);
        }

        public static CharacterTestMovableNaturalObjectUnityViewBridge FromSceneCache(
            CharacterTestWorldObjectSceneCache cache)
        {
            if (cache == null || !cache.Enabled)
                return new CharacterTestMovableNaturalObjectUnityViewBridge(
                    new Dictionary<int, Transform>());

            var map = new Dictionary<int, Transform>();
            IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> instances =
                cache.MovableInstances;
            for (int i = 0; i < instances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.MovableInstance instance = instances[i];
                if (instance == null
                    || !instance.IsEnabled
                    || instance.NumericInstanceId <= 0
                    || instance.Transform == null
                    || map.ContainsKey(instance.NumericInstanceId))
                {
                    continue;
                }

                map.Add(instance.NumericInstanceId, instance.Transform);
            }

            return new CharacterTestMovableNaturalObjectUnityViewBridge(map);
        }

        public int Sync(IReadOnlyList<CharacterTestMovableNaturalObjectState> states)
        {
            if (states == null || states.Count == 0 || _views.Count == 0)
                return 0;

            int synced = 0;
            for (int i = 0; i < states.Count; i++)
            {
                CharacterTestMovableNaturalObjectState state = states[i];
                if (state == null || !_views.TryGetValue(state.NumericId, out Transform view) || view == null)
                    continue;

                view.position = state.Position;
                synced++;
            }

            return synced;
        }
    }

    internal static class CharacterTestMovableNaturalObjectSceneFallback
    {
        private const string NamePrefix = "MovableNaturalObject_";
        private const string NameSuffix = "_visual";

        public static void CollectNamedTransforms(Transform root, List<Transform> results)
        {
            if (root == null || results == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!string.IsNullOrEmpty(ParseStableId(child.name)))
                    results.Add(child);

                CollectNamedTransforms(child, results);
            }
        }

        public static string ParseStableId(string name)
        {
            if (string.IsNullOrEmpty(name)
                || !name.StartsWith(NamePrefix, StringComparison.Ordinal)
                || !name.EndsWith(NameSuffix, StringComparison.Ordinal))
                return string.Empty;

            return name.Substring(NamePrefix.Length, name.Length - NamePrefix.Length - NameSuffix.Length);
        }

        public static bool TryCreateSpawnSpec(
            ICharacterTestWorldObjectDefinitionProvider definitionProvider,
            Transform transform,
            out CharacterTestMovableNaturalObjectSpawnSpec spec)
        {
            spec = default;
            if (transform == null)
                return false;
            if (definitionProvider == null)
                return false;

            string stableId = ParseStableId(transform.name);
            if (!definitionProvider.TryGetDefinitionByStableId(
                    stableId,
                    out CharacterTestWorldObjectDefinition definition))
                return false;
            if (!definition.CanSpawnMovableNaturalObject)
                return false;

            Vector3 scale = transform.lossyScale;
            Vector3 halfExtents = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(scale.x) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.y) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.z) * 0.5f));
            return definition.TryCreateMovableNaturalObjectSpawnSpec(
                definition.DefinitionId,
                stableId,
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(halfExtents.z),
                CharacterTestDeterministicUnits.MetersToMillimeters(transform.position.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(transform.position.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(transform.position.z),
                out spec);
        }

        public static bool TryCreateSpawnSpec(
            Transform transform,
            out CharacterTestMovableNaturalObjectSpawnSpec spec)
        {
            return TryCreateSpawnSpec(
                CharacterTestWorldObjectDefinitionCatalogSource.Catalog,
                transform,
                out spec);
        }

        public static bool TryGetDefaults(
            string stableId,
            out int numericId,
            out float massKg,
            out CombatSurfaceMaterialKind materialKind,
            out float friction,
            out float elasticity,
            out float fixedBounceHeight)
        {
            numericId = 0;
            massKg = 0f;
            materialKind = CombatSurfaceMaterialKind.Default;
            friction = 0f;
            elasticity = 0f;
            fixedBounceHeight = 0f;

            CharacterTestWorldObjectDefinitionCatalog catalog =
                CharacterTestWorldObjectDefinitionCatalogSource.Catalog;
            if (!catalog.TryGetDefinitionByStableId(stableId, out CharacterTestWorldObjectDefinition definition)
                || !definition.CanSpawnMovableNaturalObject)
                return false;

            numericId = definition.DefinitionId;
            massKg = CharacterTestDeterministicUnits.GramsToKilograms(
                definition.MovableNaturalObject.MassGrams);
            materialKind = definition.Material.Kind;
            friction = ToFloat(definition.Material.Friction);
            elasticity = ToFloat(definition.Material.Elasticity);
            fixedBounceHeight = ToFloat(definition.Material.FixedBounceHeight);
            return true;
        }

        private static float ToFloat(MxFramework.Core.Math.Fix64 value)
        {
            return (float)((double)value.RawValue / MxFramework.Core.Math.Fix64.Scale);
        }
    }
}
