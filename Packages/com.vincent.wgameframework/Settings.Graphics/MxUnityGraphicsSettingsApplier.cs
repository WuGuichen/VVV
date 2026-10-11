using System;
using System.Collections.Generic;
using MxFramework.Settings;

namespace MxFramework.Settings.Graphics
{
    public sealed class MxUnityGraphicsSettingsApplier : IMxSettingsApplier
    {
        private readonly IMxGraphicsRuntime _runtime;
        private readonly MxUnityGraphicsSettingsApplyOptions _options;

        public MxUnityGraphicsSettingsApplier(
            IMxGraphicsRuntime runtime = null,
            MxUnityGraphicsSettingsApplyOptions options = null,
            string applierId = "settings.graphics")
        {
            _runtime = runtime ?? new MxUnityGraphicsRuntime();
            _options = options ?? MxUnityGraphicsSettingsApplyOptions.Default;
            ApplierId = string.IsNullOrWhiteSpace(applierId) ? "settings.graphics" : applierId;
        }

        public string ApplierId { get; }
        public MxUnityGraphicsSettingsApplyOptions Options => _options;

        public MxSettingsApplyResult Apply(MxSettingsProfile profile)
        {
            MxGraphicsSettings graphics = profile?.Graphics ?? MxGraphicsSettings.Default;
            var issues = new List<MxSettingsIssue>();

            if (_options.ApplyQualityLevel)
                ApplyQualityLevel(graphics, issues);
            ApplyResolutionAndWindowMode(graphics, issues);
            if (_options.ApplyVSync)
                ApplyVSync(graphics, issues);
            if (_options.ApplyTargetFrameRate)
                ApplyTargetFrameRate(graphics, issues);
            if (_options.ApplyRenderScale)
                ApplyRenderScale(graphics, issues);
            if (_options.ApplyAntiAliasingSamples)
                ApplyAntiAliasingSamples(graphics, issues);
            if (_options.ApplyShadowDistance)
                ApplyShadowDistance(graphics, issues);
            if (_options.ApplyMainLightShadowmapResolution)
                ApplyMainLightShadowmapResolution(graphics, issues);
            if (_options.ApplyShadowCascadeCount)
                ApplyShadowCascadeCount(graphics, issues);
            if (_options.ApplyAdditionalLightsShadowmapResolution)
                ApplyAdditionalLightsShadowmapResolution(graphics, issues);

            return issues.Count == 0
                ? MxSettingsApplyResult.Succeeded()
                : MxSettingsApplyResult.Failed(issues);
        }

