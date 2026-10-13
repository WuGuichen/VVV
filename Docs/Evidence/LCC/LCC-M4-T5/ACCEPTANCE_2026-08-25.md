# LCC-M4-T5 Acceptance Evidence — 2026-08-25

> 状态：`Candidate Complete / Handoff Ready for Independent Review`。这是验收实现者证据，不是最终独立审查；父 M4、总台账与 `Verified` 状态不由本文件写入。

## 1. 验收边界

- 复用正式 Dragon14 generated package、现有 CharacterTest runtime fixture、Authority Pose debug snapshot、Hurtbox/Trace/Planner/PhysicalAudio consumer 与测试工具。
- 本任务只新增三个 feature-specific tests-only fixture（Playable、performance、单一 network acceptance）；不新增通用 overlay、pose provider/cache/scheduler、第二 pose frame/snapshot/revision 或运行时 authoring fallback。
- 禁止 Unity Physics/Animator/Transform authority readback。Animator/Transform 只作为正式 Prefab 的 presentation target；terrain probe、IK、Hurtbox 与 socket 数值全部来自 fixed-point authority frame。
- Dragon14 正式 rig 当前有 4 probes、12 channels、18 Hurtbox、4 Collider attachments、4 Socket attachments，但 `disabledActionTags=0`、四肢 `partStateId` 均为空、`ContactPoint config/attachment=0/0`。验收不猜测或补造这些正式数据。
- 正式运行时没有 M4 统一数值 overlay 开关；测试专用彩色 marker 只用于截图定位，不冒充生产 overlay。截图不能证明的同源关系由同帧数值断言承担。

## 2. 本任务文件

- `Assets/Scripts/MxFramework/Tests/PlayMode/CharacterTest/CharacterTestDragon14AuthorityTerrainPosePlayModeTests.cs` 及 Unity 生成 `.meta`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestAuthorityPosePerformanceTests.cs` 及 Unity 生成 `.meta`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestAuthorityPoseNetworkAcceptanceTests.cs` 及 Unity 生成 `.meta`
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestBodyPartStateNetworkTests.cs`（只迁移当前 capture 的 schema32 期望/名称；schema31 legacy 覆盖不变）
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestCharacterDistanceM1HitPolicyNetworkTests.cs`（共享 tests-only request 改用已注册 `light_attack/light_attack.hit_window` identity；不绕过依赖 gate）
- `Assets/Scripts/MxFramework/Tests/Game/CharacterTest/CharacterTestLargeCharacterPresenceAuthorityTests.cs`（v6 clone 透传现有 Authority Pose Rig identity；既有 hash/fail-closed 语义不变）
- `Assets/Scripts/MxFramework/Tests/PlayMode/CharacterTest/CharacterTestDragon14BodyPartDamagePlayModeTests.cs`（只把正式 Body Part Damage revision 期望从 1 更新为作者源当前值 2；其余断言不变）
- `Docs/Interfaces/CharacterApplication.md`、`Combat.md`、`Animation.md`、`CharacterAction.md`、`Network.md`、`Audio.md` 与 `Docs/CAPABILITIES.md`（只回写 M4 实际合同、能力等级与限制）
- `Docs/Evidence/LCC/LCC-M4-T5/ACCEPTANCE_2026-08-25.md`
- `Docs/Evidence/LCC/LCC-M4-T5/PERFORMANCE_2026-08-25.md`
- `Docs/Guides/CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_T5_ACCEPTANCE.md`（最终仅收口为 candidate/handoff，不修改父 M4 或总台账）

共享 dirty worktree 包含 T1～T4 与无关既有修改。本任务未 stage、commit、push、clean，也未回滚他人状态。

验收发现的三项生产缺陷均由原 owner 在原边界最小修复：T4 的 `CharacterTestRuntimeCharacterView.cs` 正式 Animator root exact path 解析、T4 的 `CharacterTestCharacterRuntimeModule.cs` 在 snapshot view sync 前重建现有 derived Animation，以及 M1 的 `CharacterTestCharacterBodyPresenceSystem.cs` 严格接受 geometry v6/6.0 并沿用 v5 的 Body Presence compatibility 语义。三处都不是 T5 实现者的生产写入。

