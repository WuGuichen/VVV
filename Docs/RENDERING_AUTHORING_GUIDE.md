# MxFramework Rendering Authoring Guide

> Version 0.1 | 2026-05-25
>
> Status: Current authoring guide for Phase 15.1-15.8 Rendering infrastructure.
>
> Scope: Authoring rules for shader globals, camera globals, SharedRT keys, render passes/providers, material bindings, render data publishing, VolumeBlender request diagnostics, demo showcase validation, and rendering report bundles.

This is the single Rendering authoring guide. Keep design rationale in `RENDERING_FRAMEWORK_DESIGN.md`, public signatures in `Interfaces/Rendering.md`, and Unity pipeline baseline in `RENDERING_PIPELINE.md`.

## 1. Authoring Boundaries

Rendering is a Unity + URP-facing presentation layer. It may use UnityEngine, URP, CommandBuffer, RTHandle, MaterialPropertyBlock, Renderer, Texture, Color, and VolumeProfile inside `MxFramework.Rendering`.

Do not author Rendering content that:

- Adds WGame-specific terms, characters, elements, levels, Buff ids, or gameplay rules.
- Makes Core, Runtime, Gameplay, Combat, Buffs, Resources, Runtime AI Planner, or other noEngine modules reference `MxFramework.Rendering`.
- Writes Rendering state into runtime authority, Replay hash, Runtime result hash, or SaveState.
- Adds independent framework `ScriptableRendererFeature` assets for feature slices. Use `MxRenderingPipelineFeature` plus `IMxRenderPass` or `IMxRenderPassProvider`.
- Bypasses `IMaterialBindingHub` with direct `Renderer.SetPropertyBlock(...)` from Rendering or bridge code.

## 2. Shader Global Naming And Ownership

All framework-owned shader globals use the `_Mx` prefix. Use `MxRenderingShaderIds` for property ids instead of string literals in runtime code.

Global frame values are owned by `IGlobalFrameContext` / `GlobalFrameContext`:

| Shader property | Authoring meaning | Writer |
| --- | --- | --- |
| `_MxTime` | Packed presentation time values. | `SetTime(...)` |
| `_MxGameTime` | Game-time scalar for presentation. | `SetTime(...)` |
| `_MxDeltaTime` | Presentation delta time. | `SetTime(...)` |
| `_MxWindDirection` | Framework wind direction vector. | `SetWind(...)` |
| `_MxWindStrength` | Framework wind strength. | `SetWind(...)` |
| `_MxWindTurbulence` | Framework wind turbulence. | `SetWind(...)` |
| `_MxWetness` | Presentation wetness. | `SetWeather(...)` |
| `_MxRain` | Presentation rain amount. | `SetWeather(...)` |
| `_MxSnowCoverage` | Presentation snow coverage. | `SetWeather(...)` |
| `_MxPrimarySubjectWorldPos` | Primary render subject position. | `SetPrimarySubjectPose(...)` |
| `_MxPrimarySubjectVelocity` | Primary render subject velocity. | `SetPrimarySubjectPose(...)` |
| `_MxLocalSubjectWorldPos` | Local-controlled render subject position. | `SetLocalSubjectPose(...)` |

Camera-scoped values are owned by `ICameraRenderContext` / `CameraRenderContext`:

| Shader property | Authoring meaning | Writer |
| --- | --- | --- |
| `_MxViewFocusWorldPos` | View focus position for the current rendered camera. | `SetViewFocus(...)` through the camera rendering path |
| Custom camera override property ids | Camera-local presentation overrides. | `SetCameraOverride(...)` |

Rules:

- Do not write camera-derived values through `GlobalFrameContext`.
- Do not write global frame ids through `CameraRenderContext`; `SetCameraOverride(...)` rejects known global ids.
- SceneView, Preview, Reflection, and Game cameras may see different camera context values.
- Feature shaders should treat `_Mx*` as framework-owned. Project-specific shader properties need their own prefix.
- Shared RT shader property names also use `_Mx*RT`; the stable identity still comes from `SharedRTId`, not the shader property name.

## 3. SharedRT Keys, Writers, And Conflicts

Shared render textures are identified by `SharedRenderTextureKey`. `SharedRTId` is the stable identity. `DebugName` is only for diagnostics and Frame Debugger display.

Author a key with these fields fixed intentionally:

- `SharedRTId Id`: stable ASCII id used for equality and lookup.
- `SharedRTOwnerId Owner`: logical owner registering the texture.
- `SharedRTAccessPolicy Access`: writer set id, additive writer rule, and frame-order rule.
- `SharedRTAnchor Anchor`: world, main camera, primary subject, or static anchoring.
- `SharedRTFormat Format`, `SharedRTSize Size`, `SharedRTClearSpec Clear`, `SharedRTResizePolicy Resize`.
- `EstimatedMemoryBytes`: diagnostics and budget planning.

Writer rules:

- Register allowed writer sets through `RegisterWriterSet(SharedRTWriterSetId, IReadOnlyList<SharedRTOwnerId>)`.
- A write owner not present in the registered writer set is unauthorized.
- If `AllowAdditiveWriters` is false, a second writer in the same frame is a conflict.
- If current-frame data is required, readers must run after the writer phase/order.
- Keep pass `Reads` and `Writes` metadata stable after construction so topology diagnostics remain deterministic.

