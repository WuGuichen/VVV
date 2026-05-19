using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.CharacterApplication
{
    public static class CharacterApplicationConfigSchemas
    {
        public const int CharacterConfigIdMin = 710000;
        public const int CharacterConfigIdMax = 719999;
        public const int AttributeProfileIdMin = 720000;
        public const int AttributeProfileIdMax = 729999;
        public const int BodyProfileIdMin = 730000;
        public const int BodyProfileIdMax = 739999;
        public const int BodyPartConfigIdMin = 740000;
        public const int BodyPartConfigIdMax = 749999;
        public const int EquipmentSchemaIdMin = 750000;
        public const int EquipmentSchemaIdMax = 759999;
        public const int EquipmentLoadoutIdMin = 760000;
        public const int EquipmentLoadoutIdMax = 769999;
        public const int EquipmentStateIdMin = 770000;
        public const int EquipmentStateIdMax = 779999;
        public const int WeaponConfigIdMin = 780000;
        public const int WeaponConfigIdMax = 789999;
        public const int AbilityLoadoutIdMin = 790000;
        public const int AbilityLoadoutIdMax = 799999;
        public const int CombatActionSetIdMin = 800000;
        public const int CombatActionSetIdMax = 809999;
        public const int PresentationProfileIdMin = 810000;
        public const int PresentationProfileIdMax = 819999;
        public const int SpawnProfileIdMin = 820000;
        public const int SpawnProfileIdMax = 829999;

        public static IReadOnlyList<ConfigSchema> CreateAll()
        {
            return new ConfigSchema[]
            {
                CharacterConfig.CreateSchema(),
                CharacterAttributeProfileConfig.CreateSchema(),
                CharacterBodyProfileConfig.CreateSchema(),
                CharacterBodyPartConfig.CreateSchema(),
                EquipmentSchemaConfig.CreateSchema(),
                EquipmentLoadoutConfig.CreateSchema(),
                EquipmentStateConfig.CreateSchema(),
                WeaponConfig.CreateSchema(),
                AbilityLoadoutConfig.CreateSchema(),
                CombatActionSetConfig.CreateSchema(),
                CharacterPresentationProfileConfig.CreateSchema(),
                SpawnProfileConfig.CreateSchema()
            };
        }
    }

    internal static class CharacterApplicationSchemaField
    {
        public static ConfigField Id(string displayName)
        {
            return new ConfigField("Id", ConfigFieldType.Integer, displayName: displayName, required: true);
        }

        public static ConfigField StableId()
        {
            return new ConfigField(
                "StableId",
                ConfigFieldType.String,
                displayName: "稳定 ID",
                description: "长期稳定 ID，用于 SaveState、Mod、调试报告和跨版本迁移。",
                required: true);
        }

        public static ConfigField TypedReference(
            string fieldName,
            Type valueType,
            string displayName,
            string targetTableName,
            bool required = true,
            string description = "")
        {
            return new ConfigField(
                fieldName,
                ConfigFieldType.ConfigReference,
                displayName: displayName,
                description: description,
                required: required,
                valueType: valueType,
                referenceRule: new ConfigReferenceRule(
                    fieldName,
                    targetTableName,
                    ConfigStructureKind.Table,
                    required: required));
        }
    }

    public sealed partial class CharacterConfig
    {
        public static ConfigSchema<CharacterConfig> CreateSchema()
        {
            var schema = new ConfigSchema<CharacterConfig>(
                TableName,
                displayName: "Character Config",
                description: "角色应用层聚合配置入口，只保存静态引用、默认值和规则。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.CharacterConfigIdMin, CharacterApplicationConfigSchemas.CharacterConfigIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("角色配置编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("NameText", ConfigFieldType.LocalizedText, displayName: "名称文本", required: true))
                .AddField(new ConfigField("DescriptionText", ConfigFieldType.LocalizedText, displayName: "描述文本"))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(AttributeProfileId), typeof(CharacterAttributeProfileId), "属性配置", CharacterAttributeProfileConfig.TableName))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(BodyProfileId), typeof(CharacterBodyProfileId), "身体配置", CharacterBodyProfileConfig.TableName))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(EquipmentSchemaId), typeof(EquipmentSchemaId), "装备槽位 Schema", EquipmentSchemaConfig.TableName))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(DefaultLoadoutId), typeof(EquipmentLoadoutId), "默认装备方案", EquipmentLoadoutConfig.TableName))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(BaseAbilityLoadoutId), typeof(AbilityLoadoutId), "基础能力方案", AbilityLoadoutConfig.TableName, required: false))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(PresentationProfileId), typeof(CharacterPresentationProfileId), "表现配置", CharacterPresentationProfileConfig.TableName))
                .AddField(new ConfigField("DefaultControllerKind", ConfigFieldType.Enum, displayName: "默认控制器类型", required: true, enumId: "character.ControllerKind"))
                .AddField(new ConfigField("DefaultControllerProfileId", ConfigFieldType.String, displayName: "默认控制器配置", description: "例如 Runtime AI Planner profile、脚本控制 profile 或输入 profile 的 StableId。"))
                .AddField(new ConfigField("DefaultSpawnTags", ConfigFieldType.Custom, displayName: "默认生成标签", valueType: typeof(string[])))
                .RequireLocale(LocaleId.ZhCN)
                .RequireLocale(LocaleId.EnUS);

            return schema;
        }
    }

    public sealed partial class CharacterAttributeProfileConfig
    {
        public static ConfigSchema<CharacterAttributeProfileConfig> CreateSchema()
        {
            var schema = new ConfigSchema<CharacterAttributeProfileConfig>(
                TableName,
                displayName: "Character Attribute Profile",
                description: "角色属性初始值配置。运行时当前值不保存在本表。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.AttributeProfileIdMin, CharacterApplicationConfigSchemas.AttributeProfileIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("属性配置编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("Attributes", ConfigFieldType.Custom, displayName: "属性列表", description: "CharacterAttributeEntry[]，包含 AttributeId、BaseValue、InitialValue 和 clamp。", required: true, valueType: typeof(CharacterAttributeEntry[])));

            return schema;
        }
    }

    public sealed partial class CharacterBodyProfileConfig
    {
        public static ConfigSchema<CharacterBodyProfileConfig> CreateSchema()
        {
            var schema = new ConfigSchema<CharacterBodyProfileConfig>(
                TableName,
                displayName: "Character Body Profile",
                description: "身体模板配置，支持人形、奇幻生物和简单几何体。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.BodyProfileIdMin, CharacterApplicationConfigSchemas.BodyProfileIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("身体配置编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("BodyKind", ConfigFieldType.Enum, displayName: "身体类型", required: true, enumId: "character.BodyKind"))
                .AddField(new ConfigField("PartSetId", ConfigFieldType.String, displayName: "部位集合 ID", required: true))
                .AddField(new ConfigField("DefaultMotionProfileId", ConfigFieldType.String, displayName: "默认运动配置", description: "运动胶囊、速度等运行时运动 profile 的 StableId。"))
                .AddField(new ConfigField("DefaultPhysicsProfileId", ConfigFieldType.String, displayName: "默认物理配置", description: "质量、碰撞代理等物理 profile 的 StableId。"))
                .AddField(new ConfigField("Sockets", ConfigFieldType.Custom, displayName: "Socket 绑定", valueType: typeof(CharacterSocketEntry[])))
                .AddField(new ConfigField("HitZoneBindings", ConfigFieldType.Custom, displayName: "HitZone 到部位绑定", valueType: typeof(CharacterHitZoneBindingEntry[])));

            return schema;
        }
    }

    public sealed partial class CharacterBodyPartConfig
    {
        public static ConfigSchema<CharacterBodyPartConfig> CreateSchema()
        {
            var schema = new ConfigSchema<CharacterBodyPartConfig>(
                TableName,
                displayName: "Character Body Part Config",
                description: "身体部位配置，用于命中部位、弱点、受击表现和姿态伤害倍率。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.BodyPartConfigIdMin, CharacterApplicationConfigSchemas.BodyPartConfigIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("身体部位配置编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("PartSetId", ConfigFieldType.String, displayName: "部位集合 ID", required: true))
                .AddField(new ConfigField("PartId", ConfigFieldType.String, displayName: "部位 ID", required: true))
                .AddField(new ConfigField("ParentPartId", ConfigFieldType.String, displayName: "父部位 ID"))
                .AddField(new ConfigField("PartKind", ConfigFieldType.Enum, displayName: "部位类型", required: true, enumId: "character.BodyPartKind"))
                .AddField(new ConfigField("LocatorId", ConfigFieldType.String, displayName: "Locator ID"))
                .AddField(new ConfigField("HitZoneId", ConfigFieldType.String, displayName: "简单 HitZone ID", description: "简单一对一映射；复杂映射使用 BodyProfile.HitZoneBindings。"))
                .AddField(new ConfigField("ReactionGroupId", ConfigFieldType.String, displayName: "受击反应组"))
                .AddField(new ConfigField("DamageMultiplier", ConfigFieldType.Float, displayName: "伤害倍率", required: true))
                .AddField(new ConfigField("ImpulseScale", ConfigFieldType.Float, displayName: "冲量倍率", required: true))
                .AddField(new ConfigField("StaggerScale", ConfigFieldType.Float, displayName: "硬直倍率", required: true))
                .AddField(new ConfigField("PostureDamageScale", ConfigFieldType.Float, displayName: "姿态伤害倍率", required: true))
                .AddField(new ConfigField("IsCritical", ConfigFieldType.Boolean, displayName: "关键部位"));

            return schema;
        }
    }

    public sealed partial class EquipmentSchemaConfig
    {
        public static ConfigSchema<EquipmentSchemaConfig> CreateSchema()
        {
            var schema = new ConfigSchema<EquipmentSchemaConfig>(
                TableName,
                displayName: "Equipment Schema Config",
                description: "角色可装备部位、互斥组和允许装备状态范围。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.EquipmentSchemaIdMin, CharacterApplicationConfigSchemas.EquipmentSchemaIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("装备 Schema 编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("Slots", ConfigFieldType.Custom, displayName: "装备槽位", required: true, valueType: typeof(EquipmentSlotEntry[])))
                .AddField(new ConfigField("ExclusiveGroups", ConfigFieldType.Custom, displayName: "互斥组", valueType: typeof(EquipmentExclusiveGroupEntry[])))
                .AddField(new ConfigField("AllowedStateIds", ConfigFieldType.Custom, displayName: "允许的装备状态", description: "EquipmentStateId[]；数组引用由后续 resolver/validator 校验。", valueType: typeof(EquipmentStateId[])));

            return schema;
        }
    }

    public sealed partial class EquipmentLoadoutConfig
    {
        public static ConfigSchema<EquipmentLoadoutConfig> CreateSchema()
        {
            var schema = new ConfigSchema<EquipmentLoadoutConfig>(
                TableName,
                displayName: "Equipment Loadout Config",
                description: "角色默认或预设装备方案，武器按 slot 装配。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.EquipmentLoadoutIdMin, CharacterApplicationConfigSchemas.EquipmentLoadoutIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("装备方案编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(EquipmentSchemaId), typeof(EquipmentSchemaId), "装备 Schema", EquipmentSchemaConfig.TableName))
                .AddField(new ConfigField("Slots", ConfigFieldType.Custom, displayName: "槽位装配", required: true, valueType: typeof(EquipmentLoadoutSlotEntry[])));

            return schema;
        }
    }

    public sealed partial class EquipmentStateConfig
    {
        public static ConfigSchema<EquipmentStateConfig> CreateSchema()
        {
            var schema = new ConfigSchema<EquipmentStateConfig>(
                TableName,
                displayName: "Equipment State Config",
                description: "由当前装备解析出的派生装备状态，决定能力、动作集和动画 profile。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.EquipmentStateIdMin, CharacterApplicationConfigSchemas.EquipmentStateIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("装备状态编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(EquipmentSchemaId), typeof(EquipmentSchemaId), "装备 Schema", EquipmentSchemaConfig.TableName))
                .AddField(new ConfigField("Priority", ConfigFieldType.Integer, displayName: "匹配优先级", required: true))
                .AddField(new ConfigField("MatchRules", ConfigFieldType.Custom, displayName: "匹配规则", description: "EquipmentMatchRule[]；规则之间为 AND。", required: true, valueType: typeof(EquipmentMatchRule[])))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(GrantedAbilityLoadoutId), typeof(AbilityLoadoutId), "状态授予能力", AbilityLoadoutConfig.TableName, required: false))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(CombatActionSetId), typeof(CombatActionSetId), "动作绑定集", CombatActionSetConfig.TableName))
                .AddField(new ConfigField("AnimationProfileId", ConfigFieldType.String, displayName: "动画 Profile", required: true))
                .AddField(new ConfigField("Tags", ConfigFieldType.Custom, displayName: "状态标签", valueType: typeof(string[])));

            return schema;
        }
    }

    public sealed partial class WeaponConfig
    {
        public static ConfigSchema<WeaponConfig> CreateSchema()
        {
            var schema = new ConfigSchema<WeaponConfig>(
                TableName,
                displayName: "Weapon Config",
                description: "可装备武器定义；武器不与角色强绑定，通过 Loadout 和 EquipmentState 生效。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.WeaponConfigIdMin, CharacterApplicationConfigSchemas.WeaponConfigIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("武器配置编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("Category", ConfigFieldType.Enum, displayName: "武器类别", required: true, enumId: "character.WeaponCategory"))
                .AddField(new ConfigField("Tags", ConfigFieldType.Custom, displayName: "武器标签", valueType: typeof(string[])))
                .AddField(new ConfigField("OccupiesSlots", ConfigFieldType.Custom, displayName: "占用槽位", valueType: typeof(string[])))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(GrantedAbilityLoadoutId), typeof(AbilityLoadoutId), "武器授予能力", AbilityLoadoutConfig.TableName, required: false))
                .AddField(new ConfigField("WeaponPresentationProfileId", ConfigFieldType.String, displayName: "武器表现 Profile"))
                .AddField(new ConfigField("WeaponCombatProfileId", ConfigFieldType.String, displayName: "武器战斗 Profile"))
                .AddField(new ConfigField("WeaponResourceProfileId", ConfigFieldType.String, displayName: "武器资源 Profile"))
                .AddField(new ConfigField("ResourceKeys", ConfigFieldType.Custom, displayName: "资源引用", valueType: typeof(CharacterResourceKeyEntry[])))
                .AddField(new ConfigField("TraceBindings", ConfigFieldType.Custom, displayName: "轨迹绑定", valueType: typeof(CharacterTraceBindingEntry[])))
                .AddField(new ConfigField("BaseModifiers", ConfigFieldType.Custom, displayName: "基础 modifier", valueType: typeof(CharacterModifierGrantEntry[])));

            return schema;
        }
    }

    public sealed partial class AbilityLoadoutConfig
    {
        public static ConfigSchema<AbilityLoadoutConfig> CreateSchema()
        {
            var schema = new ConfigSchema<AbilityLoadoutConfig>(
                TableName,
                displayName: "Ability Loadout Config",
                description: "能力授予与移除集合；具体合并顺序由 AbilityGrantResolver 定义。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.AbilityLoadoutIdMin, CharacterApplicationConfigSchemas.AbilityLoadoutIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("能力方案编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("AbilityIds", ConfigFieldType.Custom, displayName: "授予能力", valueType: typeof(CharacterAbilityId[])))
                .AddField(new ConfigField("RemoveAbilityIds", ConfigFieldType.Custom, displayName: "移除能力", valueType: typeof(CharacterAbilityId[])))
                .AddField(new ConfigField("SlotBindings", ConfigFieldType.Custom, displayName: "槽位绑定", valueType: typeof(AbilitySlotBinding[])));

            return schema;
        }
    }

    public sealed partial class CombatActionSetConfig
    {
        public static ConfigSchema<CombatActionSetConfig> CreateSchema()
        {
            var schema = new ConfigSchema<CombatActionSetConfig>(
                TableName,
                displayName: "Combat Action Set Config",
                description: "角色应用层动作绑定集，只映射 action key、CombatActionId、trace override 和 animation action key。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.CombatActionSetIdMin, CharacterApplicationConfigSchemas.CombatActionSetIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("动作绑定集编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("Actions", ConfigFieldType.Custom, displayName: "动作绑定", required: true, valueType: typeof(CombatActionEntry[])));

            return schema;
        }
    }

    public sealed partial class CharacterPresentationProfileConfig
    {
        public static ConfigSchema<CharacterPresentationProfileConfig> CreateSchema()
        {
            var schema = new ConfigSchema<CharacterPresentationProfileConfig>(
                TableName,
                displayName: "Character Presentation Profile",
                description: "角色模型、动画 profile 和表现资源引用。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.PresentationProfileIdMin, CharacterApplicationConfigSchemas.PresentationProfileIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("表现配置编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(new ConfigField("DefaultAnimationProfileId", ConfigFieldType.String, displayName: "默认动画 Profile", required: true))
                .AddField(new ConfigField("ResourceKeys", ConfigFieldType.Custom, displayName: "表现资源", valueType: typeof(CharacterResourceKeyEntry[])))
                .AddField(new ConfigField("PresentationTags", ConfigFieldType.Custom, displayName: "表现标签", valueType: typeof(string[])));

            return schema;
        }
    }

    public sealed partial class SpawnProfileConfig
    {
        public static ConfigSchema<SpawnProfileConfig> CreateSchema()
        {
            var schema = new ConfigSchema<SpawnProfileConfig>(
                TableName,
                displayName: "Spawn Profile Config",
                description: "可复用生成预设；一次性覆盖由 CharacterSpawnRequest 表达。",
                idRange: new ConfigIdRange(CharacterApplicationConfigSchemas.SpawnProfileIdMin, CharacterApplicationConfigSchemas.SpawnProfileIdMax));

            schema
                .AddField(CharacterApplicationSchemaField.Id("生成预设编号"))
                .AddField(CharacterApplicationSchemaField.StableId())
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(CharacterId), typeof(CharacterConfigId), "角色配置", CharacterConfig.TableName))
                .AddField(new ConfigField("TeamId", ConfigFieldType.String, displayName: "队伍 ID", required: true))
                .AddField(new ConfigField("ControllerKind", ConfigFieldType.Enum, displayName: "控制器类型", required: true, enumId: "character.ControllerKind"))
                .AddField(CharacterApplicationSchemaField.TypedReference(nameof(EquipmentLoadoutId), typeof(EquipmentLoadoutId), "装备方案覆盖", EquipmentLoadoutConfig.TableName, required: false))
                .AddField(new ConfigField("SpawnPose", ConfigFieldType.Custom, displayName: "生成姿态", valueType: typeof(CharacterPoseEntry)))
                .AddField(new ConfigField("RuntimeAiPlannerProfileId", ConfigFieldType.String, displayName: "Runtime AI Planner Profile"))
                .AddField(new ConfigField("DebugName", ConfigFieldType.String, displayName: "调试名称"));

            return schema;
        }
    }
}
