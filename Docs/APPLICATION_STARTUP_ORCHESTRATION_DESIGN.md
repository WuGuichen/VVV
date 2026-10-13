# Application Startup Orchestration 启动编排设计

The application-lifetime lifecycle drain, suspend/quitting checkpoint policy, exact-session save
authority, and publication/save mutual exclusion are specified in
`Docs/APPLICATION_PLATFORM_LIFECYCLE_BRIDGE_DESIGN.md`.

The Current Unity Boot composition, unique driver ownership, Update/FixedUpdate phase order,
fresh retry, typed Validation surface, retirement slots, and shutdown boundary are specified in
`Docs/Interfaces/ApplicationUnity.md`. Its UI Toolkit scene is a Validation Runtime Slice, not a
player-facing Playable entry.

> Status: Design
>
> Version: 0.1.1
>
> Date: 2026-07-20
>
> Implementation status: Phase 1-5 framework contracts are implemented; Phase 4 includes the generated Unity Boot Validation Runtime Slice and focused EditMode/PlayMode evidence, without a player-facing Playable claim

Issue #709-B authority note: the candidate factory binds an exact transaction owner before build. Its public graph is read-only and yields only one cached frozen-handoff proof; RuntimeHost, AppFlow/resource leases, pump transitions, and synchronous cleanup remain assembly-owned. Nested AppFlow faults drain the same operation handle before compensation, and cleanup reports exact multi-domain debt through `SessionCleanupFailed` / `SessionCleanup`.

## 1. Context

MxFramework 已有多块启动所需能力：

- `RuntimeHost` 负责已构建 runtime session 的生命周期和固定 Tick；
- `AppFlowController` 负责项目状态 Enter/Tick/Exit 和单个 pending transition；
- `SceneFlowController` 负责串行 scene load/unload、busy reject、progress 和结果；
- `ResourcePreloadService` 支持 cancellation、fail-fast、并发 preload 和失败清理；
- `ResourcePlanOrchestrator` 支持 preload、acquire、prepare change、commit change 和 release；
- Settings、Audio、Input、Resources 和各 Demo 有自己的 bootstrap/composition 入口。

当前缺少的是这些能力之外的应用级事务：

```text
platform ready
  -> settings/content/catalog
  -> optional save prepare-load
  -> resource plan
  -> candidate scene/session
  -> Runtime restore and validation
  -> AppFlow start
  -> publish live session
```

如果每个 Demo 继续自行编排，常见失败会变成：新资源已占用但旧资源已释放、目标场景失败后 Loading 卡死、某 participant restore 失败却继续使用半恢复 session、取消后残留 operation、RuntimeHost 尚未建立却把 startup 注册进 Host 形成循环。

本文设计独立的 noEngine `MxFramework.Application` 程序集，其公共 API 位于 `MxFramework.ApplicationStartup` 命名空间。它复用现有模块并明确唯一 owner，不替代 AppFlow、SceneFlow、Resources 或 RuntimeHost。

本文是目标设计。类型名和 bridge 名称在实现并同步 Current interface docs 前都不是公共 API。

## 2. Goals And Non-Goals

目标：

- 用可验证 plan 表达 step 依赖、稳定顺序、权重、取消和 rollback。
- 启动/加载始终在候选 transaction 中完成，只有全部成功才发布 live session。
- 复用现有 Runtime operation、ResourcePlan、SceneFlow、AppFlow 和 Runtime SaveState，并依赖本文兄弟设计中拟议的 SaveGame prepare-load contract。
- 暴露结构化 progress/snapshot/error，Loading UI 只观察，不掌握编排权威。
- 支持 cold boot、new session、load session 等项目 plan，而不硬编码 MainMenu/Gameplay 枚举。
- 正常每帧 pump 在初始化后 `NoAlloc`。
- 失败、取消、timeout 和 rollback failure 都有确定的 terminal state。

非目标：

- 不把 Startup Coordinator 注册为 `IRuntimeModule`；它必须先于 RuntimeHost 创建/发布运行。
- 不创建第二套并行 live authority、save system 或 dependency injection container；隔离 candidate 可以拥有下一代 AppFlow/SceneFlow/resource manager，但 commit 前不得冒充 live singleton。
- 不硬编码 Boot、Login、MainMenu、Gameplay、CharacterTest 等项目状态。
- 不让 Loading UI 持有 step、resource handle 或 session publication 权限。
- 不在 v0.1 通用化所有运行中场景 streaming、联网 matchmaking、patch download 或 account login。
- 不承诺对不可撤销的第三方副作用进行魔法回滚；step 必须声明 compensation 边界。
- 不允许反射发现 step、全局 service locator 或任意脚本文本执行。
- 不把 startup progress、wall clock 或 lifecycle 事件写入 Runtime authority、Replay 或 SaveState。

## 3. Relationship To Existing Modules

| Existing module | Remains owner of | Application Startup owns |
| --- | --- | --- |
| RuntimeHost | 已发布 session 的 Initialize/Start/Tick/Stop/Dispose | 何时构建候选 Host、何时发布 |
| AppFlow | 已发布/候选 session 内的项目状态 | AppFlow start step 的顺序和失败处理 |
| SceneFlow | 一次 scene load/unload operation | 候选 scene 策略、ownership handoff、rollback |
| Resources | catalog/provider/preload/handle/session | ResourcePlan step 的生命周期和 commit 时机 |
| Persistence | slot/envelope/prepare-load/restore bridge | 是否 load、候选 session publication |
| Platform | location、focus/suspend/quitting facts | startup/suspend policy 对事实的响应 |
| Settings | preference profile 和 apply | load/apply step 的顺序 |
| UI | Loading、error、retry presentation | 只观察 snapshot 并发出 typed request |
| Network | transport/session/authority | future network startup step 自己的 compensation |

Startup 是一次低频 application transaction，不是 gameplay authority。RuntimeHost 一旦发布并进入常规 Tick，Startup 不再 pump 该 Host 的模块。

## 4. Target Assemblies And Dependency Direction

```text
MxFramework.Core + MxFramework.Runtime
  -> assembly MxFramework.Application
     namespace MxFramework.ApplicationStartup  (noEngine coordinator/plan/transaction)

MxFramework.Application + MxFramework.Resources
  -> namespace MxFramework.ApplicationStartup.ResourcesBridge (optional step adapters)

MxFramework.Application + MxFramework.Persistence.Runtime
  -> namespace MxFramework.ApplicationStartup.PersistenceBridge

MxFramework.Application + MxFramework.Platform
  -> namespace MxFramework.ApplicationStartup.PlatformBridge    (noEngine lifecycle drain/policy adapter)

MxFramework.Application + MxFramework.Application.PlatformBridge
MxFramework.Runtime + MxFramework.Platform + MxFramework.Platform.Unity + UnityEngine
  -> assembly MxFramework.Application.Unity
     namespace MxFramework.ApplicationStartup.Unity              (thin main-thread driver)

MxFramework.Application.Unity + Unity UIElements
  -> assembly MxFramework.Application.Unity.Validation
     namespace MxFramework.ApplicationStartup.Unity.Validation   (optional Validation surface)

MxFramework.Application + MxFramework.Runtime
  -> project/session composition adapters

Unity composition root
  -> Application + Platform.Unity + Scene/Resource adapters
```

