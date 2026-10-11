using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class VfxSourceLibraryTests
{
    public static void RunAll()
    {
        SealIndex_IsStableAndDoesNotAssumeFixedCounts();
        Search_PrefersExistingProjectPrimitive();
        ExtractionPlan_BlocksStaleAndEscapingInputs();
        ExtractionPlan_ForcesExactAndFingerprintReuse();
        ExtractionReceipt_BindsUnityEvidenceAndComponentMembership();
    }

    private static void ExtractionPlan_ForcesExactAndFingerprintReuse()
    {
        VfxSourceLibraryDocument index = BuildIndex();
        index.Components[1].TechnicalFingerprint = index.Components[0].TechnicalFingerprint;
        VfxSourceLibraryValidator.SealIndex(index);
        var registry = new VfxSourceExtractionRegistryDocument
        {
            SourceLibraryId = index.SourceLibraryId,
            SourceIndexHash = index.IndexHash,
            Entries = new List<VfxSourceExtractionRegistryEntry>
            {
                new VfxSourceExtractionRegistryEntry
                {
                    PrimitiveId = "vfx.primitive.source.flash.existing", PrefabPath = "Assets/VFX/MxFramework/Review/existing.prefab",
                    SourceComponentIds = new List<string> { index.Components[0].ComponentId },
                    TechnicalFingerprints = new List<string> { index.Components[0].TechnicalFingerprint }
                }
            }
        };
        VfxSourceLibraryValidator.SealRegistry(registry);
        VfxSourceExtractionPlan exact = VfxSourcePlanner.PlanExtraction(index, Request(index.Components[0].ComponentId, "exact"), registry);
        Require(!exact.Blocked && exact.Resolution == VfxSourceExtractionStates.ReuseExisting && exact.Components.Count == 0, "Exact source membership must force Primitive reuse.");
        VfxSourceExtractionPlan equivalent = VfxSourcePlanner.PlanExtraction(index, Request(index.Components[1].ComponentId, "equivalent"), registry);
        Require(!equivalent.Blocked && equivalent.Resolution == VfxSourceExtractionStates.ReuseExisting, "Equivalent technical fingerprints must force Primitive reuse.");
        VfxSourceExtractionPlan variant = VfxSourcePlanner.PlanExtraction(index, new VfxSourceExtractionRequest
        {
            PrimitiveId = "vfx.primitive.source.flash.variant", DisplayName = "Variant", OutputPrefabPath = "Assets/VFX/MxFramework/Review/variant.prefab",
            ComponentIds = new List<string> { index.Components[0].ComponentId }, DeriveMaterials = true
        }, registry);
        Require(variant.Blocked && variant.Resolution == VfxSourceExtractionStates.VariantRequired, "A Variant without an override hash must be blocked.");
        Require(VfxSourceLibraryValidator.ValidateRegistry(registry, index).Valid, "A sealed one-owner registry must validate.");
    }

    private static VfxSourceExtractionRequest Request(string componentId, string token) => new VfxSourceExtractionRequest
    {
        PrimitiveId = "vfx.primitive.source.flash." + token,
        DisplayName = token,
        OutputPrefabPath = "Assets/VFX/MxFramework/Review/" + token + ".prefab",
        ComponentIds = new List<string> { componentId }
    };

    private static void SealIndex_IsStableAndDoesNotAssumeFixedCounts()
    {
        VfxSourceLibraryDocument first = BuildIndex();
        string hash = VfxSourceLibraryValidator.SealIndex(first).IndexHash;
        first.Components.Reverse();
        first.Effects.Reverse();
        string resealed = VfxSourceLibraryValidator.SealIndex(first).IndexHash;
        Require(hash == resealed, "Source Library seal must be deterministic after input ordering changes.");
        Require(first.PrefabCount == 2 && first.ParticleSystemCount == 2, "Source Library counts must derive from actual indexed entries, not a fixed baseline.");
        Require(VfxSourceLibraryValidator.ValidateIndex(first).Valid, "A sealed authorized deep index should validate.");
    }

    private static void Search_PrefersExistingProjectPrimitive()
    {
        VfxSourceLibraryDocument index = BuildIndex();
        index.Components[1].ExistingPrimitiveId = "vfx.primitive.production.flash";
        VfxSourceLibraryValidator.SealIndex(index);
        IReadOnlyList<VfxSourceSearchResult> results = new VfxSourceSearchService().Search(index, new VfxSourceSearchQuery { Role = "flash", Theme = "Fire" });
        Require(results.Count == 2, "Role/theme search should return both matching source components.");
        Require(results[0].ReuseDecision == "ReuseExistingPrimitive" && results[0].ComponentId == index.Components[1].ComponentId, "Production Primitive reuse must rank above source extraction.");
    }

    private static void ExtractionPlan_BlocksStaleAndEscapingInputs()
    {
        VfxSourceLibraryDocument index = BuildIndex();
        VfxSourceLibraryValidator.SealIndex(index);
        VfxSourceExtractionPlan valid = VfxSourcePlanner.PlanExtraction(index, new VfxSourceExtractionRequest
        {
            PrimitiveId = "vfx.primitive.source.flash.test",
            DisplayName = "Flash",
            OutputPrefabPath = "Assets/VFX/MxFramework/Review/Piloto/Primitives/flash.prefab",
            ComponentIds = new List<string> { index.Components[0].ComponentId }
        });
        Require(!valid.Blocked && VfxCreativeFlywheelHash.IsSha256(valid.PlanHash), "A bounded project extraction should produce a stable plan.");
        VfxSourceExtractionPlan escaping = VfxSourcePlanner.PlanExtraction(index, new VfxSourceExtractionRequest
        {
            PrimitiveId = "vfx.primitive.source.flash.escape",
            OutputPrefabPath = "Assets/VFX/MxFramework/../../Piloto Studio/source.prefab",
            ComponentIds = new List<string> { index.Components[0].ComponentId }
        });
        Require(escaping.Blocked, "Path traversal must block extraction planning.");
        index.DeepScanComplete = false;
        Require(VfxSourcePlanner.PlanExtraction(index, new VfxSourceExtractionRequest { PrimitiveId = "vfx.primitive.source.flash.stale", OutputPrefabPath = "Assets/VFX/MxFramework/stale.prefab", ComponentIds = new List<string> { index.Components[0].ComponentId } }).Blocked, "An incomplete/stale deep index must block extraction.");
    }

    private static void ExtractionReceipt_BindsUnityEvidenceAndComponentMembership()
    {
        VfxSourceLibraryDocument index = BuildIndex();
        VfxSourceLibraryValidator.SealIndex(index);
        VfxSourceExtractionPlan plan = VfxSourcePlanner.PlanExtraction(index, new VfxSourceExtractionRequest
        {
            PrimitiveId = "vfx.primitive.source.flash.receipt",
            DisplayName = "Flash",
            OutputPrefabPath = "Assets/VFX/MxFramework/Review/Piloto/Primitives/receipt.prefab",
            ComponentIds = new List<string> { index.Components[0].ComponentId }
        });
        var receipt = new VfxSourceExtractionReceipt
        {
            PlanHash = plan.PlanHash,
            PrimitiveId = plan.PrimitiveId,
            OutputPrefabPath = plan.OutputPrefabPath,
            OutputPrefabGuid = Hex(32, 'a'),
            OutputContentHash = "sha256:" + Hex(64, 'b'),
            OutputDependencyHash = Hex(32, 'c'),
            SourceComponentIds = new List<string> { index.Components[0].ComponentId },
            UnitySerializationUsed = true
        };
        VfxSourceLibraryValidator.SealReceipt(receipt);
        Require(VfxSourceLibraryValidator.ValidateExtraction(plan, receipt, index).Valid, "A sealed Unity receipt should validate against its exact plan and index.");
        receipt.SourceComponentIds.Add(index.Components[1].ComponentId);
        Require(!VfxSourceLibraryValidator.ValidateExtraction(plan, receipt, index).Valid, "Changed membership must invalidate the extraction receipt.");
    }

    private static VfxSourceLibraryDocument BuildIndex()
    {
        var index = new VfxSourceLibraryDocument
        {
            LicenseId = "license.test",
            PublicRedistributionAllowed = true,
            DeepScanComplete = true
        };
        for (int i = 0; i < 2; i++)
        {
            string prefabGuid = i == 0 ? Hex(32, '1') : Hex(32, '2');
            string effectId = "source.effect.test." + i;
            string componentId = "source.component.test." + i;
            index.Effects.Add(new VfxSourceEffectEntry
            {
                SourceEffectId = effectId, SourcePackageId = "piloto.test", DisplayName = "Effect " + i,
                PrefabGuid = prefabGuid, PrefabPath = "Assets/Piloto Studio/Effect" + i + ".prefab",
                ContentHash = "sha256:" + Hex(64, i == 0 ? '3' : '4'), DependencyHash = Hex(32, i == 0 ? '5' : '6'),
                Family = "Impact", Theme = "Fire", QualityTier = "Normal", Roles = new List<string> { "Impact" }, ParticleSystemCount = 1,
                ComponentIds = new List<string> { componentId }
            });
            index.Components.Add(new VfxSourceComponentEntry
            {
                ComponentId = componentId, SourcePackageId = "piloto.test", SourceEffectId = effectId, PrefabGuid = prefabGuid,
                PrefabPath = "Assets/Piloto Studio/Effect" + i + ".prefab", LocalFileId = i + 1, ObjectPath = "Flash", DisplayName = "Flash",
                Roles = new List<string> { "flash" }, Theme = "Fire", Shape = "Billboard", Anchor = "HitPoint", QualityTier = "Normal",
                TechnicalFingerprint = "sha256:" + Hex(64, i == 0 ? '7' : '8'), CreativeFingerprint = "sha256:" + Hex(64, '9'),
                SourceContentHash = "sha256:" + Hex(64, i == 0 ? '3' : '4'), DependencyHash = Hex(32, i == 0 ? 'a' : 'b')
            });
        }
        index.Packages.Add(new VfxSourcePackageEntry { SourcePackageId = "piloto.test", LicenseId = "license.test", PublicRedistributionAllowed = true, PrefabCount = 2, ParticleSystemCount = 2, ContentHash = "sha256:" + Hex(64, 'c') });
        index.SupportAssets.Add(new VfxSourceSupportAssetEntry { AssetGuid = Hex(32, 'd'), AssetPath = "Assets/Piloto Studio/test.mat", AssetKind = "Material", SourcePackageId = "piloto.test", ContentHash = "sha256:" + Hex(64, 'e') });
        return index;
    }

    private static string Hex(int count, char value) => new string(value, count);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
