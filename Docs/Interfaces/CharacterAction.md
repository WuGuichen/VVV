# Character Action 接口

> Status: Current

> 文档版本：2026-10-09

> 状态：CharacterAction 单一执行权威已完成；CharacterTest、CharacterControl 和两个 Runtime 示例均通过有状态 Gateway 执行，旧 Runner / 通用 Cancel / 双重 CombatAction 权威已移除
> Status: Current
> 模块：`MxFramework.CharacterAction` — noEngine（`noEngineReferences=true`）
> 依赖：`MxFramework.Combat`、`MxFramework.Gameplay`、`MxFramework.Runtime`

## 武器 requirement 扩展

`CharacterActionRequirementKind` 追加 `WeaponTypeRequired=11` 与 `WeaponTypeIndependent=12`。前者使用稳定类型 ID 和正数装备槽，后者不带引用；形状、互斥、未核对 BasicAttack 策略和真实装备准入统一见 [武器类型与动作准入](../CHARACTER_WEAPON_CONFIGURATION.md#34-实体武器类型与动作准入)。浏览分类只提供查找线索；实际执行仍通过正式动作和卡片 Authority。该扩展当前为隔离候选，原生导入/Player 验收待稳定窗口放行。

## 职责

`MxFramework.CharacterAction` 是角色动作的解析 → 计划 → 执行 → 轨道分发管线。它把玩家输入 / Runtime AI Planner 决策 / 受击事件统一解析成动作计划，再编排到 CharacterControl、Combat、Gameplay、MxAnimation、Audio、VFX、Camera。

### 动作身份不变量

- `CharacterActionId` 是 CharacterAction 的唯一身份，内部包装全局唯一正整数；`None/default` 的值为 `0`，只表示未配置或无动作，注册、执行和持久化的正式动作必须大于 `0`。
- JSON、网络、快照与 Replay 均把 `CharacterActionId` 序列化为普通整数。动作定义只写 `"actionId": 301`，不再存在动作自身的字符串 `StableId`、字符串别名索引或 `TryGetAction(string)`。
- `DisplayName` 只负责展示；输入选择由 ActionSet 的 intent/binding/sequence 决定，类型语义由 `CharacterActionCategory` 决定，附加能力由 Tags、Requirements 与轨道配置决定。不得从 `light_attack` 等名字推断身份或语义。
- `CombatActionId`、Animation Action Key、Resource Key、Audio Cue、轨道事件 `StableEventId`、ActionSet/Profile/Reaction/行为卡 Stable ID 仍属于各自领域，不受本次动作身份改造影响。
- ID 分配权威是 `CharacterActionSource/action_id_catalog.json`。Unity Editor、CharacterStudio/Authoring CLI 和角色生成流程必须通过该目录的 revision/hash 事务分配；复制领取新 ID，删除标记 retired，已分配 ID 永不回收。

`MxFramework.CharacterControl` 通过 Request Coordinator 解析请求并调用 `CharacterActionExecutionGateway`，再由 Dispatch Coordinator 把 Motion、action-authored root motion、Combat、Gameplay、Animation、Presentation 和 Debug track 派发给组合根 sink。`CharacterActionCombatTrackBridge` 只把旧 CombatAction 作为外层 CharacterAction 的子时间线；`CharacterActionGameplayTrackBridge` 只提交 Gameplay command，二者都不拥有外层动作状态。Motion track 和 root motion samples 可使用 `CharacterActionMotionRequestAccumulatorSink` 落到 `CharacterMotionRequestAccumulator`。`MxFramework.Game.CharacterTest` 和两个 Runtime 示例均使用同一正式执行路径。

## 公开接口

### Tactical timing queries

Operation and measurement lifecycle: [CharacterAction tactical metrics guide](../Guides/CHARACTER_ACTION_TACTICAL_METRICS.md).

`CharacterActionTacticalMetrics` binds action ID/content fingerprint, timing and an optional `CharacterActionSpatialSummary`.
A missing spatial summary stays null. Spatial evidence is a nominal reference-capsule measurement for root-distance approach planning and coarse filtering;
it never replaces live Attack Opportunity or Gateway authority.

`CharacterActionReferenceDummy` freezes a positive configuration version and capsule height/radius/center in millimeters.
`CharacterActionSpatialMeasurement` freezes the prober version, positive integer-millimeter step and nonnegative search range with max > min;
meter values are projections. The max endpoint is included even if the final step is shorter.
`CharacterActionSpatialSummary` copies its ordered discrete hit intervals into a read-only collection, retains all measurement inputs,
action/dummy fingerprints and optional formally verified optimal point. `IsContinuousAtResolution` means exactly one successful sampled run, never mathematical continuity.
`MinDistance` / `MaxDistance` are null when no hits or when that boundary is open. Positive search-min hits open the lower boundary; hits at search-max open the upper boundary.
OpenBounded takes status precedence over Discontinuous while the independent continuity flag and intervals retain gaps.
NoHit, Unavailable and Unsupported cannot carry hit distances. `CharacterActionTacticalSector` records Authored, Measured or Fallback;
fallback is explicitly forward +/-30 degrees. Constructors are LowFreqAlloc except the scalar interval/sector value constructors (NoAlloc).

`CharacterActionTacticalUtility.ExtractTiming(config)` returns `CharacterActionTacticalTiming`:
`StartupFrames` / `ActiveFrames` count the union of inclusive authored Phase ranges (long, no overflow at int frame endpoints),
`StartupEndFrame` is the last Startup endpoint or -1 when absent, and nullable `TotalDurationFrames` preserves the authored duration.
Missing phases have zero length. Unsorted and overlapping ranges are supported without double counting.

`TryGetEarliestTransitionFrame(config, requestType, slotMask, out frame)` reuses `CharacterCancelRule.Matches` and returns the earliest matching local window start.
Rule-side zero masks are wildcards; query-side zero does not search nonzero rules. Multi-bit queries mean an existing match, not permission for every bit.
No matching rule returns false / -1. These NoAlloc queries are planning facts; ordinary Gateway requests check current-frame eligibility separately.
Timeline orchestration replacement has its own authority and does not require this query as a cancellation-window gate; the query is not a universal card-exit permission.

### 配置定义

| 类型 | 用途 |
| --- | --- |
| `CharacterActionCategory` | 动作分类枚举：None、Idle、Movement、BasicAttack(10)、Skill(11)、Guard(12)、Dodge(13)、Jump(14)、Interaction(15)、Reaction(20)、PassiveOverlay(30) |
| `CharacterMovementMode` | 移动模式枚举：Idle、Walk、Run、Strafe、TurnInPlace、ApproachTarget、Retreat、CircleTarget、RootMotionDriven、Airborne、ControlLocked |
| `CharacterActionRequirementKind` | 动作准入条件枚举：Grounded、Airborne、EquipmentTagRequired、StatusRequired/Forbidden、ResourceAvailable、CooldownReady、TargetValid、PhaseAllowed |
| `CharacterActionSetConfig` | 动作映射集合构造函数在末尾包含 `defaultActionSlotMask=0, landingProfileId=""`；`characterStableId` / `equipmentStateStableId` 是可选筛选上下文，不表示 ActionSet 被角色或装备拥有。`landingProfileId` 为空保持旧落地行为；slot request 路径在无 active action 时使用默认 mask 判定可启动槽位。 |
| `CharacterLandingProfile` | 可选落地动作配置：`StableId`、`LightLandingActionId`、`HeavyLandingActionId`、`LightLandingMinDownwardSpeedMillimetersPerSecond`、`HeavyLandingMinDownwardSpeedMillimetersPerSecond`。低于轻落地阈值时不请求落地动作；两个 ActionId 都可为空；目标等级为空时才双向 fallback，已配置动作被切换窗口拒绝时不 fallback。默认轻/重落地阈值分别为 `1000 / 8000 mm/s`。 |
| `CharacterActionId` | noEngine 强类型正整数身份；`Value`、`None`、`IsValid`、相等、排序、稳定 hash、`TryCreate/TryParse` 与整数 JSON converter。正式入口拒绝 `0`，构造拒绝负数。 |
| `CharacterActionConfig` | 单个动作配置以 `CharacterActionId actionId` 开头；其余字段是显示名、Category、时间轴权威、Tags、优先级、时长、Requirements、Phases、切换/打断规则与各轨道。没有字符串 StableId 或第二套动作身份。 |
| `CharacterMovementProfileConfig` | 移动配置：包含 `gravity` 与可选 `upwardGravityReference`（默认 90 m/s²）；构建时计算并缓存 `UpwardMotionGravityScaleMilli`，供所有正向 Y 根运动消费者直接读取。 |
| `CharacterActionRequirement` | 准入条件：`(CharacterActionRequirementKind kind, string stableId="", int value=0)` |
| `CharacterActionBinding` | 命令绑定：`(string intentId, CharacterActionId actionId, int priority=0, bool allowQueue=false, int queueWindowFrames=0)` |
| `CharacterActionCommandSequence` | 结构化命令连段：一个 intent 对应有序 `actionIds`、`wrapAfterLast`、优先级及排队策略；Resolver 根据当前活动动作选择下一段。同一 intent 不得同时存在于普通 binding 与 sequence。 |
| `CharacterAbilityActionBinding` | Ability 绑定：`(int abilityId, CharacterActionId actionId, string[] requiredTags=null, string[] forbiddenTags=null)` |
| `CharacterReactionBinding` | 受击绑定：`(string reactionProfileId, CharacterActionId defaultActionId, string[] requiredTags=null, string[] forbiddenTags=null)` |

Character Package 的 `actionConfiguration.commandBindings[]` 是可选 Authoring 投影：每项可用 `intentId + actionRole`（或已解析的正整数 `actionId`）生成现有 `CharacterActionBinding`。它与 Ability 生成的同名 command intent 冲突时，以角色包显式 command binding 为准；与 `commandSequences` 同名仍属于非法双重权威。

### Phase / Timeline Authority

| 类型 | 用途 |
| --- | --- |
| `CharacterActionPhaseKind` | Phase 类型枚举：None、Startup、Active、Recovery、Loop、Airborne、Landing、Channel、Hold、Exit、Custom(100) |
| `CharacterActionTimelineAuthority` | 时间轴权威枚举：CharacterAuthored(0)、CombatAnchored(1) |
| `CharacterActionPhase` | Phase 区间 struct：`(CharacterActionPhaseKind kind, int startFrame, int endFrame, CombatActionPhase combatPhaseAnchor=CombatActionPhase.None, bool requiresCombatPhaseMatch=true)` — 包含 `Contains(int localFrame)` |

### 切换窗口 / 打断

`CharacterCancelRule` 是当前 runtime / JSON 的兼容命名；在设计和编辑器语义中，它表示动作 **Transition Window / 切换窗口**。Running action 默认阻挡新的动作启动；只有当前本地帧落在切换窗口内，并且最终解析动作的请求类型与请求 Slot 同时满足窗口掩码时，Gateway 才允许从当前动作原子切入目标动作。切换窗口不读取动作请求的触发渠道，也不配置目标动作或允许/禁止开关；窗口存在即表示允许，未匹配任何窗口即拒绝。切换窗口必须被当前 validation 认定的可切换 phase 覆盖；允许跨连续的 Active、Recovery、Loop、Landing、Channel、Hold、Exit，但不能覆盖 Startup、Airborne、Custom、None、phase gap 或 duration 之外的帧。`Landing` 只是允许承载作者显式配置的窗口；没有匹配的 `cancelRules` 时，落地动作仍会阻挡普通动作切换。

`actionSlotId` is the transition-decision slot bit, not `intentId`. New slot requests carry `actionSlotId`, `requestedActionId`, `sourceKind`, `priority`, `frame`, and `traceId`，但 Cancel Rule 只消费最终 `CharacterActionPlan.Category` 与 `actionSlotId`。`RequestTypeMask == 0` 表示任意请求类型，`ActionSlotMask == 0` 表示任意 Slot；非零时分别执行位与判断，两个维度采用 AND。`StartIntent(..., actionSlotId)` 可携带触发该 intent 的 Slot；未携带 Slot 时只能匹配 `ActionSlotMask == 0` 的窗口。Empty resolved slots reject with `ActionSlotEmpty` and do not cancel the current action.

切换窗口可用于普通攻击接续、闪避取消、技能切入、防御切入和落地动作提前退出。结构化命令连段的“下一动作”由 `CharacterActionResolver` 根据当前活动动作和 `CharacterActionCommandSequence` 选择；Runner/Gateway 仍只负责切换窗口与排队规则。Ability 冷却和资源消耗仍属于对应上层系统。

| 类型 | 用途 |
| --- | --- |
| `CharacterActionSourceKind` | 动作请求溯源枚举包含 Command、GameplayAbility、LocalInput、RuntimeAiPlanner、Replay、Scripted、CohesionBreak、GuardBreak、ArmorBreak、Hit、Death、Reaction、`Landing(16)`、PlayerIntervention、Debug；继续用于请求诊断、Reaction、Interrupt 与生命周期处理，不参与 Cancel Rule。 |
| `CharacterActionRequestType` | `[Flags]` 最终动作请求类型掩码，与 `CharacterActionCategory` 固定映射；`Any=0`。 |
| `CharacterActionSlotType` | `[Flags]` 输入缓存打断掩码（Input Buffer Cancel Interrupt Mask）：AttackPrimary、AttackSecondary、Dodge、Jump、Skill、Guard、Interact、LightLanding、HeavyLanding；`Any=0`。仅用于即时动作打断与取消规则（`CharacterCancelRule`），与战术行为卡中的“动作槽”无任何从属关系。 |
| `CharacterCancelRule` | 切换窗口 runtime 规则 struct（兼容名仍为 CancelRule）：`(int startFrame, int endFrame, CharacterActionRequestType requestTypeMask=Any, CharacterActionSlotType actionSlotMask=Any)`；包含 NoAlloc `Matches(localFrame, requestCategory, actionSlotId)`，两个非零掩码必须同时命中。 |
| `CharacterInterruptRule` | 打断规则 struct：`(CharacterActionSourceKind sourceKind, int minimumPriority=0, CharacterActionId targetActionId=default, bool allow=true)` — 包含强类型 `Matches(sourceKind, priority, targetActionId)`。 |
| `CharacterCancelConflictClassifier` | 切换窗口冲突分类器：`Classify(authority, characterRules, combatTimeline, localFrame, targetActionId, requestCategory, int actionSlotId=0) → CharacterCancelConflictResult`；`targetActionId` 仅继续供 Combat Cancel Window 使用。 |
| `CharacterCancelRejectionAuthority` | 拒绝来源枚举：None、Character(1)、Combat(2) |
| `CharacterCancelConflictResult` | 切换冲突结果 struct：`Allowed(bool)、RejectedBy(CharacterCancelRejectionAuthority)、Code(string)` — 工厂方法 `Accepted()` / `Rejected(authority, code)` |

### 意图 / 解析 / 计划

| 类型 | 用途 |
| --- | --- |
| `CharacterActionIntentRequest` | 意图请求携带 entity、intent、可选 ability、强类型 `RequestedActionId`、source、priority、frame、trace 与可选 action slot。 |
| `CharacterActionResolveStatus` | 解析状态枚举：Success、Queued、Rejected |
| `CharacterActionRejectReason` | 拒绝原因枚举：30+ 原因（MissingActionSet、StateDisabled、CooldownActive 等） |
| `CharacterActionResolveResult` | 解析结果 sealed class：`Status(ResolveStatus)`、`RejectReason(RejectReason)`、`Plan(CharacterActionPlan)`、`Diagnostics`、`TraceId`、`IsSuccess`、`IsQueued`、`IsRejected` — 工厂方法 `Success(plan)` / `Queued(plan)` / `Rejected(reason)` |
| `CharacterActionCandidate` | 候选动作 struct：10 字段（ActionId、四层优先级、SourceOrder、StableTieBreaker、AllowQueue、QueueWindowFrames） |
| `CharacterActionPlan` | 动作计划 sealed class：`ActionId` 为 `CharacterActionId`；同时携带 plan id、Category、priority、duration、phases、tracks 与 trace id。 |
| `CharacterActionTrackPlan` | 轨道计划 struct：`(CharacterActionTrackKind kind, string configReferenceId, int eventCount)` — 静态方法 `FromConfig(CharacterActionConfig)` |
| `CharacterActionDurationPolicy` | 兜底时长策略 struct：`(int fallbackDurationFrames)` — `HasFallbackDuration(bool)`、`FallbackDurationFrames(int)` |
| `CharacterActionPlanDurationSource` | 时长来源枚举：None、CharacterActionConfig、CombatActionTimeline、FallbackPolicy |
| `CharacterActionPlanDurationResult` | 时长解析结果 struct：`Resolved(bool)`、`DurationFrames(int)`、`Source(PlanDurationSource)`、`Diagnostics` |
| `CharacterActionPlanDurationResolver` | 时长解析器 static class：`Resolve(CharacterActionConfig, CombatActionTimeline=null, CharacterActionDurationPolicy=default) → DurationResult` |

### Resolver

| 类型 | 用途 |
| --- | --- |
| `CharacterActionResolverContext` | 解析器上下文 sealed class：`(CharacterActionSetConfig actionSet, CharacterActionConfig[] actions, CharacterReactionProfile[] reactionProfiles=null, CombatActionTimeline[] combatTimelines=null, CharacterActionResolverState state=default, CharacterActionDurationPolicy durationPolicy=default, CharacterActionCombatTimelineBinding[] combatTimelineBindings=null, string[] contextTags=null)` |
| `CharacterActionCombatTimelineBinding` | Combat 时间线绑定 struct：`(string combatActionId, CombatActionTimeline timeline)` |
| `CharacterActionResolverState` | 当前状态 struct：`(bool isDisabled, bool isDead, bool hasActiveAction, bool activeActionBlocksImmediateStart, ...)` |
| `CharacterActionResolver` | 解析器 sealed class：`ResolveCommand(context, intentRequest) → ResolveResult`、`ResolveActionSlot(context, slotRequest) → ResolveResult`、`ResolveAbility(context, request) → ResolveResult`、`ResolveReaction(context, reactionContext) → ResolveResult` |

### Authoring 与编译投影

- 公共动作正文权威是 `CharacterActionSource/Libraries/Common/character_actions.json`，角色专属正文权威是 `<角色包>/config/character_actions.json`；两者只接受 Action Library schema `0.3.0` 和整数 `actionId`。
- `Assets/MxFrameworkGenerated/CharacterActions` 只保存编译结果。Profile `<package>.default` 与 ActionSet `<package>.<weaponCombination>` 由 Compiler 确定性生成，Profile/ActionSet/Reaction/Landing 投影只接受 schema `0.4.0`。
- `actionConfiguration.actions[]` 只写 `{ actionRole, actionId }`。Compiler 合并当前角色库与 Common，校验唯一 ID、引用权限、Humanoid 兼容性及连段完整性，再将 role sequence 解析为 runtime action ID sequence。
- Action Library 的可选根字段 `animationSources[]` 以 `resourceKeyPrefix` 声明实际动画资源的 `rigType`、`skeletonProfileId`、`avatarProfileId` 和 `allowRetargeting`；最长前缀匹配决定每个动作引用的源骨架。未声明或冲突的动画资源不得编译为可选槽，跨骨架仅在源/目标均为 Humanoid 且源明确允许重定向时成立。
- CharacterStudio 只选择动作槽；Unity Character Action Authoring 只编辑显示名称、分类、标签、要求与动作正文轨道。动作 ID 只读；Common 在角色上下文只读，需要修改时通过目录事务复制并领取新的角色专属数字 ID。
- Unity 精确打开请求携带 Profile、ActionSet、Library、Action ID、读写模式与源 hash；源文件变化后拒绝覆盖，并将打开/保存/冲突/失败/超时状态回传 CharacterStudio。

### Execution Gateway（单一执行网关）

000010 的法杖火球／火焰枪作用域绑定、消费适配与尚未执行的原生验收见 [角色技能适配](../Guides/CHARACTER_000010_STAFF_ABILITIES.md)。

第一方运行代码必须通过有状态的 `CharacterActionExecutionGateway` 启动、切换、打断、完成、恢复或终止 CharacterAction。Gateway 独占 internal Runner；Runner 原始操作和 operation result 不再是公共 API。网关不重复解析动作，目标仍由 `CharacterActionResolver` 和 `CharacterActionRuntimePlanCache` 生成。

| 类型 | 用途 |
| --- | --- |
| `CharacterActionExecutionRequestKind` | 执行策略枚举。普通请求为 `StartSlot / StartIntent / React / CancelOnly / CompleteExternal`；强制边界分为生命周期 `ReplaceForLifecycle / TerminateForLifecycle` 与玩法编排 `ReplaceForOrchestration / TerminateForOrchestration`。 |
| `CharacterActionExecutionRequest` | NoAlloc 请求值；提供兼容的 `StartIntent(...)` 与带 Slot 的 `StartIntent(..., actionSlotId)` 重载，以及 `StartSlot(...)`、`React(...)`、`CancelOnly(requestType, sourceKind, actionSlotId=0)`、`CompleteExternal(expectedActionId, reason)`、生命周期 `ReplaceForLifecycle / TerminateForLifecycle` 和玩法编排 `ReplaceForOrchestration / TerminateForOrchestration` 工厂。编排请求固定标记 `CharacterActionSourceKind.RuntimeAiPlanner`，不能由普通输入或 Reaction 冒用；`CancelOnly` 仍经过当前切换窗口且不解析或启动目标 Action。 |
| `CharacterActionLifecycleReason` | 强制生命周期边界：`Death / ReviveReset / EntityDespawn / ConfigurationReload / RuntimeShutdown`。 |
| `CharacterActionOrchestrationReason` | 高层玩法编排边界：`BehaviorRecipeTimelineJump / BehaviorRecipeReturnToPlanner`。它与生命周期原因是两套权限，行为配方不得借用 `ConfigurationReload` 等原因伪装跳转。 |
| `CharacterActionExternalCompletionReason` | 外部正常完成事实；首批仅支持 `Landed`。 |
| `CharacterActionExecutionStatus` | 结构化结果：`Rejected / Started / Transitioned / Interrupted / LifecycleReplaced / Completed / Terminated / NoOp / Cancelled / OrchestrationReplaced / OrchestrationTerminated`。调用方必须按明确状态确认交接，不能仅凭当前 ActionId 推测成功。 |
| `CharacterActionExecutionResult` | NoAlloc 结果值；携带活动实例、调用方事件缓冲和 Runner 复用诊断。诊断只保证在下一次 buffered Runner 操作前有效。 |
| `CharacterActionExecutionState` | LowFreqAlloc 捕获/恢复值，保存 next instance id、活动实例、计划、动作、本地帧、phase 与诊断状态，不含事件数组。 |
| `CharacterActionExecutionGateway` | 有状态唯一执行对象：`Execute(in request, eventBuffer)`、`Tick(eventBuffer)`、`CaptureState()`、`TryRestore(...)`、`CreateDebugSnapshot()`；初始化并预热缓存/缓冲后，稳定成功和常规拒绝路径均为 NoAlloc。 |

固定策略如下：无活动动作时启动解析目标；普通 slot/intent 在有活动动作时检查切换窗口；普通反应检查打断规则，空 `interruptRules` 表示不可被普通反应打断。`CancelOnly` 使用显式 `CharacterActionRequestType` 和可选 Slot 检查 Character-authored 切换窗口，命中后只产生 `ActionCancelled`，不启动替代 Action；Combat-anchored 时间轴因为缺少具体目标动作而拒绝。CharacterTest 使用 `CancelOnly(Movement, Command, 0)` 让超过 locomotion 激活阈值的确定性移动输入提前退出允许 Movement 的动作并在同帧交还 Motion System。`CompleteExternal` 必须匹配预期活动动作。

强制边界必须先完成目标计划和定义校验，再改变活动实例。`ReplaceForLifecycle` 服务死亡、反序列化重置、实体销毁、配置重载和运行时关闭；`ReplaceForOrchestration` 只服务已获得高层行为编排权限的 Timeline 跳转。二者都不检查普通切换窗口或打断窗口，并在同一事件缓冲中原子结束旧实例、启动新实例；目标无效时旧实例完全不变。`TerminateForOrchestration(BehaviorRecipeReturnToPlanner)` 只释放当前行为配方拥有的动作，不启动替代动作。生命周期权限和玩法编排权限不能互相替代，也不能由普通玩家输入、普通 AI 动作请求或 Reaction 调用链获得。

正式来源映射为：玩家输入 `LocalInput`、Runtime AI Planner `RuntimeAiPlanner`、剧情 `Scripted`、调试入口
`Debug`；反应来源使用 `Hit / CohesionBreak / GuardBreak / ArmorBreak / Death / Reaction`，优先级使用目标
动作计划优先级，不使用无界优先级覆盖。

### 执行状态与内部 Runner

| 类型 | 用途 |
| --- | --- |
| `CharacterActionInstanceState` | 执行实例状态枚举：None、Running、Finished、Cancelled、Interrupted |
| `CharacterActionRunnerEventKind` | 运行器事件枚举新增 `TrackLifecycle`。一次性事件使用 `TrackEventFired`，有状态区间使用 `TrackLifecycle`。 |
| `CharacterActionTrackLifecyclePhase` / `CharacterActionTrackLifecycleBoundary` | 区间阶段为 `Enter / Active / Exit`；边界为 `BeforeFrame / AfterFrame`。自然结束在结束帧消费完成后的 `AfterFrame` 退出，强制退出在后续消费者前的 `BeforeFrame` 退出。 |
| `CharacterActionTrackExitReason` | 区间退出原因：NaturalEnd、ActionFinished、ActionCancelled、ActionInterrupted、ActionReplaced、Death、ReviveReset、EntityDespawn、ConfigurationReload、RuntimeShutdown、PreviewSeek、ReplayRebuild。 |
| `CharacterActionTrackLifecycleEvent` | 区间生命周期值，携带 ActionInstanceId、ActionId、TrackKind、EventKind、StableEventId、StartFrame、EndFrame、LocalFrame、Phase、Boundary、ExitReason 与原始 dispatch。区间身份固定为 `(ActionInstanceId, TrackKind, StableEventId)`。 |
| `CharacterActionTrackDispatchEvent` | 轨道派发事件 struct：包含 TrackKind、EventKind、Frame、EndFrame、StableEventId 及各轨道专有 payload。 |
| `CharacterActionRunnerEvent` | 运行器事件 struct：同时携带一次性 `TrackDispatch` 或区间 `TrackLifecycle`，并包含 `ToReplayLine()`。 |
| `CharacterActionTransitionRequest` | 过渡请求 struct：`(ResolveResult resolveResult, RunnerActionDefinition definition, SourceKind sourceKind, int priority=0, string traceId="")` |
| `CharacterActionRunnerActionDefinition` | 动作定义 sealed class：`actionId` 使用 `CharacterActionId`；同时携带 timeline authority、切换/打断规则、Combat 子时间线、轨道事件与 root-motion 配置。 |
| `CharacterActionInstance` | 运行中实例 sealed class：`InstanceId`、`Plan`、`Definition`、`State`、`LocalFrame`、`CurrentPhaseKind`、`IsRunning`、`FinishReason` |
| `CharacterActionDebugSnapshot` | 诊断快照 sealed class：`ActiveActionInstanceId`、`PlanId`、`ActionId`、`State`、`LocalFrame`、`CurrentPhase`、`DurationFrames`、`FinishReason`、`LastRejectReason`、`FiredEventsThisFrame` |
| `CharacterActionRunner` | internal 状态机，由 Gateway 独占；第一方组合根无法直接启动、切换、打断或清理。 |

Runner allocation notes:

- `CharacterActionRuntimePlanCache` caches action validation, duration, track plan, runner definition, and per `actionId + traceId` resolve results. It allocates during construction and first-seen entries; repeated cached lookups are NoAlloc.
- `CharacterActionRunnerEventBuffer` 是 Gateway / Reaction 调用方拥有的事件缓冲；初始化和扩容为 LowFreqAlloc，容量稳定后的执行与常规拒绝路径为 NoAlloc。
- Runner 数组返回 API 和 `DrainEvents()` 已从公共面删除；`ToReplayLine()` 仅用于显式诊断/导出，属于 AllocByDesign。

### Reaction Runtime / CharacterControl Bridge

| Type | Contract |
| --- | --- |
| `CharacterActionReactionStartResult` | 单一路径结果：解析结果、Gateway 执行结果、是否启动/使用 fallback 及稳定诊断。 |
| `CharacterActionReactionStartBuffer` | 调用方拥有的事件缓冲；构造与首次扩容分配，预热后重复反应请求 NoAlloc。 |
| `CharacterActionReactionRuntime` | noEngine helper。使用 `CharacterReactionSelector.SelectNoAlloc`、`CharacterActionRuntimePlanCache`、Gateway 和调用方缓冲；普通反应遵守 `interruptRules`，Death 使用原子生命周期替换。 |
| `CharacterActionController.SubmitReaction` | CharacterControl entry point for action-level reactions. It submits `CharacterReactionContext` through the generic helper, sets CharacterControl state to `Reaction` for Reaction-category actions, and applies the default `Move | Jump | Action` lock mask unless `CharacterActionControllerOptions.ReactionLockMask` overrides it. |
| `CharacterActionReactionBridge` | CharacterControl bridge that converts `CohesionBreakEvent`, `GuardBreakEvent`, `ArmorBreakEvent`, `PressureBandChangedEvent`, explicit death, and explicit hit reaction metadata into `CharacterReactionContext` and calls `SubmitReaction`. Ordinary damage does not auto-trigger action interruption; projects must explicitly call `ApplyHit(...)` or `SubmitReaction(...)`. |

`MxFramework.Game.CharacterTest` 使用唯一的 `CharacterActionReactionRuntime.TryStart(...)` 路径处理 hit、launch、death 和 cohesion-break 反应。CohesionBreak 被规则拒绝时只保留压力/战斗事实并记录 `InterruptRejected`，原活动实例与 ECS Action Component 不变。已删除独立 `CombatCohesionBreakAdapter`。

### Track Adapter（轨道适配层）

| 类型 | 用途 |
| --- | --- |
| `CharacterActionAdapterContext` | 适配器上下文：`(RuntimeFrame frame, GameplayEntityId gameplayEntityId, CombatEntityId combatEntityId, CombatBodyId combatBodyId, int sourceId=0, string traceId="")` |
| `CharacterActionAdapterDispatchMetadata` | 派发元数据 struct：除 frame、instance/plan/action id、localFrame、stableEventId、sourceId、traceId 外，还携带 lifecycle phase、boundary 与 exit reason。 |
| `CharacterActionMotionRequest` | 运动请求 struct：`(AdapterDispatchMetadata metadata, GameplayEntityId gameplayEntityId, CombatBodyId combatBodyId, TrackEventKind eventKind, MovementMode movementMode, float x, float y, float z, float movementControlScale=1, float rotationControlScale=1, MotionVectorSpace space=World, bool hasMovementControlScale=true, bool hasRotationControlScale=true)` |
| `CharacterActionRootMotionRequest` | Root motion 请求 struct：`(AdapterDispatchMetadata metadata, float x, float y, float z, RootMotionSpace space, RootMotionObstaclePolicy obstaclePolicy)`；只表示 action-local sample，不授权表现层反写权威 transform。 |
| `CharacterActionCombatRequest` | Combat 请求 struct；`StartCombatAction`/`HitTraceWindow` 保留原载荷，`SetHitTargetState` 携带类型化 `HitTargetStateFlags`。 |
| `CharacterActionGameplayRequest` | Gameplay 请求 struct：`(AdapterDispatchMetadata metadata, GameplayEntityId gameplayEntityId, TrackEventKind eventKind, string requestId, string abilityStableId, string targetPolicyId, CharacterActionDamagePayload damagePayload)` |
| `CharacterActionDebugRequest` | Debug 请求 struct：`(AdapterDispatchMetadata metadata, TrackEventKind eventKind, string markerId)` |
| `CharacterActionPresentationAdapterContext` | 表现适配器上下文：`(RuntimeFrame frame, string targetActorId, int sourceId=0, string traceId="", string animationLayerId="base")` |
| `CharacterActionAnimationRequest` | 动画请求 struct：除 metadata、目标角色、事件类型、动画 key、层和过渡时间外，还携带 `EndFrame`、`PlaybackSpeed`、动画源前后截断帧及可选 Clip ResourceKey。 |
| `CharacterActionAudioCueRequest` | 音频请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, string cueId)` |
| `CharacterActionVfxRequest` | VFX 请求 struct：除 metadata、target actor、ResourceKey 和帧区间外，携带正式 `CharacterActionVfxEventConfig`；其中通用 `MxAttachmentTransform` 覆盖跟随模式、挂点、Local/World 偏移空间和位置/旋转/缩放。VFX 专属部分包含 `PlayToCompletion / StopAtEventEnd / LoopUntilEventEnd` 播放方式、播放速率、实例整体尺寸、60 Hz 源帧前后截断、正常结束策略和四个确定性运行时参数槽。有限资源的时间轴长度为 `ceil((defaultLifetimeFrames - StartOffsetFrames - EndOffsetFrames) / PlaybackRate)`；完整播放按该长度结束，截止/循环模式以事件窗口为运行时上限。整体尺寸与挂点变换缩放相乘。 |
| `CharacterActionCameraRequest` | 相机请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, string requestId, string payloadKey)` |
| `CharacterActionUiFeedbackRequest` | UI 反馈请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, string feedbackId, string payloadKey)` |
| `CharacterActionPressureOnlyReactionRequest` | 仅压力反应请求 struct：`(CharacterReactionContext context, CharacterActionId requestedActionId=default)` |

**Sink 接口**（组合根实现，注入到 Adapter）：

| 接口 | 方法 |
| --- | --- |
| `ICharacterActionMotionRequestSink` | `SubmitMotionRequest(MotionRequest) → AdapterSinkResult` |
| `ICharacterActionRootMotionRequestSink` | `SubmitRootMotionRequest(RootMotionRequest) → AdapterSinkResult` |
| `ICharacterActionCombatRequestSink` | `SubmitCombatRequest(CombatRequest) → AdapterSinkResult` |
| `ICharacterActionGameplayRequestSink` | `SubmitGameplayRequest(GameplayRequest) → AdapterSinkResult` |
| `ICharacterActionPressureOnlyReactionRequestSink` | `SubmitPressureOnlyReactionRequest(PressureOnlyReactionRequest) → AdapterSinkResult` |
| `ICharacterActionDebugRequestSink` | `SubmitDebugRequest(DebugRequest) → AdapterSinkResult` |
| `ICharacterActionAnimationRequestSink` | `SubmitAnimationRequest(AnimationRequest) → AdapterSinkResult` |
| `ICharacterActionAudioCueRequestSink` | `SubmitAudioCueRequest(AudioCueRequest) → AdapterSinkResult` |
| `ICharacterActionVfxRequestSink` | `SubmitVfxRequest(VfxRequest) → AdapterSinkResult` |
| `ICharacterActionCameraRequestSink` | `SubmitCameraRequest(CameraRequest) → AdapterSinkResult` |
| `ICharacterActionUiFeedbackRequestSink` | `SubmitUiFeedbackRequest(UiFeedbackRequest) → AdapterSinkResult` |

**适配器类**：

| 类 | 用途 |
| --- | --- |
| `CharacterActionAdapterRequestCollector` | 实现 Motion/RootMotion/Combat/Gameplay/PressureOnlyReaction/Debug 六个 sink，收集请求到只读列表 |
| `CharacterActionPresentationRequestCollector` | 实现 Animation/AudioCue/Vfx/Camera/UiFeedback 五个 sink，收集请求到只读列表 |
| `CharacterActionTrackAdapter` | 轨道适配器：`HasDebugSink`、`Adapt(RunnerEvent, AdapterContext) → AdapterResult`、`AdaptMany(RunnerEvent[], AdapterContext) → AdapterResult`、`SubmitRootMotionRequest(RootMotionRequest) → AdapterSinkResult`、`SubmitPressureOnlyReaction(ReactionContext) → AdapterResult` |
| `CharacterActionPresentationTrackAdapter` | 表现轨道适配器：`Adapt(RunnerEvent, PresentationAdapterContext) → PresentationAdapterResult` |
| `CharacterActionAdapterSinkResult` | Sink 结果 struct：`Accepted(bool)`、`Diagnostics` — 工厂 `AcceptedResult()` / `Rejected(diagnostics)` |
| `CharacterActionAdapterResult` | 适配结果 struct：`MotionRequestCount`、`CombatRequestCount`、`GameplayRequestCount`、`PressureOnlyReactionRequestCount`、`DebugRequestCount`、`Diagnostics`、`Accepted(bool)` |
| `CharacterActionPresentationAdapterResult` | 表现适配结果 struct：`AnimationRequestCount`、`AudioCueRequestCount`、`VfxRequestCount`、`CameraRequestCount`、`UiFeedbackRequestCount`、`Diagnostics` |

### 轨道配置与事件（Tracks）

| 类型 | 用途 |
| --- | --- |
| `CharacterActionTrackKind` | 轨道类型枚举：Motion(0)、Combat(1)、Gameplay(2)、Animation(3)、Presentation(4)、Debug(5) |
| `CharacterActionTrackEventKind` | 轨道事件类型枚举：SetMovementMode(1)、ApplyImpulse(2)、LockMovement(3)、SetControlScale(4)、SetCollisionProfile(6)、StartCombatAction(100)、HitTraceWindow(101)、SetHitTargetState(102)、SendGameplayRequest(200)、CastAbility(201)、ApplyGameplayEffect(202)、StartEffectTimeline(203)、PlayAnimation(300)、CrossFadeAnimation(301)、SetAnimationBlend(302)、PlayAudioCue(400)、SpawnVisualCue(401)、CameraImpulse(402)、UiFeedback(403)、EmitDebugMarker(500)。Reset 与 Start/Stop HitTrace 类型已删除。 |

轨道配置（`MotionTrackConfig` / `CombatTrackConfig` / `GameplayTrackConfig` / `AnimationTrackConfig` / `PresentationTrackConfig` / `DebugTrackConfig`）——每个包含对应事件数组，静态 `Empty` 实例。

轨道事件（`MotionTrackEvent` / `CombatTrackEvent` / `GameplayTrackEvent` / `AnimationTrackEvent` / `PresentationTrackEvent` / `DebugTrackEvent`）——每个包含 `int Frame`、`TrackEventKind Kind`、`string StableEventId` 和轨道专有字段。有状态事件还必须包含包含式 `EndFrame`，且 `StableEventId` 在同动作、同轨道内唯一。`SpawnVisualCue` 的正式 JSON 使用嵌套 `vfxAttachment`：`mode`、`socketId`、`offsetSpace` 与 `transform.position*/rotationEuler*/scale*`；不读取旧的扁平 VFX 字段，不复用音效 `cueId`，也不存在兼容分支。

有状态区间的固定序列为：起始帧 `Enter(BeforeFrame) → Active(BeforeFrame)`；中间帧一次 `Active(BeforeFrame)`；结束帧 `Active(BeforeFrame) → Exit(NaturalEnd, AfterFrame)`。单帧区间同帧产生 `Enter → Active → Exit`。取消、打断、替换、死亡、重置、实体销毁、配置重载、Runtime 关闭、预览 Seek 或回放重建会立即产生一次 `Exit(BeforeFrame)` 并注销活动身份；任何已 Enter 的身份必须且只能 Exit 一次。

`CharacterControl.CharacterActionController` 的正式桥将 `TrackLifecycle` 与一次性 `TrackEventFired` 一起交给既有轨道分发协调器，保留 Runner 的生命周期载荷、顺序和实例身份。Runtime / Presentation Adapter 继续决定具体消费策略，Controller 不复制事件或额外生成 Exit；见 [CharacterControl 使用约定](CharacterControl.md#使用约定)。

非连续 Seek、向前跳帧、向后回放、循环回零和配置刷新采用同一重建规则：先退出旧活动集合，再重置并重放到目标帧，最后按目标帧重建活动集合。动画与 Root Motion 继续按目标帧采样；伤害、冲量、音频、Gameplay 命令和 `StartEffectTimeline` 都是不可逆单发，不进入通用 Exit。

动画事件在编辑器中按有源长度的时间轴剪辑处理：源长度来自实际 `AnimationClip`，最终时间轴长度为 `ceil((源长度 - AnimationStartOffsetFrames - AnimationEndOffsetFrames) / PlaybackSpeed)`。普通拖拽片段边缘反算 `PlaybackSpeed`，`Alt` 拖拽边缘修改前后截断，拖动主体只移动事件；动画片段仍必须位于动作时长内。无法解析源 Clip 长度时保留人工事件窗口，但不使用固定帧数回退，也不提供自动变速或截断。

Action Composition 的动作属性提供“整体速率 / 应用到所有事件”：对当前 `CharacterAuthored` 动作草稿执行一次可撤销的全轨重定时，保存后生效，重复应用基于当前结果继续缩放。倍率范围为 `0.05–16`，`0.8` 表示时间约拉长到 `1.25` 倍。总时长、阶段、取消窗口、聚势支持窗口、全部轨道事件与碰撞过渡时间一起缩放；区间以 `ceil(start / rate)` 与 `ceil((end + 1) / rate) - 1` 映射闭区间边界，保持相邻窗口连续。动画和 VFX 倍率相乘，动画混合时间同步拉伸，源素材截断保持不变；整数帧取整允许亚帧误差。RootMotion 对已有逐帧位移做时间区间重采样，保留手工编辑和总位移，不重新提取原始动画。伤害、冲量数值和独立引用资源不变：音效、技能、外部 Combat/Effect Timeline 只调整动作内的触发时机，不修改被引用资源的内部时序或音频音高。`CombatAnchored` 动作、倍率越界以及加速后不足一帧的窗口会在写入前拒绝，原草稿不受影响。此操作与工具栏预览倍率独立。

CharacterTest consumes `PresentationTrackEvent(kind=SpawnVisualCue)` as deterministic action presentation. Presentation events may carry `endFrame`; during the current placeholder-art adjustment period, Skill actions use at least 90 frames and Fireball / Fireball Burst use frames 0-29 visual cues for windup VFX. The CharacterTest bridge converts those cue resource keys into finite `VfxSourceDomains.ActionPresentation` spawn requests before `VfxRuntimeModule` ticks; gameplay request frames are not proportionally stretched.

`HitTraceWindow` authoring optionally carries `bladeTrail` (`enabled`, `advanceFrames`, `delayFrames`, prepared event-domain `stableEventId`). New weapon hit windows created by the authoring UI default to enabled trails with zero advance/delay; Action Hit Volume windows are excluded, and loading existing data preserves explicit disabled settings. The shared authoring/compiler projection derives an owned `SpawnVisualCue`, with `bladeTrailGenerated`, `bladeTrailSourceEventId`, and `useWeaponBladeTrailStyle`; runtime `CharacterActionVfxEventConfig.UseWeaponBladeTrailStyle` defaults to false and participates in equality/hash. The actor bridge and preview resolve the current slot's weapon default VFX style rather than changing hit authority. Generated events keep the existing Enter/Exit/stop-policy lifecycle, are read-only in authoring, and are omitted when restoring source; manual VFX remain independent. See [weapon blade trail contract](../Guides/WEAPON_BLADE_TRAIL.md) for identity, timing and publication rules.

#### Motion / Root Motion runtime contract

- 制作端的控制倍率由 `motionTrack.movementControlKeys`、`rotationControlKeys` 两组标记唯一拥有。`ControlScaleKeyJsonDto` 保存 `frame/value` 及制作期事件身份；缺少标记时默认 1。值必须有限且在 `0..10`，从所在帧立即生效到下一标记或动作退出，无插值。
- `CharacterActionControlScaleJsonProjection` 是 Unity Editor 与外部 Authoring 共用的 noEngine 制作／加载期投影器。发布时分别生成单轴 `SetControlScale(frame,endFrame)`，终点为下一标记前一帧或动作末帧；`controlScaleGenerated=true` 标记只读派生事件，制作源恢复时移除。Runtime Tick 不生成事件、不解析名称、不分配身份。
- 旧控制区间在 Editor 内存中逐轴转换：使用起点和包含式终点后一帧，按原起始帧／排序求有效值，只保存变化点，包括恢复默认 1 的标记。正常保存才准备正式身份并写回；转换失败保留原数据并阻止保存。草稿校验／隔离预览的临时身份不写目录，不可用于发布。
- `LockMovement`、派生 `SetControlScale` 与 `SetCollisionProfile` 继续使用 MotionTrack 有状态区间生命周期。旧发布数据的普通控制区间仍可导入；制作数据不能同时包含标记和手工控制区间。
- `SetControlScale` sets active-control contribution scales for the current action. Values use `0..10`, with deterministic milli-scales `0..10000`; 1 is normal and values above 1 accelerate active control.
- `movementControlScale` and `rotationControlScale` are independent. If one field is omitted, that axis keeps its current control scale.
- Active-control scales are reset to `1/1` automatically when the action finishes, is cancelled, or is interrupted.
- CharacterTest resolves overlapping Motion state ranges by start frame, then the persisted `StableEventOrder` carried by the dispatch. Control-scale, movement-lock and collision-profile contributions must not look up newly authored event IDs in the compiled legacy catalog; their priority comes from the published action data. Exiting the winning range restores the remaining contribution, or the default when none remains.
- `SetControlScale` does not apply `movementMode`; legacy data may still contain the field, but runtime ignores it for this event.
- `LockMovement` is interpreted by CharacterTest as active movement scale `0` and active rotation scale `1`.
- `SetCollisionProfile` is a MotionTrack authority range, not presentation
  animation events. They carry `collisionProfileId`, `collisionRestorePolicy`, and
  `collisionTransitionFrames`, and let runtime integrations such as CharacterTest align the
  authoritative gameplay collider posture with authored animation frames.
- CharacterAction does not derive collision shape from skeleton bounds or Unity Animator state.
  Runtime slices choose how profile ids map to collision dimensions; CharacterTest currently maps
  `standing`, `crouch`, `low`, `corpse`, and `revive_low` to explicit deterministic profiles.
- `ApplyImpulse` supports a `space` field. `World` is the default for existing data; `Local` rotates the X/Z impulse by the character yaw before centralized motion settlement.
- CharacterAction's ordinary `rootMotion.frames[]` is planar: X/Z remain action-authored displacements and Y is always zero, including after animation rebaking. A vertical animation curve does not move the authoritative character unless the action explicitly enables the `SampleRootMotionY` range event.
- `SampleRootMotionY` reads its own `motionTrack.verticalRootMotion.frames[]` Y deltas baked from the covering animation clip. The event's inclusive `frame/endFrame` gates those samples. Runtime consumes saved deltas and never samples Unity animation curves as motion authority; a missing active frame is a configuration error.
- Positive `SampleRootMotionY` deltas are multiplied by the movement profile's cached upward scale. The scale is `clamp(referenceGravity / (referenceGravity + abs(gravity)), 0.01, 1)`; gravity 0 yields 1. During a positive sample, gravity contributes through this multiplier and does not add a separate downward displacement. Thus a 0.2 m upward sample at scale 0.01 still requests 0.002 m upward movement, subject to collision and deterministic position precision. Negative samples continue through the ordinary falling/gravity settlement. `IgnoreGravity` makes the positive-sample scale 1 for its active range.
- `SetVerticalVelocity` is an inclusive range that sets vertical speed in metres per second each active frame. `IgnoreGravity` is an inclusive range that suppresses gravity while active; it does not clear existing vertical speed. Both end on action exit, interruption, or replay rebuild. `ApplyImpulse` remains a single-frame velocity request.
- Control-scale events affect only active control input: player/agent target velocity and yaw steering. They must not attenuate `RootMotionDelta`, `ApplyImpulse`, external forces, collision response, friction, or gravity.
- Authoring JSON fields are `movementControlScale` and `rotationControlScale`; omitted values mean "do not change this axis".
- `MotionTrackConfig.UsesRootMotion` is a declaration that the action expects authored root motion. It does not authorize Unity Animator/Playables root motion to mutate authoritative gameplay transforms directly.
- `MotionTrackConfig.RootMotion` owns optional action-local planar root motion samples: `sourceAnimationEventId`, `sourceAnimationClipResourceKey`, `space`, `sampleTickRate`, and per-frame `frames[]` deltas. Authoring tools may generate these samples from animation clips, but runtime integrations read them from the action config.
- Authoring may bake multiple Animation track events into one `MotionTrackConfig.RootMotion.frames[]` stream. Runtime integrations must treat `frames[]` as the merged authority and must not require separate runtime RootMotion objects per animation event.
- Runtime integrations must translate motion track events and action-authored root motion samples into their own motion authority. In CharacterControl bridge code, `CharacterActionMotionRequestAccumulatorSink` translates Motion track requests and `CharacterActionRootMotionRequest` samples into `CharacterMotionRequestAccumulator`. In CharacterTest this authority is `CharacterTestCharacterMotionSystem`, and the runtime bridge is `CharacterTestCharacterMotionRequest`.
- `ApplyImpulse` should be treated as a velocity request. `RootMotionDelta` should be treated as a per-frame displacement request. Positive authored Y samples use the cached upward gravity scale above; other motion still resolves in the same collision and motion system.

#### Combat HitTrace source contract

- One `HitTraceWindow(frame,endFrame)` opens one inclusive combat hit window. Without `hitVolume`, the window is a WeaponTrace window and should use `equipmentSlotId` to describe which equipped weapon slot opens hit detection, for example `mainHand`.
- `SetHitTargetState(frame,endFrame,targetStateMask)` contributes inclusive transient combat-target flags for the owning CharacterAction instance. Allowed bits are Invincible, Parrying, Blocking, SuperArmor and Unhittable; Alive is lifecycle-owned and invalid in action data. Contributions combine by bitwise OR and an Exit/cancel removes only the matching instance/event contribution.
- If `HitTraceWindow.hitVolume` is present and valid, the window is an Action Hit Volume window. `hitVolume.sourceKind` controls the hit source and takes priority over `equipmentSlotId` / `traceProfileId`; new Action Hit Volume data should leave those weapon selector fields empty.
- `hitVolume.sourceKind=ManualCapsule` is the compatibility default. It uses the authored local capsule (`shapeKind=Capsule`, `localPointA`, `localPointB`, `radius`) transformed by the action owner.
- `hitVolume.sourceKind=CasterBody` derives the capsule from the action owner's `CharacterTestCharacterPhysicsComponent` effective radius / height / centerY at runtime. It does not require or save authored A/B/radius values; the authority is the CharacterTest runtime physics body, not editor preview geometry.
- `CasterBody` may optionally carry action-local modifiers: `bodyOffset {x,y,z}`, `bodyRadiusScale`, `bodyRadiusAdd`, `bodyHeightScale`, and `bodyHeightAdd`. Missing values mean offset `0,0,0`, scales `1`, and adds `0`; scale values must be positive.
- Runtime computes modified CasterBody hit geometry as `radius = bodyRadius * bodyRadiusScale + bodyRadiusAdd`, `height = bodyHeight * bodyHeightScale + bodyHeightAdd`, and `segmentHalf = max(0, height * 0.5 - radius)`. If the final radius or height is not positive, the hit volume is invalid and should not query.
- `CharacterActionHitVolumeSourceKind.BakedSocketCapsule` 表示由角色动画挂点轨道驱动的天然攻击胶囊；JSON 固定字段为 `startSocketId`、`endSocketId`、`radius`。该模式不得同时配置 `localPointA/B`、CasterBody 修正、`equipmentSlotId` 或 `traceProfileId`，导入器遇到混合配置时保留诊断并把命中体判为无效。
- Baked Socket Capsule 的运行时权威是实体自身 Generated Package 下的 Socket Bake v3 配对：`generated/character_socket_bake_catalog.json` 小索引与 `generated/character_socket_bake_catalog.bytes` 逐帧载荷，由 `CharacterSocketBakeCatalogLoader` 成对校验后读取。旧完整 JSON 必须重新导入，不自动回退。查询必须同时命中当前动画 key、两个 socket 和当前本地帧；缺失角色包、动画、socket 或帧时失败关闭，不回退到角色根节点、手工胶囊、武器 Trace 或实时 Animator。`ManualCapsule`、`CasterBody` 与既有 Weapon Trace 保持独立兼容。
- `bodyOffset` is owner-local XYZ and rotates with the action owner's yaw before being added to the runtime body center. These modifiers affect only the hit query and preview; they do not change the real kinematic body, movement blocking, mass, push response, or Unity Rigidbody state.
- Weapon-specific trace configuration stays on weapon/package data (`WeaponConfig.TraceBindings` plus geometry `weaponTraces`). The action timeline must not bind directly to a concrete weapon trace such as `trace.iron_sword.blade` for new data.
- Weapon trace geometry may opt into `proximalExtension` + `proximalRadius`. Runtime still treats the hit window as the same `WeaponTrace`: it queries the authored blade capsule (`blade_root -> blade_tip`) and, when both proximal fields are positive, an additional short capsule extending backward from `blade_root` along the weapon axis. This covers close-range gaps near the hand without changing action events, damage source/profile configuration, blade-tip reach, Unity Physics, or `CombatPhysicsWorld` query semantics.
- Runtime adapters carry both `equipmentSlotId` and legacy `traceProfileId`. Runtime integrations must resolve `equipmentSlotId -> current equipped weapon -> weapon trace binding -> geometry trace` before generating `WeaponTraceFrame`.
- `traceProfileId` 仅用于仍由新 schema 明确引用的既有 trace 资源；Authoring UI 应把 `equipmentSlotId` / Action Hit Volume 作为主要工作流。
- `HitTraceWindow` carries a normalized `hitEvent` (`CombatHitEventConfig`) for target filtering, environment hit policy, stable event id, max accepted hits, same-target policy, common damage value (`damageTypeId`, `baseDamage`, `impactPower`, `tags`, `analysisCategory`), and optional `onHitBuffs`. `equipmentSlotId` chooses the weapon slot and baked trace only; it does not own target type or damage semantics.
- `damageProfileId` and legacy top-level damage payload fields are invalid in the formal CharacterAction importer. Damage-producing `HitTraceWindow` / `ApplyGameplayEffect` events must carry `hitEvent.damage`, including `baseDamage`, `impactPower`, and optional `launchPower`. Damage Profile and Presentation Profile are not part of the common `hitEvent` authoring path.
- `hitEvent.damage.formulaId` is read only for legacy migration and is omitted from new authoring output. CharacterTest uses one global soft-defense formula for WeaponTrace, scripted/area, projectile direct/area, and status-tick damage. The preview pane’s “伤害事件” view compares the current action’s `HitTraceWindow` inputs while retaining the timeline and playhead.
- Environment targets such as wall / ground / static scene objects only become valid hit trace outputs when `hitEvent.targetTypes` includes the matching world target and `environmentHitPolicy=BlockedPresentation`. This path produces blocked / presentation / diagnostics semantics and does not apply HP damage.
- CharacterAction authoring should expose target types as checkboxes, environment policy as a Chinese enum, and raw layer masks or legacy ids only in advanced diagnostics. Damage Profile and hit presentation Profile are not primary editing fields for `HitTraceWindow`.
- CharacterTest resolves root motion through `MotionTrackConfig.RootMotion`, optionally cross-checking the configured source animation event/clip against the active animation track event. `MotionTrackConfig.UsesRootMotion` only enables the path; `motionTrack.rootMotion.frames` supplies the actual delta frames.
- If an action declares root motion but the runtime cannot resolve the configured sample for the current action frame, that is an Error-level runtime resource/config diagnostic, not a silent fallback.
- CharacterTest CD-M0 的 direct-hit 实际执行与预演构建共用同一个内部 trace sampler。它按 Active frame 逐帧读取调用方提供的定点 source pose，单帧内固定先生成 WeaponTrace，再按事件顺序生成 `ManualCapsule`、`CasterBody`、`BakedSocketCapsule`；每个 resolved action 独立维护 sample、稳定 ID 与保守 broadphase bounds。预演输入若存在 frame 缺口、乱序、缺失 pose、未知 source kind、action/hit-window 不匹配或 revision 不合法则失败关闭。
- CD-M0 实际执行侧的 `resolvedActionRevision` 目前由当前 `ActionInstanceId`/`HitWindowId` 映射，不是 action catalog/config 内容 hash。CD-M1 在 CharacterTest 内把最终 resolved `BasicAttack/light_attack` action 绑定到 Attack Opportunity guard；Trace Plan 仍只负责确定性采样和生命周期校验。
- CD-M1 `PrimaryAttack` 仅在 `CurrentPose` opportunity 提交 direct hit；`TurnInPlace` 通过显式量化 yaw 的 zero-translation command/input 语义转向，本 tick 不启动攻击；`RequiresTranslation`、`Unavailable`、`StaleSnapshot`、`Unsupported` 均拒绝 PrimaryAttack。`HasActiveMove=false` 仍必须由 Motion 应用 facing 更新体现。
- CD-M1 Trace Plan/adapter identity 绑定独立 source pose/facing write revision；同帧 root、yaw 或 AuthorityPose/socket 变化必须使旧 witness stale。实际窗口与 Preview 共用 `PolicyEventId`、same-target/max-hit ledger 和 damage eligibility；缺失/非法 `hitEvent.damage` 不得让 Preview 宣称可攻击。M1 不计算 package/profile 的 HitZone 倍率，完整 Zone 规则属于 CD-M2。
- LCC-M4 不新增 CharacterAction track/schema 或第二套 socket source。对 geometry v6 中显式声明的 Socket attachment，CharacterTest 的 actual `BakedSocketCapsule` 与 Planner preview 在同一 action/local frame 上读取唯一 resolved authority pose frame/revision，并把 channel delta 应用于既有 socket-bake sample；未声明 attachment、缺帧、source/revision stale 或无效 basis 继续失败关闭或保持既有 base-pose 合同，禁止回退到实时 Animator/Transform。正式 Dragon14 `attack1a` 当前只为 `rightClawStart/rightClawEnd` 提供该 clip 的实际轨道，不能跨 clip 拼出左爪同帧证据。
- formal network snapshot 在 schema 25 恢复活动 HitTrace window、instance/window IDs 与 hit-once state，在 schema 26 恢复 source revisions，在 schema 27 恢复 CasterBody previous-center sweep state；action authority restore 不需要伪造或重放已经发生的历史 `HitTraceWindow` track event。
- `CastAbilitySlot`、Skill、Guard、Dodge 等非 `BasicAttack/light_attack` action 不得误用 PrimaryAttack guard；它们继续各自 action/旧范围兼容合同，并在适用处标记 `CompatibilityLegacyRange`。旧 direct-input wrapper 仅是兼容旁路/测试入口，不是 M1 生产提交路径。
- 影响未来命令的 TurnInPlace yaw/delta 与消费状态必须随 command、network、snapshot、hash、rollback 和 replay 保持一致；解析 requested action 时必须绑定 ActionSet/intent 最终 resolved action revision，不能把 Planner display/action id 当作 CharacterAction id 静默 fallback。
- CD-M4 的联合选择只预览已由 ActionSet/intent 最终解析的 `BasicAttack`。派生 trace cache identity 同时绑定 action-set binding、Animation track、RootMotion 和 Combat trace 内容 witness；只有最终胜者可以提交现有 PrimaryAttack/TurnInPlace/Approach command，selector 本身不启动或替换 CharacterAction。

#### Common `hitEvent.damage` timeline contract

- CharacterTest 新数据把伤害权威写在通用 `hitEvent.damage` 数据结构上。`HitTraceWindow` / `ApplyGameplayEffect` 的公共入口是 `hitEvent`，它携带目标类型、环境策略、命中约束、`hitEvent.damage` 伤害数值和可选 `onHitBuffs`。`onHitBuffs` 由 Combat on-hit Buff bridge 在 Damage-kind 命中后转为 `GameplayBuffRequestBuffer` apply request；0 HP damage 的控制命中可以触发 Buff，但不计入 accepted HP damage。
- CharacterAction Inspector 已把 `onHitBuffs` 接入所有当前伤害事件编辑面：武器 `HitTraceWindow`、范围 `ApplyGameplayEffect`、projectile direct `impact.hitEvent` 和 `AreaPulseOnImpact` 的 `impact.areaHitEvent`。Inspector 只编辑 Buff 引用（`buffId`）；持续时间、叠层、来源和效果本体都由 Buff v2 definition 与运行时命中上下文决定。Buff v2 定义本体仍由 `buff_authoring.json` 和 Authoring CLI 生成。
- `equipmentSlotId` 仍只负责选择当前装备槽位、武器和轨迹；它不拥有动作伤害数字。`targetPolicyId` 仍用于非武器伤害，例如 `target.area_2m` 或 `target.locked.area_2m` 的 `AreaPulse`。
- `damageProfileId` 不是正式字段。Action JSON 中出现 `damageProfileId` 会产生导入错误；旧顶层 `baseDamage/impactPower/launchPower/tags/analysisCategory` 也会产生导入错误。
- 新 Authoring UI 和新导出只写 `hitEvent.damage`。编辑器不再显示 Damage Profile 迁移入口，也不要求用户维护独立伤害档案。
- 时间轴只配置基础 HP 伤害、攻击力收益、`impactPower / 冲击强度`、`launchPower / 击飞效果值`、`damageTypeId / 伤害性质` 和少量分析标签；这些字段属于通用 `hitEvent.damage` 数据结构，不属于 ReactionProfile、受击动作组或武器 trace 配置。Authoring UI 不把 `damageTypeId` 描述为公式分支。CharacterTest 当前固定伤害公式不按 `damageTypeId` 分支；该字段用于日志、分析、筛选和后续表现/反应扩展。`impactPower` 由 CharacterTest 结合双方质量和命中部位 `ImpulseScale` 派生为 `ScatterImpact` 与一次性击退初速度；`launchPower` 是特殊受击输入，`0` 表示无击飞，`100` 表示标准人形击飞，正式触发不使用 `impactPower` 阈值或 `reaction.input.launch` tag。轻击默认基准为 `HP 6 + Attack x0.65 / 冲击 34`，同级普通目标第 3 次连续轻击破聚。最终硬直、实际击退距离、命中表现、防御压力等单边命中反馈不在伤害事件中固定配置。
- `impactPower` 是唯一公开冲击字段；CharacterAction importer / Authoring UI / 导出 JSON 不再读取或写出旧 `scatterDamage`。
- `launchPower` 不属于冲击字段，不派生 `ScatterImpact`，也不改变水平击退；CharacterTest v1 只在目标已 Broken 且目标承受能力允许时把它解析为垂直击飞速度。
- Every damage-producing event must have a stable `stableEventId`. Authoring tools should auto-generate one and surface a warning instead of asking users to hand-type opaque ids.
- `CharacterActionDamageUsageIndexBuilder` exports a deterministic action damage usage index for analytics. Combat hit windows are indexed as `WeaponTrace`; weapon trace proximal range is geometry detail under the same source kind. `ApplyGameplayEffect` events are indexed as `Scripted` / `AreaPulse` style usages and include the inline `DamagePayload`. Future Buff DOT events should appear as runtime `StatusTick` damage requests, not as direct HP mutations in Buff tick code.

### Validation

| 类型 | 用途 |
| --- | --- |
| `CharacterActionValidation` | 校验入口 static class：`ValidateActionSet(actionSet, actions) → Diagnostic[]`、`ValidateActionConfig(action, combatTimeline=null) → Diagnostic[]`、`ValidateReactionProfile(profile, actions, completeness) → Diagnostic[]` |
| `CharacterActionPhaseValidator` | Phase 校验：`Validate(authority, phases, combatTimeline, durationFrames=null) → ValidationIssue[]` — 检查覆盖完整 duration、无重叠、无间隙 |
| `CharacterActionValidationIssue` | 校验问题 struct：`(string code, int phaseIndex, PhaseKind phaseKind, string message)` |
| `CharacterActionResourceDependencyKind` | 资源依赖类型枚举：CombatAction、TraceProfile、GameplayRequest、AnimationAction、AudioCue、VfxResource、MotionEvent、DebugMarker |
| `CharacterActionResourceDependency` | 资源依赖 struct：`(kind, stableId, actionId, trackKind, eventKind, frame=-1, stableEventId="", isMissing=false)` |
| `CharacterActionResourceDependencyCollector` | 依赖收集器：`Collect(CharacterActionConfig) → ResourceDependency[]` |

### 诊断

| 类型 | 用途 |
| --- | --- |
| `CharacterActionDiagnosticCodes` | 诊断 code 常量：48 个 `ACT_*` 字符串常量（MissingActionSet、PhaseGap、CombatActionMissing、AdapterSinkFailure 等） |
| `CharacterActionDiagnostics` | 兼容常量包装器（`const string` 字段指向 `DiagnosticCodes`） |
| `CharacterActionDiagnosticSeverity` | 严重级别枚举：Info、Warning、Error |
| `CharacterActionDiagnostic` | 诊断条目 struct：`(string code, DiagnosticSeverity severity, string message)` — 工厂 `Warning(code, message)` / `Error(code, message)` / `Info(code, message)` |
| `CharacterActionDiagnosticFormatContext` | 诊断格式化上下文携带 `CharacterActionId`、Phase、Track、frame 与 suggested fix。 |
| `CharacterActionDiagnosticFormatter` | 诊断格式化器：`Format(diagnostic, context=default) → string`、`FormatMany(diagnostics, contexts=null) → string[]` |

### 受击反应

| 类型 | 用途 |
| --- | --- |
| `CharacterReactionContextSourceKind` | 反应上下文来源枚举：Unknown、CohesionBreak、GuardBreak、ArmorBreak、PressureBandChanged、Death、Lifecycle、Hit |
| `CharacterReactionContextCompleteness` | 上下文完整度枚举：None、SourceOnly、PressureOnly、Full |
| `CharacterHitDirection` | 受击方向枚举：Unknown、Front、Back、Left、Right、Up、Down |
| `CharacterReactionContext` | 反应上下文 struct：`(SourceKind, Completeness, RuntimeFrame, GameplayEntityId, PreviousPressureBand, CurrentScatterPressureBand, PreviousPressure, CurrentScatterPressure, MaxScatterPressure, Delta, SourceId, Reason, TraceId, bool isDeath, LifecycleState, BodyPartId, HitZoneId, DamageTypeId, HitDirection, ReactionGroupId, int impactForce=0, bool isAirborne=false, CurrentActionId, CurrentCharacterPhase, CurrentCombatPhase, CurrentActionCommitted, CurrentActionInterruptible)` |
| `CharacterReactionHitSource` | 受击数据源携带强类型 `CharacterActionId CurrentActionId`；其它字段为受击部位、方向、压力、生命周期、phase 与 trace 上下文。 |
| `CharacterReactionContextBuildResult` | 构建结果 struct：`Context(ReactionContext)`、`Completeness`、`Diagnostics`、`Success(bool)` |
| `CharacterReactionContextBuilder` | 上下文构建器：`FromHitSource(CharacterReactionHitSource) → BuildResult`、`FromHitSourceNoAlloc(CharacterReactionHitSource) → BuildResult`、`FromCohesionBreak(CohesionBreakEvent) → BuildResult`、`FromCohesionBreakNoAlloc(CohesionBreakEvent) → BuildResult`、`FromDeathNoAlloc(...) → BuildResult`、`FromGuardBreak(GuardBreakEvent) → BuildResult`。NoAlloc variants omit warning/error diagnostic arrays and are intended for hot runtime paths after callers already know whether diagnostics are needed. |
| `CharacterReactionRuleTrigger` | 反应规则触发器枚举：Any、CohesionBreak、GuardBreak、ArmorBreak、PressureBandChanged、Death、Lifecycle、Hit |
| `CharacterReactionRule` | 反应规则 sealed class；目标动作为 `CharacterActionId`，其余条件为 trigger、部位、方向、伤害类型、压力、生命周期、phase、impact force 与 reaction group。 |
| `CharacterReactionProfile` | 反应配置 sealed class：Profile 自身仍用字符串 StableId；规则和 `DefaultActionId` 使用 `CharacterActionId`。 |
| `CharacterReactionSelectionResult` | 选择结果 struct：`Accepted(bool)`、`SelectedActionId(CharacterActionId)`、`RejectCode(string)`、`Diagnostics`。 |
| `CharacterReactionSelectionFastResult` | NoAlloc 选择结果 struct：`Accepted(bool)`、`SelectedActionId(CharacterActionId)`、`RejectCode(string)`、`FallbackUsed(bool)`；不收集 skipped-rule diagnostics。 |
| `CharacterReactionSelector` | 反应选择器 static class：`Select(CharacterReactionProfile, CharacterReactionContext) → SelectionResult` for diagnostics / compatibility, `SelectNoAlloc(CharacterReactionProfile, CharacterReactionContext) → FastResult` for hot runtime start paths. |
| `CharacterReactionRuleValidator` | 反应规则校验器：`ValidateProfileForCompleteness(profile, completeness) → Diagnostic[]` |

CharacterTest 组合根仅在目标存活且已处于 Broken 时，才对有效普通命中使用 `CharacterReactionContextBuilder.FromHitSourceNoAlloc(...)` 和 `CharacterActionReactionRuntime` 启动正式 Reaction action；未破聚的命中仍结算 HP、散势和独立击退，不生成普通受击动作或受击硬直。高 / 中 / 低与普通 / 强力动作由 Reaction Profile 选择；`CohesionBreak`、Launch 和 Death 优先，不被同帧普通 Hit 覆盖。受击动作首帧通过 `SetControlScale(0, 0)` 阻断主动移动和转向，末帧恢复 `1/1`；Combat/Kinematic impulse 不受 control scale 缩放。`HitStunFramesRemaining` 仅用于 Combat 状态和诊断，不是移动权限来源，Reaction 结束也不会把角色位置恢复到受击前。

CharacterStudio / Character Package 的受击动作最小运行契约由 Profile 自己声明：`defaultSlotId` 对应槽位天然必需，`required=true` 的槽位也必需；其余槽位是可选表现增强。编译器只为已绑定的可选槽位生成规则，完整 Hit 上下文没有命中专用规则时可由组合根允许回退到 `defaultActionId`。当前默认 Profile 把 `hit_default` 作为默认槽位并把 `death` 标为必需，因此角色只绑定这两个动作即可编译运行；高 / 中 / 低、强力、Launch 和 CohesionBreak 可逐步补齐。Death 仍要求显式动作且不允许误用普通受击 fallback；Revive 未配置动作时由 CharacterTest 生命周期流程直接完成复活。

### 诊断工作站（Workstation）

| 类型 | 用途 |
| --- | --- |
| `CharacterActionWorkstationCapability` | 工作站能力枚举：ReadOnlyPreview、LightEditing、UnityAssetEditing |
| `CharacterActionWorkstationRowKind` | 时间轴行枚举：Phase、Motion、Combat、Gameplay、Animation、Presentation、Debug、Cancel、Interrupt |
| `CharacterActionWorkstationBuildRequest` | 构建请求：`(CharacterActionConfig action, CombatActionTimeline combatTimeline=null, DurationPolicy, RunnerEvent[], Diagnostic[], Capability[])` |
| `CharacterActionWorkstationTimelineEntry` | 时间轴条目 struct：`(RowKind, int startFrame, int endFrame, PhaseKind, EventKind, SourceKind, stableId, payload)` |
| `CharacterActionWorkstationTimelineRow` | 时间轴行：`(RowKind, string label, TimelineEntry[])` |
| `CharacterActionWorkstationPreviewEvent` | 预览事件：`(int sequence, RunnerEvent runnerEvent)` |
| `CharacterActionWorkstationSnapshot` | 诊断快照 sealed class：`ActionId`、`DisplayName`、`Category`、`TimelineAuthority`、`DurationFrames`、`DurationResolved`、`TimelineRows`、`Dependencies`、`Diagnostics`、`FormattedDiagnostics`、`PreviewEvents`、`ExportLines`、`HasErrors(bool)`、`ExportText() → string` |
| `CharacterActionWorkstation` | 工作站入口 static class：`BuildSnapshot(BuildRequest) → WorkstationSnapshot`、`BuildSnapshot(CharacterActionConfig, CombatActionTimeline=null) → WorkstationSnapshot` |

Authoring / Workstation 应把 `CharacterActionWorkstationRowKind.Cancel` 显示为“切换窗口”轨道。该轨道底层仍读写 `cancelRules`；属性面板只维护 `requestTypeMask` 与 `actionSlotMask` 两个多选掩码，两个 `0` 都显示为“任意”，不显示目标动作、来源或允许切换。编辑器应按 Active / Recovery / Loop / Landing / Channel / Hold / Exit 限制可拖拽范围；跨 phase 拖拽只有在每一帧都被连续可切换 phase 覆盖时才合法。旧 JSON 的 `targetActionId`、`sourceKind`、`allow` 作为未知兼容字段被忽略，重新保存后不再输出。

## 程序集边界

```text
MxFramework.CharacterAction
  -> MxFramework.Combat
  -> MxFramework.Gameplay
  -> MxFramework.Runtime
