# Mx 关卡编辑器 P3.3：统一触发器工作区开发指南

> Status: Current
>
> Last reviewed: 2026-10-05（源码合同核对，未重跑原生验收）
>
> Scope: CharacterTest 区域来源、当前玩家 HP Threshold、现有正式 Trigger 模板、Editor-only 工作区

## 1. 当前实现草案

本指南维护当前工作区合同；只读投影与ID/保存规则见§4.1、§6.1，玩家生命阈值见§3.1。已完成阶段报告不再保留；紧凑条款与布尔条件的未完成验收由下表两份任务维护，不能把旧编译/验收数字解释为当前通过。

| 顺序 | 任务 | 状态 |
|---|---|---|
| 1 | [P3.3a 只读投影合同](LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md#41-只读投影与不变性) | Complete |
| 2 | [P3.3b 编辑与 ID 生命周期合同](LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md#61-所属-id-与保存基线) | Complete |
| 3 | [当前玩家生命阈值合同](#31-当前玩家生命阈值来源) | Complete |
| 4 | [P3.3d 紧凑条款 UI 收口](../Tasks/LEVEL_EDITOR_P3_3D_COMPACT_CLAUSE_UI_CLOSEOUT.md) | Partial |
| 5 | [P3.3e 布尔条件组合](../Tasks/LEVEL_EDITOR_P3_3E_BOOLEAN_CONDITION_AUTHORING.md) | Partial |

代码已经存在的项目只能说明“实现草案存在”。只有对应任务的自动验证、手动验收和完成记录全部填写后，才能标记该任务完成。

关卡编辑器顶部新增`玩法触发器`工作区。它与`世界对象`工作区共享同一个 `CharacterTestWorldObjectLevelEditorModule` 实例、同一份 level envelope、同一个文档 Undo Session 和同一个保存参与者。

当前代码提供：

- 搜索、按自动分类浏览和选择区域 Trigger；
- 新建`进入时施加 Buff`、`区域内维持 Buff`和`进入时触发 Story`；
- 编辑名称、启用状态、P3.2 目标筛选、可展平的`且 / 或 / 非`附加条件、Buff、效果归因、Story 引用、冷却和次数策略；
- 复制、删除、单步 Undo/Redo；
- 查看并编辑紧凑的`事件 / 条件 / 动作 / 效果`条款；
- 从 Trigger 定位 Scene 区域，从 Region Inspector 或 Scene 右键进入关联 Trigger；
- 按需展开现有 P2.4 运行时检查；
- 保存时同步更新运行时缓存。

当前区域能力和唯一的非区域来源`当前玩家生命值低于阈值`均已完成。该来源使用正式 Source Adapter、validator、compiler、complete scalar sampler 和 typed diagnostics。Signal、时间、交互、任务和任意属性来源仍不提供，也不提供任意 Route/Action 图编辑。

## 2. Shell 契约

公共 Editor 契约位于：

- `Packages/com.vincent.wgameframework/Editor/Levels/MxLevelEditorContracts.cs`
- `Packages/com.vincent.wgameframework/Editor/Levels/MxLevelEditorWindow.Workspaces.cs`

`IMxLevelEditorWorkspace`只声明导航和内容投影：

```text
WorkspaceId
DisplayName
OwnerModuleId
OnEnable()
CreateContent(window)
```

它不继承 `IMxLevelEditorModule`，没有 `Save()` 和独立脏状态。`MxLevelEditorWorkspaceRegistry`按稳定 workspace ID 注册低频 Editor 入口；重复 ID 会替换入口，不会创建第二个文档 owner。

现有 module 自动保留自己的默认 workspace。窗口下拉框将默认 workspace 与显式 workspace 展平显示，选中显式 workspace 时仍使用 owner module 提供的统一保存与缓存同步、校验、SceneView 和右键菜单能力。

这些 API 是 Editor-only：

- registry 查询与 workspace 构建：`AllocByDesign`；
- 显式切换 workspace 和 Scene context 投影：`LowFreqAlloc`；
- 当前 workspace ID：`NoAlloc`；状态标签刷新：`LowFreqAlloc`。

不得从 Runtime、Tick 或 Player 程序集调用。

## 3. CharacterTest 组合

注册入口在：

- `CharacterTestLevelEditorRegistration.cs`
- `CharacterTestGameplayTriggerWorkspace.cs`

注册顺序使用同一个 world-object module 实例：

```text
CharacterTest World Object module
├─ 世界对象 default workspace
└─ 玩法触发器 explicit workspace
```

不得重新构造第二个 world-object module 注册 Trigger workspace；否则会产生两份内存文档、两个 Undo baseline 和关闭恢复竞争。

### 3.1 当前玩家生命阈值来源

当前非区域来源限定为`当前玩家生命值`，stable key=`characterTest.attribute.localPlayerHp`、Provider ID=2、模板=`attribute.local_player_hp.low.story.v1`。新建入口为`战斗状态 > 当前玩家生命值低于阈值时触发 Story`，作用对象仅LocalPlayer，动作复用Story `RaiseTrigger`，route固定`PerSubject + OncePerPresence`，最多一个状态键；不开放任意实体/属性表达式。

Low Threshold默认进入30、恢复35：HP≤进入值时激活，保持到HP>恢复值；恢复值须≥进入值，Editor整数范围1..100。初次`PrimeSilently`，对象离开`EmitExited`。每Gameplay Tick从角色权威属性采样并提交complete scalar batch，无玩家时提交空complete batch；`TrustedDerived`在世界恢复后重采样，不新增网络命令或Replay输入流。Scene Cache当前v3包含独立Threshold rows/catalog hash并纳入组合hash，仍兼容v2区域-only cache。

进入/恢复值、Story引用、复制/删除沿用同一事务、Undo和保存；未知历史值原样保留并给兼容警告，不在打开时覆写。非区域来源没有Scene定位按钮。效果句说明低阈值、Story与恢复条件，不重复套用区域Presence措辞。Signal、通用属性及其他来源须先满足§8正式领域门禁，不能因Demo/测试代码存在而加入新建菜单。

## 4. 投影与界面职责

实现按职责拆分：

| 文件 | 职责 |
|---|---|
| `CharacterTestWorldObjectLevelEditorModule.TriggerWorkspace.Model.cs` | 从现有 Authoring、Region Binding、目录和编译结果构建只读 workspace item、自动分类、搜索文本和效果句 |
| `CharacterTestWorldObjectLevelEditorModule.TriggerWorkspace.cs` | 两栏 UI、选择、新建、编辑、复制、删除、Scene 双向定位、窄窗口折叠和运行时抽屉 |
| `CharacterTestWorldObjectLevelEditorModule.Triggers.Inspector.Fields.cs` | 复用现有领域字段、P3.0 引用选择器、P3.2 目标筛选、紧凑动作条款和高级策略 |
| `CharacterTestWorldObjectLevelEditorModule.Triggers.BooleanConditions.cs` | 将现有 Condition Tree 无损投影为根布尔组、叶子取反和 Source-aware 条件行 |
| `CharacterTestLevelGameplayTriggerAuthoring.Conditions.cs` | 保持 ConditionId 与 Route 引用不变，事务性替换现有 condition root |
| `CharacterTestWorldObjectLevelEditorModule.Triggers.RuntimeInspector.cs` | 复用 P2.4 typed runtime diagnostics |
| `MxLevelEditorWindow.uss` | 工作区、紧凑 Region 入口和窄窗口样式 |

自动分类只派生`区域玩法`和`关卡流程`，不写入 JSON。搜索只更新 Editor 状态，不产生文档 Undo 或脏状态。

标准宽度下左栏约 280 px，可拖动；工作区宽度小于 920 px 时左栏折叠，通过`显示触发器列表 / 收起触发器列表`临时展开，主编辑区不保留旧 Inspector 空白列。

P3.3d 后，主编辑区不再构造独立`作用对象 / 触发对象`卡片。事件和模板锁定事实使用普通文字条款；阵营、队伍、Buff、来源机制与 Story 选择器直接嵌入条件或动作句。Story 的兼容参数保留在`高级规则`，不占用日常编辑区域。

P3.3e 的附加条件不另建 DTO。空列表映射为`PushTrue`，一条映射为叶子或
`Not(叶子)`，两条以上按`All / Any`稳定构造左结合二叉树。Editor 只编辑可无损
展平的同类根组；混合嵌套和未开放叶子只读保真。当前 Occupancy 来源只提供
`触发对象存在`，玩家生命阈值来源额外提供`触发前生命值 / 当前生命值`比较。

### 4.1 只读投影与不变性

读取、浏览、搜索、分类、选择、定位和窄窗口展开只更新 Editor 状态，不创建文档 Undo 或 dirty state。投影前后必须保持 Authoring ID、事件 / 条件 / Route / Action 顺序、canonical JSON、Trigger / Sensor / combined hash 和 Scene-owned Runtime cache 不变。

三个现有区域模板按原 Authoring 顺序投影；Presence Buff 保留 Entered / Stayed Apply 与 Exited Remove 的 Route / Action 顺序。未知来源、Buff / 效果归因 / Story 引用和复杂未知 condition tree 保留原值并显示兼容摘要；未开放内容保持只读。


## 5. 效果句

效果句由 Editor-only formatter 从类型化 Authoring 投影生成，不保存到 JSON，也不参与 Runtime 编译。

顺序固定为：

```text
对象身份与关系
→ 区域事件
→ 附加布尔条件
→ 按模板 Route 投影的动作
→ Buff / Story 正式显示名
→ 效果归因
→ 触发范围、冷却和次数
```

缺失引用保留原 stable ID 或 numeric ID，并在句子和诊断中显式提示。秒数只在 CharacterTest 正式 fixed tick rate 可整除时显示；不会把 Buff 叠层、Route 次数和 Trigger 冷却合并为同一概念。

formatter 是单向视图，禁止从中文句子反向解析配置。

## 6. 编辑、Undo 与保存

所有 mutation 继续调用 world-object module 已有方法，并由 `CharacterTestLevelEditorDocumentUndoSession` 捕获完整 envelope 快照。

- 下拉选择在选中时提交；
- 名称使用 delayed text field，在 Enter 或失焦后提交；提交延迟到当前 UI 事件结束，`Esc`会取消排队提交并恢复本次编辑前的正式值；
- 一次重命名、启用、筛选、参数、复制或删除对应一个 Undo step；
- mutation 后只重建 Trigger 列表和编辑区，恢复滚动位置与输入焦点；
- Undo/Redo 后重新构建列表、效果句、诊断和脏状态；
- 搜索、分类、选择、折叠高级规则和打开运行时检查不修改 Authoring。

`保存`由 `MxLevelEditorSaveCoordinator`统一预检全部关卡模块，写入 Authoring、重建 Scene-owned Runtime cache，并在最后保存受管 Scene。界面不再提供独立缓存同步按钮；即使 Authoring 已是 clean baseline，显式保存也会重新编译缓存，以修复历史遗留的 stale cache。

P3.3b 的持久化专项直接验证关卡窗口的 `SaveChanges` / `DiscardChanges` 回调：保存会同时固化 Trigger JSON、对应 Runtime cache 与受管 Scene，并建立新的 Undo 基线；放弃会重新打开最近保存的 Scene、重新加载唯一 module owner，并恢复相同配置与效果句。统一保存必须先通过正式校验、Trigger Compiler 和 Sensor Compiler，任一失败都不得覆盖现有 canonical envelope 或 catalog hash。

### 6.1 所属 ID 与保存基线

复制 Trigger 为新的 Source Definition、Condition、Definition、Route、Binding 和 Source Binding 分配唯一 ID。删除 Trigger 退休其拥有的这些 ID，保留原 Region；复制和删除各自仍是一项完整快照事务。

dirty state 以最近一次成功保存的基线判断：保存后 Undo 到不同内容重新显示未保存。Runtime cache 只经统一保存链更新；预检或编译失败保留原 canonical envelope 与 catalog hash，不能发布半套缓存。Runtime 检查只读已编译 Scene cache 和 typed Sensor / Occurrence / Action 状态，不从 Scene Collider 反推玩法事实。


## 7. Scene 集成

Region Inspector 不再创建完整 Trigger 编辑卡，只显示：

```text
关联触发器  N 个  [编辑关联触发器] [新建关联触发器]
```

Scene 中对 Trigger Region 右键可打开关联 Trigger，或基于当前三个正式模板新建。一个 Region 只有一个 Trigger 时直接选中；多个 Trigger 共用 Region 时只打开过滤列表，要求使用者显式选择。

从工作区点击定位会选择、Ping 并 Frame 区域 Transform，同时给 owner module 设置 Scene overlay 所需的 Region row context。区域几何仍由 Scene Transform 编辑，玩法查询仍由 Combat Sensor 与 `CombatPhysicsWorld`提供，不使用 Unity Collider callback。

P3.3b 的 Scene 集成专项通过 Inspector 按钮和 Scene `GenericMenu` 的真实回调验收入口，而不是只检查菜单文字。Presence Buff 的 Runtime 专项覆盖 Entered 施加、Stayed 保持和 Exited 移除；移除最后一个 Buff 后允许 Gameplay Component World 回收空的 BuffSet 组件。

## 8. Source 与 Action 门禁

新来源必须同时具备：

1. 领域模块拥有的正式 Source Adapter；
2. 稳定目录或 binding；
3. Authoring validator；
4. 正式 compiler；
5. 当前组合根中的 Runtime consumer；
6. typed diagnostics 和效果句 formatter；
7. focused Runtime 测试。

逐动作编辑还必须具备正式 Action Adapter、参数目录、target 约束和 action-level validator。当前三个 CharacterTest template 是现有正式领域契约，工作区不会直接修改任意 action kind、payload 数组或 Route 拓扑。

## 9. 手动验收

1. 打开 `MxFramework > Level Editor`，顶部选择`玩法触发器`。
2. 确认左侧只出现实际存在的`区域玩法`或`关卡流程`，搜索`601012`可找到中毒规则。
3. 选择一条规则，确认主区只有一个名称栏，以及`事件 / 条件 / 动作 / 效果`；不存在独立`作用对象 / 触发对象`卡片。
4. 确认事件和锁定身份是普通文字，不显示成整行灰色输入框；阵营、Buff、来源机制或 Story 选择器嵌入句子。
5. 修改名称、阵营、Buff 或效果归因；确认滚动位置和输入焦点不跳回顶部，顶部变为`有未保存更改`；名称草稿按`Esc`应恢复且不产生未保存状态。
6. 执行 Undo/Redo，确认字段、效果句和脏状态同步恢复。
7. 点击定位，确认 Scene 选中并聚焦相同 Region。
8. 在 Scene 对 Region 右键选择`编辑关联触发器`，确认回到同一工作区；多个关联项时不自动选择第一条。
9. 缩窄窗口到 920 px 以下，确认左侧折叠且主区不裁切；使用顶部按钮往返显示列表。
10. 移动区域或修改规则后点击一次`保存`，重新读取 `GameManager.GameplayTriggerSceneCache`，确认传感器范围、canonical envelope 与 catalog hash 同步更新。
11. 进入 Play Mode，按需打开`更多 > 运行时检查`，确认读取现有 Sensor、Occurrence 和 Action typed 状态。

## 10. 自动验证

聚焦 EditMode：

- `CharacterTestGameplayTriggerWorkspaceTests`
- `CharacterTestGameplayTriggerWorkspaceTransactionTests`
- `CharacterTestGameplayTriggerWorkspaceInteractionTests`
- `CharacterTestGameplayTriggerWorkspacePersistenceTests`
- `CharacterTestGameplayTriggerWorkspaceSceneIntegrationTests`
- `CharacterTestLevelGameplayTriggerAuthoringTests`
- `CharacterTestWorldObjectLevelAuthoringTests`
- `CharacterTestLevelGameplayTriggerRuntimeTests`

公共 Editor workspace seam 属于 V4 变更。提交前还应执行 Unity refresh/compile、Console error check、`run_lightweight_checks.sh`和 `check_code_standards.py` report-only，并记录实际窗口检查结果。

测试存在或曾经运行过不等于当前任务完成。每次执行必须把当前 `main`、提交、测试结果和已知例外写入对应任务文档的完成记录。

## 11. 未完成验收

当前HP来源合同见§3.1。后续完成紧凑条款与布尔条件的Unity focused检查、Console、标准/窄窗口真实视觉与编辑/Undo/保存交互，分别见P3.3d/e任务。不得由源码控件存在、旧构建记录或历史测试数字标记完成。本次只整理文档，未操作Editor或重新运行验收。
