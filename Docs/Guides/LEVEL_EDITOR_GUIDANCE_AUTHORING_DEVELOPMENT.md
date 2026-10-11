# Mx 关卡编辑器 P3.4：教学引导可视化 Authoring 开发指南

> Status: Design
>
> Parent design: [P3.4 教学引导可视化 Authoring 设计](../LEVEL_EDITOR_GUIDANCE_AUTHORING_DESIGN.md)
>
> Implementation status: Current — P3.4a～P3.4e 已完成并通过正式数据链、Unity 与视觉门禁
>
> Last reviewed: 2026-08-13
>
> Scope: CharacterTest 项目私有 Authoring、Level Editor、Scene cache、Runtime coordinator、测试与原子切换

本指南把已接受并已实现的 P3.4 设计拆成可独立维护与回归的工程切片。下文同时保留阶段合同
和 2026-08-13 完成记录；当前实现事实仍应从源码、测试和 Current 能力 / 接口文档核对。

玩家可见 Guidance contract 和 FairyGUI 表现不在本指南内重新设计；需要修改 Card、Notice、
Presenter、package 或 binder 时回到
[正式玩家引导提示 UI 设计](../PLAYER_GUIDANCE_PROMPT_UI_DESIGN.md)、
[UI 接口](../Interfaces/UI.md)与
[FairyGUI 接口](../Interfaces/UI.FairyGUI.md)。

---

## 1. 开发前置与不变量

### 1.1 先读入口

实施任何 P3.4 C# 或 Unity 资产变更前必须读取：

- [P3.4 设计](../LEVEL_EDITOR_GUIDANCE_AUTHORING_DESIGN.md)；
- [关卡编辑器能力复用矩阵](../LEVEL_EDITOR_CAPABILITY_REUSE_MATRIX.md)；
- [Editor 接口](../Interfaces/Editor.md)；
- [Input 接口](../Interfaces/Input.md)；
- [UI 接口](../Interfaces/UI.md)与[FairyGUI 接口](../Interfaces/UI.FairyGUI.md)；
- [API 标准](../API_STANDARDS.md)与[质量门禁](../QUALITY_GATE.md)；
- 若修改正式 Game 行为或 Playable，补读
  [游戏功能开发指南](../AGENT_GAME_CREATION_GUIDE.md)。

### 1.2 工程不变量

1. `characterTest.guidance` 是现有 `mx.level.authoring.v3` envelope 的独立模块。
2. `CharacterTestGuidanceLevelEditorModule` 是唯一文档 owner、Undo owner、dirty owner 和
   save participant；不再构造同数据的显式 workspace owner。
3. Player Runtime 只消费 scene-owned cache，不读取 `Assets/Config`、`AssetDatabase` 或
   Editor state。
4. completion condition 必须同时提供 Editor 字段、normalize/validator、compiler 和
   Runtime observer factory，不能先把占位选项暴露给作者。
5. completion observer 只读取已确认领域事实，不执行命令或修改领域状态。
6. Snapshot Mapper 只生成现有 `MxUiGuidanceCardSnapshot` / `MxUiGuidanceNotice`。
7. 缺失或非法正式数据 fail closed，不调用旧硬编码六步。
8. P3.4 不新增公共 API，不修改框架 Runtime/noEngine 依赖方向。
9. 本轮首版不加入 SaveState、Replay、Network、Gameplay hash 或活动 Session 热重载。
10. 每个阶段的“代码存在”不等于完成；只有对应自动、Unity 和人工门禁完成后才能更新状态。

## 2. 目标代码形状

### 2.1 Assembly 与依赖

P3.4 不新建 asmdef：

| 层 | Assembly | 允许依赖 | 禁止依赖 |
|---|---|---|---|
| Authoring model、validator、compiler、cache、Runtime coordinator | `MxFramework.Game.CharacterTest` | Config、Input、UI、Combat 与 CharacterTest 既有 Runtime 类型 | `UnityEditor`、Level Editor UI |
| Level Editor module、UI Toolkit 字段与语义预览 | `MxFramework.Game.CharacterTest.Editor` | CharacterTest Runtime assembly、`MxFramework.Editor`、`UnityEditor` | Player-facing FairyGUI package 创作权威 |
| 现有 UI contracts / Presenter | 原有 `MxFramework.UI` / `MxFramework.UI.FairyGUI` | 维持现有依赖 | 反向依赖 CharacterTest Guidance 类型 |

Authoring model 位于 CharacterTest Runtime assembly 是有意设计：Editor 写入的数据模型、
compiler 和 Player 解析器必须共享同一 typed contract。文件不应放进 `.Editor` assembly 后再
用重复 DTO 喂给 Runtime。

### 2.2 目标目录职责

目标仍按职责拆分；当前第一版先以以下实际文件落地，后续拆分不得改变 serialized schema：

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Guidance/
  CharacterTestGuidanceAuthoring.cs
  CharacterTestGuidanceRuntime.cs
  CharacterTestGuidanceSceneCache.cs

Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/Guidance/
  CharacterTestGuidanceLevelEditorModule.cs
  CharacterTestGuidanceSceneCacheFreshness.cs
