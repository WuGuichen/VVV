# CharacterTest 角色距离与 Attack Opportunity 实施总台账

> Status: Guide
>
> 状态权威：本文第 4 节
>
> 建立日期：2026-08-18
>
> 父设计：[CharacterTest 角色距离计算方案](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md)
>
> 范围：CharacterTest 内部 Attack Opportunity、DamageZone、Engagement Anchor 与有限联合目标选择
> 当前状态仅见本文第 4 节；本文和子文档不因记录计划而证明能力已经实现。

## 1. 文档分层与加载规则

这组文档按设计、总台账和单阶段执行页分层：

| 层级 | 权威内容 | 使用方式 |
| --- | --- | --- |
| [父设计](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) | As-Is、设计理念、术语和最终合同 | 设计评审或合同变更时读取；实施时只读当前里程碑链接的相关章节。 |
| 本总台账 | 公共首版合同、依赖关系、唯一状态表、跨阶段验收和子文档路由 | 每次实施先读；不在这里展开五个阶段的完整任务。 |
| CD-M0 至 CD-M4 子文档 | 单个里程碑的目标、任务、兼容边界、测试、Playable 证据和实际交付记录 | 只加载当前正在实施或评审的一个里程碑；CD-M2/CD-M3 加载当前 T0～T6，CD-M4 加载当前 T0～T5。 |

默认上下文包是“本总台账 + 当前里程碑总控页 + 当前一个工作包 + 工作包点名的父设计/前置实际交付/接口”。CD-M2/CD-M3 分别使用 T0～T6，CD-M4 使用 T0～T5；PF-M4 使用自己的 T0～T4。除非做跨阶段合同审计，不应一次加载全部里程碑或全部工作包。

维护规则：

- 本文第 4 节是里程碑状态的唯一事实源；子文档不复制当前状态。
- 实际提交、测试数字、Playable/CharacterStudio 证据和限制写入对应子文档；CD-M2 写入当前任务页，并由 CD-M2 总控页维护工作包状态。
- 父设计只在合同变化时更新；普通进度不回写父设计。
- 相关接口文档只在实现已成立时回写；接口同步是进入 `Verified` 的必要条件。
- 类型名、schema 名和任务清单不证明源码、资产或工具已经存在。

## 2. 已锁定的公共首版合同

### 2.1 实现边界与内部类型

首版保持 CharacterTest 内部实现，不新增框架级 `MxFramework.AI` 公共 API。计划中的内部合同名称固定为：

| 内部合同 | 职责 |
| --- | --- |
| `CharacterTestAttackEvaluationSnapshot` | 绑定一次逻辑不可变的攻击评估快照及全部 revision。 |
| `CharacterTestAttackOpportunityService` | 在指定快照和模式下执行无副作用攻击机会评估。 |
| `CharacterTestAttackOpportunityResult` | 返回机会等级、稳定选择、样本证据和失败原因。 |
| `CharacterTestAttackOpportunityStatus` | 区分查询有效性与是否命中。 |
| `CharacterTestAttackEvaluationMode` | 区分当前姿态、原地转向和候选姿态。 |

查询状态固定为：

| 状态 | 合同 |
| --- | --- |
| `Valid` | 查询完整执行；可以表达肯定命中或合法的否定结果。 |
| `Unavailable` | 必需快照、绑定或运行时数据不可用。 |
| `Unsupported` | 动作或模式没有 evaluator，不得以近似结果冒充支持。 |
| `StaleSnapshot` | tick、阶段或任一 revision 不一致，结果不得参与权威决策。 |

评估模式固定为：

| 模式 | 合同 |
| --- | --- |
| `CurrentPose` | 使用决策快照中的 source 位置和朝向立即起手。 |
| `TurnInPlace` | source 位置不变，只允许先完成量化原地转向。 |
| `CandidatePose` | 使用 CD-M3 Engagement Planner 提供的 Anchor 候选位置和朝向，并复用同一 Attack Opportunity evaluator；缺失/非法 Anchor 明确 fail closed。 |

### 2.2 兼容边界

