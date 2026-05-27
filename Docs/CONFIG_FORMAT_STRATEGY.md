# 配置源与运行时格式策略

> **本文涉及的 AI 概念属于 AIAction Config 域**（AIAction/AIConfig/Buff 的配置格式策略）。详见 `Docs/INTERFACES.md` 的 AI Terminology 章节。

> Status: Archive
> Archived state: Frozen for Phase 9 closeout input
> Owner: Framework Producer / Codex Review
> Last Verified: 2026-05-09
>
> Fact Conclusions: 本文冻结 `Table`、`Graph`、`Localization`、`GeneratedRuntime`、`ConfigSourceIndex` 的分层策略；策略基于已完成审计文档，不代表已完成代码实现或真实数据迁移。
>
> Pending Confirmation: Schema JSON 的最终文件格式、生成器 API、Runtime bytes 编码和编辑器交互仍待实现阶段确认；本文不把旧 Luban、Excel、BaseDataJson 或 Split JSON 格式直接升级为运行时契约。

> 目标：编辑时易读、易审查、AI 友好；运行时统一、稳定、高效。
>
> 本文是策略草案。真实 WGame 数据结构必须先经过 `Docs/WGAME_DATA_AUDIT.md` 逐类汇总；Ability JSON 细节分析结果见 `Docs/Tasks/ABILITY_JSON_AUDIT_RESULT.md`。

## 1. 结论

MxFramework 不把 TSV、JSON、Excel、Luban、ScriptableObject 或 bytes 任何一种格式直接作为运行时契约。

推荐分层是：

```text
Authoring Source（权威源）
  Tables/*.tsv
  Graphs/*.json
  Schemas/*.schema.json

Build Pipeline
  validate
  normalize
  generate

Runtime Data（唯一运行时格式）
  RuntimeConfig.bytes
  RuntimeConfigIndex

Runtime API
  IConfigProvider
  IConfigTable<T>
  IConfigRegistry
```

核心规则：

- 编辑源可以按数据形态选择 TSV 或 JSON。
- 运行时只读取统一生成产物，不直接读取编辑源。
- bytes 是构建产物，不是人工编辑源。
- Schema 是中心，负责字段、类型、引用、多语言、默认值和校验规则。
- AI Agent 只修改权威源，不修改运行时产物。
- 旧数据关系必须先归一成结构模型，再生成运行时产物；不要把 WGame 旧字段位序、混合 JSON 或 Luban 约定直接暴露给新编辑器。

## 2. WGame 当前数据来源审计

当前 WGame 数据来源存在混合状态：

| 来源 | 代表路径 | 观察结果 | 新策略 |
|------|----------|----------|--------|
| Luban Excel | `Luban/Configs/Datas/*.xlsx` | 存在角色、Buff、AIAction、Talent、Entry、多语言等表 | 迁移为表格型权威源，优先 TSV |
| Luban 定义 | `Luban/Configs/Defines/builtin.xml` | 类型定义和生成配置由 Luban 管理 | 迁移为 Schema，不绑定 Luban |
| BaseData JSON | `Assets/Res/BaseDataJson/{locale}/` | 运行时或热更侧已有大量 JSON | 作为历史导出产物审计，不直接作为新权威源 |
| Ability JSON | `Assets/Res/SplitAbilityData/**.json` | 大量技能、动作、地图触发等结构型数据 | 保留结构型权威源思路，重整为 Graph JSON |
| AI JSON | `Assets/Res/SplitAIConfigData/**.json`、`Assets/Res/SplitAIActionData/**.json` | AI 行为、目标、动作数量较多 | 结构型走 Graph JSON，索引和基础定义走 TSV |
| Buff JSON | `Assets/Res/SplitBuffData/**.json` | Buff 行为和参数较多 | 基础定义拆 TSV，复杂效果链走 Graph JSON |
| SaveData JSON/bytes | `Assets/Res/SaveData/`、`StreamingAssets/SaveData/` | 玩家存档和运行时数据读写混杂 | 不纳入配置权威源，只纳入存档系统 |
| Runtime bytes | `*.bytes` | 当前存在运行时或资源加载产物 | 保留为生成目标，不允许人工编辑 |

