# Issue 76 CharacterStudio Model Binding Status Context Bug

Date: 2026-05-22

Labels:
- `type/bug`
- `module/editor`
- `status/spec-draft`
- `priority/high`

## Goal

Fix the CharacterStudio model binding status display so selecting geometry
components does not incorrectly report that the body model is unselected.

## Background

When the author clicks sockets, colliders, body parts, traces, or other
geometry components, the UI may show:

- `Character.Model / 角色主体模型：未选择模型资源`

even though the character package already has a valid bound body model.

This appears to be a context-resolution bug in the status display rather than
actual model binding loss. The current view logic derives the "selected model
resource" from a `resources/...` selection path, so when selection moves to a
`geometry/...` path the status bar falls back to "未选择模型资源".

That is misleading during authoring and makes ordinary component selection look
like a data-loss bug.

## Scope

- Fix model binding status resolution so it reflects the actual package binding,
  not only the currently selected `resources/...` node.
- Distinguish between:
  - current component selection
  - currently bound body model
  - current editable resource field target
- Prevent misleading "未选择模型资源" messages when the bound model still exists.
- Keep geometry selection free to change inspector context without implying the
  model reference was cleared.

Possible implementation options:

- resolve the bound body model from package binding state first, not from
  `selectedPath`
- show separate labels such as `当前选中组件` and `当前绑定模型`
- only show "未选择模型资源" when the package truly has no body model binding

## Out Of Scope

- Redesigning the entire resource binding bar
- Changing actual model binding persistence behavior
- Solving viewport flash or camera reset issues

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
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

- Selecting a geometry component does not falsely report that the character body
  model is unselected.
- The status display reflects the actual bound body model while editing other
  components.
- "未选择模型资源" only appears when no valid body-model binding exists in the
  package.
- Existing body model save/load behavior remains unchanged.

## Validation

- Bind a body model, save, then select multiple sockets/colliders/body parts
- Confirm the status bar continues to show the bound model rather than an empty
  state
- Reload the package and verify the same behavior

## Public API

- No public runtime API changes intended

## Agent Constraints

- Treat this as a misleading-state bug first, not a data-loss issue unless
  persistence reproduction proves otherwise
