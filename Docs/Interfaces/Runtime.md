# Runtime 接口

> Status: Current
> Code-Doc: `Packages/com.vincent.wgameframework/Runtime/RuntimeHashSchemaIds.cs`
>
> Runtime Foundation Host Core v0.1 + Frame / Command / Replay Core v0.1 + fixed typed command envelope + trusted source authorization/exogenous replay filtering + hard-bounded atomic batch admission + SaveState Contract v0 + authoritative root-header composition + end-of-Tick checkpoint/quiescence barrier + shared restore profile/bounded UTF-8 JSON 已实现（2026-07-20）。本页记录 `MxFramework.Runtime` 当前公共接口和边界。

2026-10-12补充：`RuntimeHashSchemaIds.NetworkSnapshotDoubleEdgeStage = 1018` 为CharacterTest阶段级双刃状态注册独立schema ID，v1根字段367。通用Runtime不持有此游戏状态；结构与私有恢复边界见[双刃配件交接](../Tasks/TACTICAL_DOUBLE_EDGE_ACCESSORY_IMPLEMENTATION.md)。

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
- SaveState v0 DTO、provider/restorer 契约、错误模型、迁移管线、strict bounded UTF-8 JSON、shared restore profile/plan 和多 participant 编排。
- 最近 Tick attempt 完成状态、checkpoint quiescence registry 与 fail-closed end-of-Tick capture barrier。
- Deterministic random，可 capture / restore。
- Runtime event queue，按帧缓冲并稳定 drain。
- Runtime timer scheduler，支持 frame / seconds / repeating / command timer。
- App/Scene flow controller contract，供 Unity 或 Resources.Unity driver 在外层适配。
- Cooldown、versioning、operation、rate limit / debounce 和 command registry 运行时质量工具。
- Local state machine、typed context map、presentation interpolation 和 snapshot diff 工具。

## 为什么不依赖 Unity

