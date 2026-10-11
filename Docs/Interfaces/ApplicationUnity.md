# Application Unity Boot 接口

> Status: Current
>
> Target assembly: `MxFramework.Application.Unity`
>
> Validation surface target assembly: `MxFramework.Application.Unity.Validation`
>
> Implementation status: runtime driver, generated Validation scene/assets, focused EditMode tests, and the cold-boot failure/cancel/rollback/fresh-retry PlayMode slice are implemented for #710

## Purpose and authority

`ApplicationStartup.Unity` is the thin Unity main-thread composition and driving layer for the
already implemented Application startup, atomic publication, live-frame, retirement, Platform
lifecycle, and Persistence checkpoint contracts. It does not create a second startup coordinator,
save authority, scene owner, Runtime loop, or UI navigation authority.

The persistent `ApplicationUnityBootRoot` is the sole Unity owner of:

- the exact `LiveApplicationStatePublisher` and its one claimed `ApplicationLiveFrameDriver`;
- the one long-lived `ApplicationPlatformLifecycleBridge`, which already owns the exact lifecycle
  drain claim;
- at most one active `ApplicationStartupCoordinator` run;
- the bounded set of publisher-transferred `LiveApplicationStateRetirement` receipts;
- one typed startup surface request queue and one cached read-only surface model.

It owns none of the candidate RuntimeHost, AppFlow, SceneFlow, ResourcePlan, SaveGame, or Trigger
internals directly. Those remain behind a fresh run returned by the injected run factory and, after
publication, behind the immutable live-state lease.

## Assembly and namespace boundary

```text
MxFramework.Application + MxFramework.Application.PlatformBridge
MxFramework.Runtime
MxFramework.Platform + MxFramework.Platform.Unity
UnityEngine.CoreModule
  -> MxFramework.Application.Unity
     namespace MxFramework.ApplicationStartup.Unity

MxFramework.Application.Unity
UnityEngine.UIElementsModule
  -> MxFramework.Application.Unity.Validation
     namespace MxFramework.ApplicationStartup.Unity.Validation
```

`MxFramework.Application.Unity` must not reference Persistence, SessionBridge, Resources,
SceneFlow, Gameplay, Gameplay Trigger, FairyGUI, UnityEditor, or a product/Demo assembly. Exact
candidate and Persistence compositions are injected through the run/lifecycle objects that those
outer composition assemblies build. This keeps the Boot driver reusable and prevents a Unity-facing
assembly from becoming a service locator.

The optional `Application.Unity.Validation` assembly contains the UI Toolkit binder only. It uses
Unity UIElements directly and does not require `MxFramework.UI.Toolkit`; the driver assembly does
not reference either UI assembly. The surface is a Debug /
Validation surface, not formal player-facing UI, so it does not trigger the FairyGUI Composer flow.
Any future product Loading UI remains a separate FairyGUI-facing adapter over the same typed model
and request contracts.

The committed Validation fixture generator belongs to the separate test-only
`MxFramework.Tests.Application.Unity.Editor` assembly. Runtime assemblies never reference
`UnityEditor`, and generated test scenes remain absent from production `EditorBuildSettings`.

## Boot root lifetime and unique ownership

`ApplicationUnityBootRoot` is a composition-owned `[DisallowMultipleComponent]` `MonoBehaviour` in
a minimal Boot scene. Its initialized GameObject is made persistent before any candidate scene is
prepared. Candidate content scenes must not contain another Boot root or lifecycle proxy.

Initialization uses one process/domain ownership gate. The first root whose serialized shell
dependencies are valid reserves the gate before constructing the outer composition, then becomes
the initialized owner only after exact-reference validation. A duplicate root fails
closed before any callback, drain, frame-driver, or surface ownership is acquired; it must not
silently replace the active owner. Claim failure is a stable diagnostic, not an exception-driven
control path.

The composition supplied to the root must allow exact-reference proof that its publisher,
publication/save fence, lifecycle bridge, and lifecycle reporter belong to the same application
lifetime. The serialized proxy is validated separately, and the root itself claims the frame
driver. The root does not search the scene, use a global service locator, or construct a fallback
graph. Initialization is all-or-nothing: no startup run begins until the unique owner, frame driver,
and lifecycle callback binding are established.

