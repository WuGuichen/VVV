# GAMEPLAY_ECS_STYLE_11_COMPONENT_RUNTIME_HASH

## 目标

实现 Component Runtime Hash v0，让 `GameplayComponentWorld` 能通过 schema registry 中显式注册的 hash writer 接入 `RuntimeHashCombiner`。

本批次只做：

- `GameplayComponentWorldHashContributor`
- schema registry 的 hash adapter 稳定执行入口
- core component hash writers
- unsupported / unregistered component 默认不进入 hash
- hash ordering regression tests

本批次不做：

- Component SaveState provider / restorer
- 反射展开 component value
- 自动扫描程序集注册 schema
- 替换旧 `GameplayHashContributor`

## Runtime Hash 规则

`GameplayComponentWorldHashContributor` 的稳定 contributor id 为：

```text
mxframework.gameplay.component-world
```

写入顺序：

1. 按 `GameplayEntityLifecycle.CreateSnapshot()` 的 alive entity 顺序遍历 entity。
2. 对每个 entity，按 schema `StableId` 升序遍历已注册且实际挂载 hash writer 的 component schema。
3. 只对存在于该 entity 上的 component 写入 hash。
4. 每个 component 先写 `schemaId`、`schemaVersion`、`entity.index`、`entity.generation`，再调用对应 hash writer 写字段。

未注册 schema、未声明 `SupportsHash` 或未挂载 hash writer 的 component 不参与 hash。它们仍可通过 diagnostics 的 store 摘要或 diagnostics writer 暴露给工具观察。

## Core Hash Writers

`GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(registry)` 注册：

- `GameplayIdentityComponent`
- `GameplayTeamComponent`
- `GameplayLifecycleComponent`
- `GameplayTagComponent`
- `GameplayStatusComponent`

Core hash writers 只写稳定字段，不使用 `GetHashCode()`、反射字段顺序、对象地址或本地化文本。Tag / Status component 依赖组件自身的排序去重数组输出。

`RegisterDiagnostics` 和 `RegisterRuntimeHash` 可以按任意顺序注册到同一个 registry；同一 component 仍只保留一个 schema entry。

`SupportsHash` 表示该 schema entry 支持 hash capability，不表示每个 descriptor 自身都是 hash writer。比如 core diagnostics descriptor 和 core hash descriptor 需要声明相同 schema metadata，才能挂到同一个 entry 下。

## 验收

- ComponentWorld hash contributor 可通过 `RuntimeHashCombiner` 计算稳定 hash。
- Store 创建顺序和 schema capability 注册顺序不影响 hash。
- Core component 字段变化会改变 hash。
- 未注册或不支持 hash 的 component 不改变 hash。
- Core diagnostics writer 和 hash writer 可以挂到同一个 schema entry。
- `Docs/README.md` 和 `Docs/Interfaces/Gameplay.md` 同步入口和接口说明。
