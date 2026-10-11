using MxFramework.Audio;
using MxFramework.Audio.Diagnostics;
using MxFramework.Diagnostics;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioDebugSourceTests
    {
        [Test]
        public void CreateSnapshot_ExportsBackendAndPresentationSections()
        {
            var catalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(10, "SFX", "bus:/SFX") },
                new[] { new AudioEventDefinition(1, "click", "event:/click", string.Empty, AudioEventKind.Event, 10, false, false, 0f) },
                null);
            using (var service = new AudioService(catalog, new NullAudioBackend()))
            {
                var drain = new AudioPresentationDrain();
                var buffer = new FixedAudioCommandBuffer(1);
                buffer.TryEnqueue(new AudioCommand(AudioCommandKind.PlayOneShot, AudioPlayRequest.Create2D(1, frame: 1)));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);

                FrameworkDebugSnapshot snapshot = new AudioDebugSource(service, drain).CreateSnapshot();

                Assert.AreEqual("Audio", snapshot.SourceName);
                Assert.AreEqual(8, snapshot.Sections.Count);
                StringAssert.Contains("plays=1", snapshot.Sections[0].Body);
                Assert.AreEqual("Banks", snapshot.Sections[2].Title);
                Assert.AreEqual("Listeners", snapshot.Sections[3].Title);
                StringAssert.Contains("accepted=1", snapshot.Sections[5].Body);
                StringAssert.Contains("scopes(frame/entity/scene/app)=0/0/0/0", snapshot.Sections[5].Body);
                Assert.AreEqual("Recent Decisions", snapshot.Sections[7].Title);
            }
        }
    }
}
