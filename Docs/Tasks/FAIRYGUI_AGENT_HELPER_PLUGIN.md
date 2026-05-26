# FairyGUI Agent Helper Plugin

`FGUIProject/plugins/wgameframework-agent-helper` is a project-local FairyGUI
Editor plugin for small, repeatable agent operations inside the framework
FairyGUI project.

## Why This Exists

Some FairyGUI operations must be performed by FairyGUI Editor rather than by
hand-editing files. Source files such as `package.xml` and component XML can be
created from text, but runtime package files such as `*_fui.bytes` are binary
publish outputs and must come from FairyGUI's publisher.

The helper narrows that editor dependency to explicit commands:

- create or repair the `MxFguiSmoke` package source
- publish the `MxFguiSmoke` package
- refresh the open FairyGUI project

Current checked-in smoke output:

```text
Assets/Bundles/FGUI/MxFguiSmoke/MxFguiSmoke_fui.bytes
```

This file is a FairyGUI publish output. Do not hand-edit or regenerate it by
ad hoc binary writing; use FairyGUI Editor or this helper plugin.

## Usage

Open `FGUIProject/FGUIProject.fairy` in FairyGUI Editor, then use:

```text
WGameFramework/Create/Repair Smoke Package
WGameFramework/Publish Smoke Package
WGameFramework/Refresh Project
```

If the menu is missing, reload FairyGUI plugins from the editor plugin panel or
restart FairyGUI Editor after opening the project.

## Local Editor Entry

The FairyGUI Editor executable currently used on this machine is:

```text
..\FairyGUI-Editor\FairyGUI-Editor.exe
```

Use it to open `FGUIProject/FGUIProject.fairy`, then run the GUI menu commands
listed above. The previously documented `-script` command-line form is not a
verified plugin entry on this editor build; it only opens the editor welcome
flow here.

Observed local behavior:

- `package.json`, `main.js`, source XML, and `Publish.json` pass static checks.
- `MxFguiSmoke_fui.bytes` has a valid `FGUI` header and contains `mxfgui0` /
  `MxFguiSmoke`.
- GUI menu commands are the verified workflow for this plugin.
- The `-script` command-line form is not accepted as proof of plugin execution
  in this local editor setup.

## Boundaries

The helper is intentionally limited to framework smoke assets:

- source package: `FGUIProject/assets/MxFguiSmoke`
- component: `SmokePanel`
- bindable child: `txtTitle`
- publish output: `Assets/Bundles/FGUI/MxFguiSmoke`

It must not create WGame business UI, generated C# UI bindings, global UI
manager code, fonts, images, or Story/HUD migration code.
