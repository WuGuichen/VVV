# Runtime Gameplay 06：Config Change Apply

> **状态**: ✅ 已完成（r1197）
> **优先级**：P0
> 所属 Goal：`PHASE11_RUNTIME_GAMEPLAY_GOAL.md`
> 前置任务：`RUNTIME_GAMEPLAY_05_DIAGNOSTIC_SNAPSHOT.md`
> 目标版本：Phase 11 M4

## 目标

定义并实现 Runtime Config Change Handling v0，让运行时配置变更有可测试、可诊断、可展示的最小语义。

本任务的核心结论：

```text
Ability config changed
  -> rebuild ability from latest config
  -> newly created ability uses new selector/effects/parameters
  -> existing mounted buffs/modifiers keep their captured runtime state
```

首版不做 Ability effect 热替换，也不回溯修改已经挂载到 Entity 上的 Buff / Modifier。配置变更只影响变更后新建或重新绑定的运行时对象。

## 完成结果

本任务已落地 Runtime Config Change Handling v0：

- 新增 `RuntimeAbilityConfigResolver`，采用 `RebuildOnResolve` 策略，每次按最新 `IConfigProvider` 重建 Ability。
- 新增 `RuntimeConfigChangeSummary`，记录 config source、changed ability / buff / modifier ids、rebuilt ability ids、failed ability ids 和错误摘要。
- Demo 的 config-driven Ability 路径改为通过 resolver 创建，并在 OnGUI / Snapshot ability source 中展示 config source 和 change summary。
- 纯 C# 测试覆盖 Ability 配置变更后新 Ability 生效、旧 Ability 不被热替换、已挂载 Buff / Modifier 不被回溯修改、失败摘要可断言。

本阶段未扩展 JSON Patch loader 的 Ability Patch 格式；后续 Mod / Runtime Preview 可以复用 resolver 和 summary 语义继续接入。

## 背景

Phase 11 已经具备：

- `BasicAbilityConfig -> ConfigAbilityFactory -> SimpleAbility`
- Runtime Entity / Ability / Buff / Modifier 基础链路
- Gameplay Diagnostic Snapshot
- Demo 可以展示 Ability source 和诊断摘要

下一步需要明确配置被 Patch、Mod 或 Demo 切换后，运行时对象如何响应。若不先固定语义，后续 Ability 编辑器、Runtime Preview 和 Mod Package 会各自发明不同的“热更新”行为，导致测试和玩家可见结果不一致。

## 配置变更语义 v0

### Ability

Ability 配置变更采用重建语义。

要求：

- 运行时持有的 Ability 不在原对象上热替换 selector / effects / parameters。
- 配置变更后，需要通过 `ConfigAbilityFactory` 使用最新配置重新创建 Ability。
- Demo 或调用方负责把旧 Ability 引用替换为新 Ability 引用。
- 新 Ability 后续 cast 必须使用最新配置。
- 如果重建失败，旧 Ability 不应被静默替换；调用方应保留旧引用或进入无 Ability 状态，并暴露错误摘要。
- 未重建前，旧 Ability 行为保持旧配置语义。

明确不支持：

- 不支持在 `SimpleAbility` 内部替换 effect list。
- 不支持正在执行中的 cast 被中途改写。
- 不支持对已发出的 Ability event 进行重写。

### Buff / Modifier

已挂载 Buff / Modifier 不追溯修改。

要求：

- Entity 上已经存在的 Buff / Modifier 保持挂载时捕获的运行时值和剩余状态。
- Buff / Modifier 配置变更只影响后续新创建并挂载的实例。
- 不遍历 Entity 清理、重算或替换已有 Buff / Modifier。
- Snapshot 应能让测试和 Demo 区分“新配置 source”和“当前已挂载运行时实例”。

明确不支持：

- 不支持根据新配置批量重算已挂载 Modifier 数值。
- 不支持把已挂载 Buff 的 tick interval / duration / effect 参数改成新配置。
- 不支持配置变更后自动驱散旧 Buff。

### Config Source / Change Summary

配置变更应暴露最小摘要，用于 Demo、测试、AI 上下文和后续 Runtime Preview。

建议新增或复用类似概念：

```text
RuntimeConfigChangeSummary
  SourceName
  PreviousSourceName
  ChangedAbilityIds
  ChangedBuffIds
  ChangedModifierIds
  RebuiltAbilityIds
  FailedAbilityIds
  Error
```

字段可以按现有代码风格微调，但必须满足：

- 可在纯 C# 测试中创建和断言。
- 不引用 UnityEngine / UnityEditor。
- 能表达“有配置变更但 Ability 重建失败”。
- 能表达“Buff / Modifier 配置变更不会追溯已挂载实例”。

