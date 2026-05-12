using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Hit
{
    public sealed class HitResolveSystem
    {
        private readonly List<HitCandidate> _sortedCandidates = new List<HitCandidate>();

        public int Resolve(
            IReadOnlyList<HitCandidate> candidates,
            ISet<WeaponHitOnceKey> consumedHitOnceKeys,
            List<HitResolveResult> results)
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
                    results.Add(CreateResult(candidate, HitResolveKind.Duplicate, 0, 0, FixVector3.Zero));
                    continue;
                }

                results.Add(ResolveSingle(candidate));
            }

            return results.Count - startCount;
        }

        private static HitResolveResult ResolveSingle(HitCandidate candidate)
        {
            HitTargetStateFlags state = candidate.TargetState;
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
                return CreateResult(candidate, HitResolveKind.Blocked, 0, 0, FixVector3.Zero);
            }

            int staggerFrames = (state & HitTargetStateFlags.SuperArmor) != 0 ? 0 : candidate.StaggerFrames;
            return CreateResult(candidate, HitResolveKind.Damage, candidate.Damage, staggerFrames, candidate.Knockback);
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
