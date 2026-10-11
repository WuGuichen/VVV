# CharacterTest Runtime AI Lab 行为配方 v5 实施台账

> Status: Archive — v5 已由统一 Event 与条件树的 v6 取代。当前权威见 [v6 实施台账](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_V6_IMPLEMENTATION_LEDGER.md)。
>
> Started: 2026-08-11
>
> Last updated: 2026-08-12
>
> Design authority: `Docs/Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md`
>
> Runtime contract authority: `Docs/Guides/CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_CARD_PAYLOAD.md`

## 1. 台账用途

本文只记录行为配方“大时间轴”v5 的实际实施状态、验证证据、剩余验收和已知限制，不重新定义产品或运行语义。发生冲突时：

1. Timeline/Event 的产品与交互语义以设计文档为准；
2. schema、迁移、编译、运行、快照和动作交接以 Runtime contract 文档与代码为准；
3. 本文仅回答“当前做到哪里、由什么证据证明、还有什么没有证明”。

状态只使用：`Pending`、`In Progress`、`Implemented`、`Validated`、`Blocked`。

## 2. 已锁定边界

- `behavior-card-logic.v5` 的 Timeline/Event 是保存、编译、运行和调试的统一权威。
- 一个行为配方同时只有一个当前 Timeline 和一个 PrimaryAction 所有者。
- 每条 Timeline 使用独立局部帧，进入目标 Timeline 后从其 `0f` 开始。
- 用户界面不显示 Track；Event 类型对应的内部行只用于防止重叠，不保存、不命名、不参与运行。
- Trigger 只有 `Predicate`、`LocalFrame`、`PrimaryActionSignal` 三类来源。
- Trigger 只有 `EnterTimeline`、`ReturnToPlanner` 两类结果。
- Timeline 跳转属于高层动作编排边界，使用 CharacterAction orchestration 请求，不借用普通取消窗口或配置重载原因。
- 行为配方只编排动作引用；伤害、碰撞、VFX、SFX 等仍归 CharacterAction/Ability 时间轴。
- v4 内容先按 v4 hash 验证，再在内存迁移；旧运行快照和旧 replay 不静默转换。
- 本轮视觉范围仅为最低可用编辑闭环；进一步高度、颜色、卡片密度和动效精修不计入运行基础完成条件。

## 3. 里程碑状态

| 里程碑 | 状态 | 已实现结果 | 当前证据 / 剩余项 |
| --- | --- | --- | --- |
| V5-M1 数据、迁移与哈希 | Validated | v5 DTO、严格校验、v4 认证后迁移、规范化语义哈希、正式 JSON 往返 | v5 schema/migration/hash 专项测试已通过；标题、备注和视口状态不进入语义哈希 |
| V5-M2 编译器与确定性运行器 | Validated | 稳定 Timeline/Event 索引、单 Timeline 游标、Predicate 边沿、LocalFrame crossing、动作实例信号、单帧唯一结果 | v5 evaluator 与行为逻辑专项测试已通过；无限 Move/Logic 不伪造结束帧 |
| V5-M3 CharacterAction/Ability 交接 | Validated | `ReplaceForOrchestration`、`TerminateForOrchestration`、原子替换、Ability 成功后提交冷却 | CharacterAction runner 与真实 CharacterTest runtime module 专项测试已通过 |
| V5-M4 快照、回放与运行历史 | Validated | snapshot schema 7、runtime contract 2、Timeline/Event 状态、pending 跳转、动作实例和结构化原因已进入快照；历史事件保留 Event 来源/结果/目标/局部帧/实例/原因 | 单元级 snapshot round-trip 与真实历史帧恢复均已通过；历史 UI 用例已改为断言 v5 Timeline/Branch Event 归属 |
| V5-M5 最低编辑闭环 | Implemented | 新建 Timeline、三类 Trigger、两类结果、主行为配置、时间/条件/原因/优先级、Undo/Redo/Save/Reload、上下文菜单 | 工作区、graph mutation、published package 与 PlayMode 菜单测试已通过；不包含本轮后的视觉精修 |
| V5-M6 正式行为卡升级 | Validated | “持续接敌”发布为原生 v5，三个具体 Predicate 均 `ReturnToPlanner` | Catalog/正式卡测试通过；真实 Trigger 已打开运行中大时间轴并观察到距离事件结束配方 |
| V5-M7 真实场景完整验收 | In Progress | 已验证真实 Game View、当前 Timeline、本地播放头、三个具体 Event 与一次实际 Predicate 命中 | 仍需把三 Timeline 连续动作强制切换和 Ability 成功/冷却拒绝整理为同一套最终场景证据 |
| V5-M8 保存闭环 | Implemented | 草稿落盘、重新编译、frame-zero 重建、Overlay 安装、角色/槽位/authority 核对，以及重算回保存前帧已合并为一次事务；正式发布独立 | Unity 编译通过；旧本地 catalog 恢复专项测试通过；仍需完成真实 Game View 单击闭环证据 |
| V5-M9 TriggerEvent Inspector | Validated | Branch Event 直接投影并修改 v5 Event；正式 520×600 Inspector、条件 GList、两类结果、优先级、目标定位和原生删除已接入；用户概念统一为“当条件成立，则执行结果”，不显示事件类型或 SourceKind；监听时间由 Event 几何直接编辑 | Composer Apply/Publish、Unity 编译、v5/发布包/选择/创建/删除专项测试通过；EntryEvent/PrimaryActionEvent Inspector 仍待后续迁移 |

