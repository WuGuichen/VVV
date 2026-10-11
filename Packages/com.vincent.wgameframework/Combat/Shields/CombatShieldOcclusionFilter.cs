using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;

namespace MxFramework.Combat.Shields
{
    public readonly struct CombatShieldOcclusionHit : IEquatable<CombatShieldOcclusionHit>
    {
        public CombatShieldOcclusionHit(HitCandidate candidate, CombatShieldPotencyComponent shield)
        {
            Candidate = candidate;
            Shield = shield;
        }

        public HitCandidate Candidate { get; }
        public CombatShieldPotencyComponent Shield { get; }

        public bool Equals(CombatShieldOcclusionHit other)
        {
            return Candidate.Equals(other.Candidate) && Shield.Equals(other.Shield);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldOcclusionHit other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Candidate.GetHashCode() * 397) ^ Shield.GetHashCode();
            }
        }
    }

    public readonly struct CombatShieldQueryHit : IEquatable<CombatShieldQueryHit>
    {
        public CombatShieldQueryHit(CombatQueryResult queryResult, CombatShieldPotencyComponent shield)
        {
            QueryResult = queryResult;
            Shield = shield;
        }

        public CombatQueryResult QueryResult { get; }
        public CombatShieldPotencyComponent Shield { get; }

        public bool Equals(CombatShieldQueryHit other)
        {
            return QueryResult.Equals(other.QueryResult) && Shield.Equals(other.Shield);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatShieldQueryHit other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (QueryResult.GetHashCode() * 397) ^ Shield.GetHashCode();
            }
        }
    }

    public sealed class CombatShieldOcclusionFilter
    {
        private readonly Dictionary<TraceKey, NearestShieldHit> _nearestShieldByTrace =
            new Dictionary<TraceKey, NearestShieldHit>();

        /// <remarks>NoAlloc on the steady-state path after caller/result buffers and internal dictionary capacity are warm.</remarks>
        public int FilterWeaponTraceCandidates(
            IReadOnlyList<HitCandidate> candidates,
            CombatShieldRuntime shieldRuntime,
            List<HitCandidate> visibleCandidates,
            List<CombatShieldOcclusionHit> shieldHits)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (shieldRuntime == null)
                throw new ArgumentNullException(nameof(shieldRuntime));
            if (visibleCandidates == null)
                throw new ArgumentNullException(nameof(visibleCandidates));
            if (shieldHits == null)
                throw new ArgumentNullException(nameof(shieldHits));

            _nearestShieldByTrace.Clear();
            int visibleStart = visibleCandidates.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                HitCandidate candidate = candidates[i];
                if (!TryResolveShield(candidate, shieldRuntime, out CombatShieldPotencyComponent shield))
                    continue;

                var key = TraceKey.From(candidate);
                var hit = new NearestShieldHit(candidate, shield);
                if (!_nearestShieldByTrace.TryGetValue(key, out NearestShieldHit existing)
                    || candidate.PhysicsHit.Distance < existing.Candidate.PhysicsHit.Distance)
                {
                    _nearestShieldByTrace[key] = hit;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                HitCandidate candidate = candidates[i];
                var key = TraceKey.From(candidate);
                if (!_nearestShieldByTrace.TryGetValue(key, out NearestShieldHit nearestShield))
                {
                    visibleCandidates.Add(candidate);
                    continue;
                }

                if (candidate.TargetId.Equals(nearestShield.Shield.ShieldEntityId)
                    || IsShieldCollider(candidate, nearestShield.Shield))
                {
                    if (candidate.Equals(nearestShield.Candidate))
                        shieldHits.Add(new CombatShieldOcclusionHit(candidate, nearestShield.Shield));
                    continue;
                }

                if (candidate.TargetId.Equals(nearestShield.Shield.OwnerEntityId)
                    && candidate.PhysicsHit.Distance >= nearestShield.Candidate.PhysicsHit.Distance)
                {
                    continue;
                }

                visibleCandidates.Add(candidate);
            }

            return visibleCandidates.Count - visibleStart;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryFindNearestShieldHit(
            IReadOnlyList<CombatQueryResult> queryResults,
            CombatShieldRuntime shieldRuntime,
            out CombatShieldQueryHit shieldHit)
        {
            if (queryResults == null)
                throw new ArgumentNullException(nameof(queryResults));
            if (shieldRuntime == null)
                throw new ArgumentNullException(nameof(shieldRuntime));

            bool found = false;
            CombatQueryResult nearestResult = default;
            CombatShieldPotencyComponent nearestShield = default;
            for (int i = 0; i < queryResults.Count; i++)
            {
                CombatQueryResult queryResult = queryResults[i];
                if (!TryResolveShield(queryResult, shieldRuntime, out CombatShieldPotencyComponent shield))
                    continue;

                if (!found || queryResult.Distance < nearestResult.Distance)
                {
                    found = true;
                    nearestResult = queryResult;
                    nearestShield = shield;
                }
            }

            if (found)
            {
                shieldHit = new CombatShieldQueryHit(nearestResult, nearestShield);
                return true;
            }

            shieldHit = default;
            return false;
        }

        private static bool TryResolveShield(
            HitCandidate candidate,
            CombatShieldRuntime shieldRuntime,
            out CombatShieldPotencyComponent shield)
        {
            return shieldRuntime.TryGetActiveShield(candidate.TargetId, out shield)
                || shieldRuntime.TryGetActiveShieldByCollider(
                    candidate.PhysicsHit.TargetBodyId,
                    candidate.PhysicsHit.TargetColliderId,
                    out shield);
        }

        private static bool TryResolveShield(
            CombatQueryResult queryResult,
            CombatShieldRuntime shieldRuntime,
            out CombatShieldPotencyComponent shield)
        {
            return shieldRuntime.TryGetActiveShield(queryResult.TargetEntityId, out shield)
                || shieldRuntime.TryGetActiveShieldByCollider(
                    queryResult.TargetBodyId,
                    queryResult.TargetColliderId,
                    out shield);
        }

        private static bool IsShieldCollider(HitCandidate candidate, CombatShieldPotencyComponent shield)
        {
            return candidate.PhysicsHit.TargetBodyId.Equals(shield.BodyId)
                && candidate.PhysicsHit.TargetColliderId.Equals(shield.ColliderId);
        }

        private readonly struct TraceKey : IEquatable<TraceKey>
        {
            private TraceKey(
                CombatEntityId attackerId,
                int actionId,
                int actionInstanceId,
                int traceId,
                CombatFrame frame)
            {
                AttackerId = attackerId;
                ActionId = actionId;
                ActionInstanceId = actionInstanceId;
                TraceId = traceId;
                Frame = frame;
            }

            public CombatEntityId AttackerId { get; }
            public int ActionId { get; }
            public int ActionInstanceId { get; }
            public int TraceId { get; }
            public CombatFrame Frame { get; }

            public static TraceKey From(HitCandidate candidate)
            {
                return new TraceKey(
                    candidate.AttackerId,
                    candidate.ActionId,
                    candidate.ActionInstanceId,
                    candidate.TraceId,
                    candidate.Frame);
            }

            public bool Equals(TraceKey other)
            {
                return AttackerId.Equals(other.AttackerId)
                    && ActionId == other.ActionId
                    && ActionInstanceId == other.ActionInstanceId
                    && TraceId == other.TraceId
                    && Frame.Equals(other.Frame);
            }

            public override bool Equals(object obj)
            {
                return obj is TraceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = AttackerId.Value;
                    hash = (hash * 397) ^ ActionId;
                    hash = (hash * 397) ^ ActionInstanceId;
                    hash = (hash * 397) ^ TraceId;
                    hash = (hash * 397) ^ Frame.Value;
                    return hash;
                }
            }
        }

        private readonly struct NearestShieldHit
        {
            public NearestShieldHit(HitCandidate candidate, CombatShieldPotencyComponent shield)
            {
                Candidate = candidate;
                Shield = shield;
            }

            public HitCandidate Candidate { get; }
            public CombatShieldPotencyComponent Shield { get; }
        }
    }
}
