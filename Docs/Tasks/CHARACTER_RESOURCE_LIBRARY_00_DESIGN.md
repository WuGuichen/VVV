# Authoring Resource Manager 00：全局资源发现、选择、编译与运行时编排设计

> 状态：草案
> 范围：全局 Authoring Resource Manager、独立资源管理器、跨编辑器字段资源选择器、Authoring Compiler 资源计划、Unity / FMOD / runtime catalog 同步、运行时资源编排
> 交付等级：下一阶段设计契约
> 前置：#221 Character Resource Package C0、#223 package-local resource catalog、#224 Authoring Compiler、#240-#246 CharacterStudio C1 MVP、`CHARACTER_RESOURCE_PACKAGE_C2_UNITY_ASSET_SYNC.md`

## 背景

前一版设计把“资源库”过度放在 CharacterStudio 和角色包语境下，这会导致一个错误边界：资源管理器看起来只服务角色包。实际目标应当是全局 Authoring Resource Manager：它是所有外部编辑器发现、筛选、选择、导入和同步资源的统一入口。CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 都只是它的消费者，角色包 `resource_catalog.json` 也只是其中一个 provider 的数据源。

新的目标链路是：

```text
Unity AssetDatabase / MxFramework.Resources catalog / FMOD cache / external files / generated assets / package-local catalogs
  -> Authoring Resource Manager
       unified resource item, provider status, picker contract, diagnostics, reference graph
  -> Editor Consumers
       CharacterStudio / Animation Editor / Combat Editor / VFX Editor / UI Editor / CLI
  -> ResourceSelectionRef
       config fields store stable authoring selections
  -> Authoring Compiler
       resolve selections into runtime catalogs, audio manifests and domain resource plans
  -> Runtime
       only consumes compiled plans and existing MxFramework.Resources catalogs
```

角色资源包仍然需要这条链路，但它不再是资源管理器的中心。CharacterStudio 不负责常驻展示全量资源，也不拥有资源库；它只在编辑具体字段时打开由 `ResourceFieldSpec` 驱动的资源选择器。

如果系统只停留在“编辑器能看见资源”和“Unity 能导入资源”，运行时仍会缺一层明确编排：

```text
Authoring Resource Manager
  -> 选择、校验、引用关系
Authoring Compiler
  -> 编译成运行时资源计划
Runtime Resource Catalog / Character Resource Plan
  -> preload / acquire / release
Runtime Resource Orchestrator
  -> ResourceManager / Animation / Audio / Presentation
```

本设计把资源系统明确拆成四类对象：

| 对象 | 面向对象 | 回答的问题 |
| --- | --- | --- |
| Authoring Resource Manager | 所有外部编辑器 / CLI / Authoring server | 项目中有哪些可用资源、来自哪个 provider、能否被当前字段选择 |
| Provider Catalogs | Unity / package / FMOD / runtime catalog / external staging | 每类来源自己的同步状态、源身份、导入结果和 diagnostics |
| Authoring Compiler Outputs | 运行时加载、Audio、角色/动画/战斗表现计划 | 编辑期选择如何编译成运行时可消费数据 |
| Runtime Resource Plans | Runtime Spawn / Animation / Audio / Presentation | 运行时要加载什么、何时加载、失败策略和释放策略是什么 |

这些对象不能合并成一个 catalog。编辑期可见资源不一定是运行时可加载资源：外部 `.fbx` 不能被 Player 直接加载，Unity-only asset 可能只用于编辑预览，FMOD event 不是 `AudioClip`，一个导入源也可能生成多个 prefab、clip、material 和 preview asset。

## 核心原则

- Authoring Resource Manager 是全局“可用资源集合和选择桥接”，不是“当前角色引用集合”，也不是 `MxFramework.Resources` runtime loader 的替代品。
- Character package、Unity AssetDatabase、现有 `MxFramework.Resources.ResourceCatalog`、FMOD snapshot、external import staging 和 generated assets 都是 provider；任何单一 provider 都不能成为资源管理器的中心。
- CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 通过同一套 query / inspect / picker API 消费资源管理器。
- 配置字段保存编辑期选择引用；Authoring Compiler 再解析成运行时绑定。
- Runtime 不直接读取 `resource_catalog.json`、`unity_resource_catalog.json`、`fmod_audio_library_snapshot.json` 或 Authoring Resource Manager 的聚合索引。
- Runtime 只消费编译产物：现有 `MxFramework.Resources.ResourceCatalog` 语义的 runtime catalog、domain resource plan、audio cue manifest。
- `resourceKey` 只代表可由 `ResourceManager` 加载的运行时资源；它不是所有资源项的通用 ID。
- FMOD event / bank path 不进入普通 Resource Catalog，也不伪装成 `ResourceTypeIds.AudioClip`。
- Runtime resource catalog 复用现有 `MxFramework.Resources.ResourceCatalog` / `ResourceCatalogEntry` 语义，不另造并行资源系统。
- 资源删除默认先看引用图；删除角色引用不删除资源，未引用资源只标记 orphan。

