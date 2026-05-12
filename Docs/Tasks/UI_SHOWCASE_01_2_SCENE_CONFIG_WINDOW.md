# UI Showcase 01.2：Scene Config Window

> **状态**: ✅ 已完成（r1203）
> **优先级**：P0
> 所属 Goal：`PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 前置任务：`UI_SHOWCASE_01_1_DYNAMIC_MOUNT_CONFIG_INSPECTOR.md`

## 目标

把测试场景预配置从场景组件迁移到统一 EditorWindow + 配置资产。场景只作为运行舞台，不预挂 `RuntimeVerticalSliceRunner`、`PreviewCaster`、`PreviewTarget` 或 `SceneTargetConfig`。

## 完成结果

- 新增 `RuntimeVerticalSliceSceneConfig` 资产：
  - 配置资产路径：`Assets/Config/MxFramework/Demo/RuntimeVerticalSliceSceneConfig.asset`
  - 控制 Runtime Vertical Slice 的自动启动、Showcase 模式、Patch / Mod 路径、初始数值和诊断输出。
- 新增 `MxPreviewSceneTargetProfile` 资产：
  - 配置资产路径：`Assets/Config/MxFramework/Preview/RuntimeVerticalSlicePreviewTargets.asset`
  - 控制 Preview Target 列表，默认包含 `TestTarget` 和 `TestCaster`。
- 新增 `RuntimeVerticalSliceBootstrap`：
  - Play 后按配置自动创建 `RuntimeVerticalSliceRuntime`。
  - 再由 Runner 动态挂载 Ability Runner / HUD / UI 适配器。
- 新增 `RuntimeVerticalSliceConfigWindow`：
  - 菜单：`MxFramework / Runtime Showcase / Scene Config`
  - 使用 UI Toolkit EditorWindow。
  - 提供配置分组、中文说明、tooltip、校验提示、打开场景和定位资产按钮。
- `RuntimeVerticalSlice.unity` 删除预挂对象：
  - `RuntimeSliceRunner`
  - `PreviewTarget`
  - `PreviewCaster`
- `ScenePreviewWorld` 在没有场景组件时从 `MxPreviewSceneTargetProfile` 生成运行时目标。
- HUD 样式调整为单一紧凑面板，Preview Target legacy overlay 默认关闭，避免 Play 后多个显示层堆叠。

## 规则

- 测试场景中不应预挂 Showcase / Preview 运行时组件。
- 修改测试场景运行方式，优先打开 `MxFramework / Runtime Showcase / Scene Config`。
- 交互层必须给出明确提示：配置来源、运行时生成行为、风险和兜底结果。
