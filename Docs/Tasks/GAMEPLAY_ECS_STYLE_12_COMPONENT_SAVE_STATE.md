# GAMEPLAY_ECS_STYLE_12_COMPONENT_SAVE_STATE

## 目标

实现 Component Runtime SaveState v0，让 `GameplayComponentWorld` 可以通过 schema registry 中显式注册的 SaveState adapter 捕获和恢复 component runtime 状态。

本批次延续前置契约：

- 不直接序列化 `GameplayComponentStore<T>`。
- 不使用反射展开 component value。
- 不使用 `Type.FullName` 作为权威 type key。
- 只保存显式注册了 `IGameplayComponentSaveStateAdapter<T>` 的 component。
- Restore 遇到缺 schema、缺 adapter、版本不支持、entity id 非法或 payload 解析失败时返回结构化错误。

## 前置依赖

- `GAMEPLAY_ECS_STYLE_09_COMPONENT_SCHEMA_CONTRACT`
- `GAMEPLAY_ECS_STYLE_10_COMPONENT_SCHEMA_REGISTRY`
- `GAMEPLAY_ECS_STYLE_11_COMPONENT_RUNTIME_HASH`
- Runtime SaveState 基础契约：`RuntimeSaveState`、`RuntimeModuleSaveState`、`RuntimeCustomState`、`IRuntimeSaveStateProvider`、`IRuntimeSaveStateRestorer`

## 建议新增 API

```csharp
public sealed class GameplayComponentWorldSaveStateProvider :
    IRuntimeSaveStateProvider,
    IRuntimeSaveStateRestorer
{
    public const string ModuleId = "mxframework.gameplay.component-world";

    public GameplayComponentWorldSaveStateProvider(GameplayComponentWorld world);

    public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState();
    public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState);
}
```

建议新增 component runtime 自己的 DTO，再通过 `RuntimeModuleSaveState.CustomState.PayloadJson` 承载：

```csharp
public sealed class GameplayComponentWorldSaveState
{
    public int SchemaVersion { get; }
    public IReadOnlyList<GameplayComponentEntitySaveState> Entities { get; }
    public IReadOnlyList<GameplayComponentStoreSaveState> ComponentStores { get; }
}

public sealed class GameplayComponentEntitySaveState
{
    public int Index { get; }
    public int Generation { get; }
}

public sealed class GameplayComponentStoreSaveState
{
    public string SchemaId { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<GameplayComponentEntrySaveState> Entries { get; }
}

public sealed class GameplayComponentEntrySaveState
{
    public int EntityIndex { get; }
    public int EntityGeneration { get; }
    public RuntimeCustomState Payload { get; }
}
```

`Payload.TypeId` 应等于 component schema `StableId`，`Payload.SchemaVersion` 应等于 component schema version。

## Save Adapter 契约

当前 `IGameplayComponentSaveStateAdapter<T>` 只是 capability marker。第 12 批需要补齐写入 / 读取契约：

```csharp
public interface IGameplayComponentSaveStateAdapter<T> : IGameplayComponentSchemaDescriptor
    where T : struct, IGameplayComponent
{
    RuntimeCustomState WriteSaveState(GameplayEntityId entityId, in T component);
    RuntimeSaveStateResult<T> ReadSaveState(GameplayEntityId entityId, RuntimeCustomState payload);
}
```

规则：

- Adapter 只负责单个 component value 的 payload，不负责遍历 store。
- Adapter 必须输出稳定 JSON 字段名。
- Adapter 不写 `schemaId` / `schemaVersion` 外层字段，provider 统一写。
- Adapter 可以把 payload 写入 `RuntimeCustomState.PayloadJson`，但不能依赖自动序列化 `GameplayComponentStore<T>`。
- Adapter read 必须校验 payload `TypeId`、`SchemaVersion` 和必需字段。

如果现有接口为了兼容需要保留 marker，可新增独立 runtime adapter 接口；但 registry lookup 必须能拿到实际 save adapter。

## Capture 顺序

`CaptureSaveState()` 必须按稳定顺序输出：

1. 写 `RuntimeSaveState.Frame`。
2. 写 component runtime module state，`moduleId = mxframework.gameplay.component-world`。
3. `Entities` 按 `GameplayEntityLifecycle.CreateSnapshot()` 顺序输出 `Index + Generation`。
4. `ComponentStores` 按 schema `StableId` 升序输出。
5. 每个 store 的 entries 按 `GameplayEntityId` 稳定顺序输出。
6. 每个 entry 由对应 save adapter 写 payload。

只保存同时满足以下条件的 component：

- store 存在。
- schema 已注册。
- schema `SupportsSaveState == true`。
- registry 中能解析 `IGameplayComponentSaveStateAdapter<T>`。

