# Phase 9.0 Closeout Report

> Status: Accepted / Closed
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09

## 1. Scope

This report closes the documentation-contract portion of Phase 9.0 as accepted on 2026-05-09. It does not migrate WGame real business data, add code, add Unity assets, or resolve the remaining follow-up risks listed below.

## 2. Completion Evidence

| Item | Status | Evidence |
|---|---|---|
| P9.1 Audit conclusion freeze | Complete | Added unified `Status`, `Owner`, `Last Verified`, fact conclusions, and pending-confirmation boundaries to the Phase 9 core audit documents. |
| P9.2 Schema seed list | Complete | Added `Docs/Interfaces/ConfigSchemaSeeds.md` with seeds for `AIActionIndex`, `AIActionGraph`, `AIConfig`, `AIConfigDefense`, `AIGoals`, `BuffIndex`, `BuffGraph`, `AbilityIndex`, `AbilityGraph`, and `Localization`. |
| P9.3 Reference-rule whitelist | Complete | Added `Docs/Interfaces/ConfigReferenceRulesPhase9.md` with severity definitions and the required Phase 9 whitelist rules. |
| P9.4 Close review acceptance | Complete | This document summarizes evidence, first migration pilot recommendation, boundaries, and accepted closeout status. |

Core audit documents frozen for Phase 9 input:

- `Docs/WGAME_DATA_AUDIT.md`
- `Docs/CONFIG_FORMAT_STRATEGY.md`
- `Docs/WGAME_DATA_RELATION_AUDIT.md`
- `Docs/WGAME_SPLIT_GRAPH_AUDIT.md`
- `Docs/WGAME_TABLE_FIELD_INDEX.md`
- `Docs/WGAME_ENUM_MAPPING_AUDIT.md`
- `Docs/Tasks/ABILITY_JSON_AUDIT_RESULT.md`

## 3. First Migration Pilot Preparation Recommendation

Recommended preparation target: `AIAction` vertical slice.

Pilot boundary:

- Table: `AIActionIndex`
- Graph: `AIActionGraph`
- Required references: `AIActionIndex.id -> AIActionGraph.id`, `AIActionGraph.abilityId -> AbilityGraph.id`
- Supporting index: `ConfigSourceIndex`
- Supporting contract: `ConfigReferenceRule`

Reasoning:

- `AIAction` has stable table-to-graph coverage for `TbAIAction` rows `1-499`.
- `AIActionGraph.id=0` is an already identified Graph-only exception.
- `AIActionGraph.abilityId` has audited AbilityGraph coverage with no missing target in the recorded evidence.
- The slice exercises `Table + Graph + Reference` contract preparation without importing WGame real business data.

## 4. Boundaries and Risks

Not closed by this report:

- `FatOgre_3` remains an observed `TbCharacterAI.fightAI -> AIConfig.name` missing target and is a `warning` until WGame project ownership confirms it.
- `SkillData` and `BuffGraph` Graph-only entries must be modeled explicitly; they are not automatic errors.
- Buff-internal references, `CastOrb.HitSkill`, MapGraph, full enum-domain generation, Localization key validation, and Runtime bytes encoding remain follow-up implementation or audit work.
- Closing Phase 9 only accepts the documentation-contract scope. It does not imply that downstream implementation, validator coverage, or WGame project data ownership decisions are complete.

## 5. Next Implementation Inputs

The next phase can start from:

- `Docs/Interfaces/ConfigSchemaSeeds.md`
- `Docs/Interfaces/ConfigReferenceRulesPhase9.md`
- `Docs/CONFIG_FORMAT_STRATEGY.md`
- `Docs/WGAME_DATA_RELATION_AUDIT.md`
- `Docs/WGAME_SPLIT_GRAPH_AUDIT.md`

Recommended first implementation order:

1. Define in-code or schema-file representation for `ConfigSchema`, `ConfigReferenceRule`, and `ConfigSourceIndex`.
2. Implement an index-only validator over sample authoring sources, without importing WGame real data.
3. Add a synthetic AIAction pilot fixture that proves table-to-graph and graph-to-Ability reference validation.
