# Character Locomotion Blend Calibration Lab 00：开发方案

> Status: Development Plan
> Task level: S2
> Delivery level: Playable / Runtime Showcase Plan
> 日期：2026-05-22

## Summary

建立一个专门用于调试角色 locomotion BlendTree 的运行时手测环境，重点不是泛泛比较“角色速度”和“动画速度”，而是验证：

```text
角色控制器的地面移动速度 / 方向
  是否和
BlendTree 当前混合出来的脚步运动速度 / 接地表现
  匹配
```

最终目标是能在 Play Mode 中直接观察和定位 foot sliding：

- 不同方向移动动画的 `nativeVelocity` 是否正确。
- 每个 clip 的 `playbackSpeed` 是否和目标移动速度匹配。
- 每个方向 clip 在脚掌接地阶段表达的地面速度是否能匹配角色实际位移速度。
- 2D BlendTree 当前权重是否合理。
- 左脚 / 右脚接地阶段是否被正确识别。
- 脚接触地面时世界空间滑动速度是否超过阈值。
- 运行时 controller 输出范围是否能到达 BlendTree 中的 walk / run / strafe 点。

本任务先冻结方案和任务切分。后续实现应从新的 Gitea Issue / milestone 开始。

## Problem Statement

当前已有 `CharacterRuntimeLocomotionBlendController`、`UnityPlayablesAnimationBackend`、animation warmup 和运行时 debug snapshot，但它们只能说明：

- 后端是否存在。
- 当前请求是否成功。
- 当前 BlendTree 权重是什么。
- 动画资源是否预热成功。

这些还不能回答最关键的问题：

```text
为什么看起来脚在滑？
是 BlendTree 点不可达？
是方向动画 native speed 配错？
是 playback speed 没随角色速度调？
是脚接地窗口不准？
是骨骼/Avatar/retarget 导致脚底轨迹不对？
```

当前 Iron Vanguard 样例中，`blend.move2d` 已有：

| clip | point |
| --- | --- |
| idle | `(0, 0)` |
| walk_f | `(0, 1)` |
| run_f | `(0, 2)` |
| walk_r | `(1, 0)` |
| walk_l | `(-1, 0)` |
| walk_b | `(0, -1)` |

因此手测环境必须明确显示 controller 输出的 blend domain。如果 controller 最终只输出 `[-1, 1]`，`run_f` 的 `(0, 2)` 就不可达；如果 controller 能输出到 `2`，还要继续验证 run clip 脚步速度和角色位移是否匹配。

关键判定不是“模型看起来在播放动画”或“角色速度大致正确”，而是：

```text
当某只脚处于 planted/contact 阶段时，
脚底世界空间水平位移应该接近 0；
如果角色实际位移速度和当前混合动画表达的脚步速度不匹配，
接地脚就会相对地面滑动。
```

所以 Lab 必须同时显示“实际运动速度”和“动画表达速度”，并把差值落到脚底接地点的可见指标上。

## Product Goal

新增一个运行时校准场景：

```text
Assets/Scenes/MxFramework/CharacterLocomotionCalibration.unity
```

它是一个 Play Mode 可用的手测和开发诊断环境：

- 通过正常资源加载链路实例化角色，不直接把最终 prefab 当作场景常驻角色使用。
- 可手动控制方向、速度、walk/run、暂停、慢动作、单帧。
- 可按 preset 运行标准测试：Idle、Walk Forward、Run Forward、Walk Back、Strafe Left、Strafe Right、Diagonal、Speed Ramp。
- 实时显示 BlendTree 权重、clip playback speed、native velocity、controller target velocity、actual velocity。
- 实时显示左右脚接地状态、脚底锁定点、脚滑速度和脚滑距离。
- 支持一键生成校准报告，供 Issue / PR / 调试面板复制。

## Non-goals

- 不把 root motion 变成权威位移。角色控制器仍是权威移动来源。
- 不让 runtime authority 依赖 Unity Animator / PlayableGraph 的当前骨骼姿态。
- 不在 `MxFramework.Animation` noEngine 层引用 `UnityEngine`。
- 不替代 Animation Editor。Animation Editor 负责编辑 clip / blend / timeline / calibration metadata；Calibration Lab 负责运行时验证和调参观察。
- 不把脚滑检测结果写入 Replay hash / SaveState。
- 不解决完整 IK、foot locking runtime 修正或 Motion Matching。Lab 只先做观测、诊断和校准依据。

