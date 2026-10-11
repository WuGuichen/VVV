# Platform Contracts 基础平台契约设计

> Status: Design
>
> Version: 0.2.0
>
> Date: 2026-07-19
>
> Implementation status: Architecture accepted by `Decisions/ADR-009-platform-contract-authority.md`; Phase 1 Platform Core and Phase 2 `MxFramework.Platform.Unity` environment/location/lifecycle/microphone-permission adapters with focused tests are implemented. Unity validation is required before delivery; consumer migration remains unimplemented.

## 1. Context

MxFramework 已经有稳定的 noEngine Runtime、Settings、Resources、Input、Audio 和 VoiceInput 边界，但宿主平台信息仍由各 Unity adapter、Demo 和项目组合根直接读取：

- `Application.persistentDataPath` 被校准、网络自动化、trace 和验证报告分别使用；
- `Application.streamingAssetsPath` 被 Resources、Audio、Story 和 Character runtime 分别解释；
- `Application.platform` 被 Audio / Resources adapter 直接用于平台分支；
- `OnApplicationFocus` / `OnApplicationQuit` 目前只在 CharacterTest 局部控制器中处理；
- 麦克风权限由 `VoiceInput.Unity` 内部 gateway 单独访问 Unity authorization API；
- Settings 文件路径由组合根传入，框架没有统一的可写路径与生命周期契约。

这些局部调用本身并非错误，但当 SaveGame、暂停 checkpoint、平台权限和启动编排开始复用时，会产生多个相互不一致的“平台真相”。本文设计 `MxFramework.Platform`，只统一跨项目都需要的宿主事实，不把商店、账号、云服务或 Unity API 带入 noEngine 模块。

本文是目标设计，不是当前能力说明。当前事实仍以 `INTERFACES.md`、`Interfaces/*.md`、源码和测试为准。

## 2. Goals And Non-Goals

目标：

- 为可写数据、临时缓存和 StreamingAssets 提供可测试、可替换的平台位置契约。
- 把 focus、pause/suspend、resume 和不可 veto 的 quitting notification 归一为低频生命周期事实。
- 为权限查询与请求提供 typed、可轮询的异步边界。
- 提供只用于非权威元数据的 UTC wall clock。
- 让 Unity adapter 成为 `UnityEngine.Application` 的唯一通用运行时入口。
- 支持桌面、移动、WebGL、server/headless 和 noEngine 测试替身。
- 所有可恢复失败返回结构化结果；正常平台差异不依赖异常作流程控制。

非目标：

- 不提供 Steam、Epic、PlayStation、Xbox、Apple、Google 等商店 SDK 的统一最小公分母。
- 不承载账号登录、成就、排行榜、云存档、支付、联机认证或 Analytics。
- 不把设备时间、focus 或 pause 事件写入 Runtime authority、Replay、Runtime hash 或 Gameplay SaveState。
- 不用 Platform 替代 Input、VoiceInput、Audio、Resources、Settings 或 Persistence 的领域服务。
- 不把 `Application.dataPath` 或 Unity project root 暴露给 player runtime；Editor 工具继续使用 Editor-owned 路径工具。
- 不承诺所有 StreamingAssets 都是本地文件；Android/WebGL 可以是 URI 或 provider-owned location。
- 不在 v0.1 提供任意字符串 capability bag 或反射式 service locator。

## 3. Source Of Truth And Ownership

| 数据 / 行为 | 唯一 owner | Platform 的职责 |
| --- | --- | --- |
| Runtime frame、delta、deterministic clock | `MxFramework.Runtime` / 外层驱动 | 不读取、不替换，不向 authority 提供 wall clock |
| Writable data root、temporary cache、streaming content location | Platform adapter | 归一位置类型、访问语义和可用性 |
| Focus、suspend、resume、quitting notification | Platform adapter | 去重并发布 lifecycle revision |
| Permission status / request | Platform adapter | typed 查询、异步完成和结构化失败 |
| Save slot、文件布局、原子提交、恢复 | `MxFramework.Persistence` | 只消费 writable root 或注入的 store |
| Settings profile | `MxFramework.Settings` | 继续拥有 schema、default 和 apply 行为 |
| Resource key、catalog、remote cache | `MxFramework.Resources` | 只消费 streaming/cache location adapter |
| 麦克风设备和录音 session | `MxFramework.VoiceInput` | 可复用通用 permission service，但设备能力仍归 VoiceInput |
| 游戏暂停规则、自动保存策略 | Application / 项目组合根 | 观察 lifecycle，不由 Platform 直接执行 |
| 本地时间显示、时区、文化格式 | Localization / presentation | Platform v0.1 只提供 UTC wall clock，不提供显示规则 |

