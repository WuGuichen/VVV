using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Demo.MarbleMaze
{
    public enum MarbleMazeCommand
    {
        None = 0,
        Tilt = 1,
        PhysicsSample = 2,
        Checkpoint = 3,
        Finish = 4,
        Reset = 5,
        Pause = 6
    }

    public readonly struct MarbleMazeVector3
    {
        public MarbleMazeVector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static MarbleMazeVector3 Zero => new MarbleMazeVector3(0d, 0d, 0d);
    }

    public sealed class MarbleMazeOptions
    {
        public MarbleMazeOptions(
            int checkpointCount = 2,
            double targetTimeSeconds = 45d,
            double fallY = -2.5d,
            double positionHashScale = 1000d,
            double tiltHashScale = 1000d)
        {
            if (checkpointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount), "Marble Maze checkpoint count must be positive.");
            if (targetTimeSeconds <= 0d || double.IsNaN(targetTimeSeconds) || double.IsInfinity(targetTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(targetTimeSeconds), "Marble Maze target time must be finite and positive.");
            if (positionHashScale <= 0d || tiltHashScale <= 0d)
                throw new ArgumentOutOfRangeException(nameof(positionHashScale), "Marble Maze hash scales must be positive.");

            CheckpointCount = checkpointCount;
            TargetTimeSeconds = targetTimeSeconds;
            FallY = fallY;
            PositionHashScale = positionHashScale;
            TiltHashScale = tiltHashScale;
        }

        public int CheckpointCount { get; }
        public double TargetTimeSeconds { get; }
        public double FallY { get; }
        public double PositionHashScale { get; }
        public double TiltHashScale { get; }
    }

    public sealed class MarbleMazeState
    {
        public MarbleMazeState(
            int checkpointCount,
            int nextCheckpointIndex,
            double elapsedSeconds,
            double bestTimeSeconds,
            bool hasBestTime,
            bool isPaused,
            bool isFinished,
            int fallCount,
            int resetCount,
            MarbleMazeVector3 ballPosition,
            MarbleMazeVector3 ballVelocity,
            double tiltX,
            double tiltZ,
            int eventCount,
            string lastEvent)
        {
            if (checkpointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount), "Marble Maze state checkpoint count must be positive.");
            if (nextCheckpointIndex < 0 || nextCheckpointIndex > checkpointCount)
                throw new ArgumentOutOfRangeException(nameof(nextCheckpointIndex), "Marble Maze next checkpoint index is outside the route.");
            if (elapsedSeconds < 0d || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Marble Maze elapsed time must be finite and non-negative.");
            if (bestTimeSeconds < 0d || double.IsNaN(bestTimeSeconds) || double.IsInfinity(bestTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(bestTimeSeconds), "Marble Maze best time must be finite and non-negative.");

            CheckpointCount = checkpointCount;
            NextCheckpointIndex = nextCheckpointIndex;
            ElapsedSeconds = elapsedSeconds;
            BestTimeSeconds = bestTimeSeconds;
            HasBestTime = hasBestTime;
            IsPaused = isPaused;
            IsFinished = isFinished;
            FallCount = Math.Max(0, fallCount);
            ResetCount = Math.Max(0, resetCount);
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            TiltX = tiltX;
            TiltZ = tiltZ;
            EventCount = Math.Max(0, eventCount);
            LastEvent = lastEvent ?? string.Empty;
        }

        public int CheckpointCount { get; }
        public int NextCheckpointIndex { get; }
        public double ElapsedSeconds { get; }
        public double BestTimeSeconds { get; }
        public bool HasBestTime { get; }
        public bool IsPaused { get; }
        public bool IsFinished { get; }
        public int FallCount { get; }
        public int ResetCount { get; }
        public MarbleMazeVector3 BallPosition { get; }
        public MarbleMazeVector3 BallVelocity { get; }
        public double TiltX { get; }
        public double TiltZ { get; }
        public int EventCount { get; }
        public string LastEvent { get; }
    }

    public sealed class MarbleMazeSnapshot
    {
        public MarbleMazeSnapshot(
            int nextCheckpointIndex,
            int checkpointCount,
            double elapsedSeconds,
            double bestTimeSeconds,
            bool hasBestTime,
            bool isPaused,
            bool isFinished,
            int fallCount,
            int resetCount,
            MarbleMazeVector3 ballPosition,
            MarbleMazeVector3 ballVelocity,
            double tiltX,
            double tiltZ,
            int eventCount,
            string lastEvent)
        {
            NextCheckpointIndex = nextCheckpointIndex;
            CheckpointCount = checkpointCount;
            ElapsedSeconds = elapsedSeconds;
            BestTimeSeconds = bestTimeSeconds;
            HasBestTime = hasBestTime;
            IsPaused = isPaused;
            IsFinished = isFinished;
            FallCount = fallCount;
            ResetCount = resetCount;
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            TiltX = tiltX;
            TiltZ = tiltZ;
            EventCount = eventCount;
            LastEvent = lastEvent ?? string.Empty;
        }

        public int NextCheckpointIndex { get; }
        public int CheckpointCount { get; }
        public double ElapsedSeconds { get; }
        public double BestTimeSeconds { get; }
        public bool HasBestTime { get; }
        public bool IsPaused { get; }
        public bool IsFinished { get; }
        public int FallCount { get; }
        public int ResetCount { get; }
        public MarbleMazeVector3 BallPosition { get; }
        public MarbleMazeVector3 BallVelocity { get; }
        public double TiltX { get; }
        public double TiltZ { get; }
        public int EventCount { get; }
        public string LastEvent { get; }
        public int CheckpointsCleared => Math.Min(CheckpointCount, NextCheckpointIndex);

        public string ToDiagnosticsSummary(RuntimeFrame frame)
        {
            return "frame=" + frame.Value
                + " checkpoints=" + CheckpointsCleared + "/" + CheckpointCount
                + " elapsed=" + ElapsedSeconds.ToString("0.000")
                + " falls=" + FallCount
                + " finished=" + IsFinished
                + " event=" + LastEvent;
        }
    }

    public sealed class MarbleMazeGame : IRuntimeHashContributor
    {
        private readonly MarbleMazeOptions _options;
        private int _nextCheckpointIndex;
        private double _elapsedSeconds;
        private double _bestTimeSeconds;
        private bool _hasBestTime;
        private bool _isPaused;
        private bool _isFinished;
        private int _fallCount;
        private int _resetCount;
        private MarbleMazeVector3 _ballPosition;
        private MarbleMazeVector3 _ballVelocity;
        private double _tiltX;
        private double _tiltZ;
        private int _eventCount;
        private string _lastEvent = "ready";

        public MarbleMazeGame(MarbleMazeOptions options = null)
        {
            _options = options ?? new MarbleMazeOptions();
            ResetRun();
        }

        public string ContributorId => "mxframework.demo.marble-maze";
        public MarbleMazeOptions Options => _options;

        public void Tick(double deltaTime)
        {
            if (_isPaused || _isFinished)
                return;

            _elapsedSeconds += Math.Max(0d, deltaTime);
            if (_ballPosition.Y < _options.FallY)
            {
                _fallCount++;
                _lastEvent = "fall";
                _eventCount++;
            }
        }

        public void ApplyCommand(MarbleMazeCommand command, RuntimeCommand runtimeCommand)
        {
            switch (command)
            {
                case MarbleMazeCommand.Tilt:
                    _tiltX = Decode(runtimeCommand.Payload0);
                    _tiltZ = Decode(runtimeCommand.Payload1);
                    _lastEvent = "tilt";
                    break;
                case MarbleMazeCommand.PhysicsSample:
                    _ballPosition = new MarbleMazeVector3(
                        Decode(runtimeCommand.Payload0),
                        Decode(runtimeCommand.Payload1),
                        Decode(runtimeCommand.Payload2));
                    _lastEvent = "sample";
                    break;
                case MarbleMazeCommand.Checkpoint:
                    AcceptCheckpoint(runtimeCommand.TargetId);
                    break;
                case MarbleMazeCommand.Finish:
                    FinishRun();
                    break;
                case MarbleMazeCommand.Reset:
                    ResetRun();
                    break;
                case MarbleMazeCommand.Pause:
                    _isPaused = runtimeCommand.Payload0 != 0;
                    _lastEvent = _isPaused ? "pause" : "resume";
                    break;
            }

            if (command != MarbleMazeCommand.None)
                _eventCount++;
        }

        public void ApplyVelocitySample(double x, double y, double z)
        {
            _ballVelocity = new MarbleMazeVector3(x, y, z);
        }

        public MarbleMazeSnapshot CaptureSnapshot()
        {
            return new MarbleMazeSnapshot(
                _nextCheckpointIndex,
                _options.CheckpointCount,
                _elapsedSeconds,
                _bestTimeSeconds,
                _hasBestTime,
                _isPaused,
                _isFinished,
                _fallCount,
                _resetCount,
                _ballPosition,
                _ballVelocity,
                _tiltX,
                _tiltZ,
                _eventCount,
                _lastEvent);
        }

        public MarbleMazeState CaptureState()
        {
            return new MarbleMazeState(
                _options.CheckpointCount,
                _nextCheckpointIndex,
                _elapsedSeconds,
                _bestTimeSeconds,
                _hasBestTime,
                _isPaused,
                _isFinished,
                _fallCount,
                _resetCount,
                _ballPosition,
                _ballVelocity,
                _tiltX,
                _tiltZ,
                _eventCount,
                _lastEvent);
        }

        public void RestoreState(MarbleMazeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.CheckpointCount != _options.CheckpointCount)
                throw new InvalidOperationException("Marble Maze checkpoint count in save state does not match options.");

            _nextCheckpointIndex = state.NextCheckpointIndex;
            _elapsedSeconds = state.ElapsedSeconds;
            _bestTimeSeconds = state.BestTimeSeconds;
            _hasBestTime = state.HasBestTime;
            _isPaused = state.IsPaused;
            _isFinished = state.IsFinished;
            _fallCount = state.FallCount;
            _resetCount = state.ResetCount;
            _ballPosition = state.BallPosition;
            _ballVelocity = state.BallVelocity;
            _tiltX = state.TiltX;
            _tiltZ = state.TiltZ;
            _eventCount = state.EventCount;
            _lastEvent = state.LastEvent;
        }

        public long ComputeStableHash(RuntimeFrame frame)
        {
            return RuntimeHashCombiner.ComputeHash(frame, new[] { this });
        }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt("checkpoint.next", _nextCheckpointIndex);
            accumulator.AddDoubleQuantized("elapsed", _elapsedSeconds, 1000d);
            accumulator.AddInt("paused", _isPaused ? 1 : 0);
            accumulator.AddInt("finished", _isFinished ? 1 : 0);
            accumulator.AddInt("falls", _fallCount);
            accumulator.AddInt("resets", _resetCount);
            accumulator.AddDoubleQuantized("pos.x", _ballPosition.X, _options.PositionHashScale);
            accumulator.AddDoubleQuantized("pos.y", _ballPosition.Y, _options.PositionHashScale);
            accumulator.AddDoubleQuantized("pos.z", _ballPosition.Z, _options.PositionHashScale);
            accumulator.AddDoubleQuantized("tilt.x", _tiltX, _options.TiltHashScale);
            accumulator.AddDoubleQuantized("tilt.z", _tiltZ, _options.TiltHashScale);
            accumulator.AddInt("events", _eventCount);
        }

        private void AcceptCheckpoint(int checkpointIndex)
        {
            if (_isFinished || checkpointIndex != _nextCheckpointIndex)
            {
                _lastEvent = "checkpoint-rejected";
                return;
            }

            _nextCheckpointIndex++;
            _lastEvent = "checkpoint-" + checkpointIndex;
        }

        private void FinishRun()
        {
            if (_isFinished)
                return;

            if (_nextCheckpointIndex < _options.CheckpointCount)
            {
                _lastEvent = "exit-locked";
                _eventCount++;
                return;
            }

            _isFinished = true;
            if (!_hasBestTime || _elapsedSeconds < _bestTimeSeconds)
            {
                _bestTimeSeconds = _elapsedSeconds;
                _hasBestTime = true;
            }

            _lastEvent = "finish";
        }

        private void ResetRun()
        {
            _nextCheckpointIndex = 0;
            _elapsedSeconds = 0d;
            _isPaused = false;
            _isFinished = false;
            _ballPosition = MarbleMazeVector3.Zero;
            _ballVelocity = MarbleMazeVector3.Zero;
            _tiltX = 0d;
            _tiltZ = 0d;
            _resetCount++;
            _lastEvent = "reset";
        }

        public static int Encode(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;
            return (int)Math.Round(value * 1000d);
        }

        public static double Decode(int value)
        {
            return value / 1000d;
        }
    }

    public static class MarbleMazeRuntimeCommandIds
    {
        public const int Tilt = 22001;
        public const int PhysicsSample = 22002;
        public const int Checkpoint = 22003;
        public const int Finish = 22004;
        public const int Reset = 22005;
        public const int Pause = 22006;

        public static int ToCommandId(MarbleMazeCommand command)
        {
            switch (command)
            {
                case MarbleMazeCommand.Tilt:
                    return Tilt;
                case MarbleMazeCommand.PhysicsSample:
                    return PhysicsSample;
                case MarbleMazeCommand.Checkpoint:
                    return Checkpoint;
                case MarbleMazeCommand.Finish:
                    return Finish;
                case MarbleMazeCommand.Reset:
                    return Reset;
                case MarbleMazeCommand.Pause:
                    return Pause;
                default:
                    return 0;
            }
        }

        public static bool TryToCommand(int commandId, out MarbleMazeCommand command)
        {
            switch (commandId)
            {
                case Tilt:
                    command = MarbleMazeCommand.Tilt;
                    return true;
                case PhysicsSample:
                    command = MarbleMazeCommand.PhysicsSample;
                    return true;
                case Checkpoint:
                    command = MarbleMazeCommand.Checkpoint;
                    return true;
                case Finish:
                    command = MarbleMazeCommand.Finish;
                    return true;
                case Reset:
                    command = MarbleMazeCommand.Reset;
                    return true;
                case Pause:
                    command = MarbleMazeCommand.Pause;
                    return true;
                default:
                    command = MarbleMazeCommand.None;
                    return false;
            }
        }
    }

    public sealed class MarbleMazeRuntimeModule : RuntimeModule, IRuntimeSaveStateProvider, IRuntimeSaveStateRestorer
    {
        public const string DefaultModuleId = "marble-maze-runtime";
        public const string CustomStateTypeId = "mxframework.demo.marble-maze.state";
        private const int CustomStateSchemaVersion = 1;

        private readonly RuntimeCommandBuffer _commands;
        private readonly RuntimeReplayRecorder _recorder;

        public MarbleMazeRuntimeModule(MarbleMazeGame game)
            : base(DefaultModuleId, RuntimeTickStage.Simulation, 0)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = new RuntimeCommandBuffer(new MarbleMazeCommandValidator());
            _recorder = new RuntimeReplayRecorder(new RuntimeReplayHeader(
                1,
                "marble-maze-runtime-showcase-v0.1",
                "unity-physics-adapter",
                "none",
                RuntimeFrame.Zero));
        }

        public MarbleMazeGame Game { get; }
        public RuntimeFrame LastFrame { get; private set; } = RuntimeFrame.Zero;
        public long LastResultHash { get; private set; }
        public string LastDiagnosticsSummary { get; private set; } = string.Empty;
        public int ReplayFrameCount => _recorder.Count;

        public RuntimeCommandValidationResult EnqueueCommand(RuntimeFrame frame, MarbleMazeCommand command, int targetId = 0, int payload0 = 0, int payload1 = 0, int payload2 = 0, int sourceId = 1)
        {
            return EnqueueRuntimeCommand(new RuntimeCommand(
                frame,
                sourceId,
                MarbleMazeRuntimeCommandIds.ToCommandId(command),
                targetId,
                payload0,
                payload1,
                payload2,
                command.ToString()));
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
                MarbleMazeCommand command;
                if (MarbleMazeRuntimeCommandIds.TryToCommand(drained[i].CommandId, out command))
                    Game.ApplyCommand(command, drained[i]);
            }

            Game.Tick(context.DeltaTime);
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
            string payload = MarbleMazeGameStateCodec.Encode(Game.CaptureState());
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CustomStateSchemaVersion,
                new RuntimeCustomState(CustomStateTypeId, CustomStateSchemaVersion, payload));
            var state = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "marble-maze-runtime-showcase-v0.1",
                "unity-physics-adapter",
                "none",
                LastFrame.Value,
                null,
                null,
                new[] { moduleState },
                new Dictionary<string, string> { { "fixture", "marble-maze-runtime-showcase" } });
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(state);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Marble Maze save state cannot be null."));
            }

            RuntimeModuleSaveState moduleState = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                if (string.Equals(saveState.ModuleStates[i].ModuleId, ModuleId, StringComparison.Ordinal))
                {
                    moduleState = saveState.ModuleStates[i];
                    break;
                }
            }

            if (moduleState == null || moduleState.CustomState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates",
                    "Marble Maze module state is missing."));
            }

            if (!string.Equals(moduleState.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates.marbleMaze.customState.typeId",
                    "Marble Maze module custom state type mismatch."));
            }

            try
            {
                Game.RestoreState(MarbleMazeGameStateCodec.Decode(moduleState.CustomState.PayloadJson));
                LastFrame = new RuntimeFrame(saveState.Frame < 0 ? 0 : saveState.Frame);
                LastResultHash = Game.ComputeStableHash(LastFrame);
                LastDiagnosticsSummary = Game.CaptureSnapshot().ToDiagnosticsSummary(LastFrame);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
            catch (Exception exception)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates.marbleMaze.customState.payloadJson",
                    "Marble Maze state could not be decoded: " + exception.Message,
                    exception: exception));
            }
        }

        private sealed class MarbleMazeCommandValidator : IRuntimeCommandValidator
        {
            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                MarbleMazeCommand ignored;
                if (!MarbleMazeRuntimeCommandIds.TryToCommand(command.CommandId, out ignored))
                {
                    return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.UnregisteredCommandId,
                        command,
                        RuntimeFrame.Zero,
                        "Runtime command is not a Marble Maze command."));
                }

                return RuntimeCommandValidationResult.Accepted(command);
            }
        }
    }

    public sealed class MarbleMazeRuntimeRunner : IDisposable
    {
        private readonly RuntimeHost _host;

        public MarbleMazeRuntimeRunner()
            : this(new MarbleMazeOptions())
        {
        }

        public MarbleMazeRuntimeRunner(MarbleMazeOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Module = new MarbleMazeRuntimeModule(new MarbleMazeGame(Options));
            _host = new RuntimeHost();
            _host.RegisterModule(Module);
            _host.Initialize();
            _host.Start();
        }

        public MarbleMazeOptions Options { get; }
        public MarbleMazeRuntimeModule Module { get; }
        public MarbleMazeGame Game => Module.Game;
        public long LastResultHash => Module.LastResultHash;
        public string LastDiagnosticsSummary => Module.LastDiagnosticsSummary;

        public RuntimeCommandValidationResult EnqueueCommand(long frame, MarbleMazeCommand command, int targetId = 0, int payload0 = 0, int payload1 = 0, int payload2 = 0)
        {
            return Module.EnqueueCommand(new RuntimeFrame(frame), command, targetId, payload0, payload1, payload2);
        }

        public RuntimeCommandValidationResult EnqueueRuntimeCommand(RuntimeCommand command)
        {
            return Module.EnqueueRuntimeCommand(command);
        }

        public void TickFrame(long frame, double deltaTime = 1d / 60d)
        {
            _host.Tick(frame, deltaTime, frame * deltaTime);
        }

        public void RunFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
                TickFrame(i);
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

    public sealed class MarbleMazeReplayFrameDriver : IRuntimeReplayFrameDriver
    {
        private readonly MarbleMazeOptions _options;
        private MarbleMazeRuntimeRunner _runner;

        public MarbleMazeReplayFrameDriver(MarbleMazeOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void Reset(RuntimeReplayHeader header)
        {
            if (_runner != null)
                _runner.Dispose();
            _runner = new MarbleMazeRuntimeRunner(_options);
        }

        public RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record)
        {
            var errors = new List<RuntimeCommandError>();
            for (int i = 0; i < record.Commands.Count; i++)
            {
                RuntimeCommandValidationResult result = _runner.EnqueueRuntimeCommand(record.Commands[i]);
                if (!result.Success)
                    errors.Add(result.Error);
            }

            _runner.TickFrame(record.Frame.Value);
            return new RuntimeReplayPlaybackFrameResult(record.Frame, _runner.LastResultHash, _runner.LastDiagnosticsSummary, errors);
        }
    }

    public static class MarbleMazeGameStateCodec
    {
        public static string Encode(MarbleMazeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var payload = new MarbleMazeStatePayload
            {
                Version = 1,
                CheckpointCount = state.CheckpointCount,
                NextCheckpointIndex = state.NextCheckpointIndex,
                ElapsedSeconds = state.ElapsedSeconds,
                BestTimeSeconds = state.BestTimeSeconds,
                HasBestTime = state.HasBestTime,
                IsPaused = state.IsPaused,
                IsFinished = state.IsFinished,
                FallCount = state.FallCount,
                ResetCount = state.ResetCount,
                BallPosition = VectorPayload.From(state.BallPosition),
                BallVelocity = VectorPayload.From(state.BallVelocity),
                TiltX = state.TiltX,
                TiltZ = state.TiltZ,
                EventCount = state.EventCount,
                LastEvent = state.LastEvent
            };
            return JsonConvert.SerializeObject(payload);
        }

        public static MarbleMazeState Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Marble Maze state JSON cannot be empty.", nameof(json));

            MarbleMazeStatePayload payload = JsonConvert.DeserializeObject<MarbleMazeStatePayload>(json);
            if (payload == null || payload.Version != 1)
                throw new InvalidOperationException("Unsupported Marble Maze state payload version.");

            return new MarbleMazeState(
                payload.CheckpointCount,
                payload.NextCheckpointIndex,
                payload.ElapsedSeconds,
                payload.BestTimeSeconds,
                payload.HasBestTime,
                payload.IsPaused,
                payload.IsFinished,
                payload.FallCount,
                payload.ResetCount,
                payload.BallPosition.ToVector(),
                payload.BallVelocity.ToVector(),
                payload.TiltX,
                payload.TiltZ,
                payload.EventCount,
                payload.LastEvent);
        }

        private sealed class MarbleMazeStatePayload
        {
            public int Version { get; set; }
            public int CheckpointCount { get; set; }
            public int NextCheckpointIndex { get; set; }
            public double ElapsedSeconds { get; set; }
            public double BestTimeSeconds { get; set; }
            public bool HasBestTime { get; set; }
            public bool IsPaused { get; set; }
            public bool IsFinished { get; set; }
            public int FallCount { get; set; }
            public int ResetCount { get; set; }
            public VectorPayload BallPosition { get; set; }
            public VectorPayload BallVelocity { get; set; }
            public double TiltX { get; set; }
            public double TiltZ { get; set; }
            public int EventCount { get; set; }
            public string LastEvent { get; set; }
        }

        private sealed class VectorPayload
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }

            public static VectorPayload From(MarbleMazeVector3 value)
            {
                return new VectorPayload { X = value.X, Y = value.Y, Z = value.Z };
            }

            public MarbleMazeVector3 ToVector()
            {
                return new MarbleMazeVector3(X, Y, Z);
            }
        }
    }
}
