# CharacterTest Frame Sync Development Guide

> 2026-09-11 交互调度更新：正常播放的本地 Runtime AI Lab（含 loopback）每次 Update 默认最多推进 1 个完整固定 Tick；超限时丢弃整步墙钟积压并保留相位余量，状态栏提示可能慢于实时。RuntimeFrame 与命令不跳跃；真实网络模式、手动步进和历史重演保留各自推进路径。4 ms 为步间预算，不会中断单个 Tick；实现与当前性能边界见[运行卡顿优化实施记录](../Tasks/CHARACTER_TEST_RUNTIME_STALL_OPTIMIZATION_IMPLEMENTATION.md)。

> Runtime AI Lab boundary update (2026-07-11): normal historical navigation restores one immutable initial warmed-session baseline and replays retained external commands in the same `GameSlice`; it does not rebuild Views, Playables, camera or HUD. Full rebuild is available only through explicit `冷启动校验`. Intermediate state restoration is disabled by default. `状态采集 / 校验状态 / 同步记录状态` are explicit, session-only test tools; Authority determines Frame Sync PASS/FAIL, while Presentation is reported separately.

Warm Replay 同时保留每帧每角色的小型 animation input（locomotion blend、grounded/airborne、action key 与过渡参数），用于在无渲染重演中驱动常驻 Playables。该数据属于 session-only Presentation Capture：不恢复 gameplay authority，不参与 authority hash，也不进入 network snapshot、SaveState 或 replay wire。Frame Sync 是否通过仍只由命令重演后的 authority hash 决定。

> Status: Guide
>
> Date: 2026-07-09
>
> 本文记录 CharacterTest 帧同步链路的当前事实源。当前已覆盖已核对的角色移动、CharacterAction Runner、战斗命中 / 伤害、Gameplay identity / lifecycle、Gameplay Attribute / ScatterPressure、投射物、Interaction / Explosion Field、Tornado、Chain Lightning、Arc Mine、Rune Turret、Poison Bottle Pool、Hook Pull、Ability / cooldown、Guard Aura / Aura Buff、Runtime command state、Runtime event diagnostics、持续场域和 Physical Shield 链路，用于 Runtime AI Lab 精准播放、seek/review 和表现同步排查。本文不定义新的 Runtime AI Planner schema，不改变 SaveState、network snapshot、replay/hash 或公共 API。

> **边界整改提示（2026-07-11）**：本文混合记录了确定性帧模拟、authority snapshot / rollback、Runtime AI Lab Replay 和 Presentation Diagnostics。它们不都属于正式帧同步。后续新增或整改前必须先读取 `CHARACTER_TEST_FRAME_SYNC_BOUNDARY_REMEDIATION.md`；正式玩家联机以 `BootstrapOnly`、RuntimeFrame 权威命令和最小 authority state 为边界。动画、骨骼、音频、VFX、Overlay、HUD 及其它表现诊断不得因 Lab 回看需求继续扩张 network snapshot 或 authority hash。

当前代码基线：authority snapshot schema `20` 已移除 VFX、相机、Unity 动画输入和 planner 命令摘要；Lab 默认从不可变初始预热基线纯数据重演，不自动应用中途 checkpoint，A-B Loop 只做命令重演与 authority hash 验证。显式冷启动校验才执行完整 session 重建。`CharacterTestRuntimeAiLabAuthorityProbe` 是帧同步验收入口；显式“状态采集”只使用 `CharacterTestRuntimeAiLabPresentationDiagnosticProbe` 保存角色 View、动画/骨骼/procedural pose/ragdoll、VFX runtime 与 Overlay 的最小表现样本。综合 `CharacterTestRuntimeAiLabFrameSyncProbe` 已迁入 `MxFramework.Tests`，仅用于确定性测试的字段级诊断，不能作为 Runtime AI Lab authority 通过条件。

| 内容 | 正式帧同步验收 | 用途 |
| --- | --- | --- |
| RuntimeFrame、外部命令、固定 Tick、authority hash | 是 | 证明相同输入得到相同 gameplay 结果。 |
| Authority Snapshot / Rollback Checkpoint | 恢复工具 | bootstrap、late join、resync、稀疏本地回滚。 |
| Runtime AI Lab seek / A-B Loop | 本地工具 | 同一预热 session 恢复唯一初始基线 + 历史命令正向重演 authority；中间帧用逐帧 Presentation Capture 的 animation input 推进常驻动画状态机，但不刷新相机/UI/Overlay，目标帧提交一次完整表现。 |
| 动画、骨骼、音频、VFX、Overlay、HUD | 否 | 独立 Presentation Diagnostics；不进入 snapshot/hash。 |

当前探针职责边界：

- `CharacterTestRuntimeAiLabAuthorityProbe`：Runtime authority 验收，只比较 `RuntimeFrame + authority hash`。
- `CharacterTestRuntimeAiLabPresentationDiagnosticProbe`：Runtime 低频表现诊断，只由显式状态采集调用；不进入 gameplay Tick，也不采 gameplay、Planner、Projectile、Ability 或 Audio。
- `CharacterTestRuntimeAiLabFrameSyncProbe`：`MxFramework.Tests` 内的 test-only 复合诊断，保留 gameplay/Planner/Projectile/Ability/战斗机制/Audio 的详细 sample 和 mismatch 行合同；Runtime 程序集不引用它。

## Audio Runtime / 音频表现诊断链路

Audio Runtime 是表现诊断层，不是 CharacterTest gameplay authority。它的作用是把动作、命中、物理接触、interaction 等系统在确定帧发出的音频请求，转成可诊断的 audio intent drain、audio service debug snapshot 和各类 audio summary。Runtime AI Lab 可以检查这些表现输入，但音频差异不会导致 Frame Sync authority 验收失败，也不能把实际听到的声音或 Unity audio object 反推为权威状态。

链路：

```text
CharacterAction / weapon hit / physical contact / interaction
  -> CharacterTest audio intent queue / audio summary systems
  -> CharacterTestAudioIntentDrainSystem
  -> IAudioService / AudioDebugSnapshot
  -> Runtime AI Lab Presentation Diagnostics audio layer
```

关键边界：

- `CharacterTestAudioIntentDrainSummary` 记录目标帧 drain 的请求数、播放数、失败/丢弃/跳过数、最后 cue / event / emitter。它用于定位 seek/replay 后是否重复 drain 或漏 drain 音频 intent。
- `CharacterTestInteractionAudioSummary`、`CharacterTestPhysicalAudioSummary`、`CharacterTestWeaponHitAudioSummary` 是 CharacterTest 内部低频表现摘要，用于定位 interaction、脚步/落地/物体碰撞、武器命中音频是否跟目标帧一致。
- `IAudioService.CaptureSnapshot()` 提供 `AudioDebugSnapshot`：初始化状态、总 play/stop 请求、active event 数、active event 列表、bus 状态和 recent errors。Runtime AI Lab probe 只读这些字段，不在检查时播放、停止或刷新音频。
- Unity AudioSource、FMOD/Wwise/平台音频后端和听觉结果都是表现层。seek/review 后如果 gameplay state 一致但声音不对，优先检查 audio intent drain、active audio event、audio service snapshot 和 presentation mute/stop 策略。

Runtime AI Lab probe 的 `audio` layer 字段：

| 字段 | 用途 |
| --- | --- |
| `serviceAvailable` / `initialized` | 定位目标帧是否存在 audio service，以及初始化状态是否一致。 |
| `totalPlayRequests` / `totalStopRequests` | 定位 seek/replay 后是否重复播放或漏停止音频。 |
| `activeEventCount` / `activeEvents.*` | 定位 active audio event 是否丢失、重复或绑定到错误 emitter / bus / handle state。 |
| `bus.*` | 定位 bus 音量、静音状态和 bus id/name 是否与目标帧一致。 |
| `error.*` | 定位 replay/seek 后是否产生了不同的音频错误。 |
| `intent.*` | 定位 CharacterTest audio intent drain 是否与目标 RuntimeFrame 一致。 |
| `interaction.*` | 定位 interaction force / field audio 是否在同一帧产生同一摘要。 |
| `physical.*` | 定位脚步、落地、身体接触、物体碰撞和 motion source fallback 是否一致。 |
| `weaponHit.*` | 定位武器命中、格挡、规则缺失、cue 缺失和播放失败等音频摘要是否一致。 |

失败示例：

Automation coverage:
- `RuntimeAiLabFrameSyncProbe_ReportsAudioMismatch` proves the probe reports concrete `audio` layer fields when a sampled audio state differs.
- `RuntimeAiLabHeadlessSeek_ReplaysAudioIntentDrainExactly` restores a checkpoint with `NullAudioBackend`, injects the same explicit audio cue request at the same `RuntimeFrame`, then verifies replay produces the same per-frame audio intent drain. Backend cumulative debug counters are diagnostic-only and are not treated as rollback authority.

```text
audio/entity 0/totalPlayRequests expected=3 actual=4
audio/entity 0/intent.lastCueKey expected=dash-start actual=dash-late
audio/entity 0/interaction.lastAudioCueKey expected=barrel-hit actual=barrel-missed
audio/entity 0/physical.bodyPartContactPlayedCount expected=1 actual=3
audio/entity 0/weaponHit.playedCount expected=2 actual=1
audio/entity 1/activeEvent.51.name expected=dash actual=dash-late
```

排查表补充：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| 同一帧状态一致但音效重复播放 | `audio.totalPlayRequests`、`intent.requestCount`、`intent.lastEventId` | seek replay 重复 drain 了旧 intent，或 restore 后 pending audio intent 没有清空。 |
| seek 后声音消失但 gameplay 正常 | `activeEventCount`、`activeEvents.*`、`weaponHit.playedCount`、`physical.bodyPartContactPlayedCount` | audio service active event 未按 checkpoint/selected frame 同步，或表现层 stop/mute 过度清理。 |
| 命中/脚步表现帧正确但声音错 | `weaponHit.*`、`physical.*`、`interaction.*` | audio summary 采样使用 latest/live frame，而不是 selected RuntimeFrame。 |
| AudioService 报错但 HUD 没提示 | `error.count`、`error.*` | backend 错误只进 recent errors，Runtime AI Lab 需要在 Details/Console 显示 audio layer mismatch。 |

## 定位

CharacterTest 的帧同步基础不是 Unity `Transform`、`Rigidbody`、动画时间或 UI 采样帧，而是显式 `RuntimeFrame`、稳定命令顺序、确定性组件和固定 Tick 顺序。

角色移动的权威链路是：

```text
RuntimeFrame
  -> RuntimeCommandBuffer
  -> SetControlIntent
  -> CharacterTestCharacterInputComponent
  -> CharacterTestCharacterMotionSystem
  -> CharacterTestCharacterTransformComponent / MotionState
 -> ViewSync / AnimationSync / Runtime AI Lab Overlay
```

角色动作的权威执行链路还包括 `CharacterTestCharacterActionComponent` 与 `CharacterActionRunner`。动作组件记录当前动作状态，Runner 持有 active instance、local frame、phase、plan id、next instance id 和 track event 进度。Runtime AI Lab 的 `+10 / -10 / Jump / Latest` 排查必须先确认 authority frame 和 authority components 是否一致，再确认 Runner、Unity view、动画、debug capsule 和 overlay 是否已经同步到同一帧。

## 固定帧推进

CharacterTest session 由 `GameSlice.Tick(deltaTime)` 推进。每次 Tick 使用当前 `RuntimeClock.CurrentFrame` 调度 `RuntimeHost.Tick(frame.Value, deltaTime, elapsedSeconds)`，随后调用 `RuntimeClock.Step()` 进入下一帧。

Unity `Update` 只负责驱动固定步进：

- `CharacterTestFixedTickDriver` 累积 Unity delta time，并按固定 `fixedDeltaTime` 调用 simulation step。
- `GameManager.TickSimulationOnce` 在离线模式下调用 `GameSlice.Tick(fixedDeltaTime)`。
- Runtime AI Lab 的单帧控制也应通过同一条 `GameSlice.Tick(fixedDeltaTime)` 权威路径推进，而不是直接移动 Unity view。

关键源码入口：

| 入口 | 职责 |
| --- | --- |
| `Packages/com.vincent.wgameframework/Runtime/RuntimeFrame.cs` | 非负 runtime frame 值。 |
| `Packages/com.vincent.wgameframework/Runtime/RuntimeClock.cs` | `CurrentFrame`、`Step()`、`Reset(frame)`。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/GameSlice.cs` | 用当前 frame 调度 RuntimeHost，并在帧末 Step。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/GameManager.cs` | 固定 tick 驱动、暂停、Runtime AI Lab 单帧入口。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/CharacterTestFixedTickDriver.cs` | Unity delta 到固定 simulation tick 的外层适配。 |

## 移动命令

角色移动不会直接写 Unity `Transform`。输入、玩家控制和 Runtime AI Planner 输出最终都应变成 runtime command，典型命令是 `SetControlIntent`。

`CharacterTestControlIntent` 会把移动轴值确定性量化：

- `MoveXMilli / MoveYMilli`
- `WorldMoveXMilli / WorldMoveYMilli`
- 轴值范围经过 clamp，保存为 `short` milli 值，而不是保存原始 Unity float。
- `WorldMove` 用于已经解析到世界方向的移动意图，Runtime AI Planner 输出移动时走这条路径。
- schema 8 起，`MovementSpeedScaleMilli` 独立表达最终移动速度倍率；它缩放角色的 Walk/Run 目标速度，不再通过缩小移动轴值间接改变速度档位。旧命令未携带该字段时按 `1.0x` 读取。

`CharacterTestRuntimeCommandFactory.SetControlIntent(...)` 负责把这些 milli 轴值和 schema/flags 打包进 `RuntimeCommand` payload；`DecodeControlIntent(...)` 再把 payload 还原成 `CharacterTestControlIntent`。

关键源码入口：

| 入口 | 职责 |
| --- | --- |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Commands/CharacterTestRuntimeCommandTypes.cs` | `CharacterTestControlIntent` 的确定性字段和构造。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Commands/CharacterTestRuntimeCommandFactory.cs` | `SetControlIntent` 打包和 `DecodeControlIntent` 解码。 |

## 命令按帧应用

任务 02 的行为执行后端沿用本章命令阶段。ControlIntent schema 10 的可选 BehaviorControl envelope 与原移动输入一并提交，包含独立朝向、转向倍率、基础接近每步上限及释放 owner token；它不是 TurnInPlace，不隐式抑制平移或攻击。动作终止的实例保护在正式命令应用边界检查。CharacterTest authority snapshot 36 保存该控制扩展，Planner snapshot 16 保存执行表实例与已应用编排回执；普通玩家同步仍采用 BootstrapOnly，未增加周期性全量状态同步。详细合同见[行为执行表后端](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_CARD_PAYLOAD.md#执行表后端2026-09-06)。

`RuntimeCommandBuffer` 是同一帧输入一致性的第一道门：

- `Enqueue` 拒绝早于当前帧的命令。
- 未来帧命令保留在 buffer 中。
- `DrainForFrame(frame, destination)` 只取出 `command.Frame <= frame` 的命令。
- drain 后按 `Frame / SourceId / CommandId / TargetId / Sequence` 稳定排序。

CharacterTest 中 `CharacterTestRuntimeCommandModule.Tick` 会创建当前 `RuntimeFrame`，调用 command buffer drain，然后逐条 `ApplyCommand`。`SetControlIntent` 会进入 `ApplySetControlIntent`，解码后调用：

```csharp
_characterRuntimeModule.TryApplyControlIntent(command.Frame, intent, out message)
```

这一层仍不移动角色，只把本帧输入意图交给角色 runtime 模块。

关键源码入口：

| 入口 | 职责 |
| --- | --- |
| `Packages/com.vincent.wgameframework/Runtime/RuntimeCommandBuffer.cs` | 命令入队、按帧 drain、同帧稳定排序。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Commands/CharacterTestRuntimeCommandModule.cs` | CharacterTest runtime command drain 和 dispatch。 |

## 输入组件写入

`CharacterTestCharacterRuntimeModule.TryApplyControlIntent` 会：

- 根据 `TargetEntityId` 找到角色 entity。
- 校验 `ControlOwner`，当前 `LocalPlayer`、`RemotePlayer`、`AiControlled` 可接收 control intent。
- 刷新战斗状态，并校验角色是否能接收移动或动作输入。
- 用 `CharacterTestCharacterInputComponent.FromDeterministicWithSkillSlotMask(...)` 写入 entity input。

写入的仍是确定性输入组件：

- 本地 move milli。
- 世界 move milli。
- `HasWorldMove`。
- action / dodge / ability slot flags。

这一步是“命令 -> authority input”的转换点。它不写 transform，也不触碰 Unity view。

关键源码入口：

| 入口 | 职责 |
| --- | --- |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterRuntimeModule.cs` | `TryApplyControlIntent` 权限校验和 input 写入。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/CharacterTestCharacterInputComponent.cs` | 确定性输入组件。 |

## 移动求解

`CharacterTestCharacterMotionSystem` 是角色移动 authority 的核心。每帧对角色执行：

1. 从 `entity.Input` 读取 move milli。
2. 如果 `HasWorldMove=true`，直接使用 `WorldMoveXMilli / WorldMoveYMilli`。
3. 否则把本地 move 按控制基准转换为世界方向。
4. 合并 root motion、action movement lock、motion requests、外部速度和当前物理接触状态。
5. 构造 `CharacterKinematicMotionInput`。
6. 调用 `CharacterKinematicMotionSolver.SolvePrePhysics(...)`。
7. 有 CombatPhysics runtime 时，通过 kinematic physics adapter 推进碰撞和位移。
8. 无 CombatPhysics runtime 时，用整数速度按 tickRate 做 fallback 位移。
9. 调用 `CharacterKinematicMotionSolver.CompleteAfterPhysics(...)`。
10. 写回 `CharacterTestCharacterTransformComponent` 和 motion state。
11. 同步角色 combat collider，并清空本帧 motion requests。

权威 transform 使用整数存储：

- `XMillimeters`
- `YMillimeters`
- `ZMillimeters`
- `YawMilliDegrees`

所以角色位置同步的判断基准是 runtime entity 的 transform/motion state，而不是 Unity scene 中看到的模型位置。

关键源码入口：

| 入口 | 职责 |
| --- | --- |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterMotionSystem.cs` | 读取 input，求解移动，写回 transform/motion state。 |
| `Packages/com.vincent.wgameframework/CharacterControl/CharacterKinematicMotion.cs` | noEngine kinematic motion solver。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/CharacterTestCharacterTransformComponent.cs` | authority transform 的毫米 / 毫度存储。 |

## Tick 内顺序

`CharacterTestCharacterRuntimeModule.Tick` 中，移动不是孤立系统。角色一帧内的大致顺序是：

1. 清理上一帧表现请求。
2. Spawn / status / battle state 更新。
3. action system 更新动作状态。
4. root motion system 产出动作移动请求。
5. collision profile / motion effect bridge。
6. motion system 写回 authority transform 和 motion state。
7. perception / combat trace / battle state 使用更新后状态。
8. authority pose / animation system 更新。
9. view sync / animation sync 把表现层同步到 authority state。
10. view bridge 最后同步 authority pose。

因此，如果 Runtime AI Lab seek 后 runtime transform 正确但 view、动画或 overlay 错乱，优先排查 presentation sync，而不是移动求解本身。

## 角色动作 Runner 同步链路

CharacterAction 的帧同步不能只看 `CharacterTestCharacterActionComponent`。组件能描述“当前角色动作状态”，但 seek/rollback 后要继续执行同一个动作实例，还必须恢复 Runner 内部执行状态：

- active action instance id。
- current action id。
- action state / phase。
- local frame。
- plan id 和 next action instance id。
- duration、trace id、finish/reject reason 等调试状态。
- pending/last frame event buffers 的清理边界。

当前低频恢复入口是 `CharacterActionRunnerRestoreState`。`CharacterActionRunner.CreateRestoreState()` 负责捕获 Runner 状态；`CharacterActionRunner.TryRestore(...)` 负责恢复 Runner。该路径用于 rollback / replay / Runtime AI Lab seek 等低频路径，不是高频 Tick 路径。

恢复语义固定为：

1. 先恢复 `CharacterTestCharacterActionComponent`。
2. 从 snapshot 中读取 Runner restore state。
3. `CharacterTestCharacterActionRuntime.TryRestoreRunner(...)` 用 plan cache 解析 action plan。
4. 调用 `CharacterActionRunner.TryRestore(...)` 恢复 active instance 和 local frame。
5. 根据恢复后的 active instance debug state 同步 `CharacterTestCharacterActionComponent`。
6. 下一次 Runner tick 从恢复的 `LocalFrame + 1` 继续。

`TryRestore(...)` 不会调用 `ProcessCurrentFrame`，也不会重放历史 track event。这样可以避免 seek 回旧帧后重复发命中、位移、VFX 或 gameplay request。恢复时会清理 pending operation events 和 last frame events，让下一帧只产生恢复点之后的新事件。

关键源码入口：

| 入口 | 职责 |
| --- | --- |
| `Packages/com.vincent.wgameframework/CharacterAction/CharacterActionRunnerStateTypes.cs` | `CharacterActionRunnerRestoreState` 的低频恢复数据。 |
| `Packages/com.vincent.wgameframework/CharacterAction/CharacterActionRunner.cs` | Runner capture / restore，实现不重放历史事件的恢复语义。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat/CharacterTestCharacterActionRuntime.cs` | CharacterTest action runtime 的 Runner capture / restore 和组件同步。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Network/Snapshot/CharacterTestNetworkSnapshot.cs` | network snapshot schema 12，记录 Runner restore 字段并纳入 hash 覆盖。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterRuntimeModule.cs` | capture/apply character snapshot 时接入 action runtime。 |

### Snapshot / hash 覆盖

CharacterTest network character snapshot 已升级到 schema 12，用于保存动作 Runner 恢复所需字段。状态 hash 也覆盖 Runner 的 active instance id、plan id、next instance id、action id、state、phase 和 local frame。这样 Runtime AI Lab seek 后，如果动作组件看起来一致但 Runner 内部帧不一致，hash 或 focused probe 应能暴露差异。

旧 snapshot 缺少 Runner 字段时，只能从 action component 做 best-effort 重建；没有正在运行的动作时会清空 Runner。这种路径只用于兼容旧数据或诊断，不能作为精准回看成功的证明。

### Runtime AI Lab seek 顺序

Runtime AI Lab 的 seek/review 应按以下顺序处理动作层：

```text
restore checkpoint
  -> apply network character snapshot
  -> restore CharacterAction Runner
  -> replay drained commands to target frame
  -> compute/compare state hash
  -> capture fresh Runtime AI Planner debug snapshot
  -> force presentation sync
  -> refresh overlay from selected frame
  -> readonly frame sync probe
