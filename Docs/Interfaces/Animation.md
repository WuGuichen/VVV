# Animation 接口

> 状态：MVP Implemented
> 来源：`Docs/Tasks/MX_ANIMATION_01_DESIGN_CONTRACT.md`、`Docs/Tasks/MX_ANIMATION_07_NETWORK_PRESENTATION_SYNC_CONTRACT.md`、`Docs/Tasks/MX_ANIMATION_08_CLIP_REGISTRY_MAPPING_EDITOR.md`、`Docs/Tasks/MX_ANIMATION_09_LAYER_WEIGHT_AVATAR_MASK.md`、`Docs/Tasks/MX_ANIMATION_10_WARMUP_RESOURCE_VERSION_VALIDATION.md`、`Docs/Tasks/MX_ANIMATION_12_1D_LOCOMOTION_BLEND_DEMO.md`、`Docs/Tasks/MX_ANIMATION_13_BAKE_MVP.md`、Gitea Issue #94、Gitea Issue #95、Gitea Issue #106、Gitea Issue #107、Gitea Issue #108、Gitea Issue #109、Gitea Issue #110、Gitea Issue #111、Gitea Issue #112
> 实现边界：`MxFramework.Animation` noEngine contract 已落地，包含 mapping、presentation sync、warmup、presentation event timeline、dispatch sink、1D blend DTO/weight evaluation、bake artifact/hash/diagnostics 和资源版本校验；`MxFramework.Animation.Unity` 提供首版 Unity Playables backend、layer weight、AvatarMask 加载和 1D clip blend；`MxFramework.Combat.Animation.Unity` 提供 Combat 到 MxAnimation 的 Unity 表现桥；`MxFramework.Editor.Animation` 提供最小 clip registry authoring / export / validation / event timeline preview / bake MVP tool。

## 职责

Animation 是表现层契约。它接收 play、stop、crossfade、set layer weight、set blend 这类 presentation 请求，通过 `ResourceKey` 引用动画资源和 AvatarMask，并输出只读 diagnostics。它不参与 Combat hash、replay、命中、取消、无敌、伤害或其他权威逻辑。

`MxFramework.Animation` 必须保持 `noEngineReferences=true`，不依赖 `UnityEngine` 或 `UnityEditor`，也不保存 Unity object、GUID 或 `Assets/...` path。

Unity Playables 接入放在 `MxFramework.Animation.Unity`，可以引用 UnityEngine / Playables / Resources.Unity，并通过 `IResourceManager` 加载 `AnimationClip` 和 `AvatarMask`。

## 模块边界

| 模块 | 状态 | 依赖 | 职责 |
|------|------|------|------|
| `MxFramework.Animation` | MVP | Resources | layer id、layer definition、play / stop / crossfade / set layer weight / set blend request、animation set definition、1D blend definition、warmup/version validation、bake profile/artifact/diagnostics、fade state、backend interface |
| `MxFramework.Animation.Unity` | MVP | Animation、Resources、Resources.Unity、UnityEngine Playables | `UnityPlayablesAnimationBackend`、clip load、AvatarMask load、layer mixer weight、1D clip blend、fallback、manual tick、graph shutdown、handle ownership |
| `MxFramework.Combat.Animation.Unity` | MVP | Combat、Animation、Animation.Unity | 订阅 `CombatActionRunner` lifecycle / frame presentation events，转成 MxAnimation play / stop / crossfade 请求和 presentation event dispatch |
| `MxFramework.Editor.Animation` | MVP | Editor、Animation、Resources、UnityEditor | Clip registry authoring asset、mapping export、catalog validation、最小 Inspector validation、AnimationClip bake MVP tool |

依赖方向：

```text
MxFramework.Resources
  <- MxFramework.Animation
      <- MxFramework.Animation.Unity
          <- MxFramework.Combat.Animation.Unity

MxFramework.Combat
  <- MxFramework.Combat.Animation.Unity
```