```

`MxFramework.CharacterAction` 当前由 CharacterTest Runtime Slice、CharacterControl、两个 Runtime 示例和测试程序集引用；其他组合根仍需显式添加 asmdef 引用并构建自己的 Gateway owner。

## 使用约定

- 所有配置类使用构造函数（无 parameterless constructor，属性 get-only），不支持 object initializer。
- `CharacterActionConfig.DurationFrames` 和 `Phases` 必须覆盖完整动作时长。`CharacterActionPhaseValidator` 要求 phases 无间隙、无重叠，且最后一个 phase 的 endFrame 等于 `DurationFrames - 1`。
- `character_action_profile.json` 是运行时组合入口：Profile 只列出要加载的 `actionSetIds` 和 `actionLibraryIds`，不表示 ActionSet 或 Action 被角色、武器拥有。
- `CharacterActionResolver.ResolveCommand()` 通过 `CharacterActionBinding.IntentId` 匹配请求 intent，再使用强类型 `binding.ActionId` 在唯一的 `CharacterActionId → CharacterActionConfig` 索引中解析；连段由 ActionSet sequence 决定，不比较动作名称。
- 普通动作切换只能提交 `StartSlot` / `StartIntent`，普通反应只能提交 `React`；调用方不得模拟“先终止、再启动”。
- 只有已经拥有行为配方执行权威的 Runtime AI Planner 桥接层可以提交 `ReplaceForOrchestration / TerminateForOrchestration`。Timeline 跳转先预检目标，再原子替换；返回 Planner 只终止当前配方动作。该权限不改变普通输入、普通 AI 请求和 Reaction 的切换/打断规则。
- CharacterTest 的动作使用提交在项目层协调：Gateway 状态及事件先暂存，当前使用要求与既有 Ability／属性费用提交成功后才发布动作／动画／清理输出；失败恢复原 Gateway，不产生该次新动作或冷却。暂存仅存在于同步命令消费边界，期间不 Tick 世界或调用外部观察者；Gateway 不拥有费用规则。这是已验证边界内的提交协调，不承诺任意进程崩溃或外部重入的通用 ACID。
- 战术事件的提交失败记录为本次进入的 `CommitFailed` 及具体原因，不在游标停留时反复提交。配置局部结束后，由原命令消费阶段处理有界后继，再交原运动消费者；默认不自动退卡、重选或强制兜底。移动／转向仍遵守既有权限、优先级和惯性。
- `Gateway.Tick(buffer)` 自动递增 `localFrame` 并把事件写入调用方复用缓冲。
- `CharacterReactionContextBuilder.FromHitSource()` 要求 6 个字段齐全（BodyPartId、HitZoneId、DamageTypeId、HitDirection ≠ Unknown、ImpactForce 有值、ReactionGroupId 非空）才返回 `Completeness=Full`；不完整的 Hit 上下文仍可以按规则实际声明的字段逐项匹配。例如 SourceOnly context 只要带 `reactionGroupId + impactForce`，即可匹配 `Hit + Broken + reactionGroupId + min/maxImpactForce` 规则；`HitDirection=Unknown` 不会阻止这类不要求方向的规则。规则要求但 context 缺失的 bodyPart / hitZone / damageType / direction / impactForce / reactionGroup 仍会被拒绝，且无匹配规则时不会使用 fallback action。
- Character Package 校验不得把 Profile 的全部槽位强制绑定；只阻塞 `defaultSlotId`、`required=true` 或用户已经显式建立但内容损坏/冲突的绑定。可选槽位完全缺省不是 warning，也不进入顶部阻塞计数。
- `CharacterActionWorkstation` 是只读诊断工具，不支持编辑写回。

## 测试入口

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterAction/`（17 个测试文件）

