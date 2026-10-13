# Character Resource Package 角色创作方案

> Status: Draft
> Scope: 角色资源包、外部 3D 装配编辑器、Authoring Compiler、Unity 导入桥、Runtime Spawn 垂直切片
> Audience: 框架开发者、工具开发者、技术策划
> Implementation plan: `Docs/CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md`

## 结论

这套方案可以做。它的核心不是把 Unity Editor 包一层外壳，而是把角色做成一个可独立创作、可校验、可导入 Unity、可被 Runtime 生成的资源包。

最终形态接近“角色资源包 / 角色 Mod 包”：

```text
Character Resource Package
  -> 外部 3D 装配编辑器
  -> Authoring Compiler 预检和编译
  -> Unity Importer Bridge 导入资源和配置
  -> Runtime Spawn 生成可活动角色
```

第一版目标不是替代建模软件，也不是做完整动画状态机编辑器。第一版只负责把一个角色进入框架所需的数据可视化装配好，并保证导入 Unity 后 Runtime 能用。

## 武器配置开发基准

全局单一武器/公共组合/动画/能力/受击配置、角色组合引用与覆盖、显式空手、角色 weapon slot 和 6.0/4.0 → 7.0/5.0 → 8.0/6.0 事务迁移统一以 [`Docs/CHARACTER_WEAPON_CONFIGURATION.md`](CHARACTER_WEAPON_CONFIGURATION.md) 为准。本文不再维护独立的武器 Schema。

当前源码已实现 Character Application 8.0 / Global Library 6.0：单一武器拥有模型姿态和完整武器局部 Trace，公共组合拥有主副手与行为默认；角色只保存 `weaponSlots[]`、公共组合 bindings 和可选行为覆盖。动作正文来自正式 Common/角色 Action Library，组合只保存动作槽引用与命令连段。显式 `weapon.unarmed` 与“无覆盖即继承公共默认”是运行中的 authoring 契约。

## 最终效果

设计者拿到一个角色资源包，例如 `CodexFlowTest.mxchar` 或目录包：

```text
CodexFlowTest.mxchar/
  manifest.json
  resource_catalog.json
  resources/
    models/
    textures/
    materials/
    animations/
    audio/
    vfx/
  previews/
  config/
  geometry/
  validation/
```

外部编辑器打开这个包后，用户看到的是角色装配工作台：

- 顶部是稳定流程条：`源资产`、`资源绑定`、`动画/受击`、`动作`、`Unity 导入`、`入游戏验证`，并显示当前 readiness、blocker / warning 数量和下一步主动作。冻结的能力兼容数据不进入普通流程或下一步。
- 左侧默认是流程导航和当前角色字段入口；完整包资源树、manifest/config/slot/body part 等技术节点收在 `高级 / 调试树`。
- 中央是 3D 视口：显示角色模型、胶囊体、身体部位碰撞框、hit zone、武器 socket、武器 trace。
- 右侧默认是 `当前流程` 总览：下一步、问题卡、关键依赖和文件状态；选中 3D 对象或高级节点后才显示对应 Inspector，例如 collider shape、partId、hitZoneId、socketId、bone path、local pose、trace radius 和可选 proximal range。
- readiness blocker / warning 用中文问题卡呈现，技术 diagnostic code 作为次级信息；raw manifest / JSON 调试信息只在显式 Debug 区域展开。

完成后点击“导入 Unity”，Unity Importer Bridge 会把角色源文件、公共资源引用、生成配置、geometry binding、Unity catalog、Prefab 状态和 CharacterAction 同步结果写入 Unity 项目。Runtime Spawn 再通过 `CharacterPackageResolver` 解析角色，生成可活动实例。

## 设计边界

Character Resource Package 是源头。Unity 项目不是外部编辑器的前置输入。

这意味着：

- 外部编辑器直接打开角色资源包，不要求 Unity 先导出模型或骨骼工作包。
- 角色主体模型、贴图、geometry、socket、weapon slot 和角色配置在角色包内组织；公共武器资产由所属 producer snapshot 聚合进 `global_resource_mapping_profile.v2.json`，武器自身姿态与 Trace 保存在 Global Library，角色不保存针对具体武器的覆盖层。
- Unity Importer 负责把包内资源导入项目，并把包内 ResourceKey 映射到 Unity 项目的 ResourceCatalog。
- Runtime 只消费导入后的配置、资源映射和 binding，不直接把未导入的资源包当运行时资源源头。

这也意味着第一版必须坚持以下限制：

- 外部 authoring DTO 不引用 `UnityEngine` 或 `UnityEditor`。
- Runtime 配置不保存 `UnityEngine.Object`、prefab、`AnimationClip` 或 material。
- 角色配置只保存初始值、规则和引用关系；当前 HP、冷却、Buff、装备实例、资源 handle 属于运行时状态。
- 外部编辑器不复制 Gameplay / Combat / CharacterControl 运行时逻辑，只调用纯 resolver 和 compiler 做预检。
- Unity Importer 不私自解释 authoring 数据，转换权威属于 Authoring Compiler。
- 贴图资源、FBX 内部材质、Unity `Material` 资产和 Prefab renderer 绑定是四个不同层。传统路径的 noEngine authoring 追踪和同步 body texture，Unity Prefab builder 在生成层用 material template 把 `body-albedo` 绑定到 body renderer，但该绑定不写回 runtime 配置。显式 `mx.characterPaletteBinding.v5` 是独立路径：它声明共享 RGB Color Palette ResourceKey、`colorUvSurfaceProfileVertexAlpha` 寻址、`paletteDataAlpha16` Profile 权威，以及可选的角色专属 FaceDetail 资源，但不把颜色或 Profile 修改 API 写进 gameplay/runtime 配置。Binding v1–v4 返回 `FaceDetailTextureMigrationRequired`；Usage v1 仍返回 `PaletteRepaintRequired`，不做地址或最近色兼容。

## 关键对象

