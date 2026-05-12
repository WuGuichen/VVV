# Gameplay ECS-style 03：V0 API Bridge

> 状态：Implemented v0（2026-05-12）

## 目标

本批次不是旧数据兼容层，也不把 `RuntimeEntity` 状态复制进 component store。目标是先补新 component runtime 需要的 lifecycle / store 注册边界，让后续 `RuntimeEntity` / `GameplayWorld` v0 API 可以逐步通过 bridge 过渡，同时避免双写 source of truth。

本批次落地：

```text
IGameplayComponentStore
GameplayComponentRegistry
DestroyEntity cleanup registered stores
```

## Public API

```csharp
public interface IGameplayComponentStore
{
    Type ComponentType { get; }
    int Count { get; }
    bool Remove(GameplayEntityId entityId);
    void Clear();
}

public sealed class GameplayComponentRegistry
{
    public GameplayEntityLifecycle Lifecycle { get; }
    public int CountAlive { get; }
    public int StoreCount { get; }

    public GameplayEntityId CreateEntity();
    public bool DestroyEntity(GameplayEntityId entityId);
    public bool IsAlive(GameplayEntityId entityId);
    public GameplayEntityId[] CreateEntitySnapshot();

    public GameplayComponentStore<T> CreateStore<T>()
        where T : struct, IGameplayComponent;
    public void RegisterStore<T>(GameplayComponentStore<T> store)
        where T : struct, IGameplayComponent;
    public bool TryGetStore<T>(out GameplayComponentStore<T> store)
        where T : struct, IGameplayComponent;
    public void Clear();
}
```

## 生命周期规则

`GameplayEntityLifecycle` 仍只负责 id 生命周期。`GameplayComponentRegistry` 是第一版 component runtime owner：

```text
CreateEntity -> lifecycle.Create
DestroyEntity -> lifecycle.Destroy -> remove entity from every registered store
Clear -> lifecycle.Clear -> clear every registered store
```

`DestroyEntity` 对 stale / invalid id 返回 false，不会清理任何 store，避免旧 id 清掉 index 复用后的新实体组件。

## Source of Truth

本批次不迁移 `RuntimeEntity.TeamId`、`AttributeStore`、`BuffPipeline` 或 `ModifierPipeline`。后续迁移某类状态时必须保持：

```text
同一类状态只能有一个 source of truth。
```

如果某类状态迁到 component store，`RuntimeEntity` 对该状态只能做 facade，不能保留第二份权威值。

## 测试

新增 `GameplayComponentRegistryTests` 覆盖：

- `DestroyEntity_RemovesComponentsFromRegisteredStores`
- `DestroyedEntity_DoesNotAppearInRegistrySnapshot`
- stale / invalid id 不清理复用后新实体组件
- duplicate component store type 拒绝
- typed store lookup
- `Clear` 同时失效 entity 并清空 registered stores

## 后续

`GAMEPLAY_ABILITY_03_COMMAND_SYSTEM` 已新增：

- `AbilityCommandSystem`
- `EntityLifecycleSystem`
- built-in command handling 从 `GameplayRuntimeModule` 迁入 system
- 真实 pipeline 顺序：PreCommand -> Command -> Simulation -> Resolution -> Diagnostics
