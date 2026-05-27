# MxFramework Project Index

This file is the token-budget entrypoint for agents. It points to the smallest set of authoritative docs needed before reading source code.

## Read Order

1. `AGENTS.md`
2. `Docs/README.md`
3. Issue-specified active docs and ADRs
4. Relevant `Docs/Interfaces/*.md`
5. Relevant source and tests

`Docs/WORKFLOW.md` is authoritative for daily development, validation, branch, PR, Harness, and Agent behavior. Ordinary implementation tasks do not need to read it in full; read the relevant sections only when workflow details are unclear.

## Control Plane

- Gitea Issue = Agent Spec
- Gitea labels = Agent queue state
- Gitea branch = Agent sandbox
- Gitea PR = Agent delivery and audit record
- Gitea Actions / Runner = Harness
- `Docs/Decisions/` = accepted project decisions

## Do Not Read By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `.codex/cache/`
- `Docs/Tasks/` unless the issue explicitly references a task file
- `Assets/Plugins/`
- third-party package internals

## Main Workflow Docs

- `Docs/WORKFLOW.md`: daily development, Gitea control plane, PR, harness, backup.
- `Docs/AGENT_GAME_CREATION_GUIDE.md`: game feature, playable demo, runtime showcase rules.
- `Docs/QUALITY_GATE.md`: validation and acceptance expectations.
- `Docs/RENDERING_PIPELINE.md`: current URP project baseline and rendering validation rules.
- `Docs/RENDERING_FRAMEWORK_DESIGN.md`: Rendering framework bus, URP feature entry, context, SharedRT, and bridge boundaries.
- `Docs/RENDERING_AUTHORING_GUIDE.md`: single authoring entry for Rendering shader globals, camera globals, SharedRT, pass/provider, material binding, publisher, VolumeBlender, demo, and diagnostics rules.
- `Tools/GiteaGithubSync/README.md`: manual Gitea Issue / PR metadata mirror to GitHub.
