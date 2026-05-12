# Combat Authoring / Gizmo Tool Design

> 状态：Reviewed
> 日期：2026-05-08
> 关联设计：`Docs/COMBAT_ANIMATION_PHYSICS.md`
> 关联 Epic：`Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md`

## 1. 目标

Combat Authoring / Gizmo 工具的目标是让动作战斗数据可以在 Unity Editor 中直观创作、预览、校验和导出，同时保持 Runtime Core 的确定性和纯净边界。

本工具不是最终玩家向 Mod Editor，也不是权威战斗逻辑。它是开发者桥接工具：

- 用 Scene View Gizmo / Overlay / Handles 编辑与预览 hitbox、hurtbox、weapon trace、actor marker 和 action frame。
- 用 EditorWindow 统一管理 Combat authoring asset、场景绑定、timeline scrubber、query explain 和 validation report。
- 将可视化编辑结果保存为 ScriptableObject authoring asset，再导出为 Runtime 只读数据或 JSON authoring 包。
- 与 Runtime Showcase 共享同一批 runtime 数据契约，避免 Editor 工具手写另一套命中逻辑。
- 为后续外部 Authoring Editor / CLI 留出 schema、JSON、validation report 和 preview patch 边界。

制作人验收口径：

- 非工程人员应能通过窗口、Scene View 和提示理解当前 action 数据是否正确。
- 任何复杂实现都要在入口层呈现为清晰按钮、可定位错误、可撤销编辑和可复制报告。
- 手动测试者不应依赖 Console 才能判断 trace、query、hit resolve 的结果。
- 场景不应因为测试工具长期挂载大量组件；默认走 asset 配置、临时预览和运行时动态创建。

## 2. 非目标和硬边界

- 不引入 WGame 特化角色、技能、元素、Buff 或真实业务配置。
- Runtime Core 不引用 `UnityEditor`。
- Combat Core / Physics / Animation / Hit / Diagnostics 不因为本工具新增 UnityEditor 依赖。
- Editor 工具不是权威逻辑，所有 query、trace、hit resolve 预览必须调用 Combat Runtime 公开 API 或明确标记为 display-only。
- Unity Editor 不是最终主创工具；完整玩家 / 策划 / AI 协作创作入口仍应走外部 Authoring Editor 规划。
- 第一版不做复杂骨骼动画烘焙、GraphView 节点编辑器、多人协作、真实 Mod 打包平台。

## 3. 用户故事

开发者打开 `MxFramework > Combat > Combat Authoring` 后，应能完成以下流程：

1. 选择或创建一个 Combat Action Authoring Asset。
2. 在同一个窗口中查看 action id、总帧数、startup / active / recovery、事件、窗口、trace、hitbox、hurtbox。
3. 在 Scene View 中看到当前帧的 actor、body、collider、trace、命中点、miss 点和 query 方向。
4. 拖动帧 scrubber，Scene View 立即切换到对应固定帧。
5. 用 Handles 移动 / 缩放 / 旋转 shape，并看到数值输入框同步更新。
6. 点击 Validate，得到可定位到 asset、track、frame、shape、scene binding 的中文错误提示。
7. 点击 Preview Query / Resolve，看到 generated query、candidate、hit result、过滤原因和 world hash。
8. 点击 Export Runtime Data，生成 Runtime 只读数据快照或 JSON 包。
9. 在 Play Mode Showcase 中加载同一份预览数据，继续用 HUD / Scene feedback 手动测试。

## 4. 入口、菜单和 UI 性能

建议入口：

```text
MxFramework > Combat > Combat Authoring
MxFramework > Combat > Open Combat Test Scene
MxFramework > Combat > Validate Selected Combat Asset
MxFramework > Combat > Export Runtime Combat Data
MxFramework > Combat > Toggle Combat Gizmos
```

`Combat Authoring Window` 使用 UI Toolkit：

