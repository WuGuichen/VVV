using System;
using System.IO;
using System.Linq;
using MxFramework.Rendering;
using MxFramework.Settings;
using MxFramework.Settings.Graphics;
using MxFramework.Settings.UI;
using NUnit.Framework;

namespace MxFramework.Tests.Settings
{
    public sealed class DirectionalShadowQualitySettingsTests
    {
        private static MxSettingsProfile Profile(MxGraphicsFeatureQuality quality) =>
            new MxSettingsProfile(graphics: new MxGraphicsSettings(directionalShadowQuality: quality));

        [Test]
        public void LegacyProfileKeepsSceneDefaultAndOtherGraphicsValues()
        {
            var loaded = MxSettingsProfileJson.LoadFromJson(
                "{\"format\":\"mx.settings.v1\",\"profileId\":\"default\",\"graphics\":{\"renderScale\":0.75,\"mainLightShadowmapResolution\":4096}}");
            Assert.IsFalse(loaded.HasIssues, string.Join("; ", loaded.Issues.Select(issue => issue.Path + ": " + issue.Message)));
            Assert.AreEqual(MxGraphicsFeatureQuality.Default, loaded.Profile.Graphics.DirectionalShadowQuality);
            Assert.AreEqual(.75f, loaded.Profile.Graphics.RenderScale);
            Assert.AreEqual(4096, loaded.Profile.Graphics.MainLightShadowmapResolution);
            var scene = new MxSceneRenderingConfiguration { DynamicShadowResolution = 2048 };
            using (var applier = new MxDirectionalShadowSettingsApplier(() => scene))
            {
                Assert.IsTrue(applier.Apply(loaded.Profile).Success);
                Assert.AreEqual(2048, scene.EffectiveDynamicShadowResolution);
            }
        }

        [Test]
        public void SupportedValuesRoundTripAndUnsupportedOffReportsIssue()
        {
            foreach (var quality in new[] { MxGraphicsFeatureQuality.Default, MxGraphicsFeatureQuality.Low,
                MxGraphicsFeatureQuality.Medium, MxGraphicsFeatureQuality.High })
            {
                var loaded = MxSettingsProfileJson.LoadFromJson(MxSettingsProfileJson.SaveToJson(Profile(quality)));
                Assert.IsFalse(loaded.HasIssues);
                Assert.AreEqual(quality, loaded.Profile.Graphics.DirectionalShadowQuality);
            }
            foreach (int value in new[] { -1, 1, 5, 99 })
            {
                var loaded = MxSettingsProfileJson.LoadFromJson(
                    "{\"format\":\"mx.settings.v1\",\"profileId\":\"default\",\"graphics\":{\"directionalShadowQuality\":" + value + "}}");
                Assert.IsTrue(loaded.Issues.Any(issue => issue.Path == "$.graphics.directionalShadowQuality"),
                    string.Join("; ", loaded.Issues.Select(issue => issue.Path + ": " + issue.Message)));
                Assert.AreEqual(MxGraphicsFeatureQuality.Default, loaded.Profile.Graphics.DirectionalShadowQuality);
            }
        }

        [Test]
        public void EditingOtherOptionsPreservesShadowQuality()
        {
            var presenter = new MxSettingsUiPresenter(new MxSettingsService(null, Profile(MxGraphicsFeatureQuality.Medium)));
            Action[] edits = {
                () => presenter.SetGraphicsQualityPreset(MxGraphicsQualityPreset.High),
                () => presenter.SetResolution(1280, 720),
                () => presenter.SetWindowMode(MxWindowMode.FullscreenWindow),
                () => presenter.SetVSyncCount(1), () => presenter.SetTargetFrameRate(60),
                () => presenter.SetRenderScale(.8f), () => presenter.SetHdrOutput(MxHdrOutputSettings.Default),
                () => presenter.SetAntiAliasingSamples(4), () => presenter.SetShadowDistance(80),
                () => presenter.SetMainLightShadowmapResolution(4096), () => presenter.SetShadowCascadeCount(2),
                () => presenter.SetAdditionalLightsShadowmapResolution(2048),
                () => presenter.SetAudioBusVolume(0, .5f), () => presenter.SetAudioBusMuted(0, true),
                () => presenter.SetLocalizationLocale("en-US")
            };
            foreach (var edit in edits)
            {
                edit();
                Assert.AreEqual(MxGraphicsFeatureQuality.Medium, presenter.WorkingProfile.Graphics.DirectionalShadowQuality);
            }
            var item = presenter.BuildViewModel().Sections.SelectMany(section => section.Items)
                .Single(value => value.Key == MxSettingsUiItemKey.GraphicsDirectionalShadowQuality);
            CollectionAssert.AreEqual(new[] { "0", "2", "3", "4" }, item.Options.Select(option => option.Value));
        }

