# CharacterTest 属性设计

> 状态：当前指南
> Status: Current
>
> 版本：0.2 | 2026-10-01
>
> 范围：CharacterTest 专用属性、伤害、散势压力、UI / Debug / Telemetry 和未来扩展接入规则。

---

## 1. 定位

本文是 CharacterTest 属性体系的当前事实源。后续如果要修改 CharacterTest 的伤害、受击、散势压力、HeadPad、DebugUI、Buff、装备属性、Runtime AI Planner 传感器或数据分析输出，应先核对本文，再修改代码。

角色基础值现由 CharacterStudio 的 `config/character_attributes.json` 制作并编译到角色属性档案。新包 HP / Attack / Defense 使用 1 / 2 / 3，Stamina 保持 920002。CharacterTest 生成适配器将旧包 HP 920001 映射为 1；若同时有正式 HP 1，正式项优先且不创建旧 HP 副本。生命镜像上限读取权威 HP 的 BaseValue，聚限及恢复参数从同一档案初始化散势组件。制作数据与入口见 [CharacterStudio 数据归属](CHARACTER_STUDIO_DATA_OWNERSHIP.md)。

CharacterTest 只定义“如何在当前垂直切片中组合框架模块”。它不重新设计通用 `Attributes`、`Gameplay`、`Combat` 或 `Buffs` 模块。

2026-10-03 用户确认：角色 HP 归零即进入死亡状态；“停止自维持”是创世概念解释，不另建运行时死亡判据。角色破聚是受势后的状态，不是死亡；非角色语境的破聚可表示结构被毁掉，不能据此把角色破聚判作死亡。动势储量与卡牌费用使用同一资源，实际动作跨卡共享冷却，统一规则见[角色与卡组底层数据设计](../../StorySource/玩法基石/角色与卡组底层数据设计.md)。现有 Stamina 字段、卡牌账本与冷却记录是否已统一需另行核对，本次只改说明，不宣称代码已修复。

散势压力设计遵循 VVV Discussion #148 的方向：角色不是持有一条会被打空的“韧性血条”，而是累积 `ScatterDebt`；动作、支撑和硬直保护改变 `CohesionLimit`；UI 显示玩家可理解的压力和临时护框。

核心原则：

- CharacterTest 的普通属性权威状态是 `GameplayComponentWorld + GameplayAttributeSetComponent`。
- CharacterTest 的散势压力权威状态是 `GameplayScatterPressureComponent`；`CurrentScatterPressure` 对应设计语义中的 `ScatterDebt`。
- `CharacterTestCharacterCombatStateComponent`、HeadPad、DebugUI、日志和 Telemetry 只读或镜像权威状态，不反向写权威属性。
- Unity View、Animator、FairyGUI、Debug 面板和日志不得直接扣 HP、修改攻击/防御或改变散势压力。
- CharacterTest 伤害权威来自 CharacterAction / projectile 命中事件的通用 `hitEvent.damage` 数据结构。`damageProfileId`、旧顶层 `baseDamage/impactPower` 和 DamageProfile catalog fallback 不再是 CharacterTest 正式伤害链路。

## 2. 权威状态和镜像状态

### 2.1 普通属性权威

`GameplayAttributeSetComponent` 是 CharacterTest 当前普通属性的唯一权威源。它保存每个属性的 `AttributeId`、`BaseValue` 和 `CurrentValue`。

当前权威读写路径：

- 初始化：`CharacterTestCharacterRuntimeWorld.Spawn` / `RegisterGameplayState` 创建 `GameplayAttributeSetComponent`。
- 读取：`CharacterTestCharacterRuntimeWorld.GetCurrentAttribute(...)`。
- HP 扣减：`CombatDamageApplicationSystem` 通过 `GameplayRuntimeCommandFactory.AddComponentAttribute(..., Hp, -finalDamage, traceId)` 写入 command。
- 直接调试或测试设置属性时，应通过 Gameplay attribute component/command 路径，不应写 `CombatState.Health`。

`GameplayAttributeSetComponent` 的 `BaseValue` 用于表达出生值或配置初始值；当前 CharacterTest v1 伤害只读取 `CurrentValue`。

### 2.2 散势压力权威

`GameplayScatterPressureComponent` 是 CharacterTest 当前散势压力的唯一权威源：

| 字段 / 概念 | 设计语义 | 说明 |
| --- | --- | --- |
| `CurrentScatterPressure` | `ScatterDebt` 散势债务 | 被命中、冲击、爆炸等事件累计上来的压力。它越高，越接近破聚。 |
| `MaxScatterPressure` | `BaseCohesion` | 基础抗断线。它不是一条会被打空的韧性血条。 |
| `MaxScatterDebt` | 最大散势债务 | `ScatterDebt` 允许累积到的上限。动作支撑可以让债务超过基础抗断线，但不能无限累积。 |
| `CurrentCohesionLimit` | 当前抗断线 | 当前动作和状态下的破聚阈值。无动作支撑时等于 `BaseCohesion`；有支撑时由动作支撑参数推导。 |
| `RecoveryRate` | 债务恢复速度 | 恢复的是 `ScatterDebt`，不是恢复一个“韧性血量”。 |
| `RecoveryDelayFrames` | 债务恢复延迟 | 最后一次受压后等待多少帧才开始自然回落。 |
| `BrokenRecoveryDelayFrames` | Broken 定时全恢复窗口 | CharacterTest 启用后，Broken 期间停止新增散势债务，到期一次性清空 `ScatterDebt`。 |
| `LastCohesionBreakFrame` | 上次破聚帧 | 首次进入 Broken 时记录，用于定时全恢复窗口；后续 Broken 期间命中不刷新它。 |
| `Band` | `CohesionBreakRisk` 风险段 | 按 `ScatterDebt / CurrentCohesionLimit` 推导，用于 UI、Debug 和 Runtime AI Planner facts。 |
| broken 状态 | 已破聚 | 表示本次债务已经越过当前抗断线并触发 `CohesionBreakEvent`。 |

