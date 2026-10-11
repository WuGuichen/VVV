# FairyGUI Runtime UI

> Status: Current
>
> Version: v0.5 | 2026-10-09
>
> Allocation: popup operations and Guidance submissions are `LowFreqAlloc`; Guidance steady-state `Tick` is `NoAlloc` after view initialization.

## View retention and owner teardown

`MxFairyGuiNavigator.Close(id)` hides and caches views whose descriptor enables
`KeepAlive`; reopening reuses the component and binds the latest model. Hidden
views release their input scope and owner popups. `ReleaseView(id)` disposes the
named open or cached view regardless of `KeepAlive`, or cancels its pending open.
It returns false when that id has no view or pending open, and does not release
unrelated views. Owners using a shared navigator must call it when their binding
context is destroyed or reconfigured. `CloseSceneViews()` and navigator disposal
continue to release both open and cached views in their existing scope.

## Lightweight popup contract

`IMxFairyGuiPopupService` owns lightweight, non-modal popovers through FairyGUI's
native `GRoot` popup stack.

- `Show` opens a popup for one owner view and anchor.
- `Toggle` closes the same open popup or opens it when closed.
- `Hide`, `HideTop`, `HideOwner`, and `HideAll` preserve stack order and report a
  typed close reason.
- Pointer input inside the popup keeps it open. Pointer input outside closes it
  without consuming the target click.
- A sibling popup replaces the currently open sibling. An anchor inside the
  current popup may open a nested popup.
- `MxFairyGuiPopupOptions` controls above, below, left, or right placement,
  anchor gap, and root safe margin.
- Closing an owner view, changing roots, or disposing the service closes its
  remaining popups.

`MxFairyGuiFocusInputBridge` accepts the popup service as an optional dependency.
Cancel, navigation, and submit target the top popup before the modal stack or
the current surface.

## Rendering and batching contract

`MxFairyGuiLayerHost` enables FairyGUI batching on every productized layer root.
The active URP asset must also support dynamic batching for FairyGUI's reordered,
material-compatible meshes to collapse into fewer draw calls. CharacterTest binds
the FairyGUI Stage Camera as an Overlay Camera and excludes its UI culling mask
from the Base Camera so the same UI layer is not rendered by both cameras.

## CharacterTest pointer press feedback

`CharacterTestFairyGuiCursor` owns a `CharacterTestFairyGuiClickFeedback` per Stage.
The transient decoration uses one non-interactive Shape and a bounded four-pulse
mesh factory; it does not change Composer component trees or cursor semantics.
Left-button press coordinates are converted from bottom-left screen pixels to
Stage top-left pixels once. Each pair of feathered rings lasts 0.38 seconds on
unscaled UI time, remains at the press location, and fades without additive glare.
Release/hover does not emit another pulse. Hidden/locked cursor, focus loss and
owner disposal clear the effect. The Shape rejects both touch and non-touch hit
tests so disabled-control cursor detection still sees the underlying control.
Idle feedback is hidden; overlapping pulses share one draw and bounded storage.
The ripple shader evaluates a broad uniform blue-green band with pixel-derivative
feathering. The leading band is 4–3 design pixels wide, the quieter following band
2.8–2 pixels; structure is carried by their separation and expansion, without fine
reflection or trough detail. Each ring uses one quad; four simultaneous double
ripples total 32 vertices. The delayed inner ring fades in smoothly. No additive
blending is used.

## Player Guidance presenter

`MxFairyGuiGuidancePresenter` renders the shared noEngine Guidance state through the
Composer-owned `MxPlayerGuidanceUi` package. It opens two non-interactive views:

| View | Component | Layer | Capacity |
| --- | --- | --- | --- |
| `ui.guidance.card` | `GuidanceCardPanel` | `MxUiLayer.Hud` | one fixed Card |
| `ui.guidance.notices` | `GuidanceNoticeStackPanel` | `MxUiLayer.Toast` | two fixed notices |

`CreateCatalog(...)` registers both views against the same package descriptor and generated
manifest/binders. Card and Notice roots are full-stage layout surfaces but the binder disables
touch recursively; they do not join the modal stack, capture cursor, pause gameplay, poll input,
or emit `MxUiCommand`.

The presenter owns one `MxUiGuidanceState`, reuses one Card and one Notice-stack ViewModel,
caches active FairyGUI roots, and applies fixed enter/update/exit interpolation. `Tick(float)`
advances only two notice slots, animation state, and locale revision. It performs no per-frame
LINQ, temporary collection, string concatenation, reflection, child lookup, or Unity object
search. Text and child binding occur only on submission, expiry, layout change, or locale
revision change.

CharacterTest projects a six-step authored player test flow into this presenter: movement/stop,
jump/land, camera-mode switch, target lock, target unlock, and a basic-attack damage hit. The
scene-owned Guidance cache supplies the immutable plan; the project sequence coordinator owns
selection and completion, typed observers consume confirmed runtime state/events/telemetry, and
the snapshot mapper only emits shared Guidance DTOs. `[` and `]` navigate the flow without
requiring the Developer Debug Panel or changing gameplay authority.

Two static layout presets are current:

- `Default`: Card at the upper-left and notice stack at the upper-right;
- `TopCornersOccupied`: Card moves down 208 design pixels and notices move to the top center.

CharacterTest composes the presenter through `CharacterTestFairyGuiGuidanceHost`, uses
`TopCornersOccupied`, declares the visible surface as non-modal `LightOverlay`, preloads the
package through the existing FairyGUI resource plan, and clears its presentation when a large
player panel takes control. The current real producers are the five project-private completion
observers backed by CharacterTest deterministic character state, camera/target-lock events and
Combat damage telemetry. Missing, invalid, or stale formal cache disables Guidance with a typed
diagnostic and never restores a hardcoded sequence. The explicit development-preview method is
labelled as preview and does not claim Validation success.

## Tooltip, popover, and modal boundaries

| Surface | Use | Lifetime | Dismissal |
| --- | --- | --- | --- |
| Tooltip | Short hover/focus explanation | Owned by `MxFairyGuiTooltipService` | Pointer/focus leave, bound target hidden/removed, or service cleanup |
| Popover | Lightweight selection or contextual actions | Owned by `IMxFairyGuiPopupService` and an owner view | Outside click, repeated trigger, cancel, replacement, or owner close |
| Modal | Blocking decision or workflow | Owned by `IMxFairyGuiLayerHost` modal stack | Explicit modal command/cancel policy |

Do not emulate a popover with a transparent full-screen graph. Do not use a
popover for a blocking confirmation, and do not assign FairyGUI's native
`.tooltips` property when the project tooltip service is available.

## Tests

- `Packages/com.vincent.wgameframework/Tests/UI/FairyGUI/MxFairyGuiPopupServiceTests.cs`
- `Packages/com.vincent.wgameframework/Tests/UI/FairyGUI/MxFairyGuiTooltipServiceTests.cs`
- `Packages/com.vincent.wgameframework/Tests/UI/FairyGUI/MxFairyGuiNavigatorTests.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/PlayMode/PlayerGuidancePresentationPlayModeTests.cs`
- `Tools/MxFramework.NoEngineTests/UI.Guidance.Tests/`
