# CharacterTest Runtime AI Lab 统一工作台行为时间轴 v2 完整设计

> Status: Design
>
> Version: 2.3
>
> Date: 2026-08-04
>
> Parent: `CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md`
>
> Implementation slices:
> [CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_IMPLEMENTATION_SLICES.md](../Tasks/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_IMPLEMENTATION_SLICES.md)
>
> Related contracts:
> `CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_CARD_PAYLOAD.md`,
> `CHARACTER_TEST_RUNTIME_AI_LAB_UX_LAYOUT_DESIGN.md`,
> `CHARACTER_TEST_RUNTIME_AI_LAB_DRAFT_MODEL_PERSISTENCE.md`,
> `CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md`
>
> Visual baseline:
> `runtime-ai-lab-unified-workbench-plan-mode-v2-target.png`,
> `runtime-ai-lab-unified-workbench-plan-conflict-compare-v2-target.png`
>
> Scope: CharacterTest internal Runtime AI Lab 的统一 Workbench、行为计划时间轴、
> 共享 Inspector、卡库抽屉、按需运行证据与 A/B 对比。本文不修改
> `MxFramework.AI` 公共 API，不定义生产 catalog 审批、云存档、账号或多人网络权限。
>
> Current behavior-recipe authoring authority: [行为配方大时间轴设计](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md).

## Current Authoring Override (2026-08-11)

本文继续作为 Behavior Plan、RuntimeFrame、历史帧联动和高级 Workbench 能力的设计参考，但不再定义当前“编辑逻辑”中的行为配方 UI。凡后文涉及整卡绝对逻辑时间轴、嵌套动作子时间轴、Stage/Detector/Transition 卡片、端点、箭头、连接线或 Graph 排布，均为历史设计输入。

当前行为配方编辑固定使用“多条同级 Stage 本地时间轴 → 自动 Track → Event”；具体时间语义、交互和验收以链接的专项权威为准。

## 1. 文档权威与阅读方式

本文是 Behavior Plan、RuntimeFrame 和历史高级 Workbench 的完整设计参考；它不再是当前行为配方编辑器的目标设计或实现验收基线。

当设计与当前实现不一致时：

1. 当前源码和测试仍是“已经实现什么”的事实源；
2. 当前行为配方 UI 以专项大时间轴设计为目标；
3. 本文只用于 Behavior Plan、RuntimeFrame、历史能力和设计追溯；
4. 相关 Payload、Draft、RuntimeFrame 文档继续定义数据与 authority；
4. 效果图中的生成式文案只说明位置和层级，不覆盖稳定命令、typed field 和正式中文文案。

除明确附有实际 Game 帧和任务索引状态的部分外，本文不把目标设计描述为已经交付。
所有“必须”都表示最终验收要求；当前完成度以任务索引为准。

本文中的 `v2` 是**统一工作台的信息架构与视觉投影版本**，不是新的行为运行时、
新的 Logic schema 或第二套编辑器。当前源码已经完成的大部分领域能力必须原样复用；
本设计的主要开发量是搬迁、组合和重投影。

### 1.1 单行为创作表面覆盖关系（2026-08-07）

[单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md)已经取代本文对默认单行为创作表面的产品定义。本文继续作为 Behavior Card 内部逻辑时间、现有 RuntimeFrame authority、手势、projection 和高级诊断能力的复用参考。

在单行为创作范围内，本文后续以下目标均不再生效：

- `编辑逻辑 / 行为计划 / 运行轨迹` 三 Tab 统一 Workbench；
- Behavior Group、Player Deck、Facts、A/B、分支重算或 Replay 作为主创作界面常驻/抽屉能力；
- 默认展示完整五轨 RuntimeFrame；
- `Apply F+1` 和同一段预览历史中的多 revision 热更新；
- 从 Behavior Group drawer 或组条目组织正式行为组。

单行为编辑器只显示精简 RuntimeFrame scrub 时间轴，并以“应用并重跑”冻结测试组合和双方阵容，从第 0 帧建立新的不可变 Run Snapshot。本文的完整五轨、typed marker、A/B 和分支能力可以留在高级/兼容运行测试表面，但不得重新占用第一版单行为编辑空间。

实现 Agent 默认从[任务索引](../Tasks/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_IMPLEMENTATION_SLICES.md)
进入，只读取当前 BT2 文件列出的本文章节；不要为执行一个切片预加载本文全文、全部
BT2 文件和 A1–G1 历史任务。只有产品/设计完整评审需要通读本文。

## 2. 决策摘要（历史统一 Workbench 目标）

> 下表不再决定单行为创作表面的一级界面、运行历史入口、抽屉、可见轨道或应用语义；这些决策以 1.1 节链接的新设计为准。Plan 内部时间语义、typed 编辑命令和 RuntimeFrame 单一 authority 仍可复用。

以下决策已经锁定，不留给实现阶段再次选择：

| 决策 | 结论 |
| --- | --- |
| 一级界面数量 | 仅保留 `Setup / 实验准备` 和 `Workbench / 工作台` 两个全屏上下文 |
| 行为编辑入口 | Workbench 底部固定 `编辑逻辑 / 行为计划 / 运行轨迹` 三个 Tab；编辑不打开独立 Behavior Card 页面 |
| 运行历史入口 | 同一底部区域切换到 `运行轨迹`，不与行为计划上下堆叠 |
| 卡组与卡库 | Player Deck、Behavior Group、卡库均为按需抽屉或 picker |
| 当前决策 / Inspector | Runtime 显示当前因果；Plan 默认显示真实重决策候选，选择计划元素时原地切换详情 |
| 时间轴结构 | Plan 是紧凑的 committed 行为内部视图；单阶段合并为一行，多阶段才展开结构；Runtime 保留既有五轨 |
| 时间轴高度 | 设计基线中占 Workbench 可用高度的 `52%–55%` |
| 缩略总览 | 首个 v2 不做 minimap、缩略图、overview strip，也不预留空白 |
| A/B | 直接叠加在现有主轨内，指标进入共享 Inspector 或 tooltip |
| 长时间轴导航 | sticky label、zoom、pan、横向滚动、`Fit selection`、`Fit card` |
| authority | 编辑只进入 Lab Draft；只有确认的 `Apply F+1` 才替换当前 Lab session evaluator |
| UI authority | Composer-owned `MxCharacterTestRuntimeAiLabUi`；禁止运行时代码伪造或手工修补 package XML |

`编辑逻辑` 的统一 Host 收口直接归入既有 BT2-04，不新增子切片，也不改变 BT2-05–08
的语义边界。

### 2.1 BT2-03 信息收口（当前权威）

2026-08-04 起，Plan 不再用固定轨道或行为关系图填满下区。当前权威合同如下：

- 下方只显示真实 committed 行为内部的 Stage、动作与退出；右侧固定显示 CardArbitration
  产生的重决策候选，不把当前行为摘要冒充下一行为；
- 单阶段行为合并成约 `60px` 的紧凑事件行，不重复阶段容器、动作标题或当前行为摘要；
- 默认持续靠近显示为 `持续靠近    距离 3.5m / ≤1.5m`。当前值来自同帧运行事实，
  `≤1.5m` 来自 MoveToTarget 的权威完成边界；
- 持续靠近的激活条件是严格的 `目标距离 > 1.5m`，到达 `≤1.5m` 后结束并重新决策；
- 固定动作显示 `轻攻击 29/76帧`，等待显示 `等待 12/30帧`，布尔事实显示
  `目标存在 ✓`，区间显示 `距离 1.2m / 0–1.5m`；
- 成功、失败和条件出口使用紧凑事件行。结束本行为的出口明确显示
  `≤1.5m → 重新决策`、`移动失败 → 重新决策` 等真实语义；开放式出口只以高亮表达活动，
  不显示 `N帧/∞` 伪进度；
- 删除左侧轨道名、常驻“执行中/监听中”、“下一帧继续评估”和装饰性大色块；
- 元素宽度由内容和权威时长决定，并受最小/最大宽度约束；剩余空间允许为空，不拉伸伪造信息密度。

本文后续带有“关系区”“固定三轨”“上一行为/重新决策/全部候选”的章节和效果图均为历史设计证据，
若与本节冲突，以本节和 BT2-03 附属实现文档为准。

这里的 `1.5m` 是新建持续靠近模板的默认值，不是 UI 常量。导入行为组若真实配置为
`1.6m`，界面必须显示 `≤1.6m`；当前值、运算符和阈值都必须来自同一张已提交行为的运行事实与
Logic v3 条件。

## 3. 设计效果图

### 3.0 BT2-03 信息收口后的真实 Game 帧（历史实帧）

![退出与下一行为候选 1920×1080](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-exits-next-candidates-live-v3-1920x1080.png)

该图由当前 Play Mode Game framebuffer 直接获取。默认近战导入组的真实阈值为 `1.6m`，
因此画面显示 `持续靠近  距离 4.0m / ≤1.6m`，并在其下显示成功与失败出口。右侧六行来自
当时的 CardArbitration 投影仍混合显示前置目标策略、普通行为和系统兜底。2026-08-04
职责收口后，右侧普通候选只显示可执行行为；`Targeting` 基础槽不进入仲裁，`Fallback`
基础槽仅在没有普通行为满足条件并真实接管时显示。因此该图只保留为收口前的真实证据，
不再作为候选列表的视觉权威。

### 3.1 玩法可读正常态（构图参考）

![Runtime AI Lab 早期四轨玩法可读计划目标](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-gameplay-readable-plan-ui-target-v2.png)

该图取代早期五条技术轨目标，定义：

- 当前阶段与主动作直接使用中文玩法动词；
- 同一阶段的成功、失败和条件出口汇入一个菱形分支节点，不表现为时间先后；
- 当前执行不再占独立轨；只有 latest + exact identity 才在动作和出口块内部显示各自的帧进度，不显示全局播放头；
- 右侧默认给出当前计划、当前动作、主要成功出口和失败出口；点击元素原地切换详情；
- Detector、Transition 等稳定英文 ID 只进入 Tooltip/诊断。

### 3.1.1 BT2-03 三轨修订后的实际 Game 帧

![BT2-03 三轨玩法可读计划 2560×1440](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-three-track-plan-2560x1440.png)

### 3.1.2 精确运行身份下的旧版动态进度（历史证据）

![BT2-03 行为计划动态进度 2560×1440](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-live-progress-prominent-2560x1440.png)

该帧来自真实 Play Mode，但其中脱离动作块的青色粗线、圆点和全局播放头已经废弃，仅保留
为动态刷新链路的历史证据。当前视觉权威要求每个动作和出口只显示自己的块内进度；没有
精确运行身份时继续隐藏这些动态覆盖，不伪造进度。

### 3.1.3 块内事件进度视觉权威

动作执行中，固定时长显示“当前帧/总帧”，开放监听显示“已监听帧/∞”：

![BT2-03 动作执行中 1920×1080](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-event-progress-running-ui-target-v1.png)

动作完成后保持满格和完成标记，出口继续独立监听：

![BT2-03 动作完成且出口监听中 1920×1080](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-event-progress-action-complete-ui-target-v1.png)

响应式目标：

![BT2-03 块内帧进度 2560×1440](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-event-progress-running-responsive-ui-target-v1.png)

