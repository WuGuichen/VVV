# CharacterTest Runtime AI Planner Pathfinding Foundation

> Status: Current
>
> Date: 2026-06-30
>
> Scope: CharacterTest Runtime Slice 的 Runtime AI Planner 寻路基础。本文不定义 `MxFramework.AI` 公共寻路 API，不引入 Unity NavMesh authority，也不替代 CharacterTest 角色运动权威。

## 目标

CharacterTest Runtime AI Planner 已经能用 `MoveToTarget`、`Strafe`、`Retreat` 输出连续移动意图，并通过 `CharacterTestPerceptionFrameCache` 复用目标几何、视线、navigation 和环境采样。下一步寻路基础要解决的是：当直线移动被静态障碍、动态角色或危险区域干扰时，Runtime AI Planner 可以得到一个确定性、可诊断、低分配的移动建议，而不是在每个行为里各自做距离、角度、射线和绕行计算。

首版目标是 CharacterTest 内部 runtime slice：

- 复用 `ICharacterTestAiNavigationService` 作为 Runtime AI Planner 的导航入口。
- 复用 `CharacterTestPerceptionFrameCache` 的目标几何、LOS、navigation、environment 派生数据。
- 复用 `CombatPhysicsWorld` 的 noEngine 查询和稳定排序，不读取 Unity `NavMesh`、`Collider`、`Rigidbody` 或场景对象作为权威。
- 输出仍是 `SetControlIntent` 的 move vector；实际移动、碰撞、摩擦、推挤、root motion、击退和重力仍由 `CharacterTestCharacterMotionSystem` 处理。
- 高频 Tick 路径 warmed 后保持 `NoAlloc after initialization`。

## 当前基线

当前实现已有两级导航：

| 层级 | 当前能力 | 入口 | 边界 |
| --- | --- | --- | --- |
| L0 Direct | 直接朝目标移动，距离进入 attack range 时停止或攻击 | `CharacterTestDirectAiNavigationService` | 不查询障碍；适合作为 fallback |
| L1 Probe | 通过 CombatPhysics ray 检查直线路径；直线受阻时按候选角尝试局部绕行 | `CharacterTestCombatPhysicsNavigationService` | 局部 steering，不保存跨帧路径，不做全局寻路 |
| Shared Frame Cache | 同帧复用 navigation query、目标几何、环境采样 | `CharacterTestPerceptionFrameCache.TryGetNavigation` | 只保存当前帧派生摘要，不持有权威状态 |

这套基线适合小场景和近距离战斗移动，但不是完整寻路：它不知道地图连通性，不维护路径 corridor，不限制每帧全局搜索预算，也不能解释“目标可达但需要绕到另一侧”的长距离情况。

## API 复用计划

| 需求点 | 优先使用的模块 / API | 本文约定 |
| --- | --- | --- |
| 固定帧和生命周期 | `RuntimeHost`、`RuntimeFrame`、CharacterTest runtime module tick | path query 只能在明确的 frame 生命周期内执行；不要从 Unity `Update` 直接驱动权威寻路 |
| 行为决策 | Runtime AI Planner HFSM + Utility、`CharacterTestRuntimeAiPlannerEvaluator` | pathfinding 只提供移动事实和移动建议，不接管 state transition / action score |
| 命令输出 | `CharacterTestRuntimeCommandModule`、`SetControlIntent` | path result 最终仍转成 move vector / skill intent；不绕过 command pipeline |
| 空间权威 | `CombatPhysicsWorld`、`CombatRayQuery`、`CombatSphereQuery`、`CombatCapsuleQuery` | 所有 obstruction / clearance / reachability 检查走 noEngine Combat Physics |
| 每帧派生数据 | `CharacterTestPerceptionFrameCache` | 距离、角度、目标中心、LOS、direct-path、environment 摘要必须优先复用 |
| 角色运动 | `CharacterTestCharacterMotionSystem`、`CombatKinematicMotor` | 寻路不直接改 transform / velocity；只产生命令意图 |
| 配置 | `CharacterTestAiBehaviorProfile`、Runtime AI Planner profile movement/perception | 首版不改 Runtime AI Planner JSON schema；如需新 `navigation` schema，另开版本迁移 |
| Debug / HUD | `IFrameworkDebugSource`、Runtime AI Planner Play Monitor | 输出 query count、path status、selected waypoint、blocked reason 等只读诊断 |

