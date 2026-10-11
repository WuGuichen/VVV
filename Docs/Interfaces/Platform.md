# Platform Core 接口

> Status: Current
>
> Version: 0.2.0
>
> Date: 2026-07-19

## 职责

`MxFramework.Platform` 是 BCL-only、zero framework dependency、`noEngineReferences=true` 的宿主事实契约层。它负责描述环境、逻辑位置、应用生命周期、权限状态/请求和非权威 UTC wall clock，不执行保存、暂停 Runtime、切场景、UI 或权限业务策略。

当前交付包含 Platform Core contracts，以及单向 `MxFramework.Platform.Unity` environment/location/lifecycle/microphone-permission adapter。consumer 迁移、Persistence store、Runtime SaveState、Gameplay Trigger 和 target SDK provider 不在本阶段。

## 程序集边界

```text
BCL -> MxFramework.Platform

MxFramework.Platform.asmdef
  references: []
  noEngineReferences: true
  allowUnsafeCode: false
```

Core 不引用 `MxFramework.Core`、UnityEngine、UnityEditor、Runtime、Gameplay、Settings、Resources、Persistence 或项目代码。Unity 映射位于只依赖 Platform + UnityEngine 的 `MxFramework.Platform.Unity`；它不引用 UnityEditor、Runtime、Gameplay、Application、Persistence 或项目代码。

```text
MxFramework.Platform + UnityEngine -> MxFramework.Platform.Unity
```

## 结构化错误

`PlatformErrorCode` 的稳定数值为：

| Code | Value | 含义 |
| --- | ---: | --- |
| `None` | 0 | 无错误 |
| `Unsupported` | 1 | 宿主不支持能力 |
| `Unavailable` | 2 | 能力暂不可用 |
| `InvalidArgument` | 3 | 无效调用参数 |
| `InvalidLocation` | 4 | location kind/value 不合法 |
| `PermissionDenied` | 5 | 权限拒绝 |
| `PermissionRestricted` | 6 | 权限受宿主策略限制 |
| `Busy` | 7 | 已有独占请求进行中 |
| `RequestFailed` | 8 | 请求失败 |
| `Cancelled` | 9 | 请求已取消 |
| `AdapterFailure` | 10 | adapter contract 或调用失败 |

`PlatformError` 是 readonly value；`Message` 与可选 `Exception` 只用于本地低频诊断，不能进入 SaveState、network 或 authority hash。正常 unsupported/denied/busy 不以异常作流程控制。

## Environment

```csharp
public interface IPlatformEnvironmentProvider
{
    PlatformEnvironment Current { get; }
}
```

`PlatformEnvironment` 暴露 `Family`、`IsEditor`、`IsHeadless` 和 diagnostics-only `RuntimeName`。`PlatformFamily.Unknown = 0`，其余稳定值为 Windows 1、MacOS 2、Linux 3、Android 4、IOS 5、Web 6、Console 7。

`RuntimeName` 不参与规则、排序、hash、Replay 或 SaveState compatibility。

分配：构造时由 caller 创建 runtime-name string 属于 `LowFreqAlloc`；读取、复制、相等比较为 `NoAlloc`。

## Locations

`PlatformLocationKind`：

```text
Unknown = 0
FileSystemPath = 1
Uri = 2
ProviderOwned = 3
```

`PlatformLocationId`：

```text
Unknown = 0
PersistentData = 1
TemporaryCache = 2
StreamingContent = 3
```

`PlatformLocation.TryCreate(...)` 是受支持的创建入口：

- `FileSystemPath` 必须是非空绝对路径，并由 BCL 规范化；
- `Uri` 必须是非空 absolute URI；
- `ProviderOwned` 必须是非空 opaque locator，consumer 不得拆解；
- `Unknown`、default、空白、相对路径或相对 URI 返回 `PlatformLocationResult.Success=false` 和 `InvalidLocation`；
- access flags 与 `IntendedForPersistentData` 是 adapter 声明的宿主事实，不等于原子 replace、flush、quota、transaction 或 cloud sync 保证。

`IPlatformLocations.Resolve(id)` 返回 structured result。`PersistentData`、`TemporaryCache` 和 `StreamingContent` 是逻辑根；slot layout、相对名转义和目录结构仍归 Persistence / Resources owner。Filesystem consumer 只能接受 `FileSystemPath` 与所需 access flag；URI/provider-owned 必须使用匹配 adapter。

分配：result/location 读取、复制为 `NoAlloc`；路径和 URI 规范化及失败诊断为 `LowFreqAlloc`。

## Application Lifecycle

```csharp
public interface IApplicationLifecycleReporter
{
    bool ReportFocus(bool hasFocus);
    bool ReportSuspend(bool isSuspended);
    bool ReportQuitting();
}

public interface IApplicationLifecycleSource
{
    bool TryClaimDrain(out IApplicationLifecycleDrainOwner drainOwner);
}

public interface IApplicationLifecycleDrainOwner
{
    ApplicationLifecycleSnapshot Current { get; }
    long DroppedChangeCount { get; }
    bool TryDequeue(out ApplicationLifecycleChange change);
}
```