三图均取消 Plan 的全局帧刻度和竖向网格。横向位置仍由内部帧几何确定，但用户只读取
事件块上的 `9/300`、`✓ 300/300` 或 `37/∞`，不读取百分比。

### 3.1.4 当前行为一眼可读的全屏视觉权威

![BT2-03 当前行为全屏视觉权威](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-current-behavior-fullscreen-ui-target-v1.png)

该图不创建新页面或摘要面板。既有 Plan toolbar 用一个紧凑句式同时回答“当前做什么、已经
运行多少帧、下一步是什么”；三轨中只有当前阶段、当前动作和当前活动出口保持满亮，已完成
内容降一级，尚未进入的未来内容进一步弱化。目标出口在紧凑卡片中优先显示目标 Stage 对应
的玩法动作名，例如“轻攻击”；完整目标 Stage 名与 stable identity 仍留在 Tooltip/右侧详情。

当前视觉权威为三轨；以下两张四轨帧仅保留为早期空间演进证据。

![BT2-03 四轨玩法可读计划 1920×1080](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-gameplay-readable-plan-1920x1080.png)

![BT2-03 四轨玩法可读计划 2560×1440](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-03-gameplay-readable-plan-2560x1440.png)

两档均为直接 Game framebuffer；实际活动卡只有一个 Stage，但仍完整呈现阶段、玩法动作、
同源成功/失败出口、分支连接和 latest exact-identity 快照。多阶段与多出口密度继续由
projection focused tests 覆盖，不用伪造运行帧填满截图。

### 3.1.5 右栏“当前行为”视觉权威

![BT2-03 当前行为摘要](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-current-behavior-summary-ui-target-v2.png)

当前 `1920×1080` Game framebuffer 实现证据：

![BT2-03 当前行为摘要实际帧](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-current-behavior-summary-actual-1920x1080.png)

Plan 默认右栏不再常驻四张技术因果卡。它用三行回答：角色正在等待什么、当前事实为何
不满足、当前实际命令是什么。若当前事实与活动 Detector 条件冲突，紧邻显示一条短警告和
`修复`；按钮只切换到同一 Workbench 的 `编辑逻辑` Tab 并定位 Detector，不自动改写语义。
原 `感知 / 判断 / 行为 / 结果` 通过“查看决策详情”按需展开，Stage/Action/Exit 的选择详情
仍在同一右栏原位显示。

### 3.1.6 行为关系与全部重决策候选（历史设计，已废弃）

![默认近战真实帧行为关系与完整连线](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-default-melee-real-frame3-connected-ui-target-v6.png)

该图曾替代旧的“上一 → 当前 → 单一下一候选”关系条，但已被 2.1 的紧凑内部视图取代。
候选仍来自真实 CardArbitration 数据，但只在右侧“当前决策”呈现；下方不得再创建关系节点、
候选卡或重决策总线。以下条目仅保留为历史演进记录：

- 上半区直接展示 `上一行为 → 当前 committed 行为 → 卡片结束出口 → 重决策 → 全部候选行为`；
- 下一行为尚未确定，因此不得投影一个虚假的“下一行为”。重决策节点连接当前行为组的每个
  CardArbitration binding，连线不能因候选不满足而消失；
- 每张候选卡显示本帧真实 `Selected / Evaluated / Ineligible / NotEvaluated` 状态、真实 score，
  但 `Selected` 必须翻译为“本帧首选”而不是“当前选中”。当前 committed 卡固定显示“执行中”，
  pending 卡显示“待接管”；只有动作网关接管成功后，候选才允许成为当前行为。这样重决策结果
  与下方仍在运行的 CharacterAction 不会产生伪冲突；
  以及由 compiled activation conditions 与同帧 decision trace 计算的逐条 `✓ / ×`；
- 无 activation condition 的候选显示 `—`，不得伪造判断；完整 stable ID、reason code 与技术
  诊断只进入 Tooltip；
- 当前卡的 End transition 只在关系区收口成“完成/失败”出口并先汇流，再进入独立的“重新决策”
  矩形节点；出口线、汇流线和候选总线不得穿过节点文字，不再占用独立
  分支轨。下方仅保留“阶段、动作”两行，用于阅读当前 committed 卡内部的局部时长与动作进度；
- `1920×1080` 固定几何为关系区 `y=72..404`、阶段 `y=404,h=68`、动作 `y=472,h=136`。
  关系候选使用高水位对象池；结构变化时重排，稳定帧只更新文字、状态色和块内进度。

### 3.2 早期统一 Workbench 构图参考

![Runtime AI Lab unified workbench plan mode v2 target](../Images/CharacterTest/runtime-ai-lab-unified-workbench-plan-mode-v2-target.png)

该图定义：

- 当前 Game 画面、Agent 和空间 Overlay 持续可见；
- 上区左侧保持 3D Scene，右侧固定保留上下文栏；Plan 默认显示一眼可读的“当前行为”，Runtime 默认显示 `感知 / 判断 / 行为 / 结果`；
- 进入 `行为计划` 不切换到另一页面，Plan 选择和事件详情优先复用右侧上下文列；
- 底部 `行为计划` 被选中，`运行轨迹` 保留为同级模式；
- 卡库是按钮入口，不是固定左栏；
- Plan 关系区按 3.1.6 展示上一、当前、结束出口、重决策和全部候选；pending 只影响候选状态，
  不替代 committed 当前行为；点击有身份的节点可查看对应行为卡，但不得改变执行权；
- 下方两条玩法主轨只展开当前已提交行为的阶段与动作；
- 没有缩略图、小地图或独立比较栏。

### 3.2.1 编辑逻辑统一 Tab 目标

![Runtime AI Lab unified workbench behavior logic target](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-unified-tabs-behavior-logic-ui-target-v1.png)

该图补充定义：

- `编辑逻辑` 与 `运行轨迹 / 行为计划` 使用同一个 Tab host、Game 画面和右侧属性栏；
- 进入编辑态不再打开全屏 Behavior Card 页面，也不重建测试上下文；
- 下方改为玩法可读的阶段流，右栏显示所选 Stage/Detector/Transition 属性；
- blocking diagnostic 直接附着到对应元素，主修复动作与错误相邻；
- 卡库改为按需抽屉，技术 stable ID 只进入 Tooltip/诊断。

### 3.2.2 编辑逻辑统一 Tab 实际 Game 帧

![Runtime AI Lab 编辑逻辑 1920×1080](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-unified-tabs-behavior-logic-1920x1080-final.png)

![Runtime AI Lab 编辑逻辑 2560×1440](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-unified-tabs-behavior-logic-2560x1440-final.png)

实际实现已将 `编辑逻辑 / 行为计划 / 运行轨迹` 收进同一个 RuntimeFrame root；Logic
采用中文优先的紧凑 Stage/Detector/Transition 阶段流，右栏显示所选元素摘要，并在缺少
触发条件时直接给出“添加条件”。当前行为卡只有一个 Stage，实帧不伪造多阶段内容。

### 3.3 冲突与轨内 A/B 态

![Runtime AI Lab unified workbench plan conflict and comparison v2 target](../Images/CharacterTest/runtime-ai-lab-unified-workbench-plan-conflict-compare-v2-target.png)

该图定义：

- blocking ownership conflict 在精确重叠区间使用红色斜纹；
- 冲突解释和确定性修复动作进入按需 Inspector 浮层/抽屉；
- `Apply F+1` 被禁用，但场景和时间轴上下文保持可见；
- A 使用当前计划/实际样式，B 使用同轨橙色虚线；
- A/B 不创建新页面，也不占用底部独立摘要区域。

早期两张统一 Workbench 图均为 `1672×941` 的构图参考，不再作为 Plan 轨道语义权威。
最终实现按
`1920×1080`、`2560×1440` 和 `1280×720` 的规则响应式物化，不要求逐像素放大。

## 4. 当前基线与目标差异

### 4.1 源码复核结论：领域能力大部分已经完成

截至本文日期，Behavior Timeline v2 不是从零开发。以下能力已有源码和测试合同，
实现阶段不得重新造一套等价逻辑：

| 已有能力 | 当前事实与证据入口 | v2 处理 |
| --- | --- | --- |
| Logic v3 运行链 | Stage、Detector、Transition、稳定 id、validator、immutable compiler、NoAlloc evaluator 已存在；离散动作统一为直配稳定 ID 的 `CharacterAction` Stage；见 `CharacterTestRuntimeAiLabBehaviorCardLogic.cs`、`CharacterTestRuntimeAiLabBehaviorCardCompiler.cs` | 原样复用 |
| Stage 计划时间 | `PlannedStartFrame`、`PlannedDurationFrames`、`DurationMode` 已进入 schema、compiler 和 ViewModel | 原样复用 |
| Detector 窗口 | card/stage scope、`ActivationStartFrame`、`ActivationDurationFrames`、`WindowEndMode`、priority 已存在 | 原样复用 |
| Transition 编辑 | source/target、policy、priority、back edge、self loop 和稳定排序已存在 | 原样复用 |
| Stage 编辑手势 | stable-id 选择、范围选择、框选、多选、移动、resize、边缘滚动、取消、gesture epoch 已存在；见 `CharacterTestRuntimeAiLabBehaviorCardStageEditing.cs`、`CharacterTestRuntimeAiLabBehaviorCardStageGesture.cs` | 原样复用 |
| Detector 编辑手势 | window move/resize、priority reorder、window canonicalization 和 typed field edit 已存在 | 原样复用 |
| 编辑事务 | copy-on-write、原子 mutation、单次 commit、Undo/Redo、重复/删除和 invalid Draft 保留已存在 | 原样复用 |
| 冲突语义 | validator 已产生 `CARD_V2_PRIMARY_ACTION_TIME_CONFLICT`，drag preview 和 ViewModel 已标记 `ActionConflict` | 复用检测，只扩展显示与修复 |
| 时间轴导航 | zoom、pointer-centered viewport、pan、横纵滚动、Fit all、follow-current 和当前 `15f` snap 已存在 | 复用基础，只扩展 preset/selection fit |
| 卡库 | query normalize、filter、deterministic sort、paging、stable focus 和 availability gate 已存在 | 搬入 drawer，不重写查询 |
| Runtime identity | applied revision、authority hash、card/stage/action stable identity、same/different semantic relationship 已由 `CharacterTestRuntimeAiLabBehaviorCardIdentityProjection.cs` 解析 | 原样复用 |
| Actual 与历史态 | current Stage、actual duration、Detector/Transition runtime state、冻结 program、typed historical event 和 historical read-only 已存在 | BT2-03 只做最新帧快照；完整 Actual 在 BT2-05 重投影为轨内证据 |
| RuntimeFrame | retained records、五轨投影、typed item navigation、Latest/Jump、A-B loop review、Warm Replay 和分支重算已存在 | 作为 `运行轨迹` mode 原样复用 |
| Candidate 比较 | immutable candidate revision、semantic hash 和 deterministic comparison report 已存在 | 作为 Plan A/B 的输入之一 |
| 应用与玩家链路 | `Apply F+1`、Player Deck、Published Behavior Card、local Publish、安装/重开/应用链路已存在 | 原样复用 |
| 性能基础 | high-water buffers、pooling binder、warm projection/gesture NoAlloc 测试已存在 | 保持并补 UI 组合基线 |

