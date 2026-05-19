using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class CharacterResourcePackageSchemas
    {
        public const string ManifestSchemaId = "CharacterPackageManifest";
        public const string ResourceCatalogSchemaId = "CharacterPackageResourceCatalog";
        public const string BodyGeometrySchemaId = "CharacterBodyGeometryProfile";
        public const string BodyColliderSchemaId = "CharacterBodyColliderProfile";
        public const string SocketSchemaId = "CharacterSocketProfile";
        public const string WeaponAttachmentSchemaId = "WeaponAttachmentProfile";
        public const string WeaponTraceSchemaId = "WeaponTraceProfile";
        public const string ValidationIssueSchemaId = "CharacterAuthoringValidationIssue";

        public static IReadOnlyList<ConfigSchema> CreateAll()
        {
            return new[]
            {
                CreateManifestSchema(),
                CreateResourceCatalogSchema(),
                CreateBodyGeometrySchema(),
                CreateBodyColliderSchema(),
                CreateSocketSchema(),
                CreateWeaponAttachmentSchema(),
                CreateWeaponTraceSchema(),
                CreateValidationIssueSchema()
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
                CreatePoseParentKindEnum(),
                CreateColliderShapeEnum(),
                CreateSocketUsageEnum(),
                CreateSocketHandednessEnum(),
                CreateSocketSideTagEnum(),
                CreateTraceSampleRuleEnum(),
                CreateValidationSeverityEnum(),
                CreateValidationGateEnum()
            };
        }

        public static ConfigSchema CreateManifestSchema()
        {
            var schema = CreateSchema(ManifestSchemaId, "角色资源包 Manifest");
            Add(schema, "packageId", "包短 ID", FieldType.String, true, "identity", "身份", description: "包内短 ID，例如 iron_vanguard。");
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

        public static ConfigSchema CreateBodyColliderSchema()
        {
            var schema = CreateSchema(BodyColliderSchemaId, "身体碰撞体");
            Add(schema, "colliderId", "Collider ID", FieldType.String, true, "identity", "身份");
            Add(schema, "partId", "身体部位 ID", FieldType.Reference, true, "binding", "绑定", referenceSource: "CharacterBodyPartAuthoring");
            Add(schema, "hitZoneId", "HitZone ID", FieldType.String, true, "binding", "绑定");
            Add(schema, "shape", "形状", FieldType.Enum, true, "shape", "形状", enumId: "character.colliderShape");
            Add(schema, "localPose", "局部姿态", FieldType.String, true, "transform", "Transform");
            Add(schema, "size", "尺寸", FieldType.String, false, "shape", "形状");
            Add(schema, "radius", "半径", FieldType.Float, false, "shape", "形状", unit: "m");
            Add(schema, "height", "高度", FieldType.Float, false, "shape", "形状", unit: "m");
            Add(schema, "priority", "优先级", FieldType.Integer, false, "resolve", "解析");
            Add(schema, "isWeakPoint", "弱点", FieldType.Boolean, false, "resolve", "解析");
            Add(schema, "damageMultiplierOverride", "伤害倍率覆盖", FieldType.Float, false, "resolve", "解析");
            Add(schema, "postureDamageScaleOverride", "姿态倍率覆盖", FieldType.Float, false, "resolve", "解析");
            Add(schema, "physicsLayer", "物理层", FieldType.String, false, "physics", "物理");
            Add(schema, "materialStableId", "物理材质 StableId", FieldType.String, false, "physics", "物理");
            return schema;
        }

        public static ConfigSchema CreateSocketSchema()
        {
            var schema = CreateSchema(SocketSchemaId, "角色 Socket");
            Add(schema, "socketId", "Socket ID", FieldType.String, true, "identity", "身份");
            Add(schema, "parentPartId", "父部位", FieldType.Reference, false, "binding", "绑定", referenceSource: "CharacterBodyPartAuthoring");
            Add(schema, "bonePath", "骨骼路径", FieldType.String, false, "binding", "绑定");
            Add(schema, "locatorPath", "Locator 路径", FieldType.String, false, "binding", "绑定");
            Add(schema, "localPose", "局部姿态", FieldType.String, true, "transform", "Transform");
            Add(schema, "usage", "用途", FieldType.Enum, true, "usage", "用途", enumId: "character.socketUsage");
            Add(schema, "mirrorPairSocketId", "镜像 Socket", FieldType.Reference, false, "usage", "用途", referenceSource: SocketSchemaId);
            Add(schema, "handedness", "手性", FieldType.Enum, false, "usage", "用途", enumId: "character.socketHandedness");
            Add(schema, "sideTag", "侧向标签", FieldType.Enum, false, "usage", "用途", enumId: "character.socketSideTag");
            return schema;
        }

        public static ConfigSchema CreateWeaponAttachmentSchema()
        {
            var schema = CreateSchema(WeaponAttachmentSchemaId, "武器挂接");
            Add(schema, "weaponId", "武器 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "equipSlot", "装备槽", FieldType.String, true, "binding", "绑定");
            Add(schema, "attachSocketId", "挂接 Socket", FieldType.Reference, true, "binding", "绑定", referenceSource: SocketSchemaId);
            Add(schema, "localGripPose", "Grip 姿态", FieldType.String, true, "transform", "Transform");
            Add(schema, "previewResourceKey", "预览资源", FieldType.Reference, false, "resource", "资源", referenceSource: ResourceCatalogSchemaId);
            Add(schema, "traceId", "Trace ID", FieldType.Reference, false, "trace", "Trace", referenceSource: WeaponTraceSchemaId);
            Add(schema, "traceStartSocketId", "Trace 起点 Socket", FieldType.Reference, false, "trace", "Trace", referenceSource: SocketSchemaId);
            Add(schema, "traceEndSocketId", "Trace 终点 Socket", FieldType.Reference, false, "trace", "Trace", referenceSource: SocketSchemaId);
            Add(schema, "traceRadius", "Trace 半径", FieldType.Float, false, "trace", "Trace", unit: "m");
            Add(schema, "traceSampleRule", "Trace 采样规则", FieldType.Enum, false, "trace", "Trace", enumId: "character.traceSampleRule");
            return schema;
        }

        public static ConfigSchema CreateWeaponTraceSchema()
        {
            var schema = CreateSchema(WeaponTraceSchemaId, "武器 Trace");
            Add(schema, "traceId", "Trace ID", FieldType.String, true, "identity", "身份");
            Add(schema, "weaponId", "武器 ID", FieldType.String, true, "identity", "身份");
            Add(schema, "equipSlot", "装备槽", FieldType.String, true, "binding", "绑定");
            Add(schema, "startLocatorPath", "起点 Locator", FieldType.String, false, "binding", "绑定");
            Add(schema, "endLocatorPath", "终点 Locator", FieldType.String, false, "binding", "绑定");
            Add(schema, "startPose", "起点姿态", FieldType.String, true, "transform", "Transform");
            Add(schema, "endPose", "终点姿态", FieldType.String, true, "transform", "Transform");
            Add(schema, "radius", "半径", FieldType.Float, true, "shape", "形状", unit: "m");
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
            return Enum("character.resourceSourceFormat", ("gltf", 1), ("glb", 2), ("fbx", 100), ("png", 10), ("jpg", 11), ("jpeg", 12), ("tga", 13), ("json", 20), ("materialJson", 21), ("animationGroupJson", 22), ("wav", 30), ("ogg", 31), ("vfxJson", 40));
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