`ApplicationLifecycleSnapshot` 保持正交的 `HasFocus`、`IsSuspended`、sticky `IsQuitting` 与本地 monotonic `Revision`。change kind 稳定值为 Unknown 0、FocusChanged 1、SuspendChanged 2、Quitting 3、StateResyncRequired 4。

`ApplicationLifecycleBuffer` 是默认 fixed-capacity implementation：

- 构造必须提供 revision-zero initial snapshot 和正容量；初始化不产生 synthetic edge；
- 重复 callback 去重，只有 effective change 增加 revision；
- 普通 edge 按 report 顺序进入预分配 ring buffer；
- buffer 满时，现有 queued edge 与触发 overflow 的 edge 一起折叠到 queue 外 sticky resync slot；
- resync pending 期间的 effective edge 只更新 latest snapshot、完整 lost revision range、饱和 lost/drop count 以及 suspend/quitting sticky evidence；
- `Revision` 到达 `long.MaxValue` 后保持饱和；后续 effective edge 更新 latest snapshot，但不得以重复 revision 进入普通队列，必须把已有 queued edge 和新 edge 折叠为 `StateResyncRequired`；
- 下一次 `TryDequeue` 首先且单独返回一个 `StateResyncRequired`，成功 dequeue 后才恢复普通 enqueue；
- `DroppedChangeCount` 是 lifetime cumulative saturating `long`，resync dequeue 不清零；
- `IsQuitting` 一旦置位不恢复；v0.1 仅表示 non-veto quit notification。

默认 buffer 要求 serialized single producer，并通过 `TryClaimDrain` 原子地只授予一次 drain owner；第二次 claim 在读取或 dequeue 任何事实前失败。平台 callback 必须先 marshal 到声明的 owner thread；Application bridge 在自己的 safe phase drain 并执行 policy。Platform callback/report path 不保存、不 checkpoint、不发 Gameplay/Runtime command。

分配：构造 backing array 与首次成功 drain claim 为 `AllocByDesign`/`LowFreqAlloc`；`Report*` 以及 claimed owner 的 `Current`、`DroppedChangeCount` 和 `TryDequeue` 在初始化后为 `NoAlloc`。

## Permissions

`PlatformPermissionId` 使用稳定正整数；default/zero/negative invalid，built-in `Microphone.Value = 1`。后续 ID 不得复用为不同权限。

`PlatformPermissionStatus`：Unknown 0、Unsupported 1、NotRequired 2、NotDetermined 3、Granted 4、Denied 5、Restricted 6。

`PlatformPermissionOperationStatus`：Unknown 0、Pending 1、Succeeded 2、Failed 3、Cancelled 4。default operation status 不能被解释为 pending。

```csharp
public interface IPlatformPermissionService
{
    PlatformPermissionQueryResult Query(PlatformPermissionId permissionId);
    IPlatformPermissionOperation Request(PlatformPermissionId permissionId);
}
```

`Query.Success=true` 表示取得可信宿主事实；Unsupported 也是成功 query。invalid/adapter failure 返回 Unknown + structured error。`Request` 永不返回 null；invalid/unsupported/busy 使用独立 completed failed operation，Granted/NotRequired 使用 completed succeeded operation。

`CompletedPlatformPermissionOperation` 冻结 terminal invariants：Succeeded 仅允许 Granted/NotRequired；Failed 必须有非-None error 且不能携带 Granted/NotRequired；Cancelled 的 permission status 为 Unknown、error 为 None。terminal operation 不可取消。

真实异步 provider 必须保持 one active host dialog、second request 独立 Busy、cooperative cancellation 和 late host result 语义；Core 本阶段不实现 target provider。

分配：query/result/property polling 为 `NoAlloc`；operation 创建与 host request 为 `LowFreqAlloc`。

## UTC Wall Clock

```csharp
public interface IWallClock
{
    DateTime UtcNow { get; }
}
```

`SystemUtcWallClock` 是 BCL 默认实现。`PlatformUtcClock.TryReadUtc(...)` 在发布 metadata 前验证 provider：null 返回 InvalidArgument；provider exception 或 Local/Unspecified 返回 AdapterFailure，out timestamp 保持 default。它不会调用 `ToUniversalTime`、`SpecifyKind` 或 fallback clock。

Wall clock 允许回拨/跳变，只用于 diagnostics、人类可读时间、telemetry 和 cache age hint。它不能驱动 lifecycle/command order、generation/revision、Replay、authority hash、deterministic simulation 或 SaveState conflict resolution。

分配：`SystemUtcWallClock.UtcNow` 与成功验证路径为 `NoAlloc`；adapter exception diagnostic 为 `LowFreqAlloc`。

## Unity Host Adapter

`UnityPlatformEnvironmentProvider` 在组合期缓存 `Application.platform`、editor/headless 独立事实和 diagnostics runtime name。`MapFamily(RuntimePlatform)` 显式映射 Windows、macOS、Linux、Android、Apple mobile、Web 与 console family，未识别值 fail-closed 为 `Unknown`。默认 headless fact 来自 batch mode 或 null graphics device；它不从 `IsEditor` 推导。构造为 `LowFreqAlloc`，`Current` 与 mapping 为 `NoAlloc`。

