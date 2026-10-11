using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestWeaponTraceBakeRequests
    {
        public static CharacterWeaponTraceBakeRequest[] Create(
            CharacterImportedPackage package,
            CharacterActionConfigRegistry registry)
        {
            CharacterActionConfig[] actions = registry?.Actions ?? Array.Empty<CharacterActionConfig>();
            if (actions.Length == 0)
                return Array.Empty<CharacterWeaponTraceBakeRequest>();

            var requests = new List<CharacterWeaponTraceBakeRequest>();
            for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
            {
                CharacterActionConfig action = actions[actionIndex];
                if (action == null)
                    continue;

                CombatTrackEvent[] combatEvents = action.CombatTrack?.Events
                    ?? Array.Empty<CombatTrackEvent>();
                for (int eventIndex = 0; eventIndex < combatEvents.Length; eventIndex++)
                {
                    CombatTrackEvent start = combatEvents[eventIndex];
                    if (start.Kind != CharacterActionTrackEventKind.HitTraceWindow
                        || start.HitVolume.HasVolume)
                    {
                        continue;
                    }

                    int endFrame = start.EndFrame;
                    string clipKey = ResolveActiveClipKey(action, start.Frame);
                    int traceId = CharacterWeaponTraceBakeCatalogBuilder.ResolveTraceIdForEquipmentSlot(
                        package,
                        start.EquipmentSlotId,
                        start.TraceProfileId);
                    requests.Add(new CharacterWeaponTraceBakeRequest(
                        action.ActionId,
                        clipKey,
                        traceId,
                        Math.Max(0, start.Frame),
                        Math.Max(start.Frame, endFrame),
                        ResolveMaxFrame(action, endFrame),
                        CreateSourceClipHash(action, clipKey, traceId, start.Frame, endFrame)));
                }
            }

            return requests.ToArray();
        }

        private static string ResolveActiveClipKey(CharacterActionConfig action, int frame)
        {
            AnimationTrackEvent[] events = action.AnimationTrack?.Events
                ?? Array.Empty<AnimationTrackEvent>();
            string fallback = string.Empty;
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent candidate = events[i];
                string key = candidate.EffectiveAnimationKey;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (string.IsNullOrWhiteSpace(fallback))
                    fallback = key;
                if (frame >= candidate.Frame && frame <= candidate.EndFrame)
                    return key;
            }

            return fallback;
        }

        private static int ResolveMaxFrame(CharacterActionConfig action, int endFrame)
        {
            int maxFrame = Math.Max(1, endFrame);
            if (action.DurationFrames.HasValue)
                maxFrame = Math.Max(maxFrame, action.DurationFrames.Value);

            AnimationTrackEvent[] events = action.AnimationTrack?.Events
                ?? Array.Empty<AnimationTrackEvent>();
            for (int i = 0; i < events.Length; i++)
                maxFrame = Math.Max(maxFrame, events[i].EndFrame);
            return maxFrame;
        }

        private static string CreateSourceClipHash(
            CharacterActionConfig action,
            string clipKey,
            int traceId,
            int startFrame,
            int endFrame)
        {
            return "character-test.action="
                + action.ActionId.Value
                + "|clip="
                + (clipKey ?? string.Empty)
                + "|trace="
                + traceId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|frames="
                + startFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ".."
                + endFrame.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
