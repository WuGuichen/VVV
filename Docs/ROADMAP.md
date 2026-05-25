# MxFramework 路线图

> 版本 0.6.41 | 2026-05-25
>
> 路线图按“先稳定边界，再迁移实现，再做工具化”的顺序推进。

## Phase 0: 文档基线

目标：统一框架边界、接口风格和验收规则。

完成条件：

- `Docs/README.md`、`ARCHITECTURE.md`、`API_STANDARDS.md`、`QUALITY_GATE.md` 建立。
- 现有设计文档引用新规范。
- 迁移计划明确 Core 纯净化和 Unity 适配层拆分。

## Phase 1: Core 基础层

目标：稳定可复用、低依赖的基础工具。

任务：

- 修正 `Heap<T>` 容量语义和边界行为。
- 修正 `UnsortList<T>.Optimize` 后元素 Index。
- 检查 `VectorExtensions` 长度缩放、角度和顺时针方向语义。
- 将 Unity 类型相关工具拆到 `Core.Unity`。
- 为集合、位运算、随机表、Vector 扩展添加 EditMode tests。

完成条件：

- `MxFramework.Core` 可设置 `noEngineReferences=true`。
- `Core.Unity` 独立 asmdef 引用 UnityEngine。
- 所有 Core 测试通过。
- Heap/UnsortList 边界 bug 修复。
- VectorExtensions 长度缩放、角度和顺时针方向语义修复。

**状态**: ✅ 完成

## Phase 2: Events

目标：提供类型安全、可解除订阅、低分配的事件基础设施。

任务：

- 设计 `IEventBus<T>` 和订阅句柄。
- 定义防重入和异常传播策略。
- 提供默认 `EventBus<T>`。
- 编写订阅、取消订阅、嵌套发布、异常传播测试。

完成条件：

- Attributes 可以依赖 Events 而不引入 Unity。
- 高频发布路径无分配或分配已记录。

**状态**: ✅ 完成

## Phase 3: Attributes

目标：从 WGame 属性系统提取通用属性存储与计算链。

任务：

- 实现 `AttributeStore`。
- 定义 `AttributeValue`、`IAttributeModifier`、排序阶段。
- 建立属性变更事件。
- 提供调试快照。

完成条件：

- 支持 base/final value。
- 支持添加、移除、清空修改器。
- 属性计算链可被 Editor 展示。

**状态**: ✅ 完成 v1

## Phase 4: Buffs

目标：实现通用 Buff 生命周期与堆叠策略。

任务：

- 定义 `IBuff`、`IBuffPipeline`、`IBuffFactory`。
- 定义 `IBuffStackingPolicy`。
- 实现默认 BuffPipeline。
- 建立 Buff 事件和快照。

完成条件：

- 支持永久、限时、层数、刷新、覆盖。
- Buff 移除可清理属性修改器和事件订阅。

**状态**: ✅ 完成 v1

**v1 边界**:
- 已实现纯运行时 `BuffPipeline`、`IBuff`、`IBuffTarget`、`IBuffFactory`、`IBuffStackingPolicy`。
- 已支持添加、同 ID 堆叠刷新、Tick 过期、移除、清空、快照。
- 属性修改器清理由 `IBuffTarget.AttributeModifiers` 接口承接，不依赖具体游戏实体。
- 暂不包含具体业务 Buff、技能效果、配置加载和可视化编辑器。

## Phase 5: Modifiers

目标：将 WGame Entry 泛化为修改器管线。

任务：

- 定义 `ModifierContext` 池化策略。
- 实现 `ModifierPipeline`。
- 实现 CounterSystem。
- 定义条件和效果接口。

完成条件：

- Modifier 通过接口访问 Attributes/Buffs/Events。
- 不含元素、装备、关卡等 WGame 业务逻辑。

**状态**: ✅ 完成 v1

**v1 边界**:
- 已实现纯运行时 `ModifierPipeline`、`ModifierContext`、`ModifierBase`、`CounterStore`。
- 已支持添加、替换、移除、清空、Apply、Update、快照和事件。
- 已支持条件与效果组合，效果可通过接口访问 Attributes/Buffs/Counters。
- Counter 仅提供通用读写、累加、重置和变更事件，不包含 WGame 的具体计数器 ID/文案。

## Phase 6: Config

目标：提供配置访问抽象和测试用 Provider。

任务：

- 定义 `IConfigProvider`、`IConfigRegistry`。
- 实现内存 Provider。
- 提供 Unity ScriptableObject 适配示例。
- 保留 Luban 接入点但不依赖 Luban。

完成条件：

- 所有模块可通过接口读取配置。
- 测试不需要真实配置导出文件。

**状态**: ✅ 完成 v1

**v1 边界**:
- 已实现纯运行时 `IConfigProvider`、`IConfigRegistry`、`MemoryConfigProvider`、`ConfigRegistry`。
- 已支持 `ConfigResult<T>`、缺失配置异常、重复 ID 策略、非法 ID 检查。
- 已提供基础引用校验 API：`IConfigReferenceProvider`、`ConfigReference`、`ConfigValidator`。
- 暂不绑定 Luban、Json、ScriptableObject 或任何游戏私有配置格式。

## Phase 6.1: Config Table / Schema

目标：建立配置表、字段结构、多语言引用和 AI 友好导出。

任务：

- 定义 `IConfigTable<T>`、`ConfigTable<T>`。
- 定义 `ConfigSchema`、`ConfigField`、`ConfigFieldType`。
- 定义 `ConfigIdRange`、`ConfigReferenceRule`。
- 定义 `LocalizedTextKey`、`LocaleId`、`ILocalizationProvider`。
- 实现表级校验和 Schema 摘要导出。

完成条件：

- 支持表名、字段、ID 范围、引用字段和多语言字段。
- 能校验 ID 越界、引用缺失、多语言缺失和 Schema 重复字段。
- 能导出 AI 可读 Schema 摘要。

**状态**: ✅ 完成 v1

**v1 边界**:
- 只定义表结构与校验，不绑定 Excel、CSV、Json、Luban、ScriptableObject。
- 多语言通过 `LocalizedTextKey` 和 `ILocalizationProvider` 表达，不把具体语言列塞进业务表。
- AI 摘要是稳定文本格式，后续可扩展为 JSON。

## Phase 6.2: Config-backed Factories

目标：打通配置表到运行时对象的创建链路。

任务：

- 定义 `IBuffConfig`、`IModifierConfig`。
- 提供基础配置类 `BasicBuffConfig`、`BasicModifierConfig`。
- 实现 `ConfigBuffFactory<TConfig>`、`ConfigModifierFactory<TConfig>`。
- 提供配置创建的 `ConfiguredBuff`、`ConfiguredModifier`。
- 验证 BuffConfig 引用 ModifierConfig 的校验链路。

完成条件：

- `BuffPipeline` 可通过配置 ID 创建 Buff。
- `ModifierPipeline` 可通过配置 ID 创建 Modifier。
- 缺失配置时 factory 返回 false。
- 配置桥接层不让 Buffs/Modifiers 反向依赖 Config。

**状态**: ✅ 完成 v1

## Phase 6.3: Config Authoring

目标：让配置表对人工编辑、Editor 工具和 AI 辅助都更友好。

任务：

- 提供基于 `ConfigSchema` 的 TSV 样例模板导出。
- 提供 authoring 层结构化错误报告。
- 复用 `ConfigTableValidator`，不重复实现校验规则。
- 保持运行时纯净，不绑定 Excel、CSV、Json、Luban 或 ScriptableObject。

完成条件：

