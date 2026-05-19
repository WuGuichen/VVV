# 角色应用层 00：角色数据契约与纯解析器契约

> 状态：草案
> 范围：角色应用层数据契约、纯解析器契约、校验契约、运行时状态边界和验证场景
> 交付等级：设计契约

> 角色资源包、外部 3D 装配编辑器、Unity 导入和 Runtime Spawn 的完整主线见 `Docs/CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`；工程实现方案见 `Docs/CHARACTER_RESOURCE_PACKAGE_IMPLEMENTATION_PLAN.md`。

## 目标

角色是框架进入应用层后的第一个聚合对象。它不是让底层模块反向依赖的新基础类型，而是一个标准组合契约：通过 Runtime、Gameplay、Combat、Character Control、Resources、Animation、Input、UI、Debug 和 SaveState，把一个可在游戏世界里活动的 Actor 创建并驱动起来。

本阶段不先写角色运行时代码。第一步是固定数据契约、纯解析器契约和校验契约：

- 哪些静态配置表共同定义一个角色。
- 哪些数据属于运行时状态，不能写回配置表。
- 武器、装备状态、身体部位、属性、动作、动画和生成入口之间如何引用。
- 如何从多张配置表解析出 stable effective profile。
- 解析失败、引用缺失、装备状态冲突和未映射命中区如何输出结构化 diagnostics。
- 同一套契约如何支持玩家角色、敌人、NPC、Boss、召唤物、奇幻生物和简单几何体 Actor。

## 设计定位

Character 属于框架应用层：

```text
框架核心层
  Runtime / Gameplay / Combat / Attributes / Buffs / Resources / Input / UI

框架应用层
  Character / Equipment / Character spawning / Character workstation

游戏业务层
  具体职业、剧情角色、成长规则、关卡逻辑、项目专属内容
```

底层模块不依赖 Character。Character 依赖并编排现有框架模块。

## 核心规则

- 配置数据是静态定义数据，只保存初始值、规则、默认值和引用关系。
- 运行时状态保存会变化的游戏数据：当前 HP、位置、当前装备的武器实例、冷却、Buff、动作状态、部位损伤、资源 handle 和 view 实例。
- SaveState 保存运行时状态，不保存创作源配置。
- 角色不强绑定某一件具体武器；角色拥有的是装备状态系统。
- 角色始终有一个武器/装备状态。空手也是合法装备状态。
- 一件或多件已装备武器会解析为一个 active equipment state；该状态决定当前有效能力、Combat action、动画 profile、trace 绑定和表现数据。
- Player、Enemy、NPC、Boss、召唤物、奇幻生物、简单几何体 Actor 都走同一套 Character 契约；差异由 controller、team、body profile、equipment state、ability 和 presentation 决定。

## 关键不变量

- `CharacterConfig` 是静态定义，不保存任何运行时当前值。
- Character 配置不能直接持有 `UnityEngine.Object`、`AnimationClip`、prefab、material 等对象，只能保存 `ResourceKey`、`StableId` 或 typed config id。
- Character runtime state 必须能由 Config + SaveState + Resource Catalog 重建。
- `EquipmentState` 是由当前装备解析出的派生状态；运行时可以保存 `ActiveEquipmentStateId` 作为缓存和诊断值，但必须能通过当前装备重新计算。
- Resolver 必须是纯函数：相同输入得到相同输出，不读取 Unity 场景对象，不写 Runtime / Gameplay / Combat world。
- 所有 resolver 失败都必须返回结构化 diagnostics，不允许静默 fallback。
- 多个候选结果同优先级时不能随机选择。
- Presentation runtime state 不能反向驱动 Gameplay / Combat 权威状态。
- Character 可以缓存 effective profile 和 diagnostics，但缓存不是 source of truth。
- Workstation 只能编辑 Config / Authoring Patch，不直接写运行时状态。

## 框架模块复用计划

| 角色关注点 | 优先复用的框架模块 / API |
| --- | --- |
| 主循环、命令、回放、hash、存档 | `RuntimeHost`、`RuntimeCommandBuffer`、`RuntimeReplayRecorder`、`IRuntimeHashContributor`、`RuntimeSaveState` |
| 身份、队伍、生命周期、属性、能力、Buff | `GameplayComponentWorld`、Gameplay core components、component attribute / ability / buff / modifier systems |
| 移动、碰撞、动作、命中结算 | `CombatKinematicMotor`、`CombatPhysicsWorld`、`CombatActionRunner`、`HitResolveSystem` |
| 输入 / 规划器命令 | `ICharacterCommandSource`、`InputCharacterCommandSource`、`RuntimeAiPlannerCharacterCommandSource` |
| 控制状态、动作桥、受击反应 | `CharacterControlStateMachine`、`CharacterActionController`、`CharacterPressureReactionController` |
| 模型、动画、表现 | `ResourceKey`、`MxAnimationSetDefinition`、Character Control animation adapter、Combat animation bridge |
| 调试和 UI | `CharacterControlDebugSource`、Debug UI registry/snapshot、UI Toolkit controls |
| 创作和导出 | Config schema、Config tables、Resource Catalog、现有 Editor 模式 |

## 配置与运行时状态

数据模型分三层：

