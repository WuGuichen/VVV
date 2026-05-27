# WGame Buff 创作流水线

> Status: Archive
>
> Scope: Legacy WGame authoring workflow reference. Current authoring workflow facts live in `AUTHORING_WORKFLOW.md` and current tool docs.

## 目标

Buff 编辑器的操作逻辑必须同时满足两件事：

- 操作入口贴近 WGame 现有心智模型：先选 `BuffType`，再填 `BuffData` 公共字段和类型专属字段。
- 底层协议保持框架化：流程、步骤、快捷动作、校验和 AI 上下文仍使用 `AuthoringWorkflow`，不绑定 Unity Editor 或旧 JSON 实现。

换句话说，编辑器对人呈现 WGame 逻辑，对工具暴露可移植 Workflow。

## WGame 类型入口

创建 Buff 的第一步不是选择 Modifier，而是选择 WGame 风格的 `BuffType`：

| BuffType | 数据类 | 操作含义 |
| --- | --- | --- |
| `Numerical` | `NBuffData` | 属性数值 Buff，配置属性、加值、乘值、增益标记和特效 |
| `Condition` | `CBuffData` | 条件 Buff，配置条件列表，以及满足条件后添加或移除的 Buff |
| `ChangeAttr` | `SBuffChangeAttrData` | 直接改变属性，配置属性、加值、乘值和表现 |
| `DamageByAttr` | `SBuffDamageByAttrData` | 按属性造成伤害，配置伤害值、伤害类型、元素和命中特效 |
| `CastOrbBezier` | `SBuffCastOrbBezierData` | 贝塞尔弹道 Buff，配置起点、目标、命中、技能和表现 |
| `CastOrbTrack` | `SBuffCastOrbTrackData` | 追踪弹道 Buff，配置起点、目标、命中、技能和表现 |
| `CastOrbLinear` | `SBuffCastOrbLinearData` | 直线弹道 Buff，配置起点、目标、命中、技能和表现 |
| `Positive` | `PBuffData` | 被动属性 Buff，配置被动属性类型和值 |
| `Status` | `StatusBuffData` | 状态 Buff，配置状态类型，并推导属性影响和是否增益 |

`None` 和 `Max` 不作为可创建模板暴露。

## 操作流程

推荐的创建流程：

```text
确定 Buff 设计目标
  -> 选择 Buff 类型
  -> 填写公共字段
  -> 配置目标、堆叠和持续
  -> 填写类型专属字段
  -> 配置表现资源
  -> 检查引用关系
  -> 填写多语言
  -> 选择 Patch / Mod 层
  -> 运行校验
  -> 预览运行时合并结果
  -> 导出报告 / Mod Patch / 提交材料
```

### 1. 确定 Buff 设计目标

先写清楚这个 Buff 的玩法目的，例如持续伤害、属性提升、控制状态、条件触发或发射弹道。

输出：

- Buff 目的。
- 推荐 BuffType。
- 是否面向玩家 Mod。

### 2. 选择 Buff 类型

选择 `BuffType` 后，编辑器必须切换到对应数据类的字段集合。

校验：

- `BuffType` 不能为 `None`。
- `BuffFactoryData.Type` 必须和 `BuffFactoryData.Buff` 的数据类匹配。
- 玩家模式只显示安全模板，不暴露内部调试类型。

### 3. 填写公共字段

公共字段来自 `BuffData`：

| 字段 | 说明 |
| --- | --- |
| `ID` | Buff 稳定 ID |
| `Name` | 名称或名称 key |
| `Desc` | 描述或描述 key |
| `ShowHeadIcon` | 是否显示图标 |
| `Removeable` | 是否可移除 |

这些字段不应该和类型专属字段混在同一个巨型表单里。

### 4. 配置目标、堆叠和持续

这些字段也来自 `BuffData`，但属于行为策略：

| 字段 | 说明 |
| --- | --- |
| `Target` | 数值来源或目标 |
| `AddType` | 堆叠、刷新、替换策略 |
| `AddNum` | 堆叠数量 |
| `Duration` | 持续时间，沿用 WGame 毫秒语义 |
| `HitCooldown` | 周期伤害或周期触发间隔 |

