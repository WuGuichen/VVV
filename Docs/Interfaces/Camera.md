# Camera 接口

> Phase 14 v0.1，2026-05-19。本文记录当前已实现的 Camera runtime 契约。

## 职责

Camera 提供框架级表现相机能力：profile、request、target snapshot、target group framing、evaluated state、diagnostics、Unity Camera apply、Animation presentation event bridge、Debug UI snapshot 和 profile authoring adapter。

`MxFramework.Camera` 是 noEngine core，不引用 `UnityEngine`、`UnityEditor`、Cinemachine、Input System 或 UI Toolkit。它只根据显式输入求值，不读取 Unity Camera 当前状态，也不改变 Gameplay / Combat 权威状态。

## 程序集

| 程序集 | 职责 |
| --- | --- |
| `MxFramework.Camera` | noEngine DTO、service、profile validation、target group solver、diagnostics、Null backend、view basis helper |
| `MxFramework.Camera.Unity` | Unity Camera rig、Transform / Renderer target binder、profile provider、ScriptableObject authoring adapter |
| `MxFramework.Camera.Animation` | Animation presentation event sink，把 Camera event payload 转为 camera request |
| `MxFramework.Camera.Editor` | profile asset 创建菜单和 inspector validation |
| `MxFramework.DebugUI.Adapters` | `CameraDebugSource` 只读 Debug UI adapter |

## 公开接口

| 类型 | 用途 |
| --- | --- |
| `MxCameraProfileId` / `MxCameraRigId` / `MxCameraTargetRef` / `MxCameraTargetGroupId` | 强类型稳定 id |
| `MxCameraProfileDefinition` | runtime profile DTO，包含 mode、distance/FOV/orthographic size、smoothing、target lost grace、shake limit 等 |
| `MxCameraTargetSnapshot` | 单帧 target 输入，包含 position、forward/up、bounds、weight、primary、valid 和 frame |
| `MxCameraTargetGroup` / `MxCameraTargetGroupState` | 多目标输入和求值后的 center/bounds/radius/primary 摘要 |
| `MxCameraRequest` | SetProfile、BindTarget、SetTargetGroup、Focus、Shake、Impulse、Zoom 等表现请求 |
| `MxCameraEvaluationContext` | 单次求值输入：frame、delta、viewport、previous state、profiles、targets、requests |
| `MxCameraEvaluationResult` | 稳定输出：state、accepted/rejected request ids、target group state、diagnostics、summary |
| `MxCameraState` | backend 可应用状态：position、rotation、projection、FOV、orthographic size、focus、shake、source |
| `MxCameraService` | request queue、deterministic ordering、profile validation、target group solver、fallback / grace |
| `IMxCameraBackend` | backend apply 边界；Unity backend 实现该接口 |
| `MxCameraFacingBasisResolver` | 从 camera state 生成组合根可用的 camera-facing basis |
| `MxCameraUnityRig` | Unity Camera apply adapter，`ApplyLate` 同一 Unity frame 最多应用一次 |
| `MxCameraTransformTargetBinder` / `MxCameraRendererBoundsTargetBinder` | Unity target snapshot adapter |
| `MxCameraProfileAuthoringAsset` | ScriptableObject authoring adapter，可导出 runtime DTO |
| `MxCameraPresentationEventSink` | Animation presentation event sink，输出 shake/focus/impulse request |
| `CameraDebugSource` | Debug UI 只读 source |

## 诊断码

当前稳定码定义在 `MxCameraDiagnosticCodes`：

- `CAM_PROFILE_MISSING`
- `CAM_INVALID_PROFILE`
- `CAM_TARGET_LOST`
- `CAM_GROUP_EMPTY`
- `CAM_GROUP_BOUNDS_EXCEEDED`
- `CAM_INVALID_REQUEST`
- `CAM_REQUEST_CONFLICT`
- `CAM_INVALID_VIEWPORT`
- `CAM_BACKEND_UNAVAILABLE`
- `CAM_BACKEND_APPLY_FAILED`
- `CAM_BACKEND_MISSING_CAMERA`
- `CAM_BACKEND_MISSING_PROFILE_PROVIDER`
- `CAM_BACKEND_MISSING_TARGET_BINDER`
- `CAM_EVENT_PAYLOAD_MISSING`
- `CAM_EVENT_INVALID_EFFECT`

## 边界

- Camera core 不引用 Unity，不保存 Transform、Camera、GameObject、asset path 或 GUID。
- Unity backend 只应用表现状态，不把 Camera transform 回写为 Gameplay / Combat authority。
- Character Control core 不依赖 Camera；Demo 组合根可用 `MxCameraFacingBasisResolver` 提供 facing basis。
- Camera presentation event bridge 不让 Animation / Combat core 依赖 Camera。
- Debug UI snapshot 只读，不驱动 Gameplay / Combat authority。
- Profile authoring asset 是 adapter，不是 noEngine source of truth。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Camera/` 覆盖：

- single target follow。
- perspective / orthographic group framing。
- target lost grace / fallback。
- request ordering and conflict diagnostics。
- profile validation。
- shake clamp。
- Unity Camera position / rotation / FOV / orthographic size apply。
- Animation presentation event dedupe bridge。
- Debug UI snapshot sections。
