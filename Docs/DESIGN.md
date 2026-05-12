# MxFramework 总体设计规范

> 版本 0.3.0 | 2026-05-05
> 
> 目标：从 WGame 提取通用的、低耦合的 Unity 游戏框架。
> 当前阶段：**需求规范 + 总设计**，为后续开发铺路。

---

## 1. 愿景

一套**数据驱动、可组合**的 Unity 游戏数值和行为框架。
不绑定任何具体游戏类型（ARPG、Roguelike、卡牌均可使用）。

### 1.1 文档基线

本文件只描述总体设计。执行层面的约束拆分到以下文档：

| 文档 | 约束范围 |
|------|----------|
| `Docs/ARCHITECTURE.md` | 架构契约、依赖方向、生命周期、错误处理 |
| `Docs/API_STANDARDS.md` | API 分级、命名、事件、GC、兼容性 |
| `Docs/QUALITY_GATE.md` | 每批迁移和每个模块的完成定义 |
| `Docs/ROADMAP.md` | 分阶段路线图和阶段产物 |

当本文与上述文档冲突时，以更具体的文档为准。

## 2. 约束

### 2.1 必须
- ✅ 零游戏逻辑依赖。框架不包含任何 WGame 特化内容
- ✅ 接口驱动。所有外部依赖通过接口注入
- ✅ 自描述。每个模块提供 Editor 可视化面板
- ✅ 纯 C# 优先。Core 层零 Unity 依赖
- ✅ SVN 版本管理（svn://dxp2800-c03f/WGame/MxFramework/trunk）

### 2.2 禁止
- ❌ 不引入 Entitas / Luban / CrashKonijn 等特定插件依赖
- ❌ 不引入 WGame 游戏特化数据（元素系统、具体 Buff 实例、关卡逻辑）
- ❌ 模块间不直接引用实现类，只通过接口通信

### 2.3 插件最小化原则
框架只依赖 Unity 内置包：
```
必需品: UnityEditor (编辑器工具), UnityEngine (运行时)
可选:   Unity.Mathematics (高性能数学)
```

不使用 UPM 外部包。GitNexus 作为外部辅助工具，不嵌入框架运行时代码；接入和工作流规则统一见 `GITNEXUS.md`。

---

## 3. 模块架构

```
MxFramework/
├── Core/              纯 C# 工具层（长期目标：零 Unity 依赖）
│   ├── Collections/   Heap, UnsortList, ObjectPool
│   ├── Extensions/    ZString (0GC), 泛型扩展
│   └── Math/          RandomTable, VectorExt, BitUtils
│
├── Core.Unity/        Unity 类型适配层（Vector/Mathf 等）
│
├── Events/            事件系统
│   └── EventBus<T>    类型安全的事件发布/订阅
│
├── Attributes/        属性系统
│   ├── IAttributeOwner     —— 属性持有者接口
│   ├── AttributeStore      —— 属性存储（Add/Get/Set + 变更通知）
│   ├── IAttributeModifier  —— 属性修改器接口
│   └── AttributeEvent      —— 属性变更事件
│
├── Modifiers/         修改器系统（WGame Entry 的泛化）
│   ├── IModifier           —— 修改器接口
│   ├── IModifierCondition  —— 触发条件接口
│   ├── IModifierEffect     —— 效果接口
│   ├── ModifierPipeline    —— 修改器生命周期管理
│   └── CounterSystem       —— 计数器系统
│
├── Buffs/             Buff 系统
│   ├── IBuff               —— Buff 接口
│   ├── BuffPipeline        —— Buff 生命周期管理
│   ├── BuffBase            —— 通用 Buff 基类
│   └── IBuffStackingPolicy —— 堆叠策略接口
│
├── AI/                AI 抽象层
│   ├── IAiGoal             —— 目标接口
│   ├── IAiAction           —— 行为接口
│   ├── IAiSensor           —— 感知器接口
│   └── IAiPlanner          —— 规划器接口
│
├── Diagnostics/       运行时调试快照协议
│   └── IFrameworkDebugSource
│
├── Config/            配置系统抽象
│   └── IConfigProvider     —— 配置提供者接口
│
└── Editor/            编辑器工具（Unity Editor only）
    ├── FrameworkManager    —— 框架总管理器窗口
    ├── ModuleEditors/      —— 各模块可视化编辑器
    │   ├── AttributesEditor
    │   ├── ModifiersEditor
    │   ├── BuffsEditor
    │   ├── AIEditor
    │   └── ConfigEditor
    └── MxEditorUtils       —— 编辑器通用工具
```

### 3.1 依赖方向（自底向上）