`UnityPlatformLocations` 在构造期缓存并验证 persistent、temporary 与 streaming locations：

- persistent/temporary 必须是绝对 `FileSystemPath`，均声明 readable/writable；只有 persistent 标记 `IntendedForPersistentData`；
- streaming 首先保留绝对 filesystem path，否则保留 absolute URI（包括 Android `jar:file:` 和 Web URL），始终 readonly；
- invalid/default id 或无法规范化的 Unity value 返回 structured failure；
- `Resolve` 只返回 cached result，初始化后 `NoAlloc`。

`UnityApplicationLifecycleReporter` 在构造线程建立 revision-zero bounded buffer。focus/suspend/quitting report 与 source drain 只允许 owner thread；cross-thread write fail-closed、返回 false 且 snapshot 不变，不在 callback 间共享可变诊断 counter。`UnityApplicationLifecycleProxy` 是 `[DisallowMultipleComponent]` 的 composition-owned `MonoBehaviour`，只有显式 `Bind`/`Unbind` 和三个 facts-only Unity callbacks；它不创建 adapter、singleton 或 `DontDestroyOnLoad` owner，也不执行 persistence、RuntimeHost、scene、UI 或 application policy。构造/buffer 为 `AllocByDesign`，warm callback/report/current/drain 为 `NoAlloc`。

`UnityPlatformPermissionService` 当前只适配 built-in Microphone ID：

- Query/Request/Pump 必须在构造 owner thread 调用；invalid、unknown permission、unsupported、restricted、busy、adapter exception 均返回 non-null structured result/operation；
- Granted/NotRequired 直接成功；一个 service 同时只允许一个真实 Unity authorization request；
- request operation 的 host completion 只有在组合根主线程调用 `Pump()` 后才可见；operation property polling 与 warm pending Pump 为 `NoAlloc`；
- Unity `AsyncOperation` 不可取消，因此默认 operation 的 `CanCancel=false`，`RequestCancel` 不伪造 `Cancelled`；只有 provider 明确认可取消且确认成功时才发布 Cancelled；
- desktop runtime 明确为 `NotRequired`；WebGL microphone 和未分类 runtime 明确为 `Unsupported`；iOS/VisionOS 使用 `Application.RequestUserAuthorization`；Android 使用 `UnityEngine.Android.Permission`，并为每次 request 独立创建 `PermissionCallbacks` 与 result slot；
- 每个 Android request 分配独立 callback state、generation 和 result slot；旧 request 的迟到 callback 只能写旧 slot，不会在下一次 request reset 后形成 ABA 完成。callback 只通过 `Interlocked` 写 slot，任意线程的 host fact 必须等主线程 `Pump()` 才能改变 operation。request 前 Android false query 映射为 `NotDetermined`，且 host 当前返回 false 时绝不复用历史 `Granted`，以正确反映系统设置中的权限撤销；denied callback 只写 Denied slot，主线程 poll 再用 `ShouldShowRequestPermissionRationale` 区分 `Denied` / `Restricted`；dismiss 映射 structured request failure。不订阅 Unity 已标记 unreliable/obsolete 的 `PermissionDeniedAndDontAskAgain`。

service/operation 创建与 Unity request 为 `LowFreqAlloc`；operation polling、owner-thread check 与 warm `Pump` 为 `NoAlloc after initialization`。

## 禁止事项

- 不从 Platform Core 读取 Unity `Application.*` 或创建 MonoBehaviour/singleton。
- 不把 lifecycle change 投影为 Trigger signal、Runtime command、Replay 或 SaveState field。
- 不从 Platform drain/clear/reorder Trigger pending work，也不绕过 checkpoint quiescence。
- 不把 URI 或 provider-owned location 当成本地 filesystem path。
- 不用 wall clock 代替 monotonic sequence、generation、Runtime frame 或 domain clock。
- 不在本阶段迁移现有 consumer、VoiceInput permission gateway、Runtime SaveState timestamp 或 Trigger wall-clock metadata。

## 测试入口

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform/PlatformEnvironmentAndLocationTests.cs
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform/ApplicationLifecycleBufferTests.cs
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform/PlatformPermissionTests.cs
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform/WallClockTests.cs
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform.Unity/UnityPlatformEnvironmentAndLocationTests.cs
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform.Unity/UnityApplicationLifecycleTests.cs
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Platform.Unity/UnityPlatformPermissionServiceTests.cs
```

focused noEngine tests 覆盖 stable enum/default、location kind/access/validation、lifecycle dedupe/order/sticky quitting/overflow 与 revision-exhaustion resync/saturating counters/0 B steady loop、permission terminal/cancel/Busy contract 和 UTC fail-closed/rollback/0 B system getter。focused Unity EditMode tests 覆盖 platform mapping、cached path/URI normalization、callback proxy、cross-thread rejection、permission query/request/Pump/cancellation/late-result/failure，以及 warm lifecycle 与 permission polling 0 B。