## 当前约束

- 其他组合根仍需手动添加 `MxFramework.CharacterAction` asmdef 引用，并为每个执行实体持有 Gateway、计划缓存和复用缓冲。
- `CharacterControl` 已覆盖 request → resolver / gateway → track/presentation dispatch；`SubmitReaction(...)` / `CharacterActionReactionBridge` 覆盖通用 action-level reaction。旧 CombatAction 只能经 `CharacterActionCombatTrackBridge` 作为子动作运行。
- 已有 CharacterTest Runtime Slice 接入 Profile + JSON-backed jump、light attack 和多种 ability sample；Profile 选择 ActionSet 和 Action Library，Runtime 再合并导入。
- 已提供 `MxFramework/Character/Character Action Authoring` Unity Editor 工作台。它通过“动作编排 / 技能编排 / 映射与方案 / 共享资源”四个工作区聚合当前 Profile 的全部 ActionSet、Reaction Profile、Landing Profile、Action Library 和 Ability Effect Timeline；Landing Profile 可编辑轻/重 Action Slot 与整数速度阈值，并和 ActionSet bundle 参与同一次保存事务。

## JSON Authoring Import

| Type | Purpose |
| --- | --- |
| `CharacterActionJsonImporter` | Low-frequency noEngine JSON import entry: `Import(actionSetsJson, actionsJson, actionSetsSourcePath="character_action_sets.json", actionsSourcePath="character_actions.json") -> CharacterActionJsonImportResult`; single-file `ImportActionSets` / `ImportActions` are also available. |
| `CharacterActionJsonImportResult` | Import result with `ActionSets`, `Actions`, `Diagnostics`, and `Success`; `Success=false` when any error diagnostic exists. |
| `CharacterActionJsonDiagnostic` | Stable import diagnostic with `SourcePath`, `JsonPath`, `Code`, `Severity`, and `Message`; can be converted to `CharacterActionDiagnostic`. |
| `CharacterActionProfilesJsonDocument` | DTO root for `character_action_profile.json`; used by authoring/runtime composition sources to select ActionSet ids and Action Library ids before importing action runtime configs. |
| `CharacterActionSetsJsonDocument` / `CharacterActionsJsonDocument` | DTO roots for `character_action_sets.json` and `character_actions.json`. |
| `CharacterActionProfileJsonDto` | Serialized profile DTO: `id`, `stableId`, `displayName`, `actionSetIds[]`, `actionLibraryIds[]`. |
| `CharacterActionSetJsonDto` / `CharacterActionJsonDto` | Serialized DTOs for action sets and actions; fields mirror the runtime constructor inputs. |
| `*TrackJsonDto` / `*TrackEventJsonDto` | Serialized DTOs for Motion, Combat, Gameplay, Animation, Presentation, and Debug tracks. |

