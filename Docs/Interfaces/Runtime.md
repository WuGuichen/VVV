# Runtime 接口

> Runtime Foundation Host Core v0.1 + Frame / Command / Replay Core v0.1 + SaveState Contract v0 + v1 parallel closeout 已实现（2026-05-10）。本页记录 `MxFramework.Runtime` 当前公共接口和边界。

## 职责

Runtime 提供框架级运行时组合根和生命周期调度。它不承载业务玩法，不依赖 Unity，不反向引用 Gameplay、Combat、Resources、Preview 或 Demo。

当前 v0/v0.1 覆盖 Host Core、Frame / Command / Replay Core、Hash Contract、SaveState Orchestration 与小型 runtime utilities：

- 模块注册。
- 生命周期：`Initialize`、`Start`、`Tick`、`Stop`、`Dispose`。
- Tick stage / priority / module id 稳定排序。
- 模块异常收集和错误策略。
- Host diagnostics。
- 最小服务注册表，供外层组合根传入依赖。
- 显式 runtime frame 与 frame clock。
- 通用 command 输入契约、validation result 和 command buffer。
- Replay frame record / recorder / readonly snapshot。
- Replay playback runner。
- Runtime result hash contributor contract。
- SaveState v0 DTO、provider/restorer 契约、错误模型、迁移管线、JSON roundtrip 和多 participant 编排。
- Deterministic random，可 capture / restore。
- Runtime event queue，按帧缓冲并稳定 drain。
- Runtime timer scheduler，支持 frame / seconds / repeating / command timer。
- Cooldown、versioning、operation、rate limit / debounce 和 command registry 运行时质量工具。
- Local state machine、typed context map、presentation interpolation 和 snapshot diff 工具。

## 为什么不依赖 Unity

