# Gameplay World 01A：World Contract

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`GAMEPLAY_WORLD_01_V0_FOUNDATION.md`

## 目标

建立 `GameplayWorld` v0 和实体注册表，让现有 `IRuntimeEntity` 可以进入世界级生命周期和查询入口。01A 只处理 world / registry / lifecycle / tick，不做 tag/status、复杂 targeting 或 ability service。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/GameplayWorld*.cs`
- `Assets/Scripts/MxFramework/Gameplay/RuntimeEntityRegistry*.cs`
- `Assets/Scripts/MxFramework/Tests/Ability/GameplayWorldContractTests.cs`
- 对应 `.meta`

不要修改 `RuntimeEntity.cs`、`IRuntimeEntity.cs`、`SimpleAbility.cs` 或 asmdef。

## 建议 API

```csharp
public sealed class GameplayWorld
{
    public RuntimeEntityRegistry Entities { get; }
    public long TickCount { get; }
    public void Register(IRuntimeEntity entity);
    public bool Remove(int entityId);
    public void Tick(double deltaTime);
    public GameplayWorldSnapshot CreateSnapshot();
}
```

`RuntimeEntityRegistry` 至少支持：

- 重复 entity id 拒绝。
- `TryGet(entityId, out IRuntimeEntity entity)`。
- 按 `EntityId` 稳定排序枚举。
- 移除不存在实体返回 false。

## 规则

- 不读取 Unity 时间，tick 的 `deltaTime` 由外层传入。
- Registry 不拥有实体内存释放，只负责注册关系。
- 枚举顺序必须稳定，不依赖 Dictionary 原始顺序。
- 错误必须可诊断：null entity、重复 id、非法 id 都有明确异常或结果。

## 测试

至少覆盖：

- 注册、查询、移除实体。
- 重复 id 被拒绝。
- 枚举顺序按 entity id 稳定。
- TickCount 随 `Tick()` 前进。
- Snapshot 不受后续 registry 变化影响。

## 验收

- GameplayWorld v0 可由纯 C# 测试驱动。
- 不引入 Runtime / Unity 依赖。
- 不破坏现有 Ability slice tests。

## 2026-05-10 实现记录

- 新增 `RuntimeEntityRegistry`：注册、查询、移除、按 `EntityId` 稳定排序枚举和 snapshot copy。
- 新增 `GameplayWorld`：持有 registry、`TickCount`、注册/移除代理、稳定 tick 实体 `BuffPipeline`。
- 新增 `GameplayWorldSnapshot`：保存 tick 和实体 snapshot copy。
- 新增 `GameplayWorldContractTests` 覆盖注册、查询、移除、重复/非法 id、稳定排序、tick 和 snapshot copy。
