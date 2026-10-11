using System;
using System.Collections.Generic;

namespace MxFramework.Settings
{
    public enum MxWindowMode
    {
        Windowed = 0,
        FullscreenWindow = 1,
        ExclusiveFullscreen = 2,
        MaximizedWindow = 3
    }

    public enum MxGraphicsQualityPreset
    {
        Custom = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4
    }

    public enum MxGraphicsFeatureQuality
    {
        Default = 0,
        Disabled = 1,
        Low = 2,
        Medium = 3,
        High = 4
    }

    public enum MxGraphicsSettingExposure
    {
        Player = 0,
        Developer = 1,
        Internal = 2
    }

    public enum MxGraphicsSettingKey
    {
        Resolution = 0,
        WindowMode = 1,
        VSync = 2,
        TargetFrameRate = 3,
        QualityPreset = 4,
        RenderScale = 5,
        UnityQualityLevel = 6,
        AntiAliasingSamples = 7,
        ShadowDistance = 8,
        VolumetricFogQuality = 9,
        MainLightShadowmapResolution = 10,
        ShadowCascadeCount = 11,
        AdditionalLightsShadowmapResolution = 12,
        HdrOutput = 13,
        HdrPaperWhite = 14,
        HdrPeakBrightness = 15,
        DirectionalShadowQuality = 16
    }

    public readonly struct MxGraphicsSettingDescriptor
    {
        public MxGraphicsSettingDescriptor(
            MxGraphicsSettingKey key,
            MxGraphicsSettingExposure exposure,
            bool requiresRestart = false)
        {
            Key = key;
            Exposure = exposure;
            RequiresRestart = requiresRestart;
        }

        public MxGraphicsSettingKey Key { get; }
        public MxGraphicsSettingExposure Exposure { get; }
        public bool RequiresRestart { get; }
    }

