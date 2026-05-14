# MxFramework 迁移记录

> 从 WGame（/Users/vincent/Documents/WGame/Client）提取代码到 MxFramework 的完整记录。

## 迁移策略

1. **自底向上**: Core → Events → Attributes → Modifiers → Buffs → AI → Config
2. **接口优先**: 先迁移接口，再迁移实现（本规范阶段仅定义接口草案）
3. **最小改动**: 迁移时只做必要修改，不重构
4. **每批提交**: 一个模块一个 SVN commit
5. **质量门禁**: 每批必须满足 `Docs/QUALITY_GATE.md` 的完成定义

---

## 迁移批次

### 批次 0: 项目初始化
**SVN**: r1054–r1061
- [x] 创建 Unity 项目 (6000.0.61f1)
- [x] 创建目录结构
- [x] SVN 仓库: svn://dxp2800-c03f/WGame/MxFramework/trunk
- [x] DESIGN.md (总体设计规范)
- [x] EDITORS.md (编辑器规范)
- [x] INTERFACES.md (接口索引)
- [x] Docs/Interfaces/ (模块接口规范)
- [x] MIGRATION.md (本文件)
- [x] AGENTS.md

### 批次 1: Core 工具层 
**来源**: `WGame/Client/Assets/Scripts/Common/`
**目标**: `MxFramework/Core/` + `MxFramework/Core.Unity/`
**SVN**: r1062–r1063 (migration), r1064+ (Phase 1 cleanup)

**状态**: ✅ 完成

| 源文件 (WGame) | 目标文件 (MxFramework) | 状态 |
|---------------|----------------------|------|
| `WGame/Common/IHeapItem.cs` | `Core/Collections/Heap.cs` (merged) | ✅ |
| `WGame/Common/MyHeap.cs` | `Core/Collections/Heap.cs` (merged) | ✅ |
| `WGame/Common/WUnsortList.cs` | `Core/Collections/UnsortList.cs` | ✅ |
| `WGame/Common/WRandom.cs` | `Core.Unity/RandomTable.cs` ← 已拆分 | ✅ |
| `WGame/Common/Vector3Extension.cs` | `Core.Unity/VectorExtensions.cs` ← 已拆分 | ✅ |
| `WGame/Common/IntExtension.cs` (generic parts) | `Core/Math/BitUtils.cs` | ✅ |
| `WGame/Common/ZString.cs` | `Core/Extensions/ZString.cs` | ✅ |

**Phase 1 收尾修复**:
- Core 纯净化：`VectorExtensions` + `RandomTable` 拆到 `MxFramework.Core.Unity`
- `MxFramework.Core.asmdef`: `noEngineReferences=true`
- Heap: 修复容量、空堆 `Top`、`Pop()` 后槽位清理、`Contains()` 校验
- Heap/UnsortList: 明确 item 必须是引用类型，避免索引状态被值类型复制破坏
- UnsortList: 修复 `Optimize()` 后元素 `Index` 未更新、删除项未失效的 bug
- VectorExtensions: 修复 `ScaledToLength`、非单位向量角度、`AngleTo360` 顺时针方向和零角度语义
- 新增 EditMode tests: HeapTests, UnsortListTests, BitUtilsTests, VectorExtensionsTests, RandomTableTests

**偏差 / 风险**:
- `ZString` 命名仍为小写 `zstring`，保留原 API
- `ZString` 使用了 `ThreadStatic` / 静态全局状态，AOT 兼容性待验证

### 批次 2: Events 事件系统
**来源**: `WGame/Client/Assets/Scripts/ECS/Attributes/AttrBridge.cs`
**目标**: `MxFramework/Events/`
**状态**: ✅ 完成

目标契约见 `INTERFACES.md` 和 `API_STANDARDS.md`：
- `IEventBus<T>.Subscribe` 返回 `IDisposable`。
- `Publish(in T args)` 默认同步发布。
- 防重入、异常传播、重复订阅策略已有 EditMode tests 覆盖。

**Phase 2 实现**:
- 新增 `MxFramework.Events` 程序集，`noEngineReferences=true`。
- 新增 `IEventBus<T>` 和默认 `EventBus<T>`。
- 约定同步按订阅顺序发布；发布期间新增订阅下次生效；取消订阅会跳过尚未执行的同一订阅。
- handler 抛异常时立即传播，并停止本次发布。
- 新增 `EventBusTests` 覆盖订阅、取消订阅、重复订阅、防重入和异常传播。

