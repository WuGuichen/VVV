# Rendering 接口

> Version 0.2 | 2026-05-25
>
> Status: Current through Phase 15.8 infrastructure and demo showcase. VolumeBlender includes request arbitration and diagnostics; runtime URP Volume object application remains a follow-up integration step.
>
> Scope: Public API shape for `MxFramework.Rendering` through Phase 15.8 demo showcase. Feature-specific shader APIs are out of scope.

## 职责

Rendering 提供 Unity + URP-facing 的渲染编排接口：全局帧上下文、相机作用域上下文、URP 总入口 Feature、Pass/Provider 注册、SharedRT 注册与冲突诊断、MaterialBindingHub、RenderDataPublisher、VolumeBlender request arbitration / diagnostics 和 bridge 公共边界。

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

`MxFramework.Rendering` does not depend on any bridge assembly and does not depend on `MxFramework.DebugUI`.

## 1. Subject Identity

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

Current SharedRT frame lifecycle is scoped to one URP camera render invocation: `MxRenderingPipelineFeature` starts the SharedRT frame synchronously in `AddRenderPasses` before calling any `IMxRenderPass.Configure`, then enqueues camera globals, feature passes, and a SharedRT `EndFrame` pass after feature passes. This keeps Configure-side SharedRT reads/writes on fresh frame state, and keeps SceneView, Preview, reflection, and Game cameras isolated until a later Unity-runtime task has stronger evidence for a different multi-camera frame policy. The internal camera globals, SharedRT lifecycle, and feature wrapper passes implement both compatibility `Execute` and URP 17 `RecordRenderGraph` paths; their RenderGraph path records unsafe passes with culling disabled so camera globals, feature command buffers, and EndFrame execute when RenderGraph is active.

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

## 5. Material Binding

Material binding is implemented as the framework-owned path for material property writes. Future features must not call `Renderer.SetPropertyBlock` directly.

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

Bridge lifecycle:

```csharp
public interface IRenderingBridge : IDisposable
{
    void Install();
    void Uninstall();
}
```

### GameplayBridge 15.6 Subset

`MxFramework.Rendering.GameplayBridge` is an optional composition-root owned assembly. It depends on `MxFramework.Rendering`, `MxFramework.Gameplay`, and `MxFramework.Runtime`; Rendering core does not depend on it, and Gameplay does not depend on Rendering.

Implemented event mapping:

| Gameplay public event | Source id | Rendering lifecycle |
| --- | --- | --- |
| `GameplayRuntimeEventType.ComponentEntityCreated` | `GameplayRuntimeEvent.ComponentEntityId` / `GameplayEntityId` | `MxSubjectLifecycleKind.Spawned` |
| `GameplayRuntimeEventType.ComponentEntityDestroyed` | `GameplayRuntimeEvent.ComponentEntityId` / `GameplayEntityId` | `MxSubjectLifecycleKind.Despawned`, then defer subject mapping release until a later bridge drain |
| `GameplayRuntimeEventType.EntityDespawned` | `GameplayRuntimeEvent.TargetEntityId` / `int` runtime entity id, only when the composition root supplied an existing runtime entity subject map | `MxSubjectLifecycleKind.Despawned`, then defer subject mapping release until a later bridge drain |

The bridge consumes `GameplayRuntimeModule.DrainEvents(...)` and public `GameplayRuntimeEvent` payloads only. It does not read Gameplay private fields and does not create fake `GameplayEntityId` values for legacy `EntityDespawned` events. Unsupported Gameplay events are drained as no-op render events. Despawn subject releases are deferred so the real `RenderDataPublisher` can keep the despawn lifecycle event visible for the publisher frame that received it; pending releases are flushed on a later runtime frame or when the bridge is uninstalled/disposed.

Deferred bridge scopes remain outside this subset: Combat hit/contact translation, Character movement/impact translation, Camera render-value translation, MaterialBindingHub writes, SharedRT writes, VolumeBlender, demo scenes, shader assets, runtime authority, Replay hash, deterministic simulation, and SaveState integration.

Bridge dependencies are constructor-injected. Do not add a broad composition-root parameter type to this interface.

Concrete bridge docs must not list private source module types. They may subscribe only to already-public runtime or presentation event contracts.

## 8. Diagnostics

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
    public const string PublisherCounts = "publisherCounts";
}

public static class RenderingReportExporter
{
    public static RenderingReportExportResult Export(string targetDirectory);
}
```

Diagnostics are read-only and depend on `MxFramework.Diagnostics`, not `MxFramework.DebugUI`.

## 9. Test Surface

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

## 10. Compatibility

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

## 11. Acceptance Checklist

- Public signatures use `MxRenderSubjectId`, not source module entity ids.
- Public signatures avoid game-specific terms.
- Any API returning `RTHandle`, `CommandBuffer`, `Renderer`, `Texture`, or `Color` is inside the Unity + URP Rendering assembly boundary.
- Rendering diagnostics use `IFrameworkDebugSource` from Diagnostics, not Debug UI.
- Bridge contracts list only generic lifecycle and naming rules.
- VolumeBlender documents request id, profile reference, scope, priority, lifecycle, release, stable tie-breaker, diagnostics, and URP Volume Framework ownership before implementation.
- VolumeBlender does not replace URP Volume Framework, does not introduce independent framework `ScriptableRendererFeature`, and does not require legacy post-processing.
- No grass, water, character, or other feature-specific API appears in this interface page.
