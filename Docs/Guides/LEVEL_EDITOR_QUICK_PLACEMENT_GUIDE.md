# Mx 关卡编辑器：快速布置指南

> Status: Current
>
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestWorldObjectLevelEditorModule.QuickPlacement.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestWorldObjectCopyNativeTests.cs`
>
> 阶段：第一阶段快速布置 + 第二阶段 P2.1-P2.4 Runtime Slice
>
> 适用范围：CharacterTest 世界对象与场景角色
>
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestWorldObjectLevelEditorModule.QuickPlacement.cs`

本指南只描述当前已经实现的快速布置、对象定义、Trigger Authoring 与 CharacterTest offline Runtime 检查。继续增加角色、AppFlow、Runtime AI Planner 或其他模块能力前，必须先按 [关卡编辑器能力复用矩阵](../LEVEL_EDITOR_CAPABILITY_REUSE_MATRIX.md) 确认运行时权威和已有 API，再为关卡编辑器补薄适配层。SceneView 的选取、表面落点和预览可以使用 Unity Editor API；正式玩法碰撞、运动和区域判定不能因此改用 Unity Physics 作为权威。

## 目标

2026-10-13 源码交付复核：复制仍在 Add/Normalize 替换、排序列表前保存规范化源对象引用，随后通过该引用复制属性；本次 QuickPlacement 仅合并局部变量声明的换行，不改变复制行为或验收结论。

第一阶段只解决一个高频闭环：选中对象，放到场景中，调整位置，必要时复制、撤销或删除。界面不按策划、地编、程序或美术拆分岗位，也不增加素材库和常驻工具面板。

推荐顺序是：

1. 放置
2. 配置
3. 校验
4. 应用

## 窗口往返

默认按 `B` 在 Scene 与关卡编辑器之间往返。当前在 Scene 时会聚焦关卡编辑器；当前在关卡编辑器时会回到最后使用的 Scene。编辑器尚未打开时，按 `B` 会直接打开并聚焦；从 Inspector、Hierarchy 等其他 Unity 窗口按下时，也会聚焦关卡编辑器。

该快捷键只切换窗口，不会重置当前模块、分类、列表选择、搜索、滚动位置或放置状态。需要改键时，可在 `Edit > Shortcuts` 中搜索 `MxFramework/Level Editor/Toggle Focus` 后重新绑定。

## Scene 右键操作

关卡编辑器打开时，在 Scene 中对已登记对象右键单击，会在鼠标位置显示快捷菜单。右键拖动仍然使用 Unity 原有的视角旋转，不会弹出菜单；空白处右键也保持 Unity 原行为。点到模型、碰撞体等子节点时，编辑器会向上找到对应的已登记对象，并在需要时自动切换到世界对象或场景角色模块。

菜单提供`保存全部`、`复制并放置`、`重新放置`、`对齐网格`、`仅移除登记`、`删除场景对象`和`打开关卡编辑器`。放置过程中不显示右键菜单；静态批次或存在外部引用时，相关命令会置灰并直接显示原因。删除仍使用确认弹窗，并继续遵守现有引用保护与 Undo/Redo 事务。

## 放置

世界对象的`对象定义`分类用于管理可放置内容。可以新建定义并绑定 Resource Catalog 中的 Prefab；场景对象复制统一在 Scene 中右键选择`复制并放置`，定义工作区不承担实例复制。绑定成功后，Scene 中看到的是实际 Prefab 网格预览，确认放置会创建保持 Prefab 关联的新实例；资源未发布或 ResourceKey 错误时会阻止放置，不会悄悄改用方块。只有定义明确选择`基础几何体`时才使用 Cube、Sphere 或 Capsule。

点击列表上方的`放置`会在按钮旁打开临时弹层。世界对象在弹层中选择类型和定义，场景角色选择控制来源；点击`开始放置`后弹层自动关闭，按钮变为`结束放置`。复制和重新放置会在 Scene 视图显示原对象的网格与材质预览；新建对象显示对应形状的实体预览。左键确认，`Esc` 或`结束放置`取消。取消不会创建或移动对象，也不会修改登记数据。

放置默认命中鼠标下的可见场景表面，并保持对象原有旋转，不沿表面法线倾斜。编辑器会优先按 Renderer 底部边界计算落地高度；没有可用 Renderer 时按 Transform Pivot 放置。

