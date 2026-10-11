using System;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Focused isolated build validation. No shared Editor interaction or broad test run.</summary>
    public static class CharacterTestBuildOptimizationValidation
    {
        public static void Prepare()
        {
            CharacterTestJsonPreservation.Regenerate();
            string output = Output(); Directory.CreateDirectory(output);
            CharacterTestJsonPreservation.RequireCurrent(output);
            CharacterTestPlayerResourceInputs.PublishPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("Build optimization preparation passed.");
        }

        public static void ValidateResourceCache()
        {
            string output = Output(); Directory.CreateDirectory(output);
            CharacterTestPlayerResourceInputs.PublishPrefabs();
            var plan = GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            if (plan.HasErrors) throw new BuildFailedException(GlobalPlayerResourceBuildProfileBuilder.CreateReportText(plan.Report));
            string Build(string name, string configuration)
            {
                using (new GlobalPlayerResourceBuildContext.Scope(configuration, Path.Combine(output, name)))
                using (new CharacterTestWindowsBuildConfiguration.Scope(CharacterTestWindowsBuildConfiguration.Parse(configuration)))
                {
                    GlobalPlayerResourceBuildProfileBuilder.Build(plan);
                    GlobalPlayerResourceBuildProfileBuilder.ValidatePublishedArtifacts(plan);
                }
                return Path.GetFullPath((string)JObject.Parse(File.ReadAllText(Path.Combine(output, name, "BuildAnalysis/AssetBundles/bundle-cache.json")))["cacheDirectory"]);
            }
            string first = Build("DevelopmentFirst", "Development");
            string probe = Path.Combine(first, "incremental-cache-probe.txt"); File.WriteAllText(probe, "Owned validation probe.");
            string second = Build("DevelopmentSecond", "Development");
            if (first != second || !File.Exists(probe)) throw new BuildFailedException("Development cache was discarded between identical inputs.");
            string release = Build("ReleaseFirst", "Release");
            if (release == first) throw new BuildFailedException("Release and Development cache directories overlap.");
            string cleanProbe = Path.Combine(release, "clean-cache-probe.txt"); File.WriteAllText(cleanProbe, "Owned validation probe.");
            string clean = Build("ReleaseSecond", "Release");
            if (clean != release || File.Exists(cleanProbe)) throw new BuildFailedException("Release did not clean its owned cache.");
            string[] names = Directory.GetFiles(GlobalPlayerResourceBuildProfileBuilder.BundleRootPath + "/StandaloneWindows64", "*")
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToArray();
            var loaded = names.Select(AssetBundle.LoadFromFile).Where(bundle => bundle != null).ToList();
            try
            {
                JArray entries = JObject.Parse(File.ReadAllText(GlobalPlayerResourceBuildProfileBuilder.CatalogPath))["entries"] as JArray;
                JToken[] prefabs = entries.Where(entry => ((string)entry["id"]).EndsWith(".prefab.character_preview", StringComparison.Ordinal)).ToArray();
                if (prefabs.Length != new CharacterTestCharacterDefinitionRegistry().Definitions.Count)
                    throw new BuildFailedException("Player prefab coverage differs from the playable registry.");
                foreach (JToken entry in prefabs)
                {
                    string bundleName = (string)entry["providerData"]["bundleName"];
                    string asset = (string)entry["providerData"]["assetPath"];
                    AssetBundle bundle = loaded.SingleOrDefault(candidate => candidate.name == bundleName);
                    GameObject prefab = bundle?.LoadAsset<GameObject>(asset);
                    if (prefab == null || prefab.GetComponentsInChildren<Component>(true).Any(component => component == null))
                        throw new BuildFailedException("AssetBundle prefab or component is missing: " + asset);
                }
                // Exercise both disposal and exception restoration using the exact generated assets.
                using (new CharacterTestPlayerResourceInputs.PlayerScope(CharacterTestPerformancePlayerBuildReportMenu.CharacterTestScenePath, output)) { }
                try
                {
                    using (new CharacterTestPlayerResourceInputs.PlayerScope(CharacterTestPerformancePlayerBuildReportMenu.CharacterTestScenePath, output))
                        throw new InvalidOperationException("Owned validation failure.");
                }
                catch (InvalidOperationException ex) when (ex.Message == "Owned validation failure.") { }
                File.WriteAllText(Path.Combine(output, "ResourceCacheValidation.json"), new JObject {
                    ["status"] = "Passed", ["developmentRetained"] = true, ["releaseClean"] = true,
                    ["configurationIsolated"] = true, ["bundlePrefabCount"] = prefabs.Length,
                    ["mirrorRestorationNormalAndException"] = true, ["artifactMarkerAndHashesValidated"] = true }.ToString());
            }
            finally { foreach (AssetBundle bundle in loaded) bundle.Unload(true); }
            Debug.Log("Resource cache and prefab input validation passed: " + output);
        }

        private static string Output()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-optimization-evidence-output");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("A fresh -optimization-evidence-output is required.");
            return Path.GetFullPath(args[index + 1]);
        }
    }
}
