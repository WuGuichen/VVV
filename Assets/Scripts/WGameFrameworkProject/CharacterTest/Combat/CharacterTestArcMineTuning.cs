using System;
using System.IO;
using MxFramework.Core.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestArcMineIds
    {
        public const int MechanicSourceId = 501013;
        public const string SourceStableId = "mechanic.character_test.arc_mine.trap";
        public const string TuningStableId = "mechanic_tuning.character_test.arc_mine";
        public const int DeployRangeMeters = 8;
        public const int FallbackForwardMeters = 5;
        public const int FuseDelayFrames = 60;
        public const int ArmDelayFrames = FuseDelayFrames;
        public const int DurationFrames = 180;
        public const int TriggerRadiusCentimeters = 125;
        public const int BlastRadiusCentimeters = 225;
        public const int MaxBlastTargets = 8;
        public const int DeployVfxDefinitionId = 12041;
        public const int DeployVfxLifetimeFrames = 30;
        public const int TriggerVfxDefinitionId = 12042;
        public const int TriggerVfxLifetimeFrames = 24;
        public const int FuseVfxDefinitionId = 12043;
        public const int FuseVfxLifetimeFrames = FuseDelayFrames;
        public const int WaitingVfxRefreshIntervalFrames = 45;
    }

    internal sealed class CharacterTestArcMineTuning
    {
        public const string DefaultGeneratedTuningsPath =
            "Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json";

        public CharacterTestArcMineTuning(
            string stableId,
            string abilityStableId,
            string sourceStableId,
            int deployRangeMeters,
            int fallbackForwardMeters,
            int fuseDelayFrames,
            int durationFrames,
            int triggerRadiusCentimeters,
            int blastRadiusCentimeters,
            int maxBlastTargets,
            int deployVfxDefinitionId,
            int triggerVfxDefinitionId,
            int fuseVfxDefinitionId)
        {
            StableId = string.IsNullOrWhiteSpace(stableId)
                ? CharacterTestArcMineIds.TuningStableId
                : stableId;
            AbilityStableId = string.IsNullOrWhiteSpace(abilityStableId)
                ? CharacterTestAbilityIds.ArcMineAbilityStableId
                : abilityStableId;
            SourceStableId = string.IsNullOrWhiteSpace(sourceStableId)
                ? CharacterTestArcMineIds.SourceStableId
                : sourceStableId;
            DeployRangeMeters = deployRangeMeters <= 0
                ? CharacterTestArcMineIds.DeployRangeMeters
                : deployRangeMeters;
            FallbackForwardMeters = fallbackForwardMeters <= 0
                ? CharacterTestArcMineIds.FallbackForwardMeters
                : fallbackForwardMeters;
            FuseDelayFrames = Math.Max(1, fuseDelayFrames);
            DurationFrames = durationFrames <= 0
                ? CharacterTestArcMineIds.DurationFrames
                : durationFrames;
            TriggerRadiusCentimeters = triggerRadiusCentimeters <= 0
                ? CharacterTestArcMineIds.TriggerRadiusCentimeters
                : triggerRadiusCentimeters;
            BlastRadiusCentimeters = blastRadiusCentimeters <= 0
                ? CharacterTestArcMineIds.BlastRadiusCentimeters
                : blastRadiusCentimeters;
            MaxBlastTargets = maxBlastTargets <= 0
                ? CharacterTestArcMineIds.MaxBlastTargets
                : maxBlastTargets;
            DeployVfxDefinitionId = deployVfxDefinitionId <= 0
                ? CharacterTestArcMineIds.DeployVfxDefinitionId
                : deployVfxDefinitionId;
            TriggerVfxDefinitionId = triggerVfxDefinitionId <= 0
                ? CharacterTestArcMineIds.TriggerVfxDefinitionId
                : triggerVfxDefinitionId;
            FuseVfxDefinitionId = Math.Max(0, fuseVfxDefinitionId);
        }

        public string StableId { get; }
        public string AbilityStableId { get; }
        public string SourceStableId { get; }
        public int DeployRangeMeters { get; }
        public int FallbackForwardMeters { get; }
        public int FuseDelayFrames { get; }
        public int ArmDelayFrames => FuseDelayFrames;
        public int DurationFrames { get; }
        public int TriggerRadiusCentimeters { get; }
        public int BlastRadiusCentimeters { get; }
        public int MaxBlastTargets { get; }
        public int DeployVfxDefinitionId { get; }
        public int TriggerVfxDefinitionId { get; }
        public int FuseVfxDefinitionId { get; }
        public Fix64 TriggerRadius => Fix64.FromRatio(TriggerRadiusCentimeters, 100);
        public Fix64 BlastRadius => Fix64.FromRatio(BlastRadiusCentimeters, 100);

        public static CharacterTestArcMineTuning Default =>
            new CharacterTestArcMineTuning(
                CharacterTestArcMineIds.TuningStableId,
                CharacterTestAbilityIds.ArcMineAbilityStableId,
                CharacterTestArcMineIds.SourceStableId,
                CharacterTestArcMineIds.DeployRangeMeters,
                CharacterTestArcMineIds.FallbackForwardMeters,
                CharacterTestArcMineIds.FuseDelayFrames,
                CharacterTestArcMineIds.DurationFrames,
                CharacterTestArcMineIds.TriggerRadiusCentimeters,
                CharacterTestArcMineIds.BlastRadiusCentimeters,
                CharacterTestArcMineIds.MaxBlastTargets,
                CharacterTestArcMineIds.DeployVfxDefinitionId,
                CharacterTestArcMineIds.TriggerVfxDefinitionId,
                CharacterTestArcMineIds.FuseVfxDefinitionId);
    }

    internal static class CharacterTestArcMineTuningSource
    {
        public static CharacterTestArcMineTuning LoadDefault()
        {
            CharacterTestRuntimeCatalogPathCandidate[] candidates =
                CharacterTestRuntimeCatalogPathResolver.ResolveCandidates(
                    CharacterTestArcMineTuning.DefaultGeneratedTuningsPath,
                    CharacterTestRuntimeCatalogPathResolverContext.CreateDefault());
            for (int i = 0; i < candidates.Length; i++)
            {
                if (TryLoad(candidates[i].JsonPath, out CharacterTestArcMineTuning tuning))
                    return tuning;
            }

            return CharacterTestArcMineTuning.Default;
        }

        public static CharacterTestArcMineTuning LoadOrDefault(string path)
        {
            return TryLoad(path, out CharacterTestArcMineTuning tuning)
                ? tuning
                : CharacterTestArcMineTuning.Default;
        }

        private static bool TryLoad(string path, out CharacterTestArcMineTuning tuning)
        {
            tuning = CharacterTestArcMineTuning.Default;
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
                    if (!string.Equals(stableId, CharacterTestArcMineIds.TuningStableId, StringComparison.Ordinal))
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

        private static CharacterTestArcMineTuning Read(JObject fields)
        {
            return new CharacterTestArcMineTuning(
                ReadString(fields, "stableId"),
                ReadString(fields, "abilityStableId"),
                ReadString(fields, "sourceStableId"),
                ReadInt(fields, "deployRangeMeters"),
                ReadInt(fields, "fallbackForwardMeters"),
                ReadInt(fields, "fuseDelayFrames", ReadInt(fields, "armDelayFrames")),
                ReadInt(fields, "durationFrames"),
                ReadInt(fields, "triggerRadiusCentimeters"),
                ReadInt(fields, "blastRadiusCentimeters"),
                ReadInt(fields, "maxBlastTargets"),
                ReadInt(fields, "deployVfxDefinitionId"),
                ReadInt(fields, "triggerVfxDefinitionId"),
                ReadInt(fields, "fuseVfxDefinitionId"));
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
