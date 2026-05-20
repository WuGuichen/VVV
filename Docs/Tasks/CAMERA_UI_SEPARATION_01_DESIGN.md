# Camera UI Separation 01：UI 相机分离与 3D UI 渲染设计

> Status: Spec Draft
> Issue: #258 `[Camera/UI] 01：UI 相机分离与 3D UI 渲染设计`
> Task level: S2 / S3 architecture slice
> Delivery level: Design / Implementation Plan
> Milestone: `Phase 14: Camera Management`
> Date: 2026-05-20

## 背景

`MxFramework.Camera` 已经提供 noEngine 相机求值、Unity Camera backend、profile、request、target group、Debug UI snapshot 和 Demo migration 基础。当前缺口不是“再做一套 UI 相机系统”，而是需要在既有 Camera 边界内补齐 UI 表现层相机能力：

- 普通 HUD / Debug UI 继续由 UI Toolkit 渲染，不需要 UI Camera。
- 全屏叠加的 3D UI 表现需要可选 UI 3D Overlay Camera，例如武器前景、驾驶舱、角色轮廓、技能前景特效。
- 嵌入式 3D UI 需要独立 preview camera -> RenderTexture -> UI Toolkit 显示路径，例如背包角色预览、装备模型、卡牌立绘、头像模型。
- UI 相关相机状态只能是表现层，不进入 Gameplay / Combat authority、Runtime hash、Replay hash 或 SaveState 默认路径。

本设计作为 `Phase 14: Camera Management` 的 UI camera extension slice，先冻结方案、边界、里程碑和后续 implementation Issue 输入。

## 目标

1. 定义 UI 相机分离的判断规则：哪些 UI 不需要相机，哪些需要 overlay camera，哪些需要 RenderTexture preview。
2. 定义 UI 3D Overlay Camera 在 URP Camera Stack 中的边界。
3. 定义嵌入式 3D UI preview camera / RenderTexture / UI Toolkit 的组合方式。
4. 明确 `MxFramework.Camera`、`MxFramework.Camera.Unity`、UI Toolkit、Resources 和 Debug UI 的复用方式。
5. 拆出一个可执行里程碑，覆盖设计、core adapter、URP stack、preview slot、Demo 验证和诊断。

## 设计结论

UI 相机能力采用“三层策略”：

| 场景 | 默认方案 | 是否需要 UI Camera |
| --- | --- | --- |
| HUD、Debug UI、菜单、血条、纯 2D 面板 | UI Toolkit + `UIDocument` / `PanelSettings` | 不需要 |
| 全屏叠加 3D 表现，和主画面同帧合成 | URP Base Camera + UI 3D Overlay Camera | 需要，可选 |
| 面板内独立 3D 模型或小视窗 | Preview Camera -> RenderTexture -> UI Toolkit texture view | 需要，但不进主 camera stack |
| 世界空间 UI 标记 / 名字牌 | 由主相机或普通 world-space view adapter 渲染 | 通常不需要独立 UI Camera |

核心原则：

- UI Camera 是表现层 rig，不是第二套 gameplay camera authority。
- `MxFramework.Camera` core 不新增 `UI` 概念；UI 相机通过 `MxCameraRigId` 区分，例如 `main`、`ui.presentation`、`ui.preview.character`。
- URP Camera Stack 操作只存在 Unity-facing adapter，不进入 noEngine core。
- UI Toolkit 不反向依赖 Camera；UI 只接收 texture、view model 或组合根注入的 adapter。
- UI 3D 对象必须使用专用 layer，例如 `MxUi3D` / `MxUiPreview3D`，主相机默认不渲染这些 layer。

## API 复用计划

| 需求点 | 优先使用的框架 API / 模块 | 本任务使用方式 | 不使用时的原因 |
| --- | --- | --- | --- |
| 主游戏相机 | `MxFramework.Camera`、`MxCameraService`、`MxCameraUnityRig` | 复用现有 main rig，不改 core 语义 | 不适用 |
| UI 3D overlay 相机 | `MxFramework.Camera.Unity` + 新 Unity-facing stack adapter | 新增 `ui.presentation` rig，作为表现相机接入 URP stack | 不另建平行相机 core |
| 嵌入式 3D 预览 | `MxFramework.Camera.Unity` + RenderTexture view adapter | 新增 preview rig / slot，输出 texture 给 UI Toolkit | 不进入主 stack，避免面板预览影响主画面 |
| 运行时 UI | `MxFramework.UI.Toolkit`、UXML / USS、`UIDocument` | 普通 HUD 和 texture 展示继续走 UI Toolkit | 不用 OnGUI |
| 资源加载 / 生命周期 | `Resources` / Catalog / 组合根 | preview prefab、材质、RenderTexture 池由 view adapter 或组合根管理 | 不把资源引用写入 noEngine camera profile |
| 调试诊断 | Camera Debug UI source、Debug UI Toolkit | 增加多 rig / UI camera snapshot 只读展示 | 不提供可写调参入口 |
| Runtime hash / SaveState | Runtime 默认路径 | UI camera 状态默认排除 | UI 表现不属于权威状态 |

