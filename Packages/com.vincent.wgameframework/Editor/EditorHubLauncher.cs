using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class EditorHubLauncher
    {
        private const string MenuPath = "MxFramework/Editor/Open Editor Hub";
        private const string WindowsLauncherPath = "Tools/MxFramework.EditorHub/start-editor-hub.bat";
        private const string UnixLauncherPath = "Tools/MxFramework.EditorHub/start-editor-hub.sh";

        [MenuItem(MenuPath, priority = 20)]
        private static void Open()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string launcherRelativePath = Application.platform == RuntimePlatform.WindowsEditor
                ? WindowsLauncherPath
                : UnixLauncherPath;
            string launcherPath = Path.Combine(
                projectRoot,
                launcherRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(launcherPath))
            {
                EditorUtility.DisplayDialog(
                    "Editor Hub launcher not found",
                    "Expected launcher at:\n" + launcherPath,
                    "OK");
                return;
            }

            try
            {
                ProcessStartInfo startInfo = CreateStartInfo(projectRoot, launcherPath);
                Process process = Process.Start(startInfo);
                if (process == null)
                    throw new InvalidOperationException("The operating system did not start the launcher process.");

                UnityEngine.Debug.Log("MxFramework Editor Hub launch requested: " + launcherPath);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Could not open Editor Hub",
                    exception.Message,
                    "OK");
            }
        }

        private static ProcessStartInfo CreateStartInfo(string projectRoot, string launcherPath)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return new ProcessStartInfo
                {
                    FileName = launcherPath,
                    WorkingDirectory = projectRoot,
                    UseShellExecute = true
                };
            }

            return new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = Quote(launcherPath),
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
