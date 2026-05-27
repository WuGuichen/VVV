# Task Archive

> Status: Archive
>
`Docs/Tasks/` stores historical task specs, implementation slices, closeout notes, and validation evidence.

These files are **not** the current source of truth for framework capabilities, public APIs, workflows, or validation policy.

## Read Policy

- Do not include `Docs/Tasks/` in the default Agent Context Pack.
- Read a task document only when a Gitea Issue explicitly references it, when tracing why a historical decision was made, or when using an old task as a pattern for a similar new task.
- Treat completed task files as evidence from the time they were written. They may be stale.
- For current facts, prefer `Docs/CAPABILITIES.md`, `Docs/INTERFACES.md`, `Docs/Interfaces/*.md`, `Docs/WORKFLOW.md`, `Docs/QUALITY_GATE.md`, source code, and tests.

## Maintenance Policy

- Do not keep completed task documents in sync with every later implementation detail.
- When a task produces durable knowledge, copy that knowledge into the proper current document before closing the task.
- Keep task files for audit and regression archaeology unless there is a deliberate archive cleanup task.