## 3. 通用合同状态覆盖与正式 Dragon14 资产覆盖

### 3.1 通用合同状态

PlayMode fixture 覆盖 flat、uphill、downhill、step、uneven、no-hit、too-steep、out-of-reach、移动/转身，以及 flying、knockback、dead、generic action-disabled 与 Severed limb 合同。状态驱动只读 CharacterTest authority 组件，不用 presentation Transform 反推权威。

### 3.2 正式 Dragon14 资产

- 所有正式 pose、Hurtbox、claw socket 与 View 断言统一使用 `char.000003.animation.attack1a` 的 `localFrame=10`。
- Action component、terrain tick、Hurtbox attachment delta、`attack1a` 实际使用的 `rightClawStart/rightClawEnd` 与 View 同一链读取 frame 10；resolved frame 为 12 channels。正式 rig 虽声明左右各一对 Socket attachment，但 `attack1a` catalog 只含右爪两条 track，验收不跨 clip 拼接左爪伪同帧证据。
- frame 0 的正式 attack1a 右后肢在该固定平地 fixture 中为 `OutOfReach`；四条链共同可达窗口为 frame 6～14，因此验收基帧校准为 frame 10。这是 fixture/clip 校准事实，不是生产缺陷。
- 正式 Dragon14 的 `disabledActionTags=0` 与四肢空 `partStateId` 保持 fail closed。generic action-disabled/Severed 合同的覆盖与正式资产事实分开报告。
- tail Severed 后正式 Hurtbox 从 18 个稳定叶变为 15 个；不生成假 limb metadata、detached resource、VFX/SFX 或 contact identity。

## 4. PlayMode 候选运行与原始失败证据

初始化阶段的 tests-only 编译定位均保留但不计为测试结果：

| job | 结果 | 定位 |
| --- | --- | --- |
| `b451824ae5b0492bb1e4b6ec20f35e83` | initialization failed，0 tests | NUnit 版本不支持 `[NonParallelizable]`；改用现有 `[Parallelizable(ParallelScope.None)]` |
| `ebb0a380b681424294de96cdcdc643d8` | initialization failed，0 tests | tests-only 缺 `CharacterMovementMode` namespace |
| `431cd90b71db446a9b66fdb15f66423b` | initialization failed，0 tests | tests-only `SetMotionState` named arguments 与当前 API 不一致 |
| `0bf5a3d22e1a49eabdfce993c9389b22` | MCP initialization timeout，0 tests | job 在新 compile 前启动；随后真实 TestRunner 运行 3 项并揭示 frame 0 右后肢 `OutOfReach`，据正式 bake 校准到 frame 10 |

frame 10 后 PlayMode job `7a63452730834534bf5ae13621b90ca7` 完成真实 3 项运行：`1 passed / 2 failed / 0 skipped`，`1.9228847 s`。

- 通过：`FormalDragon14_EligibilityAndTailSeverance_FailClosedWithoutInventedLimbMetadata`。原始输出：`flying=Flying knocked_back=KnockedBack dead=Dead tail_severed_hurtboxes=15 formal_disabled_action_tags=0 formal_limb_part_state_ids=0 generic_action_and_limb_severed_contract=separate`。
- tests-only fixture failure：uphill 的 limb 2 返回 `InvalidProbe`。原因是首版宽薄 OBB 斜面 fixture 选到无效面；已只在测试中替换为按四个正式 probe cast 分离布置的斜面 patch，未改生产。
- 生产 defect：正式 Prefab 顶层为 `000003_character_preview/ModelRoot/BodyModel/000003_basemesh2/Dragon15_root/...`，authored bone path 从 `Dragon15_root/...` 开始，而 T4 View 原实现只在 Prefab 顶层执行 exact `Transform.Find`，导致 12 个正式 channel 无法落到实际骨。该缺陷已回派 T4 consumer owner；T5 不改生产，等待交接后复跑。

