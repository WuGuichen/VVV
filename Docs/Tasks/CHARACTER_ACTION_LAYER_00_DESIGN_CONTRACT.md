# Character Action Layer 00：角色动作层设计契约

> 状态：设计草案
> 交付等级：Design Contract
> 范围：角色跳跃、攻击、特殊攻击、受击、破韧、动作取消、动作轨道、动作调试和动作编辑器的应用层契约

## 目标

当前项目已经具备角色配置聚合、角色资源导入、Runtime Spawn binding plan、基础移动/跳跃输入、Combat action bridge、Gameplay ability command bridge、压力反应、MxAnimation 表现适配和 Debug UI source。

下一阶段要补的是角色动作层：

```text
Character Action Layer
  把玩家输入 / Runtime AI Planner 决策 / Replay / Debug / 受击事件 / 能力触发
  统一解析成角色动作计划，
  再编排到 CharacterControl、Combat、Gameplay、MxAnimation、Audio、VFX、Camera、Debug。
```

核心结论：

```text
统一“动作请求、解析、优先级、状态切换、取消/打断、时间轴、调试和编辑方式”。
不把移动、攻击、跳跃、受击都塞进一个大动作执行类。
```

基础移动仍由 Motion / Locomotion resolver 连续处理；攻击、特殊攻击、跳跃、受击和破韧进入可配置 Action / Reaction 计划；表现层只消费事件和请求，不反向驱动权威状态。

## 当前项目基线

### 已有能力

| 能力 | 当前落点 | 说明 |
| --- | --- | --- |
| 角色静态聚合 | `MxFramework.Character.Application` | `CharacterConfig`、`EquipmentStateConfig`、`AbilityLoadoutConfig`、`CombatActionSetConfig`、`CharacterPresentationProfileConfig` 已落地。 |
| 角色导入到 Runtime binding | `MxFramework.Character.RuntimeSpawn` | 读取导入产物，输出 `CharacterRuntimeBinding`、Gameplay plan、Combat body plan、weapon attachment/trace plan 和 resource preload plan。 |
| 命令入口 | `MxFramework.CharacterControl.CharacterCommand` | Local Input、Runtime AI Planner、Replay/Test source 可以输出同一命令形状。 |
| 动作 v0 请求 | `MxFramework.CharacterControl.CharacterActionRequest` | 当前是直接可执行请求：`combatActionId`、`gameplayAbilityId`、cancel、queue/force start。 |
| 控制状态 | `CharacterControlStateMachine` | 当前四态：`Locomotion`、`Action`、`Reaction`、`Disabled`。 |
| 基础移动和跳跃 | `CharacterMotionResolver` + `CombatKinematicMotor` | 已支持移动、速度倍率、冲刺倍率、跳跃、grounded、world sync。 |
| Combat action | `CombatActionTimeline` + `CombatActionRunner` | 已支持 Startup/Active/Recovery、cancel/invincible/parry/super armor window、frame event 和 lifecycle event。 |
| Gameplay ability | `GameplayRuntimeCommandFactory` + component ability systems | CharacterControl 只 enqueue command，不直接写 Gameplay 状态。 |
| 姿态/防御/护甲压力 | `GameplayPosturePressureSystem`、`GameplayGuardPressureSystem`、`CharacterPressureReactionController` | 已有 typed event 到 Reaction 的 v0 bridge。 |
| 动画表现 | `MxAnimation`、`CombatMxAnimationUnityBridge`、`CharacterAnimationPresentationController`（定义在 `CharacterAnimationPresentation.cs`） | locomotion/reaction 和 Combat action 表现已有 bridge。 |
| 调试 | `CharacterControlDebugSource`、Combat / Gameplay / Runtime Debug UI source | 已有只读 snapshot 和事件摘要。 |

### 当前缺口

| 缺口 | 影响 |
| --- | --- |
| `CharacterControl.CharacterActionRequest` 仍偏执行目标，不是完整意图解析入口 | 输入意图、Ability、受击上下文、调试强制动作尚未统一解析。 |
| `CombatActionSetConfig` 只做 action key -> Combat action / trace / animation key 绑定 | 缺少角色级动作集合、动作分类、需求、取消/打断、轨道、动作模板/变体。 |
| `CharacterMotionSettings` 只是状态倍率 | 缺少角色级 `MovementProfile`、Movement Mode、转向/牵引/空中控制/停止距离等配置。 |
| Reaction 目前主要来自 pressure typed events | `PostureBreakEvent` / `GuardBreakEvent` / `ArmorBreakEvent` 不携带 body part、hit zone、damage type、hit direction；完整受击选择需要 `ReactionContext` 生成器。 |
| Combat action timeline 是权威战斗时间线，但不是角色级多轨动作计划 | 缺少 Motion / Combat / Gameplay / Animation / Presentation / Debug 统一可视化轨道。 |
| 没有 Character Action Workstation | 工具层缺口存在，但优先级低于 `ReactionContext` 数据源和 phase authority；第一阶段只需要只读诊断。 |

## API 复用计划