```

如果跳过 Runner restore，`+10 -> -10 -> +10` 容易出现“角色位置回去了，但动作、root motion、动画或命中窗口仍像未来帧”的错乱。

## 表现层边界

Unity view、胶囊、动画和 Runtime AI Lab overlay 都是表现层：

- `CharacterTestCharacterViewSyncSystem` 从 runtime entity 读取 transform、战斗状态、head pad、阵营和表现数据，再调用 view bridge 同步 Unity view。
- Runtime AI Lab overlay 应消费同一帧的 typed debug snapshot 和 authority-derived geometry。
- seek/review 后如果继续使用 smoothing、旧 snapshot 或 live frame overlay input，就会出现画面和权威状态不一致。

表现层不能反向作为 authority，也不能用来判断帧同步是否成功。测试阶段可以用 probe 检查它们是否跟随 authority，但修复方向仍应是“恢复 authority 后强制刷新表现层”。

关键源码入口：

| 入口 | 职责 |
| --- | --- |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterViewSyncSystem.cs` | runtime entity 到 Unity view 的同步。 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/View/CharacterTestCharacterUnityViewBridge.cs` | Unity view bridge。 |

## Runtime AI Lab 精准播放排查表

Runtime AI Lab 的 `+10 / -10 / Jump / Latest` 不应只看 HUD 帧号或 state hash。排查应按层级定位：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| `RuntimeFrame` 不一致 | `GameSlice.CurrentFrame`、timeline selected frame、frame buffer selected frame、selected preview frame | timeline cursor / selected frame 更新顺序错误。 |
| `state hash` 不一致 | 目标 frame 的 checkpoint、drained commands、seek replay 后 hash | checkpoint restore、command replay、snapshot coverage 或 seek 重入问题。 |
| runtime transform 不一致 | entity `CharacterTestCharacterTransformComponent`、motion state、input/action state | seek 没有恢复完整 authority state，或重放命令与原始命令不一致。 |
| action component 一致但 Runner 错 | Runner active instance、plan id、next instance id、local frame、phase | snapshot schema 低于 12、Runner restore 未调用、plan cache/action id 解析失败，或 restore 后组件/Runner 未重新同步。 |
| Runner local frame 正确但旧事件重放 | pending operation events、last frame events、`ProcessCurrentFrame` 调用点 | restore 路径误触发当前帧处理，导致命中、位移、VFX 或 gameplay request 重复发出。 |
| runtime transform 一致但 Unity view 错 | view root transform、capsule/debug marker、presentation smoothing 状态 | seek 后未强制 view sync，或 smoothing 从旧帧插值。 |
| view 一致但 overlay 错 | selected Runtime AI Planner snapshot frame、source/target/move/range overlay input | overlay 使用了 latest/live frame 或旧 snapshot，而不是 selected review frame。 |
| Runner 一致但动画错 | animation request/state、clip key、normalized time、transition frame | animation sync 未跟随 selected frame，或 presentation forced sync 仍使用旧动作表现状态。 |

### Authority replay 与表现诊断的比较边界

Runtime AI Lab 的恢复校验必须拆成两个独立合同，不能用同一个“大而全 sample”定义帧同步是否成功：

- `CharacterTestRuntimeAiLabAuthorityProbe` 是帧同步验收入口。它比较目标 `RuntimeFrame` 与 CharacterTest authority state hash；移动、动作 Runner、combat、Ability、projectile、机制活动状态和必要 RNG 由 authority hash 及对应专项测试覆盖。
- `CharacterTestRuntimeAiLabFrameSyncProbe` 保留为历史命名的复合表现诊断器。它可以定位 Unity view、动画输入、音频、VFX、Overlay、HUD 和当帧诊断摘要差异，但这些差异不得把 authority replay 判定为失败，也不得推动 network snapshot 继续扩字段。

checkpoint 恢复后先验证 authority；正向重放到目标帧后再次验证同一 authority。表现诊断按动画、VFX、音频和 Overlay 的独立工具分别报告。需要逐帧视觉一致性时，应使用独立 `Visual Capture` 术语和数据，不得称为帧同步。

当前文档证明角色移动 authority 链路和 CharacterAction Runner 低频恢复链路的实现结构；Runtime AI Lab 精准播放的完整 seek/checkpoint/review 视觉正确性仍需要继续以源码、focused tests 和 Unity smoke 为准，不能把未验证的 UI/Overlay 修复结论写成事实。

## 后续补充方向

后续可在本文继续补齐：

- root motion 如何进入 movement requests。
- Ability 和 CharacterAction 如何在固定帧中产生 gameplay request。
- Combat hit / projectile / physics query 的帧同步链路。
- Network snapshot、rollback clone、state hash 对角色移动字段的覆盖。
- Runtime AI Lab timeline seek、checkpoint restore、presentation forced sync 的最终实现契约。

## 战斗命中 / 伤害同步链路

本节补齐移动、CharacterAction Runner 之后的第三层事实源。它替代上方“Combat hit / projectile / physics query 后续补充”的占位说明；当前先落地近战命中 / 伤害链路，projectile 和更复杂 physics query 可继续扩展。

战斗命中和伤害的权威链路是：

```text
CharacterAction / Ability / Projectile hit source
  -> CharacterTestCharacterCombatTraceSystem
  -> CharacterTestCombatDamageResolver
  -> CharacterTestCombatDamageApplier
  -> CharacterTestCharacterCombatStateComponent / BattleState / SpecialHit authority fields
  -> CharacterTestNetworkSnapshotParticipant snapshot + state hash
  -> presentation sync / HUD / Runtime AI Lab overlay
```

关键边界：

- `CharacterAction` 或 `Ability` 只产生命中源，例如 `StartHitTrace`、projectile impact 或 gameplay request；它们不是伤害后的最终状态。
- `CharacterTestCharacterCombatTraceSystem` 负责把命中窗口、投射物命中、area/runtime effect 等输入统一为可解析命中。
- `CharacterTestCombatDamageResolver` 负责计算 `HitResolveResult`，例如 damage、trace id、source、hit zone、scatter impact 等。
- `CharacterTestCombatDamageApplier` 才把结果写回权威角色状态：`health`、`hitStun`、`lastDamage`、`lastHitFrame/source/trace`、scatter pressure、death/battle state、special hit / launch 等。
- Unity VFX、飘字、受击表现、HUD 和 Runtime AI Lab overlay 都是只读表现层，必须从 runtime authority state 或同帧 debug snapshot 刷新，不能反向作为权威。

### Snapshot / hash 覆盖

CharacterTest state hash 已覆盖基础 combat 字段：

- `MaxHealth / Health`
- `HitStunFramesRemaining`
- `LastDamage`
- `LastHitFrame`
- `LastHitSourceEntityId`
- `LastHitTraceId`
- `SpecialHitState / SpecialHitPhase / SinceFrame / SourceEntityId / TraceId / LaunchVelocityY / HorizontalImpulse`

Runtime AI Lab 的 seek/replay 需要用这些字段判断回到历史帧后是否真的重建了同一层 combat authority。只比较角色 transform 或 action runner 不够，因为动作和位置一致时，命中后 `health / hitStun / lastHit` 仍可能遗漏恢复。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 在 Lab 操作完成后低频采样，不进入 gameplay Tick。combat 层采样必须至少包含：

- `health / maxHealth`
- `hitStunFramesRemaining`
- `lastDamage`
- `lastHitFrame / lastHitSourceEntityId / lastHitTraceId`
- life/death 状态
- scatter pressure 和 band
- special hit / launch 关键字段

Compare 失败时必须定位到 `combat/entity/field`，例如：

```text
combat/entity 2/health expected=87 actual=100
combat/entity 2/lastHitFrame expected=24 actual=-1
combat/entity 2/specialHitState expected=Launched actual=None
```

Probe 只能只读比较；不得在检查期间调用会刷新 overlay、presentation 或 timeline cursor 的方法。否则诊断会把问题掩盖成“检查后看起来正确”。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| combat state 不一致 | `health`、`hitStun`、`lastDamage`、`lastHitFrame/source/trace`、special hit fields | checkpoint snapshot 没覆盖 combat 字段；seek replay 未重放命中窗口；damage applier 状态恢复顺序错误。 |
| combat state 一致但受击表现错 | Unity character view、animation sync、hit VFX、floating combat text | presentation forced sync 没在 seek 后执行，或仍使用 live/latest frame 的表现事件。 |
| overlay target/hit info 使用错帧 | selected Runtime AI Planner snapshot frame、overlay preview input frame | Review/Seek 后 overlay 消费了 latest/live frame，而不是 selected frame。 |
| 命中发生帧不一致 | CharacterAction runner local frame、active hit trace window、drained command replay | Runner restore 或 replay 命令顺序错，导致 `StartHitTrace` 在不同 RuntimeFrame 发出。 |

### 自动化验收

Runtime AI Lab 帧同步测试应覆盖一条 headless combat replay：

1. 生成攻击者和目标。
2. 在可命中距离启动确定性近战动作。
3. 运行到命中后记录 checkpoint、state hash 和 frame sync sample。
4. 恢复命中前 checkpoint，重放 drained commands 到同一目标帧。
5. 断言 frame、state hash、transform、motion、action runner、combat fields 全部一致。

这条测试不依赖 Game View、相机或 FairyGUI。PlayMode smoke 仍可覆盖真实 Runtime AI Lab UI 操作链，但 combat 正确性的基础验收应优先用 headless 数据比较。

## Gameplay identity / lifecycle 同步链路

Gameplay identity / lifecycle 是 CharacterTest 角色进入通用 GameplayComponentWorld 后的基础身份层。它不等同于 CharacterTest 自己的 `Presence`、`BattleState` 或 `CombatState` 摘要：目标筛选、Ability 规则、机制源归属和阵营关系会读取 `GameplayLifecycleComponent` 与 `GameplayTeamComponent`。因此 Runtime AI Lab seek/replay 不能只验证角色 transform、combat health 或 Runtime AI Planner 目标；还要确认 GameplayComponentWorld 中同一 entity 的生命周期和队伍身份恢复到了目标帧。

权威链路是：

```text
CharacterTestCharacterRuntimeWorld.RegisterGameplayState
  -> GameplayEntityId
  -> GameplayLifecycleComponent
  -> GameplayTeamComponent
  -> Gameplay targeting / ability / mechanic source relation queries
  -> GameplayComponentWorld snapshot / hash
  -> Runtime AI Lab FrameSyncProbe gameplay layer
```

关键边界：

- `GameplayLifecycleComponent.State` 是通用 gameplay 目标候选是否 alive / destroyed 的判断输入。它必须与 CharacterTest battle / combat life state 同步恢复，但不能被表现层或 Runtime AI Planner overlay 反向修正。
- `GameplayTeamComponent.TeamId` 是通用 gameplay 阵营关系输入。它必须与 CharacterTest character team 和行为目标选择一致；seek 后如果 team id 错，Ability target query、mechanic source friendly fire、guard aura 等结果都可能错误。
- Runtime AI Lab probe 的 `gameplay` layer 现在采样 `lifecyclePresent`、`lifecycleState`、`teamPresent` 和 `teamId`。Compare 失败必须定位到 `gameplay/entity/field`，例如：

```text
gameplay/entity 1/lifecycleState expected=Alive actual=Destroyed
gameplay/entity 1/teamId expected=1 actual=2
```

排查表补充：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| Runtime AI Planner 目标看似存在但 Ability 不选中目标 | `gameplay.lifecycleState`、target candidate lifecycle | Gameplay lifecycle 没随 checkpoint 恢复，目标被通用 query 视为 destroyed / none。 |
| CharacterTest team 正确但技能或机制阵营错 | `gameplay.teamId`、CharacterTest team id、mechanic source team query | `GameplayTeamComponent` 漏恢复或 restore 顺序覆盖了 CharacterTest team 映射。 |
| combat life state 与 lifecycle 不一致 | `battle.lifeState`、`combat.lifeState`、`gameplay.lifecycleState` | 伤害销毁只更新了其中一层，或 seek restore 后同步顺序不完整。 |

## Gameplay Attribute 同步链路

Gameplay Attribute 是战斗命中 / 伤害之后必须单独确认的一层。`CharacterTestCharacterCombatStateComponent.Health` 是角色战斗摘要；真正参与通用 gameplay 计算的 HP、Attack、Defense、ScatterPressure 来自 `GameplayAttributeSetComponent`。如果 Runtime AI Lab 只检查 combat health，可能漏掉 `GameplayAttributeSetComponent` 的 current/base 值和角色 combat 摘要不一致的问题。

权威链路是：

```text
CharacterTestCharacterRuntimeWorld.RegisterGameplayState
  -> GameplayAttributeSetComponent(Hp / Attack / Defense / ScatterPressure)
  -> GameplayComponentAbilityRules / CombatEntityLookup / DamageResolver
  -> GameplayAttributeCommandSystem / SetCurrentAttribute
  -> CharacterTestCharacterRuntimeWorld.SyncCombatStateHealth
  -> CombatState summary / Runtime AI Lab FrameSyncProbe
```

关键边界：

- `Hp` 同时存在于 `GameplayAttributeSetComponent` 和 `CharacterTestCharacterCombatStateComponent.Health`。前者是 gameplay attribute authority，后者是 CharacterTest combat summary；seek/replay 后两者都必须与目标帧一致。
- `Attack / Defense` 会参与伤害、护盾、Guard Aura 等链路的规则计算。它们即使不直接显示在 HUD 上，也必须随 checkpoint restore + forward replay 回到同一值。
- `ScatterPressure` 在 attribute set 中有 current/base 值，同时还有 `GameplayScatterPressureComponent` 的恢复、band、break/recovery 状态。Runtime AI Lab 需要同时检查 attribute entry 和 combat/scatter summary。
- Attribute 的 Unity 表现、HUD 数字和 Details 文案只读跟随。不能用当前 HUD 文本反推为权威。

Runtime AI Lab probe 的 `attribute` layer 现在采样每个角色的 attribute entries：

| 字段 | 用途 |
| --- | --- |
| `attribute.count` / `attribute.hash` | 快速定位 attribute set 是否丢失、重复或排序/值变化。 |
| `entry.<id>.baseValue` | 定位 base attribute 是否恢复到目标帧，例如 Attack/Defense 被 aura/buff 初始化链路改错。 |
| `entry.<id>.currentValue` | 定位 current attribute 是否恢复到目标帧，例如 HP damage、ability cost 或 scatter pressure 不一致。 |

Runtime AI Lab probe 还把 `GameplayScatterPressureComponent` 作为独立的 `gameplayScatter` layer 采样。它不是 `combat.scatterPressure` 的重复字段，而是 scatter runtime 组件本体，用于验证恢复、band、break/recovery 计数是否与目标帧一致：

| 字段 | 用途 |
| --- | --- |
| `gameplayScatter.present` | 定位 scatter 组件是否随 checkpoint restore 丢失或误创建。 |
| `currentPressure` / `maxPressure` / `maxDebt` / `currentCohesionLimit` | 定位当前压力、基础上限、可欠债上限和当前韧性上限是否恢复一致。 |
| `recoveryRate` / `recoveryDelayFrames` / `brokenRecoveryDelayFrames` | 定位恢复规则参数是否随 runtime state 恢复。 |
| `currentBand` / `isBroken` | 定位 Stable / Pressed / Cracked / Critical / Broken band 和破防状态。 |
| `lastPressureFrame` / `lastCohesionBreakFrame` | 定位恢复延迟和破防恢复的计时基准是否回到目标帧。 |

失败示例：

```text
attribute/entity 2/hash expected=301 actual=302
attribute/entity 2/entry.1.currentValue expected=87 actual=100
attribute/entity 2/entry.2.baseValue expected=20 actual=25
attribute/entity 2/entry.3.missing expected=present actual=missing
gameplayScatter/entity 2/currentPressure expected=34 actual=88
gameplayScatter/entity 2/currentBand expected=Stable actual=Critical
gameplayScatter/entity 2/lastPressureFrame expected=18 actual=21
```

排查表补充：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| combat health 一致但 attribute HP 不一致 | `attribute.entry.Hp.currentValue`、`SyncCombatStateHealth` 调用点 | restore/replay 后只恢复了 combat summary，没有恢复 gameplay attribute store，或同步顺序反了。 |
| damage 结果不一致但 hit trace 一致 | `attribute.entry.Attack/Defense`、buff/aura modifier 状态 | 攻防属性或 modifier 状态没有随 checkpoint 恢复，导致同一 hit resolve 出不同 damage。 |
| scatter / break 表现不一致 | `attribute.entry.ScatterPressure`、`combat.scatterPressure`、`GameplayScatterPressureComponent` | attribute current、scatter runtime component 和 combat summary 三层有一层漏恢复。 |

## 投射物同步链路

本节补齐移动、CharacterAction Runner、战斗命中 / 伤害之后的第四层事实源。投射物不是 Unity 场景里的 VFX 或 presenter，也不是 Runtime AI Planner 的目标线；它的权威状态由 `CombatProjectileRuntime` 持有，并通过 CharacterTest combat trace 系统进入 snapshot/hash/replay。

投射物权威链路是：

```text
CharacterAction / Ability gameplay request
  -> CharacterTestCharacterCombatTraceSystem
  -> CharacterTestFireballProjectileAdapter / projectile definition + spawn profile
  -> CombatProjectileRuntime active projectile state
  -> CharacterTestNetworkProjectileRuntimeSnapshot / state hash
  -> projectile presenter / VFX / Runtime AI Lab overlay
```

关键边界：

- `CharacterAction` 或 `Ability` 只在确定帧产出 projectile gameplay request；它们不直接移动 Unity projectile object。
- `CharacterTestFireballProjectileAdapter` 负责把 CharacterTest 的 Fireball、Hook、Sword Wave、Tornado 等请求映射到稳定 projectile definition、impact config、motion config 和 spawn profile。
- `CombatProjectileRuntime` 才是 active projectile 的权威：`ProjectileId`、`SpawnSequence`、definition、source entity、action instance、trace id、local frame、spawn/last frame、position、direction、origin、target point、lifetime、speed、tracking target 和 accepted impacts 都在这里维护。
- `CharacterTestNetworkProjectileRuntimeSnapshot` 负责把 active projectile state 写入网络 snapshot / rollback restore；state hash 覆盖 active count 和每个 active projectile 的关键字段。
- Unity projectile presenter、VFX、trail、impact VFX、HUD 和 Runtime AI Lab overlay 都是表现层，只能从 projectile authority state 或同帧 debug snapshot 刷新。

### Snapshot / hash 覆盖

投射物 seek/replay 的最小一致性字段包括：

- runtime metadata：`SchemaVersion`、`DefinitionHash`、`NextProjectileId`。
- identity：`ProjectileId`、`SpawnSequence`、`DefinitionId`、`DefinitionStableId`。
- source：`SourceEntityId`、`ActionId`、`ActionInstanceId`、`TraceId`。
- timing：`LocalFrame`、`SpawnFrame`、`LastFrame`、`AgeFrames`。
- geometry：`Position`、`Direction`、`Origin`、`TargetPoint`、`HasTargetPoint`。`Direction` 必须按 runtime authority 原值保存，snapshot / restore 只能校验非零，不能重新 normalize 或重算；否则 `BallisticArcToTarget` 这类 `Direction * LaunchSpeed * age` 采样会在回看后出现微小漂移。
- movement/targeting：`EffectiveLifetimeFrames`、`LaunchSpeedPerFrame`、`HasTrackingTarget`、`TrackingTargetEntityId`。
- impact history：`AcceptedImpactCount` and accepted impact target/frame hash。

Runtime AI Lab 的 `+10 / -10 / +10` 如果只比较角色 transform、action runner 或 combat health，仍可能漏掉 projectile 错乱。例如 Fireball 的 projectile local frame 或 position 错了，角色本体和 HUD 帧号可能仍看起来正确。因此 probe 必须把 projectile 作为独立 layer 比较，并输出到具体字段：

```text
projectile/entity 101/localFrame expected=4 actual=5
projectile/entity 101/positionXRaw expected=1000 actual=2000
projectile/entity 0/activeCount expected=1 actual=0
```

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 现在在低频 Lab 操作完成后采样 projectile state。它只读 `CombatProjectileRuntimeState`，不刷新 presenter，不改 overlay，不改 timeline cursor。

当前 focused tests 覆盖两类风险：

1. 人工构造 projectile mismatch，确认 probe 能定位 `projectile/entity/field`。
2. Headless projectile replay：启动指定 projectile 行为，记录 active projectile checkpoint，恢复 checkpoint 后正向重放到目标帧，并断言 projectile sample 与第一次运行一致。

这条测试不依赖 Game View、相机、FairyGUI 或 Unity projectile presenter。它验证的是 projectile authority state 可以通过 checkpoint restore + forward replay 回到同一帧。

当前 projectile replay matrix：

| 覆盖项 | 代表行为 | motion / policy | 验证重点 |
| --- | --- | --- | --- |
| 基础直线投射物 | Fireball | `Linear` / `DirectHit` | active projectile identity、local frame、position/direction、target point、lifetime、snapshot restore。 |
| 横向摆动投射物 | Fireball Sway | `SwayForward` / `DirectHit` | 非直线 motion 的 position/direction 在 checkpoint restore + forward replay 后保持一致。 |
| 抛物目标点投射物 | Fireball Lob | `ArcToTarget` / `AreaPulseOnImpact` | target point、arc position、area policy projectile identity 在 checkpoint restore + forward replay 后保持一致。 |
| 返回型投射物 | Fireball Return | `BoomerangReturn` / `DirectHit` | 返回 motion 的 source/origin、position/direction、local frame 在 checkpoint restore + forward replay 后保持一致。 |
| 追踪目标投射物 | Fireball Homing | `HomingToTarget` / `DirectHit` | locked target、`HasTrackingTarget`、tracking target entity id、homing position/direction 在 checkpoint restore + forward replay 后保持一致。 |
| 贝塞尔曲线投射物 | Fireball Bezier | `BezierToTarget` / `DirectHit` | target point、Bezier 控制点采样后的 position/direction、local frame 在 checkpoint restore + forward replay 后保持一致。 |
| 游戏重力弹道投射物 | Poison Bottle | `BallisticArcToTarget` / `DirectHit -> poison pool` | target point、effective lifetime、solved launch speed、ballistic position/direction 在 checkpoint restore + forward replay 后保持一致；命中后生成的 poison pool 作为 mechanic source 继续进入 snapshot/hash/probe。 |
| 控制型 OnHitBuff 投射物 | Stun Bolt | `Linear` / `DirectHit -> OnHitBuff(Stunned)` | projectile 命中后 HP 可保持不变，但目标 buff/status count/hash 与 `Stunned` 投射结果必须在 checkpoint restore + forward replay 后保持一致。 |
| 穿透命中历史 | Flame Lance | `Linear` / `DirectHit` / pierce once per target | accepted impact count/hash 在 checkpoint restore + forward replay 后保持一致，避免 seek 后重复命中或漏掉已命中目标。 |
| 多枚 active 投射物 | Meteor Barrage | `Linear` / `AreaPulseOnImpact` / staggered launches | 同一 checkpoint 内多枚 active meteor 的 projectile identity、local frame、target point、position/direction 在 forward replay 后保持一致。 |
| 持续伤害 / 吸引投射物 | Tornado | `SwayForward` / periodic damage tick / active attraction field | active Tornado projectile identity、local frame、position/direction、damage tick target count、target combat state、target-frame attraction interaction count/hash 在 checkpoint restore + forward replay 后保持一致。 |
| 瞬时多目标连锁效果 | Chain Lightning | `CharacterAction gameplay request -> chained combat targets` | Chain Lightning 不是 active projectile runtime；需要验证同一帧的连锁目标数、弧段事件数量、弧段事件 hash 和每条弧段事件的字段级 sample 在 checkpoint restore + forward replay 后保持一致。 |
| 短生命周期战斗投射物 | Sword Wave | `Linear` / `DirectHit` / staged short waves | active Sword Wave projectile 的 identity、effective lifetime、position/direction 在 forward replay 后保持一致；到期移除后的 active count 也必须一致。 |
| 命中后拉拽投射物 | Hook Chain | `Linear` / `DirectHit -> hook pull` | 飞行中 projectile checkpoint 正向重放到命中帧后，active projectile sample 和生成的 hook pull state hash/count 必须一致；active hook pull checkpoint 再向后重放数帧也必须一致；Runtime AI Lab probe 必须能定位 `hookPull` 层字段。 |
| 部署派生投射物 | Arc Mine | `BallisticArcToTarget` / `DirectHit -> drop projectile -> deployed mine` | carrier projectile checkpoint 正向重放到 drop projectile 帧后 active projectile sample 必须一致；drop checkpoint 正向重放到 deployed mine 后，Arc Mine state hash/count 必须一致；active mine checkpoint 再向后重放数帧也必须一致；Runtime AI Lab probe 必须能定位 `arcMine` 层字段。 |
| 部署后持续发射投射物 | Rune Turret | `BallisticArcToTarget` / `deploy turret -> repeated shot projectile` | deploy carrier checkpoint 正向重放到 active turret 后，Rune Turret state hash/count 必须一致；active turret checkpoint 正向重放到 shot projectile 帧后，active projectile sample 与 Rune Turret state 仍必须一致；shot checkpoint 再向后重放数帧也必须一致；Runtime AI Lab probe 必须能定位 `runeTurret` 层字段。 |
| 延迟 channel 场域 | Area Confinement | `Channel -> pending zone -> mechanic source` | pending channel checkpoint 正向重放到 active zone 后，Area Confinement pending/active source state hash/count 必须一致；active zone checkpoint 再向后重放数帧也必须一致。 |
| 命中后持续毒池 | Poison Bottle Pool | `DirectHit -> mechanic source poison pool` | active poison pool source 的 id、definition、owner、center、start/end frame、trace id、poison tick summary 和 tick target count 在 checkpoint restore + forward replay 后保持一致。 |
| 临时实体护盾 | Physical Shield | `Channel / Ability -> physical shield body -> CombatShieldRuntime` | active shield source、shield entity id、physics body id、center、end frame、shield potency state 和 shield tick summary 在 checkpoint restore + forward replay 后保持一致。 |

后续新增 projectile、channel、deployed mechanic source 或持续发射机制时，必须在本矩阵追加一行，并补一条 checkpoint restore + forward replay focused test；不能只依赖角色 transform、action runner 或 combat health 一致。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| projectile active count 不一致 | `CombatProjectileRuntimeState.ActiveProjectiles`、checkpoint projectile snapshot、seek replay frame | checkpoint 未覆盖 projectile runtime，或 seek restore 后没有调用 projectile restore。 |
| projectile id/sequence 不一致 | `NextProjectileId`、`ProjectileId`、`SpawnSequence` | restore 后继续沿用旧 runtime allocator，或 replay 重复生成 projectile。 |
| projectile position/local frame 不一致 | `LocalFrame`、`Position`、`Direction`、`LaunchSpeedPerFrame` | projectile runtime 没有从 checkpoint 恢复，或 replay 到目标帧的 tick 次数错误。 |
| projectile impact history 不一致 | `AcceptedImpactCount`、accepted impact target/frame hash | impact history 未进 snapshot，或 seek 时重复/漏放命中帧。 |
| projectile authority 一致但 VFX/trail 错 | Unity projectile presenter state、VFX instance、trail state | seek 后 presentation 没有强制同步，或 presenter 仍在从 live frame 插值。 |
| overlay line/range 正确但 projectile 错 | Runtime AI Planner snapshot vs projectile runtime state | overlay 只证明决策几何正确，不能证明 projectile runtime 正确。 |

## Fireball Sway / SwayForward 横向摆动同步链路

Fireball Sway 的关键不是目标点，而是 `SwayForward` motion 根据同一条 runtime authority 输入在同一 `LocalFrame` 采样出同一条摆动路径。Seek / review 不能从 Unity trail、当前 view 朝向或屏幕表现重建摆动相位；必须恢复 projectile runtime state，再按固定帧正向 replay 到目标帧。

权威链路是：

```text
Fireball Sway CharacterAction / Ability request
  -> CharacterTestCharacterCombatTraceSystem projectile spawn
  -> CombatProjectileRuntime active projectile state
  -> Origin + Direction + LocalFrame + SwayForward motion config
  -> sampled Position / sampled Direction
  -> CharacterTestNetworkProjectileRuntimeSnapshot / projectile state hash
  -> Runtime AI Lab probe / projectile presentation
