using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Explicit build-attempt context. Menus without a scope remain clean Release builds.</summary>
    public static class GlobalPlayerResourceBuildContext
    {
        private static string _configuration = "Release";
        private static string _evidenceRoot;
        private const string Pointer = "Library/MxFramework/ResourceBundles/player-manifest.json";

        public sealed class Scope : IDisposable
        {
            private readonly string _previousConfiguration = _configuration;
            private readonly string _previousRoot = _evidenceRoot;
            public Scope(string configuration, string evidenceRoot)
            {
                if (configuration != "Release" && configuration != "Development")
                    throw new ArgumentException("Unknown resource build configuration.");
                _configuration = configuration;
                _evidenceRoot = Path.GetFullPath(evidenceRoot);
            }
            public void Dispose() { _configuration = _previousConfiguration; _evidenceRoot = _previousRoot; }
        }

        public static string PrepareBundleDirectory(string profileId, string buildPlanFingerprint, BuildAssetBundleOptions options)
        {
            string identity = Application.unityVersion + "|" + EditorUserBuildSettings.activeBuildTarget
                + "|" + _configuration + "|" + profileId + "|" + buildPlanFingerprint + "|" + (int)options
                + "|" + PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            string key;
            using (var sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
            // Unity rejects BuildAssetBundles outputs inside Library. .agent is local, excluded from snapshots/version control.
            string directory = Path.Combine(".agent", "BuildCache", "AssetBundles",
                Application.unityVersion, EditorUserBuildSettings.activeBuildTarget.ToString(), _configuration, key);
            // Own cache only. Release deliberately ignores all previous Bundle manifests.
            if (_configuration == "Release" && Directory.Exists(directory)) Directory.Delete(directory, true);
            Directory.CreateDirectory(directory);
            WriteEvidence("bundle-cache.json", new JObject { ["identity"] = identity, ["cacheDirectory"] = directory,
                ["mode"] = _configuration == "Development" ? "Incremental" : "Clean" });
            return directory;
        }

        public static void CaptureBundles(string directory, string sourceFingerprint, string buildPlanFingerprint, double seconds)
        {
            string manifest = Path.GetFullPath(Path.Combine(directory, Path.GetFileName(directory) + ".manifest"));
            if (!File.Exists(manifest)) throw new BuildFailedException("AssetBundle type manifest is missing: " + manifest);
            var pointer = new JObject { ["unityVersion"] = Application.unityVersion,
                ["target"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                ["sourceFingerprint"] = sourceFingerprint, ["buildPlanFingerprint"] = buildPlanFingerprint,
                ["manifestPath"] = manifest, ["manifestSha256"] = Hash(manifest), ["bundleBuildSeconds"] = seconds };
            BuildReport report = BuildReport.GetLatestReport();
            if (report != null)
            {
                pointer["buildGuid"] = report.summary.guid.ToString();
                pointer["rawReportResult"] = report.summary.result.ToString();
                pointer["rawReportSeconds"] = report.summary.totalTime.TotalSeconds;
#if UNITY_6000_6_OR_NEWER
                pointer["rawReportBuildType"] = report.summary.buildType.ToString();
#endif
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Pointer));
            File.WriteAllText(Pointer, pointer.ToString());
            string destination = EvidenceDirectory("AssetBundles");
            foreach (string file in Directory.GetFiles(directory, "*.manifest", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            CopyIfPresent("Library/LastBuild.buildreport", Path.Combine(destination, "LastBuild.buildreport"));
            CopyIfPresent("Library/BuildPlayerTEP.json", Path.Combine(destination, "BuildPlayerTEP.json"));
            File.WriteAllText(Path.Combine(destination, "receipt.json"), pointer.ToString());
        }

        public static void CaptureBundleFailure(string directory, double seconds)
        {
            string destination = EvidenceDirectory("AssetBundles");
            CopyIfPresent("Library/LastBuild.buildreport", Path.Combine(destination, "Failed.buildreport"));
            File.WriteAllText(Path.Combine(destination, "failure.json"), new JObject {
                ["result"] = "Failed", ["cacheDirectory"] = directory, ["elapsedSeconds"] = seconds,
                ["unityVersion"] = Application.unityVersion }.ToString());
        }

        public static string RequirePlayerManifest(GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan) =>
            RequirePlayerManifest(plan.Input.SourceInputFingerprint, plan.Compilation.Plan.BuildPlanFingerprint);

        private static string RequirePlayerManifest(string sourceFingerprint, string buildPlanFingerprint)
        {
            if (!File.Exists(Pointer)) throw new BuildFailedException("Rebuild resources to produce the Player type manifest.");
            JObject receipt = JObject.Parse(File.ReadAllText(Pointer));
            string manifest = (string)receipt["manifestPath"];
            if ((string)receipt["unityVersion"] != Application.unityVersion
                || (string)receipt["target"] != EditorUserBuildSettings.activeBuildTarget.ToString()
                || (string)receipt["sourceFingerprint"] != sourceFingerprint
                || (string)receipt["buildPlanFingerprint"] != buildPlanFingerprint
                || !File.Exists(manifest) || (string)receipt["manifestSha256"] != Hash(manifest))
                throw new BuildFailedException("Player AssetBundle type manifest does not match validated resource inputs.");
            return manifest;
        }

        public static void CapturePlayer(BuildReport report)
        {
            string destination = EvidenceDirectory("Player");
            CopyIfPresent("Library/LastBuild.buildreport", Path.Combine(destination, "LastBuild.buildreport"));
            CopyIfPresent("Library/BuildPlayerTEP.json", Path.Combine(destination, "BuildPlayerTEP.json"));
#if UNITY_6000_6_OR_NEWER
            if (BuildHistory.TryGetBuildReportDirectory(report.summary.guid, out string history))
                foreach (string file in Directory.GetFiles(history, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(destination, "History", Path.GetRelativePath(history, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, false);
                }
#endif
            File.WriteAllText(Path.Combine(destination, "receipt.json"), new JObject {
                ["unityVersion"] = Application.unityVersion, ["buildGuid"] = report.summary.guid.ToString(),
                ["result"] = report.summary.result.ToString(), ["buildSeconds"] = report.summary.totalTime.TotalSeconds,
                ["outputPath"] = report.summary.outputPath, ["options"] = report.summary.options.ToString() }.ToString());
        }

        private static string EvidenceDirectory(string kind)
        {
            string root = _evidenceRoot ?? Path.Combine("Library", "MxFramework", "BuildEvidence", Guid.NewGuid().ToString("N"));
            string directory = Path.Combine(root, "BuildAnalysis", kind);
            Directory.CreateDirectory(directory);
            return directory;
        }
        private static void WriteEvidence(string name, JObject content) => File.WriteAllText(Path.Combine(EvidenceDirectory("AssetBundles"), name), content.ToString());
        private static void CopyIfPresent(string source, string destination) { if (File.Exists(source)) File.Copy(source, destination, false); }
        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
