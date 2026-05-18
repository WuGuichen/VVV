# Character Control 接口

> 状态：v0.3 contract + command sources / motion modifier adapters / pressure reaction bridge
> 任务入口：`Docs/Tasks/CHARACTER_CONTROL_RUNTIME_00_DESIGN_CONTRACT.md`

## 职责

`MxFramework.CharacterControl` 是 noEngine 角色控制编排层。它把本地输入、Runtime AI Planner、Replay/Test source 或 UI 命令统一成 `CharacterCommand`，再驱动控制状态机、Combat Motion、Combat Action 和 Gameplay command。

它不读取 Unity 输入、不调用 Unity Physics、不拥有 HP/Buff/Ability source of truth，也不让 MxAnimation / Animator / Playables root motion 反向驱动权威状态。

## 程序集边界

```text
MxFramework.CharacterControl
  -> MxFramework.Core
  -> MxFramework.Runtime
  -> MxFramework.Combat
  -> MxFramework.Gameplay

MxFramework.CharacterControl.Input
  -> MxFramework.CharacterControl
  -> MxFramework.Input

MxFramework.CharacterControl.RuntimeAiPlannerBridge
  -> MxFramework.CharacterControl
  -> MxFramework.AI
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
| `CharacterActionRequest` | attack / skill / interact / dodge / cancel 等动作请求 DTO。 |
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
| `ICharacterActionConstraint` | 冷却、资源、状态等项目/Gameplay 限制的只读检查扩展点。 |
| `CharacterActionController` | 连接 `CombatActionRunner`、`RuntimeCommandBuffer` 和 `GameplayRuntimeCommandFactory` 的动作桥，并允许 interrupt flow 清理 queued action request。 |
| `CharacterActionEvent` | accepted / rejected / queued / started / command enqueued / finished / canceled 事件 payload。 |
| `CharacterActionResult` | 动作请求处理结果，包含 stable rejection reason、action instance id 和 accepted runtime command。 |
| `CharacterPressureReactionPolicy` | Gameplay pressure typed events 到 Character Control Reaction 的可配置策略。 |
| `CharacterPressureReactionController` | 消费 `PostureBreakEvent` / `GuardBreakEvent` / `ArmorBreakEvent` / `PressureBandChangedEvent`，验证 Gameplay id 映射，按策略取消动作并进入 Reaction。 |
| `CharacterPressureReactionResult` | pressure reaction 处理结果，包含 reaction kind、end frame、lock mask、action cancel result 和 stable rejection reason。 |
| `CharacterPressureReactionEvent` | recorded / reaction started / reaction finished / rejected 诊断事件 payload。 |
| `InputCharacterCommandSource` | 可选 Input adapter，把 `IInputProvider` snapshot / command queue 转成 `CharacterCommand`。 |
| `CharacterInputActionBinding` | Input intent 到 Combat action / Gameplay ability / cancel 的显式绑定。 |
| `RuntimeAiPlannerCharacterCommandSource` | Runtime AI Planner bridge，把 plan selected action profile 转成 `CharacterCommand`。 |
| `RuntimeAiCharacterCommandProfile` / `RuntimeAiCharacterCommandProfileRegistry` | Runtime AI Planner action id 到 move/action request 的稳定映射。 |
| `RuntimeAiPlannerCharacterCommandDiagnostics` | last goal、selected action、last command、suppressed reason 诊断。 |

## 使用约定

- 同一个 `RuntimeCommandBuffer` 仍只能由 GameplayRuntimeModule 等现有 owner drain；Character Action Controller 只 enqueue。
- `CharacterMotionResolver` 必须通过 `CombatKinematicMotor` 得到权威运动结果。
- motion modifier 只影响 `CombatMotionInput.MoveSpeedScale`；provider 不能写 Gameplay / Combat source of truth。
- `CharacterActionController` 可以调用 `CombatActionRunner.StartAction` / `ForceStartAction` / `ForceCancel`，但不修改 Combat action timeline。
- `CharacterPressureReactionController` 只消费 Gameplay typed pressure events，不直接写 posture / guard / armor / HP / Buff / Ability 状态。
- `PostureBreakEvent` 和 `GuardBreakEvent` 默认会先清理 queued action request、取消当前 Combat action，再让状态机进入 `Reaction`；`ArmorBreakEvent` 默认只记录反馈，项目可通过 `CharacterPressureReactionPolicy` 改成进入 Reaction。
- `PressureBandChangedEvent` 默认只记录 band 变化，避免和 break typed event 重复触发；需要 broken band 直接触发时必须显式打开 `BrokenBandChangeStartsReaction`，且事件必须是 pressure 上升导致的 band 升级。
- recovery、negative delta 或 band 回落不会刷新 active reaction window；组合根停用 pressure owner 时可调用 `FinishActiveReaction(...)` 主动释放控制锁。
- pressure reaction 会校验 `CharacterControlEntityRef.GameplayEntityId`；缺失映射或 entity mismatch 只输出 rejected result/event，不抛异常。
- Gameplay ability 只通过 `GameplayRuntimeCommandFactory` 生成 command，不直接写 Gameplay component store。
- cooldown / resource / status 限制通过 `ICharacterActionConstraint` 注入；CharacterControl 不内置具体属性 id、cost 或 status id。
- Input adapter 只读 `IInputProvider`，不直接读设备 API；Gameplay context 不可用时不输出 command，并丢弃当前 frame 及以前的 queued commands，避免 UI / cutscene 期间的动作在恢复后补发。
- Runtime AI Planner bridge 使用 `Runtime AI Planner` 公共接口和 pressure fact keys，不使用 AIAction Config 或 WGame 私有行为数据。
- Runtime AI Planner profile 的 `ActionRequest` 是 selection-edge one-shot；缓存复用、平滑复用和同 action 后续决策只继续输出移动、朝向、jump / sprint。
- `RuntimeAiCharacterCommandProfile` 未指定 `moveSpeedScale` 时默认 `1`；显式传入 `Fix64.Zero` 是合法配置，可用于站定施法、原地防御或停步等待。
- UI Toolkit、Audio、VFX、MxAnimation 和 debug overlay 只能消费事件 / snapshot。

## 测试入口

```text
Assets/Scripts/MxFramework/Tests/CharacterControl/
```