`7a634...` 的两类失败不作为通过证据，也不被后续结果覆盖或隐藏。

## 5. 最终 PlayMode / Game View

T4 owner 修复正式 Prefab bone root 后，`ae58e2a5d9d1473fb3a5b9e460028821` 为 `2 passed / 1 failed / 0 skipped`，`1.8754685 s`：terrain matrix 与 eligibility/Severed 已通过；剩余失败是 tests-only 误要求 `attack1a` catalog 不存在的左爪 track。夹具改为只证明该 clip 实际声明的 `rightClawStart/rightClawEnd`，未补假 track。

随后 `bae12922b31a4334b26dc9694e7a6045` 在测试执行前遭 Unity TestRunner 基础设施异常：`PlayModeRunTask NullReferenceException`、`Too many instant steps ... ExitPlayModeTask`，XML 未更新，0 项有效结果。按授权仅调用现有官方 `TestJobManager.ClearStuckJob` 清理该 orphan job，并恢复未 dirty 的正式 `Assets/Scenes/CharacterTestScene.unity`；不修改 MCP package 或临时场景。

最终专用 job `bdd838f341804a8fbe87222f06e41454`：`3 passed / 0 failed / 0 skipped`，`2.1406873 s`。

- terrain matrix：`flat=success uphill=success downhill=success step=success uneven=success no_hit=NoHit too_steep=TooSteep out_of_reach=OutOfReach move_turn=success limbs=4 channels=12`；too-steep/out-of-reach 均为 2 limbs。
- eligibility/Severed：`flying=Flying knocked_back=KnockedBack dead=Dead tail_severed_hurtboxes=15 formal_disabled_action_tags=0 formal_limb_part_state_ids=0 generic_action_and_limb_severed_contract=separate`。
- 同帧数值：4 个 limb target/tip、`rightClawStart=(1262003,270001,1168006)`、`rightClawEnd=(1435647,272584,696093)`、Hurtbox `hurt.left_forelimb.capsule.0 center=(131500,1332000,1251002)`，正式 ContactPoint 数为 0。
- 截图绝对路径：`C:\UnityProjects\WGameFramework\Temp\CodexEvidence\LCC-M4-T5\LCC-M4-T5-Dragon14-Terrain-GameView.png`
- 文件：2560×1440，929,298 bytes；SHA-256 `EFD6E7E400CA821AD596A0471711CF55194A9996B5CBFF46EB90ECDE3B08CF2A`。
- 颜色：绿色=probe target，青色=resolved channel/tip，红色=resolved Hurtbox，黄色=该 clip 的右爪 sockets，灰蓝色=tests-only ground。画面可见正式 Dragon14 与四块不等高地面，但整体偏暗且中央带 Directional Light 的 Game View gizmo 图标；它只作为测试专用可视定位，不能单独证明数值同源或生产 overlay 品质。

## 6. Network / replay 闭合

既有 `CharacterTestAuthorityPoseNetworkTests` 覆盖 schema32 capture/apply/clone、full/owned/non-owned hash、legacy/future/source mismatch、rollback+1 pose 与 Dragon14 late join；`CharacterTestReplayTests.AuthorityPoseSchema32_*` 覆盖 replay command hash/bootstrap/mismatch。为把真实 consumer 链放在同一 rollback+1 分支，新增一个窄的 tests-only `CharacterTestAuthorityPoseNetworkAcceptanceTests`：正式 Dragon14 在 `attack1a` frame 35 checkpoint，+1 执行现有 Runtime AI Planner command，并读取现有 resolved pose、真实 Hurtbox、`DirectHitTracePlans` 的实际 `BakedSocketCapsule` 与 state hash。

原始运行：

