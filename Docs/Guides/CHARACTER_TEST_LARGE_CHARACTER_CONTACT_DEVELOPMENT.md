# CharacterTest 大型角色接触、部位状态、权威姿态与动画附肢软接触实施总台账

> Status: Guide
>
> 路线代号：LCC（Large Character Contact）
>
> 计划基线：2026-08-24
>
> 里程碑状态唯一权威：本文第 6 节
>
> 单任务执行标准：[LCC 单任务执行文档模板](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_TASK_EXECUTION_TEMPLATE.md)
>
> 本文只定义已接受的实施路线、阶段边界和验收门禁。计划、候选类型名、测试清单或源码调查不证明实现存在；实际能力仍以源码、测试执行结果、Unity 运行证据和 `Docs/CAPABILITIES.md` 为准。

> 2026-08-25 `main` 集成说明：上游数值 `CharacterActionId` 已占用 snapshot schema 30，当前 LCC 版本顺延为 M2 Body Part schema 31、M3 Severed schema 32、M4 terrain authority pose schema 33。下方历史执行记录、旧测试名和 Evidence 文件中的 schema30/31/32 保留其当时原始证据标签，不再代表当前线协议号。

## 1. 目标与完成定义

本路线解决大型、长体型或多附肢角色仅依赖单个根部移动代理时出现的身体穿入、接触缺乏体积感、动画附肢可被完全无反馈穿过、部位状态无法持续、断肢后几何与动作未联动，以及表现姿态不能确定性适配地形的问题。

目标不是把全部动画 Hurtbox 变成移动硬阻挡，而是在现有权威链上增加五项互相分离的能力：

1. 少量、稳定、根空间的身体存在体积，用于连续软接触压力；
2. 独立于整角色 HP 的伤口与部位结构状态；
3. 部位切断后的 Hurtbox、DamageZone、Engagement Anchor、动作 Trace 和表现联动；
4. 由 `CombatPhysicsWorld` 地形事实驱动、同时投影到战斗几何与表现的确定性权威姿态。
5. 作者明确选择的动画 Hurtbox 附肢产生有界软压力与接触反馈，但不成为硬墙、导航障碍或普通接触伤害体。

路线完成时必须保持以下现有合同：

- 根节点 XZ 距离继续表达粗筛和兼容距离，不改写为 Hurtbox 或身体表面距离；即时命中继续由 Attack Opportunity / 实际 Trace 几何决定，长期接近继续由稳定 DamageZone / Engagement Anchor 决定。
- 角色移动硬阻挡、身体软接触、伤害接收和动作攻击保持不同职责，不建立第二套碰撞真源。
- Combat authority 不读取 Unity `Collider`、`Rigidbody`、Animator、骨骼 `Transform` 或 Unity Physics；这些对象只能用于表现、编辑或调试。
- `CharacterHurtbox` 不加入移动障碍 mask；翼膜、尾尖、头尖等瞬态动画几何不成为长期硬阻挡或寻路目标。M5 只允许作者选定叶子作为派生软压力来源，不能把“不硬阻挡”再次实现成“完全无接触反馈”。
- 所有会影响未来命令的状态必须进入正式 snapshot、full/owned/non-owned hash、rollback 和 replay；纯派生几何不得重复持久化。

## 2. 当前基线与缺口

下表是计划建立时的源码/当前文档调查基线，不代表本轮重新执行了 Unity 测试：

| 领域 | 当前基线 | 本路线结论 |
| --- | --- | --- |
| 移动阻挡 | `CharacterTestColliderPostureComponent` 提供半径、高度和中心 Y；CharacterTest 将其同步为单个 Dynamic Character AABB，并由 kinematic motor 处理地形和障碍 | 保留为唯一硬移动代理 |
| 动画受击 | `CharacterTestCharacterHurtboxSystem` 将离线 60 FPS 定点姿态投影为 sphere/capsule/OBB compound；Dragon14 当前有 18 个显式 Hurtbox | 继续只承担伤害接收和即时命中几何 |
| 附肢攻击 | Dragon14 咬击、扫尾和尾旋已使用动作窗口内的 `BakedSocketCapsule` | 头尾翼攻击继续走动作 Trace，不转为常驻硬碰撞 |
| 大型目标站位 | CD-M0～CD-M4 已建立 Attack Opportunity、DamageZone、Engagement Anchor、CandidatePose 和联合选择 | LCC 只消费这些合同，不反写根距离语义 |
| 角色接触 | 当前角色压力和重叠解算只使用根部水平半径；重叠位移会直接改确定性位置 | 缺少稳定身体体积；LCC 软压力不得复用无障碍 sweep 的直接位移路径 |
| 部位状态 | Hit Resolve 已能解析 `HitZoneId` / `BodyPartId`，Damage telemetry 也保留两者 | 缺少按部位分组的伤口、破坏和切断权威状态 |
| 地形姿态 | RuntimeSpawn 有定点 two-bone IK 求解器和 secondary pose state，但当前 two-bone solver 没有 CharacterTest 运行时消费者 | 只能作为 M4 基础，不能声明现有权威地形 IK |
| Dragon14 接触点 | 计划建立时 `body_part_contact_points.json` 为空；当前已有 3 个 PresentationOnly 头尾点 | 只用于音频/调试，不能替代身体或动画附肢软接触 |

已知合同债务：Dragon14 `body_geometry.json` 作者胶囊中心包含非零 Z，但当前 Spawn 只消费中心 Y。LCC-M0 必须把这个差异变成显式诊断；在独立合同完成前不得静默改变所有角色的 motor 中心语义。

## 3. 冻结的分层架构

| 层 | 几何来源 | 权威职责 | 是否硬阻挡 | 是否接收伤害 | 是否持久化 |
| --- | --- | --- | --- | --- | --- |
| Hard Root | `ColliderPosture` / 现有 Dynamic Character | 地形、台阶、静态障碍、角色核心不可穿越边界 | 是 | 否 | 现有角色状态 |
| Body Presence | 新增 `body_presence.json` 的少量 ModelRoot 体积 | 大型身体表面软压力、滑开和体积感 | 否 | 否 | 配置持久；世界几何按根姿态派生 |
| Animated Hurtbox | `body_colliders.json` + 离线姿态 bake | 精确受击、BodyPart/HitZone、Attack Opportunity 即时几何 | 否 | 是 | 配置/bake 持久；逐帧几何派生 |
| Active Trace | CharacterAction combat track / projectile / ability | 咬击、扫尾、翼击、武器和技能的有效帧攻击 | 否 | 产生命中 | 动作状态按既有合同持久化 |
| Animated Soft Presence | Body Presence v2 对既有 Animated Hurtbox stable collider 的显式引用 | 头、尾等选定动画附肢的有界被动软压力与接触相位 | 否 | 否 | 配置持久；同帧几何和事件派生 |

