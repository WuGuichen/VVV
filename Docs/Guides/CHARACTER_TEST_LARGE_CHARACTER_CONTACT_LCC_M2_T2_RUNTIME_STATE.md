# LCC-M2-T2：BodyPartState authority 与 Reducer

> Status: Guide
>
> 工作包：LCC-M2-T2（生产实现）
>
> 状态权威：[LCC-M2 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md)
>
> 执行文档状态：`Verified`
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
> 前置：T1 `Verified`。

## 1. 任务结果与非目标

CharacterTest实体拥有fixed-capacity、稳定排序的BodyPartState authority；frame-staged reducer确定性处理Closed/Open/Consumed与Intact/Broken，并发布一次性typed transition event。

非目标：不接damage resolver/applier、不改变Zone选择、不接网络序列化、不实现Severed/恢复治疗。

## 2. 写入所有权

- Owner：新 `CharacterTestBodyPartState*` component/world/definition lookup/reducer/event文件、spawn/despawn/reset职责与focused tests。
- 只读：damage resolver/applier（T3）、selector（T4）、network（T5）、Authoring binding（T1）。
- 共享entity/component registry只由本任务增加state存储，后续任务通过冻结API消费。

## 3. 实施清单

- [x] 初始化按partStateId ordinal，固定最多8个entry并在spawn校验；Missing definitions产生零state且不改变旧角色。
- [x] O(1)或有界lookup，不用字符串/LINQ/动态扩容；未知BodyPart返回typed no-evidence。
- [x] 每帧开始生成 immutable read snapshot；该帧全部 damage/selection pre-read 使用它，reducer只在damage phase结束后消费staged facts并原子commit。
- [x] reducer输入包含稳定accepted-damage fact与typed Focus-consume fact，按damage-first、各自稳定排序、去重和overflow政策处理。
- [x] wound与structure轴正交；阈值边界、饱和累计、Open/Broken单向转换及revision明确。
- [x] 同帧所有hit不消费刚触发的新状态；Closed同帧刚变Open时consume忽略，只有frame-start Open可consume。Consumed在当前entity生命周期内终态，respawn重置，structure仍可累计。
- [x] event按entity+partStateId+axis+revision一次性，重复fact/restore重放不重复发布。
- [x] respawn/reinitialize、death政策、invalid binding fail-closed有测试。

## 4. 验证与停止

focused tests覆盖阈值前/等于/超过、同帧hit A/hit B均读旧状态、damage+consume顺序、Closed当帧打开不能当帧consume、Consumed终态、顺序扰动、重复、overflow、双轴独立、event一次、spawn/respawn；warmed reducer 0 B。若需从几何重新query或默认BodyPart猜测，停止。回报API、时序、容量、tests/Unity和网络待接状态。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | 共享工作区实施；本任务未建分支、未提交、未暂存。 |
| 实际修改文件 | 新增 `Runtime/World/CharacterTestBodyPartStateComponent.cs` 与 `.meta`、`Tests/Game/CharacterTest/CharacterTestBodyPartStateRuntimeTests.cs` 与 `.meta`；最小修改 `CharacterTestComponentWorld.cs`、`CharacterTestCharacterEntity.cs`、`CharacterTestCharacterRuntimeWorld.cs`；仅更新本文执行记录。 |
| 测试与原始证据 | 核心 EditMode `e422587a250c4e769eb7f49fd2b08445`：4/4；补充 EditMode `b0350d86d6ce4de7b5367e05a655a114`：3/3；删除审计后整类 `ea2ed67a0f8c494e97819ce8cfc4b99c`：7/7；主代理修正 Authored 空 definitions 分类后复跑 `8c03433536cb48ae9d6160ee6843bc0a`：7/7。覆盖阈值前/等于/超过、value-only revision、Open+Broken revision +2/event revision、frame-start旧view、damage-first/consume、Consumed+structure、顺序扰动/去重/overflow原子失败、Missing/Invalid/reinitialize、Dragon14 v5 spawn/death/revive/respawn与warmed 0 B。 |
| 集成 / 回滚结果 | Spawn复用既有一次package load并在`EntitySpawned`前初始化；ComponentWorld明确存储并在Remove/Clear释放；death/revive不重置，正式reinitialize/新spawn重置。回滚只需移除两个专属文件并撤销三处共享接线。 |
| 状态历史 | 2026-08-24：`In Progress` → `Verified`；主代理完成 diff/语义复核，将 Authored 空 definitions 从 Missing 修正为 Invalid 后复跑 focused/Unity 门禁通过。 |
| 执行者 / 审查者 | 实现代理 `/root/lcc_m2_t2_runtime_state`；主代理完成集成检查；未增加任务级独立评审，M2 最终只读审查留到 T6 |
| state / reducer / event | 每实体最多8 entry；默认damage/focus staging容量128/64，transition容量16；fact只携带numeric definition index作为部位状态引用；stable insertion sort、邻接dedupe、任一overflow/conflict/revision耗尽均原子fail closed。每条实际axis transition独立推进entry revision，value-only commit推进一次。 |
| deterministic / NoAlloc | wound/integrity使用`long`中间值与轴容量饱和，无BigInteger；初始化后Begin/Stage/Commit不用LINQ、字符串部位查找或动态扩容。4096次warmed reducer测试断言0 B并通过。 |
| Unity | 实现代理最终 job `02206b4839e74f26932cf2227f1320f4` 与主代理修订后 job `b3057e6df60345ca85f614b28d87f90c` 均确认 refresh completed、compile started/finished、domain reload observed、Editor ready、Console error 0。现有 obsolete warnings 不属于 T2。 |
| 限制 | 按边界未接T3 damage resolver/applier、T4 selector、T5 network/schema、UI/presentation；未跑全仓测试、PlayMode、独立评审或提交。 |
