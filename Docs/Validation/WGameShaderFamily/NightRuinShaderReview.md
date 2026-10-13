# Night Ruin Shader Review Validation

> Status: Archive
>
> Issue: #590
>
> Scene: `Assets/Scenes/WGame/NightRuinShaderReview.unity`
>
> Captures:
> - `Docs/Validation/WGameShaderFamily/NightRuinShaderReview_NormalBloom.png`
> - `Docs/Validation/WGameShaderFamily/NightRuinShaderReview_ReducedBloom.png`

## API Reuse Plan

The scene is a static Runtime Showcase / scene validation entry, not a production WGame level and not a full Playable Demo.

| Area | Decision |
| --- | --- |
| Rendering shader/material assets | Reused landed assets from #587/#588/#589: `WGame/CharacterStylizedLit`, `WGame/EnvironmentStylizedLit`, `WGame/MagicVfxUnlit`, `WGame/WorldMarkerUnlit`, and the environment/VFX/marker material samples. The 2026-06-07 follow-up adds only the persistent Character review material and reuses existing sample materials for the later Prop/Foliage/Impact families. |
| RuntimeHost / command buffer | Not used. The scene has no gameplay loop, input, commands, replay, save state, or runtime hash. |
| Unity composition root / scene entry | The Unity entry is `Assets/Scenes/WGame/NightRuinShaderReview.unity`; the 2026-06-07 atmosphere follow-up adds a generic `MxVolumetricFogSceneBootstrap` scene root to register the existing Rendering height-fog provider. |
| UI Toolkit / HeadPad style reference | Represented as a static in-scene HeadPad-style plate with label, amber diamond, and corner markers. No HeadPad runtime, UI Toolkit runtime, or DebugUI binding was implemented. |
| Rendering framework APIs | No public Rendering API, `MxRenderingPipelineFeature`, `IMxRenderPassProvider`, `MxMaterialChannel`, or renderer feature changes. The scene reuses the existing `MxRenderingPipelineFeature` and `MxVolumetricFogPassProvider` for atmosphere validation. |

## Scene Content

- Character reference in the center uses the landed `WGame/CharacterStylizedLit` shader with `Assets/Art/WGame/Materials/Character/CharacterStylizedLitReview.mat`.
- Environment samples reuse `RuinDarkStone`, `RuinMossStone`, and `RuinBronzeTrim` material assets from #588.
- Magic VFX samples reuse `MagicAmberFocus`, `MagicDangerEmber`, and `MagicHealingGreen` material assets from #589.
- World marker samples reuse `WorldMarkerTargetDiamond` and `WorldMarkerInteractCorners` material assets from #589.
- Prop samples reuse `PropChestAmberFocus`, `PropBronzeMechanism`, `PropLockedDisabled`, and `PropDamagedDissolve` material assets from #595.
- Foliage samples reuse `FoliageMossPatch` and `FoliageDryGrass` material assets from #595.
- Impact Decal samples reuse `ImpactDecalHitMark` and `ImpactDecalCrackBreak` material assets from #595.
- HeadPad/UI style reference is static: a dark bronze plate, amber diamond/corner accents, and a text label placed above the character.
- The atmosphere review uses `Assets/Art/WGame/Materials/Environment/NightRuinSkybox.mat`, `NightRuinShaderReview_Camera` clears to Skybox, and `NightRuinShaderReview_VolumetricFogRoot` registers the existing framework global height-fog pass at a Low/Gameplay cost setting. The scene enables the explicit SceneView preview switch so art review can see the same height-fog atmosphere in the Unity Scene view.
- Unity built-in `RenderSettings.fog` is disabled in this scene. Atmosphere validation must come from the framework height-fog pass plus the skybox, not from Unity's ordinary scene fog.

## 2026-06-07 Persistent Full-Family Update

The scene was updated through Unity MCP to make the full shader family review persistent:

- `Assets/Art/WGame/Materials/Character/CharacterStylizedLitReview.mat` was created for Character review.
- `Full shader family persistent samples` was added under the static composition root.
- Prop, Foliage, and Impact Decal review objects were added beside the existing Character, Environment, Magic VFX, World Marker, and HeadPad/HUD references.
- The update is still a static visual review scene; it adds no runtime loop, input, UI Toolkit binding, gameplay authority, Replay, SaveState, or Runtime hash path.
- The 2026-06-07 main-light receive trial keeps the existing `Night ruin key light` as the scene sun, enables hard review shadows, and adds `Shadow receive validation samples` for visible caster/receiver inspection.
- The follow-up shadow receive path uses relative darkening for dark stone readability, rather than a fixed absolute shadow color or clamp-only blend.
- The atmosphere follow-up disables ordinary Unity RenderSettings fog and enables the framework global height-fog pass through the scene bootstrap for spatial depth validation. SceneView preview is explicitly enabled for this review scene only; ordinary framework defaults still target Game cameras.

## Capture Method

Unity GUI was used against the issue-590 worktree:

```text
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -projectPath /Users/vincent/Documents/WGameFramework.worktrees/issue-590 \
  -executeMethod Issue590NightRuinReviewGenerator.Generate \
  -quit \
  -logFile /tmp/issue590-night-ruin-review.log
```

No `-batchmode` flag was used. The temporary Editor script created the scene through Unity APIs, rendered the approved scene camera to PNG, and was deleted before final commit.

## Normal / Reduced Bloom Result

Normal bloom capture:

- Character silhouette is the clearest central shape.
- Environment dark stone, moss stone, and bronze samples remain distinguishable without taking priority from the character.
- Amber focus, danger ember, healing green, target diamond, and interact corner samples are visually separated.
- The static HeadPad style reference remains readable above the character.

Reduced bloom capture:

- Marker and VFX meaning remains readable through diamond/corner shape and edge value, not glow alone.
- Amber, ember, and green samples remain distinguishable.
- Character silhouette and HeadPad reference remain visible.

## Overdraw / Frame Debugger

Frame Debugger was not run because Unity MCP did not expose the issue-590 worktree; the MCP endpoint was unavailable at `http://127.0.0.1:8080/mcp`.

Source and scene audit conclusion:

- Transparent VFX and marker samples are small-area quads and do not use large transparent circles, broad hologram rings, or screen-wide overlays.
- The review scene keeps VFX/marker samples away from the character body and HeadPad label except for the intended target diamond reference.
- Residual risk remains for interactive overdraw and Frame Debugger inspection in a live issue-worktree Unity session.

## Material Binding / Dynamic Presentation Notes

- No runtime `MaterialBindingHub` writes are performed in this static scene.
- Dynamic presentation concepts from the shaders, such as dissolve, outline, marker pulse, status tint, and debug overlay, remain material/presentation state only.
- No gameplay, combat authority, replay, save state, or runtime hash path is introduced.

## Style Risk Notes

- The composition is intentionally a review/sample scene, so primitives stand in for production meshes.
- The central character now has a persistent review material. The scene still uses primitive review geometry instead of production character meshes.
- The scene is dark by design; future #596 review should capture additional viewpoints if environment readability needs broader art review.
- HeadPad is a static style reference only; #592/#596 should validate real HeadPad/HUD responsibility split when runtime UI exists.

## Unity MCP / Console Status

- Attempted Unity MCP discovery for the issue worktree.
- MCP resource read failed because `http://127.0.0.1:8080/mcp` was unavailable.
- Branch Unity MCP Console validation was not executed.
- Main worktree Console was not used as branch evidence.
- Unity GUI import/generation log contained no `Shader error` or `Shader warning` for this scene generation. Existing unrelated package/USS/plugin warnings were present.
- 2026-06-07 Unity MCP validation opened the persistent scene, validated it with no missing scripts or broken prefab references, confirmed Unity Console had no shader/material import errors, and visually inspected the full-family scene through camera screenshots.
- 2026-06-07 main-light receive validation passed targeted Rendering EditMode tests, kept shader/material Console filters clean, confirmed visible shadow validation samples, and deleted temporary screenshots after inspection.
- 2026-06-07 atmosphere follow-up changed the review camera to Skybox clear, added a WGame night skybox material, registered the existing global height-fog provider through a generic scene bootstrap, disabled Unity built-in `RenderSettings.fog`, enabled explicit SceneView preview for art review, and confirmed the scene should validate sky/framework-height-fog atmosphere rather than only material passes or ordinary scene fog.

## Play Mode Smoke Status

Play Mode smoke was not executed. This issue is a static shader review/sample scene with no runtime gameplay loop or input path. Opening the scene and validating rendered materials/captures is the intended verification path.
