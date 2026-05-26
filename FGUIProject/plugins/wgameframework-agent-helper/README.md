# WGameFramework Agent Helper

Project-local FairyGUI Editor helper plugin.

## Menu

- `WGameFramework/Create/Repair Smoke Package`
- `WGameFramework/Publish Smoke Package`
- `WGameFramework/Create/Repair Runtime HUD Package`
- `WGameFramework/Publish Runtime HUD Package`
- `WGameFramework/Refresh Project`

Use this from FairyGUI Editor after opening `FGUIProject/FGUIProject.fairy`.
The FairyGUI Editor GUI menu is the expected publish path for package bytes.
If the menu does not appear, open `Tools/Plugins` in FairyGUI Editor and reload
plugins, or restart FairyGUI Editor.

## Batch Commands

The plugin also exposes batch commands for FairyGUI Editor command-line script
execution when that editor mode is available:

```bash
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script create-smoke
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script publish-smoke
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script create-runtime-hud
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script publish-runtime-hud
FairyGUI-Editor -p /path/to/FGUIProject/FGUIProject.fairy -script refresh
```

Known local limitation: the currently available FairyGUI Editor command-line
batch/publish path has been unreliable on this machine, so the menu workflow is
the primary path for now.

## Scope

The helper only targets framework-owned FairyGUI packages:

- source package: `FGUIProject/assets/MxFguiSmoke`
- component: `SmokePanel`
- bindable child: `txtTitle`
- source package: `FGUIProject/assets/MxRuntimeHud`
- component: `RuntimeHudPanel`
- bindable children: `title`, `mode`, `playerName`, `playerHp`, `enemyName`,
  `enemyHp`, `recentAction`, `btnStrike`, `btnReset`
- publish output: configured by `FGUIProject/settings/Publish.json`

It intentionally does not create project-specific WGame UI, generated C# code,
fonts, images, or runtime integration code.
