# FairyGUI Localization Provider Adapter And Refresh Smoke (#542)

## Goal

Provide a composition-level localization provider path for Runtime HUD and Story
Dialog FairyGUI shells without coupling UI core, FairyGUI adapter code or
generated manifests to `MxFramework.Config`.

## Contract

- UI core continues to expose provider-neutral `MxUiTextKey`, `MxUiLocaleId`,
  `MxUiLocalizedTextRequest` and `IMxUiTextProvider`.
- `MxFramework.UI.FairyGUI` and `MxFramework.UI.FairyGUI.Manifest` do not
  reference Config.
- `MxFramework.Demo.FairyGUI` accepts an optional `IMxUiTextProvider` in Runtime
  HUD and Story Dialog composition methods.
- `MxDelegateUiTextProvider` is the thin composition adapter for real or test
  providers. A project-level Config provider can be wrapped by mapping
  `MxUiTextKey.Value` and `MxUiLocaleId.Value` to its own key / locale types.
- Shell `Refresh(...)` routes through `MxFairyGuiNavigator.Open(...)`, so an
  already open view is rebound against the current provider revision / locale.

Example composition mapping for a Config-backed project:

```csharp
var uiTextProvider = new MxDelegateUiTextProvider(
    (key, locale, out string text) => localizationProvider.TryGetText(
        new LocalizedTextKey(key.Value),
        new LocaleId(locale.Value),
        out text),
    new MxUiLocaleId("en-US"));
```

## Smoke Coverage

- Runtime HUD shell opens the published package, receives an injected provider,
  changes locale, refreshes, reuses the same view instance and updates title /
  mode text.
- Story Dialog shell opens the published package, receives an injected provider,
  changes locale, refreshes, reuses the same view instance and updates title /
  dialogue / choice text.

## Validation

```bash
dotnet build MxFramework.Demo.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
```

## Non-Goals

- No Story runtime/core changes.
- No FairyGUI package publish or source XML edits.
- No global localization singleton.
- No Config dependency in UI core, FairyGUI adapter or FairyGUI manifest
  assembly.
