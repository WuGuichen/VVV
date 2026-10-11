# LCC-M2-T6：CharacterStudio、Dragon14 校准与 M2 验收

> Status: Guide
>
> 工作包：LCC-M2-T6（UI、Playable、性能、独立审查）
>
> 状态权威：[LCC-M2 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md)
>
> 执行文档状态：`Verified`（实现、主代理复跑与唯一独立终审均已闭合）
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

CharacterStudio具备部位定义的完整编辑/验证/Undo/Redo/save/reopen/future只读闭环；Dragon14 Playable校准并证明伤口/破坏、选择和网络行为，完整性能与独立审查通过。

非目标：不实现断尾/拓扑/动作失效，不宣称所有怪物通用阈值，不用UI状态替代authority。

## 2. 写入所有权

- Owner：`Tools/MxFramework.CharacterStudio/web/**` 中body-part专属state/api/inspector/context/UI、`scripts/smoke.mjs`；M2 PlayMode/Playable tests与证据；接口/能力表事实回写。
- UI只调用T1同一service/validation/save transaction，不自建schema或本地正式存储。
- 正式Dragon14参数调整只改作者真源并走生成链；父状态由主代理更新，独立审查只读。

## 3. 验收清单

- [x] create/edit/delete/reorder、成员映射、阈值/倍率、validation、Undo/Redo、save/reopen、普通刷新保草稿/UI、显式丢弃、future raw只读。
- [x] 标准和窄宽度实际浏览器画面、无溢出/不可达控件；smoke通过。
- [x] Dragon14至少一个Open与Broken部位：既有focused bridge/runtime测试证明同一authority frame内后续hit仍读frame-start旧倍率；正式Dragon14链证明Frame N真实`light_attack`打开Head、Frame N+1真实`light_attack_2`读取Open倍率并Broken、Frame N+2沿同一resolver/applier链进入死亡生命周期；reaction/telemetry次数可解释。
- [x] Focus consume typed ingress与聚焦事实测试验证：frame-start Open才可consume、同帧刚Open不可consume、Consumed生命周期终态/respawn重置；当前没有生产Action/Ability/Interaction调用方，正式Dragon14 `focusConsumeTag`为空并严格关闭。
- [x] 动态Zone/Focus/lock变化正确，不改变Approachable/root distance；无定义角色不回归。
- [x] schema/hash/rollback/replay/late join/source mismatch全通过。
- [x] 8角色固定容量state+damage+selector steady 0 B；阈值校准记录样本damage、命中次数和理由。
- [x] Unity Console 0、Game View直接证据、independent review完整P0～P3且P0/P1=0；P2/P3已登记接受理由、owner与后续门禁，接口/能力表按实际等级回写。

## 4. 停止与晋升

