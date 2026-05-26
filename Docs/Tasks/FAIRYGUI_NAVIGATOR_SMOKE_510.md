# FairyGUI Navigator Smoke (#510)

## Scope

This task validates the first real FairyGUI runtime package through the
framework navigator path.

- source package: `FGUIProject/assets/MxFguiSmoke`
- published package bytes: `Assets/Bundles/FGUI/MxFguiSmoke/MxFguiSmoke_fui.bytes`
- package name: `MxFguiSmoke`
- component: `SmokePanel`
- bindable child: `txtTitle`

The smoke remains a minimal M2d validation slice. It does not migrate Runtime
HUD, Story, Debug UI, or any WGame UI surface to FairyGUI.

## Runtime Path

The EditMode smoke uses the same public framework APIs expected by runtime
callers:

1. Load the checked-in FairyGUI publish output as `byte[]`.
2. Register it in `MemoryResourceProvider`.
3. Expose it through `ResourceCatalogEntry` with
   `MxFairyGuiResourceTypeIds.PackageBytes`.
4. Register `MxUiViewContract` for `MxFguiSmoke/SmokePanel`.
5. Register `MxFairyGuiPackageDescriptor` for the package bytes key.
6. Open the view through `MxFairyGuiNavigator`.
7. Bind a ViewModel into `txtTitle`.
8. Close the view and verify resource release.

This keeps the test on the adapter boundary rather than calling FairyGUI
directly from the test surface.

## Verification Coverage

`MxFairyGuiNavigatorTests.Open_WithRealMxFguiSmokePackage_BindsClosesAndReleases`
now covers:

- the checked-in bytes file exists and starts with the `FGUI` header
- `MxFairyGuiNavigator.Open` succeeds for the real package/component
- binder receives a concrete `MxFairyGuiComponentHandle`
- `txtTitle` is found and updated from the ViewModel
- the view enters the visible lifecycle state
- `Close` is idempotent at the navigator boundary
- `MxFairyGuiResourceBridge` releases the loaded scope
- `ResourceManager` has no loaded resources after close

Existing fake-host navigator tests continue to cover missing package bytes,
missing contract, missing package descriptor, binding failures, pending
resources, repeated open and close idempotence.

## Remaining Work

M2d does not prove full rendered pixels or input. Those belong to later slices:

- M3: a real Runtime HUD or demo-facing vertical slice using FairyGUI.
- M4: generated descriptors, package/component/control validation, and CI
  checks for FairyGUI resource drift.
