# MxFramework 能力清单

> Status: Current

## CharacterTest 单段战术行为卡（2026-09-06）

任务 03 已交付 Runtime Slice：版本 1 声明源以当前装配 LightAttack 序列的正式起手解析绑定动作，复用任务 01 有效摘要及任务 02 只读执行产物/共用 Host，直接驱动正式移动、独立转向、动作 Gateway 和 Combat。支持最佳距离接近或不提供基础接近、编译失败保留旧产物但拒绝失效启动、待用/Gate/活动动作状态恢复及确定性对照。最终 51/51 专项测试和完整 Unity 编译/域重载/Console 0 error 通过。

制作入口和范围见 [CharacterTest 单段战术行为卡](Guides/CHARACTER_TEST_TACTICAL_CARD.md)，证据见[任务 03 执行记录](../StorySource/玩法基石/任务/任务03执行记录.md)。双装配运行包括真实 301/940000 与明确的特殊参考靶；不宣称通用动作/目标、完整编辑器、已发布卡组或 Playable 场景已交付。

## Platform Core + Unity Host Adapters (2026-07-20)

MxFramework 已有 zero framework dependency、`noEngineReferences=true` 的 `MxFramework.Platform` Core。当前公开能力包括 normalized environment facts、typed FileSystemPath/URI/ProviderOwned location result、fixed-capacity lifecycle buffer、typed permission query/operation contracts、completed permission operation、`IWallClock`、`SystemUtcWallClock` 和 UTC fail-closed validator。

Lifecycle buffer 在初始化后 report/drain NoAlloc：focus 与 suspend 保持正交，duplicate callback 去重，quitting sticky；普通 edge 按序进入预分配 ring。overflow 会把未 drain 的完整 revision range 折叠成 queue 外 sticky `StateResyncRequired`，保留饱和 lost/drop count 和 suspend/quitting evidence，不静默丢弃关键事实。Permission 的 Unknown/default 不等于 pending，built-in Microphone ID 固定为 1；wall clock 只允许非权威 metadata，Local/Unspecified fail-closed，不能参与 ordering/generation/replay/hash。

`MxFramework.Platform.Unity` 已提供 cached environment/location adapters、composition-owned lifecycle reporter/proxy，以及单 active request、main-thread Pump 的 microphone permission service。StreamingContent 保留 filesystem/absolute-URI 语义；initial lifecycle revision 为 0 且无 synthetic edge，callback 只转发 facts；cross-thread report fail-closed；不可取消的 Unity authorization dialog 不伪造 Cancelled。warm lifecycle callback/report/drain 与 pending permission Pump/property polling 的 focused allocation tests 断言 0 B。

当前尚未提供额外 target SDK provider、`ApplicationStartup.PlatformBridge` 或 consumer migration；现有 VoiceInput permission、Persistence/Runtime SaveState/Gameplay Trigger 调用保持不变。Microphone permission backend 按平台选择：Android 使用 `Android.Permission` callback result slot，iOS/VisionOS 使用 application authorization async，WebGL/未分类 runtime 为 Unsupported，desktop 为 NotRequired；Android false query 在 callback 前不伪造 Denied。公共边界见 `Interfaces/Platform.md`，架构决策见 `Decisions/ADR-009-platform-contract-authority.md`。

## SaveGame Persistence Core Phase 1 (2026-07-19)

`MxFramework.Persistence` now provides a zero-dependency noEngine Persistence Core: frozen profile/slot/operation IDs and limits, monotonic generations, structured errors including additive provider-facing location/atomicity/permission/quota/I/O codes, explicit operation result validity and terminal/cancellation capability, exact canonical v1 envelope JSON/framing, payload and envelope SHA-256 integrity, tombstones, Current versus ExactGeneration reads, and a thread-safe in-memory compare-generation provider. Public byte/metadata/list boundaries are ownership-isolated. Commit/delete cancellation is rechecked immediately before publication, fault callbacks run outside the store lock, and CAS is revalidated after reentrant callbacks. Successful commit/delete results can carry a typed non-fatal retention-cleanup warning without rolling back publication; the in-memory provider remains warning-free. Public provider diagnostics use logical paths only and do not expose physical storage locations. Core defines no default timeout policy. Default listing hides tombstones while `includeDeleted` exposes an ordinal diagnostics view. Runtime restore/compatibility, Platform/Settings policy, Trigger, FileSystem, compression/encryption, and live-session bridges remain outside this phase. Public API and tests are documented in `Interfaces/Persistence.md`.

## SaveGame Persistence Runtime Bridge Phase 3 (2026-07-20)

`MxFramework.Persistence.RuntimeBridge` now provides a noEngine bridge depending only on Persistence and Runtime. `RuntimeSaveGameService` captures durable save plans exclusively through `RuntimeCheckpointBarrier`, validates authoritative root/build provenance, serializes bounded Runtime UTF-8 JSON, creates the v1 envelope, and delegates CAS commit to an injected `ISaveGameStore`. Its prepare-load path re-reads and validates the selected envelope, fails closed on unknown/incompatible build decisions, verifies mirrored Runtime root facts, and creates a per-load sibling of the frozen live template `RuntimeSaveStateRestoreProfile` for migration and exact descriptor validation without invoking restorers. That sibling becomes the candidate composition identity; it does not reuse the live coordinator or its participants.

Prepared restore is candidate-only: `IRuntimeSaveGameRestoreTarget.TryAcquireRestoreLease` combines validation and exclusive acquisition atomically, and `IRuntimeSaveGameCandidateRestoreLease.TryCompleteRestore(F, F+1, ...)` freezes the completed/next-frame handoff exactly once. Incomplete disposal must permanently poison the candidate. The bridge never publishes or Ticks a candidate, does not depend on Application/FileSystem/Platform/Resources/Settings/Gameplay/Trigger/Unity, and treats all participant custom state as opaque.

Focused verification covers the RuntimeBridge contract/service suite, Persistence Core, Runtime SaveState/orchestration/checkpoint/root-header, and Trigger save/replay/outbox/config compatibility. Exact delivery-commit counts and code-standard evidence are recorded in the PR closeout instead of freezing candidate-build numbers here. This is not a Playable or production-safe live-load claim: Application candidate build/validate/atomic publication, Resource/Scene handoff, Unity Boot/loading UI and suspend policy remain pending.

## Gameplay Trigger Runtime Phase 1 + 19.2A-H2 + 19.3A-D + 19.4A-B Complete (2026-07-20)

MxFramework 现有独立 noEngine `MxFramework.Gameplay.Triggers` kernel。它接受 complete occupancy、complete `Fix64` scalar sample 和 ordered/unordered typed Signal batch，确定性归约 Entered/Stayed/Exited/Pulsed，再执行 postfix condition、count/cooldown/stay/once-per-presence policy、explicit exclusive arbitration 和 `Prepare -> Commit/Abort` action reservation。运行时按显式 capacity 初始化，steady submit/evaluate 路径 NoAlloc；错误返回 numeric result/diagnostic，不用 Unity callback、EventBus 或任意委托作为 authority。

当前已通过 `GameplayTriggerDeferredSystem` / `GameplayTriggerCommandSystem` / `GameplayTriggerEvaluationSystem` 接入既有 Gameplay pipeline；`GameplayRuntimeModule` 与 `StoryRuntimeModule` 仍分别是自身 command buffer 的唯一 drain owner。19.2B-D 已交付 fixed `RuntimeCommandEnvelope`、Signal/occupancy/scalar batch codec、sealed source registry、External/TrustedDerived submission gate 和可信 exogenous stream filtering；unknown source/command、provenance mismatch 与 external raw lifecycle mutation 均 fail-closed。Runtime gate 现在还为 hard-bounded target 提供 caller-span atomic batch admission，失败不会改变 pending/frame/sequence。19.2E-F2 已交付 catalog/authority hash、Runtime checkpoint barrier、Trigger schema-3 SaveState participant 与 atomic restore；19.2G1-G2 已交付 Trigger-owned internal deferred authority 和 reliable cross-module outbox。19.2H1 提供 fixed-capacity typed presentation event source、唯一 `Diagnostics/1000` Router drain 与独立 bounded observer fanout，presentation pending/drop/enabled 状态不进入 authority hash、SaveState、replay 或 checkpoint quiescence。19.2H2 现提供 schema/config-bound header、exact composition bootstrap、fresh 与 checkpoint F+1 suffix、可信 exogenous typed observation admission、逐帧一次 Host.Tick/hash、typed fault context 和 derived trace/G1/G2 再生成。19.3A 新增独立 noEngine deterministic config compiler：typed authoring + trusted retired/source/action registries编译为canonical runtime catalog plan，输出used action projection、capacity requirements与hash；actual sink/internal descriptor在bootstrap注入，actual Host/outbox/transfer/module事实在Created composition gate验证。19.3B 新增独立 deterministic Combat sensor bridge，在 `Simulation/950` 生成 trusted-derived complete occupancy。19.3C1 新增 Gameplay Buff sibling bridge：Apply/Remove action 通过 Gameplay-owned fixed reservation lane 在 `Simulation/1000 -> Resolution/50` same-frame 投递，terminal result 以 numeric correlation relay 为 Trigger `ActionResult`；request/result 不进入 hash、SaveState 或 replay，bridge-owned non-mutating 64-Tick pipeline 为 0 B。19.3C2 新增 Gameplay component command sibling bridge：Destroy、Attribute Set/Add 与 direct Ability Cast 以四种 `ReliableDeferred` action 经 hard-bounded trusted-derived Runtime command gate 投递，目标 consumer 发布只读 numeric terminal result，再严格关联为 Trigger `ActionResult`；派生 request/result 不进入 hash、SaveState 或 replay，retained/accepted/result-validation steady path 均验证为 0 B。19.3C3 新增单一 Story RaiseTrigger reliable sibling bridge：bounded admission 与 Story module 使用 exact default validator 实例，Story-owned numeric terminal result 严格 relay 为 Trigger `ActionResult`；派生 command/result/correlation 不进入 hash、SaveState 或 replay，bridge-owned preflight/retain/accept/relay steady path 验证为 0 B。#732 新增 Unity-enabled occupancy adapter：immutable catalog 校验 compiled provider/capacity contract，zone/subject components只写 fixed contact rows，持久 `PreSimulation/0` module 按 SourceBindingId 每帧原子提交 complete Exogenous occupancy；多 Collider按logical GameplayEntityId去重，Disable/Destroy/scene unload重试final empty，ReplayDisabled抑制live input。

19.4A 已交付 `Assets/Scenes/GameplayTriggerPlayable.unity`：Combat motion/occupancy 驱动 checkpoint、door 和 trap Trigger，door 经 reliable Story `RaiseTrigger` 打开，trap 经 same-frame Gameplay Buff bridge累计 activation并刷新 slow；UI Toolkit HUD 提供方向 held/pulse、`CAPTURE`、`RESTORE`、`RESET`，键盘对应 `WASD`/方向键、`E`、`F6`、`F4`。Capture只写 `GameplayTriggerPlayableSession` 的单个内存 `RuntimeSaveState` slot，Restore在fresh candidate中验证并原子换入，Reset重建默认composition并清slot；该slot不是磁盘/profile级durable persistence。canonical input trace与capture suffix hash覆盖本地replay/restore组合，但Playable不提供跨进程network/replay UI。

19.4B 新增独立noEngine `MxFramework.Gameplay.Triggers.NetworkBridge`：protocol 2 kinds 10..13使用caller-owned bounded binary wire，按authenticated transport metadata、connected session/peer与Gameplay ownership授权白名单Exogenous observation；atomic frontiers、canonical profile、Gameplay component exact codec coverage、Gameplay 100 -> Trigger 200 snapshot、late join F+1、record/restore跨participant原子rollback/resync与partial-hash mode均已交付。固定seed规模档在120 warm-up + 480 measured中为median/P95/max 1.519100/1.569100/1.613900 ms，Trigger与bounded wire+rollback 0 B，snapshot 132752 B，满足冻结预算。最终新增network namespace与指定Network/Trigger回归298/298通过。

Gameplay Trigger Runtime父计划与整个19.4至此完成。仍不提供JSON importer、active-session hot reload、通用Unity action bridge、durable persistence、matchmaking、Relay、transport encryption、credential/account、lobby UI或CharacterTest protocol migration；19.4B所称认证仅是transport/session独立提供并由runner核对的session/remote/local/direction metadata与ownership授权，CRC不提供身份认证。接入边界与公共API见`Interfaces/Gameplay.Triggers.md`。

Runtime AI Lab 时间轴现在把 `selectedFrame` 定义为当前操作帧，而不是 Latest 的别名。普通历史播放会逐帧回放已有记录，到 Latest 后继续实时模拟；历史帧只有显式选择 `从此重算` 后才能生成新 Authority 数据。首次成功捕获的新 Tick 会原子截断旧未来并建立唯一的新时间线；`推进 +1`、未来拖动和重算播放共用同一个 internal 单帧生成原语。该状态仅属于 Lab session，不修改公共 API、SaveState、network、正式 replay wire 或 gameplay hash。

## Items Lifecycle / Inventory / Network (2026-07-10)

MxFramework now has a noEngine Items lifecycle foundation. `MxFramework.Items` defines item identities, localized text keys, kind, tags, stack policy, resource refs, profile refs, duplicate validation, built-in id constants, and stable definition hash; non-equipment definitions must provide a positive max stack count, while equipment defaults to one. `MxFramework.Items.Loot` resolves deterministic grouped loot tables into `LootItemQuantity[]` through `IDeterministicRandom`; it does not mutate inventory, spawn world objects, or read Unity assets. `MxFramework.Items.Runtime` provides slot-based inventory containers, stacks, owner/container ids, request-store command execution, typed runtime events, deterministic inventory hash, SaveState JSON under `ItemRuntimeIds.InventoryModule` (`mxframework.items.inventory`), and atomic `TryInsertStack` planning with merge-then-first-empty behavior. Only non-instance stacks with equivalent custom-state envelopes may merge; invalid SaveState payloads are validated in a temporary store and leave live inventory unchanged. Supported inventory operations are add, remove, move, split, merge, swap, insert, and diagnostic-only use; failed operations are atomic. `MxFramework.Items.Acquisition` adds the low-frequency direct-grant / purchase-like transaction base: request idempotency ledger, cost removal, grant insertion, atomic dry-run/commit, command request store, deterministic hash, and SaveState JSON under `ItemRuntimeIds.AcquisitionModule` (`mxframework.items.acquisition`).

`MxFramework.Items.World` owns noEngine active world item state: `WorldItemStack`, owner/access policy, logical position, spawn/expire frames, spawn/pickup/expire commands, events, deterministic hash, and SaveState JSON under `ItemRuntimeIds.WorldItemsModule` (`mxframework.items.world`). Pickup is atomic: the world item is removed only after access, expiry, destination container, and inventory insertion all succeed. `MxFramework.Items.Network` is an optional network bridge: it keeps catalog/profile/resource data out of high-frequency network payloads, uses compact authoritative inventory commands, owner authorization, bootstrap/resync snapshots, definition hash validation, and rollback restore/replay helpers. `InventoryRollbackParticipant` caches the definition hash at construction and captures fixed-shape rollback states without allocations after warmup, so the definition registry must be fully loaded before participant creation and remain unchanged during the session.

Character equipment can be represented as a special inventory container whose slot order matches `EquipmentSchemaConfig.Slots`. `MxFramework.Items.CharacterApplicationBridge` projects equipped item stacks through `CharacterItemEquipmentProfileConfig` into legacy-compatible `WeaponConfig[]` and `EquipmentLoadoutConfig`, so existing `EquipmentStateResolver` flows continue to work. `Assets/Scenes/ItemLifecycleShowcase.unity` is now a playable lifecycle validation scene for `ItemDefinition -> LootResult -> WorldItem -> InventoryStack -> EquippedItem`, with UI Toolkit buttons for loot, logical grid movement, pickup, selected-slot equip, unequip back to bag, save/load, reset, and hash compare; pickup diagnostics show target item, position, and out-of-range distance. Iron Sword now also validates an icon resource path through `ItemResourceRef` / `ResourceKey` / `ResourceManager` into UI Toolkit without putting Sprite data into SaveState, hash, or network payloads. V0 still does not add full shop UI/catalog, trading, crafting, durability, random affixes, binding, migration of existing CharacterTest equipment data, real consumable effects, or Unity trigger/physics as pickup authority.

## CharacterTest Runtime AI Planner Editor (2026-07-07)

Runtime AI Lab 的行为开发以 `Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md` 为总纲。正式 Lab Draft 继续使用 `Experiment Workspace -> Behavior Group Draft -> Behavior Card Draft`、分文件 autosave、`.previous` 恢复和 Undo/Redo。行为卡当前权威 schema 是 logic v11 / Published runtime contract 8：统一 Timeline/Event 条件树保留显式 `CurrentCombatTarget / Self / Environment` SubjectScope；作者界面统一把 Planner 锁定目标简称为“目标”，内部稳定标识仍为 `CurrentCombatTarget`。目标离散事实由稳定 `uint TargetStateMask` 统一承载，作者使用允许阶段、必须状态和排除状态三段模型；目标目录同时公开根节点 XZ 平面距离、姿态胶囊到锁定 Hurtbox 的三维表面净空与下一攻击窗口剩余帧。Hurtbox 净空不替代具体招式的 Attack Opportunity。自身动作条件以“没有运行中动作，或当前 LocalFrame 已进入任一正式 CancelRule”为可打断；带具体动作参数的切换合法性继续独立判断。对象优先编辑器显示 selected RuntimeFrame 的满足、阻断、对象不可用或未采样证据。v9/v10 内容先按旧 semantic/content hash 认证，再确定性迁移到 v11；旧活动程序不静默恢复。不可变 compiler、路径化 validator、NoAlloc evaluator、CharacterAction 原子交接、`Apply F+1`、Warm Replay、Player Deck、COW 编辑和 append-only 本地 Publish 继续使用同一权威链。完整历史 Actual、Plan 轨内 A/B、跨进程锁、生产 catalog 审批/签名/分发/回滚、账号、云存档和联网玩家卡组仍属于后续能力。

战术卡库支持独立战术行为卡直接发布：未发布草稿使用预留 Stable ID 创建正式 r1，已有来源草稿追加连续 revision；事务固定该卡引用的共享战术定义并只更新行为卡目录，行为组 catalog 与 revision 不改变。组级“发布本次修改”仍是另一条显式事务，只在需要提交行为组装配时使用。

CharacterTest now has a formal Runtime AI Planner v3 authoring and debug path. The runtime model is HFSM + Utility: a StreamingAssets JSON profile is loaded into a pure runtime DTO, validated, and compiled before session use. The v3 profile covers state tree, transition conditions, target selection, perception settings, movement style settings, sensor facts, utility actions, and action output mapping. Supported outputs are `Wait`, `MoveToTarget`, `Strafe`, `Retreat`, `PrimaryAttack`, and `CastAbilitySlot`. v1/v2 JSON profiles still load with default perception/movement settings and a validation warning.

Runtime decisions run through `CharacterTestRuntimeAiPlannerCommandModule` in `PreSimulation`. AiControlled entities no longer write character input directly from the planner path; they enqueue `SetControlIntent` with `CharacterTestRuntimeCommandSources.RuntimeAiPlanner`, then the existing `CharacterTestRuntimeCommandModule` applies authority state. The steady-state Tick path keeps typed fact/perception state and compiled action arrays instead of allocating a blackboard, sensor manager, or temporary planner result every frame. CharacterTest perception now uses the runtime-owned `CharacterTestPerceptionFrameCache` to share same-frame entity samples, target geometry, visibility, navigation, and environment query results across Runtime AI Planner, target locking, projectile aiming, and debug/HUD readers. Target locking consumes the semantic `Gameplay/TargetLock` action, whose defaults are keyboard `L`, mouse middle button, and gamepad right-stick press. Pressing it locks the nearest hostile, alive, damageable character within lock distance; pressing it again unlocks, and locked-state view-look flicks switch to a legal hostile target in that screen direction. Friendly/dead/untargetable/out-of-range targets are rejected, stale locks are pruned on the next simulation tick, and line-of-sight blockage affects perception and skill diagnostics but does not break or block target lock. The FairyGUI lock crosshair anchors to the target character root position at body height so extended renderer bounds, weapon poses, or hit VFX do not pull it off the character. It projects target visibility, line-of-sight blockage, last-seen age, threat score, direct-path status, surface kind, nearby static obstacle, and nearby movable/useful/hazard environment facts from CharacterTest runtime and CombatPhysics data. The cache is current-frame derived data only; it does not enter SaveState, network snapshots, replay/result hashes, or Runtime AI Planner JSON schema.

CharacterTest 的角色距离路线 CD-M0～CD-M4 已完成；CD-M5 生产实现和 focused gate 已完成，完整 Projectile/Chain action-path 仍等待当前 CharacterAction fixture 基线恢复后的直接复验。PrimaryAttack 从 sealed Hurtbox/CombatPhysics snapshot 构建确定性 Attack Opportunity，支持 CurrentPose、TurnInPlace、DamageZone、Engagement Anchor/CandidatePose 和 PF-M4 bounded A*。Completed Frame Geometry 同时冻结 Transform-root、source 姿态胶囊和目标 Hurtbox/Zone：`target.rootDistance` 保留根节点 XZ 语义，`target.distance` 是 Actor + LogicalHurtbox 持久锁对应的三维表面净空，`target.inAttackRange` 来自 exact Hurtbox CurrentPose opportunity。同 Actor 不因动作、动画、出现更近部位或部位状态变化重选；Hurtbox unavailable 时保留 Handle 并失败关闭。AI、玩家 Target Lock、投射物初始 AimPoint、Chain Lightning 首目标、HUD/Camera/Preview 共用该复合目标。Behavior Contract 7、Planner state 14、Network 34、Replay 4 显式承载语义迁移；gap/witness/cache 仍为派生数据。CD-M4 现在以 `(Actor + locked/provisional Hurtbox, action)` 有限归约，固定预算与 typed truncation 保留。该能力仍只声明 local-offline/loopback 代表规模，不构成 dedicated server、公网、多进程或大型 raid 容量声明。

CharacterTest Runtime AI Planner 的基础内容集合现在只包含两张正式 Published Behavior Card：`靠近并普攻`（`bc_3qz33zjzk4`）和 `发呆`（`bc_g6a5fw319a`）。默认 Profile、Project Catalog 恢复、推荐预设和卡库只使用这两个 Stable Card。当前完成的是基础身份、Published Logic 与目录收敛；两卡完整 runtime/Overlay 接入尚未完成，没有有效 Overlay 时兼容 Profile 只会 Wait。旧 `Targeting.* / Movement.* / Survival.* / Combat.* / Fallback.WaitOrHold` 模块不再作为行为卡定义、seed 或可选内容存在，只为历史 JSON 保留不进入卡库的只读迁移别名；不得通过旧模块动作或硬编码接近/攻击候选掩盖未完成状态。行为组继续保存稳定 `EntryId`、顺序、启用状态、卡片引用与作者职责元数据。

Runtime AI Planner behavior-card authoring now uses behavior-card logic v11. A recipe owns ordered peer Timelines and concrete Events; facts are scoped to target (`CurrentCombatTarget`), self or environment, while EventSignal reads the same Timeline's previous-frame stable event result. Target phase and six orthogonal target flags are evaluated through fixed bit operations; distance values remain numeric. The author-facing term "target" always means the Planner's locked target. Explicit movement strategies and `CurrentCombatTarget / WorldPosition / Entity` movement targets remain available internally. Every discrete action pins an exact CharacterAction id; deterministic compilation stays on low-frequency load/apply and the NoAlloc evaluator never interprets JSON in Tick. The Composer-owned `MxCharacterTestRuntimeAiLabUi` hosts the object-first condition picker, target-state picker, condition evidence and Timeline/Event editor. This does not change public `MxFramework.AI`, SaveState field layout or the network wire.

Runtime AI Planner now has a phase-driven Runtime AI Lab for `CharacterTestRuntimeAiLabScene`. Its FairyGUI `MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel` derives `Setup`, `Live`, `PausedInspect`, `Review`, `LoopReview`, and `Completed` presentation states. Starting a test enters the unified Runtime Test surface: the upper area keeps the 3D scene and selected Agent on the left and `感知 / 判断 / 行为 / 结果` plus action summary on the fixed right `当前决策` column. The lower host switches in place between the existing five-track `运行轨迹` and the tall six-track read-only `行为计划`; it does not open another full-screen page and does not add a thumbnail/minimap/overview strip. Facts, Replay, validation and long-form details remain contextual drill-downs. Setup selects a concrete project JSON behavior-group revision and, in Unity Editor, exposes `编辑当前行为组`; that command opens a new JSON-backed local working copy rather than the retired drawer/binder. The group workspace can open its pinned project cards, while the separately labelled local Player Deck keeps its own card-authoring path. Setup freezes a `1..1800` RuntimeFrame limit (default 900); each authority Tick captures one immutable record plus typed command, CharacterAction, Combat, runtime and trace history, and the session enters Completed exactly at the limit. The formal runtime timeline publishes ruler, stable marker/span/cluster projections, five fixed revision/decision/action/hit-damage/warning tracks, zoom/pan, scrub preview, marker navigation, future/retained masks and A/B Loop Review. Live projection is capped at 10Hz and reuses pooled FairyGUI components; paused selection updates immediately. Historical selection uses scrub/`跳转`/`最新帧`; `单帧` only generates one new frame from paused Latest. Warm Replay failure restores the previous committed frame, and an unrecoverable session permits only Reset/exit. The Lab can be hidden and reopened only through `F9` without changing simulation, timeline, draft, selection, or Overlay state; transition/recovery uses the independently preflighted `MxCharacterTestRuntimeAiLabRecoveryUi/RuntimeAiLabRecoverySurface`. Raw facts, hashes, internal ids, frame-sync diagnostics, and Edit Gate stay in developer mode.

The implementation also includes the CharacterTest runtime controller, recent-frame snapshot buffer, read-only Runtime AI Planner frame/agent summaries, selected-Agent scene visualization, and a Unity Editor menu generator/validator for `Assets/Scenes/CharacterTestRuntimeAiLabScene.unity`. The selected-Agent overlay consumes typed debug snapshot geometry only. The Lab surface remains an internal developer tool rather than a player UI and does not add public `MxFramework.AI` API. Its formal authoring authority is the lightweight project index at `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_card_catalog.json`, one full revision-bearing file per stable card under `runtime_ai_planner/behavior_cards/<stable-card-id>.json`, and `behavior_group_catalog.json`; native logic v11 uses peer Timelines, concrete Events and explicit CurrentCombatTarget/Self/Environment SubjectScope. The object-first condition picker exposes five current authoring Facts; target state uses explicit phase multi-select and six visible three-state controls, and selected-frame evidence remains Satisfied/Unsatisfied/SubjectUnavailable/NotObserved. Specific-action transition, cooldown-only Ability, dodge-cooldown and generic environment-hazard predicates are migration-only and no longer authorable; the empty Environment subject is hidden from the picker. v9/v10 cards authenticate before deterministic in-memory migration to v11; Published runtime contract is 8. Editor-only transactional publishing, conflict refusal/reload/reapply, project-card publishing, explicit group-reference upgrades, immutable experiment freezing and read-only Player loading remain implemented. Session-local replacement remains the separate confirmed `Apply F+1` boundary.

