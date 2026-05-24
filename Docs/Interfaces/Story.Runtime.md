# Story.Runtime 接口

> 状态：S1 Runtime Slice 已实现（2026-05-24）。本文记录 `MxFramework.Story.Runtime` 当前可用的 Runtime bridge API 和仍未实现的后续范围。

## 职责

`MxFramework.Story.Runtime` connects Story core to `MxFramework.Runtime`:

- RuntimeHost module scheduling.
- Story command ids, factories, registry definitions, and validation.
- A single-drain Story command buffer.
- `RuntimeEventQueue<StoryRuntimeEvent>`.
- Runtime replay frame diagnostics through Story result hash and event summaries.
- Runtime hash contribution.
- Runtime SaveState provider/restorer using current Runtime contracts.

It depends on `MxFramework.Story` and `MxFramework.Runtime`.

```text
MxFramework.Story.Runtime
  -> MxFramework.Story
  -> MxFramework.Runtime
```

## Command Buffer Ownership

Story Runtime owns one Story command buffer. Gameplay owns a separate Gameplay command buffer.

```text
Story input adapters
  -> StoryCommandBuffer.Enqueue(...)
  -> StoryRuntimeModule.DrainForFrame(...)
  -> StoryDirector
  -> RuntimeEventQueue<StoryRuntimeEvent>
  -> optional Story.GameplayBridge enqueue into GameplayCommandBuffer
  -> GameplayRuntimeModule.DrainForFrame(...)
```

Rules:

- `StoryRuntimeModule` is the only drain owner for the Story command buffer.
- `GameplayRuntimeModule` is the only drain owner for the Gameplay command buffer.
- Story-to-Gameplay bridges may enqueue Gameplay commands; they never drain Gameplay commands.
- Same-frame Story-to-Gameplay effects require `StoryRuntimeModule` to run before `GameplayRuntimeModule` in `RuntimeTickStage.Simulation`.

## Command IDs

Accepted Story command id range: `1003000-1003999`.

| Command | Id | Payload0 | Payload1 | Payload2 | TargetId |
| --- | ---: | --- | --- | --- | --- |
| `Story.RaiseTrigger` | `1003001` | `triggerId` | `param0` | `param1` | optional stable target id |
| `Story.SelectChoice` | `1003002` | `beatInstanceId` | `choiceId` | `0` | `graphId` or `0` |
| `Story.CompletePresentation` | `1003003` | `beatInstanceId` | `stepId` | `0` | `graphId` or `0` |
| `Story.RequestEnterBeat` | `1003004` | `graphId` | `beatId` | `0` | `0` |
| `Story.AbortGraph` | `1003005` | `graphId` | `reason` | `0` | `0` |

`StoryRuntimeCommandFactory` should create all five commands so adapters do not hand-write command id or payload order.

## Validation

`StoryRuntimeCommandRegistry` registers all Story command definitions. `StoryRuntimeCommandValidator` composes registry validation with Story-specific payload checks.

Required validation:

- unknown command id returns `UnregisteredCommandId`.
- negative command id returns existing Runtime `InvalidCommandId`.
- required ids must be positive.
- `SelectChoice` requires a live beat instance and a known choice id.
- `CompletePresentation` requires the live beat instance to be waiting for the matching step id.
- `RequestEnterBeat` and `AbortGraph` require `SourceId` to be in configured debug/system source whitelists.
- validator must not mutate Director state.

## Runtime Module

```csharp
public sealed class StoryRuntimeModule : RuntimeModule
{
    public const string DefaultModuleId = "mxframework.story.runtime";
}
```

Responsibilities:

- Drain Story commands for the current Runtime frame.
- Apply validated command intents to `StoryDirector`.
- Tick Director with explicit Runtime frame/delta context.
- Enqueue `StoryRuntimeEvent` values into `RuntimeEventQueue<StoryRuntimeEvent>`.
- Optionally call bridge policies after Director output, before Gameplay module runs.
- Expose diagnostics and snapshots without mutating state.

Default scheduling:

- Stage: `RuntimeTickStage.Simulation`
- Priority: `StoryRuntimeModule.DefaultPriority = -100`, so composition roots can run Story before Gameplay when a later bridge needs same-frame Story-to-Gameplay command enqueue.

S1 has no Gameplay bridge and does not enqueue Gameplay commands; the priority is reserved for that later composition policy.

## Runtime Events

Use existing Runtime queue:

```csharp
public RuntimeEventQueue<StoryRuntimeEvent> Events { get; }
public IReadOnlyList<StoryRuntimeEvent> RecentEvents { get; }
```