- 能导出稳定的表头、样例行、字段说明和必需语言列表。
- 能把 ID、引用、多语言等校验问题转成 Editor/AI 可消费的结构化报告。
- 后续 ConfigEditor 可以直接复用该入口。

**状态**: ✅ 完成 v1

## Phase 6.4: Config Structure Model

目标：把 WGame 审计结果转成框架可执行的统一结构模型，避免继续暴露旧数据中的裸整数、数组位序和隐式引用。

任务：

- 为 Schema 增加数据结构形态：Table、Graph、Localization、GeneratedRuntime。
- 建立枚举域注册表，支持普通 enum 和 flags。
- 字段通过 `enumId` 引用枚举域，供 Editor、AI 摘要、校验和迁移器共享。
- 建立跨源引用模型，把表格索引到 Graph、Localization、GeneratedRuntime 的关系显式化。

完成条件：

- `ConfigField` 能记录字段使用的枚举域。
- `ConfigEnumDomain` 能描述枚举值、中文显示名和 flags 组合。
- `ConfigReferenceRule` 能描述 `Graph:AIActionGraph.Id` 这类非运行时类型引用。
- `ConfigSchemaExporter` 和 `ConfigAuthoring` 能导出结构形态与 `enumId`。
- 测试覆盖 enumId 导出、flags 分解和跨源引用导出。

**状态**: ✅ 完成 v1

## Phase 6.5: Config Source Index

目标：把 Schema 中的跨源引用推进到自动校验和统计，不再只停留在文档和 AI 摘要。

任务：

- 定义 `ConfigSourceEntry`，记录配置源的 Schema、key 字段、路径、hash 和 key 集合。
- 定义 `ConfigSourceIndex`，登记 Table、Graph、Localization、GeneratedRuntime 源。
- 根据 `ConfigReferenceRule` 校验表格行引用的跨源 key 是否存在。
- 保持 Index 纯净，不绑定 TSV、JSON、Excel、Luban 或 Unity AssetDatabase。

完成条件：

- 能登记 `Graph:AIActionGraph` 这类源和 key。
- 能报告缺失跨源引用。
- 能报告缺失目标源。
- 能识别 `LocalizedTextKey` 作为 source key。
- 测试覆盖存在、缺失、源缺失和多语言 key 归一。

**状态**: ✅ 完成 v1

## Phase 6.6: External Config Editor Source

目标：给项目层真实配置源提供固定接入口，让 TSV、JSON、Luban 或其他导入器的解析结果能进入 Config Editor、Health Report 和 SourceIndex。

任务：

- 提供 `ExternalConfigEditorSource`，包装项目层已经解析好的 Schema、key、问题列表、预览、路径和 hash。
- 保持框架不解析 TSV、JSON、Excel 或 Luban。
- 让外部源自动实现 `IConfigEditorSourceIndexProvider`，参与索引统计和跨源引用校验。
- 补充项目层注册示例。

完成条件：

- 项目层可通过 `MxEditorUtils.RegisterConfigEditorSource(new ExternalConfigEditorSource(...))` 接入真实配置源。
- Health Report 能看到外部源的 source type、row count、key count、hash 和问题列表。
- SourceIndex 能登记外部源 key。

**状态**: ✅ 完成 v1

## Phase 7: Runtime AI Planner（AI 轻量 Planner）

> 本文的 AI 指 Runtime AI Planner。不包含 AIAction 配置迁移或 Authoring AI Assist 或 Development Agent。

目标：提供不依赖插件的轻量 AI 基础设施，供游戏层参考 WGame GOAP 思路扩展。

任务：

- 定义 `IAiWorldState`、`IAiGoal`、`IAiAction`、`IAiPlanner`。
- 定义条件和效果接口：`IAiCondition`、`IAiEffect`。
- 提供默认事实状态、事实目标、事实条件和设置事实效果。
- 提供 `PriorityGoalSelector` 和 `SequentialPlanner`。

完成条件：

- 游戏层不引入插件也能构建目标、动作和规划链。
- Planner 能按目标优先级选择未满足目标。
- Planner 能基于前置条件和效果找到可执行动作序列。
- AI 模块不反向污染 Buff/Modifier 实现。

**状态**: ✅ 完成 v1

## Phase 8: Editor 和工具化

目标：让框架状态可检查、可视化、可导出。

任务：

- Framework Manager。
- 依赖图检查。
- 模块状态和 API 文档检查。
- Debug 快照面板。

完成条件：

- 可在 Unity 内看到模块依赖、实现状态、验证结果。
- 可导出健康报告用于提交前检查。

## Phase 8.0: Framework Manager Skeleton

目标：建立统一 Editor 入口，后续模块编辑器都挂到这里。

任务：

- 新增 `MxFramework.Editor` 程序集，仅 Editor 平台编译。
- 新增菜单 `MxFramework > Framework Manager`。
- 展示模块列表、程序集名、依赖摘要和状态。
- 提供 `USAGE / DESIGN / INTERFACES` 文档打开按钮。
- 提供基础验证按钮，先检查模块 asmdef 是否存在。

完成条件：

- Unity Editor 中能打开 Framework Manager。
- Editor 程序集不进入 Runtime。
- 后续依赖图、ConfigEditor、Buff/Modifier sandbox、AI sandbox 能以此为入口扩展。

**状态**: ✅ 完成 v1

## Phase 8.1: Editor Mode / Debug Source Foundation

目标：统一后续可视化编辑器的编辑模式、运行模式和调试数据协议。

任务：

- Framework Manager 增加 `编辑模式 / 运行模式` 切换。
- 运行模式在非 Play Mode 下显示明确提示。
- 新增 `MxFramework.Diagnostics` 运行时程序集。
- 定义 `IFrameworkDebugSource`、`FrameworkDebugSnapshot`、`FrameworkDebugSection`。
- 提供报告导出/复制入口。

完成条件：

- 后续 ConfigEditor、Buff/Modifier sandbox、AI sandbox 都能复用同一套模式结构。
- Runtime 不依赖 Editor，Editor 通过 Diagnostics 协议读取运行时快照。
- 运行模式默认只读，不修改运行时对象。

**状态**: ✅ 完成 v1

## Phase 8.2: Config Schema Viewer

目标：在 Framework Manager 中提供配置表可视化基础入口，让配置 Schema、TSV 模板和校验报告可以被策划、程序和 AI 直接查看。

任务：

- Framework Manager 编辑模式增加 `配置表` 页。
- 展示内置 `BasicBuffConfig`、`BasicModifierConfig` Schema。
- 展示字段名、字段类型、必填状态、引用目标、说明、ID 范围和必需语言。
- 支持复制 `ConfigAuthoring.CreateTemplate(schema)` 生成的 TSV 模板。
- 支持运行内置示例表校验，验证 Schema、引用和多语言规则。
- 校验结果和 Schema 报告可通过现有复制报告入口留证。

完成条件：

- 不进入 Play Mode 也能查看配置表结构。
- 不引入 Excel、CSV、Json、Luban 或 ScriptableObject 依赖。
- Editor 复用 `MxFramework.Config` / `MxFramework.Config.Runtime` 公开 API，不绕过运行时契约。
- 用户可见 UI 文案使用中文，配置表名、字段名、类型名等技术标识保留英文。

**状态**: ✅ 完成 v1

## Phase 8.3: Config Editor Source Preview

目标：让配置表页从只看 Schema 升级为查看配置源，为项目层接入真实 Excel、CSV、Json、Luban 或 ScriptableObject 导入器做准备。

任务：

