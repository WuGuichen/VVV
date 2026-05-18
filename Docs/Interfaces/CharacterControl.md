# Character Control 接口

> 状态：v0.1 contract + first implementation slice
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
```

禁止引用：

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
| `CharacterMotionResolver` | `CharacterCommand` + control state -> `CombatMotionInput` -> `CombatKinematicMotor.Step(...)`。 |
| `CharacterMotionResult` | 运动结果摘要，暴露 position、velocity、grounded、collision flags、applied delta 和 world sync 信息。 |
| `ICharacterActionConstraint` | 冷却、资源、状态等项目/Gameplay 限制的只读检查扩展点。 |
| `CharacterActionController` | 连接 `CombatActionRunner`、`RuntimeCommandBuffer` 和 `GameplayRuntimeCommandFactory` 的动作桥。 |
| `CharacterActionEvent` | accepted / rejected / queued / started / command enqueued / finished / canceled 事件 payload。 |
| `CharacterActionResult` | 动作请求处理结果，包含 stable rejection reason、action instance id 和 accepted runtime command。 |

## 使用约定

- 同一个 `RuntimeCommandBuffer` 仍只能由 GameplayRuntimeModule 等现有 owner drain；Character Action Controller 只 enqueue。
- `CharacterMotionResolver` 必须通过 `CombatKinematicMotor` 得到权威运动结果。
- `CharacterActionController` 可以调用 `CombatActionRunner.StartAction` / `ForceStartAction` / `ForceCancel`，但不修改 Combat action timeline。
- Gameplay ability 只通过 `GameplayRuntimeCommandFactory` 生成 command，不直接写 Gameplay component store。
- cooldown / resource / status 限制通过 `ICharacterActionConstraint` 注入；CharacterControl 不内置具体属性 id、cost 或 status id。
- UI Toolkit、Audio、VFX、MxAnimation 和 debug overlay 只能消费事件 / snapshot。

## 测试入口

```text
Assets/Scripts/MxFramework/Tests/CharacterControl/
```
