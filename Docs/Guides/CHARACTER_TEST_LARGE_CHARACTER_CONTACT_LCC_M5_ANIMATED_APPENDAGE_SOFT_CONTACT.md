# LCC-M5：动画附肢软接触

> Status: Guide
>
> 里程碑：LCC-M5
>
> 里程碑状态权威：[LCC 实施总台账第 6 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)
>
> 当前状态：`Verified`
>
> 执行包准备度：`Production-Frozen`（T0 已 Verified；精确 tick/schema/owner/filter/evidence 已回写，允许按 DAG 派发 T1～T3）
>
> 前置：LCC-M4 `Verified`。

## 1. 目标与完成体验

M5 补齐 M0～M4 未覆盖的中间层：作者明确选择的动画头部、尾部等附肢不成为硬移动墙、导航障碍或常驻伤害体，但与玩家权威胶囊接触时产生有界软压力和可见/可听反馈，避免“模型完全像空气一样被穿过”。

Dragon14 首期只覆盖：

- `hurt.head.obb.0`；
- `hurt.tail_mid.capsule.0`；
- `hurt.tail_tip.capsule.0`。

尾根继续由现有 ModelRoot Body Presence 覆盖；翼膜首期仍不参与被动软接触。普通接触不造成伤害，咬击、扫尾和尾旋仍只由 authored Active Trace 窗口造成命中。

## 2. 冻结架构方向

### 2.1 单一几何真源

M5 不复制头尾 shape，也不把 `body_part_contact_points.json` 的 PresentationOnly 音频球提升为 Gameplay authority。候选 Authoring 方向是在 Body Presence v2 中增加固定容量的 `animatedHurtboxSources[]`，每项只引用现有 `body_colliders.json` 的稳定 `colliderId`，并保存最小软接触策略：

- stable `sourceId`；
- `colliderId`；
- `skinMillimeters`；
- `pressureScaleMilli`；
- `enabled`。

shape、BodyPart、当前动画姿态、Severed 过滤和 world identity 继续来自同帧 `CharacterTestDamageGeometrySnapshot` / Hurtbox compound。T0 必须冻结字段、容量和兼容细节；T1 不得自行增加第二份动画几何表。

### 2.2 软接触而非硬阻挡

- `CharacterHurtboxLayer` 不加入 `MotionObstacleLayerMask`、导航、投射物 blocker 或静态障碍 mask。
- 玩家 `ColliderPosture` capsule 对已授权的动画 Hurtbox 叶子执行现有 fixed-point `QueryCapsuleSeparations`。
- 接触只复用 M1 的质量、PushScale、`CanBePushed`、三轮 pressure、墙体优先和 profile-derived clamp；不直接 `SetTransform`，不调用无障碍位移路径。
- 动画表面速度从同一 authority frame 的 previous/current 叶子姿态派生；spawn、restore、teleport、source revision 变化或 witness 缺失时 fail closed，不读取 Unity Transform 补算。
- 约束相位可派生 Started/Stayed/Ended 供 Audio/VFX/轻表现消费，但 presentation 不回写 Combat。

### 2.3 状态与兼容

- 尾部 Severed 后 Hurtbox 叶子已由 M3 移除，动画软接触必须随同一 topology 自动失效，不建立第二份断尾列表。
- Body Presence v1、geometry binding v1～v6、缺失 `animatedHurtboxSources` 的角色保持现有字节、运行和能力语义。
- M5 首选纯派生几何与既有 contact velocity/state，不预占网络 schema。T0 若证明 previous/current 动画 surface witness 会影响未来命令且不能从现有 formal state 重建，必须停止并登记下一可用 schema 34 候选，禁止私有跨帧 cache。
- 当前三个 Dragon14 PresentationOnly ContactPoint 可继续用于音频/调试，但不作为 M5 参与资格或 authority 证据。

## 3. 工作包与 DAG

```text
M5-T0 -> M5-T1 -> M5-T2 -> M5-T3
```

