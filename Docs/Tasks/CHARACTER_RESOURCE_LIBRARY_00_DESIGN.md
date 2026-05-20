# Character Resource Library 00：资源库、编译计划与运行时编排设计

> 状态：草案
> 范围：独立 Resource Library Editor、CharacterStudio 字段资源选择器、Authoring Compiler 资源计划、Unity / FMOD 同步、运行时资源编排
> 交付等级：下一阶段设计契约
> 前置：#221 Character Resource Package C0、#223 package-local resource catalog、#224 Authoring Compiler、#240-#246 CharacterStudio C1 MVP、`CHARACTER_RESOURCE_PACKAGE_C2_UNITY_ASSET_SYNC.md`

## 背景

CharacterStudio 当前能导入模型、武器和动画资源，并把它们写入 package-local `resource_catalog.json`。C2 Unity sync 也已经定义了 `unity_resource_catalog.json`，用于记录 Unity 导入结果。但如果系统只停留在“编辑器能看见资源”和“Unity 能导入资源”，运行时仍会缺一层明确编排：

```text
编辑期资源库
  -> 选择、校验、引用关系
Authoring Compiler
  -> 编译成运行时资源计划
Runtime Resource Catalog / Character Resource Plan
  -> preload / acquire / release
Runtime Resource Orchestrator
  -> ResourceManager / Animation / Audio / Presentation
```

本设计把资源系统明确拆成三个产物：

| 产物 | 面向对象 | 回答的问题 |
| --- | --- | --- |
| Resource Library | CharacterStudio / CLI / 外部编辑器 | 有哪些资源、能否选择、是否兼容、被谁引用 |
| Unity Import Catalog | Unity Editor / Prefab builder / 预览 | 源资源导入成了哪些 Unity asset / sub-assets，状态如何 |
| Runtime Resource Plan | Runtime Spawn / Animation / Audio / Presentation | 运行时要加载什么、何时加载、失败策略和释放策略是什么 |

这三层不能合并成一个 catalog。编辑期可见资源不一定是运行时可加载资源：外部 `.fbx` 不能被 Player 直接加载，Unity-only asset 可能只用于编辑预览，FMOD event 不是 `AudioClip`，一个导入源也可能生成多个 prefab、clip、material 和 preview asset。

## 核心原则

- Resource Library 是“可用资源集合”，不是“当前角色引用集合”。
- 配置字段保存编辑期选择引用；Authoring Compiler 再解析成运行时绑定。
- Runtime 不直接读取 `resource_catalog.json`、`unity_resource_catalog.json` 或 `fmod_audio_library_snapshot.json`。
- Runtime 只消费编译产物：`runtime_resource_catalog.json`、`character_resource_plan.json`、`audio_cue_manifest.json`。
- `resourceKey` 只代表可由 `ResourceManager` 加载的运行时资源；它不是所有资源库项的通用 ID。
- FMOD event / bank path 不进入普通 Resource Catalog，也不伪装成 `ResourceTypeIds.AudioClip`。
- Runtime resource catalog 复用现有 `MxFramework.Resources.ResourceCatalog` / `ResourceCatalogEntry` 语义，不另造并行资源系统。
- 资源删除默认先看引用图；删除角色引用不删除资源，未引用资源只标记 orphan。

## 总体链路

```text
External Files / Unity Assets / FMOD Cache / Generated Assets
  -> Resource Library
       library item, compatibility, preview, diagnostics, reference graph
  -> ResourceSelectionRef
       config fields store stable authoring selections
  -> Authoring Compiler
       resolves selections against library, Unity import catalog and FMOD snapshot
  -> Runtime Resource Catalog
       existing MxFramework.Resources catalog entries only
  -> Character Resource Plan
       spawn/equipment/animation/vfx/ui/audio preload groups
  -> Runtime Resource Orchestrator
       ResourcePreloadService + IResourceManager + Audio/FMOD warmup
```

## Resource Library

Resource Library 负责编辑期可见、筛选和选择：

- 有哪些资源。
- 资源来自外部文件、Unity asset、FMOD metadata 还是生成资产。
- 资源的 kind、usage、标签、预览和兼容性。
- 资源是否成功导入 Unity。
- 资源是否可运行时加载，或只是 editor-only / preview-only / audio-only。
- 当前被哪些角色、武器、动画、音频、override 或预览配置引用。

Resource Library 不负责：

- 当前角色运行时装备了哪件武器。
- 某个 Runtime 实例当前持有哪些 resource handles。
- FMOD bank 的 runtime 生命周期。
- 具体 Animation profile / Combat action 的归属决策。

