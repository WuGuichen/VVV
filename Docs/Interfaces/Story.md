# Story 接口

> 状态：S0 Proposed Contract。本文固定 Story core 的目标公共契约；S1 实现关闭前，不代表仓库已有可用 API。

## 职责

`MxFramework.Story` 提供框架级剧情运行时核心：Story graph、beat、step、branch、choice、deterministic blackboard、Director 状态机和同步 id-only Story event。

Story core 是 noEngine 模块，只依赖 `MxFramework.Core` 和 `MxFramework.Events`。它不依赖 Runtime、Gameplay、Attributes、Buffs、Modifiers、Config、Resources、Runtime AI Planner、UnityEngine 或 UnityEditor。

## 非目标

- 不内置 WGame 角色、地点、世界观、任务、对白或具体剧情内容。
- 不内置 Yarn / Ink / Articy 等第三方 DSL。
- 不实现 UI Toolkit 对话框、气泡、字幕、镜头、音频或 Timeline 表演。
- 不直接修改 Gameplay 属性、Buff、Modifier、Ability 或 component store。
- 不使用 `AiWorldState` 作为权威黑板。

## 依赖

```text
MxFramework.Story
  -> MxFramework.Core
  -> MxFramework.Events
```

外部能力通过 bridge 模块接入：

- `MxFramework.Story.Runtime`
- `MxFramework.Story.Config`
- `MxFramework.Story.GameplayBridge`
- `MxFramework.Story.ResourcesBridge`
- `MxFramework.Story.RuntimeAiPlannerBridge`
- `MxFramework.Story.Unity`
- `MxFramework.Story.Editor`

## 核心类型

| 类型 | 用途 |
| --- | --- |
| `StoryGraphDefinition` | 静态 graph 定义，包含 graph id、version、entry beat 和稳定排序的 beat 列表 |
| `StoryBeatDefinition` | beat 节点定义，包含 triggers、steps、choices、branches 和 exit effects 的稳定 id 引用 |
| `StoryStepDefinition` | beat 内 step 定义，包含 `StoryStepKind`、文本/说话人/资源等稳定 id 和等待策略 |
| `StoryBranchDefinition` | beat 出边定义，包含 condition id、target beat id、priority 和 fallback 标记 |
| `StoryChoiceDefinition` | 选择定义，包含 choice id、label text key id、condition id、target beat id 和 effect ids |
| `StoryFactKey` | 黑板事实 key，使用 `Namespace` + `Id` 稳定定位 |
| `StoryValue` | 无装箱受限 union，保存 bool/int/long/fix64/string-ref 等确定性值 |
| `StoryFactEntry` | `StoryFactKey` + `StoryValue` 的有序枚举项 |
| `StoryBlackboard` | 默认黑板实现，提供稳定 set/get/copy ordered facts |
| `IStoryCondition` | 纯 Story 条件，只读 Story evaluation context |
| `IStoryEffect` | 纯 Story effect，只能修改 Story blackboard 或返回 Story-local intent |
| `StoryDirector` | Director 状态机，推进 graph/beat/step/branch，不读取 RuntimeCommand |
| `StoryEvent` | core 同步事件，id-only payload |
| `StorySnapshot` | Director 只读快照，用于 tests、Debug UI adapter 和 UI 查询 |
| `IStoryChoiceSnapshotReader` | UI 查询当前可选项的只读接口 |

## Blackboard

Story blackboard 使用稳定 key 和受限 value：

```csharp
public readonly struct StoryFactKey : IEquatable<StoryFactKey>
{
    public readonly int Namespace;
    public readonly int Id;
}

public enum StoryValueKind : byte
{
    None = 0,
    Bool = 1,
    Int32 = 2,
    Int64 = 3,
    Fix64 = 4,
    StringRef = 5
}

public readonly struct StoryValue
{
    public readonly StoryValueKind Kind;
    public readonly long Raw;
}

public readonly struct StoryFactEntry
{
    public readonly StoryFactKey Key;
    public readonly StoryValue Value;
}
```

Rules:

- `Namespace=0` is global Story state; positive namespace values usually map to graph id.
- `Id <= 0` is invalid unless a specific API documents a reserved value.
- `StoryValueKind.Fix64` stores `Fix64.RawValue`.
- `StoryValueKind.StringRef` stores a deterministic integer string id, not a string object.
- Story core must not store `object`, `Dictionary<string, object>`, Unity instance ids, localized text values, or current system time in blackboard authority state.

The blackboard copy contract must be explicit about buffer capacity:

```csharp
public interface IStoryBlackboard
{
    bool TryGet(in StoryFactKey key, out StoryValue value);
    void Set(in StoryFactKey key, in StoryValue value);
    bool Remove(in StoryFactKey key);
    int Count { get; }
    StoryFactCopyResult CopyOrdered(Span<StoryFactEntry> buffer);
}

public readonly struct StoryFactCopyResult
{
    public readonly int RequiredCount;
    public readonly int WrittenCount;
    public readonly bool Complete;
}
```

