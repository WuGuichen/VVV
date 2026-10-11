using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class CharacterResourcePackageSchemas
    {
        public const string ManifestSchemaId = "CharacterPackageManifest";
        public const string ResourceCatalogSchemaId = "CharacterPackageResourceCatalog";
        public const string BodyGeometrySchemaId = "CharacterBodyGeometryProfile";
        public const string BodyPartSchemaId = "CharacterBodyPartAuthoring";
        public const string BodyColliderSchemaId = "CharacterBodyColliderProfile";
        public const string SocketSchemaId = "CharacterSocketProfile";
        public const string CharacterApplicationSchemaId = "CharacterApplicationAuthoringSummary";
        public const string GlobalWeaponLibrarySchemaId = "GlobalWeaponLibrary";
        public const string WeaponDefinitionSchemaId = "SingleWeaponConfiguration";
        public const string AnimationConfigurationSchemaId = "GlobalAnimationConfiguration";
        public const string AbilityConfigurationSchemaId = "GlobalAbilityConfiguration";
        public const string ReactionConfigurationSchemaId = "GlobalReactionConfiguration";
        public const string WeaponConfigurationSchemaId = "CombinedWeaponConfigurationAuthoringSummary";
        public const string WeaponConfigurationBindingSchemaId = "CharacterCombinedWeaponConfigurationBinding";
        public const string WeaponConfigurationOverridesSchemaId = "CharacterCombinedWeaponConfigurationOverrides";
        public const string WeaponActionConfigurationSchemaId = "CombinedWeaponActionConfiguration";
        public const string WeaponConfigurationSlotSchemaId = "CombinedWeaponConfigurationSlotAuthoringSummary";
        public const string WeaponAttachmentSchemaId = "CharacterWeaponSlotProfile";
        public const string WeaponTraceSchemaId = "SingleWeaponTraceProfile";
        public const string ValidationIssueSchemaId = "CharacterAuthoringValidationIssue";
        public const string CompilerResultSchemaId = "CharacterAuthoringCompileResult";

        public static IReadOnlyList<ConfigSchema> CreateAll()
        {
            return new[]
            {
                CreateManifestSchema(),
                CreateResourceCatalogSchema(),
                CreateBodyGeometrySchema(),
                CreateBodyPartSchema(),
                CreateBodyColliderSchema(),
                CreateSocketSchema(),
                CreateCharacterApplicationSchema(),
                CreateGlobalWeaponLibrarySchema(),
                CreateWeaponDefinitionSchema(),
                CreateAnimationConfigurationSchema(),
                CreateAbilityConfigurationSchema(),
                CreateReactionConfigurationSchema(),
                CreateWeaponConfigurationSchema(),
                CreateWeaponConfigurationBindingSchema(),
                CreateWeaponConfigurationOverridesSchema(),
                CreateWeaponActionConfigurationSchema(),
                CreateWeaponConfigurationSlotSchema(),
                CreateWeaponAttachmentSchema(),
                CreateWeaponTraceSchema(),
                CreateValidationIssueSchema(),
                CreateCompilerResultSchema()
            };
        }

        public static IReadOnlyList<EnumDomain> CreateEnumDomains()
        {
            return new[]
            {
                CreatePackageKindEnum(),
                CreateCoordinateAxisEnum(),
                CreateCoordinateHandednessEnum(),
                CreateRotationStorageEnum(),
                CreateResourceTypeEnum(),
                CreateResourceSourceFormatEnum(),
                CreateResourceUsageEnum(),
                CreateImportTargetPathPolicyEnum(),
                CreateConflictActionEnum(),
                CreateBodyKindEnum(),
                CreateBodyPartKindEnum(),
                CreatePoseParentKindEnum(),
                CreateColliderShapeEnum(),
                CreateSocketUsageEnum(),
                CreateSocketHandednessEnum(),
                CreateSocketSideTagEnum(),
                CreateTraceSampleRuleEnum(),
                CreateValidationSeverityEnum(),
                CreateValidationGateEnum(),
                CreateCompilerStatusEnum()
            };
        }

        public static ConfigSchema CreateManifestSchema()
        {
            var schema = CreateSchema(ManifestSchemaId, "角色资源包 Manifest");
            Add(schema, "packageId", "包短 ID", FieldType.String, true, "identity", "身份", description: "包内短 ID，例如 codex_flow_test。");
            Add(schema, "stableId", "稳定 ID", FieldType.String, true, "identity", "身份", description: "跨版本、Mod、导入报告使用的长期稳定 ID。");
            Add(schema, "version", "版本", FieldType.String, true, "identity", "身份");
            Add(schema, "kind", "包类型", FieldType.Enum, true, "identity", "身份", enumId: "characterPackage.kind");
            Add(schema, "packageSchemaVersion", "包 Schema 版本", FieldType.String, true, "version", "版本");
            Add(schema, "sourceSchemaVersion", "Source Schema 版本", FieldType.String, true, "version", "版本");
            Add(schema, "authoringSchemaVersion", "Authoring Schema 版本", FieldType.String, true, "version", "版本");
            Add(schema, "coordinateConvention", "坐标与单位约定", FieldType.String, true, "coordinate", "坐标");
            Add(schema, "hashes", "Hash 占位", FieldType.String, false, "hash", "Hash");
            Add(schema, "dependencies", "依赖", FieldType.String, false, "dependency", "依赖", isList: true);
            return schema;
        }

        public static ConfigSchema CreateResourceCatalogSchema()
        {
            var schema = CreateSchema(ResourceCatalogSchemaId, "角色包资源目录");
            Add(schema, "resourceKey", "ResourceKey", FieldType.String, true, "identity", "身份");
            Add(schema, "localId", "包内 LocalId", FieldType.String, true, "identity", "身份", description: "ResourceKey 生成使用的包内稳定局部 ID，例如 model.body。");
            Add(schema, "stableId", "资源 StableId", FieldType.String, true, "identity", "身份", description: "跨版本、冲突处理和诊断使用的长期稳定资源 ID。");
            Add(schema, "typeId", "资源类型", FieldType.Enum, true, "identity", "身份", enumId: "character.resourceType");
            Add(schema, "variant", "变体", FieldType.String, false, "identity", "身份");
            Add(schema, "usage", "用途", FieldType.Enum, true, "identity", "身份", enumId: "character.resourceUsage");
            Add(schema, "sourceFormat", "源格式", FieldType.Enum, true, "format", "格式", enumId: "character.resourceSourceFormat");
            Add(schema, "packageId", "包 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "relativePath", "包内相对路径", FieldType.AssetPath, false, "path", "路径");
            Add(schema, "hash", "资源 Hash 兼容字段", FieldType.String, false, "hash", "Hash");
            Add(schema, "hashes.contentHash", "内容 Hash", FieldType.String, true, "hash", "Hash");
            Add(schema, "hashes.importHash", "导入 Hash", FieldType.String, false, "hash", "Hash");
            Add(schema, "hashes.dependencyHash", "依赖 Hash", FieldType.String, false, "hash", "Hash");
            Add(schema, "importHints", "导入提示", FieldType.String, false, "import", "导入");
            Add(schema, "importHints.targetPathPolicy", "Unity 目标路径策略", FieldType.Enum, false, "import", "导入", enumId: "character.importTargetPathPolicy");
            Add(schema, "importHints.modelWrapperPose", "模型包裹节点 Pose", FieldType.String, false, "import", "导入", description: "导入模型实例外层 GameObject 的局部位移、旋转和缩放，用于把美术模型对齐到角色或武器槽。");
            Add(schema, "importHints.modelWrapperPose.position", "模型包裹节点位移", FieldType.String, false, "import", "导入");
            Add(schema, "importHints.modelWrapperPose.eulerHint", "模型包裹节点旋转", FieldType.String, false, "import", "导入", description: "Authoring UI 使用角度制编辑；Quaternion 仍是权威旋转存储。");
            Add(schema, "importHints.modelWrapperPose.scale", "模型包裹节点缩放", FieldType.String, false, "import", "导入");
            Add(schema, "dependencies", "资源依赖", FieldType.Reference, false, "dependency", "依赖", referenceSource: ResourceCatalogSchemaId, isList: true);
            Add(schema, "conflictPolicy", "冲突策略", FieldType.String, false, "conflict", "冲突");
            Add(schema, "preview", "预览元数据", FieldType.String, false, "preview", "预览");
            Add(schema, "provenance", "来源元数据", FieldType.String, false, "provenance", "来源");
            return schema;
        }

        public static ConfigSchema CreateBodyGeometrySchema()
        {
            var schema = CreateSchema(BodyGeometrySchemaId, "角色身体几何");
            Add(schema, "profileId", "几何 Profile ID", FieldType.String, true, "identity", "身份");
            Add(schema, "bodyKind", "身体类型", FieldType.Enum, true, "body", "身体", enumId: "character.bodyKind");
            Add(schema, "bodyScale", "模型缩放", FieldType.Float, true, "body", "身体");
            Add(schema, "heightMeters", "身高", FieldType.Float, true, "body", "身体", unit: "m");
            Add(schema, "radiusMeters", "半径", FieldType.Float, true, "body", "身体", unit: "m");
            Add(schema, "massKg", "质量", FieldType.Float, false, "physics", "物理", unit: "kg");
            Add(schema, "defaultCapsule", "默认胶囊", FieldType.String, true, "physics", "物理");
            Add(schema, "defaultPhysicsProfileId", "默认物理 Profile", FieldType.String, false, "physics", "物理");
            Add(schema, "modelRootStableId", "模型根 StableId", FieldType.String, false, "root", "根节点");
            Add(schema, "skeletonRootStableId", "骨骼根 StableId", FieldType.String, false, "root", "根节点");
            Add(schema, "locatorRootStableId", "Locator 根 StableId", FieldType.String, false, "root", "根节点");
            return schema;
        }

        public static ConfigSchema CreateBodyPartSchema()
        {
            var schema = CreateSchema(BodyPartSchemaId, "角色身体部位");
            Add(schema, "partId", "部位 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "displayName", "显示名", FieldType.String, false, "identity", "身份");
            Add(schema, "partKind", "部位类型", FieldType.Enum, true, "binding", "绑定", enumId: "character.bodyPartKind");
            Add(schema, "parentPartId", "父部位", FieldType.Reference, false, "binding", "绑定", referenceSource: BodyPartSchemaId);
            Add(schema, "bonePath", "代表骨骼路径", FieldType.String, false, "binding", "绑定", description: "骨骼角色中代表该 body part 的骨骼路径；collider/socket 可以以此作为局部父空间。");
            Add(schema, "locatorId", "代表 Locator", FieldType.String, false, "binding", "绑定", description: "骨骼 locator、primitive anchor 或 virtual locator。");
            Add(schema, "defaultHitZoneId", "默认 HitZone", FieldType.String, false, "combat", "战斗");
            Add(schema, "reactionGroupId", "受击反应组", FieldType.String, false, "combat", "战斗");
            Add(schema, "tags", "标签", FieldType.String, false, "identity", "身份", isList: true);
            return schema;
        }

        public static ConfigSchema CreateBodyColliderSchema()
        {
            var schema = CreateSchema(BodyColliderSchemaId, "身体碰撞体");
            Add(schema, "colliderId", "Collider ID", FieldType.String, true, "identity", "身份");
            Add(schema, "partId", "身体部位 ID", FieldType.Reference, true, "binding", "绑定", referenceSource: BodyPartSchemaId);
            Add(schema, "hitZoneId", "HitZone ID", FieldType.String, true, "binding", "绑定");
            Add(schema, "shape", "形状", FieldType.Enum, true, "shape", "形状", enumId: "character.colliderShape");
            Add(schema, "localPose", "局部姿态", FieldType.String, true, "transform", "Transform", description: "相对 partId 代表骨骼 / locator 的局部姿态；未显式声明父空间时 Compiler 继承 body part。");
            Add(schema, "size", "尺寸", FieldType.String, false, "shape", "形状");
            Add(schema, "radius", "半径", FieldType.Float, false, "shape", "形状", unit: "m");
            Add(schema, "height", "高度", FieldType.Float, false, "shape", "形状", unit: "m");
            Add(schema, "priority", "优先级", FieldType.Integer, false, "resolve", "解析");
            Add(schema, "isWeakPoint", "弱点", FieldType.Boolean, false, "resolve", "解析");
            Add(schema, "damageMultiplierOverride", "伤害倍率覆盖", FieldType.Float, false, "resolve", "解析");
            Add(schema, "impactPowerScaleOverride", "冲击倍率覆盖", FieldType.Float, false, "resolve", "解析");
            Add(schema, "physicsLayer", "物理层", FieldType.String, false, "physics", "物理");
            Add(schema, "materialStableId", "物理材质 StableId", FieldType.String, false, "physics", "物理");
            return schema;
        }

        public static ConfigSchema CreateSocketSchema()
        {
            var schema = CreateSchema(SocketSchemaId, "角色 Socket");
            Add(schema, "socketId", "Socket ID", FieldType.String, true, "identity", "身份");
            Add(schema, "abstractBoneId", "抽象骨骼 ID", FieldType.String, false, "identity", "身份", description: "核心语义或 ext.<packageId>.<name>；同一角色包内必须唯一。");
            Add(schema, "parentPartId", "父部位", FieldType.Reference, false, "binding", "绑定", referenceSource: BodyPartSchemaId);
            Add(schema, "bonePath", "骨骼路径", FieldType.String, false, "binding", "绑定");
            Add(schema, "locatorPath", "Locator 路径", FieldType.String, false, "binding", "绑定");
            Add(schema, "localPose", "局部姿态", FieldType.String, true, "transform", "Transform", description: "相对 bonePath、locatorPath 或 parentPartId 的局部姿态；武器 socket 的 identity pose 应是默认握持姿态。");
            Add(schema, "usage", "用途", FieldType.Enum, true, "usage", "用途", enumId: "character.socketUsage");
            Add(schema, "mirrorPairSocketId", "镜像 Socket", FieldType.Reference, false, "usage", "用途", referenceSource: SocketSchemaId);
            Add(schema, "handedness", "手性", FieldType.Enum, false, "usage", "用途", enumId: "character.socketHandedness");
            Add(schema, "sideTag", "侧向标签", FieldType.Enum, false, "usage", "用途", enumId: "character.socketSideTag");
            Add(schema, "tags", "标签", FieldType.String, false, "identity", "身份", isList: true);
            return schema;
        }

        public static ConfigSchema CreateWeaponAttachmentSchema()
        {
            var schema = CreateSchema(WeaponAttachmentSchemaId, "角色武器槽位");
            Add(schema, "slotId", "语义槽位", FieldType.String, true, "identity", "身份");
            Add(schema, "attachSocketId", "挂接 Socket", FieldType.Reference, true, "binding", "绑定", referenceSource: SocketSchemaId);
            Add(schema, "slotPose", "角色槽位姿态", FieldType.String, true, "transform", "Transform");
            return schema;
        }

        public static ConfigSchema CreateCharacterApplicationSchema()
        {
            var schema = CreateSchema(CharacterApplicationSchemaId, "角色应用 8.0");
            Add(schema, "schemaVersion", "Schema 版本", FieldType.String, true, "version", "版本");
            Add(schema, "defaultCombinedWeaponConfigurationId", "默认组合武器配置", FieldType.Reference, true, "weapon", "组合武器配置", referenceSource: WeaponConfigurationSchemaId);
            Add(schema, "combinedWeaponConfigurationBindings", "公共组合选择与角色覆盖", FieldType.Reference, true, "weapon", "组合武器配置", referenceSource: WeaponConfigurationBindingSchemaId, isList: true);
            return schema;
        }

        public static ConfigSchema CreateGlobalWeaponLibrarySchema()
        {
            var schema = CreateSchema(GlobalWeaponLibrarySchemaId, "Global Weapon Configuration Library 6.0");
            Add(schema, "schemaVersion", "Schema Version", FieldType.String, true, "version", "Version");
            Add(schema, "libraryId", "Library ID", FieldType.String, true, "identity", "Identity");
            Add(schema, "singleWeaponConfigurations", "Single Weapon Configurations", FieldType.Reference, true, "configuration", "Configurations", referenceSource: WeaponDefinitionSchemaId, isList: true);
            Add(schema, "animationConfigurations", "Animation Configurations", FieldType.Reference, true, "configuration", "Configurations", referenceSource: AnimationConfigurationSchemaId, isList: true);
            Add(schema, "abilityConfigurations", "Ability Configurations", FieldType.Reference, true, "configuration", "Configurations", referenceSource: AbilityConfigurationSchemaId, isList: true);
            Add(schema, "reactionConfigurations", "Reaction Configurations", FieldType.Reference, true, "configuration", "Configurations", referenceSource: ReactionConfigurationSchemaId, isList: true);
            Add(schema, "combinedWeaponConfigurations", "Public Combined Weapon Configurations", FieldType.Reference, true, "configuration", "Configurations", referenceSource: WeaponConfigurationSchemaId, isList: true);
            return schema;
        }

        public static ConfigSchema CreateAnimationConfigurationSchema()
        {
            var schema = CreateSchema(AnimationConfigurationSchemaId, "Global Animation Configuration");
            Add(schema, "animationConfigurationId", "Animation Configuration ID", FieldType.String, true, "identity", "Identity");
            Add(schema, "skeletonProfileId", "Skeleton Profile", FieldType.String, true, "compatibility", "Compatibility");
            Add(schema, "avatarProfileId", "Avatar Profile", FieldType.String, false, "compatibility", "Compatibility");
            Add(schema, "sets", "Animation Sets", FieldType.String, true, "animation", "Animation", isList: true);
            Add(schema, "profiles", "Animation Profiles", FieldType.String, true, "animation", "Animation", isList: true);
            return schema;
        }

        public static ConfigSchema CreateAbilityConfigurationSchema()
        {
            var schema = CreateSchema(AbilityConfigurationSchemaId, "Global Ability Configuration");
            Add(schema, "abilityConfigurationId", "Ability Configuration ID", FieldType.String, true, "identity", "Identity");
            Add(schema, "abilityIds", "Complete Ability Set", FieldType.Integer, true, "ability", "Ability", isList: true);
            Add(schema, "removeAbilityIds", "Removed Ability IDs", FieldType.Integer, false, "ability", "Ability", isList: true);
            Add(schema, "slotBindings", "Input and Action Role Bindings", FieldType.String, false, "ability", "Ability", isList: true);
            return schema;
        }

        public static ConfigSchema CreateReactionConfigurationSchema()
        {
            var schema = CreateSchema(ReactionConfigurationSchemaId, "Global Reaction Configuration");
            Add(schema, "reactionConfigurationId", "Reaction Configuration ID", FieldType.String, true, "identity", "Identity");
            Add(schema, "defaultSlotId", "Default Reaction Slot", FieldType.String, true, "reaction", "Reaction");
            Add(schema, "slots", "Reaction Slots and Action Roles", FieldType.String, true, "reaction", "Reaction", isList: true);
            return schema;
        }

        public static ConfigSchema CreateWeaponDefinitionSchema()
        {
            var schema = CreateSchema(WeaponDefinitionSchemaId, "单一武器配置");
            Add(schema, "singleWeaponConfigurationId", "单一武器配置 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "displayName", "显示名称", FieldType.String, true, "identity", "身份");
            Add(schema, "category", "武器类别", FieldType.String, true, "weapon", "武器");
            Add(schema, "tags", "标签", FieldType.String, false, "weapon", "武器", isList: true);
            Add(schema, "allowedSlots", "允许槽位", FieldType.String, true, "slot", "槽位", isList: true);
            Add(schema, "occupiesSlots", "占用槽位", FieldType.String, false, "slot", "槽位", isList: true);
            Add(schema, "weaponRef", "武器资源引用", FieldType.String, true, "resource", "资源");
            Add(schema, "modelPose", "武器自身变换", FieldType.String, true, "transform", "Transform");
            Add(schema, "presentationConfiguration", "Presentation", FieldType.String, true, "configuration", "Base Configuration");
            Add(schema, "combatConfiguration", "Combat", FieldType.String, true, "configuration", "Base Configuration");
            Add(schema, "resourcePolicy", "Resource Policy", FieldType.String, true, "configuration", "Base Configuration");
            Add(schema, "traceProfiles", "Weapon-local Traces", FieldType.Reference, false, "configuration", "Base Configuration", referenceSource: WeaponTraceSchemaId, isList: true);
            Add(schema, "baseModifiers", "Base Modifiers", FieldType.String, false, "configuration", "Base Configuration", isList: true);
            return schema;
        }

        public static ConfigSchema CreateWeaponActionConfigurationSchema()
        {
            var schema = CreateSchema(WeaponActionConfigurationSchemaId, "Combined Weapon Action Configuration");
            Add(schema, "actionSetId", "ActionSet ID", FieldType.String, true, "identity", "Identity");
            Add(schema, "actions", "Actions and Timelines", FieldType.String, true, "action", "Action", isList: true);
            Add(schema, "abilityActionBindings", "Ability Action Role Bindings", FieldType.String, false, "binding", "Binding", isList: true);
            Add(schema, "reactionActionBindings", "Reaction Action Role Bindings", FieldType.String, false, "binding", "Binding", isList: true);
            return schema;
        }

        public static ConfigSchema CreateWeaponConfigurationSchema()
        {
            var schema = CreateSchema(WeaponConfigurationSchemaId, "组合武器配置");
            Add(schema, "combinedWeaponConfigurationId", "组合配置 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "displayName", "显示名称", FieldType.String, true, "identity", "身份");
            Add(schema, "slots", "槽位", FieldType.Reference, false, "slot", "槽位", referenceSource: WeaponConfigurationSlotSchemaId, isList: true);
            Add(schema, "animationConfigurationId", "Global Animation Configuration", FieldType.Reference, true, "binding", "Binding", referenceSource: AnimationConfigurationSchemaId);
            Add(schema, "abilityConfigurationId", "Global Ability Configuration", FieldType.Reference, true, "binding", "Binding", referenceSource: AbilityConfigurationSchemaId);
            Add(schema, "reactionConfigurationId", "Global Reaction Configuration", FieldType.Reference, true, "binding", "Binding", referenceSource: ReactionConfigurationSchemaId);
            Add(schema, "actionConfiguration", "Public CharacterAction / ActionSet Default", FieldType.Reference, true, "action", "Action Default", referenceSource: WeaponActionConfigurationSchemaId);
            return schema;
        }

        public static ConfigSchema CreateWeaponConfigurationBindingSchema()
        {
            var schema = CreateSchema(WeaponConfigurationBindingSchemaId, "角色公共组合绑定");
            Add(schema, "combinedWeaponConfigurationId", "公共组合配置", FieldType.Reference, true, "binding", "绑定", referenceSource: WeaponConfigurationSchemaId);
            Add(schema, "overrides", "角色行为覆盖", FieldType.Reference, false, "binding", "覆盖", referenceSource: WeaponConfigurationOverridesSchemaId);
            return schema;
        }

        public static ConfigSchema CreateWeaponConfigurationOverridesSchema()
        {
            var schema = CreateSchema(WeaponConfigurationOverridesSchemaId, "角色组合行为覆盖");
            Add(schema, "animationConfigurationId", "动画覆盖", FieldType.Reference, false, "binding", "覆盖", referenceSource: AnimationConfigurationSchemaId);
            Add(schema, "abilityConfigurationId", "能力覆盖", FieldType.Reference, false, "binding", "覆盖", referenceSource: AbilityConfigurationSchemaId);
            Add(schema, "reactionConfigurationId", "受击覆盖", FieldType.Reference, false, "binding", "覆盖", referenceSource: ReactionConfigurationSchemaId);
            Add(schema, "actionConfiguration", "动作完整覆盖", FieldType.Reference, false, "action", "覆盖", referenceSource: WeaponActionConfigurationSchemaId);
            return schema;
        }

        public static ConfigSchema CreateWeaponConfigurationSlotSchema()
        {
            var schema = CreateSchema(WeaponConfigurationSlotSchemaId, "武器配置槽位");
            Add(schema, "slotId", "槽位 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "singleWeaponConfigurationId", "单一武器配置", FieldType.Reference, true, "binding", "绑定", referenceSource: WeaponDefinitionSchemaId);
            return schema;
        }

        public static ConfigSchema CreateWeaponTraceSchema()
        {
            var schema = CreateSchema(WeaponTraceSchemaId, "武器 Trace");
            Add(schema, "traceId", "Trace ID", FieldType.String, true, "identity", "身份");
            Add(schema, "startLocatorPath", "起点 Locator", FieldType.String, false, "binding", "绑定");
            Add(schema, "endLocatorPath", "终点 Locator", FieldType.String, false, "binding", "绑定");
            Add(schema, "startLocatorPath", "起点 Locator", FieldType.String, false, "binding", "绑定");
            Add(schema, "endLocatorPath", "终点 Locator", FieldType.String, false, "binding", "绑定");
            Add(schema, "startPose", "起点姿态", FieldType.String, true, "transform", "Transform");
            Add(schema, "endPose", "终点姿态", FieldType.String, true, "transform", "Transform");
            Add(schema, "radius", "半径", FieldType.Float, true, "shape", "形状", unit: "m");
            Add(schema, "proximalExtension", "Proximal Extension", FieldType.Float, false, "shape", "Trace", unit: "m");
            Add(schema, "proximalRadius", "Proximal Radius", FieldType.Float, false, "shape", "Trace", unit: "m");
            Add(schema, "sampleRule", "采样规则", FieldType.Enum, true, "sampling", "采样", enumId: "character.traceSampleRule");
            Add(schema, "fixedSampleCount", "固定采样数", FieldType.Integer, false, "sampling", "采样");
            Add(schema, "actionKeys", "动作 Key", FieldType.String, false, "binding", "绑定", isList: true);
            return schema;
        }

        public static ConfigSchema CreateValidationIssueSchema()
        {
            var schema = CreateSchema(ValidationIssueSchemaId, "角色包校验 Issue");
            Add(schema, "code", "稳定错误码", FieldType.String, true, "identity", "身份");
            Add(schema, "severity", "严重度", FieldType.Enum, true, "gate", "Gate", enumId: "character.validationSeverity");
            Add(schema, "gate", "Gate", FieldType.Enum, true, "gate", "Gate", enumId: "character.validationGate");
            Add(schema, "sourcePath", "源文件路径", FieldType.AssetPath, true, "source", "来源");
            Add(schema, "sourceObjectPath", "源对象路径", FieldType.String, false, "source", "来源");
            Add(schema, "field", "字段", FieldType.String, false, "source", "来源");
            Add(schema, "message", "消息", FieldType.String, true, "message", "消息");
            Add(schema, "suggestedFix", "建议修复", FieldType.String, false, "message", "消息");
            return schema;
        }

        public static ConfigSchema CreateCompilerResultSchema()
        {
            var schema = CreateSchema(CompilerResultSchemaId, "角色 Authoring Compiler 结果");
            Add(schema, "format", "格式", FieldType.String, true, "identity", "身份", description: "固定为 mx.characterAuthoringCompileResult.v1。");
            Add(schema, "packageId", "包 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "packageStableId", "包 StableId", FieldType.String, true, "identity", "身份");
            Add(schema, "isDeterministicFullCompile", "确定性全量编译", FieldType.Boolean, true, "compile", "编译");
            Add(schema, "status", "编译状态", FieldType.Enum, true, "gate", "Gate", enumId: "character.compilerStatus");
            Add(schema, "hashes.sourcePackageHash", "源包 Hash", FieldType.String, true, "hash", "Hash");
            Add(schema, "hashes.generatedConfigHash", "生成配置 Hash", FieldType.String, true, "hash", "Hash");
            Add(schema, "hashes.resourceMappingHash", "资源映射 Hash", FieldType.String, true, "hash", "Hash");
            Add(schema, "generatedConfigPatch", "生成配置 Patch", FieldType.String, true, "output", "输出");
            Add(schema, "geometryBinding", "几何绑定", FieldType.String, true, "output", "输出");
            Add(schema, "resourceMapping", "资源映射", FieldType.String, true, "output", "输出");
            Add(schema, "unityImportWritePlan", "Unity 写入计划", FieldType.String, true, "output", "输出");
            Add(schema, "resolverVerificationPlan", "Resolver 验证计划", FieldType.String, true, "output", "输出");
            Add(schema, "sourceMappings", "Source Mapping", FieldType.String, true, "source", "来源", isList: true);
            return schema;
        }

        private static ConfigSchema CreateSchema(string id, string displayName)
        {
            return new ConfigSchema
            {
                SchemaId = id,
                DisplayName = displayName,
                StructureKind = "CharacterResourcePackage"
            };
        }

        private static void Add(ConfigSchema schema, string name, string displayName, FieldType type, bool required, string groupId, string groupDisplayName, string enumId = "", string referenceSource = "", string unit = "", string description = "", bool isList = false)
        {
            schema.Fields.Add(new SchemaField
            {
                Name = name,
                DisplayName = displayName,
                Type = type,
                Required = required,
                EnumId = enumId,
                ReferenceSource = referenceSource,
                Unit = unit,
                Description = description,
                GroupId = groupId,
                GroupDisplayName = groupDisplayName,
                IsList = isList
            });
        }

        private static EnumDomain CreatePackageKindEnum()
        {
            return Enum("characterPackage.kind", ("Unknown", 0), ("Character", 1));
        }

        private static EnumDomain CreateCoordinateAxisEnum()
        {
            return Enum("character.coordinateAxis", ("Unknown", 0), ("XPositive", 1), ("XNegative", 2), ("YPositive", 3), ("YNegative", 4), ("ZPositive", 5), ("ZNegative", 6));
        }

        private static EnumDomain CreateCoordinateHandednessEnum()
        {
            return Enum("character.coordinateHandedness", ("Unknown", 0), ("LeftHanded", 1), ("RightHanded", 2));
        }

        private static EnumDomain CreateRotationStorageEnum()
        {
            return Enum("character.rotationStorage", ("Unknown", 0), ("Quaternion", 1));
        }

        private static EnumDomain CreateResourceTypeEnum()
        {
            return Enum("character.resourceType", ("model", 1), ("texture", 2), ("material", 3), ("animation", 4), ("audio", 5), ("vfx", 6), ("preview", 7), ("config", 8), ("geometry", 9));
        }

        private static EnumDomain CreateResourceSourceFormatEnum()
        {
            return Enum("character.resourceSourceFormat", ("gltf", 1), ("glb", 2), ("fbx", 100), ("anim", 23), ("png", 10), ("jpg", 11), ("jpeg", 12), ("tga", 13), ("json", 20), ("materialJson", 21), ("animationGroupJson", 22), ("wav", 30), ("ogg", 31), ("vfxJson", 40));
        }

        private static EnumDomain CreateResourceUsageEnum()
        {
            return Enum("character.resourceUsage", ("characterModel", 1), ("weaponModel", 2), ("texture", 3), ("material", 4), ("animationClipGroup", 5), ("audioCue", 6), ("vfxCue", 7), ("previewThumbnail", 8), ("previewMesh", 9), ("characterConfig", 10), ("geometryAuthoring", 11));
        }

        private static EnumDomain CreateImportTargetPathPolicyEnum()
        {
            return Enum("character.importTargetPathPolicy", ("generatedCharacterPackage", 1), ("packageRelativeMirror", 2), ("projectResourceCatalogOnly", 3));
        }

        private static EnumDomain CreateConflictActionEnum()
        {
            return Enum("character.resourceConflictAction", ("skipWhenHashUnchanged", 1), ("reportWhenHashChanged", 2), ("requireExplicitUpgrade", 3), ("createVariant", 4));
        }

        private static EnumDomain CreateBodyKindEnum()
        {
            return Enum("character.bodyKind", ("Unknown", 0), ("Skeletal", 1), ("Primitive", 2), ("Compound", 3));
        }

        private static EnumDomain CreateBodyPartKindEnum()
        {
            return Enum("character.bodyPartKind", ("Unknown", 0), ("Bone", 1), ("Primitive", 2), ("Virtual", 3));
        }

        private static EnumDomain CreatePoseParentKindEnum()
        {
            return Enum("character.poseParentKind", ("Unknown", 0), ("ModelRoot", 1), ("SkeletonRoot", 2), ("Bone", 3), ("Locator", 4), ("BodyPart", 5), ("Socket", 6), ("WorldPreview", 7));
        }

        private static EnumDomain CreateColliderShapeEnum()
        {
            return Enum("character.colliderShape", ("Unknown", 0), ("Capsule", 1), ("Box", 2), ("Sphere", 3), ("Convex", 100), ("CustomMesh", 101), ("Reserved1000", 1000), ("Reserved1001", 1001));
        }

        private static EnumDomain CreateSocketUsageEnum()
        {
            return Enum("character.socketUsage", ("Unknown", 0), ("Weapon", 1), ("Vfx", 2), ("Camera", 3), ("Ui", 4), ("Gameplay", 5));
        }

        private static EnumDomain CreateSocketHandednessEnum()
        {
            return Enum("character.socketHandedness", ("Unknown", 0), ("None", 1), ("Left", 2), ("Right", 3), ("Both", 4));
        }

        private static EnumDomain CreateSocketSideTagEnum()
        {
            return Enum("character.socketSideTag", ("Unknown", 0), ("Center", 1), ("Left", 2), ("Right", 3), ("Front", 4), ("Back", 5));
        }

        private static EnumDomain CreateTraceSampleRuleEnum()
        {
            return Enum("character.traceSampleRule", ("Unknown", 0), ("LineSegment", 1), ("CapsuleSweep", 2), ("FixedSamples", 3));
        }

        private static EnumDomain CreateValidationSeverityEnum()
        {
            return Enum("character.validationSeverity", ("Info", 0), ("Warning", 1), ("Error", 2));
        }

        private static EnumDomain CreateValidationGateEnum()
        {
            return Enum("character.validationGate", ("Unknown", 0), ("ExportBlocked", 10), ("ImportBlocked", 20), ("SpawnBlocked", 30), ("WarningOnly", 40), ("Reserved1000", 1000), ("Reserved1001", 1001), ("Reserved1002", 1002));
        }

        private static EnumDomain CreateCompilerStatusEnum()
        {
            return Enum("character.compilerStatus", ("Ready", 0), ("WarningOnly", 1), ("SpawnBlocked", 2), ("ImportBlocked", 3), ("ExportBlocked", 4));
        }

        private static EnumDomain Enum(string id, params (string Name, int Value)[] options)
        {
            var domain = new EnumDomain { EnumId = id };
            for (int i = 0; i < options.Length; i++)
            {
                domain.Options.Add(new EnumOption
                {
                    Name = options[i].Name,
                    DisplayName = options[i].Name,
                    Value = options[i].Value
                });
            }

            return domain;
        }
    }
}