散势压力系统必须区分三个量：

- `ImpactPower`：动作命中事件配置的冲击强度，是攻击侧提供的基础冲量输入。
- `ScatterImpact`：一次命中、爆炸、推撞或状态 tick 实际施加的散势冲击。CharacterTest 当前由 `ImpactPower` 结合双方质量和命中部位 `ImpulseScale` 派生；`baseDamage` 只代表 HP 伤害。
- `ScatterDebt`：角色已经累积的散势债务。当前映射到 `GameplayScatterPressureComponent.CurrentScatterPressure`。
- `CohesionLimit`：当前动作和状态下的抗断线。没有动作支撑时使用基础抗断线；有动作支撑时由 `CombatCohesionLimitAdapter.CalculateCohesionLimit(basePressure, supportProfile)` 推导。

破聚风险不是直接看 `CurrentScatterPressure / MaxScatterPressure` 的固定血条比例，而是：

```text
cohesionBreakRisk = ScatterDebt / CurrentCohesionLimit
```

`PressureBand` 应表达这个风险段：`Stable -> Pressed -> Cracked -> Critical -> Broken`。临时支撑、霸体窗口或重动作护框改变的是 `CurrentCohesionLimit` 或破聚响应策略，不应清空 `ScatterDebt`。

普通 attribute 中的 `ScatterPressure` id 不是破聚判定源。它只保留为 CharacterTest 早期展示 / 兼容桥，不能用于：

- 判断是否破聚。
- 显示当前韧性压力条。
- 给 Runtime AI Planner 提供韧性压力事实。
- 参与 `CohesionBreakEvent` 发布条件。

### 2.3 CharacterTest 镜像状态

`CharacterTestCharacterCombatStateComponent` 是 CharacterTest 层的战斗表现镜像，当前用于 HeadPad、DebugUI、运行后排错和简单受击反馈。它可以镜像：

- 当前 HP 和 max HP。
- hit-stun / reaction frames。
- last hit 信息。
- scatter pressure / cohesion limit / risk band。
- last cohesion break frame。

它不得成为属性权威事实源。任何实现发现 `CombatState.Health` 和 `GameplayAttributeSetComponent.Hp` 不一致时，应修正镜像同步路径，而不是让 UI 或 CombatState 直接覆盖 Gameplay 属性。

## 3. CharacterTest v1 属性表

| 属性 | ID | 默认值 | 权威组件 | 作用 |
| --- | ---: | ---: | --- | --- |
| `Hp` | `1` | `100` | `GameplayAttributeSetComponent` | 当前生命值。已接受伤害通过 Gameplay attribute command 扣减。 |
| `Attack` | `2` | `20` | `GameplayAttributeSetComponent` | 攻击属性。CharacterTest damage resolver 的攻击者属性缩放读取它。 |
| `Defense` | `3` | `10` | `GameplayAttributeSetComponent` | 防御属性。`ct.soft_defense_v1` 固定读取它进入软防御曲线。 |
| `ScatterPressure` | `4` | `100` | `GameplayAttributeSetComponent` | 普通展示 / 兼容字段，不等于 `ScatterDebt` 或 `CohesionLimit`，不驱动破聚。 |

韧性压力参数：

| 参数 | 默认值 | 权威组件 | 作用 |
| --- | ---: | --- | --- |
| `DefaultScatterPressureMax` | `100` | `GameplayScatterPressureComponent` | 当前实现命名下的基础抗断线 `BaseCohesion`。 |
| `DefaultScatterPressureMaxScatterDebt` | `100` | `GameplayScatterPressureComponent` | 默认最大姿态债务。动作支撑提高当前抗断线时，运行时会确保债务上限不低于当前抗断线和已累积债务。 |
| `DefaultScatterPressureRecoveryRate` | `2` | `GameplayScatterPressureComponent` | 恢复延迟结束后每 tick 回落的 `ScatterDebt`。 |
| `DefaultScatterPressureRecoveryDelayFrames` | `360` | `GameplayScatterPressureComponent` | 最后一次增加 `ScatterDebt` 后等待多少帧才自然恢复；60fps 下约 6 秒。 |
| `DefaultCohesionBreakRecoveryDelayFrames` | `270` | `GameplayScatterPressureComponent` | CharacterTest 进入 Broken 后的定时全恢复窗口；到期一次性把 `ScatterDebt` 清为 `0`。 |
| `DefaultCohesionBreakReactionFrames` | `18` | CharacterTest combat mirror | v1 破聚瞬间和 Broken 窗口内受击使用的可见长反应帧数；不是 270f 持续硬直。 |

