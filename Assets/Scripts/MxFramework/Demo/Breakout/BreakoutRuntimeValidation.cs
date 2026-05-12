using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Demo.Breakout
{
    public static class BreakoutRuntimeCommandIds
    {
        public const int MoveLeft = 21001;
        public const int MoveRight = 21002;
        public const int Launch = 21003;
        public const int Pause = 21004;
        public const int Restart = 21005;

        public static int ToCommandId(BreakoutCommand command)
        {
            switch (command)
            {
                case BreakoutCommand.MoveLeft:
                    return MoveLeft;
                case BreakoutCommand.MoveRight:
                    return MoveRight;
                case BreakoutCommand.Launch:
                    return Launch;
                case BreakoutCommand.Pause:
                    return Pause;
                case BreakoutCommand.Restart:
                    return Restart;
                default:
                    return 0;
            }
        }

        public static bool TryToCommand(int commandId, out BreakoutCommand command)
        {
            switch (commandId)
            {
                case MoveLeft:
                    command = BreakoutCommand.MoveLeft;
                    return true;
                case MoveRight:
                    command = BreakoutCommand.MoveRight;
                    return true;
                case Launch:
                    command = BreakoutCommand.Launch;
                    return true;
                case Pause:
                    command = BreakoutCommand.Pause;
                    return true;
                case Restart:
                    command = BreakoutCommand.Restart;
                    return true;
                default:
                    command = BreakoutCommand.None;
                    return false;
            }
        }
    }

    public sealed class BreakoutRuntimeModule : RuntimeModule, IRuntimeSaveStateProvider, IRuntimeSaveStateRestorer
    {
        public const string DefaultModuleId = "breakout-validation";
        public const string CustomStateTypeId = "mxframework.demo.breakout.state";
        private const int CustomStateSchemaVersion = 0;

        private readonly RuntimeCommandBuffer _commands;
        private readonly RuntimeReplayRecorder _recorder;

        public BreakoutRuntimeModule(BreakoutGame game)
            : this(game, DefaultModuleId)
        {
        }

        public BreakoutRuntimeModule(BreakoutGame game, string moduleId)
            : base(moduleId, RuntimeTickStage.Simulation, 0)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = new RuntimeCommandBuffer(new BreakoutCommandValidator());
            _recorder = new RuntimeReplayRecorder(new RuntimeReplayHeader(
                1,
                "breakout-validation-v0.1",
                "fixed-brick-grid",
                "none",
                RuntimeFrame.Zero));
        }

        public BreakoutGame Game { get; }
        public long LastResultHash { get; private set; }
        public RuntimeFrame LastFrame { get; private set; } = RuntimeFrame.Zero;
        public string LastDiagnosticsSummary { get; private set; } = string.Empty;
        public int ReplayFrameCount => _recorder.Count;

        public RuntimeCommandValidationResult EnqueueCommand(RuntimeFrame frame, BreakoutCommand command, int sourceId = 1)
        {
            int commandId = BreakoutRuntimeCommandIds.ToCommandId(command);
            var runtimeCommand = new RuntimeCommand(
                frame,
                sourceId,
                commandId,
                targetId: 0,
                traceId: command.ToString());
            return EnqueueRuntimeCommand(runtimeCommand);
        }

        public RuntimeCommandValidationResult EnqueueRuntimeCommand(RuntimeCommand command)
        {
            return _commands.Enqueue(command);
        }

        public override void Tick(RuntimeTickContext context)
        {
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            IReadOnlyList<RuntimeCommand> drained = _commands.DrainForFrame(frame);
            for (int i = 0; i < drained.Count; i++)
            {
                BreakoutCommand command;
                if (BreakoutRuntimeCommandIds.TryToCommand(drained[i].CommandId, out command))
                {
                    Game.ApplyCommand(command);
                }
            }

            Game.TickSimulation();
            LastFrame = frame;
            LastResultHash = Game.ComputeStableHash(frame);
            LastDiagnosticsSummary = Game.CaptureSnapshot().ToDiagnosticsSummary(frame);
            _recorder.RecordFrame(frame, drained, LastResultHash, LastDiagnosticsSummary);
        }

        public RuntimeReplaySnapshot CreateReplaySnapshot()
        {
            return _recorder.CreateSnapshot();
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            string payload = BreakoutGameStateCodec.Encode(Game.CaptureState());
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CustomStateSchemaVersion,
                new RuntimeCustomState(CustomStateTypeId, CustomStateSchemaVersion, payload));
            var state = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "breakout-validation-v0.1",
                "fixed-brick-grid",
                "none",
                LastFrame.Value,
                null,
                null,
                new[] { moduleState },
                new Dictionary<string, string>
                {
                    { "fixture", "breakout-runtime-validation" }
                });
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(state);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Breakout save state cannot be null."));
            }

            RuntimeModuleSaveState moduleState = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (string.Equals(candidate.ModuleId, ModuleId, StringComparison.Ordinal))
                {
                    moduleState = candidate;
                    break;
                }
            }

            if (moduleState == null || moduleState.CustomState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates",
                    "Breakout module state is missing."));
            }

            if (!string.Equals(moduleState.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates.breakout.customState.typeId",
                    "Breakout module custom state type mismatch."));
            }

            try
            {
                BreakoutGameState gameState = BreakoutGameStateCodec.Decode(moduleState.CustomState.PayloadJson);
                Game.RestoreState(gameState);
                LastFrame = new RuntimeFrame(saveState.Frame < 0 ? 0 : saveState.Frame);
                LastResultHash = Game.ComputeStableHash(LastFrame);
                LastDiagnosticsSummary = Game.CaptureSnapshot().ToDiagnosticsSummary(LastFrame);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
            catch (Exception exception)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates.breakout.customState.payloadJson",
                    "Breakout module custom state could not be decoded: " + exception.Message,
                    exception: exception));
            }
        }

        private sealed class BreakoutCommandValidator : IRuntimeCommandValidator
        {
            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                BreakoutCommand ignored;
                if (!BreakoutRuntimeCommandIds.TryToCommand(command.CommandId, out ignored))
                {
                    return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.UnregisteredCommandId,
                        command,
                        RuntimeFrame.Zero,
                        "Runtime command is not a Breakout validation command."));
                }

                return RuntimeCommandValidationResult.Accepted(command);
            }
        }
    }

    public sealed class BreakoutRuntimeValidationRunner : IDisposable
    {
        private readonly RuntimeHost _host;

        public BreakoutRuntimeValidationRunner()
            : this(new BreakoutGameOptions())
        {
        }

        public BreakoutRuntimeValidationRunner(BreakoutGameOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Module = new BreakoutRuntimeModule(new BreakoutGame(Options));
            _host = new RuntimeHost();
            _host.RegisterModule(Module);
            _host.Initialize();
            _host.Start();
        }

        public BreakoutGameOptions Options { get; }
        public BreakoutRuntimeModule Module { get; }
        public BreakoutGame Game => Module.Game;
        public long LastResultHash => Module.LastResultHash;
        public string LastDiagnosticsSummary => Module.LastDiagnosticsSummary;

        public RuntimeCommandValidationResult EnqueueCommand(long frame, BreakoutCommand command)
        {
            return Module.EnqueueCommand(new RuntimeFrame(frame), command);
        }

        public RuntimeCommandValidationResult EnqueueRuntimeCommand(RuntimeCommand command)
        {
            return Module.EnqueueRuntimeCommand(command);
        }

        public void TickFrame(long frame)
        {
            _host.Tick(frame, 1d / 60d, frame / 60d);
        }

        public void RunFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                TickFrame(i);
            }
        }

        public RuntimeReplaySnapshot CreateReplaySnapshot()
        {
            return Module.CreateReplaySnapshot();
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            return Module.CaptureSaveState();
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            return Module.RestoreSaveState(saveState);
        }

        public void Dispose()
        {
            _host.Dispose();
        }
    }

    public sealed class BreakoutReplayFrameDriver : IRuntimeReplayFrameDriver
    {
        private readonly BreakoutGameOptions _options;
        private BreakoutRuntimeValidationRunner _runner;

        public BreakoutReplayFrameDriver(BreakoutGameOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public BreakoutRuntimeValidationRunner Runner => _runner;

        public void Reset(RuntimeReplayHeader header)
        {
            if (_runner != null)
            {
                _runner.Dispose();
            }

            _runner = new BreakoutRuntimeValidationRunner(_options);
        }

        public RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record)
        {
            var errors = new List<RuntimeCommandError>();
            for (int i = 0; i < record.Commands.Count; i++)
            {
                RuntimeCommandValidationResult result = _runner.EnqueueRuntimeCommand(record.Commands[i]);
                if (!result.Success)
                {
                    errors.Add(result.Error);
                }
            }

            _runner.TickFrame(record.Frame.Value);
            return new RuntimeReplayPlaybackFrameResult(
                record.Frame,
                _runner.LastResultHash,
                _runner.LastDiagnosticsSummary,
                errors);
        }
    }

    public static class BreakoutGameStateCodec
    {
        public static string Encode(BreakoutGameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var payload = new BreakoutGameStatePayload
            {
                Version = 2,
                PlayfieldWidth = state.PlayfieldWidth,
                PlayfieldHeight = state.PlayfieldHeight,
                BrickRows = state.BrickRows,
                BrickColumns = state.BrickColumns,
                Bricks = EncodeBricks(state.Bricks),
                PaddleX = state.PaddleX,
                BallX = state.BallX,
                BallY = state.BallY,
                BallVelocityX = state.BallVelocityX,
                BallVelocityY = state.BallVelocityY,
                IsLaunched = state.IsLaunched,
                IsPaused = state.IsPaused,
                Score = state.Score,
                Lives = state.Lives,
                IsWin = state.IsWin,
                IsGameOver = state.IsGameOver,
                PowerUpType = (int)state.PowerUpType,
                PowerUpTimerFrames = state.PowerUpTimerFrames,
                BrickTypes = new List<int>(state.BrickTypes),
                BrickHitPoints = new List<int>(state.BrickHitPoints),
                BrickPowerUps = new List<int>(state.BrickPowerUps),
                Balls = EncodeBalls(state.Balls),
                LevelIndex = state.LevelIndex,
                EventCount = state.EventCount,
                LastEvent = state.LastEvent
            };
            return JsonConvert.SerializeObject(payload, Formatting.None);
        }

        public static BreakoutGameState Decode(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new FormatException("Breakout payload is empty.");
            }

            BreakoutGameStatePayload state = JsonConvert.DeserializeObject<BreakoutGameStatePayload>(payload);
            if (state == null)
            {
                throw new FormatException("Breakout payload could not be deserialized.");
            }

            if (state.Version != 1 && state.Version != 2)
            {
                throw new FormatException("Unsupported Breakout payload version.");
            }

            if (state.Bricks == null)
            {
                throw new FormatException("Breakout payload is missing bricks.");
            }

            return new BreakoutGameState(
                state.PlayfieldWidth,
                state.PlayfieldHeight,
                state.BrickRows,
                state.BrickColumns,
                DecodeBricks(state.Bricks),
                state.PaddleX,
                state.BallX,
                state.BallY,
                state.BallVelocityX,
                state.BallVelocityY,
                state.IsLaunched,
                state.IsPaused,
                state.Score,
                state.Lives,
                state.IsWin,
                state.IsGameOver,
                (BreakoutPowerUpType)state.PowerUpType,
                state.PowerUpTimerFrames,
                state.BrickTypes,
                state.BrickHitPoints,
                state.BrickPowerUps,
                DecodeBalls(state.Balls),
                state.LevelIndex,
                state.EventCount,
                state.LastEvent);
        }

        private static List<int> EncodeBricks(IReadOnlyList<bool> bricks)
        {
            var encoded = new List<int>(bricks.Count);
            for (int i = 0; i < bricks.Count; i++)
            {
                encoded.Add(bricks[i] ? 1 : 0);
            }

            return encoded;
        }

        private static List<bool> DecodeBricks(IReadOnlyList<int> encoded)
        {
            var bricks = new List<bool>(encoded.Count);
            for (int i = 0; i < encoded.Count; i++)
            {
                bricks.Add(encoded[i] != 0);
            }

            return bricks;
        }

        private static List<BreakoutBallState> DecodeBalls(IReadOnlyList<BreakoutBallStatePayload> encoded)
        {
            var balls = new List<BreakoutBallState>();
            if (encoded == null)
            {
                return balls;
            }

            for (int i = 0; i < encoded.Count; i++)
            {
                BreakoutBallStatePayload ball = encoded[i];
                balls.Add(new BreakoutBallState(ball.Id, ball.X, ball.Y, ball.VelocityX, ball.VelocityY));
            }

            return balls;
        }

        private static List<BreakoutBallStatePayload> EncodeBalls(IReadOnlyList<BreakoutBallState> balls)
        {
            var encoded = new List<BreakoutBallStatePayload>();
            if (balls == null)
            {
                return encoded;
            }

            for (int i = 0; i < balls.Count; i++)
            {
                BreakoutBallState ball = balls[i];
                encoded.Add(new BreakoutBallStatePayload
                {
                    Id = ball.Id,
                    X = ball.X,
                    Y = ball.Y,
                    VelocityX = ball.VelocityX,
                    VelocityY = ball.VelocityY
                });
            }

            return encoded;
        }

        private sealed class BreakoutGameStatePayload
        {
            public int Version { get; set; }
            public double PlayfieldWidth { get; set; }
            public double PlayfieldHeight { get; set; }
            public int BrickRows { get; set; }
            public int BrickColumns { get; set; }
            public List<int> Bricks { get; set; }
            public List<int> BrickTypes { get; set; }
            public List<int> BrickHitPoints { get; set; }
            public List<int> BrickPowerUps { get; set; }
            public List<BreakoutBallStatePayload> Balls { get; set; }
            public double PaddleX { get; set; }
            public double BallX { get; set; }
            public double BallY { get; set; }
            public double BallVelocityX { get; set; }
            public double BallVelocityY { get; set; }
            public bool IsLaunched { get; set; }
            public bool IsPaused { get; set; }
            public int Score { get; set; }
            public int Lives { get; set; }
            public bool IsWin { get; set; }
            public bool IsGameOver { get; set; }
            public int PowerUpType { get; set; }
            public int PowerUpTimerFrames { get; set; }
            public int LevelIndex { get; set; }
            public int EventCount { get; set; }
            public string LastEvent { get; set; }
        }

        private sealed class BreakoutBallStatePayload
        {
            public int Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double VelocityX { get; set; }
            public double VelocityY { get; set; }
        }
    }
}
