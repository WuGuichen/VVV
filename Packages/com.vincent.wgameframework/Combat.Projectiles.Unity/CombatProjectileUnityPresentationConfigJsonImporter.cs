using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Core.Math;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;

namespace MxFramework.Combat.Projectiles.Unity
{
    /// <summary>Imports projectile Presentation Binding v2 JSON and the explicit v1 compatibility shape.</summary>
    public static class CombatProjectileUnityPresentationConfigJsonImporter
    {
        public const string CurrentSchemaVersion = "2";

        /// <remarks>LowFreqAlloc during startup/catalog load.</remarks>
        public static CombatProjectileUnityPresentationRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new CombatProjectileUnityPresentationRegistry();
            return Import(File.ReadAllText(path), path);
        }

        /// <remarks>LowFreqAlloc during startup/catalog load; registration is atomic through a temporary registry.</remarks>
        public static CombatProjectileUnityPresentationRegistry Import(string json, string sourcePath = "")
        {
            try
            {
                return ImportValidated(json, sourcePath);
            }
            catch (Exception exception) when (!(exception is InvalidDataException))
            {
                throw new InvalidDataException("Invalid projectile presentation JSON at " + (sourcePath ?? string.Empty) + ".", exception);
            }
        }

        private static CombatProjectileUnityPresentationRegistry ImportValidated(string json, string sourcePath)
        {
            var registry = new CombatProjectileUnityPresentationRegistry();
            if (string.IsNullOrWhiteSpace(json)) return registry;
            JObject root = JObject.Parse(json);
            string schema = ReadString(root, "schemaVersion", "1");
            if (!string.Equals(schema, "1", StringComparison.Ordinal) && !string.Equals(schema, CurrentSchemaVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Unsupported projectile presentation schema: " + schema + ".");
            JArray bindings = root["bindings"] as JArray;
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                if (!(bindings[i] is JObject fields)) throw new InvalidDataException("Projectile presentation binding entry must be an object.");
                CombatProjectileUnityPresentationBindingV2 binding = string.Equals(schema, "1", StringComparison.Ordinal)
                    ? ReadLegacyBinding(fields)
                    : ReadBindingV2(fields);
                if (!registry.TryRegister(binding, out string failure))
                    throw new InvalidDataException("Invalid projectile presentation binding at " + (sourcePath ?? string.Empty) + "#" + i + ": " + failure);
            }
            return registry;
        }

        private static CombatProjectileUnityPresentationBindingV2 ReadLegacyBinding(JObject fields)
        {
            ResourceKey resource = ReadResourceKey(fields["prefabResource"] as JObject);
            return new CombatProjectileUnityPresentationBindingV2(
                ReadInt(fields, "projectileDefinitionId"),
                ReadString(fields, "viewNamePrefix", CombatProjectileUnityPresentationBindingV2.DefaultViewNamePrefix),
                CombatProjectileUnityPresentationExitPolicy.Default,
                new CombatProjectileUnityPresentationSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, resource, CombatProjectileUnityPresentationOrientation.ProjectileForward, FixVector3.Zero, 0));
        }