核心规则：Platform 只陈述“宿主发生了什么、可从哪里访问”，不决定“游戏应该怎么做”。

## 4. Target Assemblies And Dependency Direction

```text
BCL
  -> MxFramework.Platform                    (zero framework dependencies, noEngine contracts)

MxFramework.Platform + UnityEngine
  -> MxFramework.Platform.Unity              (Unity Application/lifecycle adapter)

MxFramework.Platform + target SDK
  -> MxFramework.Platform.<Provider>          (optional provider adapter)

MxFramework.Platform + MxFramework.Application
  -> MxFramework.ApplicationStartup.PlatformBridge   (public namespace; long-lived consumer; noEngine)

MxFramework.Platform
  -> MxFramework.Persistence.FileSystem       (consumer; one-way)
  -> project composition root                 (consumer; one-way)
```

目标 asmdef：

| Assembly | 允许依赖 | 禁止依赖 |
| --- | --- | --- |
| `MxFramework.Platform` | BCL | Core、UnityEngine、Runtime、Gameplay、Settings、Resources、Persistence |
| `MxFramework.Platform.Unity` | Platform、UnityEngine | UnityEditor、Gameplay、具体项目代码 |
| `MxFramework.Platform.<Provider>` | Platform、对应平台 SDK | 其他无关平台 SDK、项目业务 |

`MxFramework.Platform` 保持 `noEngineReferences=true`。Unity 生命周期通过一个 composition-owned `MonoBehaviour` 喂入 adapter；Platform core 不查找 GameObject、不创建全局 singleton。

## 5. Contract Candidates

以下类型名是 v0.1 候选契约，只有实现后写入 `INTERFACES.md` 和 `Interfaces/Platform.md` 才成为公共 API。

### 5.1 Platform Identity

```csharp
public enum PlatformFamily
{
    Unknown = 0,
    Windows = 1,
    MacOS = 2,
    Linux = 3,
    Android = 4,
    IOS = 5,
    Web = 6,
    Console = 7
}

public readonly struct PlatformEnvironment
{
    public PlatformFamily Family { get; }
    public bool IsEditor { get; }
    public bool IsHeadless { get; }
    public string RuntimeName { get; }
}

public interface IPlatformEnvironmentProvider
{
    PlatformEnvironment Current { get; }
}
```

`RuntimeName` 只用于 diagnostics，不参与规则、排序、hash 或 SaveState 兼容判断。需要精确能力时使用领域 adapter 的显式契约，不解析 `RuntimeName`。

### 5.2 Platform Locations

```csharp
public enum PlatformLocationKind
{
    Unknown = 0,
    FileSystemPath = 1,
    Uri = 2,
    ProviderOwned = 3
}

public enum PlatformLocationId
{
    Unknown = 0,
    PersistentData = 1,
    TemporaryCache = 2,
    StreamingContent = 3
}

public readonly struct PlatformLocation
{
    public PlatformLocationKind Kind { get; }
    public string Value { get; }
    public bool IsReadable { get; }
    public bool IsWritable { get; }
    public bool IntendedForPersistentData { get; }
}

public readonly struct PlatformLocationResult
{
    public bool Success { get; }
    public PlatformLocation Location { get; }
    public PlatformError Error { get; }
}

public interface IPlatformLocations
{
    PlatformLocationResult Resolve(PlatformLocationId id);
}
```

位置规则：