## 4. 实现落点

### 4.1 数据、迁移、编译和运行

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorRecipeV5.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorRecipeV5Compiler.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorRecipeTimelineCanonicalizer.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorCardLogic.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorCardLogicJson.cs`

### 4.2 动作交接与运行桥

- `Packages/com.vincent.wgameframework/CharacterAction/CharacterActionExecutionGateway.cs`
- `Packages/com.vincent.wgameframework/CharacterAction/CharacterActionRunner.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat/CharacterTestCharacterActionRuntime.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Commands/CharacterTestRuntimeCommandTypes.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge.cs`

### 4.3 快照、回放与历史事件

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerStateSnapshot.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerCommandModule.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabTimelineProjection.EventIndex.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.Timeline.cs`

### 4.4 编辑器与正式内容

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/BehaviorRecipeTimelineProjection.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/BehaviorRecipeTriggerEventInspectorViewModel.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder.Recipe.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler.Edit.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.TriggerEventEditing.cs`
- `Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_card_catalog.json`

### 4.5 保存与本地恢复

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.SaveRun.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabBehaviorRecipeSaveRun.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerLocalPlayerDeckService.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.cs`

事务只有在真实运行 Overlay 的 profile slot、所选角色 EntityId 与 candidate authority hash 全部一致后，才提交 Active revision 和“运行中”状态。操作日志写入 `Logs/CharacterTest/runtime-ai-lab-behavior-recipe-editor.log`，每次命令保留 operationId、草稿/候选/安装 hash、角色、槽位、阶段和失败码。

## 5. 当前验证证据

### 5.1 Unity 编译

- `f4ad0c9891294b429c73442539116bc0`：保存并运行事务、固定双按钮和本地目录恢复接入后，refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。
- `c4f74a08973846bda5372a53552c5d85`：本地目录恢复写回边界修正后再次通过 refresh、compile、domain reload、Editor ready 与 Console error=0。

- `03d8925b118c4a509209ca6d4aac0113`：移除可见 Track 标签栏后，refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。
- `d02f7606fe0e4cdc93d5e12e6bb22af9`：完整运行代码与事件历史字段修正后，refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。
- `8f3c8d98448b4a0a9bbd8695709ba183`：历史帧测试诊断更新后，仅重编译测试程序集，Console error 为 0。
- `3e8880c93fe44bfeaa76c3d9ee0e06b8`：历史帧断言切换到 v5 Timeline/Branch Event 后再次通过 refresh、compile、domain reload、Editor ready 与 Console error=0。
- `4843a765fdc446e2b0fcbfaa0477d62b`：提交前最终验证再次确认 refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。

