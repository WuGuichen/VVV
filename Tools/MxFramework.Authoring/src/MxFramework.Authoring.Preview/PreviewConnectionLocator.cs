using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Preview
{
    /// <summary>
    /// 跨平台读取 / 写入 Authoring Preview 连接描述文件。
    /// 默认路径来自 03 子需求权威 Spec；通过环境变量 MX_AUTHORING_PREVIEW_DIR 可覆盖（用于测试隔离）。
    /// </summary>
    public static class PreviewConnectionLocator
    {
        public const string EnvOverride = "MX_AUTHORING_PREVIEW_DIR";
        public const string FileName = "preview.json";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string GetDefaultDirectory()
        {
            string overrideDir = Environment.GetEnvironmentVariable(EnvOverride);
            if (!string.IsNullOrEmpty(overrideDir))
                return overrideDir;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                if (string.IsNullOrEmpty(local))
                    local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local ?? string.Empty, "MxFramework", "AuthoringPreview");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
                return Path.Combine(home, "Library", "Application Support", "MxFramework", "AuthoringPreview");
            }

            // Linux / others
            string xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(xdg))
                return Path.Combine(xdg, "mxframework", "authoring-preview");
            string fallbackHome = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
            return Path.Combine(fallbackHome, ".cache", "mxframework", "authoring-preview");
        }

        public static string GetDefaultFilePath()
        {
            return Path.Combine(GetDefaultDirectory(), FileName);
        }

        /// <summary>
        /// 读取连接描述文件。若文件不存在或对应进程已不存活，返回 null。
        /// </summary>
        public static PreviewConnectionDescriptor TryRead()
        {
            return TryReadFrom(GetDefaultFilePath());
        }

        public static PreviewConnectionDescriptor TryReadFrom(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;
                string text = File.ReadAllText(filePath, Encoding.UTF8);
                PreviewConnectionDescriptor descriptor = JsonSerializer.Deserialize<PreviewConnectionDescriptor>(text, JsonOptions);
                if (descriptor == null)
                    return null;
                if (!IsProcessAlive(descriptor.ProcessId))
                    return null;
                return descriptor;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 仅供测试 / Mock Server 使用：原子写入连接描述文件，先 .tmp 再 rename。
        /// </summary>
        public static string WriteForTests(string directory, PreviewConnectionDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            string targetDir = string.IsNullOrEmpty(directory) ? GetDefaultDirectory() : directory;
            Directory.CreateDirectory(targetDir);
            string path = Path.Combine(targetDir, FileName);
            string tmp = path + ".tmp";
            string text = JsonSerializer.Serialize(descriptor, JsonOptions);
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        public static void DeleteIfExists(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }
        }

        private static bool IsProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using Process p = Process.GetProcessById(pid);
                return p != null && !p.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
