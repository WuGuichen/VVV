# Rendering 接口

> Status: Current

`MxDirectionalShadowData` format 3 explicitly distinguishes raw depth (encoding 0)
from fixed-plane coverage in six low mantissa bits (encoding 1). Existing format-2
raw assets remain readable; unknown versions/encodings are rejected. `PlaneCoverage`
returns `(encoding, horizontalWorldY, receiverYTolerance, bakedWorldBias)`; raw data
returns zero. The existing `Initialize` overloads remain raw-depth initializers; a
new trailing-Vector4 overload initializes coverage metadata. `InvalidateForRebuild`
marks in-place texture writes unusable until initialization completes.

`MxDirectionalShadowBaker.Bake` and `Fingerprint` add overloads accepting that
explicit contract; previous signatures and raw fingerprints are retained. Coverage
is evaluated offline against the declared plane, unioning 8x8 accepted depth/alpha
samples before packing, while retaining quantized original centre depth for other
receivers. Centre-only reuse requires identical encoding/plane/bias metadata.
`CharacterTestDirectionalShadowAuthoring.BakeWithPlaneCoverage` is the project
authoring opt-in; ordinary Bake preserves an already selected plane. The provider
binds one additional float4, cleared by the existing camera reset. Texture formats,
dimensions, dynamic atlas, 2x2 read count and direct-light/GI ownership do not change.
Changing the plane, bias, sources, or encoding requires explicit baking. GPU bit
round-trip and representative grass visual acceptance are separate native gates.



> Version 0.3 | 2026-10-01
>
> Status: Current
>
> Implementation state: Current through Phase 15.8 infrastructure and demo showcase plus Volumetric Fog v1 global height fog. VolumeBlender includes request arbitration and diagnostics; runtime URP Volume object application remains a follow-up integration step.
>
> Scope: Public API shape for `MxFramework.Rendering` through Phase 15.8 demo showcase and Volumetric Fog v1. Feature-specific shader internals are out of scope.

## 职责

`MxSceneLightingSourceLayout.EmissionIntensityProperty` is an optional material float-property mapping. The compiler multiplies surface-data emission by this non-negative finite HDR scalar before area aggregation; an empty mapping or absent property keeps the legacy multiplier of 1. Empty mappings preserve the legacy fingerprint. Configured mappings and live material values are part of the source fingerprint, so changing intensity requires a transport rebuild. The framework does not hardcode a project shader property. The payload format is unchanged.

Editor-only `MxSceneLightingBuilder.PlanProbeGrid` returns `MxSceneLightingProbeGrid` without capture, material readback or asset writes. It uses the same selected sources, world triangles, subdivision/degenerate rejection and lattice calculation as the compiler. `CaptureBounds` describes accepted geometry; `GridBounds` describes candidate lattice endpoints. Origin is capture minimum minus one spacing; each dimension is `ceil((capture size + 2 * spacing) / spacing) + 1`. Maximum padding can exceed one spacing because of ceiling. Null/empty source selection uses active descendants with enabled MeshRenderers and MeshFilters; Static flags and layers are not additional filters. Candidate validity is unknown before capture. `MxSceneRenderingConfiguration.ProbeSpacing` defaults to 4; finite values are clamped to at least 1 by the builder. The Level Editor uses this same input for planning and explicit builds, with no automatic rebuild. `CacheMatchesSource` preserves the legacy source fingerprint format and separately rejects spacing mismatches; Editor preview and player-build source checks share that rule. Changing spacing is an authoring invalidation, not a live probe-layout update.

Editor-only `MxSceneLightingProbePreview` owns a disposable decoded CPU payload, refreshed by data/payload dependency hashes and metadata. It exposes planned versus actual cached positions, saved `PositionValid.w`, and deterministic capped slice/radius/validity filtering. Drawing does not decode assets or allocate runtime buffers. The project Scene Rendering panel owns its SceneView overlay only while attached; preview controls are transient and leaving the panel releases the payload and subscriptions. Stale cached positions remain inspectable and explicitly marked stale. A selected point exposes the saved 96 direction-bin distance means/second moments, representative bin directions and transport-link count. These are capture statistics and heuristic validity, not exact ray hits, runtime radiance, relocation or adaptive placement. No scene probe GameObjects are created.

Scene light transport uses `MxSceneLightingData`, `MxSceneLightingLook`, `MxSceneLightingSourceLayout`, `MxSceneLightingPayload`, `MxSceneLightingProvider`, `MxSceneRenderingConfiguration` and `MxSceneRenderingSession`. The project's GameManager serializes the configuration; the plain C# session owns lighting/fog registration and disposal. Data is compiled from static geometry and explicit material mappings; the provider owns its persistent GPU buffers, updates on sun/sky changes and targets an explicit Game camera with opt-in Scene View preview. A Game camera with `MxSceneLightingPreviewCamera` also receives that scene's lighting while its preview surface is alive. Camera motion does not invalidate world radiance. Data/pose/cache state remains presentation-only. The original provider/session constructors still decode transport on demand; an additional overload accepts a previously validated payload reader for Editor preview reuse. The editor checks transport and data asset dependency hashes before reusing that CPU copy, while each session still owns and releases its GPU resources. See `Docs/Guides/SCENE_LIGHT_TRANSPORT.md` for construction, binary format, limitations and validation.

`MxSceneLightingLook.SpecularStrength` and `LightRimStrength` are optional non-negative material-response controls, both defaulting to zero. The existing camera resolve publishes them as `_MxSceneSurfaceHighlights.xy` (`zw` reserved zero); consumers must gate reads on `_MxSceneLightingEnabled` to avoid another camera's stale state. Editor preview copies these fields into its existing Look clone. They do not change transport injection inputs, source fingerprints, world-buffer allocation or the radiance/visibility invalidation rules. Material shaders own the profile-specific response and must respect main-light visibility; these controls add no render pass or additional light. Existing Looks remain opt-out and existing surface emission retains its material authority.

`IMxRenderGraphPass : IMxRenderPass` is an optional exclusive recording contract: `ScriptableRenderPassInput InputRequirements` and `void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, in MxRenderPassConfigureContext context)`. The wrapper calls this method instead of the legacy Execute/fullscreen path. It is Unity/URP-facing and does not change noEngine boundaries. Existing pass implementations are unaffected. Recording may allocate graph pass data; static scene building and payload loading are low-frequency allocating operations.

`MxSceneRenderingAuxiliaryCamera` explicitly associates a secondary camera with
`SourceCamera` in the same loaded scene. `Kind` supplies the framework camera
descriptor (default Reflection); `InvertCulling` requests reflected winding within
that camera's feature scope. Camera globals start with the requested winding and
the existing end lifecycle resets it. `TryGet` and `Matches` reject disabled or
destroyed markers. Lighting and directional-shadow providers admit this explicit
association to the source camera; they retain their existing world-resource owner
and perform camera-dependent resolves separately. No screen-space TextureHandle
crosses camera graphs. The association alone does not allocate a camera, request
pose work, or register a project water pass. The project sample's explicit pose
admission and lifecycle are documented in [Shallow water](../Guides/SHALLOW_WATER_SAMPLE.md).

The project shallow-water Scene View extension uses one independent reflection
context per visible Scene camera while retaining the configured Game camera as
the world-lighting owner; it does not change this package's auxiliary-camera API
or share screen-space resources between camera graphs. Native evidence and
implementation status remain in the linked sample guide.

Project water authoring belongs to the Level Editor scene-rendering module.
GameManager serializes the project-only configuration and owns a disposable plain
C# water session; authored water MonoBehaviours are legacy migration inputs only.
The module includes shared water settings and the owner in the existing coordinated
save snapshots. This adds no package-to-project dependency or gameplay JSON schema;
usage and native evidence remain in the linked sample guide.

