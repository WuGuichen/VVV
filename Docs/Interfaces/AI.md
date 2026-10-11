# Runtime AI Planner 接口

> Status: Current
>
> **本文的 AI 指 Runtime AI Planner**，即 `MxFramework.AI` 模块的游戏内轻量行为决策引擎。
> 不包含 AIAction 配置数据迁移（AIAction Config）、LLM 辅助创作（Authoring AI Assist）或开发 Agent 工作流（Development Agent）。
> 参见 `Docs/INTERFACES.md` 的 AI Terminology 章节。

## 职责

Runtime AI Planner 提供不依赖插件的游戏内行为决策基础设施：事实、黑板、目标、动作、条件、效果、传感器、规划器、执行器和代码级 HFSM。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `AiFactKey` | AI 事实 key |
| `IAiWorldState` / `AiWorldState` | 事实集合 |
| `IReadOnlyAiBlackboard` / `IAiBlackboard` / `AiBlackboard` / `SharedAiBlackboard` | 分层黑板；支持 parent fallback、本地覆盖、变更事件和 flatten snapshot |
| `IAiGoal` / `AiFactGoal<T>` | 目标 |
| `IAiAction` / `AiAction` | 动作 |
| `IAiCondition` / `AiFactCondition<T>` | 前置条件 |
| `IAiEffect` / `AiSetFactEffect<T>` | 模拟效果 |
| `IAiGoalSelector` / `PriorityGoalSelector` | 目标选择 |
| `IAiPlanner` / `SequentialPlanner` / `AiUtilityPlanner` / `AiHfsmPlannerAdapter` | 规划器 |
| `AiPlan` | 规划结果 |
| `IAiAgent` / `IAiSensor` | 游戏层扩展点 |
| `IAiSensorManager` / `AiSensorManager` / `AiSensorManagerDiagnostics` | 传感器注册、优先级、tick 间隔和失败诊断 |
| `IAiUtilityConsideration` / `AiUtilityPlanner` | Utility action 评分和确定性 tie-break |
| `IAiActionRunner` / `AiSequenceActionRunner` | 规划动作序列执行器；逐步 apply action effect |
| `AiAgentBrain` / `AiAgentBrainTickResult` | Agent 组合根；按 sensors -> planner -> runner 顺序推进 |
| `IAiHfsmState` / `IAiHfsmTransition` / `AiHfsmStateMachine` | 代码级分层状态机；支持 parent path enter/exit 和优先级 transition |
| `RuntimeAiPressureFactKeys` | Runtime AI Planner pressure fact key 字符串契约；band 使用稳定 `int` 值，不引用 Gameplay enum |
| `ExploitScatterPressureWeaknessGoal` | 基于 `target.scatter_pressure.band` 的目标，默认 activation band 为 `2`（Cracked） |
| `PressureImpactData` / `ScatterPressureWeightEvaluator` | Planner 外层 action 排序工具，根据 pressure facts 和 action impact data 返回权重倍率 |
| `ScatterPressureAiSensorAdapter` | Gameplay scatter / guard / armor pressure 到 Runtime AI Planner world state 的 bridge adapter |

## 使用约定

