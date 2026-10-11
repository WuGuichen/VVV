using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor
{
    /// <summary>Local build evidence for bundles retired by committed artifact generations.</summary>
    internal static class GlobalResourceBundleResidueLedger
    {
        public const string LedgerPath = "Library/MxFramework/ResourceBundles/residue-ledger.json";
        public const string ReportPath = "Library/MxFramework/ResourceBundles/residue-report.txt";
        public const string ReceiptPath = "Library/MxFramework/ResourceBundles/cleanup-receipts.jsonl";
        private const string BundlePrefix = "MxFramework/Resources/Bundles/";

        public sealed class Context
        {
            internal Ledger Data;
            internal string FilePath;
            public bool AutoCleanupEnabled => Data != null && Data.trusted;
            public string Issue { get; internal set; }
        }

        public static Context Prepare(string profileId, string target, string markerPath = null,
            string artifactRoot = null, string ledgerPath = null)
        {
            markerPath = Path.GetFullPath(markerPath ?? GlobalPlayerResourceBuildProfileBuilder.ArtifactSetPath);
            artifactRoot = Path.GetFullPath(artifactRoot ?? "Assets/StreamingAssets");
            ledgerPath = Path.GetFullPath(ledgerPath ?? LedgerPath);
            var context = new Context { FilePath = ledgerPath };
            try
            {
                if (File.Exists(ledgerPath))
                    context.Data = ReadLedger(ledgerPath);
                else
                {
                    // An existing marker can prove its own files, but cannot reconstruct older failed cleanups.
                    context.Data = new Ledger { trusted = !File.Exists(markerPath) };
                    if (File.Exists(markerPath))
                        context.Issue = "Ownership ledger was absent beside an existing generation; automatic cleanup is disabled.";
                    Save(context);
                }
                if (File.Exists(markerPath))
                    RecordCommittedMarker(context, markerPath, artifactRoot);
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is ResourceCatalogException
                || ex is InvalidOperationException
                || ex is UnauthorizedAccessException)
            {
                context.Data = null;
                context.Issue = "Ownership ledger or committed marker could not be verified: " + ex.Message;
            }
            return context;
        }

        public static void RecordCommittedMarker(Context context, string markerPath = null, string artifactRoot = null)
        {
            if (context?.Data == null)
                return;
            markerPath = Path.GetFullPath(markerPath ?? GlobalPlayerResourceBuildProfileBuilder.ArtifactSetPath);
            artifactRoot = Path.GetFullPath(artifactRoot ?? "Assets/StreamingAssets");
            GeneratedResourceArtifactSet verified = GeneratedResourceArtifactSetLoader.LoadFromFile(markerPath, artifactRoot);
            string generation = HashFile(markerPath);
            Scope scope = GetScope(context.Data, verified.ProfileId, verified.BuildTarget, create: true);
            if (scope.currentGeneration == generation)
                return;
            JObject marker = JObject.Parse(File.ReadAllText(markerPath));
            var current = new Dictionary<string, Owned>(StringComparer.Ordinal);
            foreach (JToken artifact in marker["artifacts"] ?? new JArray())
            {
                if ((string)artifact["kind"] != "bundle") continue;
                string relative = (string)artifact["relativePath"];
                RequireOwnedPath(relative, verified.BuildTarget, artifactRoot);
                if (!current.TryAdd(relative, new Owned
                {
                    relativePath = relative,
                    sha256 = (string)artifact["sha256"],
                    generation = generation
                }))
                    throw new InvalidOperationException("Duplicate owned bundle path: " + relative);
            }

            string previous = scope.currentGeneration;
            if (!string.IsNullOrEmpty(previous))
            {
                foreach (Owned old in scope.current)
                {
                    if (current.ContainsKey(old.relativePath)) continue;
                    if (scope.pending.Any(item => item.relativePath == old.relativePath)) continue;
                    scope.pending.Add(new Pending
                    {
                        relativePath = old.relativePath,
                        sha256 = old.sha256,
                        generatedBy = old.generation,
                        retiredBy = generation,
                        status = "eligible"
                    });
                }
                // A pre-ledger marker was inventoried before this new commit. From this
                // generation onward, newly retired paths have persisted ownership proof.
                context.Data.trusted = true;
            }
            for (int i = scope.pending.Count - 1; i >= 0; i--)
            {
                Pending pending = scope.pending[i];
                if (!current.ContainsKey(pending.relativePath)) continue;
                AppendReceipt(context, scope, pending, "reReferenced", "Current committed generation references this path.");
                scope.pending.RemoveAt(i);
            }
            scope.current = current.Values.OrderBy(value => value.relativePath, StringComparer.Ordinal).ToList();
            scope.currentGeneration = generation;
            if (!scope.committedGenerations.Contains(generation)) scope.committedGenerations.Add(generation);
            Save(context);
        }

        public static void CleanupCommitted(Context context, string markerPath = null,
            string artifactRoot = null, Func<string, bool> deleteAsset = null)
        {
            if (context?.Data == null || !context.Data.trusted) return;
            markerPath = Path.GetFullPath(markerPath ?? GlobalPlayerResourceBuildProfileBuilder.ArtifactSetPath);
            artifactRoot = Path.GetFullPath(artifactRoot ?? "Assets/StreamingAssets");
            if (!File.Exists(markerPath)) return;
            GeneratedResourceArtifactSet marker = GeneratedResourceArtifactSetLoader.LoadFromFile(markerPath, artifactRoot);
            Scope scope = GetScope(context.Data, marker.ProfileId, marker.BuildTarget, create: false);
            if (scope == null || scope.currentGeneration != HashFile(markerPath)) return;
            deleteAsset ??= fullPath => AssetDatabase.DeleteAsset(ToUnityAssetPath(fullPath));
            for (int i = scope.pending.Count - 1; i >= 0; i--)
            {
                Pending pending = scope.pending[i];
                if (!scope.committedGenerations.Contains(pending.retiredBy)) continue;
                if (scope.current.Any(item => item.relativePath == pending.relativePath))
                {
                    AppendReceipt(context, scope, pending, "reReferenced", "Current generation references this path.");
                    scope.pending.RemoveAt(i);
                    Save(context);
                    continue;
                }
                try
                {
                    string path = RequireOwnedPath(pending.relativePath, scope.target, artifactRoot);
                    if (!File.Exists(path))
                    {
                        AppendReceipt(context, scope, pending, "alreadyAbsent", "File was absent at cleanup.");
                        scope.pending.RemoveAt(i);
                    }
                    else if (!string.Equals(HashFile(path), pending.sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        pending.status = "blockedHashMismatch";
                        pending.lastFailure = "File content changed after retirement.";
                        pending.attempts++;
                    }
                    else
                    {
                        if (!deleteAsset(path) || File.Exists(path))
                            throw new IOException("Unity did not remove the owned bundle and its .meta: " + path);
                        AppendReceipt(context, scope, pending, "cleaned", "Matching retired file and .meta removed.");
                        scope.pending.RemoveAt(i);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    pending.status = "failed";
                    pending.lastFailure = ex.Message;
                    pending.attempts++;
                }
                Save(context);
            }
        }

        public static string CreateReport(string profileId, string target, string markerPath = null,
            string artifactRoot = null, string ledgerPath = null)
        {
            markerPath = Path.GetFullPath(markerPath ?? GlobalPlayerResourceBuildProfileBuilder.ArtifactSetPath);
            artifactRoot = Path.GetFullPath(artifactRoot ?? "Assets/StreamingAssets");
            ledgerPath = Path.GetFullPath(ledgerPath ?? LedgerPath);
            Ledger ledger = null;
            string ledgerIssue = null;
            try { if (File.Exists(ledgerPath)) ledger = ReadLedger(ledgerPath); }
            catch (Exception ex) { ledgerIssue = ex.Message; }
            Scope scope = ledger == null ? null : GetScope(ledger, profileId, target, create: false);
            var current = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(markerPath))
                {
                    GeneratedResourceArtifactSet verified = GeneratedResourceArtifactSetLoader.LoadFromFile(markerPath, artifactRoot);
                    if (verified.ProfileId == profileId && verified.BuildTarget == target)
                        foreach (JToken artifact in JObject.Parse(File.ReadAllText(markerPath))["artifacts"] ?? new JArray())
                            if ((string)artifact["kind"] == "bundle") current.Add((string)artifact["relativePath"]);
                }
            }
            catch (Exception ex) { ledgerIssue = "Current marker invalid: " + ex.Message; }
            var builder = new StringBuilder("Global Bundle residue report\n");
            builder.Append("profileId: ").AppendLine(profileId);
            builder.Append("buildTarget: ").AppendLine(target);
            builder.Append("ownershipState: ").AppendLine(ledgerIssue ?? (ledger == null ? "missing" : ledger.trusted ? "trusted" : "untrusted"));
            builder.Append("currentGeneration: ").AppendLine(scope?.currentGeneration ?? string.Empty);
            if (scope != null)
                foreach (Pending pending in scope.pending)
                    builder.Append("pending: ").Append(pending.status).Append(' ').Append(pending.relativePath)
                        .Append(" generatedBy=").Append(pending.generatedBy)
                        .Append(" retiredBy=").Append(pending.retiredBy)
                        .Append(" attempts=").Append(pending.attempts)
                        .Append(" failure=").AppendLine(pending.lastFailure ?? string.Empty);
            string bundleRoot = Path.Combine(artifactRoot, BundlePrefix.Replace('/', Path.DirectorySeparatorChar));
            int foundCount = 0;
            if (Directory.Exists(bundleRoot))
            {
                foreach (string file in Directory.GetFiles(bundleRoot, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = Path.GetRelativePath(artifactRoot, file).Replace('\\', '/');
                    string status = current.Contains(relative) ? "current"
                        : !relative.StartsWith(BundlePrefix + target + "/", StringComparison.Ordinal) ? "otherTarget"
                        : scope != null && scope.pending.Any(item => item.relativePath == relative) ? "retired"
                        : scope != null && scope.current.Any(item => item.relativePath == relative) ? "inactiveGeneration"
                        : ledger != null && ledger.scopes.Any(item => item != scope && item.target == target
                            && (item.current.Any(owned => owned.relativePath == relative)
                                || item.pending.Any(pending => pending.relativePath == relative))) ? "otherProfile"
                        : "unknown";
                    if (status != "current" && status != "otherTarget") foundCount++;
                    builder.Append("file: ").Append(status).Append(' ').Append(relative)
                        .Append(" bytes=").AppendLine(new FileInfo(file).Length.ToString());
                }
            }
            int cleanedCount = 0;
            string receiptPath = ResolveReceiptPath(ledgerPath);
            if (File.Exists(receiptPath))
            {
                foreach (string line in File.ReadLines(receiptPath))
                {
                    try
                    {
                        JObject receipt = JObject.Parse(line);
                        if ((string)receipt["profileId"] == profileId && (string)receipt["target"] == target
                            && (string)receipt["disposition"] == "cleaned") cleanedCount++;
                    }
                    catch (JsonException) { ledgerIssue = "Cleanup receipt contains an invalid line."; }
                }
            }
            builder.Append("counts.found: ").AppendLine(foundCount.ToString());
            builder.Append("counts.eligible: ").AppendLine(
                (ledger != null && ledger.trusted && scope != null
                    ? scope.pending.Count(item => item.status == "eligible") : 0).ToString());
            builder.Append("counts.cleaned: ").AppendLine(cleanedCount.ToString());
            builder.Append("counts.failed: ").AppendLine(
                (scope == null ? 0 : scope.pending.Count(item => item.status == "failed"
                    || item.status == "blockedHashMismatch")).ToString());
            if (!string.IsNullOrWhiteSpace(ledgerIssue))
                builder.Append("issue: ").AppendLine(ledgerIssue);
            return builder.ToString();
        }

        private static Scope GetScope(Ledger ledger, string profileId, string target, bool create)
        {
            Scope scope = ledger.scopes.FirstOrDefault(item => item.profileId == profileId && item.target == target);
            if (scope == null && create)
            {
                scope = new Scope { profileId = profileId, target = target };
                ledger.scopes.Add(scope);
            }
            return scope;
        }

        private static string RequireOwnedPath(string relative, string target, string root)
        {
            string prefix = BundlePrefix + target + "/";
            if (string.IsNullOrWhiteSpace(relative) || !relative.StartsWith(prefix, StringComparison.Ordinal)
                || relative.Contains("\\") || relative.Contains("/../") || relative.EndsWith("/..", StringComparison.Ordinal)
                || Path.IsPathRooted(relative))
                throw new InvalidOperationException("Bundle path is outside its owned target subtree: " + relative);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bundle path escapes artifact root: " + relative);
            if (!string.Equals(Path.GetRelativePath(fullRoot, fullPath).Replace('\\', '/'), relative, StringComparison.Ordinal))
                throw new InvalidOperationException("Bundle path is not normalized: " + relative);
            for (DirectoryInfo directory = new FileInfo(fullPath).Directory; directory != null; directory = directory.Parent)
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Linked bundle directory is not owned: " + directory.FullName);
            if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked bundle file is not owned: " + fullPath);
            return fullPath;
        }

        private static string ToUnityAssetPath(string fullPath)
        {
            string assetsRoot = Path.GetFullPath("Assets").TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Owned bundle is outside Unity Assets: " + fullPath);
            return "Assets/" + Path.GetRelativePath(assetsRoot, fullPath).Replace('\\', '/');
        }

        private static void AppendReceipt(Context context, Scope scope, Pending pending, string disposition, string reason)
        {
            string path = ResolveReceiptPath(context.FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.AppendAllText(path, JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), scope.profileId, scope.target,
                pending.relativePath, pending.sha256, pending.generatedBy, pending.retiredBy,
                disposition, reason
            }) + "\n");
        }

        private static string ResolveReceiptPath(string ledgerPath)
        {
            return ledgerPath == Path.GetFullPath(LedgerPath)
                ? Path.GetFullPath(ReceiptPath) : ledgerPath + ".receipts.jsonl";
        }

        private static Ledger ReadLedger(string path)
        {
            Envelope envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path));
            if (envelope?.data == null || envelope.data.schemaVersion != 1 || envelope.data.scopes == null)
                throw new InvalidOperationException("Ownership ledger schema is invalid.");
            string actual = HashText(JsonConvert.SerializeObject(envelope.data, Formatting.None));
            if (!string.Equals(actual, envelope.sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Ownership ledger checksum is invalid.");
            foreach (Scope scope in envelope.data.scopes)
                if (scope == null || string.IsNullOrWhiteSpace(scope.profileId) || string.IsNullOrWhiteSpace(scope.target)
                    || scope.current == null || scope.pending == null || scope.committedGenerations == null)
                    throw new InvalidOperationException("Ownership ledger contains an invalid scope.");
            return envelope.data;
        }

        private static void Save(Context context)
        {
            if (context?.Data == null) return;
            string path = context.FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var envelope = new Envelope
            {
                data = context.Data,
                sha256 = HashText(JsonConvert.SerializeObject(context.Data, Formatting.None))
            };
            string pending = path + ".pending";
            File.WriteAllText(pending, JsonConvert.SerializeObject(envelope, Formatting.Indented) + "\n");
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(pending, path, null);
                    else File.Move(pending, path);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        private static string HashFile(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string HashText(string value)
        {
            using SHA256 sha = SHA256.Create();
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class Envelope { public string sha256; public Ledger data; }
        internal sealed class Ledger
        {
            public int schemaVersion = 1;
            public bool trusted;
            public List<Scope> scopes = new List<Scope>();
        }
        internal sealed class Scope
        {
            public string profileId;
            public string target;
            public string currentGeneration;
            public List<string> committedGenerations = new List<string>();
            public List<Owned> current = new List<Owned>();
            public List<Pending> pending = new List<Pending>();
        }
        internal class Owned { public string relativePath; public string sha256; public string generation; }
        internal sealed class Pending : Owned
        {
            public string generatedBy;
            public string retiredBy;
            public string status;
            public string lastFailure;
            public int attempts;
        }
    }
}
