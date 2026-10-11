# CharacterTest Runtime AI Planner Editor Development Guide

> Status: Guide
>
> Date: 2026-07-02
>
> 本文的 AI 指 **Runtime AI Planner** 编辑界面：`MxFramework/Character/Runtime AI Planner` Unity Editor 窗口。本文不记录 AIAction Config、Authoring AI Assist 或 Development Agent 工作流。

## 定位

本文记录 CharacterTest Runtime AI Planner Editor 的开发目标、界面结构、代码结构、交互边界和后续路线。它是编辑界面的持续开发记录，不是单次任务归档。

当前事实源仍以源码、测试、`Docs/Guides/CHARACTER_TEST_RUNTIME_AI_PLANNER_DEVELOPMENT.md`、`Docs/Interfaces/AI.md` 和 `Docs/README.md` 为准。本文只解释 Editor 界面如何 author 同一份 CharacterTest Runtime AI Planner JSON profile。

## 当前目标

Runtime AI Planner Editor 的目标是让主创在 Unity Editor 内高效编辑和理解 CharacterTest 行为 profile：

- 快速选择、创建、加载、绑定、校验、保存 StreamingAssets JSON profile。
- 用结构化控件编辑 HFSM 状态、状态切换、Utility actions、感知参数、移动风格参数和条件 facts。
- 在 Edit Mode 完成保存和校验；Play Mode 默认只做只读监控，只有 `行为组编辑` 页签在显式开启 debug gate 后允许当前会话 overlay 热更新。
- 用流程图预览、局部诊断、条件模板、行为卡原型和 Play Monitor 解释降低理解成本。

普通 profile 编辑仍不是节点图编辑器，也不是外部 Workbench。状态树、切换表、动作表和 Inspector 仍是 profile 的唯一编辑入口。`行为卡` 页是例外的 Editor-only 原型视图：它用自动布局分支流程图解释单张行为卡草稿，但该草稿尚不保存为正式 JSON，也不进入 Runtime AI Planner runtime 执行。

当前行为卡静态编辑器不再继续扩展为完整行为开发环境。后续优先在独立 `CharacterTestRuntimeAiLabScene` 中做运行时开发者工具：按帧暂停/单帧、读取真实 debug snapshot、在场景中可视化空间关系，并在显式 debug gate 下做当前会话临时调参。Editor 窗口继续承担 JSON/profile、行为组和行为卡原型的低频 authoring 与排查入口，不承担“实时按帧验证 AI 效果”的主职责。

## 界面结构

主窗口菜单路径为 `MxFramework/Character/Runtime AI Planner`。

当前布局分为五块：

| 区域 | 职责 |
| --- | --- |
| 顶部 toolbar | 行为 Profile 库选择器、打开、新建模板、复制为、设为场景默认、校验、保存、复制报告 |
| 左侧面板 | Profile 摘要、状态树、状态增删复制入口 |
| 中间页签 | `状态`、`动作`、`感知`、`行为组预览`、`行为组编辑`、`行为卡`、`校验`、`Play Monitor` 主编辑/监控内容 |
| 右侧 Inspector | 当前状态、切换、动作或 Play agent 的结构化字段和局部诊断 |
| 底部状态条 | 未加载、已加载、有未保存修改、已保存且重建 CharacterTest 会话后生效 |

页面职责：

- `状态`：只读行为流程预览、状态切换表、状态/切换 Inspector。
- `动作`：按 state/action id 筛选 Utility actions，编辑 action kind、score、cost、输出映射、移动风格覆盖和条件。
- `感知`：编辑 v3 perception 与 movement 默认参数，并展示可用 facts 说明。
- `行为组预览`：只选择低频候选行为组并查看只读决策链、紧凑动作结果、底层流程高级预览、摘要和诊断；不提供搜索、编辑、复制 JSON、覆盖 profile 或 hot update 入口。
- `行为组编辑`：左侧搜索行为定义库，中间编辑当前行为组 entries，右侧检查选中实例参数；低频编译成现有 v3 profile，可复制 behavior group JSON、覆盖底层 profile 草稿，或在显式 debug gate 下应用到当前 Play Mode evaluator。
- `行为卡`：Editor-only 行为卡草稿页。未选中流程组件时，左侧单侧栏显示行为卡库；选中动作节点、判断节点或连线标签时，同一侧栏切换为标准化步骤/分支 Inspector。中间主区域显示自动布局分支流程图、步骤内小时间轴和结果预估；右侧独立 Inspector 在该页隐藏，让主图获得最大宽度。
- `校验`：展示 validator report 和可复制报告文本。
- `Play Monitor`：只读显示当前 Runtime AI Planner debug snapshot 和决策解释。

## 数据与保存边界

Profile 真源是 JSON 文件，不是 ScriptableObject。

- 默认 profile 路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/default.json`。
- 感知版近战模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/perception_melee.json`。
- 远程风筝模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/ranged_kiter.json`。
- 危险游斗模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/hazard_skirmisher.json`。
- 技能施法模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/skill_caster.json`。
- 激进冲锋模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/aggressive_berserker.json`。
- 守卫防御模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/defensive_guardian.json`。
- 伏击者模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/ambusher.json`。
- 控制控场模板路径：`Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/controller_zoner.json`。
- 场景只保存 StreamingAssets 相对路径，例如 `MxFramework/CharacterTest/runtime_ai_planner/default.json`。
- 编辑器通过 draft model 修改内存数据，保存时一次性写回 JSON。
- `ProfileAsset` / ScriptableObject 不再作为 Runtime AI Planner profile 真源。

日常选择 profile 不再以文件选择器为主入口。顶部 `行为 Profile` 控件会扫描 `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/*.json`，显示中文模板名、文件名、profile id 和校验状态；搜索和刷新都在窗口内完成。原始 JSON 路径和 `选择 JSON` 只保留在“高级 JSON 路径”折叠区，用于临时导入 StreamingAssets 下的自定义文件。

Unity `PopupField` 底层菜单会把 `/` 当作层级分隔符，因此行为库选项 label 不能使用 slash 分隔。Profile picker 使用扁平分隔符显示信息，路径细节放在状态行和高级折叠区。