Runtime AI Lab review restores one immutable initial warmed-session baseline inside the existing `GameSlice`, replays retained external commands to a target `RuntimeFrame`, then verifies the authority hash. Normal review retains character Views, Playables, camera and HUD; explicit `冷启动校验` is the only full session rebuild path. Normal scrub/`跳转`/`最新帧`/A-B Loop operations do not restore intermediate checkpoints; old `-1/-10` controller methods remain internal compatibility paths and are not published controls. The Lab provides explicit session-only `状态采集`, `校验状态`, and `同步记录状态` tools: capture is off by default, authority validation determines Frame Sync PASS/FAIL, and manual sync may apply a captured Authority plus Presentation checkpoint without changing the timeline. Animation, VFX, audio, bone pose, and Overlay remain separate Presentation Diagnostics and do not define Frame Sync success or enter gameplay hash, network snapshot, SaveState, replay wire, or Runtime AI Planner schema.

For visual review, the Lab timeline also retains compact per-frame character animation inputs and feeds them into the existing warmed Playables during data replay. These inputs are session-only Presentation Capture, not authority state or network synchronization data.

Behavior Loadout is the CharacterTest-only gameplay foundation for selecting published Runtime AI Planner strategies. The player-facing language is strategy cards and short combat labels such as `突击`, `风筝`, `守卫`, `控场`, `保命`, and `协同`; internal module ids, facts, score/cost, profile slots, and catalog hashes remain authoring/debug data. The low-frequency presentation foundation builds strategy card text and combat status labels from loadout metadata plus typed debug snapshots, and the action-chain preview foundation links Runtime AI Planner actions to Ability slots, Ability bindings, and CharacterAction summaries with missing-link warnings. Runtime AI Planner still decides when to act while CharacterAction remains the source for action timing, gameplay requests, hit traces, VFX, projectile, and damage presentation. The CharacterTest-local Player Deck strategy-selection UI and typed Combat HUD current-card/switch-reason feedback are implemented. Progression/equipment unlock wiring, combat-time switching, account/cloud synchronization and networked player-deck integration remain outside the current delivery.

The behavior-card gameplay foundation documented in `Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md` includes internal Published Behavior Cards, per-character Installed Decks, local persistence, presets, authority locks, the Composer-owned player deck page, runtime overlay installation and typed Combat HUD feedback. Runtime AI Lab opens a Published revision as an isolated Logic v11 COW Draft without mutating Published content, the Installed Deck, active session or selected behavior group. Publish revalidates v11 SubjectScope and TargetState masks, authoritative references, player legality and append-only lineage, and leaves `Apply F+1`, the current evaluator and Replay application history unchanged. v9/v10 cards authenticate before migration; running contract-7 programs are not silently restored under contract 8. Production account progression, catalog approval/distribution/signing/rollback and networked player pages remain outside this capability.

CharacterTest also has a playable Runtime AI Planner arena test harness at `Assets/Scenes/CharacterTestArenaScene.unity`. Entering Play opens the FairyGUI `MxCharacterTestHud/ArenaSetupPanel`, pauses the initial runtime, and lets the tester configure 1-3 slots for Team 1 and Team 2 by choosing a character, published behavior loadout/strategy, and variant. `Start Battle` validates that both teams have at least one enabled slot, rebuilds a clean offline `GameSlice` from generated `CharacterTestCharacterSpawnSpec` values, writes authority `CharacterTestRuntimeAiBehaviorSelection` summaries into each spawn spec, locks the selection phase, and resumes ticking. Battle starts in `MultiTargetAuto` watch mode; during battle the HUD can switch between the existing `ThirdPersonControlled` and `MultiTargetAuto` modes directly, and `V` toggles the same modes. During battle the panel otherwise only shows compact alive-count/time/result status and reset controls; it does not edit behavior groups, inject arbitrary JSON, change loadouts during combat, expose facts, score/cost, profile slots, or behavior definition ids, or change Runtime AI Planner authority. The arena is CharacterTest internal playable infrastructure and does not add `MxFramework.AI` public API.

The CharacterTest Runtime AI Planner pathfinding foundation is verified through PF-M4. Formal CharacterTest and Runtime AI Lab sessions compile explicit serialized navigation regions plus static world-object/CombatPhysics occupancy into one immutable deterministic grid graph with stable revision/content hash and a conservative clearance envelope covering every registered playable character. `CharacterTestBoundedAStarPathfinder` provides bounded deterministic A* into caller-owned storage; direct, detour, unreachable, budget, endpoint, stale and unavailable outcomes remain typed. `CharacterTestPerceptionFrameCache.TryGetNavigation` composes direct capsule probing, A* next-waypoint steering, a second CombatPhysics segment probe, and existing L1 local candidate fallback under a complete same-frame cache witness. MoveToTarget, Strafe and Retreat consume the same sample but still submit `SetControlIntent` through `CharacterTestCharacterMotionSystem`; attack range and Attack Opportunity stay separate. Runtime AI Lab Facts and Play Monitor expose graph/status/waypoint/cost/query/blocker/fallback evidence without issuing queries. The graph, corridor and diagnostics are recomputable derived data excluded from SaveState, network snapshot, rollback/replay authority and result hash; warmed session/path/perception/Planner windows are 0 B. This is a CharacterTest local-offline/loopback capability, not production server navigation or distributed authority.

Unity Editor authoring lives at `MxFramework/Character/Runtime AI Planner` and is also registered in Framework Manager. Edit Mode supports JSON profile selection, a compact template menu, current scene profile path loading/binding, validation, save, and report copy. Built-in CharacterTest profiles now cover default melee, perception-aware melee, ranged kiting, hazard skirmishing, skill casting, aggressive berserker, defensive guardian, ambusher, and controller zoner patterns. Scene test characters are configured separately in `MxFramework/Level Editor` through the `CharacterTest Characters` module; `ControlOwner = AiControlled` rows can override their Runtime AI Planner JSON profile, and empty character-level profile paths inherit the `CharacterTestSceneProfile.RuntimeAiPlannerProfilePath` default after the CharacterTest session is rebuilt. Non-AI rows may retain an inactive profile path so switching control ownership does not destroy authoring intent; session construction ignores that path unless the row is `AiControlled`. Play Mode is read-only: it shows active state path, target, facts, selected action/score/cost, movement style diagnostics, last command, suppress/reject reason, and recent transition through `IFrameworkDebugSource`; Play Monitor can also enable an off-by-default 5 Hz SceneView diagnostic preview for the selected agent or all agents, drawing source/target geometry, LOS/path severity, attack range, move vector, configurable label density, legend, and compact decision labels from typed debug snapshot fields only. Saved edits take effect after rebuilding the CharacterTest session.

## Buff Framework v2 Reset (2026-06-12)

`MxFramework.Buffs`, `BuffPipeline`, `ConfigBuffFactory`, external Buff editor compatibility, WGame `BuffType`, and old Buff preview data are retired. Current Buff capability is component-native Gameplay state plus request/application/projection systems. Buff v2 authoring is JSON + CLI plus Buff v2 Workbench: `config/buff_authoring.json` compiles to `buff_definitions.json`, `buff_presentation_definitions.json`, and `aura_buff_definitions.json`, consumed by runtime importers. The Workbench is now the single safe editing surface for existing Buff/Aura fields, with designer-friendly labels, player-facing multi-type classification (`types[]`), `瞬时 / 持续 / 永久` time editing, simplified stacking editing, badge metadata editing (`nameKey`, fallback `displayName`, shortName, icon key), Auto/Manual effect descriptions, safe dropdown options, Validate-gated authoring diff, generated diff compile preview, and explicit saved-package import. Auto descriptions are generated from effect, time, and stacking config and are not written back into source authoring JSON. CharacterTest `Guard Aura` validates the first runtime slice: same-team targets inside the 4m aura receive Buff `601008`, which projects `Defense +5`, affects combat damage after cleanup/projection runs before damage application, and appears through read-only HUD/head pad presentation snapshots.

## CharacterTest Deterministic Network Sync (2026-06-11)

CharacterTest Host/Join now defaults to deterministic command replication. Gameplay movement/action state is synchronized through server-authoritative `NetworkAuthoritativeCommandPacket` packets and fixed-frame command buffers; routine digest/full state sync is disabled after the bootstrap full snapshot used for owned entity assignment and initial state setup.

Sync Probe and the automated MPPM smoke now treat any post-enter full snapshot, digest snapshot, or snapshot request growth as a failure (`STATE_SYNC_ACTIVE`). The older digest/full verification path remains available at the framework level through `NetworkStateSyncPolicy.SnapshotVerification`, but the player-facing CharacterTest LAN flow uses `BootstrapOnly`.

CharacterTest authoritative replay is available for tests, automation, and NetworkPanel diagnostics. The server records accepted scheduled `NetworkAuthoritativeCommandPacket` commands plus per-frame CharacterTest result hashes; playback rebuilds a headless `GameSlice` from the bootstrap snapshot and replays the command stream to classify sync bugs as missing stop command, non-deterministic rules/hash mismatch, or client receive/enqueue/apply path mismatch. The full-screen NetworkPanel shows the latest host replay status as `Replay Check: not run`, `recording authoritative commands`, `PASS`, `FAIL`, or `host only`.

## CharacterTest Surface Material Probe (2026-06-13)

CharacterTest has a playable surface material probe in `CharacterTestScene`. The official scene keeps the CharacterTest entry surface to the `GameManager` MonoBehaviour only; Level Editor Apply writes static collider instances into `GameManager.WorldObjectSceneCache`. Material, shape, layer, and default collider data resolve from the noEngine `CharacterTestWorldObjectDefinitionCatalog`, and legacy `CharacterTestCombatPhysicsAabbMarker` / scene-source components remain only as compatibility fallback for older scenes. The GameManager scene cache builds `CombatSurfaceMaterial` colliders for `CombatPhysicsWorld`, and `CharacterTestCharacterEntity` / network snapshots / runtime hashes retain material kind, stable id, friction, elasticity, and fixed bounce height after motion resolution. `HardSlick` behaves as a low-traction ice surface that limits grounded acceleration, braking, and reverse-turn response while preserving momentum. `Elastic` preserves restitution without adding energy; `BouncePad` targets a fixed bounce height and caps repeated launches to that height.

CharacterTest now has a playable static planar slope course in `Assets/Scenes/CharacterTestScene.unity`. Level Editor authoring and the CharacterTest static-Box compiler produce three finite Rough OBBs with stable identities `character-test.slope.15deg`, `.30deg`, and `.50deg` (`body.collider=10080.1/10081.1/10082.1`); the visible Cube children contain no Unity Collider authority. With the existing default 45° movement limit and formal 0.10m CharacterTest ground snap, 15°/30° support walking up, stopping, descending, lateral traversal, jump/landing, typed Root Motion and external impulse, while 50° remains a `SteepSlope` blocker that may preserve legal tangent/downhill motion but cannot be climbed or penetrated beyond skin. Real finite-edge support ends after the expanded capsule gap exceeds 0.10m and becomes Airborne. The same fixed-tick `CharacterKinematicPhysicsAdapter -> CombatKinematicMotor` path serves local/AI commands, persistent velocity, Root Motion, external requests, gravity and jump; same-frame diagnostics expose OBB identity, normal, threshold, classification, snap, flags, requested/applied/resolved motion and material. Active local/AI locomotion also preserves its reached target-bounded horizontal speed along a legal single-wall tangent without repeatedly losing skin time; Root Motion/Impulse/external motion keep default physical projection, and true corners remain blocked. Level v2 OBB JSON/validation/hash/world construction, snapshot/rollback/replay, physics-owning network clones, navigation/movable isolation, warmed eight-case full CharacterTest motion at 0 B, thirteen automated behavior/NoAlloc tests, and eight direct formal-scene Game View segments are verified. This capability is limited to static planar OBB character motion: it does not add height-aware AI slope navigation, movable/dynamic slope physics, moving or rotating platforms, Terrain/triangle meshes/curves, foot IK, real-device input certification, or Unity Physics authority; generic Combat ground snap still defaults to zero.

Developer Debug exposes `Impulse`, `Hit`, `Push`, and `Reset` controls. They enqueue the audited `CharacterTest.SurfaceProbe` runtime command through `GameSlice.EnqueueSurfaceProbe`; no debug button or scene entry directly writes Unity transforms or bypasses the command buffer. Tests cover command schema validation, marker-to-world material propagation, surface material snapshot/hash restore, and command execution.

CharacterTest snapshot schema v1 serializes authority transform, input axes, motion scalars, and physics profile/runtime modifiers as named integer units (`millimeters`, axis `milli`, scale `milli`, `grams`) and computes CharacterTest state hashes from those raw integer values instead of float quantization. Unity input/view code can still use float adapters, but snapshot/hash no longer treat those float values as the sync fact source.

## CharacterTest Character Potency Reception / Living Natural Attributes (2026-07-08)

CharacterTest character-side Genesis potency currently lands as body reception, not as a full public `D / P / S` material state. The active runtime projection is: incoming motion/scatter input plus `impactPower`, hit zone, and mass derive `ScatterImpact` and knockback; accumulated scatter pressure is `GameplayScatterPressureComponent.CurrentScatterPressure` (`ScatterDebt`); `CurrentCohesionLimit` is the local cohesion line from base pressure plus action `CohesionSupport`; `PressureBand` and `CohesionBreakEvent` express break risk and break.

`CharacterTestCombatDamageApplier` connects real hit results into `GameplayScatterPressureRequest` and `GameplayCohesionLimitRequest`; Debug, HUD, HeadPad, telemetry, and Runtime AI Planner facts read `CombatState` mirrors and typed pressure events. This is character-side reception only, not a public MxFramework potency API, YinYang runtime, formal role attribute schema, material transformation, or non-character effect-volume productization.

`CharacterTestLivingNaturalAttributesRuntimeState` covers the CharacterTest-internal body predicates `Embody`, `ApplyExternalPressure`, `ReleaseAndRecover`, `OverlimitDeactivate`, and `Reset`. It is held by `CharacterTestCharacterEntity.LivingNaturalAttributes`, can be changed through `CharacterTest.GenesisLivingNaturalAttributesRuntimeState`, participates in CharacterTest snapshot schema v6 and state hash, and appears in Developer Debug / `CharacterTestSliceDebugSource`. The older `GenesisLivingNaturalAttributesProbe` remains `harness-only`.

## CharacterTest Movable Natural Object Sandbox (2026-06-14)

CharacterTest now has a playable offline movable-natural-object sandbox in `Assets/Scenes/CharacterTestScene.unity`. The scene owns a `MovableNaturalObjectsSandbox` root with visible AABB cube instances, a movable Sphere instance, a movable Capsule instance, and a rough static boundary. Movable object rules come from the CharacterTest noEngine `CharacterTestWorldObjectDefinitionCatalog`; the official scene stores the seven movable instances in `GameManager.WorldObjectSceneCache` as definition id, numeric instance id, optional instance stable id, transform reference, and transform/scale source. `GameManager` collects resolved spawn specs from that cache when rebuilding the offline `GameSlice`, and the presentation tick syncs runtime object positions back to the referenced transforms.

Movable objects are authoritative CharacterTest runtime state, not Unity Rigidbody state. `CharacterTestCharacterRuntimeWorld` stores `CharacterTestMovableNaturalObjectState` entries with stable id, numeric id, category, shape kind, mass, material, half extents, position/velocity, contact velocity/displacement/pressure, contact pair, behavior state, contact count, and Genesis potency state (`Motion/Cohesion/Scatter`, shown as `D/P/S` in summaries). When a valid CharacterTest physics runtime is present, the fixed Tick applies deterministic `-30 m/s²` vertical gravity, sweeps against cached static AABB tops and bottoms, keeps resting contacts stable within 1 mm, and uses the larger surface elasticity for vertical rebound; movable objects still do not stack on one another. Character motion uses the existing `SetControlIntent` path; when the kinematic motor hits a movable dynamic body, the runtime transfers push velocity by source mass, target mass, push scale, and material friction. Potency commands are data-only in this slice and do not change friction, elasticity, breakage, burning visuals, or physics behavior.

The world-object definition catalog uses shared facets for identity/category, material, shape, potency, static collider defaults, and the movable-natural-object facet. The current built-in CharacterTest catalog defines `slick_light`, `rough_heavy`, `elastic_seed`, `contact_a`, `contact_b`, `sphere`, `capsule`, common static boundaries, surface probes, and static Sphere/Capsule probe targets. `CharacterTestWorldObjectInstanceMarker`, `CharacterTestMovableNaturalObjectMarker`, `CharacterTestCombatPhysicsAabbMarker`, `CharacterTestCombatPhysicsSphereMarker`, and `CharacterTestCombatPhysicsCapsuleMarker` remain as legacy compatibility readers, and a separate debug override marker can temporarily alter resolved definitions for experiments, but the playable scene no longer depends on per-object marker components.

CharacterTest world-object, level-point, and scene-test-character authoring now uses the framework `MxFramework/Level Editor` window. The default human-reviewable source is `Assets/Config/MxFramework/Levels/CharacterTest/character_test_scene.world_objects.json` as a modular `mx.level.authoring.v3` envelope; its world-object module is `characterTest.worldObjects`, whose payload preserves the world-object v2 fields: `movableInstances`, `singleStaticColliders`, `staticColliderBatches`, `points`, and `regions`. The new character module is `characterTest.characters`, whose payload preserves enabled scene character rows, stable ids, transform references, character ids, team ids, control owners, groups, and notes. Material/mass/shape/potency rules still resolve from `CharacterTestWorldObjectDefinitionCatalog`, while character definitions resolve from `CharacterTestCharacterDefinitionRegistry`. Transform references contain hierarchy path plus `UnityEditor.GlobalObjectId`; resolution prefers GlobalObjectId and falls back to the path for review/repair. Level Editor Apply generates the Play/runtime caches inside `GameManager.WorldObjectSceneCache` and `GameManager.CharacterSceneCache`, including JSON path, v3 content hash, and last-applied timestamp. Runtime and builds continue to read the GameManager scene caches rather than authoring JSON directly. The Level Editor is UI Toolkit based and uses a framework-owned structure tree, virtualized fixed-column list, inspector, scene placement, and diagnostics panel; static collider batches expand direct children only and are never written back as generated rows in JSON.

CharacterTest Genesis Validation P1 adds the project-private `characterTest.genesisValidation` payload and a top-level `创世验证` Level Editor module. Besides validation groups and station metadata, P1 now includes a foundational scene-design layer: root `ScenePlan`, typed `Zones[]` and `Routes[]` with ordered waypoints, per-station `SpatialProgram`, five station templates, resolved-layout diagnostics, a quantized spatial diagnostics hash, and SceneView overlays for scene bounds, zone footprints, routes and spatial roles. `创建基础场景框架` composes the existing world-object owner with the Genesis module in one Unity Undo group and creates an unsaved three-zone Surface / Boundary / Movable draft using owner-registered Region, Marker, Checkpoint, single-static and movable rows; it refuses duplicate scaffolds and never hand-writes Scene YAML. The world-object catalog owns the new pure-spatial `Validation Checkpoint` and `Validation Marker` definitions, which carry no character, team or control identity. Static references remain restricted to `SingleStatic + BodyId + ColliderId + ExpectedDefinitionId`; batch children are absent from the picker and fail closed if injected. Structural and resolved-geometry errors block Authoring save, while executable capability remains deliberately outside P1: authority/readiness/cache display stays `HarnessOnly` or `Unsupported`, `NotRunnable`, and `NotBuilt`, and save never builds or applies a validation Scene cache. The tool does not modify the official CharacterTest Scene until a designer explicitly creates, reviews and saves the draft; no public framework API, Runtime runner, observer, startup correlation or Player-facing UI is added.

CharacterTest Level Editor P2.3 owns the optional `characterTest.gameplayTriggers` Authoring payload with three fixed mappings: Story RaiseTrigger on Entered once, Gameplay Buff Apply on Entered, and Gameplay Buff Apply/Remove across Entered/Stayed/Exited. The P3.3 implementation exposes them through a top-level`玩法触发器` workspace instead of a large Region Inspector card. It provides derived categories, search, new/copy/delete, name/enabled state, Event/Condition/Target/Action sections, a deterministic Chinese effect sentence, Scene bidirectional location, a compact Region link card, narrow-window list collapse, and the existing typed runtime inspector on demand. It shares the same world-object module instance, document, Undo baseline, dirty state, save participant, compiler and close transaction; it does not add another Trigger model or alter Runtime/schema/hash. `保存` is the only commit action: it validates and writes Authoring, rebuilds Scene-owned runtime caches, then saves the managed Scene as one coordinated transaction. Current creation remains limited to formally compiled templates; unsupported sources and generic per-action composition stay unavailable until a domain adapter, catalog, validator, compiler and Runtime consumer exist.

Level Editor P3.0-P3.1 now separates catalog-backed references from target semantics. Editor-only reference fields consume existing Buff/Aura/Mechanic Source catalogs and preserve unknown historical values; they do not create a second Runtime registry. The Buff picker presents duration in seconds, translates stack policy into repeat-application behavior, keeps raw frames/stacks in the Tooltip, and shows the CharacterTest gameplay result. Its user-facing `来源机制` field still writes the existing Buff `SourceId`, but explicitly does not change Buff rules or execute the referenced Aura/Mechanic Source effects. Gameplay target semantics are layered as source selector, generation-safe instance identity, definition identity, base identity/capabilities, team relation, tags, and statuses. The base identity contract is limited to Character, WorldObject, SpatialPoint, and SpatialRegion; capabilities are Damageable, Movable, and Static. Character runtime entities declare Character + Damageable + Movable, while Player/NPC/Boss/Summon remain controller/definition/tag concerns. Component targeting can filter identity, required capabilities, alive state, Self/SameTeam/Enemy/Neutral relation, tags, statuses, and count. The CharacterTest Trigger Inspector shows a compact source row plus a read-only identity-rule row derived from the existing route and binding; Trigger JSON and action payloads are unchanged.

Level Editor P3.2 makes the first CharacterTest Combat-region Subject Filter editable and runtime-effective. Region Buff/Story sources can filter same-team, enemy or neutral subjects against a TeamId selected from the current Scene Character document; required Character/alive/template capabilities stay locked, while additional Movable is progressive. Filtering runs in the existing Combat Sensor before complete occupancy submission using Gameplay classification/team/lifecycle and the shared no-allocation evaluator. The CharacterTest Trigger document/cache is v2 with v1 default migration, filter-sensitive hash, typed diagnostics and single-step Undo/Redo.

P2.5 closes the CharacterTest offline Playable boundary on top of the P2.4 Runtime Slice. `CharacterTestGameplayTriggerSceneCache` stores the canonical Trigger envelope, fixed Combat AABB sensor rows, schema/content identity, and Trigger/sensor/combined hashes on `GameManager`; session bootstrap rebuilds and exactly verifies the formal Trigger plan, `GameplayTriggerCombatSensorSystem`, Buff same-frame bridge, Story reliable bridge, SaveState/checkpoint participants, and combined result hash. Invalid cache/action/source composition fails closed without Unity Physics fallback. The official `CharacterTestScene` sample binds `trigger.region.start.1` to collider-free `region.start`, applies and refreshes Buff `601012` with source mechanism `501012` on Entered/Stayed, and removes it on Exited. Its sensor cache separates 64 Combat query candidates from 8 mapped subjects. The Level Editor runtime tab reads typed status/results at low frequency and shows the actual Combat query, sensor hits/mapping, occurrence, and action result. Save/reopen, the actual CharacterTest session, focused EditMode and a collider-free PlayMode smoke have been verified. Network-session injection and live hot reload remain unsupported; configuration changes still require cache rebuild and a new offline session.

The sandbox covers hard-slick vs rough push distance, light/heavy wall pressure, elastic boundary rebound, object-to-object velocity transfer, and player pushes against AABB / Sphere / upright Capsule movable targets. Developer Debug includes a `Movables` tab, a numeric object target field (`0` means all), `Apply Fire`, `Apply Hammer`, `Apply Condense`, and `Reset Objects`; these enqueue `CharacterTest.MovableNaturalObjectPotencyInfluence` or `CharacterTest.MovableNaturalObjectReset` through the runtime command buffer. Generic `CharacterTestSliceDebugSource` also reports movable summaries.

CharacterTest snapshot schema is now v5. `CharacterTestNetworkSnapshotDocument.movableNaturalObjects` participates in full snapshot capture/apply, rollback clone, and full/owned/non-owned state hash because movable natural objects are shared environment state. Schema v5 includes shape kind plus base/current/pending potency, potency profile, and the latest potency application record. Schema v4 documents normalize potency to zero with disabled thresholds, schema v3 documents also normalize missing shape to AABB, and schema v2 documents normalize missing movable arrays to empty. LAN/MPPM sync is not the current acceptance gate, but the snapshot/hash format is ready for later network transport.

## CharacterTest Combat Physics Shape Registry Probe (2026-06-14)

`CombatPhysicsWorld` now supports AABB / Sphere / Capsule target collider registries, and `CharacterTestScene` includes `TestCourse_40_ColliderShapes` near the start area with `PhysicsProbe_SphereTarget`, `PhysicsProbe_CapsuleTarget`, and `PhysicsProbe_QueryScanner`. The official scene builds AABB / Sphere / Capsule static collider records from `GameManager.WorldObjectSceneCache`, so scene tests can verify query hits against all three target shape kinds without introducing Unity PhysX authority. `CharacterTestCombatPhysicsSceneBridge` remains as a legacy fallback for older scenes that still carry bridge/source MonoBehaviours.

This is a core registry/query probe. The visible probe transforms are static authoring references; authoritative verification comes from `CombatPhysicsWorld.Query` scene tests. Playable movable Sphere/Capsule push tests live in `TestCourse_20_MovableNaturalObjects`.

## CharacterTest Abilities Full-Screen Binding UI (2026-06-12)

CharacterTest 1-9 ability hotbar binding is available through the FairyGUI `MxCharacterTestAbilityUi/AbilityBindingPanel`. The panel is authored as a `1920x1080` full-screen source component and uses FairyGUI relations for full-screen backgrounds, the stretchable ability list, and right/bottom anchored action controls. `CharacterTestFairyGuiAbilityBindingPanelBinder` only sizes the root to the active FairyGUI layer/stage so the source relations drive resolution adaptation.

## CharacterTest Player UI Surface Policy (2026-07-08)

