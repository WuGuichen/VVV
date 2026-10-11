# LCC-M3-T6 Acceptance Evidence — 2026-08-25

> 状态：`Verified`（唯一独立终审发现两项 P1；均已整改并由主代理复核关闭，当前未关闭 P0/P1=0）
>
> 证据边界：实现者验收与整改记录，不是第二次独立审查；未更新 LCC-M3 父状态或总台账。

## 1. 结果摘要

- 现场重新确认的最高正式 CharacterTest schema 为 `30`，本任务启用下一可用 schema `31`。
- schema31 只扩展既有 Body Part authority entry，使 `StructureState=Severed` 可进入 capture/apply/clone、full/owned/non-owned hash、rollback、replay 和 late join。最小记录仍是 entity/definition identity、双轴值、integrity、component/entry revision、definition revision/content hash 与 `LastAcceptedHitFrame` witness。
- 不序列化 active leaf mask、Hurtbox/Zone validity 或 priority、Anchor/Planner 派生选择、presentation、VFX、SFX 或 view-only limb。
- schema30 原始整数 `2` 的 shape validation 明确拒绝；schema30 canonical legacy restore 将其映射为 `Broken`，不能借 `Enum.IsDefined` 自动接受 Severed。future schema、envelope/document schema 不一致和 definition source identity mismatch 均在 authority commit 前失败关闭。
- rollback/replay/late join 恢复 Severed state/revision，但 transition/presentation event 为瞬态，不写 snapshot，也不重放；下一帧 command/result hash 与恢复分支一致。
- 唯一独立终审原始结论为 `Not Verified`（P0=0、P1=2、P2=1、P3=2）。两项 P1 已按原 owner 整改：schema31 的非 `Severable` 篡改失败关闭，以及 schema31 restore 在 Planner 消费前完成 Hurtbox/Zone 重建并清理失效 tail lock/reservation。主代理随后只读核对源码并直接查询两条缺陷 job 与 16/16 聚合结果，关闭两项 P1；当前未关闭 P0/P1=0。未发起第二轮独立终审。

## 2. 本任务实际写入

生产与测试：