## 总体链路

```text
Unity AssetDatabase
MxFramework.Resources Runtime Catalog
Character Package Resource Catalog
FMOD Event/Bank Snapshot
External Import Staging
Generated Assets
  -> Authoring Resource Manager
       provider adapters, unified resource items, compatibility, preview, diagnostics, reference graph
  -> ResourceSelectionRef
       editor config fields store stable authoring selections
  -> Authoring Compiler
       resolves selections against providers and emits runtime outputs
  -> Runtime Resource Catalog / Domain Resource Plan / AudioCue Manifest
       existing MxFramework.Resources catalog entries plus domain-specific plans
  -> Runtime Orchestrators
       ResourcePreloadService + IResourceManager + Animation/Audio/FMOD/Presentation warmup
```

## Authoring Resource Manager

Authoring Resource Manager 负责编辑期可见、筛选、选择和资源桥接：

- 有哪些资源。
- 资源来自 Unity internal asset、现有 runtime catalog、角色包、外部文件、FMOD metadata 还是生成资产。
- 资源的 kind、usage、标签、预览和兼容性。
- 资源是否成功导入 Unity。
- 资源是否可运行时加载，或只是 editor-only / preview-only / audio-only。
- 当前被哪些角色、武器、动画、音频、override 或预览配置引用。
- 哪些资源能被当前编辑器字段选择，以及选择后应写回哪种引用形式。

Authoring Resource Manager 不负责：

- 当前角色运行时装备了哪件武器。
- 某个 Runtime 实例当前持有哪些 resource handles。
- FMOD bank 的 runtime 生命周期。
- 具体 Animation profile / Combat action 的归属决策。
- 替代 Unity AssetDatabase、Addressables、AssetBundle 或 `MxFramework.Resources.IResourceManager` 的加载实现。

## Provider Model

所有来源都通过 provider adapter 暴露给 Authoring Resource Manager。Provider 负责读取本来源的真实状态，资源管理器负责聚合、筛选、诊断和选择输出。

| Provider | 数据来源 | 典型资源 | 说明 |
| --- | --- | --- | --- |
| `unityAssetDatabase` | Unity `AssetDatabase`、GUID、sub-assets、importer metadata | prefab、model、animation clip、texture、material、AudioClip | Unity 内部资源发现和编辑期预览来源；Player 是否可加载要由 runtime catalog 或编译产物决定 |
| `runtimeCatalog` | 现有 `MxFramework.Resources.ResourceCatalog` / build catalog | runtime prefab、texture、material、bundle asset | 只表示 `IResourceManager` 可加载的运行时资源 |
| `characterPackage` | package-local `resource_catalog.json`、manifest、geometry/config | 角色模型、武器模型、角色包贴图、包内配置 | 角色资源包 provider；不是全局资源管理器中心 |
| `fmod` | FMOD event / bank / parameter snapshot | FMOD event、bank、parameter metadata | 进入选择列表，但不进入普通 Resource Catalog |
| `externalImportStaging` | 用户选择的文件/文件夹、导入暂存目录 | `.fbx`、`.glb`、`.png`、`.wav`、`.json` | 导入前可见，运行时默认不可加载 |
| `generatedAssets` | Authoring / Unity 生成产物 | preview thumbnail、generated prefab、compiled config | 必须记录可重建来源和生成状态 |

Provider 输出不能互相覆盖真实身份。例如 Unity GUID、runtime `resourceKey`、FMOD GUID/path、package-local key 都可以映射到同一个 authoring item，但它们不是同一种 ID。

### Authoring Resource Item Identity

所有资源卡片都有统一编辑期身份：

| 字段 | 说明 |
| --- | --- |
| `resourceId` | 资源管理器项 ID，用于 UI、API 和选择器；不要求可运行时加载 |
| `libraryItemId` | 兼容字段；等价于 `resourceId`，旧文档/接口迁移期可保留 |
| `stableId` | 长期稳定 ID，用于跨重命名、重复导入和引用持久化 |
| `displayName` | 可读名称 |
| `kind` | `Model`、`Animation`、`Texture`、`Material`、`AvatarMask`、`Vfx`、`Audio`、`Config`、`Generated` |
| `usage` | 具体用途，例如 `characterModel`、`weaponModel`、`animationClipGroup`、`fmodEvent` |
| `sourceProviderId` | `unityAssetDatabase`、`runtimeCatalog`、`characterPackage`、`fmod`、`externalImportStaging`、`generatedAssets` |
| `sourceKind` | `ExternalFile`、`UnityAsset`、`RuntimeCatalogAsset`、`PackageResource`、`FmodLibrary`、`GeneratedAsset` |
| `bindingKind` | 见下节 |
| `providerBindings` | 同一资源在各 provider 中的 GUID、path、resourceKey、package key、FMOD path/guid 等 |
| `compatibility` | 骨架、avatar、body kind、slot、weapon class、坐标系、单位和 bounds |
| `preview` | 缩略图、预览 mesh、相机 preset、预览姿势 |
| `importStatus` | 编辑期 / Unity 导入状态 |
| `runtimeAvailability` | 运行时可用性 |
| `diagnostics` | 结构化问题 |

