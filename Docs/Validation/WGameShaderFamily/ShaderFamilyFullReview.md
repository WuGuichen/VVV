# WGame Shader Family Full Review

> Status: Current
>
> Issue: #596 `[Closeout] WGame Shader family full review and docs sync`
>
> Captures:
> - `Docs/Validation/WGameShaderFamily/ShaderFamilyFullReview_NormalBloom.png`
> - `Docs/Validation/WGameShaderFamily/ShaderFamilyFullReview_ReducedBloom.png`
> - `Docs/Validation/WGameShaderFamily/ShaderFamilyFullReview_UiOff.png`
>
> Boundary: This is the WGame shader family v1 closeout record. It does not define MxFramework public API and does not create production art. The original #596 closeout did not modify persistent Unity assets; the 2026-06-07 follow-up below adds review-only persistent scene/material samples for inspection.

## Scope

#596 closes out the first WGame shader family pass by reviewing all landed family shaders, material samples, validation records, and the #595 budget/sample-library profile.

Reviewed families:

| Family | Shader |
| --- | --- |
| Character | `Assets/Art/WGame/Shaders/Character/CharacterStylizedLit.shader` |
| Environment | `Assets/Art/WGame/Shaders/Environment/EnvironmentStylizedLit.shader` |
| Magic VFX | `Assets/Art/WGame/Shaders/VFX/MagicVfxUnlit.shader` |
| World Marker | `Assets/Art/WGame/Shaders/Marker/WorldMarkerUnlit.shader` |
| Prop | `Assets/Art/WGame/Shaders/Props/PropStylizedLit.shader` |
| Foliage | `Assets/Art/WGame/Shaders/Environment/FoliageMossLit.shader` |
| Impact Decal | `Assets/Art/WGame/Shaders/Decal/ImpactDecal.shader` |

This closeout does not add a new shader family, runtime view adapter, `MxMaterialChannel`, `MaterialBindingHub` API, render pass, resource catalog entry, gameplay authority path, Replay input, SaveState input, or Runtime hash input.

The 2026-06-07 follow-up keeps that boundary intact while adding persistent review samples to `Assets/Scenes/WGame/NightRuinShaderReview.unity` and `Assets/Art/WGame/Materials/Character/CharacterStylizedLitReview.mat`.

## Shared HLSL And Property Boundary Audit

All WGame shader families keep WGame material/presentation properties under `_Wg*`. Framework globals are read through the documented `_Mx*` boundary exposed by Rendering and the shared `WGameFogBlend.hlsl` include.

| Family | Shared HLSL reuse | `_Mx*` read boundary | Notes |
| --- | --- | --- | --- |
| Character | Common, stylized lighting, fog, emission, rim, dissolve. | Reads documented weather/time/focus globals through shared helpers. | Runtime-style feedback remains `_WgHitFlash*`, `_WgStatusTint*`, `_WgDissolve*`, `_WgOutline*`. |
| Environment | Common, stylized lighting, fog. | Reads documented fog/weather/focus globals through shared helpers. | Opaque Geometry queue; no runtime authority coupling. |
| Magic VFX | Common, fog, emission, dissolve. | Reads `_MxTime` and focus/fog globals through shared helpers. | Transparent; shape and emission are material state only. |
| World Marker | Common, fog, emission, dissolve. | Reads `_MxTime` and `_MxViewFocusWorldPos` through shared helpers. | Marker state remains spatial presentation; no HUD text/state ownership. |
| Prop | Common, stylized lighting, fog, emission, rim, dissolve. | Reads documented time/weather/focus globals through shared helpers. | Existing `HitFlash`, `StatusTint`, `DissolveProgress`, and `OutlineState` semantics remain suitable. |
| Foliage | Common, stylized lighting, fog. | Reads documented `_MxWind*`, weather, time, local subject, and focus globals through shared helpers. | Wind is vertex-stage presentation only. |
| Impact Decal | Common, stylized lighting, fog, emission, dissolve. | Reads documented time/focus/local subject globals through shared helpers. | Decal lifetime/fade/dissolve are material presentation state only. |

No shader source introduces new `_Mx*` ownership. No MxFramework public API changed during this closeout.

## Material Sample Inventory

