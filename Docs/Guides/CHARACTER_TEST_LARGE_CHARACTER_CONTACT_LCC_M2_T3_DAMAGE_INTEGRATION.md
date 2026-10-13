# LCC-M2-T3：伤害前后处理与状态投影

> Status: Guide
>
> 工作包：LCC-M2-T3（生产实现）
>
> 状态权威：[LCC-M2 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md)
>
> 执行文档状态：`Verified`（生产实现、主代理集成检查与 focused gate 已完成）
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

现有damage chain在resolve前读取frame-start BodyPartState修饰当前伤害，在accepted damage应用后把明确BodyPart事实送入staging；显式action/ability/interaction事件经同一authority accepted gate产生typed Focus-consume fact。reaction/presentation仅消费commit后的typed结果，拒绝/默认/零伤/重复事实不污染累计。

非目标：不重新query Hurtbox、不改基础HP真源、不改Zone selector/network/CharacterStudio、不实现Severed。

## 2. 写入所有权

- Owner：`CharacterTestCombatDamageResolver.cs`、`CharacterTestCombatDamageApplier.cs`、accepted-damage bridge、typed Focus-consume fact adapter、telemetry/reaction专属投影和对应tests；T0 必须把 action/ability/interaction accepted seam 精确到文件/partial。
- T2 state API只读；state缺陷回派T2。T4/T5文件禁止写。

## 3. 实施清单

- [x] 从resolved hit保留稳定target/attacker/event/HitZone/BodyPart/partState证据与source policy分类；用CharacterTest内部typed provenance区分显式作者`body`与`DefaultFallback`，不改公共DamageResolvedEvent形状。
- [x] pre-hit modifier只读 frame-start immutable state；同帧所有 hit 使用相同旧状态，fixed-point倍率与舍入按T0冻结；无证据保持旧伤害。
- [x] HP接受并应用后产生accepted fact；拒绝、0伤控制、默认body fallback、未知part和重复event不累计。
- [x] accepted damage只stage；damage phase结束后由T2按稳定顺序commit，刚Open/Broken不放大同帧任何hit。
- [x] 仅authority accepted且明确target/partStateId/tag匹配的action/ability/interaction产生consume fact；accepted damage tag不直接consume。Closed同帧刚Open不能consume，duplicate/rejected/stale/unknown tag逐项typed拒绝。
- [x] source-policy tests逐行覆盖 direct trace、projectile、ability 与 self/friendly/dead/blocked/rejected/duplicate/default fallback/0 damage/poison-status/no-part-area exclusions。
- [x] telemetry区分base/resolved/final damage、old/new state和ignored reason；presentation/reaction不成为authority。
- [x] projectile/trace/ability不同source共享同一accepted事实合同，去重键稳定。
- [x] 旧角色/无definitions保持原伤害值与Gameplay行为。

## 4. 验证与停止

tests覆盖old/new frame倍率、同帧hit A/hit B旧状态、下一帧新状态、damage/consume、完整source-policy矩阵、拒绝/0/fallback/duplicate、reaction一次、旧角色回归、warmed 0 B；Unity脚本验证。若现有链无法区分resolved与accepted，先冻结正式accepted seam，不旁路HP。回报时序、source matrix、tests和风险。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | 共享 `main` 工作区实施；本任务未建分支、未提交、未暂存。 |
| 实际修改文件 | 新增 CharacterTest 专属 `CharacterTestBodyPartDamageAuthorityBridge.cs` 与 `.meta`、focused `CharacterTestBodyPartDamageIntegrationTests.cs` 与 `.meta`；最小修改 resolver、applier、trace composition root，以及 AbilityTimeline/ChainLightning/ArcMine/Projectile 四个既有 typed 调用点；仅更新本文执行记录。 |
| 测试与原始证据 | 实现代理最终 EditMode job `6fdf981a175c486a9cc3e9e3b29c8b31`：34/34 Passed；主代理集成复跑 job `c2a4c6335dbc490eab6615bcd3101de2`：34/34 Passed。覆盖 frame-start A/B 与下一帧倍率、post-HP stage/commit、stable policy identity、exact/conflicting duplicate、direct/projectile/ability、完整 rejected source matrix、显式 authored `body`/同值 fallback、Focus 三类 adapter 与 stale/tag/同帧限制、transition/reaction once、empty frame 清投影、Missing definitions兼容和4096次 warmed 完整链0 B。实现代理 Unity scripts job `d06837a8efbc4712bf4acaac113d323d`、主代理复跑 job `a49edcbfe1f74b4b9a139e403c581935` 均确认 refresh/compile/domain reload/editor ready 完整且 Console error 0。全仓 report-only standards baseline 扫描3442个文件、153项历史告警；本任务文件不在告警列表。 |
| 集成 / 回滚结果 | Resolver与applier共享同一专属bridge；`DamageResolvedEvent`回调只缓存，attribute command tick 后才以accepted且`FinalDamage>0` stage并commit。rejected outcome cache只补typed telemetry，永不stage；commit后复制一次transition/reaction投影，下一Begin或空帧completion清理。回滚移除专属bridge/tests并撤销resolver/applier/composition与四个typed参数接线即可。 |
| 状态历史 | T0 Verified 后实施合同进入 `Production-Frozen`；2026-08-24 T2 Verified 后解除并派发；2026-08-24 实现代理完成生产实现与focused gate；主代理检查专属bridge、resolver/applier/callsites并复跑focused与Unity验证后晋升为 `Verified`。 |
| 执行者 / 审查者 | 实现代理 `/root/lcc_m2_t3_damage_integration`；主代理完成集成检查，M2 唯一最终只读审查留到 T6。 |
| pre/post damage integration | pre-hit只读每target一次frame-start T2 view；Open倍率用`long`中间值、千分比整数除法向下取整并饱和`int`。post-hit不旁路HP：仅现有HP authority tick后处理缓存事件，使用实际`FinalDamage`累计。 |
| source / dedupe | DirectTrace、direct Projectile impact与显式 Ability hit可接受；Projectile AreaPulse、Tornado、Status、no-part AOE及非显式provenance typed fail closed。direct policy id优先保留query级numeric override；resolved key exact duplicate幂等，冲突identity标记ambiguous并拒绝，事件只消费一次。 |
| Focus / projection | 当前无同时具备accepted/target/partStateId/tag的真实生产seam，按T0保持生产adapter为空；仅提供专属typed Action/Ability/Interaction ingress。transition投影包含T2 old/new state与BreakReactionId，damage telemetry包含base/resolved/final与ignored reason，均不反写authority。 |
| deterministic / NoAlloc | bridge使用128 target、256 resolved/event/outcome、64 Focus及固定transition projection容量；hot path无LINQ、无动态容器、无BigInteger。4096次 warmed resolve/outcome cache/event cache/complete断言0 B并通过。 |
| 限制 | 按边界未接T4 selector、T5 network/schema、CharacterStudio/UI或Severed；未跑全仓测试、PlayMode、任务级独立评审或提交。 |
