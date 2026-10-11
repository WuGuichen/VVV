using System;
using MxFramework.Config;
using MxFramework.UI;

namespace MxFramework.UI.ConfigBridge
{
    public sealed class MxConfigUiTextProvider : IMxUiTextProvider
    {
        private readonly ILocalizationProvider _localizationProvider;
        private MxUiLocaleId _currentLocale;
        private long _revision;

        public MxConfigUiTextProvider(
            ILocalizationProvider localizationProvider,
            MxUiLocaleId currentLocale = default)
        {
            _localizationProvider = localizationProvider ?? new MemoryLocalizationProvider();
            _currentLocale = currentLocale.IsValid ? currentLocale : new MxUiLocaleId(LocaleId.ZhCN.Value);
        }

        public MxUiLocaleId CurrentLocale => _currentLocale;
        public long Revision => _revision;

        public bool TryGetText(MxUiLocalizedTextRequest request, out string text)
        {
            text = string.Empty;
            if (request.IsValid &&
                _currentLocale.IsValid &&
                _localizationProvider.TryGetText(
                    new LocalizedTextKey(request.Key.Value),
                    new LocaleId(_currentLocale.Value),
                    out text))
            {
                return true;
            }

            text = request.FallbackText ?? string.Empty;
            return !string.IsNullOrEmpty(text);
        }

        public void SetLocale(MxUiLocaleId locale)
        {
            if (!locale.IsValid)
                locale = new MxUiLocaleId(LocaleId.ZhCN.Value);

            if (string.Equals(_currentLocale.Value, locale.Value, StringComparison.Ordinal))
                return;

            _currentLocale = locale;
            _revision++;
        }
    }
}