这里需要高亮单位，避免秒和毫秒混淆。

### 5. 填写类型专属字段

不同 BuffType 对应不同字段页：

- `Numerical`：`AttrID`、`AddValue`、`MulValue`、`IsBuff`、`IsNeutralBuff`。
- `Condition`：`AddBuffList`、`RemoveBuffList`、`ConditionList`。
- `ChangeAttr`：`AttrID`、`AddValue`、`MulValue`。
- `DamageByAttr`：`Values`、`DmgType`、`EleType`、`EleValue`、`DamageBaseTypeID`。
- `CastOrb*`：`HitTarget`、`HitBuffs`、`HitSkill`、命中、弹道、元素和表现字段。
- `Positive`：`PAttrType`、`PValue`。
- `Status`：`StatusType`，并自动推导 `AttrType`、`AttrValueAdd`、`AttrValueMul`、`IsBuff`。

### 6. 配置表现资源

表现字段必须和类型绑定：

- 属性类 Buff 主要关注开始特效、持续部位、缩放和时长。
- 伤害类 Buff 关注命中特效、命中部位、元素和伤害表现。
- 弹道类 Buff 关注飞行特效、命中特效、音效、起点和目标位置。
- 状态和被动类 Buff 可以允许无表现，但要明确是有意为空。

### 7. 检查引用关系

引用校验必须按类型展开：

- 属性 ID 是否存在。
- 状态类型是否存在，并能推导状态信息。
- 条件类型是否存在。
- `AddBuffList` / `RemoveBuffList` / `HitBuffs` 是否引用有效 Buff。
- 技能、音效、元素、伤害类型、位置类型和资源路径是否存在。

### 8. 填写多语言

旧 WGame 数据可能直接保存 `Name` / `Desc` 字符串。框架新流程仍要为多语言保留独立层：

- 编辑器显示可直接输入中文。
- 导出层可以生成多语言 key。
- AI 只在这一步生成文本，不擅自改行为字段。

### 9. 选择 Patch / Mod 层

玩家模式只能写 Mod 层。开发者可以选择 Patch 或项目数据层。

任何流程都不能直接破坏 Base 数据。

### 10. 运行校验

保存前必须通过：

- 字段合法性。
- 类型和数据类匹配。
- 单位检查。
- ID 冲突检查。
- 引用检查。
- 多语言检查。
- Patch / Mod 冲突检查。

### 11. 预览运行时合并结果

编辑器不能只展示编辑源，必须展示运行时最终读到的结果：

- Base + Patch + Mod 后的 `BuffFactoryData`。
- 具体 `BuffData` 子类 payload。
- ChangeSet：新增、替换、删除、无变化。

## 扬长避短原则

保留 WGame 的优点：

- `BuffType` 分类清晰，适合作为编辑器第一层入口。
- `BuffData` 公共字段和子类字段边界明确。
- 状态、属性、伤害、条件、弹道这些玩法分类对策划直观。

修正 WGame 的痛点：

- 不再用数组下标作为编辑器心智模型，编辑器必须显示字段名和中文别名。
- 不把所有类型塞进一个大表单。
- 不让表现、引用、堆叠、多语言混在同一步。
- 不要求 AI 或玩家理解完整源码。
- 不直接写 Base，先进入 Draft / Patch / Mod 层。

保留框架抽象的优点：

- 流程用 `AuthoringWorkflow` 描述，可以被 Unity Editor、外部 Mod Editor、CLI 和 AI 共用。
- QuickAction 只表达意图，例如打开字段、打开引用、运行校验、预览合并。
- 运行时只读 merged snapshot，不把编辑器逻辑塞进战斗运行时。

## 当前实现状态

- `BuffAuthoringWorkflowTemplate` 已改为 WGame 风格流程。
- 当前仍是只读 Demo，不保存配置源。
- 下一步应实现类型模板的 Schema / 字段别名 / 校验规则，而不是继续堆通用 UI。