### 3.1 Body Presence 不变量

- v1 只允许 `ModelRoot` reference frame，避免逐骨动画把软接触变成甩动硬墙。
- 单角色最多 4 个 presence volume；支持 sphere、capsule、box/OBB，按 `volumeId` 稳定排序。
- Body Presence 使用独立 Combat physics layer/body-id 范围，但不得加入 `MotionObstacleLayerMask`、伤害 query mask 或导航硬阻挡。
- Body Presence 的接触输出进入现有 contact velocity / kinematic motor 路径；不得直接 `SetTransform` 做软体积去穿透。
- 静态障碍优先于软压力。角色被夹在大型身体和墙之间时允许保留有限软重叠，不允许被推入墙内或穿越障碍。
- 接触相对速度必须覆盖根平移和根 Yaw 旋转产生的表面速度；只读取输入向量不足以表达大型角色原地转身。

### 3.2 部位状态不变量

- 状态键是稳定的 `partStateId`，可聚合多个现有 `BodyPartId`；不得按每个 Hurtbox 叶子建立独立血条。
- 伤口轴与结构轴正交：伤口至少区分 Closed/Open/Consumed，结构至少区分 Intact/Broken；Severed 只在 LCC-M3 引入。
- 当前命中使用命中前状态计算伤害倍率；本次命中造成的 Open/Broken 从后续命中和后续 authority frame 生效，避免同一事件自我放大。
- 部位累积只消费已经接受且具有明确 BodyPart 证据的伤害事实；不重新查询碰撞几何，不把默认 body fallback、拒绝命中或零伤控制命中静默计入。
- 动态伤口可以改变伤害倍率、Focus 可见性和 Zone 选择优先级，但不得把原本 `Approachable=false` 的 Zone 改成可接近。

### 3.3 公共命名与程序集边界

- CharacterTest 内部类型使用 `CharacterTestBodyPresence*`、`CharacterTestBodyPartState*` 等项目名。
- 若提升到框架公共 API，使用 `CharacterBodyPresenceDefinition`、`CombatShapeSeparationResult` 等通用名；公共类型禁止出现 `Monster`、Dragon 或具体技能名。
- Combat/Runtime noEngine 数据不得依赖 `UnityEngine`；Unity 编辑、Gizmo、Scene overlay 和 Presenter 保持在对应 Unity/Editor 程序集。

### 3.4 动画附肢软接触不变量

- M5 只引用现有 Animated Hurtbox stable collider，不复制 shape、BodyPart、骨骼或动画轨，不建立第二 Authoring 几何真源。
- `CharacterHurtboxLayer` 继续不进入全局 motion obstacle/navigation/projectile blocker mask；feature query 只为软压力读取作者授权叶子。
- 复用 M1 fixed-point separation、质量/PushScale、三轮 pressure、静态障碍优先和 profile clamp；不直接位移、不造成普通接触伤害。
- previous/current 动画表面速度必须来自 authority pose/Hurtbox witness；spawn/restore/source mismatch 缺失合法 witness 时 fail closed，不读取 View Transform。
- Severed 过滤继续由 M3 Hurtbox topology 唯一决定；ContactPoint 音频配置不成为 Gameplay 参与资格。

## 4. 单一权威数据流

```text
geometry/body_presence.json
  -> Authoring Core / validation / canonical hash
  -> existing geometry_binding.json (planned v4)
  -> RuntimeSpawn binding
  -> CombatPhysicsWorld presence compound
  -> CharacterTest contact constraints
  -> existing contact velocity -> kinematic motor

geometry/body_part_damage.json
  -> Authoring Core / compiler / CharacterStudio
  -> Runtime part-state definitions
  -> accepted HitResolve + resolved BodyPartId
  -> BodyPartState authority
  -> damage/selection/reaction projection
  -> snapshot/hash/rollback/replay

CombatPhysicsWorld ground probes + baked authority pose
  -> quantized terrain targets
  -> fixed-point IK overlay
  -> the same pose channels feed Hurtbox and Unity presentation

geometry/body_presence.json v2 animatedHurtboxSources
  -> compiled stable Hurtbox collider references
  -> current-frame DamageGeometry/Hurtbox compound
  -> capsule separation against authored leaves
  -> existing bounded pressure/contact velocity -> kinematic motor
  -> derived contact phases -> presentation only
```

Runtime 不读取 Authoring 源 JSON；Unity Presenter 不回写上述 authority。

## 5. 阶段依赖与范围

```text
LCC-M0 -> LCC-M1 -> LCC-M2 -> LCC-M3 -> LCC-M4 -> LCC-M5
```

- 后续阶段可以提前进行只读调查，但生产接入和验收不得绕过前置阶段。
- LCC-M4 的地形探针研究可在 M1 后开始，但正式 pose/hurtbox 集成仍等待 M3 收口，以免同时改变部位拓扑和姿态来源。
- LCC-M5 必须等待 M4 收口，以便只消费最终同帧 Hurtbox/pose/topology，而不是建立第三套动画姿态来源。
- 每次只实施一个里程碑；发现需要改变本文第 1～4 节不变量时停止代码修改，先更新总计划和当前里程碑合同。

## 6. 里程碑状态与路由

状态模型：

```text
Planned -> In Progress -> Implementation Complete -> Verified
```

`Blocked` 只用于已确认的外部阻断，并必须记录解除条件。源码调查、设计文字或候选测试不允许把状态提升为 `Implementation Complete` 或 `Verified`。

| 里程碑 | 唯一状态 | 实施文件 | 完成边界 | 前置 |
| --- | --- | --- | --- | --- |
| LCC-M0 | `Verified` | [合同、Authoring 与分离查询基础](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_CONTRACT_FOUNDATION.md) | Presence schema、binding v4、通用 separation、Dragon14 fixture 与调试合同 | CD-M0～M4 Verified |
| LCC-M1 | `Verified` | [稳定身体 Presence 与软接触](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md) | 独立 layer/compound、稳定约束、转身表面速度、motor 安全和 Playable | M0 Verified |
| LCC-M2 | `Verified` | [伤口与部位破坏状态](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md) | 部位分组、双轴状态、伤害/选择接入、网络回滚和 CharacterStudio | M1 Verified |
| LCC-M3 | `Verified` | [切断、几何拓扑与动作失效](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md) | 稳定 collider identity、拓扑失效、Trace gate、锁/预约失效和视觉事件 | M2 Verified |
| LCC-M4 | `Verified` | [确定性地形探针与权威姿态](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_AUTHORITY_TERRAIN_POSE.md) | CombatPhysics ground probes、定点 IK、Hurtbox/表现同源、网络回滚和斜坡 Playable | M3 Verified |
| LCC-M5 | `Verified` | [动画附肢软接触](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_ANIMATED_APPENDAGE_SOFT_CONTACT.md) | Animated Hurtbox 引用、同帧软压力、接触反馈、Severed 联动和 Dragon14 Playable | M4 Verified |

