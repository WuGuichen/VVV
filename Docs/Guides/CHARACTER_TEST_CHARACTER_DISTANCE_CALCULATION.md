# CharacterTest 角色距离计算方案

> Status: Design

> 状态：CD-M0～CD-M5 当前实现整理；历史提案仅作演进背景
>
> 范围：CharacterTest 中的角色目标距离、最近目标选择、AI 攻击范围，以及与 Hurtbox、视线、导航和投射物瞄准的衔接
>
> 主要代码：`CharacterTestPerceptionFrameCache`

## 1. 结论

CharacterTest 当前明确保留两种正式距离：

- `target.distance`：自身 Completed Frame 姿态胶囊到已锁定 `LogicalHurtboxId` 表面的三维净空，按 Fix64 求值并向上量化到毫米；
- `target.rootDistance`：两个角色 Transform 原点之间的 XZ 平面距离，即 CD-M0～CD-M4 原 `target.distance` 语义。

下式现在描述 `target.rootDistance`：

```text
dxMm = target.XMillimeters - source.XMillimeters
dzMm = target.ZMillimeters - source.ZMillimeters

dx = dxMm / 1000
dz = dzMm / 1000

sqrDistance = dx * dx + dz * dz
distance = sqrt(sqrDistance)
```

`target.rootDistance`：

- 以角色运行时 Transform 为数据源，先取毫米整数坐标，再派生米制结果。
- 只计算 XZ，忽略 Y 轴高度差。
- 表示“两个角色在地面上相距多远”，不是碰撞体表面之间的净空距离。
- 不扣除角色半径、攻击者半径或目标半径。
- 是当前帧可重算的派生数据，不是独立的权威状态。

它主要服务于 Actor 候选排序/软粗筛、Vision/LOS/threat、目标锁定最大距离和相机。正式接敌、保持、后撤、行为卡距离 Predicate 与 PrimaryAttack Opportunity 使用锁定 Hurtbox 距离。

`RootPlanarDistance` 仍然有价值，不应删除，也不应静默改成“最近 Hurtbox 距离”。但它无法独立回答 AI 真正关心的两个问题：

> 从当前位置到一个可执行攻击的合法姿态还有多大代价；当前姿态下，该技能是否至少能命中一个合法 Hurtbox。

当前 `target.inAttackRange` 只在正式 PrimaryAttack 对锁定 Hurtbox 得到 `CurrentPose + CanHitFromCurrentPose` 时成立；Skill/Ability 继续使用各自动作目标合同。

## 2. 设计理念

### 2.1 用平面距离表达通用地面空间关系

CharacterTest 的主动移动、普通接近、后撤、保持距离和最近目标粗筛都是地面导向的行为。默认忽略 Y 轴可以避免台阶、坡度或角色身高差无意中改变“谁的根位置更近”。具体技能是否能命中必须由后续的攻击几何判定，不能从忽略 Y 轴的标量直接推导。

因此，当配置中写“1.5 m 停止距离”或“2.5–4 m 理想距离带”时，含义是两个角色根位置的平面间距，不是模型、Hurtbox 或胶囊表面的间距。

### 2.2 根距离使用 Completed Frame 几何，阶段查询另行缓存

`CharacterTestCompletedFrameGeometryCache` 冻结每个已完成固定 Tick 的角色根位置，并以 Building / Committed 双缓冲维护帧身份：

- 角色 Motion、接触解算和 AuthorityPose 同步后建立 `Building[N]`，当前 Combat Trace、投射物瞄准和 Chain Lightning 可以读取；
- Tick 的 BattleState、Destruction 和最终同步完成后，`Building[N]` 原子提交为 `Committed[N]`；
- Runtime AI Planner 与 Runtime Commands 在 `N+1` 读取 `Committed[N]`，不会重新从 live Transform 构建相同根距离；
- rollback、network restore、Session rebuild 或 ReleaseAll 会推进 generation 并使旧时间线缓存整体失效。

实体位置每 Tick 冻结一次；无方向角色 Pair 的平方距离和真实距离在该 geometry generation 内按需最多构建一次。A→B 与 B→A 共享距离标量，方向、bearing 和 facing projection 各自缓存。`EvaluationFrame` 表示消费者执行帧，`GeometryFrame` / `GeometryGeneration` 表示数据来源，避免把 Planner `N+1` 对 `N` 的读取误写成 live `N+1` 几何。

`CharacterTestPerceptionFrameCache` 继续拥有 Visibility、Navigation、Environment 和 Combat query scratch 等阶段缓存；阶段切换只清这些参数化查询，不再清 Completed Pair Geometry。Attack Opportunity 仍必须额外冻结 source pose、target Hurtbox、resolved action 和障碍世界 revision，不能从根距离缓存推导命中结论。

