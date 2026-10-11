# WGame Shader Texture And Material Budget

> Status: Current
>
> Issue: #595 `[Tech Art] WGame Shader texture/material budget and sample library`
>
> Scope: WGame shader family texture budgets, mask conventions, material sample library inventory, transparent review format, and #596 handoff inputs.
>
> Boundary: This is a WGame project tech-art profile. It is not MxFramework public API, is not a framework-wide material budget, and does not create or modify Unity assets.

> 2026-09-20 character update: the character-specific mixed-shader descriptions below are historical. `WGame/CharacterStylizedLit` is now baked-only; palette and flat previews use `WGame/CharacterPaletteLegacy`, while traditional textures use `WGame/CharacterTraditionalLit`. Current contracts and validation are in [the character guide](../../Guides/CHARACTER_BAKED_SURFACE_AND_HAIR.md) and [the split acceptance](CharacterBakedShaderSplit.md).

## Source Inputs

This document reconciles the first-pass WGame shader family work that landed through #586-#594:

| Area | Current source |
| --- | --- |
| Style target | `ArtSource/Projects/WGame/ArtBible/RENDERING_STYLE.md`, `ArtSource/UIKits/MxRuinNight/source/style-guide.md`, `ArtSource/UIKits/MxRuinNight/source/component-catalog.md` |
| Shader family plan | `ArtSource/Projects/WGame/ArtBible/SHADER_DEVELOPMENT_PLAN.md` |
| Shared HLSL | `Assets/Art/WGame/Shaders/ShaderLibrary/*.hlsl` |
| Shader family sources | `Assets/Art/WGame/Shaders/**/*.shader` |
| Material samples | `Assets/Art/WGame/Materials/**/*.mat` |
| Review records | `Docs/Validation/WGameShaderFamily/*.md` |

All WGame material and presentation properties remain under `_Wg*`. Framework-owned globals remain `_Mx*` and may only be read when they are documented by Rendering. `WGame/CharacterStylizedLit` and `WGame/PropStylizedLit` additionally expose Unity-compatible `_BaseMap` and `_Color` properties for traditional albedo interop; generated character body and public weapon materials should address those shader properties through material templates that map semantic inputs such as `surface.albedoTexture`, not through package authoring data that names shader slots directly.

`WGame/MagicVfxUnlit` additionally exposes `_BaseMap`/`_BaseColor`, `_EmissionMap`/`_EmissionColor` and standard blend/depth/cull state for its `_WG_VFX_TEXTURED` particle/line interoperability mode. These preserve existing texture/vertex-color/lifetime-alpha bindings; procedural VFX remains the default and uses its existing `_Wg*` controls. The textured branch performs one base sample and an optional emission sample, without procedural noise, shape, pulse or depth-fade work. `WorldMarkerUnlit` adds `_WG_MARKER_MESH` for already-shaped geometry without UV/color streams; that branch needs no texture/depth sample. Its new render-state controls use `_Wg*` names to avoid activating stale stock-shader properties on old marker assets. Both modes stay in the existing forward pass and are initialized through the project-only `CharacterTestUnlitMaterials` helper.

Both shaders also expose the opt-in palette properties `_WgPaletteTex`、the import-owned `_WgPaletteRow` profile index、`_WgPaletteEnabled` and `_WgPaletteParams` behind `_WG_PALETTE_ON`. These properties are a separate explicit authoring mode, not a fallback that may silently replace traditional albedo. `_WgPaletteRow` is an immutable character-profile implementation field, not a supported runtime recolor channel.

## Ramp Texture V1 Strategy

V1 ramp authoring is a shared family strategy, not a per-material texture sprawl:

| Rule | Budget |
| --- | --- |
| Lit families use `_WgRampTex` when they need stylized lighting steps. | Prefer one shared WGame ramp strip/atlas per review profile before creating family-specific ramp textures. |
| Default samples may use Unity's default white texture. | Do not claim a production ramp asset exists until it is generated and committed by a later asset issue. |
| Ramp bands should preserve MxRuinNight dark stone, moss, bronze, parchment, and amber hierarchy. | Keep character/prop readability above environment foliage and decals. |
| Ramp rows, if authored later, should be named by use rather than gameplay ids. | Example row intent: neutral stone, moss/foliage, character/prop, amber focus. |

