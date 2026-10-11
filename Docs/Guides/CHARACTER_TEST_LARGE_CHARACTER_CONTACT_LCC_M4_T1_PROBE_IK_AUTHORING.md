# LCC-M4-T1：Ground Probe / IK Authoring 与 Dragon14 rig

> Status: Guide
>
> 工作包：LCC-M4-T1（生产实现与正式数据）
>
> 状态权威：[LCC-M4 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_AUTHORITY_TERRAIN_POSE.md)
>
> 执行文档状态：`Verified`（主代理一次交接门禁已闭合；T5 承担唯一独立终审）
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

Character package可无损作者化ground-probe/two-bone IK definitions，经validation/compiler/runtime binding单链消费；Dragon14四肢fixture使用稳定channel/chain且可正式重建。

非目标：不执行地形query/IK、不改Hurtbox/View/network、不硬编码Dragon骨名到CharacterTest。

## 2. 写入所有权

- Owner：Character Resource Package authority-pose/geometry Authoring DTO、validation/compiler/CLI、RuntimeSpawn binding、Dragon14正式作者rig/probe定义、生成输出和对应tests。
- 作者真源为 `geometry/authority_pose_rig.json`；compiler 只向 geometry binding v6 增加 feature-specific section。
- `Character.RuntimeSpawn/CharacterAuthorityPose.cs`可由本任务扩展现有definition/solver input，不写CharacterTest orchestration。
- 动画 base channel 复用现有 socket-bake/import 流程，以 additive 可选 basis 扩展 position-only track；若无法保持旧消费者兼容，停止回报，不自行建立第二套catalog/二进制协议。
- T2 Combat、T3 state、T4 consumers只读；正式资产只走generator/Editor/MCP。

## 3. 实施清单

- [x] 字段覆盖probeId/ikBindingId、root/mid/tip channel、tip-channel-local origin/axis、distance/radius/layer/slope、ModelRoot-local pole、length/reach、world-normal target offset、eligibility/hysteresis；每个字段的space/units进入validation与canonical hash。
- [x] validation覆盖唯一identity、channel存在、正长度、reach范围、finite/quantized参数、重复channel控制、M3 Severed映射和typed conflict。
- [x] Missing兼容base pose；invalid/future raw保护且fail closed；canonical/hash/source mapping稳定。
- [x] compiler/runtime binding fixed capacity、stable ordinal，不在tick解析JSON或读取Transform scale。
- [x] Dragon14四条真实limb chain与镜像数据，经正式bake/compile；记录length/pole来源；逐帧base position/basis由Unity import生成，不进入人工逐帧作者数据。
- [x] round-trip/order/future/missing/invalid/conflict/compiler output/Runtime compatibility/fixture tests。

## 4. 验证与停止

Authoring/CLI/RuntimeSpawn tests、重复生成diff、Unity脚本验证和rig binding可视检查。主代理仅做本任务范围、作者真源、生成provenance、future raw、space/units与focused evidence门禁；不另开独立评审轮次。若channel/bone映射不稳定、长度只能从Runtime Transform测量、作者源不唯一，或只能靠第二套bake协议完成，停止。回报schema、四肢表、generated provenance、tests/Unity和限制；独立P0/P1终审只在T5进行。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main` 共享 dirty worktree；按派发合同未 stage、未 commit、未 push |
| 实际修改文件 | authority-pose Authoring/validation/compiler/CLI、geometry v6/runtime binding、现有 authority definition/solver、现有 socket-bake optional basis、Dragon14 作者真源/生成物、focused tests；精确清单见证据文件 |
| 测试与原始证据 | Authoring focused PASS；CLI build 0 warning/0 error；最终 Unity validator job `9bcf6f41dc394f73bac513f113dac54e` PASS；整改后 EditMode `86a996368a474dfc8939cfc9507fd5b0` 5/5、既有 AuthorityPose 回归 `ec9d1cefc3944107b696d1d3c05bdced` 5/5；重复生成三项 hash 相同；整改文件代码规范扫描 2 files / 0 findings |
| 集成 / 回滚结果 | 现有 `mx.character.socketBakeCatalog.v1` 内 additive optional basis，position-only consumer不变；回滚可移除v1 rig源/新增partials并重生成回v5，不需要迁移旧catalog |
| 状态历史 | 2026-08-25 T0 `Verified`、M4 `Production-Frozen` 后由 `Planned` 派发为 `In Progress`；同日实现与focused evidence完成；主代理发现basis合法性P1后回派原owner窄修，最终门禁闭合并晋升 `Verified` |
| 执行者 / 主代理门禁 | T1实现子代理完成并按原owner关闭basis合法性P1；主代理核对源码、正式数据、5/5 focused、5/5回归、Unity Console 0与代码规范后通过；T5负责唯一独立终审 |
| Authoring / compiler / binding | `geometry/authority_pose_rig.json` v1为唯一作者真源；合法Current生成additive geometry binding v6；Missing保留v1-v5行为；invalid/future fail closed并保留raw；runtime fixed-capacity/stable ordinal绑定已落地 |
| Dragon14 four-limb fixture | 12 channels、4 root/mid/tip limbs、12 collider/socket attachments；8 clips正式生成96条position+basis轨道，另10条旧action socket轨道继续position-only；generator报告106 tracks、0 issues |
| tests / Unity | 详见 `Docs/Evidence/LCC/LCC-M4-T1/AUTHORING_RIG_2026-08-25.md`；正式生成前后config geometry、generated geometry、socket catalog文件hash逐项一致 |
| 限制 | T1不执行terrain query/IK authority tick、Hurtbox/View/network或CharacterTest orchestration；全局Resource Mapping build-target前置仍使CLI import进程非零退出，但报告为`WarningOnly`且正式T1产物成功写入；复用既有JSON catalog后正式Dragon14产物约10.88 MB，作为首版已知P3数据体积接受，不在无加载/包体证据时另造压缩协议；未新增独立rig gizmo/UI |
