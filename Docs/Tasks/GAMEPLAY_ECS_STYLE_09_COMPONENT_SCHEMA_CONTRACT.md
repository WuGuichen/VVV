# GAMEPLAY_ECS_STYLE_09_COMPONENT_SCHEMA_CONTRACT

## 目标

在实现 Component Runtime Hash / SaveState 之前，先固定 component value 的 schema 契约。

本批次只定义注册、诊断、hash 和保存恢复的边界，不实现真正的 component value 序列化，也不把 `GameplayComponentStore<T>` 的泛型内部结构直接写进 hash/save。

核心目标：

- Component value 必须通过显式 schema 注册后，才能参与 value diagnostics、runtime hash 或 SaveState。
- 泛型 store 只负责存储和稳定 snapshot，不负责解释组件字段。
- Hash / SaveState 不依赖 `typeof(T).FullName`、反射字段顺序、`GetHashCode()`、JSON 自动序列化顺序或 `Dictionary` / `HashSet` 原始迭代顺序。
- 诊断、hash、save 可以分阶段接入；一个 component 可以只支持 diagnostics，不支持 hash/save。

## 建议 API

后续代码实现可以按现有风格调整命名，但必须保留以下语义。

```csharp
public readonly struct GameplayComponentSchema
{
    public string StableId { get; }
    public int Version { get; }
    public Type ComponentType { get; }
    public string DisplayName { get; }
    public bool SupportsDiagnostics { get; }
    public bool SupportsHash { get; }
    public bool SupportsSaveState { get; }
}

public interface IGameplayComponentSchemaDescriptor
{
    GameplayComponentSchema Schema { get; }
}

public interface IGameplayComponentDiagnosticWriter<T> : IGameplayComponentSchemaDescriptor
    where T : struct, IGameplayComponent
{
    void WriteDiagnostics(GameplayEntityId entityId, in T component, GameplayComponentDiagnosticWriter writer);
}

public interface IGameplayComponentHashWriter<T> : IGameplayComponentSchemaDescriptor
    where T : struct, IGameplayComponent
{
    void WriteHash(GameplayEntityId entityId, in T component, RuntimeHashAccumulator accumulator);
}

public interface IGameplayComponentSaveStateAdapter<T> : IGameplayComponentSchemaDescriptor
    where T : struct, IGameplayComponent
{
    void WriteSaveState(GameplayEntityId entityId, in T component, GameplayComponentSaveStateWriter writer);
    bool TryReadSaveState(GameplayComponentSaveStateReader reader, out GameplayEntityId entityId, out T component);
}

public sealed class GameplayComponentSchemaRegistry
{
    public void Register(IGameplayComponentSchemaDescriptor descriptor);
    public bool TryGetByStableId(string stableId, out GameplayComponentSchema schema);
    public bool TryGetByType(Type componentType, out GameplayComponentSchema schema);
    public bool TryGetDiagnosticWriter<T>(out IGameplayComponentDiagnosticWriter<T> writer)
        where T : struct, IGameplayComponent;
    public bool TryGetHashWriter<T>(out IGameplayComponentHashWriter<T> writer)
        where T : struct, IGameplayComponent;
    public bool TryGetSaveStateAdapter<T>(out IGameplayComponentSaveStateAdapter<T> adapter)
        where T : struct, IGameplayComponent;
    public GameplayComponentSchema[] CreateSnapshot();
}
```

`GameplayComponentWorld` 后续可以持有一个 `GameplayComponentSchemaRegistry`，作为 component runtime 的 metadata 入口。但 schema registry 不应自动扫描程序集，也不应通过反射推断 field schema；组合根或模块显式注册 descriptor。

同一 component schema 在 registry 中只能有一个 schema owner。Diagnostics、hash、SaveState capability 可以由同一个 descriptor 同时实现多个接口，也可以由 registry 在同一个 schema entry 下挂载多个 capability adapter；但不得以多个 schema entry 重复注册同一个 `StableId` 或 `ComponentType`。

`CreateSnapshot()` 只暴露 schema metadata，用于 UI、Editor、测试和 agent 观察 registry 状态。真正执行 diagnostics / hash / SaveState 时，executor 必须通过 registry 解析对应 capability adapter，例如 diagnostic writer、hash writer 或 save adapter，而不是只拿 metadata 后自行反射 component value。

## StableId 规则

`StableId` 是 SaveState / ReplayHash / Diagnostics 的权威 component type id。

规则：

- 必须非空。
- 必须长期稳定，不随 C# namespace、类名、文件路径或程序集名变化而变化。
- 建议使用小写 dotted id，例如 `mxframework.gameplay.identity`。
- 同一个 registry 内 `StableId` 不能重复。
- 同一个 component `Type` 不能重复注册到两个 schema。
- `StableId` 变更必须通过迁移处理，不能只改 descriptor。

`ComponentType.FullName` 只能用于 Editor / Diagnostics 显示和未注册 store 的摘要，不得作为 SaveState / Hash 的权威 key。

## Version 规则

`Version` 表示单个 component schema 的 payload 版本，不等同于全局 SaveState schema version。

规则：

- 第一版为 `1`。
- 删除字段、改变字段含义、改变单位或枚举值含义，必须提升 version 并提供 migration / read adapter。
- Restore 时遇到不支持的 component schema version，必须返回结构化错误，不能静默跳过。

## Diagnostics 契约

Diagnostics 用于 UI、Editor、测试和 agent 观察 component value，不是权威保存格式。

规则：

