# Phase 9 Config Reference Rules

> Status: Phase 9 seed contract
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09

## 1. Purpose

This document freezes the first reference-rule whitelist for `ConfigReferenceRule` and `ConfigSourceIndex` implementation. It is scoped to rules evidenced by the Phase 9 audits and does not imply full WGame data migration.

Severity meanings:

| Severity | Meaning |
|---|---|
| `error` | The referenced key must exist for the migrated source to be accepted. |
| `warning` | The reference is known to have transition exceptions or requires project confirmation; it must be reported but should not block the first schema-index build. |
| `info` | The relation should be indexed for visibility and impact analysis, but is not a blocking validation rule in Phase 9. |

## 2. Whitelist

| Rule Id | Source | Target | Severity | Phase 9 handling |
|---|---|---|---|---|
| `phase9.aiaction.index.graph` | `AIActionIndex.id` | `AIActionGraph.id` | `error` | `TbAIAction` rows `1-499` are expected to have matching AIAction Graphs. `AIActionGraph.id=0` is Graph-only and is allowed outside the table. |
| `phase9.aiaction.ability` | `AIActionGraph.abilityId` | `AbilityGraph.id` | `error` | `SplitAIActionData.B[4]` maps to Ability Graph ids. Empty or sentinel values must be modeled explicitly before migration; observed audited references had no missing target. |
| `phase9.characterai.fightai` | `TbCharacterAI.fightAI` | `AIConfig.name` | `warning` | Transitional string-name bridge. `FatOgre_3` is an observed missing target and must remain a warning until WGame project ownership confirms whether it is stale, missing, or intentionally disabled. |
| `phase9.buff.index.graph` | `BuffIndex.id` | `BuffGraph.id` | `error` | Visible `TbBuff` rows must have matching Buff Graphs. Buff Graph-only ids are allowed with `visibility=internal` and must not be treated as missing table rows. |
| `phase9.talent.effect.polymorphic` | `TalentTree(effectType,effectId)` | polymorphic target | `error` | `effectType` selects the target schema for `effectId`; supported Phase 9 targets are `TalentEffect`, `TalentAttribute`, `Blessing`, `AIActionIndex`, `CharacterSkill`, and `Character`. |

## 3. Polymorphic Target Map

| effectType | Target Schema | Target Key | Severity |
|---|---|---|---|
| `TalentEffect` or legacy `0` | `TalentEffect` | `id` | `error` |
| `TalentAttribute` or legacy `1` | `TalentAttribute` | `id` | `error` |
| `Blessing` or legacy `2` | `Blessing` | `id` | `error` |
| `AIAction` or legacy `3` | `AIActionIndex` | `id` | `error` |
| `CharacterSkill` or legacy `4` | `CharacterSkill` | `id` | `error` |
| `Character` or legacy `5` | `Character` | `id` | `error` |

## 4. ConfigReferenceRule Shape

The implementation can map each whitelist entry into a shape equivalent to:

```text
ConfigReferenceRule
  id
  sourceSchemaName
  sourceField
  sourceStructureKind
  targetSchemaName
  targetKeyField
  targetStructureKind
  required
  severity
  transitionNote
```

For polymorphic references, `targetSchemaName` is selected by `effectType` before validating `effectId`.

## 5. AIAction Pilot Fixture Rules

The first executable pilot fixture maps the Phase 9 whitelist into synthetic `ConfigReferenceRule` instances:

| Rule Id | Source Field | Target Source | Required | Severity | Synthetic handling |
|---|---|---|---:|---|---|
| `pilot.aiaction.index.graph` | `AIActionIndex.id` | `Graph:AIActionGraph.Id` | yes | `error` | Missing graph key blocks the fixture. |
| `pilot.aiaction.ability` | `AIActionGraph.abilityId` | `Graph:AbilityGraph.Id` | yes | `error` | Missing ability graph key blocks the fixture. |
| `pilot.transition.warning` | synthetic transition bridge field | synthetic legacy graph name | yes | `warning` | Used only to prove that missing transition targets can report without setting `HasErrors`. |

The warning fixture is not an AIAction migration rule. It exists to verify the shared warning/error contract before later transition bridges such as `TbCharacterAI.fightAI -> AIConfig.name` are implemented.

## 6. Boundaries

- This whitelist is not the full WGame relation graph.
- `reactAI`, `goal`, `AIConfig.actions[]`, Buff-internal references, SkillData graph-only ids, MapGraph, and Localization key checks are valid follow-up rules but are not required to close P9.3.
- Missing target counts are only those recorded in the Phase 9 audit documents. No external or fresh WGame project validation was performed for this closeout.
