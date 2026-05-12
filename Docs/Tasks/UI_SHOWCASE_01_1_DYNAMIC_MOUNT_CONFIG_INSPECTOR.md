# UI Showcase 01.1：Dynamic Mount + Config Inspector

> **状态**: ✅ 已完成（r1199）
> **优先级**：P0
> 所属 Goal：`PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 前置任务：`UI_SHOWCASE_01_RUNTIME_HUD_SHELL.md`

## 目标

修正首版 HUD Shell 的装配方式：场景中只保留一个统一入口组件，由它在 Play 模式按配置动态挂载具体 Runner、HUD 和 UI 适配器。同时提供统一 Inspector 配置界面，给制作人清楚的配置项提示。

## 完成结果

- `RuntimeVerticalSliceRunner` 作为唯一场景入口。
- Ability Slice、`MxRuntimeHudController`、`RuntimeAbilitySliceShowcaseUi` 改为由 `RuntimeVerticalSliceRunner` 统一动态挂载。
- `RuntimeAbilitySliceRunner` 不再自行挂载 HUD，避免子 Runner 私自扩展场景组件栈。
- 新增 `RuntimeVerticalSliceRunnerEditor`，按 Showcase 入口、非 Ability 模式、Patch / Mod 路径、初始数值、诊断输出分组显示。
- Inspector 配置项带中文说明、tooltip、HelpBox 和风险提示。
- Ability + UI Toolkit HUD 开启时自动隐藏 legacy OnGUI；旧非 Ability 模式仍可用 legacy OnGUI 作为后备显示。

## 后续

- M2 Manual Test Controls 应继续沿用统一入口动态挂载。
- 后续旧 Runtime Slice 模式迁移到 UI Toolkit 后，再逐步关闭 legacy OnGUI 默认显示。
