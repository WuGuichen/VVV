# Character Action 接口

> 状态：v0 / Phase 1-7 代码已合并（8,391 行），未接入运行管线
> 模块：`MxFramework.CharacterAction` — noEngine（`noEngineReferences=true`）
> 依赖：`MxFramework.Combat`、`MxFramework.Gameplay`、`MxFramework.Runtime`

## 职责

`MxFramework.CharacterAction` 是角色动作的解析 → 计划 → 执行 → 轨道分发管线。它把玩家输入 / Runtime AI Planner 决策 / 受击事件统一解析成动作计划，再编排到 CharacterControl、Combat、Gameplay、MxAnimation、Audio、VFX、Camera。

当前不替代 `MxFramework.CharacterControl` 的 v0 `CharacterActionRequest` / `CharacterActionController` 路径。无 asmdef 引用 `MxFramework.CharacterAction`，因此本节所有类型在 Demo 和项目层中不可直接实例化——引用 `MxFramework.CharacterAction` 前必须先在 asmdef 中添加引用。

## 公开接口

### 配置定义

| 类型 | 用途 |
| --- | --- |
| `CharacterActionCategory` | 动作分类枚举：None、Idle、Movement、BasicAttack(10)、Skill(11)、Guard(12)、Dodge(13)、Jump(14)、Interaction(15)、Reaction(20)、PassiveOverlay(30) |
| `CharacterMovementMode` | 移动模式枚举：Idle、Walk、Run、Strafe、TurnInPlace、ApproachTarget、Retreat、CircleTarget、RootMotionDriven、Airborne、ControlLocked |
| `CharacterActionRequirementKind` | 动作准入条件枚举：Grounded、Airborne、EquipmentTagRequired、StatusRequired/Forbidden、ResourceAvailable、CooldownReady、TargetValid、PhaseAllowed |
| `CharacterActionSetConfig` | 角色级动作集合构造函数：`(int id, string stableId, string displayName, string characterStableId, string equipmentStateStableId, CharacterActionBinding[] commandBindings, CharacterAbilityActionBinding[] abilityBindings, CharacterReactionBinding[] reactionBindings, string movementProfileId, string reactionProfileId, string defaultActionId)` |
| `CharacterActionConfig` | 单个动作配置构造函数：`(int id, string stableId, string displayName, CharacterActionCategory category, CharacterActionTimelineAuthority timelineAuthority, string[] tags, int priority, int? durationFrames, CharacterActionRequirement[] requirements, CharacterActionPhase[] phases, CharacterCancelRule[] cancelRules, CharacterInterruptRule[] interruptRules, MotionTrackConfig motionTrack=null, CombatTrackConfig combatTrack=null, GameplayTrackConfig gameplayTrack=null, AnimationTrackConfig animationTrack=null, PresentationTrackConfig presentationTrack=null, DebugTrackConfig debugTrack=null)` |
| `CharacterMovementProfileConfig` | 移动配置：`(string stableId, CharacterMovementMode defaultMode, float walkSpeed, float runSpeed, float acceleration, float deceleration, float turnSpeed, float groundFriction, float airControl, float gravity, float jumpImpulse, float slopeLimitDegrees, string locomotionBlendId)` |
| `CharacterActionRequirement` | 准入条件：`(CharacterActionRequirementKind kind, string stableId="", int value=0)` |
| `CharacterActionBinding` | 命令绑定：`(string intentId, string actionId, int priority=0, bool allowQueue=false, int queueWindowFrames=0)` |
| `CharacterAbilityActionBinding` | Ability 绑定：`(int abilityId, string actionId, string[] requiredTags=null, string[] forbiddenTags=null)` |
| `CharacterReactionBinding` | 受击绑定：`(string reactionProfileId, string defaultActionId, string[] requiredTags=null, string[] forbiddenTags=null)` |

### Phase / Timeline Authority

