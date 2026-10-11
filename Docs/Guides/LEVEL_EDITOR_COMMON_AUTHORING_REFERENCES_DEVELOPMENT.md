# Mx 关卡编辑器 P3.0：通用创作引用开发指南

> Status: Current
>
> Implementation state: P3.0 implemented and V4 accepted
>
> Parent design: [通用创作引用设计](../LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DESIGN.md)
>
> Last verified: 2026-07-28

## 1. 用途与边界

本文记录 P3.0 的公共契约、CharacterTest 适配方式、兼容策略和验收入口。它是 Editor 创作层指南，不定义 Runtime 目标或来源。

本次改动不在高频路径。所有新增分配都发生在 Inspector 构建或刷新时，归类为 Editor-only `AllocByDesign`；Runtime Trigger、Gameplay、Buff、Story、SaveState、Replay 和 Network 均保持原结构。

## 2. API 复用计划

| 需求 | 复用能力 | P3.0 做法 |
|---|---|---|
| 目标语义 | `GameplayTriggerTargetSelector`、route action、source binding、显式目标 | 解析并投影为 Editor 候选，不新增目标枚举 |
| Buff 候选 | `GameplayBuffDefinitionRegistry` | 作为唯一 Buff 定义权威 |
| Buff 展示 | `GameplayBuffPresentationDefinitionRegistry`、CharacterTest 中文展示适配 | 秒数优先显示持续时间，翻译叠层行为并说明玩法结果 |
| 来源机制 | Aura 与 CharacterTest Mechanic Source definition registries | 聚合现有正式定义，不使用 Runtime command source |
| 数据写回 | Trigger action `Payload0/1/2` | `Payload1`继续保存底层效果归因 `SourceId` |
| 事务 | 现有关卡文档事务、Unity Undo、脏状态和保存 | 每次选择为一个既有事务步骤 |
| Runtime | Gameplay Trigger compiler 与 Gameplay Buff bridge | 不修改；复用 `Payload1 → SourceId` |

## 3. 文件职责

| 文件 | 职责 |
|---|---|
| `Packages/com.vincent.wgameframework/Editor/MxAuthoringReferenceField.cs` | 通用 Option 值对象与 0/1/多项/未知值 UI 规则 |
| `CharacterTestWorldObjectLevelEditorModule.Triggers.BuffPicker.cs` | 从 Gameplay Buff Registry 构造 Buff 候选 |
| `CharacterTestWorldObjectLevelEditorModule.Triggers.References.cs` | 解析 Trigger route/binding/compiler 的合法作用目标 |
| `CharacterTestWorldObjectLevelEditorModule.Triggers.AttributionReferences.cs` | 聚合 Aura/Mechanic Source 来源机制目录 |
| `CharacterTestWorldObjectLevelEditorModule.Triggers.Inspector.Fields.cs` | 把通用字段嵌入现有渐进式 Trigger 属性区 |
| `MxAuthoringReferenceFieldTests.cs` | 通用控件四种状态与未知值保持 |
| `CharacterTestLevelGameplayTriggerAuthoringTests.cs` | CharacterTest 候选、事务、刷新和 Inspector 回归 |

## 4. 公共契约用法

业务适配器直接创建候选，不注册 Provider：

```csharp
var options = new List<MxAuthoringReferenceOption<long>>
{
    new("none", 0L, "未指定"),
    new("mechanic.example.zone", 500001L, "500001 · 示例区域")
};

VisualElement field = MxAuthoringReferenceField.Create(
    "来源机制",
    currentValue,
    options,
    value => ApplyExistingDocumentTransaction(value));
```

接入约束：

1. `Key`必须来自业务稳定身份，不能使用列表索引。
2. `Value`必须是现有 Authoring/Runtime 已认识的持久化值。
3. 候选必须来自正式目录或编译结果，不能凭 UI 常量补造 Runtime 定义。
4. 当前值未知时不得先修正再构建控件。
5. 变更回调必须进入业务自己的事务和校验流程。
6. 不在通用层保存候选、不反射扫描业务程序集、不建立全局缓存。

## 5. CharacterTest 目标解析

目标解析按以下顺序进行：

1. 从 level entry 找到 Trigger definition、binding 和 source。
2. 校验 source 为当前区域使用的 Occupancy Source，并存在 source binding。
3. 遍历 routes/actions，要求同一 Trigger 的动作目标语义一致。
4. Buff action 只接受 `Subject`。
5. Story action 只接受指向当前 source binding 的 `ExplicitBindingTarget`，且 Domain 必须为 Trigger Source Binding。
6. 已有 compiler 成功结果可用时，要求其中存在对应 definition/binding。
7. 任一步失败都只返回诊断，不生成伪候选。

本阶段目标字段只有一个合法值，所以显示只读内容。不得为了制造下拉框暴露 Owner、Instigator 或未绑定的显式目标。

## 6. Buff 与来源机制目录

### 6.1 Buff

- 定义：`CharacterTestGuardAuraRuntime.CreateBuffDefinitions()`。
- 展示：`CharacterTestGuardAuraRuntime.CreateBuffPresentationDefinitions()`。
- 排序：数值 Buff ID 升序。
- 缺失展示：回退到 definition stable ID。
- 未登记当前值：原值保持，普通区显示警告，高级规则保留手动迁移入口。
- 普通摘要：显示“持续多少秒、重复施加会怎样、是否叠层”和项目玩法结果。
- Tooltip：保留逻辑帧、原始 Stack Policy、每次施加层数、最大层数和技术效果摘要。
- `ApplyStackCount / MaxStackCount`来自 Buff Definition，不是 Trigger 的覆盖值；Trigger 冷却和执行次数仍是独立规则。