### 6.1 单任务执行包准备度

执行包准备度与里程碑状态彼此独立。`Audit-Frozen` 只允许派发当前里程碑 T0 只读审计；T0 只在自身冻结表和结构化回报中记录精确文件/partial owner、测试 filter、证据目录和交接点，主代理复核并回写各生产任务后，当前里程碑才进入 `Production-Frozen`，允许按 DAG 派发 T1+。两种准备度都不表示生产实现存在。

| 里程碑 | 执行包 | 准备度 | 派发边界 |
| --- | ---: | --- | --- |
| LCC-M0 | 6 | `Production-Frozen` | T0～T5 已全部 Verified；基础合同、实现与验收闭合 |
| LCC-M1 | 6 | `Production-Frozen` | T0～T5 已全部 Verified；Runtime Slice、Playable 与终审闭合 |
| LCC-M2 | 7 | `Production-Frozen` | T0～T6 已全部 Verified；M2 Gate 与唯一终审闭合 |
| LCC-M3 | 7 | `Production-Frozen` | T0～T6 已全部 Verified；唯一终审两项P1整改关闭，当前未关闭P0/P1=0 |
| LCC-M4 | 6 | `Production-Frozen` | T0～T5 已全部 Verified；Playable、性能、接口与唯一终审闭合 |
| LCC-M5 | 4 | `Production-Frozen` | T0～T3 已全部Verified；Authoring/Runtime/Playable/网络/性能与唯一终审闭合 |

派发和写入规则：

- 主代理是总台账、父里程碑状态表和状态历史的唯一写入者；子代理不得修改这些状态权威。
- 子代理只写当前任务执行文档授权的范围，并保留工作区既有修改；同一生产文件同一批次只有一个 owner。
- T0 可在 `Audit-Frozen` 且阶段前置满足时进入 `In Progress`；T1+ 还要求当前里程碑 `Production-Frozen`。主代理先更新工作包状态，子代理完成回报最多形成候选实现。
- 主代理检查 diff 并复跑必要验证后，才按证据将任务晋升为 `Implementation Complete` 或 `Verified`。
- schema、碰撞数学、网络、拓扑与正式资产等高风险任务最多安排一次不同于实现者的只读审查；P2 建议不阻塞实施，除非它实际影响验收合同。

状态更新规则：

- 当前里程碑第一个工作包进入 `In Progress` 时，先更新本表并追加第 10 节状态历史。
- 工作包状态只在对应里程碑文件维护；总表不复制工作包进度。
- 当前里程碑全部计划内实现已完成但最终 Playable、性能、Authoring 或独立审查未闭合时，最多为 `Implementation Complete`。
- 只有当前里程碑完成 Gate 全部满足，才能进入 `Verified` 并解除下一里程碑前置。

## 7. Schema、身份与网络演进

- LCC-M0 计划把 `mx.characterGeometryBinding.v3` 扩展为 v4；v1～v3 继续兼容读，只有存在 Current Presence 文档时输出 v4。
- Presence 配置、根姿态和当前/上一 tick 根变换足以派生接触几何时，不新增 formal snapshot 字段；source revision/content hash 必须覆盖 presence binding，restore 后必须清理并确定性重建派生缓存。
- LCC-M2 建立首个持久部位状态。计划基线时 CharacterTest formal network schema 为 29，预期使用 30；若实施前已有其它 schema 升级，必须取下一可用版本并同步本文，不得争用版本号。
- LCC-M3 若增加持久 Severed 状态或动作 gate 状态，使用下一可用 schema；不得把切断仅保存在 Presenter 或 compound 拓扑中。
- LCC-M4 的 probe/IK target、迟滞或任何影响未来 Hurtbox 的状态使用下一可用 schema；纯当帧可重算数据不重复写入 snapshot。
- LCC-M5 首选从现有 animation/action/pose/topology 与当前/前一 authority frame 纯派生，并复用既有 contact velocity/state，不预占 schema。T0 若证明动画 surface witness 影响未来命令且无法重建，必须停止并登记下一可用 schema 34 候选。
- 每次 schema 变化都必须覆盖 clone、capture/apply、full/owned/non-owned hash、rollback 后下一 tick command parity、replay、late join 兼容和旧 schema 迁移/fail-closed。

## 8. 统一验证门禁

每个里程碑按实际改动执行 `Docs/QUALITY_GATE.md`，最低还包括：

### 8.1 自动化

- Authoring round-trip、canonical hash、稳定排序、缺失文件、非法引用、未知 schema 原文保护和禁止覆盖。
- narrow phase/separation 的退化法线、完全重合、边界相切、容器顺序扰动和 fixed-point 重演。
- snapshot/hash/rollback/replay 只在存在新权威状态时纳入，但不得因“派生”名义遗漏影响未来命令的缓存。
- 受影响的 CD-M0～M4 Attack Opportunity、DamageZone、Engagement Anchor、Dragon14 natural attack 和 CharacterTest motion 回归。

### 8.2 Unity 与直接证据

- 普通 C# 改动完成 Unity refresh/compile/domain reload、Editor ready 和 Console 0 error。
- Dragon14 正式 fixture 的 Game View/Play Mode 直接证据；headless 测试不能替代画面、接触感或 CharacterStudio 事务验收。
- Debug overlay 同时区分 Hard Root、Body Presence、Animated Hurtbox 和 Active Trace；不得用视图 Gizmo 反向证明 authority 已接入。

### 8.3 性能与确定性

- 高频路径初始化及容量 warm-up 后 0 B GC；不得在 tick 中解析 JSON、LINQ、字符串组装或动态扩容。
- 至少覆盖 8 个角色、Dragon14 18 Hurtbox 加最多 4 Presence volume 的完整 Sync + contact 路径；只测孤立 query 不足以证明性能。
- 实体、volume、collider、BodyPart、Zone 和 action gate 使用显式稳定比较器；字典、broadphase 或 Unity hierarchy 返回顺序不得参与 authority。

### 8.4 安全场景

