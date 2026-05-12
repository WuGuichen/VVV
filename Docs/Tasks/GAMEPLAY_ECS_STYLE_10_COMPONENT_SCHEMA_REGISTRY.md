# GAMEPLAY_ECS_STYLE_10_COMPONENT_SCHEMA_REGISTRY

## 目标

实现 Component Schema Registry v0，让 component runtime 有一个稳定的 schema metadata 和 capability adapter 注册入口，为后续 component diagnostics、runtime hash 和 SaveState 做准备。

本批次只做：

- `GameplayComponentSchema`
- `GameplayComponentSchemaRegistry`
- diagnostics/hash/save capability lookup
- core component diagnostics descriptors
- `GameplayComponentWorld.Schemas`

本批次不做：

- Component runtime hash contributor
- Component SaveState provider / restorer
- SaveState payload writer / reader
- 反射展开 component value
- 自动扫描程序集注册 schema

## 新增 API

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
    where T : struct, IGameplayComponent;

public interface IGameplayComponentHashWriter<T> : IGameplayComponentSchemaDescriptor
    where T : struct, IGameplayComponent;

public interface IGameplayComponentSaveStateAdapter<T> : IGameplayComponentSchemaDescriptor
    where T : struct, IGameplayComponent;

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

`GameplayComponentWorld` 新增：

```csharp
public GameplayComponentSchemaRegistry Schemas { get; }
```

## 语义

- `StableId` 和 `ComponentType` 在一个 registry 内必须唯一。
- 同一 schema entry 可以挂载多个 capability adapter。
- 多个 adapter 注册到同一 entry 时，schema metadata 必须完全一致。
- 同一 capability 不能重复注册。
- `CreateSnapshot()` 只返回 schema metadata，按 `StableId` 升序稳定排序。
- Runtime diagnostics/hash/save executor 必须通过 registry lookup capability adapter，不应拿 metadata 后反射 component value。

## Core diagnostics descriptors

`GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(registry)` 注册当前 core components 的 diagnostics capability：

- `GameplayIdentityComponent` -> `mxframework.gameplay.identity`
- `GameplayTeamComponent` -> `mxframework.gameplay.team`
- `GameplayLifecycleComponent` -> `mxframework.gameplay.lifecycle`
- `GameplayTagComponent` -> `mxframework.gameplay.tags`
- `GameplayStatusComponent` -> `mxframework.gameplay.statuses`

本批次只注册 diagnostics capability。Core components 的 hash writer 和 SaveState adapter 留到后续批次按真实 executor 一起实现。

## 验收

- Schema 校验 stable id、version 和 component type。
- Stable id 拒绝前后空格、空白字符、大写字符和空 dotted segment。
- Component type 必须是实现 `IGameplayComponent` 的 value type。
- Registry 拒绝重复 stable id、重复 component type 和冲突 metadata。
- Registry 允许不同 capability adapter 挂到同一 schema entry。
- Capability adapter 必须匹配 `Schema.ComponentType`，且 schema 必须显式声明对应 support flag。
- Registry 拒绝重复 capability。
- Snapshot 按 `StableId` 稳定排序。
- Core diagnostics descriptors 注册 5 个 core component schema，并能写出稳定 diagnostics fields。
- Diagnostics executor 后续负责统一写入 `schemaId` / `schemaVersion`；core diagnostics writer 只写 entity 和 component fields。
- `GameplayComponentWorld` 默认持有 schema registry，也支持注入。
