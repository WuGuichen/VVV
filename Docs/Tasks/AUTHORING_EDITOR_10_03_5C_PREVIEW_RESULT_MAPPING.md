# Authoring Editor 10 / 03.5C: Preview Protocol Result Mapping

> **状态**: Completed / Verified 2026-05-09
> **优先级**: P0
> **父任务**: `AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md`
> **并行批次**: Preview lane C, starts after lane B

## Goal

Map real runtime preview state into `RuntimePreviewResult` without breaking 03.4 JSON-RPC method names or connection descriptor compatibility.

03.5C starts after 03.5B has stabilized how config source, loaded patch ids, changed ids, and failed ids are exposed.

## Scope

### Do

- Preserve existing methods: `preview.handshake`, `preview.loadPatch`, `preview.applyBuff`, `preview.getSnapshot`, `preview.getLogs`, `preview.reset`.
- Extend compatible result fields only where needed.
- Map runtime data into:
  - `success`;
  - `appliedBuffId`;
  - `buffSnapshots`;
  - `attributeChanges`;
  - `damageTicks` or clear explanatory logs;
  - `errors[]`;
  - `logs`;
  - performance / elapsed summary where already available;
  - config source / change summary metadata from 03.5B.
- Use `GameplayDiagnosticSnapshot` or equivalent runtime snapshot as source, not Console-only text.
- Add tests for success, no-attribute-change explanatory logs, unknown Buff, missing target, malformed patch residue, and result size/log truncation behavior.

### Do Not

- Do not rename JSON-RPC methods.
- Do not make the frontend recompute Buff or attribute state.
- Do not add WGame data or project-private runtime logic.
- Do not touch Phase 12 Showcase UI or Combat.

## Expected Files

Allowed write scope after 03.5B:

```text
Assets/Scripts/MxFramework/Preview/Runtime/
Assets/Scripts/MxFramework/Tests/Preview/
Docs/Tasks/AUTHORING_EDITOR_10_03_5C_PREVIEW_RESULT_MAPPING.md
Docs/Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md
Docs/CAPABILITIES.md
```

## Acceptance

- Successful `applyBuff` returns `success=true`, `appliedBuffId`, and at least one Buff snapshot or a clear no-op explanation.
- Attribute changes include before / after / source when they exist.
- Failures include stable code or structured `errors[]`, not only a generic message.
- Config source and change summary are visible in logs or metadata.
- Single-result payload respects the 1 MB soft budget and truncates logs deterministically.

## Suggested Verification

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Preview
Unity Console: 0 compile error
Tools/GitNexus/gitnexus.sh detect-changes
```

## Dispatch Notes

Start only after 03.5B reports stable metadata shape. Do not revert 03.5B changes; adapt to them.

## Closeout 2026-05-09

### Files Changed

- `Assets/Scripts/MxFramework/Preview/Runtime/RuntimePreviewAdapter.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/DummyPreviewWorld.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/ScenePreviewWorld.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/PreviewRpcServer.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/MxPreviewBootstrap.cs`
- `Assets/Scripts/MxFramework/Tests/Preview/ScenePreviewWorldDynamicTargetTests.cs`
- `Docs/Tasks/AUTHORING_EDITOR_10_03_5C_PREVIEW_RESULT_MAPPING.md`
- `Docs/Tasks/AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md`
- `Docs/CAPABILITIES.md`

### Behavior Closed

- `preview.applyBuff` success now maps the adapter runtime snapshot into `RuntimePreviewResult.success / previewMode / appliedBuffId / buffSnapshots / attributeChanges / damageTicks / statusChanges / logs / performance / configMetadata`.
- `PreviewRpcServer` now shares the same `PreviewLogBuffer` with `ScenePreviewWorld` when supplied by bootstrap, so patch load, merge rejection, target, and config logs are visible through result `logs` and `preview.getLogs`.
- Successful results add explanatory logs when no `attributeChanges` or `damageTicks` are captured; this covers state-only, zero-delta, and modifier-only Buffs without inventing new runtime DTO fields.
- `applyBuff` failures keep the 03.4 JSON-RPC `error.code=2003` compatibility path and include structured `error.data.reason / previewMode / buffId / targetId / result`. The nested result uses `success=false` and `errors[]` with the stable code/message/reason.
- `ScenePreviewWorld` and `DummyPreviewWorld` expose last failure reason/message for missing runtime patch, invalid Buff id, unknown Buff/config, missing target, and missing dummy factory cases.
- Result serialization enforces the 1 MB soft budget by truncating inline result logs deterministically from oldest to newest and setting `truncated=true`.

### Stable RuntimePreviewResult Fields

03.5C treats these fields as stable for Preview E:

```json
{
  "requestId": "client-supplied-or-empty",
  "success": true,
  "previewMode": "scene|dummy|runtime",
  "loadedPatchIds": ["preview-package", "preview-fixture-v1"],
  "configMetadata": {
    "sourceId": "preview-fixture-v1",
    "layer": "Patch",
    "loadedPatchIds": ["preview-fixture-v1"],
    "changedConfigIds": ["BasicModifierConfig:200101", "BasicBuffConfig:100101"],
    "failedConfigIds": [],
    "mergeWarnings": []
  },
  "appliedBuffId": "100101",
  "buffSnapshots": [],
  "attributeChanges": [],
  "damageTicks": [],
  "statusChanges": [],
  "logs": [],
  "errors": [],
  "performance": { "loadMs": 0, "applyMs": 0, "tickCount": 0, "totalMs": 0 },
  "truncated": false
}
```

Failure shape remains JSON-RPC compatible:

```json
{
  "error": {
    "code": 2003,
    "message": "Preview target 'MissingTarget' was not found in scene preview world.",
    "data": {
      "reason": "missing_target",
      "previewMode": "scene",
      "buffId": "100101",
      "targetId": "MissingTarget",
      "result": { "success": false, "errors": [{ "code": 2003, "reason": "missing_target" }] }
    }
  }
}
```

### 03.5E Handoff

Preview E can consume:

- `result.previewMode` to show scene / dummy fallback status.
- `result.configMetadata.sourceId / layer / loadedPatchIds / changedConfigIds / failedConfigIds / mergeWarnings`.
- `result.buffSnapshots[]` for active Buff rows.
- `result.attributeChanges[]` with `ownerId / attribute / before / after / deltaSource`.
- `result.damageTicks[]`; if empty, read `result.logs[]` for the `no damageTicks captured` explanation.
- `result.logs[]` for patch loaded/rejected summaries and no-change explanations.
- `result.performance.applyMs / tickCount / totalMs`.
- `result.truncated` to show that inline logs were clipped and `preview.getLogs` may be needed.
- JSON-RPC `error.code` plus `error.data.result.errors[]` for failure presentation without recomputing runtime state in the UI.

### Verification

- `dotnet build WGameFramework.sln --no-restore -v minimal` passed with 0 warnings / 0 errors.
- Unity MCP EditMode `MxFramework.Tests.Preview` passed: 17 total, 17 passed, 0 failed.
- Unity Console compile errors: 0 after refresh and test run.
- `Tools/GitNexus/gitnexus.sh detect-changes` passed; report showed low risk. It observed unrelated pre-existing worktree changes, but no command failure.

### Remaining Risks / Follow-Up

- Authoring Core DTOs can currently ignore unknown `previewMode` / `configMetadata` fields. 03.5E should update frontend-facing DTOs only as needed for display.
- `damageTicks` remains empty for config-driven modifier Buffs; the result now explains this in logs instead of inventing damage semantics at the framework layer.
