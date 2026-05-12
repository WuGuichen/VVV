# Phase 9 Config Schema Seeds

> Status: Phase 9 seed contract
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09

## 1. Purpose

This document lists the first Schema seeds extracted from the Phase 9 WGame audits. It is an interface contract input, not a data migration.

Boundaries:

- `sourcePath` names the proposed new `ConfigSource` authority path.
- `legacyEvidence` names the WGame source audited for the seed.
- `referenceRules` lists only the minimum rules needed for the first implementation pass.
- No WGame real business data is copied into this document.

## 2. Seed List

| Schema | key | structureKind | sourcePath | legacyEvidence | referenceRules |
|---|---|---|---|---|---|
| `AIActionIndex` | `id` | `Table` | `ConfigSource/Tables/AIActionIndex.tsv` | `Luban/Configs/Datas/#AIAction.xlsx`, `Assets/Res/BaseDataJson/{locale}/tbaiaction.json` | `id -> AIActionGraph.id` required; `nameKey/descKey -> Localization.key` optional when localized fields are present |
| `AIActionGraph` | `id` | `Graph` | `ConfigSource/Graphs/AIAction/{id}.json` | `Assets/Res/SplitAIActionData/*.json` | `abilityId -> AbilityGraph.id` required when non-empty; `conditions[].key/effects[].key -> enum:GOAPWorldKey`; `conditions[].compare -> enum:GOAPCompareType`; `effects[].effect -> enum:GOAPEffectType` |
| `AIConfig` | `name` | `Graph` | `ConfigSource/Graphs/AI/Config/{name}.json` | `Assets/Res/SplitAIConfigData/AIConfig/*.json` | `actions[] -> AIActionGraph.id` required; `name` unique; `TbCharacterAI.fightAI -> AIConfig.name` transition rule |
| `AIConfigDefense` | `name` | `Graph` | `ConfigSource/Graphs/AI/Defense/{name}.json` | `Assets/Res/SplitAIConfigData/AIConfig_Defense/*.json` | `actions[] -> AIActionGraph.id` required; `name` unique; `TbCharacterAI.reactAI -> AIConfigDefense.name` transition rule |
| `AIGoals` | `name` | `Graph` | `ConfigSource/Graphs/AI/Goals/{name}.json` | `Assets/Res/SplitAIConfigData/AIGoals/*.json` | `goalFight[].key/goalReact[].key -> enum:GOAPWorldKey`; `goalFight[].compare/goalReact[].compare -> enum:GOAPCompareType`; `TbCharacterAI.goal -> AIGoals.name` transition rule |
| `BuffIndex` | `id` | `Table` | `ConfigSource/Tables/BuffIndex.tsv` | `Luban/Configs/Datas/#Buff.xlsx`, `Assets/Res/BaseDataJson/{locale}/tbbuff.json` | `id -> BuffGraph.id` required for visible Buff rows; `nameKey/descKey -> Localization.key` optional when localized fields are present |
| `BuffGraph` | `id` | `Graph` | `ConfigSource/Graphs/Buff/{id}.json` | `Assets/Res/SplitBuffData/*.json` | `id` unique; `type -> enum:BuffType`; `target -> enum:BuffTargetType`; `stacking -> enum:BuffAddType`; graph-only ids allowed with `visibility=internal` |
| `AbilityIndex` | `id` | `Table` | `ConfigSource/Tables/AbilityIndex.tsv` | `Assets/Res/SplitAbilityData/**/_index.json`, `Assets/Res/SplitAbilityData/**/*.json` | `id -> AbilityGraph.id` required for indexed Ability rows; `graphKind` distinguishes `TestGroup`, `SkillData`, `MapTriggerData`, `MapSettingData` during transition |
| `AbilityGraph` | `id` | `Graph` | `ConfigSource/Graphs/Ability/{graphKind}/{id}.json` | `Assets/Res/SplitAbilityData/**/*.json` | `events[].type -> enum:EventDataType`; event common fields required; event data must be schema-bound per event type before migration |
| `Localization` | `key` | `Localization` | `ConfigSource/Tables/Localization.tsv` | `Luban/Configs/Datas/#lang.xlsx`, `Assets/Res/BaseDataJson/{locale}/*.json` | localized table fields use `nameKey`, `descKey`, or field-specific `*Key` references; BaseDataJson localized text is legacy export evidence, not authority |

## 3. Minimum Structure Expectations

### Table

- Must have a unique `key` field.
- Should keep visible, sortable, and localizable fields.
- Must not embed Graph arrays or old Split JSON positional payloads.
- May reference Graph, Localization, enum domains, and resource addresses.

### Graph

- Must have a unique `key` field.
- Must use semantic field names, not old short keys such as `B`, `P`, `E`, `C`, `T`, `D`, `N`, `S`, or positional arrays as authority.
- May retain ordered lists when order is semantic, such as Ability events or AIAction conditions.
- Must declare cross-source references in `referenceRules`.

### Localization

- Must be edited as independent authority data.
- Business Tables and Graphs should reference Localization keys rather than storing per-locale text.
- `BaseDataJson/{locale}` remains legacy runtime export evidence only.

## 4. AIAction Pilot Minimum Contract

The first migration pilot uses only synthetic fixture data. It must not import WGame real `#AIAction.xlsx`, `tbaiaction.json`, `SplitAIActionData`, or `SplitAbilityData` rows.

### `AIActionIndex`

Minimum fields:

| Field | Type | Required | Notes |
|---|---|---:|---|
| `id` | `int` | yes | Table key and required reference to `AIActionGraph.id`. |
| `nameKey` | `LocalizedTextKey` | yes | Synthetic key, not localized text copied from WGame. |
| `descKey` | `LocalizedTextKey` | yes | Synthetic key, not localized text copied from WGame. |
| `canGet` | `bool` | yes | Minimal visible table flag retained for table-shape coverage. |

### `AIActionGraph`

Minimum fields:

| Field | Type | Required | Notes |
|---|---|---:|---|
| `id` | `int` | yes | Graph key and target of `AIActionIndex.id`. |
| `name` | `string` | yes | Synthetic internal name. |
| `abilityId` | `int` | yes | Required reference to `AbilityGraph.id`. |
| `cost` | `int` | yes | Minimal planning scalar. |
| `cooldownMs` | `int` | yes | Minimal planning scalar. |
| `conditions[]` | object list | yes | Items use semantic fields `key`, `compare`, `value`; enum-domain validation is follow-up. |
| `effects[]` | object list | yes | Items use semantic fields `key`, `effect`; enum-domain validation is follow-up. |

The pilot source index only needs to register synthetic keys for `Graph:AIActionGraph` and `Graph:AbilityGraph`.