The Boot sentinel remains current until a candidate publishes. It has no Runtime session and no
checkpoint capability. The Boot root persists across success, failure, cancellation, retry, and
live-session replacement; those transitions never transfer the lifecycle queue or live frame
driver.

## Proposed composition contracts

The exact public spelling is implemented only after focused API review, but the Unity boundary is
frozen around the following typed roles:

```csharp
public interface IApplicationUnityBootCompositionFactory
{
    ApplicationUnityBootCompositionCreateResult TryCreate();
}

public sealed class ApplicationUnityBootComposition
{
    public LiveApplicationStatePublisher Publisher { get; }
    public ApplicationPublicationSaveFence PublicationSaveFence { get; }
    public ApplicationPlatformLifecycleBridge LifecycleBridge { get; }
    public UnityApplicationLifecycleReporter LifecycleReporter { get; }
    public IApplicationUnityStartupRunFactory RunFactory { get; }
    public int RetirementSlotCapacity { get; }
}

public interface IApplicationUnityStartupRunFactory
{
    ApplicationUnityStartupRunCreateResult TryCreate(
        in ApplicationUnityStartupIntent intent,
        long attempt,
        LiveApplicationStatePublisher publisher);
}

public interface IApplicationUnityStartupSurface
{
    bool TryDequeueRequest(out ApplicationUnityStartupSurfaceRequest request);
    void Apply(in ApplicationUnityStartupSurfaceModel model);
}
```

`ApplicationUnityBootCompositionCreateResult` is an immutable discriminated result: success carries
one non-null `ApplicationUnityBootComposition`; failure carries a stable create status/error and no
partially usable composition. The composition object is immutable and exact-reference based. It
does not expose a mutable registration API after creation.

Unity cannot serialize an interface field directly, so the Boot asset stores a serialized
`MonoBehaviour` adapter reference and casts it to `IApplicationUnityBootCompositionFactory` during
initialization. The concrete adapter lives in an outer product/composition assembly. It may reference
`Application.PersistenceBridge`, SessionBridge, Resources, and project factories as required; the
Boot root and `MxFramework.Application.Unity` reference only the interface above. A missing or wrong
adapter type is a structured initialization failure, never a scene search or reflection fallback.

The outer factory creates the reporter, publisher/fence, Persistence-backed lifecycle bridge, and
fresh-run factory as one low-frequency composition. `ApplicationPlatformLifecycleBridge` must retain
the exact lifecycle source identity used by `TryCreate` and add an internal, read-only, `NoAlloc`
verification seam equivalent to:

```csharp
internal bool UsesComposition(
    LiveApplicationStatePublisher publisher,
    ApplicationPublicationSaveFence fence,
    IApplicationLifecycleSource source);
```

The method succeeds only when all three references are exactly those captured by that bridge; it
does not expose the drain owner, checkpoint policy, save capability, or fence acquisition. The
PlatformBridge assembly grants `InternalsVisibleTo("MxFramework.Application.Unity")` only for this
verification seam. `Application.Unity` does not receive Persistence internals or a general friend
surface. This exact-reference proof also establishes that the bridge's already claimed drain came
from the returned `UnityApplicationLifecycleReporter`.

Application core grants the Unity assembly only the narrow internal read-only seams needed to
verify `LiveApplicationStatePublisher.RetirementCapacity` and
`ApplicationStartupCoordinator.UsesPublisher(exactPublisher)`. They expose no publication, lease,
save-fence acquisition, Runtime pump, or transaction mutation authority. The root preallocates a
slot array at least as large as the exact publisher capacity before claiming the frame driver.

The root itself, not the outer factory, calls `Publisher.TryClaimLiveFrameDriver` and binds the
serialized `UnityApplicationLifecycleProxy` to the returned reporter. A factory must not pre-claim
the frame driver or pre-bind the proxy.

Initialization order is fixed:

1. capture the Unity main thread, validate serialized proxy/factory/surface adapters, and claim the
   process-level Boot-root gate;
2. call the factory once and require a successful immutable result;
3. validate every non-null field, positive/capacity-consistent retirement bound, reporter owner
   thread, unbound proxy, `LifecycleBridge.UsesComposition(Publisher, Fence, Reporter)`, and
   preallocate every bounded root/surface slot needed by steady pumping;
4. only after all validation succeeds, claim the publisher's unique live frame driver;
5. bind the exact proxy to the exact reporter and then mark the root initialized;
6. only an initialized root may create the first startup run.

Validation failure occurs before frame-driver claim or proxy bind. If the irreversible frame-driver
claim succeeds but a later Unity proxy bind unexpectedly fails, the root retains the Boot ownership
gate and failed composition for diagnostics; it never releases the gate and lets a second root
pretend the contaminated graph was clean.

The run factory is the only Unity Boot dependency allowed to choose a plan and construct the exact
transaction/candidate graph. A successful result contains a newly allocated, not-yet-started
`ApplicationStartupCoordinator` bound to the root's exact publisher. It cannot return a cached
coordinator, transaction, operation, candidate session, or retirement receipt.

Before `Start`, the root verifies the newly returned coordinator through the narrow internal
`UsesPublisher` seam. A null, already-started/terminal, or foreign-publisher coordinator is rejected
without being ticked or disposed as if the root owned its unknown graph.

`ApplicationUnityStartupIntent` is an immutable typed value selected at composition time, for
example cold boot, new session, or load exact profile/slot. It does not contain an untyped service
map, arbitrary type name, scene path, or mutable payload. Missing-save fallback to New Game is never
implicit.

## Fresh run and retry semantics

The root holds at most one run. Initial start and every retry follow the same sequence:

1. reject the request unless the current run state permits it;
2. increment the checked application-lifetime attempt number;
3. ask the run factory for a fresh coordinator using the frozen intent;
4. verify the result is non-null, not started, not terminal, and bound to the exact publisher;
5. generate a new stable run id and call `Start` exactly once;
6. retain that coordinator until its terminal evidence has been projected to the surface.

Retry is accepted only after Failed, Cancelled, or TimedOut. `RollbackFailed` is ownership debt and
is never retryable. Before a run exists, only an explicit `RunFactoryUnavailable` result with no
coordinator may be retried; factory exceptions, contradictory results, validation failures, and
start failures poison the root for the lifetime. Retry never calls `Start` again on an old
coordinator and never reuses the old transaction/candidate/operation graph. A Succeeded run is not
retryable; a later load/new-session action is a new typed intent initiated by an outer product
policy.

Cancel is advisory and is forwarded once to the active coordinator. UI state does not become
Cancelled until the coordinator reaches its truthful terminal state. Repeated or stale request ids
are ignored deterministically.

## Main-thread phase order

All methods below run on the root's captured Unity main thread. Cross-thread calls fail closed.
There is one call per Unity message; the root never loops an async provider to terminal in one frame.

### `Update`

The fixed order is:

1. consume at most one validated surface request and create/cancel a run if permitted;
2. tick the active startup coordinator once with `Time.unscaledDeltaTime` and a root-owned monotonic
   elapsed accumulator;
3. pump the long-lived lifecycle bridge once. `checkpointSafePhase` is true only when no startup
   transaction is Running, Draining, RollingBack, or committing; the bridge and publisher still
   perform their exact-current qualification and publication/save-fence checks;
4. tick every occupied retirement slot once, in stable slot order, including a receipt begun in a
   previous frame;
5. dequeue publisher retirement receipts into every free slot in stable slot order, call
   `BeginRetirement` once for each transferred receipt, and do not tick it a second time in the same
   `Update`;
6. project the final state into one cached surface model and call `Apply` once.

This ordering lets a coordinator publication become visible before lifecycle policy dispatch while
still requiring the first successful live fixed Tick before a Runtime checkpoint can start. It also
keeps old-state retirement outside the publication critical section and prevents UI callbacks from
reentering startup or lifecycle work in the same phase.