- `AiFactKey` 使用字符串 key，不使用 WGame 枚举或实体。
- Planner 对传入世界状态 clone 后模拟，不修改原始状态。
- `Apply` 只修改模拟状态；真实移动、攻击、技能执行由游戏层负责。
- 框架不依赖 CrashKonijn、外部行为树插件、Unity NavMesh 或具体怪物逻辑。
- `AiUtilityPlanner` 是单步 utility selector；`SequentialPlanner` 是有界动作链搜索；`AiHfsmStateMachine` 是代码级 HFSM primitive。框架当前不提供可视化行为树编辑器、生产级 GOAP authoring/editor 或 WGame 专用行为包。
- CharacterTest 的正式 adapter 位于 `MxFramework.Game.CharacterTest`，不是 `MxFramework.AI` 公共契约的一部分。它把 StreamingAssets JSON profile 加载为 CharacterTest 专用 DTO/evaluator，复用 `AiHfsmStateMachine` 的 HFSM 语义和 `AiUtilityPlanner` 的 score/cost/id tie-break，但高频 Tick 路径不直接读文件、解析 JSON、创建 `AiBlackboard`、`AiSensorManager` 或 `AiPlan`。
- Runtime AI Lab 的正式行为创作权威由 `runtime_ai_planner/behavior_card_catalog.json` 索引、`runtime_ai_planner/behavior_cards/<stable-card-id>.json` 独立卡文件与 `behavior_group_catalog.json` 组成。项目新建卡使用 `bc_` 加 10 位 Crockford Base32 随机短码作为 stable ID；每个 stable card ID 只对应一个文件，该文件保存它的全部已发布 revision。行为组版本固定引用具体 revision、semantic hash 和 content hash，卡目录更新不会让既有组浮动升级。
- JSON 解析、目录校验、v3→v4 迁移和 Profile 编译只发生在设置、编辑或启动边界。实验开始后使用冻结的组/卡证据与编译 Profile，Tick 和回放不追踪项目目录“最新版”。项目写入由 CharacterTest.Editor 桥执行，Player 不注册写入实现；缺失或损坏时阻止实验，不回退到 C# seed。
- CharacterTest adapter 提供 `character-test.runtime-ai-planner.target.dead` 兼容布尔事实；选中目标的 `CombatState.IsAlive == false`（`Dying`、`Dead`、`Reviving`）时为 `true`，没有目标时为 `false`。
- 精确生命周期使用 `character-test.runtime-ai-planner.target.life-state`：`None`、`Alive`、`Dying`、`Dead`、`Reviving`。两项事实只供条件、快照与诊断读取，不改变默认目标选择或行为规则。
- CharacterTest Runtime AI Planner 只输出 runtime command intent。`CharacterTestRuntimeAiPlannerCommandModule` 在 `PreSimulation` 提交 `CharacterTestRuntimeCommandSources.RuntimeAiPlanner` 来源的 `SetControlIntent`，由 `CharacterTestRuntimeCommandModule` 统一应用；Editor Play Monitor 只通过 `IFrameworkDebugSource` 读取快照，不把运行时对象反写到 profile。

### CharacterTest CD-M1 Attack Opportunity 接线

2026-09-11 性能边界补充：已编译 behavior execution 按当前条件窗口声明 `target.inAttackRange` 需求，并按程序依赖闭包声明环境／导航需求，移动操作的导航方向也是隐式依赖。未请求时通过事实存在性／观测标记与 false 区分，同帧新到达的攻击事实依赖由执行上下文补齐，UI 不发起查询；目标记忆等有状态观测继续推进。有效 Joint 主攻击结论可投影为该事实。CurrentPose 先准备，TurnInPlace 按需生成；query 复制保留惰性，provider 绑定 preparation generation 与源／世界证据。纯 Service 以固定容量的每模式最近结果复用相同有效查询，但每次仍先做完整新鲜度校验，权威锁定／占位和命令守卫保持原路径。策略事件索引在准备期间建立，稳定帧顺序在计划 Seal 时建立，均不进入 schema／hash。实际范围、回归与性能限制见[实施记录](../Tasks/CHARACTER_TEST_PLANNER_OPPORTUNITY_OPTIMIZATION_IMPLEMENTATION.md)。

CD-M1 的 CharacterTest adapter 是已实现的内部 Runtime AI Planner 接线，不扩展 `MxFramework.AI` 公共类型。`PreSimulation` 以上一已完成模拟帧的 sealed CombatPhysics/Hurtbox authority 为输入，独立构建 static-target Preview Trace Plan；不读取当前帧尚未产生的 actual plan。目标在一次评估内冻结，目标未来运动不属于首版合同。

`PrimaryAttack` 的最终 eligibility 由机会结果控制：`CurrentPose` 才能攻击；`TurnInPlace` 只发量化 yaw、零平移且本 tick 不攻击；`RequiresTranslation` 禁止攻击并继续接近；`Unavailable`、`StaleSnapshot`、`Unsupported` 均 fail closed。CD-M3 对正式 authored Engagement Anchor 使用同一 evaluator 的 `CandidatePose` 模式筛选可攻击姿态。CD-M5 通过版本化迁移把 `target.inAttackRange` 接到锁定 Hurtbox 的 CurrentPose opportunity；`CastAbilitySlot`、Skill、Guard、Dodge 等特殊动作仍不借此放行。

Runtime AI Lab behavior overlay 只有明确 `BasicAttack` / `light_attack` action 才使用同一 guard。旧 direct-input wrapper 仅作兼容旁路/测试 fixture，不是 M1 生产路径。影响未来命令的 TurnInPlace yaw/delta、active-move=false 语义随 command、input、network envelope、snapshot、hash、rollback 和 replay 一致序列化。CD-M3 的 Anchor reservation 也进入 Planner schema 12 与 CharacterTest network schema 29；CandidatePose/path sample 是当前帧派生证据，不序列化。

