using System;
using System.Collections.Generic;
using MxFramework.Editor.Levels;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Thin Level Editor projection over scene-owned rendering and the shared Look asset.</summary>
    internal sealed partial class CharacterTestSceneRenderingLevelEditorModule :
        IMxLevelEditorModule, IMxLevelEditorDirtyDiagnostics,
        IMxLevelEditorSceneChangeAwareModule, IMxLevelEditorSaveParticipant,
        IMxLevelEditorAssetSavePaths, IMxLevelEditorSceneSaveSnapshotProvider
    {
        private const string Id = "characterTest.sceneRendering";
        private GameManager _owner;
        private MxLevelEditorWindow _window;
        private bool _dirty;
        private string _status = string.Empty;
        private string _scanMessage = string.Empty;
        private bool _scanHasWarnings;
        private Label _diagnosticLabel;
        private readonly HashSet<MxSceneLightingLook> _editedLooks = new HashSet<MxSceneLightingLook>();
        private readonly Dictionary<MxSceneLightingLook, string> _preparedLooks = new Dictionary<MxSceneLightingLook, string>();

        [InitializeOnLoadMethod]
        private static void Register() => MxLevelEditorModuleRegistry.Register(new CharacterTestSceneRenderingLevelEditorModule());

        public string ModuleId => Id;
        public string DisplayName => "场景渲染";
        public string DocumentPath => _owner != null ? _owner.gameObject.scene.path : string.Empty;
        public bool IsDirty => _dirty || IsWaterAssetDirty || _editedLooks.Count > 0 ||
            (_owner != null && _owner.SceneRendering?.Look != null && EditorUtility.IsDirty(_owner.SceneRendering.Look));
        public bool IsDocumentDirty => false;
        public bool IsManagedSceneDirty => _dirty;
        public string Status => _status;
        public Transform ManagedSceneRoot => _owner != null ? _owner.transform : null;

        internal static GameManager FindUniqueOwner(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            GameManager result = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var manager in root.GetComponentsInChildren<GameManager>(true))
                {
                    if (result != null) return null;
                    result = manager;
                }
            return result;
        }

        internal static bool IsUnconfigured(MxSceneRenderingConfiguration c) => c == null ||
            (!c.AuthoringCreated && !c.IndirectLightingEnabled && !c.HeightFogEnabled && !c.DustEnabled && c.StaticRoot == null && c.Sun == null &&
             c.TargetCamera == null && c.Feature == null && c.Data == null && c.Look == null &&
             c.Compute == null && c.CaptureShader == null && c.ResolveShader == null && c.FogCompositeShader == null && c.DustShader == null);

        public void OnEnable()
        {
            var candidate = FindUniqueOwner(SceneManager.GetActiveScene());
            if (!ReferenceEquals(_owner, candidate)) { _scanMessage = string.Empty; _scanHasWarnings = false; ResetProbeOwner(); }
            if (_owner != candidate && !IsDirty) _editedLooks.Clear();
            _owner = candidate;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_owner == null) { _status = "当前活动场景需要唯一的 GameManager。"; return; }
            var c = _owner.SceneRendering;
            if (IsUnconfigured(c)) { _status = "未配置场景渲染。"; return; }
            var preview = CharacterTestSceneRenderingPreview.Session;
            if (c.IndirectLightingEnabled)
            {
                if (c.StaticRoot == null || c.Data == null || c.SourceLayout == null)
                    { _status = "间接光尚未绑定静态场景或缓存；可保存草稿，正式构建前需补齐。"; return; }
                if (preview != null && !string.IsNullOrEmpty(preview.LightingIssue))
                    { _status = preview.LightingIssue; return; }
                if (preview == null)
                {
                    try
                    {
                        if (!MxSceneLightingBuilder.CacheMatchesSource(c, out _))
                            { _status = "光传输缓存已过期；保存可继续，正式构建前需重建。"; return; }
                    }
                    catch (Exception e) { _status = e.Message; return; }
                }
            }
            _status = preview != null && !string.IsNullOrEmpty(preview.LightingIssue) ? preview.LightingIssue :
                preview != null && !string.IsNullOrEmpty(preview.FogIssue) ? preview.FogIssue :
                preview != null && !string.IsNullOrEmpty(preview.DustIssue) ? preview.DustIssue : "场景渲染配置可用。";
        }

        internal void OnPreviewDiagnosticsChanged()
        {
            RefreshStatus();
            if (_diagnosticLabel != null) _diagnosticLabel.text = _status;
            _window?.RefreshWorkspaceState();
        }

        private void Changed()
        {
            CharacterTestShallowWaterPreview.Invalidate();
            _dirty = true;
            if (_owner != null) EditorSceneManager.MarkSceneDirty(_owner.gameObject.scene);
            CharacterTestSceneRenderingPreview.RefreshNow();
            RefreshStatus(); _window?.RefreshWorkspaceState();
            SceneView.RepaintAll();
        }
        internal void OnMigrated() { Changed(); RebuildBody(); }

        private void CreateAndScan()
        {
            if (EditorApplication.isPlaying || _owner == null) return;
            var configuration = new MxSceneRenderingConfiguration();
            Undo.RecordObject(_owner, "创建场景渲染配置");
            try
            {
                var scan = CharacterTestSceneRenderingAutoScanner.Scan(_owner, configuration);
                int filled = scan.ApplyMissing(configuration);
                _scanMessage = scan.Message(filled);
                _scanHasWarnings = scan.Diagnostics.Count > 0;
            }
            catch (Exception e) { _scanMessage = "自动扫描未完成：" + e.Message; _scanHasWarnings = true; Debug.LogException(e); }
            _owner.SetSceneRendering(configuration);
            Changed(); RebuildBody();
        }

        private void AutoScanMissing()
        {
            if (EditorApplication.isPlaying || _owner?.SceneRendering == null) return;
            try
            {
                var scan = CharacterTestSceneRenderingAutoScanner.Scan(_owner, _owner.SceneRendering);
                Undo.RecordObject(_owner, "自动扫描场景渲染绑定");
                int filled = scan.ApplyMissing(_owner.SceneRendering);
                _scanMessage = scan.Message(filled);
                _scanHasWarnings = scan.Diagnostics.Count > 0;
                if (filled > 0) Changed();
            }
            catch (Exception e) { _scanMessage = "自动扫描未完成：" + e.Message; _scanHasWarnings = true; Debug.LogException(e); }
            RebuildBody();
        }
        private void EditScene(string label, Action<MxSceneRenderingConfiguration> edit)
        {
            if (EditorApplication.isPlaying || _owner == null || _owner.SceneRendering == null) return;
            Undo.RecordObject(_owner, label);
            edit(_owner.SceneRendering); Changed();
        }
        private void EditLook(string label, Action<MxSceneLightingLook> edit)
        {
            if (EditorApplication.isPlaying || _owner?.SceneRendering?.Look == null) return;
            var look = _owner.SceneRendering.Look;
            Undo.RecordObject(look, label); edit(look);
            EditorUtility.SetDirty(look); _editedLooks.Add(look);
            Changed();
        }
        private void EditSun(string label, bool rotation, Action<Light> edit)
        {
            if (EditorApplication.isPlaying || _owner?.SceneRendering?.Sun == null) return;
            var sun = _owner.SceneRendering.Sun;
            Undo.RecordObject(rotation ? (UnityEngine.Object)sun.transform : sun, label);
            edit(sun); Changed();
        }

        public void OpenJson() => SelectRootSource(); // Unity Scene is the source; no JSON copy exists.
        public void SelectRootSource() { if (_owner != null) Selection.activeGameObject = _owner.gameObject; }
        public void ApplyToScene() { CharacterTestSceneRenderingPreview.RefreshNow(); RefreshStatus(); }
        public void Reload() { OnEnable(); CharacterTestSceneRenderingPreview.RefreshNow(); }
        public void Validate() { OnEnable(); _window?.RefreshWorkspaceState(); }
        public void Save()
        {
            if (EditorApplication.isPlaying) return;
            MxLevelEditorSaveCoordinator.TrySaveModules(MxLevelEditorModuleRegistry.Modules, out _status);
            _window?.RefreshWorkspaceState();
        }
        public void CollectManagedSceneTransforms(List<Transform> output)
        {
            if (_owner != null) output.Add(_owner.transform);
        }
        public bool NotifySceneTransformChanged(Transform transform)
        {
            var c = _owner?.SceneRendering;
            if (c == null || transform == null) return false;
            if (c.Sun != null && transform == c.Sun.transform) { Changed(); return true; }
            if (c.StaticRoot != null && (transform == c.StaticRoot || transform.IsChildOf(c.StaticRoot)))
                { RefreshStatus(); return true; }
            return false;
        }
        public void RefreshUnregisteredSceneObjects(IReadOnlyList<Transform> allManagedTransforms) { }
        public void AcknowledgeManagedSceneSaved() { _dirty = false; }

        public void CollectAssetSavePaths(List<string> paths)
        {
            CollectWaterSavePaths(paths);
            foreach (var look in _editedLooks)
            {
                if (look == null) continue;
                string path = AssetDatabase.GetAssetPath(look);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            var current = _owner?.SceneRendering?.Look;
            if (current != null && EditorUtility.IsDirty(current))
            {
                string path = AssetDatabase.GetAssetPath(current);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
        }
        public void CollectSceneSaveObjects(List<UnityEngine.Object> output)
        {
            if (_owner != null) output.Add(_owner);
        }
        public bool TryPrepareSave(out string failureMessage)
        {
            OnEnable(); failureMessage = null;
            if (_owner == null) { failureMessage = "场景渲染需要唯一的 GameManager。"; return false; }
            var c = _owner.SceneRendering;
            // Incomplete bindings and stale transport are authoring diagnostics. Player-build
            // validation rejects enabled GI/fog until their required data is ready.
            PrepareWaterSave();
            _preparedLooks.Clear();
            foreach (var look in _editedLooks)
                if (look != null) _preparedLooks[look] = JsonUtility.ToJson(look);
            if (c?.Look != null && EditorUtility.IsDirty(c.Look))
                _preparedLooks[c.Look] = JsonUtility.ToJson(c.Look);
            return true;
        }
        public void SavePreparedChanges()
        {
            SavePreparedWater();
            foreach (var look in _preparedLooks.Keys)
                if (look != null) AssetDatabase.SaveAssetIfDirty(look);
        }
        public void CompleteCoordinatedSave(bool succeeded, string message)
        {
            CompleteWaterSave(succeeded);
            if (succeeded) { _dirty = false; _editedLooks.Clear(); }
            else foreach (var entry in _preparedLooks)
                if (entry.Key != null)
                {
                    JsonUtility.FromJsonOverwrite(entry.Value, entry.Key);
                    EditorUtility.SetDirty(entry.Key);
                    _editedLooks.Add(entry.Key);
                }
            _preparedLooks.Clear();
            _status = succeeded ? "场景渲染与共享参数已保存。" : message;
            CharacterTestSceneRenderingPreview.RefreshNow();
        }
    }
}
