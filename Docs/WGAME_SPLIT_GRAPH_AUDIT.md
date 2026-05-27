# WGame Split Graph 结构审计

> **本文涉及的 AI 概念属于 AIAction Config 域**（旧 WGame AI 行为配置的 Split JSON 结构审计）。详见 `Docs/INTERFACES.md` 的 AI Terminology 章节。

> Status: Archive
> Archived state: Frozen for Phase 9 closeout input
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09
>
> Fact Conclusions: 本文冻结 `SplitAIActionData`、`SplitAIConfigData`、`SplitBuffData` 的旧结构位序、类型分布和迁移影响；可作为 `AIActionGraph`、`AIConfig`、`AIConfigDefense`、`AIGoals`、`BuffGraph` Schema 种子的依据。
>
> Pending Confirmation: `CastOrb.HitSkill` 精确位序、Buff 条件细分、AIAction 枚举解析、bytes 分类和 SaveData 边界仍是待补项；本文不把旧数组位序作为新权威源格式。

> 目标：补充审计 `SplitAIActionData`、`SplitAIConfigData`、`SplitBuffData` 的结构位序、字段含义和迁移影响。本文只记录结构事实，不迁移真实数据。

## 1. 范围和结论

本轮覆盖：

- `Assets/Res/SplitAIActionData/*.json`
- `Assets/Res/SplitAIConfigData/{AIConfig,AIConfig_Defense,AIGoals}/*.json`
- `Assets/Res/SplitBuffData/*.json`

核心结论：

- `AIAction` 是最适合作为下一步试点的数据：它结构固定，关系清晰，同时连接 `TbAIAction`、`AIConfig` 和 `AbilityGraph`。
- `AIConfig` 是动作组合图，当前以字符串 `Name` 被 `TbCharacterAI` 引用，后续应改成稳定 ID 引用。
- `Buff` 已经有明确 `Type`，但每个类型内部仍大量使用数组位序，应改成类型化 Schema。
- 这三类数据都不适合作为单一 TSV；推荐“索引表 + 类型化 Graph JSON + 统一运行时产物”。

## 2. SplitAIActionData

### 2.1 结构规模

| 项 | 数量 |
|----|------|
| 有效 JSON 文件 | 500 |
| `_index.json` | 1 |
| B 数组长度 | 500/500 均为 15 |
| 对应 `TbAIAction` | ID 1-499 全部存在 |
| Graph-only | ID 0 |
| Ability 引用缺失 | 0 |

### 2.2 B 位序

来源代码：`Assets/Scripts/Ability/Data/AI/GOAPActionData.cs`

| 索引 | 字段 | 类型 | 含义 | 迁移建议 |
|------|------|------|------|----------|
| 0 | `ID` | int | AIAction ID | `id` |
| 1 | `Name` | string | 内部名称 | `name` |
| 2 | `EquipType` | flags int | 穿戴类型，区分 Fight / Reaction 等 | `equipTypes` |
| 3 | `SpecialEquipType` | int | 特殊武器类型 | `specialEquipType` |
| 4 | `Ability` | int | 引用 `SplitAbilityData/TestGroup` | `abilityId` |
| 5 | `AbilityWeaponType` | flags int | 适配武器类型 | `weaponTypes` |
| 6 | `Cost` | int | 行动 Cost | `cost` |
| 7 | `CooldownTime` | int | 冷却时间 ms | `cooldownMs` |
| 8 | `CooldownTimeInit` | int | 初始冷却 ms | `initialCooldownMs` |
| 9 | `UsageCount` | int | 可存储次数 | `usageCount` |
| 10 | `ReactType` | int | 对策条件类型 | `reactionType` |
| 11 | `ReactPointNum` | int | 对策点数 | `reactionPoints` |
| 12 | `BreakType` | flags int | 打断/连携组，反序列化后会规整 | `breakType` |
| 13 | `ConditionList` | array | GOAP 条件列表 | `conditions` |
| 14 | `EffectList` | array | GOAP 效果列表 | `effects` |

### 2.3 Conditions / Effects

`GOAPCondition` 当前结构：

| 字段 | 含义 | 建议命名 |
|------|------|----------|
| `K` | `ConKey`，GOAP world key | `key` |
| `T` | `CmpType`，比较类型 | `compare` |
| `V` | `CmpValue`，比较值 | `value` |

`GOAPEffect` 当前结构：

| 字段 | 含义 | 建议命名 |
|------|------|----------|
| `K` | `EffectKey`，GOAP world key | `key` |
| `T` | `EffectType`，增减或设置语义 | `effect` |

统计结果：

| 项 | 分布 |
|----|------|
| 条件数量 | 1 条：382；2 条：110；0 条：6；3 条：2 |
| 效果数量 | 1 条：466；2 条：29；0 条：4；3 条：1 |
| 高频条件 Key | `9` 出现 587 次，远高于其他条件 |
| 高频效果 Key | `0` 出现 487 次，`9` 出现 27 次 |
| EffectType | `0` 出现 500 次，`1` 出现 27 次 |

