# FairyGUI M14 #544: Story Dialog Choice List UX

Status: implemented in `workpack-fairygui-ui-closure`.

## Scope

- Extends the Story vertical-slice snapshot/viewmodel from a single primary choice to an ordered choice list.
- Keeps legacy `ChoiceId`, `ChoiceText`, `ChoiceLocalizedText`, and `SelectChoice` command descriptor fields mapped to the first enabled choice for compatibility.
- Binds FairyGUI Story dialog choices as a runtime button list using the checked-in `btnChoice` control as the template.
- Does not change StoryDirector authority: FairyGUI emits `MxUiCommand`, and `StoryRuntimeVerticalSliceUiCommandSink` maps it to Story runtime commands.

## Notes

- `Continue` remains a separate complete-presentation command.
- Disabled Story choices render disabled and do not enqueue commands.
- Cancel/back is not enabled in this work package because the current Story vertical-slice rules expose continue/select only; there is no Story cancel/back command to map without adding new Story rules.
- `FGUIProject/assets/MxStoryDialog/*` and `Assets/Bundles/FGUI/MxStoryDialog/*` were not edited. The binder creates additional choice buttons at runtime, avoiding manual edits to FairyGUI binary exports.

## Validation Targets

```bash
dotnet build MxFramework.Demo.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
```