`MxFramework.Runtime` 是 `noEngineReferences=true` 的纯 C# 程序集，不引用 `UnityEngine` 或 `UnityEditor`。Unity 场景、MonoBehaviour、输入、时间和资源对象都应在外层适配后传给 Host。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `RuntimeHost` | 运行时组合根，负责模块注册、生命周期和 Tick 调度 |
| `IRuntimeModule` | 可被 Host 调度的模块契约 |
| `RuntimeModule` | 空实现基类，便于模块只重写需要的生命周期 |
| `RuntimeLifecycleState` | Host 当前生命周期状态 |
| `RuntimeTickStage` | Tick 分组：PreSimulation / Simulation / PostSimulation / Diagnostics |
| `RuntimeTickContext` | 单次 RuntimeHost Tick 上下文，包含 runtime frame、tick delta、elapsed 和当前 stage |
| `RuntimeHostContext` | 生命周期上下文，暴露 Host 和 service registry |
| `RuntimeServiceRegistry` / `IRuntimeServiceRegistry` | 组合根服务表 |
| `RuntimeHostOptions` | Host 选项，当前包含错误策略和服务表 |
| `RuntimeHostErrorPolicy` | 模块异常策略：FailFast / CollectAndStopFrame / CollectAndContinue |
| `RuntimeHostError` | 单个模块错误记录 |
| `RuntimeHostException` | FailFast 策略下抛出的包装异常 |
| `RuntimeHostDiagnostics` / `RuntimeModuleDiagnostics` | Host 状态、模块列表和错误快照 |
| `RuntimeLogLevel` / `IRuntimeLogger` / `NullRuntimeLogger` | Lightweight noEngine logging contract for composition roots and runtime probes |
| `RuntimeFrame` | 非负 runtime frame 值，支持比较、相等和字符串输出 |
| `RuntimeClock` | 显式 frame clock，支持 `CurrentFrame`、`Step()`、`Reset(frame)` |
| `RuntimeCommand` | 通用输入命令契约，包含 frame、source、command、target、payload、trace 和 sequence |
| `RuntimeCommandBuffer` | 命令缓冲区，支持 `Enqueue()`、`DrainForFrame()`、未来帧保留和迟到命令拒绝 |
| `RuntimeCommandErrorCode` / `RuntimeCommandError` | command validation 错误码和结构化错误 |
| `RuntimeCommandValidationResult` | command 入队 / validator 结果，包含 accepted command 或 error |
| `IRuntimeCommandValidator` | 外部命令校验扩展点，可检查 command id 注册、payload 合法性等 |
| `RuntimeReplayHeader` | Replay 元信息：schema、framework、config/resource hash、start frame |
| `RuntimeReplayFrameRecord` | 单帧 replay 记录：frame、commands、result hash、diagnostics summary |
| `RuntimeReplayRecorder` | Replay v0 记录器，记录 frame record 并创建只读快照 |
| `RuntimeReplaySnapshot` | Replay 只读快照，复制 header 和 frame records |
| `IRuntimeReplayFrameDriver` | Replay playback 外部驱动扩展点，负责执行单帧 record 并返回实际 hash |
| `RuntimeReplayPlaybackRunner` | Replay playback 编排器，按 snapshot record 顺序执行并比较 hash |
| `RuntimeReplayPlaybackFrameResult` | 单帧 playback 结果，包含 actual hash、diagnostics 和 command errors |
| `RuntimeReplayPlaybackResult` | Playback 汇总结果，包含 success、frames played、首个 mismatch 或 driver exception |
| `RuntimeHashContext` | Runtime hash 计算上下文，当前包含 frame |
| `IRuntimeHashContributor` | 模块 hash 贡献接口，使用稳定 `ContributorId` 排序 |
| `RuntimeHashCombiner` | 多 contributor 稳定排序、去重和组合入口 |
| `RuntimeHashAccumulator` | 稳定 hash 累加器，使用显式 key/value 输入并支持 double 量化 |
| `IDeterministicRandom` | 权威确定性随机接口 |
| `DeterministicRandom` | XorShift32 v1 确定性随机实现 |
| `RuntimeRandomState` / `RuntimeRandomStateJson` | 随机状态 capture / restore / JSON roundtrip |
| `RuntimeEventQueue<T>` | 按帧缓冲、稳定 drain、可诊断的 runtime event queue |
| `RuntimeEventQueueSnapshot` | Event queue pending 数、帧范围和 sequence 诊断 |
| `IRuntimeTimerScheduler` | Runtime timer 调度接口 |
| `RuntimeTimerScheduler` | 可作为 `IRuntimeModule` 注册的 noEngine timer scheduler |
| `RuntimeTimerHandle` | 基于 `StableHandle` 的 timer handle，防止 stale cancel |
| `RuntimeTimerCallback` / `RuntimeTimerContext` | Timer callback 模式上下文 |
| `RuntimeTimerSchedulerSnapshot` / `RuntimeTimerSnapshotEntry` | Pending timer 诊断快照 |
| `RuntimeTimerSchedulerStateSummary` / `RuntimeTimerStateSummary` | Timer 诊断摘要；不可直接恢复，真实 SaveState 需要 resolver 和完整 command/callback payload |
| `RuntimeTimerSchedulerState` / `RuntimeTimerState` | 兼容别名，语义同 summary |
| `CooldownTracker` / `CooldownTrackerSnapshot` | 基于 `RuntimeFrame` 的冷却跟踪、过期清理和诊断 |
| `VersionToken` | 简单版本 token |
| `DirtyFlag` | dirty 标记和版本递增工具 |
| `VersionedValue<T>` | 值变化时递增版本的包装 |
| `RuntimeOperationStatus` | 通用 operation 状态：Pending / Running / Succeeded / Failed / Cancelled / TimedOut |
| `RuntimeOperationError` | Operation 错误码和消息 |
| `IRuntimeOperation` / `RuntimeOperation` | 通用运行时 operation 状态模型 |
| `RuntimeRateLimiter` | 基于 frame 或显式 seconds 的限频 |
| `RuntimeDebouncer` | 基于 frame 或显式 seconds 的防抖 |
| `RuntimeCommandDefinition` / `RuntimeCommandPayloadSchema` | RuntimeCommand 调试定义和 payload schema |
| `RuntimeCommandRegistry` / `RuntimeCommandRegistrySnapshot` | command id 注册表和稳定快照 |
| `RuntimeCommandRegistryValidator` | 基于 registry 的 `IRuntimeCommandValidator` |
| `RuntimeStateMachine<TState>` | 局部状态机，不替代 AppFlow / SceneFlow |
| `RuntimeStateTransitionPredicate<TState>` | State machine 转换规则委托 |
| `ContextKey<T>` | typed context key |
| `RuntimeContextMap` / `RuntimeContextMapSnapshot` | typed blackboard / context map 和诊断摘要 |
| `RuntimeEasing` / `RuntimeEasingFunctions` | 表现层 easing 工具 |
| `RuntimeFloatInterpolator` | noEngine float 插值工具 |
| `RuntimeTween` | 显式 delta 驱动的 presentation tween；只用于 view、UI、diagnostics 表现，不进入 replay/hash 权威状态，除非调用方把结果记录为确定性输入 |
| `RuntimeSnapshotValue` | 简单 key/value snapshot 项 |
| `RuntimeChangeKind` / `RuntimeChange` / `RuntimeChangeSet` | Snapshot diff change set |
| `RuntimeSnapshotDiff` | 简单 key/value snapshot diff |
| `RuntimeSaveState` | SaveState v0 根文档，包含 schema、版本、frame、entities、global counters、module states 和 metadata |
| `RuntimeEntitySaveState` | Entity runtime 状态 DTO，包含 definition/team/alive、attributes、buffs、modifiers、abilities、counters 和 custom state |
| `RuntimeAttributeSaveState` | Attribute base value 与 final value 恢复策略 |
| `RuntimeBuffSaveState` | Active Buff 状态：buff/instance/layer/time/source/config/custom state |
| `RuntimeModifierSaveState` | Modifier 状态：modifier/instance/source/param/counters/custom state |
| `RuntimeAbilitySaveState` | Ability 状态：cooldown、charges、last cast frame、source config 和 custom state |
| `RuntimeCounterSaveState` | Counter id/value |
| `RuntimeCustomState` | 自定义状态 envelope，必须保留 `typeId`、`schemaVersion` 和 payload |
| `RuntimeModuleSaveState` | Runtime module 自定义存档状态 |
| `IRuntimeSaveStateProvider` | 模块或组合根提供 SaveState 的扩展点 |
| `IRuntimeSaveStateRestorer` | 模块或组合根恢复 SaveState 的扩展点 |
| `RuntimeSaveStateErrorCode` / `RuntimeSaveStateError` | SaveState 结构化错误码和错误路径 |
| `RuntimeSaveStateResult<T>` | SaveState 操作结果，包含 value 或 error |
| `IRuntimeSaveStateMigration` | 单步 schema migration 契约 |
| `RuntimeSaveStateMigrationPipeline` | 多步 schema migration 串联管线 |
| `RuntimeSaveStateJson` | Newtonsoft.Json v0 serializer，提供 `SaveToJson()` / `LoadFromJson()` |
| `RuntimeSaveStateParticipant` | SaveState provider/restorer 注册项，包含 participant id、order 和扩展点 |
| `RuntimeSaveStateRegistry` | SaveState participant 注册表，拒绝重复 id 并按 order/id 稳定排序 |
| `RuntimeSaveStateCoordinator` | SaveState capture/restore 编排器，合并 provider 输出并聚合 restore 错误 |

