# Editor 接口

> Status: Current

## 职责

Editor 提供 Unity 内部可视化工具入口。正式工具使用 UI Toolkit，用户可见文案默认中文，技术标识保留英文。

动画资源压缩入口：`MxFramework/Assets/批量压缩动画片段`，以及 Project 右键 `MxFramework/动画/快速压缩动画（默认）`。支持选中独立 `.anim` 或目录、只读分析、按曲线误差精简、默认根运动保护、逐片段 Undo 和实际文件大小报告；实现留在 Editor 程序集，不引入 Runtime API。使用与恢复边界见[动画片段批量压缩](../Guides/ANIMATION_CLIP_COMPRESSION.md)。

`MxAnimationClipCompressionWindow.RegisterSource(id, displayName, resolvePaths)` 注册 Editor 侧资源来源，`OpenForSource(id, compressImmediately=false)` 打开指定来源；均为 `LowFreqAlloc`。Resolver 每批重新返回独立 `.anim` 资产路径，窗口去重后处理；来源解析失败不开始写入。项目层 `BasicMovementAnimationCompression` 注册“剑与法杖基础移动配置”，只读取 Global Weapon Library 的 locomotion 槽位对应组，并提供 `MxFramework/Assets/一键压缩剑与法杖基础移动动画`。框架不引用项目配置或项目程序集。

## 当前公开入口

| 类型 | 用途 |
|------|------|
| `FrameworkManager` | `MxFramework > Framework Manager` 主窗口，包含模块概览、配置工作台和运行模式提示 |
| `FrameworkModuleInfo` | 模块列表元数据 |
| `FrameworkManagerToolInfo` | Framework Manager 工具入口描述，包含分组、状态、菜单路径和打开动作 |
| `FrameworkManagerToolRegistry` | Editor 程序集向 Framework Manager 注册工具入口的扩展点 |
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
| `CharacterPackageImportCommand` | Character Resource Package Unity 导入桥入口，提供菜单导入、重复导入和 batchmode `-executeMethod` |
| `CharacterModelPreviewCatalog` | 从已导入角色、Loadout 和正式动画目录提供通用 Editor 预览候选；不按 EquipmentState 合并 Loadout |
| `CharacterModelPreviewSession` | 在调用方提供的 Preview Scene 内装配纯视觉角色；事务式换装、保留 Animator、诊断和幂等释放，不播放动画 |
| `StoryRuntimeDebugWindow` | `MxFramework > Story > Runtime Debug`，只读查看 Story runtime 快照 |
| `StoryEditorDebugRegistry` | Story.Editor 侧注册当前可观察 `StoryRuntimeModule` target |
| `StoryRuntimeDebugSource` | 将 Story runtime graph / beat / blackboard / event queue 状态映射为 Debug UI snapshot |
| `IMxLevelEditorSceneChangeAwareModule` | 关卡模块参与场景 Transform 未保存跟踪、已登记 Transform 汇总和未登记对象覆盖检查的 Editor-only 扩展点 |
| `IMxLevelEditorSceneScope` | 可选的 Editor-only 场景归属判断；统一保存和未保存提示只处理适用于当前活动场景的模块 |
| `IMxLevelEditorSaveParticipant` | 关卡模块参与“全部预检、配置写入、场景最后保存”的 Editor-only 统一保存事务 |
| `MxLevelEditorSaveCoordinator` | 统一保存全部脏模块；可选 `IMxLevelEditorAssetSavePaths` 将模块修改过的共享资产纳入事务快照，写入失败时恢复磁盘基线并保持场景未保存 |
| `MxLevelEditorSceneCoverage` | 从模块共享场景根中找出未被任一模块登记的顶层子树；忽略已登记对象的子节点与纯分组祖先 |
| `MxLevelEditorWindow` | `MxFramework > Level Editor` / `Window > MxFramework > Level Editor`，统一承载模块、导航、虚拟化列表、渐进属性、放置弹层，以及保存并同步运行时缓存 |
| `IMxLevelEditorWorkspace` | Editor-only 顶层内容投影契约；声明稳定 workspace ID、显示名、owner module 和内容构建，不拥有保存或脏状态 |
| `MxLevelEditorWorkspaceRegistry` | 按稳定 ID 注册显式 workspace；低频 `AllocByDesign`，同 ID 替换，不创建第二个 module/save participant |
| `MxAuthoringReferenceOption<TValue>` | Editor-only 目录候选值，保存稳定键、真实值、显示名、摘要和 Tooltip；低频 `AllocByDesign` |
| `MxAuthoringReferenceField` | 从业务正式目录候选构建 0/1/多项/未知值兼容的 UI Toolkit 引用控件；低频 `AllocByDesign` |
| `MxLevelAuthoringTargetClassification` | 将正式 `LevelAuthoringDefinitionUsage` 无分配投影为 Gameplay 基础身份/能力，保持 Config.Levels 不反向依赖 Gameplay |

