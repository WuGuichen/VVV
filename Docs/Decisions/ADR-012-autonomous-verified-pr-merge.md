# ADR-012: Autonomous Verified PR Merge

> Status: ADR

Date: 2026-07-19

Decision Status: Rescinded by the repository owner on 2026-08-08; retained only as historical context.

## Current Effect

None. The repository no longer defines an Autonomous Verified Merge gate, mandatory pull-request delivery, S0-S3 merge policy, independent-review sequence, or project-specific merge authority record.

## Historical Decision

This ADR previously replaced a mandatory Human Gate with an evidence-based Development Agent merge gate while retaining PR-only delivery. That decision is no longer operative. The full former gate and transition details remain available in Git history.

## Rescission Consequences

- No owner-only, Human Gate, or Autonomous Verified Merge policy is implied by this ADR.
- No older PR-only policy is revived.
- Repository hosting roles remain defined by `ADR-001-Version-Control-Gitea.md`.

## References

- `Docs/Decisions/ADR-001-Version-Control-Gitea.md`
- `Docs/Decisions/ADR-002-Gitea-Agent-Control-Plane.md`
- `Docs/Decisions/ADR-013-unified-main-fast-lane.md`
