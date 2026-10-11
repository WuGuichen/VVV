# MxFramework Rendering Pipeline

> Status: Current
>
> Date: 2026-10-03
>
> Scope: Unity project rendering pipeline, URP assets, and documentation rules for framework demos and sample content.

## Current Baseline

The Unity project is configured for Universal Render Pipeline (URP), not the Built-in Render Pipeline.

Evidence:

| Area | Current value |
| --- | --- |
| Unity Editor | `6000.6.3f1` (`45d8eee7de74`) in `ProjectSettings/ProjectVersion.txt` |
| Package | `com.unity.render-pipelines.universal` `17.6.0` in `Packages/manifest.json` |
| Global pipeline asset | `Assets/Config/MxFramework/Rendering/MxFrameworkURPAsset.asset` |
| Universal renderer | `Assets/Config/MxFramework/Rendering/MxFrameworkUniversalRenderer.asset` |
| Graphics Settings | `ProjectSettings/GraphicsSettings.asset` `m_CustomRenderPipeline` points to `MxFrameworkURPAsset` |
| Quality Settings | `ProjectSettings/QualitySettings.asset` `High.customRenderPipeline` points to `MxFrameworkURPAsset` |
| Global settings | `Assets/UniversalRenderPipelineGlobalSettings.asset` |

Only the current Unity `6000.6.3f1` and Core/URP `17.6.0` versions are supported. The framework package declaration matches this baseline; older versions are not compatibility targets.

The default framework renderer currently uses the URP Universal Renderer asset listed above. Any future render feature, renderer asset, or quality-profile split should be documented here before being treated as a stable project baseline.

## PC Rendering Baseline

Current shared baseline (one Unity Quality level; user graphics settings override supported runtime values independently):

| Setting | Current value |
| --- | --- |
| Rendering path | `Forward+` |
| Intermediate Texture | `Always` |
| Copy Depth Mode | `After Opaques` |
| Depth Texture | Enabled |
| Opaque Texture | Enabled |
| Terrain Holes | Disabled |
| Light Cookies | Disabled |
| HDR | Enabled |
| MSAA | `2x` |
| Render Scale | `1.0` |
| Main Light | Per Pixel, shadows enabled, shadowmap `4096` |
| Additional Lights | Per Pixel, per-object limit `4`, shadow capability enabled; current TacticalCardDemoScene has no local lights |
| Shadow Distance | `150` (TacticalCardDemoScene 80×60 m base overview) |
| Shadow Cascades | `4`, split points `0.2 / 0.45 / 0.75` |
| Soft Shadows | High quality; TacticalCardDemoScene Directional Light uses soft shadows |
| Color Grading Mode | HDR |
| Volume Update Mode | Via Scripting |
| SRP Batcher | Enabled |
| Native RenderPass | Enabled |
| Camera-Relative Culling | Shadows enabled, Lights disabled |

TacticalCardDemoScene additionally binds the opt-in precomputed scene light transport provider through the existing `MxRenderingPipelineFeature`. Its formal data, rebuild workflow and functional validation are described in [Scene light transport](Guides/SCENE_LIGHT_TRANSPORT.md). This does not replace URP or alter the legacy provider execution path.

## Historical Unity 6000.5 Migration (2026-06-18)

At that time, the repository was upgraded from Unity `6000.0.61f1` to `6000.5.0f1` on 2026-06-18.
The following package versions record that historical migration, not the current support baseline:

| Package | Historical value |
| --- | --- |
| `com.unity.inputsystem` | `1.19.0` |
| `com.unity.multiplayer.center` | `1.0.1` |
| `com.unity.multiplayer.playmode` | `2.0.2` |
| `com.unity.render-pipelines.universal` | `17.5.0` |
| `com.unity.test-framework` | `1.7.0` |
| `com.unity.transport` | `6.5.0` |

URP asset serialization was also migrated by Unity to the URP 17.5 schema. The framework renderer still uses `MxFrameworkUniversalRenderer.asset`, and the only framework-owned Renderer Feature remains `MxRenderingPipelineFeature`.

## Dependency Boundary

HDR display output is allowed in Player Settings. Startup does not force HDR;
explicit user preferences are owned by Settings.Graphics and restored by the
CharacterTest settings host. Internal HDR rendering and linear color space remain
the existing baseline. The transient output Volume applies SDR ACES or HDR
Neutral/BT.2390 with display calibration, based on actual display state. It does not
modify scene lighting or saved Volume profiles. See [Settings](Interfaces/Settings.md).

URP is a Unity project dependency, not a dependency of the noEngine framework core.

- Pure C# modules such as Core, Config, Events, Attributes, Modifiers, Buffs, Gameplay, Combat, Runtime, Resources, and Runtime AI Planner must not reference URP assemblies.
- Unity adapter, view, demo, editor, and asset-authoring layers may reference URP when the dependency is contained in their Unity-facing assemblies.
- Runtime authority, replay hash, and SaveState must not depend on render pipeline state.
- Rendering assets may be referenced by Unity scenes, prefabs, materials, volumes, and cataloged sample content, but gameplay/config data should keep using stable resource keys or view adapter settings instead of hard-coded Unity object references.