Project water quality tiers, same-graph SSPR compute passes, capture-based planar-camera
cadence, copy-free composition and per-view cached local ripple fields are project-owned implementation details.
They preserve this package's camera/resource contracts; budgets, approximation and
validation limits are maintained in the [shallow-water guide](../Guides/SHALLOW_WATER_SAMPLE.md#性能档与有界涟漪2026-10-11).

`MxSceneLightingProvider` implements `IMxRenderPassProvider`/`IDisposable`, accepts explicit data, Look, sun, camera and shader references, and exposes functional visibility/radiance update counters and estimated persistent bytes. `SetCapsules` copies at most 32 supplied capsules without per-call allocations. `SetTargetCamera(Camera, bool previewInSceneView)` retargets camera work without reallocating or invalidating world resources; a null camera suspends collection and retains the cache. `SetMotionVectorsRequired(bool)` explicitly requests motion input; `MotionVectorsRequired` also includes Look debug mode 3. Both setters reject a disposed provider. `MxSceneRenderingConfiguration.RequireMotionVectors` defaults false and supplies the explicit consumer request. The session temporarily enables skinned motion only while requested, restores prior settings, owns registration/unregistration and resets camera history; other URP effects retain independent motion requirements. The `SourceLayout` maps consumer material properties and normalized profile diffuse fractions without embedding project shader names in the framework.

`MxSceneLightingFrameData : ContextItem` publishes borrowed `TextureHandle` fields `Irradiance`, `SceneColor`, `Depth`, `Normals`, `Motion`, plus `Camera`, `ColorDescriptor`, `ColorSpace`, `PreExposure` and `HistoryGeneration`. SceneColor publication occurs at `BeforeRenderingPostProcessing`, order -100; consumers must record later within the same camera graph and declare their resource reads. Handles expire with that graph. Motion is optional: consumers must request it and check handle validity; GI does not request it by default. `PreExposure=1` means this provider applies no pre-exposure, not that all future post effects preserve exposure. `SetHistoryGeneration(int)` propagates the scene history generation to this context. Camera/scene identity must also participate in a temporal consumer's history key. Runtime changes to `TargetCamera` update the existing provider, reset camera history and retain world buffers/counters. Explicit `Rebind()` remains a full resource rebuild for regenerated assets or changed world configuration; disabling/unloading still releases resources. No export-copy or DLSS SDK contract is implied.

Editor-only `MxSceneLightingBuilder.BuildOccluders(Transform)` returns a caller-owned position-only world mesh using original triangles and exact position welding, independent of offline patch subdivision. Its optional explicit `MeshRenderer` selection lets mixed test scenes build only stationary material-compatible sources without duplicating or reparenting scene geometry; null/empty selection preserves the original whole-root fingerprint and build behavior. `MxSceneRenderingConfiguration.StaticSourceRenderers` stores this selection with the GameManager. Fingerprint, offline transport capture, sun occluders, preview validation and player-build validation all consume the same selection. `RebuildOccluders(MxSceneRenderingConfiguration)` requires a matching source fingerprint and updates the generated mesh asset in place, preserving its GUID/file ID and the existing transport payload. The Level Editor project module `characterTest.sceneRendering` owns authoring, fingerprint diagnostics, build adoption and preview. Old `MxSceneLightingScene` and `MxHeightFogScene` classes contain serialized migration data only and register no providers.

`WorldUpdatePassesRecorded` and `SunDepthTexturesCreated`, exposed by both provider and scene, are functional cumulative recording counters. A stable camera records no world update pass. Light inputs are captured by value during recording and marked applied only when the compute pass executes. Sun rotation records a separate raster pass with an explicitly dependent, transient depth attachment; color/sky-only changes record compute only. `SunDepthTexturesCreated` counts graph handle creation, not physical allocations in RenderGraph's pool. World resource bindings persist independently of per-camera capsule candidate buffers.

Capsule reconstruction uses per-camera 16x16 tiles of the half-resolution target and a 32-bit candidate mask. Influence fades between radii R and 1.25R, where R is derived from the capsule radius, strength and active count. The sum of omitted maximum terms is bounded by 0.01, so the absolute change in the multiplicative occlusion factor is at most 0.01 (1% of unoccluded indirect irradiance). This is a controlled approximation to the former infinite-distance tail, not a performance guarantee. Zero strength/count bypasses tile generation; outside-field and zero-irradiance pixels bypass capsule evaluation. Near-plane intersection and stereo bounds use conservative coverage.

Rendering 提供 Unity + URP-facing 的渲染编排接口：全局帧上下文、相机作用域上下文、URP 总入口 Feature、Pass/Provider 注册、SharedRT 注册与冲突诊断、MaterialBindingHub、RenderDataPublisher、VolumeBlender request arbitration / diagnostics、Volumetric Fog 和 bridge 公共边界。

Rendering 是表现层能力，不进入 Gameplay / Combat authority、Runtime result hash、Replay hash 或 SaveState。

## 程序集

| 程序集 | 依赖 | 说明 |
| --- | --- | --- |
| `MxFramework.Rendering` | `MxFramework.Core`, `MxFramework.Diagnostics`, UnityEngine, URP | Runtime-facing rendering orchestration |
| `MxFramework.Rendering.Editor` | `MxFramework.Rendering`, UnityEditor | Authoring, validation, inspectors, asset menus |
| `MxFramework.Rendering.GameplayBridge` | Implemented 15.6 subset | Optional bridge from Gameplay public lifecycle events to Rendering |
| `MxFramework.Rendering.CombatBridge` | Planned | Optional bridge from Combat public events to Rendering |
| `MxFramework.Rendering.CharacterBridge` | Planned | Optional bridge from Character-facing public events to Rendering |
| `MxFramework.Rendering.CameraBridge` | Optional later | Optional bridge from Camera public state to Rendering |

Rendering EditMode tests compile into `MxFramework.Tests` unless a separate test mode, platform, or dependency-isolation boundary is required.

`MxFramework.Rendering` does not depend on any bridge assembly and does not depend on `MxFramework.DebugUI`.

## Scene dust and shared environment sampling

`MxSceneRenderingConfiguration` adds opt-in `DustEnabled`, explicit `DustShader`,
`PreviewDustInSceneView`, and serialized `MxSceneDustSettings Dust`. Settings contain
`ParticleCount` (0..8192), world-space `Radius`, `BaseHeight`, `Height`, `Size`,
`Opacity`, `DriftSpeed`, `Anisotropy`, `LightResponse`, sRGB `Color`, and `Seed`.
Existing scenes remain disabled. `MxSceneRenderingSession` owns registration,
retargeting, disposal and `DustIssue` / `HasDust` / `DustDrawsRecorded` diagnostics.
Dust works with GI and height fog disabled. A missing explicit shader binding is
reported rather than silently relying on Shader.Find in a Player build.

`MxSceneLightFieldData : ContextItem` publishes borrowed `BufferHandle` fields
`Probes`, `Coefficients`, `Visibility`, `TextureHandle SunVisibility`, grid and sun
transforms/inputs, and `IndirectStrength`. `IsValid` describes the current camera
graph only; `Reset()` invalidates all handles. The scene lighting provider remains
the sole owner/updater of persistent storage. Consumers record after its resolve,
declare their reads, and copy handles/parameters into their own graph pass data.
No additional world cache or lighting update is allocated for dust.

Shared framework HLSL contracts:

- `MxFrameInputs.hlsl` owns declarations formerly in project `WGameFrameGlobals`.
  `GlobalFrameContext.SetTime` marks `_MxTimeProvided`; when absent, shared clock
  helpers use Unity's render clock. Explicit zero/paused clocks remain valid.
- `MxWind.hlsl` shares directional sway between foliage color/depth/shadow passes
  and dust. It consumes the existing global wind parameters, not a new wind grid.
- `MxLightField.hlsl` shares probe layout, visibility indexing and depth-moment
  visibility. Medium sampling uses no surface normal/tangent/capsule-AO weighting.
  It convolves the cache's first angular moments with an L1 phase approximation;
  screen-space surface irradiance is not used to light airborne particles.
- `MxSunVisibility.hlsl` shares the existing static sun visibility lookup between
  light injection and dust. URP main-light shadows additionally supply dynamic
  visibility; the configured scene sun is expected to be the URP main light.
- `MxMedium.hlsl` shares height density, focus clearing, transmittance and the
  normalized Henyey-Greenstein phase. Height fog retains its existing endpoint
  approximation; this is not general volumetric integration.

The internal dust provider records one procedural instanced raster draw at
`BeforeRenderingPostProcessing`, order 50, after scene height fog at order 40.
World-periodic positions have faded wrap boundaries and analytic drift; no CPU
particle loop, particle simulation buffer or additional fullscreen target is used.
Per-camera coverage is centered on the central view ray's forward intersection with
the middle of the dust layer, when within the camera range. Horizontal views use a
small forward offset. This keeps high tactical cameras' dust coverage in view;
coverage does not blindly follow the camera's ground projection. Particle identities
remain attached to the periodic world distribution while the faded window moves.
Depth tests/soft intersections sample current opaque depth. Dust applies shared
fog at its own position before premultiplied composition over the already-fogged
background. It adds sparse grain, not another continuous fog density.
Transparent surfaces without depth cannot occlude dust. World cache sampling is
limited to the existing probe field; without a valid field dust uses scene ambient
and URP main light. No temporal accumulation or XR validation is claimed.

Focused verification: `SceneDustTests` covers independent lifecycle/camera ownership
and GPU medium sampling with known isotropic/directional energy. Existing
`SceneLightingTests` and `VolumetricFogTests` cover the reused source paths.

## 1. Subject Identity

> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/RenderSubjectIdentity.cs`

```csharp
public readonly struct MxRenderSubjectId : IEquatable<MxRenderSubjectId>
{
    public MxRenderSubjectId(int value);
    public int Value { get; }
    public bool IsValid { get; }
}

public enum MxRenderSubjectRole
{
    None = 0,
    Primary = 1,
    LocalControlled = 2,
    Focus = 3,
    Tracked = 4
}

public interface IRenderSubjectMap<TSourceId>
{
    bool TryResolve(TSourceId sourceId, out MxRenderSubjectId subject);
    MxRenderSubjectId GetOrCreate(TSourceId sourceId, MxRenderSubjectRole role);
    bool Release(TSourceId sourceId);
}
```

Rules:

- Rendering public APIs use `MxRenderSubjectId`, not source module entity ids.
- `MxRenderSubjectId` values are stable for the lifetime of a subject registration.
- Released ids may be reused only after the registry can prove no active binding, SharedRT writer, or publisher event references the old subject.
- `MxRenderSubjectRegistry.Clear()` invalidates all active registrations before release notifications. Consumers clean references using the old complete subject id during those notifications; generation advancement and free-queue rebuilding follow. Slots with outstanding references remain unavailable.
- After reference cleanup, `Clear()` preserves the configured slot capacity, including slots released before the call or made available during release notifications. Repeated calls without active registrations do not lose available slots or emit duplicate release notifications. Reusing a slot produces a new generation; old complete subject ids remain invalid.
- Registry `Clear()` invalidates registrations across its maps. `RenderSubjectMap<TSourceId>.Clear()` releases only that map's registrations.
- Public API names must not expose game-specific identity terms.

## 2. Rendering Context

```csharp
public interface IGlobalFrameContext
{
    void SetTime(float time, float gameTime, float deltaTime);
    void SetWind(Vector3 direction, float strength, float turbulence);
    void SetWeather(float wetness, float rain, float snowCoverage);
    void SetPrimarySubjectPose(Vector3 worldPosition, Vector3 velocity);
    void SetLocalSubjectPose(Vector3 worldPosition, Vector3 velocity);
    GlobalFrameSnapshot Snapshot();
}

public readonly struct GlobalFrameSnapshot
{
    public float Time { get; }
    public float GameTime { get; }
    public float DeltaTime { get; }
    public Vector3 WindDirection { get; }
    public float WindStrength { get; }
    public float WindTurbulence { get; }
    public float Wetness { get; }
    public float Rain { get; }
    public float SnowCoverage { get; }
    public Vector3 PrimarySubjectWorldPos { get; }
    public Vector3 PrimarySubjectVelocity { get; }
    public Vector3 LocalSubjectWorldPos { get; }
    public Vector3 LocalSubjectVelocity { get; }
}
```

```csharp
public enum MxCameraRenderKind
{
    Unknown = 0,
    Game = 1,
    SceneView = 2,
    Reflection = 3,
    Preview = 4
}

public interface ICameraRenderContext
{
    MxCameraRenderKind CurrentCameraKind { get; }
    void SetViewFocus(Vector3 worldPosition);
    void SetCameraOverride(int propertyId, Vector4 value);
    CameraRenderSnapshot Snapshot();
}

public readonly struct MxCameraRenderContextDescriptor
{
    public MxCameraRenderKind CameraKind { get; }
    public Camera Camera { get; }
    public Vector3 ViewFocusWorldPosition { get; }
}

public sealed class CameraRenderSnapshot
{
    public MxCameraRenderKind CameraKind { get; }
    public Camera Camera { get; }
    public Vector3 ViewFocusWorldPosition { get; }
    public IReadOnlyList<CameraShaderOverride> Overrides { get; }
}

public readonly struct CameraShaderOverride
{
    public int PropertyId { get; }
    public Vector4 Value { get; }
}
```

Shader property ids are centralized:

```csharp
public static class MxRenderingShaderIds
{
    public static readonly int MxTime;
    public static readonly int MxGameTime;
    public static readonly int MxDeltaTime;
    public static readonly int MxWindDirection;
    public static readonly int MxWindStrength;
    public static readonly int MxWindTurbulence;
    public static readonly int MxWetness;
    public static readonly int MxRain;
    public static readonly int MxSnowCoverage;
    public static readonly int MxPrimarySubjectWorldPos;
    public static readonly int MxPrimarySubjectVelocity;
    public static readonly int MxLocalSubjectWorldPos;
    public static readonly int MxViewFocusWorldPos;
}
```

`GlobalFrameContext` writes frame-global values once per frame. `CameraRenderContext` writes camera-scoped values through URP camera rendering. The same shader property id must not be owned by both contexts.

## 3. URP Integration

```csharp
public sealed class MxRenderingPipelineFeature : ScriptableRendererFeature
{
    public override void Create();
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData);
}
```

```csharp
public interface IMxRenderPipeline
{
    bool RegisterPass(IMxRenderPass pass);
    bool UnregisterPass(string debugName);
    bool RegisterProvider(IMxRenderPassProvider provider);
    bool UnregisterProvider(string debugName);
    IReadOnlyList<IMxRenderPass> CollectPasses(in MxCameraRenderContextDescriptor cameraContext);
    MxRenderPipelineTopologySnapshot CaptureTopology();
}

