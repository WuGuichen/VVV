# FairyGUI Localization Binding Contract (#525)

## Goal

Define the first productized localization seam for FairyGUI without coupling
UI, FairyGUI adapter code or generated manifests to `MxFramework.Config`.

This slice makes localization keys visible in noEngine UI contracts and
validation. It does not load language tables or migrate all Runtime HUD copy.

## Core Contract

`MxFramework.UI` owns provider-neutral text identity:

- `MxUiTextKey`
- `MxUiLocaleId`
- `MxUiLocalizedTextRequest`
- `IMxUiTextProvider`
- `MxUiNullTextProvider`

These types are pure UI core types. They do not reference Config, Story,
Gameplay, FairyGUI, Unity or global singletons.

`IMxUiTextProvider.Revision` is the refresh hook. Adapter composition can compare
revision values or rebind a view after a locale change. The UI core does not
define where locale data is stored.

## Manifest Contract

FairyGUI manifests can declare localized text controls through
`MxFairyGuiLocalizedTextBinding`:

```csharp
new MxFairyGuiLocalizedTextBinding(
    controlName: "title",
    textKey: "ui.runtimehud.title",
    fallbackText: "Runtime HUD")
```

The binding declares:

- the FairyGUI text-like control that receives localized text
- the provider-neutral key to request
- optional fallback text for missing providers or missing entries
- whether the key is required

`MxFairyGuiManifestGenerator` accepts `LocalizedTexts` in
`MxFairyGuiManifestGenerationSpec` and emits the same metadata into generated
manifest source. The checked-in Runtime HUD generated manifest currently
declares title and mode localization keys as the first smoke target.

## Validation

`MxFairyGuiManifestValidator` now reports structured localization diagnostics:

- `LocalizationKeyMissing`
- `LocalizationControlMissing`
- `LocalizationControlUnknown`
- `LocalizationControlNotText`
- `LocalizationDuplicate`

Validation checks both authored manifest metadata and FairyGUI source XML drift.
Localization bindings must point at named text-like controls such as `Text`,
`GTextField`, `GTextInput` or `GLabel`.

## Ownership Rules

ViewModels own runtime state and may expose literal text or plain string keys.
They must not expose `Config.LocalizedTextKey`.

Manifests own UI binding metadata. They declare which controls need localized
text and which key should be requested.

Binders and shells own adapter behavior. They receive an `IMxUiTextProvider`
from composition, resolve requests, apply fallback text when appropriate, and
rebind on locale revision changes. They must not call global localization
singletons.

Config integration belongs at the composition root. A later adapter can wrap
`MxFramework.Config.ILocalizationProvider` behind `IMxUiTextProvider`, but UI and
FairyGUI contracts should continue to depend only on UI core types.

## Validation Commands

```bash
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
dotnet build MxFramework.UI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.UI.FairyGUI.Manifest.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

## Non-Goals

- No full localization database or language file loader.
- No Config-to-UI provider adapter implementation.
- No Story dialog or choice UI migration.
- No broad Runtime HUD copy migration.
- No FairyGUI Editor publish or package bytes changes.
