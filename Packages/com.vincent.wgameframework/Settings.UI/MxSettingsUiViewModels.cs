using System;
using System.Collections.Generic;

namespace MxFramework.Settings.UI
{
    public enum MxSettingsUiSectionKey
    {
        Graphics = 0,
        Audio = 1,
        Input = 2,
        Localization = 3
    }

    public enum MxSettingsUiItemKey
    {
        GraphicsQualityPreset = 0,
        GraphicsResolution = 1,
        GraphicsWindowMode = 2,
        GraphicsVSync = 3,
        GraphicsTargetFrameRate = 4,
        GraphicsRenderScale = 5,
        GraphicsAntiAliasingSamples = 6,
        GraphicsShadowDistance = 7,
        GraphicsMainLightShadowmapResolution = 8,
        GraphicsShadowCascadeCount = 9,
        GraphicsAdditionalLightsShadowmapResolution = 10,
        GraphicsHdrOutput = 11,
        GraphicsHdrPaperWhite = 12,
        GraphicsHdrPeakBrightness = 13,
        GraphicsDirectionalShadowQuality = 14,
        GraphicsWaterQuality = 15,
        GraphicsRippleQuality = 16,
        AudioBusVolume = 20,
        AudioBusMuted = 21,
        InputBinding = 40,
        LocalizationLocale = 60
    }

    public enum MxSettingsUiControlKind
    {
        Label = 0,
        Toggle = 1,
        Integer = 2,
        Float = 3,
        Select = 4,
        Command = 5
    }

    public sealed class MxSettingsUiOptionViewModel
    {
        public MxSettingsUiOptionViewModel(string value, string label)
        {
            Value = value ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public string Value { get; }
        public string Label { get; }
    }

    public sealed class MxSettingsUiItemViewModel
    {
        private readonly MxSettingsUiOptionViewModel[] _options;

        public MxSettingsUiItemViewModel(
            MxSettingsUiSectionKey section,
            MxSettingsUiItemKey key,
            string id,
            string label,
            string value,
            string valueText,
            MxSettingsUiControlKind controlKind,
            bool enabled = true,
            bool dirty = false,
            IReadOnlyList<MxSettingsUiOptionViewModel> options = null)
        {
            Section = section;
            Key = key;
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            ValueText = valueText ?? string.Empty;
            ControlKind = controlKind;
            Enabled = enabled;
            Dirty = dirty;
            _options = Copy(options);
        }

        public MxSettingsUiSectionKey Section { get; }
        public MxSettingsUiItemKey Key { get; }
        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
        public string ValueText { get; }
        public MxSettingsUiControlKind ControlKind { get; }
        public bool Enabled { get; }
        public bool Dirty { get; }
        public IReadOnlyList<MxSettingsUiOptionViewModel> Options => _options;

        private static MxSettingsUiOptionViewModel[] Copy(IReadOnlyList<MxSettingsUiOptionViewModel> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<MxSettingsUiOptionViewModel>();

            var copy = new MxSettingsUiOptionViewModel[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public sealed class MxSettingsUiSectionViewModel
    {
        private readonly MxSettingsUiItemViewModel[] _items;

        public MxSettingsUiSectionViewModel(
            MxSettingsUiSectionKey key,
            string title,
            IReadOnlyList<MxSettingsUiItemViewModel> items)
        {
            Key = key;
            Title = title ?? string.Empty;
            _items = Copy(items);
        }

        public MxSettingsUiSectionKey Key { get; }
        public string Title { get; }
        public IReadOnlyList<MxSettingsUiItemViewModel> Items => _items;
        public int ItemCount => _items.Length;

        private static MxSettingsUiItemViewModel[] Copy(IReadOnlyList<MxSettingsUiItemViewModel> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<MxSettingsUiItemViewModel>();

            var copy = new MxSettingsUiItemViewModel[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public sealed class MxSettingsUiViewModel
    {
        private readonly MxSettingsUiSectionViewModel[] _sections;
        private readonly MxSettingsIssue[] _issues;

        public MxSettingsUiViewModel(
            string profileId,
            string updatedUtc,
            bool dirty,
            bool isRebinding,
            IReadOnlyList<MxSettingsUiSectionViewModel> sections,
            IReadOnlyList<MxSettingsIssue> issues)
        {
            ProfileId = profileId ?? string.Empty;
            UpdatedUtc = updatedUtc ?? string.Empty;
            Dirty = dirty;
            IsRebinding = isRebinding;
            _sections = Copy(sections);
            _issues = CopyIssues(issues);
        }

        public string ProfileId { get; }
        public string UpdatedUtc { get; }
        public bool Dirty { get; }
        public bool IsRebinding { get; }
        public IReadOnlyList<MxSettingsUiSectionViewModel> Sections => _sections;
        public IReadOnlyList<MxSettingsIssue> Issues => _issues;
        public bool HasIssues => _issues.Length > 0;

        private static MxSettingsUiSectionViewModel[] Copy(IReadOnlyList<MxSettingsUiSectionViewModel> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<MxSettingsUiSectionViewModel>();

            var copy = new MxSettingsUiSectionViewModel[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        private static MxSettingsIssue[] CopyIssues(IReadOnlyList<MxSettingsIssue> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<MxSettingsIssue>();

            var issues = new List<MxSettingsIssue>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                if (!values[i].IsNone)
                    issues.Add(values[i]);
            }

            return issues.Count == 0 ? Array.Empty<MxSettingsIssue>() : issues.ToArray();
        }
    }

    public sealed class MxSettingsUiOperationResult
    {
        private readonly MxSettingsIssue[] _issues;

        private MxSettingsUiOperationResult(bool success, IReadOnlyList<MxSettingsIssue> issues)
        {
            Success = success;
            _issues = CopyIssues(issues);
        }

        public bool Success { get; }
        public IReadOnlyList<MxSettingsIssue> Issues => _issues;
        public bool HasIssues => _issues.Length > 0;

        public static MxSettingsUiOperationResult Succeeded()
        {
            return new MxSettingsUiOperationResult(true, Array.Empty<MxSettingsIssue>());
        }

        public static MxSettingsUiOperationResult FromLoad(MxSettingsLoadResult result)
        {
            return new MxSettingsUiOperationResult(result != null && result.Success, result?.Issues);
        }

        public static MxSettingsUiOperationResult FromApply(MxSettingsApplyResult result)
        {
            return new MxSettingsUiOperationResult(result != null && result.Success, result?.Issues);
        }

        public static MxSettingsUiOperationResult FromSave(MxSettingsSaveResult result)
        {
            return new MxSettingsUiOperationResult(result != null && result.Success, result?.Issues);
        }

        public static MxSettingsUiOperationResult FromInput(MxFramework.Settings.Input.MxInputSettingsOperationResult result)
        {
            return new MxSettingsUiOperationResult(result != null && result.Success, result?.Issues);
        }

        private static MxSettingsIssue[] CopyIssues(IReadOnlyList<MxSettingsIssue> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<MxSettingsIssue>();

            var issues = new List<MxSettingsIssue>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                if (!values[i].IsNone)
                    issues.Add(values[i]);
            }

            return issues.Count == 0 ? Array.Empty<MxSettingsIssue>() : issues.ToArray();
        }
    }
}