- 定义 Editor 侧 `IConfigEditorSource`，描述配置源名称、来源类型、Schema、行数、TSV 预览和校验报告。
- 提供 `MemoryConfigEditorSource<T>`，用内存表模拟真实配置源。
- Framework Manager 配置表页改为选择配置源，而不是只选择 Schema。
- 展示配置源名称、来源类型、表名、行数、字段结构、TSV 模板和最多 5 行 TSV 预览。
- `校验当前源` 只校验当前选中配置源。
- `复制报告` 输出当前配置源报告，包含 source、sourceType、table、rowCount 和 issues。

完成条件：

- 内置示例源可以显示真实行数和 TSV 行预览。
- 项目层可通过实现 `IConfigEditorSource` 接入自己的配置导入器。
- 框架仍不绑定任何具体配置文件格式或第三方生成器。
- Config 页继续保持编辑模式可用，不依赖 Play Mode。

**状态**: ✅ 完成 v1

## Phase 8.4: Config Health Check

目标：配置源接入后自动检测整体健康度，通过统计快速发现问题集中在哪些表、哪些错误类型。

任务：

- 定义 `ConfigHealthReport`、`ConfigSourceHealth`、`ConfigIssueStat`。
- Config 页打开、刷新配置源或切换配置源时自动检测所有配置源。
- 显示总配置源数、总行数、问题源数、Error 数、Warning 数。
- 统计缺失引用、多语言缺失、ID 问题和 Schema 问题。
- 按配置源输出表级状态：正常、警告、错误。
- 支持复制健康报告，包含总览统计、表级统计、错误类型统计和最多 10 条样例问题。

完成条件：

- 用户不点击校验按钮也能看到当前配置整体健康状态。
- 健康报告可直接复制给 AI 或提交前检查使用。
- 健康检测仍复用 `IConfigEditorSource.Validate()`，不重复实现配置校验规则。
- 不绑定任何具体配置文件格式或第三方生成器。

**状态**: ✅ 完成 v1

## Phase 8.5: Config Issue List / Authoring AI Assist Fix Context

目标：把健康检测发现的问题变成可处理的明细，并生成 AI 可直接使用的修复上下文。

任务：

- 定义 `ConfigIssueView`，保存配置源名和结构化问题。
- Config 页显示问题明细，支持 `全部 / Error / Warning` 基础筛选。
- 支持复制当前筛选后的问题列表。
- 支持复制当前源 AI 修复上下文。
- AI 修复上下文包含当前源信息、健康报告、Schema 模板、TSV 预览和当前源问题列表。

完成条件：

- 配置出错时可以看到具体 source、table、row、field、severity、error 和 message。
- 问题列表可复制给人或 AI。
- AI 修复上下文不要求读取源码即可理解配置结构和错误。
- 仍不绑定任何具体配置文件格式或第三方生成器。

**状态**: ✅ 完成 v1

## Phase 8.6: Config Change Detection

目标：用低成本统计发现配置源变化，让配置修改后的影响更容易被人和 AI Agent 理解。

任务：

- 定义 `ConfigChangeReport`、`ConfigSourceChange`、`ConfigIssueStatChange`。
- Config 页基于健康报告保存轻量变动基线。
- 再次检测时比较配置源新增/移除、行数、Error、Warning 和错误类型统计变化。
- 显示配置变动摘要。
- 支持复制配置变动报告。
- 支持手动重置变动基线。

完成条件：

- 不读取真实配置文件内容即可发现表级统计变化。
- 不要求项目层修改 `IConfigEditorSource`。
- 变动报告可复制给 AI，用于辅助判断本次配置修改影响。
- 仍不绑定任何具体配置文件格式或第三方生成器。

**状态**: ✅ 完成 v1

## Phase 8.7: Config Report Export Standard

目标：把配置健康、问题、变动和 AI 修复上下文统一导出为稳定报告包，方便提交前检查和 AI Agent 自动读取。

任务：

- 定义 `ConfigReportExportResult`。
- 定义固定报告目录 `Temp/MxFrameworkReports/Config/`。
- 导出 `config_health.txt`、`config_issues.txt`、`config_changes.txt`、`config_ai_context.txt`。
- 导出 `config_report_index.txt` 作为报告包索引。
- Config 页提供 `导出配置报告` 中文按钮。
- 文档说明 AI Agent 应优先读取报告包。

完成条件：

- 不依赖剪贴板即可取得完整配置检查上下文。
- 报告目录位于项目内，但不作为框架源码或 SVN 资产提交。
- 导出格式稳定、文件名稳定、便于自动化读取。
- 仍不绑定任何具体配置文件格式或第三方生成器。

**状态**: ✅ 完成 v1

## Phase 8.8: Config Precommit Check

目标：把配置健康检测、问题明细、变动检测和报告导出收束成一个提交前检查入口。

任务：

- Config 页提供 `提交前检查` 中文按钮。
- 点击后刷新配置源、重新检测健康状态和变动状态。
- 自动导出配置报告包。
- 生成 `config_precommit.txt`。
- 提交前结果分为 `ready / warning / blocked`。
- Error 数大于 0 时判定 `blocked`。
- Warning 不阻断提交，但报告中明确提示需要人工确认。

完成条件：

- 团队提交 SVN 前有明确的 Unity 内部检查入口。
- AI Agent 可以优先读取 `config_precommit.txt` 判断是否应继续修复。
- 不直接绑定 SVN hook，避免早期流程过重。
- 仍不绑定任何具体配置文件格式或第三方生成器。

**状态**: ✅ 完成 v1

## Phase 8.9: Config Workbench v0

目标：把现有配置表查看页升级为统一配置工作台，为后续 Table Editor、Graph Inspector 和 Graph Visual Editor 打基础。

任务：

- 将 Framework Manager 中的 `配置表` 入口升级为 `配置工作台`。
- 三栏布局明确为：配置工作台、结构与关系、校验与 AI。
- 小窗口下配置源区域置顶，结构和校验区域可换行并滚动。
- 结构面板显示结构类型、字段数、引用规则、enumId、ID 范围和只读阶段状态。
- 健康摘要显示当前阶段、源统计、索引统计、缺失引用、多语言缺失和 Schema 问题。
- 保持 v0 只读，不直接编辑真实配置资产。

完成条件：

- 用户可以在 Unity 内统一查看 Table、Graph、Localization 等源的 Schema 和引用关系。
- 可复制健康报告、问题列表、源预览和 AI 修复上下文。
- 界面为后续 Table Editor v1 和 Graph Inspector v1 预留清晰职责。

**状态**: ✅ 完成 v0

## Phase 8.9.1: Config Workbench Responsive Layout

目标：让配置工作台在小窗口中仍可正常查看，不因三栏固定宽度导致内容被挤出边界。

任务：

- 降低 Framework Manager 最小窗口宽度。
- 配置源和动作区置顶。
- 结构与关系、校验与 AI 区域允许换行。
- 结构面板长内容使用滚动区域。
- 字段结构行允许换行，避免列宽溢出。

完成条件：

- 小窗口下仍能访问配置源选择、健康统计、字段结构、行视图、问题列表和源预览。
- 后续新增控件时优先进入滚动区域，不继续撑破窗口。

**状态**: ✅ 完成

## Phase 8.9.2: Config Workbench Navigation Layout

目标：把配置工作台从报告面板式布局调整为日常编辑工作流布局。

任务：

- 顶部上下文栏显示当前源、结构类型、健康状态和高频动作。
- 左侧源导航使用按钮列表直接选择配置源，替代数据源下拉框。
- 中间主工作区展示结构、字段、引用、行视图和控件映射。
- 右侧检查器集中显示问题、源预览、校验和 AI 上下文。
- 低频报告动作从主体布局中收敛。