CharacterTest player-facing FairyGUI now uses an internal surface policy instead of ad hoc host-to-host state checks. Surfaces are classified as `Hud`, `LightOverlay`, `NarrativeModal`, `PrimaryPanel`, `TransactionalPanel`, `SystemMenu`, or `DebugOverlay`. The current CharacterTest slice declares Combat HUD, floating combat text, and Player Guidance as non-modal presentation surfaces, `StoryDialog` as a narrative modal, MainMenu / Settings as system menu surfaces, Network / AbilityBinding / Runtime AI Planner loadout / Arena setup as primary panels, and Arena behavior group editing as a transactional panel.

Large player surfaces (`PrimaryPanel`, `TransactionalPanel`, and `SystemMenu`) are mutually exclusive as one player UI session. Opening MainMenu, NetworkPanel, SettingsPanel, AbilityBindingPanel, Runtime AI Planner loadout, ArenaSetupPanel, or ArenaBehaviorGroupPanel closes conflicting large player surfaces after the target opens successfully. `StoryDialog` is not a conflict-close target; it is suppressed while a player panel is active and can continue its narrative flow after the panel session clears. HUD, floating combat text, and Player Guidance do not enter the modal stack; when a large player surface is active the runtime policy clears or pauses gameplay presentation, then resumes normal presentation when gameplay owns the screen again.

All large player FairyGUI surfaces push exclusive `InputContext.UI`, release the cursor through `MxCursorController`, and rely on the existing FairyGUI modal stack plus `MxFairyGuiModalCommandGate` for lower-layer command blocking. Cancel/back remains top-first: the active top modal or current large surface handles it before gameplay receives input. This is CharacterTest-internal in v1 and does not add a reusable `MxFramework.UI` public Surface Policy API or change FairyGUI layer ordering; Player Guidance remains a bounded semantic presentation sink rather than an imperative global Confirm/Alert/Toast service.

Formalization status: CharacterTest MainMenu, SettingsPanel, Combat HUD, and Player Guidance are now managed by this internal policy for the current playable slice. The main and settings surfaces are mutually exclusive large player surfaces, while HUD, floating combat text, and Guidance remain light presentation layers that do not take modal input or cursor ownership.

## Player Guidance Prompt + Level Editor Authoring (2026-08-13)

The formal Guidance foundation is current. `MxFramework.UI` supplies noEngine stable scope/message ids, closed tone/glyph semantics, a fixed title plus Action/Focus/Status Card snapshot, a bounded Notice request, `IMxUiGuidanceSink`, and `MxUiGuidanceState`. State capacity is deliberately one Card plus two transient notices; duplicate scope/message requests refresh in place, strictly higher severity may replace a lower-severity slot, and stable `Advance` is NoAlloc. Guidance never owns Tutorial, Objective, Story, Trigger, Combat, Validation, SaveState, Replay, network, or completion authority.

The Composer-owned `MxPlayerGuidanceUi` package publishes `GuidanceCardPanel` and `GuidanceNoticeStackPanel`. `MxFairyGuiGuidancePresenter` registers them as `ui.guidance.card` on `Hud` and `ui.guidance.notices` on `Toast`, disables input recursively, supports `Default` and `TopCornersOccupied` layouts, refreshes localization by revision, and performs fixed-slot presentation timing without steady-state managed allocation after initialization.

CharacterTest P3.4 is Current. The Level Editor owns one independent `教学引导` module backed by `characterTest.guidance` inside the shared `mx.level.authoring.v3` envelope. Authors can start from a blank step or six player-intent presets, fully edit a linear ordered sequence, and switch each step among five registered typed completion conditions without changing its other authored fields. Missing modules open as a disabled empty document; incomplete drafts remain in the Editor/Undo session but fail save preparation regardless of root or step enablement. The module offers waiting/completed/target-rejected semantic preview, participates in document Undo/Redo and the coordinated JSON/cache/Scene save, preserves unknown completion payloads for explicit repair, and protects unknown module schema as read-only. Save compiles a Guidance-only canonical hash into `CharacterTestGuidanceSceneCache` on GameManager; Play and build preflight reject missing, invalid, or stale cache, while Player Runtime never reads the Editor JSON path.

Ordinary CharacterTest sessions configure `CharacterTestFairyGuiGuidanceHost` only from that Scene cache. The formal six-step plan is movement/stop, jump/land, camera-mode switch, target lock, target unlock, and basic-attack damage. `CharacterTestGuidanceSequenceCoordinator` owns selection/completion/countdown, five typed observers consume confirmed CharacterTest state, `CameraModeChanged`, `TargetLockChanged` / `TargetLockRejected`, and Combat `DamageTelemetryRow`, and `CharacterTestGuidanceSnapshotMapper` only projects existing Guidance DTOs. Each authored `inputActionId` resolves explicit binding index `0` through `InputBindingDisplayService`; binding revision refreshes the current prompt, but active-device-aware binding choice remains outside the current contract. `[` / `]` provide bounded manual navigation without changing gameplay authority or fabricating completion. Missing/invalid/stale data fails closed with a typed diagnostic; there is no hardcoded six-step fallback. The old Combat HUD target-lock feedback outlet remains removed to prevent duplicate messages, and the separate development preview stays explicitly labelled as preview.

CharacterTest also has a local player-facing Runtime AI Planner behavior deck flow. The Composer-owned `MxCharacterTestBehaviorDeckUi/BehaviorDeckPanel` preserves an independent `6`-slot / `10`-cost deck for each stable player character, supports install/remove, replacement and presets, and persists all local decks atomically. Starting Arena play locks Published revisions and hashes, installs Logic v11 / contract-8 programs as one atomic batch and keeps deck editing read-only for the session. Combat HUD feedback follows the explicit observed entity; empty decks use the free targeting/idle fallback. Production accounts, cloud saves, catalog distribution and networked player pages are not connected.

## UI Art Resource Library + FairyGUI Art Slice Audit Tooling (2026-07-10)

UI cut-image semantics now use an independent hand-curated resource library as the primary authoring source. The first library is `ArtSource/UIKits/MxUiFoundation/ui-art-resources.json`; it currently contains 40 semantic resources spanning the original generic-button test entry, formal player UI foundations, Combat HUD, and Runtime AI Lab authoring assets. The resource catalog owns semantics, recommended FairyGUI usage, and replacement constraints before assets are imported into any FairyGUI package.

The primary editing surface is the external EditorHub tool `Tools/MxFramework.FairyGuiArtSlices/web/?library=MxUiFoundation`, backed by Authoring Core and Authoring Server APIs for capability-version checks, library state, catalog hash, complete proposed-catalog validation, semantic-only guarded save, replacement planning, same-path PNG replacement, configurable new-resource planning, and read-only FairyGUI integration handoff planning. Its desktop UI is a fixed three-column workbench with independent scrolling, a tabbed semantic inspector, file-first create and replacement drawers, compact diagnostics, metadata copy actions, and a draggable NineSlice preview synchronized with numeric parameters. The FairyGUI inspector can select a package.xml-backed target package, detect import/content/usage drift, and copy or download deterministic JSON/Markdown recipes containing suggested package paths and recommended parameters. Resource-library roots are constrained to `ArtSource/UIKits`; invalid catalog data is blocked before write, and opaque PNG resources can be created when alpha constraints are disabled. The existing `MxCharacterTestHud` `ui-art-slices.json` scanner/report remains available as a FairyGUI package usage audit with `--check-art-slices`, `--write-art-slice-report`, and `--review-report`, but it is no longer the default source for new UI assets. The Unity Editor menu `MxFramework/FairyGUI/UI Art Slice Catalog` remains as a thin launcher/debug fallback. This resource-library capability does not write FairyGUI XML or publish packages; those actions are available only through the separate, ownership-constrained UI Composer flow described below. Runtime dynamic usage scanning and NineSlice auto-replacement remain outside the resource-library scope.

## FairyGUI Semantic UI Composer (Authoring API v5, 2026-08-09)

The external UI Composer at `Tools/MxFramework.FairyGuiUiComposer/web/` authors multiple ownership-constrained screens from the `MxUiFoundation` semantic resource catalog, reusable component recipes, generated binding contracts, and constrained screen blueprints. It defaults to `MxCharacterTestUi/MainMenuPanel`, also owns the published `MxCharacterTestCombatHud/CombatHudPanel`, `MxCharacterTestSettingsUi/SettingsPanel`, `MxCharacterTestNetworkUi/NetworkPanel`, `MxCharacterTestAbilityUi/AbilityBindingPanel`, and `MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel`, and keeps `MxUiFoundationDemo/UiFoundationDemoPanel` as a regression sample. Blueprint v2 supports Graph, controlled Text including input restrictions, controlled Image, ComponentInstance nodes, authoring layout containers, drag/resize, ordering, grid snapping, undo/redo, anchor/relation layout, semantic image and icon selection, and four desktop preview resolutions. Blueprint v3 and Authoring API v5 add a controlled screen-level FairyGUI List whose default item references a component recipe; its runtime virtualization flag remains a binder responsibility, and the API version gate rejects clients that cannot interpret the new node type. Binding slots come from generated C# contracts and cannot contain arbitrary code.

Runtime AI Lab now consumes the controlled List for its behavior-card composition: one native `GList` replaces the fixed six-card projection and hand-written scrollbar. The selected card exposes recipe-owned enable/disable and delete actions, while runtime status, behavior type, and priority are omitted from the editing list.

Blueprint save, validation, apply planning, FairyGUI synchronization, and publish are guarded by catalog, recipe, contract, blueprint, mapping, and component XML hashes. The Authoring Server writes blueprint drafts and operation requests; the FairyGUI helper plugin is the only writer for allowlisted owned packages, assigns and records package/resource/node ids, applies semantic NineSlice metadata, materializes fixed button components and panels, and publishes through the existing `PublishHandler` with `genCode=false`. Unknown nodes, transitions, unsupported gears, unsupported attributes, path escape, or stale hashes block synchronization instead of being silently discarded. Runtime AI Planner and arena panels remain in the read-only `MxCharacterTestHud` package. The Combat HUD stays non-modal and reuses the existing no-allocation refresh/binder path, while modal panels retain their existing Surface Policy, `InputContext.UI`, cursor, and pause behavior.

The published package is exercised by `Assets/Scenes/MxFramework/UiFoundationDemo.unity`. Its runtime composition loads the real package bytes and atlas through `MxFramework.Resources`, opens the panel through `MxFramework.UI.FairyGUI`, acquires the normal UI input and released-cursor scopes, and routes the three buttons through generated bindings and `MxUiCommand`. Primary increments an execution count, Secondary toggles the status text, and Close releases the modal UI session and restores captured gameplay cursor ownership. V1 intentionally excludes arbitrary controllers, transitions, animation, lists, input fields, localization authoring, and custom script expressions. Authority and operating details are in `Docs/FAIRYGUI_UI_COMPOSER.md` and `Docs/Decisions/ADR-007-fairygui-semantic-ui-composer-authority.md`.

Composer v4 introduced the unified resource, Button recipe, and node workspace. Composer v5 retains it and adds the Blueprint v3 List compatibility boundary. It keeps separate dirty states and authority files, builds cross-screen reverse references on the server, supports hash-guarded recipe saves and safe deletion planning, and performs unified preflight before an Apply-and-Publish operation.

The recipe catalog also supports controlled, read-only `Composite` templates for reusable authoring surfaces. Composite dependencies are recursively validated, indexed, and materialized into deterministic FairyGUI component XML; dependency cycles, unknown resources, and unknown recipes block preflight. Runtime AI Lab uses these composites for behavior cards, the gameplay editor, the developer flow, and the five-track timeline. CharacterTest loads `MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel` for the Lab workspace and the independently preflighted `MxCharacterTestRuntimeAiLabRecoveryUi/RuntimeAiLabRecoverySurface` for transition/recovery; the former persistent launcher is no longer registered, while other unmigrated CharacterTest surfaces continue to use `MxCharacterTestHud`.

## CharacterTest NetworkPanel Ready Room Update (2026-06-10)

CharacterTest LAN testing is player-facing through full-screen `MxCharacterTestNetworkUi/NetworkPanel`. The default flow is now LAN Room: Host/Join keeps the panel open, each member clicks `Ready`, and only Host clicks `Enter Game` after required connected members are ready. Host `Enter Game` starts room gameplay; Host and Clients close the panel automatically after their local gameplay handshake, owned entity assignment, and first full snapshot pass. Direct LAN remains a quick diagnostic mode without ready/start protocol; its `Enter Game` gate is connected client + owned entity + full snapshot. This path is supported by MPPM local testing and Standalone double-instance testing.

NetworkPanel also exposes `Auto Probe` and `Run Sync Probe` diagnostics. `Auto Probe` is a local persisted trigger: enable it before `Enter Game`, and that instance automatically runs the 15 second / 900 frame `MoveStopLoop` input-sync probe after its local gameplay handshake completes. The manual `Run Sync Probe` button remains available after gameplay is verified. Both paths run on the real UTP/MPPM session, inject deterministic `SetControlIntent` commands through the existing command path, write the final report to Unity Console, keep the latest summary in NetworkPanel, and write per-frame JSONL under `Application.persistentDataPath/MxFramework/CharacterTest/SyncProbe/` for pass/warning/fail causes such as `AUTH_LATE_GROWING`, `NO_STOP_INTENT`, `FULL_SNAPSHOT_STORM`, `RELIABLE_QUEUE_BLOCKED`, `NO_AUTH_ECHO`, and `FRAME_DRIFT_HIGH`.

CharacterTest also has an automated MPPM PlayMode smoke gate: `CharacterTestNetworkAutomationTests.RoomHostAndOneVirtualPlayer_AutoSyncProbeAndExit` writes a one-run request, enters Play, drives Main Editor as Host and Virtual Player as Join through LAN Room Ready / Host Enter Game, runs Sync Probe on both instances, collects JSON reports, and exits PlayMode. It requires a selected/running MPPM Scenario with Main Editor Player plus at least 1 Virtual Player; it does not create scenarios through internal MPPM APIs.

## CharacterTest Temporary Skill Panel (2026-06-12)

CharacterTest now includes a UI Toolkit temporary skill panel for projectile and combat skill testing. The panel shows nine runtime-only slots and twenty-one ability candidates: Fireball, Fireball Burst, Flame Lance, Fireball Lob, Fireball Surge, Fireball Return, Tornado, Fireball Sway, Fireball Homing, Fireball Bezier, Beam Lance, Channel Beam, Laser Lance, Chain Lightning, Poison Bottle, Stun Bolt, Sword Wave, Dash Strike, Hook Chain, Arc Mine, and Rune Turret. Default slots are `1..9` mapped to Fireball, Fireball Burst, Flame Lance, Fireball Lob, Fireball Surge, Fireball Return, Tornado, Fireball Sway, and Fireball Homing. Clicking a candidate equips it into the currently selected slot; clicking an occupied slot or pressing keyboard `1..9` submits the corresponding local `SetControlIntent` skill-slot request.

The panel is CharacterTest-only and does not write ability authoring JSON, SaveState, replay files, or network loadout state. It only changes the local Play-session slot table. Actual casts still go through the existing Ability cooldown gate, CharacterAction timeline, projectile spawn profile, projectile runtime, hit event, VFX, and fixed-frame command path. Empty slots and cooldown rejects are diagnostics-only and do not create projectile, damage, or hit presentation events.

## CharacterTest Skill Showcase (2026-06-21)

CharacterTest now has an independent playable Skill Showcase scene at `Assets/Scenes/CharacterTestSkillShowcase.unity`, generated from `MxFramework / CharacterTest / Generate Skill Showcase Scene`. It is separate from `MxVfxShowcase`: VFX Showcase remains resource/prefab playback, while Skill Showcase starts a fresh CharacterTest runtime and previews real Ability -> CharacterAction -> Combat -> Buff -> VFX behavior.

The UI Toolkit panel supports search, scrolling ability selection, Play/Replay/Stop, Step Frame, Loop, Reset Actors, target count, target distance, target spacing, lock target, optional friendly target, background color swatches, orbit camera drag, and telemetry. The recent CharacterTest skills are sorted first: Physical Shield (`910019`), Beam Lance (`910014`), Channel Beam (`910015`), Laser Lance (`910016`), Chain Lightning (`910017`), Poison Bottle (`910018`), Stun Bolt (`910020`), Meteor Barrage (`910021`), Sword Wave (`910022`), Dash Strike (`910023`), Hook Chain (`910024`), Arc Mine (`910025`), and Rune Turret (`910026`). Replay assigns the selected ability only to a preview runtime slot and submits the existing `CastAbilitySlot` command path, so it does not modify saved player hotbar data.

CharacterTest skill implementation patterns, module ownership, data sources and VFX / interaction boundaries are maintained in `Docs/Guides/CHARACTER_TEST_SKILL_IMPLEMENTATION_PATTERNS.md`.

## CharacterTest Skill Implementation Summary (2026-06-25)

CharacterTest skills now cover projectile runtime patterns, one-frame beam sweeps, multi-shot timeline projectiles, projectile-to-mechanic-source handoff, active physical interaction producers, instant non-projectile chain resolution, aura Buffs, physical shields, and channel-to-zone mechanics. The implementation pattern guide records how these modes are assembled from AbilityRuntime, CharacterAction, Combat, Gameplay, VFX and CharacterTest-specific producers; this capabilities page only keeps the current capability summary.

All projectile casts still use the same authority chain: AbilityRuntime -> CharacterAction `SendGameplayRequest` -> projectile spawn profile -> projectile definition -> `Combat.Projectiles` -> `CombatPhysicsWorld` sweep/query -> HitResolve -> CharacterTest damage / Buff / status / interaction / VFX bridges. `Chain Lightning` remains the same-frame ranged comparison case and does not create a projectile runtime entity.

Projectile-related physical interaction now starts from a generic projectile fact switch: `projectile_definitions.json` can set `physicalEventsEnabled=true`, and `Combat.Projectiles` emits deterministic `CombatProjectilePhysicalEvent` facts using existing projectile `collision.radius`, `impact.areaRadius`, and `impactPower` values. CharacterTest still owns the interpretation layer: Tornado maps active-frame facts to attraction, Sword Wave maps active-frame facts to directional push, and Fireball Explosion / Meteor map area-pulse or expiration facts to radial impulse. Unmapped projectile stable ids are ignored by the CharacterTest interaction consumer, and Unity Rigidbody / prefab physics remain presentation-only.

CharacterTest control statuses separate rule state from presentation arbitration. `Stunned` / `眩晕` is a Buff v2 status (`STUN`, buff `601013`, status `901013`); `Stasis` / `STOP` remains an action-time / animation-pause control. The view resolves one primary control presentation per entity with priority `Paralyzed > Stunned > Stasis > Rooted > None`, while poison and future burn/electric effects remain overlay-style presentations.

CharacterTest also includes an `Area Confinement` runtime slice (`AbilityId=910013`) available from the ability binding choices. It reuses AbilityRuntime + CharacterAction and sends `character_test.area_confinement.zone` on action frame 6, but that request now begins a 60-frame telegraph channel instead of creating the zone immediately. The landing point is locked at frame 6, preferring the current deterministic locked hostile target center and falling back to caster forward 6m. During frames 6-65 the runtime only shows `vfx.magic.area_confinement.telegraph`; no mechanic source exists, no Buff is refreshed, and targets are not rooted. If the caster takes effective damage, dies, becomes invalid, or exits the `area_confinement` action before frame 66, the pending channel is cancelled and `vfx.magic.area_confinement.telegraph_cancel` plays. On frame 66 the CharacterTest internal mechanic source is created as a fixed 180-frame, 2.5m-radius source. The source owns area lifetime and target query, using `CombatPhysicsWorld.QuerySphere` each simulation frame to find hostile targets. Its effect bundle refreshes Buff `601010` for 2 frames on tick, projects Gameplay status `901010` (`AreaConfined`), and spawns one `vfx.magic.area_confinement.zone` create VFX at the resolved ground center. Zone VFX fades in/out while the rule lifetime remains exactly 180 frames. A separate CharacterTest status VFX bridge watches `AreaConfined`, attaches one `vfx.magic.area_confinement.character` binding-ring VFX to each affected character, and plays `vfx.magic.area_confinement.character_release` when the status disappears; Buff refresh does not duplicate character VFX. The CharacterTest mechanic motion policy registry maps `AreaConfined` to `RootActiveMovement`, so the motion effect bridge emits `CharacterTestCharacterMotionRequest.LockMovement` and locks active movement control only; turn control, current actions, hit reactions, knockback, Tornado attraction, root motion, collision response, and other external motion remain outside the root lock. This slice intentionally does not add a generic framework `CombatGameplayAreaBuffSystem`, framework public mechanic API, Unity Trigger authority, default hotbar slot, or temporary OnGUI.

## Large Character Contact M0 Foundation (2026-08-24)

Large Character Contact M0 now provides the non-gameplay foundation: optional Body Presence authoring with independent revision-gated save, deterministic geometry binding v4 and v1～v3 compatibility, fail-closed runtime parsing, a pure Combat shape-separation query, and Dragon14 three-volume diagnostics exposed in CharacterStudio/CharacterTest debug views.

This is not playable pushing. M0 does not integrate Body Presence with character motor/collision response, contact pressure, damage/part state, severance, IK/terrain adaptation, or network authority; those remain later milestone work.

## Large Character Contact M1 Body Presence Runtime Slice (2026-08-24)

CharacterTest now consumes compiled Body Presence as a deterministic, derived soft-contact layer while preserving the existing Hard Root as the only character movement blocker. Up to four stable ModelRoot volumes per character produce fixed-capacity, stable-sorted contact constraints; translation and shortest-delta root Yaw contribute surface velocity, and the bounded result enters the existing contact-velocity path before the next kinematic-motor static sweep. `BlocksCharacters=false` entities are excluded, while blocking but `CanBePushed=false` entities retain contact observation without receiving Presence velocity. Presence remains outside motion/static obstacle, Hurtbox, damage, projectile and navigation masks, and idle appendages do not become attack traces or damage.

The slice does not add a snapshot schema: compiled Presence and world volumes are derived, while existing contact velocity/count participate in snapshot/hash/rollback. The existing session `ResourceCatalogHash` now identifies the stable playable-package set, including v4 Presence revision/content identity, and replay inspection fails closed on mismatch. Formal Dragon14 PlayMode covers translation, turn-in-place, opposing/same-center contact, mass/immovable/death, lifecycle, rollback+1, wall/corner/corridor safety, appendage isolation and natural-attack windows. The current 8-character, 32-Presence, 144-Hurtbox 4096-frame acceptance run measured 0 B managed GC and 1.687 ms P95 on the recorded i5-12400F/Unity 6000.5.0f1 fixture. M2 part-damage, the candidate-complete M3 severance Runtime Slice and the M4 Dragon14 terrain-pose candidate are described below.

## Large Character Contact M2 Body-Part Damage (Verified 2026-08-25)

The current checkout contains the verified LCC-M2 authoring, compiled binding, fixed-capacity runtime state, accepted-damage bridge, DamageZone selection projection, and CharacterTest snapshot schema 31 path. Schema 30 is owned by the numeric `CharacterActionId` migration; schema 31 adds Body Part state. Body-part state is dual-axis (`Closed/Open/Consumed` plus `Intact/Broken`), reads a frame-start view for pre-hit scaling, commits accepted facts at authority-frame end, and fails closed for unsupported sources or missing explicit part evidence. The bridge exposes a typed ingress for accepted Action/Ability/Interaction Focus facts, but no production caller is connected yet; all current formal `focusConsumeTag` values are empty and therefore fail closed. Dynamic Focus/priority/lock selection reuses existing runtime state; authored Hittable/Aimable/Approachable, root-XZ distance, GeometryInReach and pose path remain unchanged. Missing definitions preserve legacy behavior, while source hash mismatch blocks state restore.

CharacterStudio edits the optional document only through the existing T1 revision-gated service: create/edit/delete, member mapping, thresholds/scales, validation, independent Undo/Redo and refresh recovery are present; reorder is draft-only and save/reopen canonicalizes by `partStateId`. Current requires `[1,8]` definitions: deleting the final card may remain a draft, but typed Authoring validation blocks save and RuntimeSpawn rejects an empty v5 binding. Missing remains the explicit legacy no-definition state, while Future/Invalid payloads remain exact raw read-only. Formal Dragon14 is calibrated to Head `34/68`, Left/Right Wing `51/85`, and Tail `68/90`, preserving its multipliers and priority. The production Head chain resolves `light_attack` on the formal 1.25 weak-point collider to 66 (HP `100→34`, Open), then resolves `light_attack_2` to 21 and applies the next-frame Open scale as 26 (HP `34→8`, Broken); one further real follow-up reaches HP 0 and emits exactly one death event. A second formal test proves isolated accumulation through actually hittable `head`, `left_wing_membrane`, `right_wing_membrane`, and `tail_tip` collider members. Eight distinct character entities sharing one physics world completed 32 warmups plus 4096 measured state/pre-hit-damage/selector iterations at 0 B managed allocation. The unique final review recorded P0=0/P1=0; LCC-M2 is Verified, and the verified M3 Runtime Slice and M4 Dragon14 terrain-pose capability follow.

## Large Character Contact M3 Severance Runtime Slice (Verified 2026-08-25)

Dragon14 tail severance now extends the same Body Part Damage document with `severable`, `severRequiredTag`, and `presentationEventId`, and extends authority state with the one-way `Broken -> Severed` terminal transition. Stable tail leaves are removed from the derived Hurtbox topology without renumbering surviving collider identities; `zone.tail` keeps its authored identity but becomes unavailable, stale tail reservations are swept, Planner/action dependencies fail closed, tail sweep/spin follow-up traces stop, and bite/other Zones continue. Presentation consumes the transient event or current state silently on late attach/restore; it never writes authority.

CharacterTest formal snapshot schema 32 extends schema 31 Body Part state with the legal `Severed` value. Schema 31 raw integer `2` validates as illegal and canonical legacy restore maps it to Broken, while future schema, source mismatch and envelope/document mismatch fail before authority mutation. Full/owned/non-owned hashes, rollback+1, replay and late join cover Severed without replaying the transient event. Active leaf masks, Zone validity/priority, presentation/VFX and view-only limb state remain derived and are not serialized.

This remains a Runtime Slice. The unique independent M3 review originally recorded `Not Verified` (P0=0, P1=2, P2=1, P3=2); the two P1 implementation defects were remediated by their original owners and closed by the main milestone owner with focused evidence, without adding a second independent review. M3 is therefore Verified while retaining its accepted P2/P3 limits. There is no production `StageAcceptedSever` caller/player path; the recorded after image was driven by an Editor-only test command. Dragon14 currently has one basemesh and no stable detachable tail resource, so the truthful presentation result is `RuntimeSliceNoStableResource`: no visual detach, VFX, SFX or limb instance. Head/wing severance are outside this slice; the separate M4 terrain-pose capability follows.

## Large Character Contact M4 Dragon14 Terrain Authority Pose (Verified 2026-08-25)