## API 复用计划

| 需求点 | 优先使用的框架 API / 模块 | 本次使用方式 | 不使用时的原因 |
| --- | --- | --- | --- |
| 游戏主循环 / 固定帧 | `RuntimeHost`、`RuntimeFrame` | Calibration runner 用显式采样帧和可暂停/单步时间控制；若首版只作为 Unity scene probe，也要把采样 tick 独立封装。 | 不直接把权威采样逻辑散在多个 `Update()`。 |
| 玩家输入 / 手测指令 | `InputSnapshot`、现有 Input bridge / Unity input adapter | WASD、Shift、暂停、单步和 preset 转换为 calibration command。 | 不在核心采样器中直接读键盘。 |
| 资源加载 / 预热 | `ResourceManager`、`ResourceCatalog`、`ResourcePreloadService`、`CharacterRuntimeResourceBootstrap` | 角色、默认武器、animation set、clip registry 和 runtime catalog 走正常资源链路。 | 禁止直接把 AnimationClip / prefab 临时拖进场景绕过 runtime。 |
| 动画播放 | `UnityPlayablesAnimationBackend`、`MxAnimationBlend2DRequest`、`MxAnimationDiagnosticSnapshot` | 使用现有 backend 播放和输出权重；必要时扩展 diagnostics 暴露 clip normalized time / slot time。 | 不新建平行 Playables 后端。 |
| 移动 / 碰撞 | `CharacterRuntimeInputMotionController`、Combat motion / physics 能力 | 使用实际角色控制器输出 actual velocity 和 grounded 状态。 | 不用 Transform 直接平移冒充角色移动。 |
| 运行时 UI | UI Toolkit、`MxFramework.UI.Toolkit` 控件 | 新建专用 HUD，使用 `MxStatusBadge`、`MxCommandButton`、`MxPanelTabs`、`MxStatBar` 等。 | 不继续把复杂校准 UI 塞进通用 Debug UI。 |
| 诊断 / 调试快照 | `IFrameworkDebugSource`、`FrameworkDebugSnapshot` | Lab 可以同时注册 debug source，但专用 HUD 直接展示校准模型。 | Debug UI 作为辅助，不作为主要交互界面。 |
| 配置 / 数据 | Animation authoring、compiled artifacts、ResourceSelectionRef | clip calibration metadata 从 animation authoring 编译进入 runtime 可读 artifact。 | 不把校准数据只存在 Inspector 字段里。 |

## Core Concepts

### 1. Controller Velocity

角色控制器输出两组速度：

| 字段 | 含义 |
| --- | --- |
| `targetWorldVelocity` | 当前输入和角色属性期望的世界空间速度。 |
| `actualWorldVelocity` | 角色实例实际位移速度，由位置差 / 时间采样得出。 |
| `actualLocalVelocity` | 转到角色本地坐标后的速度，用于和方向动画比较。 |
| `grounded` | 角色是否接地；空中状态不做 foot sliding 判定。 |

### 2. Blend Sample

每帧记录当前动画混合：

| 字段 | 含义 |
| --- | --- |
| `blendId` | 例如 `blend.move2d`。 |
| `blendX` / `blendY` | controller 发送给 backend 的量化参数。 |
| `clipWeights` | 每个 clip 的当前权重。 |
| `dominantClipId` | 最大权重 clip。 |
| `reachablePoints` | controller 当前输出范围能覆盖的 BlendTree 点。 |
| `unreachablePoints` | 配置存在但 controller 无法到达的点。 |

`run_f (0, 2)` 是否可达必须在 UI 中直接显示。

### 3. Native Locomotion Velocity

每个 locomotion clip 需要声明“该动画自然播放时脚步表达的本地位移速度”：

```json
{
  "clipId": "walk_f",
  "nativeVelocity": { "x": 0.0, "y": 1.6 },
  "cycleDurationSeconds": 0.75,
  "playbackSpeed": 1.0
}
```