现有 package-local `resourceKey` 保留为兼容字段：它仍可标识包内源资源和当前 C0.6 `CharacterPackageResourceMapping` 输入，但不能作为所有资源管理器项的必填运行时身份。真正运行时可加载的 `resourceKey` 只来自 `runtimeCatalog` provider 或 Authoring Compiler 输出。

### Binding Kind

`BindingKind` 明确资源项如何被选择、编译或运行时消费：

| 值 | 说明 |
| --- | --- |
| `None` | 仅编辑期记录，尚无运行时绑定 |
| `UnityAsset` | Unity Editor 内部 asset，可用于编辑器预览、导入、生成，不自动代表 Player 可加载 |
| `PackageResource` | package-local authoring resource，可由对应 package compiler 处理 |
| `ResourceManagerAsset` | 会编译成普通 `ResourceCatalogEntry`，可由 `IResourceManager` 加载 |
| `UnityEditorOnlyAsset` | 仅 Unity Editor / 预览使用，不进入 Player runtime catalog |
| `ExternalSource` | 外部源文件或文件夹中的资源，需先导入或注册才能进入 Unity/runtime |
| `AudioEventDefinition` | FMOD event / snapshot 映射到 `AudioEventDefinition.Id` |
| `AudioCue` | 映射到项目层 audio cue，包含 event 和参数默认值 |
| `GeneratedPreviewOnly` | 缩略图、临时预览或可重建缓存 |

FMOD item 可以有 `resourceId` 和 `stableId`，但不应拥有普通 `resourceKey`。外部 `.fbx` 可以进入资源管理器，但在导入前 `runtimeAvailability=NotRuntimeLoadable`。Unity asset 可以有 GUID/path，但只有进入 runtime catalog 或被 compiler 生成 runtime entry 后才拥有 runtime `resourceKey`。

### Resource Runtime Availability

导入状态和运行时可用性分开：

| 维度 | 值 |
| --- | --- |
| `ResourceImportStatus` | `New`、`Clean`、`SourceChanged`、`UnityMissing`、`ImportFailed`、`Conflict`、`ManualOverride`、`OrphanCandidate` |
| `ResourceRuntimeAvailability` | `Unknown`、`RuntimeReady`、`RuntimeMissing`、`EditorOnly`、`PreviewOnly`、`AudioCueOnly`、`NotRuntimeLoadable` |

这样可以表达“导入是 Clean，但只适合 Editor 预览”、“FMOD event 已同步，但只能通过 AudioCue 播放”、“源文件存在但尚未生成运行时 asset”等状态。

## ResourceFieldSpec

每个配置字段都应声明自己的资源选择规则，避免各编辑器页面手写筛选逻辑。`ResourceFieldSpec` 是跨编辑器契约：CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 都用同一种 spec 打开资源选择器。

| 字段 | 说明 |
| --- | --- |
| `fieldKey` | 稳定字段 key，例如 `Character.Model`、`Weapon.Icon`、`Animation.Clip`、`CombatAction.HitSfx`、`Ui.Icon` |
| `editorKind` | 字段所属编辑器或配置域，例如 `CharacterStudio`、`AnimationEditor`、`CombatEditor`、`VfxEditor`、`UiEditor` |
| `displayName` | UI 可读名称 |
| `acceptedKinds` | 允许的 resource item kind |
| `acceptedUsages` | 允许的 usage |
| `acceptedProviderIds` | 允许的 provider；为空表示不限制来源 |
| `acceptedBindingKinds` | 允许的 binding kind |
| `requireRuntimeLoadable` | 是否必须能编译成运行时绑定 |
| `requireUnityImported` | 是否必须已有 Unity 导入结果 |
| `allowIncompatibleWithWarning` | 是否允许兼容性 warning 下选择 |
| `compatibilityFilter` | 当前 skeleton、avatar、body kind、weapon class、slot 等过滤条件 |
| `preloadPolicy` | 编译进入哪个计划组 |
| `outputKind` | `ResourceSelectionRef` 输出应解析成 runtimeResourceKey、Unity GUID、package key、AudioCueId、AudioEventDefinitionId 等 |

示例：

```json
{
  "fieldKey": "Character.Model",
  "acceptedKinds": ["Model"],
  "acceptedUsages": ["characterModel"],
  "acceptedBindingKinds": ["ResourceManagerAsset"],
  "requireUnityImported": true,
  "requireRuntimeLoadable": true,
  "preloadPolicy": "SpawnCritical",
  "outputKind": "ResourceKey"
}
```

