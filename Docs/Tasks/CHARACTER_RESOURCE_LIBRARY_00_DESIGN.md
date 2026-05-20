# Character Resource Library 00：资源准备与选择系统设计

> 状态：草案
> 范围：CharacterStudio 资源库、资源准备、分类、导入状态、Unity 内部资源和外部资源统一选择
> 交付等级：下一阶段设计契约
> 前置：#221 Character Resource Package C0、#223 package-local resource catalog、#224 Authoring Compiler、#240-#246 CharacterStudio C1 MVP、`CHARACTER_RESOURCE_PACKAGE_C2_UNITY_ASSET_SYNC.md`

## 背景

CharacterStudio 当前已经可以在角色包内导入模型、武器和动画资源，并把它们写入 `resource_catalog.json`。但现有体验仍然把“资源准备”和“角色正在引用哪些资源”混在一起：

- 用户不知道已经导入了哪些资源、资源是什么类型、适合哪个部位或用途。
- 角色、武器、动画、碰撞体和挂点都需要选择资源，但很多地方还在手填 key、路径或标签。
- Unity 内部已有资产、FMOD 事件和外部 `.glb` / `.fbx` / `.png` 等资源没有统一选择入口。
- 删除角色对武器或动画的引用时，不应误删资源本身。
- 动画资源需要能提前准备和分类，但动画最终归属角色、武器还是装备状态，本设计先不定论。

资源库要解决的是“有哪些可用资源、它们是否可用、如何被选择和预览”，不是“某个角色当前必须使用哪些资源”。

## 目标

建立 CharacterStudio 可消费的资源准备层：

```text
External Files / Unity Assets / Generated Assets
  -> Resource Library
       prepared resources, tags, compatibility, preview, import status
  -> Character Package References
       character model, weapon model, animation profile, sockets, colliders
  -> Unity Import Catalog
       Unity asset guid/path/sub-assets/import diagnostics
```

完成后：

- CharacterStudio 有独立“资源库”板块，可以查看、筛选、预览和选择资源。
- 外部资源和 Unity 内部资源都能进入同一个资源库视图。
- FMOD 中可用的 event / snapshot / bus / parameter 能被同步成可筛选、可选择的音频列表。
- 资源准备不会直接改变角色当前引用；引用改变必须发生在角色、武器、动画、挂点等配置面板中。
- 所有资源都有稳定 ID、分类、来源、hash、导入状态和诊断。
- 编辑器中能选择的字段必须优先用资源选择器，不让用户手填易错字符串。

## 非目标

- 不决定动画最终归属角色、武器、装备状态或动作集；本设计只定义动画资源如何准备和选择。
- 不实现完整动画状态机、BlendTree、PlayableGraph 或 Combat action 映射。
- 不把 Unity Editor 变成唯一资源准备入口。
- 不让 Runtime 直接消费未导入的外部源文件。
- 不把 FMOD event path / guid 伪装成普通 `ResourceKey` 或 `AudioClip`。
- 不自动删除 orphan 资源；只标记和提示。
- 不在资源库里保存运行时当前状态，例如当前装备、当前动画、当前 HP。

## 核心原则

- 资源库是“可用资源集合”，不是“当前角色引用集合”。
- `resource_catalog.json` 保存包内源资源条目；角色配置、装备配置、动画配置等保存引用关系。
- Unity 导入结果保存到 `unity_resource_catalog.json`；它是 Unity 可消费资产映射，不解释玩法语义。
- 同一资源可以被多个角色、武器、动画 profile 或预览配置引用。
- 资源删除必须先检查引用关系；默认只移除引用或标记 orphan，不直接删除源文件。
- 所有资源选择都应通过 stable id / resource key，而不是路径字符串。
- 所有资源状态必须结构化诊断，不依赖 Console warning。

## 分层边界

### 1. Resource Library

资源库负责回答：

- 有哪些资源？
- 资源来自哪里？
- 资源属于哪一类？
- 资源是否已经导入 Unity？
- 资源能否被当前角色、部位、骨架或用途选择？
- 资源有哪些预览、缩略图、子资源和诊断？

资源库不负责回答：

- 当前角色装备了哪把武器。
- 当前装备状态使用哪套动画 profile。
- 某个 Combat action 对应哪个动画。
- 某个 Runtime 实例当前加载了哪个 handle。

