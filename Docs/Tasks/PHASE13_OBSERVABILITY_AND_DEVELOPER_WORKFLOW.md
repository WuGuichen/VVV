# Phase 13: Observability and Developer Workflow

> Parent Epic: #177
> Issues: #178, #179, #180, #181, #182, #183, #184, #185, #186, #187
> Status: #178-#184 merged; #185-#187 implementation in review
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
| Event timeline and entity watch | #182 | Timeline view model, Gameplay / Combat timeline adapters and Gameplay component entity watch source |
| Performance counters | #183 | Opt-in noEngine performance counter snapshots and RuntimeHost / Gameplay / Combat adapter counters |
| Simulation Harness reports | #184 | noEngine batch scenario runner with Markdown / JSON report formatting and debug source export |
| Config / template hot reload observation | #185 | Explicit runtime patch reload service, result contract, polling helper, Debug UI source and Demo composition root |
| Debug UI input adapter and command gate | #186 | Optional Input adapter using `InputContext.Debug`, read-only command gate contracts and command diagnostics source |
| Observability debugging guide | #187 | Practical guide for source registration, overlay input, command gates, hot reload, timeline, counters and Simulation Harness reports |

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
4. Add event timeline and entity watch views on top of existing Gameplay / Combat diagnostics without creating new authoritative event semantics.
5. Add opt-in performance counters and noEngine Simulation Harness reports for regression-oriented checks.
6. Add explicit Config Runtime patch hot reload results and expose them through the observation layer.
7. Add optional Debug UI input routing and a disabled-by-default command gate without moving write commands into snapshots.
8. Document the end-to-end debugging path with implemented APIs only.

## Done Definition

- `Docs/ROADMAP.md`, `Docs/README.md`, `Docs/CAPABILITIES.md` and `Docs/Interfaces/DebugUI.md` describe Phase 13.
- `MxFramework.DebugUI` aggregates available, unavailable and failing sources without throwing out the whole dashboard.
- `MxFramework.DebugUI.Toolkit` can switch Hidden / Collapsed / Expanded and render source sections and errors.
- Existing Logging and Resources sources can be registered beside RuntimeHost / Gameplay / Combat adapters.
- Timeline entries expose frame, source, category, entity, trace id and summary fields with source/entity/category filtering.
- Entity watch exposes id, active state, key attributes, pressure band, guard state and armor state from component-world diagnostics.
- Performance counters are disabled by default and can be exposed through Diagnostics snapshots without adding runtime authority.
- Simulation Harness can run deterministic noEngine scenarios and export Markdown / JSON reports with metrics, timeline events and failures.
- Config Runtime patch hot reload is explicit, produces source/version/hash/duration/change/error details, and does not mutate the active provider on failure.
- Debug UI input routing goes through `IInputProvider`, `InputContext.Debug` and debug intents; it does not read Unity device APIs directly.
- Debug UI commands are behind `DebugUiCommandGate`, disabled by default, require descriptors and confirmations for risky operations, and stay outside `FrameworkDebugSnapshot`.
- `Docs/Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` matches the implemented Debug UI, Diagnostics, Config Runtime and Input APIs.
- Boundary checks confirm no Unity, Editor, UI Toolkit or Input System references in the noEngine DebugUI core.
