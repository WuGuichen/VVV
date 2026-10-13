# Application Startup Core 接口

> Status: Current
>
> Code-Doc: `Packages/com.vincent.wgameframework/Application/ApplicationStartupCoordinator.cs`, `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Application/ApplicationStartupCoordinatorTests.cs`
>
> Phase 1 noEngine coordinator。本文描述 `MxFramework.Application` 程序集中、由 `MxFramework.ApplicationStartup` 公共命名空间暴露的 plan/runner/transaction/publication contracts；candidate Runtime/Resource/AppFlow/Scene bridge 与 Unity exact-scene adapter 见 `ApplicationSessionBridge.md`，SaveGame candidate publication 见 `ApplicationPersistenceBridge.md`。Platform lifecycle bridge 与 Persistence-backed suspend/quitting policy 已实现；Unity Boot bridge 仍未实现。

## 职责与依赖

程序集 `MxFramework.Application` 位于 `RuntimeHost` 外层，负责冻结并执行一份 startup DAG。公共 C# API 统一位于 `MxFramework.ApplicationStartup`；禁止重新引入 sibling namespace `MxFramework.Application`，避免在 `MxFramework.*` 调用方中遮蔽 `UnityEngine.Application`。程序集只依赖 `MxFramework.Core` 与 `MxFramework.Runtime` 的 operation contract，保持 `noEngineReferences=true`，不注册为 Runtime module，也不直接引用 Platform、Persistence、Resources、SceneFlow、Gameplay Trigger 或 Unity。

```text
Core + Runtime operation contracts
  -> Application startup plan / runner / transaction boundary
  -> future domain bridges
  -> future ApplicationStartup.Unity Boot shell
```

## 稳定身份与 Plan

| API | 分配分类 | 语义 |
| --- | --- | --- |
| `ApplicationStartupPlanId` / `ApplicationStartupStepId` | value operations `NoAlloc` | ordinal、case-sensitive 稳定身份；default 无效 |
| `ApplicationStartupStepDescriptor` | `LowFreqAlloc` | 冻结 dependency、order、weight、timeout 和 exclusive owner |
| `ApplicationStartupPlan.TryCreate` | `LowFreqAlloc` | 在任何 transaction/step side effect 前验证并建立稳定拓扑顺序 |
| `ApplicationStartupPlan.Get*InExecutionOrder` | `NoAlloc` | 按 dependency readiness -> order -> step id ordinal 读取 |

Plan 拒绝空/重复身份、descriptor-step mismatch、missing/self/duplicate dependency、cycle、invalid weight/timeout、exclusive owner conflict、空 plan 和超过 256 steps。v0.1 只串行执行；DAG 不授权跨 step 并发。

## Operation 与 Coordinator

`IApplicationStartupStepOperation` 扩展 `IRuntimeOperation`，增加协作式取消、typed inner error detail 和显式 `Tick`。`Begin` / `BeginRollback` 是 `LowFreqAlloc`；operation steady polling 由实现保证 `NoAlloc after initialization`。

`Tick` 抛异常不等于 operation 已释放 ownership。若 operation 可能在抛异常前进入外部或异步工作，必须实现 `IApplicationStartupFaultDrainOperation`；coordinator 只调用其专用 fault-drain pump，并保留同一 operation，直到真实 terminal status。未实现该接口的 operation 只能采用 strong exception guarantee：抛出前不得留下在途 ownership，且抛出时必须已经呈现 terminal status。coordinator 在无法证明 terminal 时 fail-closed，不会开始 rollback。

`ApplicationStartupCoordinator` 是 single-use runner：

- `Start(runId)` 创建一份新 transaction 并捕获 non-null expected live state；
- `Tick()` 串行 pump 当前 forward 或 rollback operation；steady path 为 `NoAlloc after Start`，phase transition 和用户 operation code 除外；
- cancel/timeout 进入 `Draining`，不再开始新 step；不可取消 operation 必须先到 truthful terminal boundary；
- 可取消 operation 在 `RequestCancel()` 后若同步进入 terminal，不会再收到一次非法 `Tick()`；取消分发抛异常时 coordinator 保留 operation ownership 并继续 drain，不会让 rollback 与未知在途工作并发；
- 只回滚已经 `Succeeded` 的 forward steps，顺序为严格 reverse completion order；取消、超时或 forward fault 后当前 operation drain 为 `Succeeded` 时，也必须恰好一次登记完成计数与权重，并先补偿当前步骤、再补偿此前成功步骤。drain 为 `Failed` / `Cancelled` / `TimedOut` 的当前步骤不进入成功补偿序列；一个 rollback error 不阻断后续 compensation；
- 全部 forward step 成功后进入 transaction `TryCommit()`；普通 terminal result 只调用一次，`PublicationDeferred` 表示 shared save fence 在任何 candidate mutation 前拒绝本次尝试，coordinator 保持 rollback ownership 并在后续 Tick 重试；失败、取消、超时都不发布；
- transaction 的 `CommitObservation` 是 `NoAlloc` ownership query；`TryCommit()` 返回或抛出前必须记录 `NotPublished` 或精确的 `PublishedState`。已记录 Published 的异常按已提交处理，绝不 rollback/dispose live lease；outcome 仍为 `NotAttempted` 时 coordinator 保留 transaction 并停在 Draining，禁止猜测发布结果；
- terminal run 不可重启；retry 必须创建新的 coordinator/transaction/operation graph。

