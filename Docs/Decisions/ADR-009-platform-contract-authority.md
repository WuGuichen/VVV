# ADR-009: Platform Contract Authority And Host Fact Boundary

> Status: ADR

Date: 2026-07-19
Decision Status: Accepted

## Context

MxFramework currently reads platform facts directly from Unity APIs and the BCL in multiple modules. Persistent-data and streaming-content locations, application focus and pause callbacks, permission requests, and UTC timestamps therefore have inconsistent contracts and failure semantics.

These values are host facts, not gameplay or application policy. If their acquisition is mixed into Runtime, Gameplay, Persistence, or project-specific code, those domains become coupled to Unity callbacks and target SDKs, noEngine tests become difficult, and platform data can accidentally enter deterministic authority, replay, hashing, or SaveState generation rules.

`Docs/PLATFORM_CONTRACTS_DESIGN.md` defines the proposed contract surface. This ADR accepts that direction and freezes the authority boundary and the public semantic rules required for phased implementation.

## Decision

### 1. Authority And Assembly Boundary

`MxFramework.Platform` is the sole framework authority for normalized host facts in the following areas:

- environment identity;
- logical platform locations;
- application lifecycle observations;
- permission state and permission-request operations;
- non-authoritative UTC wall-clock access.

The core assembly is a pure noEngine assembly:

```text
MxFramework.Platform
  references: []
  noEngineReferences: true
  allowUnsafeCode: false
```

It may use the BCL, but it must not reference `UnityEngine`, `UnityEditor`, Runtime, Gameplay, Settings, Resources, Persistence, target SDKs, or project code. Adding a framework dependency to this core assembly requires an explicit revision of this decision.

Unity callback and API mapping belongs to `MxFramework.Platform.Unity`, which depends one-way on `MxFramework.Platform` and `UnityEngine`. Target SDK integrations belong to separate provider assemblies. Application policy belongs to an application composition bridge and consumes Platform contracts; Platform does not depend on that policy.

The dependency direction is:

```text
BCL -> Platform
Platform + UnityEngine -> Platform.Unity
Platform + target SDK -> Platform.Provider.*
Platform + application policy -> ApplicationStartup.PlatformBridge
Platform location result -> Persistence.FileSystem or another explicit storage adapter
```

Platform reports facts and structured failures. It does not decide when to save, retry, pause simulation, cancel gameplay, or terminate the application.

### 2. Environment And Location Contracts

The environment family values are stable public values:

```text
Unknown = 0
Windows = 1
MacOS = 2
Linux = 3
Android = 4
IOS = 5
Web = 6
Console = 7
```

`PlatformEnvironment` contains `Family`, `IsEditor`, `IsHeadless`, and a diagnostic `RuntimeName`. It is obtained through `IPlatformEnvironmentProvider`; consumers do not read `Application.platform` directly after migration.

Logical locations are identified by the following stable values:

```text
PlatformLocationKind
  Unknown = 0
  FileSystemPath = 1
  Uri = 2
  ProviderOwned = 3

PlatformLocationId
  Unknown = 0
  PersistentData = 1
  TemporaryCache = 2
  StreamingContent = 3
```

`IPlatformLocations.Resolve(PlatformLocationId)` returns a structured `PlatformLocationResult`. A successful `PlatformLocation` contains:

- `Kind`;
- a normalized non-empty `Value`;
- `IsReadable`;
- `IsWritable`;
- `IntendedForPersistentData`.

The following invariants are mandatory:

- `FileSystemPath` values are absolute filesystem paths.
- `Uri` values are absolute URIs.
- `ProviderOwned` values are opaque tokens interpreted only by their matching provider.
- `Unknown`, default values, empty values, malformed paths, and malformed URIs fail structurally; they are never treated as successful locations.
- `PersistentData` expresses logical intent only. It does not prove atomic writes, durability, quota, backup behavior, or transaction support.
- `TemporaryCache` may be cleared by the host at any time.
- `StreamingContent` may be a URI or provider-owned resource and must not be passed blindly to `Path.Combine`, `File.*`, or synchronous filesystem APIs.

The filesystem persistence adapter accepts only a successful, writable `FileSystemPath`. It must reject URI and provider-owned locations with a structured error. URI and provider-owned storage require their own explicit adapters. The persistence layer remains responsible for validating its atomic-commit and recovery guarantees.