Combat 不引用 Animation.Unity。Unity animation time 不反向驱动 Combat authority。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `MxAnimationLayerId` | 稳定 layer id value object；默认 `base`，不等同 Unity Animator layer index |
| `MxAnimationLayerDefinition` | noEngine layer 配置；保存 layer id、profile id、default weight、blend mode 和 AvatarMask `ResourceKey` |
| `MxAnimationLayerBlendMode` | layer 混合模式，当前 Unity backend 映射为 Playables layer override / additive |
| `MxAnimationLayerMaskStatus` | diagnostics 中的 AvatarMask 生命周期状态：未配置、加载中、已加载、失败或已释放 |
| `MxAnimationSetDefinition` | actor / archetype / skin 的 presentation binding 集合，clip 使用 `ResourceKey` |
| `MxAnimationActionBinding` | action key 或 binding id 到 clip、layer、speed、loop 和 presentation events 的映射 |
| `MxAnimationBlend1DDefinition` / `MxAnimationBlend1DPoint` | noEngine 1D blend 定义，按量化参数把 idle / walk / run 这类 clip key 映射成权重 |
| `MxAnimationBlend1DCalculator` | 不依赖 Unity 的 1D 权重计算；输入 `MxAnimationQuantizedParameter`，输出每个 point 的稳定权重 |
| `MxAnimationClipRegistry` | 从 `ResourceCatalog` 发现的 animation clip registry，只保存 `ResourceKey` 和 catalog entry hash |
| `MxAnimationClipRegistryBuilder` | 从正式 `ResourceCatalog` 过滤 `ResourceTypeIds.AnimationClip` 并生成 registry |
| `IMxAnimationMappingProvider` | 按 animation set id 提供 `MxAnimationSetDefinition` 的最小 provider surface |
| `MxAnimationStaticMappingProvider` | code-only / early validation provider；仍只消费 noEngine definition，不绕过资源系统 |
| `MxAnimationSetDefinitionHasher` | 对 set id、version、default/fallback、binding、events 生成稳定 `sha256:` definition hash |
| `MxAnimationSetDefinitionValidator` | 校验 set id/version/hash、default/fallback、catalog entry、clip type、重复 binding/action key |
| `MxAnimationWarmupDefinition` | animation set 的 warmup 声明：preload group id、required keys、labels、failFast 和是否包含 default/fallback/action/mask |
| `MxAnimationWarmupService` | 复用 `IResourcePreloadService` 预热 animation set 资源，并输出版本 / hash / preload diagnostics |
| `MxAnimationWarmupRequest` / `MxAnimationWarmupResult` | warmup 输入与结果；结果持有 `ResourcePreloadResult` / `ResourceGroupHandle`，释放必须走 service |
| `MxAnimationWarmupIssue` | 结构化 warmup diagnostics，定位 animation set、catalog、clip registry、具体 clip / mask key 或 preload `ResourceError` |
| `MxAnimationClipRegistryAsset` | Editor-only registry authoring asset，可引用 `AnimationClip` 但不进入 runtime DTO |
| `MxAnimationClipRegistryExporter` | 从 Editor registry 导出 noEngine `MxAnimationSetDefinition` 和 validation report |
| `MxAnimationPlayRequest` | 播放请求，可指定 binding/action 或直接 clip key |
| `MxAnimationStopRequest` | 停止请求，支持 layer 和 fade out duration |
| `MxAnimationCrossFadeRequest` | crossfade 请求，支持 target clip、fade duration、start offset 和 outgoing release policy |
| `MxAnimationLayerWeightRequest` | layer weight correction / transition 请求，支持 immediate set 或按 presentation delta fade |
| `MxAnimationBlend1DRequest` | 1D blend 播放请求，指定 actor、blend id、量化参数和可选 fade duration |
| `MxAnimationDiagnosticSnapshot` | backend、graph、resident default/fallback、layer state、active fades、recent requests/errors |
| `IMxAnimationBackend` | 最小 backend surface：play、stop、crossfade、set layer weight、set blend 1D、tick、snapshot、release |
| `UnityPlayablesAnimationBackend` | Unity Playables MVP backend，使用 manual `Tick(deltaTime)` 推进 |
| `MxAnimationPresentationSyncState` | 多人 / late join / 补包场景的表现恢复状态；保存 actor、animation set version/hash、action instance、Combat frame anchor、layer state 和量化表现参数 |
| `MxAnimationLayerSyncState` | layer weight / transition 恢复状态；包含 current / target weight、transition frame 信息和 correlation |
| `MxAnimationQuantizedParameter` | 表现层量化参数，例如 locomotion speed blend 参数 |
| `MxAnimationPresentationEventDedupeKey` | 表现事件去重键，使用 actor、action instance、world/local frame、event id 和 source order |
| `MxAnimationPresentationEventDispatch` | noEngine presentation event dispatch payload，包含 event、actor/action/binding、frame correlation、source order 和 dedupe key |
| `IMxAnimationPresentationEventSink` | noEngine 表现事件消费接口，供 VFX / SFX / Camera / Footstep / UI feedback 后端适配 |
| `MxAnimationPresentationEventDedupeWindow` | bounded 去重窗口，按 actor + action instance + world/local frame + event id + source order 过滤重复 dispatch |
| `MxAnimationPresentationEventDispatchSink` | noEngine dispatch wrapper，负责 dedupe、payload unresolved diagnostics 和转发到 sink |
| `MxAnimationEventTimelineBuilder` / `MxAnimationEventTimelineRow` | 从 `MxAnimationSetDefinition` 生成 Editor / diagnostics 可读的事件时间轴行，显示 Seconds / NormalizedTime / CombatFrame / PresentationFrame 和 deterministic correlation |
| `MxAnimationPresentationSyncValidator` | 校验 sync state 与本地 animation set / catalog / clip registry version 是否兼容，并输出结构化 diagnostics |
| `MxAnimationBakeProfile` | bake 输入指纹；包含 source clip key/hash、skeleton/avatar profile id/hash、sample tick rate、quantization scale、coordinate space、rounding policy 和 import/settings fingerprint |
| `MxAnimationBakeArtifact` | 派生 authoring artifact；保存 fixed-frame socket/root/weapon trace reference、root motion reference 和 event markers，并带 artifact hash |
| `MxAnimationBakedWeaponTraceFrame` / `MxAnimationBakedRootMotionFrame` / `MxAnimationBakedEventMarker` | bake output 中的量化参考数据；只保存整数和 `ResourceKey`，不保存 Unity object 或运行时 pose |
| `MxAnimationBakeArtifactValidator` / `MxAnimationBakeValidationReport` | 校验 source/profile/skeleton/artifact hash、profile 字段、重复 trace frame，并输出明确 diagnostics |
| `MxAnimationBakeQuantizer` / `MxAnimationBakeHasher` | noEngine 量化和稳定 hash 工具，供 Editor bake、CI 校验和加载侧 stale-artifact 检测复用 |

