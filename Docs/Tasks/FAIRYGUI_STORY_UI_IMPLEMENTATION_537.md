# FairyGUI Story UI Implementation (#537)

## Goal

Implement a player-facing FairyGUI adapter for the Story runtime vertical slice
without replacing the existing UI Toolkit diagnostics surface.

## Scope

- Add a framework-owned FairyGUI source package for Story dialog and choice UI.
- Add generated manifest coverage for the Story package, controls,
  localization bindings, commands and package bytes resource key.
- Add a Demo/FairyGUI adapter that:
  - builds a ViewModel from `StoryRuntimeVerticalSliceSnapshot`;
  - binds dialog, phase, signal and event-log text to FairyGUI controls;
  - emits provider-neutral `MxUiCommand` values for continue and choice;
  - maps those UI commands to `StoryRuntimeCommandFactory`.
- Keep Story runtime, Story config and Story bridge assemblies free of
  FairyGUI references.
- Keep the UI Toolkit Story runner as diagnostics and fallback surface.

## Boundaries

`MxFramework.Demo.FairyGUI` owns the FairyGUI Story presentation adapter. It may
reference FairyGUI, `MxFramework.UI.FairyGUI`, `MxFramework.Demo` and Story
runtime types through the existing Demo assembly boundary. Story runtime
assemblies must not reference FairyGUI, the FairyGUI manifest sidecar or Demo
FairyGUI code.

FairyGUI package bytes are editor output. Source XML and manifests are reviewed
in git; `*_fui.bytes` must be regenerated through FairyGUI Editor or the local
helper plugin after source package changes.

## Minimum Slice

1. Source package: `FGUIProject/assets/MxStoryDialog`.
2. Published output path: `Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes`.
3. View id: `ui.story.dialog`.
4. ViewModel: `StoryRuntimeVerticalSliceFairyGuiViewModel`.
5. Commands:
   - `story.dialog.continue` -> `StoryRuntimeCommandFactory.CompletePresentation`.
   - `story.dialog.selectChoice` -> `StoryRuntimeCommandFactory.SelectChoice`.
6. Runtime smoke:
   - constructed-component binder tests;
   - shell test over published bytes once the package is exported;
   - noEngine manifest/source/generated-output validation.

## Validation

```bash
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
dotnet build MxFramework.Demo.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
rg -n "FairyGUI|Fgui|FairyGui" Assets/Scripts/MxFramework/Story Assets/Scripts/MxFramework/Story.Runtime Assets/Scripts/MxFramework/Story.Config Assets/Scripts/MxFramework/Story.Unity Assets/Scripts/MxFramework/Story.GameplayBridge -g '*.cs' -g '*.asmdef'
```
