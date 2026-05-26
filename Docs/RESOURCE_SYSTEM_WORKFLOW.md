# Resource System Workflow

> Status: current working contract for the resource system.
> Goal: make one working path clear before adding more providers, editors, or build features.

## Current Main Path

The project has one supported local Player resource path:

```text
Authoring Resource Manager
  -> edits Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json

Global AssetBundle Builder Workbench
  -> reads global_resource_build_profile.json
  -> builds generated Player artifacts under StreamingAssets

Runtime Offline mode
  -> reads generated catalog / preload groups / bundle dependencies
  -> registers AssetBundleProvider
  -> loads resources through IResourceManager
```

This is the path to make reliable first. Other routes are secondary until this path is stable.

## Responsibilities

| Layer | Owns | Does not own |
| --- | --- | --- |
| Authoring Resource Manager | Global provider view, resource inspection, diagnostics, selection, Build Profile draft editing and saving | AssetBundle build execution, runtime loading, hot-update publishing |
| `global_resource_build_profile.json` | Internal Player resource membership, stable `ResourceKey`, bundle intent, preload groups, delivery mode | Character package ownership, external/mod/remote runtime authority |
| Global AssetBundle Builder Workbench | Profile validation, local AssetBundle build, generated runtime catalog, generated preload group file, bundle dependency manifest, build report | Profile authoring UX, resource discovery, gameplay loading |
| Runtime `ResourceManager` | Catalog resolve, provider routing, handle lifetime, preload execution, diagnostics | Authoring selection, import, profile editing, Unity build |
| Character/Animation/UI tools | Consume resource selections and save domain data as stable resource references | Own global resource library, invent bundle layout, scan resources independently |

## File Ownership

| File / folder | Written by | Consumed by |
| --- | --- | --- |
| `Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json` | Resource Manager via Authoring Server save API | Workbench / `GlobalPlayerResourceBuildProfileBuilder` |
| `Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json` | Workbench / build menu | Runtime Offline bootstrap |
| `Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json` | Workbench / build menu | Runtime Offline bootstrap and preload service |
| `Assets/StreamingAssets/MxFramework/Resources/global_bundle_dependencies.json` | Workbench / build menu | `AssetBundleProvider` dependency loader |
| `Assets/StreamingAssets/MxFramework/Resources/Bundles/<BuildTarget>/...` | Workbench / Unity `BuildPipeline.BuildAssetBundles` | `AssetBundleProvider` |
| `Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json` | Workbench / build menu | humans, diagnostics, Resource Manager provider status |

## Working Authoring Flow

1. Start Resource Manager from Editor Hub or `Tools/MxFramework.ResourceLibrary/start-resource-library.*`.
2. Inspect providers and diagnostics before editing.
3. Choose runtime-ready or intended internal resources.
4. Click `加入构建 Profile` or batch add selected resources.
5. Check generated `ResourceKey.id`, `deliveryMode`, `bundleRule`, `bundleGroupHint`, `preloadGroups`, and `labels`.
6. Click `保存 Profile`.
7. Open Unity menu `MxFramework/Resources/Open Global AssetBundle Builder`.
8. Click `Refresh / Validate Profile`.
9. Click `Build Global Player Resource Catalog`.
10. In Play Mode, use `GlobalResourceRuntimeMode.Offline` and the generated preload group id.

## Runtime Flow

Runtime code should not touch Resource Manager authoring APIs or profile JSON directly.

```text
GlobalResourceRuntimeServices.Create(Offline)
  -> GlobalResourceRuntimeBootstrap.CreateFromStreamingAssets
  -> load global_runtime_catalog.json
  -> load global_preload_groups.json
  -> load global_bundle_dependencies.json
  -> register AssetBundleProvider
  -> ResourcePreloadService preloads group
  -> gameplay/UI/animation loads by ResourceKey
```

Editor mode is currently a permissive authoring/dev mode. It is not proof that the generated Player resource path works. Offline mode is the local Player simulation path.

## Current Blockers

These are the urgent fixes before expanding the system:

1. Resource Manager Profile entry generation must be deterministic and valid.
   - `ResourceKey.id` must use only lowercase letters, digits, `.`, `_`, `-`.
   - It must be a runtime key, not `resourceId`, provider id, file path, or display label.
   - External/editor-only/excluded entries must not receive default `bundleRule`.

2. Resource Manager needs clearer save diagnostics.
   - Validation errors should point to the affected draft row and field.
   - Warnings should be shown separately from errors.
   - Save failure should not make authors guess which resource caused the issue.

3. Profile and Bundle Plan need one obvious "can build" state.
   - A profile entry should visibly say whether it will become internal bundle content, external runtime content, editor-only content, or excluded content.
   - Bundle rules that select zero internal entries should be obvious before save/build.

4. CharacterTest should use Offline mode for real resource path testing.
   - `Editor` mode can remain a loose dev mode.
   - `Offline` mode is the mode that proves generated catalog + AssetBundle + preload groups work.

5. Generated build artifacts need a cleanup story.
   - Workbench should make stale bundles visible and safe to remove.
   - Source control policy for generated local artifacts must be explicit per project.

## Do Not Add Yet

Do not add these until the main path is reliable:

- Online hot-update catalog flow.
- CDN publish/sign/encrypt/delta patch workflow.
- YooAsset or Addressables adapter as default route.
- More domain-specific resource services like `CharacterTestResourceServices`.
- Another separate resource editor that also writes Profile data.

## Definition Of Done For "Resource System Works"

The minimum acceptable result is:

1. Add one real Unity asset in Resource Manager.
2. Save Profile without validation errors.
3. Build in Global AssetBundle Builder Workbench without errors.
4. Generated catalog contains the expected `ResourceKey`.
5. Generated bundle file exists for the active build target.
6. Runtime Offline mode initializes with catalog count greater than zero.
7. Preload group loads at least one resource.
8. Direct `IResourceManager.Load<T>(ResourceKey)` succeeds.
9. Releasing preload group / handles returns debug snapshot loaded count to the expected value.

