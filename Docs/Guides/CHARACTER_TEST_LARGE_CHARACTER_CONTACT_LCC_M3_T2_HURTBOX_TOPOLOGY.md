# LCC-M3-T2：Hurtbox 稳定拓扑失效

> Status: Guide
>
> 工作包：LCC-M3-T2（生产实现）
>
> 状态权威：[LCC-M3 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)
>
> 执行文档状态：`Verified`（主代理已完成源码、focused job、Unity validator、证据与 scope/diff 集成复核）
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

Hurtbox SyncAll按Severed filter跳过指定稳定叶子，正确移除compound leaf并推进geometry/topology revision；其它collider ID不重排，并可从显式BodyPartState输入全量重建。formal late join 由T6验证。

非目标：不修改Zone/Anchor/Planner、不gate动作、不隐藏mesh、不让Presence随断尾消失。

## 2. 写入所有权

- Owner：`CharacterTestCharacterHurtboxSystem.cs`、`CharacterTestDamageGeometrySnapshot.cs` 的 topology/active-leaf partial，以及既有 Combat compound leaf removal/refit seam 的最窄必要修改与 topology tests。
- 不建立通用 topology manager。若既有 Combat seam 无法稳定删除叶子，停止并回报所需最小 API；不得把 CharacterTest 业务类型写入 Combat 核心。
- T3～T6文件、Body Presence和Presenter只读。

## 3. 实施清单

- [x] 遍历原binding稳定数组，以logical collider ID查询T1 filter；Severed叶子不Upsert且显式remove旧leaf。
- [x] `BeginCompoundColliderUpdate/End...`对增删/无变化推进正确revision并重建/refit BVH。
- [x] 其余collider/leaf ID、HitZone/BodyPart映射与排序保持；数组索引不因移除重编码。
- [x] DamageGeometry snapshot只聚合有效叶子并暴露topology witness；姿态变化与拓扑变化revision语义分离。
- [x] 全部显式Hurtbox失效时返回typed unavailable，不回退compatibility整身AABB。
- [x] spawn/显式state restore清旧compound并从binding+state重建相同叶子集合；stable state下不重复topology rebuild。formal snapshot/late join不属于本任务 `Verified` 门禁。
- [x] Presence核心volume不随Severed删除。

## 4. 验证与停止

tests覆盖单/多叶移除、其余ID不变、全部移除fail closed、重复sync、restore重建、顺序扰动、query前后、stable warmed 0 B；Unity脚本验证。若Combat API只能reindex leaf，停止并先扩展稳定identity合同。回报leaf表、revision、tests/perf和风险。

Dragon14 冻结叶子为 `hurt.tail_base.capsule.1`、`hurt.tail_mid.capsule.0`、`hurt.tail_tip.capsule.0`；只做 active filter，不重排 18 个正式 Hurtbox。证据写入 `Docs/Evidence/LCC/LCC-M3-T2/ACCEPTANCE_2026-08-25.md`。完成后串行移交 `CharacterTestDamageGeometrySnapshot.cs` 的只读 Zone/topology 视图给 T3。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享工作区；未提交、暂存、推送或清理 |
| 实际修改文件 | `CharacterTestCharacterHurtboxSystem.cs`、`CharacterTestDamageGeometrySnapshot.cs` topology/active-leaf区域；独立 `CharacterTestHurtboxTopologyTests.cs`及Unity生成`.meta`；本文与 `Docs/Evidence/LCC/LCC-M3-T2/ACCEPTANCE_2026-08-25.md`。未修改Combat core或T3～T6 owner。 |
| 测试与原始证据 | 最终Unity validator `55043d137af04f1aa27998961315f0bc`完整通过、Console 0 error；T2 EditMode `6e84e34ad1984c8e9780a37fd1ca7307` 5/5；既有Hurtbox删除回归 `f5330a52b5ab4d8e92d0598a7641d189` 1/1；详见任务证据。 |
| 集成 / 回滚结果 | 直接复用既有compound active-write-set省略删除、zero-leaf与stable refit语义，无Combat API扩展。回滚只需恢复两个runtime owner并移除本任务focused fixture；不改正式binding/BodyPartState或Presence。 |
| 状态历史 | 2026-08-25 T0 回写后进入 `Production-Frozen`；同日 T1 `Verified` 后由 `Planned` 派发为 `In Progress`；同日实现与focused gates完成，主代理复核两个Unity job、源码/证据和`git diff --check`后提升为`Verified`。 |
| 执行者 / 审查者 | 实现代理 `/root/lcc_m3_t2_hurtbox_topology`；未增加任务级独立评审，里程碑唯一独立终审仍留T6。 |
| Hurtbox filter / leaf removal | 复用T1 `GetBodyPartFilterStatus`；由原binding ordinal经既有HitZone/BodyPart metadata查询。Dragon14只省略三条冻结尾叶，18→15；其余ID保持`start + 原ordinal`。全部失活保持body+0 leaf，InvalidBinding移除body。 |
| revision / restore | Snapshot新增typed状态和真实Combat `TopologyRevision` witness；stable pose只推进HurtboxRevision。clear/recreate按Severed state重建15叶，显式Intact reinitialize恢复18个原ID。 |
| NoAlloc / Unity | focused NoAlloc 64次预热+512次测量断言0 B并保持15叶，包含于job `6e84e34ad1984c8e9780a37fd1ca7307`；最终Unity validator见上。 |
| 限制 | Zone/Anchor、action/trace、presentation与formal network/late join尚未实现，分别属于T3～T6；未跑全仓、PlayMode或独立终审。 |