- `Assets/Scripts/MxFramework/Game/CharacterTest/Network/Snapshot/CharacterTestNetworkSnapshot.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Network/Snapshot/CharacterTestBodyPartStateNetworkSnapshot.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterMotionSystem.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterRuntimeModule.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Combat/CharacterTestDamageZoneSelector.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs`
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerCommandModule.cs`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestBodyPartStateNetworkTests.cs`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestReplayTests.cs`

接口、能力与验收证据：

- `Docs/Interfaces/Network.md`
- `Docs/Interfaces/Combat.md`
- `Docs/Interfaces/CharacterApplication.md`
- `Docs/CAPABILITIES.md`
- `Docs/Guides/CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T6_ACCEPTANCE.md`
- `Docs/Evidence/LCC/LCC-M3-T6/ACCEPTANCE_2026-08-25.md`

`CharacterTestReplayValidation.cs` 经只读复核后未为任务名强改：现有 `CharacterTestReplayFrameDriver.RestoreGameplayBootstrap -> ApplyRollbackSnapshot` 已自动经过 schema31/outer-document mismatch/restore preflight。

工作树包含大量 T1～T5 与无关既有修改；本任务未 stage、commit、push、清理或改写这些状态。

## 3. 唯一独立终审与两项 P1 整改

唯一独立终审已结束，原始结论为 `Not Verified`：P0=0、P1=2、P2=1、P3=2。实现者没有执行第二轮独立审查；以下只记录整改事实与 focused 结果：

1. schema31 `TryCreateNetworkEntry` 现在要求 `StructureState=Severed` 同时满足 `definition.Severable`。篡改用例把一个真实 Dragon14 非 Severable definition 改为 `Severed/currentIntegrity=0`，restore/preflight 失败；失败前后 character entity、BodyPart component引用、character/body canonical authority与full hash均不变。精确 job `35f09725f0754df28feea68544a7118a`：EditMode `1/1 passed`，`2.0809862 s`。
2. schema31 restore 的顺序现在是 BodyPart commit -> 既有 Hurtbox `SyncAll` -> Zone rebuild -> topology-only restored state sweep -> 下一 Planner。没有新增 topology manager：Character runtime只提供到既有 motion/hurtbox owner的窄同步入口；reservation复用 `ReservationAuthority.SweepInvalid`，lock复用 selector 的既有 exact target/binding/member witness predicate。失效 tail witness在任何 MoveToTarget/PrimaryAttack分支消费前清理，存活 `zone.core` witness仍可跨rollback保留。
3. 真实 Physics schema31 late join与rollback精确 job `d7569d67d7da4acabfeab1356b3eb721`：EditMode `1/1 passed`，`2.8836289 s`。两条restore路径在返回时均为15 leaves、`zone.tail` authored identity保留但unavailable/0 member、`zone.core`与`zone.head`仍available、tail lock/reservation已释放、transition未重放；相同 `+1` command 均接受且result hash一致。

真实定位失败未隐藏：初始 job `3ad05f5381c343d0aefd26b0cac69c43` 与诊断 job `90f540b6e244431b86bbbc448a2f55c4` 均为 `1/1 failed`，共同证明 MoveToTarget 分支下 restored tail lock未被selector消费，因而不能用强迫PrimaryAttack的fixture掩盖。整改改为restore后无条件 topology-only sweep。首次聚合 `67a3a48bc1cc493d9f6b7f897266b597` 为 `14/16 passed`：它进一步发现schema29 legacy被误扫及旧schema31测试使用伪 `zone.test`；最终实现把新sweep严格限定为schema31，并把parity fixture改成真实 Physics、可存活 `zone.core` witness，未放宽schema31 fail-closed。

P2/P3仍按第9节的接受限制保留；本次没有新增 production sever caller、detached resource、VFX/SFX/limb或性能阈值。

## 4. 最终 focused network / replay

最终精确 filter：

- `MxFramework.Tests.Game.CharacterTest.CharacterTestBodyPartStateNetworkTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestReplayTests.ReplaySchema31_SeveredBootstrapRollbackPlusOneCommandHashesAndRestoresSilently`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestReplayTests.ReplaySchema31_GameplayBootstrapSchemaMismatchFailsClosed`

整改后最终 Unity job `f5b98dcce92d435bb15d782957fba152`：EditMode `16/16 passed`，`0 failed`，`0 skipped`，`7.47585 s`。候选阶段 job `62a44bf5d7584a2fbcbe88a612c87fe7` 的 `14/14 passed` 被该更新后聚合取代。

覆盖事实：

- schema31 capture/canonical clone、full/owned/non-owned hash、Severed state/revision/accepted-hit witness。
- 非 Severable definition不能由schema31 raw `Severed`篡改绕过；失败无character/body/hash partial mutation。
- schema30 raw `2` validator rejection与 canonical `Broken` restore；legacy/future/source mismatch无 partial authority mutation。
- schema31真实Physics late join/rollback在返回前重建15 leaves与Zone view并清理tail lock/reservation；真实可存活`zone.core` lock仍跨rollback保持。
- late join 恢复8个 Severed entity，`TransitionCount=0`、presentation projection `0`；双方 `+1` frame full hash一致。
- replay bootstrap 恢复 Severed、rollback+1 authoritative command/result hash一致；future bootstrap和schema30 envelope/schema31 document mismatch结构化失败。
- 更新后的完整 network class在最终聚合中为 `13/13 passed`；两个schema31 replay filter合计 `3/3 passed`。

## 5. T1～T5 紧凑集成矩阵

EditMode job `143c56464b2b48e8b0a8d6ea92c8adf6`：`62/62 passed`，`1.4162216 s`。只运行以下核心类：

- `MxFramework.Tests.CharacterRuntimeSpawn.CharacterBodyPartDamageRuntimeBindingTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestBodyPartStateRuntimeTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestHurtboxTopologyTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestSeveredTargetingInvalidationTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestActionPartStateDependencyTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestSeverancePresentationTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestCharacterDistanceM2SelectionTests`
- `MxFramework.Tests.Game.CharacterTest.CharacterTestCharacterDistanceM3AnchorRuntimeTests`

