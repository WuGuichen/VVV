using System;
using System.Globalization;
using System.IO;
using MxFramework.Core.Math;
using Newtonsoft.Json.Linq;

namespace MxFramework.Combat.Projectiles
{
    public sealed class CombatProjectileSpawnProfileConfig
    {
        public CombatProjectileSpawnProfileConfig(
            int profileId,
            string stableId,
            int projectileDefinitionId,
            string projectileStableId,
            CombatProjectileSpawnOriginConfigData origin,
            CombatProjectileAimConfigData aim)
        {
            ProfileId = profileId;
            StableId = stableId ?? string.Empty;
            ProjectileDefinitionId = projectileDefinitionId;
            ProjectileStableId = projectileStableId ?? string.Empty;
            Origin = origin ?? new CombatProjectileSpawnOriginConfigData();
            Aim = aim ?? new CombatProjectileAimConfigData();
        }

        public int ProfileId { get; }
        public string StableId { get; }
        public int ProjectileDefinitionId { get; }
        public string ProjectileStableId { get; }
        public CombatProjectileSpawnOriginConfigData Origin { get; }
        public CombatProjectileAimConfigData Aim { get; }

        public CombatProjectileSpawnProfile ToProfile()
        {
            return new CombatProjectileSpawnProfile(
                ProfileId,
                StableId,
                ProjectileDefinitionId,
                ProjectileStableId,
                new CombatProjectileSpawnOriginConfig(
                    Origin.Kind,
                    Origin.SocketId,
                    ProjectileConfigMath.ToFixVector3(Origin.LocalOffsetX, Origin.LocalOffsetY, Origin.LocalOffsetZ),
                    ProjectileConfigMath.ToFixVector3(Origin.FallbackRootOffsetX, Origin.FallbackRootOffsetY, Origin.FallbackRootOffsetZ),
                    Origin.SocketLabel),
                new CombatProjectileAimConfig(
                    Aim.Kind,
                    ProjectileConfigMath.ToFix64(Aim.TargetHeightOffset),
                    Aim.FallbackKind,
                    ProjectileConfigMath.ToFix64(Aim.MaxRange)));
        }
    }

    public sealed class CombatProjectileSpawnOriginConfigData
    {
        public CombatProjectileSpawnOriginConfigData(
            CombatProjectileSpawnOriginKind kind = CombatProjectileSpawnOriginKind.Socket,
            int socketId = 0,
            string socketLabel = "",
            float localOffsetX = 0f,
            float localOffsetY = 0f,
            float localOffsetZ = 0f,
            float fallbackRootOffsetX = 0f,
            float fallbackRootOffsetY = 1.15f,
            float fallbackRootOffsetZ = 0.65f)
        {
            Kind = kind;
            SocketId = socketId;
            SocketLabel = socketLabel ?? string.Empty;
            LocalOffsetX = localOffsetX;
            LocalOffsetY = localOffsetY;
            LocalOffsetZ = localOffsetZ;
            FallbackRootOffsetX = fallbackRootOffsetX;
            FallbackRootOffsetY = fallbackRootOffsetY;
            FallbackRootOffsetZ = fallbackRootOffsetZ;
        }

        public CombatProjectileSpawnOriginKind Kind { get; }
        public int SocketId { get; }
        public string SocketLabel { get; }
        public float LocalOffsetX { get; }
        public float LocalOffsetY { get; }
        public float LocalOffsetZ { get; }
        public float FallbackRootOffsetX { get; }
        public float FallbackRootOffsetY { get; }
        public float FallbackRootOffsetZ { get; }
    }

    public sealed class CombatProjectileAimConfigData
    {
        public CombatProjectileAimConfigData(
            CombatProjectileAimKind kind = CombatProjectileAimKind.CasterForwardRange,
            float targetHeightOffset = 0f,
            CombatProjectileAimKind fallbackKind = CombatProjectileAimKind.CasterForwardRange,
            float maxRange = 0f)
        {
            Kind = kind;
            TargetHeightOffset = targetHeightOffset;
            FallbackKind = fallbackKind;
            MaxRange = maxRange;
        }

        public CombatProjectileAimKind Kind { get; }
        public float TargetHeightOffset { get; }
        public CombatProjectileAimKind FallbackKind { get; }
        public float MaxRange { get; }
    }

    public static class CombatProjectileSpawnProfileConfigJsonImporter
    {
        public const string CurrentSchemaVersion = "2";

        public static CombatProjectileSpawnProfileRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new CombatProjectileSpawnProfileRegistry();

