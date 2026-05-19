# Character Application 接口

> 状态：#216 完成纯解析器与 diagnostics。本文记录已经落地的静态配置接口、resolver DTO 和 noEngine 解析器；不包含工作台 UI 或 Runtime Spawn 实现。

## 职责

Character Application 是角色应用层的数据聚合契约。它不让 Runtime / Gameplay / Combat / Resources / Animation / CharacterControl 反向依赖角色概念，而是在应用层用配置引用把这些模块编排起来。

角色资源包 authoring 主线见 `Docs/CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`，工程实现见 `Docs/CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md`。该主线把模型、贴图、动画、武器资源、geometry、socket、trace 和角色配置放在同一个 Character Resource Package 中，由外部 3D 装配编辑器编辑，再通过 Unity Importer Bridge 导入项目。

当前程序集：

- `MxFramework.Character.Application`
- 路径：`Assets/Scripts/MxFramework/Character.Application/`
- 依赖：`MxFramework.Config`
- `noEngineReferences=true`

外部角色资源包 C0 契约当前落点：

- `MxFramework.Authoring.Core`
- 路径：`Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/`
- 依赖：无 Unity 依赖，不引用 `UnityEngine` / `UnityEditor`
- 样例：`Tools/MxFramework.Authoring/samples/character-iron-vanguard/`、`Tools/MxFramework.Authoring/samples/character-slime/`

## 公开接口

| 类型 | 用途 |
| --- | --- |
| `CharacterConfig` | 角色聚合入口，引用属性、身体、装备 schema、默认 loadout、基础能力和表现配置 |
| `CharacterAttributeProfileConfig` | 属性初始值配置，字段使用 `BaseValue` / `InitialValue`，不保存运行时当前值 |
| `CharacterBodyProfileConfig` | 身体模板，包含身体类型、部位集合、socket 和 hit zone binding |
| `CharacterBodyPartConfig` | 身体部位行，描述部位类型、父子关系、hit zone、受击反应和倍率 |
| `EquipmentSchemaConfig` | 装备槽位、互斥组和允许装备状态范围 |
| `EquipmentLoadoutConfig` | 按 slot 装配武器的预设 loadout |
| `EquipmentStateConfig` | 由装备解析出的派生状态，引用能力 loadout、Combat action set 和动画 profile |
| `WeaponConfig` | 可装备武器配置，不强绑定角色 |
| `AbilityLoadoutConfig` | 能力授予、移除和输入槽绑定 |
| `CombatActionSetConfig` | 应用层动作绑定，只映射 action key、CombatActionId、trace override 和 animation action key |
| `CharacterPresentationProfileConfig` | 模型、动画 profile 和表现资源 ResourceKey 条目 |
| `SpawnProfileConfig` | 可复用生成预设；一次性覆盖由 `CharacterSpawnRequest` 表达 |
| `CharacterApplicationConfigSchemas` | 12 张表的 schema 聚合入口 |

## 纯解析器接口

| 类型 | 用途 |
| --- | --- |
| `CharacterDiagnostic` / `CharacterValidationReport` | 稳定诊断项和聚合报告，包含 severity、stable code、来源表、来源 id、字段和消息 |
| `CharacterResolvedProfile` | Workstation、Spawn、调试报告和测试共用的角色解析结果 |
| `CharacterResourceDependencyReport` | 角色生成、装备、动画和 trace profile 的资源依赖摘要 |
| `CharacterDebugContext` | 面向调试 UI / Development Agent 的稳定摘要 |
| `EquipmentStateResolver` | 从 `EquipmentSchemaConfig` + `EquipmentLoadoutConfig` + weapon/state 配置解析 active equipment state |
| `AbilityGrantResolver` | 按 base -> weapon -> equipment state -> runtime grant -> runtime disable 顺序合并能力 |
| `CombatActionBindingResolver` | 校验 action key、CombatActionId、trace override 和 animation action key |
| `BodyPartHitZoneResolver` | 从 hit zone 解析身体部位、伤害倍率、受击反应和姿态倍率 |
| `ResourceDependencyResolver` | 收集表现资源、武器资源、动画 profile 和 trace profile 依赖 |
| `SpawnPlanResolver` | 将 `SpawnProfileConfig` + `CharacterSpawnRequest` 解析成纯数据 spawn plan |
| `SaveStateBindingResolver` | 校验 SaveState 中 config id / stable id 与当前配置的可重建性 |
| `CharacterPackageResolver` | 聚合以上 resolver，输出 `CharacterResolvedProfile`、validation report 和 resource report |

