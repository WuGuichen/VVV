using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config;
using MxFramework.Runtime;
using MxFramework.Settings;
using MxFramework.Settings.UI;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestLocalizationRuntime
    {
        private const string LogCategory = "CharacterTest.Localization";
        private readonly MxSettingsUiOptionViewModel[] _localeOptions;
        private readonly IRuntimeLogger _logger;

        private CharacterTestLocalizationRuntime(
            string sourcePath,
            LocalizationTsvLoadResult loadResult,
            MxConfigUiTextProvider textProvider,
            IReadOnlyList<MxSettingsUiOptionViewModel> localeOptions,
            IRuntimeLogger logger)
        {
            SourcePath = sourcePath ?? string.Empty;
            LoadResult = loadResult ?? LocalizationTsvLoader.LoadFromText(string.Empty);
            TextProvider = textProvider ?? new MxConfigUiTextProvider(LoadResult.Provider);
            _localeOptions = CopyOptions(localeOptions);
            _logger = logger ?? NullRuntimeLogger.Instance;
        }

        public string SourcePath { get; }
        public LocalizationTsvLoadResult LoadResult { get; }
        public MxConfigUiTextProvider TextProvider { get; }
        public IReadOnlyList<MxSettingsUiOptionViewModel> LocaleOptions => _localeOptions;
        internal event Action<string> LocaleChanged;

        public static CharacterTestLocalizationRuntime Create(string sourcePath, IRuntimeLogger logger = null)
        {
            string text = string.Empty;
            if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
            {
                text = File.ReadAllText(sourcePath);
            }

            LocalizationTsvLoadResult result = LocalizationTsvLoader.LoadFromText(text);
            var runtime = new CharacterTestLocalizationRuntime(
                sourcePath,
                result,
                new MxConfigUiTextProvider(result.Provider, new MxUiLocaleId(MxLocalizationSettings.DefaultLocaleId)),
                CreateLocaleOptions(result),
                logger);
            runtime.LogLoadResult();
            return runtime;
        }

        public void SyncFromSettings(CharacterTestSettingsRuntime settingsRuntime)
        {
            string localeId = settingsRuntime?.CurrentProfile?.Localization?.LocaleId;
            SetLocale(localeId);
        }

        public void SetWorkingLocale(CharacterTestSettingsRuntime settingsRuntime, string localeId)
        {
            localeId = string.IsNullOrWhiteSpace(localeId)
                ? MxLocalizationSettings.DefaultLocaleId
                : localeId.Trim();
            settingsRuntime?.SettingsUi.SetLocalizationLocale(localeId);
            SetLocale(localeId);
        }

        public string Resolve(string key, string fallback)
        {
            return CharacterTestUiText.Resolve(TextProvider, key, fallback);
        }

        private void SetLocale(string localeId)
        {
            MxUiLocaleId previous = TextProvider.CurrentLocale;
            TextProvider.SetLocale(new MxUiLocaleId(localeId));
            if (TextProvider.CurrentLocale != previous)
                LocaleChanged?.Invoke(TextProvider.CurrentLocale.Value);
        }

        private void LogLoadResult()
        {
            if (_logger == null)
                return;

            if (string.IsNullOrWhiteSpace(SourcePath) || !File.Exists(SourcePath))
            {
                _logger.Warning(LogCategory, "Localization TSV is missing: " + SourcePath + ".");
                return;
            }

            if (LoadResult.Issues.Count == 0)
            {
                _logger.Info(LogCategory, "Localization TSV loaded from " + SourcePath + ".");
                return;
            }

            for (int i = 0; i < LoadResult.Issues.Count; i++)
            {
                LocalizationTsvIssue issue = LoadResult.Issues[i];
                string message = issue.Code
                    + " line "
                    + issue.LineNumber
                    + " key="
                    + issue.Key
                    + " locale="
                    + issue.Locale
                    + ": "
                    + issue.Message;
                if (issue.IsError)
                    _logger.Warning(LogCategory, message);
                else
                    _logger.Info(LogCategory, message);
            }
        }

        private static IReadOnlyList<MxSettingsUiOptionViewModel> CreateLocaleOptions(
            LocalizationTsvLoadResult result)
        {
            var options = new List<MxSettingsUiOptionViewModel>();
            IReadOnlyList<LocaleId> locales = result?.Locales ?? Array.Empty<LocaleId>();
            for (int i = 0; i < locales.Count; i++)
            {
                LocaleId locale = locales[i];
                if (!locale.IsValid)
                    continue;

                options.Add(new MxSettingsUiOptionViewModel(
                    locale.Value,
                    ResolveLocaleLabel(result.Provider, locale)));
            }

            if (options.Count == 0)
            {
                options.Add(new MxSettingsUiOptionViewModel(MxLocalizationSettings.DefaultLocaleId, "Chinese (Simplified)"));
                options.Add(new MxSettingsUiOptionViewModel("en-US", "English"));
            }

            return options;
        }

        private static string ResolveLocaleLabel(ILocalizationProvider provider, LocaleId locale)
        {
            if (provider != null &&
                provider.TryGetText(new LocalizedTextKey("locale." + locale.Value + ".label"), locale, out string label))
            {
                return label;
            }

            return string.Equals(locale.Value, MxLocalizationSettings.DefaultLocaleId, StringComparison.Ordinal)
                ? "Chinese (Simplified)"
                : locale.Value;
        }

        private static MxSettingsUiOptionViewModel[] CopyOptions(IReadOnlyList<MxSettingsUiOptionViewModel> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<MxSettingsUiOptionViewModel>();

            var copy = new MxSettingsUiOptionViewModel[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }
}