## 4. Spawn 初始化规则

每个 CharacterTest 角色生成时必须完成以下注册：

1. 注册 Combat entity 到 Gameplay entity 的映射：`CombatEntityGameplayMap.Register(...)`。
2. 创建 `GameplayAttributeSetComponent`，包含 `Hp`、`Attack`、`Defense`、`ScatterPressure`。
3. 创建 `GameplayScatterPressureComponent`。
4. 初始化 CharacterTest combat mirror，使 DebugUI / HeadPad 能读取初始 HP 和 scatter pressure 状态。

Spawn definition、角色配置、默认模板或测试 helper 可以决定初始数值，但最终进入运行时后，权威状态必须落在 Gameplay component world。

## 5. 伤害和受击数据流

CharacterTest 伤害链路必须保持统一：

```text
CharacterAction timeline
  -> Combat request / Gameplay effect request
  -> damage source context
  -> Common hitEvent.damage payload
  -> CharacterTest damage resolver
  -> HitCandidate
  -> HitResolveSystem
  -> CombatDamageApplicationSystem
  -> Gameplay attribute command / ScatterDebt request
  -> Gameplay systems
  -> CombatState mirror / HeadPad / DebugUI / telemetry
```

### 5.1 DamageRequest 来源

当前 CharacterTest 已验证的来源：

- `WeaponTrace`：武器轨迹命中窗口。
- `AreaPulse`：带通用 `hitEvent.damage` payload 和 area target policy 的 `ApplyGameplayEffect`；缺少 `hitEvent.damage` 时视为配置错误。

未来允许的来源：

- `ProjectileImpact`
- `StatusTick`
- `Scripted`

所有来源都应进入同一条通用伤害结算链路，并携带来源上下文。不要为 Buff、投射物或脚本伤害另建一套扣 HP 路径。

### 5.2 CharacterTest damage resolver 语义

CharacterTest v1 的默认推荐公式是 `ct.soft_defense_v1`。它是确定性 ARPG 软防御公式，不包含随机暴击、随机浮动、弱点倍率、标记层数、处决倍率或破聚后的 HP 增伤。

```text
offense = baseDamage + Attack * attackScale
mitigation = defenseWeight * Defense / (Defense + defenseReference)
raw = offense * (1 - mitigation) * takenDamageMultiplier
incomingDamage = roundAwayFromZero(raw)
incomingDamage = max(0, incomingDamage)
```

说明：

- `sourceAttributeScales` 在默认公式下读取攻击者攻击属性，推荐 `Attack * 0.65` 这类正向缩放。新配置只保留攻击属性收益，不再暴露目标防御取值比例。
- `ct.soft_defense_v1` 固定读取目标 `Defense(3) * 1.0`，不让单条伤害事件配置目标防御取值。
- `defenseReference` 控制防御曲线变缓的位置；当前 CharacterTest 全局默认 `50`。
- `defenseWeight` 控制防御最多能吃掉多大比例；当前 CharacterTest 全局默认 `0.60`，即软上限约 60% 减伤。
- `takenDamageMultiplier` 是运行时上下文可提供的受伤倍率，例如命中区域、身体部位、动作阶段、Buff、装备或难度系数。它不属于单条伤害事件的普通配置字段。
- `defenseReference` 和 `defenseWeight` 属于 CharacterTest 软防御公式的全局参数，不属于单条伤害事件。默认曲线参数 `50 / 0.60` 不应重复写入每个 hit window；如果要表达破甲、无视防御或护甲类型克制，应设计明确机制，而不是通过单条伤害事件改防御曲线参数。
- `ct.soft_defense_v1` 的 `defenseReference / defenseWeight` 是 CharacterTest resolver 固定基准，不再从 damage profile catalog root 级默认值读取。新 Action 时间轴内联配置不应把 `defenseReference` / `defenseWeight` 写到单个事件上。
- CharacterTest v1 不支持在单条伤害事件上配置防御穿透、防御倍率或防御取值比例。以后如果需要防御穿透、抗性、护甲类型克制，应作为独立机制设计，不能复用 `targetAttributeScales`。
- 最终只做非负保护，不做 `minDamage` / `maxDamage` 上下限 clamp。
- `minDamage` / `maxDamage` 只能作为旧 JSON 兼容字段存在，不能作为 CharacterTest v1 配置入口或数值平衡手段。
- `impactPower` 只表示并行的冲击强度，不乘到 HP damage 上；破聚也不反向给 HP damage 加成。运行时会由它派生 `ScatterImpact` 和击退初速度。
- 旧 `Flat`、`AttackMinusDefense`、`AttackScaledMinusDefense` 公式只作为旧数据和测试兼容路径保留，新 CharacterTest 配置优先使用 `ct.soft_defense_v1`。

### 5.2.1 CharacterTest v1 数值基准

当前 CharacterTest 的第一套平衡锚点是“同级轻击身体命中”：