任何UI绕过service、future被覆盖、触发hit自我放大、default BodyPart累计、网络不一致或未取得实际Play证据均阻止验收。派发给验收子代理时只授权本任务写入范围；子代理仅回报，主代理复核后更新M2；M2 `Verified` 才解除M3。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main` 共享工作区；未 commit/stage/push/clean |
| 实际修改文件 | CharacterStudio `web/src/{state,api,sessionState,contextPanels,inspector}.js`、`web/{app.js,index.html,styles.css}`、`scripts/smoke.mjs`、README；T1原validator及聚焦Authoring测试补Current非空门禁，RuntimeSpawn parser/test补v5 definitions `[1,8]`失败关闭；正式Dragon14作者真源及生成物校准；正式Dragon14 PlayMode与8角色组合性能测试；v5 playable catalog identity与既有M1 Presence runtime兼容seam；本任务证据与Interfaces/CAPABILITIES事实回写。未改T1 schema/service/canonical writer。 |
| 测试与原始证据 | 主代理最终validator `f4b4ff25b5a24395ba4fe7e98a66d562`通过且Console 0；M2 focused `f7c8616b42334a7dbb7ab9bb417c0eb9` 59/59；Runtime binding `20781d53c61c4671b6b49a6b3c167ecd` 4/4；Presence authority与catalog replay `553f7d1297f74fad8491402dbd9b245b` 13/13；NoAlloc `703efd340cba41738ab40c941f08f760` 1/1；Dragon14 PlayMode `30f2cd691c0f47fd820c07f449aaca55` 2/2；Presence v5 runtime `ebbb74aeb7d049f1b7896c85e6944c73` 2/2；既有Presence/尾攻击聚合PlayMode `52cc88fb5eb64ae7a75f8ae8ef367d14` 3/3，独立自然攻击 `af0c9482b3804fc5b551d88f50211ff2` 1/1。Authoring、CharacterStudio smoke、Node语法与formal import均exit 0。详见 `Docs/Evidence/LCC/LCC-M2-T6/ACCEPTANCE_2026-08-25.md`。 |
| 集成 / 回滚结果 | UI只调用T1 revision-gated GET/POST；整包save保留独立文件。浏览器draft reorder→Undo→Redo→save/reopen在重新校准前证明了规范化行为，当时SHA-256为`7408C2B25D907EB8A5F073E174F18F2CDB889FCB1A522329453859FBD6884E3E`；最终校准后作者真源SHA-256为`CCC85EC37FEF549B6A67D3C0ABF8937780550B5C79A1CEC145A175E6975F924B`，generated body-part identity为`sha256:9e7ba22de3fb2741850363cfe371844456a73401714bae5e3b0d082636158d1d`。规范化顺序仍为head/left_wing/right_wing/tail。 |
| 状态历史 | T0 后实施合同 `Production-Frozen`；2026-08-25 T5 Verified 后 `Planned` → `In Progress`；同日主代理复跑与唯一独立终审完成，`In Progress` → `Verified`。 |
| UI / smoke / 浏览器 | 1600×1000 与 820×1000 三张直接浏览器证据；标准/窄宽均无横向溢出，成员跨组占用为 disabled；普通刷新保留 dirty/Undo/selection；T1 保存重开报告 canonical reorder。显式 discard 的确认取消未静默丢稿；确认接受分支与 Future raw 由 source/smoke 覆盖，未伪造成直接浏览器证据。 |
| Dragon14 Playable / 校准 | 正式generated Dragon14为4 definitions/10 lookup；阈值校准为Head `34/68`、左右Wing各`51/85`、Tail `68/90`，倍率/priority保持不变。Head正式链为Frame N `light_attack`经真实head弱点解析66，HP `100→34`并Open；Frame N+1 `light_attack_2`先解析21、读取Open倍率后为26，HP `34→8`并Broken；Frame N+2同链26使HP到0且只发出一次death event。另一正式测试用真实collider成员`head`、`left_wing_membrane`、`right_wing_membrane`、`tail_tip`逐组证明只累计目标entry，并额外断言root member映射。正式CharacterTest Game View只证明Dragon14实际入场，数值结论来自PlayMode。 |
| network / NoAlloc | 现有 focused 覆盖 schema 30 canonical/hash、rollback/replay/late join/source mismatch；8 个不同 entity（2..9）共享 physics world，32 warmup + 4096 次 state + pre-hit bridge + accepted staging/commit + selector 测得 0 B。 |
| 独立审查 | 唯一只读终审：P0=0、P1=0。接受P2：Focus仅有typed ingress、无生产调用方；任何正式非空tag前必须接真实accepted fact并补Playable。接受P3：当前固定catalog唯一，但动态/Mod catalog前必须以规范化root集合做全局去重并补非相邻重复测试。状态摘要P2已由本次主代理回写关闭。 |
| 文档 / 能力表 | 已按Verified事实更新CharacterStudio README、CharacterApplication/Combat/Network接口与CAPABILITIES，并明确Focus生产adapter尚不存在；父M2/总台账由主代理同步晋升。 |
| 结论 | T6实现、直接证据、主代理聚焦复跑与独立终审全部闭合，状态为`Verified`；不声明Severed、Hurtbox拓扑变化或M3/M4能力。 |