完成条件：

- 用户不需要在下拉框中查找配置源。
- 小窗口下仍能先选择源，再查看主区和检查器。
- 后续 Table Editor 和 Graph Inspector 可以复用同一工作台骨架。

**状态**: ✅ 完成

## Phase 8.9.3: Config Workbench Final Readonly Layout

目标：把配置工作台整理成最终只读形态，后续编辑能力在该布局内逐步开放。

任务：

- 主工作区改为页签：概览、字段、行视图、引用。
- 右侧检查器只保留源预览、当前源校验和辅助动作。
- 全局问题列表移动到底部问题抽屉。
- 保留顶部上下文栏和左侧源导航。
- 保持只读，不写回配置源。

完成条件：

- 日常操作路径清晰：选源、看主区、看检查器、看问题抽屉。
- 字段结构、行视图和引用关系不再堆在同一长列表里。
- 后续可在主工作区页签内逐步替换为真实编辑器。

**状态**: ✅ 完成

## Phase 8.10: Table Editor v1 Preview

目标：在不写真实配置源的前提下，先验证表格编辑器的行视图和字段控件映射。

任务：

- 定义 `IConfigEditorTablePreviewProvider`。
- 内存配置源和外部配置源提供行预览。
- 根据 `ConfigField` 推导控件类型：数值输入、开关、枚举/Flags、引用选择器、多语言 Key、资源路径、自定义控件。
- 在配置工作台中展示行视图和控件映射。
- 明确当前阶段只读，不保存配置。

完成条件：

- 用户能看到真实行值和每个字段未来应使用的编辑控件。
- enumId 和引用规则能进入控件映射。
- 后续 Table Editor v1 可以在此基础上替换为真实可编辑控件。

**状态**: ✅ 完成 Preview

## Phase 8.11: Table Editor v1 Readonly Controls

目标：把文本行预览升级成 UI Toolkit 禁用态控件，先确认编辑器交互形态。

任务：

- 布尔字段显示为开关。
- 枚举和 flags 字段显示为下拉控件外观。
- 引用字段显示为引用选择按钮外观。
- 资源字段显示为资源选择按钮外观。
- 普通字段显示为文本输入外观。
- 所有控件保持只读，不提供保存入口。

完成条件：

- 配置工作台能看到接近真实编辑器的表格行控件。
- 控件类型仍由 `ConfigField`、`enumId` 和 `ConfigReferenceRule` 推导。
- 后续可以在相同布局中逐步开放真实编辑。

**状态**: ✅ 完成 Preview

## Phase 8.12: Enum / Flags Candidate Preview

目标：让只读控件进入真实枚举语义，而不是只显示普通下拉外观。

任务：

- 定义 `IConfigEditorEnumProvider`。
- 行预览单元格携带格式化值、enumId 和候选项。
- `MemoryConfigEditorSource` 和 `ExternalConfigEditorSource` 支持 `ConfigEnumRegistry`。
- enum/flags 控件从枚举域显示候选项。
- 内置 Demo 增加 `ActionCatalog / ActionGraph`，展示 enum、flags、asset path 和跨源引用。

完成条件：

- enum/flags 当前值能显示 `中文名 英文名(数值)`。
- flags 组合值能拆分显示。
- 下拉外观能看到候选项。
- 不引入 WGame 真实数据。

**状态**: ✅ 完成 Preview

## Phase 8.13: Pure Config Demo Package

目标：让框架主干拥有完整但纯净的配置 Demo，用于验证编辑器和 AI 辅助流程，不引入真实项目数据。

任务：

- 将内置 Demo 从 `MxEditorUtils` 拆分到 `ConfigDemoSources`。
- Demo 源只使用框架内的虚构数据，不绑定任何真实项目路径或业务含义。
- 增加 `Docs/Demo/CONFIG_DEMO.md`，说明 Demo 边界、内置源、TSV 结构和可选坏引用示例。
- `ExternalConfigEditorSource` 增加基于预览行的跨源引用 key 校验，能发现 preview 中的缺失引用。

完成条件：

- Framework Manager 默认能看到四个纯净 Demo 源。
- Demo 覆盖多语言、运行时引用、enum、flags、asset path、Graph 引用。
- 可选坏引用 Demo 不默认注册，不污染提交前检查。
- 文档明确真实数据不得进入框架主干。

**状态**: ✅ 完成 Preview

## Phase 8.14: Field Display Alias

目标：让配置工作台面向策划和 AI 更易读，同时保持字段 key 稳定。

任务：

- Demo Schema 和基础运行时配置补充中文 `ConfigField.DisplayName`。
- 字段列表和行视图优先显示中文别名，并保留原字段名。
- 问题报告和 AI 上下文输出 `field=` 原 key，并补充 `fieldDisplay=`。
- 文档明确中文别名只属于 UI 显示层，不能替代真实字段 key。

完成条件：

- UI 中字段可读性提升。
- 导入、导出、引用校验、AI 修复仍使用英文 key。
- 项目侧 Adapter 可以按同一规则补充中文别名。

**状态**: ✅ 完成 Preview

## Phase 8.15: Field Inspector / Reference Jump

目标：让配置工作台从表级预览进入字段级理解。

任务：

- 字段页增加 `详情` 操作，右侧检查器显示字段元数据。
- enum/flags 字段详情显示候选项。
- ConfigReference 字段详情提供 `查看目标源` 跳转到目标配置源。
- AI 上下文追加当前选中字段摘要。

完成条件：

- 选中字段后无需读源码即可理解字段含义、控件形态、枚举域和引用目标。
- 引用跳转只改变编辑器选择状态，不修改配置资产。
- 框架仍不引入真实项目数据。

**状态**: ✅ 完成 Preview

## Phase 8.16: Buff Runtime Patch / Mod Vertical Slice

目标：先用 Buff / Modifier 跑通运行时动态更新和 Mod 配置的最小闭环。

任务：

- 定义 `ConfigLayerKind`，区分 Base、Patch、Mod、Debug。
- 定义 `ConfigPatchEntry<T>`，表达运行时 Upsert / Remove 操作。
- 定义 `RuntimeConfigPatchMerger`，把 Base 行和 Patch/Mod 行合并为新的 `ConfigTable<T>`。
- 定义 `ConfigChangeSet`，记录新增、替换、删除和无变化。
- 用 `BasicBuffConfig` / `BasicModifierConfig` 验证覆盖、新增、删除和引用校验。

完成条件：

- Patch 可以覆盖 Buff 行并生成 Replaced 变更。
- Mod 可以新增 Buff 和 Modifier，并通过 Buff -> Modifier 引用校验。
- Remove 可以从运行时 merged view 删除 Buff。
- 不读取真实项目数据，不保存回编辑源。

**状态**: ✅ 完成 Preview

## Phase 8.17: Portable Authoring Workflow Spec

目标：把 Buff 创建、Mod 编辑器、AI 协作和多前端支持统一到可移植工作流协议。

任务：

- 新增 `Docs/AUTHORING_WORKFLOW.md`。
- 明确 Workflow 不绑定 Unity Editor，也不要求完整源码。
- 定义 Player Mode、Developer Mode、AI Mode、Tool Mode。
- 定义 Workflow、Step、QuickAction 的数据结构。
- 将易用性、容错率和性能写成硬性规范。
- 用 Buff 创建流程给出玩家/开发者/AI/工具可协作的模板。

完成条件：

