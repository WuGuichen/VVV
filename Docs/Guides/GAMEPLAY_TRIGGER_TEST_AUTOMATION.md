# Gameplay Trigger Test Automation Guide

> Status: Guide
>
> 适用范围：Development Agent 创建、运行、索引和删除测试 Trigger。

## 1. 定位

Trigger Automation 是 `MxFramework.Tests` 内的测试层能力。它复用正式
`GameplayTriggerAuthoringDocument`、Config Compiler、`RuntimeHost`、
`IGameplayTriggerActionSink` 和 Simulation Harness，但不修改正式 Trigger
Runtime、catalog/hash、SaveState 或 Replay contract。

系统采用两段式验证：

1. 类型化 C# Scenario 完成确定性输入、Trigger function call 和独立断言。
2. Development Agent 根据 external evidence descriptor 完成 Unity compile、
   Console、PlayMode、场景状态、截图或 custom-tool 证据。

当前不提供运行中暂停、外部结果回填、继续协议、JSON Scenario、复杂编排 UI 或
CLI。Unity Editor 只提供一个可停靠的小型人工执行窗口。

人工窗口在 Scenario 尚未运行时会调用 `Scenario.Build(...)` 构建静态 Plan，
再根据其中注册的 Function call id 构造与运行时 fixture 相同的
`GameplayTriggerAuthoringDocument`，从中投影事件源、条件树、路由策略，以及
有序 Action 的目标、FunctionId 和固定参数；同时读取 Function 主动声明的固定执行
前置条件，并附上 Scenario 的验收声明，展示为“Trigger 定义”。界面将 Trigger
路由谓词称为“触发过滤”，将 Function 执行前检查称为“执行前提”；`PushTrue`
显示为“触发过滤 · 无”，避免被误解为行为可在任意环境执行。
主视图只保留“事件、触发过滤、触发方式、执行前提、行为、验收”；DefinitionId、
RouteId、ActionKindId、CallId、BindingId、Capability 和默认策略等调试信息收进
对应行的悬停提示，非默认的次数、冷却、Presence 和 Stay 间隔仍直接显示。
该过程不创建 fixture，也不执行 Step、Function 或 Assertion；运行后同一区域切换为
真实逐操作执行链。

Scenario 选择器提供名称 / 分组 / ScenarioId 搜索，并按“环境准备、测试控制、
时间轴观察、实验设置”分组；也可以筛选当前可执行项、Edit Mode 项或 Play Mode
项。不可执行项不会从目录中消失，仍可选择查看 Trigger 定义和受阻原因，但运行按钮
保持禁用。选择过的项会进入本次窗口会话的“最近使用”，并支持方向键、Enter 和
Escape 操作。

## 2. 管理方式

