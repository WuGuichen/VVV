# MxFramework Rendering Framework Design

> Version 0.2 | 2026-05-25
>
> Status: Design
>
> Implementation state: Current through Phase 15.8 infrastructure and demo showcase. VolumeBlender includes request arbitration and diagnostics; runtime URP Volume object application remains a follow-up integration step.
>
> Scope: Rendering system orchestration for URP-facing framework code through Phase 15.8 demo showcase. Runtime Volume object application and feature-specific production shader slices remain follow-up tasks.

## 0. Context

This document extends the current URP project baseline in `Docs/RENDERING_PIPELINE.md`. It must be read together with:

- `Docs/DESIGN.md` for framework goals and noEngine boundaries.
- `Docs/INTERFACES.md` and `Docs/Interfaces/Rendering.md` for public API navigation.
- `Docs/Interfaces/Diagnostics.md` for read-only debug source contracts.
- `Docs/Interfaces/Camera.md` for the existing camera presentation boundary.
- `Docs/Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` for Debug UI registration and report reading workflow.
- `Docs/RENDERING_AUTHORING_GUIDE.md` for concrete authoring rules and demo validation.

This file defines the Rendering framework bus and infrastructure boundaries. It does not define grass, water, character, outline, decal, dissolve, or other feature-specific shader implementations.

## 1. Goals And Non-Goals

Goals:

- Provide a unified rendering system bus for context globals, shared render textures, pass ordering, diagnostics, and material binding.
- Let data from gameplay-facing systems reach rendering through a one-way, observable path.
- Keep Rendering as a Unity + URP-facing assembly. Mesh, Material, CommandBuffer, RTHandle, Volume, and ScriptableRendererFeature are allowed implementation types inside Rendering.
- Keep shader implementation local to each feature. The framework standardizes scheduling, inputs, resources, diagnostics, and hot toggles, not HLSL or Shader Graph authoring style.

Non-goals:

- Do not support the Built-in Render Pipeline.
- Do not replace URP, Shader Graph, URP Volume Framework, or project-specific shaders.
- Do not introduce third-party rendering plugins.
- Do not write rendering state into runtime authority, replay hash, Runtime result hash, or SaveState.
- Do not let noEngine modules depend on URP or `MxFramework.Rendering`.

## 2. Module Position And Dependency Rules

`MxFramework.Rendering` is a Unity-facing assembly. It may reference:

- `UnityEngine`
- `UnityEngine.Rendering`
- `UnityEngine.Rendering.Universal`
- `MxFramework.Core`
- `MxFramework.Diagnostics`

It must not reference `MxFramework.DebugUI`; Rendering exposes `IFrameworkDebugSource` snapshots from Diagnostics, and composition roots decide whether those sources are registered into Debug UI.

The following assemblies must not reference `MxFramework.Rendering`:

- `MxFramework.Core`
- `MxFramework.Config`
- `MxFramework.Events`
- `MxFramework.Attributes`
- `MxFramework.Modifiers`
- `MxFramework.Buffs`
- `MxFramework.Gameplay`
- `MxFramework.Combat`
- `MxFramework.Runtime`
- `MxFramework.Resources`
- `MxFramework.AI` (Runtime AI Planner)

Rendering itself must not reference Gameplay, Combat, Character, Buffs, Animation, or Camera. Optional bridge assemblies own those source-specific dependencies:

| Assembly | Status | Dependency rule |
| --- | --- | --- |
| `MxFramework.Rendering` | Phase 15.1+ | Unity + URP + Core + Diagnostics only |
| `MxFramework.Rendering.Editor` | Phase 15.x | Editor authoring, validation, menus, inspectors |
| `MxFramework.Rendering.GameplayBridge` | Implemented 15.6 subset | Depends on Rendering + Gameplay contracts |
| `MxFramework.Rendering.CombatBridge` | Planned 15.4+ | Depends on Rendering + Combat contracts |
| `MxFramework.Rendering.CharacterBridge` | Planned 15.4+ | Depends on Rendering + Character-facing contracts |
| `MxFramework.Rendering.CameraBridge` | Optional later | Depends on Rendering + Camera contracts if direct camera evaluation input is needed |
| `Assets/Scripts/MxFramework/Demo/Rendering` namespace/path slice | Implemented 15.8 showcase | Lives in `Assets/Scripts/MxFramework/Demo/MxFramework.Demo.asmdef`; showcase composition and sample assets only |