## 模块边界

```text
MxFramework.Camera
  - 不新增 UI / URP / RenderTexture 类型
  - 继续只表达 profile、request、target snapshot、evaluated state、diagnostics

MxFramework.Camera.Unity
  - 可持有 UnityEngine.Camera / Transform / RenderTexture
  - 可提供 UI camera rig、preview target binder、texture output adapter
  - 不直接依赖 UI Toolkit 控件

MxFramework.Camera.URP 或 Camera.Unity 内部 URP adapter
  - 负责 Base / Overlay Camera stack 绑定
  - 依赖 URP assembly 时必须隔离在 Unity-facing 程序集

MxFramework.UI.Toolkit
  - 只负责显示 texture / view model / 控件状态
  - 不引用 Camera core 或 URP

Demo / Game composition root
  - 创建 main rig、ui.presentation rig、preview rig
  - 绑定 layer、culling mask、RenderTexture、UIDocument
  - 负责生命周期和资源释放
```

如新增独立程序集，推荐：

```text
Assets/Scripts/MxFramework/Camera.URP/
Assets/Scripts/MxFramework/Camera.Unity/
Assets/Scripts/MxFramework/UI.Toolkit/Runtime/
```

`MxFramework.Camera.URP` 只有在项目决定显式引用 URP runtime assembly 时创建；否则首版可以把 stack binder 放在 Demo / Editor 生成器中，避免提前冻结公共依赖。

## 方案 A：UI 3D Overlay Camera

适用场景：

- 全屏武器 / 手臂 / cockpit / 前景装饰。
- 必须和主相机同画面合成的 UI 3D 模型。
- 技能蓄力、目标锁定、角色轮廓等 UI 表现特效。

运行结构：

```text
Main Camera
  - URP Base Camera
  - cullingMask excludes MxUi3D
  - owned by MxCameraUnityRig(main)

UI 3D Camera
  - URP Overlay Camera
  - cullingMask includes only MxUi3D
  - clearDepth / no color clear according to URP stack rules
  - owned by MxCameraUnityRig(ui.presentation) or simple static adapter

URP stack binder
  - adds UI 3D Camera to Main Camera overlay stack
  - validates renderer supports camera stacking
  - reports diagnostics if stack binding fails
```

约束：

- UI 3D Camera 不参与 Gameplay / Combat target framing。
- UI 3D Camera 可以使用 `MxCameraProfileDefinition` 表达 FOV、orthographic size、offset 和 shake limit，但它的 target snapshot 来自 UI view adapter。
- UI 3D 物体必须放在专用 layer，避免主相机重复渲染。
- Overlay camera 不应渲染普通 world / character / effect layer。
- Demo / scene generator 必须验证相机 stack 存在且排序稳定。

推荐新增类型：

| 类型 | 所属层 | 用途 |
| --- | --- | --- |
| `MxUiCameraRigKind` | Camera.Unity 或 Demo | `Overlay3D` / `PreviewTexture` 的轻量分类，避免污染 core enum。 |
| `MxCameraUrpStackBinder` | Camera.URP / Unity-facing | 绑定 base camera 与 overlay camera stack。 |
| `MxUi3DLayerPolicy` | Unity-facing | 校验 main camera / UI camera culling mask。 |
| `MxUiCameraDebugSummary` | Debug adapter | 输出 rig id、stack status、layer mask、target texture。 |

## 方案 B：RenderTexture Preview Camera

适用场景：

- 背包 / 装备 / 角色详情中的 3D 模型。
- 卡牌、头像、物品 inspect、技能模型预览。
- 需要多个独立 preview slot，且不应该受主相机栈影响。

运行结构：

```text
Preview prefab / model
  -> Preview stage root or hidden preview layer
  -> Preview Camera
  -> RenderTexture
  -> UI Toolkit Image / VisualElement background image
```

约束：

- preview camera 默认不加入 main camera stack。
- preview scene root / layer 与主场景隔离，避免被主相机看到。
- RenderTexture 生命周期由 adapter 或组合根集中管理，不能散落临时创建不释放。
- UI Toolkit 只显示 texture，不持有 preview 业务规则。
- preview 模型资源通过 Catalog / ResourceKey 或组合根注入，不把 Unity asset 引用写进 noEngine Camera profile。

推荐新增类型：

| 类型 | 所属层 | 用途 |
| --- | --- | --- |
| `MxUiPreviewCameraSlot` | Camera.Unity 或 UI-facing adapter | 管理 preview camera、target texture、root transform。 |
| `MxUiPreviewTextureHandle` | Unity-facing | 封装 RenderTexture 生命周期和尺寸。 |
| `MxUiPreviewTextureElementBinder` | UI.Toolkit adapter / Demo | 把 RenderTexture 绑定到 UI Toolkit element。 |
| `MxUiPreviewCameraPool` | Unity-facing | 可选，复用 preview slot，降低创建成本。 |

