# LCC-M3-T4：动作与 Trace 的部位依赖

> Status: Guide
>
> 工作包：LCC-M3-T4（生产实现与正式动作数据）
>
> 状态权威：[LCC-M3 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)
>
> 执行文档状态：`Verified`（主代理已读取最终测试作业、检查正式数据/typed reason/证据与范围；不增加任务级独立评审）
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
> 前置：T1、T3 `Verified`。

## 1. 任务结果与非目标

CharacterAction/trace具有typed `requiredPartState`依赖；Planner提交与Runtime eligibility一致，Dragon14 tail sweep/spin在尾部Severed后拒绝，动作中切断从下一帧停止依赖Trace并给出独立诊断。

非目标：不根据missing socket猜Severed、不取消无关动作/动画、不实现visual severance或残肢伤害。

## 2. 写入所有权

- Owner：CharacterAction config/import/validation 的最小 dependency 字段；CharacterTest `CharacterTestCharacterActionConfigSource.*`、`CharacterTestCharacterActionRuntime.cs`、`CharacterTestCharacterCombatTraceSystem.cs` 的 severance partial、`CharacterTestCharacterActionSystem.cs` 与 Planner candidate adapter 的 topology-only partial；正式真源 `character_actions.json` 及对应 tests。
- Generated action JSON只由正式generator写；T3 targeting、T5 presenter、T6 network只读。
- 共享CharacterAction公共API保持通用命名，不出现Dragon14业务类型。

## 3. 实施清单

- [x] typed dependency稳定引用partStateId及允许状态；invalid/missing/future fail closed并保留raw。
- [x] action提交前Planner/Runtime使用同一dependency snapshot与typed reject reason。
- [x] action已开始后每authority frame生成Trace前检查；transition生效后的下一帧及后续窗口不生成依赖Trace。
- [x] missing socket/bake、Severed dependency、stale binding和action unavailable分别诊断。
- [x] Dragon14 `940002` / `000003.attack6a` 中只 gate `000003.attack6.tail_sweep`（frame 112..126），保留同动作 bite；`940004` / `000003.attack30b` 中 gate `000003.attack30b.tail_spin`（frame 108..134）。两者均为 `BakedSocketCapsule`、`tailSweepStart -> tailSweepEnd`、radius 0.55。
- [x] T0审计到的当前尾部 projectile/ability/socket/interaction emitters全部在生成事件前检查同一dependency；若当前不存在，用显式“none found”审计证据收口，不扩大为未实现能力。
- [x] trace/event去重与当前帧已接受hit边界符合T0，不追溯撤销。
- [x] authoring round-trip、eligibility、mid-action、Planner parity、natural attack和NoAlloc tests。

## 4. 验证与停止

若动作身份/trace socket无法稳定映射，停止而非字符串猜测；若Planner与Runtime只能复制状态，先建立共享只读contract。记录作者/生成diff、tests、Unity脚本验证和GameView前置证据。

当前正式包未发现尾部 projectile、ability 或 interaction emitter，因此不修改通用 emitter；missing socket 与 Severed reason 必须分离。focused tests 覆盖两个真实事件、mid-action 与 natural attack，证据写入 `Docs/Evidence/LCC/LCC-M3-T4/ACCEPTANCE_2026-08-25.md`。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | 共享 `main` 脏工作树；本任务未 commit、stage、cleanup 或 push |
| 实际修改文件 | T4 精确文件与共享文件内的 T4-only ownership 见 [`Docs/Evidence/LCC/LCC-M3-T4/ACCEPTANCE_2026-08-25.md`](../Evidence/LCC/LCC-M3-T4/ACCEPTANCE_2026-08-25.md) |
| 测试与原始证据 | EditMode single job `ee2c89609e684916b581b5ddef8bb6c4`：1/1 passed；最终 focused job `d8829e8e7db44354b68e59437240a00e`：8/8 passed；warmed 4096 次 eligibility + prune：0 B |
| 集成 / 回滚结果 | 主代理已读取最终测试作业、核对正式源/生成投影、typed reason与diff范围；formal rollback/late join仍由T6收口 |
| 状态历史 | 2026-08-25 T0 回写后进入 `Production-Frozen`；同日 T1、T3 `Verified` 后顺序派发；T4 实现与 focused evidence 完成后，主代理集成复核提升为`Verified` |
| 执行者 / 审查者 | T4 实现代理完成生产补丁、typed reason修正与focused tests；主代理完成集成复核；未增加任务级独立评审，T6仍是里程碑唯一终审 |
| Authoring dependency | `requiredPartStateId` + raw `allowedPartStructureStateMask` 已贯通 DTO/domain/import/validation；invalid/missing/future raw 被保留并由 validation fail closed；source/generated 两个正式事件逐字段一致 |
| eligibility / trace gate | Runtime 与 ordinary/Joint Planner 复用 action-runtime evaluator；Severed 映射独立 `PartStateDependencyRejected`；active dependency trace 在下一 authority frame 生成前 prune，已接受 hit 不追溯撤销 |
| Dragon14 actions / tests | 仅 `000003.attack6.tail_sweep` 与 `000003.attack30b.tail_spin` 增加 tail Intact/Broken mask `3`；真实 `940002` bite 保留、真实 `940004` tail-only 拒绝；emitters 审计为 none found |
| 限制 | 一体化 validator 在 domain reload 时 transport disconnected，已按 AGENTS 分步回退证明 compile/reload/idle/Console 0 error；未执行 full suite、PlayMode/视觉、presentation、network、rollback、commit 或 push |
