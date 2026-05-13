# GAMEPLAY_COMPONENT_PLAYABLE_COMBAT_BRIDGE_PLAN_01

> Issue: #5
> Status: Spec / Implementation Plan
> Task level: S3
> Delivery level for this issue: Framework Feature

## Goal

Turn the current Gameplay Component Runtime showcase from a verified Runtime Slice into a committed Unity Playable entry, then define the first Combat bridge boundary without moving Gameplay source of truth into Combat.

This document is a plan only. It does not implement the playable scene, Combat runtime bridge code, cast-time, interrupt, or new Unity serialized assets.

## Current State

The component runtime already has a deterministic runtime path:

```text
RuntimeHost
-> RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplaySystemPipeline
-> GameplayComponentWorld
-> spawn / attributes / targeting / ability rules / buff-modifier cleanup
-> RuntimeEventQueue
-> GameplayComponentWorldHashContributor
-> GameplayComponentWorldSaveStateProvider
```

`GameplayComponentRuntimeShowcase` and `GameplayComponentRuntimeShowcaseRunner` already demonstrate spawn, target, cost, cooldown, cleanup, hash, SaveState, and UI Toolkit binding. `Docs/CAPABILITIES.md` currently marks it as `Runtime Slice`, because the committed repository does not yet include a generated playable scene.

Combat already provides deterministic physics and motion primitives:

- `CombatPhysicsWorld` owns bodies, AABB colliders, deterministic query dispatch, `QueryBatch`, and `ExplainQuery`.
- `CombatKinematicMotor` can step motion from explicit frame/input state and optionally sync a `CombatPhysicsBody`.
- `CombatGameplayEventBridge` currently bridges `HitResolveResult` into legacy `AbilityEvent` for `RuntimeEntity`; it is not the component runtime bridge.

## API Reuse Plan

| Requirement | Primary framework APIs / modules | Use in follow-up implementation | Notes |
| --- | --- | --- | --- |
| Runtime loop / fixed frame | `RuntimeHost`, `GameplayRuntimeModule`, `RuntimeFrame`, `RuntimeCommandBuffer` | Required | One command buffer drain owner remains `GameplayRuntimeModule`; Unity only enqueues commands. |
| Player intent / UI actions | UI Toolkit buttons, optional Input adapter, `RuntimeCommandBuffer` | Required | Buttons or input adapters cannot mutate `GameplayComponentWorld` directly. |
| Entity and lifecycle state | `GameplayComponentWorld`, `GameplayEntityId`, `GameplayLifecycleComponent`, `GameplayLifecycleCleanupSystem` | Required | Component world remains the Gameplay source of truth. |
| Spawn definitions | `GameplayComponentSpawnRegistry`, `GameplayComponentSpawnCommandSystem` | Required | Demo actors are created by command-driven spawn definitions, not scene objects as authoritative entities. |
| Attributes | `GameplayAttributeSetComponent`, `GameplayAttributeCommandSystem`, schema hash/save adapters | Required | HP, mana, attack, and future bridge damage results remain component state. |
| Buff / modifier state | `GameplayComponentBuffSetComponent`, `GameplayComponentModifierSetComponent`, `GameplayComponentBuffCleanupSystem` | Required when effects need status/modifier state | Do not create a parallel buff or modifier pipeline for the playable. |
| Ability targeting / rules | `GameplayComponentAbilityRequestStore`, `GameplayComponentTargetingService`, `GameplayComponentAbilityRuleSet` | Required | Generation-safe request handles remain transient input, not SaveState. |
| Combat query / motion | `CombatPhysicsWorld`, `CombatPhysicsQuery`, `ExplainQuery`, `CombatKinematicMotor` | Required for bridge slice | Combat answers spatial questions; it does not own Gameplay HP, lifecycle, buffs, or ability rules. |
| UI diagnostics | UI Toolkit, existing `MxFramework.UI.Toolkit` controls where useful | Required | HUD must show actionable state: frame, entities, HP/mana, cooldown, hash, SaveState, Combat explain summary. |
| Replay / hash | `RuntimeReplayRecorder` where needed, `GameplayComponentWorldHashContributor`, Combat deterministic summaries | Required for validation | First playable can expose hash and command flow; full replay export can be a separate slice. |
| Save / restore | `RuntimeSaveStateJson`, `GameplayComponentWorldSaveStateProvider` | Required | SaveState captures component world results, not transient request store or Unity view objects. |
| Resources | `ResourceCatalog`, `ResourcePreloadService`, `RuntimeVerticalSliceResourceCatalog` pattern | Required for committed playable scene | UXML/USS and runtime assets should warm up through catalog-driven paths where stable assets are referenced. |
| Scene flow | `AppFlowController`, `SceneFlowController`, `UnitySceneFlowDriver` | Optional for first playable, required if Boot + Gameplay scenes are split | A single scene may skip AppFlow only if the spec records why. |

