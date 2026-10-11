using MxFramework.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Settings.Graphics
{
    public sealed class MxUnityGraphicsRuntime : IMxGraphicsRuntime
    {
        private static UniversalRenderPipelineAsset s_runtimeAsset;
        private static RenderPipelineAsset s_originalPipelineOverride;

        public MxWindowMode CurrentWindowMode => FromFullScreenMode(Screen.fullScreenMode);

        public int QualityLevelCount
        {
            get
            {
                string[] names = QualitySettings.names;
                return names != null ? names.Length : 0;
            }
        }

        public void SetResolution(int width, int height, MxWindowMode windowMode)
        {
            Screen.SetResolution(width, height, ToFullScreenMode(windowMode));
        }

        public void SetWindowMode(MxWindowMode windowMode)
        {
            Screen.fullScreenMode = ToFullScreenMode(windowMode);
        }

        public void SetVSyncCount(int vSyncCount)
        {
            QualitySettings.vSyncCount = vSyncCount;
        }

        public void SetTargetFrameRate(int targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }

        public void SetQualityLevel(int qualityLevel, bool applyExpensiveChanges)
        {
            RestoreOriginalPipeline();
            QualitySettings.SetQualityLevel(qualityLevel, applyExpensiveChanges);
        }

        public bool TrySetRenderScale(float renderScale, out string message)
        {
            if (!TryGetUniversalRenderPipelineAsset(out UniversalRenderPipelineAsset asset, out message))
                return false;

            asset.renderScale = renderScale;
            message = string.Empty;
            return true;
        }

        public bool TrySetAntiAliasingSamples(int samples, out string message)
        {
            if (!TryGetUniversalRenderPipelineAsset(out UniversalRenderPipelineAsset asset, out message))
                return false;

            asset.msaaSampleCount = samples;
            message = string.Empty;
            return true;
        }

        public bool TrySetShadowDistance(float shadowDistance, out string message)
        {
            if (!TryGetUniversalRenderPipelineAsset(out UniversalRenderPipelineAsset asset, out message))
                return false;

            asset.shadowDistance = shadowDistance;
            message = string.Empty;
            return true;
        }

        public bool TrySetMainLightShadowmapResolution(int resolution, out string message)
        {
            if (!TryGetUniversalRenderPipelineAsset(out UniversalRenderPipelineAsset asset, out message))
                return false;

            asset.mainLightShadowmapResolution = resolution;
            message = string.Empty;
            return true;
        }

        public bool TrySetShadowCascadeCount(int count, out string message)
        {
            if (!TryGetUniversalRenderPipelineAsset(out UniversalRenderPipelineAsset asset, out message))
                return false;

            asset.shadowCascadeCount = count;
            message = string.Empty;
            return true;
        }

        public bool TrySetAdditionalLightsShadowmapResolution(int resolution, out string message)
        {
            if (!TryGetUniversalRenderPipelineAsset(out UniversalRenderPipelineAsset asset, out message))
                return false;

            asset.additionalLightsShadowmapResolution = resolution;
            message = string.Empty;
            return true;
        }

        private static bool TryGetUniversalRenderPipelineAsset(
            out UniversalRenderPipelineAsset asset,
            out string message)
        {
            if (!Application.isPlaying)
            {
                asset = null;
                message = "Graphics overrides require Play Mode.";
                return false;
            }

            if (s_runtimeAsset != null && QualitySettings.renderPipeline == s_runtimeAsset)
            {
                asset = s_runtimeAsset;
                message = string.Empty;
                return true;
            }

            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                message = "No active UniversalRenderPipelineAsset is configured.";
                return false;
            }

            UniversalRenderPipelineAsset runtimeAsset = UnityEngine.Object.Instantiate(asset);
            runtimeAsset.hideFlags = HideFlags.DontSave;
            s_originalPipelineOverride = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = runtimeAsset;
            s_runtimeAsset = runtimeAsset;
            Application.quitting -= RestoreOriginalPipeline;
            Application.quitting += RestoreOriginalPipeline;
            asset = runtimeAsset;
            message = string.Empty;
            return true;
        }

        private static void RestoreOriginalPipeline()
        {
            Application.quitting -= RestoreOriginalPipeline;
            if (s_runtimeAsset == null)
                return;

            if (QualitySettings.renderPipeline == s_runtimeAsset)
                QualitySettings.renderPipeline = s_originalPipelineOverride;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(s_runtimeAsset);
            else
                UnityEngine.Object.DestroyImmediate(s_runtimeAsset);
            s_runtimeAsset = null;
            s_originalPipelineOverride = null;
        }

        private static FullScreenMode ToFullScreenMode(MxWindowMode mode)
        {
            switch (mode)
            {
                case MxWindowMode.FullscreenWindow:
                    return FullScreenMode.FullScreenWindow;
                case MxWindowMode.ExclusiveFullscreen:
                    return FullScreenMode.ExclusiveFullScreen;
                case MxWindowMode.MaximizedWindow:
                    return FullScreenMode.MaximizedWindow;
                case MxWindowMode.Windowed:
                default:
                    return FullScreenMode.Windowed;
            }
        }

        private static MxWindowMode FromFullScreenMode(FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.FullScreenWindow:
                    return MxWindowMode.FullscreenWindow;
                case FullScreenMode.ExclusiveFullScreen:
                    return MxWindowMode.ExclusiveFullscreen;
                case FullScreenMode.MaximizedWindow:
                    return MxWindowMode.MaximizedWindow;
                case FullScreenMode.Windowed:
                default:
                    return MxWindowMode.Windowed;
            }
        }
    }
}
