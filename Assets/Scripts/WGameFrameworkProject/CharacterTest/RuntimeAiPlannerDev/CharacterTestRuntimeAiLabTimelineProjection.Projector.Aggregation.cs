using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabTimelineProjector
    {
        private List<CharacterTestRuntimeAiLabTimelineItem> AggregateMarkers(
            List<CharacterTestRuntimeAiLabTimelineItem> source,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            if (source.Count < 2)
                return source;
            List<CharacterTestRuntimeAiLabTimelineItem> stacked = StackExactFrameMarkers(source);
            if (stacked.Count < 2)
                return stacked;
            List<CharacterTestRuntimeAiLabTimelineItem> result = _aggregateScratch;
            result.Clear();
            int index = 0;
            while (index < stacked.Count)
            {
                CharacterTestRuntimeAiLabTimelineItem first = stacked[index];
                if (first.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Span)
                {
                    result.Add(first);
                    index++;
                    continue;
                }
                int end = index + 1;
                float firstX = ToPixel(first.StartFrame, viewport);
                CharacterTestRuntimeAiLabTimelineSeverity maxSeverity = first.Severity;
                ulong hash = StableHash(first.StableKey);
                while (end < stacked.Count)
                {
                    CharacterTestRuntimeAiLabTimelineItem candidate = stacked[end];
                    if (candidate.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Span
                        || candidate.StartFrame == first.StartFrame
                        || ToPixel(candidate.StartFrame, viewport) - firstX >= ClusterDistancePixels)
                    {
                        break;
                    }
                    if (candidate.Severity > maxSeverity)
                        maxSeverity = candidate.Severity;
                    hash = Mix(hash, StableHash(candidate.StableKey));
                    end++;
                }
                int count = end - index;
                if (count == 1)
                {
                    result.Add(first);
                }
                else
                {
                    result.Add(new CharacterTestRuntimeAiLabTimelineItem(
                        "cluster:" + (int)first.TrackKind + ":" + BuildViewportAggregationKey(viewport) + ":" + hash.ToString("X16", CultureInfo.InvariantCulture),
                        first.TrackKind,
                        CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate,
                        CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate,
                        first.StartFrame,
                        stacked[end - 1].EndFrame,
                        false,
                        first.AgentEntityId,
                        0,
                        "DensityAggregate",
                        maxSeverity,
                        count.ToString(CultureInfo.InvariantCulture),
                        count + " 个跨帧高密度事件；只能缩放并居中。",
                        count,
                        string.Empty,
                        first.SourceOrder,
                        members: CopyMembers(stacked, index, count),
                        sessionGeneration: first.SessionGeneration,
                        recordGeneration: first.RecordGeneration,
                        authorityHash: first.AuthorityHash));
                }
                index = end;
            }
            return result;
        }

        private List<CharacterTestRuntimeAiLabTimelineItem> StackExactFrameMarkers(
            List<CharacterTestRuntimeAiLabTimelineItem> source)
        {
            List<CharacterTestRuntimeAiLabTimelineItem> result = _stackScratch;
            result.Clear();
            int index = 0;
            while (index < source.Count)
            {
                CharacterTestRuntimeAiLabTimelineItem first = source[index];
                if (first.ItemKind != CharacterTestRuntimeAiLabTimelineItemKind.Marker)
                {
                    result.Add(first);
                    index++;
                    continue;
                }

                int end = index + 1;
                CharacterTestRuntimeAiLabTimelineSeverity severity = first.Severity;
                ulong hash = StableHash(first.StableKey);
                while (end < source.Count)
                {
                    CharacterTestRuntimeAiLabTimelineItem candidate = source[end];
                    if (candidate.ItemKind != CharacterTestRuntimeAiLabTimelineItemKind.Marker
                        || candidate.StartFrame != first.StartFrame)
                    {
                        break;
                    }
                    if (candidate.Severity > severity)
                        severity = candidate.Severity;
                    hash = Mix(hash, StableHash(candidate.StableKey));
                    end++;
                }

                int count = end - index;
                if (count == 1)
                {
                    result.Add(first);
                }
                else
                {
                    result.Add(new CharacterTestRuntimeAiLabTimelineItem(
                        "stack:" + (int)first.TrackKind + ":" + first.StartFrame + ":" + hash.ToString("X16", CultureInfo.InvariantCulture),
                        first.TrackKind,
                        CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack,
                        CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate,
                        first.StartFrame,
                        first.StartFrame,
                        false,
                        first.AgentEntityId,
                        0,
                        "ExactFrameStack",
                        severity,
                        count.ToString(CultureInfo.InvariantCulture),
                        count + " 个同帧事件；可展开选择精确 typed leaf。",
                        count,
                        string.Empty,
                        first.SourceOrder,
                        members: CopyMembers(source, index, count),
                        sessionGeneration: first.SessionGeneration,
                        recordGeneration: first.RecordGeneration,
                        authorityHash: first.AuthorityHash));
                }
                index = end;
            }
            return result;
        }

        private static CharacterTestRuntimeAiLabTimelineItem[] CopyMembers(
            List<CharacterTestRuntimeAiLabTimelineItem> source,
            int start,
            int count)
        {
            var members = new CharacterTestRuntimeAiLabTimelineItem[count];
            source.CopyTo(start, members, 0, count);
            return members;
        }

        private static void CanonicalizeSamples(List<CanonicalTimelineSample> samples)
        {
            samples.Sort(CompareCanonicalSamples);
            CharacterTestRuntimeAiLabTimelineEventSample previous = default;
            bool hasPrevious = false;
            int occurrenceRank = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                CharacterTestRuntimeAiLabTimelineEventSample sample = samples[i].Sample;
                if (hasPrevious && CompareSamples(previous, sample) == 0)
                    occurrenceRank++;
                else
                    occurrenceRank = 0;
                samples[i] = new CanonicalTimelineSample(sample, occurrenceRank);
                previous = sample;
                hasPrevious = true;
            }
        }

        private static int CompareCanonicalSamples(CanonicalTimelineSample left, CanonicalTimelineSample right)
        {
            int value = CompareSamples(left.Sample, right.Sample);
            return value != 0 ? value : left.OccurrenceRank.CompareTo(right.OccurrenceRank);
        }

        private static int CompareSamples(
            in CharacterTestRuntimeAiLabTimelineEventSample left,
            in CharacterTestRuntimeAiLabTimelineEventSample right)
        {
            int value = left.Frame.CompareTo(right.Frame);
            if (value != 0) return value;
            value = left.Kind.CompareTo(right.Kind);
            if (value != 0) return value;
            value = left.EntityId.CompareTo(right.EntityId);
            if (value != 0) return value;
            value = left.RelatedEntityId.CompareTo(right.RelatedEntityId);
            if (value != 0) return value;
            value = string.CompareOrdinal(left.Category, right.Category);
            if (value != 0) return value;
            value = left.Severity.CompareTo(right.Severity);
            if (value != 0) return value;
            value = left.SourceTraceId.KindId.CompareTo(right.SourceTraceId.KindId);
            if (value == 0) value = left.SourceTraceId.SourceId.CompareTo(right.SourceTraceId.SourceId);
            if (value == 0) value = left.SourceTraceId.ExecutionId.CompareTo(right.SourceTraceId.ExecutionId);
            if (value == 0) value = left.SourceTraceId.Sequence.CompareTo(right.SourceTraceId.Sequence);
            if (value != 0) return value;
            value = left.TraceNumber.CompareTo(right.TraceNumber);
            if (value != 0) return value;
            value = left.InstanceId.CompareTo(right.InstanceId);
            if (value != 0) return value;
            value = left.ProfileSlot.CompareTo(right.ProfileSlot);
            if (value != 0) return value;
            value = left.CardInstanceStartFrame.CompareTo(right.CardInstanceStartFrame);
            if (value != 0) return value;
            value = string.CompareOrdinal(left.BehaviorEntryId, right.BehaviorEntryId);
            if (value != 0) return value;
            value = string.CompareOrdinal(left.BehaviorStepId, right.BehaviorStepId);
            if (value != 0) return value;
            value = left.Value.CompareTo(right.Value);
            if (value != 0) return value;
            return string.CompareOrdinal(left.Label, right.Label);
        }

        private void EnforceGlobalItemLimit(
            CharacterTestRuntimeAiLabTimelineTrackProjection[] tracks,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            int count = 0;
            for (int i = 0; i < tracks.Length; i++)
                count += tracks[i].Items.Count;
            if (count <= MaxVisibleItems)
                return;

            int perTrack = Math.Max(1, MaxVisibleItems / tracks.Length);
            for (int i = 0; i < tracks.Length; i++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track = tracks[i];
                if (track.Items.Count <= perTrack)
                    continue;
                List<CharacterTestRuntimeAiLabTimelineItem> reduced = _limitScratch;
                reduced.Clear();
                int bucketSize = (track.Items.Count + perTrack - 1) / perTrack;
                for (int start = 0; start < track.Items.Count; start += bucketSize)
                {
                    int end = Math.Min(track.Items.Count, start + bucketSize);
                    CharacterTestRuntimeAiLabTimelineItem[] members = FlattenLeafRange(track.Items, start, end);
                    if (members.Length == 0)
                        continue;
                    CharacterTestRuntimeAiLabTimelineItem first = members[0];
                    CharacterTestRuntimeAiLabTimelineSeverity severity = first.Severity;
                    ulong hash = 0;
                    int rangeStart = first.StartFrame;
                    int rangeEnd = first.EndFrame;
                    for (int itemIndex = 0; itemIndex < members.Length; itemIndex++)
                    {
                        CharacterTestRuntimeAiLabTimelineItem member = members[itemIndex];
                        if (member.Severity > severity)
                            severity = member.Severity;
                        if (member.StartFrame < rangeStart)
                            rangeStart = member.StartFrame;
                        if (member.EndFrame > rangeEnd)
                            rangeEnd = member.EndFrame;
                        hash = Mix(hash, StableHash(member.StableKey));
                    }
                    reduced.Add(new CharacterTestRuntimeAiLabTimelineItem(
                        "limit-cluster:" + (int)track.Kind + ":" + BuildViewportAggregationKey(viewport) + ":" + hash.ToString("X16", CultureInfo.InvariantCulture),
                        track.Kind,
                        CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate,
                        CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate,
                        rangeStart,
                        rangeEnd,
                        false,
                        first.AgentEntityId,
                        0,
                        "DensityAggregate",
                        severity,
                        members.Length.ToString(CultureInfo.InvariantCulture),
                        "高密度事件聚合",
                        members.Length,
                        string.Empty,
                        first.SourceOrder,
                        members: members,
                        sessionGeneration: first.SessionGeneration,
                        recordGeneration: first.RecordGeneration,
                        authorityHash: first.AuthorityHash));
                }
                tracks[i] = new CharacterTestRuntimeAiLabTimelineTrackProjection(
                    track.Kind,
                    track.Label,
                    reduced.ToArray(),
                    track.Available,
                    track.UnavailableMessage);
            }
        }

        private static bool IsActionEvent(CharacterTestRuntimeAiLabTimelineEventKind kind)
        {
            return kind == CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.ActionEnded
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.ActionEvent
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.Command;
        }

        private static bool IsDecisionEvent(CharacterTestRuntimeAiLabTimelineEventKind kind)
        {
            return kind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorDetectorTriggered
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionPending
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorCardEnded;
        }

        private static string BuildViewportAggregationKey(
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            return ((int)viewport.Mode).ToString(CultureInfo.InvariantCulture)
                + ":" + viewport.ZoomLevel.ToString(CultureInfo.InvariantCulture)
                + ":" + viewport.StartFrame.ToString(CultureInfo.InvariantCulture)
                + ":" + viewport.EndFrame.ToString(CultureInfo.InvariantCulture)
                + ":" + viewport.PixelWidth.ToString("R", CultureInfo.InvariantCulture)
                + ":" + (viewport.FollowLatest ? "1" : "0");
        }

        private static CharacterTestRuntimeAiLabTimelineItem[] FlattenLeafRange(
            IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items,
            int start,
            int end)
        {
            int count = 0;
            for (int i = start; i < end; i++)
            {
                CharacterTestRuntimeAiLabTimelineItem item = items[i];
                count += item.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate
                    ? item.Members.Count
                    : 1;
            }
            var result = new CharacterTestRuntimeAiLabTimelineItem[count];
            int offset = 0;
            for (int i = start; i < end; i++)
            {
                CharacterTestRuntimeAiLabTimelineItem item = items[i];
                if (item.Provenance != CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate)
                {
                    result[offset++] = item;
                    continue;
                }
                for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                    result[offset++] = item.Members[memberIndex];
            }
            Array.Sort(result, CompareItems);
            return result;
        }

        private static bool IsCombatEvent(CharacterTestRuntimeAiLabTimelineEventKind kind)
        {
            return kind == CharacterTestRuntimeAiLabTimelineEventKind.CombatHit
                || kind == CharacterTestRuntimeAiLabTimelineEventKind.Damage;
        }

        private static bool Overlaps(int start, int end, CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            return end >= viewport.StartFrame && start <= viewport.EndFrame;
        }

        private static void TrimOutsideViewport(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (!Overlaps(items[i].StartFrame, items[i].EndFrame, viewport))
                    items.RemoveAt(i);
            }
        }

        private static void SortItems(List<CharacterTestRuntimeAiLabTimelineItem> items)
        {
            items.Sort(CompareItems);
        }

        private static int CompareItems(
            CharacterTestRuntimeAiLabTimelineItem left,
            CharacterTestRuntimeAiLabTimelineItem right)
        {
            int value = left.StartFrame.CompareTo(right.StartFrame);
            if (value != 0)
                return value;
            value = left.EndFrame.CompareTo(right.EndFrame);
            if (value != 0)
                return value;
            value = left.ItemKind.CompareTo(right.ItemKind);
            if (value != 0)
                return value;
            value = right.Severity.CompareTo(left.Severity);
            if (value != 0)
                return value;
            value = left.AgentEntityId.CompareTo(right.AgentEntityId);
            if (value != 0)
                return value;
            value = left.RelatedEntityId.CompareTo(right.RelatedEntityId);
            if (value != 0)
                return value;
            value = string.CompareOrdinal(left.Category, right.Category);
            if (value != 0)
                return value;
            value = string.CompareOrdinal(left.SourceTraceId, right.SourceTraceId);
            if (value != 0)
                return value;
            value = left.SourceOrder.CompareTo(right.SourceOrder);
            return value != 0 ? value : string.CompareOrdinal(left.StableKey, right.StableKey);
        }

        private static float ToPixel(int frame, CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            return (frame - viewport.StartFrame) * viewport.PixelWidth / Math.Max(1, viewport.EndFrame - viewport.StartFrame);
        }

        private static ulong StableHash(string value)
        {
            ulong hash = 1469598103934665603UL;
            string safe = value ?? string.Empty;
            for (int i = 0; i < safe.Length; i++)
            {
                hash ^= safe[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private static ulong Mix(ulong left, ulong right)
        {
            return (left ^ right) * 1099511628211UL;
        }

        private readonly struct ActionInstanceKey : IEquatable<ActionInstanceKey>
        {
            public ActionInstanceKey(int entityId, long instanceId)
            {
                EntityId = entityId;
                InstanceId = instanceId;
            }

            private int EntityId { get; }
            private long InstanceId { get; }

            public bool Equals(ActionInstanceKey other)
            {
                return EntityId == other.EntityId && InstanceId == other.InstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is ActionInstanceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId * 397) ^ InstanceId.GetHashCode();
                }
            }
        }

        private readonly struct OpenActionSample
        {
            internal OpenActionSample(ActionInstanceKey key, CanonicalTimelineSample sample)
            {
                Key = key;
                Sample = sample;
            }

            internal ActionInstanceKey Key { get; }
            internal CanonicalTimelineSample Sample { get; }
        }

        private readonly struct CanonicalTimelineSample
        {
            public CanonicalTimelineSample(
                CharacterTestRuntimeAiLabTimelineEventSample sample,
                int occurrenceRank)
            {
                Sample = sample;
                OccurrenceRank = Math.Max(0, occurrenceRank);
            }

            public CharacterTestRuntimeAiLabTimelineEventSample Sample { get; }
            public int OccurrenceRank { get; }
        }
    }
}