The material inventory matches `Docs/Validation/WGameShaderFamily/ShaderTextureMaterialBudget.md`.

| Family | Material samples | Closeout result |
| --- | --- | --- |
| Character | `Assets/Art/WGame/Materials/Character/CharacterStylizedLitReview.mat` | Persistent review sample exists and is assigned to the Character body/head samples in `Assets/Scenes/WGame/NightRuinShaderReview.unity`. |
| Environment | `Assets/Art/WGame/Materials/Environment/RuinDarkStone.mat`; `Assets/Art/WGame/Materials/Environment/RuinMossStone.mat`; `Assets/Art/WGame/Materials/Environment/RuinBronzeTrim.mat` | Matches #595 sample library. |
| Magic VFX | `Assets/Art/WGame/Materials/VFX/MagicAmberFocus.mat`; `Assets/Art/WGame/Materials/VFX/MagicDangerEmber.mat`; `Assets/Art/WGame/Materials/VFX/MagicHealingGreen.mat` | Matches #595 sample library. |
| World Marker | `Assets/Art/WGame/Materials/Marker/WorldMarkerTargetDiamond.mat`; `Assets/Art/WGame/Materials/Marker/WorldMarkerInteractCorners.mat`; `Assets/Art/WGame/Materials/Marker/WorldMarkerQuestBeacon.mat` | Matches #595 sample library. |
| Prop | `Assets/Art/WGame/Materials/Props/PropChestAmberFocus.mat`; `Assets/Art/WGame/Materials/Props/PropBronzeMechanism.mat`; `Assets/Art/WGame/Materials/Props/PropLockedDisabled.mat`; `Assets/Art/WGame/Materials/Props/PropDamagedDissolve.mat` | Matches #595 sample library. |
| Foliage | `Assets/Art/WGame/Materials/Environment/FoliageMossPatch.mat`; `Assets/Art/WGame/Materials/Environment/FoliageDryGrass.mat` | Matches #595 sample library. |
| Impact Decal | `Assets/Art/WGame/Materials/Decal/ImpactDecalHitMark.mat`; `Assets/Art/WGame/Materials/Decal/ImpactDecalCrackBreak.mat` | Matches #595 sample library. |

## Capture Method

Unity MCP was probed first and was unavailable at `http://127.0.0.1:8080/mcp`, so no MCP Console, Game view, Test Runner, or Frame Debugger result is used as branch validation.

The final captures were generated in the issue-596 worktree with Unity GUI:

```text
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -projectPath /Users/vincent/Documents/WGameFramework.worktrees/issue-596 \
  -executeMethod Issue596ShaderFamilyFullReviewGenerator.Generate \
  -quit \
  -logFile /tmp/issue596-shader-family-full-review-rerun.log
```

No `-batchmode` flag was used. The temporary Editor script created an unsaved preview scene, loaded existing material samples as runtime material clones, created a runtime character review material, rendered the three approved captures, then was deleted before commit. The script did not save `Assets/Scenes/WGame/NightRuinShaderReview.unity`, materials, prefabs, textures, or `.meta` assets.

The Unity log contained no `Shader error`, no `Shader warning`, no C# compile errors, and no branch-specific shader warnings in the rerun log. The first attempt produced a black normal capture because URP post-processing camera render in the temporary script emitted `RenderPassContext` warnings; that attempt was discarded. The rerun uses material bloom/emission budgets without enabling a persistent post-process asset and produced all three openable captures.

## 2026-06-07 Persistent Scene Update

`Assets/Scenes/WGame/NightRuinShaderReview.unity` now contains persistent review objects for Character, Environment, Magic VFX, World Marker, Prop, Foliage, Impact Decal, and a static MxRuinNight HeadPad/HUD style reference.

- Character now uses `Assets/Art/WGame/Materials/Character/CharacterStylizedLitReview.mat` instead of a scene-local-only material gap.
- Prop, Foliage, and Impact Decal samples are placed in the persistent review scene using the existing #595 sample materials.
- Impact Decal samples are small review billboards in the scene because their depth fade and stone blending are easier to inspect when they are not fully coplanar with the floor.
- The scene remains review geometry, not production WGame meshes, textures, level layout, or runtime gameplay.

## Final Visual Review

