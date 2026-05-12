# Runtime Foundation 04：Timer Scheduler

> 状态：Planned
> 日期：2026-05-11
> 优先级：P0
> 设计文档：`Docs/RUNTIME_FOUNDATION_SYSTEM.md`
> 前置：`RUNTIME_FOUNDATION_01_RUNTIME_HOST.md`、`RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md`、`RUNTIME_FOUNDATION_03_SAVE_STATE_SERIALIZATION.md`、`CORE_HANDLES_01_STABLE_HANDLE_TABLE.md`

## 背景

当前 `MxFramework.Runtime` 已经有 `RuntimeClock`、`RuntimeHost`、`RuntimeCommandBuffer`、Replay、SaveState 和 hash 基础，但还没有通用延迟执行 / 循环任务 / 冷却 / 超时调度层。

`RuntimeClock` 只负责显式帧推进，不负责把未来事件统一调度到目标帧。如果 Buff tick、Ability cooldown、UI toast、Combat hit window、Audio delay、SceneFlow timeout、Resource preload timeout 和 Demo 状态切换各自维护小计时器，会削弱 Replay、SaveState 和诊断的一致性。

## 目标

新增 `MxFramework.Runtime.Scheduling`，提供 noEngine 的运行时定时器 / 调度器：

- 支持 frame delay。
- 支持 seconds delay，基于显式 tick delta，不读取 Unity time。
- 支持 repeating timer。
- 支持 cancel。
- 支持同帧多个 timer 的稳定排序。
- 支持诊断 snapshot。
- 支持 SaveState，至少保存 pending timers 的 id、remaining frames / time、traceId 和重复间隔。
- 支持 callback 模式和 RuntimeCommand 模式。

## 范围

### 做

- 新增 `RuntimeTimerHandle`、`RuntimeTimerCallback`、`IRuntimeTimerScheduler`。
- 新增 `RuntimeTimerScheduler`，可作为 `IRuntimeModule` 接入 `RuntimeHost`。
- `RuntimeTimerHandle` 优先基于 `StableHandleTable<T>` / generation handle，避免 stale cancel 命中新 timer。
- 新增 `RuntimeTimerSchedulerSnapshot` / pending timer summary。
- 新增可诊断 state summary；真实 SaveState restore 闭环后续需要 resolver 和完整 command / callback payload。
- 新增 command 调度 API，到期后把 `RuntimeCommand` 写入指定 `RuntimeCommandBuffer`。
- 新增测试覆盖 frame、seconds、repeating、cancel、snapshot、state summary 和 replay hash 稳定性。

### 不做

- 不依赖 `UnityEngine.Time`、coroutine、MonoBehaviour 或 Unity Scheduler。
- 不做全局单例 scheduler。
- 不做线程调度、async await 调度或真实时间后台任务。
- 不把 scheduler 和具体 Buff / Ability / Combat 业务绑死。

## 建议 API

```csharp
public delegate void RuntimeTimerCallback(RuntimeTimerContext context);

public interface IRuntimeTimerScheduler
{
    RuntimeTimerHandle ScheduleFrames(
        long delayFrames,
        RuntimeTimerCallback callback,
        string traceId = "");

    RuntimeTimerHandle ScheduleSeconds(
        double delaySeconds,
        RuntimeTimerCallback callback,
        string traceId = "");

    RuntimeTimerHandle ScheduleRepeatingFrames(
        long intervalFrames,
        RuntimeTimerCallback callback,
        string traceId = "");

    RuntimeTimerHandle ScheduleCommand(
        long frameDelay,
        RuntimeCommandBuffer commandBuffer,
        RuntimeCommand command,
        string traceId = "",
        RuntimeScheduledCommandFramePolicy framePolicy = RuntimeScheduledCommandFramePolicy.NextFrame);

    bool Cancel(RuntimeTimerHandle handle);
    RuntimeTimerSchedulerSnapshot CreateSnapshot();
}
```

第一版必须同时支持两种使用模式：

| 模式 | 用途 | 规则 |
| --- | --- | --- |
| callback | Editor、UI、测试、诊断辅助 | callback 不参与 gameplay 权威逻辑时可以直接执行 |
| RuntimeCommand | 玩法、Buff、Ability、Combat、SceneFlow | 到期后统一写入 `RuntimeCommandBuffer`，由现有 command / replay / hash 链路处理 |

## 调度规则