### Library Item Identity

所有资源库卡片都有统一编辑期身份：

| 字段 | 说明 |
| --- | --- |
| `libraryItemId` | 资源库项 ID，用于 UI、API 和选择器；不要求可运行时加载 |
| `stableId` | 长期稳定 ID，用于跨重命名、重复导入和引用持久化 |
| `displayName` | 可读名称 |
| `kind` | `Model`、`Animation`、`Texture`、`Material`、`AvatarMask`、`Vfx`、`Audio`、`Config`、`Generated` |
| `usage` | 具体用途，例如 `characterModel`、`weaponModel`、`animationClipGroup`、`fmodEvent` |
| `sourceKind` | `ExternalFile`、`UnityAsset`、`FmodLibrary`、`GeneratedAsset` |
| `runtimeBindingKind` | 见下节 |
| `compatibility` | 骨架、avatar、body kind、slot、weapon class、坐标系、单位和 bounds |
| `preview` | 缩略图、预览 mesh、相机 preset、预览姿势 |
| `importStatus` | 编辑期 / Unity 导入状态 |
| `runtimeAvailability` | 运行时可用性 |
| `diagnostics` | 结构化问题 |

现有 package-local `resourceKey` 保留为兼容字段：它仍可标识包内源资源和当前 C0.6 `CharacterPackageResourceMapping` 输入，但不能作为所有资源库项的必填运行时身份。

### Runtime Binding Kind

`RuntimeBindingKind` 明确资源库项最终如何进入运行时：

| 值 | 说明 |
| --- | --- |
| `None` | 仅编辑期记录，尚无运行时绑定 |
| `ResourceManagerAsset` | 会编译成普通 `ResourceCatalogEntry`，可由 `IResourceManager` 加载 |
| `UnityEditorOnlyAsset` | 仅 Unity Editor / 预览使用，不进入 Player runtime catalog |
| `AudioEventDefinition` | FMOD event / snapshot 映射到 `AudioEventDefinition.Id` |
| `AudioCue` | 映射到项目层 audio cue，包含 event 和参数默认值 |
| `GeneratedPreviewOnly` | 缩略图、临时预览或可重建缓存 |

FMOD item 可以有 `libraryItemId` 和 `stableId`，但不应拥有普通 `resourceKey`。外部 `.fbx` 可以进入资源库，但在导入前 `runtimeAvailability=NotRuntimeLoadable`。

### Resource Runtime Availability

导入状态和运行时可用性分开：

| 维度 | 值 |
| --- | --- |
| `ResourceImportStatus` | `New`、`Clean`、`SourceChanged`、`UnityMissing`、`ImportFailed`、`Conflict`、`ManualOverride`、`OrphanCandidate` |
| `ResourceRuntimeAvailability` | `Unknown`、`RuntimeReady`、`RuntimeMissing`、`EditorOnly`、`PreviewOnly`、`AudioCueOnly`、`NotRuntimeLoadable` |

这样可以表达“导入是 Clean，但只适合 Editor 预览”、“FMOD event 已同步，但只能通过 AudioCue 播放”、“源文件存在但尚未生成运行时 asset”等状态。

## ResourceFieldSpec

每个配置字段都应声明自己的资源选择规则，避免 UI 页面手写筛选逻辑。

| 字段 | 说明 |
| --- | --- |
| `fieldKey` | 稳定字段 key，例如 `Character.Model`、`Weapon.Icon`、`CombatAction.HitSfx` |
| `displayName` | UI 可读名称 |
| `acceptedKinds` | 允许的 library item kind |
| `acceptedUsages` | 允许的 usage |
| `acceptedBindingKinds` | 允许的 runtime binding kind |
| `requireRuntimeLoadable` | 是否必须能编译成运行时绑定 |
| `requireUnityImported` | 是否必须已有 Unity 导入结果 |
| `allowIncompatibleWithWarning` | 是否允许兼容性 warning 下选择 |
| `compatibilityFilter` | 当前 skeleton、avatar、body kind、weapon class、slot 等过滤条件 |
| `preloadPolicy` | 编译进入哪个计划组 |
| `outputKind` | `ResourceSelectionRef` 输出应解析成 ResourceKey、AudioCueId、AudioEventDefinitionId 等 |

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

CharacterStudio 资源选择器统一使用：

```text
ResourcePicker.Open(ResourceFieldSpec spec, CharacterEditorContext context)
  -> ResourceSelectionRef
```

选择器显示规则：

- 绿色：完全匹配。
- 黄色：可选但有兼容性 warning。
- 灰色：不可选，并显示结构化原因。
- 红色：导入失败或 runtime 不可用。

