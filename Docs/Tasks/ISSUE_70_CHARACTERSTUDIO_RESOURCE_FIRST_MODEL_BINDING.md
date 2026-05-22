# Issue 70 CharacterStudio Resource-First Model Binding Flow

Date: 2026-05-22

Labels:
- `type/refactor`
- `module/editor`
- `status/spec-draft`
- `priority/medium`

## Goal

Align CharacterStudio with the intended resource workflow: resources are
prepared in the Resource Manager first, then CharacterStudio only binds existing
resources to character fields.

## Background

CharacterStudio still exposes `导入源资源` and related model import actions from
an older workflow where the editor could directly import source models into the
package. This is now misleading because the authoritative flow is:

1. prepare resources in the Resource Manager
2. inspect/diagnose resource state there
3. bind selected resources from CharacterStudio

The old import entry remains usable, but it conflicts with the current tool
responsibility split and makes first-time package creation harder to teach.

## Scope

- Demote or clearly mark the legacy import path in CharacterStudio.
- Make the resource picker path the primary model-binding action.
- Update labels/help text so authors understand the intended flow.
- Ensure body/mainHand/offHand binding works entirely through existing resource
  selection.

Possible implementation options:

- move the import action into a secondary menu labeled legacy/advanced
- keep the button but add explicit legacy wording and link to Resource Manager
- add a primary "Select Existing Resource" action near model binding

## Out Of Scope

- Removing backend import support entirely
- Rebuilding Resource Manager import UX
- Changing resource catalog schema
- Unity runtime import behavior

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.ResourceLibrary/web/`
- `Tools/MxFramework.EditorHub/web/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Tools/MxFramework.CharacterStudio/README.md`
- `Tools/MxFramework.ResourceLibrary/README.md` if present

## Allowed Read/Write

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.ResourceLibrary/web/`
- `Tools/MxFramework.EditorHub/web/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- A first-time author can bind a body model without using the legacy import
  button.
- CharacterStudio clearly communicates that Resource Manager owns resource
  ingestion.
- Legacy import, if retained, is visually marked as non-primary.
- Existing save/bind behavior still works for body and weapon preview bindings.

## Validation

- UI walkthrough from EditorHub -> Resource Manager -> CharacterStudio
- Confirm body resource binding still saves and round-trips

## Public API

- No public runtime API changes intended

## Agent Constraints

- Keep compatibility with existing sample packages
- Do not remove backend import APIs in this task
