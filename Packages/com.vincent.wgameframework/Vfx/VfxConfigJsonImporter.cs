using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;

namespace MxFramework.Vfx
{
    public sealed class VfxDefinitionConfig
    {
        public VfxDefinitionConfig(
            int definitionId,
            string stableId,
            ResourceKey prefabResource,
            int defaultLifetimeFrames,
            string definitionHash)
        {
            DefinitionId = definitionId < 0 ? 0 : definitionId;
            StableId = stableId ?? string.Empty;
            PrefabResource = prefabResource;
            DefaultLifetimeFrames = defaultLifetimeFrames < 0 ? 0 : defaultLifetimeFrames;
            DefinitionHash = definitionHash ?? string.Empty;
        }

        public int DefinitionId { get; }
        public string StableId { get; }
        public ResourceKey PrefabResource { get; }
        public int DefaultLifetimeFrames { get; }
        public string DefinitionHash { get; }

        public VfxDefinition ToDefinition()
        {
            return new VfxDefinition(
                new VfxDefinitionId(DefinitionId),
                StableId,
                PrefabResource,
                DefaultLifetimeFrames,
                DefinitionHash);
        }
    }

    public sealed class VfxPresentationProfileConfig
    {
        public VfxPresentationProfileConfig(
            string presentationProfileId,
            int definitionId,
            string anchorStrategy,
            int lifetimeFramesOverride)
        {
            PresentationProfileId = presentationProfileId ?? string.Empty;
            DefinitionId = definitionId < 0 ? 0 : definitionId;
            AnchorStrategy = anchorStrategy ?? string.Empty;
            LifetimeFramesOverride = lifetimeFramesOverride < 0 ? 0 : lifetimeFramesOverride;
        }

        public string PresentationProfileId { get; }
        public int DefinitionId { get; }
        public string AnchorStrategy { get; }
        public int LifetimeFramesOverride { get; }
    }

    public sealed class VfxTriggerMapConfig
    {
        public VfxTriggerMapConfig(
            int sourceDomain,
            int triggerId,
            string presentationProfileId,
            int definitionId,
            int lifetimeFramesOverride)
        {
            SourceDomain = sourceDomain < 0 ? 0 : sourceDomain;
            TriggerId = triggerId < 0 ? 0 : triggerId;
            PresentationProfileId = presentationProfileId ?? string.Empty;
            DefinitionId = definitionId < 0 ? 0 : definitionId;
            LifetimeFramesOverride = lifetimeFramesOverride < 0 ? 0 : lifetimeFramesOverride;
        }

        public int SourceDomain { get; }
        public int TriggerId { get; }
        public string PresentationProfileId { get; }
        public int DefinitionId { get; }
        public int LifetimeFramesOverride { get; }

        public VfxTriggerMapping ToMapping()
        {
            return new VfxTriggerMapping(
                SourceDomain,
                TriggerId,
                new VfxDefinitionId(DefinitionId),
                LifetimeFramesOverride);
        }
    }

    public static class VfxConfigJsonImporter
    {
        public const string CurrentSchemaVersion = "1";
        public const string TriggerMapSchemaVersion = "2";

        public static VfxDefinitionRegistry ImportDefinitionFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new VfxDefinitionRegistry();

            return ImportDefinitions(File.ReadAllText(path), path);
        }

        public static VfxDefinitionRegistry ImportDefinitions(string json, string sourcePath = "")
        {
            var registry = new VfxDefinitionRegistry();
            if (string.IsNullOrWhiteSpace(json))
                return registry;

            JObject root = JObject.Parse(json);
            JArray definitions = root["definitions"] as JArray;
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                if (!(definitions[i] is JObject fields))
                    continue;

                VfxDefinition definition = ReadDefinition(fields).ToDefinition();
                if (!registry.TryRegister(definition, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid VFX definition at " + (sourcePath ?? string.Empty) + "#" + i + ": " + failure);
                }
            }

            return registry;
        }

        public static VfxTriggerMapResolver ImportTriggerMapFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new VfxTriggerMapResolver();

