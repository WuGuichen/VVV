# Animation 接口

> 状态：MVP Implemented
> 来源：`Docs/Tasks/MX_ANIMATION_01_DESIGN_CONTRACT.md`、`Docs/Tasks/MX_ANIMATION_07_NETWORK_PRESENTATION_SYNC_CONTRACT.md`、`Docs/Tasks/MX_ANIMATION_08_CLIP_REGISTRY_MAPPING_EDITOR.md`、`Docs/Tasks/MX_ANIMATION_09_LAYER_WEIGHT_AVATAR_MASK.md`、`Docs/Tasks/MX_ANIMATION_10_WARMUP_RESOURCE_VERSION_VALIDATION.md`、`Docs/Tasks/MX_ANIMATION_12_1D_LOCOMOTION_BLEND_DEMO.md`、`Docs/Tasks/MX_ANIMATION_13_BAKE_MVP.md`、Gitea Issue #94、Gitea Issue #95、Gitea Issue #106、Gitea Issue #107、Gitea Issue #108、Gitea Issue #109、Gitea Issue #110、Gitea Issue #111、Gitea Issue #112、Gitea Issue #123、Gitea Issue #124、Gitea Issue #125、Gitea Issue #126、Gitea Issue #127、Gitea Issue #129、Gitea Issue #130
> 实现边界：`MxFramework.Animation` noEngine contract 已落地，包含 mapping、presentation sync、warmup、presentation event timeline、dispatch sink、1D / 2D blend DTO/weight evaluation、skeleton / avatar / clip / bake compatibility validation、扩展 bake artifact/hash/diagnostics、backend cache diagnostics、资源版本校验、provider-switchable animation package loading expectation 和 Mod animation package override merge；`MxFramework.Animation.Unity` 提供 Unity Playables backend、内部 graph / clip playable / layer mixer / blend mixer / diagnostics 抽象、actor-scoped playable state cache、layer weight、AvatarMask 加载和 1D / 2D clip blend；`MxFramework.Combat.Animation.Unity` 提供 Combat 到 MxAnimation 的 Unity 表现桥；`MxFramework.Editor.Animation` 提供 clip registry authoring / export / validation、event timeline preview、Workstation timeline event editor / scrubber、bake report tool 和 compatibility profile extractor。

## 职责

Animation 是表现层契约。它接收 play、stop、crossfade、set layer weight、set blend 这类 presentation 请求，通过 `ResourceKey` 引用动画资源和 AvatarMask，并输出只读 diagnostics。它不参与 Combat hash、replay、命中、取消、无敌、伤害或其他权威逻辑。

`MxFramework.Animation` 必须保持 `noEngineReferences=true`，不依赖 `UnityEngine` 或 `UnityEditor`，也不保存 Unity object、GUID 或 `Assets/...` path。

Unity Playables 接入放在 `MxFramework.Animation.Unity`，可以引用 UnityEngine / Playables / Resources.Unity，并通过 `IResourceManager` 加载 `AnimationClip` 和 `AvatarMask`。

## 模块边界

