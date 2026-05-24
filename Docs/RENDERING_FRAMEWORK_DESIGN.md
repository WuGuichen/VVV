# MxFramework Rendering Framework Design

> Version 0.1 | 2026-05-24
>
> Status: Spec Ready for Phase 15.0 documentation baseline. Implementation starts in Phase 15.1.
>
> Scope: Rendering system orchestration for URP-facing framework code through Phase 15.3. Later Hub, Bridge, Volume, and demo feature details are intentionally left to follow-up tasks.

## 0. Context

This document extends the current URP project baseline in `Docs/RENDERING_PIPELINE.md`. It must be read together with:

- `Docs/DESIGN.md` for framework goals and noEngine boundaries.
- `Docs/INTERFACES.md` and `Docs/Interfaces/Rendering.md` for public API navigation.
- `Docs/Interfaces/Diagnostics.md` for read-only debug source contracts.
- `Docs/Interfaces/Camera.md` for the existing camera presentation boundary.
- `Docs/Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` for Debug UI registration and report reading workflow.

This file only fixes the Phase 15.0-15.3 design surface. It does not define grass, water, character, outline, decal, dissolve, or other feature-specific shader implementations.

## 1. Goals And Non-Goals

Goals:

- Provide a unified rendering system bus for context globals, shared render textures, pass ordering, diagnostics, and later material binding.
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
| `MxFramework.Rendering.GameplayBridge` | Planned 15.4+ | Depends on Rendering + Gameplay contracts |
| `MxFramework.Rendering.CombatBridge` | Planned 15.4+ | Depends on Rendering + Combat contracts |
| `MxFramework.Rendering.CharacterBridge` | Planned 15.4+ | Depends on Rendering + Character-facing contracts |
| `MxFramework.Rendering.CameraBridge` | Optional later | Depends on Rendering + Camera contracts if direct camera evaluation input is needed |
| `MxFramework.Demo.Rendering` | Planned 15.x | Showcase composition and sample assets only |

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

Shader global variables use the `_Mx` prefix. Phase 15.1-15.3 reserves:

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

The same shader property id must not be owned by both context layers. Phase 15.1-15.3 tests must treat duplicate ownership as a validation error.

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
- Current dimensions.
- Estimated and actual memory.
- Current frame writers.
- Current frame readers.
- Recent resize events.
- Recent conflicts.
- Total memory budget versus actual usage.

## 7. MaterialBindingHub Channel Rules

`MaterialBindingHub` is planned after Phase 15.3, but its constraints are fixed here because later render features must not bypass it.

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

## 8. RenderDataPublisher And Bridge Rules

`IRenderDataPublisher` is the Rendering-side semantic input API. Gameplay, Combat, Buffs, Character, and Camera modules do not call it directly. Phase 15.0 only reserves generic semantic categories:

- Subject impact.
- Surface contact.
- Field impulse.
- Subject movement.
- Subject lifecycle.

Feature-specific concepts such as grass deformation, water ripples, character hit flash, decals, or dissolve must be mapped from these generic events by later feature tasks instead of becoming 15.0 public API.

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

Phase 15.0 defines only the contract. Concrete bridges start in later tasks.

## 9. Diagnostics Protocol

Rendering diagnostics use `MxFramework.Diagnostics.IFrameworkDebugSource`.

Planned sources or sections:

- `globals`
- `cameraGlobals`
- `pipelineTopology`
- `sharedRTHealth`
- `materialBindings`
- `publisherCounts`

Report bundles reuse the existing report pattern under:

```text
Temp/MxFrameworkReports/Rendering/
```

Stable planned filenames:

- `rendering_pipeline_topology.txt`
- `rendering_sharedrt_health.txt`
- `rendering_material_bindings.txt`
- `rendering_globals.txt`
- `rendering_report_index.txt`

Rendering diagnostics are read-only. Any future debug command must go through Debug UI command gate or another explicit command interface outside `IFrameworkDebugSource`.

## 10. Authority, Hash, Replay, And SaveState Boundary

Rendering state must not enter runtime authority, replay hash, Runtime result hash, or SaveState.

Examples that must stay out:

- Wind or weather presentation values.
- SharedRT contents.
- MaterialPropertyBlock state.
- Volume blend runtime weights.
- Debug overlay state.
- Pipeline pass enable flags unless a future task explicitly defines them as configuration, not runtime authority.

Tests that cover runtime hash or SaveState must fail if Rendering state becomes an input to those systems.

## 11. Phase 15 Roadmap

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

### 15.4+

Hub, Bridge, Volume, and demo feature details will be defined by later task specs. Any later feature must first prove that it can use the 15.1-15.3 surface. If not, the infrastructure spec must be updated before the feature bypasses it.

## 12. Documentation Sync Requirements

Phase 15.0 must update:

- `Docs/RENDERING_PIPELINE.md`
- `Docs/README.md`
- `Docs/PROJECT_INDEX.md`
- `Docs/DESIGN.md`
- `Docs/INTERFACES.md`
- `Docs/ROADMAP.md`
- `Docs/QUALITY_GATE.md`

`Docs/CAPABILITIES.md` is updated only after implementation lands.

## 13. Spec Acceptance Checklist

- Public API names avoid business terms, except in explicit forbidden-term documentation.
- noEngine modules do not reference Rendering.
- Rendering does not reference Debug UI.
- Rendering does not reference Gameplay, Combat, Character, Buffs, Animation, or Camera.
- The only framework-level URP Renderer Feature is `MxRenderingPipelineFeature`.
- Global frame context and camera render context have distinct ownership.
- SharedRT conflicts are written as testable rules.
- 15.4+ content is placeholder-only.
