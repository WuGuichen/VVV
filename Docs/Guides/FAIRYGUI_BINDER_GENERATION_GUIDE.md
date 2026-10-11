# FairyGUI Binder Generation Guide

> Status: Guide
>
> Use this when adding or migrating a framework-owned FairyGUI player-facing
> panel that should have generated manifest/binder code.

## Required Reading

Read `Docs/Decisions/ADR-006-fairygui-binder-generation.md` first. It defines
the binding authority and the boundaries that this guide applies.

For PNG intake, replacement, Composer resource references, and legacy FairyGUI
integration-audit workflow, read `Docs/Guides/UI_ART_RESOURCE_LIBRARY_GUIDE.md`. For schema,
usage scanning, diagnostics, and phase boundaries, read
`Docs/FAIRYGUI_UI_ART_SLICE_CATALOG.md`. Binder generation owns view structure
and control binding; UI Art Resource Library tooling owns cut-image semantics,
recommended usage, integration recipes, diagnostics, package audit output, and
guarded PNG replacement safety boundaries.
The two documents do not replace each other.

For semantic screen authoring, component recipes, blueprint Apply/Sync, and the
tool-owned package workflow, also read `Docs/FAIRYGUI_UI_COMPOSER.md` and
`Docs/Decisions/ADR-007-fairygui-semantic-ui-composer-authority.md`. UI Composer
owns authoring intent and constrained layout synchronization; generation specs
remain the authority for C# controls, view ids, and command bindings.

## Ownership Model

- FairyGUI package/component XML remains the final layout fact and FairyGUI
  Editor owns published package assets. For Composer-owned packages, Screen
  Blueprint owns editable authoring intent and the helper is the only package
  XML writer; legacy packages may still be maintained directly under
  `FGUIProject/AGENTS.md`.
- The repository noEngine generator owns generated manifest and generated binder
  source.
- Runtime code owns view models, command payloads, command sinks, and Host
  composition.
- Generated code must be disposable output. Do not add manual edits to `.g.cs`
  files.
- `CustomProperties.json` is not a binding authority. Keep binding semantics in
  reviewed C# generation specs.

## File Split

Use the CharacterTest SettingsPanel as the canonical sample.

- `Generated/*Manifest.g.cs`: generated view contract, package descriptor, and
  required resources.
- `Generated/*Binder.g.cs`: generated partial binder for mechanical text/input,
  button, repeated row, focus, and command enqueue logic.
- `*Binder.Partial.cs`: handwritten partial methods for payload readback or
  unsupported local behavior only.
- `*Ids.cs`: stable FairyGUI child names, view id, resource ids, and command ids.
- `*ViewModel.cs`: view model, input rows, command payload DTOs, and small
  view-model-only helpers.
- `*ViewModelBuilder.cs`: maps runtime/settings state into the panel view model.
- `*CommandSink.cs`: handles `MxUiCommand` and calls the owning runtime/settings
  APIs.
- `*Host.cs`: Unity lifecycle and shell composition only: configure, catalog,
  open, refresh, close, release, layer/camera/input bridge setup, and diagnostics.

## Generation Spec Checklist

Register each generated panel in the existing noEngine FairyGUI manifest test
tool. The target must define:

- manifest generation spec with package id, component name, source XML path,
  view id, view model type, required resources, named controls, localized text,
  and commands;
- binder generation spec with namespace, binder class name, view model type,
  view id expression, root default size, localization resolver expression,
  bindings, repeated bindings, and focus order;
- generated manifest source path and generated binder source path when the panel
  owns a generated binder.

Every command also owns a generated `MxUiFeedbackPolicy`. Omitted policy defaults
to `Click / None / None`; Back, result feedback, and silent commands must be
explicit in the reviewed C# generation spec. The generated manifest always emits
all three phases. A migrated Composer contract uses
`mx.uiComposerBindingContract.v2` and explicitly projects the same policy for
every command slot. Existing v1 contracts remain v1 and are projected as
`None / None / None` by Authoring Core until that screen is deliberately
migrated.

Use expression strings for project-specific ids and types when the generated
source should be checked by the Unity/C# compiler. Keep these expressions stable
and easy to grep.

## Binder Rules

- Generate text binding, localized text calls, input text binding, normal
  buttons, check buttons, repeated rows, command dispatch, and focus order.
- For check-style FairyGUI buttons, the generated binder must set
  `Fgui.ButtonMode.Check` before assigning `selected`.
- Generated button handlers enqueue `MxUiCommand`; they do not call gameplay,
  settings, story, resource, or network services directly.
- Generated binders do not play feedback. Runtime composition later decorates
  the panel command sink with the generated policy registry.
- Payload factories should delegate to handwritten partial methods when they
  read live FairyGUI control state.
- `OnAfterGeneratedBind` is the extension point for small local supplements; do
  not use it to rebuild the generated binder by hand.

## Host And Runtime Rules

- Host files must not own repeated control lookup, input row binding, payload
  parsing, or command side effects.
- View models must not contain Unity objects, FairyGUI objects, delegates,
  service references, or live runtime handles.
- Command sinks are panel-specific unless a reusable command contract already
  exists. Do not promote CharacterTest-only settings behavior into a framework
  API.
- Composition roots wire the generated binder through `MxFairyGuiRuntimeCatalog`
  and keep the runtime contract unchanged.

## Commands

Current generation and validation run through:

```powershell
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --check-generated
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --check-art-slices
```

Useful write commands for current targets:

```powershell
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --write-manifests
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --write-character-test-settings-panel-manifest
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --write-character-test-settings-panel-binder
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --write-character-test-network-panel-manifest
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --write-character-test-network-panel-binder
```

`--check-generated` must fail when generated manifest or binder output is stale.
The review report must include generated binder path and current/stale status for
targets that own binders. It also appends a `MxCharacterTestHud` art-slice
summary; that summary comes from the Art Slice Catalog tooling and does not
become runtime binder data.

## Validation

For a new or migrated generated panel, run:

```powershell
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj
dotnet run --project .\Tools\MxFramework.NoEngineTests\FairyGUI.Manifest.Tests\FairyGUI.Manifest.Tests.csproj -- --check-generated
dotnet run --project .\Tools\MxFramework.NoEngineTests\UI.Composer.Feedback.Tests\UI.Composer.Feedback.Tests.csproj
dotnet build .\MxFramework.UI.FairyGUI.Manifest.csproj -v:minimal
dotnet build .\<owning-runtime-assembly>.csproj -v:minimal
dotnet build .\MxFramework.Tests.csproj -v:minimal
```

When Unity behavior is touched, refresh Unity through MCP, wait for script
compilation, confirm Console has no real errors, and run focused EditMode tests.
FairyGUI tests that touch `Stage`, `StageCamera`, or `GRoot.inst` should guard
play-mode-only paths and reset FairyGUI test state when needed.

## Adding Another Panel

- Do not migrate multiple large panels in one slice unless the issue explicitly
  scopes that migration.
- Start from the SettingsPanel pattern, then delete behavior that the panel does
  not need.
- Keep generated source and handwritten partial code in the same namespace.
- Add `.cs.meta` files or refresh Unity so new scripts compile in both Unity and
  dotnet paths.
- Add or update focused binder tests before broad integration tests.
