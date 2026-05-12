# Editor 接口

## 职责

Editor 提供 Unity 内部可视化工具入口。正式工具使用 UI Toolkit，用户可见文案默认中文，技术标识保留英文。

## 当前公开入口

| 类型 | 用途 |
|------|------|
| `FrameworkManager` | `MxFramework > Framework Manager` 主窗口，包含模块概览、配置工作台和运行模式提示 |
| `FrameworkModuleInfo` | 模块列表元数据 |
| `IConfigEditorSource` | Editor 侧配置源契约 |
| `IConfigEditorSourceIndexProvider` | 配置源可选 SourceIndex 接入契约 |
| `IConfigEditorTablePreviewProvider` | 配置源可选行视图预览契约 |
| `IConfigEditorEnumProvider` | 配置源可选枚举域提供契约 |
| `ConfigEditorControlHints` | 根据字段元数据推导未来编辑控件，供只读控件预览使用 |
| `MemoryConfigEditorSource<T>` | 内存配置源示例实现 |
| `ExternalConfigEditorSource` | 项目层真实配置源的通用包装器 |
| `ConfigHealthReport` | 配置健康检测总览 |
| `ConfigSourceHealth` | 单个配置源健康状态 |
| `ConfigIssueStat` | 按错误类型聚合的问题统计 |
| `ConfigIssueView` | 可复制的问题明细 |
| `ConfigChangeReport` | 配置健康基线对比结果 |
| `ConfigSourceChange` | 单个配置源的行数和问题数变化 |
| `ConfigIssueStatChange` | 单类配置问题数量变化 |
| `ConfigReportExportResult` | 配置报告包导出结果 |
| `MxEditorUtils` | 文档打开、模块验证、Config Source 注册、模板和报告文本辅助 |

## 模式规范

- `编辑模式`：不依赖 Play Mode，用于配置、Schema、依赖和静态校验。
- `运行模式`：默认只读，通过 `IFrameworkDebugSource` 快照显示运行时状态。
- 不允许把运行时对象反向写回配置表。
- 所有验证和沙盒结果都应能复制或导出报告。

## 当前配置工作台

- 菜单路径：`MxFramework > Framework Manager`。
- 操作路径：`编辑模式 > 配置工作台`。
- 内置 Demo 配置源：`内置 Demo / BasicBuffConfig`、`内置 Demo / BasicModifierConfig`、`内置 Demo / ActionCatalog`、`内置 Demo / ActionGraph`。
- 能力：查看来源类型、结构类型、行数、字段结构、引用规则、ID 范围、必需语言，复制模板，预览源内容，自动健康检测，问题明细，校验当前源。
- 接入：项目层调用 `MxEditorUtils.RegisterConfigEditorSource(source)` 后，点击 `刷新配置源`。
- 推荐接入：项目层解析 TSV/JSON/Luban 后，用 `ExternalConfigEditorSource` 包装 Schema、keys、问题列表、预览和 hash。
- 跨源校验：实现 `IConfigEditorSourceIndexProvider` 后，健康报告会自动纳入 `ConfigSourceIndex`；`ExternalConfigEditorSource` 会基于预览行校验引用 key 是否存在。
- 行视图：实现 `IConfigEditorTablePreviewProvider` 后，配置工作台会显示行预览和禁用态控件映射。
- 枚举：实现 `IConfigEditorEnumProvider` 后，配置工作台会显示 enum/flags 当前值解释和候选项。
- 字段别名：`ConfigField.DisplayName` 是 UI 显示别名；`ConfigField.Name` 仍是报告、导入、导出、校验和 AI 修复时的真实 key。
- 字段详情：配置工作台可按字段显示元数据、enum/flags 候选项和引用目标；`CreateConfigAiFixContext` 可带 `selectedFieldName` 输出当前字段摘要。
- 健康报告：`MxEditorUtils.AnalyzeConfigHealth(sources)` 和 `MxEditorUtils.CreateConfigHealthReport(health)`。
- AI 修复上下文：`MxEditorUtils.CreateConfigAiFixContext(source, health, issues, previewRows)`。
- 变动检测：`MxEditorUtils.DetectConfigChanges(health, baseline)` 和 `MxEditorUtils.CreateConfigChangeReportText(report)`。
- 报告导出：`MxEditorUtils.ExportConfigReportBundle(source, health, issues, changes, previewRows)`，默认写入 `Temp/MxFrameworkReports/Config/`。
- 提交前检查：`MxEditorUtils.CreateConfigPrecommitReportText(health, changes, issues)`，输出 `ready / warning / blocked`。
- 边界：不编辑真实资产，不绑定 Excel/CSV/Json/Luban/ScriptableObject。

## 后续扩展

- 完整 `ConfigEditor` 资产编辑和导入器适配
- `BuffsEditor`
- `ModifiersEditor`
- `AIEditor`
- 依赖图和 GitNexus Health Check 的工作流入口见 `../GITNEXUS.md`

## 验证入口

通过 Unity MCP 执行菜单 `MxFramework/Framework Manager`，并读取 `mcpforunity://editor/windows` 确认窗口存在。