```
Core        ← 零 Unity 依赖（长期目标）
Core.Unity  ← Core + UnityEngine
Events      ← Core
Attributes  ← Core + Events
Modifiers   ← Core + Events + Attributes
Buffs       ← Core + Events + Attributes
AI          ← Core
Config      ← Core
Diagnostics ← Core
Editor      ← 所有模块（仅 Editor 程序集）
```

### 3.2 命名空间

| 命名空间 | 程序集 (.asmdef) |
|----------|-----------------|
| `MxFramework.Core` | MxFramework.Core |
| `MxFramework.Core.Unity` | MxFramework.Core.Unity |
| `MxFramework.Events` | MxFramework.Events |
| `MxFramework.Attributes` | MxFramework.Attributes |
| `MxFramework.Modifiers` | MxFramework.Modifiers |
| `MxFramework.Buffs` | MxFramework.Buffs |
| `MxFramework.AI` | MxFramework.AI |
| `MxFramework.Config` | MxFramework.Config |
| `MxFramework.Diagnostics` | MxFramework.Diagnostics |
| `MxFramework.Editor` | MxFramework.Editor |

---

## 4. 模块详细规范

### 4.1 Core 层

**职责**: 零依赖的纯 C# 工具。

> 当前状态：`MxFramework.Core` 已设置 `noEngineReferences=true`；Unity 类型相关工具已拆到 `MxFramework.Core.Unity`。

| 类/文件 | 功能 | 来源 |
|---------|------|------|
| `Heap<T>` | 泛型二叉堆 | WGame MyHeap |
| `UnsortList<T>` | 无序列表（延迟删除） | WGame WUnsortList |
| `RandomTable` | 预生成随机表（Core.Unity） | WGame WRandom |
| `VectorExtensions` | Vector 角度/方向工具（迁移到 Core.Unity） | WGame Vector3Extension |
| `BitUtils` | 整型位打包工具 | WGame IntExtension (generic parts) |
| `ZString` | 零 GC 字符串构建器 | WGame ZString |

**接口暴露**: 无——Core 层全为静态工具/具体类。

### 4.2 Events 层

**职责**: 类型安全的事件发布/订阅。

```
核心接口:
  IEventBus<T> where T : struct
    IDisposable Subscribe(Action<T> handler)
    bool Unsubscribe(Action<T> handler)
    void Publish(in T args)
```

**设计要点**:
- 泛型 + 结构体约束，避免事件 payload 装箱。
- 订阅句柄支持 `Dispose`，便于生命周期内自动退订。
- 默认同步发布，按订阅顺序执行。
- 发布期间新增订阅从下一次发布开始生效。
- 发布期间取消订阅会跳过尚未执行的同一订阅。
- handler 抛异常时立即向调用方传播，并停止本次发布。
- `Subscribe` 允许低频分配；`Publish` 在订阅表稳定后不分配。

### 4.3 Attributes 层

**职责**: 角色/实体的属性存储与计算。

```
核心接口:
  IAttributeOwner
    int GetAttribute(int attrId)
    void SetAttribute(int attrId, int value)
    void AddAttribute(int attrId, int delta)
    
  IAttributeModifier
    int Priority { get; }          // 修改器优先级
    int Modify(int baseValue)      // 修改函数
    
  AttributeStore : IAttributeOwner
    // 内部维护 Dictionary<int, AttributeValue>
    // 通过 EventBus 发布变更事件
    // 支持修改器链（优先级排序）
```

**与 WGame 的映射**:
| WGame | MxFramework |
|-------|-------------|
| `WAttribute.AttrCore` | `AttributeStore` |
| `AttrBridge` | `IEventBus<AttrChangedEvent>` |
| `AttrType` | `AttributeValue` |
| `WAttrType` 枚举 | 运行时注册的 `int attrId` |
| `IaWAttrStrategy` | `IAttributeModifier` |

**关键解耦**:
- `WAttrType` 枚举 → `int attrId` 运行时注册
- `GameEntity` 依赖 → `IAttributeOwner` 接口
- 伤害公式/元素逻辑 → 保留在 WGame，不迁移

### 4.4 Modifiers 层

**职责**: 装备词条、技能效果、触发条件的通用修改器系统。

```
核心接口:
  IModifier
    int Id { get; }
    void Apply(ModifierContext ctx)
    void Update(float deltaTime, ModifierContext ctx)
    void Remove(ModifierContext ctx)
    
  IModifierCondition
    bool Evaluate(ModifierContext ctx)     // 条件是否满足
    
  IModifierEffect
    void Execute(ModifierContext ctx)      // 效果执行
    
  ModifierContext
    IAttributeOwner Target { get; }        // 目标属性系统
    IBuffPipeline Buffs { get; }           // 可选的 Buff 系统
    int[] Parameters { get; }              // 运行参数
    int CompareValue { get; }              // 比较值
```

