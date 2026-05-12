# Core Collections 01：RingBuffer

> 状态：Planned
> 日期：2026-05-11
> 优先级：P0
> 父任务：`CORE_RUNTIME_UTILITIES_01.md`

## 目标

新增通用固定容量环形缓冲，用于最近事件、错误、命令、资源释放、Combat query、Audio request、Config issue 等 recent diagnostics 场景。

```csharp
public sealed class RingBuffer<T>
{
    public int Capacity { get; }
    public int Count { get; }
    public void Add(T item);
    public void CopyTo(List<T> output);
    public void Clear();
}
```

## 范围

### 做

- 新增 `Assets/Scripts/MxFramework/Core/Collections/RingBuffer.cs`。
- 固定容量，超过容量后覆盖最旧元素。
- `CopyTo(List<T> output)` 按从旧到新的稳定顺序输出。
- 支持 `Clear()`，不改变容量。
- 可选支持 `IReadOnlyList<T>` 风格枚举，但不暴露内部数组。
- 更新 `Docs/Interfaces/Core.md`。

### 不做

- 不做无界日志列表。
- 不做线程安全队列。
- 不内置 diagnostics schema；具体 snapshot 由上层模块定义。

## 测试

新增测试建议：

```text
Assets/Scripts/MxFramework/Tests/Core/RingBufferTests.cs
```

覆盖：

- 容量为正时 Add / CopyTo 保持顺序。
- 超过容量时覆盖最旧元素。
- Clear 后 Count 为 0。
- `CopyTo` 可追加到已有 List 或按设计先清空，行为必须明确并测试。
- 容量非法时抛明确异常。

## 验收

- Core 提供 noEngine `RingBuffer<T>`。
- 至少一个后续 diagnostics / recent errors 任务可以直接复用。
- 测试覆盖覆盖顺序和清理行为。