弹层底部的网格图标控制`网格吸附`，默认关闭。打开后使用 Unity 自身的移动吸附步长 `EditorSnapSettings.move`，只量化世界坐标 X/Z，Y 仍使用实际表面高度。场景标记图标只控制对象标签、范围和辅助轮廓，不会关闭放置预览或点击确认。两个开关保存在本机 EditorPrefs 中；网格开关不会修改 Unity 的全局吸附设置。

选中 AABB 区域并开启场景标记后，Scene 会同时显示半透明范围面、清晰边线和六个面手柄。直接拖动任一面即可调整区域中心与尺寸；拖动时半透明候选面会实时跟随，松开鼠标时才正式提交为一次 Unity Undo 操作。拖动中按 `Esc` 会取消且不改场景或配置。若该区域原本使用`定义尺寸`，第一次实际调整会自动切换为该实例的`场景 Transform`尺寸，公共对象定义本身不会被修改。首版 AABB 仍要求单位世界旋转，旋转盒不会被静默近似。

## 配置

选中一个已登记的世界对象或场景角色后，属性区提供：

- `重新放置`：进入一次性鼠标放置，确认前不移动源对象。
- `复制并放置`：复制真实 GameObject 和玩法配置，并在单次确认后创建新登记。
- `更多`：对齐网格、仅移除登记、删除场景对象。

复制会保留分组、旋转、缩放和玩法属性，但会生成新的 Transform 引用，以及新的数值 ID 或稳定 ID。World 实例保留启用状态、定义、尺寸来源、ColliderId、分组和说明；关卡点保留启用状态、定义、点类型、分组和说明；区域保留启用状态、定义、区域类型、尺寸来源、分组和说明。副本按名称和分组重新排序后，这些配置仍来自所选原条目，来源和其他登记不变。静态碰撞体批次第一阶段不支持复制和重新放置。

世界对象复制在新增登记前保存来源配置，因此默认副本名（例如 `Probe Copy`）排在 `Probe` 前面、或来源使用自定义分组时，也会保留来源的启用状态、定义、分组和说明。可移动物体与单个静态碰撞体同时保留尺寸来源和 ColliderId；点保留点类型，区域保留区域类型和尺寸来源。列表重排不会改变复制来源，原条目及其他登记不被修改。

多选 Unity Transform 后使用`对齐网格`，会在一次 Undo 中完成所有对象的 X/Z 对齐。

## 配置玩法触发器

在关卡编辑器顶部选择`玩法触发器`工作区。左侧显示实际存在的自动分类、搜索、新建入口和 Trigger 列表；右侧使用统一的紧凑`事件 / 条件 / 动作 / 效果`结构。锁定事实直接显示为文字，阵营、Buff、来源机制和 Story 选择器嵌入条款，目标直接写入动作句，不再单独占用`作用对象 / 触发对象`卡片。世界对象的 Region Inspector 不再显示大块编辑卡，只保留`关联触发器 N 个`、编辑入口和新建入口。

![统一触发器工作区](../Images/LevelEditor/P3.3/unified-trigger-workspace-concept.png)

点击`新建触发器`，先选择正式的 Trigger Region，再选择当前三个固定模板之一：

- `进入时触发剧情`：进入区域后以 `ReliableDeferred` 发送一次 `Story.RaiseTrigger`，填写剧情触发 ID 与两个可选整数参数。
- `进入时施加 Buff`：对象进入区域时以 `SameFrameReserved` 对该对象施加一次 Gameplay Buff。新建后先显示`请选择 Buff`，不会默认猜测一个效果；选择器读取当前 Gameplay 组合根使用的正式 Buff Registry，优先显示中文名称、秒制持续时间、重复施加结果、叠层结论和实际玩法说明。逻辑帧、原始 Stack Policy、每次施加层数与最大层数保留在悬浮说明中，它们来自 Buff Definition，不是 Trigger 的第二套覆盖配置。未选择会阻止保存；目录外旧 ID 会明确标记为未登记，只应通过`高级规则`中的手动 ID 入口迁移。
- `区域内维持 Buff`：进入与停留期间按现有 Apply/Refresh 语义维持 Buff，离开时 Remove；两条 route 保持确定顺序。默认每帧提交一次刷新，但只覆盖最多 8 个已映射主体，不引入新的持续效果系统。

