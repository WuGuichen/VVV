using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Adapters
{
    internal sealed class AnimationTrackTimelineAdapter : ITimelineItemAdapter, IAnimationTimelineClipAdapter
    {
        private readonly CharacterActionJsonDto _action;
        private readonly AnimationTrackEventJsonDto _eventRef;
        private readonly string _stableEventId;
        private readonly string _id;
        private readonly Color _color;

        public AnimationTrackTimelineAdapter(CharacterActionJsonDto action, int index, Color color, bool canEdit)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            AnimationTrackEventJsonDto[] events = action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            _eventRef = index >= 0 && index < events.Length ? events[index] : null;
            _stableEventId = CharacterActionAuthoringIds.Label(_eventRef?.StableEventId ?? 0, 401);
            _id = CharacterActionTimelineIdentity.ItemId(TimelineTrackKind.Animation, _eventRef, _stableEventId);
            _color = color;
            CanEdit = canEdit;
        }

        public string Id => _id;
        public TimelineTrackKind TrackKind => TimelineTrackKind.Animation;
        public TimelineItemKind ItemKind => TimelineItemKind.Clip;
        public bool CanEdit { get; }

        public int StartFrame => GetEvent()?.Frame ?? 0;

        public int EndFrame
        {
            get
            {
                AnimationTrackEventJsonDto evt = GetEvent();
                if (evt == null)
                    return 0;
                return Math.Max(evt.Frame, evt.EndFrame);
            }
        }

        public float TransitionSeconds => GetEvent()?.TransitionSeconds ?? 0f;

        public int TransitionFrames =>
            CharacterActionTimelineLayout.TransitionFramesFromSeconds(TransitionSeconds);

        public string Title
        {
            get
            {
                AnimationTrackEventJsonDto evt = GetEvent();
                if (evt == null)
                    return "?";
                return CharacterActionAuthoringText.AnimationSummary(evt, compact: true);
            }
        }

        public string FullTitle
        {
            get
            {
                AnimationTrackEventJsonDto evt = GetEvent();
                if (evt == null)
                    return "?";
                return CharacterActionAuthoringText.AnimationSummary(evt);
            }
        }

        public Color Color => _color;

        public AnimationTrackEventJsonDto GetEvent() => GetAnimation();

        public bool TryGetCurrentIndex(out int index)
        {
            AnimationTrackEventJsonDto[] events = _action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            if (_eventRef != null)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    if (ReferenceEquals(events[i], _eventRef))
                    {
                        index = i;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_stableEventId))
            {
                int match = -1;
                for (int i = 0; i < events.Length; i++)
                {
                    AnimationTrackEventJsonDto evt = events[i];
                    if (evt == null
                        || !string.Equals(CharacterActionAuthoringIds.Label(evt.StableEventId, 401), _stableEventId, StringComparison.Ordinal))
                        continue;
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
            if (!CanEdit)
                return;

            AnimationTrackEventJsonDto evt = GetAnimation();
            if (evt == null)
                return;

            int start = Math.Min(startFrame, endFrame);
            int end = Math.Max(startFrame, endFrame);
            evt.Frame = start;
            evt.EndFrame = end;
        }

        public void SetAnimationRange(
            int startFrame,
            int endFrame,
            bool trimSource,
            int actionDurationFrames,
            string characterStableId)
        {
            if (!CanEdit)
                return;

            CharacterActionAnimationTimelineResolver.ApplyRange(
                GetAnimation(),
                startFrame,
                endFrame,
                trimSource,
                actionDurationFrames,
                characterStableId);
        }

        public void Remove()
        {
            if (!CanEdit)
                return;

            var events = new System.Collections.Generic.List<AnimationTrackEventJsonDto>(
                _action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>());
            if (TryGetCurrentIndex(out int index))
                events.RemoveAt(index);
            if (_action.AnimationTrack != null)
                _action.AnimationTrack.Events = events.ToArray();
        }

        private AnimationTrackEventJsonDto GetAnimation()
        {
            AnimationTrackEventJsonDto[] events = _action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            return TryGetCurrentIndex(out int index) && index < events.Length ? events[index] : null;
        }

    }
}
