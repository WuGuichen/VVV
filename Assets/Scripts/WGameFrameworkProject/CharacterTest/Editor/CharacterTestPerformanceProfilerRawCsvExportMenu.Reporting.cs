using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MxFramework.Game.CharacterTest.Editor
{
    public static partial class CharacterTestPerformanceProfilerRawCsvExportMenu
    {
        private static void WriteFrameTimeCsv(
            string path,
            List<ProfilerFrameTimeCsvRow> rows)
        {
            var builder = new StringBuilder(Math.Max(512, rows.Count * 64));
            AppendCsvLine(
                builder,
                "Frame Offset",
                " Frame Index",
                " Frame Time (ms)",
                " Time from first frame (ms)",
                " GC Alloc (bytes)");
            for (int i = 0; i < rows.Count; i++)
            {
                ProfilerFrameTimeCsvRow row = rows[i];
                AppendCsvLine(
                    builder,
                    row.FrameOffset.ToString(CultureInfo.InvariantCulture),
                    row.FrameIndex.ToString(CultureInfo.InvariantCulture),
                    FormatDouble(row.FrameTimeMs),
                    FormatDouble(row.TimeFromFirstFrameMs),
                    row.GcAllocBytes.ToString(CultureInfo.InvariantCulture));
            }

            File.WriteAllText(path, builder.ToString());
        }

        private static void WriteMarkerTimingCsv(
            string path,
            List<ProfilerMarkerTimingCsvRow> rows)
        {
            var builder = new StringBuilder(Math.Max(2048, rows.Count * 256));
            AppendCsvLine(
                builder,
                "Name",
                "Median Time",
                "Min Time",
                "Max Time",
                "Median Frame Index",
                "Min Frame Index",
                "Max Frame Index",
                "Min Depth",
                "Max Depth",
                "Total Time",
                "Mean Time",
                "Time Lower Quartile",
                "Time Upper Quartile",
                "Count Total",
                "Count Median",
                "Count Min",
                "Count Max",
                "Number of frames containing Marker",
                "First Frame Index",
                "Time Min Individual",
                "Time Max Individual",
                "Min Individual Frame",
                "Max Individual Frame",
                "Time at Median Frame",
                "GC Alloc Total (bytes)",
                "GC Alloc Count",
                "GC Alloc Mean (bytes)",
                "GC Alloc Median (bytes)",
                "GC Alloc Min (bytes)",
                "GC Alloc Max (bytes)",
                "GC Alloc Lower Quartile (bytes)",
                "GC Alloc Upper Quartile (bytes)",
                "GC Alloc Median Frame Index",
                "GC Alloc Min Frame Index",
                "GC Alloc Max Frame Index");
            for (int i = 0; i < rows.Count; i++)
            {
                ProfilerMarkerTimingCsvRow row = rows[i];
                AppendCsvLine(
                    builder,
                    row.Name,
                    FormatDouble(row.MedianTimeMs),
                    FormatDouble(row.MinTimeMs),
                    FormatDouble(row.MaxTimeMs),
                    row.MedianFrameIndex.ToString(CultureInfo.InvariantCulture),
                    row.MinFrameIndex.ToString(CultureInfo.InvariantCulture),
                    row.MaxFrameIndex.ToString(CultureInfo.InvariantCulture),
                    row.MinDepth.ToString(CultureInfo.InvariantCulture),
                    row.MaxDepth.ToString(CultureInfo.InvariantCulture),
                    FormatDouble(row.TotalTimeMs),
                    FormatDouble(row.MeanTimeMs),
                    FormatDouble(row.LowerQuartileTimeMs),
                    FormatDouble(row.UpperQuartileTimeMs),
                    row.CountTotal.ToString(CultureInfo.InvariantCulture),
                    row.CountMedian.ToString(CultureInfo.InvariantCulture),
                    row.CountMin.ToString(CultureInfo.InvariantCulture),
                    row.CountMax.ToString(CultureInfo.InvariantCulture),
                    row.FrameCount.ToString(CultureInfo.InvariantCulture),
                    row.FirstFrameIndex.ToString(CultureInfo.InvariantCulture),
                    FormatDouble(row.MinIndividualTimeMs),
                    FormatDouble(row.MaxIndividualTimeMs),
                    row.MinIndividualFrameIndex.ToString(CultureInfo.InvariantCulture),
                    row.MaxIndividualFrameIndex.ToString(CultureInfo.InvariantCulture),
                    FormatDouble(row.TimeAtMedianFrameMs),
                    row.GcTotalBytes.ToString(CultureInfo.InvariantCulture),
                    row.GcCount.ToString(CultureInfo.InvariantCulture),
                    row.GcMeanBytes.ToString(CultureInfo.InvariantCulture),
                    row.GcMedianBytes.ToString(CultureInfo.InvariantCulture),
                    row.GcMinBytes.ToString(CultureInfo.InvariantCulture),
                    row.GcMaxBytes.ToString(CultureInfo.InvariantCulture),
                    row.GcLowerQuartileBytes.ToString(CultureInfo.InvariantCulture),
                    row.GcUpperQuartileBytes.ToString(CultureInfo.InvariantCulture),
                    row.GcMedianFrameIndex.ToString(CultureInfo.InvariantCulture),
                    row.GcMinFrameIndex.ToString(CultureInfo.InvariantCulture),
                    row.GcMaxFrameIndex.ToString(CultureInfo.InvariantCulture));
            }

            File.WriteAllText(path, builder.ToString());
        }

        private static string CreateSummaryText(
            string status,
            string frameTimeCsvPath,
            string markerTimingCsvPath,
            int firstRawFrameIndex,
            int lastRawFrameIndex,
            int exportedFrameCount,
            int skippedInvalidFrameCount,
            int scannedThreadCount,
            int exportedMarkerSampleCount,
            int exportedMarkerCount,
            string markerThreadFilter,
            bool profilerRecording,
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
            var builder = new StringBuilder(1024);
            builder.AppendLine("scenario: character-test.editor-profiler-raw-csv-export");
            builder.Append("status: ").AppendLine(status);
            builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("profileKind: editor-profiler-raw-frame-data");
            builder.AppendLine();
            builder.Append("frameTimeCsv: ").AppendLine(frameTimeCsvPath);
            builder.Append("markerTimingCsv: ").AppendLine(markerTimingCsvPath);
            builder.Append("markerThreadFilter: ").AppendLine(markerThreadFilter);
            builder.Append("maxFrameCount: ").AppendLine(Math.Max(0, maxFrameCount).ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(markerFocusName) || markerSearchMaxFrameCount > 0)
            {
                builder.Append("markerFocusName: ").AppendLine(markerFocusName ?? string.Empty);
                builder.Append("markerFocusFound: ").AppendLine(markerFocusDisplayFrameIndex > 0 ? "true" : "false");
                builder.Append("markerFocusFrameIndex: ")
                    .AppendLine(markerFocusDisplayFrameIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append("markerWindowRadius: ")
                    .AppendLine(Math.Max(0, markerWindowRadius).ToString(CultureInfo.InvariantCulture));
                builder.Append("markerSearchMaxFrameCount: ")
                    .AppendLine(Math.Max(0, markerSearchMaxFrameCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("markerSearchFirstRawFrameIndex: ")
                    .AppendLine(markerSearchFirstRawFrameIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append("markerSearchLastRawFrameIndex: ")
                    .AppendLine(markerSearchLastRawFrameIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append("markerSearchScannedThreads: ")
                    .AppendLine(markerSearchScannedThreadCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("markerSearchScannedSamples: ")
                    .AppendLine(markerSearchScannedSampleCount.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append("profilerRecording: ").AppendLine(profilerRecording ? "true" : "false");
            builder.Append("firstRawFrameIndex: ").AppendLine(firstRawFrameIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append("lastRawFrameIndex: ").AppendLine(lastRawFrameIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append("exportedFrames: ").AppendLine(exportedFrameCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("skippedInvalidFrames: ").AppendLine(skippedInvalidFrameCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("scannedThreads: ").AppendLine(scannedThreadCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("exportedMarkerSamples: ").AppendLine(exportedMarkerSampleCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("exportedMarkers: ").AppendLine(exportedMarkerCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("notes:");
            builder.AppendLine("- frame indices are Unity Profiler display frames, rawFrameIndex + 1.");
            builder.AppendLine("- marker timing is aggregated from RawFrameDataView for the selected thread filter.");
            builder.AppendLine("- this is current Editor profiler buffer evidence, not a saved Profile Analyzer .pdata artifact.");
            return builder.ToString();
        }

        private static void AppendCsvLine(StringBuilder builder, params string[] columns)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (i > 0)
                    builder.Append(';');
                AppendCsvField(builder, columns[i]);
            }

            builder.AppendLine();
        }

        private static void AppendCsvField(StringBuilder builder, string value)
        {
            string safeValue = value ?? string.Empty;
            bool quote = safeValue.IndexOfAny(new[] { ';', '"', '\r', '\n' }) >= 0;
            if (!quote)
            {
                builder.Append(safeValue);
                return;
            }

            builder.Append('"');
            for (int i = 0; i < safeValue.Length; i++)
            {
                char item = safeValue[i];
                if (item == '"')
                    builder.Append('"');
                builder.Append(item);
            }

            builder.Append('"');
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static double Percentile(List<double> values, double fraction)
        {
            if (values.Count == 0)
                return 0d;

            var ordered = new List<double>(values);
            ordered.Sort();
            if (ordered.Count == 1)
                return ordered[0];

            double clamped = Math.Max(0d, Math.Min(1d, fraction));
            double position = (ordered.Count - 1) * clamped;
            int lowerIndex = (int)position;
            int upperIndex = Math.Min(lowerIndex + 1, ordered.Count - 1);
            double weight = position - lowerIndex;
            return ordered[lowerIndex] + (ordered[upperIndex] - ordered[lowerIndex]) * weight;
        }

        private static long Percentile(List<long> values, double fraction)
        {
            if (values.Count == 0)
                return 0L;

            var ordered = new List<long>(values);
            ordered.Sort();
            if (ordered.Count == 1)
                return ordered[0];

            double clamped = Math.Max(0d, Math.Min(1d, fraction));
            double position = (ordered.Count - 1) * clamped;
            int lowerIndex = (int)position;
            int upperIndex = Math.Min(lowerIndex + 1, ordered.Count - 1);
            double weight = position - lowerIndex;
            double value = ordered[lowerIndex] + (ordered[upperIndex] - ordered[lowerIndex]) * weight;
            return (long)Math.Round(value);
        }

    }
}
