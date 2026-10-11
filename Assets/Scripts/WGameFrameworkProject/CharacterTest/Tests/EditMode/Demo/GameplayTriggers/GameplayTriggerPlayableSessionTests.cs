using MxFramework.Demo.GameplayTriggers;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.GameplayTriggers
{
    public sealed class GameplayTriggerPlayableSessionTests
    {
        [Test]
        public void FreshSession_UsesFixedFrameAndKeepsDoorSourceDisabled()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot initial = session.Snapshot;
                GameplayTriggerPlayableTickResult first = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 1000, moveZ: 0));
                GameplayTriggerPlayableTickResult second = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 1000));

                Assert.That(initial.Frame, Is.Zero);
                Assert.That(initial.CheckpointAccepted, Is.False);
                Assert.That(initial.DoorOpen, Is.False);
                Assert.That(initial.CombatBodyCount, Is.EqualTo(6));
                Assert.That(initial.CombatColliderCount, Is.EqualTo(6));
                Assert.That(first.Accepted, Is.True);
                Assert.That(first.Snapshot.Frame, Is.EqualTo(1));
                Assert.That(second.Snapshot.Frame, Is.EqualTo(2));
                Assert.That(second.Snapshot.AuthorityHash, Is.Not.Zero);
            }
        }

        [Test]
        public void MovingRight_DrivesFullChainAndProjectsEffectBearingTrapBuff()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot snapshot = session.Snapshot;
                for (int frame = 0; frame < 120; frame++)
                {
                    GameplayTriggerPlayableTickResult result = session.Tick(
                        new GameplayTriggerPlayableInput(moveX: 1000, moveZ: 0));
                    Assert.That(result.Accepted, Is.True, "frame " + frame);
                    snapshot = result.Snapshot;
                }

                Assert.That(snapshot.CheckpointAccepted, Is.True);
                Assert.That(snapshot.DoorOpen, Is.True);
                Assert.That(snapshot.CombatBodyCount, Is.EqualTo(5));
                Assert.That(snapshot.CombatColliderCount, Is.EqualTo(5));
                Assert.That(snapshot.TrapActivations, Is.EqualTo(1));
                Assert.That(snapshot.PlayerXRaw, Is.GreaterThan(0L));
            }
        }

        [Test]
        public void RemainingInsideTrap_ActivatesAgainAtExactCooldownBoundary()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot snapshot = session.Snapshot;
                for (int frame = 0; frame < 120 && snapshot.TrapActivations == 0; frame++)
                {
                    snapshot = session.Tick(
                        new GameplayTriggerPlayableInput(moveX: 1000, moveZ: 0)).Snapshot;
                }

                Assert.That(snapshot.TrapActivations, Is.EqualTo(1));
                long firstActivationFrame = snapshot.Frame;
                long trapPosition = snapshot.PlayerXRaw;
                for (int elapsed = 1; elapsed < 30; elapsed++)
                {
                    snapshot = session.Tick(
                        new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;
                    Assert.That(
                        snapshot.TrapActivations,
                        Is.EqualTo(1),
                        "elapsed logical frames " + elapsed);
                    Assert.That(snapshot.PlayerXRaw, Is.EqualTo(trapPosition));
                }

                snapshot = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;

                Assert.That(snapshot.Frame, Is.EqualTo(firstActivationFrame + 30L));
                Assert.That(snapshot.TrapActivations, Is.EqualTo(2));
                Assert.That(snapshot.PlayerXRaw, Is.EqualTo(trapPosition));
            }
        }

        [Test]
        public void ExitingAndReenteringTrap_ProducesAnotherActivationAfterCooldown()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot snapshot = session.Snapshot;
                for (int frame = 0; frame < 120 && snapshot.TrapActivations == 0; frame++)
                {
                    snapshot = session.Tick(
                        new GameplayTriggerPlayableInput(moveX: 1000, moveZ: 0)).Snapshot;
                }

                Assert.That(snapshot.TrapActivations, Is.EqualTo(1));
                for (int frame = 0; frame < 35; frame++)
                {
                    snapshot = session.Tick(
                        new GameplayTriggerPlayableInput(moveX: 1000, moveZ: 0)).Snapshot;
                }
                Assert.That(snapshot.TrapActivations, Is.EqualTo(1));

                for (int frame = 0; frame < 35 && snapshot.TrapActivations == 1; frame++)
                {
                    snapshot = session.Tick(
                        new GameplayTriggerPlayableInput(moveX: -1000, moveZ: 0)).Snapshot;
                }

                Assert.That(snapshot.TrapActivations, Is.EqualTo(2));
            }
        }

        [Test]
        public void OneUnitySample_MultipleLogicalTicks_PressedEdgeAdmittedOnce_HeldMoveAdmittedPerTick()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                Assert.That(session.SubmitInput(new GameplayTriggerPlayableInput(
                    moveX: 1000,
                    moveZ: 0,
                    capture: true)), Is.True);

                GameplayTriggerPlayableSnapshot first = session.Tick().Snapshot;
                GameplayTriggerPlayableSnapshot second = session.Tick().Snapshot;

                Assert.That(first.LastControlCommandId, Is.EqualTo(7360102));
                Assert.That(second.LastControlCommandId, Is.Zero);
                Assert.That(second.PlayerXRaw, Is.GreaterThan(first.PlayerXRaw));
            }
        }

        [Test]
        public void MultipleControlEdges_UseResetRestoreCapturePriority_AndBatchRemainsAtomic()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                Assert.That(session.SubmitInput(new GameplayTriggerPlayableInput(
                    moveX: 1000,
                    moveZ: 0,
                    capture: true,
                    restore: true,
                    reset: true)), Is.True);

                GameplayTriggerPlayableTickResult result = session.Tick();

                Assert.That(result.Accepted, Is.True);
                Assert.That(result.Snapshot.LastControlCommandId, Is.EqualTo(7360104));
                Assert.That(result.Snapshot.Frame, Is.Zero);
                Assert.That(result.Snapshot.PlayerXRaw, Is.EqualTo(-4000000L));
                Assert.That(result.Snapshot.HasCapture, Is.False);
            }
        }

        [Test]
        public void FullEdgeRing_RejectsNewSampleWithoutChangingHeldMovement()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                for (int sample = 0; sample < 8; sample++)
                {
                    Assert.That(session.SubmitInput(new GameplayTriggerPlayableInput(
                        moveX: 1000,
                        moveZ: 0,
                        capture: true)), Is.True);
                }
                Assert.That(session.SubmitInput(new GameplayTriggerPlayableInput(
                    moveX: -1000,
                    moveZ: 0,
                    capture: true)), Is.False);

                long startX = session.Snapshot.PlayerXRaw;
                GameplayTriggerPlayableSnapshot snapshot = session.Tick().Snapshot;

                Assert.That(snapshot.PlayerXRaw, Is.GreaterThan(startX));
                Assert.That(snapshot.LastControlCommandId, Is.EqualTo(7360102));
            }
        }
    }
}