方向动画必须使用向量，不使用单个 speed：

| clip | nativeVelocity |
| --- | --- |
| walk_f | `(0, +walkSpeed)` |
| run_f | `(0, +runSpeed)` |
| walk_b | `(0, -backSpeed)` |
| walk_l | `(-strafeSpeed, 0)` |
| walk_r | `(+strafeSpeed, 0)` |

当前混合理论速度：

```text
blendedNativeVelocity =
  sum(clip.nativeVelocity * clip.weight * clip.playbackSpeed)
```

这不是最终判定，但它能快速说明“当前播放组合是否理论上能匹配控制器速度”。

### 4. Speed Matching Model

速度匹配按三个层级判断：

| 层级 | 判定 | 作用 |
| --- | --- | --- |
| Blend domain | controller 输出的 `blendX/blendY` 是否能到达配置点 | 先排除 `run_f (0,2)` 这类不可达问题。 |
| Velocity estimate | `actualLocalVelocity` 是否接近 `blendedNativeVelocity` | 判断 clip native speed / playback speed 是否大致合理。 |
| Foot plant truth | planted foot 是否相对地面滑动 | 最终判定脚滑，避免只靠速度估算误判。 |

推荐误差计算：

```text
velocityError =
  actualLocalVelocity - blendedNativeVelocity

velocityErrorRatio =
  length(velocityError) / max(length(actualLocalVelocity), epsilon)
```

UI 不应只显示平均值，还要按方向拆开：

| 字段 | 含义 |
| --- | --- |
| `forwardError` | 前后方向速度误差，用于 walk/run/back。 |
| `strafeError` | 左右方向速度误差，用于 strafe。 |
| `directionErrorDegrees` | 实际位移方向和动画表达方向夹角。 |
| `speedScaleSuggestion` | 根据当前 clip dominant weight 推导的建议 playback speed。 |

建议播放速度调参公式：

```text
suggestedPlaybackSpeed =
  actualSpeedAlongClipDirection / clip.nativeSpeedAlongClipDirection
```

例如 `walk_f` 原生脚步速度是 `1.4m/s`，当前角色实际前进速度是 `1.8m/s`，则 `walk_f` 的建议 `playbackSpeed` 约为 `1.29`。这只是建议值，最终仍以 planted foot slip 指标为准。

### 5. Foot Contact Window

每个 locomotion clip 需要声明左右脚接地区间：

```json
{
  "clipId": "walk_f",
  "leftFootContacts": [
    { "startNormalized": 0.10, "endNormalized": 0.38 }
  ],
  "rightFootContacts": [
    { "startNormalized": 0.58, "endNormalized": 0.86 }
  ]
}
```

BlendTree 混合时，每只脚的接地置信度按权重混合：

```text
leftContactConfidence =
  sum(clip.weight * IsLeftFootInContactWindow(clip.normalizedTime))
```

当 `contactConfidence >= threshold` 且角色 grounded 时，该脚进入 planted 状态。

### 6. Foot Sliding Metric

脚进入 planted 状态时记录脚底世界空间 anchor。接地期间持续测量脚底水平位移：

```text
footSlipDistanceCm =
  HorizontalDistance(currentFootWorldPosition, plantedAnchor) * 100

footSlipSpeedCmPerSec =
  HorizontalSpeed(currentFootWorldPosition, previousFootWorldPosition) * 100
```

判定建议：

| 等级 | 条件 |
| --- | --- |
| OK | `avgSlipSpeed <= 3 cm/s` 且 `maxSlipDistance <= 3 cm` |
| WARN | `avgSlipSpeed <= 8 cm/s` 或 `maxSlipDistance <= 8 cm` |
| BAD | 超过 WARN 阈值 |

阈值需要在 Lab 中可配置。

### 7. Calibration Workflow

实际手调流程应固定为：