| 模块 | 状态 | 依赖 | 职责 |
|------|------|------|------|
| `MxFramework.Animation` | MVP | Resources | layer id、layer definition、play / stop / crossfade / set layer weight / set blend request、animation set definition、1D / 2D blend definition、warmup/version validation、skeleton / avatar / clip compatibility validation、bake profile/artifact/diagnostics、fade state、backend interface |
| `MxFramework.Animation.Unity` | MVP | Animation、Resources、Resources.Unity、UnityEngine Playables | `UnityPlayablesAnimationBackend`、内部 PlayableGraph 抽象、clip load、AvatarMask load、layer mixer weight、1D / 2D clip blend、fallback、manual tick、graph shutdown、handle ownership |
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
| `MxAnimationBlend2DDefinition` / `MxAnimationBlend2DPoint` | noEngine 2D blend 定义，使用两个量化参数和二维 point 坐标映射 clip key |
| `MxAnimationBlend2DCalculator` | 不依赖 Unity 的 2D 权重计算；覆盖单点、双点、三角形、矩形、共线和外部 clamp / nearest segment fallback |
| `MxAnimationClipRegistry` | 从 `ResourceCatalog` 发现的 animation clip registry，只保存 `ResourceKey` 和 catalog entry hash |
| `MxAnimationClipRegistryBuilder` | 从正式 `ResourceCatalog` 过滤 `ResourceTypeIds.AnimationClip` 并生成 registry |
| `IMxAnimationMappingProvider` | 按 animation set id 提供 `MxAnimationSetDefinition` 的最小 provider surface |
| `MxAnimationStaticMappingProvider` | code-only / early validation provider；仍只消费 noEngine definition，不绕过资源系统 |
| `MxAnimationSetDefinitionHasher` | 对 set id、version、default/fallback、binding、events 生成稳定 `sha256:` definition hash |
| `MxAnimationSetDefinitionValidator` | 校验 set id/version/hash、default/fallback、catalog entry、clip type、重复 binding/action key |
| `MxAnimationResourceTypeIds` | Animation 专用资源类型 id：`MxAnimationBakeArtifact` 和 `MxAnimationCompatibilityProfile`；不反向污染通用 `ResourceTypeIds` |
| `MxAnimationPackageExpectation` / `MxAnimationPackageCatalog` | animation package 加载期望和实际 catalog snapshot，保存 package id、version、catalog id/hash、允许 provider 和 required resource entries |
| `MxAnimationPackageResourceExpectation` | 声明 package 内 clip、AvatarMask、bake artifact、compatibility profile 的 `ResourceKey`、catalog entry hash、provider 和是否参与 warmup |
| `MxAnimationPackageCatalogValidator` / `MxAnimationPackageValidationReport` | 校验 package id/version/hash、provider 切换、missing clip/mask/bake/profile 和 catalog entry hash mismatch |
| `MxAnimationModPackageManifest` | Mod animation package manifest 的 noEngine 摘要：package id、version、catalog id/hash 和 load order |
| `MxAnimationModOverrideDefinition` | 针对某个 animation set 的 Mod override DTO，声明 action、layer、1D/2D blend、package resource 和 compatibility override |
| `MxAnimationModOverrideMerger` / `MxAnimationModOverrideMergeResult` | 合并 base mapping + mod override，输出新 `MxAnimationSetDefinition`、merged package expectation、base/override hash/version 和 accepted/rejected diagnostics |
| `MxAnimationWarmupDefinition` | animation set 的 warmup 声明：preload group id、required keys、labels、failFast 和是否包含 default/fallback/action/mask |
| `MxAnimationWarmupService` | 复用 `IResourcePreloadService` 预热 animation set 资源，并输出版本 / hash / preload diagnostics |
| `MxAnimationWarmupRequest` / `MxAnimationWarmupResult` | warmup 输入与结果；结果持有 `ResourcePreloadResult` / `ResourceGroupHandle`，释放必须走 service |
| `MxAnimationWarmupIssue` | 结构化 warmup diagnostics，定位 animation set、catalog、clip registry、具体 clip / mask key 或 preload `ResourceError` |
| `MxAnimationClipRegistryAsset` | Editor-only registry authoring asset，可引用 `AnimationClip` 但不进入 runtime DTO |
| `MxAnimationClipRegistryExporter` | 从 Editor registry 导出 noEngine `MxAnimationSetDefinition` 和 validation report |
| `MxAnimationPackageBuilder` | Editor-only package builder，从 registry / bake / compatibility report 生成 package expectation、catalog snapshot 和 copyable validation report |
| `MxAnimationTimelineScrubberPreviewBuilder` / `MxAnimationTimelineScrubberPreviewWindow` | Editor-only 只读 timeline / scrubber 预览；把 action binding、presentation event、CombatActionTimeline snapshot 和 bake samples 对齐到同一 frame |
| `MxAnimationPlayRequest` | 播放请求，可指定 binding/action 或直接 clip key |
| `MxAnimationStopRequest` | 停止请求，支持 layer 和 fade out duration |
| `MxAnimationCrossFadeRequest` | crossfade 请求，支持 target clip、fade duration、start offset 和 outgoing release policy |
| `MxAnimationLayerWeightRequest` | layer weight correction / transition 请求，支持 immediate set 或按 presentation delta fade |
| `MxAnimationBlendRequest` | 1D / 2D 共享的 noEngine blend request DTO，保存 blend kind、blend id、量化参数、actor 和 correlation |
| `MxAnimationBlend1DRequest` | 1D blend 播放请求，指定 actor、blend id、量化参数和可选 fade duration |
| `MxAnimationBlend2DRequest` | 2D blend 播放请求，指定 actor、blend id、两个量化参数和可选 fade duration |
| `MxAnimationDiagnosticSnapshot` | backend、graph、resident default/fallback、layer state、active fades、recent requests/errors 和 backend cache 摘要 |
| `MxAnimationBackendCacheDiagnostic` | actor-scoped backend cache 摘要，暴露 cache hit/miss、resident clip、cached/active playable 和 ResourceManager loaded/ref count |
| `IMxAnimationBackend` | 最小 backend surface：play、stop、crossfade、set layer weight、set blend 1D / 2D、tick、snapshot、release |
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
| `MxAnimationBakeArtifact` | 派生 authoring artifact；保存 fixed-frame socket trajectory、root motion reference、weapon trace reference 和 event alignment，并带 artifact hash |
| `MxAnimationBakedWeaponTraceFrame` / `MxAnimationBakedRootMotionFrame` / `MxAnimationBakedSocketFrame` / `MxAnimationBakedEventMarker` | bake output 中的量化参考数据；只保存整数、字符串 id/path 和 `ResourceKey`，不保存 Unity object 或运行时 pose |
| `MxAnimationBakeDataPurpose` | bake data 的用途枚举：Combat reference input、Authoring preview、Timeline alignment、Diagnostics；用途不改变 Combat authority 边界 |
| `MxAnimationBakeIssueLocation` | bake diagnostics 定位信息，包含 source clip key、profile id、skeleton profile id 和 artifact hash |
| `MxAnimationBakeArtifactValidator` / `MxAnimationBakeValidationReport` | 校验 source/profile/skeleton/artifact hash、profile 字段、重复 trace/socket frame，并输出带定位的 diagnostics |
| `MxAnimationBakeQuantizer` / `MxAnimationBakeHasher` | noEngine 量化和稳定 hash 工具，供 Editor bake、CI 校验和加载侧 stale-artifact 检测复用 |
| `MxAnimationSkeletonCompatibilityProfile` | noEngine skeleton/profile 指纹；保存 profile id/hash、bone path 和 socket path |
| `MxAnimationClipCompatibilityProfile` / `MxAnimationAvatarMaskCompatibilityProfile` | noEngine clip binding / AvatarMask active path profile；只保存 `ResourceKey`、profile id/hash 和 path |
| `MxAnimationCompatibilityExpectation` | animation set 级兼容性期望；声明 skeleton id/hash、必需 bone/socket、clip binding 和 AvatarMask active path |
| `MxAnimationCompatibilityValidator` / `MxAnimationCompatibilityValidationReport` | 结构化校验 skeleton id/hash、missing bone/socket、clip binding、AvatarMask active path 和 bake artifact profile mismatch |
| `MxAnimationCompatibilityEditorExtractor` | Editor-only extractor；从 `GameObject` hierarchy、`AnimationClip` curve bindings 和 `AvatarMask` transform paths 生成 noEngine compatibility profile |

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
- MxAnimation Workstation 的 Timeline Event Editor + Scrubber 可以选择 action binding、步进 / scrub frame range，并直接编辑该 binding 的 `MxAnimationPresentationEvent` 数组；编辑通过 registry serialized fields 保存，导出仍由 `MxAnimationClipRegistryExporter` 生成 noEngine `MxAnimationSetDefinition`。
- Workstation scrubber 会显示同帧 presentation events、只读 CombatActionTimeline / Combat authoring source phase/window/frame event、以及可用 bake root/socket/weapon trace samples；当前 bake artifact asset 尚未作为 registry 序列化引用，Workstation v1 默认从选中 `AnimationClip` 生成内存 bake preview，缺 clip 或关闭 bake 时输出 clear diagnostics。
- Scrubber diagnostics 覆盖 missing clip、missing bake、hash/source mismatch、event out of range、timeline frame mismatch 和 Combat source reflection limitations where available，并支持复制 / 导出为 text。Combat rows 和 bake rows均为参考 / 诊断，不改变 Combat authority、timeline semantics 或 runtime DTO。
- Workstation Batch Bake 面板可以列出 registry clips，批量烘焙 selected / all clips 到 `.mxbake.txt` 派生报告，并输出每个 clip 的 source clip hash、profile hash、skeleton profile hash、artifact hash、validation issues 和 copy / export batch report。单 clip menu `MxFramework/MxAnimation/Bake Selected Animation Clip MVP` 保持可用。
- Workstation Compatibility Profile 面板可以从 skeleton root、registry clips 和 layer AvatarMask 引用提取 compatibility profile，复用 `MxAnimationCompatibilityEditorExtractor` / `MxAnimationCompatibilityValidator` 输出 skeleton / clip / AvatarMask / bake artifact diagnostics。刷新 compatibility 时会把最近 batch bake artifact 与当前 clip source hash、profile hash、skeleton profile hash 对比，明确报告 source/profile/skeleton/artifact stale mismatch。
- Workstation Mod Override Review 面板可以选择 base registry 和 override registry，复用当前 Package Builder 的 `MxAnimationPackageExpectation` / `MxAnimationPackageCatalog` 以及 Compatibility Profile 输出，调用 `MxAnimationModOverrideMerger` 预览 accepted / rejected rows、base / override / merged hash、version expectation、package diagnostics、compatibility diagnostics 和 warmup validation report。输入、package、compatibility 或 registry 内容变化会清空 stale preview；copy / export report 文本必须保留 merger `Issues` 的 code、field、expected、actual 和 message。

