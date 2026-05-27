# Combat Authoring M10I.3：Shape Delete / Duplicate Quick Actions

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10I_2_TIMELINE_RANGE_DRAGGING.md`
> 设计依据：`COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md` 的 `Authoring 编辑交互原则` 与 `M10I`
> 派发对象：Editor / Authoring 子代理

## 背景

当前 Combat Authoring 已经可以添加 Hitbox / Hurtbox，并能通过详情面板、Scene View handle 和 timeline 拖拽调整基础数据。但 Shape 只能新增，不能删除或快速复制，导致测试和试错成本偏高，也不符合“能不打字就不打字，拖动、选择、点击为主”的编辑原则。

## 目标

让 Hitbox / Hurtbox 的生命周期形成最小闭环：创建、选择、编辑、复制、删除、Undo。开发者应能在 Combat Authoring 窗口内通过按钮或键盘删除当前 Shape，也能复制当前 Shape 作为新的编辑起点。

## 范围

本阶段做 Shape Delete / Duplicate v0：

- 选中 Hitbox / Hurtbox 后，详情面板显示清晰的 Shape 操作区。
- 支持删除当前选中的 Hitbox / Hurtbox。
- 支持复制当前选中的 Hitbox / Hurtbox。
- 支持键盘 `Delete` / `Backspace` 删除当前 Shape。
- 删除和复制都必须支持 Undo / Redo。
- 删除和复制后刷新：
  - timeline。
  - 详情面板。
  - validation report。
  - Scene View gizmo / overlay。
- 删除后选择应稳定：
  - 优先选择同组下一个 Shape。
  - 没有下一个则选择上一个。
  - 同组已空则清空选择并显示中文空状态。
- 复制后应自动选中新 Shape。
- 复制的新 Shape 必须生成新的 `trackId`，不能和原 Shape 冲突。
- 复制的新 Shape 应保留 shapeKind、frameRange、markerId、localCenter、radiusRaw、heightRaw 等编辑数据。
- 复制的新 Shape 的 `sourceOrder` 应按当前数组末尾递增或保持稳定排序语义，不得破坏 validator / exporter 的确定性。

## 交互要求

- 详情面板 Shape 操作区至少包含：
  - `复制 Shape`
  - `删除 Shape`
- `删除 Shape` 要有中文 tooltip，说明可用 Undo 恢复。
- `复制 Shape` 要有中文 tooltip，说明会创建一个新 trackId 的副本。
- `Delete` / `Backspace` 只在当前选中项是 Hitbox / Hurtbox 时生效。
- 当焦点在文本输入框、数值输入框、搜索框等可编辑控件内时，`Delete` / `Backspace` 不应触发 Shape 删除，避免误删输入内容。
- Action / Startup / Active / Recovery 不是本任务的删除目标，不允许被删除。
- 删除不弹出阻塞式确认框；依赖 Undo 恢复，并在窗口状态栏显示中文结果提示。
- 删除或复制失败时，状态栏显示明确原因，例如 `请先选择 Hitbox 或 Hurtbox`。

## 技术建议

- 主要修改 `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`。
- 继续使用现有 `SerializedObject` / `SerializedProperty` 路径写入数据。
- 可新增私有方法：

```text
IsShapeRow(TimelineRow row)
TryDeleteSelectedShape()
TryDuplicateSelectedShape()
FindShapeIndex(TimelineRow row)
SelectNearestShapeAfterDelete(...)
RegisterKeyboardShortcuts()
IsKeyboardEventFromEditableField(...)
```

- 删除数组元素时使用 `SerializedProperty.DeleteArrayElementAtIndex`，并确保 `ApplyModifiedProperties`。
- 复制数组元素时建议使用 `InsertArrayElementAtIndex`，再显式写入所有字段，避免 Unity 复制对象引用或保留旧 `trackId`。
- Undo 操作名使用中文或清晰英文均可，但要能读出行为，例如 `Delete Combat Shape` / `Duplicate Combat Shape`。
- 每次完成后调用现有刷新链路，保持 timeline、详情、validation、Scene View 同步。

## 非目标

- 不做多选删除。
- 不做批量复制 / 粘贴。
- 不做 Action phase 删除。
- 不做 WeaponTrace 删除。
- 不做拖拽复制或 timeline 空白处创建 shape。
- 不改 JSON export key。
- 不改 Runtime Combat 逻辑。

## 验收标准

- 打开 `MxFramework > Combat > Combat Authoring`。
- 选择 Action Asset。
- 添加或选择一个 Hitbox。
- 点击 `删除 Shape` 后：
  - 该 Hitbox 从 timeline 和 Scene View 预览中消失。
  - 详情面板不再显示已删除对象。
  - validation report 刷新。
  - Undo 后该 Hitbox 恢复。
- 选择一个 Hurtbox，按 `Delete` 或 `Backspace` 后：
  - Hurtbox 被删除。
  - Undo 后 Hurtbox 恢复。
- 选择一个 Hitbox，点击 `复制 Shape` 后：
  - 新 Hitbox 出现在 timeline。
  - 新 Hitbox 的 `trackId` 与原 Shape 不同。
  - 新 Hitbox 保留原 Shape 的帧范围和形状参数。
  - 新 Shape 自动成为当前选中项。
- 焦点在文本 / 数值输入控件内时，`Delete` / `Backspace` 不误删 Shape。
- Unity Console 无 error。
- Authoring EditMode tests 通过。

## 测试要求

子代理完成后不要提交 SVN，由主代理复核和提交。子代理至少自查：

- Unity MCP refresh / Console error 检查。
- 相关 Authoring EditMode tests。
- 手动打开 Combat Authoring 窗口，验证按钮删除、键盘删除、复制、Undo。
- 说明是否验证输入框焦点下 `Delete` / `Backspace` 不误删。

## 完成记录

- 详情面板选中 Hitbox / Hurtbox 时新增 `Shape 操作` 区，提供 `复制 Shape` 和 `删除 Shape`。
- `Delete` / `Backspace` 支持删除当前选中 Shape；焦点在文本、数值、slider、vector 等可编辑控件内时不会触发 Shape 删除。
- 删除和复制都通过 `SerializedObject` / `SerializedProperty` 写入，并支持 Undo / Redo。
- 删除后优先选中同组下一个 Shape，没有下一个则选上一个，同组为空时清空选择。
- 复制后保留原 Shape 的类型、帧范围、marker、本地中心、半径和高度，并生成新的 `trackId` / `sourceOrder`，自动选中新 Shape。
- 操作后会刷新 timeline、详情面板、validation report、Scene View gizmo / overlay，并显示中文状态提示。
- Unity MCP 编译 / Console error 检查：0 error。
- Authoring EditMode tests：11/11 passed。
- 不落盘反射烟测：
  - Add Hitbox 后数量为 1。
  - Duplicate 后数量为 2，原 `trackId=1`，副本 `trackId=2`。
  - Delete 当前副本后数量为 1。
  - Undo 后数量恢复为 2。
- 影响面检查完成。

## 提交边界

本任务只允许修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要提交或改动以下未跟踪本地文件：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `Tools/*.py`
