# Animation 接口

> 状态：MVP Implemented
> 来源：`Docs/Tasks/MX_ANIMATION_01_DESIGN_CONTRACT.md`、`Docs/Tasks/MX_ANIMATION_08_CLIP_REGISTRY_MAPPING_EDITOR.md`、Gitea Issue #94、Gitea Issue #95、Gitea Issue #107
> 实现边界：`MxFramework.Animation` noEngine contract 已落地；`MxFramework.Animation.Unity` 提供首版 Unity Playables backend；`MxFramework.Combat.Animation.Unity` 提供 Combat 到 MxAnimation 的 Unity 表现桥；`MxFramework.Editor.Animation` 提供最小 clip registry authoring / export / validation。

## 职责

Animation 是表现层契约。它接收 play、stop、crossfade 这类 presentation 请求，通过 `ResourceKey` 引用动画资源，并输出只读 diagnostics。它不参与 Combat hash、replay、命中、取消、无敌、伤害或其他权威逻辑。

`MxFramework.Animation` 必须保持 `noEngineReferences=true`，不依赖 `UnityEngine` 或 `UnityEditor`，也不保存 Unity object、GUID 或 `Assets/...` path。

Unity Playables 接入放在 `MxFramework.Animation.Unity`，可以引用 UnityEngine / Playables / Resources.Unity，并通过 `IResourceManager` 加载 `AnimationClip`。

## 模块边界

| 模块 | 状态 | 依赖 | 职责 |
|------|------|------|------|
| `MxFramework.Animation` | MVP | Resources | layer id、play / stop / crossfade request、animation set definition、fade state、diagnostics、backend interface |
| `MxFramework.Animation.Unity` | MVP | Animation、Resources、Resources.Unity、UnityEngine Playables | `UnityPlayablesAnimationBackend`、clip load、fallback、manual tick、graph shutdown、handle ownership |
| `MxFramework.Combat.Animation.Unity` | MVP | Combat、Animation、Animation.Unity | 订阅 `CombatActionRunner` lifecycle / frame presentation events，转成 MxAnimation play / stop / crossfade 请求和 presentation event dispatch |
| `MxFramework.Editor.Animation` | MVP | Editor、Animation、Resources、UnityEditor | Clip registry authoring asset、mapping export、catalog validation、最小 Inspector validation |

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
| `MxAnimationSetDefinition` | actor / archetype / skin 的 presentation binding 集合，clip 使用 `ResourceKey` |
| `MxAnimationActionBinding` | action key 或 binding id 到 clip、layer、speed、loop 和 presentation events 的映射 |
| `MxAnimationClipRegistry` | 从 `ResourceCatalog` 发现的 animation clip registry，只保存 `ResourceKey` 和 catalog entry hash |
| `MxAnimationClipRegistryBuilder` | 从正式 `ResourceCatalog` 过滤 `ResourceTypeIds.AnimationClip` 并生成 registry |
| `IMxAnimationMappingProvider` | 按 animation set id 提供 `MxAnimationSetDefinition` 的最小 provider surface |
| `MxAnimationStaticMappingProvider` | code-only / early validation provider；仍只消费 noEngine definition，不绕过资源系统 |
| `MxAnimationSetDefinitionHasher` | 对 set id、version、default/fallback、binding、events 生成稳定 `sha256:` definition hash |
| `MxAnimationSetDefinitionValidator` | 校验 set id/version/hash、default/fallback、catalog entry、clip type、重复 binding/action key |
| `MxAnimationClipRegistryAsset` | Editor-only registry authoring asset，可引用 `AnimationClip` 但不进入 runtime DTO |
| `MxAnimationClipRegistryExporter` | 从 Editor registry 导出 noEngine `MxAnimationSetDefinition` 和 validation report |
| `MxAnimationPlayRequest` | 播放请求，可指定 binding/action 或直接 clip key |
| `MxAnimationStopRequest` | 停止请求，支持 layer 和 fade out duration |
| `MxAnimationCrossFadeRequest` | crossfade 请求，支持 target clip、fade duration、start offset 和 outgoing release policy |
| `MxAnimationDiagnosticSnapshot` | backend、graph、resident default/fallback、layer state、active fades、recent requests/errors |
| `IMxAnimationBackend` | 最小 backend surface：play、stop、crossfade、tick、snapshot、release |
| `UnityPlayablesAnimationBackend` | Unity Playables MVP backend，使用 manual `Tick(deltaTime)` 推进 |

## Combat Presentation Bridge