1. 先跑 `Idle`，确认骨骼、脚底采样点和接地阈值没有基础错误。
2. 单独跑 `Walk Forward / Back / Strafe Left / Strafe Right`，分别校准每个方向 clip 的 `nativeVelocity` 和 `playbackSpeed`。
3. 跑 `Run Forward`，确认 controller 能到达 run blend point；如果不可达，先修 blend domain，不调 clip。
4. 跑 `Diagonal`，观察混合方向的 `directionErrorDegrees` 和接地脚滑峰值。
5. 跑 `Speed Ramp`，检查 walk -> run 过渡区是否脚滑，而不是只看端点。
6. 保存 JSON 报告，作为 animation authoring metadata 或 controller mapping 改动的验收证据。

Lab 只报告和建议，不在运行时自动改写配置；配置修改应回到 Animation Editor / Authoring Compiler 链路。

## Data Contracts

### `MxAnimationLocomotionClipCalibration`

建议放在 `MxFramework.Animation` noEngine 层，使用 primitive / framework math 类型，不引用 Unity。

```csharp
public sealed class MxAnimationLocomotionClipCalibration
{
    public string ClipId { get; }
    public ResourceKey ClipKey { get; }
    public float NativeVelocityX { get; }
    public float NativeVelocityY { get; }
    public float PlaybackSpeed { get; }
    public float CycleDurationSeconds { get; }
    public IReadOnlyList<MxAnimationFootContactWindow> LeftFootContacts { get; }
    public IReadOnlyList<MxAnimationFootContactWindow> RightFootContacts { get; }
}
```

### `MxAnimationFootContactWindow`

```csharp
public readonly struct MxAnimationFootContactWindow
{
    public float StartNormalized { get; }
    public float EndNormalized { get; }
    public float Confidence { get; }
}
```

### `MxAnimationBlendReachabilityReport`

```csharp
public sealed class MxAnimationBlendReachabilityReport
{
    public string BlendId { get; }
    public float ControllerMinX { get; }
    public float ControllerMaxX { get; }
    public float ControllerMinY { get; }
    public float ControllerMaxY { get; }
    public IReadOnlyList<string> ReachableClipIds { get; }
    public IReadOnlyList<string> UnreachableClipIds { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}
```

该报告用于明确显示类似：

```text
run_f unreachable: point=(0,2), controllerYRange=[-1,1]
```

### `CharacterLocomotionCalibrationFrame`

```csharp
public sealed class CharacterLocomotionCalibrationFrame
{
    public long Frame { get; }
    public float DeltaTime { get; }
    public float TargetLocalVelocityX { get; }
    public float TargetLocalVelocityY { get; }
    public float ActualLocalVelocityX { get; }
    public float ActualLocalVelocityY { get; }
    public float BlendedNativeVelocityX { get; }
    public float BlendedNativeVelocityY { get; }
    public float VelocityErrorRatio { get; }
    public float DirectionErrorDegrees { get; }
    public string DominantClipId { get; }
    public float LeftFootContactConfidence { get; }
    public float RightFootContactConfidence { get; }
    public float LeftFootSlipCmPerSec { get; }
    public float RightFootSlipCmPerSec { get; }
    public float MaxSlipDistanceCm { get; }
}
```

Unity adapter 可以把 Unity `Vector2/Vector3` 转换成 primitive DTO，不让 noEngine contract 依赖 Unity 类型。

## Runtime Architecture

推荐结构：

```text
Assets/Scripts/MxFramework/Character.LocomotionCalibration/
  noEngine or minimal runtime contracts
  - calibration frame
  - slip metrics
  - reachability report
  - report formatter

Assets/Scripts/MxFramework/Character.LocomotionCalibration.Unity/
  Unity runtime adapters
  - CharacterLocomotionCalibrationRunner
  - CharacterLocomotionCalibrationSampler
  - CharacterLocomotionCalibrationHudController
  - CharacterLocomotionCalibrationSceneGizmos

Assets/UI/MxFramework/CharacterLocomotionCalibration/
  - CharacterLocomotionCalibration.uxml
  - CharacterLocomotionCalibration.uss
  - CharacterLocomotionCalibrationPanelSettings.asset

Assets/Scenes/MxFramework/
  - CharacterLocomotionCalibration.unity
```

依赖方向：