保存 profile 后不热更新当前 Play Mode runtime。窗口必须继续提示“保存后重建 CharacterTest 会话生效”。`行为组编辑` 页签的 `行为组热更新` 是例外：它只替换当前 Play Mode session 中的 evaluator，失败时保留旧 evaluator，不写回 JSON，不进入 SaveState / network snapshot / replay / hash。

行为 Loadout 玩法层与上述 debug overlay 是两条路径。Loadout catalog 是发布后的低频 authoring/validation/export 入口，运行时权威状态只保存 `loadoutId`、`variantId`、`catalogHash/revision` 和派生 `profileSlot`。Editor 可以显示、校验和导出 catalog，也可以为未来战前/安全区 UI 提供低频 view model/helper：查询某实体可选哪些 loadout/variant、当前选择是什么、提交 command 后是否会被接受。Editor 不得把任意 behavior group JSON、v3 profile JSON 或 Play Mode overlay 直接注入 gameplay selection。

## 行为组预览与编辑页

行为组能力拆成两个页签：`行为组预览` 负责看懂一个行为组会生成什么，`行为组编辑` 负责修改当前行为组草稿。两者都不编辑新的 runtime schema，而是把 CharacterTest internal behavior definition catalog 和 behavior group 编译成现有 v3 `CharacterTestRuntimeAiPlannerProfile`，继续复用 validator、compiled evaluator 和 Play Monitor snapshot。

### 行为组预览

`行为组预览` 是只读页。它只能选择现有低频候选并预览编译结果，当前候选固定为：

- `当前行为组草稿`：Runtime AI Planner 窗口内当前 draft 的只读 clone。
- `内置默认行为组`：CharacterTest built-in default behavior group。
- `默认 Loadout variant`：默认 Behavior Loadout catalog 的默认 variant 所引用的 behavior group。

预览页不得扫描磁盘 JSON 目录，不新增保存/导入规则，不改变 gameplay `BehaviorSelection`，也不提供行为定义库搜索、entries 编辑、临时禁用、复制、删除、复制 JSON、覆盖底层 profile 草稿、hot update 或 Play Mode 执行入口。预览编译只发生在 Editor 低频刷新；Tick 高频路径仍只读取已编译 evaluator。

预览页中间主区以 `行为组决策链` 为首屏入口：按 `entry.order` 展示组内行为实例，固定列为 `优先级`、`行为名称`、`触发条件摘要`、`输出动作`、`状态/诊断`。它直接回答“当前组有哪些行为、先尝试什么、什么条件触发、最终输出什么动作”。`组内行为执行结果` 只作为紧凑辅助条，用玩家/主创可读文案列出所选组 entries 的编译结果，例如 `接近目标 -> 移动到目标（movement）`；entry id、module id、action@state 和 intent 放在 tooltip、Raw 或诊断折叠区，不作为首屏主文案。底层状态机图保留在 `底层 Runtime AI Planner 流程` 折叠区，用于高级排查 fixed scaffold、transition rule 和 action 所在 state。该预览不得遍历或绘制全行为定义库，也不得把未加入行为组的 behavior definition 当成预览候选。右侧 Inspector 默认显示 `策略摘要`、主要顺序、限制与风险；来源、group、entries 数、输出 profile 和 compile status 放到 `Raw` 折叠区。

### 行为组编辑

`行为组编辑` 页签是面向主创的可组合行为入口。该页必须采用三栏模型：

- 左侧 `行为定义库`：只显示搜索/过滤后的行为定义。搜索支持 behavior id、显示名、分类、tag、action primitive 和 required facts；实现上使用 UI Toolkit virtualized `ListView`，不要把几百/上千个行为定义渲染成卡片或 Toggle。
- 中间 `当前行为组`：只显示已经加入到当前组的 entries。每个 entry 有稳定 `entryId`、`behaviorId`、`order`、`disabled` 和 `parameterOverrides`，支持添加、排序、复制、删除和临时禁用。
- 右侧 `行为实例 Inspector`：显示选中 entry 的意图、参数 override、required facts、conditions、score/cost、action chain、编译摘要和诊断。

编辑页不渲染流程图预览，避免行为定义库、组实例 Inspector 和流程图挤在同一屏。需要查看当前草稿时使用 `查看预览`，切到 `行为组预览` 并选择 `当前行为组草稿`。

行为定义库不是当前角色状态；行为组才是角色/策略实际拥有的行为集合。因此 UI 不能再表达为“勾选启用哪些库内行为”。从行为库添加到组时会创建新的 entry；同一个 behavior definition 可以被复制成多个 entry，用不同 order 或参数 override 表达不同策略段。`disabled=true` 只表示该组内实例当前不可执行，仍保留在组列表和 report 中，但不进入可执行候选。

行为组编辑与 Arena 行为组设置都应采用“行为卡牌”表达：

- 行为定义库里的每个搜索结果是模板卡，显示名称、能力分类、触发摘要、输出动作、主要参数和风险标签。
- 当前行为组里的每个 entry 是实例卡，显示 order、disabled 状态、参数 override、典型情境命中情况和编译诊断。
- 筛选优先支持能力分类：`Targeting`、`Engage`、`Damage`、`Pursuit`、`Control`、`Kite`、`Defense`、`Navigation`、`Fallback`、`Support`，而不是只按旧 category 搜索。
- 详情 Inspector 必须显示“这张卡提供什么能力、依赖什么能力、适合反制什么能力、为什么可能不生效”，raw `behaviorId`、`entryId`、facts、score/cost 和 action@state 只放在 Raw/诊断区域。

行为组预览和 Arena 行为组设置应显示能力覆盖条：已满足能力、只由禁用卡提供的能力、缺失能力和风险标签。最小可运行组合至少要覆盖 `Targeting + Damage + Fallback`，并需要 `Engage/Pursuit/Control` 或等价远程压制来避免只会追或只会跑。Arena 队伍设置页可以用双方能力报告给出低频对抗提示，例如敌方有 `Kite/Defense` 而我方没有 `Pursuit/Control` 时提示“可能追不上”；某组有 `Kite/Defense` 但没有 `Damage` 或回战斗链路时提示“可能只会逃跑”。这些提示是配置预估，不是 gameplay 权威状态，也不进入 Tick、SaveState、network、replay 或 hash。

