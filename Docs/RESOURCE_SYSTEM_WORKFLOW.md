# Resource System Workflow

> Status: Current
> Scope: Current working contract for the resource system.
> Goal: make one working path clear before adding more providers, editors, or build features.
> Design background: `Docs/RESOURCE_MAPPING_CONVERGENCE_PLAN.md` records the convergence rationale. The v2 mapping described below is now the production path.

## Current Main Path

The project has one supported local Player resource path:

```text
Authoring Resource Manager
  -> edits its complete producer snapshot in Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json

Global AssetBundle Builder Workbench
  -> compiles the v2 Profile with GlobalResourceBuildPlanCompiler
  -> stages one fingerprinted artifact generation
  -> publishes catalog / mounts / preload / dependencies / bundles, then commits the artifact-set marker last

Runtime Offline mode
  -> validates global_resource_artifact_set.json and every listed SHA-256 before reading any payload
  -> rejects partial or mixed generations
  -> registers AssetBundleProvider
  -> registers SceneAssetBundleProvider when scene catalog entries exist
  -> mounts only concrete catalogs declared by global_catalog_mounts.json and accepted by its allowlist
  -> loads resources through IResourceManager
  -> loads content scenes through SceneFlowController / ResourceSceneFlowDriver
```

This is the path to make reliable first. Other routes are secondary until this path is stable.

## Responsibilities

| Layer | Owns | Does not own |
| --- | --- | --- |
| Authoring Resource Manager | Global provider view, StorySource canon/outline/packet authoring, ArtSource status visualization, resource inspection, diagnostics, selection, reference review, read-only relationship graph, and the complete `resource-manager.manual` snapshot | Domain producer rows, Bundle policy, Catalog mounts, Unity/FairyGUI import execution, AssetBundle build execution, runtime loading, hot-update publishing |
| `global_resource_mapping_profile.v2.json` | Producer receipts, stable `ResourceKey`, source locator, bundle/preload/delivery intent, override policy and concrete Catalog mounts | Runtime handle lifetime or provider implementation |
| Global AssetBundle Builder Workbench | Profile validation, local AssetBundle build, generated runtime catalog, generated preload group file, bundle dependency manifest, build report | Profile authoring UX, resource discovery, gameplay loading |
| Runtime `ResourceManager` | Catalog resolve, provider routing, handle lifetime, preload execution, diagnostics | Authoring selection, import, profile editing, Unity build |
| Runtime `SceneFlow` + Resources.Unity | Resolve `ResourceTypeIds.Scene` entries and load content scenes from scene-only AssetBundles | Treat scenes as normal `Load<T>` object resources or remove the Boot scene requirement |
| Character/Animation/UI tools | Consume resource selections and save domain data as stable resource references | Own global resource library, invent bundle layout, scan resources independently |

## File Ownership