            return Import(File.ReadAllText(path), path);
        }

        public static CombatProjectileSpawnProfileRegistry Import(string json, string sourcePath = "")
        {
            var registry = new CombatProjectileSpawnProfileRegistry();
            if (string.IsNullOrWhiteSpace(json))
                return registry;

            JObject root = JObject.Parse(json);
            JArray profiles = root["profiles"] as JArray;
            for (int i = 0; profiles != null && i < profiles.Count; i++)
            {
                if (!(profiles[i] is JObject fields))
                    continue;

                CombatProjectileSpawnProfile profile;
                try
                {
                    profile = ReadProfile(fields).ToProfile();
                }
                catch (Exception ex) when (!(ex is InvalidOperationException))
                {
                    throw new InvalidOperationException(
                        "Invalid projectile spawn profile at " + (sourcePath ?? string.Empty) + "#" + i + ": " + ex.Message,
                        ex);
                }

                if (!registry.TryRegister(profile, out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid projectile spawn profile at " + (sourcePath ?? string.Empty) + "#" + i + ": " + failure);
                }
            }

            return registry;
        }

        private static CombatProjectileSpawnProfileConfig ReadProfile(JObject fields)
        {
            return new CombatProjectileSpawnProfileConfig(
                ReadId(fields, "profileId"),
                ReadString(fields, "stableId"),
                ReadId(fields, "projectileDefinitionId"),
                ReadString(fields, "projectileStableId"),
                ReadOrigin(fields["origin"] as JObject),
                ReadAim(fields["aim"] as JObject));
        }

        private static CombatProjectileSpawnOriginConfigData ReadOrigin(JObject fields)
        {
            if (fields == null)
                return new CombatProjectileSpawnOriginConfigData();

            JObject localOffset = fields["localOffset"] as JObject;
            JObject fallbackRootOffset = fields["fallbackRootOffset"] as JObject;
            return new CombatProjectileSpawnOriginConfigData(
                ReadOriginKind(fields, "kind"),
                ReadId(fields, "socketId"),
                ReadString(fields, "socketLabel"),
                ReadFloat(localOffset, "x"),
                ReadFloat(localOffset, "y"),
                ReadFloat(localOffset, "z"),
                ReadFloat(fallbackRootOffset, "x"),
                ReadFloat(fallbackRootOffset, "y", 1.15f),
                ReadFloat(fallbackRootOffset, "z", 0.65f));
        }

        private static CombatProjectileAimConfigData ReadAim(JObject fields)
        {
            if (fields == null)
                return new CombatProjectileAimConfigData();

            return new CombatProjectileAimConfigData(
                ReadAimKind(fields, "kind"),
                ReadFloat(fields, "targetHeightOffset"),
                ReadAimKind(fields, "fallbackKind"),
                ReadFloat(fields, "maxRange"));
        }

        private static CombatProjectileSpawnOriginKind ReadOriginKind(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatProjectileSpawnOriginKind.Socket.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatProjectileSpawnOriginKind.Socket;

            if (Enum.TryParse(value, ignoreCase: false, out CombatProjectileSpawnOriginKind kind)
                && Enum.IsDefined(typeof(CombatProjectileSpawnOriginKind), kind))
            {
                return kind;
            }

            throw new ArgumentException("Unsupported projectile spawn origin kind: " + value + ".");
        }

        private static CombatProjectileAimKind ReadAimKind(JObject fields, string name)
        {
            string value = ReadString(fields, name, CombatProjectileAimKind.CasterForwardRange.ToString());
            if (string.IsNullOrWhiteSpace(value))
                return CombatProjectileAimKind.CasterForwardRange;

            if (Enum.TryParse(value, ignoreCase: false, out CombatProjectileAimKind kind)
                && Enum.IsDefined(typeof(CombatProjectileAimKind), kind))
            {
                return kind;
            }

            throw new ArgumentException("Unsupported projectile aim kind: " + value + ".");
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback ?? string.Empty;

            JToken token = fields[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static float ReadFloat(JObject fields, string name, float fallback = 0f)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.String
                && float.TryParse(token.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return token.Value<float>();
        }

        private static int ReadId(JObject fields, string name)
        {
            JToken token = fields?[name];
            if (token == null || token.Type != JTokenType.Integer)
                throw new ArgumentException("Projectile spawn profile identity must be an authored integer: " + name + ".");
            int value = token.Value<int>();
            if (value <= 0)
                throw new ArgumentException("Projectile spawn profile identity must be positive: " + name + ".");
            return value;
        }
    }
}
