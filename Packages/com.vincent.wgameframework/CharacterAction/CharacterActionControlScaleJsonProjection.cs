using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterAction
{
    /// <summary>Shared authoring/load-time control-key migration and projection. Never called by Tick.</summary>
    public static class CharacterActionControlScaleJsonProjection
    {
        public const float MaximumScale = 10f;
        public const int MaximumScaleMilli = 10000;

        /// <remarks>NoAlloc for valid values, including when motion requests are constructed during Tick.</remarks>
        public static float ValidateScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > MaximumScale)
                throw new ArgumentOutOfRangeException(nameof(value), "Control scale must be finite and between 0 and 10.");
            return value;
        }

        /// <summary>LowFreqAlloc. Converts old ranges in memory; no identity allocation or file writes.</summary>
        public static void MigrateAction(JObject action)
        {
            if (!(action?["motionTrack"] is JObject track)) return;
            if (track["movementControlKeys"] is JArray || track["rotationControlKeys"] is JArray)
            {
                RemoveGeneratedEvents(action);
                return;
            }
            var old = new List<JObject>();
            foreach (JObject evt in (track["events"] as JArray ?? new JArray()).Children<JObject>())
                if (evt.Value<string>("kind") == "SetControlScale") old.Add(evt);
            int duration = action.Value<int>("durationFrames");
            if (old.Count > 0 && duration <= 0)
                throw new FormatException("Control range migration requires the resolved action duration.");
            foreach (JObject evt in old)
            {
                int start = evt.Value<int>("frame"), end = evt.Value<int>("endFrame");
                if (start < 0 || end < start || end >= duration)
                    throw new FormatException("Legacy control range is outside the action; original data must be preserved.");
            }
            // Build both axes before replacing any original range.
            JArray movement = MigrateAxis(action, old, duration, "movementControlScale", "movement");
            JArray rotation = MigrateAxis(action, old, duration, "rotationControlScale", "rotation");
            var retained = new JArray();
            foreach (JObject evt in (track["events"] as JArray ?? new JArray()).Children<JObject>())
                if (evt.Value<string>("kind") != "SetControlScale") retained.Add(evt.DeepClone());
            track["movementControlKeys"] = movement;
            track["rotationControlKeys"] = rotation;
            track["events"] = retained;
        }

        private static JArray MigrateAxis(JObject action, List<JObject> events, int duration, string field, string axis)
        {
            var boundaries = new SortedSet<int>();
            foreach (JObject evt in events)
            {
                if (evt[field] == null || evt[field].Type == JTokenType.Null) continue;
                boundaries.Add(evt.Value<int>("frame"));
                int after = evt.Value<int>("endFrame") + 1;
                if (after < duration) boundaries.Add(after);
            }
            var result = new JArray();
            float previous = 1f;
            foreach (int frame in boundaries)
            {
                JObject winner = null;
                foreach (JObject evt in events)
                {
                    if (evt[field] == null || evt[field].Type == JTokenType.Null
                        || frame < evt.Value<int>("frame") || frame > evt.Value<int>("endFrame")) continue;
                    if (winner == null || HasHigherPriority(evt, winner)) winner = evt;
                }
                // Legacy ranges had a 0..1 clamp; preserve their actual effect.
                float value = winner == null ? 1f : Math.Max(0f, Math.Min(1f, ValidateScale(winner.Value<float>(field))));
                if (value == previous) continue;
                string label = "control-key/" + action.Value<int>("actionId").ToString(CultureInfo.InvariantCulture)
                    + "/" + axis + "/legacy/" + frame.ToString(CultureInfo.InvariantCulture);
                result.Add(new JObject { ["frame"] = frame, ["value"] = value,
                    ["stableEventId"] = 0, ["stableEventLabel"] = label, ["stableEventOrder"] = 0 });
                previous = value;
            }
            return result;
        }

        private static bool HasHigherPriority(JObject left, JObject right)
        {
            int start = left.Value<int>("frame").CompareTo(right.Value<int>("frame"));
            if (start != 0) return start > 0;
            int order = left.Value<int>("stableEventOrder").CompareTo(right.Value<int>("stableEventOrder"));
            return order != 0 ? order > 0 : string.CompareOrdinal(
                left["stableEventId"]?.ToString(), right["stableEventId"]?.ToString()) > 0;
        }

        /// <summary>LowFreqAlloc. Keys are the authority; supplied callbacks resolve prepared identities/orders.</summary>
        public static void SynchronizeAction(JObject action, Func<JObject, int> resolveIdentity = null,
            Func<int, int> resolveOrder = null)
        {
            if (!(action?["motionTrack"] is JObject track)) return;
            if (!(track["movementControlKeys"] is JArray) && !(track["rotationControlKeys"] is JArray)) return;
            var events = new JArray();
            var identities = new HashSet<int>();
            foreach (JObject evt in (track["events"] as JArray ?? new JArray()).Children<JObject>())
            {
                if (evt.Value<bool?>("controlScaleGenerated") == true) continue;
                if (evt.Value<string>("kind") == "SetControlScale")
                    throw new FormatException("Control keys and manual control ranges cannot coexist.");
                events.Add(evt.DeepClone());
                int id = evt.Value<int>("stableEventId");
                if (id > 0) identities.Add(id);
            }
            int duration = action.Value<int>("durationFrames");
            ProjectAxis(track["movementControlKeys"] as JArray, duration, "movementControlScale",
                events, identities, resolveIdentity, resolveOrder);
            ProjectAxis(track["rotationControlKeys"] as JArray, duration, "rotationControlScale",
                events, identities, resolveIdentity, resolveOrder);
            track["events"] = events;
        }

        private static void ProjectAxis(JArray keys, int duration, string field, JArray events,
            HashSet<int> identities, Func<JObject, int> resolveIdentity, Func<int, int> resolveOrder)
        {
            if (keys == null || keys.Count == 0) return;
            var sorted = new List<JObject>();
            foreach (JToken token in keys)
            {
                if (!(token is JObject key)) throw new FormatException("Control key must be an object.");
                int frame = key.Value<int>("frame");
                if (key["frame"]?.Type != JTokenType.Integer || frame < 0 || frame >= duration || key["value"] == null)
                    throw new FormatException("Control key frame must fit the action and must have a value.");
                ValidateScale(key.Value<float>("value"));
                sorted.Add(key);
            }
            sorted.Sort((left, right) => left.Value<int>("frame").CompareTo(right.Value<int>("frame")));
            for (int i = 0; i < sorted.Count; i++)
            {
                JObject key = sorted[i];
                int frame = key.Value<int>("frame");
                if (i > 0 && sorted[i - 1].Value<int>("frame") == frame)
                    throw new FormatException("Only one control key per axis and frame is allowed.");
                int id = resolveIdentity == null ? key.Value<int>("stableEventId") : resolveIdentity(key);
                int order = resolveOrder == null ? key.Value<int>("stableEventOrder") : resolveOrder(id);
                if (id <= 0 || order <= 0 || !identities.Add(id))
                    throw new FormatException("Control key requires a unique prepared event identity and order.");
                var evt = new JObject { ["frame"] = frame,
                    ["endFrame"] = i + 1 < sorted.Count ? sorted[i + 1].Value<int>("frame") - 1 : duration - 1,
                    ["kind"] = "SetControlScale", [field] = key.Value<float>("value"),
                    ["stableEventId"] = id, ["stableEventOrder"] = order, ["controlScaleGenerated"] = true };
                if (key["stableEventLabel"] != null) evt["stableEventLabel"] = key["stableEventLabel"].DeepClone();
                events.Add(evt);
            }
        }

        public static void RemoveGeneratedEvents(JObject action)
        {
            if (!((action?["motionTrack"] as JObject)?["events"] is JArray events)) return;
            for (int i = events.Count - 1; i >= 0; i--)
                if (events[i] is JObject evt && evt.Value<bool?>("controlScaleGenerated") == true) events.RemoveAt(i);
        }
    }
}
