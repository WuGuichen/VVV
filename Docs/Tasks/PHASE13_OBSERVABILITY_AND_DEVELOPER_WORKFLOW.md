# Phase 13: Observability and Developer Workflow

> Parent Epic: #177
> Issues: #178, #179, #180, #181
> Status: Implementation started
> Task level: S3 batch, with S0/S1 docs and S2 noEngine API slices
> Date: 2026-05-18

## Goal

Phase 13 turns existing diagnostics, logging, runtime host, resource, gameplay and combat snapshots into one developer-facing observation path. It is not a gameplay feature expansion. It exists to make Play Mode debugging, regression checks, source inspection and future simulation tooling easier to use without changing runtime authority.

## Scope

| Slice | Issue | Delivery |
| --- | --- | --- |
| Roadmap and task docs | #178 | Phase 13 roadmap entry, task index, capability and interface docs |
| Debug UI core | #179 | noEngine source registry, snapshot aggregation, dashboard view model and visibility state |
| UI Toolkit overlay shell | #180 | Runtime UI Toolkit controller and binder for Hidden / Collapsed / Expanded states |
| Source adapters | #181 | RuntimeHost, Gameplay, Combat and existing Logging / Resources source registration path |

## Core Rules

- Debug UI is read-only by default.
- Debug UI state is presentation state and must not enter Replay, SaveState or Runtime hash.
- `MxFramework.DebugUI` remains noEngine and does not reference UnityEngine, UnityEditor, UIElements or Input System.
- Runtime, Gameplay, Combat and Resources do not reference DebugUI. Adapter assemblies or composition roots bridge them outward.
- Registry instances are ordinary objects owned by the game, Demo or tool composition root. There is no global singleton.

## Implementation Order

1. Stabilize the `MxFramework.DebugUI` core registry, aggregator and dashboard view model.
2. Add a programmatic UI Toolkit overlay shell that can render empty dashboards and fake sources without serialized UXML / USS assets.
3. Bridge existing diagnostics into the registry through adapters and Demo composition examples.
4. Keep Hot Reload, event timeline and Simulation Harness behind this observation layer until the source contract is stable.

## Done Definition

- `Docs/ROADMAP.md`, `Docs/README.md`, `Docs/CAPABILITIES.md` and `Docs/Interfaces/DebugUI.md` describe Phase 13.
- `MxFramework.DebugUI` aggregates available, unavailable and failing sources without throwing out the whole dashboard.
- `MxFramework.DebugUI.Toolkit` can switch Hidden / Collapsed / Expanded and render source sections and errors.
- Existing Logging and Resources sources can be registered beside RuntimeHost / Gameplay / Combat adapters.
- Boundary checks confirm no Unity, Editor, UI Toolkit or Input System references in the noEngine DebugUI core.
