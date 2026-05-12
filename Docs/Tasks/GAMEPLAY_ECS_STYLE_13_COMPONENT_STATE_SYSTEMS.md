# GAMEPLAY_ECS_STYLE_13_COMPONENT_STATE_SYSTEMS

## 目标

让 component runtime 开始承担真实状态推进，先实现最小、通用、低风险的 component state systems。

本批次重点不是扩 Ability，而是验证：

```text
RuntimeHost
-> GameplayRuntimeModule
-> GameplaySystemPipeline
-> ComponentWorld
-> Component stores
-> RuntimeEventQueue / hash / SaveState
```

可以形成稳定闭环。

## 范围

建议新增：

- `GameplayLifecycleSystem`
- `GameplayDeathCleanupSystem` 或 `GameplayLifecycleCleanupSystem`
- 必要的 lifecycle event / reason 常量
- focused tests

建议使用已有组件：

- `GameplayLifecycleComponent`
- `GameplayLifecycleState`
- `GameplayComponentWorld`
- `GameplayComponentRegistry`
- `RuntimeEventQueue<GameplayRuntimeEvent>`

## 不做

本批次不要做：

- Ability cooldown / cost / cast time
- Combat damage bridge
- AI behavior state
- RuntimeEntity / GameplayWorld 迁移
- 大型 ECS query DSL
- 自动反射系统发现

## 推荐语义

### Lifecycle states

已有 `GameplayLifecycleComponent` 表达：

```text
None
Alive
PendingDestroy
Destroyed
```

第 13 批应明确：

- `Alive` 表示 entity 参与正常 component systems。
- `PendingDestroy` 表示本帧或后续 cleanup phase 会统一销毁。
- `Destroyed` 不应长期保留在 alive entity 的 component store 中；真正销毁应走 `GameplayComponentWorld.DestroyEntity(id)`。

### Cleanup phase

推荐 cleanup system 放在：

```text
GameplaySystemPhase.Resolution
```

这样 command systems / simulation systems 可以先把 entity 标记为 `PendingDestroy`，resolution phase 再统一清理，避免同一 phase 内遍历 store 时边遍历边删除。

## 建议 API

```csharp
public sealed class GameplayLifecycleCleanupSystem : IGameplaySystem
{
    public const string DefaultSystemId = "mxframework.gameplay.lifecycle.cleanup";

    public string SystemId { get; }
    public GameplaySystemPhase Phase { get; }
    public int Priority { get; }
    public bool IsEnabled { get; }

    public void Tick(GameplaySystemContext context);
}
```

可选辅助：

```csharp
public static class GameplayLifecycleEvents
{
    public const string PendingDestroyCleanupReason = "PendingDestroyCleanup";
}
```

## 执行规则

`GameplayLifecycleCleanupSystem.Tick(context)`：

1. 从 `context.ComponentWorld` 获取 `GameplayComponentStore<GameplayLifecycleComponent>`。
2. Snapshot lifecycle entries，避免遍历时修改 store。
3. 找出 `State == PendingDestroy` 的 entity。
4. 按 `GameplayEntityId` 稳定顺序 destroy entity。
5. Destroy 成功后，registered component stores 必须被清理。
6. 输出 `GameplayRuntimeEventType.ComponentEntityDestroyed` 或 `CommandRejected` 风格结构化事件。
7. 事件 reason 使用稳定字符串，例如 `PendingDestroyCleanup`。

如果 `context.ComponentWorld == null`：

- 与 component entity command system 保持一致，输出结构化 setup issue 或抛可诊断异常。
- 不允许空引用异常。

## 与 DestroyComponentEntity command 的关系

`DestroyComponentEntity` command 是显式外部命令：

```text
Input / AI / Timer / Gameplay command
-> DestroyComponentEntity
-> GameplayComponentEntityCommandSystem
-> ComponentWorld.DestroyEntity
```

Lifecycle cleanup 是状态驱动：

```text
Simulation system marks PendingDestroy
-> Resolution cleanup system
-> ComponentWorld.DestroyEntity
```

两者都必须最终走 `GameplayComponentWorld.DestroyEntity`，不能绕过 `GameplayComponentRegistry`，否则 registered store cleanup 会漏掉。

## Event 边界

推荐复用现有 event：

```text
GameplayRuntimeEventType.ComponentEntityDestroyed
```

并通过 reason 区分来源：

```text
DestroyComponentEntity
PendingDestroyCleanup
```

如果后续需要更丰富的 lifecycle event，可以另加 event type，但本批次优先复用已有事件，避免扩大 runtime event surface。

## Hash / SaveState 关系

本批次需要验证：

- cleanup 前后 `GameplayComponentWorldHashContributor` hash 会稳定变化。
- SaveState restore 后，`PendingDestroy` entity 仍能被 cleanup system 清理。
- cleanup 不直接读写 SaveState DTO。

如果 `GAMEPLAY_ECS_STYLE_12_COMPONENT_SAVE_STATE` 尚未完全实现，可先保留 SaveState 测试为后续补充项，但文档要说明依赖。

## 测试要求

至少新增：

- `LifecycleCleanupSystem_DestroysPendingDestroyEntities`
- `LifecycleCleanupSystem_RemovesComponentsFromAllRegisteredStores`
- `LifecycleCleanupSystem_EmitsDestroyedEventsInStableOrder`
- `LifecycleCleanupSystem_IgnoresAliveAndDestroyedStates`
- `LifecycleCleanupSystem_HandlesMissingComponentWorldDiagnostically`
- `LifecycleCleanupSystem_DoesNotThrowWhenLifecycleStoreMissing`

如果 SaveState 已完成，额外新增：

- `LifecycleCleanupSystem_RestoredPendingDestroyEntityCanBeCleanedUp`

如果 ComponentWorld hash 已完成，额外新增：

- `LifecycleCleanupSystem_ChangesComponentWorldHashAfterCleanup`

## 默认 pipeline 接入

本批次可以先不自动加入 `GameplayRuntimeModule.CreateDefaultSystemPipeline`，避免改变现有 runtime 行为。

建议先提供两种接入方式：

```csharp
new GameplayRuntimeModule(..., configureDefaultPipeline: pipeline =>
{
    pipeline.Add(new GameplayLifecycleCleanupSystem());
});
```

后续如果 Demo / real slice 验证稳定，再考虑加入 default pipeline。

## 验收

- PendingDestroy entity 能在 Resolution phase 被统一销毁。
- Destroy 通过 `GameplayComponentWorld.DestroyEntity`，registered stores 被清理。
- 同帧多个 pending destroy 按 `GameplayEntityId` 稳定顺序处理。
- Cleanup 输出可诊断 runtime event。
- Alive / Destroyed / missing lifecycle store 不产生误删。
- 不迁移 `RuntimeEntity` / `GameplayWorld`。
- 文档和 `Docs/Interfaces/Gameplay.md` 同步新增 system 语义。