Surface callbacks may enqueue only; they cannot execute a run factory, coordinator Tick,
publication, lifecycle Pump, retirement Tick, or application quit inline.

### `FixedUpdate`

The root calls the unique `ApplicationLiveFrameDriver.Tick` exactly once, after updating its checked
fixed-time accumulator. It does this independently of startup state so an old published session
continues running while a replacement candidate is prepared. The Boot sentinel produces no Runtime
attempt. A newly published restored session attempts exactly its frozen `F + 1` on the first
following `FixedUpdate`.

The root never directly ticks RuntimeHost, AppFlow, SceneFlow, Trigger, or a candidate graph.
Module-versus-standalone pump strategy remains inside the published live lease. Unity main-thread
serialization and the publisher safe-boundary gate jointly prevent live Tick from overlapping a
publication or lifecycle checkpoint dispatch.

## Typed surface and requests

The Validation surface consumes a presentation-only model. At minimum it carries stable typed
facts for:

- root initialization/lifetime state and attempt number;
- plan/run id, startup state, phase, step id, forward/rollback progress;
- cancel-requested state and stable Application/Runtime/bridge error codes;
- current live revision/session presence and the last live-frame result;
- lifecycle pump/checkpoint status;
- occupied/failed retirement slot count and the first stable ownership-debt code.

It must not contain transaction, operation, owner lease, checkpoint capability, absolute save path,
secret, or arbitrary exception-controlled action. Localized text is mapped by the surface from
stable codes; the root never parses display strings.

Requests are bounded typed values with a monotonic request id and one of `Start`, `Retry`, or
`Cancel`. Unknown/default kinds fail closed. The UI Toolkit binder uses prebound callbacks that only
enqueue a request. Queue overflow is visible diagnostics and does not overwrite an earlier request.
User-initiated exit is intentionally a separate product policy and must not be represented as a
Platform `Quitting` notification.

The serialized UI authority is singular: `UIDocument` owns the exact `PanelSettings` and
`VisualTreeAsset`, and that UXML declares the exact USS dependency. The surface serializes the
exact document, the same PanelSettings, and its positive request capacity; it does not duplicate
UXML/USS object references or discover them at runtime.

The driver applies the same central typed policy before executing every request: `Start` is valid
only for the initial Ready/no-run state, `Retry` is the only request that may recover an explicit
`RunFactoryUnavailable`, and `Cancel` is forwarded only to an active cancellable run. A queued or
forged request cannot bypass these state rules.

## Retirement slots

The root allocates a fixed slot array during initialization. Its capacity must be at least the
publisher retirement capacity; a mismatch fails initialization before a startup run. A transferred
receipt has exactly one local slot owner.

- Every occupied receipt is begun exactly once and ticked at most once per `Update`.
- A truthful successful retirement releases its publisher reservation and then frees the local slot.
- A failed retirement or ownership debt permanently retains and poisons that slot for diagnostics;
  it is never retried, discarded, or reported as cleaned.
- A receipt is never disposed merely because the surface or Boot scene is hidden.
- Dequeue is attempted only after existing receipts have received their fair poll for the frame.

Because failed receipts remain bounded debt, later publication can fail closed when capacity is
exhausted. The Boot root exposes that fact to the surface but cannot bypass or enlarge publisher
capacity at runtime.

## Allocation and lookup contract

- Root/composition validation, run creation, `Start`, snapshot/error transition, surface tree
  creation, retirement begin, and scene fixture creation: `LowFreqAlloc` or `AllocByDesign`.
- Warm `Update` request polling, coordinator/lifecycle/retirement polling, model projection, and
  `FixedUpdate` live-frame Tick: `NoAlloc after initialization` outside implementation-owned
  failure diagnostics and documented phase transitions.
- The cached surface model is updated in place. The root does not call the allocating
  `ApplicationStartupCoordinator.CaptureSnapshot()` every frame.
- No LINQ, reflection, string concatenation, temporary collection, per-frame delegate creation,
  `FindObjectOfType`, tag search, or scene-wide lookup is permitted in warm phases.