`MxFramework.Runtime` 是 `noEngineReferences=true` 的纯 C# 程序集，不引用 `UnityEngine` 或 `UnityEditor`。Unity 场景、MonoBehaviour、输入、时间和资源对象都应在外层适配后传给 Host。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `RuntimeHost` | 运行时组合根，负责模块注册、生命周期和 Tick 调度 |
| `RuntimeHost.HasExactAppFlowPumpRegistration` (internal to SessionBridge/tests) | NoAlloc composition-time exact-reference 校验；`expectedModule=null` 证明 Standalone 未注册 wrapper，非 null 证明唯一 exact RuntimeModule wrapper；不向普通调用方暴露 module-registration authority |
| `RuntimeHost.HasExactSceneFlowPumpRegistration` (internal to SessionBridge/tests) | NoAlloc composition-time exact SceneFlow controller/module validation used before candidate scene I/O; rejects RuntimeModule/Standalone mismatch and duplicate wrappers |
| `RuntimeHost.IsTickInProgress` / `HasTickAttempt` / `LastTickAttemptFrame` / `LastTickAttemptSucceeded` | NoAlloc 暴露最新 Tick attempt；失败的新帧会覆盖较早成功帧，不提供 checkpoint 回退 |
| `IRuntimeModule` | 可被 Host 调度的模块契约 |
| `RuntimeModule` | 空实现基类，便于模块只重写需要的生命周期 |
| `RuntimeLifecycleState` | Host 当前生命周期状态；生命周期失败进入终态 `Faulted`，最终 `Dispose()` 后进入 `Disposed` |
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
| `RuntimeCommand` | 通用输入命令契约，包含 frame、source、command、target、legacy payload、trace、sequence 和可选 fixed envelope |
| `RuntimeCommandEnvelope` / `RuntimeCommandProvenanceKind` | 固定值类型 typed payload：type/schema/provenance + 八个 `long` 数据槽；不持有数组、对象正文或 sidecar handle |
| `RuntimeCommandBuffer` | 命令缓冲区，支持 unbounded legacy 构造、`CreateBounded(...)` hard pending bound、`Enqueue()`、`DrainForFrame()`、未来帧保留和迟到命令拒绝 |
| `RuntimeCommandErrorCode` / `RuntimeCommandError` | command validation 错误码和结构化错误 |
| `RuntimeCommandValidationResult` | command 入队 / validator 结果，包含 accepted command 或 error |
| `IRuntimeCommandValidator` | 外部命令校验扩展点，可检查 command id 注册、payload 合法性等 |
| `RuntimeCommandSourceRegistry` / `RuntimeCommandSourceDefinition` | 初始化期注册稳定 SourceId、`Exogenous / Derived` 分类和 source-specific command allowlist；seal 后作为可信组合根分类事实 |
| `RuntimeCommandSourceRegistrationResult` / `RuntimeCommandSourceRegistrationErrorCode` | source/allowlist 注册的结构化结果，覆盖非法/重复 source、kind、command id 与 sealed registry |
| `RuntimeCommandSubmissionGate` / `RuntimeCommandIngressKind` | 固定 External 或 TrustedDerived ingress 的 opt-in 提交边界；单条或 hard-bounded atomic batch 都先校验 source、provenance 与 allowlist再进入现有 command buffer |
| `RuntimeCommandBatchSubmissionResult` | atomic batch 的稳定结果；成功返回完整 `SubmittedCount`，失败返回首个 caller index、typed error 且 buffer 状态不变 |
| `RuntimeReplayHeader` | Replay 元信息：schema、framework、config/resource hash、start frame |
| `RuntimeReplayFrameRecord` | 单帧 replay 记录：frame、commands、result hash、diagnostics summary |
| `RuntimeReplayRecorder` / `RuntimeReplayCommandCaptureMode` | Replay v0 记录器；legacy 构造为 unfiltered compatibility，`CreateExogenousStream` 只复制 registry 认可的可信 exogenous commands |
| `RuntimeReplaySnapshot` | Replay 只读快照，复制 header 和 frame records |
| `IRuntimeReplayFrameDriver` | Replay playback 外部驱动扩展点，负责执行单帧 record 并返回实际 hash |
| `RuntimeReplayPlaybackRunner` | Replay playback 编排器，按 snapshot record 顺序执行并比较 hash |
| `RuntimeReplayPlaybackFrameResult` | 单帧 playback 结果，包含 actual hash、diagnostics 和 command errors |
| `RuntimeReplayPlaybackResult` | Playback 汇总结果，包含 success、frames played、首个 mismatch 或 driver exception |
| `RuntimeHashContext` | Runtime hash 计算上下文，当前包含 frame |
| `IRuntimeHashContributor` | 模块 hash 贡献接口，使用稳定 `ContributorId` 排序 |
| `RuntimeHashCombiner` | 多 contributor 稳定排序、去重和组合入口 |
| `RuntimeHashAccumulator` | 稳定 hash 累加器，使用显式 key/value 输入并支持 double 量化 |
| `RuntimeFixedLayoutHash64` | 固定 16 字节输入的 XXH64：两个有序 Int64 与预定义域种子直接计算，NoAlloc |
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
| `SceneFlowController` | 场景流编排器，接收稳定 scene key 并轮询 driver operation |
| `ISceneFlowDriver` / `ISceneFlowOperation` | 外层场景加载适配契约；Unity scene list 或资源 catalog driver 都在 Runtime 外实现 |
| `SceneFlowRequest` / `SceneFlowResult` | 场景加载/卸载请求与结果 DTO |
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
| `RuntimeCustomState` | 自定义状态 envelope，必须保留正整数 `numericTypeId`、诊断标签 `typeId`、`schemaVersion` 和 payload |
| `RuntimeModuleSaveState` | Runtime module 自定义存档状态 |
| `IRuntimeSaveStateProvider` | 模块或组合根提供 SaveState 的扩展点 |
| `IRuntimeSaveStateRestorer` | 模块或组合根恢复 SaveState 的扩展点 |
| `RuntimeSaveStateRootHeaderDescriptor` | 组合期冻结的 schema/framework/config/resource catalog 根头描述；精确 ordinal 匹配 |
| `IRuntimeSaveStateRootHeaderAuthority` / `RuntimeSaveStateRootHeaderProvider` | 唯一 order-0 根头权威契约和默认 header-only provider |
| `RuntimeSaveStateErrorCode` / `RuntimeSaveStateError` | SaveState 结构化错误码和错误路径 |
| `RuntimeSaveStateResult<T>` | SaveState 操作结果，包含 value 或 error |
| `IRuntimeSaveStateMigration` | 单步 schema migration 契约 |
| `RuntimeSaveStateMigrationPipeline` | 多步 schema migration 串联管线 |
| `RuntimeSaveStateRestoreProfile` / `RuntimeSaveStateRestorePlan` | 复制并冻结 descriptor/migration topology 的 opaque composition identity，以及仅由该 profile prepare 的不可变迁移/校验计划 |
| `RuntimeSaveStateJson` | Newtonsoft.Json v0 serializer；除 legacy string API 外提供 bounded `SaveToUtf8()` / strict `LoadFromUtf8()` |
| `RuntimeSaveStateParticipant` | SaveState provider/restorer 注册项，包含 participant id、order 和扩展点 |
| `RuntimeSaveStateRegistry` / `RuntimeSaveStateRegistrySnapshot` | SaveState participant 注册表；authoritative preflight/seal 会验证唯一根头权威并冻结稳定 catalog |
| `RuntimeSaveStateCoordinator` / `RuntimeSaveStateCoordinatorFactoryResult` | legacy capture/restore 编排器，以及 fail-closed authoritative composition/capture/restore 入口；`CreateAuthoritativeWithProfile` 与 `RestorePrepared` 强制精确 profile identity |
| `RuntimeCheckpointContext` / `RuntimeCheckpointQuiescenceResult` | completed frame 与 ready/pending numeric detail 的 readonly、NoAlloc 检查值 |
| `IRuntimeCheckpointQuiescenceChecker` | participant 纯只读 quiescence 检查；不得 drain、clear 或修改 pending state |
| `RuntimeCheckpointQuiescenceParticipant` / `RuntimeCheckpointQuiescenceRegistry` | 初始化期按 `Order -> ParticipantId ordinal` 建立稳定检查顺序；重复 id 结构化拒绝 |
| `RuntimeCheckpointBarrier` | 只在最新 Tick 完整成功且所有 participant ready 后调用 coordinator，并把根 Frame 归一到 completed frame |
| `RuntimeCheckpointErrorDetailCode` | host/checker 的固定 numeric detail；participant pending detail 由 ParticipantId 作用域隔离 |

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

`RuntimeCommandBuffer.Enqueue(command)` 会在接受命令时分配 `Sequence`。`RuntimeCommandBuffer(initialCapacity)` 和 `RuntimeCommandBuffer(validator, startFrame, initialCapacity)` 可在初始化期预分配 pending command 容量；当 pending command 数量不超过该容量时，`Enqueue` / caller-owned `DrainForFrame(frame, destination)` 是 NoAlloc 热路径。无容量预分配或超过容量时，`List<T>` 增长是 LowFreqAlloc。

`RuntimeCommand.TraceId` 使用 `RuntimeTraceIdentity`，直接持有预定义 `KindId`、数值 `SourceId`、执行实例 `ExecutionId` 和 `Sequence`；默认值表示没有追踪身份。`WithSequence`、命令缓冲和网络 DTO 都复制相同数值身份，不提供字符串转换入口。诊断显示可按需格式化，但格式化结果不得送回身份计算。计时器 trace 同步使用该结构，计时器状态 schema 为 2。