### 2.3 几何与玩法规则分离

缓存只回答“两个实体的几何关系是什么”，不把以下规则暗中并入距离计算：

- 是否敌对、存活或可伤害；
- 是否在某个技能的射程内；
- 是否被墙体遮挡；
- 投射物是直线、弹道还是追踪运动；
- 候选人是否满足某个具体玩法的额外约束。

这些由目标选择、导航、目标锁定或技能系统自己组合。这样既可以共享几何结果，又不会让一个技能的规则污染所有消费者。

### 2.4 派生数据不升格为持久化权威

帧缓存只保存当前帧可从角色状态重算的结果。它本身不进入 SaveState、network snapshot、replay/result hash 或 Runtime AI Planner JSON schema。消费者可以把距离投影到当帧决策、调试 snapshot 或 UI 中，但真源仍是角色运行时位置。

### 2.5 平方值与真实值由 Pair memo 统一生成

最近目标选择和最大距离过滤继续使用 `SqrDistanceMeters` 做比较；行为条件、移动和显示使用同一 Pair memo 的 `DistanceMeters`。第一次请求 Pair 时仍会同时生成平方值、开方值及当前方向投影，因此本次变更消除的是跨系统/跨阶段重复计算，不宣称已经实现“所有候选人只做平方粗筛、胜者才开方”。

内部同时保留毫米平方值，CD-M4 的 XYZ 软排序从同一冻结位置派生 `SqrSpatialDistanceMillimeters`；现有 Planner 阈值仍使用原有 float 结果，避免行为迁移。

列表、query buffer 和样本 struct 都重复使用，目标是 warmed 之后的 Tick 路径不产生 GC allocation。

### 2.6 确定性权威与 float 投影分层

角色权威 Transform 使用毫米整数；动画 Hurtbox 位姿会量化并投影为 `Fix64` 的 `CombatPhysicsWorld` compound shape；实际命中由确定性 CombatPhysics query/trace 解析。这条链路才是“是否真实命中”的权威。

`CharacterTestPerceptionFrameCache` 的 `DistanceMeters`、`SqrDistanceMeters` 和 float direction 是从毫米位置派生的现有 Planner/展示投影。这不能简化为“float 只用于 UI”：Runtime AI Planner 的输出会提交 gameplay command，其连续状态也会进入 rollback snapshot 和确定性 state hash，所以 Planner 是 authority-driving 路径的一部分。

当前更核心的设计风险仍是**用根节点平面距离代替技能命中可能性**；但新 Attack Opportunity / Zone / Approach 合同不应继续扩大这份 float 兼容债务。所有影响攻击触发、候选归约、Zone 切换、迟滞和 path-cost 比较的新数值应使用毫米整数、定点或显式量化值；float 只作为它们的展示投影。

## 3. 关键实现

### 3.1 实体样本

`CharacterTestPerceptionEntitySample` 保存：

- `Position`：角色 Transform 位置；
- `Center`：优先取 `CombatPhysicsWorld` 中对应 body 的中心，无 body 时使用 Transform 加角色半高回退；
- 实体、Combat entity、body、队伍、存活状态、朝向和地表材质摘要。

`Position` 是角色平面距离的语义基准；`Center` 是视线射线起点、导航探测起点、投射物瞄准点和其它空间查询的几何基准。两者不应混用。

### 3.2 目标几何样本

`CharacterTestTargetGeometrySample` 一次计算并缓存以下数据：

| 字段 | 含义 |
| --- | --- |
| `DeltaXMillimeters` / `DeltaZMillimeters` | 基于权威 Transform 毫米坐标的平面差值 |
| `GeometryFrame` / `GeometryGeneration` | 冻结位置来源帧及 rollback-safe generation；与消费者 `EvaluationFrame` 分离 |
| `SqrPlanarDistanceMillimeters` | XZ 根距离的内部毫米平方值；不改变现有 float Planner 比较 |
| `SqrSpatialDistanceMillimeters` | XYZ 根位置内部平方值，仅供明确的三维软排序等独立语义 |
| `DeltaXMeters` / `DeltaZMeters` | 供 Unity/runtime 消费的米制派生值 |
| `SqrDistanceMeters` | 最近目标和射程粗过滤用的平方距离 |
| `DistanceMeters` | XZ 平面实际距离 |
| `DirectionX` / `DirectionZ` | float 归一化平面方向 |
| `DirectionXScaled` / `DirectionZScaled` | 通过确定性整数数学得到的缩放方向 |
| `TargetBearingMilliDegrees` | 目标的平面方位角 |
| `FacingDeltaMilliDegrees` / `FacingDot` | source 当前朝向与目标方向的差值 |
| `SourceCenter` / `TargetCenter` | 视线起点、瞄准点和物理查询使用的空间中心 |