- 玩家进入 Dragon14 胸腹区域会被平滑推出，但靠墙时不嵌墙、不穿墙、不产生无界速度。
- 静止翼膜仍不阻挡移动。头、尾中和尾尖不成为硬阻挡，但 M5 作者授权叶子必须产生有界软压力/接触反馈；相应攻击仍只在 authored action window 内产生伤害。
- 大型角色平移、原地转身、角色相向移动、同中心退化、死亡/不可推动姿态和 rollback 后继续一 tick 均有确定性覆盖。

## 9. 最小上下文加载规则

实施任一 LCC 工作包时只加载：

1. 本总台账的第 1～8 节；
2. 当前一个里程碑文件；
3. 当前单任务执行文档，以及该文档点名的现有源码、测试和接口章节；
4. 涉及移动/战斗时读取 `Docs/Guides/CHARACTER_TEST_GAME_DEVELOPMENT_STANDARD.md`、`Docs/Interfaces/Combat.md`；涉及 schema/网络时加 `Docs/Interfaces/Network.md`；涉及 Authoring/CharacterStudio 时加 Character Resource Package 与 Authoring 权威文档；涉及 C# 时加 `Docs/API_STANDARDS.md` 和 `Docs/QUALITY_GATE.md`。

默认不一次加载六个里程碑全文。跨里程碑合同审查或总计划变更才读取多个子文件。

## 10. 路线实际交付与状态历史

### 10.1 当前实际交付

| 项目 | 当前记录 |
| --- | --- |
| 路线状态 | `Verified`；M0、M1均6/6，M2、M3均7/7，M4为6/6，M5为4/4；六个里程碑与36/36任务全部Verified |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区内实施；未提交，保留既有非 LCC 修改 |
| 实际源码 / 资产变更 | M0建立Authoring/binding/separation基础；M1建立Body Presence软接触；M2建立四态Authoring与binding v5、max8双轴BodyPartState、accepted damage桥、动态Zone/lock、schema30网络、CharacterStudio与Dragon14四组正式数据。M3已落最小Severed定义/state/Studio、Dragon14三尾叶18→15、Zone/lock/Anchor reservation/Planner精确失效、事件级动作依赖、Runtime Slice表现，以及schema31与restore后Hurtbox/Zone/Planner一致性。M4已落四肢authority rig作者真源、geometry v6/runtime binding、现有socket-bake optional basis、single-best fixed-point ground probe、解析sphere-cast、唯一resolved frame/schema32，以及Hurtbox、claw Trace/Planner、显式ContactPoint和View的同帧消费。M5新增Body Presence v2/geometry v7、Dragon14 head/tail-mid/tail-tip refs、v7 RuntimeSpawn/Network/M1 consumer兼容、selected animated Hurtbox same-tick witness/soft pressure/phase和正式验收fixtures。 |
| 自动化 / Unity | M2主代理最终矩阵完整通过；M3 T1 RuntimeBinding5/5、BodyPartState9/9；T2 focused5/5、既有删除回归1/1；T3 focused5/5、既有CD回归26/26；T4 focused8/8；T5 EditMode4/4、T4行为2/2、PlayMode1/1；T6两条P1各1/1、network/replay 16/16、核心62/62、validator fallback完整且Console0。M4-T1 Authoring focused与CLI build、rig/basis5/5、AuthorityPose回归5/5、Unity validator通过；M4-T2最终15/15、4096次0 B、代码规范0问题、分步scripts compile/domain reload与Console0；M4-T3 terrain6/6、network6/6、replay3/3、最小回归6/6、分步Unity gate与Console0；M4-T4三组focused合8/8、4/4、11/11及Hurtbox数值1/1通过，唯一integrated validator完整通过且Console0；M4-T5 terrain-active PlayMode3/3、EditMode290/290、冻结PlayMode12/12、network/rollback各1/1、performance2/2。M5最终PlayMode5/5、focused/network/performance13/13、M1/M3/M4窄回归39/39，最终validator完整通过且Console0。 |
| Playable / CharacterStudio | M2 CharacterStudio CRUD/成员映射/Undo/Redo/草稿/规范化保存与窄宽画面闭合；正式PlayMode证明Head Open→Broken→死亡及头/左右翼/尾四组隔离。M4正式Dragon14 terrain-active PlayMode覆盖平/上下坡、台阶、不均匀支撑、失败/恢复和状态切换。M5正式PlayMode显示真实Dragon14/player views，三附肢下一tick位移75/41/74 mm，moving/turning/high-speed/wall-corner/Severed与HP/CombatHit隔离直接通过；Game View使用明确tests-only marker，不冒充production feedback。 |
| 性能 / NoAlloc | M0/M1既有warmed路径保持0 B；M2八entity 4096次state/pre-hit/commit/selector为0 B；M3 T2断尾15叶64 warmup+512次SyncAll为0 B；T3 selector+joint projection与T4 eligibility+trace prune各warmed 4096次为0 B；M4-T3四肢fixed-capacity authority tick与T4 helper/contact warmed路径为0 B；M4-T5以8只Dragon实测双Hurtbox Sync全链p50/p95/max=`1238.5/1821.4/6708.6 µs`、0 B。M5正式v7 8 Dragon+1 player、3 source/Dragon、RuntimeModule warm300/measure1800 whole tick p50/p95/max=`2125.7/3010.4/3964.0 µs`、0 B；ProfilerRecorder分段值在EditMode为0且未伪报。 |
| 能力表 / 接口回写 | M0～M5的CharacterApplication/Combat/Animation/CharacterAction/Network/Audio与CAPABILITIES均按实际能力回写；M2明确Focus只有typed ingress，M3明确Severed只有typed ingress与Runtime Slice表现，M4为Framework Feature且Playable证据限定Dragon14四肢，M5为Dragon14 head/tail-mid/tail-tip Animated Soft Presence Playable。 |
| 已知限制 | M2保留非阻塞Focus adapter P2与catalog唯一性P3。M3保留无生产sever caller、单basemesh Runtime Slice。M4接受catalog体积、双Sync和正式Dragon内容覆盖P3。M5唯一终审最终P0/P1=0，保留phase Source/Target身份P2（接Audio/VFX前修正）、Studio全smoke与无production appendage Audio/VFX P3；Game View标记为tests-only且画面dense，无production统一overlay。6个非M5 EditMode陈旧断言和1个非M5 ActionId PlayMode断言不计为M5通过。未跑全仓、dedicated server、多进程或公网transport，不声明翼膜/全身/硬附肢墙/ragdoll或任意生物通用支持。 |

### 10.2 状态历史

