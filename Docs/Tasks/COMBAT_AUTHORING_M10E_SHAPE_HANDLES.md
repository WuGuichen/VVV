# Combat Authoring M10E：Shape Handles v0

> **状态**：已完成
> **优先级**：P0
> 前置任务：`COMBAT_AUTHORING_GIZMO_TOOL_DESIGN.md`

## 目标

让 Combat Authoring 不只停留在只读 Gizmo 预览，而是能从编辑器窗口创建基础 Hitbox / Hurtbox，并在 Scene View 中通过 Handle 做最小可用的可视化编辑。

## 完成结果

- Combat Authoring 窗口新增 `添加 Hitbox`、`添加 Hurtbox`：
  - 在当前帧创建默认 `Sphere` shape。
  - 默认绑定到当前 Scene Binding 的第一个 Actor marker；没有 binding 时保留空 marker 并交给 Validation 提示。
  - 创建后自动选中对应 timeline 行。
- Timeline 选中状态同步到 Scene View：
  - 记录 section、trackId、SerializedProperty path。
  - Gizmo 层可根据选中行判断当前可编辑 shape。
- Scene View 支持基础 shape 半径编辑：
  - 选中 Hitbox / Hurtbox timeline 行后显示白色选中半径圈。
  - 使用 Unity `Handles.RadiusHandle` 调整 `radiusRaw`。
  - 修改进入 Undo，并标记 Action Asset dirty。

## 约束

- M10E v0 只覆盖 Sphere / Capsule 共用半径编辑，不处理中心、旋转、AABB half extents、Sector angle 或 WeaponTrace root / tip。
- Handle 写回 Authoring Asset，不写 Runtime 状态。
- Runtime Core 不新增 UnityEditor 依赖。

## 验收

- 能通过 `MxFramework > Combat > Combat Authoring` 打开窗口。
- 点击 `添加 Hitbox` 后，timeline 出现 `Hitbox / Sphere #1`。
- Scene View 中出现对应 Hitbox 标签和选中半径圈。
- 拖动半径 Handle 会更新 Action Asset，并可用 Unity Undo 撤销。