public interface IMxRenderPass
{
    string DebugName { get; }
    MxRenderPhase Phase { get; }
    int Order { get; }
    bool IsEnabled { get; }
    IReadOnlyList<SharedRenderTextureKey> Reads { get; }
    IReadOnlyList<SharedRenderTextureKey> Writes { get; }
    void Configure(in MxRenderPassConfigureContext context);
    void Execute(in MxRenderPassExecuteContext context);
}

public interface IMxRenderPassProvider
{
    string DebugName { get; }
    void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor cameraContext);
}
```

Public pass read/write collections use `IReadOnlyList<T>`. Implementations must keep these collections stable and non-allocating after construction. Internal implementations may use spans or arrays, but public contracts must not expose `ReadOnlySpan<T>` as stored state.

`MxRenderPhase` is the framework-safe mapping of URP `RenderPassEvent`:

```csharp
public enum MxRenderPhase
{
    BeforeRendering = 0,
    BeforeRenderingShadows = 100,
    AfterRenderingShadows = 200,
    BeforeRenderingPrePasses = 300,
    AfterRenderingPrePasses = 400,
    BeforeRenderingOpaques = 500,
    AfterRenderingOpaques = 600,
    BeforeRenderingTransparents = 700,
    AfterRenderingTransparents = 800,
    BeforeRenderingPostProcessing = 900,
    AfterRenderingPostProcessing = 1000,
    AfterRendering = 1100
}
```

Sorting rule:

```text
enabled passes
  -> collect static passes + provider passes for current camera
  -> validate duplicate debug names
  -> sort by Phase, then Order, then DebugName ordinal
  -> validate SharedRT read/write order and writer policy
  -> enqueue URP passes
```

Current SharedRT frame lifecycle is scoped to one URP camera render invocation: `MxRenderingPipelineFeature` starts the SharedRT frame synchronously in `AddRenderPasses` before calling any `IMxRenderPass.Configure`, then enqueues camera globals, feature passes, and a SharedRT `EndFrame` pass after feature passes. This keeps Configure-side SharedRT reads/writes on fresh frame state, and keeps SceneView, Preview, reflection, and Game cameras isolated until a later Unity-runtime task has stronger evidence for a different multi-camera frame policy. In the Unity `6000.6.3f1` / URP `17.6.0` baseline, the internal camera globals, SharedRT lifecycle, and feature wrapper passes use `RecordRenderGraph`; those RenderGraph paths record unsafe passes with culling disabled so camera globals, feature command buffers, and EndFrame execute when RenderGraph is active.

Same `Phase + Order` is allowed only when pass dependencies do not read/write the same SharedRT and debug names provide deterministic ordering. Conflicts are reported through Diagnostics.

Phase 15.3 topology diagnostics expose the sorted pass list, camera kind, duplicate debug names, invalid metadata, and same `Phase + Order` SharedRT read/write collisions through `MxRenderPipelineTopologySnapshot` and `RenderPipelineTopologyDebugSource`. These diagnostics are read-only and do not require Debug UI.

## 4. SharedRT Registry

```csharp
public readonly struct SharedRTId : IEquatable<SharedRTId>
{
    public SharedRTId(string value);
    public string Value { get; }
    public bool IsValid { get; }
}

public readonly struct SharedRTOwnerId : IEquatable<SharedRTOwnerId>
{
    public SharedRTOwnerId(string value);
    public string Value { get; }
}

public readonly struct SharedRTWriterSetId : IEquatable<SharedRTWriterSetId>
{
    public SharedRTWriterSetId(string value);
    public string Value { get; }
}
```

```csharp
public readonly struct SharedRenderTextureKey : IEquatable<SharedRenderTextureKey>
{
    public SharedRTId Id { get; }
    public string DebugName { get; }
    public SharedRTOwnerId Owner { get; }
    public SharedRTAccessPolicy Access { get; }
    public SharedRTAnchor Anchor { get; }
    public SharedRTFormat Format { get; }
    public SharedRTSize Size { get; }
    public SharedRTClearSpec Clear { get; }
    public SharedRTResizePolicy Resize { get; }
    public long EstimatedMemoryBytes { get; }
}
```

`Id` participates in identity. `DebugName` does not participate in equality or hash code.

```csharp
public readonly struct SharedRTAccessPolicy
{
    public bool AllowAdditiveWriters { get; }
    public SharedRTOrderRule Order { get; }
    public SharedRTWriterSetId WriterSetId { get; }
}

public enum SharedRTOrderRule
{
    ReadAfterWriteSameFrame = 0,
    ReadPrevFrame = 1
}

public enum SharedRTAnchor
{
    World = 0,
    MainCamera = 1,
    PrimarySubject = 2,
    Static = 3
}

public enum SharedRTFormat
{
    R8 = 0,
    RHalf = 1,
    ARGB32 = 2,
    ARGBHalf = 3,
    Depth = 4
}

public enum SharedRTResizePolicy
{
    FailOnResize = 0,
    Reallocate = 1,
    KeepLargest = 2
}

public enum SharedRTClearKind
{
    NeverClear = 0,
    ClearEveryFrame = 1,
    RollClear = 2,
    FadeOut = 3
}

public readonly struct SharedRTClearSpec
{
    public SharedRTClearKind Kind { get; }
    public Color ClearColor { get; }
    public float FadeOutRate { get; }
}
```

```csharp
public interface ISharedRenderTextureRegistry
{
    SharedRTHandle Register(in SharedRenderTextureKey key);
    bool Unregister(SharedRTHandle handle);
    bool RegisterWriterSet(SharedRTWriterSetId id, IReadOnlyList<SharedRTOwnerId> allowedWriters);
    bool TryResolve(in SharedRenderTextureKey key, out RTHandle handle);
    bool TryResolve(SharedRTHandle handle, out RTHandle rtHandle);
    SharedRTDiagnosticsSnapshot CaptureDiagnostics();
}

public readonly struct SharedRTDiagnosticsEntry
{
    public SharedRTId Id { get; }
    public string DebugName { get; }
    public SharedRTOwnerId Owner { get; }
    public SharedRTFormat Format { get; }
    public SharedRTSize Dimensions { get; }
    public SharedRTResizePolicy Resize { get; }
    public long EstimatedMemoryBytes { get; }
    public long ActualMemoryBytes { get; }
    public bool IsAllocated { get; }
    public bool IsFallback { get; }
    public bool IsOrphaned { get; }
    public int OrphanFrameCount { get; }
    public IReadOnlyList<SharedRTOwnerId> CurrentFrameReaders { get; }
    public IReadOnlyList<SharedRTOwnerId> CurrentFrameWriters { get; }
    public IReadOnlyList<SharedRTResizeEvent> RecentResizeEvents { get; }
    public IReadOnlyList<SharedRTConflictEvent> RecentConflicts { get; }
}
```

Conflicts use stable rule ids:

```csharp
public enum SharedRTConflictCode
{
    AdditiveWritersAllowed = 1, // R-RT-01
    WriterConflict = 2,         // R-RT-02
    StaleReader = 3,            // R-RT-03
    UnauthorizedWriter = 4,     // R-RT-04
    OrphanRT = 5,               // R-RT-05
    ResizeRejected = 6,         // R-RT-06
    ResizeBurst = 7,            // R-RT-07
    DroppedAllocation = 8       // R-RT-08
}
```

The registry keeps a stable logical handle after orphan reclamation. Authorized registration, accepted writer/reader access, and an accepted same-size or `KeepLargest` resize restore missing storage at the retained dimensions. Unauthorized writers and stale readers do not revive allocations. Recovery creates new storage and does not restore prior texture contents; temporal consumers must rebuild their history. `TryResolve` alone preserves orphan reclamation and returns the shared black fallback; diagnostics report `IsAllocated=false`, `IsFallback=true`, and zero allocation bytes for this state. Allocation failures remain resolvable through fallback and are reported as `DroppedAllocation` by access/resize results.

## 5. Material Binding

`MaterialBindingHub` captures each target's property-block baseline when the first binding takes ownership. `Flush` merges current effects over that baseline; releasing an effect removes its overrides, and releasing the last binding followed by `Flush` restores the original block (or removes the slot override when it was originally absent). Empty slots inherit renderer-wide properties while effects are active. A later ownership period captures a new baseline. Appearance initialization must precede binding; refresh appearance on an already owned slot only after releasing its bindings and flushing, then bind again. Independent hubs/direct writers must not concurrently own the same slot. This preserves initial Palette/FaceDetail without cloning materials or retaining released effects.

Material binding is implemented as the framework-owned path for composable effect-property writes. Future hit flash、status、dissolve、outline、weather and debug features must not call `Renderer.SetPropertyBlock` directly. `CharacterPaletteBinding` is the narrow import-generated appearance exception for the explicit `mx.characterPaletteBinding.v5` path: it may bind the shared RGB Color Palette, Atlas dimensions, and optional character-owned FaceDetail texture/intensity in the existing per-slot `MaterialPropertyBlock` when the prefab initializes or its renderer hierarchy changes. Authored color and Surface Profile remain immutable mesh data (`PaletteUV.xy` and `PaletteData.A`); the component exposes no runtime row, color, Profile, Atlas, FaceDetail preset, or feature-color mutation API, never calls `renderer.material`, and does not own effect channels. If authored Palette selection later becomes a general Rendering channel, ownership moves into `MaterialBindingHub` rather than creating another direct writer.

The character shader surface is split: `WGame/CharacterStylizedLit` consumes baked BaseColor, SurfaceData and hair-flow mesh data only; `WGame/CharacterPaletteLegacy` retains palette/FaceDetail and flat-color compatibility. Both retain the existing effect properties consumed by MaterialBindingHub. The baked shader exposes no palette-binding properties or source-mode toggles, so runtime palette binding cannot target it. Historical mixed materials remain excluded when their baked flag is set; appearance migration is an explicit Editor operation, never a runtime mutation. Serialized lighting property identifiers retain their `_WgPalette*` names for value compatibility even on the baked shader.

Character diffuse uses the unmodified Scene Lighting and additional-light results in baked, Lite and palette paths, equivalent to the former skin light-color influence of 1. The skin-specific chroma correction, material property and Inspector control are removed; saved legacy values have no effect. Shared scene Look remains the lighting authority. Dark fabric/matte hue shaping does not impose a fixed body-brightness ceiling on evaluated lighting.

```csharp
public enum MxMaterialChannel
{
    HitFlash = 0,
    StatusTint = 1,
    DissolveProgress = 2,
    OutlineState = 3,
    WetnessOverride = 4,
    BridgeCustom = 5,
    DebugOverlay = 6
}

