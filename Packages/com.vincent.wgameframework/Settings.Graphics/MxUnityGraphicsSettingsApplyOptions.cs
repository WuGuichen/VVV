namespace MxFramework.Settings.Graphics
{
    public sealed class MxUnityGraphicsSettingsApplyOptions
    {
        public static MxUnityGraphicsSettingsApplyOptions Default { get; } =
            new MxUnityGraphicsSettingsApplyOptions();

        public MxUnityGraphicsSettingsApplyOptions(
            bool applyResolution = true,
            bool applyWindowMode = true,
            bool applyVSync = true,
            bool applyTargetFrameRate = true,
            bool applyQualityLevel = true,
            bool applyRenderScale = true,
            bool applyAntiAliasingSamples = true,
            bool applyShadowDistance = true,
            bool applyMainLightShadowmapResolution = true,
            bool applyShadowCascadeCount = true,
            bool applyAdditionalLightsShadowmapResolution = true)
        {
            ApplyResolution = applyResolution;
            ApplyWindowMode = applyWindowMode;
            ApplyVSync = applyVSync;
            ApplyTargetFrameRate = applyTargetFrameRate;
            ApplyQualityLevel = applyQualityLevel;
            ApplyRenderScale = applyRenderScale;
            ApplyAntiAliasingSamples = applyAntiAliasingSamples;
            ApplyShadowDistance = applyShadowDistance;
            ApplyMainLightShadowmapResolution = applyMainLightShadowmapResolution;
            ApplyShadowCascadeCount = applyShadowCascadeCount;
            ApplyAdditionalLightsShadowmapResolution = applyAdditionalLightsShadowmapResolution;
        }

        public bool ApplyResolution { get; }
        public bool ApplyWindowMode { get; }
        public bool ApplyVSync { get; }
        public bool ApplyTargetFrameRate { get; }
        public bool ApplyQualityLevel { get; }
        public bool ApplyRenderScale { get; }
        public bool ApplyAntiAliasingSamples { get; }
        public bool ApplyShadowDistance { get; }
        public bool ApplyMainLightShadowmapResolution { get; }
        public bool ApplyShadowCascadeCount { get; }
        public bool ApplyAdditionalLightsShadowmapResolution { get; }
    }
}