```json
{
  "fieldKey": "CombatActionPresentation.HitSfx",
  "acceptedKinds": ["Audio"],
  "acceptedUsages": ["fmodEvent", "audioCue"],
  "acceptedBindingKinds": ["AudioEventDefinition", "AudioCue"],
  "requireRuntimeLoadable": false,
  "preloadPolicy": "AudioBank",
  "outputKind": "AudioCueId"
}
```

所有编辑器资源选择器统一使用：

```text
ResourcePicker.Open(ResourceFieldSpec spec, ResourceConsumerContext context)
  -> ResourceSelectionRef
```

`ResourceConsumerContext` 至少包含 `consumerKind`、`consumerStableId`、`scopeId`、可选 `packagePath`、当前 skeleton/avatar/slot/body/weapon/UI context，以及当前用户选择的 provider 过滤条件。

选择器显示规则：

- 绿色：完全匹配。
- 黄色：可选但有兼容性 warning。
- 灰色：不可选，并显示结构化原因。
- 红色：导入失败或 runtime 不可用。

## ResourceSelectionRef

配置字段不直接保存裸 `ResourceKey`、Unity path、FMOD path 或 GUID，而是保存编辑期选择引用：

| 字段 | 说明 |
| --- | --- |
| `resourceStableId` | 被选择的资源项 stable id |
| `libraryItemStableId` | 兼容字段；旧角色包配置迁移期可保留 |
| `sourceProviderId` | 选择时命中的 provider |
| `bindingKind` | 期望绑定类型：UnityAsset / RuntimeResource / PackageResource / AudioCue 等 |
| `expectedKind` | 选择时字段期望的 kind |
| `expectedUsage` | 选择时字段期望的 usage |
| `expectedHash` | 可选，用于检测源资源变化 |
| `unityGuid` | 可选；仅 Unity asset 选择或编译结果需要回指 Unity 时使用 |
| `unityAssetPath` | 可选；展示和诊断用，不作为稳定运行时身份 |
| `runtimeResourceKey` | 编译后可填；仅 `ResourceManagerAsset` 使用 |
| `providerResourceKey` | 可选；provider-local key，例如角色包 `resourceKey`、Unity GUID 或其他来源本地键 |
| `audioCueId` | 编译后可填；仅 audio cue 使用 |

编译前：

```json
{
  "resourceStableId": "charpkg.iron_vanguard.resource.model.body",
  "sourceProviderId": "characterPackage",
  "bindingKind": "ResourceManagerAsset",
  "expectedKind": "Model",
  "expectedUsage": "characterModel"
}
```

编译后：

```json
{
  "runtimeResourceKey": "char.iron_vanguard.model.body.prefab",
  "providerId": "memory",
  "hash": "sha256:..."
}
```

这避免外部路径、Unity GUID、FMOD path 直接散落在角色、武器、动画、表现、UI 或其他编辑器配置里。字段如果确实需要 Unity editor-only asset，也应通过 `bindingKind=UnityAsset` 明确表达，不能把 GUID 冒充成 runtime `resourceKey`。

## Authoring Compiler Resource Plan

Authoring Compiler 是编辑期选择与运行时消费之间的唯一编排层。

输入：

- 各编辑器配置：Character / Weapon / Animation / Presentation / VFX / UI / Config 等。
- Authoring Resource Manager 聚合索引。
- Provider catalogs：Unity Import Catalog、runtime catalog snapshot、package-local resource catalog、FMOD audio library snapshot、external import staging。
- Resource reference graph。

