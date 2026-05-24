# ADR-004: Story Module Scope

Date: 2026-05-24

Status: Proposed

## Context

MxFramework currently has Runtime, Gameplay, Config, Resources, Events, Attributes, Buffs, Modifiers, Runtime AI Planner, Debug UI, and Unity adapter patterns, but no framework-level Story module.

The intended Story module must support reusable narrative runtime structure without importing WGame-specific world, character, place, quest, dialogue, or buff content. It also must not become a second Gameplay, trigger, save, replay, UI, camera, audio, or authoring system.

The main design pressure is that story graphs naturally touch many systems:

- Runtime needs command, frame, event queue, replay hash, and SaveState integration.
- Gameplay needs beat/entity lookup, attribute conditions, buff/ability effects, and component entity references.
- Config needs graph, beat, step, branch, choice, and localized text references.
- Resources needs cutscene, audio, animation, and UI asset preload plans.
- Unity needs trigger zones, Timeline, Cinemachine, UI Toolkit, and scene entry points.
- Runtime AI Planner may consume story facts, but it must not own story state.

Existing modules already define strong source-of-truth rules:

- `MxFramework.Runtime` owns host scheduling, command buffers, replay frame records, hash contracts, and SaveState provider/restorer orchestration.
- `MxFramework.Gameplay` owns runtime gameplay entity/component state, command systems, typed gameplay events, hash, and SaveState.
- `MxFramework.Modifiers` conditions and effects use `ModifierContext`, so they cannot be directly reused as Story conditions/effects without an adapter.
- `MxFramework.AI.AiWorldState` stores object-backed planner facts and is suitable for simulation, not Story authority, deterministic hash, or SaveState.

## Decision

Add Story as a new framework capability using a narrow core plus explicit bridge assemblies.

`MxFramework.Story` core is a noEngine module that depends only on Core and Events. It owns Story DTOs, a deterministic blackboard, story graph/beat/step/branch/choice contracts, a pure Director state machine, and synchronous id-only Story events. It does not read Runtime commands, Resources, Attributes, Buffs, Modifiers, Gameplay, Unity, Config, or Runtime AI Planner.

Runtime integration is a separate `MxFramework.Story.Runtime` bridge. It owns `StoryRuntimeModule`, Story command ids/factories/validators, a `RuntimeEventQueue<StoryRuntimeEvent>`, `IRuntimeHashContributor`, and `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer` implementations.

Config integration is a sibling bridge, not a layer above Runtime:

```text
MxFramework.Story
  <- MxFramework.Story.Runtime
  <- MxFramework.Story.Config
  <- MxFramework.Story.GameplayBridge
  <- MxFramework.Story.ResourcesBridge
  <- MxFramework.Story.RuntimeAiPlannerBridge
```

Unity and Editor remain outer adapters:

```text
Story.Unity / Story.Editor
  -> Story.Runtime
  -> Story.GameplayBridge / Story.ResourcesBridge as needed
```

Story blackboard authority uses `StoryFactKey` plus restricted `StoryValue`, not `AiWorldState`. The Story Runtime AI Planner bridge projects whitelisted story facts into `IAiWorldState` one-way: Story to Runtime AI Planner.

Story effects that affect Gameplay are expressed as explicit intent and translated by `Story.GameplayBridge` into Gameplay-owned `RuntimeCommand` values. Story modules must not call `IBuffPipeline.AddBuff`, `IAttributeOwner.SetAttribute`, `GameplayComponentStore<T>.Set`, or other Gameplay mutation APIs directly.

## Source Of Truth Rules

| Concern | Owner |
| --- | --- |
| Story graph definition and deterministic beat state | `MxFramework.Story` / `MxFramework.Story.Runtime` |
| Story blackboard facts | `StoryBlackboard` |
| Runtime command intake, frame ordering, replay record, Story hash, Story SaveState | `MxFramework.Story.Runtime` |
| Gameplay attributes, buffs, modifiers, abilities, component entities | `MxFramework.Gameplay` |
| Story-to-Gameplay effect translation | `MxFramework.Story.GameplayBridge` |
| Resources preload planning | `MxFramework.Story.ResourcesBridge` |
| Runtime AI Planner facts | `MxFramework.Story.RuntimeAiPlannerBridge`, one-way projection |
| UI Toolkit dialogue view, camera, audio, Timeline, Cinemachine | Unity / presentation adapters subscribing to Story runtime events |
| Authoring import from Yarn, Ink, CSV, Markdown, or external tools | `Tools/MxFrameworkStoryAuthoring` or Unity Editor adapters |

## Bridge Rules

- Pure Story conditions implement `IStoryCondition` and read only `StoryEvaluationContext`.
- `StoryModifierConditionAdapter` may adapt `IModifierCondition` by constructing a temporary `ModifierContext`, but only in `Story.GameplayBridge`.
- Story effect adapters do not execute `IModifierEffect` directly. They emit Gameplay effect intents that are translated into Gameplay commands or rejected with diagnostics if no Gameplay command exists.
- `StoryBeatGameplayLocator` stores stable ids or handles, not direct component store references.
- Unity trigger zones are view/input adapters. They enqueue Story commands; they do not call Director mutation methods.

## Consequences

Benefits:

- Story remains a reusable framework runtime instead of a WGame quest/dialogue implementation.
- Runtime authority stays command/replay/save/hash compatible.
- Gameplay state has one owner and no direct Story mutation path.
- Story facts are deterministic, stable, and hash/save friendly.
- Runtime AI Planner can consume Story signals without becoming Story's blackboard.
- UI, camera, audio, and Timeline remain presentation subscribers.

Costs:

- The first implementation needs several small bridge assemblies instead of one large Story assembly.
- Gameplay effects cannot be promised until the corresponding Gameplay commands exist.
- Adapter boundaries require more explicit tests than direct method calls.
- Authoring import is intentionally deferred until runtime and config contracts are stable.

## Alternatives Considered

- Option: Put Story, Runtime, Config, Gameplay, Resources, and Unity adapters in one assembly.
- Reason not chosen: It would create dependency cycles and make Story core impossible to test outside Unity.

- Option: Use `AiWorldState` as Story blackboard.
- Reason not chosen: `AiWorldState` uses object values and clone semantics for planner simulation, not deterministic authority state.

- Option: Reuse `IModifierCondition` and `IModifierEffect` directly.
- Reason not chosen: Their contract is `ModifierContext`-based and can mutate Attributes/Buffs directly; Story needs adapter boundaries and Gameplay-owned commands.

- Option: Let UI/Timeline call Director directly.
- Reason not chosen: It would bypass RuntimeCommand replay and make presentation completion non-deterministic.

## References

- Related docs:
  - `Docs/Interfaces/Story.md`
  - `Docs/Interfaces/Story.Runtime.md`
  - `Docs/Interfaces/Story.GameplayBridge.md`
  - `Docs/Tasks/STORY_S1.md`
  - `Docs/Interfaces/Runtime.md`
  - `Docs/Interfaces/Gameplay.md`
  - `Docs/Interfaces/AI.md`
