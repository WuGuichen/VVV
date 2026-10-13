# ImpactDecal Validation

> Status: Current
>
> Issue: #594 `[Implementation] WGame ImpactDecal combat feedback`
>
> Shader: `Assets/Art/WGame/Shaders/Decal/ImpactDecal.shader`
>
> Material samples:
>
> - `Assets/Art/WGame/Materials/Decal/ImpactDecalHitMark.mat`
> - `Assets/Art/WGame/Materials/Decal/ImpactDecalCrackBreak.mat`
>
> Captures:
>
> - `Docs/Validation/WGameShaderFamily/ImpactDecal_Normal.png`
> - `Docs/Validation/WGameShaderFamily/ImpactDecal_Dissolve.png`
> - `Docs/Validation/WGameShaderFamily/ImpactDecal_ReducedBloom.png`

## Scope

#594 adds the first `WGame/ImpactDecal` shader family pass for short-lived hit marks, cracks, break feedback, and ground combat presentation. It does not add persistent scenes, prefabs, textures, models, runtime code, tests, public Rendering API, view adapters, bridge code, render passes, `MaterialBindingHub`, `MxMaterialChannel`, Combat authority, Gameplay authority, collision, occlusion, terrain data, Replay, SaveState, or Runtime hash dependencies.

The shader is a transparent URP material intended for small quad/projection-style decal samples. It is not a long-term terrain destruction system.

## API Reuse And Ownership

- Shared HLSL reuse: `WGameShaderCommon.hlsl`, `WGameStylizedLighting.hlsl`, `WGameFogBlend.hlsl`, `WGameEmission.hlsl`, and `WGameDissolve.hlsl`.
- Project material parameters use `_Wg*`: decal/crack textures, decal color, emission color/intensity, edge width, blend-to-stone, dissolve progress, lifetime fade, hit flash, depth fade, bloom budget, and debug overlay.
- Framework globals are read only through the documented `_Mx*` boundary: `_MxTime`, `_MxViewFocusWorldPos`, and `_MxLocalSubjectWorldPos` from the shared fog/global include.
- `_WgDissolveProgress`, `_WgLifetimeFade`, `_WgHitFlash*`, `_WgEmission*`, and `_WgEdgeWidth` are presentation/material state only. They do not write gameplay truth, Combat authority, bridge state, Replay, SaveState, Runtime hash, collision, or terrain data.
- `WGameRimLighting.hlsl` is intentionally not included. The decal is a transparent surface mark without a subject silhouette; rim lighting would make it read closer to an outline or UI sticker.

## Material Audit

Both approved material samples were generated through Unity `AssetDatabase.CreateAsset` in the issue worktree:

| Material | Shader | Key intent |
| --- | --- | --- |
| `ImpactDecalHitMark` | `WGame/ImpactDecal` | Short amber/ember hit mark with a restrained hit flash and lower blend-to-stone. |
| `ImpactDecalCrackBreak` | `WGame/ImpactDecal` | Darker crack/break mark with stronger stone blending and reduced flash. |

Material YAML was not hand-written. The final material files contain the generated shader reference and key `_WgDecalColor`, `_WgDecalTex`, `_WgCrackTex`, `_WgEmissionColor`, `_WgEmissionIntensity`, `_WgEdgeWidth`, `_WgBlendToStone`, `_WgDissolveProgress`, `_WgLifetimeFade`, and `_WgHitFlash*` properties.

## Capture Method

Unity MCP was probed first but was unavailable at `http://127.0.0.1:8080/mcp`, so no main-worktree Console, Game view, Test Runner, or Frame Debugger result is used as branch validation.

The captures and material samples were generated in the issue worktree with Unity GUI:

```text
Unity -projectPath /Users/vincent/Documents/WGameFramework.worktrees/issue-594 -executeMethod Issue594ImpactDecalGenerator.Generate -quit -logFile /tmp/issue594-impact-decal-generator.log
```

The temporary Editor generator created an unsaved preview scene, rendered the camera to PNG, generated the two approved material assets, then was removed before commit. The Unity log had no `Shader error` or `Shader warning` entries.

## Visual Review

### Normal

`ImpactDecal_Normal.png` places the hit mark and crack/break samples over #588-style dark stone with #593 foliage, #591 damaged prop, and #592 marker context. The decal color reads as short combat feedback while remaining lower priority than the marker and not duplicating HUD/HeadPad information.

### Dissolve

`ImpactDecal_Dissolve.png` raises `_WgDissolveProgress` and `_WgLifetimeFade` on runtime material instances for a static capture of fade-out behavior. Both decals visibly reduce coverage while keeping thin edge marks long enough to communicate the impact.

### Reduced Bloom

`ImpactDecal_ReducedBloom.png` lowers emission and bloom budget. The samples remain readable through crack shape, value contrast, and stone blending, not only through glow.

## Cross-Family Context

- #588 `EnvironmentStylizedLit`: Decals blend toward dark stone with `_WgBlendToStone` so they sit on the surface instead of reading as UI stickers.
- #589 `MagicVfxUnlit`: ImpactDecal uses much lower emission area and intensity than magic VFX.
- #590 review scene: This validation keeps the same shader-review stance and does not modify the scene or its captures.
- #591 `PropStylizedLit`: Hit flash and crack/break language aligns with destructible prop presentation, but #594 does not bind runtime channels or prop state.
- #592 `WorldMarkerUnlit`: Marker diamonds remain higher spatial priority than decals.
- #593 `FoliageMossLit`: Decals do not modify foliage placement, collision, or occlusion; they only share nearby environment context in the capture.

## Transparent, Sorting, And Overdraw

The shader is transparent:

- Tags: `RenderType=Transparent`, `Queue=Transparent`
- Blend: `SrcAlpha OneMinusSrcAlpha`
- `ZWrite Off`
- `Cull Off`
- Small procedural shape masks and material alpha limit decal screen area.

Source/material audit conclusion:

- The samples use two small transparent quads, not broad filled rings or screen-covering VFX.
- Decal meaning is carried by crack/burst shape and edge value, so reduced bloom still works.
- Sorting can still fail if many decals overlap on the same surface or intersect other transparent VFX/markers.
- Overdraw and sorting must be reviewed in #595/#596 with production placement density and Frame Debugger access.

Frame Debugger was not opened because issue-worktree Unity MCP was unavailable. Main-worktree Frame Debugger/Console was not used as branch evidence.

## Lifetime / Fade / Dissolve Risk

- `_WgDissolveProgress` and `_WgLifetimeFade` are material presentation parameters only.
- A future runtime view adapter may drive these values through existing `DissolveProgress` or short `HitFlash` semantics, but #594 does not add that adapter or any channel.
- Long-lived decals should be avoided until budget/placement rules are reviewed; this family is authored for short hit/break feedback.
- If future combat integration needs independent lifetime, stacking, pooling, or channel ownership, record a follow-up issue instead of overloading `BridgeCustom`.

## Style Risk

- Amber/ember is limited to edge and flash accents so it does not look like an interactable or marker.
- Broad transparent circles and UI-like rings are avoided; the procedural shape uses angular crack/burst marks.
- The captures use temporary preview geometry, not production WGame meshes, placement rules, or imported decal textures.

## Validation

- `git diff --check`
- `git diff --cached --check`
- `Tools/Harness/run_lightweight_checks.sh`
- Unity GUI issue-worktree import/material/capture run, with `/tmp/issue594-impact-decal-generator.log` checked for shader errors and warnings.
- PNG non-empty check for all three captures.
- Material property audit for both approved samples.