```

关键边界：

- `LocalFrame` 是摆动相位的主索引。恢复到 checkpoint 后继续 replay 时，目标帧的 `LocalFrame` 必须与第一次运行完全一致。
- `Origin`、`Direction`、`LaunchSpeedPerFrame` 和 projectile definition / motion config 是采样输入；`Position` 与 `Direction` 是采样输出。Runtime AI Lab probe 需要比较输入和输出，才能判断是 restore 输入错，还是 replay 采样错。
- Fireball Sway 可以不依赖 `TargetPoint`；把它按 ArcToTarget / Bezier 类问题排查会漏掉真正的相位漂移。
- Unity trail、粒子尾迹、projectile view forward 和场景调试线都是表现层，只能从 selected frame 的 projectile sample 刷新，不能反向修正 authority。

### Snapshot / hash 覆盖

`CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash 必须覆盖：

- `projectile.definitionStableId`
- `projectile.sourceEntityId`
- `projectile.origin.*`
- `projectile.position.*`
- `projectile.direction.*`
- `projectile.localFrame`
- `projectile.launchSpeedPerFrame`
- `projectile.effectiveLifetimeFrames`
- `projectile.acceptedImpactCount`
- `projectile.acceptedImpactHash`

Runtime AI Lab 的 `CharacterTestRuntimeAiLabFrameSyncProbe` 对 Fireball Sway 的最低验收是：第一次运行到目标帧的 sample 与从 checkpoint 正向 replay 到同一帧的 sample 在上述字段上完全一致。Compare 失败必须定位到 `projectile/<id>/localFrame`、`origin*`、`position*` 或 `direction*` 这类具体字段。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysProjectileSwayVariantExactly` 覆盖 Fireball Sway 的核心回看链路：

1. 生成 `Fireball Sway` active projectile。
2. 在 projectile 已激活后保存 checkpoint，并确认 checkpoint 仍有 active projectile authority state。
3. 正向 replay 到目标帧。
4. 断言 active projectile 仍存在，stable id 是 `Fireball Sway`。
5. 断言 source entity、origin、local frame、sampled position 和 sampled direction 与第一次运行目标帧完全一致，再交给 FrameSyncProbe 比较完整 projectile sample。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| Sway projectile 数量一致但摆动相位不同 | `projectile.localFrame`、checkpoint frame、replay tick count | seek 从错误 checkpoint 或错误 tick 数 replay，导致相位错位。 |
| `LocalFrame` 一致但位置漂移 | `projectile.origin*`、`direction*`、definition / motion config hash | restore 输入不完整，或 replay 使用了不同 motion config。 |
| authority sample 一致但 trail / view 不一致 | Projectile Unity presenter、trail state、selected frame | 表现层还在消费 live frame、旧插值或未在 seek 后强制同步。 |
| 被误按 target-point projectile 排查 | `HasTargetPoint`、motion policy、definition stable id | Sway 的核心是 origin / direction / local frame，不是 ArcToTarget / Bezier 目标点。 |

## Fireball Lob / ArcToTarget 目标点弧线同步链路

Fireball Lob 的关键同步点是生成帧解析出的 `TargetPoint`、`EffectiveLifetimeFrames` 和 `ArcToTarget` 采样输入必须随 projectile authority state 一起恢复。Seek / review 不能在目标帧重新读取当前 target transform，也不能用 Unity arc gizmo、落点标记或 VFX 反推弧线；只能恢复 checkpoint 中的 projectile runtime state，再由 deterministic projectile runtime 正向 replay 到 selected `RuntimeFrame`。

权威链路是：

```text
Fireball Lob CharacterAction / Ability request
  -> CharacterTestCharacterCombatTraceSystem projectile aim / target point
  -> CombatProjectileRuntime active projectile state
  -> HasTargetPoint + TargetPoint + Origin + Direction + LocalFrame + EffectiveLifetimeFrames
  -> ArcToTarget motion sampling
  -> AreaPulseOnImpact projectile identity / impact policy
  -> CharacterTestNetworkProjectileRuntimeSnapshot / projectile state hash
  -> Runtime AI Lab probe / projectile presentation
```

关键边界：

- `TargetPoint` 是 Fireball Lob 的固定落点输入。它必须来自 projectile spawn request / runtime state，不能在 seek 后用当前目标位置重新计算。
- `EffectiveLifetimeFrames` 决定 `ArcToTarget` 在同一 `LocalFrame` 的进度采样；`LaunchSpeedPerFrame` 仍作为 projectile snapshot/probe 字段保持一致，但 Fireball Lob 的 `ArcToTarget` 可以为 0，不应按 `BallisticArcToTarget` 的已解算发射速度语义要求非零。
- `AreaPulseOnImpact` 不改变飞行中同步规则；它要求 projectile identity、accepted impact history 和到达 impact / expire 帧时的 area pulse 输出可复现。impact / interaction 输出另见 `Interaction / Explosion Field 同步链路`。
- Unity 弧线预览、爆炸 VFX、地面落点提示和 Runtime AI Lab overlay 都是表现层，只能消费 selected frame 的 projectile sample。

### Snapshot / hash 覆盖

`CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash 必须覆盖：

- `projectile.definitionStableId`
- `projectile.sourceEntityId`
- `projectile.hasTargetPoint`
- `projectile.targetPoint.*`
- `projectile.origin.*`
- `projectile.position.*`
- `projectile.direction.*`
- `projectile.localFrame`
- `projectile.effectiveLifetimeFrames`
- `projectile.launchSpeedPerFrame`
- `projectile.acceptedImpactCount`
- `projectile.acceptedImpactHash`

Runtime AI Lab 的 `CharacterTestRuntimeAiLabFrameSyncProbe` 对 Fireball Lob 的最低验收是：第一次运行到目标帧的 sample 与从 checkpoint 正向 replay 到同一帧的 sample 在上述字段上完全一致。Compare 失败必须定位到 `projectile/<id>/targetPointXRaw`、`effectiveLifetimeFrames`、`launchSpeedPerFrameRaw`、`position*`、`direction*` 或 `localFrame` 这类具体字段。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysProjectileArcAreaVariantExactly` 覆盖 Fireball Lob 的核心回看链路：

1. 生成 `Fireball Lob` active projectile。
2. 在 projectile 已激活后保存 checkpoint，并确认 checkpoint 仍有 active projectile authority state。
3. 正向 replay 到目标帧。
4. 断言 active projectile 仍存在，stable id 是 `Fireball Lob`，`HasTargetPoint=true`。
5. 断言 checkpoint 与目标帧的 `TargetPointX/Y/ZRaw` 保持不变，证明落点没有被 seek 后重算。
6. 断言 replay 后的 `TargetPointX/Y/ZRaw`、`EffectiveLifetimeFrames`、`LaunchSpeedPerFrameRaw`（允许 `ArcToTarget` 为 0）、`LocalFrame`、sampled `Position` 和 `Direction` 与第一次运行目标帧完全一致，再交给 FrameSyncProbe 比较完整 projectile sample。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| Arc projectile 数量一致但落点不同 | `projectile.hasTargetPoint`、`projectile.targetPoint*` | snapshot / restore 没覆盖 target point，或 seek 后从当前目标位置重算落点。 |
| target point 一致但弧线位置漂移 | `projectile.localFrame`、`effectiveLifetimeFrames`、definition hash；`launchSpeedPerFrameRaw` 只要求恢复一致 | replay tick 次数不一致，或 ArcToTarget 采样输入不是同一份数据。 |
| 飞行中 sample 一致但 area pulse 结果不同 | `acceptedImpactCount/hash`、`interaction.lastFrameEventCount/hash` | projectile 飞行恢复正确，但 impact / expiration / area pulse 输出帧或 interaction 记录不同。 |
| authority sample 一致但落点提示 / 爆炸 VFX 错 | projectile Unity presenter、Interaction VFX、selected frame | 表现层还在消费 live frame、旧插值或未在 seek 后强制同步。 |
| 被误按 Bezier 或 ballistic 排查 | motion policy、`effectiveLifetimeFrames`、`launchSpeedPerFrameRaw` | ArcToTarget 是固定目标点弧线，不使用 Bezier 控制点，也不是游戏重力解算；`launchSpeedPerFrameRaw=0` 对 Fireball Lob 可合法。 |

## Fireball Return / BoomerangReturn 返回同步链路

Fireball Return 的关键同步点不是“飞回来时看起来朝向施法者”，而是返回 motion 的 source entity、origin anchor 和 local frame 必须在 checkpoint / restore / replay 后保持一致。Seek 回看不能用当前 caster transform、Unity 角色对象或当前屏幕位置重新计算回程锚点；它只能恢复 `CombatProjectileRuntimeState` 中的 source / origin，再由 deterministic projectile runtime 正向推进。

权威链路是：

```text
Fireball Return CharacterAction / Ability request
  -> CharacterTestCharacterCombatTraceSystem projectile spawn
  -> CombatProjectileRuntime active projectile state
  -> SourceEntityId + Origin + Direction + LocalFrame
  -> BoomerangReturn motion sampling
  -> CharacterTestNetworkProjectileRuntimeSnapshot / projectile state hash
  -> Runtime AI Lab probe / projectile presentation
```

关键边界：

- `SourceEntityId` 是返回 motion 的身份锚点。它必须是 runtime combat entity id，不能来自 Unity view instance、当前 local player、screen label 或目标锁定结果。
- `Origin` 是生成帧保存下来的回程位置锚点。Return projectile 的 seek/replay 不能在目标帧重新读取 caster 当前位置来替代它。
- `LocalFrame` 决定返回曲线 / 往返采样进度。即使 source / origin 一致，local frame 错一帧也会让 position / direction 漂移。
- Return projectile 可以不依赖 target point；检查重点是 source/origin/local frame/position/direction，而不是把它误判成 ArcToTarget / Bezier 类落点同步。
- Unity trail、回旋 VFX、角色手部 socket 和 Runtime AI Lab overlay 都是表现层，只能从 selected frame 的 projectile sample 刷新，不能反向修正 source / origin。

### Snapshot / hash 覆盖

`CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash 必须覆盖：

- `projectile.definitionStableId`
- `projectile.sourceEntityId`
- `projectile.origin.*`
- `projectile.position.*`
- `projectile.direction.*`
- `projectile.localFrame`
- `projectile.acceptedImpactCount`
- `projectile.acceptedImpactHash`

Runtime AI Lab 的 `CharacterTestRuntimeAiLabFrameSyncProbe` 对 Return projectile 的最低验收是：第一次运行到目标帧的 sample 与从 checkpoint 正向 replay 到同一帧的 sample 在上述字段上完全一致。Compare 失败必须定位到 `projectile/<id>/sourceEntityId`、`origin*`、`position*`、`direction*` 或 `localFrame` 这类具体字段。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysProjectileBoomerangReturnVariantExactly` 覆盖 Fireball Return 的核心回看链路：

1. 生成 `Fireball Return` active projectile。
2. 在 projectile 已激活后保存 checkpoint。
3. 正向 replay 到目标帧。
4. 断言 active projectile 仍存在，stable id 是 `Fireball Return`。
5. 断言 checkpoint 到目标帧之间的 `SourceEntityId` 和 `OriginX/Y/ZRaw` 保持稳定。
6. 断言 replay 后的 `SourceEntityId`、`OriginX/Y/ZRaw`、`EffectiveLifetimeFrames`、`LocalFrame`、sampled `Position` 和 `Direction` 与第一次运行目标帧完全一致，再交给 FrameSyncProbe 比较完整 projectile sample。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| Return projectile 数量一致但回程中心漂移 | `projectile.sourceEntityId`、`projectile.origin*` | snapshot / restore 没覆盖 origin，或 seek 后从 caster 当前 transform 重新计算回程锚点。 |
| source / origin 一致但位置漂移 | `projectile.localFrame`、`direction`、`position` | replay tick 次数错，或 restore 后 local frame 从错误帧继续推进。 |
| Return 被当成 target-point 投射物排查 | `HasTargetPoint`、motion policy、definition stable id | Return 的权威锚点是 source / origin，不是 ArcToTarget / Bezier 的 target point。 |
| authority sample 一致但 trail / socket 错 | projectile Unity presenter、socket-follow VFX、selected frame | 表现层仍消费 live frame 或旧插值，没有强制同步到 selected RuntimeFrame。 |

## Homing 投射物追踪目标同步链路

Homing 投射物的关键同步点不是“飞行轨迹看起来接近目标”，而是 active projectile 在生成帧锁定的追踪目标身份必须在 checkpoint / restore / replay 后保持一致。Seek 回看不能在目标帧重新按距离、朝向或 Unity 场景对象再选一次目标；它只能恢复 `CombatProjectileRuntimeState` 中已经记录的 tracking target，然后让 deterministic projectile runtime 从同一 state 正向推进。

权威链路是：

```text
Homing CharacterAction / Ability request
  -> CharacterTestCharacterCombatTraceSystem projectile aim / locked target
  -> CombatProjectileRuntime active projectile state
  -> HasTrackingTarget + TrackingTargetEntityId
  -> CharacterTestNetworkProjectileRuntimeSnapshot / projectile state hash
  -> Runtime AI Lab probe / projectile presentation
```

关键边界：
- `HasTrackingTarget` 只说明该 projectile 使用目标追踪模式；真正的可恢复身份是 `TrackingTargetEntityId`。
- `TrackingTargetEntityId` 必须来自 runtime combat entity id。它不能来自 Unity view instance、Transform 引用、screen label 或当前最近目标查询。
- Homing 每帧 position / direction 会随目标位置变化，但 replay 到同一 `RuntimeFrame` 时，target entity id、source/origin、position/direction、local frame、accepted impact history 都必须一致。
- Unity trail、朝向插值、命中特效和 Runtime AI Lab overlay 都是表现层，只能从同一帧 projectile sample 刷新，不能反向修正 authority state。

### Snapshot / hash 覆盖

`CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash 必须覆盖：

- `projectile.hasTrackingTarget`
- `projectile.trackingTargetEntityId`
- `projectile.position.*`
- `projectile.direction.*`
- `projectile.localFrame`
- `projectile.acceptedImpactCount`
- `projectile.acceptedImpactHash`

Runtime AI Lab 的 `CharacterTestRuntimeAiLabFrameSyncProbe` 对 homing projectile 的最低验收是：第一次运行到目标帧的 sample 与从 checkpoint 正向 replay 到同一帧的 sample 在上述字段上完全一致。Compare 失败必须定位到 `projectile/<id>/trackingTargetEntityId`、`position*` 或 `direction*` 这类具体字段，而不是只报告“projectile mismatch”。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysProjectileHomingVariantExactly` 覆盖 Homing Fireball 的核心回看链路：

1. 用 locked target 生成 `Fireball Homing` active projectile。
2. 在 projectile 已激活后保存 checkpoint。
3. 正向 replay 到目标帧。
4. 断言 active projectile 仍存在，stable id 是 `Fireball Homing`，`HasTrackingTarget=true`，`TrackingTargetEntityId > 0`。
5. 断言 checkpoint 与目标帧保留同一 source entity 和 tracking target identity，避免 seek 后重新选目标。
6. 断言 replay 后的 `TrackingTargetEntityId`、`EffectiveLifetimeFrames`、`LocalFrame`、`position.*` 和 `direction.*` 与第一次运行目标帧完全一致，再交给 FrameSyncProbe 比较完整 projectile sample。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| homing projectile 数量一致但目标不同 | `projectile.hasTrackingTarget`、`projectile.trackingTargetEntityId` | snapshot / restore 没覆盖 tracking target，或 seek 后重新解析了当前最近目标。 |
| tracking target 一致但位置漂移 | target runtime transform、projectile `localFrame`、`direction`、`position` | target state 没恢复到同一帧，或 replay tick 次数 / direction restore 有偏差。 |
| authority sample 一致但 trail / 朝向错 | projectile Unity presenter、smoothing、selected frame | 表现层仍消费 live frame 或旧插值，没有强制同步到 selected RuntimeFrame。 |
| overlay 目标线正确但 projectile mismatch | Runtime AI Planner snapshot vs projectile sample | 决策几何正确不等于 homing projectile runtime 正确，需要检查 projectile probe 层。 |

## Bezier 投射物目标点曲线同步链路

Bezier 投射物的关键同步点是生成帧确定的 target point 和曲线采样输入必须作为 projectile authority state 恢复，而不是 seek 后从当前目标、Unity 场景位置或屏幕标记重新计算。Bezier 的控制点来自 projectile definition / motion config，目标点来自 spawn request；checkpoint / restore 只允许恢复这些确定性输入，再由 projectile runtime 正向采样同一条曲线。

权威链路是：

```text
Bezier CharacterAction / Ability request
  -> CharacterTestCharacterCombatTraceSystem projectile aim / target point
  -> CombatProjectileRuntime active projectile state
  -> HasTargetPoint + TargetPoint + Origin + Direction + LocalFrame
  -> projectile definition Bezier control offsets
  -> CharacterTestNetworkProjectileRuntimeSnapshot / projectile state hash
  -> Runtime AI Lab probe / projectile presentation
```

关键边界：

- `TargetPoint` 是 Bezier 曲线终点的 gameplay authority 输入。它必须来自 projectile spawn request / runtime state，不能在 seek 后重新用当前 target transform 推导。
- Bezier 控制点是 definition / motion config 的确定性数据；Runtime AI Lab 不需要把控制点复制进每帧 sample，但 replay 到同一 `RuntimeFrame` 时，`DefinitionStableId`、`Origin`、`TargetPoint`、`LocalFrame`、`Position` 和 `Direction` 必须完全一致。
- `HasTargetPoint=false` 的 Bezier sample 是错误信号；这表示曲线终点没有进入可恢复状态。
- Unity trail、曲线 gizmo、命中特效和 Runtime AI Lab overlay 都是表现层，只能从 selected frame 的 projectile sample 刷新，不能反向修正 target point 或曲线位置。

### Snapshot / hash 覆盖

`CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash 必须覆盖：

