# Camera Management 01：运行时相机管理设计

> Issue: #231「[Camera] 01：运行时相机管理设计」
> Status: Implementation Context Pack
> Task level: S2 / S3 architecture slice
> Delivery level: Design / Implementation Plan
> Date: 2026-05-19

## 目标

设计 `MxFramework.Camera` 相机管理模块，补齐框架当前缺少的运行时相机能力。首版目标是自研轻量相机系统，不引入 Cinemachine 硬依赖，同时支持单目标、多目标入镜、profile 切换、平滑、缩放、抖动、诊断和 Unity Camera 应用路径。

本任务只交付设计文档和后续实施切片计划，不实现代码，不创建 Unity 序列化资产，不手写场景 YAML。实现必须从 Gitea implementation Issue 开始。

本文的作用不是描述一个“理想相机系统”，而是冻结 Phase 14 的第一批可开发契约：哪些数据属于 noEngine core、哪些行为属于 Unity backend、哪些失败必须被诊断、哪些 Demo 可以作为第一条迁移路径。

## 设计结论

首版相机系统采用“noEngine 相机求值 + Unity 后端应用”的分层：

```text
Game / Demo composition root
  -> collects target snapshots
  -> sends camera requests
  -> MxCameraService.Evaluate()
  -> MxCameraEvaluationResult
  -> MxCameraUnityRig.ApplyLate(result.State)
  -> Unity Camera transform / projection
```

核心结论：

- `MxFramework.Camera` 是框架级表现层模块，不拥有 Gameplay / Combat 权威状态。
- 相机 core 只处理显式输入：profile、request、target snapshot、delta time、viewport aspect。
- Unity 后端只负责采样 Unity `Transform` / bounds，并把 `MxCameraState` 应用到 `Camera`。
- Character Control 不依赖 Camera；组合根用相机 view basis 生成 `CharacterFacingBasis`。
- 首版必须支持多目标入镜，因为这是角色、Boss、竞技场、本地多人和正交俯视 Demo 的共同需求。
- Cinemachine 是 future optional backend，不是 v1 依赖，也不能污染 noEngine core API。
- 运行时真正对外稳定的是 `MxCameraEvaluationResult`，不是一组散落字段；Debug UI、Unity backend、测试和 Demo 都应从同一结果读取 state 与 diagnostics。

交付等级：Design / Implementation Plan。后续任何声称 Playable 的相机 PR 必须提供可打开场景或迁移现有 Demo。

## 当前基础和缺口

已有基础：

- Runtime：`RuntimeHost`、`RuntimeTickStage`、显式 frame / delta，可承载相机服务的统一 tick。
- Character Control：已有 `CharacterFacingBasis`，核心层不直接读取 Unity Camera。
- Animation：`MxAnimationPresentationEvent` 已能表达 VFX / SFX 等表现事件，可扩展相机表现事件映射。
- Audio：已有 noEngine intent / backend 适配模式，可作为 Camera 模块的分层参考。
- Debug UI / Diagnostics：可通过只读 snapshot 暴露相机 active profile、target、blend、shake 和错误。
- Demo：多个 scene creator 和 runner 当前散落创建 `Camera.main`、手写 orbit / follow 逻辑。
- `RuntimeCombatShowcaseInputController` 当前包含 orbit yaw / pitch / distance、`Camera.main` resolve、pointer ray 和 `LateUpdate` apply，是首个 Demo migration 的高价值候选。

缺口：

- 没有统一相机 profile、target、group framing、blend、shake 和 zoom 契约。
- Demo 侧相机逻辑分散，无法复用到后续 Runtime Showcase / Playable Demo。
- Character Control 的 camera-facing 输入需要组合根自行处理，缺少标准 resolver。
- 战斗表现事件没有统一转相机震动、focus、impulse 的 sink。
- Debug UI 无相机诊断源，难以排查 target lost、profile mismatch 和 framing clipping。

不解决的问题：

- 不做 Gameplay / Combat 视角判定权威。
- 不做遮挡避障、碰撞探针、墙体透明或 camera collision volume。
- 不做 Timeline / Cutscene 工具。
- 不做完整 photo mode 工具链。
- 不做 split screen。
- 不强制迁移所有现有 Demo。
- 不引入 Cinemachine package，不新增 Cinemachine asmdef 依赖。

## API 复用计划