## Bake Artifact Contract

MxAnimation bake output 是派生 authoring artifact，不是唯一事实来源。它用于把 Unity `AnimationClip` 中可采样的 root / socket / weapon trace / event marker 信息转换为 fixed-frame、量化、可复现的参考数据：

- `MxAnimationBakeProfile.SourceClipHash` 记录源 clip 内容 hash。
- `MxAnimationBakeProfile.ProfileHash` 记录 bake profile 设置 hash。
- `MxAnimationBakeProfile.SkeletonProfileHash` 记录 skeleton/avatar/import 相关指纹。
- `MxAnimationBakeArtifact.ArtifactHash` 记录最终 reference 数据 hash。
- `MxAnimationBakedRootMotionFrame` 只提供表现、预览、诊断或后续烘焙输入；不能直接驱动权威位移。
- `MxAnimationBakedSocketFrame` 保存 socket id/path 的量化轨迹，用于 Authoring preview、Timeline 对齐、Combat reference 构建前的离线检查或 diagnostics。
- `MxAnimationBakedWeaponTraceFrame` 是 Combat 可消费 reference 的来源之一，但 Combat 仍必须结合显式 runtime profile、character scale、weapon length/radius、socket offset 和 target mask 生成最终 query。
- `MxAnimationBakedEventMarker` 保存 local frame、presentation frame 和可选 Combat frame；Editor bake 默认只写 presentation frame，Combat frame 需要由动作时间轴或项目层对齐流程显式提供。
- 加载侧或 CI 必须用 `MxAnimationBakeArtifactValidator` 检查 hash mismatch，不能静默使用明显过期 artifact。Mismatch diagnostics 会携带 source clip key、profile id、skeleton profile id 和 artifact hash，便于定位是源 clip、profile、skeleton 还是 artifact cache 过期。
- Skeleton / Avatar / Clip compatibility 是独立 noEngine 契约：`MxAnimationCompatibilityExpectation` 进入 `MxAnimationSetDefinition` hash 和 warmup 校验，Editor extractor 只负责从 Unity object 提取 profile。加载侧可以在 warmup 或 bake 校验中报告 missing bone、missing socket、AvatarMask active path mismatch、clip binding mismatch 和 bake artifact skeleton mismatch。