The current shader support is:

| Shader | Ramp property |
| --- | --- |
| `WGame/CharacterStylizedLit` | `_WgRampTex` |
| `WGame/EnvironmentStylizedLit` | `_WgRampTex` |
| `WGame/PropStylizedLit` | `_WgRampTex` |
| `WGame/FoliageMossLit` | `_WgRampTex` |
| `WGame/MagicVfxUnlit` | No ramp; unlit emission and masks carry shape. |
| `WGame/WorldMarkerUnlit` | No ramp; unlit diamond/corner/beacon shape carries meaning. |
| `WGame/ImpactDecal` | No ramp; decal/crack shape, dissolve, and stone blend carry meaning. |

## Packed Mask Map Convention

Use one packed mask texture per material where authored region control is needed. Default procedural or Unity default textures are acceptable for current sample materials; this table defines the authoring convention for later texture work.

| Family | Property | R | G | B | A |
| --- | --- | --- | --- | --- | --- |
| Character | `_WgMaskTex` | Cloth / primary region | Leather / secondary region | Bronze trim | Magic core or emission mask |
| Ruin environment | `_WgMaskTex` | Weathered stone | Moss | Bronze trim | Facet or surface variation seed |
| Props | `_WgMaskTex` | Worn base / material split | Facet variation | Bronze trim | Interactable glow |
| Foliage | `_WgMaskTex` | Alpha coverage | Moss | Dry grass | Wind or variation weight |
| Magic VFX | `_WgMaskTex` | Shape coverage | Optional edge variant | Optional inner variant | Optional lifetime variant |
| World marker | `_WgMarkerTex` | Shape coverage | Reserved | Reserved | Reserved |
| Impact decal | `_WgDecalTex`, `_WgCrackTex` | Decal/crack coverage | Reserved | Reserved | Reserved |

Channel reuse rules:

- Do not introduce `_Mx*` material parameters for WGame mask data.
- Do not encode gameplay truth, collision, damage authority, save state, replay state, or runtime hash inputs into mask channels.
- If a family needs more than one mask texture for production assets, #596 or a later asset issue should record the reason and update the sample/library table.

## Character Traditional Albedo Slot

`WGame/CharacterStylizedLit` uses `[MainTexture] _BaseMap` multiplied by `_Color` as the character base color authority. It does not expose an albedo blend weight: authored texture color always enters the lighting path intact.

Generated imported-character body materials use the `wgame.character_stylized_lit` template for the `character.body` role. That template maps the abstract surface inputs `surface.albedoTexture` and `surface.albedoTint` directly to `_BaseMap` and `_Color`. A conventional `body_albedo.png` therefore remains the authoritative color texture through Unity import and runtime rendering.

`_WgMaskTex` remains the packed material-region mask for cloth, leather, trim, and emission semantics. Do not store body albedo color in `_WgMaskTex`, and do not treat `_BaseMap` as gameplay, collision, damage, replay, or save-state data.

## Prop Traditional Albedo Slot

`WGame/PropStylizedLit` supports the same minimal traditional albedo interop through `[MainTexture] _BaseMap`, `_Color`, and `_WgAlbedoStrength`. The default `_WgAlbedoStrength = 0` preserves existing prop mask/ramp materials; setting it to `1` lets a generated public weapon material use a conventional albedo/base-color texture while keeping the prop lighting, ramp, normal, hit flash, status tint, dissolve, rim, and fog behavior.

Public weapon wrappers use the `wgame.prop_stylized_lit` template for the weapon prop role. That template maps `surface.albedoTexture`, `surface.albedoTint`, and `surface.albedoWeight` to `_BaseMap`, `_Color`, and `_WgAlbedoStrength`. `weapon_sources.json` can declare an optional `texture` item with `target.bindTarget = "weapon.albedo"`; if it is absent, Unity import/material fallback is used, and if no texture exists a visible default prop material is still generated.

`_WgMaskTex` remains the packed prop material-region mask. Do not store weapon albedo color in `_WgMaskTex`, and do not write shader slot names such as `_BaseMap` into weapon authoring JSON.