| job | 结果 | 分类 |
| --- | --- | --- |
| `88a5acd838ca41ab83669ae5d8945fc6` | 1 failed | rollback empty slice 的 clock 未与 source 并行推进；tests-only fixture 问题，已最小修正 |
| `aff0cc4acc1a435a9d4c8f71d9454a27` | 1 failed | source/rollback 的 action36、Planner command、12-channel pose/revision 已相等；首次发现真实 Hurtbox LocalShape 不等 |
| `38a8fdaa594445d7a14630abfe0437fb` | 1 failed | 最小数值诊断确认非比较口径：source capsule A=`(126000,3363000,1498002)`、B=`(159000,3813000,1342002)`；rollback A=`(126000,3364000,1498002)`、B=`(158000,3813000,1342002)`，radius 均 `441000`，存在 1 mm rollback 差异 |

该 1 mm 差异由主代理判定为真实缺陷并回派 T4 owner。最终根因：rebuild rollback 会清空 Hurtbox 的 `EntityPoseState`；snapshot 已恢复 Action/Motion/Authority Pose，但 derived `Animation` 尚未重建，随后 Hurtbox 首次同步从 Idle 初始化，产生 1 mm transition/cache 偏差。T4 owner 仅在 `SyncNetworkSnapshotViews(frame)` 的既有链首行调用现有 `_animationSystem.Tick(_world, frame)`；没有换序其它阶段、增加字段/cache/reset/schema/容差或新抽象。

owner 交接后的窄回归：

- 新 acceptance job `724b5174c3fe4870848e37447e9cc641`：`1 passed / 0 failed / 0 skipped`，`7.0549433 s`。source/rollback Hurtbox capsule 精确相等：A=`(126000,3363000,1498002)`、B=`(159000,3813000,1342002)`、radius=`441000`；`schema32.rollback+1 frame=39 pose_revision=39 hurtboxes=18 trace_kind=BakedSocketCapsule trace_local_frame=36 planner_source=8 state_hash=-2010630302382664122`。
- 既有 rollback regression job `1427b590025a4de3b38ac8e799f8a7ba`：`CharacterTestAuthorityPoseNetworkTests.Schema32_RollbackPlusOneRecomputesIdenticalPoseHistoryAndHash`，`1 passed / 0 failed / 0 skipped`，`0.2905305 s`。

因此同一 rollback+1 分支的 resolved pose/revision、真实 Hurtbox、实际 BakedSocket trace、Planner command 与 state hash 已闭合；formal schema、full/owned/non-owned、replay/late join/source mismatch 仍由下述原聚合集合覆盖，不把单一 acceptance 冒充全部网络覆盖。

## 7. 聚焦回归

### 7.1 EditMode 原集合

不运行全仓。首轮原 27-filter EditMode 聚合 job `796584fe5f1b40398bf0fca4e2014ade`：`290 total / 280 passed / 10 failed / 0 skipped`，`63.964 s`。该 job 不含当时尚在缺陷交接中的新 network acceptance；10 个失败按原始 stack 分类为已 Verified 合同后的陈旧 fixture，不计 T5 通过，也没有降低 production fail-closed：

- 2 项 `CharacterTestBodyPartStateNetworkTests` 的 current capture 仍硬断言 schema31，实际 current wire 已为 schema32；schema31 legacy normalize/apply 覆盖本身有效。
- 4 项 `CharacterTestCharacterDistanceM1HitPolicyNetworkTests` 使用未注册的 `cd-m1.network.active-action` 直接注入 hit window；现被 M3 `PruneIneligiblePartDependentTraces` 在 snapshot 前正确 fail closed，`ActiveTraceCount=0`。
- 4 项 `CharacterTestLargeCharacterPresenceAuthorityTests` 仍按 geometry v5 clone/断言；v6 正式包要求 Authority Pose Rig identity，3 项 clone 被 identity gate 拒绝，1 项仍直接期望 v5。

