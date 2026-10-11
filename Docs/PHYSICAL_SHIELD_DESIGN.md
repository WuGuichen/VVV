# 物理防护盾三势韧性设计

> Status: Current
>
> Version 0.2.0 | 2026-06-22
>
> Scope: Design + noEngine Combat Runtime v1 + CharacterTest sample skill/VFX
>
> Delivery level: noEngine Combat runtime implementation plus CharacterTest sample integration. This document defines the shield model, records the runtime API now implemented under `MxFramework.Combat.Shields`, and tracks the sample skill/VFX wiring used to verify the framework path.

## 1. 设计目标

物理防护盾是跟随拥有者的前方 Combat Object。它用 noEngine Combat 物理参与投射物和近战命中遮挡，但不作为移动障碍，也不依赖 Unity `Collider`、`Rigidbody`、`OnTriggerEnter` 或 `UnityEngine.Physics` 作为权威逻辑。

必须满足：

- 护盾拥有独立权威状态，不新增 `ShieldDamage`、`ShieldDurability`、`ShieldHp` 这类普通角色属性。
- 护盾三势使用现有语义 `Motion / Cohesion / Scatter`；中文“三势”只作为文档和 UI 展示名。
- 破盾采用角色韧性式“压力 + 当前凝聚限 + 风险段”模型，不采用被扣空的耐久血条模型。
- 未破盾时，护盾挡住的命中不再继续伤害拥有者；破盾命中本身仍由护盾消费，后续命中才可穿过。
- 投射物、近战 Trace、伤害档案、Buff 来源、Runtime hash、SaveState 和 diagnostics 都接入现有框架链路。

明确非目标：

- 不把护盾实现为 `GameplayArmorIntegrityComponent` 的别名。护甲完整度是 HP 吸收层，护盾是空间遮挡对象。
- 不给角色属性表增加护盾专用 AttributeId。
- 不引入 WGame 特化元素、角色、关卡或真实 Buff 配置。
- 不新增 Unity 序列化资产、场景、Prefab 或可玩入口。

## 2. API 复用计划

| 需求点 | 优先使用的框架 API / 模块 | 本设计是否使用 | 缺口或边界 |
| --- | --- | --- | --- |
| 权威固定帧 | `CombatFrame`、`CombatFrameClock`、`RuntimeHost` Combat bridge | 是 | 护盾状态随 Combat fixed step 推进，不读 Unity time。 |
| 物理体积 | `CombatPhysicsWorld`、`UpsertBody`、`UpsertCapsuleCollider` / `UpsertAabbCollider`、`RemoveCollider` | 是 | v1 推荐随身前方 Capsule；AABB 可作为低成本 fallback。 |
| 投射物遮挡 | `CombatProjectileRuntime`、`CombatProjectileCollisionConfig`、`ICombatProjectileImpactResolver` | 是 | 需要在 projectile impact resolver 中识别 shield hit 并提交护盾冲击。 |
| 近战 Trace | `CombatWeaponTraceEvaluator`、`WeaponTraceQueryBuilder`、`CombatPhysicsWorld.QueryCapsule` | 是 | 需要新增 shield occlusion filter；不能只依赖 `HitResolveSystem`。 |
| 命中结算 | `HitResolveSystem`、`HitResolveResult`、`HitResolveKind.Blocked` | 部分使用 | 护盾遮挡应在进入普通目标 `HitResolveSystem` 前消费或裁剪候选。 |
| 伤害输入 | `DamageProfileResolver`、`DamageProfileResolveResult`、`DamageProfileUsage` | 是 | 伤害结果只作为三势增量输入之一，不产生独立 shield damage 属性。 |
| 三势韧性 | `GameplayScatterPressureComponent/System` 的语义模式 | 使用模式，不直接依赖类型 | Combat 核心不引用 Gameplay 组件；当前实现定义 Combat 独立类型。 |
| 角色属性 | `AttributeStore`、`GameplayAttributeSetComponent` | 沿用现有角色属性 | 不新增护盾普通 AttributeId；耐久条由 `CohesionBreakRisk` 派生。 |
| Buff 来源 | Buff v2：`GameplayBuffDefinitionRegistry`、`GameplayBuffRequestBuffer`、projection systems | 是，作为来源和表现 | Buff 不直接改 HP，也不直接写护盾三势；组合根通过 bridge 创建、刷新或移除护盾。 |
| Runtime 状态 | `IRuntimeHashContributor`、Runtime SaveState、diagnostics snapshot | 是 | 当前实现记录三势、状态段、破盾状态和 collider 绑定。 |
| Unity 表现 | Unity presenter / VFX / UI Toolkit | 只作为 view | Unity 层只显示护盾、破盾和压力条，不驱动权威命中。 |