| 需求点 | 优先复用的框架 API / 模块 | 本设计使用方式 | 不新建平行系统的边界 |
| --- | --- | --- | --- |
| Runtime 时钟、命令、回放、hash | `RuntimeHost`、`RuntimeFrame`、`RuntimeCommandBuffer`、`RuntimeReplayRecorder`、`IRuntimeHashContributor` | 动作请求和动作实例记录 Runtime frame；Gameplay 仍通过 shared command buffer；动作 debug 可进入 replay/diagnostics 事件。 | Action Layer 不 drain shared command buffer。 |
| 输入意图 | `InputSnapshot`、`InputCommandQueue`、`InputCharacterCommandSource` | 输入只转成 `CharacterCommand` / action intent，不直接调用动作系统私有 API。 | 核心动作层不引用 Unity Input。 |
| Runtime AI Planner | `RuntimeAiPlannerCharacterCommandSource`、Runtime AI Planner facts | Runtime AI Planner 只提交同一动作意图，读取 resolver 可用性、代价、拒绝原因。 | 不让 Runtime AI Planner 直接播放动画或启动 Combat trace。 |
| 角色聚合 | `CharacterResolvedProfile`、`EquipmentStateResolver`、`AbilityGrantResolver`、`CombatActionBindingResolver` | 以当前角色 + 当前装备状态选择可用动作集合、能力绑定、reaction profile 和资源依赖。 | Character Action 不让 Gameplay/Combat 反向依赖 Character。 |
| 移动和跳跃权威 | `CharacterMotionResolver`、`CombatKinematicMotor`、`CombatMotionInput` | Movement profile 生成 motion settings / motion config；Jump action 通过 MotionTrack 写起跳意图，最终仍由 Combat motor 结算。 | 不用 Unity `CharacterController`、`Rigidbody` 或 root motion 作为权威。 |
| Combat action / trace / hit resolve | `CombatActionRunner`、`CombatActionTimeline`、`ICombatActionTraceProvider`、`HitResolveSystem` | CombatTrack 只引用 Combat action / trace profile / hit group，在指定帧请求 Combat 开启或推进权威动作。 | Character Action 不复制 Combat hit window、hit resolve 或伤害权威。 |
| Gameplay 能力、属性、Buff、压力 | `GameplayComponentWorld`、component attributes、ability commands、pressure systems | GameplayTrack 发 cost/cooldown/buff/pressure/ability effect request 或 RuntimeCommand。 | 不直接写 attribute store 私有字段，不新建 HP/Buff/Ability 管线。 |
| 动画表现 | `MxAnimationSetDefinition`、`IMxAnimationBackend`、Combat/CharacterControl animation bridge | AnimationTrack 只表达 play/crossfade/blend/layer request 和 action key；Combat action 表现继续由 Combat bridge 拥有。 | 动画时间、Playable state、root motion 不反写权威。 |
| 音频、VFX、相机、UI feedback | Audio service、Resources、Camera、UI Toolkit、presentation event sink | PresentationTrack 只输出 ResourceKey / cue id / camera request / UI event，组合根注入 adapter。 | 表现失败只进 diagnostics，不改变动作是否命中。 |
| 调试和复盘 | `IFrameworkDebugSource`、Debug UI source、Combat/GamePlay timeline adapters | Action runner 输出结构化事件和 snapshot，Debug UI 展示动作选择、拒绝、取消、track fired。 | Debug UI 默认只读，不成为运行时状态源。 |
| 配置和资源 | `ConfigSchema`、`ConfigTable<T>`、`ResourceKey`、Resource Catalog / preload | Character action config 是 noEngine 配置；资源引用一律用 ResourceKey 或 stable id。 | 不保存 Unity object、AnimationClip、Prefab、Material。 |

## 架构定位

建议新增角色动作层位于 Character Application 与 Character Control/Combat/Gameplay 之间：

```text
Player Input / Runtime AI Planner / Script / Replay / Debug / Combat Hit Events
        ↓
CharacterCommand / CharacterActionRequest
        ↓
CharacterActionResolver
        ↓
CharacterActionPlan
        ↓
CharacterActionRunner
        ↓
Action Track Adapters
        ↓
CharacterControl / Combat / Gameplay / MxAnimation / Audio / VFX / Camera / Debug
```

模块建议：

```text
MxFramework.CharacterAction
  noEngine contract、resolver、runner、validation、diagnostics
  -> Core / Config / Runtime / Gameplay / Combat / Resources / Character.Application / CharacterControl

MxFramework.CharacterAction.Animation
  optional noEngine presentation adapter
  -> CharacterAction / Animation / Resources

MxFramework.Editor.CharacterAction
  Unity Editor / UI Toolkit workstation
  -> CharacterAction / Character.Application / Resources / Animation authoring helpers
```

第一阶段可以不新增程序集，先在任务中固定契约；实现时若体量较小，也可以先落在 `MxFramework.Character.Application` 的扩展命名空间中。但长期不建议继续扩大 `CharacterControl`，因为它已经是命令、状态、运动、桥接和 debug 的 runtime orchestration 层，不应承担配置 authoring 和多轨校验职责。

角色动作层按四个职责层落地：

| 层 | 职责 | 输出 | 边界 |
| --- | --- | --- | --- |
| Action Request Layer | 接收 Local Input、Runtime AI Planner、Script、Replay、Debug 和 PlayerIntervention，把外部意图统一成请求。 | `CharacterActionIntentRequest` 或兼容当前 `CharacterActionRequest` 的 direct request。 | 不解析资源、冷却、Combat window，也不启动动作。 |
| Action Resolver Layer | 按角色配置、装备状态、能力装载、当前状态、姿态段、目标和 reaction context 解析动作。 | `CharacterActionResolveResult` / `CharacterActionPlan` / 稳定 reject code。 | 不推进帧，不执行 track，不直接写 Gameplay / Combat。 |
| Action Runner Layer | 将 plan 实例化并按 frame 推进，切 phase，派发 track event，处理 cancel / interrupt。 | `CharacterActionInstance`、track dispatch、debug event stream。 | 不引用 Unity backend；所有外部效果经 track sink / adapter。 |
| State Management Layer | 维护 Base / Action / Reaction / Overlay 的高层占用、锁和优先级。 | 当前层状态、锁、活动动作实例和可取消/可打断状态。 | 不替代 Gameplay 生命周期、Combat action state 或 Animation state。 |

Resolver 和 Runner 都必须可在无 Unity 场景的 EditMode 测试中直接实例化。Unity 组合根只负责提供输入来源、资源 provider、track adapter 和表现后端。

## 动作类型如何统一

### 基础移动

基础移动不是普通时间轴动作。它是持续意图和 Movement Mode：

```text
MoveIntent
  -> desired direction
  -> movement profile
  -> movement mode
  -> CharacterMotionResolver
  -> CombatKinematicMotor
```

建议新增：

```csharp
public enum CharacterMovementMode
{
    Idle,
    Walk,
    Run,
    Strafe,
    TurnInPlace,
    ApproachTarget,
    Retreat,
    CircleTarget,
    RootMotionDriven,
    Airborne,
    ControlLocked
}
```

`CharacterMovementProfileConfig` 第一版建议只保存可确定性转换到 Combat motion 的字段：

```text
StableId
WalkSpeed
RunSpeed
Acceleration
Deceleration
TurnSpeed
GroundFriction
AirControl
Gravity
JumpImpulse
SlopeLimitDegrees
LocomotionBlendId
```

当前 `CharacterMotionSettings` 可作为 profile 的最小 runtime adapter；后续再把速度、加速度、空中控制等下沉到 Combat motion config。

### 攻击和特殊攻击

普通攻击和特殊攻击统一为 Timeline Action：

```text
BasicAttack = 简单 CharacterActionConfig
Skill / SpecialAttack = 更复杂 CharacterActionConfig
```

区别不在系统，而在配置复杂度：

| 类型 | 典型轨道 |
| --- | --- |
| 普通攻击 | CombatTrack + AnimationTrack + 少量 GameplayTrack + PresentationTrack |
| 重攻击 | CombatTrack + MotionTrack + GameplayTrack + cancel/interrupt rule |
| 特殊攻击 | GameplayTrack + CombatTrack / projectile / area + resource/cooldown + 表现轨道 |

### 跳跃

跳跃是 Action + Movement Mode 的混合：

```text
CharacterActionCategory.Jump
  -> MotionTrack 发起跳 impulse / movement mode = Airborne
  -> CombatKinematicMotor 结算速度、重力和落地
  -> Landing policy 选择 landing / hard landing / recovery
```