The importer converts already-loaded JSON strings into `CharacterActionSetConfig[]` and `CharacterActionConfig[]`. Action Library 只接受 `0.3.0`，Profile/ActionSet/Reaction/Landing 只接受 `0.4.0`；旧字符串 Action ID schema 返回稳定的 unsupported/invalid diagnostic，不自动转换或按显示名猜测。它不读取文件，也不依赖 `UnityEngine`、`UnityEditor` 或 `JsonUtility`。Diagnostics 按 `SourcePath`、`JsonPath`、`Code`、`Message` 排序；非正 ID、重复数字 ID、缺失引用、畸形 JSON、未知枚举与非法帧范围均阻断导入。

## Validation Registry

| Type | Purpose |
| --- | --- |
| `CharacterActionConfigRegistry` | Low-frequency registry；动作只建立 `CharacterActionId → CharacterActionConfig` 索引并按数字 ID 排序/tie-break。ActionSet 自身仍可按其领域 StableId/id 查询。重复或非正动作 ID 阻断，不提供字符串动作查询。 |
| `CharacterActionValidationReport` | Aggregates registry diagnostics, local CharacterAction validation, and optional cross-module validation diagnostics. `Validate(registry, options)` returns sorted diagnostics with `SourcePath`, `StablePath`, `Code`, `Severity`, and `Message`. |
| `CharacterActionValidationDiagnostic` | Stable report diagnostic with source path and stable path fields. Converts to legacy `CharacterActionDiagnostic` when pathless consumers are required. |
| `CharacterActionValidationOptions` | Report options, including `MissingAnimationActionKeyPolicy` and injected cross-module validators. |
| `CharacterActionMissingAnimationActionKeyPolicy` | Controls local missing animation action key severity: `Error`, `Warning`, or `Ignore`. |

