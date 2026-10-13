using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestNavigationProbeShape
    {
        Capsule = 1,
        SweptObb = 2
    }

    internal static class CharacterTestNavigationClearanceProbe
    {
        // A single conservative box encloses the swept standing capsule, including its end caps.
        // Ground skin and the motor's grounded step allowance are excluded from the lower band.
        internal static CombatPhysicsQuery CreateQuery(
            CombatQueryHeader header,
            CharacterTestCharacterPhysicsRuntime physics,
            CharacterTestCharacterEntity source,
            FixVector3 origin,
            FixVector3 direction,
            Fix64 length,
            FixVector3 halfExtents)
        {
            direction.TryNormalize(out FixVector3 forward);
            Fix64 radius = Fix64.Max(halfExtents.X, halfExtents.Z);
            Fix64 skin = Fix64.Max(physics.SkinWidth, Fix64.FromRaw(1));
            Fix64 lowerAllowance = skin + (source.IsGrounded ? physics.MaxStepHeight : Fix64.Zero);
            Fix64 height = halfExtents.Y + halfExtents.Y;
            lowerAllowance = Fix64.Min(lowerAllowance, height);
            Fix64 halfLength = length * Fix64.Half;
            FixVector3 center = origin + forward * halfLength
                + new FixVector3(Fix64.Zero, lowerAllowance * Fix64.Half, Fix64.Zero);
            return CombatPhysicsQuery.From(new CombatObbQuery(
                header,
                center,
                new FixVector3(halfLength + radius, (height - lowerAllowance) * Fix64.Half, radius),
                forward,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                new FixVector3(-forward.Z, Fix64.Zero, forward.X)));
        }
    }

    public readonly struct CharacterTestAiNavigationResult
    {
        public CharacterTestAiNavigationResult(
            Vector2 move,
            bool directPathClear,
            bool targetInAttackRange,
            int selectedCandidateIndex)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            DirectPathClear = directPathClear;
            TargetInAttackRange = targetInAttackRange;
            SelectedCandidateIndex = selectedCandidateIndex;
        }

        public Vector2 Move { get; }

        public bool DirectPathClear { get; }

        public bool TargetInAttackRange { get; }

        public int SelectedCandidateIndex { get; }
    }

    public interface ICharacterTestAiNavigationService
    {
        bool TryResolveMove(
            CharacterTestCharacterEntity self,
            CharacterTestCharacterEntity target,
            float attackRange,
            out CharacterTestAiNavigationResult result);
    }

    public sealed class CharacterTestDirectAiNavigationService : ICharacterTestAiNavigationService
    {
        public static readonly CharacterTestDirectAiNavigationService Instance =
            new CharacterTestDirectAiNavigationService();

        private const float DirectionEpsilon = 0.0001f;

        private CharacterTestDirectAiNavigationService()
        {
        }

        public bool TryResolveMove(
            CharacterTestCharacterEntity self,
            CharacterTestCharacterEntity target,
            float attackRange,
            out CharacterTestAiNavigationResult result)
        {
            result = default;
            if (self == null || target == null)
                return false;

            Vector2 toTarget = ToTarget(self, target);
            float sqrDistance = toTarget.sqrMagnitude;
            bool inAttackRange = sqrDistance <= attackRange * attackRange;
            Vector2 move = sqrDistance > DirectionEpsilon
                ? toTarget.normalized
                : Vector2.zero;
            result = new CharacterTestAiNavigationResult(
                move,
                directPathClear: true,
                targetInAttackRange: inAttackRange,
                selectedCandidateIndex: -1);
            return true;
        }

        internal static Vector2 ToTarget(
            CharacterTestCharacterEntity self,
            CharacterTestCharacterEntity target)
        {
            return new Vector2(
                target.Transform.X - self.Transform.X,
                target.Transform.Z - self.Transform.Z);
        }
    }

    public sealed class CharacterTestCombatPhysicsNavigationService : ICharacterTestAiNavigationService
    {
        private const float DirectionEpsilon = 0.0001f;
        private const int NavigationQueryId = 910100;
        private const int InitialQueryResultCapacity = 32;

        private readonly CharacterTestCharacterPhysicsRuntime _physicsRuntime;
        private readonly float _probeMinLength;
        private readonly float _probeMaxLength;
        private readonly float[] _candidateAngles;
        private readonly List<CombatQueryResult> _queryResults =
            new List<CombatQueryResult>(InitialQueryResultCapacity);
        private readonly CharacterTestPerceptionFrameCache _perceptionFrameCache;
        private readonly int _navigationProfileKey;
        private readonly CharacterTestNavigationSession _navigationSession;

        public CharacterTestCombatPhysicsNavigationService(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestAiBehaviorProfile profile = null)
            : this(physicsRuntime, profile, null)
        {
        }

        internal CharacterTestCombatPhysicsNavigationService(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestAiBehaviorProfile profile,
            CharacterTestPerceptionFrameCache perceptionFrameCache)
            : this(physicsRuntime, profile, perceptionFrameCache, null)
        {
        }

        internal CharacterTestCombatPhysicsNavigationService(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestAiBehaviorProfile profile,
            CharacterTestPerceptionFrameCache perceptionFrameCache,
            CharacterTestNavigationSession navigationSession)
        {
            _physicsRuntime = physicsRuntime;
            _perceptionFrameCache = perceptionFrameCache;
            _navigationSession = navigationSession;
            CharacterTestAiBehaviorProfile safeProfile =
                CharacterTestAiBehaviorProfileSource.Normalize(profile);
            _probeMinLength = safeProfile.NavigationProbeMinLength;
            _probeMaxLength = safeProfile.NavigationProbeMaxLength;
            _candidateAngles = safeProfile.CopyNavigationCandidateAngles();
            FixVector3 clearanceHalfExtents = physicsRuntime != null
                ? physicsRuntime.CharacterHalfExtents
                : new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);
            CombatPhysicsLayerMask obstacleLayerMask = physicsRuntime != null
                ? physicsRuntime.MotionObstacleLayerMask
                : CombatPhysicsLayerMask.None;
            _navigationProfileKey = ComputeNavigationProfileKey(
                _probeMinLength,
                _probeMaxLength,
                _candidateAngles,
                CharacterTestNavigationProbeShape.SweptObb,
                obstacleLayerMask,
                clearanceHalfExtents);
        }

        internal CharacterTestNavigationSession NavigationSession => _navigationSession;

        public bool TryResolveMove(
            CharacterTestCharacterEntity self,
            CharacterTestCharacterEntity target,
            float attackRange,
            out CharacterTestAiNavigationResult result)
        {
            result = default;
            if (self == null || target == null)
                return false;

            if (_physicsRuntime == null || !_physicsRuntime.IsReady)
            {
                return CharacterTestDirectAiNavigationService.Instance.TryResolveMove(
                    self,
                    target,
                    attackRange,
                    out result);
            }

            if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryGetNavigation(
                    self,
                    target,
                    attackRange,
                    _navigationProfileKey,
                    _probeMinLength,
                    _probeMaxLength,
                    _candidateAngles,
                    _navigationSession,
                    out CharacterTestNavigationSample navigationSample))
            {
                result = navigationSample.Result;
                return true;
            }

            Vector2 toTarget = CharacterTestDirectAiNavigationService.ToTarget(self, target);
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance <= DirectionEpsilon)
            {
                result = new CharacterTestAiNavigationResult(
                    Vector2.zero,
                    directPathClear: true,
                    targetInAttackRange: true,
                    selectedCandidateIndex: -1);
                return true;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            Vector2 directMove = toTarget / distance;
            bool directPathClear = IsDirectionClear(
                self,
                target,
                directMove,
                distance,
                candidateIndex: -1);
            if (directPathClear)
            {
                result = new CharacterTestAiNavigationResult(
                    directMove,
                    directPathClear: true,
                    targetInAttackRange: distance <= attackRange,
                    selectedCandidateIndex: -1);
                return true;
            }

            for (int i = 0; i < _candidateAngles.Length; i++)
            {
                Vector2 candidateMove = Rotate(directMove, _candidateAngles[i]);
                if (!IsDirectionClear(self, target, candidateMove, distance, i))
                    continue;

                result = new CharacterTestAiNavigationResult(
                    candidateMove,
                    directPathClear: false,
                    targetInAttackRange: false,
                    selectedCandidateIndex: i);
                return true;
            }

            result = new CharacterTestAiNavigationResult(
                Vector2.zero,
                directPathClear: false,
                targetInAttackRange: false,
                selectedCandidateIndex: _candidateAngles.Length);
            return true;
        }

        private bool IsDirectionClear(
            CharacterTestCharacterEntity self,
            CharacterTestCharacterEntity target,
            Vector2 direction,
            float distance,
            int candidateIndex)
        {
            if (direction.sqrMagnitude <= DirectionEpsilon)
                return true;

            Fix64 probeLength = ToFix64(Mathf.Clamp(distance, _probeMinLength, _probeMaxLength));
            FixVector3 origin = ResolveEntityCenter(self);
            FixVector3 probeDirection = new FixVector3(
                ToFix64(direction.x),
                Fix64.Zero,
                ToFix64(direction.y));
            var header = new CombatQueryHeader(
                queryId: NavigationQueryId + candidateIndex + 1,
                kind: CombatQueryKind.Obb,
                sourceEntityId: _physicsRuntime.GetEntityId(self),
                traceId: NavigationQueryId,
                actionId: 0,
                sourceOrder: candidateIndex + 1,
                layerMask: _physicsRuntime.MotionObstacleLayerMask);

            _queryResults.Clear();
            _physicsRuntime.World.Query(
                CharacterTestNavigationClearanceProbe.CreateQuery(
                    header, _physicsRuntime, self, origin, probeDirection, probeLength,
                    _physicsRuntime.GetCharacterHalfExtents(self)),
                _queryResults);
            CombatEntityId ignoredTargetEntityId = _physicsRuntime.GetEntityId(target);
            for (int i = 0; i < _queryResults.Count; i++)
            {
                if (ignoredTargetEntityId.IsNone
                    || !_queryResults[i].TargetEntityId.Equals(ignoredTargetEntityId))
                {
                    return false;
                }
            }

            return true;
        }

        private FixVector3 ResolveEntityCenter(CharacterTestCharacterEntity entity)
        {
            CombatBodyId bodyId = _physicsRuntime.GetBodyId(entity);
            if (_physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body))
                return body.Position;

            return new FixVector3(
                ToFix64(entity.Transform.X),
                ToFix64(entity.Transform.Y) + _physicsRuntime.GetCharacterCenterOffsetY(entity),
                ToFix64(entity.Transform.Z));
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos).normalized;
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static int ComputeNavigationProfileKey(
            float probeMinLength,
            float probeMaxLength,
            float[] candidateAngles,
            CharacterTestNavigationProbeShape probeShape,
            CombatPhysicsLayerMask layerMask,
            FixVector3 clearanceHalfExtents)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + CharacterTestDeterministicUnits.MetersToMillimeters(probeMinLength);
                hash = hash * 31 + CharacterTestDeterministicUnits.MetersToMillimeters(probeMaxLength);
                hash = hash * 31 + (int)probeShape;
                hash = hash * 31 + layerMask.Value;
                hash = hash * 31 + ToMillimeters(clearanceHalfExtents.X > clearanceHalfExtents.Z
                    ? clearanceHalfExtents.X
                    : clearanceHalfExtents.Z);
                hash = hash * 31 + ToMillimeters(clearanceHalfExtents.Y + clearanceHalfExtents.Y);
                int count = candidateAngles != null ? candidateAngles.Length : 0;
                hash = hash * 31 + count;
                for (int i = 0; i < count; i++)
                {
                    hash = hash * 31 + CharacterTestDeterministicUnits.DegreesToMilliDegreesUnnormalized(
                        candidateAngles[i]);
                }

                return hash;
            }
        }

        private static int ToMillimeters(Fix64 value)
        {
            return CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                value < Fix64.Zero ? Fix64.Zero : value);
        }
    }
}