`CopyOrdered` sorts by `(Namespace, Id)` ascending. If `buffer.Length < Count`, it writes the first `buffer.Length` entries in sort order, returns `Complete=false`, and reports `RequiredCount=Count`. Hash and SaveState callers must treat incomplete copy as an error and retry with a large enough buffer or use a cached ordered store.

Implementation guidance:

- S1 may use a dictionary plus dirty cached ordered array for low-frequency changes.
- Per-frame hash should not sort and allocate every tick.
- Dynamic string insertion must be deterministic. Prefer config-stable text key ids over runtime string pool writes.

## Director

`StoryDirector` is the authority for Story graph progression:

```csharp
public interface IStoryDirector
{
    bool LoadGraph(in StoryGraphDefinition graph);
    StoryEnterBeatResult TryEnterBeat(int graphId, int beatId, in StoryActivationContext context);
    StoryTickResult Tick(in StoryTickContext context);
    StoryChoiceResult TryResolveChoice(int beatInstanceId, int choiceId);
    StoryPresentationResult CompletePresentation(int beatInstanceId, int stepId);
    IStoryBlackboard Blackboard { get; }
    IEventBus<StoryEvent> Events { get; }
}
```

Director does not know `RuntimeCommand`. `Story.Runtime` translates commands into these method calls.

Director may internally update:

- active graph state.
- active beat instances.
- step cursors and pending presentation wait state.
- branch and choice resolution.
- Story blackboard facts.

Director must expose read-only snapshots for UI and diagnostics; external code must not mutate internal collections.

## Beat Instance Id

`BeatInstanceId` is a stable signed 32-bit id unique within a Director session until SaveState restore. It distinguishes repeated activations of the same beat.

Rules:

- `0` is invalid.
- Restored sessions must continue from the restored next-instance counter.
- Events and UI choice commands use `BeatInstanceId`, not array index.
- Config may pack graph/beat ids separately, but `BeatInstanceId` remains runtime instance identity.

## Events

Story core synchronous events use id-only payload:

```csharp
public enum StoryEventKind : byte
{
    GraphLoaded = 1,
    GraphCompleted = 2,
    GraphAborted = 3,
    BeatEntered = 10,
    BeatExited = 11,
    StepStarted = 20,
    StepCompleted = 21,
    ChoiceOffered = 30,
    ChoiceResolved = 31,
    FactChanged = 40
}

public readonly struct StoryEvent
{
    public readonly StoryEventKind Kind;
    public readonly int GraphId;
    public readonly int BeatId;
    public readonly int BeatInstanceId;
    public readonly int StepId;
    public readonly int ChoiceSetId;
    public readonly int AuxId;
}
```

No event payload may contain arrays, object references, localized text strings, Unity objects, or mutable collections.

## Choice Snapshot

UI resolves choice display by snapshot query:

```csharp
public interface IStoryChoiceSnapshotReader
{
    int GetChoices(int beatInstanceId, int choiceSetId, Span<StoryChoiceView> buffer);
}

public readonly struct StoryChoiceView
{
    public readonly int ChoiceId;
    public readonly int LabelTextKey;
    public readonly bool Enabled;
}
```

If the buffer is too small, `GetChoices` returns the required count without writing beyond the provided span. UI then retries or displays the first copied entries based on adapter policy.

## Presentation Wait

Step definitions include a presentation completion policy:

| Policy | Meaning |
| --- | --- |
| `NoWait` | Director advances immediately. |
| `WaitForCommand` | Director waits for matching beat instance and step id. |
| `WaitWithFrameTimeout` | Director waits until a Runtime-frame timeout configured by the Runtime bridge. |

Core stores the policy and pending wait state. Runtime owns frame interpretation and command validation.

## Hash / SaveState Inputs

Story core must provide deterministic snapshot data for Runtime:

- graph id and state.
- active beat instances ordered by activation order or instance id, as documented by `Story.Runtime`.
- step cursor and pending presentation waits.
- ordered blackboard facts from `CopyOrdered`.
- deterministic string table snapshot if `StringRef` is used.

It must not serialize private object trees, delegates, event handlers, Unity objects, or live bridge references.

## Current Unsupported

- Runtime command integration. Planned in `Story.Runtime`.
- Config loading / schema mapping. Planned in `Story.Config`.
- Gameplay effect execution. Planned in `Story.GameplayBridge`.
- Resources preload. Planned in `Story.ResourcesBridge`.
- Runtime AI Planner projection. Planned in `Story.RuntimeAiPlannerBridge`.
- Unity trigger zones, Timeline, Cinemachine, UI Toolkit view, GraphView editor, or external authoring import.

## Test Entry

Planned S1 tests:

- `Assets/Scripts/MxFramework/Tests/Story/StoryBlackboardTests.cs`
- `Assets/Scripts/MxFramework/Tests/Story/StoryDirectorTests.cs`
- `Assets/Scripts/MxFramework/Tests/Story/StoryRuntimeModuleTests.cs`

See `Docs/Tasks/STORY_S1.md`.