The fixed-point terrain-pose pipeline is a Framework Feature, with current Playable evidence intentionally limited to formal Dragon14 four-limb support. Package authoring supplies one `mx.characterAuthorityPoseRig.v1/1.0` source, compiled as additive geometry binding v6: 4 limbs/probes, 12 root/mid/tip channels, 4 Collider attachments and 4 Socket attachments. Combat supplies deterministic static ground facts; CharacterTest composes baked base pose, one root transform, quantized two-bone solve and non-conflicting secondary into one resolved frame/revision. Hurtbox, actual/preview `BakedSocketCapsule`, Planner and Unity View consume authored attachments from that frame. Unity Physics, Animator, bones and Transform are never authority inputs; presentation-only work cannot feed back into Combat or state.

Focused PlayMode covers flat/uphill/downhill, step edge, four unequal supports, no-hit, too-steep, out-of-reach, movement/turning, flying, knockback, death and tail Severed. Generic action-disabled and limb-Severed contracts are covered separately because formal Dragon14 currently authors no `disabledActionTags` and no four-limb `partStateId`. Dragon14 now has 3 PresentationOnly ContactPoint configs and 0 authority-pose ContactPoint attachments: they can drive base/presentation contact audio and debug, but do not receive terrain-channel authority deltas and do not block or push characters. Terrain-active formal evidence uses `attack1a` local frame 10 and only the clip's real `rightClawStart/rightClawEnd` tracks. The accepted Game View image uses tests-only colored markers rather than a production unified overlay, and its same-frame numerical assertions are the authority evidence.

CharacterTest schema 33 preserves future-affecting terrain history/source witnesses and participates in full/owned/non-owned hash, rollback, replay, late join and source-mismatch gates; derived resolved samples are rebuilt. A rollback+1 acceptance proves identical pose/revision, real Hurtbox capsule, actual BakedSocket trace, Planner command and state hash. The 8-Dragon calibration (300 warm-up, 1800 measured ticks) records the actual first `SyncAll` → 4 probes/IK → second `SyncAll` chain at 1.2385 ms p50 / 1.8214 ms p95, with 0 B managed allocation; no production overlay exists, so overlay-on is N/A. Both Hurtbox syncs remain measured and intentionally unoptimized because the frozen T0 gate set no absolute millisecond threshold and the verified slice passed capacity/steady-GC requirements.

The unique final independent read-only review recorded P0=0, P1=0, P2=1 and P3=3; the main milestone owner closed the parent-state P2 and accepted the catalog-size, double-Hurtbox-sync and formal-content coverage P3 limits. This verified capability does not claim spine-to-ground fitting, snakes/tentacles, full-body procedural animation, wing aerodynamics, ragdoll, detachable tail content or arbitrary creature rigs.

## Large Character Contact M5 Animated Appendage Soft Contact (Verified 2026-08-26)

CharacterTest can now treat an authored, fixed-capacity subset of animated Hurtbox leaves as derived soft-contact sources without adding the Hurtbox layer to hard movement obstacles, navigation, projectile blocking or damage. Body Presence v2 / geometry binding v7 references existing logical collider identities only; formal Dragon14 selects `hurt.head.obb.0`, `hurt.tail_mid.capsule.0` and `hurt.tail_tip.capsule.0`. The final same-frame DamageGeometry snapshot supplies shape/topology identity, M1's bounded three-pass pressure path supplies contact velocity, and Severed topology omission disables tail sources without a second severance table. Ordinary contact does not create Damage, HitResolve or Active Trace.

Contact pressure is external motion: it contributes to translation and locomotion blend but does not feed the post-physics final velocity back into authority yaw. Facing remains driven by locked target, explicit turn/action facing, active move input or the pre-external persistent locomotion direction.

Verified evidence proves formal head/tail-mid/tail-tip contacts at 4274/2747/4401 mm/s, next-tick root slides of 75/41/74 mm, unchanged target HP/CombatHit trace, direct moving/turning/high-speed clamp, wall/corner static-sweep safety, Severed tail loss with retained head contact, v7-compatible root Presence, M5 order/restore behavior and formal Dragon14 rollback+1 hash parity. A real 8-Dragon + 1-player RuntimeModule run with 3 sources/Dragon completed warm 300 / measure 1800 at 0 B managed allocation and whole-Tick p50/p95/max 2.126/3.010/3.964 ms; the existing Motion and AuthorityPose profiler seams were valid but returned no nonzero EditMode stage samples, so synthetic query/build/apply timings remain substage-only evidence. The unique final review closed both historical P1s and ended at P0=0/P1=0 open, P2=1/P3=2. The inspected Game View uses real formal views plus dense tests-only markers/Gizmos, not a production unified overlay. Wings, full-body animated collision, hard appendage walls, ragdoll and production appendage audio/VFX are unsupported; phase Source/Target identity must be corrected before a presentation consumer is connected.

## Character Authority Pose IK / Secondary Foundation (2026-06-13)

`MxFramework.Character.RuntimeSpawn` now provides a noEngine authority pose foundation for CharacterTest fixed-frame IK / secondary motion: `CharacterAuthorityPoseRigDefinition`, `CharacterAuthorityPoseStateComponent`, `CharacterAuthoritySecondaryBinding`, `CharacterAuthorityTwoBoneIkBinding`, `CharacterAuthorityPoseImpulseRequest`, `CharacterAuthorityPoseSnapshot`, and `CharacterAuthorityPoseHashUtility`. It runs on `RuntimeFrame`, `Fix64`, and `FixVector3`, not Unity `deltaTime`, `Transform`, `Rigidbody`, Collider, random state, or PhysX.

Each `CharacterTestCharacterEntity` owns a `CharacterAuthorityPoseStateComponent`. `CharacterTestCharacterRuntimeModule` ticks pose state after Combat trace and before animation/view sync, and CharacterTest network snapshots / runtime hashes include the authority pose snapshot. Unity views only mirror the snapshot after animation sampling, then re-sync bone/socket bindings so weapon attachments and socket VFX anchors follow the final displayed authority pose. The legacy Unity `CharacterProceduralBoneRig`, `CharacterSecondaryBoneChain`, `CharacterTwoBoneIkTarget`, and `CharacterViewOnlyRagdollRig` remain available only for explicit prefab/view debug and are no longer ordinary-hit or default F5-F8 paths.

Authority pose regions are config-driven. The canonical region id is the character package `CharacterBodyPartConfig.PartId` (for example `head`, `torso`, `left_hand`, or non-humanoid ids such as `core`, `shell`, `front_face`, `tail`, `wing`). Ordinary unknown hits resolve to `default`; the empty region is reserved for explicit broadcast/debug requests and must not be used as the unknown-hit fallback.

CharacterTest ordinary damage now has automatic authority secondary feedback. `CharacterTestProceduralHitFeedbackModule` consumes the current frame `DamageResolvedEvent` list, filters positive `HitResolveKind.Damage`, maps the strongest hit per target into a deterministic `CharacterAuthorityPoseImpulseRequest`, and schedules it at `currentFrame + 1` so it cannot rewrite the current hit result. The mapper resolves `DamageProfileUsage.BodyPartId` first, then `HitZoneId` through package geometry, then `default`. This does not call ragdoll, does not interrupt the current action, and does not require `HitPresentationEvent`; the pose state does enter runtime hash, replay, network full snapshot, and SaveState-compatible DTOs. `CharacterTestProceduralHitFeedbackOptions.Enabled=false` prevents module registration.

CharacterTest also exposes authority pose preview through temporary debug hotkeys: F5 `LightSecondary`, F6 `HeavySecondary`, F7 `FullRigPulse`, and F8 `SideSecondary`. `GameManager` samples these keys from the existing `IInputProvider.InputSnapshot` bindings and calls `GameSlice.TryApplyLockedTargetProceduralEffectPreview`; F5/F6 no longer start Fireball Burst / Fireball Lob directly. The preview targets the current locked target when available and falls back to the local player when there is no lock. It enqueues `CharacterTest.ApplyAuthorityPosePreview`, enters the command/replay/hash path, and records `LastProceduralEffectPreviewResult` diagnostics including known region count, active secondary count, last resolved region, match source, and latest issue.

> Status: Current
>
> 框架当前（2026-06-10 文档校准）**已经能做**什么。按你想做的事查找，不要按 Phase 翻。

## Character Cloth Motion Preview（2026-08-29）

Unity `MxFramework/Character/Cloth Authoring` 提供“保存并重建”主流程、Scene View 蒙皮骨骼可视化/点选创建 Cloth，以及隔离 Play Mode 肉眼运动预览。Scene 覆盖从真实 `SkinnedMeshRenderer` 收集骨骼，区分 Cloth Root/Collider Parent，并支持 3D 创建 Cloth、追加 Root 和 Esc 取消。预览读取角色正式 Runtime Prefab、生成的 Animation Clip Registry 与全局资源映射，支持动作切换、播放/暂停/循环/倍速、可调往返/横移/启停/转身/Z 字运动、固定世界/跟随角色双镜头、四向观察、米制网格/路径/位置标记、速度/加速度/角速度遥测、Runtime Collider 线框和 Cloth Reset。运动中可实时编辑内存 Cloth/Collider 草稿及 25 项 schema 1.1 typed 物理参数 Override；有效变更去抖后在冻结 Preset 基线上重建运行投影，UI 回读实际 Run 值并标注 BoneCloth/Self Collision/风源适用条件，专用 Magica WindZone 和急停/转身压力脉冲提供可见刺激。校验失败保留最后有效投影，只有“保存并重建”才持久化。该入口是 Editor Validation surface，只观察表现层 Cloth，不进入 Gameplay/Combat authority、SaveState、Replay、网络或 Runtime hash。focused PlayMode 自动验收仍作为更多菜单中的独立结构检查存在。
>
> 状态标记：✅ v1 可用 | ✅ v0.x 部分可用 | 🔄 设计中 | 📋 规划中

---

## 1. 运行时核心：给游戏实体加属性、挂 Buff、跑 Modifier

### 1.1 属性系统 — 给任何实体挂数值属性

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 注册属性，读取 base/final value | ✅ v1 | `AttributeStore.RegisterAttribute()` / `GetAttribute()` | Attributes |
| 添加计算阶段修改器（Add/Mul/FinalMul） | ✅ v1 | `IAttributeModifier` / `AttributeStore.AddModifier()` | Attributes |
| 属性变化时收到事件 | ✅ v1 | `AttributeStore.OnAttributeChanged` | Attributes |
| 调试快照 | ✅ v1 | `AttributeStore.CreateSnapshot()` | Attributes |

→ 代码示例：`USAGE.md §3`
→ 测试：`Tests/Attributes/AttributeStoreTests.cs`
→ Demo 运行：`RuntimeVerticalSliceRunner._useHardcoded = true`

### 1.2 Buff 系统 — 生命期管理 + 堆叠策略

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| Component-native Buff 定义 | ✅ v2 | `GameplayBuffDefinition` / `GameplayBuffDefinitionRegistry` / definition hash | Gameplay |
| Apply / Refresh / Remove 请求 | ✅ v2 | `GameplayBuffRequestBuffer` / `GameplayBuffApplicationSystem` / `ApplyComponentBuff` / `RemoveComponentBuff` | Gameplay |
| 主动 Buff 状态 | ✅ v2 | `GameplayComponentBuffSetComponent` / `GameplayComponentBuffEntry` / schema-backed hash & SaveState | Gameplay |
| 属性 / 状态投影 | ✅ v2 | `GameplayBuffEffectProjectionSystem` / source-aware modifier & status components | Gameplay |
| Component-native 光环源状态 | ✅ v0.1 | `GameplayComponentAuraSourceEntry` / `GameplayComponentAuraSourceCleanupSystem` / schema-backed hash & SaveState | Gameplay |
| Combat 跟随施法者光环 Buff | ✅ v0.1 | `CombatGameplayAuraBuffAbility` / `CombatGameplayAuraBuffSystem` / `ICombatGameplayAuraAnchorProvider` | Combat.GameplayBridge + Gameplay |
| Combat 命中后申请 Buff | ✅ v0 | `CombatHitEventConfig.OnHitBuffs` / `CombatOnHitBuffApplicationSystem` / `GameplayBuffRequestBuffer` | Combat.GameplayBridge + Gameplay |
| Buff v2 Authoring JSON / CLI | ✅ v0 | `buff validate` / `buff compile` / `buff import-unity` -> `GameplayBuffDefinitionJsonImporter` / `CombatGameplayAuraBuffDefinitionJsonImporter` | Tools + Gameplay + Combat.GameplayBridge |
| Buff v2 Workbench | ✅ v9 | EditorHub tool for existing `buff_authoring.json` package edit / designer-friendly field labels and options / `types[]` multi-type classification / `瞬时-持续-永久` time input / simplified `可叠层-最大层数` input / localized name key + fallback display name / Auto-Manual effect description / icon key warning / Validate-gated authoring diff / save / generated diff compile preview / explicit generated JSON import | Tools |
| Buff v2 表现快照 | ✅ v0.2 | `GameplayBuffPresentationDefinitionRegistry` / `GameplayBuffPresentationSnapshotBuilder` / `buff_presentation_definitions.json` with `nameKey`, `descriptionMode`, fallback `displayName`, generated/manual description, shortName, icon key, inferred compatibility `polarity`, and effect summary | Gameplay + Game.CharacterTest |

→ 代码示例：`USAGE.md §4`
→ 测试：`Tests/Ability/GameplayComponentBuffModifierTests.cs`、`Tests/Combat/GameplayBridge/CombatGameplayAuraBuffSystemTests.cs`

### 1.3 Modifier 系统 — 条件 + 效果组合

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 添加/替换/移除 Modifier | ✅ v1 | `ModifierPipeline.AddModifier()` / `RemoveModifier()` | Modifiers |
| 条件评估 + 效果执行 | ✅ v1 | `IModifierCondition` / `IModifierEffect` | Modifiers |
| 上下文池化（零分配） | ✅ v1 | `ModifierContext.Get()` / `Push()` | Modifiers |
| 计数器系统 | ✅ v1 | `CounterStore.SetCounter()` / `AddCounter()` / `GetCounter()` | Modifiers |
| 配置驱动创建 | ✅ v1 | `ConfiguredModifier` / `ConfigModifierFactory<TConfig>` | Config.Runtime |

→ 代码示例：`USAGE.md §5`
→ 测试：`Tests/Modifiers/ModifierPipelineTests.cs`, `Tests/Modifiers/CounterStoreTests.cs`

### 1.4 事件总线 — 模块间消息

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 类型安全订阅/发布 | ✅ v1 | `IEventBus<T>` / `EventBus<T>` | Events |
| 防重入 | ✅ v1 | 发布期间新订阅下次生效 | Events |
| 异常传播 | ✅ v1 | handler 抛异常停止本次发布 | Events |

→ 代码示例：`USAGE.md §2`
→ 测试：`Tests/Events/EventBusTests.cs`

### 1.5 Runtime Host — 运行时组合根和生命周期调度

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 模块注册和重复 ID 检测 | ✅ v0.1 | `RuntimeHost.RegisterModule()` / `IRuntimeModule.ModuleId` | Runtime |
| 生命周期调度 | ✅ v0.1 | `Initialize()` / `Start()` / `Tick()` / `Stop()` / `Dispose()` | Runtime |
| Tick 分组和稳定排序 | ✅ v0.1 | `RuntimeTickStage` / `Priority` / `ModuleId` | Runtime |
| 错误策略和错误快照 | ✅ v0.1 | `RuntimeHostErrorPolicy` / `RuntimeHostError` / `RuntimeHostDiagnostics` | Runtime |
| 组合根服务表 | ✅ v0.1 | `RuntimeServiceRegistry` / `IRuntimeServiceRegistry` | Runtime |
| 显式 Frame Clock | ✅ v0.1 | `RuntimeFrame` / `RuntimeClock.Step()` / `Reset()` | Runtime |
| Command Buffer | ✅ v0.1 | `RuntimeCommand` / `RuntimeCommandBuffer.Enqueue()` / `DrainForFrame()` | Runtime |
| Command validation result | ✅ v0.1 | `RuntimeCommandValidationResult` / `RuntimeCommandError` / `IRuntimeCommandValidator` | Runtime |
| Trusted command source authorization | ✅ v0.1 | `RuntimeCommandSourceRegistry` / `RuntimeCommandSubmissionGate` / source allowlist | Runtime |
| Replay frame recorder | ✅ v0.1 | `RuntimeReplayRecorder` / `CreateExogenousStream()` / `RuntimeReplayCommandCaptureMode` / `RuntimeReplayFrameRecord` / `RuntimeReplaySnapshot` | Runtime |
| Replay playback runner | ✅ v0.1 | `RuntimeReplayPlaybackRunner` / `IRuntimeReplayFrameDriver` / `RuntimeReplayPlaybackResult` | Runtime |
| Runtime result hash contract | ✅ v0.1 | `IRuntimeHashContributor` / `RuntimeHashAccumulator` / `RuntimeHashCombiner` | Runtime |
| SaveState DTO contract | ✅ v0.1 | `RuntimeSaveState` / Entity / Attribute / Buff / Modifier / Ability / Counter / CustomState DTO | Runtime |
| SaveState provider/restorer contract | ✅ v0.1 | `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer` | Runtime |
| SaveState orchestration | ✅ v0.1 | `RuntimeSaveStateRegistry` / `RuntimeSaveStateCoordinator` | Runtime |
| SaveState structured errors | ✅ v0.1 | `RuntimeSaveStateErrorCode` / `RuntimeSaveStateError` / `RuntimeSaveStateResult<T>` | Runtime |
| SaveState migration pipeline | ✅ v0.1 | `IRuntimeSaveStateMigration` / `RuntimeSaveStateMigrationPipeline` | Runtime |
| SaveState JSON roundtrip | ✅ v0.1 | `RuntimeSaveStateJson.SaveToJson()` / `LoadFromJson()` | Runtime |
| Preview Host adapter | retired | Removed with the legacy Buff preview runtime; current Runtime slices use direct Gameplay / Combat systems | Runtime |

→ 接口：`Interfaces/Runtime.md`
→ 测试：`Tests/Runtime/RuntimeHostTests.cs`, `Tests/Runtime/RuntimeFrameCommandReplayTests.cs`, `Tests/Runtime/RuntimeReplayPlaybackTests.cs`, `Tests/Runtime/RuntimeHashContributorTests.cs`, `Tests/Runtime/RuntimeSaveStateTests.cs`, `Tests/Runtime/RuntimeSaveStateOrchestrationTests.cs`, `Tests/Ability/RuntimeAbilitySliceRuntimeFoundationTests.cs`
→ 验证夹具：`Demo/Tetris/TetrisRuntimeValidation.cs` 用纯 C# Tetris 覆盖 RuntimeHost、CommandBuffer、Replay hash playback、SaveState JSON roundtrip
→ Ability Showcase 已接入：HUD 手动命令经 `RuntimeCommandBuffer` 入队，由 `RuntimeHost` 帧执行；每帧记录 replay result hash；支持 Ability Slice save -> reset -> load -> continue。
→ **不含**: JSON replay export/playback、command network wire/跨进程身份认证、具体 Gameplay/Combat hash contributor adapter、通用 Gameplay restore 编排接入

### 1.5A Application Startup Core — noEngine 启动事务编排

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 稳定 plan/step identity 与 DAG validation | ✅ Phase 1 Core | `ApplicationStartupPlan.TryCreate()` / stable topological order | Application |
| 串行 forward runner、协作 cancel/timeout drain | ✅ Phase 1 Core | `ApplicationStartupCoordinator.Start()` / `Tick()` / `RequestCancel()` | Application + Runtime operation contract |
| reverse completion rollback 与错误聚合 | ✅ Phase 1 Core | `BeginRollback()` / `FinalizeRollback()` / `RollbackErrorCount` | Application |
| immutable live-state publication seam | ✅ contract only | `LiveApplicationState` / `ILiveApplicationStatePublisher` / `IApplicationStartupTransaction` | Application |
| Persistence Runtime bridge | ✅ Phase 3 | durable checkpoint/commit、prepare-load、candidate restore lease；不发布 live session | `MxFramework.Persistence.RuntimeBridge` |
| Runtime/Resource/Scene/Platform application bridge | ⏳ pending | 依赖 #702、#709、#710 | future bridge assemblies |
| Unity Boot / Loading / Retry / suspend policy | ⏳ pending | 依赖 #706、#710 | ApplicationStartup.Unity / composition root |

→ 接口：`Interfaces/Application.md`
→ **不含**：具体 candidate RuntimeHost、SaveGame load、Scene/Resource handoff、Platform lifecycle policy 或可玩 Unity 入口；不能据此声明 production-safe atomic load。

### 1.5.1 Story Core / Runtime — 剧情最小可回放运行时切片

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 确定性 Story blackboard | ✅ v0.1 | `StoryBlackboard` / `StoryFactKey` / `StoryFactCopyResult` | Story |
| 最小 graph / beat / step / choice flow | ✅ v0.1 | `StoryGraphDefinition` / `StoryBeatDefinition` / `StoryDirector` | Story |
| Story RuntimeCommand 边界 | ✅ v0.1 | `StoryRuntimeCommandFactory` / `StoryRuntimeCommandValidator` | Story.Runtime + Runtime |
| Runtime event queue | ✅ v0.1 | `RuntimeEventQueue<StoryRuntimeEvent>` | Story.Runtime + Runtime |
| Runtime hash contributor | ✅ v0.1 | `StoryRuntimeHashContributor` | Story.Runtime + Runtime |
| Runtime SaveState provider/restorer | ✅ v0.1 | `StoryRuntimeSaveStateProvider` | Story.Runtime + Runtime |
| Replay/hash validation | ✅ v0.1 | Existing `RuntimeReplayRecorder` / `RuntimeReplayPlaybackRunner` with Story commands | Story.Runtime + Runtime |
| Story config schema / mapper / validator | ✅ v0.1 | `StoryConfigSet` / `StoryGraphConfigMapper` / `StoryConfigValidator` | Story.Config + Config |
| Story external authoring draft CLI | ✅ v0.1 | `Tools/MxFrameworkStoryAuthoring/story_authoring.py import-markdown` / `validate` | Tools + Story.Config handoff |
| Story Gameplay bridge | ✅ v0.1 | `StoryGameplayEffectBridge` / `StoryBeatGameplayLocator` / `StoryModifierConditionAdapter` | Story.GameplayBridge + Gameplay |
| Story Resources preload plan bridge | ✅ v0.1 | `StoryResourcePreloadPlanBuilder` / `StoryResourcePreloadMetadata` | Story.ResourcesBridge + Resources |
| Story Runtime AI Planner fact projection | ✅ v0.1 | `StoryRuntimeAiWorldStateProjector` / `StoryRuntimeAiProjectionProfile` | Story.RuntimeAiPlannerBridge + Runtime AI Planner |
| Story Unity adapters | ✅ v0.1 | `StoryTriggerZoneAdapter` / `StoryPresentationCompletionAdapter` / `StoryRuntimeEventPresentationRouter` | Story.Unity + Runtime |
| Story Editor debug surface | ✅ v0.1 | `StoryRuntimeDebugSource` / `StoryEditorDebugRegistry` / `StoryRuntimeDebugWindow` | Story.Editor + DebugUI |
| Story playable vertical slice | ✅ v0.1 | `StoryRuntimeVerticalSliceDemo` / `StoryRuntimeVerticalSliceRunner` | Demo + Story + Gameplay + UI Toolkit |

→ 接口：`Interfaces/Story.md`, `Interfaces/Story.Runtime.md`, `Interfaces/Story.Config.md`, `Interfaces/Story.GameplayBridge.md`, `Interfaces/Story.ResourcesBridge.md`, `Interfaces/Story.RuntimeAiPlannerBridge.md`, `Interfaces/Story.Unity.md`, `Interfaces/Story.Editor.md`
→ 测试：`Tests/Story/StoryBlackboardTests.cs`, `Tests/Story.Runtime/`, `Tests/Story.Config/`, `Tests/Story.GameplayBridge/`, `Tests/Story.ResourcesBridge/`, `Tests/Story.RuntimeAiPlannerBridge/`, `Tests/Story.Unity/`, `Tests/Story.Editor/`, `Tests/Demo/Story/`
→ 外部工具：`Tools/MxFrameworkStoryAuthoring/tests/` 覆盖 Markdown import、`.story.json` validate 和结构化 diagnostics。
→ 交付等级：`Playable` for `Assets/Scenes/StoryRuntimeVerticalSlice.unity` when generated through the Unity menu/MCP and smoke-validated；runtime/config/bridge APIs remain reusable slices.
→ **不含**: direct Story buff grant/remove、Timeline/Cinemachine package-specific binding、full Yarn/Ink/Articy importer、Authoring AI Assist execution、WGame production story content

### 1.5.2 VFX — Tick 驱动、可回放的表现意图 / 实例层

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine VFX definition / instance runtime | ✅ v0.1 | `VfxDefinition` / `VfxDefinitionRegistry` / `VfxRuntimeModule` | Vfx + Runtime |
| 独立 VFX command buffer | ✅ v0.1 | `VfxRuntimeCommandFactory` / `VfxRuntimeCommandValidator` / dedicated `RuntimeCommandBuffer` | Vfx + Runtime |
| Replay hash contributor | ✅ v0.1 | `VfxRuntimeHashContributor` | Vfx + Runtime |
| SaveState provider/restorer | ✅ v0.1 | `VfxRuntimeSaveStateProvider` | Vfx + Runtime |
| Gameplay event fanout / bridge | ✅ v0.1 | `GameplayRuntimeEventFanout` / `GameplayVfxBridge` | Gameplay + Vfx.GameplayBridge |
| Combat action / hit bridge | ✅ v0.1 | `CombatVfxBridge` / `CombatVfxPhysicsAdapter` | Combat + Vfx.CombatBridge |
| Rendering semantic event bridge | ✅ v0.1 | `VfxRenderingBridge` -> `IRenderDataPublisher` | Vfx.RenderingBridge + Rendering |
| Unity prefab / ParticleSystem presenter | ✅ v0.1 | `UnityVfxPresenter` + `IResourceManager.Load<GameObject>` | Vfx.Unity + Resources |
| VFX Showcase scene | ✅ v0.1 | `Assets/Scenes/MxVfxShowcase.unity` / `VfxShowcaseDemoBootstrap` / `MxFramework/VFX/Generate VFX Showcase Scene`；按 `domain.vfx` catalog 自动列出 RuntimeDefinition 与 Catalog Prefab，支持搜索、分组、重播、loop、auto advance 和 runtime length 参数 | Demo + Vfx + Vfx.Unity + Resources + UI Toolkit |
| VFX semantic catalog / reuse governance / manual composition / Development Agent creation | ✅ generic authoring workbench; ParticleSystem v3 semantic authoring infrastructure | 37/37 semantic coverage；Source Library 完整保留 506 个 Prefab、13,425 个 ParticleSystem 和 1,993 个支持资产。当前项目启用 `DraftLocalOnly`，全局精选与提取注册表为空，工作台只显示 `Fireball Impact 工作草稿` 的完整 `Hit_Nova_Light` Compound Element；7 个成员仅在详情中编辑。自动精选和完整源索引仍是通用框架能力，但不会在该模式下重新填充素材栏。当前草稿、Candidate、已发布 Prefab、Ready 回执和正式绑定记录均保留，运行时资源仍可解析；既有 Ready approval / binding 中记录的 Prefab content hash 已与当前资产字节不一致，重新激活前必须重新人工审批，不能由清理任务自动续签。 | Authoring Core + CLI + Editor + optional VfxGraph + Demo |
| Projectile flight VFX presenter | ✅ v0.1 | `CombatProjectileUnityPresenter` 读取 `CombatProjectileRuntimeState`，view-only 同步 flight prefab；binding 可由 generated projectile presentation JSON 导入；不进入 VFX hash / SaveState | Combat.Projectiles.Unity + Resources |
| CharacterTest action windup / aura VFX | ✅ v0.3 | CharacterAction `SpawnVisualCue` 事件进入 `VfxSourceDomains.ActionPresentation`；占位调整期 Fireball / Fireball Burst frames 0-29 播放并跟随施法者锚点的 charge cue，所有有限 VFX Definition/Trigger 至少保持 30 帧，Guard Aura 在 action frame 0 发出脚底中心 4m 可视光环并由 VFX runtime 保持 300 帧跟随施法者，projectile 仍只由原 gameplay request frame 6 生成 | CharacterAction + Game.CharacterTest + Vfx |
| Projectile Authoring + CharacterAction reference | ✅ v1.0 | 独立 Projectile Authoring 是 Definition、Request/Spawn 与 Presentation 的唯一 Unity writer，提供受限规则编辑、lifecycle VFX v2、确定性隔离预览、无损 Core/CLI 事务及 Generated/Player mirror 原子发布；CharacterAction 只保存 Request Stable ID/触发帧并显示只读摘要、引用健康度与 deep link。Arc Mine/Poison Bottle/Rune Turret 等 mechanic tuning 仍由 consumer 所有者维护。现有 20 个 `PROJAUTH013` 是明确的 v1 Presentation compatibility debt，不构成第二写入口。 | ProjectileAuthoring.Editor + CharacterAction.Editor + Combat.Projectiles + Authoring |