`601012 · 中毒`当前显示`持续 6 秒 · 重复施加刷新 · 不可叠加`，并说明每 1 秒造成 2 点毒素伤害。伤害值与间隔复用 CharacterTest 现有毒伤常量，Editor 不建立第二套玩法配置。

### 6.2 来源机制

- Aura：`CharacterTestGuardAuraRuntime.CreateAuraDefinitions()`。
- 区域禁锢：`CharacterTestAreaConfinementRuntime.CreateMechanicSourceDefinitions()`。
- 毒瓶池：`CharacterTestPoisonBottlePoolRuntime.CreateMechanicSourceDefinitions()`。
- 显式加入 `0 · 未指定`。
- 以数值 ID 为去重键，稳定升序；同 ID 不同 stable ID 产生警告。
- 当前正式结果应为 `0/501008/501010/501012`。
- 普通摘要只说明它记录施加来源，不改变 Buff 伤害、持续时间或叠层。
- 不显示 Mechanic Source 自身的 Effect 数量；选择来源机制只写入 `SourceId`，不会执行来源定义中的 Apply Buff、VFX 或其他 Effect。

中文名称由现有 `CharacterTestLevelEditorChineseText` 展示适配统一提供；稳定 ID 保留在 Tooltip/回退显示中。新增正式来源必须先进入对应 Runtime 目录，再由适配器读取；不能只给下拉框加一行。

## 7. 兼容与刷新策略

- 未登记历史正整数继续保存在 `Payload1`，UI 不自动写 `0`。
- 折叠`高级规则`、重建 Inspector、切换同一条 Trigger 或刷新目录不能触发变更回调。
- Dropdown 只列正式候选；未知当前值显示在控件当前文本和警告中。
- 用户显式选中新候选后，调用现有 `UpdateTriggerPayloads(...)`，再使用现有局部 Inspector 刷新路径。
- 选择操作继续由现有文档事务记录，因此 Undo/Redo、脏状态、保存基线和重新加载不另建实现。

## 8. 自动测试

最低 focused EditMode 集合：

- `MxAuthoringReferenceFieldTests`
  - 0 个候选只有诊断；
  - 1 个候选只读；
  - 多个候选可选择；
  - 未知当前值保持且不混入候选。
- `CharacterTestLevelGameplayTriggerAuthoringTests`
  - Buff 目标只有 Subject；
  - Story 目标只有当前显式绑定；
  - 来源机制精确包含 `0/501008/501010/501012`；
  - 中毒摘要显示 6 秒、刷新、不叠加以及每秒 2 点毒素伤害；
  - 毒瓶池来源摘要不出现`2 个效果`；
  - payload round-trip、Undo/Redo、保存和 Inspector 刷新无回归。
- Gameplay Trigger Gameplay Buff bridge focused test
  - 继续确认 `Payload1`进入 `GameplayBuffApplicationRequest.SourceId`。

不要求新增 PlayMode。若现有 P2.4 Runtime 测试因本次 Editor-only 改动失败，应先作为回归处理，不能用“无 Runtime 修改”跳过。

## 9. 手动验收

1. 打开 `MxFramework > Level Editor`，进入`世界对象`。
2. 选择一个已启用的触发区域，并打开一条 Buff Trigger。
3. 确认`作用对象`只读显示`进入区域的角色`，不显示 Trigger Source / Subject 技术摘要。
4. 确认 Buff 为目录选择器；`601012 · 中毒`显示`持续 6 秒 · 重复施加刷新 · 不可叠加`以及每秒 2 点毒素伤害。
5. 确认`来源机制`只含四个预期值；选择 `501012 · 毒瓶池`后只显示来源说明，不出现`Mechanic Source · 2 个效果`，Inspector 不跳到顶部。
6. Undo/Redo 一次，确认候选和值同步恢复。
7. 保存、重新加载，确认来源机制仍为 `501012`。
8. 打开 Story Trigger，确认作用对象只读显示`当前区域`。
9. 用高级规则输入一个未登记正整数，确认普通区显示兼容警告且不会自动清零。

## 10. P3.0 状态台账

| 项目 | 状态 |
|---|---|
| 通用 Editor 契约 | Implemented |
| Buff 选择器迁移 | Implemented |
| Trigger 目标适配 | Implemented |
| 来源机制适配 | Implemented |
| JSON/Runtime schema | Unchanged |
| 高频路径 | Unchanged |
| Unity 编译与 Console | Passed；0 error |
| Focused EditMode | Passed；44/44（通用引用、CharacterTest Trigger 创作与 Buff Bridge） |
| Lightweight harness | P3.0 基线曾通过；本次仅调整 Editor 展示语义，未重复执行 |
| Code standards report | Passed；本次涉及的 8 个首方 C# 文件，0 finding |
| 实际窗口布局验收 | Passed；1440×900 窗口中，Buff 摘要 210×48、来源机制摘要 210×32，均完整换行且无横向溢出 |

实际窗口检查应确认`进入区域的角色`只读作用对象、中文 Buff 下拉、语义化 Buff 摘要以及`501012 · 毒瓶池`来源机制说明完整可见；来源行不得暗示会执行毒瓶池定义自己的两个 Effect。
