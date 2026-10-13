# Task05 author publishing acceptance

Run the source tests without connecting to or operating the Unity Editor:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/MxFramework.NoEngineTests/Tactical.SelectedPoint.Author.Tests/Run.ps1 -UnityProjectPath E:\Unity\WxFramework\WGameFramework

dotnet run --project Tools/MxFramework.NoEngineTests/Tactical.Execution.Input.Tests/Tactical.Execution.Input.Tests.csproj
```

The first runner compiles production Runtime and Editor sources against existing Unity dependencies. It exercises 86 checks, including the source contract, strict JSON types, author edit/undo, freezing, metadata, semantic/content hash compatibility, exact Catalog projection and Deployment identity. The second runner exercises 52 original Deck/Ledger/Driver/input checks. Neither runner opens player services or writes published cards or saves.

For native acceptance, obtain exclusive ownership of the Editor, record the clean original scene, create an empty **unsaved** scene, and enter Play. Do not run the fixture in a user's scene. FairyGUI creates its default Stage Camera at Play startup. Only in this newly created empty validation scene, inspect that it is the single root with only Transform/Camera/StageCamera, then remove that auto-created camera before calling `BeginPublishedFormal`. `Build` creates a replacement overlay owned by its transient fixture root. Every mutating fixture entry still requires one unsaved Play scene, no GameManager, no player services and no foreign roots.

Invoke `MxFramework.Game.CharacterTest.Editor.CharacterTestSelectedPointValidationFixture` through Unity `execute_code`:

1. `BeginPublishedFormal()`; `FormalSelect()`; `FormalPointer(true)`; `FormalConfirm()`; `FormalCheckStarted()`; `FormalFinish()`.
2. Repeat with `FormalPointerAt(1000, 3000)` to prove each play binds its own point.
3. Repeat confirmation with `FormalFailBeforeStart()`; with `FormalCheckStarted(); FormalInterrupt()`; with `BeginPublishedFormal(6)` and `FormalTimeout()`; with `FormalBlockedAfterStart()`; and with `FormalSameFrameReplacement()`.
4. For each `BeginPublishedFormal(180, "id"/"revision"/"hash")`, select and point, then call `FormalVerifyPublishedRejection("PublishedCardVersionMissing")` for ID/revision or `FormalVerifyPublishedRejection("PublishedCardHashMismatch")` for hash. This calls the real execution input port and shared exact resolver, then cancels the preserved original reservation.
5. For the real input path: `BeginPublishedFormal(); FormalFairyView(); FormalNativeInput(); FormalSelect();` focus Game View; `FormalDeviceClick(); FormalConfirm(); FormalCheckStarted(); FormalFinish();`. Device events are queued through the real Unity Input System and InputService; they are not physical human clicks.
6. `VerifyNativePublishedCompatibility()` is read-only and authenticates original project tactical cards.
7. Check every returned `results[].pass`, not only tool success. Always run `CleanupFormal(); Cleanup();`, stop Play and restore the original scene without saving the fixture.

All contract/cost/cooldown numbers in these fixtures are temporary test values. The fixture publishes through the existing publisher into an in-memory Catalog, serializes and reauthenticates it, projects it and constructs Deployment. Only its catalog read dependency is injected; ownership, exact ID/revision/hash, contract checks, compiler, execution and settlement use production code. It never invokes the durable project publish transaction or player service.

Native evidence for the controlled main-tree integration is `.agent/SelectedPointAuthorNative-20261003/receipt.json`. The original 28-file patch was applied with a single defense JSON conflict resolved by retaining `maximumGuardFrames`; additional native fixes own the overlay, prevent a standalone HUD from opening player services, and provide the existing execution adapter's catalog dependency for isolated acceptance. No formal balance values or cards are approved by this acceptance.