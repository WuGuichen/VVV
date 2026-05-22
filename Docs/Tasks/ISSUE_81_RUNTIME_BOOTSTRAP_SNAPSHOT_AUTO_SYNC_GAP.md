# Issue 81 Runtime Bootstrap Snapshot Auto-Sync Gap

Date: 2026-05-23

Labels:
- `type/implementation`
- `module/runtime`
- `module/editor`
- `module/resource`
- `status/spec-draft`
- `priority/high`

## Goal

Ensure Unity preview / calibration runtime entry points stay synchronized with
the latest imported animation artifacts without requiring manual scene rebuilds
or hand-edited `CharacterRuntimeResourceBootstrap._resources` snapshots.

## Background

Observed current behavior:

- animation authoring and import can advance `animation_set_definition.json` and
  `animation_clip_registry.json`
- Unity preview entry points such as:
  - `Assets/Scenes/MxFramework/CharacterImportedPreview.unity`
  - `Assets/Scenes/MxFramework/CharacterLocomotionCalibration.unity`
  still depend on serialized `CharacterRuntimeResourceBootstrap._resources`
- when those serialized runtime resources are not regenerated, runtime preview
  and calibration silently drift behind the imported artifacts

Recent incident:

- directional locomotion clips `standing_run_right`, `standing_run_left`,
  `standing_run_back` existed in imported animation artifacts
- the calibration scene bootstrap snapshot still lacked those runtime resource
  entries
- Animation Editor and Resource Library appeared correct, but Unity runtime
  preview could not play those clips
- current fix required:
  - patching scene snapshots
  - adding stale snapshot warnings in
    `CharacterRuntimeResourceBootstrap`
  - adding editor-side regression coverage for
    `CharacterImportedPackagePrefabBuilder.AddAnimationRuntimeResources(...)`

This means the current pipeline can detect drift more clearly than before, but
it still does not prevent drift by default.

## Scope

- Define the authoritative synchronization contract between imported animation
  artifacts and generated Unity runtime preview/bootstrap assets.
- Remove or greatly reduce the need for manual preview/calibration scene
  regeneration after animation import changes.
- Ensure the generated runtime bootstrap resource list stays aligned with
  `animation_clip_registry.json`.
- Improve editor-side regeneration and validation so stale bootstrap snapshots
  are not left behind as a normal workflow hazard.

Suggested implementation directions:

- make the import/generation pipeline automatically refresh preview prefab and
  calibration scene bootstrap resource snapshots when imported animation
  artifacts change
- or add a single explicit editor-side sync path that import flow always calls
- record enough provenance/hash information on generated bootstrap assets to
  verify freshness against:
  - `animation_set_definition.json`
  - `animation_clip_registry.json`
  - related imported package hashes already serialized on the bootstrap
- add consistency validation between generated bootstrap `_resources` and clip
  registry runtime keys

## Out Of Scope

- Replacing Unity preview runtime with a direct loader that instantiates
  arbitrary `AnimationClip` assets from `animation_resource_plan.json`
- Reworking the broader animation authoring data model
- General locomotion blend tuning or animation content authoring
- Changing gameplay/runtime clip selection semantics

## Related Modules

- `Assets/Scripts/MxFramework/Character.RuntimeSpawn.Unity/`
- `Assets/Scripts/MxFramework/Editor/CharacterImport/`
- `Assets/Scripts/MxFramework/Tests/CharacterImport/`
- `Assets/Scenes/MxFramework/`

## Must Read

- `AGENTS.md`
- `Docs/WORKFLOW.md`
- `Docs/USAGE.md`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn.Unity/CharacterRuntimeResourceBootstrap.cs`
- `Assets/Scripts/MxFramework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.cs`
- `Docs/Tasks/ISSUE_79_ANIMATION_AUTHORING_UNITY_CONSUMPTION_SYNC_GAP.md`
- `Docs/Tasks/ISSUE_80_ANIMATION_EDITOR_SOURCE_CLIP_UNITY_ASSET_LINK_GAP.md`

## Allowed Read/Write

- `Assets/Scripts/MxFramework/Character.RuntimeSpawn.Unity/`
- `Assets/Scripts/MxFramework/Editor/CharacterImport/`
- `Assets/Scripts/MxFramework/Tests/CharacterImport/`
- `Assets/Scenes/MxFramework/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- After imported animation artifacts add or remove runtime clip keys, preview
  prefab / calibration scene bootstrap resources are refreshed by the normal
  editor pipeline, without requiring manual scene surgery.
- `CharacterRuntimeResourceBootstrap` no longer relies on silently stale
  serialized `_resources` as a common steady-state workflow outcome.
- Generated preview/runtime bootstrap assets expose a reliable freshness signal
  against imported animation artifacts.
- Editor-side automated tests cover the bootstrap resource generation contract
  strongly enough to catch missing directional or newly-added clips before
  users discover them in Play Mode.
- If drift still occurs, the toolchain provides a single obvious recovery path
  instead of requiring authors to infer which scene/prefab must be rebuilt.

## Validation

- Update a character package animation registry to add at least one new runtime
  locomotion clip key.
- Run the normal import / preview generation flow.
- Verify the generated preview prefab and locomotion calibration entry point
  include the new runtime clip resource automatically.
- Enter Play Mode and confirm runtime warmup / preview no longer misses the
  new clip because of a stale bootstrap snapshot.
- Verify stale snapshot detection still reports clearly if a generated asset is
  intentionally desynchronized for regression testing.

## Public API

- No public gameplay/runtime API expansion is required by default.
- Editor/import pipeline behavior and generated Unity assets are expected to
  change.

## Agent Constraints

- Do not solve this by hand-editing generated scene snapshots as the primary
  workflow.
- Do not redefine `animation_resource_plan.json` as the direct runtime asset
  loading authority unless a separate design decision explicitly approves it.
- Preserve current imported animation artifact formats unless a stronger design
  change is separately reviewed.
