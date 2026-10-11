using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Audio;
using MxFramework.Game.CharacterTest;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Settings.UI;
using NUnit.Framework;
using UnityEngine;
using MxInput = MxFramework.Input;

namespace MxFramework.Tests.Settings
{
    public sealed class SettingsUiModelTests
    {
        [Test]
        public void BuildViewModel_ExposesPlayerVisibleGraphicsItems()
        {
            var service = new MxSettingsService(
                null,
                new MxSettingsProfile(
                    "profile-ui",
                    graphics: new MxGraphicsSettings(
                        MxGraphicsQualityPreset.High,
                        targetFrameRate: 120,
                        renderScale: 1.25f)));
            var presenter = new MxSettingsUiPresenter(service);

            MxSettingsUiViewModel view = presenter.BuildViewModel();
            MxSettingsUiSectionViewModel graphics = FindSection(view, MxSettingsUiSectionKey.Graphics);

            Assert.IsFalse(view.Dirty);
            Assert.AreEqual("profile-ui", view.ProfileId);
            Assert.AreEqual(13, graphics.ItemCount);
            Assert.IsNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsQualityPreset));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsResolution));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsWindowMode));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsVSync));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsTargetFrameRate));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsRenderScale));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsAntiAliasingSamples));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsShadowDistance));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsMainLightShadowmapResolution));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsShadowCascadeCount));
            Assert.IsNotNull(FindItem(graphics, MxSettingsUiItemKey.GraphicsAdditionalLightsShadowmapResolution));
            Assert.AreEqual("120 fps", FindItem(graphics, MxSettingsUiItemKey.GraphicsTargetFrameRate).ValueText);
        }

        [Test]
        public void Setters_MarkDirtyAndSavePersistsWorkingProfile()
        {
            var store = new FakeSettingsStore();
            var service = new MxSettingsService(store);
            var presenter = new MxSettingsUiPresenter(service);

            presenter.SetRenderScale(1.5f);
            presenter.SetAntiAliasingSamples(4);
            presenter.SetShadowDistance(80f);
            presenter.SetMainLightShadowmapResolution(4096);
            presenter.SetShadowCascadeCount(3);
            presenter.SetAdditionalLightsShadowmapResolution(2048);
            presenter.SetAudioBusVolume(10, 0.25f);
            presenter.SetAudioBusMuted(10, true);
            presenter.SetLocalizationLocale("en-US");

            Assert.IsTrue(presenter.Dirty);
            MxSettingsUiOperationResult save = presenter.Save();

            Assert.IsTrue(save.Success, FirstIssue(save.Issues));
            Assert.IsFalse(presenter.Dirty);
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual(MxGraphicsQualityPreset.Custom, store.SavedProfile.Graphics.QualityPreset);
            Assert.AreEqual(1.5f, store.SavedProfile.Graphics.RenderScale);
            Assert.AreEqual(4, store.SavedProfile.Graphics.AntiAliasingSamples);
            Assert.AreEqual(80f, store.SavedProfile.Graphics.ShadowDistance);
            Assert.AreEqual(4096, store.SavedProfile.Graphics.MainLightShadowmapResolution);
            Assert.AreEqual(3, store.SavedProfile.Graphics.ShadowCascadeCount);
            Assert.AreEqual(2048, store.SavedProfile.Graphics.AdditionalLightsShadowmapResolution);
            Assert.AreEqual(10, store.SavedProfile.Audio.BusSettings[0].BusId);
            Assert.AreEqual(0.25f, store.SavedProfile.Audio.BusSettings[0].Volume);
            Assert.IsTrue(store.SavedProfile.Audio.BusSettings[0].Muted);
            Assert.AreEqual("en-US", store.SavedProfile.Localization.LocaleId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(store.SavedProfile.UpdatedUtc));
        }

        [Test]
        public void BuildViewModel_ExposesLocalizationLocaleOptions()
        {
            var service = new MxSettingsService(
                null,
                new MxSettingsProfile(localization: new MxLocalizationSettings("en-US")));
            var presenter = new MxSettingsUiPresenter(service);

            MxSettingsUiViewModel view = presenter.BuildViewModel();
            MxSettingsUiSectionViewModel localization =
                FindSection(view, MxSettingsUiSectionKey.Localization);

            Assert.AreEqual(1, localization.ItemCount);
            Assert.AreEqual(MxSettingsUiItemKey.LocalizationLocale, localization.Items[0].Key);
            Assert.AreEqual("localization.localeId", localization.Items[0].Id);
            Assert.AreEqual("en-US", localization.Items[0].Value);
            Assert.AreEqual("English", localization.Items[0].ValueText);
            Assert.GreaterOrEqual(localization.Items[0].Options.Count, 2);
        }

        [Test]
        public void Apply_UsesWorkingProfileWithoutClearingDirty()
        {
            var service = new MxSettingsService(null);
            var applier = new RecordingSettingsApplier();
            service.RegisterApplier(applier);
            var presenter = new MxSettingsUiPresenter(service);

            presenter.SetTargetFrameRate(144);
            MxSettingsUiOperationResult apply = presenter.Apply();

            Assert.IsTrue(apply.Success, FirstIssue(apply.Issues));
            Assert.IsTrue(presenter.Dirty);
            Assert.AreEqual(1, applier.ApplyCount);
            Assert.AreEqual(144, applier.LastProfile.Graphics.TargetFrameRate);
        }

        [Test]
        public void InputSection_ListsBindingsAndDelegatesRebind()
        {
            UnityEngine.InputSystem.InputActionAsset asset = MxInput.DefaultInputActionAssetFactory.Create();
            try
            {
                var rebinding = new FakeRebindingService();
                var inputSettings = new MxInputRebindingSettingsController(
                    rebinding,
                    new MxInput.InputBindingDisplayService(asset));
                var target = new MxInputBindingTarget(
                    MxInput.InputActionNames.GameplayMap + "/" + MxInput.InputActionNames.Jump,
                    0);
                var presenter = new MxSettingsUiPresenter(
                    new MxSettingsService(null),
                    inputSettings,
                    new[] { target });

                MxSettingsUiViewModel view = presenter.BuildViewModel();
                MxSettingsUiSectionViewModel input = FindSection(view, MxSettingsUiSectionKey.Input);

                Assert.AreEqual(1, input.ItemCount);
                Assert.AreEqual(MxSettingsUiItemKey.InputBinding, input.Items[0].Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(input.Items[0].ValueText));

                MxSettingsUiOperationResult rebind = presenter.StartInputRebind(target);

                Assert.IsTrue(rebind.Success, FirstIssue(rebind.Issues));
                Assert.AreEqual(1, rebinding.StartCount);
                Assert.AreEqual(target.ActionId, rebinding.LastActionId);
                Assert.AreEqual(target.BindingIndex, rebinding.LastBindingIndex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CharacterTestSettingsRuntime_ExposesSettingsUiPresenter()
        {
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                new MxSettingsService(null),
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()));

            Assert.IsNotNull(runtime.SettingsUi);
            MxSettingsUiViewModel view = runtime.SettingsUi.BuildViewModel();
            Assert.AreEqual(MxSettingsProfile.DefaultProfileId, view.ProfileId);
        }

        [Test]
        public void CharacterTestSettingsRuntime_HostCommandsUseSettingsUiWorkingProfile()
        {
            var store = new FakeSettingsStore();
            var service = new MxSettingsService(store);
            var applier = new RecordingSettingsApplier();
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                service,
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()),
                applier);

            runtime.SettingsUi.SetTargetFrameRate(144);
            MxSettingsApplyResult apply = runtime.ApplyCurrentProfile();

            Assert.AreSame(apply, runtime.LastApplyResult);
            Assert.IsTrue(apply.Success, FirstIssue(apply.Issues));
            Assert.AreEqual(1, applier.ApplyCount);
            Assert.AreEqual(144, applier.LastProfile.Graphics.TargetFrameRate);
            Assert.AreEqual(144, service.CurrentProfile.Graphics.TargetFrameRate);

            runtime.SettingsUi.SetRenderScale(1.5f);
            MxSettingsSaveResult save = runtime.SaveCurrentProfile();

            Assert.AreSame(save, runtime.LastSaveResult);
            Assert.IsTrue(save.Success, FirstIssue(save.Issues));
            Assert.IsFalse(runtime.SettingsUi.Dirty);
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual(1.5f, store.SavedProfile.Graphics.RenderScale);
        }

        [Test]
        public void CharacterTestSettingsRuntime_CreateProvidesDefaultInputBindingRows()
        {
            var ownerObject = new GameObject("SettingsRuntimeTestOwner");
            var owner = ownerObject.AddComponent<TestOwner>();
            string settingsFile = Path.Combine(
                Path.GetTempPath(),
                "mx-settings-ui-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                CharacterTestSettingsRuntime runtime =
                    CharacterTestSettingsRuntime.Create(owner, settingsFile);

                MxSettingsUiViewModel view = runtime.SettingsUi.BuildViewModel();
                MxSettingsUiSectionViewModel input =
                    FindSection(view, MxSettingsUiSectionKey.Input);

                Assert.AreEqual(21, runtime.InputBindingTargets.Count);
                Assert.AreEqual(
                    MxInput.InputActionNames.GameplayMap + "/" + MxInput.InputActionNames.TargetLock,
                    runtime.InputBindingTargets[14].ActionId);
                Assert.AreEqual(0, runtime.InputBindingTargets[14].BindingIndex);
                Assert.AreEqual(
                    MxInput.InputActionNames.GameplayMap + "/" + MxInput.InputActionNames.TargetLock,
                    runtime.InputBindingTargets[15].ActionId);
                Assert.AreEqual(1, runtime.InputBindingTargets[15].BindingIndex);
                Assert.AreEqual(
                    MxInput.InputActionNames.GameplayMap + "/" + MxInput.InputActionNames.TargetLock,
                    runtime.InputBindingTargets[16].ActionId);
                Assert.AreEqual(2, runtime.InputBindingTargets[16].BindingIndex);
                Assert.AreEqual(
                    MxInput.InputActionNames.UIMap + "/" + MxInput.InputActionNames.Cancel,
                    runtime.InputBindingTargets[20].ActionId);
                Assert.AreEqual(runtime.InputBindingTargets.Count, input.ItemCount, FirstIssue(view.Issues));
                Assert.IsFalse(view.HasIssues, FirstIssue(view.Issues));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
                if (File.Exists(settingsFile))
                    File.Delete(settingsFile);
            }
        }

        [Test]
        public void CharacterTestSettingsRuntime_PreservesDynamicInputTargetCatalog()
        {
            var targets = new MxInputBindingTarget[32];
            for (int i = 0; i < targets.Length; i++)
                targets[i] = new MxInputBindingTarget("Gameplay/Jump", 0);

            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                new MxSettingsService(null),
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()),
                inputBindingTargets: targets);

            Assert.AreEqual(targets.Length, runtime.InputBindingTargets.Count);
        }

        [Test]
        public void CharacterTestSettingsPanelViewModel_ProjectsDeviceSpecificDualSlots()
        {
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                new MxSettingsService(null),
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()),
                inputBindingTargets: CharacterTestSettingsRuntime.CreateDefaultInputBindingTargets());

            CharacterTestFairyGuiSettingsPanelViewModel keyboard =
                CharacterTestFairyGuiSettingsPanelViewModelBuilder.Build(
                    runtime,
                    CharacterTestSettingsPage.Controls,
                    CharacterTestSettingsInputDevicePage.KeyboardMouse,
                    default);
            CharacterTestFairyGuiSettingsPanelViewModel gamepad =
                CharacterTestFairyGuiSettingsPanelViewModelBuilder.Build(
                    runtime,
                    CharacterTestSettingsPage.Controls,
                    CharacterTestSettingsInputDevicePage.Gamepad,
                    default);

            Assert.AreEqual(12, keyboard.InputRows.Count);
            Assert.IsTrue(keyboard.InputRows[0].IsSection);
            Assert.AreEqual("Gameplay/TargetLock", keyboard.InputRows[8].PrimaryTarget.ActionId);
            Assert.AreEqual(0, keyboard.InputRows[8].PrimaryTarget.BindingIndex);
            Assert.AreEqual(1, keyboard.InputRows[8].SecondaryTarget.BindingIndex);
            Assert.AreEqual(2, gamepad.InputRows[8].PrimaryTarget.BindingIndex);
            Assert.IsTrue(string.IsNullOrEmpty(gamepad.InputRows[8].SecondaryTarget.ActionId));
            Assert.AreEqual("—", gamepad.InputRows[8].SecondaryValue);
        }

        [Test]
        public void CharacterTestSettingsRuntime_DefaultAudioBus_IsVisibleWithoutDirty()
        {
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                new MxSettingsService(null),
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()));

            MxSettingsUiViewModel view = runtime.SettingsUi.BuildViewModel();
            MxSettingsUiSectionViewModel audio = FindSection(view, MxSettingsUiSectionKey.Audio);

            Assert.IsFalse(runtime.SettingsUi.Dirty);
            Assert.IsFalse(view.Dirty);
            Assert.AreEqual(12, audio.ItemCount);
            Assert.AreEqual(
                "audio.bus." + CharacterTestAudioCueCatalog.DefaultBusId + ".volume",
                audio.Items[0].Id);
            Assert.AreEqual("1", audio.Items[0].ValueText);
            Assert.AreEqual(
                "audio.bus." + CharacterTestAudioCueCatalog.DefaultBusId + ".muted",
                audio.Items[1].Id);
            Assert.AreEqual("On", audio.Items[1].ValueText);
        }

        [Test]
        public void CharacterTestSettingsRuntime_AttachAudioService_RegistersMutableAudioApplier()
        {
            var service = new MxSettingsService(null);
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                service,
                new MxInputRebindingSettingsController(null, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()));
            var firstAudio = new RecordingAudioService();
            var secondAudio = new RecordingAudioService();

            runtime.AttachAudioService(firstAudio);
            Assert.AreEqual(6, firstAudio.SetVolumeCount);
            Assert.AreEqual(1f, firstAudio.MasterVolume);
            Assert.AreEqual(6, firstAudio.SetMutedCount);
            Assert.IsFalse(firstAudio.MasterMuted);
            runtime.SettingsUi.SetAudioBusVolume(CharacterTestAudioCueCatalog.DefaultBusId, 0.25f);
            runtime.SettingsUi.SetAudioBusMuted(CharacterTestAudioCueCatalog.DefaultBusId, false);
            MxSettingsApplyResult firstApply = runtime.ApplyCurrentProfile();

            Assert.IsTrue(firstApply.Success, FirstIssue(firstApply.Issues));
            Assert.AreEqual(1, service.Appliers.Count);
            Assert.AreSame(runtime.AudioSettingsApplier, service.Appliers[0]);
            Assert.AreEqual(12, firstAudio.SetVolumeCount);
            Assert.AreEqual(0.25f, firstAudio.MasterVolume);
            Assert.AreEqual(12, firstAudio.SetMutedCount);
            Assert.IsFalse(firstAudio.MasterMuted);

            runtime.AttachAudioService(secondAudio);
            Assert.AreEqual(6, secondAudio.SetVolumeCount);
            Assert.AreEqual(0.25f, secondAudio.MasterVolume);
            Assert.AreEqual(6, secondAudio.SetMutedCount);
            Assert.IsFalse(secondAudio.MasterMuted);
            runtime.SettingsUi.SetAudioBusVolume(CharacterTestAudioCueCatalog.DefaultBusId, 0.75f);
            runtime.SettingsUi.SetAudioBusMuted(CharacterTestAudioCueCatalog.DefaultBusId, true);
            MxSettingsApplyResult secondApply = runtime.ApplyCurrentProfile();

            Assert.IsTrue(secondApply.Success, FirstIssue(secondApply.Issues));
            Assert.AreEqual(1, service.Appliers.Count);
            Assert.AreEqual(12, firstAudio.SetVolumeCount);
            Assert.AreEqual(12, firstAudio.SetMutedCount);
            Assert.AreEqual(12, secondAudio.SetVolumeCount);
            Assert.AreEqual(0.75f, secondAudio.MasterVolume);
            Assert.AreEqual(12, secondAudio.SetMutedCount);
            Assert.IsTrue(secondAudio.MasterMuted);
        }

        private static MxSettingsUiSectionViewModel FindSection(
            MxSettingsUiViewModel view,
            MxSettingsUiSectionKey key)
        {
            for (int i = 0; i < view.Sections.Count; i++)
            {
                if (view.Sections[i].Key == key)
                    return view.Sections[i];
            }

            Assert.Fail("Section not found: " + key);
            return null;
        }

        private static MxSettingsUiItemViewModel FindItem(
            MxSettingsUiSectionViewModel section,
            MxSettingsUiItemKey key)
        {
            for (int i = 0; i < section.Items.Count; i++)
            {
                if (section.Items[i].Key == key)
                    return section.Items[i];
            }

            return null;
        }

        private static string FirstIssue(IReadOnlyList<MxSettingsIssue> issues)
        {
            return issues != null && issues.Count > 0 ? issues[0].ToString() : string.Empty;
        }

        private sealed class FakeSettingsStore : IMxSettingsStore
        {
            public int SaveCount { get; private set; }
            public MxSettingsProfile SavedProfile { get; private set; }

            public MxSettingsLoadResult LoadOrDefault()
            {
                return MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault());
            }

            public MxSettingsSaveResult Save(MxSettingsProfile profile)
            {
                SaveCount++;
                SavedProfile = profile;
                return MxSettingsSaveResult.Succeeded();
            }
        }

        private sealed class RecordingSettingsApplier : IMxSettingsApplier
        {
            public string ApplierId => "test.applier";
            public int ApplyCount { get; private set; }
            public MxSettingsProfile LastProfile { get; private set; }

            public MxSettingsApplyResult Apply(MxSettingsProfile profile)
            {
                ApplyCount++;
                LastProfile = profile;
                return MxSettingsApplyResult.Succeeded();
            }
        }

        private sealed class FakeRebindingService : MxInput.IInputRebindingService
        {
            public bool IsRebinding { get; private set; }
            public int StartCount { get; private set; }
            public string LastActionId { get; private set; }
            public int LastBindingIndex { get; private set; }

            public void StartRebind(string actionId, int bindingIndex)
            {
                StartCount++;
                LastActionId = actionId;
                LastBindingIndex = bindingIndex;
                IsRebinding = true;
            }

            public void Cancel()
            {
                IsRebinding = false;
            }

            public void Save()
            {
            }

            public void Load()
            {
            }

            public void ResetToDefault()
            {
            }
        }

        private sealed class TestOwner : MonoBehaviour
        {
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public int SetVolumeCount { get; private set; }
            public int SetMutedCount { get; private set; }
            public int LastBusId { get; private set; }
            public float LastVolume { get; private set; }
            public bool LastMuted { get; private set; }
            public float MasterVolume { get; private set; }
            public bool MasterMuted { get; private set; }

            public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
            {
                return AudioPlayResult.Ok(AudioHandle.Invalid);
            }

            public AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle)
            {
                handle = AudioHandle.Invalid;
                return AudioPlayResult.Ok(handle);
            }

            public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetBusVolume(int busId, float volume)
            {
                SetVolumeCount++;
                LastBusId = busId;
                LastVolume = volume;
                if (busId == AudioMixBusIds.Master)
                    MasterVolume = volume;
                return AudioResult.Ok();
            }

            public AudioResult SetBusMuted(int busId, bool muted)
            {
                SetMutedCount++;
                LastBusId = busId;
                LastMuted = muted;
                if (busId == AudioMixBusIds.Master)
                    MasterMuted = muted;
                return AudioResult.Ok();
            }

            public AudioDebugSnapshot CaptureSnapshot()
            {
                return new AudioDebugSnapshot(
                    initialized: true,
                    totalPlayRequests: 0,
                    totalStopRequests: 0,
                    activeEventCount: 0,
                    activeEvents: null,
                    busStates: null,
                    recentErrors: null);
            }

            public void Tick(float deltaTime)
            {
            }
        }
    }
}
