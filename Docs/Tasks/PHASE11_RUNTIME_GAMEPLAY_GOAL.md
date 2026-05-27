# Phase 11 Goal：Runtime Gameplay Foundation

> **状态**: Accepted / Closed（2026-05-09）
> **优先级**：P0
> 起点版本：r1192
> 目标：把框架从“可运行切片”推进到“可扩展的运行时玩法基础”

## Goal

建立一套稳定、可测试、可配置、可被 AI 和编辑器理解的 Runtime Gameplay Foundation。

完成后，框架应具备：

```text
RuntimeEntity
  -> Config Driven Ability
  -> Target Selection
  -> Effect Execution
  -> Attributes / Buffs / Modifiers
  -> Events
  -> Snapshot / Diagnostics
```

这套基础要能支撑后续：

- Ability 编辑器。
- AI 辅助创建 Ability / Buff。
- WGame Ability JSON 映射。
- Mod 包扩展。
- Runtime Preview 的真实玩法验证。

但本阶段不直接做编辑器产品化，也不迁移 WGame 真实数据。

## 为什么切到 Goal 模式

此前任务已经完成了四个底座：

| 任务 | 结果 |
|------|------|
| `RUNTIME_VERTICAL_SLICE_01_PLAYABLE_ATTRIBUTES_BUFFS_MODIFIERS.md` | Attributes + Buffs + Modifiers 可 play |
| `RUNTIME_VERTICAL_SLICE_02_ENTITY_ABILITY_EFFECT.md` | Entity + Ability + Target + Effect 闭环 |
| `RUNTIME_VERTICAL_SLICE_03_GAMEPLAY_RUNTIME_CORE.md` | 抽出 `MxFramework.Gameplay` |
| `RUNTIME_VERTICAL_SLICE_04_CONFIG_DRIVEN_ABILITY.md` | `BasicAbilityConfig -> ConfigAbilityFactory -> SimpleAbility` |

继续只写“下一小步”会有两个问题：

1. 容易继续堆功能，但缺少阶段完成定义。
2. 编辑器、Mod、AI、WGame 映射会争抢优先级，导致运行时基础没稳定就上层化。

因此后续任务必须挂到本 Goal 下，先完成运行时基础阶段闭环。

## 阶段边界

### 做

- 稳定 `MxFramework.Gameplay` 公共 API。
- 稳定 Ability 配置最小结构。
- 增强运行时诊断和快照。
- 明确配置变更如何影响运行时对象。
- 明确 Ability / Buff / Modifier 的事件链。
- 补齐 AI Agent 可直接读取的使用文档和接口文档。
- 保持 Demo 可运行，但 Demo 只做演示。

### 不做

- 不做 Ability 可视化编辑器。
- 不做外部 Mod 编辑器产品化。
- 不导入 WGame 真实 Ability JSON。
- 不一次性做完整战斗系统。
- 不做动画、输入、碰撞、寻路、弹道。
- 不做复杂公式 DSL。
- 不把 Demo 类型当作框架 API。

## Milestones

### M1：Gameplay Runtime API 稳定

当前状态：✅ 已完成到 v0.2

已完成：

- `MxFramework.Gameplay`
- `RuntimeEntity`
- `IAbility`
- `SimpleAbility`
- `ITargetSelector`
- `IAbilityEffect`
- `AbilityEvent`
- `DamageEffect`
- `ApplyBuffEffect`

剩余关注：

- 是否需要快照接口。
- 是否需要统一 `AbilityCastRequest` / `AbilityCastResult` 扩展点。
- 是否需要把属性 ID 上下文从 effect 参数提升为可复用 spec。

### M2：Config Driven Ability 稳定

当前状态：✅ v0.3 完成

已完成：

- `BasicAbilityConfig`
- `AbilityTargetSelectorKind`
- `AbilityEffectKind`
- `AbilityEffectConfig`
- `AbilityEffectParameters`
- `ConfigAbilityFactory`
- Demo 配置驱动 Strike / Ignite
- Config + Ability 测试
- 命名化效果参数：`DamageByAttackDefense(attackAttributeId, defenseAttributeId, hpAttributeId)` / `ApplyBuff(buffId)`
- 旧 `Effects.Parameters` 位序数组保留兼容

剩余关注：

- Ability 配置是否需要和 Buff / Modifier 配置形成统一导出格式。

### M3：Runtime Snapshot / Diagnostics for Gameplay

当前状态：✅ v0.1 完成（`RUNTIME_GAMEPLAY_05_DIAGNOSTIC_SNAPSHOT.md`）

目标：

让运行时玩法链路可以输出结构化快照，避免以后排查 Ability / Buff / Modifier 时只能看日志。

候选任务：

```text
RUNTIME_GAMEPLAY_05_DIAGNOSTIC_SNAPSHOT.md
```

目标快照至少包含：

- Entities
- Attributes
- Active Buffs
- Active Modifiers
- Last Ability Cast
- Ability Events
- AttributeChanged Events
- Config Source / Ability Source

验收：

- 纯 C# snapshot builder 可测试。
- Demo OnGUI 可展示摘要。
- JSON 序列化可用于 AI 上下文。

已完成：