## 数据模型边界

寻路基础应分清三类数据：

| 数据类型 | 示例 | 所有者 | 是否进 SaveState / network / replay/hash |
| --- | --- | --- | --- |
| 权威状态 | entity transform、team、alive、CombatPhysics bodies/colliders、角色 motion state | CharacterTest runtime / CombatPhysics | 按现有 CharacterTest 权威状态规则处理 |
| 当前帧派生数据 | 距离、方位角、target center、directPathClear、candidateIndex、nearHazard | `CharacterTestPerceptionFrameCache` | 否，下一帧可重算 |
| 可选跨帧导航状态 | path cursor、replan cooldown、last failed node、stuck counter | Runtime AI Planner agent state 或未来 navigation agent state | 只有当它影响后续 command 输出时，才允许成为权威状态，并必须补 hash/save/replay 边界设计 |

首版建议只做当前帧派生 path sample，不引入跨帧路径承诺。需要跨帧 state 时，必须先定义它和 Runtime AI Planner agent state 的关系，避免把隐式 path memory 藏在 service 内部。

## Pathfinding 分层路线

CD-M3 已直接复用 PF-M4 的 `CharacterTestNavigationSession`、bounded A* 与 caller-owned path buffer 计算 authored Engagement Anchor 的 CandidatePose path cost/next waypoint；没有建立第二张图或第二套寻路权威。Anchor reservation 进入 Planner/network authority，但 graph/path sample 仍按 PF-M4 合同保持当前帧派生。

### L1：局部 probe 导航稳定化

L1 是当前实现的强化版，仍不做全局寻路：

- 继续使用 `ICharacterTestAiNavigationService.TryResolveMove(self, target, attackRange, out result)`。
- direct path clear 时直接输出目标方向。
- direct path blocked 时按 profile candidate angles 做 CombatPhysics ray/capsule probe。
- 结果写入 `CharacterTestAiNavigationResult`：`Move`、`DirectPathClear`、`TargetInAttackRange`、`SelectedCandidateIndex`。
- `CharacterTestPerceptionFrameCache.TryGetNavigation` 负责同帧复用。

L1 应补的基础能力：

- query shape 从 ray 逐步升级为角色 clearance-aware capsule probe，至少可配置半径/高度来源。
- navigation profile key 必须覆盖 probe 长度、候选角、query shape、layer mask 和 clearance 参数。
- Debug 输出 blocked hit count、nearest obstacle id/body/distance、selected candidate。
- focused tests 固定旧行为：直线清空仍走 direct；候选角 tie-break 稳定；同帧同参数只查询一次。

### M1 implementation notes (2026-06-30)

M1 已落地在 CharacterTest runtime 内部边界，不新增 `MxFramework.AI` 公共 API，也不改 Runtime AI Planner JSON schema。

