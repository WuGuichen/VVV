# Application Candidate SessionBridge Interface

> Status: Current

Issue #709-B/C/D candidate Runtime/Resource/AppFlow/Scene bridge and exact-instance Unity scene adapter,
plus the #710-B generic two-phase live handoff and aggregate retirement seam. Persistence policy stays
in the outer `MxFramework.Application.PersistenceBridge`; Boot remains out of scope.

## Assembly and authority

`MxFramework.Application.SessionBridge` depends only on Application, Runtime, and Resources. It has no UnityEngine reference. Runtime grants the bridge an internal exact AppFlow registration query; that query is not public API.

`IApplicationCandidateSessionFactory.Create(owner)` first atomically consumes the transaction's framework-provided `ApplicationCandidateSessionOwnerClaim`, before token allocation or builder invocation. A second call with that transaction, or another transaction sharing the same claim, fails with `GraphAlreadyClaimed` before `Build`. The factory alone attaches the completed graph; transactions expose no attach mutator. A failed build leaves the claim poisoned so the same transaction cannot acquire another generation. Steps only validate the exact claimed owner reference; there is no first-step ownership race. The public graph is a NoAlloc read-only view: readiness, pump ownership, AppFlow/resource status, and `TryGetFrozenHandoffLease`. It exposes no RuntimeHost, AppFlow lifecycle, ResourcePlan session/manager, token, pump mutator, cleanup mutator, publication seam, or `IDisposable` escape hatch.

`ApplicationCandidateFrozenHandoffLease` is one cached, opaque proof created with the graph. Repeated successful queries return the same reference. Its internal exact-once claim accepts only an opaque SessionBridge handoff-owner identity and atomically moves graph ownership from `HandoffFrozen` to `HandoffClaimed`. Repeated, foreign, and racing claims fail; after success startup rollback is rejected and graph state is unchanged. The public surface contains only `IsValid`/`IsClaimed` observations and no live pump or publication capability; #710 may consume this internal checked transfer seam.

#710-B extends that internal seam with a prepared-owner reservation. Repeatable preparation keeps
graph and optional scene observably `HandoffFrozen`, blocks an interleaved rollback race, and can be
cancelled only before preparation succeeds. A successful preparation begins the publisher's
irrevocable no-failure interval; ordinary disposal/rollback cannot clear it. Prepared final claim is a NoAlloc/nonthrow owner transfer with no
fallible work between it and Application's live-state CAS. Each graph also owns one opaque
`ApplicationCandidateRuntimeCompositionIdentity`; the Persistence outer bridge supplies it and the
exact graph RuntimeHost to a coordinator builder and rejects any build lease that does not echo both
references exactly.

The builder returns an inert `IApplicationCandidateSessionBuildLease`. Transfer is exact once. Before transfer the factory rejects cross-role reference aliases within the same build and reference reuse from an earlier generation. On rejection it always calls ownership-aware `Abandon(reusedMask)`: reused identities transferred by an earlier generation are protected, while all fresh owners from the rejected build are released. Cleanup exceptions are aggregated with the primary construction error.

The optional scene descriptor owns a fresh `SceneFlowController`, exact RuntimeModule/Standalone pump declaration, and isolation gate. Runtime exposes the controller's actual driver reference only to trusted friend assemblies; SessionBridge additionally requires that driver to implement `IApplicationCandidateOwnedSceneDriver`. The driver and isolation gate must return the same non-null, stable `ApplicationCandidateSceneOwnershipIdentity` reference. This identity is read-only, carries no scene authority, and is checked both when the scene descriptor is constructed and when the session graph is claimed. `IApplicationCandidateSceneTransaction` exposes one stable read-only attachment port; only successful preparation attaches the read-only scene lease.

## Forward and rollback

| Step | Forward responsibility | Rollback responsibility |
| --- | --- | --- |
| Resource | Prepare and exact-once acquire the sealed plan | Permanently poison `CleanupFailed`; never retry it into `Released` |
| Scene | Enter isolation, then start Additive load with `UnloadPreviousScene=false` | Make inert, asynchronously unload, preserve exact SceneFlow/isolation errors |
| Runtime lifecycle | Explicit Initialize stage, then Start stage | Dispose and inspect new Stop/Dispose ledger errors under every RuntimeHost policy |
| AppFlow | Prepare through an assembly-owned lease | Drain the same nested fault handle before starting compensation; cache one rollback operation |
| Handoff | Validate all unticked readiness, then freeze | Transfer pump ownership to rollback without publication |

