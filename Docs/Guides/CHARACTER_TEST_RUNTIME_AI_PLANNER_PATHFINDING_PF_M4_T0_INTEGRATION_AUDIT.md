# PF-M4-T0：接入合同与现状审计

> Status: Guide
>
> 状态权威：[PF-M4 总控页第 3 节](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_PLANNER_INTEGRATION.md)
>
> 前置：PF-M3 `Verified`

## 1. 可交付结果

只读核对现有 L1 navigation、PerceptionFrameCache、Runtime AI Planner、CharacterTest level/session build、CombatPhysics revision 和 Runtime AI Lab diagnostics，冻结 PF-M4 唯一接入方案。该任务不修改生产代码。

## 2. 执行清单

- [x] 记录 `ICharacterTestAiNavigationService`、`CharacterTestCombatPhysicsNavigationService`、`CharacterTestPerceptionFrameCache` 的当前调用链、cache key 和 allocation ownership。
- [x] 定位正式 `GameManager.WorldObjectSceneCache -> CharacterTestCombatLevelSceneAdapter -> CharacterTestCharacterPhysicsRuntime` 关卡链，并确定 graph authoring/source DTO 的唯一落点。
- [x] 核对 PF-M2 graph/PF-M3 pathfinder 的实例所有者、初始化时机、失效时机和 unavailable fallback。
- [x] 列出可用于 cache witness 的 RuntimeFrame、graph revision/hash、CombatPhysics topology/body revision；不得用含糊的“当前场景没变”。
- [x] 冻结 direct path、L2 corridor、current-segment capsule、dynamic L1 fallback 的执行顺序和 typed reason。
- [x] 核对 Planner facts、MoveToTarget/Strafe/Retreat 消费点与 Runtime AI Lab overlay 只读入口。
- [x] 给出精确影响文件、focused tests、PlayMode fixture、NoAlloc 计量窗口和非目标表，无 `TBD`。

## 3. 必须冻结的决策

- graph 来源是否为显式 level navigation DTO、从 static collider 确定性生成，或二者组合；禁止 Tick 内场景扫描和 Unity NavMesh。
- graph absent/invalid/stale 时是 typed L1 fallback 还是 stop；不能静默伪报 L2 success。
- `ActiveBlockedLayerMask` 如何从当前帧 CombatPhysics obstruction 派生；不得把 layer mask 当作 topology revision。
- `CharacterTestAiNavigationResult` 是否兼容扩展，还是只在 internal sample 扩展 path fields；公共行为不得破坏既有调用方。
- path sample 是当前帧派生数据，不进入 SaveState/network/hash；若发现必须增加跨帧 state，停止并先修订合同。

## 4. 完成 Gate

- 审计表覆盖 authoring/session、L1/L2、cache、Planner、diagnostics、tests 和 performance。
- 每个关键字段都有现有来源或明确新增位置，不用计划类型冒充已存在 API。
- 影响文件与测试矩阵可直接交给 T1～T4 执行。
- 总控页状态与实际记录更新后，T0 才可 `Verified`。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；`Verified` |
| 审计结论 | 现有 L1 可以在不改变 HFSM、Utility、命令管线、角色运动权威及 snapshot/hash 的前提下组合 L2；PF-M4 使用正式 scene cache 区域边界确定性生成静态 grid graph，失败时显式退回 L1。 |
| 影响文件 | 已冻结，见第 6 节 |
| 测试矩阵 | 已冻结，见第 7 节 |
| 限制 | 本任务只冻结合同，不提供生产能力；PF-M4 不承诺生产网络部署、跨帧 corridor、动态 navmesh、crowd/RVO 或危险区全局代价。 |

## 6. 冻结接入方案

### 6.1 当前调用链与所有权

