# ADR-013: Unified Main Fast Lane

> Status: ADR

Date: 2026-07-21

Decision Status: Rescinded by the repository owner on 2026-08-08; retained only as historical context.

## Current Effect

The repository defines no project-specific task, risk-tier, Context Pack, branch, pull-request, commit, push, merge, review, Agent-reporting, or delivery workflow.

This rescission does not revive the older PR-only, Agent Control Plane, Human Gate, or Autonomous Verified Merge policies in ADR-001, ADR-002, or ADR-012. User instructions, applicable repository invariants, task-specific quality authorities, current repository state, and tool or service permissions determine actions when needed.

## Historical Decision

This ADR previously established one `main` fast lane for S0 through S3 work, made Issue/branch/PR use optional, and defined repository-specific commit and publication semantics. That decision is no longer operative. The full former policy remains available in Git history.

## Consequences

- There is no replacement workflow document or compressed workflow summary in `AGENTS.md`.
- Task-specific validation remains owned by the relevant quality, interface, source, and test authorities; it is not converted into a delivery process here.
- NAS Gitea and GitHub repository roles remain defined separately by `ADR-001-Version-Control-Gitea.md`.

## References

- `AGENTS.md`
- `Docs/QUALITY_GATE.md`
- `Docs/Decisions/ADR-001-Version-Control-Gitea.md`
- `Docs/Decisions/ADR-002-Gitea-Agent-Control-Plane.md`
- `Docs/Decisions/ADR-012-autonomous-verified-pr-merge.md`