```text
MxFramework.Animation <- Character.LocomotionCalibration
MxFramework.Resources <- Character.LocomotionCalibration.Unity
MxFramework.Character.RuntimeSpawn.Unity <- Character.LocomotionCalibration.Unity
MxFramework.UI.Toolkit <- Character.LocomotionCalibration.Unity
```

`MxFramework.Animation` 不依赖 Calibration Lab；如果新增通用 calibration DTO，应保持 noEngine。

## Runtime Flow

```text
Open CharacterLocomotionCalibration.unity
  -> CharacterLocomotionCalibrationRunner.Start
  -> CharacterRuntimeResourceBootstrap.LoadCharacter
  -> ResourceManager loads character prefab, default weapons, animation clips
  -> UnityPlayablesAnimationBackend created by runtime bootstrap
  -> CalibrationSampler resolves Animator, foot bones, locomotion controller
  -> HUD sends preset / speed / direction commands
  -> Controller moves character
  -> Animation backend receives SetBlend2D
  -> Sampler records velocity, weights, contact, slip
  -> HUD and scene gizmos update
  -> Optional report export
```

## UI Design

该 Lab 使用专用 UI Toolkit HUD，不复用通用 Debug UI 作为主界面。

### Layout

```text
┌─────────────────────────────────────────────────────────────┐
│ Header: Character / Animation Set / Warmup / Backend Status │
├───────────────┬─────────────────────────────┬───────────────┤
│ Controls      │ 3D Scene / Track / Footmark │ Telemetry     │
│ - Direction   │ - 1m / 5m grid              │ - Velocity    │
│ - Speed       │ - target arrow              │ - Blend       │
│ - Presets     │ - foot anchors              │ - Foot Slip   │
│ - Time        │ - red slip trails           │ - Warnings    │
├───────────────┴─────────────────────────────┴───────────────┤
│ Bottom: BlendTree 2D map + clip weights + report log        │
└─────────────────────────────────────────────────────────────┘
```

### 必须显示

- `warmupSuccess`
- `backendGraphValid`
- `blendId`
- `blendX / blendY`
- `reachable / unreachable points`
- `targetLocalVelocity`
- `actualLocalVelocity`
- `blendedNativeVelocity`
- `velocityError`
- `velocityErrorRatio`
- `directionErrorDegrees`
- `suggestedPlaybackSpeed`
- `dominantClip`
- `clip weights`
- `leftFootContact / rightFootContact`
- `leftFootSlipCmPerSec / rightFootSlipCmPerSec`
- `maxSlipDistanceCm`
- `reportStatus: OK / WARN / BAD`

### Controls

- Direction pad：前 / 后 / 左 / 右 / 斜向。
- Speed slider：0 到当前角色最大速度。
- Walk / Run mode：可以切换 controller speed band 或直接设置目标 speed。
- Presets：
  - Idle
  - Walk Forward
  - Run Forward
  - Walk Back
  - Strafe Left
  - Strafe Right
  - Diagonal Forward Right
  - Speed Ramp 0 -> max
- Time：
  - Pause
  - Step Frame
  - Slow Motion 0.25x / 0.5x / 1x
- Report：
  - Copy Summary
  - Save JSON Report

## Scene Visualization

场景应包含：

- 1 米地面刻度和 5 米大刻度。
- 起点线和当前角色投影点。
- 目标速度方向箭头。
- actual velocity 方向箭头。
- 左右脚骨骼采样点。
- planted foot anchor。
- 接地期间脚底轨迹。
- 脚滑超过阈值时轨迹标红。

这些表现只服务调试，不进入 runtime authority。

## Implementation Slices

### Slice 01：Calibration Contract and Report

目标：

- 定义 clip calibration metadata。
- 定义 per-frame calibration sample。
- 定义 reachability report。
- 定义 report formatter。

验收：

- noEngine EditMode tests 覆盖：
  - native velocity 混合计算。
  - contact window 归一化判断。
  - OK / WARN / BAD 阈值分类。
  - unreachable blend point 诊断。

### Slice 02：Runtime Scene and Resource Loading

目标：

- 新建 `CharacterLocomotionCalibration.unity`。
- 使用 `CharacterRuntimeResourceBootstrap` 正常加载 Iron Vanguard。
- 不直接在场景中放最终角色实例。