粗略规模：

- `Assets` 下约数千个 JSON，其中配置相关集中在 `Assets/Res/SplitAbilityData`、`SplitAIActionData`、`SplitAIConfigData`、`SplitBuffData`、`BaseDataJson`。
- `Assets` 下存在数百个 bytes，应该视为运行时或资源产物。
- `Luban/Configs/Datas` 下存在多张 Excel 表，是表格型数据迁移的重点来源。

## 3. 格式选择矩阵

| 数据形态 | 权威源格式 | 原因 | 示例 |
|----------|------------|------|------|
| 行列表格 | TSV | diff 清楚、批量编辑容易、AI 修改稳定 | Buff 基础定义、Modifier 定义、物品、掉落、多语言 key、引用关系 |
| 复杂结构 | JSON | 保留层级和数组结构，避免把树压平成难读表格 | Ability 时间轴、AI 行为图、条件组、效果链 |
| 类型和校验 | Schema JSON | 工具可解析，能描述字段和引用规则 | `*.schema.json` |
| 运行时产物 | bytes | 加载快、体积可控、不可人工编辑 | `RuntimeConfig.bytes` |
| 编辑器资产引用 | TSV/JSON 中保存资源地址或 GUID | 避免 ScriptableObject 成为配置真源 | 图标、特效、动画、音频引用 |

TSV 不是所有配置的唯一格式。它只适合作为表格型权威源。复杂结构必须允许 JSON。

## 4. 推荐目录

```text
ConfigSource/
  Schemas/
    Buff.schema.json
    AbilityGraph.schema.json
    AIAction.schema.json

  Tables/
    Buff.tsv
    Modifier.tsv
    Attribute.tsv
    Item.tsv
    Localization.tsv
    AbilityIndex.tsv
    AIActionIndex.tsv

  Graphs/
    Ability/
      0001_LongSword_Attack.json
    AI/
      0001_LongSword.json
    Buff/
      0001_BurnEffect.json

Generated/
  Config/
    RuntimeConfig.bytes
    RuntimeConfigIndex.json
    RuntimeConfig.hash
```

目录职责：

- `ConfigSource` 是权威源，进入 SVN，允许人工和 AI 修改。
- `Generated/Config` 是构建产物，可按项目策略决定是否提交。
- Unity 运行时只读 `Generated/Config`。
- Framework Manager 和提交前检查优先读取 `ConfigSource` 并验证生成结果一致性。

## 5. Schema 职责

Schema 必须至少描述：

- 表名或图类型。
- 数据结构形态：`Table`、`Graph`、`Localization`、`GeneratedRuntime`。
- 字段名、字段类型、是否必填、默认值。
- ID 范围和唯一性。
- 跨表引用规则。
- 跨源引用规则，例如表格索引到 Graph、字符串配置名到结构数据。
- 多态引用规则，例如 `effectType + effectId` 根据类型指向不同目标表。
- 多语言 key 规则。
- 枚举和 flags 类型。
- 枚举显示名、别名、flags 组合解析规则。
- 资源引用字段。
- 生成到运行时格式的模块名。

枚举和 flags 不应散落在字段说明里。统一结构是：

```text
ConfigEnumRegistry
  ConfigEnumDomain(enumId, isFlags)
    ConfigEnumValue(value, name, displayName, description)

ConfigField(enumId)
```

字段只引用 `enumId`，例如 `weapon.Type`、`AI.ActionType`、`BuffType`。这样 UI Toolkit 编辑器、AI 摘要、校验器和迁移器都能共享同一份枚举域。

跨源引用不应继续依赖文件名约定。统一结构是：

```text
ConfigReferenceRule
  fieldName
  targetSchemaName
  targetStructureKind
  targetKeyField
  required
```

例子：