上述 `MxFramework.Application` 是程序集身份，可以保持不变；公共 C# API 必须使用 `MxFramework.ApplicationStartup` 及其子命名空间，未来 bridge 不得重新创建 `MxFramework.Application.*` sibling namespace。建议该程序集依赖 Runtime 以复用 `RuntimeOperationStatus`、`RuntimeOperationError` 和 `IRuntimeOperation`，但它不能进入 RuntimeHost module list。所有跨 Platform/Resources/Persistence/UI/Network 的具体 step 放在可选 bridge 或项目组合根，Application core 不直接引用这些模块。

`ApplicationStartup.PlatformBridge` 由 Boot composition root 在第一次 startup 前创建，并跨 retry、commit 和 live session replacement 长期持有。它是 `IApplicationLifecycleSource` 的唯一 drain owner；`ApplicationStartup.Unity` 只在主线程安全阶段驱动 bridge。bridge 每次消费事实时读取当前发布的 `LiveApplicationState`，因此 session replacement 不需要移交或复制 lifecycle queue，也不会产生两个 drain owner。

如果实现阶段决定不复用 Runtime operation contract，则新的 operation 必须解释为何不是重复基础类型；默认选择复用。

## 5. Terminology And State Model

| Term | Meaning |
| --- | --- |
| Startup Plan | 初始化期冻结的 step DAG、权重和 policy。 |
| Startup Run | 某个 plan 的一次执行 attempt。 |
| Step | 一个明确 owner、可轮询、可取消/补偿的低频操作。 |
| Candidate Transaction | 未发布 session/resources/scene 的隔离容器。 |
| Live Application State | 一次性发布的不可变聚合引用；包含当前 session lease、资源/场景 ownership 和下一 Runtime frame。 |
| Prepare | commit 前完成所有可能失败的构建、恢复、验证与 handoff preflight；不得改变 live 引用。 |
| Activate | 让已准备 candidate 对 live consumer 可见；commit 后只允许不可失败的 ownership flip。 |
| Commit | 所有 step 成功后，把 candidate 切为 live 的唯一 publication 点。 |
| Rollback | 逆序补偿已成功 step，并销毁 candidate。 |
| Handoff | SceneFlow/RuntimeHost 等 controller 从 startup step 向 live owner 转交唯一 pump 权。 |

候选 coordinator 状态：

```text
Idle
  -> Validating
  -> Running
  -> Committing
  -> Succeeded

Running
  -> Cancelling / TimingOut / Failed
  -> RollingBack
  -> Cancelled / TimedOut / Failed / RollbackFailed
```

terminal run 不可重新 Start。Retry 必须创建新的 run、operation 和 candidate transaction，避免复用残留状态。

## 6. Plan And Step Contracts

以下是目标 API 形态。

### 6.1 Stable Step Identity

```csharp
public readonly struct ApplicationStartupPlanId { /* stable value */ }
public readonly struct ApplicationStartupStepId { /* stable value */ }

public sealed class ApplicationStartupStepDescriptor
{
    public ApplicationStartupStepId StepId { get; }
    public IReadOnlyList<ApplicationStartupStepId> Dependencies { get; }
    public int Order { get; }
    public float ProgressWeight { get; }
    public double TimeoutSeconds { get; }
}
```

`StepId` 是诊断和依赖身份，不是本地化名称。`Order` 只解决同时 ready 的稳定 tie-break，不覆盖 dependency。排序固定为 topological readiness -> `Order` -> `StepId` ordinal。

### 6.2 Step Operation

```csharp
public interface IApplicationStartupStep
{
    ApplicationStartupStepId StepId { get; }
    IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context);
    IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context);
}

public interface IApplicationStartupStepOperation : IRuntimeOperation
{
    bool CanCancel { get; }
    IApplicationStartupInnerErrorDetail InnerErrorDetail { get; }
    void Tick(in ApplicationStartupTickContext context);
    void RequestCancel();
}

public interface IApplicationStartupInnerErrorDetail
{
    string DomainId { get; }
    string StableCode { get; }
}
```

规则：

- `Begin` / `BeginRollback` 为 `LowFreqAlloc`；operation steady `Tick` 和 getters 在初始化后 `NoAlloc`。Unity exact-scene unload 在 mutation-token contention 期间例外：首次成功的主线程 poll 同时 claim token 并启动 unload，属于具有 mutation 语义的 `AllocByDesign` contention-to-start transition；之后的 waiting/started polls 仍为 `NoAlloc`。
- step 通过构造函数持有明确领域依赖；context 不提供 untyped service locator。
- step operation 不能自行 Tick RuntimeHost 中已由其他 owner 驱动的 controller。
- `RequestCancel` 是请求，不是强制线程终止；operation 在安全边界进入 Cancelled。
- step failure 使用稳定 code；异常由 coordinator 转为 `StepException` 并进入 rollback。
- generic `RuntimeOperationError` 提供跨模块 summary；bridge 同时通过 immutable `InnerErrorDetail` 保留 typed domain detail。具体 detail 类型定义在 bridge assembly，例如 `SaveGameStartupErrorDetail`，Application core 不引用 SaveGame/Resource/Platform 类型。
- `InnerErrorDetail` 仅用于本地低频 diagnostics 和错误展示，不参与控制流、SaveState、network 或 hash；成功/取消路径返回 None detail。
- runner 只对 forward operation 已经 `Succeeded` 的 step 调用 `BeginRollback`；尚未成功的 step 必须自行清理 partial state，并且在 partial Unity/provider operation 尚未达到可安全释放边界时不得发布 Failed/Cancelled/TimedOut terminal。
- 不需要 compensation 的纯读 step 返回成功的 no-op rollback operation。

### 6.3 Plan Validation

开始任何副作用前验证：

- plan id 与 step id 非空且唯一；
- descriptor 与 step 一一对应；
- dependency 存在且无 self dependency；
- DAG 无环；
- weight 有限且大于 0；
- order、timeout 和 capacity 在允许范围；
- 至少存在一个 step；
- transaction factory、commit/rollback contract 非空；
- bridge 声明的 exclusive owner 不冲突。

validation 失败时不调用任何 step/transaction side effect。

### 6.4 Sequential V0.1

虽然 plan 使用 DAG 表达依赖，v0.1 coordinator 只按稳定拓扑顺序串行运行：

- Resources 自己可以在 preload step 内执行有界并发；
- 跨 step 并发会放大 Unity 主线程、取消和 rollback 复杂度，暂不启用；
- 后续若加入 parallel group，必须显式声明资源锁、失败 join 和 deterministic completion order。

不应为了“有 DAG”就默认同时运行所有 ready step。

## 7. Candidate Session Transaction

### 7.1 Core Contract