```text
Config Data
  静态定义：角色、身体、身体部位、装备 schema、武器类型、装备状态、
  初始属性、动作集、表现 profile、生成 profile。

Runtime State
  每个角色实例自己的可变状态：当前属性、当前装备、武器实例、
  冷却、Buff、当前移动、当前动作、部位损伤、当前 view/resource handle。

SaveState
  运行时状态的可序列化快照。
```

Config 行可以作为定义数据被 patch 或 hot reload，但普通游戏过程不会修改 config 行。

## 稳定配置表

第一版使用 12 张稳定配置表。表可以包含固定数组字段，但不能因为不同角色类型而让表结构长出不同字段。

### 1. CharacterConfig

角色总入口。它引用其他 profile，不保存当前运行时状态。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 长期稳定 ID，例如 `char.iron_vanguard` |
| `NameText` | LocalizedTextKey | 名称文本 key |
| `DescriptionText` | LocalizedTextKey | 描述文本 key |
| `BodyProfileId` | int | 引用 `CharacterBodyProfileConfig` |
| `AttributeProfileId` | int | 引用 `CharacterAttributeProfileConfig` |
| `EquipmentSchemaId` | int | 引用 `EquipmentSchemaConfig` |
| `DefaultLoadoutId` | int | 默认 `EquipmentLoadoutConfig`，可以是空手 |
| `PresentationProfileId` | int | 引用 `CharacterPresentationProfileConfig` |
| `BaseAbilityLoadoutId` | int | 不依赖装备的基础能力 |
| `DefaultControllerKind` | enum | 可选默认控制器，SpawnProfile 可覆盖 |
| `DefaultControllerProfileId` | string | 可选默认控制器 profile，例如 Runtime AI Planner profile |
| `DefaultSpawnTags` | string[] | 默认生成标签，SpawnProfile 可追加或覆盖 |
| `Tags` | string[] | 通用标签，例如 `humanoid`、`enemyLike`、`boss` |
| `Version` | int | 行版本 |

### 2. CharacterAttributeProfileConfig

只保存初始属性。运行时当前值属于 Gameplay runtime state。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `attr.iron_vanguard.base` |
| `Values` | AttributeEntry[] | 初始属性列表 |
| `Version` | int | 行版本 |

`AttributeEntry` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `AttributeId` | string | 例如 `hp.max`、`hp.current`、`move.speed` |
| `BaseValue` | int | 初始 base value |
| `InitialValue` | int | 初始 current value；运行时字段才叫 current value |
| `MinValue` | int | 可选 clamp 提示 |
| `MaxValue` | int | 可选 clamp 提示 |
| `Group` | string | 可选分组，例如 `Health`、`Pressure`、`Resource`、`Combat` |

### 3. CharacterBodyProfileConfig

身体模板。人形、奇幻生物和简单几何体共用这张表。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `body.humanoid.medium` |
| `BodyKind` | enum | `Skeletal`、`Primitive`、`Compound` |
| `SkeletonProfileId` | string | 非骨骼体可为空 |
| `PartSetId` | string | 关联 `CharacterBodyPartConfig` 中的一组部位 |
| `DefaultMotionProfileId` | string | 默认移动参数 profile key |
| `DefaultPhysicsProfileId` | string | 默认物理 / 胶囊 / 质量 profile key |
| `Sockets` | SocketEntry[] | socket 到 locator 的映射 |
| `HitZoneBindings` | CharacterHitZoneBindingEntry[] | 可选 hit zone 到 body part 的显式绑定 |
| `Version` | int | 行版本 |

`SocketEntry` 字段：`SocketId`、`LocatorId`。

`CharacterHitZoneBindingEntry` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `HitZoneId` | string | Combat 命中区 ID |
| `PartId` | string | 映射到同一 `PartSetId` 下的 body part |
| `Priority` | int | 多个绑定命中时的优先级 |
| `IsWeakPoint` | bool | 是否弱点 |
| `DamageMultiplierOverride` | float | 可选伤害倍率覆盖；0 表示不覆盖 |
| `PostureDamageScaleOverride` | float | 可选姿态压力倍率覆盖；0 表示不覆盖 |

### 4. CharacterBodyPartConfig

身体部位定义。这是支持人形、龙、史莱姆、几何体共用命中和受击流程的核心抽象。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `PartSetId` | string | 身体部位集合 ID |
| `PartId` | string | 例如 `head`、`tail`、`core`、`front_face` |
| `PartKind` | enum | `Bone`、`Primitive`、`Virtual` |
| `LocatorId` | string | 例如 `bone.Head`、`primitive.sphere.center` |
| `ParentPartId` | string | 可选父部位 |
| `HitZoneId` | string | 默认 Combat hit zone / collider 绑定；复杂映射优先使用 `HitZoneBindings` |
| `ColliderProfileId` | string | 可选部位碰撞 profile |
| `DamageMultiplier` | float | 伤害倍率 |
| `ArmorGroupId` | string | 护甲分组 key |
| `ReactionGroupId` | string | 受击反应分组 |
| `ImpulseScale` | float | 命中冲量倍率 |
| `StaggerScale` | float | 硬直倍率 |
| `PostureDamageScale` | float | 姿态压力倍率 |
| `VfxProfileId` | string | 命中特效 profile |
| `SfxProfileId` | string | 命中音效 profile |
| `Tags` | string[] | 例如 `weakPoint`、`shield`、`tail` |

### 5. CharacterPresentationProfileConfig

