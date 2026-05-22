# Issue 80 Animation Editor Source Clip Unity Asset Link Gap

Date: 2026-05-23

Labels:
- `type/bug`
- `module/editor`
- `module/resource`
- `module/runtime`
- `status/spec-draft`
- `priority/high`

## Goal

Ensure selecting a source animation clip in Animation Editor produces a complete
source link, including both `runtimeResourceKey` and `unityAssetPath`, so the
clip can participate in Unity-side preview and runtime validation reliably.

## Background

Observed current behavior:

- some locomotion clips such as `walk.r`, `walk.l`, `run.r`, `run.l`, `run.b`
  keep a valid `runtimeResourceKey`
- but their `sourceSelection.unityAssetPath` remains empty
- Animation Editor Inspector therefore shows `Unity Asset: 未链接`
- those clips are the same ones that fail to play in Unity-side locomotion
  calibration / preview validation

Observed contrast:

- clips like `walk.b` already contain a concrete Unity path such as
  `Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_walk_back.anim`
- those clips appear correctly linked in the Inspector

This means the current source selection flow can preserve runtime binding while
losing Unity asset linkage, leaving the author with a partially bound clip that
looks valid at first glance but is not fully consumable by Unity-side tooling.

## Scope

- Audit the Animation Editor source clip selection flow and determine why a
  selected RuntimeReady animation can still persist with an empty
  `unityAssetPath`.
- Define the intended contract for a successfully linked source clip.
- Ensure linked source clips persist enough information for both:
  - runtime resource loading
  - Unity-side preview / validation
- Improve author-facing feedback when a chosen source clip only has runtime
  binding but lacks Unity asset linkage.

Possible implementation directions:

- require `unityAssetPath` when a source clip is expected to support Unity
  preview / validation
- surface a stronger warning or block confirmation when selection only yields a
  partial binding
- merge runtime and Unity binding data more reliably when saving the selection
- expose an explicit “linked / partially linked / unlinked” state instead of
  only showing `未链接`

## Out Of Scope

- Reworking the entire animation import pipeline
- Replacing runtime resource keys with Unity-only asset references
- General locomotion blend tuning unrelated to source asset linkage

## Related Modules

- `Tools/MxFramework.AnimationEditor/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Assets/MxFrameworkGenerated/CharacterPackages/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Interfaces/Animation.md`
- `Docs/USAGE.md`
- `Docs/Tasks/ISSUE_79_ANIMATION_AUTHORING_UNITY_CONSUMPTION_SYNC_GAP.md`

## Allowed Read/Write

- `Tools/MxFramework.AnimationEditor/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- After selecting a valid source animation intended for Unity preview/runtime
  validation, the clip persists both `runtimeResourceKey` and `unityAssetPath`.
- Animation Editor Inspector no longer reports `Unity Asset: 未链接` for clips
  that were selected from a valid Unity-backed source.
- If only runtime binding is available, the UI clearly indicates that the clip
  is partially linked and may not preview/play in Unity-side validation flows.
- Locomotion calibration no longer fails for those clips due solely to missing
  Unity asset linkage metadata.

## Validation

- Rebind locomotion clips such as `run.r` and `walk.r` in Animation Editor
- Save authoring data and inspect `animation_authoring.json`
- Confirm `sourceSelection.unityAssetPath` is populated for the rebound clips
- Recompile/import if required
- Verify Inspector shows a concrete Unity asset path instead of `未链接`
- Verify Unity-side locomotion calibration can play the rebound clips

## Public API

- No public runtime API changes intended by default

## Agent Constraints

- Treat this as a binding completeness issue, not a generic locomotion tuning
  problem
- Preserve `runtimeResourceKey` as the runtime authority while ensuring Unity
  validation metadata is linked consistently