```

Authoring、registry、validator、compiler 和 immutable plan 按内聚度位于同一 Runtime 文件；
Coordinator、typed observers 与 Snapshot Mapper 位于独立 Runtime 文件；Level Editor 的
Inspector/Preview 保持一个项目私有文件，cache freshness 与 Play/build preflight 独立成 Editor
文件。该拆分按职责而不是按类型数量组织。

### 2.3 冻结的项目私有类型

| 类型 | 职责 |
|---|---|
| `CharacterTestGuidanceAuthoringDocument` | 根 `enabled` 与有序 `steps`；normalize 只做确定性规范化 |
| `CharacterTestGuidanceStepAuthoring` | stable ID、启停、Input Action、自动前进、内容和 completion |
| `CharacterTestGuidanceTextAuthoring` | 系统托管 `key` + 作者编辑的简中源文案 `fallback`；转换为 `MxUiLocalizedTextRequest` |
| `CharacterTestGuidanceCompletionAuthoring` | `typeId`、`schemaVersion` 与 typed `JObject payload` |
| `CharacterTestGuidanceStepPreset` | 一个只用于创建新步骤的玩家意图预设；保存创作默认值但不参与 Runtime schema |
| `CharacterTestGuidanceStepPresetCatalog` | 六个显式创作预设；锁定/取消锁定复用同一 `TargetLockState` 条件并使用相反参数 |
| `CharacterTestGuidanceAuthoringAdapter` | 从 / 向 `characterTest.guidance` module Extract / Upsert；创建空文档、空白步骤、预设步骤、reference 六步并安全切换完成条件 |
| `CharacterTestGuidanceAuthoringValidator` | 模块结构、ID、文案、Input Action、模板参数与序列 warning |
| `CharacterTestGuidanceCompletionRegistry` | 五类 typed condition 的显式有序目录；禁止反射发现 |
| `CharacterTestGuidanceCompiler` | 从规范化 Authoring 编译 immutable plan、module source hash 与 catalog hash |
| `CharacterTestGuidancePlan` | 根状态和启用步骤的不可变 Runtime 启动输入 |
| `CharacterTestGuidanceStepDefinition` | 一条已编译步骤；不持有 Editor DTO 或 `JObject` |
| `CharacterTestGuidanceSceneCache` | GameManager 序列化的 immutable startup cache |
| `ICharacterTestGuidanceCompletionObserver` | Prime / Observe / Reset 生命周期；只返回 typed observation result |
| `CharacterTestGuidanceSequenceCoordinator` | 选择、observer 生命周期、完成、手动导航和自动前进 |
| `CharacterTestGuidanceSnapshotMapper` | plan + coordinator state 到现有 UI Guidance DTO 的单向投影 |
| `CharacterTestGuidanceLevelEditorModule` | Level Editor module、Undo、dirty、validate、save 与语义预览 |

这些类型保持 `internal`；需要由 Unity 序列化的 cache 可使用 `public sealed` 容器，但这只表示
Unity serialization surface，不构成框架公共 API 承诺。任何提升到公共框架层的提案必须等待
第二个正式项目消费者并单独评审。

## 3. Authoring 与编译合同

### 3.1 Normalize 与 Validate 分离

`Normalize()` 只能执行不会改变语义的确定性处理：

- null collection 变为空 collection；
- string 做 null-to-empty 与 trim；
- content / completion null 变为显式空对象；
- deep clone list 和 payload；
- 保持步骤数组原顺序；
- 不补 stable ID、不重排、不删除禁用步骤、不修正非法参数。

系统 Key 不由通用 `Normalize()` 猜测修复。所有 Level Editor 新建、复制和 stable ID 改名操作
必须显式调用 `AssignManagedTextKeys` / `ChangeStableId`；Validator 再精确校验六个 Key 是否等于
`ui.character_test.guidance.<stable_id>.<slot>`。这样既不吞掉外部非法数据，也不让作者日常维护
重复身份。

`Validate()` 负责报告错误和 warning，不暗中修复：

- 根启用但没有启用步骤；
- ID、Input Action、文案和 completion 合同；
- condition payload 类型与范围；
- 可疑但合法的锁定顺序与重复步骤。

不得通过 Normalize 把未知 completion type 替换成默认模板，也不得把非法数值 clamp 后保存。

### 3.2 Adapter

`CharacterTestGuidanceAuthoringAdapter` 使用固定常量：

```text
ModuleId = characterTest.guidance
ModuleSchemaVersion = 1
DisplayName = CharacterTest Guidance
```

目标方法语义：

```text
Extract(envelope)
  module 不存在 -> 返回“missing”结果，不伪装成默认正式数据
  schema 不支持 -> 返回 typed unsupported 结果并保留原 payload 用于诊断
  schema 支持 -> 解析并返回 normalize 后的 document

CreateEmptyDocument()
  根关闭、零步骤；缺失模块的无脏状态 Editor 投影

CreateBlankStep(stableId)
  创建启用但未配置的草稿；2.0 秒；立即建立六个系统 Key

CreateStepFromPreset(presetId, stableId)
  从六个玩家意图预设创建完整的新步骤；不覆盖当前步骤

ChangeCompletionType(step, typeId)
  保留作者字段和数组位置，只替换 completion type/schema/payload

CreateSixStepReferenceDocument()
  只用于当前正式六步夹具、迁移验证与 golden hash；不是新模块默认内容

Upsert(envelope, document)
  document 为空 -> 显式失败
  normalize envelope
  只替换 characterTest.guidance module
  保留全部其他 module 和 metadata