- 顶部上下文栏：当前 asset、scene binding、frame、validation 状态、play/edit 模式。
- 左侧导航：Action、Actors、Bodies、Hitboxes、Hurtboxes、WeaponTrace、Events、Validation、Export。
- 中间主工作区：timeline、表格、shape inspector、frame details。
- 右侧解释面板：selected shape、query explain、hit resolve explain、snapshot、hash。
- 底部问题抽屉：全局 validation issues，可复制报告。

UI 文案默认中文；类型名、字段名、asset key、日志 key 保留英文。

Authoring 编辑交互原则：

- 能不打字就不打字。高频编辑优先使用 Scene View handle、timeline range 拖拽、slider、stepper、toggle、下拉选择、对象拾取和 quick action。
- 文本输入只用于不可避免的稳定 ID、搜索、路径和备注；所有文本输入必须有占位提示、格式说明、实时校验、非法字符限制和冲突提示。
- 枚举 / 模式字段不得裸露为自由文本；使用下拉、分段按钮或 toolbar toggle，并显示中文名称和英文 key。
- 数值字段不得只靠手打；必须提供拖拽、步进或滑条入口，并按数据规则 clamp 到合法范围。fixed raw 字段必须显示单位换算说明。
- 引用字段优先通过 Project ObjectField、Scene pick、Relink Selected、Create Missing Marker 等操作完成；手填 markerId 只能作为高级补充，并要检测缺失和重复。
- 帧范围编辑优先通过 timeline range handle 完成，字段输入作为精修补充；编辑时必须即时显示范围 clamp、重叠 warning 和 validation 状态。
- 所有编辑入口都要支持 Undo、dirty 标记、中文 tooltip、错误定位和建议修复；不能只依赖 Console 或裸 PropertyField。

UI Toolkit 实现约束：

- 高频拖拽、播放和 scrubber 更新不得通过反复插入 / 删除 `VisualElement` 或频繁改 USS class 完成。
- 动画、playhead、拖拽反馈优先使用 transform / translate；避免在播放循环里反复改 `style.left/top/width/height` 触发布局重算。
- Timeline、issue 列表、query 列表和 event log 使用虚拟化列表或元素池；长列表不得一次性创建全部行。
- 批量状态刷新应合并到单次 UI update；不要在每个字段变化时立即重建整棵 visual tree。
- `CreateGUI` / 初始化阶段负责创建结构，运行期只更新绑定数据和少量显示状态。
- 所有字段编辑优先绑定 `SerializedObject` / `SerializedProperty`，保证 Inspector、Undo 和 dirty 状态一致。

## 5. Scene View Gizmo / Overlay / Handles

### 5.1 Overlay

Scene View Overlay 提供轻量操作：

- Asset：当前 Combat Action / Scene Binding。
- Frame：scrubber、上一帧、下一帧、播放、暂停、重置。
- Mode：Select、Move Actor、Edit Hitbox、Edit Hurtbox、Edit Trace、Preview Query。
- Visibility：Actor、Body、Hitbox、Hurtbox、Trace、Candidate、Resolve、Labels。
- Snap：frame snap、grid snap、fixed precision snap。
- Validation：当前选择是否有错误。

Overlay 不承载复杂编辑表单，复杂字段回到 EditorWindow。

实现建议：

- 使用 Unity `Overlay` 基类和 `OverlayAttribute` 注册到 Scene View。
- 工具模式使用 `EditorToolbarToggle`；一次只能有一个主编辑模式处于 active。
- 常用命令使用 `EditorToolbarButton`，例如 validate、reset frame、copy report、toggle visibility。
- 在 `CreatePanelContent` 创建 VisualElement 层次，不在 IMGUI / Scene GUI 循环中重复创建控件。
- 如果后续需要让自定义 EditorWindow 支持 overlay，应显式接入 `ISupportOverlays`，并保持 Scene View overlay 是第一入口。

### 5.2 Gizmo 颜色和语义

