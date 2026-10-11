using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class VfxPublishedBindingEditorService
    {
        internal const string ManifestPath = "Assets/Config/MxFramework/Vfx/vfx_published_bindings.json";
        internal const string ProjectionPath = "Assets/MxFrameworkGenerated/Vfx/vfx_published_unity_bindings.asset";
        internal const string PlaytestOverridePath = "Library/MxFramework/VfxPlaytestOverrides.json";

        [MenuItem("MxFramework/VFX/Published Bindings/Regenerate Unity Projection", priority = 148)]
        internal static void RegenerateProjectionMenu()
        {
            bool success = GenerateUnityProjection(out string message);
            if (success)
                Debug.Log(message);
            else
                Debug.LogError(message);
        }

        [MenuItem("MxFramework/VFX/Published Bindings/Refresh Consumer Contract Evidence", priority = 147)]
        internal static void RefreshConsumerContractEvidenceMenu()
        {
            bool success = RefreshConsumerContractEvidence(out string message);
            if (success)
                Debug.Log(message);
            else
                Debug.LogError(message);
        }

        internal static VfxPublishedBindingManifest LoadManifest(out string message)
        {
            if (!File.Exists(ManifestPath))
            {
                message = "Published VFX binding manifest is missing: " + ManifestPath;
                return null;
            }

            try
            {
                VfxPublishedBindingManifest manifest =
                    JsonConvert.DeserializeObject<VfxPublishedBindingManifest>(File.ReadAllText(ManifestPath));
                if (!ValidateManifest(manifest, out message))
                    return null;
                return manifest;
            }
            catch (Exception exception)
            {
                message = "Published VFX binding manifest is invalid: " + exception.Message;
                return null;
            }
        }

        internal static VfxPublishedBindingManifest LoadManifestUnchecked()
        {
            if (!File.Exists(ManifestPath))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<VfxPublishedBindingManifest>(File.ReadAllText(ManifestPath));
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                return null;
            }
        }

        internal static bool ValidateManifest(VfxPublishedBindingManifest manifest, out string message)
        {
            if (manifest == null
                || !string.Equals(manifest.SchemaVersion, "mx.vfx.published-bindings.v1", StringComparison.Ordinal))
            {
                message = "Published VFX binding schema is unsupported.";
                return false;
            }

            var keys = new HashSet<ResourceKey>();
            var bindingIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.Bindings.Count; i++)
            {
                VfxPublishedBindingRecord binding = manifest.Bindings[i];
                ResourceKey key = binding?.ResourceKey?.ToResourceKey() ?? default;
                if (binding == null
                    || string.IsNullOrWhiteSpace(binding.BindingId)
                    || !key.IsValid
                    || !string.Equals(key.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(binding.Address)
                    || string.IsNullOrWhiteSpace(binding.AssetPath))
                {
                    message = "Published VFX binding " + i + " is incomplete.";
                    return false;
                }

                if (!keys.Add(key) || !bindingIds.Add(binding.BindingId))
                {
                    message = "Published VFX binding contains a duplicate ResourceKey or bindingId: " + binding.BindingId;
                    return false;
                }

                string actualGuid = AssetDatabase.AssetPathToGUID(binding.AssetPath);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(binding.AssetPath) == null
                    || (!string.IsNullOrWhiteSpace(binding.AssetGuid)
                        && !string.Equals(binding.AssetGuid, actualGuid, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(binding.AssetContentHash)
                        && !string.Equals(
                            binding.AssetContentHash,
                            VfxCompositionDraftService.HashFile(binding.AssetPath),
                            StringComparison.OrdinalIgnoreCase)))
                {
                    message = "Published VFX binding asset evidence is stale: " + binding.BindingId;
                    return false;
                }
            }

            for (int i = 0; i < manifest.BindingSets.Count; i++)
            {
                VfxPublishedBindingSet set = manifest.BindingSets[i];
                if (set == null || string.IsNullOrWhiteSpace(set.BindingSetId) || set.Members.Count == 0)
                {
                    message = "Published VFX binding set is incomplete.";
                    return false;
                }

                for (int memberIndex = 0; memberIndex < set.Members.Count; memberIndex++)
                {
                    if (!bindingIds.Contains(set.Members[memberIndex].BindingId))
                    {
                        message = "Published VFX binding set references an unknown binding: "
                            + set.Members[memberIndex].BindingId;
                        return false;
                    }
                }
            }

            if (!VfxConsumerContractResolver.TryResolveFireballBurst(
                    out IReadOnlyList<VfxConsumerContract> currentContracts, out message)
                || !VfxConsumerContractResolver.ValidateEvidence(manifest, currentContracts, out message))
                return false;

            string expectedHash = ComputeManifestHash(manifest);
            if (!string.IsNullOrWhiteSpace(manifest.ManifestHash)
                && !string.Equals(manifest.ManifestHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                message = "Published VFX binding manifest hash is stale.";
                return false;
            }

            message = "Published VFX bindings valid: " + manifest.Bindings.Count + ".";
            return true;
        }

        internal static string ComputeManifestHash(VfxPublishedBindingManifest manifest)
        {
            return VfxCanonicalJson.Hash(manifest, "manifestHash");
        }

        internal static void ApplyCurrentConsumerContractEvidence(VfxPublishedBindingManifest manifest)
        {
            if (manifest == null)
                return;

            IReadOnlyList<VfxConsumerContract> contracts =
                VfxConsumerContractResolver.ResolveFireballBurst();
            for (int i = 0; i < contracts.Count; i++)
            {
                VfxConsumerContract contract = contracts[i];
                VfxPublishedBindingRecord binding = manifest.Bindings.FirstOrDefault(
                    value => value?.ResourceKey?.ToResourceKey() == contract.ResourceKey);
                if (binding != null)
                    binding.ContractHash = contract.ContractHash;
            }
        }

        internal static bool RefreshConsumerContractEvidence(out string message)
        {
            VfxPublishedBindingManifest manifest = LoadManifestUnchecked();
            if (manifest == null)
            {
                message = "Published VFX binding manifest is missing or invalid: " + ManifestPath;
                return false;
            }

            if (!VfxConsumerContractResolver.TryResolveFireballBurst(out _, out message))
                return false;
            ApplyCurrentConsumerContractEvidence(manifest);
            manifest.ManifestHash = ComputeManifestHash(manifest);
            if (!ValidateManifest(manifest, out message))
                return false;

            File.WriteAllText(
                ManifestPath,
                JsonConvert.SerializeObject(manifest, Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceSynchronousImport);

            if (!SampleResourceCatalogBuilder.TryGenerate(out string catalogMessage))
            {
                message = "Consumer contract evidence refreshed, but Resource Catalog generation failed: "
                    + catalogMessage.Replace('\n', ' ');
                return false;
            }
            if (!GenerateUnityProjection(out string projectionMessage))
            {
                message = "Consumer contract evidence and Resource Catalog refreshed, but Unity projection failed: "
                    + projectionMessage;
                return false;
            }

            message = "Published VFX consumer contract evidence, Resource Catalog, and Unity projection refreshed.";
            return true;
        }

        internal static bool GenerateUnityProjection(out string message)
        {
            try
            {
                return GenerateUnityProjectionCore(out message);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is InvalidDataException
                || exception is InvalidOperationException
                || exception is AggregateException)
            {
                message = "Published VFX Unity projection failed: " + exception.Message;
                return false;
            }
        }

        private static bool GenerateUnityProjectionCore(out string message)
        {
            VfxPublishedBindingManifest manifest = LoadManifest(out message);
            if (manifest == null)
                return false;

            var unityBindings = new List<VfxUnityPrefabBinding>(manifest.Bindings.Count);
            for (int i = 0; i < manifest.Bindings.Count; i++)
            {
                VfxPublishedBindingRecord source = manifest.Bindings[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.AssetPath);
                if (prefab == null)
                {
                    message = "Published VFX prefab is missing: " + source.AssetPath;
                    return false;
                }

                string actualGuid = AssetDatabase.AssetPathToGUID(source.AssetPath);
                if (!string.IsNullOrWhiteSpace(source.AssetGuid)
                    && !string.Equals(source.AssetGuid, actualGuid, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Published VFX prefab GUID is stale: " + source.AssetPath;
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(source.AssetContentHash)
                    && !string.Equals(
                        source.AssetContentHash,
                        VfxCompositionDraftService.HashFile(source.AssetPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    message = "Published VFX prefab content hash is stale: " + source.AssetPath;
                    return false;
                }

                var binding = new VfxUnityPrefabBinding();
                binding.Configure(source.ResourceKey.ToResourceKey(), source.Address, prefab);
                unityBindings.Add(binding);
            }

            UnityEngine.Object profile =
                AssetDatabase.LoadMainAssetAtPath(
                    "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset");
            using var transaction = new VfxUnityProjectionPublishTransaction(
                ProjectionPath,
                profile);
            if (!transaction.Publish(ComputeManifestHash(manifest), unityBindings, out message))
                return false;
            message = "Published VFX Unity projection generated: " + unityBindings.Count + " bindings.";
            return true;
        }

        internal static bool SetPlaytestOverride(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt,
            out string message)
        {
            return SetPlaytestOverride(contract, receipt, false, out message);
        }

        internal static bool SetDraftPlaytestOverride(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt,
            out string message)
        {
            return SetPlaytestOverride(contract, receipt, true, out message);
        }

        private static bool SetPlaytestOverride(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt,
            bool allowContractWarning,
            out string message)
        {
            VfxPlaytestOverrideDocument document = LoadOverrides();
            if (!TryCreateOverrideRecord(contract, receipt, allowContractWarning, out VfxPlaytestOverrideRecord record, out message))
                return false;

            document.Entries.RemoveAll(value => value.ResourceKey.ToResourceKey() == contract.ResourceKey);
            document.Entries.Add(record);
            if (!PublishOverridesAndRefresh(document, out message))
                return false;
            message = "已启用本机试玩覆盖：" + contract.SlotLabel + " → " + receipt.OutputPrefabPath;
            return true;
        }

        internal static bool SetFireballBurstPlaytestBundle(
            IReadOnlyList<VfxConsumerContract> contracts,
            IReadOnlyList<VfxCompositionBakeReceipt> receipts,
            out string message)
        {
            if (contracts == null || receipts == null || contracts.Count != 3 || receipts.Count != 3)
            {
                message = "Fireball Burst 三段试玩套装必须同时提供 Charge、Flight、Impact。";
                return false;
            }

            var records = new List<VfxPlaytestOverrideRecord>(3);
            for (int i = 0; i < contracts.Count; i++)
            {
                if (!TryCreateOverrideRecord(contracts[i], receipts[i], false, out VfxPlaytestOverrideRecord record, out message))
                {
                    message = contracts[i].SlotLabel + " 阻断：" + message;
                    return false;
                }
                records.Add(record);
            }

            VfxPlaytestOverrideDocument document = LoadOverrides();
            for (int i = 0; i < contracts.Count; i++)
            {
                ResourceKey key = contracts[i].ResourceKey;
                document.Entries.RemoveAll(value => value.ResourceKey.ToResourceKey() == key);
            }
            document.Entries.AddRange(records);
            if (!PublishOverridesAndRefresh(document, out message))
                return false;
            message = "已原子启用 Fireball Burst 三段本机试玩套装。";
            return true;
        }

        internal static void ClearPlaytestOverrides()
        {
            if (File.Exists(PlaytestOverridePath))
                File.Delete(PlaytestOverridePath);
            RefreshRuntimeBindingSource();
        }

        internal static bool ClearPlaytestOverride(ResourceKey resourceKey, out string message)
        {
            VfxPlaytestOverrideDocument document = LoadOverrides();
            int removed = document.Entries.RemoveAll(
                value => value != null && value.ResourceKey.ToResourceKey() == resourceKey);
            if (removed == 0)
            {
                message = "当前 VFX 没有启用本机 Candidate。";
                return true;
            }

            if (!PublishOverridesAndRefresh(document, out message))
                return false;
            message = "已恢复正式 VFX 资源：" + resourceKey.Id;
            return true;
        }

        internal static bool TryGetPlaytestOverride(
            ResourceKey resourceKey,
            out VfxPlaytestOverrideRecord record)
        {
            record = LoadOverrides().Entries.FirstOrDefault(
                value => value != null && value.ResourceKey.ToResourceKey() == resourceKey);
            return record != null;
        }

        internal static bool MarkHumanPlaytested(ResourceKey resourceKey, out string message)
        {
            VfxPlaytestOverrideDocument document = LoadOverrides();
            VfxPlaytestOverrideRecord entry =
                document.Entries.FirstOrDefault(value => value.ResourceKey.ToResourceKey() == resourceKey);
            if (entry == null)
            {
                message = "当前槽位没有启用本机试玩覆盖。";
                return false;
            }

            entry.HumanPlaytested = true;
            SaveOverrides(document);
            message = "已记录人工实战检查；该证据不会自动授予 Ready。";
            return true;
        }

        internal static void RefreshRuntimeBindingSource()
        {
            VfxPublishedUnityBindingAsset published =
                AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(ProjectionPath);
            var sources = new List<IVfxUnityPrefabBindingSource>();
            if (published != null)
                sources.Add(published);

            VfxUnityPrefabBindingSourceRegistry.SetCurrent(new VfxCompositeUnityBindingSource(sources));
        }

        /// <summary>Installs the generated Editor binding source after validating its authoritative evidence.</summary>
        /// <remarks>LowFreqAlloc: Editor window restoration and preview-session construction only.</remarks>
        internal static bool TryEnsureRuntimeBindingSource(out string message)
        {
            if (VfxUnityPrefabBindingSourceRegistry.Current != null)
            {
                message = string.Empty;
                return true;
            }

            VfxPublishedBindingManifest manifest = LoadManifest(out message);
            if (manifest == null)
                return false;

            VfxPublishedUnityBindingAsset projection =
                AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(ProjectionPath);
            string manifestHash = ComputeManifestHash(manifest);
            if (projection == null
                || !string.Equals(projection.ManifestHash, manifestHash, StringComparison.Ordinal))
            {
                message = "Published VFX Unity projection is missing or stale.";
                return false;
            }

            RefreshRuntimeBindingSource();
            if (VfxUnityPrefabBindingSourceRegistry.Current == null)
            {
                message = "Published VFX Unity projection could not be installed.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>Resolves one published prefab for an Editor-only authoring preview.</summary>
        /// <remarks>
        /// LowFreqAlloc. Runtime installation still requires the complete manifest to pass validation;
        /// this fallback validates the requested record's stable identity and projection path, but intentionally
        /// allows content-hash drift so an author can preview the current prefab before republishing it.
        /// </remarks>
        internal static bool TryResolveAuthoringPreviewBinding(
            ResourceKey resourceKey,
            out VfxUnityPrefabBinding resolved,
            out string message)
        {
            resolved = null;
            IVfxUnityPrefabBindingSource current = VfxUnityPrefabBindingSourceRegistry.Current;
            if (current != null && current.TryResolve(resourceKey, out resolved) && resolved?.Prefab != null)
            {
                message = string.Empty;
                return true;
            }

            TryEnsureRuntimeBindingSource(out message);
            current = VfxUnityPrefabBindingSourceRegistry.Current;
            if (current != null && current.TryResolve(resourceKey, out resolved) && resolved?.Prefab != null)
            {
                message = string.Empty;
                return true;
            }

            VfxPublishedBindingManifest manifest = LoadManifestUnchecked();
            VfxPublishedUnityBindingAsset projection =
                AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(ProjectionPath);
            if (manifest == null
                || !string.Equals(manifest.SchemaVersion, "mx.vfx.published-bindings.v1", StringComparison.Ordinal)
                || projection == null
                || !string.Equals(
                    projection.ManifestHash,
                    ComputeManifestHash(manifest),
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "Published VFX Unity projection is missing or stale.";
                return false;
            }

            VfxPublishedBindingRecord record = manifest.Bindings.FirstOrDefault(
                value => value?.ResourceKey?.ToResourceKey() == resourceKey);
            if (record == null)
            {
                message = "Published VFX binding is missing: " + resourceKey + ".";
                return false;
            }

            string actualGuid = AssetDatabase.AssetPathToGUID(record.AssetPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(record.AssetPath);
            if (prefab == null
                || (!string.IsNullOrWhiteSpace(record.AssetGuid)
                    && !string.Equals(record.AssetGuid, actualGuid, StringComparison.OrdinalIgnoreCase))
                || !projection.TryResolve(resourceKey, out resolved)
                || resolved?.Prefab == null
                || !string.Equals(
                    AssetDatabase.GetAssetPath(resolved.Prefab),
                    record.AssetPath,
                    StringComparison.Ordinal))
            {
                resolved = null;
                message = "Published VFX binding identity or projection is stale: " + record.BindingId + ".";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static VfxPlaytestOverrideDocument LoadOverrides()
        {
            if (!File.Exists(PlaytestOverridePath))
                return new VfxPlaytestOverrideDocument { ProjectId = Application.dataPath };

            try
            {
                VfxPlaytestOverrideDocument document =
                    JsonConvert.DeserializeObject<VfxPlaytestOverrideDocument>(File.ReadAllText(PlaytestOverridePath));
                if (document == null
                    || (!string.IsNullOrWhiteSpace(document.ProjectId)
                        && !string.Equals(document.ProjectId, Application.dataPath, StringComparison.OrdinalIgnoreCase)))
                {
                    return new VfxPlaytestOverrideDocument { ProjectId = Application.dataPath };
                }
                document.ProjectId = Application.dataPath;
                return document;
            }
            catch
            {
                return new VfxPlaytestOverrideDocument { ProjectId = Application.dataPath };
            }
        }

        private static void SaveOverrides(VfxPlaytestOverrideDocument document)
        {
            document.ProjectId = Application.dataPath;
            string directory = Path.GetDirectoryName(PlaytestOverridePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                PlaytestOverridePath,
                JsonConvert.SerializeObject(document, Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static bool PublishOverridesAndRefresh(
            VfxPlaytestOverrideDocument document,
            out string message)
        {
            try
            {
                using var transaction = new VfxPlaytestOverridePublishTransaction(
                    PlaytestOverridePath);
                return transaction.Publish(document, out message);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is InvalidDataException
                || exception is InvalidOperationException
                || exception is AggregateException)
            {
                message = "Playtest override publish failed: " + exception.Message;
                return false;
            }
        }

        internal static bool TryReadAndValidateReceipt(
            string receiptPath,
            string expectedReceiptHash,
            out VfxCompositionBakeReceipt receipt)
        {
            receipt = null;
            if (string.IsNullOrWhiteSpace(receiptPath) || !File.Exists(receiptPath))
                return false;

            try
            {
                receipt = JsonConvert.DeserializeObject<VfxCompositionBakeReceipt>(
                    File.ReadAllText(receiptPath));
                return receipt != null
                    && string.Equals(receipt.ReceiptHash, expectedReceiptHash, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        receipt.ReceiptHash,
                        VfxCanonicalJson.Hash(receipt, "receiptHash"),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                receipt = null;
                return false;
            }
        }

        private static bool TryCreateOverrideRecord(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt,
            bool allowContractWarning,
            out VfxPlaytestOverrideRecord record,
            out string message)
        {
            record = null;
            if (contract == null || receipt == null)
            {
                message = "技能槽位或 Candidate receipt 缺失。";
                return false;
            }

            VfxConsumerCompatibilityReport compatibility = allowContractWarning
                ? VfxConsumerCompatibilityValidator.ValidateCandidateArtifact(receipt)
                : VfxConsumerCompatibilityValidator.Validate(contract, receipt);
            if (!compatibility.Compatible)
            {
                message = compatibility.Summary;
                return false;
            }

            if (!string.Equals(
                    receipt.ConsumerResourceKey,
                    contract.ResourceKey.ToString(),
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.ConsumerContractHash,
                    contract.ContractHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "Candidate 并非按当前 ResourceKey/技能合同烘焙。";
                return false;
            }

            string receiptPath = VfxCompositionCandidateBaker.ReceiptRoot + "/"
                + VfxCompositionDraftService.FileToken(receipt.DraftId) + ".json";
            if (!TryReadAndValidateReceipt(receiptPath, receipt.ReceiptHash, out _))
            {
                message = "Candidate receipt 文件缺失或已变化，请重新烘焙。";
                return false;
            }

            record = new VfxPlaytestOverrideRecord
            {
                ResourceKey = FromResourceKey(contract.ResourceKey),
                CandidateAssetPath = receipt.OutputPrefabPath,
                CandidateAssetGuid = receipt.OutputPrefabGuid,
                CandidateContentHash = receipt.CandidateContentHash,
                CompositionHash = receipt.CompositionContentHash,
                ReceiptPath = receiptPath,
                ReceiptHash = receipt.ReceiptHash,
                ConsumerContractHash = contract.ContractHash
            };
            message = string.Empty;
            return true;
        }

        private static VfxPublishedResourceKey FromResourceKey(ResourceKey key)
        {
            return new VfxPublishedResourceKey
            {
                Id = key.Id,
                TypeId = key.TypeId,
                Variant = key.Variant,
                PackageId = key.PackageId
            };
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    internal sealed class VfxPublishedBindingStore : IVfxPublishedBindingStore
    {
        public VfxPublishedBindingManifest Load(out string message) =>
            VfxPublishedBindingEditorService.LoadManifest(out message);

        public bool Validate(VfxPublishedBindingManifest manifest, out string message) =>
            VfxPublishedBindingEditorService.ValidateManifest(manifest, out message);

        public bool GenerateProjection(VfxPublishedBindingManifest manifest, out string message)
        {
            if (!VfxPublishedBindingEditorService.ValidateManifest(manifest, out message))
                return false;
            return VfxPublishedBindingEditorService.GenerateUnityProjection(out message);
        }
    }

    internal sealed class VfxPlaytestOverrideStore : IVfxPlaytestOverrideStore
    {
        public bool Set(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt,
            out string message) =>
            VfxPublishedBindingEditorService.SetPlaytestOverride(contract, receipt, out message);

        public bool SetFireballBurstBundle(
            IReadOnlyList<VfxConsumerContract> contracts,
            IReadOnlyList<VfxCompositionBakeReceipt> receipts,
            out string message) =>
            VfxPublishedBindingEditorService.SetFireballBurstPlaytestBundle(
                contracts,
                receipts,
                out message);

        public bool MarkHumanPlaytested(ResourceKey resourceKey, out string message) =>
            VfxPublishedBindingEditorService.MarkHumanPlaytested(resourceKey, out message);

        public void Clear() =>
            VfxPublishedBindingEditorService.ClearPlaytestOverrides();
    }
}