Attack evaluation identity 使用 CharacterTest 内部独立的 source pose/facing 单调 write revision，不以 completed frame 代替。同帧 root、yaw、AuthorityPose/socket 变化会使旧 Trace Plan、result cache 与 Planner witness 失败关闭；formal network snapshot schema 26 捕获/恢复这两个 revision，旧 schema 归一为初始 `1/1`。活动 direct-hit window、multi-hit policy ledger 与 schema 27 CasterBody 跨 tick previous-center authority 均进入 snapshot/hash/rollback/replay。

### CharacterTest CD-M2 DamageZone 稳定选择

CD-M2 在 CharacterTest 内部把正式 `geometry_binding.json` v2 的 DamageZone 投影接入同一 Attack Opportunity adapter；`MxFramework.AI` 公共类型保持不变。选择器只读取同一 sealed Hurtbox/CombatPhysics snapshot，按机会等级、最早命中 tick、作者优先级、Zone ID、Hurtbox ID 稳定决胜，并以显式 previous lock 实现“更高机会等级立即切换、同等级改善连续 2 tick 后切换”的迟滞。没有 v2 Zone 的旧包继续使用 M1 legacy Hurtbox 查询，不伪造持久 Zone lock。

Zone lock 是 Planner authority：包含 source/target/action identity、Zone/HitZone/Hurtbox、机会等级、earliest tick、revision witness、选择帧与切换原因。失效或 stale 输入 fail closed；rollback reset 清空非快照 cache，正式 snapshot apply 恢复 lock。CharacterTest network document schema 28 保存该状态，稳定 canonical order 参与 full/owned/non-owned hash。

### CharacterTest CD-M3 Engagement Anchor

正式 `geometry_binding.json` v3 可投影 ModelRoot-local authored Anchor。Planner 用 CombatPhysics standing capsule、M1 CandidatePose evaluator 与 PF-M4 bounded A* 依次过滤，再按 previous Zone lock、作者优先级、penalty-inclusive path cost、最早命中 tick、Zone/Hurtbox/Anchor stable ID 决胜。容量预约由固定上限 authority 独占写入；同帧冲突以 source ID 稳定归约。逐帧投影/路径/预约 warmed 0 B，诊断捕获和 snapshot capture/apply 为显式低频分配边界。

### CharacterTest CD-M4 联合选择

CharacterTest Runtime AI Planner 每 tick 以固定容量枚举有限 `(target, resolved action, DamageZone)`，用 M1～M3 evaluator 预览 CurrentPose、TurnInPlace 和合法 ApproachPose，再按冻结的整数比较器统一归约。根距离只参与 actor 软排序；更远但可立即命中的目标可以胜过更近但需要转向/移动的目标。没有专用 evaluator 的 `CastAbilitySlot` 仍是显式 `CompatibilityLegacyRange`，不会伪造 Attack Opportunity。

CharacterTest 根距离现由 Completed Frame Geometry 双缓冲统一提供：post-motion `Building[N]` 供 Combat N，提交的 `Committed[N]` 供 Planner / Runtime Commands N+1。CD-M5 在同一 Buffer 冻结姿态胶囊、Hurtbox shape 与 DamageZone/member。`target.rootDistance` 保留原 Transform-root XZ 语义；`target.distance` 是 source 胶囊到持久锁定 Hurtbox 的三维净空。两者都是派生数据，不进入 SaveState/network/hash；稳定 Actor/Hurtbox identity 才进入 Planner/network authority。

联合 selector、两层 trace/result cache 和诊断都是派生状态：frame 开始轮换、rollback/snapshot apply 清空、下一 tick 重算，不进入 Planner/network schema 或 authority hash。只有最终胜者通过 Commit 写入既有 DamageZone lock、Engagement reservation 和 runtime command；action/motion/Combat 仍是最终执行权威。DamageZone lock 的 `HurtboxRevision/WorldRevision` 与 reservation 的 `CombatPhysicsRevision` 仅是进程内 freshness witness：它们继续随 snapshot 传输并用于本地 stale 检查，但 authority hash 只覆盖 binding、zone、body-part、anchor、graph 等跨分支语义字段，避免同一状态因 restore 触发本地重建而分叉。Commands overlay 只复制上一 tick 的不可变快照，显示 target/action/zone/anchor、机会模式、cost/tick、候选/截断/cache/failure，不重新运行 query 或改写 cache。