- `projectile.definitionStableId`
- `projectile.hasTargetPoint`
- `projectile.targetPoint.*`
- `projectile.origin.*`
- `projectile.position.*`
- `projectile.direction.*`
- `projectile.localFrame`
- `projectile.acceptedImpactCount`
- `projectile.acceptedImpactHash`

Runtime AI Lab 的 `CharacterTestRuntimeAiLabFrameSyncProbe` 对 Bezier projectile 的最低验收是：第一次运行到目标帧的 sample 与从 checkpoint 正向 replay 到同一帧的 sample 在上述字段上完全一致。Compare 失败必须定位到 `projectile/<id>/targetPointXRaw`、`position*`、`direction*` 或 `localFrame` 这类具体字段。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysProjectileBezierVariantExactly` 覆盖 Bezier Fireball 的核心回看链路：

1. 生成 `Fireball Bezier` active projectile。
2. 在 projectile 已激活后保存 checkpoint。
3. 正向 replay 到目标帧。
4. 断言 active projectile 仍存在，stable id 是 `Fireball Bezier`，`HasTargetPoint=true`。
5. 断言 checkpoint 与目标帧保留同一 source entity、origin anchor 和 target point，避免 seek 后重新按当前目标位置解算曲线。
6. 断言 replay 后的 `TargetPointX/Y/ZRaw`、`EffectiveLifetimeFrames`、`LocalFrame`、`position.*` 和 `direction.*` 与第一次运行目标帧完全一致，再交给 FrameSyncProbe 比较完整 projectile sample。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| Bezier projectile 数量一致但落点不同 | `projectile.hasTargetPoint`、`projectile.targetPoint*` | snapshot / restore 没覆盖 target point，或 seek 后重新从当前目标位置计算落点。 |
| target point 一致但曲线位置漂移 | `projectile.localFrame`、`origin`、`direction`、definition hash | replay tick 次数不一致，或 projectile definition / Bezier control offsets 不是同一份数据。 |
| authority sample 一致但曲线 trail 错 | projectile Unity presenter、trail state、selected frame | 表现层仍消费 live frame 或旧插值，没有强制同步到 selected RuntimeFrame。 |

## BallisticArc 投射物同步链路

Poison Bottle 的 `BallisticArcToTarget` 关键同步点是生成帧解算出的目标点、有效飞行寿命和发射速度。Seek / review 不能在目标帧重新读取当前 target transform，也不能从 Unity 抛物线预览或地面落点标记反推弹道；只能恢复 checkpoint 中的 projectile runtime state，再由 deterministic projectile runtime 正向采样到 selected `RuntimeFrame`。

权威链路是：

```text
Poison Bottle CharacterAction / Ability request
  -> CharacterTestCharacterCombatTraceSystem projectile aim / target point
  -> ballistic lifetime / launch speed solve
  -> CombatProjectileRuntime active projectile state
  -> CharacterTestNetworkProjectileRuntimeSnapshot / projectile state hash
  -> Runtime AI Lab probe / projectile presentation
```

关键边界：

- `TargetPoint` 是弹道落点输入，必须来自 projectile spawn request / runtime state。
- `EffectiveLifetimeFrames` 和 `LaunchSpeedPerFrame` 是弹道解算结果；它们决定同一 `LocalFrame` 下 `Position` / `Direction` 的采样。
- `Origin`、`TargetPoint`、`EffectiveLifetimeFrames` 和 `LaunchSpeedPerFrame` 在 projectile 生成后必须保持稳定；seek 后重新解算任意一个字段都会造成 `+10 / -10 / +10` 漂移。
- 命中或到期后生成的 Poison Bottle Pool 属于后续 mechanic source，同步边界见 `Poison Bottle Pool 同步链路`；飞行阶段先证明 projectile authority 自身一致。

### Snapshot / hash 覆盖

`CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash 必须覆盖：

- `projectile.definitionStableId`
- `projectile.sourceEntityId`
- `projectile.hasTargetPoint`
- `projectile.targetPoint.*`
- `projectile.origin.*`
- `projectile.position.*`
- `projectile.direction.*`
- `projectile.localFrame`
- `projectile.effectiveLifetimeFrames`
- `projectile.launchSpeedPerFrame`
- `projectile.acceptedImpactCount`
- `projectile.acceptedImpactHash`

Runtime AI Lab 的 `CharacterTestRuntimeAiLabFrameSyncProbe` 对 BallisticArc projectile 的最低验收是：第一次运行到目标帧的 sample 与从 checkpoint 正向 replay 到同一帧的 sample 在上述字段上完全一致。Compare 失败必须定位到 `projectile/<id>/targetPointXRaw`、`effectiveLifetimeFrames`、`launchSpeedPerFrameRaw`、`position*`、`direction*` 或 `localFrame` 这类具体字段。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysProjectileBallisticArcVariantExactly` 覆盖 Poison Bottle 的飞行阶段：

1. 生成 `Poison Bottle` active projectile。
2. 在 projectile 已激活后保存 checkpoint，并确认 checkpoint 已保存 source entity、origin、target point、effective lifetime 和 solved launch speed。
3. 从 checkpoint 正向 replay 到目标帧。
4. 断言 active projectile 仍存在，stable id 是 `Poison Bottle`，`HasTargetPoint=true`。
5. 断言 checkpoint 与目标帧的 `OriginX/Y/ZRaw`、`TargetPointX/Y/ZRaw`、`EffectiveLifetimeFrames` 和 `LaunchSpeedPerFrameRaw` 保持稳定，避免 seek 后重新解算落点或速度。
6. 断言 replay 后的 `TargetPointX/Y/ZRaw`、`EffectiveLifetimeFrames`、`LaunchSpeedPerFrameRaw`、`LocalFrame`、sampled `Position` 和 `Direction` 与第一次运行目标帧完全一致，再交给 FrameSyncProbe 比较完整 projectile sample。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| Ballistic projectile 数量一致但落点不同 | `projectile.hasTargetPoint`、`projectile.targetPoint*` | snapshot / restore 没覆盖 target point，或 seek 后重新从当前目标位置计算落点。 |
| target point 一致但飞行位置漂移 | `projectile.localFrame`、`effectiveLifetimeFrames`、`launchSpeedPerFrameRaw` | replay tick 次数不一致，或弹道解算结果没有进入 checkpoint。 |
| launch speed 一致但方向漂移 | `projectile.origin*`、`projectile.direction*`、definition hash | restore 输入不完整，或 replay 使用了不同 motion config。 |
| projectile authority 一致但毒池不同 | impact / expiration frame、`poisonPool` source count | projectile 飞行恢复正确，但命中 / 到期后的 mechanic source 链路有独立问题。 |
| authority sample 一致但落点提示 / 抛物线 VFX 错 | projectile Unity presenter、VFX trail、selected frame | 表现层仍消费 live frame、旧插值或未在 seek 后强制同步。 |

## Flame Lance 穿透命中历史同步链路

Flame Lance 的关键同步点不是 projectile 本体还在飞行，而是它已经接受过哪些目标的命中历史。穿透 projectile 必须保存 `AcceptedImpactCount` 和 accepted impact target/frame hash，避免 seek 后重复命中同一个目标、漏掉已命中的目标，或提前消耗 / 恢复错误的穿透次数。

权威链路是：

```text
Flame Lance CharacterAction / Ability request
  -> CombatProjectileRuntime active projectile
  -> HitResolve accepted projectile impacts
  -> projectile AcceptedImpacts target/frame history
  -> CharacterTestNetworkProjectileRuntimeSnapshot / projectile state hash
  -> Runtime AI Lab probe / projectile presentation
```

关键边界：

- `AcceptedImpactCount` 是穿透 projectile 已接受命中数量；它必须随 projectile runtime state 进入 snapshot / restore。
- `AcceptedImpactHash` 覆盖已接受命中的 target entity 和 impact frame。只比较 count 会漏掉“数量相同但命中目标或命中帧不同”的错误。
- 对 Flame Lance 来说，checkpoint 可能已经包含多个 accepted impacts。Seek 从该 checkpoint 正向 replay 时，历史不能清空，也不能在后续帧重复加入同一目标。
- Unity impact VFX、飘字、trail 和 Runtime AI Lab overlay 都是表现层，只能消费 selected frame 的 projectile / combat sample。

### Snapshot / hash 覆盖

`CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash 必须覆盖：

- `projectile.definitionStableId`
- `projectile.sourceEntityId`
- `projectile.localFrame`
- `projectile.position.*`
- `projectile.direction.*`
- `projectile.acceptedImpactCount`
- `projectile.acceptedImpactHash`

Runtime AI Lab 的 `CharacterTestRuntimeAiLabFrameSyncProbe` 对 Flame Lance accepted impact history 的最低验收是：第一次运行到目标帧的 sample 与从 accepted-impact checkpoint 正向 replay 到同一帧的 sample 在 count/hash、local frame、position 和 direction 上完全一致。Compare 失败必须定位到 `projectile/<id>/acceptedImpactCount`、`projectile/<id>/acceptedImpactHash`、`localFrame` 或 `position*` 这类具体字段。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysProjectileAcceptedImpactHistoryExactly` 覆盖 Flame Lance 的穿透历史：

1. 生成 `Flame Lance` active projectile。
2. 运行到 projectile 仍 active 且 `AcceptedImpactCount >= 2` 的 checkpoint。
3. 确认 checkpoint 和目标帧的 `AcceptedImpactCount` 都为 2，且 `AcceptedImpactHash` 保持一致。
4. 从 checkpoint 正向 replay 到目标帧。
5. 断言 replay 后的 stable id、source entity、accepted impact count/hash、`LocalFrame`、sampled `Position` 和 `Direction` 与第一次运行目标帧完全一致，再交给 FrameSyncProbe 比较完整 projectile sample。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| accepted impact count 少了 | `projectile.acceptedImpactCount`、checkpoint projectile snapshot | accepted impact history 没进 snapshot，或 restore 后清空了历史。 |
| accepted impact count 一致但 hash 不同 | accepted impact target/frame hash | replay 命中了不同目标或在不同帧重新记录了命中。 |
| accepted history 正确但重复扣血 / 重复 VFX | combat damage sample、impact VFX presenter | projectile history 正确，combat / presentation 的 last-frame 事件重复消费。 |
| projectile 到目标帧提前消失 | `localFrame`、`effectiveLifetimeFrames`、accepted impact max | replay tick 次数错，或 accepted impact 上限 / StopOnFirstAcceptedImpact 配置恢复错误。 |

## Meteor Barrage 连续发射同步链路

Meteor Barrage 是 action runner 驱动的多段投射物用例。它和“同一帧已有多枚 active projectile”的验证不同：checkpoint 可能发生在第一枚 meteor 已经发出、后续 meteor 还没发出的中间帧。Seek 必须恢复 `CharacterActionRunner` 的 action id / local frame / instance id，再继续按原本动作本地帧发出后续 meteors；只恢复 `CombatProjectileRuntimeState` 只能证明已经存在的 meteor，没有证明未来发射窗口还能继续。

权威链路是：

```text
Meteor Barrage CharacterAction Runner local frame
  -> CharacterTestCharacterCombatTraceSystem projectile frame events
  -> repeated Meteor Barrage projectile spawn requests
  -> CombatProjectileRuntime active meteor state
  -> CharacterTestNetworkCharacterSnapshot + CharacterTestNetworkProjectileRuntimeSnapshot
  -> Runtime AI Lab probe
```

关键边界：
- `CharacterActionRunner` 的恢复状态决定后续发射窗口是否还会触发；它必须和角色 action component 一起从 checkpoint 恢复。
- `CombatProjectileRuntimeState` 保存已经 active 的 meteors、`NextProjectileId` 和 target point；restore 后继续发射时不能重复使用旧 id，也不能漏掉后续 id。
- Meteor 的落点 target point 是 gameplay authority 输入，不是 Unity VFX 或屏幕标记；Runtime AI Lab 需要比较每枚 active meteor 的 `DefinitionStableId`、local frame、position/direction 和 target point。
- Meteor impact 后生成的 explosion / interaction 另见 `Interaction / Explosion Field 同步链路`；本节只证明 runner 恢复后“未来 meteor 发射”仍然确定。

### 自动化验收

`RuntimeAiLabHeadlessSeek_ReplaysMeteorBarrageRunnerContinuedLaunchesExactly` 覆盖这条链路：
1. 第一次运行中找到 checkpoint：已有至少一枚 active meteor，`MeteorBarrageActionId` runner 仍在运行，且 active meteor 数量还没有达到完整发射数量。
2. 选取一个未来目标帧：active meteor 数量必须大于 checkpoint，证明后续 meteor 是从恢复后的 runner 继续发射出来的。
3. 从 checkpoint 恢复角色 snapshot 和 projectile snapshot，正向重放到目标帧。
4. 比较目标帧 sample 和重放 sample：runner action/local frame、active meteor count、每枚 meteor stable id、target point、projectile identity 和 local frame 必须一致。
5. 字段级断言必须覆盖 projectile allocator 和每枚 meteor 的 `ProjectileId`、`SpawnSequence`、source/action/trace identity、origin、target point、lifetime、launch speed、local frame、position/direction 和 accepted impact history。只看 active count 或最终 `Compare` 通过不足以证明连续发射没有漏发、重复发射或重算落点。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| checkpoint 后不再发出后续 meteor | `runner.actionId/localFrame`、`CharacterTestNetworkCharacterSnapshot`、runner restore | 只恢复了 action component，没有恢复 action runner；或 restore 后下一 tick local frame 错位。 |
| active count 增加但 id/sequence 不一致 | `NextProjectileId`、`ProjectileId`、`SpawnSequence` | projectile allocator 没从 snapshot 恢复，或 seek replay 重复生成了早期 meteor。 |
| target point 不一致 | meteor spawn request target resolver、`CombatProjectileSnapshot.TargetPoint` | replay 时目标点从 live transform 重新计算，或 target point 没进 projectile snapshot。 |
| 本节通过但 explosion / damage 错 | Interaction / Explosion Field、combat damage probe | runner 连续发射正确，但 impact 派生 interaction / damage 的目标帧输出不同。 |

## Interaction / Explosion Field 同步链路

Interaction / Explosion Field 是命中、到期或 timeline area pulse 在某一帧派生出的 gameplay interaction 事件。它不是 Unity VFX，也不是 Runtime AI Planner 的目标线；它的权威输入来自 `CharacterTestCharacterCombatTraceSystem` 在目标 `RuntimeFrame` 发布的 `CharacterTestInteractionHistory.LastFrameEvents`。

典型来源包括：

```text
Fireball Explosion projectile impact / expiration
  -> CharacterTestInteractionEvent radial impulse
Meteor Barrage area pulse
  -> CharacterTestInteractionEvent meteor radial impulse
Sword Wave / Tornado / other physical field source
  -> CharacterTestInteractionEvent physical field
  -> Runtime AI Lab probe / optional interaction presentation
```

关键边界：

- `CharacterTestInteractionHistory.LastFrameEvents` 证明目标帧是否发布了同一批 interaction event；普通 seek/review 不能漏发、重复发或使用 live frame 的事件。
- interaction event 的 stable id、source order、source / target entity、anchor、direction、radius、intensity、trace id、source domain 和 physical field 都是需要可复现的诊断事实。
- `CharacterTestInteractionVfxSystem.LastSummary` 只证明 presentation bridge 在同一帧消费了哪些 Fireball Explosion 类 interaction；它不是 gameplay authority，但 Runtime AI Lab 需要检查它是否跟 selected frame 对齐。
- Tornado 仍保留自己的 `tornado.interactionLastFrameCount/hash`，用于复合能力排查；通用 explosion / field 事件应优先看独立 `interaction` layer。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Interaction / Explosion Field，并作为独立 `interaction` layer 比较：

- `interaction.lastFrameEventCount`
- `interaction.lastFrameEventHash`
- `interaction.vfx.frame`
- `interaction.vfx.processedInteractionCount`
- `interaction.vfx.spawnedCount`
- `interaction.vfx.skippedCount`
- `interaction.vfx.lastStableId`
- `interaction.vfx.lastForceMode`
- `interaction.vfx.lastFieldShape`
- `interaction.vfx.lastVfxDefinitionId`

`lastFrameEventHash` 只是快速聚合证据，不能替代逐事件字段校验。当前 probe 还会保存并比较 `LastFrameEvents` 的字段级 sample：

- event identity：`frame`、`kind`、`stableId`、`sourceOrder`、`sourceEntityId`、`targetEntityId`、`traceId`、`sourceDomain`。
- event geometry：`anchorPosition`、`direction`、`radius`。
- event strength：`intensity`。
- physical field：`shape`、`forceMode`、`targetFilter`、capsule offsets、impulse / acceleration / falloff / max speed、mass scale 参数和 mass / push / material scale flags。

Compare 失败时必须定位到 `interaction` layer，例如：

```text
interaction/entity 0/lastFrameEventHash expected=1001 actual=2002
interaction/entity 0/event.anchorZRaw expected=2000000 actual=2500000
interaction/entity 0/event.fieldForceMode expected=1 actual=2
interaction/entity 0/vfx.spawnedCount expected=1 actual=0
interaction/entity 0/vfx.lastStableId expected=character_test.fireball_explosion.projectile.impact actual=none
```

Probe 仍然只读；不得在检查期间刷新 interaction presenter、Runtime AI Lab overlay、timeline cursor 或 presentation。

### 自动化验收

Runtime AI Lab 的 Interaction / Explosion Field 测试覆盖两类风险：

1. 人工构造 interaction mismatch，确认 probe 能定位到 `interaction/entity/field`，包括逐事件的 geometry / physical field 字段。
2. Headless replay：在 Meteor Barrage area pulse 前保存 projectile checkpoint，restore 后正向重放到 explosion interaction 帧，projectile sample 和 interaction event sample 必须与第一次运行逐字段一致。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| projectile 一致但 interaction count/hash 不一致 | `interaction.lastFrameEventCount/hash`、area pulse / expiration trigger frame | seek replay 到达了错误 local frame，或 last-frame interaction 清空/记录时机错误。 |
| interaction count/hash 看似可疑但无法定位字段 | `interaction.event.*` sample | 需要看具体 event 的 source/target、anchor、radius、force mode、capsule offset 或 scale 参数；不能只看聚合 hash。 |
| interaction hash 一致但 VFX summary 不一致 | `interaction.vfx.*`、`CharacterTestInteractionVfxSystem.LastSummary` | presentation bridge 消费了 live frame 或没有在 seek 后强制刷新。 |
| Tornado probe 显示 interaction 错但通用 interaction 一致 | `tornado.lastDamageTickTargetCount`、Tornado-specific target query | interaction event 本身正确，Tornado 周期伤害或目标计数链路不一致。 |
| interaction probe 一致但场景视觉错 | Interaction VFX / rendering presenter | authority 和诊断事件正确，presentation 未同步到 selected frame。 |

## Tornado 同步链路

Tornado 是一个复合投射物同步用例：它有 active projectile 飞行状态，同时在固定间隔产生周期伤害 tick，并在同帧发布吸引场 interaction。只验证 `CombatProjectileRuntimeState` 不够，因为 projectile 位置正确时，damage tick 或 attraction event 仍可能在 seek/replay 后漏帧、重复或目标不同。

权威链路是：

```text
Tornado CharacterAction / Ability request
  -> CharacterTestFireballProjectileAdapter Tornado projectile
  -> CombatProjectileRuntime active projectile state
  -> CharacterTestCharacterCombatTraceSystem periodic Tornado damage tick
  -> CharacterTestInteractionHistory last-frame attraction interaction
  -> Runtime AI Lab probe / Tornado presentation
```

关键边界：

- `CombatProjectileRuntimeState` 证明 active Tornado 的 id、definition、local frame、position/direction、lifetime 和 accepted impacts 是否可恢复。
- `LastTornadoDamageTickTargetCount` 证明目标帧是否执行了同一批 Tornado periodic damage tick。
- `CharacterTestInteractionHistory.LastFrameEvents` 证明目标帧是否发布了同一批 attraction interaction；Runtime AI Lab 只比较 last-frame count/hash，不把完整历史当作权威状态。
- Unity 风场 VFX、目标拖拽表现、飘字和 Runtime AI Lab overlay 都是表现层，只能从同帧 projectile/combat/interaction 诊断刷新。

### Snapshot / hash 覆盖

Tornado active projectile 本体走 `CharacterTestNetworkProjectileRuntimeSnapshot` 和 projectile state hash。

Damage tick target count 与 last-frame attraction interaction 是帧输出诊断，不是长期持久 authority state。它们不进入 SaveState / network snapshot / replay hash；但 Runtime AI Lab seek/replay 的目标是“同一 RuntimeFrame 产生同一帧输出”，所以 probe 会在 Lab 低频操作完成后采样：

- `tornado.lastDamageTickTargetCount`
- `tornado.interactionLastFrameCount`
- `tornado.interactionLastFrameHash`

Compare 失败时必须定位到 `tornado` layer，例如：

```text
tornado/entity 0/lastDamageTickTargetCount expected=1 actual=0
tornado/entity 0/interactionLastFrameHash expected=123 actual=456
```

### 自动化验收

Runtime AI Lab 的 Tornado 测试覆盖两类风险：

1. 人工构造 Tornado mismatch，确认 probe 能定位到 `tornado/entity/field`。
2. Headless replay：active Tornado checkpoint 正向重放到 periodic damage tick 帧，active projectile sample、target combat sample、damage tick target count 和 attraction interaction count/hash 必须与第一次运行一致。
3. 字段级断言必须覆盖 Tornado projectile 的 identity、source/action/trace、origin、position/direction、local frame、lifetime、impact history，以及目标 transform、health、last damage、last hit frame/trace；只比较 active count、state hash 或聚合 tick count 不足以证明 seek/replay 精确。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| projectile 一致但 damage tick target count 为 0 | `tornado.lastDamageTickTargetCount`、Tornado tick interval、target physics index | replay tick 次数错，或 target collider / combat body 没恢复到同一帧。 |
| damage tick 正确但 attraction interaction 不一致 | `tornado.interactionLastFrameCount/hash`、`CharacterTestInteractionHistory.LastFrameEvents` | interaction history 没按目标帧清空/重建，或 seek 后重复/漏放 attraction event。 |
| projectile 和 Tornado probe 一致但目标表现错 | target transform/view sync、animation/physics presenter | authority 输出正确，presentation 未强制同步或仍在 live frame 插值。 |
| overlay 显示目标线正确但 Tornado 错 | Runtime AI Planner snapshot vs projectile/combat/interaction probe | overlay 只证明决策几何正确，不能证明 Tornado projectile、damage tick 或 interaction 正确。 |

## Chain Lightning 同步链路

Chain Lightning 是瞬时多目标连锁效果。它由 CharacterAction / Ability 在确定帧发起请求，然后在 `CharacterTestCharacterCombatTraceSystem` 中按锁定目标、队伍关系、距离、角度和连锁上限解析目标；它不是 active projectile runtime，也不应该用 Unity 闪电 VFX 的位置或生命周期作为 authority。

权威链路是：

```text
Chain Lightning CharacterAction / Ability request
  -> CharacterTestCharacterCombatTraceSystem.ProcessChainLightningRequest
  -> deterministic chained target selection
  -> CharacterTestCombatDamageResolver / DamageApplier
  -> combat authority state + last-frame Chain Lightning diagnostics
  -> Runtime AI Lab probe / Chain Lightning presentation