Runtime Combat 不读取 Unity Animator / Playable / bone pose 当前状态。若需要用动画姿态影响权威命中，必须走 `MxAnimationBakeArtifact` 这类可复现参考数据，再由 Combat noEngine 侧结合 weapon profile、character scale、socket offset 等显式运行时输入计算最终 query。

Unity Editor MVP 入口是 `MxFramework/MxAnimation/Bake Selected Animation Clip MVP`。它从选中的 `AnimationClip` 采样 transform 曲线和 `AnimationEvent`，输出 `.mxbake.txt` 派生 artifact 报告；完整 Timeline/Scrubber、retargeting matrix 和远程 bundle bake 不在当前范围内。

## Animation Package Loading Contract

Animation package loading 是 Animation 层对 Resources catalog 的一层 noEngine 期望校验，不新增 provider，也不修改 `IResourceManager`。同一份 `MxAnimationSetDefinition` 仍只保存 `ResourceKey`；sample memory、local AssetBundle、remote Bundle 或项目层可选 Addressables adapter 的差异都由 catalog entry 的 `provider` / `address` / `providerData` 表达。

`MxAnimationPackageExpectation` 用于声明当前 actor / animation set 进入表现层前期望的 package id、package version、catalog id、catalog hash、允许 provider 列表，以及必须存在的 resource entries：