The minimum shared platform error vocabulary is stable:

```text
None = 0
Unsupported = 1
Unavailable = 2
InvalidArgument = 3
InvalidLocation = 4
PermissionDenied = 5
PermissionRestricted = 6
Busy = 7
RequestFailed = 8
Cancelled = 9
AdapterFailure = 10
```

Errors are returned as values. Exceptions may be captured as low-frequency diagnostic context at adapter boundaries, but expected unsupported, denied, invalid, busy, and cancellation outcomes do not use exceptions for control flow.

### 3. Lifecycle Snapshot And Bounded Buffer

Lifecycle is an observation stream backed by a bounded noEngine buffer. Its snapshot contains:

- `HasFocus`;
- `IsSuspended`;
- `IsQuitting`;
- monotonic `Revision`.

Lifecycle change kinds are stable:

```text
Unknown = 0
FocusChanged = 1
SuspendChanged = 2
Quitting = 3
StateResyncRequired = 4
```

An effective state transition increments `Revision` and enqueues one change. Duplicate callbacks that do not change the normalized snapshot are ignored. Initial state is supplied explicitly by the adapter at construction, starts at revision zero, and does not create a synthetic edge.

The Unity adapter maps facts only:

- `OnApplicationFocus(bool)` updates focus;
- `OnApplicationPause(bool)` updates suspension;
- `OnApplicationQuit()` marks quitting.

`IsQuitting` is sticky for the lifetime of the platform/application instance. In v0.1, `Quitting` is a non-veto notification corresponding to `OnApplicationQuit`; any future quit-veto capability must be a separate contract.

The callback path only normalizes state and writes to the lifecycle buffer. It must not save, checkpoint, publish domain events, execute gameplay commands, allocate after initialization, or call application services. The long-lived `ApplicationStartup.PlatformBridge` is the sole drain owner; the application driver only invokes that bridge at a defined point in its loop.

The default buffer has one main-thread producer and one main-thread drain owner. A provider receiving callbacks on another thread must marshal them before writing; the core buffer does not hide cross-thread ownership with locks.

Overflow is fail-closed and loss-aware:

1. The buffer maintains a sticky resynchronization slot outside the regular queue.
2. On overflow, queued deltas whose exact order can no longer be proven are folded into that slot.
3. While resynchronization is pending, incoming callbacks update only the latest snapshot, the lost revision range, the saturating lost-change count, and sticky `SawSuspendInLostRange` / `SawQuittingInLostRange` flags.
4. The next successful dequeue returns `StateResyncRequired` first and as the only item for that dequeue.
5. After that resynchronization item is consumed, regular enqueueing resumes from the latest snapshot.

`StateResyncRequired` includes the latest snapshot, `LostRevisionStart`, `LostRevisionEnd`, `LostChangeCount`, `SawSuspendInLostRange`, and `SawQuittingInLostRange`. Counters saturate at `long.MaxValue`; they do not wrap.

An application consumer that observes lost suspend or quitting evidence must execute its conservative safe-checkpoint/save policy before aligning to the final snapshot. Platform reports this evidence but does not choose or execute that policy.

### 4. Permission Identity, State, And Operations

`PlatformPermissionId` is a stable positive integer value. Zero is invalid/default and must fail structurally. Built-in ID `1` is reserved for `Microphone`; assigned IDs are never reused for a different permission.

Permission status values are stable and default-safe:

```text
PlatformPermissionStatus
  Unknown = 0
  Unsupported = 1
  NotRequired = 2
  NotDetermined = 3
  Granted = 4
  Denied = 5
  Restricted = 6
```

Permission operation status also reserves zero for an uninitialized value:

```text
PlatformPermissionOperationStatus
  Unknown = 0
  Pending = 1
  Succeeded = 2
  Failed = 3
  Cancelled = 4
```

Consequently, a default operation value must never be interpreted as a live pending request.

`IPlatformPermissionService.Query` returns a structured query result. `Request` always returns a non-null pollable operation. Invalid, unsupported, restricted, already-denied-without-a-request-path, busy, and adapter-failure outcomes return completed operations with explicit status and error; they do not return null or throw for expected outcomes.

At most one host permission dialog may be active per service instance. A second request while one is active completes with `Busy`. Already granted or not-required permissions complete successfully without opening a dialog.

