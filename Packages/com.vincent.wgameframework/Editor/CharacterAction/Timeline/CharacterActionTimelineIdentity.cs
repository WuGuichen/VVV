using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MxFramework.CharacterAction;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal static class CharacterActionTimelineIdentity
    {
        private sealed class IdentityBox
        {
            public IdentityBox(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private static readonly ConditionalWeakTable<object, IdentityBox> ObjectIds =
            new ConditionalWeakTable<object, IdentityBox>();

        public static string ItemId(TimelineTrackKind trackKind, object item, string stableEventId)
        {
            string key = string.IsNullOrWhiteSpace(stableEventId)
                ? "temp:" + ObjectKey(item)
                : "stable:" + stableEventId.Trim() + ":ref:" + ObjectKey(item);
            return TrackPrefix(trackKind) + ":" + key;
        }

        public static string PhaseId(CharacterActionPhaseJsonDto phase, int fallbackIndex)
        {
            if (phase == null)
                return "phase:null:" + fallbackIndex;
            return "phase:ref:" + ObjectKey(phase);
        }

        public static bool TryParseTrackKind(string itemId, out TimelineTrackKind trackKind)
        {
            trackKind = TimelineTrackKind.Phases;
            if (string.IsNullOrEmpty(itemId))
                return false;

            int colon = itemId.IndexOf(':');
            if (colon <= 0)
                return false;

            string prefix = itemId.Substring(0, colon);
            switch (prefix)
            {
                case "phase":
                    trackKind = TimelineTrackKind.Phases;
                    return true;
                case "transitionWindow":
                    trackKind = TimelineTrackKind.TransitionWindow;
                    return true;
                case "motion":
                    trackKind = TimelineTrackKind.Motion;
                    return true;
                case "animation":
                    trackKind = TimelineTrackKind.Animation;
                    return true;
                case "combat":
                    trackKind = TimelineTrackKind.Combat;
                    return true;
                case "gameplay":
                    trackKind = TimelineTrackKind.Gameplay;
                    return true;
                case "presentation":
                    trackKind = TimelineTrackKind.Presentation;
                    return true;
                default:
                    return false;
            }
        }

        public static string TrackPrefix(TimelineTrackKind kind)
        {
            return kind switch
            {
                TimelineTrackKind.Phases => "phase",
                TimelineTrackKind.TransitionWindow => "transitionWindow",
                TimelineTrackKind.Motion => "motion",
                TimelineTrackKind.Animation => "animation",
                TimelineTrackKind.Combat => "combat",
                TimelineTrackKind.Gameplay => "gameplay",
                TimelineTrackKind.Presentation => "presentation",
                _ => "event",
            };
        }

        public static string MakeStableEventId(CharacterActionJsonDto action, string suffix)
        {
            string actionId = action == null || action.ActionId <= 0
                ? "action"
                : "action_" + action.ActionId;
            suffix = string.IsNullOrWhiteSpace(suffix) ? "event" : suffix.Trim();
            return MakeUniqueStableEventId(action, actionId + "." + suffix);
        }

        public static string MakeUniqueStableEventId(CharacterActionJsonDto action, string baseId)
        {
            baseId = string.IsNullOrWhiteSpace(baseId) ? "event" : baseId.Trim();
            var used = new HashSet<string>(StringComparer.Ordinal);
            CollectStableEventIds(action, used);
            if (!used.Contains(baseId))
                return baseId;

            for (int i = 2; i < int.MaxValue; i++)
            {
                string candidate = baseId + "_" + i;
                if (!used.Contains(candidate))
                    return candidate;
            }

            return baseId + "_" + Guid.NewGuid().ToString("N");
        }

        private static string ObjectKey(object item)
        {
            if (item == null)
                return Guid.NewGuid().ToString("N");

            IdentityBox box = ObjectIds.GetValue(
                item,
                _ => new IdentityBox(Guid.NewGuid().ToString("N")));
            return box.Value;
        }

        private static void CollectStableEventIds(CharacterActionJsonDto action, HashSet<string> used)
        {
            if (action == null)
                return;

            Collect(action.MotionTrack?.Events, evt => CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), used);
            Collect(action.AnimationTrack?.Events, evt => CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), used);
            Collect(action.CombatTrack?.Events, evt => CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), used);
            Collect(action.GameplayTrack?.Events, evt => CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), used);
            Collect(action.PresentationTrack?.Events, evt => CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), used);
            Collect(action.DebugTrack?.Events, evt => CharacterActionAuthoringIds.Label(evt?.StableEventId ?? 0, 401), used);
        }

        private static void Collect<T>(IEnumerable<T> events, Func<T, string> getStableId, HashSet<string> used)
        {
            if (events == null)
                return;

            foreach (T evt in events)
            {
                string id = getStableId(evt);
                if (!string.IsNullOrWhiteSpace(id))
                    used.Add(id.Trim());
            }
        }
    }
}