Local report validation checks duplicate registry ids, missing command / ability / reaction action references, CharacterAuthored missing duration, phase gaps / overlaps / duration overflow, cancel window coverage / duration overflow, cancel / interrupt target ids, missing track payload ids, missing `CombatAnchored` combat action id, and track event frames outside known duration.

Cross-module validation remains interface-only inside `MxFramework.CharacterAction`; the module does not reference MxAnimation, Resources, Character.Application, UnityEngine, or UnityEditor.

| Interface / default validator | External responsibility |
| --- | --- |
| `ICharacterActionCrossModuleValidator` | Generic extension point consumed by `CharacterActionValidationReport`. |
| `ICharacterActionAnimationActionKeyProvider` + `CharacterActionAnimationActionKeyValidator` | Validate animation action keys against an external animation registry. |
| `ICharacterActionCombatActionIdProvider` + `CharacterActionCombatActionIdValidator` | Validate combat action ids against external combat runtime data. |
| `ICharacterActionPresentationResourceProvider` + `CharacterActionPresentationResourceValidator` | Validate audio cue and presentation resource keys against an external resource or presentation registry. |
| `ICharacterActionEquipmentTagProvider` + `CharacterActionEquipmentTagRequirementValidator` | Validate equipment tag requirements and ability binding tag gates. |
| `ICharacterActionCharacterEquipmentStateProvider` + `CharacterActionCharacterEquipmentStateValidator` | Validate action-set character and equipment-state references against Character.Application-owned data. |

