# RM-C6 v1 / Proxy Deletion Manifest

> Status: Current
>
> Scope: exact removal and replacement gate; this file is not a second implementation ledger.

RM-C6 is complete only when every row below is deleted or replaced in the same local activation batch. No compatibility reader, redirect, proxy provider, or ignored generated proxy artifact may remain.

## Delete whole files

| Path | Retired authority |
| --- | --- |
| `Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json` and `.meta` | v1 production Profile |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/GlobalResourceBuildProfile.cs` | v1 DTO/schema |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/GlobalResourceBuildProfileValidator.cs` | v1 validator |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/GlobalResourceBundlePlanner.cs` | second semantic planner |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/GlobalResourceBuildProfileAuthoringResourceProvider.cs` | v1 Resource Library provider |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/ResourceMappingInventoryCommands.cs` | v1 inventory/worksheet and shadow-only commands |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/GlobalResourceBuildProfileTests.cs` | v1 tests |
| `Packages/com.vincent.wgameframework/Resources.Unity/RuntimeCatalogReferenceProvider.cs` and `.meta` | K1-to-K2 proxy provider |
| `Packages/com.vincent.wgameframework/Editor/ResourceMapping/Core/LegacyResourceMappingArtifactCleanup.cs` and `.meta` | one-shot cleanup implementation after its RM-C6 invocation and post-clean assertion |

## Replace in place