角色表现入口。它引用资源和动画 profile，但不拥有玩法权威状态。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `present.iron_vanguard.base` |
| `ModelKey` | ResourceKey | 角色 view prefab / model |
| `PortraitKey` | ResourceKey | 头像 / 图标 |
| `AnimationBaseProfileId` | string | 默认动画 profile |
| `SkinProfileId` | string | 皮肤 / 材质 profile |
| `UiNameplateProfileId` | string | 名牌 / 血条样式 |
| `DefaultVfxProfileId` | string | 默认 VFX profile |
| `DefaultSfxProfileId` | string | 默认 SFX profile |
| `PreloadKeys` | ResourceKey[] | 基础预加载资源 |

### 6. EquipmentSchemaConfig

装备槽、允许武器类型和槽位互斥规则。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `equip_schema.humanoid.hands` |
| `Slots` | EquipmentSlotEntry[] | 槽位定义 |
| `ExclusiveGroups` | EquipmentExclusiveGroupEntry[] | 互斥槽组 |
| `AllowedStateIds` | int[] | 允许的 `EquipmentStateConfig` ID |
| `Version` | int | 行版本 |

`EquipmentSlotEntry` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `SlotId` | string | 例如 `mainHand`、`offHand`、`twoHand` |
| `DisplayNameText` | LocalizedTextKey | 槽位显示名 |
| `AllowedWeaponCategories` | string[] | 例如 `one_hand_blade`、`shield` |
| `SocketId` | string | 身体上的默认 socket |
| `Required` | bool | 是否必须装备 |

`EquipmentExclusiveGroupEntry` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `GroupId` | string | 互斥组 ID |
| `SlotIds` | string[] | 参与互斥的槽位 |
| `MaxFilledCount` | int | 最多允许多少个槽位被填充 |

### 7. EquipmentLoadoutConfig

预设装配方案。只作为初始 / 默认数据；运行时当前装备属于 runtime state。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `equip_loadout.sword_shield` |
| `SchemaId` | int | 引用 `EquipmentSchemaConfig` |
| `Slots` | EquippedSlotEntry[] | 槽位到武器的初始分配 |
| `Version` | int | 行版本 |

`EquippedSlotEntry` 字段：`SlotId`、`WeaponId`。

### 8. EquipmentStateConfig

装备组合解析后的状态。它决定当前有效能力、Combat action、动画 profile、trace 绑定和状态级 modifier。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `equip_state.humanoid.sword_shield` |
| `SchemaId` | int | 适用的装备 schema |
| `Priority` | int | 多个状态匹配时取最高优先级 |
| `MatchRules` | EquipmentMatchRule[] | 匹配规则 |
| `GrantedAbilityLoadoutId` | int | 该状态授予的能力 loadout |
| `CombatActionSetId` | int | 当前 Combat action set |
| `AnimationProfileId` | string | 当前动画 profile |
| `ModifierEntries` | ModifierGrantEntry[] | 状态 modifier |
| `TraceBindingOverrides` | TraceBindingEntry[] | 武器 / action trace override |
| `Tags` | string[] | 例如 `unarmed`、`dual_wield`、`shielded` |

`EquipmentMatchRule` 保持简单稳定，规则之间使用 AND 语义：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `SlotId` | string | 目标槽位 |
| `RequiredCategory` | string | 需要的武器类型，可空 |
| `RequiredWeaponTag` | string | 需要的武器 tag，可空 |
| `ForbiddenWeaponTag` | string | 禁止的武器 tag，可空 |
| `MustBeEmpty` | bool | 槽位必须为空 |
| `MustBeFilled` | bool | 槽位必须已装备 |

### 9. WeaponConfig

武器类型定义。武器不绑定角色。运行时武器实例可以有耐久、弹药、强化、临时 modifier 等可变状态。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `weapon.iron_sword_01` |
| `NameText` | LocalizedTextKey | 名称文本 key |
| `Category` | string | 例如 `one_hand_blade`、`shield`、`staff` |
| `OccupiesSlots` | string[] | 占用槽位 |
| `ModelKey` | ResourceKey | 武器模型 / prefab |
| `IconKey` | ResourceKey | 武器图标 |
| `DefaultSocketId` | string | 默认挂点 socket |
| `TraceProfileId` | string | 默认武器 trace profile |
| `GrantedAbilityLoadoutId` | int | 可选武器授予能力 |
| `ModifierEntries` | ModifierGrantEntry[] | 武器基础 modifier |
| `AnimationTags` | string[] | 状态解析 / 动画选择用 tag |
| `PreloadKeys` | ResourceKey[] | 武器预加载资源 |

### 10. AbilityLoadoutConfig

能力授予集合。能力效果细节仍然走现有 Ability / Ability Graph 配置，不在 Character 里重新定义。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `ability_loadout.sword_shield` |
| `AbilityIds` | int[] | 授予能力 ID |
| `RemoveAbilityIds` | int[] | 被该 loadout 移除 / 替换的能力 |
| `SlotBindings` | AbilitySlotBinding[] | 输入 / action slot 绑定 |
| `Version` | int | 行版本 |

`AbilitySlotBinding` 字段：`SlotId`、`AbilityId`、`InputIntentId`、`ActionKind`。

### 11. CombatActionSetConfig