| 基准项 | 数值 | 设计目标 |
| --- | ---: | --- |
| 攻击者 `Attack` | `20` | 默认同级角色攻击属性。 |
| 目标 `Hp` | `100` | 默认同级角色生命值。 |
| 目标 `Defense` | `10` | 默认同级角色防御属性。 |
| 目标 `BaseCohesion` | `100` | 默认基础抗断线。 |
| 命中部位 | `hit.torso` | 身体命中倍率 `1.0`，作为所有轻击纸面伤害的默认对照。 |

`light_attack` 默认命中窗口使用：

```text
baseDamage = 6
attackScale = 0.65
impactPower = 34
```

在默认同级身体命中下：

```text
offense = 6 + 20 * 0.65 = 19
mitigation = 0.6 * 10 / (10 + 50) = 0.10
raw = 19 * 0.90 = 17.1
incomingDamage = 17
```

平衡目标：

- `light_attack` 身体命中默认约 `17% HP`，6 次身体命中击倒 100 HP 同级目标。
- 头部 `1.35x` 约 23 点，5 次头部命中击倒；手部 `0.85x` 约 15 点，7 次手部命中击倒。
- 同质量、身体命中部位 `ImpulseScale=1.0` 时，`impactPower=34` 会派生约 `ScatterImpact=34`，并产生约 `1.0m` 的一次性水平击退预估距离。
- 约 `ScatterImpact=34` 会让无动作支撑目标第 1 次命中接近 `Pressed`，第 2 次进入 `Cracked`，第 3 次触发 `Broken`。
- 未 Broken 前，恢复以 PvE stance break 的持续施压语义为基准：轻击之间只要没有超过 `360f` grace window，就不会自然回落；超过窗口后按 `2/f` 快速清理 `ScatterDebt`。
- 进入 Broken 后，CharacterTest 停止新增 `ScatterDebt`，后续命中不刷新破聚计时；从首次破聚帧起满 `270f` 后一次性把 `ScatterDebt` 清为 `0` 并回到 `Stable`。
- 轻击三连是默认普通目标的稳定破聚基准：第 1/2 次提供明确压制反馈，第 3 次破聚。重击、蓄力、技能和反击以后应以更高 `impactPower`、更强动作支撑或专门冲击倍率区分。
- 以后调轻击时优先维持上述击杀次数和破聚次数，再调整动画窗口、命中半径或动作恢复帧。

### 5.3 HitResolveSystem 职责

`HitResolveSystem` 只判断命中是否成立：

- self
- friendly
- duplicate / hit-once
- blocking
- invincible
- super armor

它不直接读取 Gameplay attributes，也不直接扣 HP。

### 5.4 DamageApplication 职责

`CombatDamageApplicationSystem` 是 accepted / blocked 命中结果进入 Gameplay 的桥。

规则：

- `HitResolveKind.Damage`：使用 `finalDamage` 输出 `AddComponentAttribute(Hp, -finalDamage)`。
- accepted 且 `impactPower > 0`：通过 CharacterTest 冲击响应解析得到 `ScatterImpact`，再输出 `GameplayScatterPressureRequest` 来增加 `ScatterDebt`。
- accepted / blocked 且冲击响应得到击退初速度时：向目标写入一次 `CharacterTestCharacterMotionRequest.HorizontalVelocityOverride`；击退初速度不与命中前的攻击推进速度相加，且压住同帧水平 root motion，后续实际位移继续由 motion authority 的摩擦、碰撞和减速统一结算。普通外部冲量仍使用 `Impulse` 的加法语义。
- `HitResolveKind.Blocked`：不扣普通 HP；如果伤害事件有 `impactPower`，仍可派生 `ScatterImpact` 和击退，用于表达格挡受压、盾牌震退和破绽风险。
- `Duplicate` / `Friendly` / `SelfDamage`：只写 telemetry / debug 结果，不改 HP，不改 `ScatterDebt`。
- armor absorb 后的 `finalDamage` 才能作为 HP delta。

## 6. 散势压力和破聚

散势压力是 pressure component，不是普通 HP-like attribute。它的核心是“债务累计 + 当前抗断线”，不是“韧性值被打空”。

当前 v1 行为：