| 对象 | 职责 |
| --- | --- |
| `CharacterResourcePackage` | 角色创作和交换的主载体，包含资源、配置、geometry 和校验报告。 |
| `package-local resource catalog` | 包内资源索引，记录 ResourceKey、relative path、type、variant、hash 和 import hints。 |
| `CharacterAuthoringGeometry` | 身高、半径、默认 capsule、身体部位 collider、socket、角色 weapon slot 等角色 3D 装配数据。 |
| `CharacterAuthoringCompiler` | 把角色资源包编译为 runtime config patch、geometry binding、resource mapping 和 gate report。 |
| `Unity Character Importer Bridge` | 在 Unity 侧执行导入、资源落盘、ResourceCatalog 映射和写入计划。 |
| `CharacterResolvedProfile` | Workstation、Spawn、Debug、测试共用的角色解析结果。 |

## 资源包结构

> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterResourceMappingV2Producer.cs`
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringCompiler.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder*.cs`

材质描述的最终目标沿用 `<packageRoot>/materials/<sanitizedPackageId>_<sanitizedDescriptorId>.mat`；`descriptorId` 来自 `.material.json` 文件名，目录和 `sourceAssetPath` 不参与目标命名。无碰撞包保留原路径，重复导入复用现有 `.mat` 并保留其 GUID。不同资源身份的最终材质目标必须唯一：发布器按清理文件名后的完整路径做忽略大小写比较；同名、仅大小写不同、或 `Shared.Detail` 与 `Shared_Detail` 等清理后同名的输入，返回 `CharacterGeneratedMaterialTargetCollision` 和 `ImportBlocked`，禁止发布 Profile 和执行项目写入计划。作者需为这些描述设置清理后仍不同的文件名及对应 `importHints.targetRelativePath`；仅改变目录、ResourceKey、StableId、内容 hash 或源材质名不能消除目标碰撞。

Unity Prefab builder 在复用缓存或写入生成资产之前执行同样的目标唯一性预检，直接材质绑定入口也先检查完整目录；检测到碰撞时返回失败，不先修改已有材质、renderer 引用、Prefab 或生成缓存。此次采用阻断方案，不迁移正式资产，不改变 Runtime 材质语义。聚焦离线回归入口为 Authoring Tests 的 `character-material-collision`；Editor 回归为 `CharacterImportedPackagePrefabBuilderTests.MaterialTargetCollision_BlocksBeforeWritingExistingAssets`（含目录、大小写和清理碰撞），以及 `BuildPrefab_MaterialDescriptorPathMatchesV2FinalAssetContract` 的路径与重复导入 GUID 检查。测试定义不代替实际运行证据。

第一版建议使用目录包作为权威格式，归档文件只作为分发格式。

```text
CharacterPackage/
  manifest.json
  resource_catalog.json
  resources/
    models/
      character_00.fbx
      iron_sword.glb
      kite_shield.glb
    textures/
    materials/
    animations/
    audio/
    vfx/
  previews/
    thumbnail.png
    preview_model.json
  config/
    character.json
    equipment.json
    abilities.json
    combat_actions.json
  geometry/
    body_geometry.json
    body_colliders.json
    sockets.json
    weapon_slots.json
  validation/
    last_report.json
```

`manifest.json` 至少包含：

| 字段 | 说明 |
| --- | --- |
| `packageId` | 包短 ID，例如 `codex_flow_test`。 |
| `stableId` | 长期稳定 ID，例如 `charpkg.codex_flow_test`。 |
| `version` | 包版本。 |
| `kind` | 第一版固定为 `character`。 |
| `schemaVersion` | 包 schema 版本。 |
| `authoringSchemaVersion` | 3D authoring 数据版本。 |
| `coordinateConvention` | 坐标系、up/forward、单位、旋转存储格式。 |
| `dependencies` | 依赖的框架版本、共享资源包或基础包。 |
| `hashes` | source package hash、resource hash、generated config hash。 |

`resource_catalog.json` 至少包含：

| 字段 | 说明 |
| --- | --- |
| `resourceKey` | 包内稳定资源 key，例如 `char.codex_flow_test.model.body`。 |
| `localId` | ResourceKey 生成用的包内局部 ID，例如 `model.body`、`anim.combat`。 |
| `stableId` | 长期稳定资源 ID，用于跨版本、导入冲突、诊断和迁移。 |
| `typeId` | model、texture、material、animation、audio、vfx、preview。 |
| `variant` | 可选变体，例如 `default`、`lod0`、`combat`。 |
| `usage` | characterModel、weaponModel、animationClipGroup、previewThumbnail 等资源用途；它描述资源能力，不代表角色当前一定引用了该资源。 |
| `sourceFormat` | fbx、gltf、glb、anim、png、jpg、tga、json、wav、ogg 等包内源格式；模型格式的 authority 由 `usage` 和角色类型决定。 |
| `relativePath` | 包内相对路径。 |
| `hash` / `hashes.contentHash` | 内容 hash；`hash` 是兼容字段，C0.5 使用 `hashes` 作为权威结构。 |
| `hashes.importHash` | 由 source format 和 import hints 计算的导入语义 hash。 |
| `hashes.dependencyHash` | 由依赖边和依赖资源 content hash 计算的依赖语义 hash。 |
| `importHints` | Unity target path policy、target relative path、scale、材质策略、动画切分策略、坐标提示、collision/physics policy。 |
| `dependencies` | 该资源依赖的其他包内 ResourceKey，形成 package-local dependency graph；依赖边用于导入校验和 hash，不等同于角色、武器或动画配置的当前引用。 |
| `conflictPolicy` | 同 stable id、hash 未变、hash 变化时的 skip/report/upgrade/variant 策略。 |
| `preview` | thumbnail、preview mesh、placeholder 和 camera preset 元数据。 |
| `provenance` | source tool、source file、license、origin、createdBy、modifiedBy 等来源信息。 |

C0.5 v1 模型资源支持 FBX、glTF 和 GLB，不再设一个跨 usage 的单一首选格式。Humanoid `characterModel` 的权威源是 FBX，Unity Importer Bridge 使用其 `ModelImporter`、Avatar 和骨骼层级；GLB/glTF 可作为非 Humanoid / 静态资源、公共武器或从 FBX 自动生成的 preview artifact。一个 Humanoid package 可以同时含 FBX authority 和 GLB preview，但两者必须使用不同 usage / ResourceKey，GLB 不能回退覆盖 `characterModel`。动画配置通过 Animation Editor 选择已导入 clip / `.anim` 资源，角色源清单不维护必需的 `locomotion.glb`。C0.6 只输出 import/write plan，不做格式转换；C2 对 GLB/glTF 的实际解析仍依赖项目 importer。

