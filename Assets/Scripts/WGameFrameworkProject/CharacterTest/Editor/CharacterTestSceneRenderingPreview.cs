using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Editor-only preview. GameManager's gameplay lifecycle never runs for this preview.</summary>
    [InitializeOnLoad]
    internal static class CharacterTestSceneRenderingPreview
    {
        private const string PlaySceneKey = "MxSceneRendering.PlayScene";
        private const string PlaySourceValidKey = "MxSceneRendering.PlaySourceValid";
        private const string PlaySourceIssueKey = "MxSceneRendering.PlaySourceIssue";
        private const string PlayShadowValidKey = "MxSceneRendering.PlayShadowValid";
        private const string PlayShadowIssueKey = "MxSceneRendering.PlayShadowIssue";
        private const double FingerprintFallbackSeconds = 10;
        private static GameManager _owner;
        private static MxSceneRenderingSession _session;
        private static double _nextRefresh, _nextFingerprint;
        private static MxSceneLightingData _cachedData;
        private static TextAsset _cachedTransport;
        private static MxSceneLightingPayload _cachedPayload;
        private static Hash128 _cachedDataHash, _cachedTransportHash;
        private static string _cachedSourceFingerprint;
        private static string _cachedScenePath;
        private static int _debugView = -1;
        private static bool _creating;
        private static bool _playValidationPending;
        internal static MxSceneRenderingSession Session => _session;

        static CharacterTestSceneRenderingPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorSceneManager.sceneClosed += SceneClosed;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
        }

        internal static void RefreshNow()
        {
            _nextRefresh = 0;
            _nextFingerprint = 0;
            Update();
        }

        internal static void SetDebugView(int mode)
        {
            _debugView = Mathf.Clamp(mode, -1, 3);
            _session?.SetPreviewDebugView(_debugView);
            SceneView.RepaintAll();
        }

        private static void Invalidate() { _nextRefresh = 0; _nextFingerprint = 0; }
        private static void SceneClosed(Scene scene)
        { if (_owner != null && _owner.gameObject.scene == scene) Release(); }

        private static void OnObjectChanges(ref ObjectChangeEventStream changes)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || _owner == null ||
                (_owner.SceneRendering?.IndirectLightingEnabled != true && _owner.SceneRendering?.DirectionalShadowsEnabled != true)) return;
            var root = _owner.SceneRendering.StaticRoot;
            for (int i = 0; i < changes.length; i++)
            {
                var kind = changes.GetEventType(i);
                if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    changes.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
                    Object changed = EditorUtility.EntityIdToObject(change.entityId);
                    Transform transform = changed is GameObject gameObject ? gameObject.transform :
                        (changed as Component)?.transform;
                    if (changed != _owner && !_owner.SceneRendering.DirectionalShadowsEnabled && (root == null || transform == null || !transform.IsChildOf(root)))
                        continue;
                }
                else if (kind == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    changes.GetChangeAssetObjectPropertiesEvent(i, out var change);
                    Object changed = EditorUtility.EntityIdToObject(change.entityId);
                    if (!(changed is Material) && !(changed is Mesh) && !(changed is Texture) &&
                        !(changed is MxSceneLightingData) && !(changed is MxDirectionalShadowData) && !(changed is TextAsset)) continue;
                }
                else if (kind == ObjectChangeKind.None) continue;
                Invalidate();
                return;
            }
        }

        private static MxSceneLightingPayload ReadPreviewPayload(MxSceneLightingData data)
        {
            TextAsset transport = data.Payload;
            string dataPath = AssetDatabase.GetAssetPath(data);
            string transportPath = AssetDatabase.GetAssetPath(transport);
            if (string.IsNullOrEmpty(dataPath) || string.IsNullOrEmpty(transportPath)) return data.ReadPayload();
            Hash128 dataHash = AssetDatabase.GetAssetDependencyHash(dataPath);
            Hash128 transportHash = AssetDatabase.GetAssetDependencyHash(transportPath);
            if (_cachedPayload != null && _cachedData == data && _cachedTransport == transport &&
                !EditorUtility.IsDirty(data) && _cachedDataHash == dataHash &&
                _cachedTransportHash == transportHash && _cachedSourceFingerprint == data.SourceFingerprint)
                return _cachedPayload;
            MxSceneLightingPayload payload = data.ReadPayload();
            _cachedData = data;
            _cachedTransport = transport;
            _cachedPayload = payload;
            _cachedDataHash = dataHash;
            _cachedTransportHash = transportHash;
            _cachedSourceFingerprint = data.SourceFingerprint;
            _cachedScenePath = SceneManager.GetActiveScene().path;
            return payload;
        }

        private static void ClearPayloadCache()
        {
            _cachedData = null;
            _cachedTransport = null;
            _cachedPayload = null;
            _cachedSourceFingerprint = null;
            _cachedScenePath = null;
            _cachedDataHash = _cachedTransportHash = default;
        }
        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RecordPlaySourceValidation();
                Release();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Release();
                _playValidationPending = true;
                ApplyPlaySourceValidation();
            }
            else if (state == PlayModeStateChange.EnteredEditMode) Invalidate();
        }

        private static void RecordPlaySourceValidation()
        {
            Scene scene = SceneManager.GetActiveScene();
            SessionState.SetString(PlaySceneKey, scene.path);
            var owner = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(scene);
            bool valid = true;
            string reason = string.Empty;
            if (owner != null && owner.SceneRendering?.IndirectLightingEnabled == true)
                valid = ValidateSource(owner.SceneRendering, out reason);
            SessionState.SetBool(PlaySourceValidKey, valid);
            SessionState.SetString(PlaySourceIssueKey, reason);
            valid=owner==null || CharacterTestDirectionalShadowAuthoring.Validate(owner,out reason);
            SessionState.SetBool(PlayShadowValidKey,valid);
            SessionState.SetString(PlayShadowIssueKey,reason);
        }

        private static void ApplyPlaySourceValidation()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SessionState.GetString(PlaySceneKey, string.Empty))
            { _playValidationPending = false; return; }
            var owner = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(scene);
            if (owner == null || !owner.isActiveAndEnabled) return;
            owner.SetSceneRenderingSourceValidation(
                SessionState.GetBool(PlaySourceValidKey, true),
                SessionState.GetString(PlaySourceIssueKey, string.Empty));
            owner.SetDirectionalShadowSourceValidation(
                SessionState.GetBool(PlayShadowValidKey,true),SessionState.GetString(PlayShadowIssueKey,string.Empty));
            _playValidationPending = false;
        }

        private static bool ValidateSource(MxSceneRenderingConfiguration c, out string reason)
        {
            if (c.Data == null || c.StaticRoot == null || c.SourceLayout == null)
            { reason = "Light transport source or cache is missing."; return false; }
            try
            {
                return MxSceneLightingBuilder.CacheMatchesSource(c, out reason);
            }
            catch (System.Exception e) { reason = e.Message; return false; }
        }

        private static void Update()
        {
            if (_creating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Release();
                if (EditorApplication.isPlaying && _playValidationPending) ApplyPlaySourceValidation();
                return;
            }
            if (EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.25;
            Scene scene = SceneManager.GetActiveScene();
            // SceneClosed also fires while Unity swaps scene instances at the Play boundary.
            // Drop the CPU copy only after the settled Edit-mode scene actually changes.
            if (_cachedPayload != null && _cachedScenePath != scene.path) ClearPayloadCache();
            GameManager candidate = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(scene);
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.SceneRendering == null ||
                (!candidate.SceneRendering.IndirectLightingEnabled && !candidate.SceneRendering.HeightFogEnabled && !candidate.SceneRendering.DustEnabled && !candidate.SceneRendering.DirectionalShadowsEnabled))
            { Release(); ClearPayloadCache(); return; }
            if (!candidate.SceneRendering.IndirectLightingEnabled) ClearPayloadCache();
            // Unity can reuse an object ID across Play scene reloads. Compare the managed
            // wrapper, not UnityEngine.Object's overloaded equality for destroyed objects.
            if (!ReferenceEquals(_owner, candidate) || _owner == null)
            {
                Release();
                _creating = true;
                try
                {
                    bool valid = true;
                    string reason = string.Empty;
                    if (candidate.SceneRendering.IndirectLightingEnabled)
                        valid = ValidateSource(candidate.SceneRendering, out reason);
                    var session = new MxSceneRenderingSession(
                        () => candidate != null ? candidate.SceneRendering : null,
                        scene, true, valid, reason, ReadPreviewPayload);
                    _owner = candidate;
                    _session = session;
                    bool shadowValid = CharacterTestDirectionalShadowAuthoring.Validate(candidate, out string shadowIssue);
                    _session.SetDirectionalShadowSourceValidation(shadowValid, shadowIssue);
                    _session.SetPreviewDebugView(_debugView);
                    _nextFingerprint = EditorApplication.timeSinceStartup + FingerprintFallbackSeconds;
                    NotifyDiagnostics();
                }
                finally { _creating = false; }
            }
            if (EditorApplication.timeSinceStartup >= _nextFingerprint)
            {
                _nextFingerprint = EditorApplication.timeSinceStartup + FingerprintFallbackSeconds;
                var c = candidate.SceneRendering;
                if (c.IndirectLightingEnabled)
                {
                    bool valid = ValidateSource(c, out string reason);
                    string previousIssue = _session.LightingIssue;
                    _session.SetSourceValidation(valid, reason);
                    if (previousIssue != _session.LightingIssue) NotifyDiagnostics();
                }
                bool shadowValid = CharacterTestDirectionalShadowAuthoring.Validate(candidate, out string shadowIssue);
                _session.SetDirectionalShadowSourceValidation(shadowValid, shadowIssue);
            }
            string previousFogIssue = _session.FogIssue;
            _session.Tick();
            if (previousFogIssue != _session.FogIssue) NotifyDiagnostics();
        }

        private static void NotifyDiagnostics()
        {
            foreach (var module in MxFramework.Editor.Levels.MxLevelEditorModuleRegistry.Modules)
                if (module is CharacterTestSceneRenderingLevelEditorModule rendering)
                    rendering.OnPreviewDiagnosticsChanged();
        }

        private static void Release()
        {
            _session?.Dispose(); _session = null; _owner = null;
        }
    }
}