```text
Actor / Body       青色
Hurtbox            蓝色
Hitbox             橙色
WeaponTrace        黄色到红色渐变
Query miss         灰蓝色
Query hit          红色
Accepted resolve   绿色脉冲
Rejected resolve   紫色或灰色，并显示原因
Invalid authoring  红色虚线
Selected           白色外框
```

### 5.3 Handles

必须支持：

- Capsule：端点拖拽、半径拖拽、中心整体移动。
- Sphere：中心移动、半径拖拽。
- AABB / OBB：中心、half extents、旋转。
- Sector：半径、角度、朝向。
- WeaponTrace：root / tip prev / now 四点拖拽，半径拖拽，substep 预览。
- Actor marker：位置和朝向拖拽。

Handle 修改先进入 `Undo` 支持的 authoring asset 或 scene binding draft，不直接改 Runtime 状态。

Handles 实现约束：

- 每类 shape 使用独立 handle 类，统一实现 `ICombatShapeHandle` 或等价接口。
- Capsule handle 负责两个端点、中心和半径；Sphere handle 负责中心和半径；AABB / OBB handle 负责中心、half extents 和旋转；Sector handle 负责半径、角度和朝向；WeaponTrace handle 负责 root、tip prev、tip now、半径和 substep 预览。
- 绘制尺寸使用 `HandleUtility.GetHandleSize(position)` 保持屏幕尺度稳定。
- 颜色使用 `Handles.DrawingScope` 或等价作用域恢复，避免污染其它 Scene View 绘制。
- 需要 2D 辅助面板时，可在 Scene GUI 中使用 `Handles.BeginGUI` / `Handles.EndGUI`，但输入表单仍以 EditorWindow 为主。
- 编辑必须写回本地坐标的 authoring 数据，不能写入 Runtime preview state。

Undo / Redo 规则：

- 数值字段和 handle 拖拽在修改前调用 `Undo.RecordObject(target, "...")`。
- 修改后调用 `EditorUtility.SetDirty(target)` 或通过 `SerializedObject.ApplyModifiedProperties` 触发 dirty。
- 拖拽类操作使用 change check 包裹，只有值变化时才记录 Undo。
- 批量偏移、复制、粘贴、删除和 quick action 都要进入同一次可读的 Undo 操作名。

## 6. Action Timeline 编辑和预览

Timeline 必须以 fixed frame 为唯一权威单位：

- 总帧数。
- Startup / Active / Recovery。
- Cancel / Invincible / SuperArmor / Parry / Custom state window。
- Frame event。
- Hitbox / Hurtbox track。
- WeaponTrace track。
- Root motion / marker sample track。

交互要求：

- Scrubber 拖动时更新当前 frame preview。
- Step 前进后只改变 preview frame，不隐式写入数据。
- Playback 可选择 editor preview speed，但显示当前 fixed frame。
- Range 拖动必须自动 clamp 到 `[0, TotalFrames)`。
- 重叠窗口应给出 warning 或 error，取决于规则是否允许。
- 关键帧复制、粘贴、镜像、平移、批量偏移必须进入 Undo。

首版可使用 UI Toolkit 自绘 timeline，不急于引入 GraphView。

Timeline 性能规则：

- Timeline 自绘或元素池化，不因播放帧推进重建所有 frame cell。
- Playhead、range selection 和 hover marker 使用可复用元素或 custom paint。
- 缩放和滚动只改变 viewport / transform / 绘制参数，不重新分配大量 VisualElement。
- 复制、粘贴、批量偏移必须先改 authoring asset，再由绑定刷新 UI；不要让 UI 状态成为权威数据源。

## 7. Hitbox / Hurtbox / WeaponTrace Authoring

### 7.1 Hitbox / Hurtbox

数据应表达：

- `TrackId`
- `ShapeKind`
- `FrameRange`
- `BoneId` 或 `MarkerId`
- `LocalCenter`
- `LocalRotation`
- `Size / Radius / Height / Angle`
- `Layer / Mask`
- `Priority`
- `State tags`

