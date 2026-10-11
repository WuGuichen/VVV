# Application Persistence Candidate Bridge

> Status: Current
>
> Assembly: `MxFramework.Application.PersistenceBridge`

## Authority and dependency direction

`MxFramework.Application.PersistenceBridge` is the noEngine outer composition layer that joins
`MxFramework.Application`, `MxFramework.Application.SessionBridge`,
`MxFramework.Persistence.RuntimeBridge`, and `MxFramework.Runtime`. None of those inner assemblies
references this bridge. Persistence Core remains unaware of Application, candidate graphs, scenes,
resources, and live publication.

`ApplicationCandidateSessionGraph` creates one opaque
`ApplicationCandidateRuntimeCompositionIdentity` per generation. The public
`ApplicationPersistenceCandidateSessionFactory` supplies that unforgeable identity and the graph's
exact `RuntimeHost` reference to an `IApplicationPersistenceCandidateCoordinatorBuilder`. Creation
accepts only a strong-exception build lease that echoes both references exactly, exposes an
authoritative profile-bound coordinator, and exposes one concrete `RuntimeSaveGameService` whose
checkpoint barrier is reference-bound to that same exact RuntimeHost, coordinator, and sibling
restore profile. The load service that prepared the candidate is never reused as the live checkpoint
service. The capability is validated before ownership transfer. Build-lease property values are
cached once before validation. `Abandon` is an idempotent no-throw cleanup contract and is invoked
exactly once after any pre-transfer contradiction or transfer failure. A public caller cannot create a
composition by presenting an arbitrary graph/coordinator tuple.

## Restore and publication transaction

`ApplicationPersistenceCandidateSession` is the concrete candidate restore target and unpublished
candidate cleanup owner. It never ticks the candidate Runtime/AppFlow/Scene. Its restore lease is
prebuilt during session construction, so acquisition validates and reserves in one critical section
without allocation:

- the prepared plan profile, target lease profile, and coordinator profile must be reference-equal;
- the graph must still be frozen, ready, and unticked;
- only one restore lease can be acquired;
- incomplete lease disposal permanently poisons the session before starting best-effort rollback;
- completion accepts only completed frame `F` and exact next frame `F+1`;
- completion atomically commits the validated save profile, slot, authoritative original head,
  selected recovery generation/source, persisted UTC creation time, `F`, and `F+1`;
- restore failure, target contradiction, invalid frame, or candidate validation failure creates no
  `LiveApplicationState` and cannot claim the graph/scene handoff.

After successful `RuntimeSaveGameService.RestorePrepared`,
`TryCreatePublicationCandidate` creates one cached unpublished `LiveApplicationState`. Its
`NextRuntimeFrame` is the restored `F+1`; repeated calls return the same candidate. No RuntimeHost,
AppFlow, or SceneFlow tick occurs before publication.

The session preallocates one private checkpoint capability during construction, initializes it
without allocation in the successful restore-completion critical section, and copies the exact same
marker into the published state. It retains only the candidate-bound checkpoint service plus exact
profile/slot/original-head/creation-time facts. Boot and incomplete candidates have no initialized
checkpoint authority, and public `Current.Session` cannot reach the marker or its service.

`ApplicationPersistenceDurableHeadAuthority` is the application-lifetime profile/slot/head/save-epoch
authority for one active durable domain. Candidate builders for the same profile and slot share that
exact instance. Under the publication fence, the candidate capability validates replacement against
the current capability before any reservation or handoff mutation: Boot accepts one initialized,
currently durable candidate; an unknown capability fails closed; the same profile/slot requires the
same authority instance and current epoch; a different slot may replace through its independently
current authority. Consequently a fresh authority cannot disguise a same-slot stale candidate, while
an intentional slot switch is not coupled to the previous slot's epoch.

## Lifecycle checkpoint policy

`ApplicationPersistencePlatformLifecycleBridgeFactory` is the only public entry to the
Persistence-backed implementation. It creates the long-lived platform bridge with an internal
`ApplicationPersistenceLifecycleCheckpointPolicy`; callers cannot invoke the exact service policy
outside the publisher safe boundary and shared save fence. The internal policy accepts only the
private capability reference carried by
the exact state passed at the publisher safe boundary. It reads UTC metadata through `IWallClock`
and `PlatformUtcClock`, asks an injected
`IApplicationPersistenceLifecycleCheckpointRequestPolicy` to create the low-frequency request, then
revalidates exact profile, slot, original head, persisted creation time, and UTC timestamp shape
before calling the capability's exact `RuntimeSaveGameService`.

The default request policy preserves persisted creation time, clamps a backwards non-authoritative
wall-clock observation to that creation time for metadata validity only, and supplies empty tags and
display metadata. Wall-clock values never select a generation or resolve a conflict.