上述事实由现有 `CharacterTestRuntimeAiLabBehaviorCard*Tests`、
`CharacterTestRuntimeAiPlannerDevTests.FrameSelection` 和 Trigger 场景覆盖；v2 开发只需
为新组合增加 focused regression，不重复证明或重写已经通过的领域实现。

### 4.2 已实现，但需要搬迁或重投影

| 当前实现 | 目标变化 | 工作性质 |
| --- | --- | --- |
| 独立 Behavior Card workspace | 放入持久 Workbench 的 `编辑逻辑` mode；`行为计划` 保持只读投影 | UI 搬迁 |
| 独立 RuntimeFrame workspace | 放入同一个 host 的 `运行轨迹` mode | UI 搬迁 |
| Stage-card/route 图式时间轴 | 使用相同 ViewModel 与 command，重投影为三条玩法可读主轨 | 表现适配 |
| Stage/Detector/Transition popover | 绑定到按需共享 Inspector 浮层/抽屉 | 表现适配 |
| 卡库、Player Deck、Behavior Group 页面 | 使用现有 state/query/controller，改由 drawer/picker 承载 | 容器搬迁 |
| runtime identity、Actual duration、historical projection | 需要时叠加到对应 Stage/Detector/Action/Transition，不占常驻轨 | 表现适配 |
| `ActionConflict` 节点级标记 | 根据现有计划区间显示精确 overlap band | 表现增强 |
| RuntimeFrame A-B loop 与 candidate comparison report | 保留各自语义，作为 Plan 轨内比较选择的已有输入 | 表现增强 |
| 旧 route/command | 在迁移期作为 mode/drawer alias | 兼容适配 |

### 4.3 本设计真正新增的工作

真正需要新开发的只有以下 UI 组合或 projection 能力：

1. 持久 `timelineModeHost`、三个同级 Tab 及不丢失 Agent/frame/camera/playback 的 mode 状态。
2. 三轨 `GameplayReadablePlanProjection`、sticky track layout、互斥出口分组和对应 pooled visual。
3. drawer host、共享 Inspector host 及其 Back/Escape 层级。
4. 在现有 zoom/pan/fit/snap 上增加 snap preset、`Fit selection` 和 `Fit card`。
5. 把现有冲突检测投影为精确区间，并增加确定性、typed、Undoable 的修复命令。
6. 把现有 runtime identity/historical evidence 映射为对应元素的轨内 Latest Actual rail 和兼容性状态。
7. Plan 专用的逐元素 A/B 时间映射与轨内 overlay；这不同于现有 RuntimeFrame A-B loop。
8. 三档响应式布局、目标视觉、长卡性能证据以及新增 UI 路径的 Trigger/render-frame 验收。

### 4.4 复用优先硬约束

- 不新增第二套 Logic schema、validator、compiler、evaluator、Draft、Undo/Redo 或 RuntimeFrame buffer。
- 不新增与现有 move/resize/select/priority/zoom/pan/fit 等价的领域命令。
- 新 projection 必须从现有 typed ViewModel、identity projector、historical projector 和 event index 读取。
- 新 UI 命令只能编排现有 controller mutation；确有缺口时，先用 focused test 证明缺口。
- 每个实现切片必须提交“复用映射”，说明接入了哪些现有类型和测试，避免把视觉重组写成领域重做。

## 5. 产品目标

- 开发者无需离开当前战斗画面即可查看并编辑正在测试的 Behavior Card。
- 从运行证据进入计划编辑时，不丢失 Agent、帧、镜头、播放状态或时间轴 viewport。
- 计划与实际在同一时间尺度中可比较，但不会混淆为同一种 authority。
- Stage、Detector、Action 和 Transition 在一次横向阅读中可理解。
- 固定时长、Until Event、Infinite、pending 和 interruption 具有不同且明确的视觉语义。
- blocking conflict 必须定位到精确区间，并提供可撤销的确定性修复动作。
- 三条玩法主轨具有足够高度，长名称、分支出口、当前执行覆盖、按需实际证据和诊断不会挤成缩略图。
- 所有编辑继续复用已有 COW Draft、Undo/Redo、validator、compiler 和 `Apply F+1`。

## 6. 非目标

- 新增独立 Behavior Card 全屏页面。
- 新增永久左侧 node library。
- 首个 v2 实现 minimap、缩略视图或 overview strip。
- 通用动画、技能或 cinematic sequencer。
- 在 Runtime AI Lab 内编辑 Ability 命中帧、root motion、VFX 或 CharacterAction 内部时间线。
- 两个并行 active Stage 或两个同时拥有 gameplay authority 的主动作。
- 隐藏在卡内的跨卡 Transition。
- 修改 RuntimeFrame 历史、authority hash 或 replay wire。
- 生产 catalog 审批、签名、分发、回滚。
- 正式账号、云存档、多人协作和 shared Undo。

## 7. 用户与核心任务

### 7.1 主要用户

Runtime AI Planner 行为设计者和 CharacterTest 开发者。该界面不是玩家 UI，
也不是 `MxFramework.AI` 公共编辑器。

### 7.2 高频任务

1. 观察当前 Agent 为什么选择某个行为。
2. 从 `行为` 因果卡直接定位正在执行的 Behavior Card 和 Stage。
3. 调整 Stage 计划时间、Detector 窗口、Transition 策略或优先级。
4. 修复主动作 ownership conflict。
5. Undo/Redo、验证并在下一帧应用候选 revision。
6. 对比计划与最近一次真实执行。
7. 对比当前草稿和上一发布/固定 run 的执行差异。
8. 从计划事件跳到精确 RuntimeFrame 证据，再返回相同编辑上下文。

### 7.3 低频任务

- 从卡库切换另一张卡；
- 编辑 Behavior Group 或 Player Deck；
- local Publish；
- Warm Replay、分支重算和高级一致性诊断。

低频任务不得增加常驻面板。

## 8. 信息架构

### 8.1 一级上下文

```text
Runtime AI Lab
├─ Setup / 实验准备
└─ Workbench / 工作台
   ├─ Header
   ├─ 3D Scene
   └─ Shared Timeline Host
      ├─ 运行轨迹
      ├─ 行为计划
      └─ Contextual Inspector overlay（按需）
```

### 8.2 按需上下文层

```text
Workbench
├─ Player Deck drawer
├─ Behavior Group drawer
├─ Card Library drawer
├─ Causal detail
├─ Timeline event detail
└─ Advanced diagnostics
```

同一时刻最多打开一个主抽屉和一个局部详情。抽屉不是路由，不创建新的
simulation/session authority。

### 8.3 导航规则

| 用户动作 | 结果 | 必须保持 |
| --- | --- | --- |
| Start test | 从 Setup 进入 Workbench，默认 `编辑逻辑` | 初始化后的 Agent、镜头和 baseline |
| 点击 `行为计划` | 只替换底部 timeline mode | Agent、frame、camera、playback、scene |
| 点击 `运行轨迹` | 恢复 RuntimeFrame timeline | plan selection 和 plan viewport 记忆 |
| 点击因果卡 `行为` | 切到 `行为计划` 并聚焦 active card/stage | 当前 frame 不变 |
| 点击 `卡库` | 打开卡库抽屉 | 场景和底部时间轴不重建 |
| 选择卡片 | 关闭抽屉并切换 plan card | Agent、frame、camera、playback |
| 选择 Actual evidence | 切到 `运行轨迹` 并定位 typed item | plan card、selection、viewport 可恢复 |
| Back/Escape | 关闭最深详情或抽屉 | 不重置 session |
| Exit | 离开 Workbench | 按现有退出合同处理 |

旧 `BehaviorCardTab` 和 `DiagnosticsRuntimeFrameTab` 在迁移期分别作为
`行为计划` 和 `运行轨迹` 的兼容 alias。它们不再代表页面导航。

## 9. Workbench 状态保持合同

切换 timeline mode、打开/关闭抽屉或进入/退出详情时，以下状态不得隐式变化：

- selected Agent entity identity；
- `selectedFrame`、`latestFrame` 和 Latest relationship；
- Live/Paused/Review/Completed；
- playback speed、loop range 和 branch transaction；
- 3D camera position、rotation、projection 和 fit target；
- selected scene entity 和 Overlay；
- active Behavior Card identity、Draft GUID 和 revision；
- 每种 timeline mode 各自的 horizontal viewport、zoom 和 selection；
- Draft dirty/save/compile/apply 状态；
- event detail selection。

Runtime timeline 和 Plan timeline 分别保存自己的 viewport。模式切换不把一套 viewport
数值解释成另一套时间域。

## 10. 1920×1080 布局基线

### 10.1 总体坐标

效果图在 `1920×1080` 的目标物化如下：

| 区域 | 坐标/尺寸 | 说明 |
| --- | --- | --- |
| RuntimeFrame root | `x=0, y=0, w=1920, h=1080` | 两个 Tab 共用的持久根，不因切换重建 |
| Game viewport | `x=0, y=0, w=1400, h=472` | 3D Scene、Agent 与空间 Overlay；顶部不再被横条遮挡 |
| Right property column | `x=1400, y=0, w=520, h=472` | 会话栏与当前决策连续占满上区 |
| Session header | `x=1400, y=0, w=520, h=60` | Agent、测试名、状态/帧与唯一会话控制 |
| Decision content | `x=1400, y=60, w=520, h=412` | 固定因果链与动作/选择摘要 |
| Shared timeline toolbar | `x=0, y=472, w=1920, h=72` | 两个 Tab 固定在相同位置；模式只替换标题、工具与投影 |
| Shared timeline viewport | `x=0, y=544, w=1920, h=536` | 模式内容区；Runtime 可保留历史标尺，Plan 直接从三条轨道开始；不保留底部缩略图或空占位 |

允许在不破坏最小尺寸的情况下按窗口比例微调，不允许把时间轴缩成右侧窄栏。

### 10.2 结构示意

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│                    3D SCENE                       │ Agent  测试名  ▌❚  ›|  ↻  ↩ │ 60
│        selected Agent / target / ranges           ├───────────────────────────┤
│                                                   │ 当前决策                   │
│                                                   │ 感知 / 判断 / 行为 / 结果 │ 412
│                                                   │ 所选动作摘要              │
├──────────────────────────────────────────────────────────────────────────────┤
│ [编辑逻辑] [行为计划] [运行轨迹]         [卡库] [Snap] [Fit] [Validate]   │
│ 阶段       [锁定最近敌人────────────]                                      │
│ 动作       [选择并写入最近的有效敌人  9/300]                               │
│ 分支                    ◇─[成功：已锁定有效目标  37/∞]                     │
│                          └[失败：无有效目标  37/∞]                         │
│             每个事件只显示自己的块内帧进度；无全局刻度或播放头              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 10.3 上下分割

