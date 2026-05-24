# STORY_S1

## 目标

实现 Story 第一条纯 C# runtime slice：`MxFramework.Story` + `MxFramework.Story.Runtime`。

本任务只做 core runtime、RuntimeHost 接入、command boundary、event queue、hash、SaveState、replay/hash tests。不接 Unity 场景、不接 Gameplay bridge、不接 Resources、不接 Editor、不接外部 authoring。

## 工作流定级

- 任务等级：`S1`
- 任务类型：`Story runtime / public API / docs / tests`
- 建议分支：`feature/<issue>-story-s1-runtime`
- 建议标签：`type/implementation`、`module/story`、`status/agent-ready`

## Context Pack

Agent 开工前读取：

1. `AGENTS.md`
2. `Docs/PROJECT_INDEX.md`
3. `Docs/README.md`
4. `Docs/WORKFLOW.md`
5. `Docs/QUALITY_GATE.md`
6. `Docs/AGENT_GAME_CREATION_GUIDE.md`
7. `Docs/Decisions/ADR-004-story-module-scope.md`
8. `Docs/Decisions/ADR-005-story-runtime-command-boundary.md`
9. `Docs/Interfaces/Story.md`
10. `Docs/Interfaces/Story.Runtime.md`
11. `Docs/Interfaces/Runtime.md`
12. 当前任务文档

## API 复用计划

| 需求点 | 优先使用的框架 API / 模块 | 本次是否使用 | 不使用时的原因 |
| --- | --- | --- | --- |
| 主循环 / 固定帧入口 | `RuntimeHost`、`RuntimeModule`、`RuntimeTickContext` | 是 | - |
| 外部剧情意图输入 | `RuntimeCommand`、`RuntimeCommandBuffer`、`RuntimeCommandRegistry`、`IRuntimeCommandValidator` | 是 | - |
| 回放 / hash | `RuntimeReplayRecorder`、`IRuntimeHashContributor`、`RuntimeHashAccumulator` | 是 | - |
| 存档 / 恢复 | `IRuntimeSaveStateProvider`、`IRuntimeSaveStateRestorer`、`RuntimeModuleSaveState`、`RuntimeCustomState`、`RuntimeSaveStateJson` | 是 | - |
| 事件流 | `RuntimeEventQueue<StoryRuntimeEvent>`、core `IEventBus<StoryEvent>` | 是 | - |
| 运行时状态 | Story-owned `StoryDirector`、`StoryBlackboard` | 是 | Runtime 无剧情 graph/beat state，需新增模块状态 |
| Gameplay 属性 / Buff / Modifier | `Gameplay` / `Attributes` / `Buffs` / `Modifiers` | 否 | S1 只做 Story runtime；桥接在 S3 |
| Resources 预加载 | `ResourcePreloadPlan` / `ResourcePreloadService` | 否 | S1 无资源 step；桥接在 S3 |
| UI Toolkit | `MxFramework.UI.Toolkit` | 否 | S1 无 Unity/UI playable；后续 Demo 接入 |
| Runtime AI Planner | `IAiWorldState` projection bridge | 否 | S1 只固定 Story authority blackboard；投影桥后续实现 |
| Diagnostics / Debug UI | `IFrameworkDebugSource` | 可选 | S1 可先提供 Story snapshot，Debug UI adapter 后续实现 |

## 允许修改

- `Assets/Scripts/MxFramework/Story/`
- `Assets/Scripts/MxFramework/Story.Runtime/`
- `Assets/Scripts/MxFramework/Tests/Story/`
- `Docs/Interfaces/Story.md`
- `Docs/Interfaces/Story.Runtime.md`
- `Docs/CAPABILITIES.md`（实现验收后同步）
- `Docs/USAGE.md`（实现验收后同步最小示例）
- 当前任务文档

## 不做

- 不创建 Unity 场景、Prefab、ScriptableObject 或 UI 资产。
- 不接 `Story.GameplayBridge`。
- 不把 Story effects 直接改 Gameplay / Attributes / Buffs / Modifiers。
- 不接 `Story.ResourcesBridge`。
- 不接 Runtime AI Planner projection。
- 不实现 GraphView / Story.Editor。
- 不实现 Yarn / Ink / CSV / Markdown 导入。
- 不标记为 Playable；本任务交付等级是 `Framework Feature` 或 `Runtime Slice`。

## S1 Scope

### Story core

新增：