- accepted / blocked 命中的 `impactPower` 通过 CharacterTest 冲击响应解析为 `ScatterImpact` 后，再转为 `GameplayScatterPressureRequest`。
- CharacterTest 每帧从当前动作读取 `cohesionSupport`；只有配置声明了显式 support window 且当前 local frame 落在窗口内时，才转换为 `CombatActionSupportProfile` 并向 `GameplayScatterPressureSystem` 写入提高后的 `CohesionLimit`。
- `GameplayScatterPressureSystem` 运行后更新 `ScatterDebt`、`CurrentCohesionLimit` 和 `PressureBand`。
- 首次进入 Broken 段时发布 `CohesionBreakEvent`。Broken 应表达 `ScatterDebt >= CurrentCohesionLimit`。
- 未 Broken 时，`RecoveryDelayFrames=360` 且 `RecoveryRate=2/f`：最后一次受压后满 360f 才开始逐步恢复。
- CharacterTest 启用 `BrokenRecoveryDelayFrames=270`：Broken 期间正向散势请求不增加 `ScatterDebt`、不刷新 `LastCohesionBreakFrame`、不重复发布 `ScatterImpactResolvedEvent` 或 `CohesionBreakEvent`。
- Broken 期间 `CurrentCohesionLimit` 可以随动作支撑更新，但不会因此提前退出 Broken；只能在 270f 到期时一次性清空 `ScatterDebt` 并回到 `Stable`。
- CharacterTest cohesion break bridge 收到 break 后，先通过目标当前武器 / 装备 `ActionSet.reactionProfileId` 选择受击反应动作，再绕过普通 cancel window 取消目标当前 CharacterAction 并启动选中的 Reaction action。默认 katana 动作组中，破聚瞬间使用 `katana_cohesion_break_react`，持续 `18f`，并请求示例受击动画 `art.character.skeleton.animation.damage_stun01_start`；`CombatState.ApplyCohesionBreak(18f)` 只保留为 UI / Debug / HeadPad 镜像。`270f` 不是持续硬直；reaction action 结束后目标可正常行动，但 Broken 窗口内后续每次有效直接命中都会按 `Hit + Broken` reaction rule 强制取消当前 CharacterAction 并从第 0 帧重启选中的 Reaction action，同时照常扣 HP、击退、刷新受击表现镜像，且不增加 `ScatterDebt` 或刷新 `LastCohesionBreakFrame`。
- 默认 katana Broken-hit 受击组按上 / 中 / 下段与冲击强度二维分流：`reaction.height.high/mid/low` 由命中区 / 身体部位归一得到，`impactForce 0..59` 走普通 `katana_hit_high/mid/low_react`，`impactForce >=60` 走 `katana_hit_high/mid/low_strong_react`。普通未 Broken 命中仍不启动受击硬直动作。
- 目前从外部 Damage 动画集中引入的人形示例资源包括：`Damage_Stun01_start / loop / end`、`Damage_KO_Mid_2`、`Damage_High_F`、`Damage_Mid_F`、`Damage_Low_L1`、`Damage_Strong_L_Head`、`Damage_Strong_L_Chest`、`Damage_Strong_L_Foot`。默认 katana 破聚 / Broken-hit reaction actions 使用这些 sample clip；loop/end/KO 作为后续 stun/death 动作组配置样例，不改变未 Broken 普通命中的打断语义。
- DebugUI / combat mirror 读取 `ScatterDebt`、当前抗断线、风险段、`CohesionBreak` 状态和 Broken 全恢复窗口；HeadPad 运行时以图形化压力细条展示 `ScatterDebt / CurrentCohesionLimit`、风险阈值和当前抗断线，并在破聚瞬间显示临时强调效果。`CombatReadout` 下 Broken 且启用定时全恢复时，HeadPad 只读镜像当前帧、`LastCohesionBreakFrame` 和 `BrokenRecoveryDelayFrames`，叠加恢复充能进度；`HealthOnly` 不显示该进度，`Hidden` 全部隐藏。动作护框细分仍属于后续 UI 表现层。

### 6.1 特殊受击：击飞 v1

击飞是 CharacterTest 第一种特殊受击状态，不是普通受击硬直动作，也不是 `impactPower` 的强冲击分支。它的正式输入是独立的 `launchPower`：

- `launchPower=0` 表示本次命中没有击飞输入；`launchPower=100` 表示标准人形击飞。
- 首次进入击飞必须要求目标已经处于 Broken；普通未 Broken 命中即使配置 `launchPower > 0` 也不进入击飞。
- 击飞不看 `impactPower` 阈值；`reaction.input.launch` 只保留为日志、分析或旧配置兼容标签，不作为正式触发条件。
- 目标质量只缩放击飞速度，不作为是否可击飞的阈值；是否可击飞由目标承受能力配置决定，例如 `CanBeLaunched`。

击飞 v1 只提供垂直速度，水平位移继续由现有 `impactPower -> knockback` 链路负责：

```text
massScale = clamp(sqrt(80 / targetMassKg), 0.6, 1.35)
velocityY = clamp(7.5 * launchPower / 100 * massScale, 0, 15.0) meters/second
```

其中 `targetMassKg` 缺失时按 `80kg` 处理；`targetMass=80kg` 且 `launchPower=100` 时得到约 `7.5m/s` 的向上速度。

状态规则：

- 特殊受击状态为 `Launched`，内部阶段为 `Enter / Airborne / LandingRecovery`。
- `Launched` 期间关闭普通行动、输入移动和 Runtime AI Planner，但仍可承受 HP 伤害。
- `Launched` / `LandingRecovery` 会暂时保持 Broken，不让 270f 全恢复窗口在空中提前结束。
- 已经 `Launched` 时再次受到 `launchPower > 0` 命中，只追加一次向上速度，不重新进入一个新的特殊受击状态。
- 落地后进入默认 `12f` `LandingRecovery`，结束后清除 `Launched`。
- 死亡、复活和销毁优先级高于击飞；`Dying / Dead / Reviving / PendingDestroy` 不可进入击飞。

动作和配置边界：

- ReactionProfile 仍只负责选择击飞表现 action，例如 `Hit + Broken + reaction.special.launch -> katana_launch_react`。
- CharacterAction 时间轴负责动画、空中姿态和运动事件；是否击飞由命中效果值和目标承受能力决定。
- 巨型、Boss 或明确不可击飞目标应配置为 `CanBeLaunched=false`；即使命中带 `launchPower`，也回落到现有 Broken-hit 硬直。

