# Character Runtime 模块关系速查

> Status: Guide
>
> 本文只解释当前角色运行时相关模块的关系，帮助 Agent 避免误读依赖方向。具体 API 仍以 `Docs/Interfaces/*.md`、`*.asmdef`、源码和测试为准。

## 真实依赖

```text
Gameplay -> Core / Events / Attributes / Buffs / Modifiers / Runtime
Combat -> Core / Runtime
Combat.GameplayBridge -> Combat / Gameplay / Buffs / Runtime
CharacterAction -> Combat / Gameplay / Runtime
CharacterControl -> Core / Runtime / Combat / Gameplay / CharacterAction
Character.Application -> Config
Character.RuntimeSpawn -> Character.Application / Config / Resources / Runtime / Gameplay / Combat / CharacterControl
```

## 职责边界

- `Gameplay` 是运行时玩法状态和效果执行层，负责实体、技能、属性、Buff、Modifier、Gameplay command / event；它不读取 Unity、Combat、角色配置或场景对象。
- `Combat` 是确定性战斗权威层，负责固定步、物理查询、运动、命中结算、动作时间轴和武器轨迹；Combat core 不依赖 Gameplay。
- `Combat.GameplayBridge` 是 Combat 与 Gameplay 的桥接程序集；不要把它误读成 `Combat -> Gameplay`。
- `CharacterAction` 是角色动作解析、计划、唯一有状态 Gateway 和 Track 分发层；internal Runner 只能被 Gateway 持有。它直接依赖 Combat / Gameplay / Runtime，但不拥有 Combat hit shape、伤害结算、Gameplay 组件状态或动画资源。
- `CharacterControl` 是角色控制编排层。Motion 继续使用 `CombatKinematicMotor`；正式动作只通过 CharacterAction Gateway，CombatAction 与 Gameplay command 只由显式下游 track bridge 执行。
- `Character.Application` 是角色静态配置聚合和纯 resolver 层，只依赖 Config；它定义属性 profile、身体、装备 schema/loadout、能力 loadout、Combat action set、表现 profile 和 spawn profile，不保存 Runtime 状态、Buff 实例、Ability 执行或资源 handle。
- `Character.RuntimeSpawn` 消费 Character.Application / imported package 产物，生成 Gameplay / Combat / Resource / CharacterControl binding plan；它是应用层组合入口，不允许下层模块反向依赖 Character.Application。

## 常见连接流

1. 角色生成：`Character.Application config` -> `Character.RuntimeSpawn` -> `CharacterRuntimeBinding` -> Gameplay entity plan / Combat body plan / resource preload plan / `CharacterControlEntityRef`。
2. 动作层：intent request -> `CharacterActionResolver` -> `CharacterActionExecutionGateway` -> `CharacterActionTrackAdapter` -> 组合根实现的 Motion / Combat / Gameplay / Animation / Presentation sinks。
3. Combat 玩法反馈：Combat hit / guard / armor 结果 -> `Combat.GameplayBridge` -> Gameplay pressure / attribute / event -> CharacterControl reaction 或 CharacterAction reaction 输入。
4. CombatAnchored 动作：`CombatTrackConfig.CombatActionId` 解析到 `CombatActionTimeline`；`CharacterActionPlanDurationResolver` 使用 `CombatActionTimeline.TotalFrames`，`CharacterActionPhaseValidator` 校验 Startup / Active / Recovery phase anchor。`CharacterActionCombatTrackBridge` 可启动对应 CombatAction 子时间线，但只有外层 Gateway 决定角色动作生命周期。

## 编辑器关系

`Editor/CharacterAction` 可以读取 Character.Application、MxAnimation registry 和 ResourceCatalog 来做引用发现和下拉列表；这不是 `MxFramework.CharacterAction` runtime 对 Character.Application 的依赖。