| 能力 | 复用方式 | 说明 |
| --- | --- | --- |
| RuntimeHost | 可选 `CameraRuntimeModule` 在 `PostSimulation` 计算 desired state | Gameplay / Combat / Character Control 先更新权威状态，相机随后读取 snapshot。 |
| Character Control | 提供 `MxCameraFacingBasisResolver` 给组合根使用 | Character Control core 仍不引用 Camera 或 Unity Camera。 |
| Animation presentation events | 通过 camera event sink 消费 `EventKind == "Camera"` 的表现事件 | 将动画 / 战斗帧事件转为 shake、focus、impulse 请求。 |
| Diagnostics / Debug UI | `MxCameraDebugSnapshot` + adapter 接入 source registry | 相机状态只读观察，不进入 Replay / SaveState / Runtime hash。 |
| Input | 项目层 adapter 把 look / zoom / lock-on / photo mode 输入转成 camera request | Camera core 不引用 `MxFramework.Input`。 |
| UI Toolkit | Demo HUD / Debug UI 可展示相机诊断和调试 controls | UI 只通过 request API，不直接改后端私有状态。 |

## 核心边界

`MxFramework.Camera` 是 noEngine 契约层：

- 不引用 `UnityEngine`、`UnityEditor`、Cinemachine、Input System 或 UI Toolkit。
- 不保存 `UnityEngine.Camera`、`Transform`、GameObject、Prefab、GUID 或 `Assets/...` path。
- 只表达相机请求、profile、target snapshot、group framing、评估结果和诊断。
- 不参与 Combat / Gameplay authority，不改变 HP、移动、碰撞、命中或技能判定。
- 默认不进入 Runtime result hash、Replay hash 或 SaveState。项目若要复现观感，应记录 camera request 摘要作为表现输入。
- 不读取 Unity 当前 Camera transform 作为下一帧权威输入；上帧 `MxCameraState` 才是平滑和 blend 的输入。

`MxFramework.Camera.Unity` 是 Unity 应用层：

- 读取目标 `Transform` / `Renderer.bounds` / 自定义 target provider，转为 `MxCameraTargetSnapshot`。
- 在 `LateUpdate` 或受控 tick 后调用 `Camera.transform.SetPositionAndRotation(...)`。
- 设置 `fieldOfView`、`orthographicSize`、near / far clip、clear flags 等 Unity Camera 属性。
- 不把 Unity Camera 当前状态反写成 Gameplay / Combat 权威输入。
- 不在 backend 内私有维护 gameplay target lookup；目标绑定由 composition root 或 target binder 显式提供。

Cinemachine 不作为首版依赖。若未来项目需要，可做 `MxFramework.Camera.Cinemachine` 可选 backend，使用编译 symbol 保护。

运行时不变量：

- 同样 profile、request、target snapshot、delta time 和 previous state 必须得到同样 `MxCameraState`。
- 目标丢失不能静默使用世界原点；必须进入 diagnostics，并按 profile 的 target lost policy 处理。
- request 队列按 frame、priority、sequence 稳定排序。
- target snapshot 是本帧输入，不归 Camera core 持有；core 只可缓存 last valid snapshot summary 用于 grace / blend。
- 同一 rig 同一帧最多应用一个最终 evaluated state。
- shake / impulse / focus 只能影响表现 state，不改变 target snapshot。
- backend unavailable 时 core 仍能 evaluate，apply 失败只进入 backend diagnostics。
- `MxCameraProfileId`、`MxCameraRigId`、`MxCameraTargetRef` 必须是强语义 ID 或 wrapper，不能在实现中长期裸传 `string` / `int`。

## 模块结构

```text
MxFramework.Camera
  -> MxFramework.Core
  -> MxFramework.Runtime?       // 仅 CameraRuntimeModule 需要时引用

MxFramework.Camera.Unity
  -> MxFramework.Camera
  -> MxFramework.Runtime
  -> UnityEngine

MxFramework.Camera.Editor
  -> MxFramework.Camera
  -> UnityEditor

MxFramework.Camera.DebugUI
  -> MxFramework.Camera
  -> MxFramework.Diagnostics / DebugUI
```

推荐首版先落地：

- `MxFramework.Camera`：noEngine contracts、service、profile provider、target group solver、Null backend、diagnostics。
- `MxFramework.Camera.Unity`：Unity Camera rig、target binding、LateUpdate apply、scene demo helper。
- `MxFramework.Camera.DebugUI` 或放入既有 `DebugUI.Adapters`：只读 debug source。

## 核心数据契约