现有 obsolete warning 属于仓库已有 Projectile 兼容 API 使用，不是本改造新增错误。

### 5.2 EditMode / PlayMode

- `9c5599912936434998a0b367151d0527`：旧本地行为卡目录不兼容时安全回退、保留旧文件并在下一次有效保存后写回当前基础身份，EditMode `1/1` 通过。
- `d279699ea90b43a68e8702d97b7d8722`：同一旧目录恢复专项在“保存并运行”整合后的最终实现上再次 `1/1` 通过。
- `8f86b0b889e34991b9ad335eb7b280e3`：Trigger 自动化发现、窗口合同、场景模块边界与源码布局 `21/21` 通过。
- `0fab47eefb4649be9b6660b579e53922`：旧交互阶段的保存运行事务证据；其中“无修改仍可再次重跑”的行为已经被 2026-08-13 的“仅有修改时可保存”规则取代，不再作为当前 UI 权威。

- `3bb65d95be8046c3af6c059e82c8d1db`：Timeline/Event 投影测试 `2/2` 通过；时间轴身份已合并进 PrimaryActionEvent。
- `ce52bb4efc8f485b9e71ab8aff4ed9e2`：正式 FairyGUI 包与事件区几何测试 `1/1` 通过；Track 标签隐藏且事件内容从 `12px` 开始。
- `ed4c1283d1ef442a87c2b3121cefc79d`：PlayMode 主行为 Event 右键新增 Trigger 与 Undo 测试 `1/1` 通过。
- `22d82dd535374c73ad66ce2daf6b5618`：7 组相关测试合计 `243/243` 通过。
- `6b568491fd4b4a44b8785d5c67c2a724`：真实 CharacterTest runtime module 定向测试 `8/8` 通过。
- `a2f12f2f941a4fbc8234ebae23d3e3b2`：v5、行为逻辑与 Timeline truth 定向测试 `61/61` 通过。
- `b30b9f1a9bc24c2796e73d8d83df3c7a`：大时间轴上下文菜单 PlayMode 测试 `1/1` 通过。
- `6da1b06dc065463b81910249f8385780`：真实场景历史帧恢复与 v5 Timeline/Branch Event 投影测试 `1/1` 通过。
- `d2e8f9a8bcda46658861b55270710d3d`：v5、行为逻辑、Timeline truth、CharacterAction runner 与历史帧恢复合计 `105/105` 通过。

### 5.3 真实 Game View / Trigger

- Trigger：`mx.trigger.test.character-test.runtime-ai-lab-open-running-behavior-logic`。
- 实际结果：成功进入运行中的 Timeline 模式行为逻辑。
- 实际帧可见：`持续靠近`、`接近当前战斗目标`、`目标存在 = 否`、`目标距离 ≤ 1.5 米`、`可用移动路径 = 否`。
- 运行历史实际记录：具体距离 Event 命中后，下一帧行为卡结束并返回 Planner；没有再依赖“正常结束/重新决策”字符串解释行为。
- 修正后的结构化历史记录实际值：`SourceKind=Predicate`、`OutcomeKind=ReturnToPlanner`、`TimelineId=approach`，Event 局部帧分别为 `70f` 和 `74f`；这些字段经过历史索引后不再退化为 `0f` 或空来源。
- 截图：`Temp/CodexCaptures/behavior-recipe-v5-runtime.png`。该文件是本地验收证据，不是版本化资产。

## 6. 验收矩阵

