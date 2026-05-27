# MxFramework Rendering Pipeline

> Status: Current
>
> Date: 2026-05-25
>
> Scope: Unity project rendering pipeline, URP assets, and documentation rules for framework demos and sample content.

## Current Baseline

The Unity project is configured for Universal Render Pipeline (URP), not the Built-in Render Pipeline.

Evidence:

| Area | Current value |
| --- | --- |
| Package | `com.unity.render-pipelines.universal` `17.0.4` in `Packages/manifest.json` |
| Global pipeline asset | `Assets/Config/MxFramework/Rendering/MxFrameworkURPAsset.asset` |
| Universal renderer | `Assets/Config/MxFramework/Rendering/MxFrameworkUniversalRenderer.asset` |
| Graphics Settings | `ProjectSettings/GraphicsSettings.asset` `m_CustomRenderPipeline` points to `MxFrameworkURPAsset` |
| Quality Settings | `ProjectSettings/QualitySettings.asset` `High.customRenderPipeline` points to `MxFrameworkURPAsset` |
| Global settings | `Assets/UniversalRenderPipelineGlobalSettings.asset` |

The default framework renderer currently uses the URP Universal Renderer asset listed above. Any future render feature, renderer asset, or quality-profile split should be documented here before being treated as a stable project baseline.

## PC Rendering Baseline

Current PC-oriented defaults:

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
| Additional Lights | Per Pixel, per-object limit `4`, shadows disabled |
| Shadow Distance | `80` |
| Shadow Cascades | `3`, split points `0.125`, `0.35` |
| Soft Shadows | Enabled, Medium quality |
| Color Grading Mode | HDR |
| Volume Update Mode | Via Scripting |
| SRP Batcher | Enabled |
| Native RenderPass | Enabled |

## Dependency Boundary

URP is a Unity project dependency, not a dependency of the noEngine framework core.

- Pure C# modules such as Core, Config, Events, Attributes, Modifiers, Buffs, Gameplay, Combat, Runtime, Resources, and Runtime AI Planner must not reference URP assemblies.
- Unity adapter, view, demo, editor, and asset-authoring layers may reference URP when the dependency is contained in their Unity-facing assemblies.
- Runtime authority, replay hash, and SaveState must not depend on render pipeline state.
- Rendering assets may be referenced by Unity scenes, prefabs, materials, volumes, and cataloged sample content, but gameplay/config data should keep using stable resource keys or view adapter settings instead of hard-coded Unity object references.

## Authoring Rules

- New framework scenes and playable demos must be authored and verified under URP.
- New materials should use URP-compatible shaders, for example `Universal Render Pipeline/Lit`, `Universal Render Pipeline/Unlit`, or a project-approved URP Shader Graph.
- The only allowed framework-owned Renderer Feature on `MxFrameworkUniversalRenderer.asset` is `MxRenderingPipelineFeature`. Feature-specific work must be implemented as `IMxRenderPass` or `IMxRenderPassProvider` inside `MxFramework.Rendering`; do not add independent framework `ScriptableRendererFeature` assets for grass, water, decals, outlines, dissolve, or similar capabilities.
- Do not add new Built-in Render Pipeline-only materials, post-processing profiles, image effects, or camera scripts as framework sample baselines.
- Post-processing should use URP Volume components and profiles. Legacy Post Processing Stack v2 should not be introduced for new framework demos.
- Code-side profile blend intent must go through the reviewed VolumeBlender request API. The current implementation covers request arbitration and diagnostics only; runtime URP `Volume` object application remains future work and must use URP Volume Framework when implemented. VolumeBlender must not replace URP Volume Framework, add an independent framework `ScriptableRendererFeature`, or depend on `MxFramework.Camera`.
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
