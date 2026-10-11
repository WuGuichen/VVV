using System;
using MxFramework.Rendering;
using MxFramework.Settings;

namespace MxFramework.Settings.Graphics
{
    public sealed class MxVolumetricFogSettingsApplier : IMxSettingsApplier
    {
        private readonly MxVolumetricFogPassProvider _provider;

        public MxVolumetricFogSettingsApplier(
            MxVolumetricFogPassProvider provider,
            string applierId = "settings.graphics.volumetricFog")
        {
            _provider = provider;
            ApplierId = string.IsNullOrWhiteSpace(applierId)
                ? "settings.graphics.volumetricFog"
                : applierId;
        }

        public string ApplierId { get; }

        public MxSettingsApplyResult Apply(MxSettingsProfile profile)
        {
            MxGraphicsFeatureQuality quality =
                profile?.Graphics?.VolumetricFogQuality ?? MxGraphicsFeatureQuality.Default;
            if (quality == MxGraphicsFeatureQuality.Default)
                return MxSettingsApplyResult.Succeeded();

            if (_provider == null)
            {
                return MxSettingsApplyResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.AdapterUnavailable,
                    "$.graphics.volumetricFogQuality",
                    "Volumetric fog provider is not configured."));
            }

            try
            {
                _provider.SetSettings(ToRenderingSettings(quality));
                return MxSettingsApplyResult.Succeeded();
            }
            catch (Exception ex)
            {
                return MxSettingsApplyResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.ApplyFailed,
                    "$.graphics.volumetricFogQuality",
                    "Failed to apply volumetric fog setting: " + ex.Message,
                    ex));
            }
        }

        private static MxVolumetricFogSettings ToRenderingSettings(MxGraphicsFeatureQuality quality)
        {
            switch (quality)
            {
                case MxGraphicsFeatureQuality.Disabled:
                    return MxVolumetricFogSettings.Disabled();
                case MxGraphicsFeatureQuality.Low:
                    return MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.Low);
                case MxGraphicsFeatureQuality.High:
                    return MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.High);
                case MxGraphicsFeatureQuality.Medium:
                default:
                    return MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.Medium);
            }
        }
    }
}
