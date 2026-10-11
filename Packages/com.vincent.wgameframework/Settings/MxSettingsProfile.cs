using System;

namespace MxFramework.Settings
{
    public sealed class MxSettingsProfile
    {
        public const string ExpectedFormat = "mx.settings.v1";
        public const string DefaultProfileId = "default";

        public MxSettingsProfile(
            string profileId = DefaultProfileId,
            MxAudioSettings audio = null,
            string format = ExpectedFormat,
            string updatedUtc = "",
            MxGraphicsSettings graphics = null,
            MxLocalizationSettings localization = null)
        {
            Format = string.IsNullOrWhiteSpace(format) ? ExpectedFormat : format;
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? DefaultProfileId : profileId;
            Audio = audio ?? MxAudioSettings.Empty;
            Graphics = graphics ?? MxGraphicsSettings.Default;
            Localization = localization ?? MxLocalizationSettings.Default;
            UpdatedUtc = updatedUtc ?? string.Empty;
        }

        public MxSettingsProfile(
            string profileId,
            MxAudioSettings audio,
            string format,
            string updatedUtc)
            : this(profileId, audio, format, updatedUtc, null)
        {
        }

        public string Format { get; }
        public string ProfileId { get; }
        public MxAudioSettings Audio { get; }
        public MxGraphicsSettings Graphics { get; }
        public MxLocalizationSettings Localization { get; }
        public string UpdatedUtc { get; }

        public static MxSettingsProfile CreateDefault()
        {
            return new MxSettingsProfile();
        }

        public MxSettingsProfile WithAudio(MxAudioSettings audio)
        {
            return new MxSettingsProfile(ProfileId, audio, Format, UpdatedUtc, Graphics, Localization);
        }

        public MxSettingsProfile WithGraphics(MxGraphicsSettings graphics)
        {
            return new MxSettingsProfile(ProfileId, Audio, Format, UpdatedUtc, graphics, Localization);
        }

        public MxSettingsProfile WithLocalization(MxLocalizationSettings localization)
        {
            return new MxSettingsProfile(ProfileId, Audio, Format, UpdatedUtc, Graphics, localization);
        }

        public MxSettingsProfile WithUpdatedUtc(string updatedUtc)
        {
            return new MxSettingsProfile(ProfileId, Audio, Format, updatedUtc, Graphics, Localization);
        }

        public MxSettingsProfile WithUpdatedUtcNow()
        {
            return WithUpdatedUtc(DateTime.UtcNow.ToString("O"));
        }
    }
}
