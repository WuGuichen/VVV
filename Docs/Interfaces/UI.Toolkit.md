# UI Toolkit 接口

> 版本 0.1.0 | 2026-05-18

本文定义 `MxFramework.UI.Toolkit` 的通用运行时 UI Toolkit 控件契约。该模块可以引用 Unity UI Toolkit，但不得引用 Gameplay、Combat 或具体游戏业务类型。

## 模块边界

- 代码入口：`Assets/Scripts/MxFramework/UI.Toolkit/Runtime/`
- Assembly：`MxFramework.UI.Toolkit`
- 测试入口：`Assets/Scripts/MxFramework/Tests/UI.Toolkit/`
- 依赖方向：UI Toolkit 控件只接收通用数值、字符串、回调和 `MxUiTone`；领域状态到 UI tone 的映射由调用方完成。

禁止事项：

- 控件不得引用 `MxFramework.Gameplay`、`MxFramework.Combat` 或 Combat / GameplayBridge 类型。
- 控件不得内置 GuardPressure、ArmorIntegrity、PressureBand 或 Runtime AI Planner 规则。
- Runtime 控件不得引用 `UnityEditor`。

## 主题 Token

`MxUiThemeTokens` 集中维护 USS class token。当前通用控件使用：

- `MxStatusBadge`：`mx-status-badge` 和 `mx-status-badge--neutral/positive/warning/danger`
- `MxCommandButton`：`mx-command-button`、enabled、hot、muted 状态 class
- `MxStatBar`：`mx-stat-bar`、`mx-stat-bar__fill`
- `MxStressBar`：`mx-stress-bar`、`mx-stress-bar__fill`、`mx-stress-bar__break-line`、active / inactive 状态 class
- `MxEventLog`、`MxPanelTabs`：事件列表和 tab class

`MxUiThemeTokens.SetStatusTone(VisualElement, MxUiTone)` 保证 neutral / positive / warning / danger tone class 互斥。

## MxStressBar

`MxStressBar` 是通用压力 / 韧性 / 张力类条形控件。它只表达当前值、最大值、断点线、tone 和激活状态，不解释这些数值的游戏含义。

公开方法：

```csharp
var bar = new MxStressBar();
bar.SetValue(current: 35, max: 100);
bar.SetBreakLine(breakLine: 70, max: 100);
bar.SetTone(MxUiTone.Warning);
bar.SetActive(active: true);
```

行为约定：

- `SetValue(int current, int max)`：`max <= 0` 时归零；`current` clamp 到 `[0, max]`；填充宽度使用百分比。
- `SetBreakLine(int breakLine, int max)`：`max <= 0` 时断点归零并隐藏 break line；否则 clamp 到 `[0, max]` 并设置百分比位置。
- `SetTone(MxUiTone tone)`：只更新填充元素的 tone class，并保持 tone class 互斥。
- `SetActive(bool active)`：在根元素上切换 active / inactive class。

领域层接入示例：

```csharp
MxUiTone tone = pressureRatio >= 0.8f ? MxUiTone.Danger : MxUiTone.Neutral;
stressBar.SetValue(pressure, pressureMax);
stressBar.SetBreakLine(breakThreshold, pressureMax);
stressBar.SetTone(tone);
stressBar.SetActive(isVisible);
```

上例中的 `pressureRatio`、`breakThreshold` 和 tone 映射必须来自调用方，不能下沉到 `MxStressBar`。
