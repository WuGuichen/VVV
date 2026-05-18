using System;
using System.Collections.Generic;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI
{
    public sealed class DebugUiSnapshotAggregator
    {
        private long _refreshSequence;

        public DebugUiDashboardViewModel Refresh(FrameworkDebugSourceRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            var orderedSources = new List<IFrameworkDebugSource>(registry.Sources);
            orderedSources.Sort(CompareSources);

            var sources = new List<DebugUiSourceViewModel>(orderedSources.Count);
            var errors = new List<DebugUiErrorViewModel>();
            long sequence = ++_refreshSequence;

            for (int i = 0; i < orderedSources.Count; i++)
            {
                IFrameworkDebugSource source = orderedSources[i];
                if (source == null)
                    continue;

                string sourceName = source.Name ?? string.Empty;
                FrameworkDebugMode sourceMode = source.Mode;

                if (!source.IsAvailable)
                {
                    sources.Add(new DebugUiSourceViewModel(
                        sourceName,
                        sourceMode,
                        DebugUiSourceStatus.Unavailable,
                        new[] { new DebugUiSectionViewModel("Status", "unavailable") },
                        "unavailable"));
                    continue;
                }

                try
                {
                    FrameworkDebugSnapshot snapshot = source.CreateSnapshot();
                    sources.Add(CreateSourceViewModel(source, snapshot));
                }
                catch (Exception exception)
                {
                    errors.Add(new DebugUiErrorViewModel(sourceName, exception.GetType().Name, exception.Message));
                    sources.Add(new DebugUiSourceViewModel(
                        sourceName,
                        sourceMode,
                        DebugUiSourceStatus.Error,
                        new[] { new DebugUiSectionViewModel("Error", exception.Message) },
                        exception.Message));
                }
            }

            return new DebugUiDashboardViewModel(sequence, sources, errors);
        }

        private static DebugUiSourceViewModel CreateSourceViewModel(
            IFrameworkDebugSource source,
            FrameworkDebugSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new DebugUiSourceViewModel(
                    source.Name,
                    source.Mode,
                    DebugUiSourceStatus.Unavailable,
                    new[] { new DebugUiSectionViewModel("Status", "snapshot unavailable") },
                    "snapshot unavailable");
            }

            IReadOnlyList<FrameworkDebugSection> sourceSections = snapshot.Sections;
            var sections = new List<DebugUiSectionViewModel>(sourceSections.Count);
            for (int i = 0; i < sourceSections.Count; i++)
            {
                FrameworkDebugSection section = sourceSections[i];
                sections.Add(new DebugUiSectionViewModel(section.Title, section.Body));
            }

            return new DebugUiSourceViewModel(
                snapshot.SourceName,
                snapshot.Mode,
                DebugUiSourceStatus.Available,
                sections);
        }

        private static int CompareSources(IFrameworkDebugSource left, IFrameworkDebugSource right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int mode = left.Mode.CompareTo(right.Mode);
            if (mode != 0)
                return mode;

            return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }
    }
}
