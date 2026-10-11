# LCC-M3-T6：网络、性能、回归与里程碑验收

> Status: Guide
>
> 工作包：LCC-M3-T6（schema、集成验收、独立审查）
>
> 状态权威：[LCC-M3 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)
>
> 执行文档状态：`Verified`（唯一独立终审发现两项 P1；均已整改并由主代理复核关闭，当前未关闭 P0/P1=0）
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
> 前置：T5 `Verified`。

## 1. 任务结果与非目标

Severed state及future-affecting witness进入实施时下一可用schema；rollback/replay/late join、Hurtbox拓扑、Zone/Anchor/Planner、动作Trace、表现、性能和全回归闭合，独立审查无P0/P1。

非目标：不扩大到翼/头/任意部位切断，不把view-only残肢纳入authority，不开始M4 IK。

## 2. 写入所有权

- Owner：CharacterTest formal network snapshot/hash/replay/late-join的M3字段与tests、M3 integration/Playable/performance tests、接口/能力表事实回写和证据索引。
- 当前最高 schema 为 30，候选 schema31；派发时必须重新确认下一可用。只序列化最小 Severed authority state/revision/event witness，不序列化派生 leaf mask、Zone validity 或 priority；核心缺陷回派 T1～T5。
- 独立审查只读，父状态由主代理更新。

## 3. 验证与验收矩阵

- [x] capture/apply/clone、full/owned/non-owned hash、legacy/future/source mismatch、rollback+1、replay/late join；event与VFX不重复。
- [x] schema `<=30` 的 snapshot/capture/restore 明确拒绝或映射为非Severed，不能因 `Enum.IsDefined` 自动接受值2；只有现场确认的schema31开始序列化Severed及其最小witness。
- [x] Broken→Severed当前hit/下一帧边界、重复trigger、respawn、stable collider IDs、全部叶子失效fail closed。
- [x] Zone lock stale、reservation release、Planner重选、其它Zone/root distance/CD-M0～M4不回归。
- [x] tail sweep/spin sever前正常、sever后提交拒绝、mid-action后续Trace取消、missing socket诊断分离、bite继续。
- [x] Game View与资源生命周期；按真实资源声明为 `Runtime Slice / RuntimeSliceNoStableResource`，不宣称完整Playable。
- [x] CharacterStudio Severed字段create/edit/Undo/Redo/save/reopen/refresh/future raw只读与smoke全部通过；UI不成为第二状态真源。
- [x] stable topology状态下Hurtbox/selector/trace gate warmed 0 B；低频切换与8角色场景记录，不臆造阈值。
- [x] Unity Console 0、focused/regression、P2/P3候选登记和接口/能力表回写完成；未运行全仓。
- [x] 唯一独立终审已完整登记P0～P3；实现者未担任终审。结论为 `Not Verified`：P0=0、P1=2、P2=1、P3=2。
- [x] 两项P1已按原T6 owner范围整改并以focused/聚合/validator证明：非Severable schema31篡改失败无partial mutation；schema31 restore即时重建Hurtbox/Zone并在Planner前清理失效tail lock/reservation。
- [x] 唯一独立终审原始结论 P0=0/P1=2；两项 P1 均按原 owner 整改，主代理逐项核对源码并直接查询缺陷 job 与 16/16 聚合结果后关闭。当前未关闭 P0/P1=0；未执行第二轮独立终审。

Network owner 固定为 `CharacterTestNetworkSnapshot.cs`、`CharacterTestBodyPartStateNetworkSnapshot.cs`、`CharacterTestReplayValidation.cs` 的 schema31 区域与 tests；证据写入 `Docs/Evidence/LCC/LCC-M3-T6/ACCEPTANCE_2026-08-25.md`。只在全部 focused/Playable/NoAlloc 证据完成后执行一次里程碑末只读终审，不开启循环评审。

## 4. 停止与晋升

任一required gate未执行、identity重排、compatibility AABB回退、sever后仍伤害、rollback command/hash不一致或表现回写authority均阻止验收。派发给验收子代理时只授权本任务写入范围；主代理复核证据后更新M3；M3 `Verified` 才解除M4。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main` dirty worktree；本任务未stage、commit、push或清理 |
| 实际修改文件 | schema31 snapshot/codec、post-restore Hurtbox/Zone同步与既有lock/reservation sweep窄seam、focused network/replay tests；`Network.md`、`Combat.md`、`CharacterApplication.md`、`CAPABILITIES.md`、本T6与Evidence；精确清单见Evidence第2节 |
| 测试与原始证据 | P1篡改job `35f09725f0754df28feea68544a7118a`：1/1；真实Physics restore job `d7569d67d7da4acabfeab1356b3eb721`：1/1；整改后schema31聚合job `f5b98dcce92d435bb15d782957fba152`：16/16；T1～T5核心job `143c56464b2b48e8b0a8d6ea92c8adf6`：62/62；PlayMode `42fa99441155431aa1989bfb9ccdbee0`：1/1；validator显式失败回退job `03a13c1967184aa7bc65d5e4ef39d1ec` passed/Console 0；未跑全仓 |
| 集成 / 回滚结果 | capture/apply/clone、full/owned/non-owned hash、rollback+1、replay/late join通过；schema31 restore返回前已有15 leaves/Zone view，tail lock/reservation清理且真实`zone.core` witness保留；restore不重放transition/presentation event，相同+1 command/result hash一致 |
| 状态历史 | 2026-08-25 T0 回写后进入 `Production-Frozen`；同日 T5 `Verified` 后由`Planned`顺序派发为`In Progress`；唯一独立终审给出P0=0/P1=2/P2=1/P3=2；两项P1按原owner整改后由主代理完成源码、精确job与聚合结果复核，当前未关闭P0/P1=0并晋升`Verified`；未执行第二轮独立终审 |
| 实际 schema / network | 现场最高30、启用31；schema30 raw整数2 validator拒绝且canonical legacy restore为Broken；future/source/outer-document mismatch在commit前失败关闭；派生leaf/Zone/presentation不入wire |
| topology / targeting / action | 复用T2～T4 formal jobs证明15叶、stable IDs/zero-leaf fail closed、tail Zone unavailable、reservation/Planner重选、tail trace gate与bite继续 |
| Game View / 能力等级 | 复用T5 before/after及SHA-256；after为Editor-only drive；单basemesh=`RuntimeSliceNoStableResource`，无detach/VFX/SFX/limb；不是完整Playable |
| performance / regression | Hurtbox 512、selector/joint 4096、eligibility/trace 4096 warmed均断言0 B；8角色schema31低频整改后最终聚合观测10.9627 ms/0 B但不设阈值；focused performance job `691e598128f44cacbdfc766525708658` 2/2 |
| 独立审查 / 文档 | 唯一独立终审已执行，原始P0=0/P1=2/P2=1/P3=2；两项P1已整改并由主代理关闭，当前未关闭P0/P1=0；P2=1/P3=2保持接受限制；未执行第二轮独立终审 |
| 结论 | `Verified`；schema31、恢复拓扑/Planner一致性、回归、性能观测、接口与能力表均按Runtime Slice边界闭合；父M3与总台账由主代理另行晋升 |