Conflict rule ids are stable and must be preserved in docs and tests:

| Rule | Code | Meaning |
| --- | --- | --- |
| R-RT-01 | `AdditiveWritersAllowed` | Additive writers are allowed and all writers are registered. |
| R-RT-02 | `WriterConflict` | A second writer appears when additive writers are not allowed. |
| R-RT-03 | `StaleReader` | A reader runs before or at the required writer frame order. |
| R-RT-04 | `UnauthorizedWriter` | The writer owner is not in the registered writer set. |
| R-RT-05 | `OrphanRT` | No readers for the configured orphan threshold. |
| R-RT-06 | `ResizeRejected` | Requested size changes when resize policy is `FailOnResize`. |
| R-RT-07 | `ResizeBurst` | Reallocation frequency crosses the diagnostics threshold. |
| R-RT-08 | `DroppedAllocation` | Allocation fails and readers receive fallback behavior. |

## 4. Render Pass And Provider Authoring

All framework render work goes through `MxRenderingPipelineFeature`. Feature-specific Unity renderer features are not allowed on `MxFrameworkUniversalRenderer.asset`.

Use `IMxRenderPass` for a concrete pass with fixed metadata:

- `DebugName`: unique, stable, and not localized.
- `Phase`: one `MxRenderPhase` value.
- `Order`: deterministic ordering within the phase.
- `IsEnabled`: cheap state check.
- `Reads` / `Writes`: stable `IReadOnlyList<SharedRenderTextureKey>` metadata.
- `Configure(...)`: declare per-camera/pass setup.
- `Execute(...)`: perform pass work through the provided context.

Use `IMxRenderPassProvider` when the pass set depends on camera kind, SceneView, Preview, reflection camera, or runtime toggles. Providers collect passes into `IMxRenderPassRegistry` from `CollectPasses(...)`.

Sorting and validation:

- Static passes and provider passes are collected for the current `MxCameraRenderContextDescriptor`.
- Enabled passes sort by `Phase`, then `Order`, then `DebugName` ordinal.
- Duplicate debug names, invalid metadata, same-order SharedRT read/write hazards, and writer policy violations are diagnostics, not hidden behavior.
- Same `Phase + Order` is acceptable only when the passes do not read/write the same SharedRT dependency.

## 5. MaterialBindingHub Channel Rules

Material authoring goes through `IMaterialBindingHub` and `IMaterialBindingWriter`.

Authoring flow:

1. Resolve or create an `MxRenderSubjectId`.
2. Bind one channel with `Bind(subject, channel, scope)`.
3. Write properties through `SetFloat`, `SetColor`, `SetVector`, `SetTexture`, or `Pulse`.
4. Release the `MaterialBinding` or release all subject bindings when the render subject is released.

Initial channels:

| Channel | Intended use |
| --- | --- |
| `HitFlash` | Short impact color/intensity feedback. |
| `StatusTint` | Status-driven tint. |
| `DissolveProgress` | Dissolve-like presentation progress. |
| `OutlineState` | Outline state values. |
| `WetnessOverride` | Subject-local wetness overrides. |
| `BridgeCustom` | Low-frequency bridge-specific presentation values only. |
| `DebugOverlay` | Diagnostics or showcase overlay values. |

Rules:

- The same `(MxRenderSubjectId, MxMaterialChannel)` has one writer. Rebinding replaces the previous binding and emits duplicate diagnostics.
- Use `MaterialBindingScope.ForRenderer(...)`, `ForRendererSubMesh(...)`, or `ForSubjectHierarchy(...)`.
- Do not expose Unity `Keyframe` as stable public API; use `MaterialBindingCurveDescriptor`.
- `BridgeCustom` is not a general escape hatch. Add a typed channel if a repeated framework use case emerges.

## 6. RenderDataPublisher And Bridge Boundaries

`IRenderDataPublisher` is the Rendering-side semantic event input. Gameplay, Combat, Character, Buffs, and Camera modules do not call it directly from noEngine code. Optional bridge assemblies or composition roots translate source events.

Current semantic event kinds:

- `PublishImpact(...)`
- `PublishSurfaceContact(...)`
- `PublishFieldImpulse(...)`
- `PublishSubjectMovement(...)`
- `PublishSubjectLifecycle(...)`

Bridge rules:

- Rendering core does not depend on bridge assemblies.
- `MxFramework.Rendering.GameplayBridge` is implemented for a 15.6 subset and consumes public `GameplayRuntimeModule.DrainEvents(...)` / `GameplayRuntimeEvent` payloads only.
- Combat, Character, and Camera bridges remain future optional bridge work unless their own specs land.
- Bridges keep an `IRenderSubjectMap<TSourceId>` from source ids to `MxRenderSubjectId`.
- Bridges use constructor injection plus `Install()` / `Uninstall()` / `Dispose()` lifecycle. Do not add a broad composition-root object to the Rendering API.
- Bridges must not read source module private fields, invent source ids, or make source modules depend on Rendering.