每个 shape 在 Inspector 中必须显示：

- 中文说明。
- 原始字段 key。
- 单位。
- 是否影响 Runtime 权威结果。
- 默认值来源。
- 错误和修复建议。

### 7.2 WeaponTrace

WeaponTrace authoring 支持：

- root / tip socket 或 marker。
- prev / now sample 预览。
- blade capsule。
- tip sweep substeps。
- radius。
- target mask。
- once-per-target key 预览。

Scene View 中必须显示当前帧 trace 和上一帧 trace 的关系，避免只看一条线无法判断高速挥砍覆盖。

### 7.3 Unity Physics Compare

可以提供对照模式，但必须清楚标记：

```text
Combat Physics = Authority
Unity Physics = Compare Only
```

对照结果只用于发现 authoring 偏差，不写回 Runtime。

## 8. Fixed-frame Playback

Playback 面板：

- `Frame`
- `Local Frame`
- `Action Phase`
- `Step`
- `Play/Pause`
- `Reset`
- `Loop`
- `Playback Speed`
- `Hash`
- `Generated Queries`
- `Hit Candidates`
- `Resolve Results`

所有播放都通过 `CombatFrameClock` 或等价 runtime preview context 推进。Editor 可以用秒做显示插值，但不能用秒重算权威帧。

## 9. Marker / Actor / Body / Collider Scene Binding

Scene binding 不应要求测试场景预挂大量 MonoBehaviour。建议使用一个可选的 editor-only 或 authoring asset：

```text
CombatSceneBindingAsset
  SceneGuid
  BindingProfileId
  Actors[]
    EntityId
    DisplayName
    MarkerId
    DefaultPosition
    DefaultRotation
    BodyId
    Colliders[]
```

Scene View 预览逻辑：

- Edit Mode 可从 binding asset 生成临时 preview objects。
- Preview objects 标记为 editor-only，不作为 Runtime 权威数据。
- Play Mode 由 Runtime Showcase 或 Bootstrap 根据 binding asset 动态创建 runner、input controller 和 visuals。
- 找不到 marker 时显示可修复错误：`Create Preview Markers`、`Relink Selected Transform`、`Use Asset Default`。

必须避免把 `SceneTargetConfig`、`RuntimeVerticalSliceRunner` 或 Combat runner 强制挂在场景里作为唯一入口。

稳定性要求：

- 绑定、预览对象、查询结果和命中结果在 UI 展示前必须按显式 key 排序，例如 entity id、body id、collider id、track id、frame、source order。
- 不依赖 Unity API 的默认返回顺序；任何 `Find*` / scene object query 的结果都必须转换为稳定排序列表。
- Quick action 只修改 scene binding draft 或 authoring asset；修改真实 scene transform 前必须有明确按钮和 Undo。

## 10. Query / HitResolve 可解释面板

解释面板必须让测试者不看 Console 也能知道发生了什么：

```text
Frame 17 / Action 400001 / Trace 7
Generated Queries: 5
Candidate Hits: 2
Selected Result: AcceptedDamage
Reason Chain:
  1. Physics overlap capsule hit target body=2 collider=2 distance=0
  2. Target alive: pass
  3. Invincible: not active
  4. Hit once key: not consumed
  5. Damage: attack 120 - defense 10 = 110
Hash: 247C727E882EC099
```

必须支持：

- query 列表。
- candidate 列表。
- hit once / duplicate 解释。
- target state filter。
- damage / stagger / knockback 摘要。
- replay input。
- snapshot / hash。
- copy report。

Console 只能输出极少量入口级信息，不能作为主要反馈。

实现约束：