## Presentation Sync Contract

MxAnimation 的网络表现同步契约只恢复 presentation state，不实现网络协议，不进入 Combat authority。同步载荷应来自权威 Combat / Gameplay 状态或项目层网络层，MxAnimation 只消费稳定 id、时间锚点和量化参数：

- actor / entity id。
- animation set id / version / hash。
- resource catalog hash。
- clip registry version。
- action id 或 action key。
- action instance id。
- started-at Combat frame 和 current local frame。
- layer sync state：layer id、current weight、target weight、transition start/duration/remaining frames、transition policy/correlation。
- quantized blend parameters。
- presentation event dedupe key。

`MxAnimationPresentationSyncVersionExpectation.None` 只校验必需 identity。强校验路径必须提供 expected animation set id/version/hash、resource catalog hash 和 clip registry version；`action instance id` 为 0 时仍允许用 actor + frame + event id + source order 进行 legacy 去重。

该状态可用于 late join、delayed packet 和 prediction correction 下的 seek、crossfade、stop 或 layer weight correction。一次性 presentation event 默认不补播；只有 `MxAnimationPresentationEventReplayPolicy.CatchUpSafe` 标记的事件才允许项目层在 late join / catch-up 中显式补发。它不得把 Playable time、Animator state、Unity bone pose 或 normalized time 写回 Combat，也不得进入 replay hash。

