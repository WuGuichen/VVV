using System;
using System.IO;
using MxFramework.Core.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestRuneTurretIds
    {
        public const int MechanicSourceId = 501014;
        public const string SourceStableId = "mechanic.character_test.rune_turret.turret";
        public const string TuningStableId = "mechanic_tuning.character_test.rune_turret";
        public const int DeployRangeMeters = 8;
        public const int FallbackForwardMeters = 5;
        public const int WarmupFrames = 18;
        public const int DurationFrames = 360;
        public const int ScanRadiusCentimeters = 1000;
        public const int ScanIntervalFrames = 12;
        public const int FireIntervalFrames = 45;
        public const int MaxShots = 8;
        public const int MuzzleHeightCentimeters = 100;
        public const int DeployVfxDefinitionId = 12045;
        public const int DeployVfxLifetimeFrames = 30;
        public const int IdleVfxDefinitionId = 12046;
        public const int IdleVfxLifetimeFrames = 90;
        public const int FireVfxDefinitionId = 12047;
        public const int FireVfxLifetimeFrames = 18;
        public const int ExpireVfxDefinitionId = 12050;
        public const int ExpireVfxLifetimeFrames = 24;
    }

    internal sealed class CharacterTestRuneTurretTuning
    {
        public const string DefaultGeneratedTuningsPath =
            "Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json";

        public CharacterTestRuneTurretTuning(
            string stableId,
            string abilityStableId,
            string sourceStableId,
            int deployRangeMeters,
            int fallbackForwardMeters,
            int warmupFrames,
            int durationFrames,
            int scanRadiusCentimeters,
            int scanIntervalFrames,
            int fireIntervalFrames,
            int maxShots,
            int muzzleHeightCentimeters,
            int deployVfxDefinitionId,
            int idleVfxDefinitionId,
            int fireVfxDefinitionId,
            int expireVfxDefinitionId)
        {
            StableId = string.IsNullOrWhiteSpace(stableId)
                ? CharacterTestRuneTurretIds.TuningStableId
                : stableId;
            AbilityStableId = string.IsNullOrWhiteSpace(abilityStableId)
                ? CharacterTestAbilityIds.RuneTurretAbilityStableId
                : abilityStableId;
            SourceStableId = string.IsNullOrWhiteSpace(sourceStableId)
                ? CharacterTestRuneTurretIds.SourceStableId
                : sourceStableId;
            DeployRangeMeters = deployRangeMeters <= 0
                ? CharacterTestRuneTurretIds.DeployRangeMeters
                : deployRangeMeters;
            FallbackForwardMeters = fallbackForwardMeters <= 0
                ? CharacterTestRuneTurretIds.FallbackForwardMeters
                : fallbackForwardMeters;
            WarmupFrames = warmupFrames < 0 ? CharacterTestRuneTurretIds.WarmupFrames : warmupFrames;
            DurationFrames = durationFrames <= 0
                ? CharacterTestRuneTurretIds.DurationFrames
                : durationFrames;
            ScanRadiusCentimeters = scanRadiusCentimeters <= 0
                ? CharacterTestRuneTurretIds.ScanRadiusCentimeters
                : scanRadiusCentimeters;
            ScanIntervalFrames = scanIntervalFrames <= 0
                ? CharacterTestRuneTurretIds.ScanIntervalFrames
                : scanIntervalFrames;
            FireIntervalFrames = fireIntervalFrames <= 0
                ? CharacterTestRuneTurretIds.FireIntervalFrames
                : fireIntervalFrames;
            MaxShots = maxShots <= 0 ? CharacterTestRuneTurretIds.MaxShots : maxShots;
            MuzzleHeightCentimeters = muzzleHeightCentimeters <= 0
                ? CharacterTestRuneTurretIds.MuzzleHeightCentimeters
                : muzzleHeightCentimeters;
            DeployVfxDefinitionId = deployVfxDefinitionId <= 0
                ? CharacterTestRuneTurretIds.DeployVfxDefinitionId
                : deployVfxDefinitionId;
            IdleVfxDefinitionId = Math.Max(0, idleVfxDefinitionId);
            FireVfxDefinitionId = fireVfxDefinitionId <= 0
                ? CharacterTestRuneTurretIds.FireVfxDefinitionId
                : fireVfxDefinitionId;
            ExpireVfxDefinitionId = Math.Max(0, expireVfxDefinitionId);
        }

        public string StableId { get; }
        public string AbilityStableId { get; }
        public string SourceStableId { get; }
        public int DeployRangeMeters { get; }
        public int FallbackForwardMeters { get; }
        public int WarmupFrames { get; }
        public int DurationFrames { get; }
        public int ScanRadiusCentimeters { get; }
        public int ScanIntervalFrames { get; }
        public int FireIntervalFrames { get; }
        public int MaxShots { get; }
        public int MuzzleHeightCentimeters { get; }
        public int DeployVfxDefinitionId { get; }
        public int IdleVfxDefinitionId { get; }
        public int FireVfxDefinitionId { get; }
        public int ExpireVfxDefinitionId { get; }
        public Fix64 ScanRadius => Fix64.FromRatio(ScanRadiusCentimeters, 100);
        public Fix64 MuzzleHeight => Fix64.FromRatio(MuzzleHeightCentimeters, 100);

        public static CharacterTestRuneTurretTuning Default =>
            new CharacterTestRuneTurretTuning(
                CharacterTestRuneTurretIds.TuningStableId,
                CharacterTestAbilityIds.RuneTurretAbilityStableId,
                CharacterTestRuneTurretIds.SourceStableId,
                CharacterTestRuneTurretIds.DeployRangeMeters,
                CharacterTestRuneTurretIds.FallbackForwardMeters,
                CharacterTestRuneTurretIds.WarmupFrames,
                CharacterTestRuneTurretIds.DurationFrames,
                CharacterTestRuneTurretIds.ScanRadiusCentimeters,
                CharacterTestRuneTurretIds.ScanIntervalFrames,
                CharacterTestRuneTurretIds.FireIntervalFrames,
                CharacterTestRuneTurretIds.MaxShots,
                CharacterTestRuneTurretIds.MuzzleHeightCentimeters,
                CharacterTestRuneTurretIds.DeployVfxDefinitionId,
                CharacterTestRuneTurretIds.IdleVfxDefinitionId,
                CharacterTestRuneTurretIds.FireVfxDefinitionId,
                CharacterTestRuneTurretIds.ExpireVfxDefinitionId);
    }

    internal static class CharacterTestRuneTurretTuningSource
    {
        public static CharacterTestRuneTurretTuning LoadDefault()
        {
            CharacterTestRuntimeCatalogPathCandidate[] candidates =
                CharacterTestRuntimeCatalogPathResolver.ResolveCandidates(
                    CharacterTestRuneTurretTuning.DefaultGeneratedTuningsPath,
                    CharacterTestRuntimeCatalogPathResolverContext.CreateDefault());
            for (int i = 0; i < candidates.Length; i++)
            {
                if (TryLoad(candidates[i].JsonPath, out CharacterTestRuneTurretTuning tuning))
                    return tuning;
            }

            return CharacterTestRuneTurretTuning.Default;
        }

        public static CharacterTestRuneTurretTuning LoadOrDefault(string path)
        {
            return TryLoad(path, out CharacterTestRuneTurretTuning tuning)
                ? tuning
                : CharacterTestRuneTurretTuning.Default;
        }

        private static bool TryLoad(string path, out CharacterTestRuneTurretTuning tuning)
        {
            tuning = CharacterTestRuneTurretTuning.Default;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path));
                JArray tunings = root["tunings"] as JArray;
                for (int i = 0; tunings != null && i < tunings.Count; i++)
                {
                    if (!(tunings[i] is JObject fields))
                        continue;

                    string stableId = ReadString(fields, "stableId");
                    if (!string.Equals(stableId, CharacterTestRuneTurretIds.TuningStableId, StringComparison.Ordinal))
                        continue;

                    tuning = Read(fields);
                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }

            return false;
        }

        private static CharacterTestRuneTurretTuning Read(JObject fields)
        {
            return new CharacterTestRuneTurretTuning(
                ReadString(fields, "stableId"),
                ReadString(fields, "abilityStableId"),
                ReadString(fields, "sourceStableId"),
                ReadInt(fields, "deployRangeMeters"),
                ReadInt(fields, "fallbackForwardMeters"),
                ReadInt(fields, "warmupFrames"),
                ReadInt(fields, "durationFrames"),
                ReadInt(fields, "scanRadiusCentimeters"),
                ReadInt(fields, "scanIntervalFrames"),
                ReadInt(fields, "fireIntervalFrames"),
                ReadInt(fields, "maxShots"),
                ReadInt(fields, "muzzleHeightCentimeters"),
                ReadInt(fields, "deployVfxDefinitionId"),
                ReadInt(fields, "idleVfxDefinitionId"),
                ReadInt(fields, "fireVfxDefinitionId"),
                ReadInt(fields, "expireVfxDefinitionId"));
        }

        private static string ReadString(JObject fields, string name)
        {
            JToken token = fields?[name];
            return token == null ? string.Empty : token.Value<string>() ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name, int fallback = 0)
        {
            JToken token = fields?[name];
            return token == null ? fallback : token.Value<int>();
        }
    }
}
