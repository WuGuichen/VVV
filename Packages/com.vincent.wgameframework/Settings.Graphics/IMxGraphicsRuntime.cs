using MxFramework.Settings;

namespace MxFramework.Settings.Graphics
{
    public interface IMxGraphicsRuntime
    {
        MxWindowMode CurrentWindowMode { get; }
        int QualityLevelCount { get; }
        void SetResolution(int width, int height, MxWindowMode windowMode);
        void SetWindowMode(MxWindowMode windowMode);
        void SetVSyncCount(int vSyncCount);
        void SetTargetFrameRate(int targetFrameRate);
        void SetQualityLevel(int qualityLevel, bool applyExpensiveChanges);
        bool TrySetRenderScale(float renderScale, out string message);
        bool TrySetAntiAliasingSamples(int samples, out string message);
        bool TrySetShadowDistance(float shadowDistance, out string message);
        bool TrySetMainLightShadowmapResolution(int resolution, out string message);
        bool TrySetShadowCascadeCount(int count, out string message);
        bool TrySetAdditionalLightsShadowmapResolution(int resolution, out string message);
    }
}