- CD-M5 起 `target.distance` 为锁定 Hurtbox 三维表面净空，`target.rootDistance` 承接原根节点 XZ 语义；`target.inAttackRange` 由锁定 Hurtbox 的 PrimaryAttack CurrentPose opportunity 驱动。
- `RootPlanarDistance` 继续用于普通空间关系、软排序和诊断，不作为攻击机会的唯一硬排除。
- `PrimaryAttack` 的最终合法性由 action-level Attack Opportunity guard 控制。
- `CastAbilitySlot` 等特殊动作首版走显式 `CompatibilityLegacyRange`。
- Attack Opportunity 内部禁止在 `Unavailable`、`Unsupported` 或 `StaleSnapshot` 时静默回退。
- 首版 evaluator 支持 `WeaponTrace`、`ManualCapsule`、`CasterBody` 和 `BakedSocketCapsule`。
- 投射物、光束、范围和脚本技能没有专用 evaluator 时返回 `Unsupported`。
- 首版不实现精确 `AttackEnvelopeSeparation`。

### 2.3 作者文件与运行时真源

| 可选作者文件 | 内容 |
| --- | --- |
| `geometry/damage_zones.json` | 稳定 Zone、`hitZoneId` 映射、`Hittable`、`Aimable`、`Approachable` 和优先级。 |
| `geometry/engagement_anchors.json` | 稳定 Anchor、Zone 引用、ModelRoot-local 毫米位置、毫度朝向和容量。 |

两者都编译进现有 `geometry_binding.json`，不建立第二份运行时几何真源：

- 缺失可选文件不标脏，也不自动写入默认文件。
- 未知 schema 进入只读阻断状态，禁止以旧结构覆盖保存。
- DTO、严格校验、迁移、hash、Compiler 投影和 Runtime binding 必须形成一条可追踪链路。
- Unity Runtime 只消费编译结果。

### 2.4 确定性、权威状态与性能

- Planner 权威比较使用整数、定点数或明确量化值；浮点只作展示投影。
- 影响未来命令的 Zone lock、Anchor 预约或稳定选择状态必须进入 snapshot、hash、rollback 和 replay。
- 稳定 ID 是最后决胜依据；容器、broadphase 或 collider 返回顺序不得成为隐式规则。
- 所有逐帧确定性热路径必须在 warmed 条件下提供 0 B GC allocation 证据；formal network/rollback snapshot 的 capture/apply 沿用现有 `LowFreqAlloc` 合同，不得冒充逐帧热路径，也不以其低频分配否定热路径门禁。

### 2.4.1 Completed Frame 根距离缓存细化

2026-08-27 的基础细化将根距离从三段 `BeginFrame` 缓存改为 Completed Frame Geometry 双缓冲。CD-M5 在同一 Buffer 中继续冻结 source 姿态胶囊、Hurtbox shape 和 DamageZone/member；根 Pair 继续为 `target.rootDistance`，锁定 Hurtbox gap 按 source/Handle/generation memo。

### 2.5 CD-M1 当前 Runtime 合同

CD-M1 当前状态为 `Verified`。CharacterTest Runtime AI Planner 在 `PreSimulation` 只读取上一已完成模拟帧的 sealed CombatPhysics/Hurtbox authority，并独立构建 Preview Trace Plan；不得读取当前帧尚未产生的 actual plan。目标在一次评估内冻结，属于 static-target prediction，不预测目标未来运动。