资源/版本不匹配必须以明确 diagnostics 失败：animation set id/version/hash、resource catalog hash 或 clip registry version 不一致时，加载侧不能静默 fallback 到空播。#109 的 warmup / resource validation 会复用这条契约。

## Combat Presentation Bridge

`MxFramework.Combat.Animation.Unity` 是独立 Unity bridge assembly，不放入 `MxFramework.Combat` noEngine assembly。它的默认 action key 策略是 `action:<combatActionId>`，并用该 key 查找 `MxAnimationSetDefinition` / `MxAnimationActionBinding`。

公开类型：

| 接口/类型 | 用途 |
|-----------|------|
| `CombatMxAnimationUnityBridge` | 订阅 `CombatActionRunner.ActionStarted`、`ActionCanceled`、`ActionFinished`、`ActionFrameEventRaised`，按 entity 路由到注册的 `IMxAnimationBackend` |
| `CombatMxAnimationBridgeOptions` | 配置 start 使用 `Play` 或 `CrossFade`，cancel / finish 使用 `Stop` 或 `CrossFade`，以及 fade duration、action key prefix、frame event binding |
| `CombatMxAnimationFrameEventBinding` | 可选 explicit bridge config，用 Combat event correlation keys 匹配并解析到表现事件 |
| `ICombatMxAnimationPresentationEventSink` | 接收已解析的 presentation events，供 VFX / SFX / camera / footstep / UI feedback 层消费 |
| `CombatMxAnimationPresentationEventDispatch` | presentation event dispatch payload，保留 Combat entity、action、action instance、world frame、local frame、原始 `CombatActionFrameEvent`、dedupe key 和 correlation id |
| `CombatMxAnimationBridgeDiagnosticSnapshot` | bridge 最近请求 / dispatch diagnostics，用于排查 mapping、lifecycle、duplicate drop 和 payload 解析问题 |

Lifecycle 策略：

- action started 默认发 `MxAnimationCrossFadeRequest`，可通过 options 改为 `MxAnimationPlayRequest`。
- action canceled / finished 默认发 `MxAnimationStopRequest`，可通过 options 改为 crossfade 到指定 binding 或 default clip。
- bridge 只向表现 backend 发送请求，不把 backend state、Playable time、Animator state 或 normalized time 写回 Combat。

Frame event mapping 策略：

- 首选 `CombatMxAnimationFrameEventBinding` 显式配置；配置可以直接提供 `MxAnimationPresentationEvent`，也可以指向 animation set / action binding 中的 presentation event id。
- 没有显式配置时，从当前 `MxAnimationActionBinding.PresentationEvents` 查找 `TimeDomain == CombatFrame` 或 `PresentationFrame`、`Time == localFrame`、`EventId == event:<CombatActionFrameEvent.EventId>` 或纯数字 event id 的事件。
- `CombatActionFrameEvent.EventId`、`SourceOrder`、`IntPayload` 只作为 deterministic correlation / matching keys。VFX / SFX / Camera / Footstep / UI kind 与 `ResourceKey` payload 必须来自 `MxAnimationPresentationEvent` 或显式 bridge config。
- runtime dispatch 使用 `MxAnimationPresentationEventDedupeKey` 去重；v0 window 是 bounded actor + action instance window，key 包含 entity actor id、action instance、world/local frame、event id 和 source order。重复 key 会记录 `FramePresentationEventDuplicateDropped` diagnostics，不再转发给 sink。

Event timeline authoring / preview：

- `MxAnimationClipRegistryAsset` 的默认 Inspector 仍负责编辑 clip、binding 和 `MxAnimationPresentationEvent` 数组；Event Timeline Preview 从 exporter 生成的 noEngine `MxAnimationSetDefinition` 构建，只读取 `ResourceKey` DTO，不把 Unity object 写入 runtime contract。
- timeline row 支持 Seconds / NormalizedTime / CombatFrame / PresentationFrame。CombatFrame / PresentationFrame row 显示 deterministic correlation label，便于和 Combat fixed-frame event 对齐。
- 该 preview 不是完整 Timeline/Scrubber；逐帧 scrub、真实 VFX/SFX/Camera 执行和 resource payload preview 留给后续任务或项目层工具。