基础跳跃不应复制一套移动系统；跳劈、跃击、飞扑可以作为带 CombatTrack / GameplayTrack 的 Jump action。

### 受击、破韧和击退

Combat 只产生命中事实，Gameplay 更新属性/压力，Character Action Layer 选择反应动作：

```text
HitResolveResult / Pressure typed event
  -> ReactionContext
  -> CharacterReactionSelector
  -> CharacterActionRequest(category=Reaction)
  -> CharacterActionRunner
```

反应选择至少考虑：

```text
body part / hit zone / damage type / impact force / hit direction
posture band / guard break / armor break / death
current action phase / support profile / super armor / airborne
body kind / equipment tags / reaction group
```

### 被动和状态表现

燃烧、冰冻、护盾、蓄力、光环等应进入 Overlay layer：

```text
Overlay Action
  -> PresentationTrack / GameplayTrack
  -> 可与 Locomotion 或 Action 共存
```

Overlay 默认不争夺 base movement，但可以通过 Gameplay status / motion modifier 改变移动倍率或输入可用性。

## 核心数据契约

### CharacterActionSetConfig

它是角色级动作集合，不替代现有 `CombatActionSetConfig`。现有 `CombatActionSetConfig` 仍只负责 action key 到 Combat action / trace / animation key 的轻绑定。

```csharp
public sealed class CharacterActionSetConfig
{
    public int Id;
    public string StableId;
    public string DisplayName;

    public string CharacterStableId;
    public string EquipmentStateStableId;

    public CharacterActionBinding[] CommandBindings;
    public CharacterAbilityActionBinding[] AbilityBindings;
    public CharacterReactionBinding[] ReactionBindings;

    public string MovementProfileId;
    public string ReactionProfileId;
    public string DefaultActionId;
}
```

职责：

```text
当前角色 + 当前装备状态能使用哪些动作。
输入 / Runtime AI Planner 意图映射到哪个动作。
Ability 映射到哪个动作。
受击上下文映射到哪个 reaction profile。
```

### CharacterActionConfig

```csharp
public enum CharacterActionCategory
{
    BasicAttack,
    Skill,
    Guard,
    Dodge,
    Jump,
    Interaction,
    Reaction,
    PassiveOverlay
}
```

```csharp
public sealed class CharacterActionConfig
{
    public int Id;
    public string StableId;
    public string DisplayName;

    public CharacterActionCategory Category;
    public string[] Tags;

    public int Priority;
    public int? DurationFrames;

    public CharacterActionRequirement[] Requirements;
    public CharacterActionPhase[] Phases;
    public CharacterCancelRule[] CancelRules;
    public CharacterInterruptRule[] InterruptRules;

    public MotionTrackConfig MotionTrack;
    public CombatTrackConfig CombatTrack;
    public GameplayTrackConfig GameplayTrack;
    public AnimationTrackConfig AnimationTrack;
    public PresentationTrackConfig PresentationTrack;
    public DebugTrackConfig DebugTrack;
}
```

关键边界：

- `DurationFrames` 为空时由绑定的 `CombatActionTimeline.TotalFrames` 或 runner policy 决定；非空时作为角色级编排和编辑器视图，不复制 Combat 的命中权威。
- 如果 `CombatTrack.CombatActionId` 非空，validator 必须检查 Character action duration / phase 与 `CombatActionTimeline.TotalFrames` 的对齐策略。
- Hit window、same-target hit-once、parry、invincible、super armor 仍以 Combat action timeline / HitResolve 为权威。
- `Requirements` 只能表达动作准入条件，例如地面/空中、装备 tag、资源预检、姿态段、target valid、status required/forbidden；真正资源扣除和 status 变化仍由 GameplayTrack adapter 申请。

### Binding

```csharp
public sealed class CharacterActionBinding
{
    public string IntentId;        // LightAttack / HeavyAttack / Dodge / Jump / Guard
    public string ActionId;
    public int Priority;
    public bool AllowQueue;
    public int QueueWindowFrames;
}
```

```csharp
public sealed class CharacterAbilityActionBinding
{
    public int AbilityId;
    public string ActionId;
    public string[] RequiredTags;
    public string[] ForbiddenTags;
}
```

```csharp
public sealed class CharacterReactionBinding
{
    public string ReactionProfileId;
    public string DefaultActionId;
    public string[] RequiredTags;
    public string[] ForbiddenTags;
}
```

### Phase

```csharp
public enum CharacterActionPhaseKind
{
    Startup,
    Active,
    Recovery,
    Loop,
    Airborne,
    Landing,
    Channel,
    Hold,
    Exit
}
```

```csharp
public sealed class CharacterActionPhase
{
    public CharacterActionPhaseKind Kind;
    public int StartFrame;
    public int EndFrame;
    public string DisplayName;
    public CombatActionPhase? CombatPhaseAnchor;
    public bool RequiresCombatPhaseMatch;

    public bool LockMovement;
    public bool LockRotation;
    public bool AllowAimAdjust;
    public bool IsCommitted;
    public bool IsInterruptible;
}
```

Phase 权威必须按动作是否绑定 `CombatActionId` 分两种模式：

```csharp
public enum CharacterActionTimelineAuthority
{
    CharacterAuthored,
    CombatAnchored
}
```

| 模式 | 触发条件 | 权威规则 |
| --- | --- | --- |
| `CharacterAuthored` | 无 `CombatTrack.CombatActionId`，例如纯跳跃、闪避、交互、overlay。 | `CharacterActionConfig.DurationFrames`、`CharacterActionPhase`、`CancelRule` 和 `InterruptRule` 是动作时序权威。 |
| `CombatAnchored` | 绑定 `CombatTrack.CombatActionId`。 | `CombatActionTimeline` 是总帧数、Startup/Active/Recovery/Finished、hit window、parry/super armor/invincible window 和 Combat cancel window 的权威。Character phase 只做角色层 overlay、锁定、调试和意图级规则。 |

Combat anchored 动作的 phase 映射规则：

| Character phase | Combat phase anchor |
| --- | --- |
| `Startup` | 必须锚定 `CombatActionPhase.Startup`。 |
| `Active` | 必须锚定 `CombatActionPhase.Active`。 |
| `Recovery` | 必须锚定 `CombatActionPhase.Recovery`。 |
| `Loop` / `Channel` / `Hold` | 必须显式填写 `CombatPhaseAnchor`，否则不能用于 cancel、committed 或 interruptible 判断。 |
| `Airborne` / `Landing` / `Exit` | 只能作为角色子阶段；若动作绑定 Combat action，必须声明父级 `CombatPhaseAnchor` 或由 validator 拒绝。 |

`IsCommitted` 和 `IsInterruptible` 在 `CharacterAuthored` 模式下由 Character phase 直接给出；在 `CombatAnchored` 模式下只能作为角色层附加限制，最终结果必须同时满足：

