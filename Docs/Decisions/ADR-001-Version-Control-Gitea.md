# ADR-001: Use NAS Gitea As Primary Collaboration Source

Date: 2026-05-13

Status: Accepted

## Context

MxFramework is a Unity framework project with Git LFS assets, local Unity validation, manual impact analysis, and AI-assisted development. Direct local edits against `main` make it too easy for humans or agents to bypass task definition, review, and validation.

The project also mirrors non-LFS Git content to GitHub, but GitHub is not the authoritative location for LFS assets, permissions, issue triage, or local automation.

## Decision

NAS Gitea `origin` is the primary repository, task system, branch host, PR review entry, permission boundary, and automation source.

Development follows:

```text
Gitea Issue -> task branch -> development -> PR -> checks -> owner review -> merge main
```

`main` should be protected in Gitea:

- no direct push
- no force push
- no branch deletion
- merge only through PR
- agents cannot merge

GitHub `github` remains a non-LFS Git mirror only. It does not replace Gitea review or LFS backup.

## Consequences

- Issues become the source of task definition.
- PRs become the required safety checkpoint before `main`.
- AI agents must work on controlled branches tied to issues.
- Workflow, permissions, and automation are centralized in Gitea.
- GitHub sync is explicitly separate from daily development.

## Alternatives Considered

- Direct push to `main`: simple, but unsafe for agent-assisted work and easy to bypass review.
- GitHub as primary: better public hosting, but does not match the NAS LFS and local automation setup.
- Git Flow: more complex than needed for the current project stage.

## References

- `Docs/WORKFLOW.md`
- `.gitea/PULL_REQUEST_TEMPLATE.md`