        private static CombatProjectileUnityPresentationBindingV2 ReadBindingV2(JObject fields)
        {
            var slots = new List<CombatProjectileUnityPresentationSlot>(7);
            JObject slotFields = fields["slots"] as JObject;
            AddSlot(slots, slotFields, "spawn", CombatProjectileUnityPresentationSlotKind.Spawn);
            AddSlot(slots, slotFields, "flightBody", CombatProjectileUnityPresentationSlotKind.FlightBody);
            AddSlot(slots, slotFields, "trail", CombatProjectileUnityPresentationSlotKind.Trail);
            AddSlot(slots, slotFields, "directImpact", CombatProjectileUnityPresentationSlotKind.DirectImpact);
            AddSlot(slots, slotFields, "areaImpact", CombatProjectileUnityPresentationSlotKind.AreaImpact);
            AddSlot(slots, slotFields, "blockedImpact", CombatProjectileUnityPresentationSlotKind.BlockedImpact);
            AddSlot(slots, slotFields, "expire", CombatProjectileUnityPresentationSlotKind.Expire);
            JObject exit = fields["exitPolicy"] as JObject;
            return new CombatProjectileUnityPresentationBindingV2(
                ReadInt(fields, "projectileDefinitionId"),
                ReadString(fields, "viewNamePrefix", CombatProjectileUnityPresentationBindingV2.DefaultViewNamePrefix),
                new CombatProjectileUnityPresentationExitPolicy(
                    ParseExit(ReadString(exit, "acceptedImpact", "Complete"), false),
                    ParseExit(ReadString(exit, "blockedImpact", "Dissipate"), false),
                    ParseExit(ReadString(exit, "expiration", "Complete"), false),
                    ParseExit(ReadString(exit, "externalRemoval", "Dissipate"), false),
                    ParseExit(ReadString(exit, "runtimeReset", "Immediate"), true)),
                slots.ToArray());
        }

        private static void AddSlot(List<CombatProjectileUnityPresentationSlot> output, JObject fields, string name, CombatProjectileUnityPresentationSlotKind kind)
        {
            if (!(fields?[name] is JObject slot)) return;
            JObject offset = slot["localOffset"] as JObject;
            output.Add(new CombatProjectileUnityPresentationSlot(
                kind,
                ReadResourceKey(slot["resource"] as JObject),
                ParseOrientation(ReadString(slot, "orientation", DefaultOrientation(kind))),
                new FixVector3(ReadFix(offset, "x"), ReadFix(offset, "y"), ReadFix(offset, "z")),
                ReadInt(slot, "lifetimeFramesOverride"),
                ReadInt(slot, "vfxDefinitionId")));
        }

        private static string DefaultOrientation(CombatProjectileUnityPresentationSlotKind kind)
        {
            if (kind == CombatProjectileUnityPresentationSlotKind.DirectImpact || kind == CombatProjectileUnityPresentationSlotKind.BlockedImpact) return "SurfaceNormal";
            if (kind == CombatProjectileUnityPresentationSlotKind.AreaImpact) return "ImpactNormalOrWorldUp";
            return "ProjectileForward";
        }

        private static CombatProjectileUnityPresentationOrientation ParseOrientation(string value)
        {
            if (Enum.TryParse(value, false, out CombatProjectileUnityPresentationOrientation result) && Enum.IsDefined(typeof(CombatProjectileUnityPresentationOrientation), result)) return result;
            throw new InvalidDataException("Unknown projectile presentation orientation: " + (value ?? string.Empty));
        }

        private static CombatProjectileUnityPresentationExitMode ParseExit(string value, bool immediateAllowed)
        {
            if (!Enum.TryParse(value, false, out CombatProjectileUnityPresentationExitMode result) || !Enum.IsDefined(typeof(CombatProjectileUnityPresentationExitMode), result) || (result == CombatProjectileUnityPresentationExitMode.Immediate && !immediateAllowed))
                throw new InvalidDataException("Unknown projectile presentation exit mode: " + (value ?? string.Empty));
            return result;
        }

        private static ResourceKey ReadResourceKey(JObject fields) => fields == null ? default : new ResourceKey(ReadString(fields, "id"), ReadString(fields, "typeId"), ReadString(fields, "variant"), ReadString(fields, "packageId"));
        private static string ReadString(JObject fields, string name, string fallback = "") => fields?[name]?.Value<string>() ?? fallback ?? string.Empty;
        private static int ReadInt(JObject fields, string name, int fallback = 0) => fields?[name]?.Value<int>() ?? fallback;
        private static Fix64 ReadFix(JObject fields, string name) => Fix64.FromRaw(checked((long)Math.Round((fields?[name]?.Value<double>() ?? 0d) * Fix64.Scale, MidpointRounding.AwayFromZero)));
    }
}
