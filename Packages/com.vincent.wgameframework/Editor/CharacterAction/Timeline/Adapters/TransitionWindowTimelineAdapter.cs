using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Adapters
{
    internal sealed class TransitionWindowTimelineAdapter : ITimelineItemAdapter
    {
        private readonly CharacterActionJsonDto _action;
        private readonly CharacterCancelRuleJsonDto _ruleRef;
        private readonly int _initialIndex;
        private readonly int _durationFrames;
        private readonly string _id;
        private readonly Color _color;

        public TransitionWindowTimelineAdapter(
            CharacterActionJsonDto action,
            int index,
            int durationFrames,
            Color color,
            bool canEdit)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _initialIndex = index;
            _durationFrames = Math.Max(1, durationFrames);
            CharacterCancelRuleJsonDto[] rules = action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            _ruleRef = index >= 0 && index < rules.Length ? rules[index] : null;
            _id = CharacterActionTimelineIdentity.ItemId(
                TimelineTrackKind.TransitionWindow,
                _ruleRef,
                string.Empty);
            _color = color;
            CanEdit = canEdit;
        }

        public string Id => _id;
        public TimelineTrackKind TrackKind => TimelineTrackKind.TransitionWindow;
        public TimelineItemKind ItemKind => TimelineItemKind.Clip;
        public bool CanEdit { get; }
        public int StartFrame => GetRule().StartFrame;
        public int EndFrame => Math.Max(StartFrame, GetRule().EndFrame);
        public string Title => CharacterActionAuthoringText.TransitionWindowSummary(GetRule(), compact: true);
        public string FullTitle => CharacterActionAuthoringText.TransitionWindowSummary(GetRule());
        public Color Color => _color;

        public bool TryGetCurrentIndex(out int index)
        {
            CharacterCancelRuleJsonDto[] rules = _action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            if (_ruleRef != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    if (ReferenceEquals(rules[i], _ruleRef))
                    {
                        index = i;
                        return true;
                    }
                }
            }

            if (_initialIndex >= 0 && _initialIndex < rules.Length && rules[_initialIndex] == null)
            {
                index = _initialIndex;
                return true;
            }

            index = -1;
            return false;
        }

        public CharacterCancelRuleJsonDto GetRule()
        {
            CharacterCancelRuleJsonDto[] rules = _action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            return TryGetCurrentIndex(out int index) && index < rules.Length
                ? rules[index] ?? new CharacterCancelRuleJsonDto()
                : new CharacterCancelRuleJsonDto();
        }

        public void SetRange(int startFrame, int endFrame)
        {
            if (!CanEdit)
                return;

            CharacterCancelRuleJsonDto rule = GetEditableRule();
            if (rule == null)
                return;

            SetTransitionWindowRange(_action, rule, startFrame, endFrame, _durationFrames);
        }

        public void Remove()
        {
            if (!CanEdit)
                return;

            var rules = new List<CharacterCancelRuleJsonDto>(_action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>());
            if (TryGetCurrentIndex(out int index))
                rules.RemoveAt(index);
            _action.CancelRules = rules.ToArray();
        }

        public static CharacterCancelRuleJsonDto CreateDefaultRule(
            CharacterActionJsonDto action,
            int frame,
            int durationFrames)
        {
            var rule = new CharacterCancelRuleJsonDto
            {
                StartFrame = frame,
                EndFrame = Math.Min(Math.Max(0, durationFrames - 1), frame + 2),
                RequestTypeMask = (int)CharacterActionRequestType.Any,
                ActionSlotMask = (int)CharacterCancelInputMask.Any,
            };
            SetTransitionWindowRange(action, rule, rule.StartFrame, rule.EndFrame, durationFrames);
            return rule;
        }

        public static void SetTransitionWindowRange(
            CharacterActionJsonDto action,
            CharacterCancelRuleJsonDto rule,
            int startFrame,
            int endFrame,
            int durationFrames)
        {
            if (rule == null)
                return;

            TransitionWindowRange range = ClampTransitionWindowRange(action, startFrame, endFrame, durationFrames);
            rule.StartFrame = range.StartFrame;
            rule.EndFrame = range.EndFrame;
        }

        public static TransitionWindowRange ClampTransitionWindowRange(
            CharacterActionJsonDto action,
            int startFrame,
            int endFrame,
            int durationFrames)
        {
            int duration = Math.Max(1, durationFrames);
            int safeStart = CharacterActionTimelineLayout.ClampFrame(Math.Min(startFrame, endFrame), duration);
            int safeEnd = CharacterActionTimelineLayout.ClampFrame(Math.Max(startFrame, endFrame), duration);
            List<TransitionWindowRange> spans = BuildSwitchableSpans(action, duration);
            if (spans.Count == 0)
                return new TransitionWindowRange(safeStart, safeEnd);

            TransitionWindowRange span = SelectBestSpan(spans, safeStart, safeEnd);
            int desiredLength = Math.Max(0, safeEnd - safeStart);
            int nextStart;
            int nextEnd;
            if (safeEnd < span.StartFrame)
            {
                nextStart = span.StartFrame;
                nextEnd = Math.Min(span.EndFrame, nextStart + desiredLength);
            }
            else if (safeStart > span.EndFrame)
            {
                nextEnd = span.EndFrame;
                nextStart = Math.Max(span.StartFrame, nextEnd - desiredLength);
            }
            else
            {
                nextStart = Mathf.Clamp(safeStart, span.StartFrame, span.EndFrame);
                nextEnd = Mathf.Clamp(safeEnd, span.StartFrame, span.EndFrame);
            }

            if (nextEnd < nextStart)
                nextEnd = nextStart;
            return new TransitionWindowRange(nextStart, nextEnd);
        }

        private CharacterCancelRuleJsonDto GetEditableRule()
        {
            CharacterCancelRuleJsonDto[] rules = _action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            return TryGetCurrentIndex(out int index) && index < rules.Length ? rules[index] : null;
        }

        private static List<TransitionWindowRange> BuildSwitchableSpans(CharacterActionJsonDto action, int duration)
        {
            CharacterActionPhaseJsonDto[] phases = action?.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            var spans = new List<TransitionWindowRange>();
            for (int i = 0; i < phases.Length; i++)
            {
                CharacterActionPhaseJsonDto phase = phases[i];
                if (!IsSwitchablePhase(phase?.Kind))
                    continue;

                int start = CharacterActionTimelineLayout.ClampFrame(phase.StartFrame, duration);
                int end = CharacterActionTimelineLayout.ClampFrame(Math.Max(phase.StartFrame, phase.EndFrame), duration);
                spans.Add(new TransitionWindowRange(start, end));
            }

            spans.Sort((left, right) => left.StartFrame.CompareTo(right.StartFrame));
            var merged = new List<TransitionWindowRange>(spans.Count);
            for (int i = 0; i < spans.Count; i++)
            {
                TransitionWindowRange span = spans[i];
                if (merged.Count > 0 && span.StartFrame <= merged[merged.Count - 1].EndFrame + 1)
                {
                    TransitionWindowRange previous = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new TransitionWindowRange(
                        previous.StartFrame,
                        Math.Max(previous.EndFrame, span.EndFrame));
                    continue;
                }

                merged.Add(span);
            }

            return merged;
        }

        private static bool IsSwitchablePhase(string kind)
        {
            if (!Enum.TryParse(kind, out CharacterActionPhaseKind parsed))
                return false;

            switch (parsed)
            {
                case CharacterActionPhaseKind.Active:
                case CharacterActionPhaseKind.Recovery:
                case CharacterActionPhaseKind.Loop:
                case CharacterActionPhaseKind.Landing:
                case CharacterActionPhaseKind.Channel:
                case CharacterActionPhaseKind.Hold:
                case CharacterActionPhaseKind.Exit:
                    return true;
                default:
                    return false;
            }
        }

        private static TransitionWindowRange SelectBestSpan(
            IReadOnlyList<TransitionWindowRange> spans,
            int startFrame,
            int endFrame)
        {
            int bestIndex = 0;
            int bestOverlap = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < spans.Count; i++)
            {
                TransitionWindowRange span = spans[i];
                int overlap = Math.Max(0, Math.Min(endFrame, span.EndFrame) - Math.Max(startFrame, span.StartFrame) + 1);
                int distance = 0;
                if (endFrame < span.StartFrame)
                    distance = span.StartFrame - endFrame;
                else if (startFrame > span.EndFrame)
                    distance = startFrame - span.EndFrame;

                if (overlap > bestOverlap || (overlap == bestOverlap && distance < bestDistance))
                {
                    bestIndex = i;
                    bestOverlap = overlap;
                    bestDistance = distance;
                }
            }

            return spans[bestIndex];
        }
    }

    internal readonly struct TransitionWindowRange
    {
        public TransitionWindowRange(int startFrame, int endFrame)
        {
            StartFrame = startFrame;
            EndFrame = Math.Max(startFrame, endFrame);
        }

        public int StartFrame { get; }
        public int EndFrame { get; }
    }
}