→ 接口：`Interfaces/Vfx.md`
→ 测试：`Tests/Vfx/VfxRuntimeTests.cs`、`Tests/Demo/VfxShowcaseDemoTests.cs`
→ **边界**: VFX runtime 保存的是 spawn/stop/参数/anchor/active instance 状态，不做粒子级确定性模拟；Unity prefab、ParticleSystem、VFX Graph、Unity Physics 和 Unity instance id 不进入 replay hash、Runtime hash 或 SaveState。第一版确定性物理集成只消费 `CombatPhysicsWorld` 查询结果。

### 1.5.3 Network — 服务端权威 gameplay session 抽象

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine 网络抽象程序集 | ✅ v0.1 | `MxFramework.Network.asmdef` (`noEngineReferences=true`) | Network |
| session 配置与 handshake 校验 | ✅ v0.1 | `NetworkSessionConfig` / `NetworkHandshakeRequest` / `NetworkHandshakeResponse` | Network + Runtime |
| 传输无关 message envelope | ✅ v0.1 | `INetworkTransport` / `NetworkMessageEnvelope` | Network |
| noEngine JSON wire codec | ✅ v0.1.10 | `INetworkMessageCodec` / `JsonNetworkMessageCodec` / `NetworkCodecError` / `NetworkSnapshotRequestPacket` | Network |
| codec-backed byte loopback | ✅ v0.1.1 | `CodecLoopbackNetworkTransport` | Network |
| Unity Transport adapter | ✅ v0.1.9 | `UnityTransportNetworkTransport` / `UnityTransportNetworkOptions` / queue capacity / client restart | Network.UnityTransport |
| transport event 与 heartbeat 诊断 | ✅ v0.1.8 | `NetworkTransportEvent` / `NetworkHeartbeatPacket` / smoothed RTT window | Network |
| reliable ordered sequence diagnostics | ✅ v0.1.1 | duplicate drop / out-of-order counters / `LastReceivedSequence` | Network |
| session runner 结构收敛 | ✅ v0.1.11 | `INetworkMessagePayload` / internal sequence, per-peer ack, connected peer ids, disconnect sequence reset, rolling diagnostics, digest/full snapshot helpers | Network |
| LAN Room Session MVP | ✅ v0.1.12 | `NetworkRoomSessionRunner` / `NetworkRoomConfig` / join-ready-start handoff / room diagnostics | Network |
| server command authorization hook | ✅ v0.1.11 | `NetworkCommandAuthorizationResult` / server-side command authorizer before `RuntimeCommandBuffer` enqueue | Network |
| 客户端命令包进入服务端 command buffer | ✅ v0.1 | `NetworkRuntimeCommandPacket` / `NetworkSessionRunner.SubmitCommands()` | Network + Runtime |
| 服务端权威命令广播 | ✅ v0.1.17 | `NetworkAuthoritativeCommandPacket` / scheduled accepted commands / own echo ack / late command full snapshot request | Network + Runtime |
| 权威 digest/full snapshot + hash resync | ✅ v0.1.11 | `NetworkSnapshotPacket` / `NetworkSnapshotRequestPacket` / `INetworkSnapshotParticipant` / `INetworkPeerSnapshotParticipant` / receiver-specific result hash / `RuntimeCustomState` | Network + Runtime |
| 预测 client snapshot 策略 | ✅ v0.1.10 | `NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly` / `NetworkSnapshotHashLookupResult` / same-frame hash diagnostics / bounded prediction lead | Network |
| 通用 prediction rollback 协调器 | ✅ v0.1.20 | `INetworkRollbackParticipant` / `NetworkPredictionRollbackCoordinator` / `RuntimeCommandBufferSnapshot` / `NetworkSnapshotCorrectionResult` / 128-frame default history / authoritative command receive hook / snapshot correction hook | Network + Runtime |
| protocol-2 bounded binary wire | ✅ 19.4B | `NetworkBoundedWire` / kinds 10..13 / exact negotiation / atomic outer+semantic frontiers / 8192 B command + 524288 B snapshot limits | Network |
| bounded state registry / rollback history | ✅ 19.4B | `NetworkBoundedStateRegistry` / `INetworkBoundedStateParticipant` / `NetworkBoundedRollbackHistory` / additive coordinator bounded history seam | Network |
| Gameplay Trigger network authority / resync | ✅ 19.4B | `GameplayTriggerNetworkProfile` / authority+snapshot ingress / Gameplay 100 -> Trigger 200 state bundle / rollback-resync / FullComparable + PartialNonComparable | Gameplay.Triggers.NetworkBridge + Network + Runtime + Gameplay |
| noEngine loopback 测试传输 | ✅ v0.1 | `LoopbackNetworkTransportOptions` / `LoopbackNetworkTransport` | Network |
| CharacterTest loopback playable 接入 | ✅ v0.1 | `CharacterTestNetworkLoopbackRunner` / `CharacterTestNetworkSnapshotParticipant` | Game.CharacterTest + Network |
| CharacterTest 传输无关 session runner | ✅ v0.1.4 | `CharacterTestNetworkSessionRunner` | Game.CharacterTest + Network |
| CharacterTest 网络组合根 factory | ✅ v0.1.5 | `CharacterTestNetworkSessionFactory` | Game.CharacterTest + Network |
| CharacterTest Unity Transport 本机 loopback | ✅ v0.1.4 | `CharacterTestNetworkMode.UnityTransportLoopback` / `UnityTransportNetworkTransport` | Game.CharacterTest + Network.UnityTransport |
| CharacterTest Unity Transport Host / Join 入口 | ✅ v0.1.16 | `CharacterTestNetworkMode.UnityTransportHost` / `UnityTransportClient` / host local client peer / MVP reconnect | Game.CharacterTest + Network.UnityTransport |
| CharacterTest 玩家主菜单 | ✅ v0.1.14 | `CharacterTestFairyGuiMainMenuHost` / `MxCharacterTestUi/MainMenuPanel` / `ICharacterTestMainMenuController` / on-demand NetworkPanel and SettingsPanel | Game.CharacterTest + UI.FairyGUI |
| CharacterTest Settings 自动反馈运行时切片 | ✅ runtime / 🔇 content pending | generated v2 feedback policy / `MxUiFeedbackCommandSink` / real Settings outcome / `MxAudioUiFeedbackPlayer`; 四个 UI Cue 尚未发布，当前静音降级 | Game.CharacterTest + UI + UI.Audio + Audio |
| CharacterTest 能力热栏与绑定 UI | ✅ v0.1 | `CharacterTestAbilityHotbarRuntime` / `CharacterTestRuntimeCommandIds.CastAbilitySlot` / `CharacterTestFairyGuiAbilityBindingPanelHost` / `MxCharacterTestAbilityUi/AbilityBindingPanel` | Game.CharacterTest + Runtime + UI.FairyGUI |
| CharacterTest Runtime AI Planner 策略选择 UI | ✅ v0.1 | `CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelHost` / `MxCharacterTestHud/RuntimeAiBehaviorLoadoutPanel` / `SetRuntimeAiBehaviorSelection` / selection phase gate | Game.CharacterTest + Runtime + UI.FairyGUI |
| CharacterTest Runtime AI Planner 竞技场测试场景 | ✅ v0.1 | `Assets/Scenes/CharacterTestArenaScene.unity` / `CharacterTestArenaSessionController` / `MxCharacterTestHud/ArenaSetupPanel` / clean offline session rebuild / behavior loadout selection per team slot | Game.CharacterTest + Runtime + UI.FairyGUI |
| CharacterTest 玩家 LAN 联机大厅 | ✅ v0.1.15 | `CharacterTestFairyGuiNetworkPanelHost` / generated `CharacterTestFairyGuiNetworkPanelManifest.g.cs` + `CharacterTestFairyGuiNetworkPanelBinder.g.cs` / `MxCharacterTestNetworkUi/NetworkPanel` / Direct LAN + LAN Room `LaunchMode` / `ICharacterTestNetworkSessionController.StartNetworkSession(...)` / default offline | Game.CharacterTest + UI.FairyGUI + Network.UnityTransport |
| CharacterTest MPPM 自动联机 smoke | ✅ v0.1.18 | `CharacterTestNetworkAutomationRequest` / `CharacterTestNetworkAutomationRunner` / JSON reports / replay result fields / `CharacterTestNetworkAutomationTests.RoomHostAndOneVirtualPlayer_AutoSyncProbeAndExit` | Game.CharacterTest + Network.UnityTransport + Unity Test Framework |
| CharacterTest authoritative replay diagnostic | ✅ v0.1.19 | `CharacterTestReplayRecorder` / `CharacterTestReplayFrameDriver` / `ICharacterTestNetworkReplayRunner` / server accepted scheduled commands | Game.CharacterTest + Runtime + Network |
| CharacterTest FairyGUI 本地化 | ✅ v0.1 | `CharacterTestLocalizationRuntime` / `MxConfigUiTextProvider` / `StreamingAssets/MxFramework/Localization/character_test.tsv` | Game.CharacterTest + Config + UI.ConfigBridge + Settings |
| CharacterTest Unity Transport Room Host / Join 入口 | ✅ v0.1.16 | `UnityTransportRoomHost` / `UnityTransportRoomClient` / host local room member / Debug UI room status / auto-ready-start | Game.CharacterTest + Network |
| CharacterTest peer ownership / assignment | ✅ v0.1.11 | `CharacterTestNetworkOwnershipState` / `peerId -> controlledEntityId` / full snapshot assignment / owned-only prediction hash | Game.CharacterTest + Network |
| CharacterTest 远程 client live rollback | ✅ v0.1.20 | wait for assignment / owned local predicted `RuntimeCommandBuffer` enqueue / authoritative command correction rollback / full snapshot correction replay / fixed 128-frame prediction hash + rollback history / rollback diagnostics | Game.CharacterTest + Network |
| CharacterTest headless client mirror | ✅ v0.1 | `GameSlice(createCharacterViews:false)` | Game.CharacterTest |
| CharacterTest control intent commandization | ✅ v0.1.3 | `CharacterTestRuntimeCommandIds.SetControlIntent` / `CharacterTestLocalInputCommandModule` | Game.CharacterTest + Runtime |
| CharacterTest Debug UI 网络诊断 | ✅ v0.1.20 | `CharacterTestNetworkLoopbackDiagnostics` / local peer, owned entity, ownership summary, RTT, prediction lead, rollback history/drop/count/failure/snapshot correction/replayed/max/last frame, snapshot interval, frame drift, authoritative command counts, bytes, full/digest counts | Game.CharacterTest + DebugUI |

→ 接口：`Interfaces/Network.md`
→ 测试：`Tests/Network/NetworkSessionRunnerTests.cs`, `Tests/Network/NetworkMessageCodecTests.cs`, `Tests/Network/UnityTransportNetworkTransportTests.cs`, `Tests/Game/CharacterTest/CharacterTestNetworkLoopbackTests.cs`, `Tests/Game/CharacterTest/CharacterTestReplayTests.cs`
→ CharacterTest 场景接入：`CharacterTestScene` 进入 Play 默认创建离线会话并显示 FairyGUI `MxCharacterTestUi/MainMenuPanel` 暂停菜单弹窗。菜单或从菜单打开的 NetworkPanel 可见时默认暂停 runtime ticking；该 UI 组合策略由代码内默认值固定，不再作为 `CharacterTestSceneProfile` 的可编辑项。Pause input 可打开菜单，MainMenu 打开时同一有效按键通过 UI Cancel 关闭菜单；Unity Editor Play 下 CharacterTest 会把现有 `Gameplay/Pause` 和 `UI/Cancel` 的键盘默认临时切到 `P`，让 `Esc` 保留给 Game 视图释放鼠标，Player 包仍使用默认 `Esc`。Start / Close 会隐藏菜单并按手动暂停状态恢复或保持当前会话；Reset Local 会重建干净的本地离线会话并保持菜单可见；Network 会按需打开 `MxCharacterTestNetworkUi/NetworkPanel`，Settings 会按需打开 `MxCharacterTestSettingsUi/SettingsPanel`，Back 只在非 active session 下返回主菜单。主菜单、联机大厅和设置面板都是真实 FairyGUI 源包组件，分别来自 Composer-owned `FGUIProject/assets/MxCharacterTestUi/MainMenuPanel.xml`、`MxCharacterTestNetworkUi/NetworkPanel.xml` 和 `MxCharacterTestSettingsUi/SettingsPanel.xml`，不是运行时动态拼接临时面板。NetworkPanel 提供 Direct LAN / LAN Room segmented mode，并持久化上次使用的模式、地址、端口、玩家名和房间名，但不会持久化运行时 client peer id：Host / Join 继续表示角色，`LaunchMode` 决定映射到 `UnityTransportHost` / `UnityTransportClient` 或 `UnityTransportRoomHost` / `UnityTransportRoomClient`；Host 会开本机权威 server 并用本机 client peer 连接自己，所以房主也通过 assignment、command packet 和 snapshot/resync 路径获得 owned entity；Host / Join 启动成功后停留在全屏 NetworkPanel 中验通，Room 模式由成员 Ready 后 Host 点击 Enter Game，随后各实例在本地 gameplay handshake 验通后自动关闭面板并恢复 gameplay tick；Direct LAN 仍作为连接诊断模式，验通后本实例本地点 Enter Game 进入；玩家面板默认会为每个运行实例生成稳定的唯一 client peer id，避免多实例默认都以 `client-0` 接入而撞到同一 ownership；切换时释放当前 runner/UI/camera/slice 并重建 CharacterTest 会话，不迁移当前离线战斗状态；v1 不新增 AppFlow/SceneFlow 状态。本地输入会采样为 `SetControlIntent` command，camera / target-lock 等 runtime command 也先进入 client packet，再由 server slice 授权、排帧并广播为 authoritative command；其它 client 将远端玩家标记为 `RemotePlayer` 并用同一 `RuntimeCommandBuffer` 确定性推进移动/动画。玩家流程默认采用 `BootstrapOnly`：正常 gameplay 不发送周期性 full/digest state snapshot；full snapshot 仅用于初次 assignment、late join、显式 snapshot request / resync。可选 `SnapshotVerification` 策略才发送低频 digest，并可通过 full snapshot correction 的 rollback hook 恢复权威 snapshot，再 replay 本地保留的 predicted command。headless client mirror 不生成重复 Unity view，通过 initial / requested full snapshot 收敛。`CharacterTestSceneProfile` 不保存玩家会在 NetworkPanel 中填写的地址、端口、玩家名或房间名；这些连接/房间值来自 `CharacterTestNetworkSessionOptions` 默认常量和 `CharacterTestNetworkPanelPreferences` 持久化输入。Profile 只保留 network input delay / snapshot / heartbeat、loopback impairment 和 UTP queue / window / capacity 等低层调参；Debug UI `Network Session` 只负责深度诊断，并显示 prediction 与 rollback history、rollback 次数、失败和 snapshot correction 计数。Room 玩家流程覆盖为手动 Ready / Host-only Enter Game，不使用默认 auto-ready / auto-start 作为玩家入口。UTP adapter 支持 MVP client restart、同 peerId 重握手、driver update failure 恢复和 receive/send queue capacity 配置。
→ CharacterTest 主 HUD 网络延迟：进入角色控制后的 FairyGUI `CombatHudPanel` 会在网络 session active 时显示简洁 RTT badge，例如 `NET 2f / 33ms`；离线隐藏。完整 RTT、frame drift、authoritative command、rollback 和 snapshot 诊断仍由 Debug UI `Network Session` 与 NetworkPanel 诊断区承担。
→ CharacterTest 能力热栏：进入角色控制后的 FairyGUI `CombatHudPanel` 常驻显示 1~9 能力槽、短名、选中态和 cooldown remaining；暂停菜单的 Abilities 按钮打开全屏 `MxCharacterTestAbilityUi/AbilityBindingPanel`，可编辑槽位、Clear、Reset Defaults、Save 或 Cancel。默认绑定为 1 Fireball、2 Fireball Burst、3 Flame Lance、4 Fireball Lob、5 Fireball Surge、6 Fireball Return、7 Tornado、8 Guard Aura、9 Fireball Explosion；Fireball Sway、Area Confinement、Beam Lance、Channel Beam、Laser Lance、Chain Lightning、Poison Bottle、Stun Bolt、Meteor Barrage、Sword Wave、Dash Strike、Hook Chain、Arc Mine 和 Rune Turret 保留为可绑定能力；保存文件是 `Application.persistentDataPath/MxFramework/CharacterTest/ability_hotbar.json`。数字键 1~9 是 CharacterTest 本地 gameplay adapter，提交 `CastAbilitySlot` runtime command，不进入 `DefaultInputBindingCatalog`、`.inputactions` 或输入重绑定 override；UI/Rebinding/Cutscene 等独占 context 会阻断。Guard Aura 是 CharacterTest 专用测试接入：第 6 帧通过 `GameplayComponentAbilityCommandSystem` 创建 caster-following aura source，并刷新同队目标的 component-native Buff；Area Confinement 是 CharacterTest 专用固定区域 root 接入：第 6 帧创建 CharacterTest internal mechanic source，经 `CombatPhysicsWorld.QuerySphere` 对 hostile 目标刷新 rooted Buff/Status，并由 mechanic motion policy 只锁主动移动。Beam Lance 是 CharacterTest 专用一帧 44m 穿透 sweep，绑定后从所选槽释放，accepted hostile 命中会刷新 90 frame `STOP` Stasis / 凝滞：自身动作时间轴、主动移动、转向和动画暂停，但外力、碰撞、击退和 Tornado 吸附仍生效。Laser Lance 是 CharacterTest 专用点到点单体 44m sweep，命中首个 accepted hostile 后阻断，不附加 Stasis，并用路径 VFX 从 muzzle 拉到命中点或射程终点。Chain Lightning 是 CharacterTest 专用同帧链式命中：frame 30 锁定优先选择首目标，之后每段从上一目标向 8m 内最近未命中过的 hostile 跳跃，最多 4 个目标，伤害 6/5/4/3，不附加 STOP。Sword Wave 是 CharacterTest 专用中距离剑气斩：frame 13 发出 30m Linear crescent projectile，最多命中 4 个 hostile，撞到墙/静态场景会阻断并播放 impact 表现，不附加 Buff/Status。Dash Strike 是 CharacterTest 专用冲锋斩：frame 6..19 通过动作 root motion 每帧本地前向 0.3m，`rootMotion.obstaclePolicy=StaticOnly`，frame 6..20 开启 `StartHitTrace.hitVolume.sourceKind=CasterBody` 身体撞击窗口，命中范围由施法者 runtime physics body 派生并按上一帧到当前帧 sweep，`OncePerEvent` 命中 hostile 目标，静态墙/场景阻挡仍由角色 kinematic motion 处理，不创建 projectile。Hook Chain 是 CharacterTest 专用 projectile hit -> tether pull：frame 12 发射 12m hook projectile，首个 accepted hostile hit 后创建确定性目标拉拽。Arc Mine 是 CharacterTest 专用 projectile carrier -> drop projectile -> fuse mine：frame 12 抛物线投掷 `character_test.arc_mine.projectile`，锁定 8m 内 hostile 时落向目标脚下，否则落向前方 5m；carrier 命中 hostile 只生成 `character_test.arc_mine.drop_projectile` 继续下落，carrier/drop 落地、撞墙或过期后才部署 mine；hostile 进入 1.25m trigger 后启动 60f 引信，引信结束才用 2.25m blast sphere 走 `HitResolveSystem`，飞行和下落都不造成伤害；表现使用独立 `vfx.magic.arc_mine.flight`、`vfx.magic.arc_mine.deploy`、`vfx.magic.arc_mine.fuse`、`vfx.magic.arc_mine.trigger` 符文电弧资源，分别读取 projectile flight、落地部署、引信开始和爆炸事件。Rune Turret 是 CharacterTest 专用 deployable persistent turret -> auto projectile source：frame 12 抛物线投放 `character_test.rune_turret.deploy_projectile` 到锁定 8m 内 hostile 脚下或前方 5m fallback，落地/静态阻挡/过期后创建炮台；炮台 18f warmup 后最多存在 360f，按 12f scan interval 搜索 10m 内最近 hostile，并按 45f fire interval 发射最多 8 发 `character_test.rune_turret.shot_projectile`，每发仍由 `Combat.Projectiles` + `HitResolveSystem` 结算 `damage.rune_turret_shot`；VFX 使用 `vfx.magic.rune_turret.deploy/idle/fire/flight/impact/expire`，只读 runtime 事件。表现层播放 `vfx.magic.guard_aura` 跟随施法者，并在 Area Confinement source 创建成功时通过 source create effect 播放 `vfx.magic.area_confinement.zone` 180 frame 地面法阵/束缚柱；每个处于 `AreaConfined` status 的角色会附加 `vfx.magic.area_confinement.character` 束缚环直到 status 消失。Combat HUD 还会从 Buff presentation snapshot 显示本地玩家和锁定目标的小图标条，包含 Buff/Debuff/Aura 色系、剩余时间遮罩、叠层角标和悬停摘要；角色 head pad 显示最多 4 个高优先级小状态徽章，例如 `DEF+` / `DEF-` / `ROOT` / `STOP`，只作为战斗画面精简提示，不替代大 HUD 的完整 Buff 列表。
→ CharacterTest 本地多人验证：项目已接入 Unity `com.unity.multiplayer.playmode@1.6.3`。优先使用 `Window > Multiplayer > Multiplayer Play Mode` 启用至少 1 个 Virtual Player，并用 `Window > Multiplayer > Play Mode Status` 查看实例状态；Main Editor Player 作为 LAN Room Host，Virtual Player 通过 NetworkPanel LAN Room Join `127.0.0.1`；双方 Ready 后只有 Host 点击 Enter Game，随后各实例在本地 gameplay handshake 验通后自动关闭面板并恢复 gameplay tick。Direct LAN 仍可用作连接诊断，Address 接受 `host:port` 并用地址中的端口覆盖 Port 字段；也支持反向 Player2 Host / Default Join 手测。如果 MPPM 当前环境不可用，Standalone 双实例仍是 Host/Join fallback。

→ CharacterTest 启动体验：普通 Play 默认只显示 Pause Menu，`StoryDialog` 在 MainMenu、NetworkPanel 或 SettingsPanel 打开期间被抑制，关闭菜单后再按当前 Story 状态显示。Debug UI 默认 `Hidden` 且不自启。MainMenu、StoryDialog、NetworkPanel、SettingsPanel 打开时会进入 exclusive `InputContext.UI`，屏蔽 Gameplay / Debug 等非 UI action map，并通过 `MxCursorController` push released cursor scope；关闭最后一个玩家 modal 后恢复 Debug / camera cursor policy。
→ **不含**: FishNet/NGO/Relay/WebSocket/Steam Lobby/matchmaking、房间列表 / discovery、核心重传、binary codec、snapshot 压缩、prediction 视觉误差平滑、长时 replay UI、带 token / 房间 / 角色恢复的生产级断线重连、资源分发协议。

### 1.6 App / Scene Flow — 游戏启动状态和场景切换骨架

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| App 状态注册和启动 | ✅ v0.1 | `AppFlowController.RegisterState()` / `Start()` | Runtime |
| Pending 状态切换 | ✅ v0.1 | `AppFlowController.RequestTransition()` / `IAppFlowState` | Runtime |
| AppFlow 诊断快照 | ✅ v0.1 | `AppFlowSnapshot` | Runtime |
| Scene 加载请求和 busy 拒绝 | ✅ v0.1 | `SceneFlowController.RequestLoad()` / `SceneFlowResult` | Runtime |
| SceneFlow 进度和结果快照 | ✅ v0.1 | `SceneFlowSnapshot` / `SceneFlowErrorCode` | Runtime |
| RuntimeHost 模块入口 | ✅ v0.1 | `AppFlowRuntimeModule` / `SceneFlowRuntimeModule` | Runtime |
| Unity SceneManager 适配 | ✅ v0.1 | `UnitySceneFlowDriver` | Runtime.Unity |

→ 接口：`Interfaces/AppFlow.md`
→ 测试：`Tests/Runtime/AppFlowTests.cs`, `Tests/Runtime/SceneFlowTests.cs`
→ **不含**: Loading UI、Addressables 场景加载、项目级 scene manifest、联网 session、WGame 关卡规则

### 1.7 Input — 输入意图、上下文和重绑定

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| Unity Input System 采集适配 | ✅ v0.1 | `InputService` / `InputActionAsset` | Input |
| 每帧输入快照 | ✅ v0.1 | `IInputProvider.Snapshot` / `InputSnapshot` | Input |
| 瞬时意图命令队列 | ✅ v0.1 | `InputCommand` / `InputCommandQueue` | Input |
| Gameplay/UI 等上下文栈 | ✅ v0.1 | `InputContextStack` / `InputContextPolicy` | Input |
| Unity cursor scope 管理 | ✅ v0.2 | `IMxCursorController` / `MxCursorController` / `UnityCursorDriver` | Input |
| 运行时重绑定和保存 | ✅ v0.1 | `IInputRebindingService` / `InputRebindingService` | Input |
| 本地多人 PlayerInput 适配 | ✅ v0.1 | `LocalUserInputAdapter` | Input |
| 测试/回放输入源 | ✅ v0.1 | `FakeInputProvider` / `RecordedInputProvider` | Input |