## 模式规范

- `编辑模式`：不依赖 Play Mode，用于配置、Schema、依赖和静态校验。
- `运行模式`：默认只读，通过 `IFrameworkDebugSource` 快照显示运行时状态。
- 不允许把运行时对象反向写回配置表。
- 所有验证和沙盒结果都应能复制或导出报告。
- Story runtime debug 窗口默认只读；可写 debug command 必须通过显式 command gate 或 Story `RuntimeCommandBuffer`。

## Level Editor P2.5 Current 边界

- CharacterTest 世界对象模块当前提供数据化定义、真实 Prefab/显式 primitive 放置、区域 Trigger Authoring、统一保存和低频运行时检查。
- `保存`是唯一提交入口：统一预检当前活动场景适用的关卡模块，写入 Authoring 与 Scene-owned 派生缓存，再保存受管 Scene；它不直接改动活动 Runtime session，用户必须重新进入 Play Mode 或重建 offline session。
- 拥有场景专属 Authoring 文档的模块必须实现 `IMxLevelEditorSceneScope`；不适用于活动场景时不参与预检、事务快照、写入或未保存提示。新场景只有显式绑定其关卡文档后才参与对应模块的保存。
- 正式 `region.start` 示例把区域 Transform 编译为 Combat AABB，查询候选容量 64、映射主体容量 8；区域不要求 Collider。
- Level Editor 不拥有 Gameplay、Buff、Story 或 Combat 的第二份 Runtime 模型；窗口只编辑各领域 Authoring，并通过正式 compiler/bridge 接入现有模块。

## Level Editor P3.3 Workspace 边界

- 顶部下拉框显示 module 的默认 workspace 与显式 `IMxLevelEditorWorkspace`；显式 workspace 继续使用 `OwnerModuleId` 对应 module 的文档、校验、Undo、保存与缓存同步、SceneView 能力。
- `IMxLevelEditorWorkspace`不得实现第二份 `Save()`、脏状态或文档 session。CharacterTest 的`世界对象`和`玩法触发器`必须注册为同一个 world-object module owner 下的两个视图。
- `MxLevelEditorWindow.TrySelectWorkspace(...)`、`SetWorkspaceSceneContext(...)`和`RefreshWorkspaceState()`是显式 Editor 导航/界面更新，`LowFreqAlloc`；`CurrentWorkspaceId`为 `NoAlloc`。
- 当前`玩法触发器`工作区投影 CharacterTest 已正式接入的 Combat occupancy 区域、三个区域模板和`当前玩家生命值低于阈值`来源。其他 Signal、Threshold 及逐动作编排必须先有业务 Source/Action Adapter、validator、compiler 和 Runtime consumer。
- 工作区宽度小于 920 px 时左侧列表折叠，主编辑区保持可用；Region Inspector 只保留关联入口，不再重复构建完整 Trigger 编辑卡。
- 主编辑区使用紧凑`事件 / 条件 / 动作`条款：只读事实是普通文字，目标直接进入动作句，目录选择器嵌入条款；不得重新增加独立`作用对象 / 触发对象`大卡或灰色只读输入表。
- P3.3e 的条件组合直接替换现有 `GameplayTriggerConditionAuthoring.Root`：0 条为`PushTrue`，1 条为叶子或`Not`，多条为确定性左结合`All / Any`。该 Editor 投影为`AllocByDesign`；不得建立第二份 Runtime condition 模型。
- 条件候选必须由当前 Source Adapter 的真实语义约束。CharacterTest Occupancy 只开放 Subject existence；玩家 HP Threshold 额外开放 Previous/Current scalar，并以生命值单位显示、以现有 `Fix64.RawValue`持久化。混合嵌套和未知叶子必须只读保真。
- 保存事务必须先完成全部模块的校验与缓存编译；失败时不得把半套 Authoring 或 Scene cache 标记为已保存。
- 设计与实现职责见 [P3.3 统一触发器工作区](../LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DESIGN.md)和[开发指南](../Guides/LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md)。