- Explain 面板以树状层级展示：generated queries -> candidates -> resolve result -> reason chain。
- 面板数据来自 runtime preview context 调用 Combat Runtime API 后生成的 report DTO，不在 UI 层重新计算命中规则。
- Copy report 输出同时包含人可读文本和稳定 key，便于粘贴到任务记录或回归测试。
- Query / Resolve 结果显示前使用 Combat Runtime 的稳定排序规则；如果 runtime API 暂未提供排序 key，M10F 必须补齐。

## 11. 数据资产和导出格式

### 11.1 ScriptableObject Authoring Asset

Unity 内编辑使用 ScriptableObject，因为它天然支持 Inspector、Undo、引用和 SVN diff：

```text
CombatActionAuthoringAsset
CombatSceneBindingAsset
CombatAuthoringValidationProfile
CombatAuthoringExportProfile
```

这些 asset 属于 Editor / Authoring Bridge 数据，不直接进入 Runtime Core。

### 11.2 Runtime Pure Data

导出后的 Runtime 数据必须是纯 C# 数据或序列化快照：

```text
CombatActionTimeline
CombatActionWindow[]
CombatActionFrameEvent[]
WeaponTraceFrame[]
CombatPhysicsBody[]
CombatPhysicsAabbCollider[]
```

Runtime 加载时不需要 UnityEditor，也不要求读取 ScriptableObject authoring schema。

### 11.3 JSON / External Authoring Package

为了后续外部 Authoring Editor / CLI，必须定义 JSON 包边界：

```text
CombatAuthoringPackage/
  manifest.json
  schema/
    combat_authoring.schema.json
  actions/
    action_400001.json
  scene_bindings/
    combat_animation_physics_test.json
  reports/
    validation_report.json
    validation_report.txt
```

JSON 字段名使用稳定英文 key；可附带中文 `displayName` 和 `description`，但不能作为主键。

`manifest.json` 至少包含：

```text
packageId
version
schema
schemaVersion
createdAt
toolVersion
sourceAssetGuid
contentHash
```

兼容规则：

- JSON schema 字段名和类型一旦进入 Runtime export，默认只增不改。
- 破坏性变更必须提升 `schemaVersion`，并提供 migration 或明确拒绝加载。
- 导出报告必须记录 authoring asset hash、runtime data hash 和 JSON package hash，用于比对 Editor 与 Play Mode 结果。

## 12. 依赖方向

建议依赖：

```text
MxFramework.Combat
  <- MxFramework.Combat.Unity
  <- MxFramework.Combat.Authoring
  <- MxFramework.Combat.Editor
  <- MxFramework.Demo
```

说明：

- `MxFramework.Combat`：现有 Core / Physics / Animation / Hit / Diagnostics，纯 Runtime。
- `MxFramework.Combat.Unity`：UnityEngine 适配，如 Vector3 <-> FixVector3、LineRenderer / Gizmo display data。
- `MxFramework.Combat.Authoring`：可序列化 authoring model、validation、export，不引用 UnityEditor；是否引用 UnityEngine 需按 asset 形态拆分。
- `MxFramework.Combat.Editor`：EditorWindow、Overlay、Handles、asset creation、scene preview，只在 Editor asmdef。
- `MxFramework.Demo`：Showcase 使用导出的 Runtime 数据，不反向依赖 Editor。

如果第一版拆 asmdef 成本过高，可以先新增 Editor asmdef，但必须用 asmdef references 保证 Runtime 不反向引用 Editor。

校验逻辑应尽量放在 `MxFramework.Combat.Authoring`，由 EditorWindow、CLI 和测试共同调用；窗体层只负责展示 report 和触发 quick action。

## 13. Validation / 错误提示 / 容错

Validation 分层：

- Asset structure：id、frame、range、空引用、重复 key。
- Timeline rule：阶段顺序、窗口越界、事件越界、非法重叠。
- Shape rule：负半径、零长度 capsule、非法 sector angle、mask 为空。
- Binding rule：entity/body/collider id 重复，marker 丢失，scene binding 失效。
- Runtime preview：query 为空、没有 target、hit resolve 全过滤、hash 异常。
- Export rule：runtime snapshot 与 authoring asset 不一致。

