# Issue 71 EditorHub Service Restart UX

Date: 2026-05-22

Labels:
- `type/implementation`
- `module/editor`
- `status/spec-draft`
- `priority/medium`

## Goal

Provide a stable in-Hub restart path for the local authoring service so editor
frontend changes and backend API changes can be picked up without manual
process management.

## Background

`start-editor-hub.bat` currently prefers reusing an existing local authoring
service. That is useful for normal startup, but it makes iteration confusing:

- frontend files may update immediately
- backend API changes stay stale inside the old process
- users see `404 Not Found` or network failures after clicking new UI actions

An initial `Restart Service` button and `/api/editor/restart` endpoint were
added, but this flow still needs hardening and product-quality validation on the
normal desktop usage path.

## Scope

- Harden `/api/editor/restart` and the EditorHub restart client flow.
- Ensure restart uses the same boot path as the supported local startup script.
- Show clear reconnect status in the Hub.
- Restore the page cleanly after the service comes back.

## Out Of Scope

- Full multi-instance management
- Remote/distributed service control
- Background Windows service installation

## Related Modules

- `Tools/MxFramework.EditorHub/web/`
- `Tools/MxFramework.EditorHub/start-editor-hub.bat`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/WORKFLOW.md` relevant tooling sections
- `Tools/MxFramework.EditorHub/start-editor-hub.bat`

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

- Restart can be triggered from EditorHub.
- The old authoring process exits.
- A new authoring process starts on the intended port.
- EditorHub can detect recovery and return to a usable state.
- The flow works on the normal local desktop startup path, not only isolated
  test outputs.

## Validation

- Manual restart test from the running local Hub
- Confirm `/api/character/packages` recovers after restart
- Confirm newly added backend endpoints become available after restart

## Public API

- Local tooling API only; no runtime API changes intended

## Agent Constraints

- Reuse the existing startup script or boot chain
- Do not introduce a separate long-term process manager in this task
