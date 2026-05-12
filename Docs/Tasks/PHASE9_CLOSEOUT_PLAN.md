# Phase 9.0 收尾执行清单

> **状态**: ✅ P9.1-P9.4 已完成，Phase 9.0 Close Review 已接受 / 关闭（详情见 `Docs/Tasks/PHASE9_CLOSEOUT_REPORT.md`）
>
> **Owner**: Framework Producer / Codex Review
>
> **Last Verified**: 2026-05-09

> 目标：把 `Phase 9.0` 从“审计完成但未收口”推进到“可落地迁移输入就绪”。
>
> 范围：只做文档和契约收口，不迁移 WGame 真实业务数据，不进入代码实现。

## 1. 当前状态

`Phase 9.0` 的核心审计文档已具备：

- `Docs/WGAME_DATA_AUDIT.md`
- `Docs/CONFIG_FORMAT_STRATEGY.md`
- `Docs/WGAME_DATA_RELATION_AUDIT.md`
- `Docs/WGAME_SPLIT_GRAPH_AUDIT.md`
- `Docs/WGAME_TABLE_FIELD_INDEX.md`
- `Docs/WGAME_ENUM_MAPPING_AUDIT.md`
- `Docs/Tasks/ABILITY_JSON_AUDIT_RESULT.md`

当前 Phase 9.0 缺口已通过 P9.1-P9.4 收口为可执行迁移输入；剩余风险继续保留为后续实现或项目侧确认项。

## 2. 收尾里程碑

### P9.1 审计结论冻结（Doc Freeze）

状态：已完成（2026-05-09）

任务：

- 在每份审计文档顶部补充 `Status`、`Owner`、`Last Verified` 字段。
- 明确每份文档的“事实结论”和“待确认项”边界，避免后续混写。
- 统一术语：`Table`、`Graph`、`Localization`、`GeneratedRuntime`、`ConfigSourceIndex`。

完成条件：

- 七份核心审计文档都能快速判断“是否可作为输入”。
- 所有待确认项都有唯一追踪条目（不出现“后续补充”这类模糊描述）。

产出：

- 七份核心审计文档头部元数据一致化。
- 实际冻结范围为七份核心审计文档：`WGAME_DATA_AUDIT`、`CONFIG_FORMAT_STRATEGY`、`WGAME_DATA_RELATION_AUDIT`、`WGAME_SPLIT_GRAPH_AUDIT`、`WGAME_TABLE_FIELD_INDEX`、`WGAME_ENUM_MAPPING_AUDIT`、`ABILITY_JSON_AUDIT_RESULT`。

### P9.2 Schema 种子清单（Schema Seed）

状态：已完成（2026-05-09）

任务：

- 在 `Docs/CONFIG_FORMAT_STRATEGY.md` 基础上给出首批 Schema 名单：
  - `AIActionIndex`
  - `AIActionGraph`
  - `AIConfig`
  - `AIConfigDefense`
  - `AIGoals`
  - `BuffIndex`
  - `BuffGraph`
  - `AbilityIndex`
  - `AbilityGraph`
  - `Localization`
- 为每个 Schema 标注 `key`、`structureKind`、`sourcePath`、`referenceRules` 最小集合。

完成条件：

- 每个候选 Schema 都有最小字段契约，不再停留在概念名词。
- 能直接用于下一阶段代码实现的接口建模。

产出：

- `Docs/Interfaces/ConfigSchemaSeeds.md`（新建）。

### P9.3 引用规则白名单（Reference Rules）

状态：已完成（2026-05-09）

任务：

- 基于 `WGAME_DATA_RELATION_AUDIT` 提炼必须先落地的引用规则白名单：
  - `TbAIAction -> AIActionGraph`
  - `AIActionGraph.abilityId -> AbilityGraph.id`
  - `TbCharacterAI.fightAI -> AIConfig.name`（过渡期）
  - `TbBuff.id -> BuffGraph.id`
  - `TalentTree(effectType,effectId) -> polymorphic target`
- 给每条规则定义严重级别：`error/warning/info`。

完成条件：

- 引用规则可直接映射到 `ConfigReferenceRule` 和 `ConfigSourceIndex` 校验。
- 明确过渡期特例（例如 `FatOgre_3`）是 warning 还是 error。

产出：

- `Docs/Interfaces/ConfigReferenceRulesPhase9.md`（新建）。

### P9.4 Phase 9 关闭评审（Close Review）

状态：已完成并接受（2026-05-09）。

任务：

- 汇总 P9.1~P9.3 的完成证据。
- 给出“首个迁移试点准备”推荐和边界。

完成条件：

- `ROADMAP` 中 `Phase 9.0` 已从“进行中”切到“完成 / closed”。
- 下一阶段可以直接进入实现，不需要再次做大范围数据审计。

产出：

- `Docs/Tasks/PHASE9_CLOSEOUT_REPORT.md`（新建）。

## 3. 推荐试点

首个迁移试点准备建议使用 `AIAction` 垂直切片：

- 表层：`TbAIAction` / `AIActionIndex`
- 图层：`SplitAIActionData` / `AIActionGraph`
- 关系：`AIActionGraph -> AbilityGraph`

原因：

- 数据规模适中。
- 跨源关系明确。
- 能完整覆盖 `Table + Graph + Reference` 三种能力。

## 4. 非目标

- 不在本阶段引入 WGame 真实业务配置文件到框架仓库。
- 不在本阶段处理 Runtime Preview 03.5。
- 不在本阶段迁移 GOAPAI 运行时代码。

## 5. 完成记录

2026-05-09：

- P9.1 完成统一元数据与事实/待确认边界补充。
- P9.2 新增 `Docs/Interfaces/ConfigSchemaSeeds.md`。
- P9.3 新增 `Docs/Interfaces/ConfigReferenceRulesPhase9.md`。
- P9.4 接受 `Docs/Tasks/PHASE9_CLOSEOUT_REPORT.md`，Phase 9.0 文档契约范围关闭。
- 未修改代码、Unity 资源或 WGame 真实业务数据。
