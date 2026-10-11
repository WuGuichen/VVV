using System;
using System.Collections.Generic;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI
{
    public enum DebugUiSourceStatus
    {
        Available = 0,
        Unavailable = 1,
        Error = 2
    }

    public sealed class DebugUiDashboardViewModel
    {
        private readonly DebugUiSourceViewModel[] _sources;
        private readonly DebugUiErrorViewModel[] _errors;

        public DebugUiDashboardViewModel(
            long refreshSequence,
            IReadOnlyList<DebugUiSourceViewModel> sources,
            IReadOnlyList<DebugUiErrorViewModel> errors)
        {
            RefreshSequence = refreshSequence;
            _sources = Copy(sources);
            _errors = Copy(errors);
        }

        public static DebugUiDashboardViewModel Empty { get; } =
            new DebugUiDashboardViewModel(0, Array.Empty<DebugUiSourceViewModel>(), Array.Empty<DebugUiErrorViewModel>());

        public long RefreshSequence { get; }
        public IReadOnlyList<DebugUiSourceViewModel> Sources => _sources;
        public IReadOnlyList<DebugUiErrorViewModel> Errors => _errors;
        public int SourceCount => _sources.Length;
        public int ErrorCount => _errors.Length;
        public bool HasErrors => _errors.Length > 0;

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<T>();

            var copy = new T[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public sealed class DebugUiSourceViewModel
    {
        private readonly DebugUiSectionViewModel[] _sections;

        public DebugUiSourceViewModel(
            string sourceName,
            FrameworkDebugMode mode,
            DebugUiSourceStatus status,
            IReadOnlyList<DebugUiSectionViewModel> sections,
            string statusMessage = "")
        {
            SourceName = sourceName ?? string.Empty;
            Mode = mode;
            Status = status;
            StatusMessage = statusMessage ?? string.Empty;
            _sections = Copy(sections);
        }

        public string SourceName { get; }
        public FrameworkDebugMode Mode { get; }
        public DebugUiSourceStatus Status { get; }
        public string StatusMessage { get; }
        public bool IsAvailable => Status == DebugUiSourceStatus.Available;
        public bool HasError => Status == DebugUiSourceStatus.Error;
        public IReadOnlyList<DebugUiSectionViewModel> Sections => _sections;

        private static DebugUiSectionViewModel[] Copy(IReadOnlyList<DebugUiSectionViewModel> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<DebugUiSectionViewModel>();

            var copy = new DebugUiSectionViewModel[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public sealed class DebugUiSectionViewModel
    {
        public DebugUiSectionViewModel(string title, string body)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public string Title { get; }
        public string Body { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Body);
    }

    public sealed class DebugUiErrorViewModel
    {
        public DebugUiErrorViewModel(string sourceName, string exceptionType, string message)
        {
            SourceName = sourceName ?? string.Empty;
            ExceptionType = exceptionType ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string SourceName { get; }
        public string ExceptionType { get; }
        public string Message { get; }
    }
}