**与 WGame 的映射**:
| WGame | MxFramework |
|-------|-------------|
| `IEntry` | `IModifier` |
| `EntryApplyData` | `ModifierContext` |
| `EntryManager` | `ModifierPipeline` |
| `IEntryCondition` | `IModifierCondition` |
| `IEntryEffect` | `IModifierEffect` |
| `CounterDefine` | `CounterConfig`（运行时注册） |
| `EntryFactory` | 配置驱动，不硬编码 |

**关键解耦**:
- `GameEntity Entity` → `IAttributeOwner Target`
- `WAttrType` 枚举 → `int attrId`
- `BuffIDs` → `int buffId`（IFactory 模式创建）
- 游戏词条（ElementType.Pyro 等）→ 配置数据
- `EntryCondCounter` 的运行时计数 → `ICounterStore`
- 计数器 ID 文案、元素、伤害、技能效果由游戏层或配置层提供

### 4.5 Buffs 层

**职责**: Buff/Debuff 的生命周期管理。

```
核心接口:
  IBuff
    int Id { get; }
    float Duration { get; }
    float RemainingTime { get; }
    int MaxLayers { get; }
    int CurrentLayers { get; }
    bool IsPermanent { get; }
    bool IsExpired { get; }
    void OnAttach(IBuffTarget target)
    void OnTick(float deltaTime, IBuffTarget target)
    void OnDetach(IBuffTarget target)
    
  IBuffTarget
    IAttributeOwner Attributes { get; }
    IAttributeModifierOwner AttributeModifiers { get; }
    IEventBus<BuffEvent> BuffEvents { get; }
    
  IBuffPipeline
    IBuff AddBuff(IBuff buff, IBuffTarget target)
    bool TryAddBuff(int buffId, IBuffTarget target, out IBuff buff)
    bool RemoveBuff(int buffId)
    void TickAll(float deltaTime)
```

**与 WGame 的映射**:
| WGame | MxFramework |
|-------|-------------|
| `BuffManager` | `BuffPipeline` |
| `BuffData` / `BuffStatus` | `IBuff` / `BuffBase` |
| `NBuffData` | 游戏层数值 Buff 实现 |
| `StatusBuffData` | 游戏层状态 Buff 实现 |

**关键解耦**:
- `BuffIDs` 枚举 → `int buffId`（`IBuffFactory` 创建）
- `BuffOwner` / `GameEntity` → `IBuffTarget`
- 属性修改器添加/移除 → `IAttributeModifierOwner`
- 具体状态类型、技能效果、表现特效由游戏层实现，不进入框架层

### 4.6 AI 层

**职责**: 轻量 AI 基础设施。提供事实、目标、动作、效果和规划器，不依赖第三方 AI 插件。

```
核心接口:
  IAiWorldState
    bool TryGetValue<T>(AiFactKey key, out T value)
    void SetValue<T>(AiFactKey key, T value)
    IAiWorldState Clone()
    
  IAiGoal
    int Id { get; }
    float Priority { get; }
    bool IsRelevant(IAiWorldState worldState)
    bool IsSatisfied(IAiWorldState worldState)
    
  IAiAction
    int Id { get; }
    float Cost { get; }
    IReadOnlyList<IAiCondition> Preconditions { get; }
    IReadOnlyList<IAiEffect> Effects { get; }
    bool CanExecute(IAiWorldState worldState)
    void Apply(IAiWorldState worldState)
    
  IAiSensor
    void Sense(IAiAgent agent, IAiWorldState worldState)
    
  IAiPlanner
    bool TryPlan(IAiWorldState worldState, IEnumerable<IAiGoal> goals,
                 IEnumerable<IAiAction> actions, out AiPlan plan)
```

**与 WGame 的映射**:
| WGame | MxFramework |
|-------|-------------|
| `WGOAPMgr` | `SequentialPlanner` / 游戏层自定义 Planner |
| `GoalAgent` | `IAiGoal` |
| GOAP Actions | `IAiAction` |
| GOAP Sensors | `IAiSensor` |

**设计说明**:
- 框架提供内置轻量 Planner，不要求引入 CrashKonijn、行为树或其他插件。
- 游戏层可以参考 WGame 的 GOAP 组织方式实现自己的目标、动作、传感器和状态适配器。
- 框架层不包含怪物、技能、导航、仇恨、目标选择等游戏语义。

### 4.7 Config 层

**职责**: 配置数据访问抽象。

