# Persistence Core And Runtime Bridge

> Status: Current
> Version: 0.1 | 2026-10-01
> Assemblies: `MxFramework.Persistence`, `MxFramework.Persistence.RuntimeBridge`

## Authority and dependencies

`MxFramework.Persistence` owns save-game profile/slot/generation identities, exact v1 envelope framing and integrity, asynchronous-shaped store operations, and provider-neutral CAS semantics. The assembly has no Unity or framework-module references, uses no `unsafe`, and does not interpret Runtime/config/content compatibility. The separate noEngine siblings `MxFramework.Persistence.FileSystem` and `MxFramework.Persistence.RuntimeBridge` respectively own filesystem-root preflight and the B2 Commit/Delete/Read/List store slice, and durable checkpoint/commit, prepare-load and unpublished-candidate restore. Platform policy, Settings, Trigger, encryption/compression, Application publication, and live-session selection remain outside Persistence Core.

## FileSystem provider preflight and B2 store

`MxFramework.Persistence.FileSystem` is a noEngine sibling assembly with one-way references to Persistence and Platform. Its current public surface is:

```csharp
FileSystemSaveGameStoreFactoryCreateResult FileSystemSaveGameStoreFactory.TryCreate(
    PlatformLocation location);

ISaveGameStore FileSystemSaveGameStoreFactory.CreateStore();

FileSystemSaveGameStoreFactoryCreateResult.Success
FileSystemSaveGameStoreFactoryCreateResult.Factory
FileSystemSaveGameStoreFactoryCreateResult.CapabilityEvidence
FileSystemSaveGameStoreFactoryCreateResult.Error

FileSystemCapabilityEvidence.CreateNewFile
FileSystemCapabilityEvidence.SameDirectoryMove
FileSystemCapabilityEvidence.NonOverwritingMove
FileSystemCapabilityEvidence.DurableFileFlush
FileSystemCapabilityEvidence.ExclusiveWriterLock
FileSystemCapabilityEvidence.HasRequiredPublicationPrimitives
```

`TryCreate` accepts only a writable `FileSystemPath`. URI, provider-owned, readonly and invalid locations fail before any filesystem write. For a valid location it creates a one-use probe directory under the configured root and verifies create-new refuses an existing path without changing its bytes, two-argument same-directory move removes the source and preserves exact target bytes, move refuses an existing target while preserving both files, the backend exposes `FileStream.Flush(true)`-equivalent durable file flush, and a same-path exclusive writer lock follows `Acquired -> Contended -> Acquired` across double-open and release. Probe cleanup is part of creation: cleanup failure makes `Success=false` even if all required evidence fields are true.

Only `FileSystemSaveGameStoreFactoryCreateResult.Success` is the authoritative complete creation outcome. Individual evidence fields are immutable diagnostics about one configured root, backend and probe instant. They do not prove power-loss durability, future availability, another volume, network/cloud filesystems, WebGL, consoles or provider-owned storage. A successful factory can create independent stores that share one root-scoped capacity of eight active operations; Commit/Delete share the configured root/backend and coordinate same-slot writes across instances through each slot directory's `.writer.lock`, while different slots may publish concurrently. Physical paths and path-bearing exceptions stay in the internal diagnostics sink; public `SaveGameError` contains only stable logical paths/messages and `Exception=null`.

The v1 layout rejects reparse points and junctions that exist when the root-to-slot chain is validated. It does not claim protection against a malicious local process replacing a path component during an operation; that local TOCTOU attack is outside the v1 threat model.

Commit and Delete validate canonical v1 envelopes/tombstones, enforce strict highest-generation CAS, write same-slot create-new staging, durable-flush and byte-verify it, then publish with a same-directory non-overwriting move. Move ambiguity is reconciled from the final path: exact final bytes are success, any present non-exact final is `GenerationConflict` and is never deleted, and an absent final preserves the sanitized move-failure category such as `PermissionDenied`, `QuotaExceeded`, or `IoFailure`. Cancellation before publication returns Cancelled and removes staging; after publication it cannot replace success.

Read treats the strict highest generation as the authoritative Current head. Exact reads require `PrimaryOnly` and never fall back. Current with `PrimaryThenLastKnownGood` may scan downward only when the authoritative primary fails with a bounded-record corruption class: invalid envelope, integrity failure, actual storage length over the envelope limit, declared framing/header/payload length over its limit, or decoded path/envelope identity mismatch. These length failures are detected before any over-limit allocation. Unsupported version/feature/transform, I/O and permission failures never enable recovery. During the downscan, corrupt records may be skipped, the first valid Save succeeds as `LastKnownGood`, and the first fully decoded Tombstone or unsupported record is a barrier. A raw tombstone-looking header is not a barrier until complete decode and profile/slot/generation mirror validation succeeds.