距离为 0 或小于方向 epsilon 时，归一化方向返回 0，避免除零或不稳定方向。

### 3.3 最近目标选择

`TryFindNearestTarget(...)` 按实体稳定列表顺序扫描，每个候选人复用目标几何样本，然后分层应用：

1. 关系过滤：`AnyOther` 或 `OpposingTeam`；
2. 状态过滤：`Any` 或 `Alive`；
3. 最大 XZ 平面距离；
4. 平方距离最小；
5. 同距离策略。

同距离策略是显式参数：

- `First`：只在严格更近时替换，保留列表中先遇到的目标；
- `Last`：小于或等于当前最佳距离时替换，保留后遇到的目标。

这不是细节差异，而是为了保持历史玩法语义：

- Runtime AI Planner 当前使用 `OpposingTeam + Any + First + 无限最大距离`；死亡目标是否导致后续行为由 target-state facts 再表达；
- 目标锁定的缓存快速路径使用 `OpposingTeam + Alive + Last + targetLockMaxDistance`，并对胜者追加 `CanReceiveHpDamage` 校验。如果该胜者不可伤害，当前代码会进入完整回退扫描，在距离归约前用 `IsTargetLockCandidate` 过滤，因此不会因最近的不可伤害目标而直接遗漏次近合法目标。

### 3.4 AI 中的传递链路

```text
Character Transform（毫米）
    -> CharacterTestPerceptionFrameCache
    -> CharacterTestTargetGeometrySample.DistanceMeters
    -> Runtime AI Planner target.distance fact
    -> 行为条件 / 评分 / 距离带
    -> MoveToTarget / Retreat / Hold / Attack 等决策
```

`target.inAttackRange` 不是单独再扫描目标。导航服务复用同一几何样本：直通 CombatPhysics capsule probe 清空时，使用 `DistanceMeters <= attackRange` 得出射程结果；直通路径受阻时，即使根距离较近也返回 false。这里的 `attackRange` 来自 Runtime AI Planner profile，不是当前待释放 Ability/CharacterAction 的实际攻击包络。如果 CombatPhysics 导航服务或共享缓存不可用，直线兼容路径会直接从 Transform X/Z 重算平面距离，并把路径视为清空。

### 3.5 已有的命中几何基础

“Planner 还没有技能相关的攻击机会查询”不等于“仓库还没有 Hurtbox 或真实攻击形状”。当前已有基础是：

- `CharacterTestCharacterHurtboxSystem` 把角色包的动画烘焙 Hurtbox 位姿同步为 `CombatPhysicsWorld` 中的 Sphere / Capsule / OBB compound leaves；
- Hurtbox 位置、旋转和插值使用量化数据与 `Fix64`，不从 Unity Collider 或实时 Animator Transform 建立第二套权威；
- 每个 `CharacterBodyColliderRuntimeBinding` 已有稳定 collider id、part id、hit zone id、shape、priority 和 weak-point/伤害修正信息；
- `CharacterTestCombatDamageResolver` 已能从实际命中的 `CombatColliderId` 解析 HitZone / BodyPart 及对应的伤害、冲击修正；
- CharacterAction 已支持 WeaponTrace、`ManualCapsule`、`CasterBody` 和 `BakedSocketCapsule` 等实际 HitTrace/HitVolume 权威数据。

当前缺失的是一层**对现有权威几何的只读预判**：在 AI 真正提交攻击前，以同一模拟快照为起点，按 resolved action 的实际 Active Trace 序列预演目标 Hurtbox 是否会被命中解析接受。第一版的目标运动假设、source 动作轨迹和姿态模式在第 7.4 节定义；不应通过 Unity 物体或另一份手写半径重建命中模型。

## 4. 与其它“距离”的边界

| 场景 | 距离基准 | 是否复用角色 XZ 距离 | 说明 |
| --- | --- | --- | --- |
| AI `target.distance` | source 姿态胶囊到锁定 Hurtbox 表面的三维净空 | 否 | 正式接敌、保持/后撤、行为条件 |
| AI `target.rootDistance` | Transform 原点的 XZ 平面距离 | 是 | Actor 粗筛、感知、相机、旧卡迁移 |
| 最近目标 / 目标锁定 | Transform 原点的 XZ 平方距离 | 是 | 再组合队伍、存活、可伤害和最大距离规则 |
| 导航攻击范围 | Transform 原点的 XZ 平面距离 | 是 | 除距离阈值外，CombatPhysics 路径还要求直通 capsule probe 清空；仍不检查技能包络与 Hurtbox |
| 视线 | 从 `SourceCenter` 出发、长度等于 XZ 距离的水平射线 | 是 | 当前实现的射线 Y 分量为 0，遮挡权威来自 `CombatPhysicsWorld` |
| 投射物瞄准 | 已锁定 Hurtbox AimPoint | 否 | 发射后 Homing 仍保持 Actor tracking |
| 连锁闪电跳跃 | 首段使用锁定 Hurtbox AimPoint，后续使用链式规则 | 否 | 最终跳跃距离不是角色根距离 |
| 碰撞、Hurtbox、范围技能 | `CombatPhysicsWorld` 中的 AABB / Sphere / Capsule / Ray / Sweep | 否 | 以形状和物理查询为权威，可表达表面接触与立体高度 |
| 击退预估距离 | 冲击、质量、减速度和地表摩擦的派生结果 | 否 | 是运动效果预估，不是角色之间的几何距离 |

