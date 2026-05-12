using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Demo.Tetris
{
    public static class TetrisRuntimeCommandIds
    {
        public const int MoveLeft = 20001;
        public const int MoveRight = 20002;
        public const int RotateClockwise = 20003;
        public const int SoftDrop = 20004;
        public const int HardDrop = 20005;

        public static int ToCommandId(TetrisCommand command)
        {
            switch (command)
            {
                case TetrisCommand.MoveLeft:
                    return MoveLeft;
                case TetrisCommand.MoveRight:
                    return MoveRight;
                case TetrisCommand.RotateClockwise:
                    return RotateClockwise;
                case TetrisCommand.SoftDrop:
                    return SoftDrop;
                case TetrisCommand.HardDrop:
                    return HardDrop;
                default:
                    return 0;
            }
        }

        public static bool TryToCommand(int commandId, out TetrisCommand command)
        {
            switch (commandId)
            {
                case MoveLeft:
                    command = TetrisCommand.MoveLeft;
                    return true;
                case MoveRight:
                    command = TetrisCommand.MoveRight;
                    return true;
                case RotateClockwise:
                    command = TetrisCommand.RotateClockwise;
                    return true;
                case SoftDrop:
                    command = TetrisCommand.SoftDrop;
                    return true;
                case HardDrop:
                    command = TetrisCommand.HardDrop;
                    return true;
                default:
                    command = TetrisCommand.None;
                    return false;
            }
        }
    }

    public sealed class TetrisRuntimeModule : RuntimeModule, IRuntimeSaveStateProvider, IRuntimeSaveStateRestorer
    {
        public const string DefaultModuleId = "tetris-validation";
        public const string CustomStateTypeId = "mxframework.demo.tetris.state";
        private const int CustomStateSchemaVersion = 0;

        private readonly RuntimeCommandBuffer _commands;
        private readonly RuntimeReplayRecorder _recorder;

        public TetrisRuntimeModule(TetrisGame game)
            : this(game, DefaultModuleId)
        {
        }

        public TetrisRuntimeModule(TetrisGame game, string moduleId)
            : base(moduleId, RuntimeTickStage.Simulation, 0)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = new RuntimeCommandBuffer(new TetrisCommandValidator());
            _recorder = new RuntimeReplayRecorder(new RuntimeReplayHeader(
                1,
                "tetris-validation-v0.1",
                "fixed-piece-queue",
                "none",
                RuntimeFrame.Zero));
        }

        public TetrisGame Game { get; }
        public long LastResultHash { get; private set; }
        public RuntimeFrame LastFrame { get; private set; } = RuntimeFrame.Zero;
        public string LastDiagnosticsSummary { get; private set; } = string.Empty;
        public int ReplayFrameCount => _recorder.Count;

        public RuntimeCommandValidationResult EnqueueCommand(RuntimeFrame frame, TetrisCommand command, int sourceId = 1)
        {
            int commandId = TetrisRuntimeCommandIds.ToCommandId(command);
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
                TetrisCommand command;
                if (TetrisRuntimeCommandIds.TryToCommand(drained[i].CommandId, out command))
                {
                    Game.ApplyCommand(command);
                }
            }

            Game.TickGravity();
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
            string payload = TetrisGameStateCodec.Encode(Game.CaptureState());
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CustomStateSchemaVersion,
                new RuntimeCustomState(CustomStateTypeId, CustomStateSchemaVersion, payload));
            var state = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "tetris-validation-v0.1",
                "fixed-piece-queue",
                "none",
                LastFrame.Value,
                null,
                null,
                new[] { moduleState },
                new Dictionary<string, string>
                {
                    { "fixture", "tetris-runtime-validation" }
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
                    "Tetris save state cannot be null."));
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
                    "Tetris module state is missing."));
            }

            if (!string.Equals(moduleState.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates.tetris.customState.typeId",
                    "Tetris module custom state type mismatch."));
            }

            try
            {
                TetrisGameState gameState = TetrisGameStateCodec.Decode(moduleState.CustomState.PayloadJson);
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
                    "$.moduleStates.tetris.customState.payloadJson",
                    "Tetris module custom state could not be decoded: " + exception.Message,
                    exception: exception));
            }
        }

        private sealed class TetrisCommandValidator : IRuntimeCommandValidator
        {
            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                TetrisCommand ignored;
                if (!TetrisRuntimeCommandIds.TryToCommand(command.CommandId, out ignored))
                {
                    return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.UnregisteredCommandId,
                        command,
                        RuntimeFrame.Zero,
                        "Runtime command is not a Tetris validation command."));
                }

                return RuntimeCommandValidationResult.Accepted(command);
            }
        }
    }

    public sealed class TetrisRuntimeValidationRunner : IDisposable
    {
        private readonly RuntimeHost _host;

        public TetrisRuntimeValidationRunner()
            : this(new TetrisGameOptions())
        {
        }

        public TetrisRuntimeValidationRunner(TetrisGameOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Module = new TetrisRuntimeModule(new TetrisGame(Options));
            _host = new RuntimeHost();
            _host.RegisterModule(Module);
            _host.Initialize();
            _host.Start();
        }

        public TetrisGameOptions Options { get; }
        public TetrisRuntimeModule Module { get; }
        public TetrisGame Game => Module.Game;
        public long LastResultHash => Module.LastResultHash;
        public string LastDiagnosticsSummary => Module.LastDiagnosticsSummary;

        public RuntimeCommandValidationResult EnqueueCommand(long frame, TetrisCommand command)
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

    public sealed class TetrisReplayFrameDriver : IRuntimeReplayFrameDriver
    {
        private readonly TetrisGameOptions _options;
        private TetrisRuntimeValidationRunner _runner;

        public TetrisReplayFrameDriver(TetrisGameOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public TetrisRuntimeValidationRunner Runner => _runner;

        public void Reset(RuntimeReplayHeader header)
        {
            if (_runner != null)
            {
                _runner.Dispose();
            }

            _runner = new TetrisRuntimeValidationRunner(_options);
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

    public static class TetrisGameStateCodec
    {
        public static string Encode(TetrisGameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var payload = new TetrisGameStatePayload
            {
                Version = 1,
                Width = state.Width,
                Height = state.Height,
                Cells = CopyCells(state.Cells),
                Queue = EncodeQueue(state.PieceQueue),
                QueueIndex = state.QueueIndex,
                HasActivePiece = state.HasActivePiece,
                ActivePieceType = (int)state.ActivePieceType,
                ActiveRotation = state.ActiveRotation,
                ActiveX = state.ActiveX,
                ActiveY = state.ActiveY,
                Score = state.Score,
                LinesCleared = state.LinesCleared,
                LockedPieces = state.LockedPieces,
                IsGameOver = state.IsGameOver,
                GravityCounter = state.GravityCounter
            };
            return JsonConvert.SerializeObject(payload, Formatting.None);
        }

        public static TetrisGameState Decode(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new FormatException("Tetris payload is empty.");
            }

            TetrisGameStatePayload state = JsonConvert.DeserializeObject<TetrisGameStatePayload>(payload);
            if (state == null)
            {
                throw new FormatException("Tetris payload could not be deserialized.");
            }

            if (state.Version != 1)
            {
                throw new FormatException("Unsupported Tetris payload version.");
            }

            if (state.Cells == null)
            {
                throw new FormatException("Tetris payload is missing cells.");
            }

            if (state.Queue == null)
            {
                throw new FormatException("Tetris payload is missing queue.");
            }

            return new TetrisGameState(
                state.Width,
                state.Height,
                state.Cells,
                DecodeQueue(state.Queue),
                state.QueueIndex,
                state.HasActivePiece,
                (TetrisPieceType)state.ActivePieceType,
                state.ActiveRotation,
                state.ActiveX,
                state.ActiveY,
                state.Score,
                state.LinesCleared,
                state.LockedPieces,
                state.IsGameOver,
                state.GravityCounter);
        }

        private static List<int> CopyCells(IReadOnlyList<int> cells)
        {
            var copy = new List<int>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                copy.Add(cells[i]);
            }

            return copy;
        }

        private static List<int> EncodeQueue(IReadOnlyList<TetrisPieceType> queue)
        {
            var encoded = new List<int>(queue.Count);
            for (int i = 0; i < queue.Count; i++)
            {
                encoded.Add((int)queue[i]);
            }

            return encoded;
        }

        private static List<TetrisPieceType> DecodeQueue(IReadOnlyList<int> encoded)
        {
            var queue = new List<TetrisPieceType>(encoded.Count);
            for (int i = 0; i < encoded.Count; i++)
            {
                queue.Add((TetrisPieceType)encoded[i]);
            }

            return queue;
        }

        private sealed class TetrisGameStatePayload
        {
            public int Version { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public List<int> Cells { get; set; }
            public List<int> Queue { get; set; }
            public int QueueIndex { get; set; }
            public bool HasActivePiece { get; set; }
            public int ActivePieceType { get; set; }
            public int ActiveRotation { get; set; }
            public int ActiveX { get; set; }
            public int ActiveY { get; set; }
            public int Score { get; set; }
            public int LinesCleared { get; set; }
            public int LockedPieces { get; set; }
            public bool IsGameOver { get; set; }
            public int GravityCounter { get; set; }
        }
    }
}
