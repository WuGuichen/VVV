using System;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAirDisplacementAudioSystemTests
    {
        [Test]
        public void LightAttackPresentationPeakBuildsKatanaAgainstAirFact()
        {
            CharacterTestCharacterRuntimeWorld world = CreateWorld(out CharacterTestCharacterEntity entity);
            CharacterActionAudioCueRequest request = CreateRequest(localFrame: 26);

            bool success = CharacterTestAirDisplacementFactBuilder.TryCreate(
                entity,
                request,
                CharacterTestAudioCueRequestClassifier.Classify(request),
                new RuntimeFrame(100),
                out PhysicalAudioInteractionFact fact);

            Assert.IsTrue(success);
            Assert.AreEqual(PhysicalAudioInteractionKind.AirDisplacement, fact.Kind);
            Assert.AreEqual(PhysicalAudioInteractionMode.Sweep, fact.Mode);
            Assert.AreEqual(CharacterTestAcousticMaterialMapper.Metal, fact.Impactor.MaterialId);
            Assert.AreEqual(CharacterTestAcousticMaterialMapper.Air, fact.Receiver.MaterialId);
            Assert.AreEqual(AcousticConstructionKind.Gas, fact.Receiver.Construction);
            Assert.AreEqual(18f, fact.RelativeSpeed, 0.001f);
            Assert.AreEqual(18f, fact.TangentialSpeed, 0.001f);
            Assert.AreEqual(100, fact.Frame);
            Assert.AreEqual(77, fact.Sequence);
            Assert.AreEqual(entity.Id.Value, fact.EmitterId);
            Assert.AreEqual(fact.Transform.ForwardX, fact.ContactNormal.X, 0.0001f);
            Assert.AreEqual(fact.Transform.ForwardY, fact.ContactNormal.Y, 0.0001f);
            Assert.AreEqual(fact.Transform.ForwardZ, fact.ContactNormal.Z, 0.0001f);
        }

        [Test]
        public void GeneratedCatalogConsumesPresentationAndQueuesResolvedNumericEvent()
        {
            CharacterTestCharacterRuntimeWorld world = CreateWorld(out CharacterTestCharacterEntity entity);
            CharacterTestAudioCueCatalog catalog = CreateGeneratedCatalog();
            var playback = new CharacterTestAudioCuePlaybackSystem(
                null,
                catalog,
                NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(4);
            var system = new CharacterTestAirDisplacementAudioSystem(playback, queue);
            entity.EnqueueAudioCueRequest(CreateRequest(localFrame: 26));

            system.Tick(world, new RuntimeFrame(100));

            Assert.AreEqual(0, entity.PendingAudioCueRequestCount);
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(0, queue[0].CueId);
            Assert.AreEqual(600101, queue[0].EventId);
            Assert.AreEqual(2, queue[0].ParameterCount);
            Assert.AreEqual(-7, queue[0].Priority);
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
            Assert.AreEqual(530101, system.LastSummary.LastBasisId);
        }

        [Test]
        public void BufferedAirPresentationUsesStableEventClassificationInsteadOfActionStringDuringTick()
        {
            CharacterTestCharacterRuntimeWorld world = CreateWorld(out CharacterTestCharacterEntity entity);
            CharacterTestAudioCueCatalog catalog = CreateGeneratedCatalog();
            var playback = new CharacterTestAudioCuePlaybackSystem(null, catalog, NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(4);
            var system = new CharacterTestAirDisplacementAudioSystem(playback, queue);
            entity.EnqueueAudioCueRequest(CreateRequest(
                localFrame: 26,
                actionId: new CharacterActionId(999999),
                stableEventId: CharacterTestKatanaAirDisplacementProfile.FirstAttackStableEventId));

            system.Tick(world, new RuntimeFrame(100));

            Assert.AreEqual(0, entity.PendingAudioCueRequestCount);
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, system.LastSummary.PlayedCount);
        }

        [Test]
        public void MatchingCueAndActionStringsWithoutStableAirEventRemainOnGenericPath()
        {
            CharacterTestCharacterRuntimeWorld world = CreateWorld(out CharacterTestCharacterEntity entity);
            CharacterTestAudioCueCatalog catalog = CreateGeneratedCatalog();
            var playback = new CharacterTestAudioCuePlaybackSystem(null, catalog, NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(4);
            var system = new CharacterTestAirDisplacementAudioSystem(playback, queue);
            entity.EnqueueAudioCueRequest(CreateRequest(
                localFrame: 26,
                stableEventId: int.MaxValue));

            system.Tick(world, new RuntimeFrame(100));

            Assert.AreEqual(1, entity.PendingAudioCueRequestCount);
            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, system.LastSummary.ProcessedCount);
        }

        [Test]
        public void LegacyCatalogLeavesPresentationForGenericCuePath()
        {
            CharacterTestCharacterRuntimeWorld world = CreateWorld(out CharacterTestCharacterEntity entity);
            var playback = new CharacterTestAudioCuePlaybackSystem(
                null,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(4);
            var system = new CharacterTestAirDisplacementAudioSystem(playback, queue);
            entity.EnqueueAudioCueRequest(CreateRequest(localFrame: 25));

            system.Tick(world, new RuntimeFrame(100));

            Assert.IsFalse(system.LastSummary.PhysicalCatalogActive);
            Assert.AreEqual(1, entity.PendingAudioCueRequestCount);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void GeneratedCatalogWarmTickDoesNotAllocate()
        {
            CharacterTestCharacterRuntimeWorld world = CreateWorld(out CharacterTestCharacterEntity entity);
            CharacterTestAudioCueCatalog catalog = CreateGeneratedCatalog();
            var playback = new CharacterTestAudioCuePlaybackSystem(null, catalog, NullRuntimeLogger.Instance);
            var queue = new CharacterTestAudioIntentQueue(4);
            var system = new CharacterTestAirDisplacementAudioSystem(playback, queue);

            entity.EnqueueAudioCueRequest(CreateRequest(localFrame: 26));
            system.Tick(world, new RuntimeFrame(1));
            queue.Clear();
            entity.EnqueueAudioCueRequest(CreateRequest(localFrame: 26));
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            system.Tick(world, new RuntimeFrame(2));

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(1, queue.Count);
        }

        private static CharacterActionAudioCueRequest CreateRequest(
            int localFrame,
            CharacterActionId actionId = default,
            int stableEventId = CharacterTestKatanaAirDisplacementProfile.FirstAttackStableEventId)
        {
            return new CharacterActionAudioCueRequest(
                new CharacterActionAdapterDispatchMetadata(
                    new RuntimeFrame(localFrame),
                    instanceId: 77,
                    planId: 88,
                    actionId.IsValid
                        ? actionId
                        : CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    localFrame,
                    stableEventId,
                    sourceId: 1,
                    traceId: new RuntimeTraceIdentity(930037)),
                "1",
                CharacterTestAudioCueCatalog.LightAttackAirDisplacementCueId);
        }

        private static CharacterTestCharacterRuntimeWorld CreateWorld(out CharacterTestCharacterEntity entity)
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out CharacterTestCharacterDefinition definition));
            entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 1f,
                    y: 2f,
                    z: 3f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            return world;
        }

        private static CharacterTestAudioCueCatalog CreateGeneratedCatalog()
        {
            const int busId = 600100;
            const int cueId = 600101;
            const int eventId = 600101;
            var parameters = new[]
            {
                new AudioParameterDefinition(500251, "MxPhysicalVolume", AudioParameterKind.Continuous, 0.5f, 0f, 1f),
                new AudioParameterDefinition(500252, "MxPhysicalPitch", AudioParameterKind.Continuous, 1f, 0.5f, 1.6f)
            };
            var audioCatalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(busId, "generated", "bus:/") },
                new[]
                {
                    new AudioEventDefinition(
                        eventId,
                        "generated.air",
                        "event:/MxFramework/Generated/Physical/AirDisplacement/530101",
                        string.Empty,
                        AudioEventKind.Event,
                        busId,
                        is3D: true,
                        isLoop: false,
                        maxDistance: 24f,
                        parameters)
                },
                new[] { new AudioCueDefinition(cueId, "sfx.generated.air", eventId, AudioCuePlayMode.OneShot) },
                "audio-hash",
                "snapshot-hash");
            var responseId = new PhysicalAudioResponseProfileId(540001);
            var response = new PhysicalAudioResponseProfileDefinition(
                responseId,
                new[]
                {
                    CreateCurve(500251, 0.2f, 1f),
                    CreateCurve(500252, 0.8f, 1.2f)
                });
            var unit = new PhysicalAudioRange(0f, 1f, 0.5f);
            var physicalCatalog = new PhysicalAudioDefinitionCatalog(
                new[]
                {
                    new AcousticMaterialProfileDefinition(CharacterTestAcousticMaterialMapper.Metal, 0.9f, 0.8f, 0.2f, 0.2f, 0.8f),
                    new AcousticMaterialProfileDefinition(CharacterTestAcousticMaterialMapper.Air, 0f, 0f, 0f, 0f, 0f)
                },
                new[]
                {
                    new PhysicalAudioFamilyDefinition(
                        new PhysicalAudioFamilyId(520006),
                        PhysicalAudioInteractionKind.AirDisplacement,
                        PhysicalAudioInteractionMode.Sweep,
                        PhysicalAudioFamilyId.None,
                        AudioBasisId.None,
                        responseId,
                        priority: 15,
                        cooldownFrames: 2,
                        new PhysicalAudioSelectionWeights(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f))
                },
                new[]
                {
                    new PhysicalAudioBasisDefinition(
                        new AudioBasisId(530101),
                        new PhysicalAudioFamilyId(520006),
                        cueId,
                        priority: -7,
                        new PhysicalAudioBodyAnchorDefinition(CharacterTestAcousticMaterialMapper.Metal, unit, unit, AcousticSurfaceKind.Smooth, AcousticConstructionKind.Solid),
                        new PhysicalAudioBodyAnchorDefinition(CharacterTestAcousticMaterialMapper.Air, unit, unit, AcousticSurfaceKind.Any, AcousticConstructionKind.Gas),
                        new PhysicalAudioRange(2f, 80f, 18f),
                        unit,
                        new PhysicalAudioRange(0f, 180f, 45f))
                },
                new[] { response },
                "physical-hash",
                "audio-hash");
            return CharacterTestAudioCueCatalog.FromRuntimeCatalog(audioCatalog, physicalCatalog);
        }

        private static PhysicalAudioResponseCurveDefinition CreateCurve(
            int parameterId,
            float min,
            float max)
        {
            return new PhysicalAudioResponseCurveDefinition(
                parameterId,
                PhysicalAudioResponseInput.Intensity,
                min,
                max,
                new[]
                {
                    new PhysicalAudioResponsePoint(0f, min),
                    new PhysicalAudioResponsePoint(1f, max)
                });
        }
    }
}