经主代理批准，只做三组 tests-only fixture 迁移：current capture 期望/名称更新为 schema32；共享 hit-window request 改用 CodexFlow profile 已加载的 `light_attack/light_attack.hit_window` identity；v6 `CloneGeometry` 透传原 `AuthorityPoseRig` 并更新 v6 名称/诊断。legacy31、dependency gate、hash 与 invalid identity fail-closed 断言均保留。分组原始结果：

| group | job | 结果 |
| --- | --- | --- |
| A — current schema32 capture | `ea16b62f41094e069758be488cf80d71` | `2 passed / 0 failed / 0 skipped`，`18.3027401 s`；8 Dragon low-frequency switch 输出 `allocated_bytes=0` |
| B — registered action/event | `52964d22e41e47319723ce1d863ce138` | `4 passed / 0 failed / 0 skipped`，`4.0982405 s` |
| C — geometry v6 identity | `5b45e941cd1241478645879fa8d157c3` | `4 passed / 0 failed / 0 skipped`，`0.2368562 s` |

同一 27-filter 集合复跑 job `7857bb06ae3c4b359375b2c224ada014`：`290 passed / 0 failed / 0 skipped`，`59.6987766 s`。filter 仍为 geometry v1～v4 compatibility、Combat shape separation、LCC M1～M3、CD M0～M4、navigation graph/session/perception、PhysicalAudio、M4 terrain/consumer/network 与两项 schema32 replay；没有扩大为全仓。

### 7.2 冻结 PlayMode 集合

首轮只运行 5 个验收类：

- `CharacterTestDragon14NaturalAttackPlayModeTests`
- `CharacterTestLargeCharacterPresencePlayModeTests`
- `CharacterTestDragon14BodyPartDamagePlayModeTests`
- `CharacterTestSeverancePresentationLifecyclePlayModeTests`
- `CharacterTestDragon14AuthorityTerrainPosePlayModeTests`

job `d94890fef09c491cba058199b4c0ef65`：`12 total / 9 passed / 3 failed / 0 skipped`，无 TestRunner infrastructure 异常。三项原始失败保留：M2 测试仍期望正式 Body-Part Damage revision 1，而作者真源/生成物已为 revision 2；M1 的 motion matrix `BodyPresenceSystem.SyncAll=false`，contact diagnostics 因 `Presence Sync=Unavailable / Contact Build=SourceUnavailable` 不含 `normal/penetration/pressure/surface`。只读定位确认后二者共因是 M1 `ValidateBinding` 只承认 geometry v4/v5、未承认正式 v6。

经授权，T5 仅把 M2 tests-only revision expected 更新为 2。M1 Presence owner 在 `CharacterTestCharacterBodyPresenceSystem.ValidateBinding` 内增加严格 `format=v6 && schema=6.0`，把合法版本门改为 v4/v5/v6，并让 v6 `CompatibilityHardRoot` 与 v5 共用 revision=0/hash空/volumes空的 Missing 条件；没有读取 Authority Pose、增加 fallback/抽象或放宽 invalid fail-closed。owner focused 结果：

- PlayMode `ed1dd7f015c24ef18672b23003eae757`：原两项 Presence acceptance `2 passed / 0 failed / 0 skipped`（单项 6.346 s、4.032 s）。
- EditMode `b7983b69f8b64f5481eb8edd21eb8046`：v6 authored、v6 compatible Missing 与 invalid revision fail-closed `1 passed / 0 failed / 0 skipped`。
- `996c8e8afaf6418b9f475f4b7d88fb5e` 是外部脚本重编译打断的 cached orphan：1/2 已完成且无断言失败；按授权调用官方 `clear_stuck` 后终态 `failed / Job cleared manually (stuck or orphaned)`。该项只作基础设施证据，不计产品结果。

相同 5-class/12-test 集合最终只复跑一次，job `076edfb771df4120b7608375cdad3354`：`12 passed / 0 failed / 0 skipped`，`15.6954525 s`，status `succeeded`，无 infrastructure 异常。filters 未扩大，分别闭合 natural attacks、M1 Presence、M2 Body Part Damage、M3 Severance presentation lifecycle 与 M4 terrain-authority Playable。