| 类型 | 用途 |
| --- | --- |
| `MxCameraProfileId` | 稳定 profile id，不等同 Unity Camera 名称。 |
| `MxCameraRigId` | 一个实际输出相机 / rig 的稳定 id。 |
| `MxCameraMode` | Follow、LookAt、GroupFollowPerspective、GroupFollowOrthographic、FixedShot、FreeLook、PhotoMode。 |
| `MxCameraProfileDefinition` | offset、distance、FOV/orthographic size、smoothing、bounds、group framing、shake limits。 |
| `MxCameraTargetRef` | actor/entity/socket 的稳定引用，不保存 `Transform`。 |
| `MxCameraTargetSnapshot` | 位置、朝向、速度、bounds、权重、有效性和 timestamp。 |
| `MxCameraTargetGroup` | 多目标集合配置：primary、secondary、权重、padding、target drop 策略。 |
| `MxCameraTargetGroupState` | 计算后的 center、bounds、radius、primary、valid target count。 |
| `MxCameraRequest` | SetProfile、BindTarget、SetGroup、Focus、Shake、Impulse、Zoom、Override。 |
| `MxCameraState` | 计算后的 position、rotation、FOV / orthographic size、shake offset、blend state。 |
| `MxCameraEvaluationContext` | 单次求值输入：frame、delta、viewport、previous state、profiles、snapshots、requests。 |
| `MxCameraEvaluationResult` | 单次求值输出：state、accepted / rejected requests、target group state、diagnostics。 |
| `MxCameraDebugSnapshot` | active profile、target/group、backend、recent requests/errors、framing summary。 |

### Profile 字段

`MxCameraProfileDefinition` 第一版字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `ProfileId` | `MxCameraProfileId` | 稳定 id。 |
| `Mode` | `MxCameraMode` | Follow / LookAt / GroupFollowPerspective / GroupFollowOrthographic 等。 |
| `Priority` | `int` | 多 profile request 冲突时的默认优先级。 |
| `LocalOffset` | `MxCameraVector3` | 目标局部偏移。 |
| `WorldOffset` | `MxCameraVector3` | 世界偏移。 |
| `Distance` / `MinDistance` / `MaxDistance` | `float` | 透视 follow 距离约束。 |
| `FieldOfView` / `MinFieldOfView` / `MaxFieldOfView` | `float` | 透视相机投影约束。 |
| `OrthographicSize` / `MinOrthographicSize` / `MaxOrthographicSize` | `float` | 正交相机投影约束。 |
| `PositionSmoothing` / `RotationSmoothing` / `ZoomSmoothing` | `float` | 显式 delta 驱动的平滑参数。 |
| `DeadZone` / `SoftZone` | `MxCameraFramingZone` | 屏幕空间或归一化 framing zone。 |
| `TargetPadding` | `float` | 多目标 bounds padding。 |
| `TargetLostGraceFrames` | `int` | 目标丢失后保持上帧目标状态的帧数。 |
| `BoundsPolicy` | `MxCameraBoundsPolicy` | NoClamp / ClampPosition / ClampTargetCenter。 |
| `ShakeLimit` | `float` | 单帧最大 shake offset，防止表现请求失控。 |
| `DiagnosticTags` | `string[]` | Debug / editor filter 用标签。 |

### Target 字段

`MxCameraTargetSnapshot` 第一版字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `TargetRef` | `MxCameraTargetRef` | entity / actor / socket 的稳定引用。 |
| `Position` | `MxCameraVector3` | 目标中心。 |
| `Forward` / `Up` | `MxCameraVector3` | 可选朝向，用于 look-at / facing basis。 |
| `Velocity` | `MxCameraVector3` | 平滑和预测用速度。 |
| `BoundsCenter` / `BoundsExtents` | `MxCameraVector3` | 多目标入镜 bounds。 |
| `Weight` | `float` | 多目标权重。 |
| `IsPrimary` | `bool` | 主目标标记。 |
| `IsValid` | `bool` | provider 是否认为本目标可用。 |
| `TimestampFrame` | `long` | snapshot 来源 frame，用于 stale 判断。 |

### Request 字段

`MxCameraRequest` 第一版字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `RequestId` | `ulong` | 组合根分配的稳定 request id。 |
| `Frame` | `long` | 目标 runtime frame。 |
| `Sequence` | `long` | 同帧稳定排序序号，由 request source 或 service 分配。 |
| `SourceId` | `string` | input / combat / animation / debug / cutscene 等来源。 |
| `Kind` | `MxCameraRequestKind` | SetProfile / BindTarget / SetGroup / Focus / Shake / Impulse / Zoom / ClearOverride。 |
| `Priority` | `int` | 同帧冲突排序。 |
| `TargetRef` | `MxCameraTargetRef` | 可选目标。 |
| `GroupId` | `MxCameraTargetGroupId` | 可选目标组。 |
| `ProfileId` | `MxCameraProfileId` | 可选 profile。 |
| `FloatValue` | `float` | zoom / intensity 等轻量参数。 |
| `DurationFrames` | `int` | shake / focus / override 持续帧。 |
| `ExpiresFrame` | `long` | 临时 override / shake 的过期帧；0 表示由 `DurationFrames` 推导。 |
| `PayloadKey` | `string` | 表现事件或调试命令的 payload key。 |
| `TraceId` | `string` | 调试链路 id。 |

冲突规则：