把逻辑 action key 映射到 Combat action、trace 和动画关联 key。第一版不复制 Combat action / timeline 的权威时间线；持续帧、hit window、cancel window 由 `CombatActionId` 指向的 Combat action 定义负责。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `actions.sword_shield` |
| `Actions` | CombatActionEntry[] | 动作列表 |
| `Version` | int | 行版本 |

`CombatActionEntry` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `ActionKey` | string | 例如 `primary`、`guard`、`secondary` |
| `CombatActionId` | int | Combat action ID |
| `TraceProfileIdOverride` | string | 可选 trace profile override；为空时使用 Combat action 默认值 |
| `AnimationActionKey` | string | MxAnimation action binding key |

### 12. SpawnProfileConfig

运行时角色实例生成方案。Player 和 Enemy 可以使用同一个 `CharacterConfig`，但使用不同 spawn profile。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int | 主键 |
| `StableId` | string | 例如 `spawn.player.iron_vanguard` |
| `CharacterId` | int | 引用 `CharacterConfig` |
| `TeamId` | int | 初始队伍 |
| `ControllerKind` | enum | `LocalInput`、`RuntimeAiPlanner`、`Replay`、`Scripted` |
| `EquipmentLoadoutId` | int | 初始装备 loadout |
| `SpawnPose` | PoseEntry | 初始位置和朝向 |
| `RuntimeAiPlannerProfileId` | string | 需要 Runtime AI Planner 时使用 |
| `DebugName` | string | Debug 显示名 |

`SpawnProfileConfig` 是可复用预设，不是唯一运行时生成输入。运行时生成应使用 `CharacterSpawnRequest`，允许关卡、召唤、Replay 或测试对预设做一次性覆盖：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `SpawnProfileId` | int | 基础 `SpawnProfileConfig` |
| `CharacterOverride` | int? | 可选角色覆盖 |
| `LoadoutOverride` | int? | 可选装备 loadout 覆盖 |
| `TeamOverride` | int? | 可选队伍覆盖 |
| `ControllerOverride` | enum? | 可选控制器覆盖 |
| `PoseOverride` | PoseEntry? | 可选生成位置覆盖 |
| `DebugNameOverride` | string | 可选 Debug 名称覆盖 |

## ID 规则

配置表同时保留 int `Id` 和 string `StableId`，但语义不同：

- `Id` 是表内短 ID，用于运行时快速引用和 typed config lookup。
- `StableId` 是长期稳定 ID，用于 Mod、迁移、SaveState 诊断、外部编辑器、Debug 报告和人工排查。
- Config 内部引用优先使用 typed id。
- Workstation 和 resolver diagnostics 必须同时输出 `Id` 与 `StableId`。
- 不同表的 int ID 不应裸用同一个类型；代码实现时应优先使用 typed wrapper，例如 `CharacterConfigId`、`EquipmentStateId`、`WeaponConfigId`。
- SaveState 不应保存完整 config 行；需要跨版本稳定性时保存 config id + stable id + version 作为诊断和迁移线索。

## 通用嵌套结构

以下嵌套结构在多张表中复用，第一版实现 DTO 时必须一起固定。

| 类型 | 字段 | 说明 |
| --- | --- | --- |
| `PoseEntry` | `X`、`Y`、`Z`、`Yaw` | 生成位置和朝向，使用 runtime 量化坐标约定 |
| `ModifierGrantEntry` | `AttributeId`、`Operation`、`Value`、`SourceTag`、`Priority` | 属性 / 状态修正基础定义 |
| `TraceBindingEntry` | `ActionKey`、`SlotId`、`WeaponId`、`TraceProfileId` | 当前动作使用哪个武器 / trace |
| `ResourceKeyEntry` | `Key`、`Required`、`Purpose` | 资源依赖项，`Purpose` 可为 model / icon / animation / vfx / sfx |
| `CharacterDiagnostic` | `Severity`、`Code`、`SourceTable`、`SourceId`、`SourceStableId`、`Field`、`Message` | 稳定诊断项 |

诊断 code 必须稳定，供 Workstation、命令行校验、Debug UI 和 Development Agent 修复上下文复用。

建议首批 code：

```text
CHAR_MISSING_BODY_PROFILE
CHAR_MISSING_ATTRIBUTE_PROFILE
CHAR_MISSING_EQUIPMENT_SCHEMA
CHAR_MISSING_DEFAULT_LOADOUT
CHAR_INVALID_LOADOUT_SCHEMA
CHAR_EQUIPMENT_STATE_NO_MATCH
CHAR_EQUIPMENT_STATE_TIE
CHAR_MISSING_WEAPON
CHAR_MISSING_ABILITY
CHAR_MISSING_COMBAT_ACTION
CHAR_MISSING_ANIMATION_ACTION
CHAR_MISSING_RESOURCE_KEY
CHAR_UNMAPPED_HIT_ZONE
CHAR_AMBIGUOUS_HIT_ZONE
CHAR_BODY_PART_PARENT_MISSING
```

## 纯解析器契约

00 阶段必须定义并优先实现纯解析器。Resolver 不写运行时 world，不读取 Unity 对象，不做资源加载，只把配置和运行时输入解析成稳定结果和 diagnostics。

### CharacterResolvedProfile

`CharacterResolvedProfile` 是 Workstation、Spawn、调试报告、自动化测试和后续 Runtime Spawn 的共同语言。