No existing module is intentionally bypassed. The only missing area is a component-native Combat bridge API; the follow-up should add the smallest bridge layer instead of overloading the legacy `RuntimeEntity` bridge.

## Layering

The playable entry should keep the same ownership model as the runtime slice:

```text
Pure Runtime Composition
  GameplayComponentRuntimeShowcase or successor composition root
  RuntimeHost + command buffer + GameplayRuntimeModule
  GameplayComponentWorld + schemas + registries
  optional CombatPhysicsWorld + CombatKinematicMotor bridge service

Unity Composition Root
  one MonoBehaviour on the scene root
  binds UIDocument, input adapter, catalog warmup, and view transforms
  does not own authoritative gameplay state

UI Toolkit View
  UXML / USS for HUD and controls
  C# only binds snapshot data and enqueues commands

Scene / Assets
  generated through Unity Editor / Unity MCP / Editor menu
  committed `.unity` only after Unity-generated validation
```

## Playable Scene Plan

The first implementation slice should upgrade the existing showcase into a real Playable before adding Combat behavior.

Target scene:

```text
Assets/Scenes/GameplayComponentRuntimeShowcase.unity
```

Required scene content:

- One clear composition root with `GameplayComponentRuntimeShowcaseRunner` or its successor.
- `UIDocument` with committed `PanelSettings`, `VisualTreeAsset`, and `StyleSheet` references.
- UI Toolkit HUD with non-empty labels and command buttons for spawn, cast, cleanup, save, restore, reset, and scripted flow.
- Optional simple view markers for hero and enemy, driven from runtime snapshots only.
- Catalog warmup for HUD and stable runtime assets using the M7 resource catalog pattern.

Playable acceptance:

- Opening the scene and pressing Play requires no manual component wiring.
- At least one input path follows:

```text
button / input adapter
-> RuntimeCommandBuffer
-> RuntimeHost.Tick
-> GameplayRuntimeModule
-> GameplayComponentWorld state change
-> UI/view refresh
<- UI refresh through snapshot binding
```

- The user can complete the current loop: spawn, cast Strike, observe cooldown rejection, cast again, mark/cleanup enemy, save, restore, and reset.
- The HUD shows runtime frame, hash, actor state, cooldown, pending requests/commands, SaveState status, and event log.
- Unity Console has no new errors. Warnings must be recorded with justification.

## Combat Bridge Boundary

The first Combat bridge should answer spatial and motion questions for component Gameplay without transferring Gameplay authority to Combat.

### Data From Gameplay To Combat

Only the minimum spatial proxy data should cross into Combat:

- `GameplayEntityId` mapped to stable `CombatEntityId` / `CombatBodyId` by an adapter.
- lifecycle state used to decide whether a body should be present in `CombatPhysicsWorld`.
- team or layer mapping used to build `CombatPhysicsLayerMask`.
- position, facing, simple body size, and collider shape data for Combat bodies.
- ability/action query intent: source entity, target mask, shape, range, and trace id.

The adapter may maintain a lookup table, but the authoritative Gameplay entity lifecycle remains in `GameplayComponentWorld`.

### Data From Combat To Gameplay

Combat may return deterministic facts:

- sorted query hits from `CombatPhysicsWorld.Query` / `QueryBatch`;
- `CombatPhysicsQueryDebugReport` from `ExplainQuery`;
- motion step results from `CombatKinematicMotor`;
- hit resolve summaries if `HitResolveSystem` is used in the slice.
- zero-hit query results as an empty candidate set; Gameplay should apply no damage and trigger no hit animation from that query.

Gameplay systems then decide what those facts mean:

- target selection can use Combat hits as candidates, then still apply `GameplayComponentTargetingService` filters;
- damage is applied by component attribute commands or component ability effects;
- lifecycle/death stays with Gameplay systems;
- buffs, modifiers, cooldown, costs, interrupts, and SaveState stay in Gameplay/Runtime-owned state.

### Explicit Non-Goals

The first bridge must not:

- make `CombatPhysicsWorld` store HP, mana, buffs, modifiers, ability cooldown, or lifecycle source of truth;
- replace `GameplayComponentAbilityRequestStore` with Combat query state;
- make Unity `Collider`, `Rigidbody`, `OnTriggerEnter`, or `UnityEngine.Physics` authoritative;
- extend the legacy `CombatGameplayEventBridge` by forcing component entities through `RuntimeEntity`;
- encode generation ids into ad hoc command payload fields not documented by `GameplayRuntimeCommandFactory`.

## Proposed Implementation Slices

### Slice 1: Commit Playable Scene

Create a `status/agent-ready` implementation issue for:

- generating and committing `Assets/Scenes/GameplayComponentRuntimeShowcase.unity`;
- committing any generated `.meta` files and required UI / PanelSettings assets;
- adding catalog entries or generation support for the showcase UI assets if needed;
- updating `Docs/USAGE.md` and `Docs/CAPABILITIES.md` from Runtime Slice to Playable only after Play Mode smoke passes.

Validation:

- Unity scene generation via Editor / Unity MCP / existing menu;
- Unity Console no new errors;
- Play Mode smoke: spawn -> cast -> cooldown reject -> save/restore;
- `git diff --check`;
- relevant Demo tests.

### Slice 2: Component Combat Proxy Contract

Create an implementation issue for a small component-native bridge contract:

- a noEngine adapter type that maps `GameplayEntityId` to Combat proxy ids;
- deterministic body/collider sync from component state into `CombatPhysicsWorld`;
- diagnostics that show missing lifecycle/body mappings and stale ids;
- tests for create, update, remove, stale generation, and deterministic body order.

Likely write scope:

- `Assets/Scripts/MxFramework/Combat/GameplayBridge/`
- `Assets/Scripts/MxFramework/Tests/Combat/GameplayBridge/`
- `Docs/Interfaces/Gameplay.md` or a future Combat interface doc if public API is added.

### Slice 3: Combat Query Target Candidates

Create an implementation issue for query-backed targeting:

- build a `CombatPhysicsQuery` from component ability/query intent;
- run `CombatPhysicsWorld.Query` and `ExplainQuery`;
- convert sorted hits into `GameplayComponentTargetCandidate` input;
- apply existing `GameplayComponentTargetingService` relation/tag/status filters;
- expose explain rows in the UI diagnostics.

Validation:

- hit / miss / filtered source / filtered layer tests;
- deterministic hit ordering independent of registration order;
- no direct Unity Physics usage in runtime path.

### Slice 4: Motion Bridge For Playable Movement

Create an implementation issue only after Slice 2 is stable:

- convert input intent into runtime commands;
- step `CombatKinematicMotor`;
- sync accepted motion back to the component-side position/proxy state through a documented adapter;
- update Unity transforms from runtime snapshots.

Validation:

- wall/blocking collision tests;
- deterministic motion sequence hash or summary comparison;
- Play Mode smoke with visible movement and UI diagnostics.

### Slice 5: Cast-Time / Interrupt Design

Defer cast-time and interrupt until the playable and spatial bridge are stable.

The future design should use a component-native pending operation/timeline state, fixed frames, and Runtime command cancellation. It should not be implemented as a Unity coroutine or Animator transition authority.

