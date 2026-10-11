using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioPersistentEventControllerTests
    {
        [Test]
        public void TransitionTo_ReplacesAndStopsOwnedEvent()
        {
            var catalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(610000, "Ambience", "bus:/Ambience") },
                new[]
                {
                    Event(610001, "wind"),
                    Event(610002, "rain")
                },
                null);
            using (var service = new AudioService(catalog, new NullAudioBackend()))
            using (var controller = new AudioPersistentEventController(service))
            {
                Assert.IsTrue(controller.TransitionTo(610001, new AudioTraceIdentity(960001)).Success);
                AudioHandle wind = controller.ActiveHandle;
                Assert.IsTrue(wind.IsValid);

                Assert.IsTrue(controller.TransitionTo(610002, new AudioTraceIdentity(960002)).Success);
                Assert.IsFalse(service.IsHandleActive(wind));
                Assert.AreEqual(610002, controller.EventId);

                Assert.IsTrue(controller.TransitionTo(0, AudioTraceIdentity.None).Success);
                Assert.AreEqual(0, controller.EventId);
                Assert.IsFalse(controller.ActiveHandle.IsValid);
            }
        }

        private static AudioEventDefinition Event(int id, string name)
        {
            return new AudioEventDefinition(
                id,
                name,
                "event:/Ambience/" + name,
                string.Empty,
                AudioEventKind.Event,
                610000,
                false,
                true,
                0f);
        }
    }
}