```text
Character phase / CancelRule / InterruptRule
AND CombatActionRunner.CanCancelTo(...) / CombatActionWindow / Combat support profile
```

若 Character 允许但 Combat 拒绝，结果是 `ACT_COMBAT_CANCEL_REJECTED`；若 Combat 允许但 Character 拒绝，结果是 `ACT_CHARACTER_CANCEL_REJECTED`。不能让两个 phase 系统各自独立做最终裁决。

### CancelRule / InterruptRule

```csharp
public sealed class CharacterCancelRule
{
    public string FromPhase;
    public string ToIntentId;
    public int StartFrame;
    public int EndFrame;
    public int PriorityRequired;
    public string RequiredTag;
    public string ForbiddenTag;
}
```

```csharp
public sealed class CharacterInterruptRule
{
    public string Source; // PostureBreak / GuardBreak / Hit / Death / PlayerIntervention
    public int MinPriority;
    public bool ForceInterrupt;
    public string ReactionActionId;
}
```

第一切片不需要替换 `CombatActionWindow`。Character cancel rule 负责意图级判断和 Debug 解释；CombatActionWindow 负责 Combat action runner 能否 cancel 到具体 CombatActionId。两者冲突时按上面的 phase authority 规则输出稳定拒绝码。

### ReactionContext

`ReactionContext` 是 Reaction selector 的核心输入，必须先成为 Issue 0 的前置契约。当前源码中的 `PostureBreakEvent`、`GuardBreakEvent`、`ArmorBreakEvent` 只携带 pressure / break / source / trace 相关字段，不携带 body part、hit zone、damage type 或 hit direction。因此不能让第一版 `CharacterReactionRule` 默认依赖这些字段。

建议契约：

```csharp
public enum CharacterReactionContextSourceKind
{
    PressureEvent,
    CombatHitResult,
    CombatHitWithPressureEvent,
    Death,
    Scripted
}

public enum CharacterReactionContextCompleteness
{
    PressureOnly,
    HitResolved,
    BodyPartResolved,
    Full
}

public enum CharacterHitDirection
{
    Unknown,
    Front,
    Back,
    Left,
    Right,
    Up,
    Down
}

public readonly struct CharacterReactionContext
{
    public CharacterControlEntityRef TargetEntity { get; }
    public RuntimeFrame RuntimeFrame { get; }
    public string TraceId { get; }

    public CharacterReactionContextSourceKind SourceKind { get; }
    public CharacterReactionContextCompleteness Completeness { get; }

    public PressureBand PostureBand { get; }
    public bool IsPostureBreak { get; }
    public bool IsGuardBreak { get; }
    public bool IsArmorBreak { get; }
    public bool IsDeath { get; }
    public bool IsAirborne { get; }

    public int SourceId { get; }
    public int ImpactForce { get; }
    public string DamageTypeId { get; }
    public CharacterHitDirection HitDirection { get; }
    public string HitZoneId { get; }
    public string BodyPartId { get; }
    public string BodyPartKind { get; }
    public string ReactionGroupId { get; }

    public string CurrentActionId { get; }
    public CharacterActionPhaseKind CurrentCharacterPhase { get; }
    public CombatActionPhase CurrentCombatPhase { get; }
    public bool CurrentActionCommitted { get; }
    public bool CurrentActionInterruptible { get; }

    public bool HasHitPayload { get; }
    public bool HasBodyPartPayload { get; }
}
```

生成器契约：

```text
CharacterReactionContextBuilder
  输入：Gameplay pressure typed event、Combat hit result、BodyPartHitZoneResolver、CharacterControl state、CombatActionRunner state。
  输出：CharacterReactionContext + completeness + diagnostics。
```

第一版必须支持 `PressureOnly`：由 `PostureBreakEvent`、`GuardBreakEvent`、`ArmorBreakEvent`、`PressureBandChangedEvent`、Death/lifecycle 事件和 CharacterControl 当前状态生成，只允许基于 pressure band、break flags、death、airborne、当前动作可打断性选择 reaction。

`CombatHitResult` 到 `ReactionContext` 的桥梁落地前，`RequiredBodyPartKind`、`RequiredDamageType`、`RequiredHitDirection`、`MinImpactForce`、`RequiredReactionGroupId` 这些规则字段只能作为完整版本预留；若 rule 填写了这些字段但 context completeness 低于要求，validator 必须报错或 resolver 返回 `ReactionContextIncomplete`，不能静默降级。

### ReactionProfile

```csharp
public sealed class CharacterReactionProfile
{
    public string StableId;
    public CharacterReactionRule[] Rules;
    public string DefaultLightHitActionId;
    public string DefaultHeavyHitActionId;
    public string DefaultBreakActionId;
    public string DefaultDeathActionId;
}
```

```csharp
public sealed class CharacterReactionRule
{
    public string StableId;
    public CharacterReactionContextCompleteness RequiredContextCompleteness;
    public string RequiredBodyPartKind;
    public string RequiredDamageType;
    public PressureBand MinPostureBand;
    public int MinImpactForce;
    public string RequiredHitDirection;
    public bool RequiresPostureBreak;
    public bool RequiresGuardBreak;
    public bool RequiresArmorBreak;
    public bool RequiresAirborne;
    public bool RequiresHyperArmorPierce;
    public string RequiredReactionGroupId;

    public string ReactionActionId;
    public int Priority;
}
```

Reaction selector 的完整目标维度包括：

```text
命中部位 / hit zone / body part kind / reaction group
damage type / impact force / hit direction
posture band / posture break / guard break / armor break / death
airborne / grounded / current movement mode
current action phase / committed / interruptible / support profile / hyper armor
equipment tags / body kind / status tags
```

MVP 维度必须收敛为：

```text
posture band / posture break / guard break / armor break / death
airborne / grounded / current movement mode
current action phase / committed / interruptible
```

同优先级规则必须使用稳定 tie-breaker，例如 priority DESC、specificity DESC、source order ASC、stable id ASC；不能按 Dictionary 遍历顺序选择。

## Track 契约

### MotionTrack

```csharp
public sealed class MotionTrackConfig
{
    public bool UsesRootMotionReference;
    public MotionEvent[] Events;
}
```

MotionEvent 建议使用枚举，不用自由字符串：

```text
SetVelocity
AddImpulse
LockMove
UnlockMove
TurnToTarget
SetMovementMode
ApplyRootMotionReference
```

MotionTrack 只生成 movement request / lock / impulse；最终位置、速度、落地和碰撞仍由 `CombatKinematicMotor` 负责。

### CombatTrack

```csharp
public sealed class CombatTrackConfig
{
    public int CombatActionId;
    public CombatTraceEvent[] TraceEvents;
}
```

```csharp
public sealed class CombatTraceEvent
{
    public int StartFrame;
    public int EndFrame;
    public string TraceProfileId;
    public string HitGroupId;
}
```

