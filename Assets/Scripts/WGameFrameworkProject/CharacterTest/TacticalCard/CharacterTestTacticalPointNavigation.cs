using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal interface ICharacterTestWorldPointNavigationService
    {
        bool TryResolvePointMove(CharacterTestCharacterEntity actor, float x, float z, out Vector2 move);
    }

    /// <summary>Point adapter over the existing Combat Physics queries and bounded scene A*. No alternate movement owner.</summary>
    internal sealed class CharacterTestTacticalPointNavigation : ICharacterTestAiNavigationService, ICharacterTestWorldPointNavigationService,
        ITacticalPointValidationPort, ITacticalPointSelectionPort
    {
        private const int QueryId = 910300;
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly CharacterTestCharacterEntity _actor;
        private readonly CharacterTestCharacterPhysicsRuntime _physics;
        private readonly CharacterTestNavigationSession _navigation;
        private readonly CharacterTestNavigationPathBuffer _path;
        private readonly List<CombatQueryResult> _hits = new List<CombatQueryResult>(32);
        private TacticalWorldPoint _destination;
        private bool _hasDestination;
        private long _frame = -1, _sampledFrame = -2;
        private Vector2 _sampledMove;
        internal TacticalInputStatus LastStatus { get; private set; }

        internal CharacterTestTacticalPointNavigation(CharacterTestCharacterRuntimeModule runtime,
            CharacterTestCharacterEntity actor, CharacterTestNavigationSession navigation)
        {
            _runtime = runtime; _actor = actor; _physics = runtime?.PhysicsRuntime; _navigation = navigation;
            if (navigation?.IsAvailable == true) _path = new CharacterTestNavigationPathBuffer(navigation.Input.MaxPathNodeCount);
        }
        internal void BindDestination(TacticalWorldPoint point)
        { _destination = point; _hasDestination = true; _sampledFrame = -2; LastStatus = TacticalInputStatus.Valid; }
        internal void ClearDestination() { _hasDestination = false; _sampledFrame = -2; }
        internal void BeginFrame(long frame) => _frame = frame;
        public bool TryGetPointOrigin(out TacticalWorldPoint origin)
        {
            origin = default;
            if (!CurrentActor()) return false;
            origin = new TacticalWorldPoint(_actor.Transform.XMillimeters, _actor.Transform.YMillimeters, _actor.Transform.ZMillimeters);
            return true;
        }
        private bool CurrentActor() => _actor != null && _actor.CombatState.IsAlive &&
            ReferenceEquals(_runtime?.FindEntityById(_actor.Id.Value), _actor) && _actor.GameplayEntityId.IsValid;
        private Fix64 MinimumGroundNormal => CharacterTestDeterministicMath.CosFix64FromMilliDegrees(
            CharacterTestDeterministicMath.Clamp(CharacterTestDeterministicUnits.DegreesToMilliDegrees(
                _runtime.MovementProfile.Profile.SlopeLimitDegrees), 0, 89900));
        private static Fix64 Fix(int millimeters) => Fix64.FromRaw((long)millimeters * (Fix64.Scale / 1000));
        private static FixVector3 Fix(TacticalWorldPoint point) => new FixVector3(Fix(point.X), Fix(point.Y), Fix(point.Z));
        private CombatQueryHeader Header(CombatQueryKind kind, bool motion = false) => new CombatQueryHeader(QueryId, kind,
            _physics.GetEntityId(_actor), QueryId, 0, 0, motion ? _physics.MotionObstacleLayerMask : _physics.StaticObstacleLayerMask);

        public TacticalInputStatus TryPickGround(TacticalWorldPoint rayOrigin, TacticalWorldPoint rayDirection, out TacticalWorldPoint point)
        {
            point = default;
            if (!CurrentActor() || _physics?.IsReady != true) return TacticalInputStatus.GroundUnavailable;
            FixVector3 direction = Fix(rayDirection);
            if (direction.IsZero) return TacticalInputStatus.InvalidGround;
            _hits.Clear();
            _physics.World.QueryRay(new CombatRayQuery(Header(CombatQueryKind.Ray), Fix(rayOrigin), direction, Fix(100000)), _hits);
            if (_hits.Count == 0 || _hits[0].Normal.Y < MinimumGroundNormal) return TacticalInputStatus.InvalidGround;
            point = Point(_hits[0].Point);
            return ValidateGround(_actor.GameplayEntityId, point);
        }
        public TacticalInputStatus ValidateGround(GameplayEntityId actor, TacticalWorldPoint point)
        {
            if (!CurrentActor() || actor != _actor.GameplayEntityId || _physics?.IsReady != true) return TacticalInputStatus.GroundUnavailable;
            Fix64 rise = _physics.MaxStepHeight + _physics.GroundSnapDistance + _physics.SkinWidth;
            if (rise <= Fix64.Zero) rise = Fix(1);
            FixVector3 candidate = Fix(point);
            _hits.Clear();
            _physics.World.QueryRay(new CombatRayQuery(Header(CombatQueryKind.Ray), candidate + new FixVector3(Fix64.Zero, rise, Fix64.Zero),
                new FixVector3(Fix64.Zero, -Fix64.One, Fix64.Zero), rise + rise), _hits);
            if (_hits.Count == 0 || _hits[0].Normal.Y < MinimumGroundNormal
                || (_hits[0].Point.Y - candidate.Y).Abs() > _physics.SkinWidth + Fix(2)) return TacticalInputStatus.InvalidGround;
            return SegmentClear(candidate, candidate) ? TacticalInputStatus.Valid : TacticalInputStatus.InvalidGround;
        }
        // LowFreqAlloc caller activation; query buffers are reused and the exact endpoint is never snapped.
        internal bool TryValidateRecallDestination(GameplayEntityId actor, TacticalWorldPoint point, out MxFramework.Combat.Physics.CombatSurfaceMaterial material) =>
            TryValidateGroundPlacement(actor, point, out material);

        // Placement mechanics share geometry queries while retaining their own activation and consumption rules.
        internal bool TryValidateGroundPlacement(GameplayEntityId actor, TacticalWorldPoint point, out MxFramework.Combat.Physics.CombatSurfaceMaterial material)
        {
            material = default;
            if (!CurrentActor() || actor != _actor.GameplayEntityId || _physics?.IsReady != true) return false;
            return CharacterTestCharacterPlacementSafety.TryValidateGroundPlacement(_physics.World, Header(CombatQueryKind.Ray), Header(CombatQueryKind.Aabb, true),
                _physics.GetBodyId(_actor), Fix(point), _physics.GetCharacterHalfExtents(_actor), _physics.GetCharacterCenterOffsetY(_actor),
                _physics.SkinWidth, _physics.MaxStepHeight + _physics.GroundSnapDistance + _physics.SkinWidth, MinimumGroundNormal, _hits, out material);
        }
        public TacticalInputStatus ValidatePath(GameplayEntityId actor, TacticalWorldPoint origin, TacticalWorldPoint point)
        {
            if (!CurrentActor() || actor != _actor.GameplayEntityId || _physics?.IsReady != true) return TacticalInputStatus.PathUnavailable;
            if (SegmentClear(Fix(origin), Fix(point))) return TacticalInputStatus.Valid;
            if (_navigation?.IsAvailable != true || _path == null) return TacticalInputStatus.PathUnavailable;
            return FindPath(origin, point, out _) ? TacticalInputStatus.Valid : TacticalInputStatus.Unreachable;
        }
        internal TacticalInputStatus ValidateTraction(GameplayEntityId actor, TacticalWorldPoint origin, TacticalWorldPoint point, int range)
        {
            if (!CurrentActor() || actor != _actor.GameplayEntityId || _physics?.IsReady != true) return TacticalInputStatus.GroundUnavailable;
            long dx = (long)point.X - origin.X, dz = (long)point.Z - origin.Z;
            if (System.Math.Abs(dx) > range || System.Math.Abs(dz) > range || dx * dx + dz * dz > (long)range * range)
                return TacticalInputStatus.OutOfRange;
            var ground = ValidateGround(actor, point);
            if (ground != TacticalInputStatus.Valid) return ground;
            return SegmentClear(Fix(origin), Fix(point)) ? TacticalInputStatus.Valid : TacticalInputStatus.Unreachable;
        }
        private bool FindPath(TacticalWorldPoint origin, TacticalWorldPoint point, out CharacterTestPathfindingSample sample)
        {
            var half = _physics.GetCharacterHalfExtents(_actor);
            int radius = Millimeters(Fix64.Max(half.X, half.Z)), height = Millimeters(half.Y + half.Y);
            var query = new CharacterTestPathQuery(_navigation.Graph.Revision, _navigation.Graph.ContentHash, Fix(origin), Fix(point),
                radius, height, _navigation.Input.MaxEndpointSnapDistanceMillimeters, _navigation.Input.MaxNodeExpansions, _navigation.Input.MaxPathNodeCount);
            return _navigation.Pathfinder.TryFindPath(query, _path, out sample);
        }
        public bool TryResolvePointMove(CharacterTestCharacterEntity actor, float x, float z, out Vector2 move)
        {
            move = Vector2.zero;
            if (!ReferenceEquals(actor, _actor) || !CurrentActor() || _physics?.IsReady != true) return false;
            if (!_hasDestination)
            {
                Vector2 direct = new Vector2(x - actor.Transform.X, z - actor.Transform.Z);
                move = direct.sqrMagnitude > 0.000001f ? direct.normalized : Vector2.zero;
                return true;
            }
            if (_sampledFrame == _frame) { move = _sampledMove; return true; }
            _sampledFrame = _frame; _sampledMove = Vector2.zero;
            LastStatus = ValidateGround(_actor.GameplayEntityId, _destination);
            if (LastStatus != TacticalInputStatus.Valid) return true;
            TryGetPointOrigin(out var origin);
            TacticalWorldPoint waypoint = _destination;
            if (!SegmentClear(Fix(origin), Fix(_destination)))
            {
                if (_navigation?.IsAvailable != true || !FindPath(origin, _destination, out var sample) || !sample.HasNextWaypoint)
                { LastStatus = TacticalInputStatus.Unreachable; return true; }
                waypoint = Point(sample.NextWaypoint);
                if (!SegmentClear(Fix(origin), Fix(waypoint))) { LastStatus = TacticalInputStatus.Unreachable; return true; }
            }
            Vector2 delta = new Vector2((waypoint.X - (long)origin.X) / 1000f, (waypoint.Z - (long)origin.Z) / 1000f);
            _sampledMove = delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector2.zero;
            move = _sampledMove; return true;
        }
        public bool TryResolveMove(CharacterTestCharacterEntity self, CharacterTestCharacterEntity target, float range,
            out CharacterTestAiNavigationResult result) => CharacterTestDirectAiNavigationService.Instance.TryResolveMove(self, target, range, out result);
        private bool SegmentClear(FixVector3 start, FixVector3 end)
        {
            var half = _physics.GetCharacterHalfExtents(_actor);
            Fix64 skin = Fix64.Max(_physics.SkinWidth, Fix(1));
            Fix64 radius = Fix64.Max(Fix64.Max(half.X, half.Z) - skin, Fix(1));
            Fix64 offset = _physics.GetCharacterCenterOffsetY(_actor);
            Fix64 vertical = Fix64.Max(half.Y - radius - skin, Fix64.Zero);
            var ownBody = _physics.GetBodyId(_actor);
            // Cover the full height, including tall bodies; fail closed rather than leaving gaps between two tubes.
            int intervals = vertical <= Fix64.Zero ? 0 : checked((int)((vertical / radius).RawValue / Fix64.Scale) + 1);
            if (intervals > 16) return false;
            for (int slice = 0; slice <= intervals; slice++)
            {
                Fix64 y = intervals == 0 ? offset : offset - vertical + vertical * Fix64.FromInt(2 * slice) / Fix64.FromInt(intervals);
                FixVector3 center = new FixVector3(Fix64.Zero, y, Fix64.Zero);
                _hits.Clear();
                _physics.World.QueryCapsule(new CombatCapsuleQuery(Header(CombatQueryKind.Capsule, true), start + center, end + center, radius), _hits);
                for (int hit = 0; hit < _hits.Count; hit++) if (!_hits[hit].TargetBodyId.Equals(ownBody)) return false;
            }
            return true;
        }
        private static int Millimeters(Fix64 value) => checked((int)(value.RawValue / (Fix64.Scale / 1000)));
        private static TacticalWorldPoint Point(FixVector3 point) => new TacticalWorldPoint(Millimeters(point.X), Millimeters(point.Y), Millimeters(point.Z));
    }
}
