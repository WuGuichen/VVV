using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public static class AbilityDefinitionBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.AbilityDefinitions;
        public const string CurrentSchemaVersion = AbilityDefinitionJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by AbilityDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry>.Failed(
                    "MissingFile",
                    "Ability definition binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry>.Failed(
                    "ReadFileFailed",
                    "Ability definition binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by AbilityDefinitionRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> ImportBytes(
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

        private static AbilityDefinitionRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var registry = new AbilityDefinitionRegistry();
            for (int i = 0; i < header.EntryCount; i++)
            {
                AbilityDefinitionConfig definition = ReadDefinition(reader);
                if (!registry.TryRegister(definition, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid ability binary catalog entry at "
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

        private static AbilityDefinitionConfig ReadDefinition(BinaryReader reader)
        {
            int id = reader.ReadInt32();
            string stable = reader.ReadString(), display = reader.ReadString();
            int intent = reader.ReadInt32(); string intentLabel = reader.ReadString();
            int slot = reader.ReadInt32(); string slotLabel = reader.ReadString();
            var input = new AbilityInputBindingConfig(intent, slot, intentLabel, slotLabel);
            long frames = reader.ReadInt64();
            int group = reader.ReadInt32(); string groupLabel = reader.ReadString();
            var cooldown = new AbilityCooldownConfig(frames, group, groupLabel);
            AbilityCostConfig[] costs = ReadCosts(reader);
            int target = reader.ReadInt32(); string targetLabel = reader.ReadString();
            var action = new MxFramework.CharacterAction.CharacterActionId(reader.ReadInt32());
            int actionIntent = reader.ReadInt32(); string actionIntentLabel = reader.ReadString();
            var binding = new AbilityActionBindingConfig(action, actionIntent, actionIntentLabel);
            AbilityDamageConfig damage = ReadDamage(reader);
            int presentation = reader.ReadInt32(); string presentationLabel = reader.ReadString();
            int timeline = reader.ReadInt32(); string timelineLabel = reader.ReadString();
            return new AbilityDefinitionConfig(id, stable, display, input, cooldown, costs,
                target, binding, damage, presentation, timeline,
                targetLabel, presentationLabel, timelineLabel);
        }

        private static AbilityDamageConfig ReadDamage(BinaryReader reader)
        {
            // JSON uses the canonical empty damage value when the optional object is absent.
            // Preserve the same empty identity labels in the legacy content fingerprint.
            if (!reader.ReadBoolean()) return new AbilityDamageConfig(0, 0, 0, Array.Empty<int>(), 0);
            int type = reader.ReadInt32(); string typeLabel = reader.ReadString();
            int damage = reader.ReadInt32(), impact = reader.ReadInt32();
            int count = reader.ReadInt32();
            if (count < 0) throw new InvalidDataException("Negative ability tag count.");
            var tags = new int[count];
            for (int i = 0; i < count; i++) tags[i] = reader.ReadInt32();
            string[] labels = ReadStringArray(reader);
            int category = reader.ReadInt32(); string categoryLabel = reader.ReadString();
            int launch = reader.ReadInt32();
            int scaleCount = reader.ReadInt32();
            if (scaleCount < 0 || scaleCount > 256)
                throw new InvalidDataException("Invalid ability source attribute scale count.");
            var scales = new AbilityDamageAttributeScale[scaleCount];
            for (int i = 0; i < scaleCount; i++)
            {
                int attributeId = reader.ReadInt32();
                float scale = reader.ReadSingle();
                if (attributeId <= 0 || scale < 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                    throw new InvalidDataException("Invalid ability source attribute scale.");
                scales[i] = new AbilityDamageAttributeScale(attributeId, scale);
            }
            return new AbilityDamageConfig(
                type,
                damage,
                impact,
                tags,
                category,
                launch,
                typeLabel,
                categoryLabel,
                labels,
                scales);
        }

        private static AbilityCostConfig[] ReadCosts(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0)
                return Array.Empty<AbilityCostConfig>();

            var costs = new AbilityCostConfig[count];
            for (int i = 0; i < count; i++)
                costs[i] = new AbilityCostConfig(reader.ReadInt32(), reader.ReadInt32());
            return costs;
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
    }

    public static class AbilityEffectTimelineBinaryImporter
    {
        public const string CatalogKind = RuntimeCatalogBinaryKinds.AbilityEffectTimelines;
        public const string CurrentSchemaVersion = AbilityEffectTimelineJsonImporter.CurrentSchemaVersion;

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by AbilityEffectTimelineRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<AbilityEffectTimelineRegistry> ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return RuntimeCatalogBinaryLoadResult<AbilityEffectTimelineRegistry>.Failed(
                    "MissingFile",
                    "Ability effect timeline binary catalog file is missing: " + (path ?? string.Empty));
            }

            try
            {
                return ImportBytes(File.ReadAllBytes(path), path);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<AbilityEffectTimelineRegistry>.Failed(
                    "ReadFileFailed",
                    "Ability effect timeline binary catalog file could not be read: " + ex.Message);
            }
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registry lookup remains governed by AbilityEffectTimelineRegistry.</remarks>
        public static RuntimeCatalogBinaryLoadResult<AbilityEffectTimelineRegistry> ImportBytes(
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

        private static AbilityEffectTimelineRegistry ReadCatalog(
            BinaryReader reader,
            RuntimeCatalogBinaryHeader header,
            string sourcePath,
            out int loadedEntryCount)
        {
            var definitions = new List<AbilityEffectTimelineDefinition>(header.EntryCount);
            for (int i = 0; i < header.EntryCount; i++)
                definitions.Add(ReadTimeline(reader));

            AbilityEffectTimelineValidationResult validation =
                AbilityEffectTimelineValidator.ValidateRegistry(definitions);
            if (!validation.IsValid)
            {
                AbilityEffectTimelineValidationError error = validation.Errors[0];
                throw new InvalidOperationException(
                    "Invalid ability effect timeline binary catalog at "
                    + (sourcePath ?? string.Empty)
                    + ": "
                    + error.Code
                    + " "
                    + error.FieldPath
                    + " - "
                    + error.Message);
            }

            var registry = new AbilityEffectTimelineRegistry();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (!registry.TryRegister(definitions[i], out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid ability effect timeline binary catalog at "
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

        private static AbilityEffectTimelineDefinition ReadTimeline(BinaryReader reader)
        {
            int timelineId = reader.ReadInt32();
            string label = reader.ReadString();
            int duration = reader.ReadInt32();
            int count = reader.ReadInt32();
            if (count < 0) throw new InvalidDataException("Negative timeline event count.");
            var events = new AbilityEffectTimelineEvent[count];
            for (int i = 0; i < count; i++) events[i] = ReadEvent(reader);
            return new AbilityEffectTimelineDefinition(timelineId, duration, events, label);
        }

        private static AbilityEffectTimelineEvent ReadEvent(BinaryReader reader)
        {
            int id = reader.ReadInt32(); string label = reader.ReadString();
            var track = ReadEnum<AbilityEffectTimelineTrackKind>(reader, "trackKind");
            var kind = ReadEnum<AbilityEffectTimelineEventKind>(reader, "eventKind");
            int frame = reader.ReadInt32(), end = reader.ReadInt32();
            var shape = ReadEnum<AbilityEffectTimelineShapeKind>(reader, "shapeKind");
            var anchor = ReadEnum<AbilityEffectTimelineAnchorMode>(reader, "anchorMode");
            FixVector3 offset = ReadVector(reader), pointB = ReadVector(reader);
            Fix64 radius = Fix64.FromRaw(reader.ReadInt64());
            int layers = reader.ReadInt32();
            int target = reader.ReadInt32(); string targetLabel = reader.ReadString();
            int request = reader.ReadInt32(); string requestLabel = reader.ReadString();
            int effect = reader.ReadInt32();
            int presentation = reader.ReadInt32(); string presentationLabel = reader.ReadString();
            bool owner = reader.ReadBoolean(), source = reader.ReadBoolean();
            return new AbilityEffectTimelineEvent(id, track, kind, frame, end, shape, anchor,
                offset, pointB, radius, layers, target, request, effect, presentation, owner, source,
                label, targetLabel, requestLabel, presentationLabel);
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
