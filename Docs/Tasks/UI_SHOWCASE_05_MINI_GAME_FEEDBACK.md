# UI Showcase 05: Mini Game Feedback

> **状态**: 已完成（2026-05-09）
> **优先级**: P1
> 所属 Goal: `PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 目标版本: Phase 12 M5
> 前置任务: Phase 12 M3 `Config / Patch / Rebuild Panel`、Phase 12 M4 `Diagnostic View`

## 目标

- 让 Runtime Showcase 从工程调试窗口进一步接近轻量可玩样板。
- 在不改变 Runtime 语义的前提下，增加 HP 状态徽章、Buff 倒计时/层数、技能按钮反馈和最近动作反馈。
- 保持框架展示优先，继续让制作人能在 Play Mode 直观看到 Ability / Buff / Modifier / Config / Diagnostic 状态。

## 范围

- 扩展 `MxRuntimeHudViewModel` 的通用 Mini Game Feedback 显示数据。
- 扩展 `MxRuntimeHudController`，绑定状态徽章、Buff 反馈、最近动作和按钮反馈样式。
- 扩展 `RuntimeAbilitySliceShowcaseUi`，由 Demo adapter 把 `RuntimeAbilitySliceRunner` 的实体、Buff 和事件日志映射成 UI Toolkit 通用 ViewModel。
- 扩展 `GameplayShowcase.uxml` / `.uss`，新增轻量 feedback 区块并保持 M3 Config Panel、M4 Diagnostic View 原位可见。
- 增加 EditMode 测试覆盖关键 UXML 元素、状态徽章和技能按钮反馈逻辑。
- 同步 Phase 12 Goal、能力清单和使用手册。

## 非目标

- 不修改 Combat。
- 不修改 Phase 9 / AIAction 试点文档。
- 不引入 WGame 业务数据、真实角色、真实 Buff 表或私有运行时。
- 不做 M6 可复用控件抽象，例如 `MxStatusBadge` 或 `MxCommandButton`。
- 不做 Runtime Preview 03.5。
- 不引入复杂动画系统、美术资源、第三方 UI 框架或新的运行时语义。

## 文件边界

允许修改：

- `Docs/Tasks/UI_SHOWCASE_05_MINI_GAME_FEEDBACK.md`
- `Assets/Scripts/MxFramework/UI.Toolkit/**`
- `Assets/Scripts/MxFramework/Demo/Ability/**`
- `Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml`
- `Assets/UI/MxFramework/Showcase/GameplayShowcase.uss`
- `Assets/Scripts/MxFramework/Tests/Ability/**` 或 UI / Showcase 相关测试
- `Docs/Tasks/PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`

需要避免：

- 修改 `Assets/Scripts/MxFramework/Combat/**`。
- 修改纯 Runtime Core 依赖方向，尤其不要让 `Gameplay`、`Buffs`、`Modifiers`、`Config.Runtime` 反向依赖 UI Toolkit。
- 覆盖 M3 Config / Patch / Rebuild Panel 或 M4 Diagnostic View。
- 改动非本任务相关文档或资源。

## 验收标准

- Runtime Showcase HUD 出现独立的 `Mini Game Feedback` 区域。
- Player / Enemy 能显示 HP 状态徽章，例如 Stable、Wounded、Critical、Down。
- Buff 状态能显示无 Buff 空状态，或显示 Buff id、层数和剩余时间。
- 技能按钮有可读反馈，能区分可用、目标不可用、刷新 Burning 或叠层已满等状态。
- 最近动作反馈来自事件日志，不依赖 Console。
- M3 Config / Patch / Rebuild Panel 和 M4 Diagnostic View 仍保留且不被 M5 替换。
- `MxFramework.UI.Toolkit` 只依赖通用 ViewModel 和 UI Toolkit，不引用 Demo / Gameplay / Config.Runtime 业务类型。

## 测试要求

- EditMode 测试覆盖 `GameplayShowcase.uxml` 的 M5 关键元素。
- ViewModel / Demo adapter 测试覆盖初始状态徽章、Buff 倒计时/层数文本、按钮反馈可用状态。
- 测试覆盖 Enemy Down 时按钮反馈降级为目标不可用。
- 完成后运行 `dotnet build`。
- 能连接 Unity MCP 时运行相关 EditMode 测试；如能进入 Play 模式，补充 visual tree 验收。

## 完成记录

- 新增 Mini Game Feedback 区块，显示 Player / Enemy 状态徽章、Buff 反馈、技能按钮反馈和最近动作。
- 扩展通用 HUD ViewModel 和 Controller 绑定，不让 UI Toolkit 引用 Demo 或 Runtime 业务类型。
- Demo adapter 负责从 `RuntimeAbilitySliceRunner` / `RuntimeEntity` / Buff snapshot / 事件日志映射反馈文本与按钮高亮。
- USS 使用文本、边框和简单 class 状态表现，不新增复杂美术资源或动画系统。
- 增加 `RuntimeAbilitySliceMiniGameFeedbackTests` 覆盖 UXML、状态徽章、Buff 文本和按钮反馈逻辑。