- `PersistentData` 必须表示适合用户数据的逻辑根，但不自动承诺原子 replace、quota 或 cloud sync。
- `TemporaryCache` 可被宿主清理，不能保存唯一存档副本。
- `StreamingContent` 可能是 URI；consumer 不得无条件调用 `System.IO.Path.Combine` / `File.ReadAllText`。
- `IntendedForPersistentData` 只表示位置用途，不等于 store 已证明 flush/rename/transaction 语义。
- `FileSystemPath` 的 value 必须是非空绝对路径；`Uri` 必须是可解析的绝对 URI；`ProviderOwned` 是非空 opaque locator，consumer 不得自行拆解。
- unknown id、default location、空 value 或 adapter failure 返回结构化失败；default enum 不得被解释成真实路径。
- 返回值只提供根位置，不接受未校验的用户输入作为相对路径。
- 路径规范化、slot 名称转义和目录布局归 Persistence / Resources owner，而不是 Platform。
- `Persistence.FileSystem` 只接受 `FileSystemPath + IsWritable`；URI/ProviderOwned 必须交给独立 store adapter，并继续验证原子 commit capability。

### 5.3 Application Lifecycle

Focus 和 suspend 是正交事实，不能折叠成一个 `Paused` bool：桌面失焦不一定暂停，移动端 pause 也可能伴随多次 focus callback。

```csharp
public readonly struct ApplicationLifecycleSnapshot
{
    public bool HasFocus { get; }
    public bool IsSuspended { get; }
    public bool IsQuitting { get; }
    public long Revision { get; }
}

public enum ApplicationLifecycleChangeKind
{
    Unknown = 0,
    FocusChanged = 1,
    SuspendChanged = 2,
    Quitting = 3,
    StateResyncRequired = 4
}

public readonly struct ApplicationLifecycleChange
{
    public ApplicationLifecycleChangeKind Kind { get; }
    public ApplicationLifecycleSnapshot Snapshot { get; }
    public long LostRevisionStart { get; }
    public long LostRevisionEnd { get; }
    public long LostChangeCount { get; }
    public bool SawSuspendInLostRange { get; }
    public bool SawQuittingInLostRange { get; }
}

public interface IApplicationLifecycleSource
{
    ApplicationLifecycleSnapshot Current { get; }
    long DroppedChangeCount { get; }
    bool TryDequeue(out ApplicationLifecycleChange change);
}
```

语义：

- adapter 对相同状态的重复 Unity callback 去重；只有有效变化增加 `Revision`。
- default/`Unknown` change 不可执行 policy，只能作为 invalid input 结构化拒绝；真实 adapter 不入队 Unknown。
- `Revision` 是本地诊断顺序，不跨进程、不进 Replay、不作为 Runtime frame。
- Unity callback 只更新 snapshot 并写入预分配 bounded queue；长生命周期 `ApplicationStartup.PlatformBridge` 是唯一 drain owner，Application driver 只在自己的安全阶段驱动 bridge 执行 policy，再向其他领域 fanout，避免 callback 重入保存、切场景或 Runtime 操作。
- focus/suspend/quitting 的有效 edge 按 callback 接受顺序入队，因此一帧内 suspend -> resume 仍可被 Application 看见。
- queue overflow 不静默继续：adapter 把 resync metadata 保存在 queue 外的 sticky slot，清空已无法证明连续性的 queued deltas，并在 resync pending 期间只更新 latest snapshot、lost revision range 和 `SawSuspendInLostRange` / `SawQuittingInLostRange`。lost range 从最早被清空的 queued revision 开始，覆盖之后所有被折叠的有效 edge；下一次 `TryDequeue` 必须先返回且只返回一个覆盖该完整区间的 `StateResyncRequired`，之后才恢复普通 edge enqueue。
- `Revision` 在 `long.MaxValue` 饱和且永不 wrap；耗尽后的 effective change 仍更新 latest snapshot，但不能生成重复 revision 的普通 edge，必须连同已有 queued edge 折叠为 sticky `StateResyncRequired`，以 lost count 表达同一饱和 revision 下的后续变化。
- `DroppedChangeCount` 与 `LostChangeCount` 为饱和 `long` counter，不允许 wrap 成负数；resync 被成功 dequeue 后只清除本次 lost-range metadata，不清除累计 dropped count。
- consumer 收到带 suspend/quitting 证据的 resync 时必须执行一次保守 safe-checkpoint/save policy，再按最终 snapshot 对齐；不能因最终 `IsSuspended=false` 就忽略丢失的后台 edge。
- `IsQuitting` 一旦置位即保持 sticky，直到 Platform adapter/application lifetime 结束，避免 gameplay session replacement 让 drain owner 错过事实。
- v0.1 的 `Quitting` 只映射不可 veto 的 `OnApplicationQuit()` 通知。它不是 user request，也不提供取消退出；若未来接入 `Application.wantsToQuit`，必须另建 `QuitRequested` 契约并冻结 veto/clearing 语义。

