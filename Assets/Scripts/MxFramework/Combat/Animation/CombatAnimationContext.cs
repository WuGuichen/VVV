using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatAnimationContext : ICombatAnimationContext
    {
        private readonly List<HitCandidate> _lastFrameHitCandidates = new List<HitCandidate>();
        private CombatActionRunner _actionRunner;
        private CombatAnimationSnapshot? _lastSnapshot;

        public CombatActionRunner ActionRunner
        {
            get
            {
                if (_actionRunner == null)
                {
                    throw new InvalidOperationException("Combat action runner is not initialized.");
                }

                return _actionRunner;
            }
        }

        public IReadOnlyList<HitCandidate> LastFrameHitCandidates => _lastFrameHitCandidates;

        public CombatAnimationSnapshot? LastSnapshot => _lastSnapshot;

        public void SetActionRunner(CombatActionRunner runner)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            if (_actionRunner != null)
            {
                throw new InvalidOperationException("Combat action runner is already initialized.");
            }

            _actionRunner = runner;
        }

        public void SetLastFrameHitCandidates(List<HitCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            _lastFrameHitCandidates.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                _lastFrameHitCandidates.Add(candidates[i]);
            }
        }

        public void SetLastSnapshot(CombatAnimationSnapshot snapshot)
        {
            _lastSnapshot = snapshot;
        }
    }
}
