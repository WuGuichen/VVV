# Combat Authoring M10I.1：No-Typing Shape Details

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10G_EXPORT_RUNTIME_JSON.md`
> 设计依据：`COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md` 的 `Authoring 编辑交互原则` 与 `M10I`
> 派发对象：Editor / Authoring 子代理

## 目标

把 Combat Authoring 的 Shape 详情面板从“裸字段输入”推进到“拖动、选择、点击为主，打字为辅”。开发者创建 Hitbox / Hurtbox 后，应能在不手打 marker、shape 类型、半径、高度和基础帧范围的情况下完成常用编辑。

本阶段只做 Shape Details v0，不做完整 timeline range 拖拽；timeline range handle 留给 M10I.2。

## 范围

### Shape 类型

- `shapeKind` 不再只使用裸 `PropertyField`。
- 使用选择控件展示可用类型：
  - Sphere
  - Capsule
  - Aabb
  - Sector
- 控件显示中文说明和英文 key。
- 修改必须写回 `SerializedProperty`，支持 Undo / dirty / validation refresh。

### Marker 绑定

- `markerId` 不再只依赖手打。
- 如果当前有 `CombatSceneBindingAsset.Markers`，提供下拉选择。
- 下拉项显示：
  - markerId
  - targetPath 简短提示
- 保留高级文本输入或只读显示，但必须：
  - 显示中文 tooltip。
  - 空值 / 找不到 marker 时显示窗口内 warning。
  - 不只依赖 Console。
- 提供 `使用当前选择重连` 或复用现有 `重连选中对象` 入口，帮助从 Scene 对象生成 marker。

### FrameRange 精修

- `frameRange` 在本阶段可继续用字段编辑，但必须补充 no-typing 辅助：
  - start / end 提供 integer stepper 或等价控件。
  - 自动 clamp 到 `[0, TotalFrames - 1]`。
  - 如果 start > end，自动修正或显示明确 warning。
- 不能让非法帧范围静默写入。

### Raw 数值

- `radiusRaw` 和 `heightRaw` 不再只靠手打。
- 提供 slider / stepper / preset button 中至少一种 no-typing 控件。
- 显示单位换算：`1,000,000 raw = 1 Unity unit`。
- `radiusRaw` 必须 clamp 为正数。
- `heightRaw` 必须 clamp 为 `0` 或正数；当 shape 是 Capsule 且高度小于直径时，显示 warning 或引导修正。
- 常用 preset 建议：
  - 小：0.25 unit
  - 中：0.5 unit
  - 大：1.0 unit

### 反馈与刷新

- 改动后刷新 timeline、Scene View gizmo 和 validation。
- 不刷屏、不依赖 Console。
- 所有新增控件必须有中文 tooltip。

## 非目标

- 不做 timeline range 拖拽。
- 不做 WeaponTrace root / tip 选择器。
- 不做外部 Authoring Editor。
- 不改 JSON export key。
- 不改 Runtime Combat 逻辑。

## 建议实现

- 主要修改 `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`。
- 可以增加小型 Editor helper class，但不要引入新框架。
- 优先使用 UI Toolkit 内置控件：
  - `EnumField`
  - `DropdownField`
  - `IntegerField`
  - `SliderInt`
  - `Button`
  - `HelpBox`
- 继续通过 `SerializedObject` / `SerializedProperty` 修改数据。
- 每次修改前确保 Undo 语义正确；如果使用 `SerializedProperty` 绑定，确认 `ApplyModifiedProperties` 后 dirty 状态和 validation 刷新正常。

## 验收标准

- 打开 `MxFramework > Combat > Combat Authoring`。
- 选择 Action 和 Binding。
- 点击 `添加 Hitbox`。
- 选中新增 Hitbox 后，右侧详情面板可以通过选择 / 点击 / 拖动完成：
  - 切换 Shape 类型。
  - 从 Binding marker 下拉选择 marker。
  - 调整 radiusRaw。
  - 调整 Capsule heightRaw。
  - 调整 start / end frame，并被 clamp 到合法范围。
- 非法或缺失 marker 在窗口内有 warning。
- 改动后 Scene View gizmo 和 validation 状态刷新。
- Unity Console 无 error。
- Authoring EditMode tests 通过。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- 相关 Authoring EditMode tests。
- 手动打开 Combat Authoring 窗口，说明新增控件是否可见。

## 完成记录

- Shape 详情面板新增 `shapeKind` 下拉，显示中文名称和英文 key。
- `markerId` 提供 Binding marker 下拉、窗口内 warning、高级文本入口和 `使用当前选择重连`。
- `frameRange` 提供 Start / End integer stepper，写入时 clamp 到 Action 总帧范围，并修正 start / end 顺序。
- `radiusRaw` / `heightRaw` 提供 slider、输入框、常用 preset 和 raw/unit 换算提示。
- Capsule 高度小于直径时显示 warning，并提供 `高度=直径` quick fix。
- 修正 timeline 刷新时清空当前 selection 的问题，确保 Shape 详情不会被 delayed refresh 立即清掉。
- Unity Console error 检查：0 error。
- Authoring EditMode tests：11/11 passed。
- 影响面检查完成。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要提交或改动以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `Tools/*.py`