## CharacterTest 创世验证 P1 Current 边界

- `characterTest.genesisValidation` 是 `mx.level.authoring.v3` 中的 CharacterTest 项目私有 payload；它保存根级 `Groups[]`、验证站、Canon 追溯、typed owner identity、Authoring scenario 与人工观察请求，不新增 MxFramework 公共 Genesis API。
- 同一 payload 还保存根级 `ScenePlan`（Scene bounds、origin、layout policy、typed `Zones[] / Routes[]`）和每站 `SpatialProgram`（template、zone、footprint、entry / observation / exit、approach / clearance）。Route waypoint、连通图和 template / scenario / group / zone 一致性均由 P1 Authoring validator 管理。
- 顶层`创世验证`模块复用现有结构化 Level Editor、UI Toolkit、文档 Undo、选择同步、SceneView overlay 与 `MxLevelEditorSaveCoordinator`。新增、复制、分组、排序、禁用和删除只修改 validation draft；读取、搜索、展开、定位和诊断不制造 dirty state。
- `创建基础场景框架` 是 CharacterTest 私有 composition seam：它调用同一个 world-object Level Editor module 的正式 Definition、placement 和 authoring model，创建 3 个首批 Zone 的 Region / Marker / Checkpoint / fixture，并把 world-object 文档、Genesis 文档和 Scene GameObject 折叠为一个 Unity Undo。它拒绝已有 Zone / Station 或重复 `GenesisValidationLayout`，不手写 `.unity` / world-object JSON，也不自动保存官方 Scene。
- P1 保存前除结构与 typed identity 外，还校验解析后的 Scene 几何：Scene bounds 包含、Zone overlap / clearance、station role / target locality、approach length、sample clearance 与无 junction Route crossing。毫米位置、毫度旋转和毫米 scale 进入 Editor diagnostics hash；该 hash 只证明空间变化可检测，不是 P2 Runtime cache hash 或 physics authority。
- P1 Authoring descriptor registry 独立于 executable compiler、runner 和 observer。未知 schema / scenario、丢失或错类型引用、重复 Stable ID、无效 group、typed payload 与 observation vocabulary 错误 fail closed；缺 executable adapter、Runtime command 或 snapshot 只让 capability 保持 `HarnessOnly / NotRunnable / NotBuilt`，不阻止合法 Authoring 保存。
- 静态绑定只允许 enabled `singleStaticColliders`，保存 `SingleStatic + BodyId + normalized ColliderId + ExpectedDefinitionId`。picker 不显示 batch child；未知 `SourceKind`、single key 歧义、Definition guard 不一致和 owner expanded collider 全局 key 冲突都阻止保存。validation payload 不保存 Transform path、GlobalObjectId、Unity instance id、空间副本或领域参数。
- P1 save participant 只通过 `UpsertModulePayload` 写 Authoring，不编译、创建或应用 Scene cache。pre-commit `false` 保留保存前 dirty baseline；success 后的 tail `false` 进入幂等 `Committed -> TailFailed`，保留已提交 clean baseline且不声称文件已恢复。
- P1 Editor 以只读方式对照 Genesis typed fixture、world-object owner Authoring 与 `GameManager.WorldObjectSceneCache`：已应用夹具保持正常实体表现；未应用夹具显示橙色`无框架碰撞 / 无框架可动物体`线框，引用失配显示洋红色警示。任一状态未闭合时阻止进入 Play，并引导用户修复后点击统一`保存`；保存会同步 Scene-owned 缓存。该门禁不添加 Unity Collider、不修改 Runtime authority，也不构建 P2 Genesis cache。
- P2 seam 源码复核结果：受控移动候选入口是 `CharacterTestRuntimeCommandFactory.SetControlIntent`；三势调试候选入口是 `MovableNaturalObjectPotencyInfluence`；角色观察候选为 `TryGetAuthorityPoseSnapshot` 与现有 body-part contact debug snapshot；可动物体观察候选为 `CaptureNetworkMovableNaturalObjects` / `CharacterTestMovableNaturalObjectSnapshot`。这些仅登记为后续 adapter 候选，P1 未建立 runner、signal mapping、startup correlation、runtime cache 或 executable claim。

## 用户可见配置控件规范

### 临时角色模型装配

