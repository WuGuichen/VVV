using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Bakes Authoring Recipe layers into one complete Runtime-loadable Prefab.</summary>
    /// <remarks>Editor-only and LowFreqAlloc. Runtime never expands Primitive or Recipe data.</remarks>
    public static class VfxAuthoringRecipePrefabBaker
    {
        public const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        public const string EvidenceRoot = "Assets/MxFrameworkGenerated/Vfx/AuthoringBakeEvidence";
        private const string SupportedBakerId = "mx.vfx.baker.particle-system-prefab";
        private const string SupportedBakerVersion = "1";

        [MenuItem("MxFramework/VFX/Bake Planned Authoring Recipes")]
        public static void BakePlannedRecipes()
        {
            JObject catalog = LoadCatalog();
            JArray effects = RequireArray(catalog, "effects");
            int baked = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                JObject effect = effects[i] as JObject;
                if (effect == null || string.IsNullOrWhiteSpace(effect.Value<string>("recipeId")))
                    continue;
                BakeEffect(catalog, effect.Value<string>("effectId"));
                baked++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("VFX Authoring Recipe bake completed. effects=" + baked);
        }

        /// <summary>Bakes one Effect from the current project Catalog and returns its output path.</summary>
        /// <remarks>Editor-only and AllocByDesign. Creates a Prefab plus derived JSON evidence.</remarks>
        public static string BakeEffect(string effectId)
        {
            return BakeEffect(LoadCatalog(), effectId);
        }

        private static string BakeEffect(JObject catalog, string effectId)
        {
            JObject effect = FindById(RequireArray(catalog, "effects"), "effectId", effectId);
            if (effect == null)
                throw new InvalidOperationException("VFX Effect is missing: " + effectId);
            string recipeId = RequireString(effect, "recipeId");
            JObject published = effect["published"] as JObject ?? throw new InvalidOperationException("Recipe-backed Effect requires published evidence: " + effectId);
            string outputPath = RequireProjectPrefabPath(RequireString(published, "assetPath"));
            string recipeInputHash = RequireString(published, "recipeInputHash");
            string bakerId = RequireString(published, "bakerId");
            string bakerVersion = RequireString(published, "bakerVersion");
            if (!string.Equals(bakerId, SupportedBakerId, StringComparison.Ordinal) || !string.Equals(bakerVersion, SupportedBakerVersion, StringComparison.Ordinal))
                throw new NotSupportedException("Unsupported VFX Recipe baker: " + bakerId + "@" + bakerVersion);

            var recipes = IndexById(RequireArray(catalog, "recipes"), "recipeId");
            var primitives = IndexById(RequireArray(catalog, "primitives"), "primitiveId");
            if (!recipes.TryGetValue(recipeId, out JObject recipe))
                throw new InvalidOperationException("VFX Recipe is missing: " + recipeId);

            var root = new GameObject(effectId.Replace('.', '_'));
            try
            {
                AppendRecipeLayers(root.transform, recipe, recipes, primitives, new HashSet<string>(StringComparer.Ordinal));
                PublishEffect(
                    root,
                    effectId,
                    recipeId,
                    recipeInputHash,
                    bakerId,
                    bakerVersion,
                    outputPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            return outputPath;
        }

        private static void AppendRecipeLayers(Transform parent, JObject recipe, Dictionary<string, JObject> recipes, Dictionary<string, JObject> primitives, HashSet<string> visiting)
        {
            string recipeId = RequireString(recipe, "recipeId");
            if (!visiting.Add(recipeId))
                throw new InvalidOperationException("VFX Recipe dependency cycle: " + recipeId);
            JArray included = recipe["includedRecipeIds"] as JArray;
            if (included != null)
            {
                for (int i = 0; i < included.Count; i++)
                {
                    string includedId = included[i]?.Value<string>();
                    if (!recipes.TryGetValue(includedId ?? string.Empty, out JObject childRecipe))
                        throw new InvalidOperationException("Included VFX Recipe is missing: " + includedId);
                    AppendRecipeLayers(parent, childRecipe, recipes, primitives, visiting);
                }
            }

            JArray layers = recipe["layers"] as JArray ?? new JArray();
            foreach (JObject layer in layers.OfType<JObject>().OrderBy(item => item.Value<int?>("order") ?? 0).ThenBy(item => item.Value<string>("layerId"), StringComparer.Ordinal))
            {
                string primitiveId = RequireString(layer, "primitiveId");
                if (!primitives.TryGetValue(primitiveId, out JObject primitive))
                    throw new InvalidOperationException("VFX Primitive is missing: " + primitiveId);
                if (!string.Equals(primitive.Value<string>("backend"), "ParticleSystem", StringComparison.Ordinal))
                    throw new NotSupportedException("ParticleSystem baker cannot bake backend " + primitive.Value<string>("backend") + ": " + primitiveId);
                JObject assetRef = primitive["assetRef"] as JObject ?? throw new InvalidOperationException("VFX Primitive assetRef is missing: " + primitiveId);
                string assetPath = RequireProjectPrefabPath(RequireString(assetRef, "path"));
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (source == null)
                    throw new FileNotFoundException("VFX Primitive Prefab is missing.", assetPath);
                GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("Could not instantiate VFX Primitive: " + primitiveId);
                instance.name = RequireString(layer, "layerId");
                instance.transform.SetParent(parent, false);
                ApplyOverrides(instance, layer["overrides"] as JObject);
                JObject bindings = layer["bindings"] as JObject;
                if (bindings != null && bindings.Properties().Any())
                    throw new NotSupportedException("ParticleSystem baker v1 requires bindings to be resolved into static layer overrides: " + recipeId + "/" + instance.name);
            }
            visiting.Remove(recipeId);
        }

        private static void ApplyOverrides(GameObject instance, JObject overrides)
        {
            if (overrides == null)
                return;
            foreach (JProperty property in overrides.Properties())
            {
                switch (property.Name)
                {
                    case "active": instance.SetActive(property.Value.Value<bool>()); break;
                    case "localPosition": instance.transform.localPosition = ReadVector3(property.Value, property.Name); break;
                    case "localEulerAngles": instance.transform.localEulerAngles = ReadVector3(property.Value, property.Name); break;
                    case "local_scale": instance.transform.localScale = property.Value.Type == JTokenType.Float || property.Value.Type == JTokenType.Integer
                            ? Vector3.one * property.Value.Value<float>()
                            : ReadVector3(property.Value, property.Name); break;
                    default: throw new NotSupportedException("Unsupported ParticleSystem Recipe layer override: " + property.Name);
                }
            }
        }

        private static Vector3 ReadVector3(JToken token, string field)
        {
            JArray values = token as JArray;
            if (values == null || values.Count != 3)
                throw new InvalidOperationException(field + " must be a three-number array.");
            return new Vector3(values[0].Value<float>(), values[1].Value<float>(), values[2].Value<float>());
        }

        internal static void PublishEffect(
            GameObject root,
            string effectId,
            string recipeId,
            string recipeInputHash,
            string bakerId,
            string bakerVersion,
            string outputPath,
            Action<string, string> evidenceWriter = null,
            Func<string, string, bool> evidenceCommitter = null,
            Func<string, bool> evidenceImporter = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            List<string> createdOutputFolders = EnsureAssetFoldersTracked(Path.GetDirectoryName(outputPath)?.Replace('\\', '/'));
            bool completed = false;
            try
            {
                string evidencePath = GetEvidencePath(effectId);
                using var prefabTransaction = new VfxCandidatePrefabAssetTransaction(outputPath);
                if (!prefabTransaction.Save(root, out string prefabSaveMessage))
                    throw new InvalidOperationException(prefabSaveMessage);

                string existingGuid = AssetDatabase.AssetPathToGUID(outputPath);
                string predictedGuid = string.IsNullOrWhiteSpace(existingGuid)
                    ? AssetDatabase.AssetPathToGUID(prefabTransaction.TemporaryPath)
                    : existingGuid;
                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabTransaction.TemporaryPath);
                if (candidate == null || string.IsNullOrWhiteSpace(predictedGuid))
                    throw new InvalidOperationException("Unity failed to load the Published Effect candidate Prefab.");

                JObject evidence = BuildEvidence(
                    candidate, effectId, recipeId, recipeInputHash, bakerId, bakerVersion, outputPath, predictedGuid);
                using var evidenceTransaction = new VfxPublishedEvidenceAssetTransaction(
                    evidencePath, evidenceWriter, evidenceCommitter, evidenceImporter);
                if (!evidenceTransaction.Prepare(evidence, out string evidencePrepareMessage))
                    throw new InvalidOperationException(evidencePrepareMessage);
                if (!prefabTransaction.Commit(out string prefabCommitMessage))
                    throw new InvalidOperationException(prefabCommitMessage);
                if (!evidenceTransaction.Commit(out string evidenceCommitMessage))
                    throw new InvalidOperationException(evidenceCommitMessage);

                string finalGuid = AssetDatabase.AssetPathToGUID(outputPath);
                if (!string.Equals(predictedGuid, finalGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException("Published Effect Prefab GUID changed during commit.");

                evidenceTransaction.Complete();
                prefabTransaction.Complete();
                completed = true;
            }
            finally
            {
                if (!completed) DeleteEmptyAssetFolders(createdOutputFolders);
            }
        }

        internal static string GetEvidencePath(string effectId)
        {
            return EvidenceRoot + "/" + effectId.Replace('.', '_') + ".json";
        }

        private static JObject BuildEvidence(
            GameObject candidate,
            string effectId,
            string recipeId,
            string recipeInputHash,
            string bakerId,
            string bakerVersion,
            string outputPath,
            string outputGuid)
        {
            return new JObject
            {
                ["schemaVersion"] = "mx.vfx.authoring.bake-evidence.v1",
                ["effectId"] = effectId,
                ["recipeId"] = recipeId,
                ["recipeInputHash"] = recipeInputHash,
                ["bakerId"] = bakerId,
                ["bakerVersion"] = bakerVersion,
                ["outputAssetPath"] = outputPath,
                ["outputAssetGuid"] = outputGuid,
                ["gameObjectCount"] = candidate.GetComponentsInChildren<Transform>(true).Length,
                ["particleSystemCount"] = candidate.GetComponentsInChildren<ParticleSystem>(true).Length,
                ["rendererCount"] = candidate.GetComponentsInChildren<Renderer>(true).Length
            };
        }

        private static JObject LoadCatalog()
        {
            if (!File.Exists(CatalogPath))
                throw new FileNotFoundException("VFX Authoring Catalog is missing.", CatalogPath);
            return JObject.Parse(File.ReadAllText(CatalogPath));
        }

        private static Dictionary<string, JObject> IndexById(JArray array, string field)
        {
            var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject item in array.OfType<JObject>())
                result.Add(RequireString(item, field), item);
            return result;
        }

        private static JObject FindById(JArray array, string field, string id)
        {
            return array.OfType<JObject>().FirstOrDefault(item => string.Equals(item.Value<string>(field), id, StringComparison.Ordinal));
        }

        private static JArray RequireArray(JObject owner, string field) => owner[field] as JArray ?? throw new InvalidOperationException("VFX Catalog array is missing: " + field);
        private static string RequireString(JObject owner, string field)
        {
            string value = owner.Value<string>(field);
            return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException("VFX authoring field is required: " + field);
        }

        private static string RequireProjectPrefabPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || !normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || normalized.Contains("../"))
                throw new InvalidOperationException("VFX Prefab path must remain under Assets/: " + normalized);
            return normalized;
        }

        private static List<string> EnsureAssetFoldersTracked(string path)
        {
            var created = new List<string>();
            if (string.IsNullOrWhiteSpace(path) || string.Equals(path, "Assets", StringComparison.Ordinal))
                return created;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    created.Add(next);
                }
                current = next;
            }
            return created;
        }

        private static void DeleteEmptyAssetFolders(List<string> createdFolders)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            for (int i = createdFolders.Count - 1; i >= 0; i--)
            {
                string folder = createdFolders[i];
                string absolute = Path.GetFullPath(Path.Combine(projectRoot, folder));
                if (Directory.Exists(absolute) && Directory.GetFileSystemEntries(absolute).Length == 0)
                    AssetDatabase.DeleteAsset(folder);
            }
        }
    }

    internal sealed class VfxPublishedEvidenceAssetTransaction : IDisposable
    {
        private readonly string _outputPath;
        private readonly string _absoluteOutputPath;
        private readonly string _absoluteMetaPath;
        private readonly string _absoluteDirectory;
        private readonly List<string> _createdDirectories;
        private readonly Action<string, string> _writer;
        private readonly Func<string, string, bool> _committer;
        private readonly Func<string, bool> _importer;
        private bool _prepared;
        private bool _commitApplied;
        private bool _completed;
        private bool _hadExistingOutput;
        private byte[] _originalContents;
        private byte[] _originalMetaContents;

        internal VfxPublishedEvidenceAssetTransaction(
            string outputPath,
            Action<string, string> writer = null,
            Func<string, string, bool> committer = null,
            Func<string, bool> importer = null)
        {
            _outputPath = outputPath.Replace('\\', '/');
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            _absoluteOutputPath = Path.GetFullPath(Path.Combine(projectRoot, _outputPath));
            _absoluteMetaPath = _absoluteOutputPath + ".meta";
            _absoluteDirectory = Path.GetDirectoryName(_absoluteOutputPath)
                ?? throw new InvalidOperationException("Published Effect evidence directory is missing.");
            _createdDirectories = FindMissingDirectories(_absoluteDirectory);
            Directory.CreateDirectory(_absoluteDirectory);
            TemporaryPath = Path.Combine(
                _absoluteDirectory,
                "." + Path.GetFileName(_absoluteOutputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            _writer = writer ?? WriteText;
            _committer = committer ?? CommitFile;
            _importer = importer ?? ImportEvidence;
        }

        internal string TemporaryPath { get; }

        internal bool Prepare(JObject evidence, out string message)
        {
            try
            {
                string json = evidence.ToString(Formatting.Indented) + Environment.NewLine;
                _writer(TemporaryPath, json);
                JObject roundTrip = JObject.Parse(File.ReadAllText(TemporaryPath));
                if (!JToken.DeepEquals(evidence, roundTrip))
                    throw new InvalidDataException("Published Effect evidence roundtrip changed its content.");
                _prepared = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not prepare Published Effect evidence: " + exception.Message;
                return false;
            }
        }

        internal bool Commit(out string message)
        {
            if (!_prepared || !File.Exists(TemporaryPath))
            {
                message = "Published Effect evidence temporary file is missing.";
                return false;
            }

            try
            {
                _hadExistingOutput = File.Exists(_absoluteOutputPath);
                if (_hadExistingOutput)
                {
                    _originalContents = File.ReadAllBytes(_absoluteOutputPath);
                    _originalMetaContents = File.Exists(_absoluteMetaPath) ? File.ReadAllBytes(_absoluteMetaPath) : null;
                }

                _commitApplied = true;
                if (!_committer(TemporaryPath, _absoluteOutputPath))
                    throw new IOException("Evidence commit seam rejected the replacement.");
                if (!_importer(_outputPath))
                    throw new IOException("Unity failed to import the new evidence asset.");

                JObject.Parse(File.ReadAllText(_absoluteOutputPath));
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "Published Effect evidence commit failed and was rolled back: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "Published Effect evidence commit failed: " + exception.Message
                        + " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        internal void Complete()
        {
            if (!_commitApplied) throw new InvalidOperationException("Published Effect evidence has not been committed.");
            _completed = true;
            _originalContents = null;
            _originalMetaContents = null;
        }

        private void RollBack()
        {
            if (!_commitApplied) return;
            try
            {
                if (_hadExistingOutput)
                {
                    File.WriteAllBytes(_absoluteOutputPath, _originalContents);
                    RestoreMeta();
                    if (!_importer(_outputPath))
                        throw new IOException("Unity failed to reimport the restored evidence asset.");
                }
                else
                {
                    if (File.Exists(_absoluteOutputPath)) File.Delete(_absoluteOutputPath);
                    if (File.Exists(_absoluteMetaPath)) File.Delete(_absoluteMetaPath);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally
            {
                _commitApplied = false;
            }
        }

        private void RestoreMeta()
        {
            if (_originalMetaContents != null) File.WriteAllBytes(_absoluteMetaPath, _originalMetaContents);
            else if (File.Exists(_absoluteMetaPath)) File.Delete(_absoluteMetaPath);
        }

        private static void WriteText(string path, string json)
        {
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private static bool CommitFile(string temporaryPath, string outputPath)
        {
            if (File.Exists(outputPath)) File.Replace(temporaryPath, outputPath, null, true);
            else File.Move(temporaryPath, outputPath);
            return true;
        }

        private static List<string> FindMissingDirectories(string directory)
        {
            var result = new List<string>();
            string current = directory;
            while (!string.IsNullOrWhiteSpace(current) && !Directory.Exists(current))
            {
                result.Add(current);
                current = Path.GetDirectoryName(current);
            }
            return result;
        }

        private static bool ImportEvidence(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath) != null;
        }

        public void Dispose()
        {
            try
            {
                if (_commitApplied && !_completed) RollBack();
            }
            finally
            {
                if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
                string temporaryMeta = TemporaryPath + ".meta";
                if (File.Exists(temporaryMeta)) File.Delete(temporaryMeta);
                for (int i = 0; i < _createdDirectories.Count; i++)
                {
                    string directory = _createdDirectories[i];
                    if (!Directory.Exists(directory) || Directory.GetFileSystemEntries(directory).Length != 0) continue;
                    Directory.Delete(directory);
                    string directoryMeta = directory + ".meta";
                    if (File.Exists(directoryMeta)) File.Delete(directoryMeta);
                }
                if (_createdDirectories.Count > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