- 后续 Unity 内部编辑器、外部 Mod Editor、AI agent 和 CLI 都能以同一协议为入口。
- 玩家模式不暴露危险字段和 Base 修改能力。
- 运行时只消费 merged snapshot。
- 每个步骤都能脱离完整代码库生成当前上下文。

**状态**: ✅ 完成 Spec

## Phase 8.18: Buff Authoring Workflow Demo

目标：先把 Buff 创建做成可执行的工作流骨架，让玩家、开发者、AI 和工具能围绕同一套步骤协作。

任务：

- 定义 runtime-agnostic 的 `AuthoringWorkflow`、`AuthoringWorkflowStep` 和 `AuthoringQuickAction`。
- 提供 `BuffAuthoringWorkflowTemplate.CreateCreateBuffWorkflow(...)`。
- 外部 Authoring Editor 展示创作流程；Unity Framework Manager 不再内置创作流程页。
- 支持多条进行中的 Buff 流程。
- 展示步骤状态、Actor、输入、输出、校验、AI 提示和快捷动作。
- 支持复制当前步骤 AI 上下文。
- 保持只读，不保存配置源，不导入 WGame 真实数据。

完成条件：

- 创建 Buff 的流程能脱离 Unity 运行时和完整源码表达。
- Unity Editor 内能看到多流程列表和当前步骤详情。
- Modifier 引用步骤能明确暴露字段和目标源快捷动作。
- AI 上下文只包含当前 workflow / step / target。
- 后续可以在同一结构上接入字段跳转、校验、合并预览和 Mod Patch 导出。

**状态**: ✅ 完成 Demo

## Phase 8.19: WGame Buff Authoring Logic

目标：修正 Buff 流水线的操作逻辑，让编辑器入口贴合 WGame 的 `BuffType` 和 `BuffData` 子类，同时保留框架化 Workflow 协议。

任务：

- 新增 `Docs/WGAME_BUFF_AUTHORING_WORKFLOW.md`。
- 明确 `Numerical / Condition / ChangeAttr / DamageByAttr / CastOrb* / Positive / Status` 的类型入口。
- 将流程从泛化 Modifier 入口调整为 `BuffType -> 公共字段 -> 堆叠持续 -> 类型专属字段 -> 表现 -> 引用`。
- 保留 `AuthoringWorkflow`、`QuickAction`、步骤级 AI 上下文和 Patch/Mod 层。
- Framework Manager 内置 Demo 使用不同 Buff 类型模板。

完成条件：

- 操作逻辑能被 WGame 现有 Buff 设计经验直接理解。
- 框架不复制 WGame 真实数据，也不绑定旧 JSON 数组下标作为 UI 心智模型。
- 后续 UI 可以按 BuffType 动态展示字段组。

**状态**: ✅ 完成 Logic

## Phase 9.0: WGame Data Audit / Unified Config Strategy

目标：先汇总 WGame 当前真实数据来源，再设计“编辑时易读、运行时统一”的配置结构。

任务：

- 汇总 Luban Excel、BaseDataJson、SplitAbilityData、SplitAIActionData、SplitAIConfigData、SplitBuffData、bytes 和 SaveData 的职责。
- 区分权威编辑源、运行时导出产物、工具缓存和存档数据。
- 建立配置源与运行时格式策略草案。
- 将 Ability JSON 细节分析拆为独立任务，避免在主线中展开大量样本。
- 根据 Ability JSON 审计结果沉淀 Ability Graph 初步结构。
- 审计 WGame 各数据源之间的关系，特别是表格索引到 Graph 的桥接链路。
- 审计 `SplitAIActionData`、`SplitAIConfigData`、`SplitBuffData` 的字段位序和类型结构。
- 汇总 Luban/BaseData 表字段、引用、多语言列和枚举列。
- 汇总常见枚举、flags 和 C# 常量域，供可视化编辑器使用。
- 保持 WGameFramework 不提交 WGame 真实业务数据。

完成条件：

- `Docs/WGAME_DATA_AUDIT.md` 记录当前数据来源、规模、代码使用证据和待补审计项。
- `Docs/CONFIG_FORMAT_STRATEGY.md` 明确 TSV/JSON/Schema/bytes 的分层职责。
- `Docs/WGAME_DATA_RELATION_AUDIT.md` 明确 AIAction、AIConfig、Buff、Talent、Character/Weapon 和 Map 的跨源引用关系。
- `Docs/WGAME_SPLIT_GRAPH_AUDIT.md` 明确 AIAction、AIConfig、Buff 的 Split JSON 主干结构。
- `Docs/WGAME_TABLE_FIELD_INDEX.md` 建立表字段索引。
- `Docs/WGAME_ENUM_MAPPING_AUDIT.md` 建立枚举映射索引。
- `Docs/Tasks/ABILITY_JSON_AUDIT_TASK.md` 明确 Ability JSON 独立分析输入、产出和验收标准。
- `Docs/Tasks/ABILITY_JSON_AUDIT_RESULT.md` 记录 Ability JSON 字段、事件类型、异常和命名建议。
- 后续能基于审计结果选择首个配置迁移试点准备方向。

**状态**: ✅ 完成 / closed（2026-05-09，文档契约收口）

**收尾计划**: `Docs/Tasks/PHASE9_CLOSEOUT_PLAN.md`

**关闭说明**:
- P9.1-P9.4 已完成并接受，收口证据见 `Docs/Tasks/PHASE9_CLOSEOUT_REPORT.md`。
- 下一阶段已创建 `Docs/Tasks/AI_ACTION_MIGRATION_PILOT_01_CONTRACT_FIXTURE.md`，先用 synthetic fixture 验证 `Table + Graph + Reference` 契约链路。
- 该状态不代表已导入 WGame 真实业务数据，也不代表 `FatOgre_3` 等待确认风险已解决。

## Phase 10.0: External Authoring Editor Program

目标：将“编辑友好 + AI 辅助友好 + 实时预览 + Mod / 开发双模式”提升为独立大开发需求，明确外部主创编辑器、Authoring Core、游戏运行时预览器和 Unity Bridge 的职责。

任务：

- 新增 `Docs/AUTHORING_EDITOR_PROGRAM.md`。
- 新增 `Docs/Tasks/AUTHORING_EDITOR_EPIC.md`。
- 拆分 Authoring Core / CLI、Buff MVP、Runtime Preview、AI Assist、Mod / Developer Modes、Unity Bridge 六个子需求。
- 明确 Unity Editor 不作为主创工具，只作为开发者桥接和验证入口。
- 明确 Authoring Core 物理形态、Patch / Mod 包格式、运行时预览通信协议、安全策略、兼容性、规模预算和可测验收指标。
- 定义统一流程状态：Draft、Spec Ready、Core Ready、Tool Verified、UI Integrated、Preview Verified、Docs Ready、Done。

完成条件：

- 后续编辑器开发可以按子需求独立推进和验收。
- Buff 外部编辑器 MVP 不再和 Unity Editor 面板混在一起。
- 每个子需求都有目标、范围、依赖和验收标准。
- EPIC 状态进入 `Spec Ready (pending sub-tasks)`。

**状态**: ✅ 完成 Planning / EPIC Spec Ready

**v0.1 进展**: 已完成 Core/CLI Workflow 路由、嵌套字段、ManifestAwareValidator、LayeredMerger、退出码契约、schemaVersion 兼容窗口、AI step context 雏形、EditorServer 安全修复、Mod 样例。

**v0.2 进展**: AuthoringValidate 统一入口、PackageValidator 解耦内置 schema、EditorServer 包参数化与 /api/packages、samples 忽略 reports/preview、AI 上下文 enumSlice/referenceSummary/allowedActions、precommit 命令、SchemaField.IsList、UI 实时校验。