`MxFramework.Combat.Animation.Unity` 是独立 Unity bridge assembly，不放入 `MxFramework.Combat` noEngine assembly。它的默认 action key 策略是 `action:<combatActionId>`，并用该 key 查找 `MxAnimationSetDefinition` / `MxAnimationActionBinding`。

公开类型：

| 接口/类型 | 用途 |
|-----------|------|
| `CombatMxAnimationUnityBridge` | 订阅 `CombatActionRunner.ActionStarted`、`ActionCanceled`、`ActionFinished`、`ActionFrameEventRaised`，按 entity 路由到注册的 `IMxAnimationBackend` |
| `CombatMxAnimationBridgeOptions` | 配置 start 使用 `Play` 或 `CrossFade`，cancel / finish 使用 `Stop` 或 `CrossFade`，以及 fade duration、action key prefix、frame event binding |
| `CombatMxAnimationFrameEventBinding` | 可选 explicit bridge config，用 Combat event correlation keys 匹配并解析到表现事件 |
| `ICombatMxAnimationPresentationEventSink` | 接收已解析的 presentation events，供 VFX / SFX / camera / footstep / UI feedback 层消费 |
| `CombatMxAnimationPresentationEventDispatch` | presentation event dispatch payload，保留 Combat entity、action、action instance、world frame、local frame、原始 `CombatActionFrameEvent` 和 correlation id |
| `CombatMxAnimationBridgeDiagnosticSnapshot` | bridge 最近请求 / dispatch diagnostics，用于排查 mapping 和 lifecycle |

Lifecycle 策略：

- action started 默认发 `MxAnimationCrossFadeRequest`，可通过 options 改为 `MxAnimationPlayRequest`。
- action canceled / finished 默认发 `MxAnimationStopRequest`，可通过 options 改为 crossfade 到指定 binding 或 default clip。
- bridge 只向表现 backend 发送请求，不把 backend state、Playable time、Animator state 或 normalized time 写回 Combat。

Frame event mapping 策略：

- 首选 `CombatMxAnimationFrameEventBinding` 显式配置；配置可以直接提供 `MxAnimationPresentationEvent`，也可以指向 animation set / action binding 中的 presentation event id。
- 没有显式配置时，从当前 `MxAnimationActionBinding.PresentationEvents` 查找 `TimeDomain == CombatFrame` 或 `PresentationFrame`、`Time == localFrame`、`EventId == event:<CombatActionFrameEvent.EventId>` 或纯数字 event id 的事件。
- `CombatActionFrameEvent.EventId`、`SourceOrder`、`IntPayload` 只作为 deterministic correlation / matching keys。VFX / SFX / Camera / Footstep / UI kind 与 `ResourceKey` payload 必须来自 `MxAnimationPresentationEvent` 或显式 bridge config。

Legacy coexistence:

- 旧 `MxFramework.Runtime.Unity.CombatAnimationUnityModule` / `CombatAnimatorDriver` 保持可用，但仍是 opt-in。
- 新 `CombatMxAnimationUnityBridge` 不创建、不注册、不调用旧 driver。项目层 composition root 应在同一 entity 上选择 legacy Animator bridge 或 MxAnimation bridge 之一，避免同一 Combat event 双触发表现。

## 使用约定

- DTO 中的 clip、VFX、SFX、camera profile 等表现资源一律使用 `ResourceKey`。
- clip key 的 `TypeId` 使用 `ResourceTypeIds.AnimationClip`。
- `MxAnimationSetDefinition.DefinitionHash` 是 mapping 内容 hash，用于加载侧和 #109 warmup / resource validation 检测过期 mapping。
- `MxAnimationClipRegistryAsset` 只属于 Unity Editor authoring。运行时和 Demo 不得从该 asset 直接取 `AnimationClip`，必须通过导出的 `MxAnimationSetDefinition` + `ResourceManager` 加载。
- 当前 Mapping Editor 是最小 Inspector authoring / validation 入口；复杂搜索、预览和 timeline scrubber 不在 #107 范围内。
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
- play / stop state transition 和非 resident handle release。
- requested clip load failure fallback 到 resident fallback，并输出 diagnostics。
- crossfade 期间 outgoing handle 保持到 fade 完成后释放。
- backend release destroy graph 并释放 default、fallback 和当前 clip handles。
- Combat bridge action started -> play / crossfade、cancel / finish -> stop、frame event -> binding presentation event dispatch、correlation diagnostics、legacy opt-in coexistence、noEngine Combat asmdef 边界。