## 3. 权威状态模型

护盾权威状态属于 Combat runtime。Gameplay、Buff、UI、VFX 和 Runtime AI Planner 只能通过 bridge、snapshot 或事件读取它，不直接修改内部字段。

当前实现使用以下命名作为公共设计约束：

- `CombatShieldPotencyTriplet`：三势值，字段为 `Motion`、`Cohesion`、`Scatter`。
- `CombatShieldPressureBand`：`Stable = 0`、`Pressed = 1`、`Cracked = 2`、`Critical = 3`、`Broken = 4`，数值顺序与 Gameplay `PressureBand` 保持一致，但类型独立。
- `CombatShieldPotencyComponent`：单个护盾的权威状态，至少包含 shield entity、owner entity、body/collider id、当前三势、基础三势、当前凝聚限、恢复参数、最后受击帧、当前状态段和 broken 标记。
- `CombatShieldImpactRequest`：一次投射物、近战或其他 Combat 命中对护盾施加的 transient 输入。

核心派生值：

```text
ShieldPressure = max(0, Motion + Scatter - Cohesion)
CohesionBreakRisk = CurrentCohesionLimit <= 0 ? 1 : ShieldPressure / CurrentCohesionLimit
```

语义：

- `Motion` 表示冲击、震荡、位移趋势和结构失稳输入。
- `Cohesion` 表示护盾凝聚、稳定和结构保持能力。它不是一条可扣空的耐久值。
- `Scatter` 表示离散、裂解、扩散和能量破碎趋势。
- `CurrentCohesionLimit` 是当前凝聚限，可由护盾基础配置、技能来源、动作支撑或状态加成推导，但不来自普通角色 AttributeId。
- UI 可把 `CohesionBreakRisk` 显示为护盾耐久条，但文档、调试和实现中必须称为破盾风险或压力比例。

状态段阈值默认采用韧性系统口径：

| 状态段 | 默认条件 |
| --- | --- |
| `Stable` | `CohesionBreakRisk < 0.35` |
| `Pressed` | `0.35 <= CohesionBreakRisk < 0.65` |
| `Cracked` | `0.65 <= CohesionBreakRisk < 0.85` |
| `Critical` | `0.85 <= CohesionBreakRisk < 1.00` |
| `Broken` | `CohesionBreakRisk >= 1.00` |

向下恢复使用韧性系统风格滞回，避免临界值附近反复闪烁：`Pressed` 退出线为 `0.30`，`Cracked` 退出线为 `0.60`，`Critical` 退出线为 `0.80`，`Broken` 语义退出线保留为 `0.95` 但 broken 状态不会自然恢复。未破盾时，最后一次增加压力经过 `RecoveryDelayFrames` 后，默认自然恢复只降低 `Motion` 和 `Scatter`，直到压力回落；提高 `Cohesion` 必须来自明确的修复、凝聚或技能效果。进入 `Broken` 后不自然恢复，必须由技能、Buff 来源或显式重建请求创建新护盾。

## 4. 命中与遮挡流程

### 4.1 护盾体积更新

护盾由 owner 当前 Combat pose 推导中心、朝向和局部偏移，在每个 Combat fixed step 同步到 `CombatPhysicsWorld`。推荐 v1 形态是随身前方 Capsule：

- 胶囊长轴沿拥有者右向或垂直方向，由具体角色体型配置决定。
- 胶囊中心位于拥有者前方局部偏移处。
- 胶囊半径和高度来自护盾配置，不从 Unity Transform 反推。
- 护盾 collider 所在 Combat layer 只进入 projectile / weapon trace 查询 mask，不进入角色运动 obstacle mask，因此不阻挡移动。

当护盾进入 `Broken`：

- `CombatPhysicsWorld.RemoveCollider(bodyId, colliderId)` 移除遮挡体。
- 发送 `CombatShieldBreakEvent`。
- 保留 broken 状态进入 hash/save/debug，直到明确重建或移除护盾实体。

### 4.2 投射物

投射物 sweep 查询包含护盾 layer。若最近命中为未破盾护盾：