主编辑区优先显示真正需要编辑的内容：

- 顶部编辑名称和启用状态，并提供 Scene 定位、复制和`更多`。stable ID、分类与区域名只作为一行弱化元数据。
- `事件`用一句话显示模板实际启用的进入、停留或离开时机；`条件`先显示角色身份和阵营筛选，再提供`附加条件`。两条以上可选`全部满足（且） / 任一满足（或）`，每条左侧的`非`可单独取反；删除最后一条会恢复“无附加条件”，不会删除事件或目标筛选。高级规则继续提供`全区域共用`、`每个对象独立`、`每次进入一次`。
- 区域来源当前只开放真实存在的`触发对象存在`条件；当前玩家生命阈值来源还可比较`触发前生命值 / 当前生命值`。Payload、Signal ID 和混合嵌套树不会伪装成普通选项，历史复杂树会只读保留。
- 动作目标直接写在`动作`句中：区域 Buff 对`触发角色`生效，Story 对`当前区域`生效，不再重复显示独立的`作用对象 / 触发对象`卡片。Trigger Source、完整技术类型与不兼容 Domain 只保留在技术信息中。
- `动作`把 Story、Buff 和`来源机制`选择器直接嵌入句子。来源机制底层写入 `GameplayBuffApplicationRequest.SourceId`，只记录施加来源，不改变 Buff 的伤害、持续时间或叠层，也不会执行所选 Aura / Mechanic Source 定义自己的效果。普通区只显示正式目录中的`未指定 / 501008 守护光环 / 501010 区域禁锢 / 501012 毒瓶池`，501013 地雷和 501014 炮台没有正式 Buff 来源定义，因此不出现。
- `高级规则`包含`冷却`、`执行次数`和`必须可移动`：默认使用`无冷却`以及模板自身的`不限`或`1 次`，选择`自定义`后才显示正整数输入；`必须可移动`只在确实要排除不具备 Gameplay Movable 能力的角色时开启。冷却使用 Runtime 逻辑帧，CharacterTest 默认 60 帧约为 1 秒；次数按当前所选范围累计。`Buff ID（手动）`和`来源机制 ID（手动）`只用于迁移目录外旧数据，不是普通配置入口。
- `技术信息（只读）`折叠 stable ID、source/condition/definition/route/binding/source-binding ID、provider、layer、容量、delivery contract 与正式 Compiler 路径。
- 底部`效果`用一句确定性中文描述整条规则；它只读取类型化 Authoring，不保存到 JSON，也不从文字反向生成配置。

触发时机、目标和动作种类仍由所选固定模板决定，不伪装成尚未接入的自由编辑能力。需要更换它们时，删除当前 Trigger 后选择另一模板。复制在标题栏直接可见，删除与运行时检查放在`更多`中；删除配置会保留区域对象。

新增、启用/停用、计数与冷却范围、冷却、次数、动作参数、复制和删除分别占用一个 Unity Undo 步骤。修改 Buff 或 Story 参数只替换动作 payload，不会重置已设置的范围；复制会保留启用状态、范围和参数，但为 entry 及全部 typed domain row 分配新 ID。未从菜单选择模板、取消删除或切换选择都不会产生草稿数据。

主编辑区已经滚动到下方时，切换范围、Buff、来源机制、冷却或执行次数会保留当前位置和输入焦点，不会因为刷新效果句和校验结果而自动跳回顶部。搜索、分类、选择和展开高级规则不产生未保存状态。目录中找不到历史值时，普通区会保留原值并显示兼容警告，不会在刷新、折叠或保存前自动清零。

SceneView 会给区域显示低透明度实体面；当前选中、通过正式 `GameplayTriggerConfigCompiler` 预检且使用 AABB 定义的 Trigger Region 还会追加绿色 compiled candidate 面与边线。它是 P2.3 Authoring 预览，不代表 Runtime 已经执行。

![检查编译范围](../Images/LevelEditor/P2.3/03-check-compiled-range.png)