### 行为卡编辑器 UI 形态

行为卡编辑器用于维护单张 `BehaviorDefinition` 的内部流程，不用于替代行为组编辑页或 CharacterAction 编辑器。当前实现基线采用“单侧栏 + 自动布局分支流程图 + 步骤内时间轴辅助”的布局，而不是纯时间轴编辑、普通步骤列表、三列泳道图或自由节点画布：

- 左侧单侧栏：无流程组件选中时显示 `行为卡库`，按能力分类、标签、输出动作和风险过滤模板卡；只显示搜索/分页结果，不渲染全库。选中动作节点、判断节点或连线标签后，同一侧栏切换为标准化步骤/分支 Inspector。
- 中间 `分支流程图工作台`：占用剩余全部宽度，显示 3-5 个动作节点、结构化判断节点、条件连线和明确 `End` 终点，步骤类型固定为 `UseAbility`、`UseAction`、`MoveToTarget`、`Wait`、`End`。动作节点是紧凑矩形卡，只显示执行摘要、友好动作名、持续时间/关键窗口和必要 warning；判断节点表达 `距离 <= 1m?`、`连段窗口允许?` 这类分支问题；连线标签写具体条件，例如 `已进 1m`、`未进 1m`、`失败/中断`。主图区必须默认看全图，不使用横向滚动作为主理解方式，节点、线段和标签不得互相遮挡或穿过节点。
- 标准化 Inspector：以步骤模板、条件预设和出口规则为主，编辑 Ability / CharacterAction 引用、距离阈值、监听出口、完成出口、失败/中断出口和判断窗口。raw `ConditionSummary`、底层 id、完整表达式、裸帧字段和高级诊断默认隐藏，不作为主编辑单元。
- 底部 `路径预估与高级诊断`：用路径式短句显示典型情境会走到哪一步；完整 Ability / CharacterAction 关键帧、gameplay request、hit trace、root motion、VFX、raw id 和断链 warning 只放高级诊断区。

步骤内小时间轴只用于解释当前步骤引用的 Ability / CharacterAction 大约持续多久、在哪个窗口判断条件、窗口错过后去哪里，不作为 CharacterAction 时间轴编辑器。原因是行为卡编辑的是“先判断什么、下一步请求什么动作、动作结束后如何分支”，而 CharacterAction 时间轴编辑的是“动作内部哪一帧发生什么”。两者必须保持分层：行为卡可引用和预览时间线结果，但不复制时间线字段，也不直接编辑 hit frame、root motion 或 VFX track。

`End` 必须作为明确终点节点显示，推荐文案为 `End / 交回行为组重新决策`。成功结束、失败结束和条件不满足结束都可以指向它，但主视图必须通过连线标签说明具体分支原因，不能只显示“继续 / 结束 / 条件”。

首版编辑限制：

- 单张行为卡最多 3-5 个步骤。
- 不支持用户拖拽保存节点布局、复杂 pan/zoom、循环、并行分支、任意表达式、脚本注入或运行时上传 JSON。
- 条件来源限定为现有 typed facts 或明确新增的 typed debug/authoring facts，例如目标距离、目标可见、自身空闲/硬直、Ability 是否可用、当前 CharacterAction 是否允许切换。
- “普通攻击”不作为编辑器概念；使用 `UseAction(light_attack)` 或具体 CharacterActionId。`CastAbilitySlot` 仅保留为旧 profile、测试和兼容路径，正式追击行为应使用稳定 `UseAbility(DashStrikeAbilityId)` 或后续 `CastAbilityId`。

第一步 UI 基础不直接发布行为卡到 gameplay。它已经形成 Editor-only 原型：行为卡库/Inspector 单侧栏、自动布局分支流程图、路径预估和高级诊断，并以 `突进追击` 示例卡验证编辑体验。06 的当前实现基线记录在 `Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_EDITOR_UI_FOUNDATION_06.md`；早期自由节点画布、三列泳道图和左右双侧栏布局只作为历史参考，不再作为当前 UI 验收目标。

### Runtime AI Lab 交接边界

`CharacterTestRuntimeAiLabScene` 是后续运行时 AI 开发的主要承载面，设计目标记录在 `Docs/Tasks/CHARACTER_TEST_RUNTIME_AI_PLANNER_RUNTIME_AI_LAB_SCENE_08.md`。Editor 窗口和 Lab 场景的职责边界固定为：

| 能力 | Runtime AI Planner Editor | Runtime AI Lab Scene |
| --- | --- | --- |
| JSON profile authoring | 是 | 否 |
| 行为卡静态原型预览 | 是 | 只消费概念和结果 |
| 真实帧暂停/单帧 | 否 | 是 |
| 场景内 LOS/path/range 可视化 | 只读 SceneView 辅助 | 主视图能力 |
| session-only 参数 overlay | 仅行为组 debug gate | Lab debug gate 下的主要验证路径 |
| 正式保存/发布 | 仍需后续 schema/publish 切片 | 不直接保存或发布 |

Lab 中的临时修改不能回写 Editor draft、StreamingAssets JSON、behavior definition catalog 或 BehaviorLoadout catalog。若某个 Lab 调参结果需要沉淀为正式内容，后续必须另走 schema、compiler、publish/loadout 切片。

当前内置行为定义包括：

- `Targeting.NearestEnemy`
- `Movement.ApproachTarget`
- `Movement.KeepDistance`
- `Movement.StrafeWhenBlocked`
- `Survival.AvoidHazard`
- `Combat.PrimaryAttack`
- `Combat.CastAbilitySlot`
- `Fallback.WaitOrHold`

行为组编辑页的按钮语义：

