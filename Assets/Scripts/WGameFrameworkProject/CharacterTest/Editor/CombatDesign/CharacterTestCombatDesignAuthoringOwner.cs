using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Editor-thread adapter for Web authoring; all card mutations remain with the existing owners.</summary>
    /// <remarks>LowFreqAlloc: bounded explicit commands, no Play, scene or player-save operations.</remarks>
    [InitializeOnLoad]
    internal static partial class CharacterTestCombatDesignAuthoringOwner
    {
        private static double _nextPoll;
        private static bool _processing;
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        internal static string Root => Path.Combine(ProjectRoot, "Temp/MxFramework/CombatDesign/authoring");
        private static bool Ready => !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode;
        static CharacterTestCombatDesignAuthoringOwner() { EditorApplication.update += Tick; }

        private static void Tick()
        {
            if (_processing || EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 2;
            try
            {
                Atomic(Path.Combine(Root, "owner.json"), new JObject { ["schema"] = "mx.combat-design.authoring.owner.v1",
                    ["projectRoot"] = ProjectRoot, ["ready"] = Ready, ["updatedUtc"] = DateTime.UtcNow.ToString("O"),
                    ["playing"] = EditorApplication.isPlayingOrWillChangePlaymode, ["compiling"] = EditorApplication.isCompiling });
                if (!Ready) return;
                string directory = Path.Combine(Root, "requests");
                if (!Directory.Exists(directory)) return;
                foreach (string path in Directory.GetFiles(directory, "*.json").OrderBy(File.GetCreationTimeUtc))
                {
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (!Token(id) || File.Exists(Path.Combine(Root, "responses", id + ".json"))) continue;
                    _processing = true;
                    try { Process(path); } finally { _processing = false; }
                    break;
                }
            }
            catch (Exception error) { Debug.LogWarning("[CombatDesignAuthoring] " + error); }
        }

        private static void Process(string path)
        {
            string id = Path.GetFileNameWithoutExtension(path), running = Path.Combine(Root, "running", id + ".json");
            JObject result;
            try
            {
                var command = Read(path);
                Need((string)command["schema"] == "mx.combat-design.authoring.command.v1" && (string)command["operationId"] == id, "COMMAND_INVALID");
                Need(!File.Exists(running), "RECOVERY_REQUIRED: 上次请求被中断。发布请恢复原操作，其他请求请刷新后重试。");
                Need(DateTime.UtcNow - DateTime.Parse((string)command["createdUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind) < TimeSpan.FromMinutes(5),
                    "COMMAND_EXPIRED: 等待超过五分钟；请刷新后重新操作。");
                Atomic(running, command);
                result = Execute((string)command["operation"], (JObject)command["request"], id);
                result["status"] = result["status"] ?? "Succeeded";
            }
            catch (Exception error)
            {
                result = new JObject { ["status"] = error.Message.StartsWith("RECOVERY_REQUIRED", StringComparison.Ordinal) ? "RecoveryRequired" : "Rejected",
                    ["message"] = error.Message, ["diagnostics"] = new JArray(error.Message) };
            }
            result["operationId"] = id; result["completedUtc"] = DateTime.UtcNow.ToString("O");
            Atomic(Path.Combine(Root, "responses", id + ".json"), result);
            if (File.Exists(running)) File.Delete(running);
        }

        internal static JObject Execute(string operation, JObject request, string operationId)
        {
            Need(Ready, "EDITOR_BUSY: 请在 Editor 编辑模式就绪后操作。");
            Need(Token(operationId), "OPERATION_ID_INVALID");
            request = request ?? throw new ArgumentNullException(nameof(request));
            if (operation.StartsWith("levels.", StringComparison.Ordinal)) return CharacterTestLevelAuthoringOperations.Execute(operation, request);
            string scope = (string)request["scope"] ?? "main";
            using (var context = new AuthoringContext(scope))
            {
                switch (operation)
                {
                    case "refresh": return context.Library();
                    case "open": return context.Open(request);
                    case "save": return context.Save(request);
                    case "preflight": return context.Preflight(request);
                    case "publish": return context.Publish(request, operationId);
                    case "reconcile": return context.Reconcile(request);
                    default: throw new ArgumentException("UNKNOWN_AUTHORING_OPERATION");
                }
            }
        }

        internal static string Hash(string text)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        private static bool Token(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
        private static void Need(bool ok, string message) { if (!ok) throw new InvalidOperationException(message ?? "AUTHORING_OPERATION_FAILED"); }
        private static JObject Read(string path)
        {
            Safe(path);
            // Plan hashes cover exact JSON strings; parsing an ISO string as DateTime changes its canonical bytes.
            using var reader = new JsonTextReader(new StringReader(File.ReadAllText(path))) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }
        private static void Safe(string path)
        {
            string full = Path.GetFullPath(path);
            Need(full.StartsWith(ProjectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "PATH_OUTSIDE_PROJECT");
            if ((File.Exists(full) || Directory.Exists(full)) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("LINK_PATH_REJECTED");
            for (var dir = new DirectoryInfo(Path.GetDirectoryName(full)); dir != null; dir = dir.Parent)
                if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("LINK_PATH_REJECTED");
        }
        private static void Atomic(string path, JObject value)
        {
            Safe(path); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(value.ToString(Formatting.Indented));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (IOException error)
            {
                throw new IOException("AUTHORING_ATOMIC_WRITE_FAILED: " + path, error);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
