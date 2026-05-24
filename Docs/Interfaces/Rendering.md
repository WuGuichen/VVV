# Rendering 接口

> Version 0.1 | 2026-05-24
>
> Status: Spec Ready for Phase 15.0 documentation baseline. Implementation starts in Phase 15.1.
>
> Scope: Public API shape for `MxFramework.Rendering` through Phase 15.3. Feature-specific shader APIs are out of scope.

## 职责

Rendering 提供 Unity + URP-facing 的渲染编排接口：全局帧上下文、相机作用域上下文、URP 总入口 Feature、Pass/Provider 注册、SharedRT 注册与冲突诊断、后续 MaterialBindingHub 和 RenderDataPublisher 的公共边界。

Rendering 是表现层能力，不进入 Gameplay / Combat authority、Runtime result hash、Replay hash 或 SaveState。

## 程序集

| 程序集 | 依赖 | 说明 |
| --- | --- | --- |
| `MxFramework.Rendering` | `MxFramework.Core`, `MxFramework.Diagnostics`, UnityEngine, URP | Runtime-facing rendering orchestration |
| `MxFramework.Rendering.Editor` | `MxFramework.Rendering`, UnityEditor | Authoring, validation, inspectors, asset menus |
| `MxFramework.Rendering.GameplayBridge` | Planned | Optional bridge from Gameplay public events to Rendering |
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

Same `Phase + Order` is allowed only when pass dependencies do not read/write the same SharedRT and debug names provide deterministic ordering. Conflicts are reported through Diagnostics.

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

Material binding is planned after Phase 15.3. The public shape is fixed early so future features do not call `Renderer.SetPropertyBlock` directly.

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

## 6. Data Publishing And Bridge Contract

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

Phase 15.0 may implement this as no-op in later code. Actual connection to MaterialBindingHub, SharedRT, particles, or post-processing is deferred.

Bridge lifecycle:

```csharp
public interface IRenderingBridge : IDisposable
{
    void Install();
    void Uninstall();
}
```

Bridge dependencies are constructor-injected. Do not add a broad composition-root parameter type to this interface.

Concrete bridge docs must not list private source module types. They may subscribe only to already-public runtime or presentation event contracts.

## 7. Diagnostics

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
    public const string PublisherCounts = "publisherCounts";
}

public static class RenderingReportExporter
{
    public static RenderingReportExportResult Export(string targetDirectory);
}
```

Diagnostics are read-only and depend on `MxFramework.Diagnostics`, not `MxFramework.DebugUI`.

## 8. Test Surface

Implementation may expose internal test hooks to the test assembly through `InternalsVisibleTo`.

```csharp
internal interface IRenderingTestHooks
{
    void AdvanceTestTime(float deltaTime);
    void ForceSharedRTConflict(SharedRTConflictCode code);
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

## 9. Compatibility

Stable identity and policy types must not reorder fields once implementation lands:

- `MxRenderSubjectId`
- `SharedRTId`
- `SharedRTOwnerId`
- `SharedRTWriterSetId`
- `SharedRenderTextureKey`
- `SharedRTAccessPolicy`
- `SharedRTClearSpec`

Enums may append values but must not reorder existing values:

- `MxRenderSubjectRole`
- `MxRenderPhase`
- `MxCameraRenderKind`
- `SharedRTConflictCode`
- `MxMaterialChannel`

## 10. Acceptance Checklist

- Public signatures use `MxRenderSubjectId`, not source module entity ids.
- Public signatures avoid game-specific terms.
- Any API returning `RTHandle`, `CommandBuffer`, `Renderer`, `Texture`, or `Color` is inside the Unity + URP Rendering assembly boundary.
- Rendering diagnostics use `IFrameworkDebugSource` from Diagnostics, not Debug UI.
- Bridge contracts list only generic lifecycle and naming rules.
- No grass, water, character, or other feature-specific API appears in this interface page.