### 4.1 不应再压缩成一个“万能角色距离”

面向普通人形目标时，根节点距离与“还要接近多少才能攻击”通常近似。面向大型、动画化、多 Hurtbox 目标时，至少需要区分以下概念：

| 语义 | 含义 | 状态 | 主要用途 |
| --- | --- | --- | --- |
| `RootPlanarDistance` | 两个角色根节点的 XZ 距离 | 已实现为 `target.rootDistance` / `DistanceMeters` | 远距离粗筛、感知、相机、兼容 |
| `LockedHurtboxSurfaceGap` | source 姿态胶囊与持久锁定 Hurtbox 表面的三维净空 | CD-M5 已实现为 `target.distance` | 接敌、距离 band、正式行为条件 |
| `AttackEnvelopeSeparation` | 技能攻击包络与目标合法 Hurtbox 之间的形状间隔 | 未实现，第一版可选 | 几何诊断、迟滞和粗粒度评分；不表示真实移动距离 |
| `EnvelopeIntersectsTarget` | 实际 Active Trace 包络是否与任一兼容 Hurtbox 相交 | 未实现 | 原始形状相交结论 |
| `TraceCanResolveTargetHit` | 按实际 collision/damage filter、blocker order 和 penetration/multi-hit policy 预演后，目标是否会被接受 | 未实现 | 比泛化 LOS 更接近实际命中权威 |
| `CanHitFromCurrentPose` / `CanHitAfterTurnInPlace` / `RequiresTranslation` | 当前姿态、原地允许转向、以及必须位移三种结论 | 未实现 | 区分立即攻击、原地转向和接近移动 |
| `ApproachPose` | 可站立、可导航、满足攻击方向/高度/视线的候选姿态 | 未实现 | 引导移动到真正可攻击的位置 |
| `AttackPosePathCost` | 当前姿态到最优合法 `ApproachPose` 的路径代价 | 未实现 | 目标选择和接敌规划 |
| `SelectedDamageZone` / `TargetPoint` | 当前稳定攻击区域与具体目标点 | 只有部分技能的 target point；无通用 AI 区域选择 | 瞄准、朝向、稳定接近和调试 |

前一版使用的 `GeometryInReach` 只适合作为讨论时的雨伞词，不应直接成为含义过宽的最终 API。最终合同应区分原始形状相交、实际 trace 预演是否能接受目标，以及当前姿态/原地转向/必须位移三种结论。冷却、资源、硬直、技能禁用和行为切换规则仍属于技能/Planner 规则层。

## 5. 回退和兼容策略

当共享帧缓存或 CombatPhysics 未就绪时，部分系统保留了兼容路径：

- Runtime AI Planner 可直接扫描 `entities`，用 Transform X/Z 平方距离选择最近敌人；
- 目标锁定可直接重算 Transform X/Z 平方距离；
- 直线 AI 导航可通过 `CharacterTestDirectAiNavigationService.ToTarget(...)` 重算 XZ 向量；
- 投射物瞄准可直接从目标物理 body 或 Transform + half extents 解析目标中心。

这些回退路径的目标是保留 standalone/兼容构造语义，不是推荐正式组合继续复制距离计算。正式 GameSlice 的目标选择、AI 感知、目标锁定、角色 Entity 移动和 Debug/HUD 使用 Completed Frame Geometry；直接读取 live Transform 的路径必须显式标记为兼容或其它距离语义。

新 `AttackOpportunityService` 不继承“CombatPhysics 不可用时退化为根距离射程”的兼容策略。快照缺失、evaluator 不支持或 revision 不一致必须分别返回 `Unavailable`、`Unsupported` 或 `StaleSnapshot`，由 Planner 显式决定等待、换动作、走旧兼容行为或报错。

## 6. 当前限制与注意事项

1. **根节点进入范围不等于技能可命中**
   当前 `target.inAttackRange` 以 `RootPlanarDistance <= attackRange` 为核心，CombatPhysics 导航路径再叠加直通清空条件。它没有检查当前技能的实际 HitTrace/HitVolume、目标哪些 Hurtbox 合法、垂直覆盖或攻击朝向角。这是大型多 Hurtbox 目标下的核心限制。