## 范围

### 必须完成

1. 定义 Config Change Handling v0 的运行时入口。

建议位置：

```text
Assets/Scripts/MxFramework/Gameplay/
Assets/Scripts/MxFramework/Config.Runtime/
```

职责可以拆分，但必须保持运行时核心不依赖 UnityEditor。

入口至少能完成：

- 接收新的 config source 或 provider。
- 判断 Ability / Buff / Modifier 相关配置是否发生变化。
- 对受影响 Ability 执行重建。
- 返回 change summary。

2. Ability 配置变更后重建。

至少覆盖：

- 同一个 ability id 的 damage 参数变更后，新建 Ability 使用新参数。
- selector 变更后，新建 Ability 使用新 selector。
- effect kind 或参数非法时，重建失败并返回明确错误。
- 重建失败不静默吞掉错误。

3. Buff / Modifier 不追溯。

至少覆盖：

- 已挂载 Buff 在配置变更后保持旧 duration / tick / effect 参数。
- 已挂载 Modifier 在配置变更后保持旧数值或旧计算语义。
- 配置变更后新挂载的 Buff / Modifier 使用新配置。

如果当前 Buff / Modifier 运行时结构还不足以精确表达某项断言，应在本任务中补最小可测试能力，不做大规模重构。

4. RuntimeAbilitySliceRunner Demo 展示。

Demo 可继续使用 OnGUI，但必须展示：

```text
Config Source: ...
Config Change: changed abilities=N, buffs=N, modifiers=N, rebuilt=N, failed=N
```

当 Ability 重建失败时，Demo 应显示失败摘要，而不是只写 Console。

5. 诊断快照接入。

如果已有 `GameplayDiagnosticSnapshot` 支持 source，应复用；否则补充最小字段或额外摘要，让测试可以读取：

- 当前 config source
- 最近一次 config change summary
- 当前 ability source
- 已挂载 Buff / Modifier 的运行时状态

6. 自动化测试。

至少覆盖：

- Ability damage 参数变更后，重建的新 Ability 生效。
- 旧 Ability 未重建前仍保持旧行为。
- selector 变更后，重建的新 Ability 选择目标变化。
- 已挂载 Buff 不受新 Buff 配置影响。
- 已挂载 Modifier 不受新 Modifier 配置影响。
- 配置变更后新挂载 Buff / Modifier 使用新配置。
- Ability 重建失败时返回失败 summary。
- Demo 依赖的 config source / change summary 字符串可由纯 C# 数据构建。

7. 文档更新。

至少更新：

- `Docs/Interfaces/Gameplay.md`
- `Docs/USAGE.md`
- `Docs/CAPABILITIES.md`
- `Docs/Tasks/PHASE11_RUNTIME_GAMEPLAY_GOAL.md` 中 M4 状态
- 本任务状态

### 不做

- 不做完整热重载系统。
- 不做运行中 cast 的中途替换。
- 不做已挂载 Buff / Modifier 的迁移、重算或批量替换。
- 不做编辑器 UI。
- 不做 Runtime Preview 协议。
- 不做 Mod Package 合并策略扩展。
- 不导入 WGame 真实配置。
- 不新增 `.unity` 场景。

## 失败语义

必须把失败情况显式化：

- 未找到 ability config：重建失败，summary 记录 ability id。
- 未知 selector / effect kind：重建失败，summary 记录错误。
- effect 参数不足或非法：重建失败，summary 记录错误。
- 缺少 BuffFactory 且 effect 需要创建 Buff：重建失败，summary 记录错误。
- Buff / Modifier 配置变化不会对已挂载实例返回“已应用”，只能记录为“影响后续新实例”。

错误信息不要求最终本地化，但应短、稳定、可被测试断言。

## 验收标准

1. Ability 配置变更采用重建语义，并有代码和文档说明。
2. 旧 Ability 未重建前保持旧配置行为。
3. 新 Ability 重建后使用新配置行为。
4. 已挂载 Buff / Modifier 不被配置变更追溯修改。
5. 配置变更后新挂载 Buff / Modifier 使用新配置。
6. Runtime change summary 能表达 source、changed ids、rebuilt ids、failed ids 和错误。
7. Demo 显示 config source 和 config change summary。
8. Demo 中 Ability 重建失败有可见摘要。
9. Gameplay / Ability / Config 相关 EditMode 测试通过。
10. Unity Console 无编译 Error。
11. Runtime 纯 C# 类型不引用 UnityEngine / UnityEditor。
12. 不新增 `.unity` 场景。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- 自动化测试通过。
- 影响面检查完成或影响面合理。
- SVN 提交信息建议：

```text
Add runtime config change handling
```