**v0.3 Spec**: Runtime Preview 协议 Spec Ready —— 连接描述文件、`preview.handshake/loadPatch/applyBuff/reset/getSnapshot/getLogs` JSON-RPC 方法、错误码、`RuntimePreviewResult` 结构，以及 03.1~03.5 实施分阶段，详见 `Docs/Tasks/AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md`。

**v0.3 进展**: Runtime Preview 03.2 Authoring 客户端 + Mock Server + preview CLI；02 UI 收尾（创建 Buff 向导、Mod/Developer 模式、多语言可写）；CLI 退出码扩展 4 = preview unavailable。

**v0.3 Unity 进展**: Runtime Preview 03.3 PreviewRpcServer + DummyPreviewWorld + Bootstrap + Editor 菜单；纯框架运行，不依赖 WGame 真实数据。

**v0.4 进展**: Runtime Preview 03.4 编辑器 UI/API 接入；EditorServer 提供 `/api/preview/status` 和 `/api/preview/run`，外部编辑器显示运行时预览面板。Unity Preview Server 在 Edit Mode 可启动，Authoring 端可握手并加载 Patch；当前 `applyBuff` 仍停在 DummyPreviewWorld 无真实 `IBuffFactory` 的 2003 预期边界。

## Phase 10.1: Authoring Core / CLI v0

目标：建立脱离 Unity 的 Authoring Core / CLI 骨架，为外部编辑器、AI、CLI 和 Unity Bridge 共用同一套协议打基础。

任务：

- 新增 `Tools/MxFramework.Authoring/`。
- 建立 `.NET Standard 2.1` 的 `MxFramework.Authoring.Core`。
- 建立 `authoring` CLI。
- 支持内置 Buff Workflow、Buff Schema、Patch validate、merge-preview、report 和 step context。
- 支持 Project Authoring Manifest export / inspect，包含 Schema、Enum、Reference、Workflow、Localization。
- 支持 report bundle 写文件。
- 增加 Buff Preview ModPackage 样例。
- 增加 Project Authoring Manifest 样例。
- 增加无第三方依赖的测试控制台。

完成条件：

- Core 可以不打开 Unity 独立构建。
- CLI 可读取样例 ModPackage 并输出校验报告和合并预览。
- CLI 可导出并 inspect Project Authoring Manifest。
- CLI 可写出稳定报告包目录，供外部编辑器和 AI 读取。
- 测试覆盖步骤上下文、Base 写入阻断和 Patch 合并。

**状态**: ✅ 完成 v0.1

## Phase 10.2: Buff External Editor UI Skeleton

目标：建立第一个能打开的外部 Buff 主创编辑器骨架，验证外部 UI 可以消费 Authoring Core 输出的 manifest、ModPackage 和 report bundle。

任务：

- 新增 `Tools/MxFramework.Authoring.Editor/`。
- 提供本地 Web UI 和启动脚本。
- 读取 Project Authoring Manifest 样例。
- 读取 Buff Preview ModPackage 样例。
- 展示 Workflow 步骤、Buff 列表、Schema 字段、Enum、Reference、Localization、Validation Report 和 Merge Preview。
- 支持复制当前步骤 AI 上下文。

完成条件：

- 不打开 Unity 即可通过本地 URL 打开编辑器。
- 页面资源、manifest、report bundle 都能通过本地服务读取。
- UI 文案中文为主，技术 key 保留英文副标。
- 当前阶段只读，不保存配置源。

**状态**: ✅ 完成 UI Skeleton

## Phase 10.3: Buff External Editor Editable Local Loop

目标：让外部 Buff 主创编辑器从只读骨架推进到可用的本地编辑闭环，先不绑定 Unity 和游戏运行时。

任务：

- Authoring CLI 增加 `editor serve` 本地 API。
- 外部编辑器优先读取 `/api/state`，静态服务不可写时降级为只读预览。
- 支持编辑示例 Buff Patch 字段，并进行必填字段即时提示。
- 支持保存 `samples/buff-preview/patches/buff.patch.json`。
- 支持一键重新生成 report bundle，并刷新校验报告和合并预览。
- 运行时预览按钮保留入口，但明确提示需要后续游戏 Preview Server。

完成条件：

- 不打开 Unity 即可通过本地 URL 修改示例 Buff Patch。
- 保存后 Authoring Core 能重新读取 Patch。
- 生成报告后页面显示最新 validation report 和 merge preview。
- 静态打开页面时不会假装可写，明确提示只读。

**状态**: ✅ 完成 Local MVP

## Phase 10.4: Buff Type-Aware Field Authoring

目标：修正 Buff 编辑器“所有字段混在一起、类型差异不清楚”的问题，让编辑器按 WGame BuffType 显示当前类型真正需要填写的字段。

任务：

- Buff Schema 字段增加分组、单位和 BuffType 可见性元数据。
- 补充 WGame 风格的 Buff 类型专属字段：Numerical、Condition、ChangeAttr、DamageByAttr、CastOrb*、Positive、Status。
- 补充目标、属性、伤害、元素、状态、条件和位置等基础枚举域。
- 外部编辑器按公共字段、目标 / 堆叠 / 持续、类型专属字段、表现资源分组显示。
- 外部编辑器根据当前 BuffType 动态显示当前类型相关字段。
- Authoring Core Validator 校验当前 BuffType 下可见的必填字段。

完成条件：

- 用户选择不同 BuffType 后，字段集合会变化。
- 用户能通过中文字段名、英文 key、单位和说明理解字段用途。
- 示例 DamageByAttr Buff 能通过 Authoring Core 校验并生成 ready report。
- 类型专属必填字段缺失时，UI 和 CLI report 都能定位到字段。

**状态**: ✅ 完成 v0

## Phase 10.5: Runtime Preview Editor Integration

目标：让外部 Buff 编辑器的“运行时预览”按钮真正接入本机 Unity Preview Server，而不是只显示占位提示。

任务：

- EditorServer 增加 `/api/preview/status`，自动读取连接描述文件并执行握手。
- EditorServer 增加 `/api/preview/run`，按当前 package 依次调用 `preview.loadPatch`、`preview.applyBuff` 和 `preview.getLogs`。
- 外部编辑器增加运行时预览面板，展示连接、Patch 加载、Buff 结果、错误码、日志和性能摘要。
- Unity Preview Server 改为轻量 TCP WebSocket 握手/帧处理，避免 Unity Mono `HttpListener.IsWebSocketRequest` 兼容问题。
- Unity Preview Server 支持 Edit Mode 启动，Edit Mode 下 DummyPreviewWorld / MemoryBuffPatchLoader 使用内联 dispatcher。

完成条件：

- 未启动 Unity Preview Server 时，UI 和 API 返回 clear unavailable，而不是 500。
- 启动 Unity Preview Server 后，Authoring 端能握手并获得 capabilities。
- 外部编辑器能触发 Patch 加载并显示运行时返回结果。
- 当前未接真实 BuffFactory 时，`applyBuff` 返回结构化 2003 错误，作为 03.5 前的已知边界。

**状态**: ✅ 完成 03.4

## Phase 11: Runtime Gameplay Foundation

目标：把 Gameplay 从 Demo 逻辑推进为可复用运行时能力，固定公共 API、配置驱动、诊断、配置变更和 Ability authoring contract。

完成记录：
- M1-M5 已收口：公共 API、配置驱动 Ability、诊断快照、运行时配置变更处理和 Ability authoring contract。
- `GameplayRuntimeModule` 已接入 `RuntimeHost` / `RuntimeCommandBuffer` / `RuntimeEventQueue` 主线。
- Ability Graph v0 已完成定义、执行、阶段时间线、配置映射、诊断 trace 和 runtime hash。