## Bake Artifact Contract

MxAnimation bake output 是派生 authoring artifact，不是唯一事实来源。它用于把 Unity `AnimationClip` 中可采样的 root / socket / weapon trace / event marker 信息转换为 fixed-frame、量化、可复现的参考数据：

- `MxAnimationBakeProfile.SourceClipHash` 记录源 clip 内容 hash。
- `MxAnimationBakeProfile.ProfileHash` 记录 bake profile 设置 hash。
- `MxAnimationBakeProfile.SkeletonProfileHash` 记录 skeleton/avatar/import 相关指纹。
- `MxAnimationBakeArtifact.ArtifactHash` 记录最终 reference 数据 hash。
- 加载侧或 CI 必须用 `MxAnimationBakeArtifactValidator` 检查 hash mismatch，不能静默使用明显过期 artifact。

Runtime Combat 不读取 Unity Animator / Playable / bone pose 当前状态。若需要用动画姿态影响权威命中，必须走 `MxAnimationBakeArtifact` 这类可复现参考数据，再由 Combat noEngine 侧结合 weapon profile、character scale、socket offset 等显式运行时输入计算最终 query。

Unity Editor MVP 入口是 `MxFramework/MxAnimation/Bake Selected Animation Clip MVP`。它从选中的 `AnimationClip` 采样 transform 曲线和 `AnimationEvent`，输出 `.mxbake.txt` 派生 artifact 报告；完整 Timeline/Scrubber、retargeting matrix 和远程 bundle bake 不在当前范围内。

Legacy coexistence:

- 旧 `MxFramework.Runtime.Unity.CombatAnimationUnityModule` / `CombatAnimatorDriver` 保持可用，但仍是 opt-in。
- 新 `CombatMxAnimationUnityBridge` 不创建、不注册、不调用旧 driver。项目层 composition root 应在同一 entity 上选择 legacy Animator bridge 或 MxAnimation bridge 之一，避免同一 Combat event 双触发表现。

## 使用约定