`MxFramework.Editor.CharacterPreview` 的 `CharacterModelPreviewChoice`、`CharacterModelPreviewLoadout`、`CharacterModelPreviewClip` 是 Editor 候选投影。`DiscoverCharacters / DiscoverLoadouts / DiscoverClips` 读取既有导入目录；角色键包含包与角色身份，Loadout 保留 StableId，动画返回实际 AnimationClip（包括子资源）。

`CharacterModelPreviewSession.TryCreate(choice, loadoutKey, hideWeapons, scene, out session, out diagnostic)` 只接受有效 Preview Scene；失败释放候选并返回诊断。成功返回拥有 `Instance / Animator / CharacterKey / LoadoutKey / WeaponCount / WeaponsHidden` 的句柄。`TrySetEquipment` 全部准备成功后替换武器，失败保留旧装备；`Dispose` 释放实例和资源句柄，可重复调用。调用方拥有舞台与会话生命周期；临时实例禁止保存到正式资产，不安装角色控制器或运行其它动画播放器。

项目侧 `AnimationDesignerLauncherWindow` 使用这些接口，菜单 `MxFramework/Character/Animation Designer Launcher`；第三方类型仅由项目 `AnimationDesignerAdapter` 通过定点兼容访问处理。装配预检仍在 Preview Scene 完成，最终插件目标移入舞台专用的普通附加场景，因为插件 OnGUI 拒绝所有 Preview Scene。项目舞台负责普通场景的创建、隔离呈现和关闭，保持原 active scene；框架 API 不因此开放任意正式场景装配。框架不引用 Animation Designer 或项目程序集。操作与生命周期限制见 [使用指南](../Guides/ANIMATION_DESIGNER_LAUNCHER.md)。

Launcher 的项目侧片段实时预览使用同一临时角色舞台与装配，改由 Unity Animation 窗口独占片段采样；临时 AnimatorController 仅存在于会话内，不写入正式资产。“根运动曲线预览”快捷入口同时定位 Root T.x/y/z 曲线，不修改片段内容；“保存当前动画”显式保存正在预览或选中的独立 `.anim`，不写其他资源，也不创建 Git 提交。该模式只预览原始片段及其未保存曲线修改，不叠加 Animation Designer 插件或 CharacterAction RootMotion；切换模式与退出时释放临时绑定。入口与结束方式见上述指南。

Launcher 的项目侧快速替换只对已跟踪且干净的独立 `.anim` 执行 Unity 内容复制与单路径 Git 提交，保留资源身份；撤回只恢复该资源并追加恢复提交。Git 后端、事务日志与按钮均为项目 Editor 内部实现，不增加 Runtime API，也不修改 CharacterAction／资源映射 schema。当前行为及失败保护见上述指南“快速替换、提交与撤回”。

### 字段显示

- 给人日常使用的配置字段优先中文命名；技术 key 和 stable id 可在括号、tooltip、兼容提示或导出报告中保留英文。
- 枚举、资源引用、Action / Intent / Combat / Animation / Equipment Slot / Tag / Profile 等可枚举或可发现字段，优先用下拉框、搜索选择器、对象选择器或引用选择器，不应要求用户直接手填字符串。
- 对未知旧值和外部导入值必须容错：保留原值，加入候选项或标记为 `missing/未知`，在字段附近用 `HelpBox` 说明风险和修复方向。
- 不同类型的配置只显示相关参数，避免所有事件共用同一批无关输入框。隐藏字段不得在用户未确认时被静默清空。
- 配置控件应提供合理默认值、空值提示、校验提示和导出前错误；运行时会失败的问题应在编辑器里尽早显示。
- 稳定事件 ID、debug marker 等确实需要精确输入的技术字段可以保留文本输入，但应优先自动生成，并说明什么时候需要手动修改。

## 通用创作引用