| 类型 | 用途 |
| --- | --- |
| `CharacterActionPhaseKind` | Phase 类型枚举：None、Startup、Active、Recovery、Loop、Airborne、Landing、Channel、Hold、Exit、Custom(100) |
| `CharacterActionTimelineAuthority` | 时间轴权威枚举：CharacterAuthored(0)、CombatAnchored(1) |
| `CharacterActionPhase` | Phase 区间 struct：`(CharacterActionPhaseKind kind, int startFrame, int endFrame, CombatActionPhase combatPhaseAnchor=CombatActionPhase.None, bool requiresCombatPhaseMatch=true)` — 包含 `Contains(int localFrame)` |

### 取消 / 打断

| 类型 | 用途 |
| --- | --- |
| `CharacterActionSourceKind` | 动作来源枚举：Command(1)、GameplayAbility(2)、LocalInput(3)、RuntimeAiPlanner(4)、Replay(5)、Scripted(6)、PostureBreak(10)、GuardBreak(11)、ArmorBreak(12)、Hit(13)、Death(14)、Reaction(15)、PlayerIntervention(20)、Debug(30) |
| `CharacterCancelRule` | 取消规则 struct：`(int startFrame, int endFrame, int targetActionId=0, CharacterActionSourceKind sourceKind=Command, bool allow=true)` — 包含 `Matches(localFrame, targetActionId, sourceKind)` |
| `CharacterInterruptRule` | 打断规则 struct：`(CharacterActionSourceKind sourceKind, int minimumPriority=0, int targetActionId=0, bool allow=true)` — 包含 `Matches(sourceKind, priority, targetActionId)` |
| `CharacterCancelConflictClassifier` | 取消冲突分类器：`Classify(authority, characterRules, combatTimeline, localFrame, targetActionId, sourceKind) → CharacterCancelConflictResult` |
| `CharacterCancelRejectionAuthority` | 拒绝来源枚举：None、Character(1)、Combat(2) |
| `CharacterCancelConflictResult` | 取消冲突结果 struct：`Allowed(bool)、RejectedBy(CharacterCancelRejectionAuthority)、Code(string)` — 工厂方法 `Accepted()` / `Rejected(authority, code)` |

### 意图 / 解析 / 计划

| 类型 | 用途 |
| --- | --- |
| `CharacterActionIntentRequest` | 意图请求 struct：`(GameplayEntityId entity, string intentId, int? abilityId, string abilityStableId, string requestedActionId, CharacterActionSourceKind sourceKind, int priority, RuntimeFrame frame, string traceId)` |
| `CharacterActionResolveStatus` | 解析状态枚举：Success、Queued、Rejected |
| `CharacterActionRejectReason` | 拒绝原因枚举：30+ 原因（MissingActionSet、StateDisabled、CooldownActive 等） |
| `CharacterActionResolveResult` | 解析结果 sealed class：`Status(ResolveStatus)`、`RejectReason(RejectReason)`、`Plan(CharacterActionPlan)`、`Diagnostics`、`TraceId`、`IsSuccess`、`IsQueued`、`IsRejected` — 工厂方法 `Success(plan)` / `Queued(plan)` / `Rejected(reason)` |
| `CharacterActionCandidate` | 候选动作 struct：10 字段（ActionId、四层优先级、SourceOrder、StableTieBreaker、AllowQueue、QueueWindowFrames） |
| `CharacterActionPlan` | 动作计划 sealed class：`(long planId, string actionId, CharacterActionCategory category, int priority, int durationFrames, CharacterActionPhase[] phases, CharacterActionTrackPlan[] tracks, string traceId)` |
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
| `CharacterActionResolver` | 解析器 sealed class：`ResolveCommand(context, intentRequest) → ResolveResult`、`ResolveAbility(context, request) → ResolveResult`、`ResolveReaction(context, reactionContext) → ResolveResult` |

### Runner（执行器）

