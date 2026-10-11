namespace MxFramework.Settings
{
    public sealed class MxLocalizationSettings
    {
        public const string DefaultLocaleId = "zh-CN";

        public MxLocalizationSettings(string localeId = DefaultLocaleId)
        {
            LocaleId = string.IsNullOrWhiteSpace(localeId)
                ? DefaultLocaleId
                : localeId.Trim();
        }

        public string LocaleId { get; }

        public static MxLocalizationSettings Default => new MxLocalizationSettings();

        public MxLocalizationSettings WithLocaleId(string localeId)
        {
            return new MxLocalizationSettings(localeId);
        }
    }
}
