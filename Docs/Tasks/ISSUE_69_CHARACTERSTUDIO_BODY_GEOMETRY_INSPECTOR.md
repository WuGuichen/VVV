# Issue 69 CharacterStudio Body Geometry Inspector

Date: 2026-05-22

Labels:
- `type/implementation`
- `module/editor`
- `status/spec-draft`
- `priority/medium`

## Goal

Expose `geometry/body` as an editable inspector in CharacterStudio so authors
can configure the character body profile without editing JSON by hand.

## Background

Current CharacterStudio behavior already:

- loads `geometry/body_geometry.json`
- renders a `geometry/body` tree node
- uses `bodyProfile` for preview fallback and framing

But the inspector field factory does not provide a `kind === "body"` branch, so
the node is effectively read-only from the UI. This breaks the intended
authoring flow for newly created character packages.

## Scope

- Add editable inspector fields for `geometry/body`.
- Persist edits through the existing CharacterStudio save path.
- Keep field semantics aligned with `CharacterBodyGeometryProfile`.
- Ensure the preview updates after edits to height/radius/root identifiers.

Recommended minimum fields:

- `profileId`
- `bodyKind`
- `bodyScale`
- `heightMeters`
- `radiusMeters`
- `massKg`
- `defaultCapsule.height`
- `defaultCapsule.radius`
- `defaultCapsule.center.{x,y,z}`
- `defaultPhysicsProfileId`
- `modelRootStableId`
- `skeletonRootStableId`
- `locatorRootStableId`

## Out Of Scope

- Adding new runtime body profile behavior
- Reworking collider/socket/trace data models
- Auto-deriving body geometry from imported meshes
- Adding Unity-side validation or import changes

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/WORKFLOW.md` relevant Issue/PR sections
- `Tools/MxFramework.CharacterStudio/README.md`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringGeometry.cs`

## Allowed Read/Write

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- third-party package internals

## Acceptance Criteria

- Selecting `geometry/body` shows editable fields in CharacterStudio.
- Editing fields marks the package dirty and can be saved.
- Saved values round-trip into `geometry/body_geometry.json`.
- Preview fallback/body framing reacts to modified height and radius values.
- Existing sample packages still open without console errors.

## Validation

- Static UI check in CharacterStudio
- Save a sample package and confirm `body_geometry.json` updates
- `dotnet build` for touched authoring CLI/core projects when backend code is touched

## Public API

- No public runtime API changes intended

## Agent Constraints

- Do not hand-edit Unity YAML assets
- Keep this as an authoring/editor task only
- Do not expand into package management or resource import redesign
