# MxAnimation 12 - 1D Locomotion Blend Demo

> 来源：Gitea Issue #111
> 状态：Implemented

## Scope

- 在 `MxFramework.Animation` noEngine 层补充 1D blend definition、point、request、weight result 和 deterministic weight calculator。
- 让 `MxAnimationSetDefinition` 持有 1D blend definitions，并把 blend point clip 纳入 stable definition hash、mapping validation 和 warmup required keys。
- 在 `UnityPlayablesAnimationBackend` 中支持 `SetBlend1D`，通过正式 `IResourceManager` 加载 weighted clip，并把 blend id、参数和权重输出到 diagnostics。
- 扩展 MxAnimation Play Mode smoke 场景，使 Skeleton model 通过正式 sample catalog 加载 idle / walk / run blend、upper body AvatarMask 和 upper attack layer。

## Boundaries

- 1D blend 只影响表现层 Playables 权重，不写回 Combat authority、Replay hash、命中、取消、伤害或权威位移。
- Demo 不直接把 `AnimationClip` 或 `AvatarMask` 注入 backend；所有资源都必须经过 `ResourceKey`、`ResourceCatalog`、provider 和 `ResourceManager`。
- upper body layer 使用 `MxAnimationLayerDefinition` + `ResourceTypeIds.AvatarMask`，不从场景脚本绕过正式 mapping / resource loading 流程。
- 当前任务只提供 1D locomotion blend 的可运行垂直切片，不实现通用 2D blend tree、PlayableGraph 后端抽象或 runtime authoring editor。

## Manual Validation

1. 执行 `MxFramework / MxAnimation / Generate Play Mode Smoke Scene` 重新生成 `Assets/Scenes/MxAnimationPlayModeSmoke.unity` 和 upper body mask。
2. 打开 `Assets/Scenes/MxAnimationPlayModeSmoke.unity` 并进入 Play Mode。
3. 按 `I` / `O` / `P` 验证 idle / walk / run speed parameter 和 HUD blend weights。
4. 按 `Space` 验证 upper body attack layer 叠加播放，locomotion blend 不被 Combat action finish 清空。

## Validation

- `AnimationContractTests` 覆盖 1D blend 权重计算和 definition hash。
- `UnityPlayablesAnimationBackendTests` 覆盖 weighted clip 加载、diagnostics 和 upper body layer 共存。
- `MxAnimationWarmupTests` / smoke demo tests 覆盖 blend clip warmup、AvatarMask catalog path 和 Play Mode smoke 场景绑定。
- `CombatMxAnimationUnityBridgeTests` 覆盖新增 backend surface 后的 bridge test double。
