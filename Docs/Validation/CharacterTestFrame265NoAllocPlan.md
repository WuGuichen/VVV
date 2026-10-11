# CharacterTest Frame 265 NoAlloc Plan

> Status: Current

Implementation plan and optimization notes for the frame 265 NoAlloc pass.

## Summary

This implementation targets the frame 265 markers observed in the CharacterTest capture:

- `CharacterTest.GameManager.Simulation`: about 19.4 KB GC
- `CombatTrace.Projectiles`: about 10.2 KB GC
- `VfxPresentation`: about 3.5 KB GC
- `SlicePresentation`: about 1.4 KB GC

The touched paths are high-frequency paths: `RuntimeHost.Tick`,
`CombatProjectileRuntime.Tick`, `CharacterTestCharacterCombatTraceSystem.Tick`,
`VfxRuntimeModule.Tick`, presentation sync, event publication, and
resource/prefab presentation sync. The warmed steady-state target is NoAlloc.
Debug snapshots, network serialization, save-state capture, and diagnostics
remain allocation-bearing APIs by design.

## API And Interface Changes

- `CombatProjectileRuntime.Tick(frame, CombatProjectileFrameEventBuffer)` is the
  no-allocation frame event path when the caller owns and reuses the buffer.
- `CombatProjectileRuntime.CopyActiveViewsTo(List<CombatProjectileActiveView>)`
  copies lightweight active projectile views for Tick and presentation consumers.
- `CombatProjectileActiveView` carries the projectile values needed by
  CharacterTest tornado logic and Unity presentation, without accepted-hit
  history collections.
- `CombatProjectileFrameEventBuffer` keeps reusable spawn, impact, expiration,
  and rejection event lists.
- `CombatProjectileUnityPresenter.Sync(IReadOnlyList<CombatProjectileActiveView>)`
  synchronizes projectile views without requiring a full
  `CombatProjectileRuntimeState` snapshot.
- `UnityVfxPresenter.PrewarmDefinitions()` and
  `CombatProjectileUnityPresenter.PrewarmBindings()` preload prefab handles and
  warm pools during composition setup.
- `CharacterTestCharacterRuntimeModule.TryApplyAuthorityPoseImpulse(..., out string issue)`
  applies authority pose impulses without forcing `CreateSnapshot()`.

Existing snapshot/debug APIs remain available for debug UI, networking,
save-state, and tests. They are treated as `AllocByDesign`/low-frequency paths.

## Implementation Changes

- CharacterTest projectile processing now consumes a reusable
  `CombatProjectileFrameEventBuffer` instead of `CombatProjectileFrameEvents`.
- Tornado attraction and tornado tick damage use
  `CombatProjectileActiveView` copied into a reused list, instead of creating a
  full projectile runtime state snapshot each frame.
- `CharacterTestSliceServices.TickPresentation` no longer calls
  `CreateProjectileStateSnapshot()` for projectile presentation sync.
- Projectile accepted-hit history is stored in a runtime-owned buffer so
  position/tracking updates do not repeatedly copy accepted-hit arrays after a
  piercing hit.
- Projectile and VFX presenters cache prefab `ResourceHandle<GameObject>` values
  and cache each pooled view's `ParticleSystem[]` at instantiation time.
- Warmed spawn/stop uses the pool and cached component arrays. Pool exhaustion
  remains a supported fallback and records diagnostics.
- `ResourceManager.Load` remains a low-frequency resource API; composition
  warmup moves expected CharacterTest loads out of Tick/spawn.
- Procedural hit feedback applies authority pose impulses without forcing an
  authority pose snapshot. Debug/preview paths can still request snapshots
  explicitly.

## Acceptance

- Warmed steady-state projectile processing should not allocate for frame event
  list wrappers or active projectile snapshots.
- Warmed projectile presentation sync should not call `ResourceManager.Load` or
  `GetComponentsInChildren`.
- Warmed VFX spawn/stop should not call `ResourceManager.Load` or
  `GetComponentsInChildren` when the pool has capacity.
- Pool exhaustion must record diagnostics rather than silently converting the
  hot path into a resource-load path.
- Snapshot/save/restore/network behavior remains compatible through the
  existing `CreateStateSnapshot()`, `CreateDebugSnapshot()`, and restore APIs.

## Verification Plan

- `Tools/Harness/check_code_standards.py --all --summary-only`
- `Tools/Harness/run_lightweight_checks.sh`
- Unity MCP compile check and relevant EditMode tests when the editor/MCP
  connection is available.
- CharacterTest smoke/profiling recapture of frame 265 markers, with
  `CharacterTest.GameManager.Simulation` warmed steady-state GC target of `0 B`.
