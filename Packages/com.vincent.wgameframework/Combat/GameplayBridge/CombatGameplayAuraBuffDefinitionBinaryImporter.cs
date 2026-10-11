using System;
using System.IO;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Combat.GameplayBridge
{
    public static class CombatGameplayAuraBuffDefinitionBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.CombatGameplayAuraBuffDefinitions;
        public const string CurrentSchemaVersion = CombatGameplayAuraBuffDefinitionJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by CombatGameplayAuraBuffDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatGameplayAuraBuffDefinitionRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<CombatGameplayAuraBuffDefinitionRegistry>.Failed(
                    "MissingFile",
                    "Combat gameplay aura buff binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<CombatGameplayAuraBuffDefinitionRegistry>.Failed(
                    "ReadFileFailed",
                    "Combat gameplay aura buff binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by CombatGameplayAuraBuffDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatGameplayAuraBuffDefinitionRegistry> ImportBytes(
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

        private static CombatGameplayAuraBuffDefinitionRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var definitions = new CombatGameplayAuraBuffDefinition[header.EntryCount];
            for (int i = 0; i < header.EntryCount; i++)
            {
                definitions[i] = new CombatGameplayAuraBuffDefinition(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    Fix64.FromRaw(reader.ReadInt64()),
                    new CombatPhysicsLayerMask(reader.ReadInt32()),
                    ReadEnum<GameplayTargetRelationFilter>(reader, "relationFilter"),
                    reader.ReadBoolean());
            }

            var registry = new CombatGameplayAuraBuffDefinitionRegistry(definitions);
            loadedEntryCount = registry.Count;
            return registry;
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


