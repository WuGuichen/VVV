using System;
using MxFramework.Rendering;

namespace MxFramework.Settings.Graphics
{
    /// <summary>Applies the existing settings profile to one scene's directional backend without rebaking static data.</summary>
    public sealed class MxDirectionalShadowSettingsApplier : IMxSettingsApplier, IDisposable
    {
        private readonly Func<MxSceneRenderingConfiguration> _configuration;
        private MxSceneRenderingConfiguration _bound;
        private int _previousOverride;
        private bool _disposed;
        public string ApplierId => "settings.graphics.directionalShadow";
        public MxDirectionalShadowSettingsApplier(Func<MxSceneRenderingConfiguration> configuration)
        { _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); }
        public static int ResolutionOverride(MxGraphicsFeatureQuality quality)
        {
            switch (quality)
            {
                case MxGraphicsFeatureQuality.Low: return 256;
                case MxGraphicsFeatureQuality.Medium: return 512;
                case MxGraphicsFeatureQuality.High: return 1024;
                default: return 0;
            }
        }
        public MxSettingsApplyResult Apply(MxSettingsProfile profile)
        {
            if (_disposed) return MxSettingsApplyResult.Failed(new MxSettingsIssue(
                MxSettingsIssueCode.AdapterUnavailable, "$.graphics.directionalShadowQuality", "Shadow settings session has ended."));
            var configuration = _configuration();
            int resolution = ResolutionOverride(profile?.Graphics?.DirectionalShadowQuality ?? MxGraphicsFeatureQuality.Default);
            if (configuration == null)
                return resolution == 0 ? MxSettingsApplyResult.Succeeded() : MxSettingsApplyResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.AdapterUnavailable, "$.graphics.directionalShadowQuality", "Directional shadow configuration is unavailable."));
            if (!ReferenceEquals(_bound, configuration))
            {
                Restore(); _bound = configuration; _previousOverride = configuration.DynamicShadowResolutionOverride;
            }
            configuration.DynamicShadowResolutionOverride = resolution;
            return MxSettingsApplyResult.Succeeded();
        }
        private void Restore()
        {
            if (_bound != null) _bound.DynamicShadowResolutionOverride = _previousOverride;
            _bound = null;
        }
        public void Dispose() { if (_disposed) return; Restore(); _disposed = true; }
    }
}
