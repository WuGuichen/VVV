using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestCombatEvadeDirection
    {
        None = 0, Forward = 1, ForwardRight = 2, Right = 3, BackRight = 4, Back = 5, BackLeft = 6, Left = 7, ForwardLeft = 8
    }

    public readonly struct CharacterTestCombatEvadeStateSnapshot
    {
        public CharacterTestCombatEvadeStateSnapshot(
            CharacterTestCombatEvadeDirection firstDirection,
            bool pendingRoll,
            CharacterTestCombatEvadeDirection pendingDirection,
            long lastSpendFrame,
            int regenerationRemainder)
        {
            if ((uint)firstDirection > 8u || (uint)pendingDirection > 8u)
                throw new ArgumentOutOfRangeException(nameof(firstDirection), "Unknown combat evade direction.");
            FirstDirection = firstDirection;
            PendingRoll = pendingRoll;
            PendingDirection = pendingDirection;
            LastSpendFrame = lastSpendFrame;
            RegenerationRemainder = regenerationRemainder;
        }

        public CharacterTestCombatEvadeDirection FirstDirection { get; }
        public bool PendingRoll { get; }
        public CharacterTestCombatEvadeDirection PendingDirection { get; }
        public long LastSpendFrame { get; }
        public int RegenerationRemainder { get; }
    }

    internal static class CharacterTestCombatEvadeRuntime
    {
        public const int StaminaAttributeId = 920002;
        public const int StepAbilityId = 900001;
        public const int ForwardStepActionId = 950008;
        public const int RollAbilityId = 900002;
        public const int SimulationTickRate = 60;

    }

    internal readonly struct CharacterTestCombatEvadeTuning
    {
        public const string StableId = "mechanic_tuning.character_test.combat_evade";
        public const string GeneratedPath =
            "Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json";

        public CharacterTestCombatEvadeTuning(
            int maxStamina,
            int regenerationDelayFrames,
            int regenerationPerSecond)
        {
            MaxStamina = maxStamina > 0 ? maxStamina : 100;
            RegenerationDelayFrames = Math.Max(0, regenerationDelayFrames);
            RegenerationPerSecond = regenerationPerSecond > 0 ? regenerationPerSecond : 25;
        }

        public int MaxStamina { get; }
        public int RegenerationDelayFrames { get; }
        public int RegenerationPerSecond { get; }

        public static CharacterTestCombatEvadeTuning Default =>
            new CharacterTestCombatEvadeTuning(100, 45, 25);

        public static CharacterTestCombatEvadeTuning LoadDefault()
        {
            CharacterTestRuntimeCatalogPathCandidate[] candidates =
                CharacterTestRuntimeCatalogPathResolver.ResolveCandidates(
                    GeneratedPath,
                    CharacterTestRuntimeCatalogPathResolverContext.CreateDefault());
            for (int i = 0; i < candidates.Length; i++)
                if (TryLoad(candidates[i].JsonPath, out CharacterTestCombatEvadeTuning tuning))
                    return tuning;
            return Default;
        }

        private static bool TryLoad(string path, out CharacterTestCombatEvadeTuning tuning)
        {
            tuning = Default;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            try
            {
                JArray tunings = JObject.Parse(File.ReadAllText(path))["tunings"] as JArray;
                for (int i = 0; tunings != null && i < tunings.Count; i++)
                {
                    JObject fields = tunings[i] as JObject;
                    if (!string.Equals((string)fields?["stableId"], StableId, StringComparison.Ordinal))
                        continue;
                    tuning = new CharacterTestCombatEvadeTuning(
                        (int?)fields["maxStamina"] ?? 0,
                        (int?)fields["regenerationDelayFrames"] ?? 0,
                        (int?)fields["regenerationPerSecond"] ?? 0);
                    return true;
                }
            }
            catch (JsonException) { }
            catch (IOException) { }
            return false;
        }
    }
}