Agent 和后续系统接入时按以下规则使用共享数据：
- `CharacterTestCombatPhysicsNavigationService` 和 `CharacterTestPerceptionFrameCache.TryGetNavigation` 共用 `CharacterTestNavigationClearanceProbe`，使用一次 `CombatObbQuery` 做 L1 全高保守 local probe（2026-10-11，Issue #758）。半径来自当前姿态 half extents 的 X/Z 最大值，高度和实际中心进入 query；box 沿移动方向包围扫过的 standing capsule，包含两端半径范围，避免腰部单横向胶囊漏检矮墙/上部障碍。下缘排除 motor skin，只有 grounded 角色再排除 `MaxStepHeight`；实际移动权威仍在 `CharacterTestCharacterMotionSystem`。
- `CharacterTestNavigationSample` 是当前帧只读派生数据，可供 Runtime AI Planner、Debug/HUD 或后续 Play Monitor 读取：`Result`、`QueryShape`、`LayerMask`、`ClearanceRadiusMillimeters`、`ClearanceHeightMillimeters`、`CombatQueryCount`、`DirectPathHitCount`、`SelectedPathHitCount`、`NearestObstacleEntityId`、`NearestObstacleBodyId`、`NearestObstacleDistance`。
- 消费者不得保存或公开底层 `List<CombatQueryResult>`；需要“是否阻挡、命中数量、最近阻挡体、候选角选择”时直接读 sample 字段。
- navigation cache key 覆盖 frame 生命周期、source/target、attack range、profile key、probe min/max、candidate count、query shape、layer mask、clearance radius/height/实际中心及 grounded 状态；同帧同参数复用，下一帧或 clearance/profile/跨台阶资格变化会刷新。
- direct、waypoint、candidate 每段维持一次 CombatPhysics query，sample 的 `QueryShape=SweptObb` 如实表示查询形状；复用结果列表，warmed 后不新增分配。矩形端部角落可能产生比圆胶囊更保守的拒绝；它不是精确 capsule cast，也不代替电机对台阶/斜坡的最终判断。共享 graph 的 standing capsule/grid build 保持原有实现，不新增第二张图。
- 回归入口为 `CharacterTestNavigationPerceptionTests` 的全高障碍、低墙 detour/stop、ground/step、空中 cache witness、posture center 和 dynamic mask 对照；原生测试/compile 的具体候选及回执维护于 Issue #758，测试定义不等于已运行通过。
- Runtime AI Planner 仍通过 `ICharacterTestAiNavigationService.TryResolveMove` 取得移动建议；`TargetLastSeenAgeFrames`、`TargetThreatScore`、score/cost、strafe/reaction 状态仍归 Runtime AI Planner agent state。
- 这仍是 L1 局部 steering 基础，不是 L2 graph/A*；不要把该 sample 解释为长距离可达性承诺。

### L2：静态路径图 / waypoint corridor

L2 才开始做长距离绕行。它应使用 CharacterTest runtime / level authoring 生成的 noEngine 数据，不使用 Unity NavMesh：

- 在 session build 或低频 level apply 阶段构建 `CharacterTestNavigationGraph`。
- 节点使用稳定 id、position、surface/cost metadata、clearance。
- 边使用稳定 id、from/to、length、required clearance、blocked layer mask、optional area tags。
- 图数据来自 CharacterTest level authoring、CombatLevel static colliders、手工 waypoint/portal 或后续 editor bake；不能在 Tick 内扫描 Unity scene。
- Runtime query 用 A* 或等价有界搜索，排序 key 必须稳定：total cost、heuristic、node id / edge id。
- path result 只输出 caller-owned path buffer、next waypoint、remaining distance、status、replan reason。

L2 不改变 motion authority。它只把 `MoveToTarget` 的目标从“最终目标中心”改成“当前 path segment / next waypoint”。真正是否能走过去仍由 `CharacterTestCharacterMotionSystem` 和 CombatPhysics collision 决定。

### L3：动态阻挡和重规划

动态角色、可移动自然物、临时危险区不应该每帧重建全局图。首版策略：

- 静态图负责大方向连通性。
- `CharacterTestPerceptionFrameCache.TryGetEnvironment` 和 CombatPhysics probe 负责当前帧局部避障。
- 动态阻挡导致 path segment blocked 时，先尝试 L1 candidate steering；连续失败达到阈值后才触发 L2 replan。
- replan 预算按 frame 分摊：每帧最大 agent 数、每个 query 最大 node expansion、超预算时沿旧 path 或 fallback stop。
- 危险区首版作为 cost / avoidance hint，不作为硬阻挡；只有设计明确的禁行区域才进入 hard block。

## 建议的内部类型

首版类型保持 CharacterTest internal，不进入 `MxFramework.AI` 公共 API。

```csharp
internal readonly struct CharacterTestNavigationQuery
{
    RuntimeFrame Frame;
    int SourceEntityId;
    int TargetEntityId;
    FixVector3 SourceCenter;
    FixVector3 TargetCenter;
    int ProfileKey;
    int MaxNodeExpansions;
    int LayerMask;
}
```

