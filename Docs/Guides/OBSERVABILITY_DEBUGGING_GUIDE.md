# Observability Debugging Guide

> Status: v0.1 | Date: 2026-05-18 | Scope: Phase 13 Issues #178-#187

This guide shows the implemented path for inspecting runtime state through Debug UI, Diagnostics, Config Runtime hot reload and Simulation Harness reports. Debug UI is an observation surface by default: overlay visibility, refresh state and snapshots do not enter Replay, SaveState or Runtime hash.

## 1. Register Debug Sources

Create a registry in the Demo, game composition root or tool host. The registry is not a singleton and observed runtime modules do not reference Debug UI.

```csharp
var registry = new FrameworkDebugSourceRegistry();
registry.Register(new LogDebugSource(logBuffer));
registry.Register(new ResourceDebugSource(resourceManager));
registry.Register(new RuntimeHostDebugSource(runtimeHost));
registry.Register(new GameplayDiagnosticSnapshotDebugSource(() => gameplaySnapshot));
registry.Register(new CombatDebugSnapshotDebugSource(() => combatSnapshot));
registry.Register(new CharacterControlDebugSource(characterControlStateMachine));

var aggregator = new DebugUiSnapshotAggregator();
DebugUiDashboardViewModel dashboard = aggregator.Refresh(registry);
```

Unavailable sources still appear in the dashboard. If one source throws during `CreateSnapshot()`, the aggregator records a `DebugUiErrorViewModel` and continues refreshing the other sources.

## 2. Show The Runtime Overlay

`DebugUiOverlayController` renders the same registry through UI Toolkit. Configure it from a composition root or scene bootstrap:

```csharp
overlay.Configure(registry);
overlay.SetVisibility(DebugUiVisibility.Collapsed);
overlay.RefreshNow();
```

The current overlay tabs are Overview, Snapshots, Timeline, Entities and Logs. They render existing snapshot sections and do not execute commands.

## 3. Add Timeline And Entity Watch

Timeline sources format existing diagnostics; they do not create new gameplay event authority.

```csharp
registry.Register(new GameplayRuntimeEventTimelineDebugSource(() => gameplayEvents));
registry.Register(new CombatTimelineDebugSource(() => combatDebugSnapshot));
registry.Register(new GameplayComponentWorldEntityWatchDebugSource(componentWorld));
```

Use `DebugUiTimelineFilter` and `DebugUiTimelineViewModel` in tests or custom tools when you need source, entity or category filtering outside the overlay.

## 4. Inspect Performance Counters

Performance counters are opt-in diagnostics. Use module-specific counter sources when you already have runtime objects or snapshots, then wrap their captured snapshot in `FrameworkPerformanceCounterDebugSource` for Debug UI:

```csharp
var runtimeCounters = new RuntimeHostPerformanceCounterSource(runtimeHost);
registry.Register(new FrameworkPerformanceCounterDebugSource(
    () => runtimeCounters.Capture(),
    "RuntimeHostPerformance"));

var gameplayCounters = new GameplayDiagnosticPerformanceCounterSource(() => gameplaySnapshot);
registry.Register(new FrameworkPerformanceCounterDebugSource(
    () => gameplayCounters.Capture(),
    "GameplayPerformance"));

var combatCounters = new CombatDebugPerformanceCounterSource(() => combatSnapshot);
registry.Register(new FrameworkPerformanceCounterDebugSource(
    () => combatCounters.Capture(),
    "CombatPerformance"));
```

For custom counters, use `FrameworkPerformanceCounterRecorder` and expose the snapshot through `FrameworkPerformanceCounterDebugSource`.

## 5. Run Simulation Harness Reports

`FrameworkSimulationBatchRunner` runs deterministic noEngine scenarios and produces `FrameworkSimulationReport`. Format the result for PR evidence or Debug UI:

```csharp
FrameworkSimulationReport report = runner.Run(scenarios);
string markdown = FrameworkSimulationReportFormatter.ToMarkdown(report);
string json = FrameworkSimulationReportFormatter.ToJson(report);
registry.Register(new FrameworkSimulationReportDebugSource(() => report));
```