2. **根节点距离不能单独作为大型目标的硬粗筛**
   巨龙根节点可以距 AI 很远，而尾部 Hurtbox 已在面前。如果在 damage aggregate bounds/Hurtbox 查询前用普通根距离阈值硬排除，新系统会重新引入原问题。`RootPlanarDistance` 可用于排序和软评分；硬排除必须由攻击 broadphase bounds 与目标 damage aggregate bounds 的保守不相交证明得出。

3. **根距离与净空必须分名**
   `target.rootDistance` 不补偿体型；`target.distance` 明确是锁定 Hurtbox 净空。CD-M5 通过 Behavior Contract 7、Planner state 14、Network 34 和 Replay 4 显式迁移，没有静默复用旧合同。

4. **根距离不包含高度差，Hurtbox 净空包含**
   `target.rootDistance` 继续忽略 Y；`target.distance` 使用三维形状。动作是否真正命中仍由 Attack Opportunity 与 Combat Trace 决定。

5. **根距离按 Completed Frame 一致，重查询仍按参数/revision 分阶段**
   Pair Geometry 在 `Building[N]` / `Committed[N]` 生命周期内一致并可跨 Combat N 与 Planner/Commands N+1 复用；Visibility、Navigation 和 Attack Opportunity 仍绑定各自的 physics、graph、action 与 Hurtbox witness。不得把根距离 generation 当成这些较重查询的完整 freshness 证明。

6. **现有 float 是 authority-driving Planner 的兼容债务**
   真实 Hurtbox/HitTrace 使用 `Fix64` CombatPhysics，但 `DistanceMeters`、`SqrDistanceMeters` 和 float direction 会影响 Planner 条件和输出。它们不是持久化坐标或实际命中证明；新 Opportunity/Zone/Approach 权威比较也不应继续使用未量化 float。

7. **同距离顺序是玩法合同的一部分**
   修改实体扫描顺序或 `First` / `Last` 策略可以改变等距离场景的选中结果，需要有针对性回归证据。

## 7. 演进方向与已实现边界

### 7.1 不变量与保守粗筛

后续演进应保持以下设计不变量：

- 根节点 XZ 语义已显式迁移为 `target.rootDistance`；`target.distance` 已通过版本化合同改为锁定 Hurtbox 表面净空；
- 目标、技能、区域的合法性过滤必须先于距离归约和评分；
- 预判与实际命中复用同一份 CharacterAction HitTrace/HitVolume、碰撞 mask 和 CombatPhysics Hurtbox 数据；
- 不在 `CharacterTestPerceptionFrameCache` 中预计算所有 `source × target × action × hurtbox × attackPhase` 组合；
- 根节点距离继续服务通用空间关系、排序和软评分，但不能单独承担攻击候选的硬排除。

最后一条对大型目标尤其重要：龙尾 Hurtbox 已进入攻击范围时，龙的根节点仍可能很远。一个不会漏掉真命中的角色级硬粗筛只能在下列条件成立时排除目标：

```text
SourceActionBroadphaseBounds 与 TargetDamageAggregateBounds 可证明不相交
```

其中 source bounds 应保守覆盖被评估姿态下完整 Active Trace 路径，target bounds 应保守覆盖目标当前全部合法伤害体。空间索引也应存储聚合 bounds 或“根节点 + 保守 extent”，而不是只索引根节点点位。`RootPlanarDistance` 可以决定先算谁，但不能成为唯一的 `continue` 条件。

### 7.2 统一时间语义与不可变快照

CharacterTest 当前的权威模拟序号是 `RuntimeFrame`。第一版合同应把它命名为 `SimulationTickId` 的具体来源，不再另造一个没有映射关系的 “Combat Tick”。如果以后跨到通用 Combat `CombatFrame`，必须显式记录二者映射。

一次 Attack Opportunity 查询只能读取同一个逻辑快照 `T`，至少绑定：

- `RuntimeFrame` / `SimulationTickId`；
- CombatPhysicsWorld、障碍物和 Hurtbox compound revision；
- source 的位置、朝向、姿态以及这些数据的 revision；
- target 的位置、姿态、DamageZone/Hurtbox aggregate revision；
- 已解析 CharacterAction、攻击配置、装备/profile revision。

这些值应来自同一个已完成的模拟状态，不能把 PreSimulation 的角色根位置与 post-motion 的 Hurtbox 拼在一起。缓存命中也必须核对 tick、revision、查询模式和锁定 Zone；任一版本不一致都返回 `StaleSnapshot`，不得悄悄退回根距离并给出“可攻击”。

