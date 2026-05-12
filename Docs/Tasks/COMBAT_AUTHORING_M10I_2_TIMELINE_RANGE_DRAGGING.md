# Combat Authoring M10I.2：Timeline Range Dragging

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10I_1_NO_TYPING_SHAPE_DETAILS.md`
> 设计依据：`COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md` 的 `Authoring 编辑交互原则` 与 `M10I`
> 派发对象：Editor / Authoring 子代理

## 目标

让 Combat Authoring 的 timeline 从“展示和选择”推进到“可直接拖动编辑帧范围”。开发者应能通过鼠标拖动 Hitbox / Hurtbox / Action phase 的 range 条，完成 start / end / 整体位移调整，尽量不手打帧数。

## 范围

本阶段做 timeline range dragging v0：

- 支持拖动 timeline 条块主体，整体平移 `FrameRange`。
- 支持拖动条块左边缘调整 `startFrame`。
- 支持拖动条块右边缘调整 `endFrame`。
- 编辑目标至少覆盖：
  - Action / Startup
  - Action / Active
  - Action / Recovery
  - Hitbox
  - Hurtbox
- 拖动时按 fixed frame snap，不允许半帧。
- 写入时自动 clamp 到 `[0, TotalFrames - 1]`。
- 如果拖动导致 start > end，必须自动修正或阻止。
- 所有修改写回 `CombatActionAuthoringAsset` 的 `SerializedProperty`。
- 支持 Undo / dirty / validation refresh / Scene View repaint。

## 交互要求

- Timeline 条块 hover 或选中时显示清晰可见的左右边缘 handle。
- 鼠标靠近左边缘时进入 resize-start 模式。
- 鼠标靠近右边缘时进入 resize-end 模式。
- 鼠标在条块中间时进入 move-range 模式。
- 拖动过程中应显示即时视觉反馈：
  - 当前 preview range。
  - 中文 tooltip 或状态提示，例如 `移动范围：3-6`。
  - 越界时 clamp 后的结果。
- 鼠标释放时提交一次 Undo 操作，避免拖动过程中产生大量 Undo step。
- 拖动完成后右侧 Shape 详情 Start / End 控件同步刷新。
- Scene View gizmo 和 validation 状态同步刷新。

## 技术建议

- 主要修改 `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`。
- 可在窗口内新增小型私有状态结构，例如：

```text
TimelineDragState
TimelineDragMode: None / Move / ResizeStart / ResizeEnd
```

- 不要把 timeline UI 状态作为权威数据源；权威数据仍是 `SerializedObject` / `SerializedProperty`。
- 拖动过程可以只更新 preview 样式；最终提交时再写回 asset。
- 如果 v0 实现成本较低，也可以拖动过程中写回，但必须合并 Undo 或避免每帧生成大量 Undo 记录。
- 不要在每次 pointer move 时重建整棵 visual tree；只更新被拖动条块或 playhead / preview element。
- 使用稳定 frame mapping：
  - `frame = round((mouseX - trackStartX) / frameWidth)`
  - clamp 到合法范围。
- 对 `empty` range 不显示拖动条；可在后续任务提供“点击空 lane 创建 range”。

## 非目标

- 不做 timeline 上点击空白创建 range。
- 不做多选、批量移动、复制粘贴。
- 不做 WeaponTrace root / tip 编辑。
- 不做外部 Authoring Editor。
- 不改 JSON export key。
- 不改 Runtime Combat 逻辑。

## 验收标准

- 打开 `MxFramework > Combat > Combat Authoring`。
- 选择 Action Asset。
- 选中一个有非空 range 的 Hitbox / Hurtbox 或 Action phase。
- 拖动条块主体后，start / end 同步平移，并 clamp 到合法帧。
- 拖动左边缘后，只调整 start。
- 拖动右边缘后，只调整 end。
- 释放鼠标后：
  - 右侧详情 Start / End 显示新值。
  - timeline 条块位置刷新。
  - validation 状态刷新。
  - Scene View gizmo 刷新。
  - Undo 可撤销本次拖动。
- Unity Console 无 error。
- Authoring EditMode tests 通过。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- 相关 Authoring EditMode tests。
- 手动打开 Combat Authoring 窗口，说明是否验证过 move / resize-start / resize-end。
- 说明是否验证 Undo。

## 完成记录

- 非空 `Action / Startup`、`Action / Active`、`Action / Recovery`、`Hitbox`、`Hurtbox` timeline 条块支持主体拖动整体移动 range。
- timeline 条块左边缘支持调整 start，右边缘支持调整 end。
- 拖动按 fixed frame snap，并 clamp 到 `[0, TotalFrames - 1]`。
- 鼠标释放时通过 `SerializedObject` / `SerializedProperty` 写回 Action Asset，并执行 Undo / dirty / validation refresh / SceneView repaint。
- 拖动中只局部更新当前条块、标签和状态提示，不重建整棵 timeline。
- 主线手动验证：
  - Hitbox 主体拖动 `2-4` 到 `4-6`。
  - 左边缘 resize 到 `3-6`。
  - 右边缘 resize 到 `3-8`。
  - Undo 后回到 `3-6`。
- Unity Console error 检查：0 error。
- Authoring EditMode tests：11/11 passed。
- GitNexus detect-changes：low risk，affected processes 0。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要提交或改动以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `.gitnexus` / `Tools/*.py`
