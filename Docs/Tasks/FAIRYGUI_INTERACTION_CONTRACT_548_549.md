# FairyGUI Interaction Contract (#548, #549)

## Goal

Productize adapter-level FairyGUI transition close/cancel semantics and
keyboard/gamepad focus navigation without moving input authority into views.

## Contract

- `MxFairyGuiViewTransitionController` owns adapter show/hide transition starts
  and cancellation. `Close` / `Dispose` cancels any active transition before
  component/package/input cleanup.
- `MxFairyGuiFocusNavigationMetadata` declares per-view default focus and
  ordered next/previous focus targets.
- `MxFairyGuiFocusInputBridge` is called by composition roots after framework
  input has produced UI intent. FairyGUI views do not poll devices.
- Submit triggers the focused button's normal `onClick` path, so commands still
  flow through binders and `IMxUiCommandSink`.
- Cancel is routed through `MxFairyGuiCancelCommandBridge` as `ui.cancel` for
  the top modal view.
- Modal focus submit/navigation is blocked for lower views while a modal is
  active; modal command filtering remains enforced by `MxFairyGuiModalCommandGate`.

## Boundaries

- No `MxFramework.UI.FairyGUI.Manifest` generator or noEngine tooling changes.
- No `FGUIProject/**` or `Assets/Bundles/FGUI/**` publish output changes.
- No direct keyboard/gamepad polling in FairyGUI views or binders.
- No gameplay, Story, or Runtime authority moves into FairyGUI.

## Validation

```bash
dotnet build MxFramework.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Demo.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

Focused coverage:

- transition close cancels before resource/component/package release;
- Runtime HUD default focus, next/previous and submit;
- Story dialog default focus skips disabled actions;
- focus input bridge does not let lower views submit while a modal is active;
- cancel emits `ui.cancel` for the top modal and passes through modal gate.
