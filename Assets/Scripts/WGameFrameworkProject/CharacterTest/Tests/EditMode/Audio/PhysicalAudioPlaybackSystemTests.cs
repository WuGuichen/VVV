using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class PhysicalAudioPlaybackSystemTests
    {
        [Test]
        public void Tick_StartedContact_ResolvesRuleAndPlays3DRequest()
        {
            var audioService = new RecordingAudioService();
            var system = new PhysicalAudioPlaybackSystem(audioService, CreateRuleTable(), cooldownCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(4);
            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Rough, CombatPhysicalContactPhase.Started));

            PhysicalAudioPlaybackSummary summary = system.Tick(buffer, frame: 12);

            Assert.AreEqual(1, summary.PlayedCount);
            Assert.AreEqual(1, summary.BodyPartContactPlayedCount);
            Assert.AreEqual(1002, audioService.LastRequest.EventId);
            Assert.AreEqual(
                CreateStableBodyPartEmitterId(sourceEntityId: 10, contactPointId: 1, bodyPartId: 701),
                audioService.LastRequest.EmitterId);
            Assert.AreEqual(1.25f, audioService.LastRequest.Transform.X, 0.0001f);
            Assert.AreEqual(0.5f, audioService.LastRequest.Transform.Y, 0.0001f);
            Assert.AreEqual(-0.75f, audioService.LastRequest.Transform.Z, 0.0001f);
        }

        [Test]
        public void Tick_CooldownBlocksSamePairUntilElapsed()
        {
            var audioService = new RecordingAudioService();
            var system = new PhysicalAudioPlaybackSystem(audioService, CreateRuleTable(), cooldownCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(4);

            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Rough, CombatPhysicalContactPhase.Started));
            system.Tick(buffer, frame: 10);
            buffer.Clear();
            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Rough, CombatPhysicalContactPhase.Started));
            PhysicalAudioPlaybackSummary blocked = system.Tick(buffer, frame: 14);
            buffer.Clear();
            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Rough, CombatPhysicalContactPhase.Started));
            PhysicalAudioPlaybackSummary elapsed = system.Tick(buffer, frame: 18);

            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(1, blocked.CooldownSkipCount);
            Assert.AreEqual(1, elapsed.PlayedCount);
        }

        [Test]
        public void Tick_MissingRuleOrServiceSkipsWithoutThrowing()
        {
            var buffer = new CombatPhysicalContactEventBuffer(4);
            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Elastic, CombatPhysicalContactPhase.Started));
            var missingRuleSystem = new PhysicalAudioPlaybackSystem(
                new RecordingAudioService(),
                PhysicalAudioCueRuleTable.Empty,
                cooldownCapacity: 1);

            PhysicalAudioPlaybackSummary missingRule = missingRuleSystem.Tick(buffer, frame: 1);

            Assert.AreEqual(1, missingRule.MissingRuleSkipCount);

            var missingServiceSystem = new PhysicalAudioPlaybackSystem(
                null,
                CreateRuleTable(),
                cooldownCapacity: 1);
            buffer.Clear();
            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Rough, CombatPhysicalContactPhase.Started));

            PhysicalAudioPlaybackSummary missingService = missingServiceSystem.Tick(buffer, frame: 2);

            Assert.AreEqual(1, missingService.MissingAudioServiceSkipCount);
        }

        [Test]
        public void Tick_StayedContactIsIgnored()
        {
            var audioService = new RecordingAudioService();
            var system = new PhysicalAudioPlaybackSystem(audioService, CreateRuleTable(), cooldownCapacity: 1);
            var buffer = new CombatPhysicalContactEventBuffer(4);
            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Rough, CombatPhysicalContactPhase.Stayed));

            PhysicalAudioPlaybackSummary summary = system.Tick(buffer, frame: 1);

            Assert.AreEqual(0, audioService.PlayOneShotCount);
            Assert.AreEqual(1, summary.IgnoredPhaseCount);
        }

        [Test]
        public void Tick_CooldownCapacityPreservesActiveKeyThenReusesExpiredSlotWithoutAllocation()
        {
            var audioService = new RecordingAudioService();
            var system = new PhysicalAudioPlaybackSystem(audioService, CreateRuleTable(), cooldownCapacity: 1);
            var buffer = new CombatPhysicalContactEventBuffer(2);
            buffer.Add(CreateContactEvent(
                CombatSurfaceMaterial.Rough,
                CombatPhysicalContactPhase.Started,
                contactPairKey: 456));
            system.Tick(buffer, frame: 1);
            buffer.Clear();
            buffer.Add(CreateContactEvent(
                CombatSurfaceMaterial.Rough,
                CombatPhysicalContactPhase.Started,
                contactPairKey: 457));
            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();

            PhysicalAudioPlaybackSummary summary = system.Tick(buffer, frame: 2);

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(0, summary.PlayedCount);
            Assert.AreEqual(1, summary.CooldownCapacitySkipCount);
            Assert.AreEqual(0, summary.PlaybackFailedSkipCount);
            Assert.AreEqual(0L, allocated);

            PhysicalAudioPlaybackSummary elapsed = system.Tick(buffer, frame: 20);
            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(1, elapsed.PlayedCount);
        }

        [Test]
        public void FixedCooldownCache_MixedDurationsNeverExpireLongSlotUsingIncomingShortDuration()
        {
            var cache = new PhysicalAudioFixedCooldownCache<int>(1);
            Assert.AreEqual(PhysicalAudioCooldownAdmission.Available, cache.Evaluate(1, 0, 100));
            Assert.IsTrue(cache.Record(1, 0, 100));
            Assert.AreEqual(PhysicalAudioCooldownAdmission.CapacityFull, cache.Evaluate(2, 10, 1));
            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();

            PhysicalAudioCooldownAdmission admission = cache.Evaluate(2, 100, 1);
            bool recorded = cache.Record(2, 100, 1);

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(PhysicalAudioCooldownAdmission.Available, admission);
            Assert.IsTrue(recorded);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void Tick_BodyPartAndContactPointExactRuleWinsFallback()
        {
            var audioService = new RecordingAudioService();
            var rules = new[]
            {
                new PhysicalAudioCueRule(
                    CombatPhysicalContactKind.BodyPartContact,
                    CombatSurfaceMaterialKind.Rough,
                    sourceSlotId: 0,
                    minimumIntensity: 0,
                    eventId: 1002,
                    cooldownFrames: 8,
                    priority: 2),
                new PhysicalAudioCueRule(
                    CombatPhysicalContactKind.BodyPartContact,
                    CombatSurfaceMaterialKind.Rough,
                    sourceSlotId: 0,
                    minimumIntensity: 0,
                    eventId: 2002,
                    cooldownFrames: 8,
                    priority: 2,
                    bodyPartId: 701,
                    contactPointId: 1)
            };
            var system = new PhysicalAudioPlaybackSystem(
                audioService,
                new PhysicalAudioCueRuleTable(rules),
                cooldownCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(4);
            buffer.Add(CreateContactEvent(CombatSurfaceMaterial.Rough, CombatPhysicalContactPhase.Started));

            PhysicalAudioPlaybackSummary summary = system.Tick(buffer, frame: 12);

            Assert.AreEqual(1, summary.BodyPartContactPlayedCount);
            Assert.AreEqual(2002, audioService.LastRequest.EventId);
        }

        [Test]
        public void Tick_ContactPhysicsWritesAudioParameters()
        {
            const int volumeParameterId = 101;
            const int pitchParameterId = 102;
            const int lowPassParameterId = 103;
            const int brightnessParameterId = 104;
            const int scrapeParameterId = 105;
            var audioService = new RecordingAudioService();
            var system = new PhysicalAudioPlaybackSystem(
                audioService,
                CreateRuleTable(),
                cooldownCapacity: 4,
                parameterMapping: new PhysicalAudioParameterMapping(
                    volumeParameterId,
                    pitchParameterId,
                    lowPassParameterId,
                    brightnessParameterId,
                    scrapeParameterId));
            var buffer = new CombatPhysicalContactEventBuffer(4);
            buffer.Add(CreateContactEvent(
                CombatSurfaceMaterial.HardSlick,
                CombatPhysicalContactPhase.Started,
                relativeSpeedMillimetersPerSecond: 2600,
                normalSpeedMillimetersPerSecond: 1800,
                tangentialSpeedMillimetersPerSecond: 1600,
                pressure: 180000,
                intensity: 2600));

            PhysicalAudioPlaybackSummary summary = system.Tick(buffer, frame: 12);

            Assert.AreEqual(1, summary.PlayedCount);
            Assert.AreEqual(5, audioService.LastRequest.Parameters.Count);
            Assert.Greater(FindParameter(audioService.LastRequest.Parameters, volumeParameterId), 0.6f);
            Assert.Greater(FindParameter(audioService.LastRequest.Parameters, pitchParameterId), 1f);
            Assert.Greater(FindParameter(audioService.LastRequest.Parameters, lowPassParameterId), 0.6f);
            Assert.Greater(FindParameter(audioService.LastRequest.Parameters, brightnessParameterId), 0.7f);
            Assert.Greater(FindParameter(audioService.LastRequest.Parameters, scrapeParameterId), 0.2f);
        }

        private static PhysicalAudioCueRuleTable CreateRuleTable()
        {
            return new PhysicalAudioCueRuleTable(new[]
            {
                new PhysicalAudioCueRule(
                    CombatPhysicalContactKind.BodyPartContact,
                    CombatSurfaceMaterialKind.Default,
                    sourceSlotId: 0,
                    minimumIntensity: 0,
                    eventId: 1001,
                    cooldownFrames: 8,
                    priority: 1),
                new PhysicalAudioCueRule(
                    CombatPhysicalContactKind.BodyPartContact,
                    CombatSurfaceMaterialKind.Rough,
                    sourceSlotId: 0,
                    minimumIntensity: 0,
                    eventId: 1002,
                    cooldownFrames: 8,
                    priority: 2)
            });
        }

        private static CombatPhysicalContactEvent CreateContactEvent(
            CombatSurfaceMaterial material,
            CombatPhysicalContactPhase phase,
            int relativeSpeedMillimetersPerSecond = 900,
            int normalSpeedMillimetersPerSecond = 0,
            int tangentialSpeedMillimetersPerSecond = 900,
            int pressure = 0,
            int intensity = 900,
            int targetBodyId = 20,
            int contactPairKey = 456)
        {
            return new CombatPhysicalContactEvent(
                new CombatFrame(1),
                0,
                CombatPhysicalContactKind.BodyPartContact,
                phase,
                new CombatEntityId(10),
                new CombatBodyId(10),
                new CombatColliderId(1),
                new CombatBodyId(targetBodyId),
                new CombatColliderId(1),
                CombatPhysicsShapeKind.Aabb,
                1,
                contactPairKey,
                new FixVector3(ToFix(1250), ToFix(500), ToFix(-750)),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                material,
                relativeSpeedMillimetersPerSecond,
                normalSpeedMillimetersPerSecond,
                tangentialSpeedMillimetersPerSecond,
                pressure,
                intensity,
                contactPointId: 1,
                bodyPartId: 701);
        }

        private static float FindParameter(IReadOnlyList<AudioParameterValue> parameters, int parameterId)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].ParameterId == parameterId)
                {
                    return parameters[i].Value;
                }
            }

            Assert.Fail("Audio parameter was not found: " + parameterId);
            return 0f;
        }

        private static Fix64 ToFix(int millimeters)
        {
            return Fix64.FromRatio(millimeters, 1000);
        }

        private static int CreateStableBodyPartEmitterId(
            int sourceEntityId,
            int contactPointId,
            int bodyPartId)
        {
            unchecked
            {
                int hash = 37;
                hash = (hash * 397) ^ sourceEntityId;
                hash = (hash * 397) ^ contactPointId;
                hash = (hash * 397) ^ bodyPartId;
                hash &= int.MaxValue;
                return hash == 0 ? sourceEntityId : hash;
            }
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public int PlayOneShotCount { get; private set; }
            public AudioPlayRequest LastRequest { get; private set; }

            public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
            {
                PlayOneShotCount++;
                LastRequest = request;
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
                return AudioResult.Ok();
            }

            public AudioResult SetBusMuted(int busId, bool muted)
            {
                return AudioResult.Ok();
            }

            public AudioDebugSnapshot CaptureSnapshot()
            {
                return new AudioDebugSnapshot(
                    initialized: true,
                    totalPlayRequests: PlayOneShotCount,
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