```

关键边界：

- 目标 health、hit stun、last hit 等长期战斗状态仍走 combat authority state、snapshot 和 state hash。
- `LastChainLightningTargetCount` 与 `LastFrameChainLightningVfxEvents` 是目标帧输出诊断，用于 Runtime AI Lab seek/replay 比较同一帧是否解析出同一批连锁目标和弧段。
- Chain Lightning VFX presenter、闪电弧 mesh、屏幕标签和 Runtime AI Lab overlay 都是表现层，只能从同帧 diagnostic event 刷新。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 对 Chain Lightning 采样以下低频字段：

- `chainLightning.lastTargetCount`
- `chainLightning.arcEventCount`
- `chainLightning.arcEventHash`
- `chainLightning.arcEvents[]` 字段级 sample

`arcEventHash` 只作为快速聚合证据；字段级 sample 才是回放验收的主要证据。每条 arc event 必须记录并比较 frame、source/target entity、segment index、action instance、local frame、origin、direction、endpoint、length 和 hit 标记。Compare 失败时必须定位到 `chainLightning` layer 和具体 arc event 字段，例如：

```text
chainLightning/entity 0/lastTargetCount expected=4 actual=3
chainLightning/entity 0/arcEventHash expected=123 actual=456
chainLightning/entity 2/arcEvent.originXRaw expected=100000 actual=101000
chainLightning/entity 2/arcEvent.segmentIndex expected=1 actual=2
```

### 自动化验收

Runtime AI Lab 的 Chain Lightning 测试覆盖两类风险：

1. 人工构造 Chain Lightning mismatch，确认 probe 能定位到 `chainLightning/entity/field`，包括 `arcEvent.originXRaw` 这类单条弧段字段。
2. Headless replay：在同一批目标、同一锁定目标和同一 action local frame 下触发 Chain Lightning，checkpoint restore + forward replay 到连锁帧后，target count、arc event count、arc event hash、每条弧段 event payload、弧段 target entity 顺序，以及每个被命中目标的 combat sample（health、lastDamage、lastHitFrame、lastHitSourceEntityId、lastHitTraceId）必须与第一次运行一致。只比较 count/hash 不足以证明 damage authority state 被同步。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| combat health 一致但 Chain Lightning count 不一致 | `LastChainLightningTargetCount`、locked target provider、target relation/filter、CombatPhysics query | target selection 使用了错误目标、物理索引未恢复、或 replay 到了不同 local frame。 |
| Chain Lightning count/hash 一致但目标 health / last hit 不一致 | 弧段 target entity id、`combat.health`、`combat.lastDamage`、`combat.lastHitFrame`、`combat.lastHitSourceEntityId`、`combat.lastHitTraceId` | 弧段诊断事件重放正确，但 damage resolver / applier、target combat snapshot 或 seek 后 combat authority restore 漏字段。 |
| target count 一致但 arc hash 不一致 | `LastFrameChainLightningVfxEvents` 的 segment order / origin / endpoint / local frame | 弧段事件顺序不稳定、source/target center 使用了错误帧，或 VFX event 重复/漏发。 |
| Chain Lightning probe 一致但视觉错 | Chain Lightning presenter / VFX mesh / Runtime AI Lab overlay | authority 输出正确，presentation 未强制同步或仍在使用 live frame。 |

## Stun Bolt OnHitBuff 同步链路

Stun Bolt 是控制型 projectile impact 用例：投射物命中后可以不依赖额外持续 runtime source，也不一定以 HP 扣减作为主要验证点，而是通过 `hitEvent.OnHitBuffs` 把 `Stunned` Buff / Status 写入目标的 gameplay authority state。它验证的是“projectile impact -> OnHitBuff -> target buff/status”这条链路是否能在 checkpoint restore + forward replay 后恢复到同一帧。

权威链路是：

```text
Stun Bolt CharacterAction / Ability request
  -> CombatProjectileRuntime active projectile
  -> projectile impact / HitResolveResult
  -> _onHitBuffsByHit / CombatOnHitBuffApplicationSystem
  -> target GameplayComponentBuffSetComponent / GameplayStatusComponent
  -> Runtime AI Lab probe character buff/status sample
```

关键边界：
- `CombatProjectileRuntimeState` 只证明 Stun Bolt projectile 的 identity、position、direction、local frame 和 impact frame 能恢复。
- `CombatOnHitBuffApplicationSummary` 证明命中帧确实产生 OnHitBuff 请求；它是低频诊断摘要，不是长期权威状态。
- 目标的 `GameplayComponentBuffSetComponent` 和 `GameplayStatusComponent` 才是 Stunned 结果的权威状态；Runtime AI Lab probe 先用目标 character sample 的 `BuffCount/BuffHash` 与 `StatusCount/StatusHash` 做摘要校验，再用字段级 entry sample 定位具体差异。
- Buff 字段级 entry sample 覆盖 `buffId`、`stackCount`、`startFrame`、`endFrame`、`isPermanent`、`sourceId`；Status 字段级 entry sample 覆盖 `statusId`、`sourceBuffId`。诊断字段形如 `buff.entry.<buffId>.stackCount`、`buff.entry.<buffId>.endFrame`、`status.entry.<statusId>.sourceBuffId`。
- Unity stun VFX、头顶状态标签、受击表现和 Runtime AI Lab overlay 只读跟随同帧 authority state；不能反向作为同步依据。

### 自动化验收

Runtime AI Lab 的 Stun Bolt 测试覆盖三类风险：
1. projectile checkpoint 正向重放到命中帧后，target `Stunned` Buff 和 Status 必须存在。
2. 目标 character sample 必须包含非零 `BuffHash` 和 `StatusHash`，并且包含具体 Buff / Status entry sample，避免只验证“有任意 buff/status”。
3. 命中帧必须记录 OnHitBuff request summary；否则说明 projectile impact / HitResolveResult 和 buff application bridge 脱节。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| projectile 一致但目标没有 Stunned | `OnHitBuffLastSummary`、`_onHitBuffsByHit`、`CombatOnHitBuffApplicationSystem` | projectile impact 成立，但 hitEvent 的 OnHitBuff 配置没有被传到 buff application bridge。 |
| OnHitBuff request 存在但 target buff/status 不一致 | target gameplay buff/status stores、`buff.entry.<id>.*`、`status.entry.<id>.*`、snapshot/hash coverage | gameplay component snapshot 或 seek replay 没恢复目标 buff/status；字段级 entry 能区分层数、结束帧、来源 Buff 或状态来源错。 |
| Stunned authority 一致但视觉错 | status presenter、head pad、animation reaction、Runtime AI Lab overlay | presentation 未强制同步，或仍在使用 latest/live frame。 |

## Sword Wave 短生命周期投射物同步链路

Sword Wave 是短生命周期 staged projectile 用例。它验证的不是命中后长期机制源，而是“同一动作按固定本地帧发出短生命 projectile，checkpoint restore 后 active lifetime、position/direction 和到期移除都与第一次运行一致”。这类问题如果只看角色 transform、combat health 或 Runtime AI Planner overlay，可能完全看不出错；必须把 active projectile 和过期后的 active count 都作为独立证据。

权威链路是：

```text
Sword Wave CharacterAction local frame
  -> projectile spawn request
  -> CombatProjectileRuntime active Sword Wave projectile
  -> effective lifetime / local frame advance
  -> expiration removes projectile before next staged wave
  -> Runtime AI Lab probe projectile sample
```

关键边界：
- `CombatProjectileRuntimeState` 是 Sword Wave projectile 的权威；Unity sword-wave VFX、mesh、trail 和 Runtime AI Lab overlay 只读跟随。
- `EffectiveLifetimeFrames` 是本次发射解析出的短生命期，必须进入 probe / snapshot / replay 比较；不能只验证 definition id。
- active 目标帧要证明 projectile 仍在飞行，过期目标帧要证明 active projectile count 已回到 0，避免 seek 后延迟销毁或提前清理。
- Sword Wave 的短生命期不等于动作 Runner 的完整时长；动作可以继续，但该 projectile 必须按自己的 authority lifetime 到期。

### 自动化验收

Runtime AI Lab 的 Sword Wave 测试覆盖三类风险：
1. checkpoint 帧包含 active Sword Wave projectile，且 stable id 与 `EffectiveLifetimeFrames` 正确。
2. checkpoint restore + forward replay 到 active target frame 后，projectile sample 必须与第一次运行一致，并保持同一个 short lifetime。
3. checkpoint restore + forward replay 到 expiration target frame 后，active projectile count 必须为 0，证明短生命周期清理发生在同一帧。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| active target frame 少了 Sword Wave | projectile spawn local frame、action runner restore、spawn request buffer | seek 恢复后动作本地帧不一致，或 spawn request 没有重放。 |
| effective lifetime 不一致 | spawn profile、distance/lifetime resolver、`EffectiveLifetimeFrames` snapshot | 短生命周期按 live state 重新计算，或 snapshot 没覆盖本次发射的有效生命期。 |
| expired frame 仍有 active projectile | projectile lifetime advance、expiration event、active list cleanup | seek 后 local frame / lifetime 递增错，或过期清理没有在目标帧执行。 |
| projectile probe 一致但视觉拖尾错 | Sword Wave presenter / VFX trail / Runtime AI Lab overlay | authority 正确，presentation 没按 selected frame 强制刷新或仍保留旧 trail。 |

## Laser Path VFX 同步链路

Laser Path VFX 是 Laser Lance projectile 命中或到期后，在同一 `RuntimeFrame` 派生出的 last-frame 表现诊断事件。它不是新的 gameplay authority，也不进入 Runtime AI Planner schema；它的输入必须来自同帧的 projectile snapshot、combat hit resolve result 和 deterministic endpoint 计算。

链路是：

```text
Laser Lance CharacterAction / Ability request
  -> Laser Lance projectile spawn
  -> projectile impact / expiration
  -> CharacterTestLaserPathVfxEvent last-frame diagnostic
  -> Runtime AI Lab probe / Laser Path VFX bridge
```

关键边界：
- `CombatProjectileRuntimeState` 证明 Laser Lance projectile 的 id、definition、local frame、origin、direction、accepted impact 是否可恢复。
- `CharacterTestLaserPathVfxEvent` 证明表现层要画的 path 起点、方向、终点、长度、hit/miss、source/target 和 action instance 是否来自同一帧。
- Unity laser line renderer、impact VFX、resource key 和 presenter 生命周期都是表现层，不能作为权威状态。
- 如果 projectile / combat state 一致但 Laser Path VFX event 不一致，优先排查 seek replay 到达的 action local frame、`LastFrameLaserPathVfxEvents` 清空/记录时机，以及 VFX bridge 是否消费了错误帧。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Laser Path VFX last-frame events，并作为独立 `laserPath` layer 比较：
- event count
- event hash，覆盖 frame、source/target entity、projectile id、action instance、local frame、origin/direction/endpoint raw values、length millimeters 和 hit flag
- 每个 event 的字段级 sample：frame、source/target entity、projectile id、action instance、local frame、origin / direction / endpoint raw values、length millimeters、hit flag

字段级 sample 是 Laser Path 回放验收的主证据；hash 只用于快速发现整体 payload 改变，不能替代逐字段 mismatch 诊断。

Compare 失败时必须定位到 `laserPath` layer，例如：

```text
laserPath/entity 0/eventCount expected=1 actual=0
laserPath/entity 0/eventHash expected=1001 actual=2002
laserPath/entity 0/event.originXRaw expected=1000 actual=3000
laserPath/entity 0/event.hit expected=true actual=false
```

Probe 仍然只读；不得在检查期间刷新 Laser Path presenter、Runtime AI Lab overlay、timeline cursor 或 presentation。

### 自动化验收

Runtime AI Lab 的 Laser Path VFX 测试覆盖两类风险：
1. 人工构造 Laser Path mismatch，确认 probe 能定位到 `laserPath/eventCount` 和 `laserPath/eventHash`。
2. 人工构造字段级 mismatch，确认 probe 能定位到 `laserPath/event.originXRaw` 这类具体 event payload 字段。
3. Headless replay：在 Laser Lance path event 前保存 action runner checkpoint 和 projectile state，restore 后正向重放到 path event 帧，Laser Path event count、hash 和每个 event payload 字段必须与第一次运行一致。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| projectile 一致但 laserPath event count 不一致 | `LastFrameLaserPathVfxEvents`、Laser Lance active frame、seek target frame | replay 到了错误 local frame，或 last-frame event 被过早清空 / 没有重新记录。 |
| laserPath event hash 不一致 | event origin / direction / endpoint / length / hit flag | hit point、expiration endpoint 或 target resolve 在 replay 后使用了错误帧数据。 |
| laserPath probe 一致但视觉错 | Laser Path VFX bridge / presenter | authority 和 diagnostic event 正确，presentation 未强制同步或仍在使用 live frame。 |

## Arc Mine 同步链路

Arc Mine 是部署派生型投射物链路：前半段是 carrier / drop projectile，落地后转成独立的 active mine runtime。它不是 projectile presenter 的子对象，也不是地面 VFX；active mine 的 armed / fuse / detonate 状态必须由 `CharacterTestArcMineRuntimeState` 承载。

权威链路是：

```text
Arc Mine CharacterAction / Ability request
  -> carrier projectile
  -> drop projectile
  -> CharacterTestArcMineRuntime active mine
  -> CharacterTestNetworkArcMineRuntimeSnapshot / state hash
  -> Runtime AI Lab probe / Arc Mine presentation
```

关键边界：
- `CombatProjectileRuntimeState` 只能证明 carrier / drop projectile 飞行状态正确；部署后的 active mine 必须看 `CharacterTestArcMineRuntimeState`。
- active mine 的 `RuntimeId`、caster、source combat entity、action instance、local frame、created / armed / expire / fuse / detonate frame、trigger target、center / forward 和 trace id 都是权威字段。
- `FuseStartedFrame` 和 `DetonateFrame` 是“何时开始爆炸链路”的确定性状态；不能从 Unity VFX、粒子生命周期或当前 target distance 重新推导。
- Unity 预览体、地面标记、爆炸 VFX 和 Runtime AI Lab overlay 只读跟随，不能反向修正 active mine authority。

### Snapshot / hash 覆盖

CharacterTest network snapshot 通过 `CharacterTestNetworkArcMineRuntimeSnapshot` 覆盖 Arc Mine runtime：
- schema / module id
- `NextRuntimeId`
- 每个 active mine 的 runtime id、caster、source combat entity、action instance、source order、local frame
- created / armed / expire / fuse started / detonate frame
- trigger target entity id
- center / forward raw values
- trace id

state hash 同步加入 Arc Mine：
- full hash 覆盖全部 active mines。
- owned hash 可按 caster entity 过滤。
- non-owned hash 可排除 owned caster 的 mine。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Arc Mine runtime，并作为独立 `arcMine` layer 比较：
- runtime 可用性、schema version、module id、next runtime id
- active mine count
- 每个 mine 的 runtime id、caster、source combat entity、action instance、source order、local frame
- created / armed / expire / fuse started / detonate frame
- trigger target entity id
- center / forward raw values
- trace id

Compare 失败时必须定位到具体 mine runtime id 和字段，例如：

```text
arcMine/entity 1/mine.detonateFrame expected=90 actual=94
arcMine/entity 1/mine.centerXRaw expected=1000 actual=2000
arcMine/entity 0/activeMineCount expected=1 actual=0
```

Probe 仍然只读；不得在检查期间刷新 Arc Mine presenter、overlay、presentation 或 timeline cursor。

### 自动化验收

Runtime AI Lab 的 Arc Mine 测试覆盖两类风险：

1. 人工构造 Arc Mine mismatch，确认 probe 能定位到 `arcMine/entity/field`。
2. Headless replay：carrier checkpoint 正向重放到 drop projectile，drop checkpoint 正向重放到 active mine，active mine checkpoint 再向后重放数帧；每个目标帧的 projectile sample 和 Arc Mine sample 都必须与第一次运行一致。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| arc mine active count 不一致 | `CharacterTestArcMineRuntimeState.ActiveMines`、checkpoint arc mine snapshot、seek restore result | snapshot 未覆盖 arc mine runtime，或 restore 后没有清理旧 active mine。 |
| mine runtime id / next id 不一致 | `NextRuntimeId`、`RuntimeId` | restore 后 allocator 沿用 live 状态，或 replay 重复部署 mine。 |
| fuse / detonate frame 不一致 | `FuseStartedFrame`、`DetonateFrame`、trigger query frame | query / trigger 状态未恢复，或 seek replay tick 次数错。 |
| mine center / forward 不一致 | center / forward raw values、drop projectile impact / expiration frame | drop projectile 到 deployed mine 的转换帧错，或 deploy target 重新计算而不是恢复。 |
| Arc Mine authority 一致但视觉错 | Arc Mine presenter / VFX / debug marker | seek 后 presentation 未强制同步，或 presenter 仍在使用 live frame。 |

## Rune Turret 同步链路

Rune Turret 是部署后持续发射型投射物链路：部署阶段由 carrier projectile 落点创建 turret runtime，后续由 active turret 按固定帧 warmup / scan / fire interval 生成 shot projectile。它不是一个普通 projectile 的生命周期延长，也不是 VFX 计时器；炮台自身的思考帧、发射次数和位置朝向必须由 `CharacterTestRuneTurretRuntimeState` 承载。

权威链路是：

```text
Rune Turret CharacterAction / Ability request
  -> deploy carrier projectile
  -> CharacterTestRuneTurretRuntime active turret
  -> periodic shot projectile
  -> CharacterTestNetworkRuneTurretRuntimeSnapshot / state hash
  -> Runtime AI Lab probe / Rune Turret presentation
```

关键边界：
- `CombatProjectileRuntimeState` 只能证明 deploy carrier 和 shot projectile 的飞行状态正确；active turret 必须看 `CharacterTestRuneTurretRuntimeState`。
- active turret 的 `RuntimeId`、caster、source combat entity、action instance、local frame、created / activate / expire frame、next think frame、shots fired、center / forward 和 trace id 都是权威字段。
- `NextThinkFrame` 和 `ShotsFired` 是“何时再次扫描 / 开火”的确定性状态；不能从 Unity VFX、炮台动画或当前 target distance 重新推导。
- Unity 炮台预览体、idle/fire/expire VFX、shot trail 和 Runtime AI Lab overlay 都只读跟随，不能反向修正 turret authority。

### Snapshot / hash 覆盖

CharacterTest network snapshot 通过 `CharacterTestNetworkRuneTurretRuntimeSnapshot` 覆盖 Rune Turret runtime：
- schema / module id
- `NextRuntimeId`
- 每个 active turret 的 runtime id、caster、source combat entity、action instance、source order、local frame
- created / activate / expire / next think frame
- shots fired
- center / forward raw values
- trace id

state hash 同步加入 Rune Turret：
- full hash 覆盖全部 active turrets。
- owned hash 可按 caster entity 过滤。
- non-owned hash 可排除 owned caster 的 turret。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Rune Turret runtime，并作为独立 `runeTurret` layer 比较：
- runtime 可用性、schema version、module id、next runtime id
- active turret count
- 每个 turret 的 runtime id、caster、source combat entity、action instance、source order、local frame
- created / activate / expire / next think frame
- shots fired
- center / forward raw values
- trace id

Compare 失败时必须定位到具体 turret runtime id 和字段，例如：

```text
runeTurret/entity 1/turret.nextThinkFrame expected=140 actual=148
runeTurret/entity 1/turret.shotsFired expected=1 actual=2
runeTurret/entity 0/activeTurretCount expected=1 actual=0
```

Probe 仍然只读；不得在检查期间刷新 Rune Turret presenter、overlay、presentation 或 timeline cursor。

### 自动化验收

Runtime AI Lab 的 Rune Turret 测试覆盖两类风险：

1. 人工构造 Rune Turret mismatch，确认 probe 能定位到 `runeTurret/entity/field`。
2. Headless replay：deploy carrier checkpoint 正向重放到 active turret，active turret checkpoint 正向重放到 shot projectile，shot checkpoint 再向后重放数帧；每个目标帧的 projectile sample 和 Rune Turret sample 都必须与第一次运行一致。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| rune turret active count 不一致 | `CharacterTestRuneTurretRuntimeState.ActiveTurrets`、checkpoint rune turret snapshot、seek restore result | snapshot 未覆盖 rune turret runtime，或 restore 后没有清理旧 active turret。 |
| turret runtime id / next id 不一致 | `NextRuntimeId`、`RuntimeId` | restore 后 allocator 沿用 live 状态，或 replay 重复部署 turret。 |
| next think / shots fired 不一致 | `NextThinkFrame`、`ShotsFired`、fire interval / scan interval | seek replay tick 次数错，或 active turret 的思考帧没有恢复。 |
| turret center / forward 不一致 | center / forward raw values、deploy carrier impact / expiration frame | deploy carrier 到 active turret 的转换帧错，或 deploy target 重新计算而不是恢复。 |
| Rune Turret authority 一致但视觉错 | Rune Turret presenter / idle-fire-expire VFX / shot trail | seek 后 presentation 未强制同步，或 presenter 仍在使用 live frame。 |

## Area Confinement 同步链路

Area Confinement 是延迟 channel 场域链路：施法帧先创建 pending channel，telegraph 完成后转换为 mechanic source active zone。它不是 projectile 的延长生命周期，也不是 VFX 计时器；pending channel 的 activation frame、落点、朝向和 active zone source 必须由 `CharacterTestAreaConfinementRuntimeState` 承载。

权威链路是：

```text
Area Confinement CharacterAction / Ability request
  -> pending channel
  -> mechanic source active zone
  -> CharacterTestNetworkAreaConfinementSnapshot / state hash
  -> Runtime AI Lab probe / Area Confinement presentation
