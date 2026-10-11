using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    /// <summary>Stable schemas for Development Agent VFX task exchange.</summary>
    public static class VfxDevelopmentAgentTaskSchemas
    {
        public const string TaskV1 = "mx.vfx.agent-task.v1";
        public const string ProgressV1 = "mx.vfx.agent-progress.v1";
        public const string ReceiptV1 = "mx.vfx.agent-receipt.v1";
        public const string TextureBriefV1 = "mx.vfx.texture-brief.v1";
    }

    public static class VfxDevelopmentAgentProviderIds
    {
        public const string DevelopmentAgent = "mx.vfx.provider.development-agent";
        public const string LegacyProcedural = "mx.vfx.provider.procedural";
    }

    public static class VfxDevelopmentAgentTaskStates
    {
        public const string Created = "Created";
        public const string Claimed = "Claimed";
        public const string Planning = "Planning";
        public const string Authoring = "Authoring";
        public const string Capturing = "Capturing";
        public const string AwaitingReview = "AwaitingReview";
        public const string Completed = "Completed";
        public const string CancelRequested = "CancelRequested";
        public const string Blocked = "Blocked";
        public const string Cancelled = "Cancelled";

        public static readonly string[] Ordered =
        {
            Created, Claimed, Planning, Authoring, Capturing, AwaitingReview, Completed
        };
    }

    public static class VfxDevelopmentAgentDiagnosticCodes
    {
        public const string SchemaInvalid = "VFXAGENT001";
        public const string TaskTampered = "VFXAGENT002";
        public const string InputStale = "VFXAGENT003";
        public const string SandboxViolation = "VFXAGENT004";
        public const string ProceduralProviderForbidden = "VFXAGENT005";
        public const string ReceiptTampered = "VFXAGENT006";
        public const string CandidateInvalid = "VFXAGENT007";
        public const string ProvenanceMissing = "VFXAGENT008";
        public const string TextureBriefRequired = "VFXAGENT009";
        public const string UnitySerializationRequired = "VFXAGENT010";
        public const string StateInvalid = "VFXAGENT011";
        public const string ReferencePackInvalid = "VFXAGENT012";
        public const string PlaceholderDependencyForbidden = "VFXAGENT013";
    }

    public static class VfxAgentAssetUsagePolicies
    {
        public const string ContractReferenceOnly = "ContractReferenceOnly";
        public const string CandidateOnly = "CandidateOnly";
        public const string ProductionReusable = "ProductionReusable";
        public const string SupportAsset = "SupportAsset";
        public const string LicensedReusable = "LicensedReusable";
    }

    /// <summary>One technical asset available to the local Development Agent.</summary>
    /// <remarks>AllocByDesign. Authoring-only inventory DTO.</remarks>
    public sealed class VfxAgentAssetInventoryItem
    {
        public string Kind { get; set; } = string.Empty;
        public string AssetPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string UsagePolicy { get; set; } = VfxAgentAssetUsagePolicies.SupportAsset;
        public string SourcePackageId { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
    }

    public sealed class VfxAgentCandidateWorkItem
    {
        public string CandidateId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string OutputRoot { get; set; } = string.Empty;
        public int MaximumIterations { get; set; } = 3;
        public List<string> ReuseCandidateIds { get; set; } = new List<string>();
        public List<string> ReferenceEffectIds { get; set; } = new List<string>();
        public List<string> BorrowedDimensions { get; set; } = new List<string>();
    }

    /// <summary>Immutable hand-off package consumed by a local Development Agent.</summary>
    /// <remarks>AllocByDesign. The document describes work; it never mutates Unity assets.</remarks>
    public sealed class VfxDevelopmentAgentTaskDocument
    {
        public string SchemaVersion { get; set; } = VfxDevelopmentAgentTaskSchemas.TaskV1;
        public string TaskId { get; set; } = string.Empty;
        public string Stage { get; set; } = "Impact";
        public string ProviderId { get; set; } = VfxDevelopmentAgentProviderIds.DevelopmentAgent;
        public string TargetEffectId { get; set; } = string.Empty;
        public string TargetContractHash { get; set; } = string.Empty;
        public string ReferencePackId { get; set; } = string.Empty;
        public string ReferencePackHash { get; set; } = string.Empty;
        public string ReferencePackUsagePolicy { get; set; } = VfxReferenceUsagePolicies.ReferenceOnly;
        public string ReferenceSourcePackageId { get; set; } = string.Empty;
        public string ReferenceLicenseId { get; set; } = string.Empty;
        public bool ReferencePublicRedistributionAllowed { get; set; }
        public string CatalogHash { get; set; } = string.Empty;
        public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        public string AssetInventoryHash { get; set; } = string.Empty;
        public VfxCreativeBrief Brief { get; set; } = new VfxCreativeBrief();
        public string CandidateRoot { get; set; } = string.Empty;
        public int MaximumRounds { get; set; } = 3;
        public bool ReuseFirst { get; set; } = true;
        public bool AllowNewSupportAssetsAfterGap { get; set; } = true;
        public bool AllowAgentImageGeneration { get; set; } = true;
        public List<string> AllowedOperations { get; set; } = new List<string>();
        public List<string> ProhibitedOperations { get; set; } = new List<string>();
        public List<VfxReuseCandidate> ReuseCandidates { get; set; } = new List<VfxReuseCandidate>();
        public List<VfxAgentAssetInventoryItem> AssetInventory { get; set; } = new List<VfxAgentAssetInventoryItem>();
        public List<VfxAgentCandidateWorkItem> Candidates { get; set; } = new List<VfxAgentCandidateWorkItem>();
        public string TaskHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
        public bool Blocked => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    public sealed class VfxDevelopmentAgentProgressDocument
    {
        public string SchemaVersion { get; set; } = VfxDevelopmentAgentTaskSchemas.ProgressV1;
        public string TaskId { get; set; } = string.Empty;
        public string TaskHash { get; set; } = string.Empty;
        public string State { get; set; } = VfxDevelopmentAgentTaskStates.Created;
        public string AgentId { get; set; } = string.Empty;
        public string CurrentCandidateId { get; set; } = string.Empty;
        public int CompletedCandidates { get; set; }
        public int Sequence { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ProgressHash { get; set; } = string.Empty;
    }

    public class VfxAgentSelectedAsset
    {
        public string Kind { get; set; } = string.Empty;
        public string AssetPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string UsagePolicy { get; set; } = VfxAgentAssetUsagePolicies.SupportAsset;
        public string SourcePackageId { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
    }

    public sealed class VfxAgentContractReference
    {
        public string StableId { get; set; } = string.Empty;
        public string ContractHash { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }

    public sealed class VfxAgentCreatedAssetProvenance
    {
        public string GenerationMethod { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderVersion { get; set; } = string.Empty;
        public string PromptHash { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
        public string SourceUri { get; set; } = string.Empty;
        public string ReuseGap { get; set; } = string.Empty;
    }

    public sealed class VfxAgentCreatedAsset : VfxAgentSelectedAsset
    {
        public VfxAgentCreatedAssetProvenance Provenance { get; set; } = new VfxAgentCreatedAssetProvenance();
    }

    public sealed class VfxAgentSettingRecord
    {
        public string TargetPath { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public sealed class VfxAgentCaptureRecord
    {
        public string CaptureId { get; set; } = string.Empty;
        public string AssetPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string ViewId { get; set; } = string.Empty;
        public string TimeSliceId { get; set; } = string.Empty;
    }

    public sealed class VfxAgentCandidateReceipt
    {
        public string CandidateId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string SelectionRationale { get; set; } = string.Empty;
        public string PrefabAssetPath { get; set; } = string.Empty;
        public string PrefabContentHash { get; set; } = string.Empty;
        public string PrefabUnityGuid { get; set; } = string.Empty;
        public string PrefabDependencyHash { get; set; } = string.Empty;
        public string SerializedBy { get; set; } = string.Empty;
        public int IterationCount { get; set; }
        public bool ManualPreviewRequired { get; set; } = true;
        public List<VfxAgentContractReference> ContractReferences { get; set; } = new List<VfxAgentContractReference>();
        public List<VfxAgentSelectedAsset> ActualVisualDependencies { get; set; } = new List<VfxAgentSelectedAsset>();
        public List<VfxAgentSelectedAsset> ReusedAssets { get; set; } = new List<VfxAgentSelectedAsset>();
        public List<VfxAgentCreatedAsset> CreatedAssets { get; set; } = new List<VfxAgentCreatedAsset>();
        public List<VfxAgentSettingRecord> Settings { get; set; } = new List<VfxAgentSettingRecord>();
        public List<VfxAgentCaptureRecord> Captures { get; set; } = new List<VfxAgentCaptureRecord>();
    }

    public sealed class VfxAgentTextureBrief
    {
        public string SchemaVersion { get; set; } = VfxDevelopmentAgentTaskSchemas.TextureBriefV1;
        public string TaskId { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string VisualPurpose { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public string RequiredLicenseId { get; set; } = "project-owned";
    }

    /// <summary>Development Agent result bound to one immutable task and six Unity candidates.</summary>
    /// <remarks>AllocByDesign. Receipt validation may perform low-frequency file IO.</remarks>
    public sealed class VfxDevelopmentAgentReceiptDocument
    {
        public string SchemaVersion { get; set; } = VfxDevelopmentAgentTaskSchemas.ReceiptV1;
        public string TaskId { get; set; } = string.Empty;
        public string TaskHash { get; set; } = string.Empty;
        public string CatalogHash { get; set; } = string.Empty;
        public string GenerationTemplateCatalogHash { get; set; } = string.Empty;
        public string AssetInventoryHash { get; set; } = string.Empty;
        public string ReferencePackId { get; set; } = string.Empty;
        public string ReferencePackHash { get; set; } = string.Empty;
        public string ReviewStatus { get; set; } = VfxAuthoringReviewStatuses.Draft;
        public string FinalState { get; set; } = VfxDevelopmentAgentTaskStates.AwaitingReview;
        public List<VfxAgentCandidateReceipt> Candidates { get; set; } = new List<VfxAgentCandidateReceipt>();
        public List<VfxAgentTextureBrief> TextureBriefs { get; set; } = new List<VfxAgentTextureBrief>();
        public string ReceiptHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
        public bool Blocked => VfxCreativeFlywheelUtilities.HasErrors(Diagnostics);
    }

    public sealed class VfxDevelopmentAgentValidationContext
    {
        public string RepositoryRoot { get; set; } = string.Empty;
        public string ActualCatalogHash { get; set; } = string.Empty;
        public string ActualGenerationTemplateCatalogHash { get; set; } = string.Empty;
        public string ActualAssetInventoryHash { get; set; } = string.Empty;
        public string ActualReferencePackHash { get; set; } = string.Empty;
        public string ActualReferencePackUsagePolicy { get; set; } = VfxReferenceUsagePolicies.ReferenceOnly;
        public string ActualReferenceSourcePackageId { get; set; } = string.Empty;
        public string ActualReferenceLicenseId { get; set; } = string.Empty;
        public bool ActualReferencePublicRedistributionAllowed { get; set; }
        public bool VerifyAssetFiles { get; set; } = true;
    }

    public sealed class VfxDevelopmentAgentValidationReport
    {
        public bool Valid { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    /// <summary>Builds a deterministic, repository-relative VFX technical asset inventory.</summary>
    public static class VfxDevelopmentAgentAssetInventory
    {
        private static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".prefab"] = "Prefab", [".mat"] = "Material", [".shader"] = "Shader", [".shadergraph"] = "ShaderGraph",
            [".vfx"] = "VfxGraph", [".png"] = "Texture", [".tga"] = "Texture", [".jpg"] = "Texture",
            [".jpeg"] = "Texture", [".exr"] = "Texture", [".psd"] = "Texture", [".fbx"] = "Mesh", [".obj"] = "Mesh"
        };

        /// <remarks>AllocByDesign. Enumerates project files and hashes matching assets.</remarks>
        public static List<VfxAgentAssetInventoryItem> Build(string repositoryRoot) => Build(repositoryRoot, null);

        public static List<VfxAgentAssetInventoryItem> Build(string repositoryRoot, VfxReferencePackManifest referencePack)
        {
            string root = Path.GetFullPath(repositoryRoot ?? string.Empty);
            var result = new List<VfxAgentAssetInventoryItem>();
            AddRoot(result, root, "Assets/VFX");
            AddRoot(result, root, "Assets/Art");
            if (IsLicensedReusable(referencePack)) AddRoot(result, root, "Assets/Piloto Studio", referencePack);
            result.Sort((left, right) => string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal));
            return result;
        }

        public static string ComputeHash(List<VfxAgentAssetInventoryItem> items) => VfxCreativeFlywheelHash.Compute(items ?? new List<VfxAgentAssetInventoryItem>());

        private static void AddRoot(List<VfxAgentAssetInventoryItem> result, string repositoryRoot, string relativeRoot, VfxReferencePackManifest referencePack = null)
        {
            string absolute = Path.Combine(repositoryRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absolute)) return;
            foreach (string file in Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories))
            {
                string normalized = Relative(repositoryRoot, file);
                if (normalized.StartsWith("Assets/VFX/MxCandidates/", StringComparison.Ordinal) || normalized.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!Kinds.TryGetValue(Path.GetExtension(file), out string kind)) continue;
                result.Add(new VfxAgentAssetInventoryItem
                {
                    Kind = kind,
                    AssetPath = normalized,
                    ContentHash = VfxCreativeFlywheelHash.ComputeFile(file),
                    UnityGuid = ReadMetaGuid(file + ".meta"),
                    UsagePolicy = referencePack != null ? VfxAgentAssetUsagePolicies.LicensedReusable : ClassifyUsage(normalized),
                    SourcePackageId = referencePack?.SourcePackageId ?? string.Empty,
                    LicenseId = referencePack?.LicenseId ?? string.Empty
                });
            }
        }

        private static bool IsLicensedReusable(VfxReferencePackManifest manifest)
        {
            return manifest != null && string.Equals(manifest.UsagePolicy, VfxReferenceUsagePolicies.LicensedReusable, StringComparison.Ordinal) &&
                   manifest.PublicRedistributionAllowed && !string.IsNullOrWhiteSpace(manifest.SourcePackageId) && !string.IsNullOrWhiteSpace(manifest.LicenseId);
        }

        private static string ClassifyUsage(string path)
        {
            if (path.StartsWith("Assets/VFX/MxFramework/Production/", StringComparison.Ordinal)) return VfxAgentAssetUsagePolicies.ProductionReusable;
            if (path.StartsWith("Assets/VFX/MxFramework/Review/", StringComparison.Ordinal)) return VfxAgentAssetUsagePolicies.CandidateOnly;
            if (path.StartsWith("Assets/VFX/MxFramework/", StringComparison.Ordinal)) return VfxAgentAssetUsagePolicies.ContractReferenceOnly;
            return VfxAgentAssetUsagePolicies.SupportAsset;
        }

        private static string ReadMetaGuid(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            foreach (string line in File.ReadLines(path))
                if (line.StartsWith("guid: ", StringComparison.Ordinal)) return line.Substring(6).Trim();
            return string.Empty;
        }

        private static string Relative(string root, string path)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).Substring(prefix.Length).Replace('\\', '/');
        }
    }

    /// <summary>Creates immutable Impact-six-direction task packages for the local Development Agent.</summary>
    public static class VfxDevelopmentAgentTaskPlanner
    {
        private static readonly string[] Goals =
        {
            "Create an original contract-faithful replacement without reusing placeholder visual content.",
            "Strengthen the primary silhouette, impact peak, and readability against combat backgrounds.",
            "Meet the same completion baseline with a lower renderer, particle, and material budget.",
            "Strengthen flash, sparks, shockwave, smoke, and dissipation hierarchy.",
            "Create a distinct theme variant while preserving the target gameplay contract.",
            "Explore a new single-responsibility Primitive only after documenting the production reuse gap."
        };

        /// <remarks>AllocByDesign. Produces a new task DTO and hashes its immutable content.</remarks>
        public static VfxDevelopmentAgentTaskDocument Plan(string taskId, VfxCreativeBrief brief, string catalogHash, string templateHash, List<VfxAgentAssetInventoryItem> inventory, VfxReuseDecision reuse, string targetEffectId, string targetContractHash, VfxReferencePackManifest referencePack)
        {
            string candidateRoot = "Assets/VFX/MxCandidates/" + taskId;
            var task = new VfxDevelopmentAgentTaskDocument
            {
                TaskId = taskId ?? string.Empty,
                TargetEffectId = targetEffectId ?? string.Empty,
                TargetContractHash = targetContractHash ?? string.Empty,
                ReferencePackId = referencePack != null ? referencePack.ReferencePackId : string.Empty,
                ReferencePackHash = referencePack != null ? referencePack.ManifestHash : string.Empty,
                ReferencePackUsagePolicy = referencePack?.UsagePolicy ?? VfxReferenceUsagePolicies.ReferenceOnly,
                ReferenceSourcePackageId = referencePack?.SourcePackageId ?? string.Empty,
                ReferenceLicenseId = referencePack?.LicenseId ?? string.Empty,
                ReferencePublicRedistributionAllowed = referencePack?.PublicRedistributionAllowed == true,
                CatalogHash = catalogHash ?? string.Empty,
                GenerationTemplateCatalogHash = templateHash ?? string.Empty,
                Brief = brief ?? new VfxCreativeBrief(),
                CandidateRoot = candidateRoot,
                AssetInventory = inventory ?? new List<VfxAgentAssetInventoryItem>(),
                ReuseCandidates = reuse?.Candidates ?? new List<VfxReuseCandidate>()
            };
            task.AssetInventoryHash = VfxDevelopmentAgentAssetInventory.ComputeHash(task.AssetInventory);
            task.AllowedOperations.AddRange(new[] { "SearchProductionAssets", "ReadPlaceholderContract", "ReadReferencePackEvidence", "CreateOriginalPrefab", "AssignShader", "AssignMaterial", "AssignTexture", "ConfigureParticleSystem", "ConfigureTransform", "CreateCandidateSupportAssetAfterGap", "CapturePreview" });
            task.ProhibitedOperations.AddRange(new[] { "HandWriteUnityYaml", "UseLegacyProceduralGenerator", "ReuseLegacyPlaceholderVisual", "ReuseUnlicensedReferenceAsset", "WriteReady", "ModifyPublishedAssetInPlace", "WriteOutsideCandidateRoot" });
            for (int i = 0; i < VfxRoundZeroVariantKinds.Ordered.Length; i++)
            {
                var item = new VfxAgentCandidateWorkItem
                {
                    CandidateId = taskId + "-c" + (i + 1).ToString("00"),
                    Direction = VfxRoundZeroVariantKinds.Ordered[i],
                    Goal = Goals[i],
                    OutputRoot = candidateRoot + "/" + taskId + "-c" + (i + 1).ToString("00"),
                    MaximumIterations = 3
                };
                for (int r = 0; r < task.ReuseCandidates.Count; r++) item.ReuseCandidateIds.Add(task.ReuseCandidates[r].StableId);
                SelectReferences(referencePack, brief, item.ReferenceEffectIds);
                item.BorrowedDimensions.AddRange(new[] { "composition", "timing", "layering", "readability" });
                task.Candidates.Add(item);
            }
            VfxDevelopmentAgentTaskValidator.ValidateTask(task, new VfxDevelopmentAgentValidationContext { ActualCatalogHash = catalogHash, ActualGenerationTemplateCatalogHash = templateHash }, task.Diagnostics);
            task.TaskHash = VfxCreativeFlywheelHash.Compute(task, "taskHash", "diagnostics");
            return task;
        }

        public static string ComputeTargetContractHash(VfxAuthoringEffect effect)
        {
            return effect == null ? string.Empty : VfxCreativeFlywheelHash.Compute(new
            {
                effect.EffectId,
                effect.ResourceKey,
                effect.DefinitionId,
                effect.Roles,
                effect.ThemeIds,
                effect.AnchorContract,
                effect.Lifecycle,
                effect.ParameterOverrides
            });
        }

        private static void SelectReferences(VfxReferencePackManifest referencePack, VfxCreativeBrief brief, List<string> output)
        {
            if (referencePack?.Effects == null || brief?.Roles == null) return;
            for (int i = 0; i < referencePack.Effects.Count && output.Count < 3; i++)
            {
                VfxReferenceEffectSummary reference = referencePack.Effects[i];
                for (int roleIndex = 0; roleIndex < brief.Roles.Count; roleIndex++)
                    if (reference.SemanticRoles.Contains(brief.Roles[roleIndex])) { output.Add(reference.ReferenceEffectId); break; }
            }
        }
    }

    public static class VfxDevelopmentAgentTaskValidator
    {
        public static VfxDevelopmentAgentValidationReport ValidateTask(VfxDevelopmentAgentTaskDocument task, VfxDevelopmentAgentValidationContext context)
        {
            var report = new VfxDevelopmentAgentValidationReport();
            ValidateTask(task, context, report.Diagnostics);
            if (task != null && !string.Equals(task.TaskHash, VfxCreativeFlywheelHash.Compute(task, "taskHash", "diagnostics"), StringComparison.OrdinalIgnoreCase))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.TaskTampered, "taskHash", "Development Agent task hash is invalid or stale.");
            report.Valid = !VfxCreativeFlywheelUtilities.HasErrors(report.Diagnostics);
            return report;
        }

        internal static void ValidateTask(VfxDevelopmentAgentTaskDocument task, VfxDevelopmentAgentValidationContext context, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (task == null || !string.Equals(task.SchemaVersion, VfxDevelopmentAgentTaskSchemas.TaskV1, StringComparison.Ordinal))
            { Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.SchemaInvalid, "schemaVersion", "Unsupported Development Agent task schema."); return; }
            if (!IsIdentifier(task.TaskId) || !string.Equals(task.Stage, "Impact", StringComparison.Ordinal) || task.Candidates == null || task.Candidates.Count != 6)
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, "candidates", "Impact task must contain six stable candidates.");
            if (!string.Equals(task.ProviderId, VfxDevelopmentAgentProviderIds.DevelopmentAgent, StringComparison.Ordinal))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ProceduralProviderForbidden, "providerId", "Production VFX tasks must use the Development Agent provider.");
            if (!IsStableId(task.TargetEffectId) || !VfxCreativeFlywheelHash.IsSha256(task.TargetContractHash))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, "targetEffectId", "Replacement task requires a stable target Effect id and immutable contract hash.");
            if (!string.Equals(task.ReferencePackId, VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels, StringComparison.Ordinal) || !VfxCreativeFlywheelHash.IsSha256(task.ReferencePackHash))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ReferencePackInvalid, "referencePack", "Replacement task requires a registered Piloto source pack and manifest hash.");
            if (string.Equals(task.ReferencePackUsagePolicy, VfxReferenceUsagePolicies.LicensedReusable, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(task.ReferenceSourcePackageId) || string.IsNullOrWhiteSpace(task.ReferenceLicenseId) || !task.ReferencePublicRedistributionAllowed))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ReferencePackInvalid, "referencePack", "LicensedReusable tasks require explicit package, license, and redistribution evidence.");
            if (context != null && !string.IsNullOrWhiteSpace(context.ActualReferencePackUsagePolicy) &&
                (!string.Equals(task.ReferencePackUsagePolicy, context.ActualReferencePackUsagePolicy, StringComparison.Ordinal) ||
                 !string.Equals(task.ReferenceSourcePackageId, context.ActualReferenceSourcePackageId, StringComparison.Ordinal) ||
                 !string.Equals(task.ReferenceLicenseId, context.ActualReferenceLicenseId, StringComparison.Ordinal) ||
                 task.ReferencePublicRedistributionAllowed != context.ActualReferencePublicRedistributionAllowed))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, "referencePackAuthorization", "Task source authorization no longer matches the registered source pack.");
            if (context != null && !string.IsNullOrWhiteSpace(context.ActualReferencePackHash) && !string.Equals(task.ReferencePackHash, context.ActualReferencePackHash, StringComparison.OrdinalIgnoreCase))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, "referencePackHash", "Task reference pack is stale.");
            if (context != null && !string.IsNullOrWhiteSpace(context.ActualCatalogHash) && !string.Equals(task.CatalogHash, context.ActualCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, "catalogHash", "Task Catalog hash is stale.");
            if (context != null && !string.IsNullOrWhiteSpace(context.ActualGenerationTemplateCatalogHash) && !string.Equals(task.GenerationTemplateCatalogHash, context.ActualGenerationTemplateCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, "generationTemplateCatalogHash", "Task template Catalog hash is stale.");
            if (!string.Equals(task.AssetInventoryHash, VfxDevelopmentAgentAssetInventory.ComputeHash(task.AssetInventory), StringComparison.OrdinalIgnoreCase))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, "assetInventoryHash", "Task asset inventory hash is invalid.");
            if (context != null && !string.IsNullOrWhiteSpace(context.ActualAssetInventoryHash) && !string.Equals(task.AssetInventoryHash, context.ActualAssetInventoryHash, StringComparison.OrdinalIgnoreCase))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, "assetInventoryHash", "Task asset inventory is stale relative to the project.");
            string expectedRoot = "Assets/VFX/MxCandidates/" + task.TaskId;
            if (!string.Equals(task.CandidateRoot, expectedRoot, StringComparison.Ordinal))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.SandboxViolation, "candidateRoot", "Task candidate root escaped its session.");
            for (int i = 0; i < task.Candidates.Count; i++)
            {
                VfxAgentCandidateWorkItem candidate = task.Candidates[i];
                if (!IsBelow(candidate?.OutputRoot, expectedRoot)) Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.SandboxViolation, "candidates.outputRoot", "Candidate output escaped its task root.");
                if (candidate?.ReferenceEffectIds == null || candidate.ReferenceEffectIds.Count == 0 || candidate.BorrowedDimensions == null || candidate.BorrowedDimensions.Count == 0)
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ReferencePackInvalid, "candidates.referenceEffectIds", "Each candidate requires reference effects and permitted borrowed dimensions.");
            }
        }

        public static VfxDevelopmentAgentValidationReport ValidateProgress(VfxDevelopmentAgentTaskDocument task, VfxDevelopmentAgentProgressDocument progress)
        {
            var report = new VfxDevelopmentAgentValidationReport();
            if (progress == null || !string.Equals(progress.SchemaVersion, VfxDevelopmentAgentTaskSchemas.ProgressV1, StringComparison.Ordinal) || task == null || !string.Equals(progress.TaskId, task.TaskId, StringComparison.Ordinal) || !string.Equals(progress.TaskHash, task.TaskHash, StringComparison.OrdinalIgnoreCase))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.StateInvalid, "progress", "Progress is not bound to the task.");
            else if (!IsKnownState(progress.State))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.StateInvalid, "state", "Unknown Development Agent task state.");
            if (progress != null && !string.Equals(progress.ProgressHash, VfxCreativeFlywheelHash.Compute(progress, "progressHash"), StringComparison.OrdinalIgnoreCase))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.TaskTampered, "progressHash", "Progress hash is invalid.");
            report.Valid = !VfxCreativeFlywheelUtilities.HasErrors(report.Diagnostics);
            return report;
        }

        public static VfxDevelopmentAgentValidationReport ValidateReceipt(VfxDevelopmentAgentTaskDocument task, VfxDevelopmentAgentReceiptDocument receipt, VfxDevelopmentAgentValidationContext context)
        {
            var report = new VfxDevelopmentAgentValidationReport();
            VfxDevelopmentAgentValidationReport taskReport = ValidateTask(task, context);
            report.Diagnostics.AddRange(taskReport.Diagnostics);
            if (receipt == null || !string.Equals(receipt.SchemaVersion, VfxDevelopmentAgentTaskSchemas.ReceiptV1, StringComparison.Ordinal) || task == null || !string.Equals(receipt.TaskId, task.TaskId, StringComparison.Ordinal) || !string.Equals(receipt.TaskHash, task.TaskHash, StringComparison.OrdinalIgnoreCase))
            { Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.ReceiptTampered, "receipt", "Receipt is not bound to the task."); report.Valid = false; return report; }
            if (!string.Equals(receipt.ReviewStatus, VfxAuthoringReviewStatuses.Draft, StringComparison.Ordinal) || receipt.Candidates == null || receipt.Candidates.Count != 6)
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, "candidates", "Agent receipt must contain six Draft candidates.");
            if (!string.Equals(receipt.FinalState, VfxDevelopmentAgentTaskStates.AwaitingReview, StringComparison.Ordinal))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.StateInvalid, "finalState", "A promotable Agent receipt must stop at AwaitingReview.");
            if (receipt.TextureBriefs != null && receipt.TextureBriefs.Count > 0)
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.TextureBriefRequired, "textureBriefs", "Unresolved TextureBrief entries block candidate review; procedural fallback is forbidden.");
            if (!string.Equals(receipt.CatalogHash, task.CatalogHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(receipt.GenerationTemplateCatalogHash, task.GenerationTemplateCatalogHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(receipt.AssetInventoryHash, task.AssetInventoryHash, StringComparison.OrdinalIgnoreCase))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, "inputHashes", "Receipt inputs do not match the task.");
            if (!string.Equals(receipt.ReferencePackId, task.ReferencePackId, StringComparison.Ordinal) || !string.Equals(receipt.ReferencePackHash, task.ReferencePackHash, StringComparison.OrdinalIgnoreCase))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.ReferencePackInvalid, "referencePack", "Receipt is not bound to the task ReferenceOnly quality pack.");
            if (!string.Equals(receipt.ReceiptHash, VfxCreativeFlywheelHash.Compute(receipt, "receiptHash", "diagnostics"), StringComparison.OrdinalIgnoreCase))
                Add(report.Diagnostics, VfxDevelopmentAgentDiagnosticCodes.ReceiptTampered, "receiptHash", "Agent receipt hash is invalid.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < receipt.Candidates.Count; i++) ValidateCandidate(task, receipt.Candidates[i], context, ids, report.Diagnostics);
            report.Valid = !VfxCreativeFlywheelUtilities.HasErrors(report.Diagnostics);
            return report;
        }

        private static void ValidateCandidate(VfxDevelopmentAgentTaskDocument task, VfxAgentCandidateReceipt candidate, VfxDevelopmentAgentValidationContext context, HashSet<string> ids, List<AuthoringResourceDiagnostic> diagnostics)
        {
            VfxAgentCandidateWorkItem work = candidate == null ? null : FindWorkItem(task, candidate.CandidateId);
            if (candidate == null || !ids.Add(candidate.CandidateId) || work == null || !string.Equals(candidate.Direction, work.Direction, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(candidate.SelectionRationale))
            { Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, "candidate", "Candidate id and selection rationale are required."); return; }
            if (!IsBelow(candidate.PrefabAssetPath, task.CandidateRoot) || !VfxCreativeFlywheelHash.IsSha256(candidate.PrefabContentHash) || !VfxCreativeFlywheelHash.IsUnityGuid(candidate.PrefabUnityGuid) || !VfxCreativeFlywheelHash.IsUnityDependencyHash(candidate.PrefabDependencyHash))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, candidate.CandidateId, "Candidate Prefab evidence is invalid or outside the task root.");
            if (!string.Equals(candidate.SerializedBy, "UnityEditor", StringComparison.Ordinal) && !string.Equals(candidate.SerializedBy, "UnityMcp", StringComparison.Ordinal))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.UnitySerializationRequired, candidate.CandidateId, "Candidate must be serialized by Unity Editor or Unity MCP.");
            if (candidate.IterationCount < 1 || candidate.IterationCount > 3 || candidate.Settings.Count == 0 || candidate.Captures.Count == 0)
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, candidate.CandidateId, "Candidate requires 1-3 iterations, settings, and captures.");
            bool hasTargetContract = false;
            for (int i = 0; i < candidate.ContractReferences.Count; i++)
            {
                VfxAgentContractReference contract = candidate.ContractReferences[i];
                if (contract != null && string.Equals(contract.StableId, task.TargetEffectId, StringComparison.Ordinal) && string.Equals(contract.ContractHash, task.TargetContractHash, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(contract.Purpose)) hasTargetContract = true;
            }
            if (!hasTargetContract)
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, candidate.CandidateId, "Candidate is not bound to the target placeholder contract.");
            for (int i = 0; i < candidate.ActualVisualDependencies.Count; i++)
                ValidateVisualDependency(candidate.CandidateId, candidate.ActualVisualDependencies[i], task, context, diagnostics);
            for (int i = 0; i < candidate.ReusedAssets.Count; i++)
            {
                VfxAgentSelectedAsset asset = candidate.ReusedAssets[i];
                if (asset == null || Path.IsPathRooted(asset.AssetPath) || asset.AssetPath.Contains("..") || !VfxCreativeFlywheelHash.IsSha256(asset.ContentHash) || !VfxCreativeFlywheelHash.IsUnityGuid(asset.UnityGuid) || !VfxCreativeFlywheelHash.IsUnityDependencyHash(asset.DependencyHash) || string.IsNullOrWhiteSpace(asset.Purpose))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ProvenanceMissing, candidate.CandidateId, "Reused assets require a project-relative path, hashes, Unity GUID and purpose.");
                if (IsForbiddenVisualAsset(asset, task))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.PlaceholderDependencyForbidden, candidate.CandidateId, "LegacyPlaceholder, review-only, candidate, or external reference assets cannot be reused as candidate visual content.");
                if (context?.VerifyAssetFiles == true && !VerifyFile(context.RepositoryRoot, asset?.AssetPath, asset?.ContentHash))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, candidate.CandidateId, "A reused asset is missing or changed after authoring.");
            }
            for (int i = 0; i < candidate.CreatedAssets.Count; i++)
            {
                VfxAgentCreatedAsset asset = candidate.CreatedAssets[i];
                if (!IsBelow(asset?.AssetPath, task.CandidateRoot) || !VfxCreativeFlywheelHash.IsSha256(asset?.ContentHash) || !VfxCreativeFlywheelHash.IsUnityGuid(asset?.UnityGuid) || !VfxCreativeFlywheelHash.IsUnityDependencyHash(asset?.DependencyHash) || string.IsNullOrWhiteSpace(asset?.Provenance?.ReuseGap) || string.IsNullOrWhiteSpace(asset.Provenance.LicenseId))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ProvenanceMissing, candidate.CandidateId, "Created support assets require a task-local path, reuse gap, and license.");
                if (string.Equals(asset?.Provenance?.GenerationMethod, "Procedural", StringComparison.OrdinalIgnoreCase) || string.Equals(asset?.Provenance?.ProviderId, VfxDevelopmentAgentProviderIds.LegacyProcedural, StringComparison.Ordinal))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ProceduralProviderForbidden, candidate.CandidateId, "Legacy procedural assets cannot enter Agent task evidence.");
                if (context?.VerifyAssetFiles == true && !VerifyFile(context.RepositoryRoot, asset?.AssetPath, asset?.ContentHash))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, candidate.CandidateId, "A created support asset is missing or changed after authoring.");
            }
            string captureRoot = "Temp/MxFramework/VfxAgentTasks/" + task.TaskId + "/";
            for (int i = 0; i < candidate.Captures.Count; i++)
            {
                VfxAgentCaptureRecord capture = candidate.Captures[i];
                if (capture == null || !capture.AssetPath.StartsWith(captureRoot, StringComparison.Ordinal) || capture.AssetPath.Contains("..") || !VfxCreativeFlywheelHash.IsSha256(capture.ContentHash) || string.IsNullOrWhiteSpace(capture.ViewId) || string.IsNullOrWhiteSpace(capture.TimeSliceId))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, candidate.CandidateId, "Capture evidence must be task-local, hashed, and identify its view/time slice.");
                if (context?.VerifyAssetFiles == true && !VerifyFile(context.RepositoryRoot, capture?.AssetPath, capture?.ContentHash))
                    Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, candidate.CandidateId, "A capture file is missing or changed after authoring.");
            }
            if (context?.VerifyAssetFiles == true && !VerifyFile(context.RepositoryRoot, candidate.PrefabAssetPath, candidate.PrefabContentHash))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.CandidateInvalid, candidate.CandidateId, "Candidate Prefab file is missing or its content hash changed.");
        }

        private static void ValidateVisualDependency(string candidateId, VfxAgentSelectedAsset asset, VfxDevelopmentAgentTaskDocument task, VfxDevelopmentAgentValidationContext context, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (asset == null || Path.IsPathRooted(asset.AssetPath) || asset.AssetPath.Contains("..") || !VfxCreativeFlywheelHash.IsSha256(asset.ContentHash) || !VfxCreativeFlywheelHash.IsUnityGuid(asset.UnityGuid) || string.IsNullOrWhiteSpace(asset.Purpose))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.ProvenanceMissing, candidateId, "Actual visual dependencies require project-relative path, hash, Unity GUID, usage policy and purpose.");
            if (IsForbiddenVisualAsset(asset, task))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.PlaceholderDependencyForbidden, candidateId, "Candidate has a forbidden LegacyPlaceholder or external reference visual dependency: " + (asset?.AssetPath ?? string.Empty));
            if (context?.VerifyAssetFiles == true && !VerifyFile(context.RepositoryRoot, asset?.AssetPath, asset?.ContentHash))
                Add(diagnostics, VfxDevelopmentAgentDiagnosticCodes.InputStale, candidateId, "An actual visual dependency is missing or changed after authoring.");
        }

        private static bool IsForbiddenVisualAsset(VfxAgentSelectedAsset asset, VfxDevelopmentAgentTaskDocument task)
        {
            if (asset == null) return true;
            string path = (asset.AssetPath ?? string.Empty).Replace('\\', '/');
            bool licensedPiloto = path.IndexOf("Piloto Studio", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                  string.Equals(asset.UsagePolicy, VfxAgentAssetUsagePolicies.LicensedReusable, StringComparison.Ordinal) &&
                                  string.Equals(task?.ReferencePackUsagePolicy, VfxReferenceUsagePolicies.LicensedReusable, StringComparison.Ordinal) &&
                                  task.ReferencePublicRedistributionAllowed &&
                                  string.Equals(asset.SourcePackageId, task.ReferenceSourcePackageId, StringComparison.Ordinal) &&
                                  string.Equals(asset.LicenseId, task.ReferenceLicenseId, StringComparison.Ordinal);
            return string.Equals(asset.UsagePolicy, VfxAgentAssetUsagePolicies.ContractReferenceOnly, StringComparison.Ordinal) ||
                   string.Equals(asset.UsagePolicy, VfxAgentAssetUsagePolicies.CandidateOnly, StringComparison.Ordinal) ||
                   path.StartsWith("Assets/VFX/MxFramework/", StringComparison.Ordinal) && !path.StartsWith("Assets/VFX/MxFramework/Production/", StringComparison.Ordinal) ||
                   path.IndexOf("Piloto Studio", StringComparison.OrdinalIgnoreCase) >= 0 && !licensedPiloto;
        }

        private static bool VerifyFile(string root, string assetPath, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(assetPath) || Path.IsPathRooted(assetPath) || assetPath.Contains("..")) return false;
            string repository = Path.GetFullPath(root);
            string full = Path.GetFullPath(Path.Combine(repository, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = repository.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && string.Equals(VfxCreativeFlywheelHash.ComputeFile(full), expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static VfxAgentCandidateWorkItem FindWorkItem(VfxDevelopmentAgentTaskDocument task, string id)
        { for (int i = 0; i < task.Candidates.Count; i++) if (string.Equals(task.Candidates[i]?.CandidateId, id, StringComparison.Ordinal)) return task.Candidates[i]; return null; }

        private static bool IsKnownState(string state)
        { if (string.Equals(state, VfxDevelopmentAgentTaskStates.CancelRequested, StringComparison.Ordinal) || string.Equals(state, VfxDevelopmentAgentTaskStates.Blocked, StringComparison.Ordinal) || string.Equals(state, VfxDevelopmentAgentTaskStates.Cancelled, StringComparison.Ordinal)) return true; for (int i = 0; i < VfxDevelopmentAgentTaskStates.Ordered.Length; i++) if (string.Equals(state, VfxDevelopmentAgentTaskStates.Ordered[i], StringComparison.Ordinal)) return true; return false; }

        private static bool IsBelow(string path, string root)
        { if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root) || Path.IsPathRooted(path) || path.Contains("..") || path.Contains("\\")) return false; string normalizedRoot = root.TrimEnd('/'); return path.StartsWith(normalizedRoot + "/", StringComparison.Ordinal); }

        private static bool IsIdentifier(string value)
        { if (string.IsNullOrWhiteSpace(value)) return false; for (int i = 0; i < value.Length; i++) { char c = value[i]; if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false; } return true; }

        private static bool IsStableId(string value)
        { if (string.IsNullOrWhiteSpace(value)) return false; for (int i = 0; i < value.Length; i++) { char c = value[i]; if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')) return false; } return true; }

        private static void Add(List<AuthoringResourceDiagnostic> diagnostics, string code, string field, string message)
        { diagnostics.Add(new AuthoringResourceDiagnostic { Severity = CharacterAuthoringValidationSeverity.Error, Code = code, ResourceStableId = "vfx.agent-task", SourceField = field, Message = message }); }
    }
}
