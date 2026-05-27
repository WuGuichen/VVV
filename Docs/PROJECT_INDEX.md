# MxFramework Project Index

> Status: Current
>
> Agent token-budget entrypoint. Read the smallest useful context pack first, then jump to source and tests.

## Default Context Pack

Read this pack before ordinary framework work:

1. `AGENTS.md`
2. `Docs/README.md`
3. The active Gitea Issue, PR comment, or user request
4. `Docs/CAPABILITIES.md` only when checking current supported capabilities
5. `Docs/INTERFACES.md` and the relevant `Docs/Interfaces/*.md` for modules being touched
6. Relevant source files and tests

Do not read all design, guide, workflow, or task documents by default.

## Conditional Packs

| Task shape | Add these docs |
|------------|----------------|
| Workflow, branch, PR, validation, harness, or repository delivery | `Docs/WORKFLOW.md`, `Docs/QUALITY_GATE.md` |
| Public API, module boundary, dependency direction, naming, compatibility, or GC behavior | `Docs/API_STANDARDS.md`, `Docs/INTERFACES.md`, relevant `Docs/Interfaces/*.md` |
| Game feature, playable demo, runtime showcase, scene verification, input/UI/save/replay extension | `Docs/AGENT_GAME_CREATION_GUIDE.md`, then relevant module interfaces |
| Rendering pass, SharedRT, material binding, camera global, Volume request, URP Feature, or rendering demo | `Docs/RENDERING_AUTHORING_GUIDE.md`, `Docs/Interfaces/Rendering.md`, then design docs only if needed |
| Runtime host, lifecycle, command buffer, replay, save state, app/scene flow | `Docs/Interfaces/Runtime.md`, `Docs/Interfaces/AppFlow.md`, `Docs/RUNTIME_FOUNDATION_SYSTEM.md` |
| Resource manager, catalog, locator, package, build profile, or resource workflow | `Docs/RESOURCE_SYSTEM_WORKFLOW.md`, `Docs/Interfaces/Resources.md`, relevant resource design docs only if needed |
| Debug UI, diagnostics, logging, timeline, hot reload, simulation harness troubleshooting | `Docs/Guides/OBSERVABILITY_DEBUGGING_GUIDE.md`, `Docs/Interfaces/Diagnostics.md`, `Docs/Interfaces/DebugUI.md` |
| Roadmap, phase planning, or stale status triage | `Docs/ROADMAP.md`, then current source / capability / interface docs to verify facts |
| Historical decision, old task evidence, regression archaeology, or Issue-linked task file | `Docs/Decisions/*.md` or `Docs/Tasks/README.md` plus the referenced task file |

## Control Plane

- Gitea Issue = Agent Spec
- Gitea labels = Agent queue state
- Gitea branch = Agent sandbox
- Gitea PR = delivery and audit record
- Gitea Actions / Runner = Harness
- `Docs/Decisions/` = accepted project decisions

## Do Not Read By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `.codex/cache/`
- `Docs/Tasks/`
- `Docs/Progress/`
- `Assets/Plugins/`
- third-party package internals

Read these only when the active Issue, PR, error, or user request explicitly points there.

## Conflict Rule

Use the status model in `Docs/README.md`:

1. Source and tests are the implementation truth.
2. Current docs define maintained project facts.
3. Guide docs explain current usage and troubleshooting.
4. Design docs explain intent, but do not override current API docs.
5. ADRs preserve accepted decisions.
6. Archive and Draft docs are never current facts unless promoted into Current / Guide / Design / ADR docs.
