# GAMEPLAY_ECS_STYLE_14_COMPONENT_SPAWN_DEFINITIONS

## 目标

让 component runtime 支持“按显式 spawn definition 创建带初始组件的 entity”，把当前空的 `CreateComponentEntity` 能力推进到可用于真实玩法对象生成。

本批次重点是建立稳定、可诊断、可回放的 spawn 入口：

```text
RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplaySystemPipeline
-> GameplayComponentSpawnCommandSystem
-> GameplayComponentWorld
-> component stores
-> RuntimeEventQueue / hash / SaveState
```

## 背景

当前 component runtime 已有：

- generation-safe `GameplayEntityId`
- `GameplayComponentWorld.CreateEntity()` / `DestroyEntity()`
- `CreateComponentEntity` / `DestroyComponentEntity` command
- core components、schema registry、diagnostics、hash、SaveState
- `GameplayLifecycleCleanupSystem`

但 `CreateComponentEntity` 只创建空 entity，不能同时写入 `Identity`、`Team`、`Lifecycle`、`Tag`、`Status` 等初始组件。

如果后续 Ability、Combat、AI、UI 都各自手写：

```text
CreateEntity()
GetStore<T>().Set(...)
```

会导致初始化顺序、默认 lifecycle、诊断 reason、SaveState/hash 语义分散。第 14 批应先把 spawn 入口收拢到一个明确 contract。

## 范围

建议新增：

- `GameplayComponentSpawnDefinition`
- `GameplayComponentSpawnRegistry`
- `IGameplayComponentSpawnInitializer`
- `GameplayComponentSpawnCommandSystem`
- `SpawnComponentEntity` command id / factory
- spawn event / reason 常量
- focused tests

建议复用：

- `GameplayComponentWorld`
- `GameplayComponentRegistry`
- `GameplayComponentStore<T>`
- `GameplayComponentSchemaRegistry`
- `GameplayRuntimeCommandBuffer`
- `GameplayRuntimeEvent`

## 不做

本批次不要做：

- 配置文件加载 / Addressables / Luban 接入
- Ability cast / cooldown / cost / cast time
- Combat actor bridge
- AI blackboard spawn
- Prefab / Unity GameObject 实例化
- 自动反射扫描 component initializer
- 复杂 archetype / chunk / query DSL

## 设计原则

### Definition 是显式注册对象

Spawn definition 必须由组合根或测试显式注册，不做程序集扫描。

推荐第一版：

```csharp
public sealed class GameplayComponentSpawnDefinition
{
    public int DefinitionId { get; }
    public string StableId { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<IGameplayComponentSpawnInitializer> Initializers { get; }
}
```

规则：

- `DefinitionId` 是 command payload 使用的稳定数字 id。
- `StableId` 是 diagnostics / authoring / future config 的稳定 dotted id。
- `SchemaVersion` 用于后续 definition payload migration。
- `Initializers` 顺序必须稳定。

### Initializer 只负责写初始 component

推荐接口：

```csharp
public interface IGameplayComponentSpawnInitializer
{
    string SchemaId { get; }

    RuntimeSaveStateResult<bool> Apply(
        GameplayComponentWorld world,
        GameplayEntityId entityId,
        GameplayComponentSpawnContext context);
}
```

也可以先不用 `RuntimeSaveStateResult<bool>`，但必须有结构化失败信息，不能只返回 `false`。

推荐提供 typed initializer：

```csharp
public sealed class GameplayComponentSpawnInitializer<T> :
    IGameplayComponentSpawnInitializer
    where T : struct, IGameplayComponent
{
    public GameplayComponentSpawnInitializer(string schemaId, T component);
}
```

`Apply()` 内部只做：

```text
world.GetOrCreateStore<T>().Set(entityId, component)
```

不要在 initializer 中读取 Unity object、随机数、时间、全局单例或外部 mutable state。

### Spawn command 不塞完整 component payload

新增 command：

```csharp
public const int SpawnComponentEntity = 1001005;
```

推荐 factory：

```csharp
public static RuntimeCommand SpawnComponentEntity(
    RuntimeFrame frame,
    int spawnDefinitionId,
    int variantId = 0,
    int sourceId = 0,
    string traceId = "");
```

