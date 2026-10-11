using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioMixSnapshotControllerTests
    {
        [Test]
        public void SetState_ReplacesControlledSnapshotAndReturnsToStandard()
        {
            var catalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(AudioMixBusIds.Master, "Master", "bus:/") },
                new[]
                {
                    Snapshot(AudioProductEventIds.PausedSnapshot, "paused"),
                    Snapshot(AudioProductEventIds.MenuSnapshot, "menu"),
                    Snapshot(AudioProductEventIds.CombatSnapshot, "combat"),
                    Snapshot(AudioProductEventIds.DialogueSnapshot, "dialogue")
                },
                null);
            using (var service = new AudioService(catalog, new NullAudioBackend()))
            using (var controller = new AudioMixSnapshotController(service))
            {
                Assert.IsTrue(controller.SetState(AudioMixState.Menu).Success);
                AudioHandle menu = controller.ActiveHandle;
                Assert.IsTrue(menu.IsValid);
                Assert.AreEqual(AudioProductEventIds.MenuSnapshot, menu.EventId);

                Assert.IsTrue(controller.SetState(AudioMixState.Dialogue).Success);
                Assert.IsFalse(service.IsHandleActive(menu));
                Assert.AreEqual(AudioProductEventIds.DialogueSnapshot, controller.ActiveHandle.EventId);

                Assert.IsTrue(controller.SetState(AudioMixState.Standard).Success);
                Assert.IsFalse(controller.ActiveHandle.IsValid);
                Assert.AreEqual(AudioMixState.Standard, controller.State);
            }
        }

        private static AudioEventDefinition Snapshot(int id, string name)
        {
            return new AudioEventDefinition(
                id,
                name,
                "snapshot:/" + name,
                string.Empty,
                AudioEventKind.Snapshot,
                AudioMixBusIds.Master,
                false,
                true,
                0f);
        }
    }
}
