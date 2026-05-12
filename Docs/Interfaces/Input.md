# Input 接口

> 状态：v0.1 | 依赖：Unity Input System | 代码：`Assets/Scripts/MxFramework/Input/`

Input 模块把 Unity Input System 作为底层采集层，只向业务暴露意图快照、瞬时命令和上下文控制。业务脚本不应直接读取 `Keyboard.current`、`Gamepad.current` 或散落持有 `InputAction`。

## 1. 模块边界

- `MxFramework.Input` 是 Unity 依赖程序集，引用 `UnityEngine` 和 `Unity.InputSystem`。
- 模块不依赖 WGame、Entitas、Luban、Gameplay、Runtime 或 Demo。
- noEngine 模块不得反向引用 Input；需要输入时由游戏组合根把 `IInputProvider` 传给上层控制器。
- 默认配置资产位于 `Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions`，第一版包含 `Gameplay`、`UI` 和 `Debug` 三个 Action Map。

## 2. 公开契约

| 类型 | 用途 |
|------|------|
| `IInputProvider` | 业务侧依赖入口，提供 `Snapshot`、`Commands` 和上下文切换 |
| `InputSnapshot` | 每帧连续输入和一帧按钮状态，例如 `Move`、`Look`、`JumpPressed` |
| `InputCommand` / `InputCommandQueue` | 瞬时意图事件队列，例如 Jump、Submit、Pause、Click |
| `InputContext` / `InputContextStack` | Gameplay、UI、Vehicle、PhotoMode、Cutscene、Rebinding、Debug 的上下文栈 |
| `InputService` | 从 `InputActionAsset` 读取输入，生成 `InputSnapshot` 和 `InputCommand` |
| `LocalUserInputAdapter` | 本地多人时接入 Unity `PlayerInput` 的私有 actions 副本 |
| `IInputRebindingService` / `InputRebindingService` | 运行时交互式重绑定、保存、读取、重置 |
| `InputBindingDisplayService` | 读取当前 binding 的显示文案，用于按钮提示 |
| `UIInputBridge` | UI 打开/关闭时 push/pop UI 输入上下文 |
| `FakeInputProvider` / `RecordedInputProvider` | 自动化测试、AI 接管和回放输入源 |

## 3. Snapshot 与 Command

连续值从 `InputSnapshot` 读取：

```csharp
Vector2 move = input.Snapshot.Move;
Vector2 look = input.Snapshot.Look;
bool sprint = input.Snapshot.SprintHeld;
```

瞬时输入从 `InputCommandQueue` 消费：

```csharp
IReadOnlyList<InputCommand> commands = input.Commands.DrainForFrame(frame);
for (int i = 0; i < commands.Count; i++)
{
    if (commands[i].Intent == InputIntent.Jump)
    {
        // 转成游戏自己的 runtime command 或直接触发控制器意图。
    }
}
```

`InputCommandQueue.DrainForFrame(frame, List<InputCommand>)` 可复用外部列表，适合避免额外分配。

## 4. 上下文栈

`SetContext()` 会清空已有上下文并设置单一基础上下文：

```csharp
input.SetContext(InputContext.Gameplay);
```

临时覆盖使用 `PushContext()`：

```csharp
using (input.PushContext(InputContext.UI))
{
    // UI 独占输入，Gameplay map 会被禁用。
}
```

半透明覆盖且保留下层输入时使用 `InputContextPolicy.Overlay`：

```csharp
IDisposable scope = input.PushContext(InputContext.UI, InputContextPolicy.Overlay);
scope.Dispose();
```

## 5. 重绑定

`InputRebindingService` 使用 Unity Input System 的 interactive rebinding。完成和取消都会释放 operation；完成后自动保存 binding overrides 到 `PlayerPrefs`。

```csharp
input.Rebinding.StartRebind("Gameplay/Jump", bindingIndex: 0);
input.Rebinding.Load();
input.Rebinding.ResetToDefault();
```

绑定显示：

```csharp
if (input.BindingDisplay.TryGetDisplayString("Gameplay/Jump", 0, out string label))
{
    // label 可用于 UI 提示。
}
```

## 6. 本地多人

本地多人使用 Unity `PlayerInput` 时，把 `LocalUserInputAdapter` 挂在同一对象上。它读取 `PlayerInput.actions`，不会绕过 Unity 为每个本地用户维护的私有 action 副本和设备过滤。

## 7. 测试入口

- `Assets/Scripts/MxFramework/Tests/Input/InputCoreTests.cs`
- 测试覆盖上下文栈 overlay/exclusive、命令队列排序/迟到拒绝、Fake/Recorded 输入源。
