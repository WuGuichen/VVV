# FairyGUI Project Scaffold (#512)

## Scope

This task creates the framework-owned FairyGUI authoring project scaffold only.

- `FGUIProject/` is the FairyGUI Editor source project and lives outside Unity `Assets`.
- `FGUIProject/assets/` is intentionally empty until the first framework package is authored.
- `Assets/Bundles/FGUI/` is reserved for FairyGUI Editor publish output and should not be hand-authored.

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

- No FairyGUI package or component is created in this scaffold.
- No `*_fui.bytes`, atlas images, generated code, Common package, fonts, textures, WGame UI content, or FairyGUI Editor binaries are committed.
- Issue #510 navigator smoke tests and the M4 generator are out of scope.

## Next Manual Authoring Step

Open `FGUIProject/FGUIProject.fairy` in FairyGUI Editor and create the first smoke package manually:

- package: `MxFguiSmoke`
- component: `SmokePanel`
- text object: `txtTitle`

Publishing that package should generate runtime files under `Assets/Bundles/FGUI/`; those generated files belong to a later task.
