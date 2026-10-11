using System;
using System.IO;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.Vfx
{
    public static class VfxConfigBinaryImporter
    {
        public const string DefinitionCatalogKind = RuntimeCatalogBinaryKinds.VfxDefinitions;
        public const string TriggerMapCatalogKind = RuntimeCatalogBinaryKinds.VfxTriggerMaps;
        public const string CurrentSchemaVersion = VfxConfigJsonImporter.CurrentSchemaVersion;
        public const string TriggerMapSchemaVersion = VfxConfigJsonImporter.TriggerMapSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by VfxDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<VfxDefinitionRegistry> ImportDefinitionFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<VfxDefinitionRegistry>.Failed(
                    "MissingFile",
                    "VFX definition binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportDefinitions(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<VfxDefinitionRegistry>.Failed(
                    "ReadFileFailed",
                    "VFX definition binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by VfxDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<VfxDefinitionRegistry> ImportDefinitions(
            byte[] bytes,
            string sourcePath = "")
        {
            return RuntimeCatalogBinary.Import(
                bytes,
                DefinitionCatalogKind,
                CurrentSchemaVersion,
                ReadDefinitions,
                sourcePath);
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; runtime trigger resolution remains unchanged.</remarks>
        public static RuntimeCatalogBinaryLoadResult<VfxTriggerMapResolver> ImportTriggerMapFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<VfxTriggerMapResolver>.Failed(
                    "MissingFile",
                    "VFX trigger map binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportTriggerMaps(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<VfxTriggerMapResolver>.Failed(
                    "ReadFileFailed",
                    "VFX trigger map binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; runtime trigger resolution remains unchanged.</remarks>
        public static RuntimeCatalogBinaryLoadResult<VfxTriggerMapResolver> ImportTriggerMaps(
            byte[] bytes,
            string sourcePath = "")
        {
            return RuntimeCatalogBinary.Import(
                bytes,
                TriggerMapCatalogKind,
                TriggerMapSchemaVersion,
                ReadTriggerMaps,
                sourcePath);
        }

        private static VfxDefinitionRegistry ReadDefinitions(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var registry = new VfxDefinitionRegistry();
            for (int i = 0; i < header.EntryCount; i++)
            {
                var definition = new VfxDefinition(
                    new VfxDefinitionId(reader.ReadInt32()),
                    reader.ReadString(),
                    ReadResourceKey(reader),
                    reader.ReadInt32(),
                    reader.ReadString());
                if (!registry.TryRegister(definition, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid VFX definition binary catalog entry at "
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

        private static VfxTriggerMapResolver ReadTriggerMaps(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var resolver = new VfxTriggerMapResolver();
            for (int i = 0; i < header.EntryCount; i++)
            {
                int sourceDomain = reader.ReadInt32();
                int triggerId = reader.ReadInt32();
                int definitionId = reader.ReadInt32();
                int lifetimeFramesOverride = reader.ReadInt32();
                if (sourceDomain <= 0 || triggerId <= 0 || definitionId <= 0)
                {
                    throw new InvalidOperationException(
                        "Invalid VFX trigger mapping binary catalog entry at "
                        + (sourcePath ?? string.Empty)
                        + "#"
                        + i
                        + ".");
                }

                resolver.Add(new VfxTriggerMapping(
                    sourceDomain,
                    triggerId,
                    new VfxDefinitionId(definitionId),
                    lifetimeFramesOverride));
            }

            loadedEntryCount = header.EntryCount;
            return resolver;
        }

        private static ResourceKey ReadResourceKey(BinaryReader reader)
        {
            return new ResourceKey(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString());
        }
    }
}
