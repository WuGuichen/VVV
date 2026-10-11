using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterAction
{
    /// <summary>Authoring/compile-time derivation only. LowFreqAlloc; never called by a runtime tick.</summary>
    public static class CharacterActionBladeTrailJsonProjection
    {
        public const string DefaultResourceKey = "vfx.weapon.blade_trail:GameObject@mxframework.samples";

        public static void SynchronizeDocument(JObject document,
            Func<int, JToken, int> resolveIdentity, Func<string, int> resolveDefinition)
        {
            if (!(document?["actions"] is JArray actions)) return;
            foreach (JObject action in actions.Children<JObject>())
                SynchronizeAction(action, resolveIdentity, resolveDefinition);
        }

        public static void SynchronizeAction(JObject action,
            Func<int, JToken, int> resolveIdentity, Func<string, int> resolveDefinition)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            JArray oldEvents = (action["presentationTrack"] as JObject)?["events"] as JArray;
            var events = new JArray();
            var occupied = new HashSet<int>();
            foreach (JObject evt in Enumerate(oldEvents))
            {
                if (evt.Value<bool?>("bladeTrailGenerated") == true)
                {
                    if (evt["bladeTrailSourceEventId"] == null)
                        throw new FormatException("Generated blade trail is missing its source event identity.");
                    continue;
                }
                events.Add(evt.DeepClone());
                occupied.Add(resolveIdentity(401, evt["stableEventId"]));
            }

            bool changed = oldEvents != null && oldEvents.Count != events.Count;
            foreach (string trackName in new[] { "motionTrack", "combatTrack", "gameplayTrack", "animationTrack", "debugTrack" })
                foreach (JObject evt in Enumerate((action[trackName] as JObject)?["events"] as JArray))
                    occupied.Add(resolveIdentity(401, evt["stableEventId"]));
            var owners = new HashSet<int>();
            var derived = new HashSet<int>();
            foreach (JObject hit in Enumerate((action["combatTrack"] as JObject)?["events"] as JArray))
            {
                if (!string.Equals(hit.Value<string>("kind"), "HitTraceWindow", StringComparison.Ordinal)) continue;
                JObject config = hit["bladeTrail"] as JObject;
                if (config?.Value<bool?>("enabled") != true) continue;
                int sourceId = resolveIdentity(401, hit["stableEventId"]);
                int eventId = resolveIdentity(401, config["stableEventId"]);
                int slot = resolveIdentity(405, hit["equipmentSlotId"]);
                if (sourceId <= 0 || eventId <= 0 || slot <= 0)
                    throw new FormatException("Enabled blade trail requires prepared source/event identities and an equipment slot.");
                if (!owners.Add(sourceId) || !derived.Add(eventId) || occupied.Contains(eventId) || sourceId == eventId)
                    throw new FormatException("Blade trail event identity conflicts with another event; manual events are preserved.");
                int advance = config.Value<int?>("advanceFrames") ?? 0;
                int delay = config.Value<int?>("delayFrames") ?? 0;
                int start = hit.Value<int>("frame");
                int end = hit.Value<int>("endFrame");
                int duration = action.Value<int>("durationFrames");
                if (advance < 0 || delay < 0 || start < 0 || end < start || duration <= end)
                    throw new FormatException("Blade trail offsets must be nonnegative and the source window must fit the action.");
                int trailStart = (int)Math.Max(0L, (long)start - advance);
                int trailEnd = (int)Math.Min(duration - 1L, (long)end + delay);
                int definitionId = resolveDefinition(DefaultResourceKey);
                if (definitionId <= 0) throw new FormatException("The formal blade trail VFX definition is missing.");
                var generated = new JObject
                {
                    ["frame"] = trailStart, ["endFrame"] = trailEnd,
                    ["kind"] = "SpawnVisualCue", ["resourceKey"] = DefaultResourceKey,
                    ["stableEventId"] = config["stableEventId"].DeepClone(),
                    ["bladeTrailGenerated"] = true,
                    ["useWeaponBladeTrailStyle"] = true,
                    ["bladeTrailSourceEventId"] = hit["stableEventId"].DeepClone(),
                    ["vfxAttachment"] = new JObject { ["mode"] = "FollowSubject", ["socketId"] = "", ["offsetSpace"] = "Local",
                        ["transform"] = new JObject { ["positionX"] = 0, ["positionY"] = 0, ["positionZ"] = 0,
                            ["rotationEulerX"] = 0, ["rotationEulerY"] = 0, ["rotationEulerZ"] = 0,
                            ["scaleX"] = 1, ["scaleY"] = 1, ["scaleZ"] = 1 } },
                    ["vfxPlaybackMode"] = "StopAtEventEnd", ["vfxPlaybackRate"] = 1,
                    ["vfxScaleMultiplier"] = 1, ["vfxStartOffsetFrames"] = 0, ["vfxEndOffsetFrames"] = 0,
                    ["vfxNaturalEndPolicy"] = "Dissipate", ["vfxDefinitionId"] = definitionId,
                    ["numericSocketId"] = 0, ["vfxParameter0"] = slot,
                    ["vfxParameter1"] = resolveIdentity(403, hit["traceProfileId"]),
                    ["vfxParameter2"] = 0, ["vfxParameter3"] = 0,
                };
                if (config["stableEventLabel"] != null)
                    generated["stableEventLabel"] = config["stableEventLabel"].DeepClone();
                if (hit["stableEventLabel"] != null)
                    generated["bladeTrailSourceEventLabel"] = hit["stableEventLabel"].DeepClone();
                events.Add(generated);
                changed = true;
            }
            if (!changed) return;
            if (!(action["presentationTrack"] is JObject track))
                action["presentationTrack"] = track = new JObject();
            track["events"] = events;
        }

        public static void RemoveGeneratedEvents(JObject action)
        {
            if (!((action?["presentationTrack"] as JObject)?["events"] is JArray events)) return;
            for (int i = events.Count - 1; i >= 0; i--)
                if (events[i] is JObject evt && evt.Value<bool?>("bladeTrailGenerated") == true)
                    events.RemoveAt(i);
        }

        private static readonly JObject[] EmptyEvents = Array.Empty<JObject>();
        private static IEnumerable<JObject> Enumerate(JArray events) => events == null
            ? EmptyEvents : (IEnumerable<JObject>)events.Children<JObject>();
    }
}