## Shared Palette UV Mode

`mx.characterPaletteBinding.v5` selects `wgame.character_palette_stylized_lit`; compatible public weapons use `wgame.prop_palette_stylized_lit`. Both use the project-level `Assets/Art/WGame/Textures/Character/WGameColorPaletteAtlas.png` RGB24 asset. The fragment stage performs the existing single Color Palette sample, while `PaletteData.A` selects one of 16 generated Surface Profiles without a material LUT texture, extra pass, keyword, or draw call. Binding v1-v4 require the explicit per-character FaceDetail migration; Usage v1 remains a repaint-required input and is not normalized forward.

The Color Atlas contract is fixed at 256×256 RGB24, sRGB, Point, Clamp, no mipmaps and no compression. Index `0..65535` is a complete RGB565 address: `index=(R5<<11)|(G6<<5)|B5`, and expanding the three components produces exactly 65,536 unique RGB8 colors with zero aliases. `TEXCOORD1.xy` selects the exact pixel center. The raw Atlas is technical storage; Blender and CharacterStudio use a color picker, formula components, recent/favorite/used views and map arbitrary input RGB to the nearest RGB565 address. Vertex color R/G/B carries static AO, rim and emission weights, while A selects one of 16 Surface Profile centers.

The palette branch samples the atlas at LOD 0 and skips `_BaseMap`、`_WgNormalTex` and `_WgMaskTex`. Import must reject missing UV1、missing vertex colors、off-center PaletteUV、invalid Atlas material-class alpha、an Atlas importer that discards input alpha or a missing Atlas. `CharacterPaletteBinding` may apply the immutable Profile through `MaterialPropertyBlock` when a prefab initializes or its renderer hierarchy changes, but it exposes no runtime Palette row/Atlas mutation API. Shared materials and the shared Atlas remain identical across characters, and `renderer.material` is forbidden.

### Per-character Face Detail

Palette characters may opt into one additional character-owned face-detail sample through `_WG_FACE_DETAIL_ON`、`_WgFaceDetailEnabled`、`_WgFaceDetailTex` and `_WgFaceDetailIntensity`. The model provides `TEXCOORD2 / FaceDetailUV`: face values directly address the complete 0..1 image and every non-face loop uses `(-1,-1)` as the neutral sentinel. When disabled, neither texture nor UV2 is required.

Each enabled character owns one 512×512 Direct RGBA texture under its package. RGB is the final sRGB detail color and A is coverage; the fragment shader samples it once and alpha-blends it over the Palette-lit surface. There is no atlas-cell transform, preset switch, Palette Mask routing, feature-color lookup, extra varying, pass, draw call, or runtime material instance.

The source authority is `ArtSource/Characters/<packageId>/FaceDetail/face_detail_rgba.png`, paired with an `mx.characterFaceDetailTexture.v1` build manifest. Unity imports the generated package texture as sRGB、Bilinear、Clamp、mipmapped、Alpha FromInput、non-readable、maximum 512 and Android ASTC 6×6. `CharacterPaletteBinding` supplies the serialized character texture and intensity through `MaterialPropertyBlock`; an enabled binding rejects missing, shared, or cross-package resources and hash drift.

The mobile hybrid Palette lighting remains analytic: `_WgPaletteLightingParams` controls global wrap scale、two thresholds and band softness; `_WgPaletteValueParams` controls shadow/mid/light values and SH ambient strength. The Catalog-generated 16 Surface Profiles provide shadow floor、wrap、AO、specular power、rim、roughness、metallic and procedural-detail mode without a material LUT sample. Shared sky/ground hemisphere fill keeps dark Palette colors readable, while `_WgPaletteIndirectNormalInfluence` blends constant/average SH with normal-directed SH. Hair sheen、silk、leather finish、metal Fresnel and stylized opaque crystal response derive from existing normal/view/light inputs. Main-light form bands remain authoritative; Forward/Forward+ additional lights use a softer analytic band and bounded strength. Cast-shadow visibility stays separate from form-band selection, and the Palette branch preserves positive HDR output for tonemapping/Bloom. This adds no ramp/normal/material texture sample and no outline pass.

