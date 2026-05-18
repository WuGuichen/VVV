using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using MxFramework.Runtime;

namespace MxFramework.Demo.Breakout
{
    public enum BreakoutCommand
    {
        None = 0,
        MoveLeft = 1,
        MoveRight = 2,
        Launch = 3,
        Pause = 4,
        Restart = 5
    }

    public enum BreakoutPowerUpType
    {
        None = 0,
        WidePaddle = 1,
        SlowBall = 2,
        MultiBall = 3,
        ExtraLife = 4,
        Laser = 5
    }

    public enum BreakoutBrickType
    {
        Empty = 0,
        Normal = 1,
        Strong = 2,
        Unbreakable = 3,
        PowerUp = 4
    }

    public readonly struct BreakoutRect
    {
        public BreakoutRect(double left, double bottom, double width, double height)
        {
            Left = left;
            Bottom = bottom;
            Width = width;
            Height = height;
        }

        public double Left { get; }
        public double Bottom { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => Left + Width;
        public double Top => Bottom + Height;
        public double CenterX => Left + Width * 0.5d;
        public double CenterY => Bottom + Height * 0.5d;

        public bool Intersects(BreakoutRect other)
        {
            return Left < other.Right
                && Right > other.Left
                && Bottom < other.Top
                && Top > other.Bottom;
        }
    }

    public sealed class BreakoutGameOptions
    {
        public BreakoutGameOptions(
            double playfieldWidth = 160d,
            double playfieldHeight = 120d,
            double paddleY = 8d,
            double paddleWidth = 24d,
            double paddleHeight = 4d,
            double paddleMoveStep = 2.75d,
            double widePaddleMultiplier = 1.5d,
            double ballRadius = 2d,
            double ballSpeedX = 0.75d,
            double ballSpeedY = 1.35d,
            double preLaunchBallRollStep = 0.55d,
            int brickRows = 4,
            int brickColumns = 8,
            double brickTopY = -1d,
            double brickHeight = 6d,
            double brickGap = 1d,
            int startingLives = 3,
            int scorePerBrick = 10,
            int powerUpDurationFrames = 180)
        {
            RequirePositive(playfieldWidth, nameof(playfieldWidth));
            RequirePositive(playfieldHeight, nameof(playfieldHeight));
            RequirePositive(paddleWidth, nameof(paddleWidth));
            RequirePositive(paddleHeight, nameof(paddleHeight));
            RequirePositive(paddleMoveStep, nameof(paddleMoveStep));
            RequirePositive(widePaddleMultiplier, nameof(widePaddleMultiplier));
            RequirePositive(ballRadius, nameof(ballRadius));
            RequirePositive(ballSpeedX, nameof(ballSpeedX));
            RequirePositive(ballSpeedY, nameof(ballSpeedY));
            RequirePositive(preLaunchBallRollStep, nameof(preLaunchBallRollStep));
            RequirePositive(brickHeight, nameof(brickHeight));

            if (brickRows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(brickRows), "Breakout brick rows must be positive.");
            }

            if (brickColumns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(brickColumns), "Breakout brick columns must be positive.");
            }

            if (startingLives <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingLives), "Breakout starting lives must be positive.");
            }

            if (scorePerBrick <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scorePerBrick), "Breakout score per brick must be positive.");
            }

            if (powerUpDurationFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(powerUpDurationFrames), "Breakout power-up duration cannot be negative.");
            }

            if (brickGap < 0d || double.IsNaN(brickGap) || double.IsInfinity(brickGap))
            {
                throw new ArgumentOutOfRangeException(nameof(brickGap), "Breakout brick gap must be finite and non-negative.");
            }

            double resolvedBrickTopY = brickTopY < 0d ? playfieldHeight - 12d : brickTopY;
            if (resolvedBrickTopY <= paddleY + paddleHeight + ballRadius * 2d || resolvedBrickTopY > playfieldHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(brickTopY), "Breakout brick grid must fit above the paddle and inside the playfield.");
            }

            PlayfieldWidth = playfieldWidth;
            PlayfieldHeight = playfieldHeight;
            PaddleY = paddleY;
            PaddleWidth = paddleWidth;
            PaddleHeight = paddleHeight;
            PaddleMoveStep = paddleMoveStep;
            WidePaddleMultiplier = widePaddleMultiplier;
            BallRadius = ballRadius;
            BallSpeedX = ballSpeedX;
            BallSpeedY = ballSpeedY;
            PreLaunchBallRollStep = preLaunchBallRollStep;
            BrickRows = brickRows;
            BrickColumns = brickColumns;
            BrickTopY = resolvedBrickTopY;
            BrickHeight = brickHeight;
            BrickGap = brickGap;
            StartingLives = startingLives;
            ScorePerBrick = scorePerBrick;
            PowerUpDurationFrames = powerUpDurationFrames;
        }

        public double PlayfieldWidth { get; }
        public double PlayfieldHeight { get; }
        public double PaddleY { get; }
        public double PaddleWidth { get; }
        public double PaddleHeight { get; }
        public double PaddleMoveStep { get; }
        public double WidePaddleMultiplier { get; }
        public double BallRadius { get; }
        public double BallSpeedX { get; }
        public double BallSpeedY { get; }
        public double PreLaunchBallRollStep { get; }
        public int BrickRows { get; }
        public int BrickColumns { get; }
        public double BrickTopY { get; }
        public double BrickHeight { get; }
        public double BrickGap { get; }
        public int StartingLives { get; }
        public int ScorePerBrick { get; }
        public int PowerUpDurationFrames { get; }

        private static void RequirePositive(double value, string name)
        {
            if (value <= 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Breakout option must be finite and positive.");
            }
        }
    }

    public sealed class BreakoutGameState
    {
        private readonly ReadOnlyCollection<int> _brickTypes;
        private readonly ReadOnlyCollection<int> _brickHitPoints;
        private readonly ReadOnlyCollection<int> _brickPowerUps;
        private readonly ReadOnlyCollection<BreakoutBallState> _balls;

        public BreakoutGameState(
            double playfieldWidth,
            double playfieldHeight,
            int brickRows,
            int brickColumns,
            IReadOnlyList<bool> bricks,
            double paddleX,
            double ballX,
            double ballY,
            double ballVelocityX,
            double ballVelocityY,
            bool isLaunched,
            bool isPaused,
            int score,
            int lives,
            bool isWin,
            bool isGameOver,
            BreakoutPowerUpType powerUpType,
            int powerUpTimerFrames,
            IReadOnlyList<int> brickTypes = null,
            IReadOnlyList<int> brickHitPoints = null,
            IReadOnlyList<int> brickPowerUps = null,
            IReadOnlyList<BreakoutBallState> balls = null,
            int levelIndex = 0,
            int eventCount = 0,
            string lastEvent = "")
        {
            if (brickRows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(brickRows), "Breakout state brick rows must be positive.");
            }

            if (brickColumns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(brickColumns), "Breakout state brick columns must be positive.");
            }

            RequireFinite(playfieldWidth, nameof(playfieldWidth));
            RequireFinite(playfieldHeight, nameof(playfieldHeight));
            RequireFinite(paddleX, nameof(paddleX));
            RequireFinite(ballX, nameof(ballX));
            RequireFinite(ballY, nameof(ballY));
            RequireFinite(ballVelocityX, nameof(ballVelocityX));
            RequireFinite(ballVelocityY, nameof(ballVelocityY));

            if (lives < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lives), "Breakout lives cannot be negative.");
            }

            if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), "Breakout score cannot be negative.");
            }

            if (powerUpTimerFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(powerUpTimerFrames), "Breakout power-up timer cannot be negative.");
            }

            PlayfieldWidth = playfieldWidth;
            PlayfieldHeight = playfieldHeight;
            BrickRows = brickRows;
            BrickColumns = brickColumns;
            Bricks = CopyBricks(bricks, brickRows, brickColumns);
            PaddleX = paddleX;
            BallX = ballX;
            BallY = ballY;
            BallVelocityX = ballVelocityX;
            BallVelocityY = ballVelocityY;
            IsLaunched = isLaunched;
            IsPaused = isPaused;
            Score = score;
            Lives = lives;
            IsWin = isWin;
            IsGameOver = isGameOver;
            PowerUpType = powerUpType;
            PowerUpTimerFrames = powerUpTimerFrames;
            _brickTypes = CopyIntList(brickTypes, brickRows, brickColumns, BreakoutBrickType.Normal);
            _brickHitPoints = CopyIntList(brickHitPoints, brickRows, brickColumns, 1);
            _brickPowerUps = CopyIntList(brickPowerUps, brickRows, brickColumns, BreakoutPowerUpType.None);
            _balls = CopyBalls(balls);
            LevelIndex = levelIndex < 0 ? 0 : levelIndex;
            EventCount = eventCount < 0 ? 0 : eventCount;
            LastEvent = lastEvent ?? string.Empty;
        }

        public double PlayfieldWidth { get; }
        public double PlayfieldHeight { get; }
        public int BrickRows { get; }
        public int BrickColumns { get; }
        public IReadOnlyList<bool> Bricks { get; }
        public double PaddleX { get; }
        public double BallX { get; }
        public double BallY { get; }
        public double BallVelocityX { get; }
        public double BallVelocityY { get; }
        public bool IsLaunched { get; }
        public bool IsPaused { get; }
        public int Score { get; }
        public int Lives { get; }
        public bool IsWin { get; }
        public bool IsGameOver { get; }
        public BreakoutPowerUpType PowerUpType { get; }
        public int PowerUpTimerFrames { get; }
        public IReadOnlyList<int> BrickTypes => _brickTypes;
        public IReadOnlyList<int> BrickHitPoints => _brickHitPoints;
        public IReadOnlyList<int> BrickPowerUps => _brickPowerUps;
        public IReadOnlyList<BreakoutBallState> Balls => _balls;
        public int LevelIndex { get; }
        public int EventCount { get; }
        public string LastEvent { get; }

        private static ReadOnlyCollection<bool> CopyBricks(IReadOnlyList<bool> source, int rows, int columns)
        {
            int expected = rows * columns;
            if (source == null || source.Count != expected)
            {
                throw new ArgumentException("Breakout state brick count must match rows * columns.", nameof(source));
            }

            var copy = new List<bool>(expected);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i]);
            }

            return new ReadOnlyCollection<bool>(copy);
        }

        private static ReadOnlyCollection<int> CopyIntList<TEnum>(IReadOnlyList<int> source, int rows, int columns, TEnum fallback)
        {
            int expected = rows * columns;
            var copy = new List<int>(expected);
            if (source == null || source.Count != expected)
            {
                int fallbackValue = Convert.ToInt32(fallback);
                for (int i = 0; i < expected; i++)
                {
                    copy.Add(fallbackValue);
                }
            }
            else
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copy.Add(source[i]);
                }
            }

            return new ReadOnlyCollection<int>(copy);
        }

        private static ReadOnlyCollection<BreakoutBallState> CopyBalls(IReadOnlyList<BreakoutBallState> source)
        {
            if (source == null || source.Count == 0)
            {
                return new ReadOnlyCollection<BreakoutBallState>(new List<BreakoutBallState>());
            }

            var copy = new List<BreakoutBallState>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i]);
            }

            return new ReadOnlyCollection<BreakoutBallState>(copy);
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Breakout state value must be finite.");
            }
        }
    }

    public sealed class BreakoutBallState
    {
        public BreakoutBallState(int id, double x, double y, double velocityX, double velocityY)
        {
            Id = id;
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public double VelocityX { get; }
        public double VelocityY { get; }
    }

    public sealed class BreakoutGameSnapshot
    {
        private readonly ReadOnlyCollection<BreakoutBrickSnapshot> _bricks;
        private readonly ReadOnlyCollection<BreakoutBallSnapshot> _balls;

        public BreakoutGameSnapshot(
            double playfieldWidth,
            double playfieldHeight,
            double paddleX,
            double paddleY,
            double paddleWidth,
            double paddleHeight,
            double ballX,
            double ballY,
            double ballRadius,
            double ballVelocityX,
            double ballVelocityY,
            bool isLaunched,
            bool isPaused,
            int score,
            int lives,
            bool isWin,
            bool isGameOver,
            BreakoutPowerUpType powerUpType,
            int powerUpTimerFrames,
            int bricksRemaining,
            string brickCode,
            IReadOnlyList<BreakoutBrickSnapshot> bricks,
            IReadOnlyList<BreakoutBallSnapshot> balls = null,
            int levelIndex = 0,
            int eventCount = 0,
            string lastEvent = "")
        {
            PlayfieldWidth = playfieldWidth;
            PlayfieldHeight = playfieldHeight;
            PaddleX = paddleX;
            PaddleY = paddleY;
            PaddleWidth = paddleWidth;
            PaddleHeight = paddleHeight;
            BallX = ballX;
            BallY = ballY;
            BallRadius = ballRadius;
            BallVelocityX = ballVelocityX;
            BallVelocityY = ballVelocityY;
            IsLaunched = isLaunched;
            IsPaused = isPaused;
            Score = score;
            Lives = lives;
            IsWin = isWin;
            IsGameOver = isGameOver;
            PowerUpType = powerUpType;
            PowerUpTimerFrames = powerUpTimerFrames;
            BricksRemaining = bricksRemaining;
            BrickCode = brickCode ?? string.Empty;
            _bricks = CopyBricks(bricks);
            _balls = CopyBalls(balls);
            LevelIndex = levelIndex;
            EventCount = eventCount;
            LastEvent = lastEvent ?? string.Empty;
        }

        public double PlayfieldWidth { get; }
        public double PlayfieldHeight { get; }
        public double Width => PlayfieldWidth;
        public double Height => PlayfieldHeight;
        public double PaddleX { get; }
        public double PaddleY { get; }
        public double PaddleWidth { get; }
        public double PaddleHeight { get; }
        public double BallX { get; }
        public double BallY { get; }
        public double BallRadius { get; }
        public double BallVelocityX { get; }
        public double BallVelocityY { get; }
        public bool IsLaunched { get; }
        public bool IsPaused { get; }
        public int Score { get; }
        public int Lives { get; }
        public bool IsWin { get; }
        public bool IsVictory => IsWin;
        public bool IsGameOver { get; }
        public BreakoutPowerUpType PowerUpType { get; }
        public int PowerUpTimerFrames { get; }
        public int BricksRemaining { get; }
        public string BrickCode { get; }
        public IReadOnlyList<BreakoutBrickSnapshot> Bricks => _bricks;
        public IReadOnlyList<BreakoutBallSnapshot> Balls => _balls;
        public int BallCount => _balls.Count;
        public int LevelIndex { get; }
        public int EventCount { get; }
        public string LastEvent { get; }

        public string ToDiagnosticsSummary(RuntimeFrame frame)
        {
            return "frame=" + frame.Value
                + " score=" + Score
                + " lives=" + Lives
                + " level=" + (LevelIndex + 1)
                + " bricks=" + BricksRemaining
                + " balls=" + BallCount
                + " ball=(" + Math.Round(BallX, 2) + "," + Math.Round(BallY, 2) + ")"
                + " launched=" + IsLaunched
                + " paused=" + IsPaused
                + " win=" + IsWin
                + " gameOver=" + IsGameOver
                + " powerUp=" + PowerUpType
                + " timer=" + PowerUpTimerFrames
                + " event=" + LastEvent;
        }

        private static ReadOnlyCollection<BreakoutBrickSnapshot> CopyBricks(IReadOnlyList<BreakoutBrickSnapshot> bricks)
        {
            if (bricks == null || bricks.Count == 0)
            {
                return new ReadOnlyCollection<BreakoutBrickSnapshot>(new List<BreakoutBrickSnapshot>());
            }

            var copy = new List<BreakoutBrickSnapshot>(bricks.Count);
            for (int i = 0; i < bricks.Count; i++)
            {
                copy.Add(bricks[i]);
            }

            return new ReadOnlyCollection<BreakoutBrickSnapshot>(copy);
        }

        private static ReadOnlyCollection<BreakoutBallSnapshot> CopyBalls(IReadOnlyList<BreakoutBallSnapshot> balls)
        {
            if (balls == null || balls.Count == 0)
            {
                return new ReadOnlyCollection<BreakoutBallSnapshot>(new List<BreakoutBallSnapshot>());
            }

            var copy = new List<BreakoutBallSnapshot>(balls.Count);
            for (int i = 0; i < balls.Count; i++)
            {
                copy.Add(balls[i]);
            }

            return new ReadOnlyCollection<BreakoutBallSnapshot>(copy);
        }
    }

    public sealed class BreakoutBrickSnapshot
    {
        public BreakoutBrickSnapshot(
            int row,
            int column,
            double x,
            double y,
            double width,
            double height,
            bool isActive,
            int hitPoints)
            : this(row, column, x, y, width, height, isActive, hitPoints, BreakoutBrickType.Normal, BreakoutPowerUpType.None)
        {
        }

        public BreakoutBrickSnapshot(
            int row,
            int column,
            double x,
            double y,
            double width,
            double height,
            bool isActive,
            int hitPoints,
            BreakoutBrickType type,
            BreakoutPowerUpType powerUpType)
        {
            Row = row;
            Column = column;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            IsActive = isActive;
            HitPoints = hitPoints;
            Type = type;
            PowerUpType = powerUpType;
        }

        public int Row { get; }
        public int Column { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public bool IsActive { get; }
        public bool IsDestroyed => !IsActive;
        public int HitPoints { get; }
        public BreakoutBrickType Type { get; }
        public BreakoutPowerUpType PowerUpType { get; }
    }

    public sealed class BreakoutBallSnapshot
    {
        public BreakoutBallSnapshot(int id, double x, double y, double radius, double velocityX, double velocityY)
        {
            Id = id;
            X = x;
            Y = y;
            Radius = radius;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public double Radius { get; }
        public double VelocityX { get; }
        public double VelocityY { get; }
    }

    public sealed class BreakoutGame
    {
        private const int DefaultLevelCount = 3;
        private readonly bool[] _bricks;
        private readonly BreakoutBrickType[] _brickTypes;
        private readonly BreakoutPowerUpType[] _brickPowerUps;
        private readonly int[] _brickHitPoints;
        private readonly List<BreakoutBall> _balls = new List<BreakoutBall>(4);
        private readonly BreakoutGameOptions _options;
        private int _bricksRemaining;
        private int _nextBallId;
        private string _lastEvent = string.Empty;
        private int _eventCount;

        public BreakoutGame()
            : this(new BreakoutGameOptions())
        {
        }

        public BreakoutGame(BreakoutGameOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _bricks = new bool[_options.BrickRows * _options.BrickColumns];
            _brickTypes = new BreakoutBrickType[_bricks.Length];
            _brickPowerUps = new BreakoutPowerUpType[_bricks.Length];
            _brickHitPoints = new int[_bricks.Length];
            Restart();
        }

        public double PlayfieldWidth => _options.PlayfieldWidth;
        public double PlayfieldHeight => _options.PlayfieldHeight;
        public double PaddleX { get; private set; }
        public double PaddleWidth => PowerUpType == BreakoutPowerUpType.WidePaddle
            ? _options.PaddleWidth * _options.WidePaddleMultiplier
            : _options.PaddleWidth;
        public int LevelIndex { get; private set; }
        public double BallX { get; private set; }
        public double BallY { get; private set; }
        public double BallVelocityX { get; private set; }
        public double BallVelocityY { get; private set; }
        public bool IsLaunched { get; private set; }
        public bool IsPaused { get; private set; }
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public bool IsWin { get; private set; }
        public bool IsGameOver { get; private set; }
        public BreakoutPowerUpType PowerUpType { get; private set; }
        public int PowerUpTimerFrames { get; private set; }
        public int BricksRemaining => _bricksRemaining;
        public int BallCount => IsLaunched ? _balls.Count : 1;
        public string LastEvent => _lastEvent;
        public int EventCount => _eventCount;

        public void ApplyCommand(BreakoutCommand command)
        {
            switch (command)
            {
                case BreakoutCommand.MoveLeft:
                    MovePaddle(-_options.PaddleMoveStep);
                    break;
                case BreakoutCommand.MoveRight:
                    MovePaddle(_options.PaddleMoveStep);
                    break;
                case BreakoutCommand.Launch:
                    LaunchBall();
                    break;
                case BreakoutCommand.Pause:
                    if (!IsGameOver && !IsWin)
                    {
                        IsPaused = !IsPaused;
                    }

                    break;
                case BreakoutCommand.Restart:
                    Restart();
                    break;
            }
        }

        public void TickSimulation()
        {
            if (IsPaused || IsGameOver || IsWin)
            {
                return;
            }

            TickPowerUpTimer();

            if (!IsLaunched)
            {
                RollBallOnPaddle();
                return;
            }

            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                BreakoutBall ball = _balls[i];
                double previousX = ball.X;
                double previousY = ball.Y;
                ball.X += ball.VelocityX;
                ball.Y += ball.VelocityY;

                ResolveWallCollisions(ball);
                if (ball.Y + _options.BallRadius < 0d)
                {
                    _balls.RemoveAt(i);
                    RecordEvent("ball-lost");
                    continue;
                }

                ResolvePaddleCollision(ball, previousX, previousY);
                ResolveBrickCollision(ball, previousX, previousY);
            }

            if (_balls.Count == 0)
            {
                LoseLife();
                return;
            }

            SyncPrimaryBallFromList();
        }

        public void Restart()
        {
            LevelIndex = 0;
            Score = 0;
            Lives = _options.StartingLives;
            IsPaused = false;
            IsWin = false;
            IsGameOver = false;
            PowerUpType = BreakoutPowerUpType.None;
            PowerUpTimerFrames = 0;
            _eventCount = 0;
            _lastEvent = "restart";
            _nextBallId = 1;
            PaddleX = _options.PlayfieldWidth * 0.5d;
            LoadLevel(LevelIndex);
            ResetBallOnPaddle();
        }

        public BreakoutRect GetPaddleBounds()
        {
            double width = PaddleWidth;
            return new BreakoutRect(
                PaddleX - width * 0.5d,
                _options.PaddleY,
                width,
                _options.PaddleHeight);
        }

        public BreakoutRect GetBallBounds()
        {
            double diameter = _options.BallRadius * 2d;
            return new BreakoutRect(
                BallX - _options.BallRadius,
                BallY - _options.BallRadius,
                diameter,
                diameter);
        }

        private void LoadLevel(int levelIndex)
        {
            _bricksRemaining = 0;
            for (int row = 0; row < _options.BrickRows; row++)
            {
                for (int column = 0; column < _options.BrickColumns; column++)
                {
                    int index = ToBrickIndex(row, column);
                    BreakoutBrickType type = ResolveBrickType(levelIndex, row, column);
                    _brickTypes[index] = type;
                    _brickPowerUps[index] = ResolvePowerUp(levelIndex, row, column, type);
                    _brickHitPoints[index] = ResolveHitPoints(type);
                    _bricks[index] = type != BreakoutBrickType.Empty;
                    if (IsBreakable(type) && _brickHitPoints[index] > 0)
                    {
                        _bricksRemaining++;
                    }
                }
            }

            RecordEvent("level-" + (levelIndex + 1));
        }

        private static BreakoutBrickType ResolveBrickType(int levelIndex, int row, int column)
        {
            if (levelIndex >= 2 && row == 0 && (column % 3) == 1)
            {
                return BreakoutBrickType.Unbreakable;
            }

            if (((row + column + levelIndex) % 7) == 0)
            {
                return BreakoutBrickType.PowerUp;
            }

            if (row <= levelIndex || ((row + column) % 5) == 0)
            {
                return BreakoutBrickType.Strong;
            }

            return BreakoutBrickType.Normal;
        }

        private static BreakoutPowerUpType ResolvePowerUp(int levelIndex, int row, int column, BreakoutBrickType type)
        {
            if (type != BreakoutBrickType.PowerUp)
            {
                return BreakoutPowerUpType.None;
            }

            int selector = Math.Abs(levelIndex * 11 + row * 5 + column) % 5;
            switch (selector)
            {
                case 0:
                    return BreakoutPowerUpType.WidePaddle;
                case 1:
                    return BreakoutPowerUpType.SlowBall;
                case 2:
                    return BreakoutPowerUpType.MultiBall;
                case 3:
                    return BreakoutPowerUpType.ExtraLife;
                default:
                    return BreakoutPowerUpType.Laser;
            }
        }

        private static int ResolveHitPoints(BreakoutBrickType type)
        {
            switch (type)
            {
                case BreakoutBrickType.Empty:
                    return 0;
                case BreakoutBrickType.Strong:
                    return 2;
                case BreakoutBrickType.Unbreakable:
                    return 99;
                default:
                    return 1;
            }
        }

        private static bool IsBreakable(BreakoutBrickType type)
        {
            return type == BreakoutBrickType.Normal
                || type == BreakoutBrickType.Strong
                || type == BreakoutBrickType.PowerUp;
        }

        public BreakoutRect GetBrickBounds(int row, int column)
        {
            if (row < 0 || row >= _options.BrickRows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Breakout brick row is outside the grid.");
            }

            if (column < 0 || column >= _options.BrickColumns)
            {
                throw new ArgumentOutOfRangeException(nameof(column), "Breakout brick column is outside the grid.");
            }

            double usableWidth = _options.PlayfieldWidth - _options.BrickGap * (_options.BrickColumns + 1);
            double brickWidth = usableWidth / _options.BrickColumns;
            double left = _options.BrickGap + column * (brickWidth + _options.BrickGap);
            double top = _options.BrickTopY - row * (_options.BrickHeight + _options.BrickGap);
            return new BreakoutRect(left, top - _options.BrickHeight, brickWidth, _options.BrickHeight);
        }

        public bool IsBrickActive(int row, int column)
        {
            return _bricks[ToBrickIndex(row, column)];
        }

        public BreakoutGameSnapshot CaptureSnapshot()
        {
            return new BreakoutGameSnapshot(
                _options.PlayfieldWidth,
                _options.PlayfieldHeight,
                PaddleX,
                _options.PaddleY,
                PaddleWidth,
                _options.PaddleHeight,
                BallX,
                BallY,
                _options.BallRadius,
                BallVelocityX,
                BallVelocityY,
                IsLaunched,
                IsPaused,
                Score,
                Lives,
                IsWin,
                IsGameOver,
                PowerUpType,
                PowerUpTimerFrames,
                _bricksRemaining,
                EncodeBricks(),
                CaptureBrickSnapshots(),
                CaptureBallSnapshots(),
                LevelIndex,
                _eventCount,
                _lastEvent);
        }

        public BreakoutGameState CaptureState()
        {
            return new BreakoutGameState(
                _options.PlayfieldWidth,
                _options.PlayfieldHeight,
                _options.BrickRows,
                _options.BrickColumns,
                _bricks,
                PaddleX,
                BallX,
                BallY,
                BallVelocityX,
                BallVelocityY,
                IsLaunched,
                IsPaused,
                Score,
                Lives,
                IsWin,
                IsGameOver,
                PowerUpType,
                PowerUpTimerFrames,
                CopyBrickTypes(),
                CopyBrickHitPoints(),
                CopyBrickPowerUps(),
                CaptureBallStates(),
                LevelIndex,
                _eventCount,
                _lastEvent);
        }

        public void RestoreState(BreakoutGameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (Math.Abs(state.PlayfieldWidth - _options.PlayfieldWidth) > 0.0001d
                || Math.Abs(state.PlayfieldHeight - _options.PlayfieldHeight) > 0.0001d
                || state.BrickRows != _options.BrickRows
                || state.BrickColumns != _options.BrickColumns)
            {
                throw new InvalidOperationException("BreakoutGame cannot restore a state with different board dimensions.");
            }

            _bricksRemaining = 0;
            for (int i = 0; i < _bricks.Length; i++)
            {
                _bricks[i] = state.Bricks[i];
                _brickTypes[i] = (BreakoutBrickType)state.BrickTypes[i];
                _brickHitPoints[i] = state.BrickHitPoints[i];
                _brickPowerUps[i] = (BreakoutPowerUpType)state.BrickPowerUps[i];
                if (_bricks[i] && IsBreakable(_brickTypes[i]) && _brickHitPoints[i] > 0)
                {
                    _bricksRemaining++;
                }
            }

            LevelIndex = state.LevelIndex;
            _eventCount = state.EventCount;
            _lastEvent = state.LastEvent;
            PowerUpType = state.PowerUpType;
            PowerUpTimerFrames = state.PowerUpTimerFrames;
            PaddleX = Clamp(state.PaddleX, PaddleWidth * 0.5d, _options.PlayfieldWidth - PaddleWidth * 0.5d);
            BallX = state.BallX;
            BallY = state.BallY;
            BallVelocityX = state.BallVelocityX;
            BallVelocityY = state.BallVelocityY;
            IsLaunched = state.IsLaunched;
            IsPaused = state.IsPaused;
            Score = state.Score;
            Lives = state.Lives;
            IsWin = state.IsWin;
            IsGameOver = state.IsGameOver;
            _balls.Clear();
            for (int i = 0; i < state.Balls.Count; i++)
            {
                BreakoutBallState ball = state.Balls[i];
                _balls.Add(new BreakoutBall(ball.Id, ball.X, ball.Y, ball.VelocityX, ball.VelocityY));
                _nextBallId = Math.Max(_nextBallId, ball.Id + 1);
            }

            if (_balls.Count == 0 && IsLaunched && !IsGameOver && !IsWin)
            {
                _balls.Add(new BreakoutBall(_nextBallId++, BallX, BallY, BallVelocityX, BallVelocityY));
            }

            if (!IsLaunched && !IsGameOver && !IsWin)
            {
                ClampBallOnPaddle();
                if (Math.Abs(BallVelocityX) < 0.0001d)
                {
                    BallVelocityX = _options.PreLaunchBallRollStep;
                }

                BallVelocityY = 0d;
            }
            else
            {
                SyncPrimaryBallFromList();
            }
        }

        public long ComputeStableHash(RuntimeFrame frame)
        {
            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddLong("breakout.frame", frame.Value);
            accumulator.AddDoubleQuantized("breakout.width", _options.PlayfieldWidth, 1000d);
            accumulator.AddDoubleQuantized("breakout.height", _options.PlayfieldHeight, 1000d);
            accumulator.AddInt("breakout.score", Score);
            accumulator.AddInt("breakout.lives", Lives);
            accumulator.AddInt("breakout.level", LevelIndex);
            accumulator.AddInt("breakout.bricksRemaining", _bricksRemaining);
            accumulator.AddInt("breakout.ballCount", BallCount);
            accumulator.AddInt("breakout.launched", IsLaunched ? 1 : 0);
            accumulator.AddInt("breakout.paused", IsPaused ? 1 : 0);
            accumulator.AddInt("breakout.win", IsWin ? 1 : 0);
            accumulator.AddInt("breakout.gameOver", IsGameOver ? 1 : 0);
            accumulator.AddDoubleQuantized("breakout.paddleX", PaddleX, 1000d);
            accumulator.AddDoubleQuantized("breakout.paddleWidth", PaddleWidth, 1000d);
            accumulator.AddDoubleQuantized("breakout.ballX", BallX, 1000d);
            accumulator.AddDoubleQuantized("breakout.ballY", BallY, 1000d);
            accumulator.AddDoubleQuantized("breakout.ballVelocityX", BallVelocityX, 1000d);
            accumulator.AddDoubleQuantized("breakout.ballVelocityY", BallVelocityY, 1000d);
            accumulator.AddInt("breakout.powerUpType", (int)PowerUpType);
            accumulator.AddInt("breakout.powerUpTimer", PowerUpTimerFrames);
            for (int i = 0; i < _bricks.Length; i++)
            {
                accumulator.AddInt("breakout.brick", _bricks[i] ? 1 : 0);
                accumulator.AddInt("breakout.brickType", (int)_brickTypes[i]);
                accumulator.AddInt("breakout.brickHp", _brickHitPoints[i]);
                accumulator.AddInt("breakout.brickPowerUp", (int)_brickPowerUps[i]);
            }

            for (int i = 0; i < _balls.Count; i++)
            {
                accumulator.AddInt("breakout.ballId", _balls[i].Id);
                accumulator.AddDoubleQuantized("breakout.ball.x", _balls[i].X, 1000d);
                accumulator.AddDoubleQuantized("breakout.ball.y", _balls[i].Y, 1000d);
                accumulator.AddDoubleQuantized("breakout.ball.vx", _balls[i].VelocityX, 1000d);
                accumulator.AddDoubleQuantized("breakout.ball.vy", _balls[i].VelocityY, 1000d);
            }

            return accumulator.ToHash();
        }

        private void MovePaddle(double delta)
        {
            if (IsGameOver || IsWin)
            {
                return;
            }

            double previousPaddleX = PaddleX;
            double halfWidth = PaddleWidth * 0.5d;
            PaddleX = Clamp(PaddleX + delta, halfWidth, _options.PlayfieldWidth - halfWidth);
            if (!IsLaunched)
            {
                BallX += PaddleX - previousPaddleX;
                ClampBallOnPaddle();
            }
        }

        private void LaunchBall()
        {
            if (IsPaused || IsGameOver || IsWin || IsLaunched)
            {
                return;
            }

            IsLaunched = true;
            double offset = Clamp((BallX - PaddleX) / GetLaunchRange(), -1d, 1d);
            if (Math.Abs(offset) < 0.12d)
            {
                offset = Math.Sign(BallVelocityX);
                if (Math.Abs(offset) < 0.0001d)
                {
                    offset = 1d;
                }

                offset *= 0.12d;
            }

            BallVelocityX = offset * _options.BallSpeedX;
            BallVelocityY = _options.BallSpeedY;
            _balls.Clear();
            _balls.Add(new BreakoutBall(_nextBallId++, BallX, BallY, BallVelocityX, BallVelocityY));
            RecordEvent("launch");
        }

        private void ResetBallOnPaddle()
        {
            IsLaunched = false;
            _balls.Clear();
            BallVelocityX = _options.PreLaunchBallRollStep;
            BallVelocityY = 0d;
            BallX = PaddleX;
            AttachBallHeightToPaddle();
        }

        private void AttachBallHeightToPaddle()
        {
            BreakoutRect paddle = GetPaddleBounds();
            BallY = paddle.Top + _options.BallRadius;
        }

        private void RollBallOnPaddle()
        {
            BallX += BallVelocityX;
            double minX = GetPaddleBallMinX();
            double maxX = GetPaddleBallMaxX();
            if (BallX <= minX)
            {
                BallX = minX;
                BallVelocityX = Math.Abs(GetPreLaunchRollSpeed());
            }
            else if (BallX >= maxX)
            {
                BallX = maxX;
                BallVelocityX = -Math.Abs(GetPreLaunchRollSpeed());
            }

            AttachBallHeightToPaddle();
        }

        private void ClampBallOnPaddle()
        {
            BallX = Clamp(BallX, GetPaddleBallMinX(), GetPaddleBallMaxX());
            AttachBallHeightToPaddle();
        }

        private double GetPaddleBallMinX()
        {
            return GetPaddleBounds().Left + _options.BallRadius;
        }

        private double GetPaddleBallMaxX()
        {
            return GetPaddleBounds().Right - _options.BallRadius;
        }

        private double GetLaunchRange()
        {
            return Math.Max(_options.BallRadius, PaddleWidth * 0.5d - _options.BallRadius);
        }

        private double GetPreLaunchRollSpeed()
        {
            return Math.Abs(BallVelocityX) < 0.0001d
                ? _options.PreLaunchBallRollStep
                : Math.Abs(BallVelocityX);
        }

        private void TickPowerUpTimer()
        {
            if (PowerUpTimerFrames <= 0)
            {
                return;
            }

            PowerUpTimerFrames--;
            if (PowerUpTimerFrames == 0)
            {
                if (PowerUpType == BreakoutPowerUpType.SlowBall)
                {
                    ScaleActiveBalls(_options.BallSpeedY);
                }

                PowerUpType = BreakoutPowerUpType.None;
                MovePaddle(0d);
                RecordEvent("powerup-expired");
            }
        }

        private void ResolveWallCollisions(BreakoutBall ball)
        {
            if (ball.X - _options.BallRadius < 0d)
            {
                ball.X = _options.BallRadius;
                ball.VelocityX = Math.Abs(ball.VelocityX);
            }
            else if (ball.X + _options.BallRadius > _options.PlayfieldWidth)
            {
                ball.X = _options.PlayfieldWidth - _options.BallRadius;
                ball.VelocityX = -Math.Abs(ball.VelocityX);
            }

            if (ball.Y + _options.BallRadius > _options.PlayfieldHeight)
            {
                ball.Y = _options.PlayfieldHeight - _options.BallRadius;
                ball.VelocityY = -Math.Abs(ball.VelocityY);
            }
        }

        private void ResolvePaddleCollision(BreakoutBall movingBall, double previousX, double previousY)
        {
            if (movingBall.VelocityY >= 0d)
            {
                return;
            }

            BreakoutRect ball = GetBallBounds(movingBall.X, movingBall.Y);
            BreakoutRect paddle = GetPaddleBounds();
            if (!ball.Intersects(paddle))
            {
                return;
            }

            BreakoutRect previousBall = GetBallBounds(previousX, previousY);
            if (previousBall.Bottom < paddle.Top)
            {
                return;
            }

            movingBall.Y = paddle.Top + _options.BallRadius;
            double offset = Clamp((movingBall.X - PaddleX) / (PaddleWidth * 0.5d), -1d, 1d);
            movingBall.VelocityX = offset * _options.BallSpeedX;
            movingBall.VelocityY = Math.Abs(movingBall.VelocityY);
            RecordEvent("paddle-hit");
        }

        private void ResolveBrickCollision(BreakoutBall movingBall, double previousX, double previousY)
        {
            BreakoutRect ball = GetBallBounds(movingBall.X, movingBall.Y);
            BreakoutRect previousBall = GetBallBounds(previousX, previousY);
            for (int row = 0; row < _options.BrickRows; row++)
            {
                for (int column = 0; column < _options.BrickColumns; column++)
                {
                    int index = ToBrickIndex(row, column);
                    if (!IsBrickActive(row, column))
                    {
                        continue;
                    }

                    BreakoutRect brick = GetBrickBounds(row, column);
                    if (!ball.Intersects(brick))
                    {
                        continue;
                    }

                    HitBrick(index, row, column, movingBall, previousBall, brick);

                    if (_bricksRemaining == 0)
                    {
                        CompleteLevelOrGame();
                    }

                    return;
                }
            }
        }

        private void ReflectBallFromBrick(BreakoutBall movingBall, BreakoutRect previousBall, BreakoutRect brick)
        {
            if (previousBall.Right <= brick.Left)
            {
                movingBall.X = brick.Left - _options.BallRadius;
                movingBall.VelocityX = -Math.Abs(movingBall.VelocityX);
                return;
            }

            if (previousBall.Left >= brick.Right)
            {
                movingBall.X = brick.Right + _options.BallRadius;
                movingBall.VelocityX = Math.Abs(movingBall.VelocityX);
                return;
            }

            if (previousBall.Top <= brick.Bottom)
            {
                movingBall.Y = brick.Bottom - _options.BallRadius;
                movingBall.VelocityY = -Math.Abs(movingBall.VelocityY);
                return;
            }

            if (previousBall.Bottom >= brick.Top)
            {
                movingBall.Y = brick.Top + _options.BallRadius;
                movingBall.VelocityY = Math.Abs(movingBall.VelocityY);
                return;
            }

            movingBall.VelocityY = -movingBall.VelocityY;
        }

        private void LoseLife()
        {
            Lives--;
            PowerUpType = BreakoutPowerUpType.None;
            PowerUpTimerFrames = 0;
            if (Lives <= 0)
            {
                Lives = 0;
                IsGameOver = true;
                IsLaunched = false;
                return;
            }

            ResetBallOnPaddle();
        }

        private void CompleteLevelOrGame()
        {
            if (LevelIndex + 1 < DefaultLevelCount)
            {
                LevelIndex++;
                IsLaunched = false;
                PowerUpType = BreakoutPowerUpType.None;
                PowerUpTimerFrames = 0;
                LoadLevel(LevelIndex);
                ResetBallOnPaddle();
                RecordEvent("next-level");
                return;
            }

            IsWin = true;
            IsLaunched = false;
            _balls.Clear();
            RecordEvent("victory");
        }

        private void RecordEvent(string eventName)
        {
            _lastEvent = eventName ?? string.Empty;
            _eventCount++;
        }

        private void SyncPrimaryBallFromList()
        {
            if (_balls.Count == 0)
            {
                return;
            }

            BreakoutBall ball = _balls[0];
            BallX = ball.X;
            BallY = ball.Y;
            BallVelocityX = ball.VelocityX;
            BallVelocityY = ball.VelocityY;
        }

        private void ActivateWidePaddle()
        {
            if (_options.PowerUpDurationFrames <= 0)
            {
                return;
            }

            PowerUpType = BreakoutPowerUpType.WidePaddle;
            PowerUpTimerFrames = _options.PowerUpDurationFrames;
            MovePaddle(0d);
        }

        private void HitBrick(int index, int row, int column, BreakoutBall movingBall, BreakoutRect previousBall, BreakoutRect brick)
        {
            BreakoutBrickType type = _brickTypes[index];
            if (type == BreakoutBrickType.Unbreakable)
            {
                ReflectBallFromBrick(movingBall, previousBall, brick);
                RecordEvent("unbreakable-hit");
                return;
            }

            _brickHitPoints[index] = Math.Max(0, _brickHitPoints[index] - 1);
            Score += _options.ScorePerBrick;
            ReflectBallFromBrick(movingBall, previousBall, brick);
            RecordEvent("brick-hit");
            if (_brickHitPoints[index] > 0)
            {
                return;
            }

            _bricks[index] = false;
            _bricksRemaining--;
            RecordEvent("brick-destroyed");
            ActivatePowerUp(_brickPowerUps[index], row, column, movingBall);
        }

        private void ActivatePowerUp(BreakoutPowerUpType powerUp, int row, int column, BreakoutBall movingBall)
        {
            switch (powerUp)
            {
                case BreakoutPowerUpType.WidePaddle:
                    ActivateTimedPowerUp(BreakoutPowerUpType.WidePaddle);
                    break;
                case BreakoutPowerUpType.SlowBall:
                    ActivateTimedPowerUp(BreakoutPowerUpType.SlowBall);
                    ScaleActiveBalls(_options.BallSpeedY * 0.65d);
                    break;
                case BreakoutPowerUpType.MultiBall:
                    SpawnExtraBalls(movingBall);
                    break;
                case BreakoutPowerUpType.ExtraLife:
                    Lives++;
                    RecordEvent("extra-life");
                    break;
                case BreakoutPowerUpType.Laser:
                    FireLaser(column);
                    break;
            }
        }

        private void ActivateTimedPowerUp(BreakoutPowerUpType powerUp)
        {
            if (_options.PowerUpDurationFrames <= 0)
            {
                return;
            }

            PowerUpType = powerUp;
            PowerUpTimerFrames = _options.PowerUpDurationFrames;
            MovePaddle(0d);
            RecordEvent("powerup-" + powerUp);
        }

        private void SpawnExtraBalls(BreakoutBall source)
        {
            if (source == null || _balls.Count >= 3)
            {
                return;
            }

            double speedX = Math.Max(0.2d, Math.Abs(_options.BallSpeedX));
            _balls.Add(new BreakoutBall(_nextBallId++, source.X, source.Y, -speedX, Math.Abs(source.VelocityY)));
            if (_balls.Count < 3)
            {
                _balls.Add(new BreakoutBall(_nextBallId++, source.X, source.Y, speedX, Math.Abs(source.VelocityY)));
            }

            RecordEvent("multi-ball");
        }

        private void FireLaser(int column)
        {
            for (int row = _options.BrickRows - 1; row >= 0; row--)
            {
                int index = ToBrickIndex(row, column);
                if (!_bricks[index] || _brickTypes[index] == BreakoutBrickType.Unbreakable)
                {
                    continue;
                }

                _brickHitPoints[index] = 0;
                _bricks[index] = false;
                _bricksRemaining--;
                Score += _options.ScorePerBrick;
                RecordEvent("laser");
                return;
            }
        }

        private void ScaleActiveBalls(double targetYSpeed)
        {
            for (int i = 0; i < _balls.Count; i++)
            {
                BreakoutBall ball = _balls[i];
                double ySign = ball.VelocityY < 0d ? -1d : 1d;
                ball.VelocityY = ySign * targetYSpeed;
            }
        }

        private BreakoutRect GetBallBounds(double centerX, double centerY)
        {
            double diameter = _options.BallRadius * 2d;
            return new BreakoutRect(
                centerX - _options.BallRadius,
                centerY - _options.BallRadius,
                diameter,
                diameter);
        }

        private string EncodeBricks()
        {
            var builder = new StringBuilder(_bricks.Length + _options.BrickRows);
            for (int row = 0; row < _options.BrickRows; row++)
            {
                if (row != 0)
                {
                    builder.Append('/');
                }

                for (int column = 0; column < _options.BrickColumns; column++)
                {
                    int index = ToBrickIndex(row, column);
                    if (!IsBrickActive(row, column))
                    {
                        builder.Append('.');
                    }
                    else if (_brickTypes[index] == BreakoutBrickType.Unbreakable)
                    {
                        builder.Append('U');
                    }
                    else if (_brickTypes[index] == BreakoutBrickType.PowerUp)
                    {
                        builder.Append('P');
                    }
                    else if (_brickHitPoints[index] > 1)
                    {
                        builder.Append('2');
                    }
                    else
                    {
                        builder.Append('#');
                    }
                }
            }

            return builder.ToString();
        }

        private ReadOnlyCollection<BreakoutBrickSnapshot> CaptureBrickSnapshots()
        {
            var bricks = new List<BreakoutBrickSnapshot>(_bricks.Length);
            for (int row = 0; row < _options.BrickRows; row++)
            {
                for (int column = 0; column < _options.BrickColumns; column++)
                {
                    BreakoutRect bounds = GetBrickBounds(row, column);
                    bricks.Add(new BreakoutBrickSnapshot(
                        row,
                        column,
                        bounds.Left,
                        bounds.Bottom,
                        bounds.Width,
                        bounds.Height,
                        IsBrickActive(row, column),
                        _brickHitPoints[ToBrickIndex(row, column)],
                        _brickTypes[ToBrickIndex(row, column)],
                        _brickPowerUps[ToBrickIndex(row, column)]));
                }
            }

            return new ReadOnlyCollection<BreakoutBrickSnapshot>(bricks);
        }

        private ReadOnlyCollection<BreakoutBallSnapshot> CaptureBallSnapshots()
        {
            var balls = new List<BreakoutBallSnapshot>(BallCount);
            if (!IsLaunched)
            {
                balls.Add(new BreakoutBallSnapshot(0, BallX, BallY, _options.BallRadius, BallVelocityX, BallVelocityY));
            }
            else
            {
                for (int i = 0; i < _balls.Count; i++)
                {
                    BreakoutBall ball = _balls[i];
                    balls.Add(new BreakoutBallSnapshot(ball.Id, ball.X, ball.Y, _options.BallRadius, ball.VelocityX, ball.VelocityY));
                }
            }

            return new ReadOnlyCollection<BreakoutBallSnapshot>(balls);
        }

        private ReadOnlyCollection<BreakoutBallState> CaptureBallStates()
        {
            var balls = new List<BreakoutBallState>(_balls.Count);
            for (int i = 0; i < _balls.Count; i++)
            {
                BreakoutBall ball = _balls[i];
                balls.Add(new BreakoutBallState(ball.Id, ball.X, ball.Y, ball.VelocityX, ball.VelocityY));
            }

            return new ReadOnlyCollection<BreakoutBallState>(balls);
        }

        private ReadOnlyCollection<int> CopyBrickTypes()
        {
            return CopyIntArray(_brickTypes);
        }

        private ReadOnlyCollection<int> CopyBrickHitPoints()
        {
            return CopyIntArray(_brickHitPoints);
        }

        private ReadOnlyCollection<int> CopyBrickPowerUps()
        {
            return CopyIntArray(_brickPowerUps);
        }

        private static ReadOnlyCollection<int> CopyIntArray(BreakoutBrickType[] source)
        {
            var values = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                values.Add((int)source[i]);
            }

            return new ReadOnlyCollection<int>(values);
        }

        private static ReadOnlyCollection<int> CopyIntArray(BreakoutPowerUpType[] source)
        {
            var values = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                values.Add((int)source[i]);
            }

            return new ReadOnlyCollection<int>(values);
        }

        private static ReadOnlyCollection<int> CopyIntArray(int[] source)
        {
            var values = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                values.Add(source[i]);
            }

            return new ReadOnlyCollection<int>(values);
        }

        private int ToBrickIndex(int row, int column)
        {
            if (row < 0 || row >= _options.BrickRows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Breakout brick row is outside the grid.");
            }

            if (column < 0 || column >= _options.BrickColumns)
            {
                throw new ArgumentOutOfRangeException(nameof(column), "Breakout brick column is outside the grid.");
            }

            return row * _options.BrickColumns + column;
        }

        private sealed class BreakoutBall
        {
            public BreakoutBall(int id, double x, double y, double velocityX, double velocityY)
            {
                Id = id;
                X = x;
                Y = y;
                VelocityX = velocityX;
                VelocityY = velocityY;
            }

            public int Id { get; }
            public double X { get; set; }
            public double Y { get; set; }
            public double VelocityX { get; set; }
            public double VelocityY { get; set; }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
