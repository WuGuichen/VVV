using System;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class ContinuousPhysicalAudioSystemTests
    {
        [Test]
        public void Scrape_StartedStayedEnded_OwnsOneMovingHandleWithoutAllocatingOnUpdate()
        {
            AudioDefinitionCatalog audio = CreateAudioCatalog();
            using (var service = new AudioService(audio, new NullAudioBackend()))
            {
                var system = new ContinuousPhysicalAudioSystem(
                    service,
                    audio,
                    new PhysicalAudioResolver(CreatePhysicalCatalog()),
                    voiceCapacity: 4,
                    parameterCapacity: 0);

                Assert.IsTrue(system.Process(CreateFact(PhysicalAudioInteractionPhase.Started, 0f)).Success);
                Assert.AreEqual(1, system.Snapshot.Active);

                PhysicalAudioInteractionFact stayed = CreateFact(PhysicalAudioInteractionPhase.Stayed, 2f);
                Assert.IsTrue(system.Process(stayed).Success);
                long before = GC.GetAllocatedBytesForCurrentThread();
                Assert.IsTrue(system.Process(stayed).Success);
                Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);

                Assert.IsTrue(system.Process(CreateFact(PhysicalAudioInteractionPhase.Ended, 2f)).Success);
                Assert.AreEqual(0, system.Snapshot.Active);
                Assert.AreEqual(1, system.Snapshot.Started);
                Assert.AreEqual(2, system.Snapshot.Updated);
                Assert.AreEqual(1, system.Snapshot.Stopped);
            }
        }

        private static AudioDefinitionCatalog CreateAudioCatalog()
        {
            return new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(10, "Physical", "bus:/SFX/Physical") },
                new[] { new AudioEventDefinition(20, "Scrape Loop", "event:/Scrape", string.Empty, AudioEventKind.Event, 10, true, true, 20f) },
                new[] { new AudioCueDefinition(30, "physical.scrape", 20, AudioCuePlayMode.StartEvent) });
        }

        private static PhysicalAudioDefinitionCatalog CreatePhysicalCatalog()
        {
            var weights = new PhysicalAudioSelectionWeights(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
            var family = new PhysicalAudioFamilyDefinition(
                new PhysicalAudioFamilyId(40),
                PhysicalAudioInteractionKind.Scrape,
                PhysicalAudioInteractionMode.Scrape,
                PhysicalAudioVariantKind.Scrape,
                PhysicalAudioFamilyId.None,
                AudioBasisId.None,
                PhysicalAudioResponseProfileId.None,
                priority: 5,
                cooldownFrames: 0,
                weights);
            var range = new PhysicalAudioRange(0f, 10f, 1f);
            var anchor = new PhysicalAudioBodyAnchorDefinition(
                AcousticMaterialId.None,
                range,
                range,
                AcousticSurfaceKind.Any,
                AcousticConstructionKind.Any);
            var basis = new PhysicalAudioBasisDefinition(
                new AudioBasisId(50),
                family.Id,
                cueId: 30,
                priority: 5,
                anchor,
                anchor,
                range,
                new PhysicalAudioRange(0f, 1f, 0.5f),
                new PhysicalAudioRange(0f, 180f, 90f));
            return new PhysicalAudioDefinitionCatalog(null, new[] { family }, new[] { basis }, null);
        }

        private static PhysicalAudioInteractionFact CreateFact(PhysicalAudioInteractionPhase phase, float x)
        {
            var body = new AcousticBodyProperties(AcousticMaterialId.None, 1f, 1f);
            return new PhysicalAudioInteractionFact(
                PhysicalAudioInteractionKind.Scrape,
                PhysicalAudioInteractionMode.Scrape,
                phase,
                body,
                body,
                relativeSpeed: 2f,
                normalSpeed: 0.2f,
                tangentialSpeed: 1.8f,
                pressure: 0.5f,
                impulse: 0.1f,
                angleDegrees: 90f,
                intensity: 0.6f,
                new AudioTransform(x, 0f, 0f, 0f, 0f, 1f),
                emitterId: 77,
                frame: 100,
                sequence: 9001);
        }
    }
}
