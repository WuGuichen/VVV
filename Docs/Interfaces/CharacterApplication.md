# Character Application 接口

> 状态：#215 完成第一阶段配置 DTO / Schema / typed id 契约。本文只记录已经落地的静态配置接口，不包含 resolver、工作台 UI 或 Runtime Spawn 实现。

## 职责

Character Application 是角色应用层的数据聚合契约。它不让 Runtime / Gameplay / Combat / Resources / Animation / CharacterControl 反向依赖角色概念，而是在应用层用配置引用把这些模块编排起来。

当前程序集：

- `MxFramework.Character.Application`
- 路径：`Assets/Scripts/MxFramework/Character.Application/`
- 依赖：`MxFramework.Config`
- `noEngineReferences=true`

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

## ID 规则

- 每张表都有独立 typed id，例如 `CharacterConfigId`、`EquipmentStateId`、`WeaponConfigId`。
- `IConfigData.Id` 仍返回 `int`，用于兼容现有 `ConfigTable<T>`。
- 跨表字段使用 typed id，并通过 `ConfigReferenceRule` 暴露目标 schema。
- `StableId` 用于 SaveState、Mod、调试报告和跨版本迁移。

## 使用约定

- 配置只保存初始值、规则和引用关系，不保存运行时当前 HP、冷却、Buff 实例、装备实例或资源 handle。
- 角色一定有装备状态系统，但不强绑定某一件武器；空手、单武器、多槽位武器都通过 `EquipmentLoadoutConfig` 和后续 resolver 解释。
- `CombatActionSetConfig` 不复制 Combat action timeline 的 duration、hit window 或 cancel window 权威字段。
- 表现资源只保存 `CharacterResourceKeyEntry`，不直接保存 `UnityEngine.Object`、Prefab、`AnimationClip` 或 Material。
- #215 不实现 resolver；装备状态匹配、能力合并、部位 hit zone 解析和资源依赖解析归后续 Issue。

## 测试入口

`Assets/Scripts/MxFramework/Tests/CharacterApplication/`