| 层 | 当前事实 | PF-M4 决策 |
| --- | --- | --- |
| Planner | `CharacterTestRuntimeAiPlannerEvaluator` 在 Sense 中调用 `ICharacterTestAiNavigationService.TryResolveMove`，动作最终仍通过 `SetControlIntent` 进入 Character Motion。 | 不改变动作选择或 motion authority；L2 只改变返回的当前帧 move suggestion。 |
| L1 service | `CharacterTestSliceCompositionBuilder` 在 physics ready 时创建唯一 `CharacterTestCombatPhysicsNavigationService`；service 持有 profile 副本和 fallback query list。 | 构造时额外注入一个 session-shared navigation runtime；公开 `CharacterTestAiNavigationResult` 构造与既有字段保持兼容。 |
| frame cache | `CharacterTestPerceptionFrameCache.BeginFrame` 清空并复用 lists；当前 navigation key 覆盖 source/target、attack range、profile、probe、shape、mask、clearance、candidate count。 | cache 持有最终 navigation sample；新增 graph identity、path budget 和 `CombatPhysicsWorld.Revision` witness。缓存只活一帧。 |
| PF-M2/PF-M3 | graph immutable；pathfinder 持有预分配 scratch；path buffer 由 caller 持有。 | 新 session runtime 唯一持有 graph、一个顺序执行的 bounded A* 和一个预分配 path buffer；多 agent 在单线程 simulation 中共享，不复制 topology，不保留 corridor。 |

### 6.2 正式 graph 输入与生命周期

- 唯一 authoring/session 落点是 `GameManager` 的序列化 `CharacterTestNavigationSceneSettings`，边界数据来自正式 `GameManager.WorldObjectSceneCache.RegionInstances`，静态可通行性来自同一次 session 构建得到的 `CharacterTestCharacterPhysicsRuntime`。
- settings 明确保存 `enabled`、`revision`、bounds region stable id、root plane Y、cell size、4/8 邻接、node/edge hard cap、endpoint snap、expansion 和 path-buffer budget。空值或 `*` 表示对全部 enabled formal regions 做确定性 union；其他值必须唯一匹配，否则 typed failure。
- `CharacterTestNavigationGridGraphBuilder` 仅在 `CharacterTestRuntimeSessionBuilder` 启动/重建低频路径读取 region transforms。它把 bounds 量化到 world-aligned grid，以 grid ordinal 生成稳定 node/edge id，用 static CombatPhysics capsule 排除占用节点和阻断边，再交给 PF-M2 canonical builder 生成 revision/hash。
- Tick 不枚举 GameObject、Transform、Collider 或 Unity NavMesh。session dispose/rebuild 丢弃旧 runtime 引用并创建新实例；相同 authoring 可保留相同 revision/hash，内容变化必须改变 content hash，作者语义变更同时递增 settings revision。
- absent、disabled、invalid settings/bounds、capacity、empty graph 或 PF-M2 validation failure 都产生 `CharacterTestNavigationSessionBuildStatus`，注入一个 unavailable runtime，并使用带原因的 L1 fallback；不得伪报 L2 success。

### 6.3 Cache witness 与执行顺序

- cache witness 固定为：当前 `RuntimeFrame` 生命周期、source/target 与 profile/clearance、`graph.Revision`、`graph.ContentHash`、endpoint/expansion/path-node budgets，以及公开的 `CombatPhysicsWorld.Revision`。`TopologyRevision` 可用于诊断；`BodyRevision` 为 Combat assembly internal，PF-M4 不跨 assembly 读取它。
- 生成 grid edge 的 `BlockedLayerMask` 固定为 `0`。动态角色/可移动物只通过当前 segment 的 CombatPhysics query 参与；不得把“存在某动态层”映射成整张图所有 edge 均 blocked。
- 执行顺序冻结为：attack-range/零距离 -> direct capsule -> direct blocked 时 bounded A* -> success 后 current next-waypoint segment capsule -> segment clear 则朝 waypoint -> segment blocked 时按既有候选角做 L1 steering -> L2 不可用/失败时按既有 direct-target L1 退化。
- sample 新增 `PathAttempted`、`PathAvailable`、typed path status、graph revision/hash、next waypoint、remaining distance、total cost、expanded count、path query count、physics revision、resolution kind 和 fallback reason。失败路径每次完整覆盖 sample，不能残留旧 success。
- typed fallback reason 至少区分：graph disabled/unavailable/invalid/stale、source/target outside、unreachable、budget exceeded、buffer too small、segment blocked、L1 exhausted。`BudgetExceeded` 不等于 `Unreachable`。