Reports can include metrics, timeline entries and failures. They are regression evidence, not runtime authority.

## 6. Reload Config Runtime Patches

Hot reload is explicit. A file watcher or button should create a request, call the service, then switch providers only when the result succeeds.

```csharp
var service = new RuntimeConfigPatchHotReloadService(baseProvider);
var request = new RuntimeConfigHotReloadRequest(path, "demo-runtime-patch");
RuntimeConfigHotReloadResult result = service.Reload(request);

if (result.Success)
{
    activeProvider = result.Provider;
}
```

`RuntimeConfigHotReloadResult` exposes source name, source id, content hash, duration, changed tables, `ConfigChangeSet` and errors. `RuntimeConfigHotReloadPoller` only detects file path/write-time/length changes and returns a request; it does not reload silently.

To show the latest result in Debug UI:

```csharp
RuntimeConfigHotReloadResult lastHotReloadResult = null;
registry.Register(new RuntimeConfigHotReloadDebugSource(() => lastHotReloadResult));
```

Current hot reload support targets Config Runtime JSON patch bundles loaded by `RuntimeConfigPatchJsonLoader`. It does not hot reload Unity serialized assets, replay data, save states or runtime hashes.

## 7. Route Debug UI Input

Use the optional `MxFramework.DebugUI.Input` assembly when the overlay should respond to the framework input layer.

```csharp
var adapter = new DebugUiInputAdapter(inputProvider);
adapter.SetEnabled(true);
adapter.ProcessFrame(frame, debugUiTarget);
```

The adapter pushes `InputContext.Debug` as an overlay scope and non-destructively peeks `ToggleHud`, `ToggleConsole`, `DebugCycle` and `DebugStep` commands from `IInputProvider.Commands`. It does not drain gameplay commands, advance the queue frame, read `Keyboard.current`, `Gamepad.current`, `Mouse.current` or hold `InputAction` directly.

In Unity scenes, `DebugUiOverlayInputBridge` can connect an `InputService` and `DebugUiOverlayController` on the same GameObject. The bridge uses `InputService.LastCommandFrame` instead of maintaining its own frame counter.

## 8. Gate Debug Commands

Commands are opt-in and disabled by default.

```csharp
var gate = new DebugUiCommandGate(provider);
gate.Options.Enabled = true;

DebugUiCommandResult result = gate.Execute(
    new DebugUiCommandRequest("debug.refresh", confirmed: true));

registry.Register(new DebugUiCommandGateDebugSource(gate));
```

Providers expose `DebugUiCommandDescriptor` records with command id, risk, confirmation requirement and parameter schema. Destructive commands also require `gate.Options.AllowDestructiveCommands = true`. Command execution logs are observable through `DebugUiCommandGateDebugSource`; command execution is separate from `FrameworkDebugSnapshot`.

## 9. Troubleshooting Checklist

- Source missing from overlay: verify the composition root registered it in the same `FrameworkDebugSourceRegistry` passed to `DebugUiOverlayController.Configure()`.
- Dashboard refresh fails: check `DebugUiDashboardViewModel.Errors`; one bad source should not hide other sources.
- Timeline or Entities tab is empty: verify the registered source emits sections titled for timeline/entity watch and that upstream diagnostics snapshots contain entries.
- Hot reload did nothing: confirm a `RuntimeConfigHotReloadRequest` was issued and `result.Success` is true before switching providers.
- Debug shortcuts do nothing: confirm `DebugUiInputAdapter.Enabled`, the `InputContext.Debug` map, `InputService.LastCommandFrame` and whether another consumer drains commands before the bridge can peek them.
- Command rejected: inspect `DebugUiCommandResult.ErrorCode`; common values are `disabled`, `not_found`, `confirmation_required` and `destructive_disabled`.

## 10. Pending Boundaries

- There is no Runtime AI Planner-specific Debug UI adapter in Phase 13.
- There is no command tab in the Toolkit overlay yet; command providers are contracts and diagnostics only.
- There is no automatic Unity asset hot reload, replay export, save-state migration tool, network session view or Addressables-specific panel in this slice.
