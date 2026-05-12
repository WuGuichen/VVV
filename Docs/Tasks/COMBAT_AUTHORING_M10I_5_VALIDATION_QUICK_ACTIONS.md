# Combat Authoring M10I.5：Validation Quick Actions

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10I_4_SPLIT_TIMELINE_WINDOW.md`
> 设计依据：`COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md` 的 Validation / Quick Action 设计、No-Typing Authoring UX 原则
> 派发对象：Editor / Authoring 子代理

## 背景

当前 validation report 已经能指出问题，并且 `CombatAuthoringIssue` 已经包含 `QuickAction`。例如 `Startup 0-24` 超出 `Total Frames` 时会报告：

```text
Message: Frame range must be inside totalFrames.
Fix: Clamp Frame Range.
QuickAction: ClampFrameRange
```

但 Editor 窗口里这些 quick action 还只是文本，用户需要手动找到字段再修。制作侧已经明确需要“简单易用且容错和提示都保障好”，所以 validation 不能只报错，也要提供可点击、可撤销的一键修复。

## 目标

让 Combat Authoring 的 validation issue list 从“报告问题”推进到“可直接修复 / 定位问题”。用户看到 issue 后，应能点击按钮完成常见修复，或者跳转到对应 Action phase / Shape / Binding 字段。

## 范围

本阶段做 Quick Actions v0：

- 在 validation issue row 中显示中文 quick action 按钮。
- 支持至少以下 action：
  - `ClampFrameRange`：把目标帧范围 clamp 到 `[0, TotalFrames - 1]`，并修正 `start > end`。
  - `FitTotalFrames`：当帧范围超出 `Total Frames` 时，将 `Total Frames` 扩到能包含当前最大结束帧。
  - `SelectIssueTarget`：选中 issue 对应的 Action phase / Hitbox / Hurtbox / WeaponTrace row，并同步 Timeline / Inspector。
  - `SelectAsset`：定位当前 Action / Binding asset。
  - `CreatePreviewMarker` v0：如果 issue 是 marker 缺失，优先显示明确按钮；可以先跳到对应 shape 并提示用户使用 `使用当前选择重连`，如实现成本可控再直接创建 / 重连 marker。
- Quick action 成功后刷新：
  - validation report。
  - Inspector detail。
  - Timeline strip / row list。
  - Scene View gizmo / overlay。
- 所有会修改 asset 的 quick action 必须支持 Undo / dirty。
- Quick action 失败时要给中文提示，不静默失败。

## 交互要求

- Issue row 至少显示：
  - 严重度。
  - 来源 section / field。
  - message。
  - suggested fix。
  - 主按钮，例如 `修正帧范围` / `适配总帧数` / `定位目标`。
- 对帧范围越界问题，优先提供两个按钮：
  - `适配总帧数`：扩 Total Frames。
  - `修正帧范围`：clamp 当前 issue 的 range。
- 对 marker 缺失问题，按钮文案应明确，例如：
  - `定位目标`
  - `使用当前选择重连`
- 按钮 tooltip 要说明会修改什么，以及可用 Undo 恢复。
- 不能让用户误以为 quick action 会改 Runtime；只改 Authoring Asset / Binding Asset。

## 技术建议

- 主要修改：
  - `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`
  - 如需补充 action kind，可修改 `Assets/Scripts/MxFramework/Combat.Authoring/CombatAuthoringReport.cs`
- 现有 `CombatAuthoringIssue` 已包含：
  - `Section`
  - `TrackId`
  - `FrameRange`
  - `Field`
  - `QuickAction`
  - `SourceOrder`
- 可以新增私有方法：

```text
CreateIssueQuickActions(...)
ExecuteIssueQuickAction(...)
SelectIssueTarget(...)
ClampIssueFrameRange(...)
FitTotalFramesForIssue(...)
TryFindIssueTimelineRow(...)
```

- `ClampFrameRange` 的目标定位建议：
  - `Section == Startup / Active / Recovery` 或 `Field == startup / active / recovery`：修改 Action phase range。
  - `Section == Hitbox / Hurtbox`：按 `TrackId` 找对应 shape 的 `frameRange`。
  - `Section == WeaponTrace`：按 `TrackId` 找对应 trace 的 `frameRange`。
- `FitTotalFrames` 可以复用当前窗口内已实现的 required total frames 计算逻辑，或抽成 helper。
- 修改后调用现有跨窗口刷新链路：`CombatAuthoringSceneState.NotifyDataChanged()`。
- 不要把 issue row 里的按钮绑定到过期 index；callback 应捕获 issue 值或重新按当前 report 查找。

## 非目标

- 不做复杂 quick action command framework。
- 不做批量修复所有 issue。
- 不做外部 CLI quick action。
- 不做 scene transform 的静默创建 / 修改。
- 不改 Runtime Combat 逻辑。
- 不改 JSON export key。

## 验收标准

- 制造 `Total Frames = 1`、`Startup = 0-24`。
- Validation issue row 显示错误和 quick action 按钮。
- 点击 `适配总帧数` 后：
  - `Total Frames` 变成至少 25。
  - issue 消失或 validation 变为通过。
  - Timeline 扩展到 0-24。
  - Undo 可恢复。
- 再制造越界 shape frame range。
- 点击 `修正帧范围` 后：
  - 对应 shape 的 `start/end` 被 clamp 到合法范围。
  - Inspector / Timeline 同步刷新。
  - Undo 可恢复。
- 点击 `定位目标` 后：
  - Timeline / Inspector 选中对应 row。
  - Scene View gizmo selection 同步。
- Unity Console 无 error。
- Authoring EditMode tests 通过。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- 相关 Authoring EditMode tests。
- 手动打开 `Open Authoring Layout`，制造 `Total Frames = 1 / Startup 0-24`，验证 `适配总帧数`。
- 手动制造一个 Shape frame 越界，验证 `修正帧范围` 和 Undo。
- 验证 `定位目标` 会同步两个窗口。

## 完成记录

- 已在 validation issue row 中加入中文 quick action 按钮：`适配总帧数`、`修正帧范围`、`定位目标`、`定位 Asset`、`使用当前选择重连` / `处理提示`。
- 帧范围越界问题支持两条修复路径：
  - `适配总帧数` 会把 Action `Total Frames` 扩到能包含当前最大结束帧。
  - `修正帧范围` 会把对应 phase / shape / weapon trace 的 frame range clamp 到 `[0, TotalFrames - 1]`。
- Quick action 修改 Authoring / Binding asset 时走 Undo / dirty，并刷新 validation、Inspector、Timeline、Scene gizmo 上下文。
- Inspector 窄窗口下调整了 Validation Report 高度和 issue row 高度，避免 issue 文本、按钮、Report Preview 重叠。
- Unity MCP 编译刷新通过，Console error = 0。
- Authoring EditMode tests 通过：`MxFramework.Tests.Combat.Authoring.*` 共 11/11 passed。
- 手动验证：
  - 制造 `Total Frames = 1`、`Startup = 0-24` 后，点击 `适配总帧数`，`Total Frames` 变为 25，issue 消失，Timeline 扩到 0-24。
  - 再制造同一错误后，点击 `修正帧范围`，`Startup` 变为 0-0，issue 消失，Inspector / Timeline 同步刷新。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要提交或改动以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `.gitnexus` / `Tools/*.py`