错误格式必须包含：

```text
Severity
SourceAsset
Section
TrackId
Frame / FrameRange
Field
Message
SuggestedFix
QuickAction
```

容错要求：

- 默认保存 Draft，不直接覆盖已导出 runtime snapshot。
- 所有编辑支持 Undo。
- 删除默认进入 disabled / hidden 状态，确认后才物理删除。
- Export 前必须 validation 无 error；warning 可导出但报告必须包含。
- Play Mode 预览加载失败时禁用该 asset，并保留错误报告，不让场景刷屏。

Quick Action 设计：

```text
CombatAuthoringQuickAction
  Id
  Label
  SeverityAllowed
  TargetAssetGuid
  TargetPath
  CanExecute(context)
  Execute(context)
```

常见 action：

- `Select Asset`
- `Reveal In Project`
- `Focus Scene Marker`
- `Create Preview Marker`
- `Relink Selected Transform`
- `Clamp Frame Range`
- `Disable Invalid Shape`
- `Copy Issue Report`

Quick action 必须可预测、可撤销；不能静默改 Runtime 数据。

## 14. Runtime Showcase 衔接

当前 `RuntimeCombatShowcaseRunner` 已经提供：

- Player / Enemy marker 同步到 `CombatPhysicsWorld`。
- WeaponTrace capsule query。
- HitResolve。
- Snapshot / replay hash。
- HUD 和 Scene feedback。

Authoring/Gizmo 工具应复用这条链路：

- Editor 预览使用同一份 query builder 和 hit resolve。
- Scene binding asset 可被 Showcase Bootstrap 读取。
- Showcase HUD 可以显示当前 authoring asset id、frame、shape、trace、validation 状态。
- 手动测试命令继续保留：移动、攻击、探测、trace、resolve、snapshot、replay。
- Gizmo 预览结果和 Play Mode 结果必须能用同一份 validation report 对照。

Editor preview context：

- 使用 `CombatFrameClock` 或等价固定帧状态推进 preview。
- 使用 runtime 的 query builder、physics world、hit resolve 和 diagnostics API。
- UI 播放速度只影响 `EditorApplication.update` 推进频率，不参与权威帧计算。
- Preview context 输出 display DTO，例如 gizmo lines、shape outlines、query rows、resolve rows、labels；Scene View 和 HUD 只消费这些 DTO。
- Editor 与 Play Mode 对同一 authoring export 的 query / resolve 摘要应能通过 hash 或 report key 对齐。

## 15. 文件和 asmdef 规划

建议文件范围：

```text
Assets/Scripts/MxFramework/Combat.Authoring/
  CombatActionAuthoringAsset.cs
  CombatSceneBindingAsset.cs
  CombatAuthoringValidator.cs
  CombatAuthoringQuickAction.cs
  CombatAuthoringExportContext.cs
  CombatAuthoringReport.cs
  CombatAuthoringJsonSchema.cs
  CombatAuthoringPreviewContext.cs

Assets/Scripts/MxFramework/Combat.Editor/
  CombatAuthoringWindow.cs
  CombatAuthoringWindow.uxml
  CombatAuthoringWindow.uss
  CombatSceneOverlay.cs
  CombatGizmoDrawer.cs
  CombatShapeHandleUtility.cs
  Handles/CapsuleCombatShapeHandle.cs
  Handles/SphereCombatShapeHandle.cs
  Handles/SectorCombatShapeHandle.cs
  Handles/WeaponTraceCombatShapeHandle.cs
  CombatAuthoringMenu.cs

Assets/Scripts/MxFramework/Tests/Combat/Authoring/
  CombatAuthoringValidatorTests.cs
  CombatAuthoringExportTests.cs
  CombatAuthoringJsonSchemaTests.cs
  CombatAuthoringPreviewContextTests.cs

Docs/Tasks/
  COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md
```

