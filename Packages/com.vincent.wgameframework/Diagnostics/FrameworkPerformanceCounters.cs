using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Diagnostics
{
    public enum FrameworkPerformanceCounterCost
    {
        Unknown = 0,
        NoAlloc = 1,
        AllocByDesign = 2
    }

    public readonly struct FrameworkPerformanceCounterSample
    {
        public FrameworkPerformanceCounterSample(
            string counterId,
            string displayName,
            string category,
            long value,
            string unit = "count",
            FrameworkPerformanceCounterCost cost = FrameworkPerformanceCounterCost.Unknown,
            bool hasBudget = false,
            long budget = 0L)
        {
            if (string.IsNullOrWhiteSpace(counterId))
                throw new ArgumentException("Performance counter id cannot be empty.", nameof(counterId));

            CounterId = counterId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? counterId : displayName;
            Category = string.IsNullOrWhiteSpace(category) ? "Runtime" : category;
            Unit = string.IsNullOrWhiteSpace(unit) ? "count" : unit;
            Value = value;
            Cost = cost;
            HasBudget = hasBudget;
            Budget = budget;
        }

        public string CounterId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Unit { get; }
        public long Value { get; }
        public FrameworkPerformanceCounterCost Cost { get; }
        public bool HasBudget { get; }
        public long Budget { get; }
    }

    public sealed class FrameworkPerformanceCounterSnapshot
    {
        private readonly List<FrameworkPerformanceCounterSample> _samples;

        public FrameworkPerformanceCounterSnapshot(
            string sourceName,
            bool enabled,
            IReadOnlyList<FrameworkPerformanceCounterSample> samples)
        {
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? "Performance" : sourceName;
            Enabled = enabled;
            _samples = samples != null
                ? new List<FrameworkPerformanceCounterSample>(samples)
                : new List<FrameworkPerformanceCounterSample>();
            _samples.Sort(CompareSamples);
        }

        public string SourceName { get; }
        public bool Enabled { get; }
        public IReadOnlyList<FrameworkPerformanceCounterSample> Samples => _samples;
        public int Count => _samples.Count;

        private static int CompareSamples(
            FrameworkPerformanceCounterSample left,
            FrameworkPerformanceCounterSample right)
        {
            int category = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
            if (category != 0)
                return category;

            return string.Compare(left.CounterId, right.CounterId, StringComparison.Ordinal);
        }
    }

    public sealed class FrameworkPerformanceCounterRecorder
    {
        private readonly Dictionary<string, FrameworkPerformanceCounterSample> _samples =
            new Dictionary<string, FrameworkPerformanceCounterSample>(StringComparer.Ordinal);

        public bool Enabled { get; private set; }
        public int Count => _samples.Count;

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
        }

        public bool SetCounter(FrameworkPerformanceCounterSample sample)
        {
            if (!Enabled)
                return false;

            _samples[sample.CounterId] = sample;
            return true;
        }

        public bool AddCounter(
            string counterId,
            string displayName,
            string category,
            long delta,
            string unit = "count",
            FrameworkPerformanceCounterCost cost = FrameworkPerformanceCounterCost.NoAlloc)
        {
            if (!Enabled)
                return false;

            if (_samples.TryGetValue(counterId, out FrameworkPerformanceCounterSample current))
            {
                return SetCounter(new FrameworkPerformanceCounterSample(
                    current.CounterId,
                    current.DisplayName,
                    current.Category,
                    current.Value + delta,
                    current.Unit,
                    current.Cost,
                    current.HasBudget,
                    current.Budget));
            }

            return SetCounter(new FrameworkPerformanceCounterSample(
                counterId,
                displayName,
                category,
                delta,
                unit,
                cost));
        }

        public FrameworkPerformanceCounterSnapshot CreateSnapshot(string sourceName = "Performance")
        {
            return new FrameworkPerformanceCounterSnapshot(sourceName, Enabled, new List<FrameworkPerformanceCounterSample>(_samples.Values));
        }

        public void Clear()
        {
            _samples.Clear();
        }
    }

    public sealed class FrameworkPerformanceCounterDebugSource : IFrameworkDebugSource
    {
        private readonly Func<FrameworkPerformanceCounterSnapshot> _snapshotFactory;

        public FrameworkPerformanceCounterDebugSource(
            Func<FrameworkPerformanceCounterSnapshot> snapshotFactory,
            string name = "Performance")
        {
            _snapshotFactory = snapshotFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "Performance" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _snapshotFactory != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            FrameworkPerformanceCounterSnapshot snapshot = _snapshotFactory != null ? _snapshotFactory() : null;
            if (snapshot == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "snapshot unavailable") });
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(snapshot)),
                    new FrameworkDebugSection("Performance Counters", CreateCounters(snapshot))
                });
        }

        private static string CreateSummary(FrameworkPerformanceCounterSnapshot snapshot)
        {
            return "source: " + snapshot.SourceName
                + "\nenabled: " + (snapshot.Enabled ? "true" : "false")
                + "\ncounters: " + snapshot.Count;
        }

        private static string CreateCounters(FrameworkPerformanceCounterSnapshot snapshot)
        {
            if (snapshot.Samples.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Samples.Count; i++)
            {
                FrameworkPerformanceCounterSample sample = snapshot.Samples[i];
                builder.Append(sample.Category)
                    .Append('/')
                    .Append(sample.CounterId)
                    .Append(" value=")
                    .Append(sample.Value)
                    .Append(' ')
                    .Append(sample.Unit)
                    .Append(" cost=")
                    .Append(sample.Cost);
                if (sample.HasBudget)
                    builder.Append(" budget=").Append(sample.Budget);
                if (i + 1 < snapshot.Samples.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
