# LCC-M1-T4：Authority parity、诊断与 NoAlloc

> Status: Guide
>
> 工作包：LCC-M1-T4（网络、诊断、性能）
>
> 状态权威：[LCC-M1 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md)
>
> 执行文档状态：`Production-Frozen`（M1-T0～T3 已 Verified；network/diagnostics/performance owner 已解除）
>
> 工作包派发状态：`Verified`（主代理已完成范围、实现、原始结果与编译证据复核；里程碑级独立终审仍只在 M1-T5 执行一次）
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

所有影响下一 tick Presence contact 的历史状态正确进入现有 snapshot/hash/rollback/replay，派生 compound/cache 在 restore 后确定性重建；诊断和四层 overlay 可解释接触，完整稳定路径达到容量与 0 B 门禁。

非目标：不持久化可重算 world shapes，不把 debug数据纳入 formal snapshot，不制作最终 Playable/能力声明（T5）。

## 2. 写入所有权

- Owner：`CharacterTestNetworkSnapshot.cs` 只审查/必要时修改 contact velocity 的 `FromEntity/CreatePhysicsComponent/AddCharacterEntityHash/ApplyDocument` partial；`CharacterTestNetworkSessionFactory.cs` 只允许在 `CreateNetworkSessionConfig` 路径增加私有/内部的角色包 authority identity 计算并写入现有 `ResourceCatalogHash`；`CharacterTestReplayValidation.cs` 只允许在现有 record inspect/kernel 入口把 replay header 的 `ResourceCatalogHash` 与同一当前生产 identity 做一次精确比较，并使用独立于 Runtime AI catalog 的 failure code；`CharacterTestCharacterContactResolver.cs` / `CharacterTestCharacterBodyPresenceSystem.cs` 只允许增加固定容量、纯数值、只读的 Presence diagnostics snapshot，`CharacterTestCharacterMotionSystem.cs` / `CharacterTestCharacterRuntimeModule.cs` 只允许增加对应内部只读 accessor；`CharacterTestLargeCharacterPresenceDebug.cs`、`CharacterTestDebugPresentationController.cs`、`CharacterTestDeveloperDebugSnapshotBuilder.cs` 只写 Presence contact numeric diagnostics/overlay partial；`CharacterTestNetworkLoopbackTests.cs`、`CharacterTestReplayTests.cs`、`CharacterTestProfilerGcRemediationTests.cs` 与新增 `CharacterTestLargeCharacterPresenceAuthorityTests.cs`。
- T1/T2/T3 的候选、数学、pressure、motion 与写状态逻辑只读；除上述 diagnostics projection/accessor partial 外，缺陷回派原 owner。禁止为诊断建立第二套 contact 模型或公共框架。
- T0 结论是不新增 formal state/schema；contact velocity 已 capture/apply 并进入 full/owned/non-owned hash。若 T3 实际引入跨 tick future-affecting history，停止交主代理，不得预占或无状态升级。
- T4 stop-check 结论：contact velocity 的 capture/apply/rollback/replay/hash 链已完整，不写 snapshot；缺口仅是生产会话当前把 `ResourceCatalogHash` 留空。最小修复必须复用该字段，对 `CharacterTestPlayableCharacterCatalog.Discover()` 返回的正式可玩角色定义稳定排序，纳入 CharacterId、规范化 GeneratedPackageRoot、import report 的 source/geometry hash、geometry binding format/schema 及 BodyPresence revision/content hash，以标准库 SHA-256 生成带版本前缀的启动期 identity。非空 package root 无法加载或上述 identity 字段无效时，在创建会话前失败关闭；不新增 snapshot schema、wire 字段、公共 API、通用 catalog 抽象或自研大整数。
- T4 replay stop-check 结论：header 已记录并参与 record hash，但现有 playback validation 未比较当前生产 `ResourceCatalogHash`；既有 `CATALOG_HASH_MISMATCH` 专用于 Runtime AI catalog。允许仅在 `CharacterTestReplayValidation.cs` 的 inspect/kernel 入口复用 factory 的当前 identity 做精确失败关闭，并新增明确的 package resource mismatch code；不改 header/schema、frame driver、Runtime AI identity 或 UI/controller。

## 3. 实施清单

- [x] 分类 root pose/history、definition/source revision、pair cache、compound world shapes：只持久化会影响未来结果且不可从已有 snapshot重算的状态。
- [x] 实证 contact velocity 现有 capture/apply/clone、full/owned/non-owned hash、legacy normalize、rollback+1 tick、replay/late join链；无缺陷，因此未改 snapshot。
- [x] restore清理 candidate/constraint/compound派生缓存并按稳定顺序重建；不借用未来帧或旧世界历史。
- [x] source mismatch/future schema fail closed，不让双方以不同 Presence 配置继续 authority；复用既有 `ResourceCatalogHash` 握手/回放校验路径，未增加第二套协议。
- [x] 为已确认缺口补正式 package/session identity：稳定顺序、字段边界、单字段变化、无效包失败关闭及生产配置非空均有 focused tests，未把差异藏在派生 cache。
- [x] diagnostics输出 entity/volume/normal/penetration/pressure/clamp/overflow/failure；字符串只在低频投影阶段生成。
- [x] overlay分别开关 Hard Root、Presence、Hurtbox、Active Trace，并标出 contact normal/pressure；不回写 authority。
- [x] 8角色×4 Presence×18 Hurtbox full sync/contact warmed NoAlloc与容器顺序扰动。

## 4. 验证与证据