第一版如果已有 `CombatActionTimeline` 能表达 frame event / weapon trace，Character action 可只保存 `CombatActionId` 和 override。不要把 Combat 内部 hit resolve 数据复制到 Character action。

### GameplayTrack

```csharp
public sealed class GameplayTrackConfig
{
    public GameplayActionEvent[] Events;
}
```

Gameplay event kind：

```text
ConsumeResource
StartCooldown
AddBuff
RemoveBuff
AddPressure
ApplyAbilityEffect
SpawnProjectile
GrantStatus
ClearStatus
```

这些 event 不直接写 store。adapter 应转成 Gameplay request、typed event 或 `RuntimeCommand`。

### AnimationTrack

```csharp
public sealed class AnimationTrackConfig
{
    public string AnimationActionKey;
    public string LayerId;
    public int CrossFadeFrames;
    public bool UsesRootMotionReference;
    public string[] RequiredClips;
}
```

如果动作是 Combat action，默认仍由 `CombatMxAnimationUnityBridge` 处理 action started / finished / canceled。Character AnimationTrack 只用于：

- 非 Combat 的 gameplay-only action。
- Jump / landing / reaction 的表现请求。
- layer weight / upper body overlay。
- editor validation 和 warmup resource dependency。

### PresentationTrack

```csharp
public sealed class PresentationTrackConfig
{
    public VfxEvent[] VfxEvents;
    public AudioCueEvent[] AudioEvents;
    public CameraEvent[] CameraEvents;
    public UiFeedbackEvent[] UiEvents;
}
```

资源字段使用 `ResourceKey`、audio event id、camera profile id 或 stable id；不保存 Unity object。

### DebugTrack

DebugTrack 不影响权威，只定义需要额外记录的 markers：

```text
ActionStarted
PhaseChanged
TrackEventFired
HitTraceStarted
HitResolved
PressureChanged
CancelWindowOpened
ActionRejected
ActionInterrupted
ActionFinished
```

## Runtime 执行流程

### 1. 请求动作

现有 `CharacterCommand` 继续作为外部命令入口。为了兼容当前 v0，可以先新增 resolver 输入 DTO：

```csharp
public readonly struct CharacterActionIntentRequest
{
    public CharacterControlEntityRef Entity;
    public string IntentId;
    public int? AbilityId;
    public string RequestedActionId;
    public CharacterCommandSourceKind SourceKind;
    public int Priority;
    public RuntimeFrame Frame;
    public string TraceId;
}
```

后续可以把它并入 `CharacterControl.CharacterActionRequest`，但不建议在第一切片直接破坏当前 direct combat / gameplay request API。

`SourceKind` 至少覆盖：

```text
LocalInput
RuntimeAiPlanner
Replay
Scripted
Debug
PlayerIntervention
Reaction
```

不同来源只影响优先级、排队策略和是否允许插队；它们不绕过 cancel / interrupt / disabled / death 等规则。`PlayerIntervention` 可以拥有很高 priority，但仍必须通过当前动作阶段和规则校验。

### 2. Resolver 解析

输入：

```text
CharacterActionIntentRequest
+ CharacterResolvedProfile
+ active equipment state
+ effective ability loadout
+ CharacterActionSetConfig
+ current CharacterControl state
+ current Combat action state
+ Gameplay resource/cooldown/status snapshot
+ target/reaction context
```

固定解析顺序：

```text
1. 读取 CharacterControl / Gameplay lifecycle / Reaction layer，拒绝死亡、Disabled、不可控制、硬直锁定等基础状态。
2. 如果存在 pending ReactionContext 或 Death / Knockdown / PostureBreak 这类高优先级事件，先走 ReactionBinding / ReactionProfile。
3. 根据 CharacterActionSetConfig.ActionBindings 找到 intent 候选动作。
4. 根据 CharacterAbilityActionBinding 追加 ability 候选动作；冲突时按 action priority、binding priority、source priority 和 source order 稳定排序。
5. 检查 CharacterActionConfig.Requirements，包括地面/空中、装备 tag、资源预检、status、target、姿态段。
6. 检查当前 Action / Reaction / Overlay 占用，应用 CancelRule / InterruptRule / queue window。
7. 解析 CombatActionId、AnimationActionKey、ResourceKey 和 track adapter 可用性，输出 plan 或稳定拒绝原因。
```

Resolver 不执行动作。即使是资源消耗，也只能做只读预检；真正扣资源必须在 runner 派发 GameplayTrack 时由 Gameplay 返回成功或失败。

输出：

```csharp
public sealed class CharacterActionResolveResult
{
    public bool Accepted;
    public CharacterActionRejectReason RejectReason;
    public CharacterActionPlan Plan;
    public CharacterDiagnostic[] Diagnostics;
}
```

拒绝原因必须稳定：

```text
MissingActionSet
MissingActionBinding
MissingAbilityBinding
MissingActionConfig
StateDisabled
Dead
ActionCommitted
CooldownActive
InsufficientResource
PressureReactionLocked
InvalidTarget
EquipmentStateMismatch
LowerPriorityRejected
CombatActionMissing
AnimationActionMissing
ResourceMissing
```

`Queued` 应作为 resolve result 的显式状态，而不是把排队伪装成 success。建议第一版状态为：

```text
Success
Queued
Rejected
```

### 3. Plan

```csharp
public sealed class CharacterActionPlan
{
    public long PlanId;
    public string ActionId;
    public CharacterActionCategory Category;
    public int Priority;
    public int DurationFrames;
    public CharacterActionPhase[] Phases;
    public CharacterActionTrackPlan[] Tracks;
    public string TraceId;
}
```

Plan 是 resolver 产物，便于 debug、preview、replay 和 tests。Plan 不持有 Unity 对象或资源 handle。

Plan 中的 `DurationFrames` 是 resolved value：当 `CharacterActionConfig.DurationFrames` 为空时，resolver 必须从 Combat timeline、template 默认值或 runner policy 得到一个明确值，并把来源写入 diagnostics。

### 4. Runner

```csharp
public sealed class CharacterActionInstance
{
    public long InstanceId;
    public string ActionId;
    public int LocalFrame;
    public CharacterActionPhaseKind CurrentPhase;
    public CharacterActionState State;
    public string TraceId;
}
```

每 tick：

```text
1. 推进 local frame。
2. 更新 phase。
3. 先处理 queued / pending interrupt，确认本帧是否应切换。
4. 依次派发 MotionTrack、CombatTrack、GameplayTrack、AnimationTrack、PresentationTrack、DebugTrack。
5. 收集 adapter result，若 Gameplay cost / Combat start 等权威请求失败，按 InterruptRule 或 fail policy 退出动作。
6. 检查 cancel window / interrupt window / completion。
7. 完成后释放 Action layer 或 Reaction layer 占用。
8. 输出 diagnostics。
```

