# CD-M3-T3：路径代价与稳定 Anchor 选择

> Status: Guide
>
> 状态权威：[CD-M3 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md)
>
> 前置：CD-M3-T2 `Verified`

## 1. 可交付结果

对合法 CandidatePose 复用 PF-M4 计算 `AttackPosePathCost`，结合容量可用性与稳定决胜选择 Anchor，并输出可解释但无副作用的选择结果。

## 2. 实现清单

- [x] path query 复用 PF-M4 graph/pathfinder/cache contract，绑定 graph revision/hash、clearance、budget 和 physics witness。
- [x] 区分 success、unreachable、budget exceeded、stale、capacity full；预算失败不伪报不可达。
- [x] cost 使用 PF-M3 penalty-inclusive total cost；root distance 仅软诊断，不替代 path cost。
- [x] 先过滤非法/不可站立/不可攻击/满容量，再按 Zone lock、作者优先级、path cost、earliest tick、Zone/Hurtbox/Anchor stable ID 决胜。
- [x] selection 自身无副作用；预约写入只由 T4 authority 执行。
- [x] diagnostics 保存 typed 过滤原因、path cost/next waypoint、selected Anchor 与稳定决胜结果。

## 3. 自动化测试

- [x] nearer-root/unreachable 与 farther-root/reachable 正确选择。
- [x] 等 cost、输入顺序和 graph adjacency 扰动后 stable selection 不变。
- [x] capacity available/full、budget exceeded、graph/Zone/Anchor stale 精确诊断。
- [x] path 重算选择一致；不残留旧 candidate/path sample。
- [x] warmed multi-anchor CandidatePose + path cost + selection 为 0 B GC allocation。

## 4. 完成 Gate

- focused tests、PF-M4 受影响回归、Unity validate 和 Console 0 error 通过。
- 选择排序和每个 failure 的非权威/权威边界回写 AI/Pathfinding 文档。
- 实际记录包含预算、候选容量和 NoAlloc 数字。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；`Verified` |
| 实际分支 / 提交 | `main`；随 CD-M3 里程碑提交 |
| focused / 回归 | M3 13/13；PF/距离/诊断合并 163/163 |
| path/selection diagnostics | Game View 显示 `anchor.head.front`、9709 mm、next waypoint 与 typed result |
| NoAlloc | warmed projection/standing/path/selection 0 B |
| 已知限制 | path sample 为当前帧派生，不进入 snapshot/hash |