production 工作量预算按 seed 42004 的 6 类代表 fixture（最高 16v16）零截断峰值逐项取 2 倍：actor scan/TopK/ready action/action-zone broadphase/joint/leaf PreviewTrace/CandidatePose/path query 为 `64/32/8/512/512/512/512/512`，trace/result cache 为 `128/512`。预算限制工作量而非语义可达性；超限 fail closed，按 ActorScan、ActorTopK、ReadyAction、ActionZoneBroadphase、JointCandidate、LeafPreviewTrace、CandidatePose、PathQuery 或 cache phase 输出 typed truncation。该校准只覆盖 local-offline 单进程代表规模，不构成 dedicated server、公网或超大 raid 容量声明。

### CharacterTest CD-M5 复合目标

正式 combat target 为 `GameplayEntityId + LogicalHurtboxId`。首次 Actor 选择按 source Completed Frame 姿态胶囊到目标合法 Hurtbox 表面的 Fix64 gap 取得最近 leaf；同 Actor 下动作、距离、机会等级、Zone priority、动画和部位状态变化均不触发重选。Hurtbox 不可用时保留 Handle，`target.distanceAvailable=false`、距离 Predicate 和攻击失败关闭。CD-M4 对当前 Actor 只评估锁定 leaf；challenger Actor 使用 generation memo 的 provisional nearest leaf，只有 Actor 胜出才提交。

### CharacterTest Pathfinding Foundation PF-M2 静态图

PF-M2 已在 `MxFramework.Game.CharacterTest` 内部建立不可变静态 navigation graph，不新增 `MxFramework.AI` 公共 API。Node/Edge 使用正整数稳定 ID、`FixVector3` 位置、毫米 clearance、非负 traversal penalty、area tag 和 edge blocked-layer metadata；双向通行由两条显式有向边表达。低频 builder 复制并严格校验输入，Node/Edge 及 outgoing adjacency 按稳定 ID canonicalize，图身份固定为调用方正 revision 与内容导出的 stable content hash 二元组。

运行期只读入口提供 stable-id 二分查找、clearance-aware nearest node 和预计算 outgoing range，初始化后 NoAlloc。它不扫描 Unity Scene、不读取 NavMesh/Collider/Transform，也不保存跨帧路径状态。全局可达性查询由 PF-M3 在这份唯一拓扑上实现，不能另建平行图。

### CharacterTest Pathfinding Foundation PF-M3 有界 A*

PF-M3 已在同一 internal graph 上提供 `CharacterTestBoundedAStarPathfinder`、`CharacterTestPathQuery`、caller-owned `CharacterTestNavigationPathBuffer` 和只读 `CharacterTestPathfindingSample`，仍不新增 `MxFramework.AI` 公共 API。查询以 graph revision/content hash 双 witness fail closed，按 clearance、hard area mask 和调用方给出的 active blocked-layer mask 过滤，并以最大 expansion/path node 数形成硬预算。

权威比较只使用固定点位置与非负整数毫米成本。Edge 作者长度必须不小于两端 Node 的几何距离，因此直线毫米 heuristic 保持 admissible/consistent；open set 决胜固定为 `f -> h -> NodeId -> incoming EdgeId`，等价 parent 再按 incoming EdgeId、parent NodeId。成功结果包含 node/edge corridor、endpoint snap、next waypoint、remaining geometric distance、penalty-inclusive total cost 和 expanded count；失败返回 typed status 并清空 corridor。pathfinder/scratch 在初始化时分配，预热后的重复查询为 0 B，不保存跨帧 cursor 或隐式 replan state。

### CharacterTest Pathfinding Foundation PF-M4 Planner 接入

PF-M4 已在正式 CharacterTest session 低频路径中把显式 navigation region 和 static `WorldObjectSceneCache`/CombatPhysics 输入编译为唯一共享 graph/session；build clearance 使用场景 fallback 与全部已注册 playable character physics profile 的分量最大值。Tick 不扫描 Unity Scene，也不使用 NavMesh、PhysX 或 Transform 作为导航权威。

`CharacterTestPerceptionFrameCache` 的同帧 navigation key 覆盖 source/target/profile、角色 clearance、graph revision/hash、路径预算和 CombatPhysics world revision。direct capsule 受阻后才运行有界 A*；成功时从 corridor 第一条实际 traversed node 产生 waypoint，并再次用 CombatPhysics capsule 验证当前 segment；动态 segment 阻挡进入现有 L1 candidate steering，无法绕行才显式 stop。`Unreachable`、`BudgetExceeded`、endpoint outside、graph unavailable/stale 等状态保持 typed 且不会复用旧 success。

