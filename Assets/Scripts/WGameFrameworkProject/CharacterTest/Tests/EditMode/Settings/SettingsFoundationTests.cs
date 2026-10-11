using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Audio;
using MxFramework.Game.CharacterTest;
using MxFramework.Rendering;
using MxFramework.Settings;
using MxFramework.Settings.Audio;
using MxFramework.Settings.Graphics;
using MxFramework.Settings.Input;
using NUnit.Framework;
using UnityEngine;
using MxInput = MxFramework.Input;

namespace MxFramework.Tests.Settings
{
    public sealed class SettingsFoundationTests
    {
        [Test]
        public void JsonFileStore_WhenFileMissing_ReturnsDefaultProfileWithIssue()
        {
            string path = CreateTempPath();
            var store = new MxSettingsJsonFileStore(path);

            MxSettingsLoadResult result = store.LoadOrDefault();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(MxSettingsProfile.DefaultProfileId, result.Profile.ProfileId);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(MxSettingsIssueCode.MissingFile, result.Issues[0].Code);
        }

        [Test]
        public void JsonFileStore_WhenJsonIsInvalid_ReturnsDefaultProfileWithIssue()
        {
            string path = CreateTempPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{ invalid json");
            var store = new MxSettingsJsonFileStore(path);

            MxSettingsLoadResult result = store.LoadOrDefault();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(MxSettingsProfile.DefaultProfileId, result.Profile.ProfileId);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(MxSettingsIssueCode.InvalidJson, result.Issues[0].Code);
        }

        [Test]
        public void JsonFileStore_SaveLoadRoundTrip_PreservesAudioSettings()
        {
            string path = CreateTempPath();
            var store = new MxSettingsJsonFileStore(path);
            var profile = new MxSettingsProfile(
                "profile-a",
                new MxAudioSettings(new[]
                {
                    new MxAudioBusSetting(10, 0.25f, false),
                    new MxAudioBusSetting(20, 0.75f, true)
                }),
                updatedUtc: "2026-06-07T00:00:00.0000000Z");

            MxSettingsSaveResult save = store.Save(profile);
            MxSettingsLoadResult load = store.LoadOrDefault();

            Assert.IsTrue(save.Success);
            Assert.IsTrue(load.Success);
            Assert.AreEqual("profile-a", load.Profile.ProfileId);
            Assert.AreEqual("2026-06-07T00:00:00.0000000Z", load.Profile.UpdatedUtc);
            Assert.AreEqual(2, load.Profile.Audio.BusSettings.Count);
            Assert.AreEqual(10, load.Profile.Audio.BusSettings[0].BusId);
            Assert.AreEqual(0.25f, load.Profile.Audio.BusSettings[0].Volume);
            Assert.IsFalse(load.Profile.Audio.BusSettings[0].Muted);
            Assert.AreEqual(20, load.Profile.Audio.BusSettings[1].BusId);
            Assert.AreEqual(0.75f, load.Profile.Audio.BusSettings[1].Volume);
            Assert.IsTrue(load.Profile.Audio.BusSettings[1].Muted);
        }

        [Test]
        public void SettingsProfile_LegacyConstructorPreservesDefaultGraphics()
        {
            var profile = new MxSettingsProfile(
                "legacy",
                MxAudioSettings.Empty,
                MxSettingsProfile.ExpectedFormat,
                "2026-06-07T00:00:00.0000000Z");

            Assert.AreEqual("legacy", profile.ProfileId);
            Assert.AreSame(MxAudioSettings.Empty, profile.Audio);
            Assert.AreEqual(MxGraphicsSettings.Default, profile.Graphics);
            Assert.AreEqual(MxLocalizationSettings.DefaultLocaleId, profile.Localization.LocaleId);
            Assert.AreEqual("2026-06-07T00:00:00.0000000Z", profile.UpdatedUtc);
        }

        [Test]
        public void JsonProfile_WhenGraphicsMissing_LoadsDefaultGraphicsSettings()
        {
            const string json = "{\"format\":\"mx.settings.v1\",\"profileId\":\"profile-a\",\"audio\":{\"busSettings\":[]},\"updatedUtc\":\"\"}";

            MxSettingsLoadResult load = MxSettingsProfileJson.LoadFromJson(json);

            Assert.IsTrue(load.Success);
            Assert.IsFalse(load.HasIssues, FirstIssue(load.Issues));
            Assert.AreEqual(MxGraphicsQualityPreset.Custom, load.Profile.Graphics.QualityPreset);
            Assert.AreEqual(0, load.Profile.Graphics.ResolutionWidth);
            Assert.AreEqual(-1, load.Profile.Graphics.VSyncCount);
            Assert.AreEqual(0f, load.Profile.Graphics.RenderScale);
            Assert.AreEqual(MxLocalizationSettings.DefaultLocaleId, load.Profile.Localization.LocaleId);
        }

        [Test]
        public void JsonFileStore_SaveLoadRoundTrip_PreservesLocalizationSettings()
        {
            string path = CreateTempPath();
            var store = new MxSettingsJsonFileStore(path);
            var profile = new MxSettingsProfile(
                "profile-localization",
                updatedUtc: "2026-06-07T00:00:00.0000000Z",
                localization: new MxLocalizationSettings("en-US"));

            MxSettingsSaveResult save = store.Save(profile);
            MxSettingsLoadResult load = store.LoadOrDefault();

            Assert.IsTrue(save.Success);
            Assert.IsTrue(load.Success);
            Assert.AreEqual("profile-localization", load.Profile.ProfileId);
            Assert.AreEqual("en-US", load.Profile.Localization.LocaleId);
            StringAssert.Contains("\"localization\"", File.ReadAllText(path));
            StringAssert.Contains("\"localeId\": \"en-US\"", File.ReadAllText(path));
        }

        [Test]
        public void JsonFileStore_SaveLoadRoundTrip_PreservesGraphicsSettings()
        {
            string path = CreateTempPath();
            var store = new MxSettingsJsonFileStore(path);
            var graphics = new MxGraphicsSettings(
                MxGraphicsQualityPreset.High,
                resolutionWidth: 1920,
                resolutionHeight: 1080,
                applyWindowMode: true,
                windowMode: MxWindowMode.FullscreenWindow,
                vSyncCount: 1,
                targetFrameRate: 144,
                renderScale: 1.25f,
                antiAliasingSamples: 4,
                shadowDistance: 72f,
                volumetricFogQuality: MxGraphicsFeatureQuality.Medium,
                unityQualityLevel: 2,
                mainLightShadowmapResolution: 4096,
                shadowCascadeCount: 3,
                additionalLightsShadowmapResolution: 2048);
            var profile = new MxSettingsProfile(
                "profile-graphics",
                updatedUtc: "2026-06-07T00:00:00.0000000Z",
                graphics: graphics);

            MxSettingsSaveResult save = store.Save(profile);
            MxSettingsLoadResult load = store.LoadOrDefault();

            Assert.IsTrue(save.Success);
            Assert.IsTrue(load.Success);
            Assert.AreEqual("profile-graphics", load.Profile.ProfileId);
            Assert.AreEqual(MxGraphicsQualityPreset.High, load.Profile.Graphics.QualityPreset);
            Assert.AreEqual(1920, load.Profile.Graphics.ResolutionWidth);
            Assert.AreEqual(1080, load.Profile.Graphics.ResolutionHeight);
            Assert.IsTrue(load.Profile.Graphics.ApplyWindowMode);
            Assert.AreEqual(MxWindowMode.FullscreenWindow, load.Profile.Graphics.WindowMode);
            Assert.AreEqual(1, load.Profile.Graphics.VSyncCount);
            Assert.AreEqual(144, load.Profile.Graphics.TargetFrameRate);
            Assert.AreEqual(1.25f, load.Profile.Graphics.RenderScale);
            Assert.AreEqual(4, load.Profile.Graphics.AntiAliasingSamples);
            Assert.AreEqual(72f, load.Profile.Graphics.ShadowDistance);
            Assert.AreEqual(4096, load.Profile.Graphics.MainLightShadowmapResolution);
            Assert.AreEqual(3, load.Profile.Graphics.ShadowCascadeCount);
            Assert.AreEqual(2048, load.Profile.Graphics.AdditionalLightsShadowmapResolution);
            Assert.AreEqual(MxGraphicsFeatureQuality.Medium, load.Profile.Graphics.VolumetricFogQuality);
            Assert.AreEqual(2, load.Profile.Graphics.UnityQualityLevel);
        }

        [Test]
        public void GraphicsSettings_ClampsValues()
        {
            var settings = new MxGraphicsSettings(
                vSyncCount: 99,
                targetFrameRate: 2,
                renderScale: 9f,
                antiAliasingSamples: 16,
                shadowDistance: 999f,
                unityQualityLevel: -5);

            Assert.AreEqual(2, settings.VSyncCount);
            Assert.AreEqual(15, settings.TargetFrameRate);
            Assert.AreEqual(2f, settings.RenderScale);
            Assert.AreEqual(8, settings.AntiAliasingSamples);
            Assert.AreEqual(500f, settings.ShadowDistance);
            Assert.AreEqual(-1, settings.UnityQualityLevel);
        }

        [Test]
        public void JsonProfile_InvalidGraphicsValues_LoadsClampedProfileWithIssues()
        {
            const string json =
                "{" +
                "\"format\":\"mx.settings.v1\"," +
                "\"profileId\":\"profile-a\"," +
                "\"graphics\":{" +
                "\"qualityPreset\":99," +
                "\"resolutionWidth\":1920," +
                "\"resolutionHeight\":0," +
                "\"vSyncCount\":99," +
                "\"renderScale\":9," +
                "\"antiAliasingSamples\":3," +
                "\"volumetricFogQuality\":99" +
                "}}";

            MxSettingsLoadResult load = MxSettingsProfileJson.LoadFromJson(json);

            Assert.IsTrue(load.Success);
            Assert.IsTrue(load.HasIssues);
            Assert.AreEqual(MxGraphicsQualityPreset.Custom, load.Profile.Graphics.QualityPreset);
            Assert.AreEqual(0, load.Profile.Graphics.ResolutionWidth);
            Assert.AreEqual(2, load.Profile.Graphics.VSyncCount);
            Assert.AreEqual(2f, load.Profile.Graphics.RenderScale);
            Assert.AreEqual(4, load.Profile.Graphics.AntiAliasingSamples);
            Assert.AreEqual(MxGraphicsFeatureQuality.Default, load.Profile.Graphics.VolumetricFogQuality);
        }

        [Test]
        public void GraphicsSettingsCatalog_PlayerVisibleDescriptors_ExcludeDeveloperSettings()
        {
            var playerKeys = new HashSet<MxGraphicsSettingKey>();
            IReadOnlyList<MxGraphicsSettingDescriptor> descriptors =
                MxGraphicsSettingsCatalog.PlayerVisibleDescriptors;
            for (int i = 0; i < descriptors.Count; i++)
                playerKeys.Add(descriptors[i].Key);

            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.Resolution));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.WindowMode));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.VSync));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.TargetFrameRate));
            Assert.IsFalse(playerKeys.Contains(MxGraphicsSettingKey.QualityPreset));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.RenderScale));
            Assert.IsFalse(playerKeys.Contains(MxGraphicsSettingKey.UnityQualityLevel));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.AntiAliasingSamples));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.ShadowDistance));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.MainLightShadowmapResolution));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.ShadowCascadeCount));
            Assert.IsTrue(playerKeys.Contains(MxGraphicsSettingKey.AdditionalLightsShadowmapResolution));
            Assert.IsFalse(playerKeys.Contains(MxGraphicsSettingKey.VolumetricFogQuality));
        }

        [Test]
        public void AudioBusSetting_ClampsVolume()
        {
            var low = new MxAudioBusSetting(10, -1f, false);
            var high = new MxAudioBusSetting(10, 2f, false);

            Assert.AreEqual(0f, low.Volume);
            Assert.AreEqual(1f, high.Volume);
        }

        [Test]
        public void AudioSettingsApplier_AppliesVolumeThenMuted()
        {
            MemoryAudioDefinitions definitions = CreateAudioDefinitions();
            using (var audio = new AudioService(definitions, new NullAudioBackend()))
            {
                var applier = new MxAudioSettingsApplier(audio);
                var profile = new MxSettingsProfile(
                    audio: new MxAudioSettings(new[]
                    {
                        new MxAudioBusSetting(10, 2f, true)
                    }));

                MxSettingsApplyResult result = applier.Apply(profile);

                Assert.IsTrue(result.Success);
                AudioDebugSnapshot snapshot = audio.CaptureSnapshot();
                Assert.AreEqual(1, snapshot.BusStates.Count);
                Assert.AreEqual(10, snapshot.BusStates[0].BusId);
                Assert.AreEqual(1f, snapshot.BusStates[0].Volume);
                Assert.IsTrue(snapshot.BusStates[0].Muted);
            }
        }

        [Test]
        public void AudioSettingsApplier_UnknownBusReportsIssueAndContinues()
        {
            MemoryAudioDefinitions definitions = CreateAudioDefinitions();
            using (var audio = new AudioService(definitions, new NullAudioBackend()))
            {
                var applier = new MxAudioSettingsApplier(audio);
                var profile = new MxSettingsProfile(
                    audio: new MxAudioSettings(new[]
                    {
                        new MxAudioBusSetting(10, 0.5f, false),
                        new MxAudioBusSetting(999, 0.1f, true)
                    }));

                MxSettingsApplyResult result = applier.Apply(profile);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(1, result.Issues.Count);
                AudioDebugSnapshot snapshot = audio.CaptureSnapshot();
                Assert.AreEqual(1, snapshot.BusStates.Count);
                Assert.AreEqual(10, snapshot.BusStates[0].BusId);
                Assert.AreEqual(0.5f, snapshot.BusStates[0].Volume);
                Assert.IsFalse(snapshot.BusStates[0].Muted);
            }
        }

        [Test]
        public void InputSettingsController_ListsBindingDisplayStrings()
        {
            UnityEngine.InputSystem.InputActionAsset asset = MxInput.DefaultInputActionAssetFactory.Create();
            try
            {
                var controller = new MxInputRebindingSettingsController(
                    new FakeRebindingService(),
                    new MxInput.InputBindingDisplayService(asset));

                MxInputBindingListResult result = controller.ListBindings(new[]
                {
                    new MxInputBindingTarget("Gameplay/Jump", 0)
                });

                Assert.IsTrue(result.Success, FirstIssue(result.Issues));
                Assert.AreEqual(1, result.Bindings.Count);
                Assert.AreEqual("Gameplay/Jump", result.Bindings[0].Target.ActionId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.Bindings[0].DisplayString));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void InputSettingsController_DelegatesOperationsToRebindingService()
        {
            var rebinding = new FakeRebindingService();
            var controller = new MxInputRebindingSettingsController(rebinding, null);

            Assert.IsTrue(controller.StartRebind("Gameplay/Jump", 0).Success);
            Assert.IsTrue(controller.IsRebinding);
            Assert.IsTrue(controller.Cancel().Success);
            Assert.IsTrue(controller.Load().Success);
            Assert.IsTrue(controller.Save().Success);
            Assert.IsTrue(controller.ResetToDefault().Success);

            Assert.AreEqual("Gameplay/Jump", rebinding.LastActionId);
            Assert.AreEqual(0, rebinding.LastBindingIndex);
            Assert.AreEqual(1, rebinding.StartCount);
            Assert.AreEqual(1, rebinding.CancelCount);
            Assert.AreEqual(1, rebinding.LoadCount);
            Assert.AreEqual(1, rebinding.SaveCount);
            Assert.AreEqual(1, rebinding.ResetCount);
        }

        [Test]
        public void CharacterTestSettingsRuntime_CanBeCreatedWithoutAudioService()
        {
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                new MxSettingsService(null),
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()));

            Assert.IsNotNull(runtime.SettingsService);
            Assert.IsNotNull(runtime.InputSettings);
            Assert.AreEqual(MxSettingsProfile.DefaultProfileId, runtime.CurrentProfile.ProfileId);
        }

        [Test]
        public void CharacterTestSettingsRuntime_RegistersGraphicsApplierWithoutAutoApply()
        {
            var applier = new RecordingSettingsApplier();
            var service = new MxSettingsService(null);

            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                service,
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(service.CurrentProfile),
                applier);

            Assert.AreEqual(1, service.Appliers.Count);
            Assert.AreEqual(0, applier.ApplyCount);
            Assert.IsTrue(runtime.LastApplyResult.Success);

            MxSettingsApplyResult apply = runtime.ApplyCurrentProfile();

            Assert.IsTrue(apply.Success, FirstIssue(apply.Issues));
            Assert.AreSame(apply, runtime.LastApplyResult);
            Assert.AreEqual(1, applier.ApplyCount);
        }

        [Test]
        public void CharacterTestSettingsRuntime_ApplyCurrentProfile_ReturnsApplierIssues()
        {
            var applier = new RecordingSettingsApplier
            {
                Result = MxSettingsApplyResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.UnsupportedSetting,
                    "settings.graphics",
                    "Unsupported graphics setting."))
            };
            var service = new MxSettingsService(null);
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                service,
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(service.CurrentProfile),
                applier);

            MxSettingsApplyResult apply = runtime.ApplyCurrentProfile();

            Assert.IsFalse(apply.Success);
            Assert.AreSame(apply, runtime.LastApplyResult);
            Assert.AreEqual(1, apply.Issues.Count);
            Assert.AreEqual(MxSettingsIssueCode.UnsupportedSetting, apply.Issues[0].Code);
        }

        [Test]
        public void CharacterTestSettingsRuntime_ReloadAndSaveReturnStructuredResults()
        {
            var loadedProfile = new MxSettingsProfile(
                "loaded",
                graphics: new MxGraphicsSettings(vSyncCount: 1));
            var store = new FakeSettingsStore
            {
                LoadResult = MxSettingsLoadResult.Succeeded(loadedProfile)
            };
            var service = new MxSettingsService(store);
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                service,
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(service.CurrentProfile));

            MxSettingsLoadResult reload = runtime.ReloadProfile();

            Assert.IsTrue(reload.Success);
            Assert.AreSame(reload, runtime.LastLoadResult);
            Assert.AreEqual("loaded", runtime.CurrentProfile.ProfileId);
            Assert.AreEqual(1, store.LoadCount);

            var profileToSave = new MxSettingsProfile(
                "saved",
                new MxAudioSettings(new[]
                {
                    new MxAudioBusSetting(10, 0.5f, true)
                }),
                graphics: new MxGraphicsSettings(renderScale: 1.2f));
            MxSettingsSaveResult save = runtime.SaveCurrentProfile(profileToSave);

            Assert.IsTrue(save.Success);
            Assert.AreSame(save, runtime.LastSaveResult);
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual("saved", store.SavedProfile.ProfileId);
            Assert.AreEqual(10, store.SavedProfile.Audio.BusSettings[0].BusId);
            Assert.IsTrue(store.SavedProfile.Audio.BusSettings[0].Muted);
            Assert.AreEqual(1.2f, store.SavedProfile.Graphics.RenderScale);
            Assert.IsFalse(string.IsNullOrWhiteSpace(store.SavedProfile.UpdatedUtc));
        }

        [Test]
        public void UnityGraphicsSettingsApplier_AppliesRuntimeSettingsInOrder()
        {
            var runtime = new FakeGraphicsRuntime();
            var applier = new MxUnityGraphicsSettingsApplier(runtime);
            var profile = new MxSettingsProfile(
                graphics: new MxGraphicsSettings(
                    resolutionWidth: 1920,
                    resolutionHeight: 1080,
                    applyWindowMode: true,
                    windowMode: MxWindowMode.FullscreenWindow,
                    vSyncCount: 1,
                    targetFrameRate: 144,
                    renderScale: 1.25f,
                    antiAliasingSamples: 4,
                    shadowDistance: 72f,
                    unityQualityLevel: 2,
                    mainLightShadowmapResolution: 4096,
                    shadowCascadeCount: 3,
                    additionalLightsShadowmapResolution: 2048));

            MxSettingsApplyResult result = applier.Apply(profile);

            Assert.IsTrue(result.Success, FirstIssue(result.Issues));
            CollectionAssert.AreEqual(new[]
            {
                "quality:2:true",
                "resolution:1920x1080:FullscreenWindow",
                "vsync:1",
                "targetFrameRate:144",
                "renderScale:1.25",
                "aa:4",
                "shadowDistance:72",
                "mainShadowResolution:4096",
                "shadowCascades:3",
                "localShadowResolution:2048"
            }, runtime.Calls);
        }

        [Test]
        public void UnityGraphicsSettingsApplier_QualityPresetMapsToUnityQualityLevel()
        {
            var runtime = new FakeGraphicsRuntime
            {
                QualityLevelCountValue = 4
            };
            var applier = new MxUnityGraphicsSettingsApplier(runtime);
            var profile = new MxSettingsProfile(
                graphics: new MxGraphicsSettings(MxGraphicsQualityPreset.High));

            MxSettingsApplyResult result = applier.Apply(profile);

            Assert.IsTrue(result.Success, FirstIssue(result.Issues));
            CollectionAssert.AreEqual(new[]
            {
                "quality:2:true"
            }, runtime.Calls);
        }

        [Test]
        public void UnityGraphicsSettingsApplier_UnityQualityLevelOverridesQualityPreset()
        {
            var runtime = new FakeGraphicsRuntime
            {
                QualityLevelCountValue = 4
            };
            var applier = new MxUnityGraphicsSettingsApplier(runtime);
            var profile = new MxSettingsProfile(
                graphics: new MxGraphicsSettings(
                    MxGraphicsQualityPreset.Low,
                    unityQualityLevel: 3));

            MxSettingsApplyResult result = applier.Apply(profile);

            Assert.IsTrue(result.Success, FirstIssue(result.Issues));
            CollectionAssert.AreEqual(new[]
            {
                "quality:3:true"
            }, runtime.Calls);
        }

        [Test]
        public void UnityGraphicsSettingsApplier_OptionsCanSkipTargetFrameRate()
        {
            var runtime = new FakeGraphicsRuntime();
            var applier = new MxUnityGraphicsSettingsApplier(
                runtime,
                new MxUnityGraphicsSettingsApplyOptions(applyTargetFrameRate: false));
            var profile = new MxSettingsProfile(
                graphics: new MxGraphicsSettings(
                    vSyncCount: 1,
                    targetFrameRate: 144,
                    renderScale: 1.25f));

            MxSettingsApplyResult result = applier.Apply(profile);

            Assert.IsTrue(result.Success, FirstIssue(result.Issues));
            CollectionAssert.Contains(runtime.Calls, "vsync:1");
            CollectionAssert.Contains(runtime.Calls, "renderScale:1.25");
            CollectionAssert.DoesNotContain(runtime.Calls, "targetFrameRate:144");
        }

        [Test]
        public void UnityGraphicsSettingsApplier_UrpUnavailableReportsIssueAndContinues()
        {
            var runtime = new FakeGraphicsRuntime
            {
                SupportsUniversalRenderPipelineAsset = false
            };
            var applier = new MxUnityGraphicsSettingsApplier(runtime);
            var profile = new MxSettingsProfile(
                graphics: new MxGraphicsSettings(
                    vSyncCount: 1,
                    targetFrameRate: 120,
                    renderScale: 1.2f));

            MxSettingsApplyResult result = applier.Apply(profile);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(MxSettingsIssueCode.UnsupportedSetting, result.Issues[0].Code);
            CollectionAssert.Contains(runtime.Calls, "vsync:1");
            CollectionAssert.Contains(runtime.Calls, "targetFrameRate:120");
            CollectionAssert.Contains(runtime.Calls, "renderScale:unsupported");
        }

        [Test]
        public void UnityGraphicsSettingsApplier_InvalidQualityLevelReportsIssue()
        {
            var runtime = new FakeGraphicsRuntime
            {
                QualityLevelCountValue = 1
            };
            var applier = new MxUnityGraphicsSettingsApplier(runtime);
            var profile = new MxSettingsProfile(
                graphics: new MxGraphicsSettings(unityQualityLevel: 2));

            MxSettingsApplyResult result = applier.Apply(profile);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxSettingsIssueCode.InvalidTarget, result.Issues[0].Code);
            Assert.AreEqual(0, runtime.Calls.Count);
        }

        [Test]
        public void VolumetricFogSettingsApplier_MapsQualityToProvider()
        {
            using (var provider = new MxVolumetricFogPassProvider(MxVolumetricFogSettings.Default()))
            {
                var applier = new MxVolumetricFogSettingsApplier(provider);
                var profile = new MxSettingsProfile(
                    graphics: new MxGraphicsSettings(
                        volumetricFogQuality: MxGraphicsFeatureQuality.High));

                MxSettingsApplyResult result = applier.Apply(profile);

                Assert.IsTrue(result.Success, FirstIssue(result.Issues));
                Assert.IsTrue(provider.Settings.Enabled);
                Assert.AreEqual(MxVolumetricFogQuality.High, provider.Settings.Quality);
            }
        }

        [Test]
        public void VolumetricFogSettingsApplier_WhenProviderMissing_ReportsIssueForExplicitQuality()
        {
            var applier = new MxVolumetricFogSettingsApplier(null);
            var profile = new MxSettingsProfile(
                graphics: new MxGraphicsSettings(
                    volumetricFogQuality: MxGraphicsFeatureQuality.Low));

            MxSettingsApplyResult result = applier.Apply(profile);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxSettingsIssueCode.AdapterUnavailable, result.Issues[0].Code);
        }

        private static string CreateTempPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "MxFrameworkSettingsTests",
                Guid.NewGuid().ToString("N"),
                "settings.json");
        }

        private static string FirstIssue(IReadOnlyList<MxSettingsIssue> issues)
        {
            return issues != null && issues.Count > 0 ? issues[0].ToString() : string.Empty;
        }

        private static MemoryAudioDefinitions CreateAudioDefinitions()
        {
            var definitions = new MemoryAudioDefinitions();
            definitions.AddBus(new AudioBusDefinition(10, "SFX", "bus:/SFX"));
            return definitions;
        }

        private sealed class MemoryAudioDefinitions : IAudioDefinitionProvider
        {
            private readonly Dictionary<int, AudioEventDefinition> _events =
                new Dictionary<int, AudioEventDefinition>();
            private readonly Dictionary<int, AudioBusDefinition> _buses =
                new Dictionary<int, AudioBusDefinition>();

            public void AddBus(AudioBusDefinition definition)
            {
                _buses[definition.Id] = definition;
            }

            public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
            {
                return _events.TryGetValue(eventId, out definition);
            }

            public bool TryGetBus(int busId, out AudioBusDefinition definition)
            {
                return _buses.TryGetValue(busId, out definition);
            }

            public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
            {
                definition = default;
                return false;
            }
        }

        private sealed class FakeRebindingService : MxInput.IInputRebindingService
        {
            public bool IsRebinding { get; private set; }
            public string LastActionId { get; private set; }
            public int LastBindingIndex { get; private set; }
            public int StartCount { get; private set; }
            public int CancelCount { get; private set; }
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }
            public int ResetCount { get; private set; }

            public void StartRebind(string actionId, int bindingIndex)
            {
                LastActionId = actionId;
                LastBindingIndex = bindingIndex;
                StartCount++;
                IsRebinding = true;
            }

            public void Cancel()
            {
                CancelCount++;
                IsRebinding = false;
            }

            public void Save()
            {
                SaveCount++;
            }

            public void Load()
            {
                LoadCount++;
            }

            public void ResetToDefault()
            {
                ResetCount++;
            }
        }

        private sealed class FakeSettingsStore : IMxSettingsStore
        {
            public MxSettingsLoadResult LoadResult { get; set; } =
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault());

            public MxSettingsSaveResult SaveResult { get; set; } =
                MxSettingsSaveResult.Succeeded();

            public int LoadCount { get; private set; }
            public int SaveCount { get; private set; }
            public MxSettingsProfile SavedProfile { get; private set; }

            public MxSettingsLoadResult LoadOrDefault()
            {
                LoadCount++;
                return LoadResult;
            }

            public MxSettingsSaveResult Save(MxSettingsProfile profile)
            {
                SaveCount++;
                SavedProfile = profile;
                return SaveResult;
            }
        }

        private sealed class RecordingSettingsApplier : IMxSettingsApplier
        {
            public string ApplierId { get; set; } = "settings.graphics";
            public MxSettingsApplyResult Result { get; set; } =
                MxSettingsApplyResult.Succeeded();

            public int ApplyCount { get; private set; }
            public MxSettingsProfile LastProfile { get; private set; }

            public MxSettingsApplyResult Apply(MxSettingsProfile profile)
            {
                ApplyCount++;
                LastProfile = profile;
                return Result;
            }
        }

        private sealed class FakeGraphicsRuntime : IMxGraphicsRuntime
        {
            public readonly List<string> Calls = new List<string>();

            public MxWindowMode CurrentWindowMode { get; set; } = MxWindowMode.Windowed;
            public int QualityLevelCountValue { get; set; } = 4;
            public bool SupportsUniversalRenderPipelineAsset { get; set; } = true;
            public int QualityLevelCount => QualityLevelCountValue;

            public void SetResolution(int width, int height, MxWindowMode windowMode)
            {
                Calls.Add("resolution:" + width + "x" + height + ":" + windowMode);
            }

            public void SetWindowMode(MxWindowMode windowMode)
            {
                Calls.Add("windowMode:" + windowMode);
            }

            public void SetVSyncCount(int vSyncCount)
            {
                Calls.Add("vsync:" + vSyncCount);
            }

            public void SetTargetFrameRate(int targetFrameRate)
            {
                Calls.Add("targetFrameRate:" + targetFrameRate);
            }

            public void SetQualityLevel(int qualityLevel, bool applyExpensiveChanges)
            {
                Calls.Add("quality:" + qualityLevel + ":" + applyExpensiveChanges.ToString().ToLowerInvariant());
            }

            public bool TrySetRenderScale(float renderScale, out string message)
            {
                if (!SupportsUniversalRenderPipelineAsset)
                {
                    Calls.Add("renderScale:unsupported");
                    message = "No active UniversalRenderPipelineAsset is configured.";
                    return false;
                }

                Calls.Add("renderScale:" + FormatFloat(renderScale));
                message = string.Empty;
                return true;
            }

            public bool TrySetAntiAliasingSamples(int samples, out string message)
            {
                if (!SupportsUniversalRenderPipelineAsset)
                {
                    Calls.Add("aa:unsupported");
                    message = "No active UniversalRenderPipelineAsset is configured.";
                    return false;
                }

                Calls.Add("aa:" + samples);
                message = string.Empty;
                return true;
            }

            public bool TrySetShadowDistance(float shadowDistance, out string message)
            {
                if (!SupportsUniversalRenderPipelineAsset)
                {
                    Calls.Add("shadowDistance:unsupported");
                    message = "No active UniversalRenderPipelineAsset is configured.";
                    return false;
                }

                Calls.Add("shadowDistance:" + FormatFloat(shadowDistance));
                message = string.Empty;
                return true;
            }

            public bool TrySetMainLightShadowmapResolution(int resolution, out string message)
            {
                return RecordShadowSetting("mainShadowResolution", resolution, out message);
            }

            public bool TrySetShadowCascadeCount(int count, out string message)
            {
                return RecordShadowSetting("shadowCascades", count, out message);
            }

            public bool TrySetAdditionalLightsShadowmapResolution(int resolution, out string message)
            {
                return RecordShadowSetting("localShadowResolution", resolution, out message);
            }

            private bool RecordShadowSetting(string name, int value, out string message)
            {
                if (!SupportsUniversalRenderPipelineAsset)
                {
                    Calls.Add(name + ":unsupported");
                    message = "No active UniversalRenderPipelineAsset is configured.";
                    return false;
                }

                Calls.Add(name + ":" + value);
                message = string.Empty;
                return true;
            }

            private static string FormatFloat(float value)
            {
                return value.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
    }
}