## 最小示例

```csharp
using MxFramework.Runtime;

var host = new RuntimeHost();
host.RegisterModule(new MyGameplayModule());

host.Initialize();
host.Start();
host.Tick(frameIndex: 0, deltaTime: 1.0 / 60.0, elapsedTime: 0.0);
host.Stop();
host.Dispose();
```

模块示例：

```csharp
public sealed class MyGameplayModule : RuntimeModule
{
    public MyGameplayModule()
        : base("gameplay", RuntimeTickStage.Simulation, priority: 0)
    {
    }

    public override void Tick(RuntimeTickContext context)
    {
        // Advance pure runtime state with context.DeltaTime.
    }
}
```

## 调度规则

同一帧内模块按以下顺序执行：

1. `RuntimeTickStage` 升序。
2. `Priority` 升序。
3. `ModuleId` ordinal 字典序。

这保证注册顺序不同也能得到稳定 Tick 顺序。

## Frame / Command 规则

`RuntimeFrame` 使用非负 `long` 值。`RuntimeClock.Step()` 只会从当前帧前进到下一帧；`Reset(frame)` 可把 clock 重置到任意合法帧，供测试、回放或会话重启使用。

Runtime 时间域只表达 Host 调度和通用输入顺序：

- Runtime frame：`RuntimeFrame` / `RuntimeTickContext.FrameIndex`，是外层组合根传给 `RuntimeHost.Tick(...)` 的非负调度序号，用于 command drain、Replay record、Hash context、SaveState frame 和模块排序诊断。
- Runtime tick delta：`RuntimeTickContext.DeltaTime`，是本次 Host tick 的显式秒数输入。它可以是渲染帧 delta、固定更新 delta、测试脚本指定 delta 或 replay driver 指定 delta；Runtime 只校验非负并转交模块，不声明它等于任何固定模拟步长。
- Runtime elapsed：`RuntimeTickContext.ElapsedTime`，是外层提供的会话累计时间，只作诊断、表现或模块自定义输入，不是固定模拟 authority。

