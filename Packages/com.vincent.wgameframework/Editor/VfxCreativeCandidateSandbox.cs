using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor
{
    /// <summary>Path, receipt, and content-integrity helpers for the importable VFX candidate sandbox.</summary>
    /// <remarks>Editor-only and AllocByDesign. All paths are validated before any AssetDatabase mutation.</remarks>
    internal static class VfxCandidateSandbox
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static bool TryGetSessionRoot(string sessionId, out string sessionRoot, out string error)
        {
            sessionRoot = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                error = "VFX candidate sessionId is required.";
                return false;
            }

            for (int i = 0; i < sessionId.Length; i++)
            {
                char value = sessionId[i];
                bool allowed = value >= 'a' && value <= 'z'
                    || value >= 'A' && value <= 'Z'
                    || value >= '0' && value <= '9'
                    || value == '-'
                    || value == '_';
                if (!allowed)
                {
                    error = "VFX candidate sessionId may contain only ASCII letters, digits, '-' and '_': " + sessionId;
                    return false;
                }
            }

            sessionRoot = VfxCandidateProtocol.ResolvedSandboxRoot + "/" + sessionId;
            return true;
        }

        public static bool IsWithinSessionRoot(string assetPath, string sessionRoot)
        {
            string path = NormalizeAssetPath(assetPath);
            string root = NormalizeAssetPath(sessionRoot).TrimEnd('/');
            return path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        public static bool IsCandidateRoot(string assetPath)
        {
            string path = NormalizeAssetPath(assetPath);
            string root = VfxCandidateProtocol.ResolvedSandboxRoot.TrimEnd('/');
            return string.Equals(path, root, StringComparison.Ordinal)
                || path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        public static bool TryValidateDestinationRoot(string assetPath, out string normalized, out string error)
        {
            normalized = NormalizeAssetPath(assetPath).TrimEnd('/');
            error = string.Empty;
            if (!normalized.StartsWith("Assets/VFX/", StringComparison.Ordinal)
                || normalized.Contains("../")
                || normalized.Contains("/./")
                || IsCandidateRoot(normalized))
            {
                error = "VFX promotion destination must stay under Assets/VFX and outside the candidate sandbox: " + normalized;
                return false;
            }

            return true;
        }

        public static bool IsFormalVfxDestination(string assetPath)
        {
            string path = NormalizeAssetPath(assetPath);
            return path.StartsWith("Assets/VFX/", StringComparison.Ordinal)
                && !IsCandidateRoot(path)
                && IsSafeProjectRelativePath(path);
        }

        public static bool IsControlledEvidencePath(string path)
        {
            string normalized = NormalizeAssetPath(path);
            string root = VfxCandidateProtocol.AuthoritativeEvidenceRoot.TrimEnd('/');
            return (string.Equals(normalized, root, StringComparison.Ordinal) || normalized.StartsWith(root + "/", StringComparison.Ordinal))
                && IsSafeProjectRelativePath(normalized);
        }

        public static bool TryGetCandidateSessionRoot(string assetPath, out string sessionRoot, out string sessionId)
        {
            sessionRoot = string.Empty;
            sessionId = string.Empty;
            string normalized = NormalizeAssetPath(assetPath);
            string prefix = VfxCandidateProtocol.ResolvedSandboxRoot.TrimEnd('/') + "/";
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            int separator = normalized.IndexOf('/', prefix.Length);
            sessionId = separator < 0 ? normalized.Substring(prefix.Length) : normalized.Substring(prefix.Length, separator - prefix.Length);
            if (!TryGetSessionRoot(sessionId, out sessionRoot, out _))
                return false;
            return string.Equals(normalized, sessionRoot, StringComparison.Ordinal) || normalized.StartsWith(sessionRoot + "/", StringComparison.Ordinal);
        }

        public static bool FixedDraftEvidenceAssetExists(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            return File.Exists(normalized)
                || File.Exists(normalized + ".meta")
                || AssetDatabase.LoadMainAssetAtPath(normalized) != null;
        }

        public static bool TryDeleteFixedDraftEvidenceAsset(string assetPath, out bool deleted, out string error)
        {
            deleted = false;
            error = string.Empty;
            string normalized = NormalizeAssetPath(assetPath);
            string fileName = Path.GetFileName(normalized);
            if (!string.Equals(fileName, "candidate_manifest.json", StringComparison.Ordinal) &&
                !string.Equals(fileName, "candidate_receipt.json", StringComparison.Ordinal))
            {
                error = "Only fixed Draft candidate manifest/receipt evidence may be deleted: " + normalized;
                return false;
            }
            if (!TryGetCandidateSessionRoot(normalized, out string sessionRoot, out _))
            {
                error = "Fixed Draft evidence path is outside a candidate session: " + normalized;
                return false;
            }
            string parent = NormalizeAssetPath(Path.GetDirectoryName(normalized) ?? string.Empty).TrimEnd('/');
            if (!string.Equals(parent, sessionRoot, StringComparison.Ordinal))
            {
                error = "Fixed Draft evidence must be stored at the candidate session root: " + normalized;
                return false;
            }
            try
            {
                if (!FixedDraftEvidenceAssetExists(normalized))
                    return true;
                if (!AssetDatabase.DeleteAsset(normalized))
                {
                    error = "Unity could not delete fixed Draft evidence: " + normalized;
                    return false;
                }
                if (FixedDraftEvidenceAssetExists(normalized))
                {
                    error = "Fixed Draft evidence still exists after rollback: " + normalized;
                    return false;
                }
                deleted = true;
                return true;
            }
            catch (Exception exception)
            {
                error = "Fixed Draft evidence rollback threw for " + normalized + ": " + exception.Message;
                return false;
            }
        }

        public static void EnsureAssetFolder(string path, List<string> createdFolders = null)
        {
            string normalized = NormalizeAssetPath(path).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "Assets", StringComparison.Ordinal))
                return;
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Asset folder must remain under Assets/: " + normalized);

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException("Unity failed to create VFX candidate folder: " + next);
                    createdFolders?.Add(next);
                }
                current = next;
            }
        }

        public static string WriteJsonAsset<T>(string assetPath, T value)
        {
            string normalized = NormalizeAssetPath(assetPath);
            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            EnsureAssetFolder(parent);
            string json = JsonConvert.SerializeObject(value, Formatting.Indented) + Environment.NewLine;
            File.WriteAllText(normalized, json, Utf8WithoutBom);
            AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return ComputeAssetFileHash(normalized);
        }

        public static string WriteJsonFile<T>(string path, T value)
        {
            string normalized = NormalizeAssetPath(path);
            if (!IsSafeProjectRelativePath(normalized))
                throw new InvalidOperationException("VFX JSON output path is invalid or outside the controlled project scope: " + normalized);
            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal))
                EnsureAssetFolder(parent);
            else if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            string json = JsonConvert.SerializeObject(value, Formatting.Indented) + Environment.NewLine;
            File.WriteAllText(normalized, json, Utf8WithoutBom);
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal))
                AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return ComputeAssetFileHash(normalized);
        }

        public static T ReadJsonAsset<T>(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (!File.Exists(normalized))
                throw new FileNotFoundException("VFX candidate JSON asset is missing.", normalized);
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(normalized));
        }

        public static string ComputePlanHash(VfxCandidatePlan plan)
        {
            string canonical = JsonConvert.SerializeObject(plan, Formatting.None);
            return ComputeHash(Utf8WithoutBom.GetBytes(canonical));
        }

        public static string ComputeAssetFileHash(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (!File.Exists(normalized))
                return string.Empty;
            return ComputeHash(File.ReadAllBytes(normalized));
        }

        public static string ComputeCoreDocumentHash<T>(T value, params string[] excludedProperties)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return ComputeCoreDocumentHash(JObject.FromObject(value), excludedProperties);
        }

        public static string ComputeCoreDocumentHash(JObject document, params string[] excludedProperties)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (excludedProperties != null)
            {
                for (int i = 0; i < excludedProperties.Length; i++)
                    if (!string.IsNullOrWhiteSpace(excludedProperties[i]))
                        excluded.Add(excludedProperties[i]);
            }
            var builder = new StringBuilder(2048);
            AppendCanonical(builder, document, excluded);
            return ComputeHash(Utf8WithoutBom.GetBytes(builder.ToString()));
        }

        public static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal))
                return false;
            for (int i = 7; i < value.Length; i++)
            {
                char character = value[i];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F')))
                    return false;
            }
            return true;
        }

        public static bool IsUnityHash128(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F')))
                    return false;
            }
            return true;
        }

        public static bool HasErrorDiagnostics(JArray diagnostics)
        {
            if (diagnostics == null)
                return false;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                JObject diagnostic = diagnostics[i] as JObject;
                if (string.Equals(diagnostic?.Value<string>("severity"), "Error", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string ComputeHash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            var builder = new StringBuilder(hash.Length * 2 + 7);
            builder.Append("sha256:");
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        public static VfxCandidateAssetReceipt CreateAssetReceipt(string kind, string assetPath, string sessionRoot)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (!IsWithinSessionRoot(normalized, sessionRoot))
                throw new InvalidOperationException("Candidate asset escaped its session root: " + normalized);
            string relative = normalized.Substring(NormalizeAssetPath(sessionRoot).TrimEnd('/').Length + 1);
            return new VfxCandidateAssetReceipt
            {
                Kind = kind ?? string.Empty,
                AssetPath = normalized,
                RelativePath = relative,
                Guid = AssetDatabase.AssetPathToGUID(normalized),
                ContentHash = ComputeAssetFileHash(normalized),
                DependencyHash = AssetDatabase.GetAssetDependencyHash(normalized).ToString()
            };
        }

        public static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }

        private static bool IsSafeProjectRelativePath(string value)
        {
            string path = NormalizeAssetPath(value);
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.StartsWith("/", StringComparison.Ordinal))
                return false;
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 || string.Equals(segments[i], ".", StringComparison.Ordinal) || string.Equals(segments[i], "..", StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static void AppendCanonical(StringBuilder builder, JToken token, HashSet<string> excluded)
        {
            if (token is JObject owner)
            {
                builder.Append('{');
                var properties = new List<JProperty>();
                foreach (JProperty property in owner.Properties())
                    if (!excluded.Contains(property.Name))
                        properties.Add(property);
                properties.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
                for (int i = 0; i < properties.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append(JsonConvert.ToString(properties[i].Name)).Append(':');
                    AppendCanonical(builder, properties[i].Value, excluded);
                }
                builder.Append('}');
                return;
            }
            if (token is JArray array)
            {
                builder.Append('[');
                for (int i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    AppendCanonical(builder, array[i], excluded);
                }
                builder.Append(']');
                return;
            }
            builder.Append(token == null || token.Type == JTokenType.Null
                ? "null"
                : token.ToString(Formatting.None));
        }
    }
}
