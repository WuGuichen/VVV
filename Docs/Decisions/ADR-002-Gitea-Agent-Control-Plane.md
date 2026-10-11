# ADR-002: Treat Gitea As The Agent Control Plane

> Status: ADR

Date: 2026-05-13

Decision Status: Rescinded by the repository owner on 2026-08-08; retained only as historical context.

## Current Effect

None. The repository no longer defines Gitea as a mandatory Agent Control Plane and does not require an Issue queue, Context Pack, task branch, pull request, harness sequence, review record, or knowledge-writeback sequence.

## Historical Decision

This ADR previously defined Gitea Issues as executable Agent specs, branches as Agent sandboxes, pull requests as delivery records, CI as a task-boundary harness, and webhooks as Agent triggers. That model is no longer operative. The full former text remains available in Git history.

## Rescission Consequences

- Gitea features may still be used when useful, but this ADR grants them no mandatory workflow role.
- No older Issue/branch/PR policy is revived by this rescission.
- Repository hosting roles remain defined by `ADR-001-Version-Control-Gitea.md`.

## References

- `Docs/Decisions/ADR-001-Version-Control-Gitea.md`
- `Docs/Decisions/ADR-013-unified-main-fast-lane.md`
