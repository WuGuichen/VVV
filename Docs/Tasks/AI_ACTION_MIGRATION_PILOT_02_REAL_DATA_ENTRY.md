# AIAction Migration Pilot 02: Real Data Entry Design

> Status: Planned / Ready for implementation
> Owner: Framework Producer / Codex
> Created: 2026-05-09

## 1. Goal

Define the first real-data entry plan for AIAction migration after Pilot 01 proved the synthetic `Table + Graph + Reference` contract.

This task is an implementation contract only. It defines source inputs, normalized outputs, migration boundaries, ordering, acceptance gates, and parallelization. It does not implement TSV parsing, JSON parsing, loader integration, runtime bytes generation, or real WGame data import.

## 2. Starting Point

Pilot 01 has already established:

- `AIActionIndex.id -> AIActionGraph.id` is a blocking `error` rule.
- `AIActionGraph.abilityId -> AbilityGraph.id` is a blocking `error` rule.
- `ConfigReferenceRule.Severity` can distinguish `error` from `warning`.
- Synthetic fixtures can register `Graph:AIActionGraph` and `Graph:AbilityGraph` keys through `ConfigSourceIndex`.

Pilot 02 starts from that contract and defines how the first real-data entry should be staged without letting legacy WGame formats become the new authority shape.

## 3. Scope

- Define the real AIAction source entry points.
- Define the normalized target files and schema boundaries.
- Define what must be parsed first and what must remain blocked.
- Define the reference validation severity rules for the first real-data pass.
- Define the relationship between `AIActionIndex`, `AIActionGraph`, and `AbilityGraph`.
- Define acceptance gates for documentation, dry-run import, validation, and follow-up implementation.
- Split work into tasks that can run in parallel and tasks that must remain serial.

## 4. Non-Goals

- Do not import real WGame rows into this repository in this task.
- Do not add `ConfigSource/Tables/AIActionIndex.tsv`.
- Do not add `ConfigSource/Graphs/AIAction/*.json`.
- Do not add `ConfigSource/Graphs/Ability/**`.
- Do not implement TSV parser code.
- Do not implement Graph JSON parser code.
- Do not implement runtime loader or runtime bytes generation.
- Do not change Config runtime code.
- Do not modify Preview Runtime, Phase 12 Showcase, Combat, or WGame private project files.
- Do not preserve old Split JSON short keys or positional arrays as the new authority format.

## 5. Real Data Inputs

Pilot 02 recognizes these legacy inputs as evidence sources only:

| Legacy Input | Role | Pilot 02 Handling |
|---|---|---|
| `Luban/Configs/Datas/#AIAction.xlsx` | Table authoring evidence for AIAction rows. | Map only to the future `AIActionIndex.tsv` contract; do not copy data. |
| `Assets/Res/BaseDataJson/{locale}/tbaiaction.json` | Legacy localized runtime export evidence. | Use only to confirm localizable fields; do not treat localized text as source authority. |
| `Assets/Res/SplitAIActionData/*.json` | Legacy AIAction graph payload evidence. | Map fields into the future semantic `AIActionGraph` shape; do not keep short-key arrays as authority. |
| `Assets/Res/SplitAbilityData/**/*.json` | Ability Graph key evidence for `abilityId` references. | Use only to build or validate `AbilityGraph.id` key availability. |

These inputs are not added to WGameFramework. Any future importer must read them from an external source path or from a controlled fixture copy that contains no WGame business payload unless explicitly approved.

## 6. Normalized Outputs

The first real-data pass should target these framework-owned authority shapes:

| Output | Path | Authority Rule |
|---|---|---|
| `AIActionIndex` | `ConfigSource/Tables/AIActionIndex.tsv` | Human-editable table with stable `id`, localized key references, and table-only flags. |
| `AIActionGraph` | `ConfigSource/Graphs/AIAction/{id}.json` | Semantic graph JSON keyed by `id`, with named scalar fields and named condition/effect lists. |
| `AbilityGraph` key index | `ConfigSource/Graphs/Ability/{graphKind}/{id}.json` | Only key registration is required for Pilot 02 validation; full Ability migration is outside this task. |
| Validation report | implementation-defined report artifact | Must list missing references, warning bridges, skipped rows, and unsupported legacy fields. |

Pilot 02 does not require the output files to exist. It defines the contract that the next implementation task must satisfy.

## 7. Field Mapping Boundary

### 7.1 `AIActionIndex.tsv`

Required normalized fields:

| Field | Source Evidence | Rule |
|---|---|---|
| `id` | `#AIAction.xlsx` row id / `tbaiaction.json` id | Primary key; must match `AIActionGraph.id`. |
| `nameKey` | localized field evidence | Localization key only; no copied localized text. |
| `descKey` | localized field evidence | Localization key only; no copied localized text. |
| `canGet` | table flag evidence | Boolean table field retained from Pilot 01 contract. |

Unsupported or unresolved table fields must be recorded in the dry-run report rather than silently imported.

### 7.2 `AIActionGraph.json`

Required normalized fields:

| Field | Source Evidence | Rule |
|---|---|---|
| `id` | `SplitAIActionData` graph identity | Graph key; target of `AIActionIndex.id`. |
| `name` | graph debug/name evidence if available | Internal semantic name; must not rely on localized display text. |
| `abilityId` | legacy AIAction ability reference, audited as `B[4]` in Phase 9 | Required `error` reference to `AbilityGraph.id` unless an explicit sentinel model is approved. |
| `cost` | legacy planning scalar evidence | Required for the first real-data pass if present in the audited shape; missing values must be reported. |
| `cooldownMs` | legacy planning scalar evidence | Required normalized millisecond field; unit conversion must be explicit. |
| `conditions[]` | legacy condition list evidence | Semantic objects only: `key`, `compare`, `value`. |
| `effects[]` | legacy effect list evidence | Semantic objects only: `key`, `effect`. |

