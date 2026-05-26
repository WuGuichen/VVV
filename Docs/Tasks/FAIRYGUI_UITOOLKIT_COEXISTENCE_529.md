# FairyGUI And UI Toolkit Coexistence Cleanup (#529)

## Goal

Close the FairyGUI productization gate by making the coexistence policy
explicit: FairyGUI is the preferred adapter for formal player-facing runtime UI,
while UI Toolkit remains the default for debug, editor, showcase and validation
surfaces until a specific replacement issue says otherwise.

This slice does not rewrite UI Toolkit surfaces and does not remove debug
overlays.

## Current Surface Inventory

### UI Toolkit Stays By Design

| Surface | Current files | Reason |
| --- | --- | --- |
| Debug UI overlay | `Assets/Scripts/MxFramework/DebugUI.Toolkit/`, `Assets/Scripts/MxFramework/DebugUI.Input/`, `Docs/Interfaces/DebugUI.md` | Development diagnostics are read-only by default and should stay independent from formal runtime UI skinning. |
| Editor tools | `Assets/Scripts/MxFramework/*Editor*/`, `Docs/Interfaces/Editor.md`, `Docs/Interfaces/Story.Editor.md` | Unity editor surfaces use UI Toolkit by convention; FairyGUI is not an editor tooling dependency. |
| Runtime Ability Showcase diagnostics | `Assets/Scripts/MxFramework/Demo/RuntimeVerticalSliceRunner.cs`, `Assets/Scripts/MxFramework/Demo/Ability/RuntimeAbilitySliceShowcaseUi.cs`, `Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml` | The showcase exposes config, patch, diagnostics, event log and manual validation controls. FairyGUI Runtime HUD is the player-facing path, not a replacement for diagnostics. |
| Story vertical slice | `Assets/Scripts/MxFramework/Demo/Story/StoryRuntimeVerticalSliceRunner.cs`, `Assets/UI/MxFramework/Story/StoryRuntimeVerticalSlice.uxml`, `Docs/Tasks/FAIRYGUI_STORY_UI_MIGRATION_READINESS_528.md` | Story FairyGUI UI is not implemented yet; the UI Toolkit slice remains fallback and diagnostics. |
| Playable demos and validation HUDs | `Assets/UI/MxFramework/Tetris/`, `Breakout/`, `MarbleMaze/`, `CharacterControl/`, `CombatAnimationHud.*`, `MxAnimationShowcase/`, `MxAnimationSmoke/`, `RenderingDemoSlices/`, `CameraUi3DValidation/` | These surfaces validate runtime systems and expose technical diagnostics; migrating them would be separate feature work. |
| Legacy OnGUI fallback | `RuntimeVerticalSliceRunner.OnGUI`, `RuntimeAbilitySliceRunner.OnGUI` | Temporary fallback diagnostics only; not a formal UI target. |

The validation/showcase bucket currently includes:

- Runtime Combat Showcase and Combat Animation HUD.
- MxAnimation Smoke and MxAnimation System Showcase.
- Rendering Demo Slices Showcase.
- UI Camera 3D Validation.
- Gameplay Component Runtime Showcase.
- Character Control Playable.
- Tetris, Breakout and Marble Maze playable demos.

Debug UI overlay is a development observation layer. It may be stacked over a
scene for debugging, but it is not a player HUD and does not participate in
FairyGUI ownership decisions.

### FairyGUI Formal Runtime Path

| Surface | Current files | Status |
| --- | --- | --- |
| Runtime Ability HUD | `FGUIProject/assets/MxRuntimeHud`, `Assets/Bundles/FGUI/MxRuntimeHud/`, `Assets/Scripts/MxFramework/Demo/FairyGUI/` | Productized through #527 as an opt-in player-facing HUD shell. |
| Future Story dialog / choices | `Docs/Tasks/FAIRYGUI_STORY_UI_MIGRATION_READINESS_528.md` | Candidate after a dedicated implementation issue creates package, manifest, binder, shell and smoke path. |
| Future player-facing panels, popups and modals | none yet | Candidate only after a scoped issue defines ViewModel, commands, resources, manifest and validation. |

## Ownership Rules

- A runtime scene must not imply that FairyGUI is mandatory for debug, editor or
  validation surfaces.
- A formal player-facing HUD or panel should use FairyGUI after it has a
  manifest, command contract, resource catalog and smoke validation.
- A diagnostics/showcase surface may continue using UI Toolkit even when a
  FairyGUI player HUD exists for the same runtime system.
- When both surfaces exist, the composition root must keep them opt-in and
  visibly separate:
  - FairyGUI HUD: player-facing path.
  - UI Toolkit HUD: diagnostics/showcase path.
  - Debug UI overlay: development observation path.
- Do not stack a FairyGUI HUD and a UI Toolkit HUD as two default player HUDs in
  the same scene. One may be active by default only if the other is explicitly
  disabled or documented as diagnostics.
- UI Toolkit UXML/USS and PanelSettings are authored or generated through Unity
  tooling. Do not hand-author `.unity` or `.asset` YAML to force coexistence.

## Migration Candidate Policy

A UI Toolkit surface becomes a FairyGUI migration candidate only when a Gitea
issue names the target surface and includes:

- ViewModel and command ownership
- target FairyGUI package and generated manifest path
- resource catalog entry for package bytes
- coexistence or replacement decision for the current UI Toolkit surface
- validation commands and runtime smoke

Current candidates:

- Runtime Ability HUD: #527 completed the reusable FairyGUI shell. UI Toolkit
  `MxRuntimeHudController` remains diagnostics/showcase.
- Story dialog and choice UI: #528 defines readiness; #537 owns the future
  FairyGUI package, manifest, binder, shell and smoke path before any UI
  Toolkit Story surface is replaced.

Current non-candidates:

- Debug UI overlay and Debug UI sources.
- Unity editor windows and inspectors.
- Runtime validation scenes whose purpose is to expose diagnostics or
  framework-internal state.
- Demo HUDs that are still validating runtime contracts rather than presenting
  final game UX.

## Validation

#529 is a docs and boundary cleanup. Local validation is:

```bash
git diff --check -- Docs/Tasks/FAIRYGUI_UITOOLKIT_COEXISTENCE_529.md Docs/Tasks/FAIRYGUI_PRODUCTIZATION_GATE_520.md Docs/USAGE.md Docs/CAPABILITIES.md Docs/Interfaces/UI.Toolkit.md
rg -n "FairyGUI|Fgui|FairyGui" Assets/Scripts/MxFramework/Core Assets/Scripts/MxFramework/Runtime Assets/Scripts/MxFramework/Gameplay Assets/Scripts/MxFramework/Combat Assets/Scripts/MxFramework/Story -g '*.cs' -g '*.asmdef'
```

The dependency scan should produce no matches. It proves the documentation
cleanup did not turn FairyGUI into a dependency of pure runtime modules.

## Non-Goals

- Do not rewrite UI Toolkit surfaces.
- Do not remove Debug UI overlays.
- Do not migrate Story UI, Debug UI or playable demo HUDs.
- Do not create or edit FairyGUI packages.
- Do not change scene assets or PanelSettings in this issue.