**状态**: ✅ Accepted / Closed（2026-05-09）

## Phase 11.1: Gameplay Component Runtime（ECS-style v0）

目标：沉淀 command-driven、component-native 的 Gameplay 运行时，验证 entity/component/store/system/event/hash/SaveState 的最小闭环。

完成记录：
- `GAMEPLAY_ECS_STYLE_00`-`19` 已形成 v0 component runtime 链路：design contract、component store、system pipeline、v0 API bridge、core components、query、component world、entity commands、diagnostics、schema registry、runtime hash、SaveState、state systems、spawn definitions、attribute runtime、ability command bridge、ability targeting、ability rules 和 vertical slice。
- `GameplayComponentRuntimeShowcase` 已作为可观察 Runtime Slice 展示 spawn、target、ability rules、cleanup、events、hash 和 SaveState；Editor 菜单可生成 Unity 场景。
- `GameplayComponentBuffSetComponent` / `GameplayComponentModifierSetComponent` 已提供 component-native buff / additive modifier state、cleanup、diagnostics、hash 和 SaveState。

**状态**: ✅ v0.1 / Runtime Slice

## Phase 11.2: Runtime Foundation 与运行时工具层

目标：补齐 RuntimeHost、Frame/Command/Replay、SaveState 之外的高频运行时工具，支撑 Demo、Ability、UI、Combat、SceneFlow 和 Resources 的稳定验证。

完成记录：
- Runtime Foundation 01-03 已完成 Host Core、Frame/Command/Replay 和 SaveState contract / orchestration。
- Runtime Foundation 04 v1 parallel closeout 已 `Completed / Verified`。
- Runtime 工具层已记录并沉淀 `StableHandleTable`、`RingBuffer<T>`、`ObjectPool<T>` / `ReferencePool<T>`、`TimerScheduler`、`DeterministicRandom`、`RuntimeEventQueue<T>`、Cooldown / Dirty / Version / Operation / RateLimiter / Debouncer / CommandRegistry 等能力路线。

**状态**: ✅ Completed / Verified

## Phase 12: UI Toolkit Runtime Showcase

目标：把 Runtime Vertical Slice 的 HUD 和配置窗口沉淀为可复用 UI Toolkit 控件、主题 token 和运行时展示路径。

完成记录：
- M1-M5 已验收，Runtime Showcase 已覆盖手动控制、Patch / Mod Package 加载、Ability 重建、Old/New 配置对比、Diagnostic View 和 Mini Game Feedback。
- M6 已从 Showcase 沉淀 `MxStatBar`、`MxCommandButton`、`MxStatusBadge`、`MxEventLog`、`MxPanelTabs` 等可复用控件和主题 token。

**状态**: ✅ M6 accepted

## Phase 13: Observability and Developer Workflow

目标：把已有 Diagnostics、Logging、RuntimeHost、Resources、Gameplay、Combat、Config Runtime 和 Input 的开发观察能力统一接入开发者工作流，让 Debug UI、source adapters、event timeline、performance、simulation、hot reload、input adapter 和 command gate 都有稳定入口。

阶段边界：
- Phase 13 不是玩法功能扩张，不新增 WGame 业务规则、关卡逻辑或可写调试命令。
- Debug UI 默认只读，优先顺序是 Core registry -> UI Toolkit overlay -> source adapters；可写命令必须经过独立 command gate。
- Debug UI 的展开、折叠、刷新暂停、选中 tab 等状态只属于表现层，不进入 Replay、SaveState 或 Runtime hash。
- Hot Reload 是显式 Config Runtime patch reload，不热重载 Unity 序列化资产，也不修改 replay、save state 或 runtime hash。

任务：
- `Tasks/PHASE13_OBSERVABILITY_AND_DEVELOPER_WORKFLOW.md`：Phase 13 总览、实施顺序和完成定义。
- `Interfaces/DebugUI.md`：Debug UI core、Toolkit overlay 和 adapter 接入契约。
- #179：`MxFramework.DebugUI` noEngine source registry、snapshot aggregator、dashboard view model。
- #180：`MxFramework.DebugUI.Toolkit` Runtime UI Toolkit overlay shell。
- #181：Framework Debug Source adapters，接入 Logging、RuntimeHost、Resources、Gameplay 和 Combat。
- #182：Event timeline 和 entity watch，首批接入 Gameplay / Combat 诊断事实。
- #183：Diagnostics performance counters，默认 opt-in，不改变 runtime authority。
- #184：Simulation Harness batch reports，支持 noEngine scenario、Markdown / JSON report 和 Debug Source export。
- #185：Config Runtime patch hot reload，支持显式 reload request、hash / duration / changed tables / errors 和 Debug UI result source。
- #186：Debug UI input adapter 和 command gate，复用 `InputContext.Debug` 与 debug intents，命令默认 disabled。
- #187：Observability 调试指南，覆盖 source registration、overlay、timeline、counters、Simulation Harness、hot reload、input 和 command gate。

完成条件：
- `MxFramework.DebugUI` 无 UnityEngine / UnityEditor / UIElements / Input System 引用。
- Debug source registry 支持普通对象生命周期、ordinal 唯一 source name、unavailable source 展示和异常隔离。
- UI Toolkit overlay 支持 Hidden / Collapsed / Expanded，并能展示空 dashboard、source sections 和 aggregator errors。
- 至少一个现有 Demo 或 Showcase 能创建统一 Debug UI source registry。
- Timeline / Entity Watch / Performance / Simulation report 均通过只读 Diagnostics / Debug UI sections 观察，不进入 Replay、SaveState 或 Runtime hash。
- Config Runtime hot reload 失败不切换 active provider，成功结果可通过 Demo composition root 和 Debug UI source 观察。
- Debug UI input adapter 不直接读取 Unity device API；command gate 默认关闭并要求 descriptor、risk 和 confirmation。
- `Docs/Guides/OBSERVABILITY_DEBUGGING_GUIDE.md` 只描述已实现 API。
- 现有 Diagnostics / Logging / Resources / Runtime / Gameplay / Combat 测试不回退。

**状态**: 🔄 #185-#187 implementation in review

## Phase 14: Camera Management

目标：补齐框架级运行时相机管理能力，提供 noEngine 相机契约、自研 Unity Camera backend、多目标入镜、profile blend、shake / focus、Character Control facing basis 辅助和 Debug UI 诊断。

阶段边界：
- 首版自研轻量相机系统，不引入 Cinemachine 硬依赖。
- `MxFramework.Camera` 只保存 profile、request、target snapshot、group framing、evaluated state、evaluation result 和 diagnostics，不引用 UnityEngine / UnityEditor / Input System / UI Toolkit。
- Unity Camera、Transform、FOV、orthographic size 和 LateUpdate 应用放在 `MxFramework.Camera.Unity`。
- 相机是表现层能力，不进入 Gameplay / Combat authority、Runtime result hash、Replay hash 或 SaveState 默认内容。
- 多目标入镜是首版核心能力，覆盖玩家 + 敌人、多人本地、Boss / arena framing 和正交俯视视角。
- request 解析、target lost、fallback、backend failure 必须有稳定诊断码，不能静默 fallback。

