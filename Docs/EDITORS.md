# MxFramework 编辑器规范

> Status: Guide
>
> 版本 0.3.0 | 2026-05-05
> 
> 定义 Unity Editor 中所有编辑器工具的需求和接口。

> 注意：Unity Editor 不是最终主创工具。面向玩家、策划和 AI 协作的主入口是外部主创编辑器；Unity Editor 只承担开发者桥接、导出、调试和验证。总规划见 `Docs/AUTHORING_EDITOR_PROGRAM.md`。

> Editor 工具不是装饰性面板，而是 `QUALITY_GATE.md` 的可视化执行入口。所有验证结果都应能复制或导出，便于 SVN 提交前留证。

> 可视化工具的用户界面默认使用中文。类名、程序集名、菜单路径、配置 key、日志中的技术标识保留英文。

---

## 1. 总体架构

```
MxFramework.Editor (asmdef)
├── FrameworkManager.cs        —— 框架总管理器窗口
├── MxEditorBase.cs            —— 编辑器基类
├── MxEditorUtils.cs           —— 通用编辑器工具
│
├── ModuleEditors/
│   ├── AttributesEditor.cs    —— 属性模块编辑器
│   ├── ModifiersEditor.cs     —— 修改器模块编辑器
│   ├── BuffsEditor.cs         —— Buff 模块编辑器
│   ├── AIEditor.cs            —— AI 模块编辑器
│   └── ConfigEditor.cs       —— 配置模块编辑器
│
└── GraphView/
    ├── DependencyGraphView.cs —— 模块依赖图可视化
    └── ModifierGraphView.cs   —— 修改器节点编辑器
```

## 2. 框架总管理器 (FrameworkManager)

### 菜单路径
`MxFramework > Framework Manager`

### Phase 8.0 范围

- 已提供 `MxFramework.Editor` 程序集和 Framework Manager 入口。
- 已展示模块列表、程序集名、依赖摘要和状态。
- 已提供 `USAGE / DESIGN / INTERFACES` 文档入口。
- 已提供基础验证：检查模块 asmdef 是否存在。
- 用户可见 UI 文案使用中文，技术标识保留英文。
- 已提供 `编辑模式 / 运行模式` 切换。
- 已提供复制报告入口。
- 运行模式通过 `IFrameworkDebugSource` 接入运行时快照，当前阶段为只读提示。
- 已提供 Config Workbench v0：配置源选择、Schema 查看、字段结构、引用规则、行数、源预览、自动健康检测、问题明细、当前源校验和 AI 上下文导出。
- Buff 创作流程不再放在 Unity Editor 内部维护；当前统一转向外部 Authoring Editor。
- Framework Manager 已拆分为窗口壳、模块/验证、配置工作台、运行模式、工具入口和 UI helper 文件；新增入口默认通过 `FrameworkManagerToolRegistry` 注册，避免继续扩大单个窗口文件。
- 暂不包含 GraphView、模块 sandbox、完整配置资产编辑器或真实 Mod 包导出。

### Config Workbench Layout

- 入口：`编辑模式 > 配置工作台`。
- 主窗口采用单窗口工作台，不拆成多个主编辑窗口。
- 顶部上下文栏负责当前源、结构类型、健康状态和高频动作。
- 左侧源导航负责直接选择配置源，避免下拉框里查找。
- 中间主工作区负责 Schema、字段、enumId、引用规则、行视图和控件映射。
- 右侧检查器负责问题列表、源预览、当前源校验和 AI 修复上下文。
- 底部问题抽屉负责全局问题筛选和问题列表，避免打断主工作区。
- 主工作区使用页签：概览、字段、行视图、引用。
- 小窗口布局：主体区域允许换行，长内容使用滚动区域。
- 当前阶段只读，不保存配置源，不编辑真实资产。

## Table Editor v1 Preview

当前先提供只读行视图和字段控件映射：

- 行视图最多展示当前源的前几行。
- 每个单元格显示字段名、值和未来控件类型。
- 控件映射由 `ConfigField` 推导：
  - 数值字段：整数/浮点输入。
  - 布尔字段：开关。
  - `enumId` 字段：枚举/Flags 控件。
  - `ConfigReferenceRule` 字段：引用选择器。
  - `LocalizedText` 字段：多语言 Key。
  - `AssetPath` 字段：资源路径。
- 当前不保存、不写回真实配置源。

## Table Editor v1 Readonly Controls

