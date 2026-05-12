# ADR-002: Treat Gitea As The Agent Control Plane

Date: 2026-05-13

Status: Accepted

## Context

Issue -> Branch -> PR -> Merge is not sufficient if used as a traditional human-only workflow. MxFramework needs a workflow that lets AI agents execute substantial work while keeping task definition, permissions, review, and long-term knowledge under human control.

Modern agent workflows still use issues and pull requests, but with different meanings:

- Issue is an executable Agent Spec.
- Branch is an Agent sandbox.
- PR is the Agent delivery artifact and audit record.
- CI is a harness that checks task boundaries and validation evidence.
- Webhook is an Agent trigger.
- Docs / ADR are the long-term truth source.

## Decision

Use Gitea as the local Agent Control Plane:

```text
Spec Issue -> Agent Queue -> Context Pack -> Agent Branch -> Harness -> Self-review PR -> Human Gate -> Docs / ADR / Memory Update
```

Gitea owns:

- Issue specs
- queue labels
- protected branches
- PR review and audit
- Actions / Runner harness entry
- webhooks for Hermes / local agents
- low-permission automation tokens

Agents may create branches, push task branches, create PRs, and comment. Agents may not push `main`, merge PRs, modify branch protection, delete repositories, or change Gitea settings.

## Consequences

- Agent tasks become spec-first rather than chat-first.
- Context Pack prevents default whole-repo scanning.
- PRs contain Agent Session audit records.
- Harness checks both tests and task-boundary violations.
- Owner remains the final merge gate.
- Docs, ADR, progress notes, and memory become required knowledge writeback targets after meaningful merges.

## Alternatives Considered

- Traditional Issue -> Branch -> PR only: safe but misses queue state, context engineering, and agent audit requirements.
- Fully autonomous agent execution: faster, but unsafe for Unity assets, public APIs, and Gitea permissions.
- Chat-first local edits: convenient, but loses task specs, audit trail, and review boundaries.

## References

- `Docs/WORKFLOW.md`
- `Docs/PROJECT_INDEX.md`
- `.gitea/ISSUE_TEMPLATE/task.md`
- `.gitea/PULL_REQUEST_TEMPLATE.md`
