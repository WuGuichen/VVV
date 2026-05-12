# UI Showcase 01：Runtime HUD Shell

> **状态**: ✅ 已完成（r1198）
> **优先级**：P0
> 所属 Goal：`PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 目标版本：Phase 12 M1

## 目标

用 UI Toolkit 建立第一版 Runtime Showcase HUD，让 `RuntimeVerticalSlice.unity` 在 Play 模式下显示一个清晰、有趣、可扩展的运行时面板。

首版只做展示，不做复杂交互。交互按钮放到 `UI_SHOWCASE_02_MANUAL_TEST_CONTROLS.md`。

## 范围

### 必须完成

1. 新增 `MxFramework.UI.Toolkit` 运行时程序集。
2. 新增 UI Toolkit HUD 控制器和 ViewModel。
3. 新增 `GameplayShowcase.uxml` 和 `GameplayShowcase.uss`。
4. Ability Slice 自动挂载 Showcase UI 适配器。
5. UI 展示：
   - Player / Enemy HP、Attack、Defense、Alive。
   - Buff 摘要。
   - Ability source。
   - Config change summary。
   - Snapshot summary。
   - Event log。
6. 保留现有 OnGUI 后备显示。
7. 更新 `Docs/USAGE.md`、`Docs/CAPABILITIES.md`、`Docs/Tasks/PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`。

### 不做

- 不做技能按钮。
- 不做配置切换按钮。
- 不做复杂动画。
- 不新建完整游戏场景。
- 不做 UI 编辑器。

## 验收标准

1. Play 模式能看到 UI Toolkit HUD。
2. HUD 不依赖 Console 即可显示当前 runtime state。
3. `MxFramework.Gameplay`、`Config.Runtime` 等纯 C# 模块不新增 UnityEngine / UI Toolkit 依赖。
4. Unity EditMode 编译和相关测试通过。
5. GitNexus 检查 low risk 或影响面合理。
6. SVN 提交信息建议：

```text
Add UI Toolkit runtime showcase shell
```
