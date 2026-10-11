using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MxFramework.Editor;
using MxFramework.Editor.CharacterImport;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Authoritative Player composition: publish prefab keys before excluding only their generated fallback mirrors.</summary>
    internal static class CharacterTestPlayerResourceInputs
    {
        internal static void PublishPrefabs()
        {
            var prefabs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CharacterTestCharacterDefinition definition in new CharacterTestCharacterDefinitionRegistry().Definitions)
            {
                string id = definition.PrefabKey.PackageId;
                prefabs.Add(id, "Assets/MxFrameworkGenerated/CharacterPackages/" + id + "/prefabs/" + id + "_character_preview.prefab");
            }
            CharacterImportedPackagePrefabBuilder.PublishPlayerPrefabMappings(prefabs);
        }

        internal sealed class PlayerScope : IDisposable
        {
            private readonly List<(string Source, string Destination, string Guid, string PayloadHash, string MetaHash)> _moved = new();
            private readonly List<string> _createdFolders = new();
            private readonly string _receiptPath;
            private readonly JArray _evidence = new();
            private readonly JArray _directoryEvidence = new();

            internal PlayerScope(string scene, string outputRoot)
            {
                _receiptPath = Path.Combine(outputRoot, "CharacterTestResourceInputs.json");
                JArray catalog = JObject.Parse(File.ReadAllText(GlobalPlayerResourceBuildProfileBuilder.CatalogPath))["entries"] as JArray;
                var sceneDependencies = new HashSet<string>(AssetDatabase.GetDependencies(scene, true), StringComparer.Ordinal);
                var publishedPaths = new HashSet<string>(catalog.Select(entry => (string)entry["providerData"]?["assetPath"])
                    .Where(path => !string.IsNullOrEmpty(path)), StringComparer.Ordinal);
                try
                {
                    foreach (CharacterTestCharacterDefinition definition in new CharacterTestCharacterDefinitionRegistry().Definitions)
                    {
                        string id = definition.PrefabKey.PackageId;
                        string root = "Assets/MxFrameworkGenerated/CharacterPackages/" + id;
                        string original = root + "/prefabs/" + id + "_character_preview.prefab";
                        string mirror = root + "/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/" + id + "/prefabs/" + id + "_character_preview.prefab";
                        JToken entry = catalog?.SingleOrDefault(e => (string)e["id"] == definition.PrefabKey.Id
                            && (string)e["packageId"] == id && (string)e["variant"] == definition.PrefabKey.Variant && (string)e["type"] == "GameObject");
                        if ((string)entry?["provider"] != "assetBundle" || (string)entry?["providerData"]?["assetPath"] != original)
                            throw new BuildFailedException("Player spawn key is not published by AssetBundle: " + definition.PrefabKey);
                        if (!File.Exists(mirror) || sceneDependencies.Contains(mirror))
                            throw new BuildFailedException("Fallback mirror is missing or directly required by the Player scene: " + mirror);
                        string destination = root + "/runtime_resources/PlayerExcluded/" + id + "_character_preview.prefab";
                        if (File.Exists(destination) || File.Exists(destination + ".meta"))
                            throw new BuildFailedException("A previous Player exclusion needs recovery before building: " + destination);
                        string parent = root + "/runtime_resources/PlayerExcluded";
                        if (!AssetDatabase.IsValidFolder(parent))
                        {
                            AssetDatabase.CreateFolder(root + "/runtime_resources", "PlayerExcluded");
                            _createdFolders.Add(parent);
                        }
                        string guid = AssetDatabase.AssetPathToGUID(mirror);
                        string payloadHash = Hash(mirror), metaHash = Hash(mirror + ".meta");
                        string error = AssetDatabase.MoveAsset(mirror, destination);
                        if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
                        _moved.Add((mirror, destination, guid, payloadHash, metaHash));
                        _evidence.Add(new JObject { ["key"] = definition.PrefabKey.ToString(), ["sourcePrefab"] = original,
                            ["mirror"] = mirror, ["temporaryAssetPath"] = destination, ["guid"] = guid,
                            ["payloadSha256"] = payloadHash, ["metaSha256"] = metaHash,
                            ["provider"] = "assetBundle", ["bundle"] = (string)entry["providerData"]["bundleName"] });
                        // Unity also gathers lower-case resources directories. Move only fully mapped model/texture
                        // inputs outside that special folder for the Player build; GUID scene dependencies remain.
                        foreach (string kind in new[] { "models", "textures" })
                        {
                            string sourceDirectory = root + "/resources/" + kind;
                            if (!Directory.Exists(sourceDirectory)) continue;
                            string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Select(path => path.Replace('\\', '/')).ToArray();
                            if (files.Any(path => !publishedPaths.Contains(path)))
                            {
                                _directoryEvidence.Add(new JObject { ["source"] = sourceDirectory, ["retained"] = "Contains an asset without a production Bundle declaration." });
                                continue;
                            }
                            string targetDirectory = parent + "/resource_assets_" + kind;
                            if (Directory.Exists(targetDirectory) || File.Exists(targetDirectory + ".meta"))
                                throw new BuildFailedException("A previous generated input exclusion needs recovery: " + targetDirectory);
                            string directoryGuid = AssetDatabase.AssetPathToGUID(sourceDirectory);
                            string directoryHash = HashContents(sourceDirectory), directoryMetaHash = Hash(sourceDirectory + ".meta");
                            string moveError = AssetDatabase.MoveAsset(sourceDirectory, targetDirectory);
                            if (!string.IsNullOrEmpty(moveError)) throw new BuildFailedException(moveError);
                            _moved.Add((sourceDirectory, targetDirectory, directoryGuid, directoryHash, directoryMetaHash));
                            _directoryEvidence.Add(new JObject { ["source"] = sourceDirectory, ["temporaryAssetPath"] = targetDirectory,
                                ["guid"] = directoryGuid, ["contentSha256"] = directoryHash, ["metaSha256"] = directoryMetaHash,
                                ["publishedAssets"] = new JArray(files), ["sceneReferences"] = new JArray(files.Where(sceneDependencies.Contains)) });
                        }
                    }
                    WriteReceipt(false);
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                var failures = new List<string>();
                for (int i = _moved.Count - 1; i >= 0; i--)
                {
                    var entry = _moved[i];
                    string error = AssetDatabase.MoveAsset(entry.Destination, entry.Source);
                    if (!string.IsNullOrEmpty(error) || AssetDatabase.AssetPathToGUID(entry.Source) != entry.Guid
                        || (!File.Exists(entry.Source) && !Directory.Exists(entry.Source))
                        || HashContents(entry.Source) != entry.PayloadHash || Hash(entry.Source + ".meta") != entry.MetaHash)
                        failures.Add(entry.Source + ": " + error);
                }
                if (failures.Count != 0) throw new BuildFailedException("Resource mirror restoration failed: " + string.Join(" | ", failures));
                _moved.Clear();
                foreach (string directory in _createdFolders)
                    if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                        AssetDatabase.DeleteAsset(directory);
                _createdFolders.Clear();
                WriteReceipt(true);
            }
            private static string Hash(string path)
            {
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
            private static string HashContents(string path)
            {
                if (File.Exists(path)) return Hash(path);
                string contents = string.Join("\n", Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .OrderBy(file => file, StringComparer.Ordinal).Select(file => Path.GetRelativePath(path, file).Replace('\\', '/') + "|" + Hash(file)));
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(contents))).Replace("-", "").ToLowerInvariant();
            }
            private void WriteReceipt(bool restored) => File.WriteAllText(_receiptPath,
                new JObject { ["schemaVersion"] = 1, ["mirrorsRestored"] = restored, ["excludedGeneratedMirrors"] = _evidence,
                    ["generatedResourceDirectories"] = _directoryEvidence,
                    ["retained"] = "All original assets, direct scene dependencies, VFX inputs and unknown assets remain." }.ToString());
        }
    }
}
