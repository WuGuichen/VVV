# FairyGUI Input Focus and Modal Command Bridge (#524)

## Goal

Bridge FairyGUI modal ownership to the framework input context stack and define
minimal modal command rules without redesigning the Input module.

This slice stays in `MxFramework.UI.FairyGUI`. The adapter does not directly
reference `MxFramework.Input`; composition roots inject an `IDisposable` scope
factory that can call `InputContextStack.Push(InputContext.UI)`.
`MxFramework.UI` core and Gameplay/Combat/Story modules remain independent of
FairyGUI.

## Input Context

`MxFairyGuiInputContextBridge` connects modal FairyGUI views to an injected
input scope factory:

- showing a modal view enters the injected UI scope
- the expected composition-root scope is
  `InputContextStack.Push(InputContext.UI, InputContextPolicy.Exclusive)`
- hiding or disposing the modal view releases the input scope
- keep-alive close releases the scope while caching the component
- reopening a cached modal view pushes a fresh scope

Non-modal views do not push an input context.

## Cancel / Back

`MxFairyGuiCancelCommandBridge` maps a caller-confirmed cancel/back input to a
normal `MxUiCommand`:

```text
sourceViewId = top modal view id
commandId    = ui.cancel
payload      = caller-supplied input payload, if any
```

The composition root is responsible for detecting `InputIntent.Cancel` with the
desired phase, then calling `ProcessCancel`. The bridge only enqueues the
command when a modal view is active. It does not close the modal directly and
does not execute gameplay logic.

## Modal Command Gate

`MxFairyGuiModalCommandGate` is an `IMxUiCommandSink` wrapper. When no modal is
active, commands pass through. When a modal is active, only commands from the
top modal view pass through; commands from lower-layer views or invalid sources
are blocked.

This is intentionally adapter-level command gating. Command validation and
gameplay authority still belong to Runtime/Gameplay systems.

## Boundaries

- No project-specific keybindings are introduced.
- No global UI manager is introduced.
- The adapter does not poll devices directly.
- FairyGUI binders continue to emit `MxUiCommand`; they do not mutate gameplay
  state directly.
- Focus and modal input blocking are productized at the command/input context
  level. Visual keyboard navigation remains a later UX concern.

## Validation

```bash
dotnet build MxFramework.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
dotnet build MxFramework.Tests.UI.FairyGUI.csproj /nr:false -m:1 -v:minimal
```

Focused coverage is in
`Assets/Scripts/MxFramework/Tests/UI/FairyGUI/MxFairyGuiNavigatorTests.cs`.
