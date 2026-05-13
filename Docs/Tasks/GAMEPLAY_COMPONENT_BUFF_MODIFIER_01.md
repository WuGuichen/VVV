# GAMEPLAY_COMPONENT_BUFF_MODIFIER_01

## 目标

为 Component Gameplay Runtime 增加第一版 component-native buff / modifier state，让 buff 和 modifier 可以进入 diagnostics、runtime hash、SaveState，并能被 system 做最小生命周期清理。

本任务只做 runtime 数据和测试，不接 UI、不接 Combat、不迁移旧 `BuffPipeline` / `ModifierPipeline`。

## 工作流定级

- 任务等级：`S2`
- 任务类型：`Gameplay runtime / public component API / tests`
- 建议分支：`feature/<issue>-component-buff-modifier`
- 建议标签：`type/implementation`、`module/gameplay`、`status/agent-ready`

## Context Pack

Agent 开工前读取：

1. `AGENTS.md`
2. `Docs/PROJECT_INDEX.md`
3. `Docs/README.md`
4. `Docs/WORKFLOW.md`
5. `Docs/QUALITY_GATE.md`
6. `Docs/Interfaces/Gameplay.md`
7. `Docs/Tasks/GAMEPLAY_COMPONENT_RUNTIME_V0_CLOSEOUT.md`
8. `Docs/Tasks/GAMEPLAY_ECS_STYLE_15_COMPONENT_ATTRIBUTE_RUNTIME.md`
9. 当前任务文档

允许修改：

- `Assets/Scripts/MxFramework/Gameplay/`
- `Assets/Scripts/MxFramework/Tests/Ability/`
- `Docs/Interfaces/Gameplay.md`
- `Docs/CAPABILITIES.md`
- 当前任务文档

不做：

- 不把旧 `BuffPipeline` / `ModifierPipeline` 引用塞进 component store。
- 不让 component runtime 与旧 `RuntimeEntity` 双写同一 buff / modifier 状态。
- 不实现完整 buff stacking policy DSL。
- 不实现 Combat damage / hit bridge。
- 不实现 cast time / interrupt / timeline。
- 不接 UI Toolkit Showcase。

## v0 数据语义

新增纯数据 components：

- `GameplayComponentBuffEntry`
  - `BuffId`
  - `StackCount`
  - `MaxStackCount`
  - `EndFrame`
  - `IsPermanent`
  - `SourceId`
- `GameplayComponentBuffSetComponent`
  - 按 `BuffId` 升序保存。
  - 拒绝重复 buff id。
  - 支持 upsert、remove、remove expired。
- `GameplayComponentModifierEntry`
  - `ModifierId`
  - `AttributeId`
  - `AddValue`
  - `SourceBuffId`
- `GameplayComponentModifierSetComponent`
  - 按 `ModifierId` 升序保存。
  - 拒绝重复 modifier id。
  - 支持按 source buff id 移除。

Modifier v0 只做 additive current-value evaluation：

```text
display/effective current = attribute current + sum(modifier.addValue where attributeId matches)
```

它不写回 `GameplayAttributeSetComponent`，避免 base/current/final 在本批次混成双写状态。

## System

新增 `GameplayComponentBuffCleanupSystem`：

- Phase：`Resolution`
- 输入：`GameplayComponentBuffSetComponent`
- 行为：
  - 移除 `!IsPermanent && EndFrame <= frame` 的 buff。
  - 如果 entity 有 `GameplayComponentModifierSetComponent`，同步移除 `SourceBuffId` 属于过期 buff 的 modifiers。
  - 没有剩余条目时移除对应 component。

## Schema

新增 schema descriptors：

- `GameplayComponentBuffSchemaDescriptors`
- `GameplayComponentModifierSchemaDescriptors`

两者都提供：

- diagnostics
- runtime hash
- SaveState

## 验收

- Buff / Modifier component 构造会排序，并拒绝非法 id / duplicate id。
- Modifier evaluator 能按 attribute id 计算 additive current value。
- Cleanup system 移除过期 buff，并移除同 source buff 的 modifiers。
- Buff / Modifier 参与 ComponentWorld hash。
- SaveState JSON roundtrip 后 hash 一致。
- 非法 SaveState payload 返回结构化错误。
- 不修改 UI、Combat、旧 `RuntimeEntity` / `BuffPipeline` / `ModifierPipeline`。