| 类型 | 用途 |
| --- | --- |
| `CharacterActionInstanceState` | 执行实例状态枚举：None、Running、Finished、Cancelled、Interrupted |
| `CharacterActionRunnerEventKind` | 运行器事件枚举：ActionStarted、PhaseChanged、TrackEventFired、ActionFinished、ActionCancelled、ActionInterrupted、CancelRejected、InterruptRejected |
| `CharacterActionTrackDispatchEvent` | 轨道派发事件 struct：16 字段（TrackKind、EventKind、Frame、StableEventId、MovementMode、X/Y/Z、CombatActionId、TraceProfileId、GameplayRequestId、AbilityStableId、AnimationActionKey、TransitionSeconds、PresentationCueId、ResourceKey、DebugMarkerId） |
| `CharacterActionRunnerEvent` | 运行器事件 struct：`(RunnerEventKind kind, long instanceId, long planId, string actionId, InstanceState state, int localFrame, PhaseKind previousPhase, PhaseKind currentPhase, TrackDispatchEvent trackDispatch, string diagnosticCode, string reason, string traceId)` — 包含 `ToReplayLine()` |
| `CharacterActionRunnerOperationResult` | 操作结果 struct：`Accepted(bool)`、`ActiveInstance(Instance)`、`Events(RunnerEvent[])`、`Diagnostics` |
| `CharacterActionTransitionRequest` | 过渡请求 struct：`(ResolveResult resolveResult, RunnerActionDefinition definition, SourceKind sourceKind, int priority=0, string traceId="")` |
| `CharacterActionRunnerActionDefinition` | 动作定义 sealed class：`(int actionConfigId, string actionId, TimelineAuthority timelineAuthority, CancelRule[] cancelRules, InterruptRule[] interruptRules, CombatActionTimeline combatTimeline, TrackDispatchEvent[] trackEvents)` |
| `CharacterActionInstance` | 运行中实例 sealed class：`InstanceId`、`Plan`、`Definition`、`State`、`LocalFrame`、`CurrentPhaseKind`、`IsRunning`、`FinishReason` |
| `CharacterActionDebugSnapshot` | 诊断快照 sealed class：`ActiveActionInstanceId`、`PlanId`、`ActionId`、`State`、`LocalFrame`、`CurrentPhase`、`DurationFrames`、`FinishReason`、`LastRejectReason`、`FiredEventsThisFrame` |
| `CharacterActionRunner` | 运行器 sealed class：`Start(ResolveResult, RunnerActionDefinition=null) → OperationResult`、`Start(CharacterActionPlan, RunnerActionDefinition) → OperationResult`、`Tick() → OperationResult`、`RequestCancel(TransitionRequest) → OperationResult`、`RequestInterrupt(TransitionRequest) → OperationResult`、`DrainEvents() → RunnerEvent[]`、`CreateDebugSnapshot() → DebugSnapshot` |

### Track Adapter（轨道适配层）

| 类型 | 用途 |
| --- | --- |
| `CharacterActionAdapterContext` | 适配器上下文：`(RuntimeFrame frame, GameplayEntityId gameplayEntityId, CombatEntityId combatEntityId, CombatBodyId combatBodyId, int sourceId=0, string traceId="")` |
| `CharacterActionAdapterDispatchMetadata` | 派发元数据 struct：`(RuntimeFrame frame, long instanceId, long planId, string actionId, int localFrame, string stableEventId, int sourceId, string traceId)` |
| `CharacterActionMotionRequest` | 运动请求 struct：`(AdapterDispatchMetadata metadata, GameplayEntityId gameplayEntityId, CombatBodyId combatBodyId, TrackEventKind eventKind, MovementMode movementMode, float x, float y, float z)` |
| `CharacterActionCombatRequest` | Combat 请求 struct：`(AdapterDispatchMetadata metadata, CombatEntityId combatEntityId, TrackEventKind eventKind, string combatActionId, string traceProfileId)` |
| `CharacterActionGameplayRequest` | Gameplay 请求 struct：`(AdapterDispatchMetadata metadata, GameplayEntityId gameplayEntityId, TrackEventKind eventKind, string requestId, string abilityStableId)` |
| `CharacterActionPresentationAdapterContext` | 表现适配器上下文：`(RuntimeFrame frame, string targetActorId, int sourceId=0, string traceId="", string animationLayerId="base")` |
| `CharacterActionAnimationRequest` | 动画请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, TrackEventKind eventKind, string actionKey, string layerId, float transitionSeconds)` |
| `CharacterActionAudioCueRequest` | 音频请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, string cueId)` |
| `CharacterActionVfxRequest` | VFX 请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, string resourceKey)` |
| `CharacterActionCameraRequest` | 相机请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, string requestId, string payloadKey)` |
| `CharacterActionUiFeedbackRequest` | UI 反馈请求 struct：`(AdapterDispatchMetadata metadata, string targetActorId, string feedbackId, string payloadKey)` |
| `CharacterActionPressureOnlyReactionRequest` | 仅压力反应请求 struct：`(CharacterReactionContext context, string requestedActionId="")` |

