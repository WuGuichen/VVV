using System.Collections.Generic;
using MxFramework.Demo.Tetris;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.Tetris
{
    public class TetrisRuntimeValidationTests
    {
        [Test]
        public void TetrisGame_HardDropLocksPieceAndClearsLine()
        {
            var options = new TetrisGameOptions(pieceQueue: new[] { TetrisPieceType.I, TetrisPieceType.O });
            var game = new TetrisGame(options);
            TetrisGameState initial = game.CaptureState();
            var cells = new List<int>(initial.Cells);
            for (int x = 0; x < initial.Width; x++)
            {
                if (x < 3 || x > 6)
                {
                    cells[x] = (int)TetrisPieceType.L;
                }
            }

            game.RestoreState(new TetrisGameState(
                initial.Width,
                initial.Height,
                cells,
                initial.PieceQueue,
                initial.QueueIndex,
                initial.HasActivePiece,
                initial.ActivePieceType,
                initial.ActiveRotation,
                initial.ActiveX,
                initial.ActiveY,
                initial.Score,
                initial.LinesCleared,
                initial.LockedPieces,
                initial.IsGameOver,
                initial.GravityCounter));

            game.ApplyCommand(TetrisCommand.HardDrop);

            TetrisGameSnapshot snapshot = game.CaptureSnapshot();
            Assert.AreEqual(1, snapshot.LinesCleared);
            Assert.AreEqual(100, snapshot.Score);
            Assert.AreEqual(1, snapshot.LockedPieces);
            StringAssert.EndsWith("..........", snapshot.BoardCode);
        }

        [Test]
        public void TetrisGame_GravityDoesNotChangeHorizontalPositionWithoutCommands()
        {
            var options = new TetrisGameOptions(
                gravityIntervalFrames: 1,
                pieceQueue: new[] { TetrisPieceType.I, TetrisPieceType.O });
            var game = new TetrisGame(options);

            TetrisGameSnapshot initial = game.CaptureSnapshot();
            for (int i = 0; i < 5; i++)
                game.TickGravity();

            TetrisGameSnapshot snapshot = game.CaptureSnapshot();
            Assert.AreEqual(initial.ActiveX, snapshot.ActiveX);
            Assert.Less(snapshot.ActiveY, initial.ActiveY);
        }

        [Test]
        public void RuntimeReplayPlayback_ReplaysTetrisCommandSequenceWithMatchingHashes()
        {
            TetrisGameOptions options = CreateReplayOptions();
            RuntimeReplaySnapshot snapshot;
            long finalHash;

            using (var runner = CreateRecordedRunner(options))
            {
                snapshot = runner.CreateReplaySnapshot();
                finalHash = runner.LastResultHash;
            }

            var playback = new RuntimeReplayPlaybackRunner();
            RuntimeReplayPlaybackResult result = playback.Play(snapshot, new TetrisReplayFrameDriver(options));

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(snapshot.Count, result.FramesPlayed);
            Assert.Greater(snapshot.Count, 0);
            Assert.AreEqual(finalHash, result.FrameResults[result.FrameResults.Count - 1].ActualResultHash);
            StringAssert.Contains("score=", result.FrameResults[result.FrameResults.Count - 1].DiagnosticsSummary);
        }

        [Test]
        public void RuntimeReplayPlayback_HashMismatchReportsFrameAndCommands()
        {
            TetrisGameOptions options = CreateReplayOptions();
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
                .Play(mutated, new TetrisReplayFrameDriver(options));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch, result.FailureCode);
            Assert.AreEqual(last.Frame, result.FailureFrame);
            Assert.AreEqual(last.ResultHash + 1L, result.ExpectedResultHash);
        }

        [Test]
        public void RuntimeSaveStateJson_RoundtripRestoresTetrisHashAndBoard()
        {
            TetrisGameOptions options = CreateReplayOptions();
            RuntimeSaveState saveState;
            long expectedHash;
            string expectedBoard;
            using (var runner = CreateRecordedRunner(options))
            {
                RuntimeSaveStateResult<RuntimeSaveState> saveResult = runner.CaptureSaveState();
                Assert.IsTrue(saveResult.Success, saveResult.Error.ToString());
                saveState = saveResult.Value;
                expectedHash = runner.LastResultHash;
                expectedBoard = runner.Game.CaptureSnapshot().BoardWithActiveCode;
            }

            string json = RuntimeSaveStateJson.SaveToJson(saveState);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            Assert.IsTrue(loaded.Success, loaded.Error.ToString());

            using (var restored = new TetrisRuntimeValidationRunner(options))
            {
                RuntimeSaveStateResult<bool> restoreResult = restored.RestoreSaveState(loaded.Value);

                Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
                Assert.AreEqual(expectedHash, restored.LastResultHash);
                Assert.AreEqual(expectedBoard, restored.Game.CaptureSnapshot().BoardWithActiveCode);
            }
        }

        [Test]
        public void RuntimeCommandBuffer_RejectsUnknownTetrisCommandIds()
        {
            using (var runner = new TetrisRuntimeValidationRunner(CreateReplayOptions()))
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

        private static TetrisGameOptions CreateReplayOptions()
        {
            return new TetrisGameOptions(
                gravityIntervalFrames: 4,
                pieceQueue: new[]
                {
                    TetrisPieceType.I,
                    TetrisPieceType.T,
                    TetrisPieceType.O,
                    TetrisPieceType.L
                });
        }

        private static TetrisRuntimeValidationRunner CreateRecordedRunner(TetrisGameOptions options)
        {
            var runner = new TetrisRuntimeValidationRunner(options);
            runner.EnqueueCommand(0, TetrisCommand.MoveLeft);
            runner.EnqueueCommand(1, TetrisCommand.RotateClockwise);
            runner.EnqueueCommand(2, TetrisCommand.MoveRight);
            runner.EnqueueCommand(3, TetrisCommand.SoftDrop);
            runner.EnqueueCommand(4, TetrisCommand.HardDrop);
            runner.EnqueueCommand(7, TetrisCommand.MoveRight);
            runner.EnqueueCommand(8, TetrisCommand.RotateClockwise);
            runner.RunFrames(12);
            return runner;
        }
    }
}
