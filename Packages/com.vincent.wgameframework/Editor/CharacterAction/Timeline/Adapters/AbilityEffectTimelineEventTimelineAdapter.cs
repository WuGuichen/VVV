using System;
using System.Collections.Generic;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Gameplay;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Adapters
{
    internal sealed class AbilityEffectTimelineEventTimelineAdapter : ITimelineItemAdapter
    {
        private readonly AbilityEffectTimelineDraft _timeline;
        private readonly AbilityEffectTimelineEventDraft _eventRef;
        private readonly string _eventId;
        private readonly TimelineTrackKind _trackKind;
        private readonly Color _color;
        private readonly string _id;

        public AbilityEffectTimelineEventTimelineAdapter(
            AbilityEffectTimelineDraft timeline,
            AbilityEffectTimelineEventDraft evt,
            Color color)
        {
            _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            _eventRef = evt ?? throw new ArgumentNullException(nameof(evt));
            _eventId = evt.EventId ?? string.Empty;
            _trackKind = ResolveTrackKind(evt);
            _color = color;
            _id = CharacterActionTimelineIdentity.ItemId(_trackKind, evt, _eventId);
        }

        public string Id => _id;
        public TimelineTrackKind TrackKind => _trackKind;
        public TimelineItemKind ItemKind => IsRangeEvent(GetEvent()) ? TimelineItemKind.Clip : TimelineItemKind.Marker;
        public int StartFrame => Math.Max(0, GetEvent()?.Frame ?? 0);
        public int EndFrame
        {
            get
            {
                AbilityEffectTimelineEventDraft evt = GetEvent();
                if (!IsRangeEvent(evt))
                    return StartFrame;
                return Math.Max(StartFrame, evt.EndFrame);
            }
        }

        public string Title => BuildSummary(GetEvent(), compact: true);
        public string FullTitle => BuildSummary(GetEvent(), compact: false);
        public Color Color => _color;
        public bool CanEdit => true;

        public bool TryGetCurrentIndex(out int index)
        {
            List<AbilityEffectTimelineEventDraft> events =
                _timeline.Events ?? new List<AbilityEffectTimelineEventDraft>();
            if (_eventRef != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    if (ReferenceEquals(events[i], _eventRef))
                    {
                        index = i;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_eventId))
            {
                int match = -1;
                for (int i = 0; i < events.Count; i++)
                {
                    AbilityEffectTimelineEventDraft current = events[i];
                    if (current == null
                        || !string.Equals(current.EventId, _eventId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (match >= 0)
                    {
                        index = -1;
                        return false;
                    }

                    match = i;
                }

                if (match >= 0)
                {
                    index = match;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        public void SetRange(int startFrame, int endFrame)
        {
            AbilityEffectTimelineEventDraft evt = GetEvent();
            if (evt == null)
                return;

            int safeStart = Math.Max(0, startFrame);
            evt.Frame = safeStart;
            if (CanUseRange(evt))
                evt.EndFrame = Math.Max(safeStart, endFrame);
            else
                evt.EndFrame = -1;
        }

        public void Remove()
        {
            if (_timeline.Events == null)
                return;

            if (TryGetCurrentIndex(out int index) && index >= 0 && index < _timeline.Events.Count)
                _timeline.Events.RemoveAt(index);
        }

        public AbilityEffectTimelineEventDraft GetEvent()
        {
            List<AbilityEffectTimelineEventDraft> events =
                _timeline.Events ?? new List<AbilityEffectTimelineEventDraft>();
            return TryGetCurrentIndex(out int index) && index >= 0 && index < events.Count
                ? events[index]
                : null;
        }

        private static bool IsRangeEvent(AbilityEffectTimelineEventDraft evt)
        {
            return CanUseRange(evt) && evt.EndFrame > evt.Frame;
        }

        private static bool CanUseRange(AbilityEffectTimelineEventDraft evt)
        {
            return evt != null
                && string.Equals(
                    evt.EventKind,
                    AbilityEffectTimelineEventKind.ShapeQueryWindow.ToString(),
                    StringComparison.Ordinal);
        }

        private static TimelineTrackKind ResolveTrackKind(AbilityEffectTimelineEventDraft evt)
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

        private static string BuildSummary(AbilityEffectTimelineEventDraft evt, bool compact)
        {
            if (evt == null)
                return "Ability event";

            string eventKind = string.IsNullOrWhiteSpace(evt.EventKind) ? "Event" : evt.EventKind;
            if (compact)
                return eventKind;

            string id = string.IsNullOrWhiteSpace(evt.EventId) ? "unset" : evt.EventId;
            string anchor = string.IsNullOrWhiteSpace(evt.AnchorMode) ? "CapturedAnchor" : evt.AnchorMode;
            string shape = string.IsNullOrWhiteSpace(evt.ShapeKind) ? "None" : evt.ShapeKind;
            string target = string.IsNullOrWhiteSpace(evt.TargetTimelineId) ? "-" : evt.TargetTimelineId;
            string request = string.IsNullOrWhiteSpace(evt.RequestId) ? "-" : evt.RequestId;
            return eventKind
                + " | id=" + id
                + " | anchor=" + anchor
                + " | shape=" + shape
                + " r=" + evt.Radius.ToString("0.###")
                + " | target=" + target
                + " | request=" + request;
        }
    }
}