**Sink 接口**（组合根实现，注入到 Adapter）：

| 接口 | 方法 |
| --- | --- |
| `ICharacterActionMotionRequestSink` | `SubmitMotionRequest(MotionRequest) → AdapterSinkResult` |
| `ICharacterActionCombatRequestSink` | `SubmitCombatRequest(CombatRequest) → AdapterSinkResult` |
| `ICharacterActionGameplayRequestSink` | `SubmitGameplayRequest(GameplayRequest) → AdapterSinkResult` |
| `ICharacterActionPressureOnlyReactionRequestSink` | `SubmitPressureOnlyReactionRequest(PressureOnlyReactionRequest) → AdapterSinkResult` |
| `ICharacterActionAnimationRequestSink` | `SubmitAnimationRequest(AnimationRequest) → AdapterSinkResult` |
| `ICharacterActionAudioCueRequestSink` | `SubmitAudioCueRequest(AudioCueRequest) → AdapterSinkResult` |
| `ICharacterActionVfxRequestSink` | `SubmitVfxRequest(VfxRequest) → AdapterSinkResult` |
| `ICharacterActionCameraRequestSink` | `SubmitCameraRequest(CameraRequest) → AdapterSinkResult` |
| `ICharacterActionUiFeedbackRequestSink` | `SubmitUiFeedbackRequest(UiFeedbackRequest) → AdapterSinkResult` |

**适配器类**：

| 类 | 用途 |
| --- | --- |
| `CharacterActionAdapterRequestCollector` | 实现 Motion/Combat/Gameplay/PressureOnlyReaction 四个 sink，收集请求到只读列表 |
| `CharacterActionPresentationRequestCollector` | 实现 Animation/AudioCue/Vfx/Camera/UiFeedback 五个 sink，收集请求到只读列表 |
| `CharacterActionTrackAdapter` | 轨道适配器：`Adapt(RunnerEvent, AdapterContext) → AdapterResult`、`AdaptMany(RunnerEvent[], AdapterContext) → AdapterResult`、`SubmitPressureOnlyReaction(ReactionContext) → AdapterResult` |
| `CharacterActionPresentationTrackAdapter` | 表现轨道适配器：`Adapt(RunnerEvent, PresentationAdapterContext) → PresentationAdapterResult` |
| `CharacterActionAdapterSinkResult` | Sink 结果 struct：`Accepted(bool)`、`Diagnostics` — 工厂 `AcceptedResult()` / `Rejected(diagnostics)` |
| `CharacterActionAdapterResult` | 适配结果 struct：`MotionRequestCount`、`CombatRequestCount`、`GameplayRequestCount`、`PressureOnlyReactionRequestCount`、`Diagnostics`、`Accepted(bool)` |
| `CharacterActionPresentationAdapterResult` | 表现适配结果 struct：`AnimationRequestCount`、`AudioCueRequestCount`、`VfxRequestCount`、`CameraRequestCount`、`UiFeedbackRequestCount`、`Diagnostics` |

