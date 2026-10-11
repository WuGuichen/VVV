using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Config.Levels;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Resolves Player character packages from the level's authored character IDs.</summary>
    internal sealed class CharacterTestPlayerCharacterPackageSelection
    {
        internal const string GeneratedRoot = "Assets/MxFrameworkGenerated/CharacterPackages";
        internal const string IdentityCatalogPath = "Tools/RuntimeHash/character_definition_ids.json";
        private const int RuntimeCharacterDomain = 413;

        internal sealed class Package
        {
            public string PackageId;
            public int RuntimeCharacterId;
            public string SourceDirectory;
            public bool DefaultDependency;
        }

        private CharacterTestPlayerCharacterPackageSelection(List<Package> packages, string sourceEvidence)
        {
            Packages = packages;
            SourceEvidence = sourceEvidence;
        }

        public IReadOnlyList<Package> Packages { get; }
        public string SourceEvidence { get; }

        public static CharacterTestPlayerCharacterPackageSelection Resolve(bool validateSceneCache = false)
        {
            CharacterTestPlayerCharacterPackageSelection selection = ResolveFromFiles(
                CharacterTestSceneCharacterAuthoringAdapter.DefaultDocumentPath,
                IdentityCatalogPath, GeneratedRoot, validatePlayable: true);
            if (validateSceneCache)
                RequireSceneCacheMatches(CharacterTestSceneCharacterAuthoringAdapter.DefaultDocumentPath);
            return selection;
        }

        internal static CharacterTestPlayerCharacterPackageSelection ResolveFromFiles(
            string levelPath, string identityCatalogPath, string generatedRoot, bool validatePlayable,
            bool includeDefault = true)
        {
            if (!File.Exists(levelPath))
                throw new InvalidOperationException("CharacterTest level character source is missing: " + levelPath);
            if (!File.Exists(identityCatalogPath))
                throw new InvalidOperationException("Character runtime identity catalog is missing: " + identityCatalogPath);
            MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(levelPath));
            if (!CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                    envelope, out CharacterTestSceneCharacterAuthoringDocument document, out _))
                throw new InvalidOperationException("CharacterTest level character module cannot be read: " + levelPath);
            Dictionary<int, string> packageById = ReadActiveIdentityCatalog(identityCatalogPath);
            var idByPackage = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<int, string> pair in packageById)
            {
                if (!idByPackage.TryAdd(pair.Value, pair.Key))
                    throw new InvalidOperationException("Character identity catalog repeats package " + pair.Value + ".");
            }

            var selected = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var evidence = new StringBuilder();
            evidence.Append("levelSource=").AppendLine(HashFile(levelPath));
            evidence.Append("identityCatalog=").AppendLine(HashFile(identityCatalogPath));
            if (!File.Exists(document.ScenePath))
                throw new InvalidOperationException("CharacterTest scene asset is missing: " + document.ScenePath);
            evidence.Append("sceneAsset=").AppendLine(HashFile(document.ScenePath));
            int enabledCount = 0;
            for (int i = 0; document.Characters != null && i < document.Characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition character = document.Characters[i];
                if (character == null || !character.Enabled) continue;
                enabledCount++;
                if (!packageById.TryGetValue(character.CharacterId, out string packageId))
                {
                    string hint = string.Empty;
                    foreach (KeyValuePair<string, int> pair in idByPackage)
                    {
                        if (CharacterTestPlayableCharacterCatalog.ComputeLegacyCharacterId(pair.Key)
                            != character.CharacterId) continue;
                        hint = " Legacy ID points to package " + pair.Key + ", whose current runtime ID is "
                            + pair.Value + ".";
                        break;
                    }
                    throw new InvalidOperationException("CharacterTest scene row " + character.StableId
                        + " has an invalid or retired character ID " + character.CharacterId + "." + hint);
                }
                selected[packageId] = character.CharacterId;
                evidence.Append("sceneCharacter=").Append(character.StableId).Append('|')
                    .Append(character.CharacterId).Append('|').AppendLine(packageId);
            }
            if (enabledCount == 0)
                throw new InvalidOperationException("CharacterTest level has no enabled character references.");

            // The default package remains a runtime fallback dependency even when the level
            // selects other characters. The selected set is derived from the level above.
            if (includeDefault)
            {
                if (!idByPackage.TryGetValue("000001", out int defaultId))
                    throw new InvalidOperationException("Default CharacterTest package 000001 has no active numeric identity.");
                if (!selected.ContainsKey("000001")) selected.Add("000001", defaultId);
            }

            var packages = new List<Package>(selected.Count);
            CharacterTestCharacterDefinitionRegistry registry = validatePlayable
                ? new CharacterTestCharacterDefinitionRegistry(CharacterTestPlayableCharacterCatalog.Refresh())
                : null;
            foreach (KeyValuePair<string, int> pair in selected)
            {
                string source = Path.Combine(generatedRoot, pair.Key);
                if (!Directory.Exists(source))
                    throw new InvalidOperationException("CharacterTest Player package " + pair.Key
                        + " is missing for scene character ID " + pair.Value + ": " + source);
                string reportPath = Path.Combine(source, "package_cache", "import_report.json");
                if (!File.Exists(reportPath))
                    throw new InvalidOperationException("CharacterTest Player package " + pair.Key
                        + " has no Unity import report: " + reportPath);
                JObject report = JObject.Parse(File.ReadAllText(reportPath));
                if ((string)report["packageId"] != pair.Key || (bool?)report["canSpawnAfterImport"] != true)
                    throw new InvalidOperationException("CharacterTest Player package " + pair.Key
                        + " is not spawnable according to its import report.");
                CharacterImportedPackage imported;
                try { imported = CharacterImportedPackageJson.LoadFromDirectory(source); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("CharacterTest Player package " + pair.Key
                        + " cannot be loaded: " + ex.Message, ex);
                }
                if (imported.PackageId != pair.Key || imported.Configs.RuntimeCharacterId != pair.Value)
                    throw new InvalidOperationException("CharacterTest Player package " + pair.Key
                        + " runtimeCharacterId does not match the level and numeric identity catalog. expected="
                        + pair.Value + " imported=" + imported.Configs.RuntimeCharacterId);
                if (registry != null
                    && (!registry.TryGet(pair.Value, out CharacterTestCharacterDefinition definition)
                        || definition.PrefabKey.PackageId != pair.Key))
                    throw new InvalidOperationException("CharacterTest Player package " + pair.Key
                        + " is imported but cannot be resolved as a playable character ID " + pair.Value + ".");
                bool defaultDependency = pair.Key == "000001"
                    && !document.Characters.Any(item => item != null && item.Enabled && item.CharacterId == pair.Value);
                packages.Add(new Package
                {
                    PackageId = pair.Key,
                    RuntimeCharacterId = pair.Value,
                    SourceDirectory = source,
                    DefaultDependency = defaultDependency
                });
                evidence.Append("package=").Append(pair.Key).Append('|').Append(pair.Value)
                    .Append('|').Append(defaultDependency ? "defaultDependency" : "sceneSelected")
                    .AppendLine();
            }
            return new CharacterTestPlayerCharacterPackageSelection(packages, evidence.ToString());
        }

        private static Dictionary<int, string> ReadActiveIdentityCatalog(string path)
        {
            JObject catalog = JObject.Parse(File.ReadAllText(path));
            if ((string)catalog["schema"] != "mx.runtime-definition-id-catalog.v1")
                throw new InvalidOperationException("Character runtime identity catalog schema is invalid.");
            JToken domain = (catalog["domains"] as JArray)?.FirstOrDefault(
                item => (int?)item["domainId"] == RuntimeCharacterDomain);
            if (domain?["entries"] is not JArray entries)
                throw new InvalidOperationException("Character runtime identity domain 413 is missing.");
            var result = new Dictionary<int, string>();
            foreach (JToken entry in entries)
            {
                if ((string)entry["status"] != "active") continue;
                int id = (int?)entry["id"] ?? 0;
                string packageId = (string)entry["sourceKey"];
                if (id <= 0 || string.IsNullOrWhiteSpace(packageId) || !result.TryAdd(id, packageId))
                    throw new InvalidOperationException("Character runtime identity domain has an invalid or duplicate entry.");
            }
            return result;
        }

        private static void RequireSceneCacheMatches(string levelPath)
        {
            MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(levelPath));
            if (!CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                    envelope, out CharacterTestSceneCharacterAuthoringDocument document, out _))
                throw new InvalidOperationException("CharacterTest level character module cannot be read.");
            Scene scene = SceneManager.GetSceneByPath(document.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(document.ScenePath, OpenSceneMode.Additive);
            try
            {
                GameManager manager = null;
                foreach (UnityEngine.GameObject root in scene.GetRootGameObjects())
                {
                    manager = root.GetComponentInChildren<GameManager>(true);
                    if (manager != null) break;
                }
                if (manager == null)
                    throw new InvalidOperationException("CharacterTest scene has no GameManager character cache.");
                CharacterTestSceneCharacterCacheSyncStatus status =
                    CharacterTestSceneCharacterCacheSyncStatus.Evaluate(document, envelope,
                        manager.CharacterSceneCache, levelPath);
                if (!status.IsApplied)
                    throw new InvalidOperationException("CharacterTest scene character cache is stale: " + status.Message);
                var cached = new Dictionary<string, CharacterTestSceneCharacterCache.CharacterInstance>(
                    StringComparer.Ordinal);
                foreach (CharacterTestSceneCharacterCache.CharacterInstance instance
                         in manager.CharacterSceneCache.Characters)
                {
                    if (instance == null || !cached.TryAdd(instance.StableId, instance))
                        throw new InvalidOperationException("CharacterTest scene character cache has a missing or repeated row.");
                }
                foreach (CharacterTestSceneCharacterDefinition character in document.Characters)
                {
                    if (character == null || !cached.TryGetValue(character.StableId, out var instance)
                        || instance.CharacterId != character.CharacterId
                        || instance.IsEnabled != character.Enabled
                        || instance.TeamId != character.TeamId
                        || instance.ControlOwner != character.ControlOwner)
                        throw new InvalidOperationException("CharacterTest scene character cache row differs from Authoring: "
                            + (character?.StableId ?? "<null>"));
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string HashFile(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
