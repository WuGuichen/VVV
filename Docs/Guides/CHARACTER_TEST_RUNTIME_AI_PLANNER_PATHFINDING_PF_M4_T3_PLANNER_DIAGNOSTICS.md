# PF-M4-T3：Planner Movement、Facts 与诊断

> Status: Guide
>
> 状态权威：[PF-M4 总控页第 3 节](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_PLANNER_INTEGRATION.md)
>
> 前置：PF-M4-T2 `Verified`

## 1. 可交付结果

让 Runtime AI Planner 的移动动作消费统一 navigation/path sample，并在 Runtime AI Lab/Debug 表面只读解释路径选择，不改变 HFSM/Utility 或角色运动权威。

## 2. 实现清单

- [x] facts 保持 `move.pathAvailable`、`move.directPathClear`、`move.candidateIndex` 的稳定语义；新增字段先核对 schema/compatibility。
- [x] MoveToTarget/Strafe/Retreat 使用 sample move/next waypoint，但最终仍通过 `SetControlIntent`。
- [x] attack range 与 CD-M1/CD-M2 Attack Opportunity guard 不被 path distance 反写。
- [x] path failure 按冻结 fallback/stop 规则处理，不把 `BudgetExceeded` 或 `CandidateSetTruncated` 当成 `Unreachable`。
- [x] diagnostics/Play Monitor 展示 graph identity、status、waypoint、cost、expanded/query count、segment blocker 和 fallback reason。
- [x] diagnostics 只读缓存 sample，不调用 service、不推进 frame、不修改 Planner state。

## 3. 自动化测试

- [x] MoveToTarget、Strafe、Retreat direct/detour/failure 命令输出。
- [x] PrimaryAttack/TurnInPlace/Zone lock 与已有行为回归。
- [x] facts 在 sample success/failure/next-frame refresh 时正确写入或移除，不残留旧值。
- [x] overlay snapshot 与 runtime sample identity 一致，读取不增加 query count。
- [x] warmed Planner Tick 含 path sample 消费为 0 B GC allocation。

## 4. 完成 Gate

- focused Planner/command/overlay tests 与受影响旧回归全通过。
- Unity 完整脚本验证和 Console 0 error 通过。
- diagnostics 的字段和非权威边界回写 AI/Runtime 文档。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main` PF-M4 独立收口工作树；随 PF-M4 提交 |
| focused / 回归 | Planner path integration Unity EditMode `ed93536b99d340b28f4b0d3ab75e3040` 9/9；受影响 movement/perception/command 回归 `749d7a448fa54fdf8be8fb1148db608c` 18/18 |
| overlay | Runtime AI Lab Facts 与 Play Monitor 从 Planner 缓存只读投影；Game View `Artifacts/CharacterTest/Pathfinding/PF-M4/pf-m4-runtime-ai-lab-agent1-path-waypoint-diagnostic-proof.png` 直接显示 `PathWaypoint/Success`、graph identity、WP、cost、expanded/query 与 fallback 摘要 |
| NoAlloc | `PathfindingPlanner_WarmedPlannerTickWithPathSampleDoesNotAllocate`：预热后 32 帧 `BeginFrame + navigation/path + Planner evaluate` 实测 0 B |
| 已知限制 | navigation diagnostics 是可重算派生数据，不进入 authority snapshot/hash；当前只验证 CharacterTest local-offline/loopback，没有 production server 或分布式 transport 权威证据 |