### 2. Character Package References

角色包配置负责保存当前引用关系，例如：

- 角色主体模型引用哪个 `ResourceKey`。
- 武器配置引用哪个模型资源。
- 挂点 / 碰撞体 / trace 引用哪个 body part、bone 或 locator。
- 后续动画配置引用哪个动画资源或 clip group。

引用关系可以变化，但资源库条目仍保留，直到用户明确清理。

### 3. Unity Import Catalog

Unity 导入目录负责保存：

- Unity asset GUID。
- Unity asset path。
- 主对象类型和 sub-assets。
- importer kind。
- import status。
- diagnostics。

Prefab builder 和运行时预览只能消费 Unity 可实例化资产，不能把源 `.glb` / `.fbx` 当作稳定 runtime 入口。

## 资源范围

第一版资源库覆盖：

| Kind | Usage 示例 | 说明 |
| --- | --- | --- |
| `model` | `characterModel`、`weaponModel`、`propModel` | 角色主体、武器、道具、占位体 |
| `animation` | `animationClip`、`animationClipGroup` | 单 clip 或 clip group，归属后续再定 |
| `texture` | `thumbnail`、`albedo`、`normal`、`mask` | 缩略图和材质贴图 |
| `material` | `previewMaterial`、`runtimeMaterial` | Unity 内部材质或生成材质 |
| `avatarMask` | `upperBodyMask`、`fullBodyMask` | 动画 layer 需要的 mask |
| `vfx` | `hitVfx`、`trailVfx` | 后续表现资源 |
| `audio` | `audioClip`、`fmodEvent`、`fmodSnapshot`、`fmodBus` | Unity AudioClip 和 FMOD 可选音频项 |
| `config` | `authoringPatch`、`profile` | 受控配置资源 |

## 资源来源

资源库必须同时支持三种来源：

| 来源 | 示例 | 处理方式 |
| --- | --- | --- |
| 外部文件 | `.glb`、`.gltf`、`.fbx`、`.png`、`.wav` | 复制或引用到包内资源目录，计算 hash，写入 catalog |
| Unity 项目资产 | `Assets/.../*.prefab`、`.anim`、`.mat` | 记录 GUID/path/type，必要时生成包内引用或导入映射 |
| FMOD 元数据 | FMOD event、snapshot、bus、parameter、bank | 由 Unity Editor 读取 FMOD cache / bank 后导出快照，供 CharacterStudio 选择 |
| 生成资产 | 缩略图、预览材质、转换后的 `.glb` | 记录生成来源、生成 hash 和可重建策略 |

外部文件和 Unity 项目资产不应在 UI 上分裂成两套入口。用户应该看到统一资源卡片，只在详情里显示来源和导入状态。

FMOD 音频项也应显示在同一个资源库里，但它们不是普通 Resource Catalog asset。选择 FMOD event 时，配置写入的是 `AudioEventDefinition.Id` 或后续音频 cue id；FMOD path / guid 只作为 Audio/FMOD 定义的后端映射数据。

## 数据模型

资源库可以先复用并扩展 `resource_catalog.json`，后续再按需要拆出 workspace 级库。第一阶段建议在 package-local catalog 中补齐以下语义。

### Resource Entry

| 字段 | 说明 |
| --- | --- |
| `resourceKey` | 包内唯一资源 key，用于配置引用 |
| `stableId` | 长期稳定 ID，跨重命名和重复导入判断使用 |
| `displayName` | 可读名称 |
| `kind` | `model`、`animation`、`texture`、`material` 等 |
| `usage` | 更具体用途，例如 `weaponModel`、`animationClipGroup` |
| `source` | 外部文件、Unity asset、生成资产 |
| `sourceFormat` | `glb`、`fbx`、`unityPrefab`、`anim`、`png` |
| `relativePath` | 包内源资源路径；Unity-only 资源可为空 |
| `unityAssetGuid` | Unity 资产来源时的 GUID |
| `unityAssetPath` | Unity 资产来源时的项目路径 |
| `hashes` | content / import / dependency hash |
| `tags` | 用户和系统标签 |
| `compatibility` | 骨架、avatar、单位、坐标系、目标类型等兼容信息 |
| `preview` | 缩略图、预览 mesh、默认相机 preset |
| `importHints` | scale、axis、material policy、animation policy 等导入提示 |
| `diagnostics` | 资源级结构化问题 |