行视图现在使用 UI Toolkit 禁用态控件展示未来编辑形态：

- 布尔字段显示为开关。
- 枚举和 flags 字段显示为下拉控件外观。
- 引用字段显示为引用选择按钮外观。
- 资源字段显示为资源选择按钮外观。
- 其他字段显示为文本输入外观。

这些控件当前全部 `SetEnabled(false)`，只用于确认布局和交互形态。

## Enum / Flags Candidate Preview

配置源可以实现 `IConfigEditorEnumProvider` 或通过 `ExternalConfigEditorSource` 传入 `ConfigEnumRegistry`。配置工作台会：

- 根据字段 `enumId` 查找枚举域。
- 将当前整数值格式化为 `中文名 英文名(数值)`。
- flags 字段会尝试拆分组合值。
- 下拉控件会显示该枚举域的候选项。

内置 Demo `ActionCatalog` 和 `ActionGraph` 用于展示 Table 到 Graph 引用、`demo.ActionCategory` enum、`demo.ActionTags` flags 以及 SourceIndex 关系，不代表任何真实游戏数据。详见 `Docs/Demo/CONFIG_DEMO.md`。

## Field Display Name

字段真实身份始终使用英文 `ConfigField.Name`，UI 显示层优先使用 `ConfigField.DisplayName`：

- 字段列表显示为 `中文别名 + 原字段名`。
- 行视图控件标签显示为 `中文别名 + 原字段名`。
- 问题报告和 AI 上下文保留 `field=` 原字段名，并补充 `fieldDisplay=` 中文别名。
- 没有 `DisplayName` 时回退到原字段名。

项目侧 Adapter 不应把中文别名作为数据 key；中文只服务编辑器显示、人工审核和 AI 理解。

## Field Inspector

配置工作台的字段页提供字段级检查器：

- 点击字段行的 `详情` 后，右侧检查器显示中文名、原字段名、类型、必填、控件、enumId、引用目标和说明。
- enum / flags 字段会列出当前枚举域候选项。
- ConfigReference 字段会显示 `查看目标源`，可跳转到目标配置源并选中目标 key 字段。
- `AI 上下文` 会附带当前选中字段摘要，便于后续 AI 只围绕一个字段分析问题。

字段详情仍然只读，不修改配置资产。

### Editor 长期规范

- Unity Editor 不承担外部主创编辑器职责；完整 Buff 创作、Mod 打包和实时预览入口应在外部桌面编辑器中实现。
- Unity Editor Bridge 负责导出 Schema、Enum、资源索引、多语言索引、引用索引和提交前报告。
- Unity Editor Bridge 的新增工具入口应优先注册到 `FrameworkManagerToolRegistry`，而不是直接修改 Framework Manager 主窗口文件。
- Character Resource Package 由 `MxFramework > Character > Import Character Package...` 导入；该入口只调用 Authoring CLI / Importer Bridge、刷新 AssetDatabase、输出报告，不承担 3D 装配主创 UI。
- Unity Editor 可以提供运行时连接状态和开发者诊断，但不再提供内置创作流程页。
- 所有正式面板使用 UI Toolkit，IMGUI 只用于临时调试工具。
- 每个模块面板都必须同时考虑 `编辑模式` 和 `运行模式`。
- 编辑模式优先支持，不依赖 Play Mode，不依赖场景对象。
- 运行模式默认只读，通过 Debug Source 快照显示运行时状态。
- 可写操作必须后置，例如 Add Buff、Reset Counter、Step AI Plan，不在第一版默认开放。
- Authoring 数据模型使用 `ConfigSchema / ConfigTable / ConfigAuthoringReport`。
- Runtime Debug 数据模型使用 `IFrameworkDebugSource / FrameworkDebugSnapshot`。
- 不允许把运行时对象反向写回配置表。
- 每个验证或沙盒结果都应支持复制或导出报告，便于 SVN 提交前留证和 AI 继续分析。

### 面板布局