## Unity Editor Authoring Workbench

| Type / Entry | Purpose |
| --- | --- |
| `MxFramework/Character/Character Action Authoring` | Editor menu entry that opens the Character Action Authoring workbench. |
| `CharacterActionAuthoringWindow` | Editor-only UI Toolkit workbench with four persistent workspaces: “动作编排”, “技能编排”, “映射与方案” and “共享资源”. 动作与技能编排共享时间轴、播放控制和隔离预览骨架；`OpenWindow()` and `OpenForAbilityBinding(...)` keep their existing public signatures, and the latter selects the ability-binding row in “映射与方案”. |
| `CharacterActionAuthoringOpenRequest.StatusCallback` | Editor-only in-process status callback used by contextual callers such as Runtime AI Lab. It is ignored by JSON serialization, so CharacterStudio open-request/status wire files remain unchanged. |
| `CharacterActionAuthoringWorkspaceSession` | Editor-only UI-neutral Action Composition session shared by the existing EditorWindow and embedded AI Lab frontend. It projects actions, all timeline tracks, scalar Inspector fields, Problems and dirty paths; commands cover action/timeline mutation, gestures, Undo/Redo, validation and Save All. |
| `CharacterActionAuthoringWorkspaceSnapshot` / `CharacterActionAuthoringWorkspaceCommand` | Stable Editor authoring projection and command protocol. UI clients do not own or directly persist mutable `CharacterActionJsonDto` instances. |
| `CharacterActionAuthoringInspectorFieldDescriptor` | Data-driven scalar Inspector contract for action metadata and selected Phase/Transition/Motion/Animation/Combat/Gameplay/Presentation entries. |
| `CharacterActionAuthoringDocumentSession` | Internal Editor-only full-document session. It owns every `profiles[]`, `actionSets[]`, `reactionProfiles[]` entry loaded from the selected sources plus all Action Libraries referenced by the current Profile, tracks selection by stable id, and preserves untouched sibling JSON entries during round-trip. |
| `CharacterActionAuthoringModel` | Existing Editor-only mutable projection for the current Profile / ActionSet / Action Library context. It remains an implementation bridge for inspectors and timeline code; it is not expanded into a new public document API. |
| `CharacterActionAuthoringReferenceContext` | Editor-only cross-module reference context. It implements the CharacterAction validator provider interfaces for animation action keys, combat action ids, presentation resource keys, audio cue ids, equipment tags, and character/equipment-state ids. |
| `CharacterActionReferenceDiscoveryService` | Editor-only provider-backed discovery service for populating authoring references from MxAnimation registry assets, Combat/Character.Application config objects, and ResourceCatalog metadata. It returns source entries plus unavailable-provider diagnostics while preserving manual fallback references. |
| `CharacterActionAuthoringJsonService` | Editor-only import/validate/export service. It serializes guide-shaped JSON, imports through `CharacterActionJsonImporter`, builds `CharacterActionConfigRegistry`, validates with `CharacterActionValidationReport`, and builds `CharacterActionWorkstation` snapshots for preview. |
| `CharacterActionAuthoringProblemsModel` | Internal Editor-only projection over structured Import, Local Validation, Cross-module Validation and Reference Discovery diagnostics. It supports filtering and stable-path navigation without parsing formatted report text. |
| `CharacterActionAuthoringSearchCatalog` | Internal Editor-only searchable catalog for Action, Intent, Ability, Reaction Profile, Character, Equipment and Tag references. Unknown serialized values are retained and labeled instead of being cleared when a provider is unavailable. |
| `CharacterActionPreviewViewport` / `CharacterActionPreviewRenderScene` | Internal Editor-only embedded viewport and isolated `PreviewRenderUtility` object host. Character, VFX and Projectile preview objects stay outside the user's active Scene. |
| `ICharacterActionPreviewObjectHost` / `CharacterActionPreviewSurfaceMode` | Internal preview-host boundary used to move the single playback/scrub state between the embedded viewport and SceneView exclusive-edit mode. |