### 5.4 Permission Boundary

```csharp
public readonly struct PlatformPermissionId { /* stable value */ }

public enum PlatformPermissionStatus
{
    Unknown = 0,
    Unsupported = 1,
    NotRequired = 2,
    NotDetermined = 3,
    Granted = 4,
    Denied = 5,
    Restricted = 6
}

public enum PlatformPermissionOperationStatus
{
    Unknown = 0,
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}

public readonly struct PlatformPermissionQueryResult
{
    public bool Success { get; }
    public PlatformPermissionStatus Status { get; }
    public PlatformError Error { get; }
}

public interface IPlatformPermissionOperation
{
    PlatformPermissionOperationStatus OperationStatus { get; }
    bool IsDone { get; }
    bool CanCancel { get; }
    PlatformPermissionStatus PermissionStatus { get; }
    PlatformError Error { get; }
    void RequestCancel();
}

public interface IPlatformPermissionService
{
    PlatformPermissionQueryResult Query(PlatformPermissionId permissionId);
    IPlatformPermissionOperation Request(PlatformPermissionId permissionId);
}
```

内置 ID 只覆盖框架实际消费的权限，例如 microphone；camera/location 等在有对应模块前不提前冻结。VoiceInput 的现有 Unity microphone gateway 可以逐步适配，v0.1 不强制一次性迁移所有 provider。

Permission operation 规则：

- `Query.Success=true` 表示取得了可信宿主事实；`Unsupported` 也是成功 query 的明确状态。invalid ID 或 adapter failure 返回 `Success=false`、`Status=Unknown` 和结构化 error。
- `Request` 永不返回 null；invalid/unsupported/busy 返回 completed failed operation。
- 已是 `Granted` / `NotRequired` 时返回 completed succeeded operation。只有终态 `Granted` / `NotRequired` 的 request 为 `Succeeded`；Denied/Restricted 保留最终 `PermissionStatus` 并以对应 error 进入 `Failed`。
- `Error` 仅在 operation `Failed` 时有值。`Cancelled` 时 Error=None，operation 的 `PermissionStatus` 不作为新的宿主事实；caller 如需后续事实必须重新 `Query`。
- v0.1 service 同时最多拥有一个 OS permission request；第二个相同或不同 permission request 都返回 `Busy`，避免共享 operation 被单个 caller cancel。
- `RequestCancel` 是协作式放弃等待：`CanCancel=false` 时不能关闭系统弹窗，adapter 等待宿主终态但允许 caller 丢弃迟到结果；只有 provider 确认取消后 operation 才进入 `Cancelled`。
- cancel 未被 provider 确认时，迟到的 Granted/NotRequired 仍进入 Succeeded，Denied/Restricted 仍进入 Failed；不能仅因 caller 曾请求 cancel 就覆盖真实宿主终态。
- OS 在 caller 放弃后仍可能改变最终 permission status；后续 `Query` 返回真实宿主状态，不能被旧 operation 的 Cancelled 结果覆盖。
- query/request 和 Unity authorization API 只能在声明的主线程调用；adapter failure 使用结构化 error。

### 5.5 Wall Clock

```csharp
public interface IWallClock
{
    DateTime UtcNow { get; }
}
```

`IWallClock` 只用于存档展示时间、日志和过期缓存等低频非权威数据。Gameplay、Runtime timer、cooldown、Replay 和 network simulation 禁止读取它作规则输入。

provider 必须返回 `DateTimeKind.Utc`；Local/Unspecified 视为 adapter contract failure，不能在 Persistence 内猜测时区。wall clock 允许校时后回拨，因此排序、并发和 generation 规则不得依赖它。

## 6. Unity Adapter Rules

`MxFramework.Platform.Unity` 应满足：

