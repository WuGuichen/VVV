# LCC-M2-T5：部位状态正式网络权威

> Status: Guide
>
> 工作包：LCC-M2-T5（schema、snapshot、rollback）
>
> 状态权威：[LCC-M2 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md)
>
> 执行文档状态：`Verified`（schema 30、formal authority、rollback/replay/late join 与主代理组合验证已闭合；里程碑唯一独立终审仍留 T6）
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
> 前置：T2、T4 `Verified`。

## 1. 任务结果与非目标

BodyPartState、必要revision与Zone lock witness进入实施时下一可用formal schema，full/owned/non-owned hash、rollback/replay/late join和source mismatch全部确定性一致。

非目标：不争用T0已冻结的schema 30；若开始实施前已被其它已合入变更占用则先重新审计。不持久化presentation/telemetry/派生Zone summary，不实现Severed。

## 2. 写入所有权

- Owner：`Network/Snapshot/CharacterTestNetworkSnapshot.cs`及capture/apply/clone/schema normalization、authority checkpoint/replay/hash专属文件与network tests。
- T2 state、T4 selector只读；需要新增snapshot adapter时放在本任务专属partial。
- schema/history文档由主代理最终回写；子代理不修改父状态。

## 3. 实施清单

- [x] 复核T0记录的当前最高schema 29并使用30；若已被占用，停止并先回写下一可用版本、占用证据和legacy policy。
- [x] fixed-capacity state按partStateId稳定序列化，包含双轴、累计、revision及未来决策必需字段。
- [x] Zone lock/state witness、definition/source identity按T0合同；派生priority/文本不重复持久化。
- [x] capture/apply/clone、full/owned/non-owned hash覆盖owner语义；顺序扰动bytes/hash相同。
- [x] legacy无state归一为默认Closed+Intact或T0冻结行为；future/source mismatch fail closed。
- [x] rollback后下一tick damage/transition/Zone/Planner command/hash parity；replay/late join一次性event不重复。
- [x] restore清理T2/T4派生cache并重建，不借用restore前数据。

## 4. 验证与停止

运行network focused matrix、legacy/future、ownership hash、rollback+1、replay/late join/source mismatch、determinism；Unity脚本验证。若并行变更已占schema，重新审计取下一版，禁止强占。回报schema、bytes/hash、parity和未验证项。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main` 共享工作区；按派发合同未 commit、未 stage、未 clean。 |
| 实际修改文件 | 新增 `Network/Snapshot/CharacterTestBodyPartStateNetworkSnapshot.cs` 与 focused network tests；修改 `CharacterTestNetworkSnapshot.cs`、`CharacterTestCharacterRuntimeModule.cs`、Runtime AI Planner lock snapshot/adapter；按批准的最小扩界在 T3 bridge → damage applier → combat trace 链增加 authority-restore 清理转发；更新本文。未改 selector/comparer、通用 serializer、SessionFactory 或正式资产。 |
| 测试与原始证据 | T5 focused 最终 `a648e5065c9f4a209c8b6e285282a79c`：8/8；既有 M1 authority + hit-policy network + M2 lock authority `0f410fa13d1a47b78cda751120cef165`：22/22；late join/schema mismatch/lockstep replay `d7ecd7450510452da9634ceb368fd3de`：3/3。上游 v5 identity 修复后 package identity `a075abf9550441d5a35712a53c413fef`：5/5、原失败 replay `01e2fd1f0ce14314b258c348202217c1`：2/2；主代理最终组合 job `5b17a19d25c745099d3d34d35ab9ec00`：20/20，含 4096 帧 0 B、P95 1.928 ms。最终 Unity validator `0754f0d9a0bd4b57aa56e170a74f972c`：refresh/compile/domain reload/ready 完整，Console 0 errors；全局 standards 3446 文件/153 条既有基线，T5 文件无新增 finding，`git diff --check` 通过。 |
| 集成 / 回滚结果 | 主代理已完成源码边界、原始 job、v5 上游兼容修复与当前工作区组合验证，T5 提升为 `Verified`；按用户约束不增加任务级独立评审，M2 唯一 P0/P1 终审留 T6。未做提交/合并。若需源码回滚，按本表文件边界撤回 schema 30 body-part DTO/participant 接线及三个最薄 restore 转发，不触碰 T2/T3/T4 其它交付。 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress`，实施 schema 30 并完成 focused/窄回归；2026-08-25：上游 geometry v5 catalog identity 缺口按原 owner 窄修，主代理组合 20/20 与最终 Unity 门禁通过，`In Progress` → `Verified`。 |
| 执行者 / 审查者 | Development Agent（T5 实施）；主代理完成集成复核；里程碑唯一独立终审保留给 T6。 |
| 实际 schema | 正式 schema `30`；`SchemaVersionValue=30`，显式保留 schema 29 EngagementReservation 支持。schema ≤29 清空 BodyPartStates，按当前 binding 恢复 Closed+Intact/零 wound/满 integrity，并将每个 Zone lock 的 `BodyPartStateRevision` 归零；future schema fail closed。 |
| snapshot / hash | 每 entity 记录 initialization、component revision、definition source format/schema/revision/content hash；每 definition-index/partStateId 记录 wound value/state、integrity/structure、last accepted frame、entry revision。entity/entry/Zone-lock 数组 capture/normalize/clone/formal JSON 规范排序；full/owned/non-owned hash 按 entity owner 分区，Zone lock 按 source owner 分区并覆盖 `BodyPartStateRevision`。派生 effective priority、Focus 文本、telemetry、reaction、presentation 不入 snapshot。 |
| rollback / replay / late join | `a648...` 覆盖 source mismatch/future/坏 lock target 在首次世界写入前原子失败且旧 hash/投影不变，其中 late-join source mismatch 不生成角色；legacy 默认及 lock revision 归零；late join 双轴/累计/revision/hash；rollback+1 damage/transition/Zone-lock witness/hash parity；真实 T3 future telemetry/transition restore 清零，下一 empty/hit frame 可重新开始，restore/replay 一次性投影不重复。 |
| 限制 | 初次 replay job `91c156d324d04425bf65d08cd785e407` 暴露的 geometry v5 catalog identity 缺口已按 M1-T4 owner 修复并由 2/2、组合 20/20 关闭。T5 未运行全仓测试、PlayMode、玩家可见场景、提交/推送；这些属于 T6 的整体验收与唯一独立终审边界。 |
