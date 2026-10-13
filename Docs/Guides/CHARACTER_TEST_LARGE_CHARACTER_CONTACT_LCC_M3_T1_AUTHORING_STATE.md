# LCC-M3-T1：Severed Authoring、Authority State 与事件

> Status: Guide
>
> 工作包：LCC-M3-T1（生产实现）
>
> 状态权威：[LCC-M3 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)
>
> 执行文档状态：`Verified`（Authoring/binding/state/CharacterStudio 与正式 Dragon14 尾部定义已通过冻结 gate）
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
> 前置：T0 `Verified`。

## 1. 任务结果与非目标

M2部位定义向后兼容扩展Severed配置，compiler/runtime binding提供稳定依赖，CharacterStudio可无损编辑新增字段，BodyPartState reducer实现Broken→Severed与一次性typed event，并为后续系统提供只读filter/dependency API。

非目标：不移除Hurtbox、不失效Zone/动作、不实例化残肢、不接formal network（T6）。

## 2. 写入所有权

- Authoring Owner：`CharacterAuthoringBodyPartDamage.cs`、`CharacterPackageValidation.BodyPartDamage.cs`、`CharacterAuthoringCompiler.cs` 的 body-part partial/region、CLI `CharacterBodyPartDamageAuthoring.cs`；只扩展现有 body-part damage schema，不创建第二份 severance schema。
- Runtime binding/state Owner：`CharacterRuntimeSpawnContracts.cs`、`CharacterImportedPackageJson.cs` 的 body-part binding 区域，以及 `CharacterTestBodyPartStateComponent.cs`、`CharacterTestBodyPartDamageAuthorityBridge.cs` 的 Severed state/fact/event 区域。
- 正式真源 Owner：`Authoring/CharacterPackages/character-000003/geometry/body_part_damage.json`；只为 `partstate.000003.tail` 写明确引用。
- CharacterStudio Owner：`web/src/contextPanels.js`、`inspector.js`、`api.js`、`sessionState.js`、`state.js` 中现有 body-part damage 路径及 `scripts/smoke.mjs` 的对应覆盖；不得改其它编辑器域。
- 正式生成输出只走generator；T2～T6生产文件只读。
- 不并列创建第二份severance JSON或Presenter私有状态。

## 3. 实施清单

- [x] 最小扩展只新增 `severable`、typed `severRequiredTag` 与 `presentationEventId`；disabled BodyPart复用 `memberPartIds`，Zone/Anchor由现有关系派生，T4直接引用 `partStateId`，当前不伪造detached resource key；非法/重复/根核心引用fail closed。
- [x] Missing/旧schema保持M2无Severed行为；future raw不覆盖；canonical/hash/source mapping稳定。
- [x] Broken→Severed单向、有界显式fact/tag；Intact直接Severed始终拒绝。同一hit刚把Intact变Broken且携带tag时只commit Broken，后续authority frame的新合法sever fact才可Severed。
- [x] 触发hit完成M2当前帧处理；Severed revision在冻结commit点更新，几何/动作通过只读lookup从下一authority frame消费。
- [x] event key=entity+partStateId+state revision，一次投影；restore清空瞬态投影，重复fact/终态不重复side effect。
- [x] 提供stable filter/dependency lookup，不暴露字符串猜测或Unity对象。
- [x] Dragon14仅尾部定义为severable，复用真实 `tail_base/mid/tip` 与 `zone.tail`；Anchor仍由既有Zone关系派生，T4 action/trace依赖交接为 `partstate.000003.tail`。
- [x] CharacterStudio复用M2同一service/validation/save transaction与既有草稿/Undo/Redo/refresh/discard路径，编辑三个最小扩展字段；future raw仍只读，不建UI私有schema。

## 4. 验证与停止

Authoring/state/CharacterStudio tests覆盖旧schema、invalid refs、tag政策、同hit Broken+tag只到Broken、下一frame合法Severed、重复、event一次、respawn、UI round-trip/Undo/refresh/future raw；Unity脚本验证与浏览器smoke。若需要改变M2双轴或无法兼容旧包，停止合同评审。回报schema、transition、event、fixture、UI和tests。

Focused filter 固定为 Authoring `character-body-part-damage`、`CharacterBodyPartDamageRuntimeBindingTests`、`CharacterTestBodyPartStateRuntimeTests` 与 CharacterStudio smoke；证据写入 `Docs/Evidence/LCC/LCC-M3-T1/ACCEPTANCE_2026-08-25.md`。本任务完成后把稳定 binding/state 交给 T2，并把事件依赖只读合同交给 T4。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享工作区；不提交/暂存/推送/清理 |
| 实际修改文件 | 现有body-part Authoring DTO/validation/compiler、RuntimeSpawn binding/parser、BodyPartState/AuthorityBridge、两组focused tests；Dragon14 body-part真源与generator输出；CharacterStudio inspector/smoke；本文与 `Docs/Evidence/LCC/LCC-M3-T1/ACCEPTANCE_2026-08-25.md`。未修改T2～T6生产owner。 |
| 测试与原始证据 | Authoring `character-body-part-damage` exit 0；CLI validate exit 0/ready；Studio smoke exit 0；最终 Unity validator `448afa097b3e4ed2b6133e5968b364ff`完整通过、Console 0 error；EditMode `47341ea8aba04ae1bfe4eeaf34541a75` 5/5与`aa149fe612b84ebd963e07c8c7d4e139` 9/9；详见任务证据。 |
| 集成 / 回滚结果 | Dragon14通过既有expected-revision safe-save提升到revision 2，再由正式generator生成binding/hash；generator报告49 artifacts、5 updated、44 skipped、0 conflict/error，但后置ResourceMapping因既有`RESOURCE_MAPPING_BUILD_TARGET_REQUIRED`使命令exit 1。回滚为恢复三个可选字段/revision后重跑同一generator，不手改生成物。 |
| 状态历史 | 2026-08-25 T0 `Verified`、M3 `Production-Frozen` 后由 `Planned` 派发为 `In Progress`；同日实现、生成与 focused gates 完成，主代理复核 job/hash/scope 后提升为 `Verified`。 |
| 执行者 / 审查者 | 实现代理 `/root/lcc_m3_t1_authoring_state`；主代理完成集成复核，未增加任务级独立评审；里程碑唯一独立终审仍留T6。 |
| Authoring / binding | `mx.characterBodyPartDamage.v1/1.0`原位可选扩展三字段；compiler每definition全量输出，Runtime parser只接受全有/全无并分别验证新/旧hash；legacy缺字段=non-severable，future raw只读，非法tag/event/duplicate/root fail closed。 |
| Severed state / event | 固定frame-start Broken + 新typed sever fact；frame-end commit，下一authority frame消费；稳定BodyPart filter和`partStateId` dependency lookup；重复/终态不重复转移，restore不重放瞬态event。 |
| Dragon14 definition | revision 2；仅`partstate.000003.tail`启用，tag=`action.000003.tail.sever`、event=`event.000003.tail.severed`；generated content hash=`sha256:38f8f91475d263480b06bdf86da8a6f3f0527e387101137335861e1b8a582adb`。 |
| 限制 | topology/Zone与Anchor失效/action trace caller/presentation资源/schema31 network仍分别属于T2～T6；schema30整数枚举必须由T6显式禁止承载Severed；未跑全仓、PlayMode、直接浏览器截图或独立终审。 |