| 字段 | 说明 |
| --- | --- |
| `CharacterId` | 来源 `CharacterConfig` |
| `BodyProfileId` | 解析后的 body profile |
| `AttributeProfileId` | 解析后的 attribute profile |
| `EquipmentSchemaId` | 解析后的 equipment schema |
| `LoadoutId` | 当前用于解析的 loadout |
| `ActiveEquipmentStateId` | 解析出的 active equipment state |
| `EffectiveAbilityLoadoutIds` | 参与合并的 ability loadout |
| `EffectiveAbilityIds` | 最终有效 ability id，去重后稳定排序或稳定 slot 顺序 |
| `EffectiveSlotBindings` | 最终输入 / action slot 绑定 |
| `CombatActionSetId` | 当前 Combat action set |
| `AnimationProfileId` | 当前 animation profile |
| `RequiredResources` | 当前角色生成需要的 ResourceKey 列表 |
| `Diagnostics` | 解析期间产生的 warning / error |

### CharacterPackageResolver

用途：从一组配置解析完整角色包摘要。

输入：

- `CharacterConfig`
- `CharacterAttributeProfileConfig`
- `CharacterBodyProfileConfig`
- `CharacterBodyPartConfig[]`
- `EquipmentSchemaConfig`
- `EquipmentLoadoutConfig`
- `EquipmentStateConfig[]`
- `WeaponConfig[]`
- `AbilityLoadoutConfig[]`
- `CombatActionSetConfig`
- `CharacterPresentationProfileConfig`

输出：

- `CharacterResolvedProfile`
- `CharacterValidationReport`
- `CharacterResourceDependencyReport`

失败原因至少包括：

```text
MissingCharacterConfig
MissingBodyProfile
MissingAttributeProfile
MissingEquipmentSchema
MissingDefaultLoadout
MissingPresentationProfile
InvalidBodyPartSet
InvalidEquipmentLoadout
NoEquipmentStateMatch
AmbiguousEquipmentStateMatch
MissingAbility
MissingCombatAction
MissingAnimationAction
MissingResourceKey
```

### EquipmentStateResolver

用途：从当前装备解析 active equipment state。

输入：

- `EquipmentSchemaConfig`
- 当前 slot -> weapon instance / weapon config
- `WeaponConfig[]`
- `EquipmentStateConfig[]`

输出：

- `EquipmentStateResolveResult`

状态枚举：

```text
Success
NoMatchingState
MultipleMatchingStates
InvalidSlot
MissingRequiredSlot
CategoryNotAllowed
OccupiedSlotConflict
MissingWeaponConfig
MissingEquipmentStateConfig
```

固定规则：

1. 先验证 loadout 是否符合 schema。
2. 只在 `EquipmentSchemaConfig.AllowedStateIds` 中寻找候选 state。
3. 一个 state 的所有 `MatchRules` 必须全部满足才算匹配。
4. 命中的 state 按 `Priority` 降序选择。
5. 如果最高 `Priority` 有多个 state，不允许随机选择，返回 `MultipleMatchingStates`。
6. 如果无匹配，必须有明确 fallback state，例如 empty / unarmed / invalid；没有 fallback 则返回 `NoMatchingState`，Spawn 失败。

### AbilityGrantResolver

用途：合并角色基础能力、武器能力、装备状态能力和运行时临时能力。

应用顺序：

1. Character base ability loadout。
2. 当前装备的 weapon grants。
3. Active equipment state grants。
4. Runtime temporary grants。
5. Runtime disable / remove rules。

冲突规则：

- 同一个 `AbilityId` 重复授予只保留一份，但 diagnostics 记录来源列表。
- `RemoveAbilityIds` 只移除较早层级授予的能力，不移除更高优先级 runtime grant。
- 同一 `SlotId` 有多个 ability binding 时，第一版应返回 conflict diagnostic，不静默覆盖。
- 同一 `InputIntentId` 多处绑定时必须输出 diagnostic。

输出：

- `EffectiveAbilityIds`
- `EffectiveSlotBindings`
- `AbilityGrantHandle[]`
- `CharacterDiagnostic[]`

### CombatActionBindingResolver

用途：根据 active equipment state 和 ability/action binding 找到当前 Combat action set。

规则：

- `CombatActionSetConfig` 只做 action key -> Combat action / trace / animation key 绑定。
- Combat action timeline、hit window、cancel window 的权威数据来自 Combat action 定义，不在 Character 表中复制。
- 如果后续允许 override，字段必须显式命名为 `*Override`，并进入 hash / diagnostics。

### BodyPartHitZoneResolver

用途：从 Combat hit zone 解析身体部位、伤害倍率、反应组和姿态倍率。

输入：

- `CharacterBodyProfileConfig`
- 当前 body part set
- `HitZoneId`

输出：

- `PartId`
- `DamageMultiplier`
- `ReactionGroupId`
- `ImpulseScale`
- `StaggerScale`
- `PostureDamageScale`
- `VfxProfileId`
- `SfxProfileId`
- `Diagnostics`

失败状态：

```text
UnknownHitZone
UnmappedHitZone
AmbiguousHitZone
MissingBodyPart
```

解析顺序：

1. 优先查 `CharacterBodyProfileConfig.HitZoneBindings`。
2. 如果没有显式 binding，再查 `CharacterBodyPartConfig.HitZoneId`。
3. 多个候选按 `Priority` 选择；同优先级冲突返回 `AmbiguousHitZone`。
4. 未映射 hit zone 不得静默当作 torso，必须返回 diagnostic；是否降级到全身受击由上层策略决定。