public enum MaterialBindingScopeKind
{
    Renderer = 0,
    RendererSubMesh = 1,
    SubjectHierarchy = 2
}

public interface IMaterialBindingHub
{
    MaterialBinding Bind(MxRenderSubjectId subject, MxMaterialChannel channel, in MaterialBindingScope scope);
    bool Release(MaterialBinding binding);
    void Release(MxRenderSubjectId subject);
    MaterialBindingDiagnosticsSnapshot CaptureDiagnostics();
}

public readonly struct MaterialBinding
{
    public int Id { get; }
    public MxRenderSubjectId Subject { get; }
    public MxMaterialChannel Channel { get; }
}
```

```csharp
public interface IMaterialBindingWriter
{
    void SetFloat(MaterialBinding binding, int propertyId, float value);
    void SetColor(MaterialBinding binding, int propertyId, Color value);
    void SetVector(MaterialBinding binding, int propertyId, Vector4 value);
    void SetTexture(MaterialBinding binding, int propertyId, Texture texture);
    void Pulse(MaterialBinding binding, int propertyId, in MaterialBindingCurveDescriptor curve, float duration);
}
```

`MaterialBindingCurveDescriptor` must not expose Unity `Keyframe` as the stable public contract. The implementation may convert to Unity curves internally.

## 6. VolumeBlender

VolumeBlender is the Rendering-owned request API for code-side URP Volume Profile blend intent. It does not replace Unity's URP Volume Framework. The Phase 15.7 implementation provides deterministic request arbitration plus diagnostics and does not create runtime `Volume` objects yet. A later integration may create, update, or recycle framework-owned URP `Volume` runtime objects inside `MxFramework.Rendering` while preserving this public contract.

Public request shape:

```csharp
public readonly struct MxVolumeRequestId : IEquatable<MxVolumeRequestId>
{
    public MxVolumeRequestId(ulong value);
    public ulong Value { get; }
    public bool IsValid { get; }
}

public readonly struct MxVolumeProfileReference : IEquatable<MxVolumeProfileReference>
{
    public string Key { get; }
    public VolumeProfile Profile { get; }
}

public enum MxVolumeRequestScopeKind
{
    Global = 0,
    CameraKind = 1,
    ExplicitCamera = 2
}

public readonly struct MxRenderingCameraToken : IEquatable<MxRenderingCameraToken>
{
    public MxRenderingCameraToken(ulong value);
    public ulong Value { get; }
    public bool IsValid { get; }
}

public readonly struct MxVolumeRequestScope
{
    public MxVolumeRequestScopeKind Kind { get; }
    public MxCameraRenderKind CameraKind { get; }
    public MxRenderingCameraToken CameraToken { get; }
    public static MxVolumeRequestScope Global();
    public static MxVolumeRequestScope ForCameraKind(MxCameraRenderKind cameraKind);
    public static MxVolumeRequestScope ForExplicitCamera(MxRenderingCameraToken cameraToken);
}
```

Rules:

- `MxVolumeRequestId` is assigned by Rendering and is stable until release or expiry cleanup.
- `MxVolumeProfileReference` may be a stable resource/catalog key, a direct `VolumeProfile` reference inside the Unity-facing Rendering boundary, or both. Equality and diagnostics must prefer the stable key when present.
- `ExplicitCamera` uses `MxRenderingCameraToken`; it must not expose or depend on `MxFramework.Camera`. A composition root or optional future `MxFramework.Rendering.CameraBridge` may map presentation cameras to tokens.
- `Global` requests apply to all rendering cameras unless isolated by stronger camera-scoped requests. `CameraKind` requests apply only to matching `MxCameraRenderKind`. `ExplicitCamera` requests apply only to the matching token.

Request timing and arbitration:

```csharp
public readonly struct MxVolumeBlendTiming
{
    public float BlendInSeconds { get; }
    public float HoldSeconds { get; }
    public float BlendOutSeconds { get; }
}

public readonly struct MxVolumeRequestDescriptor
{
    public MxVolumeProfileReference Profile { get; }
    public MxVolumeRequestScope Scope { get; }
    public int Priority { get; }
    public MxVolumeBlendTiming Timing { get; }
    public string DebugName { get; }
}

public interface IVolumeBlender
{
    void SetPresentationTime(float presentationTimeSeconds);
    MxVolumeRequestId Request(in MxVolumeRequestDescriptor descriptor);
    MxVolumeRequestId Request(in MxVolumeRequestDescriptor descriptor, float presentationTimeSeconds);
    bool Release(MxVolumeRequestId requestId);
    bool Release(MxVolumeRequestId requestId, float presentationTimeSeconds);
    bool TryGetRequest(MxVolumeRequestId requestId, out MxVolumeRequestSnapshot request);
    MxVolumeBlendStateSnapshot CaptureBlendState(in MxVolumeEvaluationContext context);
    MxVolumeDiagnosticsSnapshot CaptureDiagnostics();
}
```

Lifecycle rules:

- `BlendInSeconds`, `HoldSeconds`, and `BlendOutSeconds` are non-negative. Zero durations are valid and produce immediate phase transitions.
- A request becomes active at creation, ramps from weight `0` to `1` during blend-in, stays at `1` during hold, then ramps from `1` to `0` during blend-out.
- `HoldSeconds <= 0` means no automatic hold expiry; the request remains until `Release(...)` unless a future implementation explicitly adds a separate finite lifetime field.
- `Release(...)` is idempotent. The first release moves an active request into blend-out using its `BlendOutSeconds`; later releases return `false` without restarting the fade.
- Expired requests remain visible in diagnostics long enough to report cleanup reason, then may be pruned by the implementation.
- Time used for VolumeBlender is presentation time owned by Rendering and must not enter runtime authority, Replay hash, Runtime result hash, or SaveState.
- Composition roots must advance VolumeBlender time through `SetPresentationTime(...)`, `Request(..., presentationTimeSeconds)`, `Release(..., presentationTimeSeconds)`, or `CaptureBlendState(...)`. The overloads with explicit presentation time are preferred when request or release timing occurs between render evaluations.

Arbitration rules:

- Scope isolation is evaluated per rendered camera from the union of `Global`, matching `CameraKind`, and matching `ExplicitCamera` requests.
- Global and camera-scoped requests do not mutate each other. A camera-scoped request contributes only to its matching evaluation; it does not lower or release the global request.
- Higher `Priority` wins when multiple active requests target mutually exclusive control of the same final URP profile slot.
- Equal priority uses stable tie-breaker: earlier request creation sequence wins; if creation sequence is unavailable in persisted diagnostics, lower `MxVolumeRequestId.Value` wins.
- Multiple profiles may contribute weights to the diagnostics final state. Current code does not create runtime URP Volume entries; a future runtime application may apply one arbitration winner or multiple weighted URP Volume entries only if it preserves the same public diagnostics and deterministic ordering.
- VolumeBlender must not read Gameplay, Combat, Runtime authority, replay, or SaveState data to choose priorities or weights. Bridges or composition roots translate source events into explicit requests.

Evaluation context and diagnostics:

```csharp
public readonly struct MxVolumeEvaluationContext
{
    public MxCameraRenderKind CameraKind { get; }
    public MxRenderingCameraToken CameraToken { get; }
    public float PresentationTimeSeconds { get; }
}

public enum MxVolumeRequestPhase
{
    BlendIn = 0,
    Hold = 1,
    BlendOut = 2,
    Expired = 3,
    Released = 4
}

public enum MxVolumeRequestCleanupReason
{
    None = 0,
    AutoExpired = 1,
    Released = 2
}

public readonly struct MxVolumeRequestSnapshot
{
    public MxVolumeRequestId RequestId { get; }
    public MxVolumeProfileReference Profile { get; }
    public MxVolumeRequestScope Scope { get; }
    public int Priority { get; }
    public MxVolumeRequestPhase Phase { get; }
    public float Weight { get; }
    public ulong CreationSequence { get; }
    public string DebugName { get; }
    public MxVolumeRequestCleanupReason CleanupReason { get; }
}

public readonly struct MxVolumeBlendStateSnapshot
{
    public MxVolumeEvaluationContext Context { get; }
    public IReadOnlyList<MxVolumeRequestSnapshot> ActiveRequests { get; }
    public IReadOnlyList<MxVolumeRequestSnapshot> SuppressedRequests { get; }
    public IReadOnlyList<MxVolumeAppliedProfileSnapshot> AppliedProfiles { get; }
}

public readonly struct MxVolumeAppliedProfileSnapshot
{
    public MxVolumeProfileReference Profile { get; }
    public float Weight { get; }
    public int Priority { get; }
    public MxVolumeRequestId SourceRequestId { get; }
}

public readonly struct MxVolumeDiagnosticsSnapshot
{
    public IReadOnlyList<MxVolumeRequestSnapshot> ActiveRequests { get; }
    public IReadOnlyList<MxVolumeRequestSnapshot> ExpiredRequests { get; }
    public IReadOnlyList<MxVolumeBlendStateSnapshot> RecentBlendStates { get; }
}
```

Diagnostics must expose active requests, expired requests, profile references, request scopes, priorities, weights, phases, stable tie-break data, suppressed candidates, and the final applied blend state per evaluated camera context.

The first implementation is diagnostics/arbitration-only: it evaluates the final applied profile snapshot but does not create or mutate runtime URP `Volume` objects. Because no runtime `Volume` GameObject, scene asset, prefab, or VolumeProfile asset is touched, PlayMode smoke is not required for Phase 15.7; runtime URP application must add its own PlayMode smoke when implemented.

Future implementation tests must cover:

- Request id creation, lookup, release idempotency, and expired cleanup.
- Diagnostics-only expiry cleanup without a blend-state evaluation.
- Public presentation-time control for request and release timing.
- Priority ordering and stable equal-priority tie-breaker.
- Blend-in, hold, blend-out, zero-duration transitions, and manual release semantics.
- Global request visibility across camera kinds.
- `CameraKind` isolation and `ExplicitCamera` token isolation.
- Diagnostics for active requests, expired requests, suppressed requests, weights, priorities, and final applied blend state.
- Forbidden dependency checks: Rendering must not depend on `MxFramework.Camera`, Gameplay, Combat, Runtime authority, Replay hash, SaveState, independent `ScriptableRendererFeature`, or legacy post-processing.

## 7. Data Publishing And Bridge Contract

```csharp
public interface IRenderDataPublisher
{
    void PublishImpact(MxRenderSubjectId subject, in MxRenderImpactEvent impact);
    void PublishSurfaceContact(MxRenderSubjectId subject, in MxRenderSurfaceContactEvent contact);
    void PublishFieldImpulse(MxRenderSubjectId subject, in MxRenderFieldImpulseEvent impulse);
    void PublishSubjectMovement(MxRenderSubjectId subject, Vector3 velocity);
    void PublishSubjectLifecycle(MxRenderSubjectId subject, MxSubjectLifecycleKind lifecycle);
}