→ 接口：`Interfaces/Input.md`
→ 默认配置：`Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions`
- Current input defaults are documented through `DefaultInputBindingCatalog` plus runtime fallback data and `.inputactions`: Gameplay `Pause` maps to `Escape`, Gameplay `SkillSlot1..9` maps to number keys `1..9`, Debug maps to `F1`-`F8`, and CharacterTest local runtime actions use `F/H`. In Unity Editor Play mode, CharacterTest also keeps runtime action clones such as `P` for local testing. User rebinding overrides are saved per player; CharacterTest 1-9 abilities are demo-level adapters in the Gameplay context and submit `CastAbilitySlot` instead of becoming UI rebinding actions.
→ 测试：`Tests/Input/InputCoreTests.cs`
→ **不含**: 项目专属按键布局、UI 提示美术、联网预测和录制文件格式

### 1.7.1 Settings — 用户偏好 profile、输入设置入口、音频 Bus 和画质应用

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine settings profile 和 JSON store | ✅ v0.1 | `MxSettingsProfile` / `MxSettingsJsonFileStore` | Settings |
| 结构化 load/save/apply 诊断 | ✅ v0.1 | `MxSettingsIssue` / `MxSettingsLoadResult` / `MxSettingsApplyResult` | Settings |
| 音频 Bus 音量/静音应用 | ✅ v0.1 | `MxAudioSettingsApplier` -> `IAudioService.SetBusVolume()` / `SetBusMuted()` | Settings.Audio + Audio |
| 输入重绑定设置控制器 | ✅ v0.1 | `MxInputRebindingSettingsController` / `MxInputBindingTarget` | Settings.Input + Input |
| 画质 profile 与玩家可见白名单 | ✅ v0.2 | `MxGraphicsSettings` / `MxGraphicsSettingsCatalog` | Settings |
| Unity 画质运行时应用 | ✅ v0.3 | `MxUnityGraphicsSettingsApplier` / `MxUnityGraphicsSettingsApplyOptions` / `IMxGraphicsRuntime` | Settings.Graphics + Unity/URP |
| 体积雾质量设置 bridge | ✅ v0.2 | `MxVolumetricFogSettingsApplier` -> `MxVolumetricFogPassProvider.SetSettings()` | Settings.Graphics + Rendering |
| CharacterTest 代码层 settings host | ✅ v0.3 | `CharacterTestSettingsRuntime.ApplyCurrentProfile()` / `SaveCurrentProfile()` / `ReloadProfile()` | Game.CharacterTest |
| 设置页 UI model / presenter | ✅ v0.4 | `MxSettingsUiPresenter` / `MxSettingsUiViewModel` | Settings.UI |
| 本地语言偏好 | ✅ v0.6 | `MxLocalizationSettings` / `settings.json` `localization.localeId` / default `zh-CN` | Settings |
| CharacterTest FairyGUI 设置面板 | ✅ v0.6 | `CharacterTestFairyGuiSettingsPanelHost` / generated `CharacterTestFairyGuiSettingsPanelManifest.g.cs` + `CharacterTestFairyGuiSettingsPanelBinder.g.cs` / `MxCharacterTestUi/MainMenuPanel` Settings button / Composer-owned `MxCharacterTestSettingsUi/SettingsPanel` / Language row；作为 FairyGUI generated binder canonical sample | Game.CharacterTest + UI.FairyGUI + Settings.UI |

→ 接口：`Interfaces/Settings.md`
→ 测试：`Tests/Settings/SettingsFoundationTests.cs`
→ **边界**: Settings 不保存 gameplay 权威状态，不进入 Runtime SaveState、Replay、Runtime hash 或网络同步；`localization.localeId` 是本地用户偏好；输入 override 仍由 `InputRebindingService` 保存到 `PlayerPrefs`；画质设置只做运行时代码应用，不写 URP/Quality/Scene YAML 资产；CharacterTest 不自动应用 profile，且暂不让 settings 接管 frame cap；FairyGUI 设置面板是 CharacterTest 的第一版玩家可见入口，默认隐藏并由暂停菜单的 Settings 按钮按需打开，绑定 `MxSettingsUiPresenter` 和宿主 Apply/Save/Reload。面板视觉来自 `MxUiFoundation` 语义资源、紧凑按钮配方和 Settings Screen Blueprint，并由 UI Composer/helper 发布到独立 `MxCharacterTestSettingsUi` 包；它仍不作为全项目通用设置面板。

### 1.8 Character Control — 角色控制编排

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 统一角色命令 DTO | ✅ v0.1 | `CharacterCommand` / `ICharacterCommandSource` / `CharacterActionRequest` | CharacterControl |
| 控制状态机 | ✅ v0.1 | `CharacterControlStateMachine` / `CharacterControlLockMask` / `CharacterStateChangedEvent` | CharacterControl |
| Combat Motion resolver | ✅ v0.1 | `CharacterMotionResolver` / `CharacterMotionSettings` / `CharacterMotionResult` | CharacterControl + Combat |
| Combat / Gameplay action bridge | ✅ v0.1 | `CharacterActionController` / `ICharacterActionConstraint` / `CharacterActionEvent` | CharacterControl + Runtime + Gameplay + Combat |
| CharacterAction 单一执行桥 | ✅ v0.7 | `CharacterActionControllerOptions` / `CharacterActionRequest.ActionIntentId` / `CharacterActionController.TickCharacterAction()` / `CharacterActionMotionRequestAccumulatorSink`；Request Coordinator 解析并调用唯一 Gateway，Dispatch Coordinator 派发 Motion、root motion、Combat、Gameplay、Animation、Presentation 和 Debug track；CombatAction 仅由显式 track bridge 作为子时间线运行 | CharacterControl + CharacterAction + Combat |
| Gameplay pressure reaction bridge | ✅ v0.3 | `CharacterPressureReactionController` / `CharacterPressureReactionPolicy` / `CharacterPressureReactionEvent` | CharacterControl + Gameplay + Combat |
| MxAnimation presentation adapter | ✅ v0.4 | `CharacterAnimationPresentationController` / `CharacterAnimationPresentationOptions` / `CharacterAnimationPresentationDiagnosticSnapshot` | CharacterControl.Animation + Animation |
| Debug UI source | ✅ v0.5 | `CharacterControlDebugSource` | DebugUI.Adapters + CharacterControl |
| Local Input command source | ✅ v0.2 | `InputCharacterCommandSource` / `CharacterInputActionBinding` | CharacterControl.Input + Input |
| Runtime AI Planner command source | ✅ v0.2 | `RuntimeAiPlannerCharacterCommandSource` / `RuntimeAiCharacterCommandProfile` | CharacterControl.RuntimeAiPlannerBridge + AI |
| Motion modifier / traction contract | ✅ v0.2 | `ICharacterMotionModifierProvider` / `CharacterMotionModifierAggregator` | CharacterControl + Combat |
| Playable vertical slice | ✅ v0.6.50 | `RuntimeCombatCharacterControlSlice` / `RuntimeCombatShowcaseRunner.StepCharacterControlFromInput()` / `RunRuntimeAiPlannerCommand()` | Demo + CharacterControl + Input + Runtime + Combat + DebugUI |

→ 接口：`Interfaces/CharacterControl.md`
→ 设计：`Tasks/CHARACTER_CONTROL_RUNTIME_00_DESIGN_CONTRACT.md`
→ 测试：`Tests/CharacterControl/`、`Tests/Combat/RuntimeCombatShowcaseRunnerTests.cs`
→ **边界**: Character Control 不读取 Unity 输入、不调用 Unity Physics、不写 Gameplay HP/Buff/Ability source of truth，也不让 MxAnimation / Animator / Playables root motion 反向驱动权威状态。

### 1.8.1 Character Action — 可配置角色动作层

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 动作意图请求 | ✅ 核心完成，CharacterTest 已接入 | `CharacterActionIntentRequest` | CharacterAction + Runtime |
| 动作解析（候选、优先级、tag 需求） | ✅ 核心完成，CharacterTest 已接入 | `CharacterActionResolver` / `CharacterActionCandidate` | CharacterAction |
| 结构化命令连段 | ✅ v0.3，CharacterTest 普攻已接入 | `CharacterActionCommandSequence`；按当前活动动作选择 1～N 段、可配置末段循环，切换窗口/排队仍由 Gateway 执行 | CharacterAction + CharacterTest |
| 动作计划生成 | ✅ 核心完成，CharacterTest 已接入 | `CharacterActionPlan` | CharacterAction |
| 动作执行权威（phase/frame/track dispatch） | ✅ 核心完成，CharacterTest 与两个 Runtime 示例已接入 | `CharacterActionExecutionGateway` / `CharacterActionExecutionState` / `CharacterActionInstance`；Runner 为 internal | CharacterAction |
| 单一动作执行网关 | ✅ 完成，第一方入口已收口 | `CharacterActionExecutionGateway` / `CharacterActionExecutionRequest` / `CharacterActionExecutionResult` | CharacterAction + CharacterControl + CharacterTest |
| Motion/Combat/Gameplay 轨道适配 | ✅ 核心完成，CharacterTest 已接入 | `CharacterActionTrackAdapter` / sink 接口 | CharacterAction + Combat + Gameplay |
| Animation/Presentation 轨道适配 | ✅ 核心完成，CharacterTest 已接入 | `CharacterActionPresentationTrackAdapter` | CharacterAction + Animation |
| 取消/打断规则 | ✅ 核心完成，CharacterTest 已接入 | `CharacterCancelConflictClassifier` / `CharacterInterruptRule` | CharacterAction |
| 资源依赖收集 | ✅ 核心完成，Authoring 使用 | `CharacterActionResourceDependencyCollector` | CharacterAction |
| 反应上下文（受击→动作选择） | ✅ 通用 reaction bridge v0 已完成，CharacterTest 已迁移示范 | `CharacterReactionContextBuilder` / `CharacterReactionSelector` / `CharacterActionReactionRuntime` / `CharacterActionReactionBridge` | CharacterAction + CharacterControl |
| 动作诊断和工作站 | ✅ 核心完成，Authoring / 诊断使用 | `CharacterActionWorkstation` / `CharacterActionDiagnostics` | CharacterAction |
| 配置定义 | ✅ 核心完成，JSON-backed CharacterTest 已接入 | `CharacterActionConfig` / `CharacterActionSetConfig` / `CharacterMovementProfileConfig` | CharacterAction |
| 配置校验 | ✅ 核心完成，导入与 reload 使用 | `CharacterActionValidation` / `CharacterActionPhaseValidator` | CharacterAction |
| 正式动作源与逐槽编辑 | ✅ v0.3 | Common/角色 `character_actions.json` 为正文权威；CharacterStudio 分组选择动作槽，Unity 精确只读/可写打开并以源 hash 防覆盖 | Authoring + CharacterStudio + CharacterAction.Editor |
| 实体级 ActionSet 隔离 | ✅ v0.3 | CharacterTest 按实体 `characterStableId + equipmentStateStableId` 缓存并解析 ActionSet；导入角色缺配置时显式失败，不回退 Codex Flow | Game.CharacterTest + CharacterAction |

