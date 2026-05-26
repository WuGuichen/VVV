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

This design is only the internal build authority part of that global resource world. The Authoring Resource Manager remains the global aggregation and management surface. It must bridge editor tools and runtime resource consumption by showing both:

- internally built resources controlled by `GlobalResourceBuildProfile`;
- externally supplied runtime resources, such as existing `StreamingAssets` catalogs/files, standalone AssetBundles, package-local catalogs, Mod package catalogs and future remote bundle sources.

The build profile owns internally generated runtime outputs. It does not own every runtime resource visible to the Resource Manager.

## Current Status

This project now has the first Resource Manager / Resource Build Profile slice. It is still not a complete productized Global Resource Editor or hot-update build pipeline.

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

Completed first slice:

- Editor Hub can open the Resource Manager and resource plan/status views.
- Resource Manager can show provider/resource diagnostics from the Authoring API.
- Resource Manager can add or remove the selected item from `GlobalResourceBuildProfile`.
- Resource Manager can edit delivery mode, bundle override intent, bundle group/rule hints, preload groups and labels.
- Resource Manager can save the profile through the Authoring API validation gate.
- Resource Manager can preview the Bundle Plan through `/api/authoring/resources/bundle-plan`.
- Unity Editor exposes `MxFramework/Resources/Validate Global Resource Build Profile` and `MxFramework/Resources/Build Global Player Resource Catalog`.
- The Unity build menu can generate the Player runtime catalog, preload groups, bundle dependency manifest, local AssetBundles and build report for the active build target.

Still out of scope for this slice:

- a productized general AB Builder workstation;
- batch build queues, build farm integration or per-platform release orchestration;
- remote hot-update version manifests, CDN publishing, signing, encryption or delta updates;
- YooAsset adapter or YooAsset as the default MxFramework resource route.

## Resource Manager Relationship

`Tools/MxFramework.ResourceLibrary` is the global Authoring Resource Manager surface. Its responsibility is broader than this build profile:

```text
Unity AssetDatabase
Global Resource Build Profile
Generated runtime catalog
StreamingAssets catalogs / files
Standalone AssetBundles
Package-local catalogs
Mod package catalogs
Future remote bundle sources
  -> Authoring Resource Manager
       unified provider view
       resource identity bridge
       references and diagnostics
       editor picker source
       runtime availability status
```

The build profile should appear in Resource Manager as the provider / authority for internally built Player resources. External runtime resources should appear through their own providers. Resource Manager can compare and diagnose them together, but it should not force external resources to be imported into the internal build profile.

Current authoring ownership:

```text
Resource Manager Web UI
  -> edits the Global Resource Build Profile draft
  -> saves through /api/authoring/resources/global-build-profile/save
  -> writes Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json

Global AssetBundle Builder Workbench
  -> reads the saved profile
  -> generates catalog, preload groups, bundle dependency manifest, AssetBundles and build report

Runtime Offline mode
  -> reads generated StreamingAssets artifacts
  -> does not read Resource Manager's authoring aggregate collection
```

This means `global_resource_build_profile.json` is managed by Resource Manager today. The web UI is still an authoring metadata editor: it can validate, save and preview Bundle Plan output, but it does not perform the Unity AssetBundle build itself.

This distinction is important:

| Layer | Owns | Does not own |
| --- | --- | --- |
| Authoring Resource Manager | Unified resource view, provider status, editor picker data, references, diagnostics, runtime availability | Physical bundle layout or runtime loading implementation |
| Global Resource Build Profile | Internal runtime catalog membership, bundle rules, preload groups, generated AssetBundles, generated reports | External package/mod/remote resources that are not built by this profile |
| Runtime ResourceManager | Catalog resolution, provider routing, handle lifecycle, preload execution | Authoring selection, import, source asset organization |

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
      "buildTarget": "ActiveBuildTarget",
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

For Unity source entries, `unityGuid` is the stable identity and `unityAssetPath` is the human-readable and diagnostic path. The Unity resolver must prefer GUID resolution, then compare the resolved path with the stored path and report drift. A missing GUID is only allowed for temporary drafts that are not runtime-loadable.

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

