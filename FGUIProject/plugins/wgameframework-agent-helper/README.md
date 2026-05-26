# WGameFramework Agent Helper

Project-local FairyGUI Editor helper plugin.

## Menu

- `WGameFramework/Create/Repair Smoke Package`
- `WGameFramework/Publish Smoke Package`
- `WGameFramework/Refresh Project`

Use this from FairyGUI Editor after opening `FGUIProject/FGUIProject.fairy`.
If the menu does not appear, open `Tools/Plugins` in FairyGUI Editor and reload
plugins, or restart FairyGUI Editor.

## Local Editor

The FairyGUI Editor executable currently used on this machine is:

```text
..\FairyGUI-Editor\FairyGUI-Editor.exe
```

Use it to open `FGUIProject/FGUIProject.fairy`, then run the menu commands above.
The previously documented `-script` command-line form is not a verified plugin
entry on this editor build; it only opens the editor welcome flow here.

## Scope

The helper only targets the framework smoke package:

- source package: `FGUIProject/assets/MxFguiSmoke`
- component: `SmokePanel`
- bindable child: `txtTitle`
- publish output: configured by `FGUIProject/settings/Publish.json`

It intentionally does not create project-specific WGame UI, generated C# code,
fonts, images, or runtime integration code.