1. 组合根显式创建并持有 adapter；不使用隐藏的 `DontDestroyOnLoad` singleton。
2. `OnApplicationFocus(bool)`、`OnApplicationPause(bool)`、`OnApplicationQuit()` 只转换为 Platform facts。
3. 不在 callback 内直接保存文件、暂停 RuntimeHost、切场景或调用 UI。
4. `Application.streamingAssetsPath` 先转换为 `PlatformLocation`，保留 URI 语义。
5. Unity `RuntimePlatform` 到 `PlatformFamily` 的映射必须显式覆盖 Unknown/default。
6. Editor project root 属于 Editor tooling，不通过 player-facing `IPlatformLocations` 暴露。
7. 测试替身可以直接注入 lifecycle change；测试不依赖真实窗口 focus。

## 7. Lifecycle Consumption Policy

推荐消费链：

```text
Unity callback
  -> Platform.Unity normalization
  -> bounded lifecycle change queue + current snapshot
  -> Application driver invokes long-lived ApplicationStartup.PlatformBridge
  -> PlatformBridge is the sole drain owner
  -> Application/session policy
       -> input focus policy
       -> audio pause policy
       -> next safe Runtime checkpoint request
       -> persistence save request
```

必须保持：

- Platform 不直接触碰 `RuntimeHost`。
- suspend 期间何时停止输入、是否继续网络、是否自动保存由项目/session policy 决定。
- 自动保存只能在 `RuntimeCheckpointBarrier` 报告最近 Tick 完成且 participant quiescent 时捕获。
- quitting 路径不得假设还有足够时间完成磁盘或 cloud I/O；关键状态应在更早的 safe point 保存。

## 8. Error, Diagnostics And Allocation Contract

建议 `PlatformErrorCode` 至少区分：

- `Unsupported`
- `Unavailable`
- `InvalidLocation`
- `PermissionDenied`
- `PermissionRestricted`
- `RequestFailed`
- `Cancelled`
- `AdapterFailure`

要求：

- 正常“不支持”返回结构化结果，不抛异常。
- adapter 抛出的宿主异常可附在低频 error object 中，但不能跨 network / SaveState。
- lifecycle current snapshot getter、adapter enqueue 和 Application drain 为 `NoAlloc` after initialization。
- permission request 和 location normalization 为 `LowFreqAlloc`。
- `PlatformEnvironment`、location 和 permission status 只进入 diagnostics snapshot，不进入 Runtime authority hash。

## 9. Test Strategy

| Test ID | 场景 | 断言 |
| --- | --- | --- |
| `PLAT-LOC-01` | file / URI / provider-owned location | kind、access、intent 和 value 不丢失 |
| `PLAT-LOC-02` | StreamingAssets URI | consumer 不当作本地文件拼接 |
| `PLAT-LOC-03` | default/invalid location + FileSystem store preflight | structured failure；URI/provider-owned 被 FileSystem adapter 拒绝 |
| `PLAT-LIFE-01` | focus 重复 callback | snapshot 不变，revision 只增一次 |
| `PLAT-LIFE-02` | focus 与 suspend 交错 | 两个事实独立、edge 顺序稳定 |
| `PLAT-LIFE-03` | quitting notification | sticky fact 可 drain，不冒充可 veto request，不直接触发领域动作 |
| `PLAT-LIFE-04` | 一帧内 suspend -> resume | 两个 edge 均可 drain，最终 snapshot 正确 |
| `PLAT-LIFE-05` | overflow 中 suspend -> resume / quitting | 首个 dequeue 是覆盖完整折叠区间的单一 resync；lost range/flags 保留，Application 执行保守 checkpoint |
| `PLAT-LIFE-06` | revision 已达 `long.MaxValue` 后继续 effective change | latest snapshot 更新；已有 queued edge 与新变化折叠为 resync，不产生相同 revision 的普通 edge，也不 wrap |
| `PLAT-PERM-01` | unsupported / granted / denied / restricted / invalid ID | unsupported 是成功 query fact；invalid/adapter failure 为 Unknown + error；request terminal 稳定 |
| `PLAT-PERM-02` | cancellable / non-cancellable OS request | 协作式取消和迟到宿主结果语义稳定 |
| `PLAT-PERM-03` | concurrent request | 第二个 request completed Busy，不共享 cancellation ownership |
| `PLAT-CLK-01` | fake UTC clock / clock rollback / invalid DateTimeKind | UTC 元数据可测试；rollback 不影响 generation/order；invalid kind fail-closed；不影响 Runtime hash |
| `PLAT-GC-01` | lifecycle enqueue/drain | warmup 后 adapter update、enqueue、drain 0 B |
| `PLAT-UNITY-01` | Unity EditMode mapping | `RuntimePlatform` 与 path kind 映射正确 |
| `PLAT-UNITY-02` | focused PlayMode smoke | focus/pause/quit proxy 无 Console error |

