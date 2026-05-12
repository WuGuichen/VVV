# Preview Scene Target 02：Dynamic Runtime Target

> **状态**: ✅ 已完成（r1200）
> **优先级**：P0
> 前置任务：`AUTHORING_EDITOR_07_SCENE_PREVIEW.md`

## 目标

优化 Runtime Preview Scene Target 的装配方式：场景中不再保存运行时 `MxPreviewSceneTarget`，改为保存轻量编辑态配置。Preview Server 启动或刷新时，根据配置动态生成真实运行时目标。

## 完成结果

- 新增 `MxPreviewSceneTargetProfile`：
  - 以 Resources 资产保存 TargetId、HP、Attack、Defense、ResetOnPreviewRun、ShowOverlay、是否生成运行时目标。
  - 由 `RuntimeVerticalSliceConfigWindow` 统一编辑。
  - 不实现 `IBuffTarget`，不承载运行时 Buff / Attribute 状态。
- `MxPreviewSceneTarget` 改为运行时目标：
  - 移除 `ExecuteAlways`。
  - 增加 `Configure(...)`，由配置动态初始化。
  - 仍负责 `IBuffTarget`、AttributeStore、BuffPipeline、ModifierPipeline 和 Preview Server 快照。
- `ScenePreviewWorld`：
  - 优先查找现有 runtime target。
  - 没有 runtime target 时查找 `MxPreviewSceneTargetConfig` 并动态生成 runtime target。
  - 没有有效配置时才回退 dummy world。
- `RuntimeVerticalSlice.unity`：
  - 不再保存 `PreviewCaster` / `PreviewTarget` 预挂对象。
  - Preview Target 由 profile 资产在运行时生成。
- 新增 `MxPreviewSceneTargetConfigEditor`：
  - 提供统一中文提示、tooltip、运行态/编辑态区别说明。

## 规则

- 场景资产中不应再手动挂 `MxPreviewSceneTarget`。
- 要编辑预览目标，打开 `MxFramework / Runtime Showcase / Scene Config`。
- 真正的 `MxPreviewSceneTarget` 只在 Preview Server / ScenePreviewWorld 运行时动态生成。
