using System;
using System.Collections.Generic;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed class CharacterActionTimelineItemPlacement
    {
        public CharacterActionTimelineItemPlacement(ITimelineItemAdapter adapter, int lane, float left, float right)
        {
            Adapter = adapter;
            Lane = lane;
            Left = left;
            Right = right;
        }

        public ITimelineItemAdapter Adapter { get; }
        public int Lane { get; }
        public float Left { get; }
        public float Right { get; }
    }

    internal sealed class CharacterActionTimelineTrackLayout
    {
        private readonly Dictionary<string, CharacterActionTimelineItemPlacement> _placements;

        public CharacterActionTimelineTrackLayout(
            int laneCount,
            float rowHeight,
            Dictionary<string, CharacterActionTimelineItemPlacement> placements)
        {
            LaneCount = Math.Max(1, laneCount);
            RowHeight = rowHeight;
            _placements = placements ?? new Dictionary<string, CharacterActionTimelineItemPlacement>();
        }

        public int LaneCount { get; }
        public float RowHeight { get; }

        public bool TryGetPlacement(string itemId, out CharacterActionTimelineItemPlacement placement)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                placement = null;
                return false;
            }

            return _placements.TryGetValue(itemId, out placement);
        }
    }

    internal static class CharacterActionTimelineLaneLayout
    {
        public static CharacterActionTimelineTrackLayout Build(
            TimelineTrackKind trackKind,
            IReadOnlyList<ITimelineItemAdapter> items,
            float pixelsPerFrame)
        {
            items = items ?? Array.Empty<ITimelineItemAdapter>();
            var candidates = new List<Candidate>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                ITimelineItemAdapter item = items[i];
                if (item == null)
                    continue;
                candidates.Add(CreateCandidate(item, pixelsPerFrame));
            }

            candidates.Sort(CompareCandidates);

            if (trackKind == TimelineTrackKind.Phases)
                return BuildPhaseTrackLayout(candidates);

            var laneRightEdges = new List<float>();
            var placements = new Dictionary<string, CharacterActionTimelineItemPlacement>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                int lane = FindAvailableLane(candidate.Left, laneRightEdges);
                if (lane >= laneRightEdges.Count)
                    laneRightEdges.Add(candidate.Right);
                else
                    laneRightEdges[lane] = candidate.Right;

                placements[candidate.Adapter.Id] = new CharacterActionTimelineItemPlacement(
                    candidate.Adapter,
                    lane,
                    candidate.Left,
                    candidate.Right);
            }

            int laneCount = Math.Max(1, laneRightEdges.Count);
            float minHeight = trackKind == TimelineTrackKind.Animation
                ? CharacterActionTimelineLayout.AnimationRowHeight
                : CharacterActionTimelineLayout.RowHeight;
            float rowHeight = Math.Max(
                minHeight,
                CharacterActionTimelineLayout.LaneVerticalPadding * 2f
                    + laneCount * CharacterActionTimelineLayout.ItemLaneHeight);

            return new CharacterActionTimelineTrackLayout(laneCount, rowHeight, placements);
        }

        private static CharacterActionTimelineTrackLayout BuildPhaseTrackLayout(IReadOnlyList<Candidate> candidates)
        {
            var placements = new Dictionary<string, CharacterActionTimelineItemPlacement>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                placements[candidate.Adapter.Id] = new CharacterActionTimelineItemPlacement(
                    candidate.Adapter,
                    0,
                    candidate.Left,
                    candidate.Right);
            }

            return new CharacterActionTimelineTrackLayout(
                1,
                CharacterActionTimelineLayout.RowHeight,
                placements);
        }

        public static float ClipTop(int lane)
        {
            return CharacterActionTimelineLayout.LaneVerticalPadding
                + lane * CharacterActionTimelineLayout.ItemLaneHeight
                + CharacterActionTimelineLayout.ClipLaneInset;
        }

        public static float MarkerTop(int lane)
        {
            return CharacterActionTimelineLayout.LaneVerticalPadding
                + lane * CharacterActionTimelineLayout.ItemLaneHeight
                + (CharacterActionTimelineLayout.ItemLaneHeight - CharacterActionTimelineLayout.MarkerHeight) * 0.5f;
        }

        private static int FindAvailableLane(float left, List<float> laneRightEdges)
        {
            for (int i = 0; i < laneRightEdges.Count; i++)
            {
                if (left >= laneRightEdges[i] + CharacterActionTimelineLayout.LaneItemGap)
                    return i;
            }

            return laneRightEdges.Count;
        }

        private static Candidate CreateCandidate(ITimelineItemAdapter adapter, float pixelsPerFrame)
        {
            float left;
            float right;
            float pad = CharacterActionTimelineLayout.ContentPadding;
            if (adapter.ItemKind == TimelineItemKind.Marker)
            {
                float centerX = pad
                    + CharacterActionTimelineLayout.FrameToX(adapter.StartFrame, pixelsPerFrame)
                    + pixelsPerFrame * 0.5f;
                float halfWidth = CharacterActionTimelineLayout.MarkerNodeWidth * 0.5f;
                left = centerX - halfWidth;
                right = centerX + halfWidth;
            }
            else
            {
                left = pad + CharacterActionTimelineLayout.FrameToX(adapter.StartFrame, pixelsPerFrame);
                float end = pad + CharacterActionTimelineLayout.FrameToX(adapter.EndFrame + 1, pixelsPerFrame);
                right = Math.Max(left + CharacterActionTimelineLayout.MinClipWidth, end);
            }

            return new Candidate(adapter, left, right);
        }

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            int compare = a.Left.CompareTo(b.Left);
            if (compare != 0)
                return compare;
            compare = a.Right.CompareTo(b.Right);
            if (compare != 0)
                return compare;
            compare = a.Adapter.StartFrame.CompareTo(b.Adapter.StartFrame);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(a.Adapter.Id, b.Adapter.Id);
        }

        private readonly struct Candidate
        {
            public Candidate(ITimelineItemAdapter adapter, float left, float right)
            {
                Adapter = adapter;
                Left = left;
                Right = right;
            }

            public ITimelineItemAdapter Adapter { get; }
            public float Left { get; }
            public float Right { get; }
        }
    }
}