### ResourceDependencyResolver

用途：收集角色生成、装备状态和表现所需资源。

输入：

- `CharacterPresentationProfileConfig`
- 当前装备的 `WeaponConfig[]`
- active `EquipmentStateConfig`
- `CombatActionSetConfig`
- animation profile / action keys

输出：

- `RequiredResourceKeys`
- `PreloadGroups`
- `MissingResourceDiagnostics`

Resource Catalog 不完整时，Workstation 可以输出 warning，但不能宣称资源链路完成。

### SpawnPlanResolver

用途：把 `SpawnProfileConfig` + `CharacterSpawnRequest` 解析成可执行 spawn plan。

输出至少包含：

- resolved character id
- resolved team id
- resolved controller kind / profile
- resolved equipment loadout
- resolved pose
- `CharacterResolvedProfile`
- diagnostics

### SaveStateBindingResolver

用途：恢复时把 SaveState 中的角色实例、装备实例和 config 引用重新绑定到当前配置。

规则：

- 推荐保存当前装备和 weapon instance state，restore 时重算 `ActiveEquipmentStateId`。
- SaveState 中保存的 active state id 只作为诊断期望值，不作为唯一权威恢复来源。
- 如果当前配置解析出的 active state 与保存值不同，返回 migration / mismatch diagnostic。

## 运行时状态契约

角色生成后，运行时状态才是权威。尽量把状态放到已有 Runtime / Gameplay / Combat / Character Control 存储里，Character 只持有聚合和映射状态。

### CharacterRuntimeBinding

| 字段 | 说明 |
| --- | --- |
| `CharacterInstanceId` | 稳定运行时角色实例 ID |
| `CharacterConfigId` | 来源 `CharacterConfig` ID |
| `GameplayEntityId` | Gameplay component runtime entity ID |
| `CombatEntityId` | Combat entity ID |
| `CombatBodyId` | Combat body ID |
| `AnimationActorId` | 表现 actor ID |
| `ViewInstanceId` | 可选 Unity view / runtime view ID |

### CharacterEquipmentRuntimeState

| 字段 | 说明 |
| --- | --- |
| `EquippedWeaponsBySlot` | slot id -> weapon instance id |
| `ActiveEquipmentStateId` | 已解析出的 `EquipmentStateConfig` ID |
| `AppliedGrantHandles` | 已应用的 ability / modifier grant handle |
| `DirtyVersion` | 装备变化时递增 |

### WeaponInstanceState

| 字段 | 说明 |
| --- | --- |
| `WeaponInstanceId` | 运行时武器实例 ID |
| `WeaponConfigId` | 来源 `WeaponConfig` ID |
| `Durability` | 当前耐久，如果启用 |
| `Ammo` | 当前弹药 / 充能，如果启用 |
| `EnhancementLevel` | 运行时或养成强化等级 |
| `RuntimeModifiers` | 临时 modifier 或词缀 |

### Gameplay Runtime State

以下状态保留在 Gameplay component runtime：

- 当前属性，例如 `hp.current`、`stamina.current`、`posture.current`、`guard.current`、`armor.integrity`。
- 当前拥有的能力、冷却、禁用状态、charge 和临时授予能力。
- 当前 Buff、Modifier、来源、剩余时间和层数。
- 生命周期、队伍、tag、status。

### Combat Runtime State

以下状态保留在 Combat runtime：

- 位置、速度、grounded 状态、碰撞 flags。
- Combat body / collider 状态。
- 当前 action instance、local frame、action phase、取消窗口状态。
- hit-once 数据和 trace runtime 数据。

### Character Control Runtime State

以下状态保留在 Character Control：

- Locomotion / Action / Reaction / Disabled 状态。
- 控制锁。
- 最近命令和命令来源。
- pressure reaction window。

### BodyPartRuntimeState

v1 可以不立即实现，但契约要允许后续扩展：

| 字段 | 说明 |
| --- | --- |
| `PartId` | 来源 `CharacterBodyPartConfig` 的 part id |
| `DamageState` | Normal / Damaged / Broken / Disabled |
| `ArmorIntegrity` | 如果启用部位护甲，保存部位护甲状态 |
| `TemporaryReactionOverride` | 运行时临时反应覆盖 |
| `DisabledUntilFrame` | 部位临时失效到哪一帧 |

### PresentationRuntimeState

表现状态应尽量可以从 config + runtime authority 重建：

- 已加载模型 / 资源 handle。
- 武器 view 实例和 socket attachment。
- 当前动画请求 / diagnostics 状态。
- UI / Debug 绑定 handle。

表现状态不能反向驱动 Gameplay 或 Combat 权威状态。

## SaveState 边界

SaveState 保存运行时状态，不保存配置表。

应该保存：

- 恢复映射所需的 `CharacterRuntimeBinding` 标识。
- 角色 config ID。
- 当前属性、status、Buff、Modifier、冷却。
- 当前装备 slot -> weapon instance id。
- 武器实例状态。
- active equipment state ID，或足够重新计算该状态的装备数据。
- 支持时保存 Combat motion / action state。
- 可选 body-part runtime damage state。

不应该保存：

- 完整复制的 config 行。
- Unity object 引用。
- AnimationClip、prefab 引用或已加载 resource handle。
- Debug UI 状态。
- transient command source 对象。

