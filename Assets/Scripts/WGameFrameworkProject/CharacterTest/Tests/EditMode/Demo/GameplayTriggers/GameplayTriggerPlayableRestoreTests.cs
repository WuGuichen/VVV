using System;
using MxFramework.Demo.GameplayTriggers;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.GameplayTriggers
{
    public sealed class GameplayTriggerPlayableRestoreTests
    {
        [Test]
        public void CaptureBeforeFirstTick_IsRejectedWithoutPublishingSlot()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableControlResult capture =
                    session.CaptureCheckpoint();

                Assert.That(capture.Success, Is.False);
                Assert.That(capture.Snapshot.HasCapture, Is.False);
                Assert.That(capture.Snapshot.Frame, Is.Zero);
            }
        }

        [Test]
        public void CaptureAfterSuccessfulTick_BeforeCheckpointAcquisition_IsRejected()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot before = TickRight(session, 1);

                GameplayTriggerPlayableControlResult capture =
                    session.CaptureCheckpoint();

                Assert.That(before.CheckpointAccepted, Is.False);
                Assert.That(capture.Success, Is.False);
                Assert.That(capture.Snapshot.HasCapture, Is.False);
                Assert.That(capture.Snapshot.Frame, Is.EqualTo(before.Frame));
            }
        }

        [Test]
        public void CaptureMutateRestore_RebuildsFreshSessionAtFPlusOne()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot captured = TickRight(session, 75);
                GameplayTriggerPlayableControlResult capture =
                    session.CaptureCheckpoint();
                Assert.That(capture.Success, Is.True);
                Assert.That(capture.Snapshot.HasCapture, Is.True);
                Assert.That(capture.Snapshot.CapturedFrame, Is.EqualTo(74L));

                TickRight(session, 30);
                GameplayTriggerPlayableControlResult restore = session.RestoreFresh();

                Assert.That(restore.Success, Is.True);
                Assert.That(restore.Snapshot.Frame, Is.EqualTo(captured.Frame));
                Assert.That(restore.Snapshot.PlayerXRaw, Is.EqualTo(captured.PlayerXRaw));
                Assert.That(restore.Snapshot.CheckpointAccepted, Is.EqualTo(
                    captured.CheckpointAccepted));
                Assert.That(restore.Snapshot.DoorUnlocked, Is.EqualTo(
                    captured.DoorUnlocked));
                Assert.That(restore.Snapshot.DoorOpen, Is.EqualTo(captured.DoorOpen));
                Assert.That(restore.Snapshot.TrapActivations, Is.EqualTo(
                    captured.TrapActivations));
                Assert.That(restore.Snapshot.CombatBodyCount, Is.EqualTo(
                    captured.CombatBodyCount));
                Assert.That(restore.Snapshot.HasCapture, Is.True);

                GameplayTriggerPlayableSnapshot continued = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;
                Assert.That(continued.Frame, Is.EqualTo(captured.Frame + 1L));
            }
        }

        [Test]
        public void RestoreWhileTrapOccupied_ContinuesStayedWithoutNewEnteredActivation()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot snapshot = session.Snapshot;
                for (int frame = 0; frame < 120 && snapshot.TrapActivations == 0; frame++)
                {
                    snapshot = session.Tick(new GameplayTriggerPlayableInput(
                        moveX: 1000,
                        moveZ: 0)).Snapshot;
                }
                Assert.That(snapshot.TrapActivations, Is.EqualTo(1));
                long occupiedX = snapshot.PlayerXRaw;
                Assert.That(session.CaptureCheckpoint().Success, Is.True);

                TickRight(session, 10);
                GameplayTriggerPlayableControlResult restore = session.RestoreFresh();
                GameplayTriggerPlayableSnapshot stayed = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;

                Assert.That(restore.Success, Is.True);
                Assert.That(restore.Snapshot.PlayerXRaw, Is.EqualTo(occupiedX));
                Assert.That(stayed.PlayerXRaw, Is.EqualTo(occupiedX));
                Assert.That(stayed.TrapActivations, Is.EqualTo(1));
                Assert.That(
                    stayed.LastOccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Stayed));
                Assert.That(
                    stayed.FirstOccurrenceKindThisTick,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Stayed));
                Assert.That(stayed.OccurrenceCountThisTick, Is.EqualTo(1));
            }
        }

        [Test]
        public void CaptureEndF_WithCommittedDeferredDueFPlus1_RestoreExecutesExactlyOnce()
        {
            int firstUnlockedFrame = FindFirstDoorUnlockedFrame();
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot beforeDue = TickRight(
                    session,
                    firstUnlockedFrame - 1);
                Assert.That(beforeDue.CheckpointAccepted, Is.True);
                Assert.That(beforeDue.DoorUnlocked, Is.False);
                Assert.That(session.CaptureCheckpoint().Success, Is.True);
                Assert.That(session.RestoreFresh().Success, Is.True);

                GameplayTriggerPlayableSnapshot due = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;
                GameplayTriggerPlayableSnapshot after = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;

                Assert.That(due.Frame, Is.EqualTo(firstUnlockedFrame));
                Assert.That(due.CheckpointAccepted, Is.True);
                Assert.That(due.DoorUnlocked, Is.True);
                Assert.That(after.CheckpointAccepted, Is.True);
                Assert.That(after.DoorUnlocked, Is.True);
            }
        }

        [Test]
        public void CaptureEndF_WithCommittedReliableOutboxDueFPlus1_RestoreExecutesExactlyOnce()
        {
            int firstDoorOpenFrame = FindFirstDoorOpenFrame();
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot beforeDue = TickRight(
                    session,
                    firstDoorOpenFrame - 1);
                Assert.That(beforeDue.DoorOpen, Is.False);
                Assert.That(beforeDue.CombatBodyCount, Is.EqualTo(6));
                Assert.That(session.CaptureCheckpoint().Success, Is.True);
                Assert.That(session.RestoreFresh().Success, Is.True);

                GameplayTriggerPlayableSnapshot due = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;
                GameplayTriggerPlayableSnapshot after = session.Tick(
                    new GameplayTriggerPlayableInput(moveX: 0, moveZ: 0)).Snapshot;

                Assert.That(due.Frame, Is.EqualTo(firstDoorOpenFrame));
                Assert.That(due.DoorOpen, Is.True);
                Assert.That(due.CombatBodyCount, Is.EqualTo(5));
                Assert.That(due.ActionResultCountThisTick, Is.EqualTo(1));
                Assert.That(
                    due.LastResultKind,
                    Is.EqualTo(GameplayTriggerRuntimeEventKind.ActionResult));
                Assert.That(
                    due.LastResultStatus,
                    Is.EqualTo(GameplayTriggerRuntimeEventStatus.Succeeded));
                Assert.That(
                    due.LastActionKindId,
                    Is.EqualTo(StoryRuntimeCommandIds.RaiseTrigger));
                Assert.That(after.DoorOpen, Is.True);
                Assert.That(after.CombatBodyCount, Is.EqualTo(5));
                Assert.That(after.ActionResultCountThisTick, Is.Zero);
            }
        }

        [Test]
        public void RestoreWithoutSlot_FailsAndPreservesLiveSession()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot before = TickRight(session, 5);

                GameplayTriggerPlayableControlResult restore = session.RestoreFresh();

                Assert.That(restore.Success, Is.False);
                Assert.That(restore.Snapshot.Frame, Is.EqualTo(before.Frame));
                Assert.That(restore.Snapshot.PlayerXRaw, Is.EqualTo(before.PlayerXRaw));
                Assert.That(restore.Snapshot.HasCapture, Is.False);
            }
        }

        [Test]
        public void RestoreFresh_IncompatibleCheckpointFailsCandidateAndPreservesLiveSession()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                GameplayTriggerPlayableSnapshot captured = TickRight(session, 25);
                GameplayTriggerPlayableControlResult capture =
                    session.CaptureCheckpoint();
                Assert.That(capture.Success, Is.True);

                TickRight(session, 5);
                Assert.That(session.SubmitInput(new GameplayTriggerPlayableInput(
                    moveX: -1000,
                    moveZ: 0)), Is.True);
                long beforeHash = session.ComputeHash();
                GameplayTriggerPlayableSnapshot before = session.Snapshot;
                var incompatible = new RuntimeSaveState(
                    RuntimeSaveState.CurrentSchemaVersion,
                    DateTime.UtcNow,
                    "mxframework-19.4a",
                    "gameplay-trigger-playable-v1",
                    "embedded-demo-assets-v1",
                    frame: before.Frame - 1L,
                    entities: null,
                    globalCounters: null,
                    moduleStates: null);

                GameplayTriggerPlayableControlResult failed =
                    session.RestoreFresh(incompatible);

                Assert.That(failed.Success, Is.False);
                Assert.That(failed.Snapshot.Frame, Is.EqualTo(before.Frame));
                Assert.That(failed.Snapshot.PlayerXRaw, Is.EqualTo(before.PlayerXRaw));
                Assert.That(failed.Snapshot.AuthorityHash, Is.EqualTo(beforeHash));
                Assert.That(failed.Snapshot.HasCapture, Is.True);
                Assert.That(
                    failed.Snapshot.CapturedFrame,
                    Is.EqualTo(capture.Snapshot.CapturedFrame));
                Assert.That(session.ComputeHash(), Is.EqualTo(beforeHash));

                GameplayTriggerPlayableSnapshot latched = session.Tick().Snapshot;
                Assert.That(latched.PlayerXRaw, Is.LessThan(before.PlayerXRaw));

                GameplayTriggerPlayableControlResult restored = session.RestoreFresh();
                Assert.That(restored.Success, Is.True);
                Assert.That(restored.Snapshot.Frame, Is.EqualTo(captured.Frame));
                Assert.That(restored.Snapshot.PlayerXRaw, Is.EqualTo(captured.PlayerXRaw));
            }
        }

        [Test]
        public void ResetFresh_ReplacesAuthorityAndClearsInputLatchAndSlot()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                TickRight(session, 25);
                Assert.That(session.CaptureCheckpoint().Success, Is.True);
                Assert.That(session.SubmitInput(new GameplayTriggerPlayableInput(
                    moveX: 1000,
                    moveZ: 0,
                    capture: true)), Is.True);

                GameplayTriggerPlayableControlResult reset = session.ResetFresh();
                GameplayTriggerPlayableSnapshot next = session.Tick().Snapshot;

                Assert.That(reset.Success, Is.True);
                Assert.That(reset.Snapshot.Frame, Is.Zero);
                Assert.That(reset.Snapshot.PlayerXRaw, Is.EqualTo(-4000000L));
                Assert.That(reset.Snapshot.HasCapture, Is.False);
                Assert.That(next.PlayerXRaw, Is.EqualTo(-4000000L));
                Assert.That(next.LastControlCommandId, Is.Zero);
            }
        }

        private static GameplayTriggerPlayableSnapshot TickRight(
            GameplayTriggerPlayableSession session,
            int count)
        {
            GameplayTriggerPlayableSnapshot snapshot = session.Snapshot;
            for (int i = 0; i < count; i++)
            {
                snapshot = session.Tick(new GameplayTriggerPlayableInput(
                    moveX: 1000,
                    moveZ: 0)).Snapshot;
            }
            return snapshot;
        }

        private static int FindFirstDoorUnlockedFrame()
        {
            using (var probe = new GameplayTriggerPlayableSession())
            {
                for (int frame = 1; frame <= 120; frame++)
                {
                    GameplayTriggerPlayableSnapshot snapshot = probe.Tick(
                        new GameplayTriggerPlayableInput(moveX: 1000, moveZ: 0)).Snapshot;
                    if (snapshot.DoorUnlocked)
                        return frame;
                }
            }
            Assert.Fail("Door source was not enabled within the probe window.");
            return -1;
        }

        private static int FindFirstDoorOpenFrame()
        {
            using (var probe = new GameplayTriggerPlayableSession())
            {
                for (int frame = 1; frame <= 120; frame++)
                {
                    GameplayTriggerPlayableSnapshot snapshot = probe.Tick(
                        new GameplayTriggerPlayableInput(moveX: 1000, moveZ: 0)).Snapshot;
                    if (snapshot.DoorOpen)
                        return frame;
                }
            }
            Assert.Fail("Door did not open within the probe window.");
            return -1;
        }
    }
}