```text
AIActionIndex.GraphId -> Graph:AIActionGraph.Id
BuffIndex.GraphId -> Graph:BuffGraph.Id
LocalizationKey -> Localization:Localization.Key
```

运行时类型引用仍可使用 `ConfigReferenceRule(fieldName, typeof(TargetConfig))`。这类引用可以直接通过 `IConfigProvider` 校验；Graph/Localization 等跨源引用进入 Schema 和 AI 摘要，并由 `ConfigSourceIndex` 统一校验存在性。

`ConfigSourceIndex` 的职责：

- 登记每个配置源的 `ConfigSchema`、结构类型、key 字段、源路径和内容 hash。
- 登记每个源包含的 key，例如 Graph id、多语言 key、运行时模块名。
- 根据 `ConfigReferenceRule` 校验表格行是否引用了存在的跨源 key。
- 给后续统计、变动影响分析、Editor 面板和 AI 上下文提供同一份索引。

它不负责：

- 解析 TSV、JSON、Excel 或 Luban。
- 生成运行时 bytes。
- 注入 WGame 业务规则。

Schema 不应包含：

- WGame 业务逻辑。
- Unity 场景对象。
- Luban 专有语义。
- 运行时缓存结构。

## 6. 表格型数据拆分规则

适合 TSV：

- 一行一个 ID。
- 字段数量稳定。
- 主要是数值、枚举、字符串 key、资源引用、跨表引用。
- 需要批量排序、筛选、比较、AI 批量修改。

不适合 TSV：

- 一行里有深层嵌套数组。
- 条件和效果需要多层组合。
- 时间轴、状态机、行为树、GOAP 图。
- 同一 ID 下有多段复杂事件序列。

拆分建议：

| 旧形态 | 新形态 |
|--------|--------|
| 巨大 Buff JSON | `Buff.tsv` 保存基础字段，`Graphs/Buff/*.json` 保存复杂效果链 |
| Ability 单文件大结构 | `AbilityIndex.tsv` 保存 ID、名称、资源引用，`Graphs/Ability/*.json` 保存时间轴 |
| AIAction 混合字段 | `AIActionIndex.tsv` 保存基础可见字段，`Graphs/AI/*.json` 保存 GOAP/行为结构 |
| 多语言散落在业务表 | `Localization.tsv` 独立管理，业务表只引用 key |

Split Graph 的字段位序审计见 `Docs/WGAME_SPLIT_GRAPH_AUDIT.md`。AIAction、AIConfig、Buff 不能继续沿用旧数组位序作为权威源；迁移器可以读取旧位序，但新源必须使用具名字段和类型化 Schema。

结构优化时应按关系密度拆分：

| 旧问题 | 新结构 |
|--------|--------|
| 表格字段直接塞复杂数组 | 表格只保存索引、展示、资源、基础数值 |
| JSON 里短字段和数组位序混用 | Graph JSON 使用具名节点、具名事件、具名参数 |
| 枚举值以裸整数散落 | 字段引用 `enumId`，枚举域独立注册 |
| 多语言文本混在业务表 | 业务表保存 key，`Localization` 独立管理 |
| 表格到 Graph 的隐式文件名约定 | Schema 显式声明跨源引用 |

## 7. Ability Graph 初步结构

Ability 旧 JSON 审计确认：旧格式结构稳定，但主要问题是短字段名和数组位序不适合人工编辑、代码审查和 AI 辅助。

新 Ability Graph 权威源建议使用具名字段：

```json
{
  "id": 2,
  "name": "Base_Attack1",
  "totalTimeMs": 900,
  "properties": [
    { "name": "speed", "type": "Float", "value": 1.0 }
  ],
  "timeline": [
    {
      "trackName": "Animation 0",
      "trackIndex": 0,
      "triggerType": "Signal",
      "triggerTimeMs": 0,
      "enabled": true,
      "durationMs": 0,
      "eventType": "PlayAnim",
      "eventData": {
        "animName": "Attack1"
      }
    }
  ]
}
```

结构规则：