- network focused tests验证 snapshot bytes/hash所有域、rollback后下一tick pose/contact/command parity、replay/late join/source mismatch。
- debug tests验证层级、开关、overflow/failure reason；Game View 初步证据不能替代 T5 playable。
- 性能记录硬件/editor版本、角色数、帧数、warm-up、时间/容量峰值和 GC=0 B。
- Unity脚本验证完整；是否漏持久化 future-affecting history 或误持久化派生几何，统一纳入 M1-T5 的唯一一次 P0/P1 终审，本任务不另开评审轮次。

## 5. 停止条件与派发

若发现上一 root pose 会影响下一 tick但无法从 snapshot恢复，必须正式持久化并按 schema流程；不得称为cache绕过。回报 schema决策、parity、诊断截图/路径和性能原始结果。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | 未执行 |
| 实际修改文件 | `CharacterTestNetworkSessionFactory.cs`；`CharacterTestReplayValidation.cs`；`CharacterTestCharacterContactResolver.cs`；`CharacterTestCharacterBodyPresenceSystem.cs`；`CharacterTestCharacterMotionSystem.cs`；`CharacterTestCharacterRuntimeModule.cs`；`CharacterTestLargeCharacterPresenceDebug.cs`；`CharacterTestDebugPresentationController.cs`；`CharacterTestDeveloperDebugSnapshotBuilder.cs`；`CharacterTestLargeCharacterPresenceAuthorityTests.cs` 及新增 `.meta`；`CharacterTestReplayTests.cs`；`CharacterTestLargeCharacterPresenceDebugTests.cs`；本文。`CharacterTestNetworkSnapshot.cs` 只读审计后保持未改。 |
| 测试与原始证据 | 性能单项 EditMode job `c9b6e1a6f52e462e94fe28ed2c5b2c7f`：1/1；Authority focused job `2e8c0c825e32489987ebd4e58d5224ec`：8/8；Replay/Debug focused job `406d2a57581648298ce2543b68e1ebd5`：4/4；最终 `validate_unity_scripts` job `96f99b4990ee420facfaa640afba2793`：refresh/compile/domain reload/editor ready 完整，Console 0 errors。2026-08-25 下游M2-T1引入geometry v5后，按本owner窄补catalog identity：package identity `a075abf9550441d5a35712a53c413fef` 5/5、原失败replay `01e2fd1f0ce14314b258c348202217c1` 2/2、主代理组合 `5b17a19d25c745099d3d34d35ab9ec00` 20/20、最终validator `0754f0d9a0bd4b57aa56e170a74f972c` Console 0。 |
| 集成 / 回滚结果 | `FromEntity/CreatePhysicsComponent` 保留 contact velocity；full/owned/non-owned hash 均会响应对应 owned/non-owned 实体的 contact velocity；`CaptureRollbackDocument` → `ApplyRollbackDocument` 恢复后同帧三类 hash 相等，下一 tick full hash 继续一致。Replay header 空/不一致 package resource identity 使用独立 `PLAYABLE_RESOURCE_CATALOG_MISMATCH` 失败关闭；未改 replay/snapshot schema。 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Candidate Complete` → `Verified`；首次 stop-check 确认 contact velocity 链完整、生产 `ResourceCatalogHash` 为空；主代理冻结最小 identity 与 replay 校验合同后解除单点写入，并复核实现边界、8/8 与 4/4 原始结果、0 B/P95 和最终 validator 后接受。2026-08-25：M2-T1 geometry v5 暴露白名单兼容缺口，按原owner只补v5 Presence/BodyPartDamage identity与回归，状态保持 `Verified`。 |
| 执行者 / 审查者 | 子代理完成实现与定向验证；主代理完成范围与集成接受。未增加逐任务独立评审；M1 仍只在 T5 保留一次 P0/P1 独立终审。 |
| snapshot / hash / replay | snapshot 生产文件、schema 与 wire 字段未改；会话 identity 使用标准库 SHA-256 与版本前缀，覆盖全部正式 playable 定义及 CharacterId、规范根路径、source/geometry hash、format/schema、Presence revision/content；v5继续强制Authored Presence并纳入BodyPartDamage mode/format/schema/revision/contentHash，v1-v4要求部位伤害Missing兼容状态；非空无效package创建会话前失败关闭，握手与replay inspect共用同一当前identity。 |
| diagnostics / overlay | Resolver/BodyPresence 直接投影既有固定 buffer/counter，不复制 authority 数组；Motion/RuntimeModule 仅内部只读 accessor。Characters tab 输出 status、capacity、clamp、failure 与 entity/volume/normal/penetration/pressure；Hard Root、Presence、Hurtbox、Active Trace 四层独立开关，投影本身不写 authority。 |
| NoAlloc / order | 固定夹具：Unity `6000.5.0f1`，i5-12400F，8 角色、32 active Presence、144 active Hurtbox，64 warm-up、4096 capture。单项 job：median `1.206 ms`、P95 `1.838 ms`、max `26.564 ms`、GC `0 B`；完整 Authority 复跑：median `1.215 ms`、P95 `2.017 ms`、max `33.194 ms`、GC `0 B`。峰值 raw/pair/constraint=`28/28/224`，iterations=`3`，sync projections/leaves/registered=`8/32/8`，status=`Success`、overflow/failure=`0/0`。该完全饱和夹具的 `clampPeak=224` 是现有安全限幅观测，不是 overflow/failure；本任务不猜性能硬阈值。诊断顺序扰动与 warmed read `0 B` 同在 Authority focused 内通过。 |
| 限制 | 未运行全仓、PlayMode 或 Game View；未提交；未进行独立评审。Playable 场景体验、最终截图、P0/P1 与性能门槛判定留给 M1-T5；这些限制不冒充 T4 的 network/diagnostics/performance 定向 Gate。 |
