# Story.Editor 接口

> 状态：S4 Framework Feature 已实现（2026-05-24）。本文记录 Story runtime 只读 Editor debug surface。

## 职责

`MxFramework.Story.Editor` 提供 Unity Editor 内的 Story runtime 观察入口：

- 将 `StoryRuntimeModule` / `StoryDirectorSnapshot` 映射为 `IFrameworkDebugSource`。
- 使用 `DebugUiSnapshotAggregator` 和 UI Toolkit 构建只读窗口。
- 显示 graph、active beat、step、blackboard、event queue、recent event、recent command 和 command error 摘要。

它不写 Story state，不执行 hidden mutation，不绕过 `RuntimeCommandBuffer`。

## 依赖

```text
MxFramework.Story.Editor
  -> MxFramework.Diagnostics
  -> MxFramework.DebugUI
  -> MxFramework.Runtime
  -> MxFramework.Story
  -> MxFramework.Story.Runtime
  -> UnityEditor / UnityEngine.UIElements
```

该 assembly 仅包含在 Unity Editor。

## 当前公开接口

| 类型 | 用途 |
| --- | --- |
| `StoryRuntimeDebugSource` | `IFrameworkDebugSource` adapter，读取 `StoryDirector.CreateSnapshot()` 和 `RuntimeEventQueue<StoryRuntimeEvent>.CreateSnapshot()` |
| `StoryEditorDebugTarget` | Editor debug registry 中的 Story runtime target |
| `StoryEditorDebugRegistry` | Editor-only registry，供调试窗口选择当前 Story runtime target |
| `StoryEditorDebugWindowView` | 可测试的只读 UI Toolkit tree 构造器 |
| `StoryRuntimeDebugWindow` | 菜单 `MxFramework/Story/Runtime Debug`，显示只读 Story runtime 快照 |

## Debug Source Sections

`StoryRuntimeDebugSource.CreateSnapshot()` 当前输出：

- `摘要`：schema、next beat instance id、graph / active beat / blackboard / event queue 计数。
- `Graphs`：graph id、version、runtime status。
- `Beats`：beat instance id、graph / beat id、step index、pending presentation step、choice set。
- `Blackboard`：有序 `StoryFactKey=StoryValue`。
- `事件队列`：pending count、oldest / newest frame、next sequence、event type。
- `最近事件`：默认来自 `StoryRuntimeModule.RecentEvents`；注册 target 也可传入自定义 recent event provider。
- `最近命令` / `命令错误`：来自 `StoryRuntimeModule.LastDrainedCommands` 和 `LastCommandErrors`。

## 使用约定

运行时组合根或手测 harness 可在 Editor 环境注册 target：

```csharp
using MxFramework.Story.Editor;
using MxFramework.Story.Runtime;

StoryRuntimeModule module = CreateStoryModule();
StoryEditorDebugRegistry.Register("Demo Story Runtime", module);
```

打开菜单 `MxFramework > Story > Runtime Debug` 后，窗口显示已注册 target。没有 target 时窗口仍可打开，但只显示“没有已注册的 Story Runtime 调试源”。

窗口默认只读。若后续要加入 debug command，必须通过显式 command gate 或 enqueue Story `RuntimeCommand`，不得在 Editor UI 中直接调用 Story Director mutation API。

## 禁止事项

- 不把 Editor debug UI 状态写入 Replay、SaveState 或 Runtime hash。
- 不读取 Story Director 私有字段。
- 不提供默认可写按钮。
- 不把 Story.Editor 作为 runtime assembly 依赖。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Story.Editor/`
