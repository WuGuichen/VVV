using System.Collections.Generic;
using System.IO;
using MxFramework.Demo.Breakout;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.Breakout
{
    public class BreakoutRuntimeValidationTests
    {
        [Test]
        public void BreakoutGame_CommandMovementClampsAndCarriesUnlaunchedBall()
        {
            var game = new BreakoutGame(CreateCompactOptions());
            BreakoutGameSnapshot initial = game.CaptureSnapshot();

            game.ApplyCommand(BreakoutCommand.MoveLeft);

            BreakoutGameSnapshot moved = game.CaptureSnapshot();
            Assert.Less(moved.PaddleX, initial.PaddleX);
            Assert.AreEqual(moved.PaddleX, moved.BallX);
            Assert.IsFalse(moved.IsLaunched);

            for (int i = 0; i < 20; i++)
            {
                game.ApplyCommand(BreakoutCommand.MoveLeft);
            }

            BreakoutGameSnapshot clampedLeft = game.CaptureSnapshot();
            Assert.AreEqual(clampedLeft.PaddleWidth * 0.5d, clampedLeft.PaddleX, 0.0001d);

            game.ApplyCommand(BreakoutCommand.MoveRight);
            Assert.Greater(game.CaptureSnapshot().PaddleX, clampedLeft.PaddleX);
        }

        [Test]
        public void BreakoutGame_BallBouncesAndRemovesBrick()
        {
            BreakoutGameOptions options = CreateCompactOptions();
            var game = new BreakoutGame(options);
            BreakoutRect brick = game.GetBrickBounds(0, 0);
            RestoreBallState(
                game,
                ballX: brick.CenterX,
                ballY: brick.Bottom - options.BallRadius - 0.5d,
                velocityX: 0d,
                velocityY: 2d,
                isLaunched: true);

            game.TickSimulation();

            BreakoutGameSnapshot snapshot = game.CaptureSnapshot();
            Assert.IsFalse(game.IsBrickActive(0, 0));
            Assert.AreEqual(options.ScorePerBrick, snapshot.Score);
            Assert.AreEqual(options.BrickRows * options.BrickColumns - 1, snapshot.BricksRemaining);
            Assert.Less(snapshot.BallVelocityY, 0d);
            Assert.AreEqual(BreakoutPowerUpType.WidePaddle, snapshot.PowerUpType);
            Assert.Greater(snapshot.PaddleWidth, options.PaddleWidth);
        }

        [Test]
        public void BreakoutGame_BottomExitLosesLifeAndResetsBall()
        {
            BreakoutGameOptions options = CreateCompactOptions();
            var game = new BreakoutGame(options);
            RestoreBallState(
                game,
                ballX: options.PlayfieldWidth * 0.5d,
                ballY: -options.BallRadius - 0.5d,
                velocityX: 0d,
                velocityY: -1d,
                isLaunched: true);

            game.TickSimulation();

            BreakoutGameSnapshot snapshot = game.CaptureSnapshot();
            Assert.AreEqual(options.StartingLives - 1, snapshot.Lives);
            Assert.IsFalse(snapshot.IsLaunched);
            Assert.IsFalse(snapshot.IsGameOver);
            Assert.AreEqual(snapshot.PaddleX, snapshot.BallX);
        }

        [Test]
        public void BreakoutGame_UnlaunchedBallRollsAcrossPaddle()
        {
            BreakoutGameOptions options = CreateCompactOptions();
            var game = new BreakoutGame(options);
            BreakoutGameSnapshot initial = game.CaptureSnapshot();

            game.TickSimulation();
            BreakoutGameSnapshot rolled = game.CaptureSnapshot();

            Assert.IsFalse(rolled.IsLaunched);
            Assert.Greater(rolled.BallX, initial.BallX);
            Assert.AreEqual(initial.BallY, rolled.BallY, 0.0001d);

            for (int i = 0; i < 40; i++)
            {
                game.TickSimulation();
            }

            BreakoutGameSnapshot afterBounce = game.CaptureSnapshot();
            Assert.LessOrEqual(afterBounce.BallX, afterBounce.PaddleX + afterBounce.PaddleWidth * 0.5d - afterBounce.BallRadius + 0.0001d);
            Assert.GreaterOrEqual(afterBounce.BallX, afterBounce.PaddleX - afterBounce.PaddleWidth * 0.5d + afterBounce.BallRadius - 0.0001d);
        }

        [Test]
        public void BreakoutGame_LaunchDirectionUsesRolledBallOffset()
        {
            BreakoutGameOptions options = CreateCompactOptions();
            var game = new BreakoutGame(options);

            for (int i = 0; i < 3; i++)
            {
                game.TickSimulation();
            }

            BreakoutGameSnapshot beforeLaunch = game.CaptureSnapshot();
            Assert.Greater(beforeLaunch.BallX, beforeLaunch.PaddleX);

            game.ApplyCommand(BreakoutCommand.Launch);
            BreakoutGameSnapshot launched = game.CaptureSnapshot();

            Assert.IsTrue(launched.IsLaunched);
            Assert.Greater(launched.BallVelocityX, 0d);
            Assert.LessOrEqual(launched.BallVelocityX, options.BallSpeedX);
            Assert.AreEqual(options.BallSpeedY, launched.BallVelocityY, 0.0001d);
        }

        [Test]
        public void BreakoutGame_ConfiguredLevelsExposeBrickTypesAndPowerUps()
        {
            var game = new BreakoutGame();
            BreakoutGameSnapshot snapshot = game.CaptureSnapshot();
            bool hasStrong = false;
            bool hasPowerUp = false;
            for (int i = 0; i < snapshot.Bricks.Count; i++)
            {
                hasStrong |= snapshot.Bricks[i].Type == BreakoutBrickType.Strong;
                hasPowerUp |= snapshot.Bricks[i].Type == BreakoutBrickType.PowerUp
                    && snapshot.Bricks[i].PowerUpType != BreakoutPowerUpType.None;
            }

            Assert.AreEqual(0, snapshot.LevelIndex);
            Assert.IsTrue(hasStrong);
            Assert.IsTrue(hasPowerUp);
        }

        [Test]
        public void BreakoutGame_MultiBallPowerUpAddsDynamicBallSnapshots()
        {
            var game = new BreakoutGame();
            BreakoutRect brick = game.GetBrickBounds(0, 7);
            RestoreBallState(
                game,
                ballX: brick.CenterX,
                ballY: brick.Bottom - 2.5d,
                velocityX: 0d,
                velocityY: 2d,
                isLaunched: true);

            game.TickSimulation();

            BreakoutGameSnapshot snapshot = game.CaptureSnapshot();
            Assert.Greater(snapshot.BallCount, 1);
            Assert.AreEqual("multi-ball", snapshot.LastEvent);
        }

        [Test]
        public void RuntimeReplayPlayback_ReplaysBreakoutCommandSequenceWithMatchingHashes()
        {
            BreakoutGameOptions options = CreateCompactOptions();
            RuntimeReplaySnapshot snapshot;
            long finalHash;

            using (var runner = CreateRecordedRunner(options))
            {
                snapshot = runner.CreateReplaySnapshot();
                finalHash = runner.LastResultHash;
            }

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner()
                .Play(snapshot, new BreakoutReplayFrameDriver(options));

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(snapshot.Count, result.FramesPlayed);
            Assert.Greater(snapshot.Count, 0);
            Assert.AreEqual(finalHash, result.FrameResults[result.FrameResults.Count - 1].ActualResultHash);
            StringAssert.Contains("bricks=", result.FrameResults[result.FrameResults.Count - 1].DiagnosticsSummary);
        }

        [Test]
        public void RuntimeReplayPlayback_HashMismatchReportsBreakoutFrame()
        {
            BreakoutGameOptions options = CreateCompactOptions();
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
                .Play(mutated, new BreakoutReplayFrameDriver(options));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch, result.FailureCode);
            Assert.AreEqual(last.Frame, result.FailureFrame);
            Assert.AreEqual(last.ResultHash + 1L, result.ExpectedResultHash);
        }

        [Test]
        public void RuntimeSaveStateJson_RoundtripRestoresBreakoutHashAndBricks()
        {
            BreakoutGameOptions options = CreateCompactOptions();
            RuntimeSaveState saveState;
            long expectedHash;
            string expectedBricks;
            using (var runner = CreateRecordedRunner(options))
            {
                RuntimeSaveStateResult<RuntimeSaveState> saveResult = runner.CaptureSaveState();
                Assert.IsTrue(saveResult.Success, saveResult.Error.ToString());
                saveState = saveResult.Value;
                expectedHash = runner.LastResultHash;
                expectedBricks = runner.Game.CaptureSnapshot().BrickCode;
            }

            string json = RuntimeSaveStateJson.SaveToJson(saveState);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            Assert.IsTrue(loaded.Success, loaded.Error.ToString());

            using (var restored = new BreakoutRuntimeValidationRunner(options))
            {
                RuntimeSaveStateResult<bool> restoreResult = restored.RestoreSaveState(loaded.Value);

                Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
                Assert.AreEqual(expectedHash, restored.LastResultHash);
                Assert.AreEqual(expectedBricks, restored.Game.CaptureSnapshot().BrickCode);
            }
        }

        [Test]
        public void RuntimeCommandBuffer_RejectsUnknownBreakoutCommandIds()
        {
            using (var runner = new BreakoutRuntimeValidationRunner(CreateCompactOptions()))
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

        [Test]
        public void BreakoutCoreSources_DoNotReferenceUnityAssemblies()
        {
            string gameSource = File.ReadAllText("Assets/Scripts/MxFramework/Demo/Breakout/BreakoutGame.cs");
            string runtimeSource = File.ReadAllText("Assets/Scripts/MxFramework/Demo/Breakout/BreakoutRuntimeValidation.cs");
            string engineToken = "Unity" + "Engine";
            string editorToken = "Unity" + "Editor";

            Assert.IsFalse(gameSource.Contains(engineToken));
            Assert.IsFalse(gameSource.Contains(editorToken));
            Assert.IsFalse(runtimeSource.Contains(engineToken));
            Assert.IsFalse(runtimeSource.Contains(editorToken));
        }

        private static BreakoutGameOptions CreateCompactOptions()
        {
            return new BreakoutGameOptions(
                playfieldWidth: 80d,
                playfieldHeight: 80d,
                paddleY: 6d,
                paddleWidth: 18d,
                paddleHeight: 4d,
                paddleMoveStep: 6d,
                ballRadius: 2d,
                ballSpeedX: 1d,
                ballSpeedY: 2d,
                preLaunchBallRollStep: 1d,
                brickRows: 2,
                brickColumns: 4,
                brickTopY: 70d,
                brickHeight: 6d,
                brickGap: 1d,
                startingLives: 3,
                scorePerBrick: 10,
                powerUpDurationFrames: 20);
        }

        private static BreakoutRuntimeValidationRunner CreateRecordedRunner(BreakoutGameOptions options)
        {
            var runner = new BreakoutRuntimeValidationRunner(options);
            runner.EnqueueCommand(0, BreakoutCommand.Launch);
            runner.EnqueueCommand(1, BreakoutCommand.MoveLeft);
            runner.EnqueueCommand(2, BreakoutCommand.MoveRight);
            runner.EnqueueCommand(5, BreakoutCommand.Pause);
            runner.EnqueueCommand(6, BreakoutCommand.Pause);
            runner.EnqueueCommand(8, BreakoutCommand.MoveRight);
            runner.RunFrames(40);
            return runner;
        }

        private static void RestoreBallState(
            BreakoutGame game,
            double ballX,
            double ballY,
            double velocityX,
            double velocityY,
            bool isLaunched)
        {
            BreakoutGameState state = game.CaptureState();
            game.RestoreState(new BreakoutGameState(
                state.PlayfieldWidth,
                state.PlayfieldHeight,
                state.BrickRows,
                state.BrickColumns,
                state.Bricks,
                state.PaddleX,
                ballX,
                ballY,
                velocityX,
                velocityY,
                isLaunched,
                state.IsPaused,
                state.Score,
                state.Lives,
                state.IsWin,
                state.IsGameOver,
                state.PowerUpType,
                state.PowerUpTimerFrames,
                state.BrickTypes,
                state.BrickHitPoints,
                state.BrickPowerUps,
                state.Balls,
                state.LevelIndex,
                state.EventCount,
                state.LastEvent));
        }
    }
}