- `编译覆盖底层 Profile 草稿`：把当前行为组编译成 v3 profile，并加载进原有状态/动作/感知页；保存后写出的仍可作为 v3 JSON debug/export 产物。
- `查看预览`：切到 `行为组预览` 并选择 `当前行为组草稿`。
- `复制行为组 JSON`：复制 `mx.character-test.runtime-ai-planner.behavior-group.v1` JSON，供后续 authoring 真源或审阅使用。
- `行为组热更新` + `应用到 Play Mode`：显式 debug gate。仅在 Play Mode、已注册 Runtime AI Planner debug target、编译通过时可用；调用 runtime 低频 overlay 入口替换当前 evaluator。失败时显示 report，旧 evaluator 继续工作。

Agent 使用规则：

- 新增行为时，先扩 behavior definition catalog / JSON loader 和 compiler；不要在 Editor UI 中手写只存在于展示层的行为逻辑。
- 行为实例命中状态和流程预览只能从当前 Play Monitor typed snapshot / view model 或 compiler summary 派生，例如 selected action kind、target visible、blocked、path、hazard、score/cost、generated action state；不要在 Editor 中查询 Unity scene object。
- 如果 Inspector 需要更多解释数据，应扩 CharacterTest runtime typed debug snapshot 或 compiler behavior entry summary，而不是在 Tick 高频路径拼接中文字符串。
- 行为组 hot update 是调试 overlay，不是保存流程。任何要变成权威 gameplay 状态的 runtime 修改，都需要另开 SaveState / network / replay / hash 设计。

## 行为 Loadout Authoring 与监控

Runtime AI Planner Editor 对 loadout 的职责是“看得懂、校验清、导出稳”，不是实现具体战前玩法 UI。首版约定：

- `BehaviorDefinition` 和 `BehaviorGroup` 仍服务 authoring 草稿和 debug overlay；`BehaviorLoadout` catalog authoring/export 服务玩法可选策略发布；`BehaviorSelection` 只从 runtime snapshot/command 读取或提交，不由 Editor 直接改 runtime 对象。
- Catalog authoring 必须使用稳定 `int loadoutId` / `variantId` 和 `stableId`，并显示 catalog hash/revision。改变模块顺序、参数默认值、allowed group 或 variant 时，hash 变化应可预期。
- Play Monitor 和 SceneView 只读显示当前 snapshot 中的 `RuntimeAiPlannerProfileSlot`、`RuntimeAiBehaviorLoadoutId`、`RuntimeAiBehaviorVariantId`、`RuntimeAiBehaviorCatalogHash` 和 `RuntimeAiBehaviorProfileSlot`；中文标签、状态 chip、颜色和 tooltip 都在 Editor view model 中生成。
- 如果需要解释“为什么当前 loadout 选了这个 action”，优先复用 Play Monitor agent snapshot、模块摘要和 compiler diagnostics；缺少字段时扩 typed debug snapshot，而不是在 Tick 内创建展示字符串。
- 未来战前/安全区 UI 应调用低频 helper 查询 allowed list 和当前 selection，再提交 `SetRuntimeAiBehaviorSelection` command；它不应保存 evaluator、behavior group JSON、Editor 热更新 overlay 或原始 profile JSON。
- Debug hot update 控件必须继续标明“不进权威状态”。当 loadout selection 已经生效时，Play Monitor 可以并列显示“权威 loadout/variant/profileSlot”和“debug overlay 来源”，避免误判当前行为来自玩法选择。

### 运行时策略选择面板

CharacterTest 当前提供一个 FairyGUI 全屏 `RuntimeAiBehaviorLoadoutPanel` 作为战前/安全区策略选择基础。Runtime AI Planner 窗口的 Play Monitor 只负责打开/关闭该面板，并通过 debug target 显式切换 selection phase locked/unlocked；真正的玩法选择仍由面板提交 `SetRuntimeAiBehaviorSelection` command，经 runtime command module 校验后才生效。

面板职责边界：

- 左侧只列出可配置的 AI-controlled 单位；中间只列出已发布 Behavior Loadout variant；右侧显示玩家可读策略摘要、主要动作、适合场景、限制、行为顺序和 Ability / CharacterAction 链路预览。
- phase locked 时允许查看当前策略和候选策略，但 `Apply` 不可提交权威选择。
- 面板不显示 behavior definition 库、不编辑 behavior group entries、不复制 JSON、不覆盖 v3 profile、不触发行为组 debug hot update。
- 面板 ViewModel、按钮文案和动作链摘要都在低频 UI 层生成；Runtime Tick 仍只读取已编译 evaluator/profile slot。
- 测试应覆盖 ViewModel 候选生成、locked/unlocked command 提交流程、Editor debug target 控制回调，以及 selection command 进入 snapshot/hash/replay 的既有回归。

### 竞技场配置面板

`CharacterTestArenaScene` 使用 FairyGUI `ArenaSetupPanel` 作为 Runtime AI Planner 行为 Loadout 的 playable test harness。它不是 Runtime AI Planner 编辑窗口的一部分，也不是行为组编辑器；它面向测试者和未来玩法原型，回答“双方各上哪些角色、用哪套策略、点击开始后让 Runtime AI Planner 自动打一局”。

面板职责边界：
- 配置 Team 1 / Team 2 的固定 1-3 个槽位，槽位只选择角色、已发布行为 Loadout/策略和 variant。
- 点击 `开始战斗` 后，低频生成 `CharacterTestCharacterSpawnSpec`，写入 `CharacterTestRuntimeAiBehaviorSelection`，并要求 `CharacterTestArenaSessionController` 重建干净 offline session。
- 战斗中只显示存活数、运行时间、胜负状态、`再来一局` 和 `重新配置`；不允许战斗中切换策略或直接改角色 runtime 状态。
- 不显示行为定义库、行为组 entries、facts、score/cost、profileSlot、catalog hash、raw JSON、复制 JSON、覆盖 profile 或 debug hot update。
- 面板文案使用玩家/测试者可读策略语言；底层 `loadoutId + variantId -> behaviorGroupId -> compiled profile slot` 链路只作为 tooltip、诊断或调试视图信息。