noEngine tests 不引用 Unity。Unity callback smoke 只验证 adapter，不要求真实操作系统权限弹窗自动化。

## 10. Phased Delivery

| Phase | Deliverable | Minimum gate |
| --- | --- | --- |
| 0 Design | 本文与待接受 ADR | owner、非目标、location/lifecycle 语义无歧义 |
| 1 noEngine Contracts | Platform identity、locations、lifecycle、permission、wall clock + fakes | `PLAT-LOC-*`、`PLAT-LIFE-*`、`PLAT-PERM-*`、`PLAT-CLK-01` |
| 2 Unity Adapter | Unity environment/location/lifecycle/permission adapters | Unity compile、focused EditMode、`PLAT-UNITY-*` |
| 3 First Consumers | Persistence path、VoiceInput permission、Application lifecycle policy adapters | 不产生反向依赖；现有 consumer 行为保持 |
| 4 Platform Providers | 仅按产品需要增加 console/store/cloud provider | provider-specific contract 和真机证据 |

实现公共 API 前必须重新读取 `API_STANDARDS.md`、`QUALITY_GATE.md`，新增 `Interfaces/Platform.md` 并更新 `INTERFACES.md`、`CAPABILITIES.md` 和 `USAGE.md`。新增 Unity C# 属于至少 V2；真实生命周期/路径适配通常按 V3 验证。

## 11. ADR Decisions Required Before Implementation

1. 接受 Platform 只陈述宿主事实，不执行保存、暂停、场景或 UI 策略。
2. 接受 focus 与 suspend 使用正交状态，不折叠成单一 pause bool。
3. 接受 StreamingAssets location 可以不是文件路径。
4. 接受 wall clock 是非权威服务，禁止进入 deterministic gameplay。
5. 接受 Platform core noEngine，所有 Unity / provider SDK 位于单向 adapter。
6. 决定 `PlatformPermissionId` 的稳定表示和首批内置 ID。

这些决策已由 owner 接受并冻结在 `Decisions/ADR-009-platform-contract-authority.md`。本文继续作为分阶段实现设计；能力状态仍以实际代码、测试和接口文档为准。

## 12. Design Acceptance Checklist

- [x] Platform core 保持 noEngine 且不依赖 Runtime / Gameplay。
- [x] Unity environment/location/lifecycle/authorization 通用读取与 callback normalization 已集中到 Platform.Unity adapter；consumer 迁移仍按领域分阶段执行。
- [x] Persistent、temporary、streaming location 的语义区分清楚。
- [x] focus、suspend、quitting callback 只 report facts，不直接执行领域动作，request/quitting 没有混用。
- [x] wall clock 明确排除 Runtime authority / Replay / hash。
- [x] permission query/request 有独立 operation 终态、协作式取消、Busy 和结构化失败。
- [ ] SaveGame、Resources、VoiceInput 只通过单向 adapter 消费。
- [ ] 商店、账号、cloud、Analytics 保持非目标。

## References

- `ARCHITECTURE.md`
- `API_STANDARDS.md`
- `INTERFACES.md`
- `Interfaces/Runtime.md`
- `Interfaces/AppFlow.md`
- `Interfaces/Resources.md`
- `Interfaces/Settings.md`
- `Interfaces/VoiceInput.md`
- `Packages/com.vincent.wgameframework/Settings/MxSettingsJsonFileStore.cs`
- `Packages/com.vincent.wgameframework/VoiceInput.Unity/UnityMicrophoneVoiceInputProvider.cs`
- `Packages/com.vincent.wgameframework/Resources.Unity/GlobalResourceRuntimeBootstrap.cs`
