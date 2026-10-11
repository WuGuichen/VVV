using System.Collections.Generic;
using MxFramework.Combat.Hit;

namespace MxFramework.Combat.Animation
{
    public interface ICombatAnimationContext
    {
        CombatActionRunner ActionRunner { get; }

        void SetActionRunner(CombatActionRunner runner);

        IReadOnlyList<HitCandidate> LastFrameHitCandidates { get; }

        void SetLastFrameHitCandidates(List<HitCandidate> candidates);

        CombatAnimationSnapshot? LastSnapshot { get; }

        void SetLastSnapshot(CombatAnimationSnapshot snapshot);
    }
}