Cancellation is cooperative. `CanCancel` describes whether the provider can still prevent or abandon the request. `RequestCancel` must not report `Cancelled` after an irreversible host dialog has already completed or when the provider cannot cancel it. A false cancellation result leaves the operation pending or terminal according to the real host state.

Unity permission queries and requests are main-thread operations. Polling terminal state and reading operation properties are side-effect-free and no-allocation after operation creation.

### 5. UTC Wall Clock Is Non-Authoritative And Fail-Closed

The core contract remains:

```csharp
public interface IWallClock
{
    DateTime UtcNow { get; }
}
```

`SystemUtcWallClock` is the default BCL implementation. Test code may inject deterministic or deliberately regressing clocks.

Every value returned by `IWallClock.UtcNow` must have `DateTimeKind.Utc`. A `Local` or `Unspecified` value is a contract failure. A consumer crossing an application, persistence, telemetry, or external-publication boundary must validate the kind before publishing the timestamp and publish no timestamp or partially updated record on failure. A Platform adapter or bridge reports `PlatformErrorCode.AdapterFailure`; a downstream domain that deliberately has no Platform dependency maps the same fact to its own structured error, such as Persistence `InvalidTimestamp`.

Invalid clock values must not be silently repaired with `ToUniversalTime`, `SpecifyKind`, local timezone assumptions, or fallback reads from another clock. This fail-closed rule keeps provider faults observable and testable.

Wall-clock time may move backward or jump forward. It is allowed only for non-authoritative metadata such as diagnostics, human-readable timestamps, telemetry, and cache age hints. It must not determine:

- lifecycle or command ordering;
- generation or revision numbers;
- replay order;
- authority hashes;
- deterministic simulation;
- SaveState conflict resolution.

Those concerns use explicit monotonic counters, generations, sequence numbers, or domain-owned clocks.

### 6. Allocation Classification

The first public API must document allocation behavior in XML comments and interface documentation using the project classifications.

`NoAlloc after initialization` applies to:

- environment and lifecycle snapshot getters;
- platform ID/value equality and status inspection;
- lifecycle normalization, enqueue, `Current`, and `TryDequeue`;
- permission-operation polling and property getters;
- `SystemUtcWallClock.UtcNow`.

`LowFreqAlloc` applies to:

- location normalization and structured diagnostic messages;
- permission-operation creation and starting a host request;
- adapter error capture, including an optional exception reference.

Explicit diagnostic snapshots or formatted reports may be `AllocByDesign` only when their public documentation says so. No high-frequency path may introduce LINQ, temporary collections, string formatting, reflection, exception-driven control flow, Unity object lookup, or per-callback delegates after initialization.

The core contracts and default lifecycle buffer do not require `unsafe`; `unsafe`, pointer ownership, and raw memory are not part of the public surface.

### 7. Non-Goals And Trigger Isolation

This decision does not create a universal platform service locator or arbitrary capability bag. It also does not define store SDKs, accounts, achievements, cloud saves, payments, network authentication, analytics, filesystem transactions, application save policy, or gameplay pause policy.

`Application.dataPath` is not a project-root contract. Streaming content is not guaranteed to be a directly readable filesystem path. Platform facts are not deterministic authority and are excluded from replay and authority hashing.

Gameplay Trigger remains isolated:

- `MxFramework.Gameplay.Triggers` does not reference `MxFramework.Platform` or `MxFramework.Platform.Unity`.
- Platform lifecycle changes do not become Trigger signals, Runtime commands, replay entries, hash inputs, or Trigger SaveState fields.
- Platform and its application bridge must not drain, clear, reorder, or interpret Trigger pending work.
- A future suspend policy may request the existing Runtime checkpoint/quiescence boundary, but it must respect Trigger pending-work and quiescence contracts rather than bypassing them.
- The initial Platform Core and Platform.Unity deliveries do not migrate Trigger's existing direct wall-clock metadata read by adding a Platform dependency. A future Runtime SaveState header or Persistence envelope authority may own that metadata migration without changing Trigger authority.

### 8. Phased Adoption

Adoption is split so that contract review is not mixed with broad consumer migration:

