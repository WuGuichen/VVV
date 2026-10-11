using System;
using System.Collections.Generic;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class VfxDevelopmentAgentTaskTests
{
    public static void RunAll()
    {
        TaskPlan_IsStableAndImpactOnly();
        Progress_IsTaskBoundAndSupportsCancelRequest();
        Receipt_RejectsProceduralAndStaleEvidence();
    }

    private static void TaskPlan_IsStableAndImpactOnly()
    {
        VfxDevelopmentAgentTaskDocument first = CreateTask();
        VfxDevelopmentAgentTaskDocument second = CreateTask();
        Require(first.Candidates.Count == 6, "Impact task must contain six creative directions.");
        Require(first.Stage == "Impact", "First Development Agent slice must be Impact only.");
        Require(first.ProviderId == VfxDevelopmentAgentProviderIds.DevelopmentAgent, "Production task must use the Development Agent provider.");
        Require(first.TaskHash == second.TaskHash, "Task planning must be deterministic.");
        Require(first.ProhibitedOperations.Contains("UseLegacyProceduralGenerator"), "Task must explicitly prohibit the legacy procedural generator.");
        VfxDevelopmentAgentValidationReport fresh = VfxDevelopmentAgentTaskValidator.ValidateTask(first, Context(first));
        Require(fresh.Valid, "Fresh task should validate: " + string.Join(" | ", fresh.Diagnostics.ConvertAll(item => item.Code + ":" + item.Message)));

        first.ProviderId = VfxDevelopmentAgentProviderIds.LegacyProcedural;
        first.TaskHash = VfxCreativeFlywheelHash.Compute(first, "taskHash", "diagnostics");
        Require(HasCode(VfxDevelopmentAgentTaskValidator.ValidateTask(first, Context(first)), VfxDevelopmentAgentDiagnosticCodes.ProceduralProviderForbidden), "Procedural provider must be rejected.");
    }

    private static void Progress_IsTaskBoundAndSupportsCancelRequest()
    {
        VfxDevelopmentAgentTaskDocument task = CreateTask();
        var progress = new VfxDevelopmentAgentProgressDocument
        {
            TaskId = task.TaskId,
            TaskHash = task.TaskHash,
            State = VfxDevelopmentAgentTaskStates.CancelRequested,
            Sequence = 1,
            Message = "User requested cancellation."
        };
        progress.ProgressHash = VfxCreativeFlywheelHash.Compute(progress, "progressHash");
        Require(VfxDevelopmentAgentTaskValidator.ValidateProgress(task, progress).Valid, "CancelRequested must be a valid cooperative task state.");
        progress.Message = "tampered";
        Require(HasCode(VfxDevelopmentAgentTaskValidator.ValidateProgress(task, progress), VfxDevelopmentAgentDiagnosticCodes.TaskTampered), "Progress mutation must invalidate its hash.");
    }

    private static void Receipt_RejectsProceduralAndStaleEvidence()
    {
        VfxDevelopmentAgentTaskDocument task = CreateTask();
        VfxDevelopmentAgentReceiptDocument receipt = CreateReceipt(task);
        VfxDevelopmentAgentValidationContext context = Context(task);
        VfxDevelopmentAgentValidationReport valid = VfxDevelopmentAgentTaskValidator.ValidateReceipt(task, receipt, context);
        Require(valid.Valid, "Complete Development Agent receipt should validate without file IO: " + string.Join(" | ", valid.Diagnostics.ConvertAll(item => item.Code + ":" + item.Message)));

        receipt.Candidates[0].CreatedAssets.Add(new VfxAgentCreatedAsset
        {
            Kind = "Texture",
            AssetPath = task.CandidateRoot + "/" + task.Candidates[0].CandidateId + "/legacy.png",
            ContentHash = Hash('a'),
            UnityGuid = Guid32('a'),
            DependencyHash = Guid32('b'),
            Provenance = new VfxAgentCreatedAssetProvenance
            {
                GenerationMethod = "Procedural",
                ProviderId = VfxDevelopmentAgentProviderIds.LegacyProcedural,
                LicenseId = "project-owned",
                ReuseGap = "Missing impact mask"
            }
        });
        receipt.ReceiptHash = VfxCreativeFlywheelHash.Compute(receipt, "receiptHash", "diagnostics");
        Require(HasCode(VfxDevelopmentAgentTaskValidator.ValidateReceipt(task, receipt, context), VfxDevelopmentAgentDiagnosticCodes.ProceduralProviderForbidden), "Legacy procedural support assets must never become promotion evidence.");

        receipt = CreateReceipt(task);
        context.ActualAssetInventoryHash = Hash('f');
        Require(HasCode(VfxDevelopmentAgentTaskValidator.ValidateReceipt(task, receipt, context), VfxDevelopmentAgentDiagnosticCodes.InputStale), "Changed resource inventory must stale the task and receipt.");

        receipt = CreateReceipt(task);
        receipt.Candidates[0].ActualVisualDependencies.Add(new VfxAgentSelectedAsset
        {
            Kind = "Prefab",
            AssetPath = "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball.prefab",
            ContentHash = Hash('7'),
            UnityGuid = Guid32('7'),
            DependencyHash = Guid32('8'),
            UsagePolicy = VfxAgentAssetUsagePolicies.ContractReferenceOnly,
            Purpose = "Attempted placeholder visual reuse"
        });
        receipt.ReceiptHash = VfxCreativeFlywheelHash.Compute(receipt, "receiptHash", "diagnostics");
        Require(HasCode(VfxDevelopmentAgentTaskValidator.ValidateReceipt(task, receipt, Context(task)), VfxDevelopmentAgentDiagnosticCodes.PlaceholderDependencyForbidden), "LegacyPlaceholder dependencies must block candidate receipts.");
    }

    private static VfxDevelopmentAgentTaskDocument CreateTask()
    {
        var brief = new VfxCreativeBrief
        {
            BriefId = "fireball-impact-agent-test",
            CatalogHash = Hash('1'),
            GenerationTemplateCatalogHash = Hash('2'),
            Description = "Fireball Impact six-direction Development Agent slice."
        };
        var inventory = new List<VfxAgentAssetInventoryItem>
        {
            new VfxAgentAssetInventoryItem { Kind = "Prefab", AssetPath = "Assets/VFX/Test.prefab", ContentHash = Hash('3'), UnityGuid = Guid32('3') }
        };
        brief.Roles.Add("Impact");
        return VfxDevelopmentAgentTaskPlanner.Plan(
            "fireball-impact-agent-test",
            brief,
            Hash('1'),
            Hash('2'),
            inventory,
            new VfxReuseDecision(),
            "vfx.magic.fireball",
            Hash('4'),
            CreateReferencePack());
    }

    private static VfxDevelopmentAgentReceiptDocument CreateReceipt(VfxDevelopmentAgentTaskDocument task)
    {
        var receipt = new VfxDevelopmentAgentReceiptDocument
        {
            TaskId = task.TaskId,
            TaskHash = task.TaskHash,
            CatalogHash = task.CatalogHash,
            GenerationTemplateCatalogHash = task.GenerationTemplateCatalogHash,
            AssetInventoryHash = task.AssetInventoryHash,
            ReferencePackId = task.ReferencePackId,
            ReferencePackHash = task.ReferencePackHash,
            FinalState = VfxDevelopmentAgentTaskStates.AwaitingReview
        };
        for (int i = 0; i < task.Candidates.Count; i++)
        {
            VfxAgentCandidateWorkItem work = task.Candidates[i];
            var candidate = new VfxAgentCandidateReceipt
            {
                CandidateId = work.CandidateId,
                Direction = work.Direction,
                SelectionRationale = "Reuses the closest published Fireball layers and records all configuration changes.",
                PrefabAssetPath = work.OutputRoot + "/candidate.prefab",
                PrefabContentHash = Hash((char)('a' + i)),
                PrefabUnityGuid = Guid32((char)('a' + i)),
                PrefabDependencyHash = Guid32((char)('1' + i)),
                SerializedBy = "UnityMcp",
                IterationCount = 1
            };
            candidate.ContractReferences.Add(new VfxAgentContractReference
            {
                StableId = task.TargetEffectId,
                ContractHash = task.TargetContractHash,
                Purpose = "Preserve trigger, anchor, lifecycle, and parameter contracts only."
            });
            candidate.Settings.Add(new VfxAgentSettingRecord { TargetPath = "Root/Impact", Category = "Transform", Property = "localScale", Value = "1,1,1" });
            candidate.Captures.Add(new VfxAgentCaptureRecord { CaptureId = "gameplay-50", AssetPath = "Temp/MxFramework/VfxAgentTasks/" + task.TaskId + "/captures/" + work.CandidateId + "/gameplay-50.png", ContentHash = Hash('9'), ViewId = "Gameplay", TimeSliceId = "50" });
            receipt.Candidates.Add(candidate);
        }
        receipt.ReceiptHash = VfxCreativeFlywheelHash.Compute(receipt, "receiptHash", "diagnostics");
        return receipt;
    }

    private static VfxDevelopmentAgentValidationContext Context(VfxDevelopmentAgentTaskDocument task) => new()
    {
        ActualCatalogHash = task.CatalogHash,
        ActualGenerationTemplateCatalogHash = task.GenerationTemplateCatalogHash,
        ActualAssetInventoryHash = task.AssetInventoryHash,
        ActualReferencePackHash = task.ReferencePackHash,
        VerifyAssetFiles = false
    };

    private static VfxReferencePackManifest CreateReferencePack()
    {
        var manifest = new VfxReferencePackManifest
        {
            ReferencePackId = VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels,
            UsagePolicy = VfxReferenceUsagePolicies.ReferenceOnly,
            PrefabCount = 22,
            DependencyCount = 100,
            SourceUnityVersion = "6000.0.61f1",
            SourceUrpVersion = "17.0.4"
        };
        manifest.Effects.Add(new VfxReferenceEffectSummary
        {
            ReferenceEffectId = "reference.piloto.hit.01",
            DisplayName = "Hit 01",
            ComplexityTier = "Simple",
            SemanticRoles = new List<string> { "Impact" }
        });
        manifest.ManifestHash = VfxCreativeFlywheelHash.Compute(manifest, "manifestHash", "diagnostics");
        return manifest;
    }

    private static bool HasCode(VfxDevelopmentAgentValidationReport report, string code) => report.Diagnostics.Exists(item => item.Code == code);
    private static string Hash(char value) => "sha256:" + new string(value, 64);
    private static string Guid32(char value) => new(value, 32);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
