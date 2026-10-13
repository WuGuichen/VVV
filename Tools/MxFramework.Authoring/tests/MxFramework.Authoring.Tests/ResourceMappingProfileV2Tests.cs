using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.ResourceMapping.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static class ResourceMappingProfileV2Tests
{
    public static void RunCharacterMaterialCollisionChecks()
    {
        CharacterProducer_MaterialDescriptorMapsFinalUnityAssetAndFailsClosed();
        CharacterProducer_RejectsMaterialTargetCollisionsDeterministically();
        CharacterImport_MaterialTargetCollisionPreservesPublishedOutputs();
    }

    public static void RunFairyGuiAtlasChecks()
    {
        VerifyFairyGuiAtlasPages(new FairyGuiRepositoryProducerAdapter().BuildSnapshot(Directory.GetCurrentDirectory()));
    }

    public static void RunCharacterSyncChecks()
    {
        CharacterSync_RejectsCompileBlockedSnapshots();
        CharacterSync_RejectsUnsupportedFormatBeforePublishing();
        CharacterSync_RequiresCompileGateReport();
        CharacterSync_AllowsWarningsSpawnBlocksAndPendingImport();
        CharacterSync_ReplacesCompleteSnapshotAndRejectsStaleRevision();
        CharacterUnityImport_SubmitsSnapshotThroughAggregator();
        CharacterThenFirstPartySync_PreservesPackageOwnedRows();
    }

    private static void VerifyFairyGuiAtlasPages(ResourceMappingProducerSnapshotResult result)
    {
        Require(result.Success, "FairyGUI producer snapshot must be valid.");
        const string package = "MxCharacterTestRuntimeAiLabUi";
        foreach (string path in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "Assets/Bundles/FGUI", package), package + "_atlas*.png"))
        {
            string suffix = Path.GetFileNameWithoutExtension(path).Substring(package.Length + 1);
            Require(result.Snapshot.Entries.FindAll(entry => entry.ResourceKey?.Id == "ui.fairygui.charactertest.runtime_ai_lab_ui." + suffix).Count == 1,
                "Every published Runtime AI Lab atlas page must have its own resource identity: " + suffix);
        }
    }

    public static void RunAll()
    {
        ValidProfile_PassesAndRoundTripsCanonicalJson();
        LegacyField_IsRejectedByStrictJsonReader();
        SharedStrictReader_RejectsUnknownField();
        SharedStrictReader_RejectsMissingRequiredFields();
        PersistedTypeAlias_IsRejected();
        InvalidCatalogMountPath_IsRejected();
        InvalidSourceLocator_IsRejected();
        DeliveryMatrix_IsEnforced();
        Aggregator_ReplacesOnlyOwnedCompleteSnapshot();
        Aggregator_RevisionConflictDoesNotMutateProfile();
        Aggregator_CrossOwnerKeyCollisionIsRejected();
        Aggregator_OwnershipMismatchIsRejected();
        Compiler_ProducesDeterministicCanonicalPlan();
        Compiler_ReceiptChangesMappingFingerprint();
        Compiler_RejectsOldSchemaAndUnknownDependencies();
        Compiler_InvalidShapeReturnsDiagnosticsInsteadOfThrowing();
        Compiler_EnforcesGlobalOverrideParity();
        Compiler_RejectsDuplicateInternalGlobalRouteEvenWithOverride();
        Compiler_RejectsDuplicateExternalRouteWithinOneMount();
        Compiler_EmitsBundleAndMountMembership();
        Compiler_EnforcesBundleEmissionPolicy();
        Compiler_RejectsProviderDataAuthority();
        BuildTargetResolver_RequiresConcreteUnityEvidence();
        GlobalBuildStatus_RequiresOneMatchingArtifactGeneration();
        ResourceManagerProductionState_UsesSharedPlan();
        CharacterCompiler_EmitsCanonicalOwnedSnapshot();
        CharacterProducer_MaterialDescriptorMapsFinalUnityAssetAndFailsClosed();
        CharacterProducer_RejectsMaterialTargetCollisionsDeterministically();
        CharacterImport_MaterialTargetCollisionPreservesPublishedOutputs();
        CharacterSync_ReplacesCompleteSnapshotAndRejectsStaleRevision();
        CharacterSync_RejectsCompileBlockedSnapshots();
        CharacterSync_RejectsUnsupportedFormatBeforePublishing();
        CharacterSync_RequiresCompileGateReport();
        CharacterSync_AllowsWarningsSpawnBlocksAndPendingImport();
        CharacterUnityImport_SubmitsSnapshotThroughAggregator();
        CharacterThenFirstPartySync_PreservesPackageOwnedRows();
        FirstPartySync_ConsumesReviewedDecisionsWithoutAliases();
        FirstPartyProducerAdapters_ReplaceCompleteOwnedSnapshots();
        FirstPartyProducerSet_DoesNotEmitCharacterPackageTombstones();
        RepositoryProducerAdapter_DiscoversAddDeleteAndGuidPreservingMove();
        RepositoryProducerAdapter_AmbiguousPriorGuidFailsClosed();
        RepositoryProducerAdapter_MissingRootFailsClosed();
        FairyGuiProducerAdapter_MissingRequiredPackageBytesFailsClosed();
        ResourceManagerManualSave_UsesProfileAsSoleAuthorityAndRejectsConcurrentStaleWriter();
        FileAggregator_ConcurrentWritersDoNotLoseUpdates();
        CheckedInSchemaAndProductionProfileExist();
    }

    private static void ValidProfile_PassesAndRoundTripsCanonicalJson()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        ResourceMappingValidationReport report = ResourceMappingProfileValidator.Validate(profile);
        Require(!report.HasErrors, "Valid v2 Profile should pass validation.");

        JsonSerializerOptions options = CreateJsonOptions();
        string json = JsonSerializer.Serialize(profile, options);
        Require(json.Contains("\"deliveryIntent\": \"internal\""), "Delivery enum should serialize as lower camel case.");
        Require(json.Contains("\"overrideIntent\": \"forbid\""), "Override enum should serialize as lower camel case.");
        ResourceMappingProfileV2 roundTrip = JsonSerializer.Deserialize<ResourceMappingProfileV2>(json, options);
        Require(roundTrip != null && roundTrip.Entries.Count == 2, "v2 Profile should roundtrip.");
    }

    private static void LegacyField_IsRejectedByStrictJsonReader()
    {
        const string json = "{\"schema\":\"mx.resource-mapping-profile.v2\",\"schemaVersion\":1}";
        bool rejected = false;
        try
        {
            JsonSerializer.Deserialize<ResourceMappingProfileV2>(json, CreateJsonOptions());
        }
        catch (JsonException)
        {
            rejected = true;
        }
        Require(rejected, "Strict v2 reader should reject removed legacy fields.");
    }

    private static void SharedStrictReader_RejectsUnknownField()
    {
        const string json = "{\"schema\":\"mx.resource-mapping-profile.v2\",\"legacyDeliveryMode\":\"internal\"}";
        ResourceMappingProfileReadResult result = ResourceMappingProfileJson.ReadStrict(json);
        Require(!result.Success && HasCode(result.Diagnostics, "ProfileJsonInvalid"), "Shared strict reader should reject removed or unknown fields.");
    }

    private static void SharedStrictReader_RejectsMissingRequiredFields()
    {
        ResourceMappingProfileReadResult missingRoot = ResourceMappingProfileJson.ReadStrict(
            "{\"schema\":\"mx.resource-mapping-profile.v2\",\"profileId\":\"p\",\"catalogId\":\"c\",\"entries\":[],\"bundleRules\":[],\"catalogMounts\":[],\"preloadGroups\":[]}");
        Require(!missingRoot.Success && HasCode(missingRoot.Diagnostics, "ProfileJsonInvalid"), "Strict reader should reject a missing required top-level property.");

        string valid = new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(CreateValidProfile());
        string missingEntryField = valid.Replace("\"deliveryIntent\":\"internal\",", string.Empty, StringComparison.Ordinal);
        ResourceMappingProfileReadResult missingNested = ResourceMappingProfileJson.ReadStrict(missingEntryField);
        Require(!missingNested.Success && HasCode(missingNested.Diagnostics, "ProfileJsonInvalid"), "Strict reader should reject a missing required nested property.");

        string nullMetadata = valid.Replace("\"metadata\":{}", "\"metadata\":null", StringComparison.Ordinal);
        Require(!ResourceMappingProfileJson.ReadStrict(nullMetadata).Success, "Strict reader should reject null for a required string map.");
        string nullLabels = valid.Replace("\"labels\":[]", "\"labels\":null", StringComparison.Ordinal);
        Require(!ResourceMappingProfileJson.ReadStrict(nullLabels).Success, "Strict reader should reject null for a required array.");
        string numericEnum = valid.Replace("\"deliveryIntent\":\"internal\"", "\"deliveryIntent\":0", StringComparison.Ordinal);
        Require(!ResourceMappingProfileJson.ReadStrict(numericEnum).Success, "Strict reader should reject numeric enum values.");

        string[] aliases = { "model", "gameObject", "Texture", "sprite", "Audio", "Animation", "avatarMask", "SceneAsset", "material", "text", "textAsset", "panelSettings", "visualTreeAsset", "styleSheet", "font", "string", "object" };
        for (int i = 0; i < aliases.Length; i++)
        {
            ResourceMappingProfileV2 alias = CreateValidProfile();
            alias.Entries[0].ResourceKey.TypeId = aliases[i];
            Require(HasCode(ResourceMappingProfileValidator.Validate(alias), "ResourceTypeAliasRejected"), "Non-canonical built-in TypeId should be rejected: " + aliases[i] + ".");
        }
    }

    private static void PersistedTypeAlias_IsRejected()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        profile.Entries[0].ResourceKey.TypeId = "texture";
        Require(HasCode(ResourceMappingProfileValidator.Validate(profile), "ResourceTypeAliasRejected"), "Persisted TypeId alias should be rejected.");
    }

    private static void InvalidCatalogMountPath_IsRejected()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        profile.CatalogMounts[0].RelativeCatalogPath = "../outside.json";
        Require(HasCode(ResourceMappingProfileValidator.Validate(profile), "CatalogMountPathInvalid"), "Escaping Catalog path should be rejected.");

        profile = CreateValidProfile();
        profile.CatalogMounts[0].ExpectedCatalogFingerprint = "SHA256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        Require(HasCode(ResourceMappingProfileValidator.Validate(profile), "CatalogFingerprintInvalid"), "Catalog fingerprint prefix should match canonical schema casing.");
    }

    private static void InvalidSourceLocator_IsRejected()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        profile.Entries[0].SourceLocator.UnityGuid = string.Empty;
        Require(HasCode(ResourceMappingProfileValidator.Validate(profile), "UnityGuidMissing"), "Unity asset source should require a GUID.");

        profile = CreateValidProfile();
        profile.Entries[0].SourceLocator = new ResourceMappingSourceLocator
        {
            Kind = ResourceMappingSourceLocatorKind.ImportedPackageFile,
            PackageRelativePath = "../escape.asset",
            ContentHash = "sha256:fixture"
        };
        Require(HasCode(ResourceMappingProfileValidator.Validate(profile), "PackageRelativePathInvalid"), "Imported package source path should not escape its root.");
    }

    private static void DeliveryMatrix_IsEnforced()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        profile.Entries[0].ExternalCatalogBinding = new ResourceMappingExternalCatalogBinding { CatalogMountId = "external.character" };
        profile.Entries[1].BundleIntent = new ResourceMappingBundleIntent { RuleId = "common.assets" };
        ResourceMappingValidationReport report = ResourceMappingProfileValidator.Validate(profile);
        Require(HasCode(report, "ExternalBindingForbidden"), "Internal entry should reject external binding.");
        Require(HasCode(report, "BundleIntentForbidden"), "External entry should reject Bundle intent.");
    }

    private static void Aggregator_ReplacesOnlyOwnedCompleteSnapshot()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        var replacement = new ResourceMappingProducerSnapshot { ProducerId = "domain.character" };
        replacement.Entries.Add(CreateExternalEntry("character.clip.replaced", "character.clip.replaced"));

        ResourceMappingAggregationResult result = new ResourceMappingProfileAggregator()
            .ApplyCompleteSnapshot(profile, "rev-a", "rev-a", replacement);

        Require(result.Success, "Owned complete snapshot should apply.");
        Require(result.Profile.Entries.Count == 2, "Complete snapshot should replace only the producer's rows.");
        Require(result.Profile.Entries.Exists(entry => entry.AuthoringRecordId == "character.clip.replaced"), "Replacement row should exist.");
        Require(result.Profile.BundleRules.Count == 1, "Other producer policy rows should remain.");
        Require(profile.Entries.Exists(entry => entry.AuthoringRecordId == "character.clip.source"), "Input Profile must remain unchanged.");
        Require(result.AcceptedRevision == new GlobalResourceBuildPlanCompiler().ComputeProfileRevision(result.Profile), "Successful aggregation should return the new canonical Profile revision.");
    }

    private static void Aggregator_RevisionConflictDoesNotMutateProfile()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        var snapshot = new ResourceMappingProducerSnapshot { ProducerId = "domain.character" };
        ResourceMappingAggregationResult result = new ResourceMappingProfileAggregator()
            .ApplyCompleteSnapshot(profile, "rev-current", "rev-stale", snapshot);
        Require(!result.Success && result.ErrorCode == ResourceMappingAggregationErrorCodes.RevisionConflict, "Stale revision should fail with typed conflict.");
        Require(profile.Entries.Count == 2, "Revision conflict must not mutate current Profile.");
    }

    private static void Aggregator_CrossOwnerKeyCollisionIsRejected()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        var snapshot = new ResourceMappingProducerSnapshot { ProducerId = "domain.character" };
        ResourceMappingEntry collision = CreateExternalEntry("character.collision", "ui.button.normal");
        collision.ResourceKey.TypeId = "Texture2D";
        snapshot.Entries.Add(collision);
        ResourceMappingAggregationResult result = new ResourceMappingProfileAggregator()
            .ApplyCompleteSnapshot(profile, "rev", "rev", snapshot);
        Require(!result.Success && HasCode(result.Validation, "ResourceKeyDuplicate"), "Cross-owner ResourceKey collision should block aggregation.");
    }

    private static void Aggregator_OwnershipMismatchIsRejected()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        var snapshot = new ResourceMappingProducerSnapshot { ProducerId = "domain.character" };
        ResourceMappingEntry entry = CreateExternalEntry("character.bad-owner", "character.bad-owner");
        entry.ProducerReceipt.ProducerId = "other.owner";
        snapshot.Entries.Add(entry);
        ResourceMappingAggregationResult result = new ResourceMappingProfileAggregator()
            .ApplyCompleteSnapshot(profile, "rev", "rev", snapshot);
        Require(!result.Success && result.ErrorCode == ResourceMappingAggregationErrorCodes.ProducerOwnershipMismatch, "Mixed ownership snapshot should be rejected.");
    }

    private static void Compiler_ProducesDeterministicCanonicalPlan()
    {
        ResourceMappingProfileV2 first = CreateValidProfile();
        ResourceMappingProfileV2 second = CreateValidProfile();
        second.Entries[1].Labels.Add("combat");
        second.Entries[1].Labels.Add("combat");
        first.Entries[1].Labels.Add("combat");
        second.Entries.Reverse();
        second.BundleRules.Reverse();

        var compiler = new GlobalResourceBuildPlanCompiler();
        GlobalResourceBuildPlanCompilation firstResult = compiler.Compile(first, "StandaloneOSX");
        GlobalResourceBuildPlanCompilation secondResult = compiler.Compile(second, "StandaloneOSX");
        Require(firstResult.Plan.Success && secondResult.Plan.Success, "Canonical fixtures should compile.");
        Require(firstResult.Plan.MappingInputFingerprint == secondResult.Plan.MappingInputFingerprint, "Input fingerprint should ignore source ordering and duplicate set values.");
        Require(firstResult.Plan.BuildPlanFingerprint == secondResult.Plan.BuildPlanFingerprint, "Plan fingerprint should be deterministic.");
        Require(firstResult.CanonicalPlanJson == secondResult.CanonicalPlanJson, "Canonical plan bytes should be identical.");
        Require(firstResult.CanonicalPlanJson.EndsWith("\n", StringComparison.Ordinal), "Canonical JSON should use one trailing LF.");
        Require(firstResult.CanonicalPlanJson[0] == '{', "Canonical JSON should not contain a BOM.");
        Require(firstResult.Plan.MappingInputFingerprint == compiler.ComputeProfileRevision(first), "Compiler mapping fingerprint and optimistic Profile revision should share one canonical input.");
    }

    private static void Compiler_ReceiptChangesMappingFingerprint()
    {
        ResourceMappingProfileV2 first = CreateValidProfile();
        ResourceMappingProfileV2 second = CreateValidProfile();
        second.Entries[0].ProducerReceipt.SourceRecordFingerprint = "sha256:changed";
        var compiler = new GlobalResourceBuildPlanCompiler();
        GlobalResourceBuildPlanCompilation firstResult = compiler.Compile(first, "StandaloneOSX");
        GlobalResourceBuildPlanCompilation secondResult = compiler.Compile(second, "StandaloneOSX");
        Require(firstResult.Plan.MappingInputFingerprint != secondResult.Plan.MappingInputFingerprint, "Producer receipt must participate in mapping fingerprint.");
    }

    private static void Compiler_RejectsDuplicateInternalGlobalRouteEvenWithOverride()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        ResourceMappingEntry duplicate = JsonSerializer.Deserialize<ResourceMappingEntry>(
            JsonSerializer.Serialize(profile.Entries[0], CreateJsonOptions()), CreateJsonOptions());
        duplicate.ProducerReceipt = Receipt("resource-manager.other", "duplicate-internal");
        duplicate.AuthoringRecordId = "duplicate.internal";
        duplicate.ResourceKey.PackageId = "another.package";
        duplicate.OverrideIntent = ResourceMappingOverrideIntent.Allow;
        profile.Entries.Add(duplicate);
        GlobalResourceBuildPlanCompilation result = new GlobalResourceBuildPlanCompiler().Compile(profile, "StandaloneOSX");
        Require(!result.Plan.Success && HasCode(result.Plan.Diagnostics, "CatalogLocalGlobalRouteDuplicate"), "Primary Catalog internal rows must not use override to bypass package-less route uniqueness.");
    }

    private static void Compiler_RejectsDuplicateExternalRouteWithinOneMount()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        ResourceMappingEntry duplicate = CreateExternalEntry("character.clip.source.duplicate", "character.clip.idle");
        duplicate.ResourceKey.PackageId = "another.package";
        duplicate.OverrideIntent = ResourceMappingOverrideIntent.Allow;
        profile.Entries.Add(duplicate);
        GlobalResourceBuildPlanCompilation result = new GlobalResourceBuildPlanCompiler().Compile(profile, "StandaloneOSX");
        Require(!result.Plan.Success && HasCode(result.Plan.Diagnostics, "CatalogLocalGlobalRouteDuplicate"), "One CatalogMount cannot contain duplicate package-less routes even with override.");
    }

    private static void Compiler_RejectsOldSchemaAndUnknownDependencies()
    {
        ResourceMappingProfileV2 old = CreateValidProfile();
        old.Schema = "mx.resource-mapping-profile.v1";
        GlobalResourceBuildPlanCompilation oldResult = new GlobalResourceBuildPlanCompiler().Compile(old, "StandaloneOSX");
        Require(!oldResult.Plan.Success && HasCode(oldResult.Plan.Diagnostics, "SchemaUnsupported"), "Compiler should reject every old schema.");

        ResourceMappingProfileV2 missing = CreateValidProfile();
        missing.Entries[0].Dependencies.Add(Key("missing.dependency", "Texture2D"));
        GlobalResourceBuildPlanCompilation missingResult = new GlobalResourceBuildPlanCompiler().Compile(missing, "StandaloneOSX");
        Require(!missingResult.Plan.Success && HasCode(missingResult.Plan.Diagnostics, "DependencyUnknown"), "Compiler should reject unresolved complete dependency keys.");
    }

    private static void Compiler_InvalidShapeReturnsDiagnosticsInsteadOfThrowing()
    {
        GlobalResourceBuildPlanCompilation missing = new GlobalResourceBuildPlanCompiler().Compile(null, "StandaloneOSX");
        Require(!missing.Plan.Success && HasCode(missing.Plan.Diagnostics, "ProfileMissing"), "Missing Profile should return a typed diagnostic.");

        ResourceMappingProfileV2 invalid = CreateValidProfile();
        invalid.Entries = null;
        GlobalResourceBuildPlanCompilation invalidResult = new GlobalResourceBuildPlanCompiler().Compile(invalid, "StandaloneOSX");
        Require(!invalidResult.Plan.Success && HasCode(invalidResult.Plan.Diagnostics, "EntriesMissing"), "Missing entry array should not be normalized into a valid Profile.");
    }

    private static void Compiler_EnforcesGlobalOverrideParity()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        ResourceMappingEntry collision = CreateExternalEntry("character.ui.override", "ui.button.normal");
        collision.ResourceKey.TypeId = "Texture2D";
        collision.ResourceKey.PackageId = "character.package";
        collision.OverrideIntent = ResourceMappingOverrideIntent.Forbid;
        profile.Entries.Add(collision);
        GlobalResourceBuildPlanCompilation blocked = new GlobalResourceBuildPlanCompiler().Compile(profile, "StandaloneOSX");
        Require(!blocked.Plan.Success && HasCode(blocked.Plan.Diagnostics, "GlobalRouteOverrideRequired"), "Later global route collision should require explicit override.");

        collision.OverrideIntent = ResourceMappingOverrideIntent.Allow;
        GlobalResourceBuildPlanCompilation allowed = new GlobalResourceBuildPlanCompiler().Compile(profile, "StandaloneOSX");
        Require(allowed.Plan.Success, "Same-TypeId later route should compile with explicit allow override.");
    }

    private static void Compiler_EmitsBundleAndMountMembership()
    {
        GlobalResourceBuildPlanCompilation result = new GlobalResourceBuildPlanCompiler().Compile(CreateValidProfile(), "StandaloneOSX");
        Require(result.Plan.Success, "Valid fixture should compile.");
        Require(result.Plan.Bundles.Count == 1 && result.Plan.Bundles[0].ResourceKeys.Count == 1, "Internal entry should belong to exactly one compiled Bundle.");
        Require(result.Plan.CatalogMounts.Count == 1 && result.Plan.CatalogMounts[0].AllowlistedKeys.Count == 1, "External entry should be allowlisted by exactly one concrete mount.");
        Require(result.Plan.CatalogMounts[0].AllowlistedKeys[0].OverrideIntent == ResourceMappingOverrideIntent.Forbid,
            "Compiled CatalogMount allowlist must freeze override intent with the complete ResourceKey.");
        Require(result.Plan.Counts.Internal == 1 && result.Plan.Counts.External == 1, "Delivery counts should be explicit.");
    }

    private static void Compiler_EnforcesBundleEmissionPolicy()
    {
        ResourceMappingProfileV2 invalidCompression = CreateValidProfile();
        invalidCompression.BundleRules[0].Compression = "brotli";
        Require(HasCode(new GlobalResourceBuildPlanCompiler().Compile(invalidCompression, "StandaloneOSX").Plan.Diagnostics, "BundleCompressionUnsupported"),
            "Unsupported Bundle compression must fail before Unity emission.");
        invalidCompression.BundleRules[0].Compression = "LZ4";
        Require(HasCode(new GlobalResourceBuildPlanCompiler().Compile(invalidCompression, "StandaloneOSX").Plan.Diagnostics, "BundleCompressionUnsupported"),
            "Bundle compression casing must match the canonical v2 schema.");

        ResourceMappingProfileV2 mismatchedTarget = CreateValidProfile();
        mismatchedTarget.BundleRules[0].BuildTarget = "StandaloneWindows64";
        Require(HasCode(new GlobalResourceBuildPlanCompiler().Compile(mismatchedTarget, "StandaloneOSX").Plan.Diagnostics, "BundleBuildTargetMismatch"),
            "A rule may use ActiveBuildTarget or the requested concrete target only.");

        ResourceMappingProfileV2 mixed = CreateValidProfile();
        mixed.BundleRules.Add(new ResourceMappingBundleRule
        {
            ProducerReceipt = Receipt("resource-manager.profile-policy", "bundle-mixed"), AuthoringRecordId = "bundle.mixed",
            Id = "mixed", BundleName = "mixed", Compression = "lzma", BuildTarget = "ActiveBuildTarget", AllowEmpty = true
        });
        Require(HasCode(new GlobalResourceBuildPlanCompiler().Compile(mixed, "StandaloneOSX").Plan.Diagnostics, "MixedBundleCompressionUnsupported"),
            "Mixed compression must fail closed because Unity applies compression per build invocation.");
    }

    private static void Compiler_RejectsProviderDataAuthority()
    {
        ResourceMappingProfileV2 profile = CreateValidProfile();
        profile.Entries[0].ProviderData["providerId"] = "proxy";
        GlobalResourceBuildPlanCompilation result = new GlobalResourceBuildPlanCompiler().Compile(profile, "StandaloneOSX");
        Require(!result.Plan.Success && HasCode(result.Plan.Diagnostics, "ProviderDataAuthorityForbidden"), "ProviderData cannot restore Provider routing authority.");
    }

    private static void ResourceManagerProductionState_UsesSharedPlan()
    {
        bool rejectedImplicitTarget = false;
        try
        {
            EditorServer.ReadResourceMappingState(Directory.GetCurrentDirectory(), "ActiveBuildTarget");
        }
        catch (ArgumentException)
        {
            rejectedImplicitTarget = true;
        }
        Require(rejectedImplicitTarget, "Resource Manager must reject an implicit build target that can diverge from Unity artifact emission.");

        object preview = EditorServer.ReadResourceMappingState(Directory.GetCurrentDirectory(), "StandaloneOSX");
        JsonElement json = JsonSerializer.SerializeToElement(preview, CreateJsonOptions());
        Require(json.GetProperty("productionActive").GetBoolean() && !json.GetProperty("readOnly").GetBoolean(), "Resource Manager must expose the active production Profile.");
        Require(json.GetProperty("success").GetBoolean(), "Checked-in production Profile should compile.");
        Require(json.GetProperty("effectiveBuildTarget").GetString() == "StandaloneOSX", "Preview should expose the concrete build target used by the shared compiler.");
        Require(json.GetProperty("mappingInputFingerprint").GetString().StartsWith("sha256:", StringComparison.Ordinal), "Resource Manager preview should expose shared compiler fingerprints.");
        int expectedRuntimeRows = 0;
        foreach (JsonElement entry in json.GetProperty("profile").GetProperty("entries").EnumerateArray())
        {
            string deliveryIntent = entry.GetProperty("deliveryIntent").GetString();
            if (string.Equals(deliveryIntent, "internal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(deliveryIntent, "external", StringComparison.OrdinalIgnoreCase))
                expectedRuntimeRows++;
        }
        Require(json.GetProperty("plan").GetProperty("entries").GetArrayLength() == expectedRuntimeRows,
            "Resource Manager should expose exactly the reviewed runtime rows from the shared plan.");
    }

    private static void BuildTargetResolver_RequiresConcreteUnityEvidence()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-resource-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            bool missingRejected = false;
            try { ResourceMappingBuildTargetResolver.Resolve(root, string.Empty); }
            catch (InvalidOperationException) { missingRejected = true; }
            Require(missingRejected, "An implicit build target must fail when Unity has not published concrete evidence.");
            Require(ResourceMappingBuildTargetResolver.Resolve(root, "Android") == "Android", "An explicit concrete target should not depend on a local report.");

            string reportPath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceBuildReports", "global_resource_build_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, "{\"schemaVersion\":1,\"buildTarget\":\"StandaloneWindows64\"}");
            bool staleSchemaRejected = false;
            try { ResourceMappingBuildTargetResolver.Resolve(root, "ActiveBuildTarget"); }
            catch (InvalidOperationException) { staleSchemaRejected = true; }
            Require(staleSchemaRejected, "A stale build-report schema must not become current Unity target evidence.");
            File.WriteAllText(reportPath, "{\"schemaVersion\":2,\"buildTarget\":\"StandaloneWindows64\"}");
            Require(ResourceMappingBuildTargetResolver.Resolve(root, "ActiveBuildTarget") == "StandaloneWindows64",
                "The legacy ActiveBuildTarget sentinel should resolve only through the latest Unity-owned report.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void GlobalBuildStatus_RequiresOneMatchingArtifactGeneration()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-resource-status-" + Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
        string reportPath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceBuildReports", "global_resource_build_report.json");
        string streamingRoot = Path.Combine(root, "Assets", "StreamingAssets");
        string artifactSetPath = Path.Combine(streamingRoot, "MxFramework", "Resources", "global_resource_artifact_set.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            Directory.CreateDirectory(Path.GetDirectoryName(artifactSetPath));
            ResourceMappingProfileV2 profile = CreateValidProfile();
            var compiler = new GlobalResourceBuildPlanCompiler();
            File.WriteAllText(profilePath, compiler.SerializeCanonicalProfile(profile));
            GlobalResourceBuildPlanCompilation compilation = compiler.Compile(profile, "StandaloneOSX");
            Require(compilation.Plan.Success, "Build-status fixture Profile should compile.");

            string[] kinds = { "catalog", "catalogMounts", "preloadGroups", "bundleDependencies" };
            var artifactRows = new List<object>();
            for (int i = 0; i < kinds.Length; i++)
            {
                string relative = "MxFramework/Resources/fixture-" + kinds[i] + ".json";
                string fullPath = Path.Combine(streamingRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, "{\"kind\":\"" + kinds[i] + "\"}");
                artifactRows.Add(new { kind = kinds[i], relativePath = relative, sha256 = ComputeTestFileSha256(fullPath) });
            }

            const string sourceFingerprint = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            void WriteArtifactSet(IEnumerable<object> rows) => File.WriteAllText(artifactSetPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                profileId = compilation.Plan.ProfileId,
                catalogId = compilation.Plan.CatalogId,
                buildTarget = "StandaloneOSX",
                mappingInputFingerprint = compilation.Plan.MappingInputFingerprint,
                buildPlanFingerprint = compilation.Plan.BuildPlanFingerprint,
                sourceInputFingerprint = sourceFingerprint,
                artifacts = rows
            }, CreateJsonOptions()));
            void WriteReport(string catalogId) => File.WriteAllText(reportPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                profileId = compilation.Plan.ProfileId,
                catalogId,
                buildTarget = "StandaloneOSX",
                mappingInputFingerprint = compilation.Plan.MappingInputFingerprint,
                buildPlanFingerprint = compilation.Plan.BuildPlanFingerprint,
                sourceInputFingerprint = sourceFingerprint
            }, CreateJsonOptions()));

            WriteArtifactSet(artifactRows);
            WriteReport(compilation.Plan.CatalogId);
            JsonElement ready = JsonSerializer.SerializeToElement(
                EditorServer.ReadGlobalResourceBuildStatus(root, string.Empty, CreateJsonOptions(), "StandaloneOSX"), CreateJsonOptions());
            Require(ready.GetProperty("state").GetString() == "readyForOfflineVerify", "Matching report and artifact identities should be ready for Offline verification.");

            WriteReport("wrong.catalog");
            JsonElement staleReport = JsonSerializer.SerializeToElement(
                EditorServer.ReadGlobalResourceBuildStatus(root, string.Empty, CreateJsonOptions(), "StandaloneOSX"), CreateJsonOptions());
            Require(staleReport.GetProperty("state").GetString() == "needsUnityBuild" && staleReport.GetProperty("stale").GetBoolean(),
                "A report with a different Catalog identity must never validate the artifact generation.");

            WriteReport(compilation.Plan.CatalogId);
            var duplicatedPathRows = new List<object>(artifactRows);
            duplicatedPathRows[1] = new
            {
                kind = "catalogMounts",
                relativePath = "MxFramework/Resources/fixture-catalog.json",
                sha256 = ComputeTestFileSha256(Path.Combine(streamingRoot, "MxFramework", "Resources", "fixture-catalog.json"))
            };
            WriteArtifactSet(duplicatedPathRows);
            JsonElement duplicatePath = JsonSerializer.SerializeToElement(
                EditorServer.ReadGlobalResourceBuildStatus(root, string.Empty, CreateJsonOptions(), "StandaloneOSX"), CreateJsonOptions());
            Require(!duplicatePath.GetProperty("artifactSetValid").GetBoolean() && duplicatePath.GetProperty("state").GetString() == "needsUnityBuild",
                "A duplicate artifact path must be rejected exactly as runtime bootstrap rejects it.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string ComputeTestFileSha256(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return "sha256:" + Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static void CharacterCompiler_EmitsCanonicalOwnedSnapshot()
    {
        string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "MxFramework.Authoring", "samples", "character-codex-flow-test");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        CharacterAuthoringCompileResult compiled = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath
        });
        Require(compiled.ResourceMappingV2Diagnostics.Count == 0, "Character v2 producer should materialize the sample without diagnostics.");
        Require(compiled.ResourceMappingV2Snapshot.ProducerId == "character.package.codex-flow-test", "Character ownership should be package-scoped.");
        Require(compiled.ResourceMappingV2Snapshot.Entries.Count == package.ResourceCatalog.Entries.Count, "Character producer should emit one complete row per package resource.");
        Require(compiled.ResourceMappingV2Snapshot.Entries.FindAll(entry => entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal).Count == 2, "Sample should materialize two runtime internal resources.");
        Require(compiled.ResourceMappingV2Snapshot.Entries.FindAll(entry => entry.DeliveryIntent == ResourceMappingDeliveryIntent.EditorOnly).Count == 2, "Preview resources should remain editor-only and reserve no ResourceKey.");
        Require(compiled.ResourceMappingV2Snapshot.Entries.TrueForAll(entry => entry.ResourceKey == null ||
            (entry.ResourceKey.TypeId != "model" && entry.ResourceKey.TypeId != "texture")), "Character aliases must not persist in v2 output.");

        string json = JsonSerializer.Serialize(compiled.ResourceMappingV2Snapshot, CreateJsonOptions());
        Require(!json.Contains("packageResourceKey", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("projectResourceKey", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("runtimeResourceKey", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("providerId", StringComparison.OrdinalIgnoreCase), "v2 Character output must not retain Project/Provider/Package/Runtime key aliases.");
    }

    private static void CharacterProducer_MaterialDescriptorMapsFinalUnityAssetAndFailsClosed()
    {
        const string packageId = "combat_girl_sword";
        const string resourceKey = "char.combat_girl_sword.material.body";
        const string descriptorRelativePath = "resources/materials/Body/Body.material.json";
        const string descriptorImportPath = "Assets/MxFrameworkGenerated/CharacterPackages/combat_girl_sword/" + descriptorRelativePath;
        var package = new CharacterResourcePackage
        {
            Manifest = new CharacterPackageManifest { PackageId = packageId },
            ResourceCatalog = new CharacterPackageResourceCatalog
            {
                Entries = new List<CharacterPackageResourceEntry>
                {
                    new CharacterPackageResourceEntry
                    {
                        ResourceKey = resourceKey,
                        StableId = "resource.material.body",
                        TypeId = CharacterPackageResourceTypeIds.Material,
                        Usage = CharacterPackageResourceUsageIds.Material,
                        SourceFormat = CharacterPackageResourceFormatIds.MaterialJson,
                        RelativePath = descriptorRelativePath,
                        ImportHints = new CharacterPackageImportHint
                        {
                            TargetRelativePath = descriptorRelativePath
                        }
                    }
                }
            }
        };
        var mapping = new CharacterPackageResourceMapping
        {
            PackageId = packageId,
            Entries = new List<CharacterPackageResourceMappingEntry>
            {
                new CharacterPackageResourceMappingEntry
                {
                    PackageResourceKey = resourceKey,
                    TypeId = CharacterPackageResourceTypeIds.Material,
                    SourceFormat = CharacterPackageResourceFormatIds.MaterialJson,
                    ImportTargetPath = descriptorImportPath
                }
            }
        };

        CharacterResourceMappingV2Materialization materialized = CharacterResourceMappingV2Producer.Materialize(package, mapping);
        Require(!materialized.HasErrors, "materialJson should resolve its final generated Unity Material without diagnostics: "
            + JsonSerializer.Serialize(materialized.Diagnostics, CreateJsonOptions()));
        Require(materialized.Snapshot.Entries.Count == 1, "materialJson should emit exactly one runtime mapping row.");
        ResourceMappingEntry entry = materialized.Snapshot.Entries[0];
        Require(entry.SourceLocator.Kind == ResourceMappingSourceLocatorKind.GeneratedUnityAsset,
            "materialJson should remain a generated Unity asset locator.");
        Require(entry.SourceLocator.CachedPath == "Assets/MxFrameworkGenerated/CharacterPackages/combat_girl_sword/materials/combat_girl_sword_Body.mat",
            "materialJson must map to the final .mat created by the Unity importer, not the descriptor TextAsset.");
        Require(!entry.SourceLocator.CachedPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase),
            "Runtime Material source locators must never retain descriptor JSON paths.");

        package.ResourceCatalog.Entries[0].SourceFormat = CharacterPackageResourceFormatIds.Json;
        package.ResourceCatalog.Entries[0].RelativePath = "resources/materials/Body/Body.json";
        package.ResourceCatalog.Entries[0].ImportHints.TargetRelativePath = "resources/materials/Body/Body.json";
        mapping.Entries[0].SourceFormat = CharacterPackageResourceFormatIds.Json;
        mapping.Entries[0].ImportTargetPath = "Assets/MxFrameworkGenerated/CharacterPackages/combat_girl_sword/resources/materials/Body/Body.json";
        CharacterResourceMappingV2Materialization invalid = CharacterResourceMappingV2Producer.Materialize(package, mapping);
        Require(invalid.HasErrors && invalid.Snapshot.Entries.Count == 0,
            "A Material descriptor without a known final .mat materializer must fail closed instead of corrupting the Profile.");
        Require(invalid.Diagnostics.Exists(diagnostic => diagnostic.Code == "CharacterGeneratedMaterialPathInvalid"),
            "Invalid Material source path should report CharacterGeneratedMaterialPathInvalid.");
    }

    private static void AddCollisionMaterials(CharacterResourcePackage package, string firstName, string secondName)
    {
        string[] names = { firstName, secondName };
        for (int i = 0; i < names.Length; i++)
        {
            string key = "char." + package.Manifest.PackageId + ".material.probe_" + i;
            string relativePath = "resources/materials/" + i + "/" + names[i] + ".material.json";
            package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
            {
                ResourceKey = key,
                StableId = "charpkg." + package.Manifest.PackageId + ".resource.material.probe_" + i,
                TypeId = CharacterPackageResourceTypeIds.Material,
                Usage = CharacterPackageResourceUsageIds.Material,
                SourceFormat = CharacterPackageResourceFormatIds.MaterialJson,
                RelativePath = relativePath,
                ImportHints = new CharacterPackageImportHint { TargetRelativePath = relativePath }
            });
        }
    }

    private static void CharacterProducer_RejectsMaterialTargetCollisionsDeterministically()
    {
        foreach (string secondName in new[] { "Shared", "shared", "Shared_Detail" })
        {
            string firstName = secondName == "Shared_Detail" ? "Shared.Detail" : "Shared";
            var package = new CharacterResourcePackage
            {
                Manifest = new CharacterPackageManifest { PackageId = "collision_test" },
                ResourceCatalog = new CharacterPackageResourceCatalog()
            };
            AddCollisionMaterials(package, firstName, secondName);
            var mapping = new CharacterPackageResourceMapping();
            foreach (CharacterPackageResourceEntry source in package.ResourceCatalog.Entries)
                mapping.Entries.Add(new CharacterPackageResourceMappingEntry
                {
                    PackageResourceKey = source.ResourceKey,
                    ImportTargetPath = "Assets/MxFrameworkGenerated/CharacterPackages/collision_test/" + source.RelativePath
                });
            CharacterResourceMappingV2Materialization first = CharacterResourceMappingV2Producer.Materialize(package, mapping);
            Require(first.HasErrors && first.Diagnostics.Exists(d => d.Code == "CharacterGeneratedMaterialTargetCollision"),
                "Different material identities targeting the same sanitized Windows path must be rejected: " + firstName + ", " + secondName);
            package.ResourceCatalog.Entries.Reverse();
            mapping.Entries.Reverse();
            CharacterResourceMappingV2Materialization reordered = CharacterResourceMappingV2Producer.Materialize(package, mapping);
            Require(JsonSerializer.Serialize(first.Diagnostics, CreateJsonOptions()) == JsonSerializer.Serialize(reordered.Diagnostics, CreateJsonOptions()),
                "Collision diagnostics must be stable under catalog and mapping reorder.");
        }
    }

    private static void CharacterImport_MaterialTargetCollisionPreservesPublishedOutputs()
    {
        WithCharacterSyncFixture((root, profilePath, packagePath, package, normal) =>
        {
            Require(normal.UnityImportWritePlan.CanWriteToUnityProject, "Normal package must remain importable.");
            string materialPath = Path.Combine(root, normal.UnityImportWritePlan.TargetRootPath, "materials", package.Manifest.PackageId + "_Shared.mat");
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
            File.WriteAllText(materialPath, "existing published material");
            string configPath = Path.Combine(root, normal.UnityImportWritePlan.TargetRootPath, "config", "character_config_patch.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.WriteAllText(configPath, "existing published config");
            var before = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                before[file] = Convert.ToBase64String(File.ReadAllBytes(file));
            AddCollisionMaterials(package, "Shared", "Shared");
            CharacterAuthoringCompileResult blocked = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest { Package = package, PackageRootPath = packagePath });
            Require(blocked.GateReport.ImportBlocked && !blocked.UnityImportWritePlan.CanWriteToUnityProject,
                "Material target collision must block the compiler write plan before any publication.");
            Require(blocked.GateReport.Issues.Exists(i => i.Code == "CharacterGeneratedMaterialTargetCollision"),
                "The original collision diagnostic must reach the compile gate.");
            CharacterUnityImportReport report = CharacterUnityImportBridge.Execute(new CharacterUnityImportBridgeRequest
            {
                CompileResult = blocked, PackageRootPath = packagePath, ProjectRootPath = root
            });
            Require(!report.CanWriteToUnityProject && report.Operations.Count == 0,
                "Blocked import must execute no write operations.");
            ResourceMappingCharacterSyncResult sync = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, blocked, "StandaloneWindows64");
            Require(!sync.Success && !sync.Changed, "Blocked material mapping must not publish to Profile.");
            string[] after = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Require(after.Length == before.Count, "Blocked import must create no project outputs.");
            foreach (string file in after)
                Require(before.TryGetValue(file, out string bytes) && bytes == Convert.ToBase64String(File.ReadAllBytes(file)),
                    "Blocked import must preserve existing output bytes: " + file);
        });
    }

    private static void CharacterSync_RejectsCompileBlockedSnapshots()
    {
        foreach (bool exportBlocked in new[] { false, true })
        {
            WithCharacterSyncFixture((root, profilePath, packagePath, package, compiled) =>
            {
                CharacterAuthoringValidationGate gate = exportBlocked
                    ? CharacterAuthoringValidationGate.ExportBlocked : CharacterAuthoringValidationGate.ImportBlocked;
                var issue = new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = gate,
                    Code = "ORIGINAL_COMPILE_BLOCK",
                    SourcePath = "resource_catalog.json",
                    SourceObjectPath = "entries[0]",
                    Field = "sourceFormat",
                    Message = "Original compiler diagnostic.",
                    SuggestedFix = "Original compiler suggested fix."
                };
                compiled.GateReport.ExportBlocked = exportBlocked;
                compiled.GateReport.ImportBlocked = !exportBlocked;
                compiled.GateReport.Issues.Add(issue);
                byte[] before = File.ReadAllBytes(profilePath);
                string revision = new ResourceMappingProfileFileAggregator().Read(profilePath).Revision;

                ResourceMappingCharacterSyncResult result = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneWindows64");

                Require(!result.Success && !result.Changed && result.ErrorCode == "RESOURCE_MAPPING_CHARACTER_COMPILE_BLOCKED",
                    "Both compiler blocking gates must reject a complete, otherwise valid snapshot.");
                Require(result.CompileDiagnostics.Contains(issue), "Original compiler diagnostic payload must be preserved.");
                Require(Convert.ToBase64String(File.ReadAllBytes(profilePath)) == Convert.ToBase64String(before), "Blocked sync must preserve all Profile bytes and every producer.");
                Require(new ResourceMappingProfileFileAggregator().Read(profilePath).Revision == revision, "Blocked sync must preserve the revision.");
            });
        }
    }

    private static void CharacterSync_RejectsUnsupportedFormatBeforePublishing()
    {
        WithCharacterSyncFixture((root, profilePath, packagePath, package, compiled) =>
        {
            Require(ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneWindows64").Success,
                "Valid compilation must establish a normal mapping before the fault.");
            byte[] before = File.ReadAllBytes(profilePath);
            CharacterPackageResourceEntry texture = package.ResourceCatalog.Entries.Find(entry => entry.ResourceKey == "char.codex_flow_test.texture.body_albedo");
            texture.SourceFormat = "wav";
            texture.ImportHints.TargetRelativePath = "resources/textures/rejected_sync_gate.wav";
            CharacterAuthoringCompileResult blocked = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
            {
                Package = package, PackageRootPath = packagePath
            });
            Require(blocked.GateReport.ImportBlocked && blocked.ResourceMappingV2Diagnostics.Count == 0,
                "Unsupported texture source format must be a compiler gate failure, independent of mapping diagnostics.");
            ResourceMappingCharacterSyncResult result = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, blocked, "StandaloneWindows64");
            Require(!result.Success && !result.Changed, "Actual compiler ImportBlocked must stop synchronization.");
            Require(result.CompileDiagnostics.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.UnsupportedResourceFormat
                && issue.Gate == CharacterAuthoringValidationGate.ImportBlocked && issue.Field == "sourceFormat"),
                "Sync must expose the original unsupported-format diagnostic and location.");
            Require(Convert.ToBase64String(File.ReadAllBytes(profilePath)) == Convert.ToBase64String(before), "The previously valid Profile must remain byte-identical.");
        });
    }

    private static void CharacterSync_RequiresCompileGateReport()
    {
        WithCharacterSyncFixture((root, profilePath, packagePath, package, compiled) =>
        {
            byte[] before = File.ReadAllBytes(profilePath);
            compiled.GateReport = null;
            ResourceMappingCharacterSyncResult result = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneWindows64");
            Require(!result.Success && result.ErrorCode == "RESOURCE_MAPPING_CHARACTER_COMPILE_GATE_MISSING", "Missing compiler gate evidence must fail closed.");
            Require(Convert.ToBase64String(File.ReadAllBytes(profilePath)) == Convert.ToBase64String(before), "Missing gate evidence must not publish a snapshot.");
        });
    }

    private static void CharacterSync_AllowsWarningsSpawnBlocksAndPendingImport()
    {
        foreach (bool spawnBlocked in new[] { false, true })
        {
            WithCharacterSyncFixture((root, profilePath, packagePath, package, compiled) =>
            {
                compiled.GateReport.SpawnBlocked = spawnBlocked;
                compiled.GateReport.WarningOnly = !spawnBlocked;
                compiled.GateReport.Issues.Add(new CharacterAuthoringValidationIssue
                {
                    Gate = spawnBlocked ? CharacterAuthoringValidationGate.SpawnBlocked : CharacterAuthoringValidationGate.WarningOnly,
                    Severity = spawnBlocked ? CharacterAuthoringValidationSeverity.Error : CharacterAuthoringValidationSeverity.Warning,
                    Code = "NON_IMPORT_BLOCKING_DIAGNOSTIC"
                });
                Require(!Directory.Exists(Path.Combine(root, "Assets/MxFrameworkGenerated/CharacterPackages")), "Fixture must contain no imported Character assets.");
                ResourceMappingCharacterSyncResult first = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneWindows64");
                Require(first.Success && first.Changed, "Warning-only or SpawnBlocked must not block a valid pending-import mapping.");
                Require(first.CompileDiagnostics.Exists(issue => issue.Code == "NON_IMPORT_BLOCKING_DIAGNOSTIC"), "Non-blocking compiler diagnostics must remain visible.");
                ResourceMappingCharacterSyncResult second = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneWindows64");
                Require(second.Success && !second.Changed, "An identical pending-import snapshot must remain a no-op.");
                Require(!Directory.Exists(Path.Combine(root, "Assets/MxFrameworkGenerated/CharacterPackages")), "Sync must not fabricate imported assets.");
            });
        }
    }

    private static void WithCharacterSyncFixture(Action<string, string, string, CharacterResourcePackage, CharacterAuthoringCompileResult> check)
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingCharacterGateTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(root, "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
        ResourceMappingProfileReadResult seed = ResourceMappingProfileJson.ReadStrict(File.ReadAllText("Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json"));
        Require(seed.Success, "Profile fixture seed must be valid.");
        seed.Profile.Entries.RemoveAll(entry => entry?.ProducerReceipt?.ProducerId == "character.package.codex-flow-test");
        File.WriteAllText(profilePath, new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(seed.Profile));
        try
        {
            string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "Tools/MxFramework.Authoring/samples/character-codex-flow-test");
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
            CharacterAuthoringCompileResult compiled = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest { Package = package, PackageRootPath = packagePath });
            Require(!compiled.GateReport.ExportBlocked && !compiled.GateReport.ImportBlocked, "Fixture must start from a non-blocked character compilation.");
            check(root, profilePath, packagePath, package, compiled);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void CharacterSync_ReplacesCompleteSnapshotAndRejectsStaleRevision()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingCharacterSyncTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
        ResourceMappingProfileReadResult checkedProfile = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(
            "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json"));
        Require(checkedProfile.Success, "Checked Profile should seed the realistic Character synchronization fixture.");
        checkedProfile.Profile.Entries.RemoveAll(entry =>
            string.Equals(entry?.ProducerReceipt?.ProducerId, "character.package.codex-flow-test", StringComparison.Ordinal));
        File.WriteAllText(profilePath, new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(checkedProfile.Profile));
        try
        {
            string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "MxFramework.Authoring", "samples", "character-codex-flow-test");
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
            CharacterAuthoringCompileResult compiled = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packagePath
            });

            ResourceMappingCharacterSyncResult first = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneOSX");
            Require(first.Success && first.Changed, "First Character sync should atomically update the production Profile: " + JsonSerializer.Serialize(first));
            Require(first.MappingInputFingerprint.StartsWith("sha256:", StringComparison.Ordinal), "Character sync should compile the aggregated Profile.");
            ResourceMappingProfileFileState firstState = new ResourceMappingProfileFileAggregator().Read(profilePath);
            Require(firstState.Success && firstState.Profile.Entries.FindAll(entry => entry.ProducerReceipt.ProducerId == compiled.ResourceMappingV2Snapshot.ProducerId).Count == 4, "Saved Profile should contain the complete Character snapshot.");
            Require(firstState.Profile.BundleRules.Exists(rule => rule.Id == CharacterResourceMappingV2ProducerContracts.DefaultBundleRuleId), "Central policy snapshot should own the Character Bundle rule.");
            Require(firstState.Profile.BundleRules.TrueForAll(rule => rule.BuildTarget == "ActiveBuildTarget"), "Character synchronization must not persist one invocation's concrete build target into reusable policy.");

            ResourceMappingCharacterSyncResult noOp = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneOSX");
            Require(noOp.Success && !noOp.Changed, "Unchanged complete Character snapshot should be a no-op.");
            ResourceMappingCharacterSyncResult otherTarget = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneWindows64");
            Require(otherTarget.Success && !otherTarget.Changed, "Changing the requested build target must not persist target-specific policy changes.");
            ResourceMappingProfileFileState crossTargetState = new ResourceMappingProfileFileAggregator().Read(profilePath);
            GlobalResourceBuildPlanCompilation windowsPlan = new GlobalResourceBuildPlanCompiler().Compile(crossTargetState.Profile, "StandaloneWindows64");
            Require(windowsPlan.Plan.Success && windowsPlan.Plan.BuildTarget == "StandaloneWindows64" && windowsPlan.Plan.Bundles.TrueForAll(bundle => bundle.BuildTarget == "StandaloneWindows64"),
                "Consecutive target compilation must resolve reusable ActiveBuildTarget policy to the newly requested target.");

            compiled.ResourceMappingV2Snapshot.Entries.RemoveAt(compiled.ResourceMappingV2Snapshot.Entries.Count - 1);
            ResourceMappingCharacterSyncResult staleRemoval = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneOSX");
            Require(staleRemoval.Success && staleRemoval.Changed, "Omitting an owned row from a complete snapshot should remove it.");
            ResourceMappingProfileFileState afterRemoval = new ResourceMappingProfileFileAggregator().Read(profilePath);
            Require(afterRemoval.Profile.Entries.FindAll(entry => entry.ProducerReceipt.ProducerId == compiled.ResourceMappingV2Snapshot.ProducerId).Count == 3, "Scoped stale-row deletion should persist exactly the replacement snapshot.");

            ResourceMappingProfileFileAggregationResult conflict = new ResourceMappingProfileFileAggregator().ApplyCompleteSnapshots(
                profilePath,
                firstState.Revision,
                new[] { compiled.ResourceMappingV2Snapshot },
                "StandaloneOSX");
            Require(!conflict.Success && conflict.ErrorCode == ResourceMappingAggregationErrorCodes.RevisionConflict, "File aggregator should reject stale optimistic revisions.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void CharacterUnityImport_SubmitsSnapshotThroughAggregator()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingCharacterImportTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
        ResourceMappingProfileReadResult checkedProfile = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(
            "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json"));
        Require(checkedProfile.Success, "Checked Profile should seed the realistic Character import fixture.");
        File.WriteAllText(profilePath, new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(checkedProfile.Profile));
        try
        {
            string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "MxFramework.Authoring", "samples", "character-codex-flow-test");
            int exit;
            TextWriter originalOutput = Console.Out;
            using (var capturedOutput = new StringWriter())
            {
                try
                {
                    Console.SetOut(capturedOutput);
                    exit = CharacterPackageCommands.Dispatch(new[]
                    {
                        "character", "import-unity", "--package", packagePath, "--project-root", root,
                        "--build-target", "StandaloneOSX"
                    }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
                }
                finally
                {
                    Console.SetOut(originalOutput);
                }
            }
            Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "Successful Character Unity import should include shadow v2 submission.");
            ResourceMappingProfileFileState state = new ResourceMappingProfileFileAggregator().Read(profilePath);
            Require(state.Success && state.Profile.Entries.FindAll(entry => entry.ProducerReceipt.ProducerId == "character.package.codex-flow-test").Count == 4, "Character import should persist the complete producer snapshot through the Profile Aggregator.");
            Require(state.Profile.Entries.Exists(entry => entry.ProducerReceipt.ProducerId != "character.package.codex-flow-test"), "Character import must preserve every other producer's rows.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void CharacterThenFirstPartySync_PreservesPackageOwnedRows()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingCrossProducerSyncTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
        File.Copy("Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json", profilePath);
        try
        {
            string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "MxFramework.Authoring", "samples", "character-codex-flow-test");
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
            CharacterAuthoringCompileResult compiled = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest { Package = package, PackageRootPath = packagePath });
            ResourceMappingCharacterSyncResult character = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(root, compiled, "StandaloneOSX");
            Require(character.Success, "Character producer should establish package-owned rows before cross-producer synchronization.");

            ResourceMappingFirstPartySyncResult firstParty = ResourceMappingFirstPartyCoordinator.Apply(
                root, MxFramework.Authoring.Cli.Program.CreateJsonOptions(), Directory.GetCurrentDirectory(), "StandaloneOSX");
            Require(firstParty.Success, "First-party synchronization should succeed against the same revision-bound Profile snapshot: " + JsonSerializer.Serialize(firstParty));
            ResourceMappingProfileFileState state = new ResourceMappingProfileFileAggregator().Read(profilePath);
            Require(state.Profile.Entries.FindAll(entry => entry.ProducerReceipt.ProducerId == "character.package.codex-flow-test").Count == 4,
                "Recurring first-party synchronization must preserve Character package rows owned outside its adapter set.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void FirstPartySync_ConsumesReviewedDecisionsWithoutAliases()
    {
        ResourceMappingFirstPartySyncResult result = ResourceMappingFirstPartyCoordinator.Apply(
            Directory.GetCurrentDirectory(), MxFramework.Authoring.Cli.Program.CreateJsonOptions(), "StandaloneOSX");
        Require(result.Success && !result.Changed, "Checked first-party snapshots should be a deterministic no-op after materialization.");
        Require(result.EntryCount >= 72, "Every first-party producer record should be materialized exactly once, including published VFX resources.");
        Require(result.InternalCount == result.EntryCount - result.EditorOnlyCount - result.ExcludedCount &&
                result.ExternalCount == 0 && result.EditorOnlyCount == 16 && result.ExcludedCount == 1,
            "Runtime repository assets should be internal, Character previews should remain editor-only, and the redundant proxy duplicate should be excluded.");
        Require(result.ProducerCount == 7 && result.CatalogMountCount == 0, "All named first-party producers should submit complete snapshots without retaining the generated proxy Catalog.");

        string profileJson = File.ReadAllText("Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json");
        ResourceMappingProfileReadResult read = ResourceMappingProfileJson.ReadStrict(profileJson);
        Require(read.Success && read.Profile.Entries.Count == result.EntryCount, "Checked production Profile should strictly round-trip all reviewed records.");
        Require(read.Profile.Entries.FindAll(entry => entry.ProducerReceipt.ProducerId == "character.package.000004").Count == 4,
            "Recurring first-party synchronization must preserve the complete Character package-owned snapshot.");
        Require(!profileJson.Contains("runtimeResourceKey", StringComparison.Ordinal) &&
                !profileJson.Contains("runtimeResourceType", StringComparison.Ordinal) &&
                !profileJson.Contains("runtimePackageId", StringComparison.Ordinal) &&
                !profileJson.Contains("sourceProviderId", StringComparison.Ordinal) &&
                !profileJson.Contains("deliveryMode", StringComparison.Ordinal),
            "v2 Profile must not retain proxy routing or legacy mapping aliases.");
    }

    private static void FirstPartyProducerAdapters_ReplaceCompleteOwnedSnapshots()
    {
        ResourceMappingProfileReadResult read = ResourceMappingProfileJson.ReadStrict(
            File.ReadAllText("Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json"));
        Require(read.Success, "Checked v2 Profile should load for producer replacement tests.");
        IReadOnlyList<IResourceMappingFirstPartyProducerAdapter> adapters = ResourceMappingFirstPartyProducerAdapters.CreateAll();
        Require(adapters.Count == 7, "Six repository-source domain adapters plus one central policy adapter should be registered; manual rows live only in the Profile.");
        for (int i = 0; i < adapters.Count; i++)
        {
            ResourceMappingProducerSnapshotResult built = adapters[i].BuildSnapshot(Directory.GetCurrentDirectory());
            Require(built.Success && built.Snapshot != null, "Producer adapter should build: " + adapters[i].ProducerId + ".");
            Require(built.Snapshot.Entries.TrueForAll(entry => entry.ProducerReceipt.ProducerId == adapters[i].ProducerId), "Adapter may claim only its own entries.");

            ResourceMappingProducerSnapshot shortened = built.Snapshot;
            if (shortened.Entries.Count > 0)
                shortened.Entries.RemoveAt(shortened.Entries.Count - 1);
            ResourceMappingAggregationResult replaced = new ResourceMappingProfileAggregator().ApplyCompleteSnapshot(
                read.Profile, "revision", "revision", shortened);
            Require(replaced.Success, "Complete snapshot replacement should succeed for " + adapters[i].ProducerId + ".");
            int expectedOwned = shortened.Entries.Count;
            Require(replaced.Profile.Entries.FindAll(entry => entry.ProducerReceipt.ProducerId == adapters[i].ProducerId).Count == expectedOwned,
                "Omitted rows must become owner-scoped stale deletions for " + adapters[i].ProducerId + ".");
        }
        ResourceMappingProducerSnapshotResult demo = new DemoRepositoryProducerAdapter().BuildSnapshot(Directory.GetCurrentDirectory(), read.Profile);
        Require(demo.Success && demo.Snapshot.Entries.Exists(entry => entry.ResourceKey?.Id.StartsWith("character.", StringComparison.Ordinal) == true && entry.PreloadGroups.Contains("SpawnCritical")),
            "The sole Demo producer must preserve SpawnCritical semantics for common character models.");
        Require(demo.Snapshot.Entries.Exists(entry => entry.ResourceKey?.Id.StartsWith("weapon.", StringComparison.Ordinal) == true && entry.PreloadGroups.Contains("EquipmentInitial")),
            "The sole Demo producer must preserve EquipmentInitial semantics for common weapons.");

        ResourceMappingProducerSnapshotResult vfx = new VfxRepositoryProducerAdapter().BuildSnapshot(Directory.GetCurrentDirectory(), read.Profile);
        Require(vfx.Success && vfx.Snapshot.Entries.Count >= 5,
            "The global VFX producer should own Status Aura resources and every Production/Ready published VFX binding.");
        Require(vfx.Snapshot.Entries.Exists(entry => entry.ResourceKey?.Id == "vfx.authored.sample.fireball_impact"),
            "Production/Ready published VFX must be projected into the global Resource Mapping Profile.");
        Require(vfx.Snapshot.Entries.TrueForAll(entry => entry.Labels.Contains("domain.vfx")),
            "Every globally mapped VFX resource must use the domain.vfx label.");

        ResourceMappingProducerSnapshotResult fairyGui =
            new FairyGuiRepositoryProducerAdapter().BuildSnapshot(Directory.GetCurrentDirectory(), read.Profile);
        Require(fairyGui.Success && fairyGui.Snapshot.Entries.FindAll(
                entry => entry.ResourceKey?.Id.StartsWith("ui.fairygui.player_guidance.", StringComparison.Ordinal) == true).Count == 2,
            "Player Guidance package bytes and atlas must both be owned by the FairyGUI resource producer.");
        VerifyFairyGuiAtlasPages(fairyGui);
        ResourceMappingProducerSnapshotResult policy =
            new ResourceMappingProfilePolicyProducerAdapter().BuildSnapshot(Directory.GetCurrentDirectory(), read.Profile);
        Require(policy.Success && policy.Snapshot.BundleRules.Exists(
                rule => rule.Id == "ui.fairygui.player_guidance"),
            "Central Resource Mapping policy must own the Player Guidance FairyGUI bundle rule.");
    }

    private static void FirstPartyProducerSet_DoesNotEmitCharacterPackageTombstones()
    {
        IReadOnlyList<IResourceMappingFirstPartyProducerAdapter> adapters = ResourceMappingFirstPartyProducerAdapters.CreateAll();
        for (int i = 0; i < adapters.Count; i++)
            Require(!adapters[i].ProducerId.StartsWith("character.package.", StringComparison.Ordinal),
                "Recurring first-party synchronization must never emit a one-shot empty Character package tombstone.");
    }

    private static void RepositoryProducerAdapter_DiscoversAddDeleteAndGuidPreservingMove()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingRepositoryProducerTests", Guid.NewGuid().ToString("N"));
        string domain = Path.Combine(root, "Assets", "Audio", "MxFramework", "Samples", "MagicEffects");
        Directory.CreateDirectory(domain);
        string first = Path.Combine(domain, "first.wav");
        string second = Path.Combine(domain, "second.ogg");
        File.WriteAllBytes(first, Array.Empty<byte>()); File.WriteAllText(first + ".meta", "guid: 11111111111111111111111111111111\n");
        File.WriteAllBytes(second, Array.Empty<byte>()); File.WriteAllText(second + ".meta", "guid: 22222222222222222222222222222222\n");
        try
        {
            var adapter = new AudioRepositoryProducerAdapter();
            ResourceMappingProducerSnapshotResult initial = adapter.BuildSnapshot(root);
            Require(initial.Success && initial.Snapshot.Entries.Count == 2, "Repository adapter should discover newly added domain assets.");

            string profileDirectory = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceProfiles");
            Directory.CreateDirectory(profileDirectory);
            var priorProfile = new ResourceMappingProfileV2 { ProfileId = "rename.fixture", CatalogId = "rename.fixture" };
            priorProfile.Entries.AddRange(initial.Snapshot.Entries);
            File.WriteAllText(Path.Combine(profileDirectory, "global_resource_mapping_profile.v2.json"),
                new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(priorProfile));

            File.Delete(first); File.Delete(first + ".meta");
            string movedDirectory = Path.Combine(domain, "Moved");
            Directory.CreateDirectory(movedDirectory);
            string moved = Path.Combine(movedDirectory, "renamed.ogg");
            File.Move(second, moved); File.Move(second + ".meta", moved + ".meta");
            ResourceMappingProducerSnapshotResult changed = adapter.BuildSnapshot(root, priorProfile);
            Require(changed.Success && changed.Snapshot.Entries.Count == 1, "Complete snapshot should omit deleted domain assets.");
            ResourceMappingEntry entry = changed.Snapshot.Entries[0];
            Require(entry.ResourceKey.Id == "audio.magic_effect.second" && entry.SourceLocator.UnityGuid == "22222222222222222222222222222222" && entry.SourceLocator.CachedPath.EndsWith("Moved/renamed.ogg", StringComparison.Ordinal),
                "A GUID-preserving rename must retain ResourceKey identity while refreshing the canonical source path.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void RepositoryProducerAdapter_AmbiguousPriorGuidFailsClosed()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingAmbiguousGuidTests", Guid.NewGuid().ToString("N"));
        string domain = Path.Combine(root, "Assets", "Audio", "MxFramework", "Samples", "MagicEffects");
        Directory.CreateDirectory(domain);
        string asset = Path.Combine(domain, "shared.wav");
        File.WriteAllBytes(asset, Array.Empty<byte>()); File.WriteAllText(asset + ".meta", "guid: 33333333333333333333333333333333\n");
        try
        {
            var profile = new ResourceMappingProfileV2();
            ResourceMappingEntry first = CreateInternalUnityEntry("audio.global-profile", "audio.first", "33333333333333333333333333333333");
            ResourceMappingEntry second = CreateInternalUnityEntry("audio.global-profile", "audio.second", "33333333333333333333333333333333");
            profile.Entries.Add(first); profile.Entries.Add(second);
            ResourceMappingProducerSnapshotResult result = new AudioRepositoryProducerAdapter().BuildSnapshot(root, profile);
            Require(!result.Success && result.Diagnostics.Exists(item => item.Code == "ProducerSourceGuidAmbiguous"),
                "Multiple prior identities for one owner/GUID must block deterministic inheritance.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void RepositoryProducerAdapter_MissingRootFailsClosed()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingMissingProducerRootTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ResourceMappingProducerSnapshotResult result = new AudioRepositoryProducerAdapter().BuildSnapshot(root);
            Require(!result.Success && result.Diagnostics.Exists(item => item.Code == "ProducerSourceRootMissing"),
                "A missing producer authority root must fail closed instead of returning a successful empty complete snapshot.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void FairyGuiProducerAdapter_MissingRequiredPackageBytesFailsClosed()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingMissingFairyGuiPackageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets", "Bundles", "FGUI"));
        try
        {
            ResourceMappingProducerSnapshotResult result = new FairyGuiRepositoryProducerAdapter().BuildSnapshot(root);
            Require(!result.Success && result.Diagnostics.FindAll(item => item.Code == "ProducerSourceMissing").Count == 21 &&
                    result.Diagnostics.FindAll(item => item.Path.EndsWith("_fui.bytes", StringComparison.Ordinal)).Count == 11 &&
                    result.Diagnostics.FindAll(item => item.Path.EndsWith("_atlas0.png", StringComparison.Ordinal)).Count == 10,
                "Every required FairyGUI package bytes/atlas payload must independently block the complete snapshot.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void FileAggregator_ConcurrentWritersDoNotLoseUpdates()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingConcurrentWriterTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(root, "profile.v2.json");
        Directory.CreateDirectory(root);
        ResourceMappingProfileV2 profile = CreateValidProfile();
        File.WriteAllText(profilePath, new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(profile));
        try
        {
            var fileAggregator = new ResourceMappingProfileFileAggregator();
            string revision = fileAggregator.Read(profilePath).Revision;
            ResourceMappingProducerSnapshot first = new() { ProducerId = "domain.character" };
            first.Entries.Add(CreateExternalEntry("character.concurrent.first", "character.concurrent.first"));
            ResourceMappingProducerSnapshot second = new() { ProducerId = "domain.character" };
            second.Entries.Add(CreateExternalEntry("character.concurrent.second", "character.concurrent.second"));
            using var start = new ManualResetEventSlim(false);
            Task<ResourceMappingProfileFileAggregationResult> left = Task.Run(() =>
            {
                start.Wait();
                return new ResourceMappingProfileFileAggregator().ApplyCompleteSnapshots(profilePath, revision, new[] { first }, "StandaloneOSX");
            });
            Task<ResourceMappingProfileFileAggregationResult> right = Task.Run(() =>
            {
                start.Wait();
                return new ResourceMappingProfileFileAggregator().ApplyCompleteSnapshots(profilePath, revision, new[] { second }, "StandaloneOSX");
            });
            start.Set();
            Task.WaitAll(left, right);
            int successes = (left.Result.Success ? 1 : 0) + (right.Result.Success ? 1 : 0);
            ResourceMappingProfileFileAggregationResult conflict = left.Result.Success ? right.Result : left.Result;
            Require(successes == 1, "Exactly one concurrent writer should commit.");
            Require(!conflict.Success && conflict.ErrorCode == ResourceMappingAggregationErrorCodes.RevisionConflict, "The losing concurrent writer should receive a typed write conflict.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void ResourceManagerManualSave_UsesProfileAsSoleAuthorityAndRejectsConcurrentStaleWriter()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ResourceMappingManualSaveTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(root, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
        ResourceMappingProfileV2 profile = CreateValidProfile();
        File.WriteAllText(profilePath, new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(profile));
        try
        {
            var fileAggregator = new ResourceMappingProfileFileAggregator();
            string revision = fileAggregator.Read(profilePath).Revision;
            var snapshot = new ResourceMappingProducerSnapshot { ProducerId = "resource-manager.manual" };
            ResourceMappingEntry replacement = profile.Entries[0];
            replacement.AuthoringRecordId = "manual.ui.button.replacement";
            replacement.ResourceKey.Id = "ui.button.replacement";
            snapshot.Entries.Add(replacement);

            var competingSnapshot = new ResourceMappingProducerSnapshot { ProducerId = "resource-manager.manual" };
            ResourceMappingEntry competing = CreateValidProfile().Entries[0];
            competing.AuthoringRecordId = "manual.ui.button.competing";
            competing.ResourceKey.Id = "ui.button.competing";
            competingSnapshot.Entries.Add(competing);
            using var start = new ManualResetEventSlim(false);
            Task<JsonElement> left = Task.Run(() =>
            {
                start.Wait();
                return JsonSerializer.SerializeToElement(EditorServer.SaveResourceMappingManualSnapshot(root, revision, snapshot, "StandaloneOSX", CreateJsonOptions()), CreateJsonOptions());
            });
            Task<JsonElement> right = Task.Run(() =>
            {
                start.Wait();
                return JsonSerializer.SerializeToElement(EditorServer.SaveResourceMappingManualSnapshot(root, revision, competingSnapshot, "StandaloneOSX", CreateJsonOptions()), CreateJsonOptions());
            });
            start.Set();
            Task.WaitAll(left, right);
            int successes = (left.Result.GetProperty("success").GetBoolean() ? 1 : 0) + (right.Result.GetProperty("success").GetBoolean() ? 1 : 0);
            Require(successes == 1, "Exactly one concurrent manual snapshot should commit to the sole persisted Profile authority.");
            ResourceMappingProfileFileState afterSave = fileAggregator.Read(profilePath);
            Require(afterSave.Profile.Entries.Exists(entry => entry.ResourceKey?.Id == "ui.button.replacement" || entry.ResourceKey?.Id == "ui.button.competing"), "The accepted complete manual snapshot should be the only persisted manual authority.");

            string acceptedProfile = File.ReadAllText(profilePath);
            snapshot.Entries[0].ResourceKey.Id = "ui.button.stale-write";
            JsonElement rejected = JsonSerializer.SerializeToElement(
                EditorServer.SaveResourceMappingManualSnapshot(root, revision, snapshot, "StandaloneOSX", CreateJsonOptions()),
                CreateJsonOptions());
            Require(!rejected.GetProperty("success").GetBoolean() &&
                    rejected.GetProperty("errorCode").GetString() == ResourceMappingAggregationErrorCodes.RevisionConflict,
                "Stale manual save should return the typed optimistic revision conflict.");
            Require(File.ReadAllText(profilePath) == acceptedProfile, "Rejected manual save must not mutate the aggregated Profile.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void CheckedInSchemaAndProductionProfileExist()
    {
        Require(File.Exists("Docs/Validation/RESOURCE_MAPPING_PROFILE_V2.schema.json"), "v2 JSON schema should be checked in.");
        Require(File.Exists("Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json"), "shadow v2 Profile should be checked in.");
    }

    private static ResourceMappingProfileV2 CreateValidProfile()
    {
        var profile = new ResourceMappingProfileV2
        {
            ProfileId = "global.default.shadow",
            CatalogId = "global.runtime"
        };
        profile.BundleRules.Add(new ResourceMappingBundleRule
        {
            ProducerReceipt = Receipt("resource-manager.profile-policy", "bundle-common"),
            AuthoringRecordId = "bundle.common.assets",
            Id = "common.assets",
            BundleName = "global.common.assets"
        });
        profile.CatalogMounts.Add(new ResourceMappingCatalogMount
        {
            ProducerReceipt = Receipt("resource-manager.profile-policy", "catalog-character"),
            AuthoringRecordId = "catalog.character",
            CatalogMountId = "external.character",
            CatalogId = "character.runtime",
            BootstrapKind = ResourceMappingCatalogBootstrapKind.StreamingAssetsCatalog,
            RelativeCatalogPath = "MxFramework/Resources/ExternalCatalogs/character.runtime.json",
            MountOrder = 10,
            ExpectedCatalogFingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        });
        profile.PreloadGroups.Add(new ResourceMappingPreloadGroup
        {
            ProducerReceipt = Receipt("resource-manager.profile-policy", "preload-boot"),
            AuthoringRecordId = "preload.boot",
            Id = "boot",
            ExplicitKeys = new List<ResourceMappingResourceKey>(),
            AllowEmpty = true
        });
        profile.Entries.Add(new ResourceMappingEntry
        {
            ProducerReceipt = Receipt("resource-manager.manual", "ui-button"),
            AuthoringRecordId = "manual.ui.button.normal",
            ResourceKey = Key("ui.button.normal", "Texture2D"),
            SourceLocator = new ResourceMappingSourceLocator
            {
                Kind = ResourceMappingSourceLocatorKind.UnityAsset,
                UnityGuid = "guid-ui-button",
                CachedPath = "Assets/UI/button.png"
            },
            DeliveryIntent = ResourceMappingDeliveryIntent.Internal,
            OverrideIntent = ResourceMappingOverrideIntent.Forbid,
            BundleIntent = new ResourceMappingBundleIntent { RuleId = "common.assets" }
        });
        profile.Entries.Add(CreateExternalEntry("character.clip.source", "character.clip.idle"));
        return profile;
    }

    private static ResourceMappingEntry CreateExternalEntry(string authoringRecordId, string resourceId)
    {
        return new ResourceMappingEntry
        {
            ProducerReceipt = Receipt("domain.character", authoringRecordId),
            AuthoringRecordId = authoringRecordId,
            ResourceKey = Key(resourceId, "AnimationClip"),
            DeliveryIntent = ResourceMappingDeliveryIntent.External,
            OverrideIntent = ResourceMappingOverrideIntent.Forbid,
            ExternalCatalogBinding = new ResourceMappingExternalCatalogBinding { CatalogMountId = "external.character" }
        };
    }

    private static ResourceMappingEntry CreateInternalUnityEntry(string producerId, string resourceId, string guid)
    {
        return new ResourceMappingEntry
        {
            ProducerReceipt = Receipt(producerId, resourceId),
            AuthoringRecordId = producerId + ":" + resourceId,
            ResourceKey = Key(resourceId, "AudioClip"),
            SourceLocator = new ResourceMappingSourceLocator { Kind = ResourceMappingSourceLocatorKind.UnityAsset, UnityGuid = guid, CachedPath = "Assets/fixture.wav", DomainSourceId = producerId + "/" + resourceId },
            DeliveryIntent = ResourceMappingDeliveryIntent.Internal,
            OverrideIntent = ResourceMappingOverrideIntent.Forbid,
            BundleIntent = new ResourceMappingBundleIntent { RuleId = "audio.samples" }
        };
    }

    private static ResourceMappingProducerReceipt Receipt(string producerId, string fingerprint)
    {
        return new ResourceMappingProducerReceipt
        {
            ProducerId = producerId,
            ProducerContractVersion = "1",
            SourceRecordFingerprint = "sha256:" + fingerprint
        };
    }

    private static ResourceMappingResourceKey Key(string id, string typeId)
    {
        return new ResourceMappingResourceKey { Id = id, TypeId = typeId, Variant = string.Empty, PackageId = "mxframework.samples" };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static bool HasCode(ResourceMappingValidationReport report, string code)
    {
        return report != null && report.Diagnostics.Exists(item => item.Code == code);
    }

    private static bool HasCode(List<ResourceMappingDiagnostic> diagnostics, string code)
    {
        return diagnostics != null && diagnostics.Exists(item => item.Code == code);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
