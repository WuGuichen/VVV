# GAMEPLAY_ECS_STYLE_07_ENTITY_COMMANDS

## 目标

给新 ECS-style component runtime 增加第一批 entity lifecycle commands，让 `RuntimeCommandBuffer -> GameplayRuntimeModule -> GameplaySystemPipeline` 可以驱动 `GameplayComponentWorld` 创建 / 销毁 generation entity。

本批次不迁移旧 `DespawnEntity`，旧命令仍由 `GameplayEntityLifecycleCommandSystem` 处理 `GameplayWorld` / `RuntimeEntity`。

## 新增 command

```csharp
GameplayRuntimeCommandIds.CreateComponentEntity
GameplayRuntimeCommandIds.DestroyComponentEntity

GameplayRuntimeCommandFactory.CreateComponentEntity(...)
GameplayRuntimeCommandFactory.DestroyComponentEntity(RuntimeFrame frame, GameplayEntityId entityId, ...)
```

`DestroyComponentEntity` 把 `GameplayEntityId.Index` 写入 `targetId/payload0`，把 `Generation` 写入 `payload1`。System 读取 index + generation，防止 stale entity id 销毁复用后的新 entity。

## 新增 system

```csharp
public sealed class GameplayComponentEntityCommandSystem : IGameplaySystem
{
    public const string DefaultSystemId = "mxframework.gameplay.command.component_entity";
    public const string MissingComponentWorldReason = "MissingComponentWorld";
    public const string InvalidEntityReason = "InvalidComponentEntity";
    public const string MissingEntityReason = "MissingComponentEntity";
}
```

Default `GameplayRuntimeModule` pipeline 现在包含：

```text
GameplayAbilityCommandSystem
GameplayEntityLifecycleCommandSystem
GameplayComponentEntityCommandSystem
GameplayUnsupportedCommandSystem
```

## Event

`GameplayRuntimeEventType` 新增：

```csharp
ComponentEntityCreated
ComponentEntityDestroyed
```

`GameplayRuntimeEvent` 新增：

```csharp
int ComponentEntityIndex
int ComponentEntityGeneration
GameplayEntityId ComponentEntityId
bool TryGetComponentEntityId(out GameplayEntityId entityId)
```

旧 `TargetEntityId` 仍保留给 v0 `RuntimeEntity` / Ability 事件。Component entity event 用 `ComponentEntityId` 作为 generation-safe id。

`GameplayRuntimeEvent` 构造函数会校验 component entity index / generation 必须同时为 `0/0` 或同时大于 0。需要安全读取时优先使用 `TryGetComponentEntityId`。

## 语义

- Create command 创建新的 `GameplayEntityId`，并输出 `ComponentEntityCreated`。
- Destroy command 只销毁 alive 且 generation 匹配的 component entity。
- 缺少 `GameplaySystemContext.ComponentWorld` 时输出 `CommandRejected / MissingComponentWorld`。
- Destroy stale / missing entity 输出 `CommandRejected / MissingComponentEntity`。
- Destroy invalid payload 输出 `CommandRejected / InvalidComponentEntity`。
- Component entity command system 处理后必须 mark handled，避免 unsupported system 再次拒绝。

## 验收

- Create command 通过 default module 创建 component entity。
- Destroy command 通过 default module 销毁 component entity，并清理 registered components。
- 缺少 ComponentWorld 时输出结构化 rejected event，不抛出 NRE。
- Stale / invalid destroy command 输出结构化 rejected event。
- Runtime event 拒绝半合法 component entity id，并可通过 `TryGetComponentEntityId` 安全读取。
- Component entity commands 不触发 unsupported rejected event。