该集合覆盖 Broken→Severed 当前 hit/下一帧边界、重复 trigger、respawn、stable collider identity、15 tail-pruned leaves与zero-leaf fail closed、Zone lock/reservation/Planner重选、root-distance/CD联合选择不回归、tail sweep/spin gate与mid-action trace prune、missing socket诊断分离、bite继续，以及 presenter late attach/restore silent。

PlayMode job `42fa99441155431aa1989bfb9ccdbee0`：`1/1 passed`，`0.187463 s`；`ReleaseAndRespawn_UseCurrentSeveredStateWithoutLifecycleLeakOrReplay` 覆盖 release/respawn lifecycle，不重放 transient event。

未运行全仓测试。

## 6. Authoring / CharacterStudio

- `MxFramework.Authoring.Tests` 的 `character-body-part-damage` selector：process exit `0`，输出 `Character body-part-damage authoring tests passed.`
- Dragon14 CLI validate：process exit `0`，status `Ready`；仅保留既有 WarningOnly `CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED`。
- `Tools/MxFramework.CharacterStudio/scripts/smoke.mjs` 使用项目随附 Node `v24.19.0`：process exit `0`，输出 `CharacterStudio smoke ok`。
- smoke 覆盖 Current/Future raw只读、Dragon tail Severed三字段、create/edit/delete/reorder draft、Undo/Redo、revision-gated save、save/reopen canonical、普通 refresh 草稿保持、discard/compile gate；UI不是第二 authority source。

## 7. 性能

- stable Severed topology：`SeveredTail_StableWarmedSync_AllocatesZeroBytes`，64 warmup + 512次 `SyncAll`，15 leaves，断言 `0 B`。
- selector/joint projection：`TargetingInvalidation_WarmedSelectorAndJointProjectionAllocateZeroBytes`，4096次，tail不再投影且head保持，断言 `0 B`。
- action eligibility/trace prune：`EligibilityAndTracePrune_WarmedHotPathAllocatesZeroBytes`，64 warmup + 4096次，断言 `0 B`。
- focused performance job `691e598128f44cacbdfc766525708658`：`2/2 passed`，`0.45839 s`；复用既有八角色4096-frame warmed fixture与单次真实Dragon topology switch，不建立新benchmark框架。
- schema31 八角色低频 state/capture/apply 测量：整改后最终聚合输出 `elapsed_ms=10.9627 allocated_bytes=0 characters=8`；候选阶段聚合输出为 `26.3683 ms/0 B`。单独冷启动式运行 job `ac44166368da4faa9ad87cc434a11ad3` 为 `1/1 passed`，输出 `elapsed_ms=216.1316 allocated_bytes=0 characters=8`。这些数值只记录，不设置无依据阈值，也不把观测到的 `0 B` 升格为该低频路径门禁。
- 15 leaves / tail Zone unavailable 的派生重建由 T2/T3 formal fixture 证明；八角色低频用例限定为 state + network hash，未把无Physics的空副本fixture误当Hurtbox benchmark。

构造期间的非验收诊断job也保留事实：`ccffb83376ba4f13822c1481df530e72`、`e674f05b5423439b9509a47818f62e17`、`513b661d432d4aab8a9728078a506242`、`18f3f58ef0b744539bd429c9438a087f` 和 `4a4c5cfce4dc40a892c039c2bb441cc1` 均为 `1/1 failed`；前四个停在无Physics GameSlice的“restored geometry missing”，最后一个证明连source都没有warmed geometry。该定位没有放宽deterministic hash，也没有增加生产分支；按冻结指令收窄八角色测量到state/network hash，并由既有T2/T3正式fixture承担15叶/Zone派生证明。最终替代job见上文，全部通过。

## 8. Game View / 资源能力事实

复用 T5 Game View，不制造 T6 新视觉：

