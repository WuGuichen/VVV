using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatHitCollector
    {
        private readonly HashSet<WeaponHitOnceKey> _seen = new HashSet<WeaponHitOnceKey>();
        private readonly List<HitCandidate> _sortedCandidates = new List<HitCandidate>();

        public int Collect(List<HitCandidate> rawCandidates, List<HitCandidate> deduplicated)
        {
            if (rawCandidates == null)
            {
                throw new ArgumentNullException(nameof(rawCandidates));
            }

            if (deduplicated == null)
            {
                throw new ArgumentNullException(nameof(deduplicated));
            }

            _sortedCandidates.Clear();
            for (int i = 0; i < rawCandidates.Count; i++)
            {
                _sortedCandidates.Add(rawCandidates[i]);
            }

            _sortedCandidates.Sort();

            int startCount = deduplicated.Count;
            for (int i = 0; i < _sortedCandidates.Count; i++)
            {
                HitCandidate candidate = _sortedCandidates[i];
                if (_seen.Add(candidate.HitOnceKey))
                {
                    deduplicated.Add(candidate);
                }
            }

            return deduplicated.Count - startCount;
        }

        public void Reset()
        {
            _seen.Clear();
        }
    }
}
