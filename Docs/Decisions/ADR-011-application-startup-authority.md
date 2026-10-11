# ADR-011: Application Startup Authority And Atomic Session Publication

> Status: ADR

Date: 2026-07-19

Decision Status: Accepted

Implementation Status: Accepted target architecture; delivery remains phased. This ADR does not make the proposed Application assembly, bridges, RuntimeHost hardening, or Unity Boot shell a Current capability before their own implementation and validation gates pass.

## Context

MxFramework already has owners for RuntimeHost lifecycle, AppFlow state, SceneFlow operations, Resources, Persistence, Platform facts, Settings, and presentation. What it does not have is one application-level transaction that can prepare a replacement session, restore it, validate it, and publish it without exposing partial state.

Letting each Demo or composition root improvise this orchestration creates several authority failures:

- startup can become a Runtime module even though the RuntimeHost it needs does not exist yet;
- candidate resources, scenes, controllers, or restored participants can leak into live globals before the full load succeeds;
- host initialization can fail after modules have already entered lifecycle callbacks, leaving partial external side effects behind;
- cancellation or timeout can be reported before an underlying non-cancellable Unity/provider operation reaches a safe cleanup boundary;
- host, scene, resource, AppFlow, and frame cursor can be published independently and observed as different generations;
- a session-owned Platform bridge can leave lifecycle facts with two drain owners or no drain owner during replacement;
- retry can accidentally reuse failed operations, candidate owners, or gameplay authority state.

The application startup design therefore requires an outer transaction authority. This ADR freezes that authority boundary and its safety prerequisites. It freezes semantics, not the final spelling of proposed public types.

## Decision

### 1. Startup is an outer application coordinator

Assembly `MxFramework.Application` is a noEngine application-level coordinator, while its public C# API lives under `MxFramework.ApplicationStartup`. No production API may recreate the sibling namespace `MxFramework.Application`, because that namespace shadows `UnityEngine.Application` for callers under `MxFramework.*`. The assembly may depend on Core and Runtime operation contracts, but it is not an `IRuntimeModule`, is never registered in a RuntimeHost, and does not become gameplay authority.

The coordinator owns only plan validation, run state, step scheduling, cancellation/timeout policy, rollback scheduling, transaction commit, progress, and diagnostics. Existing domains remain authoritative for their own work. Concrete Resources, Persistence, Platform, Scene, UI, Network, or product steps live in bridge assemblies or the composition root; Application core does not use reflection, a service locator, or direct dependencies on those domains.

Startup may pump candidate-owned operations and controllers while preparing a transaction. It never pumps an already published RuntimeHost. Each candidate AppFlow or SceneFlow controller freezes exactly one pump strategy, `RuntimeModule` or `Standalone`, and double pumping is a plan-validation error.

### 2. Plans are deterministic and runs are single-use

A startup plan is an initialization-time-frozen DAG with stable plan and step identities. Validation completes before the first side effect and rejects empty/duplicate identities, missing or self dependencies, cycles, invalid weights/timeouts/capacities, missing transaction contracts, and conflicting exclusive owners.

V0.1 executes one step at a time in the unique order:

1. topological readiness;
2. explicit `Order`;
3. `StepId` ordinal.

The DAG does not imply cross-step concurrency. A step may internally use bounded concurrency when its owning domain already defines it. Future parallel groups require explicit resource locks, failure join, and deterministic completion rules.

A terminal run cannot restart. Retry creates a new run, transaction, step-operation set, candidate RuntimeHost, and candidate domain graph. Failed, cancelled, or timed-out operations are never reused.

### 3. Cancellation, timeout, and rollback use safe terminal boundaries

Cancellation is cooperative. After a cancel request, the coordinator starts no new forward step, requests cancellation from the current operation when supported, and continues polling until that operation reaches a truthful safe terminal boundary. It does not kill a thread, fabricate `Cancelled`, or begin rollback concurrently with an in-flight non-cancellable operation.

Timeout uses monotonic/unscaled delta supplied by the outer driver. Reaching a deadline latches the outer timeout cause and follows the same drain rule. If the inner operation later succeeds or fails, its terminal status and structured error remain diagnostic evidence while the Application outcome remains `TimedOut`.