- `delayFrames == 0` 表示下次 scheduler tick 可触发，不在 `Schedule*` 调用栈内同步触发。
- 负数 delay / interval 必须返回结构化错误或抛出明确参数异常，不能静默修正。
- repeating frame timer 的 `intervalFrames` 必须大于 0，避免无意义的零间隔重复调度。
- seconds delay 和 tick delta 必须是 finite 且非负，拒绝 NaN / Infinity / negative。
- 同一目标帧内按 `targetFrame`、`sequence`、`timerId` 稳定排序。
- cancel 已触发或不存在的 handle 返回 `false`。
- repeating timer 在 callback 或 command 入队后计算下一次目标帧；如果执行期间被 cancel，不再重排。
- seconds timer 必须由显式 delta 推进；测试中可以固定 delta，保证 Replay 稳定。

## State Summary

第一版只提供不可恢复的诊断摘要，至少记录：

```text
RuntimeTimerSchedulerState
  - schemaVersion
  - nextTimerId
  - nextSequence
  - timers[]

RuntimeTimerState
  - timerId
  - sequence
  - kind
  - targetFrame
  - remainingFrames
  - remainingSeconds
  - intervalFrames
  - intervalSeconds
  - isRepeating
  - traceId
  - command payload summary
```

callback timer 默认只保存可诊断摘要。真实 SaveState restore 闭环必须另补 resolver：command timer 保存完整 `RuntimeCommand` payload，callback timer 保存 callback id，并在不可恢复时返回结构化错误。Gameplay 逻辑应优先使用 `ScheduleCommand`。

## 测试

新增测试入口建议：

```text
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeTimerSchedulerTests.cs
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeTimerSchedulerSaveStateTests.cs
```

覆盖：

- frame delay 在目标帧触发。
- seconds delay 在显式 delta 累积后触发。
- repeating timer 按固定间隔触发。
- cancel 阻止未来触发。
- 同帧多个 timer 稳定排序。
- timer callback 内新增 timer 不破坏当前帧迭代。
- `ScheduleCommand` 到期后写入 `RuntimeCommandBuffer`。
- `ScheduleCommand` 默认写入 due frame 的下一帧，避免同帧 command drain 已发生时产生 LateCommand；需要同帧消费时显式使用 `DueFrame` policy。
- State summary 能暴露 pending timers 的剩余时间、traceId 和触发顺序；真实 restore path 后续单独补。
- replay hash 在相同 command 序列下保持一致。

## 验收

- `MxFramework.Runtime` 新增 scheduler 公共接口和实现，保持 noEngine。
- Scheduler 可注册为 `RuntimeHost` 模块，不要求调用方自己在多个模块里分散 tick。
- Callback 模式可用于测试和 UI 辅助；玩法侧推荐并验证 RuntimeCommand 模式。
- State summary / snapshot 能定位 pending timer、traceId、目标帧和重复状态；当前 summary 不承诺可恢复。
- 测试覆盖上述场景，并通过 `dotnet build MxFramework.Tests.csproj --no-restore` 或 Unity EditMode 验证。

## 2026-05-11 实现记录

- 新增 `RuntimeTimerHandle`，基于 `StableHandle` / generation handle。
- 新增 `RuntimeTimerCallback`、`RuntimeTimerContext`、`IRuntimeTimerScheduler`。
- 新增 `RuntimeTimerScheduler`，继承 `RuntimeModule`，可手动 tick 或注册到 `RuntimeHost`。
- 支持 `ScheduleFrames`、`ScheduleSeconds`、`ScheduleRepeatingFrames`、`ScheduleCommand`、`Cancel`、`CreateSnapshot` 和 `CreateStateSummary`。
- `ScheduleCommand` 到期后写入 `RuntimeCommandBuffer`，默认 command frame 为 timer due frame 的下一帧；`RuntimeScheduledCommandFramePolicy.DueFrame` 可显式保留同帧投递。
- 新增 `RuntimeTimerSchedulerSnapshot`、`RuntimeTimerSnapshotEntry`、`RuntimeTimerSchedulerStateSummary`、`RuntimeTimerStateSummary`。
- 新增 `RuntimeTimerSchedulerTests.cs` 和 `RuntimeTimerSchedulerSaveStateTests.cs`，覆盖目标帧、zero delay、seconds delay、repeating、cancel、稳定排序、callback 内新增 timer、command 入队、stale handle 和 snapshot/state。
- 验证：临时源码级 `dotnet test` 已通过；Unity EditMode 需 Unity 刷新新文件后再跑。
