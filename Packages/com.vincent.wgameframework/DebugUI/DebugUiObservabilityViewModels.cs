using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.DebugUI
{
    public readonly struct DebugUiTimelineEntryViewModel
    {
        public DebugUiTimelineEntryViewModel(
            long frame,
            string source,
            string category,
            string entityId,
            string traceId,
            string summary)
        {
            if (frame < 0L)
                throw new ArgumentOutOfRangeException(nameof(frame), "Timeline event frame cannot be negative.");

            Frame = frame;
            Source = source ?? string.Empty;
            Category = category ?? string.Empty;
            EntityId = entityId ?? string.Empty;
            TraceId = traceId ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public long Frame { get; }
        public string Source { get; }
        public string Category { get; }
        public string EntityId { get; }
        public string TraceId { get; }
        public string Summary { get; }
    }

    public readonly struct DebugUiTimelineFilter
    {
        public DebugUiTimelineFilter(string source, string entityId, string category)
        {
            Source = source ?? string.Empty;
            EntityId = entityId ?? string.Empty;
            Category = category ?? string.Empty;
        }

        public string Source { get; }
        public string EntityId { get; }
        public string Category { get; }
        public static DebugUiTimelineFilter Empty => new DebugUiTimelineFilter(null, null, null);

        public bool Matches(DebugUiTimelineEntryViewModel entry)
        {
            return MatchesValue(Source, entry.Source)
                && MatchesValue(EntityId, entry.EntityId)
                && MatchesValue(Category, entry.Category);
        }

        private static bool MatchesValue(string filter, string value)
        {
            return string.IsNullOrEmpty(filter)
                || string.Equals(filter, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class DebugUiTimelineViewModel
    {
        private readonly List<DebugUiTimelineEntryViewModel> _entries;

        public DebugUiTimelineViewModel(IReadOnlyList<DebugUiTimelineEntryViewModel> entries)
        {
            _entries = entries != null
                ? new List<DebugUiTimelineEntryViewModel>(entries)
                : new List<DebugUiTimelineEntryViewModel>();
            _entries.Sort(CompareEntries);
        }

        public IReadOnlyList<DebugUiTimelineEntryViewModel> Entries => _entries;
        public int Count => _entries.Count;

        public static DebugUiTimelineViewModel From(
            IReadOnlyList<DebugUiTimelineEntryViewModel> entries,
            DebugUiTimelineFilter filter,
            int maxEntries)
        {
            var filtered = new List<DebugUiTimelineEntryViewModel>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    DebugUiTimelineEntryViewModel entry = entries[i];
                    if (filter.Matches(entry))
                        filtered.Add(entry);
                }
            }

            filtered.Sort(CompareEntries);
            if (maxEntries > 0 && filtered.Count > maxEntries)
                filtered.RemoveRange(0, filtered.Count - maxEntries);

            return new DebugUiTimelineViewModel(filtered);
        }

        private static int CompareEntries(DebugUiTimelineEntryViewModel left, DebugUiTimelineEntryViewModel right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;

            int source = string.Compare(left.Source, right.Source, StringComparison.Ordinal);
            if (source != 0)
                return source;

            int category = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
            if (category != 0)
                return category;

            int entity = string.Compare(left.EntityId, right.EntityId, StringComparison.Ordinal);
            if (entity != 0)
                return entity;

            return string.Compare(left.TraceId, right.TraceId, StringComparison.Ordinal);
        }
    }

    public readonly struct DebugUiEntityWatchEntryViewModel
    {
        public DebugUiEntityWatchEntryViewModel(
            string entityId,
            string displayName,
            string activeStatus,
            string keyAttributes,
            string pressureBand,
            string guardState,
            string armorState,
            string summary)
        {
            EntityId = entityId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ActiveStatus = activeStatus ?? string.Empty;
            KeyAttributes = keyAttributes ?? string.Empty;
            PressureBand = pressureBand ?? string.Empty;
            GuardState = guardState ?? string.Empty;
            ArmorState = armorState ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public string EntityId { get; }
        public string DisplayName { get; }
        public string ActiveStatus { get; }
        public string KeyAttributes { get; }
        public string PressureBand { get; }
        public string GuardState { get; }
        public string ArmorState { get; }
        public string Summary { get; }
    }

    public sealed class DebugUiEntityWatchViewModel
    {
        private readonly List<DebugUiEntityWatchEntryViewModel> _entities;

        public DebugUiEntityWatchViewModel(IReadOnlyList<DebugUiEntityWatchEntryViewModel> entities)
        {
            _entities = entities != null
                ? new List<DebugUiEntityWatchEntryViewModel>(entities)
                : new List<DebugUiEntityWatchEntryViewModel>();
            _entities.Sort(CompareEntities);
        }

        public IReadOnlyList<DebugUiEntityWatchEntryViewModel> Entities => _entities;
        public int Count => _entities.Count;

        public static DebugUiEntityWatchViewModel From(
            IReadOnlyList<DebugUiEntityWatchEntryViewModel> entities,
            string entityFilter)
        {
            var filtered = new List<DebugUiEntityWatchEntryViewModel>();
            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    DebugUiEntityWatchEntryViewModel entity = entities[i];
                    if (string.IsNullOrEmpty(entityFilter)
                        || string.Equals(entity.EntityId, entityFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        filtered.Add(entity);
                    }
                }
            }

            return new DebugUiEntityWatchViewModel(filtered);
        }

        private static int CompareEntities(
            DebugUiEntityWatchEntryViewModel left,
            DebugUiEntityWatchEntryViewModel right)
        {
            return string.Compare(left.EntityId, right.EntityId, StringComparison.Ordinal);
        }
    }

    public static class DebugUiObservabilityFormatter
    {
        public static string FormatTimeline(DebugUiTimelineViewModel timeline)
        {
            if (timeline == null || timeline.Entries.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < timeline.Entries.Count; i++)
            {
                DebugUiTimelineEntryViewModel entry = timeline.Entries[i];
                builder.Append("frame=")
                    .Append(entry.Frame)
                    .Append(" source=")
                    .Append(entry.Source)
                    .Append(" category=")
                    .Append(entry.Category)
                    .Append(" entity=")
                    .Append(string.IsNullOrEmpty(entry.EntityId) ? "-" : entry.EntityId)
                    .Append(" trace=")
                    .Append(string.IsNullOrEmpty(entry.TraceId) ? "-" : entry.TraceId)
                    .Append(" summary=")
                    .Append(entry.Summary);
                if (i + 1 < timeline.Entries.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        public static string FormatEntityWatch(DebugUiEntityWatchViewModel watch)
        {
            if (watch == null || watch.Entities.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < watch.Entities.Count; i++)
            {
                DebugUiEntityWatchEntryViewModel entity = watch.Entities[i];
                builder.Append("entity=")
                    .Append(entity.EntityId)
                    .Append(" name=")
                    .Append(string.IsNullOrEmpty(entity.DisplayName) ? "-" : entity.DisplayName)
                    .Append(" active=")
                    .Append(string.IsNullOrEmpty(entity.ActiveStatus) ? "-" : entity.ActiveStatus)
                    .Append(" attrs=")
                    .Append(string.IsNullOrEmpty(entity.KeyAttributes) ? "-" : entity.KeyAttributes)
                    .Append(" pressure=")
                    .Append(string.IsNullOrEmpty(entity.PressureBand) ? "-" : entity.PressureBand)
                    .Append(" guard=")
                    .Append(string.IsNullOrEmpty(entity.GuardState) ? "-" : entity.GuardState)
                    .Append(" armor=")
                    .Append(string.IsNullOrEmpty(entity.ArmorState) ? "-" : entity.ArmorState);
                if (!string.IsNullOrEmpty(entity.Summary))
                    builder.Append(" summary=").Append(entity.Summary);
                if (i + 1 < watch.Entities.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
