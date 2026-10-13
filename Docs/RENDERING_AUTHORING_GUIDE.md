# MxFramework Rendering Authoring Guide

> Version 0.3 | 2026-10-12
>
> Status: Current
>
> Scope: Authoring rules for shader globals, camera globals, SharedRT keys, render passes/providers, material bindings, render data publishing, VolumeBlender request diagnostics, Volumetric Fog, demo showcase validation, and the rendering report bundle convention.

This is the single Rendering authoring guide. Keep design rationale in `RENDERING_FRAMEWORK_DESIGN.md`, public signatures in `Interfaces/Rendering.md`, and Unity pipeline baseline in `RENDERING_PIPELINE.md`.

## 1. Authoring Boundaries

Rendering is a Unity + URP-facing presentation layer. It may use UnityEngine, URP, CommandBuffer, RTHandle, MaterialPropertyBlock, Renderer, Texture, Color, and VolumeProfile inside `MxFramework.Rendering`.

Do not author Rendering content that:

- Adds WGame-specific terms, characters, elements, levels, Buff ids, or gameplay rules.
- Makes Core, Runtime, Gameplay, Combat, Buffs, Resources, Runtime AI Planner, or other noEngine modules reference `MxFramework.Rendering`.
- Writes Rendering state into runtime authority, Replay hash, Runtime result hash, or SaveState.
- Adds independent framework `ScriptableRendererFeature` assets for feature slices. Use `MxRenderingPipelineFeature` plus `IMxRenderPass` or `IMxRenderPassProvider`.
- Bypasses `IMaterialBindingHub` with direct `Renderer.SetPropertyBlock(...)` from Rendering or bridge code.

### 1.1 WGame Shader Family Example

WGame shader samples under `Assets/Art/WGame` use a project-specific tech-art profile documented in `Docs/Validation/WGameShaderFamily/ShaderTextureMaterialBudget.md` and closed out in `Docs/Validation/WGameShaderFamily/ShaderFamilyFullReview.md`. Treat those documents as examples of project-level shader budget and review records. They are not MxFramework public API and are not framework-wide material, texture, bloom, or overdraw rules.

### 1.2 First-Party Stock URP Lit Boundary

The reusable framework remains URP-facing and does not restrict consumer-project shader choice. The WGame repository's own versioned content has a stricter authoring rule: stock `Universal Render Pipeline/Lit` and `Universal Render Pipeline/Simple Lit` are not valid final shaders for first-party materials, embedded scene/prefab materials, generated model material subassets, generators, runtime-created materials, or tests.

Use the semantic WGame family instead:

| Content role | Required family |
| --- | --- |
| Baked characters | `WGame/CharacterStylizedLit` |
| Historical palette characters and flat generated character previews | `WGame/CharacterPaletteLegacy` |
| Props, weapons, interactables, and neutral solid-color diagnostics | `WGame/PropStylizedLit` |
| Ground, walls, stages, obstacles, and physical surfaces | `WGame/EnvironmentStylizedLit` |
| Foliage cards and moss patches | `WGame/FoliageMossLit` |
| Spatial rings, traces, and world markers | `WGame/WorldMarkerUnlit` |
| Transparent magic effects | `WGame/MagicVfxUnlit` |
| Impact and crack decals | `WGame/ImpactDecal` |
| Local ground-covering shallow-water film | `WGame/ShallowWater` |

Generated FBX assets under `Assets/MxFrameworkGenerated` import with material import disabled; reviewed external WGame materials remain the renderer-binding authority. Stock URP Unlit and Particles Unlit remain allowed for UI, line, and particle paths. Authoring generators must fail when a required WGame shader is unavailable instead of silently falling back to a stock lit shader.

