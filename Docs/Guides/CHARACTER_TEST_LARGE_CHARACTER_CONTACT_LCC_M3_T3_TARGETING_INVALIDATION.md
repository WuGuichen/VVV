# LCC-M3-T3：Zone、Anchor 与 Planner 失效

> Status: Guide
>
> 工作包：LCC-M3-T3（生产实现）
>
> 状态权威：[LCC-M3 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)
>
> 执行文档状态：`Verified`（主代理已读取最终测试作业、检查证据与范围；不增加任务级独立评审）
>
> 派发、集成与回滚规则：[LCC 单任务执行文档模板](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_TASK_EXECUTION_TEMPLATE.md)
>
> 并行规则：除本文明确写明“可并行”外，不与同里程碑其它生产写入并行；跨里程碑只允许只读调查。
>
> 必读基线：总台账第 1～8 节、父里程碑冻结合同、本任务全部章节、路径上的 AGENTS.md、Docs/API_STANDARDS.md 和 Docs/QUALITY_GATE.md；再按本文输入/所有权加载专项接口与源码。
>
> 子代理派发合同：本文是唯一任务边界；只写 Owner 范围，保留工作区其它修改，不改父状态。完成回报逐项列出实际文件、合同/兼容/失败行为、测试原始结果、未执行验证、风险冲突、集成与回滚建议。实现者不得担任 schema、network、碰撞数学、拓扑或正式资产任务的最终独立审查者。
>
> 两级派发门禁：只有 T0 只读审计可在全路线 Audit-Frozen 后派发。T0 只写自身冻结表并结构化回报精确文件/partial owner、测试 filter、Docs/Evidence/LCC/<TASK-ID>/ 证据文件名和交接点；T1+ 必须等待主代理把这些结论回写到生产任务，并将本里程碑标记为 Production-Frozen。
>
> 前置：T2 `Verified`。

## 1. 任务结果与非目标

Hurtbox拓扑变化使受影响DamageZone、当前lock、Engagement Anchor reservation、Attack Opportunity与Planner joint candidate在同一权威顺序确定性失效/重选；本任务以显式state输入验证纯状态重建，formal rollback后下一command/hash由T6收口。

非目标：不删除作者Zone/Anchor定义、不改root distance/其它Zone、不gate action trace、不做Presenter。

## 2. 写入所有权

- Owner：消费T2交接的只读`CharacterTestDamageGeometrySnapshot.cs`，写`CharacterTestDamageZoneSnapshot.cs`的Zone有效性/topology witness，以及`CharacterTestDamageZoneSelector.cs`、`CharacterTestEngagementReservationAuthority.cs`、`CharacterTestEngagementPlanner.cs`、`CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs`与`.Joint.cs`的topology-only invalidation及tests；允许在`CharacterTestRuntimeAiJointRuntime.Select`入口增加一次既有reservation sweep调用，使失效容量在joint candidate生成前同帧释放，除此之外该文件只读。T2的active-leaf/status实现只读，不回改。
- T2 topology只读；network序列化由T6写，本任务登记所有future-affecting witness，但不得把formal rollback/hash作为自身 `Verified` 前置。T0须冻结DamageGeometry/Planner共享文件的交接点。
- `CharacterTestAttackOpportunityService.cs`仅接有效geometry事实，不改其距离语义。

## 3. 实施清单

- [x] Zone有效性由当前有效Hurtbox成员决定；topology revision保留为独立诊断witness，空Zone明确unavailable。
- [x] 旧lock引用失效Zone/leaf时标stale，按现有稳定迟滞/比较链重选或无候选。
- [x] 受影响Anchor配置保留identity但runtime unavailable；reservation确定性release且容量计数正确。
- [x] Planner joint candidate和Attack Opportunity读取同一snapshot，不选择Runtime必拒绝的目标。
- [x] transition commit、topology sync、invalidation、selection/command顺序与T0一致。
- [x] 其它Zone/Anchor/CandidatePose/path cost不改变；root XZ distance合同保持，既有CD回归通过。
- [x] 使用显式state+binding的纯函数/内存重建测试得到相同validity和candidate事实；登记T6 formal snapshot/rollback所需witness，不在本任务伪造network parity。

## 4. 验证与停止

tests覆盖tail Zone失效、lock stale/reselect、reservation release、其它Zone保持、无候选、多人容量、顺序扰动、显式state重建与CD-M0～M4；T6再验证formal rollback+1 command/hash。若需删除作者定义或改root distance，停止。回报invalidation顺序、witness、tests和T6字段。

正式语义：尾部 Severed 后 `zone.tail` 为空并 unavailable，`anchor.tail.rear` 随之不可用；`zone.core` 只失去 `tail_base` 叶且仍由胸部保持有效。root-XZ、`GeometryInReach`、合法 Zone 与 pose-path cost 不变。证据写入 `Docs/Evidence/LCC/LCC-M3-T3/ACCEPTANCE_2026-08-25.md`。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享工作区；未提交、暂存、推送或清理 |
| 实际修改文件 | `CharacterTestDamageZoneSnapshot.cs`；`CharacterTestDamageZoneSelector.cs`、`CharacterTestEngagementPlanner.cs`、`CharacterTestEngagementReservationAuthority.cs`、`CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.Joint.cs`的topology-only部分；`CharacterTestRuntimeAiJointRuntime.cs`仅增加获准的一次既有sweep调用；独立focused fixture及`.meta`；本文与任务证据。主Adapter无需T3修改。 |
| 测试与原始证据 | 最终Unity validator `aeea777391eb4cdca9769728c58852c1`完整通过、Console 0；T3 EditMode `8a5af3a31f024646ac1f68c1fe6ce9cf` 5/5；既有CD-M2/CD-M3 `5a2a1ff46ef84d448b5b934d1e964f4d` 26/26；详见任务证据。 |
| 集成 / 回滚结果 | 复用既有Zone snapshot、selector、Planner、reservation authority和joint入口；未新增invalidator/registry/通用抽象。回滚仅恢复上述T3 runtime partial、移除focused fixture与证据，不触碰T2/T4～T6 owner或作者资产。 |
| 状态历史 | 2026-08-25 T0 回写后进入 `Production-Frozen`；同日 T2 `Verified` 后由`Planned`派发为`In Progress`；同日生产实现与focused gates完成，主代理读取最终5/5作业、检查证据与范围后提升为`Verified`。 |
| 执行者 / 审查者 | 实现代理 `/root/lcc_m3_t3_targeting_invalidation`；主代理完成集成复核；未增加任务级独立评审，里程碑唯一独立终审仍留T6。 |
| Zone / lock | 作者Zone identity稳定；无active leaf时`IsAvailable=false`且0 member。ZoneRevision只覆盖binding+availability+active members，不混入global topology。tail lock exact member失效并重选；存活head lock保持。 |
| Anchor / Planner | `anchor.tail.rear`作者identity保留但因`zone.tail` unavailable被Planner/joint排除；joint候选前同帧sweep释放tail reservation并保留core reservation。`zone.core`只失去tail_base且由胸部保持有效。 |
| rollback input / tests | 显式typed state+immutable binding内存重建得到相同Zone identity/availability/member顺序/revision；正式snapshot/late join/rollback+1 command/hash留T6。warmed selector+joint projection 4096次断言0 B。 |
| 限制 | action/trace dependency等待T4；presentation等待T5；formal network/hash等待T6；未跑全仓、PlayMode或独立终审。 |
