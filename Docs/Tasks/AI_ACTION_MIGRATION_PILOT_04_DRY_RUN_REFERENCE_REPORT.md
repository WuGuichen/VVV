# AIAction Migration Pilot 04: Dry-Run Reference Report

> **域**: AIAction Config — 旧 WGame AI 行为配置的 dry-run 引用验证报告。
> **不包含**: Runtime AI Planner、Authoring AI Assist、Development Agent。

> **Status**: Implemented / Target compile checked
> **Priority**: P1
> **Parent**: `AI_ACTION_MIGRATION_PILOT_02_REAL_DATA_ENTRY.md`
> **Parallel batch**: AIAction migration lane B, starts after lane A

## Goal

Use the Pilot 03 dry-run normalized model to register `AIActionGraph` and `AbilityGraph` keys into `ConfigSourceIndex`, then produce a dry-run reference validation report with error / warning / info counts.

This task still does not import real data, emit normalized files, or generate runtime bytes.

## Scope

### Do

- Consume the Pilot 03 normalized dry-run model or test fixture shape.
- Register dry-run keys for:
  - `Table:AIActionIndex`;
  - `Graph:AIActionGraph`;
  - `Graph:AbilityGraph`;
  - optional `Localization` evidence as warning-only.
- Validate the Pilot 02 rules:
  - `AIActionIndex.id -> AIActionGraph.id` is `error`;
  - `AIActionGraph.abilityId -> AbilityGraph.id` is `error`;
  - localization and enum-domain visibility checks remain `warning`;
  - runtime bytes status remains `info`.
- Produce structured report entries and summary counts.
- Add tests for pass, missing graph, missing ability graph, warning-only localization, and info-only runtime bytes boundary.

### Do Not

- Do not add real WGame data.
- Do not emit ConfigSource files.
- Do not parse Excel or JSON payloads.
- Do not modify Runtime Preview, Showcase, Combat, or WGame private files.

## Expected Files

Allowed write scope after Pilot 03:

```text
Assets/Scripts/MxFramework/Config/
Assets/Scripts/MxFramework/Tests/Config/
Docs/Tasks/AI_ACTION_MIGRATION_PILOT_04_DRY_RUN_REFERENCE_REPORT.md
Docs/CAPABILITIES.md
Docs/ROADMAP.md
```

## Acceptance

- Dry-run report contains structured error / warning / info counts.
- Missing `AIActionGraph` and missing `AbilityGraph` references block acceptance.
- Localization / enum-domain unknowns are warning-visible, not silent.
- Runtime bytes remain explicitly out of scope.

## Suggested Verification

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Config
Unity Console: 0 compile error
Tools/GitNexus/gitnexus.sh detect-changes
```

## Dispatch Notes

Start only after Pilot 03 stabilizes its dry-run model / report shape.

## Closeout Notes

- Consumed the Pilot03 test-fixture dry-run shape without promoting new production Config APIs.
- Added dry-run source registration for:
  - `Table:AIActionIndex` from `IndexRows[].Id`;
  - `Graph:AIActionGraph` from `Graphs[].Id`;
  - `Graph:AbilityGraph` from `AbilityGraphKeys[].Id`;
  - `Localization:Localization` from synthetic localization-key evidence.
- Used existing `ConfigSourceIndex` and `ConfigReferenceRule` validation for blocking references:
  - `pilot02.aiaction.index.graph`: `AIActionIndex.Id -> Graph:AIActionGraph.Id`, `error`;
  - `pilot02.aiaction.ability`: `AIActionGraph.AbilityId -> Graph:AbilityGraph.Id`, `error`;
  - `pilot02.aiaction.localization`: `AIActionIndex.NameKey/DescKey -> Localization:Localization.Key`, `warning`.
- Added warning-level enum-domain visibility checks for synthetic graph condition/effect domains.
- Added an info-only `pilot02.aiaction.runtime-bytes` entry to preserve the boundary that runtime bytes are not generated in Pilot04.
- No WGame real data, real ids, real localized text, real Split JSON payloads, ConfigSource files, or runtime bytes were added.
- Preview, UI Toolkit, and Combat files were not modified by this task.

## Report Shape

Pilot04 keeps the report as a test-fixture contract:

```text
AIActionDryRunReport
  UnsupportedFields[]
    SourceSection: string
    SourceField: string
    DestinationShape: string
    Reason: string
  SentinelCandidates[]
    SourceSection: string
    FieldName: string
    RawValue: string
    Reason: string
  SourceRegistrations[]
    SourceId: string
    SourcePath: string
    KeyCount: int
  ValidationEntries[]
    Severity: ConfigValidationSeverity
    RuleId: string
    Error: ConfigError
    SourceSection: string
    RowId: int
    FieldName: string
    TargetSourceId: string
    Message: string
  GeneratedSourceFiles[]: empty
  RuntimeBytesGenerated: false
  ErrorCount: int
  WarningCount: int
  InfoCount: int
  HasErrors: bool
  HasSentinelCandidates: bool
```

The pass case has `ErrorCount=0`, `WarningCount=0`, and `InfoCount=1` because runtime bytes remain explicitly out of scope.

## Added Coverage

```text
DryRunReferenceReport_WhenAllDryRunKeysExist_PassesWithRegisteredKeys
DryRunReferenceReport_WhenIndexGraphMissing_ReportsError
DryRunReferenceReport_WhenAbilityGraphMissing_ReportsError
DryRunReferenceReport_WhenLocalizationMissing_IsWarningOnly
DryRunReferenceReport_RuntimeBytesBoundary_IsInfoOnly
```

## Verification

```text
dotnet /usr/local/share/dotnet/sdk/9.0.306/Roslyn/bincore/csc.dll @Library/Bee/artifacts/200b0aE.dag/MxFramework.Tests.rsp -out:Temp/AIActionDryRunReferenceReportTestsCompile.dll -refout:Temp/AIActionDryRunReferenceReportTestsCompile.ref.dll -pdb:Temp/AIActionDryRunReferenceReportTestsCompile.pdb
  Result: Passed, 0 errors. Bee response file includes AIActionDryRunFieldMapTests.cs.

dotnet build WGameFramework.sln --no-restore -v minimal
  Result: Blocked by unrelated Preview Runtime compile errors:
    Assets/Scripts/MxFramework/Preview/Runtime/PreviewRpcServer.cs(495,54): CS0103 BuildFailureResultFromAdapter not found.
    Assets/Scripts/MxFramework/Preview/Runtime/PreviewRpcServer.cs(501,24): CS1501 BuildError overload with 5 args not found.
```
