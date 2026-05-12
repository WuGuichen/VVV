using System;
using UnityEngine;

namespace MxFramework.Preview
{
    /// <summary>
    /// Singleton bootstrap that owns the PreviewRpcServer + preview world and pumps the
    /// main-thread dispatcher every Update.
    ///
    /// Uses <see cref="ScenePreviewWorld"/> when scene targets are present,
    /// falls back to dummy world otherwise.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [ExecuteAlways]
    public sealed class MxPreviewBootstrap : MonoBehaviour
    {
        private static MxPreviewBootstrap s_instance;

        private PreviewRpcServer _server;
        private IPreviewWorld _world;
        private ScenePreviewWorld _sceneWorld; // null when using fallback dummy
        private MemoryBuffPatchLoader _loader;
        private PreviewMainThreadDispatcher _dispatcher;
        private PreviewLogBuffer _logs;

        public static bool IsRunning => s_instance != null && s_instance._server != null && s_instance._server.IsRunning;
        public static int Port => s_instance != null && s_instance._server != null ? s_instance._server.Port : 0;
        public static string Token => s_instance != null && s_instance._server != null ? s_instance._server.Token : null;
        public static string DescriptorPath => s_instance != null && s_instance._server != null ? s_instance._server.DescriptorPath : null;
        /// <summary>
        /// Current preview mode: "scene" when scene targets are active, "dummy" when falling back.
        /// </summary>
        public static string PreviewMode => s_instance?._sceneWorld?.PreviewMode ?? "dummy";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStartFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-mxPreviewServer", StringComparison.Ordinal))
                {
                    StartServer(null);
                    return;
                }
            }
        }

        /// <summary>
        /// Starts the preview server. Pass a custom <see cref="MxFramework.Buffs.IBuffFactory"/>
        /// to integrate with a real registry; otherwise the ScenePreviewWorld uses
        /// ConfigBuffFactory/ConfigModifierFactory from Runtime Patch v1.
        /// </summary>
        public static MxPreviewBootstrap StartServer(MxFramework.Buffs.IBuffFactory buffFactory)
        {
            if (s_instance != null) return s_instance;

            GameObject go = new GameObject("MxPreviewBootstrap");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            else
                go.hideFlags = HideFlags.HideAndDontSave;
            MxPreviewBootstrap b = go.AddComponent<MxPreviewBootstrap>();
            b.Initialize(buffFactory);
            s_instance = b;
            return b;
        }

        public static void Stop()
        {
            if (s_instance == null) return;
            try { s_instance._server?.Dispose(); } catch { }
            try
            {
                if (Application.isPlaying)
                    Destroy(s_instance.gameObject);
                else
                    DestroyImmediate(s_instance.gameObject);
            }
            catch { }
            s_instance = null;
        }

        private void Initialize(MxFramework.Buffs.IBuffFactory buffFactory)
        {
            _dispatcher = new PreviewMainThreadDispatcher { ExecuteInline = !Application.isPlaying };
            _loader = new MemoryBuffPatchLoader();
            _logs = new PreviewLogBuffer();

            // Create ScenePreviewWorld — discovers MxPreviewSceneTarget in the scene,
            // falls back to DummyPreviewWorld internally if none found.
            var sceneWorld = new ScenePreviewWorld(_logs, buffFactory);
            _world = sceneWorld;
            _sceneWorld = sceneWorld;

            // If using scene mode, log the mode; if fallback, DummyPreviewWorld handles pre-creation
            if (sceneWorld.HasSceneTargets)
            {
                _logs.Append("info", "MxPreviewBootstrap: ScenePreviewWorld active (scene mode)");
                Debug.Log("[MxPreview] ScenePreviewWorld active (scene mode)");
            }
            else
            {
                _logs.Append("info", "MxPreviewBootstrap: no scene targets found, using dummy world");
                Debug.Log("[MxPreview] No scene targets found, using dummy world");
            }

            _server = new PreviewRpcServer(_world, _loader, _dispatcher, Application.version ?? "0.3.1", _logs);
            try
            {
                _server.Start();
            }
            catch (Exception ex)
            {
                Debug.LogError("[MxPreview] failed to start: " + ex);
                _server = null;
            }
        }

        private void Update()
        {
            _dispatcher?.Pump();
        }

        private void OnDestroy()
        {
            try { _server?.Dispose(); } catch { }
            _server = null;
            if (s_instance == this) s_instance = null;
        }
    }
}
