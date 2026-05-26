# FairyGUI Runtime HUD Production Shell (#527)

## Goal

Promote the current `MxRuntimeHud` FairyGUI proof into a reusable runtime HUD
shell for the Runtime Ability Slice.

This slice keeps the runtime HUD ViewModel and command ownership
technology-neutral. FairyGUI owns presentation, lifecycle and package loading;
the demo runtime owns command validation and execution.

## Shell Contract

`RuntimeAbilitySliceFairyGuiHudShell` is the production-oriented composition
surface for this slice. It combines:

- generated `RuntimeAbilitySliceFairyGuiHudManifest`
- `MxFairyGuiNavigator`
- `MxFairyGuiResourceBridge`
- optional `IMxFairyGuiLayerHost`
- optional `IMxFairyGuiInputContextBridge`
- `RuntimeAbilitySliceUiCommandSink`

The shell exposes:

- `Open(RuntimeAbilitySliceHudViewModel)`
- `Refresh(RuntimeAbilitySliceHudViewModel)`
- `OpenFrom(RuntimeAbilitySliceRunner)`
- `OpenAsync(RuntimeAbilitySliceHudViewModel)`
- `Close()`
- command result counters and the last accepted/rejected command result

`Refresh` intentionally routes through `MxFairyGuiNavigator.Open`, so an already
open HUD is rebound through the same productized lifecycle path instead of
mutating FairyGUI controls directly.

## Command Ownership

FairyGUI buttons emit `MxUiCommand` through `RuntimeAbilitySliceFairyGuiHudBinder`.
`RuntimeAbilitySliceUiCommandSink` maps those command ids to
`RuntimeAbilitySliceHudManualCommand` and calls the runtime command target.

The binder and shell do not directly mutate gameplay state. Strike and Reset
remain runtime commands:

- `runtimeHud.strike`
- `runtimeHud.reset`

## Validation

Local gates for this shell are:

```bash
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
dotnet build MxFramework.Demo.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

The `RuntimeAbilitySliceFairyGuiHudBinderTests` suite covers:

- generated manifest validation against source XML and package bytes
- opening the published package through `MxFairyGuiNavigator`
- rebinding an already open HUD through shell refresh
- dispatching Strike through `MxUiCommand`
- closing and releasing loaded resources and FairyGUI package registration

## UI Toolkit Coexistence

UI Toolkit `MxRuntimeHudController` remains the Ability Showcase and diagnostics
surface. It is broader than the FairyGUI Runtime HUD proof: it has detailed
diagnostics, event logs and manual controls that are not part of this FairyGUI
shell.

FairyGUI is the preferred path for formal player-facing runtime HUDs. UI
Toolkit remains valid for existing showcase/debug surfaces until a dedicated
migration issue replaces a specific surface.

## Non-Goals

- Do not migrate Story, Debug UI or all Ability Showcase controls.
- Do not generate final art.
- Do not move command authority into FairyGUI binders.
- Do not replace UI Toolkit diagnostics in this slice.
