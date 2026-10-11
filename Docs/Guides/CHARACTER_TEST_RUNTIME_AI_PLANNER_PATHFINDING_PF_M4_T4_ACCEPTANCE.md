# PF-M4-T4：Playable、NoAlloc 与收口

> Status: Guide
>
> 状态权威：[PF-M4 总控页第 3 节](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_PF_M4_PLANNER_INTEGRATION.md)
>
> 前置：PF-M4-T3 `Verified`

## 1. 可交付结果

用正式 CharacterTest/Runtime AI Lab fixture 闭合 PF-M4 的可玩、性能、回归、文档和交付门禁，并形成单独可追踪提交。

## 2. 验收清单

- [x] 正式关卡 fixture 包含 direct、静态 detour、unreachable、dynamic segment obstacle 和恢复场景。
- [x] 直接 Game View/PlayMode framebuffer 显示 path status、graph identity、next waypoint、remaining distance/total cost、expanded/query count、blocker/fallback reason。
- [x] AI 实际沿 corridor 绕行；动态阻挡先 L1 steering；命令仍经过 Character Motion/CombatPhysics。
- [x] 合并 focused tests、受影响 Planner/Attack Opportunity 回归、Unity 完整脚本验证和 Console 0 error 全通过。
- [x] warmed `BeginFrame + navigation/path + Planner Tick` 计量窗口为 0 B GC allocation。
- [x] AI、Combat、Runtime/CharacterTest、Pathfinding Foundation 按实际合同回写。
- [x] 独立审查 P0/P1 为 0；准确记录没有 production network server、在线权威或分布式 transport 的证据。

## 3. 提交与推送 Gate

- exact stage 只包含 PF-M4 相关源码、fixture、测试、证据和文档。
- 提交后 fetch/rebase 最新 `origin/main`，重新执行 Unity validate 与 focused tests。
- push 后确认 `HEAD...origin/main = 0 0`、工作区干净且不动用户 stash/无关状态。

## 4. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main` PF-M4 独立收口工作树；提交、rebase、复证和 push 见总控页交付记录 |
| focused / 回归 | graph/session/perception `cbe124437c384386a2fb1b83b8eaad05` 36/36；Planner path `ed93536b99d340b28f4b0d3ab75e3040` 9/9；受影响旧行为 `749d7a448fa54fdf8be8fb1148db608c` 18/18 |
| Unity / Playable | 脚本验证 `4a133243498e4934bb6c44674df4a6a7`：compile/domain reload/editor ready/Console 0 error；正式 CharacterTest 障碍 fixture 在 208 帧到 `(1.411,-4.909)`，408 帧到 `(7.089,-1.357)`，最大侧向绕行 `4.909 m`，由 `PathWaypoint` 恢复为 `Direct` |
| Game View 证据 | direct Facts：`Artifacts/CharacterTest/Pathfinding/PF-M4/pf-m4-runtime-ai-lab-facts-navigation-final.png`；静态绕障 Facts：`Artifacts/CharacterTest/Pathfinding/PF-M4/pf-m4-runtime-ai-lab-agent1-path-waypoint-diagnostic-proof.png` |
| NoAlloc | session/path query 与 `BeginFrame + navigation/path + Planner Tick` 两个 warmed 窗口均实测 0 B |
| 接口回写 / 终审 | AI、Combat、Runtime、Capabilities、Pathfinding Foundation 与 Character Distance 台账已按实际合同回写；终审未发现 P0/P1 |
| production/network 边界 | 只证明 CharacterTest local-offline 与 Runtime AI Lab loopback；没有 production network server、在线身份/反作弊权威或分布式 transport 一致性证明 |
