# WGameFramework Agent Helper

Project-local FairyGUI Editor helper plugin.

## Menu

- `WGameFramework/Create/Repair Smoke Package`
- `WGameFramework/Publish Smoke Package`
- `WGameFramework/Create/Repair Runtime HUD Package`
- `WGameFramework/Publish Runtime HUD Package`
- `WGameFramework/Create/Repair Story Dialog Package`
- `WGameFramework/Publish Story Dialog Package`
- `WGameFramework/Refresh Project`

Use this from FairyGUI Editor after opening `FGUIProject/FGUIProject.fairy`.
The FairyGUI Editor GUI menu is the expected publish path for package bytes.
If the menu does not appear, open `Tools/Plugins` in FairyGUI Editor and reload
plugins, or restart FairyGUI Editor.

## Local Editor

The FairyGUI Editor executable currently used on this machine is:

```text
..\FairyGUI-Editor\FairyGUI-Editor.exe
```

Use it to open `FGUIProject/FGUIProject.fairy`, then run the menu commands above.
The FairyGUI Editor GUI menu is the expected publish path on this local editor
build.

## Batch Entry

Some FairyGUI Editor builds may support batch script entry points:

```bash
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script create-smoke
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script publish-smoke
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script create-runtime-hud
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script publish-runtime-hud
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script create-story-dialog
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script publish-story-dialog
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script refresh
```

The previously documented `-script` command-line form is not a verified plugin
entry on this editor build; it only opens the editor welcome flow here.

## Scope

The helper only targets framework-owned FairyGUI packages:

- source package: `FGUIProject/assets/MxFguiSmoke`
- component: `SmokePanel`
- bindable child: `txtTitle`
- source package: `FGUIProject/assets/MxRuntimeHud`
- component: `RuntimeHudPanel`
- bindable children: `title`, `mode`, `playerName`, `playerHp`, `enemyName`,
  `enemyHp`, `recentAction`, `btnStrike`, `btnReset`
- source package: `FGUIProject/assets/MxStoryDialog`
- component: `StoryDialogPanel`
- bindable children: `title`, `phase`, `dialogueText`, `choiceText`,
  `signalText`, `eventLog`, `btnContinue`, `btnChoice`
- publish output: configured by `FGUIProject/settings/Publish.json`

It intentionally does not create project-specific WGame UI, generated C# code,
fonts, images, or runtime integration code.
