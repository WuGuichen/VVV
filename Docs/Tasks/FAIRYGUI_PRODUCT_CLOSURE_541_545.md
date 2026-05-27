# FairyGUI Product Runtime Closure (#541/#545)

## Goal

Close the product-facing FairyGUI runtime UI path for the currently accepted
surfaces, including the Story multi-choice binder/viewmodel work completed in
#544.

This closure makes FairyGUI opt-in composition explicit for:

- Runtime Ability HUD (`MxRuntimeHud`)
- Story Dialog / Choice (`MxStoryDialog`)

UI Toolkit diagnostics, editor tools, showcase panels and Story fallback UI stay
in place.

## Product Composition Path

Use `MxFairyGuiProductRuntimeComposition` from
`Assets/Scripts/MxFramework/Demo/FairyGUI/RuntimeAbilitySliceFairyGuiHudComposition.cs`
when a scene wants both accepted product-facing FairyGUI surfaces:

```csharp
MxFairyGuiProductRuntimeShell shell = MxFairyGuiProductRuntimeComposition.CreateShell(
    resourceManager,
    runtimeHudCommandTarget,
    storyDialogCommandTarget,
    layerHost: layerHost,
    inputBridge: inputBridge,
    textProvider: textProvider);
```

The helper builds a local `MxFairyGuiRuntimeCatalog`, registers the generated
Runtime HUD and Story Dialog manifests, creates one local `MxFairyGuiNavigator`,
and exposes separate `RuntimeHud` and `StoryDialog` shells. It is intentionally
not a global UIManager singleton.

Before opening views, the composition root may run:

```csharp
MxFairyGuiRuntimeCatalog catalog = MxFairyGuiProductRuntimeComposition.CreateCatalog(
    runtimeHudCommandSink,
    storyDialogCommandSink,
    textProvider);

MxFairyGuiRuntimeCatalogDiagnostics diagnostics = catalog.CreateDiagnostics(resourceManager);
ResourcePreloadPlan preloadPlan = catalog.CreatePreloadPlan("runtime.ui.shell");
```

The expected product path is:

1. Package bytes are loaded through `MxFramework.Resources`.
2. Generated manifests provide package descriptors and view contracts.
3. Typed binders bind ViewModels to FairyGUI components.
4. Button clicks emit `MxUiCommand`.
5. Runtime/Story command targets validate and enqueue authoritative commands.
6. `Refresh` rebinds through navigator reopen.
7. `Close` releases component handles, package scopes and loaded resources.

## Current Product-Ready Scope

Product-ready:

- Runtime Ability HUD opt-in FairyGUI shell.
- Story Dialog opt-in FairyGUI shell for dialog/continue flow and bounded
  multi-choice presentation.
- Shared opt-in product shell for scenes that need both registered in one local
  catalog/navigator.
- Manifest source/package validation and generated-output stale gate.
- noEngine/product shell smoke covering package bytes, generated manifest,
  binder, command, refresh and release.

Fallback and diagnostics:

- UI Toolkit Ability Showcase remains the diagnostics/showcase surface.
- Story UI Toolkit vertical slice remains fallback and diagnostics.
- Debug UI overlay remains UI Toolkit.
- Editor tools remain UI Toolkit.

Deferred:

- Broad migration of debug/showcase/editor UI to FairyGUI.
- Complex transition UX beyond the current adapter-level lifecycle contracts.

## Validation

Required local gates:

```bash
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj -- --check-generated
dotnet build MxFramework.Demo.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

Focused smoke is in
`RuntimeAbilitySliceFairyGuiHudBinderTests.ProductRuntimeShell_OpenRefreshCommandAndClose_CoversRuntimeHudAndStoryDialog`.

Manual Unity smoke, when available, should open the scene using the opt-in
composition root, verify the Runtime HUD and Story Dialog can open independently,
trigger one HUD command, trigger continue and choice commands, refresh localized
or updated ViewModels, close both views, and confirm there are no Console errors
or retained FairyGUI package registrations.

## Non-Goals

- Do not remove UI Toolkit diagnostics or fallback surfaces.
- Do not introduce a global UIManager singleton.
- Do not edit FairyGUI published bytes by hand.