### 批次 3: Attributes 属性核心
**来源**: `WGame/Client/Assets/Scripts/ECS/Attributes/`
**目标**: `MxFramework/Attributes/`
**状态**: ✅ 完成 v1

迁移映射:
- `WAttribute.AttrCore` → `AttributeStore`
- `AttrBridge` → `EventBus<T>`
- `AttrType` → `AttributeValue`

**Phase 3 v1 实现**:
- 新增 `MxFramework.Attributes` 程序集，`noEngineReferences=true`。
- 新增 `AttributeValue`、`AttributeChangedEvent`、`AttributeModifierEvent`。
- 新增 `IAttributeOwner`、`IAttributeModifier`、`AttributeModifierPhase`。
- 新增 `AttributeStore`：支持 register/get/try/set/add、modifier 添加/移除/清空、final value 变更事件。
- 保留 WGame 的核心模式：属性 ID 为 `int`、缺失属性默认读取为 0、属性变化触发事件。
- 去除 WGame 绑定：不依赖 `GameEntity`、`WAttrType`、Buff、Entry、Luban 配置。

### 批次 4: Modifiers 修改器系统
**来源**: `WGame/Client/Assets/Scripts/ECS/Entry/`
**目标**: `MxFramework/Modifiers/`
**状态**: ✅ 完成 v1

迁移映射:
- `IEntry` → `IModifier`
- `EntryApplyData` → `ModifierContext`
- `EntryManager` → `ModifierPipeline`
- `IEntryCondition` → `IModifierCondition`
- `IEntryEffect` → `IModifierEffect`
- `EntryCondCounter` / `CounterDefine` 运行时部分 → `CounterStore` / `ICounterStore`
- `EntryFactory` → `IModifierFactory`

**Phase 5 v1 实现**:
- 新增 `MxFramework.Modifiers` 程序集，`noEngineReferences=true`。
- 新增 `ModifierContext`，支持对象池和 Attributes/Buffs/Counters 访问。
- 新增 `IModifier`、`IModifierCondition`、`IModifierEffect`、`IModifierFactory`、`IModifierPipeline`。
- 新增 `ModifierBase`、`ModifierPipeline`、`CounterStore`、`ModifierSnapshot`、`ModifierEvent`、`CounterChangedEvent`。
- 支持 modifier 添加、同 ID 替换、移除、清空、ApplyAll、UpdateAll、快照和事件。
- 去除 WGame 绑定：不依赖 `GameEntity`、元素、伤害、技能、`CounterDefine` 文案、Entitas、Luban。

### 批次 5: Buffs 系统
**来源**: `WGame/Client/Assets/Scripts/Ability/Data/Buff/`
**目标**: `MxFramework/Buffs/`
**状态**: ✅ 完成 v1

迁移映射:
- `BuffManager` → `BuffPipeline`
- `BuffData` / `BuffStatus` 生命周期 → `IBuff` / `BuffBase`
- `BuffAddType.RefreshAllTimeAndAdd` 核心语义 → `DefaultBuffStackingPolicy`
- `BuffData.ID` → `IBuff.Id`
- `BuffData.Duration` → `IBuff.Duration` / `RemainingTime`

**Phase 4 v1 实现**:
- 新增 `MxFramework.Buffs` 程序集，`noEngineReferences=true`。
- 新增 `IBuff`、`IBuffTarget`、`IBuffPipeline`、`IBuffFactory`、`IBuffStackingPolicy`。
- 新增 `BuffBase`、`BuffPipeline`、`DefaultBuffStackingPolicy`、`BuffSnapshot`、`BuffEvent`。
- 支持永久 Buff、限时 Buff、层数叠加、持续时间刷新、Tick 过期、移除和清空。
- 新增 `IAttributeModifierOwner`，让 Buff 通过接口清理属性修改器。
- 去除 WGame 绑定：不依赖 `BuffIDs`、`WAttrType`、`BuffOwner`、Entitas、Luban、具体技能或状态类型。

### 批次 6: Runtime AI Planner 抽象
**来源**: `WGame/Client/Assets/Scripts/ECS/GOAPAI/`
**目标**: `MxFramework/AI/`
**状态**: 📋 待开始