## ResourceSelectionRef

配置字段不直接保存裸 `ResourceKey`、Unity path、FMOD path 或 GUID，而是保存编辑期选择引用：

| 字段 | 说明 |
| --- | --- |
| `libraryItemStableId` | 被选择的资源库项 stable id |
| `bindingKind` | 期望绑定类型 |
| `expectedKind` | 选择时字段期望的 kind |
| `expectedUsage` | 选择时字段期望的 usage |
| `expectedHash` | 可选，用于检测源资源变化 |
| `resourceKey` | 编译后可填；仅 `ResourceManagerAsset` 使用 |
| `audioCueId` | 编译后可填；仅 audio cue 使用 |

编译前：

```json
{
  "libraryItemStableId": "charpkg.iron_vanguard.resource.model.body",
  "bindingKind": "ResourceManagerAsset",
  "expectedKind": "Model",
  "expectedUsage": "characterModel"
}
```

编译后：

```json
{
  "resourceKey": "char.iron_vanguard.model.body.prefab",
  "providerId": "memory",
  "hash": "sha256:..."
}
```

这避免外部路径、Unity GUID、FMOD path 直接散落在角色、武器、动画或表现配置里。

## Authoring Compiler Resource Plan

Authoring Compiler 是编辑期选择与运行时消费之间的唯一编排层。

输入：

- Character / Weapon / Animation / Presentation 配置。
- Resource Library。
- Unity Import Catalog。
- FMOD audio library snapshot。
- Resource reference graph。

输出：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/runtime_resource_catalog.json
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/character_resource_plan.json
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/audio_cue_manifest.json
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/resource_validation_report.json
```

编译流程：

1. 扫描所有 `ResourceSelectionRef`。
2. 根据 `ResourceFieldSpec` 校验 kind / usage / binding kind。
3. 从 Resource Library 解析 stable selection。
4. 从 Unity Import Catalog 解析 Unity asset / sub-assets / import status。
5. 从 FMOD snapshot 解析 event / bank / parameter。
6. 生成现有 `MxFramework.Resources.ResourceCatalog` 语义的 runtime catalog entries。
7. 生成角色资源计划和 audio cue manifest。
8. 输出结构化 diagnostics。

如果资源缺失或不可运行时加载，必须在编译期诊断，不等到 Runtime Spawn 才失败。

## Runtime Resource Catalog

`runtime_resource_catalog.json` 只包含 `ResourceManager` 能加载的资源，格式应映射到现有 `MxFramework.Resources.ResourceCatalog` / `ResourceCatalogEntry`：

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

`character_resource_plan.json` 告诉 Runtime Spawn 如何使用 runtime catalog、Audio 和表现资源。

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
ICharacterResourceOrchestrator
  PreloadForSpawn(plan)
  AcquireForSpawn(plan)
  PrepareEquipmentChange(session, nextPlan)
  CommitEquipmentChange(session, diff)
  Release(session)
```

实现约束：

- Resource preload 复用现有 `ResourcePreloadService` 和 `ResourcePreloadPlan`。
- 资源 handle 由 `IResourceManager` 持有和释放。
- Audio warmup 走 Audio/FMOD 层，不走 `ResourceManager.Load<AudioClip>`。
- `CharacterResourceSession` 记录 plan hash、loaded resource handles、audio cue ids、banks 和 diagnostics。
- Release 幂等，重复释放只写 diagnostics。

## FMOD 与 AudioCue

FMOD 数据流：