### 6.2 动作支撑规则

- 普通站立或无动作支撑时，`CurrentCohesionLimit = BaseCohesion`，当前可由 `DefaultScatterPressureMax` 兜底。
- 动作有显式支撑窗口且当前帧在窗口内时，使用 `CombatCohesionLimitAdapter.CalculateCohesionLimit(basePressure, CombatActionSupportProfile?)` 计算当前抗断线；无窗口或窗口外按无支撑处理。
- 普通 `light_attack / light_attack_2 / light_attack_3` 不应因为出手本身全程抬高 `CurrentCohesionLimit`。需要护框或霸体的动作必须配置明确窗口。
- 重攻击、蓄力、处决、受击保护或霸体窗口可以提高当前抗断线或延迟破聚响应，但不应清空 `ScatterDebt`。
- 支撑结束后，抗断线回到基础值；如果此时已有 `ScatterDebt` 超过基础线，CharacterTest v1 会在抗断线更新帧触发 Broken，并发布 `CohesionBreakEvent`。

UI 语义：

- 韧性压力条显示 `ScatterDebt / CurrentCohesionLimit`，而不是显示一个会被扣空的韧性 HP。
- 临时动作支撑应显示为护框、支撑线或另一层阈值提示，让玩家理解“现在更难被打断”。
- `Pressed`、`Cracked`、`Critical` 是破聚风险段，不是剩余韧性百分比。

后续扩展必须保持：

- Runtime AI Planner 只能通过 adapter 读取 pressure facts，不能直接读写 Gameplay component store。
- 专用破聚动画、ReactionProfile 或处决窗口可以消费 `CohesionBreakEvent` 并选择 CharacterAction，但不能改变 pressure component 的权威写入规则。
- 普通 attribute 中的 `ScatterPressure` 展示 / 兼容字段如需废弃，应走单独迁移任务，不能在功能代码中悄悄改变语义。

当前实现边界：

- `GameplayScatterPressureSystem` 已按 `ScatterDebt / CurrentCohesionLimit` 推导 `PressureBand`。
- CharacterTest 已把当前动作的 `cohesionSupport` 接入 scatter pressure bridge。
- CharacterTest 已启用 Broken 后 `270f` 定时全恢复；通用 Gameplay 使用者默认未启用该模式，继续保持旧逐步恢复兼容语义。
- DebugUI 和 combat mirror 已使用当前抗断线；HeadPad 已用图形化压力条显示风险阈值和当前抗断线，并用 MOBA 风格分段血条表达血量规模。动作护框细分仍属于后续 UI 表现任务。
- 击飞特殊受击以本文 `launchPower` 规则为目标基准；任何仍通过 `reaction.input.launch` tag 或 `impactPower` 阈值触发击飞的实现都属于待迁移兼容路径，不能继续扩展为正式规则。

## 7. 配置边界

### 7.1 Action 时间轴

动作时间轴只配置：

- 什么时候开启/关闭命中窗口。
- 使用哪个 `equipmentSlotId`。
- 本次命中的通用伤害配置：`hitEvent.damage` / damage payload 中的 `damageTypeId`、`formulaId`、`baseDamage`、`sourceAttributeScales`、`impactPower`、`launchPower`、`tags`、`analysisCategory`。
- 非武器伤害使用哪个 `targetPolicyId`。
- 动作自身的支撑/抗断参数可以通过 `CombatActionSupportProfile` 表达，例如支撑率、最低支撑线和显式霸体窗口；这属于动作防打断能力，不是伤害数值。

动作时间轴不配置：

- 武器专属 trace id。
- 武器长度/半径。
- 目标 Defense 取值比例、防御穿透或防御曲线参数。
- 直接修改 `ScatterDebt` 或清空韧性压力。

### 7.2 通用伤害字段

CharacterTest 正式配置入口是通用 `hitEvent.damage` 数据结构。武器命中、范围伤害、投射物 direct hit、状态 tick 和脚本伤害都必须由 owning config/request 提供这一组通用伤害字段：

- `damageTypeId`
- `formulaId`
- `baseDamage`
- `sourceAttributeScales`
- `impactPower`
- `launchPower`
- `tags`
- `analysisCategory`

这些字段属于“本次命中的通用伤害数据”，不是武器配置，也不是受击动作组配置。新 CharacterAction / projectile authoring 只写 `hitEvent.damage`；旧 Action 顶层 damage 字段和 `damageProfileId` 现在会被 importer 判为配置错误。

`damageTypeId` 在 CharacterAction Authoring UI 中显示为“伤害性质”，默认新建值仍为 `slash / 斩击`。CharacterTest 当前伤害公式不按该字段分支；它用于日志、分析分类、筛选、反应/表现扩展和后续元素语义，不承担当前 HP 数值公式输入。

`impactPower` 在 CharacterTest 韧性语义中表示“冲击强度”。它不是最终击退距离，也不是直接写入的 `ScatterImpact`。运行时使用：

```text
massTransfer = 2 * sourceMassKg / (sourceMassKg + targetMassKg)
effectiveImpact = impactPower * massTransfer * hitZoneImpulseScale
ScatterImpact = roundAwayFromZero(effectiveImpact)
drive = max(0, effectiveImpact - 5 * sqrt(targetMassKg / 80))
previewDistanceMeters = 10 * (1 - exp(-drive / 276))
initialKnockbackSpeed = sqrt(2 * effectiveDecel * previewDistanceMeters)
```

