# GAMEPLAY_ECS_STYLE_05_COMPONENT_QUERY

## 目标

在 generation entity id 和 component store 之上补一层稳定查询辅助，让 Gameplay systems 不直接依赖 store 内部结构，也不各自手写遍历 / join 逻辑。

本批次仍保持 noEngine，不引用 Unity / Editor / Demo / WGame 私有数据。

## 新增 API

```csharp
public readonly struct GameplayComponentPair<TPrimary, TSecondary>
    where TPrimary : struct, IGameplayComponent
    where TSecondary : struct, IGameplayComponent
{
    public GameplayEntityId EntityId { get; }
    public TPrimary Primary { get; }
    public TSecondary Secondary { get; }
}

public static class GameplayComponentQuery
{
    public static int CopyEntities<T>(
        GameplayComponentStore<T> store,
        List<GameplayEntityId> output);

    public static int CopyComponents<T>(
        GameplayComponentStore<T> store,
        List<T> output);

    public static int CopyEntries<T>(
        GameplayComponentStore<T> store,
        List<GameplayComponentSnapshot<T>> output);

    public static int CopyPairs<TPrimary, TSecondary>(
        GameplayComponentStore<TPrimary> primaryStore,
        GameplayComponentStore<TSecondary> secondaryStore,
        List<GameplayComponentPair<TPrimary, TSecondary>> output);
}
```

## 语义

- Query 方法都 append 到调用方提供的 output，不隐式 clear。
- 单组件查询沿用 `GameplayComponentStore<T>` 的稳定 entity id 顺序。
- 双组件 join 以 primary store 的稳定顺序为输出顺序。
- 双组件 join 只输出同时拥有 primary / secondary component 的 entity。
- Query 不暴露 store 内部容器；后续 store 可替换为 sparse set / dense array / archetype，而不破坏 system API。

## 非目标

- 不做 archetype / chunk / job / burst。
- 不做表达式 query DSL。
- 不做自动缓存和增量 dirty query。
- 不迁移 `RuntimeEntity` / `GameplayWorld` source of truth。

## 验收

- 单组件 entry / entity / component 输出顺序稳定。
- 双组件 join 输出交集，并按 primary store 顺序稳定。
- Registry destroy cleanup 后，query 不返回已销毁 entity 的 registered components。
- Null input 会抛出结构化 `ArgumentNullException`。