- No `unsafe` is required or authorized by this design.

## Shutdown and destruction

Controlled shutdown is an explicit state, not an immediate `OnDestroy` cleanup shortcut:

1. stop accepting surface Start/Retry requests;
2. unbind the Platform.Unity callback proxy so no new facts enter from this root;
3. request cancellation of a non-terminal startup run and continue one bounded pump per frame;
4. continue lifecycle active-operation polling and retirement polling without starting a new
   checkpoint or run;
5. release the process ownership gate only after the run is terminal, every non-poisoned local
   retirement receipt has truthfully completed, and the publisher still owns the Boot sentinel
   rather than a live Runtime session.

The current publisher contract has no authority to retire `Publisher.Current` without publishing a
replacement. Therefore a controlled shutdown that still owns a live Runtime session ends in stable
`ShutdownDebt`, retains the process gate, and cannot authorize a second application lifetime. Clean
`ShutdownComplete` and gate release are available only while the Boot sentinel is current. A future
explicit live-current retirement seam must define truthful checkpoint, scene/resource, lease, and
ownership completion before this rule can be relaxed.

Both `ShutdownComplete` and `ShutdownDebt` make the Unity root permanently inert. Debt retains the
process gate, but neither terminal shutdown state may pump lifecycle evidence, tick live Runtime,
poll retirements, create a run, or apply further surface callbacks.

`OnApplicationQuit` only latches quitting/best-effort shutdown intent. It cannot block Unity, spin
I/O, claim a final save succeeded, or convert the lifecycle notification into a user-exit command.
If Unity destroys the root before asynchronous ownership reaches terminal, the root records stable
shutdown debt and fails closed; it must not call arbitrary `Dispose` methods and claim successful
cleanup. Tests and controlled scene transitions must use the explicit drainable shutdown path.

## Validation evidence and boundary

Promotion to Current is backed by:

- Unity refresh/compile and no Console errors;
- focused EditMode tests for factory-adapter validation, unique-root claim, all-or-nothing binding,
  exact publisher/fence/reporter/lifecycle proof, root-owned frame-driver claim, typed request
  validation, fresh retry, Update/FixedUpdate order, retirement
  fairness/poison retention, shutdown debt, and warm zero-allocation polling;
- focused PlayMode cold-boot success with a persistent Boot shell and exact first live frame;
- focused PlayMode post-scene startup failure and restore/publication-boundary failure, plus exact
  candidate-scene load/unload, cancel, rollback, and fresh retry while the Boot shell remains usable;
- single lifecycle drain ownership across retry and live replacement, Boot zero-checkpoint behavior,
  and checkpoint only after the exact current session has a successful live Tick;
- asset/meta pairing and generation through Unity Editor/MCP or an existing Editor fixture command,
  not hand-written scene/prefab YAML.

The generated entry is `Assets/Scenes/Tests/Application/ApplicationUnityBootValidation.unity`; its
PanelSettings is `Assets/UI/MxFramework/ApplicationBoot/ApplicationBootPanelSettings.asset`.
`ApplicationUnityBootValidationAssetGenerator.GenerateFromCommandLine` is the materialization and
validation authority for both assets and their exact serialized references. PlayMode setup adds
the Boot/candidate fixtures to `EditorBuildSettings` only for the validation run and restores the
original production list during teardown.

The UI Toolkit surface proves only a framework Validation Runtime Slice. It is not a formal
player-facing FairyGUI UI, a product gameplay Demo, or a Playable claim.

## References

- `Docs/APPLICATION_STARTUP_ORCHESTRATION_DESIGN.md`
- `Docs/APPLICATION_PLATFORM_LIFECYCLE_BRIDGE_DESIGN.md`
- `Docs/Interfaces/Application.md`
- `Docs/Interfaces/ApplicationPersistenceBridge.md`
- `Docs/Interfaces/Platform.md`
- `Docs/Interfaces/UI.Toolkit.md`
- `Docs/Decisions/ADR-011-application-startup-authority.md`