### Normal Bloom

`ShaderFamilyFullReview_NormalBloom.png` shows the full v1 family set with UI/HeadPad style reference visible:

- Character remains the central readable silhouette.
- Environment dark stone, moss stone, and bronze samples stay lower priority.
- Magic VFX, marker, and decal samples read through compact angular shapes and edge value.
- Prop focus and disabled/damaged states remain distinct without taking marker priority.
- Foliage stays low-saturation and subordinate to marker/character readability.

### Reduced Bloom

`ShaderFamilyFullReview_ReducedBloom.png` lowers runtime material bloom/emission budgets and marker pulse values:

- VFX and marker meaning still comes from diamond/corner/beacon/crack shapes, not glow alone.
- Impact decals remain readable through crack/burst silhouettes and stone blending.
- Character and HeadPad-style plate remain readable over the dark background.

### UI-Off

`ShaderFamilyFullReview_UiOff.png` hides the static HeadPad/HUD reference objects:

- Character, marker, VFX, prop, foliage, and decal language remain spatially readable without UI overlays.
- UI-off does not imply production HUD removal; it validates that the world shader language is not dependent on HeadPad/HUD text.

## Transparent VFX / Marker / Decal Review

| Family | Queue / depth | Overdraw and sorting audit | Bloom / lifetime audit |
| --- | --- | --- | --- |
| Magic VFX | Transparent, depth fade, `ZWrite Off`. | Review quads stay compact; no broad rings or screen-wide overlays. Sorting risk remains for dense overlapping VFX stacks. | Reduced capture preserves amber/danger/healing meaning through shape and edge color. Lifetime remains future VFX/view-adapter state. |
| World Marker | Transparent, depth fade, `ZWrite Off`. | Target, quest, and interact shapes are compact diamond/beacon/corner markers; no large transparent circles. Sorting risk remains near other transparent VFX. | `_WgBloomBudget` and pulse controls reduce bloom dependence. Marker pulse remains future WGame view-adapter state. |
| Impact Decal | Transparent, depth fade, `ZWrite Off`. | Small crack/burst quads; no screen-covering decal overlays. Sorting risk remains if many decals overlap on the same surface. | `_WgLifetimeFade` and `_WgDissolveProgress` are material presentation state only. Reduced capture keeps decals readable through shape/value. |

Frame Debugger was not opened because issue-worktree Unity MCP was unavailable. Source/material/capture audit supports v1 closeout, with residual risk for live overdraw/sorting review in a future production placement scene.

## HeadPad / HUD Boundary

World shader families own spatial and material readability. HeadPad/HUD own persistent textual and stateful UI.

| Area | Closeout boundary |
| --- | --- |
| WorldMarker | Spatial target lock, task beacon, and interaction position only. No names, health, quest text, cooldowns, or persistent HUD state. |
| Magic VFX | Short-lived action readability only. It must not carry persistent status text or objective state. |
| Impact Decal | Short combat feedback and surface mark only. It must not encode long-term destruction, collision, terrain truth, or combat authority. |
| Character / Prop material | Silhouette, focus, hit flash, status tint, dissolve, and outline presentation. The gameplay truth remains outside Rendering. |
| HeadPad/HUD | Actor names, text, health/resource/objective information, and persistent UI state when runtime UI exists. |

The UI-off capture confirms that world shader samples remain readable when static UI reference objects are hidden. The normal/reduced captures confirm that the static HeadPad-style space remains visually separate from markers and VFX.

## Depth / Shadow Pass Policy

The 2026-06-07 pass follow-up adds shadow/depth passes only where they are needed for the gameplay default shadow budget:

| Family | Depth / shadow result |
| --- | --- |
| Character | `UniversalForward`, `ShadowCaster`, and `DepthOnly`; dissolve clip is shared with depth and shadow passes. |
| Ruin Environment | `UniversalForward`, `ShadowCaster`, and `DepthOnly`; opaque ruin geometry writes normal depth/shadow. |
| Prop | `UniversalForward`, `ShadowCaster`, and `DepthOnly`; dissolve clip is shared with depth and shadow passes. |
| Foliage | `UniversalForward` and `DepthOnly`; foliage receives main-light shadows but does not cast realtime shadows in the gameplay default. |
| Magic VFX / World Marker / Impact Decal | `UniversalForward` only; transparent presentation does not cast shadows or write depth-only passes. |