## Authoring Rules

- New framework scenes and playable demos must be authored and verified under URP.
- Consumer projects may use any reviewed URP-compatible shader, but this repository's first-party WGame content must not use stock `Universal Render Pipeline/Lit` or `Universal Render Pipeline/Simple Lit`. Choose the semantic WGame family (`CharacterStylizedLit`, `PropStylizedLit`, `EnvironmentStylizedLit`, `FoliageMossLit`, `WorldMarkerUnlit`, `MagicVfxUnlit`, or `ImpactDecal`) and fail authoring when the required shader is unavailable.
- Stock `Universal Render Pipeline/Unlit` and `Universal Render Pipeline/Particles/Unlit` remain allowed for first-party UI, line, and particle paths that do not require a WGame semantic family.
- CharacterTest tactical markers and its owned VFX runtime/builders use `WGame/WorldMarkerUnlit` mesh mode and `WGame/MagicVfxUnlit` texture/vertex-color mode through `CharacterTestUnlitMaterials`. These paths no longer select stock Unlit or Sprite fallback shaders. Both project shaders are explicitly included for runtime-created materials; generic framework previews and original third-party shaders retain their separate ownership.
- The only allowed framework-owned Renderer Feature on `MxFrameworkUniversalRenderer.asset` is `MxRenderingPipelineFeature`. Feature-specific work must be implemented as `IMxRenderPass` or `IMxRenderPassProvider` inside `MxFramework.Rendering`; do not add independent framework `ScriptableRendererFeature` assets for grass, water, decals, outlines, dissolve, volumetric fog, or similar capabilities.
- Do not add new Built-in Render Pipeline-only materials, post-processing profiles, image effects, or camera scripts as framework sample baselines.
- Post-processing should use URP Volume components and profiles. Legacy Post Processing Stack v2 should not be introduced for new framework demos.
- Code-side profile blend intent must go through the reviewed VolumeBlender request API. The current implementation covers request arbitration and diagnostics only; runtime URP `Volume` object application remains future work and must use URP Volume Framework when implemented. VolumeBlender must not replace URP Volume Framework, add an independent framework `ScriptableRendererFeature`, or depend on `MxFramework.Camera`.
- Global height volumetric fog uses `MxVolumetricFogPassProvider` through the same FeaturePipeline. v1 is a single-pass fullscreen global height fog composite with accumulation diagnostics only; sky pixels use horizon-weighted height density so visible atmosphere does not depend on Unity built-in `RenderSettings.fog`. SceneView rendering is opt-in for editor art review through `MxVolumetricFogSettings.PreviewInSceneView`; the default target remains Game cameras. It does not claim temporal history, full ray marching, Gameplay authority, Runtime hash, Replay, SaveState, or URP Volume object application.
- Runtime graphics preferences may apply volumetric fog quality through `MxVolumetricFogSettingsApplier`, which only calls the existing provider `SetSettings(...)`. It does not create rendering assets, mutate URP YAML, or make volumetric fog part of gameplay authority / Replay / SaveState.
- Rendering authoring rules for shader globals, SharedRT, pass/provider registration, material binding, data publishing, VolumeBlender requests, demo validation, and diagnostics live in `Docs/RENDERING_AUTHORING_GUIDE.md`.
- If a demo intentionally uses a fallback material or editor-only debug shader, document the reason in the demo/task doc and keep it out of noEngine runtime modules.
- New serialized rendering assets should be created through Unity Editor, Unity MCP, or an existing editor menu. Do not hand-write Unity YAML for pipeline assets, renderer assets, materials, or volume profiles.

## Validation Checklist

For changes that touch scenes, prefabs, materials, shaders, render settings, cameras, or playable demos:

1. Confirm `GraphicsSettings.asset` and active Quality level still point to `MxFrameworkURPAsset`.
2. Open the affected scene and confirm the Console has no render pipeline errors.
3. Check visible demo objects for missing or pink materials.
4. Confirm cameras render through URP and do not depend on Built-in-only replacement shaders or legacy image effects.
5. If adding sample resources, keep resource catalog keys stable and avoid encoding render pipeline details into gameplay-facing IDs.
6. Run the first-party stock URP Lit audit and confirm materials, serialized scenes/prefabs, generated FBX material subassets, generators, runtime code, and tests report zero `Lit` / `Simple Lit` usage.

## Documentation Sync

When the render pipeline baseline changes, update:

- `Docs/RENDERING_PIPELINE.md`
- `Docs/RENDERING_FRAMEWORK_DESIGN.md`
- `Docs/RENDERING_AUTHORING_GUIDE.md`
- `Docs/Interfaces/Rendering.md`
- `Docs/README.md`
- `Docs/DESIGN.md` if package or dependency policy changes
- `Docs/RESOURCE_DIRECTORY_LAYOUT.md` if rendering asset directories change
- `Docs/AGENT_GAME_CREATION_GUIDE.md` and `Docs/QUALITY_GATE.md` if demo validation rules change
