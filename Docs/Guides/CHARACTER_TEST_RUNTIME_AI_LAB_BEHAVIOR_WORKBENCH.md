# CharacterTest Runtime AI Lab Behavior Workbench

> 2026-10-10 新玩法已定、待实现：[主卡与配件基石](../../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)取代独立准备卡先出牌设计，固定预装与随机双牌库共用基础。本文下述CardTrial选准备卡、独立确认、opt-in及试用参数均为现有旧合同实现；新的组合试验入口与迁移任务归[开发方案](../Plans/TacticalCardAssemblyDevelopmentPlan.md)。底层行为制作／时间轴观察继续保留，不因配件设计撤除。本次未操作Lab或运行验证。

> Status: Guide
>
> Date: 2026-10-10（独立配置与观察页；下文历史设计保留原范围）
>
> Scope: 真实出牌试验的当前入口／owner／边界见第一节；后续总体工作台内容包含历史设计与目标，不表示全部已交付。
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/*CardTrial*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/*CardTrial*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/GameManager.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/*CardTrial*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink.RuntimeTestSetupCommands.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/TacticalCardHud.CardTrial.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalCardTrialOptions.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalTestPreparation.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalDeckManager.TestPreparation.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/TacticalStageFlowCoordinator.TestPreparation.cs`
> Code-Doc: `FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/Components/RuntimeAiLabCardTrial*.xml`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/TacticalCard/TacticalCardTrial*.cs`
> Code-Doc: `Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/Program.CardTrialContract.cs`

> Runtime-test visual target: [single-surface phase 1](CHARACTER_TEST_RUNTIME_AI_LAB_UX_LAYOUT_DESIGN.md#runtime-test-single-surface-convergence-phase-1-target).
>
> Authoring visual target: [Runtime AI Lab UX and layout design](CHARACTER_TEST_RUNTIME_AI_LAB_UX_LAYOUT_DESIGN.md#target-implementation-reference).
>
> Draft contract: [Runtime AI Lab draft model and persistence](CHARACTER_TEST_RUNTIME_AI_LAB_DRAFT_MODEL_PERSISTENCE.md).
>
> Player gameplay and publish contract: [Runtime AI Planner behavior-card gameplay and Runtime AI Lab](CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md).
>
> Single-behavior authoring authority: [单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md).
>
> Behavior-recipe timeline authority: [行为配方大时间轴设计](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md).

<a id="真实出牌试验第一步2026-10-08-已批准未实施"></a>
<!-- 保留旧入站锚点；当前实现以以下内容为准。 -->
## 真实出牌试验：当前实现与边界

本节是独立真实出牌试验的工程权威说明。2026-10-09 对照 main `8588a754` 的 CardTrial 源码及功能owner回执核对，修正旧“已批准但未实施”的陈述；单行为观察与这套独立试验分别判断。模块合同见[CharacterTest TacticalCard](../Interfaces/CharacterTest.TacticalCard.md#新效果机制与ai-lab接入状态2026-10-08)，玩法预期仍归[行动卡与交互效果卡设计基石](../../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)，本节不改玩法规则。

### 入口与当前行为

打开 `Assets/Scenes/CharacterTestRuntimeAiLabScene.unity`，Play 后在实验设置页点击「真实出牌测试」。`OpenCardTrial` 仅允许从 Setup 进入；若仍处于行为观察，先重置观察。独立页面通过正式Composer产物加载，不把原单行为预览改成正式牌局。页面可配置初始手牌及动作绑定、初始资源、种子、对手与测试准备费用，校验后创建会话；同条件重置使用已冻结配置，不把后续Draft编辑混进原试验。

- owner 为 `CharacterTestRuntimeAiLabCardTrialSession`，通过原 session builder 创建临时 `TacticalDeployment` 并复用 `TacticalMixedPlayerCardRuntime` 的Deck、Ledger、Stage和CommandPump；页面只提交既有选择、目标／落点、确认、取消及重置入口，不另建费用或战斗权威。
- `CharacterTestRuntimeAiLabCardTrialContent.Freeze` 使用显式初始手牌容量与试验选项；准备卡不进入补牌来源，行动卡的试验备用牌来自明确配置。这是Lab局部opt-in，不把生产Stage的容量约束或卡组规则改写为通用默认。
- `TacticalTestPreparation` 与 Deck／Stage 的专用partial已实际接入：选择先预留，确认通过既有事务只支付一次、移动一次牌并建立一次准备状态；准备激活保持世界暂停、不补牌，取消释放原预留。状态绑定实际Actor身份／generation，动作结算与取消不会误清理准备；session结束、owner重绑或控制释放按原清理入口处理。
- 实际页面读取费用／卡区／准备状态、世界帧、事务revision及角色身份。`Content.Load`、`Describe`和已有卡面投影从正式已发布目录取得角色／卡／动作名称与描述；字段、枚举及默认值继续由这些源与编译／发布投影维护，本文不复制一份默认数值表。
- 临时Lab配置不打开玩家持久化service/store，返回行为观察释放独立会话并重置原观察。不能据此宣称正式玩家库存、整场战斗存档、联网恢复或有限R2外的恢复已接线。

### 独立配置页与试验观察（2026-10-10，已实现）

真实出牌测试分为独立不透明「试验配置」页与保留场景的「试验观察」页。初版使用紧凑列表；用户后续要求正式卡面与角色头像，当前配置页左侧以头像选择双方角色并编辑初始参数，右侧以横向可滚动的正式卡面显示初始手牌。卡牌和动作通过目录选择界面直接选取，支持复制、替换和移除。观察页不展示配置表单，只提供出牌、取消、冻结条件、同条件重试和修改配置。

- 「添加卡牌／换卡」打开独立选择界面，支持名称／效果搜索及行动卡、交互准备卡、测试工具卡分类；动作候选来自当前玩家角色的正式可用目录。选卡确认时自动装配动作：换卡优先保留仍兼容的已有绑定，否则优先使用角色为该卡配置的默认绑定，再取按动作显示名称、稳定ID排序的首个可用动作；有多个候选也无需先手动装配。卡库卡面／右侧预览与最终加入手牌使用同一解析规则，并显示实际装配动作，仍可手动更换。更换玩家时保留仍兼容的绑定，失效绑定自动改为新角色可用的默认动作；无可用动作时明确提示更换角色。零张手牌、非法数值或缺少必要动作绑定均不能开始。
- 开始后进入观察页，本次角色、初始动势、种子、测试准备费用、手牌与动作可通过「本次条件」只读查看。配置和选择页覆盖场景输入；观察页保留有效地面选择入口。独立组件注册在 Panel 层，防止之后创建的战斗 HUD 遮挡页面标题。
- 「修改配置」保留当前会话、执行状态和牌区，暂停固定帧并屏蔽游戏操作；返回当前试验继续原会话，已有菜单暂停仍保留。新开试验必须经过明确的替换确认，取消确认保留当前试验；「同条件重试」只使用已冻结配置，不读取后续配置修改。
- 原 session 仍是唯一 owner，重建时先设置冻结数据与观察页状态再重新绑定 UI；不得在重建后恢复已释放的旧时间 adapter。目录构建、筛选和配置编辑是低频可分配操作；观察信息沿用最多 10Hz 的只读刷新，未宣称 UI 全路径零分配。

#### 正式卡面与角色头像（2026-10-10 后续调整）

配置卡、选卡库、选中预览与冻结手牌均嵌入已有 `ui.component.tactical.player_card_face`，沿用战斗卡面、主题、费用、插图、名称和正文。配置预览只读投影正式 published source、当前角色与动作绑定，不创建 deployment、不启动试验、不提交命令；正文继续使用共享 `CharacterTestTacticalCardPresentationProjection`，不从卡名或描述反向解析规则。

双方头像、角色候选头像与选中角色大图沿用 `CharacterTestFairyGuiCharacterImageBinding`，根据角色定义的 `PresentationImages.ResolveResourceKey(false)` 从同一 ResourceManager 加载，保持比例、共享同 key 句柄、复用行时更新绑定 key、关闭 surface 时释放资源。头像与名称分别显示，图片内不叠加按钮标题。当前正式目录登记的是角色包的 `ui_portrait_placeholder`；本批接通头像 UI，不把占位图描述为真实角色美术，也不擅自绑定角色库概念图。正式图交接继续遵循[角色头像与立绘契约](CHARACTER_PRESENTATION_IMAGES.md)。

本次 Composer 发布回执 `f0a1834f0ac644198f58431f68592d37`（Apply `37b039ff04c84a76aa49190c2644db70`，Publish `bb1ef405c4824cd8b3fec18021aeb7e4`）。Global Player Resources 已重建，头像加载回归3/3通过。已从真实发布组件读取标准卡面资源、4张配置卡和256×256登记头像，确认卡库搜索使用卡面 tile 而非文本条目；最终视觉与后续往返回执保存在本地 `Logs/CardTrialUiRedesign/`，不版本化截图。

后续原生检查已覆盖角色候选头像／大图、正式卡库 tile／选中卡面预览、添加与移除、冻结手牌复用同一卡面以及修改种子74后同条件重试仍为73；已查看1280×720配置与卡库截图。最终脚本编译无错误、域重载与Editor ready均有回执；`validate_unity_scripts` 将两条 `Debug.Log` 生命周期信息误报为Exception，保留该失败回执，并按允许的失败回退读取原生 `UnityEditor.LogEntries.GetCountsByType`：实际 errors=0、warnings=7，未清空Console或删除日志。

#### 观察页减少遮挡（2026-10-10 后续调整）

观察页隐藏整条页头背景、标题、说明和原页头返回按钮。常驻内容仅为右上角「试验菜单」、下沿单行动势／阶段／回合状态和缩小后的正式手牌；同条件重试、本次条件、修改配置、返回 AI Lab 收入展开菜单。牌区数量诊断放入主动打开的本次条件面板，该面板提供独立关闭按钮。确认、取消和定点提示只在有待确认手牌时显示，空反馈不显示。

观察手牌使用独立 `card_trial_hand_card` 220×308 测量外壳，内部仍绑定378×528正式卡面并等比缩放，保持虚拟列表真实尺寸和水平滚动；悬停沿用共享完整说明 tooltip。配置、卡库和冻结预览的尺寸及头像绑定保持既有实现。观察页根组件透明，仅实际 UI 区域拦截输入；出牌、取消、定点、重试继续提交同一 HUD/session 入口。

本次最终 Composer 发布回执 `d7732207d42848b2ba40d440c7f67e82`（Apply `ce51c3ae527a4a29be08650c93300c2c`，Publish `ab8c02c9bf2b4cb2ba335a57ddd09d12`），正式源计划检查通过。脚本验证任务 `bb8dfc57b03f4c53a12ea6f5be171822` 已确认 refresh completed、compilation started/finished、domain reload observed、Editor ready 与 Console errors=0；原有编译警告仍保留。

原生检查已查看1920×1080与1280×720观察页；标准卡面外壳实测220×308、内部缩放220/378，场景中部HitTest不命中本页而卡牌命中其手牌区域。通过真实发布控件回调验证展开菜单、冻结条件开关、定点卡选择、有效点提交后确认进入Executing、取消后隐藏出牌控件、修改／继续保留同一Stage引用、同条件重试重建Stage且冻结种子仍为73、返回AI Lab释放本页。定点选择沿用同一HUD提交入口；未将这些回调检查描述为实际鼠标点击或独立战斗规则测试。截图保存在本地 `Logs/CardTrialUiRedesign/`。

最终发布后已重建Global Player Resources，1280×720定点提示实测正文高度60、容器高度100，范围说明完整且不重复显示备用提示；最终画面为 `observation-compact-final-1920.png` 与 `observation-point-selection-final-1280.png`。检查后恢复原QHD尺寸和Gizmos偏好，移除本次临时1280预设。

#### 选卡自动装配动作（2026-10-10 后续调整）

`CharacterTestRuntimeAiLabCardTrialContent.ResolveActionBinding` 统一解析选卡预览、添加／替换和更换玩家时的草稿绑定。自动选择遵循本页上述顺序；无需动作槽的卡清空动作绑定，手动改选的兼容动作优先保留。该选择仅写下一次试验草稿，开始时仍通过原Freeze严格校验并形成显式物理动作绑定；配置页操作不会改动本次冻结手牌、角色默认资产或正式目录。

配置回归13/13通过（任务 `2b0d97b491ab485b8f0de29af421db48`），新增两项覆盖多个候选的稳定默认选择、角色预设优先、兼容选择保留、无候选，以及更换角色后的部署绑定与既有冻结数据隔离。脚本验证 `1773762f7a014446aba66365a7a0169b` 确认编译无错误、刷新完成、域重载与Editor ready；同一已知Console分类问题把3条生命周期Info误报为Exception，按失败回退读取原生Console计数，实际errors=0。

原生发布UI回调验证：当前角色有115个可用动作，选择「引敌入锋」后卡面和侧栏立即显示「持剑四连击 01 · 第1段」，直接加入第5张手牌即可开始，冻结绑定为 `action:455`，会话进入Planning且Issue为空；全程没有先打开装配动作选择器。更换玩家后兼容动作保持有效、开始按钮可用，本次冻结角色保留；检查后恢复草稿角色并继续原试验。

| 需求 | 复用 API / owner | 范围 |
|---|---|---|
| 固定帧、命令、实体、技能、移动、Combat、属性与效果 | 既有 RuntimeHost、TacticalMixedPlayerCardRuntime、Stage、CommandPump、正式角色 adapter | 保持既有运行权威；UI 只提交原有出牌入口 |
| 配置与冻结 | CharacterTestRuntimeAiLabCardTrialSession、Content.Freeze | 下一轮配置与本次冻结条件分离；明确重开后才换条件 |
| 页面往返与暂停 | 同一 session owner、TacticalTimeCoordinator UserMenu gate | 修改期间保留试验；返回释放本页取得的暂停，不释放原先存在的菜单暂停 |
| UI、输入与资源 | MxFairyGuiNavigator、UI Composer、既有按钮语义资源、ResourceManager / Catalog | 正式组件由 helper Apply/Publish；鼠标选择不穿透配置/选择界面 |
| 配置目录与诊断 | 正式角色、战术卡及动作目录，现有 HUD / Stage 只读投影 | 不复制卡规则或建立另一套 Runtime 目录 |
| 存档、网络与回放 | 既有边界 | 本次不新增玩家持久化、场景切换、存档或回放协议 |

正式 Composer Apply/Publish 已完成，操作回执 `05162449e6b8444d93c6334fccce7e49`（Apply `84088d6d21254e3383689291f77617a2`，Publish `93082f178ec34dca913e1011690d50be`）；生成器入口为 `Tools/MxFramework.FairyGuiUiComposer/scripts/update_card_trial_layout.cjs`，随后运行既有 C# contract writer 和 `--check-ai-lab-card-trial-plan`。源 XML、package mapping、发布 bytes 由 helper 维护，Global Player Resources 已重建。

本批验证：配置 EditMode 11/11 通过；既有出牌回归 13/14 通过。未修改的 `TacticalCardTrialTests.InsufficientMomentumAndInvalidActorDoNotPayOrActivate` 独立复跑仍失败：原断言期待 `InvalidSpawnBinding`，而 HEAD 中现有 `ReconcilePendingAvailability` 会先取消失效 pending、增加 snapshot revision，再拒绝旧确认命令为 `StaleSnapshot`。该 fixture、Stage、Time、Driver 源文件均与 HEAD blob 一致，不由本次 UI、session 或 GameManager 改动触发；不改断言或出牌权威来掩盖此项。

真实 `CharacterTestRuntimeAiLabScene` 中已通过正式包入口验证搜索／分类、添加、复制、移除、角色和动作选择、开始后隐藏配置、冻结条件、返回同一会话、取消重开、73→74 后同条件重试仍为73／明确新开后为74，以及测试准备预留5、取消归零、确认只扣一次至45。执行中进入配置页可保持 `Executing`、世界暂停与帧1不变，并保留菜单暂停。已查看2560×1440与1280×720的真实截图；截图临时关闭Editor Gizmos，不据此改变游戏资产或发布规则。Unity编译／域重载、Editor ready和Console error=0有本批回执；本范围不等于所有自然键鼠输入、所有分辨率、完整战斗规则或Player构建均已验收。

### 验证与未完成边界

owner在2026-10-08的主合／正式Composer发布回执包含N1–N7通过、保护回归65项与主树558项，以及非诊断存档边界证据；这些按该批次读取。本次只核对源码与回执，没有运行Unity或重跑这些测试。`TacticalCardTrialTests`／`TacticalCardTrialConfigurationTests`定义覆盖容量opt-in、冻结重置、准备预留与一次提交、取消、失败不付费和身份清理，不以测试定义存在代替执行结果。

2026-10-09 UI专项已由owner在聚焦的真实GameView验收：通过正式入口与开始操作显示4张正式手牌，50／73／5在开始前后可见，原生输入73→74→73编辑回显通过，冻结种子保持73，后续编辑作用于下一轮试验；完整角色名称、卡牌／动作分行及长描述滚动已有回执。此前数字字形显示异常来自后台GameView缓存帧误判，聚焦真实GameView后原生FairyGUI数字输入正常。正式Unity编译通过、聚焦配置测试10/10、Console error为0；这些是该次专项证据，不外推为所有自然操作、跨分辨率UI或更广玩法回归通过。

本轮试用卡的整张卡悬停使用既有 `TooltipText(data)` 和 Tooltip 服务显示共享正文、效果详细规则与当前操作提示；离开卡片、隐藏界面、退出 owner 时隐藏或释放浮窗。五张正式交互准备卡已在真实 GameView 逐张触发原悬停事件并查看截图，效果窗口 `F+600`、每回合限制与清理说明可读；不把该显示验收外推为新增玩法、自然输入事务或全仓回归。提示识别与投影合同见[战术卡描述显示契约](../Interfaces/TacticalCard.DescriptionFlow.md)。

测试准备只记录可观察状态，没有替身避伤／换位、体型缩放效果，不能当正式替身／体型机制或三张r1机制卡改版完成。后续新行为须依各自批准合同实施，不把设计预期写成已有能力。

既有正式三张机制卡的发布及整备领取／装配另见[战术内容指南](CHARACTER_TACTICAL_CONTENT.md#整备中的机制试用卡领取与装配)。本节只拥有独立Lab试验说明，不覆盖其正式制作owner。

## 2026-08-07 单行为创作收口

Runtime AI Lab 的项目行为创作界面现已收口为“单个行为卡草稿 + 测试组合 + 双方阵容测试设置 + 可拖动 RuntimeFrame 预览”。该界面不编辑、保存或发布正式行为组；测试组合只属于当前行为草稿的本地测试上下文，也不会进入项目行为组 JSON。

本文继续保留 Runtime AI Lab 的总体运行、数据权威和历史能力说明。凡本文后续涉及以下内容，均由[单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md)覆盖：

- 在当前行为编辑界面选择、新建或完整编辑正式行为组；
- 把 Behavior Group、Player Deck、Facts、A/B、分支重算或 Replay 作为单行为编辑器第一版的常驻功能；
- 使用 `Apply F+1` 把编辑结果混入当前单行为预览历史；
- 让测试组合获得正式组 ID、项目 revision、项目路径或发布能力。

当前源码中的隔离单卡 authoring group、正式行为组工作副本和旧 Workbench 路由是后续迁移输入，不是单行为编辑表面的目标产品模型。

## Summary

`CharacterTestRuntimeAiLabScene` 的长期定位是 Runtime AI Planner 行为开发工作台。当前行为创作主线是在同一个独立 3D 场景中编辑一张具体行为卡，用本地测试组合与敌我双方阵容验证行为间的组合效果，并通过 RuntimeFrame 时间线逐帧观察修改后的真实 gameplay 结果。正式行为组创作不属于该界面。

本工作台界面不是玩家界面，也不是 `MxFramework.AI` 公共编辑器，不替代 Ability 或 CharacterAction 专用编辑器；但它设计、测试并发布的必须是项目 JSON 中的同一套 Behavior Card。Player Deck、本地玩家卡库和玩家合法构筑继续保留为独立功能，不再作为项目行为卡创作的默认入口。Unity Editor 静态 Runtime AI Planner 窗口暂时降级为底层配置、兼容入口和故障排查工具。

## Product Position

Runtime AI Lab 必须形成以下闭环：

```text
打开项目行为卡具体版本
  -> 创建 copy-on-write Behavior Card Draft
  -> 编辑单个行为的逻辑和参数
  -> 用测试组合加入固定版本陪测行为
  -> 配置敌我双方测试阵容和站位
  -> 校验、冻结并从第 0 帧重跑
  -> 暂停、拖动和逐帧检查 RuntimeFrame 预览
  -> 继续修改并明确重跑
  -> 发布行为卡新版本
```

专用 Lab 场景仍默认进入 `Setup / 实验设置`。该设置页的行为组选择器同时显示正式项目行为组与明确标记的本地行为组草稿；草稿只供当前实验选择和恢复，必须由用户显式“确认为正式行为组”后才进入项目目录。进入单行为创作后使用独立的“行为测试设置”变体。测试组合不受 Player Deck `6` 槽、`10` 费约束，但它只能用于当前行为测试，不能发布或伪装成正式行为组、玩家卡组或合法项目构筑。

核心使用者是 Runtime AI Planner 开发者和行为设计者。工作台首先回答：当前行为为何被选择、空间和动作链是否正确、修改从哪一帧生效，以及同一历史输入能否重演出相同 authority 结果。

## Simulation Lifecycle

- simulation 固定为 60 Hz。
- 默认帧上限为 900 `RuntimeFrame`，即 15 秒。
- 不因胜负、无动作、目标丢失或其他 gameplay 结果提前停止；只在达到帧上限或用户主动退出模拟时结束。
- 模拟必须实时生成并播放，开发者可在运行过程中观察场景并调整 Lab Draft；不以预先离线算完整段结果作为主流程。
- Start 完成后保留一个不可变的预热初始基线。普通历史回看使用该基线、历史外部命令和行为 revision 事件正向重演，不依赖自动中途状态恢复。
- 时间线记录 `RuntimeFrame`、外部权威命令、authority hash、不可变行为 revision 事件和必要的 session-only presentation input。表现数据不进入 authority hash 或正式网络同步合同。

## Complete Editing Surface

工作台目标是完整行为编辑，不只做数值调参。

### Behavior Test Composition

- 当前行为草稿必须且只能出现一次，可以调整仲裁顺序但不能删除或禁用。
- 陪测行为固定引用项目行为卡具体 revision，支持添加、删除、启用、禁用和排序。
- 测试组合跟随当前行为草稿本地保存，不进入项目行为组目录。
- 测试组合不提供正式组 ID、组级参数、保存、发布或转换为正式行为组的操作。

### Behavior Card

- 添加、删除和排序步骤。
- 选择标准步骤模板。
- 引用 CharacterAction，并只读预览动作时长和关键窗口；行为配方不再直接创作 Ability Event。
- 编辑进入条件、执行中监听条件、完成出口、失败出口和条件分支。
- 编辑判断窗口、分支目标和显式 `End / 交回行为组重新决策`。

### Runtime AI Planner Settings

- 当前行为可以编辑自己 schema 明确拥有的条件、移动策略和动作参数；不得在本界面编辑正式行为组的目标选择、感知、攻击距离、移动或 Utility 参数。
- 测试组合固定继承明确项目行为组 revision 的组级运行基线；来源缺失时阻止重跑，不使用 C# 默认值静默兜底。
- 条件和动作输出只能使用受支持的 typed facts、标准条件积木、步骤模板和安全 action primitive。
- 禁止任意 C#、脚本注入、未经校验的 raw JSON 和运行时上传未知行为类型。

Ability 与 CharacterAction 仍由各自专用编辑器负责动作时间线、命中帧、root motion、VFX 和 gameplay request。Runtime AI Lab 行为配方只引用 CharacterAction，并负责预览和诊断这些结果。

## Live Edit Semantics

- UI 修改先进入 `Lab Draft`，不得直接改写运行中的 evaluator。
- 行为、测试组合或阵容修改只把当前预览标记为“需要重跑”，不得原地改写旧 RuntimeFrame 历史。
- 开发者点击“应用并重跑”后执行低频校验与编译，冻结当前行为、固定版本陪测行为、双方阵容和组级运行基线。
- 编译和新 session 初始化全部成功后才切换到新的不可变 Run Snapshot，并从第 0 RuntimeFrame 开始播放。
- 同一段单行为预览历史只对应一个 owner behavior revision，不在该表面执行 `Apply F+1` 热更新。
- 编译失败时保留旧 evaluator、Run Snapshot 和时间轴；失败草稿与具体诊断保留在编辑侧，不能静默替换或清空。
- 运行时编辑不得通过自动中途 authority snapshot 恢复掩盖不确定性。Authority hash 决定逻辑重演是否通过，表现差异单独进入 Presentation Diagnostics。

## Workbench Layout Principles

- 单行为编辑视图同时保留大尺寸 3D 场景、精简 RuntimeFrame scrub 时间轴、次要测试组合区和占据主要编辑空间的当前行为逻辑区。
- 顶栏只显示返回、当前行为名称、简短状态、测试设置、应用并重跑和发布行为版本。
- “行为测试设置”是同一 Shell 内复用初始实验设置逻辑的完整视图变体，不是遮挡编辑内容的抽屉。
- 测试组合使用真实 FairyGUI `GList` 承载完整行为卡集合，由列表自身负责裁剪、滚轮与拖动滚动；不得再以固定数量的卡槽和手写滚动条模拟列表。
- `编辑逻辑` 页的组合卡片只承担行为身份与编辑入口。启用/禁用和删除在选中卡片内部按需出现；运行状态、行为类型和优先级不作为该编辑列表的常驻并列列。
- RuntimeFrame 时间轴第一版只承担播放、暂停、逐帧、返回 latest 和拖动预览进度；完整五轨、Facts、A/B、分支重算与 Replay 不常驻。
- 任何区域都不得把测试组合表现成正式行为组资产，也不得让技术 ID、hash 和项目路径挤占行为编辑与预览空间。
- 高级帧同步探针、手动状态采集、状态同步和冷启动校验不进入主工作流，只在高级诊断区显示。Raw facts/hash/id 仅作为内部重放与一致性证据保存；所有 Runtime AI Lab 界面与 Tooltip 都显示其语义化中文投影。
- 任何面板都不得遮挡中央场景的主要交互区域；编辑与观察必须能同时进行。

## Data And Authority Boundaries

- Runtime Tick 只读取已经编译的 evaluator/profile slot，不读取 Lab Draft、UI 状态、搜索文本或展示文案。
- Lab Draft、行为编辑器 view model、中文标签和编译报告仅存在于低频开发工具路径。
- 正式 Frame Sync 仍以固定 Tick、稳定顺序的外部命令、确定性配置/RNG 和 authority hash 为准。
- Lab behavior revision 是当前 Lab session 的确定性重演输入，不进入 network wire、SaveState 或正式 replay wire。
- 感知缓存、距离、路径摘要和 UI projection 是可重建派生数据。
- 动画、骨骼、IK、音频、VFX、Overlay 和 HUD 属于表现或诊断，不定义 authority 成功。
- 中途状态恢复继续作为显式手动测试工具；正常回看使用预热初始基线加历史输入重演。

## Save And Publish Layers

保存、发布和玩家装配必须区分六层：

1. **Lab Draft**：正在编辑、允许编译失败的工作副本。
2. **Validated Draft**：通过 schema、引用、compiler 和短局模拟验证的候选版本。
3. **Published Behavior Card**：进入生产 catalog 的稳定卡 ID 与版本化行为定义。
4. **Unlocked Card**：玩家进度对稳定卡 ID 的拥有关系。
5. **Installed Deck**：每角色安装的稳定卡 ID 集合、槽位/费用和战前锁定状态。
6. **Runtime Instance**：会话开始时固定 Published revision/hash 后的不可变编译结果与运行状态。

现有 `BehaviorLoadout / Variant` 作为推荐预设保留，应用后展开成可自由换卡的 Installed Deck，而不是目标玩法的唯一入口。UI 草稿不得直接进入 gameplay authority；`Apply F+1` 只作用于当前 Lab session candidate，Publish 必须经过独立验证门禁。详细合同以[行为卡玩家玩法与 Lab 统一设计](CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md)为准。

## Follow-up Design Sequence

后续设计必须按以下顺序展开，并引用本文：

1. [单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md)。
2. [Runtime AI Lab UX 与布局设计](CHARACTER_TEST_RUNTIME_AI_LAB_UX_LAYOUT_DESIGN.md)。
3. [行为草稿数据模型与持久化](CHARACTER_TEST_RUNTIME_AI_LAB_DRAFT_MODEL_PERSISTENCE.md)。
4. 行为卡条件、步骤和分支模型。
5. 行为卡 Runtime Compiler 与测试组合 adapter。
6. 不可变测试运行快照、短局模拟和精简预览时间轴。
7. 行为卡保存、验证和正式发布。
8. 性能预算、自动化测试与验收。

专项设计不得自行改变 60 Hz、默认 900 帧实时模拟、只按帧上限/主动退出结束，以及从不可变测试运行快照生成 RuntimeFrame 历史等已确定规则。单行为编辑表面不使用“确认后下一帧生效”，而以“应用并从第 0 帧重跑”为唯一提交点。

## Current Baseline And Gaps

当前已具备 Setup/Live/PausedInspect/Review 阶段、3D 出生点布置、selected Agent Overlay、Warm Replay、时间线、authority 校验、手动状态工具和基础 Edit Gate。既有 Lab Draft 基础也已实现 workspace/group/card GUID identity、项目版本导入、group/card copy-on-write、`Authoring/RuntimeAiLab/Drafts` 下的分文件共享 autosave、稳定引用 hash、`UserSettings` 本机 `.previous` 恢复、高版本只读和最多 100 步进程内 Undo。Setup 从项目行为组目录选择完整行为组；既有 group draft 与单卡隔离 authoring group 仍是当前源码兼容层，但后续单行为编辑表面必须迁移为本文指向的 Behavior Test Composition/Test Setup 语义。Player 对项目目录继续只读。

behavior-card logic v10 将行为卡冻结为 Timeline/Event/SubjectScope 配方：每条同级 Timeline 从自己的局部 `0f` 开始，具体 Event 使用条件、移动、CharacterAction、时间控制或跳转/返回操作。Fact 条件必须位于 CurrentCombatTarget、Self 或 Environment 对象作用域；EventSignal 只读取同 Timeline 上一 RuntimeFrame 的稳定结果。Track 只由 Event 类型自动生成，不进入 JSON 或运行。确定性 compiler 从不可变 v10 快照生成稳定索引和 semantic hash；NoAlloc evaluator 每个 RuntimeFrame 最多提交一个排他结果。v9 项目卡先按旧 semantic/content hash 认证，再确定性迁移到 v10；旧 Stage/Detector/Transition 只作为非序列化编辑兼容投影。

### 项目 JSON 权威

正式项目目录固定为：

- `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_card_catalog.json`
- `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards/<stable-card-id>.json`
- `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_group_catalog.json`

`behavior_card_catalog.json` 只保存轻量索引，不内嵌行为卡逻辑或基本信息；项目新建卡使用 `bc_` 加 10 位 Crockford Base32 随机短码（例如 `bc_7k3m9q2x5v`），生成后必须对当前索引查重。每个 stable card ID 在 `behavior_cards` 下对应一份完整 JSON，同一卡的全部已发布 revision 均追加在该文件中。行为组采用 `behavior-group-catalog.v3`，组版本固定引用每张卡的 stable ID、revision、semantic hash 和 content hash；新卡发布不会让旧组浮动升级。安装记录以规范化 `responsibilityKinds` 集合归入一个或多个兼容职责；职责以“全部/索敌/接战/输出/兜底/其他”六个 Tab 筛选同一份共享行为列表。Tab 与职责集合只用于作者整理，不影响运行分数和仲裁；`Other` 与四种明确职责互斥。Editor 事务写入同时验证索引、全部独立卡文件的聚合哈希和组目录，并通过暂存复读、本机 `UserSettings/RuntimeAiLab/ProjectCatalogRecovery` 中的单份 `.previous` 备份及失败回滚完成；事务暂存、备份和工作区状态均不进入 `Assets` 或 Git。Player 不注册写入桥。进入实验时会冻结组、卡和编译后 Profile 的版本证据，运行和回放不再读取项目 JSON。

项目行为卡目录与“本地玩家卡库”是两个明确分开的来源：前者服务项目行为卡与行为组创作，后者继续服务玩家 Deck。项目卡工作副本可编辑显示名、费用、标签、说明、使用摘要、风险摘要以及 Logic v10 Timeline/Event/SubjectScope 参数；这些字段进入版本化共享草稿的 `cards/<cardDraftId>.json`，即使卡片暂时没有被行为组引用也必须保留，并由本机 `.previous` 提供崩溃恢复。战术卡库中的草稿通过卡片自身“发布”入口追加到该 stable ID 的正式独立卡文件：首次生成 r1，后续生成连续 revision，行为组目录保持不变。项目卡发布新 revision 后，行为组编辑器只提示存在更新；只有显式升级引用并再保存一个行为组新 revision，正式实验才使用新卡。

入口与 Timeline 跳转的 CharacterAction 交接统一经过有状态 `CharacterActionExecutionGateway`。行为配方使用独立的 `ReplaceForOrchestration / TerminateForOrchestration` 权限：先完整预检目标，再原子替换或释放当前动作，目标实例真正启动后才提交 Timeline 游标；无效目标不会先取消旧动作。轻攻击、闪避等玩家操作仍由输入层解析为普通 CharacterAction 请求并继续遵守切换/打断窗口；行为卡不得复用这些输入请求。Move/Wait/Logic 接管时明确释放旧动作，不等待普通取消窗口。Overlay 的 pending jump、动作实例确认、logic facts 与 decision trace 各自只有一个状态 owner。

Composer-owned `MxCharacterTestRuntimeAiLabUi` 已采用同级 Timeline、自动 Track 与 Event 组件；旧 Stage/Detector/Transition 图形、端点和连接线不再是活动编辑路径。四区布局、行为列表右侧竖向命令栏、共享帧标尺、双轴滚动、右上 Event Inspector、Undo/Redo 和项目目录继续复用。单行为创作产品关系以[单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md)为准；实际 Timeline/Event 语义以[行为配方大时间轴设计](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md)为准。[Behavior Timeline Editor v2](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_DESIGN.md)仅保留为历史复用参考。

行为配方时间轴使用帧作为唯一单位。每条 Timeline 有独立局部 `0f`，但共享比例尺；Predicate 显示具体事实与比较值，LocalFrame 显示具体帧，PrimaryActionSignal 显示动作实例和原因码。当前 Timeline、本地播放头、命中的 Event 和 pending 跳转直接来自 v5 运行状态，不以 planned interval 冒充已发生历史。完整 RuntimeFrame Seek/Replay、候选比较和决策解释仍由运行测试表面承担；workspace process lock、协作导出、正式分类与生产级审批/签名/分发/回滚仍是后续工作。

玩家自由卡组的 internal foundation、`6` 槽/`10` 费、整卡解锁、每角色卡组存档、Published Behavior Card catalog、战前 authority 锁定、离线玩家 FairyGUI 卡组页与战斗反馈已经按阶段 1–5 验收。Runtime AI Lab 继续复用同一个玩家卡组服务和隔离 COW Draft；来源 revision/semantic/content hash 随 Draft 持久化。当前正式项目行为卡发布为 Logic v10、运行合同 7；v9 卡只允许先认证再迁移，已运行的 contract-6 程序不会静默恢复。append-only 本地 Publish 会重新验证 SubjectScope、权威引用、玩家合法性和 catalog lineage，不隐式执行 `Apply F+1`。生产 catalog 分发/审批/签名/回滚以及正式多人账号与匹配接入仍未实现。
