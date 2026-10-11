using System;
using System.Collections.Generic;

namespace MxFramework.Config
{
    public enum LocalizationTsvIssueSeverity
    {
        Warning = 0,
        Error = 1
    }

    public enum LocalizationTsvIssueCode
    {
        InvalidHeader = 0,
        MissingKey = 1,
        MissingText = 2,
        DuplicateText = 3
    }

    public readonly struct LocalizationTsvIssue
    {
        public LocalizationTsvIssue(
            LocalizationTsvIssueSeverity severity,
            LocalizationTsvIssueCode code,
            int lineNumber,
            LocalizedTextKey key,
            LocaleId locale,
            string message)
        {
            Severity = severity;
            Code = code;
            LineNumber = lineNumber;
            Key = key;
            Locale = locale;
            Message = message ?? string.Empty;
        }

        public LocalizationTsvIssueSeverity Severity { get; }
        public LocalizationTsvIssueCode Code { get; }
        public int LineNumber { get; }
        public LocalizedTextKey Key { get; }
        public LocaleId Locale { get; }
        public string Message { get; }
        public bool IsError => Severity == LocalizationTsvIssueSeverity.Error;
    }

    public sealed class LocalizationTsvLoadResult
    {
        private readonly LocaleId[] _locales;
        private readonly LocalizedTextKey[] _keys;
        private readonly LocalizationTsvIssue[] _issues;

        public LocalizationTsvLoadResult(
            MemoryLocalizationProvider provider,
            IReadOnlyList<LocaleId> locales,
            IReadOnlyList<LocalizedTextKey> keys,
            IReadOnlyList<LocalizationTsvIssue> issues)
        {
            Provider = provider ?? new MemoryLocalizationProvider();
            _locales = Copy(locales);
            _keys = Copy(keys);
            _issues = Copy(issues);
            for (int i = 0; i < _issues.Length; i++)
            {
                if (_issues[i].IsError)
                {
                    HasErrors = true;
                    break;
                }
            }
        }

        public MemoryLocalizationProvider Provider { get; }
        public IReadOnlyList<LocaleId> Locales => _locales;
        public IReadOnlyList<LocalizedTextKey> Keys => _keys;
        public IReadOnlyList<LocalizationTsvIssue> Issues => _issues;
        public bool HasErrors { get; }

        private static LocaleId[] Copy(IReadOnlyList<LocaleId> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<LocaleId>();

            var copy = new LocaleId[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        private static LocalizedTextKey[] Copy(IReadOnlyList<LocalizedTextKey> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<LocalizedTextKey>();

            var copy = new LocalizedTextKey[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        private static LocalizationTsvIssue[] Copy(IReadOnlyList<LocalizationTsvIssue> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<LocalizationTsvIssue>();

            var copy = new LocalizationTsvIssue[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public static class LocalizationTsvLoader
    {
        private const string HeaderKey = "key";

        public static LocalizationTsvLoadResult LoadFromText(string text)
        {
            var provider = new MemoryLocalizationProvider();
            var locales = new List<LocaleId>();
            var localeColumns = new List<int>();
            var keys = new List<LocalizedTextKey>();
            var issues = new List<LocalizationTsvIssue>();
            var keySet = new HashSet<LocalizedTextKey>();
            var registered = new HashSet<string>(StringComparer.Ordinal);

            string[] lines = SplitLines(text ?? string.Empty);
            if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
            {
                issues.Add(Error(
                    LocalizationTsvIssueCode.InvalidHeader,
                    1,
                    default,
                    default,
                    "Localization TSV header is required."));
                return new LocalizationTsvLoadResult(provider, locales, keys, issues);
            }

            string[] header = lines[0].Split('\t');
            if (header.Length < 2 || !string.Equals(header[0].Trim(), HeaderKey, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    LocalizationTsvIssueCode.InvalidHeader,
                    1,
                    default,
                    default,
                    "Localization TSV header must start with key and include at least one locale column."));
                return new LocalizationTsvLoadResult(provider, locales, keys, issues);
            }

            for (int column = 1; column < header.Length; column++)
            {
                var locale = new LocaleId(header[column].Trim());
                if (!locale.IsValid)
                {
                    issues.Add(Error(
                        LocalizationTsvIssueCode.InvalidHeader,
                        1,
                        default,
                        locale,
                        "Localization TSV locale header is empty at column " + (column + 1) + "."));
                    continue;
                }

                locales.Add(locale);
                localeColumns.Add(column);
            }

            if (locales.Count == 0)
            {
                issues.Add(Error(
                    LocalizationTsvIssueCode.InvalidHeader,
                    1,
                    default,
                    default,
                    "Localization TSV must include at least one valid locale column."));
                return new LocalizationTsvLoadResult(provider, locales, keys, issues);
            }

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] cells = line.Split('\t');
                var key = new LocalizedTextKey(cells.Length > 0 ? cells[0].Trim() : string.Empty);
                int lineNumber = lineIndex + 1;
                if (!key.IsValid)
                {
                    issues.Add(Error(
                        LocalizationTsvIssueCode.MissingKey,
                        lineNumber,
                        key,
                        default,
                        "Localization TSV row key is required."));
                    continue;
                }

                if (keySet.Add(key))
                    keys.Add(key);

                for (int localeIndex = 0; localeIndex < locales.Count; localeIndex++)
                {
                    LocaleId locale = locales[localeIndex];
                    int column = localeColumns[localeIndex];
                    string value = column < cells.Length ? cells[column].Trim() : string.Empty;
                    if (string.IsNullOrEmpty(value))
                    {
                        issues.Add(Warning(
                            LocalizationTsvIssueCode.MissingText,
                            lineNumber,
                            key,
                            locale,
                            "Localization TSV text is empty for key " + key + " locale " + locale + "."));
                        continue;
                    }

                    string registrationKey = key.Value + "\n" + locale.Value;
                    if (!registered.Add(registrationKey))
                    {
                        issues.Add(Warning(
                            LocalizationTsvIssueCode.DuplicateText,
                            lineNumber,
                            key,
                            locale,
                            "Localization TSV duplicate key/locale overrides previous text. Key=" + key + ", Locale=" + locale + "."));
                    }

                    provider.Register(key, locale, value);
                }
            }

            return new LocalizationTsvLoadResult(provider, locales, keys, issues);
        }

        private static string[] SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static LocalizationTsvIssue Error(
            LocalizationTsvIssueCode code,
            int lineNumber,
            LocalizedTextKey key,
            LocaleId locale,
            string message)
        {
            return new LocalizationTsvIssue(
                LocalizationTsvIssueSeverity.Error,
                code,
                lineNumber,
                key,
                locale,
                message);
        }

        private static LocalizationTsvIssue Warning(
            LocalizationTsvIssueCode code,
            int lineNumber,
            LocalizedTextKey key,
            LocaleId locale,
            string message)
        {
            return new LocalizationTsvIssue(
                LocalizationTsvIssueSeverity.Warning,
                code,
                lineNumber,
                key,
                locale,
                message);
        }
    }
}
