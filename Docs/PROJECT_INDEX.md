# MxFramework Project Index

This file is the token-budget entrypoint for agents. It points to the smallest set of authoritative docs needed before reading source code.

## Read Order

1. `AGENTS.md`
2. `Docs/README.md`
3. Issue-specified docs and ADRs
4. Relevant `Docs/Interfaces/*.md`
5. Relevant source and tests

`Docs/WORKFLOW.md` and `Docs/GITNEXUS.md` are authoritative, but ordinary implementation tasks do not need to read them in full. Read the relevant sections only when workflow, branch, PR, Harness, GitNexus, or Agent behavior is unclear.

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
- `.gitnexus/`
- `.codex/cache/`
- `Assets/Plugins/`
- third-party package internals

## Main Workflow Docs

- `Docs/WORKFLOW.md`: daily development, Gitea control plane, PR, harness, backup.
- `Docs/GITNEXUS.md`: code intelligence, impact analysis, index maintenance.
- `Docs/AGENT_GAME_CREATION_GUIDE.md`: game feature, playable demo, runtime showcase rules.
- `Docs/QUALITY_GATE.md`: validation and acceptance expectations.
- `Docs/RENDERING_PIPELINE.md`: current URP project baseline and rendering validation rules.
- `Tools/GiteaGithubSync/README.md`: manual Gitea Issue / PR metadata mirror to GitHub.