| Path / symbol | RM-C6 replacement |
| --- | --- |
| `Packages/com.vincent.wgameframework/Editor/Resources/GlobalPlayerResourceBuildProfileBuilder.cs` | retain the build entry type only after replacing all v1 parsing/planning with strict v2 `GlobalResourceBuildPlanCompiler` output; emit concrete internal bundles/catalogs and concrete mounted external catalogs only |
| `Packages/com.vincent.wgameframework/Editor/Resources/GlobalResourceMappingV2ShadowBuilder.cs` | rename/convert to the production Unity build-input adapter; remove every shadow flag, menu label and no-Player-write contract |
| `Packages/com.vincent.wgameframework/Editor/Resources/GlobalAssetBundleBuilderWorkbench.cs` | v2 Profile/plan diagnostics and artifacts; remove external proxy artifact rows |
| `Packages/com.vincent.wgameframework/Editor/Resources/GlobalEditorResourceRuntimeFactory.cs` | strict v2 plan and direct concrete Catalog registration |
| `Packages/com.vincent.wgameframework/Resources.Unity/GlobalResourceRuntimeBootstrap.cs` | delete `RegisterRuntimeCatalogReferenceProvider`, proxy registration, proxy entry rewriting and proxy provider-data parsing; mount only allowlisted concrete Catalogs |
| `Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterPackageImportCommand.cs` | stop synchronizing/writing v1 Profile; submit producer snapshot only |
| `Packages/com.vincent.wgameframework/Editor/CharacterAction/Timeline/Preview/CharacterActionPreviewRuntimeSession.cs` | consume v2 editor catalog projection |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestWorldObjectLevelEditorBootstrap.cs` | call the v2 build path |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.GlobalResources.cs` | v2 read/save/plan/build-status only; delete v1 Profile endpoints and planner helpers |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs` | delete `/global-build-profile`, `/global-build-profile/save`, v1 `/bundle-plan` routing and every shadow-only route label; retain only v2 production routes |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CommonResources.cs` | common-resource producer snapshot through the Aggregator; no direct v1 mutation |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.ResourceLibrary.cs` | use v2 producer inventories/compiled plan; remove v1 provider |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterPackageCommands.cs` | delete v1 Profile fallback/context and retain complete producer submission |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/ResourceMappingCharacterShadowCoordinator.cs` | productionize the Character producer coordinator; remove skip-on-missing and shadow naming/defaults |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/ResourceMappingFirstPartyShadowCoordinator.cs` | productionize and retain the first-party producer synchronization entry; remove shadow naming/defaults |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/Program.cs` | replace shadow command names/help with production resource-mapping synchronization commands |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterResourcePlanCompiler.cs` | remove the v1 Global Profile output path from Character write plans |
| `Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterActionRuntimeAnimationResourceDiscovery.cs` | discover animation resources from the v2 compiled plan |
| `Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterAnimationClipRegistrationCommand.cs` | replace the v1 Profile reader with the v2 compiled plan |
| `Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.cs` | replace the v1 Profile reader with the v2 compiled plan |
| `Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json` | regenerate as the v2 plan/build report with no legacy or proxy artifact paths |
| `Tools/MxFramework.CharacterStudio/scripts/smoke.mjs` and `Tools/MxFramework.CharacterStudio/README.md` | validate and document the production v2 Profile instead of the retired v1 file |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/AuthoringResourceContracts.cs` | delete v1 context fields/provider id |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/AuthoringResourceNaming.cs` | delete `GlobalResourceBuildProfileResourceKey` helpers; use canonical v2 key formatting |
| `Tools/MxFramework.ResourceLibrary/web/resource-library-core.js`, `resource-library-profile.js`, `resource-library-build-debug.js`, `index.html` | make v2 editable/production; delete v1 save/profile routes and all shadow labels |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Resources/GlobalPlayerResourceBuildProfileBuilderTests.cs` | v2 production builder, fingerprint, Bundle parity, schema rejection and proxy-absence tests |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Resources/GeneratedResourceBuildArtifactLoaderTests.cs` | concrete internal/packaged/external/scene load-release tests; delete proxy-provider fixtures |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterImport/CharacterImportedPackagePrefabBuilderTests.cs` | replace v1 Profile fixtures with canonical v2 Profile/plan fixtures |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/ResourceMappingProfileV2Tests.cs` | delete shadow coordinator/preview expectations and assert production-only v2 behavior |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/Program.cs` | remove the deleted v1 test-suite invocation |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.Compiler.cs` | remove v1 write-plan expectations |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.EditorServer.cs` | replace v1 endpoint/Profile fixtures with v2 production fixtures |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.SampleValidation.cs` | replace v1 sample Profile validation with v2 compilation |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.Serialization.cs` | delete v1 DTO serialization fixtures |
| `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.Support.cs` | replace v1 DTO/provider helpers with v2 helpers |
| `Docs/RESOURCE_MAPPING_CONVERGENCE_IMPLEMENTATION.md` and current resource docs/interfaces | replace shadow/v1 current-fact language with activated v2 facts; historical milestone evidence may retain quoted names |

## Delete generated proxy artifacts even when ignored by Git

RM-C6 must invoke `LegacyResourceMappingArtifactCleanup.DeleteExactLegacyArtifacts(projectRoot)` before validation and fail if `FindExisting(projectRoot)` returns any row afterward. The helper and its fixture are then deleted in the same activation batch so the final production-source zero-scan cannot self-match:

- `Assets/StreamingAssets/MxFramework/Resources/global_external_runtime_catalog.json` and `.meta`
- `Assets/StreamingAssets/MxFramework/Resources/ExternalCatalogs/global_external_assetbundle_catalog.json` and `.meta`
- `Assets/StreamingAssets/MxFramework/Resources/ExternalBundles/` recursively and `ExternalBundles.meta`

The cleanup must not delete `ExternalCatalogs/` or any independently owned concrete Catalog within it.

## Zero-scan activation gate

The activation batch fails if production source or active configuration still matches any of these retired names (documentation history and this manifest are excluded from the scan):

```text
GlobalResourceBuildProfile
GlobalResourceBundlePlanner
RuntimeCatalogReferenceProvider
GlobalResourceMappingV2ShadowBuilder
GlobalResourceMappingV2ShadowBuildInput
ShadowProfilePath
ValidateShadowProfile
ResourceMappingCharacterShadowCoordinator
ResourceMappingFirstPartyShadowCoordinator
ResourceMappingCharacterShadowSyncResult
ResourceMappingFirstPartyShadowSyncResult
ReadResourceMappingV2ShadowPreview
ResolveResourceMappingV2ShadowProfilePath
resourceMappingV2ShadowPayload
resourceMappingV2Shadow=
ShadowProfileRelativePath
shadow-v2-panel
loadResourceMappingV2ShadowPreview
renderResourceMappingV2ShadowPreview
ShadowPreviewUnavailable
Shadow v2
Shadow Only
SHADOW ONLY
No Player artifacts
no Player artifacts
global_resource_build_profile.json
global_external_runtime_catalog.json
global_external_assetbundle_catalog.json
sync-character-shadow
sync-first-party-shadow
resource-mapping inventory
productionActive = false
mode = "shadow-v2"
ProductionActive = false
Mode = "shadow-v2"
WritesPlayerArtifacts = false
```

It also fails if the compiled v2 plan and emitted build report have different build targets or fingerprints, if any runtime entry contains proxy/provider routing data, or if any external entry lacks one resolved allowlisted `CatalogMount`.