```csharp
public interface IApplicationStartupTransaction : IDisposable
{
    string TransactionId { get; }
    ApplicationStartupCommitResult TryCommit(
        LiveApplicationState expected,
        ILiveApplicationStatePublisher publisher);
    ApplicationStartupRollbackResult FinalizeRollback();
}

public interface IApplicationStartupTransactionFactory
{
    IApplicationStartupTransaction Create(ApplicationStartupPlanId planId);
}

public sealed class LiveApplicationState
{
    public long Revision { get; }
    public bool HasRuntimeSession { get; }
    public long NextRuntimeFrame { get; }
    public IApplicationLiveSession Session { get; }
}

public interface IApplicationLiveSession
{
    string SessionId { get; }
}

public interface ILiveApplicationStatePublisher
{
    LiveApplicationState Current { get; }
    ApplicationStartupCommitResult TryPublish(
        LiveApplicationState expected,
        LiveApplicationState candidate);
}
```

`Current` 只暴露不带 `IDisposable` capability 的 `IApplicationLiveSession` view；concrete owner lease 只在 publisher/retirement 内部流转。Operation 的普通 `Tick` 抛异常也不表示 ownership 已释放：可能在抛出前进入 external/async work 的实现必须提供 `IApplicationStartupFaultDrainOperation`，由 coordinator 保留同一句柄并专用 drain 到 truthful terminal；否则实现必须证明 strong exception guarantee 并在抛出时已经 terminal。

Commit 不得靠异常猜测结果。Transaction 在 `TryCommit()` 返回或抛出前，通过 `CommitObservation` 冻结 `NotPublished` 或 exact `PublishedState`；Published 后的异常仍按成功 publication 收口，不能 rollback 或 dispose 已发布 live owner。outcome 尚未确定时 coordinator fail-closed 保留 transaction。

`TryPublish` 的成功 result 必须在同一 publisher critical section 返回实际安装的 `PublishedState` receipt，`Success` 同时要求 success status 与非 null receipt；transaction 不得在返回后读取可能已被并发推进的 `Current` 来构造 observation。retired cleanup receipt 在 publication 低频路径预建，`TryDequeueRetired` 只做 `NoAlloc` capability transfer；queued、已 dequeue 和正在 Dispose 的 retirement 都继续占用 bounded reservation 并参与 lease-alias 拒绝，直到真实 Dispose 成功后才原子注销。Dispose 抛异常时 slot 永久 poison，fail-closed 防止同一 raw lease 再发布。owner lease 不能重新暴露给普通 `Current` observer。

以上冻结 publication 的只读可见性和内部 owner 能力边界。具体 owner lease 必须聚合并唯一拥有同一代 RuntimeHost、AppFlow、SceneFlow controller/scene lease、resource manager/catalog/provider/plan session 及项目 session services；这些 owner 不能再分别发布到多个可独立更新的全局字段。Application core 对 `Current` 只暴露只读 view，bridge 在自己的 assembly 内使用强类型 concrete lease。

publisher 创建时必须立即持有一个非 null Boot sentinel state：`Revision=0`、`HasRuntimeSession=false`、`NextRuntimeFrame=-1` 和幂等 no-op session lease。`Current` 因此永不为 null，第一次 gameplay publication 以该 sentinel 为 expected 并获得 revision 1；revision 0 不得分配给真实 session。

具体 transaction 可以额外暴露强类型 candidate services 给项目 step，但 generic coordinator 只看 commit/rollback。step 通过构造函数接收同一个 concrete transaction/candidate，不通过全局 locator 查找。

Persistence integration 时，concrete candidate 通过 `ApplicationStartup.PersistenceBridge` 实现 `IRuntimeSaveGameRestoreTarget`；Application core 和 Persistence core 不互相引用。

### 7.2 Publication Rules

- coordinator 只有在全部 forward steps 成功后才调用一次 `TryCommit()`。
- commit 前，当前 live session reference、active resource plan 和旧 scene 保持有效。
- 所有可能失败的 candidate build、restore、scene/resource readiness、RuntimeHost/AppFlow lifecycle、driver handoff preflight 都必须在 publication 前完成；若某个 post-swap activation 仍可能失败，该 bridge 尚不满足本设计，Phase 3 保持 blocked。
- commit 只能发生在 Application driver 的 quiescent boundary：没有 live Runtime Tick、SceneFlow/Resource pump 或 lifecycle policy dispatch 正在使用旧 state。
- commit 必须是 composition-root 级 publication：验证 `expected` 仍为 current 后，通过一次 compare-exchange 交换完整 `LiveApplicationState` 引用；禁止依次写 host、scene、resource 等多个 live 字段。
- live drivers 在每个安全边界重新读取 `Current`，不能跨 publication 缓存独立 host/scene/resource reference。后台 callback 必须携带 session revision/token，并在过期时丢弃，不得继续产生 authority side effect。
- compare-exchange 冲突是 publication 前失败，candidate 仍归 transaction，coordinator 进入 rollback。
- `Revision` 只由 publisher 在成功 publication 时分配：以 `checked(expected.Revision + 1)` 生成严格单调、进程生命周期内不复用的值；溢出在 compare-exchange 前结构化失败。unpublished candidate 不得自行冒充有效 revision。
- publication 成功后 `TryCommit()` 必须返回 success；publisher 成为 candidate/current lease 的唯一 owner，transaction 立即 detach candidate。transaction 的后续 `Dispose()` 只能清理自身脚手架，不能销毁已发布 live lease。
- publisher 同时是唯一 retirement owner：成功 compare-exchange 后，它把 old state 放入自己的 retirement queue；不得把同一 old lease 同时交给 coordinator、transaction 或多个 receipt owner。
- previous state 的 dispose/unload 由 publisher 在成功 publication 后 best-effort 执行；只有已经因 revision/token 失效而停止 authority、input、audio、UI/render visibility 的 inert retired state 才允许进入该异步 cleanup。失败只进入 warning/cleanup debt，不能把已发布 candidate 伪装成 commit failure，也不能恢复旧 reference 形成双 owner。若某个旧 module/provider/scene 在 token 失效后仍能产生领域 side effect 或 player-facing output，它不是可安全 cleanup 的 lease，必须先改造，否则 publication 被阻塞。
- 未 commit 的 transaction 先由 coordinator 逐个 pump 已完成 step 的 `BeginRollback` operation；当前未成功 step 负责把自己的 partial operation drain/cleanup 到安全终态。只有这些异步补偿全部 terminal 后才能调用同步 `FinalizeRollback()`。
- `FinalizeRollback()` 只能释放剩余的内存 ownership、校验无 active lease 并汇总结果；不得新建 Unity scene/resource/I/O operation。若 concrete transaction 有异步 cleanup，必须在 plan 中暴露为显式 rollback step，否则 plan/bridge validation 失败。
- `Dispose()` 只作为 `FinalizeRollback()` 之后的幂等最终兜底，不能绕过仍在运行的 rollback operation。
- transaction `FinalizeRollback()` 与最终 `Dispose()` 幂等；重复调用不能重复释放。

`LiveApplicationState.Revision` 只用于本进程 publication conflict、driver reseed 和 stale-callback rejection。它与 Platform lifecycle revision、Runtime frame、SaveGame generation 相互独立。

