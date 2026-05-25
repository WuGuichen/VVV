# Logging Interface

> Current implementation: lightweight runtime logger contract plus Unity Console adapter.

## Responsibility

Logging provides a small, optional feedback path for composition roots, demos, runtime probes, and Unity play-mode validation.

It answers "what just happened" during development. It does not replace Diagnostics snapshots, runtime events, command validation results, Replay, SaveState, or runtime hash.

## Assemblies

| Assembly | Path | Dependencies | Responsibility |
| --- | --- | --- | --- |
| `MxFramework.Runtime` | `Assets/Scripts/MxFramework/Runtime/RuntimeLogger.cs` | `MxFramework.Core` | noEngine logging contract: `IRuntimeLogger`, `RuntimeLogLevel`, `NullRuntimeLogger`, extension helpers |
| `MxFramework.Runtime.Unity` | `Assets/Scripts/MxFramework/Runtime.Unity/UnityRuntimeLogger.cs` | `MxFramework.Runtime`, UnityEngine | Unity Console adapter backed by `Debug.Log`, `Debug.LogWarning`, and `Debug.LogError` |

There is currently no standalone `MxFramework.Logging` assembly, no ring buffer, no file sink, no remote upload, and no Diagnostics adapter for logs. Add those only through a separate task if they become necessary.

## Public API

| Type | Purpose |
| --- | --- |
| `RuntimeLogLevel` | Severity: `Info`, `Warning`, `Error` |
| `IRuntimeLogger` | noEngine sink contract: `Log(RuntimeLogLevel level, string category, string message)` |
| `NullRuntimeLogger` | no-op singleton for default injection and pure tests |
| `RuntimeLoggerExtensions` | convenience methods: `Info`, `Warning`, `Error` |
| `UnityRuntimeLogger` | Unity-facing adapter with `Enabled` switch, optional Unity `Object` context, rich-text color options, and category header color overrides |

## Usage

Pure runtime code should accept `IRuntimeLogger` through its constructor or composition root and default to `NullRuntimeLogger.Instance`.

```csharp
using MxFramework.Runtime;

public sealed class GameSlice
{
    private readonly IRuntimeLogger _logger;

    public GameSlice(IRuntimeLogger logger = null)
    {
        _logger = logger ?? NullRuntimeLogger.Instance;
        _logger.Info("GameSlice", "Construct");
    }
}
```

Unity composition roots can route messages to Console:

```csharp
using MxFramework.Runtime.Unity;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    private UnityRuntimeLogger _logger;
    private GameSlice _slice;

    private void OnEnable()
    {
        _logger = new UnityRuntimeLogger(this, "CharacterTest");
        _logger.SetCategoryHeaderColor("GameSlice", "#58D68D");
        _slice = new GameSlice(_logger);
    }
}
```

## Rules

- `MxFramework.Runtime` remains `noEngineReferences=true`; never call `UnityEngine.Debug` from pure runtime slices.
- Logging is optional. Runtime code must behave the same when passed `null` or `NullRuntimeLogger.Instance`.
- Use logs for developer feedback and lifecycle probes, not gameplay authority or recoverable error control flow.
- For recoverable failures, return structured result types such as `RuntimeCommandValidationResult`; log only as an observation.
- High-frequency paths should avoid expensive string construction unless the logger is known to be enabled by the composition root.
- `UnityRuntimeLogger` default colors are level-based. Register category header colors in the Unity composition root when a scene or demo needs per-system visual grouping.

## Current Users

- `Assets/Scripts/MxFramework/Demo/CharacterTest/GameManager.cs`
- `Assets/Scripts/MxFramework/Demo/CharacterTest/GameSlice.cs`

## Test Entry

Current coverage is by assembly compilation:

- `dotnet build MxFramework.Runtime.csproj --no-restore`
- `dotnet build MxFramework.Runtime.Unity.csproj --no-restore`
- `dotnet build MxFramework.Demo.csproj --no-restore`