payload 建议：

```text
targetId = spawnDefinitionId
payload0 = spawnDefinitionId
payload1 = variantId
payload2 = reserved
```

本批次不要把 component 字段塞进 `payload0/1/2`。复杂参数后续通过：

- definition registry
- command registry schema
- config id
- explicit spawn request store

逐步扩展。

## 建议 API

### Registry

```csharp
public sealed class GameplayComponentSpawnRegistry
{
    public void Register(GameplayComponentSpawnDefinition definition);
    public bool TryGet(int definitionId, out GameplayComponentSpawnDefinition definition);
    public GameplayComponentSpawnDefinition[] CreateSnapshot();
    public void Clear();
}
```

规则：

- duplicate `DefinitionId` 必须抛错。
- duplicate `StableId` 必须抛错。
- `StableId` 使用小写 dotted id，至少拒绝空白和前后空格。
- snapshot 按 `DefinitionId` 稳定排序。
- registry 不拥有 `GameplayComponentWorld`，只保存 definition。

### Spawn Context

```csharp
public readonly struct GameplayComponentSpawnContext
{
    public RuntimeFrame Frame { get; }
    public RuntimeCommand Command { get; }
    public GameplayComponentSpawnDefinition Definition { get; }
    public int VariantId { get; }
}
```

Context 只传递本次 spawn 的稳定输入，不暴露 command buffer。

### Command System

```csharp
public sealed class GameplayComponentSpawnCommandSystem : IGameplaySystem
{
    public const string DefaultSystemId = "mxframework.gameplay.command.component_spawn";

    public GameplayComponentSpawnCommandSystem(
        GameplayComponentSpawnRegistry spawnRegistry,
        string systemId = DefaultSystemId,
        int priority = 30);

    public void Tick(GameplaySystemContext context);
}
```

推荐 phase：

```text
GameplaySystemPhase.Command
```

推荐 priority：

```text
after GameplayComponentEntityCommandSystem
before GameplayUnsupportedCommandSystem
```

## 执行规则

`GameplayComponentSpawnCommandSystem.Tick(context)`：

1. 遍历 `context.Commands`。
2. 只处理 `SpawnComponentEntity`。
3. 校验 `context.ComponentWorld != null`。
4. 按 `payload0` 查 `GameplayComponentSpawnRegistry`。
5. 校验 definition initializer 非空或允许空 definition 的策略。
6. 调用 `context.ComponentWorld.CreateEntity()`。
7. 按 definition initializer 顺序写入初始 components。
8. 如果任一 initializer 失败：
   - 调用 `context.ComponentWorld.DestroyEntity(entityId)` 回滚已创建 entity。
   - 输出 `CommandRejected` event。
   - reason 使用稳定字符串。
9. 成功后输出 `ComponentEntityCreated` 或新增 `ComponentEntitySpawned` event。
10. `context.CommandState.MarkHandled(command)`。

第一版建议复用：

```text
GameplayRuntimeEventType.ComponentEntityCreated
```

并通过 reason 区分：

```text
SpawnComponentEntity
```

如果后续 UI / Diagnostics 需要区分“空创建”和“按 definition spawn”，再新增 event type。

## Reason 常量

建议新增：

```csharp
public static class GameplayComponentSpawnEvents
{
    public const string SpawnedReason = "SpawnComponentEntity";
    public const string MissingComponentWorldReason = "MissingComponentWorld";
    public const string MissingSpawnRegistryReason = "MissingSpawnRegistry";
    public const string MissingSpawnDefinitionReason = "MissingSpawnDefinition";
    public const string InvalidSpawnDefinitionReason = "InvalidSpawnDefinition";
    public const string SpawnInitializerFailedReason = "SpawnInitializerFailed";
}
```

如果已有 `MissingComponentWorld` reason，可复用同一个字符串，避免 reason 分裂。

## Core Definition 示例

测试或 demo 可先注册 code-defined definition：

