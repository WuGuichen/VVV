# ADR-006: FairyGUI Binder Generation

> Status: ADR

Date: 2026-06-15

Decision Status: Accepted

## Context

FairyGUI-backed player-facing UI is now used by the runtime HUD, Story dialog,
and CharacterTest panels. Before this decision, larger panel host files tended to
mix Unity component lifecycle, FairyGUI child lookup, text/input/button binding,
command payload creation, view model construction, command handling, generated
manifest metadata, and package/resource wiring.

The SettingsPanel pilot proved that the mechanical binding part can be generated
from a repository-owned C# generation spec while continuing to read the real
FairyGUI source XML and published resources. The remaining gap is a framework
rule that future panels can follow without copying ad hoc host code.

## Decision

- FairyGUI source XML remains the UI structure source of truth. Source packages
  stay under `FGUIProject/`, and published `.bytes` / atlas assets remain FairyGUI
  Editor or project plugin output.
- The repository noEngine generator owns framework metadata generation:
  generated manifest source plus generated partial binder source.
- Binder generation is driven by C# generation specs such as
  `MxFairyGuiBinderGenerationSpec`, `MxFairyGuiTextBindingSpec`,
  `MxFairyGuiInputBindingSpec`, `MxFairyGuiButtonBindingSpec`, and
  `MxFairyGuiRepeatedBindingSpec`.
- These spec types are authoring/generation API. They are not a runtime UI
  contract and do not change `IMxFairyGuiViewBinder<T>`, `MxFairyGuiNavigator`,
  or `MxFairyGuiRuntimeCatalog`.
- Generated binder files must be `partial` and limited to mechanical binding:
  text, localized text resolution calls, inputs, buttons, check buttons, repeated
  rows, focus order, and command enqueue.
- Handwritten partial binder files own only non-mechanical behavior: payload
  readback, special layout/focus supplements, or behavior the generator does not
  support yet.
- Host files own Unity component lifecycle and shell integration only: configure,
  catalog registration, open/refresh/close/release, runtime resource diagnostics,
  and camera/layer/input bridge setup.
- View model payloads, view model builders, command ids, element ids, and command
  sinks must live in separate same-namespace files once a panel grows beyond a
  trivial binder.
- `CustomProperties.json` and FairyGUI custom properties are not authoritative
  binding semantics for this framework. The C# spec is the reviewed binding
  contract.
- FairyGUI official `FUI_*` generated code remains disabled. Do not enable
  `allowGenCode` or change the project plugin's `handler.genCode = false`
  policy for framework panels.
- The noEngine check command must validate both generated manifest and generated
  binder output. Stale generated output must fail with a deterministic diagnostic
  and review report entry.

## Consequences

- Future FairyGUI panels have a stable split between authored UI, generated
  binding, handwritten view logic, and Unity host lifecycle.
- Host files stay reviewable because repeated FairyGUI child lookup and command
  wiring no longer accumulates in MonoBehaviour components.
- The repository keeps deterministic generation and CI-style stale-output checks
  without launching FairyGUI Editor.
- The current generation spec is still code, not external data. This keeps
  compiler validation strong, but panel authors must update noEngine target
  registration when adding a new generated binder.
- The first canonical sample is CharacterTest SettingsPanel. Other panels should
  migrate in later slices only after their scope and tests are explicit.

## Alternatives Considered

- Option: Enable FairyGUI official `FUI_*` code generation.
  Reason not chosen: it would introduce an external generated-code convention
  that does not know the framework view contract, command sink, resource
  descriptor, or noEngine validation model.
- Option: Store binding semantics in FairyGUI custom properties.
  Reason not chosen: it makes binding review harder in ordinary C# diffs and
  risks treating editor-local metadata as framework authority.
- Option: Keep all binder and command logic handwritten in Host files.
  Reason not chosen: SettingsPanel showed that this grows large quickly and
  obscures the Unity lifecycle responsibilities that the Host should own.

## References

- Related guide: `Docs/Guides/FAIRYGUI_BINDER_GENERATION_GUIDE.md`
- Canonical sample: `CharacterTestFairyGuiSettingsPanelHost`,
  `CharacterTestFairyGuiSettingsPanelBinder.g.cs`, and
  `CharacterTestFairyGuiSettingsPanelBinder.Partial.cs`
- Generation/check entry: `Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests`