### 批次 6.5: Config 配置抽象
**来源**: WGame/Luban 配置访问模式
**目标**: `MxFramework/Config/`
**状态**: ✅ 完成 v1

迁移映射:
- Luban 表访问 / 游戏配置静态入口 → `IConfigProvider`
- 多配置来源组合 → `IConfigRegistry`
- 测试用配置数据 → `MemoryConfigProvider`
- 配置引用检查 → `IConfigReferenceProvider` / `ConfigValidator`

**Phase 6 v1 实现**:
- 新增 `MxFramework.Config` 程序集，`noEngineReferences=true`。
- 新增 `IConfigData`、`IConfigProvider`、`IConfigRegistry`。
- 新增 `MemoryConfigProvider`、`ConfigRegistry`。
- 新增 `ConfigKey`、`ConfigResult`、`ConfigError`、`ConfigException`。
- 新增重复 ID 策略：`Throw`、`Replace`、`Ignore`。
- 新增引用校验基础 API：`ConfigReference`、`ConfigValidationIssue`、`ConfigValidationReport`、`ConfigValidator`。
- 去除 WGame 绑定：不依赖 Luban 生成代码、Json、ScriptableObject、具体配置表或静态全局入口。

**Phase 6.1 v1 实现**:
- 新增配置表抽象：`IConfigTable<T>`、`ConfigTable<T>`。
- 新增 Schema 抽象：`ConfigSchema`、`ConfigField`、`ConfigFieldType`。
- 新增规则：`ConfigIdRange`、`ConfigReferenceRule`。
- 新增多语言基础：`LocalizedTextKey`、`LocaleId`、`ILocalizationProvider`、`MemoryLocalizationProvider`。
- 新增表级校验：`ConfigTableValidator`、`ConfigTableValidationReport`、`ConfigTableValidationIssue`。
- 新增 AI 友好导出：`ConfigSchemaExporter`、`ConfigAiSummary`。
- 多语言策略：业务表保存文本 key，不直接保存各语言文本列。

**Phase 6.2 v1 实现**:
- 新增 `MxFramework.Config.Runtime` 适配程序集。
- 新增 `IBuffConfig`、`IModifierConfig`。
- 新增 `BasicBuffConfig`、`BasicModifierConfig`，包含 `LocalizedTextKey` 字段和 Schema。
- 新增 `ConfigBuffFactory<TConfig>`、`ConfigModifierFactory<TConfig>`。
- 新增 `ConfiguredBuff`、`ConfiguredModifier`。
- 打通 `ConfigTable -> factory -> BuffPipeline/ModifierPipeline`。
- 保持依赖方向：Config.Runtime 依赖 Config/Buffs/Modifiers，Buffs 和 Modifiers 不反向依赖 Config。

**Phase 6.3 v1 实现**:
- 新增 `ConfigAuthoring`、`ConfigAuthoringTemplate`、`ConfigAuthoringReport`。
- 支持从 `ConfigSchema` 导出 TSV 表头、样例行、字段说明和必需语言列表。
- 支持把 `ConfigTableValidationReport` 转成 Editor/AI 友好的结构化问题列表。
- 不绑定 Excel、CSV、Json、Luban 或 ScriptableObject，后续具体导入器只需适配到 `ConfigTable<T>`。

### 批次 7: Runtime AI Planner（AI 轻量 Planner）
**目标**: `MxFramework/AI/`
**状态**: ✅ 完成 v1

**Phase 7 v1 实现**:
- 新增 `MxFramework.AI` 程序集，`noEngineReferences=true`。
- 新增事实状态：`AiFactKey`、`IAiWorldState`、`AiWorldState`。
- 新增目标、动作、条件、效果接口和基础实现。
- 新增 `PriorityGoalSelector`、`SequentialPlanner`、`AiPlan`。
- 保持框架纯净：不依赖 AI 插件、Unity NavMesh、Entitas、WGame 类型或具体业务 AI。

### 批次 8: 编辑器工具（更新：已覆盖 Phase 8.0-8.19）
**目标**: `MxFramework/Editor/` + `MxFramework/Diagnostics/` + `MxFramework/Config/` + `MxFramework/Config.Runtime/` + 内置 Demo 源
**状态**: ✅ Phases 8.0-8.19 全部完成

