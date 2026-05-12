using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MxFramework.Preview
{
    public static class PreviewConnectionDescriptorWriter
    {
        private static string s_currentPath;
        private static bool s_hooksRegistered;

        public static string GetDescriptorPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "MxFramework", "AuthoringPreview", "preview.json");
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Application Support", "MxFramework", "AuthoringPreview", "preview.json");
            }
            // Linux / others
            string xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(xdg))
                return Path.Combine(xdg, "mxframework", "authoring-preview", "preview.json");
            string home2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home2, ".cache", "mxframework", "authoring-preview", "preview.json");
        }

        public static string Write(PreviewConnectionDescriptor desc)
        {
            if (desc == null) throw new ArgumentNullException(nameof(desc));

            string path = GetDescriptorPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            PreviewJson.Writer w = new PreviewJson.Writer().Begin();
            w.ObjStart()
                .KeyStr("schemaVersion", desc.SchemaVersion ?? "1.0")
                .KeyStr("endpoint", desc.Endpoint)
                .KeyNum("port", desc.Port)
                .KeyStr("token", desc.Token)
                .KeyNum("processId", desc.ProcessId)
                .KeyStr("gameVersion", desc.GameVersion ?? string.Empty)
                .KeyStr("startedAt", desc.StartedAt ?? string.Empty)
                .Key("capabilities").ArrStart();
            if (desc.Capabilities != null)
                for (int i = 0; i < desc.Capabilities.Count; i++) w.Str(desc.Capabilities[i]);
            w.ArrEnd().ObjEnd();

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, w.ToString());
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            s_currentPath = path;
            EnsureCleanupHooks();
            return path;
        }

        public static void Delete()
        {
            try
            {
                if (!string.IsNullOrEmpty(s_currentPath) && File.Exists(s_currentPath))
                    File.Delete(s_currentPath);
            }
            catch { /* best effort */ }
            s_currentPath = null;
        }

        private static void EnsureCleanupHooks()
        {
            if (s_hooksRegistered) return;
            s_hooksRegistered = true;
            Application.quitting += Delete;
            AppDomain.CurrentDomain.ProcessExit += (_, __) => Delete();
        }
    }
}