```text
FMOD event:/Character/IronVanguard/SwordSlash
  -> fmod_audio_library_snapshot.json
  -> Resource Library audio item
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

没有 Unity / FMOD 插件时，CharacterStudio 可以显示最后一次快照，但必须标记 `FmodUnavailable` 或 `FmodCacheStale`。

## Reference Graph

引用索引升级为 reference graph：

| 字段 | 说明 |
| --- | --- |
| `sourceConfigKind` | `character`、`weapon`、`animation`、`audio`、`geometry`、`unityOverride` |
| `sourceStableId` | 源配置 stable id |
| `sourceField` | 具体字段 |
| `targetLibraryItemStableId` | 目标资源库项 |
| `targetResourceKey` | 编译后 runtime resource key，可为空 |
| `bindingKind` | runtime binding kind |
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

资源库和角色编辑器是两个不同的编辑面：

- Resource Library Editor 是独立资源编辑器，负责全量资源发现、导入、替换、删除、标签、兼容性、引用图、Unity/FMOD 同步状态和资源级 diagnostics。
- CharacterStudio 是角色装配编辑器，只在编辑某个资源字段时消费资源库列表；它不应常驻展示“资源库里有多少资源”。
- CharacterStudio 中的资源选择入口必须由 `ResourceFieldSpec` 驱动，按字段展开可选资源列表，选择后写回 `ResourceSelectionRef` 或当前兼容字段。
- 运行时资源计划预览属于编译结果诊断，默认可以折叠；它不替代独立 Resource Library Editor。

独立 Resource Library Editor 至少包含：

1. 资源库页
   - 卡片列表、缩略图、kind / usage、source、import status、runtime availability、引用计数和 diagnostics。
   - 支持按类型、来源、状态、runtime availability、兼容性、标签和搜索筛选。

2. 资源详情页
   - Authoring 信息、Unity 导入信息、Runtime 绑定信息、引用关系图、参与的 `CharacterResourcePlan`、最近编译结果。

CharacterStudio 至少包含：

3. 字段资源选择器
   - 输入 `ResourceFieldSpec`、`CharacterEditorContext`、当前 `ResourceSelectionRef`。
   - 输出新的 `ResourceSelectionRef` 和 warnings。
   - 不允许页面手写资源过滤逻辑。
   - 只在用户编辑资源字段时弹出，不作为常驻资源库浏览区域。

4. 角色资源计划页
   - 展示 `SpawnCritical`、`EquipmentInitial`、`AnimationWarmup`、`VfxWarmup`、`UiDeferred`、`Audio`。
   - 每组显示 resource key / audio cue、状态、大小、加载策略、缺失和 fallback。

## Authoring Server / CLI

Authoring server API 应围绕 Resource Library Service：

| API | 用途 |
| --- | --- |
| `GET /api/character/resources` | 查询资源库项、状态、runtime availability、引用计数 |
| `GET /api/character/resources/{libraryItemId}` | 资源详情、Unity/FMOD/runtime 绑定、引用图 |
| `POST /api/character/resources/import` | 导入外部文件或登记 Unity asset |
| `POST /api/character/resources/reimport` | 按 library item 重导 |
| `POST /api/character/resources/replace-source` | 替换源并保留 stable id |
| `GET /api/character/resources/references` | 资源引用图 |
| `POST /api/character/resources/resolve-selection` | 用 `ResourceFieldSpec` 解析 `ResourceSelectionRef` |
| `GET /api/character/resource-plan` | 返回当前角色编译后的资源计划预览 |

CLI 至少需要：

```text
character resources list --package <path>
character resources inspect --package <path> --id <libraryItemId>
character resources validate --package <path>
character resources import --package <path> --file <path> --usage <usage>
character resources plan --package <path>
```

## 编译期 Diagnostics

稳定错误码：

| Code | 含义 |
| --- | --- |
| `RES_LIBRARY_ITEM_MISSING` | 选择引用的 library item 不存在 |
| `RES_LIBRARY_STABLE_ID_DUPLICATE` | stable id 重复 |
| `RES_LIBRARY_RESOURCE_KEY_DUPLICATE` | runtime resource key 重复 |
| `RES_LIBRARY_KIND_USAGE_MISMATCH` | kind / usage 不符合字段要求 |
| `RES_LIBRARY_SOURCE_FILE_MISSING` | 外部源文件缺失 |
| `RES_LIBRARY_HASH_MISMATCH` | hash 不匹配 |
| `RES_LIBRARY_UNITY_ASSET_MISSING` | Unity asset 缺失 |
| `RES_LIBRARY_NOT_RUNTIME_LOADABLE` | 字段要求 runtime loadable，但资源不能进入 runtime catalog |
| `RES_LIBRARY_EDITOR_ONLY_SELECTED_FOR_RUNTIME` | editor-only 资源被选进 runtime 字段 |
| `RES_LIBRARY_FMOD_EVENT_MISSING` | FMOD event 不存在 |
| `RES_LIBRARY_FMOD_GUID_PATH_MISMATCH` | FMOD guid / path 不一致 |
| `RES_LIBRARY_FMOD_BANK_MISSING` | FMOD bank 缺失 |
| `RES_LIBRARY_FMOD_PARAMETER_MISMATCH` | FMOD 参数缺失或范围变化 |
| `RES_LIBRARY_COMPAT_SKELETON_MISMATCH` | 骨架不兼容 |
| `RES_LIBRARY_COMPAT_SLOT_MISMATCH` | 插槽不兼容 |
| `RES_LIBRARY_ORPHAN_CANDIDATE` | 无引用资源 |
| `RES_LIBRARY_REFERENCE_BROKEN` | 引用图断裂 |
| `RES_LIBRARY_PLAN_REQUIRED_RESOURCE_MISSING` | 必需计划资源缺失 |

每条 diagnostic 至少包含 severity、code、library item stable id、resource key、source config kind、source field、message 和 suggested fix。

## 验收标准

- 独立 Resource Library Editor 显示模型、武器、动画、贴图、材质、VFX、Unity AudioClip 和 FMOD event。
- CharacterStudio 不常驻显示完整资源库，只在字段选择时弹出符合 `ResourceFieldSpec` 的资源列表。
- 资源库项以 `libraryItemId` / `stableId` 为统一身份；FMOD event 不拥有普通 runtime `resourceKey`。
- 字段选择器由 `ResourceFieldSpec` 驱动，选择结果保存为 `ResourceSelectionRef`。
- Authoring Compiler 能输出 runtime resource catalog、character resource plan、audio cue manifest 和 validation report。
- Runtime 明确只读编译产物，不直接读取编辑期 `resource_catalog.json`、`unity_resource_catalog.json`、`fmod_audio_library_snapshot.json`。
- `runtime_resource_catalog.json` 能映射到现有 `MxFramework.Resources.ResourceCatalog`。
- `character_resource_plan.json` 至少包含 SpawnCritical、EquipmentInitial、AnimationWarmup、UiDeferred 和 Audio 分组。
- FMOD event 通过 AudioEventDefinition / AudioCue 进入运行时，不通过 `ResourceManager.Load<AudioClip>`。
- 从角色引用移除资源后，资源库仍保留该资源，引用图显示引用数为 0 并标记 orphan。
- `character resources validate` 和 `character resources plan` 可在无 Unity 场景下输出 diagnostics。

## 建议拆分 Issue

### Resource Library 01：Library Item Contract and SelectionRef

- 定义 library item、runtime binding kind、runtime availability 和 selection ref。
- 明确 package-local `resourceKey` 与 runtime `ResourceKey` 的区别。
- 验收：FMOD event、外部源文件、Unity asset 都能进入资源库但 runtime binding 不混淆。

### Resource Library 02：ResourceFieldSpec and Picker Contract

- 定义字段级选择契约。
- 资源选择器按 spec 过滤并输出 `ResourceSelectionRef`。
- 验收：角色模型、武器模型、缩略图、FMOD 字段有不同过滤规则和不可选原因。

### Resource Library 03：Reference Graph and Diagnostics

- 扫描角色、武器、动画、音频、geometry、Unity override 引用。
- 生成 reference graph 和 diagnostics。
- 验收：资源详情能看到引用来源，删除前能提示影响面。

### Resource Library 04：Unity Sync and Runtime Availability

- 维护 `unity_resource_catalog.json`。
- 区分 import status 与 runtime availability。
- 验收：Unity asset 缺失显示 `UnityMissing`，导入成功但未进 runtime catalog 显示 `EditorOnly`。

### Resource Library 05：FMOD Audio Library and AudioCue Bridge

- 导出 `fmod_audio_library_snapshot.json`。
- 选择 FMOD event 生成 / 更新 AudioEventDefinition 和 AudioCue。
- 验收：FMOD path/guid mismatch、bank missing、parameter mismatch 都有 diagnostics。

### Resource Library 06：Authoring Compiler Resource Plan

- 编译 `ResourceSelectionRef` 为 runtime resource catalog、character resource plan、audio cue manifest。
- 验收：Iron Vanguard 生成 SpawnCritical / EquipmentInitial / AnimationWarmup / Audio plan，缺失资源在编译期报错。

### Resource Library 07：Runtime Resource Orchestrator

- 运行时按 CharacterResourcePlan preload、acquire、装备切换 diff、release。
- 验收：Spawn 前加载必需资源，装备切换复用 shared resources，dispose 释放 handles。

### Resource Library 08：Resource Picker and Plan Preview

- 在 CharacterStudio 中实现字段级资源选择器和折叠式角色资源计划预览。
- 不在角色编辑主界面常驻展示完整资源库；完整资源浏览、资源详情和资源管理进入独立 Resource Library Editor 后续任务。
- 验收：不手填 resource key，选择器能显示 runtime availability、不可选原因和 plan diagnostics。

## 后续衔接

动画归属仍留到独立设计：动画资源先作为 Resource Library item 被准备和分类；动画 profile、装备状态、武器类型、Combat action 如何引用这些资源，由后续 Animation / Equipment authoring 设计决定。本设计只规定资源如何被选择、校验、编译和运行时编排。
