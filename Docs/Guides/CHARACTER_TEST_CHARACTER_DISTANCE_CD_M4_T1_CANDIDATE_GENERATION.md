# CD-M4-T1：有限候选生成与保守粗筛

> Status: Guide
>
> 状态权威：[CD-M4 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md)
>
> 前置：CD-M4-T0 `Verified`

## 1. 可交付结果

从 ready/resolvable action 的 conservative aggregate envelope 生成有限 actor/action/zone 候选，以显式 Top-K/query budget 截断并返回 typed diagnostics。

## 2. 实现清单

- [x] ready/resolvable、资源/冷却/状态/action evaluator 支持矩阵按 T0 合同读取，不扫描任意技能目录。
- [x] 对 ready action broadphase 做稳定并集，查询 target damage aggregate bounds；actor 合法性先行。
- [x] 明确相交候选优先于 root-distance soft rank，再按 stable target ID 取得 Top-K。
- [x] 对 actor/action 应用 conservative action envelope，对每个合法 Zone 生成 canonical candidate。
- [x] 特殊技能产生独立 compatibility candidate/source，不填伪造 Zone/evaluator evidence。
- [x] Top-K 或任一 query budget 截断设置 `CandidateSetTruncated`、counts/limit/phase；未截断否定才可表达有限集内无候选。
- [x] 使用预分配 buffers/canonical sort；broadphase/container 顺序不参与选择。

## 3. 自动化测试

- [x] self/dead/friendly/invalid/mask/soft-radius、ready/unready/unsupported/compatibility。
- [x] explicit intersection 优先于更近 root；输入/broadphase/action 顺序扰动不变。
- [x] actor scan/Top-K、ready action、action-zone、candidate budget 采用首个 typed phase + observed/accepted/limit。
- [x] candidate buffer overflow fail closed，不越界、不留下旧 entries。
- [x] warmed candidate generation 为 0 B GC allocation。

## 4. 完成 Gate

- focused tests、Unity validate、Console 0 error 和 NoAlloc 通过。
- 候选生成只做保守粗筛，不提前声称可命中或最终选中。
- 实际记录包含默认临时预算、计数和 T4 待校准标记。

## 5. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 实际分支 / 提交 | `main`；`CharacterTestRuntimeAiJointCandidateGenerator`（提交号待提交后补记） |
| focused / Unity | EditMode `CharacterTestCharacterDistanceM4JointCandidateTests` 5/5；Unity validate job `72e7c5cffba94062b6b0e2d249aba576`，refresh/compile/domain reload/Editor ready/Console 0 |
| truncation diagnostics | ActorScan/ActorTopK/ReadyAction/ActionZoneBroadphase/JointCandidate 与后续 query/cache phases 使用统一 enum；首个 phase + observed/accepted/limit 不被后续覆盖 |
| NoAlloc | 预分配 actor/action/zone/candidate buffers；warmed 128 次 Generate 实测当前线程 0 B |
| 临时预算 | actor scan 64、Top-K 16、ready action 16、action-zone 256、joint candidate 256；硬上限分别 256/64/64/1024/1024 |
| 已知限制 | 当前只交付无副作用 coarse generator/seed；T2 才运行 M1～M3 evaluator/reduction/cache，T3 才接 Planner；默认值仅为 T4 前临时上限 |