### 7.3 Runtime Restore

SaveGame `PreparedSaveGame` 只恢复进 candidate Runtime registry/coordinator。任何 participant 失败时：

- forward step 失败；
- candidate 不 publish；
- rollback/dispose candidate；
- live session 不变。

这是跨 participant “应用级原子 load”的实现方式，不要求 Runtime coordinator 提供不存在的全局 rollback。

### 7.4 Runtime Frame Continuation

从存档恢复时，prepared payload 的 checkpoint frame 是完成帧 `F`。candidate transaction 必须在 publication 前计算并冻结 `NextRuntimeFrame = checked(F + 1)`；溢出结构化失败，不能 wrap。恢复、验证和 candidate preparation 期间不得执行 gameplay Tick。

一次成功 publication 把该值连同 session lease 一起写入同一个 `LiveApplicationState`。live driver 保存 `lastSeenPublicationRevision`：只有观察到新的 state revision 时，才把 `NextRuntimeFrame` 一次性装入该 session 自己的 frame cursor；重复读取同一 revision 不能 reseed。第一次固定步以精确的 `F + 1` Tick，后续由 frame cursor 正常推进到 `F + 2` 等；不能先 Tick candidate、commit 后再重复 Tick，也不能从 wall clock 或 startup pump 次数推导 frame。failed/cancelled candidate 和 retry 不推进 live frame，也不改变 driver 的 last-seen revision。

New Game 不伪造 checkpoint `F`；项目 initializer 必须显式给出并测试 `InitialRuntimeFrame`。两条路径最终都只通过 `LiveApplicationState.NextRuntimeFrame` 向 live driver 交付首帧。

## 8. Standard Startup Plan

项目可以裁剪，但推荐依赖顺序：

| Order | Step | Reused capability | Rollback / ownership |
| --- | --- | --- | --- |
| 10 | Platform Ready | long-lived PlatformBridge + environment/locations | release step-local handles only；never detach lifecycle drain owner |
| 20 | Settings Load/Apply | `IMxSettingsStore` + settings appliers | restore previous applied profile if required |
| 30 | Content/Catalog Resolve | Config/Resources/ContentPackages | release candidate catalogs |
| 40 | Localization Prepare | future Localization runtime | release candidate catalog/font handles |
| 50 | SaveGame Prepare Load | Persistence prepare-load | dispose prepared payload; no live mutation |
| 60 | ResourcePlan Preload/Acquire | Resource preload/orchestrator | release candidate plan session |
| 70 | Candidate Scene Prepare | candidate-owned SceneFlow controller/driver or prepared scene lease | unload only candidate scene |
| 80 | Runtime Session Build/Restore | RuntimeHost + SaveGame Runtime bridge | unwind/dispose candidate Host/participants |
| 90 | Candidate Lifecycle Prepare | RuntimeHost/AppFlow lifecycle；仅在满足 10 节前置条件时运行 | 完整 compensation；不得 Tick gameplay |
| 100 | Candidate Validate/Handoff Preflight | project readiness + infallible publication proof | no new publication |
| Commit | Publish | application transaction | swap live owner once |

Cold boot without save omits step 50 restore payload but still builds a candidate session. New Game uses an explicit initializer step;“找不到存档”不能静默转换为 New Game。

## 9. Scene And Resource Transaction Rules

### 9.1 Resources

`ResourcePlanOrchestrator` 已有 preload/acquire/change/release，Startup 不复制：

- preload operation progress 直接映射为 step progress；
- startup bridge 必须先取得 `ResourceManager.SealRuntimeConfiguration()` 返回的 opaque identity，再通过 `ResourcePlanOrchestrator.Prepare(identity, plan)` 创建 owner-bound strong preparation；不得把 legacy preload result 直接解释为 candidate readiness；
- strong preparation 同时绑定 exact orchestrator owner、sealed manager identity 与包含 plan id/hash、key/label 顺序和 fail-fast policy 的完整 plan fingerprint；incomplete/failed、cross-manager、cross-plan、foreign-owner 或 repeated acquire 在任何 handle ownership transfer 前 typed reject；
- `AcquirePrepared` 成功时 exact-once 把 group handles 转给独立 `ResourcePlanSession`，该 session 归 candidate transaction；`ResourcePlanSession` 只是 handle/session lease，不代表 catalog/provider 本身已经事务化；
- rollback 或放弃尚未 acquire 的 preparation 时必须调用其幂等、非阻塞 `Release()` / `Dispose()`，保留 lease 并继续 pump `Status` 从 `Releasing` 到 `Released`；`FinalizeRollback` 不得把 `Releasing` 当成完成。ownership 已转移后该调用不得反向释放 session handles；
- forward failure/cancel 时 reverse release；
- old live `ResourcePlanSession` 在 commit 前不 release；
- candidate publish 后再 release old session；
- `PrepareChange`/`CommitChange` 只用于已发布 session 的后续 plan change，不冒充 startup transaction。

资源隔离规则：

- 当 startup 会替换 catalog、provider、variant selection 或 `GlobalResourceRuntimeServices` 时，必须创建 candidate-owned `ResourceManager` 及其完整 dependency graph；它们随 candidate live lease 一起发布，旧 manager 在 publication 后才释放。
- 只有 candidate 与 live 使用完全相同且在 run 期间冻结的 catalog identity、provider identity 和 variant context 时，才允许共享现有 manager；即使共享，candidate `ResourcePlanSession` 和 handle ownership 仍必须独立。
- sealed identity 冻结后 `RegisterProvider`、`AddCatalog` 与 `SetVariantProfile` 全部 fail-closed；diagnostic fingerprint 在 seal 时预计算，steady identity reads 不重新枚举拓扑。
- preload cancellation 和 fail-fast 必须停止补位但继续 drain 所有已启动 provider operation；非协作 cancel 的 late success 逐个 exact-once release，operation 不得在仍有 in-flight ownership 时提前报告 Cancelled/Failed。
- “同一 catalog version string”不足以证明可共享；identity 至少绑定 resolved catalog/build descriptor 和 provider instance/lifetime。
- 任何读取全局静态 resource service 的 candidate module 都会破坏隔离。实现阶段必须改为构造注入 candidate manager，或把 Phase 2 标记 blocked。

### 9.2 SceneFlow

`SceneFlowController` 当前串行、busy reject、无通用 cancel，并会在 load 成功时立即更新自身 `ActiveSceneKey`。因此 load-session 不能借用 live controller 先加载 candidate；Startup bridge 必须：