- Diagnostics writer 必须按固定字段顺序输出。
- 字段 key 必须稳定，不使用本地化文本。
- 集合字段必须先排序或由组件类型保证稳定顺序。
- 可以输出摘要字段，例如 count、state、ids；不要求完整保存 payload。
- 未注册 schema 的 component store 只能输出 store type/count 摘要，不能尝试反射展开 value。

示例字段顺序：

```text
schemaId
schemaVersion
entity.index
entity.generation
field.definitionId
field.variantId
```

## Hash 契约

Component runtime hash 只接受显式注册了 hash writer 的 component。

排序规则：

1. 按 `GameplayEntityLifecycle.CreateSnapshot()` 输出 alive entity 顺序遍历 entity。
2. 对每个 entity，按 component schema `StableId` 升序遍历已注册并支持 hash 的 component schema。
3. 对每个存在的 component，先写 `StableId`、`Version`、`EntityId.Index`、`EntityId.Generation`，再由 hash writer 写字段。

Hash writer 规则：

- 必须显式写入 field key 和 value。
- `float` / `double` 必须量化，不能依赖平台字符串格式。
- 禁止写入对象地址、默认对象 `GetHashCode()`、Unity instance id、系统时间、本地化文本。
- 禁止直接遍历 `Dictionary` / `HashSet` 原始顺序；必须排序后写入。
- 未注册或未声明 `SupportsHash` 的 component 不参与 hash，并应在 diagnostics 中可见。

## SaveState 契约

Component SaveState 不直接序列化 `GameplayComponentStore<T>`。

建议保存形态：

```text
componentStores[]
  schemaId
  schemaVersion
  entries[]
    entityIndex
    entityGeneration
    payload
```

规则：

- SaveState provider 按 schema `StableId` 升序输出 component stores。
- 每个 store 的 entries 按 `GameplayEntityId` 稳定顺序输出。
- Payload 由 component 的 save adapter 写入结构化字段。
- Restore 先恢复 entity lifecycle，再按 schema adapter 恢复 component value。
- Restore 遇到 missing schema、duplicate schema、unsupported version、invalid entity id、payload parse failure 时必须返回结构化错误。
- 不支持 save 的 component 默认不写入 SaveState；如果它是权威状态，必须补 save adapter 后才能接入生产 SaveState。

## 注册流程

推荐组合根流程：

```text
Create GameplayComponentWorld
Create GameplayComponentSchemaRegistry
Register core component schemas
Register game/module component schemas
Create GameplayRuntimeModule
Run systems
Diagnostics / Hash / SaveState 通过 schema registry 解释 component value
```

系统或 Demo 不应在 Tick 中临时注册 schema。Schema 注册属于组合根 / boot 阶段。

## Core Component 首批 schema

后续实现 registry 时，框架应优先为当前 core components 提供 descriptor：

- `GameplayIdentityComponent` -> `mxframework.gameplay.identity`
- `GameplayTeamComponent` -> `mxframework.gameplay.team`
- `GameplayLifecycleComponent` -> `mxframework.gameplay.lifecycle`
- `GameplayTagComponent` -> `mxframework.gameplay.tags`
- `GameplayStatusComponent` -> `mxframework.gameplay.statuses`

这些 descriptor 第一阶段应优先只支持 diagnostics，再逐步接入 hash/save。不要因为某个 core component 看起来简单，就在 schema registry 批次里同时实现 diagnostics/hash/save；也不要因为某个 core component 暂时没有 save adapter，就让 generic store 走反射序列化。

## 禁止项

- 禁止把 `GameplayComponentStore<T>.CreateSnapshot()` 的泛型结果直接交给通用 JSON serializer 作为权威 SaveState。
- 禁止依赖 `Type.FullName` 作为 SaveState / Hash 的永久 type key。
- 禁止反射遍历 fields/properties 来自动生成权威 hash/save。
- 禁止让 component 自己实现大而全的 `Save()` / `Load()` / `Hash()` 方法；这些职责应由 schema descriptor / adapter 承担，避免污染纯数据 component。
- 禁止在 Gameplay Runtime 中引用 UnityEngine / UnityEditor 类型作为 schema payload。

## 后续批次

建议按以下顺序推进：

```text
GAMEPLAY_ECS_STYLE_10_COMPONENT_SCHEMA_REGISTRY
  - GameplayComponentSchema
  - GameplayComponentSchemaRegistry
  - core component diagnostics descriptors
  - metadata snapshot / capability adapter lookup
  - duplicate stable id / duplicate type validation
  - snapshot stable ordering tests

GAMEPLAY_ECS_STYLE_11_COMPONENT_RUNTIME_HASH
  - ComponentWorld hash contributor
  - selected core component hash writers
  - unregistered / unsupported component diagnostics
  - hash ordering regression tests

GAMEPLAY_ECS_STYLE_12_COMPONENT_SAVE_STATE
  - ComponentWorld save provider / restorer
  - selected core component save adapters
  - missing schema / unsupported version errors
  - SaveState JSON roundtrip tests
```

## 验收

- 文档明确 component value 必须经 schema 注册后才能参与 diagnostics/hash/save。
- 文档明确 `StableId`、schema version、diagnostics writer、hash writer、save adapter 的职责。
- 文档明确同一 component 只有一个 schema entry，多种 capability adapter 挂在同一 entry 下。
- 文档明确 registry snapshot 只暴露 metadata，runtime executor 需要通过 registry 解析 capability adapter。
- 文档明确禁止反射 / 泛型 store 直接序列化作为权威保存。
- `Docs/Interfaces/Gameplay.md` 同步 component schema 边界。
- `Docs/README.md` 增加任务入口。
