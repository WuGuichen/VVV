# GAMEPLAY_ECS_STYLE_06_COMPONENT_WORLD

## 目标

新增 `GameplayComponentWorld`，把 ECS-style component runtime 的核心运行时对象聚合到一个明确组合根中：

- `GameplayComponentRegistry`
- `RuntimeEventQueue<GameplayRuntimeEvent>`

并让 `GameplaySystemContext` 暴露该 component world，供后续 component systems 统一访问 entity lifecycle、component stores 和 frame event queue。

本批次不迁移 `RuntimeEntity` / `GameplayWorld` 的权威状态。

## 新增 API

```csharp
public sealed class GameplayComponentWorld
{
    public GameplayComponentRegistry Registry { get; }
    public RuntimeEventQueue<GameplayRuntimeEvent> Events { get; }
    public int CountAlive { get; }
    public int StoreCount { get; }
    public int PendingEventCount { get; }

    public GameplayEntityId CreateEntity();
    public bool DestroyEntity(GameplayEntityId entityId);
    public bool IsAlive(GameplayEntityId entityId);
    public GameplayEntityId[] CreateEntitySnapshot();

    public GameplayComponentStore<T> CreateStore<T>() where T : struct, IGameplayComponent;
    public GameplayComponentStore<T> GetOrCreateStore<T>() where T : struct, IGameplayComponent;
    public bool TryGetStore<T>(out GameplayComponentStore<T> store) where T : struct, IGameplayComponent;

    public void EnqueueEvent(in GameplayRuntimeEvent evt);
    public int DrainEvents(RuntimeFrame frame, List<GameplayRuntimeEvent> output);
    public GameplayComponentWorldSnapshot CreateSnapshot();
    public void Clear();
}
```

`GameplaySystemContext` 新增：

```csharp
public GameplayComponentWorld ComponentWorld { get; }
```

`GameplayRuntimeModule` 新增可选 `componentWorld` 构造参数。未传入时 module 会创建默认 `GameplayComponentWorld`，并使用它的 `Events` 作为 module event queue。

## 语义

- `GameplayComponentWorld` 是新 component runtime 的组合根。
- Component store 应通过 `GameplayComponentWorld.Registry` / `CreateStore` / `GetOrCreateStore` 创建或注册。
- `GameplayRuntimeModule.Events` 与 `GameplayRuntimeModule.ComponentWorld.Events` 指向同一个 queue。
- `GameplaySystemContext.Events` 与 `GameplaySystemContext.ComponentWorld.Events` 必须是同一个 queue；手动构造 context 时不一致会抛异常。
- `GameplayRuntimeModule` 驱动的 `GameplaySystemContext` 保证 `ComponentWorld` 非 null。手动构造 context 时，只有不访问 component runtime 的测试 / system 可以省略 `ComponentWorld`。
- `GameplayComponentWorld.Clear()` 会清空 component registry state 和 pending events，但不处理旧 `GameplayWorld` / `RuntimeEntity`；它只应用于 session reset / world reset，不应用于普通 gameplay cleanup。
- 本批次只建立访问边界，不把 Ability / Despawn 改成 component source of truth。

## 验收

- Destroy entity 会通过 registry 清理 registered component stores。
- Component world events 可 enqueue / drain，并随 `Clear()` 清空。
- Runtime module 会把 component world 传入 `GameplaySystemContext`。
- Runtime module 和 component world 共享同一个 runtime event queue。