`ForwardProgress` 单调使用 completed weight + current progress；`RollbackProgress` 独立计算。`CaptureSnapshot()` 是低频 `AllocByDesign` diagnostics，不能在每帧 UI 中无缓存调用。

## Candidate Publication Contract

`LiveApplicationState` 是不可变 aggregate value：Boot sentinel 固定 revision `0`、无 Runtime session、next frame `-1`；candidate revision 为 unpublished `-1`。`Current.Session` 只暴露不实现 `IDisposable` 的只读 session view；owner lease 仅由 publisher/retirement 内部持有，观察者不能从 Current 获得 disposal capability。`TryPublish` 的成功 result 原子携带本次实际安装的 `PublishedState` receipt，且 `Success` 只有在 status 与 receipt 同时有效时才为 true；legacy 两参 success result 是无有效 receipt 的兼容 sentinel，不能作为 publication 成功。transaction 不得通过随后读取 `Current` 猜测 receipt。`LiveApplicationStatePublisher` 在一个低频锁内校验 expected reference、拒绝 current/queued/outstanding-retirement lease alias、checked 分配 revision，并为 candidate 获取可撤销的 exact-once publication reservation；所有 handoff preflight、published state 和 retirement receipt 分配都在最终 ownership handoff 前完成。正常 preflight/validation/allocation 失败释放 reservation，不消费 candidate。同一 publisher 在 handoff preflight 起至 claim、CAS 和 retirement enqueue 完成的整个 external handoff interval 内重入 `TryPublish`，会在任何 reservation、mutation 或 claim 前结构化失败；不同 publisher 的 publication 不受该实例级屏障影响。可选 `IApplicationLiveSessionPublicationHandoff.TryPreparePublicationHandoff` 在 candidate 仍由 rollback owner 持有时必须可重复调用，并在 success/false/exception 三种结果下都不得转移 ownership；其最终 `ClaimPublicationHandoff` 为 `NoAlloc`、不可抛出并返回 ownership 已完整转移的 invariant proof。false 会在 `Current` CAS 前结构化失败并永久保留 candidate reservation，禁止重试一个所有权状态矛盾的 candidate。proof 成功后，publisher 使用 `Interlocked.CompareExchange` 安装单个 immutable state reference；低频锁保证 CAS 必须命中同一 expected，否则属于 publisher invariant violation。

`ApplicationPublicationSaveFence` 是不依赖 Persistence 的公开 composition token，但 acquire API 与 lease 类型保持 internal，任意 token consumer 不能自行取得 authority 并永久阻塞发布。publisher 在自己的 gate 内、candidate reservation/handoff preflight 前获取 publication lease，并持有到 `Current` CAS 完成；lifecycle save 在 publisher safe boundary 内获取 save lease，并持有到 provider operation truthful terminal。竞争方在任何 ownership mutation 前返回 `PublicationDeferred`/bridge deferred status。internal `TryDispatchSafeBoundary` 不消费 `ApplicationLiveStateRevisionToken` 的 callback sequence，且只授予 friend PlatformBridge；公开扩展仍必须使用 revision token + sequence API。Boot state 不携带内部 checkpoint marker，公开 `Current.Session` 也不能取得该 capability。

`MxFramework.Application.PlatformBridge` 只依赖 Application 与 Platform。`ApplicationPlatformLifecycleBridge.TryCreate` 必须先取得 lifecycle source 的唯一 drain claim，并验证 publisher/fence 是同一实例。`Pump` 以固定 budget 排空并合并一个 bounded pending epoch；suspend、quitting 与 overflow-hidden evidence 不会被 resume/focus 清除。active operation 每次 Pump 只 poll 一次；后续事实进入 successor epoch。失败/cancel/timeout/generation conflict 保留 evidence 并由新 checkpoint-worthy fact 或 `RequestRetry` re-arm；quitting 只做 non-blocking best effort。`LastCheckpointStatus` 在清除 operation 后保留真实 terminal enum。空 pump、drain/merge 和 conforming poll 为 `NoAlloc after initialization`，operation/request/snapshot 创建为低频分配边界。