需要真实 admission 上限时使用 `RuntimeCommandBuffer.CreateBounded(pendingCapacityLimit)` 或带 validator/start frame 的 bounded overload。limit 可为 `0`，构造期完整预分配并通过 `IsPendingCapacityBounded / PendingCapacityLimit` 只读暴露；旧构造继续 unbounded，`initialCapacity` 仍不是 hard maximum。bounded `Enqueue` 固定先执行 late/id/validator，再检查 pending hard limit，只有接受后才分配 sequence；满容量返回 `RuntimeCommandErrorCode.PendingCapacityExceeded=9`，不改变 pending 或 sequence。bounded target 恢复超限 snapshot 会在 clear/frame/sequence mutation 前抛错并保持原状态，snapshot 不重定义 target limit；unbounded restore 保持旧语义。`RuntimeCommandSubmissionGate` 只投影 target bound，不绕过上述顺序。

`RuntimeCommandEnvelope` 是可选、向后兼容的 fixed-value 扩展。默认值表示没有 envelope；非默认 envelope 显式携带正数 `TypeId`、正数 `SchemaVersion`、`Unspecified / Exogenous / Derived` provenance 和八个 64-bit 数值槽。构造、相等、hash、`WithSequence`、buffer snapshot 和 timer command reschedule 均为 NoAlloc 值复制。具体 domain codec 负责字段布局与语义校验；Runtime 不解释业务槽位。

`RuntimeCommandSourceRegistry` 在组合根初始化期复制并排序每个可信 SourceId 的 command allowlist；`Seal()` 后 `TryGet`、source/provenance/allowlist authorization 均为 `NoAlloc`。`SourceId=0 + default envelope` 明确保留为 legacy/unspecified 兼容语义，不进入可信 registry。`RuntimeCommandSubmissionGate` 按固定 `External` 或 `TrustedDerived` ingress fail-closed：未知 source、classification/ingress 不一致、缺失/错误 provenance 或未列入 allowlist 的 command 都返回结构化 `RuntimeCommandValidationResult`，不依赖异常控制流。它不改变 `RuntimeCommandBuffer.Enqueue`；旧调用继续工作，但 direct enqueue 不是 authorization boundary。

`RuntimeCommandSubmissionGate.SubmitBatch(ReadOnlySpan<RuntimeCommand>)` 只接受 hard-bounded target；unbounded target 即使输入为空也返回 `AtomicBatchRequiresBoundedBuffer=11`、`FailedCommandIndex=-1` 和 default command，合法 bounded 空批成功且提交数为 0。非空批严格按 caller index 执行 authorization，再执行 late/id/target validator、累计 pending capacity、sequence availability；任一失败返回首个 index，pending、`CurrentFrame` 与 next sequence 全部不变。全部 preflight 成功后才按 caller order 一次 commit。sequence 的可接受范围为 `0..long.MaxValue-1`，`long.MaxValue` 是 exhausted sentinel；单条 `Enqueue` 与 atomic batch 都以 `SequenceExhausted=10` 拒绝且不修改状态。`IRuntimeCommandValidator` 是纯验证契约：不能依赖 command 已提交，也不能产生外部可见 mutation，因为已验证的早期 item 仍可能随后续 item 失败而整体不提交。

需要证明跨模块 reliable admission 与 target module 二次 validation 完全一致时，Runtime 只向受信 sibling assembly 提供 internal/read-only `RuntimeCommandBuffer.ValidatorIdentity` 与 gate target-validator projection。该 seam 不公开 validator 或 buffer 的可变 authority。19.3C3 Story bridge 要求 bounded buffer admission validator 与 `StoryRuntimeModule.Validator` 为 exact same instance；近似 validator、无 validator 或 custom registry validator 均在组合期 fail-closed，避免 accepted outbox record 被删除后由 target module 后置拒绝。

可信点是 composition root 只向对应 adapter/client edge 暴露 gate，而不是 `SourceId` 数字本身。该 API 不提供跨进程身份认证，也不把 source metadata 描述为网络防伪；wire codec、session identity 与 client intent 到 server command 的转换仍由后续 network adapter 定义。

`DrainForFrame(frame)` 返回目标帧命令，排序规则为：

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
const int CastTraceKind = 100;

buffer.Enqueue(new RuntimeCommand(
    frame: clock.CurrentFrame,
    sourceId: 100,
    commandId: 10,
    targetId: 200,
    payload0: 1,
    traceId: new RuntimeTraceIdentity(kindId: CastTraceKind, sourceId: 200, executionId: 1)));

IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(clock.CurrentFrame);
clock.Step();
```

## Replay 规则

Replay v0 只记录输入和结果摘要，不记录完整对象图：

- `RuntimeReplayHeader`：schema、framework version、config hash、resource catalog hash、start frame。
- `RuntimeReplayFrameRecord`：frame、commands、result hash、diagnostics summary。
- command record 包含完整 fixed envelope value；不会只记录 frame-local handle。
- `new RuntimeReplayRecorder(header)`：legacy/unfiltered compatibility；复制 caller 传入的全部 commands，不能当作可信 exogenous stream。
- `RuntimeReplayRecorder.CreateExogenousStream(header, sourceRegistry)`：seal registry，只复制已注册 `Exogenous` source、envelope provenance 匹配且 command 在 source allowlist 中的 commands；Derived、unknown、legacy/unspecified、mismatch 与 unauthorized command 被排除。
- filter 保持 caller command 顺序；frame record 与 snapshot 继续深拷贝完整 fixed command/envelope value，后续 caller list 或 recorder 变化不修改旧记录。
- `RuntimeReplayRecorder.CreateSnapshot()`：返回只读拷贝，后续 recorder 追加不会改变旧 snapshot。
- `RuntimeReplayRecorder.PrepareCapacity(frameCapacity, commandCapacity)`：在录制准备阶段分配独立的后续帧槽与命令容量。容量内追加不创建 record/list/wrapper；每个槽只发布一次，`Clear()` 不回收仍可能被持有的旧 record。超出准备容量保持兼容扩容，不截断命令。
- `RecordFrameDeferred(..., Func<RuntimeReplayFrameRecord, string>)`：保存已捕获的命令和 hash，将诊断文本格式化推迟到 `DiagnosticsSummary` 首次读取。formatter 必须只依赖该 record 和自身冻结的上下文，不得读取可变实时状态；调用方应复用 formatter，避免逐帧闭包。

`RuntimeCommandSourceRegistry.TryRegister(...)`、未准备容量的 replay record 和 snapshot export 是初始化/录制侧 `AllocByDesign`；准备容量内的 replay record 为 `NoAlloc after initialization`。sealed registry query、`RuntimeCommandSubmissionGate.Submit(...)` / `SubmitBatch(...)` 与 rejection path 在 command buffer 容量内为 `NoAlloc after initialization`。

Replay 不等同于 SaveState，不能用于长期存档兼容。

Replay playback v0 通过 `RuntimeReplayPlaybackRunner` 编排：

- `IRuntimeReplayFrameDriver.Reset(header)` 由外层准备运行时世界。
- 每个 `RuntimeReplayFrameRecord` 按 snapshot 原顺序交给 `RunFrame(record)`。
- Playback 比较 record 的 expected `ResultHash` 和 driver 返回的 actual hash。
- 首个 hash mismatch 会返回失败结果，包含 frame、expected、actual、commands 和 diagnostics。
- Driver 抛异常会返回结构化 `DriverException` 失败，不吞异常对象。
- 当前不包含 JSON replay 文件格式；JSON replay export/playback 后续单独定义。

## Hash 规则

### 整数状态协议迁移（进行中）

`RuntimeHashAccumulator` 是唯一正式运行时状态哈希类型，编码版本为 3；构造时必须指定预定义的正整数 schema ID 和版本。根头固定写入编码版本、算法 ID、schema ID 和 schema 版本；默认算法 ID 为 1（流式 FNV-1a，XXH64 候选由 `MXFRAMEWORK_RUNTIME_HASH_XXH64` 编译符号选择）。字段 Key 为模块源码中显式分配的正整数。`AddInt` / `AddLong` / `AddBool` / `AddDoubleQuantized` / `AddDigest` 不接收字符串 Key 或字符串身份。对象通过 `BeginObject/EndObject` 编码 schema、版本和存在性；数组通过 `BeginArray/BeginElement/EndElement/EndArray` 编码数量、索引和边界。数组按声明顺序完整写入，未闭合、缺项、越界或空对象内写字段均拒绝生成结果。最大深度为 64；`Reset()` 保留根 schema 并清理整个工作区。整数按固定宽度小端编码，double 继续采用原量化规则，结果仍为 64 位。

`IRuntimeHashContributor` 声明整数 `ContributorId`、`HashSchemaId`、`HashSchemaVersion`；`RuntimeHashCombiner` 在构造时冻结这些值、拒绝重复身份，并按整数顺序贡献。实例工作区只允许同步使用，不支持递归或并发共享。`FieldCount/EncodedByteCount` 与 combiner 的 `LastFieldCount/LastEncodedByteCount` 为本次计算的计量，不参与状态输入。

`RuntimeHashDigest` 保存完整 256 位内容摘要，它不属于身份类型。`RuntimeDefinitionId` 表达制作期已分配的 `(domainId, value)`；`RuntimeDefinitionIdCatalog` 只接收数值条目并冻结索引，不解析名称或分配 ID。未定义、重复和 retired 身份在准备／恢复边界拒绝。制作期事务工具位于 `Tools/RuntimeHash/definition_ids.py`，使用显式 domain、expected revision/hash 与排他写入锁。

`RuntimeContentHashAccumulator` 保留版本 1 的低频内容编码；Ability 定义、Projectile 定义／spawn profile 和嵌套 HitEvent 配置已转到该入口，配置指纹不变。

**迁移收尾未关闭：** 正式状态入口已移除 `RuntimeHashLegacyExtensions` 与无参 `RuntimeHashAccumulator`，不接受字符串 Key 或 `RuntimeHashKey`；低频内容编码保留其独立用途。完整身份 owner、未激活分支、Runtime AI Planner 制作／持久化、完整行为矩阵与 Editor／Mono Player 性能选型仍不能按旧批次通过计数推定完成。2026-10-05 只读源码核对网络快照常量为 38、回放常量为 7 / `character-test-replay-v0.7`；不是本次重新编译或测试的证据。剩余范围、测量要求及收尾约束见[整数哈希迁移剩余收尾](../Tasks/RUNTIME_NUMERIC_HASH_MIGRATION_IMPLEMENTATION.md)。

### 固定布局的两结果组合

`RuntimeFixedLayoutHash64.Hash16(long first, long second, ulong domainSeed)` 返回 `long`，按 xxHash v0.8.3 的 XXH64 计算 `LE64(first) || LE64(second)`。输入固定为 16 字节，负数保留原始位模式；字段顺序有意义。调用直接执行安全 C# 整数运算，没有字符串、字段 Key、缓冲区、累加器实例或线程静态状态。`AlgorithmId = 2` 是冻结的协议编号；1、3 分别保留给比较阶段的 FNV/finalizer、XXH3-64，不作为生产运行时选项。

CharacterTest 的 `CombineGameplayStateHash` 已直接调用此接口，域固定为 `SchemaId = 1001`、`LayoutVersion = 1`、`domainSeed = 0x000003E900000001`。无会话参与者时，外层调用仍直接返回 gameplay hash。完整／non-owned、Loopback、UnityTransport 及回放使用同一个组合内核；上游状态哈希生成、会话权威内容哈希和 Lab 帧语义不变。

会话构造时通过 `CharacterTestSessionHashContract.Normalize` 把 `|ct.session-hash=2:1001:1` 加入既有 `ConfigHash`；重复规范化不重复追加，冲突、重复或非结尾标识拒绝。所有 CharacterTest 直接会话、endpoint、host、room wrapper 与 factory 暴露相同的规范化配置，握手在状态同步前拒绝旧配置。回放使用 schema 7 / `character-test-replay-v0.7`，旧记录在创建、释放或恢复世界前拒绝；网络快照布局为 38。实现、golden 与实测见 [会话固定布局哈希实施记录](../Tasks/RUNTIME_FIXED_LAYOUT_SESSION_HASH.md)。

### 迁移前编码及尚未迁移的调用方

Runtime hash contract 只定义稳定输入协议，不绑定具体 Gameplay / Combat：

- `RuntimeHashCombiner` 按 `ContributorId` ordinal 排序后执行 contributor。
- 重复 `ContributorId` 被拒绝，避免同一模块状态被重复写入。
- `RuntimeHashAccumulator` 要求调用方写入显式 key 和 value，避免字段碰撞。
- `RuntimeHashAccumulator.Reset()` 无分配地恢复构造时的协议种子与字段计数；私有、同步使用的哈希工作区可复用实例。Reset 不改变 hash 编码版本；不得与另一次未完成的累加共享同一实例。
- `RuntimeHashKey` 仅供低频 `RuntimeContentHashAccumulator` 保留 v1 配置编码使用；它不属于状态 API，也不存在转换到整数状态 Key 的适配层。
- Double 输入必须通过量化写入，禁止直接依赖平台浮点字符串格式。
- Hash 输入不得包含对象地址、Dictionary 原始迭代顺序、本地化文本、Unity 实例 ID、当前系统时间。

## SaveState 规则

SaveState v0 记录可恢复的运行时状态契约，不直接反序列化私有 runtime 对象，也不引用 Gameplay / Combat / Resources：

- `RuntimeSaveState.CurrentSchemaVersion` 当前为 `0`。
- 根文档包含 framework/config/resource catalog version 和 frame，便于恢复方诊断版本不匹配。
- Entity、Buff、Modifier、Ability、Counter 只保存通用 ID、数值和自定义状态 envelope。
- `RuntimeCustomState` 用 `NumericTypeId` + `SchemaVersion` + `PayloadJson` 承载项目或模块状态，`TypeId` 仅保留诊断标签。构造必须传入预定义的正整数类型号；缺失数值类型号不得从标签推断。恢复方必须显式检查数值类型和版本。确需校验文本 payload 的状态拥有者在变更或导入边界调用 `PreparePayloadDigest()`，保存返回的不可变对象；哈希贡献只读 `PreparedPayloadDigest`，未准备时明确报错。`Copy()` 保留已准备的摘要，不重新读取文本。
- `RuntimeSaveStateJson.LoadFromJson()` 对空 JSON、非法 JSON、缺失 `schemaVersion`、负数 frame 和较新 schema 返回 `RuntimeSaveStateResult<RuntimeSaveState>` 错误，不静默吞掉。
- `RuntimeSaveStateMigrationPipeline` 只接受单步迁移器（`ToSchemaVersion == FromSchemaVersion + 1`），跨版本由 pipeline 串联；缺失迁移返回 `MissingMigration`。
- `RuntimeSaveStateRestoreProfile.Create(descriptor, pipeline)` 复制 descriptor 并冻结 migration topology；`Prepare(saveState)` 只迁移和精确校验 root descriptor，不调用 participant restorer，并返回绑定到该 profile 实例的不可变 `RuntimeSaveStateRestorePlan`。migration 改变根 `Frame` 时以 `FrameChangedDuringMigration` fail-closed。
- `RuntimeSaveStateRestoreProfile.CreateSibling()` 复制相同 descriptor/topology，但生成新的 opaque composition identity，供未发布候选 Runtime 独占组合；value-equal descriptor 不能替代 profile 的 reference identity。
- `RuntimeSaveStateJson.SaveToUtf8(state, maxByteCount, maxDepth)` 在写入期间同时执行 byte/depth 上限，不先构造无界 JSON string；`LoadFromUtf8(...)` 使用 strict UTF-8，拒绝 duplicate property、trailing token、非 object root、byte overflow 和 structural depth overflow。`maxDepth` 合法范围是 `1..64`，Persistence Runtime bridge 固定使用 `16` 和 `SaveGameLimits.MaximumPayloadBytes`。
- `RuntimeSaveStateRegistry` 按 participant id 注册 provider/restorer，重复 id 返回结构化错误。
- `RuntimeSaveStateCoordinator` capture 时按稳定 participant 顺序合并输出，restore 时先执行 migration pipeline，再按 participant 顺序恢复并聚合错误。
- durable composition 必须注册且只注册一个 `mxframework.runtime.save-header` order-0 provider，并由 `RuntimeSaveStateCoordinator.CreateAuthoritative(...)` 完成 preflight、seal 与 catalog snapshot。任一其他 capture provider 使用负 order 或 order 0 都会被拒绝；restorer-only participant 不参与根头权威判定。
- authoritative factory 失败返回 `Success=false` 且 `Coordinator=null`，不会调用 provider；调用方必须停止组合，不能构造 checkpoint barrier 或回退 legacy coordinator。
- factory 以独占 composition lease 执行 prepare -> descriptor/migration freeze -> revision-guard commit；失败不封存 registry，也不泄漏 lease。成功时冻结 participant 排序、migration catalog、目标 schema 和 descriptor 四字段；封存后 registry 拒绝继续注册。
- migration 在注册时冻结 `FromSchemaVersion` / `ToSchemaVersion` 拓扑，factory snapshot 复用不可变 entry；之后修改自定义 migration 对象的版本属性不能改变已组合管线的步进和输出校验。以上路径属于 `AllocByDesign` 的组合期工作，不进入 Tick 热路径。
- authoritative capture 显式调用唯一根头权威，并要求其结果只含 schema/framework/config/resource descriptor、UTC 展示时间和 `Frame=0` placeholder。authority 的 failure/null/exception、descriptor mismatch、非零 Frame 或 body/metadata 污染均终止且不 fallback。
- 非 authority participant 的 legacy root header 会被忽略；entities/global counters/module states/metadata 仍按稳定 participant 顺序合并。任一 body provider 失败时返回 `Value=null`，不发布 partial document。
- authoritative restore 先在冻结 migration pipeline 上迁移，再精确校验 schema/framework/config/resource descriptor；不匹配时不会调用任何 restorer。`CreateAuthoritativeWithProfile(registry, profile)` 以独占 composition claim 绑定 exact profile，`RestorePrepared(plan)` 拒绝 legacy coordinator、null plan 和 cross-profile plan。participant 私有 body（包括 Trigger custom JSON）保持 opaque，不由 Runtime 解释。
- 直接构造 `RuntimeSaveStateCoordinator` 的 legacy 行为继续保留给已有调用方；生产 durable checkpoint 必须使用 authoritative factory 的成功结果。
- `RuntimeSaveStateErrorCode.PendingWork = 1401`、新增 `CompositionFailed = 1501`、`RootHeaderMismatch = 1502`、`RootHeaderPolluted = 1503`；`RuntimeSaveStateCoordinatorPhase.Checkpoint = 3`、新增 `Composition = 4`。旧 enum 数值、SaveState schema v0 和 provider/restorer DTO 不变。
- `RuntimeCheckpointErrorDetailCode` 固定为 `None=0`、`HostHasNoTickAttempt=1`、`HostTickInProgress=2`、`HostLastTickAttemptFailed=3`、`QuiescenceCheckException=4`、`InvalidQuiescenceResult=5`。
- `RuntimeSaveStateError` 保留原六参数构造精确签名，并以明确七参数 overload 增加 `DetailCode`；detail 为 `0` 时 `ToString()` 保持旧格式。
- authority checkpoint 组合根必须使用 `RuntimeCheckpointBarrier.CaptureCheckpoint()`。旧 `RuntimeSaveStateCoordinator.CaptureSaveState()` 保持兼容，但它不验证 Tick/quiescence，不能被描述为 end-of-Tick checkpoint。
- Barrier 先验证 host，再按注册期固定的 `Order -> ParticipantId ordinal` 检查全部 quiescence participant；host invalid 时 checker/provider 均不调用，任一 pending/default-invalid/exception 时 provider 不调用。
- 合法 pending 返回 `Checkpoint + PendingWork` 并保留 participant 正数 `DetailCode`；checker exception 与 default/非正 detail 返回 `Checkpoint + InvalidDocument`。多个 participant failure 按稳定顺序聚合。
- Coordinator capture 失败时 Barrier 保留原 participant errors 但返回 `Value=null`，不发布 partial merged state；成功时只把根 `RuntimeSaveState.Frame` 归一为最近完成帧，其余 header/body/metadata 只读复制。
- frame convention 是完成 F 后保存 `Frame=F`，恢复方从 F+1 继续。对同一成功 F 重复捕获是合法的，可用于多个 save slot。
- Gameplay Trigger SaveState participant 的推荐组合顺序是 Gameplay world/restored entities `100`、Trigger `200`；顺序值只是模块建议，复杂根必须自行保证无冲突。这里的SaveState原子性只覆盖Trigger自己的排他、单线程validate-plan-commit，不扩展为SaveState coordinator跨participant rollback。Phase 19.4B network snapshot另使用`NetworkBoundedStateRegistry`的candidate-all-then-ordered-commit contract；bounded rollback history则使用完整frame scratch延迟record发布，以及prepare-all -> ordered no-fail commit / failure discard-all，保证失败不覆盖旧ring slot也不产生跨participant partial live state。不能把两套registry混用。
- Gameplay Trigger participant 的 pending detail 在其 ParticipantId 作用域内固定为 input batch/items `1..4`、`PendingDeferredReservations=5`（同时覆盖 internal/reliable 未提交 reservation）、`PendingDeferredSignalFinalization=6`、`PendingReliableOutboxTransfer=7`。committed future/overdue deferred/reliable queues 是可捕获 authority，不是 pending；完整成功 Tick 必须在 Command finalization、Simulation Evaluate 与 reliable target transfer 结束后把 5/6/7 归零。

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

Durable Persistence 应使用 bounded UTF-8 与 shared profile，而不是上面的 legacy string convenience API：

```csharp
RuntimeSaveStateRestoreProfile profile = RuntimeSaveStateRestoreProfile.Create(descriptor, migrations);
RuntimeSaveStateRestoreProfile candidateProfile = profile.CreateSibling();
RuntimeSaveStateResult<byte[]> encoded = RuntimeSaveStateJson.SaveToUtf8(state, maxBytes, 16);
RuntimeSaveStateResult<RuntimeSaveState> decoded = RuntimeSaveStateJson.LoadFromUtf8(encoded.Value, maxBytes, 16);
RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> prepared = candidateProfile.Prepare(decoded.Value);
RuntimeSaveStateCoordinatorFactoryResult candidate =
    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, candidateProfile);
