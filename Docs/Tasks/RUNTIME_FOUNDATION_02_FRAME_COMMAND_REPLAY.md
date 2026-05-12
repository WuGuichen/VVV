# Runtime Foundation 02：Frame Clock / Command / Replay

> 状态：Frame / Command / Replay Core v0.1 Implemented；Ability Showcase command/hash 接入已完成；playback 待办
> 日期：2026-05-10
> 优先级：P0
> 设计文档：`Docs/RUNTIME_FOUNDATION_SYSTEM.md`
> 前置：`RUNTIME_FOUNDATION_01_RUNTIME_HOST.md`

## 目标

为 Runtime Host 增加统一帧、命令输入和 Replay 记录能力，让 Demo、Combat、Preview 和测试可以用同一条输入序列复现运行时结果。

本任务不实现联网协议，但要为未来单机回放、Bug 复现、帧同步验证和 AI 自动测试留下稳定契约。

## 范围

### 做

- 定义 `RuntimeFrame` / `RuntimeClock`。
- 定义通用 `RuntimeCommand` 契约。
- 定义 `RuntimeCommandBuffer`，每帧 drain。
- 定义 Replay record / playback。
- 定义 frame result hash / diagnostics dump。
- 接入 Runtime Showcase 和 Combat Showcase 的最小命令路径。

### 不做

- 不做网络传输。
- 不做输入设备适配层。
- 不把命令绑定到具体按键。
- 不保证所有现有 Demo 行为一次性迁移。
- 不实现完整 rollback netcode。

## 核心模型

```text
External input
  -> RuntimeCommandBuffer.Enqueue(command)
  -> RuntimeHost.Tick(frame)
  -> Commands are sorted / validated / dispatched
  -> Modules mutate runtime state
  -> ReplayRecorder records command + result hash
```

命令只描述“请求发生了什么”，不直接描述“最终状态应该是什么”。最终结果由模块在该帧按规则计算。

## 建议 API

```csharp
public readonly struct RuntimeFrame
{
    public int Value { get; }
}

public sealed class RuntimeClock
{
    public RuntimeFrame CurrentFrame { get; }
    public RuntimeFrame Step();
    public void Reset(RuntimeFrame frame);
}

public readonly struct RuntimeCommand
{
    public RuntimeFrame Frame { get; }
    public int SourceId { get; }
    public int CommandId { get; }
    public int TargetId { get; }
    public int Payload0 { get; }
    public int Payload1 { get; }
    public int Payload2 { get; }
    public string TraceId { get; }
}
```

第一版 payload 可保持简单，避免过早引入泛型序列化。后续若需要复杂输入，可增加 `RuntimeCommandPayload` 或 command schema。

## 命令排序

同一帧内命令顺序必须稳定：

1. `Frame`。
2. `SourceId`。
3. `CommandId`。
4. `TargetId`。
5. 入队序号。

非法命令处理：

| 场景 | 行为 |
|------|------|
| 命令 frame 小于当前 frame | 拒绝并记录 late command |
| 命令 frame 大于当前 frame | 保留到目标帧 |
| command id 未注册 | 返回 validation error |
| payload 不合法 | 不派发，进入 replay diagnostics |

## Replay 记录

Replay 第一版建议记录：

```text
ReplayHeader
  - schemaVersion
  - frameworkVersion
  - configHash
  - resourceCatalogHash
  - startFrame

ReplayFrameRecord
  - frame
  - commands[]
  - resultHash
  - diagnosticsSummary
```

不建议记录完整对象图；完整对象图属于 SaveState 或 DebugDump。

## Hash 策略

首版不要追求跨平台绝对完美确定性，但要做到同一运行环境下可稳定复现。

Hash 输入建议包括：

- Runtime frame。
- 关键 Entity id / alive / team。
- Attribute final values。
- Buff id / layer / remaining time frame quantized。
- Modifier id / counters。
- Combat world revision / body positions。
- Last ability result。

Hash 不应包含：

- 对象地址。
- Dictionary 迭代原始顺序。
- 本地化文本。
- Unity 对象实例 ID，除非已映射为稳定 ResourceKey。

## 实现记录

2026-05-10 Core v0.1 已落地：

- 新增 `RuntimeFrame`、`RuntimeClock`，支持显式 frame、step、reset；frame 禁止负数。
- 新增 `RuntimeCommand`，包含 `Frame`、`SourceId`、`CommandId`、`TargetId`、`Payload0-2`、`TraceId`、`Sequence`。
- 新增 `RuntimeCommandBuffer.Enqueue()` / `DrainForFrame()`，入队时分配稳定 `Sequence`；同帧 drain 按 `Frame`、`SourceId`、`CommandId`、`TargetId`、`Sequence` 排序；未来帧保留；迟到命令返回结构化 validation error。
- 新增 `RuntimeCommandErrorCode`、`RuntimeCommandError`、`RuntimeCommandValidationResult`、`IRuntimeCommandValidator`，用于 late / invalid / unregistered / payload validation。
- 新增 `RuntimeReplayHeader`、`RuntimeReplayFrameRecord`、`RuntimeReplayRecorder`、`RuntimeReplaySnapshot`，记录 frame commands、result hash 和 diagnostics summary；快照返回只读拷贝。
- 新增 `RuntimeFrameCommandReplayTests` 覆盖 clock step/reset、同帧命令稳定排序、未来帧保留、迟到/非法命令拒绝、Replay recorder copy/snapshot 行为。

仍未完成：

- Runtime Showcase / Combat Showcase 命令路径接入。Ability Showcase 已完成；Combat Showcase 待办。
- JSON replay 导出 / playback。
- Gameplay / Combat result hash contributor。Ability Showcase 已有局部 result hash；通用 contributor 待办。
- Replay desync diagnostics dump。

## Milestones

### M1：Frame Clock

- 当前状态：✅ Core v0.1 已实现 `RuntimeFrame` / `RuntimeClock`，Clock reset / step 有测试。
- Host 已由 Foundation 01 接收显式 frame / delta；本任务未改 Host 调度签名。
- 不从 `Time.deltaTime` 读取权威时间。

### M2：Command Buffer

- 当前状态：✅ Core v0.1 已实现入队、按 frame drain、稳定排序和 validation result。
- Runtime Showcase 的 Ability 按钮已转成 command 再由 Host 派发。

### M3：Replay Recorder

- 当前状态：✅ Core v0.1 可记录命令、result hash 和 diagnostics summary，并返回只读快照。
- JSON 导出和 JSON playback 仍待实现。

### M4：Combat / Gameplay Hash

- Gameplay slice 提供 frame hash contributor。
- Combat showcase 提供 frame hash contributor。
- 相同 replay 输入产生相同 hash。

### M5：Diagnostics Dump

- Replay 失败或 hash 不一致时输出最小差异上下文。
- Dump 可被 AI 或人类读取，不要求还原完整状态。

## 测试建议

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Runtime
Unity EditMode: MxFramework.Tests.Combat
Unity EditMode: MxFramework.Tests.Gameplay
```

测试重点：

- 同帧乱序命令排序稳定。
- 未来帧命令不会提前执行。
- 迟到命令有结构化错误。
- 相同 replay 文件 playback 结果 hash 一致。
- Hash contributor 不依赖集合原始迭代顺序。

## 验收

- Runtime Host 可以用显式 frame 驱动。
- Runtime Showcase 至少一个交互按钮通过 command buffer 生效。
- Replay 可记录、导出、回放最小 Gameplay slice。
- Combat 或 Gameplay 至少一个模块提供 result hash contributor。
- 文档写清 Replay 不是 SaveState，不能用于长期存档兼容。