List inspects each slot's strict authoritative head, sorts logical slot IDs ordinally, derives `UpdatedAtUtc` from the decoded envelope, excludes tombstones by default, and exposes them only when `includeDeleted=true`. A malformed reserved profile/slot/generation entry or any corrupt/unsupported head fails the whole List operation; it never returns a partial diagnostic snapshot. Non-reserved entries and staging/lock artifacts are ignored.

After successful Commit/Delete publication, retention best-effort keeps the head plus the nearest lower fully decoded and identity-matched Save or Tombstone. Corrupt or unsupported older records are not retention candidates and may be cleaned. Any I/O or permission uncertainty stops cleanup and emits the single typed `RetentionCleanupFailed` warning; publication remains Succeeded and is never rolled back.

Allocation: factory/location/layout/probe/store creation and Commit/Delete/Read/List work are `LowFreqAlloc` or `AllocByDesign`; evidence/result/property inspection, operation polling and terminal cancellation are `NoAlloc` after initialization. The BCL backend maps only known platform disk-full/quota native codes (`39`/`112` on Windows, `28`/`122` on Linux/Android, `28`/`69` on Apple/BSD) to `QuotaExceeded`; injectable backends may provide the same typed internal classification. Unknown filesystem `IOException` remains conservatively `IoFailure`.

## Exact v1 envelope

The wire contract follows ADR-010: `MXSVGM01`, little-endian version/flags/header/payload lengths, strict canonical UTF-8 JSON, payload SHA-256, and an envelope SHA-256 trailer. Decoder preflight rejects encoded input beyond `MaximumEnvelopeBytes`, header beyond 64 KiB, and payload beyond 64 MiB before payload allocation. Unknown flags/version, non-canonical JSON, malformed UTF-8, length mirrors, hashes, and record-kind mirrors fail closed. A non-empty transform chain returns `UnsupportedTransform`; v1 only supports identity.

`SaveGameBuildDescriptor.TryCreate`, `SaveGameEnvelope.TryCreateSave`, and `TryCreateTombstone` provide structured non-throwing construction. Descriptor/envelope hard-limit failures return `LimitExceeded`; other invalid inputs return a structured validation error. Their constructors and `CreateSave` / `CreateTombstone` remain programmer-facing throwing APIs. Envelope, metadata, payload, encoded bytes, list arrays, and store reads isolate ownership by copying at public boundaries. Save records accept `RuntimeSchemaVersion` in `0..int.MaxValue`; zero is a valid Runtime schema, while tombstones retain their schema-zero/no-payload sentinel shape. `RuntimeFrame` is a non-negative `long` and encodes as a canonical quoted integer.

## Operations and result validity

`SaveGameEnvelopeResult.IsSuccess` is explicit: it requires no error plus non-null encoded and logical envelope values, so `default(SaveGameEnvelopeResult)` is never successful. `ISaveGameOperation<TResult>` likewise exposes terminal status, `IsDone`, `CanCancel`, structured error, `HasResult`, `Result`, and `TryGetResult`. Callers must only consume `Result` when `HasResult` or `TryGetResult` succeeds; failed, cancelled, timed-out, and default operations/results do not expose a valid result.

Succeeded/Failed/Cancelled/TimedOut operations are terminal (`IsDone=true`, `CanCancel=false`). Cancellation is cooperative only before the provider atomic publication boundary. A provider may report `Running && !CanCancel` while atomic publication or an uncancellable cleanup/drain is still in progress; callers continue polling until a terminal state. After publication, success remains success even if cancellation arrives. TimedOut requires `Error.Code=Timeout`, but Persistence Core owns no default timeout, wall-clock timer, or implicit deadline: only an explicit provider or upper-layer policy may produce it. The current in-memory provider completes synchronously but preserves the asynchronous-shaped contract for future providers. Property reads, `Poll`, terminal `RequestCancel`, `IsDone`, and `CanCancel` are `NoAlloc`; envelope/store work is low-frequency `AllocByDesign`.

Successful commit/delete results carry a typed `SaveGameWarning`. `None` is the normal outcome. `RetentionCleanupFailed` means immutable publication already succeeded and only best-effort old-generation cleanup failed, so the operation must remain Succeeded and must not roll back the new head. One or many cleanup failures in a single operation aggregate to this singular warning; per-file details remain provider-local. This warning is a closed value with `Retention` phase and logical `retention` path; it intentionally has no message or exception. Cleanup exceptions and physical locations remain provider-local diagnostics.

## Store semantics

`InMemorySaveGameStore` provides thread-safe compare-generation publication. Commit/delete recheck cancellation immediately before publication, execute the optional fault seam outside the store lock, and revalidate the authoritative head under the lock; a reentrant fault injector therefore cannot bypass CAS. At most eight operations may be active concurrently.