```csharp
internal readonly struct CharacterTestPathfindingSample
{
    RuntimeFrame Frame;
    int SourceEntityId;
    int TargetEntityId;
    CharacterTestPathStatus Status;
    FixVector3 NextWaypoint;
    FixVector3 DesiredDirection;
    Fix64 RemainingDistance;
    bool DirectPathClear;
    int SelectedCandidateIndex;
    int ExpandedNodeCount;
    int HitCount;
}
```

这些类型只表达数据形状。真正实现时应根据现有 `CharacterTestAiNavigationResult` 和 `CharacterTestPerceptionFrameCache` 选择最小增量，不为了文档名字强行新增重复类型。

## Runtime AI Planner 集成规则

- `Sense` 阶段读取 path / navigation sample，投影到 facts：`move.pathAvailable`、`move.directPathClear`、`move.candidateIndex`，后续可追加 `move.pathStatus`、`move.nextWaypointDistance`、`move.replanReason`。
- `MoveToTarget`、`Strafe`、`Retreat` 仍由 action kind 和 movement settings 决定，不由寻路服务选择行为。
- Utility score/cost、strafe side、reaction delay、direction hold 仍归 Runtime AI Planner agent state。
- 寻路失败时，Runtime AI Planner 应能区分：目标不存在、目标不可达、暂时被动态物挡住、预算不足、profile 配置无效。
- profile schema 首版不变；如果后续需要 `navigation` 区，必须升级 schema version、补默认值、validator warning 和旧 profile 兼容测试。

## 性能和分配约束

寻路位于 `PreSimulation` / Tick 高频路径，默认规则：

- warmed 后 `NoAlloc after initialization`。
- open set、closed set、node records、path buffer、query results 都由 service 或 caller 预分配。
- 禁止 LINQ、临时集合、每帧字符串、异常控制流、反射、Unity 对象查找。
- A* / graph query 必须有硬预算：max nodes、max path length、max queries per frame。
- query key 必须包含 frame、source、target、profile key、graph revision / physics revision 中会影响结果的字段。
- 多 agent 共享静态 graph 和当前帧 perception cache；不要为每个 agent 复制 graph。
- dynamic obstacle 先走局部 steering；只有连续失败或 topology 变化才触发昂贵 replan。

## Debug 和可视化

首版 debug 以数据为主，不依赖场景 gizmo 才能验证：

- 每个 agent 输出 path status、target id、next waypoint、directPathClear、selected candidate、expanded node count、query count、blocked reason。
- Play Monitor 可以显示最近一次 path sample 和 replan reason。
- Debug/HUD 只读消费 sample；不得调用寻路服务改变 path state。
- Editor 可视化后续再做，必须从 runtime snapshot/sample 读取，不直接用 Unity NavMesh 或场景 gizmo 作为权威。

## 测试计划

实现时至少补这些 focused tests：

- Direct path clear：行为与现有 direct movement 保持一致。
- Same-frame reuse：同帧同 source/target/profile 的 navigation/path query 只执行一次。
- Next-frame refresh：障碍或目标移动后一帧刷新 path sample。
- Stable tie-break：等价节点/边/候选角按稳定 id 或配置顺序排序。
- Blocked fallback：局部候选角可绕过障碍时输出候选；不可绕过时输出 `PathBlocked` / zero move。
- Attack range：进入 attack range 时不继续追到目标中心。
- Dynamic obstacle：动态阻挡先局部避障，连续失败才 replan。
- Hazard cost：危险区提高 cost 但不默认 hard block。
- Budget cap：超过 node expansion 预算时返回可诊断状态，不分配、不抛异常。
- Warmed allocation：`BeginFrame + repeated path queries` 断言 0 managed allocation。
- Runtime AI Planner 回归：visible/blocked/hazard、MoveToTarget、Strafe、Retreat 既有行为不被无关改变。

## 实施里程碑

