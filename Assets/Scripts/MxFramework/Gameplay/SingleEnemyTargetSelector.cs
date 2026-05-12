using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Selects the first alive candidate on a different team from the caster.</summary>
    public sealed class SingleEnemyTargetSelector : ITargetSelector
    {
        private static readonly IReadOnlyList<IRuntimeEntity> Empty = new IRuntimeEntity[0];

        public IReadOnlyList<IRuntimeEntity> SelectTargets(AbilityContext context)
        {
            if (context.Caster == null || context.Candidates == null)
                return Empty;

            for (int i = 0; i < context.Candidates.Count; i++)
            {
                IRuntimeEntity candidate = context.Candidates[i];
                if (candidate != null && candidate.TeamId != context.Caster.TeamId && candidate.IsAlive)
                    return new IRuntimeEntity[] { candidate };
            }

            return Empty;
        }
    }
}
