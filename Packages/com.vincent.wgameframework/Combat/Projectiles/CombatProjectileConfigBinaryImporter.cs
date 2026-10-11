using System;
using System.IO;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Combat.Projectiles
{
    public static class CombatProjectileConfigBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.ProjectileDefinitions;
        public const string CurrentSchemaVersion = CombatProjectileConfigJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by CombatProjectileDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatProjectileDefinitionRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<CombatProjectileDefinitionRegistry>.Failed(
                    "MissingFile",
                    "Projectile definition binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<CombatProjectileDefinitionRegistry>.Failed(
                    "ReadFileFailed",
                    "Projectile definition binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by CombatProjectileDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatProjectileDefinitionRegistry> ImportBytes(
            byte[] bytes,
            string sourcePath = "")
        {
            return RuntimeCatalogBinary.Import(
                bytes,
                CatalogKind,
                CurrentSchemaVersion,
                ReadCatalog,
                sourcePath);
        }

        private static CombatProjectileDefinitionRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var registry = new CombatProjectileDefinitionRegistry();
            for (int i = 0; i < header.EntryCount; i++)
            {
                CombatProjectileDefinition definition = ReadDefinition(reader);
                if (!registry.TryRegister(definition, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid projectile definition binary catalog entry at "
                        + (sourcePath ?? string.Empty)
                        + "#"
                        + i
                        + ": "
                        + failure);
                }
            }

            loadedEntryCount = registry.Count;
            return registry;
        }

        private static CombatProjectileDefinition ReadDefinition(BinaryReader reader)
        {
            int definitionId = reader.ReadInt32();
            string stableId = reader.ReadString();
            bool physicalEventsEnabled = reader.ReadBoolean();
            return new CombatProjectileDefinition(
                definitionId,
                stableId,
                ReadMotion(reader),
                ReadCollision(reader),
                ReadImpact(reader),
                physicalEventsEnabled);
        }

        private static CombatProjectileMotionConfig ReadMotion(BinaryReader reader)
        {
            return new CombatProjectileMotionConfig(
                ReadEnum<CombatProjectileMotionKind>(reader, "motion.kind"),
                Fix64.FromRaw(reader.ReadInt64()),
                reader.ReadInt32(),
                Fix64.FromRaw(reader.ReadInt64()),
                ReadEnum<CombatProjectileSpeedProfileKind>(reader, "motion.speedProfileKind"),
                Fix64.FromRaw(reader.ReadInt64()),
                reader.ReadInt32(),
                ReadVector(reader),
                ReadVector(reader),
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()));
        }

        private static CombatProjectileCollisionConfig ReadCollision(BinaryReader reader)
        {
            return new CombatProjectileCollisionConfig(
                Fix64.FromRaw(reader.ReadInt64()),
                new CombatPhysicsLayerMask(reader.ReadInt32()),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                ReadEnum<CombatProjectileHitSameTargetPolicy>(reader, "collision.hitSameTargetPolicy"));
        }

        private static CombatProjectileImpactConfig ReadImpact(BinaryReader reader)
        {
            int presentation = reader.ReadInt32(); string presentationLabel = reader.ReadString();
            int stable = reader.ReadInt32(); string stableLabel = reader.ReadString();
            var kind = ReadEnum<CombatProjectileImpactPolicyKind>(reader, "impact.policyKind");
            Fix64 radius = Fix64.FromRaw(reader.ReadInt64());
            var layers = new CombatPhysicsLayerMask(reader.ReadInt32());
            bool expire = reader.ReadBoolean();
            CombatHitEventConfig hit = ReadHitEvent(reader), area = ReadHitEvent(reader);
            int timeline = reader.ReadInt32(); string timelineLabel = reader.ReadString();
            return new CombatProjectileImpactConfig(presentation, stable, kind, radius, layers,
                expire, hit, area, timeline, presentationLabel, stableLabel, timelineLabel);
        }

        private static CombatHitEventConfig ReadHitEvent(BinaryReader reader)
        {
            var targets = (CombatHitTargetTypes)reader.ReadInt32();
            var environment = ReadEnum<CombatEnvironmentHitPolicy>(reader, "hitEvent.environmentHitPolicy");
            int id = reader.ReadInt32(); string label = reader.ReadString();
            int maximum = reader.ReadInt32();
            var sameTarget = ReadEnum<CombatHitSameTargetPolicy>(reader, "hitEvent.hitSameTargetPolicy");
            CombatHitDamageConfig damage = reader.ReadBoolean() ? ReadDamage(reader) : default;
            return new CombatHitEventConfig(targets, environment, id, maximum, sameTarget,
                damage, ReadOnHitBuffs(reader), label);
        }

        private static CombatHitDamageConfig ReadDamage(BinaryReader reader)
        {
            int type = reader.ReadInt32(); string typeLabel = reader.ReadString();
            int damage = reader.ReadInt32(), impact = reader.ReadInt32();
            int[] tags = ReadIdArray(reader); string[] tagLabels = ReadStringArray(reader);
            int category = reader.ReadInt32(); string categoryLabel = reader.ReadString();
            int launch = reader.ReadInt32();
            int scaleCount = reader.ReadInt32();
            if (scaleCount < 0 || scaleCount > 256)
                throw new InvalidDataException("Invalid source attribute scale count.");
            var scales = new DamageAttributeScale[scaleCount];
            for (int i = 0; i < scaleCount; i++)
            {
                int attributeId = reader.ReadInt32();
                float scale = reader.ReadSingle();
                if (attributeId <= 0 || scale < 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                    throw new InvalidDataException("Invalid projectile source attribute scale.");
                scales[i] = new DamageAttributeScale(attributeId, scale);
            }
            return new CombatHitDamageConfig(type, damage, impact, tags, category, launch,
                typeLabel, categoryLabel, tagLabels, scales);
        }

        private static int[] ReadIdArray(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0) throw new InvalidDataException("Negative tag count.");
            var ids = new int[count];
            for (int i = 0; i < count; i++) ids[i] = reader.ReadInt32();
            return ids;
        }

        private static CombatHitBuffApplicationConfig[] ReadOnHitBuffs(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0)
                return Array.Empty<CombatHitBuffApplicationConfig>();

            var values = new CombatHitBuffApplicationConfig[count];
            for (int i = 0; i < count; i++)
                values[i] = new CombatHitBuffApplicationConfig(reader.ReadInt32());
            return values;
        }

        private static string[] ReadStringArray(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0)
                return Array.Empty<string>();

            var values = new string[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadString();
            return values;
        }

        private static FixVector3 ReadVector(BinaryReader reader)
        {
            return new FixVector3(
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()));
        }

        private static TEnum ReadEnum<TEnum>(BinaryReader reader, string fieldName)
            where TEnum : struct
        {
            int raw = reader.ReadInt32();
            if (!Enum.IsDefined(typeof(TEnum), raw))
                throw new ArgumentOutOfRangeException(fieldName, "Unsupported enum value: " + raw + ".");
            return (TEnum)Enum.ToObject(typeof(TEnum), raw);
        }
    }

    public static class CombatProjectileSpawnProfileConfigBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.ProjectileSpawnProfiles;
        public const string CurrentSchemaVersion = "2";

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by CombatProjectileSpawnProfileRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatProjectileSpawnProfileRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<CombatProjectileSpawnProfileRegistry>.Failed(
                    "MissingFile",
                    "Projectile spawn profile binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<CombatProjectileSpawnProfileRegistry>.Failed(
                    "ReadFileFailed",
                    "Projectile spawn profile binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by CombatProjectileSpawnProfileRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatProjectileSpawnProfileRegistry> ImportBytes(
            byte[] bytes,
            string sourcePath = "")
        {
            return RuntimeCatalogBinary.Import(
                bytes,
                CatalogKind,
                CurrentSchemaVersion,
                ReadCatalog,
                sourcePath);
        }

        private static CombatProjectileSpawnProfileRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var registry = new CombatProjectileSpawnProfileRegistry();
            for (int i = 0; i < header.EntryCount; i++)
            {
                CombatProjectileSpawnProfile profile = ReadProfile(reader);
                if (!registry.TryRegister(profile, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid projectile spawn profile binary catalog entry at "
                        + (sourcePath ?? string.Empty)
                        + "#"
                        + i
                        + ": "
                        + failure);
                }
            }

            loadedEntryCount = registry.Count;
            return registry;
        }

        private static CombatProjectileSpawnProfile ReadProfile(BinaryReader reader)
        {
            return new CombatProjectileSpawnProfile(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadString(),
                new CombatProjectileSpawnOriginConfig(
                    ReadEnum<CombatProjectileSpawnOriginKind>(reader, "origin.kind"),
                    reader.ReadInt32(),
                    ReadVector(reader),
                    ReadVector(reader),
                    reader.ReadString()),
                new CombatProjectileAimConfig(
                    ReadEnum<CombatProjectileAimKind>(reader, "aim.kind"),
                    Fix64.FromRaw(reader.ReadInt64()),
                    ReadEnum<CombatProjectileAimKind>(reader, "aim.fallbackKind"),
                    Fix64.FromRaw(reader.ReadInt64())));
        }

        private static FixVector3 ReadVector(BinaryReader reader)
        {
            return new FixVector3(
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()));
        }

        private static TEnum ReadEnum<TEnum>(BinaryReader reader, string fieldName)
            where TEnum : struct
        {
            int raw = reader.ReadInt32();
            if (!Enum.IsDefined(typeof(TEnum), raw))
                throw new ArgumentOutOfRangeException(fieldName, "Unsupported enum value: " + raw + ".");
            return (TEnum)Enum.ToObject(typeof(TEnum), raw);
        }
    }
}
