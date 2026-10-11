# LCC-M0-T5：M0 集成验收与独立终审

> Status: Guide
>
> 工作包：LCC-M0-T5（集成、验收、独立审查）
>
> 状态权威：[LCC-M0 第 4 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M0_CONTRACT_FOUNDATION.md)
>
> 执行文档状态：`Production-Frozen`
>
> 工作包派发状态：`Verified`
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

把 T1～T4 的候选实现整合成可证明的 M0：Authoring/CLI/CharacterStudio 无损保护、binding v4 兼容、separation API、Dragon14 fixture 与诊断全部通过，独立审查无 P0/P1。

非目标：不实现 M1 接触压力、M2 部位状态、M3 切断或 M4 IK；不以计划/测试源码代替实际执行。

## 2. 写入所有权

- Owner：M0 focused integration tests、CharacterStudio 最小 package-state/read-only 展示与 smoke、M0 相关接口/指南/能力表事实回写、必要的专属验收 fixture。
- 父里程碑与总台账状态只由主代理更新，验收子代理只读。
- 禁止重写 T1～T4 核心实现；发现缺陷由主代理回派原 owner 或创建明确修复批次。
- 保留所有非 LCC 工作区修改，不执行清理、重置或全量格式化。

## 3. 验收清单

- [x] T0～T4 的实际 diff 与任务边界一致，公共 API/程序集/schema/正式资产写入方式合规。
- [x] Authoring round-trip、missing/future/invalid、canonical/hash、CLI/service 和 CharacterStudio 读写保护通过。
- [x] binding v1～v4、source mapping、RuntimeSpawn compatibility 与 source mismatch 通过。
- [x] separation 全形状、退化、稳定排序、NoAlloc 和旧 Combat query 回归通过。
- [x] Dragon14 正式生成可重现；overlay 分层清楚；中心 X/Z 只诊断；Gameplay 不改变。
- [x] Unity 默认脚本验证、focused EditMode、Dragon14 natural attacks、Console 0 和正式 Playable Game/Scene 画面均有原始证据；M0 未新增行为，未为凑数追加无关 PlayMode 测试。
- [x] 接口/能力表只声明实际等级，明确 M0 尚无身体推挤能力。

## 4. 独立审查合同

使用未参与 T1～T4 实现的代理只读审查：兼容写回风险、第二真源、normal/comparer、generated asset provenance、程序集边界、测试漏项和工作区污染。按 P0～P3 列出文件/行/复现；P0/P1 未清零不得验收。

## 5. 完成证据与停止条件

- 汇总每条命令、测试通过数、Unity job 终态、Game View 路径、NoAlloc 数值和独立审查结论。
- 任何未运行的 required gate 都明确为未验证，不得升 `Verified`。
- 发现行为已进入接触/伤害/移动，或 future schema 可被覆盖，立即停止并回滚相应候选修改。

## 6. 派发回报

验收代理只报告事实，不修改父状态。主代理复核证据、更新 M0 表/历史/总台账；M0 `Verified` 后才能解除 M1 前置。

## 7. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区内验收；未提交，保留既有 camera/input/UPM 等非 LCC 修改 |
| 实际修改文件 | CharacterStudio `state/api/inspector/app/index` 最小 Body Presence 集成、Node smoke、`CharacterPackageTests.LccM0Integration.cs` 与四份实际接口/能力文档 |
| 测试与原始证据 | 根复验 Node smoke、`lcc-m0-integration`、`character-body-presence` 均通过；Unity focused EditMode job `17eb3f63e1c24e8a83b1646328b62d74` 13/13；脚本验证 job `a42e98f09d6d4fde824db8eec286bb88` 完整通过、Console 0 |
| 集成 / 回滚结果 | Body Presence 只有独立 API 是 Current 写入者；整包保存保护 Future/Invalid 原文；回退 T5 UI/测试/文档不改变 T1～T4 数据合同或 Gameplay |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Verified` |
| 集成者 / 独立审查者 | 集成实现代理完成候选；主代理复验；唯一最终独立只读审查 P0=0、P1=0、Pass |
| focused / regression | v1～v4 compatibility、separation 8 项、Dragon fixture/自然攻击隔离共 13/13；Authoring 专项通过；既有 ResourceMapping/UPM 全套失败未归因于 LCC，也未隐藏 |
| Unity / Game View | 默认脚本验证通过；复用 T4 `Temp/LCC-M0-T4/000003_gameview_direct_focus.png` 与 `000003_sceneview_presence_core.png`；未追加无关 PlayMode 套件 |
| NoAlloc | `BulkQuery_Warmed4096IterationsAllocateZeroBytes_AndLegacyQueryStillHits` 复验通过，4096 次 warmed caller-owned bulk 为 0 B |
| 文档 / 能力表 | Character Application、Character Resource Package、CharacterStudio README 与 `Docs/CAPABILITIES.md` 已按实际 API 回写，并明确 M0 尚无可玩推挤 |
| 结论 | `Verified`；T0～T5 全部闭合，M0 为 6/6 Verified，可解除 M1-T0 前置 |