Runner 不应直接知道 Unity backend。所有轨道通过 adapter sink：

```text
ICharacterMotionTrackSink
ICharacterCombatTrackSink
ICharacterGameplayTrackSink
ICharacterAnimationTrackSink
ICharacterPresentationTrackSink
ICharacterActionDebugSink
```

### 5. 与现有 CharacterActionController 的关系

短期关系：

```text
CharacterActionResolver
  -> CharacterActionPlan
  -> CharacterActionRunner
  -> CombatTrack adapter
  -> CharacterControl.CharacterActionController.Submit(...)
```

也就是说，现有 `CharacterActionController` 继续作为启动 Combat action / enqueue Gameplay ability / cancel 的桥，不立即删除。

长期可收敛为：

```text
CharacterActionController 只保留低层 bridge 职责。
CharacterActionResolver + CharacterActionRunner 成为角色动作选择和多轨编排权威。
```

## 分层状态模型

目标模型是四层：

```text
Base Layer:
  Idle / Locomotion / Airborne

Action Layer:
  Attack / Skill / Guard / Dodge / Cast / Interact

Reaction Layer:
  HitReact / Stagger / PostureBreak / Knockdown / Death

Overlay Layer:
  BuffPose / Burning / Frozen / Charging / Shield / Aim
```

优先级：

```text
Death > Knockdown > PostureBreak > HitReact > Action > Locomotion
Overlay 可与 Locomotion / Action 共存
```

当前 `CharacterControlStateMachine` 只有四态，第一切片不要强行重写。推荐映射：

| 新层级 | 当前状态机映射 |
| --- | --- |
| Base Layer | `Locomotion` + `CharacterMotionResult.Grounded` / movement mode diagnostics |
| Action Layer | `Action` |
| Reaction Layer | `Reaction` |
| Disabled / Death | `Disabled` 或 high-priority reaction |
| Overlay Layer | Gameplay status + presentation overlay，不进入当前 state enum |

每层响应规则：

| 层 | 可被谁打断 | 能打断谁 | 说明 |
| --- | --- | --- | --- |
| Base Layer | Action / Reaction / Disabled | 无 | 持续移动模式，不拥有时间轴权威。 |
| Action Layer | Reaction / Death / 更高优先级 cancel target | Base Layer | 主动动作占用角色控制权，受 `CancelRule` 和 `InterruptRule` 限制。 |
| Reaction Layer | Death / Knockdown 等更高反应 | Action Layer / Base Layer | 受击、破韧、击倒、死亡在此层运行；默认清理 queued 主动动作。 |
| Overlay Layer | Overlay 自身规则或 Gameplay status 结束 | 通常不打断其他层 | 燃烧、护盾、蓄力等可共存；若要锁移动或动作，必须通过 Gameplay status 或 motion modifier 显式表达。 |

第一版可以把四层状态作为 `CharacterActionDebugSnapshot` 和 resolver 输入，而不是立即替换 `CharacterControlStateMachine` 的 enum。这样能在不破坏现有 Character Control slice 的前提下逐步迁移。

## 与其他系统的边界

### Gameplay

Character Action 不直接改 Gameplay 私有状态。它只发：

```text
ConsumeResourceRequest
ApplyGameplayEffectRequest
AddPressureRequest
GrantBuffRequest
RuntimeCommand
```

Gameplay 返回结构化结果或 typed event。冷却、资源、status、buff 和 pressure source of truth 在 Gameplay。

### Combat

Combat 负责：

```text
动作权威帧
Combat action phase/window
weapon trace / physics query
hit candidate / hit resolve
same target hit-once
invincible / parry / super armor
```

Character Action 负责：

```text
哪个角色动作要启动哪个 CombatActionId。
什么动作意图在当前状态下允许请求 Combat。
Combat hit / phase / window 结果如何反馈给角色动作 debug 和 reaction selector。
```

### MxAnimation

MxAnimation 只负责表现请求和 diagnostics：

```text
Play / CrossFade / Stop / SetBlend / LayerWeight
```

Root motion 如果参与权威，只能作为 bake reference，经 MotionTrack 转成确定性 motion request；不能让 Animator / Playables 当前状态直接写权威位置。

### Ability

Ability 是规则能力，Action 是执行时序与表现编排。

```text
Ability cast
  -> CharacterAbilityActionBinding
  -> CharacterActionIntentRequest
  -> CharacterActionPlan
  -> GameplayTrack / CombatTrack / PresentationTrack
```

一个 Ability 可以绑定多个 Action，一个 Action 也可以被多个 Ability 复用。

## Character Action Workstation

Workstation 是 Issue 7 的工具目标，不是 Issue 1 的前置契约。进入编辑器实现前，必须先完成 `ReactionContext` 数据源和 phase authority 映射，否则 UI 会把尚未验证的数据结构固化。

第一阶段只要求只读诊断：

| 档位 | 能力 | 边界 |
| --- | --- | --- |
| Readonly Report | 展示 action set、timeline、binding、resource dependency 和 diagnostics。 | 不写配置，不生成 Unity 资产。 |
| Light Edit | 编辑 phase、cancel/interrupt、binding 和最小 track marker。 | 只在 Issue 7 后写 noEngine config / authoring patch；不写 runtime state。 |
| Preview / Simulation | 逐帧预览 resolver / runner 输出和 adapter 诊断。 | 只使用同一套 noEngine resolver / runner；Unity adapter 只能作为可选表现预览。 |

工作台最终应回答命中帧、取消帧、移动锁、资源消耗、Combat/Animation/Resource 缺失、输入拒绝原因和动作打断原因。但这些能力来自 resolver、runner、validation 和 diagnostics，不能由工作台另写一套判断逻辑。

预览输入必须来自同一个执行链：

```text
Workstation request
  -> CharacterActionResolver
  -> CharacterActionPlan
  -> noEngine ActionRunner preview
  -> optional Unity adapter preview
```

如果 Unity Editor 无法加载资源，工作台仍应能输出 noEngine 诊断报告，不能把缺资源静默当作预览成功。

## 动作模板和变体

不要每个角色从零制作动作。建议两层：

```text
ActionTemplate
ActionVariant
```

示例：

```text
Template: one_hand_light_attack
  Startup 0-8
  Active 9-15
  Recovery 16-32
  Cancel 24-32 -> dodge / light_attack

Variant: iron_vanguard_sword_light_attack
  AnimationActionKey = iron_vanguard.light_attack_01
  CombatActionId = 800101
  TraceProfileId = sword.short_blade
  DamagePayload = posture_light
  VFX/SFX = iron_sword_light
```

Template 定义结构，Variant 覆盖资源、Combat action、payload、表现和角色特化参数。

## 校验规则

稳定错误码建议使用 `ACT_*`：