- `AnimationClip`：使用 `ResourceTypeIds.AnimationClip`。
- `AvatarMask`：使用 `ResourceTypeIds.AvatarMask`。
- bake artifact：使用 `MxAnimationResourceTypeIds.BakeArtifact`。
- compatibility profile：使用 `MxAnimationResourceTypeIds.CompatibilityProfile`。

`MxAnimationPackageCatalogValidator` 必须在 warmup 或 CI 阶段输出结构化 diagnostics，不能把错误包静默当成 fallback：package id/version/hash mismatch、provider 不在允许列表、catalog entry hash mismatch、missing clip、missing AvatarMask、missing bake artifact、missing compatibility profile 都是 error。

`MxAnimationWarmupRequest` 可携带 package expectation 和 `MxAnimationPackageCatalog` snapshot。warmup 会把 `MxAnimationPackageResourceExpectation.RequiredForWarmup=true` 的资源加入 `ResourcePreloadPlan`，因此同一个 warmup group 可以持有 clip、AvatarMask、bake artifact 和 compatibility profile；释放仍通过 `MxAnimationWarmupService.Release` 归还 group handle。

Editor Workstation 的 `Animation Package Builder` 面板只生成 preview/build input，不提交派生包内容。它从 `MxAnimationClipRegistryAsset` 导出的 mapping、最近一次 batch bake report 和 compatibility report 生成 `MxAnimationPackageExpectation` 与 `ResourceCatalog` snapshot；sample provider 可以切换为 memory、local AssetBundle 或 remoteBundle。local / remote bundle 地址仍使用 `bundleName|assetName`；remote URL、cache key 和 bundle SHA-256 只写入 `providerData`，不引入 Addressables 或发布自动化。

Addressables 保持可选兼容路径：项目若已经安装 Addressables，应实现独立 provider/adapter 并把 catalog entry provider 写成项目约定的 id。Animation 层只校验 provider id 是否在 `AcceptedProviderIds`，不引用 Addressables API，也不要求 `MxFramework.Resources.Unity` 增加硬依赖。

## Mod Animation Package Override

Mod animation package override 是 noEngine merge 契约，不读取外部 Unity object，不修改 Combat action、hit、damage、cancel、invulnerability 或 authority。外部包只能通过 `ResourceKey` 覆盖表现 mapping：

