using System;
using System.Collections.Generic;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionResourceDependencyKind
    {
        None = 0,
        CombatAction = 1,
        TraceProfile = 2,
        GameplayRequest = 3,
        AnimationAction = 4,
        AudioCue = 5,
        VfxResource = 6,
        MotionEvent = 7,
        DebugMarker = 8,
    }

    public readonly struct CharacterActionResourceDependency : IEquatable<CharacterActionResourceDependency>
    {
        public CharacterActionResourceDependency(
            CharacterActionResourceDependencyKind kind,
            string stableId,
            string actionId,
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            int frame = -1,
            string stableEventId = "",
            bool isMissing = false)
        {
            if (!Enum.IsDefined(typeof(CharacterActionResourceDependencyKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Dependency kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackKind), trackKind))
                throw new ArgumentOutOfRangeException(nameof(trackKind), "Track kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackEventKind), eventKind))
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Track event kind is not defined.");

            Kind = kind;
            StableId = stableId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            TrackKind = trackKind;
            EventKind = eventKind;
            Frame = frame;
            StableEventId = stableEventId ?? string.Empty;
            IsMissing = isMissing;
        }

        public CharacterActionResourceDependencyKind Kind { get; }
        public string StableId { get; }
        public string ActionId { get; }
        public CharacterActionTrackKind TrackKind { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public int Frame { get; }
        public string StableEventId { get; }
        public bool IsMissing { get; }

        public bool Equals(CharacterActionResourceDependency other)
        {
            return Kind == other.Kind
                && string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && string.Equals(ActionId, other.ActionId, StringComparison.Ordinal)
                && TrackKind == other.TrackKind
                && EventKind == other.EventKind
                && Frame == other.Frame
                && string.Equals(StableEventId, other.StableEventId, StringComparison.Ordinal)
                && IsMissing == other.IsMissing;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionResourceDependency other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (StableId != null ? StableId.GetHashCode() : 0);
                hash = (hash * 397) ^ (ActionId != null ? ActionId.GetHashCode() : 0);
                hash = (hash * 397) ^ (int)TrackKind;
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ (StableEventId != null ? StableEventId.GetHashCode() : 0);
                hash = (hash * 397) ^ IsMissing.GetHashCode();
                return hash;
            }
        }
    }

    public static class CharacterActionResourceDependencyCollector
    {
        public static CharacterActionResourceDependency[] Collect(CharacterActionConfig action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var dependencies = new List<CharacterActionResourceDependency>();
            CollectMotion(action, dependencies);
            CollectCombat(action, dependencies);
            CollectGameplay(action, dependencies);
            CollectAnimation(action, dependencies);
            CollectPresentation(action, dependencies);
            CollectDebug(action, dependencies);
            return dependencies.ToArray();
        }

        private static void CollectMotion(CharacterActionConfig action, List<CharacterActionResourceDependency> dependencies)
        {
            for (int i = 0; i < action.MotionTrack.Events.Length; i++)
            {
                MotionTrackEvent trackEvent = action.MotionTrack.Events[i];
                dependencies.Add(new CharacterActionResourceDependency(
                    CharacterActionResourceDependencyKind.MotionEvent,
                    trackEvent.StableEventId,
                    action.StableId,
                    CharacterActionTrackKind.Motion,
                    trackEvent.Kind,
                    trackEvent.Frame,
                    trackEvent.StableEventId));
            }
        }

        private static void CollectCombat(CharacterActionConfig action, List<CharacterActionResourceDependency> dependencies)
        {
            bool hasStartCombatEvent = false;
            for (int i = 0; i < action.CombatTrack.Events.Length; i++)
            {
                CombatTrackEvent trackEvent = action.CombatTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.StartCombatAction)
                {
                    hasStartCombatEvent = true;
                    string combatActionId = string.IsNullOrEmpty(trackEvent.CombatActionId)
                        ? action.CombatTrack.CombatActionId
                        : trackEvent.CombatActionId;
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.CombatAction,
                        combatActionId,
                        action.StableId,
                        CharacterActionTrackKind.Combat,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        string.IsNullOrEmpty(combatActionId)));
                }

                if (trackEvent.Kind == CharacterActionTrackEventKind.StartHitTrace
                    || trackEvent.Kind == CharacterActionTrackEventKind.StopHitTrace)
                {
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.TraceProfile,
                        trackEvent.TraceProfileId,
                        action.StableId,
                        CharacterActionTrackKind.Combat,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        string.IsNullOrEmpty(trackEvent.TraceProfileId)));
                }
            }

            if (!hasStartCombatEvent && !string.IsNullOrEmpty(action.CombatTrack.CombatActionId))
            {
                dependencies.Add(new CharacterActionResourceDependency(
                    CharacterActionResourceDependencyKind.CombatAction,
                    action.CombatTrack.CombatActionId,
                    action.StableId,
                    CharacterActionTrackKind.Combat,
                    CharacterActionTrackEventKind.None,
                    isMissing: false));
            }
        }

        private static void CollectGameplay(CharacterActionConfig action, List<CharacterActionResourceDependency> dependencies)
        {
            for (int i = 0; i < action.GameplayTrack.Events.Length; i++)
            {
                GameplayTrackEvent trackEvent = action.GameplayTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.SendGameplayRequest)
                {
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.GameplayRequest,
                        trackEvent.RequestId,
                        action.StableId,
                        CharacterActionTrackKind.Gameplay,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        string.IsNullOrEmpty(trackEvent.RequestId)));
                }
            }
        }

        private static void CollectAnimation(CharacterActionConfig action, List<CharacterActionResourceDependency> dependencies)
        {
            for (int i = 0; i < action.AnimationTrack.Events.Length; i++)
            {
                AnimationTrackEvent trackEvent = action.AnimationTrack.Events[i];
                dependencies.Add(new CharacterActionResourceDependency(
                    CharacterActionResourceDependencyKind.AnimationAction,
                    trackEvent.AnimationActionKey,
                    action.StableId,
                    CharacterActionTrackKind.Animation,
                    trackEvent.Kind,
                    trackEvent.Frame,
                    trackEvent.StableEventId,
                    string.IsNullOrEmpty(trackEvent.AnimationActionKey)));
            }
        }

        private static void CollectPresentation(CharacterActionConfig action, List<CharacterActionResourceDependency> dependencies)
        {
            for (int i = 0; i < action.PresentationTrack.Events.Length; i++)
            {
                PresentationTrackEvent trackEvent = action.PresentationTrack.Events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.PlayAudioCue)
                {
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.AudioCue,
                        trackEvent.CueId,
                        action.StableId,
                        CharacterActionTrackKind.Presentation,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        string.IsNullOrEmpty(trackEvent.CueId)));
                    continue;
                }

                if (trackEvent.Kind == CharacterActionTrackEventKind.SpawnVisualCue)
                {
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.VfxResource,
                        trackEvent.ResourceKey,
                        action.StableId,
                        CharacterActionTrackKind.Presentation,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        string.IsNullOrEmpty(trackEvent.ResourceKey)));
                }
            }
        }

        private static void CollectDebug(CharacterActionConfig action, List<CharacterActionResourceDependency> dependencies)
        {
            for (int i = 0; i < action.DebugTrack.Events.Length; i++)
            {
                DebugTrackEvent trackEvent = action.DebugTrack.Events[i];
                dependencies.Add(new CharacterActionResourceDependency(
                    CharacterActionResourceDependencyKind.DebugMarker,
                    trackEvent.MarkerId,
                    action.StableId,
                    CharacterActionTrackKind.Debug,
                    trackEvent.Kind,
                    trackEvent.Frame,
                    trackEvent.StableEventId));
            }
        }
    }
}
