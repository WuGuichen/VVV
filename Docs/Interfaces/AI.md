# Runtime AI Planner 接口

> **本文的 AI 指 Runtime AI Planner**，即 `MxFramework.AI` 模块的游戏内轻量行为决策引擎。
> 不包含 AIAction 配置数据迁移（AIAction Config）、LLM 辅助创作（Authoring AI Assist）或开发 Agent 工作流（Development Agent）。
> 参见 `Docs/INTERFACES.md` 的 AI Terminology 章节。

## 职责

AI 提供不依赖插件的轻量 Planner 基础设施：事实、目标、动作、条件、效果和规划器。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `AiFactKey` | AI 事实 key |
| `IAiWorldState` / `AiWorldState` | 事实集合 |
| `IAiGoal` / `AiFactGoal<T>` | 目标 |
| `IAiAction` / `AiAction` | 动作 |
| `IAiCondition` / `AiFactCondition<T>` | 前置条件 |
| `IAiEffect` / `AiSetFactEffect<T>` | 模拟效果 |
| `IAiGoalSelector` / `PriorityGoalSelector` | 目标选择 |
| `IAiPlanner` / `SequentialPlanner` | 规划器 |
| `AiPlan` | 规划结果 |
| `IAiAgent` / `IAiSensor` | 游戏层扩展点 |
| `RuntimeAiPressureFactKeys` | Runtime AI Planner pressure fact key 字符串契约；band 使用稳定 `int` 值，不引用 Gameplay enum |
| `ExploitPostureWeaknessGoal` | 基于 `target.posture.band` 的目标，默认 activation band 为 `2`（Cracked） |
| `PressureImpactData` / `PostureWeightEvaluator` | Planner 外层 action 排序工具，根据 pressure facts 和 action impact data 返回权重倍率 |
| `PostureAiSensorAdapter` | Gameplay posture / guard / armor pressure 到 Runtime AI Planner world state 的 bridge adapter |

## 使用约定

- `AiFactKey` 使用字符串 key，不使用 WGame 枚举或实体。
- Planner 对传入世界状态 clone 后模拟，不修改原始状态。
- `Apply` 只修改模拟状态；真实移动、攻击、技能执行由游戏层负责。
- 框架不依赖 CrashKonijn、行为树、Unity NavMesh 或具体怪物逻辑。

## Gameplay bridge

`MxFramework.AI.GameplayBridge` 是 Runtime AI Planner core 与 `MxFramework.Gameplay` 的薄桥接程序集。它引用 `MxFramework.AI` 和 `MxFramework.Gameplay`，保持 `noEngineReferences=true`，不引用 Combat、UI、UnityEngine 或 UnityEditor。

`PostureAiSensorAdapter` 实现 `IAiSensor`，从 `GameplayComponentWorld` 读取 `GameplayPosturePressureComponent`、`GameplayGuardPressureComponent` 和 `GameplayArmorIntegrityComponent`，再写入调用方传入的 `IAiWorldState`。构造时必须传入 `Func<int, GameplayEntityId>` self resolver；可选传入 target resolver。未传 target resolver 时，adapter 会移除所有 `target.*` pressure facts，避免保留旧目标状态。

输出 facts：

| Fact key | Value type | 来源 |
|----------|------------|------|
| `self.posture.band` / `target.posture.band` | `int` | `(int)GameplayPosturePressureComponent.CurrentBand`；稳定值为 Stable=0, Pressed=1, Cracked=2, Critical=3, Broken=4 |
| `self.posture.ratio` / `target.posture.ratio` | `float` | `CurrentPressure / MaxPressure` |
| `self.posture.broken` / `target.posture.broken` | `bool` | `GameplayPosturePressureComponent.IsBroken` |
| `self.guard.band` / `target.guard.band` | `int` | `(int)GameplayGuardPressureComponent.CurrentBand`；稳定值同 posture band |
| `self.guard.ratio` / `target.guard.ratio` | `float` | `CurrentPressure / MaxPressure` |
| `self.guard.broken` / `target.guard.broken` | `bool` | `GameplayGuardPressureComponent.IsBroken` |
| `self.armor.ratio` / `target.armor.ratio` | `float` | `CurrentIntegrity / MaxIntegrity` |
| `self.armor.broken` / `target.armor.broken` | `bool` | `GameplayArmorIntegrityComponent.IsBroken` |

缺失策略：resolver 返回 invalid / dead entity、component store 缺失、目标 entity 缺少对应 component，或 component state 无效时，adapter 移除对应 facts，不抛玩法异常。缺少 posture 只移除 posture facts；缺少 guard 或 armor 同理，不影响同一 entity 上其他 pressure facts。

`PostureWeightEvaluator.GetActionWeightModifier(actionId, worldState, impactData)` 不参与 `SequentialPlanner` BFS。调用方可在 planner 选出候选 action 后，用 `PressureImpactData(impactForce, isHighPoiseDamage, isDefensiveRecovery)` 叠加排序倍率：高 poise damage 会在目标 posture band 达到 Cracked 后提高进攻权重；defensive recovery action 会在自身 guard/posture/armor 压力升高时提高恢复权重。

## 最小示例

见 `Docs/USAGE.md` 的 AI 轻量 Planner 章节。

## 测试入口

`Assets/Scripts/MxFramework/Tests/AI/`