Do not introduce a second authoritative event queue implementation. `RecentEvents` is a bounded diagnostics mirror of emitted runtime events for Editor / Debug UI inspection; it does not affect Replay, SaveState, runtime hash, or event delivery.

Event payload:

```csharp
public readonly struct StoryRuntimeEvent
{
    public readonly RuntimeFrame Frame;
    public readonly StoryEventKind Kind;
    public readonly int GraphId;
    public readonly int BeatId;
    public readonly int BeatInstanceId;
    public readonly int StepId;
    public readonly int ChoiceSetId;
    public readonly int AuxId;
}
```

Rules:

- id-only payload.
- no arrays.
- no localized strings.
- no Unity object references.
- no object references to Gameplay entities or component stores.
- authoritative event queue does not merge fact changes; UI may merge after drain.

## Runtime Hash

`StoryRuntimeHashContributor` implements `IRuntimeHashContributor`.

Accepted `ContributorId`:

```text
mxframework.story.runtime
```

Hash input order:

1. Story runtime schema version.
2. loaded graph ids sorted ascending.
3. active graph states sorted by graph id.
4. active beat instances sorted by `BeatInstanceId`.
5. step cursor and pending presentation wait data.
6. blackboard fact entries sorted by `(Namespace, Id)`.
7. deterministic string table entries sorted by string id, only if `StringRef` is enabled.

Hash must not include:

- localized display text.
- event handler counts.
- object references.
- Dictionary iteration order.
- Unity instance ids.
- wall-clock time.
- diagnostics strings unless explicitly stable and documented.

## SaveState

Story Runtime implements current Runtime SaveState contracts:

```csharp
public sealed class StoryRuntimeSaveStateProvider :
    IRuntimeSaveStateProvider,
    IRuntimeSaveStateRestorer
{
}
```

It does not implement `IRuntimeSaveStateContributor`; that interface does not exist in current Runtime.

Captured Story state is written as a `RuntimeModuleSaveState`:

| Field | Value |
| --- | --- |
| `ModuleId` | `mxframework.story.runtime` |
| `SchemaVersion` | `1` |
| `CustomState.TypeId` | `mxframework.story.runtime.state` |
| `CustomState.SchemaVersion` | `1` |
| `CustomState.PayloadJson` | Story runtime state JSON |

Payload v1 includes:

- schema version.
- next beat instance id.
- loaded graph ids and versions.
- active graph states.
- active beat instances.
- current step cursor.
- pending presentation waits.
- ordered blackboard facts.
- deterministic string table snapshot if used.

S1 stores the payload as serialized `StoryDirectorSaveState`. It captures loaded graph definitions and versions, graph status, active beat instances, current step cursor, pending presentation wait, next beat instance id, and ordered blackboard facts.

Restore rules:

- Reject missing module state with structured `RuntimeSaveStateError`.
- Reject unsupported schema versions.
- Restore blackboard before active beat cursors that read facts.
- Restore next beat instance counter to avoid id reuse.
- Rebuild transient caches and ordered fact arrays after payload load.
- Do not restore event subscriptions, bridge references, Unity objects, or Gameplay object references.

## Replay

Story Replay v0 uses existing Runtime replay mechanics:

- replay records Story commands as part of frame commands.
- playback reconstructs a Story runtime world and feeds frame records.
- result hash includes Story hash contributor.
- diagnostics summary may include Story active graph/beat counts and recent event counts.

Story Runtime does not define a separate replay file format in S1.

## Presentation Wait

Story Runtime interprets presentation wait policies:

- `NoWait`: no command required.
- `WaitForCommand`: blocks the matching beat instance until `CompletePresentation`.
- `WaitWithFrameTimeout`: blocks until a configured Runtime frame deadline, then follows timeout behavior.

S1 may implement only `NoWait` and `WaitForCommand`, but the DTO must keep policy fields so later timeout support is non-breaking.

## Current Unsupported

- JSON replay export/playback beyond existing Runtime replay snapshot.
- Network rollback or replication.
- Gameplay effect commands; defined in `Story.GameplayBridge`.
- Unity trigger zone adapters; defined in `Story.Unity`.
- Runtime timeout behavior for `WaitWithFrameTimeout`.
- Config mapping, Resources preload, Runtime AI Planner projection, Authoring import, and Editor tooling.

## Test Entry

S1 tests:

- command registry and validator.
- single-drain command buffer ownership.
- Director command intake and event output.
- replay hash consistency.
- SaveState JSON roundtrip and restored hash.
- stale beat instance / unknown choice / debug-source rejection.

See `Docs/Tasks/STORY_S1.md`.