Dependency shape:

```text
Gameplay / Combat / Buffs / Character / Camera
        │ publish existing runtime or presentation events
        ▼
MxFramework.Rendering.<Source>Bridge     (optional, composition-root owned)
        │ maps source ids to MxRenderSubjectId and render semantic events
        ▼
MxFramework.Rendering                    (Unity + URP, no source-module dependency)
        │ exposes Diagnostics snapshots
        ▼
Composition root / Debug UI / Editor
```

Camera and Rendering do not reference each other by default. Camera-derived render values are written by the composition root or a future optional `MxFramework.Rendering.CameraBridge`, not by making `MxFramework.Rendering` depend on `MxFramework.Camera`.

## 3. Naming And Business-Term Rules

Rendering public API must avoid game-specific terms. Forbidden public API words include:

- `Player`
- `Enemy`
- `Boss`
- `Hero`
- `Monster`
- `Skill`
- `Element`

Rendering uses `MxRenderSubjectId` and `MxRenderSubjectRole` for render-facing subjects. It must not expose source module entity ids directly.

Allowed subject roles:

- `None`
- `Primary`
- `LocalControlled`
- `Focus`
- `Tracked`

Shader global variables use the `_Mx` prefix. Current reserved properties:

Global frame scope:

- `_MxTime`
- `_MxGameTime`
- `_MxDeltaTime`
- `_MxWindDirection`
- `_MxWindStrength`
- `_MxWindTurbulence`
- `_MxWetness`
- `_MxRain`
- `_MxSnowCoverage`
- `_MxPrimarySubjectWorldPos`
- `_MxPrimarySubjectVelocity`
- `_MxLocalSubjectWorldPos`

Camera scope:

- `_MxViewFocusWorldPos`
- Other camera-derived values defined by `CameraRenderContext` in a later implementation task.

Shared render textures use a stable ASCII `SharedRTId` for identity and equality. `DebugName` is for Frame Debugger and Diagnostics display only; it may be localized, but it must not participate in equality, hashing, or key lookup.

Shared RT shader property names use `_Mx*RT`. Bridge assembly names use `MxFramework.Rendering.<Source>Bridge`.

## 4. URP Integration Boundary

`Assets/Config/MxFramework/Rendering/MxFrameworkUniversalRenderer.asset` must contain only one framework-level URP Renderer Feature:

```text
MxRenderingPipelineFeature
```

Feature-specific rendering work must be implemented as `IMxRenderPass` or `IMxRenderPassProvider`, registered with the framework pipeline, and scheduled by `MxRenderingPipelineFeature`. Feature code must not add another independent `ScriptableRendererFeature` to the framework renderer asset.

`MxRenderingPipelineFeature` is responsible for:

1. Collecting passes and providers applicable to the current camera.
2. Sorting by `MxRenderPhase` and `Order`.
3. Validating SharedRT reads and writes.
4. Reporting topology and conflicts through Diagnostics.
5. Enqueuing the resulting URP passes.

Provider mode exists so SceneView, Preview, reflection, and Game cameras can select different pass sets without changing the renderer asset.

Rendering must remain compatible with the current URP baseline: Forward+, SRP Batcher, Native RenderPass, depth texture, opaque texture, and scripting-controlled Volume update mode. Any future change to these baseline settings must update `Docs/RENDERING_PIPELINE.md`.

## 5. Rendering Context Layers

Rendering context is split into global frame context and camera render context.

### 5.1 GlobalFrameContext

`GlobalFrameContext` owns data that is valid for every camera in the frame:

- Time values.
- Weather and environment scalar values.
- Wind values.
- Single-value subject values, such as primary and local-controlled subject pose.

Global frame values are written once per frame, normally from the runtime or demo composition root LateUpdate path, through `Shader.SetGlobalXxx`.

Forbidden: Global frame context must not write camera-derived values such as active camera position, camera frustum, view focus, projection data, or SceneView-specific overrides.

### 5.2 CameraRenderContext

`CameraRenderContext` owns values that can differ per camera:

- View focus world position.
- Camera-specific overrides.
- Camera kind: Game, SceneView, Reflection, or Preview.
- Camera-derived globals added by later tasks.

Camera values are written from URP camera rendering, normally through a pipeline-injected `MxCameraGlobalsPass` that uses `CommandBuffer.SetGlobalXxx`. This prevents SceneView, Preview, and reflection cameras from accidentally consuming Game camera-only values.

