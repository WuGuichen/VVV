# AIAction Migration Pilot 03: Dry-Run Field Map

> **域**: AIAction Config — 旧 WGame AI 行为配置的 dry-run 字段映射验证。
> **不包含**: Runtime AI Planner、Authoring AI Assist、Development Agent。

> **Status**: Implemented / Compile checked
> **Priority**: P1
> **Parent**: `AI_ACTION_MIGRATION_PILOT_02_REAL_DATA_ENTRY.md`
> **Parallel batch**: AIAction migration lane A

## Goal

Implement a dry-run field-map model for the first AIAction real-data entry without importing WGame real payloads and without emitting normalized source files.

Pilot 03 should prove that the future importer has a typed destination model, can report unsupported fields and sentinel candidates, and keeps legacy WGame formats as evidence only.

## Scope

### Do

- Add framework-side dry-run model types or test fixture helpers for:
  - `AIActionIndex` normalized row;
  - `AIActionGraph` normalized graph;
  - `AbilityGraph` key reference evidence;
  - unsupported field report entries;
  - sentinel candidate report entries.
- Use tiny synthetic or inline test data only. Do not copy WGame real rows, localized text, ids, icons, or graph payloads.
- Keep old short keys / positional arrays as input evidence concepts only; output model must use named semantic fields from Pilot 02.
- Add tests proving:
  - required table fields map into `AIActionIndex`;
  - required graph fields map into `AIActionGraph`;
  - unsupported source fields are reported;
  - empty / zero / sentinel ability id is reported as a sentinel candidate and not silently accepted;
  - no output files are emitted.
- Document the dry-run report shape in the closeout section.

### Do Not

- Do not parse real Excel, real BaseDataJson, or real SplitAIActionData.
- Do not add `ConfigSource/Tables/AIActionIndex.tsv`.
- Do not add `ConfigSource/Graphs/AIAction/*.json`.
- Do not generate runtime bytes.
- Do not modify Preview Runtime, Phase 12 Showcase, Combat, or WGame private project files.

## Expected Files

Preferred write scope:

```text
Assets/Scripts/MxFramework/Config/
Assets/Scripts/MxFramework/Tests/Config/
Docs/Tasks/AI_ACTION_MIGRATION_PILOT_03_DRY_RUN_FIELD_MAP.md
Docs/CAPABILITIES.md
Docs/ROADMAP.md
```

If production types feel premature, keep most of the implementation as internal test fixtures and document that Pilot 04 will decide whether to promote them.

## Acceptance

- There is a dry-run field-map contract with tests.
- Tests use no WGame real data.
- Unsupported fields and sentinel candidates are visible as structured report entries.
- Runtime bytes and source file emission remain blocked.
- No runtime module outside Config / tests changes.

## Suggested Verification

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Config
Unity Console: 0 compile error
Tools/GitNexus/gitnexus.sh detect-changes
```

## Dispatch Notes

You are not alone in the codebase. Do not modify Preview, Combat, UI Toolkit, or WGame data. Keep this as a dry-run migration contract.

## Closeout Notes

- Added `AIActionDryRunFieldMapTests` as a test-fixture contract instead of promoting dry-run importer types into production Config APIs.
- The fixture uses only inline synthetic ids, keys, names, conditions, effects, and ability-key evidence.
- No WGame Excel, BaseDataJson, SplitAIActionData, SplitAbilityData, localized text, real ids, or real graph payloads were imported.
- No `ConfigSource` files and no runtime bytes are generated.
- Preview Runtime, UI Toolkit, and Combat files were not changed by this task.

## Pilot04 Handoff Shape

Pilot04 can consume the following normalized dry-run shape, currently defined and compile-checked in tests:

```text
AIActionDryRunResult
  IndexRows[]
    Id: int
    NameKey: LocalizedTextKey
    DescKey: LocalizedTextKey
    CanGet: bool
  Graphs[]
    Id: int
    Name: string
    AbilityId: int
    Cost: int
    CooldownMs: int
    Conditions[]: { Key: string, Compare: string, Value: string }
    Effects[]: { Key: string, Effect: string }
  AbilityGraphKeys[]
    Id: int
    EvidencePath: string
  Report
    UnsupportedFields[]: { SourceSection, SourceField, DestinationShape, Reason }
    SentinelCandidates[]: { SourceSection, FieldName, RawValue, Reason }
    GeneratedSourceFiles[]: empty in Pilot03
    RuntimeBytesGenerated: false in Pilot03
```

Sentinel ability ids are not accepted as valid references in Pilot03. They are surfaced as `SentinelCandidates` with the reason `Requires explicit sentinel rule before reference validation can downgrade it.`

## Verification

```text
dotnet build WGameFramework.sln --no-restore -v minimal
  Result: Passed, 10 pre-existing warnings, 0 errors.

dotnet /usr/local/share/dotnet/sdk/9.0.306/Roslyn/bincore/csc.dll @Library/Bee/artifacts/200b0aE.dag/MxFramework.Tests.rsp -out:Temp/AIActionDryRunFieldMapTestsCompile.dll -refout:Temp/AIActionDryRunFieldMapTestsCompile.ref.dll -pdb:Temp/AIActionDryRunFieldMapTestsCompile.pdb
  Result: Passed, 0 errors. Bee response file includes AIActionDryRunFieldMapTests.cs.

Unity EditMode: MxFramework.Tests.Config.AIActionDryRunFieldMapTests
  Result: Blocked by unrelated compile error in Assets/Scripts/MxFramework/Preview/Runtime/ScenePreviewWorld.cs.

Tools/GitNexus/gitnexus.sh detect-changes
  Result: Passed, risk level low. Report includes broad pre-existing dirty workspace changes.
```
