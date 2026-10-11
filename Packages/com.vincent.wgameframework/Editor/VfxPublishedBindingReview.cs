using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxReadyApprovalReceipt
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.ready-approval.v1";
        [JsonProperty("effectId")] public string EffectId = string.Empty;
        [JsonProperty("resourceKey")] public string ResourceKey = string.Empty;
        [JsonProperty("prefabPath")] public string PrefabPath = string.Empty;
        [JsonProperty("prefabGuid")] public string PrefabGuid = string.Empty;
        [JsonProperty("prefabContentHash")] public string PrefabContentHash = string.Empty;
        [JsonProperty("compositionHash")] public string CompositionHash = string.Empty;
        [JsonProperty("candidateReceiptHash")] public string CandidateReceiptHash = string.Empty;
        [JsonProperty("consumerContractHash")] public string ConsumerContractHash = string.Empty;
        [JsonProperty("profileDependencyHash")] public string ProfileDependencyHash = string.Empty;
        [JsonProperty("previewEvidenceHash")] public string PreviewEvidenceHash = string.Empty;
        [JsonProperty("approvedBy")] public string ApprovedBy = string.Empty;
        [JsonProperty("approvedUtc")] public string ApprovedUtc = string.Empty;
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
    }

    internal static class VfxManualCompositionReviewService
    {
        private const string AuthoringCatalogPath =
            "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        private const string ReviewRoot =
            "Assets/VFX/MxFramework/Review/FireballBurst";
        private const string ApprovalRoot =
            "Assets/MxFrameworkGenerated/Vfx/ReadyApprovals";

        internal static bool PromoteToReviewNeeded(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt,
            out string message)
        {
            if (!TryResolveEvidence(contract, receipt, out VfxPlaytestOverrideRecord playtest, out message))
                return false;
            if (!playtest.HumanPlaytested)
            {
                message = "必须先在 CharacterTest 实际释放技能并记录人工实战检查。";
                return false;
            }

            string role = Role(contract.ResourceKey);
            string targetPath = ReviewRoot + "/FireballBurst_" + role + ".prefab";
            if (!string.Equals(receipt.OutputPrefabPath, playtest.CandidateAssetPath, StringComparison.Ordinal)
                || AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null)
            {
                message = "Candidate 路径与试玩证据不一致，或待评审目标已存在。";
                return false;
            }

            string sourcePath = playtest.CandidateAssetPath;
            try
            {
                string sourceContentHash = VfxCompositionDraftService.HashFile(sourcePath);
                JObject authoring = JObject.Parse(File.ReadAllText(AuthoringCatalogPath));
                JObject effect = FindEffect(authoring, contract.ResourceKey.Id)
                    ?? throw new InvalidOperationException("Authoring Catalog 缺少目标 Effect。");
                effect["published"] = new JObject
                {
                    ["assetPath"] = targetPath,
                    ["assetGuid"] = receipt.OutputPrefabGuid,
                    ["assetContentHash"] = sourceContentHash,
                    ["compositionHash"] = receipt.CompositionContentHash,
                    ["candidateReceiptHash"] = receipt.ReceiptHash,
                    ["consumerContractHash"] = contract.ContractHash
                };
                effect["visualMaturity"] = "Candidate";
                effect["reviewStatus"] = "ReviewNeeded";
                effect["approval"] = null;
                VfxPlaytestOverrideDocument overrides = LoadOverrides();
                VfxPlaytestOverrideRecord entry = overrides.Entries.First(
                    value => value.ResourceKey.ToResourceKey() == contract.ResourceKey);
                entry.CandidateAssetPath = targetPath;
                entry.CandidateContentHash = sourceContentHash;
                using var transaction = new VfxReviewNeededPromotionTransaction(
                    sourcePath,
                    targetPath,
                    AuthoringCatalogPath,
                    VfxPublishedBindingEditorService.PlaytestOverridePath);
                if (!transaction.Publish(
                        authoring,
                        overrides,
                        contract.ResourceKey.Id,
                        receipt.OutputPrefabGuid,
                        sourceContentHash,
                        out message))
                    return false;
                message = role + " 已保留 GUID 晋升为 ReviewNeeded；尚未获得 Ready。";
                return true;
            }
            catch (Exception exception)
            {
                message = "晋升失败并已回滚：" + exception.Message;
                return false;
            }
        }

        internal static bool ApproveReady(
            VfxConsumerContract contract,
            out VfxReadyApprovalReceipt approval,
            out string message)
        {
            try
            {
                return ApproveReadyCore(contract, out approval, out message);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException
                || exception is InvalidDataException
                || exception is InvalidOperationException
                || exception is AggregateException)
            {
                approval = null;
                message = "Ready approval publish failed: " + exception.Message;
                return false;
            }
        }

        private static bool ApproveReadyCore(
            VfxConsumerContract contract,
            out VfxReadyApprovalReceipt approval,
            out string message)
        {
            approval = null;
            VfxPlaytestOverrideDocument overrides = LoadOverrides();
            VfxPlaytestOverrideRecord playtest = overrides.Entries.FirstOrDefault(
                value => value.ResourceKey.ToResourceKey() == contract.ResourceKey);
            if (playtest == null
                || !playtest.HumanPlaytested
                || !VfxPublishedBindingEditorService.TryReadAndValidateReceipt(
                    playtest.ReceiptPath,
                    playtest.ReceiptHash,
                    out VfxCompositionBakeReceipt candidateReceipt))
            {
                message = "缺少当前合同的 Candidate receipt 或人工实战检查。";
                return false;
            }

            JObject authoring = JObject.Parse(File.ReadAllText(AuthoringCatalogPath));
            JObject effect = FindEffect(authoring, contract.ResourceKey.Id);
            string prefabPath = effect?["published"]?.Value<string>("assetPath") ?? string.Empty;
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            string contentHash = VfxCompositionDraftService.HashFile(prefabPath);
            if (effect == null
                || !string.Equals(effect.Value<string>("reviewStatus"), "ReviewNeeded", StringComparison.Ordinal)
                || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null
                || !string.Equals(guid, playtest.CandidateAssetGuid, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(contentHash, playtest.CandidateContentHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(candidateReceipt.ConsumerContractHash, contract.ContractHash, StringComparison.OrdinalIgnoreCase))
            {
                message = "待评审资产、试玩证据或技能合同已变化，不能批准 Ready。";
                return false;
            }

            approval = new VfxReadyApprovalReceipt
            {
                EffectId = contract.ResourceKey.Id,
                ResourceKey = contract.ResourceKey.ToString(),
                PrefabPath = prefabPath,
                PrefabGuid = guid,
                PrefabContentHash = contentHash,
                CompositionHash = candidateReceipt.CompositionContentHash,
                CandidateReceiptHash = candidateReceipt.ReceiptHash,
                ConsumerContractHash = contract.ContractHash,
                ProfileDependencyHash = candidateReceipt.ProfileDependencyHash,
                PreviewEvidenceHash = candidateReceipt.PreviewEvidenceHash,
                ApprovedBy = Environment.UserName,
                ApprovedUtc = DateTime.UtcNow.ToString("O")
            };
            approval.ReceiptHash = VfxCanonicalJson.Hash(approval, "receiptHash");
            string approvalPath = ApprovalRoot + "/" + Role(contract.ResourceKey).ToLowerInvariant() + ".json";

            effect["approval"] = new JObject
            {
                ["kind"] = "HumanVfxApproval",
                ["receiptPath"] = approvalPath,
                ["receiptHash"] = approval.ReceiptHash
            };
            effect["visualMaturity"] = "Production";
            effect["reviewStatus"] = "Ready";
            using var transaction = new VfxReadyApprovalPublishTransaction(
                approvalPath,
                AuthoringCatalogPath,
                prefabPath);
            if (!transaction.Publish(approval, authoring, out message))
            {
                approval = null;
                return false;
            }
            message = Role(contract.ResourceKey)
                + " 已由 " + approval.ApprovedBy + " 显式批准 Ready；尚未切换正式绑定。";
            return true;
        }

        internal static bool ValidateReadyApproval(
            JObject effect,
            VfxConsumerContract contract,
            string prefabPath,
            out string message)
        {
            string receiptPath = effect?["approval"]?.Value<string>("receiptPath") ?? string.Empty;
            string expectedHash = effect?["approval"]?.Value<string>("receiptHash") ?? string.Empty;
            if (!File.Exists(receiptPath))
            {
                message = "缺少人工 Ready approval receipt。";
                return false;
            }
            try
            {
                VfxReadyApprovalReceipt receipt =
                    JsonConvert.DeserializeObject<VfxReadyApprovalReceipt>(File.ReadAllText(receiptPath));
                bool valid = receipt != null
                    && string.Equals(receipt.ReceiptHash, expectedHash, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(receipt.ReceiptHash, VfxCanonicalJson.Hash(receipt, "receiptHash"), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(receipt.ResourceKey, contract.ResourceKey.ToString(), StringComparison.Ordinal)
                    && string.Equals(receipt.ConsumerContractHash, contract.ContractHash, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(receipt.PrefabPath, prefabPath, StringComparison.Ordinal)
                    && string.Equals(receipt.PrefabGuid, AssetDatabase.AssetPathToGUID(prefabPath), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(receipt.PrefabContentHash, VfxCompositionDraftService.HashFile(prefabPath), StringComparison.OrdinalIgnoreCase);
                message = valid ? string.Empty : "人工 Ready approval receipt 已失效或被篡改。";
                return valid;
            }
            catch (Exception exception)
            {
                message = "人工 Ready approval receipt 无法读取：" + exception.Message;
                return false;
            }
        }

        private static bool TryResolveEvidence(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt,
            out VfxPlaytestOverrideRecord playtest,
            out string message)
        {
            playtest = LoadOverrides().Entries.FirstOrDefault(
                value => value.ResourceKey.ToResourceKey() == contract.ResourceKey);
            if (playtest == null
                || receipt == null
                || !string.Equals(playtest.ReceiptHash, receipt.ReceiptHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(playtest.ConsumerContractHash, contract.ContractHash, StringComparison.OrdinalIgnoreCase)
                || !VfxPublishedBindingEditorService.TryReadAndValidateReceipt(
                    playtest.ReceiptPath,
                    playtest.ReceiptHash,
                    out _)
                || !string.Equals(
                    playtest.CandidateAssetGuid,
                    AssetDatabase.AssetPathToGUID(playtest.CandidateAssetPath),
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    playtest.CandidateContentHash,
                    VfxCompositionDraftService.HashFile(playtest.CandidateAssetPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "当前 Candidate 没有绑定有效的本机试玩证据。";
                return false;
            }
            message = string.Empty;
            return true;
        }

        private static JObject FindEffect(JObject catalog, string resourceId) =>
            (catalog?["effects"] as JArray)?.OfType<JObject>().FirstOrDefault(
                value => string.Equals(
                    value["resourceKey"]?.Value<string>("id"),
                    resourceId,
                    StringComparison.Ordinal));

        private static VfxPlaytestOverrideDocument LoadOverrides()
        {
            if (!File.Exists(VfxPublishedBindingEditorService.PlaytestOverridePath))
                return new VfxPlaytestOverrideDocument();
            return JsonConvert.DeserializeObject<VfxPlaytestOverrideDocument>(
                       File.ReadAllText(VfxPublishedBindingEditorService.PlaytestOverridePath))
                   ?? new VfxPlaytestOverrideDocument();
        }

        private static void SaveOverrides(VfxPlaytestOverrideDocument document)
        {
            string directory = Path.GetDirectoryName(VfxPublishedBindingEditorService.PlaytestOverridePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                VfxPublishedBindingEditorService.PlaytestOverridePath,
                JsonConvert.SerializeObject(document, Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static void WriteJsonAtomic(string path, JToken value)
        {
            string temp = path + ".tmp";
            File.WriteAllText(
                temp,
                value.ToString(Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static string Role(ResourceKey key)
        {
            VfxConsumerContract contract = VfxConsumerContractResolver.Find(key);
            if (contract?.SlotLabel == "蓄力")
                return "Charge";
            if (contract?.SlotLabel == "飞行")
                return "Flight";
            return "Impact";
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parent))
                EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

}