```csharp
var definition = new GameplayComponentSpawnDefinition(
    definitionId: 1001,
    stableId: "mxframework.gameplay.test.actor",
    schemaVersion: 1,
    initializers: new IGameplayComponentSpawnInitializer[]
    {
        new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
            "mxframework.gameplay.identity",
            new GameplayIdentityComponent(definitionId: 1001, variantId: 0)),
        new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
            "mxframework.gameplay.team",
            new GameplayTeamComponent(teamId: 1)),
        new GameplayComponentSpawnInitializer<GameplayLifecycleComponent>(
            "mxframework.gameplay.lifecycle",
            GameplayLifecycleComponent.Alive)
    });
```

本批次不要求从 JSON / asset / config 创建 definition。

## Hash / SaveState 关系

Spawn definition 本身不是 world state。

成功 spawn 后，world state 只由实际 entity 和 component stores 表达：

- hash 写入创建出的 entity 和 component value。
- SaveState 捕获创建后的 entity/component state。
- Restore 不需要重新执行 spawn definition。

因此：

```text
Spawn command 是输入事件。
Spawn result 是 component world state。
SaveState 保存 result，不保存 command。
```

测试应验证：

- spawn 后 ComponentWorld hash 改变。
- SaveState roundtrip 后 world 中仍有同样 entity/components。
- Restore 不依赖 spawn registry 中仍然存在该 definition。

如果第 12 批 SaveState 当前尚未合并到主线，可把 SaveState 测试列为 follow-up，但文档要保留这个原则。

## 与 CreateComponentEntity 的关系

保留 `CreateComponentEntity`：

- 用于测试、工具、极低层 runtime API。
- 只创建空 entity。
- 不负责初始组件。

新增 `SpawnComponentEntity`：

- 用于玩法对象创建。
- 通过 definition 初始化 components。
- 后续 Ability / Combat / AI / UI 应优先用 spawn command，而不是手动拼多个 store set。

## 测试要求

至少新增：

- `SpawnRegistry_RejectsDuplicateDefinitionId`
- `SpawnRegistry_RejectsDuplicateStableId`
- `SpawnRegistry_CreateSnapshotReturnsStableOrder`
- `SpawnCommand_CreatesEntityWithInitialComponents`
- `SpawnCommand_EmitsCreatedEventWithComponentEntityId`
- `SpawnCommand_RejectsMissingDefinition`
- `SpawnCommand_RejectsMissingComponentWorldDiagnostically`
- `SpawnCommand_RollsBackEntityWhenInitializerFails`
- `SpawnCommand_IsHandledBeforeUnsupportedSystem`

如果 hash 已完成，额外新增：

- `SpawnCommand_ChangesComponentWorldHash`

如果 SaveState 已完成，额外新增：

- `SpawnCommand_SaveStateRoundtripPreservesSpawnedComponents`
- `SpawnCommand_RestoreDoesNotRequireSpawnDefinitionRegistry`

## 默认 pipeline 接入

本批次可以先不自动加入 `GameplayRuntimeModule.CreateDefaultSystemPipeline`，因为 spawn registry 由项目组合根提供。

推荐接入方式：

```csharp
var spawnRegistry = new GameplayComponentSpawnRegistry();
spawnRegistry.Register(definition);

var module = new GameplayRuntimeModule(..., configureDefaultPipeline: pipeline =>
{
    pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
});
```

如果后续 demo 已稳定使用 spawn registry，再考虑提供：

```csharp
GameplayRuntimeModule.CreateDefaultSystemPipeline(..., spawnRegistry)
```

但不要让 module 隐式创建空 registry 后吞掉 missing definition 问题。

## 验收

- 可以通过 `RuntimeCommandBuffer` 创建带初始 components 的 component entity。
- Spawn definition 注册、查询和 snapshot 顺序稳定。
- Spawn 不使用反射、不直接序列化泛型 store、不依赖 `Type.FullName`。
- Spawn command payload 只携带 definition/variant 等稳定 id，不携带任意 component 字段。
- 初始化失败会回滚新建 entity，不留下半初始化 component state。
- 成功 / 失败都输出可诊断 runtime event。
- 成功 spawn 后 hash / SaveState 只依赖实际 component world state。
- 不迁移 `RuntimeEntity` / `GameplayWorld`。
- 文档和 `Docs/Interfaces/Gameplay.md` 同步新增 spawn definition 语义。
