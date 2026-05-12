# Core Pooling 01：ObjectPool / ReferencePool

> 状态：Planned
> 日期：2026-05-11
> 优先级：P0
> 设计文档：`Docs/INTERFACES.md`、`Docs/API_STANDARDS.md`

## 背景

当前 `MxFramework.Core` 有 `Heap<T>`、`UnsortList<T>`、`BitUtils`、`zstring`、`RandomTable`、`VectorExtensions` 等工具，但没有公开通用对象池 / 引用池。

框架内已经有局部池需求，例如 `ModifierContext` 使用私有静态 `Stack<ModifierContext>` 做复用。这个模式应该沉淀为 Core 工具，避免每个模块维护自己的池化代码、清理约定和重复释放行为。

## 目标

在 `MxFramework.Core` 下新增 `Pooling`：

- `ObjectPool<T>`：普通 class 对象池。
- `IReference`：可清理引用对象契约。
- `ReferencePool<T>`：面向 `IReference` 的实例池。
- `ListPool<T>` / collection pool 作为可选小步补充。

第一版采用实例池，由组合根或模块持有；不新增隐式全局单例，避免污染测试、Replay 和模块生命周期。

本任务属于 `CORE_RUNTIME_UTILITIES_01.md` 的 P0 基础工具批次。

## 范围

### 做

- 新增 `Assets/Scripts/MxFramework/Core/Pooling/`。
- 新增 `ObjectPool<T>`。
- 新增 `IReference`。
- 新增 `ReferencePool<T>`。
- 可选新增 `ListPool<T>` 或 `CollectionPool<TCollection>`，但保持 API 克制。
- 更新 `Docs/Interfaces/Core.md` 记录公共接口。
- 新增 Core 测试。
- 将 `ModifierContext` 的私有静态池改为使用统一 `ReferencePool<ModifierContext>`，或在本任务记录后续替换子任务。

### 不做

- 不新增 `GlobalReferencePool.Acquire<T>()` 这类默认全局池。
- 不做多线程池。
- 不把 Unity `GameObject` / `Component` 生命周期纳入 Core pool。
- 不处理资源引用计数；`MxFramework.Resources` 的 `ResourceHandle<T>` 和 retain policy 仍归资源生命周期管理。

## ObjectPool API

```csharp
public sealed class ObjectPool<T> where T : class
{
    public ObjectPool(
        Func<T> create,
        Action<T> onGet = null,
        Action<T> onRelease = null,
        int defaultCapacity = 0,
        int maxSize = 1024);

    public int CountInactive { get; }
    public int CountActive { get; }
    public int CountAll { get; }

    public T Get();
    public void Release(T item);
    public void Clear();
}
```

行为要求：

- `create` 不能为空。
- `defaultCapacity` 和 `maxSize` 不能为负。
- `maxSize` 控制 inactive 容量；超过后 release 对象不再缓存。
- `onGet` 在对象返回给调用方前执行。
- `onRelease` 在对象进入 inactive 池前执行。
- 重复释放应被检测并返回结构化错误或抛出明确异常；不能让 `CountActive` 变成负数。
- `Release(null)` 行为必须明确，建议抛 `ArgumentNullException`。

## ReferencePool API

```csharp
public interface IReference
{
    void Clear();
}

public sealed class ReferencePool<T> where T : class, IReference, new()
{
    public int CountInactive { get; }
    public int CountActive { get; }
    public int CountAll { get; }

    public T Get();
    public void Release(T item);
    public void Prewarm(int count);
    public void Clear();
}
```

行为要求：

- `Release` 必须调用 `item.Clear()`。
- `Prewarm` 只增加 inactive，不改变 active。
- `Clear` 清空 inactive；active 仍由调用方负责归还。
- 重复释放和 null release 必须有测试覆盖。
- `ModifierContext` 接入时，`Clear()` 应保留 `Extra` 字典实例并清空内容，避免额外分配。

## 可选 Collection Pool

如果同一任务内补 collection pool，优先做克制版本：

```csharp
public static class ListPool<T>
{
    public static PooledList<T> Get();
    public static PooledList<T> Get(out List<T> list);
}
```

或先只做 `CollectionPool<TCollection>`，要求 release 时调用 `Clear()`。

示例目标：

```csharp
using (PooledList<CombatQueryResult>.Get(out var hits))
{
    world.Query(query, hits);
}
```

如果实现会扩散过大，本任务只记录设计，不纳入 M1 验收。

## 测试

新增测试入口建议：

```text
Assets/Scripts/MxFramework/Tests/Core/ObjectPoolTests.cs
Assets/Scripts/MxFramework/Tests/Core/ReferencePoolTests.cs
```

覆盖：

- `Get` 创建新对象并更新计数。
- `Release` 后对象进入 inactive。
- `onGet` / `onRelease` 调用顺序正确。
- `maxSize` 限制 inactive 缓存。
- `Clear` 清空 inactive 且计数正确。
- `Prewarm` 预热数量正确。
- `IReference.Clear()` 在 release 时调用。
- null release 行为明确。
- 重复释放可被检测。
- `ModifierContext` 归还后字段清理。

## 验收

- `MxFramework.Core` 提供 noEngine pooling 工具。
- 公共 API 文档更新到 `Docs/Interfaces/Core.md`。
- 至少 `ObjectPool<T>`、`IReference`、`ReferencePool<T>` 有测试覆盖。
- 不引入全局隐式状态；所有池实例由调用方持有或显式注入。
- 如改动 `ModifierContext`，现有 Modifiers / Preview / Tests 继续编译。