- `PrimaryAttack` 的最终 action eligibility 使用 Attack Opportunity：`CurrentPose` 才可提交攻击；`TurnInPlace` 只提交量化 yaw 的零平移转向且本 tick 不攻击；`RequiresTranslation` 禁止攻击并继续接近；`Unavailable`、`StaleSnapshot`、`Unsupported` fail closed。
- CD-M1～M4 的历史实现没有反写旧事实；CD-M5 通过 Behavior Contract 7 显式完成事实迁移。`CastAbilitySlot`、Skill、Guard、Dodge 等特殊动作仍走各自动作合同；Attack Opportunity 内部禁止 fallback。
- Runtime AI Lab behavior overlay 只有明确 `BasicAttack` / `light_attack` 绑定进入同一 guard；非 PrimaryAttack action 不得误判为攻击。旧 direct-input wrapper 仅用于兼容性测试/旁路，不是 M1 生产提交路径。
- TurnInPlace 的 yaw/delta、active-move=false 语义进入 command、input、network envelope、snapshot、hash、rollback 与 replay；Motion 消费一次后清除 pending turn，保留其它输入。
- 首版 evaluator 复用 M0 sampler、root motion、socket/baked source 与四类 direct-hit preview；`CandidatePose` 仍为 `Unsupported`，不引入 M3 的合法 ApproachPose、Zone lock 或路径成本。
- Preview 的 damage eligibility 直接复用 actual damage resolver 的 `TryResolveCharacterActionDamage` 入口；缺失或非法 payload 失败关闭。M1 只保证 HitZone identity 与现有 `Default/body` fallback，不承诺 M2 前的 Zone 严格校验或实际伤害/硬直/冲量倍率同构。
- 诊断/evidence 绑定 source、target、frame、revision、action 与 sample identity；source pose/facing 使用独立单调 write revision，不再以 completed frame 代替。同帧 root、yaw、AuthorityPose/socket 变化会使旧 Plan 与 adapter witness 失效。
- formal network snapshot schema 24 保存 policy ledger，schema 25 增加 active HitTrace window、instance/window ID 与 hit-once state，schema 26 增加 source pose/facing revisions，schema 27 增加 CasterBody 跨 tick previous-center authority；旧 schema 显式归一化，当前状态参与 full/owned/non-owned hash、rollback 与 replay。
- 未知 world classification 或 authority mismatch 不得近似为有效机会；`Wall`/`Ground` 只有显式 authority 才成立，未知环境按 `WorldStatic` fail closed。

## 3. 状态模型

正常流转统一为：

```text
Planned -> In Progress -> Implementation Complete -> Verified
```

| 状态 | 定义 |
| --- | --- |
| `Planned` | 已记录合同和任务，不声明实现存在。 |
| `In Progress` | 已开始修改源码、schema、工具或测试，门禁尚未完成。 |
| `Implementation Complete` | 计划内实现已完成，但仍缺完整验收、证据、文档回写或交付记录。 |
| `Verified` | 实现、focused tests、Unity、需要的 Playable/Authoring、性能和接口回写全部完成。 |
| `Blocked` | 仅用于外部依赖未满足且无法继续；必须记录阻断项和解除条件。 |

`Blocked` 不是完成度；解除后回到阻断前状态。每次状态变化必须同时：

- 更新第 4 节的唯一状态表。
- 在对应子文档填写实际分支、提交、受影响文件、测试结果、验证环境、证据和限制。
- 在第 7 节追加状态历史。
- 若进入 `Verified`，确认所有门禁和接口文档回写完成。

## 4. 里程碑状态、依赖与路由

```text
CD-M0 -> CD-M1 -> CD-M2 -> CD-M3 -> CD-M4
                                  ^
                                  |
      Pathfinding Foundation M2-M4 验收门禁
```

| 里程碑 | 唯一状态 | 执行文档 | 阶段边界 | 主要前置 |
| --- | --- | --- | --- | --- |
| CD-M0 | `Verified` | [快照、伤害几何与 Trace Plan 基础](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M0_FOUNDATION.md) | 建立共用确定性基础，不改变 Planner | 无 |
| CD-M1 | `Verified` | [静态目标 Attack Opportunity 与 PrimaryAttack 切换](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M1_ATTACK_OPPORTUNITY.md) | PreviewTrace 与 PrimaryAttack action guard | CD-M0 |
| CD-M2 | `Verified` | [DamageZone、稳定选择与 CharacterStudio 闭环](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) | Zone 作者化、Runtime 聚合和稳定锁定 | CD-M1 |
| CD-M3 | `Verified` | [Engagement Anchor、CandidatePose 与路径代价](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md) | 合法攻击姿态、容量/预约与路径成本 | CD-M2；Pathfinding Foundation M2-M4 |
| CD-M4 | `Verified` | [有限 `(target, action, zone)` 联合选择](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md) | 有预算、可诊断的确定性联合归约 | CD-M3 |
| CD-M5 | `In Progress` | [角色 + Hurtbox 复合目标与精确净空](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M5_COMPOSITE_HURTBOX_TARGET.md) | 生产实现与 focused gate 完成；等待 Projectile/Chain 当前 ActionSet 基线恢复后的直接复验 | CD-M4；Completed Frame Geometry |