角色、武器、动画和资源目录必须按基准文档分层：Resource Manager 管理资产导入修正；Global Library 5.0 保存单一武器、公共组合、武器自身姿态、完整 Trace 和结构化行为默认；角色 Schema 7.0 保存语义 weapon slot、组合 bindings 与可选覆盖。从角色移除 binding 不得删除公共配置或资源。

武器拥有独立资源身份，但角色包不拥有单一武器或组合武器定义。Authoring Compiler 先从全局库解析角色 binding 指向的公共组合与行为默认，再应用角色覆盖，并将单一武器 `weaponRef`、`modelPose`、`traceProfiles[]` 与角色 `weaponSlots[]` 精确组合后投影到 WeaponConfig、geometry binding 和 runtime catalog。Resource Manager 管理公共资源生命周期；CharacterStudio 管理公共武器配置、角色 bindings/overrides 和角色语义槽位。

材质和贴图的当前分层：

- 未提供 `config/character_palette_binding.json` 时沿用传统 BaseMap 流程。正式配置显式提供 `mx.characterPaletteBinding.v5` 且 `enabled=true` 时，固定要求 `atlasWidth=256`、`atlasHeight=256`、`addressingMode=colorUvSurfaceProfileVertexAlpha`、`surfaceProfileSource=paletteDataAlpha16` 与 `surfaceProfileCount=16`。Color Catalog v3 使用 RGB565：`colorIndex=(R5<<11)|(G6<<5)|B5`，65,536 个地址全部有效且 RGB8 唯一，任意输入 RGB 由 Authoring 工具量化到最近公式地址。`PaletteUV.xy` 必须落在精确像素中心，`PaletteData.A` 必须是 16 个 Profile Alpha 中心之一；颜色与 Surface Profile 可任意组合。
- 色板角色可在同一配置中显式声明 `faceDetailEnabled=true`、当前包专属的 `faceDetailTextureResourceKey=char.<packageId>.texture.face_detail` 与 `faceDetailIntensity=0..1`。资源源文件固定为角色目录内的 `FaceDetail/face_detail_rgba.png`，最终为 512×512 Direct RGBA；RGB 是最终 sRGB 细节色，A 是覆盖率。启用后模型必须提供 `UV2 / FaceDetailUV` 并直接寻址整张贴图：承载值位于 `0..1`，非承载顶点使用 `(-1,-1)`。关闭时无需贴图与 UV2；启用时缺图、共享或跨包 ResourceKey、非法尺寸/Alpha/UV、manifest/hash 不一致均必须 fail closed。Binding v1–v4 报告 `FaceDetailTextureMigrationRequired`，但不要求重画 Palette 网格。

- `ArtSource/Characters/<characterId>/character_sources.json` 是角色私有原材料清单。贴图项除 `presetId=texture` 外必须声明或可确定性推断材质 `slot` 与 `target.textureSemantic`；合法语义固定为 `Color`、`Normal`、`Mask`。
- 同一材质 slot 最多有一张同语义贴图；声明贴图的 slot 必须有一张 `Color`。同步器按 slot 生成或复用 package material descriptor，`Color / Normal / Mask` 分别映射 `_BaseMap / _WgNormalTex / _WgMaskTex`。材质表面模式当前只允许 `Opaque / AlphaClip`；`AlphaClip` 由该 slot 的 Color source target 声明 `materialSurfaceMode=AlphaClip` 与 `alphaCutoff=0..1`，并生成 AlphaTest queue 2450。未声明时兼容为 `Opaque`，不得根据 PNG 存在 Alpha 通道自行猜测。这些 shader property 与表面模式只进入 Unity-facing material descriptor，不进入 Character Application 或 noEngine gameplay 配置。
- `resource_catalog.json` 必须形成显式依赖图：主体 `characterModel --usesMaterial--> material --usesTexture--> texture`。只登记 texture entry、只复制文件或只记录源文件 hash 都不构成原材料导入完成。
- 原材料阶段以 FBX 源材质名声明绑定，模型 `importHints.metadata.rendererMaterialBindingsJson` 可使用 `bindingMode=sourceMaterialName`、空 Renderer path 和 `materialSlotIndex=-1`；这一层证明 `Body`、`Eye` 等源材质名能映射到 package material。具体 Renderer 节点和槽位覆盖必须由 Unity rig-aware import receipt 继续证明。
- 已存在且没有 `rawMaterialDescriptorOwner=CharacterStudio.ImportMaterials` 的 material descriptor 视为手写权威；同步器可以读取并重建 catalog 依赖，但不得覆盖文件。工具生成的 descriptor 才允许在后续同步中更新。
- Unity `Material` 资产是 Unity 导入 / Prefab builder 的产物，不应写入 noEngine runtime 配置。
- Prefab renderer 绑定是 Unity Editor bridge / Prefab builder 的最终验收对象。material descriptor 可以保存 Unity shader slot，但普通角色配置只能保存抽象 surface / ResourceKey 引用，不能直接写 `_BaseMap`。
- Public weapon material binding follows the same template rule, but belongs to the common weapon resource, not the character package. A public `weapon_sources.json` may include an optional texture item with `presetId = "texture"`, `target.resourceUsage = "texture"`, `target.resourceKind = "Texture"`, and `target.bindTarget = "weapon.albedo"`; the recommended generated key is `weapon.<weaponId>.texture.albedo`. Unity sync copies that texture under `Assets/MxFrameworkGenerated/Common/Weapons/<weaponId>/textures/` and registers it in the global profile with the `common.textures` bundle rule.
- `CommonWeaponWrapperPrefabBuilder` generates `Assets/MxFrameworkGenerated/Common/Weapons/<weaponId>/materials/<weaponId>_Weapon.mat` with the `wgame.prop_stylized_lit` material template. The template maps abstract inputs `surface.albedoTexture`, `surface.albedoTint`, and `surface.albedoWeight` to the current `WGame/PropStylizedLit` shader slots. Texture resolution order is: explicit `weapon.albedo` texture resource, imported FBX/GLB renderer material texture or base color, then a visible default prop material with `weaponMaterialBindingStatus = noWeaponAlbedo`. A public source may instead declare root-level `paletteMode = "sharedWGameAtlas"`; then the wrapper uses `wgame.prop_palette_stylized_lit` and inherits the holder character's palette row. The global single-weapon configuration stores the public reference; the character package does not copy public weapon prefabs, materials, or textures into package-local weapon resources.