`WGame/CharacterStylizedLit` uses the project-owned `WGameCharacterStylizedLitShaderGui` instead of Unity's flat default material inspector. Palette mode shows an import/runtime-owned appearance summary by default; shared LookDev parameters、global procedural-detail quality/distance、technical values and runtime effect channels live in explicit foldouts. The 16 Profile parameter vectors are generated HLSL and are no longer editable material properties. Palette dimensions、Atlas binding、FaceDetail resource ownership and culling remain read-only system state, while traditional-only BaseMap/Normal/Mask inputs are hidden from Palette mode. The inspector can restore shared Look defaults, but it must not mutate authored Color/Profile、FaceDetail texture or runtime effect state.

## Dissolve And Facet Noise Reuse

V1 should reuse grayscale noise concepts before adding unique texture sources:

| Use | Current property | Budget rule |
| --- | --- | --- |
| Character dissolve | `_WgDissolveNoiseTex` | Prefer shared grayscale dissolve noise. |
| Prop dissolve | `_WgDissolveNoiseTex` | Reuse the same dissolve family as character unless art direction requires different scale. |
| Magic VFX motion | `_WgNoiseTex` | Use a small shared grayscale/noise source; avoid per-effect unique animated noise in v1. |
| Impact decal cracks | `_WgCrackTex` | Can reuse a crack/noise source, but long-term decal texture variety needs a later production asset budget. |
| Ruin/facet variation | `_WgMaskTex.a`, vertex color, or procedural position seed | Do not add a separate facet texture unless captures show repeated tiling or unreadable planes. |
| Foliage wind/facet variation | vertex color alpha, `_WgMaskTex.a`, or procedural seed | Keep wind as presentation shader state only. |

No dissolve, facet, crack, or noise texture asset is created by #595.

## Texture Budget Targets

These are WGame v1 targets for future authored texture assets. They are not claims about existing committed texture files.

| Texture kind | V1 target | Notes |
| --- | --- | --- |
| Shared ramp strip/atlas | 1 shared WGame profile asset before family-specific ramps. | Used by lit families only. |
| Packed mask maps | 0-1 per material that needs authored regions. | Prefer packed channels over separate cloth/moss/trim masks. |
| Character body albedo | Required for traditional generated character materials; absent in explicit palette mode. | The `wgame.character_stylized_lit` material template maps `surface.albedoTexture` directly to the authoritative `_BaseMap`; `wgame.character_palette_stylized_lit` instead uses the one project-level palette Atlas. |
| Public weapon albedo | Optional per reusable public weapon wrapper material. | The `wgame.prop_stylized_lit` material template maps `surface.albedoTexture` to `_BaseMap` and uses `_WgAlbedoStrength = 1` when an explicit or imported albedo texture is available. |
| Shared character Color Palette | Exactly 1 project-level `256×256` RGB24 texture. | All 65,536 RGB565-derived cells are valid and unique; zero aliases; shared by explicit palette characters and compatible props; sRGB、Point、Clamp、no mipmaps、uncompressed. Surface Profiles are generated HLSL, not another texture. |
| Shared face-detail Atlas | Exactly 1 project-level `2048×2048` RGBA mask texture. | Optional for palette characters; 16 complete presets, Linear、Bilinear、Clamp、Mipmap、Android ASTC 6×6. |
| Normal/facet maps | Optional. | Character and props expose `_WgNormalTex`; environment/foliage currently rely on normals, vertex color, and facet helpers. |
| Emission masks | Optional, small-area. | Character exposes `_WgEmissionMaskTex`; VFX/marker/decal primarily use shape masks and material params. |
| Dissolve/noise | 1 shared grayscale noise family where possible. | Use `_WgDissolveNoiseTex`, `_WgNoiseTex`, or `_WgCrackTex` by family. |
| Transparent shape textures | Optional. | VFX, markers, and decals must remain compact and shape-first; avoid large filled rings. |

## Sample Library Path Convention

