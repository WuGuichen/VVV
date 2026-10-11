using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Settings;
using MxFramework.Settings.Graphics;
using MxFramework.Settings.UI;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityCamera = UnityEngine.Camera;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Settings
{
    public sealed class HdrOutputSettingsTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private MxUnityHdrOutputRuntime _owner;
        private Scene _fixtureScene, _originalScene;
        private UnityCamera _manual, _automatic;
        private int _manualFrames, _automaticFrames;
        private TonemappingMode _automaticMode;
        private float _automaticWhite, _automaticPeak, _automaticExposure;

        [UnityTest]
        public IEnumerator ActualOwnerRefreshesManualStacksOnChangeAndRelease()
        {
            yield return new EnterPlayMode();
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset) ||
                (HDROutputSettings.main != null && HDROutputSettings.main.available))
                Assert.Ignore("Requires URP and an SDR-only display; this fixture never requests physical HDR changes.");
            _originalScene = SceneManager.GetActiveScene();
            _fixtureScene = SceneManager.CreateScene("HdrOwnerRegression_Transient");
            SceneManager.SetActiveScene(_fixtureScene);
            _manual = CreateCamera(VolumeFrameworkUpdateMode.UsePipelineSettings);
            _automatic = CreateCamera(VolumeFrameworkUpdateMode.EveryFrame);
            if (_manual.GetUniversalAdditionalCameraData().requiresVolumeFrameworkUpdate)
                Assert.Ignore("Requires the existing pipeline's ViaScripting policy; do not change project settings.");
            RenderPipelineManager.endCameraRendering += ObserveCamera;
            yield return WaitForRenderedCameras();
            _manual.UpdateVolumeStack();
            var baseline = _manual.GetUniversalAdditionalCameraData().volumeStack.GetComponent<Tonemapping>();
            TonemappingMode baselineMode = baseline.mode.value;
            float baselineWhite = baseline.paperWhite.value, baselinePeak = baseline.maxNits.value;

            var volumeObject = new GameObject("Owned exposure control"); _owned.Add(volumeObject);
            volumeObject.layer = 31;
            var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10001;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); _owned.Add(profile);
            var exposure = profile.Add<ColorAdjustments>(); _owned.Add(exposure);
            exposure.postExposure.Override(1); volume.sharedProfile = profile;
            _owner = new MxUnityHdrOutputRuntime();
            Assert.IsTrue(_owner.TryApply(new MxHdrOutputSettings(MxHdrOutputMode.Disabled, 120, 500), out var message), message);
            yield return WaitForRenderedCameras();
            AssertTone(_manual, TonemappingMode.ACES, 120, 500);
            AssertTone(_automatic, TonemappingMode.ACES, 120, 500);
            Assert.IsTrue(_owner.TryApply(new MxHdrOutputSettings(MxHdrOutputMode.Disabled, 320, 2400), out message), message);
            yield return WaitForRenderedCameras();
            AssertTone(_manual, TonemappingMode.ACES, 320, 2400);

            // Changing an unrelated Volume does not silently turn ViaScripting into EveryFrame.
            exposure.postExposure.Override(2);
            Assert.IsTrue(_owner.TryApply(new MxHdrOutputSettings(MxHdrOutputMode.Disabled, 320, 2400), out message), message);
            yield return WaitForRenderedCameras();
            Assert.AreEqual(1, _manual.GetUniversalAdditionalCameraData().volumeStack.GetComponent<ColorAdjustments>().postExposure.value);
            Assert.AreEqual(2, _automaticExposure);
            _manual.DestroyVolumeStack();
            yield return WaitForRenderedCameras();
            AssertTone(_manual, TonemappingMode.ACES, 320, 2400);
            Assert.AreEqual(2, _manual.GetUniversalAdditionalCameraData().volumeStack.GetComponent<ColorAdjustments>().postExposure.value);

            // Mask exclusion removes our cached override; adding the mask back restores the current owner.
            _manual.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
            yield return WaitForRenderedCameras();
            AssertTone(_manual, baselineMode, baselineWhite, baselinePeak);
            _manual.GetUniversalAdditionalCameraData().volumeLayerMask = 1 | (1 << 31);
            yield return WaitForRenderedCameras();
            AssertTone(_manual, TonemappingMode.ACES, 320, 2400);
            _manual.enabled = false;
            Assert.IsTrue(_owner.TryApply(MxHdrOutputSettings.Default, out message), message);
            AssertTone(_manual, baselineMode, baselineWhite, baselinePeak); // Disabled cameras retain a stack.
            _manual.enabled = true;
            Assert.IsTrue(_owner.TryApply(new MxHdrOutputSettings(MxHdrOutputMode.Disabled, 120, 500), out message), message);
            yield return WaitForRenderedCameras();
            AssertTone(_manual, TonemappingMode.ACES, 120, 500);
            _owner.Dispose(); _owner = null;
            AssertTone(_manual, baselineMode, baselineWhite, baselinePeak);
            yield return WaitForRenderedCameras();
            AssertTone(_automatic, baselineMode, baselineWhite, baselinePeak);
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedFixture()
        {
            RenderPipelineManager.endCameraRendering -= ObserveCamera;
            _owner?.Dispose(); _owner = null;
            if (_manual != null) _manual.DestroyVolumeStack();
            if (_automatic != null) _automatic.DestroyVolumeStack();
            foreach (var item in _owned) if (item != null) Object.Destroy(item);
            _owned.Clear(); _manual = null; _automatic = null;
            if (_fixtureScene.IsValid() && _fixtureScene.isLoaded)
            {
                if (_originalScene.IsValid() && _originalScene.isLoaded) SceneManager.SetActiveScene(_originalScene);
                yield return SceneManager.UnloadSceneAsync(_fixtureScene);
            }
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        }

        private UnityCamera CreateCamera(VolumeFrameworkUpdateMode mode)
        {
            var go = new GameObject("HDR test " + mode); _owned.Add(go);
            var camera = go.AddComponent<UnityCamera>(); camera.cullingMask = 0;
            var target = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBHalf); _owned.Add(target);
            target.Create(); camera.targetTexture = target;
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
            data.volumeLayerMask = 1 | (1 << 31); data.volumeTrigger = camera.transform;
            camera.SetVolumeFrameworkUpdateMode(mode);
            return camera;
        }

        private void ObserveCamera(ScriptableRenderContext context, UnityCamera camera)
        {
            if (camera == _manual) _manualFrames++;
            if (camera != _automatic) return;
            _automaticFrames++;
            var tone = VolumeManager.instance.stack.GetComponent<Tonemapping>();
            _automaticMode = tone.mode.value; _automaticWhite = tone.paperWhite.value; _automaticPeak = tone.maxNits.value;
            _automaticExposure = VolumeManager.instance.stack.GetComponent<ColorAdjustments>().postExposure.value;
        }

        private IEnumerator WaitForRenderedCameras()
        {
            int manualTarget = _manualFrames + 3, automaticTarget = _automaticFrames + 3;
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (_manualFrames < manualTarget || _automaticFrames < automaticTarget)
            { Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "Expected actual rendered camera callbacks."); yield return null; }
        }

        private void AssertTone(UnityCamera camera, TonemappingMode mode, float white, float peak)
        {
            if (camera == _automatic)
            { Assert.AreEqual(mode, _automaticMode); Assert.AreEqual(white, _automaticWhite); Assert.AreEqual(peak, _automaticPeak); return; }
            var tone = camera.GetUniversalAdditionalCameraData().volumeStack.GetComponent<Tonemapping>();
            Assert.AreEqual(mode, tone.mode.value); Assert.AreEqual(white, tone.paperWhite.value); Assert.AreEqual(peak, tone.maxNits.value);
        }

        [Test]
        public void LegacyProfilesKeepSceneOutputAndHdrPreferencesRoundTrip()
        {
            var legacy = MxSettingsProfileJson.LoadFromJson("{\"format\":\"mx.settings.v1\",\"graphics\":{\"renderScale\":0.8}}");
            Assert.AreEqual(MxHdrOutputMode.Default, legacy.Profile.Graphics.HdrOutput.Mode);
            var profile = legacy.Profile.WithGraphics(new MxGraphicsSettings(renderScale: .8f,
                hdrOutput: new MxHdrOutputSettings(MxHdrOutputMode.Enabled, 200, 600)));
            var roundTrip = MxSettingsProfileJson.LoadFromJson(MxSettingsProfileJson.SaveToJson(profile));
            Assert.IsTrue(roundTrip.Success);
            Assert.AreEqual(MxHdrOutputMode.Enabled, roundTrip.Profile.Graphics.HdrOutput.Mode);
            Assert.AreEqual(200, roundTrip.Profile.Graphics.HdrOutput.PaperWhiteNits);
            Assert.AreEqual(600, roundTrip.Profile.Graphics.HdrOutput.PeakNits);
            Assert.AreEqual(.8f, roundTrip.Profile.Graphics.RenderScale);
        }

        [Test]
        public void InvalidCalibrationCannotPropagateNonFiniteLuminance()
        {
            var hdr = new MxHdrOutputSettings((MxHdrOutputMode)99, float.NaN, float.PositiveInfinity);
            Assert.AreEqual(MxHdrOutputMode.Default, hdr.Mode);
            Assert.AreEqual(0, hdr.PaperWhiteNits);
            Assert.AreEqual(0, hdr.PeakNits);
            hdr = new MxHdrOutputSettings(MxHdrOutputMode.Enabled, 10000, 1);
            Assert.AreEqual(400, hdr.PaperWhiteNits);
            Assert.AreEqual(400, hdr.PeakNits);
        }

        [Test]
        public void EditingOtherGraphicsSettingsKeepsHdrCalibrationAndCancelRestoresDraft()
        {
            var hdr = new MxHdrOutputSettings(MxHdrOutputMode.Enabled, 160, 800);
            var profile = MxSettingsProfile.CreateDefault().WithGraphics(new MxGraphicsSettings(hdrOutput: hdr));
            var presenter = new MxSettingsUiPresenter(new MxSettingsService(null, profile));
            Action[] changes = { () => presenter.SetVSyncCount(1), () => presenter.SetRenderScale(.9f),
                () => presenter.SetResolution(1920, 1080), () => presenter.SetWindowMode(MxWindowMode.FullscreenWindow),
                () => presenter.SetTargetFrameRate(60), () => presenter.SetGraphicsQualityPreset(MxGraphicsQualityPreset.High),
                () => presenter.SetAntiAliasingSamples(4), () => presenter.SetShadowDistance(100),
                () => presenter.SetMainLightShadowmapResolution(2048), () => presenter.SetShadowCascadeCount(2),
                () => presenter.SetAdditionalLightsShadowmapResolution(1024) };
            foreach (var change in changes) { change(); Assert.AreSame(hdr, presenter.WorkingProfile.Graphics.HdrOutput); }
            presenter.SetHdrOutput(new MxHdrOutputSettings(MxHdrOutputMode.Disabled, 300, 1000));
            presenter.ResetDirty();
            Assert.AreSame(hdr, presenter.WorkingProfile.Graphics.HdrOutput);
        }

        [Test]
        public void HdrIntentAndActualUnavailableStateRemainDistinctInUi()
        {
            var profile = MxSettingsProfile.CreateDefault().WithGraphics(new MxGraphicsSettings(
                hdrOutput: new MxHdrOutputSettings(MxHdrOutputMode.Enabled, 200, 1000)));
            using var runtime = new CharacterTestSettingsRuntime("unused.json", new MxSettingsService(null, profile),
                null, MxSettingsLoadResult.Succeeded(profile));
            var display = new FakeHdrRuntime { Status = new MxHdrOutputStatus(MxHdrOutputState.Unavailable) };
            runtime.AttachHdrOutput(display);
            var view = CharacterTestFairyGuiSettingsPanelViewModelBuilder.Build(runtime);
            Assert.AreEqual("开启", view.HdrModeText);
            StringAssert.Contains("SDR", view.HdrStatusText);
            Assert.IsTrue(view.CanToggleHdr); // Always allow disabling an unavailable saved HDR preference.
            Assert.IsFalse(view.CanCalibrateHdr);
            Assert.IsTrue(runtime.ApplyCurrentProfile().Success);
            Assert.AreEqual(MxHdrOutputMode.Enabled, display.Applied.Mode);
            display.Status = new MxHdrOutputStatus(MxHdrOutputState.Hdr, true, 200, 1000);
            var active = CharacterTestFairyGuiSettingsPanelViewModelBuilder.Build(runtime);
            Assert.IsTrue(active.CanCalibrateHdr);
            Assert.AreNotEqual(view.HdrStatusText, active.HdrStatusText);
        }

        [Test]
        public void DefaultHdrSettingsDoNotTakeOverAuthoringOutputInEditMode()
        {
            using var runtime = new MxUnityHdrOutputRuntime();
            Assert.IsTrue(runtime.TryApply(MxHdrOutputSettings.Default, out _));
            Assert.IsFalse(runtime.TryApply(new MxHdrOutputSettings(MxHdrOutputMode.Enabled), out string message));
            StringAssert.Contains("Play Mode", message);
            Assert.AreEqual(MxHdrOutputState.Unavailable, runtime.Status.State);
        }

        [Test]
        public void DefaultPreferenceShowsAnAlreadyActiveHdrDisplayTruthfully()
        {
            var profile = MxSettingsProfile.CreateDefault();
            using var runtime = new CharacterTestSettingsRuntime("unused.json", new MxSettingsService(null, profile),
                null, MxSettingsLoadResult.Succeeded(profile));
            runtime.AttachHdrOutput(new FakeHdrRuntime { Status = new MxHdrOutputStatus(MxHdrOutputState.Hdr, true) });
            var view = CharacterTestFairyGuiSettingsPanelViewModelBuilder.Build(runtime);
            Assert.AreEqual("开启", view.HdrModeText);
            Assert.IsTrue(view.CanCalibrateHdr);
            Assert.AreEqual(MxHdrOutputMode.Default, runtime.SettingsUi.WorkingProfile.Graphics.HdrOutput.Mode);
        }

        [Test]
        public void GraphicsCatalogExposesHdrControls()
        {
            var keys = MxGraphicsSettingsCatalog.PlayerVisibleDescriptors.Select(x => x.Key).ToArray();
            CollectionAssert.Contains(keys, MxGraphicsSettingKey.HdrOutput);
            CollectionAssert.Contains(keys, MxGraphicsSettingKey.HdrPaperWhite);
            CollectionAssert.Contains(keys, MxGraphicsSettingKey.HdrPeakBrightness);
        }

        private sealed class FakeHdrRuntime : IMxHdrOutputRuntime
        {
            public MxHdrOutputStatus Status { get; set; }
            public MxHdrOutputSettings Applied;
            public bool TryApply(MxHdrOutputSettings settings, out string message)
            { Applied = settings; message = string.Empty; return true; }
        }
    }
}