- `MxAuthoringReferenceField.Create<TValue>(...)`只负责 Editor 展示和用户显式选择；候选必须由业务适配器从自己的正式目录或编译结果构建。
- 0 个候选只显示诊断，1 个候选显示只读文字，2 个及以上显示紧凑选择框；未登记当前值必须保留原值并显示兼容警告。
- 通用层不得注册 Provider、反射发现业务类型、维护全局引用缓存或创建第二份 Runtime 目录。
- Gameplay Trigger 中，`作用目标`来自 `GameplayTriggerTargetSelector`和 binding；`触发对象`来自 Trigger Source 上下文；界面的`来源机制`写入 `GameplayBuffApplicationRequest.SourceId`，只记录 Buff 施加来源，不执行被引用来源定义的 Effect；Runtime `命令来源`不得作为 `SourceId` 候选。
- 当前 CharacterTest 首个接入和扩展边界见 [P3.0 通用创作引用设计](../LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DESIGN.md)。
- P3.1 在内部继续分开解析目标来源与身份规则，但 Inspector 以一个人性化`作用对象`摘要展示：当前区域 Buff 显示`进入区域的角色 / 可伤害 · 阵营不限`，Story 显示`当前区域`。`GameplayTriggerTargetSelector`、`GameplayTargetClassificationComponent`、关系、Tag 和 Status 等完整契约保留在 Tooltip；只读摘要不写回第二份配置。
- 目标语义分层和接入方法见 [P3.1 目标语义基础设计](../LEVEL_EDITOR_TARGET_SEMANTICS_DESIGN.md) 与 [开发指南](../Guides/LEVEL_EDITOR_TARGET_SEMANTICS_DEVELOPMENT.md)。
- P3.2 只开放当前 Combat 区域能真实执行的 Subject Filter：阵营关系和参照队伍在统一`条件`条款中编辑；Character/Alive 和模板最低能力保持锁定，额外 Movable 进入`高级规则`。Buff 与 Story 的实际作用目标直接显示在各自动作句中。
- 参照队伍必须来自当前场景角色文档的正整数 TeamId；修改复用文档事务和单步 Undo/Redo，未知历史值保持原值并阻止编译。实现与迁移见 [P3.2 目标筛选设计](../LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DESIGN.md) 和 [开发指南](../Guides/LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DEVELOPMENT.md)。
- P3.3 将上述引用、目标语义和 Subject Filter 复用到顶层`玩法触发器`工作区；名称、启用、模板参数、筛选、复制和删除继续走同一文档 Undo Session，不引入第二份 Trigger Authoring。

## Framework Manager 扩展点

- `FrameworkManager` 只保留窗口生命周期、页面路由和轻量组合逻辑；模块概览、配置工作台、运行模式和工具入口分别拆在独立 partial 文件中。
- 其他 Editor 程序集不应直接修改 `FrameworkManager.cs` 来新增入口，应调用 `FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(...))`。
- `工具入口` 页会按 `Group / SortOrder / DisplayName` 展示已注册工具，并通过注册的 action 或菜单路径打开对应工具。
- 当前已接入内置入口：模块概览、配置工作台、示例资源 Catalog、Character Resource Package 导入和 MxAnimation 工具。
- 旧 Runtime Preview Server 菜单已随 Buff editor preview 链路退役。

## 当前配置工作台

- 菜单路径：`MxFramework > Framework Manager`。
- 操作路径：`编辑模式 > 配置工作台`。
- 内置 Demo 配置源：`内置 Demo / BasicModifierConfig`、`内置 Demo / ActionCatalog`、`内置 Demo / ActionGraph`。
- Buff v2 不再通过旧 ConfigEditor Buff 配置源创建；定义由 Gameplay 组合根注册。
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

## Character Resource Package 导入桥

