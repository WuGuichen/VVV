# Character Application 接口

> Status: Current

> 文档版本：2026-10-09

## CharacterStudio 彻底删除（Authoring 内部接口）

- `POST /api/character/delete-plan`：请求 `{ characterId }`，只读检查六位数字角色（含已归档/已退休角色），返回 `status`、`characterIds`、`paths`、`fileCount`、`bytes`、`blockingReferences`、`planHash`。不向网页发送逐文件哈希清单。
- `POST /api/character/delete`：请求 `{ characterId, confirmCharacterId, editorsClosed, planHash }`；确认编号必须相同，停止编辑声明必须为 true。服务复用 `delete_numeric_characters.py`，重新核对计划及引用，返回 `deleted`、`already_deleted`、`partial` 或错误；过期计划、引用阻塞和部分失败返回 HTTP 409。部分失败含剩余路径，需重新预览再重试。
- 计划中的 `sharedEdits` 列出可清理的角色自有资源登记、可由它证明归属的全局运行资源登记，以及空战术获取记录，包含路径、变更前后哈希、记录类型、数量和记录身份；完整共享文件哈希参与 `planHash`。`characterRuntimeRegistration` 要求 v1 global.runtime 目录中的 AssetBundle 资源身份和专属生成路径与角色生产者记录一致；清理顺序先运行目录后制作登记。引用扫描基于拟清理后的内容，保留的其他条目仍做完整引用检查，不能通过清理登记绕过真实消费者。执行结果 `cleanedSharedRecords` 记录已完成的清理，失败先返回 `partial`，不继续删除角色文件。
- 删除范围、编号退休和外部副本边界见 [数字角色制作指南](../Guides/WGAME_NUMERIC_CHARACTER_AUTHORING.md)。服务串行执行请求，删除完成后失效角色资源缓存；仅清理计划明确列出的自有登记，不自动解除其他外部引用，不新增 Runtime 协议。不得与外部 Blender/Unity 发布或离线 CLI 写入并行操作同一角色。

## Weapon Configuration 开发基准与当前状态

全局单一武器与公共组合武器、角色引用/覆盖、CharacterStudio 两级编辑、Compiler 投影、Runtime EquipmentState 和迁移的规范契约统一定义在 [`Docs/CHARACTER_WEAPON_CONFIGURATION.md`](../CHARACTER_WEAPON_CONFIGURATION.md)。所有新开发必须先读取该文档；本文不重复维护第二套武器所有权或组合规则。具体类型及其当前隔离交付/验收边界见该文档 3.4；`WeaponTypeCatalog` 为制作/加载后的低频校验入口，查询成功路径不加载 Unity 资源。

单一武器 `PresentationConfiguration` 的可选 `BladeTrailResourceKey` / `BladeTrailResidueMilliseconds` 保存默认拖尾样式与残留时长；空资源键不生成自动拖尾，残留允许 0 或 80–150 ms，缺省 100 ms。Compiler 将它们投影到 `WeaponConfig` 同名字段、纳入被引用武器的 source hash，旧配置缺字段仍可导入。`CharacterWeaponBladeTrailStyleResolver` 复用正式装备槽/Trace/WeaponConfig 解析，拒绝另一槽位的显式 Trace。动作启用、时序与关联事件所有权见 [拖尾契约](../Guides/WEAPON_BLADE_TRAIL.md)。

当前源码和样例实现 Character Application Schema 8.0 与 Global Weapon Library 6.0：全局库保存单一武器、公共组合、武器自身姿态、完整武器局部 Trace、动画、结构化可选能力、受击和公共动作槽默认；角色包只保存与具体武器无关的 `weaponSlots[]`、`combinedWeaponConfigurationBindings[]` 和可选行为覆盖。动作正文由 Common/角色正式动作源拥有，`actions[]` 只保存角色槽到动作 ID 的引用；无覆盖时逐字段继承公共默认，组合槽位不可由角色覆盖。

能力 Authoring 当前为 `Frozen / Experimental Compatibility`：公共 DTO、Compiler 投影、Resolver 与 API 只为历史兼容保留，CharacterStudio 普通组合编辑和 readiness 不暴露能力；高级诊断可按需只读查询 `/api/character/ability-chain`，不得由此推断能力系统已经正式可用。

当前实现规则：

- 当前行为、编译产物和测试结果以源码与 focused tests 为事实依据。
- 新的字段、编辑流程或 Runtime 行为不得恢复角色本地 WeaponDefinition、缺失槽位空手或单件武器能力贡献。
- `WeaponSource/global_weapon_library.json`、显式 `weapon.unarmed`、公共 `combinedWeaponConfigurations[]`、全局动画/受击配置、兼容保留的能力数据、角色本地 bindings/overrides 与 `weaponSlots[]` 必须作为一个完整契约维护。
- Schema 2.0 只允许被 Migrator 读取；Compiler 和 Unity Import 不接受 2.0 作为可发布输入。

> 状态：#216 完成纯解析器与 diagnostics；#218 已从 Runtime Spawn binding plan 扩展到 Gameplay live entity bootstrap、CharacterTest generated prefab 消费和 Arena Playable 角色目录。本文记录已经落地的静态配置接口、resolver DTO、noEngine 解析器、导入产物到 runtime binding plan 的桥接，以及当前 runtime 接入边界；跨项目场景流、网络选角和 SaveState 选择持久化仍不在本文范围内。

## 职责

Character Application 是角色应用层的数据聚合契约。它不让 Runtime / Gameplay / Combat / Resources / Animation / CharacterControl 反向依赖角色概念，而是在应用层用配置引用把这些模块编排起来。

角色资源包 authoring 主线见 `Docs/CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`，工程实现见 `Docs/CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md`。该主线把模型、贴图、动画、武器资源、geometry、socket、trace 和角色配置放在同一个 Character Resource Package 中，由外部 3D 装配编辑器编辑，再通过 Unity Importer Bridge 导入项目。

当前程序集：

- `MxFramework.Character.Application`
- 路径：`Packages/com.vincent.wgameframework/Character.Application/`
- 依赖：`MxFramework.Config`
- `noEngineReferences=true`

Runtime Spawn 第一切片：

- `MxFramework.Character.RuntimeSpawn`
- 路径：`Packages/com.vincent.wgameframework/Character.RuntimeSpawn/`
- 依赖：`MxFramework.Character.Application`、`MxFramework.Config`、`MxFramework.Resources`、`MxFramework.Runtime`、`MxFramework.Gameplay`、`MxFramework.Combat`、`MxFramework.CharacterControl`
- `noEngineReferences=true`
- 职责：只消费 #222 导入后的 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/` 产物，读取 config patch / geometry binding / resource mapping / import report，复用 `CharacterPackageResolver`，输出 `CharacterRuntimeBinding`、Gameplay / Combat / Resource / Weapon binding plan，并为 Gameplay component world 提供低频 bootstrap 入口。

外部角色资源包 C0 / C0.5 / C0.6 契约当前落点：

- `MxFramework.Authoring.Core`
- 路径：`Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/`
- 依赖：无 Unity 依赖，不引用 `UnityEngine` / `UnityEditor`
- 样例：`Tools/MxFramework.Authoring/samples/character-codex-flow-test/`、`Tools/MxFramework.Authoring/samples/character-slime/`

## 公开接口

