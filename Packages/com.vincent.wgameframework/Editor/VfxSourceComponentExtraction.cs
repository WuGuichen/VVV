using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxSourceExtractionRequestView
    {
        [JsonProperty("primitiveId")] public string PrimitiveId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("outputPrefabPath")] public string OutputPrefabPath = string.Empty;
        [JsonProperty("componentIds")] public List<string> ComponentIds = new List<string>();
        [JsonProperty("localScale")] public float LocalScale = 1f;
        [JsonProperty("themeTint")] public List<float> ThemeTint = new List<float> { 1f, 1f, 1f, 1f };
        [JsonProperty("deriveMaterials")] public bool DeriveMaterials;
        [JsonProperty("variantOverrideHash")] public string VariantOverrideHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceExtractionPlanView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("sourceLibraryId")] public string SourceLibraryId = string.Empty;
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("primitiveId")] public string PrimitiveId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("outputPrefabPath")] public string OutputPrefabPath = string.Empty;
        [JsonProperty("dependencyPolicy")] public string DependencyPolicy = string.Empty;
        [JsonProperty("components")] public List<VfxSourceComponentView> Components = new List<VfxSourceComponentView>();
        [JsonProperty("localScale")] public float LocalScale = 1f;
        [JsonProperty("themeTint")] public List<float> ThemeTint = new List<float>();
        [JsonProperty("deriveMaterials")] public bool DeriveMaterials;
        [JsonProperty("variantOverrideHash")] public string VariantOverrideHash = string.Empty;
        [JsonProperty("registryHash")] public string RegistryHash = string.Empty;
        [JsonProperty("resolution")] public string Resolution = "NotExtracted";
        [JsonProperty("reusePrimitiveIds")] public List<string> ReusePrimitiveIds = new List<string>();
        [JsonProperty("missingComponentIds")] public List<string> MissingComponentIds = new List<string>();
        [JsonProperty("blocked")] public bool Blocked;
        [JsonProperty("diagnostics")] public List<string> Diagnostics = new List<string>();
        [JsonProperty("planHash")] public string PlanHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceExtractionReceiptView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.source-extraction-receipt.v1";
        [JsonProperty("planHash")] public string PlanHash = string.Empty;
        [JsonProperty("primitiveId")] public string PrimitiveId = string.Empty;
        [JsonProperty("outputPrefabPath")] public string OutputPrefabPath = string.Empty;
        [JsonProperty("outputPrefabGuid")] public string OutputPrefabGuid = string.Empty;
        [JsonProperty("outputContentHash")] public string OutputContentHash = string.Empty;
        [JsonProperty("outputDependencyHash")] public string OutputDependencyHash = string.Empty;
        [JsonProperty("sourceComponentIds")] public List<string> SourceComponentIds = new List<string>();
        [JsonProperty("directSourceDependencyGuids")] public List<string> DirectSourceDependencyGuids = new List<string>();
        [JsonProperty("derivedAssetPaths")] public List<string> DerivedAssetPaths = new List<string>();
        [JsonProperty("forbiddenComponents")] public List<string> ForbiddenComponents = new List<string>();
        [JsonProperty("unitySerializationUsed")] public bool UnitySerializationUsed;
        [JsonProperty("extractorId")] public string ExtractorId = "mx.vfx.source-component-extractor";
        [JsonProperty("extractorVersion")] public string ExtractorVersion = "1";
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceCompositionPlanView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("recipeId")] public string RecipeId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("outputPrefabPath")] public string OutputPrefabPath = string.Empty;
        [JsonProperty("expectedCatalogHash")] public string ExpectedCatalogHash = string.Empty;
        [JsonProperty("layers")] public List<VfxSourceCompositionLayerView> Layers = new List<VfxSourceCompositionLayerView>();
        [JsonProperty("blocked")] public bool Blocked;
        [JsonProperty("diagnostics")] public List<string> Diagnostics = new List<string>();
        [JsonProperty("planHash")] public string PlanHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceCompositionLayerView
    {
        [JsonProperty("layerId")] public string LayerId = string.Empty;
        [JsonProperty("primitiveId")] public string PrimitiveId = string.Empty;
        [JsonProperty("order")] public int Order;
    }

    /// <summary>Executes a validated Source Library extraction plan through Unity serialization APIs.</summary>
    /// <remarks>Editor-only, AllocByDesign. It never writes below Assets/Piloto Studio and never copies source scripts.</remarks>
    internal sealed class VfxSourceComponentExtractionService
    {
        private const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        private const string WorkRoot = "Temp/MxFramework/VfxSourceLibrary/Operations";

        public bool PlanAndExtract(VfxSourceLibraryView index, VfxSourceExtractionRequestView request, bool registerReviewNeeded, out VfxSourceExtractionReceiptView receipt, out string message)
        {
            receipt = null;
            if (index == null || !index.DeepScanComplete) { message = "必须先完成原材料深度扫描。"; return false; }
            Directory.CreateDirectory(WorkRoot);
            string token = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            string requestPath = WorkRoot + "/extract-request-" + token + ".json";
            string planPath = WorkRoot + "/extract-plan-" + token + ".json";
            File.WriteAllText(requestPath, JsonConvert.SerializeObject(request, Formatting.Indented), new UTF8Encoding(false));
            string command = "vfx source plan-extract --project-root . --index " + VfxSourceLibraryEditorService.Quote(VfxSourceLibraryEditorService.IndexPath) + " --request " + VfxSourceLibraryEditorService.Quote(requestPath) + " --output " + VfxSourceLibraryEditorService.Quote(planPath);
            if (!VfxSourceLibraryEditorService.RunCli(command, out message) || !File.Exists(planPath)) return false;
            VfxSourceExtractionPlanView plan = JsonConvert.DeserializeObject<VfxSourceExtractionPlanView>(File.ReadAllText(planPath));
            if (plan == null || plan.Blocked) { message = plan == null ? "提取计划无法读取。" : string.Join("\n", plan.Diagnostics); return false; }
            if (!string.Equals(plan.SourceIndexHash, index.IndexHash, StringComparison.Ordinal)) { message = "原材料索引已变化，请重新生成提取计划。"; return false; }
            if (plan.Resolution == "ReuseExisting")
            {
                message = "来源已经提取，强制复用现有 Primitive：" + string.Join(", ", plan.ReusePrimitiveIds);
                return true;
            }
            if (AssetDatabase.LoadMainAssetAtPath(plan.OutputPrefabPath) != null)
            {
                message = "目标路径已存在未登记资产，提取已阻断：" + plan.OutputPrefabPath;
                return false;
            }
            if (!Execute(plan, out receipt, out message)) return false;
            string receiptPath = WorkRoot + "/extract-receipt-" + token + ".json";
            File.WriteAllText(receiptPath, JsonConvert.SerializeObject(receipt, Formatting.Indented), new UTF8Encoding(false));
            if (!VfxSourceLibraryEditorService.RunCli(
                    "vfx source seal-receipt --index " + VfxSourceLibraryEditorService.Quote(VfxSourceLibraryEditorService.IndexPath)
                    + " --input " + VfxSourceLibraryEditorService.Quote(receiptPath)
                    + " --output " + VfxSourceLibraryEditorService.Quote(receiptPath),
                    out message))
            {
                CleanupExtractionAssets(plan, receipt);
                return false;
            }
            receipt = JsonConvert.DeserializeObject<VfxSourceExtractionReceiptView>(File.ReadAllText(receiptPath));
            string validationPath = WorkRoot + "/extract-validation-" + token + ".json";
            if (!VfxSourceLibraryEditorService.RunCli("vfx source validate-extraction --project-root . --index " + VfxSourceLibraryEditorService.Quote(VfxSourceLibraryEditorService.IndexPath) + " --plan " + VfxSourceLibraryEditorService.Quote(planPath) + " --receipt " + VfxSourceLibraryEditorService.Quote(receiptPath) + " --output " + VfxSourceLibraryEditorService.Quote(validationPath), out message))
            {
                CleanupExtractionAssets(plan, receipt);
                return false;
            }
            if (registerReviewNeeded && !RegisterExtractionTransaction(index, plan, receipt, out message)) return false;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            message = registerReviewNeeded ? "组件已提取并登记为 ReviewNeeded；未设置 Ready。" : "组件已提取为本地 Draft。";
            return true;
        }

        private static bool RegisterExtractionTransaction(VfxSourceLibraryView index, VfxSourceExtractionPlanView plan, VfxSourceExtractionReceiptView receipt, out string message)
        {
            string curationPath = VfxSourceLibraryEditorService.CurationPath;
            string registryPath = VfxSourceExtractionRegistryService.RegistryPath;
            string catalogBefore = File.ReadAllText(CatalogPath);
            string curationBefore = File.Exists(curationPath) ? File.ReadAllText(curationPath) : null;
            string registryBefore = File.Exists(registryPath) ? File.ReadAllText(registryPath) : null;
            try
            {
                if (!VfxSourceCatalogGovernance.RegisterPrimitive(index, plan, receipt, out message)) throw new InvalidOperationException(message);
                VfxSourceLibraryEditorService.RecordPrimitiveMapping(plan.Components, plan.PrimitiveId);
                var registryService = new VfxSourceExtractionRegistryService();
                registryService.Save(registryService.BuildFromCatalog(index));
                if (!VfxSourceLibraryEditorService.RunCli("vfx source validate-registry --project-root . --index " + VfxSourceLibraryEditorService.Quote(VfxSourceLibraryEditorService.IndexPath) + " --registry " + VfxSourceLibraryEditorService.Quote(registryPath), out message))
                    throw new InvalidOperationException(message);
                return true;
            }
            catch (Exception exception)
            {
                RestoreJsonAsset(CatalogPath, catalogBefore);
                RestoreJsonAsset(curationPath, curationBefore);
                RestoreJsonAsset(registryPath, registryBefore);
                CleanupExtractionAssets(plan, receipt);
                message = "提取登记事务已回滚：" + exception.Message;
                return false;
            }
        }

        private static void RestoreJsonAsset(string path, string contents)
        {
            if (contents == null)
            {
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                return;
            }
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CleanupExtractionAssets(VfxSourceExtractionPlanView plan, VfxSourceExtractionReceiptView receipt)
        {
            foreach (string path in (receipt?.DerivedAssetPaths ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)))
                AssetDatabase.DeleteAsset(path);
            if (!string.IsNullOrWhiteSpace(plan?.OutputPrefabPath)) AssetDatabase.DeleteAsset(plan.OutputPrefabPath);
        }

        internal static bool Execute(
            VfxSourceExtractionPlanView plan,
            out VfxSourceExtractionReceiptView receipt,
            out string message,
            Func<GameObject, string, bool> prefabSaver = null,
            Action<int> afterComponent = null)
        {
            receipt = new VfxSourceExtractionReceiptView { PlanHash = plan.PlanHash, PrimitiveId = plan.PrimitiveId, OutputPrefabPath = plan.OutputPrefabPath, UnitySerializationUsed = true };
            message = string.Empty;
            var transaction = new VfxExtractionAssetTransaction(plan.OutputPrefabPath);
            var materialCopies = new Dictionary<Material, Material>();
            GameObject outputRoot = null;
            bool completed = false;
            try
            {
                transaction.EnsureFolder(Path.GetDirectoryName(plan.OutputPrefabPath)?.Replace('\\', '/') ?? "Assets/VFX/MxFramework/Review/Piloto/Primitives");
                outputRoot = new GameObject(string.IsNullOrWhiteSpace(plan.DisplayName) ? plan.PrimitiveId : plan.DisplayName);
                for (int componentIndex = 0; componentIndex < plan.Components.Count; componentIndex++)
                {
                    VfxSourceComponentView evidence = plan.Components[componentIndex];
                    GameObject sourceRoot = PrefabUtility.LoadPrefabContents(evidence.PrefabPath);
                    try
                    {
                        ParticleSystem source = FindSourceSystem(sourceRoot, evidence);
                        if (source == null) { message = "找不到源 ParticleSystem：" + evidence.ComponentId; return false; }
                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(source.gameObject);
                        MonoBehaviour[] scripts = source.GetComponents<MonoBehaviour>();
                        if (missing > 0 || scripts.Any(value => value != null))
                        {
                            receipt.ForbiddenComponents.Add(evidence.ComponentId + ":source-script-dependency");
                            message = "源职责层包含脚本依赖，必须先建立正式 adapter：" + evidence.ObjectPath;
                            return false;
                        }
                        GameObject layer = new GameObject(string.IsNullOrWhiteSpace(evidence.DisplayName) ? "Layer " + componentIndex : evidence.DisplayName);
                        layer.transform.SetParent(outputRoot.transform, false);
                        CopyRelativeTransform(source.transform, sourceRoot.transform, layer.transform, plan.LocalScale);
                        ParticleSystem target = layer.AddComponent<ParticleSystem>();
                        EditorUtility.CopySerialized(source, target);
                        ParticleSystemRenderer sourceRenderer = source.GetComponent<ParticleSystemRenderer>();
                        ParticleSystemRenderer targetRenderer = target.GetComponent<ParticleSystemRenderer>();
                        if (sourceRenderer != null && targetRenderer != null) EditorUtility.CopySerialized(sourceRenderer, targetRenderer);
                        ApplyTint(target, ThemeColor(plan.ThemeTint));
                        if (plan.DeriveMaterials && targetRenderer != null) DeriveMaterials(plan, targetRenderer, materialCopies, receipt, transaction);
                        receipt.SourceComponentIds.Add(evidence.ComponentId);
                        receipt.DirectSourceDependencyGuids.AddRange(evidence.MaterialGuids);
                        receipt.DirectSourceDependencyGuids.AddRange(evidence.MeshGuids);
                        afterComponent?.Invoke(componentIndex);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(sourceRoot); }
                }
                bool saved = (prefabSaver ?? SavePrefab)(outputRoot, plan.OutputPrefabPath);
                if (!saved) { message = "Unity 未能保存提取后的 Primitive Prefab。"; return false; }
                AssetDatabase.ImportAsset(plan.OutputPrefabPath, ImportAssetOptions.ForceSynchronousImport);
                receipt.OutputPrefabGuid = AssetDatabase.AssetPathToGUID(plan.OutputPrefabPath);
                receipt.OutputContentHash = ContentHash(plan.OutputPrefabPath);
                receipt.OutputDependencyHash = AssetDatabase.GetAssetDependencyHash(plan.OutputPrefabPath).ToString();
                receipt.DirectSourceDependencyGuids = receipt.DirectSourceDependencyGuids.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                transaction.Complete();
                completed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                message = "VFX extraction asset transaction failed: " + exception.Message;
                return false;
            }
            finally
            {
                if (outputRoot != null) Object.DestroyImmediate(outputRoot);
                if (!completed && !transaction.TryRollback(out string cleanupError))
                    message = (message ?? string.Empty) + " Cleanup was incomplete: " + cleanupError;
            }
        }

        private static bool SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            return success && saved != null;
        }

        private static ParticleSystem FindSourceSystem(GameObject sourceRoot, VfxSourceComponentView evidence)
        {
            ParticleSystem[] systems = sourceRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(systems[i], out _, out long localId) && localId == evidence.LocalFileId) return systems[i];
            Transform transform = sourceRoot.transform.Find(evidence.ObjectPath);
            return transform != null ? transform.GetComponent<ParticleSystem>() : null;
        }

        private static void CopyRelativeTransform(Transform source, Transform sourceRoot, Transform target, float scale)
        {
            Matrix4x4 relative = sourceRoot.worldToLocalMatrix * source.localToWorldMatrix;
            target.localPosition = relative.GetColumn(3);
            target.localRotation = relative.rotation;
            target.localScale = relative.lossyScale * scale;
        }

        private static void ApplyTint(ParticleSystem system, Color tint)
        {
            if (tint == Color.white) return;
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxGradient source = main.startColor;
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color: source.color = Multiply(source.color, tint); break;
                case ParticleSystemGradientMode.TwoColors: source.colorMin = Multiply(source.colorMin, tint); source.colorMax = Multiply(source.colorMax, tint); break;
                case ParticleSystemGradientMode.Gradient: source.gradient = TintGradient(source.gradient, tint); break;
                case ParticleSystemGradientMode.TwoGradients: source.gradientMin = TintGradient(source.gradientMin, tint); source.gradientMax = TintGradient(source.gradientMax, tint); break;
            }
            main.startColor = source;
        }

        private static void DeriveMaterials(
            VfxSourceExtractionPlanView plan,
            ParticleSystemRenderer renderer,
            Dictionary<Material, Material> copies,
            VfxSourceExtractionReceiptView receipt,
            VfxExtractionAssetTransaction transaction)
        {
            string root = Path.GetDirectoryName(plan.OutputPrefabPath)?.Replace('\\', '/') + "/Materials";
            transaction.EnsureFolder(root);
            Material[] materials = renderer.sharedMaterials;
            Color tint = ThemeColor(plan.ThemeTint);
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;
                if (!copies.TryGetValue(source, out Material copy))
                {
                    copy = new Material(source) { name = source.name + "_" + FileToken(plan.PrimitiveId) };
                    TintMaterial(copy, tint);
                    string path = AssetDatabase.GenerateUniqueAssetPath(root + "/" + FileToken(copy.name) + ".mat");
                    transaction.CreateAsset(copy, path);
                    copies.Add(source, copy);
                    receipt.DerivedAssetPaths.Add(path);
                }
                materials[i] = copy;
            }
            renderer.sharedMaterials = materials;
        }

        private static void TintMaterial(Material material, Color tint)
        {
            string[] properties = { "_BaseColor", "_Color", "_TintColor" };
            for (int i = 0; i < properties.Length; i++) if (material.HasColor(properties[i])) material.SetColor(properties[i], Multiply(material.GetColor(properties[i]), tint));
        }

        private static Gradient TintGradient(Gradient source, Color tint)
        {
            if (source == null) return null;
            GradientColorKey[] colors = source.colorKeys;
            for (int i = 0; i < colors.Length; i++) colors[i].color = Multiply(colors[i].color, tint);
            var result = new Gradient { mode = source.mode };
            result.SetKeys(colors, source.alphaKeys);
            return result;
        }

        private static Color Multiply(Color left, Color right) => new Color(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);
        private static Color ThemeColor(List<float> values) => values != null && values.Count == 4 ? new Color(values[0], values[1], values[2], values[3]) : Color.white;
        private static string FileToken(string value) => new string((value ?? string.Empty).Select(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray());
        private static string ContentHash(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        internal static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets", StringComparison.Ordinal)) return;
            string current = "Assets";
            foreach (string part in path.Substring(6).Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }

    internal sealed class VfxExtractionAssetTransaction
    {
        private readonly string _outputPrefabPath;
        private readonly bool _ownsOutputPrefab;
        private readonly List<string> _createdAssetPaths = new List<string>();
        private readonly List<string> _createdFolders = new List<string>();
        private bool _completed;

        internal VfxExtractionAssetTransaction(string outputPrefabPath, bool ownsOutputPrefab = true)
        {
            _outputPrefabPath = outputPrefabPath;
            _ownsOutputPrefab = ownsOutputPrefab;
        }

        internal void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets", StringComparison.Ordinal)) return;
            string current = "Assets";
            foreach (string part in path.Substring(6).Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, part);
                    if (string.IsNullOrWhiteSpace(guid))
                        throw new InvalidOperationException("Unity could not create extraction output folder: " + next);
                    _createdFolders.Add(next);
                }
                current = next;
            }
        }

        internal void CreateAsset(Object asset, string path)
        {
            try
            {
                AssetDatabase.CreateAsset(asset, path);
            }
            finally
            {
                if ((AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
                    && !_createdAssetPaths.Contains(path))
                    _createdAssetPaths.Add(path);
            }
        }

        internal void Complete()
        {
            _completed = true;
        }

        internal bool TryRollback(out string error)
        {
            error = string.Empty;
            if (_completed) return true;
            var failures = new List<string>();
            for (int i = _createdAssetPaths.Count - 1; i >= 0; i--)
                TryDeleteAsset(_createdAssetPaths[i], failures);
            if (_ownsOutputPrefab) TryDeleteAsset(_outputPrefabPath, failures);
            for (int i = _createdFolders.Count - 1; i >= 0; i--)
            {
                string folder = _createdFolders[i];
                try
                {
                    if (AssetDatabase.IsValidFolder(folder)
                        && Directory.Exists(folder)
                        && Directory.GetFileSystemEntries(folder).Length == 0)
                        AssetDatabase.DeleteAsset(folder);
                }
                catch (Exception exception)
                {
                    failures.Add(folder + ": " + exception.Message);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            error = string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static void TryDeleteAsset(string path, List<string> failures)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                AssetDatabase.DeleteAsset(path);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
            }
            catch (Exception exception)
            {
                failures.Add(path + ": " + exception.Message);
            }
        }
    }

    internal static class VfxSourceCatalogGovernance
    {
        private const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        private const string CliProject = "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj";

        public static bool RegisterPrimitive(VfxSourceLibraryView index, VfxSourceExtractionPlanView plan, VfxSourceExtractionReceiptView receipt, out string message)
        {
            JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            string expectedHash = HashFile(CatalogPath);
            VfxSourceComponentView primary = plan.Components[0];
            JObject primitive = new JObject
            {
                ["primitiveId"] = plan.PrimitiveId,
                ["displayName"] = plan.DisplayName,
                ["description"] = "Reusable VFX responsibility extracted through the Source Library plan and Unity Editor API.",
                ["assetRef"] = new JObject { ["kind"] = "UnityAsset", ["path"] = plan.OutputPrefabPath },
                ["source"] = new JObject
                {
                    ["sourceKind"] = "LicensedThirdParty", ["sourcePackageId"] = primary.SourcePackageId, ["sourceLibraryId"] = index.SourceLibraryId,
                    ["sourceEffectId"] = primary.SourceEffectId, ["sourceComponentIds"] = new JArray(plan.Components.Select(value => value.ComponentId)),
                    ["sourcePrefabGuid"] = primary.PrefabGuid, ["sourceObjectPath"] = string.Join(";", plan.Components.Select(value => value.ObjectPath)),
                    ["sourceContentHash"] = primary.SourceContentHash, ["sourceDependencyHash"] = primary.DependencyHash,
                    ["derivationMode"] = plan.DeriveMaterials ? "ParameterizedVariant" : "ExtractedLayer", ["dependencyPolicy"] = plan.DependencyPolicy,
                    ["variantOverrideHash"] = plan.DeriveMaterials ? plan.VariantOverrideHash : string.Empty,
                    ["extractorId"] = receipt.ExtractorId, ["extractorVersion"] = receipt.ExtractorVersion,
                    ["licenseId"] = index.LicenseId, ["publicRedistributionAllowed"] = index.PublicRedistributionAllowed
                },
                ["backend"] = "ParticleSystem",
                ["roles"] = new JArray(CatalogRoles(plan.Components)),
                ["compatibleAnchors"] = new JArray(CatalogAnchors(plan.Components)),
                ["parameters"] = new JArray(
                    Parameter("local_scale", "Float", "Multiplier", 1f, 0.1f, 4f, "Transform.localScale"),
                    Parameter("theme_tint", "Color", "Normalized", new JArray(1f, 1f, 1f, 1f), null, null, "ParticleSystem.main.startColor")),
                ["visualMaturity"] = "Candidate", ["reviewStatus"] = "ReviewNeeded", ["exemplar"] = false,
                ["tags"] = new JArray("licensed", "piloto", "source-library", "component-extraction")
            };
            JArray primitives = (JArray)catalog["primitives"];
            JToken previous = primitives.FirstOrDefault(value => value.Value<string>("primitiveId") == plan.PrimitiveId);
            if (previous == null) primitives.Add(primitive); else previous.Replace(primitive);
            return GuardedWrite(catalog, expectedHash, "register-primitive", out message);
        }

        public static bool BackfillExistingPrimitiveEvidence(VfxSourceLibraryView index, out string message)
        {
            JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            string expectedHash = HashFile(CatalogPath);
            bool changed = false;
            var mappings = new List<KeyValuePair<string, List<VfxSourceComponentView>>>();
            foreach (JObject primitive in ((JArray)catalog["primitives"]).OfType<JObject>())
            {
                JObject source = primitive["source"] as JObject;
                if (source == null || source.Value<string>("sourceKind") != "LicensedThirdParty") continue;
                string prefabGuid = source.Value<string>("sourcePrefabGuid") ?? string.Empty;
                VfxSourceEffectView effect = index.Effects.FirstOrDefault(value => value.PrefabGuid == prefabGuid);
                if (effect == null) continue;
                string[] tokens = (source.Value<string>("sourceObjectPath") ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                List<VfxSourceComponentView> candidates = index.Components.Where(value => value.SourceEffectId == effect.SourceEffectId).ToList();
                var selected = new List<VfxSourceComponentView>();
                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i].Trim();
                    VfxSourceComponentView match = candidates.FirstOrDefault(value => value.ObjectPath == token || value.DisplayName == token || value.ObjectPath.EndsWith("/" + token, StringComparison.Ordinal));
                    if (match != null && !selected.Contains(match)) selected.Add(match);
                }
                if (selected.Count == 0 && candidates.Count == 1) selected.Add(candidates[0]);
                if (selected.Count == 0) continue;
                source["sourceLibraryId"] = index.SourceLibraryId;
                source["sourceEffectId"] = effect.SourceEffectId;
                source["sourceComponentIds"] = new JArray(selected.Select(value => value.ComponentId));
                source["sourceContentHash"] = effect.ContentHash;
                source["sourceDependencyHash"] = effect.DependencyHash;
                source["dependencyPolicy"] = "DirectReference";
                source["extractorId"] = "mx.vfx.source-component-extractor";
                source["extractorVersion"] = "1";
                mappings.Add(new KeyValuePair<string, List<VfxSourceComponentView>>(primitive.Value<string>("primitiveId") ?? string.Empty, selected));
                changed = true;
            }
            if (!changed) { message = "没有需要补齐的现有 Primitive 来源证据。"; return true; }
            if (!GuardedWrite(catalog, expectedHash, "backfill-source-evidence", out message)) return false;
            for (int i = 0; i < mappings.Count; i++) VfxSourceLibraryEditorService.RecordPrimitiveMapping(mappings[i].Value, mappings[i].Key);
            return true;
        }

        public static bool RegisterRecipe(VfxSourceCompositionPlanView plan, out string message)
        {
            JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            string expectedHash = HashFile(CatalogPath);
            var layers = new JArray(plan.Layers.OrderBy(value => value.Order).Select(value => new JObject
            {
                ["layerId"] = value.LayerId,
                ["primitiveId"] = value.PrimitiveId,
                ["order"] = (value.Order + 1) * 10,
                ["bindings"] = new JObject(),
                ["overrides"] = new JObject()
            }));
            JObject recipe = new JObject
            {
                ["recipeId"] = plan.RecipeId, ["displayName"] = plan.DisplayName,
                ["description"] = "Ordered VFX composition assembled from governed Source Library Primitives.",
                ["parameters"] = new JArray(), ["includedRecipeIds"] = new JArray(), ["layers"] = layers,
                ["visualMaturity"] = "Candidate", ["reviewStatus"] = "ReviewNeeded", ["canonical"] = false, ["exemplar"] = false,
                ["tags"] = new JArray("piloto", "source-library", "agent-assembled")
            };
            JArray recipes = (JArray)catalog["recipes"];
            JToken previous = recipes.FirstOrDefault(value => value.Value<string>("recipeId") == plan.RecipeId);
            if (previous == null) recipes.Add(recipe); else previous.Replace(recipe);
            return GuardedWrite(catalog, expectedHash, "register-recipe", out message);
        }

        private static bool GuardedWrite(JObject catalog, string expectedHash, string operation, out string message)
        {
            Directory.CreateDirectory("Temp/MxFramework/VfxSourceLibrary/Operations");
            string proposed = "Temp/MxFramework/VfxSourceLibrary/Operations/" + operation + "-catalog.json";
            File.WriteAllText(proposed, catalog.ToString(Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            var start = new ProcessStartInfo("dotnet", "run -c Release --no-restore --project " + VfxSourceLibraryEditorService.Quote(CliProject) + " -- vfx plan-save --project-root . --proposed " + VfxSourceLibraryEditorService.Quote(proposed) + " --expected-hash " + expectedHash)
            { WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using Process process = Process.Start(start);
            if (process == null) { message = "无法启动 Catalog dry-run。"; return false; }
            string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(120000) || process.ExitCode != 0) { message = "Catalog dry-run 阻断：\n" + output + error; return false; }
            JObject result = JObject.Parse(output);
            if (result.Value<bool?>("blocked") == true || result.Value<bool?>("wouldModifyUnityAssets") == true) { message = "Catalog dry-run 返回不安全计划。"; return false; }
            if (!string.Equals(expectedHash, HashFile(CatalogPath), StringComparison.OrdinalIgnoreCase)) { message = "Catalog 在确认前发生变化，未写入。"; return false; }
            try
            {
                using var transaction = new VfxCatalogAssetPublishTransaction(CatalogPath);
                if (!transaction.Prepare(catalog, out message)) return false;
                if (!transaction.Commit(out message)) return false;
            }
            catch (Exception exception)
            {
                message = "Catalog publish transaction failed: " + exception.Message;
                return false;
            }
            message = "Catalog-only 变更已通过 dry-run 与二次 hash guard。";
            return true;
        }

        private static IEnumerable<string> CatalogRoles(List<VfxSourceComponentView> components)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (VfxSourceComponentView component in components)
            {
                if (component.Roles.Contains("trail") || component.Roles.Contains("core") || component.Roles.Contains("arc")) result.Add("Flight");
                if (component.Roles.Contains("decal")) { result.Add("Impact"); result.Add("Zone"); }
                if (component.Roles.Contains("glow") || component.Roles.Contains("flame") || component.Roles.Contains("smoke")) result.Add("Charge");
                if (component.Roles.Any(value => value == "flash" || value == "sparks" || value == "shockwave" || value == "smoke" || value == "flame")) result.Add("Impact");
            }
            if (result.Count == 0) result.Add("Environment");
            return result.OrderBy(value => value, StringComparer.Ordinal);
        }

        private static IEnumerable<string> CatalogAnchors(List<VfxSourceComponentView> components)
        {
            var values = new HashSet<string>(components.Select(value => value.Anchor).Where(value => value == "Caster" || value == "Path" || value == "HitPoint" || value == "Ground" || value == "World"), StringComparer.Ordinal) { "World" };
            return values.OrderBy(value => value, StringComparer.Ordinal);
        }

        private static JObject Parameter(string id, string type, string unit, JToken value, float? min, float? max, string backend)
        {
            var result = new JObject { ["parameterId"] = id, ["valueType"] = type, ["unit"] = unit, ["defaultValue"] = value, ["transport"] = "StaticBake", ["backendProperty"] = backend, ["runtimeScale"] = 1f };
            if (min.HasValue) result["minValue"] = min.Value; if (max.HasValue) result["maxValue"] = max.Value; return result;
        }

        private static JObject Parameter(string id, string type, string unit, float value, float? min, float? max, string backend) => Parameter(id, type, unit, JToken.FromObject(value), min, max, backend);
        private static string HashFile(string path)
        {
            using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }

    internal sealed class VfxCatalogAssetPublishTransaction : IDisposable
    {
        private readonly string _catalogPath;
        private readonly string _metaPath;
        private readonly Action<string, string> _writer;
        private readonly Action<string, string, string> _replacer;
        private readonly Func<string, bool> _importer;
        private readonly byte[] _originalContents;
        private readonly byte[] _originalMetaContents;
        private readonly string _originalGuid;
        private JObject _preparedCatalog;
        private bool _replaceAttempted;
        private bool _committed;

        internal VfxCatalogAssetPublishTransaction(
            string catalogPath,
            Action<string, string> writer = null,
            Action<string, string, string> replacer = null,
            Func<string, bool> importer = null)
        {
            _catalogPath = VfxCandidateSandbox.NormalizeAssetPath(catalogPath);
            _metaPath = _catalogPath + ".meta";
            if (!File.Exists(_catalogPath))
                throw new FileNotFoundException("Authoritative VFX Catalog must already exist before guarded publish.", _catalogPath);
            _originalContents = File.ReadAllBytes(_catalogPath);
            _originalMetaContents = File.Exists(_metaPath) ? File.ReadAllBytes(_metaPath) : null;
            _originalGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            string directory = Path.GetDirectoryName(_catalogPath)
                ?? throw new InvalidOperationException("VFX Catalog directory is missing.");
            string token = Guid.NewGuid().ToString("N");
            TemporaryPath = Path.Combine(directory, "." + Path.GetFileName(_catalogPath) + "." + token + ".tmp");
            BackupPath = Path.Combine(directory, "." + Path.GetFileName(_catalogPath) + "." + token + ".bak");
            _writer = writer ?? WriteText;
            _replacer = replacer ?? ReplaceFile;
            _importer = importer ?? ImportCatalog;
        }

        internal string TemporaryPath { get; }
        internal string BackupPath { get; }

        internal bool Prepare(JObject catalog, out string message)
        {
            try
            {
                string json = catalog.ToString(Formatting.Indented) + Environment.NewLine;
                _writer(TemporaryPath, json);
                _preparedCatalog = JObject.Parse(File.ReadAllText(TemporaryPath));
                if (!JToken.DeepEquals(catalog, _preparedCatalog))
                    throw new InvalidDataException("VFX Catalog temp roundtrip changed its content.");
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                message = "VFX Catalog prepare failed without publishing: " + exception.Message;
                return false;
            }
        }

        internal bool Commit(out string message)
        {
            if (_preparedCatalog == null || !File.Exists(TemporaryPath))
            {
                message = "VFX Catalog temp file is missing or was not validated.";
                return false;
            }

            try
            {
                _replaceAttempted = true;
                _replacer(TemporaryPath, _catalogPath, BackupPath);
                if (!_importer(_catalogPath))
                    throw new IOException("Unity failed to import the published VFX Catalog.");
                JObject published = JObject.Parse(File.ReadAllText(_catalogPath));
                if (!JToken.DeepEquals(_preparedCatalog, published))
                    throw new InvalidDataException("Published VFX Catalog does not match the validated temp document.");
                if (!string.Equals(AssetDatabase.AssetPathToGUID(_catalogPath), _originalGuid, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Published VFX Catalog GUID/meta changed during replacement.");
                _committed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack();
                    message = "VFX Catalog publish failed and restored the original asset: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "VFX Catalog publish failed: " + exception.Message
                        + " Catalog rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        private void RollBack()
        {
            try
            {
                if (File.Exists(BackupPath) && File.Exists(_catalogPath))
                    File.Replace(BackupPath, _catalogPath, null, true);
                else
                    File.WriteAllBytes(_catalogPath, _originalContents);
                if (_originalMetaContents != null) File.WriteAllBytes(_metaPath, _originalMetaContents);
                else if (File.Exists(_metaPath)) File.Delete(_metaPath);
                if (!_importer(_catalogPath))
                    throw new IOException("Unity failed to reimport the restored VFX Catalog.");
                if (!File.ReadAllBytes(_catalogPath).SequenceEqual(_originalContents)
                    || !string.Equals(AssetDatabase.AssetPathToGUID(_catalogPath), _originalGuid, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Restored VFX Catalog bytes or GUID/meta do not match the prior state.");
            }
            finally
            {
                _replaceAttempted = false;
            }
        }

        private static void WriteText(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private static void ReplaceFile(string temporaryPath, string catalogPath, string backupPath)
        {
            File.Replace(temporaryPath, catalogPath, backupPath, true);
        }

        private static bool ImportCatalog(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null;
        }

        public void Dispose()
        {
            try
            {
                if (_replaceAttempted && !_committed) RollBack();
            }
            finally
            {
                if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
                if (File.Exists(TemporaryPath + ".meta")) File.Delete(TemporaryPath + ".meta");
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                if (File.Exists(BackupPath + ".meta")) File.Delete(BackupPath + ".meta");
            }
        }
    }

    /// <summary>Builds a Candidate Prefab from ordered project Primitive Prefabs.</summary>
    /// <remarks>Editor-only, AllocByDesign. Runtime consumes only the saved Prefab.</remarks>
    internal delegate bool VfxRecipeRegistration(VfxSourceCompositionPlanView plan, out string message);

    internal sealed class VfxSourceRecipeAssemblyService
    {
        private const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";

        public bool Compose(string recipeId, string displayName, string outputPrefabPath, IReadOnlyList<string> primitiveIds, out string message)
        {
            string expectedHash = HashFile(CatalogPath);
            string planPath = "Temp/MxFramework/VfxSourceLibrary/Operations/composition-plan.json";
            Directory.CreateDirectory(Path.GetDirectoryName(planPath) ?? "Temp/MxFramework/VfxSourceLibrary/Operations");
            string command = "vfx source plan-compose --recipe " + VfxSourceLibraryEditorService.Quote(recipeId) + " --name " + VfxSourceLibraryEditorService.Quote(displayName) + " --output-prefab " + VfxSourceLibraryEditorService.Quote(outputPrefabPath) + " --expected-hash " + expectedHash + " --primitives " + VfxSourceLibraryEditorService.Quote(string.Join(",", primitiveIds)) + " --output " + VfxSourceLibraryEditorService.Quote(planPath);
            if (!VfxSourceLibraryEditorService.RunCli(command, out message) || !File.Exists(planPath)) return false;
            VfxSourceCompositionPlanView plan = JsonConvert.DeserializeObject<VfxSourceCompositionPlanView>(File.ReadAllText(planPath));
            if (plan == null || plan.Blocked || !string.Equals(plan.ExpectedCatalogHash, HashFile(CatalogPath), StringComparison.OrdinalIgnoreCase))
            { message = plan == null ? "组合计划无法读取。" : string.Join("\n", plan.Diagnostics); return false; }
            JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            Dictionary<string, string> paths = ((JArray)catalog["primitives"]).OfType<JObject>().ToDictionary(value => value.Value<string>("primitiveId") ?? string.Empty, value => value["assetRef"]?.Value<string>("path") ?? string.Empty, StringComparer.Ordinal);
            return ExecutePlan(plan, paths, out message);
        }

        internal bool ExecutePlan(
            VfxSourceCompositionPlanView plan,
            IReadOnlyDictionary<string, string> primitivePaths,
            out string message,
            VfxRecipeRegistration registration = null)
        {
            message = string.Empty;
            string outputPrefabPath = plan?.OutputPrefabPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(outputPrefabPath))
            {
                message = "Recipe Candidate output Prefab path is required.";
                return false;
            }
            if (AssetDatabase.LoadMainAssetAtPath(outputPrefabPath) != null || File.Exists(outputPrefabPath))
            {
                message = "Recipe Candidate target already exists; overwrite is blocked: " + outputPrefabPath;
                return false;
            }

            var assetTransaction = new VfxExtractionAssetTransaction(outputPrefabPath, ownsOutputPrefab: false);
            var root = new GameObject(string.IsNullOrWhiteSpace(plan.DisplayName) ? plan.RecipeId : plan.DisplayName);
            bool completed = false;
            try
            {
                assetTransaction.EnsureFolder(Path.GetDirectoryName(outputPrefabPath)?.Replace('\\', '/') ?? "Assets/VFX/MxFramework/Review/Piloto/Recipes");
                using var prefabTransaction = new VfxCandidatePrefabAssetTransaction(outputPrefabPath);
                foreach (VfxSourceCompositionLayerView layer in plan.Layers.OrderBy(value => value.Order))
                {
                    if (!primitivePaths.TryGetValue(layer.PrimitiveId, out string path)) { message = "组合计划引用不存在的 Primitive：" + layer.PrimitiveId; return false; }
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) { message = "无法加载 Primitive Prefab：" + path; return false; }
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.name = layer.LayerId;
                    instance.transform.SetParent(root.transform, false);
                }
                if (!prefabTransaction.Save(root, out message)) return false;
                if (!prefabTransaction.Commit(out message)) return false;
                if (!(registration ?? VfxSourceCatalogGovernance.RegisterRecipe)(plan, out message)) return false;
                prefabTransaction.Complete();
                assetTransaction.Complete();
                completed = true;
                message = "Recipe Candidate 已烘焙并登记为 ReviewNeeded；Runtime 只加载该 Prefab。";
                try
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                catch (Exception exception)
                {
                    message += " Post-commit AssetDatabase refresh warning: " + exception.Message;
                }
                return true;
            }
            catch (Exception exception)
            {
                message = "Recipe Candidate transaction failed: " + exception.Message;
                return false;
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (!completed && !assetTransaction.TryRollback(out string cleanupError))
                    message = (message ?? string.Empty) + " Cleanup was incomplete: " + cleanupError;
            }
        }

        private static string HashFile(string path)
        {
            using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }
}
