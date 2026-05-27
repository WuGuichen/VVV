---
name: Bug
about: Reproducible defect or regression
title: ""
labels: "type/bug,status/spec-draft"
---

## Symptom

What is broken?

## Reproduction

Steps, scene, test, command, or data needed to reproduce:

1. 
2. 
3. 

## Expected Behavior

What should happen instead?

## Scope

Likely modules or files:

- 

## Task Level

- [ ] S1: local regression or bug
- [ ] S2: public API / cross-module / core runtime regression
- [ ] S3: Unity asset / playable demo / workflow regression

## Required Reading

- `AGENTS.md`
- `Docs/WORKFLOW.md`
- 

## Context Pack

Allowed reading scope:

- 

Forbidden reading scope:

- `Library/`
- `Temp/`
- `Logs/`
- `.codex/cache/`
- 

## Acceptance Criteria

- [ ] Root cause identified.
- [ ] Fix is covered by the smallest relevant test or validation.
- [ ] Regression risk is documented in the PR.

## Agent Constraints

- [ ] Agent may work on this only after the issue has `status/agent-ready`.
- [ ] Agent must keep the fix scoped to the bug.
- [ ] Agent must report files read, files changed, module impact, public API impact, and Docs / ADR status.
- [ ] Agent must not perform broad refactors unless explicitly approved.