- `Docs/Evidence/LCC/LCC-M3-T5/GAME_VIEW_BEFORE_INTACT_STATUS_2026-08-25.png`：2560×1440，2,057,453 bytes，SHA-256 `acb6218093016774191f881d820ee630f39cffe4d4219f03720a329d0923860f`。
- `Docs/Evidence/LCC/LCC-M3-T5/GAME_VIEW_AFTER_SEVERED_STATUS_EDITOR_DRIVE_2026-08-25.png`：2560×1440，1,821,666 bytes，SHA-256 `3a4d02ffb6e84096e7ec376389b796f5cfd8d37787d8e545e30de64a321f9e89`。

before显示18 shapes、Intact、tail leaves `3/3`；after显示15 shapes、Severed revision 3、tail leaves `0/3`、`RuntimeSliceNoStableResource`、VFX/SFX/limb均为0且whole renderer保持。after是 Editor-only drive，不是生产 caller/player path。

## 9. 已知能力边界与候选问题

- **P2 candidate — production ingress**：当前没有生产 `StageAcceptedSever` caller或玩家路径，只有typed runtime ingress与formal tests。接受为本M3 Runtime Slice限制；owner为后续gameplay/action integration；完整Playable或发布门禁前必须接真实accepted fact并补玩家路径证据。
- **P3 candidate — visual resource**：Dragon14为单basemesh，没有稳定detached tail resource，因此结果为 `RuntimeSliceNoStableResource`，没有visual detach/VFX/SFX/limb。owner为Character assets/presentation；宣称完整Playable前必须提供正式资源与生命周期证据。
- **P3 candidate — low-frequency budget**：八角色低频路径记录冷/暖观测值，但没有冻结硬阈值。owner为后续performance budget；发布性能门禁前应在目标硬件冻结预算，不在本任务臆造阈值。

未扩展到head/wing severance，未开始M4 IK/terrain pose。

## 10. 编译 / Console

- focused实现与测试编译期间 Unity Console error count为 `0`。
- 整改后的唯一最终自定义 validator 调用在等待结果时明确失败：Unity plugin session `ff670c91-bdb1-4151-afbf-423f52689d30` 因domain reload断连且没有返回job/result，故按仓库规则执行一次显式失败回退。
- 回退 `validate_unity_scripts` job `03a13c1967184aa7bc65d5e4ef39d1ec`：`passed`；refresh requested/completed，compilation requested/started/finished，domain reload observed，Editor ready，Console `error_count=0`。没有第二次validator循环。

## 11. 主代理只读复核结果（不是第二次独立终审）

- 已核对 `TryCreateNetworkEntry`：schema31 的 `Severed && !definition.Severable` 在 restore plan 建立前失败关闭；篡改测试同时锁定 entity/component/canonical authority/full hash 无 partial mutation。
- 已核对真实调用顺序：`Commit -> SyncBodyPartDerivedTopologyAfterAuthorityRestore -> SweepInvalidTopologyStateAfterAuthorityRestore`。实现只复用 Hurtbox `SyncAll`、reservation `SweepInvalid` 与 selector exact member witness，没有 topology manager，也没有序列化 leaf/Zone 派生状态。
- 主代理直接查询 Unity job：`35f09725f0754df28feea68544a7118a` 为 1/1 succeeded，`d7569d67d7da4acabfeab1356b3eb721` 为 1/1 succeeded，最终聚合 `f5b98dcce92d435bb15d782957fba152` 为 16/16 succeeded；失败 job 仍只作定位证据。
- schema31 在 restore 返回前释放 tail lock/reservation并保留真实 `zone.core` witness；15 leaves、其它 Zone 和相同 +1 command/result hash 由聚焦测试锁定。schema29 legacy 不进入新 sweep。
- 唯一终审的 P2=1/P3=2 继续作为接受限制：无 production caller、单 basemesh=`RuntimeSliceNoStableResource`、无 detach/VFX/SFX/limb、无低频硬阈值。
- 结论：原两项 P1 均关闭，当前未关闭 P0/P1=0；T6 可晋升 `Verified`。未执行第二轮独立审查。