### 轨道配置与事件（Tracks）

| 类型 | 用途 |
| --- | --- |
| `CharacterActionTrackKind` | 轨道类型枚举：Motion(0)、Combat(1)、Gameplay(2)、Animation(3)、Presentation(4)、Debug(5) |
| `CharacterActionTrackEventKind` | 轨道事件类型枚举：SetMovementMode(1)、ApplyImpulse(2)、LockMovement(3)、StartCombatAction(100)、StartHitTrace(101)、StopHitTrace(102)、SendGameplayRequest(200)、CastAbility(201)、ApplyGameplayEffect(202)、PlayAnimation(300)、CrossFadeAnimation(301)、SetAnimationBlend(302)、PlayAudioCue(400)、SpawnVisualCue(401)、CameraImpulse(402)、UiFeedback(403)、EmitDebugMarker(500) |

轨道配置（`MotionTrackConfig` / `CombatTrackConfig` / `GameplayTrackConfig` / `AnimationTrackConfig` / `PresentationTrackConfig` / `DebugTrackConfig`）——每个包含对应事件数组，静态 `Empty` 实例。

轨道事件（`MotionTrackEvent` / `CombatTrackEvent` / `GameplayTrackEvent` / `AnimationTrackEvent` / `PresentationTrackEvent` / `DebugTrackEvent`）——每个包含 `int Frame`、`TrackEventKind Kind`、`string StableEventId` 和轨道专有字段（MovementMode、CombatActionId、RequestId 等）。

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
| `CharacterActionDiagnosticFormatContext` | 诊断格式化上下文：`(string actionId="", PhaseKind phaseKind=None, TrackKind trackKind=Motion, bool hasTrack=false, int frame=-1, string suggestedFix="")` |
| `CharacterActionDiagnosticFormatter` | 诊断格式化器：`Format(diagnostic, context=default) → string`、`FormatMany(diagnostics, contexts=null) → string[]` |

### 受击反应

| 类型 | 用途 |
| --- | --- |
| `CharacterReactionContextSourceKind` | 反应上下文来源枚举：Unknown、PostureBreak、GuardBreak、ArmorBreak、PressureBandChanged、Death、Lifecycle、Hit |
| `CharacterReactionContextCompleteness` | 上下文完整度枚举：None、SourceOnly、PressureOnly、Full |
| `CharacterHitDirection` | 受击方向枚举：Unknown、Front、Back、Left、Right、Up、Down |
| `CharacterReactionContext` | 反应上下文 struct：`(SourceKind, Completeness, RuntimeFrame, GameplayEntityId, PreviousPressureBand, CurrentPressureBand, PreviousPressure, CurrentPressure, MaxPressure, Delta, SourceId, Reason, TraceId, bool isDeath, LifecycleState, BodyPartId, HitZoneId, DamageTypeId, HitDirection, ReactionGroupId, int impactForce=0, bool isAirborne=false, CurrentActionId, CurrentCharacterPhase, CurrentCombatPhase, CurrentActionCommitted, CurrentActionInterruptible)` |
| `CharacterReactionHitSource` | 受击数据源 struct：`(RuntimeFrame frame, GameplayEntityId entityId, string bodyPartId="", string hitZoneId="", string damageTypeId="", CharacterHitDirection hitDirection=Unknown, int? impactForce=null, string reactionGroupId="", int sourceId=0, string reason="", string traceId="", PressureBand previousPressureBand=Stable, PressureBand currentPressureBand=Stable, int previousPressure=0, int currentPressure=0, int maxPressure=0, int delta=0, bool isDeath=false, string lifecycleState="", bool isAirborne=false, string currentActionId="", CharacterActionPhaseKind currentCharacterPhase=None, CombatActionPhase currentCombatPhase=None, bool currentActionCommitted=false, bool currentActionInterruptible=true)` |
| `CharacterReactionContextBuildResult` | 构建结果 struct：`Context(ReactionContext)`、`Completeness`、`Diagnostics`、`Success(bool)` |
| `CharacterReactionContextBuilder` | 上下文构建器：`FromHitSource(CharacterReactionHitSource) → BuildResult`、`FromPostureBreak(PostureBreakEvent) → BuildResult`、`FromGuardBreak(GuardBreakEvent) → BuildResult` |
| `CharacterReactionRuleTrigger` | 反应规则触发器枚举：Any、PostureBreak、GuardBreak、ArmorBreak、PressureBandChanged、Death、Lifecycle、Hit |
| `CharacterReactionRule` | 反应规则 sealed class：`(string actionId, CharacterReactionRuleTrigger trigger, bool requiresBodyPart=false, bool requiresHitZone=false, bool requiresHitDirection=false, bool requiresDamageType=false, bool requiresReactionGroup=false, PressureBand? currentPressureBand=null, bool? isDeath=null, bool? isAirborne=null, CharacterActionPhaseKind? currentPhase=null, bool? currentActionCommitted=null, bool? currentActionInterruptible=null, int priority=0, string bodyPartId="", string hitZoneId="", string damageTypeId="", CharacterHitDirection? hitDirection=null, int? minImpactForce=null, int? maxImpactForce=null, string reactionGroupId="", bool requiresImpactForce=false)` |
| `CharacterReactionProfile` | 反应配置 sealed class：`(string stableId, CharacterReactionRule[] rules, string defaultActionId="")` |
| `CharacterReactionSelectionResult` | 选择结果 struct：`Accepted(bool)`、`SelectedActionId(string)`、`RejectCode(string)`、`Diagnostics` |
| `CharacterReactionSelector` | 反应选择器 static class：`Select(CharacterReactionProfile, CharacterReactionContext) → SelectionResult` |
| `CharacterReactionRuleValidator` | 反应规则校验器：`ValidateProfileForCompleteness(profile, completeness) → Diagnostic[]` |

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

