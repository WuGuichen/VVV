# Character Control 接口

> Status: Current
>
> 状态：v0.7 contract + command sources / motion modifier adapters / pressure state / animation presentation / Debug UI + CharacterAction 单一执行权威
> 任务入口：`Docs/Tasks/CHARACTER_CONTROL_RUNTIME_00_DESIGN_CONTRACT.md`

## 职责

`MxFramework.CharacterControl` 是 noEngine 角色控制编排层。它把本地输入、Runtime AI Planner、Replay/Test source 或 UI 命令统一成 `CharacterCommand`，再驱动控制状态机、Combat Motion 和正式 CharacterAction。CombatAction 与 Gameplay command 只允许作为 CharacterAction 轨道的下游结果。

它不读取 Unity 输入、不调用 Unity Physics、不拥有 HP/Buff/Ability source of truth，也不让 MxAnimation / Animator / Playables root motion 反向驱动权威状态。

## 程序集边界

```text
MxFramework.CharacterControl
  -> MxFramework.Core
  -> MxFramework.Runtime
  -> MxFramework.Combat
  -> MxFramework.Gameplay
  -> MxFramework.CharacterAction

MxFramework.CharacterControl.Input
  -> MxFramework.CharacterControl
  -> MxFramework.Input

MxFramework.CharacterControl.RuntimeAiPlannerBridge
  -> MxFramework.CharacterControl
  -> MxFramework.AI

MxFramework.CharacterControl.Animation
  -> MxFramework.CharacterControl
  -> MxFramework.Animation
  -> MxFramework.Combat
  -> MxFramework.Resources

MxFramework.DebugUI.Adapters
  -> MxFramework.CharacterControl
  -> MxFramework.DebugUI
  -> MxFramework.Diagnostics
```

`MxFramework.CharacterControl` core 禁止引用：

- `UnityEngine` / `UnityEditor`
- `MxFramework.Input`
- `MxFramework.UI.Toolkit`
- `MxFramework.Animation.Unity`
- Demo / Editor / WGame 私有命名空间

## 公开接口