- action binding replacement：替换已有 binding/action 的 clip、layer、播放参数和表现事件。
- layer replacement/addition：替换或新增 layer definition，包含 AvatarMask key。
- 1D / 2D blend replacement/addition：按 blend id 替换或新增 blend definition。
- package resource expectation：声明 bake artifact、compatibility profile 或其它 package resource key，进入 #129 package validation / warmup。

`MxAnimationModOverrideMerger.Merge` 的输入是 base `MxAnimationSetDefinition`、`MxAnimationModOverrideDefinition`、可选 `ResourceCatalog`、`MxAnimationCompatibilityProfile`、`MxAnimationPackageCatalog` 和 base package expectation。合并前会校验：

- target set id、expected base version、expected base hash。
- override 自身 canonical hash。
- 合并后的 mapping structure 和 catalog entry。
- override compatibility expectation 与实际 compatibility profile。
- merged package expectation 与 package catalog。

通过后返回新的 `MxAnimationSetDefinition`，其 `DefinitionHash` 由合并后内容稳定计算；结果同时保留 `BaseDefinitionHash/BaseVersion` 和 `OverrideHash/OverrideVersion`。失败时 `MergedDefinition` 为 null，`Issues` 中保留具体 rejected diagnostics。

late load / unload 不由 override merger 直接持有资源 handle。调用方应把 `MergedDefinition` 和 `MergedPackageExpectation` 交给 `MxAnimationWarmupService`；warmup result 的 `ResourceGroupHandle` 仍按 #129 规则释放。

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
- `MxAnimationWarmupDefinition` 可以声明 preload group id、warmup labels 和额外 required keys。默认会把 default clip、fallback clip、action clips、1D / 2D blend point clips 和 layer AvatarMask key 纳入 required keys。
- `MxAnimationCompatibilityExpectation` 可以挂在 `MxAnimationSetDefinition` 上，进入 definition hash。warmup 会把 compatibility clip / AvatarMask key 纳入 required keys，并在传入 `MxAnimationCompatibilityProfile` 时输出 `CompatibilityValidationFailed` issue。
- `MxAnimationBlend1DDefinition` 和 `MxAnimationBlend2DDefinition` 的 point clip 使用 `ResourceKey`，会进入 definition hash、mapping validation 和 warmup required keys。权重计算在 noEngine 层完成；Unity backend 只消费权重并加载对应 clip。
- 2D blend 权重计算只使用量化整数参数和 point 坐标，不读取 Unity pose。外部采样会 clamp 到矩形边界或最近线段 / 最近点，保证 diagnostics 和 replay presentation correction 可复现。
- `MxAnimationBakeArtifact` 是派生缓存。它可以辅助 Combat authoring 和 preview，但 runtime authority 只能消费已量化 reference + 显式 runtime profile，不得直接读 Unity 当前动画状态。
- `MxAnimationWarmupService` 不新增资源子系统，只把 required keys / labels 转成 `ResourcePreloadPlan`，并复用 `IResourcePreloadService` / `ResourceManager` / `ResourceCatalog`。
- animation package expectation 不改变 mapping contract；同一份 `MxAnimationSetDefinition` 可以在 sample memory provider、local AssetBundle provider、remote Bundle provider 或项目层 Addressables adapter 间切换，只要 catalog entry key/hash/version/provider expectation 匹配。
- warmup 会校验 animation set hash、resource catalog hash、clip registry version 和可选 expected clip registry entry hash；mismatch 必须输出具体 field、expected、actual 和相关 resource key。
- warmup 可进一步校验 package id/version/catalog hash、catalog entry hash、missing clip/mask/bake/profile，并把 package resources 加入 preload group。
- 对非立即完成的 provider，调用方应使用 `MxAnimationWarmupService.WarmupAsync` 并轮询 `IsDone`；同步 `Warmup` 只适合立即完成路径，遇到 pending preload 会返回 `PreloadOperationPending` issue。
- Mod override 只能产出新的表现 mapping 和 package expectation。合法 override 仍必须先通过 mapping/catalog/package/compatibility validation，再进入 warmup；不兼容或缺资源的 override 不允许强制播放。
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

## Unity Playables Backend Abstraction

