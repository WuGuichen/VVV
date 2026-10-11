# LCC-M1：稳定身体 Presence 与软接触

> Status: Guide
>
> 里程碑：LCC-M1
>
> 里程碑状态权威：[LCC 实施总台账第 6 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 5 节
>
> 当前状态：`Verified`
>
> 执行包准备度：`Production-Frozen`（T0～T5 全部 Verified；里程碑已收口）

## 1. 目标与玩家结果

M1 消费 M0 的正式 Body Presence binding 和 separation API，为大型角色建立连续、确定性、受静态世界约束的身体软接触。

Dragon14 的目标体验是：

- 玩家靠近胸腹、骨盆或肩颈根部时感受到稳定体积和滑开压力；
- Dragon14 平移或原地转身时，核心身体可以推动接触角色；
- 玩家靠墙时不会被软压力推入墙体；
- 翼膜、尾尖和头尖在非攻击状态下仍不阻挡移动；
- 咬击、扫尾和尾旋继续只由现有 authored action window 产生伤害；
- 现有根距离、DamageZone、Engagement Anchor、Attack Opportunity 和导航语义不变。

## 2. Runtime 拓扑

### 2.1 独立 layer 与 compound

在 `CharacterTestCharacterPhysicsRuntime` 增加项目内部 Body Presence layer 和独立 body-id 范围。计划基线候选 layer 为 11，实际 T0 必须检查全仓 layer 占用后冻结。

每个具有启用 Presence binding 的角色：

- 使用一个独立 compound body；
- 与 Hard Root 使用相同 `CombatEntityId` 语义，但使用不同 `CombatBodyId`；
- collider id 从 volume 的稳定排序索引或稳定映射得到，重载/rollback 不因集合顺序改变；
- 每 tick 只用 authority root position/Yaw 和编译后的 ModelRoot local pose 生成世界几何；
- binding/revision、根位置、Yaw 和 enable state 均未变化时不 refit、不递增无关 world revision。

Presence layer 必须排除在：

- `MotionObstacleLayerMask`；
- Dynamic Character hard obstacle mask；
- CharacterHurtbox damage query mask；
- navigation blocking 和 path graph build；
- projectile/environment blocker mask。

### 2.2 tick 顺序

计划顺序：

```text
capture previous root pose for this tick
  -> existing kinematic motion / Hard Root collision
  -> sync current Hard Root and Body Presence derived geometry
  -> existing hard-root overlap compatibility handling
  -> build root-contact + presence-contact constraints
  -> solve pressure velocities in one stable iteration order
  -> write CharacterTest contact state
  -> existing animated Hurtbox SyncAll
```

若实际源码顺序不允许一次同步满足 motor candidate cache 和后处理查询，T0 必须冻结最少 revision 的等价顺序；不得让 Presence 读取下一帧或 Unity View transform。

## 3. 接触解算合同

### 3.1 约束输入

每条 Presence 约束至少包含：

- source/target entity identity；
- source presence body/collider/volume identity；
- target ColliderPosture capsule 的 authority shape；
- separation normal、penetration、contact point；
- source/target 有效质量、push scale、`CanBePushed`；
- source 表面速度、target 当前 pressure velocity；
- stable sort key。

同一 source/target/volume 在同一 tick 只形成一条规范约束；compound query 返回重复候选时去重，不重复累计压力。

### 3.2 表面速度

source 表面速度必须由本 tick previous/current authority root pose 派生：

```text
surfaceVelocity = rootLinearVelocity + yawAngularVelocity × contactOffsetFromRoot
```

- previous/current pose 在当前 motion tick 内传递，不建立未持久化的跨帧私有 cache。
- Yaw 差使用现有量化角度和 deterministic math；跨 ±180° 使用冻结的最短有向差规则。
- 第一帧、spawn、restore 或 source binding 切换没有合法 previous pose 时，旋转表面速度为 0，平移仍按当前 motion 事实处理；不得读取未来姿态补算。
- 角速度和接触速度必须有明确上限，防止远离根部的 volume 因高转速产生无界推力。

### 3.3 压力而非直接位移

Presence 只向现有 contact pressure velocity accumulator 提交速度变化：

- 使用现有有效质量、push scale、`CanBePushed` 和 contact count 语义；
- 约束按稳定 key 排序，固定次数迭代；
- 浅穿透和接近表面可用 skin/slop 形成压力；深穿透只提高有上限的 correction velocity；
- 结果写入 `CharacterTestCharacterPhysicsComponent.ContactVelocity*`，由下一次 kinematic motor 经过静态 obstacle sweep 后移动；
- 不调用当前直接修改 transform 的 `ApplyContactDisplacement`；
- 不把每 tick Presence 接触包装为通用 Interaction event，避免事件历史膨胀和错误的根中心方向。

当墙体阻止目标沿压力方向移动时，允许 Presence 保留有限重叠并持续产生有界压力；静态障碍永远优先，不允许把目标 teleport 到另一侧。

### 3.4 对称性与推动规则

- 普通角色主动走向静止大型角色：其速度沿法线的分量被软约束削减/转移，产生滑开效果。
- 大型角色移动或转身接触静止角色：表面速度按质量比例传递给目标。
- 双方都可推动时使用现有有效质量比例；`CanBePushed=false` 的目标不被 Presence 移动，但仍记录接触诊断。
- Presence 不代替 Hard Root。两个角色核心重合仍由现有硬阻挡/兼容 overlap 处理。
- 死亡、无碰撞姿态、spawn/despawn、未配置 Presence、禁用 volume 和生命周期不可接触状态必须明确跳过。

## 4. 状态、网络与诊断

### 4.1 权威状态边界

M1 默认不新增 formal network schema：

- Presence definition、revision/content hash 来自已编译 package source identity；
- 世界 volume 由当前 root transform/Yaw 和 binding 纯派生；
- previous/current root pose 来自同一 motion tick，不跨 tick 私存；
- contact velocity/count 已按现有 CharacterTest physics snapshot/hash 合同处理。

若实现发现必须保存 hysteresis、last-contact normal、previous pose 或其它影响未来命令的跨帧数据，立即停止并把它提升为正式 authority state、使用下一可用 schema；不得以“缓存”名义隐藏。

### 4.2 诊断

每帧调试摘要至少显示：

- active presence entity/volume 数；
- broadphase candidates、deduped constraints、solver iterations；
- source/target、volume id、normal、penetration、surface velocity；
- applied contact velocity、clamp reason、immovable/wall-limited 状态；
- missing/unsupported/stale binding 计数；
- compound refit 与 skipped-unchanged 计数。

Game View overlay 同时显示 Hard Root、Body Presence、Hurtbox 和当前 Active Trace，并允许分别开关；这些诊断不得进入 formal network snapshot。

## 5. 工作包状态与实施顺序

| 工作包 | 状态 | 交付物 | 前置 |
| --- | --- | --- | --- |
| [LCC-M1-T0](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_T0_RUNTIME_AUDIT.md) | `Verified` | 重审 motion tick、candidate cache、contact resolver、physics layer/body-id、snapshot/hash 和 Dragon14 fixture；冻结实际顺序与预算 | M0 Verified |
| [LCC-M1-T1](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_T1_PRESENCE_RUNTIME.md) | `Verified` | Presence layer/body/compound runtime、dirty gate、稳定 collider identity、spawn/despawn/restore rebuild | T0 Verified |
| [LCC-M1-T2](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_T2_CONTACT_CONSTRAINTS.md) | `Verified` | capsule-vs-presence candidate/separation、去重、稳定 constraint 集合和共享 pressure solver 接入 | T1 Verified |
| [LCC-M1-T3](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_T3_SURFACE_VELOCITY_SAFETY.md) | `Verified` | previous/current root pose、Yaw 表面速度、质量/推力传递、clamp、墙边安全和无直接位移门禁 | T2 Verified |
| [LCC-M1-T4](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_T4_AUTHORITY_DIAGNOSTICS.md) | `Verified` | snapshot/hash/rollback/replay parity、diagnostics、overlay、NoAlloc 和顺序扰动 | T3 Verified |
| [LCC-M1-T5](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_T5_ACCEPTANCE.md) | `Verified` | Dragon14 正式 Playable、受影响回归、性能基线、接口/能力表与唯一终审闭合 | T4 Verified |

主代理是本表唯一写入者，子代理不得修改父状态。全路线 `Audit-Frozen` 且 M0 `Verified` 后才派发 T0；T0 `Verified` 并提交结构化回报后，由主代理完成生产任务的文件级回写并把 M1 提升为 `Production-Frozen`，再按前置逐项解除 T1+，不并行写共享 Runtime 文件。

## 6. 预计影响面

实际 T0 重新登记精确文件；候选包括：

- `CharacterTestCharacterPhysicsRuntime.cs`
- `CharacterTestCharacterMotionSystem.cs`
- `CharacterTestCharacterContactResolver.cs`
- 新增 `CharacterTestCharacterBodyPresenceSystem.cs` 或同职责内部模块
- `CombatPhysicsWorld.Compounds.cs` / M0 separation API 消费入口
- CharacterTest component/snapshot/hash/diagnostics/overlay
- Dragon14 generated package binding 与 PlayMode fixture
- CharacterTest motion、physics、network、Attack Opportunity、Dragon14 natural attack 测试

不得把 CharacterTest 项目策略下沉成带 Monster 业务词的 Combat 公共系统。

## 7. 验证矩阵

| 类别 | 必须覆盖 |
| --- | --- |
| 几何 | sphere/capsule/OBB presence；target capsule 相离、相切、浅/深穿透、完全重合、多个 volume 重叠去重 |
| 运动 | 玩家主动接近、大型角色平移、原地转身、双方相向、不同质量/推力、不可推动、死亡/禁用姿态 |
| 安全 | 墙边夹压、凹角、窄门、台阶边缘、障碍阻止压力、无 teleport、速度/penetration 上限 |
| 边界 | 翼膜/尾尖/头尖不阻挡；Hard Root 仍硬阻挡；Presence 不出现在 damage/navigation/projectile blocker query |
| 确定性 | 实体/volume/broadphase 顺序扰动、spawn/restore 第一帧、Yaw wrap、rollback 后下一 tick 位置/速度/命令/hash 一致 |
| 回归 | CD-M0～M4、现有 motion/contact、Dragon14 bite/tail/spin、projectile、pathfinding |
| 性能 | 8 角色，Dragon14 18 Hurtbox + ≤4 Presence；完整 sync/contact P95 预算在 T0 校准，steady-state 0 B GC |
| 直接证据 | Dragon14 胸腹滑开、转身推人、墙边不嵌入、静止翼尾无阻挡、动作窗口仍命中的 Game View/PlayMode 画面 |

## 8. M1 完成 Gate

- T0～T5 全部 `Verified`，实际交付记录完整。
- Presence layer/compound 不进入任何硬阻挡、伤害、导航或 projectile blocker mask。
- 软接触只通过有界 contact velocity 进入 kinematic motor；代码审查确认 Presence 路径不调用直接 transform displacement。
- 平移和 Yaw 表面速度均可确定性推动，墙体优先且没有嵌墙、穿墙、无界速度或顺序依赖。
- spawn/restore/rollback 后派生 compound、contact velocity、下一 tick command 和 state hash 一致；若新增状态，schema 全链闭合。
- Dragon14 直接 Playable 与四层 overlay 证据齐全；现有 bite/tail/spin PlayMode 回归通过。
- 完整 sync/contact 性能满足校准预算，预热后 0 B GC；独立终审 P0/P1 为 0。
- 按实际实现回写 Combat/Network/Character Resource Package 接口和 `Docs/CAPABILITIES.md`；只有此 Gate 完成后才能声明 Body Presence Runtime Slice/Playable。

## 9. 实际交付记录

| 项目 | 当前记录 |
| --- | --- |
| 工作包进度 | 6/6 `Verified`；M1 Gate 已闭合，M2-T0 可按 Audit-Frozen 边界派发。 |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区；M1 尚无生产提交 |
| 实际文件 | T0 冻结表；T1 Presence runtime/physics/motion partial；T2 resolver/post-motor partial（含 T5 捕获的 `BlocksCharacters` eligibility 窄修）；T3 same-tick pose/surface velocity/clamp；T4 现有 `ResourceCatalogHash` package identity/replay 校验、只读数值诊断/四层 overlay 与 Authority tests；T5 新增聚合 PlayMode、正式 Game View 证据与能力/接口候选回写。 |
| focused / 回归测试 | T1 6/6；T2 回修后 PresenceContact 7/7、MotionSafety 6/6；T3 6/6；T5 PlayMode 3/3、Dragon14 BakedSocket 1/1、Authority 8/8、Replay/Debug 4/4，均 0 failed/skipped。 |
| Unity 验证 | T1～T4 validator 均完整通过；T2 回修最终 job `0e71867fd4d642a9a7ec4b280fe58b0f`、T5 最终 job `5c3cd4c3549a40bc88c8a449342e4b7f` 均完成 refresh/compile/domain reload/Editor ready，Console 0 error。 |
| snapshot / rollback | contact velocity 已实证 capture/apply、full/owned/non-owned hash、rollback 恢复与下一 tick parity；未改 snapshot schema/wire；package source mismatch 在 session/replay bootstrap 前失败关闭 |
| Playable / framebuffer | 正式 CharacterTestScene 生成 Dragon14；2560×1440 标准 Game View `Docs/Evidence/LCC/LCC-M1-T5/lcc-m1-t5-multitarget-000003-characters-panel.png` 同框显示 Dragon14、Characters overlay、四层全 on、Presence Sync/Contact Build。场景退出后 clean、Console 0。 |
| 性能 / NoAlloc | 当前代码完整 8角色×4 Presence×18 Hurtbox、4096 帧为 0 B，median 1.12 ms、P95 1.687 ms、max 25.1 ms；低于 16.667 ms 门槛，overflow/failure=0/0。 |
| 接口 / 能力表 | Combat、Network、Character Application 与 `Docs/CAPABILITIES.md` 已按实际 Runtime Slice/Playable 边界接受。 |
| 限制 / 阻断 | 彩色几何 Gizmo 未进入标准 Game View capture，且没有持续人工输入视频/非零 contact 截图；四层开关/数值 overlay、formal package 与动态 PlayMode/Gizmo projection 自动化共同闭合行为证据。唯一终审 P0/P1=0；不追加编码或评审轮次。 |

## 10. 状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-24 | T0～T5 | 初始化 | `Planned` | 草拟 Presence runtime、pressure-only 解算、Yaw 表面速度、网络边界和验收矩阵 | 执行包仍待审查；等待 M0 Verified，无 Runtime 实现 |
| 2026-08-24 | T0～T5 | `Drafting` | `Audit-Frozen` | 6/6 执行文档经结构、DAG、写入边界和验证合同独立审查，P0/P1 为 0 | 等待 M0 `Verified` 后才可派 T0；无 Runtime 实现 |
| 2026-08-24 | T0 | `Planned` | `In Progress` | M0 已 6/6 Verified；按两级门禁派发 Runtime 接触链、identity/capacity、snapshot/rollback 与性能预算只读审计 | 只允许写 T0 自身冻结表；T1～T5 继续等待 `Production-Frozen`，无 Runtime 行为 |
| 2026-08-24 | T0 | `In Progress` | `Verified` | 冻结真实 motion/contact/Hurtbox 顺序、root-only direct displacement、layer/identity/capacity、same-tick pose/no-schema、snapshot/hash 与 T1～T5 partial owner；主代理补严 range stop-check 和 overlay owner | 只读审计，无 Unity/测试/Runtime 行为；完整 P95 留给 T4 实测 |
| 2026-08-24 | T1 | `Planned` | `In Progress` | M1 进入 `Production-Frozen`，按 DAG 派发 Presence body/compound、dirty/refit 与 lifecycle 实现 | T1 必须先证明 body/collider identity 不冲突；不得接 contact pressure 或改 Gameplay |
| 2026-08-24 | T1 | `In Progress` | `Verified` | layer 11 派生 compound、1..29999 checked identity、body-local 1..4 ordinal、8×4 固定容量、dirty skip、spawn/despawn/restore 与整子步 fail closed 落地；主代理 6/6、4096 warmed 0 B、最终脚本验证 Console 0 | 仍无 Presence constraint/pressure；PlayMode、完整 P95 与 M1 终审留给 T4/T5 |
| 2026-08-24 | T2 | `Planned` | `In Progress` | T1 Gate 解除后按冻结 owner 派发 candidate/separation、规范 key/去重与共享 pressure solver 接入 | 不得实现 T3 surface velocity、改 direct displacement、snapshot 或 Debug |
| 2026-08-24 | T2 | `In Progress` | `Verified` | 28 raw/28 unordered pair/224 directed constraint 有界构建、稳定排序/去重、typed fail-closed 与既有三轮 pressure 接入完成；主代理回查 focused 6/6、128 帧 0 B 和 Console 0 | 只证明 pressure plumbing；实际 root/Yaw 表面速度、clamp、墙边安全和 Playable 留给 T3～T5 |
| 2026-08-24 | T3 | `Planned` | `In Progress` | T2 Gate 解除，顺序派发 same-tick pose、actual surface velocity、现有 profile 派生 clamp 与下一 tick motor safety | 不得新增通用物理抽象、猜测常量、跨 tick 私有状态、direct displacement 或 Network schema |
| 2026-08-24 | T3 | `In Progress` | `Verified` | actual root/Yaw surface velocity、binding ordinal pressureScale、profile-derived clamp、typed degeneracy 与 next-tick static wall sweep 落地；focused 6/6、8×4×224 warmed 128 帧 0 B、Console 0 | 未做逐任务独立评审；网络 parity、完整 P95 与角落/窄通道 Playable 留给 T4/T5 |
| 2026-08-24 | T4 | `Planned` | `In Progress` | T3 Gate 解除，按冻结 owner 派发现有 contact velocity snapshot/hash/rollback/replay 实证、numeric diagnostics/overlay 与完整 NoAlloc/P95 | 不新增 formal state/schema；T1～T3 runtime 只读，缺陷回派原 owner；本任务不另开独立评审 |
| 2026-08-24 | T4 | `In Progress` | `Verified` | 主代理复核 package identity/replay fail-closed、contact velocity 三类 hash/rollback、只读诊断/四层 overlay；Authority 8/8、Replay/Debug 4/4、完整 4096 帧 0 B/P95 与 validator Console 0 | 未跑 PlayMode/Game View/最终门槛；按冻结边界交 T5，未增加逐任务评审 |
| 2026-08-24 | T5 | `Planned` | `In Progress` | T4 Gate 解除，只派发 Dragon14 Playable、场景回归、Game View、性能门槛、能力表事实回写与一次里程碑终审 | 核心缺陷必须回到 T1～T4 owner；禁止借验收扩张 Runtime 抽象或循环评审 |
| 2026-08-24 | T5 / M1 | `In Progress` | `Verified` | 聚合 PlayMode 3/3、Dragon14 自然攻击 1/1、Authority 8/8、Replay/Debug 4/4；4096 帧 GC 0/P95 1.687 ms；正式 CharacterTestScene Dragon14 + 四层 overlay；唯一终审 P0/P1/P2/P3=0 | 保留无持续人工输入视频/非零 contact 截图及无彩色 Game View Gizmo 的证据限制；不影响已组合闭合的 PlayMode + formal scene Gate |