- 三个 Tab 共享同一 Workbench、Agent、Scene、镜头、右侧栏和下方外壳；Tab、toolbar 与 viewport 外边界完全固定。Runtime/Plan 使用时间轴，Logic 使用阶段流且不显示时间标尺。
- `行为计划` 在 `1920×1080` 使用 `608px` 根高度，其中 toolbar `72px`；不显示全局 ruler，三条有效轨道依次占 `104/104/328px`，从 toolbar 下沿连续填满剩余高度。
- Plan 不显示技术 Detector/Transition 独立轨、独立运行轨、Actual 历史轨或不可操作的 runtime status footer；释放空间全部归还三条玩法轨道。
- `运行轨迹` 在相同 `536px` viewport 内保留感知/决策/动作/结果/警告五轨，每轨 `99.2px`；它与 Plan 共享外壳，但不伪装成相同的轨道语义。
- 全屏顶部栏只保留在 Setup。统一工作台的会话信息与暂停/继续、单帧、重置、返回卡组收进右侧 `60px` 会话栏，Game 从 `y=0` 开始渲染。
- 右侧详情内容从会话栏下沿连续铺到共享 toolbar 上沿；Plan 默认显示“当前行为”摘要并按需展开四步因果，Runtime 默认显示四步因果；选择 Plan 元素或 Runtime 事件后在原位切换详情。
- Runtime toolbar 只负责历史与视口；兼容节点 `btnTimelinePlayPause` 永久隐藏且无交互，唯一播放控制位于右侧会话栏。
- 后续启用可拖动分割线时，Plan 与 Runtime 分别记忆自己的高度与 viewport。
- 分割高度是 session UI 状态，不进入 Draft 或 semantic hash。
- Reset/Release 恢复当前分辨率默认值。
- 增高时间轴时压缩 scene 高度，但默认镜头必须持续看到所有 selected participants。

## 11. 响应式规则

| 分辨率 | 右栏会话栏 | Upper | Timeline | Inspector | 行为 |
| --- | ---: | ---: | ---: | --- | --- |
| 2560×1440 | 64 | 620 | 756 | 固定右列 + 按需抽屉 | 同一 Workbench；Plan 三轨按比例增加行距 |
| 1920×1080 | 60 | 412 | 608 | 固定 520px 右列 | 标准 Plan 验收基线 |
| 1280×720 | 52 | 300 | 368 | 固定紧凑右列 / 按需抽屉 | 低频命令进 `…`，保持 Scene 和决策均可读 |

### 11.1 1280×720 紧凑规则

- Scene 不得完全折叠，选中角色和目标必须仍可辨认。
- Plan timeline 最小 `340px`，默认约 `368px`。
- 三轨可在内部垂直滚动，但轨道标题必须 sticky。
- 不通过增加缩略总览补偿空间。
- breadcrumb 允许只显示 `Card / Stage`，完整路径进入 tooltip。
- `卡库`、snap、zoom、fit、validate 和 Apply 保持可达。
- `当前决策` 右列压缩到紧凑宽度；长字段和修复动作进入抽屉，不创建新页面。

低于 `1280×720` 不作为完整 authoring 验收目标。可进入只读查看态并显示明确分辨率提示。

## 12. Timeline 内部尺寸

`1920×1080`、Plan timeline 根高 `608px` 时：

| 子区域 | 高度 | 说明 |
| --- | ---: | --- |
| Mode/command toolbar | 72 | 模式、breadcrumb、卡库、编辑和应用命令；按钮高 52px |
| 当前阶段 | 104 | Stage planned interval、current/selected 状态 |
| 主动作 | 104 | 中文玩法动作、A/B 按需 overlay、ownership conflict |
| 分支出口 | 328 | 同源互斥出口共享菱形节点；三行多列确定性排布 |

多余高度按以下顺序分配：

1. 分支出口；
2. 主动作；
3. 当前阶段。

不得把多余高度分配给隐藏的未来 minimap 占位。

## 13. 视觉语言

### 13.1 基础风格

目标界面继承当前 Runtime Test，不建立新的“独立编辑器主题”：

- 平面蓝灰 Scene chrome；
- 不透明黑色/深蓝时间轴；
- 细描边、大留白、低装饰；
- 不使用大面积渐变、厚重 bevel、霓虹或另一套 sci-fi panel；
- 同一状态在 Scene、因果卡、Inspector 和 timeline 使用一致语义色。

### 13.2 颜色 token

| Token | 建议值 | 用途 |
| --- | --- | --- |
| `surface.timeline` | `#071219` | 时间轴主背景 |
| `surface.toolbar` | `#0D1C25` | toolbar/Inspector 背景 |
| `surface.ruler` | `#09171F` | 标尺 |
| `border.neutral` | `#376F7C` | 普通边界/scroll thumb |
| `state.selection` | `#37D8C8` | 选择、playhead、current Stage |
| `state.selectionStrong` | `#5CE5D5` | focus/drag preview |
| `state.action` | `#2E6FA8` | 主动作 |
| `state.detector` | `#D58A1B` | Detector window/hit |
| `state.transition` | `#9B5BD1` | Transition/pending |
| `state.compare` | `#F0A020` | comparison B |
| `state.blocking` | `#F04444` | blocking conflict |
| `text.primary` | `#F1F5F6` | 标题/主要标签 |
| `text.secondary` | `#87A8B1` | metadata |
| `text.disabled` | `#566A73` | unavailable/read-only |

正式颜色由 `MxUiFoundation` semantic resources 物化；实现不得在 binder 中散落硬编码颜色。

### 13.3 类型与字号

用户确认旧版 `12–16px` 正文在 Game 帧中过小。大字号版以 `1920×1080` 的逻辑像素为
权威基线，正文级信息约放大一倍；不能只放大字而保留旧行高，必须同步增高 toolbar、
ruler、track、按钮和右侧摘要行。

| 内容 | 1920×1080 逻辑字号 |
| --- | ---: |
| 右侧 `当前决策` 标题 | 36 px |
| 右栏测试名称 / 状态帧号 | 22 / 16 px |
| Workbench / Card title | 28 px |
| 因果链标题 / 值 | 28 / 26 px |
| Section / sticky track title | 28 px |
| Stage、Action、出口 clip label | 26 px |
| Ruler、playhead、运行快照 | 24 px |
| Mode / toolbar / Apply 按钮 | 24 px |
| 动作摘要标签和值 | 24 px |

`1280×720` 继续作为 compact 目标单独验收，允许整体响应式缩放和把低频命令收入 `…`，
但不得把 FHD 基线重新降回旧版小字号。

#### 13.3.1 大字号版运行效果

以下均为真实 Play Mode Game framebuffer，不是静态概念图：

![Runtime AI Lab Behavior Plan 大字号版 1920×1080](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-plan-large-type-final-1920x1080.png)

![Runtime AI Lab Behavior Plan 大字号版 2560×1440](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-plan-large-type-final-2560x1440.png)

#### 13.3.2 三轨玩法可读与无标尺修订

BT2-03 在早期五轨基础上进一步合并技术 Detector/Transition 轨，并取消 Plan 的全局帧标尺
与竖向网格，把空间全部分配给三条玩法轨。以下五轨帧仅作为旧版空间演进证据：

![Runtime AI Lab Behavior Plan 五轨紧凑标尺 3840×2160](../Images/CharacterTest/RuntimeAiLabBehaviorTimelineV2/bt2-plan-five-track-compact-ruler-3840x2160.png)

主要按钮和 clip handle 的有效命中区至少 `44×44px`。可见 handle 可以更窄，
但 hit area 必须扩展。

### 13.4 状态表现

- Cyan：selected、current、committed path。
- Amber：Detector hit、warning、comparison B。
- Violet：Transition、pending、interruption policy。
- Red hatching：blocking conflict only。
- Solid neutral rail：Latest Actual。
- Dashed amber rail：Comparison B。
- Gray：inactive、unarmed、unavailable、historical read-only。

任何状态不得只靠颜色表达。

## 14. FairyGUI 组件与权威

### 14.1 复用计划

| 现有能力 | v2 用法 |
| --- | --- |
| `RuntimeAiPlannerDevPanel` | 保留为统一 Workbench root |
| `RuntimeAiLabTimeline` | `运行轨迹` mode 内容 |
| `RuntimeAiLabBehaviorTimeline` | 保留 command、gesture、viewport 和 binder 生命周期，替换为 `行为计划` mode 的三轨 `GameplayReadablePlanProjection` |
| `RuntimeAiLabBehaviorCardLibrary` | 保留 query/filter/paging/state，迁移为卡库 drawer |
| `RuntimeAiLabTimelineStageCard` | 复用 stable-id binding、selection、gesture 和 pooling，改用轨内 Stage clip 外观 |
| `RuntimeAiLabTimelineDetectorBand` | 复用 Detector identity/window；投影为“全局”监听区间或分支出口事实 |
| `RuntimeAiLabTimelineTransitionChip` | 复用 Transition identity/command；投影为成功/失败/条件出口与共享分支节点 |
| Tool/Icon buttons | 复用现有 compact/neutral/primary recipes |
| Current decision semantics | 保留 typed projection与固定右栏；Plan/Runtime 选择更新同一上下文列，不创建第二套 truth |
| Draft controller/compiler | 不创建第二套编辑 authority |
| RuntimeFrame timeline/domain | 不创建第二套历史 truth |

### 14.2 目标组件树

```text
RuntimeAiPlannerDevPanel
├─ workbenchHeader
├─ sceneViewportAnchor
├─ currentDecisionPanel
├─ timelineModeHost
│  ├─ timelineModeToolbar
│  ├─ runtimeFrameTimeline
│  ├─ behaviorPlanTimeline
│  ├─ behaviorLogicFlow
└─ workbenchDrawerHost
   ├─ playerDeckDrawer
   ├─ behaviorGroupDrawer
   ├─ behaviorCardLibraryDrawer
   └─ advancedDiagnosticsDrawer
```

### 14.3 目标稳定 child contract

以下名称是 Composer blueprint 和 runtime binder 的目标合同：

| Child | 类型/职责 |
| --- | --- |
| `sceneViewportAnchor` | Scene 可用矩形和 UI hit-test exclusion |
| `timelineModeHost` | `编辑逻辑 / 行为计划 / 运行轨迹` 三种模式的共同裁剪容器 |
| `contextualInspectorOverlay` | 选中 Runtime/Plan/Logic 项时按需显示的共享 Inspector 浮层 |
| `btnTimelineModeRuntime` | 选择 `运行轨迹` |
| `btnTimelineModePlan` | 选择 `行为计划` |
| `btnTimelineModeLogic` | 选择 `编辑逻辑` |
| `planBreadcrumb` | Card/Stage 路径 |
| `btnPlanCardLibrary` | 打开卡库 drawer |
| `btnPlanSnap` | snap preset |
| `btnPlanZoomOut` / `btnPlanZoomIn` | horizontal zoom |
| `btnPlanFitSelection` | fit selected items |
| `btnPlanFitCard` | fit complete card |
| `btnPlanValidate` | 显式验证 |
| `btnApplyDraft` | 复用现有 Apply |
| `trackPlanStages` | Stage track |
| `trackPlanCardDetectors` | Card Detector track |
| `trackPlanStageDetectors` | Stage Detector track |
| `trackPlanPrimaryActions` | Primary Action track |
| `trackPlanTransitions` | Transition track |
| `trackPlanRuntimeOverlay` | Actual/A-B track |
| `planPlayhead` | logical-time cursor |

三种 `btnTimelineMode*` 使用同一组 `ButtonMode.Check` 页签视觉：只有当前模式显示
高对比青绿色选中底色。`btnPlanCardLibrary` 是一次性的抽屉操作，保持深色中性工具按钮
并显示卡组图标，不使用页签选中态，避免与“编辑逻辑”产生同级、同义的错觉。
| `planHorizontalScrollTrack` | pan scrollbar |
| `planHorizontalScrollThumb` | pan thumb |
| `workbenchDrawerHost` | 单一 drawer host |

