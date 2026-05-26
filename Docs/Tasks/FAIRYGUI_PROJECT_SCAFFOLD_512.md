# FairyGUI Project Scaffold (#512)

## Scope

This task created the framework-owned FairyGUI authoring project scaffold and
was later extended on `main` with the first smoke package and project-local
helper plugin.

- `FGUIProject/` is the FairyGUI Editor source project and lives outside Unity `Assets`.
- `FGUIProject/assets/` contains framework-owned FairyGUI source packages.
- `Assets/Bundles/FGUI/` is FairyGUI Editor publish output and should not be hand-authored.
- `FGUIProject/plugins/` contains project-local FairyGUI Editor helper plugins.

## Publish Settings

`FGUIProject/settings/Publish.json` is configured for Unity runtime assets:

- `fileExtension`: `bytes`
- `compressDesc`: `true`
- `binaryFormat`: `true`
- output pattern: `../Assets/Bundles/FGUI/{publish_file_name}`

The publish path is relative to the FairyGUI project directory so the repository can move between machines. If a FairyGUI Editor install resolves the path differently, set it explicitly on that machine, for example:

```text
/Users/vincent/Documents/WGameFramework/Assets/Bundles/FGUI/{publish_file_name}
```

## Current Non-Goals

- No atlas images, generated code, Common package, fonts, textures, WGame UI content, or FairyGUI Editor binaries are committed.
- Issue #510 navigator smoke tests and the M4 generator are out of scope.

## Current Smoke Package

The first framework smoke package now exists:

- source package: `FGUIProject/assets/MxFguiSmoke`
- component source: `FGUIProject/assets/MxFguiSmoke/SmokePanel.xml`
- bindable text object: `txtTitle`
- published runtime package: `Assets/Bundles/FGUI/MxFguiSmoke/MxFguiSmoke_fui.bytes`

`MxFguiSmoke_fui.bytes` is a FairyGUI publish output. It is checked in only as
the minimal framework smoke asset needed to unblock real adapter validation.
Do not hand-edit or synthesize this binary file.

## Agent Helper Plugin

The project now includes a FairyGUI Editor helper plugin:

```text
FGUIProject/plugins/wgameframework-agent-helper/
```

When `FGUIProject/FGUIProject.fairy` is open, it adds menu commands:

```text
WGameFramework/Create/Repair Smoke Package
WGameFramework/Publish Smoke Package
WGameFramework/Refresh Project
```

The FairyGUI Editor executable currently used on this machine is:

```text
..\FairyGUI-Editor\FairyGUI-Editor.exe
```

Use it to open `FGUIProject/FGUIProject.fairy`, then run the GUI menu commands
above. The previously documented `-script` command-line form is not a verified
plugin entry on this editor build; it only opens the editor welcome flow here.

## Follow-up Runtime Smoke

#510 uses the smoke package to validate `MxFairyGuiNavigator` with a real
FairyGUI package/component asset, including ViewModel bind, close and release.
