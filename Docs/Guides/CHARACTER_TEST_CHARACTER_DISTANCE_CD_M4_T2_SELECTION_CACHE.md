# CD-M4-T2：联合归约、两层缓存与稳定选择

> Status: Guide
>
> 状态权威：[CD-M4 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md)
>
> 前置：CD-M4-T1 `Verified`

## 1. 可交付结果

对有限 `(target, action, zone)` 候选复用 M1～M3 evaluator/Anchor/path 结果，按冻结顺序确定唯一选择，并以两层有界 cache 降低重复 Trace Plan 和归约成本。

## 2. 实现清单

- [x] Trace Plan cache key 覆盖 source/action/pose/snapshot 及所有 action/geometry/socket/root-motion revision。
- [x] result cache key 覆盖 source/target/action/mode/Zone/Anchor/snapshot/lock/reservation/graph/physics witness。
- [x] cache 固定容量、deterministic replacement/frame reset；stale/overflow typed diagnostics，不隐式复用旧值。
- [x] evaluator-backed candidate 通过只读 Preview 接口复用 CurrentPose、TurnInPlace、CandidatePose/path；compatibility candidate 走独立逻辑。
- [x] 完整排序按总控页冻结链逐字段比较；无浮点权威比较，无容器顺序 tie。
- [x] 输出稳定选择及其 opportunity、Zone/Anchor、utility、path cost、earliest tick、compatibility/truncation/cache 诊断；T3 将其接入 Planner/overlay。

## 3. 自动化测试

- [x] 短剑不可达、长枪可达；当前可攻击优先于更高 root-near 但需接近候选。
- [x] equal score 完整 stable-ID tie；候选、broadphase、cache fill 顺序扰动不变。
- [x] cache hit/miss 选择和 evidence 完全一致；各 revision/mode/Zone/Anchor/physics 逐一失效。
- [x] special compatibility 不伪报 evaluator/Zone/path result。
- [x] budget failure/truncation 与 `Unreachable`/`Valid no hit` 区分。
- [x] warmed reduction + two-level cache 为 0 B GC allocation。

## 4. 完成 Gate

- focused tests、M1～M3/PF-M4 受影响回归、Unity validate 和 Console 0 error 全通过。
- 完整 comparator 有直接边界测试，cache 不改变语义。
- 实际记录包含 cache capacities/hit counts/NoAlloc 和限制。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main`；T2 独立实现提交号待提交后补记 |
| focused / 回归 | 2026-08-20：T1/T2 + M1 Adapter + M3 authority focused `47/47`；M1～M4/PF-M4 A*/Planner path 受影响回归 `132/132`；Unity validate job `2aacd46e34544266a64dfccd5e78d47c` passed、Console 0 error |
| cache evidence | Trace Plan `128`、result `512` 默认容量（hard cap `512/2048`）；两 action × 两 target 冷态 `4` 次求值、`2` 次 Trace Plan 构建，暖态 `4` 次 result hit、`0` 次求值/构建；key witness 逐字段失效测试通过 |
| NoAlloc | warmed reducer + 两层 cache `128` 次循环，当前线程 `0 B` |
| 已知限制 | T3 前 selector 尚未接 Planner command/overlay；全 `CharacterTestRuntimeAiPlannerFormalizationTests` 宽泛扫描为 `280/298`，18 个失败集中在既有 behavior-card/UI/profile 预期，未命中本工作包与受影响路径，保留到最终跨里程碑审计，不作为 T2 全量回归通过 |
