# Application Platform Lifecycle Bridge Design

> Status: Design
>
> Issue: #710-D
>
> Implementation: Accepted
>
> Scope: noEngine application-lifetime lifecycle drain, exact-session checkpoint dispatch,
> suspend/quitting persistence policy, and publication/save fencing.

## 1. Decision

`ApplicationPlatformLifecycleBridge` is created once by the application composition root before
the first startup attempt. It owns the only drain claim for one `IApplicationLifecycleSource` and
survives startup failure, rollback, retry, successful publication, and later session replacement.
Unity lifecycle callbacks remain facts-only producers. They never invoke Runtime, Persistence,
Scene, UI, or application policy directly.

The bridge always drains and coalesces lifecycle evidence, including while `Current` is the Boot
sentinel or startup is not at a checkpoint-safe boundary. A single bounded pending epoch retains
suspend, quitting, overflow/resync, lost-range, and saturating count/revision evidence. Resume and
focus changes never erase an already observed suspend or quitting fact. Starting a save moves the
current pending epoch into one in-flight epoch; later evidence accumulates in a fresh pending epoch.

## 2. Authority and dependency direction

```text
Unity callback
  -> Platform.Unity lifecycle reporter
  -> Platform bounded lifecycle source
  -> Application.PlatformBridge (sole application-lifetime drain owner)
  -> exact Current + publisher safe boundary
  -> state-bound Persistence checkpoint policy
  -> RuntimeCheckpointBarrier
  -> RuntimeSaveGameService PlanCheckpointSave / Commit
```

`MxFramework.Application.PlatformBridge` depends only on `MxFramework.Application` and
`MxFramework.Platform`. It defines lifecycle evidence, policy, operation, and publication-fence
contracts. The Persistence-backed default policy lives in
`MxFramework.Application.PersistenceBridge`, which already sits outside Application Core and may
depend on PlatformBridge plus Persistence RuntimeBridge.

Application Core retains one private, marker-typed live-session policy capability in the immutable
`LiveApplicationState`. Candidate creation installs it; `PublishAs` copies the same reference into
the exact published state. It is not reachable through public `Current.Session`, and therefore does
not expose `RuntimeHost`, coordinator, checkpoint barrier, save service, disposal, or cleanup
authority. The Persistence bridge creates the capability only from a builder lease whose service is
proven to use the exact candidate `RuntimeHost` and authoritative coordinator.

The Boot sentinel has no policy capability. It can accumulate pending evidence but can never reach
checkpoint capture or store I/O.

## 3. Safe dispatch and ordering

Lifecycle ordering authority remains `ApplicationLifecycleChange.Revision`, including its lost
range and resync facts. It does not consume the session-local callback sequence on
`ApplicationLiveStateRevisionToken`.

At a caller-declared checkpoint-safe application phase the bridge:

1. drains and merges lifecycle facts up to its bounded per-pump budget;
2. refuses dispatch while the drain budget was exhausted, a save is already active, or no pending
   suspend/quitting evidence exists;
3. enters a publisher safe boundary and reads the exact `Current` there;
4. rejects the Boot sentinel or a state without the exact private checkpoint capability;
5. acquires the save side of the shared publication/save fence inside that exact-state callback;
6. asks the injected policy to begin one checkpoint operation for that exact state and evidence;
7. retains both the operation and save-fence lease until a truthful terminal result.

The Runtime-backed policy must use `RuntimeSaveGameService.PlanCheckpointSave`; it must not call
Runtime save participants directly, clear pending Runtime/Trigger work, or bypass
`RuntimeCheckpointBarrier`. A failed or deferred plan retains pending evidence and does not retry on
every frame. A new checkpoint-worthy lifecycle fact or an explicit retry re-arms it.

## 4. Publication/save fence

Generation compare-and-exchange alone is insufficient. An old session can be the only writer and
still commit generation `G+1` after a replacement based on `G` becomes live, making durable Current
refer to the retired Runtime snapshot.

One neutral fence therefore serializes these authority intervals:

- a lifecycle save holds the save lease from before checkpoint planning until the provider-backed
  commit operation is terminal;
- `LiveApplicationStatePublisher.TryPublish` acquires the publication lease inside its publisher
  gate, before candidate reservation or handoff preflight, and holds it through the Current CAS;
- if either side owns the fence, the other returns a structured retryable/deferred result before
  mutation; no check-then-act window is allowed.

The bridge may request cancellation of a cancelable save when replacement policy asks for it, but
publication remains blocked until the operation reports a truthful terminal state. It never assumes
that cancellation succeeded. A successful old-session save advances the durable head; a candidate
prepared from the earlier generation must consequently be rejected as stale and prepared again.

## 5. Quitting

Quitting is sticky best-effort evidence, not a reliable synchronous flush hook. The Unity callback
only reports the fact. At the next safe application pump the bridge may plan/start or poll at most
one ordinary asynchronous save attempt. It never spins, blocks shutdown, waits for I/O, vetoes quit,
or reports durable success before the underlying operation succeeds. Reliable persistence is based
on earlier suspend/checkpoint opportunities.

## 6. Allocation and failure contract

Lifecycle report, drain, evidence merge, Current observation, empty pump, and conforming active
operation polling are `NoAlloc after initialization`. Checkpoint request construction, immutable
snapshot capture, serialization, provider operation creation, and failure diagnostics are
`LowFreqAlloc` or `AllocByDesign`. The steady path uses no LINQ, temporary collections, string
composition, reflection, exception control flow, `unsafe`, or Unity object lookup.

Unknown enum values, duplicate drain ownership, invalid operation tuples, reentrancy, foreign state
capabilities, and publication/save fence contradictions fail closed with structured diagnostics.
Lost counters and revision bounds saturate; they never wrap.

## 7. Required verification

- one drain owner across Boot, rollback, retry, publication, and replacement;
- Boot suspend/quitting retains evidence with zero checkpoint/store calls;
- ordinary and overflow-hidden suspend facts each cause one conservative save;
- exact Current session service/barrier is used; old/global services are never selected;
- deterministic save-versus-publication races prove atomic mutual exclusion;
- active save plus later evidence yields exactly one successor pending epoch;
- no checkpoint before a successful Runtime tick or while participants are non-quiescent;
- failure, cancellation, timeout, generation conflict, null/contradictory operation, and explicit
  retry retain truthful evidence;
- quitting remains non-blocking best effort;
- warmed no-event pump and conforming operation poll allocate 0 B;
- Trigger sources and pending work are unchanged.