```text
ACT_MISSING_ACTION_ID
ACT_DUPLICATE_ACTION_ID
ACT_PHASE_OVERLAP
ACT_PHASE_GAP
ACT_PHASE_COMBAT_ANCHOR_MISSING
ACT_PHASE_COMBAT_RANGE_MISMATCH
ACT_CHARACTER_COMBAT_CANCEL_CONFLICT
ACT_INVALID_CANCEL_WINDOW
ACT_CANCEL_TARGET_MISSING
ACT_INTERRUPT_TARGET_MISSING
ACT_COMBAT_CANCEL_REJECTED
ACT_CHARACTER_CANCEL_REJECTED
ACT_COMBAT_ACTION_MISSING
ACT_COMBAT_TRACE_OUTSIDE_ACTIVE_PHASE
ACT_COMBAT_TIMELINE_MISMATCH
ACT_ANIMATION_ACTION_MISSING
ACT_ROOT_MOTION_WITHOUT_MOTION_ADAPTER
ACT_RESOURCE_COST_WITHOUT_RESOURCE_ID
ACT_ABILITY_BINDING_MISSING
ACT_REACTION_CONTEXT_MISSING_SOURCE
ACT_REACTION_CONTEXT_INCOMPLETE
ACT_REACTION_RULE_REQUIRES_HIT_CONTEXT
ACT_REACTION_RULE_NO_TARGET
ACT_REACTION_ACTION_WRONG_CATEGORY
ACT_PRESENTATION_RESOURCE_MISSING
ACT_AUDIO_CUE_MISSING
ACT_AUTHORITY_TRACK_INVALID
```

必备校验：

- `ActionId` / `StableId` 唯一。
- phase 不重叠；是否允许 gap 必须显式声明。
- Combat anchored 动作的 `Startup` / `Active` / `Recovery` 必须和 `CombatActionTimeline` 对齐；额外 character phase 必须填写 `CombatPhaseAnchor`。
- cancel window 不能落在不可取消 phase 内。
- 绑定 `CombatActionId` 的动作发生取消冲突时必须区分 Character 拒绝和 Combat 拒绝，不能只返回通用 cancel failed。
- reaction rule 目标必须是 `Reaction` category。
- reaction rule 如果要求 body part、damage type、hit direction、impact force 或 reaction group，必须声明需要 `HitResolved` / `BodyPartResolved` / `Full` context；MVP 的 `PressureOnly` context 下不得启用这些规则。
- Combat trace 不得在 action duration 外。
- Combat trace 默认应落在 Active phase；例外必须显式 suppress warning。
- `CombatActionId` 必须能在 Combat registry 或 authoring index 中找到。
- `AnimationActionKey` 必须能在 MxAnimation set 中找到，或标记为 external/mod-provided。
- Root motion reference 必须声明 motion adapter 和 deterministic conversion policy。
- Gameplay resource cost 必须有 resource id、cost source 和失败策略。
- Presentation resource 必须能在 Resource Catalog / package expectation 中找到。

## Debug Snapshot 和事件

建议快照：

```csharp
public sealed class CharacterActionDebugSnapshot
{
    public string CharacterStableId;
    public CharacterControlEntityRef Entity;

    public string CurrentBaseState;
    public string CurrentActionState;
    public string CurrentReactionState;
    public string[] ActiveOverlayStates;

    public string ActiveActionId;
    public long ActiveActionInstanceId;
    public int LocalFrame;
    public string CurrentPhase;

    public string LastCommandIntent;
    public string LastCommandSource;
    public string LastRejectReason;

    public string[] ActiveTracks;
    public string[] FiredEventsThisFrame;

    public int PosturePressure;
    public string PostureBand;
    public bool IsActionCommitted;
}
```

事件：

```text
ActionRequested
ActionResolved
ActionRejected
ActionQueued
ActionStarted
PhaseChanged
TrackEventFired
CombatActionStarted
HitTraceStarted
HitResolved
GameplayRequestSent
GameplayRequestRejected
PressureChanged
ActionInterrupted
ActionCanceled
ActionFinished
```

这些事件供 Debug UI、Simulation Harness、Replay diagnostic 和 Workstation preview 使用。

## 最小 MVP

第一阶段不要一次做完整动作系统。建议 7 个行为：

```text
Movement:
  Idle / Move

Action:
  LightAttack

Skill:
  HeavyAttack 或 DashStrike

Jump:
  BasicJump

Reaction:
  LightHitReact
  PostureBreakReact
  Death
```

验收问题：

```text
移动是否仍由 Combat motion 权威结算。
LightAttack 是否能通过 Character Action resolver 启动 Combat action。
HeavyAttack / DashStrike 是否能消耗 Gameplay resource 或输出结构化拒绝。
BasicJump 是否能进入 Airborne 并落地。
受击是否能根据 ReactionContext 选择 reaction action。
破韧是否能取消当前动作并进入 Reaction。
Debug 是否能解释动作选择、拒绝、命中、打断和完成。
```

## 分阶段实施建议

### Phase 1：Contract foundation（已完成）

Issue #400-#404 已完成 Phase 1 noEngine 数据契约、测试和契约文档收口：

- `CharacterReactionContext`、PressureOnly builders、reaction profile/rule selection，以及 incomplete hit context 的稳定 diagnostics。
- `CharacterActionConfig`、`CharacterActionSetConfig`、movement profile、phase、cancel/interrupt、intent request、resolve result、plan 和 track DTO contracts。
- `CharacterAuthored` / `CombatAnchored` phase authority 契约，以及缺失 Combat anchor、Character/Combat phase range mismatch 的稳定 diagnostics。
- Character-level rejection 与 Combat-window rejection 的 cancel conflict 分类。
- Plan duration resolution：Character-authored plan 来自 config 或显式 fallback policy；Combat-anchored plan 来自 Combat timeline total frames。

Phase 1 是 contract-only scope，不包含 resolver implementation、runner implementation、action track adapters、Unity integration、editor tooling 或 Character Action Workstation authoring。后续阶段必须消费这些契约，不能重新定义数据形状，除非测试证明存在明确不兼容。

Full hit reaction 仍然延后。当前只验证 `PressureOnly` reaction path：posture / guard / armor pressure events 和 explicit death。Body part、hit zone、damage type、hit direction、impact force 和 reaction group 维度必须等后续 `CombatHitResult -> ReactionContext` bridge 落地后才能成为 runtime selection rules。

### Phase 2：Resolver / Validation completion（进行中）

Phase 2 的目标是让 resolver / validator 输出足够稳定，使后续 Action Runner 可以直接消费 `CharacterActionPlan`、diagnostics 和 dependency data。Runner 不能重新做 binding、resource、phase 或 reaction selection 决策。

#### Phase 2.1：Resolver and Validation MVP（已完成，#416 / #417）

#416 / #417 已完成合并的 Resolver/Validation MVP：