`UnityPlayablesAnimationBackend` 的 public `IMxAnimationBackend` surface 保持稳定。Unity Playables 细节拆到 `MxFramework.Animation.Unity` 内部抽象，后续 cache、2D blend tree、scrubber 和 package loading 应优先复用这些边界，而不是继续扩大 backend 单类职责：

- Graph construction / lifecycle：只负责 `PlayableGraph` 创建、manual evaluate 和 destroy，不负责资源加载、binding 解析或请求语义。
- Clip Playable：只从已加载 `AnimationClip` 创建 / 销毁 clip playable，并设置起始时间和速度，不持有 `ResourceHandle`。
- Layer Mixer：只管理 root layer mixer 输入、layer weight、additive mode 和 AvatarMask wiring，不连接单个 clip slot。
- Blend Mixer：只管理 layer mixer 内的 clip input、权重设置和断开，不计算权重、不加载资源。1D / 2D 请求都先转换为 clip weight 列表，再走同一条 slot 复用和 mixer 更新路径。
- Diagnostics：只维护 bounded recent request / resource error buffer，不解释业务成功失败、不拥有 graph 或 resource lifetime。

这些抽象是 Unity assembly 内部类型；测试通过 `MxFramework.Tests` 友元程序集覆盖职责边界。

## Unity Playables State Cache

第三阶段的 cache 范围限定在单个 `UnityPlayablesAnimationBackend` 实例，也就是 actor-scoped cache。它不做跨 actor 全局复用，不改变 `ResourceManager` handle 所有权，也不把 package / variant 不同的 clip 合并：

- 重复 `Play` 当前 clip 时复用当前 clip playable，只重设 speed、loop 和 start offset，不重复加载资源。
- 同一 `MxAnimationBlend1DDefinition` 或 `MxAnimationBlend2DDefinition` 在参数变化时保留该 blend 已加载过的 clip slots；已加载 slot 权重归零后仍可作为 cached playable 留在 graph 中，`ActivePlayableCount` 只统计权重大于 0 的 blend slot。
- `Stop`、普通 `Play` 切出 blend、backend `Release`、actor destroy，以及 fade out 权重归零后的 outgoing slot detached 都会释放 backend 拥有的 handles；resident default/fallback 仍保持到 backend `Release`。
- `MxAnimationDiagnosticSnapshot.Cache` 输出 cache hit/miss、resident clip 数量、cached/active playable 数量，以及当前 `ResourceManager` loaded/ref count，供 smoke demo、MCP 手测和回归测试观察资源是否持续增长。

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
- 1D / 2D locomotion blend 权重计算、definition hash、mapping validation、warmup clip 收集和 Unity Playables backend 多 clip 权重诊断。
- Unity Playables backend 内部 graph lifecycle、clip playable factory、layer mixer、1D blend mixer 和 diagnostics buffer 职责边界。
- Unity Playables actor-scoped cache 的 repeated play、locomotion blend 参数切换和 upper-body fade-out release。
- bake profile/artifact hash 稳定性、source/profile/artifact mismatch diagnostics、Editor clip 曲线采样和 event marker bake。
- skeleton/avatar/clip compatibility profile、missing bone/socket、AvatarMask active path、clip binding、bake artifact skeleton mismatch、warmup compatibility diagnostics 和 Editor extractor。
- animation package expectation、provider-switchable catalog validation、missing bake/profile diagnostics 和 package warmup preload。
- Mod animation package override 的 stable hash、action/mask/blend/bake override、base hash mismatch、package missing resource、compatibility rejection 和 warmup handle release。
- play / stop state transition 和非 resident handle release。
- requested clip load failure fallback 到 resident fallback，并输出 diagnostics。
- crossfade 期间 outgoing handle 保持到 fade 完成后释放。
- backend release destroy graph 并释放 default、fallback 和当前 clip handles。
- Combat bridge action started -> play / crossfade、cancel / finish -> stop、frame event -> binding presentation event dispatch、dedupe drop、correlation diagnostics、legacy opt-in coexistence、noEngine Combat asmdef 边界。