首个 v2 contract 不得出现 `planMinimap`、`timelineOverview`、
`timelineThumbnail` 或等价 child。

### 14.4 物化规则

- `MxCharacterTestRuntimeAiLabUi` 是 Composer-owned package。
- Screen Blueprint、component recipes、binding contract 和 semantic resources 是 authoring authority。
- `package.xml`、component XML、atlas 和 `*_fui.bytes` 只能由 UI Composer/FairyGUI helper 受控物化和发布。
- Runtime binder 只绑定稳定 child、池化动态实例并写入 ViewModel。
- Runtime 代码不得创建替代 package XML、临时拼装权威 UI 或修补 published bytes。

## 15. Timeline 时间域

### 15.1 计划时间

- 固定为整数 logical frame。
- UI 使用 session 固定 `60 Hz` 转换成秒。
- frame 是存储和编辑 authority，秒只是展示。
- Plan zero 是当前 Behavior Card 开始帧，不是 RuntimeFrame absolute frame。
- Stage `PlannedStartFrame` 相对 card start。
- Card Detector `ActivationStartFrame` 相对 card start。
- Stage Detector `ActivationStartFrame` 相对 owning Stage entry。

### 15.2 运行时间

RuntimeFrame 使用 session absolute frame。Plan 和 RuntimeFrame 不共享同一个原点。
Typed event linkage负责在两个时间域之间定位，UI 不通过文案或估算换算。

### 15.3 标尺

- `行为计划` 不显示全局帧刻度、秒刻度、纵向网格或全局播放头；内部仍用整数帧计算几何。
- 固定时长事件在块内显示 `当前帧/总帧`，例如 `9/300`；开放事件显示 `当前帧/∞`。
- `运行轨迹` 继续保留其历史帧标尺、major/minor tick 和历史定位播放头。
- zoom 以 pointer 为锚点。
- `Fit card` 包含所有 authored plan interval，不包含无限视觉延伸。

### 15.4 Snap

目标 preset：

| Preset | 用途 |
| --- | --- |
| `1f` | 默认，精确编辑 |
| `5f` | 小窗口 |
| `0.1s / 6f` | 常用节奏 |
| `0.25s / 15f` | 兼容当前默认手势 |
| `0.5s / 30f` | 大段结构 |
| `Off` | 自由预览，提交仍量化到整数 frame |

`Alt` 临时反转当前 snap 状态。当前仅有 `15f` toggle 的实现迁移为 preset，
不得继续把 snap 语义硬编码为单一 `15f`。

## 16. 三条玩法可读 Plan 主轨合同

### 16.1 `当前阶段`

展示 Stage 的 planned interval、entry/current/selected 状态和中文玩法名称。主视觉不显示
stable id；完整技术身份只进入 Tooltip/诊断。

Duration mode：

| Mode | 视觉 | 编辑 |
| --- | --- | --- |
| `Fixed` | 明确左右边界 | 可移动和 resize |
| `UntilEvent` | 右端 continuation chevron | 可移动；duration 表示计划/最小窗口 |
| `Infinite` | 向 viewport 边缘延伸并显示 `∞` | 可移动；无伪造固定终点 |

Stage span 重叠不等于并行 authority。互斥分支可以在计划时间上重叠，但必须用 route
badge 区分。当前 runtime 永远只有一个 active Stage。

旧卡可能只定义行为图顺序，而把多个开放 Stage 的 `plannedStartFrame` 都保留为 `0`。Plan
不得因此把这些 Stage 叠画在一起：presentation projection 按 `layout.Column / Order / source index`
生成确定性的非重叠展示位置，`60` 帧只作为开放 Stage 的最小结构宽度，不代表执行上限、
动作时长或运行进度。运行中的高亮仍严格来自 `CurrentStageIndex`，后续 Stage 不得遮住当前 Stage。

### 16.2 `主动作`

Primary Action 不是第二套 schema。首个 v2 中：

- clip 由 owning Stage 的 kind、action reference、planned start 和动作权威 duration 投影；
  不得用 Stage 计划 duration 伪造动作终点；
  read-only action preview 投影；
- 主标签使用中文玩法动词，例如“选择并写入最近的有效敌人”；
- 拖动 action clip 等价于移动 owning Stage；
- 当前只读动作 clip 不提供独立 resize；后续编辑必须回写对应动作权威配置，而不是偷偷修改 Stage；
- Ability/CharacterAction 的真实内部时长和关键窗口只读显示；
- Runtime AI Lab 不写 Ability/CharacterAction authoring data。

同一路径上可同时 reach 的两个 primary-action owner 重叠时是 blocking conflict。
互斥 route 的视觉重叠不阻断，但必须标记 branch identity。

### 16.3 `分支出口`

- `ActionCompleted` 投影为绿色“成功”出口，`ActionFailed` 投影为红色“失败”出口。
- `ConditionEdge` 投影为橙色条件出口，主文案直接说明 typed condition fact。
- Stage-scope Detector 与 Transition 一一对应；同一源阶段的互斥出口连接同一菱形节点，
  不得横向画成先后事件。
- Card-scope Detector 显示带“全局”前缀的监听区间，并保持自己的 stable identity。
- Target 为其他 Stage、`End`、当前 Stage 或之前 Stage 时，分别显示“进入… / 结束行为卡 /
  重复当前阶段 / 回到…”。
- 无出口 Detector、缺失目标和不支持语义显示结构化占位，不隐藏或猜测。
- 多出口使用确定性的三行多列排布；卡片只放玩法摘要，完整监听窗口、目标、policy、
  stable id 和运行状态进入 Tooltip/右侧详情。
- 条件出口不得只写“条件”：紧凑主文案使用“条件事实 → 目标”，例如“存在有效目标 → 等待”。
  自环使用“继续‘阶段名’ ↻”，避免“进入当前阶段”的歧义。
- 每个 Stage 的菱形分支节点通过位置和 Tooltip 保留来源身份，不常驻显示来源阶段名；连接线
  使用低对比度细线，不能比事件块更醒目。出口存在动态监听进度时也采用上下两行：上行为
  事实与玩法目标，下行为局部监听帧。
- `TransitionPending`、`TransitionCommitted`、未触发和不可用必须通过颜色之外的文案区分。

### 16.4 块内事件进度

- 行为计划是行为卡内部的逻辑时长尺，不是测试会话的全局历史时间轴。
- Stage 只表达 authored 结构区间与 Current/Selected/Conflict，不显示执行填充。
- `CharacterAction` 从活动 CharacterAction Gateway 的 `InstanceId/ActionId/LocalFrame/DurationFrames`
  读取确定进度，并严格匹配 Stage 引用的动作 ID；`Wait`
  从 `ActionEnteredFrame/WaitFrames` 读取；能解析到活动 CharacterAction 的 `UseAbility` 同理。
- `Logic` 阶段在动作轨投影为“判断事件块”：Fixed 使用 Stage local frame 与 authored logical
  duration 显示确定帧进度；UntilEvent/Infinite 使用 Stage local frame 显示 `当前帧/∞`，
  运行中只高亮事件块，不显示进度条或流光。Stage 轨本身仍不显示进度。
- 固定动作显示青色块内填充和 `当前帧/总帧`；完成显示满格与 `✓ 总帧/总帧`；失败显示
  红色状态；超时只在块内显示 `! 总帧+/总帧`，不得向右无限延长。开放动作完成或失败
  保留实际局部帧，分别显示 `✓ 当前帧/∞`、`× 当前帧/∞`，不得伪造 `1/1`。
- Move、Legacy movement、UntilEvent、Infinite 和无法解析权威时长的 UseAbility 只高亮事件块，
  并显示 `已运行帧/∞`，不绘制进度条、流光，也不伪造百分比或固定终点。
- Wait 不依赖 `PrimaryActionStageIndex`；当前 Wait Stage 直接使用
  `ActionEnteredFrame/WaitFrames` 显示确定进度。
- Detector/Exit 使用自己的 activation window：固定窗口按已监听帧数填充并显示
  `当前帧/总帧`；StageEnd/CardEnd 等开放窗口使用整卡高亮和 `当前帧/∞`；Triggered
  对 Fixed 显示满格，对开放事件只显示完成高亮与触发标记。多个监听器可以同时活动并各自推进。
- 动作完成但 Stage 尚未退出时，动作保持满格，工具栏显示“动作完成 · 监听中”，出口继续
  独立推进；阶段停留时间不得重新解释为动作进度。
- `当前帧` 在玩家界面中表示已覆盖帧数而非零基索引：Runner `LocalFrame=0` 显示 `1/N`，
  `LocalFrame=N-1` 显示 `N/N`；底层 authored/Runner 帧索引仍保持 `0..N-1`。
- 回边或自环再次启动同一动作时，这是新的 CharacterAction 实例：动作块局部帧从零开始，
  显示轻量 `↻`，总帧继续使用该实例运行计划的权威时长；不得跨实例累计。
- Plan 不绘制全局帧刻度、纵向网格、全局播放头、独立运行轨或会话活动点。
- toolbar 不再重复显示行为卡标题或独立“执行中”状态，而以池化字段显示
  `逻辑 <当前逻辑动作> · <块内帧> · 命令 <角色实际命令>`；动态帧文案复用块内进度缓存，
  稳定刷新 NoAlloc。活动 Detector 不得投影成“下一步”。
- 出口卡以“监听 · / 触发 · / 已进入 ·”区分条件求值、命中与提交；其中“监听”不表示条件
  已成立，只有 Triggered/Pending/Committed 才能表达流程正在离开当前阶段。
- 动作块存在动态进度时固定使用上下两行：上行为玩法动作名，下行为局部帧徽标；两者不得
  共享同一矩形或互相覆盖。开放事件徽标使用 `N 帧 / ∞`，不把 `∞` 拼进动作名称。
- 仅 latest + exact identity 显示动态进度；identity mismatch、历史帧和无运行状态分别只显示
  “未执行”“历史帧”“暂无运行”。阶段切换后上一阶段恢复静态计划态。
- 当前块内进度不保留本轮历史，不生成 Actual interval，不提供 typed 证据跳转，不进入 Draft
  或 semantic hash；完整 Latest Actual 仍归 BT2-05。

### 16.5 完整运行证据（BT2-05，按需叠加）

- Latest Actual 的 reached interval、actual duration 和未到达状态叠加在对应 Stage、
  Action 或 Exit visual 内，不创建第四条轨道。
- comparison B 使用相同轨内 dashed rail，不新增底栏。
- 选择证据 overlay 可打开 typed event detail 或显式切到 `运行轨迹`。
- 无兼容样本时只在共享 Inspector/Tooltip 给出 typed empty state，也不按标题或顺序猜测。

## 17. Toolbar 设计

Toolbar 从左到右分为五组：

| 组 | 控件 | 规则 |
| --- | --- | --- |
| Mode | `运行轨迹`、`行为计划`、`编辑逻辑` | 同级 segmented control |
| Context | Card/Stage breadcrumb、`卡库` | breadcrumb 自适应宽度 |
| Edit | Add、Duplicate、Delete、Undo、Redo | 只在 Logic mode 显示 |
| View | Snap、Zoom−、Zoom+、Fit selection、Fit card | 不修改 Draft |
| Gate | Validate、Publish、`Apply F+1` | Publish 可进入 `…`，Apply 状态常驻 |