The shared pass code lives in `Assets/Art/WGame/Shaders/ShaderLibrary/WGameDepthShadowPass.hlsl` and stays inside the WGame project shader layer. It does not add a renderer feature or MxFramework public API.

## Main Light / Shadow Receive Trial

The 2026-06-07 main-light follow-up adds a conservative URP main-light receive path for the same opaque/cutout lit families:

- `Assets/Art/WGame/Shaders/ShaderLibrary/WGameMainLight.hlsl` wraps URP `GetMainLight` and main-light realtime shadow attenuation for WGame shaders.
- Character, Ruin Environment, Prop, and Foliage Forward passes use the URP main light direction instead of only the original fixed fallback vectors.
- Realtime shadow attenuation is applied before status tint, hit flash, rim, interactable glow, emission, dissolve edge, and fog so gameplay-facing presentation cues stay readable.
- Shadow receive uses relative contrast darkening rather than a fixed absolute shadow color: it cannot brighten the current shaded color, but it keeps a small readable value drop on dark stone.
- Ruin Environment and Foliage apply fog before realtime shadow receive and reduce shadow strength as fog increases, avoiding hard black blocks in distant fog.
- Magic VFX, World Marker, and Impact Decal remain transparent forward-only and do not receive this shadow path.
- `NightRuinShaderReview.unity` uses the existing `Night ruin key light` as the scene sun and enables hard review shadows for visible inspection.
- The scene includes `Shadow receive validation samples`: a light WGame environment receiver slab plus two bronze caster blocks, using `Assets/Art/WGame/Materials/Environment/ShadowValidationReceiver.mat` for clearer shadow contrast.

## Gameplay Shadow Performance Default

The 2026-06-07 performance follow-up makes hard main-light shadows the gameplay default:

- `MxFrameworkURPAsset` uses `2048` main-light shadowmap resolution, shadow distance `40`, `2` cascades, and soft shadows disabled.
- Foliage keeps shadow receive but drops the `ShadowCaster` pass and review-scene foliage renderers have Cast Shadows disabled.
- Review-scene caster classification keeps Character, Prop samples, large rear ruin slabs, and the dedicated shadow-validation casters as realtime shadow casters.
- Floor, plinth, HeadPad reference plate, transparent VFX/marker/decal samples, text, foliage, and the shadow receiver slab do not cast realtime shadows.
- The review scene now has `12` realtime casters and `20` non-casters among its sample renderers.
- Unity rendering stats still reported `0` draw calls and `0` shadow casters in the Editor snapshot, so this pass records renderer classification and URP configuration rather than using that stats payload as frame-cost evidence.

This is still a stylized receive path, not a physically based lighting rewrite and not an additional-light solution.

## Skybox / Height Fog Atmosphere Review

The 2026-06-07 atmosphere follow-up expands `NightRuinShaderReview.unity` from material/shadow inspection into a minimal night-ruin atmosphere review:

- `NightRuinShaderReview_Camera` clears to Skybox so the scene camera can verify the dark navy / blue-green ruin backdrop instead of a flat solid color.
- `Assets/Art/WGame/Materials/Environment/NightRuinSkybox.mat` is a review-only procedural skybox material for the WGame night ruin slice.
- `NightRuinShaderReview_VolumetricFogRoot` uses the generic `MxVolumetricFogSceneBootstrap` component to register `MxVolumetricFogPassProvider` into the existing `MxRenderingPipelineFeature.Pipeline`.
- The height-fog provider uses a Low/Gameplay cost setting: `6` quality samples, diagnostic downsample `4`, no temporal history claim, and focus clear retained for near character/marker readability.
- The scene enables `PreviewInSceneView` for art review. Framework defaults keep SceneView disabled unless a scene/bootstrap explicitly opts in.
- Unity built-in `RenderSettings.fog` is disabled. The scene's atmosphere must be visible from the framework fullscreen global height-fog pass and the skybox, not from ordinary Unity fog.

This does not add a renderer feature, local fog volume, runtime URP Volume object application, Gameplay authority path, Replay input, SaveState input, or Runtime hash input.

