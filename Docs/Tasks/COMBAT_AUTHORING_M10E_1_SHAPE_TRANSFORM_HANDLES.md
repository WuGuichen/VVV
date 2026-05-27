# Combat Authoring M10E.1：Shape Transform Handles

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_M10E_SHAPE_HANDLES.md`
> 派发对象：Editor / Authoring 子代理

## 目标

把 M10E v0 的“只可调半径”推进到真正可用的基础 shape 摆放工具。开发者应能在 Combat Authoring 窗口创建 Hitbox / Hurtbox 后，在 Scene View 中直接移动 shape 中心并调整基础尺寸，所有修改写回 Authoring Asset，并支持 Undo。

## 范围

本阶段只做 Sphere 和 Capsule 的最小完整编辑闭环：

- Sphere：
  - 中心移动 handle。
  - 半径 handle。
  - 写回 authoring 数据。
- Capsule：
  - 中心移动 handle。
  - 半径 handle。
  - 高度或端点 handle。
  - 写回 authoring 数据。
- 选中反馈：
  - 选中 timeline shape 行后，Scene View 明确显示该 shape 的选中轮廓和可拖拽 handle。
  - 非选中 shape 只显示普通 gizmo，不出现编辑 handle。
- Undo / Dirty：
  - 每次拖动进入 Unity Undo。
  - 修改后标记 Action Asset dirty。
  - 不写 Runtime 状态，不创建长期场景组件。

## 数据契约要求

如果当前 `CombatShapeAuthoringData` 缺少表达中心、高度等字段，可以扩展 Authoring 数据，但必须遵守：

- 字段使用稳定英文 key。
- 兼容旧 asset 默认值。
- Runtime Core 不引用 `UnityEditor`。
- Validation 对新增字段至少覆盖明显非法值：
  - 半径小于等于 0。
  - Capsule 高度小于直径，或自动 clamp 并给 warning。
  - FrameRange 越界仍由现有规则处理。

建议字段：

```text
localCenterRaw: FixVector3 raw 或 Vector3 authoring 值
localRotationEulerRaw / localYawRaw: 可选，本阶段如不做旋转可先不加
heightRaw: Capsule 使用
```

如选择暂不扩展完整 fixed-vector 类型，可以在 Authoring 层使用 Unity `Vector3 localCenter`，但导出前必须明确后续会转换为 runtime fixed data。

## 交互要求

- Combat Authoring 窗口：
  - `添加 Hitbox` / `添加 Hurtbox` 创建默认 Sphere。
  - 详情面板显示新增 shape 字段，并有中文说明。
  - 选中 timeline 行后 Scene View handle 同步。
- Scene View：
  - `Handles.PositionHandle` 或等价 handle 编辑中心。
  - `Handles.RadiusHandle` 编辑半径。
  - Capsule 使用高度/端点 handle，尺寸随视距保持可操作。
  - Handle 颜色和选中轮廓不污染其它 Gizmo。
- 容错：
  - 没有 Scene Binding 或 marker 找不到时，不崩溃，显示 validation issue。
  - 拖动过程中不刷屏日志。

## 非目标

- 不做 AABB / OBB。
- 不做 Sector 角度编辑。
- 不做 WeaponTrace root / tip / substep 编辑。
- 不做 Runtime Export。
- 不做 Query / Resolve Explain。

## 需要修改的主要文件

- `Assets/Scripts/MxFramework/Combat.Authoring/CombatActionAuthoringAsset.cs`
- `Assets/Scripts/MxFramework/Combat.Authoring/CombatAuthoringValidator.cs`
- `Assets/Scripts/MxFramework/Combat.Editor/CombatGizmoDrawer.cs`
- `Assets/Scripts/MxFramework/Combat.Editor/CombatAuthoringWindow.cs`
- `Assets/Scripts/MxFramework/Tests/Combat/Authoring/CombatAuthoringValidatorTests.cs`

## 验收标准

- 在 `MxFramework > Combat > Combat Authoring` 中点击 `添加 Hitbox`。
- Timeline 出现 shape 行，并自动选中。
- Scene View 中出现可编辑 handle。
- 拖动中心 handle 后，shape 位置变化，并写回 Authoring Asset。
- 拖动半径 handle 后，半径变化，并写回 Authoring Asset。
- Capsule shape 可调整基础尺寸。
- Undo 能撤销最近一次 handle 修改。
- Unity Console 无 error。
- Authoring EditMode 测试通过。

## 完成结果

- `CombatShapeAuthoringData` 增加 `localCenter` 和 `heightRaw`，旧 asset 默认值兼容。
- Combat Authoring 详情面板显示 shape 类型、帧范围、marker、本地中心、半径和 Capsule 高度。
- Scene View 选中 Sphere / Capsule 后支持中心移动和半径编辑。
- Capsule 额外支持高度端点 handle。
- Validator 覆盖半径非法值和 Capsule 高度小于直径 warning。

## 测试要求

开发完成后由主代理执行最终验证。子代理需要至少自查：

- `Unity MCP` 刷新编译无 error。
- 相关 EditMode tests 通过或说明无法执行原因。
- 不提交 SVN，由主代理复核后提交。

## 提交边界

本任务只能修改 Combat Authoring / Editor / Tests 相关文件和本任务文档状态。不要提交或改动以下未跟踪测试资产：

- `Assets/CombatActionAuthoringAsset.asset`
- `Assets/CombatSceneBindingAsset.asset`
- `.claude` / `Tools/*.py` 等本地辅助文件