其中质量来自角色包 `geometry.bodyProfile.massKg`，缺失时使用 `80kg`；`hitZoneImpulseScale` 来自命中区域/身体部位解析；`effectiveDecel` 来自目标运动配置、地面摩擦和当前地表摩擦。轻击 `impactPower=34` 的默认预估距离约 `1.0m`；击退是一次瞬时 `Impulse`，不是持续 `ExternalForce`。

`launchPower` 在 CharacterTest 特殊受击语义中表示“击飞效果值”。它不派生 `ScatterImpact`，不参与水平击退预估，也不使用 `impactPower` 的强冲击阈值。`launchPower=100` 是标准人形击飞基准；运行时结合目标质量和可击飞承受能力解析为垂直向上速度。未 Broken 的目标不会因为 `launchPower > 0` 直接击飞。

Action Damage Analysis 工具和 CharacterAction 命中数值面板必须把伤害字段显示为 `baseDamage / HP 伤害`、`Attack scale / 攻击力收益`、`ImpactPower / 冲击强度`、`launchPower / 击飞效果值` 与 `damageTypeId / 伤害性质`，并直接显示 CharacterTest 默认基准：轻击身体命中 `HP 6 + Attack x0.65 / 冲击 34`，同级普通目标 `BaseCohesion 100`，连续第 3 次轻击破聚。工具可以同时预览派生的 `EffectiveImpact`、`ScatterImpact`、击退初速度、预估距离和标准人形击飞速度，也可以提供 `BaseCohesion`、动作承托率、当前 `ScatterDebt` 和连续命中次数的模拟预览，但这些预览参数不能保存为伤害权威数据，也不能替代角色/动作侧的 `CohesionLimit` 配置。

CharacterTest v1 不使用独立 `GuardPressure` 作为攻击档案配置项。格挡受压、破防倾向和压制效果统一通过 `ScatterImpact -> ScatterDebt -> CohesionLimit` 表达。通用框架里的 `GameplayGuardPressureComponent` / `GameplayGuardPressureSystem` 保留为其他项目或后续专门格挡系统的扩展点，但不作为 CharacterTest 当前权威链路。

通用伤害字段不配置最终命中反馈。`staggerFrames`、固定 `knockback` 距离、`impactForce` 和 `presentationProfileId` 即使作为旧 schema 字段被运行时兼容读取，也不能作为 CharacterTest 的权威反馈配置。`impactPower` 只提供冲击输入；`launchPower` 只提供击飞特殊受击输入；最终硬直、实际击退距离、打断和表现入口必须由命中结果、双方质量、目标运动摩擦、目标状态、韧性压力、特殊受击状态和动作支撑共同解析，不能由伤害事件单方面固定。

### 7.3 Weapon / Equipment

武器配置负责提供：

- `weaponId`
- 装备槽绑定。
- trace binding。
- 武器长度、半径、target mask。
- 武器标签或上下文。

武器配置不拥有动作伤害数字。不同动作使用同一武器时，应通过不同 hit window 的通用 damage 配置表达伤害差异。

### 7.4 Buff / DOT / 装备词条

Buff 和 DOT 未来只能作为 `DamageRequest(sourceKind=StatusTick)` 的生产者。Buff tick 内不能直接：

- 修改 `Hp`。
- 修改 `CombatState.Health`。
- 写 HeadPad 或 DebugUI 状态。
- 绕过 CharacterTest damage resolver 和 `CombatDamageApplicationSystem`。

装备词条、临时增益、属性加成和减益应通过 Gameplay attribute modifier / command bridge 接入。它们可以影响 `Attack`、`Defense` 或未来属性的读取结果，但不能绕过 `GameplayAttributeSetComponent`。

如果未来引入 `BaseCohesion`、`CohesionRecovery`、`ScatterImpactTakenScale` 等散势 / 聚势相关属性或组件：

- 装备和 Buff 可以通过 modifier 改基础抗断线、恢复速度或受到的 `ScatterImpact` 倍率。
- 它们不能直接把 `ScatterDebt` 清零，除非通过明确的 Gameplay command / pressure request 表达“主动释放债务”。
- 它们不能直接发布 `CohesionBreakEvent`；破聚事件仍由 pressure system 根据债务和当前抗断线统一发布。

## 8. UI、Debug 和 Telemetry

### 8.1 HeadPad

HeadPad 以图形化状态牌显示角色状态，但不拥有状态；除 HP 数字外不显示正文文本。

读取优先级：

