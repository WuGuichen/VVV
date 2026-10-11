# ADR-008: Gameplay Trigger Runtime Authority And Dispatch Boundary

> Status: ADR

Date: 2026-07-16

Decision Status: Accepted; Phase 1 and Runtime Integration 19.2A-H2 delivered

## Context

MxFramework 已有 Story trigger、Modifier conditions/effects、Gameplay commands、Combat spatial query 和若干领域 trigger，但缺少公共的 Enter/Stay/Exit/Signal 生命周期、cooldown/count、条件程序、action dispatch、hash/replay/save 边界。

直接把 Unity callback、EventBus 或任意 action callback 提升为通用方案，会产生以下问题：

- callback 到达顺序和 Unity object identity 进入 authority；
- 条件和副作用跨模块直接耦合；
- replay 只记录 command 时丢失外部触发事实；
- Story、Gameplay 或 Trigger 争用同一个 command buffer drain；
- same-frame 行为依赖隐式模块顺序；
- membership、cooldown 和次数无法稳定 hash/save。

## Decision

本 ADR 冻结框架边界与 phased delivery。Phase 1 noEngine kernel 和 Runtime Integration 19.2A-H2 已分别通过独立验收；Combat/Unity/domain bridge、network wire 和 Playable 仍必须通过后续 Phase 的独立验收，不能由本地 replay playback/bootstrap 的存在推断为 Current 能力。

### 1. Authority

建立逻辑模块 `MxFramework.Gameplay.Triggers`，它拥有不可变 definition registry 和唯一 runtime state store。它是 noEngine Gameplay authority，不属于 Unity、Events、Story、Combat 或 presentation。

Trigger Runtime 作为 `IGameplaySystem` 集合接入现有 `GameplaySystemPipeline`。`GameplayRuntimeModule` 继续是 Gameplay `RuntimeCommandBuffer` 的唯一 drain owner；Trigger 不创建另一个共享该 buffer 的 Runtime module。

### 2. Canonical Inputs

Core 只接受：

- 每个 source binding、每个 scheduled evaluation frame 的 complete occupancy observation；
- ordered sequence 或 unordered multiset 的 typed Signal batch；
- 按 Subject 分组、使用 `Fix64` 和 explicit hysteresis/initial-sample policy 的 complete scalar batch。

Runtime 的 source reducer 从旧 membership/threshold state 与完整输入确定性派生 Entered/Stayed/Exited/Pulsed。扫描或采样不完整、容量不足时整批拒绝并保留旧 source state，不推断 mass exit，也不把 missing sample 当成 `0`。非 scheduled frame 不算 incomplete。外部 `SetContact` command 先归并到可 hash/save 的 provider accumulator，再形成 complete occupancy view，不建立另一套生命周期语义。

包含 SignalId、Subject、Instigator、payload 和 ordering 的 external Signal 使用 replayable typed envelope；当前 `RuntimeCommand` 三个 payload slot 不足以承载完整语义，frame-local sidecar handle 不可接受。codec 必须同时覆盖 command、replay 和 network。

正式空间事实默认由 `CombatPhysicsWorld` bridge 提交。Unity Collider/Physics 只能作为 external adapter，把合并后的 level state 或 Signal 通过已注册、已验证的 RuntimeCommand 输入；callback 不能直接修改 Trigger 或目标域状态。

### 3. Conditions And Actions

一个 Definition 由 Source Definition 与多个 Routes 组成。Source 只归约状态；每个 Route 独立订阅 occurrence，并拥有自己的 conditions、activation policy、arbitration 和 actions。Entered 与 Exited 使用独立 Route，避免进入策略抑制退出清理。

Conditions 是只读、预编译、typed predicate program。Core 不直接调用带有不同 mutable context/lifecycle 的 Modifier、Story 或 Runtime AI Planner condition；bridge 可以把目标域的只读 typed facts 适配成 leaf evaluator，但不能包装副作用接口或任意委托。字符串表达式和反射执行同样禁止。

Trigger Runtime 只产生 `GameplayTriggerActionIntent`。Gameplay、Buff、Story、VFX、Audio 等 target-owned bridge 负责把 intent 验证并转换为自己的 typed request/command。Trigger action 不直接修改目标域。

source truth 与 route/action 是两层事务：完整 input 一旦成功 reduce 就提交一次；condition、arbitration、intent capacity 或 target reject 都不回滚 membership/threshold state，也不靠下一帧伪造重复 Entered。

