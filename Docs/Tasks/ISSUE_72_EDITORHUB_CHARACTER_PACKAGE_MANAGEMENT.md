# Issue 72 EditorHub Character Package Management

Date: 2026-05-22

Labels:
- `type/design`
- `module/editor`
- `status/spec-draft`
- `priority/low`

## Goal

Extend the current create-package MVP into a proper character package
management workflow in EditorHub.

## Background

The immediate blocker was solved by adding a minimal create-package flow.
However, the broader package lifecycle is still incomplete:

- no package list actions beyond open/select
- no duplicate/archive/delete flow
- no package metadata edit flow
- no dedicated package-management view

That is acceptable for the current authoring slice, but should be tracked as a
follow-up capability gap.

## Scope

Define and optionally implement a package management surface that can:

- list character packages
- create from templates
- duplicate an existing package
- rename display metadata safely
- archive or delete with clear guardrails
- open the selected package in CharacterStudio

## Out Of Scope

- Cross-project package registries
- Remote package publishing
- Runtime package loading changes
- Mod packaging redesign

## Related Modules

- `Tools/MxFramework.EditorHub/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Tools/MxFramework.Authoring/samples/`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/WORKFLOW.md`
- current create-package implementation in `EditorServer.cs` and EditorHub

## Allowed Read/Write

- `Tools/MxFramework.EditorHub/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- Package lifecycle gaps are explicitly designed and broken into implementable
  follow-up steps.
- If implemented, actions respect package identity and reference safety.
- Management actions do not silently corrupt `packageId`, `stableId`, or
  package-local resource references.

## Validation

- Design review or prototype walkthrough
- If code lands, manual package lifecycle checks for create/duplicate/open

## Public API

- Tooling API only; no runtime API changes intended

## Agent Constraints

- Keep identity rewrite rules explicit
- Do not expand this task into asset registry redesign