```
┌────────────────────────────────────────────────────────┐
│  MxFramework Manager                             [x]   │
├────────────────────────────────────────────────────────┤
│  [Dependencies]  [Modules]  [Validation]  [Settings]   │  ← 顶部 Tab
├────────────────────────────────────────────────────────┤
│                                                        │
│  ┌──────────────────────┐  ┌────────────────────────┐ │
│  │  Dependency Graph     │  │  Module List            │ │
│  │                      │  │                        │ │
│  │    ┌─────┐           │  │  Core           ✓ 1.0  │ │
│  │    │Core │           │  │  Events         ✓ 0.1  │ │  ← 已实现/版本
│  │    └──┬──┘           │  │  Attributes     -      │ │
│  │       │              │  │  Modifiers      -      │ │  ← 未实现
│  │    ┌──┴──┐           │  │  Buffs          -      │ │
│  │    │Events│           │  │  AI             -      │ │
│  │    └──┬──┘           │  │  Config         -      │ │
│  │    ┌──┴──────────┐   │  │                        │ │
│  │    │  Attributes  │   │  │  [Open Editor...]     │ │
│  │    └──┬────┬──────┘   │  │  [Show Interfaces]    │ │
│  │       │    │          │  │  [Validate Module]    │ │
│  │   ┌───┴──┐ │          │  └────────────────────────┘ │
│  │   │Modif-│ │          │                              │
│  │   │ iers │ │          │  ┌────────────────────────┐ │
│  │   └──┬───┘ │          │  │  Quick Stats            │ │
│  │      │  ┌──┴──┐      │  │  Interfaces: 12         │ │
│  │      │  │Buffs│      │  │  Classes:    8          │ │
│  │      └──┼─────┘      │  │  Lines:      2,450     │ │
│  │         │            │  │  asmdefs:     7         │ │
│  │      ┌──┴──┐         │  │  Ext deps:    0 ✓      │ │
│  │      │ AI  │         │  └────────────────────────┘ │
│  │      └─────┘         │                              │
│  └──────────────────────┘                              │
│                                                        │
│  [Refresh All] [Validate Topology] [Export Report]     │
└────────────────────────────────────────────────────────┘
```

### Validation Tab

```
┌────────────────────────────────────────────────────────┐
│  Validation Results                                     │
├────────────────────────────────────────────────────────┤
│  ✓ 依赖图无环 (DAG)                                    │
│  ✓ 无跨层实现引用                                       │
│  ✓ 所有公共 API 有接口定义                               │
│  ✓ Runtime 程序集未引用 UnityEditor                      │
│  ⚠ Core 仍存在 UnityEngine 引用，待拆到 Core.Unity         │
│  ⚠ Modifiers 引用 Buffs 未通过接口                       │
│  ⚠ AI 模块接口数量不足 (2/5)                             │
│                                                        │
│  [Run Full Check]  [Auto-Fix Issues]                   │
└────────────────────────────────────────────────────────┘
```

### Settings Tab

```
┌────────────────────────────────────────────────────────┐
│  Framework Settings                                     │
├────────────────────────────────────────────────────────┤
│  Impact Review:      git diff / rg / tests             │
│  Auto-index on save:  n/a                              │
│                                                        │
│  Coupling Rules:                                        │
│  [✓] No circular dependencies                          │
│  [✓] Interface-only cross-module refs                   │
│  [ ] No UnityEngine in Core                            │
│                                                        │
│  SVN Integration:                                       │
│  [✓] Pre-commit coupling check                         │
│  Repo: svn://dxp2800-c03f/WGame/MxFramework/trunk      │
└────────────────────────────────────────────────────────┘
```

---

## 3. 模块编辑器基类 (MxEditorBase)

```csharp
// 位置: Assets/Scripts/MxFramework/Editor/MxEditorBase.cs

namespace MxFramework.Editor
{
    public abstract class MxEditorBase : EditorWindow
    {
        // 必需实现的元数据
        protected abstract string ModuleName { get; }
        protected abstract string[] ModuleDependencies { get; }  // 依赖的 asmdef 名称
        protected abstract Type[] ExposedInterfaces { get; }      // 模块暴露的接口类型

        // 通用布局
        protected virtual void OnGUI()
        {
            DrawHeader();       // 模块名称 + 版本
            DrawTabs();         // Interfaces | Implementations | Test
            DrawFooter();       // 依赖状态 + 耦合警告
        }

        // 子类重写
        protected abstract void DrawInterfacesTab();
        protected abstract void DrawImplementationsTab();
        protected abstract void DrawTestTab();
    }
}
```

### 布局约定

所有模块编辑器遵循三栏布局：

```
┌──────────────────────────────────────────┐
│  Module: Attributes               v1.0   │
├──────────────────────────────────────────┤
│  [Interfaces] [Implementations] [Test]   │
├──────────────────────────────────────────┤
│                                          │
│  (根据 Tab 显示不同内容)                    │
│                                          │
├──────────────────────────────────────────┤
│  Deps: Events ✓ | Core ✓  | Coupling: OK │
└──────────────────────────────────────────┘
```

