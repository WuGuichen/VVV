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

            var orderedSources = new List<DebugUiSourceReadState>(registry.Sources.Count);
            for (int i = 0; i < registry.Sources.Count; i++)
            {
                IFrameworkDebugSource source = registry.Sources[i];
                if (source != null)
                    orderedSources.Add(ReadSourceState(source));
            }

            orderedSources.Sort(CompareSources);

            var sources = new List<DebugUiSourceViewModel>(orderedSources.Count);
            var errors = new List<DebugUiErrorViewModel>();
            long sequence = ++_refreshSequence;

            for (int i = 0; i < orderedSources.Count; i++)
            {
                DebugUiSourceReadState source = orderedSources[i];

                if (source.MetadataException != null)
                {
                    AddErrorSource(sources, errors, source, source.MetadataException);
                    continue;
                }

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
                    FrameworkDebugSnapshot snapshot = source.Source.CreateSnapshot();
                    sources.Add(CreateSourceViewModel(source, snapshot));
                }
                catch (Exception exception)
                {
                    AddErrorSource(sources, errors, source, exception);
                }
            }

            return new DebugUiDashboardViewModel(sequence, sources, errors);
        }

        private static DebugUiSourceViewModel CreateSourceViewModel(
            DebugUiSourceReadState source,
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

        private static void AddErrorSource(
            List<DebugUiSourceViewModel> sources,
            List<DebugUiErrorViewModel> errors,
            DebugUiSourceReadState source,
            Exception exception)
        {
            string message = exception != null ? exception.Message : "unknown error";
            string exceptionType = exception != null ? exception.GetType().Name : nameof(Exception);
            errors.Add(new DebugUiErrorViewModel(source.Name, exceptionType, message));
            sources.Add(new DebugUiSourceViewModel(
                source.Name,
                source.Mode,
                DebugUiSourceStatus.Error,
                new[] { new DebugUiSectionViewModel("Error", message) },
                message));
        }

        private static DebugUiSourceReadState ReadSourceState(IFrameworkDebugSource source)
        {
            string sourceName;
            try
            {
                sourceName = source.Name;
                if (string.IsNullOrEmpty(sourceName))
                    sourceName = GetFallbackSourceName(source);
            }
            catch (Exception exception)
            {
                return DebugUiSourceReadState.Error(source, GetFallbackSourceName(source), default, exception);
            }

            FrameworkDebugMode mode;
            try
            {
                mode = source.Mode;
            }
            catch (Exception exception)
            {
                return DebugUiSourceReadState.Error(source, sourceName, default, exception);
            }

            bool isAvailable;
            try
            {
                isAvailable = source.IsAvailable;
            }
            catch (Exception exception)
            {
                return DebugUiSourceReadState.Error(source, sourceName, mode, exception);
            }

            return DebugUiSourceReadState.Available(source, sourceName, mode, isAvailable);
        }

        private static string GetFallbackSourceName(IFrameworkDebugSource source)
        {
            string typeName = source != null ? source.GetType().Name : null;
            return string.IsNullOrEmpty(typeName) ? "UnknownSource" : typeName;
        }

        private static int CompareSources(DebugUiSourceReadState left, DebugUiSourceReadState right)
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

        private sealed class DebugUiSourceReadState
        {
            private DebugUiSourceReadState(
                IFrameworkDebugSource source,
                string name,
                FrameworkDebugMode mode,
                bool isAvailable,
                Exception metadataException)
            {
                Source = source;
                Name = name ?? string.Empty;
                Mode = mode;
                IsAvailable = isAvailable;
                MetadataException = metadataException;
            }

            public IFrameworkDebugSource Source { get; }
            public string Name { get; }
            public FrameworkDebugMode Mode { get; }
            public bool IsAvailable { get; }
            public Exception MetadataException { get; }

            public static DebugUiSourceReadState Available(
                IFrameworkDebugSource source,
                string name,
                FrameworkDebugMode mode,
                bool isAvailable)
            {
                return new DebugUiSourceReadState(source, name, mode, isAvailable, null);
            }

            public static DebugUiSourceReadState Error(
                IFrameworkDebugSource source,
                string name,
                FrameworkDebugMode mode,
                Exception exception)
            {
                return new DebugUiSourceReadState(source, name, mode, isAvailable: false, metadataException: exception);
            }
        }
    }
}