| Asset type | Convention |
| --- | --- |
| Shader source | `Assets/Art/WGame/Shaders/<Family>/<ShaderName>.shader` |
| Shared include | `Assets/Art/WGame/Shaders/ShaderLibrary/<IncludeName>.hlsl` |
| Material sample | `Assets/Art/WGame/Materials/<Family>/<SampleName>.mat` |
| Validation note | `Docs/Validation/WGameShaderFamily/<FamilyOrReview>.md` |
| Validation capture | `Docs/Validation/WGameShaderFamily/<FamilyOrReview>_<Case>.png` |

Sample names should describe visual purpose, not production gameplay ids. Current examples use names such as `MagicAmberFocus`, `WorldMarkerTargetDiamond`, `PropLockedDisabled`, and `ImpactDecalCrackBreak`.

## Landed Shader Families And Samples

The table below lists current landed sample paths. Do not add sample paths to this table until the assets actually exist.

| Family | Shader source | Minimum landed samples | Purpose |
| --- | --- | --- | --- |
| Shared HLSL | `Assets/Art/WGame/Shaders/ShaderLibrary/WGameShaderCommon.hlsl`; `Assets/Art/WGame/Shaders/ShaderLibrary/WGameStylizedLighting.hlsl`; `Assets/Art/WGame/Shaders/ShaderLibrary/WGameFogBlend.hlsl`; `Assets/Art/WGame/Shaders/ShaderLibrary/WGameEmission.hlsl`; `Assets/Art/WGame/Shaders/ShaderLibrary/WGameRimLighting.hlsl`; `Assets/Art/WGame/Shaders/ShaderLibrary/WGameDissolve.hlsl`; `Assets/Art/WGame/Shaders/ShaderLibrary/WGameDepthShadowPass.hlsl`; `Assets/Art/WGame/Shaders/ShaderLibrary/WGameMainLight.hlsl` | No material samples. | Shared helpers for the family shaders, depth/shadow passes, and URP main-light shadow receive. |
| Character | `Assets/Art/WGame/Shaders/Character/CharacterStylizedLit.shader` | `Assets/Art/WGame/Materials/Character/CharacterStylizedLitReview.mat`; `Assets/Art/WGame/Materials/Character/CharacterPaletteShared.mat` | Traditional character review plus the shared palette-mode character material. |
| Environment | `Assets/Art/WGame/Shaders/Environment/EnvironmentStylizedLit.shader` | `Assets/Art/WGame/Materials/Environment/RuinDarkStone.mat`; `Assets/Art/WGame/Materials/Environment/RuinMossStone.mat`; `Assets/Art/WGame/Materials/Environment/RuinBronzeTrim.mat`; `Assets/Art/WGame/Materials/Environment/ShadowValidationReceiver.mat`; `Assets/Art/WGame/Materials/Environment/NightRuinSkybox.mat` | General stylized environment surfaces with dark stone, moss stone, bronze trim, a light review-only receiver slab for main-light shadow inspection, and a procedural night skybox for atmosphere review. |
| Magic VFX | `Assets/Art/WGame/Shaders/VFX/MagicVfxUnlit.shader` | `Assets/Art/WGame/Materials/VFX/MagicAmberFocus.mat`; `Assets/Art/WGame/Materials/VFX/MagicDangerEmber.mat`; `Assets/Art/WGame/Materials/VFX/MagicHealingGreen.mat` | Amber focus, danger ember, and healing green shape/emission variants. |
| World marker | `Assets/Art/WGame/Shaders/Marker/WorldMarkerUnlit.shader` | `Assets/Art/WGame/Materials/Marker/WorldMarkerTargetDiamond.mat`; `Assets/Art/WGame/Materials/Marker/WorldMarkerInteractCorners.mat`; `Assets/Art/WGame/Materials/Marker/WorldMarkerQuestBeacon.mat` | Target diamond, interact corners, and quest beacon markers without broad transparent rings. |
| Props | `Assets/Art/WGame/Shaders/Props/PropStylizedLit.shader` | `Assets/Art/WGame/Materials/Props/PropChestAmberFocus.mat`; `Assets/Art/WGame/Materials/Props/PropBronzeMechanism.mat`; `Assets/Art/WGame/Materials/Props/PropLockedDisabled.mat`; `Assets/Art/WGame/Materials/Props/PropDamagedDissolve.mat`; `Assets/Art/WGame/Materials/Props/PropPaletteShared.mat` | Traditional prop states plus the shared palette-mode public weapon material. |
| Foliage | `Assets/Art/WGame/Shaders/Environment/FoliageMossLit.shader` | `Assets/Art/WGame/Materials/Environment/FoliageMossPatch.mat`; `Assets/Art/WGame/Materials/Environment/FoliageDryGrass.mat` | Moss patch and dry grass with restrained vertex-stage wind. |
| Impact decal | `Assets/Art/WGame/Shaders/Decal/ImpactDecal.shader` | `Assets/Art/WGame/Materials/Decal/ImpactDecalHitMark.mat`; `Assets/Art/WGame/Materials/Decal/ImpactDecalCrackBreak.mat` | Short hit mark and crack/break decal feedback. |
| Review scene | `Assets/Scenes/WGame/NightRuinShaderReview.unity` | Persistent full-family review scene includes Character, Environment, VFX, Marker, Props, Foliage, Impact Decal, static HeadPad/HUD reference samples, dedicated shadow receiver/caster validation samples, skybox camera clear, disabled Unity built-in fog, and a low-cost framework height-fog scene bootstrap with SceneView preview enabled. | Static shader family review scene from #590, extended for full-family, shadow, skybox, and framework height-fog inspection. |

