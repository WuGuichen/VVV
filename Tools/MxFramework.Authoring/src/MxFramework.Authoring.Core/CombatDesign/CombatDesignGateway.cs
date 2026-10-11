using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>Project-local structured gateway. Unity owners consume requests; this gateway never edits authoring content.</summary>
    /// <remarks>LowFreqAlloc: authoring queries, plans and durable command submission.</remarks>
    public sealed class CombatDesignGateway
    {
        public const string Schema = "mx.combat-design.command.v1";
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private readonly string _projectRoot;
        private readonly string _root;

        public CombatDesignGateway(string projectRoot)
        {
            _projectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
            _root = Path.Combine(_projectRoot, "Temp", "MxFramework", "CombatDesign");
            CheckPath(_root);
        }

        /// <summary>Returns owner-generated snapshots with current source/evidence freshness, including offline Editor state.</summary>
        public object Query()
        {
            var snapshots = new List<object>();
            // Observe each authority file once per response. No observation survives this query;
            // planning and submission still verify their full read sets independently.
            var observations = new Dictionary<string, string>(Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            string dir = Path.Combine(_root, "snapshots");
            if (Directory.Exists(dir))
                foreach (string path in Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
                {
                    CheckPath(path);
                    string raw = File.ReadAllText(path);
                    var data = Parse(raw);
                    bool sourceCurrent = SourcesCurrent(data, observations);
                    snapshots.Add(new { snapshotHash = Hash(raw), data, sourceCurrent, evidenceCurrent = sourceCurrent && EvidenceMatchesSource(data) });
                }
            return new { schema = "mx.combat-design.query.v1", scope = "isolated-fixture-write/project-read-only", snapshots,
                editorRequiredFor = new[] { "create-fixture", "commit", "validate", "publish" },
                note = "Snapshots are generated projections. PendingEditor is not compilation, publication or Play evidence." };
        }

        /// <summary>Builds a read-set-bound typed patch. Callers use a queried handle, never authoring paths or reference IDs.</summary>
        public object Plan(string raw)
        {
            var input = Parse(raw);
            Fields(input, "handle", "snapshotHash", "thresholdDegrees", "maximumFrames", "rationale");
            string handle = Required(input, "handle");
            string hash = Required(input, "snapshotHash");
            var snapshot = FindSnapshot(handle, hash);
            if (!SourcesCurrent(snapshot)) throw new InvalidOperationException("SOURCE_CHANGED: request a fresh owner snapshot.");
            if (!snapshot.GetProperty("writable").GetBoolean()) throw new InvalidOperationException("READ_ONLY: first-phase writes are isolated fixtures only.");
            float threshold = input.GetProperty("thresholdDegrees").GetSingle();
            int frames = input.GetProperty("maximumFrames").GetInt32();
            if (float.IsNaN(threshold) || float.IsInfinity(threshold) || threshold <= 0 || threshold > 180 || frames < 1 || frames > 18000)
                throw new ArgumentException("INVALID_FACING_PATCH: threshold must be (0,180], frame budget 1..18000.");
            string rationale = Required(input, "rationale");
            if (rationale.Length > 4000) throw new ArgumentException("RATIONALE_TOO_LONG");
            var plan = new { schema = "mx.combat-design.plan.v1", handle, snapshotHash = hash,
                thresholdDegrees = threshold, maximumFrames = frames, rationale,
                affectedCards = snapshot.GetProperty("cards").EnumerateArray().Where(c => c.GetProperty("dependsOnFacing").GetBoolean()).Select(c => c.Clone()).ToArray(),
                requiredChecks = new[] { "owner-token-CAS", "source-code-read-set", "strict-source-codec", "affected-card-compile", "description/card-face", "publish-reload-hash" },
                playEvidence = "not-run", expiresUtc = DateTime.UtcNow.AddMinutes(30).ToString("O") };
            string serialized = JsonSerializer.Serialize(plan, Json);
            string token = Hash(serialized);
            string file = Path.Combine(_root, "plans", token + ".json");
            WriteOnce(file, serialized);
            return new { planToken = token, plan };
        }

        /// <summary>Queues one command, durably deduplicated by key and semantic JSON body. Owners alone report final outcomes.</summary>
        public object Submit(string raw)
        {
            var input = Parse(raw);
            Fields(input, "operation", "idempotencyKey", "planToken", "handle", "snapshotHash");
            string operation = Required(input, "operation");
            if (operation != "create-fixture" && operation != "commit" && operation != "validate" && operation != "publish" && operation != "refresh")
                throw new ArgumentException("UNKNOWN_OPERATION");
            string key = Required(input, "idempotencyKey");
            if (key.Length > 128) throw new ArgumentException("IDEMPOTENCY_KEY_TOO_LONG");
            if (operation == "commit") Fields(input, "operation", "idempotencyKey", "planToken");
            else if (operation == "validate" || operation == "publish") Fields(input, "operation", "idempotencyKey", "handle", "snapshotHash");
            else Fields(input, "operation", "idempotencyKey");
            string requestHash = Hash(Canonical(input));
            string id = Hash("command/" + key);
            string directory = Path.Combine(_root, "requests");
            CheckPath(directory);
            Directory.CreateDirectory(directory);
            string requestPath = Path.Combine(directory, id + ".json");
            string gate = Path.Combine(directory, id + ".lock");
            CheckPath(gate);
            // A completed identical retry remains valid even after its own mutation invalidated the old snapshot.
            using (new FileStream(gate, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (File.Exists(requestPath))
                {
                    var existing = Parse(File.ReadAllText(requestPath));
                    if (existing.GetProperty("requestHash").GetString() != requestHash) throw new InvalidOperationException("IDEMPOTENCY_CONFLICT");
                    return Status(id);
                }
            }
            JsonElement plan = default;
            if (operation == "commit")
            {
                string token = Required(input, "planToken");
                SafeToken(token);
                string path = Path.Combine(_root, "plans", token + ".json");
                CheckPath(path);
                string content = File.ReadAllText(path);
                if (Hash(content) != token) throw new InvalidDataException("PLAN_TAMPERED");
                plan = Parse(content);
                if (DateTime.Parse(plan.GetProperty("expiresUtc").GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind) < DateTime.UtcNow)
                    throw new InvalidOperationException("PLAN_EXPIRED");
                var snapshot = FindSnapshot(Required(plan, "handle"), Required(plan, "snapshotHash"));
                if (!SourcesCurrent(snapshot)) throw new InvalidOperationException("SOURCE_CHANGED");
            }
            else if (operation == "validate" || operation == "publish")
            {
                var snapshot = FindSnapshot(Required(input, "handle"), Required(input, "snapshotHash"));
                if (!snapshot.GetProperty("writable").GetBoolean()) throw new InvalidOperationException("READ_ONLY");
                if (!SourcesCurrent(snapshot)) throw new InvalidOperationException("SOURCE_CHANGED");
            }
            using (new FileStream(gate, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (File.Exists(requestPath))
                {
                    var existing = Parse(File.ReadAllText(requestPath));
                    if (existing.GetProperty("requestHash").GetString() != requestHash) throw new InvalidOperationException("IDEMPOTENCY_CONFLICT: key was used with a different request.");
                }
                else
                {
                    // Snapshot CAS is repeated by the Editor immediately before invoking an owner transaction.
                    var command = new { schema = Schema, operationId = id, requestHash, operation, request = input,
                        plan = plan.ValueKind == JsonValueKind.Undefined ? (object)null : plan,
                        createdUtc = DateTime.UtcNow.ToString("O") };
                    WriteOnce(requestPath, JsonSerializer.Serialize(command, Json));
                }
            }
            return Status(id);
        }

        public object Status(string operationId)
        {
            SafeToken(operationId);
            string request = Path.Combine(_root, "requests", operationId + ".json");
            string response = Path.Combine(_root, "responses", operationId + ".json");
            CheckPath(request); CheckPath(response);
            if (!File.Exists(request)) throw new ArgumentException("OPERATION_NOT_FOUND");
            if (File.Exists(response)) return Parse(File.ReadAllText(response));
            return new { operationId, status = "PendingEditor", message = "Waiting for the project Editor owner. No content write or validation evidence is confirmed." };
        }

        private JsonElement FindSnapshot(string handle, string expectedHash)
        {
            SafeToken(handle); SafeToken(expectedHash);
            string file = Path.Combine(_root, "snapshots", handle + ".json");
            CheckPath(file);
            string raw = File.ReadAllText(file);
            if (Hash(raw) != expectedHash) throw new InvalidOperationException("SNAPSHOT_CONFLICT: query again and replan.");
            var result = Parse(raw);
            if (Required(result, "handle") != handle) throw new InvalidDataException("SNAPSHOT_HANDLE_MISMATCH");
            return result;
        }

        private bool SourcesCurrent(JsonElement snapshot, Dictionary<string, string> observations = null)
        {
            if (!snapshot.TryGetProperty("readSet", out var readSet)) return false;
            foreach (var entry in readSet.EnumerateArray())
            {
                string relative = Required(entry, "path");
                string path = Path.GetFullPath(Path.Combine(_projectRoot, relative));
                if (!path.StartsWith(_projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
                if (observations == null || !observations.TryGetValue(path, out string observed))
                {
                    CheckPath(path);
                    observed = File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
                    if (observations != null) observations[path] = observed;
                }
                if (observed != Required(entry, "hash")) return false;
            }
            return readSet.GetArrayLength() > 0;
        }

        private static bool EvidenceMatchesSource(JsonElement snapshot)
        {
            if (!snapshot.TryGetProperty("evidence", out var evidence) || evidence.ValueKind != JsonValueKind.Object) return false;
            return evidence.TryGetProperty("sourceFingerprint", out var fingerprint)
                && snapshot.TryGetProperty("sourceFingerprint", out var current) && fingerprint.GetString() == current.GetString();
        }

        private void CheckPath(string path)
        {
            // Walk existing ancestors too: a project-local junction must not redirect command writes outside the project.
            for (var info = new DirectoryInfo(Path.GetFullPath(path)); info != null; info = info.Parent)
                if ((File.Exists(info.FullName) || Directory.Exists(info.FullName)) && (File.GetAttributes(info.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("UNSAFE_REPARSE_PATH: " + info.FullName);
        }

        private void WriteOnce(string path, string text)
        {
            CheckPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    if (File.ReadAllText(path) != text) throw new IOException("WRITE_ONCE_CONFLICT");
                }
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private static JsonElement Parse(string raw)
        {
            if (raw == null || Encoding.UTF8.GetByteCount(raw) > 1024 * 1024) throw new ArgumentException("REQUEST_TOO_LARGE");
            using (var doc = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 64 }))
            {
                RejectDuplicateFields(doc.RootElement);
                return doc.RootElement.Clone();
            }
        }
        private static void RejectDuplicateFields(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                { if (!names.Add(property.Name)) throw new ArgumentException("DUPLICATE_FIELD: " + property.Name); RejectDuplicateFields(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var value in element.EnumerateArray()) RejectDuplicateFields(value);
        }
        private static void Fields(JsonElement value, params string[] allowed)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("REQUEST_OBJECT_REQUIRED");
            foreach (var property in value.EnumerateObject()) if (!allowed.Contains(property.Name)) throw new ArgumentException("UNKNOWN_FIELD: " + property.Name);
        }
        private static string Required(JsonElement input, string name)
        {
            if (!input.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new ArgumentException("REQUIRED_FIELD: " + name);
            return value.GetString();
        }
        private static void SafeToken(string value)
        {
            if (value == null || value.Length != 64 || value.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')))
                throw new ArgumentException("INVALID_HANDLE");
        }
        private static string Canonical(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
                return "{" + string.Join(",", value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}";
            if (value.ValueKind == JsonValueKind.Array) return "[" + string.Join(",", value.EnumerateArray().Select(Canonical)) + "]";
            return value.GetRawText();
        }
        public static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
