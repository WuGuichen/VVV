# PF-M4：Perception 与 Runtime AI Planner 路径接入

> Status: Guide
>
> 工作包：Pathfinding Foundation PF-M4
>
> 状态权威：[Pathfinding Foundation 的 M2-M4 工作包状态](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md)
>
> 子任务状态权威：本文第 3 节
>
> 前置：PF-M3 `Verified`

## 1. 任务结果

把 PF-M3 path sample 接入现有 `ICharacterTestAiNavigationService`、`CharacterTestPerceptionFrameCache` 和 Runtime AI Planner，使长距离静态绕行与 L1 局部避障组合工作，并提供可解释、可回归、初始化后 NoAlloc 的运行时证据。

## 2. 公共合同

- session build/level apply 只在低频阶段建立并注入 PF-M2 graph；Tick 不扫描 Unity scene。
- direct path clear 时保留现有 L1 行为；受阻时请求 PF-M3 corridor/next waypoint，再对当前 segment 使用 CombatPhysics capsule probe。
- 同帧 query key 覆盖 frame、source/target/profile、clearance、graph revision/hash、路径预算和会影响 segment 结果的 physics witness。
- sample 暴露 typed path status、next waypoint、remaining distance、total cost、expanded node count、query count 和 blocked/replan reason。
- facts 至少投影 `move.pathAvailable`、`move.directPathClear`、`move.candidateIndex`；行为选择仍归 HFSM + Utility。
- `MoveToTarget`、`Strafe`、`Retreat` 只消费移动建议，不绕过 `SetControlIntent`、Character Motion 或 CombatPhysics authority。
- 图/目标/障碍下一帧变化会刷新 sample；动态阻挡先 L1 steering。本包不保存跨帧 corridor/replan cooldown。
- Debug/Play Monitor 只读显示 sample，不触发 query 或改变状态。

## 3. 子任务状态、依赖与路由

```text
T0 -> T1 -> T2 -> T3 -> T4
```

| 子任务 | 唯一状态 | 执行文档 | 可交付边界 | 前置 |
| --- | --- | --- | --- | --- |
| PF-M4-T0 | `Verified` | [接入合同与现状审计](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_T0_INTEGRATION_AUDIT.md) | 只读链路、graph 输入、cache key、physics witness、诊断与测试矩阵冻结 | PF-M3 Verified |
| PF-M4-T1 | `Verified` | [关卡 Graph 输入与 Session 服务](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_T1_GRAPH_SESSION.md) | 正式低频 graph 构建/注入、revision/hash 生命周期、fail-closed fallback | T0 Verified |
| PF-M4-T2 | `Verified` | [Path Perception Cache 与 L1/L2 组合](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_T2_PATH_PERCEPTION.md) | same-frame cache、A* sample、segment capsule probe、dynamic fallback | T1 Verified |
| PF-M4-T3 | `Verified` | [Planner Movement、Facts 与诊断](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_T3_PLANNER_DIAGNOSTICS.md) | Planner facts/movement、只读 diagnostics/overlay、受影响回归 | T2 Verified |
| PF-M4-T4 | `Verified` | [Playable、NoAlloc 与收口](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_T4_ACCEPTANCE.md) | Game View/PlayMode、NoAlloc、接口回写、终审与独立提交 | T3 Verified |

子任务使用 `Planned -> In Progress -> Implementation Complete -> Verified`。状态只在本表维护；执行页只记录证据。T0 未冻结前不得修改生产接入；T4 `Verified` 前 PF-M4 不得进入 `Verified`。

## 4. 跨子任务验收

| 子任务 | 自动化重点 | Unity / 可玩证据 | 性能 / 权威边界 |
| --- | --- | --- | --- |
| T0 | 审计复核，无生产测试 | 只读 | 明确 graph/physics revision 与派生数据边界 |
| T1 | graph build、identity、invalid/absent input | Unity compile + session fixture | 低频 build；Tick 不扫描场景 |
| T2 | reuse/refresh/stale/direct/detour/unreachable/budget | focused EditMode/PlayMode | warmed cache + query 0 B；无跨帧 path memory |
| T3 | MoveToTarget/Strafe/Retreat/attack 回归、诊断只读 | Runtime AI Lab overlay | movement 仍走 command/motion authority |
| T4 | 合并回归与顺序扰动 | 直接 Game View/PlayMode framebuffer | full-chain warmed 0 B；派生 sample 不入 snapshot/hash |

## 5. 完成 Gate

- T0～T4 全部 `Verified`，每页实际记录完整。
- focused tests、Unity refresh/compile/domain reload、Editor ready、Console 0 error 全部通过。
- direct/detour/unreachable/budget/dynamic fallback 与既有 Planner 行为均有回归证据。
- Runtime AI Lab/CharacterTest 直接 Game View/PlayMode 能观察 path status、next waypoint 和绕行结果。
- warmed `BeginFrame + repeated path queries + Planner Tick` 为 0 B GC allocation。
- AI、Combat、Runtime/CharacterTest 文档按实际合同回写，明确 local-offline 与 production/network 边界。

