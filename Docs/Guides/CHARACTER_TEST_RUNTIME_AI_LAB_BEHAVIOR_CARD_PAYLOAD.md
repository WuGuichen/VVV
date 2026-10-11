# CharacterTest Runtime AI Lab 行为配方 v11

> Status: Current
>
> Authority: Current runtime authority
>
> Schema: `mx.character-test.runtime-ai-lab.behavior-card-logic.v11`
>
> Runtime contract: `8`
>
> Runtime AI Planner snapshot schema: `15`

## 权威模型

v11 继续使用统一 Timeline/Event 与显式对象作用域，并把目标离散布尔事实统一为 `TargetState` 位掩码条件：

```text
BehaviorRecipe
├─ EntryTimelineId
├─ ActivationConditionRoot
└─ Timelines[]
   ├─ TimelineId / Order / Title
   ├─ EntryConditionRoot
   └─ Events[]
      ├─ EventId / Title
      ├─ TimingKind / StartFrame / DurationFrames
      ├─ ConditionRoot
      ├─ Operation
      ├─ MaxExecutionsPerPass / CooldownFrames
      ├─ Priority
      └─ DurationReviewRequired
```

`TimingKind=Instant` 只在 StartFrame 采样一次，并规范化为 `DurationFrames=1 / MaxExecutionsPerPass=1 / CooldownFrames=0`。`TimingKind=Duration` 使用固定半开区间。旧 `DurationMode` 和 `MaxExecutionsPerEntry` 只用于兼容读取，原生 v11 JSON 不输出它们。Track、lane、滚动、缩放和 Inspector 状态不进入保存模型。

## 单帧与持续语义

- Instant：`clock == StartFrame` 时评估一次。兼容 Instant Move 启动后按自身生命周期继续运行，不会被下一帧自动截断。
- Duration：`StartFrame <= clock < StartFrame + DurationFrames` 时逐帧评估；角色控制操作到右边缘仍未结束时被取消。
- WaitForCondition 只能是 Instant；LoopRegion 只能是 Duration。
- Move、EnterTimeline 和 ReturnToPlanner 支持两种类别；CharacterAction 为 Duration，首帧起手特殊处理。
- 时间类别进入 semantic hash；Instant 与 `1f Duration` 是不同运行语义。

## 固定区间和 Timeline 末端

- Duration Event 使用半开区间 `[StartFrame, StartFrame + DurationFrames)`；Instant 的范围贡献固定为 `StartFrame + 1`。
- `StartFrame >= 0`，`DurationFrames >= 1`；Duration 即使为 `1f` 仍是区间。
- Timeline 长度由最右 Event 的右边缘自动计算，不保存独立长度。
- 没有 Event 的 Timeline 长度为 `0f`，进入后立即返回行为决策。
- 游标到达 Timeline 末端时，以 `TimelineCompleted` 返回 Runtime AI Planner。
- 若 Instant 启动的角色操作尚未完成，游标停在末端等待其稳定结果；完成后退出。snapshot 10 显式保存该等待状态。
- Duration 条件只决定是否开始操作；固定区间右边缘是执行硬上限。

## 条件树

行为启用条件、Timeline 进入条件和 Event 条件共用 `BehaviorRecipeConditionNode`：

- `All`：全部子节点成立；
- `Any`：任意子节点成立；
- `Not`：对唯一子节点取反；
- `FactPredicate`：强类型事实比较；
- `EventSignal`：读取同一 Timeline 中另一个 Event 的上一 RuntimeFrame 稳定结果。
- `SubjectScope`：绑定 `CurrentCombatTarget / Self / Environment`，恰好拥有一个条件子树。

All/Any 至少一个子节点，Not 与 SubjectScope 恰好一个子节点，最大逻辑深度 8，每棵树最多 64 个叶节点；SubjectScope 不计入逻辑深度且不能嵌套。`CurrentCombatTarget` 在目标阶段为 `NoTarget` 时整体不成立且不读取子属性。`TargetState` 可以在作用域外表达“无目标”；只选择有目标阶段时位于目标作用域。行为启用条件和 Timeline 进入条件只能使用事实条件；Event 条件可以引用同 Timeline EventSignal，EventSignal 不得放入 SubjectScope。