- 所有 request 先按 `Frame` 过滤，再按 `Priority` 降序、`Sequence` 升序、`RequestId` 升序排序。
- `SetProfile` 同帧多条时取最高 priority；同 priority 取最后 sequence，但记录 `CAM_REQUEST_CONFLICT`。
- `Shake` / `Impulse` 可叠加，但必须受 profile `ShakeLimit` 限制。
- `BindTarget` 和 `SetTargetGroup` 同帧冲突时，group 优先；单目标 follow 可看作一人 group。
- Debug override 优先级必须显式高于 gameplay / animation request，并进入 snapshot。
- 过期 request 必须从队列移除并进入 debug snapshot 计数，不允许无限保留。
- payload 非法的 request 被拒绝，不改变 active state。

### Evaluation Result 字段

`MxCameraEvaluationResult` 是后续实现的共同语言：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Status` | `MxCameraEvaluationStatus` | Success / SuccessWithDiagnostics / FallbackUsed / Failed。 |
| `Frame` | `long` | 本次求值 frame。 |
| `RigId` | `MxCameraRigId` | 输出 rig。 |
| `ActiveProfileId` | `MxCameraProfileId` | 求值后使用的 profile。 |
| `State` | `MxCameraState` | 可被 backend 应用的最终状态。 |
| `TargetGroupState` | `MxCameraTargetGroupState` | 本帧 group framing 摘要。 |
| `AcceptedRequestIds` | `ulong[]` | 被采纳或叠加的 request。 |
| `RejectedRequestIds` | `ulong[]` | 被拒绝、过期或 payload 非法的 request。 |
| `Diagnostics` | `MxCameraDiagnostic[]` | 稳定诊断列表。 |
| `DebugSummary` | `MxCameraDebugSummary` | 面向 Debug UI 的轻量摘要。 |

`MxCameraState` 至少包含：

- position、rotation 或 yaw / pitch / roll 数值。
- projection kind、field of view、orthographic size、near / far clip。
- focus center、view forward、view up。
- group radius、framing utilization、bounds clamp result。
- shake offset、impulse offset、active blend progress。
- state source：Normal / Grace / Fallback / DebugOverride。

## 服务草案

```csharp
public interface IMxCameraService
{
    MxCameraResult SetProfile(MxCameraProfileId profileId, MxCameraBlendOptions blend, string traceId = "");
    MxCameraResult BindTarget(MxCameraTargetRef target, string traceId = "");
    MxCameraResult SetTargetGroup(MxCameraTargetGroup group, string traceId = "");
    MxCameraResult EnqueueRequest(in MxCameraRequest request);
    MxCameraEvaluationResult Evaluate(in MxCameraEvaluationContext context);
    MxCameraDebugSnapshot CaptureSnapshot();
}
```

```csharp
public interface IMxCameraBackend
{
    MxCameraResult Initialize(IMxCameraProfileProvider profiles);
    MxCameraResult Apply(in MxCameraState state);
    MxCameraDebugSnapshot CaptureSnapshot();
    void Dispose();
}
```

`Evaluate` 只计算相机状态，不直接应用 Unity 对象。Unity 后端负责把 `MxCameraState` 落到实际 `Camera`。

`Evaluate` 输入必须包含：

- `RuntimeFrame` / delta time。
- viewport width / height 或 aspect。
- previous `MxCameraState`。
- 当前 profile set。
- 当前 target snapshot set。
- 本帧 camera requests。

失败语义：

| 场景 | 行为 |
| --- | --- |
| profile missing | 返回上一个可用 state 或 profile fallback，并记录 `CAM_PROFILE_MISSING`。 |
| target missing | 按 `TargetLostGraceFrames` 保持 last valid target；超过后进入 fallback state，记录 `CAM_TARGET_LOST`。 |
| group 无有效目标 | 返回 fallback state，记录 `CAM_GROUP_EMPTY`。 |
| viewport aspect 非法 | 使用 profile fallback aspect，记录 `CAM_INVALID_VIEWPORT`。 |
| backend apply 失败 | core evaluate 成功，backend snapshot 记录 `CAM_BACKEND_APPLY_FAILED`。 |
| request payload 非法 | 丢弃该 request，记录 `CAM_INVALID_REQUEST`。 |

### 求值时序

推荐首版运行时顺序：

```text
Runtime PreSimulation
Runtime Simulation
Gameplay / Combat / Character Control authority update
Unity presentation sync writes target transforms
Camera target binders collect snapshots
MxCameraService.Evaluate(context)
MxCameraUnityRig.ApplyLate(result.State)
Debug UI reads MxCameraDebugSnapshot
```

约束：

- `CameraRuntimeModule` 如果接入 `RuntimeHost`，只负责组织 request queue 与 noEngine 求值，不直接持有 Unity `Camera`。
- Unity backend 可以在 `LateUpdate` 中完成 snapshot collection、evaluate 和 apply，但同一 rig 必须保证顺序固定且每帧只 apply 一次。
- 若项目需要手动控制 tick，composition root 可以显式调用 `Evaluate`，再要求 `MxCameraUnityRig` 只执行 `ApplyLate`。
- pointer ray、screen-to-world 等 Unity 查询属于应用层能力；Camera core 只暴露 view basis / projection 摘要，不直接生成 Unity `Ray`。
- 多个 rig 同场景存在时，每个 rig 拥有自己的 request queue、target group 和 debug snapshot，不能共享可变全局 active camera state。

### 状态机

首版 active state 只需要四个稳定状态：

| 状态 | 进入条件 | 退出条件 |
| --- | --- | --- |
| `Uninitialized` | rig / service 尚未成功绑定 profile provider。 | 获得默认 profile 并首次成功 evaluate。 |
| `Normal` | profile、target、viewport 均有效。 | profile 缺失、target 丢失、debug override 或 backend unavailable。 |
| `Grace` | target 本帧缺失但未超过 `TargetLostGraceFrames`。 | target 恢复、超过 grace 或切换到 fallback。 |
| `Fallback` | 无有效 profile / target group / viewport 或 override 清理后无可用状态。 | 收到有效 profile + target snapshot 并通过 validation。 |

`DebugOverride` 不建议做成第五个长期状态；它应作为 request layer 覆盖 `Normal` / `Grace` 的输出，并在 snapshot 中显示覆盖来源和剩余帧数。

## 多目标入镜设计

多目标能力必须进入首版核心，而不是后续 Demo 特化逻辑。目标组算法：

1. 从 target provider 收集所有 `MxCameraTargetSnapshot`。
2. 按 `TimestampFrame` 判断 stale，按 `TargetRef` 稳定排序。
3. 过滤无效目标，并按 target lost policy 合并 last valid snapshot summary。
4. 按 `Weight` 和 primary target 策略计算 focus center。
5. 计算目标组 AABB / bounding sphere，并加入 `TargetPadding`。
6. 透视相机根据目标组半径、FOV、aspect、min/max distance 推导相机距离。
7. 正交相机根据目标组 bounds、aspect、padding、min/max orthographic size 推导 size。
8. 应用 dead zone、soft zone、center smoothing、zoom smoothing。
9. 目标数量变化或 target lost 时使用 grace frames 和 blend，避免瞬移。

上述编号表达逻辑顺序；实现时应保持 deterministic sort 和 explicit diagnostics，而不是依赖 provider 返回顺序。

首版模式：

- `GroupFollowPerspective`：3D 透视多目标，适合玩家 + 敌人 / Boss。
- `GroupFollowOrthographic`：2D、俯视、战棋或 Marble Maze 类场景。
- `PrimaryWithSecondaryTargets`：主角优先，但尽量保证 enemy / interact target 入镜。
- `CombatArena`：以玩家和 Boss / arena bounds 共同约束 framing。

首版不做真正 split screen；保留 `SplitScreenFallbackPolicy` 配置位，超过 `MaxTargetRadius` 时返回 `CAM_GROUP_BOUNDS_EXCEEDED`，再按 profile 选择 Clamp、FallbackToPrimary 或 FallbackProfile。

多目标求值伪流程：

```text
validTargets = filter snapshots by IsValid and freshness
if validTargets is empty:
  return fallback or last state with CAM_GROUP_EMPTY

