# Input 接口

> Status: Current

> 依赖：Unity Input System | 代码：`Packages/com.vincent.wgameframework/Input/`

Input 模块把 Unity Input System 作为底层采集层，只向业务暴露意图快照、瞬时命令和上下文控制。业务脚本不应直接读取 `Keyboard.current`、`Gamepad.current` 或散落持有 `InputAction`。

## 1. 模块边界

- `MxFramework.Input` 是 Unity 依赖程序集，引用 `UnityEngine` 和 `Unity.InputSystem`。
- 模块不依赖 WGame、Entitas、Luban、Gameplay、Runtime 或 Demo。
- noEngine 模块不得反向引用 Input；需要输入时由游戏组合根把 `IInputProvider` 传给上层控制器。
- 默认配置资产位于 `Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions`，第一版包含 `Gameplay`、`UI` 和 `Debug` 三个 Action Map。
- 默认按键唯一代码源是 `DefaultInputBindingCatalog`；`DefaultInputActionAssetFactory` 和默认 `.inputactions` 资源必须与它保持一致。Unity 菜单 `MxFramework/Input/Sync Default Input Actions` 用 catalog 重新生成默认资源，`Validate Default Input Actions` 用于提交前校验。
- `MxFramework.DebugUI.Input` 复用 `Debug` Action Map 和 `InputIntent.ToggleHud` / `ToggleConsole` / `DebugCycle` / `DebugStep`；通用运行时调试 overlay 不直接读取设备 API，输入路由放在可选 adapter 或项目组合根。

默认输入布局：

| Map | Action | 默认 binding |
| --- | --- | --- |
| Gameplay | Move / Jump / Interact / Dodge / Sprint | WASD 或 Arrow / Space / E / LeftShift / LeftCtrl |
| Gameplay | AttackPrimary / AttackSecondary / Pause | Mouse Left / Mouse Right / Escape |
| Gameplay | TargetLock | L / Mouse Middle / Gamepad Right Stick Press |
| Gameplay | CameraModeNext | V |
| Gameplay | GuidancePrevious / GuidanceNext | [ / ] |
| UI | Navigate / Submit / Cancel | WASD 或 Arrow / Enter / Escape |
| Debug | ToggleConsole / ToggleHud / Cycle / Step / Restart | Backquote / F1 / F2 / F3 / F4 |
| Debug | DebugPrimary / DebugSecondary / AudioPrimary / AudioSecondary | F5 / F6 / F7 / F8 |

`P`、`F`、`H` 和数字键不再作为默认 Debug/Gameplay 快捷键；数字键预留给后续正式技能栏或项目级布局。CharacterTest 在 Unity Editor Play 中会对 runtime action clone 做临时 `P` 菜单适配，Player 包和默认 `.inputactions` 仍以 `Escape` 作为 `Gameplay/Pause`。

CharacterTest 使用 `Gameplay/GuidancePrevious` 与 `Gameplay/GuidanceNext` 在正式 Guidance Card 可见且没有大型玩家面板接管屏幕时切换当前测试指引。该输入只改变 presentation 选择，不修改玩法权威状态，也不会把未完成步骤标记为完成。

## 2. 公开契约

| 类型 | 用途 |
|------|------|
| `IInputProvider` | 业务侧依赖入口，提供 `Snapshot`、`Commands`、当前 context、启用 context 查询、context 存在查询和上下文切换 |
| `InputSnapshot` | 每帧连续输入和一帧按钮状态，例如 `Move`、`Look`、`JumpPressed` |
| `InputCommand` / `InputCommandQueue` | 瞬时意图事件队列，例如 Jump、Submit、Pause、Click；支持 destructive drain 和 non-destructive peek |
| `InputContext` / `InputContextStack` | Gameplay、UI、Vehicle、PhotoMode、Cutscene、Rebinding、Debug 的上下文栈 |
| `InputService` | 从 `InputActionAsset` 读取输入，生成 `InputSnapshot` 和 `InputCommand` |
| `LocalUserInputAdapter` | 本地多人时接入 Unity `PlayerInput` 的私有 actions 副本 |
| `IInputRebindingService` / `InputRebindingService` | 运行时交互式重绑定、保存、读取、重置 |
| `InputBindingDisplayService` | 按语义 Action ID 与显式 binding index 读取显示文案；不推断最近活动设备 |
| `UIInputBridge` | UI 打开/关闭时 push/pop UI 输入上下文 |
| `IMxCursorController` / `MxCursorController` | Unity cursor scope stack；gameplay、UI、Debug 只提交 `MxCursorRequest`，由 `UnityCursorDriver` 统一写 `UnityEngine.Cursor` |
| `DebugUiInputAdapter` / `DebugUiOverlayInputBridge` | 可选 Debug UI 输入桥，消费 `IInputProvider` 并 push `InputContext.Debug` overlay scope |
| `FakeInputProvider` / `RecordedInputProvider` | 自动化测试、AI 接管和回放输入源 |