Legacy short keys, positional arrays, and opaque payloads may be parsed as input but must not become the new ConfigSource authority format.

### 7.3 `AbilityGraph` References

Pilot 02 only needs an `AbilityGraph.id` availability set.

- Full Ability schema migration remains a separate task.
- `AbilityGraph.id` can be registered from existing future AbilityGraph source entries, an external dry-run scan, or a controlled synthetic fixture.
- Missing non-empty `abilityId` values remain `error`.
- Empty, zero, or sentinel values are not accepted by default. They require a documented sentinel rule before they can be downgraded.

## 8. Reference Severity Rules

| Rule Id | Source | Target | Severity | Pilot 02 Handling |
|---|---|---|---|---|
| `pilot02.aiaction.index.graph` | `AIActionIndex.id` | `AIActionGraph.id` | `error` | A visible table row without a matching graph blocks acceptance. |
| `pilot02.aiaction.ability` | `AIActionGraph.abilityId` | `AbilityGraph.id` | `error` | A non-empty ability reference without a registered AbilityGraph key blocks acceptance. |
| `pilot02.aiaction.localization` | `AIActionIndex.nameKey/descKey` | `Localization.key` | `warning` | Localization authority is not part of Pilot 02; missing keys are reported but do not block dry-run shape validation. |
| `pilot02.aiaction.enum-domain` | `conditions[].key`, `conditions[].compare`, `effects[].effect` | enum domains | `warning` | Enum-domain generation is a follow-up; unknown values must be surfaced in the report. |
| `pilot02.aiaction.runtime-bytes` | normalized ConfigSource | runtime bytes | `info` | Runtime bytes are explicitly not generated in Pilot 02. |

Severity may only be downgraded from `error` to `warning` when the task document names the transition exception and the validation report preserves the missing target.

## 9. Runtime Bytes Boundary

Runtime bytes are out of scope for Pilot 02.

The next implementation must stop at normalized source and validation output:

```text
legacy evidence scan
  -> dry-run normalized AIActionIndex / AIActionGraph model
  -> ConfigSourceIndex registration
  -> reference validation report
  -> no runtime bytes
```

Do not add a `GeneratedRuntime` source, binary encoder, runtime loader, or player-facing integration until the normalized source contract has one accepted real-data dry run.

## 10. Required Ordering

These steps must remain serial:

1. Confirm legacy source availability and allowed source root.
2. Define parser field map for `AIActionIndex` and `AIActionGraph`.
3. Build dry-run normalized model in memory or test fixture.
4. Register `AIActionGraph` and `AbilityGraph` keys in `ConfigSourceIndex`.
5. Run blocking reference validation.
6. Produce validation report.
7. Only after acceptance, plan source-file emission.
8. Only after source-file emission is accepted, plan runtime bytes.

The parser field map must precede source-file emission. Runtime bytes must not start until normalized ConfigSource files and validation reports have been accepted.

## 11. Parallelizable Work

These tasks can run in parallel after this document is accepted:

| Track | Can Start When | Output |
|---|---|---|
| TSV field-map design | Immediately | `AIActionIndex` field map and unsupported-field report contract. |
| Graph JSON field-map design | Immediately | `AIActionGraph` semantic field map and short-key translation notes. |
| AbilityGraph key-source design | Immediately | Minimal key registration strategy for `AbilityGraph.id`. |
| Validation report format | Immediately | Report schema for errors, warnings, skipped rows, unsupported fields, and sentinel candidates. |
| Test fixture planning | Immediately | Dry-run tests that do not include WGame real payloads. |

These tracks must converge before implementation changes Config runtime or emits ConfigSource files.

## 12. Acceptance Gates

Pilot 02 is accepted when:

- This task document exists and names the real data entry boundaries.
- The design keeps TSV, JSON Graph, AbilityGraph references, severity, and runtime bytes boundaries separate.
- No real WGame data files are added.
- No parser, loader, runtime bytes, Preview Runtime, Phase 12 Showcase, Combat, or Config runtime code is changed.
- A future implementation can identify:
  - which legacy inputs are evidence only,
  - which normalized outputs are allowed,
  - which references are `error`,
  - which references are `warning`,
  - which steps are serial,
  - which work can run in parallel.
- `svn status` shows only this task document for the Pilot 02 task unless a producer explicitly approves shared roadmap updates.

## 13. Suggested Next Implementation Tasks

1. `AI_ACTION_MIGRATION_PILOT_03_DRY_RUN_FIELD_MAP`
   - Implement no-file-emission field-map tests using tiny synthetic or externally supplied sample rows.
   - Output unsupported-field and sentinel-candidate report entries.

2. `AI_ACTION_MIGRATION_PILOT_04_DRY_RUN_REFERENCE_REPORT`
   - Register `AIActionGraph` and `AbilityGraph` keys in a dry-run index.
   - Produce error/warning/info counts without writing runtime bytes.

3. `AI_ACTION_MIGRATION_PILOT_05_SOURCE_EMISSION_PLAN`
   - Only after dry-run reports pass, define controlled ConfigSource file emission and review gates.

## 14. Completion Notes

- Pilot 02 intentionally stops at documentation and migration entry design.
- Runtime bytes remain blocked behind accepted normalized source emission.
- Localization and enum-domain validation remain warning-level visibility checks until their authority sources are defined.