原材料同步状态写入 package-local `config/import_materials_state.json` schema 2.0。除逐项 `lastImportedHash / lastImportedUtc` 外，必须持久化 `integrityReceipt`，至少包含状态、贴图数、预期/已绑定材质数、源材质绑定数、逐材质证据和结构化 diagnostics。`Ready` 只允许在当前文件、材质描述、依赖图和源材质名绑定全部通过时返回；源文件全部为 `Clean` 不能覆盖完整性 blocker。稳定诊断至少包括：

- `TEXTURE_SEMANTIC_UNRESOLVED` / `TEXTURE_MATERIAL_SLOT_UNRESOLVED` / `TEXTURE_SEMANTIC_AMBIGUOUS`
- `MATERIAL_BASE_COLOR_MISSING` / `MATERIAL_DESCRIPTOR_MISSING` / `MATERIAL_DESCRIPTOR_INVALID`
- `MATERIAL_SURFACE_MODE_UNSUPPORTED` / `MATERIAL_ALPHA_CUTOFF_INVALID` / `MATERIAL_SURFACE_MODE_MISMATCH` / `MATERIAL_SURFACE_MODE_RESOLVED`
- `MATERIAL_TEXTURE_RESOURCE_MISSING` / `TEXTURE_SEMANTIC_UNBOUND`
- `DEPENDENCY_GRAPH_INCOMPLETE` / `RENDERER_BINDING_INCOMPLETE`

自动修复必须在同步响应及持久化回执中记录 `MATERIAL_DESCRIPTOR_GENERATED|UPDATED`、`DEPENDENCY_GRAPH_RECONCILED`、`SOURCE_MATERIAL_BINDINGS_RECONCILED`。每个材质还必须记录 `MATERIAL_SURFACE_MODE_RESOLVED` 以及最终 `surfaceMode/alphaCutoff`；原材料回执必须明确记录 `UNITY_RENDERER_SLOT_VERIFICATION_DEFERRED`，避免把源材质名检查误报为 Unity Renderer 槽位已验收。

## 3D Authoring 数据

角色资源包必须能表达不同形态的角色：人形、奇幻生物、简单几何体 Actor 都使用同一套抽象。

第一版关键数据：

| 数据 | 示例 |
| --- | --- |
| body geometry | 角色身高、半径、默认 capsule center、模型缩放、质量、坐标系。 |
| body presence | 可选 `geometry/body_presence.json`；最多 4 个 `ModelRoot` sphere / capsule / box，描述低频稳定核心体积，不替代 Hard Root、动画 Hurtbox 或攻击 Trace。 |
| body parts | `head`、`torso`、`left_hand`、`tail`、`core`、`front_face`。 |
| colliders | 每个部位一个或多个 capsule / box / sphere。 |
| hit zone binding | `hit.head` -> `head`，包含优先级、弱点、倍率。 |
| sockets | `mainHand`、`offHand`、`back`、`headVfx`，绑定 bone path 或 locator path。 |
| character weapon slot | 当前角色的主手/副手等语义槽位绑定哪个 Socket，以及槽位局部位置/旋转；与具体武器无关。 |
| single weapon model pose / trace | 武器根节点相对槽位的自身变换，以及武器局部空间的完整攻击轨迹；归 Global Library。 |
| trace | 剑刃 trace 起点、终点、半径、采样规则。 |

第一版 collider shape 只支持：

- capsule
- box
- sphere

convex 和 custom mesh 可以保留枚举值，但 v1 校验必须标记为 unsupported。

Body Presence 缺文件（`Missing`）与显式空 `Current` 文档语义不同。当前 schema 只允许独立保存 `Current`；Future（服务状态 `UnsupportedSchema`）与 `Invalid` 必须原文只读，整包保存不得成为第二写入者。Dragon14 的 M0 fixture 使用 torso、neck/head root、pelvis/tail root 三个核心 capsule，明确排除翼膜、尾尖、头尖和攻击 Trace。该文档在 M0 只进入 compiler/runtime binding 与低频诊断，不接 motor、推挤、接触压力、伤害或网络状态。

### 骨骼绑定与局部空间

后续 CharacterStudio 和 Authoring Compiler 必须把会跟随动画运动的对象绑定到骨骼、locator、body part 或 socket，不得默认解释为 model root / 原点空间的绝对位置。

约束：

- `body parts` 是角色局部结构的稳定抽象。骨骼角色中，`PartKind=Bone` 的 body part 必须能解析到一根代表骨骼或 locator；非骨骼 Actor 可以使用 `Primitive` / `Virtual` part，并用 primitive locator 表达中心或朝向。
- `colliders` 绑定到 `PartId`。collider 的 `localPose` 是相对该 body part 代表骨骼 / locator 的局部偏移和局部旋转；如果 collider 没有显式 parent，Compiler 应继承 `PartId` 对应的 body part locator，而不是回退到 model root。
- `sockets` 必须绑定 `bonePath` 或 `locatorPath`，并且 `localPose` 是相对该骨骼 / locator 的局部 pose。socket 的可视轴向应显示父骨骼空间，便于作者判断 forward/up。
- `weaponSlots[]` 挂到角色 socket，只保存语义槽位的角色局部位置/旋转；同一语义槽位不得因不同武器保存多条记录。
- 单一武器 `modelPose` 保存武器根节点相对角色槽位的位置、旋转和缩放；Resource Manager wrapper pose 只负责导入坐标修正。
- 单一武器 `traceProfiles[]` 的起止 Locator/Pose 均处于武器局部空间，不保存世界点或角色槽位引用；动画驱动时按骨骼、角色槽位、武器自身和资源导入修正的固定顺序合成。

编辑器要求：

- 选择 collider 时，Inspector 必须先选择所属 body part；如果该 body part 代表骨骼，则 collider gizmo 在骨骼局部空间中编辑。
- 选择 body part 时，应能选择或显示代表骨骼 / locator，并显示该部位下的 colliders。
- 选择 socket 时，应能选择父 body part、bone path 或 locator path；无相对偏移时 socket 就是默认挂载姿态。
- 选择角色 weapon slot 时，只能编辑 Socket 和角色槽位位置/旋转；可临时选择任意单一武器装配预览，但不得修改武器数据。单一武器工作台独立编辑 `modelPose` 与 Trace，Resource Manager 独立编辑 wrapper/import correction。
- 预检必须报告 missing body part、missing bone / locator、collider parent unresolved、socket parent unresolved 和 weapon identity pose not aligned 等问题。

