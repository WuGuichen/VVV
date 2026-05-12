using UnityEditor;
using UnityEngine;

namespace MxFramework.Preview.EditorMenu
{
    /// <summary>
    /// Top-level menu entry until Framework Manager integration lands. Lets a developer
    /// start / stop the runtime preview server from inside the Unity Editor.
    /// </summary>
    public static class MxFrameworkPreviewMenu
    {
        private const string MenuStart = "MxFramework/Runtime Preview/Start Server";
        private const string MenuStop = "MxFramework/Runtime Preview/Stop Server";
        private const string MenuStatus = "MxFramework/Runtime Preview/Print Status";

        [MenuItem(MenuStart, priority = 200)]
        public static void StartServer()
        {
            if (MxPreviewBootstrap.IsRunning)
            {
                Debug.Log("[MxPreview] Server already running on port " + MxPreviewBootstrap.Port);
                return;
            }
            // Uses the built-in authoring preview factory unless the game layer wires a custom factory.
            MxPreviewBootstrap.StartServer(null);
            EditorApplication.delayCall += PrintStatus;
        }

        [MenuItem(MenuStart, validate = true)]
        public static bool ValidateStart() => !MxPreviewBootstrap.IsRunning;

        [MenuItem(MenuStop, priority = 201)]
        public static void StopServer()
        {
            MxPreviewBootstrap.Stop();
            Debug.Log("[MxPreview] Server stopped.");
        }

        [MenuItem(MenuStop, validate = true)]
        public static bool ValidateStop() => MxPreviewBootstrap.IsRunning;

        [MenuItem(MenuStatus, priority = 220)]
        public static void PrintStatus()
        {
            if (!MxPreviewBootstrap.IsRunning)
            {
                Debug.Log("[MxPreview] Server: not running");
                return;
            }
            Debug.Log($"[MxPreview] running\n  port = {MxPreviewBootstrap.Port}\n  descriptor = {MxPreviewBootstrap.DescriptorPath}\n  token = {MxPreviewBootstrap.Token}");
        }
    }
}