输出：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/runtime_resource_catalog.json
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/character_resource_plan.json
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/audio_cue_manifest.json
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/resource_validation_report.json
```

以上路径是角色包 compiler 的输出示例。全局资源管理器不强制所有 consumer 都写入 `CharacterPackages` 目录；Animation、Combat、VFX、UI 后续可以拥有各自 domain plan，但都必须复用同一套 `ResourceSelectionRef` 解析和 runtime catalog 语义。

编译流程：

1. 扫描所有 `ResourceSelectionRef`。
2. 根据 `ResourceFieldSpec` 校验 kind / usage / binding kind。
3. 从 Authoring Resource Manager 解析 stable selection。
4. 从对应 provider catalog 解析 Unity asset / runtime catalog entry / package source / FMOD event / external source。
5. 根据字段要求判断是否能生成 runtime binding。
6. 生成现有 `MxFramework.Resources.ResourceCatalog` 语义的 runtime catalog entries。
7. 生成角色资源计划或其他 domain resource plan，以及 audio cue manifest。
8. 输出结构化 diagnostics。

如果资源缺失或不可运行时加载，必须在编译期诊断，不等到 Runtime Spawn 才失败。

## Runtime Resource Catalog

`runtime_resource_catalog.json` 只包含 `ResourceManager` 能加载的资源，格式应映射到现有 `MxFramework.Resources.ResourceCatalog` / `ResourceCatalogEntry`。全局 Authoring Resource Manager 可以读取现有 runtime catalog 作为 provider，但 Runtime 只能读编译后的 runtime catalog，不读编辑期聚合索引：

| 字段 | 对应现有语义 |
| --- | --- |
| `resourceKey` | `ResourceCatalogEntry.Id` + `TypeId` + `Variant` + `PackageId` |
| `providerId` | `ResourceCatalogEntry.ProviderId`，例如 `memory`、`resources`、`assetBundle` |
| `address` | `ResourceCatalogEntry.Address` |
| `assetType` | `ResourceCatalogEntry.TypeId` |
| `hash` | `ResourceCatalogEntry.Hash` |
| `labels` | `ResourceCatalogEntry.Labels` |
| `dependencies` | `ResourceCatalogEntry.Dependencies` |
| `sizeBytes` | `ResourceCatalogEntry.Size` |
| `retainPolicy` | 使用现有 `ResourceRetainPolicy` / ProviderData 扩展表达 |

不进入 Runtime Resource Catalog：

- 外部 `.fbx` / `.glb` 源路径。
- Unity `AssetDatabase` GUID。
- FMOD editor cache。
- 缩略图生成源。
- editor-only preview asset。

## Character Resource Plan

`character_resource_plan.json` 是角色域的一个 domain resource plan，告诉 Runtime Spawn 如何使用 runtime catalog、Audio 和表现资源。它不是全局资源管理器的唯一产物；后续 Animation、Combat/VFX、UI 可以定义自己的 domain plan，但必须沿用同一套 resource ref 解析、preload policy、diagnostics 和 runtime catalog 语义。

计划组：

| 组 | 说明 | 失败策略 |
| --- | --- | --- |
| `SpawnCritical` | 生成角色必须加载：角色 prefab / model、基础材质、核心 collider prefab | `FailSpawn` |
| `PresentationCritical` | 角色可见前必须加载：基础表现 profile、基础 shader/material | `FailSpawn` 或 `UseFallbackVisual` |
| `EquipmentInitial` | 初始装备：武器 prefab、武器材质、挂点资源 | `UseFallbackEquipment` 或 `FailSpawn` |
| `AnimationWarmup` | locomotion、idle、基础 attack、hit reaction 等预热 | `FailSpawn` 或 `UseFallbackPose` |
| `VfxWarmup` | 命中特效、拖尾、buff 特效 | `SkipEffect` |
| `UiDeferred` | 头像、图标、角色卡图 | `ShowPlaceholder` |
| `Audio` | FMOD banks、AudioCue、AudioEventDefinition | `MuteMissingCue` |

示例：

```json
{
  "characterStableId": "char.iron_vanguard",
  "planHash": "sha256:...",
  "spawnCritical": {
    "required": true,
    "resources": ["char.iron_vanguard.model.body.prefab"],
    "failurePolicy": "FailSpawn"
  },
  "equipmentInitial": {
    "required": true,
    "resources": [
      "char.iron_vanguard.weapon.sword.prefab",
      "char.iron_vanguard.weapon.shield.prefab"
    ],
    "failurePolicy": "UseFallbackEquipment"
  },
  "audio": {
    "requiredBanks": ["Master", "Character"],
    "requiredCues": [500101, 500102],
    "failurePolicy": "MuteMissingCue"
  }
}
```

装备切换时应能从旧计划和新计划计算 diff：新增资源先 preload，共享资源保留，旧资源在切换提交和 fade 结束后释放。

## Runtime Resource Orchestrator

Runtime 不应让 Character Spawn、Animation、VFX、Audio 各自随意加载资源。新增运行时编排器负责统一生命周期：

```text
IResourcePlanOrchestrator
  Preload(plan)
  Acquire(plan)
  PrepareChange(session, nextPlan)
  CommitChange(session, diff)
  Release(session)

ICharacterResourceOrchestrator : IResourcePlanOrchestrator
  PreloadForSpawn(characterPlan)
  AcquireForSpawn(characterPlan)
  PrepareEquipmentChange(characterSession, nextCharacterPlan)
```

实现约束：

- Resource preload 复用现有 `ResourcePreloadService` 和 `ResourcePreloadPlan`。
- 资源 handle 由 `IResourceManager` 持有和释放。
- Audio warmup 走 Audio/FMOD 层，不走 `ResourceManager.Load<AudioClip>`。
- 通用 `ResourcePlanSession` 记录 plan hash、loaded resource handles、audio cue ids、banks 和 diagnostics；`CharacterResourceSession` 是角色域扩展。
- Release 幂等，重复释放只写 diagnostics。

## FMOD 与 AudioCue

FMOD 数据流：

```text
FMOD event:/Character/IronVanguard/SwordSlash
  -> fmod_audio_library_snapshot.json
  -> Authoring Resource Manager audio item
  -> AudioEventDefinition
  -> AudioCue
  -> Audio system plays cue with context and parameter defaults
