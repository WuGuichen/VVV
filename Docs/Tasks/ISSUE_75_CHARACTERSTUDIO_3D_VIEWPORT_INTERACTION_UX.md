# Issue 75 CharacterStudio 3D Viewport Interaction UX

Date: 2026-05-22

Labels:
- `type/refactor`
- `module/editor`
- `status/spec-draft`
- `priority/high`

## Goal

Improve the CharacterStudio 3D viewport so authors can inspect and edit
character geometry comfortably and directly, without eye strain, camera flicker,
or a form-only workflow.

## Background

The current CharacterStudio 3D editing experience has three major problems:

1. The viewport background and overall lighting are too bright, causing visual
   fatigue.
2. Selecting parts/components causes the camera or whole viewport to refresh
   and visibly flash, which breaks editing flow.
3. Authors cannot directly manipulate items inside the 3D scene, making socket,
   collider, and related geometry editing unnecessarily cumbersome.

These problems make the editor feel unstable and increase the cost of basic
authoring tasks.

## Scope

- Improve viewport visual theme and contrast so the scene is comfortable to use
  for long sessions.
- Eliminate full viewport flash/rebuild when selecting items in the editor UI.
- Preserve camera state when changing selection unless the user explicitly asks
  to frame/focus a target.
- Add direct in-viewport manipulation for applicable authoring objects such as:
  - sockets
  - colliders
  - traces
  - weapon attachments
  - body parts or markers where applicable
- Support standard transform-like interaction modes where practical:
  - move
  - rotate
  - scale or radius/extent adjustment when relevant

Possible implementation options:

- darker neutral viewport background with adjustable grid/lighting intensity
- selection highlight updates without reinitializing the renderer or camera
- explicit `Focus` action instead of implicit camera jumps on every selection
- transform gizmos or handles in the WebGL scene for editable objects
- split “inspect selected object” from “rebuild whole preview scene”

## Out Of Scope

- Rewriting the entire renderer stack from scratch
- Final DCC-grade modeling features
- Full animation authoring interaction in the viewport
- Unity SceneView parity for every editing operation

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.EditorHub/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Tools/MxFramework.CharacterStudio/README.md`
- Existing viewport-related code in `Tools/MxFramework.CharacterStudio/web/`

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

- The default viewport presentation is no longer harshly bright.
- Changing selection does not cause visible full-screen flashing or reset the
  camera unexpectedly.
- Authors can directly manipulate at least one major geometry type in the 3D
  scene instead of editing only through form fields.
- Camera framing/focus becomes an explicit action rather than an implicit side
  effect of ordinary selection changes.
- Existing package save behavior remains compatible.

## Validation

- Manual authoring walkthrough selecting multiple sockets/colliders/traces in
  sequence and confirming no disruptive flash or camera reset
- Verify viewport remains readable on long editing sessions
- Verify in-scene manipulation updates underlying authoring data and round-trips
  after save/reload

## Public API

- No public runtime API changes intended

## Agent Constraints

- Prioritize stable editing flow over cosmetic polish alone
- Avoid introducing heavy scene rebuilds on every selection update
- Keep advanced interaction optional if some object types still require form
  editing in early iterations
