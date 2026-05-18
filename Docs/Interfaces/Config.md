# Config 接口

## 职责

Config 提供配置访问、表结构、校验、多语言 key、AI/Editor 友好 authoring 输出。不绑定 Excel、CSV、Json、Luban 或 ScriptableObject。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `IConfigData` | 配置数据标记，要求 `Id` |
| `IConfigProvider` | 按类型和 ID 读取配置 |
| `IConfigRegistry` | 多 Provider 组合 |
| `IConfigTable<T>` / `ConfigTable<T>` | 配置表 |
| `ConfigSchema` / `ConfigField` | 表结构和字段元数据 |
| `ConfigReferenceRule` | 运行时类型引用和跨源引用规则 |
| `ConfigStructureKind` | 区分 Table、Graph、Localization、GeneratedRuntime |
| `ConfigEnumDomain` / `ConfigEnumRegistry` | 枚举和 flags 统一注册 |
| `ConfigSourceEntry` / `ConfigSourceIndex` | 配置源索引和跨源引用校验 |
| `IConfigEditorSourceIndexProvider` | Editor 配置源可选实现，用于接入 SourceIndex |
| `LocalizedTextKey` / `ILocalizationProvider` | 多语言 key 和文本提供者 |
| `ConfigTableValidator` | 表级校验 |
| `ConfigAuthoring` | TSV 模板和结构化报告 |
| `ConfigBuffFactory<TConfig>` | 配置驱动 Buff 创建 |
| `ConfigModifierFactory<TConfig>` | 配置驱动 Modifier 创建 |
| `BasicAbilityConfig` | 最小 Ability 配置行，ID 范围 300000-399999 |
| `ConfigAbilityFactory` | 配置驱动 Gameplay Ability 创建 |
| `AbilityGraphConfig` | synthetic Ability Runtime Graph 配置 DTO，包含 nodes、edges 和 typed payload config |
| `AbilityGraphConfigMapper` | 将 Ability Graph config 映射为 Gameplay `AbilityGraphDefinition`，并输出带 source/field path 的 diagnostics |
| `ConfigLayerKind` | 运行时配置层：Base / Patch / Mod / Debug |
| `ConfigPatchEntry<T>` | 单条运行时配置增量操作 |
| `RuntimeConfigPatchMerger` | 合并 Base 行和 Patch/Mod 行，输出运行时表 |
| `ConfigChangeSet` | 描述运行时合并产生的新增、替换、删除和无变化 |
| `RuntimeConfigHotReloadRequest` | 显式运行时 patch reload 请求，包含路径、source name 和可选期望 hash |
| `RuntimeConfigHotReloadResult` | reload 结果，包含 source、source id、内容 hash、耗时、变更表、错误和新 provider |
| `RuntimeConfigPatchHotReloadService` | 读取 JSON patch、复用 patch loader / merger，成功时产出新的只读 registry |
| `RuntimeConfigHotReloadPoller` | 轻量文件变更探测器，只负责生成显式 reload request |
| `AuthoringWorkflow` | 可移植创作流程，描述目标、模式、当前步骤和步骤列表 |
| `AuthoringWorkflowStep` | 单个流程节点，描述输入、输出、校验、Actor、AI 提示和快捷动作 |
| `AuthoringQuickAction` | 抽象快捷动作，由 Unity Editor、外部 Mod Editor 或 CLI 解释执行 |
| `BuffAuthoringWorkflowTemplate` | Buff 创建流水线模板，位于 `MxFramework.Config.Runtime` |

## 使用约定

- `id <= 0` 非法；`0` 保留为 invalid。
- `GetConfig<T>` 缺失时抛异常；无异常流程用 `TryGetConfig` 或 `GetConfigResult`。
- 业务表中的多语言字段保存 `LocalizedTextKey`，具体语言文本由 `ILocalizationProvider` 提供。
- 字段使用枚举或 flags 时只保存 `enumId`，枚举值由 `ConfigEnumRegistry` 统一解释。
- 表格引用 Graph、Localization 或运行时产物时，使用 `ConfigReferenceRule(fieldName, targetSchemaName, targetStructureKind)`。
- `ConfigSourceIndex` 只登记源和 key，不负责解析 TSV/JSON；具体导入器在项目层实现。
- Editor 配置源如果实现 `IConfigEditorSourceIndexProvider`，健康报告会自动统计索引源、索引 key，并合并跨源引用问题。
- 具体导入器在项目层实现，最后产出 `ConfigTable<T>`。
- `MxFramework.Config.Runtime` 是桥接层，不让 Buffs/Modifiers/Gameplay 反向依赖 Config。
- Ability Graph config 当前是 synthetic runtime DTO，不读取真实 WGame JSON；项目层导入器应先映射到该 DTO，再进入 `AbilityGraphConfigMapper`。
- 运行时动态更新不修改 Authoring Source；应由 Base、Patch、Mod、Debug 等层合并成新的运行时只读表。
- Mod 或热更配置优先表达为 `ConfigPatchEntry<T>`，再由 `RuntimeConfigPatchMerger` 生成 merged view 和 `ConfigChangeSet`。
- Config Runtime hot reload 是显式操作：调用方先生成 `RuntimeConfigHotReloadRequest`，再决定是否把成功的 `RuntimeConfigHotReloadResult.Provider` 切换为当前 provider。
- `RuntimeConfigPatchHotReloadService` 当前只加载 Config.Runtime 已支持的 JSON patch bundle，并复用 `RuntimeConfigPatchJsonLoader`、`RuntimeConfigPatchMerger` 与 `ConfigChangeSet`；它不热重载 Unity 序列化资产。
- reload 失败时返回 `Success == false` 和 `Errors`，不会修改传入的 base provider；调用方不应在失败结果上继续切换运行时配置。
- 创作流程必须是 runtime-agnostic 数据描述，不依赖 Unity API，也不要求完整源码。
- AI 辅助优先读取 `AuthoringWorkflow.CreateStepAiContext(...)` 生成的步骤级上下文，不默认扫描全项目。

## 最小示例

见 `Docs/USAGE.md` 的 Config、ConfigAuthoring、Buff 创作流程、配置驱动 Buff/Modifier 章节。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Config/`
