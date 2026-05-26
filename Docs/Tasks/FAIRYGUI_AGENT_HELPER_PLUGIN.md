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
- create or repair the `MxRuntimeHud` package source
- publish the `MxRuntimeHud` package
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
WGameFramework/Create/Repair Runtime HUD Package
WGameFramework/Publish Runtime HUD Package
WGameFramework/Refresh Project
```

If the menu is missing, reload FairyGUI plugins from the editor plugin panel or
restart FairyGUI Editor after opening the project.

The FairyGUI Editor GUI menu is the expected publish path for runtime package
bytes. Do not handwrite `Assets/Bundles/FGUI/MxRuntimeHud/*_fui.bytes`; publish
the source package through `WGameFramework/Publish Runtime HUD Package`.

## Batch Entry

The plugin exposes these script commands for FairyGUI Editor command-line script
mode when available:

```bash
FairyGUI-Editor -p FGUIProject/FGUIProject.fairy -script create-smoke
FairyGUI-Editor -p FGUIProject/FGUIProject.fairy -script publish-smoke
FairyGUI-Editor -p FGUIProject/FGUIProject.fairy -script create-runtime-hud
FairyGUI-Editor -p FGUIProject/FGUIProject.fairy -script publish-runtime-hud
FairyGUI-Editor -p FGUIProject/FGUIProject.fairy -script refresh
```

The local non-professional command-line publish path has been unreliable so far,
so the GUI menu path is the expected workflow until that is resolved.

Observed local behavior:

- `package.json`, `main.js`, source XML, and `Publish.json` pass static checks.
- `MxFguiSmoke_fui.bytes` has a valid `FGUI` header and contains `mxfgui0` /
  `MxFguiSmoke`.
- The command-line `-script create-smoke` path currently hangs in this local
  editor setup and is not the recommended path.

## Boundaries

The helper is intentionally limited to framework-owned FairyGUI assets:

- source package: `FGUIProject/assets/MxFguiSmoke`
- component: `SmokePanel`
- bindable child: `txtTitle`
- source package: `FGUIProject/assets/MxRuntimeHud`
- component: `RuntimeHudPanel`
- bindable children: `title`, `mode`, `playerName`, `playerHp`, `enemyName`,
  `enemyHp`, `recentAction`, `btnStrike`, `btnReset`
- publish output: `Assets/Bundles/FGUI/MxFguiSmoke`
  and `Assets/Bundles/FGUI/MxRuntimeHud`

It must not create WGame business UI, generated C# UI bindings, global UI
manager code, fonts, images, or Story/HUD migration code.
