# LCC-M1-T5：Body Presence Playable 与里程碑验收

> Status: Guide
>
> 工作包：LCC-M1-T5（集成、Playable、独立审查）
>
> 状态权威：[LCC-M1 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md)
>
> 执行文档状态：`Verified`
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

Dragon14 正式 Playable 证明玩家会被胸腹/尾根/肩颈稳定软推出，平移与原地转身有体积感，墙边安全，翼膜/尾尖/头尖仍不常驻阻挡；自动化、网络、性能和独立审查闭合。

非目标：不引入部位伤口/切断/IK，不扩大为任意 animated Hurtbox 硬碰撞，不改 root distance语义。

### 1.1 开工 API 复用表

| 验收需求点 | 复用的既有 API / 模块 | 本任务处理 |
| --- | --- | --- |
| 固定 Tick / 角色运行时 | `RuntimeHost`、`RuntimeFrame`、现有 `GameSlice` / CharacterTest composition root | 只由现有 PlayMode 入口和测试推进，不建立第二生命周期。 |
| 玩家意图 / 指令 | `RuntimeCommandBuffer`、CharacterTest command factory / existing input adapter | 复用既有命令路径驱动角色；测试不直接改玩法 authority。 |
| 回放 / snapshot / rollback / hash | `CharacterTestNetworkSnapshotParticipant`、现有 replay validation 与 state hash | 只复测 rollback+1、network/replay parity；不新增 schema、wire、回放器或存档系统。 |
| Save / Restore | 现有 snapshot restore 与 package identity 重建路径 | 本任务没有新的可保存状态，因此不建立 `RuntimeSaveState` 或第二套 Save UI；restore 验收复用现有 snapshot。 |
| 移动 / 静态障碍 / Presence 查询 | `CombatKinematicMotor`、`CombatPhysicsWorld`、M0 separation API、CharacterTest Motion / Body Presence runtime | 墙、角、窄通道和软接触只以框架物理与 Motion 结果为权威；禁止 Unity Physics 权威。 |
| Hurtbox / Active Trace / damage / pathfinding | 现有 CharacterTest Combat Trace、Hurtbox Sync、damage 与导航查询 | 仅做语义回归，Presence 不替代或扩展这些系统。 |
| 资源 / Dragon14 binding | `CharacterTestPlayableCharacterCatalog`、generated package source、compiled geometry binding | 复用正式 Dragon14 v4 package；不建测试专用资源目录或平行配置源。 |
| Debug / Game View | 现有 `CharacterTestLargeCharacterPresenceDebug`、Presentation 与 Developer Debug projection | 只切换并观察四层既有 overlay；不新增正式 player-facing UI、OnGUI 或第二 diagnostics 模型。 |
| SceneFlow / 可玩入口 | 现有 CharacterTest 正式场景和 composition root | 本次验收不增加场景状态或切换，因此不新建 `AppFlow` / `SceneFlow`；若既有入口不足则停止回报，不以测试场景替代正式 Playable。 |

## 2. 写入所有权