```

Editor 首次打开缺失模块时建立根关闭、零步骤的空文档，显示`空白步骤 / 从预设新建`入口；
不自动生成六步、不标脏、不产生 Undo。添加步骤也不自动改变根启用状态。

### 3.3 Completion condition registry

registry 是显式、稳定、有序数组，不使用 attribute、reflection 或 assembly scan。每个正式
condition 至少提供：

```text
TypeId
SchemaVersion
DisplayName
CreateDefaultPayload()
BuildInspectorFields()
TryNormalizeAndValidate(payload, issues, path, out normalizedPayload)
Compile(normalizedPayload)
CreateObserver(compiledCompletion)
ResolvePreviewSemantics(previewState)
```

`BuildInspectorFields` 只返回 Editor 可解释的 typed field metadata，不包含 UI Toolkit 对象，
从而避免 Runtime assembly 引用 `UnityEditor`。Editor assembly 根据 metadata 创建 Integer、
Toggle、String 等受控字段。

### 3.4 Stable hash

必须区分两个 hash：

1. `guidanceModuleSourceHash`
   - 输入是规范化 module identity、module schema 与 payload；
   - 不包含完整 level envelope、时间戳、绝对路径、Editor UI 状态或 Scene cache；
   - 使用稳定 ordinal 顺序，steps 保持作者顺序，JSON object 属性按固定合同输出。
2. `completionCatalogHash`
   - 输入是 condition 的 type ID、schema、参数名 / 类型 / 范围、compiler identity 和 observer
     identity 的稳定序列；
   - 不使用 CLR type hash、`GetHashCode()`、assembly path 或运行时注册顺序。

同一输入跨进程和跨 Editor 重启必须产生相同值。无关 level module 改动不得改变 Guidance
module source hash。两个 hash 都固定为“canonical UTF-8 bytes 的 SHA-256、小写 64 字符十六进制
文本”；禁止使用 `GetHashCode()` 或依赖平台换行。canonical serializer 使用 camelCase、ordinal
字段顺序和 `\n`，不写 BOM；hash tests 必须固定至少一个 golden vector。

## 4. Level Editor 实现

### 4.1 Module contracts

`CharacterTestGuidanceLevelEditorModule`实现：

```text
IMxLevelEditorModule
IMxLevelEditorStructuredModule
IMxLevelEditorDirtyDiagnostics
IMxLevelEditorSceneChangeAwareModule
IMxLevelEditorSaveParticipant
```

首版不实现 SceneView 编辑或 Selection Sync，因为 Guidance 不拥有 Scene object。

契约值：

```text
ModuleId = characterTest.guidance
DisplayName = 教学引导
DocumentPath = CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath
IsManagedSceneDirty = false
ManagedSceneRoot = current GameManager.WorldObjectSceneRoot
CollectManagedSceneTransforms = empty
NotifySceneTransformChanged = false
```

在 `CharacterTestLevelEditorRegistration` 中注册独立 module，不注册
`CharacterTestGuidanceWorkspace`。窗口自动为 module 创建默认 workspace。

### 4.2 Tree、Rows 与 Inspector

- Tree 根节点：`全部步骤`，count 为全部步骤数量；子节点`启用`、`禁用`仅作派生筛选，不写入
  JSON。
- 每个 row 显示顺序、标题简中源文案、completion 显示名、Input Action 和状态点。
- 搜索匹配 stable ID、标题源文案、Input Action、completion type 和简中源文案。
- Inspector 顶部显示 stable ID、启用状态、Input Action 和自动前进时间。
- Input Action 下拉从正式 `MxFrameworkInputActions.inputactions` 的 `Gameplay` map 动态读取；只有
  资产暂时无法解析时才使用最小恢复候选，Authoring 永远只保存 `Gameplay/Action` 语义 ID。
- `内容`区只编辑六个`简中源文案`；`本地化 / 高级`折叠区只读显示系统 Key、英文译文和翻译状态。
- `完成条件`使用只列五类正式条件的中文下拉框；Type ID / schema 在高级信息只读显示，参数区
  按当前条件动态建立。
- `预览`显示等待、完成；`TargetLockState`额外显示拒绝状态。
- diagnostics 使用稳定 code + JSON path，并能选择对应步骤；不把错误直接写进 Authoring。

### 4.3 Mutation 与 Undo

所有正式 mutation 统一走 `Mutate(label, Action)`：

1. 确保 document 和 undo session 已加载；
2. 捕获完整 Guidance document snapshot；
3. 执行一次 mutation；
4. 对编辑器拥有的步骤身份变化同步重建系统 Key，再 normalize；不自动修复其他错误；
5. 标记 document dirty；
6. 重新校验并刷新列表、Inspector、预览和 diagnostics；
7. 尽量恢复选中 stable ID、列表滚动和字段焦点。

一次用户意图对应一个 Undo step：

- 新建、复制、删除；
- 启用 / 禁用；
- 上移 / 下移；
- 单个文案字段提交；
- Input Action 或自动前进提交；
- 单个 completion 参数提交。

TextField 使用 delayed commit 或等价的 Enter / 失焦提交；`Esc`取消当前草稿且不标脏。
选择、搜索、预览状态、foldout、滚动和 diagnostics 不进入 undo snapshot。

复制 stable ID 使用确定性候选：先尝试 `<source>-copy`，已存在时依次
`<source>-copy-2`、`-3`。复制项插在源项之后，保留其他字段并成为当前选择。

### 4.4 空白步骤、创作预设与条件切换

工具栏和步骤栏统一显示：

```text
[＋ 空白步骤] [从预设新建 ▾]
```

预设菜单只列`基础移动与停步 / 跳跃与落地 / 切换相机模式 / 锁定目标 / 取消锁定 /
普通攻击命中`，不显示五个技术 Type ID。创建时：

- 空白步骤使用空 Input Action、空六项源文案、空 completion type、2.0 秒；
- 预设步骤使用预设的 Input Action、源文案、条件默认 payload 和自动前进；
- 预设的 stable ID 只是 seed；如果文档已有同名步骤，则依次生成 `-2`、`-3` 等唯一后缀；
- 插入当前选择之后；没有选择时追加；
- 一次创建只产生一个 Undo step。

完成条件可以在五类正式条件间切换。切换保留 Stable ID、启停、全部文案、Input Action、
自动前进、系统 Key 和数组位置；丢弃旧 payload，并用新条件的完整默认 payload。预览复位到
`等待`，整个转换是一个 Undo。未知 completion type 保持原值并显示诊断，但同一下拉框必须允许
作者选择正式条件进行修复；未知 module schema 仍保持整模块只读。

空白草稿允许留在会话和 Undo 历史，但 `MissingInputActionId`、六项
`MissingLocalizedTextFallback` 或 `MissingCompletionType` 中任一存在时都阻止 prepare。该规则不
因根关闭或步骤禁用而放宽，失败前不得写 envelope、cache 或 Scene。列表占位`未命名步骤`和
`未选择完成条件`不进入 Authoring JSON。

### 4.5 语义预览

预览不实例化 `MxFairyGuiGuidancePresenter`，不加载 `_fui.bytes`，也不依赖 FGUIProject。
它从当前 step + condition preview semantics 生成 Editor-only view model：

```text
Title
Action label / glyph / tone / text
Focus label / glyph / tone / text
Status label / glyph / tone / text
Optional Notice message / glyph / tone
```

`BasicAttackDamage` 的动态 outcome 使用固定、明确标记的示例（例如 damage 10、HP 100 -> 90），
不伪装成运行时数据。预览组件顶部永久显示`语义预览，不代表 FairyGUI 实际画面`。

## 5. Scene Cache 与统一保存

### 5.1 Cache API

`CharacterTestGuidanceSceneCache`采用与现有 Trigger cache 相同的 scene-owned 输入原则：

- `[Serializable]`；
- 私有 `[SerializeField]` 字段；
- Runtime 只读 property；
- Editor-only `Configure(CharacterTestGuidanceSceneCacheData data)`；
- `CurrentCacheVersion = 1`；
- 不保存 `LastAppliedUtc` 或任何时间戳。

`GameManager`新增私有 cache 字段和只读 property。Runtime Session builder 把 cache 交给
Guidance composition，不让 Host 自行查找 GameManager 或磁盘路径。

### 5.2 Compile candidate

`TryPrepareSave`必须一次性冻结 prepared candidate：

```text
normalized document
validation report
compiled immutable plan
canonical module json
guidance module source hash
completion catalog hash
cache data
cache-before-write snapshot
current save generation
dirty-before-prepare
```

prepare 后如果 UI 又发生 mutation，当前保存应拒绝或由窗口串行化到下一次 save；不能把
prepared hash 和变化后的 document 混在同一事务。

### 5.3 Shared document safety

`SavePreparedChanges`必须：

1. 确认仍处于同一 prepared generation；
2. 重新读取 `DocumentPath` 的最新 bytes；
3. 用 `MxLevelAuthoringJson.LoadFromJson` 解析最新 envelope；
4. 只调用 `CharacterTestGuidanceAuthoringAdapter.Upsert`；
5. 写回 canonical envelope 并 import asset；
6. 保留 prepare 阶段已捕获的 GameManager 原 Guidance cache 快照；
7. 把 prepared cache data 写入同一 Scene 的 GameManager；
8. 把状态标记为 Written，等待 coordinator 完成回调。

严禁保存打开模块时持有的旧 `_envelope` 副本。测试必须让另一个 participant 先写同一路径，
再验证 Guidance upsert 后两个 payload 都存在。

### 5.4 Save completion state

实现沿用已有 participant 语义，至少区分：

```text
Idle
Prepared
Written
Committed
TailFailed
PreCommitFailed
```

- prepare 或 write 前失败：恢复保存前 dirty 状态，不移动 Undo baseline；
- Written 后、首次 Scene commit 前任一参与者或 Scene save 失败：在 coordinator 恢复 JSON
  快照的同时恢复 GameManager 原 Guidance cache；不得留下仅存在于内存 Scene 的半套 candidate；
- coordinator 确认成功：清 dirty、提交 generation、重置 Undo baseline；
- 成功回调重复到达：幂等；
- 已提交后的 Scene tail failure：不谎称 JSON 已回滚，保持 clean baseline 并明确 tail failure；
- 事务失败后的下一次保存必须重新 prepare，不能复用旧 candidate。

## 6. Runtime 实现

### 6.1 Cache loader

在进入 Runtime loader 以前，Editor Playable / Player build preflight 必须读取磁盘最新
envelope，只计算 `characterTest.guidance` module source hash，并与目标 Scene cache 比较。
不一致返回 `GUIDANCE_CACHE_SOURCE_STALE` 并阻断进入 Playable / build；这个 preflight 不把
JSON 复制成第二份 Runtime 资产。

Runtime composition 的 loader 依次验证：

1. cache 是否存在；
2. cache version；
3. module schema；
4. canonical module JSON 可解析；
5. 重新计算的 module source hash 与 cache 相同；
6. 当前 registry catalog hash 与 cache 相同；
7. compiler 能产生至少一条启用步骤，或根被作者明确禁用。

作者禁用返回正常的 `DisabledByAuthoring`；其他失败返回 typed diagnostic 并不创建 Host。
loader 不调用 `CreateSixStepReferenceDocument()`，否则会重新引入隐式硬编码回退。

Player loader 不声称能比较 Editor 磁盘文件：它只能验证 cache canonical JSON 的内部 source
hash。自洽但落后于磁盘的 cache 由上述 Editor / build preflight 拦截；Player 中不得为解决
这一点重新读取 `Assets/Config`。

### 6.2 Immutable plan

`CharacterTestGuidancePlan`和`CharacterTestGuidanceStepDefinition`在组合阶段一次创建：

- 只保留启用步骤；
- 保持 Authoring 相对顺序；
- 文字保存为不可变 request；
- Input Action 保存语义 ID；
- completion payload 编译为对应 typed immutable definition，不在 Tick 中解析 `JObject`；
- stable ID 到 index 的查找表可在初始化时分配；Tick 不用 LINQ 或临时集合。

### 6.3 Observer contract

`ICharacterTestGuidanceCompletionObserver`的目标语义：

```text
Prime(in CharacterTestGuidanceObservationContext context)
Observe(in CharacterTestGuidanceObservationContext context)
  -> NoChange | ProgressChanged | Completed | Warning