---

## 4. 各模块编辑器规范

### 4.1 AttributesEditor

```
[Interfaces Tab]
┌──────────────────────────────────────────┐
│  IAttributeOwner                         │
│  ├─ int GetAttribute(int attrId)         │
│  ├─ void SetAttribute(int attrId, int v) │
│  ├─ void AddAttribute(int attrId, int d) │
│  └─ IEventBus Events { get; }           │
│                                          │
│  IAttributeModifier                      │
│  ├─ int Priority { get; }               │
│  └─ int Modify(int baseValue)           │
│                                          │
│  AttributeStore : IAttributeOwner        │
│  ├─ Fields: _values, _modifiers          │
│  ├─ Methods: RegisterAttribute(int)      │
│  └─ Events: AttrChangedEvent             │
└──────────────────────────────────────────┘

[Test Tab]
┌──────────────────────────────────────────┐
│  Runtime Attribute Debugger              │
│                                          │
│  Selected Target: [Player ▼]             │
│                                          │
│  ┌──────────┬────────┬────────┬───────┐ │
│  │ Attr ID  │  Name  │  Base  │ Final │ │
│  ├──────────┼────────┼────────┼───────┤ │
│  │ 0        │ HP     │ 1000   │ 1200  │ │
│  │ 1        │ ATK    │ 50     │ 65    │ │
│  │ 2        │ DEF    │ 30     │ 30    │ │
│  └──────────┴────────┴────────┴───────┘ │
│                                          │
│  Registered Attributes: 15               │
│  Active Modifiers: 3                     │
│                                          │
│  [Add Custom Attribute] [Clear All]      │
└──────────────────────────────────────────┘
```

### 4.2 ModifiersEditor

```
[Interfaces Tab] — 同 AttributesEditor 模式

[Implementations Tab]
┌──────────────────────────────────────────┐
│  ModifierPipeline                        │
│  ├─ AddModifier(IModifier)              │
│  ├─ RemoveModifier(int id)              │
│  ├─ UpdateAll(float dt)                 │
│  └─ Active Modifiers: 5                 │
│                                          │
│  CounterSystem                           │
│  ├─ RegisterCounter(int id, string name) │
│  ├─ Increment(int id, int delta)        │
│  └─ Counters: 12 registered              │
└──────────────────────────────────────────┘

[Test Tab]
┌──────────────────────────────────────────┐
│  Modifier Sandbox                        │
│                                          │
│  Target: [Sandbox ▼]                     │
│                                          │
│  Active Modifiers:                       │
│  ┌──────┬────────────┬──────┬────────┐ │
│  │  ID  │  Condition  │  Effect  │ Active│ │
│  ├──────┼────────────┼──────┼────────┤ │
│  │  1   │ HP < 30%   │ DEF+30  │  ✓    │ │
│  │  2   │ Kill×10    │ ATK+20  │  -    │ │
│  └──────┴────────────┴──────┴────────┘ │
│                                          │
│  [Add Test Modifier]  [Trigger Condition]│
│  [Reset All Counters]                    │
└──────────────────────────────────────────┘
```

### 4.3 BuffsEditor

```
[Test Tab]
┌──────────────────────────────────────────┐
│  Buff Timeline                           │
│                                          │
│  Target: [Sandbox ▼]                     │
│                                          │
│  ┌────────────────────────────────────┐ │
│  │  Buff A ████████░░░░  2.3s/5.0s    │ │
│  │  Buff B ██████████████ 4.8s/5.0s   │ │
│  │  Buff C ██░░░░░░░░░░  0.8s/3.0s    │ │
│  └────────────────────────────────────┘ │
│                                          │
│  Active Buffs: 3                         │
│  Layer Stacking:                         │
│  ┌──────────┬───────┬────────┐          │
│  │  Buff    │ Layers│ Effect │          │
│  ├──────────┼───────┼────────┤          │
│  │ Swift... │   5   │ Spd+25%│          │
│  │ Burn     │   3   │ -10/s  │          │
│  └──────────┴───────┴────────┘          │
│                                          │
│  [Add Buff] [Remove Buff] [Tick +1s]     │
└──────────────────────────────────────────┘
```

### 4.4 Runtime AI Planner Editor（AIEditor）

