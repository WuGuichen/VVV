# Gameplay ECS-style 01：Component Store

> 状态：Implemented v0（2026-05-11）

## 目标

建立 Gameplay ECS-style runtime 的第一层数据底座：

```text
GameplayEntityId / generation
GameplayEntityLifecycle
IGameplayComponent
GameplayComponentStore<T>
stable query / snapshot
```

本批次遵守 `GAMEPLAY_ECS_STYLE_00_DESIGN_CONTRACT`：当前无旧数据兼容目标，`RuntimeEntity` 只是 v0 API bridge，新 component store 不接受裸 int entity id 作为 key。

## Public API

```csharp
public readonly struct GameplayEntityId
{
    public int Index { get; }
    public int Generation { get; }
    public bool IsValid { get; }
}

public sealed class GameplayEntityLifecycle
{
    public int CountAlive { get; }
    public int CountAllocated { get; }
    public GameplayEntityId Create();
    public bool Destroy(GameplayEntityId entityId);
    public bool IsAlive(GameplayEntityId entityId);
    public GameplayEntityId[] CreateSnapshot();
    public void Clear();
}

public interface IGameplayComponent
{
}

public sealed class GameplayComponentStore<T>
    where T : struct, IGameplayComponent
{
    public int Count { get; }
    public bool TryAdd(GameplayEntityId entityId, T component);
    public void Set(GameplayEntityId entityId, T component);
    public bool Contains(GameplayEntityId entityId);
    public bool TryGet(GameplayEntityId entityId, out T component);
    public bool Remove(GameplayEntityId entityId);
    public int CopyTo(List<GameplayComponentSnapshot<T>> output);
    public GameplayComponentSnapshot<T>[] CreateSnapshot();
    public void Clear();
}
```

## 约束

- `GameplayEntityId` 使用 `Index + Generation`，`default` 表示 invalid。
- `GameplayEntityLifecycle.Destroy` 会推进 generation，旧 handle 不会命中新实体。
- `GameplayEntityLifecycle` 只负责 id 生命周期，不负责 component cleanup。
- `GameplayComponentStore<T>` 只接受 `GameplayEntityId`，没有裸 int key API。
- `GameplayComponentStore<T>.Set` 是 upsert：component 不存在时新增，存在时覆盖。
- Component 第一版约束为 `struct, IGameplayComponent`，避免组件对象携带行为和引用生命周期。
- Snapshot / CopyTo 按 `GameplayEntityId.Index`、`Generation` 稳定排序。
- 本批次不迁移 `RuntimeEntity` 状态，不建立双写 source of truth。

## 测试

新增 `GameplayComponentStoreTests` 覆盖：

- `GameplayEntityId` default invalid、参数校验和稳定排序。
- `GameplayEntityLifecycle` 创建、销毁、index 复用、generation 更新、stale id 失效。
- lifecycle snapshot 稳定排序，`Clear` 后旧 id 失效。
- component store add/get/set/remove/clear。
- store 拒绝 invalid entity id。
- store snapshot / copy 稳定排序。

## 后续

下一批 `GAMEPLAY_ECS_STYLE_02_SYSTEM_PIPELINE` 会在该 component store 之上补：

- `IGameplaySystem`
- `GameplaySystemPhase`
- `GameplaySystemContext`
- `GameplaySystemPipeline`
- RuntimeCommandBuffer 仍由 `GameplayRuntimeModule` 单点 drain
- World / ComponentRegistry 在 destroy entity 时统一移除所有 registered component store 中该 entity 的组件

下一批或 v0 API bridge 批次需要补测试：

- `DestroyEntity_RemovesComponentsFromRegisteredStores`
- `DestroyedEntity_DoesNotAppearInWorldComponentSnapshot`