| 里程碑 | 内容 | 验收 |
| --- | --- | --- |
| M0 文档基础 | 本文、索引、Runtime AI Planner guide 链接 | 文档健康检查通过 |
| M1 L1 稳定化 | 已实现 capsule-aware local probe、debug summary、cache key 完整化 | focused EditMode + 0 allocation test |
| M2 静态 graph 数据 | [PF-M2 可执行任务](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M2_STATIC_GRAPH.md)：internal graph DTO、session build 输入、稳定排序/hash | 纯 C# graph tests |
| M3 A* path sample | [PF-M3 可执行任务](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M3_BOUNDED_ASTAR.md)：有界 A*、caller-owned path buffer、next waypoint output | unreachable / detour / budget tests |
| M4 Runtime AI Planner 接入 | [PF-M4 可执行任务](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_PLANNER_INTEGRATION.md)：facts、movement output、debug monitor sample | Runtime AI Planner 回归 tests |
| M5 可视化和调参 | Play Monitor / Debug UI 显示，必要时 editor-only preview | 不改变 runtime authority |

### M2-M4 工作包状态

本表是 Pathfinding Foundation M2-M4 的状态权威；任务页只记录实际交付证据，不复制状态。

| 工作包 | 状态 | 前置 | 交付边界 |
| --- | --- | --- | --- |
| PF-M2 | `Verified` | M1 `Verified` | 不可变静态图、稳定 revision/hash、低频 build 输入和 NoAlloc 读取 |
| PF-M3 | `Verified` | PF-M2 `Verified` | 确定性有界 A*、caller-owned corridor、typed status 和预算 |
| PF-M4 | `Verified` | PF-M3 `Verified` | Perception/Planner 接入、facts、movement、诊断和回归证据 |

状态流转统一为 `Planned -> In Progress -> Implementation Complete -> Verified`。只有当前工作包的源码、focused tests、Unity refresh/compile、Console 0 error、性能证据和接口回写全部闭合后，才可进入 `Verified`。

### 状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 |
| --- | --- | --- | --- | --- |
| 2026-08-18 | PF-M2 | `Planned` | `In Progress` | 冻结静态图合同并开始 internal graph DTO/builder/focused tests 实现。 |
| 2026-08-18 | PF-M2 | `In Progress` | `Verified` | immutable canonical graph、typed validation、revision/content hash、NoAlloc lookup/nearest/outgoing 已落地；focused EditMode 17/17，Unity 完整脚本验证与 Console 0 error 通过。 |
| 2026-08-18 | PF-M3 | `Planned` | `In Progress` | 开始完整确定性有界 A*、caller-owned corridor、typed failure/budget 与 NoAlloc 测试实现。 |
| 2026-08-18 | PF-M3 | `In Progress` | `Verified` | admissible/consistent heuristic 约束、稳定 heap/parent tie-break、typed failure、caller-owned corridor、预算和 0 B warmed query 已落地；focused EditMode 32/32，独立 Dijkstra oracle、Unity 完整脚本验证与 Console 0 error 通过。 |
| 2026-08-18 | PF-M4 | `Planned` | `Planned` | 将 Planner 接入拆为 T0 审计、T1 graph/session、T2 path perception、T3 Planner/diagnostics、T4 acceptance 五个独立子任务；尚未修改生产接入。 |
| 2026-08-18 | PF-M4 | `Planned` | `In Progress` | T0 已冻结正式 region/static CombatPhysics grid 输入、session-shared path runtime、cache witness、L2/L1 顺序及测试/NoAlloc 合同；开始 T1。 |
| 2026-08-18 | PF-M4 | `In Progress` | `Verified` | T0～T4 5/5 Verified；正式 scene graph/session、deterministic A* + L1 segment fallback、Planner movement/Facts/diagnostics、Game View 绕障、36/36 + 9/9 + 18/18、Unity Console 0 与 warmed 0 B 全部闭合。 |

## 明确不做

- 不使用 Unity NavMesh、NavMeshAgent、PhysX 或 GameObject transform 作为权威寻路。
- 不新增框架级 `MxFramework.AI` pathfinding API，除非 CharacterTest slice 稳定并出现第二个框架消费者。
- 不做 crowd simulation、RVO、复杂队形、完整动态 navmesh rebuild。
- 不把 path result 写进 SaveState / network / replay/hash，除非它成为影响未来命令输出的跨帧权威状态并另行设计。
- 不在 Runtime AI Planner JSON schema 中偷偷新增 navigation 字段。