weightedCenter = sum(target.center * target.weight) / sum(weight)
groupBounds = encapsulate(target.bounds + padding)

if perspective:
  distanceByVerticalFov = radius / tan(verticalFov / 2)
  distanceByHorizontalFov = radius / tan(horizontalFov / 2)
  desiredDistance = clamp(max(distanceByVerticalFov, distanceByHorizontalFov), minDistance, maxDistance)
  desiredPosition = weightedCenter - viewForward * desiredDistance + offsets

if orthographic:
  sizeByHeight = boundsHeight / 2
  sizeByWidth = boundsWidth / (2 * aspect)
  desiredSize = clamp(max(sizeByHeight, sizeByWidth) + padding, minSize, maxSize)

apply dead zone / soft zone
apply smoothing from previous state
apply shake / impulse offset
return MxCameraState
```

目标丢失语义：

- primary target 丢失时优先进入 `Grace`，使用 last valid primary snapshot 和本帧 secondary targets 继续求值。
- grace 内目标恢复，按 profile blend 回到 `Normal`，不触发 hard snap。
- grace 结束后仍缺 primary，则按 `PrimaryLostPolicy` 进入 fallback profile、切换到 secondary primary 或返回 failed result。
- secondary target 丢失只降低 valid count；除非 group 为空或低于 `MinValidTargetCount`，否则不进入全局 fallback。
- snapshot stale 的 frame 阈值必须配置化，默认只接受 current frame 或 previous frame。

坐标约定：

- noEngine core 使用右手/左手无关的简单 `MxCameraVector3` 数值，不暴露 Unity `Vector3`。
- Unity backend 负责把 Unity world transform 采样成相同坐标含义的 snapshot。
- yaw / pitch / roll 可以在 core 中用数值表达，但 Unity `Quaternion` 只出现在 backend。
- 后续如需固定点确定性，可以在 core 中新增 quantized state，不改变 Unity backend 边界。

## 自研功能范围

首版自研，不接 Cinemachine：

- 单目标 follow / look-at。
- 多目标 group framing。
- 透视 distance / FOV zoom。
- 正交 size zoom。
- local / world offset。
- position / rotation / zoom smoothing。
- dead zone / soft zone。
- target bounds / camera bounds。
- profile blend。
- camera shake / impulse。
- lock-on focus。
- target lost grace frames。
- Debug snapshot。

后置能力：

- 分屏和多输出 camera stack。
- 遮挡检测、自动避障和 collision volume。
- Timeline / Cutscene editor。
- Photo mode 完整工具。
- SRP / URP camera stack 深度接入。
- Cinemachine 可选 backend。

## Unity 应用路径

```text
Unity Transform / Bounds providers
  -> MxCameraTargetSnapshot[]
  -> MxCameraService.Evaluate()
  -> MxCameraEvaluationResult
  -> MxCameraUnityRig.ApplyLate(result.State)
  -> UnityEngine.Camera transform / FOV / orthographicSize
