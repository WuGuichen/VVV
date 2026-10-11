# 战术卡描述显示契约

> 2026-10-10 后续设计边界：原准备等非行动卡已定改为配件，只随主卡统一出牌；规则归[主卡与配件基石](../../StorySource/玩法基石/行动卡与交互效果卡设计基石.md)，组合描述／槽位／总费用与UI接缝归[开发方案](../Plans/TacticalCardAssemblyDevelopmentPlan.md)。下文Utility类型、五卡Tooltip与共用卡壳是旧准备卡的当前显示实现，尚未迁移成配件分类／组合投影；本次未改生成器、卡面或UI资产。

> Status: Current
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.CardUiPreview.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestCardUiPreviewTests.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalApproachActionHint.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestFairyGuiAiLabCardTrialSurface.cs`
> 当前核对：2026-10-09；本轮 Unity EditMode 限定测试27/27（旧动作预览4、识别7、准备卡16），五张正式准备卡效果 Tooltip 已在真实 GameView 验证；Player 编译结果另见当轮构建回执。

独立意图的卡面只显示核心行为，省去句末标点；规则、参数、朝向、时限、超时与完成条件保留在 `DescriptionDetails` 和参数浮窗。已识别的段提供 `DetailsText`，由同一 C# 投影聚合详细说明。整备和战斗手牌 Tooltip 均展示详细说明，出招的动作选择入口保留。缺少装配或测量数据时沿用占位与诊断，不推定执行就绪。Step 后撤目前只有功能槽，没有独立意图源，不据此生成新行为。

2026-10-05 核对范围：15 个有独立意图源的组件已具中英文核心短句与详细规则。Web 组件目录为16项，第9项 Step 后撤来自功能动作槽，没有独立意图源；目录数量不能作为独立意图源或玩家已获卡数量。当前短句仍消费真实装配名，未装配、摘要缺失与未知结构保留占位和诊断。

Web 当前接口无需增加必选结构字段：共享生成的 `Description`（CLI `generated.description`）已经是权威排版字符串；使用 `white-space: pre-wrap` 保留换行和每层两个空格的缩进。浏览器不得解析玩法 DSL、推断标题意图或另写顺序/条件文案。2026-10-05 的 Web 目录共49条来源记录（正式卡20、作者草稿23、配置测试副本6），并非49张已发布卡；默认筛选显示正式卡与草稿43条。同名来源不合并，字段保持原契约，生成字符串自然切换到箭头。详见[Editor Hub 目录说明](../../Tools/MxFramework.EditorHub/README.md#combat-design-card-directory)。

单意图保留原短语。真正顺序用 ` → ` 连接，并去掉参与连接的短语末尾句号。真正组合 `If` 输出条件、成立路径、不成立略过和汇合后的公共后续；`Wait` 输出最大等待帧数、满足路径及超时结束；局部 `EndCondition` 只属于其子树。内部动作/移动的距离、朝向、准入条件不会成为组合分支。

现有 DSL 仅支持 `Sequence / Reference / If / Wait / Exit` 及局部 `EndCondition`，没有独立 else 或并行组合节点。Reference 中的意图由既有结构校验解析；只有完整顺序 EventBlock 才可连箭头。未识别/未知组合标记不完整并保留原描述，禁止猜测关系。

CLI 增加可选 `generated.descriptionFlow`（schema `mx.tactical-card.description-flow.v1`），仅供原生行布局、调试和验证：`text` 与 `Description` 相同；`complete` 表示是否完整解析；`root` 是显示树，kind 为 sequence/intent/if/wait/exit/interrupt/opaque，包含 nodeId、blockId、已本地化 conditionText 和 children。`rows` 是已排版展示行：kind/text/prefix/indent/segmentIndex/groupId/breakBefore/stripSentenceEnd。segmentIndex 引用原参数段；无参数的条件/汇合行为空，不删除或复制玩法参数。

这些对象是派生显示数据，不写卡、发布物或存档，不属于玩法 schema。不新增执行分支系统。Web 可只用字符串；如要树或行，必须消费这个共享结果，不重做语义。

卡牌一级类型及功能子类型由目录独立的 `cardType` 投影提供，Web 消费 Action / Utility / Unknown 与 Utility payload discriminator。准备卡属于 Utility 的 InteractionPreparation 子类型；作者草稿及测试副本明确标记来源预览。类型不能由名称、说明、意图组件或发布状态推断。类型字段与说明生成分别保留诊断；类型与筛选字段的具体契约、未知来源处理和验证入口见 [Editor Hub 目录说明](../../Tools/MxFramework.EditorHub/README.md#combat-design-card-directory)。

固定交互准备源没有 DSL 事件正文。CLI 展示适配直接调用游戏既有 `GenerateInteractionPreparation`，以已校验 payload 生成正文、详细规则和警告，`generated.descriptionBasis` 标明此权威；不按标题猜效果，也不在 Web 重写规则。未知/空效果契约由游戏解析器拒绝；空白或纯标点不算有效生成说明。Web 仅在明确标记静态说明时降级使用已有源文案，两者皆空则显示缺失说明占位。此显示适配不更改玩法、付费、时限或执行准备状态。

游戏卡面共享投影在处理通用 DSL 之前识别已校验的 `InteractionPreparation`，与 CLI 一同调用 `GenerateInteractionPreparation`；正文、详细规则、警告和无绑定正文来自同一权威，不依赖名称或静态副本文案。固定准备显示不要求动作槽或测量结果，清空旧动作提示、参数段与描述树，`Prepared` 只表示该固定显示源可投影；不安装卡、不出牌、不保存、不修改准备执行规则。未知或空效果、缺失必需字段及未知或空 ruleset 在源解析阶段拒绝。候选的纯投影测试与实际游戏 UI/Unity 编译验收分别报告，不能以隔离测试代替原生画面验收。

## 准备卡共用行动卡卡面（2026-10-10）

按用户最新决定，五张交互准备卡复用行动卡的 `TacticalPlayerCardFace` 卡壳、图标窗口位置、费用／卡名／正文位置和银／金／全息品质材质。运行时与 Web 使用同一 `ShellArtwork / AuthoredRegionMask`；区别限于图标窗口内侧的细分段刻线（`_UtilityCard`），不更换整张卡的轮廓、外框、品质配色或文字层级。

`GenerateInteractionPreparation` 提供只读 `CharacterTestTacticalUtilityFace`，数值继续读取 `TacticalInteractionTrialCards` 现行常量，费用取实际卡定义。其 `CardText` 由已校验的触发条件与核心效果组成，在共用正文槽显示；类型、子类型、本回合与次数不常驻卡面。完整正文、补充规则、时限与次数继续保留在 `Description / DescriptionDetails` 和 Tooltip 中。回返仍写为“安全时尝试回返”，不承诺瞬时返回。

CLI 的可选 `generated.utilityFace` 保留 `mx.tactical-card.utility-face.v1`，增加同源 `cardText`；Web 只消费这个显示字符串及效果插画，不解析玩法 DSL、说明或按名称分类。效果插画沿用 Utility 目录的五张灰阶图，放入共用行动卡图标窗口。独立准备卡方案的 `utility*` 文字槽保持隐藏，素材与生成器作为历史研究保留，不参与现行卡面。源研究状态见 `ArtSource/UIKits/MxUiFoundation/source/utility_card_v1/manifest.json`。

品质由共用 `PreparationQualityStyles.json` 按卡 ID 分配（0 银、1 金、2 全息）；只是外观样例，不更改稀有度、强度或获取规则。实例绑定时重置效果插画、图标边框标记、材质和文字显示，避免复用到行动卡后残留。功能卡直接使用共用材质的 alpha 轮廓，父级卡库裁剪继续生效；行动卡保持既有 Shape mask。

本轮核对范围为共用卡面、简短正文、实际图标与品质配置，以及 Unity 编译／Console、专项测试和原生卡库／Web 画面。证据位于本机 `Library/UtilityCardImplementation/`；不声明完整战斗玩法或所有语言组合完成新的验收。

2026-10-10 提交检查：Unity refresh／编译／domain reload／Editor ready 通过，Console error 0；仓库轻量 Harness 及其 86 项测试通过，卡面专项 12/12 和本批卡片试用配置检查通过。扩展的三组 EditMode 合计 38/39：既有 `TacticalCardTrialTests.InsufficientMomentumAndInvalidActorDoNotPayOrActivate` 期望 `InvalidSpawnBinding`，实际得到 `StaleSnapshot`，其整个 TacticalCard Runtime 与 fixture 相对提交前 HEAD 未改动。FairyGUI Manifest 全量检查的 SettingsPanel 命令数量断言、独立整备计划的旧 root 数量断言，均在提交前 HEAD 的隔离源码基线中复现；本批 AI Lab 卡片试用 source-plan 检查通过。上述旧断言未在本批改写，不能把扩展测试报告为全绿。

接敌／等待／出招旧事件模板的提示识别按实际执行合同检查显式等待节点、移动／转向与时间顺序，不以已停用的 `CharacterAction.ConditionRoot` 作为短句或测量提示准入条件。等待 Gate 的存活、距离、朝向条件继续严格核对；当前无动作条件与历史保留条件得到相同提示。距离参数采用已验证的 `SelectedRange` 首个闭合命中区间，并在忽略更远区间时保留明确提示；整备与配置卡一致性测试先等待真实自动测量完成，再对同一精确正式版本比较正文、参数及存储不变性。此改动只修正显示识别，不改变播放动作、测量或发布语义。

AI Lab 真实出牌试用界面的整张卡悬停绑定既有 `TooltipText(data)`（正文与效果详情），并保留当前出牌操作提示；采用现有 Tooltip 服务处理边界放置、离开隐藏和 owner 释放。隐藏试用界面时立即隐藏 Tooltip。纯准备效果没有动作测量参数，不因此丢失效果窗口、每回合限制和清理说明。
