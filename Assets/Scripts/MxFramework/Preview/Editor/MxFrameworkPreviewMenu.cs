using UnityEditor;
using UnityEngine;
using MxFramework.Editor;

namespace MxFramework.Preview.EditorMenu
{
    /// <summary>
    /// Top-level menu entry until Framework Manager integration lands. Lets a developer
    /// start / stop the runtime preview server from inside the Unity Editor.
    /// </summary>
    [InitializeOnLoad]
    public static class MxFrameworkPreviewMenu
    {
        private const string MenuStart = "MxFramework/Runtime Preview/Start Server";
        private const string MenuStop = "MxFramework/Runtime Preview/Stop Server";
        private const string MenuStatus = "MxFramework/Runtime Preview/Print Status";

        static MxFrameworkPreviewMenu()
        {
            FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(
                "preview.start-server",
                "启动 Runtime Preview Server",
                "Preview",
                "启动本机 Preview Server，用于外部 Authoring Editor 和运行时预览闭环。",
                "菜单",
                MenuStart,
                60,
                StartServer));
            FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(
                "preview.stop-server",
                "停止 Runtime Preview Server",
                "Preview",
                "停止当前运行中的 Preview Server。",
                "菜单",
                MenuStop,
                61,
                StopServer));
            FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(
                "preview.print-status",
                "打印 Runtime Preview 状态",
                "Preview",
                "向 Unity Console 输出端口、descriptor 和 token。",
                "菜单",
                MenuStatus,
                62,
                PrintStatus));
        }

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