```

关键边界：
- `CharacterAction` / `Ability` 只在确定帧提交 Area Confinement gameplay request；它们不直接创建 Unity 场域对象。
- pending channel 的权威 identity 是 `PendingChannelId`，并包含 caster、source gameplay/combat entity、center、forward、landing mode、start / activation frame、local frame、source order 和 trace id。
- active zone 复用 mechanic source runtime；权威字段包括 runtime source id、source definition、source gameplay/combat entity、center、start / end frame 和 trace id。
- Unity telegraph / zone VFX、地面贴花、状态提示和 Runtime AI Lab overlay 都是表现层，只能从 Area Confinement authority state 或同帧 debug snapshot 刷新。

### Snapshot / hash 覆盖

`CharacterTestNetworkAreaConfinementSnapshotCodec` 覆盖 Area Confinement runtime：
- schema / module id
- `NextPendingChannelId`
- pending channels
- source runtime schema / module id / next runtime source id
- active mechanic sources

state hash 同步加入 Area Confinement：
- pending channel 字段全部进入 hash。
- active zone source 通过 mechanic source runtime 进入 hash。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Area Confinement runtime，并作为独立 `areaConfinement` layer 比较：
- runtime 可用性、schema version、module id、next pending channel id
- pending channel count 和 pending channel 字段
- source runtime 可用性、schema / module id、next runtime source id
- active source count 和 active source 字段

Compare 失败时必须定位到具体 pending channel 或 active source 字段，例如：

```text
areaConfinement/entity 1/pending.activationFrame expected=80 actual=86
areaConfinement/entity 1/source.centerXRaw expected=1000 actual=2000
areaConfinement/entity 0/source.count expected=1 actual=0
```

Probe 仍然只读；不得在检查期间刷新 Area Confinement presenter、overlay、presentation 或 timeline cursor。

### 自动化验收

Runtime AI Lab 的 Area Confinement 测试覆盖两类风险：

1. 人工构造 Area Confinement mismatch，确认 probe 能定位到 `areaConfinement/entity/field`。
2. Headless replay：pending channel checkpoint 正向重放到 active zone，active zone checkpoint 再向后重放数帧；首次运行和 replay 都必须把 `world.GameplayComponentWorld` 传给 probe，并随 checkpoint 恢复目标角色的 buff/status authority。每个目标帧的 Area Confinement sample、Rooted 状态及 state hash 必须与第一次运行一致。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| pending count / activation frame 不一致 | `areaConfinement` layer pending fields、checkpoint Area Confinement snapshot | pending channel 未进 snapshot，或 seek 后 channel allocator / activation frame 未恢复。 |
| active source count / center 不一致 | `areaConfinement` source fields、mechanic source runtime | channel 转 active zone 的帧错，或 active source runtime 未随 Area Confinement state 恢复。 |
| Area Confinement authority 一致但目标 root / 状态错 | target buff/status sample、mechanic motion bridge | zone source 正确，但 buff/status 或 motion policy restore/replay 缺字段。 |
| Area Confinement authority 一致但视觉错 | telegraph / zone VFX presenter、Runtime AI Lab overlay | seek 后 presentation 未强制同步，或 presenter 仍在使用 latest/live frame。 |

## Poison Bottle Pool 同步链路

Poison Bottle Pool 是 Poison Bottle projectile 命中或到期后的持续 mechanic source。它不是仍在飞行的 projectile，也不是地面毒池 VFX；命中后的毒池范围、生命周期和后续 tick 统计必须由 `CharacterTestMechanicSourceRuntimeState` 与 `CharacterTestCharacterCombatTraceSystem` 的 poison pool tick summary 承载。

权威链路是：

```text
Poison Bottle projectile impact / expiration
  -> mechanic source poison pool
  -> poison pool query / applied-or-refreshed summary
  -> poison status tick summary
  -> CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot / state hash
  -> Runtime AI Lab probe / Poison Bottle Pool presentation
```

关键边界：
- `CombatProjectileRuntimeState` 只证明 Poison Bottle projectile 飞行、目标点、弹道解算和命中 / 到期帧正确；毒池生成后必须继续看 `CharacterTestMechanicSourceRuntimeState`。
- active poison pool 的权威字段包括 runtime source id、source definition、source gameplay/combat entity、center、start / end frame 和 trace id。
- `PoisonBottlePoolLastTickSummary` 记录本帧 active pool 数、创建 / 过期数、query 数、命中数、applied/refreshed 数、拒绝目标数和缺失 gameplay target 数。
- `LastPoisonTickTargetCount` 记录 poison status tick 的目标数；它用于判断 seek/replay 是否在同一帧产生同一批 poison DoT tick。
- Unity 毒池 VFX、shatter VFX、受毒表现、飘字和 Runtime AI Lab overlay 都是表现层，只能从同帧 authority state / debug snapshot 刷新。

### Snapshot / hash 覆盖

`CharacterTestNetworkPoisonBottlePoolSnapshotCodec` 覆盖 Poison Bottle Pool runtime：
- source runtime schema / module id / next runtime source id
- active mechanic sources

state hash 同步加入 Poison Bottle Pool：
- active pool source 字段全部通过 mechanic source runtime 进入 hash。
- pool tick summary 不是权威持久状态，但 Runtime AI Lab probe 会把它作为 seek/replay 诊断字段比较。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Poison Bottle Pool，并作为独立 `poisonPool` layer 比较：
- runtime 可用性、schema version、module id、next runtime source id
- active source count 和 active source 字段
- last tick active / created / expired / query / hit / applied / rejected / skipped counts
- last poison tick target count

Compare 失败时必须定位到具体 source 或 tick summary 字段，例如：

```text
poisonPool/entity 1/source.centerXRaw expected=123456 actual=100000
poisonPool/entity 0/lastTick.appliedOrRefreshedCount expected=1 actual=0
poisonPool/entity 0/lastPoisonTickTargetCount expected=1 actual=0
```

Probe 仍然只读；不得在检查期间刷新 Poison Bottle Pool presenter、overlay、presentation 或 timeline cursor。

### 自动化验收

Runtime AI Lab 的 Poison Bottle Pool 测试覆盖三类风险：

1. 人工构造 Poison Bottle Pool mismatch，确认 probe 能定位到 `poisonPool/entity/field`。
2. Snapshot roundtrip：active poison pool mechanic source capture/apply 后 source fields 与 hash 保持一致。
3. Headless replay：Poison Bottle projectile checkpoint 正向重放到 active poison pool，再继续重放到 poison tick 帧；每个目标帧的 projectile sample、poison pool sample、tick summary、poison pool state hash，以及目标角色的 Poisoned buff/status sample 必须与第一次运行一致。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| projectile 一致但 poison pool count 为 0 | projectile impact / expiration frame、`poisonPool` source count | projectile 到 pool 的转换帧错，或 Poison Bottle Pool source runtime 未进 snapshot。 |
| poison pool source id / center 不一致 | `runtimeSourceId`、`nextRuntimeSourceId`、center raw values | restore 后 allocator 沿用 live 状态，或 pool center 重新计算而不是恢复。 |
| last tick query / applied count 不一致 | `PoisonBottlePoolLastTickSummary`、target collider / gameplay target | seek replay tick 次数错，或目标 collision/gameplay state 未恢复到同一帧。 |
| poison tick target count 不一致 | `LastPoisonTickTargetCount`、poison status buff state | poison status apply/refresh 或 poison DoT tick 的 checkpoint / replay 覆盖缺字段。 |
| Poison Bottle Pool authority 一致但视觉错 | poison pool VFX presenter、shatter VFX、Runtime AI Lab overlay | seek 后 presentation 未强制同步，或 presenter 仍在使用 latest/live frame。 |

## Hook Pull 同步链路

Hook Pull 是 Hook Chain 命中后的持续拉拽 authority 状态。它不是飞行中的 projectile，也不是目标角色 transform 的派生值；它决定在后续固定帧里 caster / target 之间如何继续拉近。

权威链路是：

```text
Hook Chain projectile impact
  -> CharacterTestCombatDamageResolver
  -> CharacterTestCharacterCombatTraceSystem active hook pulls
  -> CharacterTestHookPullRuntimeState
  -> CharacterTestNetworkHookPullRuntimeSnapshot / state hash
  -> target movement / presentation / Runtime AI Lab probe
```

关键边界：

- `CombatProjectileRuntimeState` 只证明 Hook Chain projectile 飞行和命中历史正确；命中后拉拽必须看 `CharacterTestHookPullRuntimeState`。
- active pull 的权威 identity 是 caster、target、action instance、projectile id 和 stable event；seek/replay 后不能重复生成或丢失。
- `StartFrame` / `EndFrame` 决定拉拽生命周期；剩余时间是派生值。
- `StopDistanceMillimeters`、`AnchorForwardOffsetMillimeters`、`AccelerationMillimetersPerSecondSquared`、`TerminalSpeedMillimetersPerSecond` 决定拉拽运动参数；这些字段错了，角色 transform 可能在目标帧才表现出偏差。
- Unity 角色 view、胶囊、线框和 Runtime AI Lab overlay 都只读跟随；它们不能反向修正 Hook Pull authority。

### Snapshot / hash 覆盖

CharacterTest network snapshot schema `7` 开始覆盖 Hook Pull runtime：

- `SchemaVersion`
- `ModuleId`
- `ActivePulls[]`
- 每条 active pull 的 caster、target、start/end frame、action instance、projectile id、stable event、stop distance、anchor forward offset、acceleration、terminal speed。

state hash 同步加入 Hook Pull：

- full hash 覆盖所有 active pull。
- owned hash 按 caster / owner 过滤。
- non-owned hash 排除 owned caster。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Hook Pull runtime，并作为独立 `hookPull` layer 比较：

- runtime 可用性
- schema version 和 module id
- active pull count
- 每条 active pull 的 identity、timing 和运动参数

Compare 失败时必须定位到具体 target 和字段，例如：

```text
hookPull/entity 2/pull.endFrame expected=90 actual=94
hookPull/entity 2/pull.stopDistanceMillimeters expected=1000 actual=1500
hookPull/entity 2/pull.missing expected=1:2:33:101:character_test.hook_chain.projectile.impact actual=missing
```

Probe 仍然只读；不得在检查期间刷新 HUD、overlay、presentation 或 timeline cursor。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| hook pull active count 不一致 | `CharacterTestHookPullRuntimeState.ActivePulls`、checkpoint hook pull snapshot、seek restore result | snapshot 未覆盖 hook pull runtime，或 seek restore 后没有清理旧 active pull。 |
| hook pull timing 不一致 | `StartFrame`、`EndFrame`、seek replay tick count | seek 重放少 / 多 tick，或命中帧重复生成拉拽。 |
| hook pull 参数不一致 | stop distance、anchor offset、acceleration、terminal speed | tuning / action impact 参数在 restore/replay 两次路径中不同，或 snapshot 漏字段。 |
| hook pull 对但目标位置错 | target movement authority、motion system、presentation forced sync | 拉拽状态已恢复，但目标移动或表现同步没有跟随目标帧。 |

## Ability / cooldown / action request 同步链路

本节补齐移动、CharacterAction Runner、战斗命中 / 伤害、投射物之后的第五层事实源。Ability 同步的核心不是 UI 热键，也不是 Runtime AI Planner 的 action 名称，而是 `AbilityRuntimeModule` 持有的冷却权威状态，以及它在确定帧和 CharacterAction 启动结果之间的关系。

Ability 请求链路是：

```text
RuntimeCommand / local input / Runtime AI Planner command
  -> CharacterTestRuntimeCommandModule
  -> CharacterTestCharacterInputComponent
  -> CharacterTestCharacterActionSystem
  -> AbilityRuntimeModule.TryResolveInputIntent / TryEvaluate
  -> CharacterActionRuntime.TryStart...
  -> AbilityRuntimeModule.CommitActivation
  -> AbilityRuntimeState / CharacterTestNetworkAbilityRuntimeSnapshot / state hash
  -> HUD cooldown / Runtime AI Lab probe
```

关键边界：

- `SetControlIntent`、hotbar、Runtime AI Planner command 只能表达“本帧请求使用某个 Ability / intent”；它们不直接改 cooldown。
- `AbilityRuntimeModule.TryResolveInputIntent` / `TryEvaluate` 在当前 `RuntimeFrame` 检查 ability 是否存在、是否绑定动作、是否仍在 cooldown。
- CharacterAction 启动被接受后，`CharacterTestCharacterActionSystem` 才调用 `AbilityRuntimeModule.CommitActivation`。因此 cooldown 起点必须和 action acceptance 所在帧一致，不能提前到 UI 按钮按下帧。
- cooldown 权威字段是 `(casterEntityId, abilityId, endFrame)`。剩余 cooldown 是 `endFrame - current RuntimeFrame` 的派生值，不应作为快照权威字段。
- HUD 热键、技能图标、冷却数字、Runtime AI Lab Details 都是表现层，只能从同帧 `AbilityRuntimeState` 派生。

### Snapshot / hash 覆盖

CharacterTest network snapshot schema `13` 开始覆盖 Ability runtime：

- `SchemaVersion`
- `ModuleId`
- `DefinitionHash`
- `Cooldowns[] { CasterEntityId, AbilityId, EndFrame }`

Apply snapshot 时通过 `AbilityRuntimeSaveStateProvider` 低频恢复 `AbilityRuntimeModule`，避免把 gameplay 内部 restore API 暴露到高频路径。旧 schema 没有 Ability runtime 字段时，按当前 definition hash 恢复为空 cooldown 集合；这只用于兼容旧 snapshot，不代表真实历史 cooldown 可被无损恢复。

state hash 同步加入 Ability runtime：

- full hash 覆盖所有 cooldown。
- owned hash 只覆盖 owned caster 的 cooldown。
- non-owned hash 排除 owned caster 的 cooldown。

这能让 `+10 / -10 / +10` 在 cooldown 遗漏恢复时直接暴露为 hash 或 probe mismatch，而不是只在 UI 数字错乱时才被发现。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Ability runtime，并作为独立 `ability` layer 比较：

- runtime 可用性
- schema version
- module id
- definition hash
- cooldown count
- 每个 `(casterEntityId, abilityId)` 的 `endFrame`

Compare 失败时必须定位到具体 caster 和字段，例如：

```text
ability/entity 1/cooldown.endFrame expected=120 actual=132
ability/entity 0/cooldown.count expected=1 actual=0
```

Probe 仍然只读；不得在检查期间调用会刷新 HUD、overlay、presentation 或 timeline cursor 的方法。

### 自动化验收

Runtime AI Lab 的 Ability / cooldown 测试应覆盖两类风险：

1. 人工构造 cooldown mismatch，确认 probe 能定位到 `ability/entity/field`。
2. Headless cooldown replay：在 frame F 对某个 caster 提交 DashStrike cooldown，记录 checkpoint 和目标帧；随后把 runtime 跑到 cooldown 过期，再恢复 checkpoint 并正向重放到目标帧，断言 ability sample 与第一次运行一致。

第二条测试能覆盖一个关键失败模式：如果 checkpoint restore 没有恢复 `AbilityRuntimeModule`，未来帧里已经过期的 cooldown 不会自动回到历史目标帧，probe 会报 `ability/cooldown.count` 或 `ability/cooldown.endFrame` mismatch。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| ability cooldown count 不一致 | `AbilityRuntimeState.Cooldowns`、checkpoint ability snapshot、seek restore result | snapshot 未覆盖 ability runtime，或旧 schema 无法还原历史 cooldown。 |
| cooldown endFrame 不一致 | `(casterEntityId, abilityId, endFrame)`、action acceptance frame | `CommitActivation` 帧错了，或 seek replay 重复/漏放 action start。 |
| ability definition hash 不一致 | ability catalog hash、snapshot `DefinitionHash` | seek 使用了不同 ability catalog；应 fail fast，不能静默降级。 |
| UI cooldown 错但 ability state 对 | HUD selected frame、hotbar view model frame | HUD 消费 latest/live frame，而不是 Review/Seek selected frame。 |
| ability state 对但动作没启动 | CharacterAction Runner、action binding、input intent | Ability 只负责 cooldown / action binding；动作执行仍要看 CharacterAction Runner。 |

## Guard Aura / Aura Buff 同步链路

Guard Aura 是 CharacterAction gameplay request 驱动的 component ability / buff 链路。它不是 projectile，也不是单纯的 VFX 或 Runtime AI Planner 目标线；权威结果必须落到 Gameplay component world 中的 aura source、buff request、buff set 和 status / modifier projection。

Guard Aura 主链路是：

```text
CharacterAction gameplay track frame
  -> CharacterActionGameplayRequest(GuardAuraGameplayRequestId)
  -> CharacterTestCharacterCombatTraceSystem.ProcessGuardAuraRequests
  -> CharacterTestGuardAuraRuntime.EnqueueCast
  -> GameplayComponentAbilityCommandSystem
  -> GameplayComponentAuraSourceSetComponent
  -> CombatGameplayAuraBuffSystem
  -> GameplayBuffRequestBuffer
  -> GameplayBuffApplicationSystem / GameplayBuffEffectProjectionSystem
  -> GameplayComponentBuffSetComponent / status projection
  -> Runtime AI Lab probe / presentation
```

on-hit buff 链路也复用同一套 buff authority：

```text
Projectile / weapon accepted hit result
  -> CombatHitEventConfig.OnHitBuffs
  -> CharacterTestGuardAuraRuntime.ApplyOnHitBuffs
  -> CombatOnHitBuffApplicationSystem
  -> GameplayBuffRequestBuffer
  -> GameplayBuffApplicationSystem / GameplayBuffEffectProjectionSystem
  -> GameplayComponentBuffSetComponent / status projection
```

关键边界：

- `CharacterActionRunner` 只负责在确定 local frame 发出 `GuardAuraGameplayRequestId`；它不是 aura / buff 的权威存储。
- `CharacterTestGuardAuraRuntime` 负责把 gameplay request 转为 aura source / on-hit buff request，并输出低频 tick summary。
- Gameplay component world 中的 buff set、status projection 和 modifier projection 才是角色是否带有 aura buff 的权威结果。
- Unity head pad、buff icon、VFX、Runtime AI Lab overlay 都只读跟随；seek/review 后必须从目标帧 authority 强制刷新，不能反向修正 runtime state。

Runtime AI Lab probe 采样两层数据：

| Probe layer | 采样内容 | 用途 |
| --- | --- | --- |
| `guardAura` | `GuardAuraLastTickSummary`、`OnHitBuffLastSummary` 的 source/query/apply/reject/skip 计数 | 定位 request -> aura / on-hit buff bridge 是否在目标帧重放一致。 |
| `characters[].buff* / status*` | `GameplayComponentBuffSetComponent`、status projection 的 count/hash | 定位 buff authority 是否真正落到角色 gameplay entity 上。 |

当前自动化覆盖：

1. `RuntimeAiLabFrameSyncProbe_ReportsGuardAuraMismatch`：人工构造 `guardAura` summary mismatch，确认 probe 能定位到 `lastTick.*` 和 `onHit.*` 字段。
2. `RuntimeAiLabFrameSyncProbe_CapturesGuardAuraRefreshSummary`：直接发 Guard Aura gameplay request，确认 aura refresh summary、角色 `BuffCount` 和字段级 `buff.entry.<id>.*` 可被 probe 采样。
3. `RuntimeAiLabHeadlessSeek_ReplaysGuardAuraBuffExactly`：从 Guard Aura request 前的 character/action checkpoint 恢复并正向重放到 aura buff 生效帧，断言 guardAura summary、character action runner、buff count/hash、字段级 buff/status entry 和角色 sample 与首次运行一致。

排查表：

| 现象 | 先查 | 典型原因 |
| --- | --- | --- |
| `guardAura.lastTick.*` 不一致 | `ProcessGuardAuraRequests`、`EnqueueCast`、physics query、`CombatGameplayAuraBuffSystem` | seek 重放漏 / 重复 Guard Aura gameplay request，或 aura source query 的输入帧不一致。 |
| `guardAura.onHit.*` 不一致 | accepted hit result、on-hit buff config、`ApplyOnHitBuffs` | projectile / hit result 一致性不足，或 on-hit buff bridge 漏字段。 |
| `guardAura` summary 一致但角色 buff hash 不一致 | `GameplayBuffApplicationSystem`、`GameplayBuffEffectProjectionSystem`、component world restore/replay | buff request 被发出，但 gameplay component world 没有恢复 / 投影到同一目标帧。 |
| buff authority 一致但图标 / VFX 错 | head pad / buff presenter / VFX presenter selected frame | presentation 使用 latest/live frame，或 seek 后没有强制刷新表现层。 |

## Physical Shield 同步链路

Physical Shield 是独立的 combat runtime 状态，不是角色 transform 或 Runtime AI Planner snapshot 的派生值。它同时持有两类权威数据：

- CharacterTest 自己的 active shield 记录：owner、shield entity id、physics body id、center、end frame。
- `CombatShieldRuntime` 的护盾强度状态：owner、body / collider id、base/current motion / cohesion / scatter、cohesion limit、recovery、last impact frame、pressure band、broken state。

请求链路是：

```text
RuntimeCommand / Ability / CharacterAction request
  -> CharacterTestPhysicalShieldRuntime.EnqueueCast
  -> CombatPhysicsWorld body + collider
  -> CombatShieldRuntime active shield potency
  -> CharacterTestNetworkPhysicalShieldRuntimeSnapshot / state hash
  -> Runtime AI Lab probe / shield presentation
