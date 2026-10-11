using System;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestSceneRenderingLevelEditorModule
    {
        private static CharacterTestSceneRenderingLevelEditorModule _buildModule;
        private static ulong _buildSceneHandle;

        [InitializeOnLoadMethod]
        private static void WatchBuildLifetime()
        {
            EditorSceneManager.sceneClosing += SceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.ExitingEditMode) CancelBuild(); };
        }
        private static void SceneClosing(Scene scene, bool removingScene)
        { if (_buildModule != null && scene.handle.GetRawData() == _buildSceneHandle) CancelBuild(); }
        private static void ActiveSceneChanged(Scene previous, Scene current)
        { if (_buildModule != null && current.handle.GetRawData() != _buildSceneHandle) CancelBuild(); }
        private static void CancelBuild()
        {
            if (_buildModule == null) return;
            MxSceneLightingBuilder.Cancel();
            EditorApplication.update -= AdoptBuild;
            _buildModule._status = "光传输构建已取消。";
            _buildModule._window?.RefreshWorkspaceState();
            _buildModule.RebuildBody();
            _buildModule = null;
        }
        private void BuildTransport()
        {
            if (EditorApplication.isPlaying || _owner?.SceneRendering?.StaticRoot == null ||
                string.IsNullOrEmpty(_owner.gameObject.scene.path) || MxSceneLightingBuilder.IsBuilding) return;
            var c = _owner.SceneRendering;
            _buildModule = this;
            _buildSceneHandle = _owner.gameObject.scene.handle.GetRawData();
            try
            {
                string folder="Assets/Config/MxFramework/Rendering/SceneLighting/"+_owner.gameObject.scene.name;
                if(c.ProbeQuality?.AutomaticRefinement==true)folder+="_Automatic_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                MxSceneLightingBuilder.Begin(c.StaticRoot,
                    folder,
                    c.SourceLayout, c.StaticSourceRenderers, probeSpacing: c.ProbeSpacing,
                    probeCoverageBounds: MxSceneLightingBuilder.ProbeCoverage(c),probeQuality:MxSceneLightingBuilder.ResolveProbeQuality(c));
                EditorApplication.update -= AdoptBuild;
                EditorApplication.update += AdoptBuild;
                _status = "正在构建光传输。";
            }
            catch (Exception e)
            {
                _buildModule = null;
                _status = e.Message;
                Debug.LogException(e);
            }
            _window?.RefreshWorkspaceState();
            RebuildBody();
        }
        private static void AdoptBuild()
        {
            if (MxSceneLightingBuilder.IsBuilding) return;
            EditorApplication.update -= AdoptBuild;
            var module = _buildModule;
            _buildModule = null;
            if (module?._owner == null || module._owner.gameObject.scene.handle.GetRawData() != _buildSceneHandle) return;
            if (MxSceneLightingBuilder.LastResult == null)
            {
                module._status = "光传输构建失败：" + MxSceneLightingBuilder.LastError;
                module._window?.RefreshWorkspaceState();
                module.RebuildBody();
                return;
            }
            Undo.RecordObject(module._owner, "更新场景光传输缓存");
            module._owner.SceneRendering.Data = MxSceneLightingBuilder.LastResult;
            module.RefreshProbePreview();
            module.Changed();
            CharacterTestSceneRenderingPreview.Session?.SetSourceValidation(true, string.Empty);
            CharacterTestSceneRenderingPreview.Session?.RebindLighting();
            module._status = "光传输构建完成，使用 Level Editor 保存场景引用。";
            module._window?.RefreshWorkspaceState();
            module.RebuildBody();
        }
        private void RebuildOccluders()
        {
            if (EditorApplication.isPlaying || _owner?.SceneRendering == null || MxSceneLightingBuilder.IsBuilding) return;
            try
            {
                MxSceneLightingBuilder.RebuildOccluders(_owner.SceneRendering);
                CharacterTestSceneRenderingPreview.Session?.RebindLighting();
                _status = "太阳遮挡网格已更新。";
            }
            catch (Exception e) { _status = e.Message; Debug.LogException(e); }
            _window?.RefreshWorkspaceState();
        }
    }
}
