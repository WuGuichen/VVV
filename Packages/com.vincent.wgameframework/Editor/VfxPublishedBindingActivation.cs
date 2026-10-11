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
    internal sealed class VfxBindingActivationPlan
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.binding-activation-plan.v1";
        [JsonProperty("bindingSetId")] public string BindingSetId = "vfx.skill.fireball_burst";
        [JsonProperty("expectedManifestHash")] public string ExpectedManifestHash = string.Empty;
        [JsonProperty("expectedAuthoringCatalogHash")] public string ExpectedAuthoringCatalogHash = string.Empty;
        [JsonProperty("changes")] public List<VfxBindingActivationChange> Changes = new List<VfxBindingActivationChange>();
        [JsonProperty("diagnostics")] public List<string> Diagnostics = new List<string>();
        [JsonProperty("planHash")] public string PlanHash = string.Empty;
        [JsonIgnore] internal string Summary = string.Empty;
    }

    [Serializable]
    internal sealed class VfxBindingActivationChange
    {
        [JsonProperty("role")] public string Role = string.Empty;
        [JsonProperty("bindingId")] public string BindingId = string.Empty;
        [JsonProperty("resourceKey")] public string ResourceKey = string.Empty;
        [JsonProperty("oldAssetPath")] public string OldAssetPath = string.Empty;
        [JsonProperty("sourceAssetPath")] public string SourceAssetPath = string.Empty;
        [JsonProperty("targetAssetPath")] public string TargetAssetPath = string.Empty;
        [JsonProperty("assetGuid")] public string AssetGuid = string.Empty;
        [JsonProperty("assetContentHash")] public string AssetContentHash = string.Empty;
        [JsonProperty("contractHash")] public string ContractHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxBindingActivationReceipt
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.binding-activation-receipt.v1";
        [JsonProperty("bindingSetId")] public string BindingSetId = string.Empty;
        [JsonProperty("planHash")] public string PlanHash = string.Empty;
        [JsonProperty("previousManifestHash")] public string PreviousManifestHash = string.Empty;
        [JsonProperty("activatedManifestHash")] public string ActivatedManifestHash = string.Empty;
        [JsonProperty("changes")] public List<VfxBindingActivationChange> Changes = new List<VfxBindingActivationChange>();
        [JsonProperty("activatedUtc")] public string ActivatedUtc = string.Empty;
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
    }

    internal static class VfxPublishedBindingActivationService
    {
        private const string AuthoringCatalogPath =
            "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        private const string PlanPath =
            "Temp/MxFramework/VfxBindingActivation/fireball_burst.plan.json";
        private const string ReceiptRoot =
            "Assets/MxFrameworkGenerated/Vfx/ActivationReceipts";
        private const string ProductionRoot =
            "Assets/VFX/MxFramework/Production/FireballBurst";

        internal static bool CreateFireballBurstPlan(
            out VfxBindingActivationPlan plan,
            out string message)
        {
            plan = new VfxBindingActivationPlan();
            VfxPublishedBindingManifest manifest = VfxPublishedBindingEditorService.LoadManifest(out message);
            if (manifest == null)
                return false;
            if (!File.Exists(AuthoringCatalogPath))
            {
                message = "VFX Authoring Catalog 不存在。";
                return false;
            }

            JObject authoring;
            try
            {
                authoring = JObject.Parse(File.ReadAllText(AuthoringCatalogPath));
            }
            catch (Exception exception)
            {
                message = "VFX Authoring Catalog 无法解析：" + exception.Message;
                return false;
            }

            string bindingSetId = plan.BindingSetId;
            VfxPublishedBindingSet set = manifest.BindingSets.FirstOrDefault(
                value => string.Equals(value.BindingSetId, bindingSetId, StringComparison.Ordinal));
            if (set == null || set.Members.Count != 3)
            {
                message = "Fireball Burst 原子 binding set 不完整。";
                return false;
            }

            VfxPlaytestOverrideDocument overrides = LoadPlaytestOverrides();
            JArray effects = authoring["effects"] as JArray ?? new JArray();
            plan.ExpectedManifestHash = VfxPublishedBindingEditorService.ComputeManifestHash(manifest);
            plan.ExpectedAuthoringCatalogHash = VfxCompositionDraftService.HashFile(AuthoringCatalogPath);
            for (int i = 0; i < set.Members.Count; i++)
            {
                VfxPublishedBindingSetMember member = set.Members[i];
                VfxPublishedBindingRecord binding = manifest.Bindings.FirstOrDefault(
                    value => string.Equals(value.BindingId, member.BindingId, StringComparison.Ordinal));
                if (binding == null)
                {
                    plan.Diagnostics.Add(member.Role + " 缺少 Published Binding。");
                    continue;
                }

                JObject effect = effects.OfType<JObject>().FirstOrDefault(
                    value => string.Equals(
                        value["resourceKey"]?.Value<string>("id"),
                        binding.ResourceKey.Id,
                        StringComparison.Ordinal));
                string maturity = effect?.Value<string>("visualMaturity") ?? string.Empty;
                string review = effect?.Value<string>("reviewStatus") ?? string.Empty;
                string sourcePath = effect?["published"]?.Value<string>("assetPath") ?? string.Empty;
                VfxConsumerContract contract =
                    VfxConsumerContractResolver.Find(binding.ResourceKey.ToResourceKey());
                VfxPlaytestOverrideRecord playtest = overrides.Entries.FirstOrDefault(
                    value => value.ResourceKey.ToResourceKey() == binding.ResourceKey.ToResourceKey());
                VfxCompositionBakeReceipt playtestReceipt = null;
                if (!string.Equals(maturity, "Production", StringComparison.Ordinal)
                    || !string.Equals(review, "Ready", StringComparison.Ordinal))
                {
                    plan.Diagnostics.Add(member.Role + " 尚未达到 Production + Ready。");
                }
                string approvalMessage = contract == null
                    ? "无法解析当前真实技能合同。"
                    : string.Empty;
                if (contract == null
                    || !VfxManualCompositionReviewService.ValidateReadyApproval(
                        effect,
                        contract,
                        sourcePath,
                        out approvalMessage))
                {
                    plan.Diagnostics.Add(member.Role + "：" + approvalMessage);
                }
                if (playtest == null || !playtest.HumanPlaytested)
                    plan.Diagnostics.Add(member.Role + " 缺少绑定当前内容的人工实战检查。");
                if (playtest != null
                    && !VfxPublishedBindingEditorService.TryReadAndValidateReceipt(
                        playtest.ReceiptPath,
                        playtest.ReceiptHash,
                        out playtestReceipt))
                {
                    plan.Diagnostics.Add(member.Role + " 的 Candidate receipt 文件缺失、被篡改或已过期。");
                }
                if (string.IsNullOrWhiteSpace(sourcePath)
                    || AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
                {
                    plan.Diagnostics.Add(member.Role + " 缺少可发布 Prefab。");
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(sourcePath);
                if (playtest != null
                    && (!string.Equals(playtest.CandidateAssetGuid, guid, StringComparison.OrdinalIgnoreCase)
                        || (playtestReceipt != null
                            && !string.Equals(
                                playtestReceipt.OutputPrefabGuid,
                                guid,
                                StringComparison.OrdinalIgnoreCase))
                        || !string.Equals(
                            playtest.ConsumerContractHash,
                            binding.ContractHash,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    plan.Diagnostics.Add(member.Role + " 的试玩证据已过期或不属于当前技能合同。");
                }

                plan.Changes.Add(new VfxBindingActivationChange
                {
                    Role = member.Role,
                    BindingId = binding.BindingId,
                    ResourceKey = binding.ResourceKey.ToResourceKey().ToString(),
                    OldAssetPath = binding.AssetPath,
                    SourceAssetPath = sourcePath,
                    TargetAssetPath = ProductionRoot + "/FireballBurst_" + member.Role + ".prefab",
                    AssetGuid = guid,
                    AssetContentHash = VfxCompositionDraftService.HashFile(sourcePath),
                    ContractHash = binding.ContractHash
                });
            }

            plan.PlanHash = VfxCanonicalJson.Hash(plan, "planHash");
            plan.Summary = BuildPlanSummary(plan);
            Directory.CreateDirectory(Path.GetDirectoryName(PlanPath) ?? "Temp");
            File.WriteAllText(
                PlanPath,
                JsonConvert.SerializeObject(plan, Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
            bool ready = plan.Diagnostics.Count == 0 && plan.Changes.Count == 3;
            message = ready
                ? "Fireball Burst 三段原子发布计划已生成。"
                : "Fireball Burst 尚不可发布：" + string.Join("；", plan.Diagnostics);
            return ready;
        }

        internal static bool Execute(
            VfxBindingActivationPlan plan,
            out VfxBindingActivationReceipt receipt,
            out string message)
        {
            receipt = null;
            if (plan == null
                || plan.Diagnostics.Count > 0
                || plan.Changes.Count != 3
                || !string.Equals(plan.PlanHash, VfxCanonicalJson.Hash(plan, "planHash"), StringComparison.Ordinal))
            {
                message = "发布计划无效或仍有阻断项。";
                return false;
            }

            VfxPublishedBindingManifest manifest = VfxPublishedBindingEditorService.LoadManifest(out message);
            if (manifest == null
                || !string.Equals(
                    plan.ExpectedManifestHash,
                    VfxPublishedBindingEditorService.ComputeManifestHash(manifest),
                    StringComparison.Ordinal)
                || !string.Equals(
                    plan.ExpectedAuthoringCatalogHash,
                    VfxCompositionDraftService.HashFile(AuthoringCatalogPath),
                    StringComparison.Ordinal))
            {
                message = "Manifest 或 Authoring Catalog 已变化，请重新生成发布计划。";
                return false;
            }

            string manifestBackup = File.ReadAllText(VfxPublishedBindingEditorService.ManifestPath);
            string authoringBackup = File.ReadAllText(AuthoringCatalogPath);
            var completedMoves = new List<VfxBindingActivationChange>();
            var createdFolders = new List<string>();
            try
            {
                EnsureAssetFolder(ProductionRoot, createdFolders);
                for (int i = 0; i < plan.Changes.Count; i++)
                {
                    VfxBindingActivationChange change = plan.Changes[i];
                    if (!string.Equals(
                            AssetDatabase.AssetPathToGUID(change.SourceAssetPath),
                            change.AssetGuid,
                            StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(
                            VfxCompositionDraftService.HashFile(change.SourceAssetPath),
                            change.AssetContentHash,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(change.Role + " Prefab 已变化。");
                    if (!string.Equals(change.SourceAssetPath, change.TargetAssetPath, StringComparison.Ordinal)
                        && AssetDatabase.LoadMainAssetAtPath(change.TargetAssetPath) != null)
                        throw new InvalidOperationException("目标路径已存在：" + change.TargetAssetPath);

                    if (!string.Equals(change.SourceAssetPath, change.TargetAssetPath, StringComparison.Ordinal))
                    {
                        string moveError = AssetDatabase.MoveAsset(change.SourceAssetPath, change.TargetAssetPath);
                        if (!string.IsNullOrWhiteSpace(moveError))
                            throw new InvalidOperationException(moveError);
                        completedMoves.Add(change);
                    }
                }

                JObject authoring = JObject.Parse(authoringBackup);
                JArray effects = authoring["effects"] as JArray ?? new JArray();
                for (int i = 0; i < plan.Changes.Count; i++)
                {
                    VfxBindingActivationChange change = plan.Changes[i];
                    VfxPublishedBindingRecord binding = manifest.Bindings.First(
                        value => string.Equals(value.BindingId, change.BindingId, StringComparison.Ordinal));
                    binding.AssetPath = change.TargetAssetPath;
                    binding.AssetGuid = change.AssetGuid;
                    binding.AssetContentHash = VfxCompositionDraftService.HashFile(change.TargetAssetPath);
                    binding.VisualMaturity = "Production";
                    binding.ReviewStatus = "Ready";
                    JObject effect = effects.OfType<JObject>().First(
                        value => string.Equals(
                            value["resourceKey"]?.Value<string>("id"),
                            binding.ResourceKey.Id,
                            StringComparison.Ordinal));
                    effect["published"] ??= new JObject();
                    effect["published"]["assetPath"] = change.TargetAssetPath;
                    effect["published"]["assetGuid"] = change.AssetGuid;
                    effect["published"]["assetContentHash"] = binding.AssetContentHash;
                    effect["published"]["consumerContractHash"] = change.ContractHash;
                    effect["visualMaturity"] = "Production";
                    effect["reviewStatus"] = "Ready";
                }

                receipt = new VfxBindingActivationReceipt
                {
                    BindingSetId = plan.BindingSetId,
                    PlanHash = plan.PlanHash,
                    PreviousManifestHash = plan.ExpectedManifestHash,
                    Changes = plan.Changes,
                    ActivatedUtc = DateTime.UtcNow.ToString("O")
                };
                receipt.ReceiptHash = VfxCanonicalJson.Hash(
                    receipt,
                    "receiptHash",
                    "activatedManifestHash");
                for (int i = 0; i < plan.Changes.Count; i++)
                {
                    VfxPublishedBindingRecord binding = manifest.Bindings.First(
                        value => string.Equals(value.BindingId, plan.Changes[i].BindingId, StringComparison.Ordinal));
                    binding.ActivationReceiptHash = receipt.ReceiptHash;
                    JObject effect = effects.OfType<JObject>().First(
                        value => string.Equals(
                            value["resourceKey"]?.Value<string>("id"),
                            binding.ResourceKey.Id,
                            StringComparison.Ordinal));
                    effect["published"]["activationReceiptHash"] = receipt.ReceiptHash;
                }
                manifest.ManifestHash = VfxPublishedBindingEditorService.ComputeManifestHash(manifest);
                receipt.ActivatedManifestHash = manifest.ManifestHash;

                WriteJsonAtomic(AuthoringCatalogPath, authoring);
                WriteJsonAtomic(VfxPublishedBindingEditorService.ManifestPath, JObject.FromObject(manifest));
                SampleResourceCatalogBuilder.Generate();
                if (!VfxPublishedBindingEditorService.GenerateUnityProjection(out string projectionMessage))
                    throw new InvalidOperationException(projectionMessage);

                using (var finalization = new VfxBindingActivationFinalizationTransaction(
                    ReceiptRoot,
                    VfxPublishedBindingEditorService.PlaytestOverridePath))
                {
                    if (!finalization.PrepareReceipt(receipt, out string receiptMessage))
                        throw new InvalidOperationException(receiptMessage);
                    if (!finalization.ClearOverrideAndCommit(out string finalizationMessage))
                        throw new InvalidOperationException(finalizationMessage);
                }
                message = "Fireball Burst 三段已原子切换为 Production Active。";
                return true;
            }
            catch (Exception exception)
            {
                for (int i = completedMoves.Count - 1; i >= 0; i--)
                {
                    VfxBindingActivationChange change = completedMoves[i];
                    if (AssetDatabase.LoadMainAssetAtPath(change.TargetAssetPath) != null)
                        AssetDatabase.MoveAsset(change.TargetAssetPath, change.SourceAssetPath);
                }
                File.WriteAllText(
                    VfxPublishedBindingEditorService.ManifestPath,
                    manifestBackup,
                    new UTF8Encoding(false));
                File.WriteAllText(AuthoringCatalogPath, authoringBackup, new UTF8Encoding(false));
                DeleteCreatedEmptyFolders(createdFolders);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                SampleResourceCatalogBuilder.Generate();
                VfxPublishedBindingEditorService.GenerateUnityProjection(out _);
                message = "原子发布失败，三段资产与绑定已回滚：" + exception.Message;
                receipt = null;
                return false;
            }
        }

        internal static bool RollbackLatestFireballBurst(out string message)
        {
            if (!AssetDatabase.IsValidFolder(ReceiptRoot))
            {
                message = "没有可用的 Fireball Burst activation receipt。";
                return false;
            }
            string[] receiptGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { ReceiptRoot });
            string receiptPath = receiptGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(value => value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(value => value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(receiptPath))
            {
                message = "没有可用的 Fireball Burst activation receipt。";
                return false;
            }

            VfxBindingActivationReceipt receipt;
            try
            {
                receipt = JsonConvert.DeserializeObject<VfxBindingActivationReceipt>(
                    File.ReadAllText(receiptPath));
            }
            catch (Exception exception)
            {
                message = "Activation receipt 无法读取：" + exception.Message;
                return false;
            }
            if (receipt == null
                || receipt.Changes.Count != 3
                || !string.Equals(
                    receipt.ReceiptHash,
                    VfxCanonicalJson.Hash(receipt, "receiptHash", "activatedManifestHash"),
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "Activation receipt 无效或被篡改。";
                return false;
            }

            VfxPublishedBindingManifest manifest =
                VfxPublishedBindingEditorService.LoadManifest(out message);
            if (manifest == null
                || !string.Equals(
                    VfxPublishedBindingEditorService.ComputeManifestHash(manifest),
                    receipt.ActivatedManifestHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "当前 Published Manifest 已不是该 receipt 激活的版本，拒绝回滚。";
                return false;
            }

            string manifestBackup = File.ReadAllText(VfxPublishedBindingEditorService.ManifestPath);
            string authoringBackup = File.ReadAllText(AuthoringCatalogPath);
            var completedMoves = new List<VfxBindingActivationChange>();
            try
            {
                for (int i = 0; i < receipt.Changes.Count; i++)
                {
                    VfxBindingActivationChange change = receipt.Changes[i];
                    if (!string.Equals(
                            AssetDatabase.AssetPathToGUID(change.TargetAssetPath),
                            change.AssetGuid,
                            StringComparison.OrdinalIgnoreCase)
                        || AssetDatabase.LoadMainAssetAtPath(change.SourceAssetPath) != null)
                        throw new InvalidOperationException(change.Role + " 当前资产或 Review 回滚路径已变化。");
                    string moveError = AssetDatabase.MoveAsset(change.TargetAssetPath, change.SourceAssetPath);
                    if (!string.IsNullOrWhiteSpace(moveError))
                        throw new InvalidOperationException(moveError);
                    completedMoves.Add(change);
                }

                JObject authoring = JObject.Parse(authoringBackup);
                JArray effects = authoring["effects"] as JArray ?? new JArray();
                for (int i = 0; i < receipt.Changes.Count; i++)
                {
                    VfxBindingActivationChange change = receipt.Changes[i];
                    VfxPublishedBindingRecord binding = manifest.Bindings.First(
                        value => string.Equals(value.BindingId, change.BindingId, StringComparison.Ordinal));
                    binding.AssetPath = change.OldAssetPath;
                    binding.AssetGuid = AssetDatabase.AssetPathToGUID(change.OldAssetPath);
                    binding.AssetContentHash = VfxCompositionDraftService.HashFile(change.OldAssetPath);
                    binding.VisualMaturity = "LegacyPlaceholder";
                    binding.ReviewStatus = "ReviewNeeded";
                    binding.ActivationReceiptHash = string.Empty;

                    JObject effect = effects.OfType<JObject>().First(
                        value => string.Equals(
                            value["resourceKey"]?.Value<string>("id"),
                            binding.ResourceKey.Id,
                            StringComparison.Ordinal));
                    effect["published"]["assetPath"] = change.SourceAssetPath;
                    effect["published"]["assetGuid"] = change.AssetGuid;
                    effect["published"]["assetContentHash"] =
                        VfxCompositionDraftService.HashFile(change.SourceAssetPath);
                    effect["published"]["activationReceiptHash"] = null;
                }
                manifest.ManifestHash = VfxPublishedBindingEditorService.ComputeManifestHash(manifest);
                WriteJsonAtomic(AuthoringCatalogPath, authoring);
                WriteJsonAtomic(VfxPublishedBindingEditorService.ManifestPath, JObject.FromObject(manifest));
                SampleResourceCatalogBuilder.Generate();
                if (!VfxPublishedBindingEditorService.GenerateUnityProjection(out string projectionMessage))
                    throw new InvalidOperationException(projectionMessage);
                message = "Fireball Burst 三段正式绑定已按 activation receipt 原子回滚；Ready 候选保留在 Review 目录。";
                return true;
            }
            catch (Exception exception)
            {
                for (int i = completedMoves.Count - 1; i >= 0; i--)
                {
                    VfxBindingActivationChange change = completedMoves[i];
                    if (AssetDatabase.LoadMainAssetAtPath(change.SourceAssetPath) != null)
                        AssetDatabase.MoveAsset(change.SourceAssetPath, change.TargetAssetPath);
                }
                File.WriteAllText(
                    VfxPublishedBindingEditorService.ManifestPath,
                    manifestBackup,
                    new UTF8Encoding(false));
                File.WriteAllText(AuthoringCatalogPath, authoringBackup, new UTF8Encoding(false));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                SampleResourceCatalogBuilder.Generate();
                VfxPublishedBindingEditorService.GenerateUnityProjection(out _);
                message = "回滚失败，激活状态已恢复：" + exception.Message;
                return false;
            }
        }

        private static VfxPlaytestOverrideDocument LoadPlaytestOverrides()
        {
            if (!File.Exists(VfxPublishedBindingEditorService.PlaytestOverridePath))
                return new VfxPlaytestOverrideDocument();
            try
            {
                return JsonConvert.DeserializeObject<VfxPlaytestOverrideDocument>(
                           File.ReadAllText(VfxPublishedBindingEditorService.PlaytestOverridePath))
                       ?? new VfxPlaytestOverrideDocument();
            }
            catch
            {
                return new VfxPlaytestOverrideDocument();
            }
        }

        private static string BuildPlanSummary(VfxBindingActivationPlan plan)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Binding Set: " + plan.BindingSetId);
            for (int i = 0; i < plan.Changes.Count; i++)
            {
                VfxBindingActivationChange change = plan.Changes[i];
                builder.AppendLine(
                    change.Role + ": " + change.OldAssetPath + " → " + change.TargetAssetPath);
            }
            if (plan.Diagnostics.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("阻断项：");
                for (int i = 0; i < plan.Diagnostics.Count; i++)
                    builder.AppendLine("• " + plan.Diagnostics[i]);
            }
            return builder.ToString().TrimEnd();
        }

        private static void WriteJsonAtomic(string path, JToken value)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(
                tempPath,
                value.ToString(Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureAssetFolder(string path, List<string> createdFolders = null)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parent))
                EnsureAssetFolder(parent, createdFolders);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("Unity failed to create activation output folder: " + path);
            createdFolders?.Add(path);
        }

        private static void DeleteCreatedEmptyFolders(List<string> createdFolders)
        {
            for (int i = createdFolders.Count - 1; i >= 0; i--)
            {
                string folder = createdFolders[i];
                if (AssetDatabase.IsValidFolder(folder)
                    && Directory.Exists(folder)
                    && Directory.GetFileSystemEntries(folder).Length == 0)
                    AssetDatabase.DeleteAsset(folder);
            }
        }
    }

    internal sealed class VfxBindingActivationFinalizationTransaction : IDisposable
    {
        private readonly string _receiptRoot;
        private readonly string _overridePath;
        private readonly Action<string, string> _receiptWriter;
        private readonly Func<string, bool> _receiptImporter;
        private readonly Action<string> _overrideClearer;
        private readonly Action _runtimeRefresher;
        private readonly List<string> _createdFolders = new List<string>();
        private readonly bool _overrideExisted;
        private readonly byte[] _overrideContents;
        private readonly byte[] _overrideMetaContents;
        private bool _receiptAttempted;
        private bool _clearAttempted;
        private bool _committed;

        internal VfxBindingActivationFinalizationTransaction(
            string receiptRoot,
            string overridePath,
            Action<string, string> receiptWriter = null,
            Func<string, bool> receiptImporter = null,
            Action<string> overrideClearer = null,
            Action runtimeRefresher = null)
        {
            _receiptRoot = receiptRoot.Replace('\\', '/');
            _overridePath = overridePath.Replace('\\', '/');
            _receiptWriter = receiptWriter ?? WriteReceipt;
            _receiptImporter = receiptImporter ?? ImportReceipt;
            _overrideClearer = overrideClearer ?? DeleteOverrideFile;
            _runtimeRefresher = runtimeRefresher ?? VfxPublishedBindingEditorService.RefreshRuntimeBindingSource;
            _overrideExisted = File.Exists(_overridePath);
            if (_overrideExisted)
            {
                _overrideContents = File.ReadAllBytes(_overridePath);
                _overrideMetaContents = File.Exists(_overridePath + ".meta")
                    ? File.ReadAllBytes(_overridePath + ".meta")
                    : null;
            }
            try
            {
                EnsureReceiptFolder(_receiptRoot);
                string fileName = "fireball_burst_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff")
                    + "_" + Guid.NewGuid().ToString("N") + ".json";
                ReceiptPath = AssetDatabase.GenerateUniqueAssetPath(_receiptRoot + "/" + fileName);
            }
            catch (Exception exception)
            {
                var failures = new List<string>();
                DeleteCreatedEmptyFolders(failures);
                if (failures.Count > 0)
                    throw new InvalidOperationException(
                        "Activation receipt path preparation failed: " + exception.Message
                        + " Folder cleanup also failed: " + string.Join("; ", failures),
                        exception);
                throw;
            }
        }

        internal string ReceiptPath { get; }

        internal bool PrepareReceipt(VfxBindingActivationReceipt receipt, out string message)
        {
            _receiptAttempted = true;
            try
            {
                string json = JsonConvert.SerializeObject(receipt, Formatting.Indented) + Environment.NewLine;
                _receiptWriter(ReceiptPath, json);
                VfxBindingActivationReceipt roundTrip = JsonConvert.DeserializeObject<VfxBindingActivationReceipt>(
                    File.ReadAllText(ReceiptPath));
                if (roundTrip == null
                    || !JToken.DeepEquals(JToken.FromObject(receipt), JToken.FromObject(roundTrip)))
                    throw new InvalidDataException("Activation receipt roundtrip changed its content.");
                if (!_receiptImporter(ReceiptPath))
                    throw new IOException("Unity failed to import the provisional activation receipt.");
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (!TryRollback(out string rollbackError))
                    message = "Activation receipt prepare failed: " + exception.Message
                        + " Finalization rollback also failed: " + rollbackError;
                else
                    message = "Activation receipt prepare failed and was removed: " + exception.Message;
                return false;
            }
        }

        internal bool ClearOverrideAndCommit(out string message)
        {
            if (!_receiptAttempted || AssetDatabase.LoadAssetAtPath<TextAsset>(ReceiptPath) == null)
            {
                message = "Activation receipt is not prepared for finalization.";
                return false;
            }
            _clearAttempted = true;
            try
            {
                _overrideClearer(_overridePath);
                _runtimeRefresher();
                _committed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (!TryRollback(out string rollbackError))
                    message = "Activation finalization failed: " + exception.Message
                        + " Override/receipt rollback also failed: " + rollbackError;
                else
                    message = "Activation finalization failed and restored the override: " + exception.Message;
                return false;
            }
        }

        private bool TryRollback(out string error)
        {
            var failures = new List<string>();
            if (_receiptAttempted)
            {
                try
                {
                    AssetDatabase.DeleteAsset(ReceiptPath);
                    if (File.Exists(ReceiptPath)) File.Delete(ReceiptPath);
                    if (File.Exists(ReceiptPath + ".meta")) File.Delete(ReceiptPath + ".meta");
                }
                catch (Exception exception)
                {
                    failures.Add("receipt: " + exception.Message);
                }
            }
            if (_clearAttempted)
            {
                try
                {
                    if (_overrideExisted)
                    {
                        string parent = Path.GetDirectoryName(_overridePath);
                        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                        File.WriteAllBytes(_overridePath, _overrideContents);
                        if (_overrideMetaContents != null) File.WriteAllBytes(_overridePath + ".meta", _overrideMetaContents);
                        else if (File.Exists(_overridePath + ".meta")) File.Delete(_overridePath + ".meta");
                        if (_overridePath.StartsWith("Assets/", StringComparison.Ordinal))
                            AssetDatabase.ImportAsset(_overridePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    }
                    else
                    {
                        if (File.Exists(_overridePath)) File.Delete(_overridePath);
                        if (File.Exists(_overridePath + ".meta")) File.Delete(_overridePath + ".meta");
                    }
                    _runtimeRefresher();
                }
                catch (Exception exception)
                {
                    failures.Add("override: " + exception.Message);
                }
            }
            DeleteCreatedEmptyFolders(failures);
            _receiptAttempted = false;
            _clearAttempted = false;
            error = string.Join("; ", failures);
            return failures.Count == 0;
        }

        private void EnsureReceiptFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parent)) EnsureReceiptFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("Unity failed to create activation receipt folder: " + path);
            _createdFolders.Add(path);
        }

        private void DeleteCreatedEmptyFolders(List<string> failures)
        {
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
                    failures.Add("folder " + folder + ": " + exception.Message);
                }
            }
        }

        private static void WriteReceipt(string path, string json)
        {
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private static bool ImportReceipt(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null;
        }

        private static void DeleteOverrideFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (path.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(path + ".meta"))
                File.Delete(path + ".meta");
        }

        public void Dispose()
        {
            if (!_committed && (_receiptAttempted || _clearAttempted)) TryRollback(out _);
        }
    }

    internal sealed class VfxBindingActivationPlanner : IVfxBindingActivationPlanner
    {
        public bool CreateFireballBurstPlan(
            out VfxBindingActivationPlan plan,
            out string message) =>
            VfxPublishedBindingActivationService.CreateFireballBurstPlan(out plan, out message);
    }

    internal sealed class VfxBindingActivationExecutor : IVfxBindingActivationExecutor
    {
        public bool Execute(
            VfxBindingActivationPlan plan,
            out VfxBindingActivationReceipt receipt,
            out string message) =>
            VfxPublishedBindingActivationService.Execute(plan, out receipt, out message);

        public bool RollbackLatestFireballBurst(out string message) =>
            VfxPublishedBindingActivationService.RollbackLatestFireballBurst(out message);
    }
}