The AppFlow lease has stable states `Created`, `Preparing`, `Prepared`, `RollingBack`, `RolledBack`, `Debt`, and `Disposed`. Final cleanup cannot dispose AppFlow until rollback is truthfully terminal. Repeated rollback returns the same operation and repeated finalization does not repeat AppFlow dispose or resource-graph finalization.

Candidate scene v0.1 accepts only Additive load with `UnloadPreviousScene=false`. Cancellation is latched. The same non-cancellable load is drained to terminal; late success starts exactly one unload, and cancellation becomes terminal only after unload succeeds. After a terminal load failure, the isolation gate's NoAlloc `HasOwnedScene` observation distinguishes a failure with no created scene from a failure that still owns an exact candidate scene: owned or ownership-uncertain failures preserve the forward error, become inert, and drain exact unload before becoming terminal or allowing resource release. Getter faults remain fail-closed on the same operation.

Scene pump ownership follows `StartupOwner -> HandoffFrozen -> HandoffClaimed` or `StartupOwner/HandoffFrozen -> RollbackOwner -> Disposed`. RuntimeModule and Standalone ownership are mutually exclusive and are checked before scene I/O.

Concrete successful publication promotes graph and scene together from `HandoffClaimed` to
`LiveOwner`. Retirement uses the distinct `LiveOwner -> RetirementOwner -> Disposed` path and drains
Runtime, AppFlow, Scene, and Resources in that stable order. Candidate rollback continues to use
`RollbackOwner`; the two owner domains are not aliases. Ordinary rollback rejects both `LiveOwner`
and `RetirementOwner`; only the retirement operation's internal capability may drain the latter.

## Live frame pump

The live lease privately implements Application's cross-assembly runtime-pump capability; neither
`ApplicationCandidateSessionGraph`, the public session view, nor Persistence restore APIs expose
that authority. Publication binds the already frozen `NextRuntimeFrame` to the same aggregate state
as the lease.

For every accepted frame the aggregate pump calls the exact candidate `RuntimeHost` first. A
`RuntimeModule` AppFlow or SceneFlow controller is pumped only by that Host and is never called
directly. A `Standalone` controller is pumped only after the Host returns normally, in fixed
`AppFlow -> SceneFlow` order, exactly once each. A real Host attempt consumes the frame even when a
FailFast exception escapes; standalone pumps do not run after that exception and the frame is not
retried. Frozen strategy validation remains the proof that no controller can be driven by both
paths.

The bridge reports the exact Host attempt outcome from `HasTickAttempt`,
`LastTickAttemptFrame`, and `LastTickAttemptSucceeded`. Collect-policy failures therefore consume
their frame while surfacing `RuntimeAttemptFailed`. Its warmed live aggregate pump and fixed
standalone AppFlow-before-Scene sequence are `NoAlloc`.

## Unity exact-scene adapter

`MxFramework.Application.SessionBridge.Unity` is the Unity-facing adapter. Its factory creates a fresh controller, driver, isolation gate, and ownership identity. It accepts only canonical `Assets/.../*.unity` paths, Additive load, `UnloadPreviousScene=false`, an immutable manifest, and a provider-issued approval proof bound to the observed artifact id, path, content hash, revision, and isolation policy. The configured artifact provider is a trusted authority: `Preflight` must validate the actual artifact, and `Acquire` must atomically guard against a preflight/acquire content change by returning a lease that holds the exact same proof reference. A provider that merely echoes manifest values does not satisfy the production contract.