            return ImportTriggerMaps(File.ReadAllText(path), path);
        }

        public static VfxTriggerMapResolver ImportTriggerMaps(string json, string sourcePath = "")
        {
            var resolver = new VfxTriggerMapResolver();
            if (string.IsNullOrWhiteSpace(json))
                return resolver;

            JObject root = JObject.Parse(json);
            JArray mappings = root["mappings"] as JArray;
            ValidateTriggerMapSchema(root);
            for (int i = 0; mappings != null && i < mappings.Count; i++)
            {
                if (!(mappings[i] is JObject fields))
                    continue;

                VfxTriggerMapConfig config = ReadTriggerMap(fields);
                if (config.SourceDomain <= 0 || config.TriggerId <= 0 || config.DefinitionId <= 0)
                {
                    throw new InvalidOperationException(
                        "Invalid VFX trigger mapping at " + (sourcePath ?? string.Empty) + "#" + i + ".");
                }

                resolver.Add(config.ToMapping());
            }

            return resolver;
        }

        public static VfxPresentationProfileConfig[] ReadPresentationProfiles(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<VfxPresentationProfileConfig>();

            JObject root = JObject.Parse(json);
            JArray profiles = root["presentationProfiles"] as JArray;
            ValidateTriggerMapSchema(root);
            if (profiles == null || profiles.Count == 0)
                return Array.Empty<VfxPresentationProfileConfig>();

            var output = new List<VfxPresentationProfileConfig>(profiles.Count);
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] is JObject fields)
                    output.Add(ReadPresentationProfile(fields));
            }

            return output.ToArray();
        }

        private static void ValidateTriggerMapSchema(JObject root)
        {
            JToken schema = root["schemaVersion"];
            if (schema == null || schema.Type != JTokenType.String
                || schema.Value<string>() != TriggerMapSchemaVersion)
                throw new InvalidDataException("Unsupported VFX trigger map schema; expected " + TriggerMapSchemaVersion + ". Regenerate legacy content before import.");
        }

        private static VfxDefinitionConfig ReadDefinition(JObject fields)
        {
            return new VfxDefinitionConfig(
                ReadInt(fields, "definitionId"),
                ReadString(fields, "stableId"),
                ReadResourceKey(fields["prefabResource"] as JObject),
                ReadInt(fields, "defaultLifetimeFrames"),
                ReadString(fields, "definitionHash"));
        }

        private static VfxPresentationProfileConfig ReadPresentationProfile(JObject fields)
        {
            return new VfxPresentationProfileConfig(
                ReadString(fields, "presentationProfileId"),
                ReadInt(fields, "definitionId"),
                ReadString(fields, "anchorStrategy"),
                ReadInt(fields, "lifetimeFramesOverride"));
        }

        private static VfxTriggerMapConfig ReadTriggerMap(JObject fields)
        {
            return new VfxTriggerMapConfig(
                ReadSourceDomain(fields, "sourceDomain", "sourceDomainId"),
                ReadInt(fields, "triggerId"),
                ReadString(fields, "presentationProfileId"),
                ReadInt(fields, "definitionId"),
                ReadInt(fields, "lifetimeFramesOverride"));
        }

        private static ResourceKey ReadResourceKey(JObject fields)
        {
            if (fields == null)
                return default;

            return new ResourceKey(
                ReadString(fields, "id"),
                ReadString(fields, "typeId"),
                ReadString(fields, "variant"),
                ReadString(fields, "packageId"));
        }

        private static int ReadSourceDomain(JObject fields, string stringName, string intName)
        {
            int explicitValue = ReadInt(fields, intName);
            if (explicitValue > 0)
                return explicitValue;

            string value = ReadString(fields, stringName);
            if (string.Equals(value, "CombatHit", StringComparison.OrdinalIgnoreCase))
                return VfxSourceDomains.CombatHit;
            if (string.Equals(value, "CombatAction", StringComparison.OrdinalIgnoreCase))
                return VfxSourceDomains.CombatAction;
            if (string.Equals(value, "ActionPresentation", StringComparison.OrdinalIgnoreCase))
                return VfxSourceDomains.ActionPresentation;
            if (string.Equals(value, "Gameplay", StringComparison.OrdinalIgnoreCase))
                return VfxSourceDomains.Gameplay;
            if (string.Equals(value, "Manual", StringComparison.OrdinalIgnoreCase))
                return VfxSourceDomains.Manual;

            return 0;
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback ?? string.Empty;

            JToken token = fields[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return 0;

            JToken token = fields[name];
            return token == null ? 0 : token.Value<int>();
        }
    }
}
