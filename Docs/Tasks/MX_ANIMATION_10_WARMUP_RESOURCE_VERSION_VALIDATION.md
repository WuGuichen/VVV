# MxAnimation 10：Warmup + Resource Version Validation

> Issue: #109
> 状态：Implemented
> 任务等级：S3
> 日期：2026-05-17

## 目标

让 MxAnimation 在进入战斗、生成角色或 late join 前预热 animation set 需要的资源，并在进入表现层前校验本地 animation set、catalog、clip registry 与同步状态是否一致。

本切片完成最小可验证路径：

- `MxAnimationSetDefinition` 可声明 `MxAnimationWarmupDefinition`。
- warmup definition 支持 preload group id、required keys、catalog labels、failFast，以及是否自动包含 default/fallback/action clips 和 layer AvatarMask。
- `MxAnimationWarmupService` 复用现有 `IResourcePreloadService`，不新增资源子系统。
- warmup diagnostics 覆盖 animation set hash、catalog hash、clip registry version、具体 clip registry entry hash、catalog validation 和 preload partial failure。
- warmup result 持有 `ResourceGroupHandle`，释放通过 service 转回 `ResourcePreloadService.ReleaseGroup`。

## 边界

Runtime / noEngine 边界：

- `MxFramework.Animation` 仍只依赖 `MxFramework.Resources`。
- warmup 只保存 `ResourceKey`、label、version/hash 字符串和结构化 diagnostics。
- 不保存 Unity `AnimationClip`、`AvatarMask`、GUID、`Assets/...` path、PlayableGraph 或 Animator state。

资源系统边界：

- 不修改 `IResourceManager` 公共契约。
- 不新增 `MxFramework.Animation.Resources`。
- 不引入 Addressables 硬依赖、RemoteBundle 覆盖策略或 Mod 动画包覆盖策略。
- partial failure 由 `ResourcePreloadResult.Errors` 逐个映射为 `MxAnimationWarmupIssue`，不能静默变成空播。

## 新增接口

- `MxAnimationWarmupDefinition`
- `MxAnimationWarmupRequest`
- `MxAnimationWarmupResult`
- `MxAnimationWarmupIssue`
- `MxAnimationWarmupIssueCodes`
- `MxAnimationWarmupService`
- `MxAnimationClipRegistry.TryFind`

## Validation

Warmup issue code：

- `AnimationSetMissing`
- `AnimationSetIdMismatch`
- `AnimationSetVersionMismatch`
- `AnimationSetHashMismatch`
- `ResourceCatalogHashMismatch`
- `ClipRegistryVersionMismatch`
- `ClipRegistryCatalogHashMismatch`
- `ClipRegistryEntryMissing`
- `ExpectedClipRegistryEntryMissing`
- `ClipRegistryEntryHashMismatch`
- `CatalogValidationFailed`
- `PreloadOperationFailed`
- `PreloadResourceFailed`

Diagnostics 必须尽量定位到具体对象：

- animation set id/version/hash；
- catalog id/hash；
- clip registry version；
- clip `ResourceKey`；
- catalog validation issue code；
- provider / address / `ResourceErrorCode`。

## 验收清单

- warmup 走正式 `ResourcePreloadService` / `ResourcePreloadPlan` / `ResourceManager` / `ResourceCatalog`。
- `MxFramework.Animation` 仍保持 noEngine。
- hash/version mismatch 会阻断预热并输出具体 field、expected、actual。
- missing clip、wrong type、entry hash mismatch 和 partial preload failure 有结构化 diagnostics。
- release warmup group 后，另一个 consumer 持有的同一 clip 不会被卸载。
- focused tests 覆盖 success、sync mismatch、wrong type、partial failure、ref-count 和 entry hash mismatch。
- `Docs/Interfaces/Animation.md`、`Docs/Interfaces/Resources.md` 与 `Docs/USAGE.md` 已记录接入方式。