```

关键边界：

- `CharacterTestPhysicalShieldRuntime` 创建的是 gameplay-owned physics body 和 shield potency；Unity 预览体、debug capsule 或线框只读跟随。
- shield body id、shield entity id 和 `CombatShieldRuntime` potency 必须一起恢复。只恢复 active shield 列表但不恢复 potency，会导致碰撞存在但强度 / recovery / broken state 错。
- seek/review 后应先恢复 checkpoint 中的 shield runtime，再正向重放后续命令；不能从当前 live shield runtime 继续沿用旧 allocator 或旧 active shield 集合。
- `EndFrame` 是权威生命周期字段；剩余持续时间是 `EndFrame - RuntimeFrame` 的派生值，不作为独立权威字段。

### Snapshot / hash 覆盖

CharacterTest network snapshot schema `16` 开始覆盖 Physical Shield runtime：

- active shield count 和每个 active shield 的 owner、shield entity id、body id、center raw、end frame。
- `CombatShieldRuntimeState` 的 schema / module id。
- 每个 `CombatShieldPotencyComponent` 的 identity、base/current potency、recovery、last impact frame、pressure band、broken state。

state hash 同步加入 Physical Shield：

- full hash 覆盖所有 active shield 和 shield potency。
- owned hash 只覆盖 owner 属于 owned set 的 shield。
- non-owned hash 排除 owner 属于 owned set 的 shield。

这让 `+10 / -10 / +10` 能直接暴露 shield body、shield identity、potency 或 tick summary 的遗漏恢复，而不是只在角色线框或阻挡表现错乱时才发现。

### Runtime AI Lab probe 层级

`CharacterTestRuntimeAiLabFrameSyncProbe` 低频采样 Physical Shield runtime，并作为独立 `physicalShield` layer 比较：

- runtime 可用性
- schema version 和 module id
- active shield count
- shield runtime schema / module id / shield count
- active shield owner、shield entity id、body id、center raw、end frame
- shield potency owner、body / collider id、current motion / cohesion / scatter、current cohesion limit、last impact frame、pressure band、broken state
- last tick summary：active、created/refreshed、expired

Compare 失败时必须定位到具体 shield entity 和字段，例如：

```text
physicalShield/entity 30001/active.centerXRaw expected=1000 actual=2000
physicalShield/entity 30001/shield.currentCohesion expected=38 actual=40
physicalShield/entity 0/lastTick.createdOrRefreshedCount expected=1 actual=0
```

Probe 仍然只读；不得在检查期间刷新 shield presenter、overlay、presentation 或 timeline cursor。

### 自动化验收

Runtime AI Lab 的 Physical Shield 测试覆盖三类风险：

1. 人工构造 shield mismatch，确认 probe 能定位到 `physicalShield/entity/field`。
2. Snapshot roundtrip：构造 active shield + `CombatShieldRuntimeState`，通过 `CharacterTestNetworkPhysicalShieldRuntimeSnapshot` 转换后重新采样，断言 shield sample 与原始状态一致。
3. Headless ability replay：由 `PhysicalShieldIntentId` 启动真实 CharacterAction，经过 gameplay request 创建 shield；测试记录创建前 checkpoint 和 active shield 目标帧，恢复 checkpoint 后正向重放，断言 active shield、shield potency、tick summary 和 probe sample 全部一致。对应 focused 测试是 `RuntimeAiLabHeadlessSeek_ReplaysPhysicalShieldAbilityExactly`。

### 排查表补充

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| physical shield active count 不一致 | active shield list、checkpoint shield snapshot、seek restore result | snapshot 未覆盖 shield runtime，或 restore 后没有清理旧 active shield。 |
| shield body / entity id 不一致 | shield entity id、physics body id、CombatPhysics body existence | restore 后重新分配 id，或 replay 重复创建 shield body。 |
| shield potency 不一致 | `CombatShieldRuntimeState.Shields`、current cohesion / scatter / broken state | 只恢复了 CharacterTest active shield list，未恢复 CombatShieldRuntime potency。 |
| shield center / blocking 错 | active shield center raw、physics body center、CombatPhysicsWorld body | body center 未随 snapshot 恢复，或 presentation/debug 使用旧 body。 |
| shield authority 一致但视觉错 | shield presenter / debug capsule / Runtime AI Lab overlay | seek 后表现层未强制同步，或 presenter 仍在使用 latest/live frame。 |
## Movable Natural Object / Genesis Potency 同步链路

可移动自然物是 CharacterTest 场景内的 gameplay-owned authority，不是 Unity 场景摆件。它们会被角色碰撞、Interaction field、Sword Wave 等确定性系统推动，也会携带 Genesis potency 状态。Runtime AI Lab 做精确回看时，必须验证这层权威状态，而不是只看 Unity 预览体或 debug collider。

权威链路：

```text
WorldObjectSceneSource / setup spawn specs
  -> CharacterTestMovableNaturalObjectState
  -> CharacterTestMovableNaturalObjectSystem.BeginFrame / Tick
  -> interaction field / character contact / static obstacle contact
  -> CharacterTestMovableNaturalObjectSnapshot / state hash
  -> Runtime AI Lab probe / movable object presentation
```

关键边界：
- `CharacterTestMovableNaturalObjectState` 保存权威位置、速度、接触压力、接触来源、行为状态和 potency 状态。
- `ContactPressure`、`ContactPair`、`ContactDisplacement*` 是帧敏感诊断字段；`BeginFrame` 会清理本帧接触摘要，所以回看检查必须采样目标帧，而不是晚一帧看表现。
- `CurrentPotency`、`PendingPotency` 和 `LatestPotencyApplication` 属于权威 gameplay 状态；Unity 材质、特效、颜色提示只读跟随。
- 静态墙体和地面 collider 是确定性碰撞输入；可移动自然物自身的位置和 potency 是可回放的运行时状态。

Runtime AI Lab probe 新增 `movableObject` layer，字段级采样：
- identity：`numericId`、`stableId`、`category`
- shape/material：`massGrams`、`materialKind`、`shapeKind`、half extents
- authority transform：`x/y/z`
- velocity：`velocityX/Y/Z`
- contact summary：`contactVelocityX/Z`、`contactDisplacementX/Z`、`contactPressure`、`contactCount`、`contactPair`、`behaviorState`
- potency：base/current/pending triplet、pending influence、latest potency frame / target / influence / applied delta / after triplet / result

Compare 失败必须定位到具体 movable object 和字段，例如：

```text
movableObject/entity 41/x expected=1200 actual=1800
movableObject/entity 41/contactPressure expected=50 actual=75
movableObject/entity 41/currentPotencyMotion expected=10 actual=24
movableObject/entity 41/latestPotencyFrame expected=22 actual=23
```

Runtime AI Lab 自动化测试覆盖两类风险：

1. `RuntimeAiLabFrameSyncProbe_ReportsMovableNaturalObjectMismatch`：人工构造 movable object mismatch，确认 probe 能定位到 `movableObject/entity/field`。
2. `RuntimeAiLabHeadlessSeek_ReplaysMovableNaturalObjectPotencyExactly`：在 headless `GameSlice` 中对 movable object 施加 Fire / Hammer / Condense potency influence，记录 checkpoint 和目标帧，再恢复 checkpoint 正向重放；目标帧的 movable object sample、state hash、contact / potency 字段必须与第一次运行完全一致。

排查表补充：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| movable object 位置不一致 | `x/y/z`、velocity、checkpoint restore、command replay | seek 恢复了角色但漏恢复可移动物，或重放期间重复/漏应用 contact push。 |
| 接触压力或 contactPair 不一致 | `BeginFrame` 采样时机、Interaction history、object/static contact query | 采样晚了一帧，或 replay 时 interaction/object contact 顺序不同。 |
| potency 字段不一致 | `CurrentPotency`、`LatestPotencyApplication`、potency influence command | snapshot 漏字段，或 replay 重复/漏应用 Fire/Hammer/Condense 影响。 |
| authority 一致但视图错 | `CharacterTestMovableNaturalObjectUnityViewBridge.Sync`、presentation force sync | seek 后表现层没有强制跳到目标帧，仍在使用 live/latest 表现状态。 |

## Runtime Command State 同步链路

Runtime command state 是 CharacterTest 帧同步里的输入和开发状态边界。它不直接代表某个角色的移动或战斗结果，但会决定同一帧里哪些命令被消费、镜头/锁定目标等运行时开关处于什么状态。Runtime AI Lab 做 seek/review 时，如果这层错了，后续角色、动作、投射物或表现层可能看起来都像是“回到了错误分支”。

权威链路：

```text
RuntimeCommandBuffer / accepted RuntimeCommand
  -> CharacterTestRuntimeCommandModule.DrainFrameCommands
  -> camera mode / target lock / behavior selection phase state
  -> CharacterTest network snapshot / state hash
  -> Runtime AI Lab checkpoint restore + command replay
  -> Runtime AI Lab FrameSyncProbe command layer
```

关键边界：

- `CameraMode`、`TargetLockEnabled`、`LockedTargetEntityId`、`RuntimeAiBehaviorSelectionUnlocked` 属于 runtime command module 的权威状态。它们必须随 checkpoint restore 和 snapshot apply 恢复，而不是由 HUD 或 Unity camera presentation 反推。
- `PendingCommandCount`、`LastDrainedCommandCount`、`LastDrainedCommandHash`、`LastCommandErrorCount` 是 Runtime AI Lab 的低频诊断采样，用来定位 seek/replay 后命令是否漏 drain、重复 drain 或顺序不同。
- `LastDrainedCommandHash` 只用于 Lab 诊断，不替代正式 state hash。它的用途是当 state hash 或表现错乱时，把问题快速收敛到“命令 replay 序列”还是“后续 gameplay authority 层”。
- `LastDrainedCommandHash` 比较命令的逻辑内容和 drained 列表内顺序，不把 `RuntimeCommand.Sequence` 作为必须一致的 replay 权威。`Sequence` 是本地 `RuntimeCommandBuffer.Enqueue` 生成的排序辅助，checkpoint restore 后重新入队会生成新的 sequence；只要目标帧 drained 命令的内容和顺序一致，Lab 回放不应因为本地 sequence 变化误报。
- Runtime AI Lab timeline 会完整记录每帧 `LastDrainedCommands` 作为诊断输入，但 seek/review 只重放离散外部命令。`RuntimeAiPlanner` source 的 `SetControlIntent` 是确定性系统派生命令，必须由恢复后的 Runtime AI Planner 重新生成；Lab headless / setup path 中 `PlayerInput` source 的每帧 `SetControlIntent` 也会由恢复后的 input bridge 重新生成。它们都不能把历史 drained command 二次入队，否则同一帧会出现重复控制命令。
- Review/Seek 不应截断未来 records；只有 Paused + Edit Gate 创建新分支时，才允许从当前帧之后丢弃旧 command timeline。

Runtime AI Lab probe 新增 `command` layer，字段级比较：

| 字段 | 用途 |
| --- | --- |
| `cameraMode` | 定位 seek 后镜头模式是否被恢复为目标帧状态。 |
| `targetLockEnabled` / `lockedTargetEntityId` | 定位目标锁定状态是否跨 checkpoint/replay 丢失。 |
| `runtimeAiBehaviorSelectionUnlocked` | 定位行为选择阶段开关是否错误影响 Runtime AI Planner profile slot。 |
| `pendingCommandCount` | 定位 seek/review 后是否残留未消费命令。 |
| `lastDrainedCommandCount` / `lastDrainedCommandHash` | 定位目标帧 drain 的命令数量和内容顺序是否一致。 |
| `lastCommandErrorCount` | 定位 replay 中是否出现命令拒绝或错误但没有被 HUD 明确暴露。 |

失败示例：

```text
command/entity 0/cameraMode expected=2 actual=0
command/entity 0/lastDrainedCommandHash expected=111 actual=222
command/entity 0/pendingCommandCount expected=0 actual=1
```

排查表补充：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| state hash 不一致，但角色/投射物字段暂时看不出差异 | `command.lastDrainedCommandHash`、`lastDrainedCommandCount` | seek replay drain 了不同命令，或同帧命令顺序不一致。 |
| seek 后镜头/锁定目标状态跳错 | `command.cameraMode`、`targetLockEnabled`、`lockedTargetEntityId` | snapshot/apply 漏恢复 runtime command module 状态，或 HUD 在 Review 模式写回了 live 状态。 |
| `+10 / -10` 后下一帧输入异常 | `pendingCommandCount`、seek lock、timeline selected frame | seek 期间残留 pending command，或 step/jump 命令重入。 |
| replay 中没有视觉报错但行为选择状态不同 | `runtimeAiBehaviorSelectionUnlocked`、profile slot / behavior selection authority | 行为选择阶段开关未按 checkpoint 恢复，导致同一帧使用了不同 evaluator。 |

自动化验收：

- `RuntimeAiLabFrameSyncProbe_ReportsCommandStateMismatch` 用人工 sample 验证 `command` layer 能定位 camera mode、target lock、behavior selection gate、pending / drained command 和 command error 差异。
- `RuntimeAiLabHeadlessSeek_ReplaysRuntimeCommandStateExactly` 使用 headless `GameSlice` 执行 `SetCameraMode -> SetTargetLock -> SetCameraMode -> SetTargetLock(false)`，从中间 checkpoint restore 后正向 replay 到目标帧，要求 command state、state hash、drained command count/hash 和相关角色 authority sample 全部一致。

## Runtime Event Queue / Recent Events 诊断同步链路

Runtime event queue 和 recent events 是 CharacterTest 的运行时诊断事件流。它们不替代 gameplay authority state，也不作为 Runtime AI Planner profile/schema、SaveState 或正式 replay/hash 的权威输入；但 Runtime AI Lab 做 seek/review 时，如果事件流错帧，HUD / Details 会显示“当前帧发生了什么”的错误结论，容易误导精准播放排查。

诊断链路：

```text
CharacterTestRuntimeCommandModule.RecordEvent
  -> RuntimeEventQueue<CharacterTestRuntimeEvent>
  -> RecentRuntimeEvents
  -> CharacterTest debug/HUD runtime event log
  -> Runtime AI Lab FrameSyncProbe runtimeEvent layer
```

关键边界：

- `RuntimeEventQueueSnapshot` 只暴露队列摘要：pending count、oldest/newest frame、next sequence 和 event type name。它用于定位 seek/review 后是否残留未 drain 的事件、事件帧范围是否错位。
- `RecentRuntimeEvents` 是低频诊断列表，记录最近接受/拒绝/应用/失败的 runtime event。它用于 HUD / Details 和人工排查，不是角色移动、动作、战斗或 Runtime AI Planner 的权威状态来源。
- Runtime AI Lab probe 比较 recent events 的 count/hash 和最后一个事件的 frame、type、command/source/target/entity、payload 和 message。这样可以判断 UI 事件日志是不是来自 selected frame，而不是 live/latest frame。
- `RuntimeCommand.Sequence` 仍不是 replay 权威；runtime event queue 的 `NextSequence` 也只作为诊断字段，用来定位 seek/review 后事件队列是否走了不同分支。
- Probe 只读事件队列和 recent events；不得在检查过程中 drain、enqueue、刷新 HUD 或修改 timeline cursor。
- runtime event 比较是显式诊断项：普通 authority replay sample 默认不包含它，避免非权威历史日志污染移动、战斗、投射物或 VFX 的一致性结论；Runtime event 专项测试和带 selected-frame 摘要的 Review/Seek 才启用该层。
- rollback restore 会清空 live event queue 和 recent events，防止未来分支的诊断事件泄漏到恢复后的帧；checkpoint 之后的事件由 recorded commands 正向 replay 重新生成。
- timeline frame snapshot 保存当帧的 runtime event 摘要。Review/Seek 选择历史帧时，Probe 使用 selected frame 的摘要，而不是把 live queue 当作历史权威；event queue 本身不进入 network snapshot、SaveState 或 gameplay state hash。

Runtime AI Lab probe 的 `runtimeEvent` layer 字段：

| 字段 | 用途 |
| --- | --- |
| `pendingCount` | 定位 seek/review 后是否残留未处理 runtime events。 |
| `oldestFrame` / `newestFrame` | 定位队列事件是否落在 selected frame 之外。 |
| `nextSequence` | 定位事件队列本地 sequence 是否沿用了错误分支。 |
| `eventTypeName` | 定位采样到的事件队列类型是否正确。 |
| `recentCount` / `recentHash` | 定位 recent event 日志是否和目标帧一致。 |
| `lastFrame` / `lastType` | 定位最后一条诊断事件是否错帧或错类型。 |
| `lastCommandId` / `lastSourceId` / `lastTargetId` / `lastEntityId` | 定位最后一条事件的命令和实体身份是否错位。 |
| `lastPayload0/1/2` / `lastMessage` | 定位最后一条事件的 payload 或拒绝原因是否来自错误帧。 |

失败示例：

```text
runtimeEvent/entity 0/pendingCount expected=1 actual=2
runtimeEvent/entity 0/recentHash expected=101 actual=202
runtimeEvent/entity 0/lastFrame expected=21 actual=22
runtimeEvent/entity 0/lastMessage expected=camera accepted actual=target rejected
```

排查表补充：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| Details 事件日志显示了 latest frame 的拒绝原因 | `runtimeEvent.lastFrame`、`lastMessage`、selected frame | recent event list 没有随 seek/review 切到 selected frame。 |
| seek 后 HUD 说有 pending event，但 gameplay state 正常 | `pendingCount`、`oldestFrame/newestFrame` | checkpoint restore 或 seek lock 后残留了不该保留的 runtime event queue。 |
| replay 分支看起来正确，但事件日志 hash 不同 | `recentHash`、`lastType`、`lastCommandId` | 同一帧诊断事件记录顺序或内容不同，需要核对 command drain 和 RecordEvent 调用路径。 |

自动化验收：

- `RuntimeAiLabFrameSyncProbe_ReportsRuntimeEventMismatch` 用人工 sample 验证 `runtimeEvent` layer 能定位 pending queue、recent hash 和最后事件字段差异。
- `RuntimeAiLabFrameSyncProbe_UsesSelectedFrameRuntimeEvents` 验证 Review/Seek 使用 timeline selected frame 保存的事件摘要，不误读 live/latest runtime event log。
- `RuntimeAiLabHeadlessSeek_ReplaysRuntimeEventsExactly` 从无事件的 checkpoint 开始执行两帧 camera commands，restore 后按 drained commands 正向 replay，并单独比较 event queue 摘要、recent count/hash 和最后事件字段。它是诊断事件回归测试，不是 authority frame-sync 通过条件。

## VFX Runtime / 表现实例诊断链路

VFX Runtime 是表现实例的 runtime state，不是 CharacterTest 的 gameplay damage authority，也不是 Unity 场景里的粒子对象本身。它把动作、Ability、投射物、命中、Interaction 等系统发出的表现请求转成 active VFX instance，但不得因此进入 CharacterTest 正式 authority snapshot/hash。

表现链路：

```text
CharacterAction / Ability / Projectile / Interaction / gameplay system
  -> VfxSpawnRequest / VfxStopRequest
  -> VfxRuntimeModule active instance state
  -> VFX presentation diagnostics / optional visual capture
  -> Unity VFX presenter
  -> Runtime AI Lab probe / scene overlay diagnostics
```

关键边界：

- `VfxRuntimeModule` 持有表现实例的 runtime state：module id、definition hash、next instance id、pending spawn/stop/event count，以及每个 active instance 的 identity、definition、lifetime、anchor、parameters、source frame、local frame、source/target/trace identity。
- schema `18/19` 曾把 VFX Runtime custom state 写入 CharacterTest network snapshot。schema `20` 已将其移出正式 authority snapshot/hash；旧 schema 只为读取兼容保留版本识别，不再定义当前帧同步合同。
- Unity particle、trail、line renderer、mesh presenter 和 scene object 都是表现层，只能从 `VfxRuntimeModule` 的 selected frame state 刷新，不能反向作为 seek/review 的权威输入。
- CharacterTest authority hash 不包含 VFX runtime state。如果 combat / projectile / interaction authority 正确而 VFX runtime 不一致，报告 `Presentation Diagnostics`，不能将该差异解释为帧同步失败。
- Runtime AI Lab probe 只读 VFX runtime，不在检查时刷新 presenter、overlay 或 timeline cursor。

Runtime AI Lab probe 新增 `vfx` layer，字段级比较：

| 字段 | 用途 |
| --- | --- |
| `runtimeAvailable` / `moduleId` | 定位目标帧是否存在 VFX runtime，以及是否恢复到同一 module。 |
| `definitionHash` | 定位 VFX definitions 是否与记录帧一致。 |
| `nextInstanceId` | 定位 restore 后实例 allocator 是否沿用错误分支。 |
| `activeCount` / `instance.count` | 定位 active VFX instance 是否漏恢复、重复生成或提前结束。 |
| `pendingSpawnCount` / `pendingStopCount` / `pendingEventCount` | 定位 seek/replay 后 pending request 是否残留或漏 drain。 |
| `definitionId` / `prefabResource` | 定位同一 instance 是否绑定到错误 VFX 定义或资源。 |
| `startFrame` / `expireFrame` / `localFrame` | 定位生命周期和本地帧是否随 checkpoint/replay 恢复。 |
| `anchorKind` / `anchorSourceId` / `anchorTargetId` / `anchorSocketId` | 定位 VFX 锚点来源是否仍指向目标帧的实体和 socket。 |
| `anchorPosition*Raw` / `anchorForward*Raw` | 定位锚点姿态是否消费了 live/latest frame。 |
| `parameter0..3` | 定位颜色、半径、强度等参数是否随表现请求恢复。 |
| `sourceDomain` / `sourceFrame` / `sourceOrder` / `triggerId` / `sourceId` / `targetId` / `traceId` | 定位 VFX request 来源是否与动作、投射物、命中或 interaction 事件一致。 |

失败示例：

```text
vfx/entity 0/activeCount expected=1 actual=2
vfx/entity 101/definitionId expected=7101 actual=7102
vfx/entity 101/anchorPositionXRaw expected=1000 actual=1200
vfx/entity 101/localFrame expected=4 actual=5
vfx/entity 101/traceId expected=vfx-a actual=vfx-b
```

排查表补充：

| 现象 | 优先检查 | 可能问题 |
| --- | --- | --- |
| VFX active count 不一致 | `vfx.activeCount`、checkpoint VFX runtime、pending spawn/stop request | restore 漏恢复 VFX runtime，或 seek replay 重复发出 / 漏发 VFX request。 |
| VFX instance id / next id 不一致 | `nextInstanceId`、`instanceId`、spawn request source order | allocator 没有按 checkpoint 恢复，导致同一帧创建了不同 instance。 |
| VFX local frame 不一致 | `startFrame`、`expireFrame`、`localFrame` | restore 后没有跳到 selected RuntimeFrame，或 presenter/runtime 仍跟随 live frame。 |
| VFX anchor 不一致 | anchor source/target/socket、anchor position/forward raw | anchor resolver 使用了 Unity view 或 latest entity pose，而不是 selected frame authority pose。 |
| gameplay authority 一致但 VFX 错 | `vfx` layer、Unity VFX presenter force sync | combat/projectile/interaction 已正确恢复，但 VFX runtime 或 presenter 没有跟随 selected frame。 |

自动化验收：

- `RuntimeAiLabFrameSyncProbe_ReportsVfxRuntimeMismatch` 覆盖字段级 probe 诊断，确保 active count、instance identity、anchor、parameters、source/target/trace 等字段出现偏差时能定位到 `vfx` layer。
- `RuntimeAiLabHeadlessSeek_ReplaysVfxRuntimeInstancesExactly` 属于表现诊断回归：它验证 VFX request 可被稳定重建和比较，但结果不进入 authority frame-sync pass/fail。后续若需要逐帧视觉回放，应迁移到默认关闭的独立 Visual Capture 工具。

## Runtime AI Planner 行为选择权威同步链路

Runtime AI Planner 的行为选择权威状态在角色 `Control` 组件上，而不是 HUD 文本、行为组编辑器草稿或 Runtime AI Planner debug snapshot。精确回看时，同一 `RuntimeFrame` 必须恢复同一 `ControlOwner`、`RuntimeAiPlannerProfileSlot` 和 `RuntimeAiBehaviorSelection`，否则即使角色位置、动作 Runner 和 combat 状态一致，下一帧也可能使用不同 evaluator，导致目标选择、action 输出和 command 全部发散。

链路是：

```text
SpawnSpec / SetRuntimeAiBehaviorSelection command
  -> CharacterTestCharacterControlComponent
  -> CharacterTestNetworkCharacterSnapshot control / RuntimeAiBehaviorSelection fields
  -> checkpoint restore / snapshot apply
  -> Runtime AI Planner ResolveEvaluator(profileSlot)
  -> Runtime AI Lab FrameSyncProbe control layer