- `StoryFactKey`
- `StoryValue`
- `StoryFactEntry`
- `StoryFactCopyResult`
- `IStoryBlackboard`
- `StoryBlackboard`
- minimal graph DTOs:
  - `StoryGraphDefinition`
  - `StoryBeatDefinition`
  - `StoryStepDefinition`
  - `StoryBranchDefinition`
  - `StoryChoiceDefinition`
- `StoryStepKind`
- `StoryPresentationWaitPolicy`
- `StoryEventKind`
- `StoryEvent`
- `StoryDirector`
- `IStoryChoiceSnapshotReader`
- minimal result structs with stable error codes.

S1 graph support can be minimal:

```text
Entry beat
-> line/no-wait step
-> optional choice set
-> branch to next beat
-> graph completed
```

### Story.Runtime

新增：

- `StoryRuntimeCommandIds`
- `StoryRuntimeCommandFactory`
- `StoryRuntimeCommandRegistry`
- `StoryRuntimeCommandValidator`
- `StoryRuntimeEvent`
- `StoryRuntimeModule`
- `StoryRuntimeHashContributor`
- `StoryRuntimeSaveStateProvider`

Use existing Runtime event queue:

```csharp
RuntimeEventQueue<StoryRuntimeEvent>
```

Do not create a custom queue implementation.

### Command Boundary

Implement the five commands from ADR-005:

- `Story.RaiseTrigger`
- `Story.SelectChoice`
- `Story.CompletePresentation`
- `Story.RequestEnterBeat`
- `Story.AbortGraph`

`RequestEnterBeat` and `AbortGraph` must reject non-whitelisted `SourceId` values.

### SaveState

Use `RuntimeModuleSaveState.CustomState`:

- `ModuleId = "mxframework.story.runtime"`
- `CustomState.TypeId = "mxframework.story.runtime.state"`
- schema version `1`

Roundtrip must restore:

- blackboard facts.
- active beat instances.
- next beat instance id.
- pending presentation wait state.
- loaded graph runtime state needed by S1 tests.

### Hash

Hash must include:

- schema version.
- loaded graph ids.
- active beat instances.
- current step ids.
- pending presentation wait ids.
- ordered blackboard facts.

Hash must remain stable after SaveState JSON roundtrip.

## 验收

- Story asmdefs are noEngine and have no UnityEngine / UnityEditor references.
- Story core asmdef references only Core and Events.
- Story.Runtime references Story and Runtime.
- `StoryBlackboard` rejects invalid keys and copies facts ordered by `(Namespace, Id)`.
- `CopyOrdered` reports buffer shortage without throwing or writing past span.
- Director can load a minimal graph, enter entry beat, emit beat/step events, offer choices, resolve choice by stable `choiceId`, and complete graph.
- `StoryRuntimeModule` drains only its own Story command buffer.
- Command validator rejects unknown command ids, invalid payloads, stale beat instances, unknown choices, and non-whitelisted debug/system commands.
- Runtime event queue emits id-only `StoryRuntimeEvent` values and does not merge same-frame fact changes.
- Replay playback with identical commands produces identical Story hash.
- SaveState JSON roundtrip restores Story state and hash.
- No direct Gameplay / Attributes / Buffs / Modifiers mutation path exists in S1.
- `Docs/Interfaces/Story.md`, `Docs/Interfaces/Story.Runtime.md`, `Docs/CAPABILITIES.md`, and `Docs/USAGE.md` are updated after implementation.

## Suggested Tests

- `StoryBlackboardTests.CopyOrderedSortsByNamespaceThenId`
- `StoryBlackboardTests.CopyOrderedReportsRequiredCountWhenBufferTooSmall`
- `StoryDirectorTests.MinimalGraphCompletes`
- `StoryDirectorTests.ChoiceUsesChoiceIdNotIndex`
- `StoryRuntimeCommandValidatorTests.RejectsDebugCommandFromNonDebugSource`
- `StoryRuntimeModuleTests.DrainsOnlyStoryCommandBuffer`
- `StoryRuntimeModuleTests.EmitsRuntimeEventsWithoutArrayPayload`
- `StoryRuntimeHashTests.HashStableAcrossEquivalentProgression`
- `StoryRuntimeSaveStateTests.JsonRoundtripRestoresHash`
- `StoryRuntimeReplayTests.PlaybackCommandsRecreateHash`

## 后续

- S2: `Story.Config` schema / mapper / validator.
- S3: `Story.GameplayBridge` and `Story.ResourcesBridge`.
- S4: `Story.Unity` adapters and `Story.Editor` debug / graph tools.
- S5: Runtime AI Planner projection bridge and Playable Demo.
- S6: external Story authoring CLI and optional Authoring AI Assist.