- `CharacterActionResolver` 可以从 command binding、ability binding 和 PressureOnly reaction profile 生成 `CharacterActionResolveResult` / `CharacterActionPlan`。
- Resolver 保持 read-only，不消耗资源、不 enqueue Gameplay command、不启动 Combat action、不修改 CharacterControl、不派发 track event。
- Validation 已覆盖 action set/config binding、基础 phase authority、cancel conflict、PressureOnly reaction rule 和基础 track dependency diagnostics。
- 该 MVP 只是 Phase 2 的第一片，不代表 Phase 2 完成。

未完成项包括 candidate priority、tag requirements、deterministic PressureOnly reaction diagnostics、timeline/cancel validation completion、resource dependency collection、diagnostics formatting 和集成文档。

#### Phase 2.2：Phase 2 Plan Rebaseline（当前，#419）

更新本文档以反映 #416 / #417 的实际边界，并把剩余 Phase 2 work 拆为 2.3-2.7。该阶段不改代码，不更新 README / USAGE，除非那些入口已经公开宣传 Character Action runtime usage。

#### Phase 2.3：Resolver Candidate Priority and Tag Requirements（#420）

实现 resolver candidate list 和稳定排序：

- command binding resolution 生成候选列表，而不是只取单个 binding。
- ability binding 必须检查 `RequiredTags` / `ForbiddenTags`。
- 排序使用 source priority、request priority、binding priority、action priority、source order 和 stable id tie-breaker。
- active action 阻塞时明确 queue / lower-priority reject 语义。

#### Phase 2.4：ReactionSelector PressureOnly Determinism（#421）

完善 PressureOnly reaction 的确定性选择和解释：

- 定义 PressureOnly rule specificity scoring。
- 使用 trigger specificity、pressure/break/death/airborne specificity、rule priority、rule order、stable id 的稳定 tie-breaker。
- diagnostics 输出 matched rule id、skipped rule reason 和 fallback reason。
- 继续禁止在 PressureOnly context 下启用 body part、hit zone、damage type、hit direction 或 reaction group 规则；这些维度依赖后续 `CombatHitResult -> ReactionContext` bridge。

#### Phase 2.5：Timeline and Cancel Validation Completion（#422）

补齐 Runner 依赖的时序和取消校验：

- phase overlap、gap、range outside duration。
- `CombatAnchored` Startup / Active / Recovery anchors 和 range alignment。
- Combat trace event frame / phase legality。
- cancel window range、known target、cancelable / interruptible phase policy。
- 保留 `ACT_CHARACTER_CANCEL_REJECTED` 与 `ACT_COMBAT_CANCEL_REJECTED` 的可区分 diagnostics。

#### Phase 2.6：Resource Dependency Collector and Diagnostics Formatter（#423）

提取可复用的 dependency 和 diagnostics API：

- `CharacterActionResourceDependencyCollector` 收集 CombatAction、TraceProfile、AnimationAction、AudioCue、VfxResource、GameplayRequest 及其 track/frame metadata。
- validation 尽量复用 collector。
- diagnostics formatter 输出 code、message、action id、phase、track、frame 和可用 suggested fix。
- 不引用 Unity object、AnimationClip、Prefab、Material 或 Playables。

#### Phase 2.7：Phase 2 Integration Tests and Docs（#424）

用 noEngine 集成测试和文档关闭 Phase 2：

- 建立包含 LightAttack、HeavyAttack、DashStrike、BasicJump、LightHitReact、PostureBreakReact 和 Death 的 fixture action set。
- 覆盖 command -> plan、ability -> plan、reaction context -> reaction plan、cancel conflict -> stable reject、invalid config -> diagnostics 和 resource dependency collection。
- 文档说明 resolver / validator API、输入、输出、限制和 Runner prerequisites。
- 明确 Runner 消费 `CharacterActionPlan` 和 diagnostics，不重做 resolver decisions。

### Phase 3：Action Runner noEngine MVP（Phase 2 完成后）

Action Runner work 必须等待 Phase 2.3-2.7 完成后再开始。Runner 的职责是实例化和推进 resolver 输出的 plan：

```text
ActionInstance
local frame 推进
phase 切换
track event 派发
cancel / interrupt 判断
debug event stream
```

Runner 不能重新选择 action、ability binding、reaction rule、resource dependency 或 phase authority。若 plan 不足以执行，Runner 应返回缺口 diagnostics，并把契约缺口回流到 Phase 2 文档 / 后续 Issue，而不是在 Runner 内部补一套隐式 resolver。

### Phase 4：Motion + Combat + Gameplay Adapter

接入：

```text
MotionTrack -> CharacterMotionResolver / Combat motion request
CombatTrack -> CharacterActionController / CombatActionRunner
GameplayTrack -> RuntimeCommandBuffer / Gameplay request
```

验收：

```text
LightAttack -> Combat action -> trace / hit -> Gameplay pressure or HP delta
PostureBreak -> current action cancel -> reaction action
```

### Phase 5：Animation / Presentation Adapter

接入：

```text
MxAnimation
Audio service
VFX ResourceKey
Camera request
UI feedback
```

表现失败进入 diagnostics，不改变 authority。

### Phase 6：Reaction System

实现：

```text
CombatHitResult -> ReactionContext bridge
BodyPartHitZoneResolver integration
Full ReactionProfile dimensions
HitReact / directional hit / body part hit / PostureBreak / GuardBreak / ArmorBreak / Death
```

Phase 6 才启用 body part、damage type、hit direction、impact force、reaction group 等完整规则维度；在此之前只能使用 Phase 1 / Phase 2 已验证的 `PressureOnly` 维度。

### Phase 7：Character Action Workstation

第一版只读 + 轻编辑：

```text
Action timeline
Cancel window
Combat action alignment
Animation key
Resource dependency
Diagnostics
Preview data export
```

不手写 Unity 序列化资产；需要 Unity 资产时走 Editor 菜单 / Unity MCP / 专用生成器。

## 验收标准

- 设计明确区分 Character Action Layer、Character Control、Combat、Gameplay、MxAnimation 的 source of truth。
- `CharacterActionSetConfig` 不替代 `CombatActionSetConfig`，而是在其上层做角色动作集合。
- 基础移动使用 Movement Profile + Movement Mode，不被建模成每帧普通 Action。
- 攻击、特殊攻击、跳跃、受击、破韧、死亡都能进入统一 resolver / runner / debug 视图。
- Combat hit、cancel、trace、invincible、parry、super armor 权威仍在 Combat。
- Gameplay resource、cooldown、buff、attribute、pressure 权威仍在 Gameplay。
- Animation / Audio / VFX / Camera / UI 只作为 presentation adapter。
- 所有拒绝、缺资源、缺绑定、打断和校验失败都有稳定 code。
- 第一版 MVP 可以用 7 个行为验证移动、攻击、技能、跳跃、受击、破韧、死亡和 Debug 解释闭环。