| 类型 | 用途 |
| --- | --- |
| `CharacterControlEntityRef` | 角色在 CharacterControl / Gameplay / Combat 之间的稳定映射 DTO。 |
| `CharacterCommand` | 每帧角色控制命令，包含 frame、source、move、facing basis、jump、sprint、action buttons、action request 和 trace id。 |
| `CharacterFacingBasis` | 已量化的 camera / facing basis，避免 CharacterControl 依赖 Unity camera 或浮点三角函数。 |
| `ICharacterCommandSource` | Local Input、Runtime AI Planner、Replay/Test source 输出 `CharacterCommand` 的统一入口。 |
| `CharacterActionRequest` | attack / skill / interact / dodge / gameplay / reaction 动作请求 DTO；`ActionIntentId` 可显式映射到 CharacterAction command binding。不存在通用 Cancel 动作。 |
| `CharacterControlState` | `Locomotion`、`Action`、`Reaction`、`Disabled`。 |
| `CharacterControlLockMask` | Move / Jump / Action / Facing 控制锁。 |
| `CharacterControlStateMachine` | 状态转换、版本、last command frame、lock mask 和事件输出。 |
| `CharacterStateChangedEvent` | 状态改变事件 payload。 |
| `CharacterControlEvent` | 状态拒绝、锁变化等通用诊断事件 payload。 |
| `CharacterMotionSettings` | 状态/冲刺/外部 modifier 的移动倍率配置。 |
| `CharacterMotionModifier` / `ICharacterMotionModifierProvider` | Gameplay / Buff / Attribute / Environment adapter 输出移动倍率 modifier。 |
| `CharacterMotionModifierAggregator` / `CharacterMotionModifierResult` | 按 priority、source、reason 稳定排序并合成最终 `MoveSpeedScale`。 |
| `CharacterMotionResolver` | `CharacterCommand` + control state -> `CombatMotionInput` -> `CombatKinematicMotor.Step(...)`。 |
| `CharacterMotionResult` | 运动结果摘要，暴露 position、velocity、grounded、collision flags、applied delta 和 world sync 信息。 |
| `CharacterMotionRequestKind` / `CharacterMotionRequestSpace` / `CharacterMotionRequest` | noEngine 运动意图 DTO，用整数毫米、毫米/秒、毫米/秒²、控制倍率和 source id 表达 movement mode、impulse、root motion delta、velocity override、horizontal/vertical velocity override、ignore gravity、movement lock、external force 和 control scale。 |
| `CharacterMotionRequestFrame` / `CharacterMotionRequestAccumulator` | 将每帧多个运动请求合成为单个 request frame，并提供 control scale fallback 与 root motion 后持久速度解析。 |
| `ICharacterMotionLocalSpaceResolver` | 上层 adapter 注入 local -> world 转换；CharacterControl 不引用 Unity camera、Transform 或 CharacterTest yaw 实现。 |
| `CharacterActionMotionRequestAccumulatorSink` | `ICharacterActionMotionRequestSink` / `ICharacterActionRootMotionRequestSink` 默认实现，把 CharacterAction Motion track 请求和 action-authored root motion samples 转换为 `CharacterMotionRequestAccumulator` 输入；支持 movement mode、impulse、root motion delta、lock、control scale / reset 和 local-space resolver。 |
| `CharacterKinematicMotionProfile` | 每帧运动学求解所需的 profile 数值快照，使用 stable movement mode id 和整数毫米单位，不依赖 CharacterTest runtime profile 类型。 |
| `CharacterMotionFacingInput` / `ICharacterMotionFacingResolver` | 上层 adapter 注入 locked target、当前 yaw、命令来源和朝向向量数据；默认 resolver 只做 noEngine target yaw 选择。 |
| `CharacterKinematicMotionInput` | 运动学求解输入 DTO，包含当前速度、输入方向、request frame、grounded、surface material、tickRate、jump/coyote/buffer 和 action control scale 快照。 |
| `CharacterKinematicMotionPrePhysicsResult` | 物理步进前输出，包含 target/desired velocity、horizontal/vertical velocity、root motion velocity、jump/coyote/buffer 更新、control scale、movement lock/mode 和 surface traction 标记。 |
| `CharacterKinematicMotionFinalResult` | 物理步进后输出，包含最终 horizontal/vertical velocity、yaw、blend axis、jump/coyote/buffer、movement mode 和 surface bounce cap。 |
| `CharacterKinematicMotionSolver` | noEngine 纯运动学 solver，负责目标速度、水平加减速、跳跃/重力、root motion 速度合成、yaw 和 blend 计算。 |
| `CharacterKinematicPhysicsStepInput` / `CharacterKinematicPhysicsStepResult` | Combat physics step adapter DTO，用整数毫米、`Fix64` center offset、Combat body/entity id、root motion/persistent velocity及可选`WallSlideMinimumHorizontalSpeedMillimetersPerSecond`描述一次物理步进；旧构造器保持wall-slide floor=`0`。 |
| `CharacterKinematicPhysicsAdapter` | noEngine physics adapter，构造 `CombatMotionState` / `CombatMotionInput` / impact velocity，调用 `CombatKinematicMotor.Step(...)` 或其候选缓存重载，并把结果转回毫米单位。 |
| `ICharacterActionConstraint` | 冷却、资源、状态等项目/Gameplay 限制的只读检查扩展点。 |
| `CharacterActionControllerOptions` | 正式 CharacterAction 配置，提供 `ActionSet`、动作配置、reaction profile、Combat timeline binding、duration policy、context tags、track/presentation adapters、下游生命周期 sink 和 presentation context。 |
| `CharacterActionController` | 正式 CharacterAction facade；Request Coordinator 独占请求校验、Resolver、计划缓存和 Gateway，Dispatch Coordinator 独占 Track、RootMotion、Presentation 与终态派发。Controller 不拥有 CombatActionRunner 或 Gameplay command store。 |
| `CharacterActionCombatTrackBridge` | 只执行 CharacterAction Combat Track 指定的旧 CombatAction 子时间线；外层实例结束时清理子动作，不反向写外层生命周期。 |
| `CharacterActionGameplayTrackBridge` | 只把 Gameplay Track 转为共享 Runtime command，不参与动作解析或生命周期。 |
| `CharacterActionEvent` | accepted / rejected / queued / started / command enqueued / finished / canceled 事件 payload。 |
| `CharacterActionResult` | 动作请求处理结果，包含 stable rejection reason、action instance id 和 accepted runtime command。 |
| `CharacterPressureReactionPolicy` | Gameplay pressure typed events 到 Character Control Reaction 的可配置策略。 |
| `CharacterPressureReactionController` | 消费 `CohesionBreakEvent` / `GuardBreakEvent` / `ArmorBreakEvent` / `PressureBandChangedEvent`，验证 Gameplay id 映射并维护独立控制状态；不启动、取消或清理 CharacterAction。 |
| `CharacterPressureReactionResult` | pressure state 处理结果，包含 reaction kind、end frame、lock mask、transition result 和 stable rejection reason。 |
| `CharacterPressureReactionEvent` | recorded / reaction started / reaction finished / rejected 诊断事件 payload。 |
| `InputCharacterCommandSource` | 可选 Input adapter，把 `IInputProvider` snapshot / command queue 转成 `CharacterCommand`。 |
| `CharacterInputActionBinding` | Input intent 到具体 CharacterAction / Gameplay ability 的显式绑定；Cancel 键位如需“返回移动”，必须绑定具体目标动作并接受切换窗口判定。 |
| `RuntimeAiPlannerCharacterCommandSource` | Runtime AI Planner bridge，把 plan selected action profile 转成 `CharacterCommand`。 |
| `RuntimeAiCharacterCommandProfile` / `RuntimeAiCharacterCommandProfileRegistry` | Runtime AI Planner action id 到 move/action request 的稳定映射。 |
| `RuntimeAiPlannerCharacterCommandDiagnostics` | last goal、selected action、last command、suppressed reason 诊断。 |
| `CharacterAnimationPresentationController` | 可选 MxAnimation presentation adapter，把 locomotion、reaction 和 action lifecycle 事件转成动画请求或只读诊断。 |
| `CharacterAnimationPresentationOptions` | 配置 actor id、1D / 2D locomotion blend id、参数 id、量化 scale、airborne locomotion 策略和 reaction binding。 |
| `CharacterAnimationReactionBinding` | `CharacterControlTransitionReason` 到 `Play` / `CrossFade` 请求的表现层绑定，clip 只保存 `ResourceKey`。 |
| `CharacterAnimationPresentationDiagnosticSnapshot` | 记录 last request、backend result code、backend clip/resource error、missing binding、fallback / skipped reason 和最近诊断列表。 |
| `CharacterControlDebugSource` | Phase 13 Debug UI adapter，把状态、last command、motion、action、pressure band 和 recent events 输出为只读 `FrameworkDebugSnapshot`。 |

