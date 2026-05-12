# AI 接口

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

## 使用约定

- `AiFactKey` 使用字符串 key，不使用 WGame 枚举或实体。
- Planner 对传入世界状态 clone 后模拟，不修改原始状态。
- `Apply` 只修改模拟状态；真实移动、攻击、技能执行由游戏层负责。
- 框架不依赖 CrashKonijn、行为树、Unity NavMesh 或具体怪物逻辑。

## 最小示例

见 `Docs/USAGE.md` 的 AI 轻量 Planner 章节。

## 测试入口

`Assets/Scripts/MxFramework/Tests/AI/`