1. 由 projectile impact context 和 `DamageProfileResolveResult` 生成 `CombatShieldImpactRequest`。
2. 护盾系统应用三势增量，更新 `ShieldPressure`、`CohesionBreakRisk` 和状态段。
3. 发送 `CombatShieldBlockEvent`；如果状态段首次进入 `Broken`，同时发送 `CombatShieldBreakEvent`。
4. 该投射物按自身 impact policy 停止、销毁或播放命中表现；本次命中不再继续命中拥有者。

若护盾在查询前已经 broken，则 collider 不存在，投射物按原有 projectile / hit chain 继续解析。

### 4.3 近战 Trace

近战 Trace 需要在普通目标 `HitResolveSystem` 前执行 shield occlusion filter：

1. `CombatWeaponTraceEvaluator` 仍通过 `CombatPhysicsWorld.QueryCapsule` 生成查询结果。
2. 调用方按 Combat query 的稳定排序读取候选，找出同一 trace 上距离攻击源最近的未破盾护盾。
3. 若找到护盾，则将护盾后的 owner 或受保护目标从本次普通目标候选中裁剪，并提交一条 `CombatShieldImpactRequest`。
4. 破盾由本次护盾冲击触发时，本次攻击仍被护盾消费；下一次 Trace 或下一段有效 hit 才能穿过。
5. 没有护盾命中时，候选继续进入现有 `HitResolveSystem`。

这样避免通用 `HitResolveSystem` 同时解析“护盾”和“护盾后目标”，也避免把护盾做成普通 blocking status 后仍让 Trace 穿透后排。

### 4.4 当前不足：连续 WeaponTrace 边界和表现一致性

当前 `CombatShieldOcclusionFilter` 只覆盖同一批候选中的距离裁剪，尚未表达完整的 projectile / WeaponTrace resolver 接线、跨帧阻挡状态和表现联动。后续实现不能只用“攻击者或攻击源位于护盾哪一侧”决定是否阻挡；挥刀、长枪横扫、鞭子等武器判定的权威依据必须是 projectile sweep 或 weapon trace sub-sweep 的命中先后顺序。

后续接线必须满足：

- 对每个 projectile sweep 或 weapon trace sub-sweep，按该 sweep 的稳定 hit time / distance 判断；只有未破护盾命中早于受保护目标命中时，才裁剪目标并提交 `CombatShieldImpactRequest`。
- 武器判定从护盾外侧扫入内侧，且路径先碰护盾再碰 owner 时，本段攻击应被护盾截获，即使武器后续帧已经位于护盾内侧。
- 敌人已经位于护盾和 owner 之间，且本段 sweep 没有从外侧穿过护盾时，才允许绕过护盾直接命中 owner。
- 护盾由本段命中触发破盾时，本段攻击仍被护盾消费；下一段有效 hit、下一次 trace segment 或下一次攻击才允许穿过。

连续 WeaponTrace 还需要显式的 shield block latch。也就是说，同一攻击段第一次被护盾截获后，后续帧不能因为武器判定体已经在护盾内侧就继续伤害 owner。推荐 latch key 至少包含 attacker entity、action instance id、trace id、protected owner entity、shield entity，以及 hit segment / window id。该 latch 应在当前 trace segment 结束、攻击动作进入新的独立 hit segment、或攻击实例结束时释放。

表现层也存在当前不足：如果只裁剪伤害，可能出现刀刃视觉上穿过护盾和角色，但角色没有受击反馈的违和表现。后续应引入 `ShieldBlockedPresentation` 语义：护盾接触点播放 VFX / SFX，必要时触发轻微 hit-stop，抑制 owner 命中特效、血花、受击音效和普通 hit reaction；项目层可选择让攻击者进入 deflect / recovery，或提前结束本段 trace，以避免后续帧持续穿模。

## 5. 三势增量映射

护盾不读取独立 `ShieldDamage` 或 `ShieldDurability` 属性。每次命中都从既有 Combat 输入派生三势增量。

未来配置层提供“命中类型 / 伤害配置 -> 三势增量”的映射，最低字段语义如下：

| 字段 | 语义 |
| --- | --- |
| `stableId` | 映射规则稳定 id。 |
| `sourceKind` | 例如 `ProjectileImpact`、`WeaponTrace`、`AreaPulse`。 |
| `hitTag` | 可选命中特性标签，框架不定义具体元素体系。 |
| `baseDelta` | 直接三势增量：`Motion`、`Cohesion`、`Scatter`。 |
| `impactPowerScale` | 将 `ImpactPower` 映射到三势的比例。 |
| `damageScale` | 将 resolved HP damage 输入映射到三势的比例；可为零。 |
| `minDelta` / `maxDelta` | 单次增量夹取范围，保证确定性和平衡边界。 |

