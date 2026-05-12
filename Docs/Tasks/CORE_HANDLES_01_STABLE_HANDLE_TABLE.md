# Core Handles 01：StableHandleTable

> 状态：Planned
> 日期：2026-05-11
> 优先级：P0
> 父任务：`CORE_RUNTIME_UTILITIES_01.md`

## 目标

新增通用 generation handle 表，防止 stale handle 操作到复用后的新对象。

```csharp
public readonly struct StableHandle
{
    public int Index { get; }
    public int Generation { get; }
}

public sealed class StableHandleTable<T>
{
    public StableHandle Add(T value);
    public bool TryGet(StableHandle handle, out T value);
    public bool Remove(StableHandle handle);
    public void Clear();
}
```

## 用途

- Timer 取消时防止取消到复用后的旧 timer。
- Audio loop handle 防止 stop 错对象。
- UI 动态元素绑定时防止 stale reference。
- Operation / loading task 管理。
- Combat debug marker / temporary effect 生命周期。

## 范围

### 做

- 新增 `Assets/Scripts/MxFramework/Core/Handles/StableHandle*.cs`。
- `StableHandle` 支持相等比较、hash、`IsValid` 和清晰 `ToString()`。
- `StableHandleTable<T>` 支持 add / try get / remove / clear。
- remove 后 slot 可复用，但 generation 必须递增。
- 支持 snapshot summary：capacity、active count、free count。
- TimerScheduler 实现时优先复用该 handle 表。

### 不做

- 不做线程安全版本。
- 不把 `ResourceHandle<T>` 改成通用 handle；资源模块可以后续评估是否内部复用。
- 不暴露内部数组供外部修改。

## 规则

- 默认 handle 必须无效。
- stale handle `TryGet` 返回 `false`。
- stale handle `Remove` 返回 `false`。
- `Clear` 后旧 handle 全部失效。
- generation 溢出必须有明确策略：抛异常或跳过无效 generation，不静默回绕到可命中旧 handle。

## 测试

新增测试建议：

```text
Assets/Scripts/MxFramework/Tests/Core/StableHandleTableTests.cs
```

覆盖：

- Add 后 TryGet 成功。
- Remove 后 TryGet 失败。
- slot 复用后旧 handle 不能命中新值。
- default handle 无效。
- Clear 使全部旧 handle 失效。
- 多次 Add / Remove 后 active / free count 正确。

## 验收

- `MxFramework.Core` 提供 noEngine stable handle 表。
- Timer / Operation / Audio / UI 等后续 handle 类型可以基于它封装。
- 测试覆盖 stale handle 防护。