固定模拟 step 不属于 `MxFramework.Runtime` 公共契约。需要固定步进的模块必须在自己的程序集或组合根中拥有 clock / accumulator / step config，并把 `RuntimeTickContext.DeltaTime` 显式转换为本模块的 step。`MxFramework.Runtime` 源码保持 Combat / Gameplay agnostic，不引用 `CombatFrame`、`CombatStepConfig`、Ability timeline frame 或任何 Combat-owned bridge 类型。

`RuntimeCommandBuffer.Enqueue(command)` 会在接受命令时分配 `Sequence`。`DrainForFrame(frame)` 返回目标帧命令，排序规则为：

1. `Frame`。
2. `SourceId`。
3. `CommandId`。
4. `TargetId`。
5. `Sequence`。

命令 frame 大于当前 drain frame 时保留到未来帧；命令 frame 小于或等于当前 drain frame 时作为到期命令返回；命令 frame 小于 buffer 当前帧时返回 `RuntimeCommandErrorCode.LateCommand`。负数 `CommandId` 返回 `InvalidCommandId`。项目层可通过 `IRuntimeCommandValidator` 返回 `UnregisteredCommandId` 或 `InvalidPayload`。

示例：

```csharp
var clock = new RuntimeClock();
var buffer = new RuntimeCommandBuffer();

buffer.Enqueue(new RuntimeCommand(
    frame: clock.CurrentFrame,
    sourceId: 100,
    commandId: 10,
    targetId: 200,
    payload0: 1,
    traceId: "ui.cast"));

IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(clock.CurrentFrame);
clock.Step();
```

## Replay 规则

Replay v0 只记录输入和结果摘要，不记录完整对象图：

- `RuntimeReplayHeader`：schema、framework version、config hash、resource catalog hash、start frame。
- `RuntimeReplayFrameRecord`：frame、commands、result hash、diagnostics summary。
- `RuntimeReplayRecorder.CreateSnapshot()`：返回只读拷贝，后续 recorder 追加不会改变旧 snapshot。

Replay 不等同于 SaveState，不能用于长期存档兼容。

Replay playback v0 通过 `RuntimeReplayPlaybackRunner` 编排：

- `IRuntimeReplayFrameDriver.Reset(header)` 由外层准备运行时世界。
- 每个 `RuntimeReplayFrameRecord` 按 snapshot 原顺序交给 `RunFrame(record)`。
- Playback 比较 record 的 expected `ResultHash` 和 driver 返回的 actual hash。
- 首个 hash mismatch 会返回失败结果，包含 frame、expected、actual、commands 和 diagnostics。
- Driver 抛异常会返回结构化 `DriverException` 失败，不吞异常对象。
- 当前不包含 JSON replay 文件格式；JSON replay export/playback 后续单独定义。

## Hash 规则

Runtime hash contract 只定义稳定输入协议，不绑定具体 Gameplay / Combat：

- `RuntimeHashCombiner` 按 `ContributorId` ordinal 排序后执行 contributor。
- 重复 `ContributorId` 被拒绝，避免同一模块状态被重复写入。
- `RuntimeHashAccumulator` 要求调用方写入显式 key 和 value，避免字段碰撞。
- Double 输入必须通过量化写入，禁止直接依赖平台浮点字符串格式。
- Hash 输入不得包含对象地址、Dictionary 原始迭代顺序、本地化文本、Unity 实例 ID、当前系统时间。

## SaveState 规则

SaveState v0 记录可恢复的运行时状态契约，不直接反序列化私有 runtime 对象，也不引用 Gameplay / Combat / Resources：