```

同步边界：

- `ControlOwner` 决定角色是否由本地玩家、远端玩家、脚本或 Runtime AI Planner 控制。
- `RuntimeAiPlannerProfileSlot` 是 Runtime AI Planner evaluator 的权威选择入口。
- `RuntimeAiBehaviorSelection` 只保存稳定 `loadoutId / variantId / catalogHash / profileSlot` 摘要；不保存行为组 JSON、Editor 草稿、compiled evaluator 或 UI 文案。
- `RuntimeAiBehaviorSelectionUnlocked` 是 command module 的 phase gate，仍归 `command` layer；每个角色当前选择归 `control` layer。
- Runtime AI Planner agent snapshot 中的 loadout/profile 字段只是 debug 投影，不能替代 `control` layer 校验。

FrameSyncProbe 的 `control` layer 覆盖：

- `owner`
- `runtimeAiPlannerProfileSlot`
- `runtimeAiBehaviorHasSelection`
- `runtimeAiBehaviorLoadoutId`
- `runtimeAiBehaviorVariantId`
- `runtimeAiBehaviorCatalogHash`
- `runtimeAiBehaviorProfileSlot`

排查表：

| 现象 | 优先检查 | 说明 |
| --- | --- | --- |
| 回看后同一角色下一帧选了不同 action | `control.runtimeAiPlannerProfileSlot`、`planner.selectedActionId` | 角色权威 profile slot 恢复错，导致 planner debug 只是结果层发散。 |
| Arena / Lab 编辑后回看使用旧策略 | `control.runtimeAiBehaviorLoadoutId`、`control.runtimeAiBehaviorVariantId`、timeline edit branch | 编辑分支没有截断旧未来帧，或 checkpoint restore 没恢复行为选择。 |
| replay catalog 不匹配但没有明确报错 | `control.runtimeAiBehaviorCatalogHash` | catalog hash 是行为选择兼容性证据，不能静默降级到默认策略。 |
| UI 显示一个策略但角色执行另一个策略 | `control` layer 与 `planner.runtimeAiBehavior*` | UI / planner snapshot 可能读取 latest frame；authority 必须以角色 Control 为准。 |

## Runtime AI Planner 决策快照同步链路

Runtime AI Planner 决策快照是 Runtime AI Lab 的诊断数据，不是 SaveState / network snapshot / replay hash 的权威状态。它的作用是把同一帧 Agent 的目标、感知事实、选中 action、utility score/cost、输出 command 和场景预览几何暴露给 HUD / Overlay。精确回看时，如果角色、动作、投射物都一致，但 Lab 仍显示了错误的目标线、错误 action 或错误 command，优先检查这一层是否仍在消费 live/latest frame。

链路是：

```text
Runtime AI Planner evaluator Sense / SelectAction
  -> CharacterTestRuntimeAiPlannerAgentState
  -> CharacterTestRuntimeAiPlannerCommandModule.CreateSnapshot()
  -> Runtime AI Lab frame buffer / timeline selected frame
  -> Runtime AI Lab preview builder / overlay / HUD
  -> FrameSyncProbe planner layer
```

同步边界：

- `CharacterTestRuntimeAiPlannerAgentSnapshot` 是 debug-only typed snapshot；它不进入 Runtime AI Planner profile JSON，不进入 SaveState、network snapshot、replay/hash。
- selected frame 必须是唯一数据来源。Review / Seek 后，HUD 和 Overlay 不能继续读取 live latest snapshot。
- Probe 只读 selected frame 里的 planner snapshot，不刷新 evaluator，不重新 Sense，不查询 Unity 场景对象。
- 比较时把 float score、distance、geometry 坐标量化成 milli 值，避免 Unity 表现层浮点误差掩盖真正的错帧。

FrameSyncProbe 的 `planner` layer 至少覆盖：

- snapshot：`available`、`frame`、`profileId`、`agentCount`。
- target / perception：`targetEntityId`、`targetExists`、`targetDistanceMillimeters`、`targetVisible`、`targetLineOfSightBlocked`、`targetThreatScoreMilli`。
- navigation / environment：`movePathAvailable`、`moveDirectPathClear`、`moveCandidateIndex`、`environmentNearObstacle`、`environmentNearHazard`、`environmentNearUsefulObject`。
- decision：`activeStatePath`、`selectedActionId`、`selectedActionKind`、`selectedActionName`、`selectedUtilityScoreMilli`、`selectedActionCostMilli`、`selectedAbilitySlot`。
- command result：`lastCommand`、`suppressedReason`、`rejectReason`、`transitionFromStateId`、`transitionToStateId`。
- loadout selection summary：`runtimeAiPlannerProfileSlot`、`runtimeAiBehaviorLoadoutId`、`runtimeAiBehaviorVariantId`、`runtimeAiBehaviorCatalogHash`、`runtimeAiBehaviorProfileSlot`。
- preview geometry：`hasScenePreviewGeometry`、source / target center、decision move 和 attack range 的量化值。

排查表：

| 现象 | 优先检查 | 说明 |
| --- | --- | --- |
| 角色状态一致但 HUD action 错 | `planner.selectedActionId`、`planner.selectedActionName`、`planner.activeStatePath` | frame buffer selected snapshot 没有随 seek 更新，或 HUD 读取 latest snapshot。 |
| 目标线指向旧目标 | `planner.targetEntityId`、`overlay.targetEntityId`、selected frame | Runtime AI Planner snapshot 与 Overlay preview 输入不在同一帧。 |
| 移动箭头和 command 不一致 | `planner.lastCommand`、`planner.decisionMoveXMilli`、`command.lastDrainedCommandHash` | command state 和 planner debug snapshot 可能来自不同 RuntimeFrame。 |
| path / LOS 标签错 | `planner.targetVisible`、`planner.targetLineOfSightBlocked`、`planner.movePathAvailable` | 这些是帧内感知缓存派生结果；seek 后不能重新用当前 Unity 场景查一次。 |
| loadout / profile 显示错 | `planner.runtimeAiBehavior*` 字段 | 行为选择摘要是 debug 投影，不能替代 authority selection；错帧时会误导行为组排查。 |

## 角色碰撞 / 姿态 / Presence / BattleState 同步链路

本节补齐 Runtime AI Lab 回看中容易被误判为“模型或胶囊错位”的角色权威状态层。角色位置、动作 Runner 和 combat health 一致，并不代表碰撞胶囊、动作姿态碰撞 profile、实体销毁状态或 battle gate 也一致。`+10 / -10 / +10` 时如果这些字段漏恢复，可能出现角色模型位置正确但 debug capsule、命中、阻挡、可被选中或 Runtime AI Planner 是否可运行发生分歧。

权威链路是：

```text
CharacterAction collision profile / lifecycle / destroy request / combat result
  -> CharacterTestCharacterPhysicsComponent
  -> CharacterTestColliderPostureComponent
  -> CharacterTestEntityPresenceComponent
  -> CharacterTestBattleStateComponent
  -> CharacterTest network snapshot / state hash
  -> Runtime AI Lab checkpoint restore + command replay
  -> Runtime AI Lab FrameSyncProbe physics / posture / presence / battle layers
```

同步边界：

- `CharacterTestCharacterPhysicsComponent` 是角色 runtime 碰撞体基础参数和本帧接触摘要，包含胶囊尺寸、质量/半径/推挤缩放、是否阻挡、是否可推、contact velocity / displacement / count。
- `CharacterTestColliderPostureComponent` 是当前解析后的碰撞姿态，可能来自 standing、CharacterAction timeline、lifecycle、presence 或 clearance blocked；它决定 debug capsule 和 gameplay collision 使用哪套 height / radius / center / blocks / push。
- `CharacterTestEntityPresenceComponent` 是实体是否 active / pending destroy / destroyed 的权威状态。销毁请求帧、销毁完成帧、来源实体和 trace id 必须随 checkpoint 恢复。
- `CharacterTestBattleStateComponent` 是战斗 gate 的权威汇总，决定能否被选中、收伤害、收 scatter、开始普通动作、移动、运行 Runtime AI Planner，以及是否保持 terminal pose。
- Unity capsule gizmo、角色 view、head pad、VFX 和 Runtime AI Lab overlay 都是表现层，只能从上述 authority fields 刷新。

### BattleState snapshot schema 边界

- 从 CharacterTest network snapshot schema `19` 起，`lifeState`、`dominantReason`、`gateFlags`、`sinceFrame`、`lastTransitionFrame`、`sourceActionId` 和 `sourceTraceId` 全部进入 character snapshot、rollback clone 和 state hash。
- schema `19` restore 在基础角色组件、动作 Runner、动画、移动、物理、combat、attributes 和 authority pose 恢复后，最后精确写回 `CharacterTestBattleStateComponent`；不得再用 `RefreshEntity(..., RuntimeFrame.Zero)` 覆盖原始时间字段。
- schema `0-18` 仍可加载，但没有精确 BattleState。兼容路径只能在 snapshot 实际帧基于旧字段做 best-effort 派生，不能保证还原原始 `sinceFrame` / `lastTransitionFrame`；Runtime AI Lab 应把这种情况标记为非精确恢复，而不是静默宣称 frame sync 完整通过。

Runtime AI Lab probe 的新增层：

| Layer | 字段重点 | 用途 |
| --- | --- | --- |
| `physics` | `capsuleRadius`、`capsuleHeight`、`massScaleMilli`、`radiusScaleMilli`、`pushScaleMilli`、`contact*` | 定位胶囊尺寸、推挤和本帧 contact 是否回到目标帧。 |
| `posture` | `profileId`、`height`、`radius`、`source`、`priority`、`actionProfileId`、`actionRestorePolicy` | 定位动作/生命周期引起的碰撞姿态是否恢复。 |
| `presence` | `state`、`requestedFrame`、`destroyedFrame`、`sourceEntityId`、`traceId` | 定位 entity active / pending destroy / destroyed 是否与 selected frame 一致。 |
| `battle` | `lifeState`、`dominantReason`、`gateFlags`、`sinceFrame`、`lastTransitionFrame`、`sourceActionId`、`sourceTraceId` | 定位 battle gate 是否和 combat/lifecycle 同步。 |

失败示例：

```text
physics/entity 1/capsuleRadius expected=350 actual=220
posture/entity 1/profileId expected=standing actual=corpse
presence/entity 1/state expected=Active actual=PendingDestroy
battle/entity 1/dominantReason expected=None actual=Death
```

排查表补充：

| 现象 | 优先检查 | 说明 |
| --- | --- | --- |
| 角色 transform 一致但绿色胶囊错 | `physics.*`、`posture.*`、presentation forced sync | 逻辑位置恢复了，但碰撞尺寸或 action/lifecycle posture 没恢复到 selected frame。 |
| 角色看起来还在但无法被选中 / 无法命中 | `presence.state`、`battle.gateFlags`、`combat.lifeState` | Presence 或 battle gate 已经进入 pending/destroyed/terminal，但 view 没同步。 |
| seek 后 Runtime AI Planner 不再运行 | `battle.gateFlags` 中 `CanRunRuntimeAiPlanner`、`control.owner` | battle gate 或 control owner 恢复错误，导致 planner 被正确/错误地跳过。 |
| 动作姿态导致碰撞错误 | `posture.actionProfileId`、`posture.actionRestorePolicy`、`runner.localFrame` | CharacterAction timeline 的 collision profile restore 与 runner frame 不一致。 |

## 角色输入 / MovementState / Surface 同步链路

本节补齐角色移动 authority 的下一层细节：位置和速度一致并不代表下一帧一定一致。`CharacterTestCharacterInputComponent`、movement state、collision flags 和当前 surface material 都会影响下一帧移动、跳跃、闪避、技能请求、摩擦、反弹和路径表现。Runtime AI Lab 的 `+10 / -10 / +10` 回看如果只检查 transform / action / combat，仍可能漏掉“当前帧看起来一样，但下一帧开始发散”的问题。

权威链路是：

```text
SetControlIntent / Runtime AI Planner command
  -> RuntimeCommandBuffer drain
  -> CharacterTestRuntimeCommandModule
  -> CharacterTestCharacterInputComponent
  -> CharacterTestCharacterMotionSystem
  -> CharacterTestCharacterMovementStateComponent
  -> CharacterTestCharacterTransformComponent / CombatPhysics
  -> Runtime AI Lab FrameSyncProbe input / movement layers
```

同步边界：
- `Input` 保存确定性的 milli 轴值和一帧请求，例如 `MoveXMilli`、`WorldMoveXMilli`、jump pressed / held / released、primary / secondary attack、skill slot、ability slot、ability id 和 dodge。它不是 Unity 输入设备状态，也不能从 Unity view 反推。
- `MovementState` 保存会影响下一帧求解的状态，例如 `lastMoveInput`、jump buffer、coyote time、movement mode、movement lock、movement / rotation control scale、movement profile id、fallback 标记、physics enabled 和 collision flags。
- `SurfaceMaterial` 是当前接触地表的权威结果，包含 friction、bounce cap、material kind / stable id、elasticity 和 fixed bounce height。地表一致性错误可能导致同一位置下一帧摩擦、反弹或滑动完全不同。
- Unity 角色 view、debug capsule、HUD 和 Runtime AI Lab overlay 只能消费这些 authority 字段；它们不能作为恢复输入、MovementState 或 Surface 的权威来源。

Runtime AI Lab probe 的新增层：

| Layer | 字段重点 | 用途 |
| --- | --- | --- |
| `input` | `moveX`、`moveY`、`worldMoveX`、`worldMoveY`、`hasWorldMove`、jump / attack / debug / dodge / ability requests | 定位 command drain、Runtime AI Planner 输出或 UI 输入请求是否恢复到 selected frame。 |
| `movement` | `lastMoveInputX/Y`、jump buffer、coyote time、`movementMode`、`movementLocked`、control scales、movement profile、physics enabled、collision flags | 定位下一帧移动求解为什么发散。 |
| `movement` surface fields | `surfaceFrictionMilli`、`surfaceBounceSpeedCapY`、`surfaceMaterialKind`、`surfaceMaterialStableId`、elasticity / fixed bounce raw | 定位地表摩擦、反弹和材质派生状态是否与 selected frame 一致。 |

失败示例：

```text
input/entity 1/moveX expected=700 actual=-300
input/entity 1/abilityIdPressed expected=910002 actual=0
movement/entity 1/jumpBufferFramesRemaining expected=3 actual=0
movement/entity 1/surfaceMaterialKind expected=Rough actual=HardSlick
```

排查表补充：

| 现象 | 优先检查 | 说明 |
| --- | --- | --- |
| 当前帧位置一致，但下一帧开始偏移 | `input.move*`、`movement.lastMoveInput*`、`movement.controlScale*` | 当前 transform 只是结果；下一帧仍由 input 和 movement state 继续推进。 |
| 跳跃、闪避、技能在回看后重复或消失 | `input.jump*`、`input.dodgePressed`、`input.abilitySlotPressed`、`input.abilityIdPressed` | 一帧请求如果没有随 selected frame 恢复，会出现重复触发或漏触发。 |
| 同一位置回看后滑动 / 反弹不同 | `movement.surface*`、`movement.collisionFlags` | Surface material 和 collision flags 是 motion solver 的派生输入，不能只看 transform。 |
| Overlay 移动箭头和下一帧实际移动不一致 | `input.worldMove*`、`planner.lastCommand`、`overlay.decisionMove*` | Overlay 可能读了 planner snapshot；真实下一帧还要核对 input command 是否同帧。 |

## Runtime AI Planner 连续移动决策状态同步链路

Runtime AI Planner 的 debug snapshot 不只描述“这一帧选了哪个 action”。部分移动决策字段会影响下一帧输出，尤其是 strafe / reaction 这类连续状态。如果 Runtime AI Lab seek 后只比较 action id、target、command 和目标线，可能漏掉“当前帧看起来仍在 Strafe，但下一帧横移方向或反应延迟不同”的问题。

权威链路是：

```text
Runtime AI Planner Sense / SelectAction
  -> CharacterTestRuntimeAiPlannerAgentState
  -> CharacterTestRuntimeAiPlannerDecision
  -> CharacterTestRuntimeAiPlannerStateSnapshot
  -> CharacterTestNetworkSnapshotDocument / rollback checkpoint / state hash
  -> CharacterTestRuntimeAiPlannerAgentSnapshot
  -> Runtime AI Lab selected frame buffer
  -> FrameSyncProbe planner movement continuity fields
```

同步边界：
- `MovementSpeedScale` 是 planner 对当前 action 输出移动强度的调制结果；它和 runtime input 的 milli 轴值不是同一个字段，但二者必须来自同一 selected frame。
- `MovementStrafeSide` 决定绕行 / 横移方向；seek 后如果该值沿用 live 分支，角色下一帧会从同一位置走向相反方向。
- `MovementDirectionHoldFrames` 表示方向保持窗口；它用于稳定移动输出，漏恢复时会出现回看后一帧抖动或提前切向。
- `MovementReactionDelayFrames` 表示反应延迟剩余帧；它不是 UI 文案，回看后必须与 selected frame 的 planner snapshot 对齐。
- 这些字段不进入 Runtime AI Planner JSON profile，也不改变行为卡或 profile schema；但它们会影响下一帧 Runtime AI Planner 输出，因此必须进入 CharacterTest network snapshot、rollback checkpoint 和 state hash。
- `CharacterTestRuntimeAiPlannerStateSnapshot` 是低频 snapshot/rollback 数据，保存 evaluator 的 profile slot、agent state、HFSM state、last target、strafe/reaction/hold 等连续决策状态。Runtime Tick 仍只读 evaluator 内存状态，不读取 JSON 或 UI 文案。
- Runtime AI Lab 还会通过 selected debug snapshot 和 `planner` probe layer 定位 seek / overlay / frame buffer 是否读错帧；debug snapshot 是可视化输入，不是恢复权威来源。

FrameSyncProbe 的 `planner` layer 需要覆盖：

| 字段 | 用途 |
| --- | --- |
| `movementSpeedScaleMilli` | 定位 planner 输出移动强度是否与 selected frame 一致。 |
| `movementStrafeSide` | 定位 strafe 左右方向是否沿用错误分支。 |
| `movementDirectionHoldFrames` | 定位移动方向保持窗口是否被 seek/review 丢失。 |
| `movementReactionDelayFrames` | 定位 reaction delay 是否在回看后重置或跳帧。 |

自动化验收：

- `RuntimeAiLabHeadlessSeek_ReplaysRuntimeAiPlannerMovementStateExactly` 在 headless `GameSlice` 中生成两名 `AiControlled` 角色，让 Runtime AI Planner 自然输出移动命令；测试捕获 selected Runtime AI Planner frame snapshot，恢复早期 checkpoint 后重新推进到同一目标帧，再通过 FrameSyncProbe 比较 `planner` layer。
- 该测试不把 Runtime AI Planner 同帧生成的历史 `SetControlIntent` 命令二次入队；这些命令应由重放后的 Runtime AI Planner 自己重新生成。这样可以单独验证 evaluator 连续移动状态、角色 authority state 与 selected frame snapshot 是否能从 checkpoint 正确复现。

失败示例：

```text
planner/entity 1/movementSpeedScaleMilli expected=750 actual=1000
planner/entity 1/movementStrafeSide expected=-1 actual=1
planner/entity 1/movementDirectionHoldFrames expected=12 actual=0
planner/entity 1/movementReactionDelayFrames expected=4 actual=0
```

排查表补充：

| 现象 | 优先检查 | 说明 |
| --- | --- | --- |
| 同一帧 action 一致，但下一帧横移方向反了 | `planner.movementStrafeSide`、`input.worldMove*` | planner strafe state 和最终 input command 可能来自不同帧。 |
| 回看后移动方向抖动或提前切向 | `planner.movementDirectionHoldFrames` | 方向保持窗口没有随 selected planner snapshot 对齐。 |
| 暂停单帧后反应延迟被跳过 | `planner.movementReactionDelayFrames` | reaction delay 是连续状态；seek 后不能只重算当前 facts。 |
| Overlay 箭头显示正常但角色下一帧不一致 | `planner.movementSpeedScaleMilli`、`overlay.decisionMove*`、`input.worldMove*` | Overlay、planner snapshot 和 command input 必须同帧三方对齐。 |

## 动画姿态表现诊断链路

角色位置、CharacterAction Runner 和 action id 一致，只能证明 gameplay authority 到达同一帧，不能证明 Unity Playables 恰好处于相同 crossfade、clip time、IK、secondary bone 或 view-only ragdoll 姿态。这些内容属于 Presentation Diagnostics，不属于正式 Frame Sync。

当前 Runtime AI Lab 的 authority seek 顺序为：

1. 恢复最近稀疏 authority checkpoint 和 CharacterAction Runner。
2. 按历史外部命令固定 Tick 到目标 `RuntimeFrame`。
3. 比较目标帧 authority hash。
4. 从 authority action、motion、battle/status state 重建 `CharacterTestCharacterAnimationComponent`。
5. 同步 Unity view root，并使用 action key + Runner local frame 做 canonical animation rebuild。
6. 零时间刷新 Overlay / HUD；必要时单独运行 Presentation Diagnostics。

Runtime AI Lab timeline 不再逐帧保存 Playables、骨骼、IK、secondary bone 或 ragdoll 检查点，network snapshot schema `20` 也不再保存 `CharacterTestCharacterAnimationComponent`。因此 canonical rebuild 不能被描述为“录像级精确视觉回放”，但它也不会影响 Frame Sync authority 验收。

通用 `IMxAnimationRewindableBackend`、Playables capture/restore 和程序化骨骼恢复能力可以继续用于独立 Visual Capture 或编辑器预览测试；它们不能进入 authority hash、network snapshot 或默认 Lab timeline。若未来重新实现 Visual Capture，必须默认关闭、独立存储，并使用独立验收和术语。

旧综合诊断器仍可报告以下表现层差异：

| Layer | 字段重点 | 定位用途 |
| --- | --- | --- |
| `animationInput` | locomotion blend、speed、action key、grounded/airborne | 检查 canonical 动画输入是否从 authority 正确重建。 |
| `animation` | current/outgoing/blend clip、clip time、layer weight、fade | 检查 Playables 表现差异。 |
| `bonePose` | 稳定骨骼路径 local pose checksum | 检查最终骨骼表现差异。 |
| `procedural` | IK、secondary bone、view-only ragdoll | 检查程序化表现差异。 |

这些 layer 的失败只能报告为 Presentation Diagnostics mismatch。正式帧同步自动化只使用 `CharacterTestRuntimeAiLabAuthorityProbe` 检查 `RuntimeFrame + authority hash`；需要字段级定位时，再读取 authority subsystem 的 focused snapshot/hash 测试，不能用复制 Unity 表现状态掩盖 authority 发散。