## 使用约定

- 同一个 `RuntimeCommandBuffer` 仍只能由 GameplayRuntimeModule 等现有 owner drain；只有显式注入的 `CharacterActionGameplayTrackBridge` enqueue，Controller 本身不持有 command buffer。
- `CharacterMotionResolver` 必须通过 `CombatKinematicMotor` 得到权威运动结果。
- motion modifier 只影响 `CombatMotionInput.MoveSpeedScale`；provider 不能写 Gameplay / Combat source of truth。
- `CharacterMotionRequestAccumulator` 只聚合运动意图，不拥有角色实体、世界、碰撞体、接触压力、Unity view 或 trace 输出。CharacterTest 这类 adapter 负责把项目实体 request facade 转换为 `CharacterMotionRequest`，并把 request frame 应用到自己的 runtime state。
- `CharacterActionMotionRequestAccumulatorSink` 是 CharacterControl 内置的默认 motion/root-motion sink：`SetMovementMode` 转为 `CharacterMotionRequestKind.MovementMode`，`ApplyImpulse` 转为毫米/秒 impulse，action-authored root motion sample 转为 `RootMotionDelta` + `RootMotionDriven` movement mode，`LockMovement` 转为 active movement scale `0` / rotation scale `1`，`SetControlScale` / `ResetControlScale` 只改 active-control scale，不套用 legacy `movementMode` 字段。
- CharacterTest 普通受击通过正式 Reaction action 的 `SetControlScale` 窗口阻断玩家和 Runtime AI Planner 的主动移动/转向；HitStun 计数、last-hit frame 和击退速度方向不得作为 MotionSystem 的隐藏权限规则。Control scale 为零时，Combat impulse、外力、碰撞、摩擦和重力仍按权威运动链执行。
- `CharacterMotionRequest.MovementModeId` 是上层稳定 movement mode id；CharacterControl core 虽可选引用 `MxFramework.CharacterAction` 进行动作桥接，但 motion request 仍不依赖 `CharacterMovementMode`。CharacterTest 继续在自己的 facade 中暴露 `CharacterMovementMode`。
- `ICharacterMotionLocalSpaceResolver.ResolveLocalToWorld(...)`、`CharacterMotionRequestAccumulator.Accumulate(...)`、`CharacterActionMotionRequestAccumulatorSink.SubmitMotionRequest(...)` 和 `SubmitRootMotionRequest(...)` 的 accepted path 为高频路径，正常执行 `NoAlloc after initialization`；local-space request 缺少 resolver 属于 rejected diagnostic path。
- Root motion delta 可以参与当帧位移，但 `CharacterMotionRequestAccumulator.ResolvePersistent*AfterRootMotion(...)` 保持“root-motion-only 不变成持久速度”的规则，避免 Animator / Playables 表现反写权威运动。
- `HorizontalVelocityOverride` 只替换当帧水平速度并保留垂直速度；它同时压住该帧的水平 root motion，用于受击击退等必须独立于命中前主动推进速度的确定响应。普通 `Impulse` 仍保持加法冲量语义。
- `VerticalVelocityOverride` 只替换当帧垂直速度，不覆盖水平速度；`IgnoreGravity` 只跳过当帧重力加速度，不清空已有垂直速度。CharacterAction 的有状态区间逐帧发出这些请求，区间结束后下一帧自动恢复普通重力。
- 正向 Y RootMotion 逐帧位移读取移动配置已缓存的 `UpwardMotionGravityScaleMilli`；该帧不再叠加独立的向下重力速度。倍率始终在 `0.01..1`，可通过配置 `upwardGravityReference` 调整重力衰减曲线。负向 Y 位移与不含正向 Y RootMotion 的帧继续使用现有重力结算。
- `CharacterKinematicMotionSolver.SolvePrePhysics(...)` 和 `CompleteAfterPhysics(...)` 为高频路径，正常执行 `NoAlloc after initialization`；输入/输出 DTO 使用整数毫米、毫米/秒、毫米/秒²、stable enum/id 和 `CombatSurfaceMaterial`，不引用 `UnityEngine` 或 CharacterTest 类型，也不消费 CharacterAction runtime state。
- `CharacterKinematicMotionSolver` 只求解运动学数值和状态候选：target velocity、水平加减速、jump buffer / coyote 更新、jump impulse / release cut、gravity multiplier、surface traction acceleration cap、root motion 速度合成、yaw target/turn speed 和 locomotion blend。
- `CharacterKinematicMotionSolver` 不拥有 physics world、collider sync、contact pressure、overlap、dynamic contact hit、trace 文案、Unity view、entity 读写或 Combat authority；CharacterTest / game adapter 必须在 physics/contact 顺序不变的前提下把 solver 结果写回自己的实体。
- `ICharacterMotionFacingResolver.TryResolveTargetYaw(...)` 必须保持 noEngine 和无分配；需要 locked target 或项目特化 facing 规则时，由上层 adapter 将稳定整数数据填入 `CharacterMotionFacingInput`。
- Facing 使用 locked target、显式转向、主动移动输入或 `DesiredVelocity` 主动运动状态；`ContactVelocity`、压力推出、冲量等外部运动只参与位移/速度合成，即使写回持久物理速度也不得在后续 tick 反向改变角色 yaw。需要外部动作同时转向时，调用方必须显式提供 facing/turn 语义。
- `CharacterKinematicPhysicsAdapter.Step(...)` 为高频路径，正常执行 `NoAlloc after initialization`；调用方负责复用 `CombatMotionStepBuffer`，并负责创建/缓存 `CombatKinematicMotor` 和 `CombatPhysicsWorld`。
- `CharacterKinematicPhysicsAdapter.Step(..., CombatKinematicMotorCandidateCache)` 是 additive 缓存重载；调用方负责在 collider sync 后、逐角色 step 前准备缓存，并保证缓存 mask 覆盖 motor obstacle mask。body-only 变化可由缓存通过 Combat moved-body snapshot 增量更新；如果该 snapshot 已被普通 world query 消费，缓存会 rebuild 或由底层 motor 回退旧 world broadphase 路径，保持结果不变。adapter 不改变 DTO 转换、body sync、root motion persistent velocity、step-up grounded 修正或 surface material 解析。
- `CharacterKinematicPhysicsAdapter` 只适配 Combat motion step：body upsert、motion state/input/impact velocity 构造、ground contact probe、root motion 后 persistent velocity、step-up grounded 修正、surface material/friction/bounce cap 解析。它不拥有 collider shape sync、dynamic contact hit、contact pressure、overlap、trace 文案、Unity view 或 CharacterTest entity。
- `CharacterKinematicPhysicsStepInput` 使用毫米和毫米/秒整数表示位置/速度；`CenterOffsetY` 保持 `Fix64`，避免调用方在 Combat capsule center 与实体 transform 之间重复取整。
- `CharacterKinematicPhysicsStepInput.WallSlideMinimumHorizontalSpeedMillimetersPerSecond`是显式的主动locomotion墙滑下限；adapter只做mm/s到Fix64 m/s转换并写入`CombatMotionStepOptions`，不自行判断输入来源。旧构造器/default保持`0`，因此Root Motion、Impulse和external motion不会因API升级自动获得补速。
- CharacterTest组合根只在存在active move input，且本帧没有Root Motion、velocity/horizontal override、external acceleration、Impulse、external-velocity保留或超过既有1mm/s epsilon的有效contact velocity时提供正wall-slide floor；值为`min(simulated speed, target speed)`，因此不越过本帧加速结果或locomotion target。特殊motion结束后的后续普通输入帧可以重新接管主动墙滑。
- 未提供有效 `CharacterActionControllerOptions` 时动作请求返回 `MissingCharacterActionRuntime`；不再回退到旧 CombatAction 或 Gameplay 直执行路径。
- `Submit(...)` 把 `CharacterActionRequest` 解析为 `CharacterActionIntentRequest`，经 Resolver / 计划缓存后交给 Gateway 原子启动或切换；`TickCharacterAction(RuntimeFrame)` 推进同一 Gateway，并使用 track 事件的实际派发 frame 构造下游请求。
- `CharacterActionRequest.ActionIntentId` 为空时使用默认 intent：`Attack`、`Skill`、`Interact`、`Dodge`、`GameplayAbility`；需要连段、不同普攻段或项目自定义输入时由组合根显式写入 intent id。
- 组合根通过 `CharacterActionTrackAdapter` / `CharacterActionPresentationTrackAdapter` 注入 Motion、root motion、Combat、Gameplay、Animation、Presentation 与 Debug sink。需要旧 CombatAction 子时间线时显式注入 `CharacterActionCombatTrackBridge`；需要 Gameplay command 时显式注入 `CharacterActionGameplayTrackBridge`。
- Combat child 的 started / finished / canceled 不驱动 CharacterControl 生命周期；外层 CharacterAction 是唯一生命周期权威，外层终止会通过 downstream lifecycle sink 清理子动作。
- `CharacterActionControllerOptions` 属于 LowFreqAlloc/配置期 API；Gateway、Reaction、Track 与 Presentation 均使用调用方复用缓冲，初始化/扩容后稳定 Tick、成功及常规拒绝路径遵守 NoAlloc。
- `CharacterPressureReactionController` 只消费 Gameplay typed pressure events，不直接写 scatter pressure / guard / armor / HP / Buff / Ability 状态。
- `CohesionBreakEvent`、`GuardBreakEvent` 和 `ArmorBreakEvent` 在 `CharacterPressureReactionController` 中只更新独立控制状态；是否切换正式动作由 `CharacterActionReactionBridge` 单独提交，并由 Gateway 的 `interruptRules` 决定。拒绝不会清理原动作。
- `PressureBandChangedEvent` 默认只记录 band 变化，避免和 break typed event 重复触发；需要 broken band 直接触发时必须显式打开 `BrokenBandChangeStartsReaction`，且事件必须是 pressure 上升导致的 band 升级。
- recovery、negative delta 或 band 回落不会刷新 active reaction window；组合根停用 pressure owner 时可调用 `FinishActiveReaction(...)` 主动释放控制锁。
- pressure reaction 会校验 `CharacterControlEntityRef.GameplayEntityId`；缺失映射或 entity mismatch 只输出 rejected result/event，不抛异常。
- Gameplay ability 只通过 Gameplay Track → `CharacterActionGameplayTrackBridge` → `GameplayRuntimeCommandFactory` 生成 command，不直接写 Gameplay component store。
- cooldown / resource / status 限制通过 `ICharacterActionConstraint` 注入；CharacterControl 不内置具体属性 id、cost 或 status id。
- Input adapter 只读 `IInputProvider`，不直接读设备 API；Gameplay context 不可用时不输出 command，并丢弃当前 frame 及以前的 queued commands，避免 UI / cutscene 期间的动作在恢复后补发。
- Runtime AI Planner bridge 使用 `Runtime AI Planner` 公共接口和 pressure fact keys，不使用 AIAction Config 或 WGame 私有行为数据。
- Runtime AI Planner profile 的 `ActionRequest` 是 selection-edge one-shot；缓存复用、平滑复用和同 action 后续决策只继续输出移动、朝向、jump / sprint。
- `RuntimeAiCharacterCommandProfile` 未指定 `moveSpeedScale` 时默认 `1`；显式传入 `Fix64.Zero` 是合法配置，可用于站定施法、原地防御或停步等待。
- `MxFramework.CharacterControl.Animation` 是 noEngine adapter，只依赖 `IMxAnimationBackend` DTO；Unity Playables backend 仍由组合根注入。
- locomotion 表现只消费 `CharacterMotionResult`，默认只在 grounded 时输出非零 1D speed 或 2D direction blend 参数；1D speed 使用 Combat 同款水平输入 clamp 后的幅度乘 `MoveSpeedScale`；root motion、Animator time 和 Playable state 不能反写 Character Control / Combat。
- action lifecycle 不在 Character Control animation adapter 内再次订阅 Combat 事件；Combat action started / finished / canceled 表现仍由现有 `CombatMxAnimationUnityBridge` 负责，accepted / queued / rejected / gameplay command enqueued 只记录 skipped diagnostics，避免同一 Combat event 被双重桥接。
- reaction 表现按 `CharacterControlTransitionReason` 查找 binding 并输出 `Play` / `CrossFade`；缺失 binding 或 backend reject 只进入 diagnostics，不改变控制权威状态。
- `CharacterControlDebugSource` 位于 `MxFramework.DebugUI.Adapters`，不让 CharacterControl core 反向依赖 Debug UI；source 只订阅公开事件，command / motion 由组合根显式记录，组合根负责在角色换绑或销毁时 `Dispose()`。
- UI Toolkit、Audio、VFX、MxAnimation 和 debug overlay 只能消费事件 / snapshot。