```

Persistence.RuntimeBridge 的 candidate restore/publication 边界见 `Interfaces/Persistence.md`。Runtime 只提供 profile-bound prepare/restore authority；它不发布 live session，也不保证跨 participant rollback。

## 错误策略

| 策略 | 行为 |
|------|------|
| `FailFast` | 记录 `RuntimeHostError`；Tick 立即抛出，生命周期操作完成全清理后再抛出根错误 |
| `CollectAndStopFrame` | 记录错误并停止当前操作的后续前向模块；生命周期随后执行全清理 |
| `CollectAndContinue` | 记录错误并继续前向模块；生命周期结束后执行全清理 |

模块成功注册即把该实例的生命周期所有权转移给 Host。模块即使尚未进入 `Initialize()`，也会在 Host 清理时被提供一次 `Dispose()`；因此注册成功后，调用方不得再独立释放同一实例。

Host 为每个模块记录 `InitializeEntered` / `Initialized` / `StartEntered` / `Started` 和清理尝试。`Initialize()` 或 `Start()` 回调一旦进入，即使它在建立外部副作用后抛出，模块也必须支持补偿：Host 对所有 `StartEntered` 模块按进入逆序尝试 `Stop()`，对所有 `InitializeEntered` 模块按进入逆序尝试 `Dispose()`，再对剩余已注册但未进入的模块按注册逆序尝试 `Dispose()`。每个适用清理回调最多尝试一次；一个清理错误不会截断后续清理。

任一前向生命周期错误都会使 Host 进入不可复用的 `Faulted` 终态。`RegisterModule()`、`Initialize()`、`Start()` 和 `Tick()` 均拒绝该状态，`Stop()` 为无操作，最终 `Dispose()` 仍幂等并进入 `Disposed`。错误顺序固定为前向错误、逆序 Stop 错误、逆序 Dispose 错误；`FailFast` 在清理完成后抛出第一个前向根错误。显式 `Stop()` / `Dispose()` 的 `FailFast` 也会先遍历全部适用清理，再抛出第一个清理错误。

Tick 错误策略和热路径保持不变：Tick 阶段出现错误时，Host 仍会记录本帧已尝试处理并增加 `TickCount`，除非 `FailFast` 直接抛出；初始化后的正常 Tick 路径继续满足 NoAlloc。

每次 Tick attempt 进入模块循环前把最新 frame 标记为 unsuccessful/in-progress；只有所有模块完成且本 attempt 没新增 `RuntimeHostError` 才成功。三种 error policy 都会让有模块错误的 attempt 保持 unsuccessful；`FailFast` 继续不增加 `TickCount`，两个 collect policy 继续按旧语义增加。成功 F 后失败 F+1 时 checkpoint 只能看到 F+1 失败，不得回退 F。

Application's unique live driver treats `HasTickAttempt && LastTickAttemptFrame == requestedFrame`
as the authoritative consumption fact. Consequently a FailFast throw after entering the Host still
consumes that frame even though `TickCount` does not increase. The live driver must not retry it;
standalone AppFlow/SceneFlow pumps run only after `RuntimeHost.Tick` returns normally. RuntimeModule
controllers remain ordinary Host modules and must not also be pumped by the outer driver.
`LastTickAttemptSucceeded=false` is an exact failed-attempt fact under every error policy: Collect
failures return to the driver as `RuntimeAttemptFailed`, while FailFast preserves the original
exception after the driver records frame consumption and clears its publication boundary.

## 边界

- Runtime Host 是组合根，不是全局单例。
- Runtime 不读取 `Time.deltaTime`；frame 和 delta 由外层传入。
- Runtime 不保存 Unity 对象实例。
- Runtime 不知道 Ability、Buff、Combat、Resource 的具体类型；这些模块可以在自己的程序集里实现 `IRuntimeModule`，也可以在模块侧把 Runtime tick delta 桥接到自己的固定 step。
- Runtime frame 不能被跨模块直接当作 Combat frame、Ability timeline frame 或其他固定模拟帧。任何这种映射都必须由目标模块拥有的上下文定义，并说明是否有 accumulator、丢帧、补帧、最大步数或重同步语义。
- Runtime 只定义 SaveState 契约和迁移/序列化工具；具体 Gameplay 恢复由外层模块实现。目前 Ability Showcase 已实现一条 `RuntimeAbilitySliceRunner` 恢复路径。
- Runtime checkpoint barrier 只负责 Host completion、quiescence 与根 Frame 归一；各模块仍须实现自己的 provider/checker。它不会自动 Tick、drain pending work；Gameplay Trigger payload/checker/restore 由 `MxFramework.Gameplay.Triggers` 的 participant 提供。
- `RuntimeServiceRegistry` 只用于组合根装配，不应成为业务代码随处拉服务的替代架构。

## 当前 Showcase 接入

`RuntimeAbilitySliceRunner` 是 Demo 层适配示例，不属于 `MxFramework.Runtime` 程序集：

- 注册 command / simulation / diagnostics 三个 RuntimeHost 模块。
- HUD 手动按钮通过 `RuntimeCommandBuffer.Enqueue()` 转成 RuntimeCommand。
- 每帧 diagnostics 生成 `RuntimeReplayFrameRecord` 和稳定 result hash。
- 实现 `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer`，可完成 Ability Slice save -> reset -> load -> continue。

## CharacterTest P2.4 关卡 Trigger 组合

`CharacterTestLevelGameplayTriggerRuntimeComposition` 是游戏层组合适配，不属于 `MxFramework.Runtime` 公共 API。它在 CharacterTest offline session bootstrap 时从已生成并复验的 Scene cache 装配正式 `GameplayTriggerRuntime`、Combat sensor、Gameplay Buff/Story bridge 和 Event Router，然后以 priority `110` 把对应 `GameplayRuntimeModule` 注册到现有 `RuntimeHost`。组合失败时 Host 使用 `FailFast` 并停止构建，不发布 partial module graph。

该组合按 Gameplay `100`、Trigger `200`、Story `300` 注册 SaveState participant/quiescence，并通过 `RuntimeCheckpointBarrier` 捕获完成帧。restore 后由项目适配层清除非权威 command/result/router/diagnostics 状态并重置 sensor scheduling；权威 Trigger relation/policy 与 Story/Gameplay 状态仍由各自 participant 恢复。CharacterTest result hash combiner覆盖 Gameplay、Trigger、Story 和 Scene Trigger catalog identity，但不把 Unity object、Transform 或展示文本写入 hash。运行 Tick 不读取 Authoring JSON 或 Scene。

当前接入不构成通用 Runtime Gameplay/Combat adapter，也不改变 Runtime 的依赖方向；它只服务 CharacterTest offline `GameSlice`。网络 session 注入、活动 session 热重载和 durable 用户存档 UI 不在 P2.4 范围。

## CharacterTest Pathfinding PF-M4 组合边界

PF-M4 仍是 `MxFramework.Game.CharacterTest` 的项目内组合，不向 `MxFramework.Runtime` 增加 graph、pathfinder 或 navigation 公共类型。session bootstrap/显式 rebuild 低频创建唯一 immutable graph、bounded A* scratch 和 caller-owned path buffer，再由每帧 Perception/Planner 只读消费；Runtime Host 只提供既有 frame、module 和 command 顺序。

graph、corridor、navigation sample 和 Runtime AI Lab path diagnostics 都是当前帧可重算派生数据，不注册 SaveState/rollback participant，也不贡献 authority/result hash。CharacterTest snapshot apply、rollback 或 session rebuild 会丢弃旧 cache/session 引用，并用恢复后的角色/CombatPhysics 状态在下一 Tick 重算；只读 presentation/recovery hash 可以覆盖 diagnostics，以发现界面恢复差异，但不能改变 Frame Sync authority 结论。当前验证范围是 offline session 与本地 Runtime AI Lab loopback，没有 production server、跨进程身份或分布式 transport 一致性承诺。

## 当前不支持

- JSON replay serialization / playback。
- command 的 JSON/network wire codec、跨进程身份认证和 client intent -> authoritative command 转换。
- 通用 Gameplay / Combat result hash contributor adapter；CharacterTest P2.4 只有项目专用组合。
- 通用 Gameplay save/load 与实体工厂接入。
- 网络同步或 rollback。

以上能力由 Runtime Foundation 后续接入任务继续推进。

## 当前验证

- #732 atomic batch admission：`MxFramework.Gameplay.Triggers.Unity.csproj` production build 0 warning / 0 error；Unity 6000.5.0f1 隔离工作树 BatchMode `MxFramework.Tests.Runtime` 295/295 通过，0 failed / 0 skipped。覆盖 bounded/unbounded empty、caller-order sequence、authorization -> late/id/validator -> capacity -> sequence 顺序、任一失败 pending/frame/next-sequence 零变化、single/batch exhausted sentinel 与 64-cycle warm-up 后 0 B。V4 lightweight harness、changed-file standards report 和 `git diff --check` 通过且无 findings。
- Runtime root-header focused noEngine `net9.0`：descriptor/registry/coordinator/migration/legacy orchestration 36/36，通过；checkpoint barrier 15/15，通过；`netstandard2.1` 生产源码编译 0 warning / 0 error。
- Unity 6000.5.0f1 fresh BatchMode 编译 0 C# error；root-header registry、orchestration、checkpoint 和两个实际修改的 Trigger compatibility 用例合计 focused EditMode 53/53，通过，覆盖 composition 原子封存、opaque payload 保留和 failed Tick 优先拒绝。
- 原 feature head 的两个完整 Trigger fixture 为 92/93；唯一失败来自当时尚未同步 #702 RuntimeHost faulted 终态的旧 `Created` 断言。最终 integration base 已包含该断言修复，且 `GameplayTriggerReliableOutboxTests.Initialize_NonFailFastHostFailsClosed` focused EditMode 复验通过；本轮未重复运行两个完整 Trigger fixture。
- V4 lightweight harness 通过，15 个 changed first-party code file 无 findings；全仓 standards report-only 为 151 个既有 finding（149 个 `ZString.cs` unsafe，2 个既有 framework time read），本次未新增或扩大 `unsafe`。

## 测试入口

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeHostTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeFrameCommandReplayTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeReplayPlaybackTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeHashContributorTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeSaveStateTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeSaveStateOrchestrationTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeSaveStateRootHeaderRegistryTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/RuntimeCheckpointBarrierTests.cs`

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Ability/RuntimeAbilitySliceRuntimeFoundationTests.cs`

## 第二批配件涉及的 hash 版本（2026-10-12）

CharacterTest document schema 43 使用 Stage hash 对象 1018/version 2，增加因果轮、出生谱系、首击/每敌消费、延迟来源及身份分配字段；Gameplay Timeline contributor 2004/version 2 新增 field 15 NextInstanceId。完整、owned、non-owned 三条生产路径必须使用同一 Stage schema。旧 hash 不应与新版本直接比较；只读投影不授予恢复权威。边界与协调者待执行检查见[第二批实施交接](../Tasks/TACTICAL_ACCESSORY_SECOND_BATCH_IMPLEMENTATION.md)。