asmdef：

```text
MxFramework.Combat.Authoring
MxFramework.Combat.Editor
MxFramework.Combat.Authoring.Tests
```

其中 `MxFramework.Combat.Editor` 必须只在 Editor 平台启用。

## 16. M10 阶段拆分

每个阶段必须可独立验收、独立 SVN 提交。

### M10A：Design and Contracts

产物：

- 本设计文档。
- Authoring asset / runtime export / validation report 契约草案。
- Epic 下一步更新。

验收：

- 文档覆盖入口、Gizmo、Timeline、Shape、Playback、Binding、Explain、Data、Validation、Runtime Showcase、外部工具边界。
- 不改 Runtime 代码。

### M10B：Authoring Data Contract v0

产物：

- `CombatActionAuthoringAsset` 和 `CombatSceneBindingAsset` 最小结构。
- `CombatAuthoringReport` / issue model。
- `CombatAuthoringQuickAction` 契约。
- JSON manifest / schemaVersion 草案。
- Validator 只做结构校验。

验收：

- EditMode 测试覆盖重复 id、非法 frame range、负半径、marker 缺失。
- 测试覆盖稳定排序：乱序 scene binding / query rows 输出同序 report。
- Runtime asmdef 不引用 Editor。

### M10C：Combat Authoring Window v0

产物：

- `MxFramework > Combat > Combat Authoring`。
- UI Toolkit 窗口。
- asset 选择、基础字段、timeline 只读/轻编辑、validation report。

验收：

- Unity MCP 编译无错误。
- 打开窗口不会要求 Play Mode 或场景组件。
- 可复制 validation report。
- Timeline / issue list 使用虚拟化或元素池，不在 scrubber 拖动时重建整棵 UI。
- 字段修改走 `SerializedObject` / `SerializedProperty`，Undo 可恢复。

### M10D：Scene View Gizmo / Overlay v0

产物：

- Scene View Overlay。
- actor/body/collider/trace 可视化。
- frame scrubber 与 selected asset 同步。

验收：

- Edit Mode 可预览，不污染场景对象。
- Hide / visibility toggle 可控制每类显示。
- 分辨率和窗口缩放变化下 Overlay 不遮挡核心 Scene View 操作。
- Overlay 使用标准 toolbar button / toggle；`CreatePanelContent` 不在 Scene GUI 循环重建控件。
- Scene object query 结果按显式 key 排序后展示。

### M10E：Shape Handles Editing v0

产物：

- Capsule / Sphere / AABB / WeaponTrace handles。
- Undo / redo。
- fixed precision snap。

验收：

- 拖动 handle 后 authoring asset 数值变化正确。
- 非法 shape 立即显示错误，不写入 runtime snapshot。
- M10E 第一批优先完成 Capsule 和 Sphere；AABB / OBB / Sector / WeaponTrace 可按同一接口继续扩展。
- Handle 尺寸使用 `HandleUtility.GetHandleSize`，颜色绘制不污染其它 Scene View gizmo。
- 每次拖拽、批量偏移、删除都能 Undo / Redo。

### M10F：Preview Query and HitResolve Explain v0

产物：

- Editor 内生成 CombatPhysicsWorld preview。
- query list、candidate list、hit resolve reason chain。
- copy report。

验收：

- Editor 预览和 Runtime Showcase 对同一 asset 的 query / resolve 摘要一致。
- Console 不刷屏。
- Preview context 不在 UI 层重算命中规则，只调用 Combat Runtime API。
- Explain 面板以树状结构展示 query、candidate、result、reason chain。
- 高频预览使用预分配列表或简单空间哈希 / grid broadphase，避免大量 shape 时卡顿。

### M10G：Export Runtime Data and JSON v0

产物：

- ScriptableObject authoring asset 导出到 runtime pure data。
- JSON authoring package 草案。
- export report。

验收：