Provider-facing Store errors include stable `InvalidLocation=1302`, `AtomicCommitUnsupported=1303`, `PermissionDenied=1304`, `QuotaExceeded=1305`, and `IoFailure=1306` values. Provider public `SaveGameError.Path` values are stable logical tokens such as `location`, `staging`, `flush`, `verify`, `publish`, or `retention`; physical paths, user directories, filenames, and URIs must not cross the error/warning contract.

The provider retains current plus one previous generation. Tombstones advance generation, prevent generation reuse, make default Current/Exact tombstone reads return `SlotDeleted`, and are excluded from default listing. `SaveGameListRequest(includeDeleted: true)` is a diagnostics-only view that includes tombstone summaries through `RecordKind`/`IsDeleted`. Exact-generation reads never fall back. Slot listing is ordinal by slot ID.

The additive five-argument `SaveGameReadRequest(..., recoveryPolicy, includeDeleted: true)` explicitly returns a fully validated selected tombstone envelope as a successful `SaveGameReadResult`, preserving `OriginalGeneration`, `SelectedGeneration` and `Primary` recovery source for generation-aware recreation. The existing constructor defaults to `false` and preserves `SlotDeleted`. This opt-in never crosses a tombstone during recovery: a corrupt primary above a decoded tombstone still fails, and exact reads still reject a fallback policy. Both the InMemory and FileSystem providers support this option. Consumers must decode `RecordKind` before interpreting payload/codec; a tombstone is not a Save with an empty body.

CharacterTest tactical slots read their target directly with this option. Only typed `SlotNotFound` initializes an unpublished head, and a validated current tombstone initializes empty content while retaining its published CAS head. LKG reads retain the authoritative `OriginalGeneration`, not the older selected generation. Corruption, unsupported records, permissions, I/O and unknown Save codecs block loading without replacing player data; damage to another profile slot does not require or block target-slot reads.

`SaveGameRecoveryPolicy` is frozen as `PrimaryOnly=0` and `PrimaryThenLastKnownGood=1`. Recovery is valid only with `Current`; exact-generation requests must use `PrimaryOnly`, and stores reject unknown policy values instead of silently choosing a behavior.

## Runtime bridge Phase 3

`MxFramework.Persistence.RuntimeBridge` exposes `IRuntimeSaveGameService` / `RuntimeSaveGameService` with four explicit low-frequency boundaries:

- `PlanCheckpointSave(RuntimeSaveGameCheckpointRequest)` synchronously captures only through `RuntimeCheckpointBarrier`, validates the authoritative root against the injected `SaveGameBuildDescriptor` and `RuntimeSaveStateRestoreProfile`, reserves one profile/slot writer, and returns an immutable `RuntimeSaveGameWritePlan` lease. Capture or provenance failure never starts store I/O and never returns a partial plan.
- `Commit(RuntimeSaveGameWritePlan, CancellationToken)` transfers exact ownership once, serializes through bounded Runtime UTF-8 JSON, creates/encodes the v1 envelope, and delegates compare-generation publication to the injected `ISaveGameStore`. The wrapper mirrors provider status/cancellation and releases the plan reservation on every terminal path.
- `PrepareLoad(RuntimeSaveGameLoadRequest, CancellationToken)` reads and re-decodes the selected envelope, validates profile/slot/generation/recovery provenance, codec and mirrored Runtime root facts, and invokes the injected compatibility checker fail-closed. It then creates a fresh sibling from the service's frozen template profile and binds the migrated/validated `PreparedRuntimeSaveGame` plan to that per-load identity. It does not invoke a restorer or mutate a live Runtime.
- `RestorePrepared(PreparedRuntimeSaveGame, IRuntimeSaveGameRestoreTarget)` atomically acquires an exclusive candidate lease for the exact restore profile, invokes authoritative `RuntimeSaveStateCoordinator.RestorePrepared`, and completes the candidate with completed frame `F` and next frame `F+1`. Success returns an immutable `RuntimeSaveGameRestoreResult` containing the acquired `TargetId`, completed frame and next frame. Failures after lease acquisition preserve that same `TargetId` in `RuntimeSaveGameBridgeErrorDetail`. It never publishes or Ticks the candidate.

`RuntimeSaveGameService` uses two related but distinct restore-profile identities: the injected template is reference-equal to the live checkpoint barrier coordinator profile and supplies capture/compatibility authority; every successful prepare creates a new sibling with the same frozen descriptor/migration topology for one candidate composition. `IRuntimeSaveGameRestoreTarget.TryAcquireRestoreLease(...)` combines candidate validation and reservation in one critical section instead of exposing an `IsPublished` check. `IRuntimeSaveGameCandidateRestoreLease` exposes the candidate id, authoritative coordinator and exact per-load sibling profile; `TryCompleteRestore(in RuntimeSaveGameRestoreCompletion, ...)` is exact-once. Disposing an incomplete lease must atomically and permanently poison the candidate before cleanup so it cannot later be published or reacquired. Contradictory third-party target/lease results fail closed with `RuntimeSaveGameTargetValidationError`.

