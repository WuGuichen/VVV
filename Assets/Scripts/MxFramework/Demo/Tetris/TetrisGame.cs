using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using MxFramework.Runtime;

namespace MxFramework.Demo.Tetris
{
    public enum TetrisPieceType
    {
        None = 0,
        I = 1,
        O = 2,
        T = 3,
        S = 4,
        Z = 5,
        J = 6,
        L = 7
    }

    public enum TetrisCommand
    {
        None = 0,
        MoveLeft = 1,
        MoveRight = 2,
        RotateClockwise = 3,
        SoftDrop = 4,
        HardDrop = 5
    }

    public sealed class TetrisGameOptions
    {
        private static readonly TetrisPieceType[] DefaultQueue =
        {
            TetrisPieceType.I,
            TetrisPieceType.T,
            TetrisPieceType.O,
            TetrisPieceType.L,
            TetrisPieceType.J,
            TetrisPieceType.S,
            TetrisPieceType.Z
        };

        public TetrisGameOptions(
            int width = 10,
            int height = 20,
            int gravityIntervalFrames = 1,
            IReadOnlyList<TetrisPieceType> pieceQueue = null)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Tetris width must be positive.");
            }

            if (height <= 4)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Tetris height must be greater than the spawn box.");
            }

            if (gravityIntervalFrames <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gravityIntervalFrames), "Tetris gravity interval must be positive.");
            }

            Width = width;
            Height = height;
            GravityIntervalFrames = gravityIntervalFrames;
            PieceQueue = CopyQueue(pieceQueue == null || pieceQueue.Count == 0 ? DefaultQueue : pieceQueue);
        }

        public int Width { get; }
        public int Height { get; }
        public int GravityIntervalFrames { get; }
        public IReadOnlyList<TetrisPieceType> PieceQueue { get; }

        private static ReadOnlyCollection<TetrisPieceType> CopyQueue(IReadOnlyList<TetrisPieceType> source)
        {
            var copy = new List<TetrisPieceType>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == TetrisPieceType.None)
                {
                    throw new ArgumentException("Tetris piece queue cannot contain None.", nameof(source));
                }

                copy.Add(source[i]);
            }

            return new ReadOnlyCollection<TetrisPieceType>(copy);
        }
    }

    public sealed class TetrisGameState
    {
        public TetrisGameState(
            int width,
            int height,
            IReadOnlyList<int> cells,
            IReadOnlyList<TetrisPieceType> pieceQueue,
            int queueIndex,
            bool hasActivePiece,
            TetrisPieceType activePieceType,
            int activeRotation,
            int activeX,
            int activeY,
            int score,
            int linesCleared,
            int lockedPieces,
            bool isGameOver,
            int gravityCounter)
        {
            Width = width;
            Height = height;
            Cells = CopyCells(cells, width, height);
            PieceQueue = CopyQueue(pieceQueue);
            QueueIndex = queueIndex;
            HasActivePiece = hasActivePiece;
            ActivePieceType = activePieceType;
            ActiveRotation = NormalizeRotation(activeRotation);
            ActiveX = activeX;
            ActiveY = activeY;
            Score = score;
            LinesCleared = linesCleared;
            LockedPieces = lockedPieces;
            IsGameOver = isGameOver;
            GravityCounter = gravityCounter;
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<int> Cells { get; }
        public IReadOnlyList<TetrisPieceType> PieceQueue { get; }
        public int QueueIndex { get; }
        public bool HasActivePiece { get; }
        public TetrisPieceType ActivePieceType { get; }
        public int ActiveRotation { get; }
        public int ActiveX { get; }
        public int ActiveY { get; }
        public int Score { get; }
        public int LinesCleared { get; }
        public int LockedPieces { get; }
        public bool IsGameOver { get; }
        public int GravityCounter { get; }

        private static ReadOnlyCollection<int> CopyCells(IReadOnlyList<int> source, int width, int height)
        {
            int expected = width * height;
            if (source == null || source.Count != expected)
            {
                throw new ArgumentException("Tetris state cell count must match width * height.", nameof(source));
            }

            var copy = new List<int>(expected);
            for (int i = 0; i < source.Count; i++)
            {
                int value = source[i];
                if (value < 0 || value > (int)TetrisPieceType.L)
                {
                    throw new ArgumentOutOfRangeException(nameof(source), "Tetris cell value is outside piece id range.");
                }

                copy.Add(value);
            }

            return new ReadOnlyCollection<int>(copy);
        }

        private static ReadOnlyCollection<TetrisPieceType> CopyQueue(IReadOnlyList<TetrisPieceType> source)
        {
            if (source == null || source.Count == 0)
            {
                throw new ArgumentException("Tetris state piece queue cannot be empty.", nameof(source));
            }

            var copy = new List<TetrisPieceType>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == TetrisPieceType.None)
                {
                    throw new ArgumentException("Tetris state piece queue cannot contain None.", nameof(source));
                }

                copy.Add(source[i]);
            }

            return new ReadOnlyCollection<TetrisPieceType>(copy);
        }

        private static int NormalizeRotation(int rotation)
        {
            int normalized = rotation % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }
    }

    public sealed class TetrisGameSnapshot
    {
        public TetrisGameSnapshot(
            int width,
            int height,
            string boardCode,
            string boardWithActiveCode,
            bool hasActivePiece,
            TetrisPieceType activePieceType,
            int activeRotation,
            int activeX,
            int activeY,
            int score,
            int linesCleared,
            int lockedPieces,
            bool isGameOver,
            int queueIndex)
        {
            Width = width;
            Height = height;
            BoardCode = boardCode ?? string.Empty;
            BoardWithActiveCode = boardWithActiveCode ?? string.Empty;
            HasActivePiece = hasActivePiece;
            ActivePieceType = activePieceType;
            ActiveRotation = activeRotation;
            ActiveX = activeX;
            ActiveY = activeY;
            Score = score;
            LinesCleared = linesCleared;
            LockedPieces = lockedPieces;
            IsGameOver = isGameOver;
            QueueIndex = queueIndex;
        }

        public int Width { get; }
        public int Height { get; }
        public string BoardCode { get; }
        public string BoardWithActiveCode { get; }
        public bool HasActivePiece { get; }
        public TetrisPieceType ActivePieceType { get; }
        public int ActiveRotation { get; }
        public int ActiveX { get; }
        public int ActiveY { get; }
        public int Score { get; }
        public int LinesCleared { get; }
        public int LockedPieces { get; }
        public bool IsGameOver { get; }
        public int QueueIndex { get; }

        public string ToDiagnosticsSummary(RuntimeFrame frame)
        {
            return "frame=" + frame.Value
                + " score=" + Score
                + " lines=" + LinesCleared
                + " locked=" + LockedPieces
                + " active=" + ActivePieceType
                + " x=" + ActiveX
                + " y=" + ActiveY
                + " gameOver=" + IsGameOver;
        }
    }

    public sealed class TetrisGame
    {
        private readonly int[] _cells;
        private TetrisGameOptions _options;
        private TetrisPieceType _activePieceType;
        private int _activeRotation;
        private int _activeX;
        private int _activeY;
        private int _queueIndex;
        private int _gravityCounter;
        private bool _hasActivePiece;

        public TetrisGame()
            : this(new TetrisGameOptions())
        {
        }

        public TetrisGame(TetrisGameOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _cells = new int[_options.Width * _options.Height];
            SpawnNextPiece();
        }

        public int Width => _options.Width;
        public int Height => _options.Height;
        public int Score { get; private set; }
        public int LinesCleared { get; private set; }
        public int LockedPieces { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool HasActivePiece => _hasActivePiece;
        public TetrisPieceType ActivePieceType => _activePieceType;

        public void ApplyCommand(TetrisCommand command)
        {
            if (IsGameOver || !_hasActivePiece)
            {
                return;
            }

            switch (command)
            {
                case TetrisCommand.MoveLeft:
                    TryMove(-1, 0);
                    break;
                case TetrisCommand.MoveRight:
                    TryMove(1, 0);
                    break;
                case TetrisCommand.RotateClockwise:
                    TryRotateClockwise();
                    break;
                case TetrisCommand.SoftDrop:
                    SoftDrop();
                    break;
                case TetrisCommand.HardDrop:
                    HardDrop();
                    break;
            }
        }

        public void TickGravity()
        {
            if (IsGameOver || !_hasActivePiece)
            {
                return;
            }

            _gravityCounter++;
            if (_gravityCounter < _options.GravityIntervalFrames)
            {
                return;
            }

            _gravityCounter = 0;
            SoftDrop();
        }

        public TetrisGameSnapshot CaptureSnapshot()
        {
            return new TetrisGameSnapshot(
                Width,
                Height,
                EncodeBoard(includeActivePiece: false),
                EncodeBoard(includeActivePiece: true),
                _hasActivePiece,
                _activePieceType,
                _activeRotation,
                _activeX,
                _activeY,
                Score,
                LinesCleared,
                LockedPieces,
                IsGameOver,
                _queueIndex);
        }

        public TetrisGameState CaptureState()
        {
            return new TetrisGameState(
                Width,
                Height,
                _cells,
                _options.PieceQueue,
                _queueIndex,
                _hasActivePiece,
                _activePieceType,
                _activeRotation,
                _activeX,
                _activeY,
                Score,
                LinesCleared,
                LockedPieces,
                IsGameOver,
                _gravityCounter);
        }

        public void RestoreState(TetrisGameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_cells.Length != state.Width * state.Height)
            {
                throw new InvalidOperationException("TetrisGame cannot restore a state with a different board size.");
            }

            _options = new TetrisGameOptions(state.Width, state.Height, _options.GravityIntervalFrames, state.PieceQueue);
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = state.Cells[i];
            }

            _queueIndex = state.QueueIndex;
            _hasActivePiece = state.HasActivePiece;
            _activePieceType = state.ActivePieceType;
            _activeRotation = state.ActiveRotation;
            _activeX = state.ActiveX;
            _activeY = state.ActiveY;
            Score = state.Score;
            LinesCleared = state.LinesCleared;
            LockedPieces = state.LockedPieces;
            IsGameOver = state.IsGameOver;
            _gravityCounter = state.GravityCounter;
        }

        public long ComputeStableHash(RuntimeFrame frame)
        {
            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddLong("tetris.frame", frame.Value);
            accumulator.AddInt("tetris.width", Width);
            accumulator.AddInt("tetris.height", Height);
            accumulator.AddInt("tetris.score", Score);
            accumulator.AddInt("tetris.lines", LinesCleared);
            accumulator.AddInt("tetris.locked", LockedPieces);
            accumulator.AddInt("tetris.gameOver", IsGameOver ? 1 : 0);
            accumulator.AddInt("tetris.hasActive", _hasActivePiece ? 1 : 0);
            accumulator.AddInt("tetris.activeType", (int)_activePieceType);
            accumulator.AddInt("tetris.activeRotation", _activeRotation);
            accumulator.AddInt("tetris.activeX", _activeX);
            accumulator.AddInt("tetris.activeY", _activeY);
            accumulator.AddInt("tetris.queueIndex", _queueIndex);
            accumulator.AddInt("tetris.gravityCounter", _gravityCounter);
            for (int i = 0; i < _options.PieceQueue.Count; i++)
            {
                accumulator.AddInt("tetris.queue", (int)_options.PieceQueue[i]);
            }

            for (int i = 0; i < _cells.Length; i++)
            {
                accumulator.AddInt("tetris.cell", _cells[i]);
            }

            return accumulator.ToHash();
        }

        private bool TryMove(int dx, int dy)
        {
            int nextX = _activeX + dx;
            int nextY = _activeY + dy;
            if (Collides(_activePieceType, _activeRotation, nextX, nextY))
            {
                return false;
            }

            _activeX = nextX;
            _activeY = nextY;
            return true;
        }

        private void TryRotateClockwise()
        {
            if (_activePieceType == TetrisPieceType.O)
            {
                return;
            }

            int nextRotation = (_activeRotation + 1) & 3;
            if (!Collides(_activePieceType, nextRotation, _activeX, _activeY))
            {
                _activeRotation = nextRotation;
            }
        }

        private void SoftDrop()
        {
            if (!TryMove(0, -1))
            {
                LockActivePiece();
            }
        }

        private void HardDrop()
        {
            while (TryMove(0, -1))
            {
            }

            LockActivePiece();
        }

        private void LockActivePiece()
        {
            ForEachCell(_activePieceType, _activeRotation, _activeX, _activeY, delegate(int x, int y)
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                {
                    _cells[ToIndex(x, y)] = (int)_activePieceType;
                }
            });

            LockedPieces++;
            int cleared = ClearFullLines();
            if (cleared > 0)
            {
                LinesCleared += cleared;
                Score += cleared * cleared * 100;
            }

            SpawnNextPiece();
        }

        private int ClearFullLines()
        {
            int cleared = 0;
            int y = 0;
            while (y < Height)
            {
                if (!IsLineFull(y))
                {
                    y++;
                    continue;
                }

                cleared++;
                for (int copyY = y; copyY < Height - 1; copyY++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        _cells[ToIndex(x, copyY)] = _cells[ToIndex(x, copyY + 1)];
                    }
                }

                for (int x = 0; x < Width; x++)
                {
                    _cells[ToIndex(x, Height - 1)] = 0;
                }
            }

            return cleared;
        }

        private bool IsLineFull(int y)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_cells[ToIndex(x, y)] == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void SpawnNextPiece()
        {
            TetrisPieceType next = _options.PieceQueue[_queueIndex % _options.PieceQueue.Count];
            _queueIndex++;
            _activePieceType = next;
            _activeRotation = 0;
            _activeX = Math.Max(0, (Width - 4) / 2);
            _activeY = Height - 3;
            _gravityCounter = 0;
            _hasActivePiece = true;

            if (Collides(_activePieceType, _activeRotation, _activeX, _activeY))
            {
                _hasActivePiece = false;
                IsGameOver = true;
            }
        }

        private bool Collides(TetrisPieceType type, int rotation, int originX, int originY)
        {
            bool collides = false;
            ForEachCell(type, rotation, originX, originY, delegate(int x, int y)
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height || _cells[ToIndex(x, y)] != 0)
                {
                    collides = true;
                }
            });

            return collides;
        }

        private string EncodeBoard(bool includeActivePiece)
        {
            int[] render = null;
            if (includeActivePiece && _hasActivePiece)
            {
                render = new int[_cells.Length];
                Array.Copy(_cells, render, _cells.Length);
                ForEachCell(_activePieceType, _activeRotation, _activeX, _activeY, delegate(int x, int y)
                {
                    if (x >= 0 && x < Width && y >= 0 && y < Height)
                    {
                        render[ToIndex(x, y)] = (int)_activePieceType;
                    }
                });
            }

            var builder = new StringBuilder(Height * (Width + 1));
            for (int y = Height - 1; y >= 0; y--)
            {
                if (y != Height - 1)
                {
                    builder.Append('/');
                }

                for (int x = 0; x < Width; x++)
                {
                    int value = render == null ? _cells[ToIndex(x, y)] : render[ToIndex(x, y)];
                    builder.Append(ToCellChar(value));
                }
            }

            return builder.ToString();
        }

        private int ToIndex(int x, int y)
        {
            return y * Width + x;
        }

        private static char ToCellChar(int value)
        {
            return value == 0 ? '.' : (char)('0' + value);
        }

        private static void ForEachCell(
            TetrisPieceType type,
            int rotation,
            int originX,
            int originY,
            Action<int, int> action)
        {
            for (int i = 0; i < 4; i++)
            {
                int x;
                int y;
                GetBaseCell(type, i, out x, out y);
                RotateCell(type, rotation, ref x, ref y);
                action(originX + x, originY + y);
            }
        }

        private static void RotateCell(TetrisPieceType type, int rotation, ref int x, ref int y)
        {
            if (type == TetrisPieceType.O)
            {
                return;
            }

            int count = rotation & 3;
            for (int i = 0; i < count; i++)
            {
                int nextX = y;
                int nextY = 3 - x;
                x = nextX;
                y = nextY;
            }
        }

        private static void GetBaseCell(TetrisPieceType type, int index, out int x, out int y)
        {
            switch (type)
            {
                case TetrisPieceType.I:
                    x = index;
                    y = 1;
                    return;
                case TetrisPieceType.O:
                    x = 1 + (index & 1);
                    y = 1 + (index >> 1);
                    return;
                case TetrisPieceType.T:
                    GetFromPairs(index, 1, 2, 0, 1, 1, 1, 2, 1, out x, out y);
                    return;
                case TetrisPieceType.S:
                    GetFromPairs(index, 1, 2, 2, 2, 0, 1, 1, 1, out x, out y);
                    return;
                case TetrisPieceType.Z:
                    GetFromPairs(index, 0, 2, 1, 2, 1, 1, 2, 1, out x, out y);
                    return;
                case TetrisPieceType.J:
                    GetFromPairs(index, 0, 2, 0, 1, 1, 1, 2, 1, out x, out y);
                    return;
                case TetrisPieceType.L:
                    GetFromPairs(index, 2, 2, 0, 1, 1, 1, 2, 1, out x, out y);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "Unknown Tetris piece type.");
            }
        }

        private static void GetFromPairs(
            int index,
            int x0,
            int y0,
            int x1,
            int y1,
            int x2,
            int y2,
            int x3,
            int y3,
            out int x,
            out int y)
        {
            switch (index)
            {
                case 0:
                    x = x0;
                    y = y0;
                    return;
                case 1:
                    x = x1;
                    y = y1;
                    return;
                case 2:
                    x = x2;
                    y = y2;
                    return;
                default:
                    x = x3;
                    y = y3;
                    return;
            }
        }
    }
}
