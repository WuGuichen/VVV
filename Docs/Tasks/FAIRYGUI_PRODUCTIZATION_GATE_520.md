# FairyGUI Productization Gate (#520)

## Decision

FairyGUI is the preferred adapter for formal runtime game UI in WGameFramework.
This is a conditional productization decision, not a broad migration approval.

Use FairyGUI for player-facing runtime HUDs, panels, popups, modals and other
art-driven UI that benefits from package/component workflows, transitions,
lists, controllers and designer-authored assets. Keep UI Toolkit for editor
tools, debug overlays, diagnostics, existing showcase surfaces and lightweight
runtime validation UI until a FairyGUI replacement has an explicit migration
issue and validation gate.

`MxFramework.UI` remains the technology-neutral core. FairyGUI remains an
optional adapter under `MxFramework.UI.FairyGUI` and related generated/manifest
sidecars. Non-UI modules must not depend on FairyGUI, Unity UI Toolkit or a
global UI manager.

## Evidence

- The runtime adapter now has `MxUiViewContract`, `MxUiViewId`,
  `MxUiViewDescriptor`, `MxUiLayer`, lifecycle state, navigator open results and
  command descriptors in a noEngine `MxFramework.UI` core.
- The FairyGUI adapter can load package bytes through `MxFramework.Resources`,
  register packages, create components, bind a typed ViewModel and dispatch
  button commands through `IMxUiCommandSink`.
- The `MxRuntimeHud` vertical slice proves one real package path:
  `FGUIProject/assets/MxRuntimeHud` publishes to
  `Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes` and opens through
  `MxFairyGuiNavigator`.
- The generated `RuntimeAbilitySliceFairyGuiHudManifest` feeds both
  `MxUiViewContract` and `MxFairyGuiPackageDescriptor`, removing hand-coded
  string drift from the demo composition.
- The M4 manifest validator catches invalid schema, missing package source,
  missing package bytes, invalid package bytes header, catalog misses, missing
  exported components, missing component source, missing or renamed controls,
  control kind mismatch, duplicate view/package ids and command targets that
  are not real button controls in source XML.
- The project-local FairyGUI helper plugin documents the editor-owned publish
  path and keeps binary `*_fui.bytes` generation out of ad hoc file edits.

## Core vs Adapter Boundary

`MxFramework.UI` owns:

- stable view id, descriptor, layer and lifecycle primitives
- open result and open operation status
- navigator, registry, view and command sink contracts
- ViewModel type and command descriptor metadata
- technology-neutral ownership rules

`MxFramework.UI.FairyGUI` owns:

- FairyGUI package/component creation
- package ref counting and release through adapter scopes
- resource loading through `MxFramework.Resources`
- concrete FairyGUI component handles and binders
- mapping FairyGUI button events to `MxUiCommand`
- package/component failure mapping to `MxUiOpenResult`

Generated or checked-in manifest sidecars own:

- schema version
- package id/name/source paths
- package bytes keys and output paths
- view id, layer, component, ViewModel type
- named controls, bind paths and command targets
- required resources and validator diagnostics

Forbidden dependencies:

- `MxFramework.UI` must not reference UnityEngine, UnityEditor, FairyGUI,
  UI Toolkit, Gameplay, Combat, Story or project business modules.
- Gameplay, Combat, Story, Runtime and Resources must not reference FairyGUI.
- FairyGUI binders must not read gameplay singletons or mutate authoritative
  runtime state directly.

## Productized Rules

### Lifecycle

- `Open` creates or rebinds a view only after its contract, package descriptor,
  required resources and ViewModel type pass validation.
- `Show`, `Hide` and `Dispose` must be idempotent.
- `Close` owns component disposal, package scope release and adapter package ref
  release for normal views.