FMOD 音频项可以使用同一套列表 DTO 展示，但必须带上 `selectionKind=audioEventDefinition` 或等价标记，避免 UI 把它当作可通过 `ResourceManager.Load<AudioClip>` 加载的 `ResourceKey`。

### Compatibility

兼容信息不能只靠标签，至少需要：

| 字段 | 说明 |
| --- | --- |
| `skeletonProfileId` | 适用骨架 profile，可为空 |
| `avatarProfileId` | 适用 humanoid / generic avatar profile，可为空 |
| `bodyKind` | `Humanoid`、`Skeletal`、`Primitive` 等 |
| `weaponClass` | `sword`、`shield`、`spear` 等，可选 |
| `slotHints` | `mainHand`、`offHand`、`back` 等建议槽位 |
| `unitScaleMeters` | 单位缩放 |
| `upAxis` / `forwardAxis` | 坐标约定 |
| `bounds` | 预览和尺寸诊断用包围盒 |

兼容信息用于筛选和警告，不应默默阻止高级用户选择；阻断只发生在确定无法导入或无法解析的情况。

### Preview Metadata

每个资源都应尽量提供：

| 字段 | 说明 |
| --- | --- |
| `thumbnailResourceKey` | 缩略图资源 key |
| `previewMeshResourceKey` | 可用于预览的 mesh / prefab |
| `previewCameraPresetId` | `characterFullBody`、`weaponCloseup` 等 |
| `previewPoseId` | 可选默认姿势 |
| `previewWarnings` | 预览层可读 warning |

## 示例

```json
{
  "resourceKey": "char.iron_vanguard.weapon.sword.model",
  "stableId": "charpkg.iron_vanguard.resource.weapon.sword.model",
  "displayName": "Iron Sword",
  "kind": "model",
  "usage": "weaponModel",
  "source": {
    "kind": "externalFile",
    "format": "glb",
    "relativePath": "resources/models/katana.glb"
  },
  "tags": ["weapon", "sword", "mainHand"],
  "compatibility": {
    "weaponClass": "sword",
    "slotHints": ["mainHand"],
    "unitScaleMeters": 1,
    "upAxis": "Y+",
    "forwardAxis": "Z+"
  },
  "preview": {
    "previewMeshResourceKey": "char.iron_vanguard.weapon.sword.model",
    "previewCameraPresetId": "weaponCloseup"
  },
  "importHints": {
    "targetPathPolicy": "generatedCharacterPackage",
    "scale": 1,
    "materialPolicy": "urpLit"
  }
}
```

动画资源也可以进入资源库，但不在本设计内决定其最终归属：

```json
{
  "resourceKey": "char.iron_vanguard.anim.locomotion",
  "stableId": "charpkg.iron_vanguard.resource.anim.locomotion",
  "displayName": "Iron Vanguard Locomotion",
  "kind": "animation",
  "usage": "animationClipGroup",
  "source": {
    "kind": "externalFile",
    "format": "glb",
    "relativePath": "resources/animations/locomotion.glb"
  },
  "tags": ["animation", "locomotion", "humanoid"],
  "compatibility": {
    "skeletonProfileId": "skeleton.iron_vanguard.humanoid",
    "avatarProfileId": "avatar.humanoid.default"
  },
  "importHints": {
    "animationPolicy": "splitByClipNames"
  }
}
```

FMOD 事件可以进入资源库视图，但选择结果不写普通 `ResourceKey`：

```json
{
  "displayName": "Sword Slash",
  "kind": "audio",
  "usage": "fmodEvent",
  "source": {
    "kind": "fmodEventCache",
    "eventPath": "event:/Character/IronVanguard/SwordSlash",
    "eventGuid": "{00000000-0000-0000-0000-000000000000}",
    "banks": ["Master"]
  },
  "selectionKind": "audioEventDefinition",
  "audio": {
    "eventId": 500101,
    "kind": "Event",
    "is3D": true,
    "isLoop": false,
    "maxDistance": 20,
    "parameters": ["Intensity"]
  },
  "tags": ["audio", "sfx", "weapon", "sword"]
}
```

