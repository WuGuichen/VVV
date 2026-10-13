using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunAnimationProfileSlots()
    {
        AnimationAuthoringCompiler_ProfileSlotContractRejectsLegacyFallbacks();
        Compiler_HumanoidAnimationConfigurationRetargetsAcrossCharacters();
        Compiler_EffectiveAnimationProfileRequiresExactLocomotionSlot();
    }

    internal static void RunCommonResources()
    {
        EditorServer_CommonResourcesApi_SyncsPublicModelsIntoGlobalProfile();
    }

    internal static void RunCharacterReadinessDedupe()
    {
        EditorServer_CharacterReadinessApi_DeduplicatesUpstreamValidationIssues();
    }

    internal static void RunCharacterSaveDraft()
    {
        EditorServer_CharacterSave_PreservesIgnoredCharacterActionLibrary();
        EditorServer_CharacterSave_RejectsStaleRevisionAndMigrationUsesSameGate();
    }

    internal static void RunCharacterLifecycleActions()
    {
        CharacterAuthoringManifest_OptionalAssetsValidateWhenDeclared();
        EditorServer_NumericCharacterIds_AllocateScaffoldAndRetire();
        EditorServer_CharacterCreate_CreatesArtSourceAndRejectsDuplicatePackageId();
        EditorServer_CharacterCreate_RewritePreservesPublicConfigurationReferences();
        EditorServer_CharacterCreate_RollsBackPackageAndArtSourceOnFailure();
        EditorServer_CharacterDuplicate_RewritesIdentityAndCopiesArtSource();
        EditorServer_CharacterArchive_MovesActiveSourcesAndRetainsGeneratedOutputs();
    }

    internal static void RunCharacterUnityImportReceipt()
    {
        EditorServer_CharacterReadinessApi_UsesPersistentUnityImportReceiptEvidence();
    }

    internal static void RunCharacterUnityImportBridge()
    {
        UnityImportBridge_PropagatesPaletteBindingAndHashesIt();
    }

    internal static void RunCharacterAbstractBones()
    {
        Schemas_ExposeC0ContractFields();
        AbstractBoneAuthoring_RoundTripAndValidationAreFailClosed();
        Compiler_SkeletonBindingsFlowToGeometryBindingAndHashes();
        EditorServer_CharacterReadinessApi_UsesPersistentUnityImportReceiptEvidence();
    }

    internal static void RunCharacterReactionProjection()
    {
        EditorServer_ReactionProfileSave_RebuildsConsistentCharacterActionHandoff();
        EditorServer_ReactionProfileSave_RejectsSchema8GeneratedProjectionWrites();
    }

    internal static void RunCharacterGenericRig()
    {
        AnimationAuthoringPackage_GenericRigRoundTripAndCompilerProjection();
        GlobalAnimationCompatibility_GenericRequiresExactNativeIdentity();
        CharacterSourceManifest_TextureSemanticRoundTrip();
        CharacterResourcePipeline_GenericAnimationFbxIsSupported();
        CharacterAuthoringCompiler_GenericSkeletalBodyProjectsAsCustomBodyWithExplicitParts();
        CharacterWeaponValidation_EmptyOptionalReactionConfigurationIsValid();
        CharacterReadiness_EmptyOptionalReactionConfigurationIsReady();
        AnimationAuthoringCompiler_FbxTakeIsAConcreteClipSource();
        CharacterAnimationFbx_CompilesToStandaloneAnimRuntimeAsset();
    }

    internal static void RunCharacterGameplayCameraContract()
    {
        RunCharacterGameplayCamera();
    }

    internal static void RunCharacterHurtboxContract()
    {
        Schemas_ExposeC0ContractFields();
        SkeletonBindingValidation_ReportsBrokenReferences();
        Compiler_SkeletonBindingsFlowToGeometryBindingAndHashes();
        EditorServer_CharacterReadinessApi_UsesPersistentUnityImportReceiptEvidence();
    }

    internal static void RunCharacterImportMaterials()
    {
        EditorServer_ImportMaterialSourceCandidates_AreScopedAndClassified();
        EditorServer_ImportMaterialsApi_ReportsManifestPresence();
        EditorServer_ImportMaterialsApi_ReportsFbxTextureDiagnostics();
        EditorServer_ImportMaterialsApi_SavesStatusAndSyncsChangedSources();
        EditorServer_ImportMaterialsSync_InfersExistingSlotResourceKeys();
        EditorServer_ImportMaterialsSync_GeneratesAndAuditsMaterialGraph();
    }

    internal static void RunAudioWorkbenchFmodScript()
    {
        AudioAuthoringManifestV2_MigratesAndCompilesSharedEvents();
        AudioAuthoringManifestV2_InvalidatesChangedBindingEvidence();
        AudioAuthoringManifestV2_ValidatesCueDefaultParameterReferences();
        FmodAudioLibraryProvider_MergesSemanticOverlay();
    }

    internal static void RunAudioWorkbenchServer()
    {
        EditorServer_AudioWorkbench_BlocksUnknownCueDefaultParametersWithoutWrites();
        PhysicalAudioWorkbench_UsesTripleHashAndAtomicSave();
        PhysicalAudioWorkbench_PromotesAppliedSetWithHash();
        PhysicalAudioWorkbench_WritesUnityRuntimePreviewRequest();
        EditorServer_AudioWorkbench_EnforcesHashGateAndAtomicPublish();
        EditorServer_AudioRuntimeActivation_AggregatesAuthoritativeReadiness();
        EditorServer_AudioSemanticReplacement_KeepsOriginalBinding();
    }

    public static void RunAll()
    {
        CharacterAuthoringManifest_OptionalAssetsValidateWhenDeclared();
        EditorServer_NumericCharacterIds_AllocateScaffoldAndRetire();
        ValidationIssue_JsonRoundTrip_PreservesGateAndSourceFields();
        ResourceCatalogEntry_JsonRoundTrip_PreservesResourceFields();
        CharacterImportMaterials_JsonRoundTrip_PreservesChecklistFields();
        CharacterSourceManifest_JsonRoundTrip_PreservesLayeredSourceRefs();
        ResourceLibraryItem_JsonRoundTrip_PreservesRuntimeContract();
        AuthoringResourceItem_JsonRoundTrip_PreservesProviderBindings();
        CharacterPackageProvider_ProjectsPackageResourceWithoutRuntimeKey();
        CharacterPackageProvider_ReportsDuplicateStableIdsAndProviderKeys();
        UnityAssetDatabaseProvider_ProjectsSnapshotAndUnavailableState();
        UnityProjectAssetProvider_DiscoversAnimationAssets();
        RuntimeCatalogProvider_ProjectsRuntimeReadyEntries();
        ResourceMappingProvider_NormalizesRuntimeTypeAndUsageTaxonomy();
        AuthoringResourceCollectionMerger_EnrichesRuntimeAnimationWithUnityAssetLink();
        FmodAudioLibraryProvider_ProjectsEventsAndUnavailableState();
        FmodAudioLibraryProvider_MergesSemanticOverlay();
        AudioSemanticManifestValidator_ReportsDuplicatesAndValidatesReplacement();
        AudioAuthoringManifestV2_MigratesAndCompilesSharedEvents();
        AudioAuthoringManifestV2_InvalidatesChangedBindingEvidence();
        AudioAuthoringManifestV2_ValidatesCueDefaultParameterReferences();
        EditorServer_AudioWorkbench_BlocksUnknownCueDefaultParametersWithoutWrites();
        AudioGenerationBriefBuilder_DerivesFoleyAndLayeredActionContracts();
        AudioGenerationRecipeV2_PersistsAndBoundsHistory();
        PhysicalAudioAuthoring_CompilesSeedAndMatchesRuntimeSelectionSemantics();
        PhysicalAudioAuthoring_RejectsInvalidActiveGraphAndRuntimeDto();
        PhysicalAudioCoverage_ResolvesFullFactsAndDetectsTrueAmbiguity();
        PhysicalAudioAuthoring_MatchesRuntimeResponseFixture();
        PhysicalAudioWorkbench_UsesTripleHashAndAtomicSave();
        PhysicalAudioWorkbench_PromotesAppliedSetWithHash();
        PhysicalAudioWorkbench_WritesUnityRuntimePreviewRequest();
        PhysicalAudioManagedEvent_PlansOnlyControlledBindings();
        EditorServer_AudioWorkbench_EnforcesHashGateAndAtomicPublish();
        EditorServer_AudioRuntimeActivation_AggregatesAuthoritativeReadiness();
        EditorServer_AudioSemanticReplacement_KeepsOriginalBinding();
        ExternalImportStagingProvider_FiltersFolderEntriesAndDetectsDuplicates();
        AuthoringResourceSelectionContracts_JsonRoundTrip();
        CharacterAnimationAuthoringSummary_JsonRoundTrip_PreservesSlots();
        CharacterAbilityLoadouts_JsonRoundTrip_PreservesSourceAndSlots();
        WeaponConfiguration3_JsonRoundTrip_SeparatesGlobalSinglesAndCharacterCombinations();
        RunCharacterActions();
        WeaponConfiguration3_SchemasExposeSinglesCombinationsAndExplicitMounts();
        WeaponConfiguration3_Validator_RejectsInvalidReferencesAndTwoHandConflicts();
        WeaponConfiguration3_Validator_AcceptsMinimumReactionBindings();
        WeaponConfiguration3_Compiler_EmitsExactStableFourHandCombinations();
        AnimationAuthoringPackage_JsonRoundTrip_PreservesAnimationEditorContracts();
        RunCharacterGenericRig();
        RunCharacterGameplayCamera();
        AnimationAuthoringCompiler_EmitsRuntimePlanArtifacts();
        AnimationAuthoringCompiler_ProfileSlotContractRejectsLegacyFallbacks();
        AnimationAuthoringResourceFieldSpecs_FilterAndResolveContracts();
        EditorServer_CharacterCreate_CreatesArtSourceAndRejectsDuplicatePackageId();
        EditorServer_CharacterCreate_RewritePreservesPublicConfigurationReferences();
        EditorServer_CharacterCreate_RollsBackPackageAndArtSourceOnFailure();
        EditorServer_CharacterDuplicate_RewritesIdentityAndCopiesArtSource();
        EditorServer_CharacterArchive_MovesActiveSourcesAndRetainsGeneratedOutputs();
        EditorServer_GlobalWeaponLibrary_UsesRevisionAndReferenceProtection();
        CharacterPackageMigration_NormalizesSupportedLegacySchemasAndBlocksFutureSchemas();
        CharacterPackageMigration_PromotesLegacyWeaponsAndRejectsGlobalConflicts();
        EditorServer_CharacterSave_PreservesIgnoredCharacterActionLibrary();
        EditorServer_CharacterSave_RejectsStaleRevisionAndMigrationUsesSameGate();
        EditorServer_CharacterMigration_UsesDualRevisionAndRollsBackBothFiles();
        EditorServer_AnimationPackageApi_IsFileBackedAndValidatesShallowDraft();
        EditorServer_AnimationPreview_IsCompilerBackedAndResolvesResources();
        EditorServer_CharacterReadinessApi_ReportsDeliveryStagesAndDeferredAbility();
        EditorServer_CharacterReadinessApi_DeduplicatesUpstreamValidationIssues();
        EditorServer_CharacterReadinessApi_UsesPersistentUnityImportReceiptEvidence();
        EditorServer_AbilityActionBindings_ArePackageScoped();
        EditorServer_CharacterAbilityChainApi_UsesCompiledDefaultScenario();
        EditorServer_CombinedWeaponConfigurationsApi_UsesSharedConfigurationIdentity();
        EditorServer_CharacterReadinessApi_ReportsLegacyReactionProfileHandoff();
        EditorServer_ReactionProfileSave_RebuildsConsistentCharacterActionHandoff();
        EditorServer_ReactionProfileSave_RejectsSchema8GeneratedProjectionWrites();
        RunCharacterImportMaterials();
        EditorServer_CommonResourcesApi_SyncsPublicModelsIntoGlobalProfile();
        EditorServer_CharacterActionOpenApi_WritesRequestAndStatus();
        EditorServer_CharacterUnityImportEditorStatus_PreservesWeaponTraceBakeFields();
        AuthoringResourceSelectionService_FiltersAndResolvesFieldSpecs();
        AuthoringResourceSelectionService_SourceClipAndFmodMultiBinding_DoNotDriftResolution();
        AuthoringResourceReferenceGraph_ScansCrossConsumerReferencesAndDiagnostics();
        ResourceFieldSpec_ResolveSelection_FillsCompiledResourceReference();
        ResourceReferenceGraph_ValidatesBrokenReferencesAndOrphans();
        ResourceKeyGenerator_GeneratesStablePackageLocalKey();
        CoordinateConvention_JsonRoundTrip_PreservesUnityTargetConvention();
        Schemas_ExposeC0ContractFields();
        BodyPartAuthoring_JsonRoundTrip_PreservesSkeletonBindingFields();
        AbstractBoneAuthoring_RoundTripAndValidationAreFailClosed();
        SkeletonBindingValidation_ReportsBrokenReferences();
        WeaponTraceValidation_ReportsTraceResponsibilityDiagnostics();
        BodyPartContactPoints_ValidateSourceDrafts();
        RunCharacterBodyPresence();
        RunLccM0Integration();
        CodexFlowTestSample_ValidatesAsReady();
        CodexFlowTestSample_ResourceFilesAndHashesValidate();
        CodexFlowTestTemplate_ResolvesDefaultHumanoidTemplate();
        CodexFlowTestSample_UsesLineSkeletonBoneColliders();
        CodexFlowTestSample_UsesStarterBoneColliderAliases();
        CodexFlowTestSample_UsesPublicDefaultWeaponRefs();
        CharacterActionGeneratedSamples_HaveConsistentReactionProfileHandoff();
        ResourceDependencyGraph_MissingDependencyBlocksImport();
        ResourceDependencyGraph_DuplicateDependencyProducesDiagnostic();
        MissingResourceFile_BlocksImport();
        ResourceHashMismatch_BlocksImport();
        InvalidImportTargetPath_BlocksImport();
        UnsupportedConvexShape_ProducesExportBlockedIssue();
        SlimeSample_UsesSameDtoForPrimitiveBody();
        CodexFlowTestSample_CompilesToConfigPatchGeometryMappingAndWritePlan();
        Compiler_HumanoidAnimationConfigurationRetargetsAcrossCharacters();
        Compiler_EffectiveAnimationProfileRequiresExactLocomotionSlot();
        Compiler_GeneratedLoadoutSlotMissing_BlocksExport();
        Compiler_AllowedStateIdsReferenceGeneratedStatesOnly();
        CodexFlowTestSample_CompilesResourcePlanArtifacts();
        CharacterResourcePlanCompiler_FmodAudioCueDoesNotEnterRuntimeCatalog();
        CharacterResourcePlanCompiler_WeaponRefDoesNotFallbackToLegacyPreviewKey();
        CharacterResourcePlanCompiler_PublicWeaponRefUsesGlobalProfileResource();
        CharacterResourcePlanCompiler_ResolvesAuthoringSelectionManifest();
        CharacterResourcesPlanCommand_WritesRuntimePlanArtifacts();
        Compiler_ApplicationResourceKeysAreCharacterReferences();
        Compiler_ExplicitAbilityLoadouts_OverrideGeneratedDefaults();
        Compiler_OptionalAbilityConfiguration_OmitsLoadout();
        Compiler_ModelWrapperPoseChangesImportAndResourceMappingHash();
        Compiler_SkeletonBindingsFlowToGeometryBindingAndHashes();
        Compiler_BodyPartContactPoints_EmitStableMillimeterConfig();
        Compiler_UnsupportedConvexShape_BlocksExport();
        Compiler_MissingSocket_BlocksSpawnOnly();
        Compiler_MissingResource_BlocksImport();
        Compiler_CoordinateMismatch_ReportsWarningOnly();
        Compiler_HashMismatch_BlocksImport();
        Compiler_ResourceKeyConflict_BlocksImport();
        UnityImportBridge_ImportsCodexFlowTestAndSkipsRepeat();
        UnityImportBridge_SpawnBlockedWritesButMarksNotSpawnable();
        UnityImportBridge_ImportBlockedDoesNotWriteProjectTarget();
        RunCharacterExportLayout();
        RunCharacterAnimationRuntimePlan();
        RunCharacterPreviewGlb();
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

}