一次 activation 使用 `Prepare -> Commit/Abort` reservation protocol：prepared token 在 Commit 前不可见，失败时此前 token 按稳定逆序幂等 Abort；全部 Prepare 成功后才以 no-fail Commit 发布 core intent/deferred/outbox 和 same-frame target request。只查询剩余容量不构成原子保证。失败时不消耗 route count/cooldown，不产生部分 intent。目标域 semantic reject 不回滚 activation，且不会自动 disable/exhaust Trigger；权威反馈必须在下一 frame 作为新的显式 Signal 输入。

once-per-presence 使用 source-owned presence epoch 和 route 的 `LastActivatedPresenceEpoch`，不通过 Exited 隐式重置 lifetime count。

Phase 1 的精确策略语义冻结如下：

- frame F 接受 activation 且 `CooldownFrames=N` 后，下一次 eligible frame 为 `F+N`；`N=0` 允许同帧不同 occurrence 分别接受。
- `StayIntervalFrames` 从 presence start 或上一次 accepted Stay 计时；condition、arbitration、reservation reject 不推进 Stay schedule。
- 同一 source/frame 第二份 complete batch 只拒绝第二份并保留第一份；scalar batch 内重复 Subject 整批拒绝并保留旧 source state。
- threshold `PrimeSilently` 首样本若 active 会建立 presence epoch 但不发 Entered；departure 的 current scalar 保留最后一次有效 sample。
- action `Prepare` 顺序使用 accepted activation execution key 后接 action index；失败时按该顺序的严格逆序 Abort。
- Phase 1 disable 只实现 `ClearSilently`；`EmitExitThenClear` 连同容量失败语义留给 lifecycle command integration Phase，不作为当前 API。

### 4. Deterministic Ordering And Arbitration

默认所有合格 trigger 独立 activation。只有显式 `ExclusiveGroupId` 才做 winner arbitration。winner priority 与 accepted activation execution order 是两个独立字段。

OccurrenceIdentity、WinnerTie 和 AcceptedExecution 使用分离的 total key。`WinnerPriority + WinnerTieKey` 决定 exclusive winner，`ExecutionOrder + CandidateIdentityKey` 只排序已接受 activation，不参与 winner。Ordered Signal 的 sequence 是必须进入 replay 的 authority input；Unordered Signal multiset 按 SignalId/Subject/Instigator/payload 规范排序并由 core 分配 ordinal。不使用 DebugName、Unity instance ID、Dictionary 原始顺序或隐式 callback 到达顺序。

### 5. Frame Delivery

同帧 effect 只允许写入同一 Gameplay pipeline 后续 phase 消费、且持有 reservation token 的 typed request buffer。需要 RuntimeCommand 的目标必须显式排到 `frame.Next()` 或更晚。

Story 当前通常早于 Gameplay，因此 Gameplay Trigger -> Story 默认使用 Trigger-owned reliable outbox：frame F 写 `OriginalDueFrame=F+1` record；任意 current frame G 满足 `OriginalDueFrame <= G` 时，pre-Story bridge 才可用 target buffer current frame G 生成 command，并在 buffer 接受后移除。原始 due 保留用于稳定排序，不能生成 late command。outbox 进入 hash/save/rollback，bridge 生成的 derived command 不进入 replay exogenous input。

v0 选择 fail-fast recovery：authority host 必须使用 `RuntimeHostErrorPolicy.FailFast`。handoff 后 target Tick 抛错时 session 立即作废并从上一 checkpoint/replay 重建；不允许 CollectAndStopFrame/CollectAndContinue。需要错误后继续运行时，必须新增 persistent idempotent acknowledgement ledger 或整帧 rollback。

Trigger-local enable/disable/reset/signal 进入带 DueFrame/sequence 的 internal deferred state，禁止同帧递归。在 `PreCommand` 应用 prior-frame deferred，再在 `Command` 应用本帧 external command，因此 current-frame external intent 最终覆盖旧 deferred intent。

所有 Command 后、source reduce 前先 sanitation；已经 stale 的 owner/subject 一律 silent cleanup。Resolution 后第二次 cleanup 也一律 silent，不在 Route pass 结束后伪造 Exited。需要 Exited 的销毁必须走仍可寻址的 staged departure。

### 6. State, Hash, Replay And Save

