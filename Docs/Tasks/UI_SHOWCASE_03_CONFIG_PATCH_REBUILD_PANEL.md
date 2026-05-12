# UI Showcase 03: Config / Patch / Rebuild Panel

> **状态**: 已完成（2026-05-09）
> **优先级**: P0
> 所属 Goal: `PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 目标版本: Phase 12 M3

## 目标

- 在 Runtime Showcase UI 中加入 Config / Patch / Rebuild 面板。
- 让制作人可以在 Play Mode 中直观看到 Hardcoded / Config Driven 状态。
- 通过 UI 展示 Load Patch、Load Mod Package、Rebuild Ability、Old/New Config 对比入口。
- 展示 `RuntimeConfigChangeSummary`，说明 source、policy、changed、rebuilt、failed 等运行时配置变更结果。
- 验证配置变更后采用 Ability 重建语义，新创建 Ability 使用新配置，旧 Ability 对象不被热替换。

## 范围

- 扩展 `MxRuntimeHudController` 和 `MxRuntimeHudViewModel` 的通用 UI 命令与展示字段。
- 扩展 `RuntimeAbilitySliceShowcaseUi`，把通用 UI 命令转发给 Demo Runner。
- 扩展 `RuntimeAbilitySliceRunner` 的 Demo-only config 操作：
  - 加载演示 Patch。
  - 加载演示 Mod Package 语义的配置层。
  - 重建当前配置驱动 Ability。
  - 对比旧 Ability 对象与新配置重建 Ability 的效果差异。
- 更新 `GameplayShowcase.uxml` / `.uss`，复用 M1/M2 UI 风格。
- 增加必要测试覆盖配置面板相关的重建与旧对象不热替换语义。

## 非目标

- 不做 Phase 12 M4 Diagnostic View。
- 不做 Mini Game Feedback。
- 不引入 WGame 业务数据、真实角色、真实 Buff 表或私有运行时。
- 不让 `MxFramework.UI.Toolkit` 反向依赖 Demo、Gameplay、Config.Runtime 或任何业务类型。
- 不实现完整文件选择器、真实外部 Mod 包浏览器或 Authoring Editor 导出链路。

## 文件边界

允许修改：

- `Docs/Tasks/UI_SHOWCASE_03_CONFIG_PATCH_REBUILD_PANEL.md`
- `Assets/Scripts/MxFramework/UI.Toolkit/**`
- `Assets/Scripts/MxFramework/Demo/Ability/**`
- `Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml`
- `Assets/UI/MxFramework/Showcase/GameplayShowcase.uss`
- `Assets/Scripts/MxFramework/Tests/**` 中本任务必要测试
- `Docs/CAPABILITIES.md`
- `Docs/Tasks/PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
- `Docs/USAGE.md`

需要避免：

- 修改核心 Gameplay / Buff / Modifier 行为。
- 修改 WGame 业务仓库或引入 WGame 数据。
- 大范围重构 M1/M2 已完成的 HUD Shell 与 Manual Controls。

## 验收标准

- Runtime Showcase HUD 出现 Config / Patch / Rebuild 面板。
- UI 明确显示当前 `Hardcoded` 或 `Config Driven` 状态。
- UI 有 `Load Patch`、`Load Mod Package`、`Rebuild Ability`、`Compare Old/New` 入口。
- UI 显示 `RuntimeConfigChangeSummary` 的 source、policy、changed、rebuilt、failed 摘要。
- 点击加载 Patch 或 Mod Package 后，Config Driven 模式能用新配置重建 Ability。
- Old/New 对比能显示旧 Ability 对象仍保持旧配置效果，新 Ability 使用新配置效果，证明不是热替换旧对象。
- `MxFramework.UI.Toolkit` 只依赖通用 view model 和命令，不引用 Demo / Gameplay / Config.Runtime 类型。

## 测试要求

- 增加或更新 EditMode 测试，覆盖 Demo Runner 的 Patch / Rebuild / Compare 流程。
- 能运行 Unity 测试时，至少运行相关 Config / Ability 测试。
- 若本机无法启动 Unity 测试，说明原因，并用可行的静态检查或脚本检查补充验证。

## 完成记录

- 新增 Runtime Showcase Config / Patch / Rebuild 面板，包含 `Load Patch`、`Load Mod Package`、`Rebuild Ability`、`Compare Old/New` 操作入口。
- `MxFramework.UI.Toolkit` 仅扩展通用命令和 view model 字段，未引用 Demo / Gameplay / Config.Runtime 类型。
- `RuntimeAbilitySliceRunner` 增加 Demo-only Patch / Mod Package 配置层加载、Ability 重建、Old/New 对比流程。
- UI 显示 Hardcoded / Config Driven 状态、`RuntimeConfigChangeSummary`、重建结果和旧对象 / 新重建 Ability 效果差异。
- 增加 `RuntimeAbilitySliceConfigPanelTests` 覆盖 Patch / Mod Package 的重建与旧对象不热替换语义。
