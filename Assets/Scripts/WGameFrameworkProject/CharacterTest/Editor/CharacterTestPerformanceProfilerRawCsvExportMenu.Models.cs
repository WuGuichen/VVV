using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest.Editor
{
    public static partial class CharacterTestPerformanceProfilerRawCsvExportMenu
    {
        private readonly struct ProfilerFrameTimeCsvRow
        {
            public ProfilerFrameTimeCsvRow(
                int frameOffset,
                int frameIndex,
                double frameTimeMs,
                double timeFromFirstFrameMs,
                long gcAllocBytes)
            {
                FrameOffset = frameOffset;
                FrameIndex = frameIndex;
                FrameTimeMs = frameTimeMs;
                TimeFromFirstFrameMs = timeFromFirstFrameMs;
                GcAllocBytes = gcAllocBytes;
            }

            public int FrameOffset { get; }
            public int FrameIndex { get; }
            public double FrameTimeMs { get; }
            public double TimeFromFirstFrameMs { get; }
            public long GcAllocBytes { get; }
        }

        private readonly struct ProfilerRawSampleRecord
        {
            public ProfilerRawSampleRecord(
                string markerName,
                double timeMs,
                int depth,
                long gcBytes)
            {
                MarkerName = markerName ?? string.Empty;
                TimeMs = Math.Max(0d, timeMs);
                Depth = Math.Max(0, depth);
                GcBytes = Math.Max(0L, gcBytes);
            }

            public string MarkerName { get; }
            public double TimeMs { get; }
            public int Depth { get; }
            public long GcBytes { get; }
        }

        private sealed class ProfilerMarkerTimingAggregate
        {
            private readonly Dictionary<int, ProfilerMarkerFrameAggregate> _frames =
                new Dictionary<int, ProfilerMarkerFrameAggregate>();

            private int _minDepth = int.MaxValue;
            private int _maxDepth;
            private double _minIndividualTimeMs = double.MaxValue;
            private double _maxIndividualTimeMs;
            private int _minIndividualFrameIndex;
            private int _maxIndividualFrameIndex;

            public ProfilerMarkerTimingAggregate(string name)
            {
                Name = name ?? string.Empty;
            }

            public string Name { get; }

            public void Record(
                int frameIndex,
                double timeMs,
                int depth,
                long gcBytes,
                int gcCount)
            {
                if (!_frames.TryGetValue(frameIndex, out ProfilerMarkerFrameAggregate frame))
                    frame = new ProfilerMarkerFrameAggregate();

                frame.TimeMs += Math.Max(0d, timeMs);
                frame.Count++;
                frame.GcBytes += Math.Max(0L, gcBytes);
                frame.GcCount += Math.Max(0, gcCount);
                _frames[frameIndex] = frame;

                _minDepth = Math.Min(_minDepth, depth);
                _maxDepth = Math.Max(_maxDepth, depth);
                if (timeMs < _minIndividualTimeMs)
                {
                    _minIndividualTimeMs = timeMs;
                    _minIndividualFrameIndex = frameIndex;
                }

                if (timeMs > _maxIndividualTimeMs)
                {
                    _maxIndividualTimeMs = timeMs;
                    _maxIndividualFrameIndex = frameIndex;
                }
            }

            public ProfilerMarkerTimingCsvRow CreateRow()
            {
                var frameItems = new List<KeyValuePair<int, ProfilerMarkerFrameAggregate>>(_frames);
                frameItems.Sort((left, right) => left.Key.CompareTo(right.Key));

                var timeValues = new List<double>(frameItems.Count);
                var countValues = new List<double>(frameItems.Count);
                var gcValues = new List<long>(frameItems.Count);
                double totalTimeMs = 0d;
                int countTotal = 0;
                long gcTotal = 0L;
                int gcCount = 0;
                int countMin = int.MaxValue;
                int countMax = 0;
                for (int i = 0; i < frameItems.Count; i++)
                {
                    ProfilerMarkerFrameAggregate frame = frameItems[i].Value;
                    timeValues.Add(frame.TimeMs);
                    countValues.Add(frame.Count);
                    gcValues.Add(frame.GcBytes);
                    totalTimeMs += frame.TimeMs;
                    countTotal += frame.Count;
                    gcTotal += frame.GcBytes;
                    gcCount += frame.GcCount;
                    countMin = Math.Min(countMin, frame.Count);
                    countMax = Math.Max(countMax, frame.Count);
                }

                double medianTimeMs = Percentile(timeValues, 0.5d);
                ChooseNearestFrameValue(frameItems, medianTimeMs, frame => frame.TimeMs, out int medianFrameIndex, out double timeAtMedianFrameMs);
                ChooseMinFrameValue(frameItems, frame => frame.TimeMs, out int minFrameIndex, out double minTimeMs);
                ChooseMaxFrameValue(frameItems, frame => frame.TimeMs, out int maxFrameIndex, out double maxTimeMs);
                long gcMedianBytes = Percentile(gcValues, 0.5d);
                ChooseNearestFrameValue(frameItems, gcMedianBytes, frame => frame.GcBytes, out int gcMedianFrameIndex, out _);
                ChooseMinFrameValue(frameItems, frame => frame.GcBytes, out int gcMinFrameIndex, out long gcMinBytes);
                ChooseMaxFrameValue(frameItems, frame => frame.GcBytes, out int gcMaxFrameIndex, out long gcMaxBytes);

                return new ProfilerMarkerTimingCsvRow(
                    Name,
                    medianTimeMs,
                    minTimeMs,
                    maxTimeMs,
                    medianFrameIndex,
                    minFrameIndex,
                    maxFrameIndex,
                    _minDepth == int.MaxValue ? 0 : _minDepth,
                    _maxDepth,
                    totalTimeMs,
                    frameItems.Count == 0 ? 0d : totalTimeMs / frameItems.Count,
                    Percentile(timeValues, 0.25d),
                    Percentile(timeValues, 0.75d),
                    countTotal,
                    (int)Math.Round(Percentile(countValues, 0.5d)),
                    countMin == int.MaxValue ? 0 : countMin,
                    countMax,
                    frameItems.Count,
                    frameItems.Count == 0 ? 0 : frameItems[0].Key,
                    _minIndividualTimeMs == double.MaxValue ? 0d : _minIndividualTimeMs,
                    _maxIndividualTimeMs,
                    _minIndividualFrameIndex,
                    _maxIndividualFrameIndex,
                    timeAtMedianFrameMs,
                    gcTotal,
                    gcCount,
                    frameItems.Count == 0 ? 0L : (long)Math.Round((double)gcTotal / frameItems.Count),
                    gcMedianBytes,
                    gcMinBytes,
                    gcMaxBytes,
                    Percentile(gcValues, 0.25d),
                    Percentile(gcValues, 0.75d),
                    gcMedianFrameIndex,
                    gcMinFrameIndex,
                    gcMaxFrameIndex);
            }

            private static void ChooseNearestFrameValue<T>(
                List<KeyValuePair<int, ProfilerMarkerFrameAggregate>> frameItems,
                double target,
                Func<ProfilerMarkerFrameAggregate, T> selector,
                out int frameIndex,
                out double value)
                where T : IConvertible
            {
                frameIndex = 0;
                value = 0d;
                double bestDistance = double.MaxValue;
                for (int i = 0; i < frameItems.Count; i++)
                {
                    double candidate = selector(frameItems[i].Value).ToDouble(CultureInfo.InvariantCulture);
                    double distance = Math.Abs(candidate - target);
                    if (distance > bestDistance)
                        continue;
                    if (distance == bestDistance && frameItems[i].Key >= frameIndex)
                        continue;

                    bestDistance = distance;
                    frameIndex = frameItems[i].Key;
                    value = candidate;
                }
            }

            private static void ChooseMinFrameValue<T>(
                List<KeyValuePair<int, ProfilerMarkerFrameAggregate>> frameItems,
                Func<ProfilerMarkerFrameAggregate, T> selector,
                out int frameIndex,
                out T value)
                where T : IComparable<T>
            {
                frameIndex = 0;
                value = default;
                bool found = false;
                for (int i = 0; i < frameItems.Count; i++)
                {
                    T candidate = selector(frameItems[i].Value);
                    if (found && candidate.CompareTo(value) >= 0)
                        continue;

                    found = true;
                    frameIndex = frameItems[i].Key;
                    value = candidate;
                }
            }

            private static void ChooseMaxFrameValue<T>(
                List<KeyValuePair<int, ProfilerMarkerFrameAggregate>> frameItems,
                Func<ProfilerMarkerFrameAggregate, T> selector,
                out int frameIndex,
                out T value)
                where T : IComparable<T>
            {
                frameIndex = 0;
                value = default;
                bool found = false;
                for (int i = 0; i < frameItems.Count; i++)
                {
                    T candidate = selector(frameItems[i].Value);
                    if (found && candidate.CompareTo(value) <= 0)
                        continue;

                    found = true;
                    frameIndex = frameItems[i].Key;
                    value = candidate;
                }
            }
        }

        private struct ProfilerMarkerFrameAggregate
        {
            public double TimeMs;
            public int Count;
            public long GcBytes;
            public int GcCount;
        }

        private readonly struct ProfilerMarkerTimingCsvRow
        {
            public ProfilerMarkerTimingCsvRow(
                string name,
                double medianTimeMs,
                double minTimeMs,
                double maxTimeMs,
                int medianFrameIndex,
                int minFrameIndex,
                int maxFrameIndex,
                int minDepth,
                int maxDepth,
                double totalTimeMs,
                double meanTimeMs,
                double lowerQuartileTimeMs,
                double upperQuartileTimeMs,
                int countTotal,
                int countMedian,
                int countMin,
                int countMax,
                int frameCount,
                int firstFrameIndex,
                double minIndividualTimeMs,
                double maxIndividualTimeMs,
                int minIndividualFrameIndex,
                int maxIndividualFrameIndex,
                double timeAtMedianFrameMs,
                long gcTotalBytes,
                int gcCount,
                long gcMeanBytes,
                long gcMedianBytes,
                long gcMinBytes,
                long gcMaxBytes,
                long gcLowerQuartileBytes,
                long gcUpperQuartileBytes,
                int gcMedianFrameIndex,
                int gcMinFrameIndex,
                int gcMaxFrameIndex)
            {
                Name = name ?? string.Empty;
                MedianTimeMs = medianTimeMs;
                MinTimeMs = minTimeMs;
                MaxTimeMs = maxTimeMs;
                MedianFrameIndex = medianFrameIndex;
                MinFrameIndex = minFrameIndex;
                MaxFrameIndex = maxFrameIndex;
                MinDepth = minDepth;
                MaxDepth = maxDepth;
                TotalTimeMs = totalTimeMs;
                MeanTimeMs = meanTimeMs;
                LowerQuartileTimeMs = lowerQuartileTimeMs;
                UpperQuartileTimeMs = upperQuartileTimeMs;
                CountTotal = countTotal;
                CountMedian = countMedian;
                CountMin = countMin;
                CountMax = countMax;
                FrameCount = frameCount;
                FirstFrameIndex = firstFrameIndex;
                MinIndividualTimeMs = minIndividualTimeMs;
                MaxIndividualTimeMs = maxIndividualTimeMs;
                MinIndividualFrameIndex = minIndividualFrameIndex;
                MaxIndividualFrameIndex = maxIndividualFrameIndex;
                TimeAtMedianFrameMs = timeAtMedianFrameMs;
                GcTotalBytes = gcTotalBytes;
                GcCount = gcCount;
                GcMeanBytes = gcMeanBytes;
                GcMedianBytes = gcMedianBytes;
                GcMinBytes = gcMinBytes;
                GcMaxBytes = gcMaxBytes;
                GcLowerQuartileBytes = gcLowerQuartileBytes;
                GcUpperQuartileBytes = gcUpperQuartileBytes;
                GcMedianFrameIndex = gcMedianFrameIndex;
                GcMinFrameIndex = gcMinFrameIndex;
                GcMaxFrameIndex = gcMaxFrameIndex;
            }

            public string Name { get; }
            public double MedianTimeMs { get; }
            public double MinTimeMs { get; }
            public double MaxTimeMs { get; }
            public int MedianFrameIndex { get; }
            public int MinFrameIndex { get; }
            public int MaxFrameIndex { get; }
            public int MinDepth { get; }
            public int MaxDepth { get; }
            public double TotalTimeMs { get; }
            public double MeanTimeMs { get; }
            public double LowerQuartileTimeMs { get; }
            public double UpperQuartileTimeMs { get; }
            public int CountTotal { get; }
            public int CountMedian { get; }
            public int CountMin { get; }
            public int CountMax { get; }
            public int FrameCount { get; }
            public int FirstFrameIndex { get; }
            public double MinIndividualTimeMs { get; }
            public double MaxIndividualTimeMs { get; }
            public int MinIndividualFrameIndex { get; }
            public int MaxIndividualFrameIndex { get; }
            public double TimeAtMedianFrameMs { get; }
            public long GcTotalBytes { get; }
            public int GcCount { get; }
            public long GcMeanBytes { get; }
            public long GcMedianBytes { get; }
            public long GcMinBytes { get; }
            public long GcMaxBytes { get; }
            public long GcLowerQuartileBytes { get; }
            public long GcUpperQuartileBytes { get; }
            public int GcMedianFrameIndex { get; }
            public int GcMinFrameIndex { get; }
            public int GcMaxFrameIndex { get; }
        }
    }
}