由于 Planner 会输出权威命令，候选比较、阈值、角度、tick 和稳定决胜应使用整数、定点数或明确量化值。float 可以作为展示投影，但不能成为新合同中未定义量化规则的最终权威比较。

### 7.3 推荐分层与缓存粒度

```text
CharacterTestPerceptionFrameCache
    根位置、身体中心、方向、RootPlanarDistance
                ↓
CharacterDamageGeometrySnapshot（建议的只读视图）
    复用同一 SimulationSnapshot 的 CombatPhysics compound Hurtbox
    提供 actor/zone aggregate bounds 与稳定 collider/zone 元数据
                ↓
CharacterAttackOpportunityService（建议）
    对真实 Active Trace 做无副作用 PreviewTrace
    输出当前姿态、原地转向、必须位移及命中样本证据
                ↓
CharacterAiEngagementPlanner（建议）
    ApproachPose、路径成本、区域锁定、拥挤/预约和切换稳定性
```

`CharacterDamageGeometrySnapshot` 不是另一套物理世界，而是同一权威快照上的便宜只读视图。它应复用 compound leaves、`CombatColliderId`、`CharacterBodyColliderRuntimeBinding` 和 HitZone/BodyPart 映射。

缓存不应过早锁死成单一粒度。较重的 trace 生成可以按 `source + resolved action + source pose + tick/revision` 构建一次，再按 `source + target + resolved action + evaluation mode + tick/revision` 归约结果；若某类 evaluator 本身很便宜，也可以直接做小规模 pair query。最终粒度应由 profile 证据决定。

### 7.4 第一版 Attack Opportunity 合同

概念上的查询入口为：

```text
EvaluateAttackOpportunity(
    SimulationSnapshot T,
    SourceEntity,
    TargetEntity,
    ResolvedCharacterAction,
    EvaluationMode)
```

`EvaluationMode` 至少区分：

- `CurrentPose`：以 `T` 时刻的位置和朝向立即起手；
- `TurnInPlace`：位置不变，只允许先完成所需的原地转向；
- `CandidatePose`：由更高层 Engagement Planner 提供候选位置/朝向，第一版可以不支持。

第一版采用明确且可复现的 **static-target prediction**：假设角色在 `T` 时刻起手，source 按真实动作的 Active Trace 样本推进，包含 socket 轨迹和动作自带的 root motion；target 的 Hurtbox 冻结在 `T`，不预测目标未来移动。`CanHitFromCurrentPose` 的完整语义因此是 `CanHitIfStartNow_StaticTarget_FromCurrentPose`，它只证明该静态假设下的起手机会，不保证目标经过前摇后仍会被实际命中。若动作缺少生成这些样本所需的数据，应返回 `Unsupported`，不能用一个半径近似冒充同等结论。

建议的 `AttackOpportunityResult` 至少包含：

- `Status = Valid | Unavailable | Unsupported | StaleSnapshot`；其中 `Valid` 可以表达合法的否定结果；
- `PredictionAssumption = StaticTargetAtDecisionTick`；
- source、target、resolved action 和 snapshot 的稳定标识/revision；
- `HasCompatibleHurtbox`、`EnvelopeIntersectsTarget`、`TraceCanResolveTargetHit`；
- `CanHitFromCurrentPose`、`CanHitAfterTurnInPlace`、`RequiresTranslation`；
- 定点或量化的 `RequiredYawDelta`，以及技能确实支持俯仰瞄准时的 `RequiredPitchDelta`；
- 选中的 DamageZone/Hurtbox、source/target witness point 和 aim point；
- `EarliestCandidateHitTick` 与 `TraceSampleIndex`；前者定义为相对起手的 action tick offset，绝对时刻由 `T + offset` 得到；
- blocker、朝向、垂直范围、mask、伤害资格等失败原因 flags。

这里的 `RequiresTranslation` 只在查询 `Valid`、存在兼容 Hurtbox，且当前姿态和原地转向都因空间关系不能命中时成立；它表示需要上层寻找其它姿态，不等于已经证明存在可达攻击位，也不提供移动距离。数据不可用、技能不支持或目标没有合法 Hurtbox 时不能用它代替对应状态/失败原因。

`AttackEnvelopeSeparation` 可以作为以后按需增加的诊断值，但不是第一版必需项。若引入，必须同时规定它是 XZ 还是 3D、重叠时为零还是负穿透、精确值/下界/近似值、对应哪个 trace sample 和 witness pair，以及它是否忽略障碍；不得把它解释为导航移动距离。

### 7.5 PreviewTrace 复用实际命中语义

通用 `line-of-sight` 不能替代真实攻击路径。`PreviewTrace` / `DryRunTrace` 应复用实际执行侧的：

