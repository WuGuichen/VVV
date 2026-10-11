using System.Collections.Generic;
using System.Text;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Runtime.Unity
{
    public sealed class UnityRuntimeLogger : IRuntimeLogger
    {
        private const string ColorOpen = "<color=";
        private const string ColorClose = "</color>";
        private const string DefaultInfoHeaderColor = "#7EC8E3";
        private const string DefaultInfoBodyColor = "#E8ECEF";
        private const string DefaultWarningHeaderColor = "#F4D03F";
        private const string DefaultWarningBodyColor = "#FCF3CF";
        private const string DefaultErrorHeaderColor = "#E74C3C";
        private const string DefaultErrorBodyColor = "#FADBD8";

        private readonly Object _context;
        private readonly string _headerPrefix;
        private readonly StringBuilder _formatBuilder = new StringBuilder(256);
        private readonly Dictionary<string, string> _categoryHeaderColors =
            new Dictionary<string, string>(System.StringComparer.Ordinal);

        public UnityRuntimeLogger(Object context = null, string prefix = "MxFramework")
        {
            _context = context;
            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "MxFramework" : prefix;
            _headerPrefix = "[" + safePrefix + ".";

            Enabled = true;
            UseRichTextColors = true;
            ColorBody = true;
            UseCategoryHeaderColors = true;

            InfoHeaderColor = DefaultInfoHeaderColor;
            InfoBodyColor = DefaultInfoBodyColor;
            WarningHeaderColor = DefaultWarningHeaderColor;
            WarningBodyColor = DefaultWarningBodyColor;
            ErrorHeaderColor = DefaultErrorHeaderColor;
            ErrorBodyColor = DefaultErrorBodyColor;
        }

        public bool Enabled { get; set; }
        public bool UseRichTextColors { get; set; }
        public bool ColorBody { get; set; }
        public bool UseCategoryHeaderColors { get; set; }
        public string InfoHeaderColor { get; set; }
        public string InfoBodyColor { get; set; }
        public string WarningHeaderColor { get; set; }
        public string WarningBodyColor { get; set; }
        public string ErrorHeaderColor { get; set; }
        public string ErrorBodyColor { get; set; }

        public void SetCategoryHeaderColor(string category, string color)
        {
            if (string.IsNullOrWhiteSpace(category))
                return;

            if (string.IsNullOrWhiteSpace(color))
                _categoryHeaderColors.Remove(category);
            else
                _categoryHeaderColors[category] = color;
        }

        public void ClearCategoryHeaderColors()
        {
            _categoryHeaderColors.Clear();
        }

        public void Log(RuntimeLogLevel level, string category, string message)
        {
            if (!Enabled)
                return;

            string text = Format(level, category, message);
            switch (level)
            {
                case RuntimeLogLevel.Warning:
                    Debug.LogWarning(text, _context);
                    break;
                case RuntimeLogLevel.Error:
                    Debug.LogError(text, _context);
                    break;
                default:
                    Debug.Log(text, _context);
                    break;
            }
        }

        private string Format(RuntimeLogLevel level, string category, string message)
        {
            _formatBuilder.Clear();
            if (!UseRichTextColors)
            {
                AppendPlainHeader(_formatBuilder, category);
                _formatBuilder.Append(' ').Append(message ?? string.Empty);
                return _formatBuilder.ToString();
            }

            AppendColoredHeader(_formatBuilder, level, category);
            _formatBuilder.Append(' ');
            if (ColorBody)
                AppendColoredBody(_formatBuilder, level, message);
            else
                _formatBuilder.Append(message ?? string.Empty);

            return _formatBuilder.ToString();
        }

        private void AppendPlainHeader(StringBuilder builder, string category)
        {
            builder.Append(_headerPrefix);
            AppendCategory(builder, category);
            builder.Append(']');
        }

        private void AppendColoredHeader(StringBuilder builder, RuntimeLogLevel level, string category)
        {
            builder.Append(ColorOpen)
                .Append(ResolveHeaderColor(level, category))
                .Append('>');
            AppendPlainHeader(builder, category);
            builder.Append(ColorClose);
        }

        private void AppendColoredBody(StringBuilder builder, RuntimeLogLevel level, string message)
        {
            builder.Append(ColorOpen)
                .Append(ResolveLevelBodyColor(level))
                .Append('>')
                .Append(message ?? string.Empty)
                .Append(ColorClose);
        }

        private static void AppendCategory(StringBuilder builder, string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                builder.Append("Runtime");
            else
                builder.Append(category);
        }

        private string ResolveHeaderColor(RuntimeLogLevel level, string category)
        {
            if (UseCategoryHeaderColors
                && !string.IsNullOrWhiteSpace(category)
                && _categoryHeaderColors.TryGetValue(category, out string color)
                && !string.IsNullOrWhiteSpace(color))
            {
                return color;
            }

            return ResolveLevelHeaderColor(level);
        }

        private string ResolveLevelHeaderColor(RuntimeLogLevel level)
        {
            switch (level)
            {
                case RuntimeLogLevel.Warning:
                    return SafeColor(WarningHeaderColor, DefaultWarningHeaderColor);
                case RuntimeLogLevel.Error:
                    return SafeColor(ErrorHeaderColor, DefaultErrorHeaderColor);
                default:
                    return SafeColor(InfoHeaderColor, DefaultInfoHeaderColor);
            }
        }

        private string ResolveLevelBodyColor(RuntimeLogLevel level)
        {
            switch (level)
            {
                case RuntimeLogLevel.Warning:
                    return SafeColor(WarningBodyColor, DefaultWarningBodyColor);
                case RuntimeLogLevel.Error:
                    return SafeColor(ErrorBodyColor, DefaultErrorBodyColor);
                default:
                    return SafeColor(InfoBodyColor, DefaultInfoBodyColor);
            }
        }

        private static string SafeColor(string color, string fallback)
        {
            return string.IsNullOrWhiteSpace(color) ? fallback : color;
        }
    }
}
