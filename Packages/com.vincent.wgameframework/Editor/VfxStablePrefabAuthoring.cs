using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>AllocByDesign identity resolver for low-frequency VFX authoring operations.</summary>
    internal static class VfxStableResourceIdentityService
    {
        internal const string StablePrefabRoot = "Assets/VFX/MxFramework/Published";
        internal const string DefaultPackageId = "mxframework.samples";
        private const string AuthoringCatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";

        internal static void Ensure(VfxCompositionDraftView draft, bool forceReallocate = false)
        {
            if (draft == null)
                return;

            draft.ResourceIdentity ??= new VfxCompositionStableResourceView();
            if (!forceReallocate && IsValid(draft.ResourceIdentity))
            {
                NormalizeCompatibility(draft.ResourceIdentity);
                return;
            }

            JObject matched = forceReallocate ? null : FindExistingEffect(draft);
            JObject sourceKey = matched?["resourceKey"] as JObject;
            string effectId = matched?.Value<string>("effectId");
            if (string.IsNullOrWhiteSpace(effectId))
                effectId = AllocateEffectId(draft.DraftId);

            var key = new VfxPublishedResourceKey
            {
                Id = sourceKey?.Value<string>("id") ?? effectId,
                TypeId = sourceKey?.Value<string>("typeId") ?? ResourceTypeIds.GameObject,
                Variant = sourceKey?.Value<string>("variant") ?? string.Empty,
                PackageId = sourceKey?.Value<string>("packageId") ?? DefaultPackageId
            };
            string existingPath = (matched?["published"] as JObject)?.Value<string>("assetPath") ?? string.Empty;
            if (!IsProjectPrefabPath(existingPath))
            {
                ResourceKey resolvedKey = key.ToResourceKey();
                VfxPublishedBindingRecord binding = VfxPublishedBindingEditorService.LoadManifestUnchecked()?
                    .Bindings.FirstOrDefault(value => value != null && value.ResourceKey.ToResourceKey() == resolvedKey);
                string bindingPath = binding?.AssetPath ?? string.Empty;
                if (bindingPath.StartsWith(StablePrefabRoot + "/", StringComparison.OrdinalIgnoreCase)
                    || bindingPath.StartsWith("Assets/VFX/MxFramework/Review/Authoring/", StringComparison.OrdinalIgnoreCase))
                    existingPath = bindingPath;
            }
            draft.ResourceIdentity = new VfxCompositionStableResourceView
            {
                EffectId = effectId,
                ResourceKey = key,
                StablePrefabPath = IsProjectPrefabPath(existingPath)
                    ? existingPath
                    : StablePrefabRoot + "/" + AssetToken(key.PackageId + "_" + key.Id) + ".prefab",
                Role = (matched?["roles"] as JArray)?.Values<string>().FirstOrDefault() ?? "Impact",
                Anchor = (matched?["anchorContract"] as JObject)?.Value<string>("anchor") ?? "HitPoint",
                Space = (matched?["anchorContract"] as JObject)?.Value<string>("space") ?? "World"
            };
        }

        internal static bool IsValid(VfxCompositionStableResourceView identity)
        {
            return identity != null
                && !string.IsNullOrWhiteSpace(identity.EffectId)
                && identity.ResourceKey != null
                && identity.ResourceKey.ToResourceKey().IsValid
                && IsProjectPrefabPath(identity.StablePrefabPath);
        }

        private static void NormalizeCompatibility(VfxCompositionStableResourceView identity)
        {
            if (string.IsNullOrWhiteSpace(identity.Role)) identity.Role = "Impact";
            if (string.IsNullOrWhiteSpace(identity.Anchor)) identity.Anchor = "HitPoint";
            if (string.IsNullOrWhiteSpace(identity.Space)) identity.Space = "World";
        }

        internal static bool ValidateExclusive(VfxCompositionDraftView draft, out string message)
        {
            message = string.Empty;
            if (draft == null || !IsValid(draft.ResourceIdentity))
            {
                message = "草稿缺少有效的稳定 VFX 身份。";
                return false;
            }
            ResourceKey key = draft.ResourceIdentity.ResourceKey.ToResourceKey();
            foreach (string path in new VfxCompositionDraftService().ListDrafts())
            {
                VfxCompositionDraftView other;
                try
                {
                    other = JsonConvert.DeserializeObject<VfxCompositionDraftView>(File.ReadAllText(path));
                }
                catch (Exception exception) when (exception is IOException || exception is JsonException)
                {
                    continue;
                }
                if (other == null || string.Equals(other.DraftId, draft.DraftId, StringComparison.Ordinal)
                    || !IsValid(other.ResourceIdentity))
                    continue;
                if (other.ResourceIdentity.ResourceKey.ToResourceKey() == key
                    || string.Equals(other.ResourceIdentity.StablePrefabPath, draft.ResourceIdentity.StablePrefabPath, StringComparison.OrdinalIgnoreCase))
                {
                    message = "稳定 ResourceKey 或 Prefab 路径已由另一草稿占用：" + other.DraftId;
                    return false;
                }
            }
            VfxPublishedBindingManifest manifest = VfxPublishedBindingEditorService.LoadManifestUnchecked();
            if (TryGetPublishedPathConflict(
                    manifest,
                    key,
                    draft.ResourceIdentity.StablePrefabPath,
                    out string publishedPath))
            {
                message = "稳定 ResourceKey 已绑定到另一 Prefab，不能改绑：" + publishedPath;
                return false;
            }
            VfxPublishedBindingRecord pathOwner = manifest?
                .Bindings.FirstOrDefault(value => value != null
                    && string.Equals(value.AssetPath, draft.ResourceIdentity.StablePrefabPath, StringComparison.OrdinalIgnoreCase)
                    && value.ResourceKey.ToResourceKey() != key);
            if (pathOwner != null)
            {
                message = "稳定 Prefab 路径已绑定到其它 ResourceKey：" + pathOwner.ResourceKey.ToResourceKey();
                return false;
            }
            return true;
        }

        internal static bool TryGetPublishedPathConflict(
            VfxPublishedBindingManifest manifest,
            ResourceKey key,
            string requestedPath,
            out string publishedPath)
        {
            VfxPublishedBindingRecord existing = manifest?.Bindings.FirstOrDefault(value =>
                value != null && value.ResourceKey.ToResourceKey() == key);
            publishedPath = existing?.AssetPath ?? string.Empty;
            return existing != null
                && !string.Equals(publishedPath, requestedPath, StringComparison.OrdinalIgnoreCase);
        }

        private static JObject FindExistingEffect(VfxCompositionDraftView draft)
        {
            if (!File.Exists(AuthoringCatalogPath))
                return null;
            try
            {
                JObject catalog = JObject.Parse(File.ReadAllText(AuthoringCatalogPath));
                List<JObject> effects = (catalog["effects"] as JArray)?.OfType<JObject>().ToList()
                    ?? new List<JObject>();
                string compositionHash = VfxCompositionDraftService.HashCompositionContent(draft);
                List<JObject> byHash = effects.Where(value => string.Equals(
                    (value["published"] as JObject)?.Value<string>("compositionHash"),
                    compositionHash,
                    StringComparison.OrdinalIgnoreCase)).ToList();
                if (byHash.Count == 1)
                    return byHash[0];

                return effects.FirstOrDefault(value => string.Equals(
                    value.Value<string>("effectId"),
                    draft.TargetEffectId,
                    StringComparison.Ordinal));
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                Debug.LogWarning("VFX 稳定资源身份迁移未能读取 Authoring Catalog：" + exception.Message);
                return null;
            }
        }

        private static string AllocateEffectId(string draftId)
        {
            string value = draftId ?? string.Empty;
            const string prefix = "vfx.composition.";
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                value = value.Substring(prefix.Length);
            value = VfxCompositionDraftService.NormalizeId(value);
            return "vfx.authored." + (string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value);
        }

        private static bool IsProjectPrefabPath(string path) =>
            !string.IsNullOrWhiteSpace(path)
            && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        internal static string AssetToken(string value)
        {
            var builder = new StringBuilder(value?.Length ?? 0);
            string source = value ?? string.Empty;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-'
                    ? character
                    : '_');
            }
            return builder.Length == 0 ? "vfx_authored" : builder.ToString();
        }
    }

    /// <summary>AllocByDesign transaction that promotes a validated bake into its stable Prefab.</summary>
    internal sealed class VfxStablePrefabBakeTransaction : IDisposable
    {
        private readonly VfxCandidatePrefabAssetTransaction _prefabTransaction;
        private readonly string _candidateRoot;
        private readonly string _supportRoot;
        private readonly Dictionary<string, byte[]> _materialBackups =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _createdMaterials = new List<string>();
        private bool _completed;

        internal VfxStablePrefabBakeTransaction(string candidatePrefabPath, string stablePrefabPath)
        {
            CandidatePrefabPath = candidatePrefabPath;
            StablePrefabPath = stablePrefabPath;
            _candidateRoot = (Path.GetDirectoryName(candidatePrefabPath) ?? string.Empty).Replace('\\', '/').TrimEnd('/') + "/";
            string stableFolder = (Path.GetDirectoryName(stablePrefabPath) ?? VfxStableResourceIdentityService.StablePrefabRoot)
                .Replace('\\', '/').TrimEnd('/');
            _supportRoot = stableFolder + "/" + Path.GetFileNameWithoutExtension(stablePrefabPath) + "_Support";
            VfxCompositionDraftService.EnsureFolder(stableFolder);
            _prefabTransaction = new VfxCandidatePrefabAssetTransaction(stablePrefabPath);
        }

        internal string CandidatePrefabPath { get; }
        internal string StablePrefabPath { get; }

        internal bool Commit(out string message)
        {
            GameObject contents = null;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePrefabPath) == null)
                {
                    message = "烘焙暂存 Prefab 无法加载：" + CandidatePrefabPath;
                    return false;
                }

                contents = PrefabUtility.LoadPrefabContents(CandidatePrefabPath);
                PromoteDerivedMaterials(contents);
                if (!_prefabTransaction.Save(contents, out message)) return false;
                if (!_prefabTransaction.Commit(out message)) return false;
                message = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is InvalidOperationException)
            {
                message = "稳定 Prefab 更新失败：" + exception.Message;
                return false;
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        internal void Complete()
        {
            _prefabTransaction.Complete();
            _completed = true;
            _materialBackups.Clear();
            _createdMaterials.Clear();
        }

        private void PromoteDerivedMaterials(GameObject contents)
        {
            Renderer[] renderers = contents.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    if (source == null) continue;
                    string sourcePath = AssetDatabase.GetAssetPath(source).Replace('\\', '/');
                    if (!sourcePath.StartsWith(_candidateRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    VfxCompositionDraftService.EnsureFolder(_supportRoot);
                    string targetPath = _supportRoot + "/" + Path.GetFileName(sourcePath);
                    Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                    if (target == null)
                    {
                        target = new Material(source) { name = source.name };
                        AssetDatabase.CreateAsset(target, targetPath);
                        _createdMaterials.Add(targetPath);
                    }
                    else
                    {
                        string absolutePath = ProjectAbsolutePath(targetPath);
                        if (!_materialBackups.ContainsKey(targetPath))
                            _materialBackups.Add(targetPath, File.ReadAllBytes(absolutePath));
                        EditorUtility.CopySerialized(source, target);
                        EditorUtility.SetDirty(target);
                    }
                    materials[materialIndex] = target;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            AssetDatabase.SaveAssets();
        }

        private static string ProjectAbsolutePath(string assetPath) => Path.GetFullPath(Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
            assetPath));

        public void Dispose()
        {
            try
            {
                _prefabTransaction.Dispose();
            }
            finally
            {
                if (!_completed)
                {
                    for (int i = _createdMaterials.Count - 1; i >= 0; i--)
                        AssetDatabase.DeleteAsset(_createdMaterials[i]);
                    foreach (KeyValuePair<string, byte[]> backup in _materialBackups)
                    {
                        File.WriteAllBytes(ProjectAbsolutePath(backup.Key), backup.Value);
                        AssetDatabase.ImportAsset(backup.Key, ImportAssetOptions.ForceSynchronousImport);
                    }
                }
            }
        }
    }

    /// <summary>AllocByDesign application service for the normal Workbench bake-and-publish path.</summary>
    internal static class VfxStablePrefabBakeService
    {
        internal delegate bool MetadataPublisher(
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt,
            out string message);

        internal static bool Publish(
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt,
            out string message,
            MetadataPublisher metadataPublisher = null)
        {
            if (draft == null || receipt == null || !VfxStableResourceIdentityService.IsValid(draft.ResourceIdentity))
            {
                message = "草稿缺少有效的稳定 VFX 身份。";
                return false;
            }
            if (!VfxStableResourceIdentityService.ValidateExclusive(draft, out message))
                return false;

            metadataPublisher ??= VfxStablePrefabPublicationService.Publish;
            using var transaction = new VfxStablePrefabBakeTransaction(
                receipt.OutputPrefabPath,
                draft.ResourceIdentity.StablePrefabPath);
            if (!transaction.Commit(out message)) return false;
            if (!metadataPublisher(draft, receipt, out message)) return false;
            transaction.Complete();
            VfxPublishedBindingEditorService.ClearPlaytestOverride(
                draft.ResourceIdentity.ResourceKey.ToResourceKey(),
                out _);
            message = "已烘焙并发布 VFX：" + draft.ResourceIdentity.ResourceKey.ToResourceKey()
                + " → " + draft.ResourceIdentity.StablePrefabPath;
            return true;
        }

        internal static bool PublishProduction(
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt,
            out string message)
        {
            bool success = Publish(
                draft,
                receipt,
                out message,
                VfxStablePrefabPublicationService.PublishProduction);
            if (success)
                message = "已烘焙并正式发布 VFX：" + draft.ResourceIdentity.ResourceKey.ToResourceKey()
                    + " → " + draft.ResourceIdentity.StablePrefabPath;
            return success;
        }
    }

    /// <summary>AllocByDesign transactional metadata publisher for a stable VFX bake.</summary>
    internal static class VfxStablePrefabPublicationService
    {
        internal const string AuthoringCatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        internal const string DefinitionCatalogPath = "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json";
        internal const string DefinitionBinaryCatalogPath = "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.bytes";
        internal const string PlayerDefinitionCatalogPath =
            "Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Vfx/vfx_definitions.json";
        internal const string PlayerDefinitionBinaryCatalogPath =
            "Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Vfx/vfx_definitions.bytes";
        internal const string ResourceMappingProfilePath =
            "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";
        internal const string ReadyApprovalRoot = "Assets/MxFrameworkGenerated/Vfx/ReadyApprovals";

        internal static bool Publish(
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt,
            out string message) => Publish(draft, receipt, false, out message);

        internal static bool PublishProduction(
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt,
            out string message) => Publish(draft, receipt, true, out message);

        private static bool Publish(
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt,
            bool publishAsProduction,
            out string message)
        {
            if (draft == null || receipt == null || !VfxStableResourceIdentityService.IsValid(draft.ResourceIdentity))
            {
                message = "草稿缺少有效的稳定 ResourceKey/Prefab 身份。";
                return false;
            }

            var backups = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Backup(backups, VfxPublishedBindingEditorService.ManifestPath);
            Backup(backups, AuthoringCatalogPath);
            Backup(backups, SampleResourceCatalogBuilder.CatalogPath);
            Backup(backups, DefinitionCatalogPath);
            Backup(backups, DefinitionBinaryCatalogPath);
            Backup(backups, PlayerDefinitionCatalogPath);
            Backup(backups, PlayerDefinitionBinaryCatalogPath);
            Backup(backups, ResourceMappingProfilePath);
            string approvalPath = publishAsProduction
                ? ReadyApprovalPath(draft.ResourceIdentity.ResourceKey.ToResourceKey())
                : string.Empty;
            if (publishAsProduction) Backup(backups, approvalPath);
            try
            {
                VfxPublishedBindingManifest manifest = ReadManifestUnchecked();
                JObject authoring = File.Exists(AuthoringCatalogPath)
                    ? JObject.Parse(File.ReadAllText(AuthoringCatalogPath))
                    : CreateAuthoringCatalog();
                JObject definitions = File.Exists(DefinitionCatalogPath)
                    ? JObject.Parse(File.ReadAllText(DefinitionCatalogPath))
                    : CreateDefinitionCatalog();
                ResourceKey key = draft.ResourceIdentity.ResourceKey.ToResourceKey();
                string assetPath = draft.ResourceIdentity.StablePrefabPath;
                string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                string contentHash = VfxCompositionDraftService.HashFile(assetPath);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null || string.IsNullOrWhiteSpace(assetGuid))
                    throw new InvalidDataException("稳定 Prefab 无法加载：" + assetPath);

                VfxReadyApprovalReceipt approval = publishAsProduction
                    ? CreateProductionApproval(draft, receipt, assetPath, assetGuid, contentHash)
                    : null;
                VfxPublishedBindingRecord binding = manifest.Bindings.FirstOrDefault(value =>
                    value != null && value.ResourceKey.ToResourceKey() == key);
                if (binding == null)
                {
                    binding = new VfxPublishedBindingRecord();
                    manifest.Bindings.Add(binding);
                }
                binding.BindingId = "binding." + key.Id;
                binding.EffectId = draft.ResourceIdentity.EffectId;
                binding.ResourceKey = CopyKey(draft.ResourceIdentity.ResourceKey);
                binding.Address = ResolveAddress(binding, key);
                binding.AssetPath = assetPath;
                binding.AssetGuid = assetGuid;
                binding.AssetContentHash = contentHash;
                binding.ContractHash = string.Empty;
                manifest.Bindings.Sort((left, right) => string.Compare(
                    left.ResourceKey.Id,
                    right.ResourceKey.Id,
                    StringComparison.Ordinal));

                JObject effect = UpsertEffect(authoring, draft);
                int defaultLifetimeFrames = Math.Max(
                    1,
                    VfxConsumerCompatibilityValidator.ResolveOneShotActiveFrames(receipt));
                int definitionId = UpsertDefinition(definitions, effect, draft, defaultLifetimeFrames);
                effect["definitionId"] = definitionId;
                effect["published"] = new JObject
                {
                    ["assetPath"] = assetPath,
                    ["assetGuid"] = assetGuid,
                    ["assetContentHash"] = contentHash,
                    ["compositionHash"] = receipt.CompositionContentHash,
                    ["bakeReceiptHash"] = receipt.ReceiptHash
                };
                effect["lifecycle"] = BuildLifecycle(effect["lifecycle"] as JObject, draft, receipt);
                ApplyPublicationState(binding, effect, receipt, approval, approvalPath);

                if (approval != null) WriteJson(approvalPath, JObject.FromObject(approval));
                WriteJson(AuthoringCatalogPath, authoring);
                WriteJson(DefinitionCatalogPath, definitions);
                PublishRuntimeDefinitions();
                // Consumer evidence hashes include the generated VFX definition file hash.
                // Refresh them only after the new definition has reached disk.
                VfxPublishedBindingEditorService.ApplyCurrentConsumerContractEvidence(manifest);
                manifest.ManifestHash = VfxPublishedBindingEditorService.ComputeManifestHash(manifest);
                WriteJson(VfxPublishedBindingEditorService.ManifestPath, JObject.FromObject(manifest));
                PublishResourceMapping();
                if (!SampleResourceCatalogBuilder.TryGenerate(out string catalogMessage))
                    throw new InvalidDataException(catalogMessage);
                if (!VfxPublishedBindingEditorService.GenerateUnityProjection(out string projectionMessage))
                    throw new InvalidOperationException(projectionMessage);
                VfxPublishedBindingEditorService.RefreshRuntimeBindingSource();
                message = "已原位烘焙并更新稳定 VFX：" + key + " → " + assetPath;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is InvalidDataException
                || exception is InvalidOperationException)
            {
                foreach (KeyValuePair<string, byte[]> backup in backups)
                    Restore(backup.Key, backup.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                VfxPublishedBindingEditorService.GenerateUnityProjection(out _);
                VfxPublishedBindingEditorService.RefreshRuntimeBindingSource();
                message = "稳定 VFX 发布失败，已回滚索引：" + exception.Message;
                return false;
            }
        }

        internal static VfxReadyApprovalReceipt ApplyPublicationState(
            VfxPublishedBindingRecord binding,
            JObject effect,
            VfxCompositionBakeReceipt receipt,
            VfxReadyApprovalReceipt approval,
            string approvalPath)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            bool production = approval != null;
            binding.VisualMaturity = production ? "Production" : "Candidate";
            binding.ReviewStatus = production ? "Ready" : "ReviewNeeded";
            binding.ActivationReceiptHash = production ? approval.ReceiptHash : receipt.ReceiptHash;
            effect["visualMaturity"] = binding.VisualMaturity;
            effect["reviewStatus"] = binding.ReviewStatus;
            effect["approval"] = production
                ? new JObject
                {
                    ["kind"] = "HumanVfxApproval",
                    ["receiptPath"] = approvalPath,
                    ["receiptHash"] = approval.ReceiptHash
                }
                : null;
            return approval;
        }

        private static VfxReadyApprovalReceipt CreateProductionApproval(
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt,
            string assetPath,
            string assetGuid,
            string contentHash)
        {
            ResourceKey key = draft.ResourceIdentity.ResourceKey.ToResourceKey();
            var approval = new VfxReadyApprovalReceipt
            {
                EffectId = draft.ResourceIdentity.EffectId,
                ResourceKey = key.ToString(),
                PrefabPath = assetPath,
                PrefabGuid = assetGuid,
                PrefabContentHash = contentHash,
                CompositionHash = receipt.CompositionContentHash,
                CandidateReceiptHash = receipt.ReceiptHash,
                ConsumerContractHash = receipt.ConsumerContractHash,
                ProfileDependencyHash = receipt.ProfileDependencyHash,
                PreviewEvidenceHash = receipt.PreviewEvidenceHash,
                ApprovedBy = Environment.UserName,
                ApprovedUtc = DateTime.UtcNow.ToString("O")
            };
            approval.ReceiptHash = VfxCanonicalJson.Hash(approval, "receiptHash");
            return approval;
        }

        private static string ReadyApprovalPath(ResourceKey key) =>
            ReadyApprovalRoot + "/" + VfxStableResourceIdentityService.AssetToken(key.PackageId + "_" + key.Id) + ".json";

        private static VfxPublishedBindingManifest ReadManifestUnchecked()
        {
            if (!File.Exists(VfxPublishedBindingEditorService.ManifestPath))
                return new VfxPublishedBindingManifest();
            return JsonConvert.DeserializeObject<VfxPublishedBindingManifest>(
                       File.ReadAllText(VfxPublishedBindingEditorService.ManifestPath))
                   ?? new VfxPublishedBindingManifest();
        }

        private static JObject CreateAuthoringCatalog() => new JObject
        {
            ["schemaVersion"] = "mx.vfx.authoring.v2",
            ["catalogId"] = "mxframework.vfx.authoring",
            ["taxonomyVersion"] = "mx.vfx.taxonomy.v1",
            ["strictCoverage"] = false,
            ["primitives"] = new JArray(),
            ["recipes"] = new JArray(),
            ["effects"] = new JArray()
        };

        private static JObject CreateDefinitionCatalog() => new JObject
        {
            ["schemaVersion"] = "1",
            ["packageId"] = "mxframework.vfx.global",
            ["definitions"] = new JArray()
        };

        private static int UpsertDefinition(
            JObject catalog,
            JObject effect,
            VfxCompositionDraftView draft,
            int defaultLifetimeFrames)
        {
            JArray definitions = catalog["definitions"] as JArray;
            if (definitions == null)
            {
                definitions = new JArray();
                catalog["definitions"] = definitions;
            }

            ResourceKey key = draft.ResourceIdentity.ResourceKey.ToResourceKey();
            JObject definition = definitions.OfType<JObject>().FirstOrDefault(value =>
                SameKey(value["prefabResource"] as JObject, key));
            int preferredId = effect.Value<int?>("definitionId") ?? 0;
            if (definition == null && preferredId > 0)
            {
                definition = definitions.OfType<JObject>().FirstOrDefault(value =>
                    value.Value<int>("definitionId") == preferredId);
                if (definition != null && !SameKey(definition["prefabResource"] as JObject, key))
                    definition = null;
            }
            if (definition == null)
            {
                definition = new JObject();
                definitions.Add(definition);
            }

            int definitionId = definition.Value<int?>("definitionId") ?? 0;
            if (definitionId <= 0)
                definitionId = AllocateDefinitionId(definitions, preferredId);
            definition["definitionId"] = definitionId;
            definition["stableId"] = draft.ResourceIdentity.EffectId;
            definition["prefabResource"] = JObject.FromObject(draft.ResourceIdentity.ResourceKey);
            definition["defaultLifetimeFrames"] = defaultLifetimeFrames;
            definition["definitionHash"] = draft.ResourceIdentity.EffectId + ".authoring." + defaultLifetimeFrames;

            var ordered = definitions.OfType<JObject>()
                .OrderBy(value => value.Value<int>("definitionId"))
                .Cast<JToken>()
                .ToArray();
            definitions.Replace(new JArray(ordered));
            return definitionId;
        }

        private static int AllocateDefinitionId(JArray definitions, int preferredId)
        {
            var used = new HashSet<int>(definitions.OfType<JObject>()
                .Select(value => value.Value<int>("definitionId"))
                .Where(value => value > 0));
            if (preferredId > 0 && !used.Contains(preferredId))
                return preferredId;
            int candidate = Math.Max(20000, used.Count == 0 ? 20000 : used.Max() + 1);
            while (used.Contains(candidate))
                candidate++;
            return candidate;
        }

        private static bool SameKey(JObject value, ResourceKey key) =>
            value != null
            && string.Equals(value.Value<string>("id"), key.Id, StringComparison.Ordinal)
            && string.Equals(value.Value<string>("typeId") ?? string.Empty, key.TypeId, StringComparison.Ordinal)
            && string.Equals(value.Value<string>("variant") ?? string.Empty, key.Variant, StringComparison.Ordinal)
            && string.Equals(value.Value<string>("packageId") ?? string.Empty, key.PackageId, StringComparison.Ordinal);

        private static JObject UpsertEffect(JObject authoring, VfxCompositionDraftView draft)
        {
            JArray effects = authoring["effects"] as JArray;
            if (effects == null)
            {
                effects = new JArray();
                authoring["effects"] = effects;
            }
            ResourceKey key = draft.ResourceIdentity.ResourceKey.ToResourceKey();
            JObject effect = effects.OfType<JObject>().FirstOrDefault(value => string.Equals(
                value["resourceKey"]?.Value<string>("id"), key.Id, StringComparison.Ordinal));
            if (effect == null)
            {
                effect = new JObject
                {
                    ["effectId"] = draft.ResourceIdentity.EffectId,
                    ["displayName"] = draft.DisplayName,
                    ["description"] = draft.Brief,
                    ["resourceKey"] = JObject.FromObject(draft.ResourceIdentity.ResourceKey),
                    ["backend"] = "ParticleSystem",
                    ["themeIds"] = new JArray("generic"),
                    ["tags"] = new JArray("authored")
                };
                effects.Add(effect);
            }
            UpdateCompatibility(effect, draft);
            return effect;
        }

        private static void UpdateCompatibility(JObject effect, VfxCompositionDraftView draft)
        {
            effect["roles"] = new JArray(draft.ResourceIdentity.Role);
            effect["anchorContract"] = new JObject
            {
                ["anchor"] = draft.ResourceIdentity.Anchor,
                ["space"] = draft.ResourceIdentity.Space
            };
        }

        private static JObject BuildLifecycle(
            JObject existing,
            VfxCompositionDraftView draft,
            VfxCompositionBakeReceipt receipt)
        {
            JObject lifecycle = existing != null ? (JObject)existing.DeepClone() : new JObject();
            lifecycle["mode"] = draft.LoopPlaybackMode == "Continuous" ? "Loop" : "OneShot";
            lifecycle["defaultLifetimeFrames"] = Math.Max(
                1,
                VfxConsumerCompatibilityValidator.ResolveOneShotActiveFrames(receipt));
            lifecycle["stopPolicy"] = lifecycle.Value<string>("stopPolicy") ?? "Expire";
            return lifecycle;
        }

        private static VfxPublishedResourceKey CopyKey(VfxPublishedResourceKey source) =>
            new VfxPublishedResourceKey
            {
                Id = source.Id,
                TypeId = source.TypeId,
                Variant = source.Variant,
                PackageId = source.PackageId
            };

        internal static string ResolveAddress(VfxPublishedBindingRecord binding, ResourceKey key) =>
            !string.IsNullOrWhiteSpace(binding?.Address)
                ? binding.Address
                : key.PackageId + "/vfx/" + VfxStableResourceIdentityService.AssetToken(key.Id);

        private static void WriteJson(string path, JToken value)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                value.ToString(Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        internal static void PublishRuntimeDefinitions()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root is unavailable.");
            string cliProject = Path.Combine(
                projectRoot,
                "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj");
            string stageRoot = Path.Combine(
                projectRoot,
                "Temp/MxFramework/VfxStableRuntimePublish/" + Guid.NewGuid().ToString("N"));
            string abilityRoot = Path.Combine(stageRoot, "Abilities");
            string vfxRoot = Path.Combine(stageRoot, "Vfx");
            string projectileRoot = Path.Combine(stageRoot, "Projectiles");
            try
            {
                Directory.CreateDirectory(vfxRoot);
                File.Copy(
                    ProjectAbsolutePath(projectRoot, DefinitionCatalogPath),
                    Path.Combine(vfxRoot, "vfx_definitions.json"),
                    true);
                string arguments = "run --project " + Quote(cliProject)
                    + " -- ability export-runtime-binaries --project-root " + Quote(projectRoot)
                    + " --ability-root " + Quote(ProjectRelativePath(projectRoot, abilityRoot))
                    + " --vfx-root " + Quote(ProjectRelativePath(projectRoot, vfxRoot))
                    + " --projectile-root " + Quote(ProjectRelativePath(projectRoot, projectileRoot));
                using var process = new global::System.Diagnostics.Process
                {
                    StartInfo = new global::System.Diagnostics.ProcessStartInfo("dotnet", arguments)
                    {
                        WorkingDirectory = projectRoot,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        "VFX runtime binary export failed: "
                        + (string.IsNullOrWhiteSpace(error) ? output : error).Trim());

                string stagedBinary = Path.Combine(vfxRoot, "vfx_definitions.bytes");
                if (!File.Exists(stagedBinary))
                    throw new InvalidDataException("VFX runtime binary exporter did not produce vfx_definitions.bytes.");
                CopyFile(stagedBinary, ProjectAbsolutePath(projectRoot, DefinitionBinaryCatalogPath));
                CopyFile(
                    ProjectAbsolutePath(projectRoot, DefinitionCatalogPath),
                    ProjectAbsolutePath(projectRoot, PlayerDefinitionCatalogPath));
                CopyFile(stagedBinary, ProjectAbsolutePath(projectRoot, PlayerDefinitionBinaryCatalogPath));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, true);
            }
        }

        internal static void PublishResourceMapping()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root is unavailable.");
            string cliProject = Path.Combine(
                projectRoot,
                "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj");
            string arguments = "run --project " + Quote(cliProject)
                + " -- resource-mapping sync-first-party --root " + Quote(projectRoot)
                + " --producer vfx.global-profile"
                + " --build-target " + Quote(EditorUserBuildSettings.activeBuildTarget.ToString());
            using var process = new global::System.Diagnostics.Process
            {
                StartInfo = new global::System.Diagnostics.ProcessStartInfo("dotnet", arguments)
                {
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "VFX Resource Mapping publish failed: "
                    + (string.IsNullOrWhiteSpace(error) ? output : error).Trim());
            }

            AssetDatabase.ImportAsset(
                ResourceMappingProfilePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static string ProjectAbsolutePath(string projectRoot, string projectRelativePath) =>
            Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath ?? string.Empty));

        private static string ProjectRelativePath(string projectRoot, string absolutePath) =>
            Path.GetRelativePath(projectRoot, absolutePath).Replace('\\', '/');

        private static void CopyFile(string sourcePath, string targetPath)
        {
            string directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.Copy(sourcePath, targetPath, true);
        }

        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static void Backup(IDictionary<string, byte[]> backups, string path) =>
            backups[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;

        private static void Restore(string path, byte[] contents)
        {
            if (contents == null)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, contents);
        }
    }
}