| 工作包 | 状态 | 唯一结果 | 前置 |
| --- | --- | --- | --- |
| [LCC-M5-T0](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_T0_RUNTIME_CONTRACT_AUDIT.md) | `Verified` | 冻结 tick/pose/topology/separation/pressure 当前链、字段容量、schema 决策、文件 owner、测试与证据名 | M4 Verified、M5 Audit-Frozen |
| [LCC-M5-T1](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_T1_AUTHORING_RUNTIME_BINDING.md) | `Verified` | Body Presence v2 animated Hurtbox 引用、校验/编译/Runtime binding、CharacterStudio、Dragon14 三引用与正式生成；v7 Network/M1 consumer兼容已闭合 | T0 Verified、M5 Production-Frozen；T3回派已关闭 |
| [LCC-M5-T2](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_T2_SOFT_CONTACT_CONSTRAINTS.md) | `Verified` | 同帧动画叶子 separation、surface velocity、稳定软压力、接触相位和 Severed 失效 | T1 Verified |
| [LCC-M5-T3](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_T3_ACCEPTANCE.md) | `Verified` | Dragon14 Playable、回滚/重演、性能/0 B、Game View、接口/能力表和唯一最终审查 | T2 Verified |

T0 只读审计完成后，主代理把精确文件/partial owner、focused filter 和 Evidence 名回写 T1～T3，再把本里程碑从 `Audit-Frozen` 提升为 `Production-Frozen`。不得跳过 T0 直接实现。

## 4. 统一停止条件

出现以下任一情况立即停止当前生产任务并回报：

- 必须把整个 Hurtbox layer 加入运动障碍才能实现；
- 必须读取 Unity Collider、Animator 或骨骼 Transform 作为 authority；
- 需要复制 Hurtbox shape/动画轨形成第二 Authoring 真源；
- 软压力需要无界位移、未量化常量或绕过静态 sweep；
- previous/current witness 无法在 rollback/restore 后确定性重建；
- 现有 fixed-point separation 不支持真实 Dragon14 head/tail shape；
- 8 Dragon 固定容量或 steady-state 0 B 无法满足且需要扩大为通用物理重构。

## 5. M5 完成 Gate

- T0～T3 全部 `Verified`，仅 T3 执行一次不同实现上下文的最终 P0/P1 只读审查。
- Dragon14 头、尾中、尾尖普通接触产生有界软推出/滑开和接触反馈，不成为硬墙、不造成伤害。
- 墙边、角落、同中心、高速摆动、动作切换、spawn/restore/rollback 均无穿墙、teleport、无界速度或顺序依赖。
- bite/tail sweep/tail spin 伤害窗口、Hurtbox 命中、CD-M0～M4、LCC-M0～M4、pathfinding 和 projectile 语义无回归。
- Severed tail 在同一 topology 生效边界后不再产生尾部软接触；head 不受影响。
- 8 Dragon 全链满足 T0 校准预算且 warm steady-state 0 B；Unity refresh/compile/domain reload、Editor ready、Console 0 和正式 Game View 证据闭合。
- `Docs/CAPABILITIES.md` 与 CharacterApplication/Combat/Network/Audio 接口按实际支持等级回写，不再把 M0～M4 `Verified` 描述为已覆盖动画附肢被动接触。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 工作包进度 | 4/4 Verified；M5 Gate闭合 |
| 执行包准备度 | `Production-Frozen` |
| 实际源码 / 资产 | T1落Body Presence v2、geometry binding v7、RuntimeSpawn/Network/M1 consumer兼容、CharacterStudio和Dragon14三引用；T2落双Sync后selected-leaf same-tick witness、256约束、M1 pressure与phase；T3落正式PlayMode/network/rollback/full RuntimeModule性能与文档证据 |
| 自动化 / Unity / Playable | M5+M4 PlayMode 5/5、M5 focused/network/performance 13/13、M1/M3/M4窄回归39/39；正式三部位下一tick位移75/41/74 mm，moving/turning/high-speed/wall-corner/Severed直接断言通过；最终Unity gate完整且Console0 |
| 性能 / 网络 | 正式v7 8 Dragon+1 player、3 source/Dragon、RuntimeModule warm300/measure1800，whole tick p50/p95/max=`2125.7/3010.4/3964.0 us`、0 B；formal rollback+1/hash parity通过 |
| 独立审查 | 唯一终审最终P0=0/P1=0 open、P2=1、P3=2；两项历史P1由同一审查上下文关闭，不追加宽审 |
| 当前限制 | phase Source/Target身份语义P2须在接Audio/VFX前修正；Studio全smoke被非M5 fixture阻断；无production appendage Audio/VFX/统一overlay；翼膜/全身/硬附肢墙/ragdoll不支持 |

