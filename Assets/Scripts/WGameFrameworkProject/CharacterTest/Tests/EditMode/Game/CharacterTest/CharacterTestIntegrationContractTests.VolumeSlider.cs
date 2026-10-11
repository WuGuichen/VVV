using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.UI;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {
        [Test]
        public void SettingsVolumeSlider_DraftsRemainPerBusAndSaveOnlyOnApply()
        {
            var runtime = new CharacterTestSettingsRuntime("settings.json",
                new MxSettingsService(new RecordingSettingsStore()),
                new MxInputRebindingSettingsController(new RecordingInputRebindingService(), null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()), new RecordingSettingsApplier());
            var sink = new CharacterTestFairyGuiSettingsPanelCommandSink(runtime);
            int master = runtime.SelectedAudioBusId;
            sink.Enqueue(new MxUiCommand(CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandSink.VolumeDraftCommand, new CharacterTestSettingsVolumeDraft(master, 0.37f)));
            Assert.IsTrue(runtime.SettingsUi.Dirty);
            Assert.AreEqual(0, sink.ApplyCount);
            Assert.AreEqual(0, sink.SaveCount);
            sink.Enqueue(new MxUiCommand(CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryNext, null));
            int other = runtime.SelectedAudioBusId;
            Assert.AreNotEqual(master, other);
            sink.Enqueue(new MxUiCommand(CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandSink.VolumeDraftCommand, new CharacterTestSettingsVolumeDraft(master, 0.99f)));
            sink.Enqueue(new MxUiCommand(CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandSink.VolumeDraftCommand, new CharacterTestSettingsVolumeDraft(other, 0.62f)));
            Assert.AreEqual(0.37f, runtime.CurrentProfile.Audio.BusSettings.Single(x => x.BusId == master).Volume);
            Assert.AreEqual(0.62f, runtime.CurrentProfile.Audio.BusSettings.Single(x => x.BusId == other).Volume);
            sink.Enqueue(new MxUiCommand(CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.Apply, null));
            Assert.IsTrue(sink.LastApplyResult.Success);
            Assert.IsTrue(sink.LastSaveResult.Success);
            Assert.AreEqual(1, sink.SaveCount);
        }

        [Test]
        public void SettingsVolumeSlider_RejectsNonFiniteClampsAndCancelRestores()
        {
            var runtime = new CharacterTestSettingsRuntime("settings.json",
                new MxSettingsService(new RecordingSettingsStore()),
                new MxInputRebindingSettingsController(new RecordingInputRebindingService(), null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()), new RecordingSettingsApplier());
            var sink = new CharacterTestFairyGuiSettingsPanelCommandSink(runtime, () => true);
            int bus = runtime.SelectedAudioBusId;
            float initial = runtime.CurrentProfile.Audio.BusSettings.Single(x => x.BusId == bus).Volume;
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity })
                sink.Enqueue(new MxUiCommand(CharacterTestFairyGuiSettingsPanelIds.ViewId,
                    CharacterTestFairyGuiSettingsPanelCommandSink.VolumeDraftCommand, new CharacterTestSettingsVolumeDraft(bus, value)));
            Assert.AreEqual(0f, runtime.CurrentProfile.Audio.BusSettings.Single(x => x.BusId == bus).Volume);
            sink.Enqueue(new MxUiCommand(CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.CancelChanges, null));
            Assert.AreEqual(initial, runtime.CurrentProfile.Audio.BusSettings.Single(x => x.BusId == bus).Volume);
            Assert.AreEqual(0, sink.SaveCount);
        }
    }
}