- Active tick 和 shape sample 顺序；
- WeaponTrace、capsule、box、projectile、beam 等具体形状；
- collision/damage mask、自身忽略、owner/队伍和伤害资格；
- blocker 的先后顺序、穿透规则、多 hit/重复 hit 规则；
- HitZone/BodyPart 与最终 damage receiver 解析。

预演只关闭伤害、事件和状态修改等副作用，不应另写一套“看起来差不多”的空间判定。合同还应明确区分：形状包络碰到目标、trace 顺序能走到该 collider、伤害解析最终接受该目标。这三者不能折叠成一个含混的 `InRange`。

### 7.6 DamageZone 与 Engagement Anchor 是正交属性

区域应分别表达 `Hittable`、`Aimable`、`Approachable`，三者不能互相推出。翼尖可以在掠过时 Hittable，却不适合长期 Aim 或 Approach。

长期 `EngagementAnchor` 一般应存于角色根节点局部空间，或 Boss 当前 phase 的稳定参考系中，不应直接跟随高频摆动的叶级 Hurtbox/骨骼。确需动态 anchor 时，必须定义有限速度/范围、滤波、稳定窗口和失效 revision。部位破坏、phase 切换或可伤害区域变化时，应推进 Zone/Anchor revision，让旧查询和旧预约失效。

为避免大型角色上频繁切换，还需要：

- Zone 锁定和显式切换收益阈值；
- 攻击范围的进入/退出迟滞；
- `LockedZone -> AuthoredPriority -> ZoneId -> HurtboxId -> AnchorId` 的稳定决胜链；
- 大型 Boss 的手工 Engagement Anchor，以及普通人形的可选自动环形候选点；
- 多 AI 容量/预约，防止所有单位拥挤到同一攻击点。

### 7.7 Planner 以有限联合候选为选择单位

“先选最近目标，再拿当前技能问能否打到”会把 target 与 action 的相互依赖藏起来。更稳妥的选择单位是有限的：

```text
(TargetEntity, ResolvedCharacterAction, DamageZone)
```

建议按以下顺序生成和归约：

1. 先应用 actor 合法性：非自身、实体有效、敌对关系、存活/可受伤、mask/tag 允许；
2. 解析当前可考虑的少量 action，并以它们的保守 broadphase 并集查询 actor damage aggregate bounds；
3. 将所有明确相交候选置于根距离软排序之前，再按稳定规则取得显式预算内的 Top-K；
4. 对每个 action 应用资源、冷却、状态和 action/zone 资格，再用该 action broadphase 对 target zone aggregate bounds 做保守排除；
5. 只对剩余少量 `(target, action, zone)` 执行叶级 `PreviewTrace`；
6. 依据量化评分和稳定 ID 决胜，再输出攻击、原地转向、寻找 ApproachPose 或换目标。

Top-K 是 Planner 的显式工作量预算，不是“预算外目标不可命中”的几何证明。发生截断时应暴露 `CandidateSetTruncated` 一类诊断，不能把有限候选内的 `Valid + no hit` 描述成全世界不存在攻击机会。

只有在当前不能攻击且决策确实需要接近时，才计算 Engagement Anchor、站立/占用验证和 `AttackPosePathCost`。这样既解决目标/技能选择的循环依赖，也避免预算全部排列组合。任何 broadphase 返回顺序都不能成为隐式决胜规则。

### 7.8 实施入口与里程碑

公共合同、唯一状态和子文档路由维护在 [CharacterTest 角色距离与 Attack Opportunity 实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)。具体任务和证据按里程碑拆分，本设计只保留阶段边界：

| 里程碑 | 阶段边界 |
| --- | --- |
| [CD-M0](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M0_FOUNDATION.md) | 建立不可变评估快照、伤害几何视图和实际执行/预演共用的 Trace Plan；不改变 Planner 行为。 |
| [CD-M1](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M1_ATTACK_OPPORTUNITY.md) | 完成静态目标 Attack Opportunity，并把 PrimaryAttack 的最终合法性切换到 action-level guard。 |
| [CD-M2](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) | 完成 DamageZone 作者化、Runtime 聚合、Zone lock 与稳定切换。 |
| [CD-M3](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md) | 完成 Engagement Anchor、CandidatePose、容量/预约和 Pathfinding Foundation 路径代价接入。 |
| [CD-M4](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md) | 完成有预算、可诊断、确定性的有限 `(target, action, zone)` 联合选择。 |
| [CD-M5](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M5_COMPOSITE_HURTBOX_TARGET.md) | 完成 Actor + LogicalHurtbox 复合锁、三维表面净空、玩家/AI/瞄准统一和版本迁移。 |

里程碑摘要不代表能力已经实现；状态以总台账为准，实际提交与验收依据以对应子文档为准。