该面板可以复用 Runtime AI Planner player behavior presentation view model 和 action-chain preview 摘要，但不得在 Tick 高频路径生成中文字符串、扫描 JSON 或查询 Unity scene object。后续如果要升级为正式战前策略选择 UI，应复用同一套 allowed-list / command / authority provider，而不是从 Arena panel 直接保存 evaluator 或行为组 JSON。

### 玩家表现预览

行为编辑器 / Loadout 页面需要同时服务主创理解和未来玩家 UI 预览，但两者语言不同。主创视图可以显示模块、facts、score/cost、profileSlot、catalog hash 和 debug source；玩家预览只能显示策略卡语义，例如 `突击`、`风筝`、`守卫`、`控场`、`保命`、`协同`，以及短描述、主要动作倾向、适合场景、限制或风险。

Loadout 卡片应提供低频 presentation view model 字段：

- 玩家可读名称、短描述和战术标签。
- 主要行为倾向，例如接近、保持距离、绕行、撤退、优先施法或集火。
- 技能槽 / Ability / CharacterAction 摘要：显示该策略可能触发哪些槽位、Ability 名称和 CharacterAction 时间线摘要，但不编辑动作时间线。
- 运行风险或缺失链路 warning：槽位为空、Ability 未绑定 CharacterAction、CharacterAction 没有有效 `SendGameplayRequest` / hit trace / VFX 事件、目标距离或可见性条件长期无法满足。

当前基础实现中，`CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationCatalog` 负责策略卡和战斗短状态，`CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder` 负责 Ability / CharacterAction 链路预览。`行为组编辑` 页在选中 entry 的 Inspector 中显示动作链摘要，`行为组预览` 页先展示行为组决策链和可读动作结果，再把底层流程图放入高级折叠区；Play Monitor 的 Agent Snapshot 显示 `玩家短状态` 和 selected action 的 Ability / CharacterAction 摘要。后续玩家 UI 应复用这些低频 helper 或其派生 view model，而不是读取 Play Monitor raw facts。

Play Monitor 和 SceneView 继续是调试视图，可以显示 typed snapshot、condition chips、LOS/path 颜色、raw facts 和 reject/suppress reason。玩家 HUD、战前/安全区 UI 或未来策略选择面板只能消费低频 presentation view model，不暴露 debug-only 字段，也不能把 Play Mode overlay 当成权威玩法选择。

## 代码结构

