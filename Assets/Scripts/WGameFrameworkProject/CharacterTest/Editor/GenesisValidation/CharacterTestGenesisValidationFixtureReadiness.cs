using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestGenesisValidationFixtureState
    {
        Applied = 0,
        Unapplied = 1,
        Unresolved = 2
    }

    internal enum CharacterTestGenesisValidationFixtureKind
    {
        StaticCollider = 0,
        MovableObject = 1
    }

    internal sealed class CharacterTestGenesisValidationFixtureReadinessEntry
    {
        public CharacterTestGenesisValidationFixtureReadinessEntry(
            string stationId,
            string displayName,
            CharacterTestGenesisValidationFixtureKind kind,
            CharacterTestGenesisValidationFixtureState state,
            Transform transform,
            string detail)
        {
            StationId = stationId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            State = state;
            Transform = transform;
            Detail = detail ?? string.Empty;
        }

        public string StationId { get; }
        public string DisplayName { get; }
        public CharacterTestGenesisValidationFixtureKind Kind { get; }
        public CharacterTestGenesisValidationFixtureState State { get; }
        public Transform Transform { get; }
        public string Detail { get; }
    }

    internal sealed class CharacterTestGenesisValidationFixtureReadinessReport
    {
        private readonly List<CharacterTestGenesisValidationFixtureReadinessEntry> _entries =
            new List<CharacterTestGenesisValidationFixtureReadinessEntry>();

        public IReadOnlyList<CharacterTestGenesisValidationFixtureReadinessEntry> Entries => _entries;
        public int AppliedCount { get; private set; }
        public int UnappliedCount { get; private set; }
        public int UnresolvedCount { get; private set; }
        public bool BlocksPlay => UnappliedCount > 0 || UnresolvedCount > 0;

        public void Add(CharacterTestGenesisValidationFixtureReadinessEntry entry)
        {
            if (entry == null)
                return;

            _entries.Add(entry);
            switch (entry.State)
            {
                case CharacterTestGenesisValidationFixtureState.Applied:
                    AppliedCount++;
                    break;
                case CharacterTestGenesisValidationFixtureState.Unapplied:
                    UnappliedCount++;
                    break;
                case CharacterTestGenesisValidationFixtureState.Unresolved:
                    UnresolvedCount++;
                    break;
            }
        }
    }

    /// <summary>
    /// Editor-only, low-frequency projection from Genesis typed fixture references to the
    /// world-object owner's scene cache. It does not compile or mutate runtime data.
    /// </summary>
    internal static class CharacterTestGenesisValidationFixtureReadiness
    {
        public static CharacterTestGenesisValidationFixtureReadinessReport Evaluate(
            CharacterTestGenesisValidationAuthoringDocument document,
            LevelWorldObjectAuthoringDocument worldObjects,
            CharacterTestWorldObjectSceneCache cache,
            Transform root)
        {
            var report = new CharacterTestGenesisValidationFixtureReadinessReport();
            if (document?.Stations == null || document.Stations.Count == 0)
                return report;

            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (int stationIndex = 0; stationIndex < document.Stations.Count; stationIndex++)
            {
                CharacterTestGenesisValidationStation station = document.Stations[stationIndex];
                if (station == null || !station.Enabled || station.Bindings == null)
                    continue;

                AddStatic(
                    report,
                    visited,
                    station,
                    station.Bindings.TargetStaticCollider,
                    "目标边界",
                    worldObjects,
                    cache,
                    root);
                if (station.Bindings.SampleStaticColliders != null)
                {
                    for (int sampleIndex = 0;
                         sampleIndex < station.Bindings.SampleStaticColliders.Count;
                         sampleIndex++)
                    {
                        AddStatic(
                            report,
                            visited,
                            station,
                            station.Bindings.SampleStaticColliders[sampleIndex],
                            "表面样本 " + (sampleIndex + 1),
                            worldObjects,
                            cache,
                            root);
                    }
                }

                AddMovable(
                    report,
                    visited,
                    station,
                    station.Bindings.TargetMovableObject,
                    worldObjects,
                    cache,
                    root);
            }

            return report;
        }

        private static void AddStatic(
            CharacterTestGenesisValidationFixtureReadinessReport report,
            HashSet<string> visited,
            CharacterTestGenesisValidationStation station,
            CharacterTestGenesisValidationStaticColliderRef reference,
            string role,
            LevelWorldObjectAuthoringDocument worldObjects,
            CharacterTestWorldObjectSceneCache cache,
            Transform root)
        {
            if (reference == null)
                return;

            int colliderId = reference.ColliderId > 0 ? reference.ColliderId : 1;
            string identity = "static:"
                + reference.SourceKind
                + ":"
                + reference.BodyId
                + ":"
                + colliderId
                + ":"
                + reference.ExpectedDefinitionId;
            if (!visited.Add(identity))
                return;

            string displayName = ResolveStationName(station) + " · " + role;
            if (reference.SourceKind != CharacterTestGenesisValidationStaticSourceKind.SingleStatic
                || reference.BodyId <= 0
                || reference.ExpectedDefinitionId <= 0)
            {
                report.Add(CreateEntry(
                    station,
                    displayName,
                    CharacterTestGenesisValidationFixtureKind.StaticCollider,
                    CharacterTestGenesisValidationFixtureState.Unresolved,
                    null,
                    "静态夹具引用不是可解析的 single-static identity。"));
                return;
            }

            LevelWorldObjectInstanceDefinition owner = FindStaticOwner(worldObjects, reference);
            if (owner == null
                || root == null
                || !CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                    root,
                    owner.TransformRef,
                    out Transform transform))
            {
                report.Add(CreateEntry(
                    station,
                    displayName,
                    CharacterTestGenesisValidationFixtureKind.StaticCollider,
                    CharacterTestGenesisValidationFixtureState.Unresolved,
                    null,
                    "世界对象 owner 或 Scene Transform 无法解析。"));
                return;
            }

            bool applied = ContainsStatic(cache, reference, transform);
            report.Add(CreateEntry(
                station,
                displayName,
                CharacterTestGenesisValidationFixtureKind.StaticCollider,
                applied
                    ? CharacterTestGenesisValidationFixtureState.Applied
                    : CharacterTestGenesisValidationFixtureState.Unapplied,
                transform,
                applied
                    ? "已进入 world-object scene cache。"
                    : "尚未进入 world-object scene cache；当前没有框架碰撞。"));
        }

        private static void AddMovable(
            CharacterTestGenesisValidationFixtureReadinessReport report,
            HashSet<string> visited,
            CharacterTestGenesisValidationStation station,
            CharacterTestGenesisValidationMovableObjectRef reference,
            LevelWorldObjectAuthoringDocument worldObjects,
            CharacterTestWorldObjectSceneCache cache,
            Transform root)
        {
            string stableId = reference?.InstanceStableId?.Trim();
            if (string.IsNullOrEmpty(stableId) || !visited.Add("movable:" + stableId))
                return;

            string displayName = ResolveStationName(station) + " · 可动物目标";
            LevelWorldObjectInstanceDefinition owner = FindMovableOwner(worldObjects, stableId);
            if (owner == null
                || root == null
                || !CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                    root,
                    owner.TransformRef,
                    out Transform transform))
            {
                report.Add(CreateEntry(
                    station,
                    displayName,
                    CharacterTestGenesisValidationFixtureKind.MovableObject,
                    CharacterTestGenesisValidationFixtureState.Unresolved,
                    null,
                    "可动物 owner 或 Scene Transform 无法解析。"));
                return;
            }

            bool applied = ContainsMovable(cache, owner, stableId, transform);
            report.Add(CreateEntry(
                station,
                displayName,
                CharacterTestGenesisValidationFixtureKind.MovableObject,
                applied
                    ? CharacterTestGenesisValidationFixtureState.Applied
                    : CharacterTestGenesisValidationFixtureState.Unapplied,
                transform,
                applied
                    ? "已进入 world-object scene cache。"
                    : "尚未进入 world-object scene cache；当前没有框架可动物体。"));
        }

        private static CharacterTestGenesisValidationFixtureReadinessEntry CreateEntry(
            CharacterTestGenesisValidationStation station,
            string displayName,
            CharacterTestGenesisValidationFixtureKind kind,
            CharacterTestGenesisValidationFixtureState state,
            Transform transform,
            string detail)
        {
            return new CharacterTestGenesisValidationFixtureReadinessEntry(
                station?.StableId,
                displayName,
                kind,
                state,
                transform,
                detail);
        }

        private static LevelWorldObjectInstanceDefinition FindStaticOwner(
            LevelWorldObjectAuthoringDocument worldObjects,
            CharacterTestGenesisValidationStaticColliderRef reference)
        {
            if (worldObjects?.SingleStaticColliders == null)
                return null;

            int colliderId = reference.ColliderId > 0 ? reference.ColliderId : 1;
            for (int i = 0; i < worldObjects.SingleStaticColliders.Count; i++)
            {
                LevelWorldObjectInstanceDefinition candidate = worldObjects.SingleStaticColliders[i];
                if (candidate != null
                    && candidate.Enabled
                    && candidate.BodyId == reference.BodyId
                    && (candidate.ColliderId > 0 ? candidate.ColliderId : 1) == colliderId
                    && candidate.DefinitionId == reference.ExpectedDefinitionId)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static LevelWorldObjectInstanceDefinition FindMovableOwner(
            LevelWorldObjectAuthoringDocument worldObjects,
            string stableId)
        {
            if (worldObjects?.MovableInstances == null)
                return null;

            for (int i = 0; i < worldObjects.MovableInstances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition candidate = worldObjects.MovableInstances[i];
                if (candidate != null
                    && candidate.Enabled
                    && string.Equals(
                        candidate.InstanceStableId?.Trim(),
                        stableId,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool ContainsStatic(
            CharacterTestWorldObjectSceneCache cache,
            CharacterTestGenesisValidationStaticColliderRef reference,
            Transform transform)
        {
            if (cache == null || !cache.Enabled)
                return false;

            int colliderId = reference.ColliderId > 0 ? reference.ColliderId : 1;
            IReadOnlyList<CharacterTestWorldObjectSceneSource.StaticColliderInstance> instances =
                cache.StaticColliderInstances;
            for (int i = 0; i < instances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.StaticColliderInstance candidate = instances[i];
                if (candidate != null
                    && candidate.IsEnabled
                    && candidate.BodyId == reference.BodyId
                    && (candidate.ColliderId > 0 ? candidate.ColliderId : 1) == colliderId
                    && candidate.DefinitionId == reference.ExpectedDefinitionId
                    && candidate.Transform == transform)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMovable(
            CharacterTestWorldObjectSceneCache cache,
            LevelWorldObjectInstanceDefinition owner,
            string stableId,
            Transform transform)
        {
            if (cache == null || !cache.Enabled)
                return false;

            IReadOnlyList<CharacterTestWorldObjectSceneSource.MovableInstance> instances =
                cache.MovableInstances;
            for (int i = 0; i < instances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.MovableInstance candidate = instances[i];
                if (candidate != null
                    && candidate.IsEnabled
                    && string.Equals(candidate.InstanceStableId, stableId, StringComparison.Ordinal)
                    && candidate.DefinitionId == owner.DefinitionId
                    && candidate.NumericInstanceId == owner.NumericInstanceId
                    && candidate.Transform == transform)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveStationName(CharacterTestGenesisValidationStation station)
        {
            if (!string.IsNullOrWhiteSpace(station?.DisplayName))
                return station.DisplayName.Trim();
            return station?.StableId?.Trim() ?? "验证夹具";
        }
    }

    [InitializeOnLoad]
    internal static class CharacterTestGenesisValidationPlayModeGuard
    {
        private const string LogPrefix = "[CharacterTest.GenesisValidation] ";

        static CharacterTestGenesisValidationPlayModeGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static bool OwnsScene(
            MxLevelAuthoringDocument envelope,
            string activeScenePath)
        {
            string expectedScenePath = NormalizeScenePath(envelope?.ScenePath);
            string normalizedActiveScenePath = NormalizeScenePath(activeScenePath);
            return expectedScenePath.Length > 0
                && normalizedActiveScenePath.Length > 0
                && string.Equals(
                    expectedScenePath,
                    normalizedActiveScenePath,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryBuildBlockingMessage(
            CharacterTestGenesisValidationAuthoringDocument document,
            LevelWorldObjectAuthoringDocument worldObjects,
            CharacterTestWorldObjectSceneCache cache,
            Transform root,
            out string message,
            out Transform firstProblem)
        {
            CharacterTestGenesisValidationFixtureReadinessReport report =
                CharacterTestGenesisValidationFixtureReadiness.Evaluate(
                    document,
                    worldObjects,
                    cache,
                    root);
            if (!report.BlocksPlay)
            {
                message = string.Empty;
                firstProblem = null;
                return false;
            }

            firstProblem = null;
            for (int i = 0; i < report.Entries.Count; i++)
            {
                if (report.Entries[i].State == CharacterTestGenesisValidationFixtureState.Applied)
                    continue;
                firstProblem = report.Entries[i].Transform;
                break;
            }

            message = "已阻止进入 Play：创世验证场景有 "
                + report.UnappliedCount
                + " 个未应用夹具、"
                + report.UnresolvedCount
                + " 个引用失配夹具。请在 Mx Level Editor 中修复后点击保存。";
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
                return;

            try
            {
                if (!TryInspectActiveScene(out string message, out Transform firstProblem))
                    return;

                EditorApplication.isPlaying = false;
                if (firstProblem != null)
                {
                    Selection.activeTransform = firstProblem;
                    EditorGUIUtility.PingObject(firstProblem.gameObject);
                }
                Debug.LogError(LogPrefix + message, firstProblem);
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                EditorApplication.isPlaying = false;
                Debug.LogError(
                    LogPrefix
                    + "已阻止进入 Play：无法验证创世夹具的 world-object cache 状态。"
                    + exception.Message);
            }
        }

        private static bool TryInspectActiveScene(out string message, out Transform firstProblem)
        {
            message = string.Empty;
            firstProblem = null;
            GameManager manager = FindActiveSceneManager();
            if (manager == null || manager.WorldObjectSceneRoot == null)
                return false;

            string fullPath = ToFullPath(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            if (!File.Exists(fullPath))
                return false;

            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            if (!OwnsScene(envelope, EditorSceneManager.GetActiveScene().path))
                return false;
            if (!envelope.TryGetModule(
                    CharacterTestGenesisValidationAuthoringAdapter.ModuleId,
                    out MxLevelAuthoringModuleDocument _))
            {
                return false;
            }

            CharacterTestGenesisValidationAuthoringDocument document =
                CharacterTestGenesisValidationAuthoringAdapter.Extract(envelope);
            if (document.LoadErrors.Count > 0)
            {
                message = "已阻止进入 Play：创世验证 Authoring 无法读取。"
                    + document.LoadErrors[0];
                return true;
            }

            if (!CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                    envelope,
                    out LevelWorldObjectAuthoringDocument worldObjects,
                    out LevelWorldObjectAuthoringValidationReport worldReport)
                || worldReport.HasErrors)
            {
                message = "已阻止进入 Play：世界对象 Authoring 无法解析，不能确认夹具碰撞状态。";
                return true;
            }

            return TryBuildBlockingMessage(
                document,
                worldObjects,
                manager.WorldObjectSceneCache,
                manager.WorldObjectSceneRoot,
                out message,
                out firstProblem);
        }

        private static GameManager FindActiveSceneManager()
        {
            GameManager[] managers = UnityEngine.Object.FindObjectsByType<GameManager>(
                FindObjectsInactive.Exclude);
            string activePath = EditorSceneManager.GetActiveScene().path;
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null
                    && string.Equals(
                        managers[i].gameObject.scene.path,
                        activePath,
                        StringComparison.Ordinal))
                {
                    return managers[i];
                }
            }

            return null;
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Trim().Replace('\\', '/');
        }
    }

    internal sealed partial class CharacterTestGenesisValidationLevelEditorModule
    {
        private static GUIStyle _fixtureWarningLabelStyle;

        private CharacterTestGenesisValidationFixtureReadinessReport EvaluateFixtureReadiness()
        {
            GameManager manager = FindGameManager();
            return CharacterTestGenesisValidationFixtureReadiness.Evaluate(
                _document,
                _worldObjects,
                manager?.WorldObjectSceneCache,
                manager?.WorldObjectSceneRoot);
        }

        private void DrawFixtureReadinessOverlay()
        {
            CharacterTestGenesisValidationFixtureReadinessReport report =
                EvaluateFixtureReadiness();
            for (int i = 0; i < report.Entries.Count; i++)
            {
                CharacterTestGenesisValidationFixtureReadinessEntry entry = report.Entries[i];
                if (entry.State == CharacterTestGenesisValidationFixtureState.Applied)
                    continue;

                Transform transform = entry.Transform;
                if (transform == null)
                    transform = ResolveStationWarningAnchor(entry.StationId);
                if (transform == null)
                    continue;

                Color color = entry.State == CharacterTestGenesisValidationFixtureState.Unresolved
                    ? new Color(1f, 0.05f, 0.8f, 1f)
                    : new Color(1f, 0.48f, 0.05f, 1f);
                Bounds bounds = ResolveWarningBounds(transform);
                CompareFunction previousZTest = Handles.zTest;
                try
                {
                    using (new Handles.DrawingScope(color))
                    {
                        Handles.zTest = CompareFunction.Always;
                        Handles.DrawWireCube(bounds.center, bounds.size + Vector3.one * 0.08f);
                        Vector3 minimum = bounds.min;
                        Vector3 maximum = bounds.max;
                        Handles.DrawDottedLine(
                            new Vector3(minimum.x, maximum.y, minimum.z),
                            new Vector3(maximum.x, maximum.y, maximum.z),
                            3f);
                        Handles.DrawDottedLine(
                            new Vector3(minimum.x, maximum.y, maximum.z),
                            new Vector3(maximum.x, maximum.y, minimum.z),
                            3f);
                    }
                }
                finally
                {
                    Handles.zTest = previousZTest;
                }

                string label = entry.State == CharacterTestGenesisValidationFixtureState.Unresolved
                    ? "引用失配 · 禁止 Play"
                    : entry.Kind == CharacterTestGenesisValidationFixtureKind.StaticCollider
                        ? "未应用 · 无框架碰撞"
                        : "未应用 · 无框架可动物体";
                Handles.Label(
                    bounds.center + Vector3.up * (bounds.extents.y + 0.35f),
                    label + "\n" + entry.DisplayName,
                    FixtureWarningLabelStyle);
            }
        }

        private Transform ResolveStationWarningAnchor(string stationId)
        {
            CharacterTestGenesisValidationStation station = FindStationById(stationId);
            if (station == null)
                return null;

            GameManager manager = FindGameManager();
            Transform root = manager?.WorldObjectSceneRoot;
            if (root == null)
                return null;
            if (TryResolveRegionTransform(
                    root,
                    station.SpatialProgram?.FootprintRegion?.StableId,
                    out Transform footprint))
            {
                return footprint;
            }
            if (TryResolvePointTransform(
                    root,
                    station.SpatialProgram?.ObservationPoint?.StableId,
                    out Transform observation))
            {
                return observation;
            }
            return null;
        }

        private static Bounds ResolveWarningBounds(Transform transform)
        {
            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer != null)
                return renderer.bounds;
            return new Bounds(transform.position, Vector3.Max(transform.lossyScale, Vector3.one * 0.5f));
        }

        private static GUIStyle FixtureWarningLabelStyle
        {
            get
            {
                if (_fixtureWarningLabelStyle != null)
                    return _fixtureWarningLabelStyle;
                _fixtureWarningLabelStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.72f, 0.25f, 1f) }
                };
                return _fixtureWarningLabelStyle;
            }
        }
    }
}