`Pump` 在任何 drain、scratch 或 evidence mutation 前取得 exact reentrancy guard；并发调用、policy 内 nested Pump 与 operation Poll 内 nested Pump 都返回稳定 `Reentrant`，不能替换 active operation 或遗失 save fence。达到 drain budget 时 active operation 仍必须 poll 一次，持续 lifecycle stream 不能饿死 terminal observation。checkpoint evidence 明确携带 `HasLostRevisionRange` 与 truthful `LostRevisionStart/End`；普通 edge 没有 lost range，多个 resync 按时间顺序合并最早 start 与最晚 end，计数与 revision 饱和不回绕。

bridge 在合并前验证 lifecycle tuple：ordinary edge 不得携带 lost-range 字段，resync 必须有正 lost count、有效 start/end 且 end 等于 latest snapshot revision，Unknown/default kind fail-closed 为 `InvalidLifecycleEvidence` 并且不污染 pending epoch。active checkpoint 即使遇到 invalid evidence 仍优先 poll 一次，恶意/损坏 source 不能借此永久占住 save fence。

publisher 只在 exact current state 的真实 Runtime attempt 返回 `Succeeded` 且 Tick 本身未抛异常时记录该 state 的首次 checkpoint qualification。Boot、replacement、failed/no-attempt 与 throwing Tick 都不继承资格；PlatformBridge 在 safe boundary 内核对 exact state 资格后才获取 save fence 与调用 policy。`RuntimeCheckpointBarrier` 仍是 Persistence capture 前的第二道 participant quiescence gate。

内部 checkpoint capability 还提供 `CanReplace(currentCapability)` provenance preflight。publisher 在持有 publication fence 后、candidate reservation/handoff/CAS 前调用 candidate capability；false 返回 `PublicationConflict` 且 candidate 零 mutation，异常结构化失败。Persistence capability 对同 profile/slot replacement 必须验证 shared durable authority identity 与 exact current epoch/head；不同 profile/slot 可按明确 domain-switch 规则接受；未知 capability fail-closed。这样 successful old-current save 后，基于旧 durable head 准备的 candidate 不能在 fence 释放后成为 live，fresh-per-build authority 也不能伪装同一 save domain。Boot/null 与 legacy both-null 保持显式兼容路径。

retirement capacity 包含已 dequeue 但尚未 truthful cleanup terminal 的 receipt；满时 publication 前 fail-closed。`TryDequeueRetired(out LiveApplicationStateRetirement)` 为 `NoAlloc`，只把预建的 exact-once cleanup capability 从 queue owner 转给 retirement driver。普通同步 lease 由内置 Dispose operation 适配；需要 Unity scene unload 等异步清理的 lease 实现 `IApplicationLiveRetirementSource`，返回 `IApplicationLiveRetirementOperation`。`BeginRetirement` 是低频/可分配边界，预热后的 `TickRetirement`、status、ordered warning 与 ownership debt 读取为 `NoAlloc after initialization`。driver 在调用 operation Tick 前校验 status/progress/error tuple：status 必须已知且不得从 running 回退 pending，progress 必须为有限 `0..1`，success 必须 progress `1` 且无 error，failed 必须有 error，pending/running/cancelled 不得带 error；契约矛盾会形成 local warning 与 ownership debt 并 poison reservation。只有 truthful `Succeeded` 且 `OwnershipDebt.IsNone` 才注销 publisher reservation；失败、异常、cancel/timeout 或 success-with-debt 永久 poison 该 bounded slot。retirement 永远无权写回 `Current`，因此旧代清理失败只形成 warning/debt，不会回滚新代。

## Live frame driver

Each `LiveApplicationStatePublisher` may grant exactly one `ApplicationLiveFrameDriver`. The
publisher gate validates and atomically marks the single safe boundary for publication, live Tick,
and revision-aware callback dispatch, but is never held while invoking external Runtime pumps or
callbacks. While the boundary marker is active, concurrent or reentrant publication fails
immediately before reservation or mutation instead of waiting on external code. Tick/callback
completion re-enters the gate only to record attempt state and clear the marker; a throwing attempt
observer cannot leave the publisher permanently blocked or mask the original Tick exception. An
observer contract violation poisons the currently seeded generation so the possibly half-completed
frame cannot be retried; only publication of a new exact state permits a fresh seed.

Published states carry one opaque `ApplicationLiveStateRevisionToken`, minted by that publisher and
bound by reference to that exact immutable state. Callers cannot construct or retarget a token.
`TryDispatchCallback(token, sequence, callback)` accepts only the current publisher/state token and
the exact next non-negative sequence. It consumes the sequence before invoking the callback, so a
duplicate, gap, concurrent duplicate, stale-state token, or foreign-publisher token cannot produce
a second authority side effect. The check and dispatch path is `NoAlloc after initialization`;
callback exceptions propagate after the sequence is consumed.