public readonly struct MxRenderImpactEvent
{
    public Vector3 WorldPosition { get; }
    public Color Tint { get; }
    public float Intensity { get; }
    public float Duration { get; }
}

public readonly struct MxRenderSurfaceContactEvent
{
    public Vector3 WorldPosition { get; }
    public float Radius { get; }
    public float Pressure { get; }
}

public readonly struct MxRenderFieldImpulseEvent
{
    public Vector3 WorldPosition { get; }
    public float Radius { get; }
    public float Intensity { get; }
    public int ChannelId { get; }
}

public enum MxSubjectLifecycleKind
{
    Spawned = 0,
    Despawned = 1,
    Disabled = 2,
    Enabled = 3
}
```

The publisher is implemented as generic Rendering semantic input and diagnostics. Actual connection from these generic events to feature-specific MaterialBindingHub, SharedRT, particles, or post-processing behavior is deferred to feature tasks.

Bridge lifecycle convention:

```csharp
public sealed class GameplayRenderingBridge : IDisposable
{
    public void Install() { ... }
    public void Uninstall() { ... }
    public void Dispose() { ... }
}
```

No shared public bridge lifecycle interface is implemented today. Concrete bridge classes, such as `GameplayRenderingBridge`, own this convention directly: constructor-injected dependencies, explicit `Install()`, explicit `Uninstall()`, and `Dispose()` cleanup.

### GameplayBridge 15.6 Subset

`MxFramework.Rendering.GameplayBridge` is an optional composition-root owned assembly. It depends on `MxFramework.Rendering`, `MxFramework.Gameplay`, and `MxFramework.Runtime`; Rendering core does not depend on it, and Gameplay does not depend on Rendering.

Implemented event mapping:

| Gameplay public event | Source id | Rendering lifecycle |
| --- | --- | --- |
| `GameplayRuntimeEventType.ComponentEntityCreated` | `GameplayRuntimeEvent.ComponentEntityId` / `GameplayEntityId` | `MxSubjectLifecycleKind.Spawned` |
| `GameplayRuntimeEventType.ComponentEntityDestroyed` | `GameplayRuntimeEvent.ComponentEntityId` / `GameplayEntityId` | `MxSubjectLifecycleKind.Despawned`, then defer subject mapping release until a later bridge drain |
| `GameplayRuntimeEventType.EntityDespawned` | `GameplayRuntimeEvent.TargetEntityId` / `int` runtime entity id, only when the composition root supplied an existing runtime entity subject map | `MxSubjectLifecycleKind.Despawned`, then defer subject mapping release until a later bridge drain |

The bridge consumes `GameplayRuntimeModule.DrainEvents(...)` and public `GameplayRuntimeEvent` payloads only. It does not read Gameplay private fields and does not create fake `GameplayEntityId` values for legacy `EntityDespawned` events. Unsupported Gameplay events are drained as no-op render events. Despawn subject releases are deferred so the real `RenderDataPublisher` can keep the despawn lifecycle event visible for the publisher frame that received it; pending releases are flushed on a later runtime frame or when the bridge is uninstalled/disposed.

Deferred bridge scopes remain outside this subset: Combat hit/contact translation, Character movement/impact translation, Camera render-value translation, MaterialBindingHub writes, SharedRT writes, VolumeBlender, Volumetric Fog control, demo scenes, shader assets, runtime authority, Replay hash, deterministic simulation, and SaveState integration.

Bridge dependencies are constructor-injected. Do not add a broad composition-root parameter type to bridge constructors.

Concrete bridge docs must not list private source module types. They may subscribe only to already-public runtime or presentation event contracts.

## 8. Volumetric Fog

Volumetric Fog is a Rendering-owned PC URP global height-fog feature slice. It is implemented as a fullscreen Rendering pass/provider and must enter the renderer only through `MxRenderingPipelineFeature`; it must not add an independent framework `ScriptableRendererFeature`.

```csharp
public enum MxVolumetricFogQuality
{
    Disabled = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public readonly struct MxVolumetricFogSettings
{
    public bool Enabled { get; }
    public MxVolumetricFogQuality Quality { get; }
    public Color Color { get; }
    public float Density { get; }
    public float BaseHeight { get; }
    public float HeightFalloff { get; }
    public float MaxDistance { get; }
    public float Anisotropy { get; }
    public float ScatteringIntensity { get; }
    public float NoiseStrength { get; }
    public float NoiseScale { get; }
    public int QualitySampleBudget { get; }
    public int DiagnosticDownsample { get; }
    public float TemporalStabilityIntent { get; }
    public float FocusClearRadius { get; }
    public float FocusClearSoftness { get; }
    public bool PreviewInSceneView { get; }
    public bool HasTemporalStabilityIntent { get; }
    public static MxVolumetricFogSettings Disabled();
    public static MxVolumetricFogSettings Default();
    public static MxVolumetricFogSettings ForQuality(MxVolumetricFogQuality quality);
    public MxVolumetricFogSettings WithQuality(MxVolumetricFogQuality quality);
    public MxVolumetricFogSettings WithEnabled(bool enabled);
    public MxVolumetricFogSettings WithSceneViewPreview(bool previewInSceneView);
}

public sealed class MxVolumetricFogPassProvider : IMxRenderPassProvider, IDisposable
{
    public string DebugName { get; }
    public MxVolumetricFogSettings Settings { get; }
    public void SetSettings(MxVolumetricFogSettings settings);
    public VolumetricFogDiagnosticsSnapshot CaptureDiagnostics();
    public void CollectPasses(IMxRenderPassRegistry registry, in MxCameraRenderContextDescriptor cameraContext);
    public void Dispose();
}
```

Rules:

- v1 targets global height fog for Game cameras. SceneView preview is opt-in through `MxVolumetricFogSettings.PreviewInSceneView`; Preview / Reflection cameras are skipped and reported in diagnostics.
- Quality presets define diagnostic downsample, quality sample budget, temporal stability intent, and view-focus clear settings. These are v1 budget/diagnostic/future-policy signals, not proof of multi-step ray marching or temporal history accumulation.
- v1 is a single-pass global height fog composite. It does not perform downsampled resolve, temporal history accumulation, reprojection, or full volumetric ray marching.
- `AccumulationSize` and `EstimatedMemoryBytes` retain their public signatures and report a hypothetical downsampled ARGBHalf budget, not allocated GPU storage. v1 declares no accumulation reads/writes and does not register a diagnostic-only SharedRT. Both stable ids `mxframework.rendering.volumetric-fog.accumulation` and `mxframework.rendering.volumetric-fog.history` are reserved for a future implementation with actual producers and consumers; neither is active or allocated in v1. Consumers must not resolve these reserved ids as current fog images. SharedRT ownership/lifecycle contracts for real resources are unchanged.
- The composite shader is `Hidden/MxFramework/Rendering/VolumetricFogComposite` and uses `_MxVolumetricFog*` shader properties.
- `MxVolumetricFogPassProvider(settings, Shader compositeShader)` accepts an explicit shader reference for scene/build ownership; the existing settings-only constructor retains Shader.Find compatibility.
- `MxSceneRenderingConfiguration` explicitly binds `Feature`, `TargetCamera` and fog `CompositeShader`, with optional Scene View preview. It exposes fog color, density, base height, height falloff, maximum distance, scattering intensity and focus-clear radius/softness. Fog is independent of GI data; `MxSceneRenderingSession` releases its provider on disable/unload and rebinds when the renderer pipeline is recreated. Color is authored in sRGB and converted to linear at the settings boundary; this scene composition disables noise and temporal intent.
- The camera-scoped scene provider schedules a material-height-fog override before opaque drawing and the existing composite at BeforeRenderingPostProcessing/order 40. `_MxPipelineHeightFogEnabled` is reset by the camera globals pass for each camera. Targeted materials skip their former height fog; distant mountain distance haze remains authored separately, while its valley-height term is suppressed. Non-targeted cameras retain their own material behavior. A missing target camera, disabled density, or disabled component does not suppress material fog.
- The v1 composite reconstructs world position and uses camera-to-surface distance, including orthographic views. It remains a depth-based screen composite, so transparent objects without their own depth are fogged using the underlying scene depth; this is not per-particle volumetric integration.
- This feature does not implement local fog volumes, full multi-light volumetric lighting, runtime URP Volume object application, Gameplay authority, Replay hash, Runtime result hash, or SaveState integration.

```csharp
public readonly struct VolumetricFogDiagnosticsSnapshot
{
    public bool Enabled { get; }
    public MxVolumetricFogQuality Quality { get; }
    public MxCameraRenderKind LastCameraKind { get; }
    public SharedRTSize AccumulationSize { get; }
    public int DiagnosticDownsample { get; }
    public int QualitySampleBudget { get; }
    public float TemporalStabilityIntent { get; }
    public bool TemporalStabilityPlanned { get; }
    public float FocusClearRadius { get; }
    public float FocusClearSoftness { get; }
    public bool PreviewInSceneView { get; }
    public long EstimatedMemoryBytes { get; }
    public int ExecutedFrameCount { get; }
    public int SkippedFrameCount { get; }
    public string LastSkipReason { get; }
    public bool MaterialAvailable { get; }
}

public sealed class VolumetricFogDebugSource : IRenderingDebugSource
{
    public VolumetricFogDebugSource(MxVolumetricFogPassProvider provider, string name = "Rendering");
}
```

## 9. Diagnostics

```csharp
public interface IRenderingDebugSource : IFrameworkDebugSource
{
}

public static class RenderingDebugSectionNames
{
    public const string Globals = "globals";
    public const string CameraGlobals = "cameraGlobals";
    public const string PipelineTopology = "pipelineTopology";
    public const string SharedRTHealth = "sharedRTHealth";
    public const string MaterialBindings = "materialBindings";
    public const string VolumeBlender = "volumeBlender";
    public const string VolumetricFog = "volumetricFog";
    public const string PublisherCounts = "publisherCounts";
}
```

Diagnostics are read-only and depend on `MxFramework.Diagnostics`, not `MxFramework.DebugUI`.

Rendering report bundles are a future reporting convention, not an implemented public exporter API. Until a dedicated exporter lands, manual bundles should collect the diagnostics sections above and use the stable filenames documented by the authoring/design guides.

## 10. Test Surface

Implementation may expose internal test hooks to the test assembly through `InternalsVisibleTo`.

```csharp
internal interface IRenderingTestHooks
{
    void AdvanceTestTime(float deltaTime);
    void ForceSharedRTConflict(SharedRTConflictCode code);
    void AdvanceVolumeBlendTime(float deltaTime);
}
```

Required SharedRT tests:

- `SharedRTRegistry_R_RT_01_AdditiveWritersAllowed`
- `SharedRTRegistry_R_RT_02_WriterConflict`
- `SharedRTRegistry_R_RT_03_StaleReader`
- `SharedRTRegistry_R_RT_04_UnauthorizedWriter`
- `SharedRTRegistry_R_RT_05_OrphanRT`
- `SharedRTRegistry_R_RT_06_ResizeRejected`
- `SharedRTRegistry_R_RT_07_ResizeBurst`
- `SharedRTRegistry_R_RT_08_DroppedAllocation`

Required VolumeBlender tests:

- `VolumeBlender_RequestId_IsStableUntilReleaseOrExpiry`
- `VolumeBlender_RequestLookup_ReturnsCurrentWeight`
- `VolumeBlender_RequestAndRelease_UsePublicPresentationTime`
- `VolumeBlender_RequestExplicitTime_DoesNotInheritStaleEvaluationTime`
- `VolumeBlender_Priority_UsesStableTieBreakerForEqualPriority`
- `VolumeBlender_Lifetime_BlendInHoldBlendOutAndZeroDurations`
- `VolumeBlender_Release_IsIdempotentAndStartsBlendOut`
- `VolumeBlender_Scope_GlobalAppliesToAllCameraKinds`
- `VolumeBlender_Scope_CameraKindAndExplicitCameraAreIsolated`
- `VolumeBlender_Diagnostics_ReportsActiveExpiredSuppressedWeightsAndAppliedState`
- `VolumeBlender_Diagnostics_CleansUpExpiredRequestsWithoutBlendStateCapture`
- `VolumeBlender_Dependencies_DoNotReferenceForbiddenModulesOrLegacyPostProcessing`

Required Volumetric Fog tests:

- `VolumetricFogSettings_QualityPresetsClampRuntimeCost`
- `VolumetricFogProvider_CollectsOnlyGameCameraPasses`
- `VolumetricFogProvider_CollectsSceneViewPassWhenPreviewEnabled`
- `VolumetricFogPass_ConfiguresSharedRtKeysAndDiagnostics`
- `VolumetricFogDebugSource_ExposesDiagnosticsSection`
- `VolumetricFogSlice_ReportsQualityAndSharedRtBudget`

## 11. Compatibility

Stable identity and policy types must not reorder fields once implementation lands:

- `MxRenderSubjectId`
- `SharedRTId`
- `SharedRTOwnerId`
- `SharedRTWriterSetId`
- `MxVolumeRequestId`
- `MxVolumeProfileReference`
- `MxRenderingCameraToken`
- `SharedRenderTextureKey`
- `SharedRTAccessPolicy`
- `SharedRTClearSpec`

Enums may append values but must not reorder existing values:

- `MxRenderSubjectRole`
- `MxRenderPhase`
- `MxCameraRenderKind`
- `SharedRTConflictCode`
- `MxMaterialChannel`
- `MxVolumeRequestScopeKind`
- `MxVolumeRequestPhase`
- `MxVolumeRequestCleanupReason`
- `MxVolumetricFogQuality`

## 12. Acceptance Checklist

- Public signatures use `MxRenderSubjectId`, not source module entity ids.
- Public signatures avoid game-specific terms.
- Any API returning `RTHandle`, `CommandBuffer`, `Renderer`, `Texture`, or `Color` is inside the Unity + URP Rendering assembly boundary.
- Rendering diagnostics use `IFrameworkDebugSource` from Diagnostics, not Debug UI.
- Bridge contracts list only generic lifecycle and naming rules.
- VolumeBlender documents request id, profile reference, scope, priority, lifecycle, release, stable tie-breaker, diagnostics, and URP Volume Framework ownership for the implemented arbitration and diagnostics boundary.
- VolumeBlender does not replace URP Volume Framework, does not introduce independent framework `ScriptableRendererFeature`, and does not require legacy post-processing.
- Volumetric Fog documents global height fog settings, quality presets, active/reserved SharedRT ids, diagnostics, view-focus clear settings, and fullscreen pass/provider ownership without claiming temporal history for v1.
- No grass, water, character, local fog-volume, or project-business API appears in this interface page.

### SceneLighting probe coverage and quality (v2)

`MxSceneRenderingConfiguration.UseProbeCoverageBounds / ProbeCoverageBounds` define an optional world-space sampling volume independently of `StaticRoot / StaticSourceRenderers`. Disabled preserves the geometry-derived lattice. The default base spacing stays 4 m. The lattice retains one-cell padding and uses the same planner in preview and capture. Coverage changes invalidate transport without changing the static-source fingerprint.

`ProbeQuality.Enabled` explicitly selects v2 transport. Existing scene configuration remains compatible with v1; no automatic bake or scene migration occurs. `LocalRefinementVolumes` select half-spacing bricks within the base lattice. A base-cell lookup and 27 shared probe IDs per brick provide deterministic indexing; runtime surface and medium sampling blend to the base lattice at brick boundaries. `MaximumProbeCount` caps the total, including refinement.

v2 stores final captured positions, conservative per-bin nearest distances, relocation/clearance/back-face diagnostics and visibility-constrained dilation rows. Point relocation is bounded relative to local spacing and must recapture transport, sky and distance at the accepted position. Dilation accepts directly valid source probes only, with bidirectional conservative distance checks and an offline segment check against the exact static capture triangles. Solid interior points remain invalid. The GPU reads immutable topology; it performs no scene traversal or CPU raycasts. A separate coefficient pass is allocated/dispatched only when the cache contains dilation links.

The reader supports v1 and v2 and rejects unknown versions, truncated payloads and invalid topology/source indices. Source, spacing, coverage and quality settings participate in cache validity. The preview reports true cached bounds, base boundary validity, refinement/relocation/dilation counts and each filter stage; slice boundaries include a coordinate-scaled floating-point tolerance.

This contract is integrated as a scoped main working-tree change and validated by native compilation, focused tests, actual preview interaction, owned captures and representative renders. Bake duration, payload bytes, provider buffer bytes and Editor camera CPU samples are recorded in Docs/Guides/SCENE_LIGHTING_PROBE_QUALITY.md. GPU timing remains unverified because the timestamp recorder returned no sample blocks. Formal scene assets, the current transport and user defaults are preserved.


Editor-only automatic probe layout: `MxSceneLightingBuilder.ResolveProbeQuality` freezes medium inputs, `PlanProbeLayout` shares `MxSceneLightingProbeDemand` and `MxSceneLightingProbeLayout` with capture. `MxSceneLightingProbeQuality` adds opt-in automatic receiver/navigation/medium demand, activity and exclusion volumes, manual override enablement, score threshold and remaining-budget fraction. Exclusion blocks refinement, not base coverage. `MxSceneLightingData.RequestedProbeSpacing` falls back to legacy actual spacing; `Initialize` adds an optional final requested-spacing argument. Payload v2 and runtime interpolation format are unchanged. See `Docs/Guides/SCENE_LIGHTING_PROBE_QUALITY.md` for priority, deterministic budget degradation, cache identity and Scene handles.

### Fixed directional triangle visibility (v1)

The disabled-camera binding is still type-correct: `MxRenderingPipelineFeature`
owns a transient 1x1 random-write `R32_UInt` RenderTexture dynamic-depth fallback
after checking `Render`/`LoadStore` support, binds it at creation and
before each camera, and clears shadow enable/data-present state. RenderGraph
tracks the fallback read; an active provider overwrites it with the real atlas.
The owning RTHandle releases the RenderTexture on disposal. The receiver uses
integer `Load`; no `Texture2D` creation requiring `Sample` support is used. This
preserves the `Texture2D<uint>` receiver contract on Metal. The 2026-10-11 Metal
Editor check confirmed the fallback format, supported usages and CharacterTest
startup; compilation used persisted evidence/native Console fallback after a
validator transport interruption. Active dynamic-atlas and other-platform
acceptance are unchanged. Evidence and limits are maintained in
`Docs/Guides/DIRECTIONAL_TRIANGLE_SHADOWS.md`.

`MxDirectionalShadowData` format v2 owns two persistent RFloat nearest-depth maps for a fixed high-detail center and full peripheral coverage. `Depth`/`Rectangle` describe the center; `PeripheralDepth`/`PeripheralRectangle` describe the full scene. Both share fixed direction, light axes and a depth slab. `CenterBounds`, `TextureBudgetResolution`, `TransitionWidth`, world bounds and source fingerprint preserve authoring intent. `TextureBytes` counts both distinct textures. The two-map `Initialize` overload supplies these values; the original overload aliases one texture to both regions for explicit single-region callers. `MxDirectionalShadowBaker` explicitly persists geometry/alpha coverage in Edit mode. This direct-light visibility asset is independent of the scene-GI transport/cache.

`MxDirectionalShadowProvider` accepts this asset, a triangle Compute program, owning scene and preview scope. `MxSceneRenderingConfiguration` adds opt-in directional visibility, dynamic enablement, SceneView scope, resolution, world bias/filter and a nonserialized generic caster filter. CullDirectionalShadowsToReceivers and the nonserialized CollectDirectionalShadowReceiverCameras adapter define the conservative union of permitted three-dimensional receiver scopes; missing/invalid scopes retain all casters. StaticDirectionalShadowCastingStateValid invalidates custom non-Renderer casting inputs. `MxDirectionalShadowCoverage.Resolver` maps material/property-block coverage; the optional OverrideResolver overlays live blocks on the current epoch shared-material result, while its absence preserves the original Resolver fallback; project shader identifiers do not belong to the framework. Its defaults cover ordinary base-map cutouts.

Static XY coverage is fitted to authored receiver bounds rather than the union of all caster bounds. The baker evaluates orthographic box-edge orientations and bounded affine projections onto principal receiver planes. Unit projection axes remain perpendicular to the light direction (so points on a light ray retain the same UV), with a positive determinant of at least 0.5. Candidate cost is rectangle area divided by that determinant; skew cannot falsely appear cheaper. The selected axes may be nonorthogonal. A small world border plus three texels and grid alignment protect edge filtering. All source triangles are still rasterized/clipped into this footprint, and the original conservative depth slab is retained for offscreen casters and actor motion.

The split bake overload accepts explicit full and center receiver bounds. The existing bake overload uses a centered half-size XZ region by default. A budget side N allocates center (3N/4)^2 and peripheral (N/2)^2 texels: 81.25% of the prior N^2 storage. At N=4096 this is 3072² + 2048², 52 MiB total. Non-power-of-two center textures have no mipmaps. All casters are rasterized into both maps, regardless of region membership; the common depth slab retains offscreen-caster coverage. The center has a padded transition band (up to one light-space metre). Receivers use one region outside that band and blend the two visibility results within it. Derivatives are computed before region selection; each result unions static and dynamic visibility before filtering. The peripheral lattice never coarsens center-interior filtering. CharacterTest supplies its authoritative fixed 80x60 m battlefield as the center, with the full receiver height.

Runtime sources require `Mesh.isReadable` (importer **Read/Write Enabled**) for immutable topology/UV/skin-weight metadata and a Float32 position stream. This requirement is enforced in both Editor preview and Player; CharacterTest's scene/build validator also rejects unreadable dynamic casters. Current native pose buffers must actually expose `GraphicsBuffer.Target.Raw` before they are imported or bound as `ByteAddressBuffer`; requesting Raw on the renderer is insufficient. Compatible native buffers in Play mode are borrowed and disposed as wrappers. Edit-mode skinned previews always use an owned snapshot and leave native skin-buffer targets unchanged. These previews and incompatible runtime skinned casters reuse a `BakeMesh(true)` snapshot and position list, uploading renderer-local float3 positions to an owned Raw buffer (4-byte elements, 12-byte vertex stride); rigid casters upload their immutable positions once. This compatibility path performs CPU skinning after receiver culling, with no per-build managed allocation after warm-up. It uses the renderer matrix rather than the native skin buffer's root-bone matrix. Cached indices/attributes and dynamic R32UInt depth/status are owned. Per-bone bind-space influence envelopes conservatively bound positive linear-blend skinning, while the shadow still rasterizes actual current-pose triangles. Blend-shape and vertex-cloth displacement require a validated bound contract before admission. Rigid mesh position-stream target ownership is reference counted.

Coverage follows Unity's whole-block precedence: a nonempty per-material property block replaces the per-renderer block; missing fields then use shared-material values. The framework fallback also honors base-map scale/offset. Bake fingerprint v5 includes center/periphery bounds, allocation/blend policy, GUID/local-file-ID and dependency hash for persistent assets, content hashes for procedural meshes, canonical identities for built-in fallback textures, shader/coverage identity for recreated procedural materials, and delimited scalar fields. Other transient inputs must be persisted before baking; the fingerprint never uses a process-local EntityId. Older fingerprint v2/v3/v4 and data-format v1 bakes require an explicit rebake.

The provider counts all admitted coverage before rasterizing any source, with per-complete-build limits of 262144 triangles and 8388608 candidate pixels. Invalid pose, bounds or budget failure disables dynamic visibility with diagnostics, rather than accepting partial silhouettes. Normally one world result is computed per Play frame for all known permitted receiver scopes and rebound for consumers. A new/moved late camera triggers one additional unculled rebuild; ComputedFrames counts builds. Receiver culling now clips each caster's projected bounds against the union of permitted receiver regions before fitting the atlas. Offscreen casters on relevant light rays are retained. Unknown/late scopes conservatively use uncropped bounds. Dynamic coverage is rectangular rather than forced square, aligned to texels, and uses sixteenth-octave size buckets with 20% shrink hysteresis; the whole atlas is still cleared. Per-camera globals disable it for other scopes. Static/dynamic visibility unions each depth ray, and receivers evaluate light-space depth rather than a planar opacity mask. Shadow receivers use a fixed 2x2 bilinear comparison PCF kernel. Within each region, its light-space sample lattice follows the active region/dynamic field with the larger maximum texel footprint, and weights vary continuously with the receiver position. Each ray compares point-loaded depth with receiver-plane correction, unions static/dynamic visibility, and only then contributes to the weighted result. `DirectionalShadowFilterRadius` defaults to 0, which now selects the minimum antialiasing footprint; a positive world radius may widen the support without increasing the four-ray count. This replaces the previous single-ray/four-corner modes. Receiver culling includes filter support and depth-texel rounding. Texture dimensions and triangle/pixel budgets are unchanged; shading adds up to four comparisons per active field in the selected region; the transition band evaluates both regions.

`MxDirectionalSunVisibility(positionWS, receiverPlaneCorrection)` allows curved receivers to compare their query depth without extending triangle planes over the filter footprint. The original one-argument overload retains plane correction. The baked character shader opts out and offsets only its fixed-shadow query along the shading normal by the existing world-space `DirectionalShadowBias`, avoiding speckled self-shadowing and faceted shadow terminators on smooth face/hair geometry. Depth bias, per-ray static/dynamic union and PCF weights remain active. This does not alter caster geometry, other lighting positions or baked textures; the normal offset can soften very close contact shadows.

The owning session binds a valid fixed sun before first-camera shadow setup, honors authored Renderer casting modes without writing them, restores original buffer/offscreen/light settings on release, and keeps Editor preview ownership transient. Fingerprint validation and build processing reject stale authored data. Runtime mutation of immutable static inputs requires explicit `SetDirectionalShadowSourceValidation(false, reason)` and rebaking/rebinding; cached dynamic topology/attributes also require invalidation for in-place changes. The emergency dynamic switch remains available, enabled by default again after 2026-10-03 integration, focused native acceptance and recovery checks; full 1024 dynamic work is restored.

On the current Core 17.6.0 baseline, the owned sampler uses a distinct CPU-only marker and never starts GPU timestamp recording automatically. Only the current Unity 6000.6.3f1 / Core and URP 17.6.0 versions are supported; no 17.5 compatibility branch is retained. `GpuMilliseconds` remains for API compatibility and returns 0 (unmeasured); `CpuMilliseconds` excludes RenderGraph preparation/source refresh and CPU pose baking/upload. Profiling is null-safe in non-development Players. `CpuPoseFallbacks` counts admitted casters using the compatibility pose in the latest build. `CandidateCasters`, `CulledCasters`, `PoseBufferAcquisitions`, `BoneEnvelopeRebuilds`, `ReceiverRegionRebuilds`, `ConservativeCameraRebuilds`, `MaterialCoverageReads`, `CoverageOverrideReads`, `CoverageBindingChanges`, `CandidatePixels`, `ActiveCasters`, `TriangleDispatches`, `ComputedFrames`, `GlobalBindings`, `Issue` and `OwnedGpuBytes` support scoped diagnostics; owned GPU bytes include compatibility pose buffers. Storage/work limits are not timing results. See `Docs/Guides/DIRECTIONAL_TRIANGLE_SHADOWS.md` for platform, precision, lifecycle, acceptance evidence and measured boundaries.

### CharacterTest 表现 LOD 首阶段

项目层 `CharacterTest/Presentation/Lod/` 提供每个正式 ViewBridge 独立的显式注册、带代际句柄、相机快照、按需观察缓存和索引到期队列。Slice 的原有 presentation 入口先捕获相机，HeadPad 的 LateUpdate 阶段再捕获最终相机状态；相同对象/相机版本只计算一次观察，主循环不扫描所有对象×相机。根变换和 anchor 由已有同步入口通知；可降低布料的动态姿态边界由已有 pose 遍历标记失效。缓存键包含对象代际/版本、相机身份及矩阵、pixelRect、layer mask、targetTexture、targetDisplay 的版本。相机销毁/切换移除对应缓存，View 释放或意外销毁注销回调与期限。

正式 HeadPad 的 SyncWorldAnchor 只更新 anchor，不即时投影；原有 View TickPresentation 也不重复投影。现有 HeadPad late driver 仍逐帧维护反馈/淡入淡出，并从共享缓存获取最终屏幕坐标，包括 timeScale=0；战斗字段、锁定可见和死亡反馈规则保留。AI Lab 的独立预览投影与挂载分支保持原有路径，不共享 gameplay 观察。

没有 HeadPad 的主体不计算 UI anchor 投影；本阶段不计算用于几何分档的屏幕占比。后续消费者应扩展真正需要的观察字段，不能用 UI anchor 可见性代替模型边界或阴影 receiver 需求。

当前自动登记原有 gameplay 主相机。其他 gameplay base 相机须由其 owner 显式登记/解除；布料按登记相机中最高需求运行。显式登记要求 camera purpose 为 GameplayBase，拒绝 UI overlay/独立预览 purpose，以及已知 AI Lab、场景光照预览和 FairyGUI Stage 相机；调用方负责准确声明新相机用途，不进行全场景相机发现，也不增加 URP 直接程序集依赖。

观察计数区分请求、真实采样、缓存命中、失效、到期评估、转换和保守恢复；Unity adapter 另计相机快照、投影、边界判断与组件低频刷新。只声明缓存和调用次数的工程边界；尚无原生 Profiler、帧率或 GPU 收益数据。布料暂停合成与阴影保守保护见 [CharacterCloth.md](CharacterCloth.md) 第 9 节。几何档、动画降频、草 Compute 分类、VFX 预算、阴影质量及任何 Gameplay/Combat/Physics 时间轴均未在本阶段接入。

### CharacterTest 几何 LOD 第二阶段

`PresentationGeometryLodPolicy` 共享按投影包围球直径／视口高度的选档和双向 15% 滞回；正交相机使用投影缩放，透视相机使用球体最近深度。近面穿越、非法投影或缺失档位保守恢复原网格。阈值是作者的视觉配置，不是设备成本推断；植被默认 0.08／0.025，可按类型配置。几何消费者保持各自预算，不注册每株草为 actor 到期任务。

植被类型新增显式 `Lod1Mesh`／`Lod2Mesh` 依赖和屏幕高度／滞回参数。必须是三个不同的单 submesh、递减索引数的真实网格，否则保留 Prefab 原网格。新草甸生成器绑定已有十二个网格；旧场景未配置新引用时保持原几何。本阶段不改或重生成原 744 株战场与 851 株样区。固定烘焙阴影仍读取原 LOD0，Runtime 不因此烘焙或改变 GI；风动、UV、alpha、材质及 shader pass 不变。

Renderer 在原 CPU 空间格粗裁剪后按实例投影尺寸选择真实 mesh。每个 Camera 独立保留实例档位；绘制按实际 mesh／material／LOD 跨格汇总，默认 Indirect 容量仍为 8192，回退实际容量上限 256。各档包围盒取三级几何并集后加原有每边 1 米风动余量。GPU 数据与参数保持私有常驻 buffer；稳定实例排列与数量不重复上传，变更／重建／回退切换恢复上传。实例权威、随机种子、生命周期和阴影采集不由选档改写。`LodInstances`、`SubmittedTriangles`、`UploadedInstances` 和选择／转换次数只表示实际提交和工程工作量，不能当帧率或 GPU 耗时。

正式角色 View 注册时缓存已有 `LODGroup`；仅接纳三档不同网格且逐档三角形总数下降的作者输入。低档 Renderer 必须显式关闭 casting，且不重开作者原本 Off 的设置。现有阴影来源要求 Renderer.enabled／active／casting，Play 下优先借 Raw 蒙皮 buffer、否则 BakeMesh；只设置 updateWhenOffscreen 不等于已经验证低 LOD 时 LOD0 原生 pose 更新。因此有蒙皮投影源的组在本阶段固定 LOD0，待独立当前姿态采样证明后才允许降低；刚体和全组不投影的已制作三档输入可以自动选档。选择复用共享屏幕判据和滞回，登记 gameplay 相机取最高几何需求，玩家、锁定与反馈立即提升；未知／没有相机回原档，释放归还 `ForceLOD(-1)`。不变更 Renderer.enabled、GameObject.active、骨骼更新、Animator 或 Combat 时间轴。没有真实作者 LODGroup 的现有模型保持原样；新增消费器不等于全角色低模资产已制作完成。

`SubmittedBatches` 统计单个相机一次 `Submit` 调用中的跨格 mesh／material／LOD 聚合提交包数，不是 Forward、DepthOnly、DepthNormals 等全部渲染 pass 的总 draw 数。

第二阶段工程结果见 `Docs/Tasks/Meadow50kSample.md` 和专项交接回执。植被 Compute 分类的后续合同如下；动画降频、VFX 自动预算与阴影质量降档仍需要独立消费者与专项验证。场景视觉由用户验收，不从离线编译或提交三角形数推断收益。

### CharacterTest 植被 GPU 逐实例分类

`CharacterTestVegetationClassificationMode` 提供 `Auto`、`CpuCoarse`、`CpuInstanceReference`、`GpuInstances`。新数据资产默认 `Auto`，只有存在已验证的真实低档网格且设备、shader 和相机满足 Indirect 条件时才尝试 GPU 路径；没有低档网格的基本草使用 CPU。Compute 资源缺失或初始化失败保留 CPU 路径。`CpuInstanceReference` 使用与 GPU 相同的逐实例风动扩张 AABB、距离和屏幕高度判据，供正确性及性能对照；原有 `Prepare` 仍提供格子粗裁剪基线。

`CharacterTestVegetationGpuClassification` 在布局创建／失效时共享上传实例矩阵和不可变分类记录，每个 Camera 独占 LOD 历史，各嵌套提交独占可见索引、group count 和 indirect arguments。Compute 依次清零 group count，进行逐实例视锥／距离／屏幕高度 LOD 分类，写入可见记录索引，更新原生参数的 instanceCount；支持透视和正交投影，使用与 `PresentationGeometryLodPolicy` 相同的阈值及滞回。静止相机复用输出，移动／投影变化扫描 N 条 GPU 记录。没有 HiZ、世界流式加载、CPU 逐实例分类或提交关键路径上的同步 GPU 回读。

每个真实 mesh／material／tier group 为所有可能选择它的记录预留容量，并按 `DrawBatchSize` 切分命令。预留可能大于当前非空命令数；即使某命令 instanceCount 为零，CPU 仍提交该槽。默认容量 8192，最后一段由 Compute 限制，不能以少提交槽数丢弃溢出实例。此方式的 GPU 分类成本为 O(N)，CPU 提交成本随预留 group／packet 数增长，显存包含共享记录及每相机索引／历史／参数；不能据此承诺 20 万株或硬件收益。

草 vertex shader 在 `WG_FOLIAGE_INDIRECT` 下可通过 `_WgVegetationVisibleIndices` 和 `_WgVegetationVisibleOffset` 获取共享矩阵记录；CPU Indirect 路径显式设置 `_WgVegetationUseVisibleIndices=0`。后续风动、alpha、GI、DepthOnly／DepthNormals 算法不变，原固定几何阴影仍从 LOD0 作者数据独立采集。降档只影响这一路装饰几何，不修改玩法动画、命中／物理、时间轴或确定性事件。

GPU 模式 `VisibleInstances`、`LodInstances`、`SubmittedTriangles` 为未知标记 -1；`CountsAvailable` 表示是否有 CPU 当次结果。`RequestSnapshot` 是显式异步诊断，返回所属分类版本、各档实例、非空 packet 和计算三角形数，不用于决定渲染。`SubmittedBatches` 在 GPU 模式计预留 CPU 提交槽，仍不是 Forward／DepthOnly／DepthNormals 各 pass 的整帧 draw 数。`UploadedInstances` 只计布局记录上传，创建新相机仍会初始化其历史及参数。`ResidentGpuBytes` 计本 owner 当前持有的 buffer 字节，包含显式 CPU 对照留存的 buffer；释放、失效或删除相机时清理对应资源。计时需明确 CPU 整个提交调用、单独分类和 GPU timestamp 的不同范围；GPU timestamp 仅显式打开，Recorder 有三帧延迟。

### 植被独立数据与场景会话（2026-10-05）

CharacterTestVegetationData 是独立 ScriptableObject 实例/type/ID/seed/LOD/draw authority。原生 scene 在 MxSceneRenderingConfiguration.Presentations 中保存 MxScenePresentationBinding（稳定 OwnerId、Data、Anchor、SurfaceRoot、Enabled）。Unity scene dependencies 拥有数据及其 prefab/mesh/material 生命周期，与原有 Data/Look 直接引用相同；不引入另一套 key loader 或全局预载。

MxScenePresentationData.CreateConsumer 与 IMxScenePresentationConsumer 为外层项目扩展点；MxSceneRenderingSession 统一绑定、相机 begin/end、Tick 和 Dispose。GameManager 驱动既有会话；无玩法的独立样区通过 MxSceneRenderingSceneRoot 薄宿主驱动同一会话。每个 session 有独立资源、多相机 history/invocation 输出；相同 binding/OwnerId 不重复创建。不会为单株草添加 GameObject/Mono，不存在植被全局 singleton。

植被仅有独立数据资产权威，不保留旧 Layout、实例对象导入或迁移入口。Level Editor 直接创建/选择资产并写入场景绑定，在同一编辑器管理类型、Prefab/LOD、距离、cast flag。renderer 不自行注册相机回调；Editor 独立预览服务读取同一资产，临时 GPU/renderer 缓存不回写 asset。刷草对资产执行 Undo/Redo，地面/锚点是场景 binding；统一保存同时持久化资产和场景引用。只有数据变化/Undo/owner matrix 改变才失效派生数据。静态草影仍读取全部实例的原 prefab LOD0/reference pose，资产编辑不自动重烘焙。

使用步骤见 Docs/Guides/BASIC_SCENE_VEGETATION.md；资产保护、实际指纹与原生验证记录见 Docs/Tasks/VegetationDataSession.md。GPU 分类/CPU fallback、8192跨格聚合、wind/alpha/depth/normals/GI 合同沿用上节。

### 表现 pose 与 VFX cadence 接入（2026-10-08）

既有项目 `CharacterTestPresentationLod` 复用主体代际、显式相机快照和 bounds 观察，新增独立 pose/VFX cadence policy。屏幕高度使用所有登记 gameplay camera 的最高需求；未知 bounds/相机、重要目标、反馈和失效主体立即提升。减少档位经过滞回和稳定等待，恢复不追播漏掉的帧。参数只是可配置策略，不是成本或收益结论。

`MxDirectionalShadowPosePublication` 记录实际发布的 pose version/frame，以及 deformation version 是否可靠。可选 `MxSceneRenderingConfiguration.ReadDirectionalShadowPosePublication` 由项目注入；`MxSceneRenderingSession.PrepareDirectionalShadowPoseDemand()` 在本帧 pose 决策前捕获同一 receiver scope，`NeedsCurrentDirectionalShadowPose(renderer)` 在未知、未验证或姿态／根变换／bounds 改变时保守要求当前 pose。只对可靠且未改变的完整发布姿态，允许依照既有投影光线 receiver 判据释放需求。多相机共享 receiver union；离屏投影者仍可要求 full pose。此接缝不缓存或跳过 Raw skin buffer 读取和 Compute raster，不把动画 tick 版本当真实几何版本。布料/未知形变保持保守行为。

项目 `CharacterTestVfxPresentationBudgetSource` 已注入正式 `UnityVfxPresenter`，当前只消费可归属的 `vfx.weapon.blade_trail` 自定义 timed backend。可见或重要的刀光全速，稳定隐藏的装饰刀光可减少 anchor/backend cadence；退出、恢复、池复用由 Vfx 契约处理。普通 ParticleSystem、Graph 自动模拟、world effect、关键 telegraph、renderer/GPU/admission 数量预算尚未接入该 policy。此阶段不是全 VFX LOD 完成。相关边界见 `Docs/Guides/PRESENTATION_POSE_VFX_LOD.md`。

### Directional shadow runtime resolution override

`MxSceneRenderingConfiguration.DynamicShadowResolutionOverride` is nonserialized; zero selects authored `DynamicShadowResolution`. `EffectiveDynamicShadowResolution` exposes that choice. The directional provider normalizes the effective value to a power of two within 128..4096. `Settings.Graphics` owns the player preference adapter; Rendering does not depend on Settings.

Changing the effective size invalidates same-frame dynamic reuse and presentation-pose demand. The next eligible base camera rebuilds the dynamic field before binding the resized target; existing late-camera receiver-scope checks remain active. Replacement allocates the texture and RTHandle before releasing the previous target. Failed texture creation or handle allocation destroys the replacement and retains the old owner. Static texture ownership, baked plane coverage, Cast Shadows admission, coupled static/dynamic visibility, filter sampling, triangle budget and the candidate-pixel safety budget are unchanged.


Project shallow water optionally consumes existing body-part physical contact
events through CharacterTestCharacterRuntimeModule.PhysicalContactObserved.
TryResolvePhysicalContactPresentation normalizes the existing physical entity ID
and resolves registration-cached part semantics without generating contacts. Explicit
CharacterBodyPartKind takes precedence; imported Custom/Bone rows reuse declared
abstract left/right-foot socket roles and their parent-part relation in geometry.
Numeric part IDs and bone-name guesses are not semantic sources.
PhysicalContactMotionObserved observes the current world at the existing
presentation contact frame immediately before PhysicalContactObserved. This
avoids comparing an advanced presentation frame with the completed simulation
frame; the existing ripple motion observation is unchanged.
Current publishing still uses the pre-existing PresentationAudio/PresentationOnly
sampling eligibility; no full rendering surface router is claimed.

CharacterTestShallowWaterSession owns a bounded local water-splash ParticleSystem
pool and its framework resource scope. Admission requires a real Foot/Started
floor contact, actual immersion within water depth/rectangle, stable observed
motion, deduplication/cooldown, and receiver distance/visibility and burst budget.
Landing observation changes strength only when a same-frame foot contact arrives.
Game and Scene receivers observe the same views without multiplying emissions.
All water-splash authoring remains in the existing scene-rendering shallow-water
panel. Views, random state, clock and settings do not enter runtime authority,
VfxRuntimeModule, hash, replay or save state. Native visual/lifecycle validation
must be reported separately from staged source/contract checks.