The wrapper retains the provider-backed Runtime bridge operation and reports only truthful terminal
states. Cancellation is a cooperative request and never implies terminal cancellation. A successful
commit must return exactly `ExpectedHead + 1` before the private capability advances its head once;
failure, cancellation, timeout, and generation conflict leave the head unchanged.
`GenerationConflict` remains a distinct lifecycle operation status. Checkpoint rejection, pending
quiescence, and an already reserved slot defer without starting store I/O; malformed requests,
foreign capabilities, invalid clocks, and contradictory operation tuples fail closed.

Head advancement updates the shared durable authority before the current capability's local head.
Any same-slot candidate prepared from the previous generation therefore remains permanently stale:
publication retries return `PublicationConflict` without claiming its graph, disposal rolls it back,
and a newly prepared candidate from the advanced head binds the new epoch and may publish.

## Two-phase handoff

Publication uses the Application publisher's two-phase handoff contract:

1. the publisher completes revision, published-state, and retirement-receipt allocation;
2. repeatable preflight reserves the exact opaque handoff owner while graph and scene remain
   `HandoffFrozen` and rollback-owned;
3. no fallible work remains;
4. final claim performs only the prepared scene/graph ownership writes, then the publisher executes
   its single live-state compare-exchange and enqueues the already-built retirement receipt.

Final claim returns an invariant proof. A contradictory false result permanently fails that candidate
before the publisher compare-exchange; `Current` and the retirement queue remain unchanged.

Dispose may poison and roll back only before preflight wins. Successful preflight is the publisher's
irrevocable no-failure boundary: subsequent session disposal is a no-op and only the publisher's final
claim may consume the prepared reservation. The unpublished session does not expose live lease,
handoff, or retirement-source capabilities; those are held by the candidate's internal aggregate
lease. Successful concrete publication promotes graph and optional scene together from
`HandoffClaimed` to `LiveOwner`, after which session status derives `Published` from that owner.

Poisoning creates one cached `CandidateCleanupOperation` and performs only an initial best-effort
tick. The Application transaction/driver that owns the unpublished candidate must continue calling
`TickCandidateCleanup` until the operation is terminal. This is required for asynchronous scene
unload and resource release; `Poisoned` describes publication invalidation, not cleanup completion.

## Retirement

The internal live aggregate implements `IApplicationLiveRetirementSource`; the public restore session
does not expose that capability. Only the publisher-owned retirement receipt may begin live cleanup.
Retirement has a distinct
`LiveOwner -> RetirementOwner` transition for both graph and scene and then drains ownership in the
fixed order:

1. RuntimeHost Stop/Dispose;
2. AppFlow compensation and dispose;
3. scene make-inert and exact asynchronous unload;
4. ResourcePlan release and resource-graph finalization.

The operation preserves canonical warning order `runtime`, `app-flow`, `scene`, `resources`. A failure
does not skip later best-effort domains; the first remaining owner becomes typed ownership debt and
keeps the publisher retirement reservation poisoned. Scene retirement makes the scene inert before
starting exact unload. If that synchronous isolation transition fails, the first retirement tick
immediately exposes `scene.make-inert-failed` ownership debt while later cleanup still attempts the
exact unload and keeps resources retained until scene ownership is truthfully discharged.

## Exact frame binding

Successful restore completion freezes checkpoint `F` and checked continuation `F+1` before the
publication candidate is created. The candidate's internal live lease is the sole runtime-pump
capability, and the same immutable `LiveApplicationState` binds that capability to `F+1`.
Persistence exposes only the completed/next frame facts; it cannot Tick, reseed, or obtain the live
pump. Publication failure, stale expected state, disposal, and retry therefore leave the current
driver cursor unchanged.

The first live Tick after publication attempts exactly `F+1`, the next attempts `F+2`, and a real
failed Host attempt still consumes its frame. `F = long.MaxValue` remains invalid because no `F+1`
exists.

## Allocation behavior

- Builder, build lease, session, prepared/load work, publication-candidate creation, and retirement
  operation creation: `LowFreqAlloc` or `AllocByDesign`.
- Restore acquisition/completion, exact profile checks, candidate state reads, repeatable handoff
  preflight after warm-up, final handoff claim, and steady retirement polling: `NoAlloc after
  initialization` outside implementation-owned failure diagnostics.
- The bridge contains no `unsafe`, UnityEngine, UnityEditor, LINQ, reflection, or per-tick object
  lookup.

## Validation boundary

Focused noEngine/EditMode tests cover exact sibling profile acquisition, incomplete-lease poison,
restore through `RuntimeSaveGameService`, candidate zero Tick, `F -> F+1`, repeatable frozen preflight,
stale publication reuse, exact graph/scene live claim, preflight/dispose linearization, poisoned
candidate cleanup pumping, ordered asynchronous scene retirement, exact live `F+1` driver
continuation, exact-current lifecycle dispatch, publication/save fencing, and stale durable-generation
replacement rejection. Unity Boot/loading UI and Playable save/load evidence remain later #710 stages.