## 坐标和单位

坐标规则必须先稳定，否则外部编辑器和 Unity 导入会出现不可调试的偏差。

第一版固定：

- 存储单位默认 `1 unit = 1 meter`。
- quaternion 是旋转权威格式，Euler 只作为 UI 展示。
- 每个 local pose 必须声明父空间：model root、bone、locator、body part 或 socket。
- manifest 必须记录 up axis、forward axis、unit scale 和 handedness。
- Unity Importer 必须按同一规则转换，不能让每个 importer 自己猜。
- roundtrip 目标：authoring local pose -> Unity import -> 重新读回 authoring local pose，在误差阈值内稳定。

公共武器 FBX 的单位换算以 Unity ModelImporter 为准。开启 `Convert Units` 时，Unity 负责把源文件厘米等单位换算成米；公共 `weapon_sources.json` / global profile 中的 `modelWrapperPose` 只表达用户认可的美术偏移、旋转和资源级缩放修正，不承载 `0.01` 这类单位换算。公共武器 runtime key 应指向应用 wrapper pose 后的 Unity wrapper prefab；raw FBX 只作为源文件和 CharacterStudio 预览输入。

## 外部编辑器

外部编辑器第一版叫做 Character Resource Package 装配工作台。它不是表格数据库 UI，而是 3D 装配工具。

默认工作流：

```text
打开角色资源包
  -> 顶部流程条显示源资产 / 资源绑定 / 动画受击 / 动作 / Unity 导入 / 入游戏验证
  -> 检查 ArtSource 角色源清单和 package/resource 状态
  -> 绑定 body / texture / default weapon 引用
  -> 加载角色模型和当前默认武器预览
  -> 编辑 body geometry / colliders / sockets / character weapon slots
  -> 在单一武器工作台编辑武器自身 pose / traces
  -> 检查当前默认装备状态和 resolver 输出
  -> 运行 resolver 和 compiler 预检
  -> 保存 package
  -> 可选：调用 Unity Importer Bridge 导入 Unity
```

核心交互：

- 选择 collider，3D 视口显示 transform gizmo，右侧 Inspector 显示 shape、partId、hitZoneId、size、local pose。
- 选择 socket，视口显示轴向和父骨骼，Inspector 显示 socketId、bone path、usage、handedness。
- 选择角色 weapon slot，视口显示 Socket 与槽位局部 pose；Inspector 不显示具体武器的 Trace 或模型姿态。
- 单一武器工作台必须提供独立 3D 预览，结构化编辑模型姿态、Trace 端点、半径、近端扩展和采样规则，不依赖当前角色或组合。
- 角色整体视口显示 `activeCombinedWeaponConfigurationId` 的两个显式 slots 与确定性 runtime attachment identity，并分别提供“角色槽位”和“武器自身”归属跳转；`weapon.unarmed` 明确显示为空手。
- 点击顶部流程条或 readiness 问题卡，应进入对应流程页；可定位的问题同时选中对应 3D 对象、包内资源或字段。

外部编辑器能做：

- 打开和保存角色资源包。
- 编辑 3D 装配数据。
- 预览装备状态和资源依赖。
- 显示 resolver / compiler diagnostics。
- 调用 Unity Importer Bridge 导入 Unity。

外部编辑器不做：

- 建模、蒙皮、复杂动画曲线编辑。
- 完整 Combat action timeline 编辑。
- 完整武器资源编辑、批量武器库管理、跨角色武器复用治理。
- Gameplay / Combat 运行时模拟。
- 直接写 Unity 项目资产。

## Authoring Compiler

Compiler 是外部编辑器和 Unity Importer 的共同权威。

输入：

- `CharacterResourcePackage`
- package-local resource catalog
- 包内 dependency graph / hash report
- existing config source index
- existing project ResourceCatalog summary 或可选 Unity import target context
- compiler options：strict、allow warnings、target output format、Unity import path policy

输出：

- generated Character Application config patch
- body collider / hit zone / socket / trace binding
- package resource catalog mapping
- Unity import/write plan
- resource dependency report
- validation/gate report
- deterministic hashes
- source mapping：package path -> generated config field / import target

Compiler 必须是 noEngine 纯逻辑，不读取 Unity 场景对象，不写 Runtime world。

C0.6 的实际输出格式已经固定为：

| 输出 | 格式 |
| --- | --- |
| compile result | `mx.characterAuthoringCompileResult.v1` |
| config patch bundle | `mx.characterApplicationConfigPatchBundle.v1` |
| geometry binding | `mx.characterGeometryBinding.v1`～`v4`；只有 `Current` Body Presence 选择 v4，`Missing` 保持原 v1～v3 格式 |
| resource mapping | `mx.characterResourceMapping.v1` |
| Unity write plan | `mx.characterUnityImportWritePlan.v1` |

CLI 入口：

```bash
mx-authoring character compile \
  --package Tools/MxFramework.Authoring/samples/character-codex-flow-test \
  --out Temp/MxFrameworkAuthoring/character-codex-flow-test \
  --check-files \
  --check-hashes
```

`--out` 会写出：

- `compile_result.json`
- `generated_config_patch.json`
- `geometry_binding.json`
- `resource_mapping.json`
- `unity_import_write_plan.json`
- `gate_report.txt`

Compiler 的输入是角色资源包本身。`Project Authoring Manifest`、existing project ResourceCatalog summary 或 Unity import target context 只是校验和冲突判断辅助，不能变成外部编辑器保存角色包的前置条件。

`generated_config_patch.json` 对齐 12 张 Character Application 表，但它仍是配置初始值和引用关系，不包含当前 HP、冷却、Buff、装备实例、resource handle 或 view 实例等运行时状态。Unity Importer 后续负责把 patch bundle 适配到项目配置源；Runtime Spawn 只消费导入后的配置和 binding。

`resolverVerificationPlan` 是 C0.6 和 #216 resolver 之间的衔接契约。它声明导入后应该交给 `CharacterPackageResolver.Resolve` 的表集合、默认 loadout、预期 active equipment state、combat action set、animation profile、known ability ids 和 required resources。v1 是确定性全量编译，不要求 collider/socket 的 partial compile 或 incremental resolver diff API。