→ 接口：`Docs/Interfaces/CharacterAction.md`
→ 代码：`Packages/com.vincent.wgameframework/CharacterAction/`
→ 测试：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterAction/`（17 个测试文件）
→ **状态说明**: Phase 1-8 代码已合并；`MxFramework.Game.CharacterTest`、Editor、测试程序集、`MxFramework.CharacterControl` 和两个 Runtime 示例已引用 `MxFramework.CharacterAction`。CharacterTest Runtime Slice 发现全部生成 Profile，并按每个实体的角色与 EquipmentState 精确解析 ActionSet；普通攻击只提交统一 `LightAttack`，其具体三段由配置的 `CharacterActionCommandSequence` 决定，不再使用写死的动作名判断。Profile `<package>.default`、ActionSet `<package>.<weaponCombination>` 和运行库由 Common/角色正式动作源确定性编译；动作源变化参与 import source hash。启动、切换、反应、外部完成、生命周期替换/终止、Tick 和恢复统一由有状态 `CharacterActionExecutionGateway` 执行，Runner 及原始 operation API 已改为 internal。普通请求遵守切换窗口，普通反应遵守 `interruptRules`；空规则不可打断，只有明确生命周期可以强制终止。CharacterControl 以 Request / Dispatch Coordinator 拆分职责，CombatAction 与 Gameplay 只通过下游 track bridge 执行。Runtime AI Planner 行为卡高优先级抢占直接请求目标 CharacterAction，目标实例真实出现后才提交卡切换；窗口拒绝时保留原卡、原动作和本地帧。通用 reaction 链路由单一预热路径 `CharacterActionReactionRuntime.TryStart(...)`、`CharacterActionController.SubmitReaction(...)` 和 `CharacterActionReactionBridge` 提供。CharacterTest 动作命令及 Replay 为 schema v2，Replay v1 在播放前明确拒绝。
→ **仍不含**: CharacterControl 完整 v0 替换、其他 Demo / 项目组合根默认接入、旧直接 Combat path 退场策略、独立 Playable 角色动作 Demo

### 1.9 Camera — 运行时表现相机

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine profile / request / target group 求值 | ✅ v0.1 | `MxCameraService.Evaluate()` / `MxCameraEvaluationResult` | Camera |
| 单目标 / 多目标透视与正交入镜 | ✅ v0.1 | `MxCameraTargetSnapshot` / `MxCameraTargetGroupState` | Camera |
| target lost grace / fallback / request conflict 诊断 | ✅ v0.1 | `MxCameraDiagnosticCodes` | Camera |
| shake / impulse clamp | ✅ v0.1 | `MxCameraRequestKind.Shake` / `Impulse` | Camera |
| Unity Camera LateUpdate apply adapter | ✅ v0.1 | `MxCameraUnityRig.ApplyLate()` | Camera.Unity |
| Unity Camera procedural shake presenter | ✅ v0.1 | `IMxCameraUnityShakePresenter` / `MxCameraUnityShakeMode.Procedural` | Camera.Unity + CharacterTest |
| Unity Camera obstruction resolver option | ✅ v0.1 | `IMxCameraUnityObstructionResolver` / `MxCameraUnityObstructionMode.FrameworkPhysicsAabb` | Camera.Unity + CharacterTest |
| Transform / Renderer bounds target binder | ✅ v0.1 | `MxCameraTransformTargetBinder` / `MxCameraRendererBoundsTargetBinder` | Camera.Unity |
| Animation presentation event bridge | ✅ v0.1 | `MxCameraPresentationEventSink` | Camera.Animation |
| Debug UI snapshot | ✅ v0.1 | `CameraDebugSource` | DebugUI.Adapters |
| Profile authoring adapter | ✅ v0.1 | `MxCameraProfileAuthoringAsset` / `MxFramework/Camera/Create Profile Asset` | Camera.Unity + Camera.Editor |

→ 接口：`Interfaces/Camera.md`
→ 测试：`Tests/Camera/`
→ Demo 接入：`RuntimeCombatShowcaseInputController` 通过 `MxCameraUnityRig` 收敛核心 follow/orbit，并用 `MxCameraFacingBasisResolver` 给 Character Control 组合根提供 camera-facing basis。
→ **边界**: Camera 是表现层，不进入 Gameplay / Combat authority、Runtime result hash、Replay hash 或 SaveState 默认内容；Unity Camera / Transform 只存在于 adapter 层。

---

## 2. 配置系统：用配置数据驱动运行时

### 2.1 配置存取

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 注册/读取配置表 | ✅ v1 | `IConfigProvider` / `IConfigRegistry` / `MemoryConfigProvider` | Config |
| 表结构定义 | ✅ v1 | `ConfigSchema` / `ConfigField` / `ConfigFieldType` | Config |
| 跨表引用校验 | ✅ v1 | `IConfigReferenceProvider` / `ConfigReferenceRule` / `ConfigValidator` | Config |
| ID 范围校验 | ✅ v1 | `ConfigIdRange` | Config |
| 多语言 key 定义 | ✅ v1 | `LocalizedTextKey` / `LocaleId` / `ILocalizationProvider` | Config |
| Localization TSV 文本源 | ✅ v0.1 | `LocalizationTsvLoader.LoadFromText()` / `MemoryLocalizationProvider` / structured issues | Config |

### 2.2 配置驱动运行时

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 从配置创建 Buff | retired | Buff v2 definitions are registered in Gameplay, not through `ConfigBuffFactory` | Gameplay |
| 从配置创建 Modifier | ✅ v1 | `ConfigModifierFactory<TConfig>` / `ConfiguredModifier` | Config.Runtime |
| 从配置创建 Ability | ✅ v0.3 | `BasicAbilityConfig` / `AbilityEffectParameters` / `ConfigAbilityFactory` -> `SimpleAbility` | Config.Runtime + Gameplay |
| Ability Graph 配置映射 | ✅ v0.1 | `AbilityGraphConfig` / `AbilityGraphConfigMapper` -> `AbilityGraphDefinition` | Config.Runtime + Gameplay |
| Ability Authoring Contract | ✅ v0 | `AbilityAuthoringContract` / `AbilityAuthoringContractValidator` / `AbilityAuthoringContractMapper` / `AbilityAuthoringSchema` | Config.Runtime |
| Runtime 配置变更处理 | ✅ v0 | `RuntimeAbilityConfigResolver` / `RuntimeConfigChangeSummary`，Ability 重建、Buff/Modifier 不回溯 | Config.Runtime + Gameplay |
| JSON Patch 文件加载 + 合并 | ✅ v1 | `RuntimeConfigPatchJsonLoader` / `RuntimeConfigPatchMerger` | Config.Runtime |
| 运行时变更集展示 | ✅ v1 | `ConfigChangeSet` | Config.Runtime |

→ Demo Patch 文件：`StreamingAssets/MxFramework/Demo/runtime_config_patch.json`
→ Demo 场景：`RuntimeVerticalSlice.unity` — `_usePatchFile = true`

---

## 3. Runtime AI Planner（运行时 AI 规划系统）

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 世界状态定义 | ✅ v1 | `IAiWorldState` | AI |
| 分层黑板 | ✅ v2 | `AiBlackboard` / `SharedAiBlackboard` / `AiBlackboardFactChangedEvent` | AI |
| 顺序规划器 | ✅ v1 | `SequentialPlanner` | AI |
| Utility 单步规划器 | ✅ v2 | `AiUtilityPlanner` / `IAiUtilityConsideration` | AI |
| 优先级目标选择 | ✅ v1 | `PriorityGoalSelector` | AI |
| 动作/条件/效果/传感器 | ✅ v1 | `IAiAction` / `IAiCondition` / `IAiEffect` / `IAiSensor` | AI |
| 传感器调度 | ✅ v2 | `AiSensorManager` / `AiSensorManagerDiagnostics` | AI |
| 动作执行器和 Agent brain | ✅ v2 | `AiSequenceActionRunner` / `AiAgentBrain` | AI |
| 代码级 HFSM primitive | ✅ v2 | `AiHfsmStateMachine` / `AiHfsmPlannerAdapter` | AI |
| Agent 根接口 | ✅ v1 | `IAiAgent` / `IAiGoal` | AI |
| CharacterTest 适配层 | ✅ Runtime Slice / JSON v3 behavior library / character profile override / local obstacle avoidance slice / behavior loadout foundation / player-facing presentation foundation / action-chain preview foundation | `CharacterTestRuntimeAiPlannerCommandModule` / `CharacterTestRuntimeAiPlannerProfileJson` / `CharacterTestRuntimeAiPlannerProfilePicker` / `CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime` / `CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationCatalog` / `CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder` / `CharacterTestCombatPhysicsNavigationService` | Game.CharacterTest + AI + Combat Physics |

→ 测试：`Tests/AI/` 目录
→ **不含**: 可视化行为树编辑器、生产级 GOAP authoring/editor、Unity NavMesh 集成、WGame 专用行为包

→ CharacterTest Runtime AI Planner 行为 Loadout 基础：CharacterTest 内部支持 `BehaviorDefinition -> BehaviorGroup -> BehaviorLoadout -> BehaviorSelection` 分层语义，session build 低频把已发布 loadout/variant 通过 behavior group 编译为现有 v3 profile/evaluator slot；`SetRuntimeAiBehaviorSelection` command 只在 selection phase 解锁时接受稳定 `loadoutId + variantId`，非法 id、未授权、phase locked、未知 behavior group 或 catalog mismatch 会拒绝并保留旧 evaluator。Behavior selection 作为 gameplay-owned authority state 进入 CharacterTest network snapshot、rollback clone、state hash 和 authoritative replay bootstrap/command stream；snapshot 只保存 selection 摘要和 catalog hash，不保存 compiled evaluator、behavior group JSON、Editor 展示文本或 debug overlay。玩家可见行为已具备低频 presentation 基础，可生成策略卡语义、战斗短状态和 Ability / CharacterAction 动作链预览，不暴露 behavior id、facts、score/cost 或 profileSlot。CharacterTest 还提供 FairyGUI 全屏 `RuntimeAiBehaviorLoadoutPanel` 基础入口，用于在 selection phase 中选择 AI-controlled 单位和已发布策略并提交同一 runtime command；Play Monitor 只负责打开/关闭该面板和调试解锁 selection phase。首版不提供成长/装备解锁、不支持战斗中热切换、不提供玩家 HUD 战术状态显示，也不新增 `MxFramework.AI` 公共 API。

---

## 4. 资源管理系统

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| 稳定资源引用 | ✅ v0.5 | `ResourceKey` / `ResourceTypeIds` | Resources |
| Catalog 合并和覆盖 | ✅ v0.5 | `ResourceCatalog` / `ResourceManager.AddCatalog()` / `allowOverride` | Resources |
| Memory Provider | ✅ v0.5 | `MemoryResourceProvider` | Resources |
| Unity Resources Provider | ✅ v0.5 | `ResourcesProvider` | Resources.Unity |
| AssetBundle Provider | ✅ v0.5 | `AssetBundleProvider` / `IAssetBundleDependencyProvider` | Resources.Unity |
| Streaming Catalog Loader | ✅ v0.5 | `StreamingResourceCatalogLoader` | Resources.Unity |
| Mod Package 资源 Catalog | ✅ v0.5 | `mod.json.resourceCatalog` / `ResourceCatalogFilePath` | Config.Runtime + Resources.Unity |
| 资源诊断快照 | ✅ v0.5 | `ResourceDebugSource` / `ResourceDebugSnapshot` | Resources + Diagnostics |
| Catalog 校验 | ✅ v0.5 | `ResourceCatalogValidator` / `ResourceCatalogEditorValidator` | Resources + Editor |
| Sample Catalog Builder | ✅ v0.6.5 | `SampleResourceCatalogBuilder` / `MxFramework/Samples/Generate Resource Catalog` | Editor + Resources |
| Sample Player AssetBundle Catalog | ✅ v0.6.6 | `SamplePlayerResourceCatalogBuilder` / `MxFramework/Samples/Build Player Resource Catalog` | Editor + Resources.Unity |
| Global Resource Mapping Profile v2 | ✅ production | `global_resource_mapping_profile.v2.json` / `GlobalResourceMappingProfileAggregator` / `GlobalResourceBuildPlanCompiler` | Authoring.Core + Editor |
| Resource Manager Build Profile 编辑 | ✅ v0.7.0 | Bundle-first：定义 Bundle / 加入 Bundle / 移出 Bundle / 保存 Profile | Tools/MxFramework.ResourceLibrary |
| Resource Manager 引用审查 | ✅ v0.7.2 | Refs：`referenceGraph` / inspect references / Profile draft / Bundle 建议草稿 | Tools/MxFramework.ResourceLibrary |
| Resource Manager 关系图 | ✅ v0.7.2 | Graph：只读 SVG，`引用者配置 -> 资源 -> Bundle / delivery`，修复入口回 Refs | Tools/MxFramework.ResourceLibrary |
| Bundle Plan 预览 | ✅ production | `/api/authoring/resource-mapping/plan` / 与 Unity Builder 消费同一 compiled plan | Authoring.Cli + Authoring.Core |
| Global Player Resource Catalog Build | ✅ production | `MxFramework/Resources/Validate Global Resource Mapping` / `Build Global Player Resources` | Editor + Resources.Unity |
| Global AssetBundle Builder Workbench | ✅ v0.7.1 | `MxFramework/Resources/Open Global AssetBundle Builder` / `GlobalAssetBundleBuilderWorkbench` | Editor + Resources.Unity |
| Runtime Demo Resource Binding | ✅ v0.6.7 | `RuntimeVerticalSliceRunner.ResourceWarmupSummary` / resource binding diagnostics | Demo + Resources |
| Preload Group / Scene Warmup | ✅ v0.6.8 | `ResourcePreloadService` / `ResourcePreloadPlan.MaxConcurrentLoads` / progress + cancellation | Resources |
| Candidate Resource Preparation Prerequisites | ✅ v0.1 | safe-drain preload / `ResourceRuntimeConfigurationIdentity` / owner-bound `ResourcePlanPreparation` / exact-once `AcquirePrepared` | Resources |
| Story preload plan bridge | ✅ v0.1 | `StoryResourcePreloadPlanBuilder` produces `ResourcePreloadPlan` without loading | Story.ResourcesBridge + Resources |
| Variant Catalog / Retain Policy | ✅ v0.6.2 | `ResourceVariantProfile` / `ResourceRetainPolicy` / retain diagnostics | Resources |
| Retain Budget / Eviction Policy | ✅ v0.6.9 | `ResourceRetainPolicy.Budgeted` / retained bytes diagnostics / budget eviction | Resources |
| Remote Bundle Provider | ✅ v0.6.3 | `RemoteBundleProvider` / `providerData.url` / SHA-256 cache validation | Resources.Unity |
| Runtime Samples Resource Chain | ✅ v0.6.4 | `RuntimeVerticalSliceSampleResourceTest` / `TempImportedResourceCatalog` / `ResourceKeyConfigProfile.CreateSample()` | Demo + Resources |
| Addressables Adapter | Deferred / Optional | 独立 `MxFramework.Resources.Addressables`，仅在项目已采用 Addressables 时实现 | 不进入默认依赖 |
| YooAsset Adapter | 不做 | YooAsset 不是 MxFramework 默认资源路线，本仓库不提供 adapter | 项目层自行决策 |

→ 接口：`Interfaces/Resources.md`
→ 设计：`RESOURCE_MANAGEMENT_SYSTEM.md`
→ 测试：`Tests/Resources/`、`Tests/Config/ModPackageCatalogTests.cs`
→ 当前全局资源构建已覆盖 Profile 加入/保存、Refs 引用审查、Graph 只读关系图、Bundle Plan 预览、Global AssetBundle Builder 工作台、Unity 菜单校验和生成本地 Player catalog / preload groups / bundle dependencies / build report。
→ **不含**: 增量/差分热更、CDN 发布、签名/加密、断点续传、YooAsset 接入或 Addressables 默认依赖。

---

## 5. MxAnimation 表现动画系统

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine 动画表现契约 | ✅ v0.1 | `MxAnimationSetDefinition` / `MxAnimationActionBinding` / `IMxAnimationBackend` | Animation |
| Unity Playables Backend | ✅ v0.1 | `UnityPlayablesAnimationBackend` | Animation.Unity |
| Combat 表现桥 | ✅ v0.1 | `CombatMxAnimationUnityBridge` / `ICombatMxAnimationPresentationEventSink` | Combat.Animation.Unity |
| Character Control 表现桥 | ✅ v0.4 | `CharacterAnimationPresentationController` / locomotion blend / reaction binding diagnostics | CharacterControl.Animation + Animation |
| Clip Registry + Mapping | ✅ v0.2 | `MxAnimationClipRegistry` / `IMxAnimationMappingProvider` / `MxAnimationClipRegistryExporter` | Animation + Editor |
| Presentation Sync Contract | ✅ v0.2 | `MxAnimationPresentationSyncState` / `MxAnimationPresentationEventDedupeKey` | Animation |
| Layer Weight + AvatarMask | ✅ v0.2 | `MxAnimationLayerDefinition` / `MxAnimationLayerWeightRequest` / `ResourceTypeIds.AvatarMask` | Animation + Animation.Unity |
| Warmup + Resource Version Validation | ✅ v0.2 | `MxAnimationWarmupService` / `MxAnimationWarmupDefinition` / `MxAnimationWarmupIssue` | Animation + Resources |
| 1D Locomotion Blend Demo | ✅ v0.2 | `MxAnimationBlend1DDefinition` / `MxAnimationBlend1DRequest` / `MxAnimationBlend1DCalculator` | Animation + Animation.Unity + Demo |
| Bake MVP | ✅ v0.2 | `MxAnimationBakeArtifact` / `MxAnimationBakeArtifactValidator` / `CombatBakedWeaponTraceAdapter` | Animation + Editor + Combat |
| Provider-switchable Animation Package Loading | ✅ v0.3 | `MxAnimationPackageExpectation` / `MxAnimationPackageCatalogValidator` / `MxAnimationResourceTypeIds` | Animation + Resources |
| Mod Animation Package Override | ✅ v0.3 | `MxAnimationModOverrideDefinition` / `MxAnimationModOverrideMerger` / `MxAnimationModOverrideMergeResult` | Animation + Resources |
| MxAnimation System Showcase | ✅ playable validation | `MxAnimationShowcaseDemoBootstrap` / `MxAnimationSystemShowcase.unity` / `MxAnimationShowcaseDemoTests` | Demo + Animation.Unity |

→ 接口：`Interfaces/Animation.md`
→ 手测：`Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md`
→ 测试：`Tests/Animation/`
→ **边界**: MxAnimation 只负责表现状态和资源加载诊断；不进入 Combat authority、Replay hash、命中、取消、伤害或权威移动。

---

## 6. Audio / FMOD 音频系统

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| noEngine 音频服务契约 | ✅ v0.1 | `IAudioService` / `AudioService` | Audio |
| 可替换音频后端 | ✅ v0.1 | `IAudioBackend` / `NullAudioBackend` | Audio |
| 稳定音频定义查询 | ✅ v0.1 | `IAudioDefinitionProvider` / `AudioEventDefinition` / `AudioBusDefinition` / `AudioParameterDefinition` | Audio |
| 播放请求、句柄和结构化结果 | ✅ v0.1 | `AudioPlayRequest` / `AudioHandle` / `AudioResult` / `AudioPlayResult` | Audio |
| 参数、Bus 音量和静音控制 | ✅ v0.1 | `SetParameter()` / `SetBusVolume()` / `SetBusMuted()` | Audio |
| 音频诊断快照 | ✅ v0.1 | `AudioDebugSnapshot` / `AudioDebugActiveEvent` / `AudioDebugBusState` | Audio |
| FMOD 后端运行时脚手架 | ✅ Runtime Scaffold | `FmodAudioBackend` / `FmodAudioBackendOptions` | Audio.FMOD |
| RuntimeHost 音频 Tick 模块 | ✅ v0.1 | `AudioRuntimeModule` / `RuntimeTickStage.PostSimulation` | Audio.FMOD + Runtime |
| FMOD 设置和 bank 校验工具 | ✅ v0.1 | `FmodAudioSetupValidator` / `FmodAudioSetupReport` | Audio.FMOD.Editor |
| Global Runtime Audio Catalog | ✅ v0.2 | `AudioDefinitionCatalog` / `IAudioCueKeyDefinitionProvider` / `mx.audioRuntimeCatalog.v1` | Audio + Audio.Config |
| Physical Contact one-shot | ✅ v0.1 | `PhysicalAudioCueRuleTable` / `PhysicalAudioPlaybackSystem` | Audio.Physical + Combat |
| Physical Interaction Basis resolver core | ✅ v0.1 | `PhysicalAudioInteractionFact` / `PhysicalAudioResolver` / `PhysicalAudioDefinitionCatalog` | Audio.Physical |
| Physical Runtime Catalog startup loader | ✅ v0.1 | `PhysicalAudioRuntimeCatalogJson` / `mx.audioPhysicalRuntimeCatalog.v1` | Audio.Physical.Config |
| Physical Audio authoring 与 Workbench MVP | ✅ v0.1 | `mx.audioPhysicalAuthoringManifest.v1` / compiler-validator / Basis 提升 / coverage / scenario resolve / 双 Catalog publish | Authoring + Audio Workbench |
| Physical Audio 真实事实与覆盖分析 | ✅ v0.1 | `PhysicalAudioLatestFactCapture` / Unity capture bridge / 单参数 sweep / 二维热图 / server resolve trace | Audio.Physical + Audio.FMOD.Editor + Audio Workbench |
| 受控 Physical Basis/Cue/Event 创建 | ✅ v0.1 | `/api/authoring/audio/physical/managed-event` / managed namespace / FMOD template script / Build Banks / mirror / Snapshot / rollback | Authoring + FMOD Studio + Audio.FMOD.Editor |
| CharacterTest Physical Basis one-shot bridge | ✅ v0.1 | 成对 Catalog 校验 / `CharacterTestPhysicalAudioFactBuilder` / Footstep、Landing、ObjectImpact、WeaponHit/Block、AirDisplacement resolver path / 整份 legacy fallback | CharacterTest + Audio.Physical |
| Physical Audio 发布门禁 | ✅ v0.1 | Semantic/Generation/Physical expected hash / 原生 Volume-Pitch / 非原生必需参数存在性 / Basis ID+runtime Basis ID+adoption set 审批绑定 / Bank adoption diagnostics；Physical hash 仅审计 | Authoring + Audio.FMOD.Editor |
| Audio Workbench semantic/preview/publish | ✅ v0.2 | Authoring Manifest v2 / FMOD Snapshot / Preview Bridge / Runtime Catalog publish | Authoring + Audio.FMOD.Editor |
| SFX 生成、快捷剪辑、版本采用和自动 Banks 应用 | ✅ v0.3 | Generation Recipe / Candidate Edit / Adoption Set / FMOD apply pipeline | Authoring + AudioGenerationHost |

→ 接口：`Interfaces/Audio.md`
→ 设计：`AUDIO_SYSTEM_FMOD.md`
→ 任务：`Tasks/AUDIO_FMOD_M1_CONTRACT.md`, `Tasks/AUDIO_FMOD_M2_BACKEND.md`, `Tasks/AUDIO_FMOD_M3_VALIDATION.md`
→ 测试：`Tests/Audio/`
→ **边界**: `MxFramework.Audio` 不依赖 `UnityEngine`、`UnityEditor`、`FMODUnity` 或 `FMOD.Studio`；FMOD 真实播放仍依赖项目提供 FMOD bank / event / bus 配置。
→ **Current/Planned**: 当前 checkout 已同步有效成对 Physical Catalog，CharacterTest 使用 `GeneratedLinked`；Footstep 保留 Step 触发语义并复用公共材质 Pair 的 Impact Basis。四个已有可听 sole seed 已迁入 ObjectImpact family，稳定 Cue key 仅兼容保留；批量生成的 0 ms Impact Basis 保持 Draft，不进入游戏。组合根已在角色 spawn 时从实体物理量缓存 Body/Foot，并缓存默认 katana Weapon profile；Air Displacement 已由 CharacterAction 挥击峰值、默认刀具 profile 与稳定 Air/Gas profile 驱动，但其既有 seed 当前仍为 0 ms 内容阻断；当前 seed 只发布 `500251 MxPhysicalVolume` / `500252 MxPhysicalPitch` 两条原生后端曲线。动态装备切换、部位、Collider/地面属性刷新，以及 LowPass/Brightness/Scrape 的 FMOD parameter + automation evidence 尚未完成。Scrape/Roll/持续挤压和 loop 生命周期属于 Phase 18。设计见 `AUDIO_PHYSICAL_EXPRESSION_DESIGN.md`。

---

## 7. Observability / Debug UI：统一开发观察入口

| 能力 | 状态 | 关键 API | 对应模块 |
|------|------|----------|----------|
| Debug source 注册 | ✅ v0.1 | `FrameworkDebugSourceRegistry.Register()` / `Unregister()` | DebugUI + Diagnostics |
| Snapshot 聚合和错误隔离 | ✅ v0.1 | `DebugUiSnapshotAggregator.Refresh()` / `DebugUiDashboardViewModel` | DebugUI |
| UI 可见性状态 | ✅ v0.1 | `DebugUiVisibility.Hidden / Collapsed / Expanded` | DebugUI |
| UI Toolkit overlay shell | ✅ v0.1 | `DebugUiOverlayController` / `DebugUiOverlayViewModelBinder` | DebugUI.Toolkit + UI.Toolkit |
| RuntimeHost adapter | ✅ v0.1 | `RuntimeHostDebugSource` | DebugUI.Adapters + Runtime |
| Gameplay / Combat adapters | ✅ v0.1 | `GameplayDiagnosticSnapshotDebugSource` / `GameplayComponentWorldDebugSource` / `CombatDebugSnapshotDebugSource` | DebugUI.Adapters |
| Character Control adapter | ✅ v0.1 | `CharacterControlDebugSource` | DebugUI.Adapters + CharacterControl |
| Runtime logging contract / Unity Console adapter | ✅ v0.1 | `IRuntimeLogger` / `NullRuntimeLogger` / `UnityRuntimeLogger` | Runtime + Runtime.Unity |
| Resources Debug UI 接入 | ✅ v0.1 | `ResourceDebugSource` | Resources |
| Event timeline view model | ✅ v0.2 | `DebugUiTimelineEntryViewModel` / `DebugUiTimelineViewModel` / `DebugUiTimelineFilter` | DebugUI |
| Gameplay / Combat timeline adapters | ✅ v0.2 | `GameplayRuntimeEventTimelineDebugSource` / `CombatTimelineDebugSource` | DebugUI.Adapters |
| Entity watch view model | ✅ v0.2 | `DebugUiEntityWatchEntryViewModel` / `DebugUiEntityWatchViewModel` | DebugUI |
| Gameplay component entity watch | ✅ v0.2 | `GameplayComponentWorldEntityWatchDebugSource` | DebugUI.Adapters + Gameplay |
| Performance counter snapshots | ✅ v0.1 | `FrameworkPerformanceCounterRecorder` / `FrameworkPerformanceCounterDebugSource` | Diagnostics |
| RuntimeHost / Gameplay / Combat counters | ✅ v0.1 | `RuntimeHostPerformanceCounterSource` / `GameplayDiagnosticPerformanceCounterSource` / `CombatDebugPerformanceCounterSource` | DebugUI.Adapters + Diagnostics |
| Simulation Harness reports | ✅ v0.1 | `FrameworkSimulationBatchRunner` / `FrameworkSimulationReportFormatter` / `FrameworkSimulationReportDebugSource` | Diagnostics |
| Config Runtime patch hot reload | ✅ v0.1 | `RuntimeConfigPatchHotReloadService` / `RuntimeConfigHotReloadResult` / `RuntimeConfigHotReloadPoller` | Config.Runtime |
| Config hot reload Debug UI source | ✅ v0.1 | `RuntimeConfigHotReloadDebugSource` | DebugUI.Adapters + Config.Runtime |
| Debug UI command gate | ✅ v0.1 | `DebugUiCommandGate` / `IDebugUiCommandProvider` / `DebugUiCommandGateDebugSource` | DebugUI |
| Debug UI input adapter | ✅ v0.1 | `DebugUiInputAdapter` / `DebugUiOverlayInputBridge` | DebugUI.Input + Input |
| Observability 调试指南 | ✅ v0.1 | `Docs/Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` | Docs |

→ 接口：`Interfaces/DebugUI.md`
→ 指南：`Guides/OBSERVABILITY_DEBUGGING_GUIDE.md`
→ 诊断接口：`Interfaces/Diagnostics.md`
→ 任务：`Tasks/PHASE13_OBSERVABILITY_AND_DEVELOPER_WORKFLOW.md`
→ 测试：`Tests/DebugUI/`
→ **边界**: Debug UI 默认只读；Debug UI 展开、折叠、tab、刷新暂停等表现状态不进入 Replay、SaveState 或 Runtime hash。

---

## 8. 运行时 Demo：打开 Unity 按 Play 就能看到效果

| 能力 | 状态 | 说明 |
|------|------|------|
| 硬编码属性+Buff+Modifier 闭环 | ✅ v1 | `RuntimeVerticalSliceRunner._useHardcoded = true` |
| 配置驱动闭环 | ✅ v1 | `_useConfigDriven = true`，从 Demo 配置创建对象 |
| Patch 文件驱动闭环 | ✅ v1 | `_usePatchFile = true`，加载 JSON Patch 覆盖配置 |
| 场景对象预览 | retired | Legacy Buff preview target assets were removed with the old preview runtime |
| 属性变化 / Buff 状态 / ChangeSet 显示 | ✅ v1 | 运行中实时显示 Attack、Buff 列表、变更集 |
| Gameplay Runtime Core | ✅ v0.2 | `MxFramework.Gameplay` 提供 `RuntimeEntity` / `SimpleAbility` / Target / Effect，`_useAbilitySlice = true` 自动挂载 Demo Runner |
| Gameplay World v0 | ✅ v0.1 | `GameplayWorld` / `RuntimeEntityRegistry` / stable tick / world snapshot |
| Gameplay Team / Tag / Status | ✅ v0.1 | `GameplayTeamRelations` / `GameplayTagSet` / `GameplayStatusSet` |
| Gameplay Targeting Service | ✅ v0.1 | `GameplayTargetQuery` / `GameplayTargetingService` / rejected reasons |
| Gameplay Ability Runtime Adapter | ✅ v0.1 | `GameplayAbilityRegistry` / `GameplayAbilityRuntimeService` / structured runtime result |
| Ability Definition Runtime State | ✅ v0.1 | `AbilityDefinitionRegistry.ComputeStableHash()` / `AbilityRuntimeState` / `AbilityRuntimeHashContributor` / `AbilityRuntimeSaveStateProvider` 支持 input-intent ability 冷却状态、hash 和 SaveState |
| Ability Runtime Graph Contract | ✅ v0.1 | `AbilityGraphDefinition` / `AbilityGraphNode` / `AbilityGraphValidator` |
| Ability Runtime Graph Executor | ✅ v0.1 | `AbilityGraphRuntimeExecutor` / `AbilityGraphExecutionContext` / `AbilityGraphExecutionResult` |
| Ability Graph Phase Timeline | ✅ v0.1 | `AbilityGraphTimelineDefinition` / `AbilityGraphTimelineScheduler` / `AbilityGraphTimelinePhaseGate` |
| Ability Graph Diagnostics / Trace | ✅ v0.1 | `AbilityGraphDiagnosticSnapshot` / `AbilityGraphExecutionTrace` |
| Ability Graph Runtime Hash | ✅ v0.1 | `AbilityGraphHashContributor` 接入 `IRuntimeHashContributor` |
| Gameplay Runtime Hash | ✅ v0.1 | `GameplayHashContributor` / `GameplayComponentWorldHashContributor` 接入 `IRuntimeHashContributor` |
| Gameplay Component SaveState | ✅ v0.1 | `GameplayComponentWorldSaveStateProvider` 通过 schema SaveState adapters 捕获 / 恢复 ComponentWorld |
| Gameplay Component State Systems | ✅ v0.1 | `GameplayLifecycleCleanupSystem` 在 Resolution phase 清理 `PendingDestroy` component entity |
| Gameplay Component Spawn Definitions | ✅ v0.1 | `GameplayComponentSpawnRegistry` / `GameplayComponentSpawnCommandSystem` 按显式 definition 创建带初始 components 的 entity |
| Gameplay Component Attribute Runtime | ✅ v0.1 | `GameplayAttributeSetComponent` / `GameplayAttributeCommandSystem` 提供 component-native 属性状态、事件、hash 和 SaveState |
| Gameplay Component Ability Command Bridge | ✅ v0.1 | `GameplayComponentAbilityCommandSystem` / `GameplayComponentAttributeDeltaAbility` 支持 component entity 执行 self attribute delta ability |
| Gameplay Component Ability Targeting | ✅ v0.1 | `GameplayComponentTargetingService` / `GameplayComponentAbilityRequestStore` 支持 generation-safe explicit target、team/lifecycle/tag/status filter |
| Gameplay Component Ability Rules | ✅ v0.1 | `GameplayComponentAbilityRuleSet` / `GameplayAbilityCooldownComponent` 支持 cooldown gate、attribute cost gate、hash 和 SaveState |
| Gameplay Component Runtime Vertical Slice | ✅ v0.1 | `RuntimeHost` 驱动 spawn、attribute、targeting、ability rules、cleanup、hash 和 SaveState 的最小闭环 |
| Gameplay Component Runtime Showcase | ✅ Runtime Slice | `GameplayComponentRuntimeShowcase` / `GameplayComponentRuntimeShowcaseRunner` 展示 spawn、target、ability rules、cleanup、events、hash 和 SaveState；Editor 菜单可生成 Unity 场景，尚未提交 Playable 场景 |
| Gameplay Trigger Playable | ✅ 19.4A Playable | `Assets/Scenes/GameplayTriggerPlayable.unity`；Combat occupancy -> Trigger -> Gameplay Buff / Story -> SaveState/replay-compatible hash，支持键盘与HUD移动、Capture/Restore/Reset；capture仅为session内存slot，不是durable persistence |
| Gameplay Trigger Network / Scale | ✅ 19.4B | protocol-2 bounded authority、exact Gameplay component codec coverage、snapshot/late join、rollback/resync、partial hash；固定seed规模档满足4/6/10 ms与memory/snapshot预算 |
| Gameplay Component Buff / Modifier Runtime | ✅ v0.1 | `GameplayComponentBuffSetComponent` / `GameplayComponentModifierSetComponent` 提供 component-native buff / additive modifier state、cleanup、hash 和 SaveState |
| Config Driven Ability | ✅ v0.3 | `_useAbilitySlice = true` + `_useConfigDrivenAbility = true`，通过命名化 `AbilityEffectConfig` 驱动 Strike / Ignite |
| Gameplay Diagnostic Snapshot | ✅ v0.1 | `GameplayDiagnosticSnapshotBuilder.Build(...)` 汇总 Entity / Attribute / Buff / Modifier / Ability / Event 只读诊断状态 |
| Gameplay World Diagnostics | ✅ v0.1 | `GameplayWorldDiagnostics` / `GameplayWorldDiagnosticsSummary` |
| Runtime Config Change Handling | ✅ v0 | config-driven Ability 通过 `RuntimeAbilityConfigResolver` 创建，Demo 显示 source / policy / changed / rebuilt / failed 摘要 |
| UI Toolkit Runtime Showcase | ✅ M6 accepted | HUD 支持手动控制、Patch / Mod Package 加载、Ability 重建、Old/New 配置对比、Diagnostic View、Mini Game Feedback，以及 `MxStatBar` / `MxCommandButton` / `MxStatusBadge` / `MxEventLog` / `MxPanelTabs` 复用控件 token。按 #529 共存策略，Showcase / Debug / Editor / Validation surface 默认继续使用 UI Toolkit |
| FairyGUI Runtime UI Adapter | ✅ Runtime Slice | `MxFramework.UI` 提供 noEngine view contract / navigator / command contract；`MxFramework.UI.FairyGUI` 通过 `MxFramework.Resources` 加载 package bytes 并打开 `MxRuntimeHud` / `MxStoryDialog`；`MxFramework.UI.FairyGUI.Manifest` 校验 schema、package bytes、ResourceCatalog、component/control/command/localization drift，并通过 noEngine C# spec 生成 manifest / partial binder。FairyGUI adapter 是可选 player-facing path；UI Toolkit diagnostics/showcase 不被自动替换；生成绑定规范见 `Docs/Decisions/ADR-006-fairygui-binder-generation.md` 和 `Docs/Guides/FAIRYGUI_BINDER_GENERATION_GUIDE.md` |
| Runtime Foundation Showcase Path | ✅ v0.1 | `RuntimeAbilitySliceRunner` 通过 `RuntimeHost` 驱动 command drain、simulation tick、diagnostics/replay hash；HUD 手动按钮走 `RuntimeCommandBuffer`；Ability Slice 支持 SaveState 恢复 |
| Tetris Runtime Validation | ✅ v0.1 | 纯 C# Tetris validation fixture 覆盖固定帧输入、gravity/lock/line clear、replay playback hash、hash mismatch 和 SaveState JSON roundtrip |
| Tetris Playable Demo | ✅ v0.1 | `Assets/Scenes/TetrisRuntimeValidation.unity`，UI Toolkit UXML/USS 棋盘；键盘输入经 `RuntimeCommandBuffer` 进入 `RuntimeHost` 后推进 Tetris |
| Breakout Runtime Validation | ✅ v0.3 | 纯 C# Breakout validation fixture 覆盖连续运动、AABB 碰撞、关卡推进、砖块类型/HP/道具砖、多球、Wide/Slow/Multi/ExtraLife/Laser 道具、预发球滚动、按球拍位置发射方向、Replay hash、hash mismatch 和 SaveState JSON roundtrip |
| Breakout Playable Demo | 待补齐场景 | 当前仓库保留 `BreakoutPlayableDemo` / `BreakoutAppFlowDemo` / UI Toolkit 资产，但 Breakout 场景资产尚未提交，因此不标记为 Playable |
| Marble Maze Playable Demo | ✅ v0.1 playable | `Assets/Scenes/MarbleMazeBoot.unity` / `Assets/Scenes/MarbleMazeGameplay.unity`，Unity Physics 作为 Rigidbody/Collider 边界权威；输入经 `DefaultInputService` / `InputSnapshot` 转为 `RuntimeCommandBuffer` tilt 命令；RuntimeHost 记录 physics sample/checkpoint、计时、诊断 hash、Replay playback 和 SaveState JSON roundtrip |
| Item Lifecycle Showcase | ✅ v0.1 playable | `Assets/Scenes/ItemLifecycleShowcase.unity`，UI Toolkit 按钮驱动 `RuntimeHost` + Items Loot/World/Runtime/CharacterApplicationBridge，支持 loot、逻辑格移动、pickup、Main Hand / Off Hand 目标槽装备、unequip、inventory/world SaveState、hash compare；Iron Sword 图标验证 `ItemResourceRef` / `ResourceKey` / `ResourceManager` / Sprite / UI Toolkit 链路；pickup 诊断显示目标物、坐标和距离；不使用 Unity trigger/physics 作为拾取权威 |
| Items Acquisition Foundation | ✅ v0.1 Runtime Slice | `MxFramework.Items.Acquisition` 提供直接发放和购买式 cost+grant 的底层事务，含 `ItemAcquisitionRequestId` 幂等 ledger、atomic dry-run/commit、runtime command、hash 和 SaveState；不包含完整 Shop UI、商品目录、限购、平台支付或邮件兜底 |
| Rendering Demo Slices Showcase | ✅ v0.2 playable | `Assets/Scenes/RenderingDemoSlicesShowcase.unity`，UI Toolkit HUD + buttons/keys 验证 `GlobalFrameContext`、`IMxRenderPassProvider` / `IMxRenderPass` + `ISharedRenderTextureRegistry`、`IMaterialBindingHub`、`IRenderDataPublisher`、`IVolumeBlender` diagnostics 和 `MxVolumetricFogPassProvider` 全局高度体积雾；不包含局部雾体、完整多光源体积光、runtime URP Volume application 或项目业务内容 |

→ 场景：`Assets/Scenes/RuntimeVerticalSlice.unity`, `Assets/Scenes/TetrisRuntimeValidation.unity`, `Assets/Scenes/ItemLifecycleShowcase.unity`, `Assets/Scenes/GameplayTriggerPlayable.unity`
→ 运行器：`Demo/RuntimeVerticalSliceRunner.cs`, `Demo/GameplayComponentRuntime/GameplayComponentRuntimeShowcaseRunner.cs`, `Demo/Tetris/TetrisPlayableDemo.cs`, `Demo/Breakout/BreakoutPlayableDemo.cs`, `Demo/Breakout/BreakoutAppFlowDemo.cs`, `Demo/Items/ItemLifecycleShowcaseRunner.cs`, `Demo/GameplayTriggers/GameplayTriggerPlayableShowcaseRoot.cs`
→ CharacterTest projectile 技能当前是 playable slice：`AttackSecondary` 通过 `AbilityRuntimeModule` 解析到 `fireball_impact`，使用 60 帧冷却；`fireball_impact` 第 6 帧发 `SendGameplayRequest(requestId=character_test.fireball.projectile)`，CharacterTest 用 request id 查询 generated-first spawn profile，再由 `projectileStableId` 查询 projectile definition，最后交给通用 `MxFramework.Combat.Projectiles` runtime 做确定性 motion / speed profile 采样、`CombatPhysicsWorld.QueryCapsule` sweep、`HitResolveSystem` 结算，并以 `DamageProfileSourceKind.ProjectileImpact` 回到现有 damage / hit presentation / impact VFX 链路。spawn profile v2 支持 deterministic baked socket 起点、`CasterForwardRange`、`LockedTargetCenter` 和 `LockedTargetOrCasterForwardRange` 目标点：Fireball / Flame Lance / Fireball Lob / Fireball Homing 从 `mainHand` socket 发射并优先瞄准 CharacterTest deterministic locked hostile target center，无锁定、友方、死亡或超距时回退正前方射程；Fireball Burst / Fireball Surge / Fireball Return / Fireball Sway / Fireball Bezier 从 `mainHand` socket 发射并使用正前方射程。`LockedTargetCenter` 是严格锁定模式，无有效目标时拒绝 spawn。target point 和本次发射 effective lifetime 保存到 snapshot / save / network / debug；`ArcToTarget` 锁定目标时用 `ceil(distance / speedPerFrame)` 计算 effective lifetime，使终点精确落到发射时解析出的目标中心。普通 `ArcToTarget` / `BoomerangReturn` / `SwayForward` 会把 target point 作为路径采样目标点，但不追踪或吸附；`HomingToTarget` 会保存 tracking target id，每帧用 deterministic body 重新解析目标中心并更新 direction / target point，目标无效时沿上一帧方向继续。Fireball Burst / Fireball Lob 不再由 F5 / F6 直接触发；它们保留在临时技能栏 / slot 流程中用于验证 `AreaPulseOnImpact` 和锁定抛物线。`Fireball Surge` 使用 `Linear + DirectHit + EaseInQuadratic` 表达加速 projectile；`Fireball Return` 使用 `BoomerangReturn + DirectHit` 表达折返 projectile；`Fireball Sway` 使用 `SwayForward + DirectHit` 表达侧向摆动 projectile；`Fireball Homing` 使用 `HomingToTarget + DirectHit` 表达追踪 projectile，默认在临时技能栏 8 号槽验证。`Fireball Bezier` 使用 `BezierToTarget + DirectHit` 表达贝塞尔曲线 projectile，默认在临时技能栏 9 号槽验证。`Flame Lance` 保留为穿透 projectile 样例和 focused tests：同一 projectile 最多 accepted 3 个 hostile target，并用 accepted hit history 避免跨帧重复命中同一目标。飞行中表现由 `MxFramework.Combat.Projectiles.Unity` 的 generated presentation binding 映射到 Fireball / Fireball_Burst / Flame_Lance prefab，并由 `CombatProjectileUnityPresenter` 读取 projectile snapshot 同步；该 presenter 是 view-only，不参与规则、hash、SaveState 或 replay。CharacterTest full snapshot 现在携带 projectile runtime state、origin、target point、effective lifetime、tracking target id 和 accepted hit history，full / owned hash 都覆盖 active projectile；旧 snapshot 缺少 `projectiles` 字段时按空 projectile 状态恢复，保持离线和 LAN late-join 兼容。
→ CharacterTest Tornado 补充：当前 CharacterTest projectile roster 已增加 `Tornado`，使用 `SwayForward`、正前方 `mainHand` 发射、slot 7 默认热栏、飞行期间持续吸引 hostile、每 30 active frames 触发 2.5m wind tick damage，过期时再触发最终 2.5m wind area pulse；Tornado flight/charge/impact/tick prefab 均已绑定；Fireball Sway 仍可绑定，Fireball Homing 在临时技能栏默认 slot 9，Fireball Bezier 作为可绑定候选。
→ CharacterTest Beam/Laser Lance 补充：当前 CharacterTest projectile roster 中，旧 `910014` 已命名迁移为 `Beam Lance` / `ability.character_test.beam_lance` / `character_test.beam_lance.projectile`。它仍是 1-frame `Linear` 44m capsule sweep，半径 0.20m，最多 accepted 5 个 hostile，同一 projectile 不重复命中同一目标，accepted hostile 命中附加 `buff.character_test.stasis.action_stop`，显示 `STOP`，持续 90 runtime frames；VFX id 迁移为 `vfx.magic.beam_lance.charge/beam/impact`，prefab 归到 `Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/`。新的 `Laser Lance` / `ability.character_test.laser_lance` 使用 `910016` ability 和 projectile definition `91014`：action frames 0-29 播 `vfx.magic.laser_lance.charge`，frame 30 发 `character_test.laser_lance.projectile`，44m range、0.06m radius、最多 1 个 hostile accepted hit、命中首个目标即阻断、不附加 Stasis；path VFX id 是 `vfx.magic.laser_lance.laser`，由 projectile 结算后的 origin/endpoint/length 参数驱动，不烘焙固定 44m endpoint。
→ UI：`Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml`, `Assets/UI/MxFramework/Tetris/TetrisPlayableDemo.uxml`, `Assets/UI/MxFramework/Breakout/BreakoutPlayableDemo.uxml`, `Assets/UI/MxFramework/ItemLifecycle/ItemLifecycleShowcase.uxml`, `Assets/UI/MxFramework/GameplayTriggers/GameplayTriggerPlayable.uxml`, `FGUIProject/assets/MxRuntimeHud`

---

## 8. Unity Editor 工具

| 能力 | 状态 | 说明 |
|------|------|------|
| Framework Manager 窗口 | ✅ v1 | 框架状态面板 |
| Config Workbench | ✅ v1 | 配置编辑工作台 |
| Field Inspector | ✅ v1 | 字段检查器 |
| Buff Authoring Workflow | retired | Buff v2 no longer uses the old Unity Buff workflow |
| WGame Buff Logic 面板 | retired | WGame-specific Buff logic display is outside current framework scope |
| Runtime Preview Server | retired | Legacy Buff preview server and `MxFramework.Preview` module were removed |
| Resource Catalog Validator | ✅ v0.5 | `ResourceCatalogEditorValidator` 使用 `AssetDatabase` 校验资源存在性和类型 |

---

## 9. Authoring / Mod Diagnostics（旧 Buff 编辑器已退役）

### 8.1 Authoring Core / CLI

| 能力 | 状态 | 说明 |
|------|------|------|
| Ability Package CLI | ✅ v0 | `ability compile` |
| Buff v2 Package CLI | ✅ v0 | `buff validate` / `buff compile` / `buff import-unity` for `config/buff_authoring.json`; generates Gameplay Buff and Combat Aura runtime JSON |
| Buff v2 Workbench | ✅ v9 | Vanilla Web management UI for existing Buff v2 packages; uses designer-facing labels/options, `types[]` multi-type classification, `瞬时-持续-永久` time editing, simplified stacking editing, localized name key, fallback display name, short badge/icon metadata, Auto/Manual effect description, server-side authoring diff and field diagnostics as the Save gate, generated semantic diff on Compile, and explicit generated JSON import from saved Ready packages |
| Character Package CLI | ✅ v0 | `character inspect` / `character validate` / `character compile` |
| Animation Package CLI | ✅ v0 | `animation compile` |
| EditorServer 本地 API | ✅ v0.2 | `editor serve` (port 4873)，默认上下文为 character package |
| Mod Package 校验 | ✅ v0.1 | `package validate` — schemaVersion/路径穿越/格式/kind-layer 10 条规则 |
| Mod Package 运行时加载 | ✅ v0.1 | `RuntimeModPackageLoader.LoadFromDirectory()` — StreamingAssets 包驱动 Demo |
| Package Catalog / LoadPlan | ✅ v0.1 | `RuntimeModPackageDiscovery.Discover()` + `RuntimeModPackageLoadPlanBuilder.Build()` — 包扫描/校验/排序 |
| 多包 Runtime Patch 合并 | ✅ v0.1 | `RuntimeModPackagePatchMerger.Merge(loadPlan, baseRegistry)` — 按 LoadPlan 顺序合并，last-write-wins，输出 override 报告 |
| Mod Package Loadout / 启用状态 | ✅ v0.1 | `RuntimeModPackageLoadoutJson` + `RuntimeModPackageLoadPlanBuilder.Build(catalog, loadout)` — 按 `PackageKey` 持久化启用组合 |
| Mod Diagnostic Snapshot | ✅ v0.1 | `RuntimeModDiagnosticSnapshotBuilder.Build(...)` + `RuntimeModDiagnosticSnapshotJson.SaveToJson(...)` — 统一导出 catalog/loadout/loadPlan/merge 诊断快照 |
| Mod Diagnostic CLI | ✅ v0.1 | `mod diagnose` — 包发现/加载计划/合并诊断 → JSON 快照；支持 `--container`/`-c`、`--loadout`、`--output`、`--pretty`、`--fail-on-warning`；退出码 0/2/5/1 |
| Mod Diagnostic Service | ✅ v0.2 | `ModDiagnosticService.BuildSnapshot(...)` 由 CLI 与 EditorServer 共用 |

### 8.2 Buff 编辑器 Web UI

The old external Buff editor, WGame `BuffType`, `DamageByAttr` preview, and runtime Buff patch compatibility are retired with Buff Framework v2. Current Buff v2 tooling is the Authoring JSON + CLI path plus Buff v2 Workbench. The Workbench builds on `buff_authoring.json`, folds identity/kind fields into technical details, edits only existing Buff/Aura entries, uses designer-facing labels, safe options, `types[]` multi-type classification, three-state time editing, simplified stacking editing, localized name key, fallback display name, short badge/icon metadata, and Auto/Manual effect descriptions for current editable fields, refreshes authoring diff through Validate before Save, and must not restore the old protocol. It stores localization keys only; it does not write Localization TSV files.

### 8.3 Unity Preview Server

Retired with the legacy Buff editor path. The Unity WebSocket preview server, Buff patch loader, and `applyBuff` preview protocol were removed. Current validation should use component/runtime tests, CharacterTest slices, and focused Unity EditMode tests.

---

## 10. Combat 确定性物理 / 动作运行时

| 能力 | 状态 | 说明 |
|------|------|------|
| Combat Physics Query v0 | ✅ v1 | `CombatPhysicsWorld` 支持 Ray / AABB / Sphere / Capsule / Sector / OBB 查询、AABB / Sphere / Capsule 目标 collider registry 和稳定排序；CharacterTest 场景 authoring 当前仍走 AABB marker |
| Broadphase v0 | ✅ v1 | Grid / spatial hash candidate 裁剪，debug report 输出 raw / dedup / post-filter / hit 计数 |
| Hit Query Debug Report | ✅ accepted | `CombatPhysicsQueryDebugReport`、Showcase 摘要、Play Mode hit / miss explain 和 authoring 测试已验收 |
| Hit Resolve Bridge v0 | ✅ v0.1 | `HitResolveSystem` 支持 hit-once 去重、owner 防护、阵营过滤、动态目标状态解析、blocked/resolved 事件派发和 Action window state adapter |
| Damage Event hit config v1 | ✅ v0.3 | `CombatHitEventConfig` 统一武器 `StartHitTrace` 和 projectile impact 的目标类型、环境 blocked presentation、stable event id、命中次数约束和通用伤害属性（damage type / base damage / impact power / launch power / tags / analysis category）；目标类型包含敌方、友方、自身、第三方角色、可伤害物和环境目标；CharacterAction / Projectile / CharacterTest 正式链路只读取 `hitEvent.damage`，不再读取 `damageProfileId` 或旧顶层伤害字段 |
| Combat Projectile Runtime v0 | ✅ v1.1 | `MxFramework.Combat.Projectiles` 支持 noEngine `Linear` / `ArcToTarget` / `BoomerangReturn` / `SwayForward` / `HomingToTarget` / `BezierToTarget` motion、`Constant` / `EaseInQuadratic` / `EaseOutQuadratic` / `EaseInOutSmoothstep` speed profile、typed spawn buffer、capsule sweep、DirectHit / AreaPulseOnImpact / piercing DirectHit policy、accepted hit history、impact resolver、Hash / SaveState / Debug snapshot、generated definition + spawn profile JSON catalog importer；impact 通过公共 `hitEvent` / `areaHitEvent` 表达目标类型、命中约束和伤害属性，`CombatProjectileImpactEvent.Context` 区分 direct sweep 与 area pulse，direct `impact.hitEvent.damage` 必填，`areaHitEvent.damage` 可继承 direct `hitEvent.damage`；`damageProfileId` / 旧顶层伤害字段会导入失败；spawn profile 把 CharacterAction request id 映射到 projectile definition、deterministic socket 起点、forward range target point、strict locked-target aim 和 locked-target-or-forward aim；spawn request / active snapshot 可保存本次发射 effective lifetime 和 tracking target id，锁定目标 `ArcToTarget` 用固定速度反算生命周期并精确落到发射时的目标中心，`HomingToTarget` 通过 deterministic target provider 逐帧重定向；CharacterTest 已接入单体 Fireball、范围 Fireball Burst、锁定目标优先的穿透 Flame Lance、临时技能栏触发的锁定抛物线 Fireball Lob、加速 Fireball Surge、折返 Fireball Return、摆动 Fireball Sway 和临时技能栏 8 号槽追踪 Fireball Homing 和 9 号槽贝塞尔 Fireball Bezier 样例，并通过 `MxFramework.Combat.Projectiles.Unity` 显示 flight prefab，network full snapshot / owned hash 覆盖 active projectile、origin、target point、effective lifetime、tracking target id 和 hit history |
| CharacterTest Tornado projectile sample | ✅ playable | `ability.character_test.tornado` / `character_test.tornado.projectile` / projectile definition `91011` 使用 `SwayForward` motion 和 `0.05m/frame` 很慢前进速度，飞行期间每帧发布 CharacterTest `PhysicalField/AttractionAcceleration/Capsule` interaction，再由 interaction physics consumer 使用随 projectile 移动的竖向 `QueryCapsule` 风柱持续吸引 hostile 角色和 movable natural objects：capsule spine 从 projectile 下方 1.0m 到上方 2.5m，吸引半径 4.5m；吸引使用确定性质量/距离响应，36000 mm/s² 是 80kg 近中心参考加速度，轻目标更快、重目标更慢、边缘目标吸力降低，并通过 Tornado 专用运动请求绕过当帧 idle ground damping，径向吸附速度接近 6500 mm/s 时确定性衰减；被动吸附位移不写 active desired velocity，因此不触发基础 locomotion blend；每 30 active frames 触发 `character_test.tornado.projectile.tick` 2.5m 竖向风柱 wind tick damage；direct sweep collision disabled，过期时触发最终 2.5m wind area pulse，并通过 Tornado flight/charge/impact/tick VFX prefab 表现 | Game.CharacterTest + Combat.Projectiles + Interaction Physics + Vfx |
| CharacterTest Beam Lance projectile sample | ✅ playable | `ability.character_test.beam_lance` / `character_test.beam_lance.projectile` / projectile definition `91012` 使用 1-frame `Linear` 44m capsule sweep，0.20m radius，最多穿透 5 个 hostile，`OncePerProjectile` 防止同一目标重复命中；accepted hostile 命中附加 90 frame `STOP` Stasis / 凝滞；action frames 0-29 播放 `vfx.magic.beam_lance.charge`，frame 30 发 projectile，frames 30-60 播放 `vfx.magic.beam_lance.beam` 专属长束和 30 frame 后摇余辉；不新增 framework Laser API | Game.CharacterTest + Combat.Projectiles + Vfx |
| CharacterTest Laser Lance projectile sample | ✅ playable | `ability.character_test.laser_lance` / `character_test.laser_lance.projectile` / projectile definition `91014` 使用 1-frame `Linear` 44m single-target sweep，0.06m radius，最多 1 个 accepted hostile，命中首个目标即阻断，`OncePerProjectile` 防止同一目标重复命中；action frames 0-29 蓄力，frame 30 发 projectile，path VFX 从 muzzle 拉到首个 accepted hit point 或 44m endpoint，不附加 Stasis；不新增 framework Laser API | Game.CharacterTest + Combat.Projectiles + Vfx |
| CharacterTest Chain Lightning sample | ✅ playable | `ability.character_test.chain_lightning` / `character_test.chain_lightning.cast` 使用 CharacterTest-scoped chain resolution instead of projectile bounce：frame 30 同帧解析整条链，首目标锁定 hostile 优先，fallback 为前方 18m 锥形最近 hostile，之后每段从上一目标向 8m 内最近未命中过 hostile 跳跃；最多 4 个 unique hostile，伤害 `light` 6/5/4/3，impact power 2，不附加 `STOP` / Stasis / 新 Buff；arc VFX 使用 runtime length 参数沿本地 `+Z` 拉伸 | Game.CharacterTest + Combat.Physics + HitResolve + Vfx |
| CharacterTest Poison Bottle sample | playable | `ability.character_test.poison_bottle` / `character_test.poison_bottle.projectile`: 30f startup, frame 30 ballistic bottle throw, 30f recovery; hostile-only `BallisticArcToTarget` bottle, 0.20m radius, no authored aim max range, about 15m same-height reach from `speedPerFrame=0.3535533906` max initial speed and `gravityPerFrameSquared=0.008333333`; automatic range-optimal max launch angle (`maxLaunchAngleDegrees=0`) and per-throw solved launch speed, 90f safety lifetime cap, no direct HP damage; first hostile hit or expiry creates a 2.5m / 180f poison pool, which applies or refreshes `Poisoned` / `POIS` (`buffId=601012`, `statusId=901012`) on creation and every 30f. Poisoned keeps the original StartFrame cadence and deals `toxic` 2 every 60f while active; a non-refreshed 360f application ticks at +60/+120/+180/+240/+300, and pool refreshes extend EndFrame so later ticks continue until expiration. Refreshing extends duration only: no stacking, slow, healing reduction, `STOP`, or Stasis. | Game.CharacterTest + Combat.Projectiles + Buffs + HitResolve + Vfx |
| CharacterTest Stun Bolt sample | playable | `ability.character_test.stun_bolt` / `character_test.stun_bolt.projectile`: bindable but not default-hotbar; 30f startup, frame 30 hostile-only Linear DirectHit bolt, 32m range, 0.18m radius, 0.45m/frame speed, first Damage-kind hostile hit consumes the projectile; direct HP damage is 0 and `OnHitBuffs` applies `Stunned` / `STUN` (`buffId=601013`, `statusId=901013`) with existing control presentation arbitration and loop VFX. | Game.CharacterTest + Combat.Projectiles + Buffs + HitResolve + Vfx |
| CharacterTest Sword Wave sample | playable | `ability.character_test.sword_wave` / `character_test.sword_wave.projectile`: bindable but not default-hotbar; 13f startup, frames 13/73/133 send the same medium-speed Linear DirectHit crescent projectile, 30m range, 0.75m/frame speed, up to 4 unique hostile accepted hits, no repeated hit on the same target, and wall/static blocking consumes the projectile with impact presentation. Collision uses five parallel capsule samples with 0.22m sample radius and 0.85m half-width; sample axis roll matches the flight VFX roll `0/-55/+55`. It deals `wind` 9 with impact power 8, applies no Buff/control status, and uses dedicated charge/flight/impact runtime VFX resources. | Game.CharacterTest + Combat.Projectiles + HitResolve + Vfx |
| CharacterTest Dash Strike sample | playable | `ability.character_test.dash_strike` / `dash_strike`: bindable but not default-hotbar; frame 6..19 root motion moves 0.3m local forward per frame for about 4.2m with `rootMotion.obstaclePolicy=StaticOnly`, frame 6..20 opens `StartHitTrace.hitVolume.sourceKind=CasterBody` and `OncePerEvent`, accepted hostile hits go through `HitResolveSystem`, the hit range derives from the caster runtime physics body plus action-local CasterBody modifiers with previous-to-current body sweep, and static world blocking still comes from CharacterTest kinematic motion. It deals `slash` 12 with impact power 18 through `damage.dash_strike_hit` and reuses sword-wave style charge/impact VFX as presentation-only bindings. | Game.CharacterTest + CharacterAction rootMotion + combatTrack HitTrace/hitVolume + CharacterTest motion + HitResolve + Vfx |
| CharacterTest Hook Chain sample | playable | `ability.character_test.hook_chain` / `hook_chain` / `character_test.hook_chain.projectile`: bindable but not default-hotbar; frame 12 fires a 12m Linear DirectHit hook projectile, 0.75m/frame, 0.18m radius, first accepted hostile hit consumes the projectile, and wall/static blocking stops it without creating a pull. Accepted hostile hits still resolve through `HitResolveSystem`, then CharacterTest creates a short active tether pull that submits deterministic `ExternalForceRaw` requests to move the target toward the caster's front anchor; friendly/self/dead/blocked hits do not create the pull. It deals `control` 1 with impact power 4 and reuses stun-bolt style charge/flight/impact VFX as presentation-only bindings. | Game.CharacterTest + Combat.Projectiles + HitResolve + CharacterTest motion + Vfx |
| CharacterTest Arc Mine sample | playable | `ability.character_test.arc_mine` / `arc_mine`: bindable but not default-hotbar; frame 12 sends `character_test.arc_mine.deploy`, which throws the ballistic carrier `character_test.arc_mine.projectile` toward the locked hostile's feet within 8m or caster forward 5m fallback. Carrier hostile hit deals no damage and spawns the secondary `character_test.arc_mine.drop_projectile`; carrier/drop world, ground, wall, static impact or safety expiration deploys the deterministic mine at the projected point. The mine expires after 180f if untriggered; a hostile entering the 1.25m trigger sphere starts a 60f fuse, and only fuse completion resolves one 2.25m blast through `HitResolveSystem` for up to 8 hostile targets. It deals `light` 10 with impact power 14 through `damage.arc_mine_blast`; Arc Mine uses dedicated rune-electric `vfx.magic.arc_mine.flight/deploy/fuse/trigger` resources, and VFX only reads projectile flight, deploy, fuse-start, and explosion events. | Game.CharacterTest + CharacterAction request + Combat.Projectiles carrier/drop + CombatPhysicsWorld.QuerySphere + HitResolve + Vfx |
| CharacterTest Rune Turret sample | playable | `ability.character_test.rune_turret` / `rune_turret`: bindable but not default-hotbar; frame 12 sends `character_test.rune_turret.deploy`, which throws the ballistic carrier `character_test.rune_turret.deploy_projectile` toward the locked hostile's feet within 8m or caster forward 5m fallback. Carrier static impact or safety expiration deploys a deterministic turret, waits 18f, then scans every 12f for the nearest hostile within 10m and fires up to 8 linear `character_test.rune_turret.shot_projectile` shots at 45f intervals. Shot hits still resolve through `HitResolveSystem` using `damage.rune_turret_shot`; turret deploy/idle/fire/expire VFX and projectile flight/impact VFX only read runtime events. | Game.CharacterTest + CharacterAction request + Combat.Projectiles deploy/shot + CombatPhysicsWorld.QuerySphere + HitResolve + Vfx |
| CharacterTest control-status presentation arbitration | playable | `Stunned` / `STUN` (`buffId=601013`, `statusId=901013`) is a Buff v2 status that locks active movement, facing, action starts, root-motion traces, and weapon traces without adding new Attributes. `CharacterControlPresentationKind` resolves one primary control presentation per entity: reserved Knockup/Knockdown, Paralyzed, Stunned, Stasis, then Rooted. Higher-priority control statuses mask lower-priority main poses without deleting their rule state; removing Paralyzed while Stunned remains switches back to stun in the same tick. Stasis alone still pauses animation, but Stasis+Stun shows the stun loop VFX instead of freezing the current action frame. Dedicated control animation action keys are optional; missing Stun/Paralysis animation config falls back to idle. | Game.CharacterTest + Buffs + Gameplay.Status + Vfx + Animation |
| Combat Animation RuntimeHost Modules | ✅ v0.1 | `CombatActionRuntimeModule` / `CombatWeaponTraceRuntimeModule` / `CombatAnimationDiagnosticsModule` 使用 RuntimeHost stage + priority 推进动作、武器轨迹和诊断快照 |
| Runtime World Lifecycle | ✅ accepted | `Revision`、`CreateStats()`、`MoveBody()`、`RemoveBody()`、`RemoveCollider()`、`Clear()`、`CopyBodiesTo()`、`CopyAabbCollidersTo()`、`CopySphereCollidersTo()`、`CopyCapsuleCollidersTo()` 已验收 |
| Combat Motion v0 | ✅ accepted | 固定帧移动、重力、跳跃、grounded、静态 AABB 阻挡、Motion -> Physics World 同步和 Play Mode smoke 已验收 |
| Combat Motion v1 Capsule Proxy | ✅ accepted | `CombatMotionCapsuleProxy`、capsule narrow-phase sweep、skin width / no penetration、Showcase capsule summary 和 Combat regression 已验收 |

---

## 12. Core 工具层

| 能力 | 状态 | 说明 |
|------|------|------|
| 优先级堆 `Heap<T>` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 无序列表 `UnsortList<T>` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 位运算 `BitUtils` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 字符串工具 `ZString` | ✅ v1 | `MxFramework.Core` (noEngine) |
| 向量扩展 | ✅ v1 | `MxFramework.Core.Unity` (Unity 依赖) |
| 随机抽表 | ✅ v1 | `MxFramework.Core.Unity` (Unity 依赖) |

→ 测试：`Tests/Core/` 目录

---

## 📋 规划中 / 部分完成

| 能力 | 状态 | 任务文档 |
|------|------|----------|
| Runtime Foundation 01 Host Core | ✅ v0.1 Host Core + Ability Showcase 接入 | `Tasks/RUNTIME_FOUNDATION_01_RUNTIME_HOST.md` |
| Runtime Foundation 02 Frame/Command/Replay | ✅ v0.1 Core + Ability Showcase Command/Hash 接入；Replay playback runner 已完成，JSON replay 待办 | `Tasks/RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md` |
| Runtime Foundation 03 SaveState | ✅ v0.1 Contract + Ability Showcase restore + SaveState orchestration；通用 Gameplay restore 接入待办 | `Tasks/RUNTIME_FOUNDATION_03_SAVE_STATE_SERIALIZATION.md` |
| App / Scene Flow 01 Foundation | ✅ v0.1 Foundation | `Interfaces/AppFlow.md` |
| Breakout Runtime Showcase 01 | ✅ Runtime Validation v0.3 | `Tasks/BREAKOUT_RUNTIME_SHOWCASE_01.md`；当前仓库未提交 Breakout 场景资产，不标记为 Playable |
| Marble Maze Unity Physics Showcase 01 | ✅ playable | `Tasks/MARBLE_MAZE_UNITY_PHYSICS_SHOWCASE_01.md` |
| AI Assist 闭环（Authoring AI Assist） | 📋 | `Tasks/AUTHORING_EDITOR_04_AI_ASSIST.md` |
| Mod/Dev 双模式 | 📋 | `Tasks/AUTHORING_EDITOR_05_MOD_DEV_MODES.md` |
| Unity Bridge 导出 | 📋 | `Tasks/AUTHORING_EDITOR_06_UNITY_BRIDGE.md` |
| Runtime Preview 03.5 closeout | retired | Superseded by Buff v2 runtime tests and CharacterTest validation |
| AIAction 迁移试点准备（AIAction Config） | 🔄 Pilot 01 verified, Pilot 02 planned | `Tasks/AI_ACTION_MIGRATION_PILOT_02_REAL_DATA_ENTRY.md` |
## CharacterTest Arena Runtime AI Planner Behavior Cards

CharacterTest Arena now treats the behavior group setup page as a behavior-card deck builder organized by capability slots. The page shows coverage for Targeting, Engage, Damage, Fallback, Pursuit, Control, Kite, Defense, Navigation, and Support; filters the behavior card library by the selected capability; edits only the current Arena draft deck; and shows fixed scenario outcome forecasts before starting a match.

This is CharacterTest internal test-harness authoring. It does not add a public `MxFramework.AI` API, does not change Runtime AI Planner Tick semantics, and does not change SaveState/network/replay/hash data. The Arena draft still compiles into the existing Runtime AI Planner profile/evaluator slot before battle starts.
