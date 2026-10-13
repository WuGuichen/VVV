# FoliageMossLit Validation

> Issue: #593 `[Implementation] WGame FoliageMossLit environment completion`
>
> Status: Current
>
> Shader: `Assets/Art/WGame/Shaders/Environment/FoliageMossLit.shader`
>
> Material samples:
>
> - `Assets/Art/WGame/Materials/Environment/FoliageMossPatch.mat`
> - `Assets/Art/WGame/Materials/Environment/FoliageDryGrass.mat`
>
> Captures:
>
> - `Docs/Validation/WGameShaderFamily/FoliageMossLit_Normal.png`
> - `Docs/Validation/WGameShaderFamily/FoliageMossLit_Wind.png`
> - `Docs/Validation/WGameShaderFamily/FoliageMossLit_ReducedBloom.png`

## Scope

#593 adds the first `WGame/FoliageMossLit` shader family pass for low-saturation moss patches, dry grass, and ruin-gap foliage. It does not add a tree/scatter system, persistent scene, prefab, texture, model, runtime code, view adapter, public Rendering API, `MxMaterialChannel`, renderer feature, gameplay authority, Replay, SaveState, Runtime hash, collision, or occlusion dependency.

The shader is an AlphaTest URP shader with `Cull Off` for simple blade/patch geometry. It is intended as a v1 material family, not a vegetation placement system.

## API Reuse And Ownership

- Shared HLSL reuse: `WGameShaderCommon.hlsl`, `WGameStylizedLighting.hlsl`, and `WGameFogBlend.hlsl`.
- Project material parameters use `_Wg*`: foliage, dry, moss, alpha cutoff, coverage, wind bend, fog blend, facet, wetness override, and debug overlay.
- Framework globals are read only through the documented `_Mx*` boundary: `_MxTime`, `_MxWindDirection`, `_MxWindStrength`, `_MxWindTurbulence`, `_MxWetness`, `_MxRain`, `_MxSnowCoverage`, `_MxLocalSubjectWorldPos`, and `_MxViewFocusWorldPos` from the shared fog/global include.
- Wind is vertex-stage presentation only. It does not publish gameplay state, collision, occlusion, Replay, SaveState, or Runtime hash input.
- `WgEmission`, `WgRimLighting`, and `WgDissolve` are intentionally not included because #593 foliage samples are non-emissive, non-outlined, non-dissolving environment materials.

## Material Audit

Both approved material samples were generated through Unity `AssetDatabase.CreateAsset` in the issue worktree:

| Material | Shader | Key intent |
| --- | --- | --- |
| `FoliageMossPatch` | `WGame/FoliageMossLit` | Damp low-value moss patch with high moss coverage, light wind, and local wetness override. |
| `FoliageDryGrass` | `WGame/FoliageMossLit` | Dry/desaturated grass with higher dry coverage and stronger authored wind bend. |

Material YAML was not hand-written. The final material files contain the generated shader reference and key `_WgFoliageColor`, `_WgDryColor`, `_WgMossColor`, `_WgAlphaCutoff`, `_WgWindBendStrength`, `_WgFogBlendStrength`, and `_WgWetnessOverride` properties.

## Capture Method

Unity MCP was probed first but was unavailable at `http://127.0.0.1:8080/mcp`, so no main-worktree Console or Test Runner result is used as branch validation.

The captures and material samples were generated in the issue worktree with Unity GUI:

```text
Unity -projectPath /Users/vincent/Documents/WGameFramework.worktrees/issue-593 -executeMethod Issue593FoliageMossLitGenerator.Generate -quit -logFile /tmp/issue593-foliage-generator.log
```

The temporary Editor generator created an unsaved preview scene, rendered the camera to PNG, generated the two approved material assets, then was removed before commit. The Unity log had no `Shader error` or `Shader warning` entries.

## Visual Review

### Normal

`FoliageMossLit_Normal.png` places moss patches and dry grass over #588-style dark ruin stone context. The foliage stays in the environment value range and remains subordinate to character/marker/prop focus language from #590/#591/#592.

### Wind

`FoliageMossLit_Wind.png` raises `_MxWindStrength` and `_MxWindTurbulence` for a static capture of vertex-stage bend. The authored bend is visible on grass silhouettes but stays restrained; it is presentation-only shader state.

### Reduced Bloom

`FoliageMossLit_ReducedBloom.png` uses the same non-emissive material family without bloom-dependent meaning. The result remains readable through silhouette, dry/moss color split, and fog/value separation rather than glow.

## Cross-Family Context

- #588 `EnvironmentStylizedLit`: Foliage shares the dark stone, moss, and fog language but uses alpha clip and vertex colors for blade/patch readability.
- #590 review scene: This validation keeps the same shader-review stance and does not modify the scene or its captures.
- #591 `PropStylizedLit`: Foliage avoids amber focus and outline language so interactable props keep priority.
- #592 `WorldMarkerUnlit`: Foliage stays low saturation and small-area so marker diamonds/corners remain the spatial focus near HeadPad/HUD zones.

## Risks

- No persistent production scatter scene was created; placement density and overdraw budget still need review in #595/#596.
- Frame Debugger was not opened because issue-worktree Unity MCP was unavailable; source/material audit shows AlphaTest opaque-depth behavior with no transparent overdraw path, but runtime overdraw must still be reviewed in a real scene.
- Captures are generated from temporary preview geometry, not production foliage meshes or textures. The shader supports mask and vertex color weighting, but final authored assets must confirm mask channel conventions.
- Wind is static-captured through global shader values. Animated motion quality still needs live scene review after production placement.

## Validation

- `git diff --check`
- `git diff --cached --check`
- `Tools/Harness/run_lightweight_checks.sh`
- Unity GUI issue-worktree import/material/capture run, with `/tmp/issue593-foliage-generator.log` checked for shader errors and warnings.
- PNG non-empty check for all three captures.
- Material property audit for both approved samples.