| File / folder | Written by | Consumed by |
| --- | --- | --- |
| `StorySource/Projects/WGame/Canon/*.json`, `StorySource/Projects/WGame/Outlines/*`, `StorySource/Projects/WGame/story_packets.json` and `StorySource/Projects/WGame/Packets/*.json` | Story Source authoring / Resource Manager `storySource` API | Resource Manager `storySource` provider, ArtSource story gate derivation |
| `ArtSource/Projects/WGame/art_manifest.json` | ArtSource framework / art review tasks | Authoring Resource Manager `artSource` provider as a read-only status source |
| `Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json` | Profile Aggregator from producer snapshots; Resource Manager owns only `resource-manager.manual` | Resource Manager preview / `GlobalPlayerResourceBuildProfileBuilder` through the shared compiler |
| `Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json` | Workbench / build menu | Runtime Offline bootstrap |
| `Assets/StreamingAssets/MxFramework/Resources/global_catalog_mounts.json` | Workbench / build menu | Runtime Offline bootstrap mount validation |
| `Assets/StreamingAssets/MxFramework/Resources/ExternalCatalogs/*.json` | Concrete external pipeline / package owner | Runtime Offline bootstrap after mount and allowlist validation |
| `Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json` | Workbench / build menu | Runtime Offline bootstrap and preload service |
| `Assets/StreamingAssets/MxFramework/Resources/global_bundle_dependencies.json` | Workbench / build menu | `AssetBundleProvider` dependency loader |
| `Assets/StreamingAssets/MxFramework/Resources/global_resource_artifact_set.json` | Workbench / build menu; written last | Runtime Offline bootstrap and Resource Manager Build Status; commits one complete generation by identity and SHA-256 |
| `Assets/StreamingAssets/MxFramework/Resources/Bundles/<BuildTarget>/...` | Workbench / Unity `BuildPipeline.BuildAssetBundles` | `AssetBundleProvider`, `SceneAssetBundleProvider` |
| `Assets/StreamingAssets/MxFramework/Resources/ExternalBundles/<BuildTarget>/...` | Concrete external pipeline / package owner | Direct concrete external provider declared by the mounted catalog |
| `Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json` | Workbench / build menu | humans, diagnostics, Resource Manager provider status |
| `Library/MxFramework/ResourceBundles/residue-ledger.json` | Global Builder 的本机归属与跨代待清理台账 | 仅 Editor 清理流程；不是 Runtime 目录权威，也不进入版本控制 |
| `Library/MxFramework/ResourceBundles/residue-report.txt`、`cleanup-receipts.jsonl` | Global Builder / Workbench | 人工查看发现、可清理、处置和失败证据 |

The generated files under `Assets/StreamingAssets/MxFramework/Resources/` and `Assets/Config/MxFramework/ResourceBuildReports/` are machine-local build outputs. They include the active Unity build target, bundle hashes, file sizes and timestamps, so different devices or platforms can rewrite them even when source content has not changed. Do not commit these local outputs. Commit the authoring input `global_resource_mapping_profile.v2.json`; regenerate outputs through the Global AssetBundle Builder or publish canonical release artifacts from a fixed build environment.

## Working Authoring Flow

1. Start Resource Manager from Editor Hub or `Tools/MxFramework.ResourceLibrary/start-resource-library.*`.
2. Inspect providers and diagnostics before editing. The current top-level workspaces are `Story / Art / Browse / Profile / Refs / Graph / Build / Debug`.
3. In Story, maintain WGame world canon facts, story outlines, and Story Input Packets. It writes only `StorySource/Projects/WGame`, uses `storySourcePacketsHash` for conflict protection, validates required packet fields, requires approved canon/outline references, and can approve a complete packet as `approved-for-art`. It does not write ArtSource, Unity assets, Resource Profile data, StreamingAssets, or runtime Story SaveState.
4. In Art, review and advance WGame ArtSource workflow state when source art is involved. Art is preview-first and multi-process: `art_manifest.json` remains the asset fact source while `art_tasks.json` describes parallel workstreams and task rows. It can create workstreams/tasks and save controlled task-progression fields with `artSourceTasksHash` conflict protection. It shows real existing preview images or contact sheets when available, explicit missing-preview states when not, and this task's next action for the selected task. It does not edit the manifest, approve Story, generate images, import Unity/FairyGUI assets, save Profile data, or build resources.
5. In Profile, inspect the producer-owned Bundle policy. `bundleRules[]` is read-only in Resource Manager; its owning policy producer defines `id`, `bundleName`, compression, build target and dependency policy.
6. In Browse, choose runtime-ready or intended internal resources.
7. Assign only manual-owned resources to an existing Bundle. The browser draft uses `bundleRule` as an editing projection; save emits canonical `bundleIntent.ruleId`. Domain-owned rows are visible but cannot be edited or deleted here. Scene entries use `resourceKey.typeId = "Scene"` and a Unity source locator that resolves to a `.unity` asset.
8. In Refs, review who references the current resource, which same-config resources are loaded together, and whether required runtime resources are missing Profile membership or have split Bundle assignment. Refs may write recommended Bundle fixes only to the browser-side Profile draft; it never saves automatically.
9. Open Graph when a visual explanation is useful. Graph is read-only: it displays `引用者配置 -> 资源 -> Bundle / delivery`, supports scoped views, and routes fixes back to Refs.
10. Edit `deliveryMode`, `overrideIntent`, `preloadGroups` and `labels` on manual-owned rows as needed. `preloadGroups` are runtime loading timing, not physical bundle layout.
11. Click `保存 Profile`.
12. In Build, preview the saved Bundle Plan and confirm it shows the same Bundles that Unity will build.
   Build also shows a read-only build status summary from the saved Profile, Bundle Plan, latest Unity build report and generated artifact files. It does not build AssetBundles or write `StreamingAssets`.
