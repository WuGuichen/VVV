# Global Resource Build Profile 00: Catalog, Preload Group and AssetBundle Build Design

> Status: Draft
> Scope: global resource authoring, runtime catalog generation, preload group organization, AssetBundle build profile, and CharacterTest formal resource flow.
> Related docs: `Docs/RESOURCE_MANAGEMENT_SYSTEM.md`, `Docs/Tasks/CHARACTER_RESOURCE_LIBRARY_00_DESIGN.md`, `Docs/Tasks/CHARACTER_RESOURCE_LIBRARY_EDITOR_01_MVP.md`, `Docs/CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`

## Problem

MxFramework already has the runtime resource layer:

- `ResourceKey`
- `ResourceCatalog`
- `ResourceCatalogEntry`
- `ResourceManager`
- `ResourcePreloadService`
- `ResourcesProvider`
- `AssetBundleProvider`
- `StreamingResourceCatalogLoader`
- `ResourceCatalogEditorValidator`

It also has an authoring-side Resource Manager direction in `Tools/MxFramework.ResourceLibrary` and `MxFramework.Authoring.Core.AuthoringResources`.

The missing piece is the global build authority:

```text
Global resource library / authoring view
  -> build profile
  -> validated runtime catalog
  -> AssetBundle layout
  -> preload groups
  -> build report
  -> Runtime ResourceManager
```

Without this layer, demos and domain editors are forced to either hand-write catalog JSON or generate local catalogs from their own folder rules. That is not acceptable for formal game flow, because Character, UI, VFX, Audio, Story and Gameplay must all resolve resources through one global resource world.

## Current Status

This project does not yet have a complete Global Resource Editor / Resource Build Profile tool.

Existing reusable parts:

| Area | Existing asset | Reuse decision |
| --- | --- | --- |
| Runtime resource contract | `MxFramework.Resources` | Reuse as final runtime contract. Do not create another loader or catalog type. |
| Unity runtime providers | `MxFramework.Resources.Unity` | Reuse `AssetBundleProvider`, `ResourcesProvider`, `RemoteBundleProvider`, `StreamingResourceCatalogLoader`. |
| Catalog validation | `ResourceCatalogValidator`, `ResourceCatalogEditorValidator` | Reuse for generated catalog validation. Extend only if build profile requires extra rules. |
| Sample catalog generation | `SampleResourceCatalogBuilder`, `SamplePlayerResourceCatalogBuilder` | Reuse as implementation reference, not as architecture source of truth. |
| Authoring resource model | `AuthoringResourceItem`, provider adapters, `RuntimeCatalogAuthoringResourceProvider` | Reuse as global resource discovery and picker model. |
| Resource Manager web tool | `Tools/MxFramework.ResourceLibrary` | Continue as the global authoring resource manager shell. Rename later only if a dedicated task does it. |
| Character resource plan | `CharacterResourcePlan`, `CharacterResourceOrchestrator` | Use as a consumer/domain plan. It must not own global resource packaging. |

## Correct Ownership

The global resource build profile owns runtime resource packaging.

Domain data only references resources:

```text
Global Resource Build Profile
  owns ResourceKey, provider binding, labels, bundle rules, preload groups, output catalogs

Character / UI / VFX / Combat / Story configs
  store ResourceKey or ResourceSelectionRef
  may emit resource plans
  do not decide provider address or AssetBundle layout

Runtime
  loads ResourceCatalog
  resolves ResourceKey through ResourceManager
  preloads by explicit keys or labels
```

Character packages are not global resource packages. They can contribute imported assets and reference graphs, but the global build profile decides whether and how those assets become runtime catalog entries and AssetBundle contents.

## Non-Goals

This design does not introduce:

- A replacement for Unity `AssetBundle`.
- A mandatory Addressables dependency.
- A second runtime resource manager.
- A character-owned resource package authority.
- A hand-edited catalog workflow.
- Remote patching, CDN publishing, encryption, signing or delta update.
- A full visual editor in the first implementation slice.

Addressables can be added later as a provider/build backend, but the first formal path should use existing `AssetBundleProvider` and `BuildPipeline.BuildAssetBundles`.

## Required Concepts

### Global Resource Build Profile

The build profile is the authoring-time authority for runtime resource output.

Recommended persisted path:

```text
Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json
```

The first version should be JSON, not ScriptableObject, because:

- it is diffable;
- external tools can read/write it;
- it does not require Unity serialization;
- it can be validated by Authoring Core and Unity Editor.

Minimal schema:

```json
{
  "schemaVersion": 1,
  "profileId": "global.default",
  "catalogId": "global.runtime",
  "packageId": "",
  "entries": [
    {
      "resourceKey": {
        "id": "ui.startup.button.normal",
        "type": "Texture2D",
        "variant": "",
        "packageId": ""
      },
      "source": {
        "providerId": "unityAssetDatabase",
        "unityAssetPath": "Assets/UI/Game/Startup/button_normal.png",
        "unityGuid": ""
      },
      "labels": [
        "domain.ui",
        "preload.boot.base",
        "bundle.ui.startup"
      ],
      "bundleRule": "ui.startup",
      "preloadGroups": [
        "boot.base"
      ],
      "dependencies": [],
      "providerData": {}
    }
  ],
  "bundleRules": [
    {
      "id": "ui.startup",
      "bundleName": "global.ui.startup",
      "matchLabels": ["bundle.ui.startup"],
      "compression": "lz4",
      "includeDependencies": true
    }
  ],
  "preloadGroups": [
    {
      "id": "boot.base",
      "labels": ["preload.boot.base"],
      "failFast": true,
      "maxConcurrentLoads": 4
    }
  ]
}
```

### Resource Entry Source

The profile entry source identifies where the authoring asset currently comes from. It is not the runtime provider.

Examples:

- Unity AssetDatabase asset path / GUID.
- Imported character asset.
- Generated prefab.
- External staging asset that still needs import.
- Existing runtime catalog entry.

The runtime provider is selected by the build backend. For Player builds, the first backend should emit `assetBundle` entries.

### Bundle Rule

Bundle rules describe packaging intent. They should not duplicate resource keys one by one when labels can express the group.

Recommended rule inputs:

- explicit resource keys;
- label match;
- domain match;
- package match;
- generated domain plan input, such as CharacterResourcePlan.

Recommended output:

- bundle name;
- included asset paths;
- dependency bundle policy;
- compression mode;
- build target;
- content hash;
- warnings for duplicated or orphaned assets.

### Preload Group

Preload groups are runtime load plans, not bundles.

They should compile to:

```csharp
new ResourcePreloadPlan(
    groupId,
    explicitKeys,
    labels,
    failFast,
    maxConcurrentLoads);
```

Bundle grouping and preload grouping may overlap, but they are not the same concept:

```text
bundle.ui.startup
  physical packaging

preload.boot.base
  runtime timing and failure policy
```

## Build Pipeline

The global build pipeline should be deterministic:

```text
Load GlobalResourceBuildProfile
  -> resolve source assets through AuthoringResource providers
  -> validate ResourceKey uniqueness
  -> validate source asset existence and type
  -> expand bundle rules
  -> build AssetBundles
  -> compute hashes and sizes
  -> generate runtime ResourceCatalog JSON
  -> generate preload group report
  -> generate build report
  -> validate generated catalog
```

Recommended output paths:

```text
Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json
Assets/StreamingAssets/MxFramework/Resources/Bundles/<bundleName>
Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json
```

Generated `ResourceCatalog` entries for Player should use:

```json
{
  "provider": "assetBundle",
  "address": "<bundleName>|<unityAssetPath>",
  "labels": ["domain.ui", "preload.boot.base", "bundle.ui.startup"],
  "hash": "sha256:<bundle file hash>",
  "size": 12345,
  "providerData": {
    "bundleName": "<bundleName>",
    "assetPath": "<unityAssetPath>",
    "buildProfileId": "global.default"
  }
}
```

## Editor and Tooling Surface

### Unity Editor Menu

First implementation should add a minimal Unity menu, not a full visual editor:

```text
MxFramework/Resources/Validate Global Resource Build Profile
MxFramework/Resources/Build Global Player Resource Catalog
```

These commands should:

- load the JSON profile;
- run validation;
- build bundles;
- write catalog and report;
- log a concise result to Unity Console.

### Resource Manager Web Tool

`Tools/MxFramework.ResourceLibrary` should become the visual surface later.

For the first slice, it can stay read-only and expose:

- resources from the global profile;
- generated runtime catalog status;
- bundle rule membership;
- preload group membership;
- diagnostics.

Write operations should remain disabled until the authoring API has preview/validate/commit endpoints.

## Character Flow Integration

Character editor and imported character data should not own the resource catalog.

Correct integration:

```text
Character editor
  -> selects ResourceSelectionRef from global resource manager
  -> compiler resolves selections to ResourceKey
  -> emits CharacterResourcePlan

Global resource build profile
  -> ensures those ResourceKeys exist in global runtime catalog
  -> may use CharacterResourcePlan as analysis input for labels and preload reports
  -> decides bundle layout

Runtime CharacterTest
  -> loads global runtime catalog
  -> loads character config / CharacterResourcePlan
  -> CharacterResourceOrchestrator.PreloadForSpawn(plan)
  -> ResourceManager resolves keys globally
```

The CharacterResourcePlan may influence build validation:

- warn if a spawn-critical key is missing from the global profile;
- warn if a required key only exists as editor-only;
- suggest a preload group label;
- suggest bundle co-location;
- never directly decide provider address.

## CharacterTest Integration

CharacterTest should use the formal global flow:

```text
GameManager
  -> create GameSlice

GameSlice
  -> initialize ResourceManager
  -> register providers
  -> load global runtime catalog
  -> start Story

Story flow
  -> OpenLoadingUi
  -> LoadBaseResources
  -> LoadSelectedCharacterConfig
  -> PreloadCharacterResources
  -> SpawnCharacter
  -> OpenStartupUi
```

`LoadBaseResources` should preload global labels such as:

```text
preload.boot.base
preload.ui.loading
```

`PreloadCharacterResources` should use the selected character's `CharacterResourcePlan` and `CharacterResourceOrchestrator`, not hand-written CharacterTest resource keys.

## Validation Rules

Profile validation must fail on:

- duplicate `ResourceKey` in the same runtime scope;
- empty `id`, `type` or invalid key characters;
- missing Unity asset path / GUID for Unity source entries;
- source type mismatch against `ResourceTypeIds`;
- missing bundle rule for runtime-loadable entries;
- missing provider for generated catalog;
- bundle rule selecting no assets, unless explicitly allowed;
- preload group with no labels or keys, unless explicitly allowed;
- runtime-required domain plan key missing from the global profile.

Profile validation should warn on:

- resource exists in profile but is not referenced by any domain plan or preload group;
- domain plan references a resource that has no preload label;
- very large bundle;
- one preload group spanning too many bundles;
- AssetBundle dependency duplication;
- editor-only or preview-only asset selected for runtime.

## Development Plan

### Step 1: Profile Schema and Validator

Add noEngine DTOs and validation under Authoring Core or a new resource authoring namespace:

```text
Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/
  GlobalResourceBuildProfile.cs
  GlobalResourceBuildProfileValidator.cs
```

Validation should not depend on Unity.

### Step 2: Unity Resolver and Build Preview

Add Unity Editor code that resolves profile entries to `AssetDatabase` assets and produces a preview build plan:

```text
Assets/Scripts/MxFramework/Editor/Resources/
  GlobalResourceBuildProfileEditorLoader.cs
  GlobalResourceBuildPlan.cs
  GlobalResourceBuildProfileEditorValidator.cs
```

This step should only preview and validate. No bundle files are written yet.

### Step 3: AssetBundle Builder

Add a builder modeled after `SamplePlayerResourceCatalogBuilder`, but driven by the global profile:

```text
Assets/Scripts/MxFramework/Editor/Resources/
  GlobalPlayerResourceCatalogBuilder.cs
```

This builder should:

- call `BuildPipeline.BuildAssetBundles`;
- copy bundles to `StreamingAssets`;
- generate `ResourceCatalog`;
- write build report;
- run `ResourceCatalogEditorValidator`.

### Step 4: Resource Manager Web Read Model

Expose build profile, generated catalog, bundle rules and preload groups through the existing authoring server API.

Do not add write endpoints until preview/validate/commit flow is designed.

### Step 5: CharacterTest Runtime Consumption

Update CharacterTest to:

- load the generated global runtime catalog;
- register `AssetBundleProvider`;
- preload boot labels;
- load selected character resource plan;
- use `CharacterResourceOrchestrator`.

This step should not add CharacterTest-owned catalog generation.

## API Reuse Plan

| Requirement | Reused API / module | Notes |
| --- | --- | --- |
| Runtime resource identity | `ResourceKey`, `ResourceTypeIds` | No new key type for runtime. |
| Runtime catalog | `ResourceCatalog`, `ResourceCatalogEntry` | Generated output must map to existing JSON format. |
| Runtime loading | `ResourceManager`, `IResourceProvider` | No direct `Resources.Load` in domain code. |
| Player packaging | Unity `BuildPipeline.BuildAssetBundles`, `AssetBundleProvider` | Avoid Addressables dependency for first slice. |
| Catalog loading | `StreamingResourceCatalogLoader` | Runtime reads generated catalog from StreamingAssets. |
| Preload | `ResourcePreloadService`, `ResourcePreloadPlan` | Build profile defines labels/groups; runtime service executes. |
| Validation | `ResourceCatalogValidator`, `ResourceCatalogEditorValidator` | Extend only for profile-specific checks. |
| Authoring discovery | `AuthoringResourceItem`, provider adapters | Resource Manager UI and compiler consume the same model. |
| Character needs | `CharacterResourcePlan`, `CharacterResourceOrchestrator` | Consumer input and validation signal, not catalog authority. |
| Story flow | Story runtime and bridge | Story triggers loading phases, not asset lists. |

## Acceptance Criteria

First implementation is accepted when:

- a global build profile JSON can be validated;
- duplicate keys and missing Unity assets are reported before build;
- Player AssetBundles are generated from profile rules;
- a generated runtime catalog is written to `StreamingAssets`;
- generated catalog validates through `ResourceCatalogEditorValidator`;
- runtime can load at least one generated asset through `AssetBundleProvider`;
- preload by label works through `ResourcePreloadService`;
- CharacterTest can consume global catalog without owning resource definitions.