- HP：从 Gameplay attribute 或 CharacterTest combat mirror 读取镜像后的权威值；HeadPad 用 MOBA 风格分段血条表达血量规模，仅显示当前 HP 数字。
- 聚势：2026-09-30 用户确认显示“剩余承压比例”。读取 `GameplayScatterPressureComponent` 的同帧镜像，以 `clamp01(1 - ScatterDebt / CurrentCohesionLimit)` 表达；Broken 锁存期间为 0，负压力或非正聚限明确不可用。真实解除 Broken 后再更新，不使用恢复计时充能提前填满，不保留压力刻度。它不是新聚势余额，动态聚限、散势、破聚及恢复继续归原 Gameplay owner；压力原值仍供 Debug/Telemetry 读取。
- 受击/破聚/死亡：从 CharacterTest combat mirror 读取，用边框颜色、短脉冲和血条追尾等表现动效表达，不显示状态文字。
- 显隐：初始自动隐藏；被本地玩家锁定的目标常显；非锁定 HeadPad 仅在 HP、散势压力、生死或状态表现变化时淡入显示，5 秒后淡出隐藏。
- 显示模式：`CombatReadout` 是当前默认完整图形化战斗读数；`HealthOnly` 只显示阵营标记、分段 HP 和当前 HP 数字；`Hidden` 关闭 HeadPad。模式由 CharacterTest 组合根或 `GameSlice.SetHeadPadDisplayMode` 作为 view-only 设置传入。

HeadPad 不得直接设置 Gameplay attribute，也不得根据 UI 动画反向改变角色状态。

### 8.2 DebugUI

DebugUI 用于运行时排错，应显示：

- entity id / team / control owner。
- HP current/max。
- Attack / Defense 当前值。
- `ScatterDebt` / `CurrentCohesionLimit` / `PressureBand`。
- last hit / common damage identity / last hit result。
- recent `DamageTelemetryRow`。

DebugUI 不作为分析数据源。可供数据分析的内容应来自结构化 telemetry。

### CharacterTest runtime physics profile

CharacterTest runtime physics values are not `GameplayAttributeSetComponent` stats. The authoritative body profile for character collision uses `CharacterTestCharacterPhysicsComponent` with float geometry and push fields: `capsuleRadiusMeters`, `capsuleHeightMeters`, `capsuleCenterY`, `massKg`, `massScale`, `radiusScale`, `pushScale`, `blocksCharacters`, and `canBePushed`.

The base values come from the imported character package `geometry.bodyProfile` when present; Iron Vanguard currently supplies radius/height/mass through that generated package. Missing package data falls back to the scene bridge half extents and `80kg`. Combat hit knockback, scatter pressure impact mass transfer, and character-to-character push collision must all read the effective mass from this runtime physics component so they use one mass vocabulary.

Buffs or equipment may eventually author physical modifiers, but until that pipeline exists they should enter through explicit runtime physics component updates. Do not store radius, height, or mass as integer gameplay attributes; `GameplayAttributeSetComponent` remains for CharacterTest HP/Attack/Defense/display-style stats.

### 8.3 Telemetry

`DamageTelemetryRow` 是伤害分析的结构化输出，应包含：

- runtime frame。
- source kind。
- action id / action instance / stable event id。
- common damage identity。
- equipment slot / weapon id。
- source / target entity。
- hit zone / body part。
- incoming damage / final damage。
- formula trace：`formulaId`、`baseDamage`、source attack / scale、`offense`、target defense、`defenseReference`、`defenseWeight`、`mitigation`、damage multipliers。
- scatter impact / scatter pressure before / scatter pressure after / current cohesion limit。
- hit result kind。
- HP before / HP after。

Debug log 只保留精简排错信息，不从日志反推数值分析。

## 9. 禁止规则

CharacterTest 属性相关开发不得做以下事情：

- 直接写 `CombatState.Health` 来扣血。
- 在 Animator、Unity View、FairyGUI 或 DebugUI 中修改权威属性。
- 在 Buff tick、投射物、脚本事件中直接扣 HP。
- 让动作时间轴写固定硬直、击退、防御压力或表现档等单边命中反馈。
- 让武器配置拥有动作伤害数字。
- 用普通 `ScatterPressure` attribute 判断破聚。
- 把 `MaxScatterPressure` 当成会被打空的韧性 HP。
- 在 UI / Debug 中只显示 `CurrentScatterPressure / MaxScatterPressure`，却不说明当前动作支撑线。
- 把 Runtime AI Planner 传感器做成 Gameplay component store 的写入者。
- 用 Debug log 作为数据分析口径。
- 因为缺通用伤害配置而回退到硬编码伤害。

## 10. 后续实现核对清单

后续按本文修正和开发时，应逐项检查：

- Spawn 是否为每个角色创建 attribute set 和 scatter pressure component。
- HP 修改是否只通过 Gameplay attribute command 或明确的 Gameplay component mutation 测试入口。
- Action Damage Analysis 是否只做动作伤害分析，不成为第二套 Action 编辑器。
- CharacterAction timeline 是否使用 `hitEvent.damage` 和 `equipmentSlotId`，并且没有 `damageProfileId` 或旧顶层伤害字段。
- Weapon trace / equipment 数据是否不拥有伤害数值。
- HeadPad / DebugUI 是否只读镜像或权威查询结果，并显示韧性债务、当前抗断线和风险段。
- DamageTelemetryRow 是否覆盖运行后分析所需字段，包括 HP 变化和韧性债务变化。
- Buff / DOT / 装备属性扩展是否进入统一 DamageRequest 或 Gameplay attribute modifier / command bridge。
- Unity MCP 编译、CharacterTest damage integration tests、Gameplay scatter pressure tests 和 common damage payload tests 是否覆盖变更。
