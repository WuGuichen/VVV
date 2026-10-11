using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    public static partial class CharacterTestPerformanceProfilerRawCsvExportMenu
    {
        public const string MenuPath = "MxFramework/CharacterTest/Performance/Export Current Profiler Raw CSV";
        public const string TornadoWindowMenuPath = "MxFramework/CharacterTest/Performance/Export Current Profiler Raw CSV (Tornado Window)";
        public const string DefaultFrameTimeCsvPath = "Temp/ProfileAnalyzer/current_raw_frameTime.csv";
        public const string DefaultMarkerTimingCsvPath = "Temp/ProfileAnalyzer/current_raw_markerTiming.csv";
        public const string DefaultSummaryPath = "Temp/ProfileAnalyzer/current_raw_profiler_export_summary.txt";
        public const string DefaultTornadoWindowFrameTimeCsvPath = "Temp/ProfileAnalyzer/current_raw_tornado_frameTime.csv";
        public const string DefaultTornadoWindowMarkerTimingCsvPath = "Temp/ProfileAnalyzer/current_raw_tornado_markerTiming.csv";
        public const string DefaultTornadoWindowSummaryPath = "Temp/ProfileAnalyzer/current_raw_tornado_profiler_export_summary.txt";
        public const string DefaultMarkerThreadFilter = "Main Thread";
        public const string DefaultTornadoMarkerName = "CharacterTest.InteractionPhysics.TornadoFieldQuery";
        public const int DefaultMaxFrameCount = 30;
        public const int DefaultMarkerWindowFrameRadius = 8;
        public const int DefaultMarkerSearchMaxFrameCount = 240;

        [MenuItem(MenuPath)]
        public static void ExportDefaultProfilerRawCsv()
        {
            string summaryPath = ExportCurrentProfilerRawCsv(
                DefaultFrameTimeCsvPath,
                DefaultMarkerTimingCsvPath,
                DefaultSummaryPath,
                DefaultMarkerThreadFilter,
                DefaultMaxFrameCount);
            Debug.Log("CharacterTest Profiler raw CSV export written: " + summaryPath);
        }

        [MenuItem(TornadoWindowMenuPath)]
        public static void ExportDefaultTornadoWindowProfilerRawCsv()
        {
            string summaryPath = ExportCurrentProfilerRawCsvAroundMarker(
                DefaultTornadoWindowFrameTimeCsvPath,
                DefaultTornadoWindowMarkerTimingCsvPath,
                DefaultTornadoWindowSummaryPath,
                DefaultMarkerThreadFilter,
                DefaultTornadoMarkerName,
                DefaultMarkerWindowFrameRadius,
                DefaultMarkerSearchMaxFrameCount);
            Debug.Log("CharacterTest Tornado profiler raw CSV export written: " + summaryPath);
        }

        /// <remarks>AllocByDesign: editor-only explicit profiler evidence export.</remarks>
        public static string ExportCurrentProfilerRawCsv(
            string frameTimeCsvPath,
            string markerTimingCsvPath,
            string summaryPath,
            string markerThreadFilter = DefaultMarkerThreadFilter,
            int maxFrameCount = 0)
        {
            string resolvedFrameTimeCsvPath = ResolveReportPath(
                frameTimeCsvPath,
                DefaultFrameTimeCsvPath);
            string resolvedMarkerTimingCsvPath = ResolveReportPath(
                markerTimingCsvPath,
                DefaultMarkerTimingCsvPath);
            string resolvedSummaryPath = ResolveReportPath(summaryPath, DefaultSummaryPath);
            int firstRawFrameIndex = ProfilerDriver.firstFrameIndex;
            int lastRawFrameIndex = ProfilerDriver.lastFrameIndex;
            bool hasProfilerFrames = firstRawFrameIndex >= 0 && lastRawFrameIndex >= firstRawFrameIndex;
            if (hasProfilerFrames && maxFrameCount > 0)
                firstRawFrameIndex = Math.Max(firstRawFrameIndex, lastRawFrameIndex - maxFrameCount + 1);
            string filter = string.IsNullOrWhiteSpace(markerThreadFilter)
                ? DefaultMarkerThreadFilter
                : markerThreadFilter.Trim();

            return ExportCurrentProfilerRawCsvInRange(
                resolvedFrameTimeCsvPath,
                resolvedMarkerTimingCsvPath,
                resolvedSummaryPath,
                firstRawFrameIndex,
                lastRawFrameIndex,
                hasProfilerFrames,
                hasProfilerFrames ? "finished" : "no-profiler-frames",
                filter,
                maxFrameCount,
                markerFocusName: string.Empty,
                markerFocusDisplayFrameIndex: 0,
                markerWindowRadius: 0,
                markerSearchMaxFrameCount: 0,
                markerSearchFirstRawFrameIndex: 0,
                markerSearchLastRawFrameIndex: 0,
                markerSearchScannedThreadCount: 0,
                markerSearchScannedSampleCount: 0);
        }

        /// <remarks>AllocByDesign: editor-only explicit profiler evidence export.</remarks>
        public static string ExportCurrentProfilerRawCsvAroundMarker(
            string frameTimeCsvPath,
            string markerTimingCsvPath,
            string summaryPath,
            string markerThreadFilter,
            string markerName,
            int frameRadius,
            int maxSearchFrameCount = 0)
        {
            string resolvedFrameTimeCsvPath = ResolveReportPath(
                frameTimeCsvPath,
                DefaultTornadoWindowFrameTimeCsvPath);
            string resolvedMarkerTimingCsvPath = ResolveReportPath(
                markerTimingCsvPath,
                DefaultTornadoWindowMarkerTimingCsvPath);
            string resolvedSummaryPath = ResolveReportPath(summaryPath, DefaultTornadoWindowSummaryPath);
            int firstRawFrameIndex = ProfilerDriver.firstFrameIndex;
            int lastRawFrameIndex = ProfilerDriver.lastFrameIndex;
            bool hasProfilerFrames = firstRawFrameIndex >= 0 && lastRawFrameIndex >= firstRawFrameIndex;
            string filter = string.IsNullOrWhiteSpace(markerThreadFilter)
                ? DefaultMarkerThreadFilter
                : markerThreadFilter.Trim();
            string focusName = string.IsNullOrWhiteSpace(markerName)
                ? DefaultTornadoMarkerName
                : markerName.Trim();
            int searchFirstRawFrameIndex = firstRawFrameIndex;
            int searchLastRawFrameIndex = lastRawFrameIndex;
            if (hasProfilerFrames && maxSearchFrameCount > 0)
                searchFirstRawFrameIndex = Math.Max(firstRawFrameIndex, lastRawFrameIndex - maxSearchFrameCount + 1);

            int markerRawFrameIndex = 0;
            int markerSearchScannedThreadCount = 0;
            int markerSearchScannedSampleCount = 0;
            bool foundMarker = hasProfilerFrames
                && TryFindLatestFrameContainingMarker(
                    searchFirstRawFrameIndex,
                    searchLastRawFrameIndex,
                    filter,
                    focusName,
                    out markerRawFrameIndex,
                    out markerSearchScannedThreadCount,
                    out markerSearchScannedSampleCount);
            int windowFirstRawFrameIndex = 0;
            int windowLastRawFrameIndex = 0;
            bool hasWindow = foundMarker
                && TryResolveMarkerCenteredRawFrameWindow(
                    firstRawFrameIndex,
                    lastRawFrameIndex,
                    markerRawFrameIndex,
                    frameRadius,
                    out windowFirstRawFrameIndex,
                    out windowLastRawFrameIndex);

            return ExportCurrentProfilerRawCsvInRange(
                resolvedFrameTimeCsvPath,
                resolvedMarkerTimingCsvPath,
                resolvedSummaryPath,
                hasWindow ? windowFirstRawFrameIndex : searchFirstRawFrameIndex,
                hasWindow ? windowLastRawFrameIndex : searchLastRawFrameIndex,
                hasWindow,
                hasProfilerFrames ? (hasWindow ? "marker-window-finished" : "marker-not-found") : "no-profiler-frames",
                filter,
                maxFrameCount: 0,
                markerFocusName: focusName,
                markerFocusDisplayFrameIndex: foundMarker ? markerRawFrameIndex + 1 : 0,
                markerWindowRadius: Math.Max(0, frameRadius),
                markerSearchMaxFrameCount: maxSearchFrameCount,
                markerSearchFirstRawFrameIndex: searchFirstRawFrameIndex,
                markerSearchLastRawFrameIndex: searchLastRawFrameIndex,
                markerSearchScannedThreadCount: markerSearchScannedThreadCount,
                markerSearchScannedSampleCount: markerSearchScannedSampleCount);
        }

        public static bool TryResolveMarkerCenteredRawFrameWindow(
            int firstRawFrameIndex,
            int lastRawFrameIndex,
            int markerRawFrameIndex,
            int frameRadius,
            out int windowFirstRawFrameIndex,
            out int windowLastRawFrameIndex)
        {
            windowFirstRawFrameIndex = 0;
            windowLastRawFrameIndex = 0;
            if (firstRawFrameIndex < 0
                || lastRawFrameIndex < firstRawFrameIndex
                || markerRawFrameIndex < firstRawFrameIndex
                || markerRawFrameIndex > lastRawFrameIndex)
            {
                return false;
            }

            int radius = Math.Max(0, frameRadius);
            windowFirstRawFrameIndex = Math.Max(firstRawFrameIndex, markerRawFrameIndex - radius);
            windowLastRawFrameIndex = Math.Min(lastRawFrameIndex, markerRawFrameIndex + radius);
            return true;
        }

        private static string ExportCurrentProfilerRawCsvInRange(
            string resolvedFrameTimeCsvPath,
            string resolvedMarkerTimingCsvPath,
            string resolvedSummaryPath,
            int firstRawFrameIndex,
            int lastRawFrameIndex,
            bool hasProfilerFrames,
            string status,
            string filter,
            int maxFrameCount,
            string markerFocusName,
            int markerFocusDisplayFrameIndex,
            int markerWindowRadius,
            int markerSearchMaxFrameCount,
            int markerSearchFirstRawFrameIndex,
            int markerSearchLastRawFrameIndex,
            int markerSearchScannedThreadCount,
            int markerSearchScannedSampleCount)
        {
            EnsureDirectory(resolvedFrameTimeCsvPath);
            EnsureDirectory(resolvedMarkerTimingCsvPath);
            EnsureDirectory(resolvedSummaryPath);

            var frameRows = new List<ProfilerFrameTimeCsvRow>();
            var markerAggregates = new Dictionary<string, ProfilerMarkerTimingAggregate>(
                StringComparer.Ordinal);
            int scannedThreadCount = 0;
            int exportedMarkerSampleCount = 0;
            int skippedInvalidFrameCount = 0;
            double firstFrameStartMs = 0d;
            bool hasFirstFrameStartMs = false;
            bool filterAllThreads = string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase);

            if (hasProfilerFrames)
            {
                for (int rawFrameIndex = firstRawFrameIndex; rawFrameIndex <= lastRawFrameIndex; rawFrameIndex++)
                {
                    bool frameValid = false;
                    long frameGcBytes = 0L;
                    double frameTimeMs = 0d;
                    double frameStartTimeMs = 0d;

                    int threadIndex = 0;
                    while (true)
                    {
                        using (RawFrameDataView frameData = ProfilerDriver.GetRawFrameDataView(rawFrameIndex, threadIndex))
                        {
                            if (!frameData.valid)
                                break;

                            scannedThreadCount++;
                            if (threadIndex == 0)
                            {
                                frameValid = frameData.frameTimeNs > 0;
                                frameTimeMs = frameData.frameTimeMs;
                                frameStartTimeMs = frameData.frameStartTimeMs;
                            }

                            bool threadMatches = MarkerThreadMatches(
                                filter,
                                frameData.threadName ?? string.Empty,
                                frameData.threadGroupName ?? string.Empty);
                            if (frameValid && (filterAllThreads || threadMatches))
                            {
                                frameGcBytes += CaptureThreadMarkerTiming(
                                    frameData,
                                    rawFrameIndex + 1,
                                    filter,
                                    markerAggregates,
                                    out int exportedSamples);
                                exportedMarkerSampleCount += exportedSamples;
                            }

                            if (!filterAllThreads && threadMatches)
                                break;
                        }

                        threadIndex++;
                    }

                    if (!frameValid)
                    {
                        skippedInvalidFrameCount++;
                        continue;
                    }

                    if (!hasFirstFrameStartMs)
                    {
                        hasFirstFrameStartMs = true;
                        firstFrameStartMs = frameStartTimeMs;
                    }

                    frameRows.Add(new ProfilerFrameTimeCsvRow(
                        frameRows.Count,
                        rawFrameIndex + 1,
                        frameTimeMs,
                        frameStartTimeMs - firstFrameStartMs,
                        frameGcBytes));
                }
            }

            WriteFrameTimeCsv(resolvedFrameTimeCsvPath, frameRows);
            List<ProfilerMarkerTimingCsvRow> markerRows = BuildMarkerTimingRows(markerAggregates);
            WriteMarkerTimingCsv(resolvedMarkerTimingCsvPath, markerRows);
            File.WriteAllText(
                resolvedSummaryPath,
                CreateSummaryText(
                    status,
                    resolvedFrameTimeCsvPath,
                    resolvedMarkerTimingCsvPath,
                    firstRawFrameIndex,
                    lastRawFrameIndex,
                    frameRows.Count,
                    skippedInvalidFrameCount,
                    scannedThreadCount,
                    exportedMarkerSampleCount,
                    markerRows.Count,
                    filter,
                    ProfilerDriver.enabled,
                    maxFrameCount,
                    markerFocusName,
                    markerFocusDisplayFrameIndex,
                    markerWindowRadius,
                    markerSearchMaxFrameCount,
                    markerSearchFirstRawFrameIndex,
                    markerSearchLastRawFrameIndex,
                    markerSearchScannedThreadCount,
                    markerSearchScannedSampleCount));
            return resolvedSummaryPath;
        }

        public static string ResolveReportPath(string reportPath)
        {
            return ResolveReportPath(reportPath, DefaultSummaryPath);
        }

        private static bool TryFindLatestFrameContainingMarker(
            int firstRawFrameIndex,
            int lastRawFrameIndex,
            string markerThreadFilter,
            string markerName,
            out int markerRawFrameIndex,
            out int scannedThreadCount,
            out int scannedSampleCount)
        {
            markerRawFrameIndex = 0;
            scannedThreadCount = 0;
            scannedSampleCount = 0;
            if (firstRawFrameIndex < 0
                || lastRawFrameIndex < firstRawFrameIndex
                || string.IsNullOrEmpty(markerName))
            {
                return false;
            }

            bool filterAllThreads = string.Equals(markerThreadFilter, "all", StringComparison.OrdinalIgnoreCase);
            for (int rawFrameIndex = lastRawFrameIndex; rawFrameIndex >= firstRawFrameIndex; rawFrameIndex--)
            {
                int threadIndex = 0;
                while (true)
                {
                    using (RawFrameDataView frameData = ProfilerDriver.GetRawFrameDataView(rawFrameIndex, threadIndex))
                    {
                        if (!frameData.valid)
                            break;

                        scannedThreadCount++;
                        if (MarkerThreadMatches(
                                markerThreadFilter,
                                frameData.threadName ?? string.Empty,
                                frameData.threadGroupName ?? string.Empty))
                        {
                            for (int sampleIndex = 1; sampleIndex < frameData.sampleCount; sampleIndex++)
                            {
                                scannedSampleCount++;
                                if (string.Equals(
                                        frameData.GetSampleName(sampleIndex),
                                        markerName,
                                        StringComparison.Ordinal))
                                {
                                    markerRawFrameIndex = rawFrameIndex;
                                    return true;
                                }
                            }
                        }

                        if (!filterAllThreads
                            && MarkerThreadMatches(
                                markerThreadFilter,
                                frameData.threadName ?? string.Empty,
                                frameData.threadGroupName ?? string.Empty))
                        {
                            break;
                        }
                    }

                    threadIndex++;
                }
            }

            return false;
        }

        private static string ResolveReportPath(string reportPath, string fallbackPath)
        {
            string safePath = string.IsNullOrWhiteSpace(reportPath)
                ? fallbackPath
                : reportPath;
            return Path.GetFullPath(safePath);
        }

        private static void EnsureDirectory(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        private static long CaptureThreadMarkerTiming(
            RawFrameDataView frameData,
            int displayFrameIndex,
            string markerThreadFilter,
            Dictionary<string, ProfilerMarkerTimingAggregate> markerAggregates,
            out int exportedSampleCount)
        {
            exportedSampleCount = 0;
            var samples = new List<ProfilerRawSampleRecord>(Math.Max(0, frameData.sampleCount - 1));
            long threadGcBytes = 0L;
            string threadName = frameData.threadName ?? string.Empty;
            string threadGroupName = frameData.threadGroupName ?? string.Empty;
            bool exportThread = MarkerThreadMatches(markerThreadFilter, threadName, threadGroupName);
            var depthStack = new Stack<int>();
            var gcSizeMetadataIndexByMarkerId = new Dictionary<int, int>();

            for (int sampleIndex = 1; sampleIndex < frameData.sampleCount; sampleIndex++)
            {
                float durationMs = frameData.GetSampleTimeMs(sampleIndex);
                int markerId = frameData.GetSampleMarkerId(sampleIndex);
                string markerName = frameData.GetSampleName(sampleIndex);
                int childCount = frameData.GetSampleChildrenCount(sampleIndex);
                int depth = 1 + depthStack.Count;

                if (durationMs >= 0f)
                {
                    long gcBytes = ResolveGcAllocBytes(
                        frameData,
                        sampleIndex,
                        markerId,
                        markerName,
                        gcSizeMetadataIndexByMarkerId);
                    threadGcBytes += gcBytes;
                    if (exportThread)
                    {
                        samples.Add(new ProfilerRawSampleRecord(
                            markerName,
                            durationMs,
                            depth,
                            gcBytes));
                    }
                }

                if (childCount > 0)
                {
                    depthStack.Push(childCount);
                }
                else
                {
                    while (depthStack.Count > 0)
                    {
                        int remainingChildren = depthStack.Pop();
                        if (remainingChildren > 1)
                        {
                            depthStack.Push(remainingChildren - 1);
                            break;
                        }
                    }
                }
            }

            if (exportThread && samples.Count > 0)
            {
                RecordMarkerTiming(displayFrameIndex, samples, markerAggregates);
                exportedSampleCount = samples.Count;
            }

            return threadGcBytes;
        }

        private static bool MarkerThreadMatches(
            string markerThreadFilter,
            string threadName,
            string threadGroupName)
        {
            if (string.Equals(markerThreadFilter, "all", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(markerThreadFilter, threadName, StringComparison.Ordinal))
                return true;

            if (string.IsNullOrEmpty(threadGroupName))
                return false;

            string groupedName = threadGroupName + "." + threadName;
            return string.Equals(markerThreadFilter, groupedName, StringComparison.Ordinal);
        }

        private static long ResolveGcAllocBytes(
            RawFrameDataView frameData,
            int sampleIndex,
            int markerId,
            string markerName,
            Dictionary<int, int> gcSizeMetadataIndexByMarkerId)
        {
            if (!string.Equals(markerName, "GC.Alloc", StringComparison.Ordinal))
                return 0L;

            if (!gcSizeMetadataIndexByMarkerId.TryGetValue(markerId, out int metadataIndex))
            {
                metadataIndex = -1;
                var metadata = frameData.GetMarkerMetadataInfo(markerId);
                for (int i = 0; i < metadata.Length; i++)
                {
                    if (metadata[i].name == "Size"
                        && metadata[i].unit == Unity.Profiling.ProfilerMarkerDataUnit.Bytes)
                    {
                        metadataIndex = i;
                        break;
                    }
                }

                gcSizeMetadataIndexByMarkerId[markerId] = metadataIndex;
            }

            return metadataIndex >= 0
                ? Math.Max(0L, frameData.GetSampleMetadataAsLong(sampleIndex, metadataIndex))
                : 0L;
        }

        private static void RecordMarkerTiming(
            int displayFrameIndex,
            List<ProfilerRawSampleRecord> samples,
            Dictionary<string, ProfilerMarkerTimingAggregate> markerAggregates)
        {
            long[] bytesWithChildren = new long[samples.Count];
            int[] allocationCounts = new int[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                bytesWithChildren[i] = samples[i].GcBytes;
                allocationCounts[i] = samples[i].GcBytes > 0 ? 1 : 0;
            }

            AddChildAllocations(samples, bytesWithChildren, allocationCounts);
            for (int i = 0; i < samples.Count; i++)
            {
                ProfilerRawSampleRecord sample = samples[i];
                if (!markerAggregates.TryGetValue(sample.MarkerName, out ProfilerMarkerTimingAggregate aggregate))
                {
                    aggregate = new ProfilerMarkerTimingAggregate(sample.MarkerName);
                    markerAggregates.Add(sample.MarkerName, aggregate);
                }

                aggregate.Record(
                    displayFrameIndex,
                    sample.TimeMs,
                    sample.Depth,
                    bytesWithChildren[i],
                    allocationCounts[i]);
            }
        }

        private static void AddChildAllocations(
            List<ProfilerRawSampleRecord> samples,
            long[] bytesWithChildren,
            int[] allocationCounts)
        {
            var stack = new List<int>(samples.Count);
            for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                int depth = samples[sampleIndex].Depth;
                if (depth >= stack.Count)
                {
                    if (depth == stack.Count)
                        PopAndRecordChildAllocation(stack, bytesWithChildren, allocationCounts);
                }
                else
                {
                    while (stack.Count >= depth)
                        PopAndRecordChildAllocation(stack, bytesWithChildren, allocationCounts);
                }

                stack.Add(sampleIndex);
            }

            while (stack.Count > 0)
                PopAndRecordChildAllocation(stack, bytesWithChildren, allocationCounts);
        }

        private static void PopAndRecordChildAllocation(
            List<int> stack,
            long[] bytesWithChildren,
            int[] allocationCounts)
        {
            int childIndex = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            if (stack.Count == 0)
                return;

            int parentIndex = stack[stack.Count - 1];
            bytesWithChildren[parentIndex] += bytesWithChildren[childIndex];
            allocationCounts[parentIndex] += allocationCounts[childIndex];
        }

        private static List<ProfilerMarkerTimingCsvRow> BuildMarkerTimingRows(
            Dictionary<string, ProfilerMarkerTimingAggregate> markerAggregates)
        {
            var rows = new List<ProfilerMarkerTimingCsvRow>(markerAggregates.Count);
            foreach (ProfilerMarkerTimingAggregate aggregate in markerAggregates.Values)
                rows.Add(aggregate.CreateRow());

            rows.Sort(CompareMarkerTimingRows);
            return rows;
        }

        private static int CompareMarkerTimingRows(
            ProfilerMarkerTimingCsvRow left,
            ProfilerMarkerTimingCsvRow right)
        {
            int result = right.MedianTimeMs.CompareTo(left.MedianTimeMs);
            if (result != 0)
                return result;
            result = right.MaxTimeMs.CompareTo(left.MaxTimeMs);
            if (result != 0)
                return result;
            result = right.TotalTimeMs.CompareTo(left.TotalTimeMs);
            if (result != 0)
                return result;
            result = right.CountTotal.CompareTo(left.CountTotal);
            if (result != 0)
                return result;
            return string.Compare(right.Name, left.Name, StringComparison.Ordinal);
        }

    }
}
