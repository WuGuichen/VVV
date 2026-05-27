# Current Status

Date: 2026-05-13

## Workflow State

- NAS Gitea `origin` is the primary repository, task system, branch host, PR review entry, permission boundary, and automation source.
- GitHub `github` is only a non-LFS Git mirror.
- Daily development should use Spec Issue -> Agent Branch -> PR -> checks -> owner review -> merge main.
- Task levels S0-S3 control workflow weight so small documentation changes do not require the full Agent Control Plane.
- `status/in-progress` is reserved for human work; `status/agent-running` is for agent execution; `status/ready-to-merge` replaces ambiguous issue-level `status/approved`.
- Impact analysis is handled with `git diff`, `rg`, source reading, and relevant tests.
- Default Context Pack uses `AGENTS.md`, `Docs/PROJECT_INDEX.md`, issue-specified active docs / ADRs, relevant interfaces, source, and tests; completed `Docs/Tasks/` files are archive evidence and are read only when explicitly referenced or needed for historical tracing.
- `Docs/WORKFLOW.md` is the workflow authority.
- `Docs/Decisions/ADR-001-Version-Control-Gitea.md` records the Gitea primary repository decision.
- `Docs/Decisions/ADR-002-Gitea-Agent-Control-Plane.md` records the Agent Control Plane decision.

## Next Operational Steps

- Configure Gitea protected `main`.
- Create labels from `Docs/WORKFLOW.md`.
- Wire the new `Tools/Harness/run_lightweight_checks.sh` command into Gitea Actions after the runner target branch fetch behavior is confirmed.
- Wire webhook / Hermes as a notification and summary layer, not an auto-merge layer.
- Add backup restore checks that verify Git LFS objects can be pulled, not just Git pointer files.

## Harness State

- `Tools/Harness/` now contains the first local lightweight check entrypoint.
- Current coverage: forbidden generated/cache path changes, root Unity project file churn, Unity `.meta` pairing for changed `Assets/` files, and `git diff --check`.
- Unity batchmode, PR template linting, public API documentation detection, and Gitea workflow wiring remain future Harness increments.