The editor lives in `MxFramework.Editor` and may reference `UnityEditor`; `CharacterActionId` 位于 noEngine Core，`MxFramework.CharacterAction` 不引用 Unity API。工作台只显示一个只读“动作 ID”，搜索可按显示名或 `#301`；创建、复制、删除分别执行全局目录分配、重新分配与 retire 事务。

Runtime AI Lab Player builds project the selected action through the read-only `CharacterActionWorkstation`. In Unity Editor, the Lab keeps the current Game render frame and behavior context mounted, then replaces the lower behavior timeline and right Inspector in place with a FairyGUI Action Composition client backed by `CharacterActionAuthoringWorkspaceSession`; it does not open a second full-screen page. The Lab never owns a second persistence implementation and never writes Generated JSON directly. Unsaved current-character preview exports a complete temporary source set under `Temp/RuntimeAiLab/CharacterActionPreview`, reloads it through the normal CharacterTest importer, and restores formal configuration plus a frame-0 test restart on stop or exit. A successful contextual save preserves source-hash protection, runs the formal character-package import, reloads CharacterTest action configuration, and rebuilds the AI Lab test snapshot before reporting the change as active.

The document session tracks each source with a path-bearing document key, canonical JSON baseline, revision and dirty state. Selection, search, pane resizing and workspace switches do not enter Undo and do not dirty a document. Field changes are written immediately to the in-memory draft and participate in Undo/Redo; Profile membership changes are made only from “映射与方案 → Profile 组成”.

