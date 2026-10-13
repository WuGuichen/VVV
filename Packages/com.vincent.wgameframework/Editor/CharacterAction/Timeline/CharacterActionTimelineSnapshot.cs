using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Gameplay;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed class CharacterActionTimelineSnapshot
    {
        private CharacterActionTimelineSnapshot(
            CharacterActionJsonDto action,
            int durationFrames,
            int actionDurationFrames,
            bool readOnly,
            IReadOnlyList<TimelineTrackDescriptor> tracks,
            IReadOnlyDictionary<TimelineTrackKind, IReadOnlyList<ITimelineItemAdapter>> itemsByTrack)
        {
            Action = action;
            DurationFrames = durationFrames;
            ActionDurationFrames = actionDurationFrames;
            ReadOnly = readOnly;
            Tracks = tracks;
            ItemsByTrack = itemsByTrack;
        }

        public CharacterActionJsonDto Action { get; }
        public int DurationFrames { get; }
        public int ActionDurationFrames { get; }
        public bool ReadOnly { get; }
        public IReadOnlyList<TimelineTrackDescriptor> Tracks { get; }
        public IReadOnlyDictionary<TimelineTrackKind, IReadOnlyList<ITimelineItemAdapter>> ItemsByTrack { get; }

        public static CharacterActionTimelineSnapshot FromAction(CharacterActionJsonDto action, int fallbackDurationFrames)
        {
            action = action ?? new CharacterActionJsonDto();
            bool readOnly = string.Equals(
                action.TimelineAuthority,
                CharacterActionTimelineAuthority.CombatAnchored.ToString(),
                StringComparison.Ordinal);
            bool canEdit = !readOnly;
            int actionDuration = CharacterActionTimelineLayout.ResolveDurationFrames(
                action.DurationFrames,
                fallbackDurationFrames);
            SynchronizeVfxSpans(action);
            int duration = ResolveDisplayDuration(action, actionDuration);

            var tracks = new List<TimelineTrackDescriptor>
            {
                new TimelineTrackDescriptor(TimelineTrackKind.Phases, "阶段", new Color(0.4f, 0.55f, 0.75f), true),
                new TimelineTrackDescriptor(TimelineTrackKind.TransitionWindow, "切换窗口 / Transition Window", new Color(0.78f, 0.52f, 0.25f), true),
                new TimelineTrackDescriptor(TimelineTrackKind.Motion, "运动", new Color(0.35f, 0.7f, 0.55f), action.MotionTrack != null),
                new TimelineTrackDescriptor(TimelineTrackKind.MovementControl, "移动控制倍率", new Color(0.35f, 0.78f, 0.6f), true),
                new TimelineTrackDescriptor(TimelineTrackKind.RotationControl, "旋转控制倍率", new Color(0.45f, 0.68f, 0.93f), true),
                new TimelineTrackDescriptor(TimelineTrackKind.Animation, "动画", new Color(0.55f, 0.5f, 0.85f), action.AnimationTrack != null),
                new TimelineTrackDescriptor(TimelineTrackKind.Combat, "战斗", new Color(0.85f, 0.4f, 0.4f), action.CombatTrack != null),
                new TimelineTrackDescriptor(TimelineTrackKind.Gameplay, "玩法", new Color(0.35f, 0.58f, 0.92f), action.GameplayTrack != null),
                new TimelineTrackDescriptor(TimelineTrackKind.Presentation, "表现", new Color(0.85f, 0.65f, 0.35f), action.PresentationTrack != null),
            };

            var itemsByTrack = new Dictionary<TimelineTrackKind, IReadOnlyList<ITimelineItemAdapter>>
            {
                [TimelineTrackKind.Phases] = BuildPhases(action, actionDuration, canEdit),
                [TimelineTrackKind.TransitionWindow] = BuildTransitionWindows(action, actionDuration, canEdit),
                [TimelineTrackKind.Motion] = BuildMotion(action, canEdit),
                [TimelineTrackKind.MovementControl] = BuildControlKeys(action, false, canEdit),
                [TimelineTrackKind.RotationControl] = BuildControlKeys(action, true, canEdit),
                [TimelineTrackKind.Animation] = BuildAnimation(action, canEdit),
                [TimelineTrackKind.Combat] = BuildCombat(action, canEdit),
                [TimelineTrackKind.Gameplay] = BuildGameplay(action, canEdit),
                [TimelineTrackKind.Presentation] = BuildPresentation(action, canEdit),
            };

            return new CharacterActionTimelineSnapshot(
                action,
                duration,
                actionDuration,
                readOnly,
                tracks,
                itemsByTrack);
        }

        public static CharacterActionTimelineSnapshot FromAbilityEffectTimeline(AbilityEffectTimelineDraft timeline)
        {
            timeline = timeline ?? new AbilityEffectTimelineDraft();
            int duration = CharacterActionTimelineLayout.ResolveDurationFrames(timeline.DurationFrames, 1);

            var tracks = new List<TimelineTrackDescriptor>
            {
                new TimelineTrackDescriptor(TimelineTrackKind.Combat, "Combat", new Color(0.85f, 0.4f, 0.4f), true),
                new TimelineTrackDescriptor(TimelineTrackKind.Gameplay, "Gameplay", new Color(0.35f, 0.58f, 0.92f), true),
                new TimelineTrackDescriptor(TimelineTrackKind.Presentation, "Presentation", new Color(0.85f, 0.65f, 0.35f), true),
            };

            var itemsByTrack = new Dictionary<TimelineTrackKind, IReadOnlyList<ITimelineItemAdapter>>
            {
                [TimelineTrackKind.Combat] = BuildAbilityEffectTimelineTrack(
                    timeline,
                    TimelineTrackKind.Combat,
                    new Color(0.85f, 0.4f, 0.4f, 0.95f)),
                [TimelineTrackKind.Gameplay] = BuildAbilityEffectTimelineTrack(
                    timeline,
                    TimelineTrackKind.Gameplay,
                    new Color(0.35f, 0.58f, 0.92f, 0.95f)),
                [TimelineTrackKind.Presentation] = BuildAbilityEffectTimelineTrack(
                    timeline,
                    TimelineTrackKind.Presentation,
                    new Color(0.85f, 0.65f, 0.35f, 0.95f)),
            };

            return new CharacterActionTimelineSnapshot(
                null,
                duration,
                duration,
                readOnly: false,
                tracks,
                itemsByTrack);
        }

        private static void SynchronizeVfxSpans(CharacterActionJsonDto action)
        {
            PresentationTrackEventJsonDto[] events =
                action?.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
            {
                PresentationTrackEventJsonDto evt = events[i];
                if (evt != null
                    && string.Equals(
                        evt.Kind,
                        CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        StringComparison.Ordinal))
                {
                    CharacterActionVfxTimelineResolver.SynchronizeSpan(evt);
                }
            }
        }

        private static int ResolveDisplayDuration(CharacterActionJsonDto action, int actionDuration)
        {
            const int tailEditingPaddingFrames = 30;
            int displayDuration = Math.Max(1, actionDuration);
            foreach (bool rotation in new[] { false, true })
                foreach (var key in CharacterActionControlScaleAuthoring.Keys(action, rotation))
                    if (key != null && key.Frame >= actionDuration && key.Frame < int.MaxValue - tailEditingPaddingFrames - 1)
                        displayDuration = Math.Max(displayDuration, key.Frame + 1 + tailEditingPaddingFrames);
            PresentationTrackEventJsonDto[] events =
                action?.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
            {
                PresentationTrackEventJsonDto evt = events[i];
                if (evt == null
                    || !string.Equals(
                        evt.Kind,
                        CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                displayDuration = Math.Max(
                    displayDuration,
                    checked(Math.Max(evt.Frame, evt.EndFrame) + 1 + tailEditingPaddingFrames));
            }

            return displayDuration;
        }

        public ITimelineItemAdapter FindItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            foreach (KeyValuePair<TimelineTrackKind, IReadOnlyList<ITimelineItemAdapter>> pair in ItemsByTrack)
            {
                IReadOnlyList<ITimelineItemAdapter> items = pair.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Id == itemId)
                        return items[i];
                }
            }

            return null;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildPhases(CharacterActionJsonDto action, int durationFrames, bool canEdit)
        {
            CharacterActionPhaseJsonDto[] phases = action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            var result = new List<ITimelineItemAdapter>(phases.Length);
            for (int i = 0; i < phases.Length; i++)
                result.Add(new Adapters.PhaseTimelineAdapter(action, i, durationFrames, canEdit));
            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildTransitionWindows(
            CharacterActionJsonDto action,
            int durationFrames,
            bool canEdit)
        {
            CharacterCancelRuleJsonDto[] rules = action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            var result = new List<ITimelineItemAdapter>(rules.Length);
            Color color = new Color(0.95f, 0.62f, 0.28f, 0.88f);
            for (int i = 0; i < rules.Length; i++)
                result.Add(new Adapters.TransitionWindowTimelineAdapter(action, i, durationFrames, color, canEdit));
            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildMotion(CharacterActionJsonDto action, bool canEdit)
        {
            MotionTrackEventJsonDto[] events = action.MotionTrack?.Events ?? Array.Empty<MotionTrackEventJsonDto>();
            var result = new List<ITimelineItemAdapter>(events.Length + 1);
            Color color = new Color(0.35f, 0.7f, 0.55f, 0.95f);
            if (action.MotionTrack?.RootMotion != null)
                result.Add(new Adapters.RootMotionTimelineAdapter(action, new Color(0.25f, 0.72f, 0.62f, 0.85f)));
            for (int i = 0; i < events.Length; i++)
                if (!events[i].ControlScaleGenerated && events[i].Kind != "SetControlScale")
                    result.Add(new Adapters.TrackEventTimelineAdapter(action, TimelineTrackKind.Motion, i, color, canEdit));
            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildControlKeys(CharacterActionJsonDto action, bool rotation, bool canEdit)
        {
            var result = new List<ITimelineItemAdapter>();
            foreach (var key in CharacterActionControlScaleAuthoring.Keys(action, rotation))
                if (key != null) result.Add(new Adapters.ControlScaleTimelineAdapter(action, key, rotation, canEdit));
            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildAnimation(CharacterActionJsonDto action, bool canEdit)
        {
            AnimationTrackEventJsonDto[] events = action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            var result = new List<ITimelineItemAdapter>(events.Length);
            Color color = new Color(0.55f, 0.5f, 0.85f, 0.95f);
            for (int i = 0; i < events.Length; i++)
                result.Add(new Adapters.AnimationTrackTimelineAdapter(action, i, color, canEdit));
            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildCombat(CharacterActionJsonDto action, bool canEdit)
        {
            CombatTrackEventJsonDto[] events = action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            var result = new List<ITimelineItemAdapter>(events.Length);
            Color color = new Color(0.85f, 0.4f, 0.4f, 0.95f);
            for (int i = 0; i < events.Length; i++)
            {
                if (Adapters.HitTraceWindowTimelineAdapter.IsHitTraceWindow(events[i]))
                {
                    result.Add(new Adapters.HitTraceWindowTimelineAdapter(
                        action,
                        events[i],
                        new Color(0.95f, 0.28f, 0.22f, 0.85f),
                        canEdit));
                    continue;
                }

                result.Add(new Adapters.TrackEventTimelineAdapter(action, TimelineTrackKind.Combat, i, color, canEdit));
            }

            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildPresentation(CharacterActionJsonDto action, bool canEdit)
        {
            PresentationTrackEventJsonDto[] events = action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
            var result = new List<ITimelineItemAdapter>(events.Length);
            Color color = new Color(0.85f, 0.65f, 0.35f, 0.95f);
            for (int i = 0; i < events.Length; i++)
                result.Add(new Adapters.TrackEventTimelineAdapter(action, TimelineTrackKind.Presentation, i, color, canEdit));
            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildGameplay(CharacterActionJsonDto action, bool canEdit)
        {
            GameplayTrackEventJsonDto[] events = action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
            var result = new List<ITimelineItemAdapter>(events.Length);
            Color color = new Color(0.35f, 0.58f, 0.92f, 0.95f);
            for (int i = 0; i < events.Length; i++)
                result.Add(new Adapters.TrackEventTimelineAdapter(action, TimelineTrackKind.Gameplay, i, color, canEdit));
            return result;
        }

        private static IReadOnlyList<ITimelineItemAdapter> BuildAbilityEffectTimelineTrack(
            AbilityEffectTimelineDraft timeline,
            TimelineTrackKind trackKind,
            Color color)
        {
            List<AbilityEffectTimelineEventDraft> events =
                timeline?.Events ?? new List<AbilityEffectTimelineEventDraft>();
            var result = new List<ITimelineItemAdapter>();
            for (int i = 0; i < events.Count; i++)
            {
                AbilityEffectTimelineEventDraft evt = events[i];
                if (evt == null || ResolveAbilityEffectTimelineTrackKind(evt) != trackKind)
                    continue;
                result.Add(new Adapters.AbilityEffectTimelineEventTimelineAdapter(timeline, evt, color));
            }

            return result;
        }

        private static TimelineTrackKind ResolveAbilityEffectTimelineTrackKind(AbilityEffectTimelineEventDraft evt)
        {
            if (Enum.TryParse(evt?.TrackKind, out AbilityEffectTimelineTrackKind kind))
            {
                switch (kind)
                {
                    case AbilityEffectTimelineTrackKind.Combat:
                        return TimelineTrackKind.Combat;
                    case AbilityEffectTimelineTrackKind.Presentation:
                        return TimelineTrackKind.Presentation;
                    case AbilityEffectTimelineTrackKind.Gameplay:
                    default:
                        return TimelineTrackKind.Gameplay;
                }
            }

            return TimelineTrackKind.Gameplay;
        }
    }
}