- `RuntimeSaveState.CurrentSchemaVersion` 当前为 `0`。
- 根文档包含 framework/config/resource catalog version 和 frame，便于恢复方诊断版本不匹配。
- Entity、Buff、Modifier、Ability、Counter 只保存通用 ID、数值和自定义状态 envelope。
- `RuntimeCustomState` 用 `TypeId` + `SchemaVersion` + `PayloadJson` 承载项目或模块状态，恢复方必须显式检查类型和版本。
- `RuntimeSaveStateJson.LoadFromJson()` 对空 JSON、非法 JSON、缺失 `schemaVersion`、负数 frame 和较新 schema 返回 `RuntimeSaveStateResult<RuntimeSaveState>` 错误，不静默吞掉。
- `RuntimeSaveStateMigrationPipeline` 只接受单步迁移器（`ToSchemaVersion == FromSchemaVersion + 1`），跨版本由 pipeline 串联；缺失迁移返回 `MissingMigration`。
- `RuntimeSaveStateRegistry` 按 participant id 注册 provider/restorer，重复 id 返回结构化错误。
- `RuntimeSaveStateCoordinator` capture 时按稳定 participant 顺序合并输出，restore 时先执行 migration pipeline，再按 participant 顺序恢复并聚合错误。

示例：

```csharp
RuntimeSaveState state = provider.CaptureSaveState().Value;
string json = RuntimeSaveStateJson.SaveToJson(state);

RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
if (loaded.Success)
{
    restorer.RestoreSaveState(loaded.Value);
}
```

## 错误策略

| 策略 | 行为 |
|------|------|
| `FailFast` | 记录 `RuntimeHostError` 后抛 `RuntimeHostException` |
| `CollectAndStopFrame` | 记录错误并停止当前操作后续模块 |
| `CollectAndContinue` | 记录错误并继续执行后续模块 |

生命周期阶段如果出现模块错误，Host 不会推进到下一生命周期状态。Tick 阶段出现错误时，Host 仍会记录本帧已尝试处理并增加 `TickCount`，除非 `FailFast` 直接抛出。

## 边界

- Runtime Host 是组合根，不是全局单例。
- Runtime 不读取 `Time.deltaTime`；frame 和 delta 由外层传入。
- Runtime 不保存 Unity 对象实例。
- Runtime 不知道 Ability、Buff、Combat、Resource 的具体类型；这些模块可以在自己的程序集里实现 `IRuntimeModule`，也可以在模块侧把 Runtime tick delta 桥接到自己的固定 step。
- Runtime frame 不能被跨模块直接当作 Combat frame、Ability timeline frame 或其他固定模拟帧。任何这种映射都必须由目标模块拥有的上下文定义，并说明是否有 accumulator、丢帧、补帧、最大步数或重同步语义。
- Runtime 只定义 SaveState 契约和迁移/序列化工具；具体 Gameplay 恢复由外层模块实现。目前 Ability Showcase 已实现一条 `RuntimeAbilitySliceRunner` 恢复路径。
- `RuntimeServiceRegistry` 只用于组合根装配，不应成为业务代码随处拉服务的替代架构。

## 当前 Showcase 接入

`RuntimeAbilitySliceRunner` 是 Demo 层适配示例，不属于 `MxFramework.Runtime` 程序集：

- 注册 command / simulation / diagnostics 三个 RuntimeHost 模块。
- HUD 手动按钮通过 `RuntimeCommandBuffer.Enqueue()` 转成 RuntimeCommand。
- 每帧 diagnostics 生成 `RuntimeReplayFrameRecord` 和稳定 result hash。
- 实现 `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer`，可完成 Ability Slice save -> reset -> load -> continue。

## 当前不支持

- JSON replay serialization / playback。
- 具体 Gameplay / Combat result hash contributor adapter。
- 通用 Gameplay save/load 与实体工厂接入。
- 网络同步或 rollback。

以上能力由 Runtime Foundation 后续接入任务继续推进。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Runtime/RuntimeHostTests.cs`

`Assets/Scripts/MxFramework/Tests/Runtime/RuntimeFrameCommandReplayTests.cs`

`Assets/Scripts/MxFramework/Tests/Runtime/RuntimeReplayPlaybackTests.cs`

`Assets/Scripts/MxFramework/Tests/Runtime/RuntimeHashContributorTests.cs`

`Assets/Scripts/MxFramework/Tests/Runtime/RuntimeSaveStateTests.cs`

`Assets/Scripts/MxFramework/Tests/Runtime/RuntimeSaveStateOrchestrationTests.cs`

`Assets/Scripts/MxFramework/Tests/Ability/RuntimeAbilitySliceRuntimeFoundationTests.cs`