1. Add the noEngine Platform contracts, default lifecycle buffer, BCL wall clock, and focused noEngine tests.
2. Add `Platform.Unity` adapters and focused Unity mapping tests.
3. Add the application policy bridge and migrate consumers by domain, including location, lifecycle, and permission callers.
4. Add target-specific providers only when a real platform capability requires them.

The first core pull request does not migrate all existing consumers. Direct `Application.*` and `DateTime.UtcNow` call sites are migrated under their owning domain so that each behavior change has explicit validation.

## Consequences

Positive consequences:

- Platform facts become testable without Unity.
- Filesystem, URI, and provider-owned resources cannot be confused silently.
- Lifecycle loss becomes observable and drives conservative policy instead of silently dropping critical suspend or quit evidence.
- Permission defaults and asynchronous operations have explicit, non-null semantics.
- Invalid or regressing wall clocks cannot silently become ordering or persistence authority.
- Trigger and deterministic Runtime authority remain independent from host integration.

Costs and constraints:

- Unity and target providers require explicit mapping and main-thread ownership.
- Consumers must handle structured failures and location kinds instead of assuming strings are paths.
- Lifecycle consumers must support resynchronization and conservative checkpoint policy.
- Consumer migration is intentionally incremental, so direct host API usage remains temporarily visible until each owning domain is migrated.

## Alternatives Considered

### Put Platform APIs In Runtime Or Runtime.Unity

Rejected. Runtime owns deterministic execution and lifecycle scheduling, while Platform owns host facts. Combining them would force unrelated consumers to depend on Runtime and would weaken noEngine and authority boundaries.

### Expose Raw Unity Strings And Callbacks

Rejected. Raw strings do not distinguish files, URIs, and provider-owned resources, and direct callbacks encourage domain work during Unity callback execution without bounded buffering or loss semantics.

### Use A Generic Capability Dictionary Or Service Locator

Rejected. It removes compile-time contracts, hides allocation and threading behavior, and makes unsupported/default states ambiguous.

### Normalize Invalid Wall Clocks Automatically

Rejected. Converting or relabeling `Local` and `Unspecified` values hides provider defects and can publish incorrect timestamps. UTC validation therefore fails closed.

### Add Platform As A Trigger Dependency

Rejected. Trigger state and ordering are deterministic gameplay authority. Host lifecycle and wall-clock facts are not.

## Adoption Gate And Verification

The public contract implementation is a V4 change and is accepted only with the following evidence:

- `MxFramework.Platform.asmdef` has `references: []`, `noEngineReferences: true`, and `allowUnsafeCode: false`.
- The noEngine project builds, and the Platform core contains no `UnityEngine` or `UnityEditor` references.
- Focused location tests cover filesystem, URI, provider-owned, invalid/default values, and filesystem-adapter rejection.
- Focused lifecycle tests cover duplicate callback deduplication, focus/suspend transitions, sticky quitting, same-frame suspend/resume, overflow resynchronization, lost revision/count metadata, sticky suspend/quit evidence, and counter saturation.
- Focused permission tests cover every public state, invalid ID zero, `Microphone = 1`, non-null completed failures, granted/not-required fast paths, single-dialog `Busy`, cancellable requests, and non-cancellable requests.
- Focused clock tests cover valid UTC, deterministic fake values, backward time, invalid `Local`, invalid `Unspecified`, and proof that invalid values publish no metadata.
- Allocation tests prove lifecycle report/drain, permission polling, and the system wall-clock getter are no-allocation after initialization.
- The Unity adapter phase performs Unity refresh/compile, Console error review, focused callback/location/environment mapping tests, and a focused lifecycle/permission smoke where the host supports it.
- `git diff --check`, relevant builds and tests, `Tools/Harness/run_lightweight_checks.sh`, and report-only `Tools/Harness/check_code_standards.py` are recorded for the public API delivery.
- Unity-generated `.csproj` files are refreshed through Unity and are not hand-edited.
- No `unsafe` is introduced.
- Trigger assembly dependencies remain unchanged; Platform validation is not used to claim unrelated Trigger behavior or full-suite coverage.

## References

- `Docs/PLATFORM_CONTRACTS_DESIGN.md`
- `Docs/ARCHITECTURE.md`
- `Docs/INTERFACES.md`
- `Docs/API_STANDARDS.md`
- `Docs/QUALITY_GATE.md`
- `Docs/Decisions/ADR-008-gameplay-trigger-runtime-authority.md`