Runtime AI Planner 的 MoveToTarget、Strafe、Retreat 只消费统一 sample 的 move 建议，最终仍提交 `SetControlIntent` 并经过 Character Motion/CombatPhysics。Facts 保持 `move.pathAvailable`、`move.directPathClear`、`move.candidateIndex` 稳定语义；Runtime AI Lab Facts、Play Monitor 和 overlay 只读显示 graph identity、status、waypoint、cost、expanded/query、blocker/replan/fallback，不发起新 query。navigation sample、graph 和 diagnostics 都是可重算派生数据，不进入 SaveState、network、rollback、replay 或 authority hash；rollback/reset 后清空并在下一 Tick 重算，presentation/recovery identity 可以包含只读诊断摘要。

## Gameplay bridge

`MxFramework.AI.GameplayBridge` 是 Runtime AI Planner core 与 `MxFramework.Gameplay` 的薄桥接程序集。它引用 `MxFramework.AI` 和 `MxFramework.Gameplay`，保持 `noEngineReferences=true`，不引用 Combat、UI、UnityEngine 或 UnityEditor。

`ScatterPressureAiSensorAdapter` 实现 `IAiSensor`，从 `GameplayComponentWorld` 读取 `GameplayScatterPressureComponent`、`GameplayGuardPressureComponent` 和 `GameplayArmorIntegrityComponent`，再写入调用方传入的 `IAiWorldState`。构造时必须传入 `Func<int, GameplayEntityId>` self resolver；可选传入 target resolver。未传 target resolver 时，adapter 会移除所有 `target.*` pressure facts，避免保留旧目标状态。

输出 facts：

| Fact key | Value type | 来源 |
|----------|------------|------|
| `self.scatter_pressure.band` / `target.scatter_pressure.band` | `int` | `(int)GameplayScatterPressureComponent.CurrentBand`；稳定值为 Stable=0, Pressed=1, Cracked=2, Critical=3, Broken=4 |
| `self.scatter_pressure.ratio` / `target.scatter_pressure.ratio` | `float` | `ScatterDebt / CurrentCohesionLimit`，即 `GameplayScatterPressureComponent.CurrentScatterPressure / CurrentCohesionLimit` |
| `self.scatter_pressure.broken` / `target.scatter_pressure.broken` | `bool` | `GameplayScatterPressureComponent.IsBroken` |
| `self.guard.band` / `target.guard.band` | `int` | `(int)GameplayGuardPressureComponent.CurrentBand`；稳定值同 pressure band |
| `self.guard.ratio` / `target.guard.ratio` | `float` | `CurrentPressure / MaxPressure` |
| `self.guard.broken` / `target.guard.broken` | `bool` | `GameplayGuardPressureComponent.IsBroken` |
| `self.armor.ratio` / `target.armor.ratio` | `float` | `CurrentIntegrity / MaxIntegrity` |
| `self.armor.broken` / `target.armor.broken` | `bool` | `GameplayArmorIntegrityComponent.IsBroken` |

缺失策略：resolver 返回 invalid / dead entity、component store 缺失、目标 entity 缺少对应 component，或 component state 无效时，adapter 移除对应 facts，不抛玩法异常。缺少 scatter pressure 只移除 scatter pressure facts；缺少 guard 或 armor 同理，不影响同一 entity 上其他 pressure facts。

`ScatterPressureWeightEvaluator.GetActionWeightModifier(actionId, worldState, impactData)` 不参与 `SequentialPlanner` BFS。调用方可在 planner 选出候选 action 后，用 `PressureImpactData(impactForce, isHighScatterDamage, isDefensiveRecovery)` 叠加排序倍率：高 scatter pressure impact 会在目标 scatter pressure band 达到 Cracked 后提高进攻权重；defensive recovery action 会在自身 guard/scatter pressure/armor 压力升高时提高恢复权重。

## 最小示例

见 `Docs/USAGE.md` 的 AI 轻量 Planner 章节。

## 测试入口

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/AI/`


### CharacterTest 单手剑自动交战预设

2026-10-09 首版 `bg_06c30f0b24 r2` 使用现有战术执行与 canonical 仲裁，将远距接敵、455 攻后普通撤离、已确认近期威胁退避及现有 fallback 组合为四卡自动行为。角色身份固定为 `char.000001 / 668367834`；3m 和 24 帧为作者偏好，不替代真实动作范围、资源/冷却和武器 authority。移动窗口可通过现有 `ReturnToPlanner` 与 SelfActionInterruptible 条件返回选择器，已提交动作不被强行替换。Editor 试用复用独立 configured-card 与临时 Lab 工作区，不修改玩家角色持有或装配。源码、正式身份、显式入口、11 个原生 case 和真实场景证据见[实施记录](../Tasks/CHARACTER_TEST_SINGLE_SWORD_AUTOMATIC_PRESET_R1.md)。
