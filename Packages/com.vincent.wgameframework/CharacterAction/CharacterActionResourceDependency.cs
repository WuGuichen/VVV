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
            CharacterActionResourceDependencyKind kind, int numericId, CharacterActionId actionId,
            CharacterActionTrackKind trackKind, CharacterActionTrackEventKind eventKind,
            int frame = -1, int stableEventId = 0, bool isMissing = false)
            : this(kind, string.Empty, actionId, trackKind, eventKind, frame, stableEventId, isMissing)
        {
            if (numericId < 0) throw new ArgumentOutOfRangeException(nameof(numericId));
            NumericId = numericId;
        }

        public CharacterActionResourceDependency(
            CharacterActionResourceDependencyKind kind,
            string stableId,
            CharacterActionId actionId,
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            int frame = -1,
            int stableEventId = 0,
            bool isMissing = false)
        {
            if (!Enum.IsDefined(typeof(CharacterActionResourceDependencyKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Dependency kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackKind), trackKind))
                throw new ArgumentOutOfRangeException(nameof(trackKind), "Track kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackEventKind), eventKind))
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Track event kind is not defined.");

            Kind = kind;
            NumericId = 0;
            StableId = stableId ?? string.Empty;
            ActionId = actionId;
            TrackKind = trackKind;
            EventKind = eventKind;
            Frame = frame;
            StableEventId = stableEventId;
            IsMissing = isMissing;
        }

        public CharacterActionResourceDependencyKind Kind { get; }
        public string StableId { get; }
        public int NumericId { get; }
        public CharacterActionId ActionId { get; }
        public CharacterActionTrackKind TrackKind { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public int Frame { get; }
        public int StableEventId { get; }
        public bool IsMissing { get; }

        public bool Equals(CharacterActionResourceDependency other)
        {
            return Kind == other.Kind
                && NumericId == other.NumericId
                && string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && ActionId == other.ActionId
                && TrackKind == other.TrackKind
                && EventKind == other.EventKind
                && Frame == other.Frame
                && (StableEventId == other.StableEventId)
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
                hash = (hash * 397) ^ NumericId;
                hash = (hash * 397) ^ (StableId != null ? StableId.GetHashCode() : 0);
                hash = (hash * 397) ^ ActionId.GetHashCode();
                hash = (hash * 397) ^ (int)TrackKind;
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ StableEventId.GetHashCode();
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
                    action.ActionId,
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
                    int combatActionId = (trackEvent.CombatActionId <= 0)
                        ? action.CombatTrack.CombatActionId
                        : trackEvent.CombatActionId;
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.CombatAction,
                        combatActionId,
                        action.ActionId,
                        CharacterActionTrackKind.Combat,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        (combatActionId <= 0)));
                }

                if (trackEvent.Kind == CharacterActionTrackEventKind.HitTraceWindow
                    && (trackEvent.TraceProfileId > 0))
                {
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.TraceProfile,
                        trackEvent.TraceProfileId,
                        action.ActionId,
                        CharacterActionTrackKind.Combat,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        false));
                }

            }

            if (!hasStartCombatEvent && (action.CombatTrack.CombatActionId > 0))
            {
                dependencies.Add(new CharacterActionResourceDependency(
                    CharacterActionResourceDependencyKind.CombatAction,
                    action.CombatTrack.CombatActionId,
                    action.ActionId,
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
                        action.ActionId,
                        CharacterActionTrackKind.Gameplay,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        trackEvent.RequestId <= 0));
                }
            }
        }

        private static void CollectAnimation(CharacterActionConfig action, List<CharacterActionResourceDependency> dependencies)
        {
            for (int i = 0; i < action.AnimationTrack.Events.Length; i++)
            {
                AnimationTrackEvent trackEvent = action.AnimationTrack.Events[i];
                string animationKey = trackEvent.EffectiveAnimationKey;
                dependencies.Add(new CharacterActionResourceDependency(
                    CharacterActionResourceDependencyKind.AnimationAction,
                    animationKey,
                    action.ActionId,
                    CharacterActionTrackKind.Animation,
                    trackEvent.Kind,
                    trackEvent.Frame,
                    trackEvent.StableEventId,
                    string.IsNullOrEmpty(animationKey)));
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
                        action.ActionId,
                        CharacterActionTrackKind.Presentation,
                        trackEvent.Kind,
                        trackEvent.Frame,
                        trackEvent.StableEventId,
                        (trackEvent.CueId <= 0)));
                    continue;
                }

                if (trackEvent.Kind == CharacterActionTrackEventKind.SpawnVisualCue)
                {
                    dependencies.Add(new CharacterActionResourceDependency(
                        CharacterActionResourceDependencyKind.VfxResource,
                        trackEvent.ResourceKey,
                        action.ActionId,
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
                    action.ActionId,
                    CharacterActionTrackKind.Debug,
                    trackEvent.Kind,
                    trackEvent.Frame,
                    trackEvent.StableEventId));
            }
        }
    }
}