- Owner：新增 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/PlayMode/CharacterTestLargeCharacterPresencePlayModeTests.cs`；只扩展现有 `CharacterTestDragon14NaturalAttackPlayModeTests.cs` 与 `CharacterTestLargeCharacterPresenceDebugTests.cs` 的 M1 回归；必要的正式场景/测试驱动、接口/能力表事实回写和验收证据索引。
- 正式场景/资产通过 Unity Editor/MCP写；不得手改序列化文本。
- 独立审查代理只读；父里程碑/总台账由主代理更新。
- 核心缺陷回派 T1～T4 owner，不在验收任务跨层重写。

## 3. 验证与验收矩阵

### 3.1 性能验收门禁

- 派发前冻结硬门：同一完整 `8 characters x 4 Presence x 18 Hurtbox` warmed 路径连续采样 `4096` 帧，steady managed GC 必须为 `0 B`，且 P95 必须低于 60 Hz 单 Tick 预算 `16.667 ms`。
- 回归观测基线：T4 实测 median `1.215 ms`、P95 `2.017 ms`；只用于本任务前后对照，不另猜 Body Presence 子系统配额。
- T4 的调度尖峰 max `33.194 ms` 不作为单独失败条件；本任务不得据此调整生产算法。容量、overflow/failure 与 clamp 仍按冻结合同分别报告。

- [x] 平移、原地转身、相向、同中心、质量差、不可推动/死亡、spawn/despawn、rollback+1 tick。
- [x] 墙边、角落、窄通道：不穿静态障碍、无无界速度；允许明确有界软重叠。
- [x] Presence关闭/缺失包保持Hard Root旧行为；Hurtbox、Active Trace、damage和pathfinding无语义回归。
- [x] Dragon14 bite/tail sweep/tail spin只在动作窗口伤害；静止翼膜/尾尖/头尖不阻挡。
- [x] overlay四层、contact normal/pressure/clamp可观察；正式 Game View 直接证据保存。
- [x] focused/regression、network parity、8角色性能/0 B、Unity Console 0有原始结果。
- [x] 只进行一次最终独立只读审查，范围限可复现 P0/P1；P0/P1=0。P2/P3 不扩张为编码或循环评审；接口/能力表仅声明实际 Framework/Runtime/Playable 等级。

## 4. 停止与晋升

异步测试启动、源码存在或单张Gizmo图不能验收。任何required场景未运行、网络不一致、墙穿透或steady GC>0均阻止 `Verified`。验收代理按统一格式回报，主代理复核并更新M1/总台账；M1 `Verified` 后才解除M2。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区内实施；未提交，保留既有非 LCC 修改。 |
| 实际修改文件 | 新增 `CharacterTestLargeCharacterPresencePlayModeTests.cs` 与 `.meta`；T5 发现的非阻挡目标回归按 T2 owner 窄修 `CharacterTestCharacterContactResolver.cs` 和 `CharacterTestLargeCharacterPresenceContactTests.cs`；新增 `Docs/Evidence/LCC/LCC-M1-T5/` 直接画面；本文及父/总台账、能力/接口文档按事实回写。未改 scene、profile、snapshot schema 或 wire。 |
| 测试与原始证据 | 最终 validator `5c3cd4c3549a40bc88c8a449342e4b7f`：完整 refresh/compile/domain reload/editor ready，Console 0；新增 PlayMode 整类 `815412999de744d7a2d8be27b314df40`：3/3；Authority `b88b34c713914ca0881f9b26ee4519c3`：8/8；Replay/Debug `2aaaca95a5f340d4a25328f1181a7f38`：4/4。T2 回修 validator `0e71867fd4d642a9a7ec4b280fe58b0f`、PresenceContact 7/7、MotionSafety 6/6、Dragon14 BakedSocket PlayMode `365b8070aab04ba1b20c09770f976022` 1/1。 |
| 集成 / 回滚结果 | PlayMode 使用正式 Dragon14 v4 package；覆盖平移/Yaw、相向/同中心、质量/不可推动/死亡、spawn/despawn、rollback+1、墙/角/窄通道、Hard Root fallback、四层诊断、idle 无 damage/trace 与自然攻击窗口。非阻挡训练目标只从 Presence eligibility 排除；不影响 `CanBePushed=false` 的阻挡接触。 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Candidate Complete` → `Verified`；核心缺陷回派原 owner 后复跑通过，自动化、正式场景、性能和文档闭合；唯一终审 P0/P1=0 后接受。 |
| 集成者 / 独立审查者 | T5 验收子代理实现与取证，T2 owner 仅回修原边界，主代理复核 diff/原始结果/画面；不同子代理完成唯一一次最终只读审查，P0=0、P1=0、P2/P3=0。 |
| focused / regression | 新增 3 个聚合 PlayMode 全过；直接 Dragon14 bite/tail sweep/tail spin 回归恢复 1/1；Presence contact/motion、authority/network/replay/debug 全部 focused 通过。未执行全仓测试，符合冻结的受影响范围。 |
| Dragon14 Playable / Game View | 正式 `Assets/Scenes/CharacterTestScene.unity` 经既有“开始”入口生成 Dragon14，退出后 `isDirty=false`、Console 0。主证据 `Docs/Evidence/LCC/LCC-M1-T5/lcc-m1-t5-multitarget-000003-characters-panel.png`：标准 2560×1440 Game View 同框显示 Dragon14、Characters overlay、四层全 on、Presence Sync Success 与固定容量 Contact Build。彩色几何 Gizmo 未进入标准 Game View capture；几何分层由同一 overlay 和 focused snapshot/Gizmo 投影测试证明，不把候选图冒充彩色 Gizmo 画面。非零接触行为由正式 binding PlayMode 矩阵证明，停止线内未通过持续人工按键另取一张非零 contact 截图。 |
| network / NoAlloc | 当前代码完整 8角色/32 Presence/144 Hurtbox、64 warm-up、4096 帧：median `1.12 ms`、P95 `1.687 ms`、max `25.1 ms`、GC `0 B`；raw/pair/constraint=`28/28/224`、iterations=3、clampPeak=224、overflow/failure=0/0。低于 60Hz `16.667 ms` P95 门槛；rollback/hash/replay/package identity 在 Authority/Replay jobs 复跑通过。 |
| 文档 / 能力表 | `Docs/CAPABILITIES.md`、Combat/Network/Character Application 接口与 LCC 状态台账已按实际 Runtime Slice/Playable 边界回写；不声明 M2～M4 能力。 |
| 结论 | `Verified`；M1 Gate 已闭合。保留“无持续人工输入视频/非零 contact 截图、无彩色 Game View Gizmo”的证据限制，但正式场景集成与动态 PlayMode 证据组合足以验收，不触发额外编码或评审轮次。 |
