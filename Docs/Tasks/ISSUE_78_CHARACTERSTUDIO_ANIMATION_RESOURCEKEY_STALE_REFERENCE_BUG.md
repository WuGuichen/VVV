# Issue 78 CharacterStudio Animation ResourceKey Stale Reference Bug

Date: 2026-05-22

Labels:
- `type/bug`
- `module/editor`
- `status/spec-draft`
- `priority/high`

## Goal

Ensure CharacterStudio removes stale animation resource references when an
animation slot is rebound to a different resource.

## Background

Observed behavior:

1. an author selects an animation resource such as
   `art.character.skeleton.animation.standing_jump`
2. later the same slot is changed to a different final resource such as
   `char.test.anim.locomotion`
3. the slot binding updates correctly, but the old resource key remains in
   `applicationConfig.resourceKeys`

This leaves stale animation references in the character package even though the
current slot no longer points to them.

The result is misleading package state and makes it harder to tell which
resources are actually part of the current character configuration.

## Scope

- When an animation slot changes from one resource to another, remove the old
  slot-owned resource key from `applicationConfig.resourceKeys` if no other slot
  or binding still needs it.
- Keep the current slot resource in sync with `resourceSelection`.
- Prevent repeated animation-picking experiments from accumulating dead
  references in the package config.

Possible implementation options:

- track previous slot resource key before applying a new selection
- remove the old key only if it is no longer referenced by any animation slot,
  group, or other explicit package binding
- add a save-time cleanup pass for orphaned animation resource keys

## Out Of Scope

- Rewriting the whole animation authoring model
- Cleaning every historical package automatically unless explicitly part of the
  task
- Removing intentionally shared animation references that are still in use

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.Authoring/samples/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Tasks/ANIMATION_EDITOR_00_DESIGN.md`
- `Tools/MxFramework.CharacterStudio/README.md`

## Allowed Read/Write

- `Tools/MxFramework.CharacterStudio/web/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- Rebinding an animation slot removes obsolete animation resource keys from
  `applicationConfig.resourceKeys` when they are no longer referenced.
- The final package config reflects the current animation slot bindings rather
  than historical picker attempts.
- Shared animation resources still remain if another slot or binding uses them.
- Existing slot save/load behavior remains compatible.

## Validation

- Bind a slot to animation A, then rebind it to animation B
- Confirm the slot points to B
- Confirm A is removed from `resourceKeys` if unused elsewhere
- Reload the package and verify the stale reference does not return

## Public API

- No public runtime API changes intended

## Agent Constraints

- Treat `applicationConfig.resourceKeys` as derived binding state, not an append
  only history log