## 程序集边界

```text
MxFramework.CharacterAction
  -> MxFramework.Combat
  -> MxFramework.Gameplay
  -> MxFramework.Runtime
```

`MxFramework.CharacterAction` 当前无外部引用（仅测试程序集引用）。

## 使用约定

- 所有配置类使用构造函数（无 parameterless constructor，属性 get-only），不支持 object initializer。
- `CharacterActionConfig.DurationFrames` 和 `Phases` 必须覆盖完整动作时长。`CharacterActionPhaseValidator` 要求 phases 无间隙、无重叠，且最后一个 phase 的 endFrame 等于 `DurationFrames - 1`。
- `CharacterActionResolver.ResolveCommand()` 通过 `CharacterActionBinding.IntentId` 匹配 `CharacterActionIntentRequest.IntentId`，再通过 `binding.ActionId` 与 `CharacterActionConfig.StableId` 查找对应动作配置。
- `CharacterActionRunner.Start()` 在已有 active instance 时拒绝；必须先 `RequestCancel` 或 `RequestInterrupt`。
- `Tick()` 无参数，自动递增 `localFrame`。
- `CharacterReactionContextBuilder.FromHitSource()` 要求 6 个字段齐全（BodyPartId、HitZoneId、DamageTypeId、HitDirection ≠ Unknown、ImpactForce 有值、ReactionGroupId 非空）才返回 `Completeness=Full`；不完整的 Hit 上下文会被 `CharacterReactionSelector.Select()` 直接拒绝。
- `CharacterActionWorkstation` 是只读诊断工具，不支持编辑写回。

## 测试入口

`Assets/Scripts/MxFramework/Tests/CharacterAction/`（10 个测试文件）

## 当前约束

- 无 asmdef 引用 `MxFramework.CharacterAction`；Demo / 组合根需手动添加引用。
- `CharacterControl` 仍使用旧 v0 `CharacterActionRequest` / `CharacterActionController` 路径，未接入新 Action 层。
- 无 Playable 角色 Demo。
- 未提供 `CharacterAction` 的 Unity Editor 支持。
