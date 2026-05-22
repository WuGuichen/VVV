# Issue 77 CharacterStudio Path Field Readability and Locator Guidance

Date: 2026-05-22

Labels:
- `type/refactor`
- `module/editor`
- `status/spec-draft`
- `priority/high`

## Goal

Improve CharacterStudio path-field usability so authors can clearly see the full
selected/configured path value and understand what `locatorPath` means and when
to use it.

## Background

Current CharacterStudio reference/path fields have two related usability
problems:

1. Path values are visually truncated, making it hard to tell what was actually
   selected or configured.
2. `locatorPath` lacks clear author-facing explanation, so users cannot tell:
   - what a locator is
   - how it differs from a bone path
   - when to fill locator path versus bone path
   - whether it is optional, preferred, or imported metadata

This makes ordinary socket/body-part/component editing difficult because the UI
shows partial technical strings without enough semantic guidance.

## Scope

- Improve path field readability for values such as:
  - `bonePath`
  - `locatorPath`
  - local-space parent paths
  - other similar reference/path fields in CharacterStudio
- Ensure full path values can be inspected without guesswork.
- Add clear help text or inline guidance for `locatorPath`.
- Clarify the relationship between:
  - parent body part
  - bone path
  - locator path
  - local-space parent path

Possible implementation options:

- allow full-value expansion, tooltip, wrap, copy, or detail popover for long
  path fields
- add a compact display plus explicit full-path viewer
- show field hints such as:
  - `Bone Path`: bind to skeleton bone
  - `Locator Path`: bind to imported/model locator marker
- explain precedence and fallback rules when both bone path and locator path are
  present

## Out Of Scope

- Rewriting the whole geometry data model
- Changing runtime transform resolution behavior unless required for correctness
- Replacing all technical identifiers with friendly aliases everywhere

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`
- `Docs/Interfaces/CharacterApplication.md`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Tools/MxFramework.CharacterStudio/README.md`
- Relevant CharacterStudio field rendering code in `web/app.js`

## Allowed Read/Write

- `Tools/MxFramework.CharacterStudio/web/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- Authors can inspect the full configured value for long path fields without
  ambiguity.
- `locatorPath` is explained in plain language and no longer appears as an
  unexplained technical field.
- Users can tell when to use bone path, locator path, or both.
- Existing save/load behavior for these fields remains compatible.

## Validation

- Manual walkthrough editing sockets/body parts with long bone and locator paths
- Confirm full values are readable
- Confirm first-time authors can explain the meaning of `locatorPath` after
  reading the field guidance

## Public API

- No public runtime API changes intended

## Agent Constraints

- Favor author clarity over raw internal terminology
- Preserve compatibility with existing package files
