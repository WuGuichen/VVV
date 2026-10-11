# PF-M4-T2：Path Perception Cache 与 L1/L2 组合

> Status: Guide
>
> 状态权威：[PF-M4 总控页第 3 节](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_PLANNER_INTEGRATION.md)
>
> 前置：PF-M4-T1 `Verified`

## 1. 可交付结果

把 PF-M3 path query 纳入同帧 Perception cache，并将 direct capsule、L2 next waypoint segment 和既有 L1 candidate steering 组合成一个确定性导航 sample。

## 2. 实现清单

- [x] 扩展 internal navigation query/sample，保留既有 `CharacterTestAiNavigationResult` 兼容调用。
- [x] query key 覆盖 frame、source/target、attack range/profile、clearance、graph revision/hash、预算和 T0 冻结的 physics witness。
- [x] direct capsule clear 时保留旧 direct 结果，不运行不必要的 A*。
- [x] direct blocked 时执行 PF-M3；成功后只对当前 next-waypoint segment 做 capsule probe。
- [x] segment 被动态物阻挡时使用既有 L1 candidate steering；不得每帧重建 graph 或保存隐式 corridor。
- [x] sample 输出 path status、graph identity、next waypoint、remaining distance、total cost、expanded count、query counts、blocked/replan reason。
- [x] graph unavailable/stale、endpoint outside、unreachable、budget/buffer failure 均可诊断；失败不得留下旧 success sample。

## 3. 自动化测试

- [x] same-frame reuse、next-frame refresh、graph identity/physics witness/profile/clearance/budget key 变化。
- [x] direct、detour、unreachable、budget exceeded、stale、endpoint outside。
- [x] dynamic segment obstacle 走 L1 fallback，障碍移除后下一帧刷新。
- [x] broadphase/query 返回顺序扰动不改变 sample。
- [x] 预热后 `BeginFrame + repeated navigation/path queries` 为 0 B GC allocation。

## 4. 完成 Gate

- focused tests、Unity refresh/compile/domain reload、Console 0 error 通过。
- sample 可供 Planner/diagnostics 只读消费，且没有跨帧 path memory。
- 实际记录包含 query count、NoAlloc 数字和所有 typed failure 覆盖。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main` PF-M4 工作树；随 PF-M4 收口独立提交 |
| 受影响文件 | `CharacterTestPerceptionFrameCache.cs`、`CharacterTestAiNavigationService.cs`、perception contract tests、`CharacterTestNavigationPerceptionTests.cs` |
| focused / Unity | 最终 graph/session/perception 合并任务 `cbe124437c384386a2fb1b83b8eaad05` 36/36；Planner integration `ed93536b99d340b28f4b0d3ab75e3040` 9/9；Console 0 error |
| NoAlloc | warmed `BeginFrame + 32 forced path queries` 实测 0 B；32/32 path success |
| 已知限制 | 不保存跨帧 corridor/replan cooldown；derived sample 不进入 authority snapshot/hash；成功 corridor 的 steering 从首条实际 traversed node 开始，source snap 只计入距离证据，segment 仍由 CombatPhysics capsule probe fail closed |