- 菜单路径：`MxFramework > Character > Import Character Package...`。
- 重复导入：`MxFramework > Character > Reimport Last Character Package`。
- Batchmode：`MxFramework.Editor.CharacterImport.CharacterPackageImportCommand.Import -characterPackage <path>`。
- 实际导入逻辑不在 Unity 面板中复制实现，而是调用 Authoring CLI `character import-unity`，复用 C0.6 `CharacterAuthoringCompiler` 和 C2 `CharacterUnityImportBridge`。
- 导入后调用 `AssetDatabase.Refresh()`，Unity Console 输出 `import_report.txt` 摘要；完整报告在 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/package_cache/import_report.json`。
- Unity Editor 仍不是角色主创 UI；它只承担导入、刷新、日志和开发者验证入口。

## Story Runtime Debug

- 菜单路径：`MxFramework > Story > Runtime Debug`。
- 接入：Editor 环境调用 `StoryEditorDebugRegistry.Register("Name", storyRuntimeModule)`。
- 显示：graph、active beat、step、blackboard、event queue、`StoryRuntimeModule.RecentEvents`、last commands 和 command errors。
- 边界：窗口不调用 Story Director mutation API，不写 Replay / SaveState / Runtime hash，不创建 Unity 序列化资产。

## Level Editor 场景变更扩展点

- `IMxLevelEditorSceneChangeAwareModule.ManagedSceneRoot` 声明模块负责检查的场景根。
- `CollectManagedSceneTransforms(...)` 把当前模块已登记的 Transform 追加到调用方列表；窗口汇总所有模块后再执行覆盖检查，避免把另一个模块已管理的对象误报为未登记。
- `NotifySceneTransformChanged(...)` 只响应已登记对象或其受管理子节点的移动、旋转和缩放，并让模块进入未保存状态。
- `RefreshUnregisteredSceneObjects(...)` 接收全模块登记集合，更新模块自己的警告和定位入口。
- 以上接口及 `MxLevelEditorSceneCoverage.CollectUnregisteredRoots(...)` 均为 `LowFreqAlloc` Editor 交互路径，禁止从 Runtime Tick 或 Player 代码调用。

## Level Editor 窗口解绑与关闭恢复

- 可选 `IMxLevelEditorWindowLifecycleModule.DetachWindow(window)` 在窗口停用时解除 UI 委托与事件订阅；必须幂等，不能清除关闭保存/丢弃仍需要的 authoring owner、手势与 dirty 状态。
- 窗口在 `OnDestroy` 的恢复流程后再次解绑，覆盖 `Reload()` 重新启用订阅的情况；模块对窗口使用 Unity 判空，不能通过 `?.` 调用已销毁的 Unity Object。
- 明确丢弃时，窗口通过既有 `IMxLevelEditorAssetSavePaths` 重读当前脏模块的已保存资产，再恢复当前受管场景；其它已加载场景保持原状。普通场景切换/模块 `Reload()` 不丢弃未保存的资产内容。
- 该合同为 Editor-only、`LowFreqAlloc` 关闭/重开路径，不进入 Runtime/Player Tick。

## Level Editor 放置弹层扩展点

- `IMxLevelEditorPlacementModule.BuildPlacementPanel(...)` 只在用户打开临时放置弹层时构建模块字段、选项和主操作，不创建常驻工具栏。
- `MxLevelEditorPlacementPanel` 提供 `FieldsHost`、`OptionsHost` 和 `ActionsHost` 三个稳定布局槽；图标开关通过 `AddIconToggle(...)` 加入选项区。
- `MxLevelEditorSaveCoordinator.IsSaveInProgress` 提供统一保存期间的 Editor-only、`NoAlloc` 状态；场景监听器据此忽略保存事务自身产生的瞬态 dirty / hierarchy 通知，避免保存成功后其他关卡模块被误标为未保存。
- `IMxLevelEditorSceneChangeAwareModule.AcknowledgeManagedSceneSaved()` 在 Unity 场景保存完成后只清除模块的场景 dirty 状态，不影响尚未写入 JSON 的文档 dirty 状态；窗口同时监听 `sceneSaved`，覆盖统一保存和用户直接保存 Unity 场景两条路径。
- `IMxLevelEditorDirtyDiagnostics` 将聚合的 `IsDirty` 细分为文档与场景来源，只用于低频保存失败提示；统一保存会在模块完成回调后再次检查并收尾保存 Unity 场景，覆盖完成阶段重新产生 dirty 的情况。
- `MxLevelEditorPlacementContext.ClosePlacementSurface()` 在开始放置或模块完成当前弹层操作时关闭临时界面，不改变场景或配置。
- 场景标记偏好只控制标签、范围和辅助轮廓；`HandleScenePlacement(...)` 与 `DrawScenePlacement(...)` 不受该偏好影响。
- 上述接口均为 `AllocByDesign` 或 `LowFreqAlloc` Editor 交互路径，不进入 Runtime Player 程序集。

## 后续扩展

- 完整 `ConfigEditor` 资产编辑和导入器适配
- `BuffsEditor`
- `ModifiersEditor`
- `AIEditor`
- 依赖图和 Health Check 后续作为独立编辑器能力设计，不绑定外部索引工具

## 验证入口

通过 Unity MCP 执行菜单 `MxFramework/Framework Manager`，并读取 `mcpforunity://editor/windows` 确认窗口存在。

## 关卡文档闭环