非单位世界旋转、缺失 Transform、非法容量、无效 Story/Buff 参数或 Compiler 错误会显示在对应规则中并阻止保存。修正问题后执行一次`保存`，编辑器会在同一事务中提交 Authoring，并把 canonical Trigger payload、fixed Combat AABB sensor 和 catalog hash 写入 `GameManager` Scene cache，随后保存受管 Scene。修改已经运行的 session 不会热重载，需重新进入 Play Mode 或重建 CharacterTest offline session。

![保存与维护](../Images/LevelEditor/P2.3/04-save-and-maintain.png)

## 运行时检查

P2.4 已把 level-authored Trigger 接入 CharacterTest offline `RuntimeHost`。进入 Play Mode 后，在`玩法触发器`工作区选中规则，并使用`更多 > 运行时检查`打开临时抽屉，可以看到：

- 启动状态以及 Trigger、Combat Sensor、combined catalog hash。
- Runtime 实际使用的 `Combat AABB` 中心、半尺寸、layer mask 与容量。
- 最近一次 sensor 状态、命中数、Gameplay 实体映射数和拒绝原因。
- 最近一次 Entered/Stayed/Exited、frame 与 subject。
- Buff 或 Story 动作的派发、终态结果、原因码和丢弃计数。

这些信息来自 Runtime typed status/result，界面每 200 ms 低频刷新一次，不读取 Unity Collider 来伪造运行结果。区域 GameObject 不需要 Collider；占用查询由现有 `CombatPhysicsWorld` 与 `GameplayTriggerCombatSensorSystem` 完成。P3.2 会在 occupancy 提交前按 Gameplay 身份、能力、存活和阵营关系筛选，检查页显示命中、映射、通过、过滤和首个 typed rejection。`进入时施加 Buff`在同帧走正式 Gameplay Buff bridge，`进入时触发剧情`走可靠 Story transfer。

P2.5 正式示例已经写入 `CharacterTestScene`：选择`区域 > 起始区域`可看到 `区域内维持 Buff`，Buff 为 `601012 · 中毒`，来源机制为 `501012 · 毒瓶池`。摘要说明它持续 6 秒、重复施加刷新且不可叠加；来源机制只作来源记录。进入 Play Mode 后让角色位于起始区域，`运行时检查`应显示 sensor submitted、mapped subject、Entered/Stayed 和成功的 Buff action；离开区域后应出现 Exited 与 remove 结果。该区域只含 Transform，缓存使用 64 个 Combat 查询候选和最多 8 个映射主体。

cache schema/content/hash、unknown source binding 或动作 bootstrap 不完整时，整份关卡 Trigger runtime 会显示 fail-closed，不回落 Unity Physics，也不会运行半套动作。当前只接入 offline `GameSlice`；网络会话迁移与运行中热重载留到后续阶段。

## 删除语义

`仅移除登记`只从当前关卡文档移除记录，场景中的 GameObject 保留。

`删除场景对象`会先确认，然后同时删除 GameObject 和当前模块中的对应登记；不会删除 Prefab 资源。对象若仍被其他模块或静态碰撞体批次引用，操作会被阻止，并显示引用来源。

## 撤销、保存与应用

新建、复制、重新放置、移除登记和删除场景对象都通过同一个 Unity Undo 步骤同步场景与关卡文档。Undo/Redo 后列表、选择、校验和未保存状态会自动重建。

直接使用 Unity 的移动、旋转或缩放工具调整已登记对象时，关卡编辑器也会立即显示`未保存`。对象的 Transform 由 Unity 场景保存，JSON 只保留关卡登记、定义和玩法属性，避免两份坐标互相覆盖。

`保存`会统一保存全部有改动的关卡模块 JSON 和对应 Unity 场景，并把它们设为新的保存基线。保存会先预检全部模块，确认都能保存后才依次写入配置与场景缓存，最后只保存一次 Unity 场景；任一模块校验失败时不会提前保存场景。写配置或应用缓存时发生异常，已写入的配置文件会恢复到保存前版本，场景继续保持`未保存`，可修复后重试。撤销或重做到不同于该基线的内容时，窗口重新显示`未保存`。

场景根节点下如果出现没有被世界对象或场景角色文档登记的对象，顶部会显示`未登记 N`，底部校验区也会给出警告。点击入口可定位对象，再为它指定类型或登记为场景角色；已登记对象的模型、碰撞体等子节点不会被重复误报。