Editor 代码位于 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/RuntimeAiPlanner/`。当前按职责拆分，避免单个窗口文件继续膨胀：

| 文件 | 职责 |
| --- | --- |
| `CharacterTestRuntimeAiPlannerWindow.cs` | EditorWindow 入口、字段、`CreateGUI` |
| `CharacterTestRuntimeAiPlannerWindow.Layout.cs` | toolbar、tab、主区域、左侧摘要、空状态、刷新和选择同步 |
| `CharacterTestRuntimeAiPlannerWindow.StateTab.cs` | 状态页、切换表、流程图主窗口接入、状态/切换 Inspector |
| `CharacterTestRuntimeAiPlannerWindow.ActionTab.cs` | 动作列表、动作筛选、动作 Inspector、action movement override |
| `CharacterTestRuntimeAiPlannerWindow.PerceptionValidationTabs.cs` | 感知页、profile movement defaults、校验页 |
| `CharacterTestRuntimeAiPlannerWindow.BehaviorGroupPreviewTab.cs` | 行为组预览候选、只读 behavior group 决策链、组内行为执行结果和底层流程高级预览 |
| `CharacterTestRuntimeAiPlannerWindow.BehaviorCompositionTab.cs` / `.BehaviorCompositionEditor.cs` / `.BehaviorCompositionCommands.cs` | 行为组编辑页、行为定义库搜索、行为组 entries、实例 Inspector、复制 JSON、debug gate 热更新入口 |
| `CharacterTestRuntimeAiPlannerWindow.BehaviorCard*.cs` | 行为卡页 UI：单侧栏、header、分支流程图、步骤内小时间轴、Inspector、forecast 和命令 |
| `CharacterTestRuntimeAiPlannerBehaviorCardEditorModel*.cs` | 行为卡 Editor-only 草稿、分支、时长预览、forecast、flowchart view model、legacy graph 兼容和 validation |
| `CharacterTestRuntimeAiPlannerBehaviorCardStandardInspector.cs` | 行为卡标准化步骤模板、条件预设、执行中监听出口、完成出口和失败/中断出口的 Inspector view model |
| `CharacterTestRuntimeAiPlannerWindow.PlayMonitor*.cs` | Play Monitor agent 列表、只读 Inspector、控件和 SceneView 诊断预览 |
| `CharacterTestRuntimeAiPlannerWindow.ConditionEditor.cs` | facts 下拉、operator/value 控件、条件模板、state popup |
| `CharacterTestRuntimeAiPlannerWindow.Diagnostics.cs` | 局部诊断、快捷修复、条件合法性辅助 |
| `CharacterTestRuntimeAiPlannerWindow.Commands.cs` | 创建模板、选择/加载/绑定/保存/复制报告、路径转换 |
| `CharacterTestRuntimeAiPlannerWindow.UiHelpers.cs` | 小型 UI Toolkit helper |
| `CharacterTestRuntimeAiPlannerEditorModel.cs` | editor-only draft model、状态/切换/动作 CRUD、DTO roundtrip |
| `CharacterTestRuntimeAiPlannerProfilePicker.cs` | editor-only 行为库扫描、搜索、校验状态展示和 picker UI |
| `CharacterTestRuntimeAiPlannerTemplateLibrary.cs` | editor-only 行为模板目录和模板 DTO factory |
| `CharacterTestRuntimeAiPlannerEditorFactCatalog.cs` | editor-only facts 元数据、条件模板、显示格式 |
| `CharacterTestRuntimeAiPlannerFlowPreview*.cs` | 只读行为流程预览、布局、节点渲染、绘制数据 |
| `CharacterTestRuntimeAiPlannerFlowPreviewWindow.cs` | 独立放大预览窗口 |

新增 UI 能力优先落到已有职责文件；当文件重新超过约 700 行或混入两类职责时，继续拆 partial 或提取小型内部 helper。

## 交互原则

编辑器面向主创，默认使用中文标签，并把技术 id 放在括号、tooltip 或高级折叠区。

必须保持这些原则：

- enum、action kind、ability slot、target mode、parent state、fact 和 operator 用下拉或类型化控件，不让主创手输易错字符串。
- bool 条件用开关；距离、威胁、权重用数值框；帧数和候选索引用整数框；地表 fact 用下拉。
- 列表行内显示错误/警告标记；右侧 Inspector 展示选中项的具体诊断。
- 只提供确定安全的快捷修复，例如补默认条件、修 action id、修 CastAbilitySlot 默认 slot。
- 未加载或坏 JSON 时显示正式空状态主操作，不显示大量置灰按钮。
- Play Monitor 保持只读，不提供保存、热更新或直接修改 runtime 对象的入口；行为组热更新只能在 `行为组编辑` 页签通过显式 debug gate 触发。

## 行为流程预览

状态页的流程图预览是理解工具，不是编辑工具。

当前支持两种模式：

- 概览模式：状态页默认使用，自动适应可视区，隐藏滚动条，节点和规则卡片简化，优先保证完整状态流可见。
- 详细模式：独立放大预览窗口使用，默认按窗口宽高适应并居中，canvas 填满可视区，保留滚动和完整条件摘要，适合查看复杂 profile。
- 行为组概览：`行为组预览` 页使用所选候选 behavior group 编译出的 summary/profile 作为输入，首屏显示 `行为组决策链` 和紧凑 `组内行为执行结果`。该模式解释“这个组会按什么优先级触发哪些行为、输出哪些 Runtime AI Planner 动作”，不是编辑底层状态树，也不是 gameplay 权威选择。
- 底层排查：`底层 Runtime AI Planner 流程` 折叠区复用只读行为流程预览显示 fixed scaffold、transition rule 和 action 所在 state。默认不要把这张图作为主创理解行为组的首屏入口。
- `放大预览` 入口固定在状态页 `状态切换` 标题行右侧；流程图内部标题栏保留缩放、`100%`、`居中` 和 `适应` 控制。
- `行为组预览` 页也提供 `放大预览`，但使用独立缩放状态；点击图中节点或规则只更新 Editor 状态提示，不切换到状态页 Inspector。`行为组编辑` 页不渲染流程图，避免编辑控件和预览互相挤占。
- 详细模式支持 `Ctrl + 鼠标滚轮` 缩放，以及中键拖拽或 `Alt + 左键拖拽` 平移；这些查看状态不持久化到 JSON。
- 流程图按 `语义图 -> 紧凑占用布局 -> 避障路由` 生成：HFSM 状态、缺失目标和 transition 条件先变成占用矩形，再计算连线；左上角全局规则图例不参与图布局占用。
- transition 条件显示为规则卡片节点，不再作为悬浮在线上的 label；节点、规则卡片和标签占用矩形必须互不相交。
- 连线使用正交避障路由，不穿过任何占用矩形；空间不足时扩大 canvas，由概览适应缩放或详细模式滚动解决，不把内容压到重叠。
- 状态行距必须按该行实际规则 shelf 动态计算，不能为了无重叠长期退化成固定大行距。
- 流程图必须先表达 Runtime AI Planner 语义，再表达几何连线：HFSM parent-child 是结构线，initial state 的祖先链是初始进入链，transition 规则显示为可点击规则卡片。
- `SourceStateId` 为空的 transition 表示全局规则，必须显示在目标状态正上方的专用进入规则区；不允许为了紧凑退到目标左侧或右侧。
- 全局规则只画 `规则卡片底部中心 -> 目标状态顶部中心` 的蓝色入口线，不能复用、覆盖或贴合 HFSM parent-child / initial path 的结构线通道。
- 左侧“全局规则”节点只是说明空 `SourceStateId` 的含义，不参与主流程连线。
- 当更高优先级全局规则可能同时成立并覆盖低优先级全局规则时，规则卡和 Transition Inspector 显示 editor-only warning；这不改变 validator 结果或运行时决策。

约束：

- 不引入 GraphView、第三方节点框架或外部 Workbench。
- 不持久化图布局，每次从 draft model 重新计算。
- 不在图上拖拽节点、不连线编辑、不改 JSON schema。
- 点击状态节点只同步 `_selectedStateId`；点击规则卡片只同步 `_selectedTransitionIndex` 和 Inspector。
- 缺失 target 的 transition 显示“缺失目标”警告节点，真正修复仍通过 Inspector 和 validator 完成。

## Scene Test Character Configuration

`MxFramework/Level Editor` now includes a `CharacterTest Characters` module for scene test actors. This module is separate from the Runtime AI Planner profile editor: it edits which scene transforms spawn as characters, their `characterId`, `teamId`, `ControlOwner`, group, notes, and optional Runtime AI Planner profile path. `ControlOwner = AiControlled` is still required for Runtime AI Planner participation.

Runtime AI Planner profile binding has a scene default plus character override model. Empty character-level profile paths inherit `CharacterTestSceneProfile.RuntimeAiPlannerProfilePath`; non-empty paths are loaded once during CharacterTest session build, assigned to startup spawn profile slots, and selected by the runtime command module without per-tick JSON access. The editor must still avoid hot reload controls or runtime object mutation from Play Mode.

The character Inspector uses the same behavior library picker as the Runtime AI Planner window. `AiControlled` rows can either keep an empty path and display `继承场景默认：...`, or bind a validated StreamingAssets JSON profile override. Non-`AiControlled` rows show the field disabled because they do not consume Runtime AI Planner profiles. Invalid JSON remains visible for repair, but cannot be bound as a scene default or character override.

The character module shares the modular `mx.level.authoring.v3` envelope with `characterTest.worldObjects`. Saving one module must upsert its payload without dropping the other module. Apply writes low-frequency scene caches: world objects to `GameManager.WorldObjectSceneCache` and characters to `GameManager.CharacterSceneCache`; runtime Tick still consumes compiled/runtime data and never reads authoring JSON.

## Play Monitor

Play Monitor 只读取 Runtime AI Planner debug snapshot。

Agent 列表应优先展示当前状态、目标、动作、距离、可见/遮挡、路径、score/cost 和 last command。列表行不直接拼接一整段 report 文本，而是先把 `CharacterTestRuntimeAiPlannerAgentSnapshot` 转成 editor-only view model，再由 UI Toolkit 行和 chip 展示。这样 Agent 和主创能先看到“谁在做什么、目标是谁、为什么做、哪里被阻塞”，再按需展开细节。

Play Monitor 刷新时应先捕获一次当前 `CharacterTestRuntimeAiPlannerEditorDebugRegistry` 的 typed snapshot，并让 Agent 列表、右侧 Inspector 和选择状态复用同一批 snapshot/view model entry。不要让同一次 UI 刷新中列表和 Inspector 分别调用 `SnapshotProvider()`，否则多 agent 或未来自动刷新时会放大 runtime debug adapter 成本。完整文本 report 只在用户点击“复制报告”时按需生成；普通刷新只显示结构化行、分组 Inspector 和 Raw fallback。

选中 agent 后右侧按以下分组展示：

- `决策摘要`：decision headline、条件摘要、异常/遮挡/路径 chip。
- `目标与感知`：target、distance、visible、blocked、last seen、threat、in range。
- `移动与路径`：path available、direct path、candidate index、movement speed、strafe side、hold/reaction frames、environment obstacle/hazard/useful facts。
- `Utility`：selected action、action kind、score/cost。
- `命令输出`：last command、reject/suppress reason、recent transition。
- `Raw`：原始 fact key/value fallback，供排查和复制报告使用，默认折叠。

如果需要更多解释数据，应扩展 CharacterTest adapter debug snapshot，而不是让 Tick 高频路径每帧构造字符串、临时集合、重读 JSON 或查询 Unity 对象。

Play Monitor 页签可以使用和编辑页签不同的布局：左侧仅保留只读上下文和当前 profile 提示，右侧 Inspector 至少保留 380px 最小宽度，并使用可滚动分组、自动换行和状态 chip，避免固定 label 宽度导致字段和值互相覆盖。Play Monitor 仍然不能提供保存、热更新或修改 runtime 对象的入口。

## SceneView Runtime Preview

SceneView 预览是 Play Monitor 的只读辅助视图，只在 Play Mode 且用户显式开启 `场景预览` 时工作。默认关闭，避免大场景和多 agent 会话在 SceneView 中持续重绘。开启后刷新固定节流到 5 Hz，并且只在 `CharacterTestRuntimeAiPlannerEditorDebugRegistry` 有可用 target 时触发 `SceneView.RepaintAll()`。

预览数据来源是当前真实 Runtime AI Planner debug snapshot。Runtime 侧只允许补 typed debug 字段，例如 source center、target center、target direction、facing delta、decision move vector 和 attack range；这些字段只供 Debug/Editor consumption，不进入 SaveState、network snapshot、replay/result hash，也不改变 Runtime AI Planner JSON profile schema。`CreateSnapshot()` 只能投影 `Sense` / decision 阶段已经算好的派生数据，不得在 snapshot 阶段重新查找 Unity 对象或重新计算昂贵空间查询。

Play Monitor 控件提供两种模式：

- `选中 Agent`：只画当前 Play Monitor 选中行；没有选中行时默认取第一个 agent。
- `全部 Agent`：画当前 snapshot 中的全部 agent，用于小规模调试，不作为默认实时监控模式。

Play Monitor 还提供 SceneView 叠加层显示选项：

- `标签`：`Off`、`Issues`、`Selected`、`All`。默认 `Selected`，只给当前选中 agent 显示固定三行摘要：Agent/state、action/score-cost、target/distance/visible-path；存在 issue 时追加一行 issue。`Issues` 只显示 warning/error agent 的标签，`All` 需要显式开启。
- `显示图例`：默认开启，在 SceneView 左上角显示颜色含义、selected/all、label mode、5 Hz 刷新率和当前绘制 agent 数。
- `显示目标线`、`显示攻击范围`、`显示移动箭头`：默认开启，可独立关闭以降低标签和线条密度。

SceneView 使用 `Handles` 绘制 source/target 点、目标连线、attack range 圆、decision move arrow、简短 label 和图例。颜色语义固定为：source marker 用 agent 整体 severity；target marker 用目标可见/遮挡状态；LOS/target line 绿色表示可见/直通，黄色表示遮挡、绕行或 candidate path，灰色表示无目标或几何不可用，红色表示 reject、path unavailable 或 hazard；attack range 圆在目标处于射程内时高亮，否则低亮；decision move arrow 用移动/path 语义，path unavailable 时转红。预览不暴露底层 `CombatQueryResult` 列表，只展示 blocked、candidate index、distance、action、state、score/cost 等只读摘要。

如果需要更多可视化解释，优先扩展 CharacterTest runtime typed debug snapshot，再在 Editor 低频层转换成中文标签、颜色和 Handles 绘制。不要在 Tick 高频路径拼接展示字符串、构造 UI view model、遍历 Unity scene object 或执行新的 SceneView 专用查询。

## 诊断与模板

编辑器诊断分三层：

- validator report：全局 profile 校验结果。
- 局部诊断：状态、切换、动作列表和 Inspector 上的当前项问题。
- Play Monitor explanation：运行时为什么选中某个 state/action/command。

常用条件模板当前覆盖：

- 目标可见
- 目标被遮挡
- 目标记忆中
- 进入攻击范围
- 动作未执行
- 靠近危险
- 靠近可利用物

行为模板当前覆盖：

- 默认近战：保持追击 + 轻攻击兼容。
- 感知近战：演示 visible、blocked、lastSeen 和 hazard facts。
- 远程风筝：近身/高威胁时撤退或横移，可见且合适距离时输出 `CastAbilitySlot`。
- 危险游斗：危险优先脱离，路径清晰时追击，可攻击时轻攻击，遮挡时横移或等待记忆目标。
- 技能施法：目标可见且进入范围时优先技能槽输出，动作中等待，危险/近身时撤退。
- 激进冲锋：发现目标后快速压近，近身后轻攻击，遮挡时横移或继续追压。
- 守卫防御：靠近可利用物或障碍时守位，目标进入范围后反击，危险时优先撤退。
- 伏击者：无目标时潜伏，发现目标后跟踪，进入范围时优先技能槽突袭，遮挡时换位。
- 控制控场：保持控场距离并使用技能槽，危险或被近身时规避，遮挡时横移布控。

所有模板从 v3 起都包含 profile-level movement defaults；动作 Inspector 可以对单个 action 启用 movement override。该 override 只写回 JSON DTO，不改变 action kind 或 command schema。运行时仍通过 `SetControlIntent` 输出 move vector，由 CharacterTest motion solver 处理加速度、减速度和转向。

新增 facts 后，必须同步更新 `CharacterTestRuntimeAiPlannerEditorFactCatalog`，保证 facts 元数据覆盖 runtime facts，并为值类型、operator、默认值和中文说明提供明确规则。

## 维护边界

Runtime AI Planner Editor 不新增 `MxFramework.AI` 公共 API。

保持这些边界：

- 不改 runtime 高频决策路径。
- 不在 Editor 中直接保存 Unity runtime 对象。
- 不把 CharacterTest profile schema 上推为通用框架 schema。
- 不重新引入 ScriptableObject profile authoring。
- 不把 Play Mode hot update 偷渡进保存流程；行为组 debug gate 只能作为显式会话 overlay。
- 不让 Editor helper 进入 Runtime assembly。

Editor 代码是低频路径，可以使用 UI Toolkit 控件、字符串、列表和 report 文本；但这些分配不能进入 runtime Tick / command path。

## 验证入口

编辑器改动的最低有效验证通常是：

- `git diff --check`
- `dotnet build MxFramework.Game.CharacterTest.Editor.csproj --no-restore`
- `dotnet build MxFramework.Tests.csproj --no-restore`
- Unity refresh/compile + Console error check
- focused EditMode：`CharacterTestRuntimeAiPlannerFormalizationTests`
- 触及 C# 时跑 `Tools/Harness/check_code_standards.py` report-only

纯文档变更可降级为：

- `git diff --check`
- `python Tools/Harness/check_docs_health.py <changed-paths-file>`

Unity Test Runner 有时会留下 `Saving results to ... TestResults.xml` 的 exception-like Console 记录；如果 focused tests 已通过，应重新读取实际 error entries，不要把这条记录误判为项目编译错误。

## 后续路线

编辑器后续优先级：

- 多条件组合编辑：支持 AND 组、条件复制和更清晰的非法组合提示。
- Profile 管理：行为库 picker 已覆盖多 JSON profile 列表、搜索、复制和基础校验状态；后续继续补差异检查和场景/角色绑定一致性批量报告。
- Facts 体验：按行为意图推荐 facts 和模板，而不是只按 fact key 分组。
- 决策解释：Play Monitor 增加 score breakdown 和命中条件明细。
- 可视化增强：只读视线 / 环境采样简图；仍不做节点图编辑。
- 代码结构：继续控制单文件大小，窗口、model、facts catalog、流程图和 debug monitor 各自保持清晰职责。
## Arena Behavior Card Capability Slots

The Arena behavior group setup page is the first authoring surface that uses the behavior-card capability model as its primary workflow. It is intentionally separate from the Runtime AI Planner editor behavior group tab for now. The Arena page is a low-frequency test harness UI, not a runtime Tick path and not a public `MxFramework.AI` API.

The page is organized around capability coverage instead of a library-versus-list layout:

- Header: current Arena draft, compile status, minimum runnable coverage result, and the return-to-team-settings command.
- Capability slots: Targeting, Engage, Damage, Fallback, Pursuit, Control, Kite, Defense, Navigation, and Support. Each slot shows whether the current deck satisfies it, is missing it, only has disabled cards for it, or has a risk.
- Card library: only shows paged/search-filtered behavior cards that provide the selected capability. This prevents the UI from rendering the whole behavior definition catalog when it grows to hundreds or thousands of cards.
- Deck cards and card detail: only cards already added to the Arena draft compile into the test Runtime AI Planner profile. Deck cards can be copied, reordered, deleted, temporarily disabled, and parameter-overridden.
- Forecast table: fixed Arena scenarios show the predicted selected card, output action, reason, and risk. This forecast is static authoring feedback and does not simulate combat, damage, animation timing, path length, or victory.

`Targeting`、`Engage or pressure`、`Damage` 和 `Fallback` 是行为组的四个物理基础契约槽：每槽恰好持有一个兼容行为实例，固定在自由行为之前，不能删除、禁用或拖出，只能原子替换。`Engage.NoOp` 与 `Damage.NoOp` 是显式结构占位行为：它们满足槽位能力契约，但不会生成移动、攻击、能力或伤害动作。`Pursuit`、`Control`、`Kite`、`Defense`、`Navigation` 和 `Support` 仍是导航、风险分析和卡库筛选能力；同一行为定义可以实例化到不同槽，但单个实例不能同时占据多个基础槽。自由行为的 `disabled=true` 继续表示保留在草稿与诊断中、但不编译为可执行候选。

旧版 `Fill Basic` 由共享基础槽迁移策略取代。新建普通组安装最近敌人、接近目标、基础攻击和等待/保持；木桩安装最近敌人、不接敌占位、不输出占位和等待/保持。旧草稿加载时优先复用现有启用兼容实例、其次重新启用兼容实例、最后才添加内置推荐行为；旧木桩的接近与攻击会确定性替换为无动作占位，并将草稿标记为待保存。该策略只运行在创作、加载、发布和启动预检等低频路径，不改变 SaveState、network、replay、hash 或 Runtime AI Planner Tick。

Editor presentation helpers may build strings, rows, chips, search results, and diagnostics in this low-frequency layer. Runtime AI Planner Tick must still consume only the compiled evaluator/profile slot and must not read UI state, search text, card labels, capability names, or forecast text.