## Gate Policy

所有失败必须结构化输出，不允许静默 fallback。

| Gate | 含义 |
| --- | --- |
| `ExportBlocked` | 外部编辑器不得保存为可导入产物，但可以保存草稿。 |
| `ImportBlocked` | Unity Importer 不得写入项目资源或配置。 |
| `SpawnBlocked` | Unity 可以导入，但 Runtime 不得生成角色实例。 |
| `WarningOnly` | 可以继续，但必须进入报告。 |

每个 issue 至少包含：

- severity
- stable code
- gate
- source package path
- source object path
- field
- message
- suggested fix

v1 gate 字面值已经固定：

- `Unknown`：未知或旧版本无法解释的 gate。
- `ExportBlocked`：禁止保存为可导入 / 可分发产物，但不禁止保存 editor draft。
- `ImportBlocked`：Unity Importer 不得写入项目资源或配置。
- `SpawnBlocked`：可以导入，但 Runtime Spawn 不得生成角色实例。
- `WarningOnly`：允许继续，但必须进入报告。

后续新增 gate 必须使用保留位升级或提升 authoring schema version。

示例 code：

- `CHARPKG_MISSING_MODEL_RESOURCE`
- `CHARPKG_RESOURCE_HASH_MISMATCH`
- `CHARPKG_UNSUPPORTED_COLLIDER_SHAPE`
- `CHARPKG_RESOURCE_KEY_CONFLICT`
- `CHARPKG_COORDINATE_TARGET_MISMATCH`
- `CHARPKG_STRICT_WARNING_BLOCKED`
- `CHARPKG_SOCKET_BONE_MISSING`
- `CHARPKG_UNMAPPED_HIT_ZONE`
- `CHARPKG_IMPORT_PATH_CONFLICT`
- `CHAR_EQUIPMENT_STATE_TIE`

C0.6 gate 语义：

- unsupported convex/custom collider：`ExportBlocked`。
- missing file、hash mismatch、ResourceKey 与目标项目同 key 不同 hash：`ImportBlocked`。
- missing socket、missing collider part、missing hit zone：`SpawnBlocked`。
- source coordinate convention 与 Unity target convention 不一致但可转换：`WarningOnly`，同时输出 coordinate conversion plan。
- `--strict` 或 `--no-warnings` 下出现 warning：提升为 `ImportBlocked`。

## Unity Importer Bridge

Unity Importer Bridge 的职责是把角色资源包导入 Unity 项目。

它执行：

```text
选择 Character Resource Package
  -> 读取 manifest / resource_catalog / resources / geometry / config
  -> 调用 Authoring Compiler
  -> 检查 ImportBlocked
  -> 按 import/write plan 导入模型、贴图、材质、动画、预览资源
  -> 生成或更新 Unity project ResourceCatalog mapping
  -> 写入 config patch / package cache / adapter asset
  -> 输出导入报告
```

实际链路分成两个执行域：

- Authoring CLI / Core 负责读取 package、运行 `CharacterAuthoringCompiler`、生成 `UnityImportWritePlan`、写入资源文件和 JSON 产物、输出 `package_cache/import_report.json`。这一层不调用 Unity `AssetDatabase`，也不判断 Prefab 是否已经重建。
- Unity Editor bridge 负责消费 CharacterStudio 写入的 rebuild request，执行 `AssetDatabase.ImportAsset/Refresh`、刷新 `unity_resource_catalog.json`、验证 Humanoid Avatar source、重建角色 Prefab、同步 runtime mirror、刷新 CharacterAction Profile / ActionSet、合并角色包与 Common Action Library，并生成 weapon trace bake catalog，最后把结果写入 editor status。

当前第一版提供两个等价入口：

```bash
# 外部编辑器 / CI 直接调用
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character import-unity --package <CharacterPackage> --project-root <UnityProjectRoot> --check-files --check-hashes

# Unity batchmode 调用
Unity -batchmode -projectPath <UnityProjectRoot> \
  -executeMethod MxFramework.Editor.CharacterImport.CharacterPackageImportCommand.Import \
  -characterPackage <CharacterPackage> -quit
```