## ArtBible / Docs Sync

Updated docs:

- `ArtSource/Projects/WGame/ArtBible/RENDERING_STYLE.md` now records the v1 closeout evidence and no longer phrases the whole shader family as purely unimplemented intent.
- `ArtSource/Projects/WGame/ArtBible/SHADER_DEVELOPMENT_PLAN.md` now links this full review as the #596 closeout record and records the v1 material sample inventory result.
- `Docs/RENDERING_AUTHORING_GUIDE.md` keeps the WGame shader family references framed as project-specific examples, not MxFramework public API or framework-wide mandatory rules.

`Docs/ROADMAP.md` was not changed because it has no current WGame shader-family milestone row to sync; Phase 15 remains the framework Rendering bus roadmap and still does not define production WGame shader implementation as framework API.

## Residual Risks And Follow-ups

- Frame Debugger and live overdraw inspection were not available through issue-worktree MCP. Future production placement should inspect dense transparent VFX/marker/decal overlap in a real scene.
- Historical full-review captures use temporary primitive preview geometry; the persistent review scene now exists, but still uses primitive review geometry rather than production meshes, imported textures, or a production WGame level.
- Current ramp/noise strategy remains budget guidance; no production ramp/noise texture asset was created by #595 or #596.
- Runtime WGame view adapters for marker pulse, VFX lifetime, prop/character material feedback, and decal fade remain future integration work. Do not overload `BridgeCustom` without a follow-up channel decision.
- Parent #584 final post-merge closeout remains for the supervisor after this PR is merged.

## Validation

- Unity MCP discovery attempted; unavailable at `http://127.0.0.1:8080/mcp`.
- Unity GUI issue-worktree capture/import run completed with no `Shader error` or `Shader warning` in `/tmp/issue596-shader-family-full-review-rerun.log`.
- `file` checks confirmed all three final captures are PNG images, `1600 x 900`, RGBA, non-empty.
- Manual visual check confirmed all three captures are openable and nonblank.
- Manual docs/path/property audit compared this closeout against #595 budget and actual `Assets/Art/WGame/Shaders/**` / `Assets/Art/WGame/Materials/**` paths.
- 2026-06-07 Unity MCP follow-up created the persistent Character material, assigned it in `NightRuinShaderReview.unity`, added full-family persistent review samples, validated the scene with no missing scripts or broken prefab references, and confirmed Unity Console had no shader/material import errors.
- 2026-06-07 visual screenshot inspection confirmed the full family is visible in the persistent scene. Temporary screenshot files were deleted after inspection.
- 2026-06-07 depth/shadow follow-up imported cleanly, added focused EditMode pass coverage, and confirmed `NightRuinShaderReview.unity` still has no missing scripts, broken prefab references, or missing/error materials.
- 2026-06-07 main-light receive trial imported cleanly, passed the 18 targeted Rendering EditMode tests, validated `NightRuinShaderReview.unity`, and confirmed shader/material Console filters had no entries. Shadow validation screenshots showed visible receiver/caster shadows and were deleted after inspection.
- 2026-06-07 relative shadow contrast follow-up replaced clamp-only shadow receive with luminance-based darkening and fog-scaled environment/foliage receive. Unity refresh completed, Console error and Shader warning filters returned no entries, `NightRuinShaderReview.unity` validation reported no issues, and a temporary Scene View shadow check confirmed visible receiver/caster contrast before the screenshot was deleted.
- 2026-06-07 gameplay shadow performance follow-up set the active URP asset to `2048` / `40` / `2 cascades` / hard shadows, removed the Foliage realtime `ShadowCaster` pass, disabled nonessential review-scene casters, and kept visible caster/receiver contrast under the reduced default. Unity refresh, Console error filter, Shader warning filter, and scene validation all passed; the temporary visual check screenshot was deleted.
- 2026-06-07 atmosphere follow-up added skybox camera clear, a WGame procedural night skybox material, and a scene-level bootstrap for the existing framework global height fog; Unity built-in `RenderSettings.fog` is disabled and explicit SceneView preview is enabled for this review scene, so validation should confirm visible sky, distant ruin fog layering, near readability, and no Console shader/import errors through the framework pass.
