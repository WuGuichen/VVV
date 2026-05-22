# Issue 79 Animation Authoring Unity Consumption Sync Gap

Date: 2026-05-22

Labels:
- `type/bug`
- `module/editor`
- `module/resource`
- `module/runtime`
- `status/spec-draft`
- `priority/high`

## Goal

Ensure Animation Editor authoring changes are reflected consistently across the
intended Unity consumption entry points, so authors can trust that edited
animation configuration has actually propagated.

## Background

Observed author expectation:

- after editing locomotion/blend content in Animation Editor, Unity-side
  consumers should show the updated result consistently

Observed current behavior:

- Animation Editor changes do propagate into compiled/imported JSON such as
  `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/animation_set_definition.json`
- but Unity does not provide one clear, consistent author-facing view that makes
  those changes obviously visible across its preview/runtime entry points
- as a result, authors cannot tell whether:
  - the change failed to save
  - the change failed to compile
  - the change failed to import
  - or the Unity view they are looking at simply does not consume that data

This creates a synchronization trust gap between Animation Editor and Unity-side
validation.

## Scope

- Audit the intended Unity entry points that consume imported animation
  authoring data, including at least:
  - imported preview prefab / preview scene flows
  - locomotion calibration scene / runner flows
  - runtime animation backend flows
- Define which Unity views are expected to reflect Animation Editor changes and
  which are not.
- Make author-facing validation consistent enough that edited blend/group/clip
  configuration can be verified without guesswork.
- Improve diagnostics or presentation when a given Unity view does not consume a
  certain class of authoring data.

Possible implementation options:

- add an explicit imported-animation status/summary view in Unity
- show source `animation_set_definition.json` hash/version in preview/calibration
  entry points
- align preview/runtime bootstrap paths to consume the same imported animation
  definition where intended
- clearly label non-authoritative Unity views that do not reflect imported blend
  configuration

## Out Of Scope

- Replacing the entire runtime animation backend
- Rewriting Animation Editor itself
- Forcing every Unity inspector or Animator asset view to become a live mirror
  of authoring data

## Related Modules

- `Tools/MxFramework.AnimationEditor/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn.Unity/`
- `Assets/Scripts/MxFramework/Editor/CharacterImport/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Interfaces/Animation.md`
- `Docs/Interfaces/CharacterApplication.md`
- `Docs/Tasks/ANIMATION_EDITOR_01_NATIVE_CLIP_UNITY_CONSUMPTION.md`
- `Docs/USAGE.md`

## Allowed Read/Write

- `Tools/MxFramework.AnimationEditor/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn.Unity/`
- `Assets/Scripts/MxFramework/Editor/CharacterImport/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- Authors can identify a Unity-side authoritative validation path for Animation
  Editor changes.
- The expected Unity entry points either reflect updated imported animation
  authoring data or clearly state that they do not consume it.
- Blend/group/clip authoring changes no longer appear to "silently fail" due to
  mismatched consumption paths.
- Documentation and/or diagnostics explain the save -> compile -> import ->
  Unity-consume chain clearly enough for first-time users.

## Validation

- Edit locomotion blend content in Animation Editor
- Confirm the change reaches imported `animation_set_definition.json`
- Verify which Unity entry points reflect the updated data
- Verify non-consuming entry points are clearly labeled or excluded from the
  expected validation path

## Public API

- No public runtime API changes intended by default

## Agent Constraints

- Treat this as a synchronization/consumption clarity issue, not just a single
  BlendTree asset mismatch
- Preserve the current noEngine animation contract as the authoritative data
  source unless a broader redesign is explicitly approved