The repository's CharacterTest tactical overlays and owned VFX material builders now use the WGame family rather than that generic allowance. `CharacterTestUnlitMaterials` is their shared initialization-time material setup: mesh-shaped overlays use `WorldMarkerUnlit` with `_WG_MARKER_MESH`; texture/vertex-colored particles and lines use `MagicVfxUnlit` with `_WG_VFX_TEXTURED`. Missing required project shaders fail explicitly. Runtime and Editor creators share this setup, and the two shaders are explicitly included in Graphics Settings for runtime-created materials. Generic framework tooling and unmodified third-party source shaders are outside this project migration.

In textured VFX mode, `_BaseMap`/`_BaseColor`, `_EmissionMap`/`_EmissionColor` and the standard blend/depth/cull properties are intentional Unity interoperability inputs: they preserve existing particle textures, HDR tint, vertex colors, lifetime alpha and material-property bindings. `_WgVfxColor` and the procedural noise/shape controls belong to the existing procedural mode. Do not configure textured VFX through `Material.color`; use `CharacterTestUnlitMaterials.SetVfxColor` or `_BaseColor`. The textured variant does not execute procedural noise, shape, pulse or depth-fade sampling. Marker mesh mode requires neither UV nor vertex color and retains the mesh's exact footprint; its `_WgSrcBlend`/`_WgDstBlend`/`_WgZWrite` properties intentionally avoid obsolete blend values left on older marker materials.

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

The project shallow-water sample uses one existing feature provider for the bound
Game camera and prepared Scene previews; no reflection graph includes the water pass.
Performance/Balanced tiers project bounded screen-space inputs into transient SSPR
textures in each receiving camera's graph. Quality uses a bounded reflection camera
and an owned persistent RTHandle; `MxSceneRenderingAuxiliaryCamera` permits shared
world lighting/shadow resources. The backends are mutually exclusive. Refraction
alone requires the water-owned scene-color copy; SSPR borrows the current graph's
opaque color/depth without exporting their handles. See the
[sample guide](Guides/SHALLOW_WATER_SAMPLE.md) for creation, tuning, cleanup and
the current validation boundary.

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

## 7.1 Volumetric Fog Slice

`MxVolumetricFogPassProvider` is the Rendering-owned v1 global height fog slice. It is a PC URP-oriented fullscreen pass, not a URP Volume Profile override and not HDRP-style local volumetric lighting.

Authoring rules:

- Register it through `MxRenderingPipelineFeature` as an `IMxRenderPassProvider`; do not add a second framework renderer feature.
- Use `MxVolumetricFogSettings.ForQuality(...)` or an explicitly constructed settings value for density, height falloff, max distance, anisotropy, scattering, noise, quality sample budget, diagnostic downsample, temporal stability intent, and view-focus clear radius/softness.
- Keep shader properties under `_MxVolumetricFog*` and add ids to `MxRenderingShaderIds` before runtime code writes them.
- v1 is a single-pass global height fog composite. It does not perform multi-step ray marching, downsampled resolve, temporal history accumulation, or reprojection. Sky pixels use a horizon-weighted height density fallback, so scene atmosphere should not rely on Unity built-in `RenderSettings.fog`. The quality sample budget, diagnostic downsample, and temporal stability intent fields are budget/diagnostic/future-policy signals only.
- Stable SharedRT ids `mxframework.rendering.volumetric-fog.accumulation` and `mxframework.rendering.volumetric-fog.history` are reserved for future actual producer/consumer implementations. v1 does not register, allocate or declare reads/writes for either id.
- Diagnostics expose hypothetical accumulation size and ARGBHalf memory budget, quality, skip reason, budget/intent fields, and view-focus clear settings. Size/budget do not report allocated GPU storage or temporal history. Do not create an unused render texture merely to populate diagnostics.
- v1 skips non-Game cameras by default. `MxVolumetricFogSettings.WithSceneViewPreview(true)` may be used for editor art-review scenes that need SceneView visibility; keep it disabled for ordinary gameplay defaults and continue skipping Preview / Reflection cameras.
- Do not claim local fog volumes, multi-light shadowed volumetric lighting, runtime URP Volume object application, Gameplay authority, Replay hash, Runtime result hash, or SaveState integration.