- `GameplayDiagnosticSnapshotBuilder.Build(...)` 可汇总 Entity / Attribute / Buff / Modifier / Ability / Event 只读诊断状态。
- Demo / Showcase 已能展示诊断摘要，并由 Phase 12 Diagnostic View 继续映射到 UI Toolkit。
- `RuntimeAbilitySliceDiagnosticViewModelBuilderTests` 覆盖空状态、事件分区、Config Source、errors、summary / technical 文本和关键 UXML 元素。

### M4：Runtime Config Change Handling

目标：

明确 Ability / Buff / Modifier 配置变更后，运行时对象如何响应。

当前状态：✅ v0 完成

候选任务：

```text
RUNTIME_GAMEPLAY_06_CONFIG_CHANGE_APPLY.md
```

重点问题：

- Ability 配置变更后，是重建 ability 还是热替换 effect。
- 已挂载 Buff 是否受新配置影响。
- ConfigChangeSet 如何暴露给 Gameplay Demo。
- Mod / Patch 后续如何复用同一机制。

已完成：

- `RuntimeAbilityConfigResolver`
- `RuntimeConfigChangeSummary`
- Ability 采用 `RebuildOnResolve`，新建 Ability 使用最新配置，旧 Ability 不热替换。
- 已挂载 Buff / Modifier 不受后续配置变更回溯影响。
- Demo 展示 config source 和 change summary。
- `RuntimeConfigChangeHandlingTests` 覆盖成功、失败和非回溯语义。

验收：

- 有最小配置变更测试。
- 有明确“不支持热替换”的失败语义。
- Demo 能显示 config source 和 change summary。

### M5：Ability Authoring Contract 准备

当前状态：✅ v0 完成（`AUTHORING_CONTRACT_ABILITY_01.md`）

目标：

在真正做编辑器前，先把 Ability 编辑器需要写入什么数据、AI 需要生成什么上下文定清楚。

候选任务：

```text
AUTHORING_CONTRACT_ABILITY_01.md
```

内容：

- Ability authoring schema。
- 字段中文名和说明。
- AI 生成上下文。
- 校验错误码。
- 与 `BasicAbilityConfig` 的映射。

验收：

- 不做 UI。
- 不做外部编辑器。
- 只定 contract 和 CLI/测试入口。
- 已提供 `AbilityAuthoringContract`、结构化校验错误码、`AbilityAuthoringContractMapper` 和 `AbilityAuthoringSchema`。

## 当前下一步建议

当前已完成并收口：

```text
AUTHORING_CONTRACT_ABILITY_01.md
PHASE11_RUNTIME_GAMEPLAY_CLOSEOUT.md
```

结论：

1. M1-M5 已实现并完成 closeout，Phase 11 Runtime Gameplay Foundation 标记为 `Accepted / Closed`。
2. Ability 编辑器、AI 辅助生成、WGame Ability JSON 映射和 Mod Package 后续继续依赖同一份 schema / error code / mapping contract。
3. 新增玩法能力、Runtime Preview 协议映射、UI Toolkit 复用控件和 WGame 数据映射都应进入独立后续任务，不再混入 Phase 11。

## Goal 完成标准

Phase 11 完成时必须满足：

1. `MxFramework.Gameplay` API 稳定，Demo 不持有核心逻辑。
2. Ability 可由配置创建并运行。
3. Gameplay Runtime 有结构化 Snapshot / Diagnostics。
4. 配置变更语义明确。
5. `Docs/USAGE.md` 能让人不读源码完成最小接入。
6. `Docs/Interfaces/Gameplay.md` 和 Config 文档能让 AI Agent 自动定位核心 API。
7. Unity Console 无编译 Error。
8. 核心 EditMode 测试稳定通过。
9. 不引入 WGame 业务类型。
10. 不依赖 Unity Editor 才能理解运行时结构。

## 执行规则

- 后续所有 Runtime Gameplay 任务都必须引用本 Goal。
- 每个任务只解决一个明确缺口。
- 每个任务必须有可测试验收。
- 不因编辑器需求破坏 Runtime API。
- 如果某项需求需要 UI，先写 Authoring Contract，再做 UI。

## Closeout 2026-05-09

Phase 11 已接受关闭。正式验收记录见 `PHASE11_RUNTIME_GAMEPLAY_CLOSEOUT.md`。

验收摘要：

- M1-M5 均为 Accepted。
- `MxFramework.Gameplay` 公共 API、Config Driven Ability、Gameplay Diagnostic Snapshot、Runtime Config Change Handling、Ability Authoring Contract 均已进入文档和测试证据。
- `Docs/USAGE.md`、`Docs/Interfaces/Gameplay.md`、`Docs/CAPABILITIES.md`、`Docs/README.md` 已同步 Phase 11 状态。
- `MxFramework.Gameplay` 与 `MxFramework.Config.Runtime` 均保持 `noEngineReferences=true`，运行时结构不依赖 Unity Editor。
- 未导入 WGame real Ability JSON、WGame 业务类型、Entitas 或 Luban 生成数据。

验证记录：

- `dotnet build WGameFramework.sln --no-restore -v minimal`：通过，0 error，10 个既有 warning。
- Unity EditMode `MxFramework.Tests.Ability` + `MxFramework.Tests.Config`：124/124 passed。
- Unity Console error：0。
- 影响面检查完成。
