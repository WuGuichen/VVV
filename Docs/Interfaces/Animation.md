# Animation 接口

> 状态：MVP Implemented
> 来源：`Docs/Tasks/MX_ANIMATION_01_DESIGN_CONTRACT.md`、Gitea Issue #94
> 实现边界：`MxFramework.Animation` noEngine contract 已落地；`MxFramework.Animation.Unity` 提供首版 Unity Playables backend。Combat bridge 不在本接口范围。

## 职责

Animation 是表现层契约。它接收 play、stop、crossfade 这类 presentation 请求，通过 `ResourceKey` 引用动画资源，并输出只读 diagnostics。它不参与 Combat hash、replay、命中、取消、无敌、伤害或其他权威逻辑。

`MxFramework.Animation` 必须保持 `noEngineReferences=true`，不依赖 `UnityEngine` 或 `UnityEditor`，也不保存 Unity object、GUID 或 `Assets/...` path。

Unity Playables 接入放在 `MxFramework.Animation.Unity`，可以引用 UnityEngine / Playables / Resources.Unity，并通过 `IResourceManager` 加载 `AnimationClip`。

## 模块边界

| 模块 | 状态 | 依赖 | 职责 |
|------|------|------|------|
| `MxFramework.Animation` | MVP | Resources | layer id、play / stop / crossfade request、animation set definition、fade state、diagnostics、backend interface |
| `MxFramework.Animation.Unity` | MVP | Animation、Resources、Resources.Unity、UnityEngine Playables | `UnityPlayablesAnimationBackend`、clip load、fallback、manual tick、graph shutdown、handle ownership |
| `MxFramework.Combat.Animation.Unity` | Deferred | Combat、Animation、Animation.Unity | 后续 Issue 才实现 Combat event 到 presentation request 的 bridge |

依赖方向：

```text
MxFramework.Resources
  <- MxFramework.Animation
      <- MxFramework.Animation.Unity
```

Combat 不引用 Animation.Unity。Unity animation time 不反向驱动 Combat authority。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `MxAnimationLayerId` | 稳定 layer id value object；默认 `base`，不等同 Unity Animator layer index |
| `MxAnimationSetDefinition` | actor / archetype / skin 的 presentation binding 集合，clip 使用 `ResourceKey` |
| `MxAnimationActionBinding` | action key 或 binding id 到 clip、layer、speed、loop 和 presentation events 的映射 |
| `MxAnimationPlayRequest` | 播放请求，可指定 binding/action 或直接 clip key |
| `MxAnimationStopRequest` | 停止请求，支持 layer 和 fade out duration |
| `MxAnimationCrossFadeRequest` | crossfade 请求，支持 target clip、fade duration、start offset 和 outgoing release policy |
| `MxAnimationDiagnosticSnapshot` | backend、graph、resident default/fallback、layer state、active fades、recent requests/errors |
| `IMxAnimationBackend` | 最小 backend surface：play、stop、crossfade、tick、snapshot、release |
| `UnityPlayablesAnimationBackend` | Unity Playables MVP backend，使用 manual `Tick(deltaTime)` 推进 |

## 使用约定

- DTO 中的 clip、VFX、SFX、camera profile 等表现资源一律使用 `ResourceKey`。
- clip key 的 `TypeId` 使用 `ResourceTypeIds.AnimationClip`。
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