相邻阶段可以提前做只读调查，但后续里程碑不得绕过前置合同进入集成验收。CD-M3 开工时若 Pathfinding Foundation M2-M4 未验收，应把 CD-M3 改为 `Blocked` 并记录实际缺口。

当前路线复核（2026-08-27）：CD-M0～CD-M4 与 Pathfinding Foundation PF-M2～PF-M4 已 `Verified`；CD-M5 生产实现与 focused gate 完成，完整 Projectile/Chain action-path 复验仍受当前 CharacterAction fixture 基线阻断。

## 5. 跨里程碑验收矩阵

| 里程碑 | Focused tests | Unity 验证 | Authoring 验证 | Playable 证据 | Authority / 性能 |
| --- | --- | --- | --- | --- | --- |
| CD-M0 | Trace Plan 等价、stale、粗筛 | 必须 | 不适用 | 非强制 | revision；warmed 0 B GC |
| CD-M1 | PreviewTrace、PrimaryAttack、兼容路径 | 必须 | 不适用 | 必须 | fail closed；warmed 0 B GC |
| CD-M2 | schema、Zone 稳定性、失效 | 必须 | Core/CLI、迁移、CharacterStudio | 必须 | lock snapshot/hash/rollback/replay；逐帧 aggregate/selector warmed 0 B GC；snapshot capture/apply 为 `LowFreqAlloc` |
| CD-M3 | Anchor、CandidatePose、路径与容量 | 必须 | Core/CLI、迁移、CharacterStudio | 必须 | reservation snapshot/hash/rollback/replay；warmed 0 B GC |
| CD-M4 | 联合选择、预算、缓存、顺序扰动 | 必须 | 按实际 schema 影响 | 必须 | 预算校准；warmed 0 B GC |

统一验收规则：

- 每个 C# 里程碑执行 Unity refresh/compile、等待 Editor ready，并确认 Console error count 为 0。
- focused tests 记录测试入口、通过数量、失败数量和执行日期。
- CD-M2、CD-M3 同时运行 Authoring Core/CLI、schema round-trip、迁移和实际编辑状态检查。
- CD-M1 至 CD-M4 保存直接 Game View/Play Mode 证据；可以使用确定性合成多 Hurtbox 大型目标。headless Unity PlayMode job 可证明运行时行为，但没有 framebuffer 时不得宣称画面或美术验收。
- 权威状态变更覆盖 snapshot、hash、rollback 和 replay。
- 逐帧热路径必须在 warmed 条件下验证 0 B GC；设计预期不算性能证据。明确标注的 network/rollback snapshot capture/apply 属低频边界，按 `LowFreqAlloc` 记录和复证，不纳入逐帧 0 B 计量窗口。

## 6. 接口文档回写

里程碑完成时按实际影响同步：

- [AI 接口](../Interfaces/AI.md)：Planner fact、guard、联合选择、锁定、预算和诊断。
- [Combat 接口](../Interfaces/Combat.md)：CombatPhysics revision、Hurtbox/Zone 聚合、trace 复用和 blocker 语义。
- [CharacterAction 接口](../Interfaces/CharacterAction.md)：resolved action revision、direct-hit sampler、evaluator 支持矩阵和兼容通道。
- [CharacterApplication 接口](../Interfaces/CharacterApplication.md)：作者文件、Compiler 投影、CharacterStudio 和 Runtime binding。
- [Network 接口](../Interfaces/Network.md)：影响命中权威的 snapshot schema、hash、rollback/replay 与旧版本归一化。
- CD-M3 另同步 [Pathfinding Foundation](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md) 中实际复用的静态 graph、有界 A*、容量和 Planner 接入。