    public static class MxGraphicsSettingsCatalog
    {
        private static readonly MxGraphicsSettingDescriptor[] s_descriptors =
        {
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.Resolution, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.WindowMode, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.VSync, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.TargetFrameRate, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.QualityPreset, MxGraphicsSettingExposure.Developer),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.RenderScale, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.UnityQualityLevel, MxGraphicsSettingExposure.Developer),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.AntiAliasingSamples, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.ShadowDistance, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.VolumetricFogQuality, MxGraphicsSettingExposure.Developer),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.MainLightShadowmapResolution, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.ShadowCascadeCount, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.AdditionalLightsShadowmapResolution, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.HdrOutput, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.HdrPaperWhite, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.HdrPeakBrightness, MxGraphicsSettingExposure.Player),
            new MxGraphicsSettingDescriptor(MxGraphicsSettingKey.DirectionalShadowQuality, MxGraphicsSettingExposure.Player)
        };

        private static readonly MxGraphicsSettingDescriptor[] s_playerVisibleDescriptors =
        {
            s_descriptors[0],
            s_descriptors[1],
            s_descriptors[2],
            s_descriptors[3],
            s_descriptors[5],
            s_descriptors[7],
            s_descriptors[8],
            s_descriptors[10],
            s_descriptors[11],
            s_descriptors[12],
            s_descriptors[13],
            s_descriptors[14],
            s_descriptors[15],
            s_descriptors[16]
        };

        public static IReadOnlyList<MxGraphicsSettingDescriptor> Descriptors => s_descriptors;
        public static IReadOnlyList<MxGraphicsSettingDescriptor> PlayerVisibleDescriptors => s_playerVisibleDescriptors;
    }

    public sealed class MxGraphicsSettings
    {
        public MxGraphicsSettings(
            MxGraphicsQualityPreset qualityPreset = MxGraphicsQualityPreset.Custom,
            int resolutionWidth = 0,
            int resolutionHeight = 0,
            bool applyWindowMode = false,
            MxWindowMode windowMode = MxWindowMode.Windowed,
            int vSyncCount = -1,
            int targetFrameRate = 0,
            float renderScale = 0f,
            int antiAliasingSamples = 0,
            float shadowDistance = -1f,
            MxGraphicsFeatureQuality volumetricFogQuality = MxGraphicsFeatureQuality.Default,
            int unityQualityLevel = -1,
            int mainLightShadowmapResolution = 0,
            int shadowCascadeCount = 0,
            int additionalLightsShadowmapResolution = 0,
            MxHdrOutputSettings hdrOutput = null,
            MxGraphicsFeatureQuality directionalShadowQuality = MxGraphicsFeatureQuality.Default)
        {
            QualityPreset = NormalizeQualityPreset(qualityPreset);
            if (resolutionWidth > 0 && resolutionHeight > 0)
            {
                ResolutionWidth = resolutionWidth;
                ResolutionHeight = resolutionHeight;
            }
            else
            {
                ResolutionWidth = 0;
                ResolutionHeight = 0;
            }

            ApplyWindowMode = applyWindowMode;
            WindowMode = NormalizeWindowMode(windowMode);
            VSyncCount = Clamp(vSyncCount, -1, 2);
            TargetFrameRate = targetFrameRate <= 0 ? 0 : Clamp(targetFrameRate, 15, 360);
            RenderScale = renderScale <= 0f ? 0f : Clamp(renderScale, 0.5f, 2f);
            AntiAliasingSamples = NormalizeAntiAliasingSamples(antiAliasingSamples);
            ShadowDistance = shadowDistance < 0f ? -1f : Clamp(shadowDistance, 0f, 500f);
            VolumetricFogQuality = NormalizeFeatureQuality(volumetricFogQuality);
            UnityQualityLevel = unityQualityLevel < 0 ? -1 : unityQualityLevel;
            MainLightShadowmapResolution = NormalizeShadowmapResolution(mainLightShadowmapResolution);
            ShadowCascadeCount = shadowCascadeCount >= 1 && shadowCascadeCount <= 4
                ? shadowCascadeCount
                : 0;
            AdditionalLightsShadowmapResolution = NormalizeShadowmapResolution(additionalLightsShadowmapResolution);
            HdrOutput = hdrOutput ?? MxHdrOutputSettings.Default;
            DirectionalShadowQuality = directionalShadowQuality == MxGraphicsFeatureQuality.Low ||
                directionalShadowQuality == MxGraphicsFeatureQuality.Medium || directionalShadowQuality == MxGraphicsFeatureQuality.High
                ? directionalShadowQuality : MxGraphicsFeatureQuality.Default;
        }

        public MxGraphicsQualityPreset QualityPreset { get; }
        public int ResolutionWidth { get; }
        public int ResolutionHeight { get; }
        public bool ApplyWindowMode { get; }
        public MxWindowMode WindowMode { get; }
        public int VSyncCount { get; }
        public int TargetFrameRate { get; }
        public float RenderScale { get; }
        public int AntiAliasingSamples { get; }
        public float ShadowDistance { get; }
        public MxGraphicsFeatureQuality VolumetricFogQuality { get; }
        public int UnityQualityLevel { get; }
        public int MainLightShadowmapResolution { get; }
        public int ShadowCascadeCount { get; }
        public int AdditionalLightsShadowmapResolution { get; }
        public MxHdrOutputSettings HdrOutput { get; }
        /// <summary>Fixed directional backend dynamic-field quality. Default preserves scene authoring; no runtime bake.</summary>
        public MxGraphicsFeatureQuality DirectionalShadowQuality { get; }

        public static MxGraphicsSettings Default { get; } = new MxGraphicsSettings();

        private static int NormalizeShadowmapResolution(int value)
        {
            return value >= 256 && value <= 8192 && (value & (value - 1)) == 0
                ? value
                : 0;
        }

        private static MxGraphicsQualityPreset NormalizeQualityPreset(MxGraphicsQualityPreset value)
        {
            switch (value)
            {
                case MxGraphicsQualityPreset.Low:
                case MxGraphicsQualityPreset.Medium:
                case MxGraphicsQualityPreset.High:
                case MxGraphicsQualityPreset.Ultra:
                    return value;
                case MxGraphicsQualityPreset.Custom:
                default:
                    return MxGraphicsQualityPreset.Custom;
            }
        }

        private static MxWindowMode NormalizeWindowMode(MxWindowMode value)
        {
            switch (value)
            {
                case MxWindowMode.FullscreenWindow:
                case MxWindowMode.ExclusiveFullscreen:
                case MxWindowMode.MaximizedWindow:
                    return value;
                case MxWindowMode.Windowed:
                default:
                    return MxWindowMode.Windowed;
            }
        }

        private static MxGraphicsFeatureQuality NormalizeFeatureQuality(MxGraphicsFeatureQuality value)
        {
            switch (value)
            {
                case MxGraphicsFeatureQuality.Disabled:
                case MxGraphicsFeatureQuality.Low:
                case MxGraphicsFeatureQuality.Medium:
                case MxGraphicsFeatureQuality.High:
                    return value;
                case MxGraphicsFeatureQuality.Default:
                default:
                    return MxGraphicsFeatureQuality.Default;
            }
        }

        private static int NormalizeAntiAliasingSamples(int value)
        {
            if (value <= 0)
                return 0;
            if (value <= 1)
                return 1;
            if (value <= 2)
                return 2;
            if (value <= 4)
                return 4;
            return 8;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