        private void ApplyQualityLevel(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            int qualityLevel;
            string path;
            if (!TryResolveQualityLevel(graphics, out qualityLevel, out path))
                return;

            int qualityLevelCount = _runtime.QualityLevelCount;
            if (qualityLevelCount <= 0 || qualityLevel >= qualityLevelCount)
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidTarget,
                    path,
                    "Unity quality level " + qualityLevel +
                    " is outside available range 0.." + Math.Max(0, qualityLevelCount - 1) + "."));
                return;
            }

            TryApply(
                path,
                issues,
                () => _runtime.SetQualityLevel(qualityLevel, true));
        }

        private bool TryResolveQualityLevel(
            MxGraphicsSettings graphics,
            out int qualityLevel,
            out string path)
        {
            if (graphics.UnityQualityLevel >= 0)
            {
                qualityLevel = graphics.UnityQualityLevel;
                path = "$.graphics.unityQualityLevel";
                return true;
            }

            path = "$.graphics.qualityPreset";
            int qualityLevelCount = _runtime.QualityLevelCount;
            int maxQualityLevel = Math.Max(0, qualityLevelCount - 1);
            switch (graphics.QualityPreset)
            {
                case MxGraphicsQualityPreset.Low:
                    qualityLevel = 0;
                    return true;
                case MxGraphicsQualityPreset.Medium:
                    qualityLevel = (maxQualityLevel + 1) / 3;
                    return true;
                case MxGraphicsQualityPreset.High:
                    qualityLevel = (2 * maxQualityLevel + 2) / 3;
                    return true;
                case MxGraphicsQualityPreset.Ultra:
                    qualityLevel = maxQualityLevel;
                    return true;
                case MxGraphicsQualityPreset.Custom:
                default:
                    qualityLevel = -1;
                    return false;
            }
        }

        private void ApplyResolutionAndWindowMode(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.ResolutionWidth > 0 && graphics.ResolutionHeight > 0)
            {
                if (_options.ApplyResolution)
                {
                    MxWindowMode mode = graphics.ApplyWindowMode && _options.ApplyWindowMode
                        ? graphics.WindowMode
                        : _runtime.CurrentWindowMode;
                    TryApply(
                        "$.graphics.resolution",
                        issues,
                        () => _runtime.SetResolution(graphics.ResolutionWidth, graphics.ResolutionHeight, mode));
                    return;
                }

                if (graphics.ApplyWindowMode && _options.ApplyWindowMode)
                {
                    TryApply(
                        "$.graphics.windowMode",
                        issues,
                        () => _runtime.SetWindowMode(graphics.WindowMode));
                }

                return;
            }

            if (graphics.ApplyWindowMode && _options.ApplyWindowMode)
            {
                TryApply(
                    "$.graphics.windowMode",
                    issues,
                    () => _runtime.SetWindowMode(graphics.WindowMode));
            }
        }

        private void ApplyVSync(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.VSyncCount < 0)
                return;

            TryApply(
                "$.graphics.vSyncCount",
                issues,
                () => _runtime.SetVSyncCount(graphics.VSyncCount));
        }

        private void ApplyTargetFrameRate(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.TargetFrameRate <= 0)
                return;

            TryApply(
                "$.graphics.targetFrameRate",
                issues,
                () => _runtime.SetTargetFrameRate(graphics.TargetFrameRate));
        }

        private void ApplyRenderScale(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.RenderScale <= 0f)
                return;

            TryApplySupportedSetting(
                "$.graphics.renderScale",
                graphics.RenderScale,
                issues,
                _runtime.TrySetRenderScale);
        }

        private void ApplyAntiAliasingSamples(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.AntiAliasingSamples <= 0)
                return;

            TryApplySupportedSetting(
                "$.graphics.antiAliasingSamples",
                graphics.AntiAliasingSamples,
                issues,
                _runtime.TrySetAntiAliasingSamples);
        }

        private void ApplyShadowDistance(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.ShadowDistance < 0f)
                return;

            TryApplySupportedSetting(
                "$.graphics.shadowDistance",
                graphics.ShadowDistance,
                issues,
                _runtime.TrySetShadowDistance);
        }

        private void ApplyMainLightShadowmapResolution(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.MainLightShadowmapResolution <= 0)
                return;

            TryApplySupportedSetting(
                "$.graphics.mainLightShadowmapResolution",
                graphics.MainLightShadowmapResolution,
                issues,
                _runtime.TrySetMainLightShadowmapResolution);
        }

        private void ApplyShadowCascadeCount(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.ShadowCascadeCount <= 0)
                return;

            TryApplySupportedSetting(
                "$.graphics.shadowCascadeCount",
                graphics.ShadowCascadeCount,
                issues,
                _runtime.TrySetShadowCascadeCount);
        }

        private void ApplyAdditionalLightsShadowmapResolution(MxGraphicsSettings graphics, List<MxSettingsIssue> issues)
        {
            if (graphics.AdditionalLightsShadowmapResolution <= 0)
                return;

            TryApplySupportedSetting(
                "$.graphics.additionalLightsShadowmapResolution",
                graphics.AdditionalLightsShadowmapResolution,
                issues,
                _runtime.TrySetAdditionalLightsShadowmapResolution);
        }

        private void TryApplySupportedSetting<T>(
            string path,
            T value,
            List<MxSettingsIssue> issues,
            TrySetSetting<T> trySet)
        {
            try
            {
                string message;
                if (!trySet(value, out message))
                {
                    issues.Add(new MxSettingsIssue(
                        MxSettingsIssueCode.UnsupportedSetting,
                        path,
                        string.IsNullOrWhiteSpace(message) ? "Graphics setting is not supported." : message));
                }
            }
            catch (Exception ex)
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.ApplyFailed,
                    path,
                    "Failed to apply graphics setting: " + ex.Message,
                    ex));
            }
        }

        private static void TryApply(
            string path,
            List<MxSettingsIssue> issues,
            Action apply)
        {
            try
            {
                apply();
            }
            catch (Exception ex)
            {
                issues.Add(new MxSettingsIssue(
                    MxSettingsIssueCode.ApplyFailed,
                    path,
                    "Failed to apply graphics setting: " + ex.Message,
                    ex));
            }
        }

        private delegate bool TrySetSetting<T>(T value, out string message);
    }
}