默认解释：

- 冲击、爆炸、重击倾向于增加 `Motion` 和 `Scatter`。
- 凝聚、修复、稳定护盾的效果倾向于增加 `Cohesion` 或降低 `Motion` / `Scatter`。
- HP 伤害和 `ImpactPower` 是输入，不是护盾压力的最终权威值。
- 框架只提供映射机制和确定性结算，不写入具体游戏元素相克规则。

## 6. 模块边界

### 6.1 Attributes

角色现有 Attack、Defense、HP、Posture 兼容字段和其他 AttributeId 不变。护盾三势、凝聚限、状态段和 broken 标记不是 `AttributeStore` 字段，不通过 modifier 直接改写。

如果项目未来需要装备或 Buff 影响护盾基础凝聚限，应通过护盾配置、创建请求或显式 shield modifier bridge 表达，而不是在角色普通属性表中新增 `ShieldDurability`。

### 6.2 Gameplay posture and guard pressure

护盾借鉴 `GameplayScatterPressureComponent/System` 的语义：

- 累积压力，而不是扣空韧性 HP。
- 当前凝聚限可以被支撑或状态改变。
- 状态段用于 UI、Debug 和 Runtime AI Planner facts。
- 破盾事件由系统统一发布。

但护盾不能直接复用 `GameplayScatterPressureComponent`，因为护盾是 Combat 空间对象，需要持有 collider 绑定、shield entity、owner entity 和三势状态。Gameplay 可以通过 bridge 镜像只读状态，不能成为护盾物理遮挡权威。

### 6.3 Buff v2

Buff v2 只负责来源、持续时间、层数、显示和属性/状态投影。护盾相关 Buff 的推荐职责是：

- 在组合根或 bridge 中创建、刷新或移除护盾来源。
- 提供表现元数据，例如图标、HUD 名称和剩余时间。
- 可通过显式 bridge 影响护盾配置或重建护盾。

Buff tick 不得直接扣 HP、不得直接写 `CombatShieldPotencyComponent`，也不得绕过 shield system 发布破盾事件。

### 6.4 Damage and armor

`CombatDamageApplicationSystem` 继续处理普通 HP、姿态压力、护甲完整度和 telemetry。护盾挡住的命中默认不进入拥有者 HP damage path，也不触发针对拥有者的 on-hit Buff。

`GameplayArmorIntegrityComponent` 仍表示护甲吸收层。只有当未来需求变成“命中角色后再按比例吸收 HP 伤害”时才考虑它；物理防护盾 v1 不使用它表达遮挡。

### 6.5 Hash, SaveState, diagnostics

当前实现把以下字段纳入 Runtime hash、SaveState 和 diagnostics snapshot：

- shield entity / owner entity / body id / collider id。
- 当前三势、基础三势、当前凝聚限、恢复参数、最后受击帧。
- 当前 `ShieldPressure`、`CohesionBreakRisk`、`CombatShieldPressureBand` 和 `IsBroken`。
- 当前帧 `CombatShieldBlockEvent`、`CombatShieldPotencyChangedEvent` 和 `CombatShieldBreakEvent`，包含 source kind、trace id、输入三势增量和应用后三势。

Diagnostics 应输出结构化行或 snapshot，不从 Debug log 反推数值。

## 7. 当前公共接口命名

本节固定当前实现命名。所有类型位于 `MxFramework.Combat.Shields`，属于 noEngine Combat assembly。

| 名称 | 职责 |
| --- | --- |
| `CombatShieldPotencyTriplet` | 三势值对象，字段为 `Motion`、`Cohesion`、`Scatter`。 |
| `CombatShieldPotencyComponent` | 护盾权威运行时状态。 |
| `CombatShieldPressureBand` | Combat 独立压力段枚举，数值顺序对齐 Gameplay `PressureBand`。 |
| `CombatShieldImpactRequest` | 一次命中对护盾施加的 transient 输入。 |
| `CombatShieldImpactMapping` | 将 source kind、hit tag、damage profile 结果和 impact power 映射为三势增量。 |
| `CombatShieldRuntime` / `CombatShieldRuntimeModule` | 消费 impact request、恢复压力、更新状态段、发布事件，并接入 `RuntimeHost`。 |
| `CombatShieldOcclusionFilter` | 在 projectile / weapon trace 普通 hit resolve 前裁剪被护盾遮挡的候选。 |
| `CombatShieldBlockEvent` | 护盾成功挡住一次命中的 typed event。 |
| `CombatShieldPotencyChangedEvent` | 三势、压力或状态段变化事件。 |
| `CombatShieldBreakEvent` | 首次进入 `Broken` 时发布的破盾事件。 |
| `CombatShieldRuntimeHashContributor` | 将护盾权威状态写入 Runtime hash。 |
| `CombatShieldRuntimeSaveStateProvider` | 生成护盾 SaveState。 |