## CharacterStudio UI

新增“资源库”板块，和“角色配置 / 武器 / 动画 / 几何绑定”等配置板块并列。

### 资源库列表

列表应显示：

- 缩略图。
- display name。
- kind / usage。
- source kind。
- import status。
- 引用计数。
- tags。
- 最近 diagnostics。

必须支持筛选：

- 类型：模型、武器模型、动画、贴图、材质、mask、音频。
- 来源：外部文件、Unity asset、FMOD、生成资产。
- 状态：Clean、SourceChanged、UnityMissing、ImportFailed、Conflict、Orphan。
- 兼容性：当前角色骨架、当前槽位、当前 body kind。
- 标签和搜索。

### 资源详情

点击资源后显示：

- 源路径或 Unity asset path。
- hash / import hash。
- Unity 导入状态。
- sub-assets，例如动画 clip 列表。
- 兼容性信息。
- 当前被哪些配置引用。
- 可执行动作：预览、替换源文件、重新导入、复制 resource key、标记清理候选。

### 资源选择器

任何配置字段需要选择资源时，应弹出资源选择器：

- 根据字段类型自动过滤资源。
- 显示缩略图和可读名称。
- 显示不兼容 warning。
- 支持“只看已导入 Unity 可用资源”。
- 选择结果写入 `ResourceKey` 或 stable id，不写路径。

例如：

- 主体模型字段只显示 `usage=characterModel`。
- 武器模型字段只显示 `usage=weaponModel`。
- 缩略图字段只显示 `kind=texture` 且 `usage=thumbnail`。
- 动画字段可显示 `usage=animationClip` / `animationClipGroup`，但动画归属由后续设计决定。
- 音频字段可显示 `usage=audioClip` / `fmodEvent` / `fmodSnapshot`，但 FMOD event 的选择结果应写入 AudioEventDefinition 或音频 cue 配置，不写普通 ResourceKey。

## FMOD 音频列表同步

FMOD 是独立音频后端，不能把 `.bank` 文件或 `event:/...` path 当作普通 `AudioClip` catalog entry。资源库需要的是“可选择音频项列表”，不是直接接管 FMOD bank 生命周期。

### 数据来源

Unity Editor 侧应提供一个 FMOD library exporter：

```text
MxFramework/Audio/Export FMOD Audio Library
```

执行流程：

1. 调用或提示执行 `FMOD/Refresh Banks`，确保 FMOD Unity cache 是最新的。
2. 读取 FMOD Unity Integration 暴露的 editor cache：
   - `FMODUnity.EventManager.Events`
   - `FMODUnity.EventManager.Banks`
   - `FMODUnity.EventManager.Parameters`
3. 对每个 `EditorEventRef` 导出：
   - event path
   - event guid
   - banks
   - `Is3D`
   - `IsOneShot`
   - min / max distance
   - length
   - local / global parameters
4. 对每个 bank 导出：
   - bank name
   - bank path
   - studio path
   - last modified time
5. 写出 CharacterStudio 可读快照。

建议输出：

```text
Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json
```

或者在角色包导入时复制一份快照到：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/fmod_audio_library_snapshot.json
```

CharacterStudio 浏览器侧只读这个快照。没有 Unity / FMOD 插件时，仍可显示最后一次快照，但必须标记 `Stale` 或 `FmodUnavailable`。

### 快照结构

```json
{
  "format": "mx.fmodAudioLibrary.v1",
  "generatedAtUtc": "2026-05-20T00:00:00Z",
  "source": {
    "settingsAssetPath": "Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset",
    "cacheAssetPath": "Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset"
  },
  "banks": [
    {
      "name": "Master",
      "path": ".../Master.bank",
      "studioPath": "bank:/Master",
      "lastModifiedUtc": "2026-05-20T00:00:00Z"
    }
  ],
  "events": [
    {
      "path": "event:/Character/IronVanguard/SwordSlash",
      "guid": "{00000000-0000-0000-0000-000000000000}",
      "banks": ["Master"],
      "is3D": true,
      "isOneShot": true,
      "minDistance": 1,
      "maxDistance": 20,
      "lengthMs": 350,
      "parameters": [
        {
          "name": "Intensity",
          "isGlobal": false,
          "min": 0,
          "max": 1,
          "default": 1,
          "type": "Continuous"
        }
      ]
    }
  ]
}
```

### 选择与配置写入

FMOD event 选择器不直接写：

```text
ResourceKey = event:/...
ResourceKey = bank:/...
```

而是写入或生成音频定义：

```text
AudioEventDefinition
  Id = stable int
  Name = readable name
  FmodEventPath = event:/...
  FmodEventGuid = {...}
  Kind = Event / Snapshot
  Is3D / IsLoop / Parameters