首批稳定诊断 code 使用 `CHAR_*` 字符串，例如 `CHAR_EQUIPMENT_STATE_TIE`、`CHAR_MISSING_ABILITY_LOADOUT`、`CHAR_MISSING_COMBAT_ACTION`、`CHAR_MISSING_RESOURCE_KEY`、`CHAR_UNMAPPED_HIT_ZONE`。

## 外部角色资源包 C0 契约

| 类型 | 用途 |
| --- | --- |
| `CharacterResourcePackage` | 外部 3D 角色装配编辑器和 Unity Importer Bridge 共用的角色包聚合对象 |
| `CharacterPackageManifest` | package id、stable id、版本、schema、坐标系、依赖和 hash 占位 |
| `CharacterPackageCoordinateConvention` | Unity 目标坐标约定：Y+ up、Z+ forward、1 unit = 1 meter、quaternion 权威 |
| `CharacterPackageResourceCatalog` / `CharacterPackageResourceEntry` | 包内 `ResourceKey`、type、variant、relative path、hash、import hints 和资源依赖 |
| `CharacterBodyGeometryProfile` | 身高、半径、默认 capsule、质量、模型根、骨骼根和 locator 根 |
| `CharacterBodyPartAuthoring` | 人形、奇幻生物和简单几何体共用的身体部位定义 |
| `CharacterBodyColliderProfile` | v1 capsule / box / sphere collider，绑定 partId 和 hitZoneId |
| `CharacterSocketProfile` | 武器、VFX、相机、UI、Gameplay socket / locator 绑定 |
| `WeaponAttachmentProfile` | 武器到装备槽和 socket 的挂接姿态、预览资源和 trace 摘要 |
| `WeaponTraceProfile` | trace 起止姿态、半径、采样规则和 action key 绑定 |
| `CharacterAuthoringValidationIssue` / `CharacterAuthoringValidationReport` | 稳定 code、severity、gate、sourcePath、sourceObjectPath、field、suggestedFix |
| `CharacterResourcePackageSchemas` | C0 authoring schema 和 enum domain 导出入口 |
| `CharacterResourcePackageValidator` | C0 纯校验：包身份、坐标、resource key、body part、collider、socket、attachment、v1 shape gate |

`CharacterAuthoringValidationGate` v1 固定为 `Unknown`、`ExportBlocked`、`ImportBlocked`、`SpawnBlocked`、`WarningOnly`，并保留 `Reserved1000+` 扩展位。`ExportBlocked` 只表示不能保存为可导入 / 可分发产物，不禁止 editor draft save。

## ID 规则

- 每张表都有独立 typed id，例如 `CharacterConfigId`、`EquipmentStateId`、`WeaponConfigId`。
- `IConfigData.Id` 仍返回 `int`，用于兼容现有 `ConfigTable<T>`。
- 跨表字段使用 typed id，并通过 `ConfigReferenceRule` 暴露目标 schema。
- `StableId` 用于 SaveState、Mod、调试报告和跨版本迁移。

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

## 测试入口

`Assets/Scripts/MxFramework/Tests/CharacterApplication/`

当前覆盖：

- 12 张表 schema 与 typed reference。
- Iron Vanguard 剑盾、单剑、空手三种 loadout 的 resolver 输出。
- 装备状态优先级并列、缺失 ability loadout、缺失 Combat action、缺失资源 key、未映射 hit zone、SaveState active state mismatch。