```
核心接口:
  IConfigProvider
    T GetConfig<T>(int id) where T : IConfigData
    bool TryGetConfig<T>(int id, out T config)
    IReadOnlyCollection<T> GetAllConfigs<T>()
    
  IConfigData
    int Id { get; }

  IConfigRegistry
    void RegisterProvider<T>(IConfigProvider provider)

  MemoryConfigProvider
    Register<T>(T config)
    RegisterRange<T>(IEnumerable<T> configs)

  ConfigValidator
    ValidateReferences<T>(IConfigProvider provider)

  ConfigTable<T>
    ConfigSchema Schema { get; }
    IReadOnlyCollection<T> Rows { get; }
    ConfigTableValidationReport Validate(...)

  ConfigSchema
    TableName
    Fields
    IdRange
    RequiredLocales

  ConfigField
    Name
    ConfigFieldType
    ConfigReferenceRule

  LocalizedTextKey / LocaleId
    多语言文本引用，不直接把多语言文本塞进业务表
```

配置驱动运行时对象：

```
ConfigTable<TConfig>
  -> IConfigProvider
  -> ConfigBuffFactory<TConfig> / ConfigModifierFactory<TConfig>
  -> BuffPipeline / ModifierPipeline
```

桥接层放在 `MxFramework.Config.Runtime`，避免 `MxFramework.Buffs` / `MxFramework.Modifiers` 反向依赖 Config。

**关键解耦**:
- Luban / Json / ScriptableObject / 静态配置入口 → `IConfigProvider`
- 多配置源组合 → `IConfigRegistry`
- 配置 ID 冲突策略 → `ConfigDuplicatePolicy`
- 配置引用检查 → `IConfigReferenceProvider`
- 表结构和字段元数据 → `ConfigSchema`
- 多语言文本表 → `ILocalizationProvider`
- AI 辅助上下文 → `ConfigSchemaExporter`
- 配置编辑辅助 → `ConfigAuthoring`

**推荐配置表形态**:

业务表只保存文本引用：

```
BuffConfig
- Id
- NameText: LocalizedTextKey
- DescText: LocalizedTextKey
- Duration
- MaxLayers
```

多语言表独立维护：

```
TextConfig
- Key
- Locale
- Value
- Note
```

---

## 5. 编辑器架构

### 5.1 框架总管理器 (Framework Manager)

Unity Editor Window `MxFramework > Framework Manager`：

```
┌──────────────────────────────────────┐
│  MxFramework Manager          [x]   │
├──────────────────────────────────────┤
│  Dependency Graph  │  Module Status   │
│  ┌──────────────┐  │  Core       ✓   │
│  │   Core       │  │  Events     ✓   │
│  │    ↓         │  │  Attributes ✓   │
│  │  Events     │  │  Modifiers  -   │
│  │    ↓         │  │  Buffs      -   │
│  │ Attributes  │  │  AI         -   │
│  │    ↓    ↓    │  │  Config     -   │
│  │ Modifiers  │  │                  │
│  │  Buffs      │  │                  │
│  │    ↓         │  │                  │
│  │   AI        │  │                  │
│  └──────────────┘  │                  │
│                    │                  │
│ [Refresh Graph]    [Open Editor...]  │
│ [Validate Coupling]                   │
└──────────────────────────────────────┘
```

**功能**:
- 实时依赖图（基于 asmdef + 代码分析）
- 模块状态一览（已实现/待迁移）
- 耦合度检查（跨层引用告警）
- 一键打开各模块编辑器

### 5.2 模块编辑器（通用模式）

每个模块编辑器遵循统一布局：

```
┌──────────────────────────────────────┐
│  Module: Attributes            [x]   │
├──────────────────────────────────────┤
│  [接口定义]  [实现类]  [测试面板]    │
│                                      │
│  ┌─ Attributes ─────────────────┐   │
│  │ IAttributeOwner              │   │
│  │  ├─ GetAttribute(int) → int  │   │
│  │  ├─ SetAttribute(int, int)   │   │
│  │  └─ AddAttribute(int, int)   │   │
│  │                              │   │
│  │ AttributeStore : IAttrib...  │   │
│  │  ├─ Fields:                  │   │
│  │  │  _values: Dict<int,...>   │   │
│  │  │  _modifiers: List<...>    │   │
│  │  └─ Events: AttrChanged      │   │
│  └──────────────────────────────┘   │
│                                      │
│  [测试] AttrID: [0    ] Value: [100]│
│  [Add +10]  [Set 50]   [Get] → 50  │
│                                      │
│  Dependency check: 3/3 interfaces clean
└──────────────────────────────────────┘
```

