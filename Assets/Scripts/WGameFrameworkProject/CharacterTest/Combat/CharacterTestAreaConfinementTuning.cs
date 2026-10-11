using System;
using System.IO;
using MxFramework.Core.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAreaConfinementInterruptPolicy
    {
        CancelOnDamageOrActionExit = 1,
    }

    internal sealed class CharacterTestAreaConfinementTuning
    {
        public const string DefaultGeneratedTuningsPath =
            "Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json";
        public const string DefaultStableId = "mechanic_tuning.character_test.area_confinement";
        public const int TelegraphStartLocalFrame = 6;
        public const int StartupEndFrame = 5;
        public const int RotationUnlockLocalFrame = 12;
        public const int RecoveryFrames = 6;

        public CharacterTestAreaConfinementTuning(
            string stableId,
            string abilityStableId,
            string sourceStableId,
            CharacterTestAreaConfinementInterruptPolicy interruptPolicy,
            int telegraphFrames,
            int zoneDurationFrames,
            int zoneRadiusCentimeters,
            int fadeInFrames,
            int fadeOutFrames,
            int releaseFrames,
            int telegraphCancelFrames,
            int telegraphVfxDefinitionId,
            int telegraphCancelVfxDefinitionId,
            int zoneVfxDefinitionId,
            int characterVfxDefinitionId,
            int characterReleaseVfxDefinitionId)
        {
            StableId = string.IsNullOrWhiteSpace(stableId) ? DefaultStableId : stableId;
            AbilityStableId = string.IsNullOrWhiteSpace(abilityStableId)
                ? CharacterTestAbilityIds.AreaConfinementAbilityStableId
                : abilityStableId;
            SourceStableId = string.IsNullOrWhiteSpace(sourceStableId)
                ? CharacterTestAreaConfinementIds.SourceStableId
                : sourceStableId;
            InterruptPolicy = interruptPolicy;
            TelegraphFrames = Math.Max(0, telegraphFrames);
            ZoneDurationFrames = zoneDurationFrames <= 0
                ? (int)CharacterTestAreaConfinementIds.ZoneDurationFrames
                : zoneDurationFrames;
            ZoneRadiusCentimeters = zoneRadiusCentimeters <= 0
                ? CharacterTestAreaConfinementIds.RadiusCentimeters
                : zoneRadiusCentimeters;
            FadeInFrames = Math.Max(0, fadeInFrames);
            FadeOutFrames = Math.Max(0, fadeOutFrames);
            ReleaseFrames = Math.Max(1, releaseFrames);
            TelegraphCancelFrames = Math.Max(1, telegraphCancelFrames);
            TelegraphVfxDefinitionId = telegraphVfxDefinitionId <= 0
                ? CharacterTestVfxIds.AreaConfinementTelegraphDefinitionId
                : telegraphVfxDefinitionId;
            TelegraphCancelVfxDefinitionId = telegraphCancelVfxDefinitionId <= 0
                ? CharacterTestVfxIds.AreaConfinementTelegraphCancelDefinitionId
                : telegraphCancelVfxDefinitionId;
            ZoneVfxDefinitionId = zoneVfxDefinitionId <= 0
                ? CharacterTestVfxIds.AreaConfinementZoneDefinitionId
                : zoneVfxDefinitionId;
            CharacterVfxDefinitionId = characterVfxDefinitionId <= 0
                ? CharacterTestVfxIds.AreaConfinementCharacterDefinitionId
                : characterVfxDefinitionId;
            CharacterReleaseVfxDefinitionId = characterReleaseVfxDefinitionId <= 0
                ? CharacterTestVfxIds.AreaConfinementCharacterReleaseDefinitionId
                : characterReleaseVfxDefinitionId;
        }

        public string StableId { get; }
        public string AbilityStableId { get; }
        public string SourceStableId { get; }
        public CharacterTestAreaConfinementInterruptPolicy InterruptPolicy { get; }
        public int TelegraphFrames { get; }
        public int ZoneDurationFrames { get; }
        public int ZoneRadiusCentimeters { get; }
        public int FadeInFrames { get; }
        public int FadeOutFrames { get; }
        public int ReleaseFrames { get; }
        public int TelegraphCancelFrames { get; }
        public int TelegraphVfxDefinitionId { get; }
        public int TelegraphCancelVfxDefinitionId { get; }
        public int ZoneVfxDefinitionId { get; }
        public int CharacterVfxDefinitionId { get; }
        public int CharacterReleaseVfxDefinitionId { get; }
        public int ActivationLocalFrame => TelegraphStartLocalFrame + TelegraphFrames;
        public int ActionDurationFrames => ActivationLocalFrame + RecoveryFrames;
        public Fix64 Radius => Fix64.FromRatio(ZoneRadiusCentimeters, 100);

        public static CharacterTestAreaConfinementTuning Default =>
            new CharacterTestAreaConfinementTuning(
                DefaultStableId,
                CharacterTestAbilityIds.AreaConfinementAbilityStableId,
                CharacterTestAreaConfinementIds.SourceStableId,
                CharacterTestAreaConfinementInterruptPolicy.CancelOnDamageOrActionExit,
                CharacterTestAreaConfinementIds.TelegraphFrames,
                (int)CharacterTestAreaConfinementIds.ZoneDurationFrames,
                CharacterTestAreaConfinementIds.RadiusCentimeters,
                CharacterTestAreaConfinementIds.VfxFadeInFrames,
                CharacterTestAreaConfinementIds.VfxFadeOutFrames,
                CharacterTestAreaConfinementIds.CharacterReleaseFrames,
                CharacterTestAreaConfinementIds.TelegraphCancelFrames,
                CharacterTestVfxIds.AreaConfinementTelegraphDefinitionId,
                CharacterTestVfxIds.AreaConfinementTelegraphCancelDefinitionId,
                CharacterTestVfxIds.AreaConfinementZoneDefinitionId,
                CharacterTestVfxIds.AreaConfinementCharacterDefinitionId,
                CharacterTestVfxIds.AreaConfinementCharacterReleaseDefinitionId);
    }

    internal static class CharacterTestAreaConfinementTuningSource
    {
        public static CharacterTestAreaConfinementTuning LoadDefault()
        {
            CharacterTestRuntimeCatalogPathCandidate[] candidates =
                CharacterTestRuntimeCatalogPathResolver.ResolveCandidates(
                    CharacterTestAreaConfinementTuning.DefaultGeneratedTuningsPath,
                    CharacterTestRuntimeCatalogPathResolverContext.CreateDefault());
            for (int i = 0; i < candidates.Length; i++)
            {
                if (TryLoad(candidates[i].JsonPath, out CharacterTestAreaConfinementTuning tuning))
                    return tuning;
            }

            return CharacterTestAreaConfinementTuning.Default;
        }

        public static CharacterTestAreaConfinementTuning LoadOrDefault(string path)
        {
            return TryLoad(path, out CharacterTestAreaConfinementTuning tuning)
                ? tuning
                : CharacterTestAreaConfinementTuning.Default;
        }

        private static bool TryLoad(string path, out CharacterTestAreaConfinementTuning tuning)
        {
            tuning = CharacterTestAreaConfinementTuning.Default;
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
                    if (!string.Equals(stableId, CharacterTestAreaConfinementTuning.DefaultStableId, StringComparison.Ordinal))
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

        private static CharacterTestAreaConfinementTuning Read(JObject fields)
        {
            return new CharacterTestAreaConfinementTuning(
                ReadString(fields, "stableId"),
                ReadString(fields, "abilityStableId"),
                ReadString(fields, "sourceStableId"),
                ReadInterruptPolicy(fields),
                ReadInt(fields, "telegraphFrames"),
                ReadInt(fields, "zoneDurationFrames"),
                ReadInt(fields, "zoneRadiusCentimeters"),
                ReadInt(fields, "fadeInFrames"),
                ReadInt(fields, "fadeOutFrames"),
                ReadInt(fields, "releaseFrames"),
                ReadInt(fields, "telegraphCancelFrames"),
                ReadInt(fields, "telegraphVfxDefinitionId"),
                ReadInt(fields, "telegraphCancelVfxDefinitionId"),
                ReadInt(fields, "zoneVfxDefinitionId"),
                ReadInt(fields, "characterVfxDefinitionId"),
                ReadInt(fields, "characterReleaseVfxDefinitionId"));
        }

        private static CharacterTestAreaConfinementInterruptPolicy ReadInterruptPolicy(JObject fields)
        {
            string value = ReadString(fields, "interruptPolicy");
            return string.Equals(value, "CancelOnDamageOrActionExit", StringComparison.Ordinal)
                ? CharacterTestAreaConfinementInterruptPolicy.CancelOnDamageOrActionExit
                : CharacterTestAreaConfinementInterruptPolicy.CancelOnDamageOrActionExit;
        }

        private static string ReadString(JObject fields, string name)
        {
            JToken token = fields?[name];
            return token == null ? string.Empty : token.Value<string>() ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name)
        {
            JToken token = fields?[name];
            return token == null ? 0 : token.Value<int>();
        }
    }

    internal static class CharacterTestAreaConfinementActionTuningApplier
    {
        public static string Apply(string actionsJson, CharacterTestAreaConfinementTuning tuning)
        {
            if (string.IsNullOrWhiteSpace(actionsJson))
                return actionsJson ?? string.Empty;

            CharacterTestAreaConfinementTuning safeTuning =
                tuning ?? CharacterTestAreaConfinementTuning.Default;
            try
            {
                JObject root = JObject.Parse(actionsJson);
                JArray actions = root["actions"] as JArray;
                for (int i = 0; actions != null && i < actions.Count; i++)
                {
                    if (!(actions[i] is JObject action))
                        continue;

                    int actionId = (int?)action["actionId"] ?? 0;
                    if (actionId != CharacterTestCharacterActionConfigSource.AreaConfinementActionId.Value)
                    {
                        continue;
                    }

                    Apply(action, safeTuning);
                    return JsonConvert.SerializeObject(root);
                }
            }
            catch (JsonException)
            {
                return actionsJson;
            }

            return actionsJson;
        }

        private static void Apply(JObject action, CharacterTestAreaConfinementTuning tuning)
        {
            int activationFrame = tuning.ActivationLocalFrame;
            int durationFrames = tuning.ActionDurationFrames;
            action["durationFrames"] = durationFrames;
            action["phases"] = new JArray
            {
                CreatePhase("Startup", 0, CharacterTestAreaConfinementTuning.StartupEndFrame),
                CreatePhase("Active", CharacterTestAreaConfinementTuning.TelegraphStartLocalFrame, activationFrame - 1),
                CreatePhase("Recovery", activationFrame, durationFrames - 1)
            };
            action["motionTrack"] = CreateMotionTrack(tuning);
            PatchAnimationEndFrame(action, durationFrames - 1);
        }

        private static JObject CreatePhase(string kind, int startFrame, int endFrame)
        {
            return new JObject
            {
                ["kind"] = kind,
                ["startFrame"] = startFrame,
                ["endFrame"] = Math.Max(startFrame, endFrame)
            };
        }

        private static JObject CreateMotionTrack(CharacterTestAreaConfinementTuning tuning)
        {
            int activationFrame = tuning.ActivationLocalFrame;
            int rotationUnlockFrame = Math.Min(
                CharacterTestAreaConfinementTuning.RotationUnlockLocalFrame,
                activationFrame);
            int actionEndFrame = tuning.ActionDurationFrames - 1;
            return new JObject
            {
                ["usesRootMotion"] = false,
                ["rootMotion"] = null,
                ["events"] = new JArray
                {
                    CreateControlScaleEvent(
                        0,
                        rotationUnlockFrame - 1,
                        "ControlLocked",
                        0.0,
                        0.0,
                        CharacterTestCharacterActionConfigSource.AreaConfinementControlLockStableEventId),
                    CreateControlScaleEvent(
                        rotationUnlockFrame,
                        activationFrame - 1,
                        "ControlLocked",
                        0.0,
                        1.0,
                        CharacterTestCharacterActionConfigSource.AreaConfinementChannelControlStableEventId),
                    CreateControlScaleEvent(
                        activationFrame,
                        actionEndFrame,
                        "Run",
                        1.0,
                        1.0,
                        CharacterTestCharacterActionConfigSource.AreaConfinementControlReleaseStableEventId)
                }
            };
        }

        private static JObject CreateControlScaleEvent(
            int frame,
            int endFrame,
            string movementMode,
            double movementControlScale,
            double rotationControlScale,
            int stableEventId)
        {
            return new JObject
            {
                ["frame"] = frame,
                ["endFrame"] = Math.Max(frame, endFrame),
                ["kind"] = "SetControlScale",
                ["movementMode"] = movementMode,
                ["movementControlScale"] = movementControlScale,
                ["rotationControlScale"] = rotationControlScale,
                ["stableEventId"] = stableEventId,
                ["stableEventOrder"] = CharacterTestAuthoredContentIds.GetEventOrder(stableEventId)
            };
        }

        private static void PatchAnimationEndFrame(JObject action, int endFrame)
        {
            JArray events = action["animationTrack"]?["events"] as JArray;
            for (int i = 0; events != null && i < events.Count; i++)
            {
                if (events[i] is JObject evt)
                    evt["endFrame"] = endFrame;
            }
        }
    }
}

