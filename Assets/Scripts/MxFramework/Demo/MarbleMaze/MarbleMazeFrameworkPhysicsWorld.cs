using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Demo.MarbleMaze
{
    public sealed class MarbleMazeFrameworkPhysicsWorld
    {
        private const int SourceEntity = 1;
        private const int WallLayer = 1;
        private const int GoalLayer = 2;
        private const int ExitCollider = 100;
        private const double BoardHalfExtent = 4.25d;
        private const double WallThickness = 0.36d;

        private readonly CombatPhysicsWorld _world = new CombatPhysicsWorld();
        private readonly List<CombatQueryResult> _hits = new List<CombatQueryResult>(8);
        private readonly MarbleMazeVector3[] _checkpoints;
        private readonly MarbleMazeVector3 _exit;
        private readonly double _ballRadius;
        private readonly double _targetRadius;
        private readonly double _acceleration;
        private readonly double _damping;
        private readonly double _bounce;
        private MarbleMazeVector3 _position;
        private MarbleMazeVector3 _velocity;
        private int _queryId;

        public MarbleMazeFrameworkPhysicsWorld(
            IReadOnlyList<MarbleMazeVector3> checkpoints,
            MarbleMazeVector3 exit,
            double ballRadius = 0.325d,
            double targetRadius = 0.52d,
            double acceleration = 7.5d,
            double damping = 0.985d,
            double bounce = 0.24d)
        {
            if (checkpoints == null)
                throw new ArgumentNullException(nameof(checkpoints));
            if (checkpoints.Count == 0)
                throw new ArgumentException("Marble Maze physics needs at least one checkpoint.", nameof(checkpoints));
            if (ballRadius <= 0d || targetRadius <= 0d || acceleration < 0d || damping <= 0d || bounce < 0d)
                throw new ArgumentOutOfRangeException(nameof(ballRadius), "Marble Maze physics values must be positive.");

            _checkpoints = new MarbleMazeVector3[checkpoints.Count];
            for (int i = 0; i < checkpoints.Count; i++)
                _checkpoints[i] = checkpoints[i];
            _exit = exit;
            _ballRadius = ballRadius;
            _targetRadius = targetRadius;
            _acceleration = acceleration;
            _damping = damping;
            _bounce = bounce;
            BuildWorld();
        }

        public MarbleMazeVector3 Position => _position;
        public MarbleMazeVector3 Velocity => _velocity;
        public int ColliderCount => _world.ColliderCount;

        public void Reset(MarbleMazeVector3 position)
        {
            Reset(position, MarbleMazeVector3.Zero);
        }

        public void Reset(MarbleMazeVector3 position, MarbleMazeVector3 velocity)
        {
            _position = position;
            _velocity = velocity;
        }

        public MarbleMazePhysicsStepResult Step(double deltaTime, double tiltX, double tiltZ, int nextCheckpointIndex)
        {
            deltaTime = Math.Max(0d, Math.Min(0.1d, deltaTime));
            MarbleMazeVector3 velocity = new MarbleMazeVector3(
                (_velocity.X + (tiltX * _acceleration * deltaTime)) * _damping,
                0d,
                (_velocity.Z + (tiltZ * _acceleration * deltaTime)) * _damping);
            MarbleMazeVector3 proposed = new MarbleMazeVector3(
                _position.X + (velocity.X * deltaTime),
                _position.Y,
                _position.Z + (velocity.Z * deltaTime));

            bool wallHit = QuerySphere(proposed, _ballRadius, WallLayer).Count > 0;
            if (wallHit)
            {
                double min = -BoardHalfExtent + _ballRadius;
                double max = BoardHalfExtent - _ballRadius;
                double clampedX = Math.Max(min, Math.Min(max, proposed.X));
                double clampedZ = Math.Max(min, Math.Min(max, proposed.Z));
                if (Math.Abs(clampedX - proposed.X) > 0.0001d)
                    velocity = new MarbleMazeVector3(-velocity.X * _bounce, 0d, velocity.Z);
                if (Math.Abs(clampedZ - proposed.Z) > 0.0001d)
                    velocity = new MarbleMazeVector3(velocity.X, 0d, -velocity.Z * _bounce);

                proposed = new MarbleMazeVector3(clampedX, _position.Y, clampedZ);
            }

            _position = proposed;
            _velocity = velocity;

            int checkpointHit = -1;
            bool exitHit = false;
            IReadOnlyList<CombatQueryResult> goalHits = QuerySphere(_position, _ballRadius, GoalLayer);
            for (int i = 0; i < goalHits.Count; i++)
            {
                int colliderId = goalHits[i].TargetColliderId.Value;
                if (colliderId == ExitCollider)
                {
                    exitHit = true;
                }
                else if (colliderId - 1 == nextCheckpointIndex)
                {
                    checkpointHit = nextCheckpointIndex;
                }
            }

            return new MarbleMazePhysicsStepResult(_position, _velocity, wallHit, checkpointHit, exitHit);
        }

        private IReadOnlyList<CombatQueryResult> QuerySphere(MarbleMazeVector3 position, double radius, int layer)
        {
            _hits.Clear();
            var query = new CombatSphereQuery(
                Header(CombatQueryKind.Sphere, CombatPhysicsLayerMask.FromLayer(layer)),
                ToFix(position),
                ToFix(radius));
            _world.QuerySphere(query, _hits);
            return _hits;
        }

        private void BuildWorld()
        {
            RegisterAabb(10, WallLayer, 0d, 0d, BoardHalfExtent + (WallThickness * 0.5d), BoardHalfExtent + WallThickness, WallThickness);
            RegisterAabb(11, WallLayer, 0d, 0d, -BoardHalfExtent - (WallThickness * 0.5d), BoardHalfExtent + WallThickness, WallThickness);
            RegisterAabb(12, WallLayer, BoardHalfExtent + (WallThickness * 0.5d), 0d, 0d, WallThickness, BoardHalfExtent + WallThickness);
            RegisterAabb(13, WallLayer, -BoardHalfExtent - (WallThickness * 0.5d), 0d, 0d, WallThickness, BoardHalfExtent + WallThickness);

            for (int i = 0; i < _checkpoints.Length; i++)
                RegisterAabb(1 + i, GoalLayer, _checkpoints[i].X, _checkpoints[i].Y, _checkpoints[i].Z, _targetRadius, _targetRadius);
            RegisterAabb(ExitCollider, GoalLayer, _exit.X, _exit.Y, _exit.Z, _targetRadius, _targetRadius);
        }

        private void RegisterAabb(int id, int layer, double x, double y, double z, double halfX, double halfZ)
        {
            var entity = new CombatEntityId(1000 + id);
            var body = new CombatBodyId(1000 + id);
            _world.UpsertBody(new CombatPhysicsBody(entity, body, new FixVector3(ToFix(x), ToFix(y), ToFix(z))));
            _world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                body,
                new CombatColliderId(id),
                layer,
                new FixVector3(ToFix(-halfX), ToFix(-0.2d), ToFix(-halfZ)),
                new FixVector3(ToFix(halfX), ToFix(0.2d), ToFix(halfZ))));
        }

        private CombatQueryHeader Header(CombatQueryKind kind, CombatPhysicsLayerMask layerMask)
        {
            _queryId++;
            return new CombatQueryHeader(
                _queryId,
                kind,
                new CombatEntityId(SourceEntity),
                traceId: _queryId,
                actionId: 1,
                sourceOrder: _queryId,
                layerMask);
        }

        private static FixVector3 ToFix(MarbleMazeVector3 value)
        {
            return new FixVector3(ToFix(value.X), ToFix(value.Y), ToFix(value.Z));
        }

        private static Fix64 ToFix(double value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }

    public readonly struct MarbleMazePhysicsStepResult
    {
        public MarbleMazePhysicsStepResult(
            MarbleMazeVector3 position,
            MarbleMazeVector3 velocity,
            bool wallHit,
            int checkpointHit,
            bool exitHit)
        {
            Position = position;
            Velocity = velocity;
            WallHit = wallHit;
            CheckpointHit = checkpointHit;
            ExitHit = exitHit;
        }

        public MarbleMazeVector3 Position { get; }
        public MarbleMazeVector3 Velocity { get; }
        public bool WallHit { get; }
        public int CheckpointHit { get; }
        public bool ExitHit { get; }
    }
}