13. Open Unity menu `MxFramework/Resources/Open Global AssetBundle Builder`.
14. Click `Refresh / Validate Profile`.
15. Click `Build Global Player Resources`.
16. In Play Mode, use `GlobalResourceRuntimeMode.Offline` and the generated preload group id.

Bundle policy is not authored by the Web UI. If no existing rule owns an internal manual row, that row is not buildable; create or change the rule through its producer, then resynchronize the Profile.

Scene bundle rules must be scene-only. If a rule selects both `Scene` and non-`Scene` entries, the Unity builder reports an error and does not build that plan. This keeps Unity scene AssetBundles separate from normal asset AssetBundles.

## Runtime Flow

Runtime code should not touch Resource Manager authoring APIs or profile JSON directly.

```text
GlobalResourceRuntimeServices.Create(Offline)
  -> GlobalResourceRuntimeBootstrap.CreateFromStreamingAssets
  -> load global_resource_artifact_set.json
  -> validate target, Profile/Catalog identity and every listed file hash
  -> load catalog / mounts / preload / dependencies only from committed relative paths
  -> validate mount path or composition-root input, required canonical Catalog hash, catalog id, exact key/override allowlist and Provider availability
  -> load only the declared concrete ExternalCatalogs/*.json
  -> load global_preload_groups.json
  -> load global_bundle_dependencies.json
  -> register AssetBundleProvider
  -> register SceneAssetBundleProvider if generated catalog contains sceneAssetBundle entries
  -> register externalAssetBundle when discovered catalogs require it
  -> accept caller-supplied composition-root Catalogs only when a compiled CatalogMount declares each one exactly once
  -> ResourcePreloadService preloads group
  -> gameplay/UI/animation loads by ResourceKey
  -> SceneFlowController loads content scenes by stable scene key
```

Editor mode is currently a permissive authoring/dev mode. It is not proof that the generated Player resource path works. Offline mode is the local Player simulation path.

External rows never become proxy entries. Each row names a concrete `CatalogMount`; the generated mount manifest records bootstrap kind, relative catalog path when applicable, expected catalog id, required canonical SHA-256, exact complete-key allowlist and per-key override intent. Startup rejects unresolved, duplicated, mismatched, unmounted or Provider-incomplete inputs. `GlobalResourceRuntimeOptions.CompositionRootCatalogs` may satisfy only declared composition-root mounts; `ExternalProviders` supplies their explicit providers. Arbitrary catalog paths and the old unvalidated catalog list no longer exist. Demo/test application catalogs that are outside the global mapping are registered explicitly by that application composition root after global bootstrap, not smuggled through mapping options.

Content scenes are not preloaded as ordinary objects. Use `GlobalResourceRuntimeServices.SceneFlowController` in Offline mode and request the stable scene key, for example `scene.character_test`; the driver looks up `new ResourceKey(sceneKey, ResourceTypeIds.Scene)`, loads the scene AssetBundle, then calls Unity `SceneManager.LoadSceneAsync` with the catalog scene path. The loaded content scene can have `buildIndex == -1`; only the Boot scene remains in Unity Build Settings.

## Current Follow-ups

These are the current follow-ups before expanding the system:

0. Resource Manager information architecture is now organized around the local Player workflow.
   - See `Docs/Tasks/RESOURCE_MANAGER_WORKFLOW_UI_REORG_01.md`.
   - The active workflow is `Story -> Art -> Browse -> Profile -> Refs -> Graph -> Build -> Debug`, not one large same-priority screen.
   - Story owns StorySource packet editing/approval; Art owns ArtSource preview/status visualization and controlled `art_tasks.json` creation/progression; ArtSource manifest owns art facts, and Unity Workbench owns import/build.
   - Refs owns actionable Bundle recommendation review; Graph is read-only visual explanation.
   - The web UI remains an authoring and diagnostics surface; Unity Workbench remains the AssetBundle build surface.

1. Resource Manager Profile entry generation must remain deterministic and valid.
   - `ResourceKey.id` must use only lowercase letters, digits, `.`, `_`, `-`.
   - It must be a runtime key, not `resourceId`, provider id, file path, or display label.
   - External/editor-only/excluded entries must not receive default `bundleRule`.

2. Resource Manager needs clearer save diagnostics.
   - Validation errors should point to the affected draft row and field.
   - Warnings should be shown separately from errors.
   - Save failure should not make authors guess which resource caused the issue.

3. Profile and Bundle Plan keep one obvious "can build" state.
   - A profile entry should visibly say whether it will become internal bundle content, external runtime content, editor-only content, or excluded content.
   - Bundle rules that select zero internal entries should be obvious before save/build.
   - Bundle Plan must only list predefined Bundle rules plus explicit internal override bundles; it must not invent bundles from `bundleGroupHint`, preload groups or domain labels.
   - A missing `entries[].bundleIntent.ruleId` or a reference to an unknown Bundle id is a save/build error for internal runtime-loadable entries.

4. CharacterTest should use Offline mode for real resource path testing.
   - `Editor` mode can remain a loose dev mode.
   - `Offline` mode is the mode that proves generated catalog + AssetBundle + preload groups work.

5. Generated Bundle residue is reported by the Workbench and `residue-report.txt`. The Builder records owned bundle hashes across committed generations; only a committed retirement with a valid ledger, matching file hash, safe path and no current reference is eligible for Editor deletion with its `.meta`. Failed cleanups remain pending across later generations and restarts. A missing or corrupt ledger disables automatic cleanup. Existing marker data can be inventoried but cannot reconstruct earlier failed cleanups, so that migration generation is untrusted; a later newly committed generation can retire files proven by the inventoried marker. Unknown files, other target/profile content and `ExternalBundles` remain in place for manual ownership review.
   - Local generated artifacts and ledger files are ignored by Git; release or CI artifacts should be published through the project artifact channel instead of normal source commits.

## Do Not Add Yet

Do not add these until the main path is reliable:

- Online hot-update catalog flow.
- CDN publish/sign/encrypt/delta patch workflow.
- YooAsset or Addressables adapter as default route.
- More domain-specific resource services like `CharacterTestResourceServices`.
- Another separate resource editor that also writes Profile data.
- Story approval inside the Art workspace, manifest editing, image generation or Unity import buttons inside the Art workspace.

## Definition Of Done For "Resource System Works"

The minimum acceptable result is:

1. Add one real Unity asset to the Resource Manager manual snapshot.
2. Confirm one producer-owned Bundle policy exists.
3. Assign the asset to that Bundle.
4. Refs shows required references, same-config resources and any Bundle recommendation in an explainable form.
5. Graph can show the current resource relationship as `引用者配置 -> 资源 -> Bundle / delivery` without offering direct Profile writes.
6. Save Profile without validation errors.
7. Build page Bundle Plan shows that Bundle and the assigned resource.
8. Build in Global AssetBundle Builder Workbench without errors.
9. Generated catalog contains the expected `ResourceKey`.
10. Generated bundle file exists for the active build target.
11. Runtime Offline mode initializes with catalog count greater than zero.
12. Preload group loads at least one resource.
13. Direct `IResourceManager.Load<T>(ResourceKey)` succeeds.
14. Releasing preload group / handles returns debug snapshot loaded count to the expected value.
15. A content scene can be loaded by `SceneFlowController` from the generated scene catalog entry without adding that content scene to Unity Build Settings.