| 类型 | 用途 |
| --- | --- |
| `CharacterConfig` | 角色聚合入口，引用属性、身体、装备 schema、默认 loadout 和表现配置；Schema 8.0 由公共组合 binding 解析有效装备，能力引用可空 |
| `CharacterAttributeProfileConfig` | 属性初始值配置，字段使用 `BaseValue` / `InitialValue`；角色源 `config/character_attributes.json` 提供 HP / Attack / Defense 和聚限恢复参数，Stamina 保持现有初始值。档案新增可选 `BaseCohesion`、`ScatterRecoveryRate`、`ScatterRecoveryDelayFrames`、`CohesionBreakRecoveryDelayFrames`，旧包缺失时使用 100 / 2 / 360 / 270。运行时当前值仍由 Gameplay 组件维护 |
| `CharacterBodyProfileConfig` | 身体模板，包含身体类型、部位集合、socket 和 hit zone binding |
| `CharacterBodyPartConfig` | 身体部位行，描述部位类型、父子关系、hit zone、受击反应和倍率 |
| `CharacterBodyPartContactPointConfig` | PresentationOnly 部位接触音频点行，保存 stable/numeric contact point id、body part id、本地毫米偏移、半径、layer mask profile、usage/authority、开启模式、运动来源和稳定排序 |
| `EquipmentSchemaConfig` | 装备槽位、互斥组和允许装备状态范围 |
| `EquipmentLoadoutConfig` | 按 slot 装配武器的预设 loadout |
| `EquipmentStateConfig` | 由装备解析出的派生状态，引用能力 loadout、Combat action set 和动画 profile |
| `WeaponConfig` | 可装备武器配置，不强绑定角色；追加 `WeaponTypeId`，现有拖尾样式字段独立保留 |
| `WeaponCategory` / `WeaponTypeDefinition` / `WeaponTypeCatalog` | 既有九项宽泛分类、具体武器定义与校验查询目录；字段、构造与匹配契约见 [武器类型与动作准入](../CHARACTER_WEAPON_CONFIGURATION.md#34-实体武器类型与动作准入) |
| `WeaponBladeTrailStyle` | 表现层拖尾类型：ResourceDefault(0)、ThinBlade(1)、SoftRibbon(2)、DoubleBlade(3)、Energy(4)；`WeaponConfig.BladeTrailStyleId` 缺省为 0。 |
| `CharacterEquipmentSlotIds` | 常用装备 slot id 常量，例如 `MainHand` / `OffHand` |
| `CharacterWeaponTagIds` | 常用 weapon tag id 常量，例如 `Blade` / `Shield` |
| `EquipmentMatchRuleToken` | 生成 `EquipmentMatchRule` 使用的 `"slot:token"` 规则字符串 |
| `AbilityLoadoutConfig` | 能力授予、移除和输入槽绑定 |
| `CombatActionSetConfig` | 应用层动作绑定，只映射 action key、CombatActionId、trace override 和 animation action key |
| `CharacterPresentationProfileConfig` | 模型、动画 profile 和表现资源 ResourceKey 条目 |
| `SpawnProfileConfig` | 可复用生成预设；一次性覆盖由 `CharacterSpawnRequest` 表达 |
| `CharacterApplicationConfigSchemas` | 13 张表的 schema 聚合入口 |

`CharacterBodyPartContactPointConfig` 当前作为 CharacterStudio / 外部角色资源包导出的表现层接触音频点配置。`Usage` 默认 `PresentationAudio`，`Authority` 默认 `PresentationOnly`；CharacterTest 可在表现层读取当前 Animator / socket / bone 结果来驱动接触探测和 3D one-shot，不承诺进入 Combat authority、Replay hash、SaveState 或 network state。`LocalOffset*Millimeters`、`RadiusMillimeters`、`ContactPointId`、`BodyPartId` 和 `SourceOrder` 仍保持稳定，便于人眼审查、规则映射和 debug。

## 纯解析器接口

| 类型 | 用途 |
| --- | --- |
| `CharacterDiagnostic` / `CharacterValidationReport` | 稳定诊断项和聚合报告，包含 severity、stable code、来源表、来源 id、字段和消息 |
| `CharacterResolvedProfile` | Workstation、Spawn、调试报告和测试共用的角色解析结果 |
| `CharacterResourceDependencyReport` | 角色生成、装备、动画和 trace profile 的资源依赖摘要 |
| `CharacterDebugContext` | 面向调试 UI / Development Agent 的稳定摘要 |
| `EquipmentStateResolver` | 从 `EquipmentSchemaConfig` + `EquipmentLoadoutConfig` + weapon/state 配置解析 active equipment state |
| `AbilityGrantResolver` | 运行时仍可解析历史通用 grant/disable；Schema 8.0 Authoring 的能力入口仍冻结，空组合引用生成 `GrantedAbilityLoadoutId=0` |
| `CombatActionBindingResolver` | 校验 action key、CombatActionId、trace override 和 animation action key |
| `BodyPartHitZoneResolver` | 从 hit zone 解析身体部位、伤害倍率、受击反应和冲击倍率 |
| `ResourceDependencyResolver` | 收集表现资源、武器资源、动画 profile 和 trace profile 依赖 |
| `SpawnPlanResolver` | 将 `SpawnProfileConfig` + `CharacterSpawnRequest` 解析成纯数据 spawn plan |
| `SaveStateBindingResolver` | 校验 SaveState 中 config id / stable id 与当前配置的可重建性 |
| `CharacterPackageResolver` | 聚合以上 resolver，输出 `CharacterResolvedProfile`、validation report 和 resource report |

首批稳定诊断 code 使用 `CHAR_*` 字符串，例如 `CHAR_EQUIPMENT_STATE_TIE`、`CHAR_MISSING_ABILITY_LOADOUT`、`CHAR_MISSING_COMBAT_ACTION`、`CHAR_MISSING_RESOURCE_KEY`、`CHAR_UNMAPPED_HIT_ZONE`。

CharacterTest CD-M0 在 Hurtbox 同步时复用 `BodyPartHitZoneResolver` 生成伤害几何快照的 effective Part/HitZone 元数据，使预演基础数据与实际命中解析保持同源。解析失败时继续沿用当前 CharacterTest 的 `Body/Body` 兼容默认值，不改变旧角色包的命中行为。CD-M2 已在此基础上增加 `mx.characterDamageZones.v1`（schema `1.0`）的严格 Authoring 校验和 `geometry_binding.json` v2 Runtime 投影；未知/无效 schema lossless 只读，缺失文件不补写并继续生成兼容 v1 binding。

## 外部角色资源包 C0 / C0.5 / C0.6 契约

| 类型 | 用途 |
| --- | --- |
| `CharacterResourcePackage` | 外部 3D 角色装配编辑器和 Unity Importer Bridge 共用的角色包聚合对象 |
| `CharacterApplicationAuthoringSummary` | `config/character_application.json` 的轻量摘要，记录角色、身体、属性、装备 schema、loadout 和资源 key 等 compiler 输入线索 |
| `CharacterPackageManifest` | package id、stable id、版本、schema、坐标系、依赖和 hash 占位 |
| `CharacterPackageCoordinateConvention` | Unity 目标坐标约定：Y+ up、Z+ forward、1 unit = 1 meter、quaternion 权威 |
| `CharacterPackageResourceCatalog` / `CharacterPackageResourceEntry` | 包内 `ResourceKey`、local id、stable id、type、variant、usage、source format、relative path、hash、import hints、资源依赖、冲突策略、预览和来源元数据 |
| `CharacterSourceManifestDocument` / `CharacterSourceManifestItem` / `CharacterSourceTarget` | `ArtSource/Characters/<characterId>/character_sources.json` 的低频人工事实源；保存文件/公共引用、目标资源、材质 slot 与贴图语义，不保存 imported hash |
| `CharacterAuthoringManifestDocument` / `CharacterAuthoringManifestItem` / `CharacterAuthoringManifestValidator` | 六位数字新角色的可选 DCC/模型图/里程碑/provenance 制作清单；未声明资产不阻断，已声明资产必须满足标准目录、SHA-256 与 LFS 规则，不进入 Runtime compiler hash |
| `CharacterTextureSemanticIds` | 原材料贴图语义常量与归一化入口；当前只接受 `Color`、`Normal`、`Mask` |
| `CharacterMaterialSurfaceModeIds` | 原材料材质表面模式常量与归一化入口；当前支持 `Opaque`、`AlphaClip`，不把 Alpha Blend 伪装为已支持 |
| `CharacterImportMaterialsStateDocument` / `CharacterImportMaterialItemState` | package-local `config/import_materials_state.json` schema 2.0 工具状态；保存逐文件 hash/time 与完整性回执，不属于 package 乐观 revision |
| `CharacterImportMaterialsIntegrityReceipt` / `CharacterImportMaterialsIntegrityDiagnostic` / `CharacterImportMaterialBindingReceipt` | `LowFreqAlloc` 原材料完整性证据；区分文件同步和 model-material-texture/source-material-name 关系，返回稳定 code、建议、逐材质绑定及 `surfaceMode/alphaCutoff`；精确 Unity Renderer 槽位仍由 rig-aware Unity receipt 验收 |
| `CharacterPackageResourceHashes` | resource content hash、import hash、dependency hash，默认算法为 `sha256` |
| `CharacterPackageDependencyGraph` / `CharacterPackageDependencyEdge` | 从 resource catalog 派生的 package-local 资源依赖图 |
| `CharacterPackageResourceHashReport` | 文件存在性、声明 hash、计算 hash 和 diagnostics 输出 |
| `CharacterPackageConflictPolicy` | same stable id、hash unchanged、hash changed 的 skip/report/upgrade/variant 策略 |
| `CharacterPackagePreviewMetadata` / `CharacterPackageResourceProvenance` | 预览资源、placeholder 和来源/版权/工具元数据 |
| `CharacterPackageResourceKeyGenerator` | package-local ResourceKey 稳定生成和语法校验 |
| `CharacterBodyGeometryProfile` | 身高、半径、默认 capsule、质量、模型根、骨骼根和 locator 根 |
| `CharacterBodyPresenceDocument` / `CharacterBodyPresenceLoadResult` | 可选 `geometry/body_presence.json` 的低频稳定核心体积；区分 `Missing`、显式空 `Current`、`UnsupportedSchema` 和 `Invalid`，未知/非法原文只读保留 |
| `CharacterBodyPartDamageDocument` / `CharacterBodyPartDamageLoadResult` | 可选 `geometry/body_part_damage.json` 的伤口/结构定义；区分 `Missing`、`Current`、`Future` 和 `Invalid`，未知/非法原文只读保留，Current 按 `partStateId` 规范化 |
| `CharacterAuthorityPoseRigDocument` / `CharacterAuthorityPoseRigLoadResult` | 可选 `geometry/authority_pose_rig.json` 的定点 channel、limb/probe/IK 与 consumer attachment 作者真源；区分 `Missing`、`Current`、`Future` 和 `Invalid`，未知/非法原文只读保留 |
| `CharacterBodyPartAuthoring` | 人形、奇幻生物和简单几何体共用的身体部位定义 |
| `CharacterBodyColliderProfile` | v1 capsule / box / sphere collider，绑定 partId 和 hitZoneId |
| `CharacterSocketProfile` | 武器、VFX、相机、UI、Gameplay socket / locator 绑定；可选 `abstractBoneId` 提供跨 Humanoid / Generic / Primitive 的逻辑骨骼语义 |
| `CharacterWeaponSlotProfile` | 角色语义槽位的 Socket 与角色局部槽位姿态；不引用具体武器 |
| `SingleWeaponConfiguration.ModelPose` / `SingleWeaponTraceProfile` | 全局单一武器自身姿态，以及武器局部空间中的完整 Trace |
| `GlobalAnimationConfiguration` / `GlobalAnimationRigTypes` | 可跨角色引用的完整动画 Set / Profile 与显式 Humanoid / Generic 源 rig 声明 |
| `GlobalAnimationCompatibilityResult` / `GlobalWeaponConfigurationResolver.ResolveAnimationCompatibility` | 低频 Authoring 兼容解析，区分 Native、HumanoidRetargeting 与 Incompatible |
| `CharacterAuthoringValidationIssue` / `CharacterAuthoringValidationReport` | 稳定 code、severity、gate、sourcePath、sourceObjectPath、field、suggestedFix |
| `CharacterResourcePackageSchemas` | C0 authoring schema 和 enum domain 导出入口 |
| `CharacterResourcePackageValidator` | C0 / C0.5 纯校验：包身份、坐标、resource key、stable id、body part、collider、socket、角色武器槽位、单一武器姿态/Trace、v1 shape gate、resource path、format、dependency graph、file/hash check |
| `CharacterAuthoringCompiler` | C0.6 纯 compiler，共享于外部编辑器和 Unity Importer Bridge，不引用 Unity API |
| `CharacterAuthoringCompileRequest` / `CharacterAuthoringCompileOptions` | compiler 输入和策略，输入主对象是 `CharacterResourcePackage`，project source index / ResourceCatalog summary 只是可选校验上下文 |
| `CharacterAuthoringCompileResult` | compiler 总输出，包含 config patch、geometry binding、resource mapping、write plan、hash、gate、resolver verification 和 source mapping |
| `CharacterAuthoringCompiledConfigPatch` | 生成的 Character Application 13 表 patch bundle；字段是静态配置初始值和引用关系，不保存 runtime current state |
| `CharacterAuthoringGeometryBinding` | v1～v7 collider、hit zone、socket、由角色槽位与单一武器确定性投影的 runtime attachment、trace、coordinate conversion plan；`Current` Body Presence 进入 v4，`Current` Body Part Damage 进入 v5，合法 `Current` Authority Pose Rig 以 additive section 进入 v6，Body Presence v2 的 animated Hurtbox source references 以 additive section 进入 v7；对应可选文档 `Missing` 时保留上一兼容版本 |
| `CharacterPackageResourceMapping` | package-local `ResourceKey` 到 Unity project ResourceCatalog/import target 的映射 |
| `CharacterUnityImportWritePlan` | Unity Importer Bridge 后续执行的资源拷贝、generated config、geometry binding、resource mapping 写入计划 |
| `CharacterUnityImportBridge` / `CharacterUnityImportReport` | C2 导入桥执行 C0.6 write plan，输出 added / updated / skipped / conflicts / errors、hash、gate 和 source mapping 审计报告 |
| `CharacterResolverVerificationPlan` | 导入后应交给 `CharacterPackageResolver.Resolve` 的表集合、默认 loadout、预期 active equipment state、combat action set、animation profile、known ability ids 和 required resources |
| `CharacterPackageSourceMapping` | 包内 path / object 到生成配置字段或 Unity target 的 source mapping |
| `GET /api/character/import-materials?package=<relative-path>` | `AllocByDesign` 读取源清单、逐文件同步状态、schema 2.0 `integrityReceipt` 和模板；完整性 blocker 独立于文件行 `Clean` 状态 |
| `POST /api/character/import-materials/sync` | 同步选中/变更源文件并重建 package material descriptor、`usesMaterial/usesTexture` 依赖和 source-material-name 绑定；响应包含持久化 diagnostics，关系仍不完整时 `success=false` |
| `GET /api/character/ability-chain?package=<relative-path>` | CharacterStudio 低频只读快照；基于 compiler patch 解析当前有效组合的完整能力集合，并关联槽位输入、解析后的 CharacterAction binding 和 Runtime binding 边界 |
| `GET /api/character/body-presence?package=<relative-path>` | 读取独立 Body Presence 状态、原文、当前文档、package revision、write eligibility 与稳定 content hash；`Missing` 不自动创建文档 |
| `POST /api/character/body-presence/save?package=<relative-path>` | 只接受 `Current` 文档和匹配的 `expectedRevision`，独立写入 `geometry/body_presence.json`；Future/Invalid 返回失败且不覆盖原文 |
| `GET /api/character/body-part-damage?package=<relative-path>` | 读取独立 Body Part Damage 状态、原文、当前文档、package revision、write eligibility 与稳定 content hash；`Missing` 不自动创建文档 |
| `POST /api/character/body-part-damage/save?package=<relative-path>` | 只接受匹配 `expectedRevision` 的 Current 文档，经 T1 校验与原子事务写入 `geometry/body_part_damage.json` 并返回按 `partStateId` 规范化的重开结果；Future/Invalid 不覆盖原文 |
| `GET /api/weapon-library/references?configurationType=<type>&configurationId=<id>` | 查询单一武器、公共组合、动画、能力或受击配置的跨角色引用者；`combinedWeapon` 按角色 binding 计数，用于影响提示和受保护删除 |
| `GET /api/weapon-library/reference-index?configurationType=<type>` | `AllocByDesign` 的低频只读批量索引；一次扫描全部角色包，按配置返回引用次数、去重角色数、去重组合数和引用明细，且包含零引用配置。CharacterStudio 单一武器列表使用 `singleWeapon` 类型，旧单项接口保持兼容。 |
| `GET /api/weapon-library/configurations?configurationType=<type>` | `AllocByDesign` 的低频配置列表；返回指定类型全部记录、全局库统一 revision 和引用摘要。 |
| `POST /api/weapon-library/configuration/validate` | 无写入地校验单条全局配置草稿，返回稳定诊断；不推进 revision。 |
| `POST /api/weapon-library/configuration/save` | 按类型、稳定 ID 和 `expectedRevision` 创建或更新单条配置；稳定 ID 创建后不可修改，成功后推进全局库统一 revision，冲突返回 `409`。 |
| `POST /api/weapon-library/configuration/delete` | 按类型、稳定 ID 和 `expectedRevision` 删除单条配置；跨角色引用存在或引用未知时硬阻止。 |

C0.5 package-local ResourceKey 规则为 `char.<packageId>.<typeSegment>.<localId>[.<variant>]`。Unity 项目 ResourceCatalog 是导入后的映射目标，不是角色资源包的源头；C0.5 DTO 不保存 `UnityEngine.Object`、Prefab、`AnimationClip`、Material、Unity asset GUID 或绝对路径。

Body Presence 的写入权威仍是独立 Authoring 文档；整包 `/api/character/save` 必须保留磁盘上的文档，不充当第二写入者。LCC-M1 消费编译后 v4～v7 binding 的同一 ModelRoot Body Presence section，在 CharacterTest 中按 root pose/Yaw 派生独立 soft-contact volume；LCC-M5 的 Current v2 文档可额外保存最多 4 个 `animatedHurtboxSources`，每项只持有 stable source/collider id、skin、pressure scale 和 enabled，不复制 Hurtbox shape、bone、socket 或动画轨。Authoring/Application 不保存逐帧接触、motor、伤害或网络状态。Missing/v1 继续保持旧 binding/Hard Root 行为，Future/Invalid 继续失败关闭且原文只读。

Body Part Damage 同样由独立 T1 Authoring 文档与 revision-gated transaction 持有，整包 `/api/character/save` 只保留已持久化文件。CharacterStudio 的上下移动只是草稿便利，正式保存后的顺序恒由 `partStateId` 决定；浏览器 session draft/Undo 不是正式存储。Current 文档必须包含 `[1,8]` 个 definitions：Authoring validator/save transaction 对空定义使用 typed validation 失败关闭，RuntimeSpawn 对 v5/v6 definitions 数量越界同样拒绝绑定；Missing 仍表示合法的无定义兼容角色。有效 Current 文档编译进 geometry binding v5/v6 的 definitions、Part lookup、revision/content hash；Future/Invalid 失败关闭且不得由 UI 或 compiler 降级覆盖。

LCC-M3 在同一 `mx.characterBodyPartDamage.v1/1.0` 文档原位增加 `severable`、typed `severRequiredTag` 与 `presentationEventId`，不建立第二份 severance 文档或 UI 私有状态。三字段必须全有或全无并参与 canonical hash/source witness；旧 Current 文档按 non-severable 兼容，Future/Invalid 仍保留原始文本只读且禁止保存覆盖。CharacterStudio 已通过 create/edit/delete/reorder draft、独立 Undo/Redo、revision-gated save、save/reopen canonicalization、普通 refresh 草稿保持与 future raw 只读 smoke；Studio 只编辑 Authoring source，不写 Runtime authority、派生 leaf mask、Zone、presentation 或 network snapshot。

LCC-M4 的唯一作者真源是 `mx.characterAuthorityPoseRig.v1/1.0` 文档。合法 Current 文档按稳定 channel/limb/attachment ordinal 编译进 geometry binding v6；Missing 保留 v1～v5/base-pose 行为，Future/Invalid 保留 raw 并失败关闭，不从 Runtime `Transform` 测量或补造链长、pole、probe、partStateId 或 action tag。逐帧 base position/basis 继续复用既有 `mx.character.socketBakeCatalog.v1` 的可选 basis 字段，不新增第二套 bake catalog。正式 Dragon14 当前是 4 limbs/4 probes/12 channels、4 Collider attachments、4 Socket attachments；四肢 `partStateId` 与 `disabledActionTags` 为空。另有 3 个 PresentationOnly ContactPoint config、0 个 authority-pose ContactPoint attachment；它们不获得 terrain resolved delta，也不构成角色阻挡或推挤。

### 角色包动画资源计划的运行投影（v2）

正式角色包 `config/animation_resource_plan.json` 自导出布局 3 起为 `mx.animationResourcePlan.v2` / `2.0`，**恰好五个字段**：`format`、`schemaVersion`、`packageId`、`stableId`、`planHash`。完整 `mx.animationResourcePlan.v1` 只保留在动画编译对象、制作 API 与显式诊断快照中，不再写入正式包。

- `planHash` 原样复制自完整计划的语义哈希；Runtime 不重建它，也不得用 `config/character_resource_plan.json` 的 `planHash` 或角色级 runtime catalog 的 hash 替代——角色级与动画级不是同一集合。
- `packageId` / `stableId` 是动画包身份（`animation.<id>` / `charpkg.<id>.animation`），v2 读取时会与同包 `AnimationSetDefinition` 比对，不一致即拒绝，不自动替换身份。
- 该文件是**必需加载产物**：`CharacterCompiledAnimationArtifacts.HasRequiredArtifacts` 依赖它非 null。文件缺失走既有 `MissingCompiledAnimationArtifact` 诊断；内容非法（缺字段、非字符串、空身份、非法 hash、错版本、额外顶层字段）会让整包加载抛出 `CharacterImportedPackageJsonException`，不产生半成品对象。
- v1 与 v2 在 PackageId / StableId / PlanHash 与后续动画、Spawn 行为上等价；差异仅在 `Format` / `SchemaVersion`。因此等价性验收不应要求全对象相等。
- Player 暂存继续逐字节复制该文件，不因体积变小而改为可选或过滤项；Prefab 侧缓存键与 Bootstrap 内容哈希会因正文变化自动失效，无需手工清理缓存。

### 专用预览 GLB 的制作侧边界

`usage=previewMesh` 且有效源格式为 GLB 的资源条目（例如 `char.<id>.preview.body_mesh` → `previews/character_00.preview.glb`）属于**制作侧专用**，不进入运行投影：

- **源侧保留**：`resource_catalog.json` 的条目与源文件都不删除，CharacterStudio 继续从作者源读取它做预览。
- **运行侧排除**：`resource_catalog_mapping.json`、`unity_resource_catalog.json`、`runtime_resource_catalog.json`、`character_resource_plan.json`（含 UiDeferred）、`character_config_patch.json` 的表现资源键、resolver verification plan 均不含该运行引用；生成包不再复制该 GLB。
- **判定口径**：由 `CharacterPackageResourceExportPolicy` 统一提供，`usage` 与有效源格式两条同时成立；`typeId` 不参与判定（预览 GLB 的实际 `typeId` 是 `model`）。`previewMesh` 的 FBX/GLTF、`previewThumbnail`、正式 characterModel/weaponModel 均保持原有导出行为。
- **用途冲突**：同一物理文件若同时被非预览条目声明，文件**继续导出**（从而不被孤立清理回收），并以 `ExportBlocked` 的 `CHAR_PREVIEW_GLB_RUNTIME_REFERENCE` 报告，由资源所有者显式分类；不静默删引用、不自动换模型、不自动重标成正式用途。
- **迁移**：CLI 阶段只把这类路径标记为 `Deferred`（不删除）；真正的定向删除由 Unity 侧在 Prefab/目录重建、引用核对通过之后完成。CLI 打印 `exportCleanupPending=<n>` 表示任务尚未在 Unity 侧收口。

### Humanoid eleven-volume authoring template

CharacterStudio 的人形 11 体模板由独立 Authoring Core `CharacterHumanoidHurtboxTemplate.Generate` 提供，Service `POST /api/character/humanoid-hurtboxes` 仅返回草稿增量。模板为头、胸、腰胯与左右上臂/前臂/大腿/小腿；手脚只作为端点参与近似覆盖。输入是预览适配器在播放动画前采集的米制参考骨骼帧，输出复用现有 `BodyParts` / `Colliders` schema、稳定部位身份与唯一新 HitZone。未知 schema、歧义/缺失/退化骨骼和超过 32 体的增量失败关闭；已有相同 PartId 的受击体不被覆盖。整次插入可 Undo/Redo，正常保存仍是唯一落盘入口；模板不更改 Runtime 命中次数、伤害公式或烘焙消费合同。

### CharacterTest CD-M1 runtime boundary

CD-M1 复用当前最终 resolved CharacterAction/ActionSet、武器 `equipmentSlotId -> binding -> trace`、root motion、socket/baked source 与 geometry binding 构建 direct-hit preview；角色包/应用层只提供这些静态编译输入，不创建第二份运行时几何或目标预测真源。一次 Planner 评估冻结上一已完成模拟帧的 source/target authority，首版是 static-target，不预测目标未来运动。

`PrimaryAttack` 只对最终 resolved `BasicAttack/light_attack` action 进入 Attack Opportunity guard。`CastAbilitySlot`、Skill、Guard、Dodge 等特殊动作不误判为 PrimaryAttack，并继续既有旧范围兼容路径，适用时明确标记 `CompatibilityLegacyRange`。旧 direct-input wrapper 不是 M1 生产路径；CD-M3 的 `CandidatePose` 只接受 Compiler v3 正式 binding、sealed ModelRoot pose 和完整 revision witness，应用层不能直接伪造可用候选姿态。

M1 只消费现有 compiled geometry/body-part metadata 来冻结 Hurtbox/HitZone identity，并复用 actual damage payload eligibility；它不会伪造长期可接近 Zone，或把 package/profile HitZone 倍率复制成第二份 preview 真源。CD-M2 已实现 HitZone 到 DamageZone 的唯一归属、正交 Hittable/Aimable/Approachable、稳定编译和 CharacterStudio 编辑。CD-M3 增加 `mx.characterEngagementAnchors.v1` 与 binding v3；CharacterStudio 的普通刷新沿用现有 session draft/Undo 状态，显式启用才创建可选文档，未知 schema 原文只读且禁止覆盖。

当前数字身份导出使用 `mx.characterGeometryBinding.v8 / 8.0`，由 `features` 标识可选能力；上文 v3 指 CD-M3 最初引入的布局。接战锚点内容哈希使用 `anchorId / zoneId / phaseId` 数字身份，展示标签不参与该哈希。部位破坏使用数字 DTO 的 `mx.characterBodyPartDamage.v3 / 3.0` 包装，`bodyPartDamageContentEncodingVersion=1` 标识保留的 Authoring v1 内容见证；查询表按数字 `partId` 排序。权威姿态骨架使用 `mx.characterAuthorityPoseRig.v2 / 2.0`，Compiler 从冻结身份目录解析 channel、part、probe、IK、part-state、attachment 和 posture 的数字 ID，标签另存；其 `contentHash` 仍验证规范化 Authoring v1 内容，外层 `geometryBindingHash` 覆盖最终数字投影。详见[批量迁移实证](../CHARACTER_PREVIEW_GLB_AUTHORING_ONLY_TASK.md#1216-其它活跃角色包推广2026-09-21)。

CD-M4 不增加新的角色包或 CharacterStudio schema。联合选择只消费 Compiler 已输出的 geometry binding v1～v3、最终 ActionSet binding 和稳定 revision/content witness；旧包缺少 DamageZone/Anchor 时继续走 evaluator-backed legacy Hurtbox/普通移动兼容，不能由应用层补写虚构 Zone、Anchor 或可达结果。

C0.5 v1 模型资源允许 `fbx`、`gltf` 和 `glb`，具体权威格式按用途确定。Humanoid `characterModel` 必须以 FBX 作为主体权威源，以保留 Unity `ModelImporter`、Avatar 和骨骼动画导入链路；GLB/glTF 可用于非 Humanoid / 静态模型、公共武器和自动生成的 preview artifact，但不能替代同一 Humanoid 角色的 FBX authority。动画 FBX 永远只作为 authoring 源文件与 Take/骨架/Avatar 导入依据；Unity Importer Bridge 必须把指定 Take 提取为具有独立 `.meta` 的 `.anim` / `AnimationClip` 正式资源，运行时目录、Global Resource Mapping、Prefab 和 Animator 不得直接引用 FBX 内的 AnimationClip 子资源。指定片段不存在、提取失败或运行时地址仍指向 FBX 时必须失败关闭。重导时更新 `.anim` 内容但保留其 GUID。C0.6 输出确定性的 import/write plan 与独立 Clip 目标路径，不转换模型格式；C2 Unity Importer Bridge 直接使用 Unity 内置 FBX 导入能力，GLB/glTF 则要求项目已安装可解析对应格式的 importer。

运行时或 Editor preview 为角色动画构建临时 `ResourceCatalog` 时，必须用 `runtimeResourceKey` 在导入后的 `unity_resource_catalog.json` / Global Resource Mapping 中解析最终 `.anim` 地址；`AnimationClipRegistry.sourceSelection` 只保留创作来源与审计信息，不得把其中的 `unityAssetPath` 当作正式运行时地址。旧包只有在该字段已经明确指向独立 `.anim` 时才允许兼容读取，FBX、GLB、GLTF 或其它容器地址必须失败关闭。

`CharacterAuthoringValidationGate` v1 固定为 `Unknown`、`ExportBlocked`、`ImportBlocked`、`SpawnBlocked`、`WarningOnly`，并保留 `Reserved1000+` 扩展位。`ExportBlocked` 只表示不能保存为可导入 / 可分发产物，不禁止 editor draft save。

C0.6 compiler status 固定为 `Ready`、`WarningOnly`、`SpawnBlocked`、`ImportBlocked`、`ExportBlocked`。`ImportBlocked` 时 `UnityImportWritePlan.CanWriteToUnityProject=false`；`SpawnBlocked` 时可以导入但 `CanSpawnAfterImport=false`；coordinate mismatch 如果可转换则是 `WarningOnly` 并写入 `CharacterCoordinateConversionPlan`。

角色包编译必须先从当前组合最终选择的 `GlobalAnimationConfiguration` 构建 effective animation package，再把动画验证合并进同一个 `CharacterCompilerGateReport`，最后才能生成 Unity write plan；角色本地 `animation_authoring.json` 只提供目标 rig 身份和无全局选择时的 legacy fallback。不同角色包之间允许复用 Humanoid 动画：同 Skeleton / Avatar 为 Native；不同身份时要求源配置 `rigType=Humanoid`、目标角色为 Humanoid，并且全部 Set / Profile 显式 `allowRetargeting=true`，最终由 Unity 目标 FBX 的有效 Human Avatar 验证。配置 ID 或创建角色名称不参与兼容判定。每个有效 `EquipmentStateConfig.AnimationProfileId` 必须大小写敏感地精确匹配一个 profile；该 profile 必须且只能包含一个大小写敏感的必需 `slotId="locomotion"`。此 slot 的 `setId`、`groupId`、`defaultBlendId` 必须逐级唯一解析到同一 Group 内的一项 Blend1D 或 Blend2D，且所有 clip 引用完整。任何缺失、歧义、跨 BlendKind 重名或越组引用均为 `ExportBlocked`，只允许保留 CharacterStudio 草稿，不得由 CLI、Editor Server 或 Unity Import 绕过。

C2 Unity Importer Bridge 固定为 CLI + Unity Editor command 双入口。CLI 命令为 `character import-unity --package <path> --project-root <repo> --check-files --check-hashes`；Unity 入口为菜单 `MxFramework/Character/Import Character Package...`、`MxFramework/Character/Reimport Last Character Package` 和 batchmode `MxFramework.Editor.CharacterImport.CharacterPackageImportCommand.Import`。导入目标是 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/`，其中 `package_cache/import_report.json` 只审计 Authoring 写入，不能单独证明角色可运行；`config/unity_resource_catalog.json` 是 Unity 项目 ResourceCatalog 映射，`config/character_config_patch.json` 和 `config/geometry_binding.json` 是后续 Workstation / Runtime Spawn 的稳定输入。Unity Editor bridge 还必须合并 Profile 指定的角色包与 `Common` CharacterAction Library，成功创建 Registry。武器攻击继续写入武器 Trace Bake；`BakedSocketCapsule` 天然攻击写入独立的 Socket Bake v3 配对：`generated/character_socket_bake_catalog.json` 为小索引，`generated/character_socket_bake_catalog.bytes` 为唯一逐帧载荷；按角色包、动画和数字 socket ID 保存 Int64 量化确定性帧及可选方向基，不借用第一条武器 Trace。Registry、Clip、Trace、Locator、Socket、骨架身份或任一有效帧缺失时，editor status、Unity Import 和 Spawn 均不得标记为 Ready；不允许用静态 capsule、角色根节点或实时 Animator fallback 掩盖缺帧。

CharacterTest 的可玩角色目录以导入包 `config/character_config_patch.json` 持久化的 `runtimeCharacterId` 作为角色 ID；关卡源和场景缓存必须与数字身份目录当前分配值一致。旧包名散列仅用于报告旧引用可能对应的包，不再作为已导入角色的运行身份。Player 打包从关卡启用引用解析需暂存的角色包，并在构建前阻断缺包、不可生成角色或失效 ID。

抽象骨骼是 Socket 上的显式 Authoring 语义，不是运行时骨骼猜测。核心 ID 固定为 `root`、`body_center`、`head`、`mouth`、`left_hand`、`right_hand`、`left_foot`、`right_foot`、`tail_base`、`tail_tip`、`left_wing`、`right_wing`；角色扩展必须使用 `ext.<packageId>.<name>`。同一角色包内一个语义只能映射一个 Socket，所有可生成角色必须显式声明逻辑 `head`，旧 JSON 缺字段时保持空值而不自动推断。`localPose.parentKind=ModelRoot` 是完整的稳定根节点绑定，允许 `parentPath`、`bonePath`、`locatorPath` 与 `parentPartId` 全部为空；其局部姿态只跟随角色模型根，不参与骨骼动画。Bone、Locator 与 BodyPart Socket 则必须分别声明匹配的 `bonePath`、`locatorPath` 或 `parentPartId`。Compiler 将 `abstractBoneId` 和 pose parent 语义原样投影到 geometry binding；Unity 仅把已真实解析到骨骼、Locator、BodyPart、Primitive 或 ModelRoot 的 Socket 写入 Prefab/Runtime Prefab 的 `CharacterAbstractBoneRuntimeMap`。CharacterTest 的头顶 UI、Buff、控制状态 VFX、Foot Slip 与默认受击表现只消费该生成映射或其它显式编译绑定；Runtime 程序集禁止调用 `Animator.GetBoneTransform` / `HumanBodyBones`，缺映射时关闭对应表现并输出一次结构化诊断。

Character package 的 ResourceMapping v2 `GeneratedUnityAsset.cachedPath` 必须指向 Unity Editor bridge 完成材质化后的最终 Unity 对象，不能指向 Authoring 导入描述文件。`materialJson` 行的 `resources/**/<descriptorId>.material.json` 只用于导入和审计；对应 runtime `Material` 固定映射到 `<packageRoot>/materials/<sanitizedPackageId>_<descriptorId>.mat`，其中 `<descriptorId>` 来自描述文件名，`sourceAssetPath` 只用于匹配模型内的源材质槽。未声明 Palette Binding 的传统生成角色始终以 `_BaseMap * _Color` 作为基础色唯一权威，不提供基础色混合权重，不允许隐式框架调色板或固定灰色边缘光替换贴图颜色。正式 `config/character_palette_binding.json` 格式为 `mx.characterPaletteBinding.v5` 且 `enabled=true` 时，Unity Bridge 改用 `wgame.character_palette_stylized_lit`、共享 `256×256` RGB24 Color Palette、`UV1 / PaletteUV` 与 RGBA `PaletteData`。配置固定声明 `colorUvSurfaceProfileVertexAlpha`、`paletteDataAlpha16` 和 16 个 Profile；颜色索引与 Surface Profile 组合为 `colorIndex×16+profile`，不存在角色行或武器继承语义。Binding v1–v4 必须报告 `FaceDetailTextureMigrationRequired`；Usage v1 仍为待重画输入。该模式跳过角色专属 BaseMap、Normal 和材质 Mask。可选 `faceDetailEnabled=true` 进一步要求当前角色包专属的 `char.<packageId>.texture.face_detail`、`faceDetailIntensity=0..1` 与 `UV2 / FaceDetailUV`；512×512 Direct RGBA 的 RGB 是最终 sRGB 细节色，A 是覆盖率，Shader 直接使用完整 UV2 采样一次。非承载 UV2 使用 `(-1,-1)` 中性哨兵。关闭时不要求贴图/UV2；启用时缺失 PaletteUV/PaletteData/FaceDetailUV、越界或非中心 Color UV、非法 Profile Alpha、三角形内数据不恒定、共享/跨包 FaceDetail Key、贴图尺寸/Alpha/import settings/manifest/hash 不合法时必须 fail closed。编译器遇到没有显式最终资产解析规则的描述格式，或 `Material` 最终路径不是 `.mat` 时必须 fail closed。

Unity Editor 验收采用会话状态与持久成功凭据两层证据。`Temp/MxFrameworkAuthoring/CharacterImport/status` 是忽略版本管理的会话级 IPC；成功通过全部门禁后，Bridge 发布版本化的 `package_cache/unity_editor_import_receipt.json`。当前 Receipt 为 `mx.characterUnityEditorImportReceipt.v3 / 3.0`，记录 `packageId`、builder、五项 Compiler hash、Unity artifact hash、Prefab/Runtime Prefab、rig-aware Avatar、完整 `materials[]`、Socket/武器摘要、CharacterAction Profile/ActionSet、Weapon Trace Bake、`socketBake`、`hurtboxBake`、`abstractBones` 和 `ready=true`。色板材质仍保留 Atlas、template、Renderer 与回退诊断；Generic/Humanoid Avatar 的接受分别要求 `ValidGeneric` / `ValidHuman`。

`socketBake` 保留请求数、clip 数、轨道数、全部位置帧数 `checkedFrameCount`、输入 `hash` 与 issues，并增加 `format`、`indexPath/indexHash`、`binaryPath/binaryHash/binarySize`。两个 path 均相对角色生成包根，固定为 `generated/character_socket_bake_catalog.json` 和 `.bytes`；哈希覆盖实际文件完整字节。正式发布前通过 Runtime 加载器校验配对；制作工具的当前 session 和持久 receipt 路径均再次核对文件长度、索引身份及双文件 SHA-256。旧 v1/v2 Receipt 仅用于提示重新导入，不能 Ready；不得凭存在的 JSON 或非空哈希字段判定接受。导入请求仍携带 packageId 和五项 Compiler hash，必须逐项匹配；该 wire/receipt 版本独立于 Character Application 8.0 和 Global Library 6.0。

Readiness 取证优先级为：当前 Compiler 身份对应的会话状态、当前身份对应的持久 receipt、无有效证据时 `Deferred`。这里的“当前身份”必须同时匹配 `packageId` 与五类 Compiler hash，不能只比较 `sourcePackageHash`。当前匹配会话 `failed` 覆盖历史成功 receipt，`pending/running` 表示正在重新验收；旧身份的 Temp 状态不得覆盖当前 receipt。Receipt hash 过期或缺失 import report 表示“需要重新导入”，不属于角色内容 Error；receipt JSON 损坏、package 不匹配、路径越界，或其声称 Ready 但关键产物缺失时才是 `Blocked`。Readiness API 的 UnityImport metrics 通过 `evidenceSource`、`receiptPath` 和 `receiptStatus` 暴露证据，现有 editor-status API 保持会话兼容。

CharacterStudio 的 Unity Import Ready 必须把 Socket Bake 纳入同一门禁，并暴露 `socketBakeStatus/ClipCount/SocketTrackCount/CheckedFrameCount/IssueCount`。会话中的结构化 Socket Bake 问题直接投影为 blocker，保留缺失挂点、骨骼、动画、帧或骨架不匹配的原始 code、message 与 source path，不只显示笼统的“导入失败”。

Socket Bake 的唯一目录读取入口为 `CharacterSocketBakeCatalogLoader.TryLoadFromDirectory`，成功同时返回 Catalog 与 `CharacterSocketBakeCatalogEvidence`（格式、indexHash、binaryHash、binarySize 和轨道/帧统计）；失败返回 null Catalog 和无效证据。旧完整 JSON 必须重新导入，无自动回退。运行时查询继续使用 `PrepareClip/TryGetFrame/TryGetBasis`，不在 Tick 中读取文件或计算哈希。权威地形姿态的 base-source 内容身份取自已验证的 binaryHash。完整旧 JSON 编解码仅在测试辅助代码中保留；诊断菜单显式导出到 Assets 外。格式与迁移记录见 [Socket Bake 二进制任务](../CHARACTER_SOCKET_BAKE_BINARY_EXPORT_TASK.md)。

Socket Bake 的动画采样复用 `WeaponTracePoseSampler`，采用与 Runtime Playables 相同的根位移提取和忽略策略；缓存输入包含 `runtime-pose-v1`，旧的 AnimationMode 采样结果必须重新烘焙。000010 法杖技能的杖端对齐验证见 [角色技能适配说明](../Guides/CHARACTER_000010_STAFF_ABILITIES.md)。

CharacterStudio 不提供常规能力编辑页。`ability-chain` 仅作为“运行验证 → 高级兼容诊断”的只读历史快照，以 compiler patch 为事实源，不从 weapon attachment 数量或命名猜测能力 ID，也不参与 Ready、警告计数或下一步。CharacterAction binding 必须限定在当前 `packageId` 的 `Assets/MxFrameworkGenerated/CharacterActions/ActionSets/<packageId>/` 下，不能因为其他包复用了相同数字 `abilityId` 而误判。当前 `CharacterGameplayAbilityBindingCatalog` 是组合根显式注入的内存对象，没有可供 Authoring Server 读取的持久化 catalog；其 `Deferred` 状态只是兼容信息，不是角色创作阻塞。Gameplay 的 `AbilityDefinitionRegistry` 是另一条 input-intent ability runtime，CharacterTest 的数字快捷栏也不能替代正式能力系统证据。

## Runtime Spawn 第一切片

| 类型 | 用途 |
| --- | --- |
| `CharacterImportedPackageJson` | 从 #222 导入目录读取 `character_config_patch.json`、`geometry_binding.json`、`resource_catalog_mapping.json`、`unity_resource_catalog.json` 和 `import_report.json` |
| `CharacterImportedPackage` | 导入后角色包的 runtime 可消费聚合对象 |
| `CharacterImportedConfigSet.BodyPartContactPoints` | 导入后的 PresentationOnly 部位接触音频点配置数组，供 CharacterTest 表现层接触音频 adapter 使用 |
| `CharacterRuntimeSpawnResolver` | 执行 gate 检查、`SpawnPlanResolver`、`CharacterPackageResolver`，并生成 binding plan |
| `CharacterRuntimeBinding` | 第一切片输出，绑定 `CharacterControlEntityRef`、`CharacterSpawnPlan`、`CharacterResolvedProfile`、hash、Gameplay plan、Combat body plan、weapon attachment plan 和 resource preload plan |
| `CharacterRuntimeSpawnModule` | `RuntimeHost` 模块入口，消费排队的 `CharacterSpawnRequest` 并保存 `CharacterRuntimeSpawnResult` |
| `CharacterGameplayRuntimeBootstrap` | 低频 runtime bootstrap，基于 `CharacterRuntimeBinding` 创建 `GameplayComponentWorld` live entity、注册 runtime handle，并处理 deferred ability registry |

当前能力仍按 `Runtime Slice` 管理，不等同于完整通用 Playable。`CharacterRuntimeSpawnResolver` 本身仍是 noEngine 纯解析 / binding plan；`CharacterGameplayRuntimeBootstrap` 可以在 Gameplay component world 中创建 live entity 并维护 runtime handle，但不创建完整 Combat world、不绑定 CharacterControl 输入源，也不直接实例化 Unity view。Unity view 消费由 CharacterTest 组合根和 `CharacterTestRuntimeCharacterLoader` 读取生成 prefab / runtime resource bootstrap 完成。`SpawnBlocked` 导入报告会返回 `CharacterRuntimeSpawnStatus.SpawnBlocked`，不会创建 `CharacterRuntimeBinding`，也不得进入 Gameplay bootstrap。

Runtime Spawn 的 locomotion 选择权威只有 `ResolvedProfile.AnimationProfileId → profile → slotId="locomotion" → Set → Group → DefaultBlend`。解析结果以 `CharacterRuntimeAnimationProfileBinding` 保存 `ProfileId`、`SlotId`、`SetId`、`GroupId`、`BlendId` 和 `BlendKind`，由通用 Unity bootstrap、CharacterTest、Codex Flow 与 Combat Girl 共同消费。`ActionBindings` 仅保留攻击、闪避等动作播放职责；首个 Set/Group/Blend、Group Usage、Purpose、空 Blend、硬编码绑定以及 procedural/T Pose 都不得用于正式角色包的 locomotion 解析或成功回退。解析或资源加载失败的正式角色必须拒绝进入可玩目录或拒绝生成，并输出一次稳定错误码诊断。

## ID 规则

- 每张表都有独立 typed id，例如 `CharacterConfigId`、`EquipmentStateId`、`WeaponConfigId`。
- `IConfigData.Id` 仍返回 `int`，用于兼容现有 `ConfigTable<T>`。
- 跨表字段使用 typed id，并通过 `ConfigReferenceRule` 暴露目标 schema。
- `StableId` 用于 SaveState、Mod、调试报告和跨版本迁移。
- 常用装备 slot/tag id 通过 `CharacterEquipmentSlotIds` / `CharacterWeaponTagIds` 访问；配置 wire string 不变。
- `EquipmentMatchRuleToken.Create(slotId, token)` 只负责生成现有 resolver 消费的 `"slot:token"` 字符串，不改变 `EquipmentMatchRule` schema。

## 使用约定

- 配置只保存初始值、规则和引用关系，不保存运行时当前 HP、冷却、Buff 实例、装备实例或资源 handle。
- 角色一定有装备状态系统，但不强绑定某一件武器；空手、单武器、多槽位武器都通过 `EquipmentLoadoutConfig` 和 `EquipmentStateResolver` 解释。
- 外部角色编辑器的源头是 Character Resource Package，不是 Unity 导出的模型工作包；Unity 侧导入后才生成项目内 ResourceCatalog 映射和可用配置。
- `CombatActionSetConfig` 不复制 Combat action timeline 的 duration、hit window 或 cancel window 权威字段。
- 表现资源只保存 `CharacterResourceKeyEntry`，不直接保存 `UnityEngine.Object`、Prefab、`AnimationClip` 或 Material。
- Resolver 是纯函数：不读取 Unity 场景对象，不写 Runtime / Gameplay / Combat world，不做真实资源加载。
- 装备状态最高优先级并列时返回 `EquipmentStateResolveStatus.MultipleMatchingStates` 和 `CHAR_EQUIPMENT_STATE_TIE`，不随机选择。
- 能力重复授予只保留一份并产生 diagnostic；同 slot 或同 input intent 冲突时保留第一条绑定并报告错误。
- SaveState 恢复时 active equipment state 必须由当前装备重新解析，保存的 state id 只作为迁移 / mismatch 诊断线索。

## 烘焙表面与头发导出

头发 UV 重叠不作为错误。最终烘焙和 Unity 控制图导入以每贴图像素四个内部探测点检测头发三角面重叠，仅将重叠且 Profile 为头发的像素 R（AO）中和为 1，再生成控制图 mip。非重叠 AO、G/B/分类 Alpha 和网格 UV 保持不变；不因部分重叠禁用整个面或整个角色的 AO。Unity 的派生控制图因此可能与旧包原始 SurfaceData PNG 的重叠区 R 不同，源文件与绑定哈希不被改写。

可选 `config/character_baked_surface_binding.json` 使用 `mx.characterBakedSurfaceBinding.v1`，由 Blender 导出器生成并登记在 manifest 的 `configFiles`。它绑定当前 packageId、模型／BaseColor／SurfaceData 的包内路径与 SHA-256、512/1024/2048/4096 尺寸、工具版本及头发参数。`CharacterBakedSurfaceAuthority.Exists/Validate` 在 Authoring 编译前检查路径、哈希、布局和参数；错误通过既有 gate issue 返回 `BAKED_SURFACE_INVALID`。描述存在但非法时阻止导入，不回退。

该描述存在且有效时，色板 v5 文件只保留制作来源意义，运行时由 `wgame.character_baked_stylized_lit` 模板／仅烘焙的 `WGame/CharacterStylizedLit` 接管；不再对导出网格要求 PaletteUV 中心、逐面 PaletteData 或独立 FaceDetail。基础色是原有颜色层的线性合成结果，以 sRGB 纹理输出；不修改贴图语义。SurfaceData 的线性 RGBA 为 AO/Rim/Emission/Profile，Profile 使用既有 16 类中心值。UV0 为 BakeUV，UV1 为切线空间发流，UV2 为根梢进度和头发标记；导入保留法线与切线。分类 mip 只能选择实际存在的类别，不平均出新编号。

历史 `wgame.character_palette_stylized_lit` 模板保持 ID 与色板 v5 合同，Unity Shader 改为 `WGame/CharacterPaletteLegacy`；纯色预览也使用 Legacy。烘焙导入就绪检查以主 Shader 和两张实际纹理绑定为依据，不再依赖 `_WG_BAKED_SURFACE_ON`。主 Shader 不暴露色板绑定属性，`CharacterPaletteBinding` 不得覆盖其外观。Blender 新角色默认烘焙，已有色板角色保持历史发布，显式转换成功后由烘焙描述决定后续路径。CharacterStudio 的“角色材质”按实际描述展示烘焙、损坏状态或历史兼容，不把烘焙资源问题降级为色板显示。

烘焙材质保持独立资源归属；`CharacterPaletteBinding` 不得写入其属性。实现与制作入口见 [角色合图与头发指南](../Guides/CHARACTER_BAKED_SURFACE_AND_HAIR.md)。该能力属于表现与资源制作，不进入角色权威、回放或网络哈希。

## 测试入口

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterApplication/`

当前覆盖：

- 12 张表 schema 与 typed reference。
- 数字角色 000001 的空手、单剑等正式 loadout resolver 输出。
- 装备状态优先级并列、缺失 ability loadout、缺失 Combat action、缺失资源 key、未映射 hit zone、SaveState active state mismatch。

## 2026-09-11 Authority Pose 哈希读取

`CharacterAuthorityPoseStateComponent.AddStateHash(RuntimeHashAccumulator, string prefix)` 直接遍历当前 secondary authority 状态，预热后不创建恢复快照。字段顺序和值与 `CharacterAuthorityPoseHashUtility.AddToHash(accumulator, prefix, pose.CreateSnapshot())` 一致；恢复／网络序列化仍使用独立拥有型快照。当前 schema 和回放哈希不因此升级。

## 角色默认战术引用

000010 的杖端发射 Socket 与投射物请求 Socket Bake 收集适配见 [角色技能适配](../Guides/CHARACTER_000010_STAFF_ABILITIES.md)；正式导入和逐帧取样验收尚待执行。

`CharacterTacticalDefaults` 是 `config/character_tactical_defaults.json` 的无引擎引用 DTO，包含角色身份、初始战术动作 ID、固定版本行为卡引用及兼容 wire 结构 `CharacterTacticalSlotBinding`。玩家语义只有卡牌是否有动作槽，以及有槽实例可选的一个动作装配；`SlotPath` 仅保留作旧存档和内部执行定位，不具业务含义且不得显示。作者默认中的 `InstanceId/CardInstanceId` 保持 0 并按 CardId 唯一；玩家存档投影使用正的单调实例 ID，允许同 CardId 多实例并按实例验证动作装配唯一性。正式内容解析和实例 ID 分配由使用层负责。

Authoring 的 `CharacterResourcePackage.TacticalDefaults` 是角色源，编译结果持有独立副本；Unity 导入包的 `CharacterImportedPackage.TacticalDefaults` 读取编译投影。当前角色目录将其传入角色定义，仅用于新存档／新角色初始化，不覆盖已有玩家进度。完整使用合同见 [角色战术内容](../Guides/CHARACTER_TACTICAL_CONTENT.md)。