判断：

- `conditions/effects` 已经接近 Graph 结构，只需要语义字段名和枚举名化。
- `Ability` 是最关键引用，必须校验到 AbilityGraph。
- `BreakType` 当前会在反序列化中规整，迁移时应确认是否保存原始值还是规整值。

### 2.4 新结构草案

```json
{
  "id": 459,
  "name": "GS_LeapHeavyChop",
  "abilityId": 941,
  "equipTypes": ["Fight"],
  "weaponTypes": ["GreatSword"],
  "cost": 43,
  "cooldownMs": 12000,
  "initialCooldownMs": 8000,
  "usageCount": 1,
  "reactionType": "Hit",
  "reactionPoints": 0,
  "breakType": "AttackGroup",
  "conditions": [
    { "key": "IsNearTarget", "compare": "LessOrEqual", "value": 1200 }
  ],
  "effects": [
    { "key": "IsNearTarget", "effect": "Decrease" }
  ]
}
```

## 3. SplitAIConfigData

### 3.1 结构规模

| 分组 | 有效文件 | 角色 |
|------|----------|------|
| `AIConfig` | 134 | 进攻 AI 动作组合 |
| `AIConfig_Defense` | 13 | 反应/防御动作组合 |
| `AIGoals` | 1 | GOAP 目标条件 |

### 3.2 AIConfig 位序

来源代码：`Assets/Scripts/Ability/Data/AI/GOAPAIConfigData.cs`

```text
B = [ID, Name, ActionCount, Actions..., ChaseTargetDistance, KeepAwayTargetDistance]
```

| 索引/段 | 字段 | 类型 | 含义 |
|---------|------|------|------|
| 0 | `ID` | int | AIConfig ID |
| 1 | `Name` | string | AIConfig 名称，当前由 `TbCharacterAI.fightAI` 引用 |
| 2 | `ActionCount` | int | 后续动作数量 |
| 3..N | `Actions` | int[] | 引用 `SplitAIActionData.ID` |
| N+1 | `ChaseTargetDistance` | int | 追击距离 cm |
| N+2 | `KeepAwayTargetDistance` | int | 远离距离 cm |

统计：

| 项 | 结果 |
|----|------|
| ActionCount 分布 | 0-12 |
| 引用 AIAction 数 | 332 |
| 缺失 AIAction | 0 |

### 3.3 AIConfig_Defense 位序

来源代码：`Assets/Scripts/Ability/Data/AI/GOAPAIConfigReactData.cs`

```text
B = [ID, Name, ActionCount, Actions...]
```

| 索引/段 | 字段 | 类型 | 含义 |
|---------|------|------|------|
| 0 | `ID` | int | React Config ID |
| 1 | `Name` | string | React Config 名称，当前由 `TbCharacterAI.reactAI` 引用 |
| 2 | `ActionCount` | int | 后续动作数量 |
| 3..N | `Actions` | int[] | 引用 `SplitAIActionData.ID` |

统计：

| 项 | 结果 |
|----|------|
| ActionCount 分布 | 0-8 |
| 引用 AIAction 数 | 15 |
| 缺失 AIAction | 0 |

### 3.4 AIGoals 位序

来源代码：`Assets/Scripts/Ability/Data/AI/GOAPAIGoalData.cs`

```text
B = [ID, Name, GoalFight[], GoalReact[]]
```

| 索引 | 字段 | 类型 | 含义 |
|------|------|------|------|
| 0 | `ID` | int | Goal ID |
| 1 | `Name` | string | Goal 名称，当前由 `TbCharacterAI.goal` 引用 |
| 2 | `GoalFight` | GOAPCondition[] | 进攻目标条件 |
| 3 | `GoalReact` | GOAPCondition[] | 反应目标条件 |

判断：

- `AIConfig` 三个分组虽然都叫 `B`，实际结构不同，必须拆成三个 Schema。
- `Name` 是当前桥接键，迁移时应保留唯一性校验，并逐步改为 ID 引用。
- `FatOgre_3` 缺失属于关系层 warning，不能在结构层忽略。

## 4. SplitBuffData

### 4.1 结构规模

| 项 | 结果 |
|----|------|
| 有效 JSON 文件 | 178 |
| `_index.json` | 1 |
| 顶层结构 | `{ "Type": "...", "Buff": {...} }` |
| `Buff.Data` 长度 | 178/178 均为 9 |
| `TbBuff` 覆盖 | `TbBuff` 60 个 ID 均有 Graph |
| Graph-only Buff | 118 |

Type 分布：

| Type | 数量 | 额外字段 |
|------|------|----------|
| `Numerical` | 49 | `N` |
| `Condition` | 6 | `C` |
| `ChangeAttr` | 13 | `SBuff` |
| `DamageByAttr` | 19 | `Values`, `Eff` |
| `CastOrbBezier` | 7 | `S`, `I` |
| `CastOrbTrack` | 12 | `S`, `I` |
| `CastOrbLinear` | 29 | `S`, `I` |
| `Positive` | 17 | `D` |
| `Status` | 26 | `D` |

