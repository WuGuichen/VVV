# Character Control Runtime 00 Design Contract

> Issues: #190, follow-up implementation slices #192, #193, #194, #198
> Delivery level: Framework Feature
> Status: Design Contract + implementation slices (#192, #193, #194, #198)

## Goal

Define the framework-level Character Control orchestration contract. Character Control is the noEngine layer that turns command sources into control state, motion input, action requests, diagnostics, and presentation events without making Unity components, Input, Runtime AI Planner, Gameplay, Combat, or MxAnimation duplicate each other's source of truth.

This document is the Context Pack for the first implementation slices. It does not define WGame-specific characters, element rules, level logic, or concrete skill content. The first implementation adds the noEngine command DTOs, control state machine, Combat Motion resolver, and Combat / Gameplay action bridge described below.

## API Reuse Plan

| Requirement | Primary framework APIs / modules | Use in Character Control | Boundary |
| --- | --- | --- | --- |
| Runtime loop / frame | `RuntimeHost`, `RuntimeFrame`, `RuntimeCommandBuffer` | Character commands carry `RuntimeFrame`; Gameplay ability commands are enqueued into the existing buffer. | Character Control does not drain the shared Gameplay command buffer. |
| Local input | `InputSnapshot`, `InputCommandQueue`, `IInputProvider` | Input adapters translate snapshots/buttons into `CharacterCommand`. | `MxFramework.CharacterControl` remains noEngine and does not reference `MxFramework.Input`. |
| Runtime AI Planner | `IAiPlanner`, sensors, world state facts | Runtime AI Planner adapters can emit the same `CharacterCommand` as local input or replay. | The planner does not execute movement/action directly. |
| Gameplay pressure / attributes / abilities | `GameplayComponentWorld`, `GameplayEntityId`, pressure components, `GameplayRuntimeCommandFactory` | Character Control references stable Gameplay ids and enqueues ability commands. Pressure break is an explicit state-machine input. | It does not write HP, guard, posture, armor, buffs, modifiers, cooldowns, or costs directly. |
| Combat motion | `CombatKinematicMotor`, `CombatMotionState`, `CombatMotionInput`, `CombatPhysicsWorld` | `CharacterMotionResolver` converts control state + command into `CombatMotionInput` and calls the existing motor. | Unity `CharacterController`, `Rigidbody`, and `UnityEngine.Physics` are never authority. |
| Combat action | `CombatActionRunner`, `CombatActionTimeline`, action lifecycle events | `CharacterActionController` starts/cancels Combat actions and mirrors lifecycle into Character Control events. | It does not rewrite action timing, phase, cancel window, hit, damage, or weapon trace logic. |
| MxAnimation presentation | `MxAnimation` and Combat animation bridge events | Character Control emits presentation-friendly state/action events. | MxAnimation remains presentation-only and does not feed root motion authority back to Character Control. |
| Diagnostics | `IFrameworkDebugSource` style snapshots | Character Control exposes readonly result/event/snapshot DTOs. | No global diagnostics registry is introduced. |
| UI Toolkit | `MxFramework.UI.Toolkit` controls | Runtime HUDs can observe Character Control events and snapshots. | UI callbacks enqueue commands or requests; they do not mutate authoritative state. |

## Assembly And Dependencies

Target assembly:

```text
MxFramework.CharacterControl
  -> MxFramework.Core
  -> MxFramework.Runtime
  -> MxFramework.Combat
  -> MxFramework.Gameplay
```

`MxFramework.CharacterControl` is noEngine and must not reference UnityEngine, UnityEditor, Input, UI Toolkit, MxAnimation.Unity, Demo, Editor, WGame, or project-private packages.

Input, Runtime AI Planner, UI Toolkit, MxAnimation, Audio, and Unity presentation use adapters at the composition-root edge:

```text
Input / Runtime AI Planner / Replay
  -> ICharacterCommandSource
  -> CharacterCommand
  -> CharacterControlStateMachine
  -> CharacterMotionResolver -> CombatKinematicMotor
  -> CharacterActionController -> CombatActionRunner / RuntimeCommandBuffer
  -> diagnostics / presentation events
```

## Core Data Contracts

### CharacterControlEntityRef

`CharacterControlEntityRef` maps one controlled actor across framework domains:

- stable local character id for ordering and diagnostics;
- optional `GameplayEntityId` for component gameplay state;
- optional `CombatEntityId` and `CombatBodyId` for action and motion authority.

It is adapter state, not source-of-truth lifecycle state. Gameplay owns generation entity lifecycle; Combat owns action/motion/physics state.

### CharacterCommand

`CharacterCommand` is the single command DTO shared by local input, Runtime AI Planner adapters, replay/test sources, and UI action buttons.

Required fields:

- `RuntimeFrame Frame`;
- `int SourceId`;
- `CharacterControlEntityRef Entity`;
- quantized local move vector;
- quantized facing/camera basis;
- jump and sprint buttons;
- action button bitmask;
- optional `CharacterActionRequest`;
- non-negative move speed scale for buff/modifier/traction bridge input;
- stable `TraceId`.

The command source is responsible for converting Unity device floats or camera yaw into quantized fixed-point data before creating the command. Character Control may normalize/clamp fixed-point vectors, but it does not call Unity camera, transform, input, or trigonometry APIs.

### ICharacterCommandSource

`ICharacterCommandSource` lets input, Runtime AI Planner, replay, tests, or scripted demos provide the same command shape:

```text
TryGetCommand(frame, entity, out command)
```

It does not own command buffer drain. A composition root decides whether commands are sampled, recorded, replayed, or converted into runtime commands.

## Control State Machine

First version states:

| State | Meaning | Typical locks |
| --- | --- | --- |
| `Locomotion` | Default movement and action-eligible state. | none |
| `Action` | A Combat action or Gameplay ability request is active/accepted. | action/jump or request-specific locks |
| `Reaction` | Hit-stun, pressure break, guard break, stagger, or forced reaction. | move, jump, action |
| `Disabled` | Death, cutscene, possession loss, or global control shutdown. | all |

Allowed transitions:

| From | To | Inputs |
| --- | --- | --- |
| Locomotion | Action | action start / ability accepted |
| Locomotion | Reaction | pressure break / hit reaction |
| Locomotion | Disabled | death / cutscene / manual disable |
| Action | Locomotion | action finished / canceled |
| Action | Reaction | pressure break / hit reaction |
| Action | Disabled | death / cutscene / manual disable |
| Reaction | Locomotion | reaction finished |
| Reaction | Disabled | death / cutscene / manual disable |
| Disabled | Locomotion | explicit restore only |

Rejected transitions return a structured result and emit a diagnostic event. Re-entering the current state with the same lock mask does not increment the state version or produce a meaningless state-changed event.

## Motion Resolver

`CharacterMotionResolver` is a thin authority-preserving bridge:

```text
CharacterCommand + CharacterControlStateMachine + CharacterMotionSettings
  -> CombatMotionInput
  -> CombatKinematicMotor.Step(...)
  -> CharacterMotionResult
```

Rules:

- Locomotion can move and jump unless locked.
- Action may move at `ActionMoveSpeedScale` unless movement is locked.
- Reaction and Disabled default to zero movement and no jump.
- Speed scale is multiplied from command scale, sprint scale, state scale, and traction/modifier scale, then passed through `CombatMotionInput.MoveSpeedScale`.
- The resolver can sync the result to `CombatPhysicsWorld` through `CombatKinematicMotor.Step(world, bodyId, ...)`.
- Result DTO exposes position, velocity, grounded, collision flags, applied delta, desired delta, jump result, and world sync summary.

Explicit non-authority:

- Unity `CharacterController.Move`;
- Unity `Rigidbody`;
- Unity `Collider` trigger/collision callbacks;
- render delta integration;
- current Playables/Animator root motion.

If action displacement is needed later, it must become explicit fixed input or a Combat-owned deterministic root-motion reference. Presentation root motion cannot be authority.

## Action Controller

`CharacterActionController` accepts `CharacterActionRequest` and bridges it to existing systems:

- Combat action request -> `CombatActionRunner.StartAction`, `ForceStartAction`, or `ForceCancel`.
- Gameplay ability request -> `RuntimeCommandBuffer.Enqueue(GameplayRuntimeCommandFactory.CastComponentAbility...)`.
- Explicit target request -> `GameplayComponentAbilityRequestStore` + `CastComponentAbilityRequest`.

It checks:

- current control state;
- `CharacterControlLockMask.Action`;
- duplicate same-frame action requests;
- optional cooldown/resource/status constraints via an injected action constraint interface.

It emits:

- action accepted;
- action rejected with stable reason;
- action queued;
- action started;
- Gameplay command enqueued;
- action finished;
- action canceled.

It does not:

- rewrite Combat action timing, phase, cancel windows, hit windows, or weapon trace;
- directly subtract HP or write posture/guard/armor state;
- directly mutate Gameplay components for costs, cooldowns, or status;
- implement concrete WGame skills or configuration.

## Diagnostics And Presentation

Character Control diagnostics are readonly:

- current control state;
- version;
- last transition reason;
- current lock mask;
- last command frame;
- recent action result/event;
- last motion result summary.

Presentation adapters may subscribe to state/action events and route them to UI Toolkit, MxAnimation, Audio, VFX, camera, or debug overlays. Those adapters consume events; they do not feed presentation state back into authority.

## Implementation Order

Recommended order for the first implementation chain:

1. `#190` Design contract and interface docs.
2. Minimal command/entity/action DTO dependency from `#191`, because `#193` and `#194` require it.
3. `#192` Control state machine and transition events.
4. `#193` Motion resolver over `CombatKinematicMotor`.
5. `#194` Action controller bridge to `CombatActionRunner` and Gameplay runtime commands.
6. `#195` Local Input adapter to `ICharacterCommandSource`.
7. `#196` Runtime AI Planner adapter to `ICharacterCommandSource`.
8. `#201` Motion modifier / traction adapter contract.
9. `#197` Pressure / reaction integration.
10. `#198` MxAnimation presentation adapter for locomotion blend, reaction requests, backend result diagnostics, and Combat bridge ownership policy.
11. Follow-up: Unity composition root / UI Toolkit runtime showcase.

## Acceptance For This Contract

- `CharacterCommand`, state machine, motion resolver, action controller, command source, presentation, and diagnostics boundaries are documented.
- Unity `CharacterController`, `Rigidbody`, and current Playables / Animator root motion are explicitly non-authoritative.
- Implementation order and blocking dependency on the missing command/action DTO foundation are recorded.
- Implementation slices #192-#194 and #198 add noEngine runtime code and EditMode tests only; no scene, prefab, ScriptableObject, or hand-written YAML is introduced.
