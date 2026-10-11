using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    /// <summary>Stable camelCase JSON support for VFX creative-flywheel contracts.</summary>
    public static class VfxCreativeFlywheelJson
    {
        /// <summary>Deserializes one authoring contract.</summary>
        /// <remarks>AllocByDesign. Intended for CLI, Editor, and tests only.</remarks>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("VFX authoring JSON is empty.", nameof(json));
            T value = JsonSerializer.Deserialize<T>(json, CreateOptions());
            if (value == null)
                throw new JsonException("VFX authoring JSON produced a null document.");
            return value;
        }

        /// <summary>Reads and deserializes one authoring contract file.</summary>
        /// <remarks>AllocByDesign. Performs low-frequency file IO and JSON parsing.</remarks>
        public static T LoadFile<T>(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("VFX authoring contract file is missing.", path);
            return Deserialize<T>(File.ReadAllText(path));
        }

        /// <summary>Serializes one authoring contract using stable camelCase property names.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public static string Serialize<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return JsonSerializer.Serialize(value, CreateOptions());
        }

        internal static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }

    /// <summary>Canonical SHA-256 hashing for immutable VFX authoring plans, reports, and receipts.</summary>
    public static class VfxCreativeFlywheelHash
    {
        /// <summary>Computes a canonical JSON hash while excluding derived property names.</summary>
        /// <remarks>AllocByDesign. Serializes and recursively orders JSON object properties.</remarks>
        public static string Compute(object value, params string[] excludedPropertyNames)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (excludedPropertyNames != null)
                for (int i = 0; i < excludedPropertyNames.Length; i++)
                    if (!string.IsNullOrWhiteSpace(excludedPropertyNames[i]))
                        excluded.Add(excludedPropertyNames[i]);
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value, VfxCreativeFlywheelJson.CreateOptions()));
            var builder = new StringBuilder(2048);
            AppendCanonical(builder, document.RootElement, excluded);
            return ComputeText(builder.ToString());
        }

        /// <summary>Computes a lowercase SHA-256 content hash for text.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public static string ComputeText(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return "sha256:" + ToLowerHex(bytes);
        }

        /// <summary>Computes a lowercase SHA-256 content hash for a file.</summary>
        /// <remarks>LowFreqAlloc. Reads the file once.</remarks>
        public static string ComputeFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return "sha256:" + ToLowerHex(sha256.ComputeHash(stream));
        }

        /// <summary>Returns true for a prefixed 256-bit hexadecimal hash.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal))
                return false;
            for (int i = 7; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        /// <summary>Returns true for Unity's compact 128-bit asset GUID representation.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool IsUnityGuid(string value) => IsHex128(value);

        /// <summary>Returns true for Unity's compact Hash128 dependency fingerprint representation.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static bool IsUnityDependencyHash(string value) => IsHex128(value);

        public static string ComputeBriefHash(VfxCreativeBrief value) => Compute(value);
        public static string ComputeReuseDecisionHash(VfxReuseDecision value) => Compute(value, "decisionHash", "diagnostics", "blocked");
        public static string ComputeGenerationPlanHash(VfxGenerationPlan value) => Compute(value, "planHash", "diagnostics", "blocked");
        public static string ComputeCandidateManifestHash(VfxCandidateManifest value) => Compute(value, "manifestHash");
        public static string ComputeCandidateReceiptHash(VfxCandidateGenerationReceipt value) => Compute(value, "receiptHash");
        public static string ComputeEvaluationReportHash(VfxEvaluationReport value) => Compute(value, "reportHash", "diagnostics", "blocked");
        public static string ComputePromotionPlanHash(VfxPromotionPlan value) => Compute(value, "planHash", "diagnostics", "blocked");
        public static string ComputePromotionReceiptHash(VfxPromotionReceipt value) => Compute(value, "receiptHash");
        public static string ComputeRegistrationPlanHash(VfxPublishedRegistrationPlan value) => Compute(value, "planHash", "diagnostics", "blocked");

        private static void AppendCanonical(StringBuilder builder, JsonElement value, HashSet<string> excluded)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    builder.Append('{');
                    var properties = new List<JsonProperty>();
                    foreach (JsonProperty property in value.EnumerateObject())
                        if (!excluded.Contains(property.Name))
                            properties.Add(property);
                    properties.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
                    for (int i = 0; i < properties.Count; i++)
                    {
                        if (i > 0) builder.Append(',');
                        builder.Append(JsonSerializer.Serialize(properties[i].Name)).Append(':');
                        AppendCanonical(builder, properties[i].Value, excluded);
                    }
                    builder.Append('}');
                    break;
                case JsonValueKind.Array:
                    builder.Append('[');
                    int index = 0;
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        if (index++ > 0) builder.Append(',');
                        AppendCanonical(builder, item, excluded);
                    }
                    builder.Append(']');
                    break;
                case JsonValueKind.String:
                    builder.Append(JsonSerializer.Serialize(value.GetString()));
                    break;
                case JsonValueKind.Number:
                    if (value.TryGetInt64(out long integer))
                        builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    else if (value.TryGetDecimal(out decimal decimalValue))
                        builder.Append(decimalValue.ToString("G29", CultureInfo.InvariantCulture));
                    else
                        builder.Append(value.GetDouble().ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonValueKind.True:
                    builder.Append("true");
                    break;
                case JsonValueKind.False:
                    builder.Append("false");
                    break;
                default:
                    builder.Append("null");
                    break;
            }
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool IsHex128(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }
    }

    /// <summary>Unity-importable candidate path policy shared by planning and receipt validation.</summary>
    public static class VfxCandidatePathPolicy
    {
        public const string PhysicalCandidateRoot = "Assets/VFX/MxCandidates";
        public const string LogicalIgnoredCandidateRoot = "Assets/VFX/.mx-candidates";

        /// <summary>Builds the physical Unity-importable sandbox root for a stable session id.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public static string BuildSandboxRoot(string sessionId)
        {
            if (!IsStableSegment(sessionId))
                throw new ArgumentException("VFX candidate session id must be a stable path segment.", nameof(sessionId));
            return PhysicalCandidateRoot + "/" + sessionId;
        }

        /// <summary>Checks that a project-relative asset path is contained by the requested candidate sandbox.</summary>
        /// <remarks>LowFreqAlloc. Normalizes separators.</remarks>
        public static bool IsUnderSandbox(string assetPath, string sandboxRoot)
        {
            string path = NormalizeProjectPath(assetPath);
            string root = NormalizeProjectPath(sandboxRoot).TrimEnd('/');
            return IsProjectAssetPath(path) && root.StartsWith(PhysicalCandidateRoot + "/", StringComparison.Ordinal) &&
                   (string.Equals(path, root, StringComparison.Ordinal) || path.StartsWith(root + "/", StringComparison.Ordinal));
        }

        /// <summary>Checks a safe project-relative Unity asset path with no traversal or dot-prefixed candidate root.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public static bool IsProjectAssetPath(string value)
        {
            string path = NormalizeProjectPath(value);
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.StartsWith(LogicalIgnoredCandidateRoot + "/", StringComparison.Ordinal))
                return false;
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
                if (segments[i].Length == 0 || string.Equals(segments[i], ".", StringComparison.Ordinal) || string.Equals(segments[i], "..", StringComparison.Ordinal))
                    return false;
            return true;
        }

        /// <summary>Checks a formal destination outside all candidate sandboxes.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public static bool IsFormalDestination(string value)
        {
            string path = NormalizeProjectPath(value);
            return IsProjectAssetPath(path) && path.StartsWith("Assets/VFX/", StringComparison.Ordinal) && !IsCandidateSandboxAssetPath(path);
        }

        /// <summary>Returns true for physical or legacy logical candidate-sandbox assets that scanners must ignore.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public static bool IsCandidateSandboxAssetPath(string value)
        {
            string path = NormalizeProjectPath(value).TrimEnd('/');
            return string.Equals(path, PhysicalCandidateRoot, StringComparison.Ordinal) || path.StartsWith(PhysicalCandidateRoot + "/", StringComparison.Ordinal) ||
                   string.Equals(path, LogicalIgnoredCandidateRoot, StringComparison.Ordinal) || path.StartsWith(LogicalIgnoredCandidateRoot + "/", StringComparison.Ordinal);
        }

        /// <summary>Resolves a safe project-relative path under the repository root.</summary>
        /// <remarks>LowFreqAlloc. Does not create or write the path.</remarks>
        public static bool TryResolveUnderProject(string repositoryRoot, string projectPath, out string absolutePath)
        {
            absolutePath = string.Empty;
            if (string.IsNullOrWhiteSpace(repositoryRoot) || !IsProjectAssetPath(projectPath))
                return false;
            string root = Path.GetFullPath(repositoryRoot);
            string candidate = Path.GetFullPath(Path.Combine(root, NormalizeProjectPath(projectPath).Replace('/', Path.DirectorySeparatorChar)));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            absolutePath = candidate;
            return true;
        }

        public static string NormalizeProjectPath(string value) => (value ?? string.Empty).Trim().Replace('\\', '/');

        private static bool IsStableSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
                    return false;
            }
            return true;
        }
    }
}
