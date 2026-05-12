using System.Collections.Generic;
using MxFramework.Demo.MarbleMaze;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.MarbleMaze
{
    public class MarbleMazeRuntimeTests
    {
        [Test]
        public void RuntimeCommandBuffer_TiltAndPhysicsSampleUpdateRuntimeSnapshot()
        {
            using (var runner = new MarbleMazeRuntimeRunner(new MarbleMazeOptions(checkpointCount: 2)))
            {
                runner.EnqueueCommand(
                    0,
                    MarbleMazeCommand.Tilt,
                    payload0: MarbleMazeGame.Encode(0.5d),
                    payload1: MarbleMazeGame.Encode(-0.25d));
                runner.EnqueueCommand(
                    0,
                    MarbleMazeCommand.PhysicsSample,
                    payload0: MarbleMazeGame.Encode(1.25d),
                    payload1: MarbleMazeGame.Encode(0.5d),
                    payload2: MarbleMazeGame.Encode(-2d));

                runner.TickFrame(0);

                MarbleMazeSnapshot snapshot = runner.Game.CaptureSnapshot();
                Assert.AreEqual(0.5d, snapshot.TiltX, 0.0001d);
                Assert.AreEqual(-0.25d, snapshot.TiltZ, 0.0001d);
                Assert.AreEqual(1.25d, snapshot.BallPosition.X, 0.0001d);
                Assert.AreEqual(-2d, snapshot.BallPosition.Z, 0.0001d);
                StringAssert.Contains("checkpoints=0/2", runner.LastDiagnosticsSummary);
            }
        }

        [Test]
        public void CheckpointsMustArriveInOrderBeforeFinish()
        {
            using (var runner = new MarbleMazeRuntimeRunner(new MarbleMazeOptions(checkpointCount: 2)))
            {
                runner.EnqueueCommand(0, MarbleMazeCommand.Checkpoint, targetId: 1);
                runner.EnqueueCommand(1, MarbleMazeCommand.Checkpoint, targetId: 0);
                runner.EnqueueCommand(2, MarbleMazeCommand.Checkpoint, targetId: 1);
                runner.EnqueueCommand(3, MarbleMazeCommand.Finish);

                runner.TickFrame(0);
                Assert.AreEqual(0, runner.Game.CaptureSnapshot().CheckpointsCleared);

                runner.TickFrame(1);
                Assert.AreEqual(1, runner.Game.CaptureSnapshot().CheckpointsCleared);
                Assert.IsFalse(runner.Game.CaptureSnapshot().IsFinished);

                runner.TickFrame(2);
                MarbleMazeSnapshot collected = runner.Game.CaptureSnapshot();
                Assert.AreEqual(2, collected.CheckpointsCleared);
                Assert.IsFalse(collected.IsFinished);

                runner.TickFrame(3);
                MarbleMazeSnapshot finished = runner.Game.CaptureSnapshot();
                Assert.IsTrue(finished.IsFinished);
                Assert.IsTrue(finished.HasBestTime);
            }
        }

        [Test]
        public void FinishBeforeAllGemsDoesNotCompleteRun()
        {
            using (var runner = new MarbleMazeRuntimeRunner(new MarbleMazeOptions(checkpointCount: 2)))
            {
                runner.EnqueueCommand(0, MarbleMazeCommand.Checkpoint, targetId: 0);
                runner.EnqueueCommand(1, MarbleMazeCommand.Finish);

                runner.TickFrame(0);
                runner.TickFrame(1);

                MarbleMazeSnapshot snapshot = runner.Game.CaptureSnapshot();
                Assert.AreEqual(1, snapshot.CheckpointsCleared);
                Assert.IsFalse(snapshot.IsFinished);
                Assert.AreEqual("exit-locked", snapshot.LastEvent);
            }
        }

        [Test]
        public void FrameworkPhysicsWorld_UsesCombatPhysicsForCheckpointAndExitQueries()
        {
            var world = new MarbleMazeFrameworkPhysicsWorld(
                new[]
                {
                    new MarbleMazeVector3(-2.7d, 0.55d, -1.5d),
                    new MarbleMazeVector3(2.4d, 0.55d, 0.7d)
                },
                new MarbleMazeVector3(0d, 0.55d, 3.15d));

            Assert.AreEqual(7, world.ColliderCount);

            world.Reset(new MarbleMazeVector3(-2.7d, 0.65d, -1.5d));
            MarbleMazePhysicsStepResult checkpoint = world.Step(0d, 0d, 0d, nextCheckpointIndex: 0);
            Assert.AreEqual(0, checkpoint.CheckpointHit);
            Assert.IsFalse(checkpoint.ExitHit);

            world.Reset(new MarbleMazeVector3(0d, 0.65d, 3.15d));
            MarbleMazePhysicsStepResult exit = world.Step(0d, 0d, 0d, nextCheckpointIndex: 2);
            Assert.AreEqual(-1, exit.CheckpointHit);
            Assert.IsTrue(exit.ExitHit);
        }

        [Test]
        public void FrameworkPhysicsWorld_ClampsBallAgainstCombatPhysicsWalls()
        {
            var world = new MarbleMazeFrameworkPhysicsWorld(
                new[]
                {
                    new MarbleMazeVector3(-2.7d, 0.55d, -1.5d),
                    new MarbleMazeVector3(2.4d, 0.55d, 0.7d)
                },
                new MarbleMazeVector3(0d, 0.55d, 3.15d));

            world.Reset(new MarbleMazeVector3(3.9d, 0.65d, 0d));

            MarbleMazePhysicsStepResult result = world.Step(0.1d, 1d, 0d, nextCheckpointIndex: 0);

            Assert.IsTrue(result.WallHit);
            Assert.LessOrEqual(result.Position.X, 3.925d);
            Assert.Less(result.Velocity.X, 0d);
        }

        [Test]
        public void RuntimeReplayPlayback_ReplaysMarbleMazeCommandsWithMatchingHashes()
        {
            MarbleMazeOptions options = new MarbleMazeOptions(checkpointCount: 2);
            RuntimeReplaySnapshot snapshot;
            long finalHash;
            using (var runner = CreateRecordedRunner(options))
            {
                snapshot = runner.CreateReplaySnapshot();
                finalHash = runner.LastResultHash;
            }

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner()
                .Play(snapshot, new MarbleMazeReplayFrameDriver(options));

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(snapshot.Count, result.FramesPlayed);
            Assert.AreEqual(finalHash, result.FrameResults[result.FrameResults.Count - 1].ActualResultHash);
            StringAssert.Contains("checkpoints=", result.FrameResults[result.FrameResults.Count - 1].DiagnosticsSummary);
        }

        [Test]
        public void RuntimeReplayPlayback_HashMismatchReportsMarbleMazeFrame()
        {
            MarbleMazeOptions options = new MarbleMazeOptions(checkpointCount: 2);
            RuntimeReplaySnapshot snapshot;
            using (var runner = CreateRecordedRunner(options))
            {
                snapshot = runner.CreateReplaySnapshot();
            }

            var records = new List<RuntimeReplayFrameRecord>(snapshot.Records);
            RuntimeReplayFrameRecord last = records[records.Count - 1];
            records[records.Count - 1] = new RuntimeReplayFrameRecord(
                last.Frame,
                last.Commands,
                last.ResultHash + 1L,
                last.DiagnosticsSummary);
            var mutated = new RuntimeReplaySnapshot(snapshot.Header, records);

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner()
                .Play(mutated, new MarbleMazeReplayFrameDriver(options));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch, result.FailureCode);
            Assert.AreEqual(last.Frame, result.FailureFrame);
            Assert.AreEqual(last.ResultHash + 1L, result.ExpectedResultHash);
        }

        [Test]
        public void RuntimeSaveStateJson_RoundtripRestoresMarbleMazeHashAndCheckpoint()
        {
            MarbleMazeOptions options = new MarbleMazeOptions(checkpointCount: 2);
            RuntimeSaveState saveState;
            long expectedHash;
            int expectedCheckpoint;
            using (var runner = CreateRecordedRunner(options))
            {
                RuntimeSaveStateResult<RuntimeSaveState> saveResult = runner.CaptureSaveState();
                Assert.IsTrue(saveResult.Success, saveResult.Error.ToString());
                saveState = saveResult.Value;
                expectedHash = runner.LastResultHash;
                expectedCheckpoint = runner.Game.CaptureSnapshot().CheckpointsCleared;
            }

            string json = RuntimeSaveStateJson.SaveToJson(saveState);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            Assert.IsTrue(loaded.Success, loaded.Error.ToString());

            using (var restored = new MarbleMazeRuntimeRunner(options))
            {
                RuntimeSaveStateResult<bool> restore = restored.RestoreSaveState(loaded.Value);
                Assert.IsTrue(restore.Success, restore.Error.ToString());
                Assert.AreEqual(expectedHash, restored.LastResultHash);
                Assert.AreEqual(expectedCheckpoint, restored.Game.CaptureSnapshot().CheckpointsCleared);
            }
        }

        [Test]
        public void RuntimeCommandBuffer_RejectsUnknownMarbleMazeCommandIds()
        {
            using (var runner = new MarbleMazeRuntimeRunner())
            {
                RuntimeCommandValidationResult result = runner.EnqueueRuntimeCommand(new RuntimeCommand(
                    RuntimeFrame.Zero,
                    sourceId: 1,
                    commandId: 999999,
                    targetId: 0,
                    traceId: "unknown"));

                Assert.IsFalse(result.Success);
                Assert.AreEqual(RuntimeCommandErrorCode.UnregisteredCommandId, result.Error.Code);
            }
        }

        private static MarbleMazeRuntimeRunner CreateRecordedRunner(MarbleMazeOptions options)
        {
            var runner = new MarbleMazeRuntimeRunner(options);
            runner.EnqueueCommand(0, MarbleMazeCommand.Tilt, payload0: MarbleMazeGame.Encode(0.3d), payload1: MarbleMazeGame.Encode(0.2d));
            runner.EnqueueCommand(0, MarbleMazeCommand.PhysicsSample, payload0: MarbleMazeGame.Encode(0.5d), payload1: MarbleMazeGame.Encode(0.2d), payload2: MarbleMazeGame.Encode(-3.5d));
            runner.EnqueueCommand(1, MarbleMazeCommand.Checkpoint, targetId: 0);
            runner.EnqueueCommand(2, MarbleMazeCommand.PhysicsSample, payload0: MarbleMazeGame.Encode(1.5d), payload1: MarbleMazeGame.Encode(0.2d), payload2: MarbleMazeGame.Encode(-1.5d));
            runner.EnqueueCommand(3, MarbleMazeCommand.Checkpoint, targetId: 1);
            runner.RunFrames(5);
            return runner;
        }
    }
}