验收：

- 打开场景按 Play 后自动加载角色。
- Console 无新增 error。
- HUD 显示 `warmupSuccess=true`、`backendGraphValid=true`。
- default weapon 和 animation resources 仍走 ResourceManager。

### Slice 03：BlendTree Reachability and Weight Probe

目标：

- 展示 controller 输出的 blend domain。
- 展示 BlendTree 点是否可达。
- 展示实时 clip weight。

验收：

- 当前 `blend.move2d` 的所有点显示在 2D map 中。
- 若 controller 只输出 `[-1,1]`，`run_f (0,2)` 显示为 unreachable。
- 切换方向时对应 clip 权重可见变化。

### Slice 04：Locomotion Clip Calibration Metadata

目标：

- 在 animation authoring 中补充 locomotion calibration metadata。
- Compiler 输出 runtime 可读 calibration artifact。
- Animation Editor 后续可编辑这些字段。

验收：

- 每个 locomotion clip 可声明 `nativeVelocity`、`cycleDurationSeconds`、`footContactWindows`。
- 缺失 metadata 时 Lab 显示 warning，而不是静默通过。
- metadata hash 进入 validation report 或 diagnostics。

### Slice 05：Foot Bone Sampling and Slip Metrics

目标：

- 从 Animator Humanoid bone 或配置骨骼路径采样左右脚位置。
- 根据 contact window 和 clip normalized time 判断 planted 状态。
- 计算 foot slip speed / distance。

验收：

- Idle 状态脚滑接近 0。
- Walk / run 时接地脚 anchor 可见。
- 强行提高 / 降低 playback speed 时能观察到 foot sliding 指标变化。
- 改变 controller speed 但不改 playback speed 时，能观察到速度误差和脚滑上升。

### Slice 06：Calibration HUD and Scene Gizmos

目标：

- 实现专用 UI Toolkit HUD。
- 实现地面轨迹、foot anchor、velocity arrow、BlendTree map。

验收：

- UI 可读，不遮挡角色主体观察区域。
- 关键数值无需打开 Inspector 即可读。
- 一键复制 summary。

### Slice 07：Automated Probe Mode

目标：

- 自动跑一组 preset，每组持续固定时间。
- 输出 JSON / text report。

验收：

- 可以生成包含各 preset 的平均速度误差、最大脚滑、unreachable 点、resource errors 的报告。
- EditMode / PlayMode tests 至少覆盖 report DTO 和一个场景 smoke。

## Diagnostics

建议错误码：

| Code | 含义 |
| --- | --- |
| `LOCO_CAL_WARMUP_FAILED` | 动画预热失败。 |
| `LOCO_CAL_BACKEND_MISSING` | 未创建 Unity Playables backend。 |
| `LOCO_CAL_BLEND_UNREACHABLE_POINT` | BlendTree 点无法由 controller 输出到达。 |
| `LOCO_CAL_CLIP_METADATA_MISSING` | clip 缺 native velocity 或 foot contact metadata。 |
| `LOCO_CAL_FOOT_BONE_MISSING` | 左/右脚骨骼未解析。 |
| `LOCO_CAL_CLIP_TIME_UNAVAILABLE` | backend diagnostics 缺 clip normalized time，无法做 contact window 判定。 |
| `LOCO_CAL_SLIP_WARN` | 脚滑超过 warning 阈值。 |
| `LOCO_CAL_SLIP_BAD` | 脚滑超过 bad 阈值。 |
| `LOCO_CAL_VELOCITY_MISMATCH` | blended native velocity 和 actual local velocity 偏差过大。 |

## Required Backend Additions

现有 `MxAnimationDiagnosticSnapshot` 已有 layer、blend weights、recent requests。为了做 foot contact 判断，还需要补充：

- 当前 active clip slot 的 normalized time。
- 当前 active blend clip slot 的 normalized time。
- 每个 weighted clip 的 resolved `AnimationClip.length` 或 cycle duration。
- 可选：当前 playable time / speed。