```

`MxCameraUnityRig` 负责：

- 序列化绑定实际 `Camera` 和 pivot transform。
- 采样 configured target binders。
- 调用 `IMxCameraService.Evaluate`。
- 在 `LateUpdate` 应用 `MxCameraState`。
- 输出 backend diagnostics。

场景生成器可以创建默认 `Main Camera + MxCameraUnityRig`，但新增场景 / prefab / ScriptableObject 必须通过 Unity Editor / MCP / Editor 菜单生成，不手写 YAML。

Unity backend 验收重点：

- `MxCameraUnityRig` 不在 `Update` 中应用最终 camera state，默认在 `LateUpdate` 或受控 apply point。
- target binder 的 Transform / Renderer 引用只存在 Unity assembly。
- 同一场景允许多个 rig，但每个 rig 必须有稳定 `MxCameraRigId`。
- 缺少 Camera、target binder、profile provider 时不抛未处理异常，进入 diagnostics。
- Editor / PlayMode 测试应检查 transform、FOV、orthographic size，而不是只检查无异常。

## 和 Character Control 的关系

Camera 可以提供辅助 resolver：

```text
MxCameraState / view yaw
  -> MxCameraFacingBasisResolver
  -> CharacterFacingBasis
  -> CharacterCommand
```

规则：

- `MxFramework.CharacterControl` core 不引用 Camera。
- 组合根负责把相机 view basis 注入 `InputCharacterCommandSource` 或其它 command source。
- 相机不能从 Character Control 读取私有字段，只消费公开 motion result / target snapshot。

`MxCameraFacingBasisResolver` 只做数值转换：

```text
cameraForward projected on movement plane
cameraRight projected on movement plane
input move vector
  -> CharacterFacingBasis
```

它不读取设备输入、不修改 `CharacterCommand`，只给组合根提供标准 helper，避免每个 Demo 自己写一份 yaw / forward 计算。

## 表现事件桥

Animation / Combat 的表现事件可以映射到 Camera request：

```text
MxAnimationPresentationEvent(EventKind = "Camera", PayloadKey = camera.shake.heavy)
  -> CameraPresentationEventSink
  -> MxCameraRequest.Shake(...)
