using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Editor-only derived rendering cache. Reads the same assets that are edited/saved; owns no persistent instances.</summary>
    [InitializeOnLoad]
    internal static class CharacterTestVegetationEditorPreview
    {
        private static readonly List<CharacterTestVegetationSource> Sources = new List<CharacterTestVegetationSource>();
        private static double _next;
        static CharacterTestVegetationEditorPreview()
        {
            EditorApplication.update += Update;
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += scene => { CharacterTestVegetationAssetAuthoring.ReleaseScene(scene); _next = 0; };
            EditorApplication.playModeStateChanged += _ => Release();
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }
        private static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { if (Sources.Count != 0) Release(); return; }
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + .5;
            Sources.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                try { var source = CharacterTestVegetationAssetAuthoring.Find(scene); if (source != null) { if (source.isActiveAndEnabled) Sources.Add(source); else source.Renderer.Disable(); } }
                catch (System.InvalidOperationException) { /* Authoring diagnostics expose duplicate owners; never silently choose one. */ }
            }
        }
        private static void Invalidate() { foreach (var source in Sources) source.Renderer.Invalidate(); _next = 0; }
        private static void Begin(ScriptableRenderContext context, Camera camera)
        { if (EditorApplication.isPlayingOrWillChangePlaymode) return; Update(); foreach (var source in Sources) source.Renderer.Submit(camera); }
        private static void End(ScriptableRenderContext context, Camera camera)
        { if (EditorApplication.isPlayingOrWillChangePlaymode) return; foreach (var source in Sources) source.Renderer.EndCamera(camera); }
        private static void Release() { Sources.Clear(); CharacterTestVegetationAssetAuthoring.Release(); _next = 0; }
    }
}
