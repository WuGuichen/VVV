using System;
using System.IO;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Combat.Damage
{
    public static class DamageProfileBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.DamageProfiles;
        public const string CurrentSchemaVersion = DamageProfileJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; catalog lookup remains governed by IDamageProfileCatalog implementations.</remarks>
        public static RuntimeCatalogBinaryLoadResult<MemoryDamageProfileCatalog> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<MemoryDamageProfileCatalog>.Failed(
                    "MissingFile",
                    "Damage profile binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<MemoryDamageProfileCatalog>.Failed(
                    "ReadFileFailed",
                    "Damage profile binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; catalog lookup remains governed by IDamageProfileCatalog implementations.</remarks>
        public static RuntimeCatalogBinaryLoadResult<MemoryDamageProfileCatalog> ImportBytes(
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

        private static MemoryDamageProfileCatalog ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var profiles = new DamageProfileConfig[header.EntryCount];
            for (int i = 0; i < header.EntryCount; i++)
                profiles[i] = ReadProfile(reader);

            var catalog = new MemoryDamageProfileCatalog(profiles);
            loadedEntryCount = catalog.Count;
            return catalog;
        }

        private static DamageProfileConfig ReadProfile(BinaryReader reader)
        {
            return new DamageProfileConfig(
                reader.ReadInt32(),
                stableId: reader.ReadString(),
                displayName: reader.ReadString(),
                damageTypeId: reader.ReadInt32(),
                formulaId: reader.ReadInt32(),
                baseDamage: reader.ReadInt32(),
                sourceAttributeScales: ReadAttributeScales(reader),
                targetAttributeScales: ReadAttributeScales(reader),
                damageMultiplier: reader.ReadSingle(),
                defenseReference: reader.ReadInt32(),
                defenseWeight: reader.ReadSingle(),
                minDamage: reader.ReadInt32(),
                maxDamage: reader.ReadInt32(),
                staggerFrames: reader.ReadInt32(),
                impactForce: reader.ReadInt32(),
                knockback: ReadVector(reader),
                impactPower: reader.ReadInt32(),
                guardPressure: reader.ReadInt32(),
                presentationProfileId: reader.ReadInt32(),
                tags: ReadIdArray(reader),
                analysisCategory: reader.ReadInt32(),
                damageComponents: ReadComponents(reader));
        }

        private static DamageAttributeScale[] ReadAttributeScales(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0)
                return Array.Empty<DamageAttributeScale>();

            var values = new DamageAttributeScale[count];
            for (int i = 0; i < count; i++)
                values[i] = new DamageAttributeScale(reader.ReadInt32(), reader.ReadSingle());
            return values;
        }

        private static DamageComponentConfig[] ReadComponents(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0)
                return Array.Empty<DamageComponentConfig>();

            var values = new DamageComponentConfig[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new DamageComponentConfig(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }

            return values;
        }

        private static int[] ReadIdArray(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0)
                return Array.Empty<int>();

            var values = new int[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadInt32();
            return values;
        }

        private static FixVector3 ReadVector(BinaryReader reader)
        {
            return new FixVector3(
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()),
                Fix64.FromRaw(reader.ReadInt64()));
        }
    }
}