分配约定：

- 命中、投射物、Trace、遮挡过滤和护盾冲击处理是高频路径，公共 API 必须按 `NoAlloc after initialization` 设计。
- 高频路径使用 caller-owned `List<T>` / buffer，不使用 LINQ、反射查找、字符串拼接、异常流程控制或每帧 Unity 对象查找。
- 低频配置导入、调试快照构建和编辑器工具可按 `LowFreqAlloc` 或 `AllocByDesign` 标注。

## 8. 测试与验收场景

当前实现覆盖以下 noEngine runtime focused 测试：

- 命中未破护盾会更新三势、状态段和 `CohesionBreakRisk`；达到凝聚限时移除 collider 并发出 `CombatShieldBreakEvent`。
- 延迟恢复只降低 `Motion` / `Scatter` 派生压力；broken 状态不会自然恢复。
- 近战 Trace 候选经过 `CombatShieldOcclusionFilter` 后，同一次 Trace 不命中护盾后的 owner。
- 破盾后的下一次 Trace 不再被该护盾遮挡，可重新命中 owner。
- Projectile 查询结果可以用 `TryFindNearestShieldHit` 找出最近未破护盾，供 projectile resolver 在普通 hit resolve 前消费。
- SaveState roundtrip 后，三势、状态段、broken 标记和 collider 绑定一致。
- Runtime hash 在相同输入下稳定；三势或破盾状态变化会改变 hash。

项目层把 `CombatShieldOcclusionFilter` 接进具体 projectile / weapon trace 组合根后，还需要补充端到端验收：投射物停止或进入 impact policy、拥有者不受 HP 伤害、破盾命中本身不穿透、以及 `DamageProfileResolveResult` 到三势增量映射配置。

后续接入 resolver、跨帧 latch 和表现联动时，还必须补充以下验收：

- 敌人位于护盾和 owner 之间，且本段攻击没有先穿过护盾时，内侧攻击不被护盾阻挡。
- 武器从护盾外侧扫入内侧，且 sub-sweep 先碰护盾再碰 owner 时，本段攻击被护盾阻挡。
- 同一 trace segment 第一帧被护盾截获后，后续帧不能因为武器判定体已经位于护盾内侧而继续伤害 owner。
- 护盾挡住命中后只播放 shield block 表现，不播放 owner hit 表现。

## 9. 当前交付边界

本阶段交付 noEngine Combat runtime v1 和 CharacterTest sample skill/VFX：

- 新增护盾三势状态、冲击请求、事件、运行时、遮挡过滤、Runtime hash 和 SaveState。
- 新增 focused EditMode 测试覆盖破盾、恢复、遮挡、broken 后下一次 trace、nearest projectile shield hit 和 SaveState/hash roundtrip。
- 新增 CharacterTest `Physical Shield` 技能配置，走现有 Ability -> CharacterAction -> Combat request -> VFX presentation 链路。
- 新增 Unity VFX prefab `Assets/VFX/MxFramework/Samples/PhysicalShield/Prefabs/PhysicalShield.prefab`，由 `MxFramework/CharacterTest/VFX/Rebuild/Physical Shield Prefab` 编辑器菜单生成并绑定到 `CharacterTestSceneProfile`。
- 不新增普通角色 `AttributeId`，也不把护盾并入 `GameplayScatterPressureComponent` 或 `GameplayArmorIntegrityComponent`。
- CharacterTest 示例层当前创建随身前方 shield collider 和三势权威状态；投射物 / WeaponTrace 的通用端到端遮挡消费仍由项目层在命中解析前调用 `CombatShieldOcclusionFilter` 和 `CombatShieldRuntime` 完成。
- projectile / WeaponTrace 的完整 resolver 接线、连续 trace segment 的 shield block latch，以及 `ShieldBlockedPresentation` 表现联动仍是后续工作。