导入后默认写到 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/`。其中 `package_cache/import_report.json` 是 Authoring 文件写入审计入口，`config/character_config_patch.json`、`config/geometry_binding.json`、`config/resource_catalog_mapping.json`、`config/runtime_resource_catalog.json`、`config/character_resource_plan.json` 和 `config/unity_resource_catalog.json` 是 Runtime Spawn / Workstation 后续消费的第一批稳定 JSON 产物。`unity_resource_catalog.json` 第一版使用 `memory` provider 和 `providerData.assetPath` 映射到 Unity 项目内导入资源，不把 Unity 对象写入 noEngine 配置。

公共资源使用独立生成根和全局 profile。Schema 8.0 中，角色 binding 引用 Global Library 6.0 的公共组合 ID；`weaponRef`、`modelPose` 和 `traceProfiles[]` 只保存在全局单一武器配置，公共组合保存动画/受击/动作槽默认。编译后的 resource plan 可以引用公共资源，但不应复制公共模型、组合或动画为角色私有资产。

Prefab 构建的权威状态来自 Unity Editor bridge，而不是 import report 中的文件写入 operation。角色 Prefab 应使用当前 body FBX 自己的 Humanoid Avatar，并保持 slim 结构：模型层级、`Sockets`、非物理 `AuthoringColliders` marker transforms、`Diagnostics` 和一个无行为的 `CharacterRuntimePrefabDescriptor`。`AuthoringColliders/Collider_<id>` 只保留 Transform marker 和骨骼跟随关系，不挂载 `UnityEngine.Collider`、trigger、collision 或 `Rigidbody`；权威 shape / radius / height / size 只来自 package geometry 和 `CombatPhysicsWorld`。默认武器资源 key、package id、socket 和 grip 信息来自 package geometry / resource catalog；`CharacterDefaultEquipmentRuntimeBinder`、controller binding、locomotion、bone sync 和可选 input motion 只在实例化后由 `CharacterRuntimePrefabComposer` 动态安装。Editor bridge 必须先按 Profile 合并角色包与 `Common` Action Library，再生成 weapon trace bake request；Registry 加载失败时不得因 locomotion request 存在而继续。所有攻击有效帧必须同时具备武器 Trace frame 与装备槽位 Socket frame，且不使用静态 capsule fallback。Editor bridge status 当前报告 prefab path、输入 hash、socket 数量、weapon attachment 数量、resolved 默认武器 resource ids、Avatar source、runtime composition smoke、weapon trace bake catalog 状态和 CharacterAction Profile / ActionSet 状态；CharacterStudio 只能在这些验收项都通过时显示 Unity Import `Ready`。Prefab 构建阶段会计算并透传 weapon trace bake catalog 的输入 hash、request/artifact/frame/issue 计数和 issues。若 Authoring 写入和 Prefab 重建都成功但 UI 仍停在 `Needs Attention`，优先核对 `generated/weapon_trace_bake_catalog.json`、Prefab rebuild log 和 editor-status 中的 descriptor / composition / `weaponTraceBake*` 字段。Prefab 不应持久化项目动画 clip、默认装备 binder、controller binding、input motion、locomotion、bone sync、Unity physics collider/trigger/Rigidbody 或当前运行时装备实例。

`Skipped` 是文件写入动作的中性结果：它表示目标文件 hash 已经等于本次生成内容。`Skipped` 不能被解释成“配置没有变化”，也不能替代 Prefab、Avatar、CharacterAction Profile 或公共资源 profile 的验收。

Importer 不做：

- 不成为主创 UI。
- 不私自实现一套 authoring-to-runtime 转换。
- 不把 `UnityEngine.Object` 写进 noEngine config。
- 不把 runtime current state 写回配置。
- 不要求包内资源已经存在于 Unity 项目。
- 不构建 AssetBundle；Bundle/Profile 构建属于独立资源构建阶段。

## Runtime Spawn

Runtime Spawn 只消费导入后的产物：

- Character Application config patch
- ResourceCatalog mapping
- geometry binding
- character weapon-slot / single-weapon pose and trace projection
- compiler gate result

当前 Runtime Spawn 分两层：noEngine resolver 只负责 gate、配置解析和 binding plan；低频 runtime bootstrap 可基于 binding 创建 Gameplay live entity；Unity view 和生成 Prefab 消费由 CharacterTest / Showcase 组合根负责。

当前生成流程：

```text
CharacterSpawnRequest
  -> CharacterRuntimeBinding
  -> SpawnPlanResolver / CharacterPackageResolver
  -> ResourceDependencyReport preload binding
  -> Gameplay registration / Combat body / weapon trace binding plan
  -> optional CharacterGameplayRuntimeBootstrap
  -> Gameplay live entity / runtime handle
  -> CharacterTest composition root
  -> generated prefab / runtime resource bootstrap