原始 XML：`C:\Users\WGH\AppData\LocalLow\MxFramework\WGameFramework\TestResults.xml`（该路径会被后续 TestRunner job 覆写，job id 与本节分类为稳定索引）。

## 8. 接口 / 能力事实回写

- `CharacterApplication.md`：Authority Pose Rig v1 作者真源、geometry binding v6、v1～v5/Missing 兼容与 Dragon14 正式缺省。
- `Combat.md`：fixed-point single-best ground probe 与 CharacterTest M4 项目级 consumer 边界；Combat 不判断 eligibility，也不读取 Unity authority。
- `Animation.md`：baked base → probe/IK → secondary → 唯一 resolved frame/revision，以及 Hurtbox/socket/contact/View 同源与 PresentationOnly 隔离。
- `CharacterAction.md`：actual/preview `BakedSocketCapsule` 共用 resolved attachment delta；正式 attack1a 只声明右爪两条实际轨道。
- `Network.md`：当前 schema32 的 future terrain history、full/owned/non-owned hash、rollback/replay/late join/source mismatch，以及 derived frame 不入 wire。
- `Audio.md`：显式 ContactPoint config+attachment 才消费 resolved channel；正式 Dragon14 0/0，因此 live terrain contact audio fail closed。
- `CAPABILITIES.md`：通用 fixed-point terrain-pose pipeline 记为 Framework Feature；当前 Playable 证据限定为 Dragon14 四肢，并登记 tests-only marker、双 Sync 校准和非目标。

## 9. Console / Unity 脚本 Gate

- 所有 owner 交接与 focused rerun 完成后，清理前只读 `read_console(types=[error], page_size=100)` 返回 `total=1`。唯一条目是已登记的 Unity TestRunner 临时异常：`Saving results to: C:/Users/WGH/AppData/LocalLow/MxFramework/WGameFramework\\TestResults.xml`；没有 C# 编译、生产代码或其它用户错误。主代理据此批准只清理 Console 一次；基础设施原始 job/Editor.log 证据继续保留，清理后不再运行测试。
- 最终 integrated `validate_unity_scripts` 只调用一次。该调用在 domain reload 等待回包时返回 `success=false`、`Unity plugin session edda8404-5530-4856-b64e-d66d22984fbd disconnected while awaiting command_result`；没有 job id，也没有第二次调用 integrated validator。
- 按冻结规则执行唯一一次分步 fallback：`refresh_unity(mode=force, scope=scripts, compile=request, wait_for_ready=true)` 返回 `success=true`、`compile_requested=true`、`resulting_state=compiling`。重连后 Editor state 为 idle、PlayMode=false、is_compiling=false、is_domain_reload_pending=false、tests=false、external_changes_dirty=false，`last_domain_reload_after_unix_ms=1787639225453`。telemetry freshness flag 仍为 stale，但 Unity 工具已响应且随后 `read_console(types=[error], page_size=100)` 返回 `total=0`；该组证据按既定 false-stale 口径闭合最终 Unity 脚本/Console gate。

## 10. 限制与独立审查交接

- `overlay-on=N/A (no production M4 unified overlay)`；测试专用 marker 不计运行时 overlay 成本。
- 正式 Dragon14 仍为 0 ContactPoint config/0 attachment，不能证明 live PhysicalAudio terrain contact；只复测显式 adapter 合同与既有 fail-closed 行为。
- 正式 asset 没有 action-disabled tag 或 limb partStateId，因此相关通用合同由独立 tests-only fixture 覆盖，不能表述为正式 Dragon14 配置覆盖。
- 最终独立审查尚未执行；本实现者不会担任审查者，也不会写 `INDEPENDENT_REVIEW_2026-08-25.md`。全部 gate 完成后只把任务文档收口为 `Candidate Complete / Handoff Ready for Independent Review` 并停下。
