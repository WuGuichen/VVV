using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    internal sealed class VfxLibraryCreationBrief
    {
        public string BriefName = string.Empty;
        public string TargetEffectId = string.Empty;
        public string Intent = string.Empty;
        public string Lifecycle = string.Empty;
        public string StyleProfileId = string.Empty;
        public string StyleProfileVersion = string.Empty;
        public string ProviderId = string.Empty;
        public string ProviderVersion = string.Empty;
        public string ProviderKind = string.Empty;
        public string ProviderModelId = string.Empty;
        public string ProviderModelVersion = string.Empty;
        public bool ProviderEnabledByDefault;
        public string TemplateId = string.Empty;
        public string TemplateVersion = string.Empty;
        public string TemplateKind = string.Empty;
        public bool TemplateCertified;
        public bool TemplateArbitraryTopology;
        public bool TemplateAllowsProvider;
        public string QualityProfileId = string.Empty;
        public string QualityProfileVersion = string.Empty;
        public int[] FixedEvaluationSeeds = Array.Empty<int>();
        public string ReferencePackage = string.Empty;
        public string LicenseId = "project-owned";
        public bool ExploreNewPrimitive;
        public bool RequireFallback;
    }

    internal sealed class VfxLibraryCreationCandidate
    {
        public string CandidateId = string.Empty;
        public string Direction = string.Empty;
        public string Summary = string.Empty;
        public string Accent = string.Empty;
        public bool ExploresNewPrimitive;
    }

    internal sealed class VfxLibraryCreationPlanResult
    {
        public bool Success;
        public bool CanExecuteCandidates;
        public string Message = string.Empty;
        public string RelativePath = string.Empty;
        public string ContentHash = string.Empty;
        public readonly List<VfxCandidatePlan> PreparedPlans = new List<VfxCandidatePlan>();
        public readonly List<VfxLibraryAuthoritativeStagePlan> AuthoritativeStagePlans = new List<VfxLibraryAuthoritativeStagePlan>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryCoreAnchorContract
    {
        [JsonProperty("anchor")] public string Anchor { get; set; } = "World";
        [JsonProperty("space")] public string Space { get; set; } = "World";
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryCoreLifecycleContract
    {
        [JsonProperty("mode")] public string Mode { get; set; } = "OneShot";
        [JsonProperty("defaultLifetimeFrames")] public int DefaultLifetimeFrames { get; set; } = 60;
        [JsonProperty("stopPolicy")] public string StopPolicy { get; set; } = "Expire";
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryCoreCreativeBrief
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = "mx.vfx.creative-brief.v1";
        [JsonProperty("briefId")] public string BriefId { get; set; } = string.Empty;
        [JsonProperty("catalogHash")] public string CatalogHash { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogHash")] public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        [JsonProperty("styleProfileId")] public string StyleProfileId { get; set; } = string.Empty;
        [JsonProperty("qualityProfileId")] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("qualityProfileVersion")] public string QualityProfileVersion { get; set; } = string.Empty;
        [JsonProperty("preferredBackend")] public string PreferredBackend { get; set; } = "ParticleSystem";
        [JsonProperty("preferredProviderId")] public string PreferredProviderId { get; set; } = string.Empty;
        [JsonProperty("targetCostTier")] public string TargetCostTier { get; set; } = "Medium";
        [JsonProperty("roles")] public List<string> Roles { get; set; } = new List<string>();
        [JsonProperty("themeIds")] public List<string> ThemeIds { get; set; } = new List<string>();
        [JsonProperty("anchorContract")] public VfxLibraryCoreAnchorContract AnchorContract { get; set; } = new VfxLibraryCoreAnchorContract();
        [JsonProperty("lifecycle")] public VfxLibraryCoreLifecycleContract Lifecycle { get; set; } = new VfxLibraryCoreLifecycleContract();
        [JsonProperty("requiredParameterIds")] public List<string> RequiredParameterIds { get; set; } = new List<string>();
        [JsonProperty("referenceEffectIds")] public List<string> ReferenceEffectIds { get; set; } = new List<string>();
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("candidateCount")] public int CandidateCount { get; set; } = 6;
        [JsonProperty("round")] public int Round { get; set; }
        [JsonProperty("parentCandidateIds")] public List<string> ParentCandidateIds { get; set; } = new List<string>();
        [JsonProperty("fixedSeeds")] public List<int> FixedSeeds { get; set; } = new List<int>();
        [JsonProperty("allowExperimentalGraphTopology")] public bool AllowExperimentalGraphTopology { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryCorePlanBundleProjection
    {
        [JsonProperty("generationPlan")] public VfxCoreGenerationPlan GenerationPlan { get; set; } = new VfxCoreGenerationPlan();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryAuthoritativeStagePlan
    {
        [JsonProperty("stageId", Order = 1)] public string StageId { get; set; } = string.Empty;
        [JsonProperty("generationPlanPath", Order = 2)] public string GenerationPlanPath { get; set; } = string.Empty;
        [JsonProperty("generationPlan", Order = 3)] public VfxCoreGenerationPlan GenerationPlan { get; set; } = new VfxCoreGenerationPlan();
    }

    internal sealed class VfxLibraryAuthoritativePlanBundle
    {
        public string CatalogSemanticHash = string.Empty;
        public string TemplateCatalogSemanticHash = string.Empty;
        public readonly List<VfxLibraryAuthoritativeStagePlan> StagePlans = new List<VfxLibraryAuthoritativeStagePlan>();
    }

    internal sealed class VfxLibraryCoreStageReceiptEvidence
    {
        public string StageId { get; set; } = string.Empty;
        public string GenerationPlanPath { get; set; } = string.Empty;
        public VfxCoreReceiptExportResult Export { get; set; }
    }

    /// <summary>Exports fixed Core manifests and receipts only after every candidate in each authoritative stage exists.</summary>
    /// <remarks>Editor-only and AllocByDesign. Any missing, stale, or mismatched local receipt blocks the complete batch.</remarks>
    internal static class VfxLibraryCoreReceiptExporter
    {
        public static bool TryExport(
            IReadOnlyList<VfxLibraryAuthoritativeStagePlan> stagePlans,
            IReadOnlyList<VfxCandidateGenerationResult> generationResults,
            out List<VfxLibraryCoreStageReceiptEvidence> evidence,
            out string message)
        {
            evidence = new List<VfxLibraryCoreStageReceiptEvidence>();
            if (stagePlans == null || stagePlans.Count == 0)
            {
                message = "没有可导出的 Authoring Core 阶段计划。";
                return false;
            }
            if (generationResults == null || generationResults.Count == 0)
            {
                message = "没有 Unity 候选生成回执，Core 回执导出已阻止。";
                return false;
            }

            for (int stageIndex = 0; stageIndex < stagePlans.Count; stageIndex++)
            {
                VfxLibraryAuthoritativeStagePlan stage = stagePlans[stageIndex];
                if (stage?.GenerationPlan == null || string.IsNullOrWhiteSpace(stage.GenerationPlanPath))
                {
                    message = "Authoring Core 阶段缺少固定 generation_plan.json：" + (stage?.StageId ?? "unknown");
                    return false;
                }
                if (!VfxCandidateSandbox.TryGetSessionRoot(stage.GenerationPlan.SessionId, out string sessionRoot, out string sessionError))
                {
                    message = "Authoring Core 阶段会话无效：" + stage.StageId + "；" + sessionError;
                    return false;
                }
                string manifestPath = sessionRoot + "/candidate_manifest.json";
                string receiptPath = sessionRoot + "/candidate_receipt.json";
                if (VfxCandidateSandbox.FixedDraftEvidenceAssetExists(manifestPath) ||
                    VfxCandidateSandbox.FixedDraftEvidenceAssetExists(receiptPath))
                {
                    message = "阶段 " + stage.StageId + " 已有固定 Draft 回执；本工具不会覆盖或删除旧证据，请创建新会话。";
                    return false;
                }
            }

            for (int stageIndex = 0; stageIndex < stagePlans.Count; stageIndex++)
            {
                VfxLibraryAuthoritativeStagePlan stage = stagePlans[stageIndex];
                if (stage?.GenerationPlan == null || string.IsNullOrWhiteSpace(stage.GenerationPlanPath))
                {
                    return FailAndRollback(
                        evidence,
                        "Authoring Core 阶段缺少固定 generation_plan.json：" + (stage?.StageId ?? "unknown"),
                        out message);
                }

                var stageResults = new List<VfxCandidateGenerationResult>();
                for (int resultIndex = 0; resultIndex < generationResults.Count; resultIndex++)
                {
                    VfxCandidateGenerationResult result = generationResults[resultIndex];
                    if (result?.Receipt != null && string.Equals(result.Receipt.SessionId, stage.GenerationPlan.SessionId, StringComparison.Ordinal))
                        stageResults.Add(result);
                }
                if (stageResults.Count != stage.GenerationPlan.Candidates.Count)
                {
                    return FailAndRollback(
                        evidence,
                        "阶段 " + stage.StageId + " 的 Unity 回执数量与 Authoring Core 候选数量不一致（" +
                        stageResults.Count + "/" + stage.GenerationPlan.Candidates.Count + "），已阻止固定回执导出。",
                        out message);
                }

                VfxCoreReceiptExportResult export = VfxCoreCandidateReceiptAdapter.Export(stage.GenerationPlanPath, stageResults);
                if (export == null || !export.Success)
                {
                    return FailAndRollback(
                        evidence,
                        "阶段 " + stage.StageId + " 的 Core 回执导出失败：" + (export?.Message ?? "未知错误"),
                        out message);
                }
                evidence.Add(new VfxLibraryCoreStageReceiptEvidence
                {
                    StageId = stage.StageId,
                    GenerationPlanPath = stage.GenerationPlanPath,
                    Export = export
                });
            }

            message = "已为 " + evidence.Count + " 个阶段导出固定 Core candidate_manifest.json / candidate_receipt.json；状态保持 Draft。";
            return true;
        }

        private static bool FailAndRollback(
            List<VfxLibraryCoreStageReceiptEvidence> evidence,
            string failureMessage,
            out string message)
        {
            int rolledBack = 0;
            var rollbackFailures = new List<string>();
            for (int i = evidence.Count - 1; i >= 0; i--)
            {
                VfxCoreReceiptExportResult export = evidence[i]?.Export;
                if (export == null)
                    continue;
                if (VfxCandidateSandbox.TryDeleteFixedDraftEvidenceAsset(export.ReceiptAssetPath, out bool receiptDeleted, out string receiptError) && receiptDeleted)
                    rolledBack++;
                else if (!string.IsNullOrWhiteSpace(receiptError))
                    rollbackFailures.Add(receiptError);
                if (VfxCandidateSandbox.TryDeleteFixedDraftEvidenceAsset(export.ManifestAssetPath, out bool manifestDeleted, out string manifestError) && manifestDeleted)
                    rolledBack++;
                else if (!string.IsNullOrWhiteSpace(manifestError))
                    rollbackFailures.Add(manifestError);
            }
            evidence.Clear();
            if (rollbackFailures.Count > 0)
            {
                message = failureMessage + " 回滚未完成；会话保持阻断并需要恢复：" + string.Join(" | ", rollbackFailures);
            }
            else
            {
                message = rolledBack > 0
                    ? failureMessage + " 已回滚本批次先前写出的 " + rolledBack + " 个 Draft 证据文件。"
                    : failureMessage;
            }
            return false;
        }
    }

    internal interface IVfxLibraryAuthoritativePlanService
    {
        /// <remarks>Editor-only, AllocByDesign. Invokes the authoritative Authoring Core CLI on an explicit user action.</remarks>
        bool TryCreatePlans(
            VfxLibraryCreationBrief brief,
            string expectedCatalogContentHash,
            VfxLibraryGenerationTemplateCatalog templateCatalog,
            IReadOnlyList<VfxLibraryReuseRecommendation> recommendations,
            out VfxLibraryAuthoritativePlanBundle bundle,
            out string message);
    }

    internal sealed class VfxLibraryCliAuthoritativePlanService : IVfxLibraryAuthoritativePlanService
    {
        private const string CliProjectPath = "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj";
        private const int CliTimeoutMilliseconds = 180000;

        public bool TryCreatePlans(
            VfxLibraryCreationBrief brief,
            string expectedCatalogContentHash,
            VfxLibraryGenerationTemplateCatalog templateCatalog,
            IReadOnlyList<VfxLibraryReuseRecommendation> recommendations,
            out VfxLibraryAuthoritativePlanBundle bundle,
            out string message)
        {
            bundle = null;
            if (brief == null || templateCatalog == null || !templateCatalog.IsLoaded)
            {
                message = brief == null ? "创作简报为空。" : templateCatalog?.Error ?? "生成模板目录未加载。";
                return false;
            }
            if (!MatchesContentHash(VfxAuthoringResourceLibraryWindow.CatalogPath, expectedCatalogContentHash) ||
                !MatchesContentHash(VfxLibraryGenerationTemplateCatalog.DefaultPath, templateCatalog.ContentHash))
            {
                message = "Catalog 或生成模板目录已在界面加载后变化，请刷新后重试。";
                return false;
            }

            string[] stages = VfxLibraryTempCreationPlanService.SelectStageIds(brief.Lifecycle);
            if (stages.Length == 0)
            {
                message = "创作简报没有选择可执行的 Fireball 阶段。";
                return false;
            }
            Directory.CreateDirectory(Path.Combine(VfxLibraryTempCreationPlanService.SessionRoot, "core"));
            VfxLibraryCoreCreativeBrief probeBrief = BuildCoreBrief(brief, stages[0], string.Empty, string.Empty, recommendations);
            if (!TryRunPlanCreate(probeBrief, "probe", out VfxCoreGenerationPlan probePlan, out message) ||
                string.IsNullOrWhiteSpace(probePlan.CatalogHash) || string.IsNullOrWhiteSpace(probePlan.GenerationTemplateCatalogHash))
            {
                message = "无法从 Authoring Core 获取当前语义哈希：" + message;
                return false;
            }

            var result = new VfxLibraryAuthoritativePlanBundle
            {
                CatalogSemanticHash = probePlan.CatalogHash,
                TemplateCatalogSemanticHash = probePlan.GenerationTemplateCatalogHash
            };
            for (int i = 0; i < stages.Length; i++)
            {
                VfxLibraryCoreCreativeBrief stageBrief = BuildCoreBrief(
                    brief,
                    stages[i],
                    result.CatalogSemanticHash,
                    result.TemplateCatalogSemanticHash,
                    recommendations);
                if (!TryRunPlanCreate(stageBrief, stages[i], out VfxCoreGenerationPlan plan, out message) || plan.Blocked ||
                    !string.Equals(plan.CatalogHash, result.CatalogSemanticHash, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(plan.GenerationTemplateCatalogHash, result.TemplateCatalogSemanticHash, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Authoring Core 已阻止 " + StageDisplayName(stages[i]) + " 计划：" + message;
                    return false;
                }
                for (int candidateIndex = 0; candidateIndex < plan.Candidates.Count; candidateIndex++)
                {
                    VfxCoreGenerationCandidateSpec candidate = plan.Candidates[candidateIndex];
                    if (!string.Equals(candidate.ProviderId, brief.ProviderId, StringComparison.Ordinal) ||
                        !string.Equals(candidate.ProviderVersion, brief.ProviderVersion, StringComparison.Ordinal) ||
                        !string.Equals(candidate.ModelId, brief.ProviderModelId, StringComparison.Ordinal) ||
                        !string.Equals(candidate.ModelVersion, brief.ProviderModelVersion, StringComparison.Ordinal) ||
                        !string.Equals(candidate.TemplateId, brief.TemplateId, StringComparison.Ordinal) ||
                        !string.Equals(candidate.TemplateVersion, brief.TemplateVersion, StringComparison.Ordinal) ||
                        !candidate.DraftOnly ||
                        !VfxCandidateSandbox.IsWithinSessionRoot(candidate.OutputRoot + "/placeholder.asset", plan.SandboxRoot))
                    {
                        message = "Authoring Core 返回的提供器或模板与创作简报不一致，已停止 Unity 候选生成。";
                        return false;
                    }
                }
                string generationPlanPath = WriteCanonicalGenerationPlan(plan);
                result.StagePlans.Add(new VfxLibraryAuthoritativeStagePlan
                {
                    StageId = stages[i],
                    GenerationPlanPath = generationPlanPath,
                    GenerationPlan = plan
                });
            }

            if (!MatchesContentHash(VfxAuthoringResourceLibraryWindow.CatalogPath, expectedCatalogContentHash) ||
                !MatchesContentHash(VfxLibraryGenerationTemplateCatalog.DefaultPath, templateCatalog.ContentHash))
            {
                message = "Catalog 或生成模板目录在权威计划期间变化，计划已作废。";
                return false;
            }
            bundle = result;
            message = "Authoring Core 校验和双重哈希防护通过。";
            return true;
        }

        private static string WriteCanonicalGenerationPlan(VfxCoreGenerationPlan plan)
        {
            if (plan == null || string.IsNullOrWhiteSpace(plan.SessionId))
                throw new InvalidOperationException("Authoring Core generation plan has no session id.");
            string path = VfxCandidateProtocol.AuthoritativeEvidenceRoot + "/" + plan.SessionId + "/generation_plan.json";
            if (!VfxCandidateSandbox.IsControlledEvidencePath(path))
                throw new InvalidOperationException("Authoring Core generation plan path escaped the controlled evidence root: " + path);
            VfxCandidateSandbox.WriteJsonFile(path, plan);
            return path;
        }

        private static VfxLibraryCoreCreativeBrief BuildCoreBrief(
            VfxLibraryCreationBrief source,
            string stageId,
            string catalogHash,
            string templateHash,
            IReadOnlyList<VfxLibraryReuseRecommendation> recommendations)
        {
            var result = new VfxLibraryCoreCreativeBrief
            {
                BriefId = "vfx.brief.fireball." + stageId,
                CatalogHash = catalogHash,
                GenerationTemplateCatalogHash = templateHash,
                StyleProfileId = source.StyleProfileId,
                QualityProfileId = source.QualityProfileId,
                QualityProfileVersion = source.QualityProfileVersion,
                PreferredBackend = BackendForTemplate(source.TemplateKind),
                PreferredProviderId = source.ProviderId,
                Description = source.Intent,
                CandidateCount = source.ExploreNewPrimitive ? 6 : 5,
                FixedSeeds = new List<int>(source.FixedEvaluationSeeds ?? Array.Empty<int>()),
                AllowExperimentalGraphTopology = source.TemplateArbitraryTopology
            };
            result.Roles.Add(StageRole(stageId));
            result.ThemeIds.Add("fire");
            result.AnchorContract = AnchorForStage(stageId);
            result.Lifecycle = LifecycleForStage(stageId);
            for (int i = 0; i < recommendations.Count; i++)
                if (recommendations[i]?.Item is VfxLibraryEffect)
                    result.ReferenceEffectIds.Add(recommendations[i].Item.StableId);
            return result;
        }

        private static bool TryRunPlanCreate(VfxLibraryCoreCreativeBrief brief, string fileId, out VfxCoreGenerationPlan plan, out string message)
        {
            plan = null;
            string root = Path.GetFullPath(".");
            string coreRoot = Path.Combine(root, VfxLibraryTempCreationPlanService.SessionRoot, "core");
            string briefPath = Path.Combine(coreRoot, fileId + "_brief.json");
            string outputPath = Path.Combine(coreRoot, fileId + "_plan.json");
            WriteJson(briefPath, brief);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            string arguments = "run --configuration Release --no-restore --project " + Quote(Path.Combine(root, CliProjectPath)) +
                               " -- vfx plan-create --project-root " + Quote(root) +
                               " --brief " + Quote(briefPath) +
                               " --templates " + Quote(VfxLibraryGenerationTemplateCatalog.DefaultPath) +
                               " --output " + Quote(outputPath);
            var start = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                using Process process = Process.Start(start);
                if (process == null)
                {
                    message = "无法启动 dotnet Authoring CLI。";
                    return false;
                }
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(CliTimeoutMilliseconds))
                {
                    process.Kill();
                    message = "Authoring CLI 在 180 秒内未完成。";
                    return false;
                }
                Task.WaitAll(stdout, stderr);
                if (!File.Exists(outputPath))
                {
                    message = "Authoring CLI 未产生计划文件。" + ShortError(stderr.Result);
                    return false;
                }
                VfxLibraryCorePlanBundleProjection projection = JsonConvert.DeserializeObject<VfxLibraryCorePlanBundleProjection>(File.ReadAllText(outputPath));
                plan = projection?.GenerationPlan;
                if (plan == null)
                {
                    message = "Authoring CLI 输出不是可识别的 VFX GenerationPlan。" + ShortError(stderr.Result);
                    return false;
                }
                if (process.ExitCode != 0 && !string.Equals(fileId, "probe", StringComparison.Ordinal))
                {
                    message = "CLI 退出码 " + process.ExitCode + DiagnosticSummary(plan.Diagnostics) + ShortError(stderr.Result);
                    return false;
                }
                message = process.ExitCode == 0 ? "已通过" : "探测计划已返回当前哈希";
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private static bool MatchesContentHash(string path, string expected)
        {
            return File.Exists(path) && !string.IsNullOrWhiteSpace(expected) &&
                   string.Equals(VfxLibraryTempCreationPlanService.Hash(File.ReadAllBytes(path)), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string BackendForTemplate(string templateKind)
        {
            if (string.Equals(templateKind, "LineRendererPrimitive", StringComparison.Ordinal)) return "LineRenderer";
            if (templateKind != null && templateKind.Contains("VfxGraph")) return "VfxGraph";
            return "ParticleSystem";
        }

        private static string StageRole(string stageId) => stageId == "charge" ? "Charge" : stageId == "flight" ? "Flight" : "Impact";
        private static string StageDisplayName(string stageId) => stageId == "charge" ? "蓄力" : stageId == "flight" ? "飞行" : "命中";
        private static VfxLibraryCoreAnchorContract AnchorForStage(string stageId) => new VfxLibraryCoreAnchorContract
        {
            Anchor = stageId == "charge" ? "Caster" : stageId == "flight" ? "Path" : "HitPoint",
            Space = stageId == "charge" ? "Local" : "World"
        };
        private static VfxLibraryCoreLifecycleContract LifecycleForStage(string stageId) => new VfxLibraryCoreLifecycleContract
        {
            Mode = stageId == "impact" ? "OneShot" : "Loop",
            DefaultLifetimeFrames = stageId == "impact" ? 60 : 120,
            StopPolicy = stageId == "impact" ? "Expire" : "Stop"
        };

        private static void WriteJson(string path, object value)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(value, Formatting.Indented) + Environment.NewLine);
            File.WriteAllBytes(path, bytes);
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
        private static string ShortError(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : " · " + value.Trim().Substring(0, Math.Min(240, value.Trim().Length));
        private static string DiagnosticSummary(Newtonsoft.Json.Linq.JArray diagnostics) => diagnostics == null || diagnostics.Count == 0 ? string.Empty : " · " + diagnostics[0].ToString(Formatting.None);
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryCreationBriefSnapshot
    {
        [JsonProperty("briefName", Order = 1)] public string BriefName { get; set; } = string.Empty;
        [JsonProperty("intent", Order = 2)] public string Intent { get; set; } = string.Empty;
        [JsonProperty("lifecycle", Order = 3)] public string Lifecycle { get; set; } = string.Empty;
        [JsonProperty("roles", Order = 4)] public string[] Roles { get; set; } = Array.Empty<string>();
        [JsonProperty("themeIds", Order = 5)] public string[] ThemeIds { get; set; } = Array.Empty<string>();
        [JsonProperty("styleProfileId", Order = 6)] public string StyleProfileId { get; set; } = string.Empty;
        [JsonProperty("styleProfileVersion", Order = 7)] public string StyleProfileVersion { get; set; } = string.Empty;
        [JsonProperty("providerId", Order = 8)] public string ProviderId { get; set; } = string.Empty;
        [JsonProperty("providerVersion", Order = 9)] public string ProviderVersion { get; set; } = string.Empty;
        [JsonProperty("providerKind", Order = 10)] public string ProviderKind { get; set; } = string.Empty;
        [JsonProperty("providerModelId", Order = 11)] public string ProviderModelId { get; set; } = string.Empty;
        [JsonProperty("providerModelVersion", Order = 12)] public string ProviderModelVersion { get; set; } = string.Empty;
        [JsonProperty("templateId", Order = 13)] public string TemplateId { get; set; } = string.Empty;
        [JsonProperty("templateVersion", Order = 14)] public string TemplateVersion { get; set; } = string.Empty;
        [JsonProperty("templateKind", Order = 15)] public string TemplateKind { get; set; } = string.Empty;
        [JsonProperty("qualityProfileId", Order = 16)] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("qualityProfileVersion", Order = 17)] public string QualityProfileVersion { get; set; } = string.Empty;
        [JsonProperty("fixedEvaluationSeeds", Order = 18)] public int[] FixedEvaluationSeeds { get; set; } = Array.Empty<int>();
        [JsonProperty("referencePackage", Order = 19)] public string ReferencePackage { get; set; } = string.Empty;
        [JsonProperty("licenseId", Order = 20)] public string LicenseId { get; set; } = string.Empty;
        [JsonProperty("exploreNewPrimitive", Order = 21)] public bool ExploreNewPrimitive { get; set; }
        [JsonProperty("requireFallback", Order = 22)] public bool RequireFallback { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryReuseViewEntry
    {
        [JsonProperty("stableId", Order = 1)] public string StableId { get; set; } = string.Empty;
        [JsonProperty("kind", Order = 2)] public string Kind { get; set; } = string.Empty;
        [JsonProperty("reviewStatus", Order = 3)] public string ReviewStatus { get; set; } = string.Empty;
        [JsonProperty("score", Order = 4)] public int Score { get; set; }
        [JsonProperty("reason", Order = 5)] public string Reason { get; set; } = string.Empty;
    }

    /// <summary>Editor view request that links the UI brief to typed candidate executor plans.</summary>
    /// <remarks>This is not the authoritative Authoring Core GenerationPlan contract.</remarks>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryGenerationViewRequest
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = "mx.vfx.editor.creation-view-request.v1";
        [JsonProperty("requestId", Order = 2)] public string RequestId { get; set; } = string.Empty;
        [JsonProperty("catalogContentHash", Order = 3)] public string CatalogContentHash { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogContentHash", Order = 4)] public string GenerationTemplateCatalogContentHash { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogId", Order = 5)] public string GenerationTemplateCatalogId { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogVersion", Order = 6)] public string GenerationTemplateCatalogVersion { get; set; } = string.Empty;
        [JsonProperty("briefHash", Order = 7)] public string BriefHash { get; set; } = string.Empty;
        [JsonProperty("brief", Order = 8)] public VfxLibraryCreationBriefSnapshot Brief { get; set; } = new VfxLibraryCreationBriefSnapshot();
        [JsonProperty("fixedEvaluationSeeds", Order = 9)] public int[] FixedEvaluationSeeds { get; set; } = Array.Empty<int>();
        [JsonProperty("reuseEvidence", Order = 10)] public List<VfxLibraryReuseViewEntry> ReuseEvidence { get; set; } = new List<VfxLibraryReuseViewEntry>();
        [JsonProperty("authoritativeStagePlans", Order = 11)] public List<VfxLibraryAuthoritativeStagePlan> AuthoritativeStagePlans { get; set; } = new List<VfxLibraryAuthoritativeStagePlan>();
        [JsonProperty("candidatePlans", Order = 12)] public List<VfxCandidatePlan> CandidatePlans { get; set; } = new List<VfxCandidatePlan>();
        [JsonProperty("viewOnly", Order = 13)] public bool ViewOnly { get; set; } = true;
        [JsonProperty("authoritativePlannerRequiredForPromotion", Order = 14)] public bool AuthoritativePlannerRequiredForPromotion { get; set; } = true;
    }

    /// <summary>Blocked handoff request shown by the Editor before the formal Core promotion plan exists.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryPromotionViewRequest
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = "mx.vfx.editor.promotion-view-request.v1";
        [JsonProperty("requestId", Order = 2)] public string RequestId { get; set; } = string.Empty;
        [JsonProperty("candidateDirectionId", Order = 3)] public string CandidateDirectionId { get; set; } = string.Empty;
        [JsonProperty("catalogContentHash", Order = 4)] public string CatalogContentHash { get; set; } = string.Empty;
        [JsonProperty("generationTemplateCatalogContentHash", Order = 5)] public string GenerationTemplateCatalogContentHash { get; set; } = string.Empty;
        [JsonProperty("generationViewRequestContentHash", Order = 6)] public string GenerationViewRequestContentHash { get; set; } = string.Empty;
        [JsonProperty("candidateReceiptPaths", Order = 7)] public List<string> CandidateReceiptPaths { get; set; } = new List<string>();
        [JsonProperty("requestedReviewStatus", Order = 8)] public string RequestedReviewStatus { get; set; } = "ReviewNeeded";
        [JsonProperty("automaticReadyAllowed", Order = 9)] public bool AutomaticReadyAllowed { get; set; }
        [JsonProperty("blocked", Order = 10)] public bool Blocked { get; set; } = true;
        [JsonProperty("blockedUntil", Order = 11)] public string[] BlockedUntil { get; set; } = Array.Empty<string>();
        [JsonProperty("wouldMoveAssets", Order = 12)] public bool WouldMoveAssets { get; set; }
        [JsonProperty("wouldWriteCatalog", Order = 13)] public bool WouldWriteCatalog { get; set; }
    }

    internal interface IVfxLibraryCreationPlanService
    {
        IReadOnlyList<VfxLibraryCreationCandidate> Candidates { get; }
        VfxLibraryGenerationTemplateCatalog TemplateCatalog { get; }

        /// <remarks>Editor-only, AllocByDesign. Writes a deterministic plan under Temp only.</remarks>
        VfxLibraryCreationPlanResult CreateGenerationPlan(
            VfxLibraryCreationBrief brief,
            string catalogHash,
            IReadOnlyList<VfxLibraryReuseRecommendation> recommendations);

        /// <remarks>Editor-only, AllocByDesign. Writes a blocked promotion plan under Temp only.</remarks>
        VfxLibraryCreationPlanResult CreatePromotionPlan(
            VfxLibraryCreationBrief brief,
            VfxLibraryCreationCandidate candidate,
            string catalogHash,
            string generationPlanHash,
            IReadOnlyList<VfxCandidateGenerationResult> generationResults);

        /// <remarks>Editor-only, LowFreqAlloc.</remarks>
        bool CleanSession(out string message);
    }

    internal sealed class VfxLibraryTempCreationPlanService : IVfxLibraryCreationPlanService
    {
        internal const string SessionId = "fireball-lifecycle";
        internal const string SessionRoot = "Temp/MxFramework/VfxAuthoring/CreativeLab/fireball-lifecycle";
        internal const string GenerationPlanPath = SessionRoot + "/generation_plan.json";
        internal const string PromotionPlanPath = SessionRoot + "/promotion_plan.json";

        private static readonly VfxLibraryCreationCandidate[] CandidateDefinitions =
        {
            new VfxLibraryCreationCandidate
            {
                CandidateId = "candidate.01.conservative-reuse",
                Direction = "保守复用",
                Summary = "最大化复用现有 Fireball 发布效果、配方与基础单元，只补齐明确缺口。",
                Accent = "复用优先"
            },
            new VfxLibraryCreationCandidate
            {
                CandidateId = "candidate.02.high-readability",
                Direction = "高辨识度",
                Summary = "强化蓄力轮廓、飞行方向和命中主峰，优先保证战斗可读性。",
                Accent = "可读性"
            },
            new VfxLibraryCreationCandidate
            {
                CandidateId = "candidate.03.low-budget",
                Direction = "低预算",
                Summary = "减少透明叠层和高频粒子，保留同一生命周期与降级效果节奏。",
                Accent = "性能"
            },
            new VfxLibraryCreationCandidate
            {
                CandidateId = "candidate.04.strong-impact",
                Direction = "强冲击",
                Summary = "提高命中爆发、冲击环和余烬层次，同时守住裁切与预算门禁。",
                Accent = "冲击感"
            },
            new VfxLibraryCreationCandidate
            {
                CandidateId = "candidate.05.theme-variant",
                Direction = "主题变体",
                Summary = "沿用标准结构，探索可替换色带、烟雾密度和材质节奏。",
                Accent = "变体"
            },
            new VfxLibraryCreationCandidate
            {
                CandidateId = "candidate.06.primitive-exploration",
                Direction = "新基础单元探索",
                Summary = "只针对共享能力缺口提出闪光、火花、烟雾或冲击环基础单元简报。",
                Accent = "基础单元",
                ExploresNewPrimitive = true
            }
        };

        private readonly IVfxLibraryAuthoritativePlanService _authoritativePlanService;

        public IReadOnlyList<VfxLibraryCreationCandidate> Candidates => CandidateDefinitions;
        public VfxLibraryGenerationTemplateCatalog TemplateCatalog { get; } = VfxLibraryGenerationTemplateCatalog.Load();

        public VfxLibraryTempCreationPlanService()
            : this(new VfxLibraryCliAuthoritativePlanService())
        {
        }

        internal VfxLibraryTempCreationPlanService(IVfxLibraryAuthoritativePlanService authoritativePlanService)
        {
            _authoritativePlanService = authoritativePlanService ?? throw new ArgumentNullException(nameof(authoritativePlanService));
        }

        public VfxLibraryCreationPlanResult CreateGenerationPlan(
            VfxLibraryCreationBrief brief,
            string catalogHash,
            IReadOnlyList<VfxLibraryReuseRecommendation> recommendations)
        {
            if (brief == null || !TemplateCatalog.IsLoaded)
                return new VfxLibraryCreationPlanResult { Message = brief == null ? "创作简报为空。" : TemplateCatalog.Error };
            if (string.IsNullOrWhiteSpace(catalogHash))
                return new VfxLibraryCreationPlanResult { Message = "语义目录内容哈希为空，已阻止生成。" };
            if (!_authoritativePlanService.TryCreatePlans(
                    brief,
                    catalogHash,
                    TemplateCatalog,
                    recommendations,
                    out VfxLibraryAuthoritativePlanBundle authoritative,
                    out string authoritativeMessage))
                return new VfxLibraryCreationPlanResult { Message = authoritativeMessage };

            VfxLibraryCreationBriefSnapshot snapshot = BuildBrief(brief);
            string snapshotJson = JsonConvert.SerializeObject(snapshot, Formatting.None);
            string briefHash = Hash(Encoding.UTF8.GetBytes(snapshotJson));
            string boundInputHash = Hash(Encoding.UTF8.GetBytes(snapshotJson + "\n" + catalogHash + "\n" + TemplateCatalog.ContentHash));
            string sessionPrefix = SessionId + "_" + boundInputHash.Substring(0, 10);
            List<VfxCandidatePlan> plans = BuildBoundCandidatePlans(brief, authoritative, sessionPrefix, catalogHash, TemplateCatalog.ContentHash, briefHash);
            int[] fixedSeeds = brief.FixedEvaluationSeeds ?? Array.Empty<int>();

            var request = new VfxLibraryGenerationViewRequest
            {
                RequestId = "vfx.editor.creation." + SessionId + "." + briefHash.Substring(0, 12),
                CatalogContentHash = catalogHash,
                GenerationTemplateCatalogContentHash = TemplateCatalog.ContentHash,
                GenerationTemplateCatalogId = TemplateCatalog.CatalogId,
                GenerationTemplateCatalogVersion = TemplateCatalog.CatalogVersion,
                BriefHash = briefHash,
                Brief = snapshot,
                FixedEvaluationSeeds = (int[])fixedSeeds.Clone(),
                ReuseEvidence = BuildReuseDecision(recommendations),
                AuthoritativeStagePlans = authoritative.StagePlans,
                CandidatePlans = plans
            };
            bool providerConfigured = brief.ProviderEnabledByDefault &&
                string.Equals(brief.ProviderId, "mx.vfx.provider.procedural", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(brief.ProviderVersion) &&
                !string.IsNullOrWhiteSpace(brief.ProviderModelVersion);
            bool templateSupported = brief.TemplateCertified && !brief.TemplateArbitraryTopology && brief.TemplateAllowsProvider &&
                string.Equals(brief.TemplateId, "mx.vfx.template.particle.fireball", StringComparison.Ordinal);
            bool evaluationConfigured = fixedSeeds.Length > 0 && !string.IsNullOrWhiteSpace(brief.QualityProfileId) && !string.IsNullOrWhiteSpace(brief.QualityProfileVersion);
            VfxLibraryCreationPlanResult result = WritePlan(
                GenerationPlanPath,
                request,
                providerConfigured && templateSupported && evaluationConfigured
                    ? authoritativeMessage + " 已写入绑定 Core GenerationPlan 的编辑器视图请求，候选将由正式 Unity 编辑器执行器逐个生成。"
                    : "编辑器视图请求已写入临时目录；提供器、模板或质量档尚未满足当前 Fireball 执行条件，不会生成 Unity 资产。");
            result.CanExecuteCandidates = result.Success && providerConfigured && templateSupported && evaluationConfigured && plans.Count > 0;
            if (result.CanExecuteCandidates)
            {
                result.PreparedPlans.AddRange(plans);
                result.AuthoritativeStagePlans.AddRange(authoritative.StagePlans);
            }
            return result;
        }

        internal static string[] SelectStageIds(string lifecycle)
        {
            if (string.Equals(lifecycle, "仅蓄力", StringComparison.Ordinal)) return new[] { "charge" };
            if (string.Equals(lifecycle, "仅飞行", StringComparison.Ordinal)) return new[] { "flight" };
            if (string.Equals(lifecycle, "仅命中", StringComparison.Ordinal)) return new[] { "impact" };
            return new[] { "charge", "flight", "impact" };
        }

        internal static List<VfxCandidatePlan> BuildBoundCandidatePlans(
            VfxLibraryCreationBrief brief,
            VfxLibraryAuthoritativePlanBundle authoritative,
            string sessionPrefix,
            string catalogContentHash,
            string templateCatalogContentHash,
            string editorBriefHash)
        {
            var result = new List<VfxCandidatePlan>();
            if (brief == null || authoritative == null)
                return result;
            List<VfxCandidatePlan> templates = VfxCreativeCandidateMenu.CreateFireballRoundZeroPlans(sessionPrefix);
            for (int stageIndex = 0; stageIndex < authoritative.StagePlans.Count; stageIndex++)
            {
                VfxLibraryAuthoritativeStagePlan stage = authoritative.StagePlans[stageIndex];
                VfxCoreGenerationPlan corePlan = stage.GenerationPlan;
                if (corePlan == null || corePlan.Blocked)
                    continue;
                for (int candidateIndex = 0; candidateIndex < corePlan.Candidates.Count; candidateIndex++)
                {
                    VfxCoreGenerationCandidateSpec coreCandidate = corePlan.Candidates[candidateIndex];
                    string directionId = DirectionIdForVariant(coreCandidate.VariantKind);
                    if (string.IsNullOrEmpty(directionId) || !brief.ExploreNewPrimitive && directionId == "explore")
                        continue;
                    VfxCandidatePlan plan = FindTemplatePlan(templates, stage.StageId, directionId);
                    if (plan == null)
                        continue;
                    plan.PlanId = corePlan.PlanId + "." + stage.StageId + "." + directionId;
                    plan.SessionId = corePlan.SessionId;
                    plan.CoreCandidateId = coreCandidate.CandidateId;
                    plan.CandidateOutputRoot = coreCandidate.OutputRoot;
                    plan.FixedSeed = unchecked((uint)coreCandidate.Seed);
                    plan.GeneratorId = "mx.vfx.candidate.core-bound-fireball";
                    plan.GeneratorVersion = "1.0.0";
                    AddProvenance(plan, "corePlanId", corePlan.PlanId);
                    AddProvenance(plan, "corePlanHash", corePlan.PlanHash);
                    AddProvenance(plan, "coreCandidateId", coreCandidate.CandidateId);
                    AddProvenance(plan, "coreVariantKind", coreCandidate.VariantKind);
                    AddProvenance(plan, "coreCatalogSemanticHash", authoritative.CatalogSemanticHash);
                    AddProvenance(plan, "coreGenerationTemplateCatalogSemanticHash", authoritative.TemplateCatalogSemanticHash);
                    AddProvenance(plan, "catalogContentHash", catalogContentHash);
                    AddProvenance(plan, "generationTemplateCatalogContentHash", templateCatalogContentHash);
                    AddProvenance(plan, "editorBriefHash", editorBriefHash);
                    AddProvenance(plan, "providerId", coreCandidate.ProviderId);
                    AddProvenance(plan, "providerVersion", coreCandidate.ProviderVersion);
                    AddProvenance(plan, "providerGrantId", coreCandidate.ProviderGrantId);
                    AddProvenance(plan, "modelId", coreCandidate.ModelId);
                    AddProvenance(plan, "modelVersion", coreCandidate.ModelVersion);
                    AddProvenance(plan, "templateId", coreCandidate.TemplateId);
                    AddProvenance(plan, "templateVersion", coreCandidate.TemplateVersion);
                    AddProvenance(plan, "styleProfileId", brief.StyleProfileId);
                    AddProvenance(plan, "styleProfileVersion", brief.StyleProfileVersion);
                    AddProvenance(plan, "qualityProfileId", corePlan.QualityProfileId);
                    AddProvenance(plan, "qualityProfileVersion", corePlan.QualityProfileVersion);
                    AddProvenance(plan, "licenseId", brief.LicenseId);
                    AddProvenance(plan, "certifiedUnityAdapter", "mx.vfx.fireball-template-adapter@1.0.0");
                    AddProvenance(plan, "reviewBoundary", "DraftOnly;HumanApprovalRequired");
                    result.Add(plan);
                }
            }
            return result;
        }

        private static VfxCandidatePlan FindTemplatePlan(List<VfxCandidatePlan> plans, string stageId, string directionId)
        {
            string suffix = "." + stageId + "." + directionId;
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].PlanId.EndsWith(suffix, StringComparison.Ordinal))
                    return plans[i];
            return null;
        }

        internal static int DirectionIndexForPlan(VfxCandidatePlan plan)
        {
            if (plan == null) return -1;
            string id = plan.PlanId ?? string.Empty;
            if (id.EndsWith(".reuse", StringComparison.Ordinal)) return 0;
            if (id.EndsWith(".signature", StringComparison.Ordinal)) return 1;
            if (id.EndsWith(".budget", StringComparison.Ordinal)) return 2;
            if (id.EndsWith(".strong", StringComparison.Ordinal)) return 3;
            if (id.EndsWith(".theme", StringComparison.Ordinal)) return 4;
            if (id.EndsWith(".explore", StringComparison.Ordinal)) return 5;
            return -1;
        }

        private static string DirectionIdForVariant(string variantKind)
        {
            if (variantKind == "ConservativeReuse") return "reuse";
            if (variantKind == "HighReadability") return "signature";
            if (variantKind == "LowBudget") return "budget";
            if (variantKind == "StrongImpact") return "strong";
            if (variantKind == "ThemeVariant") return "theme";
            if (variantKind == "NewPrimitiveExploration") return "explore";
            return string.Empty;
        }

        private static void AddProvenance(VfxCandidatePlan plan, string key, string value)
        {
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = key, Value = value ?? string.Empty });
        }

        public VfxLibraryCreationPlanResult CreatePromotionPlan(
            VfxLibraryCreationBrief brief,
            VfxLibraryCreationCandidate candidate,
            string catalogHash,
            string generationPlanHash,
            IReadOnlyList<VfxCandidateGenerationResult> generationResults)
        {
            if (candidate == null || string.IsNullOrEmpty(generationPlanHash))
                return new VfxLibraryCreationPlanResult { Message = "请先生成计划并选择候选。" };

            string directionId = GetDirectionProtocolId(candidate);
            var receiptPaths = new List<string>();
            for (int i = 0; i < generationResults.Count; i++)
            {
                VfxCandidateGenerationResult result = generationResults[i];
                if (result?.Receipt == null || !result.Receipt.PlanId.EndsWith("." + directionId, StringComparison.Ordinal))
                    continue;
                receiptPaths.Add(result.ReceiptAssetPath ?? string.Empty);
            }
            receiptPaths.Sort(StringComparer.Ordinal);
            var request = new VfxLibraryPromotionViewRequest
            {
                RequestId = "vfx.editor.promotion." + SessionId + "." + directionId,
                CandidateDirectionId = directionId,
                CatalogContentHash = catalogHash ?? string.Empty,
                GenerationTemplateCatalogContentHash = TemplateCatalog.ContentHash,
                GenerationViewRequestContentHash = generationPlanHash,
                CandidateReceiptPaths = receiptPaths,
                BlockedUntil = new[]
                {
                    "authoritative-core-promotion-plan",
                    "complete-hard-gate-report",
                    "three-reviewer-visual-evaluation",
                    "target-platform-performance-report",
                    "human-vfx-approval"
                }
            };
            return WritePlan(
                PromotionPlanPath,
                request,
                "已生成受阻断的晋升视图请求；还需权威规划、完整门禁和人工审批，最高目标状态为待评审。");
        }

        public bool CleanSession(out string message)
        {
            string projectRoot = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(SessionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string expectedPrefix = projectRoot + Path.DirectorySeparatorChar + "Temp" + Path.DirectorySeparatorChar + "MxFramework" + Path.DirectorySeparatorChar + "VfxAuthoring" + Path.DirectorySeparatorChar + "CreativeLab" + Path.DirectorySeparatorChar;
            if (!fullRoot.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                message = "清理路径不在受控临时创作实验室目录内，操作已阻止。";
                return false;
            }
            int deletedCandidateSessions = 0;
            int deletedCoreEvidenceSessions = 0;
            int deletedQualityEvidenceSessions = 0;
            if (Directory.Exists(fullRoot))
                Directory.Delete(fullRoot, true);
            string candidateRoot = VfxCandidateProtocol.ResolvedSandboxRoot;
            if (Directory.Exists(candidateRoot))
            {
                string[] directories = Directory.GetDirectories(candidateRoot);
                for (int i = 0; i < directories.Length; i++)
                {
                    string name = Path.GetFileName(directories[i]);
                    if (!name.StartsWith(SessionId + "_", StringComparison.Ordinal) &&
                        !name.StartsWith("vfx-brief-fireball-", StringComparison.Ordinal))
                        continue;
                    string assetPath = VfxCandidateSandbox.NormalizeAssetPath(directories[i]);
                    if (VfxCandidateSandbox.IsCandidateRoot(assetPath) && AssetDatabase.DeleteAsset(assetPath))
                        deletedCandidateSessions++;
                }
                AssetDatabase.Refresh();
            }
            deletedCoreEvidenceSessions = DeleteEvidenceSessions(VfxCandidateProtocol.AuthoritativeEvidenceRoot, "vfx-brief-fireball-");
            deletedQualityEvidenceSessions = DeleteEvidenceSessions(VfxQualityEvidenceProtocol.OutputRoot, "vfx-brief-fireball-");
            message = "已清理临时视图请求、" + deletedCandidateSessions + " 个 Fireball 草稿候选会话、" +
                      deletedCoreEvidenceSessions + " 个 Core 证据会话和 " + deletedQualityEvidenceSessions +
                      " 个质量证据会话；正式语义目录与已发布资产未受影响。";
            return true;
        }

        private static int DeleteEvidenceSessions(string root, string sessionPrefix)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return 0;
            string normalizedRoot = VfxCandidateSandbox.NormalizeAssetPath(root).TrimEnd('/');
            int deleted = 0;
            string[] directories = Directory.GetDirectories(normalizedRoot);
            for (int i = 0; i < directories.Length; i++)
            {
                string name = Path.GetFileName(directories[i]);
                if (!name.StartsWith(sessionPrefix, StringComparison.Ordinal))
                    continue;
                string normalized = VfxCandidateSandbox.NormalizeAssetPath(directories[i]);
                if (!normalized.StartsWith(normalizedRoot + "/", StringComparison.Ordinal) ||
                    !VfxCandidateSandbox.IsControlledEvidencePath(normalized))
                    continue;
                Directory.Delete(normalized, true);
                deleted++;
            }
            return deleted;
        }

        internal static string Hash(byte[] data)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static VfxLibraryCreationBriefSnapshot BuildBrief(VfxLibraryCreationBrief brief)
        {
            return new VfxLibraryCreationBriefSnapshot
            {
                BriefName = brief.BriefName ?? string.Empty,
                Intent = brief.Intent ?? string.Empty,
                Lifecycle = brief.Lifecycle ?? string.Empty,
                Roles = StageRoles(brief.Lifecycle),
                ThemeIds = new[] { "fire" },
                StyleProfileId = brief.StyleProfileId ?? string.Empty,
                StyleProfileVersion = brief.StyleProfileVersion ?? string.Empty,
                ProviderId = brief.ProviderId ?? string.Empty,
                ProviderVersion = brief.ProviderVersion ?? string.Empty,
                ProviderKind = brief.ProviderKind ?? string.Empty,
                ProviderModelId = brief.ProviderModelId ?? string.Empty,
                ProviderModelVersion = brief.ProviderModelVersion ?? string.Empty,
                TemplateId = brief.TemplateId ?? string.Empty,
                TemplateVersion = brief.TemplateVersion ?? string.Empty,
                TemplateKind = brief.TemplateKind ?? string.Empty,
                QualityProfileId = brief.QualityProfileId ?? string.Empty,
                QualityProfileVersion = brief.QualityProfileVersion ?? string.Empty,
                FixedEvaluationSeeds = brief.FixedEvaluationSeeds == null ? Array.Empty<int>() : (int[])brief.FixedEvaluationSeeds.Clone(),
                ReferencePackage = brief.ReferencePackage ?? string.Empty,
                LicenseId = brief.LicenseId ?? string.Empty,
                ExploreNewPrimitive = brief.ExploreNewPrimitive,
                RequireFallback = brief.RequireFallback
            };
        }

        private static string[] StageRoles(string lifecycle)
        {
            string[] stageIds = SelectStageIds(lifecycle);
            var roles = new string[stageIds.Length];
            for (int i = 0; i < stageIds.Length; i++)
                roles[i] = stageIds[i] == "charge" ? "Charge" : stageIds[i] == "flight" ? "Flight" : "Impact";
            return roles;
        }

        private static List<VfxLibraryReuseViewEntry> BuildReuseDecision(IReadOnlyList<VfxLibraryReuseRecommendation> recommendations)
        {
            var result = new List<VfxLibraryReuseViewEntry>();
            for (int i = 0; i < recommendations.Count; i++)
            {
                VfxLibraryReuseRecommendation recommendation = recommendations[i];
                result.Add(new VfxLibraryReuseViewEntry
                {
                    StableId = recommendation.Item.StableId,
                    Kind = recommendation.Item.Kind.ToString(),
                    ReviewStatus = recommendation.Item.ReviewStatus,
                    Score = recommendation.Score,
                    Reason = recommendation.Reason
                });
            }
            return result;
        }

        private static string GetDirectionProtocolId(VfxLibraryCreationCandidate candidate)
        {
            if (candidate.CandidateId.Contains("conservative")) return "reuse";
            if (candidate.CandidateId.Contains("readability")) return "signature";
            if (candidate.CandidateId.Contains("low-budget")) return "budget";
            if (candidate.CandidateId.Contains("strong-impact")) return "strong";
            if (candidate.CandidateId.Contains("theme-variant")) return "theme";
            return "explore";
        }

        private static VfxLibraryCreationPlanResult WritePlan<T>(string relativePath, T value, string message)
        {
            string directory = Path.GetDirectoryName(relativePath);
            if (string.IsNullOrEmpty(directory))
                return new VfxLibraryCreationPlanResult { Message = "计划路径无效。" };
            Directory.CreateDirectory(directory);
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(value, Formatting.Indented) + Environment.NewLine);
            File.WriteAllBytes(relativePath, bytes);
            return new VfxLibraryCreationPlanResult
            {
                Success = true,
                Message = message,
                RelativePath = relativePath.Replace('\\', '/'),
                ContentHash = Hash(bytes)
            };
        }
    }

    internal sealed class VfxLibraryAuthoringStudioController
    {
        private readonly VisualElement _root;
        private readonly IVfxLibraryCreationPlanService _planService;
        private readonly VfxLibraryGenerationTemplateCatalog _templateCatalog;
        private readonly IVfxCandidateGenerator _candidateGenerator;
        private readonly IVfxPreviewCapture _previewCapture;
        private readonly IVfxVisualEvaluator _visualEvaluator;
        private readonly VfxDevelopmentAgentTaskEditorService _agentTaskService = new VfxDevelopmentAgentTaskEditorService();
        private readonly VfxAgentCandidateComparePreview _agentPreview = new VfxAgentCandidateComparePreview();
        private readonly VfxAgentReviewPromotionService _agentPromotion = new VfxAgentReviewPromotionService();
        private readonly HashSet<string> _playedAgentCandidates = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<VfxLibraryItem> _openLibraryItem;
        private readonly Action<string> _openReuseGap;
        private readonly List<VfxLibraryReuseRecommendation> _recommendations = new List<VfxLibraryReuseRecommendation>();
        private readonly List<Button> _candidateCards = new List<Button>();
        private readonly List<VisualElement> _candidatePreviews = new List<VisualElement>();
        private readonly List<Label> _candidatePreviewStates = new List<Label>();
        private readonly Texture2D[] _candidatePreviewTextures = new Texture2D[6];
        private readonly List<VfxCandidateGenerationResult> _generationResults = new List<VfxCandidateGenerationResult>();
        private readonly List<VfxLibraryCoreStageReceiptEvidence> _coreReceiptEvidence = new List<VfxLibraryCoreStageReceiptEvidence>();
        private readonly int[] _expectedPerDirection = new int[6];
        private readonly int[] _generatedPerDirection = new int[6];
        private readonly int[] _gatePassedPerDirection = new int[6];
        private readonly int[] _failedPerDirection = new int[6];

        private readonly TextField _briefName;
        private readonly TextField _targetEffectId;
        private readonly TextField _briefIntent;
        private readonly DropdownField _lifecycle;
        private readonly DropdownField _styleProfile;
        private readonly DropdownField _provider;
        private readonly DropdownField _template;
        private readonly DropdownField _qualityProfile;
        private readonly TextField _referencePackage;
        private readonly Label _referencePackStatus;
        private readonly Label _referencePackDetail;
        private readonly Button _referencePackConnect;
        private readonly Button _referencePackRescan;
        private readonly Button _referencePackOpen;
        private readonly Button _referencePackBrowse;
        private readonly Toggle _explorePrimitive;
        private readonly Toggle _requireFallback;
        private readonly VisualElement _candidateHost;
        private readonly VisualElement _reuseHost;
        private readonly VisualElement _reuseGapHost;
        private readonly Label _status;
        private readonly Label _planPath;
        private readonly Label _catalogHash;
        private readonly Label _providerStatus;
        private readonly Label _qualityContract;
        private readonly Label _qualityContractDetail;
        private readonly Label _hardGateSummary;
        private readonly Label _candidateDetail;
        private readonly Label _compareStatus;
        private readonly Button _generate;
        private readonly Button _cancel;
        private readonly Button _qualityEvidence;
        private readonly Button _clean;
        private readonly Button _promote;
        private readonly Button _openPlan;
        private readonly Button _copyAgentInstruction;
        private readonly Button _refreshAgentResult;
        private readonly IMGUIContainer _agentPreviewCanvas;
        private readonly Toggle _humanPlayedConfirmation;

        private VfxLibraryWorkspace _workspace;
        private VfxLibraryCreationCandidate _selectedCandidate;
        private VfxLibraryCreationPlanResult _generationPlan;
        private int _nextPlanIndex;
        private bool _isGenerating;
        private bool _cancelRequested;
        private bool _generationComplete;
        private VfxDevelopmentAgentTaskSession _agentSession;
        private readonly VfxReferencePackEditorService _referencePackService = new VfxReferencePackEditorService();
        private VfxReferencePackEditorStatus _referencePack;
        private double _nextAgentPollTime;

        public VfxLibraryAuthoringStudioController(
            VisualElement root,
            IVfxLibraryCreationPlanService planService,
            Action<VfxLibraryItem> openLibraryItem,
            Action<string> openReuseGap)
        {
            _root = root;
            _planService = planService;
            _templateCatalog = planService.TemplateCatalog;
            _candidateGenerator = new VfxEditorCandidateGenerator();
            _previewCapture = new VfxDeterministicPreviewCapture();
            _visualEvaluator = new VfxDefaultVisualEvaluator();
            _openLibraryItem = openLibraryItem;
            _openReuseGap = openReuseGap;
            _briefName = root.Q<TextField>("brief-name");
            _targetEffectId = root.Q<TextField>("brief-target-effect");
            _briefIntent = root.Q<TextField>("brief-intent");
            _lifecycle = root.Q<DropdownField>("brief-lifecycle");
            _styleProfile = root.Q<DropdownField>("style-profile");
            _provider = root.Q<DropdownField>("generation-provider");
            _template = root.Q<DropdownField>("certified-template");
            _qualityProfile = root.Q<DropdownField>("quality-profile");
            _referencePackage = root.Q<TextField>("reference-package");
            _referencePackStatus = root.Q<Label>("reference-pack-status");
            _referencePackDetail = root.Q<Label>("reference-pack-detail");
            _referencePackConnect = root.Q<Button>("reference-pack-connect");
            _referencePackRescan = root.Q<Button>("reference-pack-rescan");
            _referencePackOpen = root.Q<Button>("reference-pack-open");
            _referencePackBrowse = root.Q<Button>("reference-pack-browse");
            _explorePrimitive = root.Q<Toggle>("explore-primitive");
            _requireFallback = root.Q<Toggle>("require-fallback");
            _candidateHost = root.Q("candidate-host");
            _reuseHost = root.Q("reuse-host");
            _reuseGapHost = root.Q("reuse-gap-host");
            _status = root.Q<Label>("creative-status");
            _planPath = root.Q<Label>("creative-plan-path");
            _catalogHash = root.Q<Label>("creative-catalog-hash");
            _providerStatus = root.Q<Label>("creative-provider-status");
            _qualityContract = root.Q<Label>("quality-contract");
            _qualityContractDetail = root.Q<Label>("quality-contract-detail");
            _hardGateSummary = root.Q<Label>("hard-gate-summary");
            _candidateDetail = root.Q<Label>("candidate-detail");
            _compareStatus = root.Q<Label>("compare-status");
            _generate = root.Q<Button>("creative-generate");
            _cancel = root.Q<Button>("creative-cancel");
            _qualityEvidence = root.Q<Button>("creative-quality-evidence");
            _clean = root.Q<Button>("creative-clean");
            _promote = root.Q<Button>("creative-promote");
            _openPlan = root.Q<Button>("creative-open-plan");
            _copyAgentInstruction = root.Q<Button>("creative-copy-agent");
            _refreshAgentResult = root.Q<Button>("creative-refresh-agent");
            _humanPlayedConfirmation = root.Q<Toggle>("creative-human-played");
            _agentPreviewCanvas = new IMGUIContainer(DrawAgentPreview);
            _agentPreviewCanvas.name = "creative-agent-live-preview";
            _agentPreviewCanvas.style.height = 220f;
            _agentPreviewCanvas.style.marginTop = 8f;
            _candidateDetail.parent.Add(_agentPreviewCanvas);
            ConfigureFields();
            DisableUnavailableComparisonControls();
            BuildCandidateCards();
            RegisterCallbacks();
            ResetGenericStarter();
        }

        public void Dispose()
        {
            StopGenerationLoop();
            ReleaseCandidatePreviewTextures();
            _agentPreview.Dispose();
        }

        public void SetWorkspace(VfxLibraryWorkspace workspace)
        {
            _workspace = workspace;
            if (_targetEffectId != null && (string.IsNullOrWhiteSpace(_targetEffectId.value) || _targetEffectId.value == "vfx.effect.target") && workspace?.Effects.Count > 0)
                _targetEffectId.SetValueWithoutNotify(workspace.Effects[0].EffectId);
            string semantic = workspace == null ? "语义目录：未加载" : "语义目录内容哈希：" + ShortHash(workspace.CatalogHash) + " · " + workspace.SchemaVersion;
            string templates = _templateCatalog.IsLoaded
                ? "生成模板内容哈希：" + ShortHash(_templateCatalog.ContentHash) + " · 目录版本 " + _templateCatalog.CatalogVersion
                : "生成模板目录：未加载";
            _catalogHash.text = semantic + "     " + templates;
            RefreshReuseRecommendations();
        }

        public void UpdateResponsiveState(float width)
        {
            VisualElement creative = _root.Q("creative-workspace");
            if (creative == null)
                return;
            creative.EnableInClassList("creative-compact", width < 1380f);
            creative.EnableInClassList("creative-narrow", width < 1120f);
        }

        private void ConfigureFields()
        {
            SetChoices(_lifecycle, "完整生命周期：蓄力 → 飞行 → 命中", "仅蓄力", "仅飞行", "仅命中");
            SetChoices(_styleProfile, StyleDisplayNames(_templateCatalog.StyleProfiles));
            SetChoices(_provider, ProviderDisplayNames(_templateCatalog.Providers));
            SetChoices(_template, TemplateDisplayNames(_templateCatalog.Templates));
            SetChoices(_qualityProfile, QualityDisplayNames(_templateCatalog.QualityProfiles));
        }

        private void DisableUnavailableComparisonControls()
        {
            _root.Q<Button>("compare-play").SetEnabled(true);
            _root.Q<Button>("compare-pause").SetEnabled(true);
            _root.Q<Button>("compare-restart").SetEnabled(true);
            Toggle loop = _root.Q<Toggle>("compare-loop");
            loop.SetValueWithoutNotify(false);
            loop.SetEnabled(true);
            _root.Q<Slider>("compare-speed").SetEnabled(true);
            _requireFallback.SetValueWithoutNotify(false);
            _requireFallback.SetEnabled(false);
        }

        private void RegisterCallbacks()
        {
            _generate.clicked += CreateAgentTask;
            _cancel.clicked += RequestAgentCancellation;
            _qualityEvidence.clicked += GenerateQualityEvidence;
            _clean.clicked += CleanSession;
            _promote.clicked += PromoteAgentCandidate;
            _openPlan.clicked += RevealAgentTask;
            _copyAgentInstruction.clicked += CopyAgentInstruction;
            _refreshAgentResult.clicked += RefreshAgentResult;
            _root.Q<Button>("compare-play").clicked += PlayAgentPreview;
            _root.Q<Button>("compare-pause").clicked += PauseAgentPreview;
            _root.Q<Button>("compare-restart").clicked += RestartAgentPreview;
            _root.Q<Toggle>("compare-loop").RegisterValueChangedCallback(change => _agentPreview.SetLoop(change.newValue));
            _root.Q<Slider>("compare-speed").RegisterValueChangedCallback(change => _agentPreview.SetSpeed(change.newValue));
            _humanPlayedConfirmation.RegisterValueChangedCallback(_ => UpdateActions());
            _root.Q<Button>("creative-reset").clicked += ResetGenericStarter;
            _briefName.RegisterValueChangedCallback(_ => MarkBriefDirty());
            _targetEffectId.RegisterValueChangedCallback(_ => MarkBriefDirty());
            _briefIntent.RegisterValueChangedCallback(_ => MarkBriefDirty());
            _lifecycle.RegisterValueChangedCallback(_ => MarkBriefDirty());
            _styleProfile.RegisterValueChangedCallback(_ => MarkBriefDirty());
            _provider.RegisterValueChangedCallback(_ => { UpdateProviderStatus(); MarkBriefDirty(); });
            _template.RegisterValueChangedCallback(_ => { UpdateProviderStatus(); MarkBriefDirty(); });
            _qualityProfile.RegisterValueChangedCallback(_ => { UpdateProviderStatus(); MarkBriefDirty(); });
            _referencePackage.RegisterValueChangedCallback(_ => MarkBriefDirty());
            _referencePackConnect.clicked += ConnectReferencePack;
            _referencePackRescan.clicked += RescanReferencePack;
            _referencePackOpen.clicked += () => VfxReferencePackEditorService.RevealSource(_referencePack);
            _referencePackBrowse.clicked += VfxAuthoringResourceLibraryWindow.OpenManual;
            _explorePrimitive.RegisterValueChangedCallback(_ => MarkBriefDirty());
            _requireFallback.RegisterValueChangedCallback(_ => MarkBriefDirty());
        }

        private void BuildCandidateCards()
        {
            _candidateHost.Clear();
            _candidateCards.Clear();
            _candidatePreviews.Clear();
            _candidatePreviewStates.Clear();
            IReadOnlyList<VfxLibraryCreationCandidate> candidates = _planService.Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                VfxLibraryCreationCandidate candidate = candidates[i];
                var card = new Button();
                card.name = "candidate-card-" + (i + 1);
                card.AddToClassList("candidate-card");
                var index = new Label("候选 " + (i + 1).ToString("00") + "  ·  " + candidate.Accent);
                index.AddToClassList("candidate-index");
                var title = new Label(candidate.Direction);
                title.AddToClassList("candidate-title");
                var summary = new Label(candidate.Summary);
                summary.AddToClassList("candidate-summary");
                var status = new Label("等待生成计划");
                status.name = "candidate-status";
                status.AddToClassList("candidate-status");
                var score = new Label("硬门禁 —   视觉 —   性能 —");
                score.AddToClassList("candidate-score");
                var preview = new VisualElement();
                preview.AddToClassList("candidate-preview");
                var previewState = new Label("尚无真实捕获");
                previewState.AddToClassList("candidate-preview-state");
                preview.Add(previewState);
                card.Add(index);
                card.Add(preview);
                card.Add(title);
                card.Add(summary);
                card.Add(status);
                card.Add(score);
                card.clicked += () => SelectCandidate(candidate);
                _candidateHost.Add(card);
                _candidateCards.Add(card);
                _candidatePreviews.Add(preview);
                _candidatePreviewStates.Add(previewState);
            }
        }

        private void ResetGenericStarter()
        {
            StopGenerationLoop();
            ClearGenerationState();
            _agentSession = null;
            _agentPreview.Dispose();
            _briefName.SetValueWithoutNotify("未命名 VFX 创作任务");
            _targetEffectId.SetValueWithoutNotify(_workspace != null && _workspace.Effects.Count > 0 ? _workspace.Effects[0].EffectId : "vfx.effect.target");
            _briefIntent.SetValueWithoutNotify("描述目标效果的主体、辅助层、节奏、空间形态、Anchor、生命周期、主题和预算。Development Agent 必须先检索现有 Production Primitive/Recipe 与精选 Element，再补齐明确缺口。");
            SetFirstChoice(_lifecycle);
            SetFirstChoice(_styleProfile);
            SetFirstChoice(_provider);
            SetFirstChoice(_template);
            SetFirstChoice(_qualityProfile);
            _referencePackage.SetValueWithoutNotify("reference.piloto.arpg-realistic-demon-vs-angels");
            _referencePackage.SetEnabled(false);
            _explorePrimitive.SetValueWithoutNotify(true);
            _requireFallback.SetValueWithoutNotify(false);
            _planPath.text = "计划：尚未生成";
            RefreshReferencePack();
            _status.text = _referencePack.Connected
                ? "通用创作简报已就绪；授权源库连接有效，可以创建 Development Agent 任务。"
                : "请先连接授权源库，再创建 Development Agent 任务。";
            _status.EnableInClassList("status-warning", false);
            _compareStatus.text = "当前版本仅生成单帧捕获证据；同步播放与分屏动态比较尚未接入，控件已禁用。";
            UpdateProviderStatus();
            SetCandidateStatuses("等待生成计划");
            SelectCandidate(_planService.Candidates[0]);
            _agentSession = _agentTaskService.LoadLatest();
            if (_agentSession?.Task != null)
            {
                _planPath.text = "Agent 任务：" + _agentSession.TaskPath + " · " + ShortHash(_agentSession.Task.TaskHash);
                if (_agentSession.HasValidatedTask)
                {
                    BindAgentCandidateCards();
                    SelectCandidate(_planService.Candidates[0]);
                    SetStatus("已恢复最近的 Development Agent 任务：" + _agentSession.Task.TaskId + " · " + (_agentSession.Progress?.State ?? "Unknown"), false);
                }
                else
                {
                    SetCandidateStatuses("旧任务已失效 · 请重新创建");
                    SetStatus("最近的 Agent 任务已因 Catalog、资源索引或 Reference Pack 变化而失效，请重新创建。", true);
                }
            }
            UpdateActions();
        }

        public void Update()
        {
            if (_agentPreview.HasAny)
                _agentPreviewCanvas.MarkDirtyRepaint();
            if (_agentSession == null || EditorApplication.timeSinceStartup < _nextAgentPollTime)
                return;
            _nextAgentPollTime = EditorApplication.timeSinceStartup + 1.0;
            DateTime progressWrite = File.Exists(_agentSession.ProgressPath) ? File.GetLastWriteTimeUtc(_agentSession.ProgressPath) : DateTime.MinValue;
            DateTime receiptWrite = File.Exists(_agentSession.ReceiptPath) ? File.GetLastWriteTimeUtc(_agentSession.ReceiptPath) : DateTime.MinValue;
            if (progressWrite > _agentProgressWriteTime || receiptWrite > _agentReceiptWriteTime)
                RefreshAgentResult();
        }

        private DateTime _agentProgressWriteTime;
        private DateTime _agentReceiptWriteTime;

        private void DrawAgentPreview()
        {
            _agentPreview.Draw(new Rect(0f, 0f, _agentPreviewCanvas.contentRect.width, _agentPreviewCanvas.contentRect.height));
        }

        private void CreateAgentTask()
        {
            if (_workspace == null)
            {
                SetStatus("语义目录尚未加载，不能创建受当前 Catalog 约束的 Agent 任务。", true);
                return;
            }
            RefreshReferencePack();
            if (!_referencePack.Connected)
            {
                SetStatus("本机 Piloto Studio 质量参考包尚未连接或已失效；任务必须绑定 Reference Pack hash。", true);
                return;
            }
            ClearGenerationState();
            if (!_agentTaskService.TryCreate(ReadBrief(), out _agentSession, out string message))
            {
                SetStatus("创建 Development Agent 任务失败：" + message, true);
                SetCandidateStatuses("任务创建失败");
                UpdateActions();
                return;
            }
            _planPath.text = "Agent 任务：" + _agentSession.TaskPath + " · " + ShortHash(_agentSession.Task.TaskHash);
            SetStatus(message + " 请复制 Agent 指令并交给当前 Codex 任务执行。", false);
            SetCandidateStatuses("任务已创建 · 等待 Development Agent 接管");
            _compareStatus.text = "尚无通过校验的真实候选；实时播放将在 Agent 回执有效后启用。";
            _nextAgentPollTime = 0d;
            RefreshAgentResult();
            UpdateActions();
        }

        private void ConnectReferencePack()
        {
            string initial = _referencePack != null && Directory.Exists(_referencePack.SourceRoot)
                ? _referencePack.SourceRoot
                : string.Empty;
            string selected = EditorUtility.OpenFolderPanel("选择 ARPG_Realistic Demon VS Angels 参考包", initial, string.Empty);
            if (string.IsNullOrWhiteSpace(selected)) return;
            bool success = _referencePackService.Connect(selected, out string message);
            RefreshReferencePack();
            SetStatus(success ? "参考包已连接并完成只读扫描。" : "连接参考包失败：" + message, !success);
            MarkBriefDirty();
        }

        private void RescanReferencePack()
        {
            bool success = _referencePackService.Rescan(out string message);
            RefreshReferencePack();
            SetStatus(success ? "参考包已重新扫描；内容变化会使旧 Agent 任务自动过期。" : "重新扫描失败：" + message, !success);
            MarkBriefDirty();
        }

        private void RefreshReferencePack()
        {
            _referencePack = _referencePackService.Load();
            VfxReferencePackManifestView manifest = _referencePack.Manifest;
            _referencePackStatus.text = _referencePack.Message;
            _referencePackStatus.EnableInClassList("connected", _referencePack.Connected);
            _referencePackDetail.text = manifest == null
                ? "ReferenceOnly · 本机路径和扫描证据只保存在 Temp"
                : "ReferenceOnly · " + manifest.PrefabCount + " Prefab · " + manifest.DependencyCount + " 依赖 · " +
                  manifest.MaterialCount + " 材质 · " + manifest.TextureCount + " 贴图 · " + manifest.MeshCount +
                  " Mesh · Unity " + manifest.SourceUnityVersion + " · URP " + manifest.SourceUrpVersion + " · " + ShortHash(manifest.ManifestHash);
            _referencePackRescan.SetEnabled(_referencePack.Connected);
            _referencePackOpen.SetEnabled(_referencePack.Connected);
            _referencePackBrowse.SetEnabled(_referencePack.Connected);
        }

        private void CopyAgentInstruction()
        {
            if (_agentSession?.Task == null) return;
            EditorGUIUtility.systemCopyBuffer = VfxDevelopmentAgentTaskEditorService.BuildInstruction(_agentSession);
            SetStatus("已复制 Development Agent 指令；指令绑定当前 task.json 并要求通过 Unity MCP 工作。", false);
        }

        private void RevealAgentTask()
        {
            if (_agentSession == null || !Directory.Exists(_agentSession.DirectoryPath))
            {
                SetStatus("当前没有可打开的 Agent 任务目录。", true);
                return;
            }
            EditorUtility.RevealInFinder(Path.GetFullPath(_agentSession.DirectoryPath));
        }

        private void RequestAgentCancellation()
        {
            if (_agentSession == null) return;
            bool success = _agentTaskService.RequestCancel(_agentSession, out string message);
            SetStatus(success ? "已写入取消请求；Agent 将在安全边界停止并更新状态。" : "取消请求失败：" + message, !success);
            RefreshAgentResult();
        }

        private void RefreshAgentResult()
        {
            if (_agentSession == null) return;
            _agentSession = _agentTaskService.Load(_agentSession.DirectoryPath);
            if (_agentSession == null) return;
            _agentProgressWriteTime = File.Exists(_agentSession.ProgressPath) ? File.GetLastWriteTimeUtc(_agentSession.ProgressPath) : DateTime.MinValue;
            _agentReceiptWriteTime = File.Exists(_agentSession.ReceiptPath) ? File.GetLastWriteTimeUtc(_agentSession.ReceiptPath) : DateTime.MinValue;
            VfxAgentProgressView progress = _agentSession.Progress;
            string state = progress?.State ?? "Unknown";
            _status.text = "Agent 状态：" + state + " · " + (progress?.CompletedCandidates ?? 0) + "/6 · " + (progress?.Message ?? "尚无进度");
            BindAgentCandidateCards();
            UpdateActions();
        }

        private void BindAgentCandidateCards()
        {
            for (int i = 0; i < _candidateCards.Count; i++)
            {
                VfxAgentTaskCandidateView work = _agentSession?.Task?.Candidates != null && i < _agentSession.Task.Candidates.Count ? _agentSession.Task.Candidates[i] : null;
                VfxAgentCandidateReceiptView receipt = FindAgentReceipt(work?.CandidateId);
                Label status = _candidateCards[i].Q<Label>("candidate-status");
                if (receipt == null)
                {
                    status.text = work == null ? "任务数据缺失" : "等待 Agent 创作 · " + work.Direction;
                    continue;
                }
                status.text = (_agentSession.HasValidatedReceipt ? "回执已校验" : "回执待校验") + " · 迭代 " + receipt.IterationCount + " · 复用 " + receipt.ReusedAssets.Count + " · 新增 " + receipt.CreatedAssets.Count;
                if (receipt.Captures.Count > 0)
                    LoadAgentCapture(i, receipt.Captures[receipt.Captures.Count - 1].AssetPath);
            }
            _generationComplete = _agentSession?.HasValidatedReceipt == true && _agentSession.Receipt?.Candidates?.Count == 6;
            _compareStatus.text = _generationComplete
                ? "六个真实候选回执已通过校验。点击两张候选卡加入 A/B，再使用上方控件同步播放。"
                : "仅展示 Agent 当前进度；回执通过 hash、provenance 与 Unity 序列化校验前不能评审或晋升。";
        }

        private VfxAgentCandidateReceiptView FindAgentReceipt(string candidateId)
        {
            List<VfxAgentCandidateReceiptView> receipts = _agentSession?.Receipt?.Candidates;
            if (receipts == null) return null;
            for (int i = 0; i < receipts.Count; i++)
                if (string.Equals(receipts[i]?.CandidateId, candidateId, StringComparison.Ordinal)) return receipts[i];
            return null;
        }

        private void LoadAgentCapture(int index, string path)
        {
            if (index < 0 || index >= _candidatePreviews.Count || string.IsNullOrWhiteSpace(path)) return;
            string full = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            if (!File.Exists(full)) return;
            try
            {
                ReleaseCandidatePreviewTexture(index);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (!texture.LoadImage(File.ReadAllBytes(full))) { UnityEngine.Object.DestroyImmediate(texture); return; }
                _candidatePreviewTextures[index] = texture;
                _candidatePreviews[index].style.backgroundImage = new StyleBackground(texture);
                _candidatePreviewStates[index].text = "Agent 真实捕获";
            }
            catch (Exception exception) { UnityEngine.Debug.LogWarning("Cannot load VFX Agent capture: " + exception.Message); }
        }

        private void PlayAgentPreview()
        {
            _agentPreview.Play();
            VfxAgentCandidateReceiptView receipt = SelectedAgentReceipt(out _);
            if (receipt != null) _playedAgentCandidates.Add(receipt.CandidateId);
            _compareStatus.text = "A/B 候选同步播放中；当前候选已记录人工运行操作。";
            UpdateActions();
        }
        private void PauseAgentPreview() { _agentPreview.Pause(); _compareStatus.text = "A/B 候选已暂停"; }
        private void RestartAgentPreview() { _agentPreview.Restart(); _compareStatus.text = "A/B 候选已从固定种子重新播放"; }

        private VfxAgentCandidateReceiptView SelectedAgentReceipt(out VfxAgentTaskCandidateView work)
        {
            work = null;
            if (_selectedCandidate == null || _agentSession?.Task?.Candidates == null) return null;
            for (int i = 0; i < _planService.Candidates.Count && i < _agentSession.Task.Candidates.Count; i++)
            {
                if (!ReferenceEquals(_planService.Candidates[i], _selectedCandidate)) continue;
                work = _agentSession.Task.Candidates[i];
                return FindAgentReceipt(work.CandidateId);
            }
            return null;
        }

        private void PromoteAgentCandidate()
        {
            VfxAgentCandidateReceiptView receipt = SelectedAgentReceipt(out VfxAgentTaskCandidateView work);
            if (_workspace == null || receipt == null || work == null || _agentSession?.HasValidatedReceipt != true)
            {
                SetStatus("必须先取得通过校验的 Agent 回执并选择一个真实候选。", true);
                return;
            }
            if (!_playedAgentCandidates.Contains(receipt.CandidateId) || !_humanPlayedConfirmation.value)
            {
                SetStatus("请先播放当前候选，并勾选“已人工运行检查当前候选”。", true);
                return;
            }
            if (!EditorUtility.DisplayDialog("确认晋升为待评审", "候选：" + receipt.CandidateId + "\n任务：" + _agentSession.Task.TaskId + "\n\n该操作会移动候选目录并向语义 Catalog 登记 ReviewNeeded Primitive；不会授予 Ready。", "确认晋升", "取消"))
                return;
            string targetRoot = "Assets/VFX/MxFramework/Review/AgentCandidates/" + _agentSession.Task.TaskId + "/" + receipt.CandidateId;
            VfxAgentHumanReviewCapability authority = VfxAgentHumanReviewAuthority.Issue(_agentSession, receipt, _workspace.CatalogHash, targetRoot);
            VfxAgentReviewPromotionResult result = _agentPromotion.Promote(_agentSession, work, receipt, authority);
            SetStatus(result.Message, !result.Success);
            if (result.Success)
            {
                _agentPreview.Dispose();
                _humanPlayedConfirmation.SetValueWithoutNotify(false);
                _playedAgentCandidates.Remove(receipt.CandidateId);
                _generationComplete = false;
                _compareStatus.text = "所选候选已登记为待评审 Primitive；请点击顶部刷新重新加载 Catalog。";
                _planPath.text = "ReviewNeeded：" + result.PrimitiveId + " · " + result.DestinationPrefabPath;
            }
            UpdateActions();
        }

        private void GeneratePlan()
        {
            if (_isGenerating)
                return;
            if (_workspace == null)
            {
                SetStatus("语义目录尚未加载，无法生成受哈希约束的请求。", true);
                return;
            }
            try
            {
                ClearGenerationState();
                _generationPlan = _planService.CreateGenerationPlan(ReadBrief(), _workspace.CatalogHash, _recommendations);
                SetStatus(_generationPlan.Message, !_generationPlan.Success);
                _planPath.text = _generationPlan.Success
                    ? "视图请求：" + _generationPlan.RelativePath + " · " + ShortHash(_generationPlan.ContentHash)
                    : "视图请求：生成失败";
                if (_generationPlan.CanExecuteCandidates && _generationPlan.PreparedPlans.Count > 0)
                {
                    BeginGenerationLoop();
                }
                else
                {
                    SetCandidateStatuses(_generationPlan.Success ? "请求就绪 · 执行条件未满足" : "请求生成失败");
                    _compareStatus.text = "没有真实候选资产，同步比较不可用。";
                }
            }
            catch (Exception exception)
            {
                _generationPlan = null;
                SetStatus("生成视图请求失败：" + exception.Message, true);
                SetCandidateStatuses("请求生成失败");
            }
            UpdateActions();
        }

        private void CancelGeneration()
        {
            if (!_isGenerating)
                return;
            _cancelRequested = true;
            SetStatus("正在完成当前候选并取消后续调度；已生成的草稿仍保留在隔离候选区。", false);
            _cancel.SetEnabled(false);
        }

        private void FinishCancelledGeneration()
        {
            StopGenerationLoop();
            _generationComplete = false;
            SetStatus("候选生成已取消；临时视图请求和已生成草稿保留用于审计，可单独清理。", false);
            _compareStatus.text = "生成未完成，不能进入同步比较或晋升规划。";
            UpdateActions();
        }

        private void CleanSession()
        {
            if (_isGenerating)
                return;
            if (!EditorUtility.DisplayDialog("清理 Development Agent 创作会话", "只删除受控临时视图请求和候选草稿，不会修改语义目录、手动 Composition Draft 或已发布资产。", "清理", "取消"))
                return;
            bool success = _planService.CleanSession(out string message);
            ClearGenerationState();
            _planPath.text = "计划：尚未生成";
            SetCandidateStatuses("等待生成计划");
            SetStatus(message, !success);
            UpdateActions();
        }

        private void CreatePromotionPlan()
        {
            if (_workspace == null || _generationPlan == null || !_generationPlan.Success || !_generationComplete || _selectedCandidate == null)
                return;
            try
            {
                VfxLibraryCreationPlanResult result = _planService.CreatePromotionPlan(
                    ReadBrief(),
                    _selectedCandidate,
                    _workspace.CatalogHash,
                    _generationPlan.ContentHash,
                    _generationResults);
                SetStatus(result.Message, !result.Success);
                if (result.Success)
                    _planPath.text = "晋升视图请求：" + result.RelativePath + " · " + ShortHash(result.ContentHash);
            }
            catch (Exception exception)
            {
                SetStatus("晋升视图请求生成失败：" + exception.Message, true);
            }
        }

        private void RevealPlan()
        {
            string path = _generationPlan?.RelativePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SetStatus("临时视图请求不存在，请先生成。", true);
                return;
            }
            EditorUtility.RevealInFinder(Path.GetFullPath(path));
        }

        private void RefreshReuseRecommendations()
        {
            _reuseHost.Clear();
            _reuseGapHost.Clear();
            _recommendations.Clear();
            if (_workspace == null)
            {
                _reuseHost.Add(new Label("语义目录未加载"));
                return;
            }
            List<VfxLibraryReuseRecommendation> recommendations = _workspace.FindReuseRecommendations((_briefName?.value ?? string.Empty) + " " + (_briefIntent?.value ?? string.Empty));
            _recommendations.AddRange(recommendations);
            bool hasReady = false;
            for (int i = 0; i < recommendations.Count; i++)
            {
                VfxLibraryReuseRecommendation recommendation = recommendations[i];
                hasReady |= recommendation.Item.ReviewStatus == "Ready" && recommendation.Item.VisualMaturity == "Production";
                var row = new Button();
                row.AddToClassList("reuse-row");
                var title = new Label(recommendation.Item.DisplayName + "  ·  " + recommendation.Item.KindLabel);
                title.AddToClassList("reuse-title");
                var id = new Label(recommendation.Item.StableId);
                id.AddToClassList("reuse-id");
                var reason = new Label(recommendation.Reason);
                reason.AddToClassList("reuse-reason");
                row.Add(title);
                row.Add(id);
                row.Add(reason);
                row.clicked += () => _openLibraryItem(recommendation.Item);
                _reuseHost.Add(row);
            }
            Label gate = _root.Q<Label>("reuse-gate");
            gate.text = hasReady
                ? "已找到 Production 已就绪样板；Development Agent 必须优先复用。"
                : "当前查询没有 Production Ready 样板；以下条目只能提供合同或评审证据，不能自动成为视觉依赖。";
            gate.EnableInClassList("status-warning", !hasReady);

            if (_workspace.ReuseGaps.Count == 0)
            {
                var empty = new Label("当前每个效果角色都已有至少一个已就绪复用入口。");
                empty.AddToClassList("reuse-gap-detail");
                _reuseGapHost.Add(empty);
                return;
            }
            for (int i = 0; i < _workspace.ReuseGaps.Count; i++)
            {
                VfxLibraryReuseGap gap = _workspace.ReuseGaps[i];
                var row = new Button();
                row.AddToClassList("reuse-gap-row");
                var title = new Label(VfxLibraryLabels.Display(gap.Role) + " · " + gap.EffectCount + " 个发布效果");
                title.AddToClassList("reuse-gap-title");
                var detail = new Label("已就绪复用入口 " + gap.ReadyReusableCount + " · 待策展候选 " + gap.ReviewCandidateCount + "\n" + gap.Reason);
                detail.AddToClassList("reuse-gap-detail");
                row.Add(title);
                row.Add(detail);
                row.clicked += () => _openReuseGap?.Invoke(gap.Role);
                _reuseGapHost.Add(row);
            }
        }

        private void SelectCandidate(VfxLibraryCreationCandidate candidate)
        {
            _selectedCandidate = candidate;
            int selectedIndex = -1;
            for (int i = 0; i < _candidateCards.Count; i++)
            {
                bool selected = ReferenceEquals(_planService.Candidates[i], candidate);
                _candidateCards[i].EnableInClassList("selected", selected);
                if (selected) selectedIndex = i;
            }
            _candidateDetail.text = candidate.Direction + "\n" + candidate.Summary + "\n\n晋升边界：自动流程最高只能生成待评审请求，已就绪状态必须由授权人员明确审批。";
            if (selectedIndex >= 0 && _agentSession?.Task?.Candidates != null && selectedIndex < _agentSession.Task.Candidates.Count)
            {
                VfxAgentCandidateReceiptView receipt = FindAgentReceipt(_agentSession.Task.Candidates[selectedIndex].CandidateId);
                if (receipt != null && _agentSession.HasValidatedReceipt && _agentPreview.SelectNext(receipt.PrefabAssetPath, candidate.Direction))
                {
                    _candidateDetail.text = candidate.Direction + "\n" + receipt.SelectionRationale + "\n\nPrefab：" + receipt.PrefabAssetPath +
                                            "\n复用资产：" + receipt.ReusedAssets.Count + " · 新增资产：" + receipt.CreatedAssets.Count + " · 迭代：" + receipt.IterationCount;
                    _compareStatus.text = "已将“" + candidate.Direction + "”加入 A/B 实时预览。";
                }
            }
            UpdateActions();
        }

        private void SetCandidateStatuses(string text)
        {
            for (int i = 0; i < _candidateCards.Count; i++)
                _candidateCards[i].Q<Label>("candidate-status").text = text;
        }

        private void MarkBriefDirty()
        {
            if (_agentSession != null)
            {
                _agentSession = null;
                _generationComplete = false;
                _agentPreview.Dispose();
                SetCandidateStatuses("创作简报已变化 · 旧 Agent 任务仍保留审计但不能继续用于当前评审");
                _planPath.text = "Agent 任务：已过期，请重新创建";
                SetStatus("创作简报已变化；需要创建绑定新输入 hash 的 Agent 任务。", true);
            }
            if (_generationPlan != null)
            {
                StopGenerationLoop();
                _generationComplete = false;
                _generationPlan = null;
                _generationResults.Clear();
                _coreReceiptEvidence.Clear();
                ClearCandidateCaptures();
                SetCandidateStatuses("创作简报已变化 · 请求已过期");
                _planPath.text = "计划：已过期，请重新生成";
                SetStatus("创作简报已变化，先前视图请求不再可用于晋升。", true);
            }
            UpdateActions();
        }

        private void UpdateProviderStatus()
        {
            if (!_templateCatalog.IsLoaded)
            {
                _providerStatus.text = _templateCatalog.Error;
                _qualityContract.text = "质量档未加载";
                _qualityContractDetail.text = "无法显示固定评测合同";
                _hardGateSummary.text = "版本化硬门禁未加载";
                _providerStatus.EnableInClassList("status-warning", true);
                return;
            }
            VfxLibraryGenerationProviderOption provider = _templateCatalog.FindProviderByDisplayName(_provider.value);
            VfxLibraryGenerationTemplateOption template = _templateCatalog.FindTemplateByDisplayName(_template.value);
            VfxLibraryQualityProfileOption quality = _templateCatalog.FindQualityByDisplayName(_qualityProfile.value);
            bool executable = provider != null && template != null && quality != null && provider.EnabledByDefault &&
                provider.ProviderId == "mx.vfx.provider.development-agent" && template.TemplateId == "mx.vfx.template.particle.fireball" &&
                template.Certified && !template.ArbitraryTopology && template.AllowsProvider(provider.ProviderId) && quality.FixedSeeds.Length > 0;
            if (executable)
                _providerStatus.text = "当前执行链：" + provider.ProviderId + "@" + provider.ProviderVersion + " → " + template.TemplateId + "@" + template.TemplateVersion + " → " + quality.QualityProfileId + "@" + quality.Version + "。该粒子系统模板本身就是降级后端，成对 fallback 强制项不适用。";
            else if (provider != null && !provider.EnabledByDefault)
                _providerStatus.text = "提供器 " + provider.ProviderId + " 默认关闭。当前 UI 尚未接入仓库外 session-capabilities 文件，不会静默回退到程序生成器；生成将失败。";
            else
                _providerStatus.text = "当前选择只生成临时视图请求；模板不受当前 Fireball 执行器支持或质量档缺少固定种子。不会静默切换提供器。";
            _requireFallback.SetValueWithoutNotify(false);
            _requireFallback.SetEnabled(false);
            if (quality != null)
            {
                _qualityContract.text = FormatFixedStep(quality.FixedStepSeconds) + " 秒固定步长 · 固定种子 " + JoinInts(quality.FixedSeeds);
                _qualityContractDetail.text = "一次性效果 " + quality.OneShotSlicePercents.Length + " 个时间切片 · " + quality.GhostCycleCount + " 次播放/停止残影检查";
                _hardGateSummary.text = "0 错误 / 洋红材质 / 非数值 / 缺帧 · 裁切 ≤ " + quality.MaximumCropPercent + "% · 锚点 ≤ " + quality.MaximumAnchorErrorPercent + "% · 预告范围交并比 ≥ " + quality.MinimumTelegraphFootprintIou + " · 稳态 GC " + quality.MaximumSteadyStateGcBytesPerFrame + " 字节/帧";
            }
            else
            {
                _qualityContract.text = "质量档未选择";
                _qualityContractDetail.text = "无法显示固定评测合同";
                _hardGateSummary.text = "版本化硬门禁未加载";
            }
            _providerStatus.EnableInClassList("status-warning", !executable);
        }

        private void UpdateActions()
        {
            bool hasPlan = _generationPlan != null && _generationPlan.Success;
            bool configurationLoaded = _templateCatalog.IsLoaded &&
                _templateCatalog.FindStyleByDisplayName(_styleProfile.value) != null &&
                _templateCatalog.FindProviderByDisplayName(_provider.value) != null &&
                _templateCatalog.FindTemplateByDisplayName(_template.value) != null &&
                _templateCatalog.FindQualityByDisplayName(_qualityProfile.value) != null;
            bool hasAgentTask = _agentSession?.Task != null;
            string agentState = _agentSession?.Progress?.State ?? string.Empty;
            bool agentTerminal = agentState == "Completed" || agentState == "Blocked" || agentState == "Cancelled" || agentState == "CancelRequested";
            _generate.SetEnabled(!_isGenerating && configurationLoaded);
            _cancel.SetEnabled(hasAgentTask && !agentTerminal);
            _copyAgentInstruction.SetEnabled(hasAgentTask);
            _refreshAgentResult.SetEnabled(hasAgentTask);
            _qualityEvidence.SetEnabled(false);
            _clean.SetEnabled(!_isGenerating && (Directory.Exists(VfxLibraryTempCreationPlanService.SessionRoot) || Directory.Exists(VfxCandidateProtocol.ResolvedSandboxRoot)));
            VfxAgentCandidateReceiptView selectedReceipt = SelectedAgentReceipt(out _);
            bool playedAndConfirmed = selectedReceipt != null && _playedAgentCandidates.Contains(selectedReceipt.CandidateId) && _humanPlayedConfirmation.value;
            _promote.SetEnabled(!_isGenerating && hasAgentTask && _generationComplete && playedAndConfirmed);
            _openPlan.SetEnabled(hasAgentTask && Directory.Exists(_agentSession.DirectoryPath));
            _generate.text = "创建 Agent 任务";
        }

        private void BeginGenerationLoop()
        {
            _nextPlanIndex = 0;
            _cancelRequested = false;
            _generationComplete = false;
            _isGenerating = true;
            Array.Clear(_expectedPerDirection, 0, _expectedPerDirection.Length);
            for (int i = 0; i < _generationPlan.PreparedPlans.Count; i++)
            {
                int directionIndex = VfxLibraryTempCreationPlanService.DirectionIndexForPlan(_generationPlan.PreparedPlans[i]);
                if (directionIndex >= 0) _expectedPerDirection[directionIndex]++;
            }
            SetCandidateStatuses("等待 Unity 编辑器执行器");
            for (int i = 0; i < _candidateCards.Count; i++) UpdateDirectionCard(i);
            _compareStatus.text = "正在逐个生成真实草稿并更新每个方向的最新单帧捕获；动态同步比较仍禁用。";
            EditorApplication.update -= ExecuteNextCandidate;
            EditorApplication.update += ExecuteNextCandidate;
            UpdateActions();
        }

        private void ExecuteNextCandidate()
        {
            if (!_isGenerating || _generationPlan == null)
            {
                StopGenerationLoop();
                return;
            }
            if (_cancelRequested)
            {
                FinishCancelledGeneration();
                return;
            }
            if (_nextPlanIndex >= _generationPlan.PreparedPlans.Count)
            {
                FinishGenerationLoop();
                return;
            }

            int currentIndex = _nextPlanIndex;
            VfxCandidatePlan plan = _generationPlan.PreparedPlans[currentIndex];
            int directionIndex = VfxLibraryTempCreationPlanService.DirectionIndexForPlan(plan);
            VfxCandidateGenerationResult generation = _candidateGenerator.Generate(plan);
            _generationResults.Add(generation);
            if (generation.Success)
            {
                if (directionIndex >= 0) _generatedPerDirection[directionIndex]++;
                VfxPreviewCaptureResult capture = CaptureGeneratedCandidate(plan, generation);
                if (directionIndex >= 0)
                {
                    UpdateCandidateCapture(directionIndex, plan, capture);
                    if (capture.Success && _visualEvaluator.Evaluate(plan, capture).PassedHardGates)
                        _gatePassedPerDirection[directionIndex]++;
                }
            }
            else if (directionIndex >= 0)
            {
                _failedPerDirection[directionIndex]++;
                UpdateCandidateCaptureFailure(directionIndex, "生成失败");
            }
            _nextPlanIndex++;
            if (directionIndex >= 0) UpdateDirectionCard(directionIndex);
            SetStatus(
                "正在生成 Fireball 草稿候选：" + _nextPlanIndex + "/" + _generationPlan.PreparedPlans.Count +
                (generation.Success ? " · 当前回执有效" : " · 当前失败：" + generation.Message),
                !generation.Success);
            UpdateActions();
        }

        private VfxPreviewCaptureResult CaptureGeneratedCandidate(VfxCandidatePlan plan, VfxCandidateGenerationResult generation)
        {
            string prefabPath = FindReceiptAsset(generation.Receipt, "Prefab");
            if (string.IsNullOrEmpty(prefabPath))
                return new VfxPreviewCaptureResult { Message = "生成回执没有 Prefab。" };
            return _previewCapture.Capture(new VfxPreviewCaptureRequest
            {
                SessionId = plan.SessionId,
                CandidateId = string.IsNullOrEmpty(plan.CoreCandidateId) ? plan.PlanId : plan.CoreCandidateId,
                CaptureId = "seed-" + plan.FixedSeed + "-single",
                ViewId = "Gameplay",
                BackgroundId = "DarkNeutral",
                TimeSliceId = TimeSliceId(plan),
                PrefabAssetPath = prefabPath,
                FixedSeed = plan.FixedSeed,
                SampleTimeSeconds = GetSampleTime(plan),
                Width = 256,
                Height = 256
            });
        }

        private void FinishGenerationLoop()
        {
            StopGenerationLoop();
            int failures = 0;
            int generated = 0;
            int preliminaryPasses = 0;
            for (int i = 0; i < CandidateDefinitionsCount; i++)
            {
                failures += _failedPerDirection[i];
                generated += _generatedPerDirection[i];
                preliminaryPasses += _gatePassedPerDirection[i];
                UpdateDirectionCard(i);
            }
            bool localGenerationComplete = failures == 0 && generated == _generationPlan.PreparedPlans.Count;
            _generationComplete = false;
            _coreReceiptEvidence.Clear();
            var exported = new List<VfxLibraryCoreStageReceiptEvidence>();
            string exportMessage = string.Empty;
            if (localGenerationComplete && VfxLibraryCoreReceiptExporter.TryExport(
                    GetAuthoritativeStagePlans(),
                    _generationResults,
                    out exported,
                    out exportMessage))
            {
                _coreReceiptEvidence.AddRange(exported);
                _generationComplete = true;
                SetStatus("已生成 " + generated + " 个 Fireball 草稿；" + preliminaryPasses + " 个通过单帧预检。" + exportMessage + " 完整硬门禁与人工审批仍未完成。", false);
                _compareStatus.text = "真实候选与固定 Core 回执已生成；卡片显示每个方向的最新单帧捕获。同步播放与分屏动态比较尚未接入。";
            }
            else if (localGenerationComplete)
            {
                SetStatus("Unity 草稿已生成，但固定 Core manifest/receipt 导出失败，质量评测与晋升链已阻断：" + exportMessage, true);
                _compareStatus.text = "候选缺少有效的固定 Core 回执，不能进入质量评测或晋升规划。";
            }
            else
            {
                SetStatus("候选生成结束，但有 " + failures + " 个失败；请查看 Console，清理本地会话后重试。", true);
                _compareStatus.text = "候选不完整，不能进入同步比较或晋升规划。";
            }
            UpdateActions();
        }

        private void GenerateQualityEvidence()
        {
            if (_isGenerating || !_generationComplete || _coreReceiptEvidence.Count == 0)
            {
                SetStatus("必须先完成全部候选并导出固定 Core 回执，才能生成质量证据。", true);
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "生成 VFX 质量证据",
                    "将对全部阶段候选运行版本化固定步长捕获矩阵。当前没有目标平台性能提供器，因此性能门禁会明确记录为未测量，报告保持 fail-closed，不会批准已就绪。",
                    "生成证据",
                    "取消"))
                return;

            _qualityEvidence.SetEnabled(false);
            try
            {
                bool allComplete = true;
                var outputPaths = new List<string>();
                using var runner = new VfxQualityEvidenceCaptureRunner(
                    new VfxUnityQualityCaptureBackend(),
                    new VfxNotMeasuredQualityMeasurementProvider());
                for (int i = 0; i < _coreReceiptEvidence.Count; i++)
                {
                    VfxLibraryCoreStageReceiptEvidence receiptEvidence = _coreReceiptEvidence[i];
                    VfxLibraryAuthoritativeStagePlan stage = FindAuthoritativeStage(receiptEvidence.StageId);
                    if (stage == null || receiptEvidence.Export == null || !receiptEvidence.Export.Success)
                    {
                        SetStatus("质量证据生成已阻止：阶段 " + receiptEvidence.StageId + " 缺少有效的 Core 计划或回执。", true);
                        return;
                    }
                    VfxQualityCaptureResult result = runner.Capture(new VfxQualityCaptureRequest
                    {
                        GenerationPlanPath = receiptEvidence.GenerationPlanPath,
                        CandidateReceiptPath = receiptEvidence.Export.ReceiptAssetPath,
                        GenerationTemplateCatalogPath = VfxCandidateProtocol.GenerationTemplateCatalogPath,
                        CandidateContexts = BuildQualityContexts(stage)
                    });
                    if (result == null || !result.Success)
                    {
                        SetStatus("阶段 " + receiptEvidence.StageId + " 的质量证据生成失败并保持阻断：" + (result?.Message ?? "未知错误"), true);
                        return;
                    }
                    allComplete &= result.Complete;
                    outputPaths.Add(result.EvidenceManifestPath);
                }

                string firstPath = outputPaths.Count == 0 ? string.Empty : outputPaths[0];
                _planPath.text = "质量证据：" + outputPaths.Count + " 个阶段 · " + firstPath;
                SetStatus(
                    allComplete
                        ? "质量证据矩阵已完整生成；仍需三人视觉评审和人工审批，不能自动进入已就绪。"
                        : "质量证据已生成，但目标平台 CPU/GPU/GC 性能为未测量，完整门禁保持 fail-closed；不能晋升。",
                    true);
            }
            catch (Exception exception)
            {
                SetStatus("质量证据生成失败并保持阻断：" + exception.Message, true);
            }
            finally
            {
                UpdateActions();
            }
        }

        private VfxLibraryAuthoritativeStagePlan FindAuthoritativeStage(string stageId)
        {
            IReadOnlyList<VfxLibraryAuthoritativeStagePlan> stages = GetAuthoritativeStagePlans();
            for (int i = 0; i < stages.Count; i++)
                if (string.Equals(stages[i].StageId, stageId, StringComparison.Ordinal))
                    return stages[i];
            return null;
        }

        internal static List<VfxQualityCandidateContext> BuildQualityContexts(VfxLibraryAuthoritativeStagePlan stage)
        {
            var result = new List<VfxQualityCandidateContext>();
            if (stage?.GenerationPlan?.Candidates == null)
                return result;
            bool isCharge = string.Equals(stage.StageId, "charge", StringComparison.Ordinal);
            bool isFlight = string.Equals(stage.StageId, "flight", StringComparison.Ordinal);
            for (int i = 0; i < stage.GenerationPlan.Candidates.Count; i++)
            {
                VfxCoreGenerationCandidateSpec candidate = stage.GenerationPlan.Candidates[i];
                result.Add(new VfxQualityCandidateContext
                {
                    CandidateId = candidate.CandidateId,
                    LifecycleMode = isCharge || isFlight ? "Loop" : "OneShot",
                    DurationSeconds = isCharge ? 1.5f : isFlight ? 1.2f : 1f,
                    TargetScaleMeters = isFlight ? 6f : isCharge ? 2.5f : 4f,
                    RequiresPathMeasurement = isFlight,
                    ExpectedPathStart = Vector3.zero,
                    ExpectedPathEnd = isFlight ? Vector3.forward * 6f : Vector3.forward,
                    IncludePathView = isFlight,
                    IncludeTopView = isCharge || !isFlight
                });
            }
            return result;
        }

        private void StopGenerationLoop()
        {
            EditorApplication.update -= ExecuteNextCandidate;
            _isGenerating = false;
            _cancelRequested = false;
        }

        private void ClearGenerationState()
        {
            _generationPlan = null;
            _generationResults.Clear();
            _coreReceiptEvidence.Clear();
            _generationComplete = false;
            _nextPlanIndex = 0;
            Array.Clear(_generatedPerDirection, 0, _generatedPerDirection.Length);
            Array.Clear(_gatePassedPerDirection, 0, _gatePassedPerDirection.Length);
            Array.Clear(_failedPerDirection, 0, _failedPerDirection.Length);
            Array.Clear(_expectedPerDirection, 0, _expectedPerDirection.Length);
            ClearCandidateCaptures();
            for (int i = 0; i < _candidateCards.Count; i++)
            {
                _candidateCards[i].EnableInClassList("candidate-failed", false);
                _candidateCards[i].EnableInClassList("candidate-generated", false);
                _candidateCards[i].Q<Label>(className: "candidate-score").text = "单帧预检 —   完整门禁 —   人工评审 —";
            }
        }

        private IReadOnlyList<VfxLibraryAuthoritativeStagePlan> GetAuthoritativeStagePlans()
        {
            return _generationPlan?.AuthoritativeStagePlans ?? (IReadOnlyList<VfxLibraryAuthoritativeStagePlan>)Array.Empty<VfxLibraryAuthoritativeStagePlan>();
        }

        private void UpdateDirectionCard(int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= _candidateCards.Count)
                return;
            Button card = _candidateCards[directionIndex];
            Label status = card.Q<Label>("candidate-status");
            Label score = card.Q<Label>(className: "candidate-score");
            int processed = _generatedPerDirection[directionIndex] + _failedPerDirection[directionIndex];
            int expected = _expectedPerDirection[directionIndex];
            status.text = expected == 0
                ? "创作简报未选择此方向"
                : processed + "/" + expected + " 已处理" + (_failedPerDirection[directionIndex] > 0 ? " · " + _failedPerDirection[directionIndex] + " 失败" : string.Empty);
            score.text = expected == 0
                ? "单帧预检 —   完整门禁 —   人工评审 —"
                : "单帧预检 " + _gatePassedPerDirection[directionIndex] + "/" + expected + "   完整门禁 —   人工评审 —";
            card.EnableInClassList("candidate-failed", _failedPerDirection[directionIndex] > 0);
            card.EnableInClassList("candidate-generated", expected > 0 && processed == expected && _failedPerDirection[directionIndex] == 0);
        }

        private void UpdateCandidateCapture(int directionIndex, VfxCandidatePlan plan, VfxPreviewCaptureResult capture)
        {
            if (capture == null || !capture.Success || string.IsNullOrEmpty(capture.OutputPath) || !File.Exists(capture.OutputPath))
            {
                UpdateCandidateCaptureFailure(directionIndex, capture?.Message ?? "单帧捕获失败");
                return;
            }
            ReleaseCandidatePreviewTexture(directionIndex);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "VFX 候选捕获 - " + directionIndex,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!texture.LoadImage(File.ReadAllBytes(capture.OutputPath), true))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                UpdateCandidateCaptureFailure(directionIndex, "无法解码单帧捕获");
                return;
            }
            _candidatePreviewTextures[directionIndex] = texture;
            _candidatePreviews[directionIndex].style.backgroundImage = new StyleBackground(texture);
            _candidatePreviews[directionIndex].EnableInClassList("capture-error", false);
            _candidatePreviews[directionIndex].EnableInClassList("has-capture", true);
            _candidatePreviewStates[directionIndex].text = StageName(plan) + " · 固定种子 " + plan.FixedSeed;
        }

        private void UpdateCandidateCaptureFailure(int directionIndex, string message)
        {
            if (directionIndex < 0 || directionIndex >= _candidatePreviews.Count)
                return;
            ReleaseCandidatePreviewTexture(directionIndex);
            _candidatePreviews[directionIndex].style.backgroundImage = StyleKeyword.None;
            _candidatePreviews[directionIndex].EnableInClassList("has-capture", false);
            _candidatePreviews[directionIndex].EnableInClassList("capture-error", true);
            _candidatePreviewStates[directionIndex].text = "预览不可用 · " + Truncate(message, 56);
        }

        private void ClearCandidateCaptures()
        {
            ReleaseCandidatePreviewTextures();
            for (int i = 0; i < _candidatePreviews.Count; i++)
            {
                _candidatePreviews[i].style.backgroundImage = StyleKeyword.None;
                _candidatePreviews[i].EnableInClassList("has-capture", false);
                _candidatePreviews[i].EnableInClassList("capture-error", false);
                _candidatePreviewStates[i].text = "尚无真实捕获";
            }
        }

        private void ReleaseCandidatePreviewTextures()
        {
            for (int i = 0; i < _candidatePreviewTextures.Length; i++)
                ReleaseCandidatePreviewTexture(i);
        }

        private void ReleaseCandidatePreviewTexture(int index)
        {
            if (_candidatePreviewTextures[index] == null)
                return;
            UnityEngine.Object.DestroyImmediate(_candidatePreviewTextures[index]);
            _candidatePreviewTextures[index] = null;
        }

        private static string FindReceiptAsset(VfxCandidateReceipt receipt, string kind)
        {
            if (receipt == null)
                return string.Empty;
            for (int i = 0; i < receipt.Assets.Count; i++)
                if (string.Equals(receipt.Assets[i].Kind, kind, StringComparison.Ordinal))
                    return receipt.Assets[i].AssetPath;
            return string.Empty;
        }

        private static float GetSampleTime(VfxCandidatePlan plan)
        {
            if (plan.PlanId.Contains(".charge.")) return 0.7f;
            if (plan.PlanId.Contains(".flight.")) return 0.3f;
            return 0.35f;
        }

        private static string TimeSliceId(VfxCandidatePlan plan)
        {
            if (plan.PlanId.Contains(".charge.")) return "charge-70pct";
            if (plan.PlanId.Contains(".flight.")) return "flight-30pct";
            return "impact-35pct";
        }

        private static string StageName(VfxCandidatePlan plan)
        {
            if (plan.PlanId.Contains(".charge.")) return "蓄力";
            if (plan.PlanId.Contains(".flight.")) return "飞行";
            return "命中";
        }

        private static string Truncate(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return "未知错误";
            string trimmed = value.Trim();
            return trimmed.Length <= maximum ? trimmed : trimmed.Substring(0, maximum) + "…";
        }

        private int CandidateDefinitionsCount => _planService.Candidates.Count;

        private VfxLibraryCreationBrief ReadBrief()
        {
            VfxLibraryStyleProfileOption style = _templateCatalog.FindStyleByDisplayName(_styleProfile.value);
            VfxLibraryGenerationProviderOption provider = _templateCatalog.FindProviderByDisplayName(_provider.value);
            VfxLibraryGenerationTemplateOption template = _templateCatalog.FindTemplateByDisplayName(_template.value);
            VfxLibraryQualityProfileOption quality = _templateCatalog.FindQualityByDisplayName(_qualityProfile.value);
            return new VfxLibraryCreationBrief
            {
                BriefName = _briefName.value,
                TargetEffectId = _targetEffectId.value,
                Intent = _briefIntent.value,
                Lifecycle = _lifecycle.value,
                StyleProfileId = style?.StyleProfileId ?? string.Empty,
                StyleProfileVersion = style?.Version ?? string.Empty,
                ProviderId = provider?.ProviderId ?? string.Empty,
                ProviderVersion = provider?.ProviderVersion ?? string.Empty,
                ProviderKind = provider?.Kind ?? string.Empty,
                ProviderModelId = provider?.ModelId ?? string.Empty,
                ProviderModelVersion = provider?.ModelVersion ?? string.Empty,
                ProviderEnabledByDefault = provider?.EnabledByDefault ?? false,
                TemplateId = template?.TemplateId ?? string.Empty,
                TemplateVersion = template?.TemplateVersion ?? string.Empty,
                TemplateKind = template?.Kind ?? string.Empty,
                TemplateCertified = template?.Certified ?? false,
                TemplateArbitraryTopology = template?.ArbitraryTopology ?? false,
                TemplateAllowsProvider = template != null && provider != null && template.AllowsProvider(provider.ProviderId),
                QualityProfileId = quality?.QualityProfileId ?? string.Empty,
                QualityProfileVersion = quality?.Version ?? string.Empty,
                FixedEvaluationSeeds = quality?.FixedSeeds == null ? Array.Empty<int>() : (int[])quality.FixedSeeds.Clone(),
                ReferencePackage = _referencePackage.value,
                LicenseId = "project-owned",
                ExploreNewPrimitive = _explorePrimitive.value,
                RequireFallback = _requireFallback.value
            };
        }

        private void SetStatus(string message, bool warning)
        {
            _status.text = message;
            _status.EnableInClassList("status-warning", warning);
        }

        private static void SetChoices(DropdownField field, params string[] choices)
        {
            field.choices = new List<string>(choices);
            field.SetValueWithoutNotify(choices.Length == 0 ? string.Empty : choices[0]);
        }

        private static void SetFirstChoice(DropdownField field)
        {
            field.SetValueWithoutNotify(field.choices.Count == 0 ? string.Empty : field.choices[0]);
        }

        private static string[] ProviderDisplayNames(IReadOnlyList<VfxLibraryGenerationProviderOption> options)
        {
            var result = new string[options.Count];
            for (int i = 0; i < options.Count; i++) result[i] = options[i].DisplayName;
            return result;
        }

        private static string[] StyleDisplayNames(IReadOnlyList<VfxLibraryStyleProfileOption> options)
        {
            var result = new string[options.Count];
            for (int i = 0; i < options.Count; i++) result[i] = options[i].DisplayName;
            return result;
        }

        private static string[] TemplateDisplayNames(IReadOnlyList<VfxLibraryGenerationTemplateOption> options)
        {
            var result = new string[options.Count];
            for (int i = 0; i < options.Count; i++) result[i] = options[i].DisplayName;
            return result;
        }

        private static string[] QualityDisplayNames(IReadOnlyList<VfxLibraryQualityProfileOption> options)
        {
            var result = new string[options.Count];
            for (int i = 0; i < options.Count; i++) result[i] = options[i].DisplayName;
            return result;
        }

        private static string JoinInts(int[] values)
        {
            if (values == null || values.Length == 0)
                return "无";
            var builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) builder.Append(" / ");
                builder.Append(values[i]);
            }
            return builder.ToString();
        }

        private static string FormatFixedStep(double seconds)
        {
            if (Math.Abs(seconds - (1d / 60d)) < 0.0000001d)
                return "1/60";
            return seconds.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string ShortHash(string hash)
        {
            return string.IsNullOrEmpty(hash) ? "无" : hash.Substring(0, Math.Min(12, hash.Length));
        }
    }
}
