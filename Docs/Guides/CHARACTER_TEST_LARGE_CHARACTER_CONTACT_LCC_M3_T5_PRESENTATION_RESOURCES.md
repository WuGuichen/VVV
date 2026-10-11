# LCC-M3-T5：Severed 表现与资源边界

> Status: Guide
>
> 工作包：LCC-M3-T5（Presentation、正式资源、Game View）
>
> 状态权威：[LCC-M3 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)
>
> 执行文档状态：`Verified`（主代理已读取最终作业、目视核对Game View、检查证据与范围；不增加任务级独立评审）
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

Presenter一次消费Severed authority event，按T0确认的Dragon14资源能力隐藏/切换mesh并触发VFX/SFX；可选残肢严格区分view-only与正式authority entity，取得真实Game View和回收证据。

非目标：Presenter不写BodyPartState/Hurtbox/Zone/action；不把Unity Rigidbody残肢伪装成Gameplay物体；无可拆资源时不宣称完整视觉断尾。

## 2. 写入所有权

- Owner：`CharacterTestRuntimeCharacterView.cs` 或窄 `.Severance.cs` partial、debug/presentation projection、VFX/SFX 与对应 tests。正式 Dragon14 resource files 默认只读；只有直接证明存在独立 renderer/mesh/resource key 时才允许最窄 binding 写入。
- Unity序列化资产仅由Editor/MCP/正式generator写，保留`.meta`与provenance。
- T1～T4 authority只读；T6 network和父状态只读。

## 3. 实施清单

- [x] event按entity+partState+revision去重，late view attach/restore显示当前state但不重播一次性VFX/SFX。
- [x] mesh/bone renderer切换使用稳定resource binding；missing资源typed fallback，不影响authority。本次没有稳定尾部resource binding，因此明确返回`RuntimeSliceNoStableResource`且不切换整身renderer/bone。
- [x] pooling/despawn/respawn/scene reload正确恢复；无泄漏/重复残肢。View `Release`、respawn与停止Play后的场景恢复均已验证；本次从未生成残肢对象。
- [x] view-only残肢无Gameplay collider/damage/obstacle语义；若需交互，通过正式world-object spawn contract产生新entity。本次不生成view-only残肢、Rigidbody、collider或world entity。
- [x] overlay/diagnostics区分authority state、topology/action失效与presentation状态。
- [x] Dragon14 sever前后Game View、Hurtbox overlay、尾部动作不再伤害、非尾动作继续的直接证据。after由一次性Editor-only acceptance drive触发，不证明生产sever ingress/player path。
- [x] 根据实际资源明确声明Runtime Slice或完整Playable：本次为**Runtime Slice**。

## 4. 验证与停止

PlayMode/presenter/pool tests、Unity Console 0、实际Game View与资源检查。若mesh无法局部隐藏/拆分，交付明确降级表现和限制；不得修改authority来迎合View。回报资产写入方式、资源key、截图/录像路径和生命周期结果。

T0 已确认当前正式资源只有单一 `000003_basemesh2.fbx`、Body/Eye material 与整身 Preview Prefab，未证明尾部独立资源。本任务默认交付等级为 Runtime Slice，不生成虚假 detach/Rigidbody/world limb；证据写入 `Docs/Evidence/LCC/LCC-M3-T5/ACCEPTANCE_2026-08-25.md`。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享工作区；按派发合同未commit、未stage、未push |
| 实际修改文件 | `CharacterTestRuntimeCharacterView.cs`、`CharacterTestCharacterUnityViewBridge.cs`、新增`CharacterTestSeverancePresentationDebug.cs`、`CharacterTestDeveloperDebugSnapshotBuilder.cs`中仅Severance card增量、EditMode/PlayMode聚焦tests及其`.meta`、本文与T5 acceptance/两张Game View PNG；未修改父M3或总台账 |
| 测试与原始证据 | EditMode job `a3784ba37eda4e6398c807633fac9bb3` 4/4；T4行为回归 job `6018280ef3694e0797684e7101fdc9e5` 2/2；PlayMode lifecycle job `4a50d9982422450b9f83cb3af59775f7` 1/1；最终validator结果见acceptance |
| 集成 / 回滚结果 | 主代理已读取最终作业、核对生命周期/资源边界/diff并目视检查两张Game View；停止Play后`CharacterTestScene`恢复`isDirty=false`、运行态对象卸载、Console error 0；formal network仍由T6收口 |
| 状态历史 | 2026-08-25 T0 回写后进入 `Production-Frozen`；同日 T4 `Verified` 后由`Planned`顺序派发为`In Progress`；同日完成实现与聚焦证据后，主代理集成复核提升为`Verified` |
| 执行者 / 审查者 | T5实现代理完成实现与证据；主代理完成集成复核；未增加任务级独立评审，里程碑唯一终审仍留T6 |
| resource / presenter | 每个view固定容量记录按`entity + partStateId + revision`消费Severed一次；资源缺失返回`RuntimeSliceNoStableResource`，VFX=0、SFX=0、limbs=0，整身renderer保持可见 |
| Game View / lifecycle | before：`Docs/Evidence/LCC/LCC-M3-T5/GAME_VIEW_BEFORE_INTACT_STATUS_2026-08-25.png`；after：`Docs/Evidence/LCC/LCC-M3-T5/GAME_VIEW_AFTER_SEVERED_STATUS_EDITOR_DRIVE_2026-08-25.png`；after明确为Editor-only acceptance drive；PlayMode job验证Release/deferred Destroy/respawn/restore不重播 |
| 实际能力等级 | **Runtime Slice**；authority/topology/action与typed presentation fallback可观察，未交付视觉断尾 |
| 限制 | Dragon14仅单一basemesh、Body/Eye material与整身Preview Prefab；无稳定尾部renderer/mesh/resource key；生产sever ingress caller/player path仍缺失，不能声明完整Playable或生产可达的视觉断尾 |