> 注意：Phase 8.2 后续为框架原生开发，不是从 WGame 迁移的代码。

**Phase 8.0 v1 实现**:
- 新增 `MxFramework.Editor` 程序集，仅 Editor 平台编译。
- 新增 `FrameworkManager`，菜单路径 `MxFramework > Framework Manager`。
- 新增 `FrameworkModuleInfo`、`MxEditorUtils`。
- 支持模块列表、依赖摘要、文档入口和 asmdef 存在性验证。
- 暂不实现 GraphView、ConfigEditor、Buff/Modifier sandbox 或 AI sandbox。

**Phase 8.1 v1 实现**:
- 新增 `MxFramework.Diagnostics` 运行时程序集，`noEngineReferences=true`。
- 新增 `IFrameworkDebugSource`、`FrameworkDebugMode`、`FrameworkDebugSnapshot`、`FrameworkDebugSection`。
- 新增 `FrameworkDebugReportExporter`，用于导出可复制的调试报告。
- Framework Manager 增加 `编辑模式 / 运行模式` 切换。
- 运行模式默认只读，当前显示 Play Mode / Debug Source 连接提示。
- Editor 通过 Diagnostics 协议面向运行时快照，不让 Runtime 依赖 Editor。

**Phase 8.2 Config Schema Viewer v1**（非迁移代码）:
- Framework Manager 编辑模式增加 `配置表` 页，展示内置 `BasicBuffConfig`、`BasicModifierConfig` Schema。
- 新增 `ConfigSchema`、`ConfigField`、`ConfigFieldType` 元数据结构。
- 支持复制 ConfigAuthoring 模板、运行内置示例表校验。
- 不进入 Play Mode、不引入 Excel/CSV/Json/Luban/ScriptableObject。

**Phase 8.3 Config Editor Source Preview v1**（非迁移代码）:
- 新增 Editor 侧 `IConfigEditorSource` 契约。
- 新增 `MemoryConfigEditorSource<T>` 模拟真实配置源。
- Framework Manager 配置表页改为选择配置源，展示名称、类型、行数、TSV 预览。
- 项目层可通过实现 `IConfigEditorSource` 接入自己的配置导入器。

**Phase 8.4 Config Health Check v1**（非迁移代码）:
- 新增 `ConfigHealthReport`、`ConfigSourceHealth`、`ConfigIssueStat`。
- Config 页自动检测所有配置源健康度，显示 Error/Warning 统计。
- 按配置源输出表级状态：正常、警告、错误。

**Phase 8.5 Config Issue List / AI Fix Context v1**（非迁移代码）:
- 新增 `ConfigIssueView`，保存配置源名和结构化问题。
- Config 页显示问题明细，支持 `全部 / Error / Warning` 筛选。
- 支持复制当前源 AI 修复上下文（包含源信息、Schema、TSV 预览、问题列表）。

**Phase 8.6 Config Change Detection v1**（非迁移代码）:
- 新增 `ConfigChangeReport`、`ConfigSourceChange`、`ConfigIssueStatChange`。
- Config 页基于健康报告保存轻量基线，检测配置源行数/Error/Warning 变化。
- 支持重置变动基线，支持复制配置变动报告。

**Phase 8.7 Config Report Export Standard v1**（非迁移代码）:
- 新增 `ConfigReportExportResult`。
- 固定导出目录 `Temp/MxFrameworkReports/Config/`。
- 导出 `config_health.txt`、`config_issues.txt`、`config_changes.txt`、`config_ai_context.txt`、`config_report_index.txt`。
- 不依赖剪贴板即可取得完整配置检查上下文。

**Phase 8.8 Config Precommit Check v1**（非迁移代码）:
- Config 页提供 `提交前检查` 按钮。
- 刷新配置源 → 重新检测健康状态和变动 → 自动导出报告包 → 生成 `config_precommit.txt`。
- 判定结果：`ready` / `warning` / `blocked`（Error > 0 时 blocked）。

**Phase 8.9-8.9.3 Config Workbench v0**（非迁移代码）:
- 三栏布局：左侧源导航、中间主工作区（概览/字段/行视图/引用页签）、右侧检查器。
- 顶部上下文栏显示当前源、结构类型、健康状态和高频动作。
- 底部问题抽屉展示全局问题列表。
- 小窗口适配：配置源置顶、结构和校验区域换行滚动。