Trigger runtime state 包含 generation-safe instance、occupancy/threshold source state、presence epoch、instance enabled、按 instance/subject/instigator/pair scope 保存的 per-route exhausted/activation count、cooldown/next eligible frame、next activation sequence、deferred internal state 和 reliable delivery outbox。

Definition 与静态 Binding 分别计算 `DefinitionCatalogHash` / `BindingCatalogHash`，所有 authority hash、SaveState、replay bootstrap 和 network mismatch gate 使用二者合成的 `GameplayTriggerCatalogHash`。外部或非确定 trigger facts 先成为 exogenous command + envelope 并由 replay 记录；内部 occurrence、activation、action intent、deferred mutation、outbox 和 bridge-derived command 不重复录制。command source/provenance registry 必须让 recorder 和 authorizer 一致地区分两类输入。

Save 只由组合根在完整 `RuntimeHost.Tick` 结束 barrier 捕获；需要新增 barrier/quiescence contract 和 `RuntimeSaveStateErrorCode.PendingWork`，因为现有 provider API 没有 stage context，现有 enum 也没有该 error。稳定 future outbox 正常保存，未归约 input、未消费 reservation、in-flight transfer 或未被 owner 捕获的 target request 导致 capture failure。

完成 frame F 后保存 `RuntimeSaveState.Frame=F`；restore 得到 end-of-F state，首个 Tick/ReplayStartFrame 是 F+1。Restore 在完整 schema/catalog/entity/range 校验后一次性替换 state，不允许半恢复。

Phase 19.2H2 的 playback/bootstrap 使用 schema/config-bound header 和组合根提供的独占 session factory。`Fresh` 从空 authority 开始；`CheckpointSuffix` 要求 end-of-F checkpoint 与 replay start F+1 精确连续，并固定执行 `Initialize -> Start -> atomic restore -> presentation reset -> Ready`。factory 必须交付同一 Trigger runtime 的 Deferred/Command/Evaluation/Router、可选 reliable outbox、sealed exogenous source registry、restore coordinator 与精确默认调度身份；相同 ID 替身或错误 runtime identity 在 lifecycle 前 fail-closed。

每个 replay frame 只接受 sealed registry 授权的 Exogenous Signal/occupancy/scalar fixed records，并在任何 submit/Tick 前完整验证顺序、sequence、typed batch、source kind 和容量。成功 frame 恰好执行一次 Host Tick，再计算 Trigger authority hash。空 frame 仍推进 continuity；`long.MaxValue-1` 为最后可 Tick frame。occurrence/activation/action、presentation records、internal deferred、reliable outbox/transfer 和 bridge-derived command 从 authority/checkpoint 重建，不重复录制。observer 配置与 overflow 不改变 replay hash。

### 7. Allocation And Capacity

evaluation、membership diff、condition program、arbitration 和 intent dispatch 是 Tick 高频路径，目标为初始化后 `NoAlloc`。

definition、instance、membership、input、candidate、intent、outbox 和 deferred 容量显式配置。host 启动后不自动扩容；overflow 必须确定性拒绝并产生 numeric diagnostics，不能静默丢弃 authority facts。现有自动增长 request/event 容器不是 bounded sink，必须升级或隔离在新的 fixed-capacity contract 后。

Phase 19.2H1 已把 Trigger event 实现为独立 `GameplayTriggerRuntimeEventSource` fixed array，而不是扩大通用 `GameplayRuntimeEvent` 或复用自动增长的 `RuntimeEventQueue<T>`。一个 source 先绑定且只能绑定一个 Runtime producer，再绑定一个 `GameplayTriggerEventRouterSystem` destructive drain owner；Router 固定在 Gameplay `Diagnostics/1000`，只把 due prefix fanout 到 ordinal-frozen、各自独立的 bounded observer ring。多个 presenter 不得直接 drain source，observer 也不得回写 authority。

source/observer capacity 0 合法；overflow/disabled/sequence exhausted 只累计 saturating presentation diagnostics，不改变 authority。event source/observer pending、source sequence、drop/high-water、enabled 和 registry 不进入 hash、SaveState、replay 或 checkpoint quiescence。authority restore 不清 presentation；bootstrap/rebuild 在 exclusive boundary 显式 reset presentation，且保留既有 ownership/binding/enabled。Phase 19.2H2 playback 复用这个 reset，但从不把 observer 内容作为 authority 输入。

