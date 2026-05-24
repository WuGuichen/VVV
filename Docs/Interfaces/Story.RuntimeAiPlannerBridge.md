# Story.RuntimeAiPlannerBridge 接口

> **本文的 AI 指 Runtime AI Planner**，即 `MxFramework.AI` 模块的游戏内轻量行为决策引擎。
> 本页记录 `MxFramework.Story.RuntimeAiPlannerBridge` 的 noEngine one-way projection API。

## 职责

`MxFramework.Story.RuntimeAiPlannerBridge` 把 Story authority blackboard 中明确白名单的 `StoryFactKey` 投影到调用方持有的 `IAiWorldState`。

它只做 Story -> Runtime AI Planner 方向的 fact copy：

- 读取 `IStoryBlackboard` 或 `StoryDirectorSnapshot.Facts`。
- 按 `StoryRuntimeAiProjectionProfile` 白名单将 Story fact 映射为 `AiFactKey`。
- 将支持的 `StoryValueKind` 写入调用方传入的 `IAiWorldState`。
- 对缺失、跳过、unsupported value kind 记录 diagnostics。

它不做：

- 不写回 `StoryBlackboard`。
- 不调用 `StoryDirector` mutation API。
- 不运行 planner、goal selector、action effect 或 Gameplay command。
- 不依赖 Runtime、Gameplay、Resources、UnityEngine、UnityEditor、Story.Runtime、Story.Unity 或 Story.Editor。

## 依赖

```text
MxFramework.Story.RuntimeAiPlannerBridge
  -> MxFramework.Story
  -> MxFramework.AI
```

## 当前公开接口

| 类型 | 用途 |
| --- | --- |
| `StoryRuntimeAiFactMapping` | 单条 `StoryFactKey` -> `AiFactKey` 映射 |
| `StoryRuntimeAiProjectionProfile` | 显式白名单 profile，构造时拒绝非法 / 重复 Story 或 AI fact key |
| `StoryRuntimeAiWorldStateProjector` | projection 入口，支持 `IStoryBlackboard`、`StoryDirectorSnapshot` 或 ordered fact list |
| `StoryRuntimeAiProjectionResult` | 本次投影的 projected / missing / skipped / unsupported 计数 |
| `StoryRuntimeAiProjectionDiagnostics` | 非权威 diagnostics 计数与 recent diagnostic ring |
| `StoryRuntimeAiProjectionDiagnostic` | 单条 missing / unsupported / skipped / invalid mapping 诊断 |

## Value Mapping

| `StoryValueKind` | Runtime AI Planner value |
| --- | --- |
| `Bool` | `bool` |
| `Int32` | `int`，raw 必须落在 Int32 范围内 |
| `Int64` | `long` |
| `Fix64` | `long` raw value |
| `StringRef` | unsupported，移除 stale AI fact 并记录 diagnostic |
| `None` | unsupported，移除 stale AI fact 并记录 diagnostic |

缺失策略：

- profile 中声明但 Story 当前缺失的 fact 会从 `IAiWorldState` 移除，避免保留旧状态。
- Story 中存在但不在 profile 白名单的 fact 不会写入 Runtime AI Planner，并记录 `SkippedUnlistedStoryFact`。
- unsupported kind 不写入 Runtime AI Planner，并移除对应 `AiFactKey` 的 stale value。

## 示例

```csharp
using MxFramework.AI;
using MxFramework.Story;
using MxFramework.Story.RuntimeAiPlannerBridge;

var profile = new StoryRuntimeAiProjectionProfile(new[]
{
    new StoryRuntimeAiFactMapping(new StoryFactKey(1001, 5001), new AiFactKey("story.signal.seen")),
    new StoryRuntimeAiFactMapping(new StoryFactKey(1001, 5002), new AiFactKey("story.signal.level"))
});

var worldState = new AiWorldState();
var diagnostics = new StoryRuntimeAiProjectionDiagnostics();

StoryRuntimeAiProjectionResult result =
    StoryRuntimeAiWorldStateProjector.Project(storyDirector.CreateSnapshot(), worldState, profile, diagnostics);
```

## 禁止事项

- Bridge API 不得把 `IAiWorldState` 作为 Story authority blackboard。
- Projection diagnostics 不进入 Story hash、Replay 或 SaveState。
- Runtime AI Planner action/effect 不得在本 bridge 中写 Story fact；需要改变 Story 时必须通过 Story `RuntimeCommand` 边界。
- 不把 WGame 角色、地点、剧情、Buff 或真实配置写入 bridge API。

## 测试入口

- `Assets/Scripts/MxFramework/Tests/Story.RuntimeAiPlannerBridge/`
