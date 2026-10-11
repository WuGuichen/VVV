using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Build-bound evidence; never reads an unrelated latest Editor log when rendering.</summary>
    internal static class CharacterTestPackageDetailEvidence
    {
        internal const string SnapshotName = "CharacterTestPackageDetails.json";
        internal static string PathFor(string runReport) => Path.ChangeExtension(runReport, ".details.json");
        internal static string ErrorPathFor(string runReport) => Path.ChangeExtension(runReport, ".details-error.txt");

        internal static void CaptureForBuild(BuildReport report, string playerDirectory, string runReport)
        {
            string error = ErrorPathFor(runReport);
            if (File.Exists(error)) File.Delete(error);
            if (File.Exists(PathFor(runReport))) File.Delete(PathFor(runReport));
            try { Capture(report, playerDirectory, runReport); }
            catch (Exception ex)
            {
                // Keep the actual build and Player run result intact. The final overview
                // writer will fail explicitly after those results have been recorded.
                File.WriteAllText(error, ex.ToString());
                UnityEngine.Debug.LogWarning("Package detail capture failed; see " + error);
            }
        }

        internal static void Capture(BuildReport report, string playerDirectory, string runReport, bool duringBuild = true)
        {
            if (!string.Equals(Path.GetFullPath(Path.GetDirectoryName(report.summary.outputPath)),
                    Path.GetFullPath(playerDirectory), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("BuildReport output does not match this Player.");
            string inputReceipt = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(runReport)), "CharacterTestResourceInputs.json");
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            var excludedResourcePaths = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(inputReceipt))
            {
                JObject inputs = JObject.Parse(File.ReadAllText(inputReceipt));
                foreach (JToken mirror in inputs["excludedGeneratedMirrors"] as JArray ?? new JArray())
                {
                    aliases[(string)mirror["temporaryAssetPath"]] = (string)mirror["mirror"];
                    excludedResourcePaths.Add((string)mirror["mirror"]);
                }
                foreach (JToken directory in inputs["generatedResourceDirectories"] as JArray ?? new JArray())
                    foreach (JToken source in directory["publishedAssets"] as JArray ?? new JArray())
                    {
                        string original = (string)source;
                        aliases[(string)directory["temporaryAssetPath"] + original.Substring(((string)directory["source"]).Length)] = original;
                        excludedResourcePaths.Add(original);
                    }
            }
            var assetRows = new JArray();
            foreach (PackedAssets container in report.packedAssets)
                foreach (PackedAssetInfo asset in container.contents)
                {
                    string buildPath = asset.sourceAssetPath ?? string.Empty;
                    assetRows.Add(new JObject { ["path"] = aliases.TryGetValue(buildPath, out string original) ? original : buildPath,
                        ["buildPath"] = buildPath,
                        ["type"] = asset.type?.Name ?? "Unknown", ["bytes"] = (long)asset.packedSize,
                        ["container"] = container.shortPath });
                }
            var assets = new JArray(assetRows.GroupBy(a => (string)a["path"], StringComparer.Ordinal)
                .Select(g => new JObject { ["path"] = g.Key, ["bytes"] = g.Sum(a => (long)a["bytes"]),
                    ["types"] = string.Join(", ", g.Select(a => (string)a["type"]).Distinct()),
                    ["buildPaths"] = new JArray(g.Select(a => (string)a["buildPath"]).Distinct()),
                    ["containers"] = new JArray(g.Select(a => (string)a["container"]).Distinct()) })
                .OrderByDescending(a => (long)a["bytes"]));
            string scene = CharacterTestPerformancePlayerBuildReportMenu.CharacterTestScenePath;
            var sceneDependencies = new HashSet<string>(AssetDatabase.GetDependencies(scene, true), StringComparer.Ordinal);
            string[] resourceRoots = assetRows.Select(a => (string)a["path"]).Distinct()
                .Where(p => p.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0
                    && !excludedResourcePaths.Contains(p) && !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(p)).ToArray();
            var resourceDependencies = new HashSet<string>(resourceRoots.Length == 0 ? Array.Empty<string>()
                : AssetDatabase.GetDependencies(resourceRoots, true), StringComparer.Ordinal);
            var bundleDeclarations = ReadBundleDeclarations(playerDirectory);
            foreach (JObject asset in assets)
            {
                string path = (string)asset["path"];
                var origins = new JArray();
                if (!excludedResourcePaths.Contains(path) && path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0
                    && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) origins.Add("Resources 目录");
                if (sceneDependencies.Contains(path)) origins.Add("Player 场景依赖");
                if (resourceDependencies.Contains(path) && origins.Count == 0) origins.Add("Resources 依赖");
                if (path.StartsWith("Packages/", StringComparison.Ordinal)) origins.Add("Unity/第三方包资源");
                if (origins.Count == 0) origins.Add(path.StartsWith("Assets/", StringComparison.Ordinal)
                    ? "其他构建引用（未定位入口）" : "引擎内置或生成数据");
                asset["origins"] = origins;
                if (excludedResourcePaths.Contains(path)) asset["resourceInputPolicy"] = "Implicit Resources input excluded for this build; retained scene references can still include the asset.";
                if (bundleDeclarations.TryGetValue(path, out string bundle)) asset["bundleDeclaration"] = bundle;
            }
            // Bound work: show shortest concrete reference chains for the largest scene assets only.
            var targets = new HashSet<string>(assets.Take(25).Select(a => (string)a["path"])
                .Where(sceneDependencies.Contains), StringComparer.Ordinal);
            var parents = FindReferenceParents(scene, targets);
            foreach (JObject asset in assets.Take(25))
            {
                string path = (string)asset["path"];
                if (!parents.ContainsKey(path)) continue;
                var chain = new List<string>();
                for (string cursor = path; cursor != null; cursor = parents[cursor]) chain.Add(cursor);
                chain.Reverse(); asset["referenceChain"] = new JArray(chain);
            }
            var identities = new JArray(Directory.GetFiles(playerDirectory, "*", SearchOption.AllDirectories)
                .Where(p => Path.GetFileName(p) == "GameAssembly.dll" || Path.GetFileName(p) == "data.unity3d")
                .Select(p => new JObject { ["path"] = Path.GetRelativePath(playerDirectory, p).Replace('\\', '/'),
                    ["bytes"] = new FileInfo(p).Length, ["sha256"] = Hash(p) }));
            JObject native = AnalyzeNative(playerDirectory, runReport);
            var evidence = new JObject
            {
                ["schemaVersion"] = 1, ["capturedUtc"] = DateTime.UtcNow.ToString("O"),
                ["buildGuid"] = report.summary.guid.ToString(), ["buildOutput"] = report.summary.outputPath,
                ["resourceInputReceipt"] = File.Exists(inputReceipt) ? inputReceipt : null,
                ["resourceInputEvidence"] = "BuildReport size/inclusion; buildPaths retain temporary Unity paths, logical paths and reference graph refer to restored source assets.",
                ["referenceEvidence"] = duringBuild ? "构建完成时的工程引用" : "事后读取当前工程引用；资源大小来自本次保存的 BuildReport",
                ["identities"] = identities, ["assetBytes"] = assetRows.Sum(a => (long)a["bytes"]),
                ["assetRecordCount"] = assetRows.Count, ["assets"] = assets,
                ["assetTypes"] = new JArray(assetRows.GroupBy(a => (string)a["type"]).Select(g => new JObject
                    { ["name"] = g.Key, ["bytes"] = g.Sum(a => (long)a["bytes"]), ["count"] = g.Count() })
                    .OrderByDescending(a => (long)a["bytes"])),
                ["containers"] = new JArray(report.packedAssets.Select(p => new JObject
                    { ["name"] = p.shortPath, ["overheadBytes"] = (long)p.overhead })),
                ["native"] = native
            };
            File.WriteAllText(PathFor(runReport), evidence.ToString());
        }

        private static Dictionary<string, string> ReadBundleDeclarations(string player)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string catalog = Path.Combine(player, "CharacterTestPerformance_Data/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json");
            if (!File.Exists(catalog)) return result;
            foreach (JToken entry in JObject.Parse(File.ReadAllText(catalog))["entries"] as JArray ?? new JArray())
            {
                string path = (string)entry["providerData"]?["assetPath"];
                string bundle = (string)entry["providerData"]?["bundleName"];
                if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(bundle)) result[path] = bundle;
            }
            return result;
        }

        private static Dictionary<string, string> FindReferenceParents(string root, HashSet<string> targets)
        {
            var parents = new Dictionary<string, string>(StringComparer.Ordinal) { [root] = null };
            var queue = new Queue<string>(); queue.Enqueue(root);
            var budget = Stopwatch.StartNew();
            while (queue.Count > 0 && targets.Count > 0 && parents.Count < 4000 && budget.ElapsedMilliseconds < 1500)
            {
                string current = queue.Dequeue();
                foreach (string path in AssetDatabase.GetDependencies(current, false))
                {
                    if (parents.ContainsKey(path)) continue;
                    parents[path] = current; targets.Remove(path); queue.Enqueue(path);
                }
            }
            return parents;
        }

        private static JObject AnalyzeNative(string player, string runReport)
        {
            string binary = Path.Combine(player, "GameAssembly.dll");
            string receiptPath = CharacterTestWindowsBuildConfiguration.ReceiptPath(runReport);
            if (!File.Exists(binary) || !File.Exists(receiptPath)) return Unavailable("缺少本次 DLL 或符号回执");
            try
            {
                var receipt = JObject.Parse(File.ReadAllText(receiptPath));
                JToken symbol = (receipt["symbols"] as JArray)?.FirstOrDefault(s =>
                    ((string)s["path"]).EndsWith("GameAssembly.pdb", StringComparison.OrdinalIgnoreCase));
                if (symbol == null) return Unavailable("本次没有 GameAssembly.pdb");
                string root = Path.GetDirectoryName(Path.GetFullPath(runReport));
                string pdb = Path.Combine(root, (string)receipt["symbolsDirectory"], (string)symbol["archivePath"]);
                string output = Path.ChangeExtension(runReport, ".native.json");
                // A re-render can reuse evidence only when it is bound to this exact DLL.
                if (File.Exists(output))
                {
                    JObject cached = JObject.Parse(File.ReadAllText(output));
                    if ((string)cached["binarySha256"] == Hash(binary)) return cached;
                }
                string script = Path.GetFullPath("Tools/Harness/analyze_package_native_size.ps1");
                string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell/v1.0/powershell.exe");
                string cache = Path.GetFullPath("Library/PackageOverview/Dia");
                string arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script)
                    + " -Binary " + Quote(binary) + " -Pdb " + Quote(pdb) + " -Output " + Quote(output) + " -Cache " + Quote(cache);
                using var process = Process.Start(new ProcessStartInfo(shell, arguments)
                    { UseShellExecute = false, CreateNoWindow = true });
                if (!process.WaitForExit(60000))
                {
                    process.Kill(); process.WaitForExit(); return Unavailable("原生符号分析超过 60 秒，未阻断构建");
                }
                if (process.ExitCode != 0 || !File.Exists(output)) return Unavailable("本机 DIA 分析工具不可用或执行失败");
                return JObject.Parse(File.ReadAllText(output));
            }
            catch (Exception ex) { return Unavailable(ex.Message); }
        }

        private static JObject Unavailable(string issue) => new JObject { ["status"] = "Unavailable", ["issue"] = issue };
        private static string Quote(string path)
        {
            if (path.Contains('"')) throw new InvalidDataException("Quote in analysis path.");
            return "\"" + path + "\"";
        }
        internal static string Hash(string path)
        {
            using var stream = File.OpenRead(path); using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal static void Validate(JObject evidence, string player)
        {
            JArray identities = evidence["identities"] as JArray;
            if (identities == null || identities.Count == 0) throw new InvalidDataException("Detail evidence has no Player identity.");
            string root = Path.GetFullPath(player).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (JToken identity in identities)
            {
                string path = Path.GetFullPath(Path.Combine(root, (string)identity["path"]));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)
                    || new FileInfo(path).Length != (long)identity["bytes"] || Hash(path) != (string)identity["sha256"])
                    throw new InvalidDataException("Detail evidence belongs to a different or changed Player.");
            }
            foreach (string required in new[] { "GameAssembly.dll", "CharacterTestPerformance_Data/data.unity3d" })
                if (File.Exists(Path.Combine(root, required)) && !identities.Any(i => (string)i["path"] == required))
                    throw new InvalidDataException("Detail evidence omits a required Player identity.");
            long total = (long)evidence["assetBytes"];
            if (total < 0 || (evidence["assets"] as JArray)?.Any(a => (long)a["bytes"] < 0) == true
                || (evidence["assetTypes"] as JArray)?.Any(a => (long)a["bytes"] < 0) == true
                || (evidence["assets"] as JArray)?.Sum(a => (long)a["bytes"]) != total
                || (evidence["assetTypes"] as JArray)?.Sum(a => (long)a["bytes"]) != total)
                throw new InvalidDataException("Asset detail totals disagree.");
            JToken native = evidence["native"];
            if ((string)native?["status"] == "Verified")
            {
                string binary = Path.Combine(root, "GameAssembly.dll");
                if (!File.Exists(binary) || Hash(binary) != (string)native["binarySha256"]
                    || (long)native["binaryBytes"] != new FileInfo(binary).Length
                    || (native["groups"] as JArray)?.Any(g => (long)g["bytes"] < 0) == true
                    || (native["groups"] as JArray)?.Sum(g => (long)g["bytes"]) != new FileInfo(binary).Length)
                    throw new InvalidDataException("Native detail totals or DLL identity disagree.");
            }
        }
    }
}