        [Test]
        public void DraftCancelApplySaveAndNewSessionUseExistingStore()
        {
            string path = Path.Combine(Path.GetTempPath(), "mx-shadow-quality-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new MxSettingsJsonFileStore(path);
                Assert.IsTrue(store.Save(Profile(MxGraphicsFeatureQuality.Default)).Success);
                var service = new MxSettingsService(store); service.Load();
                var scene = new MxSceneRenderingConfiguration();
                using (var applier = new MxDirectionalShadowSettingsApplier(() => scene))
                {
                    service.RegisterApplier(applier);
                    var presenter = new MxSettingsUiPresenter(service);
                    presenter.SetDirectionalShadowQuality(MxGraphicsFeatureQuality.Low);
                    Assert.AreEqual(0, scene.DynamicShadowResolutionOverride);
                    presenter.ReloadProfile();
                    Assert.AreEqual(MxGraphicsFeatureQuality.Default, presenter.WorkingProfile.Graphics.DirectionalShadowQuality);
                    presenter.SetDirectionalShadowQuality(MxGraphicsFeatureQuality.Medium);
                    Assert.IsTrue(presenter.ApplyWorkingProfile().Success);
                    Assert.AreEqual(512, scene.EffectiveDynamicShadowResolution);
                    Assert.IsTrue(presenter.SaveWorkingProfile().Success);
                    Assert.IsFalse(presenter.Dirty);
                    presenter.SetDirectionalShadowQuality(MxGraphicsFeatureQuality.High);
                    presenter.ReloadProfile();
                    Assert.AreEqual(512, scene.EffectiveDynamicShadowResolution);
                    Assert.AreEqual(MxGraphicsFeatureQuality.Medium, presenter.WorkingProfile.Graphics.DirectionalShadowQuality);
                }
                Assert.AreEqual(0, scene.DynamicShadowResolutionOverride);
                var restarted = new MxSettingsService(new MxSettingsJsonFileStore(path)); restarted.Load();
                using (var applier = new MxDirectionalShadowSettingsApplier(() => scene))
                {
                    restarted.RegisterApplier(applier);
                    Assert.IsTrue(restarted.Apply().Success);
                    Assert.AreEqual(512, scene.EffectiveDynamicShadowResolution);
                }
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void MappingKeepsAuthoredStaticAndLightingInputsAndRestoresOwner()
        {
            var original = new MxSceneRenderingConfiguration {
                DynamicShadowResolution = 2048, DynamicShadowResolutionOverride = 4096,
                DirectionalShadowsEnabled = false, DynamicDirectionalShadows = false,
                DirectionalShadowCenterResolution = 4096, DirectionalShadowCenterScale = .25f,
                DirectionalShadowFilterRadius = .03f, DirectionalShadowBias = .025f,
                IndirectLightingEnabled = true
            };
            var selected = original;
            var replacement = new MxSceneRenderingConfiguration { DynamicShadowResolutionOverride = 128 };
            var applier = new MxDirectionalShadowSettingsApplier(() => selected);
            try
            {
                var qualities = new[] { MxGraphicsFeatureQuality.Low, MxGraphicsFeatureQuality.Medium, MxGraphicsFeatureQuality.High, MxGraphicsFeatureQuality.Default };
                var sizes = new[] { 256, 512, 1024, 2048 };
                for (int i = 0; i < qualities.Length; i++)
                {
                    Assert.IsTrue(applier.Apply(Profile(qualities[i])).Success);
                    Assert.AreEqual(sizes[i], original.EffectiveDynamicShadowResolution);
                }
                Assert.IsFalse(original.DirectionalShadowsEnabled);
                Assert.IsFalse(original.DynamicDirectionalShadows);
                Assert.IsTrue(original.IndirectLightingEnabled);
                Assert.AreEqual(2048, original.DynamicShadowResolution);
                Assert.AreEqual(4096, original.DirectionalShadowCenterResolution);
                Assert.AreEqual(.25f, original.DirectionalShadowCenterScale);
                Assert.AreEqual(.03f, original.DirectionalShadowFilterRadius);
                Assert.AreEqual(.025f, original.DirectionalShadowBias);
                selected = replacement;
                Assert.IsTrue(applier.Apply(Profile(MxGraphicsFeatureQuality.Low)).Success);
                Assert.AreEqual(4096, original.DynamicShadowResolutionOverride);
                Assert.AreEqual(256, replacement.DynamicShadowResolutionOverride);
            }
            finally { applier.Dispose(); }
            Assert.AreEqual(128, replacement.DynamicShadowResolutionOverride);
            applier.Dispose();
            Assert.IsFalse(applier.Apply(Profile(MxGraphicsFeatureQuality.Low)).Success);
        }

        [Test]
        public void MissingConfigurationReportsFailureForAnExplicitQuality()
        {
            using (var applier = new MxDirectionalShadowSettingsApplier(() => null))
            {
                Assert.IsTrue(applier.Apply(Profile(MxGraphicsFeatureQuality.Default)).Success);
                Assert.IsFalse(applier.Apply(Profile(MxGraphicsFeatureQuality.High)).Success);
            }
        }
    }
}
