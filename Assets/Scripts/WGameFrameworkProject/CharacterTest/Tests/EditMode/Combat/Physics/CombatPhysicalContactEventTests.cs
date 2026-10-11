using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public sealed class CombatPhysicalContactEventTests
    {
        private const int GroundLayer = 1;

        [Test]
        public void SensorRunner_EmitsStartedStayedEndedContactPhases()
        {
            CombatPhysicsWorld world = CreateWorld(CombatSurfaceMaterial.Rough);
            var runner = new CombatPhysicalContactSensorRunner(sensorCapacity: 2, queryHitCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(8);
            CombatPhysicalContactSensorSample sample = CreateSample(enabled: true);
            var samples = new[] { sample };

            runner.Tick(world, samples, new CombatFrame(10), buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Started, buffer[0].Phase);
            Assert.AreEqual(CombatPhysicalContactKind.BodyPartContact, buffer[0].Kind);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, buffer[0].SurfaceMaterial.Kind);
            Assert.AreEqual(new CombatBodyId(20), buffer[0].TargetBodyId);
            Assert.AreEqual(1, buffer[0].ContactPointId);
            Assert.AreEqual(701, buffer[0].BodyPartId);
            Assert.AreEqual(CombatBodyPartContactActivationMode.Always, buffer[0].ActivationMode);
            Assert.GreaterOrEqual(buffer[0].Normal.Y.RawValue, Fix64.Half.RawValue);

            buffer.Clear();
            runner.Tick(world, samples, new CombatFrame(11), buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Stayed, buffer[0].Phase);

            buffer.Clear();
            samples[0] = CreateSample(enabled: false);
            runner.Tick(world, samples, new CombatFrame(12), buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Ended, buffer[0].Phase);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, buffer[0].SurfaceMaterial.Kind);
        }

        [Test]
        public void EventBuffer_WhenFull_DropsWithoutThrowing()
        {
            var buffer = new CombatPhysicalContactEventBuffer(1);
            CombatPhysicalContactEvent contactEvent = CreateContactEvent(0);

            Assert.IsTrue(buffer.Add(contactEvent));
            Assert.IsFalse(buffer.Add(CreateContactEvent(1)));

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.DroppedCount);
            Assert.AreNotEqual(CombatHash.Empty, buffer.ComputeHash());
        }

        [Test]
        public void BodyPartContactPointRuntime_DisabledDoesNotQueryAndEmitsEndedWhenTurningOff()
        {
            CombatPhysicsWorld world = CreateWorld(CombatSurfaceMaterial.Default);
            var runtime = new CombatBodyPartContactPointRuntime(sampleCapacity: 2, queryHitCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(8);
            CombatBodyPartContactPointSample active =
                CreateBodyPartSample(CombatBodyPartContactActivationMode.Always);
            CombatBodyPartContactPointSample disabled =
                CreateBodyPartSample(CombatBodyPartContactActivationMode.Disabled);

            runtime.Tick(world, new[] { disabled }, new CombatFrame(1), buffer);

            Assert.AreEqual(0, buffer.Count);

            runtime.Tick(world, new[] { active }, new CombatFrame(2), buffer);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Started, buffer[0].Phase);

            buffer.Clear();
            runtime.Tick(world, new[] { disabled }, new CombatFrame(3), buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Ended, buffer[0].Phase);
            Assert.AreEqual(CombatBodyPartContactActivationMode.Disabled, buffer[0].ActivationMode);
        }

        [Test]
        public void BodyPartContactPointRuntime_AlwaysEmitsStartedThenStayed()
        {
            CombatPhysicsWorld world = CreateWorld(CombatSurfaceMaterial.Rough);
            var runtime = new CombatBodyPartContactPointRuntime(sampleCapacity: 2, queryHitCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(8);
            CombatBodyPartContactPointSample sample =
                CreateBodyPartSample(CombatBodyPartContactActivationMode.Always);

            runtime.Tick(world, new[] { sample }, new CombatFrame(1), buffer);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Started, buffer[0].Phase);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, buffer[0].SurfaceMaterial.Kind);

            buffer.Clear();
            runtime.Tick(world, new[] { sample }, new CombatFrame(2), buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Stayed, buffer[0].Phase);
        }

        [Test]
        public void BodyPartContactPointRuntime_ActionWindowOnlyActiveInsideWindow()
        {
            CombatPhysicsWorld world = CreateWorld(CombatSurfaceMaterial.Default);
            var runtime = new CombatBodyPartContactPointRuntime(sampleCapacity: 2, queryHitCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(8);

            runtime.Tick(
                world,
                new[] { CreateBodyPartSample(CombatBodyPartContactActivationMode.ActionWindow) },
                new CombatFrame(1),
                buffer);
            Assert.AreEqual(0, buffer.Count);

            runtime.Tick(
                world,
                new[] { CreateBodyPartSample(CombatBodyPartContactActivationMode.ActionWindow, actionWindowActive: true) },
                new CombatFrame(2),
                buffer);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Started, buffer[0].Phase);
            Assert.AreEqual(CombatBodyPartContactActivationMode.ActionWindow, buffer[0].ActivationMode);
        }

        [Test]
        public void BodyPartContactPointRuntime_ExplicitStateOpensDeterministicContact()
        {
            CombatPhysicsWorld world = CreateWorld(CombatSurfaceMaterial.Default);
            var runtime = new CombatBodyPartContactPointRuntime(sampleCapacity: 2, queryHitCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(8);

            runtime.Tick(
                world,
                new[] { CreateBodyPartSample(CombatBodyPartContactActivationMode.ExplicitState) },
                new CombatFrame(1),
                buffer);
            Assert.AreEqual(0, buffer.Count);

            runtime.Tick(
                world,
                new[] { CreateBodyPartSample(CombatBodyPartContactActivationMode.ExplicitState, explicitActive: true) },
                new CombatFrame(2),
                buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(CombatPhysicalContactPhase.Started, buffer[0].Phase);
            Assert.AreEqual(CombatBodyPartContactActivationMode.ExplicitState, buffer[0].ActivationMode);
            Assert.AreNotEqual(CombatHash.Empty, buffer.ComputeHash());
        }

        [Test]
        public void BodyPartContactPointRuntime_SortsSamplesForStableSequenceAndHash()
        {
            CombatPhysicsWorld world = CreateWorld(CombatSurfaceMaterial.Default);
            var firstRuntime = new CombatBodyPartContactPointRuntime(sampleCapacity: 4, queryHitCapacity: 4);
            var secondRuntime = new CombatBodyPartContactPointRuntime(sampleCapacity: 4, queryHitCapacity: 4);
            var firstBuffer = new CombatPhysicalContactEventBuffer(8);
            var secondBuffer = new CombatPhysicalContactEventBuffer(8);
            CombatBodyPartContactPointSample left = CreateBodyPartSample(
                CombatBodyPartContactActivationMode.Always,
                contactPointId: 1,
                bodyPartId: 701,
                sourceOrder: 2);
            CombatBodyPartContactPointSample right = CreateBodyPartSample(
                CombatBodyPartContactActivationMode.Always,
                contactPointId: 2,
                bodyPartId: 702,
                sourceOrder: 1);

            firstRuntime.Tick(world, new[] { left, right }, new CombatFrame(1), firstBuffer);
            secondRuntime.Tick(world, new[] { right, left }, new CombatFrame(1), secondBuffer);

            Assert.AreEqual(2, firstBuffer.Count);
            Assert.AreEqual(2, secondBuffer.Count);
            Assert.AreEqual(2, firstBuffer[0].ContactPointId);
            Assert.AreEqual(1, firstBuffer[1].ContactPointId);
            Assert.AreEqual(firstBuffer[0], secondBuffer[0]);
            Assert.AreEqual(firstBuffer[1], secondBuffer[1]);
            Assert.AreEqual(firstBuffer.ComputeHash(), secondBuffer.ComputeHash());
        }

        [Test]
        public void BodyPartContactPointRuntime_SortedTickDoesNotAllocateAfterWarmup()
        {
            CombatPhysicsWorld world = CreateWorld(CombatSurfaceMaterial.Default);
            var runtime = new CombatBodyPartContactPointRuntime(sampleCapacity: 4, queryHitCapacity: 4);
            var buffer = new CombatPhysicalContactEventBuffer(8);
            var samples = new[]
            {
                CreateBodyPartSample(
                    CombatBodyPartContactActivationMode.Always,
                    contactPointId: 3,
                    bodyPartId: 703,
                    sourceOrder: 3),
                CreateBodyPartSample(
                    CombatBodyPartContactActivationMode.Always,
                    contactPointId: 1,
                    bodyPartId: 701,
                    sourceOrder: 1),
                CreateBodyPartSample(
                    CombatBodyPartContactActivationMode.Always,
                    contactPointId: 4,
                    bodyPartId: 704,
                    sourceOrder: 4),
                CreateBodyPartSample(
                    CombatBodyPartContactActivationMode.Always,
                    contactPointId: 2,
                    bodyPartId: 702,
                    sourceOrder: 2)
            };

            for (int i = 0; i < 8; i++)
            {
                buffer.Clear();
                runtime.Tick(world, samples, new CombatFrame(i), buffer);
            }

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                buffer.Clear();
                runtime.Tick(world, samples, new CombatFrame(100 + i), buffer);
            }

            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(4, buffer.Count);
            Assert.AreEqual(1, buffer[0].ContactPointId);
            Assert.AreEqual(4, buffer[3].ContactPointId);
            Assert.AreEqual(CombatPhysicalContactPhase.Stayed, buffer[0].Phase);
        }

        private static CombatPhysicsWorld CreateWorld(CombatSurfaceMaterial material)
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(20);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(20), bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                GroundLayer,
                new FixVector3(ToFix(-1000), ToFix(-100), ToFix(-1000)),
                new FixVector3(ToFix(1000), Fix64.Zero, ToFix(1000)),
                material));
            return world;
        }

        private static CombatPhysicalContactSensorSample CreateSample(bool enabled)
        {
            return new CombatPhysicalContactSensorSample(
                1001,
                CombatPhysicalContactKind.BodyPartContact,
                new CombatEntityId(10),
                new CombatBodyId(10),
                new CombatColliderId(1),
                1,
                new FixVector3(Fix64.Zero, ToFix(50), Fix64.Zero),
                ToFix(100),
                CombatPhysicsLayerMask.FromLayer(GroundLayer),
                CombatSurfaceMaterial.Default,
                800,
                0,
                800,
                0,
                800,
                1001,
                1,
                enabled: enabled,
                contactPointId: 1,
                bodyPartId: 701,
                activationMode: CombatBodyPartContactActivationMode.Always);
        }

        private static CombatPhysicalContactEvent CreateContactEvent(int sequence)
        {
            return new CombatPhysicalContactEvent(
                new CombatFrame(1),
                sequence,
                CombatPhysicalContactKind.BodyPartContact,
                CombatPhysicalContactPhase.Started,
                new CombatEntityId(10),
                new CombatBodyId(10),
                new CombatColliderId(1),
                new CombatBodyId(20),
                new CombatColliderId(1),
                CombatPhysicsShapeKind.Aabb,
                1,
                123,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                CombatSurfaceMaterial.Default,
                800,
                0,
                800,
                0,
                800,
                contactPointId: 1,
                bodyPartId: 701,
                activationMode: CombatBodyPartContactActivationMode.Always);
        }

        private static CombatBodyPartContactPointSample CreateBodyPartSample(
            CombatBodyPartContactActivationMode activationMode,
            bool actionWindowActive = false,
            bool explicitActive = false,
            bool motionStateActive = false,
            int contactPointId = 1,
            int bodyPartId = 701,
            int sourceOrder = 1)
        {
            var definition = new CombatBodyPartContactPointDefinition(
                contactPointId,
                bodyPartId,
                FixVector3.Zero,
                ToFix(100),
                CombatPhysicsLayerMask.FromLayer(GroundLayer),
                activationMode,
                sourceOrder,
                sourceSlotId: contactPointId);
            return new CombatBodyPartContactPointSample(
                definition,
                new CombatEntityId(10),
                new CombatBodyId(10),
                new CombatColliderId(1),
                new FixVector3(Fix64.Zero, ToFix(50), Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                CombatSurfaceMaterial.Default,
                800,
                0,
                800,
                0,
                800,
                traceId: 1001 + contactPointId,
                actionWindowActive: actionWindowActive,
                explicitActive: explicitActive,
                motionStateActive: motionStateActive);
        }

        private static Fix64 ToFix(int millimeters)
        {
            return Fix64.FromRatio(millimeters, 1000);
        }
    }
}
