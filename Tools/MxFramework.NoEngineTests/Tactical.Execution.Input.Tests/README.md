# Tactical execution input focused checks

Run from the repository root:

```powershell
dotnet run --project Tools/MxFramework.NoEngineTests/Tactical.Execution.Input.Tests/Tactical.Execution.Input.Tests.csproj
```

The harness links the actual production input contract and common validator, plus existing Deck, MomentumLedger, TimeCoordinator and SingleCardDriver sources. It uses controlled geometry/path and execution ports to verify invalid or stale requests, original reservations and cancellation, delayed revalidation, one admission/payment on repeated confirmation, frozen parameters, and compatibility with an input-free execution port.

Current result: 52 checks, including generic execution start versus physical action start, original refund/cooldown settlement and automatic provider capability/unavailability/range checks. This is not a real ground query, movement, Stage command consumer, UI event, physical click or Unity Play acceptance. No production card or save data is loaded or changed. The separate temporary Stage validation runner covers the real command consumer with controlled execution/geometry ports.

Point movement preserves the original reservation after confirmation. Only the first consumer-accepted owned move commits payment and starts the original cooldown; pre-start failure releases the same token.
