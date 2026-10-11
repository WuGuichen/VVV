# LCC-M4-T4：Resolved Authority Pose 同源消费者

> Status: Guide
>
> 工作包：LCC-M4-T4（生产集成）
>
> 状态权威：[LCC-M4 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_AUTHORITY_TERRAIN_POSE.md)
>
> 执行文档状态：`Verified`（主代理已完成一次交接门禁；统一数值 overlay 作为明示验收项交 T5）
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
> 前置：T3 `Verified`。

## 1. 任务结果与非目标

Animated Hurtbox、明确绑定的BakedSocket Trace、body-part contact point与Unity View全部只读消费T3同一resolved authority pose frame。authority-controlled limb channel不再运行Unity/float two-bone IK；其它PresentationOnly修饰不能回写Combat事实。

非目标：Body Presence仍只随ModelRoot；不让所有socket默认受IK；不读更新后的Unity bone作为authority；不改probe/state算法。

## 2. 写入所有权

- Owner：`CharacterTestCharacterHurtboxSystem.cs`的resolved-pose输入、`CharacterTestBakedWeaponTraceProvider.cs`/socket resolver的明确channel接入、PhysicalAudio/contact-point authority adapter、`CharacterTestRuntimeCharacterView.cs`与`CharacterTestCharacterUnityViewBridge.cs`消费桥及tests。
- `CharacterProceduralBoneRig.cs`仅作为View投影可由本任务写，用于跳过/屏蔽authority-controlled channels的既有Transform/float IK；不得重新求解这些channel或回写T3 state。
- T1～T3合同只读；共享Hurtbox拓扑保持M3 stable identity。

## 3. 实施清单

- [x] 只读消费T3已定义的resolved pose frame/revision；所有消费者在同一authority frame读取同一版本，本任务没有新增第二种snapshot或revision。
- [x] Hurtbox每个shape在root变换前使用resolved channel的position+basis delta，不读取Transform；M3 Severed filter仍优先/正确组合。
- [x] 只有作者声明的socket/channel消费terrain overlay；左/右claw BakedSocket Trace与Planner预览复用同一窄数学入口，未声明或注入非默认socket source保持base pose。
- [x] body-part contact point仅在config stableId与显式`AttachmentKind.ContactPoint`同时存在时从resolved channel投影到PhysicalAudio motion fact；不反读View/脚骨，显式绑定失败回baked/static base。Dragon14当前为0 config/0 attachment，因此live事件fail closed。
- [x] View只按authored channelId→bonePath映射同一channel；authority-controlled channel跳过既有Unity/float two-bone IK及相交secondary chain。未受T3控制的PresentationOnly路径保留且不回写authority。
- [x] missing/stale/invalid channel typed fail to base pose；View使用固定12个restore slot在下一帧恢复animation/base，不产生零点或上帧Transform残留。
- [ ] overlay同时显示base/resolved/probe target、Hurtbox/socket/contact，便于同源对照。本批只取得角色Game View可见性和既有Hurtbox/Presence/root几何层的Scene View初证；当帧terrain overlay与统一base/resolved/probe/socket/contact数值层未实现，交T5收口。

## 4. 验证与停止

tests断言同帧channel/revision、ModelRoot-local position/orientation、Hurtbox/query、Trace endpoint、contact point、View target数值一致；authority channel的float IK不再二次运行，PresentationOnly不回写；Severed/disabled/no-hit回归；full consumer path warmed 0 B；Unity脚本和Game View初证。主代理只做消费者覆盖、Unity IK禁用、数值对照与focused evidence交接门禁，不另开独立评审轮次。若View rig无法稳定映射，不允许反读Transform补偿；独立P0/P1终审只在T5进行。回报数值对照、tests/Unity和视觉限制。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享dirty worktree；已派发，禁止stage/commit/push |
| 实际修改文件 | 新增任务专属`CharacterTestAuthorityPoseConsumerMath.cs`及消费者tests；窄改Hurtbox、RuntimeModule调度、BakedSocket Trace/Planner、PhysicalAudio contact adapter、RuntimeCharacter View/Bridge/Loader和`CharacterProceduralBoneRig.cs`。精确清单见Evidence。 |
| 测试与原始证据 | 最终consumer/contact/View/0B job `175d53b2288049be802543a726775088` = 8/8；PhysicalAudio job `d978842c974a492c84c301e5b4a3c1e7` = 4/4；Hurtbox+既有trace/planner+procedural job `bdb38859199c450590cb99f93f2c87dc` = 11/11；Hurtbox数值复跑 `54512defe8624be3854e0f72026f520b` = 1/1；唯一integrated validator job `b152394699e6406ea1093ae198ce54d6` = passed，Console error=0。 |
| 集成 / 回滚结果 | 生产接入与聚焦回归已完成；Game View/Scene View初证已保存在`Temp/CodexEvidence/LCC-M4-T4/`。未stage/commit/push/clean；回滚只限T4 helper/tests与本表列出的消费者窄hunk，不触碰T1～T3或共享dirty修改。 |
| 状态历史 | 执行文档已由`Audit-Frozen`经T0回写为`Production-Frozen`；2026-08-25 T3 `Verified`后串行派发为`In Progress`；同日生产、focused tests、视觉初证与唯一 integrated validator 通过，主代理交接门禁接受为`Verified`。 |
| 执行者 / 审查者 | T4实现子代理已完成派发范围；主代理只做一次消费者交接门禁，T5负责唯一独立终审。 |
| Hurtbox / socket / contact | Hurtbox的sphere/capsule/OBB在root前应用每source的position+basis delta，然后blend；Severed先过滤。只有四个显式claw socket attachment（左/右trace各start/end）接入Trace与Planner。Contact以config `StableId`匹配显式ContactPoint attachment；Dragon14当前0/0，不猜probe/bodyPart。 |
| Unity View / overlay | authored bonePath数值投影、非1 root scale、旧secondary不双叠、stale/missing恢复和IK skip已有数值测试。Game View可见且Console error=0，但录制时terrain overlay未激活，无统一T4数值overlay。 |
| 同源 / NoAlloc | 消费者要求expected RuntimeFrame + resolved revision + definition/base source witness；`Valid=true`的NoHit retained/fade和secondary-only sample继续消费，invalid/stale/missing回base。warmed full helper/contact path 0 B通过。 |
| 限制 | RuntimeModule为保证Motion后同帧消费者再次执行既有Hurtbox topology sync，每tick两次sync的成本交T5测量；Dragon14无live ContactPoint authoring；terrain-active Game View和统一overlay、最终Playable/8-character验收等待T5。 |