## 测试入口

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterControl/
```

## CharacterTest共用落点与停止重定位前置（2026-10-08）

项目内部 `CharacterTestCharacterPlacementSafety.TryValidateGroundPlacement/HasFullBodyClearance` 位于 `CharacterTestPositionRecallSafety.cs`，接收完整目标尺寸，查询侧面/顶部/角落并排除自己的body，使用caller-owned scratch；没有新通用package API。`CharacterTestCharacterMotionSystem.TryCommitStoppedGroundRelocation` 在刷新角色/可动障碍碰撞后复查落点，提交权威位置、ground/material、速度/contact/motion request及collider/cache/旧Hurtbox清理，保留yaw。

该Motion入口不选择卡或回合，不授予受击中断权限；调用方仍负责动作/外力策略、机制收据与当前受击阶段的即时姿态/几何重建。31项基础验证范围及原复合compile/ready/console回执缺口见[任务09](../../StorySource/玩法基石/任务/任务09_替身准备与体型切换基础.md)，不等于完整替身或体型已实现。

## 只读运动查询（2026-10-07）

`CharacterKinematicPhysicsAdapter.Query(motor, world, stepBuffer, in input, candidateCache)` 与 `Step` 使用相同的确定性求解，但不 Upsert actor body，也不把结果写回物理世界。调用者保留预测结果，再用真实输入执行一次 `Step`。motor/candidate cache/stepBuffer 是共享查询scratch；调用者应先拷贝所需结果，不把预测 contact 当成已发生事件。该 API 为 noEngine，不持有项目层牵引状态。聚焦回归验证 Query 不移动 body、与紧接的真实 Step 一致、程序位移不覆盖持久速度。
