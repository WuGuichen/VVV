# FairyGUI Lifecycle and Layer Host (#523)

## Goal

Harden the FairyGUI adapter lifecycle enough for productization work to build on
it without introducing a global UI manager.

This slice stays inside `MxFramework.UI.FairyGUI`. The noEngine
`MxFramework.UI` contracts already expose the required metadata:
`MxUiLayer`, `MxUiViewDescriptor.Modal`, `KeepAlive` and
`CloseOnSceneChange`.

## Layer Host

`IMxFairyGuiLayerHost` is the adapter boundary for presenting FairyGUI
components. `MxFairyGuiNavigator` creates views, while the layer host decides
where those views are attached.

The default `MxFairyGuiLayerHost` creates explicit layer roots under
`GRoot.inst`:

```text
MxFairyGuiLayer_Background
MxFairyGuiLayer_Hud
MxFairyGuiLayer_Panel
MxFairyGuiLayer_Popup
MxFairyGuiLayer_Modal
MxFairyGuiLayer_Toast
MxFairyGuiLayer_Debug
```

Roots are inserted by `MxUiLayer` numeric order and are created as a fixed set
on first use. This means view ordering no longer depends on the order
individual components happen to open.

## Lifecycle Rules

- `Open` validates the contract, package descriptor, resources and ViewModel
  type before component creation.
- `Show`, `Hide` and `Dispose` remain idempotent through `MxUiLifecycle`.
- A normal `Close` removes the view from the open map, disposes the component,
  releases the package load scope and releases the FairyGUI package ref.
- A `KeepAlive` close removes the view from the open map and hides it, but keeps
  the component, package scope and package ref cached for a later reopen.
- Reopening a cached view rebinds the ViewModel and shows the same component
  without reloading package bytes.
- `CloseSceneViews` disposes both open and cached views whose descriptors set
  `CloseOnSceneChange`.

## Modal Ownership

`MxUiViewDescriptor.Modal` is tracked by the layer host. Showing a modal view
adds its id to the adapter modal stack; hiding or disposing the view removes it.

This slice does not implement input blocking or cancel/back behavior. Those are
owned by #524.

## Boundaries

- No Story, Debug UI or broad runtime HUD migration is included.
- No global `UIManager` singleton is introduced.
- `MxFramework.UI` core remains unchanged and does not reference FairyGUI.
- Package resource ownership remains in `MxFairyGuiResourceBridge` and
  `MxFairyGuiPackageLoadScope`.

## Validation

```bash
dotnet build MxFramework.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

The focused test coverage is in
`Assets/Scripts/MxFramework/Tests/UI/FairyGUI/MxFairyGuiNavigatorTests.cs`.