| 能力 | 自动化 | 真实场景 | 状态 |
| --- | --- | --- | --- |
| v5 JSON 往返、唯一 ID、严格校验 | 已覆盖 | 不适用 | Validated |
| v4 认证、迁移、反向 Activation 退出 Event | 已覆盖 | 通过正式卡加载路径间接覆盖 | Validated |
| 语义哈希与非语义字段排除 | 已覆盖 | 不适用 | Validated |
| Predicate 边沿与重新 armed | 已覆盖 | 已观察距离 Predicate 命中 | Validated |
| LocalFrame crossing / PrimaryActionEnd | 已覆盖 | 三 Timeline 场景待最终归档 | In Progress |
| 动作实例精确匹配 | 已覆盖 | 三 Timeline 场景待最终归档 | In Progress |
| 同帧优先级与 EventId 决胜 | 已覆盖 | 不要求人工复现 | Validated |
| CharacterAction 强制编排替换 | 已覆盖 | 三 Timeline 场景待最终归档 | In Progress |
| Ability 冷却提交顺序 | 已覆盖 | 成功/拒绝场景待最终归档 | In Progress |
| snapshot schema 7 round-trip | 已覆盖 | 历史帧恢复与 v5 Branch Track 已通过 | Validated |
| 当前 Timeline、本地播放头、具体 Event | 已覆盖 | 已在 Game View 核对 | Validated |
| Undo/Redo、Save、Reload | 已覆盖核心 mutation/persistence | 最终组合交互待归档 | In Progress |

保存与发布在运行界面中是两个稳定、独立的命令：竖向命令栏固定显示“保存”与
“发布版本”。“保存”只在存在未保存修改时启用；它保存可恢复草稿、重建第 0 帧测试、
核对 Overlay authority，并在后台重算到保存前所在帧后恢复原播放/暂停状态。“发布版本”只有在草稿已经保存且
具备可追溯正式来源时启用，只追加正式 revision，不替换当前运行快照。

## 7. 已知限制与非目标

- 兼容 Stage/Detector/Transition 对象仍存在于 C# 编辑适配层，但不序列化进原生 v5 JSON，也不作为运行权威。
- 空 EntryEvent 仍存在于数据层；紧凑 UI 不为“无条件入口”占用独立事件行。
- v4 Card-scope Detector 只通过 `LegacySharedTriggerEvent` 迁移产生，不允许新建；第一版不自动拆分。
- CharacterAction/Ability 的 Event 宽度由引用动作的权威长度决定，行为层不能伪造修改。
- MoveToTarget 和 Logic 没有权威结束帧，不能创建 `PrimaryActionEnd`。
- 当前完成的是运行逻辑基础和最低编辑闭环，不代表大时间轴的视觉密度、颜色、字体与动效已经最终验收。
- Branch Event 已使用直接 v5 Inspector；旧 Detector/Transition 表单不再进入其活动编辑路径。
- Branch Event 不再提供“条件事件 / 时间事件 / 动作信号事件”分类选择；Predicate、LocalFrame 和 PrimaryActionSignal 只作为底层触发条件表示，Inspector 统一按“当……则……”呈现。
- Branch Event 的开始帧、固定持续帧和开放监听形态由大时间轴直接表达；Inspector 不再提供重复的监听配置行。
- EntryEvent 与 PrimaryActionEvent 仍使用旧详情组件，尚未纳入本轮原生 Inspector 迁移。

## 8. 收尾清单

- [x] 让历史帧场景测试直接断言 v5 Timeline/Branch Event，而不是旧 Detector 名称。
- [x] 重跑 v5、行为逻辑、历史帧和 CharacterAction focused tests。
- [x] 在真实 Trigger 运行历史中确认 `SourceKind`、`OutcomeKind`、`LocalFrame`、目标 Timeline、动作实例和原因码完整保留。
- [ ] 归档三 Timeline 连续强制动作切换的真实场景证据。
- [ ] 归档 Ability 成功和冷却拒绝不误提交冷却的真实场景证据。
- [x] 移除可见 Track 名称/标签栏，把 Timeline 身份合并到 PrimaryActionEvent，并将 `224px` 留白收回为事件宽度。
- [x] 最终执行一次 Unity 脚本验证、Console error 检查和工作区范围检查。

## 9. 工作区边界

本实施只应纳入行为配方 v5、CharacterAction orchestration、Runtime AI Planner snapshot/history、最低编辑器、正式行为卡和对应文档/测试。CharacterTest Settings、恢复场景、`FGUIProject/.agent` 结果以及其他无关本地改动不属于本里程碑，不得因本台账被自动纳入提交。