**Phase 8.10 Table Editor v1 Preview**（非迁移代码）:
- 新增 `IConfigEditorTablePreviewProvider`。
- 行预览根据 `ConfigField` 推导控件类型：数值、开关、枚举/Flags、引用选择器、资源路径、自定义控件。
- 当前阶段只读，不保存配置。

**Phase 8.11 Table Editor v1 Readonly Controls**（非迁移代码）:
- 布尔字段→开关外观，枚举/flags→下拉外观，引用字段→选择按钮外观。
- 所有控件保持只读，由 `ConfigField`、`enumId` 和 `ConfigReferenceRule` 推导。

**Phase 8.12 Enum / Flags Candidate Preview**（非迁移代码）:
- 新增 `IConfigEditorEnumProvider`、`ConfigEnumRegistry`、`ConfigEnumDomain`。
- enum/flags 控件显示候选项、中文名、英文名和数值。
- 内置 Demo 增加 `ActionCatalog / ActionGraph` 展示 enum/flags/asset path/跨源引用。

**Phase 8.13 Pure Config Demo Package**（非迁移代码）:
- 将内置 Demo 从 `MxEditorUtils` 拆分到 `ConfigDemoSources`。
- 四个纯净 Demo 源：BasicBuffConfig、BasicModifierConfig、ActionCatalog、ActionGraph。
- 可选坏引用 Demo 不默认注册，不污染提交前检查。
- 增加 `Docs/Demo/CONFIG_DEMO.md` 说明 Demo 边界。

**Phase 8.14 Field Display Alias**（非迁移代码）:
- Demo Schema 和基础运行时配置补充中文 `ConfigField.DisplayName`。
- 字段列表和行视图优先显示中文别名，保留原字段名。
- 问题报告和 AI 上下文输出 `field=` 原 key，并补充 `fieldDisplay=`。

**Phase 8.15 Field Inspector / Reference Jump**（非迁移代码）:
- 字段页增加 `详情` 操作，右侧检查器显示字段元数据。
- enum/flags 字段详情显示候选项。
- 引用字段提供 `查看目标源` 跳转到目标配置源。
- AI 上下文追加当前选中字段摘要。

**Phase 8.16 Buff Runtime Patch / Mod Vertical Slice**（非迁移代码）:
- 新增 `ConfigLayerKind`（Base / Patch / Mod / Debug）。
- 新增 `ConfigPatchEntry<T>`、`ConfigPatchOperation`、`ConfigChangeSet`。
- 新增 `RuntimeConfigPatchMerger` 合并 Base + Patch/Mod 行。
- 内置 `BasicBuffConfig` / `BasicModifierConfig` 验证覆盖、新增、删除和引用校验。

**Phase 8.17 Portable Authoring Workflow Spec**（非迁移代码）:
- 新增 `Docs/AUTHORING_WORKFLOW.md`。
- 定义 Player Mode / Developer Mode / AI Mode / Tool Mode。
- 定义 `Workflow`、`Step`、`QuickAction` 数据结构。
- 运行时只消费 merged snapshot，Studio 流程不绑定 Unity Editor。

**Phase 8.18 Buff Authoring Workflow Demo**（非迁移代码）:
- 新增 `AuthoringWorkflow`、`AuthoringWorkflowStep`、`AuthoringQuickAction` 数据结构（Config 模块）。
- 新增 `BuffAuthoringWorkflowTemplate`（Config.Runtime 模块）。
- 外部 Authoring Editor 展示创作流程；Unity Framework Manager 不再内置创作流程页。
- 支持多条进行中的 Buff 流程，步骤状态显示 Actor/输入/输出/校验/AI 提示/快捷动作。

**Phase 8.19 WGame Buff Authoring Logic**（非迁移代码）:
- 新增 `Docs/WGAME_BUFF_AUTHORING_WORKFLOW.md`。
- 明确 `Numerical / Condition / ChangeAttr / DamageByAttr / CastOrb* / Positive / Status` 类型入口。
- 流程调整为：BuffType → 公共字段 → 堆叠持续 → 类型专属字段 → 表现 → 引用。
- Framework Manager 内置 Demo 使用不同 Buff 类型模板。