Supported compression values for the first backend:

| Value | Unity mapping | Notes |
| --- | --- | --- |
| `lz4` | `BuildAssetBundleOptions.ChunkBasedCompression` | Recommended default for Player bundles. |
| `uncompressed` | `BuildAssetBundleOptions.UncompressedAssetBundle` | Useful for local debugging and smoke tests. |
| `lzma` | no `UncompressedAssetBundle` or `ChunkBasedCompression` option | Smaller initial bundle, slower load; must be explicit. |

Unknown compression values are validation errors.

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

Preload groups must also have a generated runtime artifact. The first implementation should write:

```text
Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json
```

This file is not a replacement for `ResourceCatalog`; it is a small plan index that lets a runtime composition root or Story step construct `ResourcePreloadPlan` without hard-coding labels in CharacterTest or other demos.

Minimal generated schema:

```json
{
  "schemaVersion": 1,
  "profileId": "global.default",
  "catalogId": "global.runtime",
  "groups": [
    {
      "id": "boot.base",
      "explicitKeys": [],
      "labels": ["preload.boot.base"],
      "failFast": true,
      "maxConcurrentLoads": 4
    }
  ]
}
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
  -> export AssetBundle dependency manifest
  -> compute hashes and sizes
  -> generate runtime ResourceCatalog JSON
  -> generate preload group JSON
  -> generate preload group report
  -> generate build report
  -> validate generated catalog
```

Recommended output paths:

```text
Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json
Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json
Assets/StreamingAssets/MxFramework/Resources/global_bundle_dependencies.json
Assets/StreamingAssets/MxFramework/Resources/Bundles/<buildTarget>/<bundleName>
Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json
```

