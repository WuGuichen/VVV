# Authoring Editor 10 / 03.5B: Runtime Config Resolver Closeout

> **状态**: Completed / Verified 2026-05-09
> **优先级**: P0
> **父任务**: `AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md`
> **并行批次**: Preview lane B

## Goal

Close out Runtime Preview 03.5B by proving that `preview.loadPatch` uses the existing Runtime Patch v1 loader / merger / config-driven factory path, and that the loaded config source is visible to later `applyBuff`, `getSnapshot`, and logs.

This task turns the currently observed implementation evidence into tested, documented behavior. It does not redesign Preview protocol fields beyond the minimum metadata needed for 03.5C.

## Scope

### Do

- Inspect the existing `ScenePreviewWorld.LoadPreviewPatch(...)`, `RuntimePreviewAdapter`, `PreviewRpcServer`, and patch loader / merger code.
- Ensure Runtime Patch v1 is loaded through `RuntimeConfigPatchJsonLoader` and merged through `RuntimeConfigPatchMerger`.
- Ensure merged `BasicBuffConfig` / `BasicModifierConfig` rows rebuild or update the config-driven factories used by new preview operations.
- Preserve the Phase 11 no-retroactive semantics: existing Buff / Modifier instances are not rewritten after a patch change.
- Add or extend tests for:
  - valid patch affects a later new Buff instance;
  - malformed patch returns `2001` and does not poison the previous valid config;
  - runtime rejected / invalid merge returns `2002` or an explicit result error;
  - reset clears target state and has documented config-source behavior;
  - config source / changed ids / failed ids appear in logs or result metadata.
- Add a closeout section to this task document with files changed, tests run, and remaining risks.

### Do Not

- Do not modify `GameplayShowcase.uxml`, `GameplayShowcase.uss`, `MxFramework.UI.Toolkit`, Combat, AIAction, or WGame private data.
- Do not introduce a second patch format.
- Do not make Buff / Modifier hot-reload already mounted instances.
- Do not implement 03.5C full result mapping or 03.5E frontend polish here, except for the minimal metadata those later tasks can consume.

## Expected Files

Allowed write scope:

```text
Assets/Scripts/MxFramework/Preview/Runtime/
Assets/Scripts/MxFramework/Tests/Preview/
Docs/Tasks/AUTHORING_EDITOR_10_03_5B_RUNTIME_CONFIG_RESOLVER_CLOSEOUT.md
Docs/Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md
Docs/CAPABILITIES.md
```

If implementation requires changing Config.Runtime, Gameplay, Buffs, or Attributes, document the reason before editing and keep the change narrowly scoped.

## Acceptance

- `preview.loadPatch` uses Runtime Patch v1 loader / merger main path.
- A valid preview patch changes behavior of a later new Buff instance through a real `IBuffFactory` / config-driven factory path.
- A failed patch load does not silently erase or corrupt the last valid config source unless an explicit `discardPrevious` behavior is documented and tested.
- `preview.reset` behavior is explicit for runtime target state, logs, and current config source.
- Later 03.5C can read config source / patch ids / changed / failed summary from logs or result metadata without re-parsing the patch.
- No WGame real data is imported.

## Suggested Verification

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Preview
Unity Console: 0 compile error
Tools/GitNexus/gitnexus.sh detect-changes
```

## Dispatch Notes

You are not alone in the codebase. Do not revert other agents' or user changes. Work only inside the allowed scope unless a blocker forces a narrow exception. Report changed files, public behavior changes, test results, and any blockers for 03.5C.

## Closeout 2026-05-09

### Files Changed

- `Assets/Scripts/MxFramework/Preview/Runtime/PreviewProtocolDto.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/RuntimePreviewAdapter.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/ScenePreviewWorld.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/PreviewRpcServer.cs`
- `Assets/Scripts/MxFramework/Tests/Preview/ScenePreviewWorldDynamicTargetTests.cs`
- `Docs/Tasks/AUTHORING_EDITOR_10_03_5B_RUNTIME_CONFIG_RESOLVER_CLOSEOUT.md`
- `Docs/Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md`
- `Docs/CAPABILITIES.md`

### Behavior Closed

- `ScenePreviewWorld.LoadPreviewPatch(...)` now consumes direct Runtime Patch v1 JSON or `preview.loadPatch.params.rawSource`; it no longer reads the fixed `StreamingAssets/MxFramework/Demo/runtime_config_patch.json` path.
- Runtime Patch v1 parsing uses `RuntimeConfigPatchJsonLoader.Load(...)`; merging uses `RuntimeConfigPatchMerger.Merge(...)`.
- Merged `BasicModifierConfig` and `BasicBuffConfig` tables are registered into a fresh `ConfigRegistry`, then used to rebuild `ConfigModifierFactory<BasicModifierConfig>` and `ConfigBuffFactory<BasicBuffConfig>`.
- Merged tables are validated before commit. Invalid references / invalid runtime tables reject the patch and preserve the last committed config source.
- Parse failures propagate to `PreviewRpcServer` as `2001`; runtime validation / merge rejection propagates as `2002`.
- `preview.reset` clears target runtime state and the current preview config source. After reset, `applyBuff` requires another successful `loadPatch`.
- Existing Buff / Modifier instances are not rewritten when a later patch loads; only later factory-created instances consume the new config.

### 03.5C Handoff Shape

`preview.loadPatch.result` and later `RuntimePreviewResult` payloads now include:

```json
"configMetadata": {
  "sourceId": "preview-fixture-v1",
  "layer": "Patch",
  "loadedPatchIds": ["preview-fixture-v1"],
  "changedConfigIds": ["BasicModifierConfig:200101", "BasicBuffConfig:100101"],
  "failedConfigIds": ["BasicBuffConfig:100102"],
  "mergeWarnings": ["BasicBuffConfig:100102 ModifierId Missing config reference..."]
}
```

Top-level `loadedPatchIds` remains present for 03.4 compatibility and includes the legacy package id plus Runtime Patch source id when both are available. Logs also include source / changed / failed summaries, for example:

```text
ScenePreviewWorld: patch loaded source=preview-fixture-v1 layer=Patch changed=2 failed=0
ScenePreviewWorld: patch rejected source=preview-invalid-v1 reason=...
```

### Verification

- `dotnet build WGameFramework.sln --no-restore -v minimal` passed with 0 warnings / 0 errors.
- Unity MCP EditMode `MxFramework.Tests` passed: 345 total, 345 passed, 0 failed, 0 skipped.
- Unity Console compile errors: none observed after the EditMode run. The console retained test-runner `IgnoreFailingMessages:true` exception entries, but no C# compile errors.
- `Tools/GitNexus/gitnexus.sh detect-changes` passed; output recorded impacted symbols and did not report a command failure.

### Remaining Risks / Follow-Up

- `discardPrevious=false` still does not implement cumulative config layering in `ScenePreviewWorld`; successful loads replace the current preview config source. 03.5C should treat `configMetadata` as the committed current source, not an accumulated history.
- Full result mapping remains in 03.5C: this task only exposes `configMetadata`, logs, stable error codes, and factory-backed Buff / Modifier behavior.
- UI status presentation remains in 03.5E.