## 7. 状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-26 | M5-T0 / M5 | `Planned` / `Planned` | `In Progress` / `In Progress` | 用户明确要求按文档开始实现；依据 Audit-Frozen 门禁只派发 T0 只读审计 | T1～T3 继续锁定，无生产实现或运行证据 |
| 2026-08-26 | M5-T0 / M5 / M5-T1 | `In Progress` / `Audit-Frozen` / `Planned` | `Verified` / `Production-Frozen` / `In Progress` | T0冻结双Sync、same-tick witness/no-schema34、v2/v7、容量、Severed边界及精确owner/filter；主代理复核后串行解锁T1 | 当前只有审计证据；T2/T3继续等待，尚无Gameplay软接触 |
| 2026-08-26 | M5-T1 / M5-T2 | `In Progress` / `Planned` | `Verified` / `In Progress` | T1完成Body Presence v2、geometry v7、RuntimeSpawn兼容、Studio编辑、Dragon14三引用、正式生成；Authoring通过、Unity 3/3、脚本门禁0 error、双compile hash一致，主代理复核后解锁T2 | Studio全smoke保留既有numeric ActionSet fixture TypeError；T2前仍无Gameplay软接触 |
| 2026-08-26 | M5-T2 / M5-T3 | `In Progress` / `Planned` | `Verified` / `In Progress` | T2完成same-tick叶子witness、最多256约束、M1三轮pressure和presentation phases；focused 8/8、M1/M3回归19/19、256约束0 B、Unity门禁0 error，主代理复核后解锁T3 | M4 consumer唯一陈旧ContactPoint=0断言交T3精确迁移；Playable/全网络/阶段耗时/最终审查待T3 |
| 2026-08-26 | M5-T3 / M5-T1 | `In Progress` / `Verified` | `In Progress` / `In Progress` | T3正式PlayMode修复后head/tail-mid/tail-tip均产生有界推出，8×3+player性能0 B；全network/replay回归发现v7仍被NetworkSessionFactory拒绝，主代理追加发现M1 BodyPresence consumer也只接受v4～v6，回派T1原owner | T3暂停；不能在v7禁用root Presence或网络身份失败时晋升 |
| 2026-08-26 | M5-T1 / M5-T3 | `In Progress` / `In Progress` | `Verified` / `In Progress` | T1关闭v7 Network/root Presence consumer兼容，focused30/30；T3复验正式PlayMode、network rollback3/3、性能0B、Unity门禁后形成候选 | 唯一终审P0=0/P1=2：GameView无效且缺多帧体验证据、性能非正式双Sync/full-chain；T3继续回修，不晋升 |
| 2026-08-26 | M5-T3 / M5 | `In Progress` / `In Progress` | `Verified` / `Verified` | T3补真实view与多帧滑开、安全/Severed矩阵和正式RuntimeModule full-chain 300+1800；最终5/5、13/13、39/39、0 B、Console0；唯一终审同上下文关闭两项P1，最终P0/P1=0 | 接受phase身份P2、Studio smoke与无production Audio/VFX P3、dense tests-only visual及非M5陈旧基线；不追加审查 |
| 2026-08-26 | M5 post-Verified facing fix | 转向语义泄漏 | `Verified` | solver改读原始Facing snapshot，CharacterTest adapter再将Facing fallback从会被外力污染的HorizontalVelocity切到DesiredVelocity；通用solver 10/10、正式Dragon14连续多tick PlayMode 2/2、Unity编译后Console0 | 外部接触速度只平移，当前/后续tick均不改变玩家/AI authority yaw；显式turn/locked target/主动输入保持 |