- DTO 中的 clip、VFX、SFX、camera profile 等表现资源一律使用 `ResourceKey`。
- clip key 的 `TypeId` 使用 `ResourceTypeIds.AnimationClip`。
- AvatarMask key 的 `TypeId` 使用 `ResourceTypeIds.AvatarMask`。noEngine definition 只保存 key；Unity backend 通过 `IResourceManager.LoadAsync<AvatarMask>` 加载并持有 handle。
- layer weight 保存在 backend diagnostics 和 presentation sync state 中。它只影响表现层 Playables layer mixer 输入权重，不写回 Combat authority。
- `MxAnimationLayerDefinition.DefaultWeight` 和 `MxAnimationLayerWeightRequest.Weight` 会夹到 0..1；`NaN` 按 0 处理。
- AvatarMask 加载失败只让该 layer 的 mask diagnostics 进入 failed，不阻断 clip 播放或 fallback 路径；缺失资源必须通过 diagnostics 暴露。
- `MxAnimationSetDefinition.DefinitionHash` 是 mapping 内容 hash，用于加载侧和 #109 warmup / resource validation 检测过期 mapping。
- `MxAnimationWarmupDefinition` 可以声明 preload group id、warmup labels 和额外 required keys。默认会把 default clip、fallback clip、action clips 和 layer AvatarMask key 纳入 required keys。
- `MxAnimationBlend1DDefinition` 的 point clip 使用 `ResourceKey`，会进入 definition hash、mapping validation 和 warmup required keys。权重计算在 noEngine 层完成；Unity backend 只消费权重并加载对应 clip。
- `MxAnimationBakeArtifact` 是派生缓存。它可以辅助 Combat authoring 和 preview，但 runtime authority 只能消费已量化 reference + 显式 runtime profile，不得直接读 Unity 当前动画状态。
- `MxAnimationWarmupService` 不新增资源子系统，只把 required keys / labels 转成 `ResourcePreloadPlan`，并复用 `IResourcePreloadService` / `ResourceManager` / `ResourceCatalog`。
- warmup 会校验 animation set hash、resource catalog hash、clip registry version 和可选 expected clip registry entry hash；mismatch 必须输出具体 field、expected、actual 和相关 resource key。
- warmup partial failure 会把每个 `ResourceError` 转成 `PreloadResourceFailed` issue，保留失败 key、provider、address 和错误码。调用方不能把失败当作空播成功。
- warmup result 的 `ResourceGroupHandle` 只代表预热持有的 handles。释放 group 只归还这一组引用；如果其它 consumer 仍持有同一 clip，底层资源不会被卸载。
- `MxAnimationClipRegistryAsset` 只属于 Unity Editor authoring。运行时和 Demo 不得从该 asset 直接取 `AnimationClip`，必须通过导出的 `MxAnimationSetDefinition` + `ResourceManager` 加载。
- 当前 Mapping Editor 是最小 Inspector authoring / structure validation 入口；完整 catalog 校验由 exporter / pipeline 传入 `ResourceCatalog` 后执行，复杂搜索、预览和 timeline scrubber 不在 #107 范围内。
- Event Timeline Preview 是 #110 的最小可视化入口：它复用导出 DTO 展示/复制事件时间轴摘要，编辑仍通过 registry serialized fields 完成。
- `MxAnimationSetDefinition.DefaultClip` 和 `FallbackClip` 是 backend 生命周期内的 resident clip。加载成功后常驻到 backend `Release`，并在 diagnostics 中标记为 resident。
- 普通 play/crossfade clip 由 backend 自己通过 `IResourceManager.LoadAsync<AnimationClip>` 获取 handle；stop、fade 完成、destroy 或 release 后释放。
- 当前 `ResourceManager.LoadAsync<T>` 是 immediate operation wrapper；backend 仍以 operation 状态处理，diagnostics 可显示 loading / failure。
- crossfade outgoing clip handle 保持到 outgoing playable 权重归零并从 graph 断开后才释放。
- requested clip 加载失败后按顺序尝试 fallback clip；fallback 也失败时 layer 进入 failed state，并记录 `ResourceError`。
- `Tick(deltaTime)` 使用外部传入的 presentation delta，Unity composition root 可以传 `Time.deltaTime` 或其他展示时间源；该时间源不得进入 Combat authority。
- `PlayableGraph.Destroy` 是 Unity backend shutdown 边界，会断开 playable 并释放 backend 拥有的全部 handles。
- `Release` 幂等；重复调用不应崩溃。

## 测试入口

```text
Assets/Scripts/MxFramework/Tests/Animation/
```

当前 focused tests 覆盖：

- noEngine layer id 和 animation set binding 查询。
- animation set definition hash、clip registry builder、mapping provider 和 catalog validation。
- presentation sync state、layer transition state、quantized parameter、event dedupe key、dispatch sink、timeline rows 和 version diagnostics。
- layer definition hash、layer weight request、AvatarMask key validation 和 mask load / release diagnostics。
- warmup success、sync hash/version mismatch、catalog wrong type、preload partial failure、clip registry entry hash mismatch 和 release 后 ref-count 归还。
- 1D locomotion blend 权重计算、definition hash、mapping validation、warmup clip 收集和 Unity Playables backend 多 clip 权重诊断。
- bake profile/artifact hash 稳定性、source/profile/artifact mismatch diagnostics、Editor clip 曲线采样和 event marker bake。
- play / stop state transition 和非 resident handle release。
- requested clip load failure fallback 到 resident fallback，并输出 diagnostics。
- crossfade 期间 outgoing handle 保持到 fade 完成后释放。
- backend release destroy graph 并释放 default、fallback 和当前 clip handles。
- Combat bridge action started -> play / crossfade、cancel / finish -> stop、frame event -> binding presentation event dispatch、dedupe drop、correlation diagnostics、legacy opt-in coexistence、noEngine Combat asmdef 边界。