## 6. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| PF-M4 状态 | 见 Pathfinding Foundation 状态表；当前 `Verified` |
| 子任务进度 | 5/5 Verified |
| 实际分支 / 提交 | `main` PF-M4 独立收口；提交后 fetch/rebase、复证并 push |
| focused / Unity | graph/session/perception 36/36（`cbe124437c384386a2fb1b83b8eaad05`）；Planner path 9/9（`ed93536b99d340b28f4b0d3ab75e3040`）；受影响旧行为 18/18（`749d7a448fa54fdf8be8fb1148db608c`）；脚本验证 `4a133243498e4934bb6c44674df4a6a7`，Console 0 error |
| Playable / Game View | 正式静态墙 fixture：Agent 1 经 `(1.411,-4.909)` 绕行并到达 `(7.089,-1.357)`；Facts framebuffer 显示 `PathWaypoint/Success`、`WP (0,0,-6)`、cost、expanded/query 与 graph identity |
| NoAlloc | graph/session query、warmed `BeginFrame + 32 forced path queries`、warmed `BeginFrame + navigation/path + Planner Tick` 均为 0 B |
| 接口回写 | AI、Combat、Runtime、Capabilities、Pathfinding Foundation 与 Character Distance 台账已回写 |
| 已知限制 | 不保存跨帧 corridor/replan cooldown；sample/diagnostics 为派生数据且不进入 authority snapshot/hash；无 production server/分布式 transport 证明 |

## 7. 子任务状态历史

| 日期 | 子任务 | 原状态 | 新状态 | 证据摘要 |
| --- | --- | --- | --- | --- |
| 2026-08-18 | T0～T4 | 初始化 | `Planned` | 将 PF-M4 拆成五个可独立执行和验收的子任务；尚未修改生产接入。 |
| 2026-08-18 | T0 | `Planned` | `Verified` | 冻结 formal region + static CombatPhysics grid graph、session-shared owner、revision/hash/physics witness、L2/L1 顺序、只读 diagnostics、测试和 NoAlloc 窗口。 |
| 2026-08-18 | T1 | `Planned` | `In Progress` | 开始实现低频 graph 构建、typed session status、唯一 runtime owner 与 composition/network 透传。 |
| 2026-08-18 | T1 | `In Progress` | `Implementation Complete` | 双正式场景 region 输入、static CombatPhysics grid 编译、session graph/pathfinder/path-buffer 唯一 owner、revision/hash 与 typed failure 已实现。 |
| 2026-08-18 | T1 | `Implementation Complete` | `Verified` | formal scene fixture、输入顺序/容量/失效/重建/共享 owner/NoAlloc 覆盖；与 T2 合并 focused EditMode 21/21，Unity compile/domain reload 和 Console 0 error。 |
| 2026-08-18 | T2 | `Planned` | `In Progress` | 开始将 direct probe、A* waypoint segment 与 L1 candidate fallback 组合进同帧 cache。 |
| 2026-08-18 | T2 | `In Progress` | `Implementation Complete` | typed path detail、完整 cache witness、只读读取、dynamic segment fallback 与无跨帧 corridor 已实现。 |
| 2026-08-18 | T2 | `Implementation Complete` | `Verified` | direct/detour/unreachable/budget/outside/unavailable、graph/budget/physics refresh、动态阻挡恢复、broadphase 插入顺序与 warmed 0 B 均通过；Unity focused 9/9。 |
| 2026-08-18 | T3 | `Planned` | `In Progress` | 开始 Planner movement/facts 与只读 Runtime AI Lab diagnostics 接线。 |
| 2026-08-18 | T3 | `In Progress` | `Implementation Complete` | MoveToTarget/Strafe/Retreat 消费统一 navigation sample；Facts、Play Monitor 与 Runtime AI Lab 只读显示 typed path detail。 |
| 2026-08-18 | T3 | `Implementation Complete` | `Verified` | Planner path 9/9、受影响旧行为 18/18、warmed Planner 0 B；overlay 读取不增加 navigation/path query。 |
| 2026-08-18 | T4 | `Planned` | `In Progress` | 进入正式 scene fixture、Playable、合并回归、接口与交付收口。 |
| 2026-08-18 | T4 | `In Progress` | `Implementation Complete` | 补齐全 playable character 最大 clearance、修复 snapped-source waypoint 回走，并完成静态绕障轨迹与 Game View Facts 证据。 |
| 2026-08-18 | T4 | `Implementation Complete` | `Verified` | 36/36 + 9/9 + 18/18、Unity Console 0、两个 warmed 0 B 窗口、接口回写和 P0/P1=0 终审闭合。 |