## 8. Demo Showcase Run Instructions

The Rendering Demo Slices Showcase verifies the infrastructure without shipping production grass, water, character, post-processing presets, or game-specific assets.

Entries:

- Scene: `Assets/Scenes/RenderingDemoSlicesShowcase.unity`
- Runtime: `Assets/Scripts/WGameFrameworkProject/Demos/Rendering/RenderingDemoSlicesShowcaseRuntime.cs`
- Composition root: `Assets/Scripts/WGameFrameworkProject/Demos/Rendering/RenderingDemoSlicesShowcaseRoot.cs`
- HUD: `Assets/Scripts/WGameFrameworkProject/Demos/Rendering/RenderingDemoSlicesHudController.cs`
- Scene generator: `MxFramework / Rendering / Create Demo Slices Showcase Scene`

Manual smoke:

1. Open `Assets/Scenes/RenderingDemoSlicesShowcase.unity`.
2. Enter Play Mode.
3. Press `1`, `2`, `3`, `4`, or `5`, or use the HUD buttons, to switch wind context, material pulse, publisher event burst, VolumeBlender priority, and Volumetric Fog quality.
4. Press `R` or the HUD reset button to reset.
5. Confirm the HUD reports Context, SharedRT / FeaturePipeline, MaterialBindingHub, RenderDataPublisher, VolumeBlender diagnostics, Volumetric Fog diagnostics, and recent events.

Showcase boundaries:

- Context slice uses `GlobalFrameContext` / `MxRenderingShaderIds`; demo code does not directly call `Shader.SetGlobal*`.
- SharedRT slice uses synthetic pass/provider metadata and `ISharedRenderTextureRegistry`; it does not add another `ScriptableRendererFeature`.
- Material slice uses `IMaterialBindingHub` / `IMaterialBindingWriter`; it does not call `Renderer.SetPropertyBlock(...)` directly.
- Publisher slice uses generic `IRenderDataPublisher` events and `MxRenderSubjectId`.
- Volume slice shows `IVolumeBlender` arbitration and diagnostics only; it does not create runtime URP `Volume` objects.
- Volumetric Fog slice uses `MxVolumetricFogPassProvider` / `MxVolumetricFogPass`, accumulation SharedRT diagnostics, and the fullscreen composite shader; it does not add another renderer feature or claim temporal history.

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

Rendering report bundles are a future reporting convention, not an implemented exporter API. When a manual bundle is needed for review or QA, collect read-only diagnostics under:

```text
Temp/MxFrameworkReports/Rendering/
```

Expected manual bundle filenames:

- `rendering_pipeline_topology.txt`
- `rendering_sharedrt_health.txt`
- `rendering_material_bindings.txt`
- `rendering_volume_blender.txt`
- `rendering_volumetric_fog.txt`
- `rendering_globals.txt`
- `rendering_report_index.txt`

Reports are diagnostic artifacts. Do not commit generated or manually collected files from `Temp/`.

## 10. Authoring Checklist

- README / PROJECT_INDEX link to this guide when rendering authoring rules change.
- `Interfaces/Rendering.md` matches public type and method names.
- `RENDERING_PIPELINE.md` matches current URP baseline and Volume boundary.
- Rendering APIs avoid business terms and use `MxRenderSubjectId`.
- New pass work enters through `IMxRenderPass` / `IMxRenderPassProvider`.
- SharedRT keys have stable ids, explicit writer sets, and documented conflict expectations.
- Material writes use `IMaterialBindingHub`.
- First-party materials, scene/prefab embedded materials, generated model imports, generators, runtime-created materials, and tests pass the zero stock URP Lit audit.
- VolumeBlender work stays at request arbitration and diagnostics unless a runtime URP Volume application task explicitly lands.
- Volumetric Fog work stays as global height fog unless a later local-volume or multi-light volumetric spec explicitly lands.
- Demo/showcase claims match actual scene and runtime entries.