> **本文的 AI 指 Runtime AI Planner**。不包含 AIAction Config、Authoring AI Assist 或 Development Agent。

```
[Interfaces Tab]
┌──────────────────────────────────────────┐
│  IAiGoal                                │
│  ├─ float Priority { get; }             │
│  ├─ bool IsRelevant(IAiWorldState)      │
│  └─ bool IsSatisfied(IAiWorldState)     │
│                                          │
│  IAiAction                              │
│  ├─ bool CanExecute(IAiWorldState)      │
│  ├─ void Apply(IAiWorldState)           │
│  └─ float Cost { get; }                 │
│                                          │
│  IAiSensor                              │
│  └─ void Sense(IAiAgent, IAiWorldState) │
│                                          │
│  IAiPlanner                             │
│  └─ bool TryPlan(..., out AiPlan)       │
└──────────────────────────────────────────┘

[Test Tab]
┌──────────────────────────────────────────┐
│  AI Sandbox                              │
│                                          │
│  ┌────────────────────────────────────┐ │
│  │  Goal-Action Graph                  │ │
│  │                                     │ │
│  │  [Fight]────→[MoveTo]──→[Attack]   │ │
│  │    │                     │          │ │
│  │    └──→[Heal]──→[UseItem]│          │ │
│  │                                     │ │
│  └────────────────────────────────────┘ │
│                                          │
│  World State:                            │
│  ┌──────────────┬───────────────┐       │
│  │  Key         │  Value        │       │
│  ├──────────────┼───────────────┤       │
│  │  HP%         │  75           │       │
│  │  EnemyDist   │  3.2          │       │
│  │  HasTarget   │  true         │       │
│  └──────────────┴───────────────┘       │
│                                          │
│  [Run Planner] [Step] [Reset]            │
└──────────────────────────────────────────┘
```

### 4.5 ConfigEditor

当前 Phase 8.4 已在 Framework Manager 的 `编辑模式 > 配置表` 页提供配置源预览和健康检测版。

已支持：
- 选择内置 Demo 配置源：`内置 Demo / BasicBuffConfig`、`内置 Demo / BasicModifierConfig`、`内置 Demo / ActionCatalog`、`内置 Demo / ActionGraph`。
- 查看来源类型、表名、行数、字段名、字段类型、必填状态、引用目标、说明、ID 范围和必需语言。
- 预览并复制 `ConfigAuthoring.CreateTemplate(schema)` 生成的 TSV 模板。
- 预览当前配置源最多 5 行 TSV 数据。
- 校验当前配置源，覆盖 Schema、跨表引用和多语言规则。
- 通过 `复制报告` 输出当前配置源报告。
- 通过 `MxEditorUtils.RegisterConfigEditorSource(source)` 注册项目层配置源。
- 通过 `刷新配置源` 重新读取已注册配置源。
- 自动统计所有配置源健康状态：源数量、总行数、问题源、Error、Warning、缺失引用、多语言缺失、ID 问题和 Schema 问题。
- 通过 `复制健康报告` 输出总览统计、表级统计、错误类型统计和问题样例。
- 显示问题明细，支持 `全部 / Error / Warning` 基础筛选。
- 通过 `复制问题列表` 输出当前筛选结果。
- 通过 `复制 AI 修复上下文` 输出当前源信息、健康报告、Schema、TSV 预览和当前源问题列表。
- 自动比较上一次健康检测基线，显示配置源、行数、Error、Warning 和错误类型统计是否变化。
- 通过 `复制变动报告` 输出配置变动统计。
- 通过 `重置变动基线` 将当前健康状态设为新的比较基线。
- 通过 `导出配置报告` 输出报告包到 `Temp/MxFrameworkReports/Config/`。
- 通过 `提交前检查` 刷新配置源、重新检测、导出报告包并生成 `config_precommit.txt`。

当前边界：
- 不编辑真实配置资产。
- 不绑定 Excel、CSV、Json、Luban 或 ScriptableObject。
- 不把运行模式对象写回配置表。
- 项目层导入器只要实现 `IConfigEditorSource`，即可接入同一套显示、校验和报告入口。

配置源接口：

```csharp
public interface IConfigEditorSource
{
    string Name { get; }
    string SourceType { get; }
    ConfigSchema Schema { get; }
    int RowCount { get; }
    ConfigAuthoringTemplate CreateTemplate();
    ConfigAuthoringReport Validate();
    string CreateTsvPreview(int maxRows);
    string CreateReport();
}
```

项目层注册入口：