任务：
- #231 / `Tasks/CAMERA_MANAGEMENT_01_DESIGN.md`：相机模块设计契约、noEngine / Unity backend 边界、request 解析、多目标入镜算法、诊断语义和 implementation slices。
- #234：`MxFramework.Camera` noEngine core，包含 profile、request、target group solver、service、Null backend 和 tests。
- #235：`MxFramework.Camera.Unity` backend MVP，包含 Unity rig、target binder、LateUpdate apply、single / group follow 验证。
- #236：Demo migration，把 `RuntimeCombatShowcaseInputController` 的核心 camera follow / orbit logic 收敛到 `MxCameraUnityRig`。
- #237：Animation / Combat presentation event sink，把 camera shake / focus / impulse 接入表现事件。
- #238：Camera Debug UI source。
- #239：Camera profile authoring / validation MVP。

完成条件：
- `MxFramework.Camera` 无 UnityEngine / UnityEditor / Cinemachine / Input System 引用。
- noEngine tests 覆盖单目标、多目标、target lost、profile blend、zoom、shake 和 diagnostics。
- Unity PlayMode tests 覆盖实际 Camera transform / FOV / orthographic size 应用和 LateUpdate 顺序。
- 至少一个 Demo 使用相机 backend 代替手写 `Camera.main` / orbit / follow。
- Character Control camera-facing 输入通过组合根 resolver 接入，core 不反向依赖 Camera。
- Debug snapshot 可定位 active profile、target group、framing bounds、shake queue 和 recent errors。

**状态**: 🔄 #234-#239 implementation in progress

## Phase 15: Rendering Framework Bus

目标：在当前 URP 项目基线上沉淀框架级 Rendering 系统总线，统一 GlobalFrameContext、CameraRenderContext、SharedRTRegistry、FeaturePipeline、MaterialBindingHub、RenderDataPublisher、VolumeBlender diagnostics 和 Demo Showcase，让后续渲染能力通过同一入口调度和观测。

阶段边界：
- `MxFramework.Rendering` 是 Unity + URP-facing 程序集，允许引用 UnityEngine、UnityEngine.Rendering 和 UnityEngine.Rendering.Universal。
- Core / Runtime / Gameplay / Combat / Buffs / Resources / Runtime AI Planner 等 noEngine 模块不得引用 Rendering。
- Rendering 自身不引用 Gameplay、Combat、Character、Buffs、Animation 或 Camera；源模块接入必须放在 `MxFramework.Rendering.<Source>Bridge` 可选程序集或组合根中。
- `MxFrameworkUniversalRenderer.asset` 上唯一允许的框架级 Renderer Feature 是 `MxRenderingPipelineFeature`；具体能力实现为 `IMxRenderPass` 或 `IMxRenderPassProvider`。
- Rendering state 不进入 runtime authority、Replay、SaveState 或 Runtime hash。
- Phase 15 不定义具体草、水、角色或其它生产 shader 实现。

任务：
- 15.0 Spec：`Docs/RENDERING_FRAMEWORK_DESIGN.md`、`Docs/Interfaces/Rendering.md` 和相关索引 / 质量门禁同步。✅ 已完成
- 15.1：`GlobalFrameContext` + `MxRenderingPipelineFeature` 骨架 + `_MxTime` / `_MxWindDirection` 注入 + Diagnostics source。✅ 已完成
- 15.2：`SharedRTRegistry` + R-RT-01 至 R-RT-08 冲突规则 + dummy RT pass 验证 + SharedRT diagnostics。✅ 已完成
- 15.3：`CameraRenderContext` + FeaturePipeline phase/order 排序 + camera kind 过滤 + pipeline topology diagnostics。✅ 已完成
- 15.4：`MaterialBindingHub` channel 写入、合并、duplicate diagnostics 和 debug source。✅ 已完成
- 15.5：`RenderDataPublisher` generic semantic events、subject lifecycle、counters 和 diagnostics。✅ 已完成
- 15.6：`MxFramework.Rendering.GameplayBridge` lifecycle subset。✅ 已完成
- 15.7：`VolumeBlender` request arbitration、stable tie-breaker、scope isolation、final applied snapshot 和 diagnostics；runtime URP `Volume` object application 未实现。✅ 已完成
- 15.8：Rendering Demo Slices Showcase，验证 Context、SharedRT / FeaturePipeline、MaterialBindingHub、RenderDataPublisher 和 VolumeBlender diagnostics。✅ 已完成
- 15.9：`Docs/RENDERING_AUTHORING_GUIDE.md` authoring 唯一入口和文档同步。✅ 已完成
- Future：VolumeBlender runtime URP Volume object application、Combat / Character / Camera bridges、feature-specific production shader slices。

完成条件：
- Rendering 设计文档明确依赖边界、业务词命名约束、唯一 URP Feature 入口、Context 分层、SharedRT 冲突语义和 Bridge 命名规则。
- `Docs/Interfaces/Rendering.md` 只列 Rendering infrastructure public surface，不包含特性专用 API。
- `Docs/RENDERING_AUTHORING_GUIDE.md` 是 Rendering authoring 唯一入口，覆盖 shader globals、camera globals、SharedRT、pass/provider、MaterialBindingHub、RenderDataPublisher、VolumeBlender、Demo 和 diagnostics。
- SharedRT 八类冲突都有可测试 rule id。
- Debug/Diagnostics 使用 `MxFramework.Diagnostics.IFrameworkDebugSource`，不让 Rendering 依赖 Debug UI。
- 后续能力 PR 不允许绕过 FeaturePipeline、SharedRTRegistry、Context surface、MaterialBindingHub 或 reviewed VolumeBlender request API。

**状态**: ✅ 15.1-15.9 accepted; Future runtime Volume application / feature slices deferred

## Phase 16: Character Action Layer

目标：将角色动作从 CharacterControl v0（`CharacterActionRequest` / `CharacterActionController`）推进到可配置、可调试的新 CharacterAction 层（Resolver → Plan → Runner → TrackAdapters → Workstation）。

当前状况：
- Phase 1-7 代码已合并到 `MxFramework.CharacterAction` 程序集，共 8,391 行代码、19 个文件、10 个测试文件。
- 核心能力包括：`CharacterActionIntentRequest` → `CharacterActionResolver` → `CharacterActionPlan` → `CharacterActionRunner` → `CharacterActionTrackAdapter`（Motion/Combat/Gameplay/Animation/Audio/VFX/Camera/UI sink），以及 `CharacterActionWorkstation` 诊断工作站、`CharacterReactionContextBuilder` 反应构建器。
- 该层当前**完全孤立**：没有任何 asmdef 引用 `MxFramework.CharacterAction`；`CharacterControl` 仍使用 v0 路径；Demo/Playable 场景使用旧 `CharacterActionController`。
- 无独立接口文档、无 CAPABILITIES 条目、无 USAGE 示例。

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| Phase 1 | ReactionContext + Phase Authority Contracts | ✅ 代码已合并 |
| Phase 2.1 | Resolver + Validation MVP | ✅ 代码已合并 |
| Phase 2.2-2.7 | Plan Rebaseline、Candidate、Reaction Determinism、Timeline/Cancel、Resource Dependency、Integration Tests | ✅ 代码已合并 |
| Phase 3 | Runner noEngine MVP | ✅ 代码已合并 |
| Phase 4 | Motion Combat Gameplay Adapter | ✅ 代码已合并 |
| Phase 5 | Animation Presentation Adapter | ✅ 代码已合并 |
| Phase 6 | Reaction Bridge (CombatHitResult → ReactionContext) | ✅ 代码已合并 |
| Phase 7 | Workstation MVP | ✅ 代码已合并 |
| **集成** | 桥接 CharacterControl v0 → CharacterAction 新层 | 📋 需要设计 |
| **Playable Demo** | 角色动作可玩场景 | 📋 需要设计 |