`InputService.TryChangeBindingPath(...)` 只用于运行时 action clone 的环境适配，不创建 `SaveBindingOverridesAsJson()` 会保存的 override；玩家可配置按键仍通过 `IInputRebindingService`。

玩家可见的按键提示不得把 `L`、鼠标中键、手柄按钮等物理控件名称写死在业务 UI 文案中。
提示应使用语义 Action ID（例如 `Gameplay/TargetLock`）并显式选择 binding index，再通过
`InputBindingDisplayService` 解析显示名称；无法解析时使用不声称具体物理按键的保守文案。
`InputBindingDisplayService` 不根据最近活动设备自动选择 binding，调用方或更上层的输入显示
策略负责决定 index。长期显示的提示应缓存解析结果，并仅在 binding path / override 变化所引起的
`InputBindingDisplayService.Revision` 变化时重新解析，不能在每帧调用显示字符串 API；设备活动本身
不会推进该 revision。

当前 `CharacterTestFairyGuiGuidanceHost` 为正式 Guidance plan 中每一步的 `inputActionId` 显式请求
binding index `0`，例如 `Gameplay/Move`、`Gameplay/Jump`、`Gameplay/CameraModeNext`、
`Gameplay/TargetLock` 与 `Gameplay/AttackPrimary`。以 `Gameplay/TargetLock` 为例，默认 index `0`
是键盘 `L`；鼠标中键和手柄右摇杆按下仍可发出同一语义动作，但当前 Guidance 提示不会仅因
最近输入设备改变而自动切换到它们的显示文案。binding revision 变化只重新投影当前步骤，不
重置 completion observer 或推进教学状态。

## 3. Snapshot 与 Command

连续值从 `InputSnapshot` 读取：

```csharp
Vector2 move = input.Snapshot.Move;
Vector2 look = input.Snapshot.Look;
bool sprint = input.Snapshot.SprintHeld;
```

瞬时输入从 `InputCommandQueue` 消费：

`InputCommand.TraceId` 为 `InputTraceIdentity`，保存预定义的整数 `KindId`、整数 `SourceId` 以及 64 位 `ExecutionId` / `Sequence`。默认值表示没有追踪身份。Input 不依赖 Runtime 程序集；`CharacterControl.Input` 在边界直接复制四个数值为 `RuntimeTraceIdentity`，不解析或哈希显示字符串。`WithSequence` 保留追踪身份，仅更新命令队列序号。

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

`InputCommandQueue.DrainForFrame(frame, List<InputCommand>)` 可复用外部列表，适合避免额外分配。它会移除 `command.Frame <= frame` 的命令并推进 queue `CurrentFrame`，因此只适合权威消费者。

`InputCommandQueue.Generation` 在 `Reset()` 时递增。非破坏性旁路消费者应同时记录 generation 和 command sequence，避免输入服务重新启用后把重置为 0 的 sequence 误判为旧命令。

只观察或旁路消费时使用 `PeekForFrame()`，可按 predicate 过滤，不移除命令也不推进 `CurrentFrame`：

```csharp
var debugCommands = new List<InputCommand>();
input.Commands.PeekForFrame(
    frame,
    debugCommands,
    command => command.Intent == InputIntent.ToggleHud);
```

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

读取输入权限时应使用 `IsContextEnabled(InputContext.Gameplay)` 判断某个 context 是否仍在启用集合中，而不是只看 `CurrentContext`。例如 Debug UI 作为 overlay 打开时，`CurrentContext` 可以是 `Debug`，但 Gameplay map 仍然启用；此时 Character Control / Combat Showcase 这类 gameplay 组合根仍应能读取移动和动作命令。需要独占阻断 Gameplay 时，使用默认 `InputContextPolicy.Exclusive`。

`ContainsContext(context)` 只表示某个 context 是否存在于 stack 中，不表示它当前可用。Gameplay 组合根如果需要自动恢复 `Gameplay` scope，必须先检查 `ContainsContext(InputContext.UI)` / `Rebinding` / `Cutscene` 等屏蔽状态，避免在玩家 modal 打开期间把 Gameplay 或 Debug 输入重新压到 UI 之上。