```csharp
MxEditorUtils.RegisterConfigEditorSource(new GameConfigEditorSource());
```

健康检测入口：

```csharp
IReadOnlyList<IConfigEditorSource> sources = MxEditorUtils.GetConfigEditorSources();
ConfigHealthReport health = MxEditorUtils.AnalyzeConfigHealth(sources);
string report = MxEditorUtils.CreateConfigHealthReport(health);
```

AI 修复上下文：

```csharp
IReadOnlyList<ConfigIssueView> issues = MxEditorUtils.CollectConfigIssues(sources);
string context = MxEditorUtils.CreateConfigAiFixContext(source, health, issues, previewRows: 5);
```

配置变动检测：

```csharp
string baseline = MxEditorUtils.LoadConfigHealthBaseline();
ConfigChangeReport changes = MxEditorUtils.DetectConfigChanges(health, baseline);
string report = MxEditorUtils.CreateConfigChangeReportText(changes);
MxEditorUtils.SaveConfigHealthBaseline(health);
```

配置报告导出：

```csharp
ConfigReportExportResult result =
    MxEditorUtils.ExportConfigReportBundle(source, health, issues, changes, previewRows: 5);
```

提交前检查：

```csharp
string precommit = MxEditorUtils.CreateConfigPrecommitReportText(health, changes, issues);
```

报告包固定输出到 `Temp/MxFrameworkReports/Config/`，包括 `config_health.txt`、`config_issues.txt`、`config_changes.txt`、`config_ai_context.txt`、`config_precommit.txt` 和 `config_report_index.txt`。AI Agent 应优先读取报告包，而不是依赖剪贴板。

```
┌──────────────────────────────────────────┐
│  Config Registry                         │
│                                          │
│  Registered Config Types:                │
│  ┌──────────┬────────┬──────┬─────────┐ │
│  │  Type    │  Count │ Range│ Source  │ │
│  ├──────────┼────────┼──────┼─────────┤ │
│  │  Buff    │  45    │1-999 │ JSON    │ │
│  │  Skill   │  30    │1-999 │ JSON    │ │
│  │  Item    │  120   │1-9999│ JSON    │ │
│  └──────────┴────────┴──────┴─────────┘ │
│                                          │
│  Config Provider: [JSON ▼]              │
│  Data Path: [Assets/Config/         ]   │
│                                          │
│  [Register Type] [Template] [Validate]   │
│  [Hot Reload] [Export Report]            │
└──────────────────────────────────────────┘
```

ConfigEditor v1 应复用运行时 authoring 入口：
- `ConfigAuthoring.CreateTemplate(schema)` 生成表头、样例行和字段说明。
- `ConfigAuthoring.ValidateTable(table, resolver, localization)` 生成结构化错误。
- `ConfigAuthoring.ExportReportText(report)` 生成可复制的提交前留证文本。
- 具体 Excel/CSV/Json/Luban 导入器放在项目或 Editor 适配层，不进入 Config 运行时核心。

---

## 5. 通用编辑器工具 (MxEditorUtils)

```csharp
public static class MxEditorUtils
{
    // 反射获取模块所有接口
    public static Type[] GetModuleInterfaces(string asmdefName);

    // 检查依赖图是否无环
    public static bool ValidateDependencyGraph(out string[] cycles);

    // 检查跨层引用（上层是否引用了下层实现类）
    public static string[] FindDirectClassReferences(string fromAsmdef, string toAsmdef);

    // 生成模块依赖 JSON（供外部图谱工具读取）
    public static string ExportDependencyJson();

    // 导出质量门禁报告（文本或 JSON）
    public static string ExportQualityReport();
}
```

## 7. 验证项映射

| Editor 检查项 | 对应规范 |
|---------------|----------|
| asmdef DAG | `ARCHITECTURE.md` 2.1 |
| Core UnityEngine 引用 | `ARCHITECTURE.md` 2.2 |
| Runtime UnityEditor 引用 | `API_STANDARDS.md` 6 |
| 公共 API 是否入档 | `API_STANDARDS.md` 1 |
| 迁移来源注释 | `QUALITY_GATE.md` 1 |
| WGame/Entitas/Luban 禁用引用 | `QUALITY_GATE.md` 1 |

---

## 6. 依赖图关系

Unity Editor 当前不维护独立代码知识图谱接入。后续如果提供 GraphView / Health Check，应作为编辑器可视化能力单独设计，不绑定外部索引工具。