- 使用 candidate-owned `SceneFlowController`/driver，或引入拥有等价隔离语义的 `PreparedSceneLease`；live controller 及其 `ActiveSceneKey` 在 commit 前保持不变；
- operation 运行期间只有 startup step pump candidate controller/lease；live controller 仍由既有 live owner 驱动，二者不是同一个实例；
- 若无法取消当前 Unity scene operation，cancel 进入“等待 safe completion 后 rollback”，不伪造立即取消；
- load-session 路径优先 additive 加载 candidate scene，commit 前不卸载旧 live scene；
- `Single` 或 `UnloadPreviousScene=true` 只允许没有可替换 live content 的首次 cold boot，并且 Boot composition root、Loading/Error UI 与 lifecycle driver 已位于 `DontDestroyOnLoad` 或等价持久 owner；统一可 retry 入口默认仍使用 Additive。其他路径只有在已有等价 rollback 证明后才能使用；
- candidate scene 中的 `Awake`/`OnEnable`、static registration、audio/UI/global service side effect 必须在 publication 前保持 transaction-owned/inert，或存在经过 fault-injection 验证的完整 compensation；“additive”本身不等于隔离；
- commit 将 candidate controller/scene lease 作为新 `LiveApplicationState` 的一部分一次性发布；startup step 随即放弃该 candidate controller 的 pump ownership，之后只由新 live owner pump。old controller 是独立实例，并在 state swap 后不再由 live driver pump；
- rollback 只卸载 candidate scene，不卸载旧 live scene。

优先的内容约束是“prepare then activate”：candidate scene 可完成 I/O、反序列化和 dependency binding，但 player-facing root 与全局 side effect 由 composition-owned、revision-aware activation gate 保持不可见；gate 的 post-publication flip 必须不可失败，并同时让 retired root 对 input/audio/UI/render 失效，实际 unload 只做后续回收。若 Unity/第三方组件只能在 scene activation/deactivation 时执行不可回滚且可能失败的全局行为，该 scene 暂不支持 production load transaction。

### 9.3 Boot Scene

Player 仍需要最小 Boot/Bootstrap scene：

- 持有唯一、persistent `ApplicationUnityBootRoot`、Platform.Unity proxy 和 typed
  Loading/Error/Retry observer；
- 不承载真实 Gameplay authority；
- content scene 通过 Resource catalog + SceneFlow 加载；
- startup failure 时 Boot shell 保持可操作，允许 retry/return；用户主动退出由项目 policy 发起，不与 Platform `Quitting` notification 混用。

Unity shell 的完整 Current contract 见 `Docs/Interfaces/ApplicationUnity.md`。它冻结：

- `Application.Unity` driver 与可选 `Application.Unity.Validation` UI Toolkit surface 分离；
- 一个 Boot-lifetime root 唯一持有 publisher/frame driver/lifecycle bridge/active run/retirement receipts；
- outer serialized composition-factory adapter 可以引用 Persistence/Session/Resources 并返回 immutable
  exact composition；Boot root 通过 PlatformBridge internal `UsesComposition` proof 校验
  publisher/fence/reporter 后才自行 claim frame driver、Bind proxy，driver asmdef 不直接引用 Persistence；
- `Update` 按 typed request -> startup -> lifecycle -> existing retirement -> dequeue -> surface
  的稳定顺序，`FixedUpdate` 只通过唯一 live frame driver Tick；
- retry 必须由 injected run factory 创建全新 coordinator/transaction/candidate graph；
- retirement failure/debt 永久占用 bounded slot，shutdown 不伪造同步 cleanup；
- 该 UI Toolkit 入口现为有生成资产和 PlayMode evidence 的 Current Validation Runtime Slice；
  它仍不属于 Playable 或正式 player-facing UI。

### 9.4 Platform Lifecycle Bridge

Platform adapter 的 lifetime 高于任何 gameplay session。Boot composition root 持有唯一 `ApplicationStartup.PlatformBridge`：

- Platform.Unity callback 只 enqueue facts；bridge 在 Application main-thread phase drain；
- bridge 不缓存某一代 RuntimeHost/scene/resource owner，而是在每次 policy dispatch 时读取 `ILiveApplicationStatePublisher.Current`；
- 当 Current 是 Boot sentinel（`HasRuntimeSession=false`）时，bridge 仍持续 drain，并处理 Boot UI、Settings 等 application-lifetime policy；它不得发起 Runtime checkpoint。需要稍后处理的 suspend evidence 以 bounded pending policy fact 保留到真实 session publication 或 startup terminal，而不是伪造 gameplay owner；
- startup/rollback 期间仍由同一 bridge drain，必要时把 suspend checkpoint 延迟到 candidate transaction 终态；
- lifecycle overflow 的 `StateResyncRequired` 及 lost suspend/quitting evidence 必须原样传给 policy，不能只看最终 snapshot；
- commit 不 handoff lifecycle queue，因此不会出现旧、新 session 同时 drain 或无人 drain 的窗口。

## 10. AppFlow And RuntimeHost Handoff

目标顺序：

1. 构造 candidate module graph 和全新 RuntimeHost；
2. Prepare/Initialize candidate services/Host，并检查最终 lifecycle state 和收集到的 errors，不能只判断“没有抛异常”；
3. 如果有 prepared save，恢复并校验 candidate participant；
4. 只有 RuntimeHost 满足下述 partial-start unwind 前置条件时，才可 Start candidate；再次检查 state/errors，失败时完整逆序补偿，retry 必须创建新 Host；
5. 不执行正常 gameplay Tick；
6. 满足下述 AppFlow 前置条件时，在 candidate 上 Start 初始 state；
7. 完成所有可能失败的 activation/handoff preflight，构造包含 exact next frame 的 immutable `LiveApplicationState`；
8. transaction commit 通过一次 compare-exchange 交换完整 live state；
9. live driver 从下一固定步开始按冻结的 pump strategy 驱动 RuntimeHost 与 standalone controllers；old live cleanup 只产生 warning。

每个 AppFlow/SceneFlow controller 在 candidate build 时必须冻结且只选择一种 pump strategy：

- `RuntimeModule`：controller 注册进该 session 的 `AppFlowRuntimeModule` / `SceneFlowRuntimeModule`，live driver 只 Tick RuntimeHost，不直接 Tick controller；
- `Standalone`：controller 不注册进 RuntimeHost，由 live driver 在声明的 main-thread stage 直接 Tick 一次；
- 同一 controller 同时注册 module 又被直接 Tick 是 plan validation error。strategy 随 session lease 发布，handoff 前后都不能切换。

### 10.1 RuntimeHost Partial-Lifecycle Unwind Prerequisite

当前 `RuntimeHost` 在 Initialize/Start 中途失败时，可能已有部分 modules 完成 Initialize 或 Start；抛异常的 module 自身也可能在 throw 前产生半完成 side effect，`CollectAndContinue` 下还会形成非连续 entered/success 集合。现有“随后调用 Dispose”不足以证明这些 module 得到逆序 Stop/Dispose、清理错误继续聚合且外部 side effect 被撤销。因此本文不把当前 Host 直接视为可回滚 candidate container。

Phase 3 前优先扩展 RuntimeHost lifecycle：

