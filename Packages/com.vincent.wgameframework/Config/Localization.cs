using System.Collections.Generic;

namespace MxFramework.Config
{
    public interface ILocalizedTextConfig : IConfigData
    {
        LocalizedTextKey Key { get; }
        bool TryGetText(LocaleId locale, out string text);
    }

    public interface ILocalizationProvider
    {
        bool TryGetText(LocalizedTextKey key, LocaleId locale, out string text);
    }

    public sealed class MemoryLocalizationProvider : ILocalizationProvider
    {
        private readonly Dictionary<LocalizedTextKey, Dictionary<LocaleId, string>> _texts;

        public MemoryLocalizationProvider()
        {
            _texts = new Dictionary<LocalizedTextKey, Dictionary<LocaleId, string>>();
        }

        public void Register(LocalizedTextKey key, LocaleId locale, string text)
        {
            if (!_texts.TryGetValue(key, out Dictionary<LocaleId, string> table))
            {
                table = new Dictionary<LocaleId, string>();
                _texts.Add(key, table);
            }

            table[locale] = text ?? string.Empty;
        }

        public bool TryGetText(LocalizedTextKey key, LocaleId locale, out string text)
        {
            text = string.Empty;
            return _texts.TryGetValue(key, out Dictionary<LocaleId, string> table)
                && table.TryGetValue(locale, out text)
                && !string.IsNullOrEmpty(text);
        }
    }
}
