using System;
using System.IO;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public static class GameplayBuffDefinitionBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.GameplayBuffDefinitions;
        public const string CurrentSchemaVersion = GameplayBuffDefinitionJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by GameplayBuffDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<GameplayBuffDefinitionRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<GameplayBuffDefinitionRegistry>.Failed(
                    "MissingFile",
                    "Gameplay buff definition binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<GameplayBuffDefinitionRegistry>.Failed(
                    "ReadFileFailed",
                    "Gameplay buff definition binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by GameplayBuffDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<GameplayBuffDefinitionRegistry> ImportBytes(
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

        private static GameplayBuffDefinitionRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var definitions = new GameplayBuffDefinition[header.EntryCount];
            for (int i = 0; i < header.EntryCount; i++)
                definitions[i] = ReadDefinition(reader);

            var registry = new GameplayBuffDefinitionRegistry(definitions);
            loadedEntryCount = registry.Count;
            return registry;
        }

        private static GameplayBuffDefinition ReadDefinition(BinaryReader reader)
        {
            return new GameplayBuffDefinition(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadInt64(),
                reader.ReadInt32(),
                ReadEnum<GameplayBuffStackPolicy>(reader, "stackPolicy"),
                ReadEffects(reader),
                reader.ReadInt32(),
                reader.ReadBoolean());
        }

        private static GameplayBuffEffectDefinition[] ReadEffects(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0)
                return Array.Empty<GameplayBuffEffectDefinition>();

            var effects = new GameplayBuffEffectDefinition[count];
            for (int i = 0; i < count; i++)
            {
                int effectId = reader.ReadInt32();
                GameplayBuffEffectKind kind = ReadEnum<GameplayBuffEffectKind>(reader, "kind");
                int attributeId = reader.ReadInt32();
                int addValue = reader.ReadInt32();
                int statusId = reader.ReadInt32();
                switch (kind)
                {
                    case GameplayBuffEffectKind.AttributeModifier:
                        effects[i] = GameplayBuffEffectDefinition.AttributeModifier(
                            effectId,
                            attributeId,
                            addValue);
                        break;
                    case GameplayBuffEffectKind.Status:
                        effects[i] = GameplayBuffEffectDefinition.Status(
                            effectId,
                            new GameplayStatusId(statusId));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported gameplay buff effect kind.");
                }
            }

            return effects;
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

    public static class GameplayBuffPresentationDefinitionBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.GameplayBuffPresentationDefinitions;
        public const string CurrentSchemaVersion = GameplayBuffPresentationDefinitionJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by GameplayBuffPresentationDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<GameplayBuffPresentationDefinitionRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<GameplayBuffPresentationDefinitionRegistry>.Failed(
                    "MissingFile",
                    "Gameplay buff presentation binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<GameplayBuffPresentationDefinitionRegistry>.Failed(
                    "ReadFileFailed",
                    "Gameplay buff presentation binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by GameplayBuffPresentationDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<GameplayBuffPresentationDefinitionRegistry> ImportBytes(
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

        private static GameplayBuffPresentationDefinitionRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var definitions = new GameplayBuffPresentationDefinition[header.EntryCount];
            for (int i = 0; i < header.EntryCount; i++)
                definitions[i] = ReadDefinition(reader);

            var registry = new GameplayBuffPresentationDefinitionRegistry(definitions);
            loadedEntryCount = registry.Count;
            return registry;
        }

        private static GameplayBuffPresentationDefinition ReadDefinition(BinaryReader reader)
        {
            return new GameplayBuffPresentationDefinition(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                ReadEnum<GameplayBuffPresentationPolarity>(reader, "polarity"),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadString());
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