- 建立逐 module lifecycle ledger，至少记录 `InitializeEntered`、`Initialized`、`StartEntered`、`Started` 与进入顺序，而不是只记录 Host 总体 state；
- 冻结 candidate module exception-safety contract：推荐要求一旦 lifecycle callback entered，即使随后 throw，`Stop`/`Dispose` 也必须能幂等清理 partial state；无法满足时，该 callback 必须提供经过测试的 strong exception guarantee，证明 throw 前不留下外部副作用；
- Initialize/Start 任一步失败后，先按进入逆序对所有 `StartEntered` modules 尝试 Stop，再按进入逆序对所有 `InitializeEntered` modules 尝试 Dispose；单个 cleanup 失败不阻止后续 cleanup；
- 同时保留最初 forward errors 与所有 unwind errors，Host 进入不可重用 terminal fault state；
- `CollectAndContinue`、`CollectAndStopFrame` 与 `FailFast` policy 都覆盖真实 entered/success 集合，不能因异常被 catch 就跳过 state 校验；
- 测试 module 在每个 index 注入 Initialize、Start、Stop、Dispose fault，包括“先注册外部 side effect 再 throw”，证明调用次数、逆序、错误聚合和无残留 owner。

如果不修改 RuntimeHost，项目 composition adapter 必须提供并验证完全等价的 entered/completed lifecycle ledger 与 compensation。另一种可接受但更严格的方案是强制 candidate Host 使用 fail-fast，并逐 module 证明 Initialize/Start 的 strong exception guarantee；两类证据都没有时，Candidate Lifecycle Prepare 与 Phase 3 保持 blocked，不能写成“失败立即 Dispose 即安全”。

### 10.2 AppFlow And External Side Effects

`AppFlowState.Enter` 目前可能执行项目 side effect，且 `AppFlowController` 没有 Stop/Reset/rollback。框架 v0.1 不能无条件提供一个“通用可回滚 AppFlow Start step”。只有满足以下至少一项时才可纳入 candidate activation：

- 初始 state 的所有 side effect 都由 candidate transaction 拥有，Dispose/rollback 能完整撤销；
- state adapter 提供显式 transaction compensation；
- AppFlow 在后续切片增加并验证 Stop/Reset contract。

否则 AppFlow Start 保持项目组合根的未解决 publication blocker，不能把 Phase 3 标记完成。单纯“在 candidate 中调用”并不能自动回滚全局 UI、音频或第三方 side effect。

这一约束同样适用于 scene `Awake`/`OnEnable`、Runtime module Initialize/Start、SDK registration 和任何 global/static 写入：要么 side effect 只指向 candidate-owned services，要么采用可验证的 Prepare/Activate 两阶段，要么提供完整 compensation。post-swap 只允许不可失败、无分配要求已冻结的 owner visibility flip；仍需异步等待或可能报错的“activation”必须留在 swap 前。

Startup Coordinator 自身不得成为 RuntimeHost module。否则会出现：Host 尚未构建 -> Startup 需要 Host Tick -> Host 又等待 Startup 完成的循环。

## 11. Cancellation, Timeout And Rollback

### 11.1 Cancellation

```text
RequestCancel
  -> stop starting new steps
  -> request current operation cancel
  -> wait for terminal safe boundary when CanCancel=false
  -> rollback completed steps in strict reverse completion order
  -> transaction.FinalizeRollback
  -> Dispose
  -> Cancelled or RollbackFailed
```

已成功 step 和 current partially-successful step 的 compensation ownership 必须明确。不能只回滚 descriptor dependency order而忽略真实 completion order。

### 11.2 Timeout

- outer driver 把 monotonic/unscaled delta 传给 coordinator；core 不读取 `Time.time` 或 wall clock。
- timeout 是 step policy，0 表示无通用 timeout。
- deadline 到达时先 latch `TimeoutRequested` cause、停止启动新 step 并请求 inner cancel；wrapper 在不可取消 inner 尚未终结时保持非 terminal Cancelling/Running snapshot，只有到达安全终态后才发布 terminal `TimedOut` 并 rollback。
- provider operation 自己的 timeout error 保留为 inner error，coordinator 追加 step/phase context。

### 11.3 Rollback Failure

- 所有可执行 rollback 继续 best-effort，错误按逆序聚合。
- terminal state 为 `RollbackFailed`，不能简化成普通 Failed。
- Boot shell 展示可恢复操作；禁止自动开始新 run，直到 transaction 报告已隔离或项目重建 composition root。
- rollback error 不覆盖最初 forward error；两组 errors 都保留。

### 11.4 Bridge Operation Mapping

Application step 对外统一实现 `IRuntimeOperation`，但不能假设下层模块共享同一 operation shape。每个 bridge 显式映射：

| Source contract | Application mapping |
| --- | --- |
| Runtime `IRuntimeOperation` | status/progress/error 原样保留；step 只附加 phase/step context。 |
| Persistence `ISaveGameOperation<T>` | wrapper Begin 后把 Pending/Running 映射 Running，Succeeded/Failed/Cancelled 映射同名 terminal；只有 Succeeded 后读取 `Result`；summary 映射为 Runtime error，完整 `SaveGameError` 放入 bridge typed detail。 |
| Resources `IResourceOperation<T>` | `IsDone=false` 为 Running；terminal 后先检查 `IsCancelled`，再读取 `Result.Success`；不得提前读取作为 pending sentinel 的 `Result`，也不得对 `IsDone=true` 的 operation 再调用 `Cancel()`；非终态 `Cancel()` 只作为协作请求；完整 `ResourceError` 放入 bridge typed detail。 |
| SceneFlow controller/operation | Accepted/active operation 为 Running；只有 operation `IsDone` 后依据 `Success/Error` 终结；现有 contract 无 cancel，外层进入 wait-safe-completion；完整 `SceneFlowError` 放入 bridge typed detail。 |
| Platform permission operation | 若某 startup step 确实需要权限，Pending 为 Running，Succeeded/Failed/Cancelled 映射同名 terminal；`CanCancel=false` 时只放弃新工作并等待宿主终态；完整 `PlatformError` 放入 bridge typed detail。 |

outer timeout 先 latch cause，等待不可取消 inner operation 的安全终态后才把 Application wrapper 置为 `TimedOut`，并同时保留 inner structured error/status。bridge 不能把 provider error 压平成 message，也不能因为 cancel requested 就伪造 inner 已取消。

## 12. Progress And Diagnostics

forward progress：

```text
(completedWeight + currentWeight * currentStepProgress) / totalWeight
```

规则：

- clamp `[0,1]`，同一 forward run 单调不下降；
- validation、commit 可保留固定小权重，避免 step 100% 后长时间无变化；
- rollback 使用独立 `RollbackProgress`，不让总 progress 从 90% 倒退到 20%；
- Loading UI 不根据本地化字符串推断状态，只读取 stable phase/step/error code。

`ApplicationStartupSnapshot` 至少包含：

- run/plan/transaction id；
- state、current step id、completed count；
- forward/rollback progress；
- cancel requested、elapsed/step elapsed；
- forward error、rollback errors；
- current inner operation status、generic error summary 和 bridge-owned typed `InnerErrorDetail`；
- cleanup warnings；
- 不包含 payload、密钥或敏感绝对路径。

snapshot 是低频 diagnostics，`CaptureSnapshot()` 可以 `AllocByDesign`。用于每帧 Loading UI 时应提供 caller-owned DTO 或缓存 snapshot，避免每帧分配。

## 13. Error Model

建议 `ApplicationStartupErrorCode`：