## 数据使用场景验证

### 场景 1：生成一个剑盾玩家角色

输入：

```text
SpawnProfileConfig
  CharacterId = char.iron_vanguard
  ControllerKind = LocalInput
  EquipmentLoadoutId = equip_loadout.sword_shield
```

解析：

```text
CharacterConfig
  -> AttributeProfile 初始化 GameplayAttributeSetComponent
  -> BodyProfile 创建 Combat body / capsule / sockets
  -> EquipmentLoadout 为 mainHand / offHand 创建武器实例
  -> EquipmentStateResolver 匹配 sword_shield
  -> Ability grants = base loadout + weapon grants + equipment state grants
  -> CombatActionSet = actions.sword_shield
  -> PresentationProfile + weapon ModelKey 实例化角色和武器表现
```

运行时结果：

```text
创建 CharacterRuntimeBinding
Gameplay entity 持有当前 HP / stamina / posture / abilities
Combat entity 持有 position / action / motion
Equipment runtime state 记录 mainHand / offHand weapon instances 和 active state
Character Control 读取 LocalInput command
```

没有任何 config 行被修改。

### 场景 2：头部受击

输入：

```text
Combat hit result -> HitZoneId = hz.humanoid.head
Base damage = 20
```

解析：

```text
HitZoneId -> CharacterBodyPartConfig(head)
DamageMultiplier = 1.5
ReactionGroupId = react.upper_heavy
PostureDamageScale = 1.2
```

运行时变化：

```text
hp.current: 120 -> 90
posture.current: 100 -> 76
CharacterControl 进入 Reaction
Animation request 使用 reaction group
可选 BodyPartRuntimeState(head) 记录损伤
```

配置保持不变。

### 场景 3：卸下盾牌

输入：

```text
Unequip offHand
```

运行时变化：

```text
CharacterEquipmentRuntimeState.EquippedWeaponsBySlot[offHand] = empty
EquipmentStateResolver 重新计算 active state
sword_shield -> one_hand_blade
```

效果：

```text
移除 shield_block 和 shield_bash grant
移除盾牌提供的 defense / guard modifier
CombatActionSet 切到 one_hand_blade
animation profile 切到 one_hand_blade
释放 shield view instance / resource handle
SaveState 记录 offHand empty
```

`EquipmentLoadoutConfig` 和 `WeaponConfig` 不变。

### 场景 4：同一个角色作为敌人生成

输入：

```text
SpawnProfileConfig
  CharacterId = char.iron_vanguard
  TeamId = 2
  ControllerKind = RuntimeAiPlanner
  EquipmentLoadoutId = equip_loadout.sword_only
```

结果：

```text
同一个 CharacterConfig
不同 team
不同 controller
不同初始 equipment runtime state
不同 active equipment state
```

Enemy 不是另一套实体模型，而是同一套 Character 契约的不同运行时配置。

### 场景 5：非人形奇幻生物

Drake body config：

```text
BodyKind = Skeletal
PartSetId = parts.drake.small
Parts = head, torso, left_wing, right_wing, tail
```

尾巴受击：

```text
HitZoneId = hz.drake.tail
-> BodyPart tail
-> DamageMultiplier = 0.8
-> ReactionGroupId = react.tail_hit
```

不需要人形专属字段。

### 场景 6：简单几何体史莱姆

Slime body config：

```text
BodyKind = Primitive
PartSetId = parts.slime.sphere
Parts = core, surface
```

核心受击：

```text
PartKind = Primitive
LocatorId = primitive.sphere.center
DamageMultiplier = 1.25
ReactionGroupId = react.squash
```

同一张身体部位表支持无骨骼 Actor。

## Character Workstation 要求

编辑器展示的是角色包视图，但导出的是稳定配置表。第一版 Workstation 是只读 / 轻编辑 MVP，不做完整 3D 预览、动画预览、武器挂点编辑或运行时生成。

Workstation 面板：

- 身份和基础引用。
- 初始属性 profile。
- 身体 profile、body part set 和 hit zone binding。
- 装备 schema、当前预览 loadout、解析出的 equipment state。
- 武器定义和槽位兼容性。
- Effective ability list 和 slot binding。
- Combat action set 绑定预览。
- 表现资源和 animation profile。
- Runtime-state 预览：spawn、hit、equip、unequip 后哪些运行时状态会变化。
- 校验报告和 resolver diagnostics。

Workstation 至少输出 4 个稳定产物：

| 输出 | 用途 |
| --- | --- |
| `CharacterResolvedProfile` | 当前角色包在某个 loadout 下的 effective profile |
| `CharacterValidationReport` | 引用、结构、resolver 失败和冲突报告 |
| `CharacterResourceDependencyReport` | 当前角色生成 / 装备 / 表现需要的资源 |
| `CharacterDebugContext` | 给 Debug UI、命令行报告和 Development Agent 使用的可读上下文 |

校验项：

- 所有 config 引用存在。
- Body profile 至少有一个有效部位。
- 同一 `PartSetId` 下 `PartId` 唯一。
- `ParentPartId` 必须存在于同一 `PartSetId`。
- 每个 hit zone 都能映射到 body part，或明确标记为不映射。
- Equipment loadout 符合 schema。
- 常见预览 loadout 能解析出唯一 winning equipment state。
- 武器 slot / category / socket 引用合法。
- 授予的 ability 存在。
- Combat action 和 animation action key 可解析。
- ResourceKey 存在于 catalog，或明确标记为 external / mod-provided。

