# Gameplay ECS-style 04：Core Components

> 状态：Implemented v0（2026-05-12）

## 目标

在 generation entity id、component store、system pipeline 和 v0 API bridge 之后，补第一组纯 gameplay 数据组件：

```text
GameplayIdentityComponent
GameplayTeamComponent
GameplayLifecycleComponent / GameplayLifecycleState
GameplayTagComponent
GameplayStatusComponent
```

本批次只定义新 component runtime 的数据面，不把 `RuntimeEntity` 的状态复制进 component store，不建立双写 source of truth。

## Public API

```csharp
public readonly struct GameplayIdentityComponent : IGameplayComponent
{
    public int DefinitionId { get; }
    public int VariantId { get; }
}

public readonly struct GameplayTeamComponent : IGameplayComponent
{
    public int TeamId { get; }
    public bool IsNeutral { get; }
    public GameplayTeamRelation RelationTo(GameplayTeamComponent other);
}

public enum GameplayLifecycleState
{
    None,
    Alive,
    PendingDestroy,
    Destroyed
}

public readonly struct GameplayLifecycleComponent : IGameplayComponent
{
    public GameplayLifecycleState State { get; }
    public bool IsAlive { get; }
    public bool IsTerminal { get; }
}

public readonly struct GameplayTagComponent : IGameplayComponent
{
    public int Count { get; }
    public bool Contains(GameplayTagId id);
    public GameplayTagId[] ToArray();
}

public readonly struct GameplayStatusComponent : IGameplayComponent
{
    public int Count { get; }
    public bool Contains(GameplayStatusId id);
    public GameplayStatusId[] ToArray();
}
```

`GameplayComponentRegistry` 同步新增：

```csharp
public GameplayComponentStore<T> GetOrCreateStore<T>()
    where T : struct, IGameplayComponent;
```

## 约束

- Component 仍是 `struct, IGameplayComponent`，不引用 Unity / Editor / Demo / WGame 私有数据。
- `GameplayIdentityComponent` 只表达 definition / variant 这类配置身份，不替代 generation entity id。
- `GameplayTeamComponent` 复用 `GameplayTeamRelations`，不引入项目阵营文案。
- `GameplayLifecycleComponent` 是 component runtime 状态，不替代 `GameplayEntityLifecycle` 的 generation id 生命周期。
- `GameplayTagComponent` / `GameplayStatusComponent` 构造时过滤 invalid id、排序、去重并复制数组；`ToArray()` 返回副本，避免外部修改破坏稳定 snapshot。
- 本批次不迁移 `RuntimeEntity.TeamId`、`GameplayTagSet`、`GameplayStatusSet` 或 `AttributeStore`。

## 测试

新增 `GameplayCoreComponentTests` 覆盖：

- identity component 参数校验和 default none。
- team relation 与 neutral 判定。
- lifecycle common states。
- tag / status component 过滤 invalid、排序、去重和防外部数组修改。
- `GameplayComponentRegistry.GetOrCreateStore<T>()` 返回已有 store。

## 后续

下一步可以继续补：

- `AttributeComponent` / `AbilityComponent` / `BuffComponent` / `ModifierComponent` 的 source-of-truth 迁移设计。
- 基于 core components 的 query helper。
- `EntityLifecycleSystem` 针对 `GameplayComponentRegistry` 的 command 版本。
