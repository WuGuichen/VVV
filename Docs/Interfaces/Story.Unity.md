# Story.Unity 接口

> 状态：S4 Framework Feature 已实现（2026-05-24）。本文记录 Unity view/input adapter 最小可用 API；本切片不交付可玩 Story 场景。

## 职责

`MxFramework.Story.Unity` 是 Unity-facing runtime adapter。它只做两件事：

- 把 Unity trigger / UI / Timeline-style presentation acknowledgement 转成 Story `RuntimeCommand`。
- 把 `StoryRuntimeEvent` 分发给 Unity 表现回调。

它不拥有 Story 权威状态，不推进 Story state machine，不直接调用 `StoryDirector.TryRaiseTrigger`、`TryResolveChoice`、`CompletePresentation`、`TryEnterBeat` 或 `AbortGraph`。

## 依赖

```text
MxFramework.Story.Unity
  -> MxFramework.Story
  -> MxFramework.Story.Runtime
  -> MxFramework.Runtime
  -> UnityEngine
```

`MxFramework.Story.Unity` 是 runtime assembly，不引用 `UnityEditor`。

## 当前公开接口

| 类型 | 用途 |
| --- | --- |
| `StoryUnityCommandAdapter` | Unity command adapter 基类，显式绑定 `RuntimeCommandBuffer` 或 `StoryRuntimeModule` |
| `StoryUnityCommandResult` | 记录 enqueue 成功、命令、错误和状态文本 |
| `IStoryUnityFrameProvider` | Unity adapter 的当前 Runtime frame 来源 |
| `StoryUnityManualFrameProvider` | 测试和简单组合根使用的显式 frame provider |
| `StoryTriggerZoneAdapter` | trigger zone / trigger intent adapter，enqueue `Story.RaiseTrigger` |
| `StoryPresentationCompletionAdapter` | UI / Timeline / audio / camera 完成回调 adapter，enqueue `Story.CompletePresentation` |
| `StoryRuntimeEventPresentationRouter` | presentation-only event router，消费 `StoryRuntimeEvent` 并调用 Unity callbacks |
| `StoryRuntimeEventRoute` / `StoryRuntimeEventUnityEvent` | 按 `StoryEventKind` 分发表现事件 |
| `StoryRuntimePresentationEventPolicy` | 定义当前 presentation event kind 过滤策略 |

## 命令策略

组合根必须显式绑定 Story command buffer：

```csharp
using MxFramework.Story.Runtime;
using MxFramework.Story.Unity;

StoryRuntimeModule storyModule = CreateStoryModule();

var trigger = triggerGameObject.AddComponent<StoryTriggerZoneAdapter>();
trigger.Bind(storyModule);
trigger.TriggerId = 4001;

var completion = uiGameObject.AddComponent<StoryPresentationCompletionAdapter>();
completion.Bind(storyModule);
completion.CompletePresentation(beatInstanceId, stepId, graphId);
```

`StoryTriggerZoneAdapter.RaiseTrigger(...)` 使用 `StoryRuntimeCommandFactory.RaiseTrigger(...)`，默认 source id 为 `StoryRuntimeCommandSources.UnityAdapter`。

`StoryPresentationCompletionAdapter.CompletePresentation(...)` 使用 `StoryRuntimeCommandFactory.CompletePresentation(...)`，默认 source id 为 `StoryRuntimeCommandSources.PresentationAdapter`。

未绑定 command buffer 时，adapter 返回 `StoryUnityCommandResult.Success=false`，不会触碰 Story Director。

## 事件表现路由

```csharp
var router = viewGameObject.AddComponent<StoryRuntimeEventPresentationRouter>();
router.OnPresentationEvent.AddListener(evt => Debug.Log(evt.Kind));
router.AddRoute(StoryEventKind.StepStarted).Event.AddListener(PlayStepPresentation);

// 如果该 router 是 event queue 的 drain owner：
router.DrainAndRoute(storyModule.Events, currentFrame);
```

默认 presentation event kind 包括 graph completed / aborted、beat entered / exited、step started / completed、choice offered / resolved。`GraphLoaded` 和 `FactChanged` 默认不路由到表现层；Debug UI 可通过 Story.Editor 快照读取事实和 event queue 状态。

## 禁止事项

- Adapter 不直接调用 Story Director mutation API。
- Adapter 不 drain Gameplay command buffer。
- Runtime assembly 不引用 `UnityEditor`。
- Unity trigger / collider 只作为输入意图来源，不成为 Story 权威状态。
- Timeline / Cinemachine package-specific binding 未在 S4 固化；当前只提供通用 event router。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Story.Unity/`
