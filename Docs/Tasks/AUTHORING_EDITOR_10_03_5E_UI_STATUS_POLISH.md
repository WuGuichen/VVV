# Authoring Editor 10 / 03.5E: Preview UI Status Polish

> **状态**: Completed / Verified 2026-05-09
> **优先级**: P0
> **父任务**: `AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md`
> **并行批次**: Authoring Editor UI lane, starts after lane C contract

## Goal

Make the external Buff authoring editor clearly distinguish unavailable, loading, success, failed, scene preview, and dummy fallback states using the stable `RuntimePreviewResult` shape from 03.5C.

## Scope

### Do

- Update the Authoring Editor preview panel and EditorServer preview API only.
- Display:
  - preview server availability;
  - `previewMode`;
  - loaded patch ids / config source summary;
  - Buff snapshot summary;
  - attribute changes;
  - logs;
  - structured errors with code and message;
  - fallback / dummy state when present.
- Preserve offline authoring validation and merge preview when Unity Preview Server is unavailable.
- Add or extend local API / UI tests where practical.
- Update `AUTHORING_EDITOR_USAGE.md` with the final user-facing preview status behavior.

### Do Not

- Do not modify Unity Runtime Showcase UXML / USS.
- Do not touch Combat, AIAction, Gameplay runtime, or Preview server protocol logic except for consuming existing result fields.
- Do not recompute Buff / attribute state in frontend code.

## Expected Files

Allowed write scope after 03.5C:

```text
Tools/MxFramework.Authoring/
Tools/MxFramework.Authoring.Editor/
Docs/AUTHORING_EDITOR_USAGE.md
Docs/Tasks/AUTHORING_EDITOR_10_03_5E_UI_STATUS_POLISH.md
Docs/Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md
Docs/CAPABILITIES.md
```

## Acceptance

- Unavailable Preview Server shows unavailable without returning HTTP 500.
- Success and failed previews are visually and textually distinct.
- Dummy fallback / scene preview mode is visible.
- Structured errors are not swallowed by the UI.
- The preview panel consumes the result contract from 03.5C and does not duplicate runtime rules.

## Suggested Verification

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Authoring Core / Editor tests relevant to preview API
Manual local UI smoke if available
Tools/GitNexus/gitnexus.sh detect-changes
```

## Dispatch Notes

Start only after 03.5C reports stable result fields.

## Closeout 2026-05-09

### Files Changed

- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Preview/AuthoringPreview.Protocol.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs`
- `Tools/MxFramework.Authoring.Editor/web/app.js`
- `Tools/MxFramework.Authoring.Editor/web/styles.css`
- `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MockPreviewServer.cs`
- `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/Program.cs`
- `Docs/AUTHORING_EDITOR_USAGE.md`
- `Docs/Tasks/AUTHORING_EDITOR_10_03_5E_UI_STATUS_POLISH.md`
- `Docs/Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md`

### Behavior Closed

- `/api/preview/status` and `/api/preview/run` return structured `status=unavailable` for missing or unreachable Unity Preview Server instead of surfacing an HTTP 500.
- `/api/preview/run` preserves JSON-RPC apply failures as `status=failed`, including `code`, `reason`, `previewMode`, `error.data`, and nested `result.errors[]` when the runtime provides them.
- Authoring Preview DTOs now consume 03.5C fields: `previewMode`, `configMetadata`, and structured error `reason / previewMode / buffId / targetId`.
- The external preview panel displays connection state, success/failed/unavailable, scene vs dummy fallback, config metadata, Buff snapshots, attribute changes, damage ticks, status changes, structured errors, performance, `truncated`, and logs.
- The panel does not recompute Buff, attribute, or damage state; it only renders `RuntimePreviewResult`, load result, logs, and failure data returned by EditorServer / Preview Server.

### Verification

- `dotnet build Tools/MxFramework.Authoring/MxFramework.Authoring.sln --no-restore -p:UseAppHost=false -v minimal` passed with 0 warnings / 0 errors.
- `dotnet run --no-restore -p:UseAppHost=false --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj` passed.
- `dotnet build WGameFramework.sln --no-restore -p:UseAppHost=false -v minimal` passed with 0 warnings / 0 errors.
- `node --check Tools/MxFramework.Authoring.Editor/web/app.js` passed.
- Local EditorServer smoke on port 4874 returned HTTP 200 with `status=unavailable` for `/api/preview/status` and `/api/preview/run` when Unity Preview Server was not running.

### Remaining Risks

- In-app Browser smoke timed out before page snapshot capture in this run; local API/static-resource smoke passed, but full visual layout should still be checked manually in a live browser if needed.
- `preview.getLogs` can still carry additional server-side log history beyond inline result logs; the panel shows both with de-duplication but does not page older logs yet.