```

CharacterTest Arena 现在会从已导入且 `CanSpawn` 的 generated character package 建立 Playable 目录，现有角色左右切换 UI 可选择任意目录条目，并继续通过 RuntimeCommandBuffer / RuntimeSpawn 创建角色。尚未变成跨项目通用 Playable 的部分包括完整场景流、玩家偏好 / SaveState 选择持久化、网络房间选角协议，以及非 CharacterTest 组合根的默认接入。

运行时状态独立保存：

- 当前 HP、资源值、散势压力。
- 当前装备实例、武器耐久、弹药、强化。
- Buff、冷却、动作状态。
- 部位损伤、断肢或护甲破坏状态。
- view 实例、resource handle、animation actor id。

配置表只保存初始值和规则，不保存这些当前值。

## 历史回归夹具：Codex Flow Test

第一版样例数据 `Codex Flow Test` 仍覆盖工具链回归，但不再是正式或可玩的角色。

工具测试继续使用 `character-codex-flow-test` 作为非可玩回归夹具；正式可验收角色链路以六位数字角色 000001/000002 为准。`character-slime` 只作为 schema / 非人形契约样例，不宣传为 ready 导入链路样本。

包内资源：

- body model authority：`resources/models/character_00.fbx`
- optional body preview artifact：`previews/character_00.preview.glb`
- sword model：`resources/models/iron_sword.glb`
- shield model：`resources/models/kite_shield.glb`
- locomotion animations：idle、walk、run
- combat animations：sword_attack、shield_guard、unarmed_attack
- preview thumbnail

authoring 数据：

- 身高 `1.86m`
- 默认 capsule：height `1.86`，radius `0.34`，center `(0, 0.93, 0)`
- body parts：head、torso、left_hand、right_hand、left_leg、right_leg
- colliders：head sphere、torso capsule、hands sphere
- sockets：mainHand、offHand、back
- weapon attachments：mainHand sword、offHand shield
- traces：sword blade start/end/radius/proximal range

loadout：

- unarmed
- single sword
- sword shield

预期结果：

- 外部编辑器可切换三种 loadout 预览。
- `EquipmentStateResolver` 能得到唯一 active state。
- `AbilityGrantResolver` 能显示当前有效能力。
- `ResourceDependencyResolver` 能列出模型、武器、动画、预览资源。
- Unity 导入后 Runtime 能生成玩家剑盾角色，也能生成同角色配置的敌方实例。

## 非人形适配

方案必须同时支持非人形。

示例一：Drake

- body parts：head、neck、body、left_wing、right_wing、tail、front_claw、back_claw。
- sockets：mouthVfx、tailTip、backMount。
- colliders：body capsule、wing boxes、tail capsules。
- hit zone：head 弱点，tail 可断尾，wing 命中影响飞行或散势压力。

示例二：Slime

- body kind：primitive 或 compound。
- body parts：core、shell、front_face。
- colliders：一个 sphere 或多个 overlapping sphere。
- sockets：topVfx、frontAttack。
- weapon state：可以是 innate_weapon / body_weapon，不要求实体武器模型。

这说明角色系统不能假设人形骨骼，也不能把武器写死为“手上拿的一把剑”。

C0 已用同一套 DTO 表达 `Training Slime`：`Primitive` body kind、`core/shell/front_face` body parts、sphere colliders、`topVfx/frontAttack` sockets，并保留 innate weapon / body weapon 的后续编译空间。

## 分阶段实施

这条主线的 Issue 拆分和当前状态如下：

| 阶段 | Issue | 当前状态 |
| --- | --- | --- |
| C0 | #221 | 已落地 Character Resource Package 与 3D Authoring 数据契约。 |
| C0.5 | #223 | 已落地包内资源管线、package-local resource catalog、hash、dependency graph、import hints。 |
| C0.6 | #224 | 已落地 Authoring Compiler、gate policy、import/write plan。 |
| C1 | #217 | 已落地 CharacterStudio MVP：顶部流程条、左侧流程导航、右侧流程总览、字段级 resource picker、3D viewport、collider/socket/character-slot/single-weapon 草稿、保存/compile/import/readiness/ability handoff；Body Presence 可显示 Missing/Current/Future/Invalid 与当前体积，并只通过独立 revision-gated API 保存 Current。 |
| C2 | #222 | 已落地 CLI + Unity Editor bridge、AssetDatabase 同步、公共资源 profile、slim 生成 Prefab / runtime Resources mirror、Avatar / CharacterAction / runtime composition / 默认武器 / weapon trace bake status 同步；Authoring 保存和 schema 迁移已有 revision/HTTP 409 冲突门禁，仍需补齐 Unity override/writeback 和导入目标冲突的完整产品状态。 |
| D | #218 | 已落地 Runtime Spawn binding、Gameplay live entity bootstrap、CharacterTest generated package 发现和 Arena Playable 角色选择；跨项目场景流、网络选角和 SaveState 偏好仍未通用化。 |

不能跳过 C0 / C0.5 / C0.6 直接做 UI。否则外部编辑器会变成看起来能调、实际导入和生成无法稳定复现的空壳。

## 需要优先补齐的前置功能

这套方案依赖以下能力稳定：

- Character Application 12 张配置表和 typed id。
- 纯 resolver：equipment、ability、combat action、body part hit zone、resource dependency、spawn、save state binding。
- Resource Catalog 的包级挂载、variant、hash、导入映射。
- Config patch / package cache 的写入目标。
- noEngine Authoring Core / CLI 的基础校验入口。
- Unity Editor 侧 importer command 或菜单入口。

已完成的 #215 / #216 覆盖了角色配置表和纯 resolver；#221 / #223 / #224 / #217 / #222 / #218 已形成从 source package、Authoring compile、Unity import 到 CharacterTest runtime 消费的主链路。CharacterStudio 现可预览 / 执行 schema 迁移并处理并发写冲突，CharacterTest Arena 也可从 generated package 目录选择角色。下一步优先补齐 Resource Manager / Weapon Editor、Unity override/writeback、跨项目场景流、网络选角和选择持久化。

## 主要风险

| 风险 | 处理方式 |
| --- | --- |
| 外部编辑器和 Unity 坐标不一致 | C0 固定坐标、单位、local pose 父空间和 roundtrip 误差。 |
| 资源包和 Unity 项目 ResourceCatalog 映射混乱 | C0.5 固定 package-local catalog 和 import mapping。 |
| 编辑器、Importer、Runtime 各自解释数据 | C0.6 固定 Authoring Compiler 作为唯一转换权威。 |
| UI 先行导致好看但不可用 | C1 只能在 C0/C0.5/C0.6 后做，并必须调用 compiler 预检。 |
| 非人形角色后补会推翻结构 | C0 必须用 Drake / Slime 作为契约样例校验。 |
| 运行时状态污染配置 | 所有当前值只进入 runtime state / SaveState，不进入 config。 |
| 过早支持复杂碰撞和动画 | v1 只支持 capsule / box / sphere，不做完整动画状态机编辑。 |
| Unity glTF/GLB 导入能力取决于项目 importer | Humanoid 主链使用 Unity 内置 FBX `ModelImporter`；GLB/glTF 仅在 importer 可用时作为非 Humanoid / 静态资源或 preview artifact 进入 Unity，C0.6 不隐式转换格式。 |

## Authority Pose Body Region Naming

`body parts.partId` is also the canonical authority pose region id. Runtime hit feedback resolves `DamageProfileUsage.BodyPartId` / `HitZoneId` to this id and maps it to deterministic IK / secondary bindings in the generated character package or CharacterTest fallback rig definition. This is a naming contract, not a global enum: humanoid packages can use `head`, `torso`, `left_hand`, `right_leg`; non-humanoid packages can use ids such as `core`, `shell`, `front_face`, `tail`, or `wing`.

For non-humanoid actors, author a Primitive / Virtual body part with a stable `partId`, `locatorId`, and optional `hitZoneId`. Authority pose bindings need stable channel/socket ids and fixed-point tuning, not Humanoid Animator bones. Missing region bindings are diagnostics and fall back to the `default` authority pose region; they do not change Combat damage calculation, but authority pose state itself is part of replay/hash/network snapshot once an impulse or IK target is active.

## Done Definition

这条主线完成时，应满足：

- `Codex Flow Test` 角色资源包可以被外部编辑器打开、编辑、保存。
- 用户能在 3D 视口按 body part / bone / locator 局部空间调整 body collider、socket、weapon trace。
- 武器 socket 在 identity local pose 下能给同类武器提供默认对齐姿态；per-weapon 差异进入武器资源 wrapper pose 或 weapon grip 修正。
- 用户能新建、复制、删除、设默认并切换结构化武器配置，分别检查主副手角色槽位、单一武器 model pose/trace、动画/动作/能力引用和 resolver 输出；CharacterTest Arena 可选择已导入的 Playable 角色。
- Compiler 能输出 generated config patch、geometry binding、resource mapping、write plan、gate report 和 resolver verification plan。
- Unity Importer 能导入包内资源并生成 ResourceCatalog 映射。
- Runtime Spawn 能生成可活动角色实例，并显示 resolved profile、runtime ids、equipment state、abilities、resource issues。
- SaveState roundtrip 能恢复角色 config binding，并重新解析 active equipment state。

当前维护样本使用 Character Application 8.0 / Global Weapon Library 6.0。`codex_flow_test` 通过 bindings 引用 `codex_flow_test.sword_empty` 与 `codex_flow_test.empty_empty`；Combat Girl 剑角色引用 `combat_girl_sword.sword_empty`，并解析其专用 `combat_girl_sword.sword`。能力 Authoring 冻结且不参与普通完成状态；`character-slime` 仅作为 schema / 非人形契约样例保留。

到这里，角色不再只是配置表，也不是 Unity 场景里的临时 prefab，而是框架可复用的角色资源包 authoring 到 runtime 的完整闭环。