```

映射规则：

- event id / payload key 指向 camera effect id 或 inline profile。
- dedupe 复用 animation presentation event 的 dedupe key。
- missing payload、invalid effect、backend unavailable 只进 diagnostics，不影响 Combat / Gameplay authority。
- `CatchUpSafe` 事件是否补播由 Animation / network presentation sync 规则决定；Camera sink 不自行补放历史 one-shot shake。
- Camera sink 输出 request 时必须带 source id、dedupe key 或 trace id，方便 Debug UI 反查来源。

首版只定义 sink 边界，不要求 Combat / Animation 直接依赖 Camera。推荐位置是独立 adapter assembly，例如 `MxFramework.Camera.PresentationBridge` 或放在项目 / Demo 组合根中。

## Diagnostics

稳定诊断 code 第一版：

| Code | Severity | 场景 |
| --- | --- | --- |
| `CAM_PROFILE_MISSING` | Error | request 或 active state 引用缺失 profile。 |
| `CAM_INVALID_PROFILE` | Error | profile 数值非法，例如 min > max。 |
| `CAM_TARGET_LOST` | Warning | 目标丢失且仍在 grace / fallback。 |
| `CAM_GROUP_EMPTY` | Error | target group 没有有效目标。 |
| `CAM_GROUP_BOUNDS_EXCEEDED` | Warning | 多目标半径超过 profile 限制。 |
| `CAM_INVALID_REQUEST` | Warning | request payload 无效，被丢弃。 |
| `CAM_REQUEST_CONFLICT` | Warning | 同帧同优先级 request 互相覆盖。 |
| `CAM_INVALID_VIEWPORT` | Error | viewport width / height / aspect 非法。 |
| `CAM_EVENT_PAYLOAD_MISSING` | Warning | presentation event 指向的 camera payload 不存在。 |
| `CAM_BACKEND_UNAVAILABLE` | Error | Unity rig 没有可用 backend 或 Camera。 |
| `CAM_BACKEND_APPLY_FAILED` | Error | backend 应用 state 失败。 |
| `CAM_DEBUG_OVERRIDE_ACTIVE` | Info | Debug / editor override 正在覆盖常规请求。 |

诊断结构必须包含：

- `Code`、`Severity`、`Frame`、`RigId`。
- `SourceId` / `TraceId` / `RequestId`，若诊断来自 request。
- `ProfileId`、`TargetRef` 或 `GroupId`，若诊断来自配置或目标。
- 面向人的 `Message` 只用于展示，测试和工具不得依赖 message 文本。

`MxCameraDebugSnapshot` 至少包含：

- rig id、active profile、mode、backend id。
- target group id、valid target count、primary target、lost target count。
- evaluated position / rotation summary、FOV / orthographic size。
- group center、bounds、radius、padding。
- active blend、blend remaining frames。
- shake queue count、current shake offset。
- recent diagnostics，按 frame / sequence 稳定排序。

## 配置和资源

首版 profile 可以先用纯 C# definition 或 JSON / ConfigTable adapter 表达，不要求 ScriptableObject。若后续提供 Editor authoring：

- ScriptableObject 只能是 Unity authoring adapter，不是 noEngine source of truth。
- 导出到 runtime 时必须转成 `MxCameraProfileDefinition`。
- profile 不保存 Unity asset GUID、Camera object、Transform path 或 prefab 引用。

Profile validation 第一版必须覆盖：

- `MinDistance <= Distance <= MaxDistance`。
- `MinFieldOfView <= FieldOfView <= MaxFieldOfView`。
- `MinOrthographicSize <= OrthographicSize <= MaxOrthographicSize`。
- smoothing 参数非负，且不能产生除零。
- `TargetLostGraceFrames >= 0`。
- `ShakeLimit >= 0`。
- group 模式必须配置 target padding、min / max target radius 和 fallback policy。
- fixed shot / photo mode 不能要求 target group。

资源策略：

- Camera profile 本身不声明模型、材质、Prefab 等资源。
- 若 camera effect 需要曲线、噪声 profile 或 payload 表，必须通过 `ResourceKey` / config id 引用，不直接保存 Unity object。
- Runtime demo 可以内嵌少量 C# profile definition；正式 authoring 工具应能导出同样 DTO。

## 测试策略

noEngine tests：

- single target follow 输出稳定 position / look-at。
- group perspective 根据 bounds 和 aspect 推导 distance。
- group orthographic 根据 bounds 和 aspect 推导 orthographic size。
- target lost grace frames 和 fallback。
- profile blend deterministic。
- shake clamp 和叠加顺序。
- request priority / sequence 冲突。
- diagnostics code 稳定。

Unity PlayMode tests：

- `MxCameraUnityRig` 在 LateUpdate 后应用 position / rotation / FOV。
- orthographic rig 应用 orthographic size。
- Transform target binder 采样位置和 Renderer bounds。
- missing Camera / target / profile 时输出 diagnostics。
- 一个迁移 Demo 不再直接用 `Camera.main` 驱动核心相机行为。

## 里程碑拆分

建议创建 Gitea milestone：`Phase 14: Camera Management`。

| Slice | 建议 Issue | 交付 |
| --- | --- | --- |
| Camera design contract | 本 Issue | 本设计文档、ROADMAP 入口、implementation slices。 |
| Camera core contracts | 后续 S2 | `MxFramework.Camera` noEngine profiles、requests、service、target group solver、Null backend、tests。验收：不引用 Unity / Input / UI；tests 覆盖单目标、多目标、lost target、blend、shake、diagnostics。 |
| Unity backend MVP | 后续 S2 | `MxFramework.Camera.Unity` rig、target binding、LateUpdate apply、single / group follow PlayMode 验证。验收：PlayMode 断言 transform / FOV / orthographic size。 |
| Demo migration | 后续 S2/S3 | 选择 `RuntimeCombatShowcase` 或另一个现有 Demo 替换散落相机逻辑，验证多目标入镜和 input facing basis。 |
| Camera presentation events | 后续 S2 | Animation / Combat presentation event sink -> camera shake / focus request。验收：dedupe、missing payload、backend unavailable diagnostics。 |
| Debug UI diagnostics | 后续 S1/S2 | Camera debug source，展示 active profile、targets、group framing、shake queue、recent errors。 |
| Editor authoring MVP | 后续 S2/S3 | Profile authoring / validation window，避免手写 Unity 序列化资产。 |

建议后续 Issue 标题：

1. `[Camera] 02：noEngine Core Contracts 与 Target Group Solver`
2. `[Camera] 03：Unity Backend MVP 与 LateUpdate Apply`
3. `[Camera] 04：Demo Camera Migration`
4. `[Camera] 05：Presentation Event Bridge`
5. `[Camera] 06：Debug UI Diagnostics`
6. `[Camera] 07：Profile Authoring MVP`

### 后续 Issue 细化

`[Camera] 02：noEngine Core Contracts 与 Target Group Solver`

- 范围：新增 noEngine assembly、typed ids、profile/request/target/state DTO、evaluation result、diagnostics、service、target group solver、Null backend。
- 不做：Unity Camera、Transform binder、Debug UI 可视化、Demo migration。
- 验收：无 UnityEngine / UnityEditor / Cinemachine / Input System / UI Toolkit 引用；EditMode tests 覆盖单目标、多目标、target lost、profile validation、request priority、shake clamp 和 diagnostics code。

`[Camera] 03：Unity Backend MVP 与 LateUpdate Apply`

- 范围：新增 Unity assembly、`MxCameraUnityRig`、Transform / Renderer target binder、LateUpdate apply、profile provider adapter。
- 不做：迁移所有 Demo、Editor authoring、Cinemachine。
- 验收：PlayMode tests 断言 position、rotation、FOV、orthographic size、missing backend diagnostics；同一 rig 每帧只 apply 一次。

`[Camera] 04：Demo Camera Migration`

- 范围：优先迁移 `RuntimeCombatShowcaseInputController` 的 orbit / follow 路径，或选择一个同等复杂度的 Runtime Showcase。
- 不做：重写 Demo 战斗逻辑、重写 Input 系统、做完整 camera collision。
- 验收：Demo 不再用散落 `Camera.main` / 手写 orbit 作为核心相机行为；Character facing basis 由相机 view basis helper 注入；保留 pointer ray 的 Unity 应用层查询。

`[Camera] 05：Presentation Event Bridge`

- 范围：从 Animation / Combat presentation event adapter 输出 camera shake / focus / impulse request。
- 不做：Combat / Animation core 直接依赖 Camera。
- 验收：dedupe 生效；missing payload 输出 `CAM_EVENT_PAYLOAD_MISSING`；backend unavailable 不影响 Combat / Gameplay authority。

`[Camera] 06：Debug UI Diagnostics`

- 范围：Camera debug source，展示 active profile、target group、framing bounds、shake queue、recent diagnostics。
- 不做：可写调参工具。
- 验收：Debug UI 能定位 target lost、profile missing、request conflict、backend unavailable；只读 snapshot 不进入 Runtime hash。

`[Camera] 07：Profile Authoring MVP`

- 范围：profile authoring / validation window 或导入器，把 authoring 数据导出为 `MxCameraProfileDefinition`。
- 不做：Cinemachine authoring、Timeline 工具、手写 Unity 序列化资产。
- 验收：能编辑 profile 字段、运行 validation、导出 runtime DTO；Unity ScriptableObject 只是 adapter，不是 noEngine source of truth。

## 完成定义

设计 Issue 完成条件：

- 本文明确核心 noEngine 与 Unity backend 的职责边界。
- 本文明确不引入 Cinemachine 硬依赖。
- 多目标入镜作为首版核心能力进入数据结构和算法。
- 里程碑和后续 implementation slices 明确。
- ROADMAP 已有 Phase 14 入口。

后续实现总体验收：

- `MxFramework.Camera` 无 UnityEngine / UnityEditor / Cinemachine / Input System 引用。
- noEngine tests 覆盖 single target、group target、lost target、blend、shake、zoom 和 diagnostics。
- Unity PlayMode 覆盖实际 Camera 应用、LateUpdate 顺序和多目标入镜。
- 至少一个 Demo 使用 `MxCameraUnityRig` 代替散落 `Camera.main` / 手写 orbit。
- Debug snapshot 能定位 target lost、profile missing、bounds exceeded、backend unavailable。
- 相机状态不进入 Gameplay / Combat authority、Runtime hash 或 SaveState 默认路径。
