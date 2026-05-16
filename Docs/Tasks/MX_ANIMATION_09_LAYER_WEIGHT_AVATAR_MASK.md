# MxAnimation 09：Layer Weight + AvatarMask MVP

> Issue: #108
> 状态：Implemented
> 任务等级：S3
> 日期：2026-05-16

## 目标

补齐 MxAnimation 第二阶段的 layer mixing 基础能力，让上半身攻击、下半身移动等表现混合可以走正式 mapping / resource loading 流程，而不是从 Demo 或场景脚本直接引用 Unity asset。

本切片完成最小可验证路径：

- noEngine `MxAnimationLayerDefinition` 描述 layer id、profile id、default weight、blend mode 和 AvatarMask `ResourceKey`。
- `MxAnimationSetDefinition` 持有 layer definitions，并把 layer definition 纳入稳定 `DefinitionHash`。
- `IMxAnimationBackend.SetLayerWeight` 支持 immediate set 和按 presentation delta 的 weight transition。
- `UnityPlayablesAnimationBackend` 使用 `AnimationLayerMixerPlayable` 的 root input weight 表达 layer weight，clip fade 仍保留在每层内部 mixer。
- AvatarMask 通过 `IResourceManager.LoadAsync<AvatarMask>` 加载、持有和释放，不绕过正式资源流程。
- Editor clip registry 可以配置 layer 和 AvatarMask key，exporter 只输出 noEngine definition。

## 边界

Runtime / noEngine 边界：

- `MxFramework.Animation` 只保存 `ResourceKey`、layer id、profile id、weight、blend mode 和 diagnostics。
- noEngine 层不保存 `AvatarMask`、`AnimationClip`、Unity object、GUID、`Assets/...` path 或 Playable index。
- layer weight 是 presentation state，不进入 Combat hash、replay hash、命中、取消、伤害或权威位移。

Unity backend 边界：

- AvatarMask 和 AnimationClip 一样由 backend 通过 `IResourceManager` 获得 handle，并在 backend `Release` 时释放。
- AvatarMask 加载失败只记录 layer mask diagnostics，不阻断该 layer 的 clip play / crossfade / fallback。
- 当前只提供 override / additive layer mode 和 0..1 layer weight。复杂 layer profile blending、Animator Controller bridge、Blend Tree 替代和 Timeline preview 不在本切片范围。

## 新增接口

- `MxAnimationLayerDefinition`
- `MxAnimationLayerBlendMode`
- `MxAnimationLayerMaskStatus`
- `MxAnimationLayerWeightRequest`
- `ResourceTypeIds.AvatarMask`
- `IMxAnimationBackend.SetLayerWeight`

## Validation

`MxAnimationSetDefinitionValidator` 对 layer 增加稳定 issue code：

- `LayerIdMissing`
- `DuplicateLayerId`
- `AvatarMaskTypeMismatch`
- `AvatarMaskCatalogTypeMismatch`
- `AvatarMaskCatalogEntryMissing`

`MxAnimationClipRegistryExporter` 对 Editor authoring 增加：

- `AvatarMaskReferenceMissing`

## 与后续任务关系

- #109 warmup / resource validation 可以把 `AvatarMask` 作为正式 catalog key 一起预热，避免进入战斗后第一次加载 mask。
- #111 locomotion / simple blend 可以复用 layer weight transition，但不应把 locomotion 参数写入 Combat authority。
- #112 bake 可以继续使用 runtime hit / trace 数据，不需要也不应该从 AvatarMask 或 Playable pose 反推权威判定。
- 第三阶段如果引入 PlayableGraph 后端抽象，应保留当前 noEngine layer definition 和 backend-owned resource handle 边界。

## 验收清单

- `MxFramework.Animation` 仍保持 `noEngineReferences=true`。
- layer definition、AvatarMask key 和 default weight 进入 stable mapping hash。
- Unity backend layer weight 与 clip fade 相互独立。
- AvatarMask 通过 `ResourceManager` 加载，backend release 会释放 mask handle。
- AvatarMask 缺失或类型错误有明确 diagnostics，不静默 fallback 到任意 Unity object。
- focused tests 覆盖 layer hash、validator、Editor export、Unity backend layer weight、mask load / release / failure 和 resource type resolver。
- `Docs/Interfaces/Animation.md`、`Docs/Interfaces/Resources.md` 与 `Docs/USAGE.md` 已记录接入方式。