### 4.2 公共 Buff.Data 位序

来源代码：`Assets/Scripts/Ability/Data/Buff/BuffData.cs`

| 索引 | 字段 | 类型 | 含义 | 建议命名 |
|------|------|------|------|----------|
| 0 | `ID` | int | Buff ID | `id` |
| 1 | `Name` | string | 内部名称 | `name` |
| 2 | `Desc` | string/null | 内部描述 | `description` |
| 3 | `Target` | enum string | 数值来源/目标 | `target` |
| 4 | `AddType` | enum string | 堆叠策略 | `stacking` |
| 5 | `AddNum` | int | 堆叠数量 | `stackCount` |
| 6 | `Duration` | int | 持续时间 ms | `durationMs` |
| 7 | `HitCooldown` | int | 可伤害间隔 ms | `hitCooldownMs` |
| 8 | `ShowHeadIcon` | bool | 是否显示图标 | `showHeadIcon` |

判断：

- 公共字段适合进入 `Buff.tsv` 或 `BuffIndex.tsv`。
- 但 `Graph-only Buff` 很多，`Buff.tsv` 必须允许 `visibility=internal`。

### 4.3 类型字段摘要

| Type | 结构 | 关键字段 |
|------|------|----------|
| `Numerical` | `N[10]` | 属性、加值、乘值、开始特效、是否增益、中立、尺寸、世界脱离 |
| `Condition` | `C[3]` | 添加 Buff 列表、移除 Buff 列表、条件列表 |
| `ChangeAttr` | `SBuff[6]` | 属性、加值、乘值、特效、部位、尺寸 |
| `DamageByAttr` | `Values[] + Eff[9]` | 伤害值表达、特效、伤害类型、元素、伤害基础类型 |
| `CastOrb*` | `S[] + I` | 命中 Buff、目标、起止位置、特效、命中特效、伤害/元素、命中技能、移动参数 |
| `Positive` | `D[2]` | 被动属性类型、数值 |
| `Status` | `D[1]` | 状态类型，属性影响由 `StatusBuffType` 推导 |

`CastOrb*` 共享 `SBuffUseSkillData.S` 的长数组，然后每种弹道类型在数组尾部追加参数：

| Type | 追加参数 |
|------|----------|
| `CastOrbBezier` | speed、accelerate、delayTime、offsetRadius、offsetType |
| `CastOrbLinear` | speed、accelerate、delayTime、eulerAngle、lockEffectRotX/Y/Z |
| `CastOrbTrack` | speed、moveAcc、gravity、rotSpeed、rotAcc、delayTime、offsetRadius、offsetType、useParabola |

判断：

- Buff 新结构必须是 type-discriminated JSON，不应把所有字段摊平成一张宽表。
- `Condition.AddBuffList/RemoveBuffList` 和 `CastOrb.HitBuffs` 是 Buff 内部引用，需要纳入引用校验。
- `CastOrb.HitSkill` 引用 `SkillData`，但当前样本未能仅靠数组统计可靠确认，迁移前应按代码位序专门校验。
- `Status` 的属性影响不是显式字段，而是由 `StatusType` 推导，Schema 中需要记录 derived behavior。

### 4.4 新结构草案

```json
{
  "id": 70,
  "name": "Stagger",
  "visibility": "public",
  "target": "None",
  "stacking": "RefreshAllTimeAndAdd",
  "stackCount": 1,
  "durationMs": 6000,
  "hitCooldownMs": 200,
  "showHeadIcon": true,
  "type": "Numerical",
  "data": {
    "attribute": "Tenacity",
    "addValue": 0,
    "multiplyValue": -50000,
    "startEffect": "",
    "startEffectPart": 0,
    "isBuff": false,
    "effectDurationMs": -1,
    "isNeutralBuff": false,
    "effectScale": 1.0,
    "isOutToWorld": false
  }
}
```

## 5. 对统一配置的影响

这一轮补齐后，可以明确三条规则：

- 表格层保存可见、可排序、可筛选、可本地化的元数据。
- Graph 层保存复杂逻辑结构，必须具名字段和类型化 Schema。
- 运行时不区分 TSV/JSON 来源，只读取统一生成产物。

优先迁移试点仍建议选择 `AIAction`：

- 它的 Graph 结构固定。
- 它和 `TbAIAction` 一一对应程度高。
- 它引用 AbilityGraph，能验证 Graph 间引用。
- 它被 AIConfig 引用，能验证上游组合图。

## 6. 待补项

后续还需要单独补：

- `CastOrb.HitSkill` 的精确位序引用校验。
- Buff 条件中 `CheckConditionAttr` / `CheckConditionStatus` 的字段分布。
- AIAction 枚举值到可读名称的映射表。
- bytes 产物分类。
- SaveData 与配置数据的边界。
