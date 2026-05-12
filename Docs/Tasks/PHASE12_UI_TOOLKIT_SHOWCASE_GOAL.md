# Phase 12 Goal：UI Toolkit Runtime Showcase Framework

> **状态**: Active（M1-M6 accepted）
> **优先级**：P0
> 起点版本：r1197
> 目标：把框架能力包装成可运行、可操作、可感知的 Showcase Mini Game

## Goal

建立基于 Unity UI Toolkit 的运行时 UI 框架基础，让 WGameFramework 的核心能力不只存在于 API、测试和 Console 中，而是能在 Play 模式下被制作人直接体验。

Phase 12 的核心目标：

```text
Framework Runtime APIs
  -> Showcase ViewModel
  -> UI Toolkit UXML / USS
  -> Play Mode Manual Test Harness
  -> Mini Game Feedback
```

完成后，每个新增框架能力都必须回答两个问题：

- 程序上是否有稳定 API 和自动化测试。
- 制作人是否能在 Showcase Mini Game 中直观看到、点击和验证。

## 边界

### 做

- 使用 UI Toolkit 作为正式运行时 UI 基础。
- UI 结构使用 UXML，视觉使用 USS，C# 只负责绑定、状态刷新和交互命令。
- 保留纯 C# Runtime Core 的干净边界。
- 先从 `RuntimeVerticalSlice.unity` 接入，不急于新建复杂场景。
- UI 文案中文优先，技术 key 保留英文副标。
- 后续需要的图标、纹理和小型表现资源可生成后纳入 `Assets/Art/MxFramework/Showcase/`。

### 不做

- 不引入 WGame 业务 UI。
- 不绑定 FairyGUI、UGUI 或第三方 UI 框架。
- 不把 UI Toolkit 类型泄漏进 `Gameplay`、`Config.Runtime`、`Buffs`、`Modifiers` 等纯逻辑模块。
- 不在首版做完整 UI 编辑器、背包、装备、关卡或复杂动画系统。

## 模块规划

```text
Assets/Scripts/MxFramework/UI.Toolkit/
  Runtime/
    MxRuntimeHudController.cs
    MxRuntimeHudViewModel.cs

Assets/Scripts/MxFramework/Demo/Ability/
  RuntimeAbilitySliceShowcaseUi.cs

Assets/UI/MxFramework/Showcase/
  GameplayShowcase.uxml
  GameplayShowcase.uss

Assets/Art/MxFramework/Showcase/
  generated runtime showcase art resources
```

## Milestones

### M1：Showcase UI Shell

目标：

- 建立 UI Toolkit 运行时 HUD Shell。
- 展示 Player / Enemy 状态卡、Ability source、Config summary、Snapshot summary、事件日志。
- 保留 OnGUI 作为临时后备显示。

验收：

- Play 模式可看到 UI Toolkit 面板。
- 不依赖 Console 即可理解当前运行状态。
- 不修改纯 C# Runtime Core 依赖方向。

### M2：Manual Test Controls

目标：

- 增加按钮：Cast Strike、Cast Ignite、Apply Buff、Apply Modifier、Tick、Reset。
- 每次操作写入事件日志。
- UI 状态实时刷新。

验收：

- 制作人可以手动验证 Ability / Buff / Modifier 闭环。

结果记录：

- `UI_SHOWCASE_02_MANUAL_TEST_CONTROLS.md`

### M3：Config / Patch / Rebuild Panel

目标：

- UI 接入 Hardcoded / Config Driven 状态。
- 展示 Load Patch、Load Mod Package、Rebuild Ability、Old/New Config 对比入口。
- 展示 `RuntimeConfigChangeSummary`。

验收：

- 可以在 UI 中直观看到配置变更后重建 Ability、不热替换旧对象。

结果记录：

- `UI_SHOWCASE_03_CONFIG_PATCH_REBUILD_PANEL.md`

### M4：Diagnostic View

目标：

- Snapshot 面板提供 summary 和 technical 两种视图。
- Ability Events、AttributeChanged Events、Config Source 和错误信息分区显示。

验收：

- 不打开测试代码也能判断运行时状态是否正确。

结果记录：

- `UI_SHOWCASE_04_DIAGNOSTIC_VIEW.md`

### M5：Mini Game Feedback

目标：

- 加入轻量图标、状态徽章、HP 条、Buff 倒计时、技能按钮反馈。
- 保持框架展示优先，不引入复杂战斗表现系统。

验收：

- Showcase 开始像一个小型可玩样板，而不是工程调试窗口。

结果记录：

- `UI_SHOWCASE_05_MINI_GAME_FEEDBACK.md`

### M3-M5：E2E Closeout

目标：

- 对 M3 Config / Patch / Rebuild Panel、M4 Diagnostic View、M5 Mini Game Feedback 做组合验收。
- 确认 Play Mode visual tree 中 M3/M4/M5 同屏存在，且没有互相替代。
- 汇总剩余风险和下一步 M6 规划。

验收：

- M3/M4/M5 当前批次建议标记为 Accepted。
- Phase 12 保持 Active，下一步进入 M6 UI Framework Components。

结果记录：

- `PHASE12_UI_TOOLKIT_SHOWCASE_CLOSEOUT.md`

### M6：UI Framework Components

目标：

- 从 Showcase 中沉淀可复用控件和主题 token。
- 候选控件：`MxStatBar`、`MxEventLog`、`MxCommandButton`、`MxStatusBadge`、`MxPanelTabs`、`MxTooltip`。

验收：

- 后续 Config、Mod、AI、Preview 都能复用同一套 UI 风格和绑定方式。

结果记录：

- `PHASE12_UI_TOOLKIT_M6_COMPONENTS.md`

## 执行规则

- 后续所有运行时框架功能都必须考虑 Showcase 入口。
- UI 层可以依赖 UnityEngine / UI Toolkit；纯 Runtime Core 不能反向依赖 UI。
- 新增 UXML / USS / 美术资源必须随 `.meta` 一起提交。
- 每个阶段至少跑 Unity 编译检查；涉及 Gameplay 行为时继续跑对应 EditMode 测试。