## Consequences

Benefits:

- checkpoint、door、trap、pickup、objective signal 和 EffectVolume 可共享同一套生命周期与验证模型。
- Unity/Combat/Story/Buff 等依赖被隔离在 bridge，core 保持 noEngine。
- replay、SaveState、hash、network 和 diagnostics 从第一天拥有明确边界。
- same-frame 与 next-frame 行为可测试，不依赖偶然模块顺序。
- 领域 trigger 可保留自己的高性能/专用语义，不被强制包装成万能规则引擎。

Costs:

- 需要 definition compiler、relation store、typed input envelope/replay codec、bounded buffers/reservation token、reliable outbox、event Router、playback/bootstrap 和多个 bridge。
- 组合根必须配置并验证 Gameplay system 相对顺序与容量。
- target domain reject 不提供跨模块原子回滚，调用方需通过 ActivationId/action result 诊断。
- checkpoint barrier/PendingWork、Network snapshot/rollback restore ordering 和只使用可序列化 command 的 restorable timer bridge 仍需后续基础设施决定。

## Alternatives Considered

### Use Unity trigger callbacks as the system

Rejected because Unity callback order、Collider identity 和 physics state 会泄漏进 authority，headless/replay/network 行为也难以统一。

### Use the Events module as trigger authority

Rejected because Events 是同步传输/fanout，不拥有 membership、cooldown、count、SaveState 或 replay input validation。

### Reuse Modifier conditions/effects as a universal rule engine

Rejected because Modifier 的 invocation context 和生命周期不同，也没有 observation、re-arm、ordering 或 cross-module delivery contract。

### Put Trigger into MxFramework.Runtime

Rejected because Runtime 必须保持 gameplay-agnostic；Subject、GameplayEntityId、conditions 和 action intents 都属于 Gameplay 语义。

### Give Trigger its own Runtime module and share Gameplay command buffer

Rejected because command buffer has one drain owner；先运行的 module 会消费并推进 frame，造成命令丢失或 late-command 行为。

### Execute arbitrary callbacks directly

Rejected because callback 无法静态验证、稳定 hash、网络授权或保证依赖方向，也会绕过目标域 request/command contracts。

### Force all existing domain triggers to migrate

Rejected because Ability timeline、CharacterAction、Combat contact/projectile 和 VFX lifecycle 有更精确的领域时间与批处理语义。

## Adoption Gate

已关闭的 Phase 1 gate：

- 使用独立 `MxFramework.Gameplay.Triggers` noEngine asmdef，依赖 Core / Runtime / Gameplay；
- occupancy、threshold、ordered/unordered signal 的 canonical reducer；
- fixed-capacity route state、condition program、policy、exclusive arbitration 和 reservation sink；
- 上述 Phase 1 精确策略语义与 focused noEngine 测试入口。

Phase 19.2H1 已关闭 live presentation transport gate：一个 Runtime producer、一个 Diagnostics Router drain、fixed-capacity source/observer、stable due fanout、exact producer hooks、typed future ActionResult seam，以及 presentation state 对 hash/save/checkpoint/restore 的明确排除。

Phase 19.2H2 已关闭本地 playback/bootstrap gate：schema/config-bound header、exact composition identity、Fresh 与 end-of-F checkpoint suffix、完整 typed exogenous preflight、连续/terminal frame contract、一次 Host Tick/hash、typed failure context、presentation reset、derived trace 与 G1/G2 exactly-once regeneration，以及 64-Tick NoAlloc evidence。该关闭不包含 network wire、跨进程认证或任何领域 bridge。

后续 Phase 仍开放：

- reliable outbox 的 retry/deadline/reset/owner-stale 语义；
- network replay wire、rollback participant dependency order、timer ownership、Combat/Unity bridge 和 Collider entity mapping 重绑语义；
- Playable 与 scale/network evidence。

因此本 ADR 的 Accepted 只授权按 Phase 交付；每项 Current 能力仍以接口文档和对应测试证据为准。

## References

- `Docs/GAMEPLAY_TRIGGER_RUNTIME_DESIGN.md`
- `Docs/Interfaces/Runtime.md`
- `Docs/Interfaces/Gameplay.md`
- `Docs/Interfaces/Combat.md`
- `Docs/Interfaces/Story.Runtime.md`
- `Docs/Decisions/ADR-005-story-runtime-command-boundary.md`