The same shader property id must not be owned by both context layers. Tests must treat duplicate ownership as a validation error.

## 6. SharedRTRegistry Conflict Semantics

Shared render textures are addressed by `SharedRenderTextureKey`. The key contains identity and allocation policy, while registry metadata owns writer lists and diagnostics.

Key fields:

- `SharedRTId Id`
- `string DebugName`
- `SharedRTOwnerId Owner`
- `SharedRTAccessPolicy Access`
- `SharedRTAnchor Anchor`
- `SharedRTFormat Format`
- `SharedRTSize Size`
- `SharedRTClearSpec Clear`
- `SharedRTResizePolicy Resize`
- `long EstimatedMemoryBytes`

`SharedRTAccessPolicy` stores a `SharedRTWriterSetId`, not a `ReadOnlySpan<T>` or collection inside the key. The registry resolves writer set ids to metadata. This keeps keys stable, hashable, and usable in dictionaries.

Conflict rules:

| Rule | Name | Trigger | Expected behavior |
| --- | --- | --- | --- |
| R-RT-01 | AdditiveWritersAllowed | `AllowAdditiveWriters=true` and all writers are in the registered writer set | Allow and increment additive writer counter |
| R-RT-02 | WriterConflict | `AllowAdditiveWriters=false` and a second writer appears in the same frame | Reject the later writer and report a conflict |
| R-RT-03 | StaleReader | Same-frame reader phase/order is earlier than or equal to required writer phase/order when policy requires current-frame data | Skip the reader for that frame and report stale read |
| R-RT-04 | UnauthorizedWriter | Writer owner is not in the registered writer set | Reject registration or write request and report |
| R-RT-05 | OrphanRT | No readers for N consecutive frames | Warn and release according to policy |
| R-RT-06 | ResizeRejected | `Resize=FailOnResize` and requested size changes | Keep current RT and report resize rejection |
| R-RT-07 | ResizeBurst | `Resize=Reallocate` exceeds resize threshold per second | Warn with recent resize count |
| R-RT-08 | DroppedAllocation | RT allocation fails | Return a safe fallback texture/handle to readers and report, without throwing in the render loop |

Every rule must have a named test using `SharedRTRegistry_R_RT_0x_*` naming.

Diagnostics must expose:

- Owner.
- Format.
- Current dimensions.
- Resize policy.
- Estimated and actual memory.
- Current frame writers.
- Current frame readers.
- Recent resize events.
- Recent conflicts.
- Total memory budget versus actual usage.

## 7. MaterialBindingHub Channel Rules

`MaterialBindingHub` is implemented and owns framework material property writes so render features and bridges do not bypass channel arbitration.

Rules:

- Rendering and bridge code must not call `Renderer.SetPropertyBlock(...)` directly.
- All material property writes go through `IMaterialBindingHub`.
- The hub owns one merged MaterialPropertyBlock per `(Renderer, materialIndex)` target.
- Multiple systems write through channels; the hub merges channels and calls `SetPropertyBlock` once per target during its apply phase.

Initial channel set:

- `HitFlash`
- `StatusTint`
- `DissolveProgress`
- `OutlineState`
- `WetnessOverride`
- `BridgeCustom`
- `DebugOverlay`

`BridgeCustom` is reserved for source bridge-specific low-frequency presentation values. It must not become a general way to bypass typed Rendering features.

The same `(Subject, Channel)` has a single writer. Re-registering the same channel for the same subject replaces the old writer and emits a warning. Releasing a subject releases all bindings under that subject.

Diagnostics must report binding count, channel distribution, merge cost, pool hit rate, and duplicate channel writer warnings.

## 8. VolumeBlender Request Semantics

VolumeBlender is the Phase 15.7 public API/spec for code-side URP Volume Profile requests. It belongs to `MxFramework.Rendering` because it is Unity + URP-facing presentation orchestration. It does not replace URP Volume Framework and does not define feature-specific post-processing presets.

Responsibilities:

1. Accept profile blend requests with Rendering-owned ids.
2. Evaluate request weight from blend-in, hold, blend-out, and release state.
3. Arbitrate requests by scope, priority, and stable tie-breaker.
4. Expose the final applied profile snapshot as diagnostics. Runtime URP Volume object application is not implemented yet and remains a follow-up integration step.
5. Report active requests, expired requests, priorities, weights, suppressed candidates, and final applied blend state.