### 批次 9: WGame 数据审计
**目标**: 审计 WGame 现有数据来源、关系、字段结构和枚举映射
**SVN**: r1093-r1097
**状态**: ✅ 审计文档已齐，收尾进行中

> 本批次为文档审计，不是代码迁移。

**Phase 9.0 审计产出**:

| 文档 | 内容 |
|------|------|
| `Docs/WGAME_DATA_AUDIT.md` | 现有数据来源、规模、代码使用证据 |
| `Docs/CONFIG_FORMAT_STRATEGY.md` | TSV/JSON/Schema/Bytes 分层职责 |
| `Docs/WGAME_DATA_RELATION_AUDIT.md` | AIAction、AIConfig、Buff、Talent、Character/Weapon、Map 跨源引用关系 |
| `Docs/WGAME_SPLIT_GRAPH_AUDIT.md` | AIAction、AIConfig、Buff 的 Split JSON 主干结构 |
| `Docs/WGAME_TABLE_FIELD_INDEX.md` | Luban/BaseData 表字段索引 |
| `Docs/WGAME_ENUM_MAPPING_AUDIT.md` | 常见枚举、flags、C# 常量域映射 |
| `Docs/Tasks/ABILITY_JSON_AUDIT_TASK.md` | Ability JSON 独立分析任务输入/产出/验收 |
| `Docs/Tasks/ABILITY_JSON_AUDIT_RESULT.md` | Ability JSON 字段、事件类型、异常和命名建议 |
| `Docs/Tasks/PHASE9_CLOSEOUT_PLAN.md` | Phase 9 收尾执行清单 |

**偏差 / 风险**:
- 未提交 WGame 真实业务数据到框架主干。
- 下一步需要将审计结果映射到 `ConfigSchema` / `ConfigReferenceRule` / `ConfigSourceIndex`。
- 引用规则白名单可直接映射到 `ConfigReferenceRule`，表格索引可直接映射到 `ConfigSchema`。

### 批次 10: 外部主创编辑器
**目标**: 建立脱离 Unity 的 Authoring Core / CLI、外部编辑器和 Runtime Preview
**SVN**: r1117-r1129
**状态**: 
- ✅ Phase 10.0 Planning/EPIC Spec Ready
- ✅ Phase 10.1 Authoring Core / CLI v0.1
- ✅ Phase 10.2 Buff Editor UI Skeleton
- ✅ Phase 10.3 Editable Local Loop MVP
- ✅ Phase 10.4 BuffType-Aware Field Authoring v0
- ✅ Phase 10.5 Runtime Preview Editor Integration 03.4

> 本批次全部为框架原生开发，不是从 WGame 迁移的代码。

**新增程序集/项目**:

| 位置 | 类型 | 职责 |
|------|------|------|
| `Tools/MxFramework.Authoring/src/Core/` | .NET Standard 2.1 | Schema、Workflow、Patch、Validator、Merger、Report、AI Context |
| `Tools/MxFramework.Authoring/src/Cli/` | .NET 8.0 Console | authoring CLI，EditorServer，PackageReader，ReportBundleWriter |
| `Tools/MxFramework.Authoring/src/Preview.NetClient/` | .NET 8.0 | WebSocket Preview Client |
| `Tools/MxFramework.Authoring/tests/` | .NET 8.0+9.0 | 无第三方依赖的测试控制台 + MockPreviewServer |
| `Tools/MxFramework.Authoring.Editor/` | React/TS Web UI | 外部 Buff 主创编辑器 |
| `Assets/Scripts/MxFramework/Preview/Runtime/` | Unity Runtime | PreviewRpcServer、DummyPreviewWorld、MemoryBuffPatchLoader |
| `Assets/Scripts/MxFramework/Preview/Editor/` | Unity Editor | Preview 菜单入口 |

**Phase 10.1 Authoring Core / CLI v0.1 实现**:
- 新增 `MxFramework.Authoring.Core`：`BuiltInContent`（内置 Buff Schema）、`Patch`、`ReportBundle`、`Schema`（Workflow Schema / ConfigSchema / Field / Validator / AuthoringExitCodes）。
- 新增 `authoring` CLI：支持内置 Buff Workflow、Buff Schema、Patch validate、merge-preview、report bundle 写文件、step context。
- 支持 Project Authoring Manifest export / inspect（Schema、Enum、Reference、Workflow、Localization）。
- 新增样本 `samples/buff-mod/` 和 `samples/buff-preview/`。
- EditorServer 本地服务模式；新增 `preview.handshake / loadPatch / applyBuff / reset / getSnapshot / getLogs` 协议。