| Category | Codes |
| --- | --- |
| Plan | InvalidPlan、DuplicateStep、MissingDependency、DependencyCycle、InvalidWeight |
| State | AlreadyRunning、NotRunning、TerminalRun、OwnerConflict |
| Step | StepBeginFailed、StepException、StepFailed、StepCancelled、StepTimedOut |
| Commit | CandidateValidationFailed、PublicationPreflightFailed、PublicationConflict |
| Rollback | RollbackBeginFailed、RollbackFailed、RollbackException、CleanupIncomplete |
| Control | Cancelled、Timeout |

error 保存 step id、phase、inner `RuntimeOperationError` summary、`IApplicationStartupInnerErrorDetail` 和 optional exception。成功路径不拼接 message。control flow 只依赖稳定 Application/Runtime code；bridge-specific detail 保持原结构，由对应 bridge diagnostics/UI adapter 读取，不压平为单字符串。

## 14. Allocation And Threading Contract

- plan construction/validation、run creation、Begin、phase transition diagnostics：`LowFreqAlloc` / `AllocByDesign`。
- coordinator steady `Tick`、current operation poll、progress getter：初始化后 `NoAlloc`。
- step 不得在 worker thread 调用 Unity API、`RuntimeCheckpointBarrier` 或 Runtime participant。
- immutable SaveGame encode/store、checksum 和 provider I/O 可以后台执行；完成结果回到 application driver。
- Unity scene/resource operations由主线程 pump。
- 不在 startup Tick 使用 LINQ、反射、临时集合、字符串拼接或每帧 GameObject 查找。

虽然 startup 是低频流程，但 operation 常被每帧轮询，steady poll 仍按高频路径标准处理。

## 15. Test Strategy

### 15.1 noEngine Coordinator

| Test ID | Scenario | Required assertion |
| --- | --- | --- |
| `APP-PLAN-01` | duplicate/missing/self dependency | no step begun, structured validation error |
| `APP-PLAN-02` | cycle | exact cycle participants reported |
| `APP-ORD-01` | registration permutation | same stable topological execution order |
| `APP-RUN-01` | all steps succeed | commit exactly once, Succeeded terminal |
| `APP-RUN-02` | middle step fails | later steps never begin, reverse rollback |
| `APP-RUN-03` | current step fails after partial async side effect | it drains self-owned partial state before terminal; only then prior steps roll back |
| `APP-CAN-01` | cancellable current step | no new step, reverse rollback, Cancelled |
| `APP-CAN-02` | non-cancellable current step | waits safe terminal then rollback |
| `APP-TIME-01` | step timeout | inner error preserved, rollback occurs |
| `APP-TIME-02` | timed-out non-cancellable inner later succeeds/fails | wrapper stays non-terminal/draining; no rollback runs concurrently; final TimedOut preserves inner terminal |
| `APP-RBK-01` | rollback failures | all rollback attempted, forward + rollback errors preserved |
| `APP-RBK-02` | transaction owns async cleanup not exposed by a step | plan/bridge validation rejects; `FinalizeRollback` never starts async work |
| `APP-RET-01` | retry | entirely new run/transaction, no old operation reuse |
| `APP-PRG-01` | weighted progress | monotonic/clamped, rollback progress separate |
| `APP-GC-01` | 1000 steady polls | 0 B after warmup outside transitions |

### 15.2 Integration

| Test ID | Scenario | Required assertion |
| --- | --- | --- |
| `APP-SAVE-01` | incompatible/corrupt save | no candidate restore/publication |
| `APP-SAVE-02` | participant restore failure | live session unchanged, candidate disposed |
| `APP-RES-01` | preload failure/cancel | candidate handles released, old plan retained |
| `APP-RES-02` | catalog/provider changes | candidate manager isolated; live global services unchanged before commit |
| `APP-RES-03` | frozen identical catalog/provider sharing | separate plan lease; candidate rollback does not release live handles |
| `APP-SCN-01` | candidate scene load failure | old scene retained, Boot shell usable |
| `APP-SCN-02` | cancel unsupported by driver | waits completion, unloads candidate, no double Tick |
| `APP-SCN-03` | candidate scene `Awake`/activation fault | no live/global side effect leaks; otherwise plan rejected as unsupported |
| `APP-SCN-04` | cold-boot Single load fails | persistent Boot composition root and Error UI remain alive and retryable |
| `APP-HOF-01` | candidate SceneFlow publication | live controller unchanged before commit; new controller has exactly one pump owner after commit |
| `APP-HOF-02` | RuntimeHost handoff | no gameplay Tick before publish; restored session starts exactly F+1 once |
| `APP-HOF-03` | AppFlow/SceneFlow module vs standalone strategy | each controller ticks exactly once per declared step; double registration/direct pump rejected before side effects |
| `APP-HOST-01` | RuntimeHost Initialize/Start fault at every module index/policy, including side effect before throw | exact entered/success ledger unwound in reverse entry order; forward + cleanup errors preserved; retry uses new Host |
| `APP-HOST-02` | Stop/Dispose faults during unwind | remaining cleanup continues; no residual published/global owner |
| `APP-FLOW-01` | AppFlow Enter throws with candidate-safe compensation | candidate discarded, live/global side effects unchanged |
| `APP-FLOW-02` | AppFlow lacks compensation | plan rejected or Phase 3 remains blocked; no false rollback claim |
| `APP-COM-00` | first cold-boot publication | non-null Boot sentinel revision 0 CASes once to real session revision 1; no Runtime checkpoint targets sentinel |
| `APP-COM-01` | commit publication conflict | zero live fields change; candidate remains rollback-owned |
| `APP-COM-02` | successful compare-exchange then old cleanup/unload fails | new state remains live; retired state stays inert with no input/audio/UI/render output; publisher records warning, no rollback |
| `APP-COM-03` | transaction Dispose after success | publisher solely owns current/retired leases; live lease remains valid and each lease is disposed exactly once |
| `APP-COM-04` | stale old-session callback after publication | revision/token rejected; no old authority side effect reaches live state |
| `APP-FRM-01` | prepared checkpoint frame F | new revision seeds once: first ticks F+1 then F+2; rereading same revision/retry does not reseed or advance live frame |
| `APP-PLAT-01` | lifecycle edge during startup/commit | one bridge drains once and dispatches against current published state |
| `APP-OP-01` | each bridge success/failure/cancel/timeout | result read only when valid; inner structured error preserved |

### 15.3 Unity Gate

- Unity refresh/compile 和 Console 无 error；
- focused EditMode plan/bridge tests；
- PlayMode cold boot success；
- PlayMode preload/scene/save restore 故障入口；
- cancel/retry 后无重复 host、重复 scene owner 或 leaked resource handle；
- Boot shell 始终能显示 stable error 并重新开始新 run。

## 16. Phased Delivery