## 7. VolumeBlender Current Boundary

`IVolumeBlender` currently provides request arbitration and diagnostics for code-side URP Volume Profile blend intent. It does not create or mutate runtime URP `Volume` objects in the current implementation.

Current authoring surface:

- `MxVolumeProfileReference`: stable key, direct `VolumeProfile`, or both. Prefer stable keys for equality and diagnostics.
- `MxVolumeRequestScope`: `Global`, `ForCameraKind(...)`, or `ForExplicitCamera(...)`.
- `MxVolumeBlendTiming`: non-negative blend-in, hold, and blend-out durations.
- `MxVolumeRequestDescriptor`: profile, scope, priority, timing, and debug name.
- `IVolumeBlender.Request(...)`, `Release(...)`, `TryGetRequest(...)`, `CaptureBlendState(...)`, `CaptureDiagnostics()`.

Arbitration rules:

- Evaluate visible requests from global plus matching camera-kind and explicit-camera scopes.
- Higher `Priority` wins when requests are mutually exclusive.
- Equal priority uses earlier creation sequence; persisted diagnostics may fall back to lower `MxVolumeRequestId.Value`.
- Release is idempotent and starts blend-out once.
- Presentation time belongs to Rendering and must not enter runtime authority, Replay, Runtime result hash, or SaveState.

Diagnostics must report active requests, expired requests, suppressed candidates, computed weights, priorities, phases, cleanup reason, and final applied profile snapshots.

Future runtime URP Volume object application must use URP Volume Framework, preserve this request API, add its own PlayMode smoke, and must not add an independent renderer feature or legacy post-processing stack.

## 8. Demo Showcase Run Instructions

The Rendering Demo Slices Showcase verifies the infrastructure without shipping production grass, water, character, post-processing presets, or game-specific assets.

Entries:

- Scene: `Assets/Scenes/RenderingDemoSlicesShowcase.unity`
- Runtime: `Assets/Scripts/MxFramework/Demo/Rendering/RenderingDemoSlicesShowcaseRuntime.cs`
- Composition root: `Assets/Scripts/MxFramework/Demo/Rendering/RenderingDemoSlicesShowcaseRoot.cs`
- HUD: `Assets/Scripts/MxFramework/Demo/Rendering/RenderingDemoSlicesHudController.cs`
- Scene generator: `MxFramework / Rendering / Create Demo Slices Showcase Scene`

Manual smoke:

1. Open `Assets/Scenes/RenderingDemoSlicesShowcase.unity`.
2. Enter Play Mode.
3. Press `1`, `2`, `3`, or `4`, or use the HUD buttons, to switch wind context, material pulse, publisher event burst, and VolumeBlender priority.
4. Press `R` or the HUD reset button to reset.
5. Confirm the HUD reports Context, SharedRT / FeaturePipeline, MaterialBindingHub, RenderDataPublisher, VolumeBlender diagnostics, and recent events.

Showcase boundaries:

- Context slice uses `GlobalFrameContext` / `MxRenderingShaderIds`; demo code does not directly call `Shader.SetGlobal*`.
- SharedRT slice uses synthetic pass/provider metadata and `ISharedRenderTextureRegistry`; it does not add another `ScriptableRendererFeature`.
- Material slice uses `IMaterialBindingHub` / `IMaterialBindingWriter`; it does not call `Renderer.SetPropertyBlock(...)` directly.
- Publisher slice uses generic `IRenderDataPublisher` events and `MxRenderSubjectId`.
- Volume slice shows `IVolumeBlender` arbitration and diagnostics only; it does not create runtime URP `Volume` objects.

## 9. Diagnostics And Report Bundles

Rendering diagnostics use `MxFramework.Diagnostics.IFrameworkDebugSource`, not `MxFramework.DebugUI`.

Sections:

- `globals`
- `cameraGlobals`
- `pipelineTopology`
- `sharedRTHealth`
- `materialBindings`
- `volumeBlender`
- `publisherCounts`

Report bundles follow the existing report pattern under:

```text
Temp/MxFrameworkReports/Rendering/
```

Stable filenames:

- `rendering_pipeline_topology.txt`
- `rendering_sharedrt_health.txt`
- `rendering_material_bindings.txt`
- `rendering_volume_blender.txt`
- `rendering_globals.txt`
- `rendering_report_index.txt`

Reports are diagnostic artifacts. Do not commit generated files from `Temp/`.

## 10. Authoring Checklist

- README / PROJECT_INDEX link to this guide when rendering authoring rules change.
- `Interfaces/Rendering.md` matches public type and method names.
- `RENDERING_PIPELINE.md` matches current URP baseline and Volume boundary.
- Rendering APIs avoid business terms and use `MxRenderSubjectId`.
- New pass work enters through `IMxRenderPass` / `IMxRenderPassProvider`.
- SharedRT keys have stable ids, explicit writer sets, and documented conflict expectations.
- Material writes use `IMaterialBindingHub`.
- VolumeBlender work stays at request arbitration and diagnostics unless a runtime URP Volume application task explicitly lands.
- Demo/showcase claims match actual scene and runtime entries.
