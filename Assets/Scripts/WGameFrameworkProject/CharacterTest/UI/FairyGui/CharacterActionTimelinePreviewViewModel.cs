using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterActionTimelinePreviewEntryViewModel
    {
        internal int StartFrame;
        internal int EndFrame;
        internal string Label = string.Empty;
        internal string Tooltip = string.Empty;
    }

    internal sealed class CharacterActionTimelinePreviewRowViewModel
    {
        internal CharacterActionWorkstationRowKind Kind;
        internal string Label = string.Empty;
        internal CharacterActionTimelinePreviewEntryViewModel[] Entries =
            Array.Empty<CharacterActionTimelinePreviewEntryViewModel>();
    }

    internal sealed class CharacterActionTimelinePreviewViewModel
    {
        internal bool Available;
        internal bool HasErrors;
        internal CharacterActionId ActionId;
        internal string DisplayName = string.Empty;
        internal string Category = string.Empty;
        internal string TimelineAuthority = string.Empty;
        internal int DurationFrames;
        internal string Diagnostic = string.Empty;
        internal CharacterActionTimelinePreviewRowViewModel[] Rows =
            Array.Empty<CharacterActionTimelinePreviewRowViewModel>();
    }

    internal static class CharacterActionTimelinePreviewProjection
    {
        internal static CharacterActionTimelinePreviewViewModel Build(CharacterActionConfig action)
        {
            var result = new CharacterActionTimelinePreviewViewModel
            {
                ActionId = action?.ActionId ?? CharacterActionId.None,
                DisplayName = action?.DisplayName ?? string.Empty,
                Category = action?.Category.ToString() ?? string.Empty,
                TimelineAuthority = action?.TimelineAuthority.ToString() ?? string.Empty,
                DurationFrames = action?.DurationFrames.GetValueOrDefault() ?? 0,
            };
            if (action == null)
            {
                result.Diagnostic = "动作引用不存在。";
                return result;
            }

            try
            {
                CharacterActionWorkstationSnapshot snapshot =
                    CharacterActionWorkstation.BuildSnapshot(action);
                result.DurationFrames = snapshot.DurationFrames;
                result.HasErrors = snapshot.HasErrors || !snapshot.DurationResolved;
                result.Diagnostic = snapshot.FormattedDiagnostics.Length > 0
                    ? string.Join("\n", snapshot.FormattedDiagnostics)
                    : snapshot.DurationResolved
                        ? string.Empty
                        : "动作缺少权威时长。";
                if (result.HasErrors || snapshot.DurationFrames <= 0)
                    return result;

                CharacterActionWorkstationTimelineRow[] sourceRows = snapshot.TimelineRows;
                var rows = new CharacterActionTimelinePreviewRowViewModel[sourceRows.Length];
                for (int rowIndex = 0; rowIndex < sourceRows.Length; rowIndex++)
                {
                    CharacterActionWorkstationTimelineRow sourceRow = sourceRows[rowIndex];
                    CharacterActionWorkstationTimelineEntry[] sourceEntries =
                        sourceRow?.Entries ?? Array.Empty<CharacterActionWorkstationTimelineEntry>();
                    var entries = new CharacterActionTimelinePreviewEntryViewModel[sourceEntries.Length];
                    for (int entryIndex = 0; entryIndex < sourceEntries.Length; entryIndex++)
                    {
                        CharacterActionWorkstationTimelineEntry sourceEntry = sourceEntries[entryIndex];
                        entries[entryIndex] = new CharacterActionTimelinePreviewEntryViewModel
                        {
                            StartFrame = sourceEntry.StartFrame,
                            EndFrame = sourceEntry.EndFrame,
                            Label = ResolveEntryLabel(sourceEntry),
                            Tooltip = BuildTooltip(sourceEntry),
                        };
                    }

                    rows[rowIndex] = new CharacterActionTimelinePreviewRowViewModel
                    {
                        Kind = sourceRow.Kind,
                        Label = sourceRow.Label,
                        Entries = entries,
                    };
                }

                result.Rows = rows;
                result.Available = true;
                return result;
            }
            catch (Exception ex)
            {
                result.HasErrors = true;
                result.Diagnostic = "动作时间轴预览不可用：" + ex.Message;
                return result;
            }
        }

        private static string ResolveEntryLabel(CharacterActionWorkstationTimelineEntry entry)
        {
            if (entry.PhaseKind != CharacterActionPhaseKind.None)
                return entry.PhaseKind.ToString();
            if (entry.EventKind != CharacterActionTrackEventKind.None)
                return entry.EventKind.ToString();
            if (entry.SourceKind != CharacterActionSourceKind.None)
                return entry.SourceKind.ToString();
            if (!string.IsNullOrWhiteSpace(entry.StableId))
                return entry.StableId;
            return entry.RowKind.ToString();
        }

        private static string BuildTooltip(CharacterActionWorkstationTimelineEntry entry)
        {
            string range = entry.StartFrame == entry.EndFrame
                ? entry.StartFrame + "f"
                : entry.StartFrame + "f–" + entry.EndFrame + "f";
            string label = ResolveEntryLabel(entry);
            string payload = string.IsNullOrWhiteSpace(entry.Payload)
                ? string.Empty
                : "\n" + entry.Payload;
            string stableId = string.IsNullOrWhiteSpace(entry.StableId)
                ? string.Empty
                : "\nID: " + entry.StableId;
            return label + " · " + range + stableId + payload;
        }
    }
}