未实现前只在总台账和对应子文档保留计划，不得提前写成 Current。

## 7. 状态历史

| 日期 | 里程碑 | 原状态 | 新状态 | 分支 / 提交 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- | --- |
| 2026-08-18 | 全部 | 初始化 | `Planned` | 文档计划，无实现提交 | 已完成任务拆分；未执行代码、测试或 Playable 验证 | 无已登记阻断 |
| 2026-08-18 | CD-M0 | `Planned` | `In Progress` | 当前工作区，尚未提交 | 已开始快照、伤害几何、Trace Plan 与 focused tests 的源码实施；尚未完成 Unity 与性能门禁 | 不改变 Planner；后续里程碑仍保持 `Planned` |
| 2026-08-18 | CD-M0 | `In Progress` | `Implementation Complete` | `main` 工作区，尚未提交 | M0 focused tests 20/20、相关实际执行回归 9/9；Unity refresh/compile、domain reload、Editor ready、Console 0 error 与 warmed 0 B GC 通过 | 仓库缺少 CharacterTest 生成动作与一项 Baked Socket fixture，CasterBody/BakedSocket 的完整实际链路等价验收待补；M1-M4 仍为 `Planned` |
| 2026-08-18 | CD-M0 | `Implementation Complete` | `Verified` | `main` 工作区，尚未提交 | 当前合并工作区 M0 + CasterBody EditMode 25/25、BakedSocketCapsule PlayMode 1/1；Unity 完整脚本验证与 Console 0 error；warmed 0 B GC、接口文档和四类实际执行/预演证据闭合 | 无 M0 验收阻断；动作目录内容 hash 不属于本阶段运行时计划身份合同 |
| 2026-08-18 | CD-M1 | `Planned` | `In Progress` | `main` 工作区，尚未提交 | Attack Opportunity 核心、冻结 sample policy、逐 tick PreviewTrace 和 21 项 focused tests 已落地；正在独立审查并接入 Runtime/Planner | PrimaryAttack guard、显式 TurnInPlace、特殊动作兼容、Playable 与接口回写尚未完成 |
| 2026-08-18 | CD-M1 | `In Progress` | `Implementation Complete` | `main` 工作区，尚未提交 | M0+M1 core 58/58（`228fccd759b147ce8e8778dda300708d`）；authority 6/6（`8de9ed2000344f37aca059a2024ff2e4`）；M1 integration 13/13（`93a4e7a152b5409782dc46905a10601d`）；PlayMode 4/4（`16b5941980d8499d891439383fca0381`）；旧 Planner command 2/2（`48871e104ca74deda799a2ba3bb14926`） | 最终 validate 与 full-chain NoAlloc 仍待 root；static-target、CandidatePose Unsupported；M2 Planned、M3 Blocked、M4 Planned |
| 2026-08-18 | CD-M1 | `Implementation Complete` | `In Progress` | `main` 工作区，尚未提交 | root 合并复证 EditMode 104/104、PlayMode 6/6、BakedSocket 1/1 与 Unity 完整验证通过；随后独立终审发现 CasterBody previous-center authority 未进入 formal network snapshot/hash，真实 target/direct-hit rollback parity 未覆盖 | 解除条件：纳入并验证 CasterBody sweep state；再次执行合并 focused、Playable、NoAlloc、Unity 与文档门禁 |
| 2026-08-18 | CD-M1 | `In Progress` | `Verified` | `main` 工作区，尚未提交 | schema 27 纳入 CasterBody previous-center authority；独立终审 P0/P1 为 0；EditMode 107/107（`bf5550f87b60457a98636d2500105fd5`）、PlayMode 6/6（`f471da910e824194ba4be952ef2588d6`）、BakedSocket 1/1（`a11aa7259e204f33be8f967591c48cc5`）、Unity 完整验证（`2c6c0efec80a41898db63cc3b9d12030`）通过 | 无 M1 验收阻断；无 framebuffer 画面验收；static-target、CandidatePose Unsupported；完整 Zone 语义属于 M2 |
| 2026-08-18 | CD-M3 | `Planned` | `Blocked` | 只读依赖审计，无实现提交 | 当前源码只有 L1 本帧局部探测；未找到静态导航图、有界 A*、路径状态/replan、稳定 graph revision/hash 或 Pathfinding Foundation M2-M4 测试与验收证据 | 解除条件：CD-M2 Verified；Pathfinding Foundation M2 静态图、M3 有界 A*、M4 Planner 接入均 Verified；随后再实施 Anchor/CandidatePose/预约门禁 |
| 2026-08-18 | CD-M2 | `Planned` | `In Progress` | `main` 脏工作区，尚未提交 | T0 当前链路审计和字段级合同已冻结；T1 Authoring/Compiler 开始实施 | T2～T6 尚未实施；CD-M3 继续 Blocked |
| 2026-08-18 | CD-M2 | `In Progress` | `In Progress` | `main` 脏工作区，尚未提交 | T1～T3 Verified；T4/T5 Implementation Complete；T6 已完成 Dragon14 正式 fixture、M2 22/22 EditMode、3/3 PlayMode、M0/M1 回归、Unity Console 0 和接口回写 | 仍缺 Game View overlay/framebuffer、CharacterStudio 完整直接 UI、下一 tick command replay/transport 与低频 snapshot 0 B 门禁；不得声明 M2 Verified |
| 2026-08-18 | CD-M2 | `In Progress` | `Verified` | `main` 脏工作区，尚未提交 | T0～T6 7/7 Verified；Game View overlay/framebuffer、CharacterStudio 隔离实际编辑闭环、rollback 后下一 tick command parity、逐帧 NoAlloc、M2 29/29 EditMode + 3/3 PlayMode、M0/M1 107/107 + 6/6、双程序集构建与 Unity Console 0 全部通过 | 无 M2 验收阻断；低频 snapshot capture/apply 为 `LowFreqAlloc`；未另做多进程 transport loopback；CD-M3 仍受 Pathfinding Foundation M2～M4 前置阻断 |
| 2026-08-18 | CD-M3 | `Blocked` | `Blocked` | 文档拆分；无生产实现提交 | PF-M2/PF-M3 已 Verified；将 CD-M3 拆为 T0～T6，并将 CD-M4 拆为 T0～T5 | 剩余解除条件仅为 PF-M4 T0～T4 全部 Verified；CD-M4 继续等待 CD-M3 Verified |
| 2026-08-18 | Pathfinding PF-M4 | `In Progress` | `Verified` | `main` PF-M4 独立收口 | T0～T4 5/5；36/36 graph/session/perception、9/9 Planner path、18/18 受影响旧行为、Unity Console 0、Game View `PathWaypoint/Success` 绕障与 warmed 0 B 闭合 | 只验证 local-offline/loopback；派生 graph/path/diagnostics 不进入 authority snapshot/hash |
| 2026-08-18 | CD-M3 | `Blocked` | `Planned` | 解除前置阻断，无 CD-M3 生产实现 | CD-M2 与 PF-M2～PF-M4 已全部 Verified | 从 CD-M3-T0 重新审计 Anchor/CandidatePose/容量/预约合同；CD-M4 仍等待 CD-M3 Verified |
| 2026-08-18 | CD-M3 | `Planned` | `In Progress` | `main`，T0 文档审计 | T0 已冻结 Anchor v1、CandidatePose/PF-M4 复用、reservation authority 与 T1～T6 精确验证矩阵 | 当前运行期尚无独立 Boss phase controller；T1 开始实施，CD-M4 继续等待 |
| 2026-08-20 | CD-M3 | `In Progress` | `In Progress` | `main`，T0～T4 Verified，T5 Implementation Complete，T6 In Progress | Dragon14 v3/6 anchors、CandidatePose/PF-M4 path、canonical reservation、schema12/29、EditMode 163/163、PlayMode 12/12、active Game View、warmed 0 B、接口回写完成 | CharacterStudio 直接浏览器因插件版本错配未取证；不得声明 CD-M3 Verified，CD-M4 继续等待 |
| 2026-08-20 | CD-M3 | `In Progress` | `In Progress` | `main` 实现提交 `9196cb2ce` 已推送并与 `origin/main` 0/0 | 提交后 Unity compile/domain reload/Console 0、M3+diagnostics EditMode 22/22、M3 PlayMode 3/3、双程序集 0 error、Authoring focused 与 Studio smoke 通过 | 浏览器插件实际安装 `26.814.41407`、运行时误寻不存在的 `26.814.41957`，直接 UI 证据仍被外部环境阻断；CD-M4 继续等待 |
| 2026-08-20 | CD-M3 | `In Progress` | `In Progress` | `main`，T5 从 Implementation Complete 升级 Verified | Browser 直接验证 Dragon14 6 anchors、标准/900px、Inspector/3D、dirty refresh/Undo/save/reopen、copy/order、future-schema raw read-only 与 Console 0 error | CD-M3 无功能阻断；等待 T6 最终 P0/P1 审查、同步点复验与收口提交；CD-M4 继续等待正式 Verified |
| 2026-08-20 | CD-M3 | `In Progress` | `Verified` | `main`；实现 `9196cb2ce`、Studio 收口 `4d9864cde` 均已推送 | T0～T6 7/7；P0/P1=0；同步点 Unity/Console 0、22/22 EditMode、3/3 PlayMode、Studio/Authoring passed、0/0 clean | 无 CD-M3 验收阻断；local-offline/loopback、无独立 Boss phase authority、多进程/公网未验证；CD-M4 前置解除 |
| 2026-08-20 | CD-M4 | `Planned` | `In Progress` | `main`；T0 合同审计 | T0 1/6 Verified；冻结 DTO/identity、完整整数 comparator、预算/cache、Preview/Commit、每 tick 派生 authority 与校准矩阵 | T1 开始生产候选生成；临时预算不得宣称已完成 T4 production 校准 |
| 2026-08-20 | CD-M4 | `In Progress` | `In Progress` | `main`；T0～T2 3/6 Verified | T1 `6ac350dcb` 已推送；T2 完成联合 reducer、两层 cache、Preview/Commit，只读 reservation，focused 47/47、受影响回归 132/132、Unity Console 0、warmed 0 B | T3 尚未接 Planner command/overlay；宽泛 Formalization 扫描 18 个既有 behavior/UI/profile 失败保留到最终审计 |
| 2026-08-20 | CD-M4 | `In Progress` | `In Progress` | `main`；T0～T3 4/6 Verified | T3 接入每 tick 联合 target/action/Zone 选择、Current/Turn/Approach command、最终胜者 commit 与只读 Commands overlay；focused 39/39、Unity validate/Console 0、热链与诊断读取 0 B | T4 尚需正式预算校准；T5 尚需 Playable/Game View 与最终全量收口 |
| 2026-08-20 | CD-M4 | `In Progress` | `In Progress` | `main`；T0～T4 5/6 Verified，T3 `d9761dd05` 已推送 | 6 fixture/5 顺序扰动/120+600 tick 校准通过；峰值 32/16/4/256/256/256/256/256，production default 与 cache 逐项 2 倍；压力 typed truncation、NoAlloc、Unity Console 0 | 仅余 T5：直接 Game View、全链 NoAlloc、全回归、接口/能力表与终审交付 |
| 2026-08-20 | CD-M4 | `In Progress` | `Verified` | `main`；T0～T4 提交均已推送，T5 收口见 Git 历史 | T0～T5 6/6；无 joint leaf 时恢复普通 Planner 移动但攻击继续 evaluator fail-closed；EditMode 199/199、PlayMode 14/14、直接 Game View、Unity Console 0、全链 NoAlloc、接口回写和 P0/P1=0 | 无 CD-M4 验收阻断；全程序集 7183 项审计仍有缺失 Piloto 索引和既有 Runtime AI Lab 卡数据失败；dedicated server/公网/多进程/大型 raid 未验证 |
