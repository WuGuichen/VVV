using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MxFramework.Settings
{
    public static class MxSettingsProfileJson
    {
        private sealed class ProfileDto
        {
            [JsonProperty("format")]
            public string Format { get; set; }

            [JsonProperty("profileId")]
            public string ProfileId { get; set; }

            [JsonProperty("audio")]
            public AudioDto Audio { get; set; }

            [JsonProperty("graphics")]
            public GraphicsDto Graphics { get; set; }

            [JsonProperty("localization")]
            public LocalizationDto Localization { get; set; }

            [JsonProperty("updatedUtc")]
            public string UpdatedUtc { get; set; }
        }

        private sealed class AudioDto
        {
            [JsonProperty("busSettings")]
            public List<AudioBusDto> BusSettings { get; set; }
        }

        private sealed class AudioBusDto
        {
            [JsonProperty("busId")]
            public int BusId { get; set; }

            [JsonProperty("volume")]
            public float Volume { get; set; }

            [JsonProperty("muted")]
            public bool Muted { get; set; }
        }

        private sealed class GraphicsDto
        {
            [JsonProperty("directionalShadowQuality")]
            public int? DirectionalShadowQuality { get; set; }

            [JsonProperty("hdrOutput")]
            public HdrOutputDto HdrOutput { get; set; }

            [JsonProperty("qualityPreset")]
            public int? QualityPreset { get; set; }

            [JsonProperty("resolutionWidth")]
            public int? ResolutionWidth { get; set; }

            [JsonProperty("resolutionHeight")]
            public int? ResolutionHeight { get; set; }

            [JsonProperty("applyWindowMode")]
            public bool? ApplyWindowMode { get; set; }

            [JsonProperty("windowMode")]
            public int? WindowMode { get; set; }

            [JsonProperty("vSyncCount")]
            public int? VSyncCount { get; set; }

            [JsonProperty("targetFrameRate")]
            public int? TargetFrameRate { get; set; }

            [JsonProperty("renderScale")]
            public float? RenderScale { get; set; }

            [JsonProperty("antiAliasingSamples")]
            public int? AntiAliasingSamples { get; set; }

            [JsonProperty("shadowDistance")]
            public float? ShadowDistance { get; set; }

            [JsonProperty("mainLightShadowmapResolution")]
            public int? MainLightShadowmapResolution { get; set; }

            [JsonProperty("shadowCascadeCount")]
            public int? ShadowCascadeCount { get; set; }

            [JsonProperty("additionalLightsShadowmapResolution")]
            public int? AdditionalLightsShadowmapResolution { get; set; }

            [JsonProperty("volumetricFogQuality")]
            public int? VolumetricFogQuality { get; set; }

            [JsonProperty("unityQualityLevel")]
            public int? UnityQualityLevel { get; set; }
        }

        private sealed class LocalizationDto
        {
            [JsonProperty("localeId")]
            public string LocaleId { get; set; }
        }

        private sealed class HdrOutputDto
        {
            [JsonProperty("mode")] public int? Mode { get; set; }
            [JsonProperty("paperWhiteNits")] public float? PaperWhiteNits { get; set; }
            [JsonProperty("peakNits")] public float? PeakNits { get; set; }
        }

        public static MxSettingsLoadResult LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return DefaultWithIssue(
                    MxSettingsIssueCode.InvalidJson,
                    "$",
                    "Settings json is null or empty.");
            }

            ProfileDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<ProfileDto>(json);
            }
            catch (Exception ex)
            {
                return DefaultWithIssue(
                    MxSettingsIssueCode.InvalidJson,
                    "$",
                    "Settings json is invalid: " + ex.Message,
                    ex);
            }

            if (dto == null)
            {
                return DefaultWithIssue(
                    MxSettingsIssueCode.InvalidJson,
                    "$",
                    "Settings json parsed to null.");
            }

            if (!string.Equals(dto.Format, MxSettingsProfile.ExpectedFormat, StringComparison.Ordinal))
            {
                return DefaultWithIssue(
                    MxSettingsIssueCode.UnsupportedFormat,
                    "$.format",
                    "Unsupported settings format: " + (dto.Format ?? "(null)") + ".");
            }

            if (string.IsNullOrWhiteSpace(dto.ProfileId))
            {
                return DefaultWithIssue(
                    MxSettingsIssueCode.InvalidProfile,
                    "$.profileId",
                    "Settings profileId is required.");
            }

            var issues = new List<MxSettingsIssue>();
            MxGraphicsSettings graphics = ToGraphicsSettings(dto.Graphics, issues);
            var profile = new MxSettingsProfile(
                dto.ProfileId,
                ToAudioSettings(dto.Audio),
                dto.Format,
                dto.UpdatedUtc ?? string.Empty,
                graphics,
                ToLocalizationSettings(dto.Localization));

            return issues.Count == 0
                ? MxSettingsLoadResult.Succeeded(profile)
                : MxSettingsLoadResult.WithIssues(profile, issues);
        }

        public static string SaveToJson(MxSettingsProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (!string.Equals(profile.Format, MxSettingsProfile.ExpectedFormat, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported settings format: " + profile.Format + ".");

            if (string.IsNullOrWhiteSpace(profile.ProfileId))
                throw new InvalidOperationException("Settings profileId is required.");

            var dto = new ProfileDto
            {
                Format = MxSettingsProfile.ExpectedFormat,
                ProfileId = profile.ProfileId,
                Audio = ToDto(profile.Audio),
                Graphics = ToDto(profile.Graphics),
                Localization = ToDto(profile.Localization),
                UpdatedUtc = profile.UpdatedUtc ?? string.Empty
            };

            return JsonConvert.SerializeObject(dto, Formatting.Indented);
        }

        private static MxSettingsLoadResult DefaultWithIssue(
            MxSettingsIssueCode code,
            string path,
            string message,
            Exception exception = null)
        {
            return MxSettingsLoadResult.WithIssue(
                MxSettingsProfile.CreateDefault(),
                new MxSettingsIssue(code, path, message, exception));
        }

        private static MxAudioSettings ToAudioSettings(AudioDto dto)
        {
            if (dto?.BusSettings == null || dto.BusSettings.Count == 0)
                return MxAudioSettings.Empty;

            var buses = new MxAudioBusSetting[dto.BusSettings.Count];
            for (int i = 0; i < dto.BusSettings.Count; i++)
            {
                AudioBusDto bus = dto.BusSettings[i] ?? new AudioBusDto();
                buses[i] = new MxAudioBusSetting(bus.BusId, bus.Volume, bus.Muted);
            }

            return new MxAudioSettings(buses);
        }

        private static AudioDto ToDto(MxAudioSettings settings)
        {
            IReadOnlyList<MxAudioBusSetting> buses =
                settings != null ? settings.BusSettings : Array.Empty<MxAudioBusSetting>();
            var dto = new AudioDto
            {
                BusSettings = new List<AudioBusDto>(buses.Count)
            };

            for (int i = 0; i < buses.Count; i++)
            {
                dto.BusSettings.Add(new AudioBusDto
                {
                    BusId = buses[i].BusId,
                    Volume = buses[i].Volume,
                    Muted = buses[i].Muted
                });
            }

            return dto;
        }

        private static MxGraphicsSettings ToGraphicsSettings(
            GraphicsDto dto,
            List<MxSettingsIssue> issues)
        {
            if (dto == null)
                return MxGraphicsSettings.Default;

            int width = dto.ResolutionWidth ?? 0;
            int height = dto.ResolutionHeight ?? 0;
            if ((width > 0 && height <= 0) || (height > 0 && width <= 0))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    "$.graphics.resolution",
                    "Resolution width and height must both be positive to apply."));
                width = 0;
                height = 0;
            }

            AddIssueIfInvalidEnum(
                dto.QualityPreset,
                "$.graphics.qualityPreset",
                IsDefinedQualityPreset,
                issues);
            AddIssueIfInvalidEnum(
                dto.WindowMode,
                "$.graphics.windowMode",
                IsDefinedWindowMode,
                issues);
            AddIssueIfInvalidEnum(
                dto.VolumetricFogQuality,
                "$.graphics.volumetricFogQuality",
                IsDefinedFeatureQuality,
                issues);
            AddIssueIfInvalidEnum(dto.DirectionalShadowQuality, "$.graphics.directionalShadowQuality",
                value => value == 0 || value >= 2 && value <= 4, issues);
            AddIssueIfOutsideRange(dto.VSyncCount, -1, 2, "$.graphics.vSyncCount", issues);
            AddIssueIfOutsidePositiveRange(dto.TargetFrameRate, 15, 360, "$.graphics.targetFrameRate", issues);
            AddIssueIfOutsidePositiveRange(dto.RenderScale, 0.5f, 2f, "$.graphics.renderScale", issues);
            if (dto.AntiAliasingSamples.HasValue &&
                dto.AntiAliasingSamples.Value > 0 &&
                !IsSupportedAntiAliasingSamples(dto.AntiAliasingSamples.Value))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    "$.graphics.antiAliasingSamples",
                    "Anti-aliasing samples must be 1, 2, 4, or 8."));
            }

            AddIssueIfOutsideRange(dto.ShadowDistance, -1f, 500f, "$.graphics.shadowDistance", issues);
            AddIssueIfUnsupportedShadowmapResolution(
                dto.MainLightShadowmapResolution,
                "$.graphics.mainLightShadowmapResolution",
                issues);
            AddIssueIfOutsideRange(dto.ShadowCascadeCount, 0, 4, "$.graphics.shadowCascadeCount", issues);
            AddIssueIfUnsupportedShadowmapResolution(
                dto.AdditionalLightsShadowmapResolution,
                "$.graphics.additionalLightsShadowmapResolution",
                issues);
            AddIssueIfOutsideRange(dto.UnityQualityLevel, -1, int.MaxValue, "$.graphics.unityQualityLevel", issues);

            return new MxGraphicsSettings(
                ToQualityPreset(dto.QualityPreset),
                width,
                height,
                dto.ApplyWindowMode ?? false,
                ToWindowMode(dto.WindowMode),
                dto.VSyncCount ?? -1,
                dto.TargetFrameRate ?? 0,
                dto.RenderScale ?? 0f,
                dto.AntiAliasingSamples ?? 0,
                dto.ShadowDistance ?? -1f,
                ToFeatureQuality(dto.VolumetricFogQuality),
                dto.UnityQualityLevel ?? -1,
                dto.MainLightShadowmapResolution ?? 0,
                dto.ShadowCascadeCount ?? 0,
                dto.AdditionalLightsShadowmapResolution ?? 0,
                ToHdrOutput(dto.HdrOutput, issues), ToFeatureQuality(dto.DirectionalShadowQuality));
        }

        private static GraphicsDto ToDto(MxGraphicsSettings settings)
        {
            MxGraphicsSettings graphics = settings ?? MxGraphicsSettings.Default;
            return new GraphicsDto
            {
                QualityPreset = (int)graphics.QualityPreset,
                DirectionalShadowQuality = (int)graphics.DirectionalShadowQuality,
                ResolutionWidth = graphics.ResolutionWidth,
                ResolutionHeight = graphics.ResolutionHeight,
                ApplyWindowMode = graphics.ApplyWindowMode,
                WindowMode = (int)graphics.WindowMode,
                VSyncCount = graphics.VSyncCount,
                TargetFrameRate = graphics.TargetFrameRate,
                RenderScale = graphics.RenderScale,
                AntiAliasingSamples = graphics.AntiAliasingSamples,
                ShadowDistance = graphics.ShadowDistance,
                MainLightShadowmapResolution = graphics.MainLightShadowmapResolution,
                ShadowCascadeCount = graphics.ShadowCascadeCount,
                AdditionalLightsShadowmapResolution = graphics.AdditionalLightsShadowmapResolution,
                HdrOutput = new HdrOutputDto { Mode = (int)graphics.HdrOutput.Mode,
                    PaperWhiteNits = graphics.HdrOutput.PaperWhiteNits, PeakNits = graphics.HdrOutput.PeakNits },
                VolumetricFogQuality = (int)graphics.VolumetricFogQuality,
                UnityQualityLevel = graphics.UnityQualityLevel
            };
        }

        private static MxHdrOutputSettings ToHdrOutput(HdrOutputDto dto, List<MxSettingsIssue> issues)
        {
            if (dto == null) return MxHdrOutputSettings.Default;
            AddIssueIfOutsideRange(dto.Mode, 0, 2, "$.graphics.hdrOutput.mode", issues);
            ValidateHdrLuminance(dto.PaperWhiteNits, 80, 400, "$.graphics.hdrOutput.paperWhiteNits", issues);
            ValidateHdrLuminance(dto.PeakNits, 400, 5000, "$.graphics.hdrOutput.peakNits", issues);
            return new MxHdrOutputSettings((MxHdrOutputMode)(dto.Mode ?? 0), dto.PaperWhiteNits ?? 0, dto.PeakNits ?? 0);
        }

        private static void ValidateHdrLuminance(float? value, float min, float max, string path, List<MxSettingsIssue> issues)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value) ||
                value.Value != 0 && (value.Value < min || value.Value > max)))
                issues.Add(new MxSettingsIssue(MxSettingsIssueCode.InvalidValue, path, "HDR luminance must be zero (automatic) or within the supported range."));
        }

        private static MxLocalizationSettings ToLocalizationSettings(LocalizationDto dto)
        {
            return dto == null
                ? MxLocalizationSettings.Default
                : new MxLocalizationSettings(dto.LocaleId);
        }

        private static LocalizationDto ToDto(MxLocalizationSettings settings)
        {
            MxLocalizationSettings localization = settings ?? MxLocalizationSettings.Default;
            return new LocalizationDto
            {
                LocaleId = localization.LocaleId
            };
        }

        private static void AddIssueIfInvalidEnum(
            int? value,
            string path,
            Func<int, bool> isDefined,
            List<MxSettingsIssue> issues)
        {
            if (value.HasValue && !isDefined(value.Value))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    path,
                    "Unsupported enum value: " + value.Value + "."));
            }
        }

        private static void AddIssueIfOutsideRange(
            int? value,
            int min,
            int max,
            string path,
            List<MxSettingsIssue> issues)
        {
            if (value.HasValue && (value.Value < min || value.Value > max))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    path,
                    "Value must be in range " + min + ".." + max + "."));
            }
        }

        private static void AddIssueIfOutsideRange(
            float? value,
            float min,
            float max,
            string path,
            List<MxSettingsIssue> issues)
        {
            if (value.HasValue && (value.Value < min || value.Value > max))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    path,
                    "Value must be in range " + min + ".." + max + "."));
            }
        }

        private static void AddIssueIfOutsidePositiveRange(
            int? value,
            int min,
            int max,
            string path,
            List<MxSettingsIssue> issues)
        {
            if (value.HasValue && value.Value > 0 && (value.Value < min || value.Value > max))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    path,
                    "Positive value must be in range " + min + ".." + max + "."));
            }
        }

        private static void AddIssueIfOutsidePositiveRange(
            float? value,
            float min,
            float max,
            string path,
            List<MxSettingsIssue> issues)
        {
            if (value.HasValue && value.Value > 0f && (value.Value < min || value.Value > max))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    path,
                    "Positive value must be in range " + min + ".." + max + "."));
            }
        }

        private static bool IsDefinedQualityPreset(int value)
        {
            return value >= (int)MxGraphicsQualityPreset.Custom &&
                value <= (int)MxGraphicsQualityPreset.Ultra;
        }

        private static bool IsDefinedWindowMode(int value)
        {
            return value >= (int)MxWindowMode.Windowed &&
                value <= (int)MxWindowMode.MaximizedWindow;
        }

        private static bool IsDefinedFeatureQuality(int value)
        {
            return value >= (int)MxGraphicsFeatureQuality.Default &&
                value <= (int)MxGraphicsFeatureQuality.High;
        }

        private static bool IsSupportedAntiAliasingSamples(int value)
        {
            return value == 1 || value == 2 || value == 4 || value == 8;
        }

        private static void AddIssueIfUnsupportedShadowmapResolution(
            int? value,
            string path,
            List<MxSettingsIssue> issues)
        {
            if (value.HasValue && value.Value != 0 &&
                (value.Value < 256 || value.Value > 8192 ||
                 (value.Value & (value.Value - 1)) != 0))
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidValue,
                    path,
                    "Shadowmap resolution must be 0 or a power of two from 256 to 8192."));
            }
        }

        private static MxGraphicsQualityPreset ToQualityPreset(int? value)
        {
            return IsDefinedQualityPreset(value ?? 0)
                ? (MxGraphicsQualityPreset)(value ?? 0)
                : MxGraphicsQualityPreset.Custom;
        }

        private static MxWindowMode ToWindowMode(int? value)
        {
            return IsDefinedWindowMode(value ?? 0)
                ? (MxWindowMode)(value ?? 0)
                : MxWindowMode.Windowed;
        }

        private static MxGraphicsFeatureQuality ToFeatureQuality(int? value)
        {
            return IsDefinedFeatureQuality(value ?? 0)
                ? (MxGraphicsFeatureQuality)(value ?? 0)
                : MxGraphicsFeatureQuality.Default;
        }
    }
}