权威目录是：

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/TriggerAutomation/
├─ Scenarios/<module>/<case-slug>/<ScenarioName>.cs
├─ Functions/
└─ Runtime/
```

每个 Scenario 使用 `mx.trigger.test.<module>.<case-slug>` 稳定 ID。程序集启动时
只发现同时实现 `ITriggerTestScenario` 且带 `TriggerTestScenarioAttribute` 的
类型，并按 ID 排序。重复 ID、缺少无参构造、attribute/descriptor 不一致、
FunctionId 未注册或 capability 不兼容都会阻断运行。

目录本身是人工索引；动态索引写入
`Temp/MxFramework/TriggerTests/index.json`。不存在第二份手写 Catalog，因此
人工删除整个 Scenario 目录后不会留下注册项。误删通过 Git 恢复。

## 3. Scenario 规范

Scenario 的 `Build(...)` 按顺序声明：

1. 注册公共或 scenario-local `ITriggerTestFunction`。
2. 为稳定 FunctionId 注册 Scenario 内局部 CallId 和类型化 arguments。
3. 注册 fresh fixture factory。
4. 安排固定帧输入 step。
5. 安排独立 assertion。

测试专用 function action 固定使用 `GameplayTriggerActionKindId(2000000001)`：

- `Payload0` 是正数 CallId；
- `Payload1`、`Payload2` 必须为零；
- `TryPrepare` 先验证 CallId 并预留 bounded slot；
- `Abort` 幂等释放 reservation；
- `Commit` 只提交调用记录，不执行 C# handler；
- Runner 在完整 Host Tick 后按 reservation sequence 执行 handler。

因此 handler 的异常、分配或 Unity adapter 行为不会改变 Trigger 的事务结果。
`TryPrepare/Abort/Commit/TryDequeue` 初始化后必须 NoAlloc；发现、handler 和报告
属于 LowFreqAlloc。

## 4. 函数与断言

公共函数优先放在 `Functions/`。现有函数不足时，Development Agent 可以在
Scenario 目录新增专用 handler，但必须：

- 使用 `mx.trigger.test.function.<module>.<name>` 稳定 FunctionId；
- 显式声明 `PureRead`、`RuntimeMutation` 或 `UnityAdapter` capability；
- 显式注册，不通过字符串方法名或任意反射调用；
- 不持久化 shell、网络或任意工具命令。

`NoEngine` Scenario 可以使用 `PureRead` 和 `RuntimeMutation`，不能注册
`UnityAdapter`。

断言通过 `ITriggerTestAssertion` 独立读取 Trigger、Gameplay、Story、Buff、
SaveState 或 authority hash。测试函数的返回值只能证明 handler 自身执行结果，
不能作为 Trigger 正确性的唯一 oracle。正式 catalog 集成测试不得插入测试
Action；它只能通过正式输入和只读 Probe 验证。

## 5. 状态与报告

`TriggerTestRunResult` 有三种状态：

- `Passed`：内部测试通过，且无外部证据要求。
- `Failed`：step、Tick、function、assertion 或 teardown 失败。
- `PendingExternalEvidence`：内部测试通过，但 Development Agent 尚需完成外部
  证据；不能描述为完整通过。

最新报告写入：

```text
Temp/MxFramework/TriggerTests/<scenario-id>/latest.json
Temp/MxFramework/TriggerTests/<scenario-id>/latest.md
```

报告复用 `FrameworkSimulationScenarioResult` 的 metrics、timeline 和 failures；
失败保留首个失败帧、ScenarioId、step/function/assertion ID。

## 6. 人工 Editor 入口与执行环境

Unity Editor 菜单 `MxFramework/Testing/Trigger Automation` 打开紧凑执行窗口。
窗口按 Scenario 的 `ExecutionProfile` 约束运行环境：

- `NoEngine` 可在 Edit Mode 或 Play Mode 运行，不驱动 Game View。
- `EditMode` 只允许在非 Play 状态运行。
- `PlayMode` 只有进入 Play 后才允许执行，并保持 Game View 可见。

`PlayMode + UnityAdapter` 仍使用测试专用 Trigger catalog。完整 Signal、Trigger
intent 和 Function ActionSink 提交结束后，UnityAdapter function 才能调用现有
场景命令入口；不能直接写 Transform、角色状态或正式 Trigger catalog。断言必须
重新读取场景 Probe，不能把 function 返回值作为唯一成功依据。

当前 Scenario catalog 只保留带 `manual` 标签的现场操作。Runner、ActionSink、
格式化和报告器的 NoEngine 覆盖使用不可发现的单元测试 fixture，不注册为
Scenario，也不写入索引。

Runtime AI Lab 的基础进入链路由两条独立的原子 Trigger 组成：

1. `打开 Runtime AI Lab 场景`：只在 Edit Mode 加载目标场景，并独立验证活动
   场景路径和唯一活动 Controller。切换场景前如发现未保存的已加载场景，会安全
   失败并提示先处理修改，不自动丢弃用户内容。
2. `进入 Play Mode`：只在目标场景校验通过后提交 Play Mode 请求。执行状态通过
   `SessionState` 跨 Domain Reload 保留；进入 Play 后继续验证活动场景、唯一
   Controller 和 Setup 阶段，再输出最终通过或失败结果。

两条 Trigger 不自动串联，便于人工分别观察“场景加载”和“Play 启动”两个边界。
每条都走完整 Signal → Intent → Function 链，显示逐操作日志和独立断言结果。

日常进入运行中编辑界面时，使用组合 Trigger `一键打开行为逻辑编辑`。它从
Edit Mode 开始，自行打开目标场景、跨 Domain Reload 进入 Play、通过 Controller
正式入口开始测试、选择当前运行行为，再通过稳定语义标识
`TimelineModeBehaviorLogic` 进入行为逻辑编辑模式。显示名称和 Tab 排序不属于
Trigger 契约；只有重新读取并确认 Running、暂停首帧、RuntimeFrame 工作区、
Behavior Logic 模式选中以及行为时间轴可见后，报告才会写为 `Passed`。底层原子
Trigger 仍保留用于逐段诊断，不再要求日常使用者手工串联它们。

第三条真实表现用例是 Runtime AI Lab 单帧推进：要求当前场景只有一个活动 Lab
Controller、测试已经开始、处于暂停且选中最新帧。成功后真实 Runtime、
presentation、Game View、时间轴和游标共同推进一帧。

基础会话控制继续拆成四条原子 Trigger：

- `开始测试`：要求 Setup 配置可运行；验证进入暂停的 Running 状态，并已捕获
  首个帧缓存和时间轴记录。
- `继续模拟`：只接受已开始且暂停的会话；验证 `Paused=false`。
- `暂停模拟`：只接受正在运行的会话；验证 `Paused=true`。
- `重置测试`：只接受已开始的会话；验证返回暂停的 Setup 状态，且帧缓存和
  时间轴均已清空。

虽然 Controller 的正式入口使用 `TogglePause()`，测试 Catalog 不提供
“切换暂停”这种结果依赖当前状态的 Trigger。继续与暂停分别在执行前校验状态，
因此每条 Trigger 的含义、日志和预期结果始终确定。

时间轴与新未来控制拆成五条原子 Trigger：

- `定位到指定历史帧`：在小窗口输入当前保留窗口内的目标帧；同步定位时校验最终
  选中帧恰好等于输入值，分片确定性恢复时校验活动恢复目标恰好等于输入值。
- `定位到最新帧`：通过正式 `SelectLatestFrame()` 返回实时末端，并采用同样的
  完成/恢复目标校验。
- `开启历史帧重算`：要求已暂停且已完成历史帧定位；只建立风险确认请求，验证
  `BranchConfirmationPending`，不提前修改未来时间线。
- `确认建立新未来`：只确认等待中的请求，验证 `BranchRecomputeArmed`；原未来
  仍会保留到下一次真实帧推进完成原子提交。
- `取消历史帧重算`：可取消等待确认或已经确认但尚未提交的重算，并验证两种
  状态均被清理。

时间轴观察与事件详情再拆成七条原子 Trigger：

- `放大时间轴` / `缩小时间轴`：调用正式 `ZoomTimeline()`，并验证 Controller
  的缩放级别与实际 Timeline Viewport 一致。
- `适配完整时间轴`：调用正式 `FitTimeline()`，验证手动缩放级别与视窗覆盖均
  已恢复为完整实验。
- `选择指定时间轴事件`：在小窗口输入可选择事件序号；按稳定轨道顺序解析 typed
  event，选择后详情保持打开，并校验实际 StableKey 与输入序号对应事件一致。
- `定位事件起点` / `定位事件终点`：复用所选事件的 typed locator 与正式
  `NavigateTimelineItem()`；校验相应端点成为最终选中帧或确定性恢复目标。
- `关闭事件详情`：显式清除当前事件选择，并验证详情不再拥有选中事件。

“选择事件”和“打开详情”没有拆成两个伪操作：Runtime AI Lab 的正式语义是选择
事件即显示详情，关闭详情即清除选择。单帧事件没有独立终点时，终点 Trigger 会
返回明确失败原因，不伪造定位。

统一时间轴宿主另有一条组合 Trigger：

- `切换运行轨迹 / 行为计划`：通过真实 FairyGUI Tab 点击依次执行
  `Plan → Runtime`，再通过旧 `BehaviorCard → RuntimeFrame` 路由 alias 复验兼容入口。
- 每一步都验证 Shell 的活动工作区仍是 `RuntimeFrame`，FairyGUI 根没有被替换，
  智能体、选中帧、暂停状态和 Game 镜头均未变化。
- 同一条 Trigger 继续点击右侧唯一会话控制，覆盖暂停、单帧、继续，并使用下方历史
  工具验证前一帧与最新帧定位；完成后恢复进入 Trigger 前的暂停状态。已完成会话不再
  伪造播放变化，但仍验证两个 Tab 可用、重复播放按钮隐藏且上下文保持。
- Plan 的“本次运行”只在 latest + exact identity 时断言真实“第 N 帧”快照；身份不匹配时
  必须出现明确的“暂无匹配记录”空态，不允许用其他智能体或旧行为卡记录补位。

Setup 配置控制拆成五条原子 Trigger：

- `选择测试槽位`：稳定选择队伍 1 的第 2 个槽位，并验证 Controller 的当前
  Setup 选择。
- `选择槽位角色`：通过正式角色注册表选择 Codex Flow Test，并验证角色 ID 已
  写入当前槽位。
- `选择槽位行为组`：从实际草稿目录选择一个不同且编译通过的行为组，并验证
  槽位引用仍可成功编译。
- `启用所选槽位` / `禁用所选槽位`：采用幂等的明确目标状态；底层只在状态
  不同时调用正式 Toggle 入口，随后重新读取槽位数据断言。

另有一条面向行为组副本实装链路的组合 Trigger：

- `使用木桩副本开始测试`：从项目行为组目录找到保留四类无动作占位行为的
  `木桩 副本`，通过真实 FairyGUI 选择命令写入槽位并刷新当前草稿工作区，立即
  保存后启动测试；独立 Probe 再从首个运行帧确认实际 loadout 正是该副本，而非
  默认近战或原始木桩。

“木桩副本”组合 Trigger 使用与 UI 相同的“保存并运行”控制器事务，独立核对草稿
candidate authority、实际 Overlay authority、角色 EntityId、profile slot、连续播放状态
和实际 loadout；不再通过“保存草稿 + 开始测试”两条命令拼接结果。

这些 Setup Trigger 只允许在 `Setup` 阶段运行，不会在运行中的测试会话里修改
候选配置。

玩家创作与发布主链路另有一条组合 Trigger：

- 进入流程后先把 Runtime AI Lab 工作区切换到独立临时恢复目录；不得改写用户的
  `latest-workspace.txt`，也不得把旧验收模板写入真实草稿。
- 从 Player Deck 打开 Published Behavior Card 的隔离 COW Draft。
- 执行一次真实语义编辑，并完成 Undo/Redo 往返。
- 通过正式本地 append-only Publish 生成下一 revision，返回卡组后移除旧卡、
  安装新 revision 并重开确认。
- 回到 Setup 后启动已知可运行的测试配置，通过正式 `Apply F+1` 请求在下一帧
  应用候选；最终重新读取运行帧、typed event 和 authority hash 断言边界生效。

该组合场景用于覆盖跨工作区产品主链路，不替代前述原子 Trigger。测试只在同步
验收中通过 test-only adapter 推进一次 Controller 的正式 pending-apply Update
边界，不修改生产运行时语义，也不写入正式 Trigger catalog。

## 7. Development Agent 工作流

1. 搜索是否已有相同 ScenarioId、FunctionId 或公共函数。
2. 新建单一 Scenario 目录，使用 fresh fixture。
3. 确认测试 Trigger 不进入正式 catalog，正式集成用例不注入测试 Action。
4. 先写独立 oracle，再接 function call。
5. 运行 Trigger Automation focused EditMode。
6. 如有 external evidence，完成 Unity MCP/工具验证并单独汇总。
7. 稳定 Scenario 保留在目录；删除时精确删除整个 Scenario 目录及 Unity meta。
