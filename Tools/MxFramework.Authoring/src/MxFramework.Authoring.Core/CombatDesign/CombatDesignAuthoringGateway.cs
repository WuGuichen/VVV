using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace MxFramework.Authoring
{
    /// <summary>Durable transport for the Unity tactical authoring owner; never writes card sources or catalogs.</summary>
    /// <remarks>LowFreqAlloc: explicit local authoring requests only.</remarks>
    public sealed class CombatDesignAuthoringGateway
    {
        private readonly string _root;
        public CombatDesignAuthoringGateway(string projectRoot)
        {
            _root = Path.GetFullPath(Path.Combine(projectRoot, "Temp/MxFramework/CombatDesign/authoring"));
            CheckPath(_root);
        }

        public object Query()
        {
            JsonElement? owner = Read("owner.json");
            bool online = owner.HasValue && owner.Value.TryGetProperty("updatedUtc", out var utc)
                && DateTimeOffset.TryParse(utc.GetString(), out var updated)
                && DateTimeOffset.UtcNow - updated < TimeSpan.FromSeconds(12);
            bool ready = online && owner.Value.GetProperty("ready").GetBoolean();
            return new { schema = "mx.combat-design.authoring.connection.v1", online, ready, owner,
                message = !online ? "Unity 作者连接不可用。打开本项目的 Editor 后刷新；请求尚未执行。"
                    : ready ? "Unity 作者已连接" : "Unity 正在编译、更新或运行；请等待编辑模式就绪。" };
        }

        public object Submit(string json)
        {
            using var parsed = JsonDocument.Parse(json);
            var input = parsed.RootElement;
            Fields(input, "operation", "idempotencyKey", "request");
            string operation = Required(input, "operation"), key = Required(input, "idempotencyKey");
            if (!new[] { "refresh", "open", "save", "preflight", "publish", "reconcile", "levels.refresh", "levels.open", "levels.save", "levels.preflight", "levels.publish" }.Contains(operation))
                throw new ArgumentException("UNKNOWN_AUTHORING_OPERATION");
            if (key.Length > 128) throw new ArgumentException("IDEMPOTENCY_KEY_TOO_LONG");
            if (!input.TryGetProperty("request", out var request) || request.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("AUTHORING_REQUEST_REQUIRED");
            string id = Hash("authoring/" + key), digest = Hash(Canonical(input));
            string directory = Path.Combine(_root, "requests");
            CheckPath(directory); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, id + ".json");
            CheckPath(path); CheckPath(path + ".lock");
            using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (File.Exists(path))
                {
                    using var existing = JsonDocument.Parse(File.ReadAllText(path));
                    if (existing.RootElement.GetProperty("requestHash").GetString() != digest)
                        throw new InvalidOperationException("IDEMPOTENCY_CONFLICT: 同一请求键不能提交不同内容。");
                    return Status(id);
                }
                var connection = JsonSerializer.SerializeToElement(Query());
                if (!connection.GetProperty("ready").GetBoolean())
                    throw new InvalidOperationException("OWNER_UNAVAILABLE: " + connection.GetProperty("message").GetString());
                var command = new { schema = "mx.combat-design.authoring.command.v1", operationId = id,
                    requestHash = digest, operation, request, createdUtc = DateTime.UtcNow.ToString("O") };
                string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
                CheckPath(temporary);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                File.Move(temporary, path);
            }
            return Status(id);
        }

        public object Status(string id)
        {
            if (id == null || id.Length != 64 || id.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("INVALID_OPERATION_ID");
            var response = Read("responses/" + id + ".json");
            if (response.HasValue) return response.Value;
            var command = Read("requests/" + id + ".json");
            if (!command.HasValue) throw new ArgumentException("OPERATION_NOT_FOUND");
            return new { operationId = id, status = "PendingEditor", connection = Query(),
                message = "等待 Unity owner。不要重发新请求；原请求的最终结果会保留。" };
        }

        private JsonElement? Read(string relative)
        {
            string path = Path.Combine(_root, relative);
            // The Editor atomically replaces its heartbeat while the external tool polls it.
            // Windows replacement opens the old file for writing and deletion; the reader
            // keeps its opened snapshot even after the directory entry has been replaced.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    CheckPath(path);
                    if (!File.Exists(path)) return null;
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    using var doc = JsonDocument.Parse(reader.ReadToEnd()); return doc.RootElement.Clone();
                }
                catch (FileNotFoundException)
                {
                    // The directory entry may change between path validation and opening.
                    if (attempt >= 4) return null;
                    Thread.Sleep(10);
                }
                // ReplaceFile itself can briefly hold an exclusive handle on Windows.
                // Retry only sharing/lock conflicts, never malformed JSON or permission failures.
                catch (IOException error) when (attempt < 4 && ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33))
                {
                    Thread.Sleep(10);
                }
            }
        }
        private static void CheckPath(string path)
        {
            for (var current = new DirectoryInfo(Path.GetDirectoryName(path)); current != null; current = current.Parent)
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("LINK_PATH_REJECTED");
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("LINK_PATH_REJECTED");
        }
        private static void Fields(JsonElement value, params string[] allowed)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("OBJECT_REQUIRED");
            var names = value.EnumerateObject().Select(p => p.Name).ToArray();
            if (names.Distinct().Count() != names.Length || names.Any(n => !allowed.Contains(n))) throw new ArgumentException("UNKNOWN_OR_DUPLICATE_FIELD");
        }
        private static string Required(JsonElement value, string name)
            => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())
                ? item.GetString()! : throw new ArgumentException(name + " is required");
        private static string Canonical(JsonElement value) => value.ValueKind switch {
            JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", value.EnumerateArray().Select(Canonical)) + "]", _ => value.GetRawText() };
        private static string Hash(string value)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
    }
}