## 8. 验证要点

现有集成契约测试已覆盖以下核心行为：

- `3-4-5` 平面三角形产出 `SqrDistanceMeters = 25` 和 `DistanceMeters = 5`；
- 同一缓存阶段内重复查询只构建一次几何样本；
- 同一缓存阶段内修改目标 Transform 后仍返回原样本，下一次 `BeginFrame(...)` 后刷新；
- Runtime AI Planner 的等距离最近敌人保留 `First` 语义；
- warmed 后反复执行几何、最近目标、环境和导航查询的测量路径为 0 B GC allocation。

新增或修改角色距离语义时，至少应验证：

- 纯 XZ、带 Y 高度差和重合位置三类几何场景；
- 最大距离边界上的等于关系；
- `First` / `Last` 等距离选择；
- 死亡、友方、不可伤害等状态过滤与几何计算的分层；
- 缓存阶段与兼容回退路径的结果一致性；
- 投射物、视线和范围命中仍使用各自正确的三维/CombatPhysics 语义。

引入 Attack Opportunity / Engagement 语义后，应新增独立契约测试：

- **保守粗筛**：目标根节点在粗筛半径外，但尾部 Hurtbox 已进入攻击包络，不得假阴性；
- **合法性优先**：距离最近候选不可伤害时，仍能稳定选择后续合法候选；
- **只需转向**：当前位置可在原地转向后命中，`CanHitAfterTurnInPlace=true` 且 `RequiresTranslation=false`；
- **静态目标假设**：高速移动目标仍按决策 tick 冻结，其结果和假设字段一致，不暗示已预测未来位置；
- **root motion**：动作自身位移使后续 Active sample 命中时，能返回正确最早 tick/sample；
- **三维包络**：目标 XZ 投影很近，但垂直高度超出动作包络；
- **blocker 顺序**：墙、可穿透物和目标的不同顺序与实际 trace 解析一致；
- **快照失效**：Hurtbox、source pose、action、phase 或障碍 revision 改变后旧缓存失效；跨 stage 数据或 snapshot id 不匹配返回 `StaleSnapshot`；
- **无序 broadphase**：随机化候选枚举顺序后，量化评分与稳定 ID 仍给出同一结果；
- **联合选择**：短剑、长枪、弓箭面对多个目标时，按 `(target, action, zone)` 而非单纯最近根节点得到正确选择；
- **Anchor 稳定性**：翼尖掠过可以即时命中，但不会成为长期追逐点；骨骼动画、部位破坏和 phase 切换能稳定保留或明确失效 anchor；
- **预演一致性**：同一 snapshot 下，AI 的 PreviewTrace 与实际 CombatPhysics Trace 对 collider、区域、blocker 和最早 sample 的解析一致；
- **规模边界**：多 AI/多 Hurtbox Boss 下，Top-K、actor/zone bounds 和分层缓存能限制叶级查询数量，并记录 profile 证据。

## 9. 代码导航

| 职责 | 位置 |
| --- | --- |
| 阶段内缓存、实体样本、目标几何、最近目标 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/CharacterTestPerceptionFrameCache.cs` |
| PreSimulation Planner 的缓存 epoch 与命令输出 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerCommandModule.cs` |
| Simulation RuntimeCommands 的缓存 epoch | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Commands/CharacterTestRuntimeCommandModule.cs` |
| post-motion/Hurtbox 同步后的缓存 epoch | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterRuntimeModule.cs` |
| Planner 权威状态快照与确定性 hash | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerStateSnapshot.cs` |
| AI 目标选择、距离 fact 和兼容回退 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerRuntime.cs` |
| 导航与全局 attack range | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/CharacterTestAiNavigationService.cs` |
| 目标锁定的过滤和等距离策略 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Commands/CharacterTestRuntimeCommandModule.TargetLock.cs` |
| 动画 Hurtbox 到确定性 compound shape 的同步 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterHurtboxSystem.cs` |
| CombatPhysics compound leaf 与更新 | `Packages/com.vincent.wgameframework/Combat/Physics/CombatPhysicsWorld.Compounds.cs` |
| Hurtbox 稳定 collider / HitZone / BodyPart 绑定 | `Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterRuntimeSpawnContracts.cs` |
| 实际命中 collider 到 HitZone/BodyPart 解析 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestCombatDamageResolver.cs` |
| CharacterAction HitTrace / HitVolume 权威合同 | `Docs/Interfaces/CharacterAction.md` |
| 投射物瞄准中心与三维射程 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.ProjectileAim.cs` |
| 连锁闪电的三维跳跃距离 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestCharacterCombatTraceSystem.ChainLightning.cs` |
| 集成契约测试 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestIntegrationContractTests.PerceptionFrameCache.cs` |
