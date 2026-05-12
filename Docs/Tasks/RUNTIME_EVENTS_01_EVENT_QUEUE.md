# Runtime Events 01：RuntimeEventQueue

> 状态：Planned
> 日期：2026-05-11
> 优先级：P0
> 前置：`RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md`

## 目标

新增按帧缓冲、可 drain、可诊断、可保存的 Runtime event queue，补足同步 EventBus 之外的运行时事件流能力。

```csharp
public sealed class RuntimeEventQueue<T> where T : struct
{
    public void Enqueue(RuntimeFrame frame, in T evt);
    public int Drain(RuntimeFrame frame, List<T> output);
    public RuntimeEventQueueSnapshot CreateSnapshot();
}
```

## 与 EventBus 的区别

| 工具 | 语义 | 用途 |
| --- | --- | --- |
| `EventBus<T>` | 即时发布 | 低耦合同步通知 |
| `RuntimeEventQueue<T>` | 按帧缓冲 / 稳定 drain / 可诊断 / 可保存 | Gameplay -> UI / Audio / Diagnostics / Replay 边界 |

## 用途

- Ability 事件按帧输出给 UI。
- Combat hit / miss 事件记录。
- Audio cue event 从 Gameplay 转给 Audio。
- Replay diagnostics。
- SaveState restore 后继续保留 pending event。
- UI event log 不直接监听内部模块私有字段。

## 范围

### 做

- 新增 `Assets/Scripts/MxFramework/Runtime/Events/RuntimeEventQueue*.cs`。
- 按 `RuntimeFrame` 入队和 drain。
- 同帧内按入队 sequence 稳定输出。
- `Drain(frame, output)` 只输出目标帧及已到期事件，未来事件保留。
- 提供 snapshot summary：pending count、oldest frame、newest frame、trace / type summary。
- 支持 SaveState 或定义 event queue state envelope。
- 测试中使用 struct event fixture。

### 不做

- 不替代现有同步 EventBus。
- 不强制所有事件都可序列化；不可恢复事件必须有明确 diagnostics。
- 不允许事件 queue 直接执行 gameplay mutation。

## 测试

新增测试建议：

```text
Assets/Scripts/MxFramework/Tests/Runtime/RuntimeEventQueueTests.cs
```

覆盖：

- 同帧事件按入队顺序 drain。
- 未来帧事件不会提前 drain。
- 迟到帧事件按设计 drain 或报错，行为必须明确。
- snapshot 记录 pending 数量和帧范围。
- Save / Restore 后 pending events 顺序稳定。
- Replay 中事件诊断输出稳定。

## 验收

- Runtime 提供 noEngine event queue。
- EventBus 和 RuntimeEventQueue 的职责在文档中分清。
- 至少一个后续 Ability / Combat / UI / Audio 集成任务可以使用 event queue 作为边界。

