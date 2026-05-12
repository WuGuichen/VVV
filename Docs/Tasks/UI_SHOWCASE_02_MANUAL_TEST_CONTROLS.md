# UI Showcase 02：Manual Test Controls

> **状态**: Completed
> **优先级**：P0
> 所属 Goal：`PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 目标版本：Phase 12 M2

## 目标

让 Runtime Showcase 从“只看状态”推进到“可手动操作”。制作人进入 Play 后，可以直接通过 UI Toolkit HUD 验证 Ability / Buff / Modifier / Snapshot / Event Log 的闭环。

## 完成结果

- HUD 新增手动控制区：
  - `Strike`
  - `Ignite`
  - `Burning`
  - `Tick 1s`
  - `+ATK`
  - `Reset`
  - `Auto: On / Off`
  - `Mode: Config / Code`
- `MxRuntimeHudController` 新增手动命令接口：
  - `MxRuntimeHudManualCommand`
  - `IMxRuntimeHudManualControlSink`
  - `RegisterManualControlSink`
  - `UnregisterManualControlSink`
  - `SetManualControlState`
- `RuntimeAbilitySliceShowcaseUi` 负责把 UI 命令接到 Demo Runner，不让 `MxFramework.UI.Toolkit` 反向依赖 Demo 程序集。
- `RuntimeAbilitySliceRunner` 新增手动操作 API：
  - `CastStrike`
  - `CastIgnite`
  - `ApplyBurningBuffToEnemy`
  - `TickBuffs`
  - `ApplyAttackModifierToPlayer`
  - `ResetDemo`
  - `SetAutoSequenceEnabled`
  - `SetConfigDrivenMode`
- 手动操作默认暂停自动流程，避免自动序列和手动点击混在一起导致结果难判断。
- 操作结果写入 HUD Event Log，不刷 Console；失败才输出必要 warning。

## 验证结果

- Unity clean compile：通过。
- UXML / USS Resources 加载：通过。
- UXML 必要元素检查：18 个关键元素全部存在。
- Play 模拟按钮命令：
  - `Strike` 改变 Enemy HP。
  - `Burning` 给 Enemy 挂 Buff。
  - `Tick 1s` 推进 Buff tick，并触发 Burning 掉血。
  - `+ATK` 改变 Player Attack。
  - 手动操作会暂停 Auto。
  - `Auto` 可恢复自动流程。
  - `Mode` 可在 Config / Code 之间切换。
  - `Reset` 恢复 Enemy HP。
- Play visual tree：手动控制面板和 7 个按钮存在，HUD 尺寸正常。
- Play Console：0 error / 0 warning。
- `RuntimeVerticalSlice.unity`：`missingComponents=0`，场景内仍不预挂 Runner / HUD / SceneTargetConfig。
- EditMode `MxFramework.Tests`：185 / 185 passed。

## 结果规则

- 交互层必须保持简单：按钮可见、状态可读、结果进入 HUD。
- 细节事件留在 HUD Event Log，不用 Console 刷屏。
- `MxFramework.UI.Toolkit` 只暴露通用 UI 命令，不依赖 Demo 或 Gameplay 业务类型。
