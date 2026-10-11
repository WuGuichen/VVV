# LCC-M4-T5：Dragon14 地形姿态 Playable 与路线终验

> Status: Guide
>
> 工作包：LCC-M4-T5（Playable、网络、性能、独立审查）
>
> 状态权威：[LCC-M4 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_AUTHORITY_TERRAIN_POSE.md)
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
> 前置：T4 `Verified`。

## 1. 任务结果与非目标

Dragon14在平地、斜坡、台阶和不均匀四足支撑中取得确定性地形姿态；无hit/过陡/动作禁用/Severed安全回退；Hurtbox/Trace/contact/View同源、网络与性能闭合，并完成LCC全路线终审。

非目标：不宣称脊柱贴地、蛇/触手、飞行空气动力、ragdoll或任意全身程序化动画。

## 2. 写入所有权

- Owner：M4 PlayMode/Playable fixture与正式场景/测试驱动、全链performance/network regression、证据索引和Character/Combat/Animation/Network/Audio/Authoring接口及能力表事实回写。
- Unity场景/资产用Editor/MCP写；核心缺陷回派T1～T4；独立审查只读；父状态由主代理更新。

## 3. 验证与验收矩阵

- [x] 平地、上下坡、台阶边、四脚不同高度、no-hit、too-steep、out-of-reach、移动/转身、动作禁用、飞行/击飞/死亡、Severed limb；正式 Dragon14 无 action tag/limb partStateId 的项目与通用合同 fixture 分开登记。
- [x] Game View显示正式 Dragon14、四块不同高度地面与测试专用 probe/resolved/Hurtbox/socket marker；同源由同帧数值断言补足，不宣称存在生产统一 overlay 或正式 ContactPoint。
- [x] 攻击窗口与受击位置在姿态变化中对齐；PresentationOnly Animator/Transform 只接收同一 resolved frame，不影响 Combat authority。
- [x] formal schema32、full/owned/non-owned hash、rollback+1 pose/Hurtbox/trace/Planner command、replay/late join/source mismatch闭合。
- [x] 4 probes+IK+两次实际 Hurtbox SyncAll、8角色、failure/recovery steady 0 B并完成T0校准；无 production overlay，因此 overlay-on=N/A。
- [x] LCC-M0～M3、CD-M0～M4、natural attacks、motion/pathfinding、Physical Audio聚焦回归；最终分步Unity脚本门禁与Console 0闭合。
- [x] 唯一 independent review 确认无 Unity Physics/Animator/Transform authority 读取，结论 P0=0、P1=0、P2=1、P3=3；P2 已随本次父状态封版关闭，P3 均登记接受理由、owner 与后续门禁，能力表仅声明实际首期范围。证据：[INDEPENDENT_REVIEW_2026-08-25.md](../Evidence/LCC/LCC-M4-T5/INDEPENDENT_REVIEW_2026-08-25.md)。

## 4. 路线终验与晋升

M4验收后，主代理还需复核M0～M4状态历史、所有required evidence、schema链和已知限制，确认五个里程碑均真实 `Verified` 才可完成目标。派发给终验子代理时只授权本任务写入范围；任何未运行gate、双真源、rollback不一致或steady GC>0均阻止晋升。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享dirty worktree；禁止stage/commit/push/clean |
| 实际修改文件 | 3个T5 feature-specific tests-only fixture及meta；4个陈旧测试fixture的最小合同迁移；Character/Combat/Animation/CharacterAction/Network/Audio接口、能力表、T5 Evidence与本文。T5不写生产；三项实证缺陷由原T4/M1 owner最小修复。精确清单见Acceptance Evidence。 |
| 测试与原始证据 | 专用PlayMode `bdd838f341804a8fbe87222f06e41454` 3/3；network acceptance `724b5174c3fe4870848e37447e9cc641` 1/1及既有rollback `1427b590025a4de3b38ac8e799f8a7ba` 1/1；performance `59d0868279cd4332aac5d4f4dadd1bdf` 2/2；27-filter EditMode `7857bb06ae3c4b359375b2c224ada014` 290/290；冻结5-class PlayMode `076edfb771df4120b7608375cdad3354` 12/12。失败/infra历史未隐藏，见Evidence。 |
| 集成 / 回滚结果 | 所有T5改动保持tests/docs-only；生产回滚分别限于原owner的View exact Animator-root适配、snapshot view sync前derived Animation重建、Presence strict v6兼容hunk。未stage/commit/push/clean，不触碰父M4或总台账。 |
| 状态历史 | 执行文档已 `Audit-Frozen`并经T0回写为`Production-Frozen`；2026-08-25 T4 `Verified`后串行派发为`In Progress`；同日focused gate与唯一最终Unity脚本门禁闭合，先收口为`Candidate Complete / Handoff Ready for Independent Review`；唯一最终只读审查 P0=0、P1=0 后由主代理晋升为`Verified`。 |
| Playable / Game View | 正式 Dragon14 terrain-active PlayMode 3/3；Game View 2560×1440截图 SHA-256 `EFD6E7E400CA821AD596A0471711CF55194A9996B5CBFF46EB90ECDE3B08CF2A`。测试专用marker非生产overlay；画面偏暗且含Directional Light gizmo，数值同源以自动化断言为准。 |
| network / replay | schema32 capture/apply/clone、full/owned/non-owned、rollback+1 resolved pose/真实Hurtbox/实际BakedSocket/Planner/hash、replay/late join/source mismatch均由聚焦集合闭合；不等价于多进程dedicated server或公网transport。 |
| performance / regressions | 8 Dragon，warm300/measure1800，首Sync→4 probes/IK→第二Sync全链p50/p95/max=`1238.5/1821.4/6708.6 µs`；full/no-hit/recovery均0 B。双Sync已校准且预算通过，不优化。EditMode 290/290、PlayMode 12/12。 |
| Unity scripts / Console | integrated validator仅调用一次并在domain reload等待回包时断线；按规则一次分步fallback明确请求compile，观察新domain reload、Editor idle/not compiling/no reload pending，最终Console error=0。未重跑validator或测试。 |
| independent review | 唯一最终只读审查已执行：P0=0、P1=0、P2=1、P3=3，无需生产回派。P2 为父计划状态残留，随本次封版关闭；P3 为约10.88 MB catalog、双Hurtbox Sync校准保留和正式Dragon内容覆盖限制，均不阻塞晋升。见 [Independent Review Evidence](../Evidence/LCC/LCC-M4-T5/INDEPENDENT_REVIEW_2026-08-25.md)。 |
| interfaces / capabilities | 已按实证回写；通用fixed-point terrain-pose为Framework Feature，当前Playable限定Dragon14四肢，正式ContactPoint 0/0、空action tag/limb partStateId与首期非目标均明确。 |
| 结论 | `Verified`。全部计划 gate 已闭合；父 M4 与 LCC 总路线由主代理同步晋升，不追加第二轮独立审查。 |