本批完整联合候选保存、诊断定位、组选择/复制门控和只读缓存检查已合入 Main；Unity 编译/域重载、46 个不同 focused EditMode 方法，以及实际新建关卡→添加 AI→选择正式三卡战术组→保存/重载→Play ID/hash→退出恢复已通过。底层模型与上层战术卡两层保留；旧玩家组兼容入口保留，新增 AI 组与复制组使用相同真实消费能力检查。完整 AI 共用费用/CD/效果规则仍未接入，统一组合牌玩法/工程文档由专责继续定义；不把现有 AI Lab 保存组流程当作完整共用规则验收。

> Code-Doc: `Packages/com.vincent.wgameframework/Editor/Levels/MxLevelEditorDocumentOwner.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Editor/Levels/MxLevelEditorSaveOutcome.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Editor/Levels/MxLevelEditorBatchSelection.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Editor/Levels/MxLevelEditorSaveCoordinator.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/Levels/CharacterTestLevelEditorDocumentSession*.cs`

`IMxLevelEditorDocumentOwner` 和 `MxLevelEditorDocumentOwnerRegistry` 是 Editor-only 的项目文档工作流接缝。owner 构建固定工具栏、提供规则脏状态和统一保存，package 不引用 CharacterTest。`IMxLevelEditorDraftProvider` 返回深拷贝的模块草稿；`IMxLevelEditorDocumentValidationOwner` 检查整个当前文档；`IMxLevelEditorScopedDocumentOwner` 提供保留其他 Scene 编辑的放弃操作。`IMxLevelEditorDocumentUndoBoundary` 在重绑文档时退役旧文档 Undo 回调。项目的同步 joined save scope 只供此编辑工作流使用，不是 Runtime API 或新存档 schema。

`MxLevelEditorWindow.HasPendingDocumentChanges` 包含 owner、模块和当前 Scene。`TryPrepareDocumentSwitch(Save/Discard/Cancel)` 在身份切换前解析未保存内容；`ReloadDocumentModules()` 重载全部模块并清除旧导航选择；`RevealDocumentRow(moduleId, treeId, rowKey, fieldName)` 复用列表、Scene 选择和 Inspector。这些操作为 `LowFreqAlloc`，状态查询为 `NoAlloc`。`IMxLevelEditorBatchSelectionModule` 是可选的 UI 投影，当前只有角色模块支持多选队伍／分组编辑。

统一保存先预检完整汇总文档，保留所有参与者的文档和 `IMxLevelEditorSceneSaveSnapshotProvider` 声明的 Scene 序列化对象快照。项目 owner 最后持久化同一完整候选，再从最终落盘 JSON 刷新世界、角色及触发器缓存。引导缓存使用同一候选中的引导模块 hash。Scene 第一次成功保存是提交边界：此前失败恢复文件和缓存并保留草稿；此后回执／收尾失败保留提交版本，报告 `CommittedWithTailFailure`，不把旧 JSON 写回已提交 Scene。`MxLevelEditorSaveOutcome` 的另两个结果为 `NotCommitted` 和 `Committed`；新增 outcome 重载保持旧调用签名可用。

CharacterTest 的新建／复制／选择、正式行为组版本与动作槽、检查／发布／试玩复用现有关卡目录和权威 compiler。试玩先保存并检查当前内容，再发布同一预览 token 的版本；以当前 level ID 覆盖本次启动入口，回到 Edit 时恢复旧入口。世界／角色错误可定位到列表和 Scene；教学步骤及验证站可定位条目和已支持字段。未提供自动焦点的嵌套字段明确显示未支持，允许打开原文档；不宣称任意 JSON path 都有编辑控件。使用与验收边界见[关卡编辑工作流](../Guides/CHARACTER_TEST_LEVEL_EDITING_WORKFLOW.md)。


- CharacterTest 联合关卡 owner 在程序集重载前用 Editor SessionState 留存完整草稿、关卡／Scene／源 hash、dirty／保存基线与登记对象范围基线；同一已加载 Scene 生命周期恢复后仍保留未保存状态。源冲突拒写并保留恢复稿，显式丢弃才接受新源；Scene／原生身份不匹配则保留记录并拒绝自动套用。恢复对象树采用扁平 DTO 与原生 EntityId 重绑，不保存所有 Scenes。2026-10-10 独立原生 19/19 与真实域重载后的保存／丢弃／源冲突验证通过；不承诺 Editor 进程重启恢复。操作与能力边界见[关卡编辑工作流](../Guides/CHARACTER_TEST_LEVEL_EDITING_WORKFLOW.md)。