关闭关卡编辑器时，只要任一模块或当前场景还有未保存内容，Unity 会显示`保存 / 不保存 / 取消`提示：

- `保存`：保存全部关卡模块 JSON 和当前 Unity 场景；任一模块因校验错误无法保存时保持窗口打开。
- `不保存`：取消正在进行的放置，重新载入最近一次保存的 Unity 场景和全部模块 JSON；当前活动场景中的其他未保存修改也会一并放弃。
- `取消`：不关闭窗口，继续编辑。

没有保存路径的新场景无法执行恢复，此时编辑器会阻止放弃关闭并提示先保存场景。

完成编辑后可先执行`校验`查看问题，再执行`保存`。场景角色、世界对象与玩法触发器都会在统一保存事务中更新 CharacterTest 场景缓存；只有全部参与模块均可完整编译时，保存才会成功。运行时读取场景缓存，而不是直接读取作者 JSON。

## 场景渲染

在正式场景打开 `MxFramework > Level Editor`，选择`场景渲染`。活动场景必须恰有一个 GameManager；模块不会自动创建或猜选宿主。`场景绑定`分别开关间接光和高度雾，直接引用太阳 Light、目标相机、共享 Look 与现有光传输缓存。`太阳与天光`直接修改太阳 Light/Transform 和共享 Look；后者会影响引用同一 Look 的其他场景。`高度雾`调整雾参数；Shader、Compute 与材质源映射位于高级区域。Play 模式只读。

新场景点击`创建并自动扫描场景渲染配置`；已有配置可点击`自动扫描缺失参数`。扫描从当前场景识别主相机、`RenderSettings.sun`、可安全预处理的静态根，以及相机正在使用的 URP Renderer；从项目资源识别唯一 Look、Compute、Shader 和**当前场景专属**光传输缓存，并从材质识别已知源属性。它只补空值，不覆盖手动绑定或 GI／雾开关；无法唯一确认的项会显示原因。`CharacterTestScene` 的世界根混合了活动物体，因此自动扫描只选择场地、材质样本和固定台阶／坡道的 32 个原有 MeshRenderer 作为 GI 静态源，不复制或重排模型；动态物体、网格线和验证标记不进入缓存。本场景现在有独立的光传输资产，不能借用 `TacticalCardDemoScene` 的缓存。以后调整所选几何、材质或位置，须重新构建本场景缓存。

编辑模式预览在关闭 Level Editor 后继续工作，不启动玩法。`预览数据`只影响当前预览，不会写入共享 Look。几何或反射率改动使缓存过期时，模块会提示重建；创作进度仍可保存，但启用 GI 的正式构建会拒绝缺失或过期缓存。`构建光传输`和`仅重建太阳遮挡`是显式操作，普通`保存`不启动烘焙；构建完成后仍需保存新缓存引用。保存事务只额外覆盖本模块修改过的 Look 资产。渲染配置属于 Unity 场景，不进入玩法关卡 JSON、回放或存档权威。

## 当前边界

当前不包含最近使用、收藏、Prefab 素材库、连续盖章、静态批次复制、自动旋转、法线对齐、阵列或随机散布。Trigger 已完成 P2.3 Authoring、P2.4 offline Runtime Slice、P2.5 正式示例/Playable 验收，以及 P3.3 的顶层区域 Trigger 工作区和首个玩家生命阈值来源；其他 Signal/Threshold、通用逐动作编排、网络会话、活动 session 热重载和 SceneView 运行时占用 overlay 尚未实现。

P2.4 Runtime 组合与 P2.5 Playable 验收见[第二阶段定义驱动玩法闭环设计](../LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DESIGN.md)，实施顺序和验证台账见[第二阶段开发指南](LEVEL_EDITOR_DEFINITION_DRIVEN_GAMEPLAY_DEVELOPMENT.md)。P3.3 的架构边界和验收见[统一触发器工作区设计](../LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DESIGN.md)与[开发指南](LEVEL_EDITOR_UNIFIED_TRIGGER_WORKSPACE_DEVELOPMENT.md)。P2.5 当前交付等级为 CharacterTest offline `Playable`，不代表网络会话或运行中热重载已经完成。