## 4.1 Cursor scope

`MxCursorController` 管理 Unity cursor 的低频 ownership scope。业务、UI 和 Debug 面板不直接写 `Cursor.visible` / `Cursor.lockState`，只 push `MxCursorRequest`；可见 cursor 请求（`Released` / `Confined`）优先于 `Captured`，同类可见请求之间仍以后进且存活的 request 作为 effective state。无 request 时默认 `Released`：

```csharp
var cursor = new MxCursorController();
IDisposable gameplay = cursor.Push(MxCursorRequest.Captured("Game.Camera"));
IDisposable modal = cursor.Push(MxCursorRequest.Released("Game.UI.Modal"));

// modal 打开时 effective state 是 Released。
modal.Dispose();
// 回到 Game.Camera 的 Captured。
gameplay.Dispose();
```

默认映射：

| Mode | Unity visible | Unity lockState |
|------|---------------|-----------------|
| `Released` | `true` | `CursorLockMode.None` |
| `Captured` | `false` | `CursorLockMode.Locked` |
| `Confined` | `true` | `CursorLockMode.Confined` |

`UnityCursorDriver` 是默认唯一写入 `UnityEngine.Cursor` 的实现点；测试可注入 `IMxCursorDriver`，避免污染 Unity 全局 cursor 状态。`IsCaptured` 同时要求 effective request 为 `Captured` 且底层实际 `lockState` 为 `Locked`，避免 Editor 或操作系统解除锁定后仍把边界受限的鼠标位移当成捕获输入。Unity Editor、操作系统或焦点切换改变 cursor 后，宿主应在重新获得焦点等低频时机调用 `Refresh()` 重应用当前 effective request；不得在每帧强制重锁而破坏用户主动释放 cursor 的能力。锁定请求失败时，默认 driver 保持 cursor 可见，不留下“隐藏但未锁定”的半状态。

## 4.2 CharacterTest player UI surface scope

CharacterTest player-facing FairyGUI surfaces use the input and cursor primitives above rather than writing `UnityEngine.Cursor` or action-map state directly. Large player surfaces such as MainMenu, NetworkPanel, SettingsPanel, AbilityBindingPanel, Runtime AI Planner loadout, ArenaSetupPanel, and ArenaBehaviorGroupPanel push exclusive `InputContext.UI` and a released cursor scope for the duration of the surface session. Closing the final player modal disposes that UI scope so camera/gameplay capture can become effective again according to the existing cursor priority rules.

Combat HUD and floating combat text are presentation surfaces and must not push UI context or cursor scopes. `StoryDialog` is a narrative modal: it can release cursor and own UI input while visible, but CharacterTest large player surfaces suppress it instead of closing it as a conflict target. Narrative progress still requires an explicit Continue or choice command; generic Cancel cannot skip it.

`CharacterTestUiRuntime.ProcessInput()` runs before simulation drains `InputCommandQueue`. It non-destructively peeks only unprocessed `Submit` / `Cancel` commands, routes them to the top FairyGUI modal, and converts `InputSnapshot.Navigate` neutral-to-direction edges into previous/next focus moves. The bridge reuses its command buffer and cached predicate after configuration. Cancel/back handling stays top-first, while lower-layer FairyGUI commands remain blocked by `MxFairyGuiModalCommandGate` whenever a modal is active.

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

## 7. Debug UI 输入桥

Debug UI 输入桥位于独立程序集 `MxFramework.DebugUI.Input`。它只通过 `PeekForFrame()` 非破坏性读取 `IInputProvider.Commands` 中的 Debug intents，不直接读取 `Keyboard.current`、`Gamepad.current`、`Mouse.current` 或散落持有 `InputAction`。

```csharp
var adapter = new DebugUiInputAdapter(input);
adapter.SetEnabled(true); // Pushes InputContext.Debug as an overlay scope.
adapter.ProcessFrame(frame, debugUiTarget);
```

Unity 场景中可使用 `DebugUiOverlayInputBridge` 把同一 GameObject 上的 `InputService` 和 `DebugUiOverlayController` 连接起来。该桥使用 `InputService.LastCommandFrame` 对齐最近一次输入采集帧，只负责 visibility / refresh，不执行 Debug UI command。

## 8. 测试入口

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Input/InputCoreTests.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/DebugUI/DebugUiInputAdapterTests.cs`
- 测试覆盖上下文栈 overlay/exclusive、命令队列排序/迟到拒绝、Fake/Recorded 输入源。