- FairyGUI M6 (#523) defines `KeepAlive` as hide-and-cache on close, with reopen
  rebinding the same component without reloading package bytes.
- FairyGUI M6 (#523) defines `CloseOnSceneChange` as disposal for matching open
  and cached views, including keep-alive views.
- Views must not retain resource handles outside their package load scope.
- Modal ownership is tracked by the FairyGUI layer host; input blocking,
  focus and cancel/back behavior are bridged in #524.

### Layers and Windows

- `MxUiLayer` is the cross-adapter ordering vocabulary. FairyGUI M6 (#523)
  implements explicit adapter-owned layer roots for the productized layers.
- `Hud` is for always-on gameplay HUDs, `Panel` for normal full panels,
  `Popup` for transient non-blocking surfaces, `Modal` for blocking decisions,
  `Toast` for short feedback and `Debug` for development overlays.
- FairyGUI views must be attached under those layer roots; do not rely on
  incidental child order under `GRoot`.

### Commands

- UI emits `MxUiCommand`; it does not execute gameplay directly.
- Command targets must be declared in the manifest and must resolve to real
  FairyGUI button controls in component source XML.
- Binders may update labels, enabled state and visual feedback from ViewModel
  command descriptors.
- Command validation and execution authority stay in Runtime/Gameplay systems.

### Resources

- FairyGUI package bytes, atlas textures, audio and fonts are described by
  `ResourceKey` and loaded through `MxFramework.Resources`.
- Source XML and generated manifests are human/code reviewable. Published
  `*_fui.bytes` files are editor outputs and must be generated through
  FairyGUI Editor or the project helper plugin.
- Validators must run before accepting package/component/control changes.

### Input and Focus

- FairyGUI may own pointer/click routing inside opened components.
- Global input context changes must go through the framework Input context stack
  or an adapter bridge; views must not poll devices directly.
- FairyGUI M7 (#524) bridges modal views to `InputContext.UI`, maps
  cancel/back input to `MxUiCommand`, and blocks lower-layer commands while a
  modal is active.
- Visual keyboard navigation remains a later UX concern.

### Localization

- Binders may assign text from ViewModels today.
- Productized localization hooks are not yet implemented. Views must not fetch
  text from global localization singletons; a localization provider/binding
  contract is required before migrating text-heavy UI.

### Transitions

- FairyGUI transitions are allowed inside adapter-owned views.
- Transition completion, cancellation and close-blocking semantics are not yet
  stable framework contracts.

### Coexistence

- UI Toolkit Debug UI remains the default diagnostics surface.
- UI Toolkit runtime showcase surfaces remain valid validation tools until a
  FairyGUI issue explicitly replaces them.
- Existing OnGUI fallback surfaces may remain as temporary diagnostics, but new
  formal runtime UI should target FairyGUI after the hardening issues are done.

## Migration Criteria

A runtime surface is eligible for FairyGUI migration when it has:

- a ViewModel that does not expose gameplay singletons
- command DTOs or `MxUiCommand` ids for every user action
- a manifest with package, view, resources, controls and command targets
- a resource catalog entry for package bytes and required dependent resources
- tests or noEngine validation that catch missing controls/resources
- a manual or automated smoke path that opens the view and verifies command
  dispatch
- an explicit fallback/coexistence decision for existing UI Toolkit or debug UI

Initial candidate order:

1. Runtime Ability HUD hardening, because it already has a real FairyGUI slice.
2. Runtime HUD shell/common UI services, because lifecycle/layers/input must be
   settled before broad panels.
3. Story presentation UI, after focus/modal/localization hooks exist.
4. Debug surfaces only if the goal is player-facing diagnostics; otherwise keep
   UI Toolkit Debug UI.

## Follow-Up Issues

The productization decision is accepted only with these follow-up issues tracked:

| Priority | Issue | Scope |
| --- | --- | --- |
| P0 | #523 FairyGUI M6: lifecycle and layer host hardening | Explicit layer roots, modal stack, keep-alive, close-on-scene-change, transition-safe close and deterministic disposal. |
| P0 | #524 FairyGUI M7: input focus and modal command bridge | Input context push/pop, pointer focus, cancel/back handling, modal blocking and command gating. |
| P1 | #525 FairyGUI M8: localization binding contract | Provider-neutral text lookup, refresh hooks and manifest-visible localization keys. |
| P1 | #526 FairyGUI M9: generator pipeline and stale output gate | Generate manifests/contracts/binder skeletons from source packages and fail on stale checked-in output. |
| P1 | #527 FairyGUI M10: Runtime HUD production shell | Promote the current MxRuntimeHud slice from demo proof to reusable HUD shell with smoke validation. |
| P2 | #528 FairyGUI M11: Story UI migration readiness | Define Story dialog/choice/presentation requirements after modal/focus/localization exist. |
| P2 | #529 FairyGUI M12: UI Toolkit coexistence cleanup | Document which showcase/debug surfaces stay UI Toolkit and which receive FairyGUI replacements. |

## Validation

The decision rests on the already validated M3/M4 slices plus the following
local gates for future changes:

```bash
dotnet run --project Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/FairyGUI.Manifest.Tests.csproj
dotnet build MxFramework.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.UI.FairyGUI.Manifest.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
rg -n "FairyGUI|Fgui|FairyGui" Assets/Scripts/MxFramework/Core Assets/Scripts/MxFramework/Runtime Assets/Scripts/MxFramework/Gameplay Assets/Scripts/MxFramework/Combat Assets/Scripts/MxFramework/Story -g '*.cs'
```

The last command should produce no matches.
