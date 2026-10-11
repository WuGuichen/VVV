using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class VolumetricFogTests
    {
        [Test]
        public void HeightFogSceneProvider_TargetsOneCameraAndOrdersMaterialOverrideBeforeComposite()
        {
            var firstObject = new GameObject("Height fog camera A");
            var secondObject = new GameObject("Height fog camera B");
            MxHeightFogSceneProvider provider = null;
            try
            {
                var first = firstObject.AddComponent<UnityEngine.Camera>(); first.enabled = false;
                var second = secondObject.AddComponent<UnityEngine.Camera>(); second.enabled = false;
                var shader = Shader.Find("Hidden/MxFramework/Rendering/VolumetricFogComposite");
                Assert.NotNull(shader);
                var settings = MxVolumetricFogSettings.Default();
                provider = new MxHeightFogSceneProvider(settings, first, shader);
                var pipeline = new MxRenderPipeline(); pipeline.RegisterProvider(provider);
                var a = new MxCameraRenderContextDescriptor(MxCameraRenderKind.Game, first, Vector3.zero);
                var b = new MxCameraRenderContextDescriptor(MxCameraRenderKind.Game, second, Vector3.zero);
                var passes = pipeline.CollectPasses(a);
                Assert.AreEqual(2, passes.Count);
                Assert.AreEqual(MxRenderPhase.BeforeRenderingOpaques, passes[0].Phase);
                Assert.AreEqual(MxRenderPhase.BeforeRenderingPostProcessing, passes[1].Phase);
                Assert.AreEqual(0, pipeline.CollectPasses(b).Count);
                Assert.AreEqual(0, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.SceneView)).Count);
                provider.SetSettings(settings.WithSceneViewPreview(true));
                Assert.AreEqual(2, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.SceneView)).Count);
                provider.SetCamera(second);
                Assert.AreEqual(0, pipeline.CollectPasses(a).Count);
                Assert.AreEqual(2, pipeline.CollectPasses(b).Count);
                provider.SetCamera(null);
                Assert.AreEqual(0, pipeline.CollectPasses(b).Count);
                provider.SetCamera(second); provider.SetSettings(settings.WithEnabled(false));
                Assert.AreEqual(0, pipeline.CollectPasses(b).Count, "Disabled fog must not suppress material fog.");
                provider.SetSettings(settings); provider.Dispose();
                Assert.AreEqual(0, pipeline.CollectPasses(b).Count);
            }
            finally
            {
                provider?.Dispose();
                UnityEngine.Object.DestroyImmediate(firstObject); UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void VolumetricFogSettings_QualityPresetsClampRuntimeCost()
        {
            MxVolumetricFogSettings low = MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.Low);
            MxVolumetricFogSettings high = MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.High);
            var clamped = new MxVolumetricFogSettings(
                true,
                MxVolumetricFogQuality.High,
                Color.white,
                -1f,
                0f,
                20f,
                -5f,
                4f,
                20f,
                2f,
                -10f,
                999,
                99,
                2f);

            Assert.AreEqual(MxVolumetricFogQuality.Low, low.Quality);
            Assert.AreEqual(4, low.DiagnosticDownsample);
            Assert.AreEqual(6, low.QualitySampleBudget);
            Assert.IsFalse(low.HasTemporalStabilityIntent);
            Assert.Greater(low.FocusClearRadius, 0f);
            Assert.Greater(low.FocusClearSoftness, 0f);

            Assert.AreEqual(MxVolumetricFogQuality.High, high.Quality);
            Assert.AreEqual(2, high.DiagnosticDownsample);
            Assert.AreEqual(20, high.QualitySampleBudget);
            Assert.IsTrue(high.HasTemporalStabilityIntent);

            Assert.AreEqual(0f, clamped.Density);
            Assert.AreEqual(8f, clamped.HeightFalloff);
            Assert.AreEqual(1f, clamped.MaxDistance);
            Assert.AreEqual(0.95f, clamped.Anisotropy);
            Assert.AreEqual(8f, clamped.ScatteringIntensity);
            Assert.AreEqual(1f, clamped.NoiseStrength);
            Assert.AreEqual(0.001f, clamped.NoiseScale);
            Assert.AreEqual(64, clamped.QualitySampleBudget);
            Assert.AreEqual(4, clamped.DiagnosticDownsample);
            Assert.AreEqual(0.98f, clamped.TemporalStabilityIntent);
            Assert.AreEqual(2.5f, clamped.FocusClearRadius);
            Assert.AreEqual(3f, clamped.FocusClearSoftness);
            Assert.IsFalse(clamped.PreviewInSceneView);
        }

        [Test]
        public void VolumetricFogProvider_CollectsOnlyGameCameraPasses()
        {
            var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.Default());
            var pipeline = new MxRenderPipeline();
            pipeline.RegisterProvider(provider);

            Assert.AreEqual(1, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.Game)).Count);
            Assert.AreEqual(0, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.SceneView)).Count);

            VolumetricFogDiagnosticsSnapshot diagnostics = provider.CaptureDiagnostics();
            Assert.AreEqual(MxCameraRenderKind.SceneView, diagnostics.LastCameraKind);
            Assert.AreEqual("camera kind not targeted", diagnostics.LastSkipReason);
            Assert.IsFalse(diagnostics.PreviewInSceneView);
        }

        [Test]
        public void VolumetricFogProvider_CollectsSceneViewPassWhenPreviewEnabled()
        {
            var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.Default().WithSceneViewPreview(true));
            var pipeline = new MxRenderPipeline();
            pipeline.RegisterProvider(provider);

            Assert.AreEqual(1, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.Game)).Count);
            Assert.AreEqual(1, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.SceneView)).Count);
            Assert.AreEqual(0, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.Preview)).Count);

            VolumetricFogDiagnosticsSnapshot diagnostics = provider.CaptureDiagnostics();
            Assert.AreEqual(MxCameraRenderKind.Preview, diagnostics.LastCameraKind);
            Assert.AreEqual("camera kind not targeted", diagnostics.LastSkipReason);
            Assert.IsTrue(diagnostics.PreviewInSceneView);
        }

        [Test]
        public void VolumetricFogPass_ReportsDiagnosticBudgetWithoutAllocatingSharedRt()
        {
            var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.High));
            var pipeline = new MxRenderPipeline();
            using var registry = new SharedRenderTextureRegistry();
            pipeline.RegisterProvider(provider);

            registry.BeginFrame();
            var descriptor = Descriptor(MxCameraRenderKind.Game);
            var snapshot = new CameraRenderSnapshot(MxCameraRenderKind.Game, null, Vector3.forward, System.Array.Empty<CameraShaderOverride>());
            foreach (IMxRenderPass pass in pipeline.CollectPasses(descriptor))
            {
                Assert.IsInstanceOf<IMxFullscreenRenderPass>(pass);
                Assert.IsEmpty(pass.Reads);
                Assert.IsEmpty(pass.Writes);
                pass.Configure(new MxRenderPassConfigureContext(descriptor, snapshot, registry));
            }

            SharedRTDiagnosticsSnapshot sharedRt = registry.CaptureDiagnostics();
            AssertSharedRtEntryMissing(sharedRt, MxVolumetricFogPass.AccumulationId);
            AssertSharedRtEntryMissing(sharedRt, MxVolumetricFogPass.ReservedTemporalHistoryId);
            Assert.IsEmpty(sharedRt.Entries);
            Assert.AreEqual(0, sharedRt.ActualMemoryBytes);

            VolumetricFogDiagnosticsSnapshot fog = provider.CaptureDiagnostics();
            Assert.IsTrue(fog.Enabled);
            Assert.AreEqual(MxVolumetricFogQuality.High, fog.Quality);
            Assert.IsTrue(fog.TemporalStabilityPlanned);
            Assert.AreEqual(20, fog.QualitySampleBudget);
            Assert.AreEqual(new SharedRTSize(640, 360), fog.AccumulationSize);
            Assert.AreEqual(640L * 360 * 8, fog.EstimatedMemoryBytes);
        }

        [Test]
        public void VolumetricFogProvider_DisposeIsTerminalAndIdempotent()
        {
            var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.Default());
            var pipeline = new MxRenderPipeline();
            pipeline.RegisterProvider(provider);

            Assert.AreEqual(1, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.Game)).Count);

            provider.Dispose();
            provider.Dispose();

            Assert.AreEqual(0, pipeline.CollectPasses(Descriptor(MxCameraRenderKind.Game)).Count);
            VolumetricFogDiagnosticsSnapshot diagnostics = provider.CaptureDiagnostics();
            Assert.IsFalse(diagnostics.Enabled);
            Assert.AreEqual(MxVolumetricFogQuality.Disabled, diagnostics.Quality);
            Assert.IsFalse(diagnostics.MaterialAvailable);
            Assert.AreEqual("disposed", diagnostics.LastSkipReason);
        }

        [Test]
        public void VolumetricFogProvider_DisposedDiagnosticsDoNotRecreateMaterial()
        {
            var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.Default());
            provider.Dispose();

            VolumetricFogDiagnosticsSnapshot first = provider.CaptureDiagnostics();
            VolumetricFogDiagnosticsSnapshot second = provider.CaptureDiagnostics();
            FrameworkDebugSnapshot debugSnapshot = new VolumetricFogDebugSource(provider).CreateSnapshot();

            Assert.IsFalse(first.MaterialAvailable);
            Assert.IsFalse(second.MaterialAvailable);
            StringAssert.Contains("materialAvailable: False", debugSnapshot.Sections[0].Body);
        }

        [Test]
        public void VolumetricFogDebugSource_ExposesDiagnosticsSection()
        {
            var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.Low));
            var source = new VolumetricFogDebugSource(provider);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("Rendering", snapshot.SourceName);
            Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
            Assert.AreEqual(RenderingDebugSectionNames.VolumetricFog, snapshot.Sections[0].Title);
            StringAssert.Contains("enabled: True", snapshot.Sections[0].Body);
            StringAssert.Contains("quality: Low", snapshot.Sections[0].Body);
            StringAssert.Contains("diagnosticDownsample: 4", snapshot.Sections[0].Body);
            StringAssert.Contains("qualitySampleBudget: 6", snapshot.Sections[0].Body);
            StringAssert.Contains("focusClearRadius:", snapshot.Sections[0].Body);
            StringAssert.Contains("previewInSceneView: False", snapshot.Sections[0].Body);
        }

        private static MxCameraRenderContextDescriptor Descriptor(MxCameraRenderKind kind)
        {
            return new MxCameraRenderContextDescriptor(kind, null, Vector3.forward);
        }

        [TestCase(MxVolumetricFogQuality.Low, 320, 180)]
        [TestCase(MxVolumetricFogQuality.Medium, 640, 360)]
        [TestCase(MxVolumetricFogQuality.High, 640, 360)]
        public void VolumetricFogPass_RepeatedConfigureAndQualityChangesKeepRegistryEmpty(MxVolumetricFogQuality quality, int width, int height)
        {
            using var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.ForQuality(quality));
            using var registry = new SharedRenderTextureRegistry();
            var pipeline = new MxRenderPipeline(); pipeline.RegisterProvider(provider);
            var descriptor = Descriptor(MxCameraRenderKind.Game);
            var snapshot = new CameraRenderSnapshot(MxCameraRenderKind.Game, null, Vector3.forward, System.Array.Empty<CameraShaderOverride>());
            for (int i = 0; i < 4; i++)
            {
                registry.BeginFrame();
                foreach (var pass in pipeline.CollectPasses(descriptor))
                {
                    Assert.IsEmpty(pass.Reads); Assert.IsEmpty(pass.Writes);
                    // No registry is needed for the budget-only Configure path.
                    pass.Configure(new MxRenderPassConfigureContext(descriptor, snapshot, null));
                }
                registry.EndFrame();
                Assert.IsEmpty(registry.CaptureDiagnostics().Entries);
                Assert.AreEqual(new SharedRTSize(width, height), provider.CaptureDiagnostics().AccumulationSize);
            }
            provider.SetSettings(MxVolumetricFogSettings.ForQuality(MxVolumetricFogQuality.Low));
            foreach (var pass in pipeline.CollectPasses(descriptor)) pass.Configure(new MxRenderPassConfigureContext(descriptor, snapshot, registry));
            Assert.AreEqual(new SharedRTSize(320, 180), provider.CaptureDiagnostics().AccumulationSize);
            Assert.IsEmpty(registry.CaptureDiagnostics().Entries);
        }

        private static void AssertSharedRtEntryMissing(SharedRTDiagnosticsSnapshot snapshot, SharedRTId id)
        {
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                if (snapshot.Entries[i].Id == id)
                    Assert.Fail("Budget-only or reserved fog resources must not be registered: " + id);
            }
        }
    }
}