**Phase 10.2 Buff Editor UI Skeleton 实现**:
- 新增 `Tools/MxFramework.Authoring.Editor/` Web UI。
- 读取 Project Authoring Manifest 样例和 Buff Preview ModPackage 样例。
- 展示 Workflow 步骤、Buff 列表、Schema 字段、Enum、Reference、Validation Report 和 Merge Preview。
- 不打开 Unity 即可通过本地 URL 打开编辑器。

**Phase 10.3 Editable Local Loop MVP 实现**:
- Authoring CLI 增加 `editor serve` 本地 API。
- 外部编辑器优先读取 `/api/state`，静态服务降级为只读预览。
- 支持编辑示例 Buff Patch 字段并进行必填字段即时提示。
- 支持保存 `samples/buff-preview/patches/buff.patch.json`。
- 支持一键重新生成 report bundle 并刷新校验报告和合并预览。

**Phase 10.4 BuffType-Aware Field Authoring v0 实现**:
- Buff Schema 字段增加分组、单位和 BuffType 可见性元数据。
- 补充 WGame 风格的 Buff 类型专属字段：Numerical、Condition、ChangeAttr、DamageByAttr、CastOrb*、Positive、Status。
- 补充目标、属性、伤害、元素、状态、条件和位置等基础枚举域。
- 外部编辑器根据当前 BuffType 动态显示类型相关字段。
- Authoring Core Validator 校验当前 BuffType 下可见的必填字段。

**Phase 10.5 Runtime Preview Editor Integration 03.4 实现**:
- 新增 `MxFramework.Preview.Runtime` 和 `MxFramework.Preview.Editor` 程序集。
- PreviewRpcServer（轻量 TCP WebSocket）、DummyPreviewWorld、MemoryBuffPatchLoader、MxPreviewBootstrap。
- 新增 preview 协议：handshake / loadPatch / applyBuff / reset / getSnapshot / getLogs / 错误码。
- EditorServer 增加 `/api/preview/status` 和 `/api/preview/run`。
- 外部编辑器增加运行时预览面板，展示连接、Patch 加载、Buff 结果、错误码、日志和性能摘要。
- Edit Mode 可启动 Unity Preview Server，`applyBuff` 当前停在 DummyPreviewWorld（2003 预期边界）。

**偏差 / 风险**:
- `applyBuff` 仍停在 DummyPreviewWorld，未接入真实 `IBuffFactory`（恢复 03.5 推进）。
- 外部编辑器 `MxFramework.Authoring.Editor` 当前为 React/TS 骨架，尚需生产级 UI 打磨。
- Preview 协议版本号未在 spec 中固化，后续需统一 schemaVersion + handshake 兼容窗口。

---

## 修改记录

| 源文件 (WGame) | 目标文件 (MxFramework) | 修改内容 | 状态 |
|---------------|----------------------|---------|------|
| IHeapItem.cs | Heap.cs | 合并进 Heap.cs，接口 IHeapItem<T> | ✅ |
| MyHeap.cs | Heap.cs | 类名 MyHeap→Heap，命名空间 | ✅ |
| WUnsortList.cs | UnsortList.cs | 类名 WUnsortList→UnsortList, WListItem→IUnsortListItem | ✅ |
| WRandom.cs | RandomTable.cs | 类名 WRandom→RandomTable, SortQuarterInt 内联 | ✅ |
| Vector3Extension.cs | VectorExtensions.cs | 类名，方法名 AngleTo/AngleTo360/IsClockwiseTo | ✅ |
| IntExtension.cs | BitUtils.cs | 仅提取位操作部分，移除游戏逻辑 | ✅ |
| ZString.cs | Extensions/ZString.cs | 直接拷贝，待后续处理命名空间 | ✅ |

---

## 依赖追踪

被迁移文件在 WGame 中被哪些文件引用，迁移后需要如何适配。

| WGame 引用方 | 迁移的类 | 适配方案 |
|-------------|---------|---------|
| 待补充 | - | - |