`1280×720` 下：

- Mode、breadcrumb、卡库、Undo/Redo、Fit、Validate、Apply 保持直接可达；
- Add/Duplicate/Delete、comparison、Publish 和低频 view command 可进入 `…`；
- 禁止把 Apply 隐藏到二级菜单。

## 18. 选择与输入

### 18.1 Pointer

| 输入 | 结果 |
| --- | --- |
| Click | 单选并绑定 Inspector |
| 再次点击同一 Plan 元素 | 返回“当前计划”总览 |
| Click Plan empty area | 返回“当前计划”总览 |
| Ctrl-click | toggle selection |
| Shift-click | 扩展同类型选择 |
| Empty drag | marquee selection |
| Clip body drag | move |
| Edge handle drag | resize |
| Detector vertical drag | priority reorder |
| Middle drag | horizontal pan |
| Shift-wheel | horizontal pan |
| Ctrl-wheel | zoom about pointer |
| Wheel | 仅在轨道溢出时垂直滚动 |
| Double-click Actual | 切到 `运行轨迹` 并定位 typed event |

UI pointer 必须阻止穿透到 3D Scene。

### 18.2 Keyboard

| Shortcut | 结果 |
| --- | --- |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` / `Ctrl+Shift+Z` | Redo |
| `Ctrl+D` | Duplicate |
| `Delete` | 删除可删除 selection |
| `F` | Fit selection |
| `Shift+F` | Fit card |
| `Esc` | 取消 gesture；否则关闭最深 detail/drawer |
| `Alt` during drag | 临时反转 snap |
| Arrow | 在同轨相邻 item 间移动 focus |
| `Enter` | 打开选中项 Inspector |
| `Space` | 继续使用全局 Play/Pause，不作为编辑快捷键 |

快捷键只在 Workbench 获得键盘焦点且没有文本输入时生效。

## 19. 编辑事务

所有 pointer gesture 使用 preview-first、single-commit：

1. pointer-down 捕获 stable ids、原始值、selection、Draft revision 和 gesture epoch；
2. pointer-move 只更新池化视觉 geometry；
3. preview validator 显示潜在冲突；
4. pointer-up 生成一个 typed mutation；
5. mutation 触发必要的 COW、一个 Undo item、Draft revision、autosave 和 candidate compile；
6. `Escape`、lost capture、authority replacement 或 stale epoch 恢复原始 geometry；
7. gesture 期间不得每帧序列化、编译、分配集合或修改 active evaluator。

跨轨非法移动在 commit 前拒绝。可修复但 blocking 的结果允许留在 Draft，
但 candidate compile/Apply 必须保持 blocked。

## 20. 固定当前决策与按需共享 Inspector

### 20.1 区域分配

- 上区固定分为左侧 Scene 与右侧上下文栏，三个 Tab 均保持该分栏；
- Plan 默认态显示“当前行为”：等待目标、当前事实、实际命令和一条可操作冲突；技术因果链按需展开；
- 选中 Stage/Action/Exit 时，同一右列切换为玩法说明、监听窗口、目标阶段和当前状态；
- 再次点击同一项或点击时间轴空白返回总览；该开关只属于 presentation state；
- Runtime mode 没有选择时仍显示当前 `感知 / 判断 / 行为 / 结果` 和 active action 摘要；
- 需要完整字段编辑、冲突修复或长内容时，升级为同一 `workbenchDrawerHost` 中的共享抽屉；
- conflict state 可以扩展 Inspector，但不得移除 Agent/Frame header 或创建新页面。

### 20.2 Inspector 类型

#### Card Summary

- display name、stable card、Draft/Published/Active revision；
- player legality；
- dirty/save/compile 状态；
- warning/blocking count；
- local Publish 与 Apply 的独立状态。

#### Stage Inspector

| 分组 | 字段 |
| --- | --- |
| Identity | Title、Kind、Entry |
| Timing | Planned start、Duration mode、Planned duration |
| Action | Ability/CharacterAction reference、read-only preview duration |
| Movement | min/max distance，仅适用 kind 显示 |
| Entry | typed entry conditions |
| Notes | designer note |

#### Detector Inspector

| 分组 | 字段 |
| --- | --- |
| Identity | Title、Scope、owning Stage |
| Trigger | ConditionEdge、ActionCompleted、ActionFailed |
| Window | start、duration、end mode |
| Conditions | typed facts/operators/values |
| Arbitration | priority、linked transitions |
| Runtime | armed/hit/rearming，只读 |

#### Transition Inspector

- Detector source；
- source Stage；
- target Stage/End；
- priority；
- policy；
- label/end reason；
- pending/committed runtime evidence。

#### Primary Action Inspector

- owning Stage；
- action/ability semantic name；
- planned interval；
- actual interval；
- completion/failure strategy；
- read-only source-editor link；
- ownership conflict status。

#### Conflict Inspector

- stable diagnostic code；
- severity；
- exact frame/second interval；
- both owners 和 route；
- why reachable；
- repair previews；
- `缩短前一动作`、`后移后一动作`、调整 route/policy 等 typed repair；
- Apply disabled reason。

Designer mode 不显示 raw GUID、hash、JSON path 或 internal command id。需要时由
Developer Diagnostics 显式打开。

## 21. Player Deck、Behavior Group 与卡库抽屉

### 21.1 通用规则

- 同一时刻仅一个主抽屉可见。
- 宽度：`1920` 下 `480–560px`，`1280` 下 `360px`。
- 抽屉从 Workbench 右边缘展开，不切换 full-screen route。
- 背景 scene 只允许轻度 dim，不得全屏不透明覆盖。
- 关闭抽屉恢复相同 Agent、frame、camera、playback、timeline mode 和 viewport。
- 抽屉搜索、filter、scroll 是 session UI state。

### 21.2 卡库

- 搜索名称、tag、能力和 semantic use；
- filter：全部、已解锁、已安装、当前编辑、Draft、warning；
- card 显示 title、cost、revision、installed/current-edit/runtime-active 状态；
- 选择 card 后关闭抽屉、保持 Workbench 并加载其 Plan；
- 无权限/未解锁原因使用 typed projection，不用 raw id。

### 21.3 Behavior Group

- 有序 card instance list；
- add/remove/duplicate/reorder/enable；
- priority/score override 和 player legality；
- 点击实例返回 Plan mode 并聚焦 card。

### 21.4 Player Deck

- 继续复用当前每角色 `6` 槽、`10` 费和安装规则；
- 不在该设计中重新定义 deck authority；
- deck mutation 与当前 Lab Draft card edit 相互独立；
- 关闭后不重建 runtime session。

## 22. Workbench 状态与 Edit Gate

| 状态 | Scene | Plan 查看 | Draft 编辑 | Apply | 说明 |
| --- | --- | --- | --- | --- | --- |
| Setup | 配置态 | 不可用 | Setup 编辑 | 不可用 | 尚无 runtime frame |
| Initialized Paused / Latest | 可见 | 可用 | 可用 | candidate ready 时可用 | 默认 Start 后状态 |
| Live / Latest | 实时 | 可用 | 可用 | 可排队 F+1 | Draft 不直接影响 evaluator |
| Paused Inspect / Latest | 静止 | 可用 | 可用 | candidate ready 时可用 | 推荐精确编辑 |
| Review / Historical | 历史 | 可用 | 只读 | 禁用 | 必须返回 Latest |
| Branch Recompute | transaction | 只读 | 禁用 | 禁用 | 防止 authority 竞争 |
| Apply Queued | 实时/暂停 | 可用 | 禁用 | pending | 完成前不接受新 edit |
| Completed | 最终帧 | 可用 | 可用 | 禁用 | 重新运行后应用 |
| Higher-version Draft | 可见 | 可用 | 只读 | 禁用 | 明确版本原因 |
| Compile Failed | 可见 | 可用 | 可修复 | 禁用 | active evaluator 保持旧版本 |

Edit Gate 原因按以下优先级显示：

1. session/rebuild transaction；
2. historical review；
3. apply queued；
4. higher-version read-only；
5. missing card/authority；
6. blocking validation；
7. compile in progress；
8. ready。

## 23. 完整工作流

### 23.1 从运行观察进入计划编辑

1. Start 进入 Workbench，默认 `编辑逻辑`。
2. 用户选择 Agent 或点击 Scene 中角色。
3. 点击底部 `行为计划` mode；也可从选中的行为事件使用兼容入口进入。
4. Workbench 保持 frame/camera/playback。
5. 底部切到 `行为计划`。
6. 自动解析 active card 和 current Stage。
7. Plan viewport 居中 current Stage；需要详情时按需打开该 Stage/Action Inspector。

### 23.2 从卡库切换卡

1. 点击 `卡库`。
2. 抽屉打开，Plan 和 Scene 保持。
3. 搜索/filter 不修改 Draft。
4. 选择卡片。
5. 抽屉关闭，breadcrumb、tracks 和 Inspector 原子刷新。
6. runtime active card 与 currently edited card 不同时，两者都显示清晰 badge。

### 23.3 编辑、验证与 Apply F+1

1. Move/resize/reorder 或 Inspector edit。
2. COW 如有必要仅执行一次。
3. Draft dirty，Undo/Redo 更新。
4. autosave 与低频 compile 独立运行。
5. warning 不阻止 Apply；blocking/compile failure 阻止。
6. 用户点击 `Apply F+1`。
7. 再次确认 Agent、session generation、card identity 和 candidate revision。
8. immutable revision 从下一 RuntimeFrame 生效。
9. `运行轨迹` 记录 revision apply event。
10. Plan 的 Latest Actual 等待新兼容样本，不伪造即时结果。

### 23.4 冲突修复

1. validator 投影 exact overlap。
2. 主动作轨出现 red hatching。
3. Inspector 说明 owners、routes、interval 和原因。
4. Apply disabled。
5. 用户预览 repair；timeline 显示 ghost geometry。
6. 确认后生成一个 typed mutation 和一个 Undo item。
7. compile 重新运行；冲突消失后 Apply 恢复。

### 23.5 从计划跳到运行证据

1. 选择 Actual rail、Transition commit 或 runtime badge。
2. 使用 typed locator 定位 RuntimeFrame item。
3. 切换底部到 `运行轨迹`。
4. Warm Replay 成功后更新 selectedFrame。
5. Scene、Agent、运行轨迹和 event detail 同步。
6. 返回 `行为计划` 时恢复原 Plan selection/viewport。

### 23.6 A/B

1. 在 `… / 比较` 中选择 A、B run/revision。
2. projection 验证 lineage 和 element mapping。
3. A/B rail 在现有轨道内出现。
4. 差异指标进入 Inspector。
5. 关闭比较只移除 overlay，不改变 Draft、runtime 或 viewport。

### 23.7 Local Publish

1. validated Draft 才允许 local Publish。
2. Publish 与 Apply 使用不同命令和状态。
3. Publish 产生 append-only revision。
4. 不自动安装、不自动 Apply、不修改当前 evaluator。
5. 生产审批/签名/分发不在本文范围。

## 24. Plan、Latest Actual 与 A/B

### 24.1 Plan

当前 Draft 的 semantic authoring truth。所有 horizontal placement、duration、
Detector priority 和 Transition semantic 都进入 semantic hash。

### 24.2 Latest Actual

BT2-03 的块内动作/出口帧进度不等于 Latest Actual。前者是 latest + exact identity 的当前
事件局部状态，没有历史区间、typed locator 或证据完整度；后者必须经过以下
identity/evidence 映射：

精确 clip overlay 要求：

- 相同 runtime contract；
- 相同 semantic hash；
- Published card 使用相同 stable card/revision；
- unpublished card 使用相同 Draft GUID/revision；
- typed stable element id 可解析。

不满足时显示 `无兼容运行样本`。禁止按 title、数组 index 或当前 UI 顺序猜测。

### 24.3 Comparison A/B

A/B 可以比较不同 revision，但必须满足：

- 相同 stable card lineage 或显式兼容 Draft lineage；
- schema/runtime contract 兼容；
- per-item 比较只使用保留的 stable element id；
- 新增/删除 item 标记为 Added/Removed，不伪造成 duration delta；
- 无法映射时只显示 card-level summary。

A/B 指标：

- total card duration；
- reached Stage set；
- committed Transition count；
- rejected primary-action count；
- interruption/pending count；
- per-item actual duration delta；
- conflict/warning count。

A 使用 solid neutral/cyan rail；B 使用 dashed amber rail。不存在独立 compare strip。

## 25. 冲突与诊断

| 级别 | Timeline | Inspector | Apply |
| --- | --- | --- | --- |
| Info | neutral note/icon | 说明 | 可用 |
| Warning | amber outline + badge | 原因与建议 | validation 后可用 |
| Blocking | exact red hatching + error icon | owners、interval、repair | 禁用 |

### 25.1 Blocking 条件

- 同一 reachable route 的 primary-action ownership overlap；
- invalid Stage/Detector/Transition reference；
- missing entry Stage；
- invalid fixed interval；
- `PreserveAction` target 仍拥有 primary action；
- schema/canonicalization required；
- compile failure；
- Apply target/session identity stale。

### 25.2 非 blocking 示例

- 互斥 route 上的视觉时间重叠；
- plan 与 actual duration 超过 warning tolerance；
- 未到达 Stage；
- comparison sample 部分无法映射；
- 无 Latest Actual。

### 25.3 Repair 合同

Repair 必须：

- 使用 typed command；
- preview-first；
- 一个操作对应一个 Undo item；
- 不直接修改 active evaluator；
- 不通过隐藏 priority 或删除 route 静默修复；
- 在 commit 前显示 semantic impact。

## 26. 数据与 authority 边界

| 数据 | Authority | 是否进 semantic hash |
| --- | --- | --- |
| Stage planned start/duration/mode | Lab Draft semantic | 是 |
| Detector window/scope/priority | Lab Draft semantic | 是 |
| Transition source/target/policy/priority | Lab Draft semantic | 是 |
| Ability/CharacterAction reference | Lab Draft semantic | 是 |
| Transition route geometry | Layout | 否 |
| track collapse/height | session/Layout | 否 |
| timeline mode/viewport/zoom | session UI | 否 |
| upper/lower split | session UI | 否 |
| drawer/search/filter | session UI | 否 |
| Inspector selection | session UI | 否 |
| Latest Actual/A-B sample | read-only local evidence | 否 |
| RuntimeFrame records | runtime history | 独立 authority |
| active evaluator | immutable runtime revision | 由 Apply 替换 |

Runtime Tick 不读取：

- Draft；
- UI selection；
- search/filter；
- display labels；
- Actual/A-B projection；
- route geometry；
- tooltip/diagnostic presentation。

## 27. ViewModel 与 projection

### 27.1 单一 truth

- Workbench shell 继续由现有 scene controller/session state 驱动。
- Runtime timeline 继续由现有 RuntimeFrame timeline domain 驱动。
- Plan timeline 继续由 current Logic v3 Draft snapshot 构建。
- Actual/A-B 从 typed event index 和 immutable sample 构建。
- 不新增另一套行为 graph、历史 buffer 或 evaluator。

### 27.2 Plan projection 最小字段

`GameplayReadablePlanProjection` 是现有 ViewModel 的 internal 只读表现适配器，不是新领域模型。
Stage/Detector/Transition、viewport、selection、diagnostics 和 latest runtime state 直接来自
现有 Behavior Card workspace ViewModel；三轨分组、同源互斥出口分组和块内事件进度只属于
presentation。BT2-05 的 Latest Actual/comparison rail 再从 identity projection 与 historical
projection 读取，不反向修改本投影或领域状态。

```text
GameplayReadablePlanProjection
  workspaceRevision
  semanticRevision
  cardIdentity
  durationFrames
  viewportStartFrame
  viewportEndFrame
  zoom
  selection
  tracks[4]
  diagnostics
  currentSnapshot
  latestActual       // BT2-05 optional overlay
  comparison         // BT2-07 optional overlay