```

角色、武器、动画、VFX 或 Combat bridge 后续只引用稳定 `AudioEventDefinition.Id` 或项目层 audio cue id。FMOD path / guid 的变化由 FMOD library exporter 和 Audio/FMOD validator 诊断。

### 状态与诊断

FMOD 资源列表至少需要这些状态：

| 状态 | 含义 |
| --- | --- |
| `FmodClean` | 快照、cache 和 bank 元数据一致 |
| `FmodCacheStale` | FMOD cache 旧于 bank 或 source project |
| `FmodUnavailable` | 当前 Unity 项目没有 FMOD 插件或 Settings |
| `FmodEventMissing` | 已配置的 event path / guid 在 FMOD 列表中找不到 |
| `FmodGuidPathMismatch` | guid 存在但 path 变了，或 path 存在但 guid 变了 |
| `FmodBankMissing` | event 所属 bank 不存在或未导出 |
| `FmodParameterMismatch` | 已配置参数在 FMOD event 中缺失或范围变化 |

这些状态应进入资源库 diagnostics，并在音频选择器和配置面板中显示。

## Unity 同步

资源库和 Unity 导入状态的关系：

```text
resource_catalog.json
  package source resources and authoring metadata

unity_resource_catalog.json
  Unity imported asset mapping and import diagnostics

unity_authoring_overrides.json
  controlled Unity-side adjustments, not source catalog replacement
```

CharacterStudio 打开包时应合并显示：

- 源资源库条目。
- Unity 导入状态。
- Unity override 状态。
- 引用关系。

Unity 导入器不能因为某资源当前没有被角色引用就删除它。未引用资源只显示 `referenceCount=0` 或 `OrphanCandidate`。

## 引用关系索引

资源库需要能显示资源被谁引用。第一阶段可以由 CharacterStudio / CLI 扫描配置生成临时索引：

| 引用来源 | 示例 |
| --- | --- |
| `characterApplication` | 主体模型、预览图、资源 keys |
| `weaponConfig` | 武器模型、图标 |
| `geometryBinding` | preview mesh、locator resource |
| `animationConfig` | 后续动画 profile / clip group |
| `audioConfig` | AudioEventDefinition、audio cue、FMOD event |
| `unityOverride` | Unity 侧替换引用 |

引用索引用于：

- 删除前提示。
- 资源详情展示。
- orphan 标记。
- 选择器排序。
- 导入最小影响面计算。

## 导入与更新状态

资源状态至少包含：

| 状态 | 含义 |
| --- | --- |
| `Clean` | 源 hash、import hash、Unity asset 都匹配 |
| `New` | catalog 有资源，但尚未导入 Unity |
| `SourceChanged` | 源文件或 import hint 变化 |
| `UnityMissing` | Unity catalog 记录存在，但 asset 不存在 |
| `ImportFailed` | 最近导入失败 |
| `Conflict` | stable id、hash 或来源冲突 |
| `ManualOverride` | Unity 侧存在受控 override |
| `OrphanCandidate` | 已无配置引用，但资源仍保留 |

重复导入规则沿用 C2：

```text
same stableId + same importHash + unity asset exists
  -> skip

same stableId + sourceHash changed + no manual override
  -> reimport in place

same stableId + sourceHash changed + manual override exists
  -> conflict, require user choice

no references
  -> mark OrphanCandidate, do not delete automatically