- 导出前 validation gate 生效。
- JSON key 稳定，不依赖中文显示名。
- `manifest.json` 包含 version、schema、schemaVersion、contentHash。
- 导出结果的 authoring hash、runtime data hash、JSON package hash 可比对。
- Runtime 加载不需要 UnityEditor。

### M10H：Showcase Binding Integration v0

产物：

- Runtime Showcase 可读取 scene binding / runtime action preview 数据。
- HUD 显示当前 authoring asset、frame、query、resolve explain。
- Play Mode 手测流程更新。

验收：

- 通过 Unity MCP 进入 Play Mode 并触发 trace / resolve。
- Scene feedback 与 HUD explain 一致。
- 不要求场景预挂 Combat runner。

### M10I：No-Typing Authoring UX Pass

目标：

- 把 Combat Authoring 从“可以改字段”推进到“主要靠拖动、选择和点击完成编辑”。
- 降低非工程人员理解和试错成本，避免裸文本输入导致 marker、frame、raw value 和 ID 错误。

范围：

- Timeline 支持拖动 range 起止帧，并自动 clamp 到 `[0, TotalFrames)`。
- Shape 详情面板中的 enum、frame range、marker 引用和 raw 数值改成选择 / 拖拽 / slider / stepper 为主，文本输入为辅。
- Marker 字段提供从 Binding markers 选择、Scene pick、Relink Selected 和 Create Missing Marker。
- TotalFrames、radiusRaw、heightRaw 等数值字段提供范围限制、单位说明和实时 validation。
- Action Id / Binding Profile Id 等文本字段保留输入，但必须限制字符、检测重复 / 空值，并显示中文提示。
- 所有新增交互必须保留 `SerializedObject` / `Undo` / dirty 语义。

验收：

- 创建 Hitbox / Hurtbox 后，可以不手打任何字段完成基本摆放和帧范围调整。
- 常用字段 hover 能看到中文说明，非法输入能在窗口内看到原因和修复建议。
- Scene View handle、timeline 和详情面板三者状态同步。
- Unity Console 无 error，Authoring EditMode tests 通过。

## 17. 测试和 Unity MCP 验收

每个实现阶段提交前至少执行：

```text
Tools/GitNexus/gitnexus.sh detect-changes
Unity MCP compile / console check
相关 EditMode tests
```

涉及 Scene View / Play Mode 的阶段还要执行：

- 打开 Combat test scene。
- 进入 Play Mode。
- 触发 step、trace、resolve、snapshot。
- 检查 Console 无 error。
- 检查 HUD / Gizmo 不重叠、不刷屏、不依赖 Console。

建议测试命名：

```text
CombatAuthoringValidatorTests
CombatAuthoringExportTests
CombatGizmoPreviewTests
CombatShowcaseAuthoringBindingTests
```

## 18. 风险和控制

| 风险 | 控制 |
| --- | --- |
| Editor 工具变成第二套 Runtime 逻辑 | 所有 preview 调用 Combat Runtime API；display-only 逻辑必须标记 |
| ScriptableObject 数据绑死 Unity | 同步定义 runtime pure data 和 JSON 包 |
| Scene View 工具污染测试场景 | preview objects editor-only，Play Mode 动态 bootstrap |
| Gizmo 信息过载 | visibility toggle、selected-only、LOD、颜色语义固定 |
| Validation 只有日志不可操作 | issue 必须包含 quick action 和 suggested fix |
| 外部 Authoring Editor 后续无法复用 | 保留 JSON schema、CLI validation、report bundle 边界 |
| Timeline UI 一次做太大 | 先做 UI Toolkit 自绘和 frame scrubber，GraphView 后置 |

## 19. 开工建议

下一步执行 `M10B: Authoring Data Contract v0`。先把数据资产、validation issue 和 export context 定下来，再做窗口和 Scene View 工具。这样可以保证后续 UI、Gizmo、Showcase 和外部工具都围绕同一份契约扩展。