```

每个 item 至少包含：

- stable identity；
- kind/track；
- start/end frame；
- duration mode；
- semantic/read-only；
- selected/current/reached；
- severity/diagnostic code；
- runtime locator；
- owner/route identity；
- display key，不缓存拼接后的每帧字符串。

### 27.3 Revision

以下变化必须递增 projection revision：

- Draft semantic mutation；
- COW identity replacement；
- undo/redo；
- card switch；
- compile/validation state；
- actual sample replacement；
- comparison selection；
- RuntimeFrame retention/branch change 造成 locator 失效。

Viewport pan、hover 和 drag preview 不递增 semantic revision。

## 28. 命令复用与迁移

继续复用现有行为卡 command：

- open/select Stage/Detector/Transition；
- add Stage/Card Detector/Stage Detector/Transition；
- move/resize Detector window；
- adjust priority；
- cycle Transition policy/target；
- remove/duplicate；
- Undo/Redo；
- zoom/pan/fit；
- Stage gesture/field editing。

只允许新增或扩展以下表现层 command；表中没有列出的编辑命令继续直接复用：

| 语义 | 目标 |
| --- | --- |
| Set workbench mode | `编辑逻辑 / 行为计划 / 运行轨迹` 原地切换 |
| Open/close drawer | Player Deck、Behavior Group、卡库、advanced |
| Fit selection | 区别于当前 Fit all |
| Select Actual evidence | 绑定 Inspector，不 seek |
| Navigate Actual evidence | 复用现有 typed timeline locator/seek，只增加 mode 切换编排 |
| Select comparison sample | 本地 read-only overlay |
| Execute conflict repair | 新增 typed repair intent，内部调用现有 move/resize mutation 并进入同一 Undo |

迁移期保留旧 command id alias；不得修改 `MxFramework.AI` public API。

## 29. 保存、恢复与发布

- Draft semantic edits继续进入现有 workspace/group/card persistence。
- timeline mode、viewport、zoom、selection、split 和 drawer state 仅保留在当前 Lab UI session。
- 不把 Latest Actual/A-B 写入 Draft JSON。
- autosave failure 持续可见，不能被 Apply/Publish 成功覆盖。
- Undo/Redo 只作用于 semantic Draft mutation，不包含 pan/zoom/selection。
- higher-version Draft 保持只读。
- `.previous` 恢复不恢复运行历史或 compare sample。
- local Publish append-only，并与 Apply 分离。

## 30. Loading、Empty、Read-only 与错误态

| 状态 | 主轨表现 | Inspector/动作 |
| --- | --- | --- |
| Loading card | skeleton tracks，不显示伪数据 | `正在加载行为卡` |
| No card selected | 空轨 + CTA | `打开卡库` |
| Empty valid card | Entry placeholder | `添加阶段` |
| Draft compile pending | 保留旧 projection + pending badge | Apply disabled |
| Compile failed | 保留 invalid Draft visual | 显示 typed diagnostics |
| No actual sample | Plan 正常，overlay empty | `无兼容运行样本` |
| Historical | gray read-only treatment | `返回 Latest 后编辑` |
| Apply queued | selection 可读、edit locked | queue frame/status |
| Stale authority | 不自动重试 | 明确 session/Agent/card changed |
| Drawer data unavailable | Workbench 不退出 | drawer error + retry |

错误不得清空 Draft、切换 Agent、重置 Scene 或回退到 legacy Loadout。

## 31. 可访问性与本地化

- 正式 UI 文案使用简体中文；类型名仅在 developer diagnostics 中保留。
- raw GUID、hash、JSON path、Ability id、CharacterAction id 不进入普通 label/tooltip。
- icon 同时有 tooltip 和 keyboard focus label。
- selection、warning、blocking、Actual、B comparison 不能只靠颜色。
- red conflict 同时使用 hatching、icon 和文字。
- focus 顺序：mode → context → edit → view → gate → tracks → Inspector。
- 所有核心操作可仅用键盘完成。
- 文本放大到 `125%` 时 track label 不截断关键语义；长名称省略并提供 tooltip。
- 右到左语言不是首个验收范围，但布局不得把文本写死到图片。

## 32. 性能预算

- Stable Live playback：不得每帧创建 FairyGUI display object。
- 所有 clip、handle、label、curve、badge、Actual rail 和 conflict overlay 必须池化。
- 仅物化 visible horizontal range 和少量 overscan。
- Actual overlay 最多 `10 Hz` 更新，并仅改 changed geometry。
- drag preview 不序列化、不编译、不创建临时集合、不格式化长字符串。
- pointer-up 只执行一次低频 mutation/validation/compile。
- tooltip 懒加载并按 stable key 缓存。
- `900f` 常规卡和 `1800f` 压力卡在 `1920×1080`、`2560×1440` 下保持可交互。
- `256` 个 visible instances 为首个压力上限 fixture。
- 模式切换不得重建 Scene、RuntimeHost 或完整 FairyGUI root。
- 卡库 drawer 使用 paging/virtualization，不一次 materialize 全 catalog。

性能验收记录：

- stable 10 秒 Live UI GC；
- mode switch allocation；
- 100 次 pan/zoom；
- 50 次 drag preview + commit；
- 900/1800 frame projection rebuild；
- drawer open/search/close。

## 33. 迁移设计

### Phase 0：合同冻结

- 本文和两张效果图成为 visual/interaction baseline。
- 冻结第 4 节的“已有 / 搬迁 / 新增”复用矩阵。
- 修正相关文档中的当前实现边界，禁止把已实现能力列入开发 backlog。
- 不改 runtime behavior。

### Phase 1：统一 Timeline Host

- 复用现有 Workbench root、scene controller、RuntimeFrame timeline 和 Behavior Card workspace state。
- 只增加 `运行轨迹 / 行为计划` host/mode state。
- 上区固定保留 `Scene + 当前决策`；仅下半区在既有 Runtime 五轨与 Plan 三轨之间切换。
- 两种模式固定使用 `x=0, y=472, w=1920, h=608` 的共享下区外壳；Runtime root 继续覆盖上区详情，但其 toolbar 与 viewport 分别对齐到全局 `y=472/544`。
- Tab 固定为 `148×52`、24px，均位于全局 `y=482`；模式切换不得改变位置、尺寸或字号。
- Runtime 保留五轨，Plan 保留三条玩法轨，两者各自记忆 viewport 与 selection。
- 把旧 tab command 映射为 mode alias，不复制 controller。
- 证明模式切换保持 Agent/frame/camera/playback。
- 当前 Behavior Card page 继续兼容。

### Phase 2：抽屉与共享 Inspector

- 复用卡库 query/filter/paging、Player Deck/Behavior Group controller 和现有选择命令。
- 只增加 drawer host、容器 binding 和 Back/Escape 层级。
- 复用现有 typed field editor，把选中项 Inspector 迁入按需浮层/共享抽屉。
- 旧页面只保留兼容入口。

### Phase 3：三轨玩法可读 Plan projection

- 直接消费现有 Stage/Detector/Transition ViewModel、selection 和 diagnostics。
- 只新增 internal `GameplayReadablePlanProjection`、三轨 sticky layout、互斥出口分组、
  精确块内事件进度和轨内 pooled visual。
- 保留现有 Logic v3、gesture、COW、Undo/Redo、validator 和 compiler，不改语义。
- 在现有 `15f` snap/fit 基础上增加 preset、Fit selection 和 Fit card。
- 不实现 minimap。

### Phase 4：Actual、A/B 与冲突

- 复用现有 runtime identity、historical projection、typed event locator、actual duration、
  RuntimeFrame A-B loop 和 candidate comparison report。
- 新增 Latest Actual 轨内表现适配与 incompatible/unknown 状态，不新增历史 buffer 或常驻轨。
- 新增 Plan 逐元素轨内 A/B mapping，不改变现有 RuntimeFrame A-B loop 语义。
- 由现有 planned interval 与 `ActionConflict` 构建 exact overlap geometry。
- shared Inspector repair action 只编排现有 move/resize mutation，并进入现有 Undo。

### Phase 5：收口旧路由

- Trigger 覆盖所有旧入口的替代路径。
- 无调用方依赖独立 Behavior Card/Diagnostics 页面后再隐藏旧 route。
- 删除只属于旧导航的常驻 tab，不删除底层兼容 controller capability。

每个 Phase 单独可验收。任何 Phase 都不得重写 controller、Draft、compiler、evaluator、
RuntimeFrame timeline 或 UI authority；若发现真实领域缺口，必须从该 Phase 拆出独立设计和测试。

## 34. Trigger 验收设计

验收只通过官方 controller/command sink/Trigger 调用和 Game render frame，不依赖
Computer Use。

### 34.1 功能 Trigger

| Scenario | 必须断言 |
| --- | --- |
| `mode_switch_preserves_context` | mode 切换前后 Agent、frame、Latest、camera、playback、card 相同 |
| `open_plan_from_causal_action` | 点击/命令进入 Plan 并选中 active card/stage，无页面导航 |
| `card_library_drawer_switch` | drawer 开关和换卡不重建 session |
| `plan_stage_move_resize` | integer frame、单 Undo、save/reload 保持 |
| `plan_detector_window_priority` | window/priority 改变 semantic hash 和 arbitration |
| `plan_undo_redo` | geometry、Draft、compile state 完整往返 |
| `plan_conflict_blocks_apply` | exact conflict、Apply disabled、repair Undoable |
| `plan_apply_f_plus_one` | candidate 只在下一 RuntimeFrame 生效 |
| `plan_historical_readonly` | historical 不可编辑/Apply，返回 Latest 恢复 |
| `plan_actual_navigation` | typed locator 导航，无 UI text parsing |
| `plan_compare_in_track` | B rail 在现有轨道内，无独立底栏 |
| `plan_drawer_back_stack` | Back/Escape 只关闭最深层，不重置 Workbench |
| `plan_long_card_1800f` | pan/zoom/fit/selection 稳定 |

### 34.2 视觉 Trigger

必须采集：

1. `1920×1080` 正常 Plan；
2. `1920×1080` conflict + A/B；
3. `2560×1440` 正常 Plan；
4. `1280×720` compact Plan；
5. 卡库 drawer；
6. historical read-only；
7. compile failure；
8. no actual sample；
9. long names/large card。

每张 render frame 必须检查：

- 角色可见；
- Scene 不被 opaque panel 覆盖；
- 只有一个 Workbench；
- `编辑逻辑 / 行为计划 / 运行轨迹` 同级；
- 三个 Tab 使用同一底部入口并保持同一上区上下文；标题和工具动作可以不同，但 Tab、toolbar 和 viewport 外边界必须一致；Runtime 保留历史标尺，Plan/Logic 不显示全局时间标尺；
- 三条 Plan 玩法轨道可读；
- 没有 minimap/thumbnail/overview strip；
- 固定右侧 `当前决策` 可读；选择详情不新建页面且不改变既定 Scene 宽度；
- Apply 状态真实；
- 文本无重叠、溢出或 raw id/hash。

## 35. 验收矩阵

| 类别 | 场景 | 通过标准 |
| --- | --- | --- |
| Navigation | 进入 Plan | 不创建 full-screen page |
| Context | mode switch | Agent/frame/camera/playback 全保持 |
| Layout | 1920×1080 | 上区 `1400×472 Game + 520×472 属性栏`；属性栏前 `60px` 是唯一会话控制、下方 `412px` 是当前决策；下区两模式共享 `1920×608` 外壳，Plan 三轨和 Runtime 五轨均填满 viewport |
| Typography | 1920×1080 / 2560×1440 | 正文级信息为 24–28px，`当前决策` 标题 36px；无重叠、裁切或退回旧小字号 |
| Layout | 1280×720 | Scene/角色可见，核心命令可达 |
| Space | no overview | 无 minimap、thumbnail、compare bottom strip 或空占位 |
| Drawer | card/group/deck | 可关闭、非 route、无 simulation mutation |
| Selection | multi-select | 稳定 identity 和 Inspector |
| Gesture | move/resize | preview-first、one commit、one Undo |
| Snap | presets | 1f 默认，15f 兼容 preset，Alt 可反转 |
| Stage | duration modes | Fixed/UntilEvent/Infinite 不混淆 |
| Detector | scope/window | card/stage origin 正确 |
| Priority | reorder | semantic hash 与同帧仲裁改变 |
| Transition | route/policy | forward/back/loop 和 pending/commit 可读 |
| Action | ownership | reachable overlap 精确阻断 |
| Actual | exact mapping | incompatible sample 不猜测 |
| A/B | in-track | rail/指标确定性且不改 Draft |
| Historical | read-only | 返回 Latest 前不能编辑/Apply |
| Apply | F+1 | active evaluator 原子切换 |
| Publish | separation | 不隐式 Apply/install |
| Error | compile/save | invalid Draft 保留，旧 evaluator 继续 |
| Accessibility | non-color | icon/text/hatching 同时存在 |
| Performance | stable live | 无 display object churn |
| Long card | 1800f | pan/zoom/fit 和 drag 可用 |
| Regression | offline core | 现有相关回归和 Trigger suite 保持通过 |

## 36. 实现切片与交付物

可直接进入开发的任务拆分、依赖顺序、复用证据、文件边界和逐切片验收见
[Behavior Timeline v2 实现切片](../Tasks/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_TIMELINE_V2_IMPLEMENTATION_SLICES.md)。
下表是六个设计域的粗粒度分组；执行文档将它们拆成 BT2-00～08 九个独立提交，
执行顺序与验收边界以任务切片文档为准。

切片按“复用输入 + 新增交付物”定义。`复用输入` 不是待实现清单，不允许在对应
Slice 中重写：

| Slice | 必须直接复用 | 该 Slice 真正新增的交付物 |
| --- | --- | --- |
| 1. Shell convergence | Workbench root、scene controller、session/page context、两个现有 timeline workspace | timeline mode host/state、mode buttons、旧 route alias、context-preservation tests、1920/2560 render frame |
| 2. Drawer + Inspector | card library query/filter/paging、Player Deck/Behavior Group controller、typed field commands、现有返回语义 | drawer host、card/group/deck 容器 binding、共享 Inspector 容器、Back/Escape UI 层级、no-session-mutation tests |
| 3. Five-track visual | Logic v3 ViewModel、selection、diagnostics、现有 pooling/gesture binder | five-track presentation adapter、sticky track visual、tall/responsive layout、no-overview preflight |
| 4. Editing extension | move/resize/marquee/multiselect、Detector priority、field edit、Undo/Redo、validator、zoom/pan/Fit all、`15f` snap | snap preset state/UI、Fit selection/card、现有 gesture 到新轨 visual 的 binding；不新增编辑 authority |
| 5. Evidence + conflict | runtime identity、historical projection、actual duration、typed locator、`ActionConflict`、candidate comparison、现有 mutations | Latest Actual rail、Plan A/B element mapping、exact conflict geometry、repair intent/Inspector UI、mode-switch navigation adapter |
| 6. Acceptance closeout | 当前已通过的离线核心回归与 Trigger 场景 | 仅新增路径的 Trigger matrix、1920/2560/1280 images、900/1800 frame UI 性能、docs sync、legacy route removal decision |

每个 Slice 必须包含：

- 本 Slice 的复用映射与“未重写既有领域能力”检查；
- Composer preflight/Apply/Publish；
- Unity refresh/compile 和 Console error check；
- focused tests；
- Trigger functional evidence；
- Game render frame；
- docs/acceptance update；
- 精确 Git scope。

除非改动触及已有领域代码，否则 Slice 6 不重复执行已经通过的完整离线核心验收；
focused regression 只覆盖被搬迁的 binding、alias 和新 projection。

## 37. 明确延后

以下内容不是 v2 首轮验收缺陷：

- minimap/thumbnail/overview strip；
- 多窗口编辑；
- shared cursor/shared Undo；
- workspace process lock 和 collaborative export；
- production catalog approval/signing/distribution/rollback；
- cloud save/account/multiplayer；
- 通用 behavior graph debugger；
- Ability/CharacterAction 源时间线编辑。

如果未来重新评估 minimap，必须以真实 `1800f+` 使用证据证明
zoom/pan/Fit 不足，并重新评估它对三条主轨高度的影响；不得预留空白提前占位。

## 38. Definition Of Done

Behavior Timeline v2 只有同时满足以下条件才可称为完成：

1. 当前产品只暴露 Setup 和一个持久 Workbench 的主流程。
2. Plan/Runtime mode 切换不改变任何上下文 authority。
3. 两张效果图的视觉层级在真实 Game render frame 中成立。
4. 三条 Plan 玩法主轨在三种验收分辨率下可读，且当前执行只在 exact identity 时轨内覆盖。
5. 时间轴下没有缩略总览或独立 A/B 底栏。
6. 卡库、Player Deck、Behavior Group 是 drawer/picker，不是新页面。
7. 共享 Inspector 覆盖 Card、Stage、Detector、Action、Transition、Actual 和 Conflict。
8. move/resize/priority/Undo/Redo/validation/Apply F+1 全部走现有 typed authority，且没有平行实现。
9. Latest Actual 和 A/B 不进入 Draft、SaveState、network 或 runtime authority。
10. historical review 严格只读。
11. 900/1800 frame 性能预算通过。
12. Trigger、render frame、docs health、link check 和相关回归均有可复核证据。

统一 Host 的 `编辑逻辑` Tab 已完成入口与视觉收口；旧 `BehaviorCard` route 仅保留兼容
重定向。Behavior Timeline v2 的整体完成仍取决于本节其他条件，尤其 Latest Actual、A/B、
完整共享 Inspector、长卡性能和响应式收口。

这里的“未完成”只指统一工作台的组合与视觉验收，不否定第 4.1 节列出的既有功能已经实现。
