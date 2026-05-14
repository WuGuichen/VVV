# Issue 67 Runtime Resource Usage Validation

Date: 2026-05-15

Scope: verify that the #65 sample resource Catalog, #66 ResourceKey config
profile, runtime provider bootstrap, preload service, direct loads, release, and
diagnostics work as a single runtime slice. This is an EditMode runtime
validation fixture, not a playable scene or full demo runner.

## API Reuse

- Catalog: `TempImportedResourceCatalog.CreateCatalog()`
- Provider bootstrap: `TempImportedResourceCatalog.CreateMemoryProvider(...)`
  with `AssetDatabase.LoadAssetAtPath` in the EditMode fixture
- Runtime manager: `ResourceManager`
- Warmup: `ResourcePreloadService` and `ResourcePreloadPlan.Labels`
- Config bridge: `ResourceKeyConfigProfile.CreateSample()`
- Diagnostics: `ResourceDebugSnapshot`

The fixture does not assume `MemoryResourceProvider` can read
`providerData.assetPath`; it explicitly registers Unity objects through the
#65 bootstrap helper.

## Coverage

`TempImportedResourceRuntimeUsageTests` validates:

- Warmup labels:
  - `package.mxframework.samples`
  - `warmup.demo.start_screen`
  - `warmup.demo.combat`
  - `warmup.demo.status_effects`
  - `warmup.demo.magic_effects`
- Direct typed loads from the #66 config profile:
  - Katana prefab as `GameObject`
  - Burn, lightning, smoke, and stun aura prefabs as `GameObject`
  - StartScreen button, separator, and icon textures as `Texture2D`
  - MagicEffects samples as Unity `AudioClip`
- Prefab instance lifecycle:
  - Instantiate the loaded Katana prefab in test code.
  - Destroy the instance explicitly with `Object.DestroyImmediate`.
  - Release only resource handles through `ResourceManager`.
- Release and diagnostics:
  - `ResourceDebugSnapshot.LoadedCount` and `TotalRefCount` rise during warmup
    or direct load.
  - Counts return to zero after group or handle release.
  - Missing sample key returns `ResourceErrorCode.NotFound` and records a
    diagnostic error.

## Boundaries

- No scene, prefab, or ScriptableObject YAML was hand-written.
- No WGame-specific gameplay rules were introduced.
- FMOD bank files and FMOD event paths remain outside ordinary Resource Catalog
  validation per #68.
- #67 remains a Runtime Slice. A playable scene runner can be added by a later
  issue if needed.