## Transparent Review Format

Use this format for transparent VFX, marker, and decal reviews. It is aligned with #590, #592, and #594, and supplies the review template for #596.

| Field | Required note |
| --- | --- |
| Source and material paths | List exact shader and sample `.mat` paths. |
| Shape language | State whether meaning is carried by diamond/corner/crack/angular shape rather than glow alone. |
| Bloom result | Compare normal and reduced-bloom captures. Reduced bloom must remain readable. |
| Overdraw footprint | Record screen area, transparent quad count when known, and whether broad rings/full-screen overlays are avoided. |
| Sorting / depth | Record `Queue`, `Blend`, `ZWrite`, depth fade, and expected sorting risk. |
| Frame Debugger status | Record actual Frame Debugger evidence, or explicitly state residual risk if not available. |
| HeadPad/HUD boundary | Confirm markers/VFX/decals do not encode names, health, quest text, or persistent UI state. |
| Material binding | State whether `MaterialBindingHub` is used, not used, or remains a future view-adapter concern. |
| Runtime authority boundary | Confirm values are presentation/material state only and do not feed gameplay authority, Replay, SaveState, or Runtime hash. |

Current transparent family notes:

- #590 established reduced-bloom readability through shape and edge value in `Docs/Validation/WGameShaderFamily/NightRuinShaderReview.md`.
- #592 refined target/interact/quest marker shapes in `Docs/Validation/WGameShaderFamily/WorldMarkerPolish.md`.
- #594 recorded transparent decal sorting, overdraw, lifetime, fade, and Frame Debugger residual risk in `Docs/Validation/WGameShaderFamily/ImpactDecal.md`.

## #596 Handoff

#595 supplies the budget, sample inventory, and review format inputs. #596 should perform the final family review and docs sync after #586-#595 are merged.

#596 should verify:

- Every shader family still uses `_Wg*` for WGame properties and only documented `_Mx*` globals.
- The landed material sample table still matches actual paths.
- The persistent Character material sample and full-family scene references are present.
- Transparent review notes include reduced-bloom, overdraw/Frame Debugger, sorting, and HeadPad/HUD boundary results.
- WGame-specific budget guidance remains separate from MxFramework public Rendering rules.

## #595 Manual Audit

Docs-only audit checklist:

- Cross-check this table against `Assets/Art/WGame/Shaders/**` and `Assets/Art/WGame/Materials/**`.
- Cross-check review format against `NightRuinShaderReview.md`, `WorldMarkerPolish.md`, `FoliageMossLit.md`, and `ImpactDecal.md`.
- Do not run Unity or create/import assets for #595.
- Do not reference nonexistent sample paths.
- Do not promote WGame texture budgets into MxFramework public API or framework-wide authoring mandates.
