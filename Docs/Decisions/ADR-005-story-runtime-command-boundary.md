# ADR-005: Story Runtime Command Boundary

Date: 2026-05-24

Status: Accepted (2026-05-24)

## Context

`RuntimeCommand` currently contains a `RuntimeFrame`, `SourceId`, `CommandId`, `TargetId`, three integer payload slots, `TraceId`, and a sequence assigned by `RuntimeCommandBuffer`.

`RuntimeCommandBuffer` has one drain owner. A module that drains a buffer advances that buffer's current frame, so Story and Gameplay must not drain the same command buffer.

Story needs player input, trigger zones, UI choices, presentation completion, debug entry, and graph abort requests to be replayable. At the same time, Story Director's internal deterministic step advancement, branch evaluation, and blackboard writes should not flood replay with internal commands.

## Decision

Only external intent and non-deterministic presentation acknowledgements enter Story through `RuntimeCommand`.

The initial Story command set is:

| Command | CommandId | Payload0 | Payload1 | Payload2 | TargetId | Allowed source |
| --- | ---: | --- | --- | --- | --- | --- |
| `Story.RaiseTrigger` | `1003001` | `triggerId` | `param0` | `param1` | optional stable target id | Input, Gameplay bridge, Unity adapter, test driver |
| `Story.SelectChoice` | `1003002` | `beatInstanceId` | `choiceId` | `0` | `graphId` or `0` | UI adapter, test driver |
| `Story.CompletePresentation` | `1003003` | `beatInstanceId` | `stepId` | `0` | `graphId` or `0` | UI, Timeline, Audio, Camera adapter, test driver |
| `Story.RequestEnterBeat` | `1003004` | `graphId` | `beatId` | `0` | `0` | Debug-only source ids |
| `Story.AbortGraph` | `1003005` | `graphId` | `reason` | `0` | `0` | System/debug source ids |

Command ids use the `1003000-1003999` proposed Story range. S1 must register these with `RuntimeCommandRegistry` and a Story-specific `IRuntimeCommandValidator`.

`StoryRuntimeModule` owns and drains a Story command buffer. `GameplayRuntimeModule` owns and drains a separate Gameplay command buffer. `Story.GameplayBridge` may enqueue Gameplay commands into the Gameplay command buffer, but it must not drain that buffer.

When Story effects must affect Gameplay in the same Runtime frame, the composition root must order modules so Story runs before Gameplay in `RuntimeTickStage.Simulation`. If the bridge enqueues Gameplay commands for a later frame, that delay must be explicit in the effect intent or bridge policy.

## Command Validation

Story command validation must check:

- command id is registered in the Story command registry.
- payload ids are positive where required.
- `Story.SelectChoice` uses a live `beatInstanceId` and a known `choiceId`.
- `Story.CompletePresentation` uses a live `beatInstanceId` and the currently waiting `stepId`.
- `Story.RequestEnterBeat` and `Story.AbortGraph` require whitelisted `SourceId` values. `RuntimeCommand` has no debug/source type field, so source policy must be defined by id.
- `TargetId` use must be stable and documented per command. Story v0 may leave it `0` except for trigger-origin diagnostics.

Invalid commands return `RuntimeCommandValidationResult.Failed(...)`; they must not partially mutate Director state.

## Internal Story Progression

The Director may update these states without creating new `RuntimeCommand` values:

- active graph and beat instance stacks.
- current step cursor and pending presentation wait.
- branch evaluation results.
- blackboard changes caused by deterministic Story effects.
- graph completion and beat exit transitions.

These internal changes are exposed through:

- `RuntimeEventQueue<StoryRuntimeEvent>` for UI, audio, camera, Timeline, diagnostics, and replay frame summaries.
- `IRuntimeHashContributor` for active beat state and ordered blackboard facts.
- `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer` through `RuntimeModuleSaveState.CustomState`.

## Event Policy

Story runtime events are id-only structs. They do not carry arrays, object references, Unity objects, localized strings, or mutable collections.

Authoritative Story events are not merged by default. If a fact changes multiple times in one frame, every `FlagChanged` / fact-changed event is emitted in sequence. UI and Debug UI may merge display rows after draining the runtime queue, but the queue remains lossless for replay diagnostics.

## Presentation Completion Policy

Steps that require presentation acknowledgement must declare one of these policies:

| Policy | Behavior |
| --- | --- |
| `NoWait` | Director advances immediately after emitting the event. |
| `WaitForCommand` | Director waits until `Story.CompletePresentation` matches `beatInstanceId` and `stepId`. |
| `WaitWithFrameTimeout` | Director waits for matching completion until a configured Runtime frame deadline, then emits timeout diagnostics and follows the configured timeout behavior. |

The default for S1 is `NoWait` for line/wait/flag steps and `WaitForCommand` only for explicit presentation steps. Timeout support can be implemented after the command boundary exists, but the policy field must be present in the contract to avoid hidden blocking semantics.

## SaveState Boundary

Story Runtime does not implement a non-existent `IRuntimeSaveStateContributor`. It implements current Runtime contracts:

- `IRuntimeSaveStateProvider.CaptureSaveState()`
- `IRuntimeSaveStateRestorer.RestoreSaveState(RuntimeSaveState saveState)`

Captured Story state is stored as one `RuntimeModuleSaveState` with:

- `ModuleId = "mxframework.story.runtime"`
- `SchemaVersion = 1`
- `CustomState.TypeId = "mxframework.story.runtime.state"`
- `CustomState.SchemaVersion = 1`
- `CustomState.PayloadJson = deterministic Story state payload`

The payload contains active beat instances, ordered blackboard facts, deterministic string table entries if used, graph state, pending presentation waits, and schema version.

## Consequences

Benefits:

- Replay records stay compact and contain external intent rather than every internal branch/step transition.
- Command buffer ownership stays compatible with current Runtime and Gameplay modules.
- Story-to-Gameplay effects can be ordered explicitly by module priority.
- Debug and system commands are auditable through `SourceId`.

Costs:

- Story and Gameplay composition roots need two command buffers.
- Immediate Story effects that need Gameplay changes must be implemented as bridge policies and tested for same-frame / next-frame ordering.
- S1 must include validator tests for debug-only commands, stale beat instances, and unknown choices.

## Alternatives Considered

- Option: Put every Story internal change into RuntimeCommand.
- Reason not chosen: It bloats replay, duplicates deterministic state machine output, and makes branch/step implementation harder to evolve.

- Option: Let Story and Gameplay share one command buffer.
- Reason not chosen: `RuntimeCommandBuffer` has one drain owner; sharing would create order bugs and frame advancement conflicts.

- Option: Let Story effects call Gameplay mutation APIs directly.
- Reason not chosen: It bypasses Gameplay command systems, hash, SaveState, event queue, and source-of-truth rules.

## References

- Related docs:
  - `Docs/Interfaces/Story.Runtime.md`
  - `Docs/Interfaces/Runtime.md`
  - `Docs/Interfaces/Gameplay.md`
  - `Docs/Tasks/STORY_S1.md`