建议扩展 diagnostics，而不是让 Calibration Lab 反射读取 `UnityPlayablesAnimationBackend` 私有字段。

## Manual Validation Plan

1. 打开 `Assets/Scenes/MxFramework/CharacterLocomotionCalibration.unity`。
2. 进入 Play Mode。
3. 确认 header：
   - `warmupSuccess=true`
   - `backendGraphValid=true`
   - `resourceErrors=none`
4. 运行 `Idle` preset：
   - dominant clip 为 idle。
   - foot slip 为 OK。
5. 运行 `Walk Forward`：
   - blend 点接近 `(0,1)`。
   - walk forward 权重上升。
   - blended native velocity 接近 actual local velocity。
   - 接地脚 slip 不超过阈值。
6. 运行 `Run Forward`：
   - 如果 controller 不能输出 y=2，UI 必须显示 `run_f unreachable`。
   - 如果可达，run forward 权重上升，并继续判定 foot slip。
7. 运行 `Strafe Left / Right / Back`：
   - 对应方向 clip 权重上升。
   - local velocity 方向和 native velocity 方向一致。
8. 运行 `Speed Ramp`：
   - 观察 walk -> run 权重过渡。
   - 观察是否出现过渡区脚滑峰值。
9. 复制 report summary 到 Issue / PR。

## Automated Validation Plan

- `git diff --check`
- EditMode:
  - `MxAnimationLocomotionCalibrationTests`
  - `CharacterLocomotionCalibrationReportTests`
  - `MxAnimationBlendReachabilityTests`
- PlayMode:
  - 场景 smoke：加载 scene、进入 Play Mode、角色加载成功、HUD 存在、backend graph valid。
  - preset smoke：至少跑 Idle / Walk Forward / Run Forward，确认报告生成。
- CLI:
  - `dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- animation compile --package Tools/MxFramework.Authoring/samples/character-iron-vanguard`
  - `dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character validate --package Tools/MxFramework.Authoring/samples/character-iron-vanguard --check-files --check-hashes`

## Acceptance Criteria

- 有可打开的 Unity 场景和一键 Play 手测入口。
- 角色通过正常 runtime resource loading 生成。
- HUD 能显示 controller velocity、BlendTree weights、clip native velocity、playback speed、foot contact、foot slip。
- 当前配置中不可达的 BlendTree 点会被明确标出。
- 可通过 preset 复现 walk / run / strafe 的权重和 foot sliding 指标。
- 每个方向 clip 都能独立显示实际速度、动画表达速度、方向误差和建议 playback speed。
- 可复制或保存校准报告。
- Console 无新增 error；warning 必须解释。
- 文档写清如何手测和如何解读结果。

## Risks and Open Questions

- 如果缺少 foot contact metadata，首版只能做 native velocity / reachability / heuristic foot height 检测；不能精准判定接地脚滑。
- 非 Humanoid skeleton 需要骨骼路径配置；Humanoid 可以优先用 `HumanBodyBones.LeftFoot / RightFoot`。
- BlendTree 混合下每个 clip normalized time 的取值需要 backend diagnostics 支持，否则 contact confidence 只能近似。
- 斜向移动可能需要单独 diagonal clip；没有 diagonal clip 时方向混合的脚步表现可能天然更差，Lab 只报告，不自动修正。
- playback speed 是否由 controller、animation profile 还是 action binding 决定，需要后续实现时明确权威来源。

## Suggested Issue Split

1. `Character Locomotion Calibration 01：Contracts and Report`
2. `Character Locomotion Calibration 02：Runtime Scene and Resource-loaded Runner`
3. `Character Locomotion Calibration 03：BlendTree Reachability and Weight Probe`
4. `Character Locomotion Calibration 04：Clip Calibration Metadata`
5. `Character Locomotion Calibration 05：Foot Sampling and Slip Metrics`
6. `Character Locomotion Calibration 06：UI Toolkit HUD and Scene Gizmos`
7. `Character Locomotion Calibration 07：Automated Preset Report`

这些 Issue 应放入同一个 milestone。建议先做 01-03，让当前 `run_f` 可达性和权重问题立刻可见；再做 04-07 进入真正 foot sliding 校准。