`CharacterValidationReport` 的 issue 结构应与 `CharacterDiagnostic` 对齐，至少包含 severity、code、source table、source id、source stable id、field 和 message。

## 前置功能优先级

后续任务如果命中这些前置缺口，应先补前置能力，再推进角色运行时。

| 优先级 | 前置能力 | 不满足时的处理 |
| --- | --- | --- |
| P0 | 12 张表 typed DTO / schema / typed id / 嵌套结构 | 不做 Workstation 导出 |
| P0 | `CharacterPackageResolver` / `EquipmentStateResolver` / `AbilityGrantResolver` 契约 | 不做 runtime spawning，只做数据表和校验 |
| P0 | 角色配置 validator 和稳定 diagnostics code | 不接入运行时，不生成可玩入口 |
| P0 | EquipmentStateResolver 最小实现 | 不实现装备影响能力 / 动作 / 动画 |
| P0 | Weapon instance 生命周期最小规则 | 不支持装备 / 卸下 runtime state |
| P1 | Character config JSON importer / exporter | 只允许代码内 sample data，不交付 JSON 数据链路 |
| P1 | BodyPart 与 Combat hit zone adapter | 不实现部位受击，只做全身受击 |
| P1 | Attribute stable id registry | 不接入最终 hash / SaveState，只做预览 |
| P1 | Resource Catalog 引用校验 | 资源链路只能作为 warning |
| P2 | Animation profile 深度校验 / 预览 | 只校验 action key 字符串，不做动画预览 |
| P2 | BodyPartRuntimeState | 不实现断肢、部位破坏、部位护甲运行时 |

第一阶段真正的完成线应是：数据表、纯解析器、校验器和样例数据可以在无 Unity 场景的情况下输出稳定 effective profile 报告。只有这个前置完成后，才进入角色 runtime spawning 和可玩场景。

## 建议拆分 Issue

### Issue A：Character Config DTO + Schema + Typed Ids

目标：只做 12 张表、嵌套结构和强类型 ID，不做 resolver。

验收：

- 12 张表 DTO 完整。
- 所有 ID 字段有 typed wrapper 或 schema reference。
- ConfigSchema 能导出字段、引用、enum、displayName。
- `AttributeEntry` 使用 `InitialValue`，不使用 `CurrentValue`。
- 没有 `UnityEngine.Object` 字段。

### Issue B：Character Pure Resolvers + Diagnostics

目标：实现 noEngine 纯 resolver。

范围：

- `CharacterPackageResolver`
- `EquipmentStateResolver`
- `AbilityGrantResolver`
- `CombatActionBindingResolver`
- `BodyPartHitZoneResolver`
- `ResourceDependencyResolver`
- `SpawnPlanResolver`
- `SaveStateBindingResolver` 契约或最小诊断实现

验收：

- `Iron Vanguard` 示例能解析为唯一 active equipment state。
- 剑盾、单剑、空手都能解析。
- 多 state 同 priority 返回 tie diagnostic。
- 缺失 ability / combat action / resource key 有稳定 issue code。
- resolver 不读取 Unity 对象，不写 runtime world。

### Issue C：Character Workstation Readonly MVP

目标：只读工作台或报告视图，不做 runtime spawn。

展示：

- `CharacterConfig`
- body parts 和 hit zone binding
- equipment loadout
- winning equipment state
- effective abilities
- combat action set
- animation profile
- resource dependencies
- validation issues

验收：

- 可选择 `Iron Vanguard`。
- 可切换 loadout 预览 resolver 输出。
- 能复制 `CharacterDebugContext`。
- 不写 runtime state。

### Issue D：Runtime Spawn Vertical Slice

目标：真正生成角色实例。该 Issue 只有在 A-C 通过后才进入。

流程：

```text
CharacterSpawnRequest
-> CharacterResolvedProfile
-> Gameplay entity
-> Combat entity/body
-> CharacterRuntimeBinding
-> Equipment runtime state
-> Presentation resource preload
-> Character Control source
```

验收：

- 生成剑盾玩家角色。
- 生成同 `CharacterConfig` 的敌方单剑角色。
- SaveState roundtrip 恢复 runtime binding。
- Debug source 能显示 resolved profile 和 runtime ids。

## 00 验收标准

- 所有配置表字段都有类型、引用目标、是否可空和默认值语义。
- 所有跨表引用都能进入 ConfigSourceIndex 或等价 validator 校验。
- 所有运行时状态字段都明确归属 Runtime / Gameplay / Combat / Character Control / Presentation。
- SaveState 保存项和不保存项有明确列表。
- EquipmentStateResolver 有确定性匹配规则和失败语义。
- AbilityGrantResolver 有确定性合并顺序和 slot 冲突规则。
- BodyPartHitZoneResolver 有确定性映射和未映射处理。
- ResourceDependencyResolver 能列出角色生成需要的 ResourceKey。
- Workstation 能输出稳定 `CharacterValidationReport`。
- `Iron Vanguard`、`Drake`、`Slime` 三个样例都能通过配置校验。

如果同一任务包含 runtime spawning，则该任务升级为应用层可玩实现，需要 Unity scene 或 runner 验证。