Bundle outputs must include the Unity build target in the path, because AssetBundles are platform-specific. The build report and generated catalog must record the build target used for the generated files.

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
    "buildProfileId": "global.default",
    "buildTarget": "<buildTarget>"
  }
}
```

The runtime composition root must register `AssetBundleProvider` with a dependency provider backed by `global_bundle_dependencies.json`. The default empty dependency provider is only valid for bundles that have no cross-bundle dependencies.

Minimal generated dependency manifest schema:

```json
{
  "schemaVersion": 1,
  "profileId": "global.default",
  "buildTarget": "StandaloneOSX",
  "bundles": [
    {
      "bundleName": "global.ui.startup",
      "dependencies": []
    }
  ]
}
```

### Determinism and Cleanup

The builder should keep generated outputs deterministic:

- sort profile entries by `ResourceKey`;
- sort expanded bundle asset paths ordinally before build;
- sort labels, preload groups, dependency lists and report sections;
- fail when the same asset is selected by multiple bundle rules unless an explicit conflict policy is added;
- clean stale generated bundles for the same `profileId` and `buildTarget`, or report them as stale in the build report.

The builder must not delete unrelated user-authored files under `StreamingAssets`; cleanup is limited to files recorded as generated by the same profile/build target.

## Editor and Tooling Surface

### Unity Editor Workbench

The recommended productized entry is now:

```text
MxFramework/Resources/Open Global AssetBundle Builder
```

The workbench is an EditorWindow built with UI Toolkit. It wraps `GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(...)` and `Build(...)` instead of duplicating build, validation, catalog, preload group or bundle dependency generation. It shows:

- profile path and active build target;
- generated catalog, preload groups, bundle dependencies, build report and build-target bundle output folder status;
- build plan summary: profile id, catalog id, package id, entry count, bundle count, error count and warning count;
- diagnostics with severity, code, message, source path and resource key;
- commands to refresh/validate, build, copy the report, open the profile and locate generated artifacts.

`Refresh / Validate Profile` is validate-only: it calls `CreateBuildPlan(...)` and must not call `Build(...)` or write generated Player artifacts. `Build Global Player Resource Catalog` calls the existing builder and refreshes artifact state and success/failure status after completion.

### Unity Editor Menu

The low-level Unity menus remain available for quick calls and automation:

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

### Existing Resource Manager Web Tool

`Tools/MxFramework.ResourceLibrary` is already the current Authoring Resource Manager surface. It keeps the historical directory name to reduce migration noise, but its product meaning is the global resource manager, not a character-local resource library.

This build profile task integrates with the existing tool instead of creating another window. The Resource Manager exposes:

- resources from the global profile;
- generated runtime catalog status;
- bundle rule membership;
- preload group membership;
- generated preload group status;
- generated bundle dependency manifest status;
- build target and stale output status;
- diagnostics.

Existing import, reimport and replace-source write operations remain in the Resource Manager. Build profile writes now go through the Authoring API save gate: the UI edits a draft, preview/diagnostics come from the Bundle Planner, and `Save Profile` validates before writing `Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json`.

The Resource Manager save path is still authoring metadata only. It does not build AssetBundles, publish remote manifests or mutate generated `StreamingAssets` outputs. Bundle Plan remains preview-only in the web tool. Player artifacts are generated by the Unity Global AssetBundle Builder workbench or by the low-level Unity build menu after validation.

Save validation is intentionally strict because the profile is the source for runtime catalog generation. Common save failures:

- `ResourceKey id contains invalid characters.` Profile entries must use a stable runtime resource key such as `ui.start_screen.button.normal`. They must not use provider-prefixed `resourceId` values, filesystem paths or keys containing `:`, `|`, whitespace or slashes.
- `Bundle rule is ignored for external, editor-only or excluded entries.` External, editor-only and excluded entries are visible in Resource Manager for comparison and diagnostics, but they are not internal Player AssetBundle members. Such entries should clear `bundleRule` and bundle hints unless the author explicitly changes delivery back to an internal bundle mode.

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
- unknown bundle compression value;
- build target mismatch between profile, output path and generated catalog;
- bundle rule selecting no assets, unless explicitly allowed;
- preload group with no labels or keys, unless explicitly allowed;
- runtime-required domain plan key missing from the global profile;
- runtime-loadable Unity source entry without a resolvable GUID.

Profile validation should warn on:

- resource exists in profile but is not referenced by any domain plan or preload group;
- domain plan references a resource that has no preload label;
- very large bundle;
- one preload group spanning too many bundles;
- AssetBundle dependency duplication;
- stored Unity asset path drifting from the path resolved by GUID;
- stale generated bundle files for the same profile/build target;
- editor-only or preview-only asset selected for runtime.

Domain plan references should be classified before validation:

| Classification | Missing from global profile | Notes |
| --- | --- | --- |
| `required` | Error | Spawn-critical or boot-critical runtime asset. |
| `optional` | Warning | Runtime can continue with fallback or degraded presentation. |
| `editorOnly` / `previewOnly` | Warning or ignored | Must not be emitted into Player runtime catalog unless explicitly promoted. |

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
- export the bundle dependency manifest;
- generate `ResourceCatalog`;
- generate preload group JSON;
- write build report;
- run `ResourceCatalogEditorValidator`.

### Step 3.5: Productized Global AssetBundle Builder Workbench

Add a UI Toolkit EditorWindow at:

```text
Assets/Scripts/MxFramework/Editor/Resources/GlobalAssetBundleBuilderWorkbench.cs
```

The workbench should remain a thin operator surface over the Step 2/3 builder APIs. It may expose editor-only helper formatting from `GlobalPlayerResourceBuildProfileBuilder`, but it must not copy the AssetBundle build flow, catalog generation flow or validation flow. Artifact actions should only open, ping or reveal existing generated files and folders.

### Step 4: Resource Manager Web Read / Write Model

Expose build profile, generated catalog, bundle rules and preload groups through the existing authoring server API.

The first write endpoint is limited to saving the global build profile after validation. Bundle Plan remains preview-only; generated Player files are still written by the Unity Global AssetBundle Builder workbench or the low-level Unity build menu, not by the web tool.

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
- generated preload group and bundle dependency manifests are written to `StreamingAssets`;
- generated catalog validates through `ResourceCatalogEditorValidator`;
- runtime can load at least one generated asset through `AssetBundleProvider`;
- `AssetBundleProvider` is registered with generated dependency data when bundles have dependencies;
- preload by label works through `ResourcePreloadService`;
- CharacterTest can consume global catalog without owning resource definitions.
