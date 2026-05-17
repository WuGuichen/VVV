using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Hit
{
    public sealed class HitResolveSystem
    {
        private readonly List<HitCandidate> _sortedCandidates = new List<HitCandidate>();
        private ICombatEventDispatcher _eventDispatcher;

        public void SetEventDispatcher(ICombatEventDispatcher eventDispatcher)
        {
            _eventDispatcher = eventDispatcher;
        }

        public int Resolve(
            IReadOnlyList<HitCandidate> candidates,
            ISet<WeaponHitOnceKey> consumedHitOnceKeys,
            List<HitResolveResult> results)
        {
            return Resolve(
                candidates,
                consumedHitOnceKeys,
                results,
                teamRelationProvider: null,
                targetStateResolver: null,
                allowFriendlyFire: false);
        }

        public int Resolve(
            IReadOnlyList<HitCandidate> candidates,
            ISet<WeaponHitOnceKey> consumedHitOnceKeys,
            List<HitResolveResult> results,
            ITeamRelationProvider teamRelationProvider,
            bool allowFriendlyFire = false)
        {
            return Resolve(
                candidates,
                consumedHitOnceKeys,
                results,
                teamRelationProvider,
                targetStateResolver: null,
                allowFriendlyFire);
        }

        public int Resolve(
            IReadOnlyList<HitCandidate> candidates,
            ISet<WeaponHitOnceKey> consumedHitOnceKeys,
            List<HitResolveResult> results,
            IHitTargetStateResolver targetStateResolver)
        {
            return Resolve(
                candidates,
                consumedHitOnceKeys,
                results,
                teamRelationProvider: null,
                targetStateResolver,
                allowFriendlyFire: false);
        }

        public int Resolve(
            IReadOnlyList<HitCandidate> candidates,
            ISet<WeaponHitOnceKey> consumedHitOnceKeys,
            List<HitResolveResult> results,
            ITeamRelationProvider teamRelationProvider,
            IHitTargetStateResolver targetStateResolver,
            bool allowFriendlyFire = false)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (consumedHitOnceKeys == null)
            {
                throw new ArgumentNullException(nameof(consumedHitOnceKeys));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            _sortedCandidates.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                _sortedCandidates.Add(candidates[i]);
            }

            _sortedCandidates.Sort();

            int startCount = results.Count;
            for (int i = 0; i < _sortedCandidates.Count; i++)
            {
                HitCandidate candidate = _sortedCandidates[i];
                WeaponHitOnceKey hitOnceKey = candidate.HitOnceKey;
                if (!consumedHitOnceKeys.Add(hitOnceKey))
                {
                    AddResult(results, CreateResult(candidate, HitResolveKind.Duplicate, 0, 0, FixVector3.Zero));
                    continue;
                }

                AddResult(results, ResolveSingle(candidate, teamRelationProvider, targetStateResolver, allowFriendlyFire));
            }

            return results.Count - startCount;
        }

        private HitResolveResult ResolveSingle(
            HitCandidate candidate,
            ITeamRelationProvider teamRelationProvider,
            IHitTargetStateResolver targetStateResolver,
            bool allowFriendlyFire)
        {
            if (candidate.AttackerId.Equals(candidate.TargetId))
            {
                return CreateResult(candidate, HitResolveKind.SelfDamage, 0, 0, FixVector3.Zero);
            }

            if (!allowFriendlyFire
                && teamRelationProvider != null
                && !teamRelationProvider.AreHostile(candidate.AttackerId, candidate.TargetId)
                && (teamRelationProvider.IsSameTeam(candidate.AttackerId, candidate.TargetId)
                    || teamRelationProvider.AreFriendly(candidate.AttackerId, candidate.TargetId)))
            {
                return CreateResult(candidate, HitResolveKind.Friendly, 0, 0, FixVector3.Zero);
            }

            HitTargetStateFlags state = targetStateResolver == null
                ? candidate.TargetState
                : targetStateResolver.ResolveTargetState(candidate.TargetId);

            if ((state & HitTargetStateFlags.Alive) == 0)
            {
                return CreateResult(candidate, HitResolveKind.TargetDead, 0, 0, FixVector3.Zero);
            }

            if ((state & HitTargetStateFlags.Invincible) != 0)
            {
                return CreateResult(candidate, HitResolveKind.Invincible, 0, 0, FixVector3.Zero);
            }

            if ((state & HitTargetStateFlags.Parrying) != 0)
            {
                return CreateResult(candidate, HitResolveKind.Parried, 0, 0, FixVector3.Zero);
            }

            if ((state & HitTargetStateFlags.Blocking) != 0)
            {
                return CreateResult(candidate, HitResolveKind.Blocked, candidate.Damage, 0, FixVector3.Zero);
            }

            int staggerFrames = (state & HitTargetStateFlags.SuperArmor) != 0 ? 0 : candidate.StaggerFrames;
            return CreateResult(candidate, HitResolveKind.Damage, candidate.Damage, staggerFrames, candidate.Knockback);
        }

        private void AddResult(List<HitResolveResult> results, HitResolveResult result)
        {
            results.Add(result);
            _eventDispatcher?.DispatchHitResolved(result);
            if (result.Kind == HitResolveKind.Blocked)
            {
                _eventDispatcher?.DispatchHitBlocked(result.AttackerId, result.TargetId, result.ActionId, result.Frame);
            }
        }

        private static HitResolveResult CreateResult(
            HitCandidate candidate,
            HitResolveKind kind,
            int damage,
            int staggerFrames,
            FixVector3 knockback)
        {
            return new HitResolveResult(
                candidate.AttackerId,
                candidate.TargetId,
                candidate.ActionId,
                candidate.ActionInstanceId,
                candidate.TraceId,
                candidate.Frame,
                kind,
                damage,
                staggerFrames,
                knockback);
        }
    }
}