**各模块编辑器特有功能**:

| 编辑器 | 特有功能 |
|--------|---------|
| AttributesEditor | 属性注册面板、修改器链可视化、实时属性值调试 |
| ModifiersEditor | 修改器节点图、条件/效果预览、计数器测试 |
| BuffsEditor | Buff 时间轴、层级堆叠预览、Tick 频率配置 |
| AIEditor | Goal-Action 图、世界状态编辑器、规划调试 |
| ConfigEditor | 配置表结构定义、ID 范围管理 |

### 5.3 编辑器实现原则

- 所有编辑器继承 `MxEditorBase` 基类
- 使用 `UIElements`（非 IMGUI），保证可扩展性
- 编辑器代码在 `MxFramework.Editor` 程序集，不影响运行时
- 测试面板的数据修改不污染实际游戏状态

---

## 6. GitNexus

GitNexus 用于代码知识图谱、影响面分析和提交前辅助检查。当前接入方式、命令、Agent 规则和提交前要求统一维护在 `GITNEXUS.md`；本文只保留架构定位，避免多处文档漂移。

---

## 7. 工程规范

### 7.1 代码风格

- 接口前缀 `I`（`IModifier`, `IBuff`, `IAttributeOwner`）
- 具体类不加前缀，用名词（`AttributeStore`, `BuffPipeline`）
- 抽象类加 `Base` 后缀（`ModifierBase`, `BuffBase`）
- 字段 `_camelCase`，属性 `PascalCase`
- 禁止 `public` 字段，用属性或 `[SerializeField] private`
- 每个文件顶部注释源文件路径（若从 WGame 迁移）
- 公共 API 的分配行为遵守 `Docs/API_STANDARDS.md`
- 生命周期、错误处理和 ID 空间遵守 `Docs/ARCHITECTURE.md`

### 7.2 耦合度检查规则

| 违规 | 说明 |
|------|------|
| 上层引用下层实现类 | 只能通过接口引用 |
| 模块间直接 new | 通过工厂/DI 创建 |
| 循环引用 | asmdef 依赖图必须是无环 DAG |
| Editor 代码在运行时程序集 | Editor 独立 asmdef |

### 7.3 asmdef 设计

每个模块一个 asmdef，仅引用下层模块：

```
MxFramework.Core          → 无依赖
MxFramework.Core.Unity    → Core + UnityEngine
MxFramework.Events        → Core
MxFramework.Attributes    → Core, Events
MxFramework.Modifiers     → Core, Events, Attributes
MxFramework.Buffs         → Core, Events, Attributes
MxFramework.AI             → Core
MxFramework.Config         → Core
MxFramework.Diagnostics    → Core
MxFramework.Editor         → 所有模块 + UnityEditor
```

---

## 8. 迁移路线图

| 批次 | 内容 | 状态 | SVN 版本 |
|------|------|------|----------|
| 0 | 项目初始化 + SVN | ✅ 完成 | r1061 |
| 1 | Core 工具层迁移 | ✅ 完成 | - |
| 2 | Events 事件系统 | ✅ 完成 | - |
| 3 | Attributes 属性核心 | ✅ 完成 v1 | - |
| 4 | Buffs 系统 | ✅ 完成 v1 | - |
| 5 | Modifiers 修改器系统 | ✅ 完成 v1 | - |
| 6 | Config 配置抽象 | ✅ 完成 v1 | - |
| 6.1 | Config Table / Schema | ✅ 完成 v1 | - |
| 6.2 | Config-backed Factories | ✅ 完成 v1 | - |
| 6.3 | Config Authoring | ✅ 完成 v1 | - |
| 7 | AI 轻量 Planner | ✅ 完成 v1 | - |
| 8 | Editor 编辑器 | 📋 待开始 | - |
| 9 | GitNexus 工作流规范 | ✅ 已接入 | `GITNEXUS.md` |
| 10 | 框架自测 + 示例项目 | 📋 待开始 | - |

---

## 9. 文件清单

| 文档 | 路径 |
|------|------|
| 总体设计规范 | `Docs/DESIGN.md` |
| 文档索引 | `Docs/README.md` |
| 架构契约 | `Docs/ARCHITECTURE.md` |
| API 规范 | `Docs/API_STANDARDS.md` |
| 质量门禁 | `Docs/QUALITY_GATE.md` |
| 路线图 | `Docs/ROADMAP.md` |
| 迁移记录 | `Docs/MIGRATION.md` |
| 模块编辑器规范 | `Docs/EDITORS.md` |
| 接口索引 | `Docs/INTERFACES.md` |
| 模块接口 | `Docs/Interfaces/` |