Before `LoadSceneAsync`, the adapter rejects an already-loaded exact path and owns a process-wide token only for the active Unity scene-mutation operation. A valid load terminal releases that token while retaining the exact scene and artifact lease, so a different-path next candidate may prepare while the old live scene remains loaded. Unload claims the token again; contention returns one pending operation that retries only on the Unity main thread instead of converting transient `Busy` into terminal cleanup debt. v0.1 deliberately rejects two simultaneously loaded generations of the same exact scene path. It captures the new `Scene` from `sceneLoaded` against the pre-load handle baseline, validates the exact root count, one exact marker per root, and serialized inactive roots, and never calls `SetActiveScene` or activates candidate roots. Unload uses only `SceneManager.UnloadSceneAsync(exactScene)`; path/name lookup and fallback are forbidden. Artifact release occurs exactly once and only after the exact `Scene` is invalid or no longer loaded. A release exception, missing/ambiguous scene callback, or uncertain additional scene keeps permanent cleanup debt and blocks later candidate mutation. This state requires process/session teardown and operator diagnostics; the adapter does not guess a path/name recovery target.

Every Unity API call, operation poll, callback finalization, root validation, and release transition is restricted to the captured Unity main thread and synchronization context. Wrong-thread observation is latched and becomes a main-thread terminal forward failure without performing Unity work on the foreign thread; once exact ownership is re-established on the main thread, normal exact cleanup remains available and the mutation token need not be permanently poisoned.

The committed Unity fixtures are test assets and are intentionally absent from production `EditorBuildSettings`. Focused PlayMode validation first runs `ApplicationCandidateUnitySceneFixtureGenerator.GenerateFromCommandLine`, which temporarily appends the approved candidate and same-name decoy scenes in that validation workspace. Those generated BuildSettings entries are validation state, not a deliverable.

If a nested AppFlow `Tick` throws and implements `IApplicationStartupFaultDrainOperation`, the wrapper calls `RequestFaultDrain` and continues `TickFaultDrain` on that same handle until its status is terminal. Even if the request itself throws, the drain tick remains authoritative. A non-terminal handle without a usable drain remains non-terminal and fail-closed; the bridge never starts concurrent compensation or releases dependencies.

## Cleanup and diagnostics

Session finalization starts no operation or I/O. Cleanup order is fixed: make the scene inert, reach scene unload terminal, release the resource plan, then finalize the resource graph. Resource rollback refuses plan release while the scene is active or has unload debt. Cleanup aggregates RuntimeHost, AppFlow, Scene, ResourcePlan, ResourceGraph, and PumpOwner debt.

`ApplicationCandidateSessionErrorDetail` preserves broad bridge classification plus forward nested RuntimeOperation error/detail/exception, resource preparation error (including ResourceError), and separate cleanup nested error/detail/exception. Multiple cleanup exceptions are retained with `AggregateException` on low-frequency fault paths.

`ApplicationCandidateSceneErrorDetail` preserves complete forward and cleanup `SceneFlowError` values in separate slots, plus isolation results, exceptions, stable phase/code, and typed Scene cleanup debt.

## Allocation behavior

- Factory, graph, cached frozen proof, build lease, step operation, and terminal diagnostics: `LowFreqAlloc` or `AllocByDesign`.
- Public graph state, Runtime exact-registration seam, lease status/progress/error, and operation getters: `NoAlloc` after initialization.
- Steady resource prepare/release and nested operation pumping: `NoAlloc` outside implementation-owned diagnostics.
- SceneFlow current-operation/type/key/progress reads, isolation `HasOwnedScene`, and warmed candidate scene polling: `NoAlloc`; isolation transitions, operation creation, and terminal diagnostics are low-frequency allocation paths.
- Unity ownership observations and steady waiting/started operation polls are `NoAlloc` after initialization. During unload-token contention, the first successful main-thread operation poll also claims the process mutation token and starts the exact-scene unload; that contention-to-start transition has mutation semantics and is `AllocByDesign`, rather than a passive getter read. Manifest/proof/lease creation, pre-load handle baselines, one-time root validation, and fault diagnostics are `LowFreqAlloc` or `AllocByDesign`; no root scan or temporary collection is created per frame.
- All public enums have explicit stable numeric values; cleanup debts and build reuse masks are stable flags.

The concrete Persistence restore/publication transaction is documented in
`Docs/Interfaces/ApplicationPersistenceBridge.md`.