| 日期 | 里程碑 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-24 | LCC-M0～M4 | 初始化 | `Planned` | 创建总计划与五个里程碑实施文件，草拟分层、阶段边界、schema/网络策略和统一验收矩阵 | 只完成计划落盘；执行包尚未审查，未修改 Runtime、Authoring、资产或测试 |
| 2026-08-24 | LCC-M0～M4 | 执行包缺失 | `Drafting` | 创建统一模板和 M0～M4 共 32 份单任务执行文档；登记任务目标、非目标、DAG、写入 owner、验证、派发与实际记录 | 仍需完成全量结构/链接/边界审查，未派发子代理 |
| 2026-08-24 | LCC-M0～M4 | `Drafting` | `Audit-Frozen` | 三组独立只读审查覆盖 M0/M1、M2/M3、M4/跨路线；32/32 任务结构与本地链接通过，审查问题已回写，P0/P1 为 0 | 只授权满足阶段前置的 T0；T1+ 仍等待各自 `Production-Frozen`，无生产实现或运行证据 |
| 2026-08-24 | LCC-M0 | `Planned` | `In Progress` | M0-T0 按两级门禁进入只读源码/测试/资产路径审计，冻结后续生产任务的精确 owner、测试 filter、证据名与交接点 | 只授权写 M0-T0 文档；M0-T1+ 仍禁止派发，无生产实现 |
| 2026-08-24 | LCC-M0 | `Audit-Frozen` | `Production-Frozen` | 根据用户“避免过度编码”要求，将 25 万字节形式化草案收敛为约 1 万字节最小合同；不再要求自研 fixed-limb 数学、定制证据封存或循环评审，T0 完成源码入口、owner、兼容与验证边界冻结 | T1 与 T3 已可派发；生产功能和运行证据仍为空 |
| 2026-08-24 | LCC-M0-T1 | `In Progress` | `Verified` | Body Presence Authoring Core 与独立安全保存落地；build/focused/CLI 通过并经主代理修订复核 | 未声明 binding、Runtime 或可玩接触；T2 已解锁 |
| 2026-08-24 | LCC-M0-T3 | `In Progress` | `Verified` | 通用 fixed-point separation API 落地；61/61 合并回归、主代理 8/8、4096 warmed 0 B，唯一独立审查的两个 P1 修复后关闭 | 仅为几何事实；不改变角色移动、伤害或 Presence 行为 |
| 2026-08-24 | LCC-M0-T2 | `In Progress` | `Verified` | binding v4、source mapping 和 RuntimeSpawn v1～v4 严格兼容落地；主代理 focused 通过，唯一 schema 审查 P0/P1 为 0 | 未迁移正式角色资产或接 Gameplay；T4 已解锁 |
| 2026-08-24 | LCC-M0-T4 | `In Progress` | `Verified` | Dragon14 正式 fixture/v4 生成、typed center debt 与三层 debug 投影落地；主代理 focused、脚本验证与两张直接图像通过 | Game capture 不合成 Gizmo；M1 前仍无接触行为 |
| 2026-08-24 | LCC-M0 | `In Progress` | `Verified` | T5 CharacterStudio/service/文档合并验收通过；Node/Authoring/Unity focused、Console 0 和正式画面证据闭合；唯一终审 P0=0、P1=0 | in-app browser 不可用，API/Node 与 Dragon Playable 画面证据保留；M0 不声明 Runtime 推挤 |
| 2026-08-24 | LCC-M1 | `Planned` | `In Progress` | M0 Gate 解除后只派发 M1-T0 Runtime 接触链与预算只读审计 | M1 仍为 `Audit-Frozen`；T1～T5 生产写入继续等待 T0 回写与 `Production-Frozen` |
| 2026-08-24 | LCC-M1-T0 | `In Progress` | `Verified` | 真实 tick/direct displacement、layer/identity/capacity、same-tick pose/no-schema、snapshot/hash、partial owner 与性能测法冻结；主代理链接/diff/事实复核通过 | 只读审计，无 Runtime 能力；T1 range stop-check 与 T4 P95/source identity 实证仍待关闭 |
| 2026-08-24 | LCC-M1-T1 | `Planned` | `In Progress` | M1 提升为 `Production-Frozen`，只派发 Presence body/compound、dirty gate、lifecycle 与隔离测试 | 不得接 contact pressure、direct displacement、snapshot schema 或 Gameplay 行为 |
| 2026-08-24 | LCC-M1-T1 | `In Progress` | `Verified` | 独立 layer/body/compound、稳定 identity、8×4 容量、dirty skip、restore 与 fail-closed 落地；主代理 6/6、4096 warmed 0 B、Console 0 | 仅为派生接触几何，不声明软接触或 player-facing 行为 |
| 2026-08-24 | LCC-M1-T2 | `Planned` | `In Progress` | T1 Gate 解除，按冻结 partial 派发 Presence constraint、稳定去重与共享三轮 pressure solver 接入 | T3 surface velocity、Network、Debug 和 direct displacement 保持锁定 |
| 2026-08-24 | LCC-M1-T2 | `In Progress` | `Verified` | 28 raw/28 pair/224 directed constraint、稳定 key、全量预检 fail-closed 与三轮 pressure 接入闭合；focused 6/6、8×4 warmed 128 帧 0 B、Console 0 | 尚不声明 actual surface velocity、墙边 motor 安全或 player-facing 推挤 |
| 2026-08-24 | LCC-M1-T3 | `Planned` | `In Progress` | T2 Gate 后顺序派发 same-tick previous/current pose、root/Yaw surface velocity、profile-derived clamp 与 static-sweep safety | 禁止新增通用抽象、猜测常量、跨 tick 私有状态、direct displacement 或 schema |
| 2026-08-24 | LCC-M1-T3 | `In Progress` | `Verified` | actual root/Yaw surface velocity、pressureScale、profile clamp、typed fail-closed 与 next-tick wall sweep 闭合；focused 6/6、8×4×224 warmed 128 帧 0 B、最终 Unity 验证 Console 0 | 未跑逐任务评审/全仓/PlayMode；网络、完整 P95 与角落/窄通道留 T4/T5 |
| 2026-08-24 | LCC-M1-T4 | `Planned` | `In Progress` | T3 Gate 后顺序派发现有 contact velocity 的 snapshot/hash/rollback/replay 实证、Presence numeric diagnostics/overlay 与完整 NoAlloc/P95 | 不预占 schema、不修改 T1～T3 runtime；缺陷回派原 owner；仅 T5 保留唯一终审 |
| 2026-08-24 | LCC-M1-T4 | `In Progress` | `Verified` | package identity/replay fail-closed、contact velocity authority parity、只读诊断/四层 overlay 闭合；Authority 8/8、Replay/Debug 4/4、4096 帧 0 B/P95 与 Console 0 | 未声明 Playable；无逐任务独立评审，唯一 P0/P1 终审仍在 T5 |
| 2026-08-24 | LCC-M1-T5 | `Planned` | `In Progress` | T4 Gate 后按冻结验收 owner 派发 Dragon14 Playable、墙角/窄通道、Game View、回归、性能门槛、能力表与一次终审 | 不扩张核心实现；核心缺陷回派 T1～T4 owner |
| 2026-08-24 | LCC-M1-T5 | `In Progress` | `Candidate Complete` | 新增聚合 PlayMode 3/3；T5 捕获的非阻挡目标回归按 T2 owner 单行回修后 Presence/自然攻击恢复；Authority 8/8、Replay/Debug 4/4、4096 帧 0 B/P95 1.687 ms；正式 CharacterTestScene Dragon14 + 四层 overlay Game View、Console 0、场景 clean | 彩色几何 Gizmo 未进入标准 Game View capture；非零接触由 PlayMode 证明；只剩唯一 P0/P1 终审 |
| 2026-08-24 | LCC-M1 | `Candidate Complete` | `Verified` | 唯一最终只读审查 P0=0、P1=0、P2/P3=0；确认 Presence soft-contact、motor safety、authority/replay/package identity 与组合证据满足 Gate，M1 6/6 收口 | 保留无持续人工输入视频/非零 contact 截图和无彩色 Game View Gizmo 的证据限制；不追加评审轮次 |
| 2026-08-24 | LCC-M2-T0 | `Planned` | `In Progress` | M1 Gate 解除后只派发伤害链、双轴部位状态、source policy、schema 与 owner 的只读审计 | M2 仍为 `Audit-Frozen`；T1～T6 生产写入继续锁定 |
| 2026-08-24 | LCC-M2-T0 | `In Progress` | `Verified` | 复核 resolver→final damage→HP、BodyPart fallback、DamageZone comparator 与 formal schema；冻结 schema 30、内部 typed provenance、显式 Focus consume ingress、max 8 和 T1～T6 owner | 仅 source-level 审计；无 M2 生产代码、测试或 Unity 证据；不实现 BigInteger 或通用框架 |
| 2026-08-24 | LCC-M2-T1 | `Planned` | `In Progress` | M2 提升为 `Production-Frozen`，只解除 body_part_damage Authoring/compiler/runtime binding 与 Dragon14 四组定义 | T2～T6 继续等待各自前置；共享 damage/selector/network/UI 文件禁止写 |
| 2026-08-24 | LCC-M2-T1 | `In Progress` | `Verified` | 四态安全保存、typed validation、binding v5/v1～v4兼容、Dragon14四组/10 lookup和重复生成闭合；主代理补两处fail-closed后Authoring/CLI/Unity/RuntimeSpawn focused通过 | Dragon阈值待T6校准；既有ResourceMapping build-target后置项保留；无任务级评审 |
| 2026-08-24 | LCC-M2-T2 | `Planned` | `In Progress` | T1 Gate解除，顺序派发fixed-capacity BodyPartState、frame-start snapshot、bounded reducer与typed transition | T3～T6仍锁定；不接damage、selector或network |
| 2026-08-24 | LCC-M2-T2 | `In Progress` | `Verified` | max 8 state、frame-start read view、damage-first reducer、逐轴revision、typed transition与spawn/reset闭合；主代理修正空Authored分类后EditMode 7/7、4096 warmed 0 B、Unity Console 0 | 未接damage/selector/network/UI；无任务级独立评审 |
| 2026-08-24 | LCC-M2-T3 | `Planned` | `In Progress` | T2 Gate解除，顺序派发pre-hit modifier、accepted-damage/typed Focus adapters、source policy与transition projection | T4～T6仍锁定；不得旁路HP authority或新增通用事件框架 |
| 2026-08-24 | LCC-M2-T3 | `In Progress` | `Verified` | frame-start伤害修饰、HP后accepted fact、typed source/Focus与派生telemetry/reaction闭合；主代理复跑34/34、4096 warmed 0 B、Unity Console 0 | 未跑全仓测试/PlayMode或任务级评审；T4已解锁 |
| 2026-08-24 | LCC-M2-T4 | `Planned` | `In Progress` | 按冻结owner派发动态DamageZone priority、state revision lock重验、joint selector与Attack Opportunity回归 | 不改变Approachable、root XZ distance、GeometryInReach或pose-path cost |
| 2026-08-24 | LCC-M2-T4 | `In Progress` | `Verified` | dynamic priority只替换既有比较位，eligibility/迟滞/root distance不变，ordinary/joint同源；主代理9/9、代理141/141、4096 warmed 0 B、Unity Console 0 | formal snapshot/hash/rollback留T5；无任务级独立评审 |
| 2026-08-24 | LCC-M2-T5 | `Planned` | `In Progress` | 当前最高formal schema仍为29，按冻结30派发BodyPartState、Zone lock witness、full/owned/non-owned hash与rollback/replay/late join | 禁止持久化派生priority、Focus显示、telemetry或Severed |
| 2026-08-25 | LCC-M2-T5 | `In Progress` | `Verified` | schema30、canonical snapshot/三类hash、legacy、rollback+1、replay/late join/source mismatch与transient清理闭合；v5 catalog identity按上游owner窄修后主代理组合20/20、Unity Console 0 | 无任务级独立评审；M2唯一终审仍留T6 |
| 2026-08-25 | LCC-M2-T6 | `Planned` | `In Progress` | T5 Gate解除，顺序派发CharacterStudio编辑闭环、Dragon14校准/Playable、八角色NoAlloc、能力表和一次P0/P1终审 | 不扩张到Severed、Hurtbox拓扑或动作依赖；缺陷按原owner回派 |
| 2026-08-25 | LCC-M2 | `In Progress` | `Verified` | T6 CharacterStudio、正式四组/连段、八角色0 B、v5 Presence回归和主代理最终矩阵闭合；唯一终审P0=0、P1=0，M2为7/7 Verified | 接受Focus生产adapter P2与catalog全局root唯一性P3；登记后续门禁，不追加评审轮次 |
| 2026-08-25 | LCC-M3-T0 | `Planned` | `In Progress` | M2 Gate解除后只派发Severed状态、Hurtbox topology、Zone/Anchor/action依赖、network与Dragon14资源seam的只读审计 | M3仍为`Audit-Frozen`；只允许写T0冻结表，T1～T6生产任务继续锁定 |
| 2026-08-25 | LCC-M3-T0 | `In Progress` | `Verified` | 冻结Broken起始帧typed Severed、3个尾部稳定叶、zone.tail/anchor.tail.rear失效、两个真实尾部事件的事件级gate、Runtime Slice、schema31候选和T1～T6文件owner | 仅source/resource审计，无Severed生产能力；不引入大整数、HTTP取证或通用抽象 |
| 2026-08-25 | LCC-M3-T1 | `Planned` | `In Progress` | M3提升为`Production-Frozen`，只解除body-part Authoring/compiler/binding/state、CharacterStudio和Dragon14尾部定义owner | T2～T6按DAG继续锁定；里程碑末仅保留T6一次终审 |
| 2026-08-25 | LCC-M3-T1 | `In Progress` | `Verified` | 最小三字段extension、typed Severed reducer/event、stable filter/dependency、Dragon14 revision2和Studio闭合；Authoring/CLI/Studio、RuntimeBinding5/5、State9/9、Unity Console0 | formal topology/action/network未实现；schema30 enum隔离列入T6 required gate |
| 2026-08-25 | LCC-M3-T2 | `Planned` | `In Progress` | T1 Gate解除后只派发Hurtbox稳定叶过滤、compound removal/refit、DamageGeometry topology witness与focused tests | T3～T6继续等待；不创建通用topology manager或修改root distance |
| 2026-08-25 | LCC-M3-T2 | `In Progress` | `Verified` | 三条尾叶18→15、原ordinal ID、typed unavailable/InvalidBinding、restore重建与topology witness闭合；focused5/5、删除回归1/1、512 measured 0 B、Unity Console0 | 未跑PlayMode/全仓/独立评审；不修改Combat core、Presence或T3+ owner |
| 2026-08-25 | LCC-M3-T3 | `Planned` | `In Progress` | T2 Gate解除后只派发Zone/lock、Anchor reservation、Attack Opportunity与Planner topology invalidation及纯状态重建 | T4～T6继续等待；不改root distance、动作Trace、Presenter或network |
| 2026-08-25 | LCC-M3-T3 | `In Progress` | `Verified` | authored Zone identity与精确member witness失效、同帧reservation sweep、Attack Opportunity/Joint/Planner同源排除闭合；主代理读取focused5/5，既有回归26/26、4096 warmed 0 B、Unity Console0 | 未跑PlayMode/全仓/独立评审；formal action、presentation、network按T4～T6串行收口 |
| 2026-08-25 | LCC-M3-T4 | `Planned` | `In Progress` | T3 Gate解除，只派发最小typed partStateId dependency与Dragon14两个既有尾部事件的事件级Trace gate | 不 gate整段动作或bite；不改T3 targeting、T5 presentation、T6 network，不新增通用condition框架 |
| 2026-08-25 | LCC-M3-T4 | `In Progress` | `Verified` | 两字段事件依赖、Runtime/Planner同源eligibility、下一帧active-trace prune与两个正式Dragon14事件闭合；主代理读取focused8/8、4096 warmed 0 B及分步compile/reload/Console0证据 | integrated validator在domain reload时transport断线且无job id，按规则保留回退证据；未跑PlayMode/全仓/独立评审 |
| 2026-08-25 | LCC-M3-T5 | `Planned` | `In Progress` | T4 Gate解除，只派发Severed事件消费、typed presentation fallback、生命周期/overlay与真实Game View的Runtime Slice收口 | 当前无独立尾部mesh/resource；禁止伪造detach/Rigidbody/world limb，不回写authority |
| 2026-08-25 | LCC-M3-T5 | `In Progress` | `Verified` | 一次性Severed事件消费、late attach/restore静默投影、Release/respawn生命周期、typed资源降级与真实before/after Game View闭合；主代理读取EditMode4/4、T4回归2/2、PlayMode1/1并目视核对画面 | after为Editor-only acceptance drive，不证明生产sever ingress/player path；单basemesh只声明Runtime Slice；无任务级独立评审 |
| 2026-08-25 | LCC-M3-T6 | `Planned` | `In Progress` | T5 Gate解除，顺序派发schema31、hash/rollback/replay/late join、组合回归/性能、CharacterStudio/能力表与里程碑唯一独立终审前候选收口 | 不扩大翼/头切断、不伪造完整Playable；生产caller缺失与Runtime Slice限制必须保留，核心缺陷回派原owner |
| 2026-08-25 | LCC-M3 | `In Progress` | `Verified` | T6 schema31与恢复一致性闭合；唯一独立终审原P0=0/P1=2/P2=1/P3=2，两项P1按原owner整改并由主代理核对源码及1/1、1/1、16/16 job关闭，当前未关闭P0/P1=0；M3为7/7 Verified | 不追加第二轮独立终审；接受无生产caller、单basemesh Runtime Slice与无低频硬阈值限制 |
| 2026-08-25 | LCC-M4-T0 | `Planned` | `In Progress` | M3 Gate解除后只派发authority pose、CombatPhysics static query、Dragon14四肢rig、状态/schema、consumer与预算的只读审计 | 仅允许写T0冻结表与Evidence；T1～T5生产写入继续锁定，不引入BigInteger、Unity Raycast或新通用框架 |
| 2026-08-25 | LCC-M4-T0 / M4 / T1 | `In Progress` / `Audit-Frozen` / `Planned` | `Verified` / `Production-Frozen` / `In Progress` | T0冻结现有secondary-only分叉链、schema32候选、Combat static-layer stop gate、Dragon14四肢rig缺口和最小owner；主代理移除T1～T4任务级独立评审、要求复用现有socket bake后串行派发T1 | 总进度27/32；M4 terrain probe/IK/resolved frame/消费者仍未实现，不新增BigInteger、HTTP或通用求解框架 |
| 2026-08-25 | LCC-M4-T1 / T2 | `In Progress` / `Planned` | `Verified` / `In Progress` | T1完成四肢authority rig作者真源、geometry v6/runtime binding和既有catalog optional basis；basis合法性P1由原owner窄修，主代理以最终5/5 focused、5/5回归、Unity Console0和代码规范0问题关闭后派发T2 | 总进度28/32；接受约10.88 MB首版JSON catalog P3，不新增BigInteger、HTTP、第二协议或通用框架 |
| 2026-08-25 | LCC-M4-T2 / T3 | `In Progress` / `Planned` | `Verified` / `In Progress` | T2完成single-best fixed-point ray/解析sphere-cast、四target与compound材质回查；主代理关闭轴向TOI、无来源边界及nonzero-radius OBB证据缺口，最终15/15、4096次0 B、分步Unity gate与Console0后派发T3 | 总进度29/32；integrated validator首次CS0177失败原证据保留，不新增BigInteger、Unity Physics或新碰撞框架 |
| 2026-08-25 | LCC-M4-T3 / T4 | `In Progress` / `Planned` | `Verified` / `In Progress` | T3完成固定4肢/32样本resolved authority frame、fixed-point IK/eligibility/失效恢复、schema32与网络链；主代理关闭原点target、future-state瘦身、orientation、v6 bootstrap与schema27兼容缺口，直接复核15/15、回归6/6、0 B与Console0后派发T4 | 总进度30/32；integrated validator的domain-reload session断线原证据保留，不新增BigInteger、HTTP、第二snapshot或通用Pose Graph |
| 2026-08-25 | LCC-M4-T4 / T5 | `In Progress` / `Planned` | `Verified` / `In Progress` | T4完成Hurtbox、claw Trace/Planner、显式ContactPoint与View的同帧/revision消费；34项focused、warmed 0 B、Dragon14 Game View初证与唯一integrated validator通过后串行派发T5 | 总进度31/32；terrain-active最终Playable、统一数值overlay、8角色性能、双Hurtbox sync成本与唯一P0/P1终审待收口 |
| 2026-08-25 | LCC-M4-T5 / LCC-M4 / 全路线 | `In Progress` / `In Progress` / `In Progress` | `Verified` / `Verified` / `Verified` | T5完成terrain-active PlayMode、schema32 rollback+1 consumer parity、8 Dragon性能/0 B、聚焦回归、接口与Unity脚本门禁；唯一最终只读审查P0=0、P1=0、P2=1、P3=3，无生产回派；五个里程碑与32/32任务全部闭合 | P2父状态残留随封版关闭；接受catalog体积、双Sync与正式Dragon内容覆盖P3，不追加评审轮次，不宣称全仓/dedicated server/公网transport或任意全身生物求解 |
| 2026-08-26 | LCC-M5 / 全路线 | 功能未登记 / `Verified` | `Planned` / `In Progress` | 复核用户实际体验后确认原路线把“不做动画硬阻挡”错误收窄成“无被动接触反馈”；新增M5及T0～T3四份执行文档，目标为选定Animated Hurtbox叶子的有界软压力、Severed联动和Dragon14 Playable | 本轮只补计划，不实现或派发；M0～M4既有Verified事实保持 |
| 2026-08-26 | LCC-M5 执行包 | `Drafting` | `Audit-Frozen` | 主代理检查四份任务的必填章节、DAG、目录级owner、禁止范围、验证/回滚合同和本地Markdown链接；无缺失链接或diff whitespace错误 | 只授权未来T0只读审计；T1～T3仍等待T0回写与Production-Frozen，无生产能力 |
| 2026-08-26 | LCC-M5-T0 / LCC-M5 | `Planned` / `Planned` | `In Progress` / `In Progress` | 用户要求按文档开始实现；主代理按Audit-Frozen门禁更新状态并只派发T0运行合同只读审计 | T1～T3仍锁定；本步不改Runtime、Authoring、资产或测试 |
| 2026-08-26 | LCC-M5-T0 / M5 / T1 | `In Progress` / `Audit-Frozen` / `Planned` | `Verified` / `Production-Frozen` / `In Progress` | T0冻结双Hurtbox Sync、same-tick previous/current witness、no-schema34、v2/v7、8×4/256容量、Severed N+1边界和T1～T3精确owner/filter；主代理复核源码锚点与diff后解锁T1 | T2/T3继续锁定；尚无生产软接触或Playable证据 |
| 2026-08-26 | LCC-M5-T1 / T2 | `In Progress` / `Planned` | `Verified` / `In Progress` | T1完成v2/v7 Authoring/RuntimeSpawn/Studio与Dragon14三引用正式生成；Authoring focused、Unity 3/3、脚本门禁和双compile hash闭合，主代理复核生成binding三source后解锁T2 | 全Studio smoke被既有ActionSet numeric fixture TypeError阻断并登记；尚无T2 runtime行为 |
| 2026-08-26 | LCC-M5-T2 / T3 | `In Progress` / `Planned` | `Verified` / `In Progress` | T2完成双Sync后的same-tick selected-leaf软接触、256固定约束、M1 pressure复用与phase；focused8/8、M1/M3回归19/19、0 B和Unity门禁闭合，主代理复核无hard-mask/direct-displacement/damage旁路后解锁T3 | M4 consumer陈旧0-ContactPoint断言交T3窄迁移；Playable、全network/replay、timing和唯一终审未执行 |
| 2026-08-26 | LCC-M5-T3 / T1 | `In Progress` / `Verified` | `In Progress` / `In Progress` | T3正式PlayMode在T2修复后通过三附肢有界推出，性能1800帧0 B；全network/replay回归暴露v7 authority identity与M1 root Presence consumer仍为v6-only，主代理回派T1精确兼容修复 | T3暂停且不伪造网络/最终门禁；T1重新打开，M5降回2/4 Verified |
| 2026-08-26 | LCC-M5-T1 / T3 / 唯一终审 | `In Progress` / `In Progress` / 未执行 | `Verified` / `In Progress` / `Not Ready` | T1关闭v7 consumer并30/30；T3形成正式三部位/rollback/0B候选；唯一只读终审P0=0/P1=2/P2=1/P3=2，指出截图为暂停菜单且性能非正式双Sync/full-chain | M5保持35/36；回派T3补有效多帧Playable/画面和正式RuntimeModule 300+1800性能，同一审查上下文仅做P1关闭核对 |
| 2026-08-26 | LCC-M5-T3 / LCC-M5 / 全路线 | `In Progress` / `In Progress` / `In Progress` | `Verified` / `Verified` / `Verified` | T3补真实views、多帧三附肢位移、moving/turning/high-speed/wall-corner/Severed、正式RuntimeModule 300+1800与最终5/5、13/13、39/39、Console0；唯一终审同上下文关闭两项P1，最终P0/P1=0、P2=1/P3=2 | 六里程碑36/36闭合；保留phase身份、Studio smoke、Audio/VFX/overlay、dense tests-only视觉与非M5陈旧基线限制，不追加审查 |
| 2026-08-26 | LCC-M5 post-Verified | 软推导致角色当前/延迟转向 | `Verified` | solver改读原始Facing，CharacterTest Facing fallback改用DesiredVelocity而非被外力写回的HorizontalVelocity；solver10/10、正式M5连续多tick PlayMode2/2、Unity编译后Console0 | 不改软压力、位移、lock target、显式turn或主动输入朝向语义 |