## Validation Gates

Before any follow-up PR claims `Playable`:

- Scene exists under `Assets/Scenes/` and was generated by Unity Editor / Unity MCP / existing menu.
- `UIDocument.panelSettings`, UXML, USS, and stable resources are bound in generated assets.
- UI labels/buttons are visible, non-empty, and not backed only by runtime-created fallback assets.
- Input or button flow goes through `RuntimeCommandBuffer`.
- `GameplayComponentWorldHashContributor` changes across real state changes and remains stable across SaveState restore.
- SaveState JSON roundtrip restores component world state and does not capture transient request handles.
- Unity Console has no new errors.
- Unity Console warnings are reviewed and justified if they are accepted or suppressed.

Before any follow-up PR claims Combat bridge support:

- Tests cover hit, miss, stale generation, lifecycle removal, deterministic ordering, and explain diagnostics.
- Runtime code has no `UnityEditor` reference and no Unity Physics authority.
- Public API docs explain whether a type is source-of-truth state, transient adapter state, or view/diagnostic data.

## Risks

- A committed scene can drift from generated UI assets if future changes rely on fallback UI tree creation. The playable slice must bind real UXML/USS/PanelSettings assets.
- The existing legacy `CombatGameplayEventBridge` can look tempting, but it bridges `HitResolveResult` to `RuntimeEntity` ability events and should not be stretched into component world authority.
- `GameplayRuntimeEvent` already carries many event families. Combat bridge details should avoid piling more unrelated fields into the generic event unless a bounded event-detail contract is introduced.
- Pending ability requests are transient and not saved. Follow-up slices must avoid designing save/restore around in-flight request handles until Runtime command/save orchestration defines that boundary.
- If Unity scene generation is blocked by another Editor instance, the playable implementation issue must remain a Runtime Slice and record the blocker instead of claiming Playable.

## Docs And ADR Status

- This plan adds a task/spec document only.
- No public API changes are made in this issue.
- No ADR is required yet because the plan follows existing module boundaries: Gameplay owns state, Combat answers deterministic spatial/motion queries, Unity is view/composition. This plan does not create new cross-module dependencies or reverse existing dependency direction.
- Follow-up implementation issues that add public bridge types must update interface docs and may need an ADR if they change module dependency direction.

## Files Inspected For This Plan

- `AGENTS.md`
- `Assets/AGENTS.md`
- `Assets/Scripts/MxFramework/AGENTS.md`
- `Docs/PROJECT_INDEX.md`
- `Docs/WORKFLOW.md`
- `Docs/AGENT_GAME_CREATION_GUIDE.md`
- `Docs/README.md`
- `Docs/CAPABILITIES.md`
- `Docs/Interfaces/Gameplay.md`
- `Docs/COMBAT_ANIMATION_PHYSICS.md`
- `Docs/Tasks/GAMEPLAY_COMPONENT_RUNTIME_SHOWCASE_01.md`
- `Docs/Tasks/GAMEPLAY_COMPONENT_RUNTIME_V0_CLOSEOUT.md`
- `Docs/Tasks/GAMEPLAY_ECS_STYLE_19_COMPONENT_RUNTIME_VERTICAL_SLICE.md`
- `Docs/Tasks/RESOURCE_MANAGEMENT_M7_RUNTIME_ASSET_CATALOG.md`
- `Assets/Scripts/MxFramework/Demo/GameplayComponentRuntime/GameplayComponentRuntimeShowcase.cs`
- `Assets/Scripts/MxFramework/Demo/GameplayComponentRuntime/GameplayComponentRuntimeShowcaseRunner.cs`
- `Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsWorld.cs`
- `Assets/Scripts/MxFramework/Combat/Motion/CombatKinematicMotor.cs`
- `Assets/Scripts/MxFramework/Combat/GameplayBridge/CombatGameplayEventBridge.cs`
- `Assets/Scripts/MxFramework/Tests/Combat/GameplayBridge/CombatGameplayEventBridgeTests.cs`