| Phase | Deliverable | Minimum gate |
| --- | --- | --- |
| 0 Design | 本文 + 待接受 ADR | owner、transaction、cancel/rollback/handoff 无歧义 |
| 1 noEngine Coordinator | Implemented: IDs、plan validator、sequential runner、progress、rollback、transaction/publication contracts | focused `ApplicationStartupPlanTests` / `ApplicationStartupCoordinatorTests`; Unity EditMode gate remains required before merge |
| 2 Resource/Scene Bridges | candidate ResourceManager/catalog policy、candidate SceneFlow/prepared scene lease、side-effect gate | `APP-RES-*`、`APP-SCN-*`、`APP-HOF-01` |
| 3 Persistence/Runtime Bridge | prepared load、candidate restore、RuntimeHost partial-lifecycle unwind、single-state publication、exact frame continuation、AppFlow compensation decision | `APP-SAVE-*`、`APP-HOF-02`、`APP-HOST-*`、`APP-FLOW-*`、`APP-COM-*`、`APP-FRM-01` |
| 4 Unity Boot Shell | Implemented in `Interfaces/ApplicationUnity.md`: driver/root/typed Validation surface, Unity-generated Boot assets, long-lived PlatformBridge, zero-checkpoint Boot sentinel, exact-session checkpoint, and failure/cancel/rollback/fresh-retry PlayMode slice | Unity compile/Console + focused EditMode/PlayMode + single drain owner + warm allocation evidence |
| 5 Suspend/Resume Policy | Implemented: long-lived PlatformBridge + exact-session safe checkpoint/save policy | lifecycle ordering、overflow fail-safe、quitting best-effort evidence、`APP-PLAT-01` |
| 6 Product Extensions | network login/matchmaking/patching as optional steps | provider-specific rollback and real environment evidence |

Phase 1-3 是 framework capability；其中 RuntimeHost partial-lifecycle unwind、candidate scene/resource isolation、single-state publication 或 AppFlow compensation 任一证据缺失时，Phase 3 不得标记完成，也不得宣称 production-safe atomic load。Phase 4 完成前不能宣称有可打开、可操作的统一 startup entry。Phase 5 不把 `OnApplicationQuit` 当作唯一保存时机。

## 17. ADR Decisions Required Before Implementation

1. 接受 Application Startup 是 RuntimeHost 外层，不注册为 Runtime module。
2. 接受 v0.1 DAG 串行执行，稳定 tie-break，不默认跨 step 并发。
3. 接受所有 load/build 在 candidate transaction 中完成，最后只有一个 commit publication 点。
4. 接受每个 SceneFlow/RuntimeHost 实例在任何时刻只有一个 pump owner，candidate 与 live 使用隔离实例，并在 publication 后显式确定新 live owner。
5. 接受 load-session 场景默认 additive candidate，commit 前保留旧 live scene/resource plan。
6. 接受 cancel 不能强杀不可取消 operation，只能等 safe boundary 后 rollback。
7. 接受 retry 创建全新 run/transaction，不复用 failed/cancelled operation。
8. 接受 live host/scene/resource/next-frame 只通过一个 immutable `LiveApplicationState` 引用原子发布。
9. 接受 RuntimeHost entered-module lifecycle unwind、scene/module side-effect isolation 和 AppFlow compensation 是 Phase 3 前置条件，不以普通 Dispose 代替证明。
10. 接受 publisher 是 current/retired live leases 的唯一 owner，transaction commit 后 detach；所有异步 compensation 属于显式 step rollback operation。
11. 接受非 null Boot sentinel 作为首次 expected state，并为每个 AppFlow/SceneFlow controller 冻结互斥的 RuntimeModule/Standalone pump strategy。

这些决策已由 owner 接受并冻结在 `Decisions/ADR-011-application-startup-authority.md`。本文继续作为分阶段实现设计；RuntimeHost hardening、candidate isolation、single-state publication 和 Unity Boot 仍分别受实现证据门禁约束。

## 18. Design Acceptance Invariants

以下条目是实现与复审持续检查的 invariant，不是未完成进度表；Phase 1-5 的交付状态以第 16 节和
各 Current 接口文档的可运行证据为准。

- [ ] Startup 没有复制 AppFlow、SceneFlow、ResourcePlan、Persistence 或 RuntimeHost。
- [ ] Startup Coordinator 位于 RuntimeHost 外层。
- [ ] plan 在副作用前完成 duplicate/missing/cycle validation。
- [ ] v0.1 顺序由 dependency、order、step id 唯一确定。
- [ ] 全部 forward step 成功后才 commit candidate。
- [ ] save restore 失败不会污染 live session。
- [ ] old scene/resource/session 在 commit 前保持有效。
- [ ] catalog/provider 变化时使用 candidate manager；共享时有严格 identity proof 和独立 plan lease。
- [ ] candidate scene 不借用 live controller，scene/module/AppFlow side effect 可隔离或完整补偿。
- [ ] RuntimeHost partial Initialize/Start failure 会按进入逆序解卷所有 entered modules（包括 throw 前半完成者），并保留 cleanup errors。
- [ ] live host/scene/resource/next-frame 只通过一次 immutable state compare-exchange 发布。
- [ ] publisher 从非 null Boot sentinel revision 0 开始；首次真实 session revision 1，Boot lifecycle policy 不访问 Runtime。
- [ ] publisher 唯一拥有 current/retired leases；publication 后 transaction Dispose 不会释放 live lease，old cleanup failure 只产生 warning。
- [ ] SceneFlow/RuntimeHost handoff 前后都只有一个对应实例的 pump owner。
- [ ] AppFlow/SceneFlow 的 RuntimeModule/Standalone pump strategy 冻结且互斥，每固定步恰好一次 Tick。
- [ ] 新 publication revision 只 seed frame cursor 一次；load continuation 精确从 checkpoint `F + 1` 开始，candidate/retry 不提前推进。
- [ ] Platform lifecycle queue 始终只有一个 long-lived bridge drain owner。
- [ ] cancellation、timeout、rollback failure 和 retry 语义完整。
- [ ] 所有异步 compensation 由 step rollback operation pump；`FinalizeRollback()` 只做同步兜底。
- [ ] bridge-specific typed error detail 可追踪，generic coordinator 不丢失领域错误。
- [ ] Loading UI 只观察 snapshot，不拥有 transaction。
- [ ] steady operation poll 满足 NoAlloc after initialization。

## References

- `ARCHITECTURE.md`
- `API_STANDARDS.md`
- `Interfaces/Runtime.md`
- `Interfaces/AppFlow.md`
- `Interfaces/Resources.md`
- `Interfaces/Settings.md`
- `RUNTIME_FOUNDATION_SYSTEM.md`
- `RESOURCE_SYSTEM_WORKFLOW.md`
- `PLATFORM_CONTRACTS_DESIGN.md`
- `SAVEGAME_PERSISTENCE_DESIGN.md`
- `Packages/com.vincent.wgameframework/Runtime/Operations/RuntimeOperation.cs`
- `Packages/com.vincent.wgameframework/Runtime/AppFlow.cs`
- `Packages/com.vincent.wgameframework/Runtime/SceneFlow.cs`
- `Packages/com.vincent.wgameframework/Resources/ResourcePreloadService.cs`
- `Packages/com.vincent.wgameframework/Resources/ResourcePlanOrchestrator.cs`
