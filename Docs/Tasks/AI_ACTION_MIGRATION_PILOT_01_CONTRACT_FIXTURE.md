# AIAction Migration Pilot 01: Contract + Synthetic Fixture

> Status: Implemented / Verified
> Owner: Framework Producer / Codex
> Last Verified: 2026-05-09

## 1. 目标

启动 AIAction 垂直切片迁移试点准备，用最小 synthetic fixture 验证 `Table + Graph + Reference` 契约能在现有 Config 基础设施中落地。

本任务只证明契约形状、索引登记和引用校验可执行，不迁移 WGame 真实业务数据。

## 2. 范围

- 定义 `AIActionIndex` 的最小 Table 字段契约。
- 定义 `AIActionGraph` 的最小 Graph 字段契约。
- 固定 synthetic fixture 的纯 C# 表达方式和源路径命名。
- 验证 `AIActionIndex.id -> AIActionGraph.id`。
- 验证 `AIActionGraph.abilityId -> AbilityGraph.id`。
- 验证缺失目标按 `ConfigReferenceRule.Severity` 产出 `error` 或 `warning`。
- 同步 `ConfigSchemaSeeds.md`、`ConfigReferenceRulesPhase9.md`、`CAPABILITIES.md` 和 `ROADMAP.md` 的入口状态。

## 3. 非目标

- 不导入 `Luban/Configs`、`Assets/Res/BaseDataJson`、`SplitAIActionData` 或 `SplitAbilityData` 的真实数据。
- 不实现 WGame AIAction 完整迁移器。
- 不把旧 `B[]` 位序作为新权威源格式。
- 不修改 Runtime Showcase UI、Combat、Preview 或 WGame 私有代码。
- 不实现完整 Graph JSON parser、TSV parser、Runtime bytes 生成或外部编辑器 UI。

## 4. Schema / Reference 输入

本任务从以下已关闭的 Phase 9 输入开始：

- `Docs/Tasks/PHASE9_CLOSEOUT_REPORT.md`
- `Docs/Interfaces/ConfigSchemaSeeds.md`
- `Docs/Interfaces/ConfigReferenceRulesPhase9.md`
- `Docs/CONFIG_FORMAT_STRATEGY.md`
- `Docs/WGAME_DATA_RELATION_AUDIT.md`
- `Docs/WGAME_SPLIT_GRAPH_AUDIT.md`
- `Docs/WGAME_TABLE_FIELD_INDEX.md`

### 4.1 `AIActionIndex` 最小字段契约

| Field | Type | Required | Rule |
|---|---|---:|---|
| `id` | `int` | yes | Table key; references `AIActionGraph.id`; Phase 9 severity `error`. |
| `nameKey` | `LocalizedTextKey` | yes | Synthetic key only; no real text import. |
| `descKey` | `LocalizedTextKey` | yes | Synthetic key only; no real text import. |
| `canGet` | `bool` | yes | Keeps one visible table flag from the old table shape. |

Synthetic source path: `ConfigSource/Tables/AIActionIndex.tsv`.

### 4.2 `AIActionGraph` 最小字段契约

| Field | Type | Required | Rule |
|---|---|---:|---|
| `id` | `int` | yes | Graph key; target of `AIActionIndex.id`. |
| `name` | `string` | yes | Synthetic internal name. |
| `abilityId` | `int` | yes | References `AbilityGraph.id`; Phase 9 severity `error`. |
| `cost` | `int` | yes | Minimal runtime planning scalar. |
| `cooldownMs` | `int` | yes | Minimal runtime planning scalar. |
| `conditions[]` | object list | yes | Each item may contain `key`, `compare`, `value`; enum validation is follow-up. |
| `effects[]` | object list | yes | Each item may contain `key`, `effect`; enum validation is follow-up. |

Synthetic source path: `ConfigSource/Graphs/AIAction/{id}.json`.

### 4.3 Supporting `AbilityGraph` fixture

The pilot only needs a source index entry for `AbilityGraph` keys:

| Field | Type | Required | Rule |
|---|---|---:|---|
| `id` | `int` | yes | Graph key; target of `AIActionGraph.abilityId`. |

Synthetic source path: `ConfigSource/Graphs/Ability/{graphKind}/{id}.json`.

## 5. Synthetic Fixture Format

The first executable fixture is pure C# in Config tests:

```text
AIActionIndexFixture
  Id = 101
  NameKey = aiaction.synthetic.quick_strike.name
  DescKey = aiaction.synthetic.quick_strike.desc
  CanGet = true

AIActionGraphFixture
  Id = 101
  Name = SyntheticQuickStrike
  AbilityId = 9001
  Cost = 10
  CooldownMs = 1200
  ConditionKey = IsNearTarget
  EffectType = Decrease

ConfigSourceIndex
  Graph:AIActionGraph keys = [101]
  Graph:AbilityGraph keys = [9001]
```

The fixture deliberately does not include real WGame names, real ability ids, real icons, real costs, or old Split JSON arrays.

## 6. 验收标准

- `Docs/Tasks/AI_ACTION_MIGRATION_PILOT_01_CONTRACT_FIXTURE.md` exists and records goal, scope, non-goals, inputs, acceptance, tests, and completion notes.
- `ConfigSchemaSeeds.md` names the AIAction minimum field contract and synthetic source paths.
- `ConfigReferenceRulesPhase9.md` records that both AIAction pilot rules are `error`, and warning severity remains available for transition bridges.
- Pure C# tests prove:
  - `AIActionIndex.id -> AIActionGraph.id` passes when target key exists.
  - missing `AIActionGraph.id` reports `ConfigValidationSeverity.Error`.
  - `AIActionGraph.abilityId -> AbilityGraph.id` passes when target key exists.
  - missing `AbilityGraph.id` reports `ConfigValidationSeverity.Error`.
  - a synthetic transition rule with `ConfigValidationSeverity.Warning` reports a warning and does not set `HasErrors`.
- No real WGame data file is added.
- Runtime Showcase UI, Combat, and Preview files remain untouched by this task.

## 7. 测试要求

- Prefer `dotnet build WGameFramework.sln` when generated Unity projects are present.
- If Unity project references prevent `dotnet build`, run the relevant Config EditMode tests in Unity or document the blocker.
- Minimum target test class: `Assets/Scripts/MxFramework/Tests/Config/ConfigSourceIndexTests.cs`.

## 8. 完成记录

- Added the task contract and synthetic fixture specification.
- Added optional severity to `ConfigReferenceRule` and made `ConfigSourceIndex` / table reference validation honor it.
- Added AIAction pilot synthetic fixture tests to `ConfigSourceIndexTests`.
- No WGame real business data was imported.
