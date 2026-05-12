# Phase 12 UI Toolkit Showcase E2E Closeout

> **状态**: 已验收（2026-05-09）
> **优先级**: P0
> 所属 Goal: `PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 范围: Phase 12 M3 / M4 / M5

## 目标

对 Phase 12 当前批次做 E2E 收口，确认 Runtime Showcase 已经从手动调试面板推进到可在 Play Mode 中被制作人直接验证的轻量 Showcase Mini Game。

本次收口只验收 M3、M4、M5 的组合状态，不新增 UI 功能，不修改 Preview Runtime 03.5、AIAction 或 Combat。

## 已验收能力

### M3：Config / Patch / Rebuild Panel

验收结论：通过。

- HUD 中存在独立 `config-controls` 区域。
- UI 可显示 Hardcoded / Config Driven 状态。
- `Load Patch`、`Load Mod Package`、`Rebuild Ability`、`Compare Old/New` 入口已存在。
- Patch / Mod Package 进入 Config Driven 后可重建 Ability。
- Old/New 对比证明旧 Ability 对象不被热替换，新 Ability 使用新配置。
- `RuntimeConfigChangeSummary` 可在 UI 中展示 source、policy、changed、rebuilt、failed 摘要。

### M4：Diagnostic View

验收结论：通过。

- HUD 中存在独立 `diagnostic-view` 区域，并位于 M3 Config Panel 之后。
- Diagnostic View 提供 Summary / Technical 双视图。
- Entity、Ability Events、AttributeChanged Events、Config Source、Errors 分区展示。
- Ability Events 与 AttributeChanged Events 保持分区，不混入单一事件日志。
- 无错误时显示稳定空状态 `No runtime errors`。
- 诊断模型由 Demo adapter 映射到通用 UI ViewModel，Runtime DTO 未被 UI 层反向污染。

### M5：Mini Game Feedback

验收结论：通过。

- HUD 中存在独立 `mini-game-feedback` 区域。
- Player / Enemy 能显示 Stable、Burning、Down 等状态反馈。
- Buff 区域能显示空状态、Buff id、层数和剩余时间。
- 技能按钮反馈能区分 ready、refresh Burning、目标不可用等状态。
- 最近动作来自事件日志，不依赖 Console。
- M5 没有替换 M3/M4，Play Mode visual tree 中三者同时存在。

## PlayMode Visual Tree 要求

后续验收 Phase 12 Showcase 时，Play Mode visual tree 至少需要满足：

1. 根 UI 可找到 `mini-game-feedback`。
2. 根 UI 可找到 `manual-controls`。
3. 根 UI 可找到 `config-controls`。
4. 根 UI 可找到 `diagnostic-view`。
5. `mini-game-feedback` 不遮挡或替代 `config-controls`、`diagnostic-view`。
6. `diagnostic-view` 中可找到 Summary / Technical 切换入口。
7. `diagnostic-errors-list` 在无错误时显示 `No runtime errors`。
8. Unity Console 无编译 Error，Play Mode 操作后无 Runtime Error。

推荐布局顺序：

```text
Runtime Showcase HUD
  -> Actor status / snapshot summary
  -> Mini Game Feedback
  -> Manual Controls
  -> Config / Patch / Rebuild Panel
  -> Diagnostic View
  -> Event Log
```

## 自动化验收覆盖

当前测试覆盖足以支撑 M3/M4/M5 收口：

- `RuntimeAbilitySliceConfigPanelTests`
  - Patch / Mod Package 配置层加载。
  - Ability 重建。
  - Old/New 对比与旧对象不热替换语义。
- `RuntimeAbilitySliceDiagnosticViewModelBuilderTests`
  - 空输入稳定状态。
  - Ability Events 与 AttributeChanged Events 分区映射。
  - Config Source、errors、summary / technical 文本映射。
  - Diagnostic View 关键 UXML 元素存在性。
- `RuntimeAbilitySliceMiniGameFeedbackTests`
  - Mini Game Feedback 关键 UXML 元素存在性。
  - 初始状态徽章和 ready 按钮反馈。
  - Burning Buff 层数 / 倒计时 / refresh 反馈。
  - Enemy Down 时目标型按钮降级。

## 依赖边界检查

验收边界：

- `MxFramework.UI.Toolkit` 只能依赖通用 ViewModel 和 UI Toolkit。
- `MxFramework.UI.Toolkit` 不应引用 Demo、Gameplay、Config.Runtime、Combat 或 WGame 业务类型。
- Demo adapter 可以依赖运行时 DTO，并负责把 DTO 映射成通用 UI ViewModel。
- 纯 Runtime Core 不能新增 UI Toolkit 或 UnityEngine 依赖。

本次收口未发现需要修改 Runtime Core 的问题。

## 剩余风险

1. M3/M4/M5 仍然是 Showcase 级 UI，尚未沉淀 M6 的可复用控件和主题 token。
2. Diagnostic View 的事件顺序仍依赖 Runtime DTO 集合顺序，缺少 timestamp / frame / sequence id。
3. AttributeChanged Events 仍缺少 entity id，UI 只能通过 source 或上下文辅助判断归属。
4. Config errors 仍是字符串列表，无法按 severity、code 或 source field 过滤。
5. 当前 Mini Game Feedback 主要是文本、边框和 class 状态，尚未引入图标、纹理或动画资源。
6. Play Mode 视觉验收依赖 `RuntimeVerticalSlice.unity` 场景持续保持 UIDocument 绑定正确。

## 阶段状态建议

Phase 12 当前批次建议标记为：

```text
M3 / M4 / M5: Accepted
Phase 12: Active, ready for M6
```

不建议直接关闭 Phase 12。原因是 M6 `UI Framework Components` 仍未开始，Showcase 里的 UI 能力还没有沉淀为可复用控件体系。

## 下一步

建议下一批进入 M6：

1. 从现有 Showcase 中提取稳定控件候选：`MxStatBar`、`MxCommandButton`、`MxStatusBadge`、`MxEventLog`、`MxPanelTabs`。
2. 先建立主题 token 和 class 命名规则，再迁移现有 UXML / USS。
3. 保持 M3/M4/M5 自动化测试不退化。
4. 增加一个 Play Mode 或 Editor 级 E2E 验收测试，固定 visual tree 的关键 name 顺序和存在性。

## 验收记录

本次收口已完成以下验证：

- `dotnet build WGameFramework.sln --no-restore -v minimal`
  - 通过，0 warning，0 error。
- UI.Toolkit 边界静态检查
  - 未发现 `MxFramework.Demo`、`MxFramework.Gameplay`、`MxFramework.Config.Runtime`、`MxFramework.Combat`、`UnityEditor` 或 `WGame` 引用。
- Unity MCP EditMode tests:
  - `MxFramework.Tests.Ability.RuntimeAbilitySliceConfigPanelTests`
  - `MxFramework.Tests.Ability.RuntimeAbilitySliceDiagnosticViewModelBuilderTests`
  - `MxFramework.Tests.Ability.RuntimeAbilitySliceMiniGameFeedbackTests`
  - 通过，9 / 9 passed。
- Unity MCP Play Mode visual tree:
  - `mini-game-feedback`
  - `manual-controls`
  - `config-controls`
  - `diagnostic-view`
  - 通过，四个关键区域均存在于 `RuntimeVerticalSliceRuntime` 的 UI tree。
  - `diagnostic-error-summary` 与 `diagnostic-errors-list` 显示 `No runtime errors`。
- Unity Console:
  - 通过，最终 0 error，0 warning。