条件创作以对象优先：先选目标、自身或环境，再从状态、感知、几何、攻击威胁、导航、动作、能力和危险类别选择属性。作者界面中的“目标”固定指 Runtime AI Planner 的锁定目标，内部稳定标识仍为 `CurrentCombatTarget`。类别只用于查找，不进入 JSON。Lab 观察状态固定为 `Satisfied / Unsatisfied / SubjectUnavailable / NotObserved`；Review 必须读取 selected RuntimeFrame，不能回读 Latest。

目标目录公开 `TargetState`、目标根节点平面距离、目标 Hurtbox 表面距离和距离下一攻击窗口剩余帧。根节点平面距离是双方根节点的 XZ 距离；Hurtbox 表面距离是自身当前姿态胶囊到稳定锁定目标 Hurtbox 的三维表面净空，缺少该帧锁定 Hurtbox 证据时不可用。它只是几何净空，不代表具体 CharacterAction 一定能够命中；招式命中能力仍由 Attack Opportunity 的攻击形状、合法区域和姿态路径判定。`TargetStateMask` 使用稳定 `uint`：bit 0～4 分别为 `NoTarget / Alive / Dying / Dead / Reviving`，每帧恰有一个阶段位；bit 5～7 预留；bit 8 起依次为 `Perceivable / LineOfSightBlocked / InAttackRange / MovePathAvailable / AttackThreatToSelf / AttackInstanceResponded`。条件固定使用 `AllowedTargetPhases` Any、`RequiredTargetFlags` HasAll、`ForbiddenTargetFlags` HasNone，不开放普通数值操作符。

自身动作条件使用 `SelfActionInterruptible`：没有运行中的 CharacterAction 时为 true；有运行中动作时，只要当前 LocalFrame 落入该动作任一正式 CancelRule 的闭区间即为 true，否则为 false。此条件不预选下一动作，因此只判断“至少存在一个当前打断窗口”。旧 `SelfActionRunning=false/true` 分别确定性迁移为 `SelfActionInterruptible=true/false`，保留节点、操作符和树位置。旧 `SelfCanTransitionToAction`、`AbilityAvailable`、`SelfDodgeCooldownReady` 与 `EnvironmentNearHazard` 数值及运行读取仅作兼容保留，不再出现在新建条件目录；当前版本不声称按具体动作 ID 判断打断窗口，也不把仅检查定义与冷却的结果称为能力可用。

## Event 操作

普通操作包括：

- 接近或移动至目标；
- CharacterAction；
- 进入其他 Timeline；
- 返回行为决策。

时间控制操作包括：

- `WaitForCondition`（等待条件）：固定 `1f` Marker。条件不成立时局部游标停在该帧；条件成立后下一 RuntimeFrame 前进。空条件表示永久等待。
- `LoopRegion`（循环区间）：自身 Span 表达 `[A,B)`。游标准备越过 `B-1` 时评估退出条件；未满足且仍可重复时，下一个 RuntimeFrame 回到 `A`。空条件且重复上限为 `0` 表示无限循环。

时间控制只影响行为配方的局部游标，不冻结游戏 RuntimeFrame、事实、物理、动作或冷却。Timeline 跳转和返回决策优先于等待和循环。

约束：Loop 至少 `1f`；Loop 不能重叠或嵌套；同帧只能有一个等待点；等待点不能占用 Loop 的结束帧。普通 Event 可以跨越 Loop 边界：完全位于 Loop 内的 Event 随回绕进入下一 pass，跨越边界的 Event 保留 Timeline 级执行次数、结果和角色控制权，并在退出 Loop 后继续到自身右边缘。

## 重复、冷却与循环 pass

- `MaxExecutionsPerPass=1` 为默认值，`0` 表示不限次数。
- 没有循环时，一次 Timeline 进入就是一次 pass；Loop 回绕后，区间内 Event 进入下一次 pass。
- 回绕会重置 Loop 内 Event 的本 pass 次数和稳定结果，但不重置 `LastExecutionRuntimeFrame`，所以冷却始终以真实 RuntimeFrame 计时。
- 跨越 Loop 边界的 Event 不属于 Loop pass 重置集合；它在回绕期间保持已有执行状态，避免持续移动或动作被无意义地反复终止和重启。
- `CooldownFrames` 是两次执行开始之间的最小真实帧间隔；UI 固定显示单位“帧”。
- `LoopRegion.MaxLoopRepeats=N` 表示初次通过后最多额外回绕 N 次；`0` 表示无限。

## 独立局部游标与确定性顺序

### 执行表后端（2026-09-06）