Non-responsibilities:

- Do not author or replace Unity `VolumeProfile` component semantics.
- Do not introduce an independent framework `ScriptableRendererFeature` for post-processing.
- Do not use legacy Post Processing Stack v2 or camera image effects.
- Do not depend on `MxFramework.Camera`, Gameplay, Combat, Runtime authority, Replay hash, Runtime result hash, or SaveState.
- Do not make post-processing state authoritative gameplay state.

### 8.1 Request Identity And Profile Reference

`MxVolumeRequestId` is created by Rendering and remains stable until the request has been released or expired and removed from the active request table. Callers must not construct ids to claim ownership.

`MxVolumeProfileReference` is the public profile target. It may contain a stable resource/catalog key, a direct URP `VolumeProfile` reference, or both. The stable key is preferred for equality, diagnostics, and future resource indirection. A direct profile reference is allowed only because `MxFramework.Rendering` is a Unity-facing assembly.

### 8.2 Scope

Requests support three scopes:

| Scope | Meaning | Dependency boundary |
| --- | --- | --- |
| `Global` | Contributes to every rendering camera evaluation. | No camera dependency. |
| `CameraKind` | Contributes only when `MxCameraRenderKind` matches. | Uses Rendering's existing camera kind enum. |
| `ExplicitCamera` | Contributes only when an opaque `MxRenderingCameraToken` matches. | Token is supplied by composition root or optional CameraBridge; Rendering still does not depend on `MxFramework.Camera`. |

Global and per-camera requests are isolated in storage and lifecycle. A per-camera release must not release or alter a global request. During evaluation, a camera receives the union of global requests plus matching camera-kind and explicit-camera requests.

### 8.3 Priority, Lifetime, Release, And Tie-Breaker

Request timing uses non-negative `BlendInSeconds`, `HoldSeconds`, and `BlendOutSeconds`.

- Blend-in ramps weight from `0` to `1`.
- Hold keeps weight at `1`.
- Blend-out ramps weight from current value to `0`.
- Zero durations are legal and produce immediate transitions.
- `HoldSeconds <= 0` means the request does not auto-expire and must be released explicitly.
- `Release(requestId)` is idempotent and starts blend-out once; it does not restart or extend the request on repeated calls.
- Presentation time is controlled through the VolumeBlender public API. Composition roots must advance time before creating or releasing requests, or use the explicit-time request/release overloads when those operations happen between render evaluations.
- Diagnostics capture also performs expiry cleanup, so requests that expire without a later blend-state evaluation still appear in expired diagnostics and are removed from active lookup.

Arbitration is deterministic:

1. Filter to requests visible to the current evaluation scope.
2. Remove requests whose computed weight is `0` and whose cleanup phase has completed.
3. Sort by higher `Priority`.
4. For equal priority, earlier creation sequence wins.
5. If creation sequence is unavailable in a persisted diagnostic snapshot, lower `MxVolumeRequestId.Value` wins.

The current implementation produces diagnostics-only applied profile snapshots and suppressed candidates. A future runtime application may apply one arbitration winner or multiple weighted URP Volume entries only if it preserves the same public diagnostics and deterministic ordering.

### 8.4 URP Volume Runtime Ownership

VolumeBlender currently manages request intent, arbitration, and diagnostics. Runtime application is not implemented yet. When added, it must use URP Volume Framework for actual post-processing behavior. It must not create a parallel post-processing evaluator, bypass URP Volume components, or add a new framework renderer feature.

Allowed future runtime-application choices:

- Maintain one or more framework-owned runtime `Volume` objects.
- Update profile references and weights during Rendering presentation update.
- Keep scripting-controlled Volume update mode compatible with the baseline in `Docs/RENDERING_PIPELINE.md`.
- Expose diagnostics-only final blend state before runtime application is connected.

Forbidden implementation choices:

- Add a standalone `ScriptableRendererFeature` for Volume blending.
- Add legacy post-processing image effects or Post Processing Stack v2.
- Read `MxFramework.Camera` state directly from Rendering.
- Feed Volume state into Gameplay/Combat decisions, Runtime authority, Replay hash, Runtime result hash, or SaveState.

### 8.5 Diagnostics And Acceptance

VolumeBlender diagnostics must expose:

- Active requests and expired requests.
- Request id, profile reference, scope, priority, phase, computed weight, creation sequence, and debug name.
- Suppressed requests after arbitration.
- Final applied blend state per evaluated camera kind/token.
- Cleanup reason for expired or released requests.

Follow-up implementation acceptance requires:

- EditMode tests for request id stability, release idempotency, priority, stable tie-breaker, blend-in/hold/blend-out, zero durations, and cleanup.
- Tests for global visibility, `CameraKind` isolation, and explicit camera token isolation.
- Diagnostics tests for active requests, expired requests, priorities, weights, suppressed candidates, and final applied state.
- Dependency inspection proving no direct reference from Rendering to Camera, Gameplay, Combat, Runtime authority/replay/SaveState, independent framework `ScriptableRendererFeature`, or legacy post-processing.
- Unity/PlayMode smoke only when runtime URP Volume objects or assets are touched.

## 9. RenderDataPublisher And Bridge Rules

`IRenderDataPublisher` is the implemented Rendering-side semantic input API. Gameplay, Combat, Buffs, Character, and Camera modules do not call it directly. The generic semantic categories are:

- Subject impact.
- Surface contact.
- Field impulse.
- Subject movement.
- Subject lifecycle.

Feature-specific concepts such as grass deformation, water ripples, character hit flash, decals, or dissolve must be mapped from these generic events by later feature tasks instead of becoming generic Rendering public API.

Source modules publish their existing runtime or presentation events. Optional bridge assemblies translate those events into render semantics:

- `MxFramework.Rendering.GameplayBridge`
- `MxFramework.Rendering.CombatBridge`
- `MxFramework.Rendering.CharacterBridge`
- Optional future `MxFramework.Rendering.CameraBridge`

Bridge rules:

- Each bridge has its own asmdef.
- Rendering core does not depend on any bridge.
- Bridges subscribe only to public runtime or presentation event contracts from their source module.
- Bridges do not read source module private fields.
- Bridges maintain an `IRenderSubjectMap` from source ids to `MxRenderSubjectId`.
- Bridges are composition-root owned.
- Bridge lifecycle uses constructor dependency injection plus `Install()` and `Dispose()` or `Uninstall()`. Do not introduce a broad composition root interface into Rendering public API.

`MxFramework.Rendering.GameplayBridge` implements the 15.6 Gameplay lifecycle subset. Other concrete bridges remain later tasks.

## 10. Diagnostics Protocol

Rendering diagnostics use `MxFramework.Diagnostics.IFrameworkDebugSource`.

Current or reserved sources or sections:

- `globals`
- `cameraGlobals`
- `pipelineTopology`
- `sharedRTHealth`
- `materialBindings`
- `volumeBlender`
- `publisherCounts`

Rendering report bundles are a future reporting convention, not an implemented exporter API. Manual bundles for review or QA should use the existing report directory pattern:

```text
Temp/MxFrameworkReports/Rendering/
```

Expected manual bundle filenames:

- `rendering_pipeline_topology.txt`
- `rendering_sharedrt_health.txt`
- `rendering_material_bindings.txt`
- `rendering_volume_blender.txt`
- `rendering_globals.txt`
- `rendering_report_index.txt`

Rendering diagnostics are read-only. Any future debug command must go through Debug UI command gate or another explicit command interface outside `IFrameworkDebugSource`.

## 11. Authority, Hash, Replay, And SaveState Boundary

Rendering state must not enter runtime authority, replay hash, Runtime result hash, or SaveState.

Examples that must stay out:

- Wind or weather presentation values.
- SharedRT contents.
- MaterialPropertyBlock state.
- Volume blend runtime weights.
- Volume request ids, priorities, scopes, profile references, and diagnostics snapshots.
- Debug overlay state.
- Pipeline pass enable flags unless a future task explicitly defines them as configuration, not runtime authority.

Tests that cover runtime hash or SaveState must fail if Rendering state becomes an input to those systems.

## 12. Phase 15 Roadmap

### 15.0 Spec

Deliver:

- `Docs/RENDERING_FRAMEWORK_DESIGN.md`.
- `Docs/Interfaces/Rendering.md`.
- Documentation sync entries in pipeline, index, design, roadmap, and quality gate docs.

Do not deliver implementation code in 15.0.

### 15.1 GlobalFrameContext + Pipeline Feature Skeleton

Deliver:

- `MxRenderingPipelineFeature` empty skeleton.
- `GlobalFrameContext`.
- `_MxTime` and `_MxWindDirection` injection.
- Rendering Diagnostics source for global values.
- PlayMode validation that a material can consume `_MxWindDirection`.

Do not deliver SharedRT, MaterialBindingHub, bridges, or feature-specific shaders beyond the minimal validation asset.

### 15.2 SharedRTRegistry + Conflict Diagnostics

Deliver:

- `SharedRTRegistry`.
- Full key and policy model.
- R-RT-01 through R-RT-08 conflict handling.
- Dummy pass validation with a small RT.
- Diagnostics source for memory, reader, writer, resize, and conflict health.

Do not connect real grass, water, snow, or other feature RTs.

### 15.3 CameraRenderContext + FeaturePipeline Sorting

Deliver:

- Camera-scoped globals through URP camera rendering path.
- `FeaturePipeline` sorting by phase and order.
- Provider camera filtering.
- Pipeline topology diagnostics.
- SceneView / GameView validation for different camera globals.

Do not deliver MaterialBindingHub, source bridges, VolumeBlender, or feature-specific demo slices.

### 15.4-15.6 Hub, Publisher, And Gameplay Bridge

Delivered:

- `IMaterialBindingHub`, `IMaterialBindingWriter`, channel arbitration, diagnostics, and duplicate writer warnings.
- `IRenderDataPublisher`, generic render semantic events, counters, recent event diagnostics, and debug source.
- `MxFramework.Rendering.GameplayBridge` 15.6 subset for public Gameplay lifecycle events.

Deferred:

- Combat, Character, and Camera bridges.
- Feature-specific production shader slices.

### 15.7 VolumeBlender Request Arbitration And Diagnostics

Deliver:

- VolumeBlender public API/spec in `Docs/Interfaces/Rendering.md`.
- Request scope, priority, lifetime, release, stable tie-breaker, global/per-camera isolation, diagnostics, and acceptance criteria.
- Implementation of request arbitration and diagnostics.

Do not claim runtime URP Volume object application. The current implementation evaluates applied profile snapshots for diagnostics but does not create or mutate runtime `Volume` objects.

### 15.8 Rendering Demo Slices Showcase

Deliver:

- `Assets/Scenes/RenderingDemoSlicesShowcase.unity`.
- UI Toolkit HUD and buttons/keys for context, material pulse, publisher event burst, and VolumeBlender priority.
- Showcase diagnostics for Context, SharedRT / FeaturePipeline, MaterialBindingHub, RenderDataPublisher, and VolumeBlender.

Do not deliver production grass, water, character, post-processing presets, project business assets, or runtime URP Volume object application.

### 15.9+ Future Work

VolumeBlender runtime URP Volume object application and feature-specific Volume presets may start only after the Phase 15.7 arbitration and diagnostics implementation is reviewed. They must use URP Volume Framework and the reviewed request API.

## 13. Documentation Sync Requirements

Rendering documentation changes should keep these files aligned:

- `Docs/RENDERING_PIPELINE.md`
- `Docs/RENDERING_FRAMEWORK_DESIGN.md`
- `Docs/RENDERING_AUTHORING_GUIDE.md`
- `Docs/Interfaces/Rendering.md`
- `Docs/README.md`
- `Docs/PROJECT_INDEX.md`
- `Docs/ROADMAP.md`
- `Docs/QUALITY_GATE.md`

`Docs/CAPABILITIES.md` and `Docs/USAGE.md` are updated only when capability status or demo usage changes.

## 14. Spec Acceptance Checklist

- Public API names avoid business terms, except in explicit forbidden-term documentation.
- noEngine modules do not reference Rendering.
- Rendering does not reference Debug UI.
- Rendering does not reference Gameplay, Combat, Character, Buffs, Animation, or Camera.
- The only framework-level URP Renderer Feature is `MxRenderingPipelineFeature`.
- Global frame context and camera render context have distinct ownership.
- SharedRT conflicts are written as testable rules.
- VolumeBlender request id, profile reference, scope, priority, lifetime, release, stable tie-breaker, global/per-camera isolation, diagnostics, and URP Volume Framework boundary are documented for the implemented arbitration and diagnostics boundary.
- VolumeBlender does not introduce Runtime authority, Replay hash, SaveState, Camera, Gameplay, Combat, independent RendererFeature, or legacy post-processing dependencies.
