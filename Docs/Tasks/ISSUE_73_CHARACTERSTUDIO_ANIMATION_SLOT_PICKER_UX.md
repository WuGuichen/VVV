# Issue 73 CharacterStudio Animation Slot Picker UX

Date: 2026-05-22

Labels:
- `type/refactor`
- `module/editor`
- `status/spec-draft`
- `priority/medium`

## Goal

Make the CharacterStudio animation slot picker usable for first-time authors by
reducing noise, clarifying what can be selected for the current slot, and
promoting animation-group level choices over raw clip clutter.

## Background

The current animation selection dialog shown from CharacterStudio is difficult
to use:

- it mixes many raw animation clips into one dense grid
- information hierarchy is weak, with long technical metadata dominating the
  visible area
- slot intent is unclear, especially for `locomotion` and `combat`
- duplicate or near-duplicate entries from different providers are hard to
  distinguish
- the dialog does not clearly guide the user toward the preferred selection
  level for the current workflow

This is especially confusing because CharacterStudio is supposed to bind
existing animation references, while `Animation Editor` owns `Group / Clip /
Blend / Timeline` authoring.

## Scope

- Improve the animation slot picker layout and information hierarchy.
- Make current slot context explicit:
  - slot id
  - slot purpose
  - expected resource kind / usage
- Prefer grouped animation resources or authored animation references over a
  long flat list of individual clips when possible.
- Add filtering/sorting so authors can quickly narrow to relevant candidates.
- Reduce duplicate-provider noise or merge equivalent entries more clearly.
- Improve empty/invalid state messaging when no suitable animation resource
  exists yet.

Possible implementation options:

- default to grouped sections such as `Recommended`, `Matching Usage`,
  `Other Compatible`
- add search, usage filter, provider filter, and duplicate collapse
- show compact cards with a progressive-detail expand area instead of dumping
  all metadata inline
- highlight the recommended binding path: select existing animation group /
  profile created by Animation Editor

## Out Of Scope

- Rebuilding the Animation Editor itself
- Redesigning the entire Resource Manager
- Changing runtime animation binding schema
- Solving every animation authoring workflow gap in this task

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.EditorHub/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Tasks/ANIMATION_EDITOR_00_DESIGN.md`
- `Docs/Tasks/ANIMATION_EDITOR_01_NATIVE_CLIP_UNITY_CONSUMPTION.md`
- `Tools/MxFramework.CharacterStudio/README.md`

## Allowed Read/Write

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.EditorHub/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- A first-time author can identify what type of animation resource the current
  slot expects.
- The picker no longer presents an overwhelming flat wall of clip cards as the
  primary view.
- Duplicate or equivalent items from multiple providers are easier to
  understand or collapsed.
- The preferred workflow of binding authored animation references is visible in
  the dialog.
- Selecting a locomotion or combat animation resource remains compatible with
  existing save behavior.

## Validation

- Manual walkthrough opening at least `locomotion` and `combat` slots from
  CharacterStudio
- Verify search/filter/recommended grouping narrows results correctly
- Confirm selected animation resource still saves and reloads correctly

## Public API

- No public runtime API changes intended

## Agent Constraints

- Preserve compatibility with existing sample packages
- Do not reintroduce `sourceClipName` hand-entry as the primary workflow
- Keep CharacterStudio focused on reference binding, not full animation
  authoring