## 10. 编辑操作诊断日志

行为配方编辑器的低频写操作会追加到本地日志：

`Logs/CharacterTest/runtime-ai-lab-behavior-recipe-editor.log`

日志不属于版本化内容。每行使用制表符分隔，记录 UTC 时间、会话、顺序号、操作阶段、行为卡草稿身份与 revision、Event/Detector ID、修改前后数量、结果和诊断。删除分支事件至少应出现：

1. `ui-confirm`：用户已经完成界面确认；
2. `command`：命令进入身份校验或要求确认；
3. `mutation`：v5 Trigger Event 和兼容投影的草稿修改结果；
4. `refresh`：修改成功后请求重新投影和刷新 UI。

该日志用于定位真实 UI 交互链，不能代替 Unity 编译、专项测试或 Game View 验收证据。

“保存”事务使用独立表头追加到同一日志，记录 `operationId`、workspace/group/card
身份、草稿语义哈希、candidate/installed authority hash、选中 EntityId、profile slot、
停止阶段和失败码。成功行必须同时满足 candidate 与 installed authority hash 相等。

### 10.1 本轮最终验证

- `e40643c42c8d46a9a36344cd9fdbbefc`：最终脚本 refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。
- `1b02e3f6d74a4aefb718f8c43739613e`：修复“保存并运行后当前实验室 UI 消失”后，脚本 refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。
- `ec461a7d68eb47499e854cdf2b6e4af5`：真实 Runtime AI Lab 场景验证“保存并运行”通过 Shell 会话启动事务原地重建；行为逻辑面板、时间轴模式和当前行为卡选择保持不变，连续播放帧继续推进，`1/1` 通过。
- `b942a134f0b5461b900b3c276316db17`：保存成功安装 Overlay 并播放、保存无效草稿但不启动两条事务回归测试，`2/2` 通过。
- `531b18d0543842f28b66e9e2e3042012`、`6a4db4d4a6f84e8eb011e2905f04bf01`：按钮改名、Dirty 门控和原帧重算测试调整后，两次 Unity refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。
- `46581dc7f77a4823a67942f397d79fff`：有修改时保存成功安装并核对 Overlay，保存后无修改再次调用被 `NO_UNSAVED_CHANGES` 拒绝，运行仍连续推进，EditMode `1/1` 通过。
- `ad05ddf8157e44b4b3cf3e5695bd44bd`：运行中保存会重建首帧前 baseline、从 `0f` 重算到保存前所在帧并恢复暂停状态，EditMode `1/1` 通过。
- `af74a29f8b594c1d8054cf8c3a7e76e5`：干净草稿下按钮标签固定为“保存”且不可点击，EditMode `1/1` 通过。
- 2026-08-13：显式保存检查点与恢复草稿的 `LastSavedRevision` 已拆分；后台自动落盘后切换左侧行为卡不会再让“保存”按钮变暗，只有成功执行显式保存才会清除待保存状态。
- `45942206027b4116910ab50adbdcc341`、`16a5fef3af384e63b757c6c8ca7050d4`：显式保存检查点接入及测试调整后，Unity refresh、compile、domain reload、Editor ready 全部成立，Console error 为 0。
- `45b173bfba4e42b0ae6a9cb7643b61fd`：恢复自动落盘后切换左侧行为卡仍保持显式“保存”可用，同时干净草稿仍禁用保存，EditMode `2/2` 通过。
- `0a6c65c01a3b4e5aa62add6bc8d36ec6`：显式保存成功后检查点被确认，无修改再次保存继续被拒绝，场景事务 EditMode `1/1` 通过。
- 当前本机旧目录实际触发 `PLAYER_DECK_CATALOG_RECOVERY`，服务继续以正式 catalog 安全启动；专项场景随后仍成功安装 Lab Overlay，证明恢复警告没有把 profile slot 退化为 `overlay=null`。