Only forward steps that reached `Succeeded` are rolled back by the runner, in strict reverse completion order. A current step that has partial asynchronous state owns and drains that state before it may report a terminal result. Each asynchronous compensation is an explicit rollback operation. Only after all such operations are terminal may `FinalizeRollback()` synchronously release remaining ownership and verify that no active lease remains.

Rollback is best-effort across every eligible step. One compensation error does not stop the rest. Forward errors and rollback errors remain separate and ordered; rollback failure produces `RollbackFailed` and never hides the initiating failure. `FinalizeRollback()` and final `Dispose()` are idempotent and cannot start new Unity, resource, or I/O operations.

### 4. All work occurs in an isolated candidate transaction

Cold boot, new session, and load session build a complete candidate generation before publication. The candidate owns its RuntimeHost/module graph, AppFlow, SceneFlow controller or prepared scene lease, resource plan session, and—when catalog, provider, variant, or global resource services change—its own ResourceManager dependency graph. Project session services and restored participants belong to the same candidate live-session lease.

Before commit:

- the old live session, scene, resource plan, and resource services remain valid;
- candidate code cannot read or publish through live/global singleton services;
- candidate scenes are separate from the live SceneFlow controller and are normally prepared additively;
- scene `Awake`/`OnEnable`, Runtime/AppFlow lifecycle, SDK registration, audio, UI, input, render, and static registration are candidate-owned, inert behind a revision-aware activation gate, or fully compensatable;
- no normal gameplay Tick occurs;
- all potentially failing restore, validation, lifecycle, and handoff preflight work is complete.

Post-publication activation is limited to an infallible ownership/visibility flip. A bridge that still requires fallible or asynchronous activation is not transaction-safe and blocks the corresponding delivery phase. `Single` scene loading is limited to an initial cold boot with no replaceable live content and a separately persistent Boot shell; replaceable-session flows default to additive preparation.

### 5. Live state is published once as one immutable aggregate

The composition root exposes one `ILiveApplicationStatePublisher`. Its `Current` value is always non-null and begins as a Boot sentinel with revision `0`, no Runtime session, next frame `-1`, and an idempotent no-op lease. Revision `0` is never assigned to a real session.

After every forward step succeeds, the transaction may call commit exactly once. Commit validates that the expected state is still current and atomically compare-exchanges one immutable `LiveApplicationState` reference containing the complete session lease and its next Runtime frame. It is forbidden to publish host, AppFlow, scene, resources, services, or frame cursor through independently mutable live fields.

Only the publisher allocates revisions, using checked `expected.Revision + 1`. Unpublished candidates have no live revision. A publication conflict or revision overflow fails before the swap and leaves the candidate rollback-owned.

On a successful swap:

- the publisher becomes the sole owner of the new current lease and the old retired lease;
- the transaction detaches the candidate, so its later `Dispose()` cannot dispose live state;
- live drivers reread `Current` at safe boundaries instead of caching separate domain references across a publication;
- callbacks carry a session revision/token and stale callbacks are rejected before authority side effects;
- the old generation is inert for authority, input, audio, UI, and rendering before asynchronous cleanup begins.

Old-generation cleanup is best-effort retirement. Its failure creates warning/cleanup debt, not commit failure, rollback, or restoration of the old live reference. Any component that can still produce domain or player-facing output after token invalidation is not a safe lease and blocks publication until redesigned.

### 6. RuntimeHost entered-module unwind is a prerequisite

Candidate Runtime lifecycle preparation is blocked until RuntimeHost, or a project adapter with demonstrably equivalent behavior, provides a per-module lifecycle ledger. At minimum it records `InitializeEntered`, `Initialized`, `StartEntered`, `Started`, and cleanup attempts/ordering.

The lifecycle contract is:

- a callback that has been entered must either provide a tested strong exception guarantee or support idempotent compensation even when it throws after creating an external side effect;
- Initialize or Start failure unwinds every actual entered module, including the throwing module and non-contiguous sets produced by collect-and-continue policy;
- `Stop` is attempted in reverse entry order for every `StartEntered` module;
- `Dispose` is attempted in reverse entry order for every `InitializeEntered` module; lifetime ownership also transfers on successful registration, so every remaining registered-but-unentered module is eventually offered `Dispose` exactly once in reverse registration order;
- one Stop/Dispose failure does not prevent remaining cleanup;
- the original forward errors and all cleanup errors are retained in deterministic order;
- after a forward lifecycle failure the Host is in a terminal faulted, non-reusable state, while final disposal remains idempotent;
- `FailFast`, `CollectAndStopFrame`, and `CollectAndContinue` preserve their forward-policy meaning but cannot bypass unwind or final state/error validation.

The existing Trigger fail-fast requirement and Runtime diagnostics ordering must remain compatible. Ordinary `Dispose()` after a partial failure is not accepted as evidence unless the ledger, throwing-module compensation, traversal, and error aggregation behavior are verified by fault injection.

### 7. Restored Runtime starts exactly at checkpoint F+1

A prepared save checkpoint frame `F` denotes completed end-of-frame state. The candidate computes and freezes `NextRuntimeFrame = checked(F + 1)` before publication; overflow is a structured failure. Restore and startup preparation do not Tick gameplay.

The live driver tracks the last observed publication revision. It seeds that session's frame cursor from `NextRuntimeFrame` exactly once when it first observes a new revision. The first fixed Tick is exactly `F + 1`, then normal progression continues. Re-reading the same revision, failing/cancelling a candidate, or retrying cannot reseed or advance the live frame. New Game supplies an explicit `InitialRuntimeFrame` rather than inventing a checkpoint.

### 8. Platform lifecycle authority outlives every session

The persistent Unity Boot composition root creates one long-lived `ApplicationStartup.PlatformBridge` before the first startup run. Platform.Unity callbacks only enqueue facts. The bridge is the sole drain owner and, at the Application main-thread lifecycle phase, reads the publisher's current state for each policy dispatch. Session replacement never hands off or copies the lifecycle queue.

The Boot scene/shell owns the composition root, Platform.Unity proxy, and Loading/Error/Retry UI. It contains no gameplay authority and remains operational after startup, scene, restore, or rollback failure. Loading UI observes snapshots and emits typed requests; it does not own steps, operations, leases, or publication.

While the Boot sentinel is current, the bridge continues draining application-lifetime facts but never targets a Runtime checkpoint. Suspend evidence that must wait for a real session is retained as a bounded pending policy fact until publication or a startup terminal state. During startup or rollback, checkpoint policy waits for the candidate transaction's safe terminal boundary. Overflow, `StateResyncRequired`, and lost suspend/quitting evidence remain explicit inputs to policy rather than being reduced to the latest snapshot.

Application quitting is a best-effort lifecycle fact, not a guaranteed final save opportunity and not an automatic user-exit command. Product policy must use earlier suspend/checkpoint opportunities and keep user-initiated exit separate.

### 9. Trigger remains isolated behind Runtime and Gameplay contracts

Application core and its generic transaction have no dependency on `MxFramework.Gameplay.Triggers`. Trigger remains Gameplay authority with its existing command-buffer, event-router, replay/save/hash, reliable-outbox, and fail-fast ownership rules.

A candidate that uses Trigger constructs an entirely new Trigger runtime/module graph inside the candidate session; it never shares live Trigger instances, buffers, source/router ownership, deferred state, or outbox state. Startup treats Trigger restore/lifecycle as opaque Runtime participants and does not inspect, drain, publish, or compensate Trigger-specific state directly. No Trigger evaluation or normal Gameplay Tick occurs before session publication.

Trigger-specific adapters may exist only outside Application core and must preserve one producer/drain owner and existing deterministic delivery contracts. RuntimeHost hardening must therefore change lifecycle failure behavior without changing Trigger Tick semantics, diagnostics ordering, or the composition root's ability to require `RuntimeHostErrorPolicy.FailFast`.

### 10. Validation gates are part of acceptance

This ADR is implemented only when evidence covers the following layers:

- noEngine plan validation, registration-permutation ordering, success/failure, partial-current-step drain, cooperative cancellation, non-cancellable timeout drain, reverse rollback, rollback faults, retry isolation, progress, structured errors, and zero-allocation steady polling after warmup;
- RuntimeHost fault injection at every module index for Initialize, Start, Stop, and Dispose across all error policies, including a callback that registers an external side effect before throwing, with exact ledger, reverse cleanup, error ordering, no duplicate disposal, and no residual owner assertions;
- integration faults for SaveGame restore, candidate resource/catalog/provider isolation, SceneFlow load/cancel/activation, AppFlow compensation, publication conflict, old-state cleanup failure, stale callbacks, single-owner handoff, Boot sentinel revision `0` to first real revision `1`, exact `F + 1`, and single Platform drain ownership;
- Unity refresh/compile with no Console errors, focused EditMode bridge tests, and focused PlayMode cold-boot success, preload/scene/save failure, cancel, rollback, and retry scenarios with a persistent usable Boot shell;
- allocation checks showing coordinator and operation steady polling are `NoAlloc after initialization`.

Phase completion and Current interface documentation must follow actual evidence. In particular, missing RuntimeHost entered-module unwind, candidate scene/resource isolation, immutable single-state publication, or AppFlow compensation keeps the Runtime/Persistence bridge phase blocked. Missing a Unity Boot entry keeps the feature non-Playable.

## Consequences

Benefits:

- startup/load failure cannot expose a half-restored or mixed-generation live application;
- ownership and pump authority are explicit across Runtime, AppFlow, SceneFlow, Resources, Platform, Unity, and Trigger;
- cancellation, timeout, rollback failure, retry, and frame continuation have deterministic testable outcomes;
- the noEngine coordinator remains reusable across products and does not absorb domain-specific logic;
- a persistent Boot shell can present stable failure and retry without depending on gameplay content.

Costs and constraints:

- candidate replacement may temporarily retain two complete generations of scene/resource/session state;
- several current controllers and composition paths need bridge work, side-effect gates, or compensation before they are production-safe;
- RuntimeHost lifecycle hardening is required before candidate lifecycle preparation can be claimed atomic;
- the publisher and live drivers must use revision-aware ownership and stale-callback rejection;
- unsupported provider/Unity operations may delay cancellation until their safe terminal boundary;
- this decision introduces multiple phased validation gates and intentionally rejects shortcuts that only appear atomic on the happy path.

## Alternatives Considered

- Option: Register startup as a RuntimeHost module.
- Reason not chosen: Startup must create and validate the Host before the Host can own modules, producing a lifecycle and Tick-ownership cycle.

- Option: Mutate the live session in place and undo changes on failure.
- Reason not chosen: Restore, scene activation, provider state, AppFlow entry, and third-party side effects do not share one reliable rollback mechanism.

- Option: Publish host, scene, resources, and frame cursor independently.
- Reason not chosen: Consumers can observe mixed generations and cleanup cannot have a single owner.

- Option: Share live controllers or Trigger state with the candidate.
- Reason not chosen: Candidate preparation would mutate live authority, create multiple pump/drain owners, and make rollback/retry non-isolated.

- Option: Treat cancellation/timeout as immediate terminal interruption.
- Reason not chosen: Unity and provider operations may be non-cancellable; early rollback would race in-flight work and make cleanup unsafe.

- Option: Rely on ordinary RuntimeHost Dispose after partial Initialize/Start failure.
- Reason not chosen: Host-level state does not identify the actual entered/non-contiguous module set or prove cleanup of a throwing callback's partial side effects.

- Option: Recreate or hand off a session-owned Platform bridge on every publication.
- Reason not chosen: Lifecycle facts could be lost, duplicated, or drained against two sessions during the ownership gap.

## References

- `Docs/APPLICATION_STARTUP_ORCHESTRATION_DESIGN.md`
- `Docs/ARCHITECTURE.md`
- `Docs/API_STANDARDS.md`
- `Docs/QUALITY_GATE.md`
- `Docs/Interfaces/Runtime.md`
- `Docs/Interfaces/AppFlow.md`
- `Docs/Interfaces/Resources.md`
- `Docs/PLATFORM_CONTRACTS_DESIGN.md`
- `Docs/SAVEGAME_PERSISTENCE_DESIGN.md`
- `Docs/GAMEPLAY_TRIGGER_RUNTIME_DESIGN.md`
- `Docs/Decisions/ADR-008-gameplay-trigger-runtime-authority.md`
