# CD-M4-T5：Playable、性能与最终收口

> Status: Guide
>
> 状态权威：[CD-M4 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md)
>
> 前置：CD-M4-T4 `Verified`

## 1. 可交付结果

闭合有限联合选择的功能、可玩、性能、回归、文档和交付门禁，并完成整个角色距离路线的最终验收。

## 2. 验收清单

- [x] 直接 Game View/PlayMode 覆盖：短剑不可达/长枪可达、多目标不同 Zone/Anchor、远程兼容受阻、TurnInPlace、ApproachPose、budget truncation。
- [x] overlay 显示最终 target/action/zone/anchor、opportunity/mode、utility/path cost/earliest tick、candidate counts、cache、budget/truncation 和原因。
- [x] 顺序扰动、cache hit/miss、rollback（若有新 authority）与下一 tick command parity 回归通过。
- [x] 合并 CD-M0～M3、PF-M4、Planner/Combat/CharacterAction/CharacterApplication/Network 受影响测试全通过。
- [x] Unity refresh/compile/domain reload、Editor ready、Console 0 error 通过。
- [x] warmed full joint selection + Planner Tick + diagnostics 为 0 B；低频 snapshot 仍单列 `LowFreqAlloc`。
- [x] AI、Combat、CharacterAction、CharacterApplication、Network、总台账/CAPABILITIES 按实际能力回写。
- [x] 独立终审 P0/P1=0，明确 local-offline 与 production/online 未覆盖边界。

## 3. 提交与推送 Gate

- exact stage 只包含 CD-M4 和最终路线收口相关源码、fixture、测试、证据、接口/台账更新。
- 提交后 fetch/rebase 最新 `origin/main`，重新执行 Unity validate、focused/关键回归、校准锁定和文档健康检查。
- push 后确认 `HEAD...origin/main = 0 0`、工作区干净、stash/用户无关状态不变。

## 4. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main`；T0～T4 已推送，T5 收口提交见 Git 历史 |
| focused / 全回归 | CD/PF/Planner 精确 EditMode 199/199、PlayMode 14/14；全 EditMode 程序集完成 7183 项，失败列表仅见缺失 Piloto 索引与既有 Runtime AI Lab 卡数据问题，未见 Character Distance 失败 |
| Unity / Playable / Game View | Unity validate `bf06c03a23a947239174fe7b36ce6308`：compile/domain reload/Editor ready/Console 0；直接 Game View 证据见 `Docs/Evidence/CHARACTER_TEST_CD_M4_T5_GAME_VIEW_JOINT_DIAGNOSTICS_2026-08-20.png`，场景矩阵由 14 项 PlayMode 闭合 |
| 校准 / NoAlloc | T4 6 fixture 校准保持零截断基线；candidate/selector/cache、Planner submit/apply/action guard 与只读 diagnostics warmed 0 B；snapshot capture/apply 仍为 `LowFreqAlloc` |
| authority / replay | joint selection/cache/diagnostics 每 tick 派生且 rollback 清空；最终胜者才提交现有 Zone lock/reservation/command authority；snapshot/hash/rollback/replay 与下一 tick command parity 回归通过 |
| 接口 / 台账 / 终审 | AI/Combat/CharacterAction/CharacterApplication/Network/CAPABILITIES/总台账已按实际能力回写；第二遍本地独立差异与权威审查 P0/P1=0 |
| production/network 边界 | 验收覆盖 local-offline 单进程、formal snapshot/rollback/replay 与 loopback-oriented 合同；dedicated server、公网 transport、多进程负载和大型 raid 容量未验证 |