首次选择 Profile 内的 ActionSet 时，编辑器优先选择装备状态与角色 `DefaultLoadoutId` 解析结果匹配的方案（空装备状态表示使用默认装配），避免因列表顺序默认预览空手变体；无匹配时仍回退到首个可解析方案。用户显式选择与会话恢复的有效方案保持不变，此默认选择不修改任何配置文档。

The top Save command is Save All for the current session. It builds one validation context from all selected Profile members before writing. Errors block all writes and expand `Problems`; warnings permit saving. A successful preflight writes dirty Action Libraries, ActionSet/Reaction bundles, consumer-specific Mechanic Tuning, Profile data and Ability Effect Timelines in stable path order, followed by one `AssetDatabase.Refresh` and one validation/UI refresh. Projectile source/generated/binary/mirror data is explicitly excluded: CharacterAction stores only Request Stable ID and trigger frame. A single-file failure keeps that document dirty and reports its path; the editor does not claim cross-file atomicity. File-menu Save / Save As remains available per source.

“动作编排” aggregates actions from every Library in the Profile, reuses the existing frame timeline and shows the selected action or timeline item inspector on the right. “技能编排” uses the same three-pane timeline workbench for Ability Effect Timeline data and projects its Projectile / VFX / Gameplay / Audio events into the existing read-only preview chain. “映射与方案” owns Profile composition, ActionSet settings, command/ability bindings, authoritative `ReactionProfileId + CharacterReactionProfile`, discovery sources, and read-only legacy `reactionBindings[]`. “共享资源” retains consumer-specific Mechanic Tuning and read-only projectile references. Projectile Definition/Request/Presentation editing always deep-links to the independent Projectile Authoring writer.

The embedded `PreviewRenderUtility` surface is observation-only and shares playback, frame-step and scrub state with the existing controller. CharacterAction may project projectile paths and lifecycle presentation for observation, but owns no projectile range/radius/explosion/arc handles or write session; those controls live only in Projectile Authoring. Preview update/repaint is an Editor hot path and must remain no-alloc after initialization.

`BakedSocketCapsule` Inspector 的挂点候选来自当前 ActionSet 所属角色包的 `geometry/sockets.json`。隔离预览在动画采样后从已生成角色 Prefab 的同名 Socket 读取端点并绘制 Capsule；任一 Socket 无法解析时不绘制命中体，并输出包含动作、挂点和帧的直接诊断。

M5 adds cross-module authoring polish without transferring ownership:

- Animation action key helpers can collect keys from `MxAnimationClipRegistryAsset` bindings and expose them as picker choices.
- Combat action id helpers use the editor reference context plus optional `Character.Application` `CombatActionSetConfig` import helpers; Combat timelines and hitbox data remain owned by Combat.
- Presentation resource and audio cue helpers validate string keys through `ICharacterActionPresentationResourceProvider`; Resources still owns loading and catalog truth.
- Character package context helpers validate action-set character/equipment-state ids through `ICharacterActionCharacterEquipmentStateProvider`; Character.Application still owns package aggregation.
- Validation diagnostics are formatted into import, local validation, and cross-module reference groups. Cross-module errors block export through the same `CharacterActionValidationReport` path.

M6 hardens reference helpers into provider-backed discovery:

- “映射与方案 → 引用数据源” calls `CharacterActionReferenceDiscoveryService` instead of owning AssetDatabase scan logic directly.
- MxAnimation registry assets and ResourceCatalog JSON/assets can populate picker/reference context entries with source labels.
- Combat/Character.Application config objects can populate combat action ids, animation action keys, presentation keys, character stable ids, equipment-state ids, and equipment tags when provider data is available.
- Unavailable provider diagnostics are shown separately from `CharacterActionValidationReport`; manual CSV fallback remains active and export validation still uses the same `CharacterActionAuthoringReferenceContext`.

Character Action 的 `SpawnVisualCue` 编辑器不再把长 `ResourceKey` 下拉作为主交互。事件 Inspector 显示 VFX 资源卡，明确列出稳定 ResourceKey、职责、当前实际 Prefab 和“正式绑定/本机 Candidate”状态；“选择 VFX…”打开共享 VFX Catalog 浏览器，可搜索、筛选并动态预览 Published Effect 与有效 Candidate。由当前 ResourceKey 后缀推断的 Charge/Flight/Impact 职责只用于推荐、筛选和黄色提示，职责不匹配不会禁用正式资源；只有资源证据失效，或调用方提供的真实消费合同不兼容时才执行硬门禁。挂点与变换使用可被其它编辑器直接复用的 `MxAttachmentTransformField`：以“跟随挂点 / 跟随角色 / 固定世界”表达意图，按模式显示挂点选择、定位和校验，再用紧凑 XYZ 行编辑 Local/World 位置、欧拉旋转与支持统一轴的缩放。生命周期显示“事件帧段（N 帧）/自定义覆盖”，不要求使用者理解 `0` 的内部语义；结束策略和四个运行时参数槽仍是 VFX 专属区域。选择 Candidate 只写 Editor 本机试玩覆盖，选择正式资源只清除该槽位覆盖，最终动作 JSON 始终保存稳定 ResourceKey。高级折叠区仍允许诊断时直接查看或修改原始 Key。

动作时间轴的隔离预览优先通过 `IVfxUnityPrefabBindingSource` 解析当前 Editor 绑定，因此资源卡显示的“实际 Prefab”和时间轴播放使用同一结果。选择新 VFX 后编辑器会暂停时间轴、只清理旧表现实例、跳到事件触发帧并立即用最新绑定重播；已加载角色、Preview Scene 和镜头保持不变。未绑定技能槽位的草稿 Candidate 可投影到当前稳定 ResourceKey 作为本机预览覆盖；时长合同不匹配时显示“仅预览”警告并继续允许试玩，但 Production/Player 发布门禁仍阻断。Candidate receipt、Prefab GUID/content hash 失效时仍只可查看、不可选用，预览回退正式绑定并提示重新烘焙。

## CharacterTest Runtime JSON Source

`MxFramework.Game.CharacterTest` owns its runtime file-path policy; `MxFramework.CharacterAction` still only imports already-loaded JSON strings.

| Type | Purpose |
| --- | --- |
| `CharacterTestCharacterActionConfigSource` | CharacterTest runtime source facade. `Load()` scans project/generated candidate directories for a character ActionSet plus one or more Action Library JSON files, merges actions into a registry input, and falls back to the embedded sample only when no external Character Action JSON is present. |
| `CharacterTestCharacterActionConfigSourceOptions` | Optional runtime/test override for source candidate directories. |
| `CharacterTestCharacterActionConfigSourceKind` | Reports whether the active data came from `ExternalJson` or `EmbeddedFallback`. |
| `CharacterTestCharacterActionConfigSourceDiagnostic` | Stable source-level diagnostics for external load, incomplete external source, and embedded fallback selection. |
| `CharacterTestCharacterActionRuntime` source properties | Exposes `ConfigSourceKind`, `ConfigSourceDescription`, `ActionSetsSourcePath`, `ActionsSourcePath`, and source diagnostics for debug panels and tests. |

Invalid complete external JSON is not silently masked by embedded fallback: the runtime keeps `ExternalJson`, importer diagnostics remain visible, and `IsReady=false` until the exported files are fixed.

CharacterTest 的行为编排终止／替换入口在目标不处于 Alive 或 HP 已归零时拒绝操作，不得清除 Dying、Dead 或 Reviving 的生命周期动作；普通 Reaction 同样不能覆盖这些生命周期状态。死亡动作完成后的 terminal hold 由生命周期 owner 保持，复活／销毁等显式生命周期权限不受此普通编排门槛替代。



### CharacterTest projectile reference summaries

The tactical bound-action preparation path can measure a finite, single-request, constant-speed linear direct-hit projectile action, including published C006_1 Fireball 1090004 (request 422 / projectile 91001). It measures a reference hurtbox at 50 mm intervals over a bounded 0–50 m search and retains the normal closed-boundary and verified-hit-midpoint checks. This search extent is not a configured attack range. Unsupported producers, missing configuration, failure to start the real action, missing accepted projectile birth, or a projectile that outlives the bounded simulation produce an unavailable result. An explicit aim range exceeding the definition's declared travel horizon is unsupported. Melee retains its existing 0–5 m measurement.

Each projectile distance executes the actual action gateway, spawn/aim, projectile runtime and direct-hit resolver in a fresh isolated reference session with the reference capsule selected as its aim target. Projectile, health, cooldown and on-hit buff state are discarded between samples. This is low-frequency nominal configuration evidence, not a live attack-opportunity permission or a replacement for Stage ownership, binding, payment, eligibility or ACK. The live Runtime AI Planner BasicAttack opportunity evaluator is unchanged.

A projectile summary fingerprint includes the actual loaded spawn profile, projectile definition and selected source-package socket bake (or the production fallback choice), in addition to the existing action/source inputs. Queries without the required loaded projectile catalog fail closed. CharacterTacticalBoundCardInputs passes this catalog and chooses the projectile measurement before the existing transactional summary repair and retry. No generated content, action eligibility classification or player inventory is granted by this path. Runtime/sample validation requires Unity; offline configuration tests alone do not establish Stage-to-ACK-to-birth or delayed-damage acceptance.


### 弹体参考测距的角色碰撞代理

弹体实际采样子会话使用正式 DynamicCharacter（layer 8）目标 AABB，由既有角色 MotionSystem 同步生成。名义 layer 10 capsule 继续负责表面距离与伤害几何，不能替代弹体 Authoring 根据 targetTypes 生成的查询层。正式 Fireball 的 mask 256 保持不变。

仅弹体采样子会话启用目标 blocksCharacters，目标不可推挤，来源保持非阻挡；其运动查询只包括静态层，以保持二分定位的重叠姿态。普通 Runtime 构造与近战参考会话仍使用原行为。采样在实际网关执行前验证目标实体、body/collider 和 layer；排除角色层、缺失代理或层错误分别返回 ProjectileReferenceCharacterLayerExcluded、ProjectileReferenceTargetColliderMissing、ProjectileReferenceTargetColliderLayerMismatch，不能当作有效 miss。

弹体测量指纹的 executor 更新为 actual-projectile-reference-v2-production-target-body，旧弹体测量缓存需重测；近战指纹版本不变。是否命中仍取真实弹体出生后的 DirectSweep Damage 结果，不能由几何预测或人为 accepted 事件代替。修补交付须保留 1m/10m 命中、40m 不命中、独立样本无状态泄漏和完整 0–50m 扫描的原生验收；离线编译与物理查询测试不代表上述 Unity 验收已通过。