```

FMOD library snapshot 只提供可选项。`AudioEventDefinition` 是项目音频定义，`AudioCue` 是角色、武器、技能、动画或 Combat presentation 使用的稳定引用。

需要 `AudioCue` 的原因：同一个 FMOD event 在不同上下文可能使用不同默认参数和 fallback policy，例如轻攻击、重攻击、破甲命中可以共用 event，但参数不同。

FMOD exporter：

```text
MxFramework/Audio/Export FMOD Audio Library
```

读取 Unity 侧 FMOD editor cache：

- `FMODUnity.EventManager.Events`
- `FMODUnity.EventManager.Banks`
- `FMODUnity.EventManager.Parameters`

输出：

```text
Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/fmod_audio_library_snapshot.json
```

没有 Unity / FMOD 插件时，Resource Manager Editor 和各 consumer picker 可以显示最后一次快照，但必须标记 `FmodUnavailable` 或 `FmodCacheStale`。

## Reference Graph

引用索引升级为 reference graph：

| 字段 | 说明 |
| --- | --- |
| `sourceConfigKind` | `character`、`weapon`、`animation`、`audio`、`geometry`、`unityOverride`、`vfx`、`ui`、`config` |
| `sourceStableId` | 源配置 stable id |
| `sourceField` | 具体字段 |
| `targetResourceStableId` | 目标资源项 |
| `targetLibraryItemStableId` | 兼容字段；旧角色包引用图迁移期可保留 |
| `targetResourceKey` | 编译后 runtime resource key，可为空 |
| `sourceProviderId` | 命中的资源 provider |
| `bindingKind` | authoring / runtime binding kind |
| `isRequiredAtRuntime` | 是否运行时必需 |
| `preloadPolicy` | 对应计划组 |

用途：

- 资源详情展示“被谁引用”。
- 删除前提示影响面。
- orphan 标记。
- 编译最小影响面。
- 资源计划预览。

删除策略：

- `referenceCount > 0`：不允许删除资源，只能移除或替换引用。
- `referenceCount == 0`：标记 `OrphanCandidate`。
- `isRequiredAtRuntime=true`：清理需要二次确认。
- generated asset：可清理，但必须有可重建来源。

## Editor UI Surfaces

资源管理器和具体业务编辑器是两个不同的编辑面：

- Resource Manager Editor 是独立资源编辑器，负责全局资源发现、provider 同步、导入、替换、删除、标签、兼容性、引用图、Unity/FMOD/runtime catalog 同步状态和资源级 diagnostics。
- CharacterStudio 是角色装配编辑器，只在编辑某个资源字段时消费资源选择器；它不应常驻展示“资源库里有多少资源”。
- Animation Editor、Combat/VFX Editor、UI/Config Editor 也应通过同一套资源选择器消费 Authoring Resource Manager，不直接解析角色包资源目录。
- CharacterStudio 中的资源选择入口必须由 `ResourceFieldSpec` 驱动，按字段展开可选资源列表，选择后写回 `ResourceSelectionRef` 或当前兼容字段。
- 运行时资源计划预览属于编译结果诊断，默认可以折叠；它不替代独立 Resource Manager Editor。

独立 Resource Manager Editor 至少包含：

1. 全局资源页
   - 卡片列表、缩略图、kind / usage、provider、source、import status、runtime availability、引用计数和 diagnostics。
   - 支持按类型、来源 provider、状态、runtime availability、兼容性、标签、所属 package/domain 和搜索筛选。

2. 资源详情页
   - Authoring 信息、provider binding、Unity 导入信息、Runtime 绑定信息、引用关系图、参与的 domain resource plan、最近编译结果。

各业务编辑器至少包含：

3. 字段资源选择器
   - 输入 `ResourceFieldSpec`、`AuthoringEditorContext`、当前 `ResourceSelectionRef`。
   - 输出新的 `ResourceSelectionRef` 和 warnings。
   - 不允许页面手写资源过滤逻辑。
   - 只在用户编辑资源字段时弹出，不作为常驻资源库浏览区域。

CharacterStudio 额外包含：

4. 角色资源计划页
   - 展示 `SpawnCritical`、`EquipmentInitial`、`AnimationWarmup`、`VfxWarmup`、`UiDeferred`、`Audio`。
   - 每组显示 resource key / audio cue、状态、大小、加载策略、缺失和 fallback。

## Authoring Server / CLI

Authoring server API 应围绕全局 Authoring Resource Manager Service。旧 `/api/character/resources` 可作为角色包兼容 alias，但新的编辑器不得只依赖 character 前缀 API：

| API | 用途 |
| --- | --- |
| `GET /api/authoring/resources` | 查询全局资源项、provider、状态、runtime availability、引用计数 |
| `GET /api/authoring/resources/{resourceId}` | 资源详情、provider binding、Unity/FMOD/runtime 绑定、引用图 |
| `GET /api/authoring/resources/providers` | 查询 provider 状态和同步 diagnostics |
| `POST /api/authoring/resources/import` | 导入外部文件/文件夹或登记 Unity asset |
| `POST /api/authoring/resources/reimport` | 按 resource item 重导 |
| `POST /api/authoring/resources/replace-source` | 替换源并保留 stable id |
| `GET /api/authoring/resources/references` | 资源引用图 |
| `POST /api/authoring/resources/resolve-selection` | 用 `ResourceFieldSpec` 解析 `ResourceSelectionRef` |
| `GET /api/authoring/resources/runtime-catalog` | 查询现有/编译后的 runtime catalog 资源视图 |
| `GET /api/character/resource-plan` | 返回当前角色编译后的资源计划预览 |

CLI 至少需要：

```text
authoring resources providers
authoring resources list [--provider <id>] [--scope <scopeId>] [--package <path>] [--kind <kind>]
authoring resources inspect --id <resourceId-or-stableId-or-resourceKey>
authoring resources import --file <path-or-folder> --usage <usage> [--target <provider>]
authoring resources validate [--scope <scopeId>] [--package <path>] [--domain <character|animation|combat|vfx|ui>]
character resources plan --package <path>
```

## 编译期 Diagnostics

稳定错误码：

| Code | 含义 |
| --- | --- |
| `AUTH_RES_ITEM_MISSING` | 选择引用的 resource item 不存在 |
| `AUTH_RES_STABLE_ID_DUPLICATE` | stable id 重复 |
| `AUTH_RES_RESOURCE_KEY_DUPLICATE` | runtime resource key 重复 |
| `AUTH_RES_KIND_USAGE_MISMATCH` | kind / usage 不符合字段要求 |
| `AUTH_RES_SOURCE_FILE_MISSING` | 外部源文件缺失 |
| `AUTH_RES_HASH_MISMATCH` | hash 不匹配 |
| `AUTH_RES_UNITY_ASSET_MISSING` | Unity asset 缺失 |
| `AUTH_RES_NOT_RUNTIME_LOADABLE` | 字段要求 runtime loadable，但资源不能进入 runtime catalog |
| `AUTH_RES_EDITOR_ONLY_SELECTED_FOR_RUNTIME` | editor-only 资源被选进 runtime 字段 |
| `AUTH_RES_FMOD_EVENT_MISSING` | FMOD event 不存在 |
| `AUTH_RES_FMOD_GUID_PATH_MISMATCH` | FMOD guid / path 不一致 |
| `AUTH_RES_FMOD_BANK_MISSING` | FMOD bank 缺失 |
| `AUTH_RES_FMOD_PARAMETER_MISMATCH` | FMOD 参数缺失或范围变化 |
| `AUTH_RES_COMPAT_SKELETON_MISMATCH` | 骨架不兼容 |
| `AUTH_RES_COMPAT_SLOT_MISMATCH` | 插槽不兼容 |
| `AUTH_RES_ORPHAN_CANDIDATE` | 无引用资源 |
| `AUTH_RES_REFERENCE_BROKEN` | 引用图断裂 |
| `AUTH_RES_PLAN_REQUIRED_RESOURCE_MISSING` | 必需计划资源缺失 |

每条 diagnostic 至少包含 severity、code、resource stable id、runtime resource key、source config kind、source field、provider id、message 和 suggested fix。

## 验收标准

- 独立 Resource Manager Editor 显示来自 Unity AssetDatabase、现有 runtime catalog、角色包、外部导入暂存、generated assets 和 FMOD snapshot 的资源。
- CharacterStudio、Animation Editor、Combat/VFX Editor、UI/Config Editor 都把 Authoring Resource Manager 当作资源发现/选择 provider，不各自解析资源目录。
- CharacterStudio 不常驻显示完整资源库，只在字段选择时弹出符合 `ResourceFieldSpec` 的资源列表。
- 资源项以 `resourceId` / `stableId` 为统一身份；`libraryItemId` 只作为兼容别名；FMOD event 不拥有普通 runtime `resourceKey`。
- Unity GUID、Unity path、package-local key、runtime `resourceKey`、FMOD path/guid 都作为 provider binding 保存，不互相冒充。
- 字段选择器由 `ResourceFieldSpec` 驱动，选择结果保存为 `ResourceSelectionRef`。
- Authoring Compiler 能把 `ResourceSelectionRef` 解析成 runtime resource catalog、domain resource plan、audio cue manifest 和 validation report。
- Runtime 明确只读编译产物，不直接读取编辑期 `resource_catalog.json`、`unity_resource_catalog.json`、`fmod_audio_library_snapshot.json` 或全局资源管理器索引。
- `runtime_resource_catalog.json` 能映射到现有 `MxFramework.Resources.ResourceCatalog`。
- `character_resource_plan.json` 至少包含 SpawnCritical、EquipmentInitial、AnimationWarmup、UiDeferred 和 Audio 分组。
- FMOD event 通过 AudioEventDefinition / AudioCue 进入运行时，不通过 `ResourceManager.Load<AudioClip>`。
- 从角色引用移除资源后，资源库仍保留该资源，引用图显示引用数为 0 并标记 orphan。
- `authoring resources list`、`authoring resources validate`、`character resources plan` 可在无 Unity 场景下输出 diagnostics；Unity provider 不可用时显示 provider unavailable，而不是假成功。

## 建议拆分 Issue

### Authoring Resource Manager 01：Provider Model and Unified Resource Item

- 定义 provider adapter、`resourceId` / `stableId`、provider binding、binding kind、runtime availability。
- 明确 Unity GUID、package-local key、runtime `resourceKey`、FMOD path/guid 的边界。
- 验收：Unity asset、runtime catalog entry、角色包资源、FMOD event、外部源文件都能进入统一资源视图但身份不混淆。

### Authoring Resource Manager 02：ResourceFieldSpec and Cross-Editor Picker Contract

- 定义字段级选择契约。
- 资源选择器按 spec、provider、context 过滤并输出 `ResourceSelectionRef`。
- 验收：角色模型、动画 clip、VFX prefab、UI 图标、FMOD 字段有不同过滤规则和不可选原因。

### Authoring Resource Manager 03：Reference Graph and Diagnostics

- 扫描角色、武器、动画、音频、geometry、Unity override、VFX、UI/config 引用。
- 生成 reference graph 和 diagnostics。
- 验收：资源详情能看到引用来源，删除前能提示影响面。

### Authoring Resource Manager 04：Unity AssetDatabase and Runtime Catalog Providers

- 读取 Unity AssetDatabase 资源、sub-assets、GUID/path/importer metadata。
- 读取现有 `MxFramework.Resources.ResourceCatalog` 并暴露 runtime-ready 资源。
- 区分 import status 与 runtime availability。
- 验收：Unity asset 缺失显示 `UnityMissing`，Unity asset 未进 runtime catalog 显示 `EditorOnly`，runtime catalog entry 显示 `RuntimeReady`。

### Authoring Resource Manager 05：External Folder Import and Staging

- 支持文件和文件夹导入，按扩展名、文件大小、`.meta`、隐藏文件、已知 importer 能力筛选。
- 导入前进入 `externalImportStaging` provider，导入后同步到 Unity/package/runtime provider。
- 验收：文件夹导入不会把 Unity `.meta` 当作主资源计数；不支持类型进入 diagnostics，而不是混入可选资源。

### Authoring Resource Manager 06：FMOD Audio Library and AudioCue Bridge

- 导出 / 读取 `fmod_audio_library_snapshot.json`。
- 选择 FMOD event 生成 / 更新 AudioEventDefinition 和 AudioCue。
- 验收：FMOD path/guid mismatch、bank missing、parameter mismatch 都有 diagnostics；FMOD event 不进入普通 runtime resource catalog。

### Authoring Resource Manager 07：Authoring Compiler Resource Plan

- 编译 `ResourceSelectionRef` 为 runtime resource catalog、character resource plan、audio cue manifest。
- 验收：Iron Vanguard 生成 SpawnCritical / EquipmentInitial / AnimationWarmup / Audio plan，缺失资源在编译期报错；后续 domain plan 可复用同一解析流程。

### Authoring Resource Manager 08：Runtime Resource Orchestrator

- 运行时按 CharacterResourcePlan preload、acquire、装备切换 diff、release。
- 验收：Spawn 前加载必需资源，装备切换复用 shared resources，dispose 释放 handles。

### Authoring Resource Manager 09：Resource Manager Editor and Hub Entry

- 独立 Resource Manager Editor 显示所有 provider 资源、provider 状态、详情、引用和 diagnostics。
- Editor Hub 作为统一外部界面中心启动资源管理器和业务编辑器。
- 验收：不用打开 CharacterStudio 也能浏览和检查资源；资源管理器能按 provider/domain/package 过滤。

### Authoring Resource Manager 10：CharacterStudio Picker and Plan Preview

- 在 CharacterStudio 中实现字段级资源选择器和折叠式角色资源计划预览。
- 不在角色编辑主界面常驻展示完整资源库；完整资源浏览、资源详情和资源管理进入独立 Resource Manager Editor。
- 验收：不手填 resource key，选择器能显示 runtime availability、不可选原因和 plan diagnostics。

## 后续衔接

动画归属仍留到独立设计：动画资源先作为 Authoring Resource Manager item 被准备和分类；动画 profile、装备状态、武器类型、Combat action 如何引用这些资源，由后续 Animation / Equipment authoring 设计决定。本设计只规定资源如何被发现、选择、校验、编译和运行时编排。

资源管理器的下一阶段实现必须先把 provider/consumer 边界落稳：资源管理器提供全局资源发现和桥接，业务编辑器只消费字段级 picker 和资源详情，Runtime 只消费编译产物和现有 `MxFramework.Resources` runtime catalog。