当前源载荷仍为 v11。正式 `BehaviorCardLogicCompiler` 在私有副本上校验、归一、解析引用，返回 `CharacterTestBehaviorExecutionProgram`（执行格式 1）；不改写输入对象，不在运行时重建 Stage。源 schema、发布内容身份、执行格式和实例快照分别维护。必需动作缺失时不返回执行程序；仅做历史结构校验的 V6 reference 产物不能安装到正式 Overlay。

程序包含连续的时间轴、事件、类型参数、条件、谓词、子项索引及时间轴事件索引表。整数索引只在同一程序身份内有效，定义和角色实例状态分离；两个角色共享程序不会共享游标、次数、控制停步或动作实例。源 ID 保留在独立调试映射中。现有时间轴 Inspector 接收缓存的只读观察投影，投影的 Stage 字段为空，也不能进入执行器。

`CharacterTestBehaviorExecutionBuilder` 是高层项目制作前端的显式低频入口，支持最佳点接近、独立转向、根距离条件、目标朝向条件和指定绑定动作的正式 Attack Opportunity 条件。它不扩张既有 v11 JSON 可写操作集合；战术源的持久化与依赖有效性见 [战术行为卡指南](CHARACTER_TEST_TACTICAL_CARD.md)。普通 v11 输入按原策略迁移，不会自动变成最佳点接近。

移动和转向各取 Priority 降序、源 EventId 字典序中的第一个有效提供者；动作仍取最后一个满足起手条件的事件。已激活的持续控制不会重复消耗次数或重新套用启动守卫；有效零输入继续占有通道。目标不可用时释放输出但保留可恢复的激活状态，若其他事件接管则取消旧操作。各事件完成采样后才提交本帧最终控制和最多一个动作编排命令。

控制命令 schema 10 携带独立朝向、倍率和最佳点基础移动的固定步长上限。正式 Motion 继续处理动作倍率、Root Motion、重力、碰撞与其他物理，不使用 TurnInPlace 冒充并行转向。释放校验控制 owner token；动作终止校验 expected instance，旧清理不能覆盖新提供者。编排应用的正式回执保存实例身份，即使短动作在下一次采样前结束也能确认起播。

Planner snapshot 16 保存新执行状态及回执／待释放状态，CharacterTest authority snapshot 36 保存控制输入扩展。旧活动布局不静默升级为新布局，需要重新开始会话；旧历史 Hash 也不能当新执行器的回放通过证据。恢复必须匹配执行格式、程序／依赖身份、实体和状态尺寸，失败不得清空正在运行的 Overlay。活动绑定禁止被新编译结果覆盖；可编辑草稿和准备未绑定产物，实际应用需等待该绑定停止。

入口、专项证据、旧模型对照与当前限制见[任务 02 执行记录](../../StorySource/玩法基石/任务/任务02执行记录.md)。

v7 起显式保存 `LocalPlayheadFrame`；snapshot 15 追加 `LastSampledPlayheadFrame`，用于判断是否实际重新进入动作首帧。每个 RuntimeFrame：

1. 刷新事实、实际动作状态及已有编排回执；动作交接回执完成后可继续本次采样。
2. 按 Priority 降序、EventId 字典序采样当前帧事件，后续条件读取当前已确认状态。待播放动作不冒充实际动作状态。
3. 动作事件只在实际进入首帧时判定；连续停留不重试，离开后重入重新判定，跳入中段不补播。一帧循环不产生位置变化，因此不重播。
4. 收集最后一个满足起手条件的动作，其他同帧事件继续采样；若退卡，丢弃候选并终止当前动作。
5. 提交最终动作的编排替换指令；没有新动作且旧动作已离开事件区间，则结束旧动作。B 的启动不需要先等待 A 的结束清理。
6. Gate 只阻止局部游标推进，不截断同帧采样；放行后处理 Loop 或推进游标。进入下一位置时检查其事件范围，末尾自然结束。

每帧最多提交一个排他编排命令。普通角色控制冲突保持原有优先级策略；动作事件以采样顺序最后一个满足条件者为最终播放结果。当前指令由同一世界模拟帧后续的命令阶段消费，事件采样时尚未发生的动作／位移效果不能提前读取。

动作自然完成不重播；离开动作事件区间或退卡会终止该卡仍拥有的动作。编排替换不经过取消窗口仲裁；缺失实体、目录或动作定义仍须显式报错，不能报告虚假成功。

## 快照和回放