### 6.4 Planner 与 diagnostics

- 既有 facts `move.pathAvailable`、`move.directPathClear`、`move.candidateIndex` 保持稳定：可攻击或存在非零移动建议即 path available；PF-M4 不偷改 Runtime AI Planner JSON schema。
- `MoveToTarget`、`Strafe`、`Retreat` 继续消费 evaluator 中的 navigation move；PrimaryAttack/attack opportunity 仍使用目标几何与 CD-M1/CD-M2 guard，不以 corridor distance 替代攻击距离。
- `CharacterTestPerceptionFrameCache` 增加按 source/target 读取“已存在 sample”的只读入口；Runtime AI Lab/Play Monitor 只从 cache/snapshot 投影 graph identity、status、waypoint、cost、expanded/query count、blocker 和 fallback reason，不发起 query、不推进 frame。

## 7. 影响文件与验证矩阵

| 子任务 | 生产文件 | 自动化与证据 |
| --- | --- | --- |
| T1 | `Composition/GameManager.cs`、`CharacterTestRuntimeSessionBuilder.cs`、`CharacterTestSliceOptions.cs`、网络 session option 透传；新增 `Runtime/Systems/CharacterTestNavigationSession.cs` | 新增 `CharacterTestNavigationSessionTests.cs`：正式 cache fixture、bounds 选择、顺序扰动、stable id/revision/hash、invalid/absent/cap、单一 owner/reset；Unity compile/Console 0。 |
| T2 | `CharacterTestAiNavigationService.cs`、`CharacterTestPerceptionFrameCache.cs`、composition builder | 扩展 `CharacterTestIntegrationContractTests.PerceptionFrameCache.cs`：direct/detour/unreachable/outside/budget/stale、same-frame reuse、next-frame physics revision、dynamic L1、顺序扰动、warmed 0 B。 |
| T3 | Planner facts/decision snapshot 与 Runtime AI Lab runtime bridge/view-model/overlay 的最小实际消费文件 | `CharacterTestRuntimeAiPlannerFormalizationTests.EvaluatorAndPerception.cs`、`DiagnosticsAndScenePreview.cs`：动作回归、facts、只读 overlay identity/query count、Planner Tick 0 B。 |
| T4 | 正式 Runtime AI Lab/CharacterTest scene settings 与必要 fixture；AI/Combat/Runtime/CharacterTest/台账文档 | focused + affected regression；Unity refresh/compile/domain reload/editor ready/Console 0；直接 PlayMode/Game View 绕行与诊断截图；256 次 warmed `BeginFrame + repeated navigation + Planner Tick` 0 B。 |

NoAlloc 计量在完成一次完整预热并预扩容 cache 后开始；每轮推进新 `RuntimeFrame`、调用 `BeginFrame`、执行相同容量内的两个相同 navigation 请求并运行一个 Planner Tick。计量不包含低频 graph/session build、Unity overlay 文本刷新或首次集合扩容。

## 8. 明确非目标

- Unity NavMesh/NavMeshAgent、Tick 内 scene scan、动态 graph rebuild、跨帧 corridor/cursor/replan cooldown。
- 把 path sample 写入 SaveState、network snapshot、authority hash 或 replay payload；它由同帧权威输入重算。
- 生产在线 matchmaking、跨机器内容分发/签名、服务端 graph catalog 或 live rollback。PF-M4 只闭合本地/现有确定性 session slice，并记录网络透传与派生边界。