At every Tick boundary the driver rereads `Current`. Boot revision 0 produces no Runtime attempt.
The first observation of each new runtime state seeds its private cursor once from
`NextRuntimeFrame`; repeated reads never reseed. A real RuntimeHost attempt consumes that frame even
when FailFast throws, so the same frame is never retried. A failure before the Host attempt does not
consume it. After attempting `long.MaxValue` the cursor becomes exhausted and later calls fail
closed without wrapping.

The internal pump reports exact `None`, `Succeeded`, or `Failed` attempt status without throwing.
Both successful and failed attempts consume the frame. A normally returning Collect-policy failure
produces `RuntimeAttemptFailed`, never the ordinary successful-attempt result.

`IApplicationLiveSessionLease` 是已实现 bridge 的聚合 ownership boundary。具体 bridge 必须保证 transaction commit 后 detach candidate，publisher 成为 current/retired lease 唯一 owner；generic runner 不拆解 lease。

## 错误与诊断

`ApplicationStartupError` 保存稳定 Application code、phase、step id、generic `RuntimeOperationError`、bridge-owned `IApplicationStartupInnerErrorDetail` 和可选 exception。控制流只读取稳定 enum/status，不解析 message 或 bridge-specific detail。

cancel/timeout drain 到 terminal 后，`DrainedOperationStatus`、`DrainedOperationError` 与 `DrainedInnerDetail` 会在清除 current operation 前持久化；snapshot 同步携带这组三字段，因此 outer Cancelled/TimedOut 不会覆盖 inner terminal evidence。

drain 成功登记不会把取消、超时或 forward fault 改为成功，也不会启动后续步骤或提交 transaction。`CompletedStepCount` / `ForwardProgress` 与 snapshot 保留真实成功步骤及其权重；补偿成功后仍使用已锁定的 outer outcome，补偿失败时返回 `RollbackFailed` 并保留原 `ForwardError`。

Rollback errors 保持实际 reverse-attempt 顺序，可通过 `RollbackErrorCount` / `GetRollbackError()` 读取。`RollbackFailed` 不覆盖 `ForwardError`。

## 当前限制

- `MxFramework.Application.SessionBridge` 已提供 exact-transaction-bound candidate RuntimeHost/AppFlow/ResourcePlan/SceneFlow steps、owner-bound compensation、scene isolation/safe-drain rollback 和只读 frozen handoff proof；`MxFramework.Application.SessionBridge.Unity` 已提供 provider-approved、exact `Scene` handle 捕获/卸载与 inactive-root gate；`MxFramework.Application.PersistenceBridge` 已提供 restore candidate 与原子 publication/retirement 组合。public graph 不暴露 raw lifecycle/resource/cleanup authority。
- 没有 Unity Boot shell、Loading/Error/Retry surface 或 PlayMode 入口。
- 当前切片已实现唯一 live driver、long-lived Platform lifecycle drain 与安全 suspend/quitting checkpoint policy，但尚未接入真实 Unity Boot 组合根，因此不声明端到端 production-ready load 或 Playable。

## 验证

取消/drain 的 focused 回归包括不可取消晚成功、协作取消晚成功、取消分发异常后成功、分发前已成功和分发中同步成功；并核对非成功 drain 对照、非均匀权重、snapshot、严格逆序、终态后重复调用不再补偿、当前/此前补偿失败继续、fault-drain ownership 与 cleanup 错误顺序。离线直接执行真实源码与 NUnit assertions 的证据不能替代 Unity compile/Console 或原生 EditMode Runner 验收。

Focused noEngine 与 Unity EditMode tests覆盖 plan invalid/cycle/owner conflict、稳定顺序、同步 terminal 不再 Tick、success/commit、commit 后异常 outcome、middle failure、fault-drain ownership、cancel drain（含同步终态和 cancel-dispatch fault）、timeout-later-failure evidence、reverse rollback、rollback failure continuation、完整 N+2 cleanup error evidence、candidate exact-once/lease alias rejection、fresh retry、live frame、lifecycle evidence 与 suspend checkpoint policy。真实 Unity Boot 与 PlayMode 证据仍属于后续阶段。

## References

- `Docs/Decisions/ADR-011-application-startup-authority.md`
- `Docs/APPLICATION_STARTUP_ORCHESTRATION_DESIGN.md`
- `Docs/Interfaces/Runtime.md`
- `Docs/API_STANDARDS.md`
- `Docs/QUALITY_GATE.md`
