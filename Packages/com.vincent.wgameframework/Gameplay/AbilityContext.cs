using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Context for one ability cast, carrying the caster and target candidates.</summary>
    public readonly struct AbilityContext
    {
        public readonly IRuntimeEntity Caster;
        public readonly IReadOnlyList<IRuntimeEntity> Candidates;

        public AbilityContext(IRuntimeEntity caster, IReadOnlyList<IRuntimeEntity> candidates)
        {
            Caster = caster;
            Candidates = candidates;
        }
    }
}