## 排序和渲染规则

- 普通 UI Toolkit overlay 应在最终视觉上覆盖主画面和 UI 3D overlay，除非某个 Demo 明确需要 3D 前景盖住 UI。
- UI 3D Overlay Camera 的深度 / stack 顺序由组合根固定，不由各 UI 控件临时修改。
- RenderTexture preview 的排序由 UI Toolkit layout 决定，不参与 Camera Stack 排序。
- 多 Panel 场景必须显式配置 `PanelSettings.sortingOrder`，避免 Debug UI、HUD 和 preview 面板互相遮挡。
- UI 3D Camera 的 post-processing 默认关闭，除非 Demo 或项目 profile 明确启用并有验证。

## 诊断码建议

新增诊断可以先进入 Unity-facing debug summary，后续再决定是否提升为 `MxCameraDiagnosticCodes` 稳定码：

| Code | 场景 |
| --- | --- |
| `CAM_UI_STACK_BIND_FAILED` | Base camera 或 overlay camera 无法绑定 URP stack。 |
| `CAM_UI_LAYER_MASK_INVALID` | main / UI camera culling mask 与专用 layer 策略冲突。 |
| `CAM_UI_PREVIEW_TEXTURE_MISSING` | preview slot 没有可用 RenderTexture。 |
| `CAM_UI_PREVIEW_CAMERA_MISSING` | preview slot 缺少 Camera。 |
| `CAM_UI_PANEL_TEXTURE_BIND_FAILED` | RenderTexture 无法绑定到 UI Toolkit element。 |

诊断必须只读展示，不提供默认可写修复按钮。

## 里程碑拆分

建议归入现有 Gitea milestone：`Phase 14: Camera Management`。

该方向可作为 milestone 内的一个子里程碑：`Phase 14B: UI Camera Separation`。

| Slice | 建议 Issue | 交付 |
| --- | --- | --- |
| UI camera design | #258 `[Camera/UI] 01：UI 相机分离与 3D UI 渲染设计` | 本设计文档、方案边界、验收切片。 |
| UI camera core adapters | #259 `[Camera/UI] 02：UI Camera Rig Contracts` | UI camera rig 分类、layer policy、debug summary，不改 noEngine Camera core。 |
| URP overlay stack MVP | #260 `[Camera/UI] 03：URP Overlay Camera Stack MVP` | Base + Overlay 绑定、layer mask 校验、PlayMode smoke。 |
| RenderTexture preview MVP | #261 `[Camera/UI] 04：Preview Camera RenderTexture Slot` | Preview slot、RT lifecycle、UI Toolkit texture binding。 |
| Demo validation | #262 `[Camera/UI] 05：3D UI Demo Validation` | 在一个现有或新 Demo 验证 overlay 3D UI 和 preview 3D UI 至少一种路径。 |
| Debug diagnostics | #263 `[Camera/UI] 06：UI Camera Debug Diagnostics` | 多 rig / stack / texture 状态进入 Debug UI 只读 snapshot。 |

## 验收标准

设计 Issue 完成条件：

- 明确普通 UI、UI 3D overlay、RenderTexture preview 三类场景的选择规则。
- 明确不新增平行 Camera core，不让 UI 相机进入 Gameplay / Combat authority。
- 明确 URP stack adapter 和 RenderTexture preview adapter 的 Unity-facing 边界。
- 明确 layer、culling mask、PanelSettings sorting、RenderTexture lifecycle 的基本规则。
- 明确后续 implementation slices 和验收方式。

后续实现总体验收：

- `MxFramework.Camera` noEngine core 不引用 UnityEngine、UnityEditor、URP、UI Toolkit 或 RenderTexture。
- UI 3D Overlay Camera 只渲染专用 UI 3D layer，主相机不重复渲染该 layer。
- Preview Camera 输出 RenderTexture，并能被 UI Toolkit 面板稳定显示。
- RenderTexture 创建、尺寸变更和释放有明确生命周期。
- Camera Debug UI 能看到 main rig、UI overlay rig、preview rig 的只读状态。
- 至少一个 Unity PlayMode 或等价验证确认画面非空、相机 stack / texture 绑定成功、Console 无新增 error。

## 非目标

- 不迁移所有现有 UI 到 UI Camera。
- 不把 UI Toolkit 变成 Camera 依赖模块。
- 不做完整 UI 编辑器、preview stage 编辑工具或资源导入器。
- 不引入 Cinemachine 依赖。
- 不把 UI camera state 写入 Replay hash、Runtime hash 或 SaveState 默认路径。
- 不手写 Unity scene / prefab / ScriptableObject YAML。