- 顶层使用 `id/name/totalTimeMs/properties/timeline`。
- `properties` 保留 `{name,type,value}` 多态值表达。
- `timeline` 中每个事件都必须包含公共字段和 `eventType/eventData`。
- `eventType` 优先使用字符串枚举名，生成运行时产物时再映射为整数。
- `eventData` 必须由每种事件类型自己的 Schema 约束。
- 旧数组位序字段只允许出现在迁移器中，不允许作为新权威源字段。

## 8. 运行时统一格式

运行时目标不是读取 TSV 或 JSON，而是读取统一产物：

```text
RuntimeConfig.bytes
RuntimeConfigIndex.json
RuntimeConfig.hash
```

建议运行时索引至少包含：

- 版本号。
- 构建时间或构建序号。
- 每个模块的 hash。
- 每个表的 row count。
- 每个 graph 分组的 count。
- Schema 版本。
- 兼容性标记。

运行时访问只通过：

```csharp
IConfigProvider
IConfigTable<T>
IConfigRegistry
```

业务系统不得直接依赖：

- TSV parser。
- JSON parser。
- Excel/Luban API。
- ScriptableObject 配置资产。
- 具体文件路径。

## 9. 构建流水线

标准流程：

```text
scan source
  -> parse schema
  -> parse tables
  -> parse graphs
  -> build source index
  -> validate
  -> normalize
  -> generate runtime data
  -> emit reports
```

校验必须覆盖：

- ID 唯一性和范围。
- 引用存在性。
- 表格元数据与 Graph 详情的双向匹配。
- Graph-only / table-only 数据的显式分类。
- 多态引用的目标表校验。
- 多语言 key 完整性。
- 枚举值存在性和 flags 组合合法性。
- 资源路径存在性。
- 表格与 Graph 双向引用。
- Schema 版本兼容。
- 生成产物 hash 一致性。

报告输出：

- `config_health.txt`
- `config_issues.txt`
- `config_changes.txt`
- `config_ai_context.txt`
- `config_precommit.txt`
- `config_report_index.txt`

## 10. Development Agent：AI Agent 修改流程

AI Agent 修改配置时必须遵循：

1. 读取 `config_precommit.txt` 判断当前是否 blocked。
2. 读取 `config_report_index.txt` 找到报告包。
3. 根据问题读取 `config_issues.txt` 或 `config_ai_context.txt`。
4. 只修改 `ConfigSource`。
5. 不修改 `Generated/Config`。
6. 运行配置构建和提交前检查。
7. 若 `config_precommit.txt` 仍为 blocked，继续修复。

## 11. WGame 迁移优先级

建议按风险和收益排序：

1. **配置目录审计**：生成 WGame 数据源清单，标记权威源、导出产物和存档数据。
2. **表格型迁移试点**：选择 `Buff` 或 `Modifier` 基础定义，从 Luban Excel/JSON 映射到 TSV + Schema。
3. **结构型试点**：选择少量 Ability 或 AIAction，定义 Graph JSON 规范。
4. **运行时生成器**：把 TSV/JSON 生成统一 `RuntimeConfig.bytes` 和索引。
5. **项目层适配器**：在 WGame 中实现 `IConfigEditorSource`，接入 Framework Manager。
6. **提交前检查**：将 WGame 真实数据纳入 `config_precommit.txt`。

不建议第一步就全量迁移 Ability，因为它数量大、结构复杂、风险最高。

## 12. 框架边界

MxFramework 负责：

- 配置 Schema 抽象。
- 表格校验和引用校验。
- Editor 预览、报告、提交前检查。
- 运行时配置访问接口。
- 格式策略和构建管线契约。

WGame 项目层负责：

- 从旧 Excel/JSON/bytes 中迁移真实数据。
- 定义 WGame 业务 Schema。
- 实现真实构建器。
- 将运行时产物接入 YooAsset 或项目资源系统。
- 处理存档和玩家数据。

MxFramework 不直接依赖 Luban，也不把 WGame 业务配置提交进框架仓库。