snapshot schema 16 的新执行状态保存上次采样位置、当前 Timeline、独立局部游标、pass generation、Loop 重复次数、等待点、待确认流程操作、Event 次数/结果/冷却、移动／转向所有者、停步状态和动作实例／回执。snapshot 15 是旧执行器的历史布局，不作为新活动状态恢复。

runtime contract 3 / snapshot schema 8 的活动状态与回放不能静默恢复为 v7。保存运行时必须从测试第 0 帧重模拟到目标 RuntimeFrame；缺少完整输入证据时明确拒绝。

## v9 → v10 迁移

加载顺序为：先按 v9 schema、runtime contract 6 和旧 semantic/content hash 认证，再确定性包装 SubjectScope 并计算 v10 hash。目标依赖 Fact 进入 CurrentCombatTarget，自身与环境 Fact 进入各自作用域，`TargetExists` 不包装。仅加载不写盘，下一次保存或发布才输出 v10。

- WaitForCondition → Instant；LoopRegion → Duration。
- Move、CharacterAction → Duration，包括旧 `1f` 内容，以保留旧右边缘硬终止语义。
- EnterTimeline、ReturnToPlanner、Unconfigured：旧时长为 `1f` 时迁移为 Instant，否则为 Duration。
- 保留 Event/Timeline 身份、条件、Operation、开始帧、时长、优先级和资源引用；不新增或删除 Event。
- v4～v8 继续按各自旧哈希认证后逐级迁移到 v9，再进入 v10。

## v10 → v11 迁移

v9/v10 Published 内容先按原 schema、runtime contract 和 semantic/content hash 认证，再在内存中确定性迁移到 v11；单纯加载不改写项目文件。旧目标布尔叶节点一对一变为 `TargetState`，保留 NodeId、ConditionId、All/Any/Not 和树顺序：目标存在映射为 `NoTarget` 或全部有目标阶段，目标已死亡映射为 `Dying / Dead / Reviving` 或 `Alive`，其余六项映射为 Required 或 Forbidden 状态位。三个掩码全部进入 v11 semantic hash。

旧 Runtime AI Lab 内容中的距离判断统一迁移为 `TargetDistance`，即锁定 Hurtbox 表面净空；旧 `TargetRootDistance` 叶节点迁移时保留 NodeId、ConditionId、比较操作符和阈值，仅替换 Fact。原生 v11 仍允许作者显式新建“目标根节点平面距离”，但不会把旧距离条件继续解释为根距离。

`TargetState` 默认只允许 `Alive`。阶段掩码必须非零且只能包含阶段位；Required/Forbidden 只能包含状态位并不得重叠。包含 `NoTarget` 的叶节点必须位于目标作用域外；编辑器可沿 All/Any 确定性移出并保留顺序，跨越 Not 时拒绝提交。contract 7 的活动程序、Replay 或 F+1 证据不能静默恢复到 contract 8，必须从第 0 帧重新模拟。

`OperationKind=Ability` 的数值只为旧 JSON 保留。当前行为配方不允许创建、保存、运行或发布 Ability Event；旧内容会收到明确诊断，作者需要改为该 Ability 绑定的正式 CharacterAction Event。Ability 系统本身与 `AbilityAvailable` 条件事实不受影响。

`OperationKind=Hold` 同样只为旧 JSON 保留。当前行为配方不允许创建、保存、运行或发布固定空等待 Event；需要等待事实时使用 `WaitForCondition`。

原生 v11 保存不输出 `DurationMode`。SubjectScope kind、subject、子树和 TargetState 三个掩码进入 semantic hash；标题、lane、缩放、滚动和 Inspector 状态不参与 semantic hash。

当前编辑设计见 [行为配方 v11 大时间轴设计](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_TIMELINE_DESIGN.md)，对象优先编辑的历史实施记录见 [v10 对象优先条件实施台账](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_CONDITION_V10_IMPLEMENTATION_LEDGER.md)。
## v11 角色动作事件规范

角色动作事件仍保存为 `OperationKind=CharacterAction`，但必须满足：`TimingKind=Duration`、可选起手条件、`MaxExecutionsPerPass=1`、`CooldownFrames=0`，且 `DurationFrames` 等于动作目录解析出的权威帧长。`DurationFrames` 是规范化缓存，不是作者输入；动作依赖身份或权威帧长变化必须使编译 authority 变化。普通条件 Event 不得通过 Operation mutation 转换成角色动作事件。

2026-09-06：snapshot 15 的采样位置字段进入克隆、恢复与 Hash；旧活动快照按版本拒绝，需重新开始会话，不推测首帧是否已经采样。