不支持 save 的 component 默认不写入 SaveState；这不是错误，但 diagnostics 应能看见该 store 不可保存。

## Restore 顺序

`RestoreSaveState()` 建议顺序：

1. 从 `RuntimeSaveState.ModuleStates` 找到 `ModuleId`。
2. 校验 module schema version。
3. 解析 `GameplayComponentWorldSaveState` payload。
4. 清空 `GameplayComponentWorld`，包括 component registry state 和 pending events。
5. 恢复 entity lifecycle，保证保存的 `Index + Generation` 被原样恢复。
6. 按 `ComponentStores` 顺序恢复 component entries。
7. 对每个 store：
   - 按 `SchemaId` 查 schema。
   - 校验 schema version。
   - 查 save adapter。
   - 创建或获取对应 `GameplayComponentStore<T>`。
   - 对每个 entry 校验 entity alive。
   - adapter read payload 后写入 store。

如果当前 `GameplayEntityLifecycle` 没有“按 index/generation 恢复”的 API，本批次需要补一个受控 restore 入口。不要通过反复 `Create()` 猜测恢复 generation，也不要暴露裸 int entity id 写入路径。

## 错误模型

建议优先复用 `RuntimeSaveStateErrorCode`，并把具体路径写清楚：

```text
moduleStates[mxframework.gameplay.component-world]
componentStores[0].schemaId
componentStores[0].entries[3].payload
```

必须覆盖的错误：

- MissingModuleState
- InvalidComponentWorldPayload
- MissingComponentSchema
- MissingComponentSaveAdapter
- UnsupportedComponentSchemaVersion
- InvalidComponentEntityId
- DuplicateComponentEntityId
- DuplicateComponentStore
- ComponentPayloadReadFailed

如果 Runtime 层没有精确 enum，可先用现有 `InvalidDocument` / `UnsupportedVersion` / `MissingMigration` 等 code，并在 message/path 中写清 component save state 原因。

## Core Save Adapters

本批次可以先接 selected core components，不必一次性接全部。推荐第一批：

- `GameplayIdentityComponent`
- `GameplayTeamComponent`
- `GameplayLifecycleComponent`

第二批再接：

- `GameplayTagComponent`
- `GameplayStatusComponent`

原因：Tag / Status 是集合 payload，需要额外覆盖排序、去重和 JSON 数组边界；可以在第一批 provider/restorer 跑通后再接。

如果选择一次接完 5 个 core components，测试必须覆盖 tag/status 数组顺序、重复输入、空数组和 invalid id 过滤。

## JSON 形态建议

`RuntimeCustomState.PayloadJson` 中的 component world payload 建议稳定为：

```json
{
  "schemaVersion": 1,
  "entities": [
    { "index": 1, "generation": 1 }
  ],
  "componentStores": [
    {
      "schemaId": "mxframework.gameplay.identity",
      "schemaVersion": 1,
      "entries": [
        {
          "entityIndex": 1,
          "entityGeneration": 1,
          "payload": {
            "typeId": "mxframework.gameplay.identity",
            "schemaVersion": 1,
            "payloadJson": "{\"definitionId\":1001,\"variantId\":2}"
          }
        }
      ]
    }
  ]
}
```

注意：这是 provider 的自定义 module payload，不要求改 `RuntimeSaveState` 顶层 DTO 字段。

## 测试要求

至少新增：

- `CaptureSaveState_WritesEntitiesAndComponentStoresInStableOrder`
- `RestoreSaveState_RecreatesEntitiesComponentsAndHash`
- `CaptureSaveState_SkipsUnsupportedComponentStores`
- `RestoreSaveState_RejectsMissingSchema`
- `RestoreSaveState_RejectsMissingSaveAdapter`
- `RestoreSaveState_RejectsUnsupportedSchemaVersion`
- `RestoreSaveState_RejectsInvalidOrStaleEntityId`
- `RuntimeSaveStateJson_RoundtripRestoresComponentWorld`

如果接入 core tag/status save adapters，额外测试：

- `TagStatusSaveAdapters_PreserveSortedIds`
- `TagStatusSaveAdapters_RestoreEmptySets`

## 验收

- ComponentWorld SaveState provider/restorer 可接入 Runtime SaveState coordinator。
- Capture 不依赖泛型 store 直接 JSON 序列化。
- Restore 不依赖 `Type.FullName` 或反射。
- SaveState JSON roundtrip 后，ComponentWorld hash 与保存前一致。
- 缺 schema / 缺 adapter / unsupported version / invalid entity 都有结构化错误。
- 不支持 save 的 component 不写入 payload，并在文档中明确不是静默权威保存。
- `Docs/README.md` 和 `Docs/Interfaces/Gameplay.md` 同步入口和边界。
