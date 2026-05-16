# MxAnimation 08：Clip Registry + Mapping Editor

> Issue: #107
> 状态：Minimal Provider + Editor Authoring MVP
> 任务等级：S3
> 日期：2026-05-16

## 目标

提供 MxAnimation 第二阶段的数据入口：从正式 `ResourceCatalog` 发现 animation clips，用 Editor-only registry asset 编辑 action / binding 映射，并导出 noEngine `MxAnimationSetDefinition`。

本切片先完成最小可验证路径，支撑后续 #111 / #112：

- noEngine clip registry 从 catalog 中筛选 `ResourceTypeIds.AnimationClip`。
- noEngine mapping provider 按 `animationSetId` 提供 `MxAnimationSetDefinition`。
- `MxAnimationSetDefinition` 产出稳定 `DefinitionHash`。
- Editor registry asset 可以引用 `AnimationClip`，但导出 runtime DTO 时只保留 `ResourceKey`。
- Editor Inspector 提供最小 Validate Mapping Structure 入口；完整 catalog 兼容性校验由 exporter / pipeline 传入 `ResourceCatalog` 后执行，完整搜索、预览、scrubber 和复杂 timeline UI 后续再做。

## 边界

Runtime / noEngine 边界：

- `MxFramework.Animation` 只依赖 `MxFramework.Resources`。
- Runtime DTO 不保存 `AnimationClip`、`UnityEngine.Object`、GUID、`Assets/...` path 或 `AssetDatabase` 信息。
- `MxAnimationSetDefinition` 只保存 set id、version、definition hash、default/fallback `ResourceKey`、binding 和 presentation events。

Editor authoring 边界：

- `MxAnimationClipRegistryAsset` 是 Editor authoring asset，可以保存 `AnimationClip` 引用，方便 Unity 内检查。
- `MxAnimationClipRegistryExporter` 只把 registry 导出为 `MxAnimationSetDefinition`。
- 导出的 runtime definition 不直接注入 backend；backend 仍通过 `ResourceManager` + `ResourceKey` 加载 `AnimationClip`。

## 新增接口

- `MxAnimationClipRegistryEntry`
- `MxAnimationClipRegistry`
- `MxAnimationClipRegistryBuilder`
- `IMxAnimationMappingProvider`
- `MxAnimationStaticMappingProvider`
- `MxAnimationSetDefinitionHasher`
- `MxAnimationSetDefinitionValidator`
- `MxAnimationClipRegistryAsset`
- `MxAnimationClipRegistryExporter`
- `MxAnimationClipRegistryAssetEditor`

## Validation

`MxAnimationSetDefinitionValidator` 使用 `ResourceCatalogValidationReport` 输出稳定 issue code：

- `CatalogMissing`
- `DefaultClipMissing`
- `FallbackClipMissing`
- `ClipTypeMismatch`
- `ClipCatalogTypeMismatch`
- `ClipCatalogEntryMissing`
- `DuplicateBindingId`
- `DuplicateActionKey`
- `ActionKeyInvalid`
- `AnimationSetHashMismatch`

Catalog lookup 按 `ResourceKey` 的 id、type、variant 和 package 进行匹配；当 mapping key 没有 package 时，允许匹配 catalog fallback package。这样既支持 package-qualified catalog，也避免同 id 不同 type 的误判。

Inspector 的结构校验不传 `ResourceCatalog`，因此只检查 authoring 结构、ResourceKey 形状、重复 binding/action key 和 definition hash。正式导出、CI 或资源 warmup 校验必须传入 `ResourceCatalog`，才能检查 catalog entry、typeId、variant 和 package。

## 与后续任务关系

- #109 warmup / resource version validation 可以使用 `DefinitionHash`、registry version 和 catalog hash 做兼容性校验。
- #111 locomotion demo 可以先消费 `IMxAnimationMappingProvider`，但可验收 demo 仍必须走正式 `ResourceKey` / `ResourceCatalog` / `ResourceManager` 加载流程。
- #112 bake 可以复用 registry clip 列表作为 bake 输入集合，不能把 Unity clip 引用写入 runtime DTO。

## 验收清单

- noEngine DTO 和 validator 已落在 `MxFramework.Animation`。
- Editor authoring asset / exporter 已落在 Editor-only assembly。
- `MxFramework.Animation` 仍保持 `noEngineReferences=true`。
- focused tests 覆盖 registry catalog filtering、mapping provider lookup、stable hash、缺失 catalog / fallback、重复 action binding、错误 typeId 和 Editor registry export。
- `Docs/Interfaces/Animation.md` 与 `Docs/USAGE.md` 已记录接入方式。