`PreparedRuntimeSaveGame` also preserves the validated selected envelope's UTC creation time.
Restore completion now receives one readonly `RuntimeSaveGameRestoreCompletion` value containing the
profile, slot, authoritative original head, selected generation and recovery source, creation time,
completed frame, and exact next frame. The target commits those facts atomically with completion;
after last-known-good recovery, future CAS uses `OriginalGeneration`, never the older selected
generation. Internal composition validation can prove that a checkpoint service's barrier retains
the exact RuntimeHost, authoritative coordinator, and sibling profile without exposing those live
authorities through the public service API.

`RuntimeSaveGameWritePlan` and `PreparedRuntimeSaveGame` are owner-bound, idempotent disposable leases. Cross-service ownership, use after release, duplicate transfer/restore and invalid state transitions return structured `RuntimeSaveGameBridgeErrorDetail`; participant failures remain Runtime-owned and Trigger payload/details stay opaque. Compatibility defaults to `Unknown` and therefore fails closed unless an injected checker makes an explicit compatible or migration-required decision.

Runtime bridge failures also map into the public stable Persistence error space without renumbering existing values: `RuntimeCheckpointRejected=1100`, `RuntimeSerializationFailed=1202`, `RuntimeCompatibilityFailed=1500`, and `CandidateRestoreFailed=1600`. Provider/store failures retain their original Persistence error. Bridge-specific detail, Runtime participant detail, target validation detail and acquired candidate `TargetId` remain available for diagnostics.

Public operation property reads, steady-state `Poll`, terminal `RequestCancel`, target validation/acquisition and lease disposal are `NoAlloc` after initialization when providers/targets honor their contracts. Request/result/plan/prepared creation, checkpoint capture, JSON/envelope work, provider I/O and participant restore are `LowFreqAlloc` or `AllocByDesign`; none belongs in `RuntimeHost.Tick`.

The separate noEngine `MxFramework.Application.PersistenceBridge` now supplies the concrete
candidate build binding, exact-profile restore target, zero-Tick validation, `F -> F+1` aggregate,
two-phase SessionBridge handoff, Application live-state publication capability, caller-pumped
unpublished cleanup, and an internal ordered live-retirement source held only by the publisher.
Restore/validation/contradiction failure does not create or publish a live
candidate, and the previous live state remains unchanged. This closes the noEngine production-safe
restore/publication boundary. It does **not** yet provide the unique global frame driver, Unity
Boot/loading shell, suspend policy, or Playable save/load surface; those require separate #710
stages and evidence.

## Validation

Core focused tests live under `Packages/com.vincent.wgameframework/Tests/Persistence/` and cover frozen ID/generation/error numeric values, golden v1 bytes, malformed UTF-8/framing/hash/transform inputs, descriptor/envelope creation limits, default and explicit result validity, terminal operation/cancellation semantics, operation polling NoAlloc, typed warning behavior, buffer ownership, cancellation-before-publication, true concurrent CAS, fault injection/reentrancy, retention, tombstones, exact reads, and diagnostic listing. Filesystem focused tests live under `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/PersistenceFileSystem/` and cover strict lowercase UTF-8 hex layout, containment/reparse rejection, invalid-location zero writes, primitive-semantics counterexamples, diagnostic isolation, cleanup failure, fixed-volume BCL evidence, Commit/Delete validation and publication, move ambiguity, cancellation boundaries, root-scoped cross-store capacity, same-/cross-store CAS, Current/Exact reads, bounded LKG recovery and barriers, whole-result List behavior, and post-publication retention warnings.

Runtime bridge focused tests live under `Packages/com.vincent.wgameframework/Tests/Persistence/RuntimeBridge/`. Final test counts and changed-file code-standard evidence are recorded from the exact delivery commit during closeout; earlier candidate counts are not carried forward as final evidence.

## References

- `Docs/SAVEGAME_PERSISTENCE_DESIGN.md`
- `Docs/Decisions/ADR-010-savegame-persistence-authority.md`
- `Packages/com.vincent.wgameframework/Persistence/`
- `Packages/com.vincent.wgameframework/Persistence/RuntimeBridge/`
- `Packages/com.vincent.wgameframework/Tests/Persistence/`
- `Packages/com.vincent.wgameframework/Persistence.FileSystem/`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/PersistenceFileSystem/`
- `Packages/com.vincent.wgameframework/Tests/Persistence/RuntimeBridge/`
- `Docs/Interfaces/ApplicationPersistenceBridge.md`