Reset()
```

Context 只提供当前已确认 snapshot、只读 runtime event 列表和 damage telemetry。observer 不持有
Presenter、Input device、GameObject、`GameManager` 或 Editor DTO。

五类 observer：

- `MoveDistanceAndStopObserver`：捕获起点；距离达标后等待速度 / Grounded；
- `JumpAndLandObserver`：必须先见到 airborne，再见到 Grounded；
- `CameraModeChangedObserver`：prime 最新 camera event key，只接受更新 key；
- `TargetLockStateObserver`：prime 最新 lock event key；Rejected 返回 Warning 而非 Completed；
- `BasicAttackDamageObserver`：prime 最新匹配 damage key，只接受本地玩家到其他实体、prefix
  匹配、damage 达标且 HP 下降的新 row。

### 6.4 Coordinator

Coordinator 是唯一的教学序列状态 owner：

- 构造时选择第一条 plan step 并创建 observer；
- `SelectStep`、`TrySelectPrevious`、`TrySelectNext` 都先销毁 / Reset 旧 observer，再 prime 新
  observer；
- `Observe`在未完成时调用当前 observer；
- 第一次 Completed 设置完成状态、启动计时并产生单次 completion token；
- `TickAutoAdvance`只推进时间和步骤，不创建 UI；
- 最后一步保持完成；
- manual navigation 有界且不循环；
- 所有公开给 Host 的状态使用不可变 snapshot；
- 初始化后的 steady observation / countdown 目标为 `NoAlloc`。

`autoAdvanceSeconds == 0`时只标记“下一 coordinator tick 前进”，不在 observer 调用栈中立刻
切换，避免释放当前 observer 时重入。

### 6.5 Snapshot Mapper 与 Host

`CharacterTestGuidanceSnapshotMapper`负责：

- title、Action、Focus、Status 的本地化 request；
- template-owned glyph / tone；
- 接收 Host 已按当前 locale 解析并组合的标题与 Action prompt；
- 完成和 Warning Notice；
- 单调 Card revision 与稳定 scope / message ID。

Mapper 不扫描 Runtime event 或 damage rows，也不持有步骤推进计时。

`CharacterTestFairyGuiGuidanceHost`迁移后只负责：

- 接收已编译 plan / coordinator；
- 捕获现有只读 observation context；
- 调用 coordinator Observe / Tick；
- 在 coordinator snapshot、binding revision 或 locale revision 改变时，解析源文案/正式译文并
  动态组合步骤序号、当前 binding 与上一项 / 下一项提示，再调用 mapper；
- 调用现有 Presenter；
- surface 被清理后从当前 snapshot 恢复 Card；
- Dispose coordinator、mapper 和 Presenter。

迁移已经删除 `CharacterTestGuidanceTestId` 与 `CharacterTestTargetLockGuidanceMapper`；数组顺序
是唯一播放权威，不再保留第二套步骤枚举。新增模板必须进入 completion registry、Editor、
validator/compiler、observer factory 与 focused tests 的同一闭环。

## 7. 分阶段实施

### 7.0 当前进度快照（2026-08-13）

| 阶段 | 状态 | 已有证据 / 剩余工作 |
|---|---|---|
| P3.4a | Current | schema、空文档/空白步骤、六个创作预设、reference 六步、五类 typed condition、validator/compiler、稳定 module/catalog hash 与 focused tests 已落地 |
| P3.4b | Current | 独立顶层模块、`空白步骤 / 从预设新建`、三栏自由步骤编辑、条件切换、语义预览、Undo/Redo、dirty、validate、未知 type 修复与未知 schema 只读保护已落地 |
| P3.4c | Current | GameManager cache、Guidance-only hash、envelope reread/upsert、统一 Scene 保存、故障回滚与 Play/build freshness preflight 已验收 |
| P3.4d | Current | 独立 Coordinator、五类 typed observer、Snapshot Mapper 与瘦 Host 已从正式 cache plan 运行；手动导航、stale cursor、binding revision 与最终步语义有 focused tests |
| P3.4e | Current | 正式六步 payload/cache 已原子切换，旧枚举/Mapper/硬编码回退已删除，发布 FairyGUI package 与实际 Game 帧已验收 |

下方阶段门禁作为未来改动的回归合同保留，不再表示剩余实施任务。

### P3.4a — Authoring 与完成条件目录

交付：

- Authoring DTO、normalize、adapter、空白草稿、六预设与 reference 六步；
- 显式 completion registry 与五类 typed condition；
- validator、compiler、immutable plan；
- module source hash 与 completion catalog hash；
- focused EditMode tests。

阶段门禁：

- 尚不注册 Level Editor module；
- 尚不改 GameManager 或 Scene；
- 尚不改 Host；
- reference 六步 roundtrip 后语义、顺序和 hash 稳定；
- 未知 type/schema 与非法 payload fail closed。

### P3.4b — 独立 Level Editor 模块

交付：

- 注册`教学引导`独立 module；
- 有序步骤列表、空白步骤、六预设新建、复制、删除、启停和排序；
- 文案、Input Action、自动前进、完成条件切换和 typed 参数 Inspector；
- 语义预览、diagnostics、Undo/Redo、dirty 与 discard/reload；
- envelope Extract / Upsert，但尚不切 Runtime。

阶段门禁：

- 缺失模块不会自动创建或标脏；
- 空白或预设新建只产生一个 Undo，并选中新步骤；
- 非编辑交互不标脏；
- unknown completion type 可保真加载并由正式条件修复；unknown module schema 只读保真并阻止保存；
- 未完成草稿在根/步骤任意启停组合下都阻止 prepare，且不写 payload/cache/Scene。

### P3.4c — Scene Cache 与统一保存

交付：

- `CharacterTestGuidanceSceneCache`与 GameManager serialized field；
- cache compiler、source/catalog hash 校验；
- Guidance save participant；
- Editor Playable / Player build cache-freshness preflight；
- JSON、cache、Scene 的统一事务和恢复 tests。

阶段门禁：

- Runtime 尚不消费 cache；
- 多 participant 同文件保存不丢 payload；
- pre-commit failure 恢复 JSON，Scene 不提前保存；
- post-commit tail failure 按真实边界报告；
- 自洽但落后于磁盘 module 的 cache 被 preflight 阻断；
- cache 不包含时间戳、绝对路径或完整 envelope hash。

### P3.4d — Runtime Coordinator 与 Mapper 解耦

交付：

- cache loader、immutable plan startup；
- 五类 observer 与 sequence coordinator；
- snapshot mapper；
- Host 组合瘦身；
- 手动导航、binding revision、surface restore 和 stale cursor tests。

阶段门禁：

- 测试夹具可从内存 cache 运行六步；
- Mapper 不再直接判断 movement、camera、target lock 或 damage；
- observer 不写领域状态；
- steady Observe / countdown 不产生 managed allocation；
- 正式场景仍未写新 payload 时不得把该阶段描述为已切换。

### P3.4e — 原子切换与 Playable 收口

交付：

- 正式 CharacterTest envelope 的六步 payload；
- 匹配的 CharacterTest Scene cache；
- Runtime composition 正式启用 cache chain；
- 删除旧硬编码六步和兼容回退；
- EditMode、PlayMode、Game 帧与文档状态校准。

阶段门禁：

- payload、cache、Runtime 消费和旧实现删除必须同一提交；
- 缺失 / stale / unknown 数据显式禁用；
- 现有 FairyGUI package 视觉测试继续通过；
- 实际 Game 中完成六步，并确认手动上一项 / 下一项、自动前进与 binding refresh；
- 证据完成后只把两份文档的 `Implementation status` 改为 Current；文件 `Status: Design`
  继续表达文档类型。

## 8. 自动测试矩阵

### 8.1 Authoring / compiler

- 当前正式六步 / reference fixture 的 JSON roundtrip、normalize 幂等和 stable hash；
- 根禁用 + 空步骤合法，根启用 + 零启用步骤非法；
- missing module 与空文档、空白步骤、六预设创建分离；
- duplicate / empty stable ID；
- 非法 Input Action 格式与未知 action；
- 六项简中源文案缺失、stable ID 非法，或任一 Key 不等于系统确定性映射；
- 正式 TSV 覆盖当前正式六步全部 Key；其 `zh-CN` 源快照与 Authoring fallback 一致，`en-US` 非空；
- unknown completion type、unknown schema、null / non-object payload、unknown field；
- 五类条件参数边界；
- disabled step 不进入 plan，但保留在 Authoring roundtrip；
- 无关 level module 改动不改变 Guidance source hash；
- condition 合同变化会改变 catalog hash；创作预设文字变化不会伪装成 Runtime 条件目录变化。

### 8.2 Editor

- 空白步骤生成唯一 Stable ID 和六个系统 Key，UI 占位不写 JSON；
- 六预设生成正确正文、Input Action、自动前进与 typed 参数；锁定/取消锁定参数相反；
- 新建插入当前选择之后、无选择时追加；新建、复制、删除、启停、排序和字段编辑各一个 Undo；
- 完成条件五类互换只重置 completion schema/payload；Undo/Redo 恢复旧类型和参数；
- Undo / Redo 恢复值、顺序、选择和 dirty state；
- 复制 stable ID 冲突递增；
- 选择、搜索、预览状态、foldout、滚动不产生脏状态；
- 缺失模块打开不标脏；
- unknown completion type 保留原 payload 且可切换修复；unknown module schema 保持只读；
- 未完成草稿在根关闭/步骤关闭/步骤开启时均阻止 prepare，payload/cache/Scene 不变；
- 等待、完成和 TargetLock 拒绝语义预览；
- 预览切换简中 / 英文不产生脏状态；缺译与源文案变更分别显示稳定 warning；
- 标准与窄窗口无关键字段裁切，人工视觉验收不能由几何测试替代。

### 8.3 保存与 cache

- Guidance upsert 保留 world objects、characters、triggers、Genesis 和未知 module；
- 两个 participant 顺序写同一路径后 payload 均存在；
- 任一 participant prepare 失败时零写入；
- Guidance write 后后续 participant 失败时恢复 JSON snapshot 与原 Guidance cache；
- 首次 Scene save 失败时恢复 JSON / cache 并保留 dirty；post-commit tail failure 按 coordinator
  当前边界报告，不伪造已回滚；
- success / repeated success / pre-commit failure / tail failure 状态机；
- cache module schema、cache schema、source hash 和 catalog hash mismatch；
- Editor Playable / build preflight 对“磁盘 module 已更新、Scene cache 仍自洽但较旧”的
  `GUIDANCE_CACHE_SOURCE_STALE` 阻断；
- cache canonical JSON 只含 Guidance module；
- cache 不包含时间戳、绝对路径或 UI 状态。

### 8.4 Runtime coordinator

- movement 必须先达距离，再满足停步和可选 Grounded；
- jump 必须在选择步骤后先 airborne 再 grounded；
- camera observer 忽略 prime 前的 stale event；
- lock / unlock observer 忽略 stale event，只接受期望状态；
- rejected 只产生 Warning，不完成；
- attack 只接受新的本地玩家、其他目标、prefix 匹配、damage 达标和 HP 下降 row；
- 同一模板重复两次具有独立游标；
- disabled step 被跳过；
- manual previous / next 有界，不循环，不重播 stale completion；
- completion Notice 只产生一次；
- `0` 秒在下一 coordinator tick 前进；正值按配置倒计时；
- 最后一条保持完成；
- Session 重建回到第一条启用步骤；
- binding revision 只更新当前 Card，不重置 observer；
- locale revision 重新投影当前标题、Action、Focus、Status 与 Notice，不改变步骤或完成状态；
- missing / invalid / stale cache 不显示 Guidance，并返回 typed diagnostic；
- steady Observe / Tick allocation 为 0 B。

### 8.5 PlayMode 与现有 UI 回归

- 正式 `characterTest.guidance` payload 能完成全部六步；
- 正式 Scene 运行中切换 `zh-CN` / `en-US`，当前步骤与完成状态不变且 Card 原位重投影；
- `[` / `]` 或对应 rebind 后 Action 保持手动导航；
- 自动前进顺序与正式 Authoring 一致；
- 目标锁定拒绝、锁定和取消 Notice 不互相残留；
- surface suppress 后按当前 coordinator snapshot 恢复 Card；
- `PlayerGuidancePresentationPlayModeTests` 继续从正式发布 package 渲染；
- `MxFramework.NoEngineTests/UI.Guidance.Tests/` 继续通过；
- 不新增第二套 player-facing UI Toolkit View。

## 9. 手工验收

### 9.1 Level Editor

1. 打开 `MxFramework > Level Editor`，选择`教学引导`。
2. 缺失模块时确认根关闭、零步骤、只显示`空白步骤 / 从预设新建`且不标脏。
3. 检查预设菜单只有六个玩家意图，不再出现五个技术 Type ID；分别创建锁定、取消锁定和攻击。
4. 新建空白步骤，确认占位、唯一 Stable ID、系统 Key、根开关不自动改变，未补全时保存被阻止。
5. 新建、复制、删除、启停和移动步骤，逐项执行 Undo / Redo。
6. 修改文案、Input Action、自动前进和条件参数，确认每项只产生一个 Undo。
7. 在五类完成条件间切换，确认其他作者字段保持、参数重置、预览回到等待且 Undo/Redo 精确。
8. 切换搜索、选择、foldout 和预览状态，确认不产生未保存状态。
9. 检查等待、完成和目标锁定拒绝语义预览，并确认预览明确标为非 FairyGUI 实景。
10. 制造重复 ID、空文案和非法参数，确认诊断定位并阻止保存。
11. 点击一次全局保存，确认 JSON、Guidance cache 和 Scene 同时提交。
12. 关闭重开后确认数据、顺序、cache hash 与保存 baseline 一致。

### 9.2 Game 帧

1. 从正式 CharacterTest Scene 启动，不使用 Development Preview。
2. 确认第一条来自正式 cache；删除或破坏 cache 的测试场景必须显式禁用而不是出现旧六步。
3. 依次完成移动、跳跃、相机、锁定、取消锁定和普通攻击。
4. 确认前五项完成后等待约 2 秒进入下一项，最后一项保持完成。
5. 在每项前制造历史事件 / 历史伤害，确认不会自动完成。
6. 使用手动上一项 / 下一项往返，确认 observer 重新 prime 且边界不循环。
7. 修改 input binding 后确认当前 Card 刷新；不宣称跟随最近活动设备。
8. 打开会 suppress HUD 的 surface 再关闭，确认按当前步骤恢复 Card。
9. 获取实际 Game 渲染帧，检查 FairyGUI 文案预算、重叠、Notice 和完成状态。

## 10. 验证顺序与完成记录

普通 C# 切片按仓库默认先执行 Unity Editor refresh / compile、等待 Editor ready 并检查
Console error。只有阶段明确要求时再运行对应 focused tests。P3.4e 的正式 Runtime 切换至少
需要：

1. Unity refresh / compile / domain reload / Editor ready，并分别记录 C# compilation errors 与
   全局 Console errors；
2. P3.4 Authoring、Editor、save/cache 和 coordinator focused EditMode tests；
3. Guidance noEngine tests；
4. Player Guidance 发布包 PlayMode；
5. 正式 CharacterTest 六步 PlayMode / Playable；
6. 标准与窄窗口 Level Editor 实际视觉检查；
7. 实际 Game 渲染帧；
8. `Tools/Harness/check_code_standards.py`、文档健康、链接和 diff hygiene；
9. 将分支、提交、测试数量、失败 / 例外和证据路径写入当前阶段完成记录。

不得用以下证据替代正式完成：

- 仅 JSON 单元测试替代 Level Editor 保存重开；
- 仅 cache 字段存在替代 Runtime 实际消费；
- 仅语义预览替代 FairyGUI package 或实际 Game 帧；
- 仅测试源码存在替代当前提交的执行结果；
- 仅旧硬编码六步通过替代新 Authoring 链路；
- 仅 Harness / Development Preview 替代正式 CharacterTest Playable。

## 11. 文档状态收口

P3.4a～P3.4e 已在 2026-08-13 收口。父设计继续使用 `Status: Design` 表示文档类型，父设计与
本指南均使用 `Implementation status: Current` 表示实现状态。收口同步项为：

- 将父设计和本指南校准为 Current；
- 更新 `Docs/README.md` 和 `Docs/PROJECT_INDEX.md` 的实现状态；
- 更新 `Docs/LEVEL_EDITOR_CAPABILITY_REUSE_MATRIX.md` 为已接入；
- `Docs/Interfaces/Editor.md` 保持不变，因为 P3.4 没有新增框架公共 Editor seam；
- 在[正式玩家引导提示 UI 设计](../PLAYER_GUIDANCE_PROMPT_UI_DESIGN.md)更新当前生产链与验证日期；
- 更新 `Docs/QUALITY_GATE.md` 的实际测试入口；
- 不把 CharacterTest 项目私有类型列为框架公共 API。

### 11.1 2026-08-13 完成证据

| 门禁 | 结果 |
|---|---|
| Unity refresh / compile / domain reload / Editor ready / Console | refresh、domain reload 与 Editor ready 完成，C# compilation error 0；全局 Console 保留一条与 P3.4 无关的既有 VFX stale-evidence error，未误报为零红项 |
| Authoring / Editor focused EditMode | 29/29 通过，覆盖空文档、空白草稿、六预设、插入顺序、条件切换与 Undo/Redo、unknown completion 修复、unknown schema 只读、三种启停组合保存阻断、roundtrip/golden hash、系统 Key、正式 TSV 中英条目、共享 envelope、cache freshness/preflight 与正式 Scene cache |
| Sequence coordinator focused EditMode | 9/9 通过，覆盖五类 observer、stale fact、手动导航、零延迟与最终步保持 |
| 正式 Scene cache / locale revision | 1/1 通过；正式六步完成链及 `zh-CN` / `en-US` 原位重投影均不改变当前步骤与完成状态 |
| Guidance noEngine | `UI guidance noEngine tests passed.` |
| 发布 FairyGUI package PlayMode | 1/1 通过，渲染 Card 与 Success/Warning/Error Notice variants |
| 正式运行帧 | 从 `CharacterTestScene` 正常启动并关闭主菜单后采集；Card 来自正式 cache 第一条 `basic-movement`，不是 Development Preview |
| Level Editor 实际视觉 | 1280×748 标准窗口与 980×720 窄窗口均完成检查：顶部及步骤栏的`空白步骤 / 从预设新建`未溢出；预设菜单实际只列六个玩家意图；完成条件下拉实际只列五类正式条件；空白草稿显示`未命名步骤 / 未选择完成条件`与逐项错误诊断。视觉试建均已 Undo，窗口恢复 6 步、已保存，未执行保存 |

正式 Scene 启动日志仍报告一个与 P3.4 无关的既有 Gameplay Trigger cache source-hash mismatch；
Guidance cache、Host 配置与六步 typed plan 验收均成功，该外部诊断不被隐去，也不由 Guidance
模块越权修复。

在补充生产 Host authored 顺序导航和 binding revision 重投影验收后的最终 Unity 复编译中，
脚本刷新、编译、domain reload 与 Editor ready 再次完成，C# compilation error 为 `0`；全局
Console 同时报告既有的
`Published VFX consumer contract evidence is stale: vfx.magic.fireball_burst.charge`。它不来自
P3.4 变更，且修复会修改独立 VFX 发布权威，因此本次没有越权刷新 VFX manifest / projection。
这条记录表示 P3.4 专项门禁已通过，但当前 checkout 的仓库级“Console 绝对零红项”复验仍有
该外部阻断，不能把两种结论混写。
