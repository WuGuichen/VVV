using System;
using System.IO;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.Combat.Projectiles.Unity
{
    public static class CombatProjectileUnityPresentationConfigBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.ProjectilePresentationBindings;
        public const string CurrentSchemaVersion = CombatProjectileUnityPresentationConfigJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; runtime presentation lookup remains governed by CombatProjectileUnityPresentationRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry>.Failed(
                    "MissingFile",
                    "Projectile presentation binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry>.Failed(
                    "ReadFileFailed",
                    "Projectile presentation binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; runtime presentation lookup remains governed by CombatProjectileUnityPresentationRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> ImportBytes(
            byte[] bytes,
            string sourcePath = "")
        {
            if (bytes == null || bytes.Length == 0)
                return RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry>.Failed("MissingBytes", "Projectile presentation binary bytes are missing.");
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    RuntimeCatalogBinaryHeader header = RuntimeCatalogBinary.ReadHeader(reader);
                    if (string.Equals(header.SchemaVersion, "1", StringComparison.Ordinal))
                        return RuntimeCatalogBinary.Import(bytes, CatalogKind, "1", ReadLegacyCatalog, sourcePath);
                }
            }
            catch (Exception)
            {
                // The shared importer below returns the structured truncated/header failure.
            }
            return RuntimeCatalogBinary.Import(
                bytes,
                CatalogKind,
                CurrentSchemaVersion,
                ReadCatalog,
                sourcePath);
        }

#pragma warning disable CS0618 // Explicit v1 binary compatibility adapter.
        private static CombatProjectileUnityPresentationRegistry ReadLegacyCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var registry = new CombatProjectileUnityPresentationRegistry();
            for (int i = 0; i < header.EntryCount; i++)
            {
                var binding = new CombatProjectileUnityPresentationBinding(
                    reader.ReadInt32(),
                    ReadResourceKey(reader),
                    reader.ReadString());
                if (!registry.TryRegister(binding, out string failure))
                    throw new InvalidDataException("Invalid legacy projectile presentation binding at " + (sourcePath ?? string.Empty) + "#" + i + ": " + failure);
            }
            loadedEntryCount = registry.Count;
            return registry;
        }
#pragma warning restore CS0618

        private static CombatProjectileUnityPresentationRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var registry = new CombatProjectileUnityPresentationRegistry();
            for (int i = 0; i < header.EntryCount; i++)
            {
                int definitionId = reader.ReadInt32();
                string viewNamePrefix = reader.ReadString();
                int slotCount = reader.ReadInt32();
                if (slotCount < 0 || slotCount > 7) throw new InvalidDataException("Projectile presentation slot count is invalid: " + slotCount + ".");
                var slots = new List<CombatProjectileUnityPresentationSlot>(slotCount);
                for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                {
                    byte kindRaw = reader.ReadByte();
                    byte orientationRaw = 0;
                    ResourceKey resource = ReadResourceKey(reader);
                    orientationRaw = reader.ReadByte();
                    if (kindRaw > (byte)CombatProjectileUnityPresentationSlotKind.Expire || orientationRaw > (byte)CombatProjectileUnityPresentationOrientation.ImpactNormalOrWorldUp)
                        throw new InvalidDataException("Projectile presentation slot enum is invalid.");
                    slots.Add(new CombatProjectileUnityPresentationSlot(
                        (CombatProjectileUnityPresentationSlotKind)kindRaw,
                        resource,
                        (CombatProjectileUnityPresentationOrientation)orientationRaw,
                        new FixVector3(Fix64.FromRaw(reader.ReadInt64()), Fix64.FromRaw(reader.ReadInt64()), Fix64.FromRaw(reader.ReadInt64())),
                        reader.ReadInt32(),
                        reader.ReadInt32()));
                }
                var exit = new CombatProjectileUnityPresentationExitPolicy(
                    ReadExit(reader, false), ReadExit(reader, false), ReadExit(reader, false), ReadExit(reader, false), ReadExit(reader, true));
                var binding = new CombatProjectileUnityPresentationBindingV2(definitionId, viewNamePrefix, exit, slots.ToArray());
                if (!registry.TryRegister(binding, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid projectile presentation binary catalog entry at "
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

        private static ResourceKey ReadResourceKey(BinaryReader reader)
        {
            return new ResourceKey(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString());
        }

        private static CombatProjectileUnityPresentationExitMode ReadExit(BinaryReader reader, bool immediateAllowed)
        {
            byte value = reader.ReadByte();
            if (value > (byte)CombatProjectileUnityPresentationExitMode.Immediate
                || (!immediateAllowed && value == (byte)CombatProjectileUnityPresentationExitMode.Immediate))
                throw new InvalidDataException("Projectile presentation exit enum is invalid.");
            return (CombatProjectileUnityPresentationExitMode)value;
        }
    }
}
