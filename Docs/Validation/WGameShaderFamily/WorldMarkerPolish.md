# World Marker Polish Validation

> Status: Archive
>
> Issue: #592
>
> Shader: `Assets/Art/WGame/Shaders/Marker/WorldMarkerUnlit.shader`
>
> Captures:
> - `Docs/Validation/WGameShaderFamily/WorldMarkerPolish_NearReducedBloom.png`
> - `Docs/Validation/WGameShaderFamily/WorldMarkerPolish_FarDarkBackground.png`

## Scope

#592 polishes the existing `WGame/WorldMarkerUnlit` family. It does not add a new shader family, persistent scene, prefab, texture, model, runtime code, tests, view adapter, HeadPad runtime, HUD runtime, UI Toolkit code, public Rendering API, `MxMaterialChannel`, `MaterialBindingHub`, renderer feature, gameplay authority, Replay, SaveState, or Runtime hash dependency.

The approved material samples are:

- `WorldMarkerTargetDiamond` for target lock.
- `WorldMarkerQuestBeacon` for task beacon.
- `WorldMarkerInteractCorners` for interaction-position hints.

## Shader / Material Audit

- Shared HLSL reuse is preserved through `WGameShaderCommon.hlsl`, `WGameFogBlend.hlsl`, `WGameEmission.hlsl`, and `WGameDissolve.hlsl`.
- Marker properties remain under `_Wg*`.
- The shader reads documented framework globals only through `_MxTime` and `_MxViewFocusWorldPos`.
- No new `_Mx*` ownership, shader id, material channel, or Rendering public API was added.
- The polish adds authored marker controls for inner fill, footprint scale, bloom budget, and dark-background boost so material samples can keep small angular shapes readable without using broad transparent rings.

## HeadPad / HUD Boundary

WorldMarker owns spatial intent only:

- Target lock position and direction.
- Task beacon position.
- Interaction hint position.

HeadPad/HUD own persistent and textual information:

- Actor names and status labels.
- Health, resources, cooldowns, objectives, inventory, and other persistent UI state.
- Any text or state explanation that would become noisy if duplicated in world markers.

#592 keeps the marker language to diamond, split beacon, and corner brackets. It does not encode names, status text, health, quest text, or HUD-style state into material variants.

## Capture Method

Unity GUI was used against the issue-592 worktree:

```text
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -projectPath /Users/vincent/Documents/WGameFramework.worktrees/issue-592 \
  -executeMethod Issue592WorldMarkerPolishGenerator.Generate \
  -quit \
  -logFile /tmp/issue592-world-marker-polish.log
```

No `-batchmode` flag was used. The temporary Editor script imported the shader, updated the existing target/interact materials, created the approved quest beacon material with `AssetDatabase.CreateAsset`, rendered two captures from a temporary unsaved preview scene camera, and was deleted before commit.

## Near / Reduced Bloom Result

`WorldMarkerPolish_NearReducedBloom.png` uses no post-process bloom volume. This is the reduced-bloom review baseline: marker meaning must come from shape and edge value, not glare.

Observed result:

- Target lock reads as a compact diamond with a small center pip.
- Quest beacon reads as a split diamond / narrow beacon, distinct from target lock.
- Interaction hint reads as four corner brackets.
- The static HeadPad-style space above the silhouette remains visually separate.
- The marker set uses amber/bronze/parchment edge values without a large transparent circle.

## Far / Dark Background Result

`WorldMarkerPolish_FarDarkBackground.png` places the same three marker states over a dark ruin band and a small #591-style prop focus reference.

Observed result:

- All three marker states remain visible at the far review scale.
- The quest beacon remains distinguishable from target lock by its vertical split shape.
- The corner marker stays less dominant than target lock.
- The prop reference retains its own object-readability space; the marker samples do not make every prop read as active focus.

## #590 / #591 Context Comparison

#590 established that reduced-bloom marker readability should be carried by diamond/corner shape and edge value. #592 follows that rule by adding a task-beacon shape between target diamond and interact corners.

#591 added prop focus language. The marker samples keep stronger spatial priority than passive prop bronze/glow, while `WorldMarkerInteractCorners` is intentionally less intense than target lock so it can sit near interactable props without stealing all focus.

## Overdraw / Frame Debugger

Frame Debugger was not run because Unity MCP did not expose the issue-592 worktree; the MCP endpoint was unavailable at `http://127.0.0.1:8080/mcp`.

Source and capture audit conclusion:

- Marker geometry remains small transparent quads.
- The shader avoids broad filled rings and large hologram surfaces.
- The far capture keeps marker screen area compact.
- Residual risk remains for live Frame Debugger overdraw inspection in an issue-worktree Unity MCP session.

## Bloom Risk

Bloom risk is reduced by:

- `_WgBloomBudget` clamping emission intensity and max intensity per material.
- Lower marker intensity/pulse on interact and quest variants than a full VFX effect.
- Shape-first readability in the reduced-bloom capture.

Residual risk: a production post-process profile could still make amber markers too dominant. #596 should review the final marker set in the full shader family scene with the active WGame bloom profile.

## Material Binding Risk

No runtime `MaterialBindingHub` writes are performed in this issue.

Expected presentation mapping remains:

- `_WgMarkerIntensity` and `_WgMarkerPulse` can be driven by a WGame marker view adapter when one is approved.
- `_WgDissolveProgress` remains suitable for appear/disappear.
- `_WgOutlineColor` and `_WgOutlineAmount` remain compatible with `OutlineState` only when a subject-bound marker needs that semantic.
- `_WgDebugOverlayColor` and `_WgDebugOverlayAmount` are diagnostics only.

Risk: if a future marker view adapter needs marker pulse independent from `OutlineState` or `DissolveProgress`, record a follow-up typed channel decision instead of overloading `BridgeCustom`.

## Style Risk

- The task beacon is intentionally a split diamond / narrow beacon, not a broad quest ring.
- Marker samples use generic spatial intent only; there is no quest text, nameplate, or HUD state in the marker material.
- The captures use primitives and temporary scene composition for validation, not production WGame meshes.
- The UI Kit remains a style reference; no raster UI kit assets were imported into Unity.

## Unity MCP / Console Status

- Attempted Unity MCP discovery for the issue worktree.
- `mcpforunity://instances`, `mcpforunity://custom-tools`, and `mcpforunity://editor/state` failed because `http://127.0.0.1:8080/mcp` was unavailable.
- Branch Unity MCP Console validation, material preview, and Frame Debugger were not executed.
- Main worktree Console was not used as branch evidence.
- Unity GUI import/generation log contained no `Shader error` or `Shader warning` entries for this issue. Existing unrelated package/USS warnings were present.

## Play Mode Smoke Status

Play Mode smoke was not executed. This issue is a static shader/material/capture validation slice with no gameplay loop, input path, runtime marker view adapter, HeadPad runtime, or HUD runtime.