```

## CLI / API 要求

Authoring server 应提供资源库 API：

| API | 用途 |
| --- | --- |
| `GET /api/character/resources` | 返回资源库列表、引用计数、导入状态 |
| `GET /api/character/resources/{key}` | 返回资源详情、sub-assets、diagnostics |
| `POST /api/character/resources/import` | 导入外部文件或 Unity asset 引用 |
| `POST /api/character/resources/reimport` | 按 key 重导 |
| `POST /api/character/resources/replace-source` | 替换源文件并保留 stable id |
| `POST /api/character/resources/mark-orphan` | 标记清理候选 |
| `GET /api/character/resources/references` | 返回资源引用索引 |

CLI 至少需要：

```text
character resources list --package <path>
character resources inspect --package <path> --key <resourceKey>
character resources validate --package <path>
character resources import --package <path> --file <path> --usage <usage>
```

## 校验规则

资源库校验至少覆盖：

- `resourceKey` 唯一。
- `stableId` 唯一，除非明确 variant。
- `relativePath` 存在或 Unity asset GUID 可解析。
- hash 与实际文件一致。
- `kind` / `usage` 合法。
- `sourceFormat` 和 importer policy 匹配。
- 动画资源声明 clip names 时，导入后 sub-assets 能匹配。
- FMOD 快照存在时，已配置的 audio event id 能解析到 event path / guid。
- FMOD event path / guid 不得作为普通 `AudioClip` ResourceKey。
- 资源引用不存在时输出结构化 diagnostic。
- Unity asset 缺失时输出 `UnityMissing`，不伪装成功。
- orphan 资源只 warning，不阻断。

## 验收标准

- CharacterStudio 有独立资源库视图，能列出模型、武器、动画、贴图等资源。
- 资源库能显示 FMOD event / snapshot / bus 列表，并标明 bank、guid、3D、loop、参数和状态。
- 外部导入和 Unity 内部资产都能显示为统一资源卡片。
- 资源详情能显示来源、hash、Unity 导入状态、引用关系和 diagnostics。
- 配置字段选择资源时使用资源选择器，不需要手填 resource key。
- 从角色引用中移除武器或动画时，资源仍保留在资源库中，引用计数变为 0。
- 修改源文件后，资源状态显示 `SourceChanged`，重新导入后恢复 `Clean`。
- 手动删除 Unity asset 后，状态显示 `UnityMissing`。
- 同 stable id + 同 import hash 重复导入会跳过。
- `character resources validate` 能在无 Unity 场景下输出资源库诊断。
- Unity Editor 可以导出 `fmod_audio_library.json`；CharacterStudio 可以在不直接引用 FMODUnity 的情况下读取并筛选音频项。

## 建议拆分 Issue

### Resource Library 01：Catalog Schema and Reference Index

- 扩展 package resource catalog 的 kind / usage / source / compatibility / preview 语义。
- 实现引用关系扫描。
- 输出资源库 diagnostics。

### Resource Library 02：Authoring Server API

- 增加 resources list / inspect / validate API。
- 返回引用计数、Unity 状态和 diagnostics。
- 保持 API 不依赖 Unity Editor。

### Resource Library 03：CharacterStudio Resource Browser

- 新增资源库板块。
- 支持筛选、搜索、缩略图、状态和引用关系。
- 点击资源显示详情。

### Resource Library 04：Resource Picker Fields

- 主体模型、武器模型、缩略图、后续动画字段改为选择器。
- 根据字段类型自动过滤资源。
- 显示兼容性 warning。

### Resource Library 05：Unity Asset Source Support

- 支持把 Unity 内部 asset 注册到资源库。
- 记录 GUID、path、type、sub-assets。
- 和 `unity_resource_catalog.json` 对齐。

### Resource Library 06：Import Idempotency and Orphan Handling

- 完成 Clean / SourceChanged / UnityMissing / Conflict / OrphanCandidate 状态机。
- 删除引用不删资源。
- 重复导入可跳过。

### Resource Library 07：FMOD Audio Library Export

- Unity Editor 侧读取 `FMODUnity.EventManager.Events/Banks/Parameters`。
- 导出 `fmod_audio_library.json`。
- CharacterStudio 资源库显示 FMOD event、bank、参数和诊断状态。
- 选择 FMOD event 时生成 / 更新 AudioEventDefinition，而不是写普通 ResourceKey。

## 后续衔接

资源库完成后，再继续讨论动画归属会更清晰：

- 动画资源先作为 `animationClip` / `animationClipGroup` 被准备和分类。
- 后续动画配置只从资源库选择资源，不处理文件导入细节。
- 装备状态、武器类型、动作集和动画 profile 的归属关系可以在独立设计中决策。
